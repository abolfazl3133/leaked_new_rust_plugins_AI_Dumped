// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ConVar;
using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.ClansExtensionMethods;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Enumerable = System.Linq.Enumerable;
using Pool = Facepunch.Pool;
using Random = UnityEngine.Random;

#if TESTING
using System.Diagnostics;
#endif

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("Clans", "rustmods.ru", "1.1.42")]
	public class Clans : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			Skinner = null,
			ArenaTournament = null,
			BetterChat = null,
			IQChat = null,
			ZoneManager = null,
			PlayTimeRewards = null,
			PlayerDatabase = null;

        private const bool LangRu = false;
        
		private static Clans _instance;

#if CARBON
        private ImageDatabaseModule imageDatabase;
#endif

		private const string
			Layer = "UI.Clans",
			ModalLayer = "UI.Clans.Modal",
			PermAdmin = "clans.admin",
			COLORED_LABEL = "<color={0}>{1}</color>";

		private Coroutine _actionAvatars,
			_actionConvert,
			_initTopHandle,
			_topHandle;

		private bool _enabledSkins, _enabledImageLibrary;

		private Regex _tagFilter, _hexFilter = new("^([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");

		private readonly List<ItemDefinition> _defaultItems = new();

		private HashSet<ulong> _lootedEntities = new();

		private readonly Dictionary<ulong, ulong> _looters = new();

		private Dictionary<string, ClanData> _playerToClan = new();

		private int _lastPlayerTop;

		private readonly HashSet<ulong> _openedUI = new();

		#region Pages

		private const int
			ABOUT_CLAN = 0,
			MEMBERS_LIST = 1,
			CLANS_TOP = 2,
			PLAYERS_TOP = 3,
			GATHER_RATES = 4,
			SKINS_PAGE = 6,
			PLAYERS_LIST = 5,
			ALIANCES_LIST = 7;

		#endregion

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = LangRu ? "Настройки аватара" : "Avatar Settings")]
			public AvatarSettings Avatar = new()
			{
				DefaultAvatar = "https://i.ibb.co/q97QG6c/image.png",
				CanOwner = true,
				CanModerator = false,
				CanMember = false,
				PermissionToChange = string.Empty,
				SteamAPIKey = string.Empty
			};

			[JsonProperty(PropertyName = LangRu ? "Команды" : "Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> ClanCommands = new()
			{
				"clan", "clans"
			};

			[JsonProperty(PropertyName = LangRu ? "Команды информации о клане" : "Clan Info Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] ClanInfoCommands = {"cinfo"};

			[JsonProperty(PropertyName =
				LangRu ? "Максимальное количество символов описания клана" : "Maximum clan description characters")]
			public int DescriptionMax = 256;

			[JsonProperty(PropertyName = LangRu ? "Тег клана в имени игрока" : "Clan tag in player name")]
			public bool TagInName = true;

			[JsonProperty(PropertyName = LangRu ? "Автоматическое создание команды" : "Automatic team creation")]
			public bool AutoTeamCreation = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Разрешить игрокам покидать свой клан с помощью кнопки выхода из внутриигровых команд"
					: "Allow players to leave their clan by using Rust's leave team button")]
			public bool ClanTeamLeave = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Разрешить игрокам исключать участников из своего клана с помощью кнопки исключения участника внутриигровых команд"
					: "Allow players to kick members from their clan using Rust's kick member button")]
			public bool ClanTeamKick = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Разрешить игрокам приглашать других игроков в свой клан через систему приглашений внутриигровых команд"
					: "Allow players to invite other players to their clan via Rust's team invite system")]
			public bool ClanTeamInvite = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Разрешить игрокам повышать других участников клана через кнопку повышения участника внутриигровых команд"
					: "Allow players to promote other clan members via Rust's team promote button")]
			public bool ClanTeamPromote = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Разрешить игрокам принять приглашение в клан с помощью кнопки принятия приглашения внутриигровых команд"
					: "Allow players to accept a clan invite using the Rust invite accept button")]
			public bool ClanTeamAcceptInvite = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Показывать интерфейс создания клана при создании команды?"
					: "Show clan creation interface when creating a team?")]
			public bool ClanCreateTeam = false;

			[JsonProperty(PropertyName =
				LangRu
					? "Принудительное создание клана при создании команды?"
					: "Force to create a clan when creating a team?")]
			public bool ForceClanCreateTeam = false;

			[JsonProperty(PropertyName = LangRu ? "Частота обновления топа" : "Top refresh rate")]
			public float TopRefreshRate = 60f;

			[JsonProperty(PropertyName =
				LangRu ? "Значение по умолчанию для нормативов ресурсов" : "Default value for the resource standarts")]
			public int DefaultValStandarts = 100000;

			[JsonProperty(PropertyName = LangRu ? "Настройки чата" : "Chat Settings")]
			public ChatSettings ChatSettings = new()
			{
				Enabled = true,
				TagFormat = "<color=#{color}>[{tag}]</color>",
				EnabledClanChat = true,
				ClanChatCommands = new[] {"c", "cchat"},
				EnabledAllianceChat = true,
				AllianceChatCommands = new[] {"a", "achat"},
				WorkingWithBetterChat = true,
				WorkingWithIQChat = true,
				WorkingWithInGameChat = false
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки разрешений" : "Permission Settings")]
			public PermissionSettings PermissionSettings = new()
			{
				UsePermClanCreating = false,
				ClanCreating = "clans.cancreate",
				UsePermClanJoining = false,
				ClanJoining = "clans.canjoin",
				UsePermClanLeave = false,
				ClanLeave = "clans.canleave",
				UsePermClanDisband = false,
				ClanDisband = "clans.candisband",
				UsePermClanKick = false,
				ClanKick = "clans.cankick",
				UsePermClanSkins = false,
				ClanSkins = "clans.canskins",
				ClanInfoAuthLevel = 0
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки альянсов" : "Alliance Settings")]
			public AllianceSettings AllianceSettings = new()
			{
				Enabled = true,
				UseFF = true,
				DefaultFF = false,
				GeneralFriendlyFire = false,
				ModersGeneralFF = false,
				PlayersGeneralFF = false,
				AllyAddPlayersTeams = false
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки очистки" : "Purge Settings")]
			public PurgeSettings PurgeSettings = new()
			{
				Enabled = true,
				OlderThanDays = 14,
				ListPurgedClans = true,
				WipeClansOnNewSave = false,
				WipePlayersOnNewSave = false,
				WipeInvitesOnNewSave = false
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки ограничений" : "Limit Settings")]
			public LimitSettings LimitSettings = new()
			{
				MemberLimit = 8,
				ModeratorLimit = 2,
				AlliancesLimit = 2
			};

			[JsonProperty(PropertyName = LangRu ? "Ресурсы" : "Resources",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Resources = new()
			{
				"stones", "sulfur.ore", "metal.ore", "hq.metal.ore", "wood"
			};

			[JsonProperty(
				PropertyName = LangRu
					? "Доступные элементы для норм добычи ресурсов"
					: "Available items for resource standarts",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> AvailableStandartItems = new()
			{
				"gears", "metalblade", "metalpipe", "propanetank", "roadsigns", "rope", "sewingkit", "sheetmetal",
				"metalspring", "tarp", "techparts", "riflebody", "semibody", "smgbody", "fat.animal", "cctv.camera",
				"charcoal", "cloth", "crude.oil", "diesel_barrel", "gunpowder", "hq.metal.ore", "leather",
				"lowgradefuel", "metal.fragments", "metal.ore", "scrap", "stones", "sulfur.ore", "sulfur",
				"targeting.computer", "wood"
			};

			[JsonProperty(PropertyName = LangRu ? "Страницы" : "Pages",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<PageSettings> Pages = new()
			{
				new PageSettings
				{
					ID = ABOUT_CLAN,
					Key = "aboutclan",
					Enabled = true,
					Permission = string.Empty
				},
				new PageSettings
				{
					ID = MEMBERS_LIST,
					Key = "memberslist",
					Enabled = true,
					Permission = string.Empty
				},
				new PageSettings
				{
					ID = CLANS_TOP,
					Key = "clanstop",
					Enabled = true,
					Permission = string.Empty
				},
				new PageSettings
				{
					ID = PLAYERS_TOP,
					Key = "playerstop",
					Enabled = true,
					Permission = string.Empty
				},
				new PageSettings
				{
					ID = GATHER_RATES,
					Key = "resources",
					Enabled = true,
					Permission = string.Empty
				},
				new PageSettings
				{
					ID = SKINS_PAGE,
					Key = "skins",
					Enabled = true,
					Permission = string.Empty
				},
				new PageSettings
				{
					ID = PLAYERS_LIST,
					Key = "playerslist",
					Enabled = true,
					Permission = string.Empty
				},
				new PageSettings
				{
					ID = ALIANCES_LIST,
					Key = "alianceslist",
					Enabled = true,
					Permission = string.Empty
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки интерфейса" : "Interface")]
			public InterfaceSettings UI = new()
			{
				Color1 = new IColor("#0E0E10",
					100),
				Color2 = new IColor("#4B68FF",
					100),
				Color3 = new IColor("#161617",
					100),
				Color4 = new IColor("#324192",
					100),
				Color5 = new IColor("#303030",
					100),
				Color6 = new IColor("#FF4B4B",
					100),
				Color7 = new IColor("#4B68FF",
					33),
				Color8 = new IColor("#0E0E10",
					99),
				ValueAbbreviation = true,
				ShowCloseOnClanCreation = true,
				TopClansColumns = new List<ColumnSettings>
				{
					new()
					{
						Width = 75,
						Key = "top",
						LangKey = TopTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "#{0}"
					},
					new()
					{
						Width = 165,
						Key = "name",
						LangKey = NameTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 70,
						Key = "leader",
						LangKey = LeaderTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 90,
						Key = "members",
						LangKey = MembersTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 80,
						Key = "score",
						LangKey = ScoreTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					}
				},
				TopPlayersColumns = new List<ColumnSettings>
				{
					new()
					{
						Width = 75,
						Key = "top",
						LangKey = TopTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "#{0}"
					},
					new()
					{
						Width = 185,
						Key = "name",
						LangKey = NameTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 70,
						Key = "kills",
						LangKey = KillsTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 70,
						Key = "resources",
						LangKey = ResourcesTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 80,
						Key = "score",
						LangKey = ScoreTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 12,
						FontSize = 12,
						TextFormat = "{0}"
					}
				},
				ProfileButtons = new List<BtnConf>
				{
					new()
					{
						Enabled = false,
						CloseMenu = true,
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = "270 -55",
						OffsetMax = "360 -30",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						TextColor = new IColor("#FFFFFF",
							100),
						Color = new IColor("#324192",
							100),
						Title = "TP",
						Command = "tpr {target}"
					},
					new()
					{
						Enabled = false,
						CloseMenu = true,
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = "370 -55",
						OffsetMax = "460 -30",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						TextColor = new IColor("#FFFFFF",
							100),
						Color = new IColor("#324192",
							100),
						Title = "TRADE",
						Command = "trade {target}"
					}
				},
				ClanMemberProfileFields = new List<ColumnSettings>
				{
					new()
					{
						Width = 140,
						Key = "gather",
						LangKey = GatherTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "{0}%"
					},
					new()
					{
						Width = 140,
						Key = "lastlogin",
						LangKey = LastLoginTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 10,
						TextFormat = "{0}"
					}
				},
				TopPlayerProfileFields = new List<ColumnSettings>
				{
					new()
					{
						Width = 300,
						Key = "clanname",
						LangKey = ClanNameTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 140,
						Key = "rating",
						LangKey = RatingTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 140,
						Key = "score",
						LangKey = ScoreTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 140,
						Key = "kills",
						LangKey = KillsTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 140,
						Key = "deaths",
						LangKey = DeathsTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "{0}"
					},
					new()
					{
						Width = 140,
						Key = "kd",
						LangKey = KDTitle,
						TextAlign = TextAnchor.MiddleCenter,
						TitleFontSize = 10,
						FontSize = 12,
						TextFormat = "{0}"
					}
				},
				ShowBtnChangeAvatar = true,
				ShowBtnLeave = true,
				PageAboutClan = new InterfaceSettings.AboutClanInfo
				{
					ShowClanName = true,
					ShowClanAvatar = true,
					ShowClanLeader = true,
					ShowClanGather = true,
					ShowClanRating = true,
					ShowClanMembers = true,
					ShowClanDescription = true
				},
				PagePlayersTop = new InterfaceSettings.PlayersTopPage()
				{
					PlayersOnPage = 7,
					Height = 37.5f,
					Width = 480,
					Margin = 2.5f,
					UpIndent = 50,
				},
				PageClansTop = new InterfaceSettings.ClansTopPage()
				{
					ClansOnPage = 7,
					Height = 37.5f,
					Width = 480,
					Margin = 2.5f,
					UpIndent = 50,
				},
				PageMembers = new InterfaceSettings.MembersPage
				{
					MembersOnLine = 2,
					MaxLines = 8,
					UpIndent = 0,
					Height = 35,
					Width = 237.5f,
					Margin = 5
				},
				PagePlayersList = new InterfaceSettings.PlayerListPage
				{
					MembersOnLine = 2,
					MaxLines = 8,
					UpIndent = 0,
					Height = 35,
					Width = 237.5f,
					Margin = 5,
					ShowAllPlayers = false
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки скинов" : "Skins Settings")]
			public SkinsSettings Skins = new()
			{
				ItemSkins = new Dictionary<string, List<ulong>>
				{
					["metal.facemask"] = new(),
					["hoodie"] = new(),
					["metal.plate.torso"] = new(),
					["pants"] = new(),
					["roadsign.kilt"] = new(),
					["shoes.boots"] = new(),
					["rifle.ak"] = new(),
					["rifle.bolt"] = new()
				},
				UseSkinner = false,
				UseSkinBox = false,
				UsePlayerSkins = false,
				UseLSkins = false,
				CanCustomSkin = true,
				Permission = string.Empty,
				DisableSkins = false,
				DefaultValueDisableSkins = true
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки статистики" : "Statistics Settings")]
			public StatisticsSettings Statistics = new()
			{
				Kills = true,
				Gather = true,
				Loot = true,
				Entities = true,
				Craft = true
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки цветов" : "Colos Settings")] //TODO: change to Colors
			public ColorsSettings Colors = new()
			{
				Member = "#fcf5cb",
				Moderator = "#74c6ff",
				Owner = "#a1ff46"
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки PlayerDatabase" : "PlayerDatabase")]
			public PlayerDatabaseConf PlayerDatabase = new(false, "Clans");

			[JsonProperty(PropertyName = LangRu ? "Настройки ZoneManager" : "ZoneManager Settings")]
			public ZoneManagerSettings ZMSettings = new()
			{
				Enabled = false,
				FFAllowlist = new List<string>
				{
					"92457",
					"4587478545"
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки клан-тегов" : "Clan Tag Settings")]
			public TagSettings Tags = new()
			{
				TagMin = 2,
				TagMax = 6,
				CaseSensitive = true,
				BlockedWords = new List<string>
				{
					"admin", "mod", "owner"
				},
				CheckingCharacters = true,
				AllowedCharacters = "!Â²Â³",
				TagColor = new TagSettings.TagColorSettings
				{
					Enabled = true,
					DefaultColor = "AAFF55",
					Owners = true,
					Moderators = false,
					Players = false
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки команд" : "Commands Settings")]
			public CommandsSettings Commands = new()
			{
				ClansFF = new[]
				{
					"cff"
				},
				AllyFF = new[]
				{
					"aff"
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки сохранения" : "Saving Settings")]
			public SavingSettings Saving = new()
			{
				SavePlayersOnServerSave = false,
				SaveClansOnServerSave = true
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки дружественного огня" : "Friendly Fire Settings")]
			public FriendlyFireSettings FriendlyFire = new()
			{
				UseFriendlyFire = true,
				UseTurretsFF = false,
				GeneralFriendlyFire = false,
				ModersGeneralFF = false,
				PlayersGeneralFF = false,
				FriendlyFire = false,
				IgnoreOnArenaTournament = false
			};

			[JsonProperty(PropertyName = LangRu ? "Платные функции" : "Paid Functionality Settings")]
			public PaidFunctionalitySettings PaidFunctionality = new()
			{
				Economy = new PaidFunctionalitySettings.EconomySettings
				{
					Type = PaidFunctionalitySettings.EconomySettings.EconomyType.Plugin,
					AddHook = "Deposit",
					BalanceHook = "Balance",
					RemoveHook = "Withdraw",
					Plug = "Economics",
					ShortName = "scrap",
					DisplayName = string.Empty,
					Skin = 0
				},
				ChargeFeeToCreateClan = false,
				CostCreatingClan = 100,
				ChargeFeeToJoinClan = false,
				CostJoiningClan = 100,
				ChargeFeeToKickClanMember = false,
				CostKickingClanMember = 100,
				ChargeFeeToLeaveClan = false,
				CostLeavingClan = 100,
				ChargeFeeToDisbandClan = false,
				CostDisbandingClan = 100,
				ChargeFeeToSetClanSkin = false,
				CostSettingClanSkin = 100,
				ChargeFeeToSetClanAvatar = false,
				CostSettingClanAvatar = 100,
				ChargeFeeForSendInviteToClan = false,
				CostForSendInviteToClan = 100
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки страниц" : "Pages Settings")]
			public PagesSettings PagesConfig = new()
			{
				Default = new PagesSettings.DefaultPages
				{
					Enabled = true,
					ClanPageNotFound = ABOUT_CLAN,
					ClanPageWhenExists = PLAYERS_TOP
				},
				StayOnPageWithSelectedSkin = true
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки добычи" : "Loot Settings")]
			public LootSettings Loot = new()
			{
				Enabled = true,
				Loots = new List<LootSettings.LootEntry>
				{
					LootSettings.LootEntry.Create("kills", LootSettings.LootType.Kill, 1f),
					LootSettings.LootEntry.Create("deaths", LootSettings.LootType.Kill, -1f),
					LootSettings.LootEntry.Create("stone-ore", LootSettings.LootType.Gather, 0.1f),
					LootSettings.LootEntry.Create("supply_drop", LootSettings.LootType.LootCrate, 3),
					LootSettings.LootEntry.Create("crate_normal", LootSettings.LootType.LootCrate, 0.3f),
					LootSettings.LootEntry.Create("crate_elite", LootSettings.LootType.LootCrate, 0.5f),
					LootSettings.LootEntry.Create("bradley_crate", LootSettings.LootType.LootCrate, 5f),
					LootSettings.LootEntry.Create("heli_crate", LootSettings.LootType.LootCrate, 5f),
					LootSettings.LootEntry.Create("bradley", LootSettings.LootType.Kill, 10f),
					LootSettings.LootEntry.Create("helicopter", LootSettings.LootType.Kill, 15f),
					LootSettings.LootEntry.Create("barrel", LootSettings.LootType.Kill, 0.1f),
					LootSettings.LootEntry.Create("scientistnpc", LootSettings.LootType.Kill, 0.5f),
					LootSettings.LootEntry.Create("heavyscientist", LootSettings.LootType.Kill, 2f),
					LootSettings.LootEntry.Create("sulfur.ore", LootSettings.LootType.Gather, 0.5f),
					LootSettings.LootEntry.Create("metal.ore", LootSettings.LootType.Gather, 0.5f),
					LootSettings.LootEntry.Create("hq.metal.ore", LootSettings.LootType.Gather, 0.5f),
					LootSettings.LootEntry.Create("stones", LootSettings.LootType.Gather, 0.5f),
					LootSettings.LootEntry.Create("cupboard.tool.deployed", LootSettings.LootType.Kill)
				}
			};

			public VersionNumber Version;

			#region Classes

			public class PagesSettings
			{
				[JsonProperty(PropertyName = LangRu ? "Страницы по умолчанию" : "Default Pages")]
				public DefaultPages Default;

				[JsonProperty(PropertyName =
					LangRu ? "Оставаться на странице выбранного скина" : "Stay on page with selected skin")]
				public bool StayOnPageWithSelectedSkin;

				public class DefaultPages
				{
					[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
					public bool Enabled;

					[JsonProperty(PropertyName =
						LangRu ? "Страница по умолчанию, когда клан не найден" : "Default page when a clan not found")]
					public int ClanPageNotFound;

					[JsonProperty(PropertyName =
						LangRu ? "Страница по умолчанию, когда клан существует" : "Default page when a clan exists")]
					public int ClanPageWhenExists;
				}
			}

			#endregion Classes
		}

		private class LootSettings
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Добыча" : "Loot",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<LootEntry> Loots = new();

			#endregion

			#region Cache

			[JsonIgnore] private Dictionary<LootType, Dictionary<string, float>> _cachedLoot = new();

			[JsonIgnore] private Dictionary<string, float> _cachedScore = new();

			public void Init()
			{
				_cachedLoot.Clear();

				Loots.ForEach(ent =>
				{
					if (!ent.Enabled || ent.Type == LootType.None) return;

					if (!_cachedLoot.ContainsKey(ent.Type))
						_cachedLoot.TryAdd(ent.Type, new Dictionary<string, float>());

					if (!_cachedLoot[ent.Type].TryAdd(ent.ShortName, ent.Score))
						_instance?.PrintError(
							$"Can't cache loot type {ent.Type}: shortname '{ent.ShortName}' already exists!!!");

					if (_cachedScore.ContainsKey(ent.ShortName))
						_cachedScore[ent.ShortName] += ent.Score;
					else
						_cachedScore.TryAdd(ent.ShortName, ent.Score);
				});
			}

			#region By Loot Type

			public bool TryGetLoot(LootType type, string shortName, out float score)
			{
				if (_cachedLoot.TryGetValue(type, out var dict))
					if (dict.TryGetValue(shortName, out score))
						return true;

				score = 0;
				return false;
			}

			public bool HasLootByType(LootType type, string shortName)
			{
				return _cachedLoot.TryGetValue(type, out var dict) && dict.ContainsKey(shortName);
			}

			public List<string> GetLootByType(LootType type)
			{
				return !_cachedLoot.TryGetValue(type, out var dict) ? new List<string>() : new List<string>(dict.Keys);
			}

			#endregion

			#region By ShortName

			public bool TryGetLoot(string shortName, out float score)
			{
				return _cachedScore.TryGetValue(shortName, out score);
			}

			public bool HasLootByShortName(string shortName)
			{
				return _cachedScore.ContainsKey(shortName);
			}

			#endregion

			#endregion

			#region Classes

			public class LootEntry
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = LangRu ? "Краткое имя предмета" : "Short Name")]
				public string ShortName;

				[JsonProperty(PropertyName = LangRu ? "Тип добычи" : "Loot Type")]
				[JsonConverter(typeof(StringEnumConverter))]
				public LootType Type;

				[JsonProperty(PropertyName = LangRu ? "Очки" : "Score")]
				public float Score;

				public static LootEntry Create(string shortName, LootType type, float score = 1f)
				{
					return new LootEntry
					{
						Enabled = true,
						ShortName = shortName,
						Type = type,
						Score = score
					};
				}
			}

			public enum LootType
			{
				None,
				Gather,
				LootCrate,
				Look,
				Kill,
				Craft,
				HackCrate
			}

			#endregion
		}

		private class PaidFunctionalitySettings
		{
			[JsonProperty(PropertyName = LangRu ? "Экономика" : "Economy")]
			public EconomySettings Economy;

			[JsonProperty(PropertyName =
				LangRu ? "Взимать плату за создание клана?" : "Charge a fee to create a clan?")]
			public bool ChargeFeeToCreateClan;

			[JsonProperty(PropertyName = LangRu ? "Стоимость создания клана" : "Cost of creating a clan")]
			public int CostCreatingClan;

			[JsonProperty(PropertyName =
				LangRu ? "Взимать плату за вступление в клан?" : "Charge a fee to join a clan?")]
			public bool ChargeFeeToJoinClan;

			[JsonProperty(PropertyName = LangRu ? "Стоимость вступления в клан" : "Cost of joining a clan")]
			public int CostJoiningClan;

			[JsonProperty(PropertyName =
				LangRu ? "Взимать плату за исключение члена клана?" : "Charge a fee to kick a clan member?")]
			public bool ChargeFeeToKickClanMember;

			[JsonProperty(PropertyName = LangRu ? "Стоимость исключения члена клана" : "Cost of kicking a clan member")]
			public int CostKickingClanMember;

			[JsonProperty(PropertyName = LangRu ? "Взимать плату за выход из клана?" : "Charge a fee to leave a clan?")]
			public bool ChargeFeeToLeaveClan;

			[JsonProperty(PropertyName = LangRu ? "Стоимость выхода из клана" : "Cost of leaving a clan")]
			public int CostLeavingClan;

			[JsonProperty(PropertyName =
				LangRu ? "Взимать плату за распущение клана?" : "Charge a fee to disband a clan?")]
			public bool ChargeFeeToDisbandClan;

			[JsonProperty(PropertyName = LangRu ? "Стоимость распуска клана" : "Cost of disbanding a clan")]
			public int CostDisbandingClan;

			[JsonProperty(PropertyName =
				LangRu ? "Взимать плату за установку скина клана?" : "Charge a fee to set a clan skin?")]
			public bool ChargeFeeToSetClanSkin;

			[JsonProperty(PropertyName = LangRu ? "Стоимость установки скина клана" : "Cost of setting a clan skin")]
			public int CostSettingClanSkin;

			[JsonProperty(PropertyName =
				LangRu ? "Взимать плату за установку аватара клана?" : "Charge a fee to set a clan avatar?")]
			public bool ChargeFeeToSetClanAvatar;

			[JsonProperty(PropertyName =
				LangRu ? "Стоимость установки аватара клана" : "Cost of setting a clan avatar")]
			public int CostSettingClanAvatar;

			[JsonProperty(PropertyName =
				LangRu
					? "Взимать плату за отправку приглашения в клан?"
					: "Charge a fee for sending an invitation to a clan?")]
			public bool ChargeFeeForSendInviteToClan;

			[JsonProperty(PropertyName =
				LangRu ? "Стоимость отправки приглашения в клан" : "Cost of sending an invitation to a clan")]
			public int CostForSendInviteToClan;

			public class EconomySettings
			{
				#region Fields

				[JsonProperty(PropertyName = LangRu ? "Тип (Plugin/Item)" : "Type (Plugin/Item)")]
				[JsonConverter(typeof(StringEnumConverter))]
				public EconomyType Type;

				[JsonProperty(PropertyName = LangRu ? "Название плагина" : "Plugin name")]
				public string Plug;

				[JsonProperty(PropertyName = LangRu ? "Функция пополнения баланса" : "Balance add hook")]
				public string AddHook;

				[JsonProperty(PropertyName = LangRu ? "Функция снятия баланса" : "Balance remove hook")]
				public string RemoveHook;

				[JsonProperty(PropertyName = LangRu ? "Функция показа баланса" : "Balance show hook")]
				public string BalanceHook;

				[JsonProperty(PropertyName = LangRu ? "Краткое имя предмета" : "ShortName")]
				public string ShortName;

				[JsonProperty(PropertyName =
					LangRu ? "Отображаемое имя (пусто - по умолчанию)" : "Display Name (empty - default)")]
				public string DisplayName;

				[JsonProperty(PropertyName = LangRu ? "Скин" : "Skin")]
				public ulong Skin;

				#endregion

				#region Public Methods

				public enum EconomyType
				{
					Plugin,
					Item
				}

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

				#region Utils

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

				private void Take(List<Item> itemList, string shortname, ulong skinId, int iAmount)
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
							//num1 += num2;
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

				#endregion
			}
		}

		private class FriendlyFireSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Использовать дружественный огонь?" : "Use Friendly Fire?")]
			public bool UseFriendlyFire;

			[JsonProperty(PropertyName =
				LangRu ? "Использовать дружественный огонь для турелей?" : "Use Friendly Fire for Turrets?")]
			public bool UseTurretsFF;

			[JsonProperty(PropertyName =
				LangRu
					? "Общий дружественный огонь (только лидер клана может включить/выключить его)"
					: "General friendly fire (only the leader of the clan can enable/disable it)")]
			public bool GeneralFriendlyFire;

			[JsonProperty(PropertyName =
				LangRu
					? "Могут модераторы переключить общий дружественный огонь?"
					: "Can moderators toggle general friendly fire?")]
			public bool ModersGeneralFF;

			[JsonProperty(PropertyName =
				LangRu
					? "Могут игроки переключить общий дружественный огонь?"
					: "Can players toggle general friendly fire?")]
			public bool PlayersGeneralFF;

			[JsonProperty(PropertyName =
				LangRu ? "Значение по умолчанию для дружественного огня" : "Friendly Fire Default Value")]
			public bool FriendlyFire;

			[JsonProperty(PropertyName =
				LangRu ? "Игнорировать при использовании ArenaTournament?" : "Ignore when using ArenaTournament?")]
			public bool IgnoreOnArenaTournament;
		}

		private class SavingSettings
		{
			[JsonProperty(PropertyName =
				LangRu
					? "Включить сохранение данных игроков во время сохранения сервера?"
					: "Enable saving player data during server saves?")]
			public bool SavePlayersOnServerSave;

			[JsonProperty(PropertyName =
				LangRu
					? "Включить сохранение данных кланов во время сохранения сервера?"
					: "Enable saving clan data during server save?")]
			public bool SaveClansOnServerSave;
		}

		private class AvatarSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Аватар по умолчанию" : "Default Avatar")]
			public string DefaultAvatar;

			[JsonProperty(PropertyName =
				LangRu ? "Может ли владелец клана изменить аватар?" : "Can the clan owner change the avatar?")]
			public bool CanOwner;

			[JsonProperty(PropertyName =
				LangRu ? "Может ли модератор клана изменить аватар?" : "Can the clan moderator change the avatar?")]
			public bool CanModerator;

			[JsonProperty(PropertyName =
				LangRu ? "Может ли участник клана изменить аватар?" : "Can the clan member change the avatar?")]
			public bool CanMember;

			[JsonProperty(PropertyName = LangRu ? "Разрешение на изменение аватара клана" : "Permission to change clan avatar")]
			public string PermissionToChange;
			
			[JsonProperty(PropertyName = LangRu ? "Ключ API Steam (получите его здесь: https://steamcommunity.com/dev/apikey)" : "Steam API key (get one here: https://steamcommunity.com/dev/apikey)")]
			public string SteamAPIKey;
		}

		private class CommandsSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Команды для кланового FF" : "Clans FF",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] ClansFF;

			[JsonProperty(PropertyName = LangRu ? "Команды для альянсового FF" : "Aliance FF",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] AllyFF;
		}

		private class TagSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Запрещенные слова" : "Blocked Words",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> BlockedWords;

			[JsonProperty(PropertyName =
				LangRu ? "Минимальное количество символов в теге клана" : "Minimum clan tag characters")]
			public int TagMin;

			[JsonProperty(PropertyName =
				LangRu ? "Максимальное количество символов в теге клана" : "Maximum clan tag characters")]
			public int TagMax;
			
				            [JsonProperty(PropertyName =
				                LangRu ? "Включить учет регистра для тегов клана" : "Enable case sensitivity for clan tags")]
			            public bool CaseSensitive;
			            
			[JsonProperty(PropertyName =
				LangRu ? "Включить проверку символов в тегах?" : "Enable character checking in tags?")]
			public bool CheckingCharacters;

			[JsonProperty(PropertyName =
				LangRu ? "Разрешенные специальные символы в тегах" : "Special characters allowed in tags")]
			public string AllowedCharacters;

			[JsonProperty(PropertyName = LangRu ? "Настройки цвета тега" : "Tag Color Settings")]
			public TagColorSettings TagColor;

			public class TagColorSettings
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = LangRu ? "Цвет по умолчанию" : "Default Color")]
				public string DefaultColor;

				[JsonProperty(PropertyName =
					LangRu ? "Могут ли владельцы изменять цвет?" : "Can the owner change the color?")]
				public bool Owners;

				[JsonProperty(PropertyName =
					LangRu ? "Могут ли модераторы изменять цвет?" : "Can the moderators change the color?")]
				public bool Moderators;

				[JsonProperty(PropertyName =
					LangRu ? "Могут ли игроки изменять цвет?" : "Can the players change the color?")]
				public bool Players;
			}
		}

		private class ZoneManagerSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(
				PropertyName = LangRu ? "Зоны с разрешенным дружественным огнем" : "Zones with allowed Friendly Fire",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> FFAllowlist = new();
		}

		private class ColorsSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Цвет владельца клана (hex)" : "Clan owner color (hex)")]
			public string Owner;

			[JsonProperty(PropertyName = LangRu ? "Цвет модератора клана (hex)" : "Clan moderator color (hex)")]
			public string Moderator;

			[JsonProperty(PropertyName = LangRu ? "Цвет участника клана (hex)" : "Clan member color (hex)")]
			public string Member;
		}

		private class PlayerDatabaseConf
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Таблица" : "Table")]
			public string Field;

			public PlayerDatabaseConf(bool enabled, string field)
			{
				Enabled = enabled;
				Field = field;
			}
		}

		private class StatisticsSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Убийства" : "Kills")]
			public bool Kills;

			[JsonProperty(PropertyName = LangRu ? "Сбор ресурсов" : "Gather")]
			public bool Gather;

			[JsonProperty(PropertyName = LangRu ? "Лутание" : "Loot")]
			public bool Loot;

			[JsonProperty(PropertyName = LangRu ? "Объекты" : "Entities")]
			public bool Entities;

			[JsonProperty(PropertyName = LangRu ? "Крафт" : "Craft")]
			public bool Craft;
		}

		private class SkinsSettings
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Скины предметов" : "Item Skins",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, List<ulong>> ItemSkins = new();

			[JsonProperty(PropertyName = LangRu ? "Использовать скины из Skinner?" : "Use skins from Skinner?")]
			public bool UseSkinner;

			[JsonProperty(PropertyName = LangRu ? "Использовать скины из SkinBox?" : "Use skins from SkinBox?")]
			public bool UseSkinBox;

			[JsonProperty(PropertyName = LangRu ? "Использовать скины из PlayerSkins?" : "Use skins from PlayerSkins?")]
			public bool UsePlayerSkins;

			[JsonProperty(PropertyName = LangRu ? "Использовать скины из LSkins?" : "Use skins from LSkins?")]
			public bool UseLSkins;

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли игроки устанавливать пользовательские скины?" : "Can players install custom skins?")]
			public bool CanCustomSkin;

			[JsonProperty(PropertyName =
				LangRu ? "Разрешение на установку пользовательского скина" : "Permission to install custom skin")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Отключить скины для клана?" : "Option to disable clan skins?")]
			public bool DisableSkins;

			[JsonProperty(PropertyName =
				LangRu ? "Значение по умолчанию для отключения скинов" : "Default value to disable skins")]
			public bool DefaultValueDisableSkins;

			#endregion Fields
		}

		private class InterfaceSettings
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Цвет один" : "Color One")]
			public IColor Color1;

			[JsonProperty(PropertyName = LangRu ? "Цвет два" : "Color Two")]
			public IColor Color2;

			[JsonProperty(PropertyName = LangRu ? "Цвет три" : "Color Three")]
			public IColor Color3;

			[JsonProperty(PropertyName = LangRu ? "Цвет четыре" : "Color Four")]
			public IColor Color4;

			[JsonProperty(PropertyName = LangRu ? "Цвет пять" : "Color Five")]
			public IColor Color5;

			[JsonProperty(PropertyName = LangRu ? "Цвет шесть" : "Color Six")]
			public IColor Color6;

			[JsonProperty(PropertyName = LangRu ? "Цвет семь" : "Color Seven")]
			public IColor Color7;

			[JsonProperty(PropertyName = LangRu ? "Цвет восемь" : "Color Eight")]
			public IColor Color8;

			[JsonProperty(PropertyName = LangRu ? "Использовать сокращение значений?" : "Use value abbreviation?")]
			public bool ValueAbbreviation;

			[JsonProperty(PropertyName =
				LangRu
					? "Показывать кнопку закрытия на экране создания клана?"
					: "Show the close button on the clan creation screen?")]
			public bool ShowCloseOnClanCreation = true;

			[JsonProperty(PropertyName = LangRu ? "Колонки лучших кланов" : "Top Clans Columns",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ColumnSettings> TopClansColumns;

			[JsonProperty(PropertyName = LangRu ? "Колонки лучших игроков" : "Top Players Columns",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ColumnSettings> TopPlayersColumns;

			[JsonProperty(PropertyName = LangRu ? "Кнопки профиля" : "Profile Buttons",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<BtnConf> ProfileButtons;

			[JsonProperty(PropertyName = LangRu ? "Поля профиля участника клана" : "Clan Member Profile Fields",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ColumnSettings> ClanMemberProfileFields;

			[JsonProperty(PropertyName = LangRu ? "Поля профиля лучшего игрока" : "Top Player Profile Fields",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ColumnSettings> TopPlayerProfileFields;

			[JsonProperty(PropertyName =
				LangRu ? "Показывать кнопку 'Изменить аватар'?" : "Show the 'Change avatar' button?")]
			public bool ShowBtnChangeAvatar;

			[JsonProperty(PropertyName = LangRu ? "Показывать кнопку 'Покинуть'?" : "Show the 'Leave' button?")]
			public bool ShowBtnLeave;

			[JsonProperty(PropertyName = LangRu ? "Страница клана" : "About Clan Page")]
			public AboutClanInfo PageAboutClan = new();

			[JsonProperty(PropertyName = LangRu ? "Страница Топ игроков" : "Top Players Page")]
			public PlayersTopPage PagePlayersTop = new();

			[JsonProperty(PropertyName = LangRu ? "Страница Топ кланов" : "Top Clans Page")]
			public ClansTopPage PageClansTop = new();

			[JsonProperty(PropertyName = LangRu ? "Страница Участники" : "Clans Members Page")]
			public MembersPage PageMembers = new();

			[JsonProperty(PropertyName = LangRu ? "Страница Список игроков" : "Players List Page")]
			public PlayerListPage PagePlayersList = new();

			#endregion

			#region Classes

			public class PlayerListPage
			{
				[JsonProperty(PropertyName = LangRu ? "Количество игроков на линии" : "Members on line")]
				public int MembersOnLine;

				[JsonProperty(PropertyName = LangRu ? "Макс количество линий" : "Max Lines")]
				public int MaxLines;

				[JsonProperty(PropertyName = LangRu ? "Отступ сверху" : "Up Indent")]
				public float UpIndent;
				
				[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
				public float Height;
				
				[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
				public float Width;
				
				[JsonProperty(PropertyName = LangRu ? "Отступ" : "Margin")]
				public float Margin;
				
				[JsonProperty(PropertyName = LangRu ? "Показывать всех игроков? (онлайн и оффлайн)" : "Show all players? (online and ofline)")]
				public bool ShowAllPlayers;
			}

			public class ClansTopPage
			{
				[JsonProperty(PropertyName = LangRu ? "Количество кланов на странице" : "Clans on page")]
				public int ClansOnPage;
				
				[JsonProperty(PropertyName = LangRu ? "Отступ сверху" : "Up Indent")]
				public float UpIndent;
				
				[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
				public float Height;
				
				[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
				public float Width;
				
				[JsonProperty(PropertyName = LangRu ? "Отступ" : "Margin")]
				public float Margin;
			}

			public class PlayersTopPage
			{
				[JsonProperty(PropertyName = LangRu ? "Количество игроков на странице" : "Players on page")]
				public int PlayersOnPage;
				
				[JsonProperty(PropertyName = LangRu ? "Отступ сверху" : "Up Indent")]
				public float UpIndent;
				
				[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
				public float Height;
				
				[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
				public float Width;
				
				[JsonProperty(PropertyName = LangRu ? "Отступ" : "Margin")]
				public float Margin;
			}

			public class MembersPage
			{
				[JsonProperty(PropertyName = LangRu ? "Количество игроков на линии" : "Members on line")]
				public int MembersOnLine;
				
				[JsonProperty(PropertyName = LangRu ? "Макс количество линий" : "Max Lines")]
				public int MaxLines;
				
				[JsonProperty(PropertyName = LangRu ? "Отступ сверху" : "Up Indent")]
				public float UpIndent;
				
				[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
				public float Height;
				
				[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
				public float Width;
				
				[JsonProperty(PropertyName = LangRu ? "Отступ" : "Margin")]
				public float Margin;
			}

			public class AboutClanInfo
			{
				[JsonProperty(PropertyName = LangRu ? "Показывать название клана?" : "Show Clan Name?")]
				public bool ShowClanName;

				[JsonProperty(PropertyName = LangRu ? "Показывать аватар клана?" : "Show Clan Avatar?")]
				public bool ShowClanAvatar;

				[JsonProperty(PropertyName = LangRu ? "Показывать лидера клана?" : "Show Clan Leader?")]
				public bool ShowClanLeader;

				[JsonProperty(PropertyName = LangRu ? "Показывать сборы клана?" : "Show Clan Gather?")]
				public bool ShowClanGather;

				[JsonProperty(PropertyName = LangRu ? "Показывать рейтинг клана?" : "Show Clan Rating?")]
				public bool ShowClanRating;

				[JsonProperty(PropertyName = LangRu ? "Показывать участников клана?" : "Show Clan Members?")]
				public bool ShowClanMembers;

				[JsonProperty(PropertyName = LangRu ? "Показывать описание клана?" : "Show Clan Description?")]
				public bool ShowClanDescription;
			}

			#endregion
		}

		private abstract class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;
		}

		private class BtnConf : InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Закрыть меню?" : "Close Menu?")]
			public bool CloseMenu;

			[JsonProperty(PropertyName = LangRu ? "Команда" : "Command")]
			public string Command;

			[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
			public IColor Color;

			[JsonProperty(PropertyName = LangRu ? "Заголовок" : "Title")]
			public string Title;

			[JsonProperty(PropertyName = LangRu ? "Размер шрифта" : "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = LangRu ? "Шрифт" : "Font")]
			public string Font;

			[JsonProperty(PropertyName = LangRu ? "Выравнивание" : "Align")]
			[JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = LangRu ? "Цвет текста" : "Text Color")]
			public IColor TextColor;

			#endregion

			#region Helpers

			private string GetCommand(ulong target)
			{
				if (string.IsNullOrEmpty(Command))
					return string.Empty;

				return Command
					.Replace("{target}", target.ToString())
					.Replace("{targetName}",
						$"\"{GetPlayerName(BasePlayer.FindAwakeOrSleeping(target.ToString()))}\"");
			}

			public void Get(ref CuiElementContainer container, ulong target, string parent, string close)
			{
				if (!Enabled) return;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = AnchorMin ?? "0 0", AnchorMax = AnchorMax ?? "1 1",
						OffsetMin = OffsetMin ?? "0 0", OffsetMax = OffsetMax ?? "0 0"
					},
					Text =
					{
						Text = $"{Title}",
						Align = Align,
						Font = Font,
						FontSize = FontSize,
						Color = TextColor?.Get() ?? "1 1 1 1"
					},
					Button =
					{
						Command = $"clans.sendcmd {GetCommand(target)}",
						Color = Color?.Get() ?? "1 1 1 1",
						Close = CloseMenu ? close : string.Empty
					}
				}, parent);
			}

			#endregion
		}

		private class ColumnSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
			public float Width;

			[JsonProperty(PropertyName = LangRu ? "Код языка (en/ru/de и тд)" : "Lang Key")]
			public string LangKey;

			[JsonProperty(PropertyName = LangRu ? "Ключ" : "Key")]
			public string Key;

			[JsonProperty(PropertyName = LangRu ? "Формат текста" : "Text Format")]
			public string TextFormat;

			[JsonProperty(PropertyName = LangRu ? "Выравнивание текста" : "Text Align")]
			[JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor TextAlign;

			[JsonProperty(PropertyName = LangRu ? "Размер шрифта Заголовка" : "Title Font Size")]
			public int TitleFontSize;

			[JsonProperty(PropertyName = LangRu ? "Размер шрифта" : "Font Size")]
			public int FontSize;

			public string GetFormat(int top, string values)
			{
				switch (Key)
				{
					case "top":
						return string.Format(TextFormat, top);

					default:
						return string.Format(TextFormat, values);
				}
			}
		}

		private class IColor
		{
			[JsonProperty(PropertyName = "HEX")] public string Hex;

			[JsonProperty(PropertyName = LangRu ? "Непрозрачность (0 - 100)" : "Opacity (0 - 100)")]
			public float Alpha;

			public string Get()
			{
				if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

				var str = Hex.Trim('#');
				if (str.Length != 6) throw new Exception(Hex);
				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
			}

			public IColor(string hex, float alpha)
			{
				Hex = hex;
				Alpha = alpha;
			}
		}

		private class PageSettings
		{
			[JsonProperty(PropertyName = LangRu ? "ID (НЕ ИЗМЕНЯТЬ)" : "ID (DON'T CHANGE)")]
			public int ID;

			[JsonProperty(PropertyName = LangRu ? "Ключ (НЕ ИЗМЕНЯТЬ)" : "Key (DON'T CHANGE)")]
			public string Key;

			[JsonProperty(PropertyName = LangRu ? "Включено?" : "Enabled?")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
			public string Permission;
		}

		private class LimitSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Лимит участников" : "Member Limit")]
			public int MemberLimit;

			[JsonProperty(PropertyName = LangRu ? "Лимит модераторов" : "Moderator Limit")]
			public int ModeratorLimit;

			[JsonProperty(PropertyName = LangRu ? "Лимит альянсов" : "Alliances Limit")]
			public int AlliancesLimit;
		}

		private class PurgeSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Включить очистку кланов" : "Enable clan purging")]
			public bool Enabled;

			[JsonProperty(PropertyName =
				LangRu
					? "Очищать кланы, которые не были онлайн в течение x дней"
					: "Purge clans that havent been online for x amount of days")]
			public int OlderThanDays;

			[JsonProperty(PropertyName =
				LangRu
					? "Выводить список очищенных кланов в консоль при очистке"
					: "List purged clans in console when purging")]
			public bool ListPurgedClans;

			[JsonProperty(PropertyName =
				LangRu ? "Очищать кланы при новом сохранении карты" : "Wipe clans on new map save")]
			public bool WipeClansOnNewSave;

			[JsonProperty(PropertyName =
				LangRu ? "Очищать игроков при новом сохранении карты" : "Wipe players on new map save")]
			public bool WipePlayersOnNewSave;

			[JsonProperty(PropertyName =
				LangRu ? "Очищать приглашения при новом сохранении карты" : "Wipe invites on new map save")]
			public bool WipeInvitesOnNewSave;
		}

		private class ChatSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Включить теги кланов в чате?" : "Enable clan tags in chat?")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Формат тега" : "Tag format")]
			public string TagFormat;

			[JsonProperty(PropertyName = LangRu ? "Включить чат клана?" : "Enable clan chat?")]
			public bool EnabledClanChat;

			[JsonProperty(PropertyName = LangRu ? "Команды чата клана" : "Clan chat commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] ClanChatCommands;

			[JsonProperty(PropertyName = LangRu ? "Включить чат альянса?" : "Enable alliance chat?")]
			public bool EnabledAllianceChat;

			[JsonProperty(PropertyName = LangRu ? "Команды чата альянса" : "Alliance chat commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] AllianceChatCommands;

			[JsonProperty(PropertyName = LangRu ? "Работа с BetterChat?" : "Working with BetterChat?")]
			public bool WorkingWithBetterChat;

			[JsonProperty(PropertyName = LangRu ? "Работа с IQChat?" : "Working with IQChat?")]
			public bool WorkingWithIQChat;

			[JsonProperty(PropertyName = LangRu ? "Работа с чатом в игре?" : "Working with in-game chat?")]
			public bool WorkingWithInGameChat;
		}

		private class PermissionSettings
		{
			[JsonProperty(PropertyName =
				LangRu ? "Использовать разрешение на создание клана" : "Use permission to create a clan")]
			public bool UsePermClanCreating;

			[JsonProperty(PropertyName = LangRu ? "Разрешение на создание клана" : "Permission to create a clan")]
			public string ClanCreating;

			[JsonProperty(PropertyName =
				LangRu ? "Использовать разрешение на вступление в клан" : "Use permission to join a clan")]
			public bool UsePermClanJoining;

			[JsonProperty(PropertyName = LangRu ? "Разрешение на вступление в клан" : "Permission to join a clan")]
			public string ClanJoining;

			[JsonProperty(PropertyName =
				LangRu
					? "Использовать разрешение на исключение участника из клана"
					: "Use permission to kick a clan member")]
			public bool UsePermClanKick;

			[JsonProperty(PropertyName =
				LangRu ? "Разрешение на исключение участника из клана" : "Clan kick permission")]
			public string ClanKick;

			[JsonProperty(PropertyName =
				LangRu ? "Использовать разрешение на выход из клана" : "Use permission to leave a clan")]
			public bool UsePermClanLeave;

			[JsonProperty(PropertyName = LangRu ? "Разрешение на выход из клана" : "Clan leave permission")]
			public string ClanLeave;

			[JsonProperty(PropertyName =
				LangRu ? "Использовать разрешение на распустить клан" : "Use permission to disband a clan")]
			public bool UsePermClanDisband;

			[JsonProperty(PropertyName = LangRu ? "Разрешение на распустить клан" : "Clan disband permission")]
			public string ClanDisband;

			[JsonProperty(PropertyName =
				LangRu ? "Использовать разрешение на смену скинов клана" : "Use permission to clan skins")]
			public bool UsePermClanSkins;

			[JsonProperty(PropertyName = LangRu ? "Разрешение на смену скинов клана" : "Use clan skins permission")]
			public string ClanSkins;

			[JsonProperty(PropertyName =
				LangRu
					? "Минимальный уровень аутентификации для просмотра информации о клане (0 = игрок, 1 = модератор, 2 = владелец)"
					: "Minimum auth level required to view clan info (0 = player, 1 = moderator, 2 = owner)")]
			public int ClanInfoAuthLevel;
		}

		private class AllianceSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Включить альянсы кланов" : "Enable clan alliances")]
			public bool Enabled;

			[JsonProperty(PropertyName =
				LangRu ? "Включить дружественный огонь (союзные кланы)" : "Enable friendly fire (allied clans)")]
			public bool UseFF;

			[JsonProperty(PropertyName =
				LangRu ? "Значение дружественного огня по умолчанию" : "Default friendly fire value")]
			public bool DefaultFF;

			[JsonProperty(PropertyName =
				LangRu
					? "Общий дружественный огонь (только лидер клана может включить/отключить его)"
					: "General friendly fire (only the leader of the clan can enable/disable it)")]
			public bool GeneralFriendlyFire;

			[JsonProperty(PropertyName =
				LangRu
					? "Могут модераторы включать общий дружественный огонь?"
					: "Can moderators toggle general friendly fire?")]
			public bool ModersGeneralFF;

			[JsonProperty(PropertyName =
				LangRu
					? "Могут игроки включать общий дружественный огонь?"
					: "Can players toggle general friendly fire?")]
			public bool PlayersGeneralFF;

			[JsonProperty(PropertyName =
				LangRu
					? "Добавить игроков из кланов-союзников в игровые команды?"
					: "Add players from the clan alliance to in-game teams?")]
			public bool AllyAddPlayersTeams;
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
				PrintError($"Your configuration file contains an error. Using default configuration values.\n{ex}");

				LoadDefaultConfig();
			}
		}

		private void UpdateConfigValues()
		{
			PrintWarning("Config update detected! Updating config values...");

			var baseConfig = new Configuration();

			if (_config.Version != default)
			{
				if (_config.Version < new VersionNumber(1, 0, 15))
				{
					_config.Skins.DisableSkins = baseConfig.Skins.DisableSkins;
					_config.Skins.DefaultValueDisableSkins = baseConfig.Skins.DefaultValueDisableSkins;

					_config.PermissionSettings.UsePermClanSkins = baseConfig.PermissionSettings.UsePermClanSkins;
					_config.PermissionSettings.ClanSkins = baseConfig.PermissionSettings.ClanSkins;
				}

				if (_config.Version < new VersionNumber(1, 1, 0)) StartConvertOldData();

				if (_config.Version < new VersionNumber(1, 1, 27))
				{
					var avatar = Config["Default Avatar"]?.ToString();
					if (!string.IsNullOrEmpty(avatar))
					{
						if (avatar.Equals("https://i.imgur.com/nn7Lcm2.png"))
							avatar = "https://i.ibb.co/q97QG6c/image.png";

						_config.Avatar = new AvatarSettings
						{
							DefaultAvatar = avatar,
							CanOwner = true,
							CanModerator = false,
							CanMember = false,
							PermissionToChange = string.Empty
						};
					}

					UpdateAvatarsAfterUpdate(avatar);

					var color1 = Config["Interface", "Color 1"].ToString();
					var color2 = Config["Interface", "Color 2"].ToString();
					var color3 = Config["Interface", "Color 3"].ToString();
					var color4 = Config["Interface", "Color 4"].ToString();
					var color5 = Config["Interface", "Color 5"].ToString();
					var color6 = Config["Interface", "Color 6"].ToString();

					_config.UI.Color1 = new IColor(color1, 100);
					_config.UI.Color2 = new IColor(color2, 100);
					_config.UI.Color3 = new IColor(color3, 100);
					_config.UI.Color4 = new IColor(color4, 100);
					_config.UI.Color5 = new IColor(color5, 100);
					_config.UI.Color6 = new IColor(color6, 100);
					_config.UI.Color7 = new IColor(color2, 33);
					_config.UI.Color8 = new IColor(color1, 99);

					_config.FriendlyFire = new FriendlyFireSettings
					{
						UseFriendlyFire = Convert.ToBoolean(Config["Use Friendly Fire?"]),
						UseTurretsFF = Convert.ToBoolean(Config["Use Friendly Fire for Turrets?"]),
						GeneralFriendlyFire =
							Convert.ToBoolean(
								Config["General friendly fire (only the leader of the clan can enable/disable it)"]),
						ModersGeneralFF = Convert.ToBoolean(Config["Can moderators toggle general friendly fire?"]),
						PlayersGeneralFF = Convert.ToBoolean(Config["Can players toggle general friendly fire?"]),
						FriendlyFire = Convert.ToBoolean(Config["Friendly Fire Default Value"])
					};
				}

				if (_config.Version >= new VersionNumber(1, 1, 27) && _config.Version < new VersionNumber(1, 1, 31))
				{
					var changeAvatar = Config.Get("Interface", "Show the \"Change avatar\" button?");
					if (changeAvatar != null)
						_config.UI.ShowBtnChangeAvatar = Convert.ToBoolean(changeAvatar);

					var leaveBtn = Config.Get("Interface", "Show the \"Leave\" button?");
					if (leaveBtn != null)
						_config.UI.ShowBtnLeave = Convert.ToBoolean(leaveBtn);
				}

				if (_config.Version < new VersionNumber(1, 1, 31))
					if (Config.Get("Score Table (shortname - score)") is Dictionary<string, float> scoreTable)
						foreach (var item in scoreTable)
						{
							var lootType = ConvertShortnameToLootType(item.Key);

							_config.Loot.Loots.Add(LootSettings.LootEntry.Create(item.Key, lootType, item.Value));
						}

				if (_config.Version < new VersionNumber(1, 1, 33))
				{
					_config.ChatSettings.WorkingWithBetterChat = Convert.ToBoolean(Config.Get("Chat Settings", "Working with BatterChat?"));
					
					_config.Tags.TagColor.DefaultColor = Convert.ToString(Config.Get("Clan Tag Settings", "Tag Color Settings", "DefaultColor")) ?? "#FFFFFF";
					
					_config.PagesConfig.StayOnPageWithSelectedSkin = Convert.ToBoolean(Config.Get("Pages Settings", "Stay on page of item for which skin is selected"));
				}
			}

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		private static LootSettings.LootType ConvertShortnameToLootType(string shortname)
		{
			LootSettings.LootType lootType;
			if (shortname.Contains("codelockedhackablecrate"))
			{
				lootType = LootSettings.LootType.HackCrate;
			}
			else
			{
				var def = ItemManager.FindItemDefinition(shortname);
				if (def != null)
					switch (def.category)
					{
						case ItemCategory.Resources:
							lootType = LootSettings.LootType.Gather;
							break;
						case ItemCategory.Component:
							lootType = LootSettings.LootType.LootCrate;
							break;
						default:
							lootType = def.Blueprint != null
								? LootSettings.LootType.Craft
								: LootSettings.LootType.Look;
							break;
					}
				else
					lootType = LootSettings.LootType.Kill;
			}

			return lootType;
		}

		private void UpdateAvatarsAfterUpdate(string avatar)
		{
			if (_clansList == null || _clansList.Count == 0)
				try
				{
					_clansList = Interface.Oxide.DataFileSystem.ReadObject<List<ClanData>>($"{Name}/ClansList");
				}
				catch
				{
					//ignore
				}

			_clansList?.ForEach(clan =>
			{
				if (clan.Avatar.Equals(avatar)) clan.Avatar = string.Empty;
			});

			if (_clansList != null) Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ClansList", _clansList);
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

		private Dictionary<string, ClanData> _clanByTag = new();

		private List<ClanData> _clansList = new();

		private void SaveClans()
		{
#if TESTING
			using (new StopwatchWrapper("Save clans"))
#endif
			{
				Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ClansList", _clansList);
			}
		}

		private void LoadClans()
		{
#if TESTING
			using (new StopwatchWrapper("Load clans"))
#endif
			{
				try
				{
					_clansList = Interface.Oxide.DataFileSystem.ReadObject<List<ClanData>>($"{Name}/ClansList");
				}
				catch (Exception e)
				{
					PrintError(e.ToString());
				}

				_clansList ??= new List<ClanData>();

				_clansList.ForEach(clan => clan.Load());
			}
		}

		private class ClanData
		{
			#region Fields

			[JsonProperty(PropertyName = "Clan Tag")]
			public string ClanTag;

			[JsonProperty(PropertyName = "Tag Color")]
			public string TagColor;

			[JsonProperty(PropertyName = "Avatar")]
			public string Avatar;

			[JsonProperty(PropertyName = "Leader ID")]
			public ulong LeaderID;

			[JsonProperty(PropertyName = "Leader Name")]
			public string LeaderName;

			[JsonProperty(PropertyName = "Description")]
			public string Description;

			[JsonProperty(PropertyName = "Creation Time")]
			public DateTime CreationTime;

			[JsonProperty(PropertyName = "Last Online Time")]
			public DateTime LastOnlineTime;

			[JsonProperty(PropertyName = "Friendly Fire")]
			public bool FriendlyFire;

			[JsonProperty(PropertyName = "Ally Friendly Fire")]
			public bool AllyFriendlyFire;

			[JsonProperty(PropertyName = "Moderators", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ulong> Moderators = new();

			[JsonProperty(PropertyName = "Members", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ulong> Members = new();

			[JsonProperty(PropertyName = "Resource Standarts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, ResourceStandart> ResourceStandarts = new();

			[JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, ulong> Skins = new();

			[JsonProperty(PropertyName = "Alliances", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Alliances = new();

			[JsonProperty(PropertyName = "Team ID")]
			public ulong TeamID;

			[JsonIgnore] public int Top;

			[JsonIgnore]
			private RelationshipManager.PlayerTeam Team =>
				RelationshipManager.ServerInstance.FindTeam(TeamID) ?? FindOrCreateTeam();

			#endregion

			#region Info

			public bool IsOwner(string userId)
			{
				return IsOwner(Convert.ToUInt64(userId));
			}

			public bool IsOwner(ulong userId)
			{
				return LeaderID == userId;
			}

			public bool IsModerator(string userId)
			{
				return IsModerator(Convert.ToUInt64(userId));
			}

			public bool IsModerator(ulong userId)
			{
				return Moderators.Contains(userId) || IsOwner(userId);
			}

			public bool IsMember(string userId)
			{
				return IsMember(Convert.ToUInt64(userId));
			}

			public bool IsMember(ulong userId)
			{
				return Members.Contains(userId);
			}

			public string GetRoleColor(string userId)
			{
				return IsOwner(userId) ? _config.Colors.Owner :
					IsModerator(userId) ? _config.Colors.Moderator : _config.Colors.Member;
			}

			public string GetHexTagColor()
			{
				return string.IsNullOrEmpty(TagColor) ? _config.Tags.TagColor.DefaultColor : TagColor;
			}

			public bool CanEditTagColor(ulong userId)
			{
				if (_config.Tags.TagColor.Owners)
					if (IsOwner(userId))
						return true;

				if (_config.Tags.TagColor.Moderators)
					if (IsModerator(userId))
						return true;

				if (_config.Tags.TagColor.Players)
					if (IsMember(userId))
						return true;

				return false;
			}

			public string GetFormattedClanTag()
			{
				return
					$"{_config.ChatSettings.TagFormat.Replace("{color}", GetHexTagColor()).Replace("{tag}", ClanTag)}";
			}

			#endregion

			#region Create

			public static ClanData CreateNewClan(string clanTag, BasePlayer leader)
			{
#if TESTING
				using (new StopwatchWrapper("Clan create"))
#endif
				{
					clanTag = _instance.AdjustClanTagCase(clanTag);
					
					var clan = new ClanData
					{
						ClanTag = clanTag,
						LeaderID = leader.userID,
						LeaderName = leader.displayName,
						Avatar = string.Empty,
						Members = new List<ulong>
						{
							leader.userID
						},
						CreationTime = DateTime.Now,
						LastOnlineTime = DateTime.Now,
						Top = _instance._clansList.Count + 1
					};

					#region Invites

					_invites.RemovePlayerInvites(leader.userID);

					#endregion

					_instance._clansList.Add(clan);
					_instance._clanByTag[clanTag] = clan;

					if (_config.TagInName)
						leader.displayName = $"[{clanTag}] {GetPlayerName(leader)}";

					if (_config.AutoTeamCreation)
						clan.FindOrCreateTeam();

					ClanCreate(clanTag);

					_instance.NextTick(() => _instance.HandleTop());
					return clan;
				}
			}

			#endregion

			#region Main

			public void Rename(string newName)
			{
#if TESTING
				using (new StopwatchWrapper("Clan rename"))
#endif
				{
					if (string.IsNullOrEmpty(newName)) return;

					var oldName = ClanTag;
					ClanTag = newName;

					_invites.AllianceInvites.ToList().ForEach(invite =>
					{
						if (invite.SenderClanTag == oldName) invite.SenderClanTag = newName;

						if (invite.TargetClanTag == oldName) invite.TargetClanTag = newName;
					});

					foreach (var check in Alliances)
					{
						var clan = _instance.FindClanByTag(check);
						if (clan != null)
						{
							clan.Alliances.Remove(oldName);
							clan.Alliances.Add(newName);
						}
					}

					_invites.PlayersInvites.ForEach(invite =>
					{
						if (invite.ClanTag == oldName)
							invite.ClanTag = newName;
					});

					foreach (var player in Players)
						_instance?.OnPlayerConnected(player);

					ClanUpdate(ClanTag);
				}
			}

			public void Disband()
			{
#if TESTING
				using (new StopwatchWrapper("Clan disband"))
#endif
				{
					var memberUserIDs = Members.Select(x => x.ToString());

					ClanDisbanded(memberUserIDs);
					ClanDisbanded(ClanTag, memberUserIDs);

					Members.ToList().ForEach(member => Kick(member, true));

					ClanDestroy(ClanTag);

					_instance?._clansList.ForEach(clanData =>
					{
						clanData.Alliances.Remove(ClanTag);

						_invites.RemoveAllyInvite(ClanTag);
					});

					if (_config.AutoTeamCreation)
						Team?.members.ToList().ForEach(member =>
						{
							Team.RemovePlayer(member);

							var player = RelationshipManager.FindByID(member);
							if (player != null)
								player.ClearTeam();
						});

					_instance?._clanByTag.Remove(ClanTag);
					_instance?._clansList.Remove(this);

					_instance?.NextTick(() => _instance.HandleTop());
				}
			}

			public void Join(BasePlayer player)
			{
#if TESTING
				using (new StopwatchWrapper("Clan join"))
#endif
				{
					Members.Add(player.userID);

					if (_config.TagInName)
						player.displayName = $"[{ClanTag}] {player.displayName}";

					if (_config.AutoTeamCreation)
					{
						player.Team?.RemovePlayer(player.userID);

                        AddPlayer(player.userID);
					}

					if (Members.Count >= _config.LimitSettings.MemberLimit) _invites.RemovePlayerClanInvites(ClanTag);

					_invites.RemovePlayerInvites(player.userID);

					ClanMemberJoined(player.UserIDString, ClanTag);

					ClanMemberJoined(player.UserIDString, Members.Select(x => x.ToString()));

					ClanUpdate(ClanTag);
				}
			}

			public void Kick(ulong target, bool disband = false)
			{
#if TESTING
				using (new StopwatchWrapper("Clan kick"))
#endif
				{
					var targetStringId = target.ToString();

					Members.Remove(target);
					Moderators.Remove(target);

					_instance?._playerToClan.Remove(targetStringId);

					if (_config.TagInName)
					{
						var name = _instance?.GetPlayerName(target);
						if (!string.IsNullOrWhiteSpace(name))
						{
							var player = RelationshipManager.FindByID(target);
							if (player != null)
								player.displayName = name;
						}
					}

					if (!disband)
					{
						if (_config.AutoTeamCreation && Team != null) Team.RemovePlayer(target);

						if (Members.Count == 0)
						{
							Disband();
						}
						else
						{
							if (LeaderID == target)
								SetLeader((Moderators.Count > 0 ? Moderators : Members).GetRandom());
						}
					}

					ClanMemberGone(targetStringId, Members.Select(x => x.ToString()));

					ClanMemberGone(targetStringId, ClanTag);

					ClanUpdate(ClanTag);
				}
			}

			public void SetModer(ulong target)
			{
#if TESTING
				using (new StopwatchWrapper("Clan set moder"))
#endif
				{
					if (!Moderators.Contains(target))
						Moderators.Add(target);

					ClanUpdate(ClanTag);
				}
			}

			public void UndoModer(ulong target)
			{
#if TESTING
				using (new StopwatchWrapper("Clan undo moder"))
#endif
				{
					Moderators.Remove(target);

					ClanUpdate(ClanTag);
				}
			}

			public void SetLeader(ulong target)
			{
#if TESTING
				using (new StopwatchWrapper("Clan set leader"))
#endif
				{
					LeaderName = _instance.GetPlayerName(target);

					LeaderID = target;

					if (_config.AutoTeamCreation)
						Team.SetTeamLeader(target);

					ClanUpdate(ClanTag);
				}
			}

			#endregion

			#region Additionall

			[JsonIgnore] public float TotalScores;

			[JsonIgnore] public float TotalFarm;

			public void Load()
			{
#if TESTING
				using (new StopwatchWrapper("Clan load"))
#endif
				{
                    _instance._clanByTag[ClanTag] = this;

					UpdateClanStats();
				}
			}

			public void UpdateClanStats()
			{
				UpdateScore();

				UpdateTotalFarm();
			}

			public void UpdateScore()
			{
#if TESTING
				using (new StopwatchWrapper("Clan update score"))
#endif
				{
					TotalScores = GetScore();
				}
			}

			public void UpdateTotalFarm()
			{
#if TESTING
				using (new StopwatchWrapper("Clan update total farm"))
#endif
				{
					TotalFarm = GetTotalFarm();
				}
			}

			public RelationshipManager.PlayerTeam FindOrCreateTeam()
			{
#if TESTING
				using (new StopwatchWrapper("Clan find or create team"))
#endif
				{
					var team = RelationshipManager.ServerInstance.FindTeam(TeamID) ??
					           RelationshipManager.ServerInstance.FindPlayersTeam(LeaderID);
					if (team != null)
					{
						if (_config.AllianceSettings.AllyAddPlayersTeams) return team;

						if (team.teamLeader == LeaderID)
						{
							TeamID = team.teamID;
							return team;
						}

						team.RemovePlayer(LeaderID);
					}

					return CreateTeam();
				}
			}

			public RelationshipManager.PlayerTeam CreateTeam()
			{
#if TESTING
				using (new StopwatchWrapper("Clan create team"))
#endif
				{
					var team = RelationshipManager.ServerInstance.CreateTeam();
					team.teamLeader = LeaderID;
					AddPlayer(LeaderID, team);

					TeamID = team.teamID;

					return team;
				}
			}

			public RelationshipManager.PlayerTeam FindTeam()
			{
#if TESTING
				using (new StopwatchWrapper("Clan find team"))
#endif
				{
					var leaderTeam = RelationshipManager.ServerInstance.FindPlayersTeam(LeaderID);
					if (leaderTeam != null)
					{
						TeamID = leaderTeam.teamID;
						return leaderTeam;
					}

					return null;
				}
			}

			public void SetTeam(ulong teamID)
			{
#if TESTING
				using (new StopwatchWrapper("Clan set team"))
#endif
				{
					TeamID = teamID;
				}
			}

			public void AddPlayer(ulong member, RelationshipManager.PlayerTeam team = null)
			{
#if TESTING
				using (new StopwatchWrapper("Clan add player to team"))
#endif
				{
					team ??= Team;

					if (!team.members.Contains(member))
					{
						team.members.Add(member);
					}

					if ((_config.AllianceSettings.Enabled && _config.AllianceSettings.AllyAddPlayersTeams) == false)
						if (member == LeaderID)
						{
							team.teamLeader = LeaderID;
						}

					RelationshipManager.ServerInstance.playerToTeam[member] = team;

					var player = RelationshipManager.FindByID(member);
					if (player != null)
					{
						if (player.Team != null && player.Team.teamID != team.teamID)
						{
							player.Team.RemovePlayer(player.userID);
							player.ClearTeam();
						}

						player.currentTeam = team.teamID;

						team.MarkDirty();
						player.SendNetworkUpdate();
					}
				}
			}

			private float GetScore()
			{
#if TESTING
				using (new StopwatchWrapper("Clan get score"))
#endif
				{
					return Members.Sum(member => PlayerData.GetNotLoad(member.ToString())?.Score ?? 0f);
				}
			}

			private string Scores()
			{
#if TESTING
				using (new StopwatchWrapper("Clan scores"))
#endif
				{
					return GetValue(TotalScores);
				}
			}

			private float GetTotalFarm()
			{
#if TESTING
				using (new StopwatchWrapper("Clan get total farm"))
#endif
				{
					var sum = 0f;

					PlayerData data;
					Members.ForEach(member =>
					{
						if (_instance.TopPlayers.TryGetValue(member, out var topPlayerData))
						{
							sum += topPlayerData.TotalFarm;
							return;
						}

						if ((data = PlayerData.GetNotLoad(member.ToString())) != null) sum += data.GetTotalFarm(this);
					});

					return (float) Math.Round(sum / Members.Count, 3);
				}
			}

			public JObject ToJObject()
			{
#if TESTING
				using (new StopwatchWrapper("Clan to json object"))
#endif
				{
					var clanObj = new JObject
					{
						["tag"] = ClanTag,
						["description"] = Description,
						["owner"] = LeaderID.ToString()
					};

					var jmembers = new JArray();
					Members.ForEach(user => jmembers.Add(user.ToString()));
					clanObj["members"] = jmembers;

					var jmoders = new JArray();
					Moderators.ForEach(user => jmoders.Add(user.ToString()));
					clanObj["moderators"] = jmoders;

					var jallies = new JArray();
					Alliances.ForEach(ally => jallies.Add(ally));
					clanObj["allies"] = jallies;

					var jinvallies = new JArray();
					_invites?.GetAllyTargetInvites(ClanTag)?.ForEach(invite =>
					{
						if (invite != null)
							jinvallies.Add(invite.TargetClanTag);
					});
					clanObj["invitedallies"] = jinvallies;

					return clanObj;
				}
			}

			public void SetSkin(string shortName, ulong skin)
			{
#if TESTING
				using (new StopwatchWrapper("Clan set skin"))
#endif
				{
					Skins[shortName] = skin;

					foreach (var player in Players.Where(x => _instance.CanUseSkins(x)))
					{
						var activeItem = player.GetActiveItem();
						
						var allItems = Pool.Get<List<Item>>();
						player.inventory.GetAllItems(allItems);
						
						foreach (var item in allItems)
							if (item.info.shortname == shortName)
							{
								ApplySkinToItem(item, skin);

								ApplySkinToActiveItem(activeItem, item, player);
							}
						
						Pool.FreeUnmanaged(ref allItems);
					}
				}
			}

			private static void ApplySkinToActiveItem(Item activeItem, Item item, BasePlayer player)
			{
#if TESTING
				using (new StopwatchWrapper("Clan apply skin to active item"))
#endif
				{
					if (activeItem != null && activeItem == item)
					{
						var slot = activeItem.position;

						activeItem.SetParent(null);
						activeItem.MarkDirty();

						player.Invoke(() =>
						{
							activeItem.SetParent(player.inventory.containerBelt);
							activeItem.position = slot;
							activeItem.MarkDirty();
						}, 0.15f);
					}
				}
			}

			public string GetParams(string value)
			{
#if TESTING
				using (new StopwatchWrapper($"Clan get params for {value}"))
#endif
				{
					switch (value)
					{
						case "name":
							return ClanTag;
						case "leader":
							return LeaderName;
						case "members":
							return Members.Count.ToString();
						case "score":
							return Scores();
						default:
							return Math.Round(
									Members.Sum(
										member => PlayerData.GetNotLoad(member.ToString())?.GetAmount(value) ?? 0f))
								.ToString(CultureInfo.InvariantCulture);
					}
				}
			}

			public void UpdateLeaderName(string name)
			{
#if TESTING
				using (new StopwatchWrapper("Clan update leader name"))
#endif
				{
					LeaderName = name;
				}
			}

			#endregion

			#region Utils

			[JsonIgnore]
			public IEnumerable<BasePlayer> Players
			{
				get
				{
					foreach (var member in Members)
					{
						var player = RelationshipManager.FindByID(member);
						if (player != null) yield return player;
					}
				}
			}

			public void Broadcast(string key, params object[] obj)
			{
#if TESTING
				using (new StopwatchWrapper($"Clan broadcast message {key}"))
#endif
				{
					foreach (var player in Players) 
						_instance.Reply(player.IPlayer, key, obj);
				}
			}

			public bool ContainsAllianceMember(ulong steamID)
			{
				for (var i = Alliances.Count - 1; i >= 0; i--)
				{
					var clan = _instance?.FindClanByTag(Alliances[i]);
					if (clan == null) continue;

					if (clan.IsMember(steamID)) return true;
				}

				return false;
			}

			#endregion

			#region Clan Info

			public string GetClanInfo(BasePlayer player)
			{
#if TESTING
				using (new StopwatchWrapper("Clan get info"))
#endif
				{
					var str = new StringBuilder();
					str.Append(_instance.Msg(player.UserIDString, ClanInfoTitle));
					str.Append(_instance.Msg(player.UserIDString, ClanInfoTag, ClanTag));

					if (!string.IsNullOrEmpty(Description))
						str.Append(_instance.Msg(player.UserIDString, ClanInfoDescription, Description));

					var online = Pool.Get<List<string>>();
					var offline = Pool.Get<List<string>>();

					foreach (var kvp in Members)
					{
						var member = string.Format(COLORED_LABEL, GetRoleColor(kvp.ToString()),
							_instance.GetPlayerName(kvp));

						if (IsOnline(kvp))
							online.Add(member);
						else offline.Add(member);
					}

					if (online.Count > 0)
						str.Append(_instance.Msg(player.UserIDString, ClanInfoOnline, online.ToSentence()));

					if (offline.Count > 0)
						str.Append(_instance.Msg(player.UserIDString, ClanInfoOffline, offline.ToSentence()));

					Pool.FreeUnmanaged(ref online);
					Pool.FreeUnmanaged(ref offline);

					str.Append(_instance.Msg(player.UserIDString, ClanInfoEstablished, CreationTime));
					str.Append(_instance.Msg(player.UserIDString, ClanInfoLastOnline, LastOnlineTime));

					if (_config.AllianceSettings.Enabled)
						str.Append(_instance.Msg(player.UserIDString, ClanInfoAlliances,
							Alliances.Count > 0
								? Alliances.ToSentence()
								: _instance.Msg(player.UserIDString, ClanInfoAlliancesNone)));

					return str.ToString();
				}
			}

			#endregion
		}

		private class ResourceStandart
		{
			public string ShortName;

			public int Amount;

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
			public string DisplayName
			{
				get
				{
					var def = ItemManager.FindItemDefinition(itemId);
					return def != null ? def.displayName.english : ShortName;
				}
			}

			[JsonIgnore] private ICuiComponent _image;

			public CuiElement GetImage(string aMin, string aMax, string oMin, string oMax, string parent,
				string name = null)
			{
				_image ??= new CuiImageComponent
				{
					ItemId = itemId
				};

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
		}

		#region Stats

		private void OnPlayerStatsGathered(LootSettings.LootType type, ulong member, string shortName, int amount = 1)
		{
#if TESTING
			using (new StopwatchWrapper("Add to stats"))
#endif
			{
				if (!member.IsSteamId())
				{
#if TESTING
					SayDebug($"[OnPlayerStatsGathered.{member}] is not a steamId");
#endif
					return;
				}

				var clan = FindClanByPlayer(member.ToString());
				if (clan == null)
				{
#if TESTING
					SayDebug($"[OnPlayerStatsGathered.{member}] hasn't a clan");
#endif
					return;
				}

				if (!_config.Loot.TryGetLoot(type, shortName, out var score) &&
				    !_config.AvailableStandartItems.Contains(shortName))
				{
#if TESTING
					SayDebug($"[OnPlayerStatsGathered.{member}] {shortName} isn't a standart item");
#endif
					return;
				}

				var data = PlayerData.GetOrCreate(member.ToString());
				if (data == null)
				{
#if TESTING
					SayDebug($"[OnPlayerStatsGathered.{member}] hasn't a data");
#endif

					return;
				}

				data.StatsStorage.Add(type, shortName, amount);

#if TESTING
				SayDebug($"[OnPlayerStatsGathered.{member}] now amount: {data.Stats[shortName]}");
#endif

				clan.TotalScores += (float) Math.Round(amount * score);
			}
		}

		private float GetStatsValue(ulong member, string shortname)
		{
#if TESTING
			using (new StopwatchWrapper("get stats value"))
#endif
			{
				var data = PlayerData.GetOrCreate(member.ToString());
				if (data == null) return 0;

				switch (shortname)
				{
					case "total":
					{
						return data.Score;
					}
					case "kd":
					{
						return data.KD;
					}
					case "resources":
					{
						return data.Resources;
					}
					default:
					{
						return data.StatsStorage.GetAmountByShortName(shortname);
					}
				}
			}
		}

		#endregion

		#endregion

		#region Hooks

		private void Init()
		{
#if TESTING
			StopwatchWrapper.OnComplete = SendLogMessage;
#endif

			_instance = this;

			LoadClans();

			LoadInvites();

			UnsubscribeHooks();

			_config.Loot.Init();

			RegisterCommands();

			RegisterPermissions();

			PurgeClans();

#if TESTING
			LoadTestingPlayers();
#endif
		}

		private void OnServerInitialized()
		{
			LoadImages();

			FillingStandartItems();

			LoadSkins();

			LoadChat();

			if (_config.Tags.CheckingCharacters)
				_tagFilter = new Regex($"[^a-zA-Z0-9{_config.Tags.AllowedCharacters}]");

			LoadPlayers();

			LoadAlliances();

			FillingTeams();

			Puts($"Loaded {_clansList.Count} clans!");

			InitTopHandle();

			timer.Every(_config.TopRefreshRate, HandleTop);
		}

		private void OnServerSave()
		{
			if (_config.Saving.SavePlayersOnServerSave)
				timer.In(Random.Range(2f, 10f), PlayerData.Save);

			if (_config.Saving.SaveClansOnServerSave)
				timer.In(Random.Range(2f, 10f), SaveClans);
		}

		private void Unload()
		{
			try
			{
				if (_actionAvatars != null)
					ServerMgr.Instance.StopCoroutine(_actionAvatars);

				if (_actionConvert != null)
					ServerMgr.Instance.StopCoroutine(_actionConvert);

				if (_initTopHandle != null)
					ServerMgr.Instance.StopCoroutine(_initTopHandle);

				if (_topHandle != null)
					ServerMgr.Instance.StopCoroutine(_topHandle);

				if (_wipePlayers != null)
					ServerMgr.Instance.StopCoroutine(_wipePlayers);

				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, Layer);
					CuiHelper.DestroyUi(player, ModalLayer);

					if (_config.TagInName)
					{
						var newName = GetPlayerName(player.userID);

#if TESTING
					Puts($"[Unload] player={player.UserIDString}, newName={newName}");
#endif

						player.displayName = newName;
					}

					PlayerData.SaveAndUnload(player.UserIDString);
				}

				SaveClans();

				SaveInvites();
			}
			finally
			{
				_instance = null;
				_config = null;
				_invites = null;
			}
		}

		private void OnNewSave(string filename)
		{
			if (_config.PurgeSettings.WipeClansOnNewSave)
			{
				try
				{
					if (_clansList == null || _clansList.Count == 0)
						LoadClans();

					_clansList?.Clear();
					_clanByTag?.Clear();

					SaveClans();
				}
				catch (Exception e)
				{
					PrintError($"[On Server Wipe] in wipe clans, error: {e.Message}");
				}
			}

			if (_config.PurgeSettings.WipeInvitesOnNewSave)
			{
				try
				{
					if (_invites == null)
						LoadInvites();

					_invites?.DoWipe();

					SaveInvites();
				}
				catch (Exception e)
				{
					PrintError($"[On Server Wipe] in wipe invites, error: {e.Message}");
				}
			}

			if (_config.PurgeSettings.WipePlayersOnNewSave)
			{
				try
				{
					var players = PlayerData.GetFiles();
					if (players is {Length: > 0})
					{
						_wipePlayers =
							ServerMgr.Instance.StartCoroutine(StartOnAllPlayers(players,
								PlayerData.DoWipe));

						_usersData?.Clear();
					}
				}
				catch (Exception e)
				{
					PrintError($"[On Server Wipe] in wipe players, error: {e.Message}");
				}
			}
		}

		private void OnPlayerConnected(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("player connected"))
#endif
			{
				if (player == null || !player.userID.IsSteamId()) return;

				GetAvatar(player.UserIDString,
					avatar => AddImage(avatar, $"avatar_{player.UserIDString}"));

#if TESTING
				Puts("[OnPlayerConnected] call GetOrCreate PlayerData");
#endif
				var data = PlayerData.GetOrCreate(player.UserIDString);
				if (data == null)
					return;

#if TESTING
				var oldDisplayName = data.DisplayName;
#endif

				data.DisplayName = GetPlayerName(player);
				data.LastLogin = DateTime.Now;

#if TESTING
				Puts("[OnPlayerConnected] check topPlayerData");
#endif

				if (TopPlayers.TryGetValue(player.userID, out var topPlayerData))
				{
					topPlayerData.SetData(ref data);
				}
				else
				{
					TopPlayers[player.userID] = new TopPlayerData(data)
					{
						Top = ++_lastPlayerTop
					};
				}
#if TESTING
				if (oldDisplayName != data.DisplayName)
					Puts(
						$"Updating display name for player {player.UserIDString} from '{oldDisplayName}' to '{data.DisplayName}'");
#endif

				PlayerData.Save(player.UserIDString);

				var clan = data.GetClan();
				if (clan == null)
				{
					if (_config.ForceClanCreateTeam && player.Team != null)
					{
#if TESTING
						Puts($"Forcing clan creation UI for player {player.UserIDString}");
#endif
						ShowClanCreationUI(player);
					}

					return;
				}

				clan.LastOnlineTime = DateTime.Now;

				if (_config.TagInName)
				{
#if TESTING
					Puts($"Setting player {player.UserIDString} display name to [{clan.ClanTag}] {data.DisplayName}");
#endif
					player.displayName = $"[{clan.ClanTag}] {data.DisplayName}";
				}

				if (_config.AutoTeamCreation)
				{
#if TESTING
					Puts($"Adding player {player.UserIDString} to clan {clan.ClanTag}, with team={clan.TeamID}");
#endif
					clan.AddPlayer(player.userID);
				}

				if (clan.IsOwner(player.userID))
				{
#if TESTING
					Puts($"Updating leader name for clan {clan.ClanTag} to {data.DisplayName}");
#endif
					clan.UpdateLeaderName(data.DisplayName);
				}
			}
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("player disconnected"))
#endif
			{
				if (player == null || !player.userID.IsSteamId()) return;

				if (TopPlayers.TryGetValue(player.userID, out var topPlayerData))
					topPlayerData.SetDataToNull();

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan != null)
				{
					clan.LastOnlineTime = DateTime.Now;
#if TESTING
					Puts($"Updating last online time for clan {clan.ClanTag} to {clan.LastOnlineTime}");
#endif
				}

				PlayerData.SaveAndUnload(player.UserIDString);
#if TESTING
				Puts($"Saved and unloaded player data for {player.UserIDString}");
#endif
			}
		}

		#region Stats

		#region Kills

		private void OnPlayerDeath(BasePlayer player, HitInfo info)
		{
#if TESTING
			using (new StopwatchWrapper("player death"))
#endif
			{
				if (player == null || info == null ||
				    (player.ShortPrefabName == "player" && !player.userID.IsSteamId())) return;

				var attacker = info.InitiatorPlayer;
				if (attacker == null || !attacker.userID.IsSteamId()
				                     || IsTeammates(player.userID, attacker.userID)) return;

				if (player.userID.IsSteamId())
				{
					OnPlayerStatsGathered(LootSettings.LootType.Kill, attacker.userID, "kills");

					OnPlayerStatsGathered(LootSettings.LootType.Kill, player.userID, "deaths");
				}
				else
				{
					OnPlayerStatsGathered(LootSettings.LootType.Kill, attacker.userID, player.ShortPrefabName);
				}
			}
		}

		#endregion

		#region Gather

		private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("collectible pickup"))
#endif
			{
				if (collectible == null || collectible.itemList == null) return;

				foreach (var itemAmount in collectible.itemList)
					if (itemAmount.itemDef != null)
						OnItemGather(player.userID, itemAmount.itemDef.shortname, (int) itemAmount.amount);
			}
		}

		private void OnCropGather(GrowableEntity plant, Item item, BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("crop gather"))
#endif
			{
				OnItemGather(player.userID, item.info.shortname, item.amount);
			}
		}

		private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
#if TESTING
			using (new StopwatchWrapper("dispenser bonus"))
#endif
			{
				OnItemGather(player.userID, item.info.shortname, item.amount);
			}
		}

		private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
#if TESTING
			using (new StopwatchWrapper("dispenser gather"))
#endif
			{
				OnItemGather(player.userID, item.info.shortname, item.amount);
			}
		}

		private void OnItemGather(ulong player, string shortname, int amount)
		{
#if TESTING
			using (new StopwatchWrapper("on gather"))
#endif
			{
				if (string.IsNullOrEmpty(shortname) || amount <= 0)
				{
#if TESTING
					SayDebug($"[OnItemGather] canceled gather of {shortname} with {amount}");
#endif

					return;
				}
#if TESTING
				SayDebug($"[OnItemGather] gathering {shortname} with {amount}");
#endif
				OnPlayerStatsGathered(LootSettings.LootType.Gather, player, shortname, amount);
			}
		}

		#endregion

		#region Loot

		#region Containers

		private readonly Dictionary<ulong, ulong> _lootedContainers = new Dictionary<ulong, ulong>();

		private void OnLootEntity(BasePlayer player, LootContainer lootContainer)
		{
			if (player == null || lootContainer == null || lootContainer.net == null ||
			    !lootContainer.net.ID.IsValid) return;

			var netID = lootContainer.net.ID.Value;

			if (_lootedContainers.ContainsKey(netID)) return;

			_lootedContainers.Add(netID, player.userID);

			OnPlayerStatsGathered(LootSettings.LootType.LootCrate, player.userID, lootContainer.ShortPrefabName);

			lootContainer.inventory?.itemList.ForEach(item =>
				OnPlayerStatsGathered(LootSettings.LootType.LootCrate, player.userID, item.info.shortname,
					item.amount));
		}

		#endregion

		#region Barrels

		private readonly List<ulong> _dropItems = new List<ulong>();

		private void OnContainerDropItems(ItemContainer container)
		{
			if (container == null) return;

			_dropItems.AddRange(container.itemList.Select(x => x.uid.Value));
		}

		private void OnItemPickup(Item item, BasePlayer player)
		{
			if (item == null || player == null) return;

			if (_dropItems.Contains(item.uid.Value))
			{
				OnPlayerStatsGathered(LootSettings.LootType.Look, player.userID, item.info.shortname, item.amount);

				_dropItems.Remove(item.uid.Value);
			}
		}

		#endregion

		#endregion

		#region Entity Death

		private readonly Dictionary<ulong, BasePlayer> _lastHeli = new Dictionary<ulong, BasePlayer>();

		private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
#if TESTING
			using (new StopwatchWrapper("entity take damage"))
#endif
			{
				if (entity == null) return;

#if TESTING
				try
				{
#endif
				var helicopter = entity as BaseHelicopter;
				if (helicopter != null && helicopter.net != null && info.InitiatorPlayer != null)
				{
#if TESTING
						Puts(
							$"Adding last attacker {info.InitiatorPlayer.UserIDString} to heli {helicopter.net.ID.Value}");
#endif
					_lastHeli[helicopter.net.ID.Value] = info.InitiatorPlayer;
				}

				if (_config.FriendlyFire.UseFriendlyFire)
				{
					var player = entity as BasePlayer;
					if (player == null) return;

					var initiatorPlayer = info.InitiatorPlayer;
					if (initiatorPlayer == null || player == initiatorPlayer) return;

					if (_config.FriendlyFire.IgnoreOnArenaTournament &&
					    (AT_IsOnTournament(player.userID) || AT_IsOnTournament(initiatorPlayer.userID)))
						return;

					var data = PlayerData.GetOrLoad(initiatorPlayer.UserIDString);
					var clan = data?.GetClan();
					if (clan == null) return;

					if (_config.ZMSettings.Enabled && ZoneManager != null)
					{
						var playerZones = ZM_GetPlayerZones(player);
						if (playerZones.Any(x => _config.ZMSettings.FFAllowlist.Contains(x)))
						{
#if TESTING
								Puts($"Allowing friendly fire in zones: {string.Join(", ", playerZones)}");
#endif
							return;
						}
					}

					var value = _config.FriendlyFire.GeneralFriendlyFire ? clan.FriendlyFire : data.FriendlyFire;
					if (!value && clan.IsMember(player.userID))
					{
						info.damageTypes.ScaleAll(0);

						Reply(initiatorPlayer, CannotDamage);
#if TESTING
							Puts(
								$"Player {initiatorPlayer.UserIDString} cannot damage friendly player {player.UserIDString} in clan {clan.ClanTag}");
#endif
						return;
					}

					value = _config.AllianceSettings.GeneralFriendlyFire
						? clan.AllyFriendlyFire
						: data.AllyFriendlyFire;
					if (!value && IsAllyPlayer(initiatorPlayer.userID, player.userID))
					{
						info.damageTypes.ScaleAll(0);

						Reply(initiatorPlayer, AllyCannotDamage);
#if TESTING
							Puts(
								$"Ally player {initiatorPlayer.UserIDString} cannot damage friendly player {player.UserIDString} in clan {clan.ClanTag}");
#endif
					}
				}

#if TESTING
				}
				catch (Exception ex)
				{
					PrintError($"In the 'OnEntityTakeDamage' there was an error:\n{ex}");

					Debug.LogException(ex);
				}
#endif
			}
		}


		private void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			if (entity == null || player == null || entity.net == null || _lootedEntities.Contains(entity.net.ID.Value))
				return;

#if TESTING
			SayDebug($"[OnLootEntity] called for player {player.UserIDString} with entity {entity.ShortPrefabName}");
#endif

			OnPlayerStatsGathered(LootSettings.LootType.LootCrate, player.userID, entity.ShortPrefabName);

			_lootedEntities.Add(entity.net.ID.Value);
		}

		private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
#if TESTING
			using (new StopwatchWrapper("entity death"))
#endif
			{
				if (entity == null || info == null) return;

				BasePlayer player;

				if (entity is BaseHelicopter)
				{
					if (_lastHeli.TryGetValue(entity.net.ID.Value, out player))
					{
						if (player != null)
						{
#if TESTING
							SayDebug($"[OnEntityDeath.{player.UserIDString}] adding helicopter kill to player {player.userID} stats");
#endif

							OnPlayerStatsGathered(LootSettings.LootType.Kill, player.userID, "helicopter");
						}
#if TESTING
						else
						{
							PrintError("Player is null in _lastHeli dictionary");
						}
#endif
					}
					else
					{
#if TESTING
						PrintError($"Could not find player in _lastHeli dictionary for heli {entity.net.ID.Value}");
#endif
					}

					return;
				}


				player = info.InitiatorPlayer;
				if (player == null) return;

#if TESTING
				SayDebug($"[OnEntityDeath.{player.UserIDString}] called with entity {entity.ShortPrefabName}");
#endif

				if (entity is BradleyAPC)
				{
#if TESTING
					SayDebug($"[OnEntityDeath.{player.UserIDString}] called for BradleyAPC");
#endif

					OnPlayerStatsGathered(LootSettings.LootType.Kill, player.userID, "bradley");
				}
				else if (entity.name.Contains("barrel"))
				{
#if TESTING
					SayDebug($"[OnEntityDeath.{player.UserIDString}] called for barrel");
#endif
					OnPlayerStatsGathered(LootSettings.LootType.Kill, player.userID, "barrel");
				}
				else if (_config.Loot.HasLootByShortName(entity.ShortPrefabName))
				{
#if TESTING
					SayDebug($"[OnEntityDeath.{player.UserIDString}] adding {entity.ShortPrefabName} kill");
#endif
					OnPlayerStatsGathered(LootSettings.LootType.Kill, player.userID, entity.ShortPrefabName);
				}
			}
		}

		#endregion

		#region FF Turrets

		private object OnTurretTarget(AutoTurret turret, BasePlayer target)
		{
			if (target.IsNull() || turret.IsNull() ||
			    target.limitNetworking ||
			    (turret is NPCAutoTurret && !target.userID.IsSteamId()) || target.userID == turret.OwnerID) return null;
			
			var clan = FindClanByPlayer(turret.OwnerID.ToString());
			if (clan == null) return null;

			if (_config.FriendlyFire.GeneralFriendlyFire)
			{
				if (!clan.FriendlyFire && clan.IsMember(target.userID))
					return false;
			}
			else if (_config.AllianceSettings.GeneralFriendlyFire)
			{
				if (!clan.AllyFriendlyFire && clan.ContainsAllianceMember(target.userID))
					return false;
			}
			else
			{
				var data = PlayerData.GetOrLoad(turret.OwnerID.ToString());
				if (data == null) return null;

				if (!data.FriendlyFire && clan.IsMember(target.userID))
				{
					return false;
				}

				if (!data.AllyFriendlyFire && clan.ContainsAllianceMember(target.userID))
				{
					return false;
				}
			}

			return null;
		}
		
		#endregion

		#region Craft

		private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
		{
#if TESTING
			using (new StopwatchWrapper("craft finished"))
#endif
			{
				if (crafter == null) return;

				var player = crafter.owner;
				if (player == null || item == null) return;

#if TESTING
				Puts($"[OnItemCraftFinished] player {player.userID} crafting item {item.info.displayName}");
#endif

				OnPlayerStatsGathered(LootSettings.LootType.Craft, player.userID, item.info.shortname, item.amount);
			}
		}

		#endregion

		#endregion

		#region Skins

		private void OnSkinnerCacheUpdated(Dictionary<int, List<ulong>> cachedSkins)
		{
#if TESTING
			using (new StopwatchWrapper("skinner skins updated"))
#endif
			{
				if (cachedSkins == null) return;

				var itemSkins = new Dictionary<string, List<ulong>>();
				
				foreach (var (itemID, skins) in cachedSkins)
				{
					var def = ItemManager.FindItemDefinition(itemID);
					if (def == null) continue;

					itemSkins.TryAdd(def.shortname, skins);
				}

				_config.Skins.ItemSkins = itemSkins;

				if (_config.Skins.ItemSkins.Count > 0)
					SaveConfig();
			}
		}

		private void OnSkinBoxSkinsLoaded(Hash<string, HashSet<ulong>> skins)
		{
#if TESTING
			using (new StopwatchWrapper("skinbox skins loaded"))
#endif
			{
				if (skins == null) return;

				_config.Skins.ItemSkins = skins.ToDictionary(x => x.Key, y => y.Value.ToList());

				if (_config.Skins.ItemSkins.Count > 0)
					SaveConfig();
			}
		}

		private void OnItemAddedToContainer(ItemContainer container, Item item)
		{
#if TESTING
			using (new StopwatchWrapper("item added to container"))
#endif
			{
				if (container == null || item == null)
					return;

				var player = container.GetOwnerPlayer();
				if (player == null) return;

				if (_enabledSkins)
					TryUpdateSkins(container, item, player);

				if (_config.Statistics.Loot)
					TryCollectLoot(container, item, player);
			}
		}

		#region Helpers
		
        public string AdjustClanTagCase(string clanTag)
        {
            return _config.Tags.CaseSensitive ? clanTag : clanTag.ToLower();
        }

		private void TryCollectLoot(ItemContainer container, Item item, BasePlayer player)
		{
			if (container.playerOwner == null || item?.uid.IsValid != true)
				return;

			if (_looters.ContainsKey(item.uid.Value))
			{
				if (_looters[item.uid.Value] == container.playerOwner.userID) return;

#if TESTING
				Puts($"[OnItemAddedToContainer] player {player.userID} looting item {item.info.shortname}");
#endif

				OnPlayerStatsGathered(LootSettings.LootType.Gather, player.userID, item.info.shortname, item.amount);
				_looters.Remove(item.uid.Value);
			}
			else
			{
				_looters.Add(item.uid.Value, container.playerOwner.userID);
			}
		}

		private void TryUpdateSkins(ItemContainer container, Item item, BasePlayer player)
		{
			var clan = FindClanByPlayer(player.UserIDString);
			if (clan == null) return;

			if (!CanUseSkins(player) ||
			    !_config.Skins.ItemSkins.ContainsKey(item.info.shortname)) return;

			if (!clan.Skins.TryGetValue(item.info.shortname, out var skin) || skin == 0) return;

			if (item.info.category == ItemCategory.Attire)
			{
				if (container == player.inventory.containerWear) ApplySkinToItem(item, skin);
			}
			else
			{
				ApplySkinToItem(item, skin);
			}
		}

		#endregion

		#endregion

		#region Team

		private object OnTeamCreate(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("team create"))
#endif
			{
				if (player == null) return null;

				ShowClanCreationUI(player);

				if (_config.ForceClanCreateTeam)
					return false;

				return null;
			}
		}

		private object OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("team leave"))
#endif
			{
				if (team == null || player == null) return null;

				if (_config.PermissionSettings.UsePermClanLeave &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanLeave) &&
				    !player.HasPermission(_config.PermissionSettings.ClanLeave))
				{
					Reply(player, NoPermLeaveClan);
					return false;
				}

				if (_config.PaidFunctionality.ChargeFeeToLeaveClan && !_config.PaidFunctionality.Economy.RemoveBalance(
					    player,
					    _config.PaidFunctionality.CostLeavingClan))
				{
					Reply(player, PaidLeaveMsg, _config.PaidFunctionality.CostLeavingClan);
					return false;
				}

				FindClanByPlayer(player.UserIDString)?.Kick(player.userID);
				return null;
			}
		}

		private object OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
		{
#if TESTING
			using (new StopwatchWrapper("team kick"))
#endif
			{
				if (team == null || player == null) return null;

				if (_config.PermissionSettings.UsePermClanKick &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanKick) &&
				    !player.HasPermission(_config.PermissionSettings.ClanKick))
				{
					Reply(player, _config.PermissionSettings.ClanKick);
					return false;
				}

				if (_config.PaidFunctionality.ChargeFeeToKickClanMember &&
				    !_config.PaidFunctionality.Economy.RemoveBalance(player,
					    _config.PaidFunctionality.CostKickingClanMember))
				{
					Reply(player, PaidKickMsg, _config.PaidFunctionality.CostKickingClanMember);
					return false;
				}

				var playerClan = FindClanByPlayer(player.UserIDString);
				if (playerClan == null) return null;

				if (!playerClan.IsMember(target))
				{
					return false;
				}

				playerClan.Kick(target);
				return null;
			}
		}

		private void OnTeamInvite(BasePlayer inviter, BasePlayer target)
		{
#if TESTING
			using (new StopwatchWrapper("team invite"))
#endif
			{
				if (inviter == null || target == null) return;

				SendInvite(inviter, target.userID);
			}
		}

		private void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader)
		{
#if TESTING
			using (new StopwatchWrapper("team promote"))
#endif
			{
				if (team == null || newLeader == null) return;

				FindClanByPlayer(team.teamLeader.ToString())?.SetLeader(newLeader.userID);
			}
		}

		private object OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("team accept invite"))
#endif
			{
				if (team == null || player == null) return null;

				if (!HasInvite(player)) return null;

				var data = PlayerData.GetOrLoad(player.UserIDString);
				if (data == null) return null;

				if (data.GetClan() != null)
				{
					Reply(player, AlreadyClanMember);
					return true;
				}

				var clan = FindClanByPlayer(team.teamLeader.ToString());
				if (clan == null) return true;

				if (clan.Members.Count >= _config.LimitSettings.MemberLimit)
				{
					Reply(player, ALotOfMembers);
					return true;
				}

				var inviteData = data.GetInviteByTag(clan.ClanTag);
				if (inviteData == null) return true;

				clan.Join(player);
				Reply(player, ClanJoined, clan.ClanTag);

				var inviter = RelationshipManager.FindByID(inviteData.InviterId);
				if (inviter != null)
					Reply(inviter, WasInvited, data.DisplayName);

				return null;
			}
		}

		#endregion

		#region Chat && Image Library

		private void OnPluginLoaded(Plugin plugin)
		{
#if TESTING
			using (new StopwatchWrapper($"plugin loaded {plugin.Name}"))
#endif
			{
				switch (plugin.Name)
				{
					case "PlayerSkins":
					case "LSkins":
					{
						NextTick(LoadSkins);
						break;
					}
					case "BetterChat":
					{
						NextTick(LoadChat);
						break;
					}
					case "ImageLibrary":
					{
						timer.In(1, LoadImages);
						break;
					}
				}
			}
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
#if TESTING
			using (new StopwatchWrapper("plugin unloaded"))
#endif
			{
				if (plugin.Name == "ImageLibrary") 
					_enabledImageLibrary = false;
			}
		}

		private string OnChatReferenceTags(BasePlayer player)
		{
			return player != null ? FindClanByPlayer(player.UserIDString)?.GetFormattedClanTag() : null;
		}
		
		private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
		{
			var clan = FindClanByPlayer(player.UserIDString);
			if (clan == null)
				return null;

			var displayname = player.Connection.username;
			var tag = clan.GetFormattedClanTag();

#if TESTING
			Puts($"[OnPlayerChat] tag={tag}");
#endif

			var nameColor = GetNameColor(player.userID, player);

			RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
			{
				Channel = channel,
				Message = new Regex("<[^>]*>").Replace(string.Join(" ", message), ""),
				UserId = player.IPlayer.Id,
				Username = player.displayName,
				Color = null,
				Time = Epoch.Current
			});

			switch (channel)
			{
				case Chat.ChatChannel.Global:
				{
					var gMsg = ArrayPool.Get(3);
					gMsg[0] = (int) channel;
					gMsg[1] = player.UserIDString;

					foreach (var p in BasePlayer.activePlayerList.Where(p => p.IsValid()))
					{
						gMsg[2] = $"{tag} <color={nameColor}>{displayname}</color>: {message}";

						p.SendConsoleCommand("chat.add", gMsg);
					}

					ArrayPool.Free(gMsg);
					break;
				}

				case Chat.ChatChannel.Team:
				{
					var tMsg = ArrayPool.Get(3);
					tMsg[0] = (int) channel;
					tMsg[1] = player.UserIDString;

					foreach (var p in BasePlayer.activePlayerList.Where(p =>
						         p.Team != null && player.Team != null && p.Team.teamID == player.Team.teamID &&
						         p.IsValid()))
					{
						tMsg[2] = $"{tag} <color={nameColor}>{displayname}</color>: {message}";

						p.SendConsoleCommand("chat.add", tMsg);
					}

					ArrayPool.Free(tMsg);
					break;
				}
			}

			return true;
		}

		#endregion

		#endregion

		#region Commands

		private void CmdClans(IPlayer cov, string command, string[] args)
		{
#if TESTING
			using (new StopwatchWrapper($"cmd clans {string.Join(", ", args)}"))
#endif
			{
				var player = cov?.Object as BasePlayer;
				if (player == null) return;

				if (_enabledImageLibrary == false)
				{
					Reply(player, NoILError);

					BroadcastILNotInstalled();
					return;
				}

				if (args.Length == 0)
				{
					var hasPlayerClan = PlayerHasClan(player.userID);

					var page = _config.PagesConfig?.Default?.Enabled == true
						? hasPlayerClan
							? _config.PagesConfig.Default.ClanPageWhenExists
							: _config.PagesConfig.Default.ClanPageNotFound
						: hasPlayerClan
							? PLAYERS_TOP
							: ABOUT_CLAN;

					ShowClanMainUI(player, page, first: true);
					return;
				}

				switch (args[0])
				{
					case "create":
					{
						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <clan tag>");
							return;
						}

						if (_config.PermissionSettings.UsePermClanCreating &&
						    !string.IsNullOrEmpty(_config.PermissionSettings.ClanCreating) &&
						    !player.HasPermission(_config.PermissionSettings.ClanCreating))
						{
							Reply(player, NoPermCreateClan);
							return;
						}

						if (PlayerHasClan(player.userID))
						{
							Reply(player, AlreadyClanMember);
							return;
						}

						var tag = string.Join(" ", args.Skip(1));
						if (string.IsNullOrEmpty(tag) || tag.Length < _config.Tags.TagMin ||
						    tag.Length > _config.Tags.TagMax)
						{
							Reply(player, ClanTagLimit, _config.Tags.TagMin, _config.Tags.TagMax);
							return;
						}

						tag = tag.Replace(" ", "");

						if (_config.Tags.BlockedWords.Exists(word =>
							    tag.Contains(word, CompareOptions.OrdinalIgnoreCase)))
						{
							Reply(player, ContainsForbiddenWords);
							return;
						}

						if (_config.Tags.CheckingCharacters && _tagFilter.IsMatch(tag))
						{
							Reply(player, ContainsForbiddenWords);
							return;
						}

						var clan = FindClanByTag(tag);
						if (clan != null)
						{
							Reply(player, ClanExists);
							return;
						}

						clan = ClanData.CreateNewClan(tag, player);
						if (clan == null) return;

						Reply(player, ClanCreated, tag);
						break;
					}

					case "disband":
					{
						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsOwner(player.userID))
						{
							Reply(player, NotClanLeader);
							return;
						}

						if (_config.PermissionSettings.UsePermClanDisband &&
						    !string.IsNullOrEmpty(_config.PermissionSettings.ClanDisband) &&
						    !player.HasPermission(_config.PermissionSettings.ClanDisband))
						{
							Reply(player, NoPermDisbandClan);
							return;
						}

						if (_config.PaidFunctionality.ChargeFeeToDisbandClan &&
						    !_config.PaidFunctionality.Economy.RemoveBalance(player,
							    _config.PaidFunctionality.CostDisbandingClan))
						{
							Reply(player, PaidDisbandMsg, _config.PaidFunctionality.CostDisbandingClan);
							return;
						}

						clan.Disband();
						Reply(player, ClanDisbandedTitle);
						break;
					}

					case "leave":
					{
						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (_config.PermissionSettings.UsePermClanLeave &&
						    !string.IsNullOrEmpty(_config.PermissionSettings.ClanLeave) &&
						    !player.HasPermission(_config.PermissionSettings.ClanLeave))
						{
							Reply(player, NoPermLeaveClan);
							return;
						}

						if (_config.PaidFunctionality.ChargeFeeToLeaveClan &&
						    !_config.PaidFunctionality.Economy.RemoveBalance(player,
							    _config.PaidFunctionality.CostLeavingClan))
						{
							Reply(player, PaidLeaveMsg, _config.PaidFunctionality.CostLeavingClan);
							return;
						}

						clan.Kick(player.userID);
						Reply(player, ClanLeft);
						break;
					}

					case "promote":
					{
						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <name/steamid>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsOwner(player.userID))
						{
							Reply(player, NotClanLeader);
							return;
						}

						var target = covalence.Players.FindPlayer(args[1]);
						if (target == null)
						{
							Reply(player, PlayerNotFound, args[1]);
							return;
						}

						if (clan.IsModerator(target.Id))
						{
							Reply(player, ClanAlreadyModer, target.Name);
							return;
						}

						if (clan.Moderators.Count >= _config.LimitSettings.ModeratorLimit)
						{
							Reply(player, ALotOfModers);
							return;
						}

						clan.SetModer(Convert.ToUInt64(target.Id));
						Reply(player, PromotedToModer, target.Name);
						break;
					}

					case "demote":
					{
						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <name/steamid>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsOwner(player.userID))
						{
							Reply(player, NotClanLeader);
							return;
						}

						var target = covalence.Players.FindPlayer(args[1]);
						if (target == null)
						{
							Reply(player, PlayerNotFound, args[1]);
							return;
						}

						if (!clan.IsModerator(target.Id))
						{
							Reply(player, NotClanModer, target.Name);
							return;
						}

						clan.UndoModer(Convert.ToUInt64(target.Id));
						Reply(player, DemotedModer, target.Name);
						break;
					}

					case "invite":
					{
						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <name/steamid>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						var target = covalence.Players.FindPlayer(args[1]);
						if (target == null)
						{
							Reply(player, PlayerNotFound, args[1]);
							return;
						}

						SendInvite(player, Convert.ToUInt64(target.Id));
						break;
					}

					case "withdraw":
					{
						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <name/steamid>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						var target = covalence.Players.FindPlayer(args[1]);
						if (target == null)
						{
							Reply(player, PlayerNotFound, args[1]);
							return;
						}

						WithdrawInvite(player, Convert.ToUInt64(target.Id));
						break;
					}

					case "kick":
					{
						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <name/steamid>");
							return;
						}

						if (_config.PermissionSettings.UsePermClanKick &&
						    !string.IsNullOrEmpty(_config.PermissionSettings.ClanKick) &&
						    !player.HasPermission(_config.PermissionSettings.ClanKick))
						{
							Reply(player, _config.PermissionSettings.ClanKick);
							return;
						}

						if (_config.PaidFunctionality.ChargeFeeToKickClanMember &&
						    !_config.PaidFunctionality.Economy.RemoveBalance(player,
							    _config.PaidFunctionality.CostKickingClanMember))
						{
							Reply(player, PaidKickMsg, _config.PaidFunctionality.CostKickingClanMember);
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						var target = covalence.Players.FindPlayer(args[1]);
						if (target == null)
						{
							Reply(player, PlayerNotFound, args[1]);
							return;
						}

						if (!clan.IsModerator(player.userID))
						{
							Reply(player, NotModer);
							return;
						}

						clan.Kick(Convert.ToUInt64(target.Id));
						Reply(player, SuccsessKick, target.Name);

						var targetPlayer = target.Object as BasePlayer;
						if (targetPlayer != null)
							Reply(targetPlayer, WasKicked);
						break;
					}

					case "ff":
					{
						if (_config.FriendlyFire.UseFriendlyFire)
							CmdClanFF(cov, command, args);
						break;
					}

					case "allyff":
					{
						if (_config.FriendlyFire.UseFriendlyFire)
							CmdAllyFF(cov, command, args);
						break;
					}

					case "allyinvite":
					{
						if (!_config.AllianceSettings.Enabled) return;

						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <clanTag>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsModerator(player.userID))
						{
							Reply(player, NotModer);
							return;
						}

						var targetClan = FindClanByTag(args[1]);
						if (targetClan == null)
						{
							Reply(player, ClanNotFound, args[1]);
							return;
						}

						AllySendInvite(player, targetClan.ClanTag);
						break;
					}

					case "allywithdraw":
					{
						if (!_config.AllianceSettings.Enabled) return;

						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <clanTag>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsModerator(player.userID))
						{
							Reply(player, NotModer);
							return;
						}

						var targetClan = FindClanByTag(args[1]);
						if (targetClan == null)
						{
							Reply(player, ClanNotFound, args[1]);
							return;
						}

						AllyWithdrawInvite(player, targetClan.ClanTag);
						break;
					}

					case "allyaccept":
					{
						if (!_config.AllianceSettings.Enabled) return;

						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <clanTag>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsModerator(player.userID))
						{
							Reply(player, NotModer);
							return;
						}

						var targetClan = FindClanByTag(args[1]);
						if (targetClan == null)
						{
							Reply(player, ClanNotFound, args[1]);
							return;
						}

						AllyAcceptInvite(player, targetClan.ClanTag);
						break;
					}

					case "allycancel":
					{
						if (!_config.AllianceSettings.Enabled) return;

						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <clanTag>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsModerator(player.userID))
						{
							Reply(player, NotModer);
							return;
						}

						var targetClan = FindClanByTag(args[1]);
						if (targetClan == null)
						{
							Reply(player, ClanNotFound, args[1]);
							return;
						}

						AllyCancelInvite(player, targetClan.ClanTag);
						break;
					}

					case "allyrevoke":
					{
						if (!_config.AllianceSettings.Enabled) return;

						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <clanTag>");
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsModerator(player.userID))
						{
							Reply(player, NotModer);
							return;
						}

						var targetClan = FindClanByTag(args[1]);
						if (targetClan == null)
						{
							Reply(player, ClanNotFound, args[1]);
							return;
						}

						AllyRevoke(player, targetClan.ClanTag);
						break;
					}

					case "description":
					{
						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <description>");
							return;
						}

						var description = string.Join(" ", args.Skip(1));
						if (string.IsNullOrEmpty(description)) return;

						if (description.Length > _config.DescriptionMax)
						{
							Reply(player, MaxDescriptionSize);
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsOwner(player.userID))
						{
							Reply(player, NotClanLeader);
							return;
						}

						clan.Description = description;
						Reply(player, SetDescription);
						break;
					}

					case "join":
					{
						if (FindClanByPlayer(player.UserIDString) != null)
						{
							Reply(player, AlreadyClanMember);
							return;
						}

						ShowClanMainUI(player, 45, first: true);
						break;
					}

					case "tagcolor":
					{
						if (!_config.Tags.TagColor.Enabled) return;

						if (args.Length < 2)
						{
							SendReply(player, $"Error syntax! Use: /{command} {args[0]} <tag color>");
							return;
						}

						var hexColor = string.Join(" ", args.Skip(1));
						if (string.IsNullOrEmpty(hexColor)) return;

						hexColor = hexColor.Replace("#", "");

						if (hexColor.Length < 6 || hexColor.Length > 6 || !_hexFilter.IsMatch(hexColor))
						{
							Reply(player, TagColorFormat);
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null) return;

						if (!clan.CanEditTagColor(player.userID))
						{
							Reply(player, NoPermissions);
							return;
						}

						var oldTagColor = clan.GetHexTagColor();
						if (!string.IsNullOrEmpty(oldTagColor) && oldTagColor.Equals(hexColor))
							return;

						clan.TagColor = hexColor;

						Reply(player, TagColorInstalled, hexColor);
						break;
					}

					default:
					{
						var msg = Msg(player.UserIDString, Help);

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan != null)
						{
							if (clan.IsModerator(player.userID))
								msg += Msg(player.UserIDString, ModerHelp);

							if (clan.IsOwner(player.userID))
								msg += Msg(player.UserIDString, AdminHelp);
						}

						SendReply(player, msg);
						break;
					}
				}
			}
		}

		private void CmdAllyFF(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!_config.AllianceSettings.Enabled || !_config.AllianceSettings.UseFF) return;

			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return;

			bool value;
			if (_config.AllianceSettings.GeneralFriendlyFire)
			{
				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null) return;

				if (!_config.AllianceSettings.PlayersGeneralFF)
				{
					if (_config.AllianceSettings.ModersGeneralFF && !clan.IsModerator(player.userID))
					{
						Reply(player, NotModer);
						return;
					}

					if (!clan.IsOwner(player.userID))
					{
						Reply(player, NotClanLeader);
						return;
					}
				}

				clan.AllyFriendlyFire = !clan.AllyFriendlyFire;
				value = clan.AllyFriendlyFire;
			}
			else
			{
				data.AllyFriendlyFire = !data.AllyFriendlyFire;
				value = data.AllyFriendlyFire;
			}

			Reply(player, value ? AllyFFOn : AllyFFOff);
		}

		private void CmdClanFF(IPlayer cov, string command, string[] args)
		{
#if TESTING
			using (new StopwatchWrapper("cmd ff"))
#endif
			{
				var player = cov?.Object as BasePlayer;
				if (player == null) return;

				var data = PlayerData.GetOrCreate(player.UserIDString);
				if (data == null) return;

				bool value;

				if (_config.FriendlyFire.GeneralFriendlyFire)
				{
					var clan = FindClanByPlayer(player.UserIDString);
					if (clan == null) return;

					if (!_config.FriendlyFire.PlayersGeneralFF)
					{
						if (_config.FriendlyFire.ModersGeneralFF && !clan.IsModerator(player.userID))
						{
							Reply(player, NotModer);
							return;
						}

						if (!clan.IsOwner(player.userID))
						{
							Reply(player, NotClanLeader);
							return;
						}
					}

					clan.FriendlyFire = !clan.FriendlyFire;
					value = clan.FriendlyFire;
				}
				else
				{
					data.FriendlyFire = !data.FriendlyFire;
					value = data.FriendlyFire;
				}

				Reply(player, value ? FFOn : FFOff);
			}
		}

		private void CmdAdminClans(IPlayer cov, string command, string[] args)
		{
#if TESTING
			using (new StopwatchWrapper("cmd admin"))
#endif
			{
				if (!(cov.IsServer || cov.HasPermission(PermAdmin))) return;

				if (args.Length == 0)
				{
					var sb = new StringBuilder();
					sb.AppendLine("Clans management help:");
					sb.AppendLine($"{command} list - lists all clans, their owners and their member-count");
					sb.AppendLine($"{command} listex - lists all clans, their owners/members and their on-line status");
					sb.AppendLine(
						$"{command} show [name/userId] - lists the chosen clan (or clan by user) and the members with status");
					sb.AppendLine($"{command} msg [clanTag] [message] - sends a clan message");

					sb.AppendLine($"{command} create [name/userId] [clanTag] - creates a clan");
					sb.AppendLine($"{command} rename [oldTag] [newTag] - renames a clan");
					sb.AppendLine($"{command} disband [clanTag] - disbands a clan");

					sb.AppendLine($"{command} invite [clanTag] [name/userId] - sends clan invitation to a player");
					sb.AppendLine($"{command} join [clanTag] [name/userId] - joins a player into a clan");
					sb.AppendLine($"{command} kick [clanTag] [name/userId] - kicks a member from a clan");
					sb.AppendLine($"{command} owner [clanTag] [name/userId] - sets a new owner");
					sb.AppendLine($"{command} promote [clanTag] [name/userId] - promotes a member");
					sb.AppendLine($"{command} demote [clanTag] [name/userId] - demotes a member");

					cov.Reply(sb.ToString());
					return;
				}

				switch (args[0].ToLower())
				{
					case "list":
					{
						var textTable = new TextTable();
						textTable.AddColumn("Tag");
						textTable.AddColumn("Owner");
						textTable.AddColumn("SteamID");
						textTable.AddColumn("Count");
						textTable.AddColumn("On");

						_clansList.ForEach(clan =>
						{
							if (clan == null) return;

							textTable.AddRow(clan.ClanTag ?? "UNKNOWN", clan.LeaderName ?? "UNKNOWN",
								clan.LeaderID.ToString(),
								clan.Members?.Count.ToString() ?? "UNKNOWN",
								clan.Players?.Count().ToString() ?? "UNKNOWN");
						});

						cov.Reply("\n>> Current clans <<\n" + textTable);
						break;
					}

					case "listex":
					{
						var textTable = new TextTable();
						textTable.AddColumn("Tag");
						textTable.AddColumn("Role");
						textTable.AddColumn("Name");
						textTable.AddColumn("SteamID");
						textTable.AddColumn("Status");

						_clansList.ForEach(clan =>
						{
							clan.Members.ForEach(member =>
							{
								var role = clan.IsOwner(member) ? "leader" :
									clan.IsModerator(member) ? "moderator" : "member";

								textTable.AddRow(clan.ClanTag ?? "UNKNOWN", role,
									GetPlayerName(member) ?? "UNKNOWN",
									member.ToString(),
									RelationshipManager.FindByID(member) != null ? "Online" : "Offline");
							});

							textTable.AddRow();
						});

						cov.Reply("\n>> Current clans with members <<\n" + textTable);
						break;
					}

					case "show":
					{
						if (args.Length < 2)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [name/userId]");
							return;
						}

						var clan = FindClanByTag(args[1]);
						if (clan == null)
						{
							var player = BasePlayer.FindAwakeOrSleeping(args[1]);
							if (player != null) clan = FindClanByPlayer(player.UserIDString);
						}

						if (clan == null)
						{
							cov.Reply($"Clan/Member's clan ({args[1]}) not found!");
							return;
						}

						var sb = new StringBuilder();
						sb.AppendLine($"\n>> Show clan [{clan.ClanTag}] <<");
						sb.AppendLine($"Description: {clan.Description}");
						sb.AppendLine($"Time created: {clan.CreationTime}");
						sb.AppendLine($"Last online: {clan.LastOnlineTime}");
						sb.AppendLine($"Member count: {clan.Members.Count}");

						var textTable = new TextTable();
						textTable.AddColumn("Role");
						textTable.AddColumn("Name");
						textTable.AddColumn("SteamID");
						textTable.AddColumn("Status");
						sb.AppendLine();

						clan.Members.ForEach(member =>
						{
							var role = clan.IsOwner(member) ? "leader" :
								clan.IsModerator(member) ? "moderator" : "member";

							textTable.AddRow(role, GetPlayerName(member) ?? "UNKNOWN",
								member.ToString(),
								RelationshipManager.FindByID(member) != null ? "Online" : "Offline");
						});

						sb.AppendLine(textTable.ToString());

						cov.Reply(sb.ToString());
						cov.Reply($"Allied Clans: {clan.Alliances.ToSentence()}");
						break;
					}

					case "msg":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag] [message]");
							return;
						}

						var broadcastClan = TryBroadcastClan(args[1], args[2]);
						if (broadcastClan != "success")
							cov.Reply(broadcastClan);
						break;
					}

					case "create":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [name/userId] [clanTag]");
							return;
						}

						var createClan = TryRenameClan(args[1], args[2]);
						cov.Reply(createClan == "success"
							? $"You created the clan {args[1]} and set {args[2]} as the owner"
							: createClan);
						break;
					}

					case "rename":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [oldTag] [newTag]");
							return;
						}

						var renameClan = TryRenameClan(args[1], args[2]);
						cov.Reply(renameClan == "success"
							? $"The clan {args[1]} was renamed to {args[2]}"
							: renameClan);
						break;
					}

					case "join":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag] [name/userId]");
							return;
						}

						var joinPlayerToClan = TryJoinPlayerToClan(args[1], args[2]);
						if (joinPlayerToClan != "success") cov.Reply(joinPlayerToClan);
						break;
					}

					case "kick":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag] [name/userId]");
							return;
						}

						var kickPlayerFromClan = TryKickPlayerFromClan(args[1], args[2]);
						if (kickPlayerFromClan != "success") cov.Reply(kickPlayerFromClan);
						break;
					}

					case "kick.player":
					{
						if (args.Length < 2)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [name/userId]");
							return;
						}

						if (!ulong.TryParse(args[1], out var target))
						{
							cov.Reply($"{args[1]} is not a steamid!");
							return;
						}

						var kickPlayerFromAnyClan = TryKickPlayerFromAnyClan(target);
						if (kickPlayerFromAnyClan != "success") cov.Reply(kickPlayerFromAnyClan);
						break;
					}

					case "owner":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag] [name/userId]");
							return;
						}

						var setClanOwner = TrySetClanOwner(args[1], args[2]);
						if (setClanOwner != "success") cov.Reply(setClanOwner);
						break;
					}

					case "invite":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag] [name/userId]");
							return;
						}

						var sendInviteToClan = TrySendInviteToClan(args[1], args[2]);
						if (sendInviteToClan != "success") cov.Reply(sendInviteToClan);
						break;
					}

					case "promote":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag] [name/userId]");
							return;
						}

						var promoteClanMember = TryPromoteClanMember(args[1], args[2]);
						if (promoteClanMember != "success") cov.Reply(promoteClanMember);
						break;
					}

					case "demote":
					{
						if (args.Length < 3)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag] [name/userId]");
							return;
						}

						var demoteClanMember = TryDemoteClanMember(args[1], args[2]);
						if (demoteClanMember != "success") cov.Reply(demoteClanMember);
						break;
					}

					case "disband":
					{
						if (args.Length < 2)
						{
							cov.Reply($"Error syntax! Use: /{command} {args[0]} [clanTag]");
							return;
						}


						var disbandClan = TryDisbandClan(args[1]);
						cov.Reply(disbandClan == "success" ? $"Successfully disbanded clan {args[1]}" : disbandClan);
						break;
					}
				}
			}
		}

		private void CmdClanInfo(IPlayer cov, string command, string[] args)
		{
#if TESTING
			using (new StopwatchWrapper("cmd clan info"))
#endif
			{
				var player = cov?.Object as BasePlayer;
				if (player == null) return;

				if (player.net.connection.authLevel < _config.PermissionSettings.ClanInfoAuthLevel)
				{
					Reply(player, NoPermissions);
					return;
				}

				if (args.Length < 1)
				{
					SendReply(player, $"Error syntax! Use: /{command} <clan tag>");
					return;
				}

				var targetClan = FindClanByTag(args[0]);
				if (targetClan == null)
				{
					Reply(player, ClanNotFound, args[0]);
					return;
				}

				SendReply(player, targetClan.GetClanInfo(player));
			}
		}

		private void ClanChatClan(IPlayer cov, string command, string[] args)
		{
#if TESTING
			using (new StopwatchWrapper("cmd clan chat"))
#endif
			{
				var player = cov?.Object as BasePlayer;
				if (player == null) return;

				if (args.Length == 0)
				{
					Msg(player.UserIDString, ClanChatSyntax, command);
					return;
				}

				var msg = string.Join(" ", args);
				if (string.IsNullOrEmpty(msg))
				{
					Msg(player.UserIDString, ClanChatSyntax, command);
					return;
				}

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null)
				{
					Msg(player.UserIDString, NotMemberOfClan);
					return;
				}

				var str = Msg(player.UserIDString, ClanChatFormat, clan.ClanTag, clan.GetRoleColor(player.UserIDString), cov.Name, msg);
				if (string.IsNullOrEmpty(str)) return;

				clan.Broadcast(ClanChatPrefix, str);

				Interface.CallHook("OnClanChat", player, str, clan.ClanTag);
			}
		}

		private void ClanChatAlly(IPlayer cov, string command, string[] args)
		{
#if TESTING
			using (new StopwatchWrapper("cmd ally chat"))
#endif
			{
				var player = cov?.Object as BasePlayer;
				if (player == null) return;

				if (args.Length == 0)
				{
					Msg(player.UserIDString, AllyChatSyntax, command);
					return;
				}

				var msg = string.Join(" ", args);
				if (string.IsNullOrEmpty(msg))
				{
					Msg(player.UserIDString, AllyChatSyntax, command);
					return;
				}

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null)
				{
					Msg(player.UserIDString, NotMemberOfClan);
					return;
				}

				var str = Msg(player.UserIDString, AllyChatFormat, clan.ClanTag, clan.GetRoleColor(player.UserIDString),
					cov.Name, msg);
				if (string.IsNullOrEmpty(str)) return;

				clan.Broadcast(AllyChatPrefix, str);

				clan.Alliances?.Select(FindClanByTag)?.ForEach(allyClan => allyClan?.Broadcast(AllyChatPrefix, str));

				Interface.CallHook("OnAllianceChat", player, str, clan.ClanTag);
			}
		}

		[ConsoleCommand("UI_Clans")]
		private void CmdConsoleClans(ConsoleSystem.Arg arg)
		{
#if TESTING
			using (new StopwatchWrapper("cmd UI_Clans"))
#endif
			{
				var player = arg?.Player();
				if (player == null || !arg.HasArgs()) return;

#if TESTING
				try
				{
#endif

				switch (arg.Args[0])
				{
					case "close_ui":
					{
						ClanCreating.Remove(player.userID);
						break;
					}

					case "close":
					{
						_openedUI.Remove(player.userID);
						break;
					}

					case "page":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[1], out var page))
						{
#if TESTING
								PrintError("Invalid input for page");
#endif
							return;
						}

						var localPage = 0;
						if (arg.HasArgs(3) && !int.TryParse(arg.Args[2], out localPage))
						{
#if TESTING
								PrintError($"Invalid input for zPage: {arg.Args[2]}");
#endif
						}

						var search = string.Empty;
						if (arg.HasArgs(4))
						{
							search = string.Join(" ", arg.Args.Skip(3));

							if (string.IsNullOrEmpty(search) || search.Equals(Msg(player.UserIDString, EnterLink)))
							{
#if TESTING
									PrintError($"Invalid input for search: {search}");
#endif
								return;
							}
						}

#if TESTING
							Puts("MainUi method called with parameters: " + page + ", " + localPage + ", " + search);
#endif

						ShowClanMainUI(player, page, localPage, search);
						break;
					}

					case "inputpage":
					{
						if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out var pages) ||
						    !int.TryParse(arg.Args[2], out var page) || !int.TryParse(arg.Args[3], out var localPage))
						{
#if TESTING
								if (!arg.HasArgs(4))
								{
									PrintError("Not enough arguments");
									return;
								}

								if (!int.TryParse(arg.Args[1], out pages))
								{
									PrintError("Invalid input for pages");
									return;
								}

								if (!int.TryParse(arg.Args[2], out page))
								{
									PrintError("Invalid input for page");
									return;
								}

								if (!int.TryParse(arg.Args[3], out localPage))
								{
									PrintError("Invalid input for localPage");
									return;
								}
#endif
							return;
						}

#if TESTING
							Puts($"Value of pages: {pages}");
							Puts($"Value of page: {page}");
							Puts($"Value of zPage: {localPage}");
#endif

						if (localPage < 0)
						{
#if TESTING
								Puts("zPage is negative, setting to 0");
#endif

							localPage = 0;
						}

						if (localPage >= pages)
						{
#if TESTING
								Puts("zPage is greater than or equal to pages, setting to pages - 1");
#endif
							localPage = pages - 1;
						}

#if TESTING
							Puts($"MainUi method called with parameters page: {page}, localPage: {localPage}");
#endif

						ShowClanMainUI(player, page, localPage);
						break;
					}

					case "invite":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						switch (arg.Args[1])
						{
							case "accept":
							{
								if (!arg.HasArgs(3))
								{
#if TESTING
										PrintError("Not enough arguments for accept");
#endif
									return;
								}

								var tag = string.Join(" ", arg.Args.Skip(2));
								if (string.IsNullOrEmpty(tag))
								{
#if TESTING
										PrintError("Invalid input for tag");
#endif
									return;
								}

#if TESTING
									Puts($"Calling AcceptInvite method with player: {player}, tag: {tag}");
#endif
								AcceptInvite(player, tag);

								_openedUI.Remove(player.userID);
								break;
							}

							case "cancel":
							{
								if (!arg.HasArgs(3))
								{
#if TESTING
										PrintError("Not enough arguments for cancel");
#endif
									return;
								}

								var tag = string.Join(" ", arg.Args.Skip(2));
								if (string.IsNullOrEmpty(tag))
								{
#if TESTING
										PrintError("Invalid input for tag");
#endif
									return;
								}
#if TESTING
									Puts($"Calling CancelInvite method with player: {player}, tag: {tag}");
#endif
								CancelInvite(player, tag);

								_openedUI.Remove(player.userID);
								break;
							}

							case "send":
							{
								if (!arg.HasArgs(3) || !ulong.TryParse(arg.Args[2], out var targetId))
								{
#if TESTING
										PrintError("Invalid input for targetId");
#endif
									return;
								}
#if TESTING
									Puts($"Calling SendInvite method with player: {player}, targetId: {targetId}");
#endif
								SendInvite(player, targetId);

#if TESTING
									Puts($"Calling MainUi method with player: {player}, page: {5}");
#endif
								ShowClanMainUI(player, 5);
								break;
							}

							case "withdraw":
							{
								if (!arg.HasArgs(3) || !ulong.TryParse(arg.Args[2], out var targetId))
								{
#if TESTING
										PrintError("Invalid input for targetId");
#endif
									return;
								}

#if TESTING
									Puts($"Calling WithdrawInvite method with player: {player}, targetId: {targetId}");
#endif
								WithdrawInvite(player, targetId);

#if TESTING
									Puts($"Calling MainUi method with player: {player}, page: {5}");
#endif
								ShowClanMainUI(player, 65);
								break;
							}
						}

						break;
					}

					case "allyinvite":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments for accept");
#endif
							return;
						}

						switch (arg.Args[1])
						{
							case "accept":
							{
								if (!arg.HasArgs(3))
								{
#if TESTING
										PrintError("Not enough arguments for accept");
#endif
									return;
								}
#if TESTING
									Puts($"Calling AllyAcceptInvite method with player: {player}, tag: {arg.Args[2]}");
#endif
								AllyAcceptInvite(player, arg.Args[2]);

								_openedUI.Remove(player.userID);
								break;
							}

							case "cancel":
							{
								if (!arg.HasArgs(3))
								{
#if TESTING
										PrintError("Not enough arguments for cancel");
#endif
									return;
								}
#if TESTING
									Puts($"Calling AllyCancelInvite method with player: {player}, tag: {arg.Args[2]}");
#endif
								AllyCancelInvite(player, arg.Args[2]);

								_openedUI.Remove(player.userID);
								break;
							}

							case "send":
							{
								if (!arg.HasArgs(3))
								{
#if TESTING
										PrintError("Not enough arguments for send");
#endif
									return;
								}
#if TESTING
									Puts($"Calling AllySendInvite method with player: {player}, tag: {arg.Args[2]}");
#endif

								AllySendInvite(player, arg.Args[2]);

#if TESTING
									Puts($"Calling MainUi method with player: {player}, page: {71}");
#endif
								ShowClanMainUI(player, 71);
								break;
							}

							case "withdraw":
							{
								if (!arg.HasArgs(3))
								{
#if TESTING
										PrintError("Not enough arguments for withdraw");
#endif
									return;
								}

#if TESTING
									Puts(
										$"Calling AllyWithdrawInvite method with player: {player}, tag: {arg.Args[2]}");
#endif

								AllyWithdrawInvite(player, arg.Args[2]);

#if TESTING
									Puts($"Calling MainUi method with player: {player}, page: {71}");
#endif
								ShowClanMainUI(player, 71);
								break;
							}

							case "revoke":
							{
								if (!arg.HasArgs(3))
								{
#if TESTING
										PrintError("Not enough arguments for revoke");
#endif
									return;
								}
#if TESTING
									Puts($"Calling AllyRevoke method with player: {player}, tag: {arg.Args[2]}");
#endif
								AllyRevoke(player, arg.Args[2]);

#if TESTING
									Puts($"Calling MainUi method with player: {player}, page: {7}");
#endif
								ShowClanMainUI(player, 7);
								break;
							}
						}

						break;
					}

					case "createclan":
					{
						if (arg.HasArgs(2))
							switch (arg.Args[1])
							{
								case "name":
								{
									if (!arg.HasArgs(3)) return;

									var tag = string.Join(" ", arg.Args.Skip(2));
									if (string.IsNullOrEmpty(tag) || tag.Length < _config.Tags.TagMin ||
									    tag.Length > _config.Tags.TagMax)
									{
#if TESTING
											Puts(
												$"Calling Reply method with player: {player}, message: {ClanTagLimit}, tagMin: {_config.Tags.TagMin}, tagMax: {_config.Tags.TagMax}");
#endif
										Reply(player, ClanTagLimit, _config.Tags.TagMin, _config.Tags.TagMax);
										return;
									}

									if (ClanCreating.TryGetValue(player.userID, out var creatingData))
									{
										var oldTag = creatingData.Tag;
										if (!string.IsNullOrEmpty(oldTag) && oldTag.Equals(tag))
										{
#if TESTING
												PrintError("Old tag equals new tag");
#endif
											return;
										}
									}

#if TESTING
										Puts($"Setting tag for player {player} to {tag}");
#endif
									ClanCreating[player.userID].Tag = tag;
									break;
								}

								case "avatar":
								{
									if (!arg.HasArgs(3))
									{
#if TESTING
											PrintError("Not enough arguments");
#endif
										return;
									}

									var avatar = string.Join(" ", arg.Args.Skip(2));
									if (string.IsNullOrEmpty(avatar))
									{
#if TESTING
											PrintError("Avatar is null or empty");
#endif
										return;
									}

									if (ClanCreating.TryGetValue(player.userID, out var creatingData))
									{
										var oldAvatar = creatingData.Avatar;
										if (!string.IsNullOrEmpty(oldAvatar))
											if (oldAvatar.Equals(Msg(player.UserIDString, UrlTitle)) ||
											    oldAvatar.Equals(avatar))
											{
#if TESTING
													PrintError("Old avatar equals new avatar or UrlTitle");
#endif
												return;
											}
									}

									if (!IsValidURL(avatar))
									{
#if TESTING
											PrintError("Avatar URL is invalid");
#endif
										return;
									}
#if TESTING
										Puts($"Setting avatar for player {player} to {avatar}");
#endif
									ClanCreating[player.userID].Avatar = avatar;
									break;
								}

								case "create":
								{
									if (!ClanCreating.TryGetValue(player.userID, out var clanCreatingData)) return;

									var clanTag = clanCreatingData.Tag;
									if (string.IsNullOrEmpty(clanTag))
									{
										if (_config.ForceClanCreateTeam)
										{
#if TESTING
												Puts($"Calling CreateClanUi method with player: {player}");
#endif
											ShowClanCreationUI(player);
										}
										else
										{
											ClanCreating.Remove(player.userID);
										}

										return;
									}

									if (_config.PermissionSettings.UsePermClanCreating &&
									    !string.IsNullOrEmpty(_config.PermissionSettings.ClanCreating) &&
									    !player.HasPermission(_config.PermissionSettings.ClanCreating))
									{
#if TESTING
											Puts(
												$"Calling Reply method with player: {player}, message: {NoPermCreateClan}");
#endif

										Reply(player, NoPermCreateClan);

										if (_config.ForceClanCreateTeam)
										{
#if TESTING
												Puts($"Calling CreateClanUi method with player: {player}");
#endif
											ShowClanCreationUI(player);
										}
										else
										{
											ClanCreating.Remove(player.userID);
										}

										return;
									}

									if (_config.PaidFunctionality.ChargeFeeToCreateClan &&
									    !_config.PaidFunctionality.Economy.RemoveBalance(player,
										    _config.PaidFunctionality.CostCreatingClan))
									{
										Reply(player, NotMoney);

										ClanCreating.Remove(player.userID);
										return;
									}

									var clan = FindClanByTag(clanTag);
									if (clan != null)
									{
#if TESTING
											Puts($"Calling Reply method with player: {player}, message: {ClanExists}");
#endif

										Reply(player, ClanExists);

										if (_config.ForceClanCreateTeam)
										{
#if TESTING
												Puts($"Calling CreateClanUi method with player: {player}");
#endif

											ShowClanCreationUI(player);
										}
										else
										{
											ClanCreating.Remove(player.userID);
										}

										return;
									}

									var checkTag = clanTag.Replace(" ", "");
									if (_config.Tags.BlockedWords.Exists(word =>
										    checkTag.Contains(word, CompareOptions.OrdinalIgnoreCase)))
									{
#if TESTING
											Puts(
												$"Calling Reply method with player: {player}, message: {ContainsForbiddenWords}");
#endif
										Reply(player, ContainsForbiddenWords);

										if (_config.ForceClanCreateTeam)
										{
#if TESTING
												Puts($"Calling CreateClanUi method with player: {player}");
#endif
											ShowClanCreationUI(player);
										}
										else
										{
											ClanCreating.Remove(player.userID);
										}

										return;
									}

									if (_config.Tags.CheckingCharacters && _tagFilter.IsMatch(checkTag))
									{
#if TESTING
											Puts(
												$"Calling Reply method with player: {player}, message: {ContainsForbiddenWords}");
#endif
										Reply(player, ContainsForbiddenWords);
										ClanCreating.Remove(player.userID);
										return;
									}

									clan = ClanData.CreateNewClan(clanTag, player);
									if (clan == null)
									{
										if (_config.ForceClanCreateTeam)
										{
#if TESTING
												Puts($"Calling CreateClanUi method with player: {player}");
#endif
											ShowClanCreationUI(player);
										}
										else
										{
											ClanCreating.Remove(player.userID);
										}

										return;
									}

									var avatar = clanCreatingData.Avatar;
									if (!string.IsNullOrEmpty(avatar) &&
									    !avatar.Equals(Msg(player.UserIDString, UrlTitle)) &&
									    IsValidURL(avatar)) 
										AddImage(avatar, $"clanavatar_{clanTag}");

									ClanCreating.Remove(player.userID);

#if TESTING
										Puts(
											$"Calling Reply method with player: {player}, message: {ClanCreated}, clanTag: {clanTag}");
#endif

									Reply(player, ClanCreated, clanTag);
									return;
								}
							}

#if TESTING
							Puts($"Calling CreateClanUi method with player: {player}");
#endif

						ShowClanCreationUI(player);
						break;
					}

					case "edititem":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError($"Not enough arguments: {arg.Args.Length}");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[1], out var slot))
						{
#if TESTING
								PrintError($"Could not parse slot from argument {arg.Args[1]}");
#endif
							return;
						}

#if TESTING
							Puts($"Calling SelectItemUi method with player: {player}, slot: {slot}");
#endif
						ShowSelectItemUI(player, slot);
						break;
					}

					case "selectpages":
					{
						if (!arg.HasArgs(4))
						{
#if TESTING
								PrintError($"Not enough arguments: {arg.Args.Length}");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[1], out var slot))
						{
#if TESTING
								PrintError($"Could not parse slot from argument {arg.Args[1]}");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[2], out var page))
						{
#if TESTING
								PrintError($"Could not parse page from argument {arg.Args[2]}");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[3], out var amount))
						{
#if TESTING
								PrintError($"Could not parse amount from argument {arg.Args[3]}");
#endif
							return;
						}

						var search = string.Empty;
						if (arg.HasArgs(5))
							search = string.Join(" ", arg.Args.Skip(4));

#if TESTING
							Puts(
								$"Calling SelectItemUi with player: {player}, slot: {slot}, page: {page}, amount: {amount}, search: {search}");
#endif
						ShowSelectItemUI(player, slot, page, amount, search);
						break;
					}

					case "setamountitem":
					{
						if (!arg.HasArgs(3))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[1], out var slot))
						{
#if TESTING
								PrintError($"Could not parse slot from argument {arg.Args[1]}");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[2], out var amount))
						{
#if TESTING
								PrintError($"Could not parse amount from argument {arg.Args[2]}");
#endif
							return;
						}

						if (amount <= 0)
						{
#if TESTING
								PrintWarning("Amount should be greater than 0");
#endif
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null || !clan.IsOwner(player.userID))
						{
#if TESTING
								PrintWarning("Player is not a clan owner or clan not found");
#endif
							return;
						}

						if (clan.ResourceStandarts.TryGetValue(slot, out var standart))
							standart.Amount = amount;

#if TESTING
							Puts($"Calling SelectItemUi with player: {player}, slot: {slot}, amount: {amount}");
#endif
						ShowSelectItemUI(player, slot, amount: amount);
						break;
					}

					case "selectitem":
					{
						if (!arg.HasArgs(4))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[1], out var slot))
						{
#if TESTING
								PrintError($"Could not parse slot from argument {arg.Args[1]}");
#endif
							return;
						}

						if (!int.TryParse(arg.Args[3], out var amount))
						{
#if TESTING
								PrintError($"Could not parse amount from argument {arg.Args[3]}");
#endif
							return;
						}

						var shortName = arg.Args[2];
						if (string.IsNullOrEmpty(shortName))
						{
#if TESTING
								PrintError("Short name is null or empty");
#endif
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null || !clan.IsOwner(player.userID))
						{
#if TESTING
								PrintWarning("Player is not a clan owner or clan not found");
#endif
							return;
						}

#if TESTING
							Puts(
								$"Setting resource standart for clan '{clan.ClanTag}' and slot '{slot}' with amount: '{amount}', shortName '{shortName}'");
#endif
						clan.ResourceStandarts[slot] = new ResourceStandart
						{
							Amount = amount,
							ShortName = shortName
						};

#if TESTING
							Puts($"Calling MainUi method with player: {player}, page: {5}");
#endif
						ShowClanMainUI(player, 4);
						break;
					}

					case "editskin":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						var page = 0;
						if (arg.HasArgs(3))
							if (!int.TryParse(arg.Args[2], out page))
							{
#if TESTING
									PrintWarning($"Could not parse page from argument {arg.Args[2]}");
#endif
							}

						var shortName = arg.Args[1];
						if (page == -1)
						{
							if (_config.PagesConfig?.StayOnPageWithSelectedSkin == true)
							{
								var clan = FindClanByPlayer(player.UserIDString);

								if (clan.Skins.TryGetValue(shortName, out var nowSkin) && nowSkin != 0UL)
									page = Mathf.FloorToInt(
										(float) _config.Skins.ItemSkins[shortName].IndexOf(nowSkin) /
										UI_SKIN_SELECTION_TOTAL_AMOUNT);
							}
							else
								page = 0;
						}

						ShowClanSkinSelectionUI(player, shortName, page);
						break;
					}

					case "setskin":
					{
						if (!arg.HasArgs(3))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						if (!ulong.TryParse(arg.Args[2], out var skin))
						{
#if TESTING
								PrintWarning($"Could not parse skin from argument {arg.Args[2]}");
#endif
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
#if TESTING
								PrintError("Clan not found");
#endif
							return;
						}

						if (_config.PaidFunctionality.ChargeFeeToSetClanSkin &&
						    !_config.PaidFunctionality.Economy.RemoveBalance(player,
							    _config.PaidFunctionality.CostSettingClanSkin))
						{
							Reply(player, PaidSetSkinMsg, _config.PaidFunctionality.CostSettingClanSkin);
							return;
						}
#if TESTING
							Puts($"Calling clan.SetSkin method with shortName: {arg.Args[1]}, skin: {skin}");
#endif
						clan.SetSkin(arg.Args[1], skin);

#if TESTING
							Puts($"Calling SelectSkinUi method with player: {player}, shortName: {arg.Args[1]}");
#endif
						ShowClanSkinSelectionUI(player, arg.Args[1]);
						break;
					}

					case "selectskin":
					{
						if (!arg.HasArgs(3))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						if (!ulong.TryParse(arg.Args[2], out var skin))
						{
#if TESTING
								PrintWarning($"Could not parse skin from argument {arg.Args[2]}");
#endif
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
#if TESTING
								PrintError("Clan not found");
#endif
							return;
						}

						if (_config.PaidFunctionality.ChargeFeeToSetClanSkin &&
						    !_config.PaidFunctionality.Economy.RemoveBalance(player,
							    _config.PaidFunctionality.CostSettingClanSkin))
						{
							Reply(player, PaidSetSkinMsg, _config.PaidFunctionality.CostSettingClanSkin);
							return;
						}
#if TESTING
							Puts($"Calling clan.SetSkin method with shortName: {arg.Args[1]}, skin: {skin}");
#endif
						clan.SetSkin(arg.Args[1], skin);

#if TESTING
							Puts($"Calling MainUi method with player: {player}, page: {6}");
#endif
						ShowClanMainUI(player, 6);
						break;
					}

					case "showprofile":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						if (!ulong.TryParse(arg.Args[1], out var target))
						{
#if TESTING
								PrintWarning($"Could not parse target from argument {arg.Args[1]}");
#endif
							return;
						}

#if TESTING
							Puts($"Calling ProfileUi method with player: {player}, target: {target}");
#endif
						ShowMemberProfileUI(player, target);
						break;
					}

					case "showclanprofile":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						if (!ulong.TryParse(arg.Args[1], out var target))
						{
#if TESTING
								PrintWarning($"Could not parse target from argument {arg.Args[1]}");
#endif
							return;
						}

#if TESTING
							Puts($"Calling ClanMemberProfileUi method with player: {player}, target: {target}");
#endif
						ShowClanMemberProfileUI(player, target);
						break;
					}

					case "moder":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
#if TESTING
								PrintError("Clan not found");
#endif
							return;
						}

						switch (arg.Args[1])
						{
							case "set":
							{
								if (!arg.HasArgs(3) || !ulong.TryParse(arg.Args[2], out var target))
								{
#if TESTING
										PrintError("Invalid input for set");
#endif
									return;
								}

								if (clan.Moderators.Count >= _config.LimitSettings.ModeratorLimit)
								{
									CuiHelper.DestroyUi(player, Layer);

									Reply(player, ALotOfModers);
									return;
								}

#if TESTING
									Puts($"Set moderator {target} for clan {clan.ClanTag}");
#endif

								clan.SetModer(target);

#if TESTING
									Puts($"Calling ClanMemberProfileUi method with player: {player}, target: {target}");
#endif
								ShowClanMemberProfileUI(player, target);
								break;
							}

							case "undo":
							{
								if (!arg.HasArgs(3) || !ulong.TryParse(arg.Args[2], out var target))
								{
#if TESTING
										PrintError("Invalid input for undo");
#endif
									return;
								}

#if TESTING
									Puts($"Removing moderator {target} from clan {clan.ClanTag}");
#endif
								clan.UndoModer(target);

#if TESTING
									Puts($"Calling ClanMemberProfileUi method with player: {player}, target: {target}");
#endif
								ShowClanMemberProfileUI(player, target);
								break;
							}

							default:
							{
#if TESTING
									PrintError("Invalid command");
#endif
								break;
							}
						}

						break;
					}

					case "leader":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
#if TESTING
								PrintError($"Clan not found for player {player.UserIDString}");
#endif
							return;
						}

						switch (arg.Args[1])
						{
							case "tryset":
							{
								if (!arg.HasArgs(3) || !ulong.TryParse(arg.Args[2], out var target))
								{
#if TESTING
										PrintError("Invalid input for tryset");
#endif
									return;
								}
#if TESTING
									Puts($"Calling AcceptSetLeader method with player: {player}, target: {target}");
#endif
								ShowClanAcceptSetLeaderUI(player, target);
								break;
							}

							case "set":
							{
								if (!arg.HasArgs(3) || !ulong.TryParse(arg.Args[2], out var target))
								{
#if TESTING
										PrintError("Invalid input for set");
#endif
									return;
								}
#if TESTING
									Puts($"Setting leader {target} for clan {clan.ClanTag}");
#endif
								clan.SetLeader(target);

#if TESTING
									Puts($"Calling ClanMemberProfileUi method with player: {player}, target: {target}");
#endif
								ShowClanMemberProfileUI(player, target);
								break;
							}

							default:
							{
#if TESTING
									PrintError("Invalid command");
#endif
								break;
							}
						}

						break;
					}

					case "kick":
					{
						if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out var target))
						{
#if TESTING
								PrintError("Invalid input");
#endif
							return;
						}
#if TESTING
							Puts($"Parsed target: {target}");
#endif
						if (_config.PermissionSettings.UsePermClanKick &&
						    !string.IsNullOrEmpty(_config.PermissionSettings.ClanKick) &&
						    !player.HasPermission(_config.PermissionSettings.ClanKick))
						{
#if TESTING
								PrintError($"Player {player.UserIDString} does not have permission to kick from clan");
#endif
							Reply(player, _config.PermissionSettings.ClanKick);
							return;
						}

						if (_config.PaidFunctionality.ChargeFeeToKickClanMember &&
						    !_config.PaidFunctionality.Economy.RemoveBalance(player,
							    _config.PaidFunctionality.CostKickingClanMember))
						{
							Reply(player, PaidKickMsg, _config.PaidFunctionality.CostKickingClanMember);
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null || !clan.IsModerator(player.UserIDString))
						{
#if TESTING
								PrintError($"Clan not found for player {player.UserIDString}");
#endif
							return;
						}

#if TESTING
							Puts($"Kicking player {target} from clan {clan.ClanTag}");
#endif
						clan.Kick(target);

#if TESTING
							Puts($"Calling MainUi method with player: {player}, page: {1}");
#endif
						ShowClanMainUI(player, 1);
						break;
					}

					case "showclan":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						var tag = arg.Args[1];
						if (string.IsNullOrEmpty(tag))
						{
#if TESTING
								PrintError("Invalid input for tag");
#endif
							return;
						}

#if TESTING
							Puts($"Calling ClanProfileUi method with player: {player}, tag: {tag}");
#endif
						ShowClanProfileUI(player, tag);
						break;
					}

					case "ff":
					{
						var data = PlayerData.GetOrCreate(player.UserIDString);
						if (data == null)
						{
#if TESTING
								PrintError($"Could not get or create player data for {player.UserIDString}");
#endif
							return;
						}

						if (_config.FriendlyFire.GeneralFriendlyFire)
						{
							var clan = data.GetClan();
							if (clan == null)
							{
#if TESTING
									PrintError($"Could not get clan for player {player.UserIDString}");
#endif
								return;
							}

							if (_config.FriendlyFire.PlayersGeneralFF ||
							    (_config.FriendlyFire.ModersGeneralFF && clan.IsModerator(player.userID)) ||
							    clan.IsOwner(player.userID))
							{
#if TESTING
									Puts($"Toggling friendly fire for clan {clan.ClanTag}");
#endif
								clan.FriendlyFire = !clan.FriendlyFire;
							}
						}
						else
						{
#if TESTING
								Puts($"Toggling friendly fire for player {player.UserIDString}");
#endif
							data.FriendlyFire = !data.FriendlyFire;
						}

#if TESTING
							Puts($"Calling ButtonFriendlyFire method with player: {player}, data: {data}");
#endif
						var container = new CuiElementContainer();
						ButtonFriendlyFire(ref container, player, data);
						CuiHelper.AddUi(player, container);
						break;
					}

					case "allyff":
					{
						var data = PlayerData.GetOrCreate(player.UserIDString);
						if (data == null)
						{
#if TESTING
								PrintError($"Could not get or create player data for {player.UserIDString}");
#endif
							return;
						}

						if (_config.AllianceSettings.GeneralFriendlyFire)
						{
							var clan = data.GetClan();
							if (clan == null)
							{
#if TESTING
									PrintError($"Could not get clan for player {player.UserIDString}");
#endif
								return;
							}

							if (_config.AllianceSettings.PlayersGeneralFF ||
							    (_config.AllianceSettings.ModersGeneralFF &&
							     clan.IsModerator(player.userID)) ||
							    clan.IsOwner(player.userID))
							{
#if TESTING
									Puts($"Toggling ally friendly fire for clan {clan.ClanTag}");
#endif
								clan.AllyFriendlyFire = !clan.AllyFriendlyFire;
							}
						}
						else
						{
#if TESTING
								Puts($"Toggling ally friendly fire for player {player.UserIDString}");
#endif
							data.AllyFriendlyFire = !data.AllyFriendlyFire;
						}

#if TESTING
							Puts($"Calling ButtonAlly method with player: {player}, data: {data}");
#endif
						var container = new CuiElementContainer();
						ButtonAlly(ref container, player, data);
						CuiHelper.AddUi(player, container);
						break;
					}

					case "description":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						var description = string.Join(" ", arg.Args.Skip(1));
						if (string.IsNullOrEmpty(description))
						{
#if TESTING
								PrintError("Invalid input to description");
#endif
							return;
						}

						if (description.Equals(Msg(player.UserIDString, NotDescription)))
						{
#if TESTING
								Puts("Description equals default message, returning");
#endif
							return;
						}

						if (description.Length > _config.DescriptionMax)
						{
#if TESTING
								Puts(
									$"Description length ({description.Length}) exceeds maximum ({_config.DescriptionMax}), returning");
#endif
							Reply(player, MaxDescriptionSize);
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
#if TESTING
								PrintError($"Clan not found for player {player.UserIDString}");
#endif
							Reply(player, NotClanMember);
							return;
						}

						if (!clan.IsOwner(player.userID))
						{
#if TESTING
								PrintError($"Player {player.UserIDString} is not the clan owner");
#endif
							Reply(player, NotClanLeader);
							return;
						}

						if (!string.IsNullOrEmpty(clan.Description) && clan.Description.Equals(description))
						{
#if TESTING
								Puts($"Clan description equals '{description}', returning");
#endif
							return;
						}

#if TESTING
							Puts($"Setting clan description to '{description}' for clan {clan.ClanTag}");
#endif
						clan.Description = description;

#if TESTING
							Puts($"Calling MainUi method with player: {player}");
#endif
						ShowClanMainUI(player);

#if TESTING
							Puts($"Calling Reply method with player: {player}, message: {SetDescription}");
#endif
						Reply(player, SetDescription);
						break;
					}

					case "clanskins":
					{
						var data = PlayerData.GetOrCreate(player.UserIDString);
						if (data == null)
						{
#if TESTING
								PrintError($"Could not get or create player data for {player.UserIDString}");
#endif
							return;
						}

						if (_config.PermissionSettings.UsePermClanSkins &&
						    !string.IsNullOrEmpty(_config.PermissionSettings.ClanSkins) &&
						    !player.HasPermission(_config.PermissionSettings.ClanSkins))
						{
#if TESTING
								PrintError($"Player {player.UserIDString} does not have permission to use clan skins");
#endif
							Reply(player, NoPermClanSkins);
							return;
						}

#if TESTING
							Puts($"Toggling clan skins for player {player.UserIDString}");
#endif
						data.ClanSkins = !data.ClanSkins;

#if TESTING
							Puts($"Calling ButtonClanSkins method with player: {player}, data: {data}");
#endif
						var container = new CuiElementContainer();
						ButtonClanSkins(ref container, player, data);
						CuiHelper.AddUi(player, container);
						break;
					}

					case "settagcolor":
					{
						if (!arg.HasArgs(2))
						{
#if TESTING
								PrintError("Not enough arguments");
#endif
							return;
						}

						var hexColor = arg.Args[1];
						if (string.IsNullOrEmpty(hexColor))
						{
#if TESTING
								PrintError("Invalid input hex color");
#endif
							return;
						}

						hexColor = hexColor.Replace("#", "");

						if (hexColor.Length < 6 || hexColor.Length > 6 || !_hexFilter.IsMatch(hexColor))
						{
#if TESTING
								PrintError("Invalid hex color format");
#endif
							Reply(player, TagColorFormat);
							return;
						}

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null || !clan.CanEditTagColor(player.userID))
						{
#if TESTING
								PrintError(
									$"Player {player.UserIDString} is not authorized to edit tag color for the clan");
#endif
							return;
						}

						var oldTagColor = clan.GetHexTagColor();
						if (!string.IsNullOrEmpty(oldTagColor) && oldTagColor.Equals(hexColor))
						{
#if TESTING
								Puts($"Current tag color for clan {clan.ClanTag} is already '{hexColor}', returning");
#endif
							return;
						}

#if TESTING
							Puts($"Setting new tag color '{hexColor}' for clan {clan.ClanTag}");
#endif
						clan.TagColor = hexColor;

#if TESTING
							Puts($"Calling MainUi method with player: {player}");
#endif
						ShowClanMainUI(player);
						break;
					}

					case "action":
					{
						if (!arg.HasArgs(3)) return;

						var clan = FindClanByPlayer(player.UserIDString);
						if (clan == null)
						{
							Reply(player, NotClanMember);
							return;
						}

						var mainAction = arg.Args[1];
						var secondAction = arg.Args[2];

						string title;
						string msg;
						switch (secondAction)
						{
							case "leave":
							{
								title = Msg(player.UserIDString, ConfirmLeaveTitle);
								msg = Msg(player.UserIDString, ConfirmLeaveMessage, clan.ClanTag);
								break;
							}

							case "avatar":
							{
								title = Msg(player.UserIDString, ConfirmAvatarTitle);
								msg = Msg(player.UserIDString, ConfirmAvatarMessage);
								break;
							}

							default:
								return;
						}

						switch (mainAction)
						{
							case "open":
							{
								ShowInputAndActionUI(player, secondAction, title, msg, string.Empty);
								break;
							}

							case "input":
							{
								ShowInputAndActionUI(player, secondAction, title, msg,
									string.Join("–", arg.Args.Skip(3)));
								break;
							}

							case "accept":
							{
								var input = arg.Args[3]?.Replace("–", " ");
								if (string.IsNullOrWhiteSpace(input)) return;

								switch (secondAction)
								{
									case "leave":
									{
										var clanTag = clan.ClanTag;
										if (string.IsNullOrWhiteSpace(clanTag) ||
										    !clanTag.Equals(input)) return;

										if (_config.PermissionSettings.UsePermClanLeave &&
										    !string.IsNullOrEmpty(_config.PermissionSettings.ClanLeave) &&
										    !player.HasPermission(_config.PermissionSettings.ClanLeave))
										{
											Reply(player, NoPermLeaveClan);
											return;
										}

										if (_config.PaidFunctionality.ChargeFeeToLeaveClan &&
										    !_config.PaidFunctionality.Economy.RemoveBalance(player,
											    _config.PaidFunctionality.CostLeavingClan))
										{
											Reply(player, PaidLeaveMsg, _config.PaidFunctionality.CostLeavingClan);
											return;
										}

										clan.Kick(player.userID);
										Reply(player, ClanLeft);

										ShowClanMainUI(player);
										break;
									}

									case "avatar":
									{
										var oldAvatar = clan.Avatar;
										if (oldAvatar.Equals(input) || !IsValidURL(input)) return;

										clan.Avatar = input;

										UpdateAvatar(clan, player, "LOADING");

#if CARBON
										AddImage(input, $"clanavatar_{clan.ClanTag}");
										
										UpdateAvatar(clan, player);
#else
										ImageLibrary?.Call("AddImage", input, $"clanavatar_{clan.ClanTag}", 0UL,
											new Action(() =>
											{
												if (!_openedUI.Contains(player.userID)) return;

												var img = GetImage($"clanavatar_{clan.ClanTag}");
												if (string.IsNullOrEmpty(img) || img == "0")
												{
													clan.Avatar = oldAvatar;
												}
												else
												{
													UpdateAvatar(clan, player);
												}
											}));
#endif
										break;
									}
								}

								break;
							}
						}

						break;
					}

					case "confirm":
					{
						if (!arg.HasArgs(3)) return;

						var mainAction = arg.Args[1];
						var secondAction = arg.Args[2];

						switch (mainAction)
						{
							case "resource":
							{
								var clan = FindClanByPlayer(player.UserIDString);
								if (clan == null)
								{
									Reply(player, NotClanMember);
									return;
								}

								var slot = arg.Args[3];

								if (!int.TryParse(slot, out var resourceSlot) || !clan.ResourceStandarts.ContainsKey(resourceSlot)) return;

								switch (secondAction)
								{
									case "open":
									{
										ShowClanConfirmResourceUI(player, mainAction,
											Msg(player.UserIDString, ConfirmResourceTitle),
											Msg(player.UserIDString, ConfirmResourceMessage), slot, string.Empty);
										break;
									}

									case "accept":
									{
										clan.ResourceStandarts.Remove(resourceSlot);

										ShowClanMainUI(player, GATHER_RATES);
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
					PrintError($"In the command 'UI_Clans' there was an error:\n{ex}");

					Debug.LogException(ex);
				}

				Puts($"Main command used with: {string.Join(", ", arg.Args)}");
#endif
			}
		}

		[ConsoleCommand("clans.loadavatars")]
		private void CmdConsoleLoadAvatars(ConsoleSystem.Arg arg)
		{
#if TESTING
			using (new StopwatchWrapper("cmd clans.loadavatars"))
#endif
			{
				if (!arg.IsAdmin) return;

				if (string.IsNullOrEmpty(_config.Avatar.SteamAPIKey))
				{
					PrintError("Steam API key is not set. Please provide a valid API key.");
					return;
				}

				StartLoadingAvatars();
			}
		}

		[ConsoleCommand("clans.refreshtop")]
		private void CmdRefreshTop(ConsoleSystem.Arg arg)
		{
#if TESTING
			using (new StopwatchWrapper("cmd clans.refreshtop"))
#endif
			{
				if (!arg.IsAdmin) return;

				HandleTop();
			}
		}

		[ConsoleCommand("clans.refreshskins")]
		private void CmdConsoleRefreshSkins(ConsoleSystem.Arg arg)
		{
#if TESTING
			using (new StopwatchWrapper("cmd clans.refreshskins"))
#endif
			{
				if (!arg.IsAdmin) return;

				foreach (var itemSkin in _config.Skins.ItemSkins)
					itemSkin.Value.Clear();

				LoadSkins();

				Puts(
					$"{_config.Skins.ItemSkins.Sum(x => x.Value.Count)} skins for {_config.Skins.ItemSkins.Count} items uploaded successfully!");
			}
		}

		[ConsoleCommand("clans.sendcmd")]
		private void SendCMD(ConsoleSystem.Arg args)
		{
#if TESTING
			using (new StopwatchWrapper("cmd clans.sendcmd"))
#endif
			{
				var player = args.Player();
				if (player == null || !args.HasArgs()) return;

				if (args.Args[0] == "chat.say")
				{
					var convertcmd = string.Join(" ", args.Args.Skip(1));

					Interface.CallHook("IOnPlayerCommand", player, convertcmd);
				}
				else
				{
					var convertcmd =
						$"{args.Args[0]}  \" {string.Join(" ", args.Args.ToList().GetRange(1, args.Args.Length - 1))}\" 0";

					player.SendConsoleCommand(convertcmd);
				}
			}
		}

		[ConsoleCommand("clans.get.top")]
		private void CmdGetTop(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;
			
			var topClans = _clansList.SkipAndTake(0, 10);
			
			var table = new StringBuilder();
			
			table.AppendLine("Top Clans:");

			for (var i = 0; i < topClans.Count; i++)
			{
				var topClan = topClans[i];
				
				var topLine = string.Empty;
				
				_config.UI.TopClansColumns.ForEach(column =>
				{
					topLine += $"   {column.GetFormat(i + 1, topClan.GetParams(column.Key))}";
				});
                
				table.AppendLine(topLine);
			}
			
			SendReply(arg, table.ToString());
		}
		
		#endregion

		#region Interface

		private const float MAIN_ABOUT_BTN_HEIGHT = 25f;
		private const float MAIN_ABOUT_BTN_MARGIN = 5f;

		private void ShowClanMainUI(BasePlayer player, int page = 0, int localPage = 0, string search = "",
			bool first = false)
		{
#if TESTING
			using (new StopwatchWrapper("main ui"))
#endif
			{
				#region Fields

				float xSwitch;
				float ySwitch;
				float height;
				float width;
				float margin;
				int amountOnString;
				int strings;
				int totalAmount;

				var data = PlayerData.GetOrCreate(player.UserIDString);

				var clan = data.GetClan();

				var container = new CuiElementContainer();

				#endregion

				#region Background

				if (first)
				{
					_openedUI.Add(player.userID);

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

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Close = Layer,
							Command = "UI_Clans close"
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
						OffsetMin = "-340 -215",
						OffsetMax = "340 220"
					},
					Image =
					{
						Color = _config.UI.Color1.Get()
					}
				}, Layer, Layer + ".Main", Layer + ".Main");

				HeaderUi(ref container, player, clan, page, Msg(player.UserIDString, ClansMenuTitle));

				MenuUi(ref container, player, page, clan);

				#region Content

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "195 0", OffsetMax = "0 -55"
					},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, Layer + ".Main", Layer + ".Second.Main", Layer + ".Second.Main");

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Second.Main", Layer + ".Content");

				// ReSharper disable PossibleNullReferenceException
				if (clan != null || page == 45 || page == 2 || page == 3)
					switch (page)
					{
						case ABOUT_CLAN:
						{
							#region Title

							container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 1", AnchorMax = "0 1",
									OffsetMin = "2.5 -30", OffsetMax = "225 0"
								},
								Text =
								{
									Text = Msg(player.UserIDString, AboutClan),
									Align = TextAnchor.MiddleLeft,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								}
							}, Layer + ".Content");

							#endregion

							ySwitch = -175;

							#region Avatar

							if (_config.UI.PageAboutClan.ShowClanAvatar)
							{
								container.Add(MenuAvatarUI(clan));

								if (_config.UI.ShowBtnChangeAvatar &&
								    (string.IsNullOrEmpty(_config.Avatar.PermissionToChange) ||
								     permission.UserHasPermission(player.UserIDString,
									     _config.Avatar.PermissionToChange)) &&
								    (
									    (_config.Avatar.CanMember && clan.IsMember(player.userID)) ||
									    (_config.Avatar.CanModerator && clan.IsModerator(player.userID)) ||
									    (_config.Avatar.CanOwner && clan.IsOwner(player.userID))
								    ))
								{
									#region Change avatar

									container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "0 1", AnchorMax = "0 1",
											OffsetMin = $"0 {ySwitch - MAIN_ABOUT_BTN_HEIGHT}",
											OffsetMax = $"140 {ySwitch}"
										},
										Text =
										{
											Text = Msg(player.UserIDString, ChangeAvatar),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-regular.ttf",
											FontSize = 12,
											Color = "1 1 1 1"
										},
										Button =
										{
											Color = _config.UI.Color2.Get(),
											Command = "UI_Clans action open avatar"
										}
									}, Layer + ".Content");

									#endregion

									ySwitch = ySwitch - MAIN_ABOUT_BTN_HEIGHT - MAIN_ABOUT_BTN_MARGIN;
								}
							}

							#endregion

							#region Leave

							if (_config.UI.ShowBtnLeave && (!_config.PermissionSettings.UsePermClanLeave ||
							                                string.IsNullOrEmpty(_config.PermissionSettings
								                                .ClanLeave) ||
							                                player.HasPermission(_config.PermissionSettings.ClanLeave)))
								container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = $"0 {ySwitch - MAIN_ABOUT_BTN_HEIGHT}",
										OffsetMax = $"140 {ySwitch}"
									},
									Text =
									{
										Text = Msg(player.UserIDString, LeaveTitle),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-regular.ttf",
										FontSize = 12,
										Color = "1 1 1 1"
									},
									Button =
									{
										Color = _config.UI.Color6.Get(),
										Command = "UI_Clans action open leave"
									}
								}, Layer + ".Content");

							#endregion

							#region Clan Name

							if (_config.UI.PageAboutClan.ShowClanName)
								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "160 -50", OffsetMax = "400 -30"
									},
									Text =
									{
										Text = $"{clan.ClanTag}",
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-bold.ttf",
										FontSize = 16,
										Color = "1 1 1 1"
									}
								}, Layer + ".Content");

							#endregion

							#region Clan Leader

							if (_config.UI.PageAboutClan.ShowClanLeader)
							{
								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "160 -105",
										OffsetMax = $"{(_config.Tags.TagColor.Enabled ? 300 : 460)} -75"
									},
									Image =
									{
										Color = _config.UI.Color3.Get()
									}
								}, Layer + ".Content", Layer + ".Clan.Leader");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "0 20"
									},
									Text =
									{
										Text = Msg(player.UserIDString, LeaderTitle),
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-regular.ttf",
										FontSize = 10,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Leader");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = "10 0", OffsetMax = "0 0"
									},
									Text =
									{
										Text = $"{clan.LeaderName}",
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-bold.ttf",
										FontSize = 12,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Leader");
							}

							#endregion

							#region Clan Tag

							if (_config.Tags.TagColor.Enabled)
							{
								var tagColor = clan.GetHexTagColor();

								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "320 -105", OffsetMax = "460 -75"
									},
									Image =
									{
										Color = _config.UI.Color3.Get()
									}
								}, Layer + ".Content", Layer + ".Clan.ClanTag");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "0 20"
									},
									Text =
									{
										Text = Msg(player.UserIDString, TagColorTitle),
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-regular.ttf",
										FontSize = 10,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.ClanTag");

								#region Line

								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 0",
										AnchorMax = "1 0",
										OffsetMin = "0 0",
										OffsetMax = "0 4"
									},
									Image =
									{
										Color = HexToCuiColor($"#{tagColor}")
									}
								}, Layer + ".Clan.ClanTag");

								#endregion

								if (clan.CanEditTagColor(player.userID))
									container.Add(new CuiElement
									{
										Parent = Layer + ".Clan.ClanTag",
										Components =
										{
											new CuiInputFieldComponent
											{
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1",
												Align = TextAnchor.MiddleCenter,
												Command = "UI_Clans settagcolor ",
												CharsLimit = 7,
												NeedsKeyboard = true,
												Text = $"#{tagColor}"
											},
											new CuiRectTransformComponent
											{
												AnchorMin = "0 0", AnchorMax = "1 1"
											}
										}
									});
								else
									container.Add(new CuiLabel
									{
										RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
										Text =
										{
											Text = $"#{tagColor}",
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 12,
											Color = "1 1 1 1"
										}
									}, Layer + ".Clan.ClanTag");
							}

							#endregion

							#region Gather

							if (_config.UI.PageAboutClan.ShowClanGather)
							{
								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "160 -165",
										OffsetMax = "460 -135"
									},
									Image =
									{
										Color = _config.UI.Color3.Get()
									}
								}, Layer + ".Content", Layer + ".Clan.Farm");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "0 20"
									},
									Text =
									{
										Text = Msg(player.UserIDString, GatherTitle),
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-regular.ttf",
										FontSize = 10,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Farm");

								var progress = clan.TotalFarm;
								if (progress > 0)
									container.Add(new CuiPanel
									{
										RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progress} 0.95"},
										Image =
										{
											Color = _config.UI.Color2.Get()
										}
									}, Layer + ".Clan.Farm", Layer + ".Clan.Farm.Progress");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "-5 0"
									},
									Text =
									{
										Text = $"{(progress > 0 ? Math.Round(progress * 100f) : 0)}%",
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 12,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Farm");
							}

							#endregion

							#region Rating

							if (_config.UI.PageAboutClan.ShowClanRating)
							{
								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "160 -225", OffsetMax = "300 -195"
									},
									Image =
									{
										Color = _config.UI.Color3.Get()
									}
								}, Layer + ".Content", Layer + ".Clan.Rating");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "0 20"
									},
									Text =
									{
										Text = Msg(player.UserIDString, RatingTitle),
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-regular.ttf",
										FontSize = 10,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Rating");

								container.Add(new CuiLabel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text =
									{
										Text = $"{clan.Top}",
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 12,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Rating");
							}

							#endregion

							#region Members

							if (_config.UI.PageAboutClan.ShowClanMembers)
							{
								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "320 -225", OffsetMax = "460 -195"
									},
									Image =
									{
										Color = _config.UI.Color3.Get()
									}
								}, Layer + ".Content", Layer + ".Clan.Members");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "0 20"
									},
									Text =
									{
										Text = Msg(player.UserIDString, MembersTitle),
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-regular.ttf",
										FontSize = 10,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Members");

								container.Add(new CuiLabel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text =
									{
										Text = $"{clan.Members.Count}",
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 12,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Members");
							}

							#endregion

							#region Clan Description

							if (_config.UI.PageAboutClan.ShowClanDescription)
							{
								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "0 0",
										OffsetMin = "0 10", OffsetMax = "460 90"
									},
									Image =
									{
										Color = _config.UI.Color3.Get()
									}
								}, Layer + ".Content", Layer + ".Clan.Description");

								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "0 20"
									},
									Text =
									{
										Text = Msg(player.UserIDString, DescriptionTitle),
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-regular.ttf",
										FontSize = 10,
										Color = "1 1 1 1"
									}
								}, Layer + ".Clan.Description");

								if (clan.IsOwner(player.userID))
									container.Add(new CuiElement
									{
										Parent = Layer + ".Clan.Description",
										Components =
										{
											new CuiInputFieldComponent
											{
												FontSize = 12,
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												Command = "UI_Clans description ",
												Color = "1 1 1 0.85",
												CharsLimit = _config.DescriptionMax,
												NeedsKeyboard = true,
												HudMenuInput = true,
												Text = string.IsNullOrEmpty(clan.Description)
													? Msg(player.UserIDString, NotDescription)
													: $"{clan.Description}",
												LineType = InputField.LineType.MultiLineNewline
											},
											new CuiRectTransformComponent
											{
												AnchorMin = "0 0", AnchorMax = "1 1",
												OffsetMin = "5 5", OffsetMax = "-5 -5"
											}
										}
									});
								else
									container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "1 1",
											OffsetMin = "5 5", OffsetMax = "-5 -5"
										},
										Text =
										{
											Text = string.IsNullOrEmpty(clan.Description)
												? Msg(player.UserIDString, NotDescription)
												: $"{clan.Description}",
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 12,
											Color = "1 1 1 0.85",
											VerticalOverflow = VerticalWrapMode.Overflow
										}
									}, Layer + ".Clan.Description");
							}

							#endregion

							break;
						}

						case MEMBERS_LIST:
						{
							amountOnString = _config.UI.PageMembers.MembersOnLine;
							strings = _config.UI.PageMembers.MaxLines;
							totalAmount = amountOnString * strings;
							ySwitch = -_config.UI.PageMembers.UpIndent;
							height = _config.UI.PageMembers.Height;
							width = _config.UI.PageMembers.Width;
							margin = _config.UI.PageMembers.Margin;

							var availablePlayers = FindAvailablePlayers(search, clan);

							var members = availablePlayers.SkipAndTake(localPage * totalAmount, totalAmount);
							for (var z = 0; z < members.Count; z++)
							{
								xSwitch = (z + 1) % amountOnString == 0
									? margin + width
									: 0;

								var member = members[z];

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
											Color = _config.UI.Color3.Get()
										}
									}, Layer + ".Content", Layer + $".Player.{member}");

								container.Add(new CuiElement
								{
									Parent = Layer + $".Player.{member}",
									Components =
									{
										new CuiRawImageComponent
											{Png = GetImage( $"avatar_{member}")},
										new CuiRectTransformComponent
										{
											AnchorMin = "0 0", AnchorMax = "0 0",
											OffsetMin = "0 0", OffsetMax = $"{height} {height}"
										}
									}
								});

								#region Display Name

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0.5", AnchorMax = "0 1",
											OffsetMin = "40 1",
											OffsetMax = "95 0"
										},
										Text =
										{
											Text = Msg(player.UserIDString, NameTitle),
											Align = TextAnchor.LowerLeft,
											Font = "robotocondensed-regular.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member}");

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "0 0.5",
											OffsetMin = "40 0",
											OffsetMax = "100 -1"
										},
										Text =
										{
											Text = $"{GetPlayerName(member)}",
											Align = TextAnchor.UpperLeft,
											Font = "robotocondensed-bold.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member}");

								#endregion

								#region SteamId

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0.5", AnchorMax = "0 1",
											OffsetMin = "95 1",
											OffsetMax = "210 0"
										},
										Text =
										{
											Text = Msg(player.UserIDString, SteamIdTitle),
											Align = TextAnchor.LowerLeft,
											Font = "robotocondensed-regular.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member}");

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "0 0.5",
											OffsetMin = "95 0",
											OffsetMax = "210 -1"
										},
										Text =
										{
											Text = $"{member}",
											Align = TextAnchor.UpperLeft,
											Font = "robotocondensed-bold.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member}");

								#endregion

								#region Button

								container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "1 0.5", AnchorMax = "1 0.5",
											OffsetMin = "-45 -8", OffsetMax = "-5 8"
										},
										Text =
										{
											Text = Msg(player.UserIDString, ProfileTitle),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-regular.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										},
										Button =
										{
											Color = _config.UI.Color2.Get(),
											Command = $"UI_Clans showclanprofile {member}"
										}
									}, Layer + $".Player.{member}");

								#endregion

								if ((z + 1) % amountOnString == 0) ySwitch = ySwitch - height - margin;
							}

							#region Search

							container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0.5 0", AnchorMax = "0.5 0",
									OffsetMin = "-140 20",
									OffsetMax = "60 55"
								},
								Image =
								{
									Color = _config.UI.Color4.Get()
								}
							}, Layer + ".Content", Layer + ".Search");

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
										Command = $"UI_Clans page {page} 0 ",
										Color = "1 1 1 0.65",
										CharsLimit = 32,
										NeedsKeyboard = true,
										Text = string.IsNullOrEmpty(search)
											? Msg(player.UserIDString, SearchTitle)
											: $"{search}"
									},
									new CuiRectTransformComponent
									{
										AnchorMin = "0 0", AnchorMax = "1 1"
									}
								}
							});

							#endregion

							#region Pages

							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0.5 0", AnchorMax = "0.5 0",
									OffsetMin = "65 20",
									OffsetMax = "100 55"
								},
								Text =
								{
									Text = Msg(player.UserIDString, BackPage),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = _config.UI.Color4.Get(),
									Command = localPage != 0 ? $"UI_Clans page {page} {localPage - 1} {search}" : ""
								}
							}, Layer + ".Content");

							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0.5 0", AnchorMax = "0.5 0",
									OffsetMin = "105 20",
									OffsetMax = "140 55"
								},
								Text =
								{
									Text = Msg(player.UserIDString, NextPage),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = _config.UI.Color2.Get(),
									Command = availablePlayers.Count > (localPage + 1) * totalAmount
										? $"UI_Clans page {page} {localPage + 1} {search}"
										: ""
								}
							}, Layer + ".Content");

							#endregion


							break;
						}

						case CLANS_TOP:
						{
							#region Title

							container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 1", AnchorMax = "0 1",
									OffsetMin = "2.5 -30", OffsetMax = "225 0"
								},
								Text =
								{
									Text = Msg(player.UserIDString, TopClansTitle),
									Align = TextAnchor.MiddleLeft,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								}
							}, Layer + ".Content");

							#endregion

							#region Head

							ySwitch = 0;

							_config.UI.TopClansColumns.ForEach(column =>
							{
								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = $"{ySwitch} -50", OffsetMax = $"{ySwitch + column.Width} -30"
									},
									Text =
									{
										Text = Msg(player.UserIDString, column.LangKey),
										Align = column.TextAlign,
										Font = "robotocondensed-regular.ttf",
										FontSize = column.TitleFontSize,
										Color = "1 1 1 1"
									}
								}, Layer + ".Content");

								ySwitch += column.Width;
							});

							#endregion

							#region Table

							ySwitch = -_config.UI.PageClansTop.UpIndent;
							height = _config.UI.PageClansTop.Height;
							margin = _config.UI.PageClansTop.Margin;
							totalAmount = _config.UI.PageClansTop.ClansOnPage;

							var topClans = _clansList.SkipAndTake(localPage * totalAmount, totalAmount);
							for (var i = 0; i < topClans.Count; i++)
							{
								var topClan = topClans[i];

								var top = localPage * totalAmount + i + 1;

								container.Add(new CuiPanel
									{
										RectTransform =
										{
											AnchorMin = "0 1", AnchorMax = "0 1",
											OffsetMin = $"0 {ySwitch - height}",
											OffsetMax = $"{_config.UI.PageClansTop.Width} {ySwitch}"
										},
										Image =
										{
											Color = _config.UI.Color3.Get()
										}
									}, Layer + ".Content", Layer + $".TopClan.{i}", Layer + $".TopClan.{i}");

								var localSwitch = 0f;
								_config.UI.TopClansColumns.ForEach(column =>
								{
									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 1",
												OffsetMin = $"{localSwitch} 0",
												OffsetMax = $"{localSwitch + column.Width} 0"
											},
											Text =
											{
												Text = $"{column.GetFormat(top, topClan.GetParams(column.Key))}",
												Align = column.TextAlign,
												Font = "robotocondensed-bold.ttf",
												FontSize = column.FontSize,
												Color = "1 1 1 1"
											}
										}, Layer + $".TopClan.{i}");

									localSwitch += column.Width;
								});

								container.Add(new CuiButton
									{
										RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
										Text = {Text = ""},
										Button =
										{
											Color = "0 0 0 0",
											Command = topClan == clan
												? "UI_Clans page 0"
												: $"UI_Clans showclan {topClan.ClanTag}"
										}
									}, Layer + $".TopClan.{i}");

								ySwitch = ySwitch - height - margin;
							}

							#endregion

							#region Pages

							PagesUi(ref container, player, (int) Math.Ceiling((double) _clansList.Count / totalAmount),
								page,
								localPage);

							#endregion

							break;
						}

						case PLAYERS_TOP:
						{
							#region Title

							container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 1", AnchorMax = "0 1",
									OffsetMin = "2.5 -30", OffsetMax = "225 0"
								},
								Text =
								{
									Text = Msg(player.UserIDString, TopPlayersTitle),
									Align = TextAnchor.MiddleLeft,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								}
							}, Layer + ".Content");

							#endregion

							#region Head

							ySwitch = 0;
							_config.UI.TopPlayersColumns.ForEach(column =>
							{
								container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = $"{ySwitch} -50", OffsetMax = $"{ySwitch + column.Width} -30"
									},
									Text =
									{
										Text = Msg(player.UserIDString, column.LangKey),
										Align = column.TextAlign,
										Font = "robotocondensed-regular.ttf",
										FontSize = column.TitleFontSize,
										Color = "1 1 1 1"
									}
								}, Layer + ".Content");

								ySwitch += column.Width;
							});

							#endregion

							#region Table

							ySwitch = -_config.UI.PagePlayersTop.UpIndent;
							height = _config.UI.PagePlayersTop.Height;
							margin = _config.UI.PagePlayersTop.Margin;
							totalAmount = _config.UI.PagePlayersTop.PlayersOnPage;

							var ourTopPlayers = _topPlayerList.SkipAndTake(localPage * totalAmount, totalAmount);
							for (var i = 0; i < ourTopPlayers.Count; i++)
							{
								var member = ourTopPlayers[i];
								var topPlayer = GetTopDataById(member);

								var top = localPage * totalAmount + i + 1;

								container.Add(new CuiPanel
									{
										RectTransform =
										{
											AnchorMin = "0 1", AnchorMax = "0 1",
											OffsetMin = $"0 {ySwitch - height}",
											OffsetMax = $"{_config.UI.PagePlayersTop.Width} {ySwitch}"
										},
										Image =
										{
											Color = _config.UI.Color3.Get()
										}
									}, Layer + ".Content", Layer + $".TopPlayer.{i}", Layer + $".TopPlayer.{i}");

								var localSwitch = 0f;
								_config.UI.TopPlayersColumns.ForEach(column =>
								{
									var param = topPlayer.GetParams(column.Key);
									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 1",
												OffsetMin = $"{localSwitch} 0",
												OffsetMax = $"{localSwitch + column.Width} 0"
											},
											Text =
											{
												Text = $"{column.GetFormat(top, param)}",
												Align = column.TextAlign,
												Font = "robotocondensed-bold.ttf",
												FontSize = column.FontSize,
												Color = "1 1 1 1"
											}
										}, Layer + $".TopPlayer.{i}");

									localSwitch += column.Width;
								});

								container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "1 1"
										},
										Text =
										{
											Text = ""
										},
										Button =
										{
											Color = "0 0 0 0",
											Command = $"UI_Clans showprofile {member}"
										}
									}, Layer + $".TopPlayer.{i}");

								ySwitch = ySwitch - height - margin;
							}

							#endregion

							#region Pages

							PagesUi(ref container, player,
								(int) Math.Ceiling((double) _topPlayerList.Count / totalAmount),
								page, localPage);

							#endregion

							break;
						}

						case GATHER_RATES:
						{
							amountOnString = 4;
							strings = 3;
							totalAmount = amountOnString * strings;

							height = 115;
							width = 115;
							margin = 5;

							xSwitch = 0;
							ySwitch = 0;

							if (clan.IsOwner(player.userID))
							{
								for (var slot = 0; slot < totalAmount; slot++)
								{
									var founded = clan.ResourceStandarts.ContainsKey(slot);

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
												Color = founded ? _config.UI.Color3.Get() : _config.UI.Color4.Get()
											}
										}, Layer + ".Content", Layer + $".ResourсeStandart.{slot}");

									if (founded)
									{
										var standart = clan.ResourceStandarts[slot];
										if (standart == null) continue;

										container.Add(standart.GetImage("0.5 1", "0.5 1", "-30 -70", "30 -10",
											Layer + $".ResourсeStandart.{slot}"));

										#region Progress Text

										var done = data.GetAmount(standart.ShortName);

										if (done > standart.Amount)
											done = standart.Amount;

										//if (done < standart.Amount)
										{
											container.Add(new CuiLabel
												{
													RectTransform =
													{
														AnchorMin = "0.5 1", AnchorMax = "0.5 1",
														OffsetMin = "-55 -85", OffsetMax = "55 -75"
													},
													Text =
													{
														Text = Msg(player.UserIDString, LeftTitle),
														Align = TextAnchor.MiddleLeft,
														Font = "robotocondensed-regular.ttf",
														FontSize = 10,
														Color = "1 1 1 0.35"
													}
												}, Layer + $".ResourсeStandart.{slot}");

											container.Add(new CuiLabel
												{
													RectTransform =
													{
														AnchorMin = "0.5 1", AnchorMax = "0.5 1",
														OffsetMin = "-55 -100", OffsetMax = "55 -85"
													},
													Text =
													{
														Text = $"{done} / {standart.Amount}",
														Align = TextAnchor.MiddleCenter,
														Font = "robotocondensed-bold.ttf",
														FontSize = 12,
														Color = "1 1 1 1"
													}
												}, Layer + $".ResourсeStandart.{slot}");
										}

										#endregion

										#region Progress Bar

										container.Add(new CuiPanel
											{
												RectTransform =
												{
													AnchorMin = "0 0", AnchorMax = "1 0",
													OffsetMin = "0 0", OffsetMax = "0 10"
												},
												Image =
												{
													Color = _config.UI.Color4.Get()
												}
											}, Layer + $".ResourсeStandart.{slot}",
											Layer + $".ResourсeStandart.{slot}.Progress");

										var progress = done < standart.Amount
											? Math.Round(done / standart.Amount, 3)
											: 1.0;
										if (progress > 0)
											container.Add(new CuiPanel
												{
													RectTransform =
													{
														AnchorMin = "0 0", AnchorMax = $"{progress} 0.95"
													},
													Image =
													{
														Color = _config.UI.Color2.Get()
													}
												}, Layer + $".ResourсeStandart.{slot}.Progress");

										#endregion

										#region Edit

										if (clan.IsOwner(player.userID))
											container.Add(new CuiButton
												{
													RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
													Text = {Text = ""},
													Button =
													{
														Color = "0 0 0 0",
														Command = $"UI_Clans edititem {slot}"
													}
												}, Layer + $".ResourсeStandart.{slot}");

										#endregion
									}
									else
									{
										container.Add(new CuiLabel
											{
												RectTransform =
												{
													AnchorMin = "0.5 1", AnchorMax = "0.5 1",
													OffsetMin = "-30 -70", OffsetMax = "30 -10"
												},
												Text =
												{
													Text = "?",
													Align = TextAnchor.MiddleCenter,
													FontSize = 24,
													Font = "robotocondensed-bold.ttf",
													Color = "1 1 1 0.5"
												}
											}, Layer + $".ResourсeStandart.{slot}");

										container.Add(new CuiButton
											{
												RectTransform =
												{
													AnchorMin = "0 0", AnchorMax = "1 0",
													OffsetMin = "0 0", OffsetMax = "0 25"
												},
												Text =
												{
													Text = Msg(player.UserIDString, EditTitle),
													Align = TextAnchor.MiddleCenter,
													Font = "robotocondensed-regular.ttf",
													FontSize = 10,
													Color = "1 1 1 1"
												},
												Button =
												{
													Color = _config.UI.Color2.Get(),
													Command = $"UI_Clans edititem {slot}"
												}
											}, Layer + $".ResourсeStandart.{slot}");
									}

									if ((slot + 1) % amountOnString == 0)
									{
										xSwitch = 0;
										ySwitch = ySwitch - height - margin;
									}
									else
									{
										xSwitch += width + margin;
									}
								}
							}
							else
							{
								var z = 1;
								foreach (var standart in clan.ResourceStandarts)
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
												Color = _config.UI.Color3.Get()
											}
										}, Layer + ".Content", Layer + $".ResourсeStandart.{z}");

									container.Add(standart.Value.GetImage("0.5 1", "0.5 1", "-30 -70", "30 -10",
										Layer + $".ResourсeStandart.{z}"));

									#region Progress Text

									var done = data.GetAmount(standart.Value.ShortName);

									if (done < standart.Value.Amount)
									{
										container.Add(new CuiLabel
											{
												RectTransform =
												{
													AnchorMin = "0.5 1", AnchorMax = "0.5 1",
													OffsetMin = "-55 -85", OffsetMax = "55 -75"
												},
												Text =
												{
													Text = Msg(player.UserIDString, LeftTitle),
													Align = TextAnchor.MiddleLeft,
													Font = "robotocondensed-regular.ttf",
													FontSize = 10,
													Color = "1 1 1 0.35"
												}
											}, Layer + $".ResourсeStandart.{z}");

										container.Add(new CuiLabel
											{
												RectTransform =
												{
													AnchorMin = "0.5 1", AnchorMax = "0.5 1",
													OffsetMin = "-55 -100", OffsetMax = "55 -85"
												},
												Text =
												{
													Text = $"{done} / {standart.Value.Amount}",
													Align = TextAnchor.MiddleCenter,
													Font = "robotocondensed-bold.ttf",
													FontSize = 12,
													Color = "1 1 1 1"
												}
											}, Layer + $".ResourсeStandart.{z}");
									}

									#endregion

									#region Progress Bar

									container.Add(new CuiPanel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "1 0",
												OffsetMin = "0 0", OffsetMax = "0 10"
											},
											Image =
											{
												Color = _config.UI.Color4.Get()
											}
										}, Layer + $".ResourсeStandart.{z}", Layer + $".ResourсeStandart.{z}.Progress");

									var progress = done < standart.Value.Amount ? done / standart.Value.Amount : 1f;
									if (progress > 0)
										container.Add(new CuiPanel
											{
												RectTransform =
												{
													AnchorMin = "0 0", AnchorMax = $"{progress} 0.95"
												},
												Image =
												{
													Color = _config.UI.Color2.Get()
												}
											}, Layer + $".ResourсeStandart.{z}.Progress");

									#endregion

									if (z % amountOnString == 0)
									{
										xSwitch = 0;
										ySwitch = ySwitch - height - margin;
									}
									else
									{
										xSwitch += width + margin;
									}

									z++;
								}
							}

							break;
						}

						case PLAYERS_LIST:
						{
							amountOnString = _config.UI.PagePlayersList.MembersOnLine;
							strings = _config.UI.PagePlayersList.MaxLines;
							totalAmount = amountOnString * strings;
							ySwitch = -_config.UI.PagePlayersList.UpIndent;
							height = _config.UI.PagePlayersList.Height;
							width = _config.UI.PagePlayersList.Width;
							margin = _config.UI.PagePlayersList.Margin;

							var availablePlayers = (_config.UI.PagePlayersList.ShowAllPlayers ? BasePlayer.allPlayerList : BasePlayer.activePlayerList).Where(member =>
							{
								if (!_invites.CanSendInvite(member.userID, clan.ClanTag))
									return false;

								if (FindClanByPlayerFromCache(member.UserIDString) != null)
									return false;

								var displayName = GetPlayerName(member);
								return string.IsNullOrEmpty(search) || search.Length <= 2 ||
								       displayName == search ||
								       displayName.StartsWith(search,
									       StringComparison.CurrentCultureIgnoreCase) ||
								       displayName.Contains(search);
							});

							var members = availablePlayers.SkipAndTake(localPage * totalAmount, totalAmount);
							for (var z = 0; z < members.Count; z++)
							{
								xSwitch = (z + 1) % amountOnString == 0
									? margin * 2 + width
									: margin;

								var member = members[z];

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
											Color = _config.UI.Color3.Get()
										}
									}, Layer + ".Content", Layer + $".Player.{member.userID}");

								container.Add(new CuiElement
								{
									Parent = Layer + $".Player.{member.userID}",
									Components =
									{
										new CuiRawImageComponent
											{Png = GetImage( $"avatar_{member.userID}")},
										new CuiRectTransformComponent
										{
											AnchorMin = "0 0", AnchorMax = "0 0",
											OffsetMin = "0 0", OffsetMax = $"{height} {height}"
										}
									}
								});

								#region Display Name

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0.5", AnchorMax = "0 1",
											OffsetMin = "40 1",
											OffsetMax = "110 0"
										},
										Text =
										{
											Text = Msg(player.UserIDString, NameTitle),
											Align = TextAnchor.LowerLeft,
											Font = "robotocondensed-regular.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member.userID}");

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "0 0.5",
											OffsetMin = "40 0",
											OffsetMax = "95 -1"
										},
										Text =
										{
											Text = $"{member.displayName}",
											Align = TextAnchor.UpperLeft,
											Font = "robotocondensed-bold.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member.userID}");

								#endregion

								#region SteamId

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0.5", AnchorMax = "0 1",
											OffsetMin = "95 1",
											OffsetMax = "210 0"
										},
										Text =
										{
											Text = Msg(player.UserIDString, SteamIdTitle),
											Align = TextAnchor.LowerLeft,
											Font = "robotocondensed-regular.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member.userID}");

								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "0 0.5",
											OffsetMin = "95 0",
											OffsetMax = "210 -1"
										},
										Text =
										{
											Text = $"{member.userID}",
											Align = TextAnchor.UpperLeft,
											Font = "robotocondensed-bold.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										}
									}, Layer + $".Player.{member.userID}");

								#endregion

								#region Button

								container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "1 0.5", AnchorMax = "1 0.5",
											OffsetMin = "-45 -8", OffsetMax = "-5 8"
										},
										Text =
										{
											Text = Msg(player.UserIDString, InviteTitle),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-regular.ttf",
											FontSize = 10,
											Color = "1 1 1 1"
										},
										Button =
										{
											Color = _config.UI.Color2.Get(),
											Command = $"UI_Clans invite send {member.userID}"
										}
									}, Layer + $".Player.{member.userID}");

								#endregion

								if ((z + 1) % amountOnString == 0) ySwitch = ySwitch - height - margin;
							}

							#region Search

							container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0.5 0", AnchorMax = "0.5 0",
									OffsetMin = "-140 20",
									OffsetMax = "60 55"
								},
								Image =
								{
									Color = _config.UI.Color4.Get()
								}
							}, Layer + ".Content", Layer + ".Search");

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
										Command = $"UI_Clans page {page} 0 ",
										Color = "1 1 1 0.65",
										CharsLimit = 32,
										NeedsKeyboard = true,
										Text = string.IsNullOrEmpty(search)
											? Msg(player.UserIDString, SearchTitle)
											: $"{search}"
									},
									new CuiRectTransformComponent
									{
										AnchorMin = "0 0", AnchorMax = "1 1"
									}
								}
							});

							#endregion

							#region Pages

							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0.5 0", AnchorMax = "0.5 0",
									OffsetMin = "65 20",
									OffsetMax = "100 55"
								},
								Text =
								{
									Text = Msg(player.UserIDString, BackPage),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = _config.UI.Color4.Get(),
									Command = localPage != 0 ? $"UI_Clans page {page} {localPage - 1} {search}" : ""
								}
							}, Layer + ".Content");

							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0.5 0", AnchorMax = "0.5 0",
									OffsetMin = "105 20",
									OffsetMax = "140 55"
								},
								Text =
								{
									Text = Msg(player.UserIDString, NextPage),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = _config.UI.Color2.Get(),
									Command = availablePlayers.Count > (localPage + 1) * totalAmount
										? $"UI_Clans page {page} {localPage + 1} {search}"
										: ""
								}
							}, Layer + ".Content");

							#endregion

							break;
						}

						case SKINS_PAGE:
						{
							#region List

							amountOnString = 4;
							strings = 3;
							totalAmount = amountOnString * strings;

							height = 105;
							width = 110;
							margin = 5;

							xSwitch = 12.5f;
							ySwitch = 0;

							var isOwner = clan.IsOwner(player.userID);

							var items = _skinnedItems.SkipAndTake(totalAmount * localPage, totalAmount);
							for (var i = 0; i < items.Count; i++)
							{
								var item = items[i];

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
											Color = _config.UI.Color3.Get()
										}
									}, Layer + ".Content", Layer + $".SkinItem.{i}");

								container.Add(new CuiElement
								{
									Parent = Layer + $".SkinItem.{i}",
									Components =
									{
										new CuiImageComponent
										{
											ItemId = FindItemID(item),
											SkinId = GetItemSkin(item, clan)
										},
										new CuiRectTransformComponent
										{
											AnchorMin = isOwner ? "0.5 1" : "0.5 0.5",
											AnchorMax = isOwner ? "0.5 1" : "0.5 0.5",
											OffsetMin = isOwner ? "-30 -70" : "-30 -30",
											OffsetMax = isOwner ? "30 -10" : "30 30"
										}
									}
								});

								#region Edit

								if (isOwner)
									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "1 0",
												OffsetMin = "0 0", OffsetMax = "0 25"
											},
											Text =
											{
												Text = Msg(player.UserIDString, EditTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 10,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color2.Get(),
												Command = $"UI_Clans editskin {item} -1"
											}
										}, Layer + $".SkinItem.{i}");

								#endregion

								if ((i + 1) % amountOnString == 0)
								{
									xSwitch = 12.5f;
									ySwitch = ySwitch - height - margin - margin;
								}
								else
								{
									xSwitch += width + margin;
								}
							}

							#endregion

							#region Pages

							PagesUi(ref container, player,
								(int) Math.Ceiling((double) _skinnedItems.Count / totalAmount), page,
								localPage);

							#endregion

							#region Header

							if (_config.Skins.DisableSkins)
								ButtonClanSkins(ref container, player, data);

							#endregion

							break;
						}

						case ALIANCES_LIST:
						{
							if (clan.Alliances.Count == 0)
							{
								container.Add(new CuiLabel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text =
									{
										Text = Msg(player.UserIDString, NoAllies),
										Align = TextAnchor.MiddleCenter,
										FontSize = 34,
										Font = "robotocondensed-bold.ttf",
										Color = _config.UI.Color5.Get()
									}
								}, Layer + ".Content");
							}
							else
							{
								amountOnString = 2;
								strings = 8;
								totalAmount = amountOnString * strings;
								ySwitch = 0f;
								height = 35f;
								width = 237.5f;
								margin = 5f;

								var alliances = clan.Alliances.SkipAndTake(localPage * totalAmount,
									totalAmount);
								for (var z = 0; z < alliances.Count; z++)
								{
									xSwitch = (z + 1) % amountOnString == 0
										? margin + width
										: 0;

									var alliance = alliances[z];

									var allianceClan = FindClanByTag(alliance);
									if (allianceClan == null) continue;

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
												Color = _config.UI.Color3.Get()
											}
										}, Layer + ".Content", Layer + $".Player.{alliance}");

									container.Add(new CuiElement
									{
										Parent = Layer + $".Player.{alliance}",
										Components =
										{
											new CuiRawImageComponent
											{
												Png = GetImage(
													string.IsNullOrEmpty(allianceClan.Avatar)
														? _config.Avatar.DefaultAvatar
														: $"clanavatar_{allianceClan.ClanTag}")
											},
											new CuiRectTransformComponent
											{
												AnchorMin = "0 0", AnchorMax = "0 0",
												OffsetMin = "0 0", OffsetMax = $"{height} {height}"
											}
										}
									});

									#region Display Name

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "40 1",
												OffsetMax = "110 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, NameTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 10,
												Color = "1 1 1 1"
											}
										}, Layer + $".Player.{alliance}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "40 0",
												OffsetMax = "95 -1"
											},
											Text =
											{
												Text = $"{allianceClan.ClanTag}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 10,
												Color = "1 1 1 1"
											}
										}, Layer + $".Player.{alliance}");

									#endregion

									#region SteamId

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "95 1",
												OffsetMax = "210 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, MembersTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 10,
												Color = "1 1 1 1"
											}
										}, Layer + $".Player.{alliance}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "95 0",
												OffsetMax = "210 -1"
											},
											Text =
											{
												Text = $"{allianceClan.Members.Count}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 10,
												Color = "1 1 1 1"
											}
										}, Layer + $".Player.{alliance}");

									#endregion

									#region Button

									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "1 0.5", AnchorMax = "1 0.5",
												OffsetMin = "-45 -8", OffsetMax = "-5 8"
											},
											Text =
											{
												Text = Msg(player.UserIDString, ProfileTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 10,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color2.Get(),
												Command = $"UI_Clans showclan {alliance}"
											}
										}, Layer + $".Player.{alliance}");

									#endregion

									if ((z + 1) % amountOnString == 0) ySwitch = ySwitch - height - margin;
								}

								#region Pages

								container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0.5 0", AnchorMax = "0.5 0",
										OffsetMin = "-37.5 20",
										OffsetMax = "-2.5 55"
									},
									Text =
									{
										Text = Msg(player.UserIDString, BackPage),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-regular.ttf",
										FontSize = 12,
										Color = "1 1 1 1"
									},
									Button =
									{
										Color = _config.UI.Color4.Get(),
										Command = localPage != 0 ? $"UI_Clans page {page} {localPage - 1} {search}" : ""
									}
								}, Layer + ".Content");

								container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0.5 0", AnchorMax = "0.5 0",
										OffsetMin = "2.5 20",
										OffsetMax = "37.5 55"
									},
									Text =
									{
										Text = Msg(player.UserIDString, NextPage),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-regular.ttf",
										FontSize = 12,
										Color = "1 1 1 1"
									},
									Button =
									{
										Color = _config.UI.Color2.Get(),
										Command = clan.Alliances.Count > (localPage + 1) * totalAmount
											? $"UI_Clans page {page} {localPage + 1} {search}"
											: ""
									}
								}, Layer + ".Content");

								#endregion
							}

							break;
						}

						case 45: //clan invites (by player)
						{
							var invites = _invites.GetPlayerClanInvites(player.userID);

							if (invites.Count == 0)
							{
								container.Add(new CuiLabel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text =
									{
										Text = Msg(player.UserIDString, NoInvites),
										Align = TextAnchor.MiddleCenter,
										FontSize = 34,
										Font = "robotocondensed-bold.ttf",
										Color = _config.UI.Color5.Get()
									}
								}, Layer + ".Content");
							}
							else
							{
								ySwitch = 0f;
								height = 48.5f;
								margin = 5f;
								totalAmount = 7;

								foreach (var invite in invites.SkipAndTake(localPage * totalAmount, totalAmount))
								{
									container.Add(new CuiPanel
										{
											RectTransform =
											{
												AnchorMin = "0 1", AnchorMax = "0 1",
												OffsetMin = $"0 {ySwitch - height}",
												OffsetMax = $"480 {ySwitch}"
											},
											Image =
											{
												Color = _config.UI.Color3.Get()
											}
										}, Layer + ".Content", Layer + $".Invite.{invite.ClanTag}");

									var targetClan = FindClanByTag(invite.ClanTag);
									if (targetClan != null)
										container.Add(new CuiElement
										{
											Parent = Layer + $".Invite.{invite.ClanTag}",
											Components =
											{
												new CuiRawImageComponent
												{
													Png = GetImage(
														string.IsNullOrEmpty(targetClan.Avatar)
															? _config.Avatar.DefaultAvatar
															: $"clanavatar_{targetClan.ClanTag}")
												},
												new CuiRectTransformComponent
												{
													AnchorMin = "0 0", AnchorMax = "0 0",
													OffsetMin = "0 0", OffsetMax = $"{height} {height}"
												}
											}
										});

									#region Clan Name

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "55 1", OffsetMax = "135 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, ClanInvitation),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.ClanTag}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "55 0", OffsetMax = "135 -1"
											},
											Text =
											{
												Text = $"{invite.ClanTag}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.ClanTag}");

									#endregion

									#region Inviter

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "160 1", OffsetMax = "315 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, InviterTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.ClanTag}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "160 0", OffsetMax = "315 -1"
											},
											Text =
											{
												Text = $"{invite.InviterName}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.ClanTag}");

									#endregion

									#region Buttons

									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "1 0.5", AnchorMax = "1 0.5",
												OffsetMin = "-95 -12.5", OffsetMax = "-15 12.5"
											},
											Text =
											{
												Text = Msg(player.UserIDString, AcceptTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color2.Get(),
												Command = $"UI_Clans invite accept {invite.ClanTag}",
												Close = Layer
											}
										}, Layer + $".Invite.{invite.ClanTag}");

									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "1 0.5", AnchorMax = "1 0.5",
												OffsetMin = "-185 -12.5", OffsetMax = "-105 12.5"
											},
											Text =
											{
												Text = Msg(player.UserIDString, CancelTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color6.Get(),
												Command = $"UI_Clans invite cancel {invite.ClanTag}",
												Close = Layer
											}
										}, Layer + $".Invite.{invite.ClanTag}");

									#endregion

									ySwitch = ySwitch - height - margin;
								}

								#region Pages

								PagesUi(ref container, player,
									(int) Math.Ceiling((double) invites.Count / totalAmount), page, localPage);

								#endregion
							}

							break;
						}

						case 65: //clan invites (by clan)
						{
							var invites = _invites.GetClanPlayersInvites(clan.ClanTag);
							if (invites.Count == 0)
							{
								container.Add(new CuiLabel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text =
									{
										Text = Msg(player.UserIDString, NoInvites),
										Align = TextAnchor.MiddleCenter,
										FontSize = 34,
										Font = "robotocondensed-bold.ttf",
										Color = _config.UI.Color5.Get()
									}
								}, Layer + ".Content");
							}
							else
							{
								ySwitch = 0f;
								height = 48.5f;
								margin = 5f;
								totalAmount = 7;

								foreach (var invite in invites.SkipAndTake(localPage * totalAmount, totalAmount))
								{
									container.Add(new CuiPanel
										{
											RectTransform =
											{
												AnchorMin = "0 1", AnchorMax = "0 1",
												OffsetMin = $"0 {ySwitch - height}",
												OffsetMax = $"480 {ySwitch}"
											},
											Image =
											{
												Color = _config.UI.Color3.Get()
											}
										}, Layer + ".Content", Layer + $".Invite.{invite.RetrieverId}");

									container.Add(new CuiElement
									{
										Parent = Layer + $".Invite.{invite.RetrieverId}",
										Components =
										{
											new CuiRawImageComponent
											{
												Png = GetImage(
													$"avatar_{invite.RetrieverId}")
											},
											new CuiRectTransformComponent
											{
												AnchorMin = "0 0", AnchorMax = "0 0",
												OffsetMin = "0 0", OffsetMax = $"{height} {height}"
											}
										}
									});

									#region Player Name

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "75 1", OffsetMax = "195 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, PlayerTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.RetrieverId}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "75 0", OffsetMax = "195 -1"
											},
											Text =
											{
												Text =
													$"{GetPlayerName(invite.RetrieverId)}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.RetrieverId}");

									#endregion

									#region Inviter

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "195 1", OffsetMax = "315 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, InviterTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.RetrieverId}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "195 0", OffsetMax = "315 -1"
											},
											Text =
											{
												Text = $"{invite.InviterName}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.RetrieverId}");

									#endregion

									#region Buttons

									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "1 0.5", AnchorMax = "1 0.5",
												OffsetMin = "-185 -12.5", OffsetMax = "-15 12.5"
											},
											Text =
											{
												Text = Msg(player.UserIDString, CancelTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color6.Get(),
												Command = $"UI_Clans invite withdraw {invite.RetrieverId}"
											}
										}, Layer + $".Invite.{invite.RetrieverId}");

									#endregion

									ySwitch = ySwitch - height - margin;
								}

								#region Pages

								PagesUi(ref container, player,
									(int) Math.Ceiling((double) invites.Count / totalAmount), page, localPage);

								#endregion
							}


							break;
						}

						case 71: //ally invites
						{
							var invites = _invites.GetAllyTargetInvites(clan.ClanTag);
							if (invites.Count == 0)
							{
								container.Add(new CuiLabel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text =
									{
										Text = Msg(player.UserIDString, NoInvites),
										Align = TextAnchor.MiddleCenter,
										FontSize = 34,
										Font = "robotocondensed-bold.ttf",
										Color = _config.UI.Color5.Get()
									}
								}, Layer + ".Content");
							}
							else
							{
								ySwitch = 0f;
								height = 48.5f;
								margin = 5f;
								totalAmount = 7;

								foreach (var invite in invites.SkipAndTake(localPage * totalAmount, totalAmount))
								{
									var targetClan = FindClanByTag(invite.SenderClanTag);
									if (targetClan == null) continue;

									container.Add(new CuiPanel
										{
											RectTransform =
											{
												AnchorMin = "0 1", AnchorMax = "0 1",
												OffsetMin = $"0 {ySwitch - height}",
												OffsetMax = $"480 {ySwitch}"
											},
											Image =
											{
												Color = _config.UI.Color3.Get()
											}
										}, Layer + ".Content", Layer + $".Invite.{invite.TargetClanTag}");

									container.Add(new CuiElement
									{
										Parent = Layer + $".Invite.{invite.TargetClanTag}",
										Components =
										{
											new CuiRawImageComponent
											{
												Png = GetImage(
													string.IsNullOrEmpty(targetClan.Avatar)
														? _config.Avatar.DefaultAvatar
														: $"clanavatar_{targetClan.ClanTag}")
											},
											new CuiRectTransformComponent
											{
												AnchorMin = "0 0", AnchorMax = "0 0",
												OffsetMin = "0 0", OffsetMax = $"{height} {height}"
											}
										}
									});

									#region Title

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "75 1", OffsetMax = "195 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, ClanTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.TargetClanTag}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "75 0", OffsetMax = "195 -1"
											},
											Text =
											{
												Text = $"{targetClan.ClanTag}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.TargetClanTag}");

									#endregion

									#region Inviter

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "195 1", OffsetMax = "315 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, InviterTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.TargetClanTag}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "195 0", OffsetMax = "315 -1"
											},
											Text =
											{
												Text = $"{invite.SenderName}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.TargetClanTag}");

									#endregion

									#region Buttons

									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "1 0.5", AnchorMax = "1 0.5",
												OffsetMin = "-95 -12.5", OffsetMax = "-15 12.5"
											},
											Text =
											{
												Text = Msg(player.UserIDString, CancelTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color6.Get(),
												Command = $"UI_Clans allyinvite withdraw {invite.TargetClanTag}"
											}
										}, Layer + $".Invite.{invite.TargetClanTag}");

									#endregion

									ySwitch = ySwitch - height - margin;
								}

								#region Pages

								PagesUi(ref container, player,
									(int) Math.Ceiling((double) invites.Count / totalAmount), page, localPage);

								#endregion
							}

							break;
						}

						case 72: //incoming ally
						{
							var invites = _invites.GetAllyIncomingInvites(clan.ClanTag);
							if (invites.Count == 0)
							{
								container.Add(new CuiLabel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text =
									{
										Text = Msg(player.UserIDString, NoInvites),
										Align = TextAnchor.MiddleCenter,
										FontSize = 34,
										Font = "robotocondensed-bold.ttf",
										Color = _config.UI.Color5.Get()
									}
								}, Layer + ".Content");
							}
							else
							{
								ySwitch = 0f;
								height = 48.5f;
								margin = 5f;
								totalAmount = 7;

								foreach (var invite in invites.SkipAndTake(localPage * totalAmount, totalAmount))
								{
									var targetClan = FindClanByTag(invite.SenderClanTag);
									if (targetClan == null) continue;

									container.Add(new CuiPanel
										{
											RectTransform =
											{
												AnchorMin = "0 1", AnchorMax = "0 1",
												OffsetMin = $"0 {ySwitch - height}",
												OffsetMax = $"480 {ySwitch}"
											},
											Image =
											{
												Color = _config.UI.Color3.Get()
											}
										}, Layer + ".Content", Layer + $".Invite.{invite.SenderClanTag}");

									container.Add(new CuiElement
									{
										Parent = Layer + $".Invite.{invite.SenderClanTag}",
										Components =
										{
											new CuiRawImageComponent
											{
												Png = GetImage(
													string.IsNullOrEmpty(targetClan.Avatar)
														? _config.Avatar.DefaultAvatar
														: $"clanavatar_{targetClan.ClanTag}")
											},
											new CuiRectTransformComponent
											{
												AnchorMin = "0 0", AnchorMax = "0 0",
												OffsetMin = "0 0", OffsetMax = $"{height} {height}"
											}
										}
									});

									#region Title

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0.5", AnchorMax = "0 1",
												OffsetMin = "75 1", OffsetMax = "195 0"
											},
											Text =
											{
												Text = Msg(player.UserIDString, ClanTitle),
												Align = TextAnchor.LowerLeft,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.SenderClanTag}");

									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "0 0.5",
												OffsetMin = "75 0", OffsetMax = "195 -1"
											},
											Text =
											{
												Text = $"{targetClan.ClanTag}",
												Align = TextAnchor.UpperLeft,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Invite.{invite.SenderClanTag}");

									#endregion

									#region Buttons

									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "1 0.5", AnchorMax = "1 0.5",
												OffsetMin = "-95 -12.5", OffsetMax = "-15 12.5"
											},
											Text =
											{
												Text = Msg(player.UserIDString, AcceptTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color2.Get(),
												Command = $"UI_Clans allyinvite accept {invite.SenderClanTag}",
												Close = Layer
											}
										}, Layer + $".Invite.{invite.SenderClanTag}");

									container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "1 0.5", AnchorMax = "1 0.5",
												OffsetMin = "-185 -12.5", OffsetMax = "-105 12.5"
											},
											Text =
											{
												Text = Msg(player.UserIDString, CancelTitle),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = _config.UI.Color6.Get(),
												Command = $"UI_Clans allyinvite cancel {invite.SenderClanTag}",
												Close = Layer
											}
										}, Layer + $".Invite.{invite.SenderClanTag}");

									#endregion

									ySwitch = ySwitch - height - margin;
								}

								#region Pages

								PagesUi(ref container, player, (int) Math.Ceiling((double) invites.Count / totalAmount),
									page, localPage);

								#endregion
							}

							break;
						}
					}
				else
					container.Add(new CuiLabel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text =
						{
							Text = Msg(player.UserIDString, NotMemberOfClan),
							Align = TextAnchor.MiddleCenter,
							FontSize = 34,
							Font = "robotocondensed-bold.ttf",
							Color = _config.UI.Color5.Get()
						}
					}, Layer + ".Content");

				#endregion

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private void ShowSelectItemUI(BasePlayer player, int slot, int page = 0, int amount = 0, string search = "",
			bool first = false)
		{
#if TESTING
			using (new StopwatchWrapper("select item ui"))
#endif
			{
				#region Fields

				var clan = FindClanByPlayer(player.UserIDString);

				var itemsList = _defaultItems.FindAll(item =>
					string.IsNullOrEmpty(search) || search.Length <= 2 ||
					item.shortname.Contains(search) ||
					item.displayName.english.Contains(search));

				if (amount == 0)
					amount = clan.ResourceStandarts.TryGetValue(slot, out var standart)
						? standart.Amount
						: _config.DefaultValStandarts;

				var amountOnString = 10;
				var strings = 5;
				var totalAmount = amountOnString * strings;

				var Height = 115f;
				var Width = 110f;
				var Margin = 10f;

				var constSwitchX = -(amountOnString * Width + (amountOnString - 1) * Margin) / 2f;

				var xSwitch = constSwitchX;
				var ySwitch = -75f;

				#endregion

				var container = new CuiElementContainer();

				#region Background

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

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Close = Layer
						}
					}, Layer);
				}

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
						Color = _config.UI.Color1.Get()
					}
				}, Layer, Layer + ".Main", Layer + ".Main");

				#region Header

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -55", OffsetMax = "0 0"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Main", Layer + ".Header");

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "25 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player.UserIDString, SelectItemTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + ".Header");

				#endregion

				#region Search

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = "160 -17.5", OffsetMax = "410 17.5"
					},
					Image =
					{
						Color = _config.UI.Color4.Get()
					}
				}, Layer + ".Header", Layer + ".Header.Search");

				container.Add(new CuiElement
				{
					Parent = Layer + ".Header.Search",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							Command = $"UI_Clans selectpages {slot} 0 {amount} ",
							Color = "1 1 1 0.65",
							CharsLimit = 32,
							NeedsKeyboard = true,
							Text = string.IsNullOrEmpty(search) ? Msg(player.UserIDString, SearchTitle) : $"{search}"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});

				#endregion

				#region Amount

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-35 -17.5", OffsetMax = "95 17.5"
					},
					Image =
					{
						Color = _config.UI.Color4.Get()
					}
				}, Layer + ".Header", Layer + ".Header.Amount");

				container.Add(new CuiElement
				{
					Parent = Layer + ".Header.Amount",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							Command = $"UI_Clans setamountitem {slot} ",
							Color = "1 1 1 0.65",
							CharsLimit = 32,
							NeedsKeyboard = true,
							Text = $"{amount}"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});

				#endregion

				#region Pages

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = "415 -17.5", OffsetMax = "450 17.5"
					},
					Text =
					{
						Text = Msg(player.UserIDString, BackPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get(),
						Command = page != 0 ? $"UI_Clans selectpages {slot} {page - 1} {amount} {search}" : ""
					}
				}, Layer + ".Header");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = "455 -17.5", OffsetMax = "490 17.5"
					},
					Text =
					{
						Text = Msg(player.UserIDString, NextPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = itemsList.Count > (page + 1) * totalAmount
							? $"UI_Clans selectpages {slot} {page + 1} {amount} {search}"
							: ""
					}
				}, Layer + ".Header");

				#endregion

				#region Close

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-35 -12.5",
						OffsetMax = "-10 12.5"
					},
					Text =
					{
						Text = Msg(player.UserIDString, CloseTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = "UI_Clans page 4"
					}
				}, Layer + ".Header");

				#endregion

				#endregion

				#region Items

				var items = itemsList.SkipAndTake(page * totalAmount, totalAmount);
				for (var i = 0; i < items.Count; i++)
				{
					var def = items[i];

					var isSelectedItem = clan.ResourceStandarts.ContainsKey(slot) &&
					                     clan.ResourceStandarts[slot].ShortName == def.shortname;
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
								Color = isSelectedItem
									? _config.UI.Color4.Get()
									: _config.UI.Color3.Get()
							}
						}, Layer + ".Main", Layer + $".Item.{i}");

					container.Add(new CuiElement
					{
						Parent = Layer + $".Item.{i}",
						Components =
						{
							new CuiImageComponent
							{
								ItemId = def.itemid
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-35 -80", OffsetMax = "35 -10"
							}
						}
					});

					container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "0 0", OffsetMax = "0 25"
							},
							Text =
							{
								Text = Msg(player.UserIDString, SelectTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color2.Get(),
								Command = $"UI_Clans selectitem {slot} {def.shortname} {amount}"
							}
						}, Layer + $".Item.{i}");

					if (isSelectedItem)
						container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "1 1", AnchorMax = "1 1",
									OffsetMin = "-25 -25", OffsetMax = "0 0"
								},
								Text =
								{
									//TODO: Add to lang
									Text = "✕",
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 10,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = "0 0 0 0",
									Command = $"UI_Clans confirm resource open {slot}"
								}
							}, Layer + $".Item.{i}");

					if ((i + 1) % amountOnString == 0)
					{
						xSwitch = constSwitchX;
						ySwitch = ySwitch - Height - Margin;
					}
					else
					{
						xSwitch += Width + Margin;
					}
				}

				#endregion

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private void ShowClanCreationUI(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("create clan ui"))
#endif
			{
				ClanCreating.TryAdd(player.userID, new CreateClanData());

				var clanTag = ClanCreating[player.userID].Tag;
				var avatar = ClanCreating[player.userID].Avatar;

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
				}, "Overlay", Layer, Layer);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Close = !_config.ForceClanCreateTeam ? Layer : ""
					}
				}, Layer);

				#endregion

				#region Main

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-340 -215",
						OffsetMax = "340 220"
					},
					Image =
					{
						Color = _config.UI.Color1.Get()
					}
				}, Layer, Layer + ".Main", Layer + ".Main");

				#region Header

				HeaderUi(ref container, player, null, 0, Msg(player.UserIDString, ClanCreationTitle),
					showClose: _config.UI.ShowCloseOnClanCreation);

				#endregion

				#region Name

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-150 -140", OffsetMax = "150 -110"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Main", Layer + ".Clan.Creation.Name");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player.UserIDString, ClanNameTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Creation.Name");

				container.Add(new CuiElement
				{
					Parent = Layer + ".Clan.Creation.Name",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							Command = "UI_Clans createclan name ",
							Color = "1 1 1 0.8",
							CharsLimit = _config.Tags.TagMax,
							NeedsKeyboard = true,
							Text = string.IsNullOrEmpty(clanTag) ? string.Empty : $"{clanTag}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
					}
				});

				#endregion

				#region Avatar

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-150 -210", OffsetMax = "150 -180"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Main", Layer + ".Clan.Creation.Avatar");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player.UserIDString, AvatarTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Creation.Avatar");

				container.Add(new CuiElement
				{
					Parent = Layer + ".Clan.Creation.Avatar",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							Command = "UI_Clans createclan avatar ",
							Color = "1 1 1 0.8",
							CharsLimit = 128,
							NeedsKeyboard = true,
							Text = string.IsNullOrEmpty(avatar) ? Msg(player.UserIDString, UrlTitle) : $"{avatar}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
					}
				});

				#endregion

				#region Create Clan

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-75 -295", OffsetMax = "75 -270"
					},
					Text =
					{
						Text = _config.PaidFunctionality.ChargeFeeToCreateClan
							? Msg(player.UserIDString, PaidCreateTitle, _config.PaidFunctionality.CostCreatingClan)
							: Msg(player.UserIDString, CreateTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = "UI_Clans createclan create",
						Close = Layer
					}
				}, Layer + ".Main");

				#endregion

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private void ShowClanMemberProfileUI(BasePlayer player, ulong target)
		{
#if TESTING
			using (new StopwatchWrapper("clan member profile ui"))
#endif
			{
				#region Fields

				var data = PlayerData.GetOrCreate(target.ToString());

				var clan = data?.GetClan();
				if (clan == null) return;

				#endregion

				var container = new CuiElementContainer();

				#region Background

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Second.Main", Layer + ".Content", Layer + ".Content");

				#endregion

				#region Header

				HeaderUi(ref container, player, clan, 1, Msg(player.UserIDString, ClansMenuTitle), "UI_Clans page 1");

				#endregion

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "2.5 -30", OffsetMax = "225 0"
					},
					Text =
					{
						Text = Msg(player.UserIDString, ProfileTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Content");

				#endregion

				#region Avatar

				container.Add(new CuiElement
				{
					Parent = Layer + ".Content",
					Components =
					{
						new CuiRawImageComponent {Png = GetImage( $"avatar_{target}")},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = "0 -170", OffsetMax = "140 -30"
						}
					}
				});

				#endregion

				#region Name

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "160 -50", OffsetMax = "400 -30"
					},
					Text =
					{
						Text = $"{data.DisplayName}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 16,
						Color = "1 1 1 1"
					}
				}, Layer + ".Content");

				#endregion

				#region Fields

				var ySwitch = -45f;
				var xSwitch = 0f;
				var maxWidth = 0f;
				var height = 30f;
				var widthMargin = 10f;
				var heightMargin = 20f;

				for (var i = 0; i < _config.UI.ClanMemberProfileFields.Count; i++)
				{
					var field = _config.UI.ClanMemberProfileFields[i];

					if (maxWidth == 0 || maxWidth < field.Width)
					{
						ySwitch = ySwitch - height - heightMargin;

						var hasAvatar = ySwitch is < -30 and > -170f;

						maxWidth = hasAvatar ? 300f : 460f;
						xSwitch = hasAvatar ? 160f : 0f;
					}

					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch} {ySwitch - height}",
								OffsetMax = $"{xSwitch + field.Width} {ySwitch}"
							},
							Image =
							{
								Color = _config.UI.Color3.Get()
							}
						}, Layer + ".Content", Layer + $".Content.{i}");

					if (field.Key == "gather")
					{
						var progress = data.GetTotalFarm(clan);
						if (progress > 0)
							container.Add(new CuiPanel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progress} 0.9"},
									Image =
									{
										Color = _config.UI.Color2.Get()
									}
								}, Layer + $".Content.{i}", Layer + $".Content.{i}.Progress");
					}

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "0 20"
							},
							Text =
							{
								Text = Msg(player.UserIDString, field.LangKey),
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							}
						}, Layer + $".Content.{i}");

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-5 0"
							},
							Text =
							{
								Text = $"{field.GetFormat(0, data.GetParams(field.Key, clan))}",
								Align = field.TextAlign,
								Font = "robotocondensed-bold.ttf",
								FontSize = field.FontSize,
								Color = "1 1 1 1"
							}
						}, Layer + $".Content.{i}");

					xSwitch += field.Width + widthMargin;

					maxWidth -= field.Width;
				}

				#endregion

				#region Owner Buttons

				if (clan.IsOwner(player.userID))
				{
					var width = 70f;
					height = 20f;
					var margin = 5f;

					xSwitch = 460f;
					ySwitch = 0;

					var isModerator = clan.IsModerator(target);

					if (player.userID != target)
					{
						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch - width} {ySwitch - height}",
								OffsetMax = $"{xSwitch} {ySwitch}"
							},
							Text =
							{
								Text = Msg(player.UserIDString, KickTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color6.Get(),
								Command = player.userID != target ? $"UI_Clans kick {target}" : ""
							}
						}, Layer + ".Content");

						xSwitch = xSwitch - width - margin;

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch - width} {ySwitch - height}",
								OffsetMax = $"{xSwitch} {ySwitch}"
							},
							Text =
							{
								Text = Msg(player.UserIDString, PromoteLeaderTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color4.Get(),
								Command = $"UI_Clans leader tryset {target}"
							}
						}, Layer + ".Content");

						xSwitch = xSwitch - width - margin;

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch - width} {ySwitch - height}",
								OffsetMax = $"{xSwitch} {ySwitch}"
							},
							Text =
							{
								Text = isModerator
									? Msg(player.UserIDString, DemoteModerTitle)
									: Msg(player.UserIDString, PromoteModerTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = isModerator ? _config.UI.Color4.Get() : _config.UI.Color2.Get(),
								Command = isModerator ? $"UI_Clans moder undo {target}" : $"UI_Clans moder set {target}"
							}
						}, Layer + ".Content");
					}
				}

				_config.UI?.ProfileButtons?.ForEach(btn => btn?.Get(ref container, target, Layer + ".Content", Layer));

				#endregion

				#region Farm

				if (clan.ResourceStandarts.Count > 0)
				{
					#region Title

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = "2.5 -200", OffsetMax = "225 -185"
						},
						Text =
						{
							Text = Msg(player.UserIDString, GatherRatesTitle),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						}
					}, Layer + ".Content");

					#endregion

					ySwitch = -205f;
					var amountOnString = 6;

					xSwitch = 0f;
					var Height = 75f;
					var Width = 75f;
					var Margin = 5f;

					var z = 1;
					foreach (var standart in clan.ResourceStandarts)
					{
						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 1", AnchorMax = "0 1",
									OffsetMin = $"{xSwitch} {ySwitch - Height}",
									OffsetMax = $"{xSwitch + Width} {ySwitch}"
								},
								Image =
								{
									Color = _config.UI.Color3.Get()
								}
							}, Layer + ".Content", Layer + $".Standarts.{z}");

						container.Add(standart.Value.GetImage("0.5 1", "0.5 1", "-20 -45", "20 -5",
							Layer + $".Standarts.{z}"));

						#region Progress

						var one = data.GetAmount(standart.Value.ShortName);

						var two = standart.Value.Amount;

						if (one > two)
							one = two;

						var progress = Math.Round(one / two, 3);

						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 0",
									OffsetMin = "0 0", OffsetMax = "0 5"
								},
								Image =
								{
									Color = _config.UI.Color4.Get()
								}
							}, Layer + $".Standarts.{z}", Layer + $".Standarts.{z}.Progress");

						if (progress > 0)
							container.Add(new CuiPanel
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progress} 0", OffsetMax = "0 5"},
									Image =
									{
										Color = _config.UI.Color2.Get()
									}
								}, Layer + $".Standarts.{z}");

						container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 0",
									OffsetMin = "0 0", OffsetMax = "0 20"
								},
								Text =
								{
									Text = $"{one}/<b>{two}</b>",
									Align = TextAnchor.UpperCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 10,
									Color = "1 1 1 1"
								}
							}, Layer + $".Standarts.{z}");

						#endregion

						if (z % amountOnString == 0)
						{
							xSwitch = 0;
							ySwitch = ySwitch - Margin - Height;
						}
						else
						{
							xSwitch += Margin + Width;
						}

						z++;
					}
				}

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private void ShowMemberProfileUI(BasePlayer player, ulong target)
		{
#if TESTING
			using (new StopwatchWrapper("profile ui"))
#endif
			{
				var data = GetTopDataById(target);
				if (data == null) return;

				var container = new CuiElementContainer();

				var clan = FindClanByPlayer(player.UserIDString);

				#region Menu

				if (player.userID == target) MenuUi(ref container, player, 3, clan);

				#endregion

				#region Background

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Second.Main", Layer + ".Content", Layer + ".Content");

				#endregion

				#region Header

				HeaderUi(ref container, player, clan, 3, Msg(player.UserIDString, ClansMenuTitle), "UI_Clans page 3");

				#endregion

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "2.5 -30", OffsetMax = "225 0"
					},
					Text =
					{
						Text = Msg(player.UserIDString, ProfileTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Content");

				#endregion

				#region Avatar

				container.Add(new CuiElement
				{
					Parent = Layer + ".Content",
					Components =
					{
						new CuiRawImageComponent {Png = GetImage( $"avatar_{target}")},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = "0 -170", OffsetMax = "140 -30"
						}
					}
				});

				#endregion

				#region Name

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "160 -50", OffsetMax = "400 -30"
					},
					Text =
					{
						Text = $"{data.Data.DisplayName}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 16,
						Color = "1 1 1 1"
					}
				}, Layer + ".Content");

				#endregion

				#region Fields

				var ySwitch = -45f;
				var xSwitch = 0f;
				var maxWidth = 0f;
				var height = 30f;
				var widthMargin = 20f;
				var heightMargin = 20f;

				for (var i = 0; i < _config.UI.TopPlayerProfileFields.Count; i++)
				{
					var field = _config.UI.TopPlayerProfileFields[i];

					if (maxWidth == 0 || maxWidth < field.Width)
					{
						ySwitch = ySwitch - height - heightMargin;

						var hasAvatar = ySwitch is < -30 and > -170f;

						maxWidth = hasAvatar ? 300f : 460f;
						xSwitch = hasAvatar ? 160f : 0f;
					}

					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch} {ySwitch - height}",
								OffsetMax = $"{xSwitch + field.Width} {ySwitch}"
							},
							Image =
							{
								Color = _config.UI.Color3.Get()
							}
						}, Layer + ".Content", Layer + $".Content.{i}");

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "0 20"
							},
							Text =
							{
								Text = Msg(player.UserIDString, field.LangKey),
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							}
						}, Layer + $".Content.{i}");

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-5 0"
							},
							Text =
							{
								Text =
									field.Key == "clanname" ? $"{FindClanByPlayer(target.ToString())?.ClanTag}" :
									field.Key == "rating" ? $"{data.Top}" :
									$"{field.GetFormat(0, data.Data.GetParams(field.Key, clan))}",
								Align = field.TextAlign,
								Font = "robotocondensed-bold.ttf",
								FontSize = field.FontSize,
								Color = "1 1 1 1"
							}
						}, Layer + $".Content.{i}");

					xSwitch += field.Width + widthMargin;

					maxWidth -= field.Width;
				}

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private void ShowClanAcceptSetLeaderUI(BasePlayer player, ulong target)
		{
#if TESTING
			using (new StopwatchWrapper("accept set leader"))
#endif
			{
				CuiHelper.AddUi(player, new CuiElementContainer
				{
					{
						new CuiPanel
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Image = {Color = _config.UI.Color8.Get()}
						},
						"Overlay", ModalLayer, ModalLayer
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
								Text = Msg(player.UserIDString, LeaderTransferTitle),
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
								Text = Msg(player.UserIDString, AcceptTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color2.Get(),
								Command = $"UI_Clans leader set {target}",
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
								Text = Msg(player.UserIDString, CancelTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button = {Color = _config.UI.Color7.Get(), Close = ModalLayer}
						},
						ModalLayer
					}
				});
			}
		}

		private void ShowClanProfileUI(BasePlayer player, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("clan profile ui"))
#endif
			{
				var clan = FindClanByTag(clanTag);
				if (clan == null) return;

				var playerClan = FindClanByPlayer(player.UserIDString);

				var container = new CuiElementContainer();

				#region Menu

				MenuUi(ref container, player, 2, playerClan);

				#endregion

				#region Background

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Second.Main", Layer + ".Content", Layer + ".Content");

				#endregion

				#region Header

				HeaderUi(ref container, player, playerClan, 2, Msg(player.UserIDString, ClansMenuTitle),
					"UI_Clans page 2");

				#endregion

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "2.5 -30", OffsetMax = "225 0"
					},
					Text =
					{
						Text = Msg(player.UserIDString, AboutClan),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Content");

				#endregion

				#region Avatar

				container.Add(new CuiElement
				{
					Parent = Layer + ".Content",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(
								string.IsNullOrEmpty(clan.Avatar)
									? _config.Avatar.DefaultAvatar
									: $"clanavatar_{clan.ClanTag}")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = "0 -170", OffsetMax = "140 -30"
						}
					}
				});

				#endregion

				#region Clan Name

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "160 -50", OffsetMax = "400 -30"
					},
					Text =
					{
						Text = $"{clan.ClanTag}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 16,
						Color = "1 1 1 1"
					}
				}, Layer + ".Content");

				#endregion

				#region Clan Leader

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "160 -105",
						OffsetMax = "460 -75"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Content", Layer + ".Clan.Leader");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player.UserIDString, LeaderTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Leader");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{clan.LeaderName}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Leader");

				#endregion

				#region Rating

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "160 -165", OffsetMax = "300 -135"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Content", Layer + ".Clan.Rating");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player.UserIDString, RatingTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Rating");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{clan.Top}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Rating");

				#endregion

				#region Members

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "320 -165", OffsetMax = "460 -135"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Content", Layer + ".Clan.Members");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player.UserIDString, MembersTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Members");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{clan.Members.Count}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + ".Clan.Members");

				#endregion

				#region Ally

				if (_config.AllianceSettings.Enabled && playerClan != null)
				{
					if (playerClan.IsModerator(player.userID) &&
					    _invites.CanSendAllyInvite(clanTag, playerClan.ClanTag)
					   )
						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = "0 -200", OffsetMax = "140 -175"
							},
							Text =
							{
								Text = Msg(player.UserIDString, SendAllyInvite),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color2.Get(),
								Command = $"UI_Clans allyinvite send {clanTag}",
								Close = Layer
							}
						}, Layer + ".Content");

					if (playerClan.Alliances.Contains(clanTag))
						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = "0 -200", OffsetMax = "140 -175"
							},
							Text =
							{
								Text = Msg(player.UserIDString, AllyRevokeTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color6.Get(),
								Command = $"UI_Clans allyinvite revoke {clanTag}",
								Close = Layer
							}
						}, Layer + ".Content");
				}

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private const int
			UI_SKIN_SELECTION_PAGE_SIZE = 10,
			UI_SKIN_SELECTION_PAGE_LINES = 5,
			UI_SKIN_SELECTION_TOTAL_AMOUNT = UI_SKIN_SELECTION_PAGE_SIZE * UI_SKIN_SELECTION_PAGE_LINES;

		private const float
			UI_SKIN_SELECTION_ITEM_HEIGHT = 115f,
			UI_SKIN_SELECTION_ITEM_WIDTH = 110f,
			UI_SKIN_SELECTION_ITEM_MARGIN = 10f;

		private void ShowClanSkinSelectionUI(BasePlayer player, string shortName, int page = 0, bool First = false)
		{
#if TESTING
			using (new StopwatchWrapper("select skin ui"))
#endif
			{
				#region Fields

				var clan = FindClanByPlayer(player.UserIDString);

				if (!clan.Skins.TryGetValue(shortName, out var nowSkin))
					nowSkin = 0;

				var constSwitchX = -(UI_SKIN_SELECTION_PAGE_SIZE * UI_SKIN_SELECTION_ITEM_WIDTH +
				                     (UI_SKIN_SELECTION_PAGE_SIZE - 1) * UI_SKIN_SELECTION_ITEM_MARGIN) / 2f;

				var ySwitch = -75f;

				var canCustomSkin = _config.Skins.CanCustomSkin &&
				                    (string.IsNullOrEmpty(_config.Skins.Permission) ||
				                     player.HasPermission(_config.Skins.Permission));

				#endregion

				var container = new CuiElementContainer();

				#region Background

				if (First)
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

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Close = Layer
						}
					}, Layer);
				}

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
						Color = _config.UI.Color1.Get()
					}
				}, Layer, Layer + ".Main", Layer + ".Main");

				#region Header

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -55", OffsetMax = "0 0"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Main", Layer + ".Header");

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "25 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player.UserIDString, SelectSkinTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + ".Header");

				#endregion

				#region Enter Skin

				if (canCustomSkin)
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5",
							OffsetMin = "160 -17.5", OffsetMax = "410 17.5"
						},
						Image =
						{
							Color = _config.UI.Color4.Get()
						}
					}, Layer + ".Header", Layer + ".Header.EnterSkin");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Header.EnterSkin",
						Components =
						{
							new CuiInputFieldComponent
							{
								FontSize = 12,
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								Command = $"UI_Clans setskin {shortName} ",
								Color = "1 1 1 0.65",
								CharsLimit = 32,
								NeedsKeyboard = true,
								Text = nowSkin == 0 ? Msg(player.UserIDString, EnterSkinTitle) : $"{nowSkin}"
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

				var xSwitch = canCustomSkin ? 415f : 160f;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = $"{xSwitch} -17.5", OffsetMax = $"{xSwitch + 35} 17.5"
					},
					Text =
					{
						Text = Msg(player.UserIDString, BackPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get(),
						Command = page != 0 ? $"UI_Clans editskin {shortName} {page - 1}" : ""
					}
				}, Layer + ".Header");

				xSwitch += 40;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = $"{xSwitch} -17.5", OffsetMax = $"{xSwitch + 35} 17.5"
					},
					Text =
					{
						Text = Msg(player.UserIDString, NextPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = _config.Skins.ItemSkins[shortName].Count > (page + 1) * UI_SKIN_SELECTION_TOTAL_AMOUNT
							? $"UI_Clans editskin {shortName} {page + 1}"
							: ""
					}
				}, Layer + ".Header");

				#endregion

				#region Close

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-35 -12.5",
						OffsetMax = "-10 12.5"
					},
					Text =
					{
						Text = Msg(player.UserIDString, CloseTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = "UI_Clans page 6"
					}
				}, Layer + ".Header");

				#endregion

				#endregion

				#region Items

				xSwitch = constSwitchX;

				var skins = _config.Skins.ItemSkins[shortName].SkipAndTake(page * UI_SKIN_SELECTION_TOTAL_AMOUNT,
					UI_SKIN_SELECTION_TOTAL_AMOUNT);
				for (var i = 0; i < skins.Count; i++)
				{
					var def = skins[i];

					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = $"{xSwitch} {ySwitch - UI_SKIN_SELECTION_ITEM_HEIGHT}",
								OffsetMax = $"{xSwitch + UI_SKIN_SELECTION_ITEM_WIDTH} {ySwitch}"
							},
							Image =
							{
								Color = nowSkin == def
									? _config.UI.Color4.Get()
									: _config.UI.Color3.Get()
							}
						}, Layer + ".Main", Layer + $".Item.{i}");

					container.Add(new CuiElement
					{
						Parent = Layer + $".Item.{i}",
						Components =
						{
							new CuiImageComponent
							{
								ItemId = FindItemID(shortName),
								SkinId = def
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-35 -80", OffsetMax = "35 -10"
							}
						}
					});

					container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "0 0", OffsetMax = "0 25"
							},
							Text =
							{
								Text = Msg(player.UserIDString, SelectTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color2.Get(),
								Command = $"UI_Clans selectskin {shortName} {def}"
							}
						}, Layer + $".Item.{i}");

					if ((i + 1) % UI_SKIN_SELECTION_PAGE_SIZE == 0)
					{
						xSwitch = constSwitchX;
						ySwitch = ySwitch - UI_SKIN_SELECTION_ITEM_HEIGHT - UI_SKIN_SELECTION_ITEM_MARGIN;
					}
					else
					{
						xSwitch += UI_SKIN_SELECTION_ITEM_WIDTH + UI_SKIN_SELECTION_ITEM_MARGIN;
					}
				}

				#endregion

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private void ShowClanConfirmResourceUI(BasePlayer player, string action, params string[] args)
		{
#if TESTING
			using (new StopwatchWrapper("input and action"))
#endif
			{
				var container = new CuiElementContainer();

				var headTitle = args[0];
				var msg = args[1];
				var slot = args[2];

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = _config.UI.Color8.Get()}
				}, "Overlay", ModalLayer, ModalLayer);

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-125 -80",
						OffsetMax = "125 80"
					},
					Image =
					{
						Color = _config.UI.Color1.Get()
					}
				}, ModalLayer, ModalLayer + ".Main");

				#region Header

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -45", OffsetMax = "0 0"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, ModalLayer + ".Main", ModalLayer + ".Header");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "12.5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{headTitle}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
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
						Text = Msg(player.UserIDString, CloseTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Close = ModalLayer,
						Color = _config.UI.Color2.Get()
					}
				}, ModalLayer + ".Header");

				#endregion

				#region Message

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "15 -110",
						OffsetMax = "-15 -60"
					},
					Text =
					{
						Text = msg,
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, ModalLayer + ".Main");

				#endregion

				#region Buttons

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-90 10",
						OffsetMax = "-10 35"
					},
					Text =
					{
						Text = Msg(player.UserIDString, CancelTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color7.Get(),
						Close = ModalLayer
					}
				}, ModalLayer + ".Main");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "10 10",
						OffsetMax = "90 35"
					},
					Text =
					{
						Text = Msg(player.UserIDString, AcceptTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = $"UI_Clans confirm {action} accept {slot}",
						Close = ModalLayer
					}
				}, ModalLayer + ".Main");

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		private void ShowInputAndActionUI(BasePlayer player, string action, params string[] args)
		{
#if TESTING
			using (new StopwatchWrapper("input and action"))
#endif
			{
				var container = new CuiElementContainer();

				var headTitle = args[0];
				var msg = args[1];
				var inputValue = args[2];

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = _config.UI.Color8.Get()}
				}, "Overlay", ModalLayer, ModalLayer);

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-125 -90",
						OffsetMax = "125 90"
					},
					Image =
					{
						Color = _config.UI.Color1.Get()
					}
				}, ModalLayer, ModalLayer + ".Main");

				#region Header

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -45", OffsetMax = "0 0"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, ModalLayer + ".Main", ModalLayer + ".Header");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "12.5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{headTitle}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
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
						Text = Msg(player.UserIDString, CloseTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Close = ModalLayer,
						Color = _config.UI.Color2.Get()
					}
				}, ModalLayer + ".Header");

				#endregion

				#region Message

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "15 -100",
						OffsetMax = "-15 -60"
					},
					Text =
					{
						Text = msg,
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, ModalLayer + ".Main");

				#endregion

				#region Input

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "15 -135",
						OffsetMax = "-15 -105"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, ModalLayer + ".Main", ModalLayer + ".Input");

				container.Add(new CuiElement
				{
					Parent = ModalLayer + ".Input",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							Command = $"UI_Clans action input {action}",
							Color = "1 1 1 0.65",
							CharsLimit = 128,
							NeedsKeyboard = true,
							Text = string.IsNullOrEmpty(inputValue) ? string.Empty : $"{inputValue.Replace("–", " ")}"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "5 0",
							OffsetMax = "-5 0"
						}
					}
				});

				#endregion

				#region Buttons

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-90 10",
						OffsetMax = "-10 35"
					},
					Text =
					{
						Text = Msg(player.UserIDString, CancelTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color7.Get(),
						Close = ModalLayer
					}
				}, ModalLayer + ".Main");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "10 10",
						OffsetMax = "90 35"
					},
					Text =
					{
						Text = Msg(player.UserIDString, AcceptTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = $"UI_Clans action accept {action} {inputValue}",
						Close = ModalLayer
					}
				}, ModalLayer + ".Main");

				#endregion

				CuiHelper.AddUi(player, container);
			}
		}

		#region UI Components

		private void UpdateAvatar(ClanData clan, BasePlayer player, string avatarKey = "")
		{
			var avatar = MenuAvatarUI(clan, avatarKey);

			avatar.Update = true;

			UpdateUI(player, avatar.DestroyUi, container => container.Add(avatar));
		}

		private void UpdateUI(BasePlayer player, string destroyLayer, Action<CuiElementContainer> callback)
		{
			CuiHelper.DestroyUi(player, destroyLayer);

			var cont = new CuiElementContainer();

			callback.Invoke(cont);

			CuiHelper.AddUi(player, cont);
		}

		private CuiElement MenuAvatarUI(ClanData clan, string avatar = "")
		{
			return new CuiElement
			{
				Name = Layer + ".Content.Avatar",
				Parent = Layer + ".Content",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(
							!string.IsNullOrEmpty(avatar)
								? avatar
								: string.IsNullOrEmpty(clan.Avatar)
									? _config.Avatar.DefaultAvatar
									: $"clanavatar_{clan.ClanTag}")
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "0 -170", OffsetMax = "140 -30"
					}
				}
			};
		}

		private void HeaderUi(ref CuiElementContainer container, BasePlayer player, ClanData clan, int page,
			string headTitle,
			string backPage = "",
			bool showClose = true)
		{
#if TESTING
			using (new StopwatchWrapper("header ui"))
#endif
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -45", OffsetMax = "0 0"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Main", Layer + ".Header", Layer + ".Header");

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "12.5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{headTitle}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + ".Header");

				#endregion

				#region Close

				if (showClose)
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
							Text = Msg(player.UserIDString, CloseTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						},
						Button =
						{
							Close = Layer,
							Color = _config.UI.Color2.Get(),
							Command = "UI_Clans close"
						}
					}, Layer + ".Header");

				#endregion

				#region Back

				var hasBack = !string.IsNullOrEmpty(backPage);

				if (hasBack)
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-65 -37.5",
							OffsetMax = "-40 -12.5"
						},
						Text =
						{
							Text = Msg(player.UserIDString, BackPage),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Color2.Get(),
							Command = $"{backPage}"
						}
					}, Layer + ".Header");

				#endregion

				#region Invites

				if (clan != null && clan.IsModerator(player.userID))
				{
					if (page == 65 || page == 71 || page == 72)
					{
						if (_config.AllianceSettings.Enabled)
						{
							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "1 1", AnchorMax = "1 1",
									OffsetMin = "-470 -37.5",
									OffsetMax = "-330 -12.5"
								},
								Text =
								{
									Text = Msg(player.UserIDString, AllyInvites),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = page == 71 ? _config.UI.Color2.Get() : _config.UI.Color4.Get(),
									Command = "UI_Clans page 71"
								}
							}, Layer + ".Header");

							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "1 1", AnchorMax = "1 1",
									OffsetMin = "-325 -37.5",
									OffsetMax = "-185 -12.5"
								},
								Text =
								{
									Text = Msg(player.UserIDString, IncomingAllyTitle),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = page == 72 ? _config.UI.Color2.Get() : _config.UI.Color4.Get(),
									Command = "UI_Clans page 72"
								}
							}, Layer + ".Header");
						}

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "1 1", AnchorMax = "1 1",
								OffsetMin = "-180 -37.5",
								OffsetMax = "-40 -12.5"
							},
							Text =
							{
								Text = Msg(player.UserIDString, ClanInvitesTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = page == 65 ? _config.UI.Color2.Get() : _config.UI.Color4.Get(),
								Command = "UI_Clans page 65"
							}
						}, Layer + ".Header");
					}
					else
					{
						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "1 1", AnchorMax = "1 1",
								OffsetMin = $"{(hasBack ? -220 : -180)} -37.5",
								OffsetMax = $"{(hasBack ? -70 : -40)} -12.5"
							},
							Text =
							{
								Text = Msg(player.UserIDString, InvitesTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _config.UI.Color4.Get(),
								Command = "UI_Clans page 65"
							}
						}, Layer + ".Header");
					}
				}

				#endregion

				#region Notify

				if (HasInvite(player))
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-215 -37.5",
							OffsetMax = "-40 -12.5"
						},
						Image =
						{
							Color = _config.UI.Color2.Get()
						}
					}, Layer + ".Header", Layer + ".Header.Invite");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 0", OffsetMax = "-5 0"
						},
						Text =
						{
							Text = Msg(player.UserIDString, InvitedToClan),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + ".Header.Invite");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 0", OffsetMax = "-5 0"
						},
						Text =
						{
							Text = Msg(player.UserIDString, NextPage),
							Align = TextAnchor.MiddleRight,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + ".Header.Invite");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command = "UI_Clans page 45"
						}
					}, Layer + ".Header.Invite");
				}

				#endregion
			}
		}

		private void MenuUi(ref CuiElementContainer container, BasePlayer player, int page, ClanData clan = null)
		{
#if TESTING
			using (new StopwatchWrapper("menu ui"))
#endif
			{
				var data = PlayerData.GetOrCreate(player.UserIDString);

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0",
						OffsetMin = "10 10",
						OffsetMax = "185 380"
					},
					Image =
					{
						Color = _config.UI.Color3.Get()
					}
				}, Layer + ".Main", Layer + ".Menu", Layer + ".Menu");

				#region Pages

				var ySwitch = 0f;
				var Height = 35f;
				var Margin = 0f;

				foreach (var pageSettings in GetAvailablePages(player))
				{
					if (clan == null)
						switch (pageSettings.ID)
						{
							case 2:
							case 3:
								break;
							default:
								continue;
						}

					switch (pageSettings.ID)
					{
						case 5:
							if (clan != null && !clan.IsModerator(player.userID)) continue;
							break;
						case 7:
							if (!_config.AllianceSettings.Enabled) continue;
							break;
					}

					container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"0 {ySwitch - Height}",
								OffsetMax = $"0 {ySwitch}"
							},
							Text =
							{
								Text = $"     {Msg(player.UserIDString, pageSettings.Key)}",
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = pageSettings.ID == page ? _config.UI.Color7.Get() : "0 0 0 0",
								Command = $"UI_Clans page {pageSettings.ID}"
							}
						}, Layer + ".Menu", Layer + $".Menu.Page.{pageSettings.Key}");

					if (pageSettings.ID == page)
						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "0 1",
									OffsetMin = "0 0", OffsetMax = "5 0"
								},
								Image =
								{
									Color = _config.UI.Color2.Get()
								}
							}, Layer + $".Menu.Page.{pageSettings.Key}");

					ySwitch = ySwitch - Height - Margin;
				}

				#endregion

				#region Notify

				if (clan == null)
				{
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-75 10", OffsetMax = "75 40"
						},
						Text =
						{
							Text = Msg(player.UserIDString, CreateClanTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Color2.Get(),
							Command = "UI_Clans createclan"
						}
					}, Layer + ".Menu");
				}
				else
				{
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-75 10", OffsetMax = "75 40"
						},
						Text =
						{
							Text = Msg(player.UserIDString, ProfileTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Color2.Get(),
							Command = $"UI_Clans showprofile {player.userID}"
						}
					}, Layer + ".Menu");

					if (_config.FriendlyFire.UseFriendlyFire)
					{
						if (!_config.FriendlyFire.GeneralFriendlyFire || _config.FriendlyFire.PlayersGeneralFF ||
						    (_config.FriendlyFire.ModersGeneralFF && clan.IsModerator(player.userID)) ||
						    clan.IsOwner(player.userID)) ButtonFriendlyFire(ref container, player, data);

						if (_config.AllianceSettings.Enabled && _config.AllianceSettings.UseFF &&
						    (!_config.AllianceSettings.GeneralFriendlyFire ||
						     _config.AllianceSettings.PlayersGeneralFF ||
						     (_config.AllianceSettings.ModersGeneralFF && clan.IsModerator(player.userID)) ||
						     clan.IsOwner(player.userID)))
							ButtonAlly(ref container, player, data);
					}
				}

				#endregion
			}
		}

		private void PagesUi(ref CuiElementContainer container, BasePlayer player, int pages, int page,
			int zPage)
		{
#if TESTING
			using (new StopwatchWrapper("pages ui"))
#endif
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-25 10",
						OffsetMax = "25 35"
					},
					Image =
					{
						Color = _config.UI.Color4.Get()
					}
				}, Layer + ".Content", Layer + ".Pages");

				container.Add(new CuiElement
				{
					Parent = Layer + ".Pages",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							Command = $"UI_Clans inputpage {pages} {page} ",
							Color = "1 1 1 0.65",
							CharsLimit = 32,
							NeedsKeyboard = true,
							Text = $"{zPage + 1}"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-55 10",
						OffsetMax = "-30 35"
					},
					Text =
					{
						Text = Msg(player.UserIDString, BackPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get(),
						Command = zPage != 0 ? $"UI_Clans page {page} {zPage - 1}" : ""
					}
				}, Layer + ".Content");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "30 10",
						OffsetMax = "55 35"
					},
					Text =
					{
						Text = Msg(player.UserIDString, NextPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color2.Get(),
						Command = pages > zPage + 1 ? $"UI_Clans page {page} {zPage + 1}" : ""
					}
				}, Layer + ".Content");
			}
		}

		private void ButtonFriendlyFire(ref CuiElementContainer container, BasePlayer player, PlayerData data)
		{
#if TESTING
			using (new StopwatchWrapper("buton ff ui"))
#endif
			{
				var clan = FindClanByPlayer(player.UserIDString);

				var allyEnabled = _config.AllianceSettings.Enabled && _config.AllianceSettings.UseFF &&
				                  (!_config.AllianceSettings.GeneralFriendlyFire ||
				                   _config.AllianceSettings.PlayersGeneralFF ||
				                   (_config.AllianceSettings.ModersGeneralFF &&
				                    clan.IsModerator(player.userID)) ||
				                   clan.IsOwner(player.userID));

				var value = _config.FriendlyFire.GeneralFriendlyFire ? clan.FriendlyFire : data.FriendlyFire;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-75 50",
						OffsetMax = $"{(allyEnabled ? 15 : 75)} 80"
					},
					Text =
					{
						Text = Msg(player.UserIDString, FriendlyFireTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = value ? _config.UI.Color2.Get() : _config.UI.Color4.Get(),
						Command = "UI_Clans ff"
					}
				}, Layer + ".Menu", Layer + ".Menu.Button.FF", Layer + ".Menu.Button.FF");
			}
		}

		private void ButtonAlly(ref CuiElementContainer container, BasePlayer player, PlayerData data)
		{
#if TESTING
			using (new StopwatchWrapper("button ally"))
#endif
			{
				var value = _config.AllianceSettings.GeneralFriendlyFire
					? FindClanByPlayer(player.UserIDString).AllyFriendlyFire
					: data.AllyFriendlyFire;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "20 50",
						OffsetMax = "75 80"
					},
					Text =
					{
						Text = Msg(player.UserIDString, AllyFriendlyFireTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = value ? _config.UI.Color4.Get() : _config.UI.Color6.Get(),
						Command = "UI_Clans allyff"
					}
				}, Layer + ".Menu", Layer + ".Menu.Button.Ally", Layer + ".Menu.Button.Ally");
			}
		}

		private void ButtonClanSkins(ref CuiElementContainer container, BasePlayer player, PlayerData data)
		{
#if TESTING
			using (new StopwatchWrapper("buton clan skins"))
#endif
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-285 -37.5",
						OffsetMax = "-185 -12.5"
					},
					Text =
					{
						Text = Msg(player.UserIDString, UseClanSkins),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = data.ClanSkins ? _config.UI.Color2.Get() : _config.UI.Color4.Get(),
						Command = "UI_Clans clanskins"
					}
				}, Layer + ".Header", Layer + ".Header.Use.ClanSkins", Layer + ".Header.Use.ClanSkins");
			}
		}

		#endregion

		#endregion

		#region Utils

		private List<PageSettings> GetAvailablePages(BasePlayer player)
		{
			return _config.Pages.FindAll(p =>
				p.Enabled && (string.IsNullOrEmpty(p.Permission) || player.HasPermission(p.Permission)));
		}

		private List<ulong> FindAvailablePlayers(string search, ClanData clan)
		{
#if TESTING
			return _testingPlayers.Keys.ToList();
#else
			return clan.Members.FindAll(member =>
			{
				var displayName = GetPlayerName(member);
				return string.IsNullOrEmpty(search) ||
				       search.Length <= 2 ||
				       displayName.StartsWith(search) ||
				       displayName.Contains(search) ||
				       displayName.EndsWith(search);
			});
#endif
		}

		#region Wipe

		private Coroutine _wipePlayers;

		private IEnumerator StartOnAllPlayers(string[] players, Action<string> callback = null)
		{
			for (var i = 0; i < players.Length; i++)
			{
				callback?.Invoke(players[i]);

				if (i % 10 == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}

			_wipePlayers = null;
		}

		#endregion

		private string GetNameColor(ulong userId, BasePlayer player = null)
		{
			var user = ServerUsers.Get(userId);
			var userGroup = user?.group ?? ServerUsers.UserGroup.None;
			var isOwner = userGroup == ServerUsers.UserGroup.Owner ||
			              userGroup == ServerUsers.UserGroup.Moderator;
			var isDeveloper = player != null
				? (player.IsDeveloper ? 1 : 0)
				: (DeveloperList.Contains(userId) ? 1 : 0);

			var nameColor = "#5af";
			if (isOwner)
				nameColor = "#af5";
			if (isDeveloper != 0)
				nameColor = "#fa5";
			return nameColor;
		}

		#region Arena Tournament

		private bool AT_IsOnTournament(ulong userID)
		{
			return Convert.ToBoolean(ArenaTournament?.Call("IsOnTournament", userID));
		}

		#endregion

		#region PlayerSkins

		private bool LoadSkinsFromPlayerSkins()
		{
#if TESTING
			using (new StopwatchWrapper("load skins from playerskins"))
#endif
			{
				if (!_config.Skins.UsePlayerSkins) return false;

				Dictionary<string, Dictionary<ulong, PlayerSkinsSkinData>> skinData;
				try
				{
					skinData = Interface.Oxide.DataFileSystem
						.ReadObject<Dictionary<string, Dictionary<ulong, PlayerSkinsSkinData>>>(
							"PlayerSkins/skinlist");
				}
				catch
				{
					skinData = new Dictionary<string, Dictionary<ulong, PlayerSkinsSkinData>>();
				}

				if (skinData != null)
				{
					_config.Skins.ItemSkins = skinData.ToDictionary(x => x.Key, x => x.Value.Keys.ToList());
					return true;
				}

				return false;
			}
		}


		private class PlayerSkinsSkinData
		{
			public string permission = string.Empty;
			public int cost = 1;
			public bool isDisabled = false;
		}

		#endregion

		#region LSkins

		private bool LoadSkinsFromLSkins()
		{
#if TESTING
			using (new StopwatchWrapper("load skins from LSkins"))
#endif
			{
				if (!_config.Skins.UseLSkins) return false;

				var itemSkins = new Dictionary<string, List<ulong>>();

				foreach (var cfgWeaponSkin in Interface.Oxide.DataFileSystem.GetFiles("LSkins/Skins/"))
				{
					var text = cfgWeaponSkin.Remove(0, cfgWeaponSkin.IndexOf("/Skins/", StringComparison.Ordinal) + 7);
					var text2 = text.Remove(text.IndexOf(".json", StringComparison.Ordinal),
						text.Length - text.IndexOf(".json", StringComparison.Ordinal));

					Dictionary<ulong, LSkinsSkinInfo> skins = null;
					try
					{
						skins = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, LSkinsSkinInfo>>(
							$"LSkins/Skins/{text2}");
					}
					catch
					{
						// ignored
					}

					if (skins is {Count: > 0})
						itemSkins[text2] = skins.Where(x => x.Value.IsEnabled && x.Key != 0UL).Select(x => x.Key);
				}

				if (itemSkins.Count > 0)
				{
					_config.Skins.ItemSkins = itemSkins;
					return true;
				}

				return false;
			}
		}

		private class LSkinsSkinInfo
		{
			[JsonProperty("Enabled skin?(true = yes)")]
			//[JsonProperty("Включить скин?(true = да)")]
			public bool IsEnabled = true;

			[JsonProperty("Is this skin from the developers of rust or take it in a workshop?")]
			// [JsonProperty("Этот скин есть от разработчиков раста или принять в воркшопе??(true = да)")]
			public bool IsApproved = true;

			[JsonProperty("Name skin")]
			// [JsonProperty("Название скина")]
			public string MarketName = "Warhead LR300";
		}

		#endregion

		#region PlayTime

		private double PlayTimeRewards_GetPlayTime(string playerid)
		{
#if TESTING
			using (new StopwatchWrapper("playtimerewards getpaytime"))
#endif
			{
				return Convert.ToDouble(PlayTimeRewards?.Call("FetchPlayTime", playerid));
			}
		}

		private static string FormatTime(double seconds)
		{
#if TESTING
			using (new StopwatchWrapper("format time"))
#endif
			{
				var time = TimeSpan.FromSeconds(seconds);

				var result =
					$"{(time.Duration().Days > 0 ? $"{time.Days:0} Day{(time.Days == 1 ? string.Empty : "s")} " : string.Empty)}{(time.Duration().Hours > 0 ? $"{time.Hours:0} Hour{(time.Hours == 1 ? string.Empty : "s")} " : string.Empty)}{(time.Duration().Minutes > 0 ? $"{time.Minutes:0} Min " : string.Empty)}{(time.Duration().Seconds > 0 ? $"{time.Seconds:0} Sec" : string.Empty)}";

				if (result.EndsWith(", ")) result = result.Substring(0, result.Length - 2);

				if (string.IsNullOrEmpty(result)) result = "0 Seconds";

				return result;
			}
		}

		#endregion

		private bool IsValidURL(string url)
		{
#if TESTING
			using (new StopwatchWrapper("is valid url"))
#endif
			{
				return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
				       && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
			}
		}

		private void UnsubscribeHooks()
		{
#if TESTING
			using (new StopwatchWrapper("unsunbscribe hooks"))
#endif
			{
				if (!_config.ClanCreateTeam)
					Unsubscribe(nameof(OnTeamCreate));

				if (!_config.ClanTeamLeave)
					Unsubscribe(nameof(OnTeamLeave));

				if (!_config.ClanTeamKick)
					Unsubscribe(nameof(OnTeamKick));

				if (!_config.ClanTeamInvite)
					Unsubscribe(nameof(OnTeamInvite));

				if (!_config.ClanTeamPromote)
					Unsubscribe(nameof(OnTeamPromote));

				if (!_config.ClanTeamInvite && !_config.ClanTeamAcceptInvite)
					Unsubscribe(nameof(OnTeamAcceptInvite));

				if (!_config.Skins.UseSkinner)
					Unsubscribe(nameof(OnSkinnerCacheUpdated));

				if (!_config.Skins.UseSkinBox)
					Unsubscribe(nameof(OnSkinBoxSkinsLoaded));

				if (!_config.Statistics.Kills)
					Unsubscribe(nameof(OnPlayerDeath));

				if (!_config.Statistics.Gather)
				{
					Unsubscribe(nameof(OnCollectiblePickup));
					Unsubscribe(nameof(OnCropGather));
					Unsubscribe(nameof(OnDispenserBonus));
					Unsubscribe(nameof(OnDispenserGather));
				}

				if (!_config.Statistics.Loot)
				{
					Unsubscribe(nameof(OnLootEntity));
					Unsubscribe(nameof(OnContainerDropItems));
					Unsubscribe(nameof(OnItemPickup));
				}

				if (!_config.Statistics.Entities)
				{
					Unsubscribe(nameof(OnLootEntity));
					Unsubscribe(nameof(OnEntityDeath));
				}

				if (!_config.Statistics.Craft) Unsubscribe(nameof(OnItemCraftFinished));

				if (!_config.FriendlyFire.UseTurretsFF) Unsubscribe(nameof(OnTurretTarget));
				
				Unsubscribe(nameof(OnChatReferenceTags));
				Unsubscribe(nameof(OnPlayerChat));
				
			}
		}

		private static bool IsOnline(ulong member)
		{
#if TESTING
			using (new StopwatchWrapper("is online"))
#endif
			{
				var player = RelationshipManager.FindByID(member);
				return player != null && player.IsConnected;
			}
		}

		private string GetPlayerName(ulong userID)
		{
#if TESTING
			if (_testingPlayers.TryGetValue(userID, out var name))
				return name;
#endif

			if (TopPlayers.TryGetValue(userID, out var topPlayer))
				return topPlayer.DisplayName;

			var player = covalence.Players.FindPlayerById(userID.ToString());
			if (player != null)
				return player.Name;

			var data = PlayerData.GetNotLoad(userID.ToString());
			return data != null ? data.DisplayName : userID.ToString();
		}

		private static string GetPlayerName(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("get player name"))
#endif
			{
				if (player == null || !player.userID.IsSteamId())
					return string.Empty;

				if (player.net?.connection == null)
				{
					var covPlayer = player.IPlayer;
					if (covPlayer != null)
						return covPlayer.Name;

					return player.UserIDString;
				}

				var value = player.net.connection.username;
				var str = value.ToPrintable(32).EscapeRichText().Trim();
				if (string.IsNullOrWhiteSpace(str))
				{
					str = player.IPlayer.Name;
					if (string.IsNullOrWhiteSpace(str))
						str = player.UserIDString;
				}

				return str;
			}
		}

		private bool CanUseSkins(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("can use skins"))
#endif
			{
				var data = PlayerData.GetNotLoad(player.UserIDString);
				if (data == null) return false;

				if (_config.Skins.DisableSkins)
					return data.ClanSkins && (!_config.PermissionSettings.UsePermClanSkins ||
					                          string.IsNullOrEmpty(_config.PermissionSettings.ClanSkins) ||
					                          player.HasPermission(_config.PermissionSettings.ClanSkins));

				return true;
			}
		}

		private void RegisterCommands()
		{
#if TESTING
			using (new StopwatchWrapper("register commands"))
#endif
			{
				AddCovalenceCommand(_config.ClanCommands.ToArray(), nameof(CmdClans));

				AddCovalenceCommand(_config.ClanInfoCommands, nameof(CmdClanInfo));

				AddCovalenceCommand("clans.manage", nameof(CmdAdminClans));

				if (_config.FriendlyFire.UseFriendlyFire)
				{
					AddCovalenceCommand(_config.Commands.ClansFF, nameof(CmdClanFF));

					AddCovalenceCommand(_config.Commands.AllyFF, nameof(CmdAllyFF));
				}

				if (_config.ChatSettings.EnabledClanChat)
					AddCovalenceCommand(_config.ChatSettings.ClanChatCommands, nameof(ClanChatClan));

				if (_config.ChatSettings.EnabledAllianceChat)
					AddCovalenceCommand(_config.ChatSettings.AllianceChatCommands, nameof(ClanChatAlly));
			}
		}

		private void RegisterPermissions()
		{
#if TESTING
			using (new StopwatchWrapper("register permissions"))
#endif
			{
				permission.RegisterPermission(PermAdmin, this);

				if (_config.PermissionSettings.UsePermClanCreating &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanCreating) &&
				    !permission.PermissionExists(_config.PermissionSettings.ClanCreating))
					permission.RegisterPermission(_config.PermissionSettings.ClanCreating, this);

				if (_config.PermissionSettings.UsePermClanJoining &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanJoining) &&
				    !permission.PermissionExists(_config.PermissionSettings.ClanJoining))
					permission.RegisterPermission(_config.PermissionSettings.ClanJoining, this);

				if (_config.PermissionSettings.UsePermClanKick &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanKick) &&
				    !permission.PermissionExists(_config.PermissionSettings.ClanKick))
					permission.RegisterPermission(_config.PermissionSettings.ClanKick, this);

				if (_config.PermissionSettings.UsePermClanLeave &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanLeave) &&
				    !permission.PermissionExists(_config.PermissionSettings.ClanLeave))
					permission.RegisterPermission(_config.PermissionSettings.ClanLeave, this);

				if (_config.PermissionSettings.UsePermClanDisband &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanDisband) &&
				    !permission.PermissionExists(_config.PermissionSettings.ClanDisband))
					permission.RegisterPermission(_config.PermissionSettings.ClanDisband, this);

				if (_config.PermissionSettings.UsePermClanSkins &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanSkins) &&
				    !permission.PermissionExists(_config.PermissionSettings.ClanSkins))
					permission.RegisterPermission(_config.PermissionSettings.ClanSkins, this);

				if (_config.Skins.CanCustomSkin &&
				    !string.IsNullOrEmpty(_config.Skins.Permission) &&
				    !permission.PermissionExists(_config.Skins.Permission))
					permission.RegisterPermission(_config.Skins.Permission, this);

				if (!string.IsNullOrEmpty(_config.Avatar.PermissionToChange) &&
				    !permission.PermissionExists(_config.Avatar.PermissionToChange))
					permission.RegisterPermission(_config.Avatar.PermissionToChange, this);

				_config.Pages.ForEach(page =>
				{
					if (!string.IsNullOrEmpty(page.Permission) && !permission.PermissionExists(page.Permission))
						permission.RegisterPermission(page.Permission, this);
				});
			}
		}

		private void LoadPlayers()
		{
#if TESTING
			using (new StopwatchWrapper("load players"))
#endif
			{
				foreach (var player in BasePlayer.activePlayerList)
					OnPlayerConnected(player);
			}
		}

		private void LoadAlliances()
		{
#if TESTING
			using (new StopwatchWrapper("load alliances"))
#endif
			{
				if (_config.AutoTeamCreation && _config.AllianceSettings.AllyAddPlayersTeams)
				{
					if (_config.LimitSettings.AlliancesLimit > 1)
					{
						PrintWarning(
							"When using the \"Add players from the clan alliance to in-game teams?\" parameter, it is not possible to have a limit on the number of alliances greater than one. The limit is set to 1.");

						_config.LimitSettings.AlliancesLimit = 1;
					}
				}
			}
		}

		private void LoadChat()
		{
#if TESTING
			using (new StopwatchWrapper("load chat"))
#endif
			{
				if (_config.ChatSettings.Enabled)
				{
					if (_config.ChatSettings.WorkingWithBetterChat)
						BetterChat?.Call("API_RegisterThirdPartyTitle", this,
							new Func<IPlayer, string>(BetterChat_FormattedClanTag));

					if (_config.ChatSettings.WorkingWithIQChat) 
						Subscribe(nameof(OnChatReferenceTags));
					
					if (_config.ChatSettings.WorkingWithInGameChat) 
						Subscribe(nameof(OnPlayerChat));
				}
			}
		}

		private void PurgeClans()
		{
#if TESTING
			using (new StopwatchWrapper("purge clans"))
#endif
			{
				if (_config.PurgeSettings.Enabled)
				{
					var toRemove = Pool.Get<List<ClanData>>();

					_clansList.ForEach(clan =>
					{
						if (DateTime.Now.Subtract(clan.LastOnlineTime).Days > _config.PurgeSettings.OlderThanDays)
							toRemove.Add(clan);
					});

					if (_config.PurgeSettings.ListPurgedClans)
					{
						var str = string.Join("\n",
							toRemove.Select(clan =>
								$"Purged - [{clan.ClanTag}] | Owner: {clan.LeaderID} | Last Online: {clan.LastOnlineTime}"));

						if (!string.IsNullOrEmpty(str))
							Puts(str);
					}

					toRemove.ForEach(clan =>
					{
						_clanByTag.Remove(clan.ClanTag);
						_clansList.Remove(clan);
					});

					Pool.FreeUnmanaged(ref toRemove);
				}
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

				_enabledImageLibrary = true;

				var imagesList = new Dictionary<string, string>
				{
					[_config.Avatar.DefaultAvatar] = _config.Avatar.DefaultAvatar
				};

				_clansList.ForEach(clan =>
				{
					if (!string.IsNullOrEmpty(clan.Avatar))
						imagesList[$"clanavatar_{clan.ClanTag}"] = clan.Avatar;
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

		private void FillingTeams()
		{
#if TESTING
			using (new StopwatchWrapper("filling teams"))
#endif
			{
				if (_config.AutoTeamCreation)
				{
					if (_config.AllianceSettings.AllyAddPlayersTeams)
					{
						RelationshipManager.maxTeamSize = _config.LimitSettings.MemberLimit * 2;
					}
					else
					{
						RelationshipManager.maxTeamSize = _config.LimitSettings.MemberLimit;
					}

					_clansList.ForEach(clan =>
					{
						clan.FindOrCreateTeam();

						clan.Members.ForEach(member => clan.AddPlayer(member));
					});
				}
			}
		}

		private static string HexToCuiColor(string HEX, float Alpha = 100)
		{
#if TESTING
			using (new StopwatchWrapper("hex to cui color"))
#endif
			{
				if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

				var str = HEX.Trim('#');
				if (str.Length != 6) throw new Exception(HEX);
				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100f}";
			}
		}

		private string BetterChat_FormattedClanTag(IPlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("betterchat formatted clan tag"))
#endif
			{
				var clan = FindClanByPlayer(player.Id);
				return clan == null
					? string.Empty
					: clan.GetFormattedClanTag();
			}
		}

		private bool IsTeammates(ulong player, ulong friend)
		{
#if TESTING
			using (new StopwatchWrapper("is teammates"))
#endif
			{
				return player == friend ||
				       RelationshipManager.ServerInstance.FindPlayersTeam(player)?.members?.Contains(friend) == true ||
				       FindClanByPlayer(player.ToString())?.IsMember(friend) == true;
			}
		}

		#region Avatar
		
		private readonly Regex Regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

		private void GetAvatar(string userId, Action<string> callback)
		{
			if (callback == null) return;

			try
			{
				webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
				{
					if (code != 200 || response == null)
						return;

					var avatar = Regex.Match(response).Groups[1].ToString();
					if (string.IsNullOrEmpty(avatar))
						return;

					callback.Invoke(avatar);
				}, this);
			}
			catch (Exception e)
			{
				PrintError($"{e.Message}");
			}
		}

		private void StartLoadingAvatars()
		{
			Puts("Loading avatars started!");

			_actionAvatars = ServerMgr.Instance.StartCoroutine(LoadAllAvatars());
		}

		private IEnumerator LoadAllAvatars()
		{
			var allPlayers = GetAllPlayersIDs();
			
			var playerIds = new List<string>();

			var totalPlayers = allPlayers.Count;
			var loadedPlayers = 0;

			foreach (var playerID in allPlayers)
			{
				playerIds.Add(playerID);

				if (playerIds.Count >= 100)
				{
					loadedPlayers += playerIds.Count;
					yield return FetchAndAddAvatars(playerIds);
					
					Puts($"Loaded avatars: {loadedPlayers}/{totalPlayers}");

					playerIds.Clear();
				}
			}
			
			if (playerIds.Count > 0)
			{
				loadedPlayers += playerIds.Count;

				yield return FetchAndAddAvatars(playerIds);
				Puts($"Loaded avatars: {loadedPlayers}/{totalPlayers}");
			}
			
			Puts("Uploading avatars is complete!");
		}
		
		private List<string> GetAllPlayersIDs()
		{
			var list = new HashSet<string>(PlayerData.GetFiles());
			foreach (var p in covalence.Players.All) list.Add(p.Id);
			
			return list.ToList();
		}
		
		private IEnumerator FetchAndAddAvatars(List<string> playerIds)
		{
			var avatars = new Dictionary<string, string>();
			
			GetAvatars(playerIds, fetchedAvatars =>
			{
				avatars = fetchedAvatars;
			});

			while (avatars.Count == 0)
				yield return null;

			foreach (var avatar in avatars) 
				AddImage(avatar.Value, $"avatar_{avatar.Key}");

			yield return CoroutineEx.waitForSeconds(0.5f);
		}
        
		private void GetAvatars(List<string> userIds, Action<Dictionary<string, string>> callback)
		{
			if (callback == null || userIds == null || !userIds.Any())
				return;
			
			if (string.IsNullOrEmpty(_config.Avatar.SteamAPIKey))
			{
				PrintError("Steam API key is not set. Please provide a valid API key.");
				return;
			}
			
			var ids = string.Join(",", userIds);
			if (string.IsNullOrWhiteSpace(ids)) return;
			
			var url = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_config.Avatar.SteamAPIKey}&steamids={ids}";

			webrequest.Enqueue(url, null, (code, response) =>
			{
				if (code != 200)
				{
					PrintError($"Error fetching avatars. HTTP code: {code}");
					return;
				}

				if (string.IsNullOrEmpty(response))
				{
					PrintError("Empty response received from Steam API.");
					return;
				}
					
				var avatars = ParseAvatars(response);
				callback?.Invoke(avatars);
			}, this);
		}
		
		private Dictionary<string, string> ParseAvatars(string response)
		{
			var avatars = new Dictionary<string, string>();
			
			var jsonResponse = JObject.Parse(response);
			if (jsonResponse == null)
				return avatars;

			foreach (var player in jsonResponse["response"]["players"])
			{
				var steamId = player["steamid"]?.Value<string>();
				var avatarFull = player["avatarfull"]?.Value<string>();

				if (!string.IsNullOrEmpty(steamId) && !string.IsNullOrEmpty(avatarFull)) 
					avatars[steamId] = avatarFull;
			}

			return avatars;
		}

		#endregion

		private void FillingStandartItems()
		{
#if TESTING
			using (new StopwatchWrapper("filling standart items"))
#endif
			{
				_config.AvailableStandartItems.ForEach(shortName =>
				{
					var def = ItemManager.FindItemDefinition(shortName);
					if (def == null) return;

					_defaultItems.Add(def);
				});
			}
		}

		private static void ApplySkinToItem(Item item, ulong Skin)
		{
#if TESTING
			using (new StopwatchWrapper("apply skin to item"))
#endif
			{
				item.skin = Skin;
				item.MarkDirty();

				var heldEntity = item.GetHeldEntity();
				if (heldEntity == null) return;

				heldEntity.skinID = Skin;
				heldEntity.SendNetworkUpdate();
			}
		}

		private static string GetValue(float value)
		{
#if TESTING
			using (new StopwatchWrapper("get value"))
#endif
			{
				if (!_config.UI.ValueAbbreviation)
					return Mathf.Round(value).ToString(CultureInfo.InvariantCulture);

				var t = string.Empty;
				while (value > 1000)
				{
					t += "K";
					value /= 1000;
				}

				return Mathf.Round(value) + t;
			}
		}

		private string[] ZM_GetPlayerZones(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("get player zones"))
#endif
			{
				return ZoneManager?.Call<string[]>("GetPlayerZoneIDs", player) ?? new string[] { };
			}
		}

		#endregion

		#region API

		private static void ClanCreate(string tag)
		{
			Interface.CallHook("OnClanCreate", tag);
		}

		private static void ClanUpdate(string tag)
		{
			Interface.CallHook("OnClanUpdate", tag);
		}

		private static void ClanDestroy(string tag)
		{
			Interface.CallHook("OnClanDestroy", tag);
		}

		private static void ClanDisbanded(List<string> memberUserIDs)
		{
			Interface.CallHook("OnClanDisbanded", memberUserIDs);
		}

		private static void ClanDisbanded(string tag, List<string> memberUserIDs)
		{
			Interface.CallHook("OnClanDisbanded", tag, memberUserIDs);
		}

		private static void ClanMemberJoined(string userID, string tag)
		{
			Interface.CallHook("OnClanMemberJoined", userID, tag);
		}

		private static void ClanMemberJoined(string userID, List<string> memberUserIDs)
		{
			Interface.CallHook("OnClanMemberJoined", userID, memberUserIDs);
		}

		private static void ClanMemberGone(string userID, List<string> memberUserIDs)
		{
			Interface.CallHook("OnClanMemberGone", userID, memberUserIDs);
		}

		private static void ClanMemberGone(string userID, string tag)
		{
			Interface.CallHook("OnClanMemberGone", userID, tag);
		}

		private static void ClanTopUpdated()
		{
#if TESTING
			SayDebug("[OnClanTopUpdated] called");
#endif
			Interface.CallHook("OnClanTopUpdated");
		}

		private ClanData FindClanByPlayerFromCache(string userId)
		{
			return !string.IsNullOrEmpty(userId) && _playerToClan.TryGetValue(userId, out var clan) ? clan : null;
		}

		private ClanData FindClanByPlayer(string userId)
		{
			if (string.IsNullOrEmpty(userId)) return null;

			var clan = FindClanByPlayerFromCache(userId) ?? FindClanByUserID(userId);
			if (clan == null) return null;

			_playerToClan[userId] = clan;
			return clan;
		}

		private ClanData FindClanByUserID(string userId)
		{
			return FindClanByUserID(Convert.ToUInt64(userId));
		}

		private ClanData FindClanByUserID(ulong userId)
		{
			return _clansList.Find(clan => clan.IsMember(userId));
		}

		private ClanData FindClanByTag(string tag)
		{
			return _clanByTag.GetValueOrDefault(AdjustClanTagCase(tag), null);
		}

		private bool PlayerHasClan(ulong userId)
		{
			return FindClanByPlayer(userId.ToString()) != null;
		}

		private bool IsClanMember(string playerId, string otherId)
		{
			return IsClanMember(Convert.ToUInt64(playerId), Convert.ToUInt64(otherId));
		}

		private bool IsClanMember(ulong playerId, ulong otherId)
		{
			var clan = FindClanByPlayer(playerId.ToString());
			return clan != null && clan.IsMember(otherId);
		}

		private JObject GetClan(string tag)
		{
			if (string.IsNullOrEmpty(tag)) return null;
			
			return FindClanByTag(tag)?.ToJObject();
		}

		private string GetClanOf(BasePlayer target)
		{
			return GetClanOf(target.userID);
		}

		private string GetClanOf(string target)
		{
			return GetClanOf(Convert.ToUInt64(target));
		}

		private string GetClanOf(ulong target)
		{
			return FindClanByPlayer(target.ToString())?.ClanTag;
		}

		private JArray GetAllClans()
		{
			return new JArray(_clansList.Select(x => x.ClanTag));
		}

		private List<string> GetClanMembers(string target)
		{
			return GetClanMembers(Convert.ToUInt64(target));
		}

		private List<string> GetClanMembers(ulong target)
		{
			return FindClanByPlayer(target.ToString())?.Members.Select(x => x.ToString());
		}

		private List<string> GetClanAlliances(string playerId)
		{
			return GetClanAlliances(Convert.ToUInt64(playerId));
		}

		private List<string> GetClanAlliances(ulong playerId)
		{
			var clan = FindClanByPlayer(playerId.ToString());
			return clan == null ? new List<string>() : new List<string>(clan.Alliances);
		}

		private bool IsAllyPlayer(string playerId, string otherId)
		{
			return IsAllyPlayer(Convert.ToUInt64(playerId), Convert.ToUInt64(otherId));
		}

		private bool IsAllyPlayer(ulong playerId, ulong otherId)
		{
			var playerClan = FindClanByPlayer(playerId.ToString());
			if (playerClan == null)
				return false;

			var otherClan = FindClanByPlayer(otherId.ToString());
			if (otherClan == null)
				return false;

			return playerClan.Alliances.Contains(otherClan.ClanTag);
		}

		private bool IsMemberOrAlly(string playerId, string otherId)
		{
			return IsMemberOrAlly(Convert.ToUInt64(playerId), Convert.ToUInt64(otherId));
		}

		private bool IsMemberOrAlly(ulong playerId, ulong otherId)
		{
			var playerClan = FindClanByPlayer(playerId.ToString());
			if (playerClan == null)
				return false;

			var otherClan = FindClanByPlayer(otherId.ToString());
			if (otherClan == null)
				return false;

			return playerClan.ClanTag.Equals(otherClan.ClanTag) || playerClan.Alliances.Contains(otherClan.ClanTag);
		}

		private Dictionary<int, string> GetTopClans()
		{
			return _clansList.ToDictionary(y => y.Top, x => x.ClanTag);
		}

		private float GetPlayerScores(ulong userId)
		{
			return GetPlayerScores(userId.ToString());
		}

		private float GetPlayerScores(string userId)
		{
			return PlayerData.GetNotLoad(userId)?.Score ?? 0f;
		}

		private float GetClanScores(string clanTag)
		{
			return FindClanByTag(clanTag)?.TotalScores ?? 0f;
		}

		private string GetTagColor(string clanTag)
		{
			return FindClanByTag(clanTag)?.GetHexTagColor() ?? _config.Tags.TagColor.DefaultColor;
		}

		private string TryJoinPlayerToClan(string clanTag, string playerName)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			var player = BasePlayer.FindAwakeOrSleeping(playerName);
			if (player == null)
			{
				return $"Player '{playerName}' not found!";
			}

			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return $"Player '{playerName}' not found!";

			if (data.GetClan() != null)
			{
				return "The player is already in a clan";
			}

			var inviteData = _invites.GetClanInvite(player.userID, clan.ClanTag);
			if (inviteData == null)
			{
				return "The player does not have a invite to that clan";
			}

			if (clan.Members.Count >= _config.LimitSettings.MemberLimit)
			{
				return "The clan is already at capacity";
			}

			clan.Join(player);

			Reply(player, AdminJoin, clanTag);

			return "success";
		}

		private string TryKickPlayerFromClan(string clanTag, string playerName)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			var player = BasePlayer.FindAwakeOrSleeping(playerName);
			if (player == null)
			{
				return $"Player '{playerName}' not found!";
			}

			if (!clan.IsMember(player.userID))
			{
				return "The player is not in that clan";
			}

			clan.Kick(player.userID);

			Reply(player, AdminKick, clan.ClanTag);

			clan.Broadcast(AdminKickBroadcast, player.displayName);

			return "success";
		}

		private string TryKickPlayerFromAnyClan(ulong playerId)
		{
			var clan = FindClanByPlayer(playerId.ToString());
			if (clan == null)
			{
				return $"Clan '{playerId}' not found!";
			}

			if (!clan.IsMember(playerId))
			{
				return "The player is not in that clan";
			}

			clan.Kick(playerId);

			var player = covalence.Players.FindPlayerById(playerId.ToString());
			if (player != null)
				Reply(player, AdminKick, clan.ClanTag);

			clan.Broadcast(AdminKickBroadcast, player != null ? player.Name : playerId.ToString());
			return "success";
		}

		private string TrySetClanOwner(string clanTag, string playerID)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			var player = BasePlayer.FindAwakeOrSleeping(playerID);
			if (player == null)
			{
				return $"Player '{playerID}' not found!";
			}

			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return $"Player '{playerID}' not found!";

			if (!clan.IsMember(player.userID))
			{
				return "The player is not a member of that clan";
			}

			if (clan.IsOwner(player.userID))
			{
				return "The player is already the clan owner";
			}

			clan.SetLeader(player.userID);

			clan.Broadcast(AdminSetLeader, player.userID);

			return "success";
		}

		private string TrySendInviteToClan(string clanTag, string playerID)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			var player = BasePlayer.FindAwakeOrSleeping(playerID);
			if (player == null)
			{
				return $"Player '{playerID}' not found!";
			}

			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return $"Player '{playerID}' not found!";

			if (data.GetClan() != null)
			{
				return "The player is already a member of the clan.";
			}

			if (clan.IsMember(player.userID))
			{
				return "The player is already a member of the clan.";
			}

			var inviteData = _invites.GetClanInvite(player.userID, clan.ClanTag);
			if (inviteData != null)
			{
				return "The player already has a invitation to join that clan";
			}

			if (clan.Members.Count >= _config.LimitSettings.MemberLimit)
			{
				return "The clan is already at capacity";
			}

			if (_config.PaidFunctionality.ChargeFeeForSendInviteToClan &&
			    !_config.PaidFunctionality.Economy.RemoveBalance(player,
				    _config.PaidFunctionality.CostForSendInviteToClan))
			{
				Reply(player, PaidSendInviteMsg, _config.PaidFunctionality.CostForSendInviteToClan);
				return "The player has insufficient funds";
			}

			_invites.AddPlayerInvite(player.userID, 0, "ADMIN", clan.ClanTag);

			Reply(player, SuccessInvitedSelf, "ADMIN", clan.ClanTag);

			clan.Broadcast(AdminInvite, player.displayName);
			return "success";
		}

		private string TryPromoteClanMember(string clanTag, string playerID)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			var player = BasePlayer.FindAwakeOrSleeping(playerID);
			if (player == null)
			{
				return $"Player '{playerID}' not found!";
			}

			if (clan.IsOwner(player.userID))
			{
				return ("You can not demote the clan owner");
			}

			if (clan.IsModerator(player.userID))
			{
				return ("The player is already a moderator");
			}

			clan.SetModer(player.userID);

			clan.Broadcast(AdminPromote, player.displayName);
			return "success";
		}

		private string TryDemoteClanMember(string clanTag, string playerID)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			var player = BasePlayer.FindAwakeOrSleeping(playerID);
			if (player == null)
			{
				return $"Player '{playerID}' not found!";
			}

			if (clan.IsOwner(player.userID))
			{
				return "You can not demote the clan owner";
			}

			if (clan.IsMember(player.userID))
			{
				return "The player is already at the lowest rank";
			}

			clan.UndoModer(player.userID);

			clan.Broadcast(AdminDemote, player.displayName);
			return "success";
		}

		private string TryDisbandClan(string clanTag)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			clan.Broadcast(AdminDisbandClan);

			clan.Disband();
			return "success";
		}

		private string TryRenameClan(string oldClanTag, string newClanTag)
		{
			var clan = FindClanByTag(oldClanTag);
			if (clan == null)
			{
				return $"Clan '{oldClanTag}' not found!";
			}

			if (string.IsNullOrEmpty(newClanTag) || newClanTag.Length < _config.Tags.TagMin ||
			    newClanTag.Length > _config.Tags.TagMax)
			{
				return "Clan tag is too short or too long";
			}

			if (_config.Tags.CheckingCharacters && _tagFilter.IsMatch(newClanTag))
			{
				return "contains forbidden characters";
			}

			if (FindClanByTag(newClanTag) != null)
			{
				return "Clan with that tag already exists!";
			}

			clan.Rename(newClanTag);
			clan.Broadcast(AdminRename, newClanTag);

			return "success";
		}

		private string TryCreateClan(string playerID, string clanTag)
		{
			var player = BasePlayer.FindAwakeOrSleeping(playerID);
			if (player == null)
			{
				return $"Player '{playerID}' not found!";
			}

			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return $"Player '{playerID}' not found!";

			if (string.IsNullOrEmpty(clanTag) || clanTag.Length < _config.Tags.TagMin ||
			    clanTag.Length > _config.Tags.TagMax)
			{
				return "Clan tag is too short or too long";
			}

			var checkTag = clanTag.Replace(" ", "");
			if (_config.Tags.BlockedWords.Exists(word =>
				    checkTag.Contains(word, CompareOptions.OrdinalIgnoreCase)))
			{
				return "Clan tag contains forbidden words";
			}

			if (_config.Tags.CheckingCharacters && _tagFilter.IsMatch(checkTag))
			{
				return "Contains forbidden characters";
			}

			var clan = FindClanByTag(clanTag);
			if (clan != null)
			{
				return "Clan already exists";
			}

			if (FindClanByPlayer(player.UserIDString) != null)
			{
				return "Player is already in a clan";
			}

			clan = ClanData.CreateNewClan(clanTag, player);
			if (clan == null) return $"Clan '{clanTag}' not found!";

			ClanCreating.Remove(player.userID);
			Reply(player, ClanCreated, clanTag);

			return "success";
		}

		private string TryBroadcastClan(string clanTag, string message)
		{
			var clan = FindClanByTag(clanTag);
			if (clan == null)
			{
				return $"Clan '{clanTag}' not found!";
			}

			if (string.IsNullOrEmpty(message)) return "Message is empty";

			clan.Broadcast(AdminBroadcast, message);
			return "success";
		}

		#endregion

		#region Invites

		#region Players

		private void SendInvite(BasePlayer inviter, ulong target)
		{
#if TESTING
			using (new StopwatchWrapper("send invite"))
#endif
			{
				if (inviter == null) return;

				var clan = FindClanByPlayer(inviter.UserIDString);
				if (clan == null) return;

				if (!clan.IsModerator(inviter.userID))
				{
					Reply(inviter, NotModer);
					return;
				}

				if (clan.Members.Count >= _config.LimitSettings.MemberLimit)
				{
					Reply(inviter, ALotOfMembers);
					return;
				}

				var targetClan = FindClanByPlayer(target.ToString());
				if (targetClan != null)
				{
					Reply(inviter, HeAlreadyClanMember);
					return;
				}

				var data = PlayerData.GetOrCreate(target.ToString());
				if (data == null) return;

				if (!_invites.CanSendInvite(target, clan.ClanTag))
				{
					Reply(inviter, AlreadyInvitedInClan);
					return;
				}

				if (_config.PaidFunctionality.ChargeFeeForSendInviteToClan &&
				    !_config.PaidFunctionality.Economy.RemoveBalance(inviter,
					    _config.PaidFunctionality.CostForSendInviteToClan))
				{
					Reply(inviter, PaidSendInviteMsg, _config.PaidFunctionality.CostForSendInviteToClan);
					return;
				}

				var inviterName = inviter.Connection.username;

				_invites.AddPlayerInvite(target, inviter.userID, inviterName, clan.ClanTag);

				Reply(inviter, SuccessInvited, data.DisplayName, clan.ClanTag);

				var targetPlayer = RelationshipManager.FindByID(target);
				if (targetPlayer != null)
					Reply(targetPlayer, SuccessInvitedSelf, inviterName, clan.ClanTag);
			}
		}

		private void AcceptInvite(BasePlayer player, string tag)
		{
#if TESTING
			using (new StopwatchWrapper("accept invite"))
#endif
			{
				if (player == null || string.IsNullOrEmpty(tag)) return;

				if (_config.PermissionSettings.UsePermClanJoining &&
				    !string.IsNullOrEmpty(_config.PermissionSettings.ClanJoining) &&
				    !player.HasPermission(_config.PermissionSettings.ClanJoining))
				{
					Reply(player, NoPermJoinClan);
					return;
				}

				if (_config.PaidFunctionality.ChargeFeeToJoinClan && !_config.PaidFunctionality.Economy.RemoveBalance(
					    player,
					    _config.PaidFunctionality.CostJoiningClan))
				{
					Reply(player, PaidJoinMsg, _config.PaidFunctionality.CostJoiningClan);
					return;
				}

				var data = PlayerData.GetOrCreate(player.UserIDString);
				if (data == null) return;

				var clan = data.GetClan();
				if (clan != null)
				{
					Reply(player, AlreadyClanMember);
					return;
				}

				clan = FindClanByTag(tag);
				if (clan == null)
				{
					_invites.RemovePlayerClanInvites(tag);
					return;
				}

				if (clan.Members.Count >= _config.LimitSettings.MemberLimit)
				{
					Reply(player, ALotOfMembers);
					return;
				}

				var inviteData = _invites.GetClanInvite(player.userID, tag);
				if (inviteData == null)
					return;

				clan.Join(player);
				Reply(player, ClanJoined, clan.ClanTag);

				var inviter = RelationshipManager.FindByID(inviteData.InviterId);
				if (inviter != null)
					Reply(inviter, WasInvited, player.Connection?.username ?? GetPlayerName(player.userID));
			}
		}

		private void CancelInvite(BasePlayer player, string tag)
		{
#if TESTING
			using (new StopwatchWrapper("cancel invite"))
#endif
			{
				if (player == null || string.IsNullOrEmpty(tag)) return;

				var data = PlayerData.GetOrCreate(player.UserIDString);
				if (data == null) return;

				var inviteData = _invites.GetClanInvite(player.userID, tag);
				if (inviteData == null) return;

				_invites.RemovePlayerClanInvites(inviteData);

				Reply(player, DeclinedInvite, tag);

				var inviter = RelationshipManager.FindByID(inviteData.InviterId);
				if (inviter != null)
					Reply(inviter, DeclinedInviteSelf, player.displayName);
			}
		}

		private void WithdrawInvite(BasePlayer inviter, ulong target)
		{
#if TESTING
			using (new StopwatchWrapper("withdraw invite"))
#endif
			{
				var inviterData = PlayerData.GetOrLoad(inviter.UserIDString);

				var clan = inviterData?.GetClan();
				if (clan == null) return;

				if (!clan.IsModerator(inviter.userID))
				{
					Reply(inviter, NotModer);
					return;
				}

				var data = PlayerData.GetOrCreate(target.ToString());
				if (data == null) return;

				var inviteData = _invites.GetClanInvite(target, clan.ClanTag);
				if (inviteData == null)
				{
					Reply(inviter, DidntReceiveInvite, data.DisplayName);
					return;
				}

				var clanInviter = inviteData.InviterId;
				if (clanInviter != inviter.userID)
				{
					var clanInviterPlayer = RelationshipManager.FindByID(clanInviter);
					if (clanInviterPlayer != null)
						Reply(clanInviterPlayer, YourInviteDeclined, data.DisplayName,
							inviterData.DisplayName);
				}

				_invites.RemovePlayerClanInvites(inviteData);

				var targetPlayer = RelationshipManager.FindByID(target);
				if (targetPlayer != null)
					Reply(targetPlayer, CancelledInvite, clan.ClanTag);

				Reply(inviter, CancelledYourInvite, data.DisplayName);
			}
		}

		private bool HasInvite(BasePlayer player)
		{
#if TESTING
			using (new StopwatchWrapper("has invite"))
#endif
			{
				if (player == null) return false;

				return _invites?.GetPlayerClanInvites(player.userID).Count > 0;
			}
		}

		#endregion

		#region Alliances

		private void AllySendInvite(BasePlayer player, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("ally send invite"))
#endif
			{
				if (player == null || string.IsNullOrEmpty(clanTag)) return;

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null) return;

				if (!clan.IsModerator(player.userID))
				{
					Reply(player, NotModer);
					return;
				}

				var targetClan = FindClanByTag(clanTag);
				if (targetClan == null) return;

				if (clan.Alliances.Count >= _config.LimitSettings.AlliancesLimit ||
				    targetClan.Alliances.Count >= _config.LimitSettings.AlliancesLimit)
				{
					Reply(player, ALotOfAlliances);
					return;
				}

				var invites = _invites.GetAllyTargetInvites(clan.ClanTag);
				if (invites.Exists(invite => invite.TargetClanTag == clanTag))
				{
					Reply(player, AllInviteExist);
					return;
				}

				if (targetClan.Alliances.Contains(clan.ClanTag))
				{
					Reply(player, AlreadyAlliance);
					return;
				}

				invites = _invites.GetAllyIncomingInvites(clanTag);
				if (invites.Exists(x => x.SenderClanTag == clanTag))
				{
					AllyAcceptInvite(player, clanTag);
					return;
				}

				_invites.AddAllyInvite(player.userID, player.displayName, clan.ClanTag, targetClan.ClanTag);

				clan.Members.FindAll(member => member != player.userID).ForEach(member =>
					Reply(RelationshipManager.FindByID(member), AllySendedInvite, player.displayName,
						targetClan.ClanTag));

				Reply(player, YouAllySendedInvite, targetClan.ClanTag);

				targetClan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), SelfAllySendedInvite, clan.ClanTag));
			}
		}

		private void AllyAcceptInvite(BasePlayer player, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("ally accept invite"))
#endif
			{
				if (player == null || string.IsNullOrEmpty(clanTag)) return;

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null) return;

				var targetClan = FindClanByTag(clanTag);
				if (targetClan == null) return;

				if (clan.Alliances.Count >= _config.LimitSettings.AlliancesLimit ||
				    targetClan.Alliances.Count >= _config.LimitSettings.AlliancesLimit)
				{
					Reply(player, ALotOfAlliances);
					return;
				}

				var invites = _invites.GetAllyIncomingInvites(clan.ClanTag);
				if (!invites.Exists(invite => invite.SenderClanTag == targetClan.ClanTag))
				{
					Reply(player, NoFoundInviteAlly, targetClan.ClanTag);
					return;
				}

				_invites.RemoveAllyInviteByClan(clan.ClanTag, targetClan.ClanTag);

				clan.Alliances.Add(targetClan.ClanTag);
				targetClan.Alliances.Add(clan.ClanTag);

				if (_config.AutoTeamCreation &&
				    _config.AllianceSettings.AllyAddPlayersTeams)
				{
					var team = targetClan.FindTeam() ?? clan.FindTeam() ?? targetClan.FindTeam() ?? clan.CreateTeam();
					if (team != null)
					{
#if TESTING
						Debug.Log(
							$"[AllyAcceptInvite] clan={clan.ClanTag} & team = {clan.TeamID}, target clan={targetClan.ClanTag} & team = {targetClan.TeamID}");
#endif

						var clanForNewTeam = team.teamLeader == targetClan.LeaderID ? clan : targetClan;
						clanForNewTeam.SetTeam(team.teamID);

						var allPlayers = new List<ulong>(targetClan.Members);
						allPlayers.AddRange(clan.Members);

						allPlayers.ForEach(member =>
						{
							if (team.members.Contains(member)) return;

							var clanMember = RelationshipManager.FindByID(member);
							if (clanMember == null) return;

							if (clanMember.Team != null && clanMember.Team.teamID != team.teamID)
								clanMember.Team.RemovePlayer(member);

							team.AddPlayer(clanMember);
						});
					}
				}

				clan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), AllyAcceptInviteTitle, targetClan.ClanTag));
				targetClan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), AllyAcceptInviteTitle, clan.ClanTag));
			}
		}

		private void AllyCancelInvite(BasePlayer player, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("ally cancel invite"))
#endif
			{
				if (player == null || string.IsNullOrEmpty(clanTag)) return;

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null) return;

				var targetClan = FindClanByTag(clanTag);
				if (targetClan == null) return;

				_invites.RemoveAllyInviteByClan(clan.ClanTag, targetClan.ClanTag);

				clan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), RejectedInviteTitle, targetClan.ClanTag));
				targetClan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), SelfRejectedInviteTitle, clan.ClanTag));
			}
		}

		private void AllyWithdrawInvite(BasePlayer player, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("ally withdraw invite"))
#endif
			{
				if (player == null || string.IsNullOrEmpty(clanTag)) return;

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null) return;

				var targetClan = FindClanByTag(clanTag);
				if (targetClan == null) return;

				_invites.RemoveAllyInviteByClan(targetClan.ClanTag, clan.ClanTag);

				clan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), WithdrawInviteTitle, targetClan.ClanTag));
				targetClan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), SelfWithdrawInviteTitle, clan.ClanTag));
			}
		}

		private void AllyRevoke(BasePlayer player, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("ally revoke"))
#endif
			{
				if (player == null || string.IsNullOrEmpty(clanTag)) return;

				var clan = FindClanByPlayer(player.UserIDString);
				if (clan == null) return;

				var targetClan = FindClanByTag(clanTag);
				if (targetClan == null) return;

				if (!clan.Alliances.Contains(clanTag))
				{
					Reply(player, NoAlly, clanTag);
					return;
				}

				clan.Alliances.Remove(targetClan.ClanTag);
				targetClan.Alliances.Remove(clan.ClanTag);

				if (_config.AutoTeamCreation &&
				    _config.AllianceSettings.AllyAddPlayersTeams)
				{
					var team = targetClan.FindTeam() ?? clan.FindTeam() ?? targetClan.FindTeam() ?? clan.CreateTeam();
					if (team != null)
					{
						var firstClan = team.teamLeader == targetClan.LeaderID;

						var clanForNewTeam = firstClan ? clan : targetClan;

						clanForNewTeam.Members.ForEach(member =>
						{
							team.RemovePlayer(member);

							RelationshipManager.FindByID(member)?.ClearTeam();
						});

						NextTick(() =>
						{
							var newTeam = clanForNewTeam.CreateTeam();

							clanForNewTeam.Members.ForEach(member =>
							{
								var targetMember = RelationshipManager.FindByID(member);
								if (targetMember == null) return;

								newTeam.AddPlayer(targetMember);
							});
						});
					}
				}

				clan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), SelfBreakAlly, targetClan.ClanTag));

				targetClan.Members.ForEach(member =>
					Reply(RelationshipManager.FindByID(member), BreakAlly, clan.ClanTag));
			}
		}

		private bool HasAllyInvite(ClanData clan, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("has ally invite"))
#endif
			{
				return _invites.CanSendAllyInvite(clan.ClanTag, clanTag);
			}
		}

		private bool HasAllyIncomingInvite(ClanData clan, string clanTag)
		{
#if TESTING
			using (new StopwatchWrapper("has ally incoming invite"))
#endif
			{
				return
					_invites.CanSendAllyInvite(clanTag,
						clan.ClanTag);
			}
		}

		#endregion

		#endregion

		#region Clan Creating

		private readonly Dictionary<ulong, CreateClanData> ClanCreating =
			new Dictionary<ulong, CreateClanData>();

		private class CreateClanData
		{
			public string Tag;

			public string Avatar;
		}

		#endregion

		#region Rating

		private Dictionary<ulong, TopPlayerData> TopPlayers = new Dictionary<ulong, TopPlayerData>();

		private List<ulong> _topPlayerList = new List<ulong>();

		private class TopPlayerData
		{
			public ulong UserId;

			public int Top;

			private PlayerData _data;

			public PlayerData Data => _data ??= PlayerData.GetNotLoad(UserId.ToString());

			public string DisplayName;

			private float _score;

			public float Score => _score;

			private float _resources;

			public float Resources => _resources;

			private float _totalFarm;

			public float TotalFarm => _totalFarm;

			public string GetParams(string value)
			{
				switch (value)
				{
					case "name":
						return DisplayName;
					case "score":
						return GetValue(Score);
					case "resources":
						return GetValue(Resources);
					default:
						return GetValue(Data.GetAmount(value));
				}
			}

			public TopPlayerData(PlayerData data)
			{
				_data = data;

				ulong.TryParse(data.SteamID, out UserId);

				UpdateData();
			}

			public void SetData(ref PlayerData data)
			{
				_data = data;

				UpdateData();
			}

			public void SetDataToNull()
			{
				UpdateData();

				_data = null;
			}

			public void UpdateData()
			{
				if (Data != null)
				{
					DisplayName = Data.DisplayName;
					_resources = Data.Resources;
					_score = Data.Score;

					var clan = Data.GetClan();
					if (clan != null)
					{
						_totalFarm = Data.GetTotalFarm(clan);
					}
				}
			}
		}

		private TopPlayerData GetTopDataById(ulong target)
		{
			return TopPlayers.GetValueOrDefault(target);
		}

		private void InitTopHandle()
		{
			if (_initTopHandle != null) return;

			_initTopHandle = ServerMgr.Instance.StartCoroutine(PlayerData.InitTopCoroutine());
		}

		private void HandleTop()
		{
			if (_topHandle != null) return;

			_topHandle = ServerMgr.Instance.StartCoroutine(PlayerData.HandleTopCoroutine());
		}

		private void SortPlayers(ref List<TopPlayerData> topPlayers)
		{
#if TESTING
			using (new StopwatchWrapper("sort players"))
			{
#endif
			topPlayers.Sort((x, y) => y.Score.CompareTo(x.Score));

			for (var i = 0; i < topPlayers.Count; i++)
			{
				var member = topPlayers[i];

				member.Top = i + 1;

				TopPlayers[member.UserId] = member;
			}
#if TESTING
			}
#endif
		}

		private void SortClans()
		{
#if TESTING
			using (new StopwatchWrapper("sort clans"))
#endif
			{
				_clansList.Sort((x, y) => y.TotalScores.CompareTo(x.TotalScores));

				for (var i = 0; i < _clansList.Count; i++)
				{
					_clansList[i].Top = i + 1;

					_clansList[i].UpdateClanStats();
				}
			}
		}

		#endregion

		#region Item Skins

		private List<string> _skinnedItems = new List<string>();

		private Dictionary<string, int> _itemIds = new Dictionary<string, int>();

		private void LoadSkins()
		{
#if TESTING
			using (new StopwatchWrapper("load skins"))
#endif
			{
				var loadClansSkins = LoadClansSkins();
				var loadSkinsFromPlayerSkins = LoadSkinsFromPlayerSkins();
				var loadSkinsFromLSkins = LoadSkinsFromLSkins();

#if TESTING
				Puts(
					$"[LoadSkins] loadClansSkins={loadClansSkins}, loadSkinsFromPlayerSkins={loadSkinsFromPlayerSkins}, loadSkinsFromLSkins={loadSkinsFromLSkins}");
#endif
				if (loadClansSkins || loadSkinsFromPlayerSkins || loadSkinsFromLSkins)
				{
#if TESTING
					Puts("[LoadSkins] SaveConfig");
#endif
					SaveConfig();
				}

				_skinnedItems = _config.Skins.ItemSkins.Keys.ToList();
			}
		}

		private bool LoadClansSkins()
		{
#if TESTING
			using (new StopwatchWrapper("load clans skins"))
#endif
			{
				if (!(_enabledSkins = _config.Pages.Exists(page => page.ID == SKINS_PAGE && page.Enabled)))
					return false;

#if TESTING
				Puts($"[LoadClansSkins]. start load skins, count={_config.Skins.ItemSkins.Count}");
#endif
				var any = false;

				var skins = _config.Skins.ItemSkins.ToList();
				for (var i = 0; i < skins.Count; i++)
				{
					var itemSkin = skins[i];

					if (itemSkin.Value.Count == 0)
					{
						_config.Skins.ItemSkins[itemSkin.Key] =
							ImageLibrary?.Call<List<ulong>>("GetImageList", itemSkin.Key) ??
							new List<ulong>();

						any = true;
					}
				}

				return any;
			}
		}

		private int FindItemID(string shortName)
		{
#if TESTING
			using (new StopwatchWrapper("find item id"))
#endif
			{
				if (_itemIds.TryGetValue(shortName, out var val))
					return val;

				var definition = ItemManager.FindItemDefinition(shortName);
				if (definition == null) return 0;

				val = definition.itemid;
				_itemIds[shortName] = val;
				return val;
			}
		}

		private ulong GetItemSkin(string shortName, ClanData clan)
		{
#if TESTING
			using (new StopwatchWrapper("get item skin"))
#endif
			{
				return clan.Skins.TryGetValue(shortName, out var skin) ? skin : 0;
			}
		}

		#endregion

		#region Lang

		private const string
			ConfirmResourceTitle = "ConfirmResourceTitle",
			ConfirmResourceMessage = "ConfirmResourceMessage",
			ConfirmLeaveTitle = "ConfirmLeaveTitle",
			ConfirmLeaveMessage = "ConfirmLeaveMessage",
			ConfirmAvatarTitle = "ConfirmAvatarTitle",
			ConfirmAvatarMessage = "ConfirmAvatarMessage",
			LeaveTitle = "LeaveTitle",
			PaidSendInviteMsg = "PaidSendInviteMsg",
			PaidSetAvatarMsg = "PaidSetAvatarMsg",
			PaidSetSkinMsg = "PaidSetSkinMsg",
			PaidDisbandMsg = "PaidDisbandMsg",
			PaidLeaveMsg = "PaidLeaveMsg",
			PaidKickMsg = "PaidKickMsg",
			PaidJoinMsg = "PaidJoinMsg",
			PaidCreateTitle = "PaidCreateTitle",
			NotMoney = "NotMoney",
			NotAllowedEditClanImage = "NotAllowedEditClanImage",
			NoILError = "NoILError",
			ClanChatPrefix = "ClanChatPrefix",
			ClanChatFormat = "ClanChatFormat",
			ClanChatSyntax = "ClanChatSyntax",
			AllyChatPrefix = "AllyChatPrefix",
			AllyChatFormat = "AllyChatFormat",
			AllyChatSyntax = "AllyChatSyntax",
			PlayTimeTitle = "PlayTimeTitle",
			TagColorTitle = "TagColorTitle",
			TagColorInstalled = "TagColorInstalled",
			TagColorFormat = "TagColorFormat",
			NoPermissions = "NoPermissions",
			ClanInfoAlliancesNone = "ClanInfoAlliancesNone",
			ClanInfoAlliances = "ClanInfoAlliances",
			ClanInfoLastOnline = "ClanInfoLastOnline",
			ClanInfoEstablished = "ClanInfoEstablished",
			ClanInfoOffline = "ClanInfoOffline",
			ClanInfoOnline = "ClanInfoOnline",
			ClanInfoDescription = "ClanInfoDescription",
			ClanInfoTag = "ClanInfoTag",
			ClanInfoTitle = "ClanInfoTitle",
			AdminRename = "AdminRename",
			AdminSetLeader = "AdminSetLeader",
			AdminKickBroadcast = "AdminKickBroadcast",
			AdminBroadcast = "AdminBroadcast",
			AdminJoin = "AdminJoin",
			AdminKick = "AdminKick",
			AdminInvite = "AdminInvite",
			AdminPromote = "AdminPromote",
			AdminDemote = "AdminDemote",
			AdminDisbandClan = "AdminDisbandClan",
			UseClanSkins = "UseClanSkins",
			ClansMenuTitle = "ClansMenuTitle",
			AboutClan = "AboutClan",
			ChangeAvatar = "ChangeAvatar",
			EnterLink = "EnterLink",
			LeaderTitle = "LeaderTitle",
			GatherTitle = "GatherTitle",
			RatingTitle = "RatingTitle",
			MembersTitle = "MembersTitle",
			DescriptionTitle = "DescriptionTitle",
			NameTitle = "NameTitle",
			SteamIdTitle = "SteamIdTitle",
			ProfileTitle = "ProfileTitle",
			InvitedToClan = "InvitedToClan",
			BackPage = "BackPage",
			NextPage = "NextPage",
			TopClansTitle = "TopClansTitle",
			TopPlayersTitle = "TopPlayersTitle",
			TopTitle = "TopTitle",
			ScoreTitle = "ScoreTitle",
			KillsTitle = "KillsTitle",
			DeathsTitle = "DeathsTitle",
			KDTitle = "KDTitle",
			ResourcesTitle = "ResourcesTitle",
			LeftTitle = "LeftTitle",
			EditTitle = "EditTitle",
			InviteTitle = "InviteTitle",
			SearchTitle = "SearchTitle",
			ClanInvitation = "ClanInvitation",
			InviterTitle = "InviterTitle",
			AcceptTitle = "AcceptTitle",
			CancelTitle = "CancelTitle",
			PlayerTitle = "PlayerTitle",
			ClanTitle = "ClanTitle",
			NotMemberOfClan = "NotMemberOfClan",
			SelectItemTitle = "SelectItemTitle",
			CloseTitle = "CloseTitle",
			SelectTitle = "SelectTitle",
			ClanCreationTitle = "ClanCreationTitle",
			ClanNameTitle = "ClanNameTitle",
			AvatarTitle = "AvatarTitle",
			UrlTitle = "UrlTitle",
			CreateTitle = "CreateTitle",
			LastLoginTitle = "LastLoginTitle",
			DemoteModerTitle = "DemoteModerTitle",
			PromoteModerTitle = "PromoteModerTitle",
			PromoteLeaderTitle = "PromoteLeaderTitle",
			KickTitle = "KickTitle",
			GatherRatesTitle = "GatherRatesTitle",
			CreateClanTitle = "CreateClanTitle",
			FriendlyFireTitle = "FriendlyFireTitle",
			AllyFriendlyFireTitle = "AllyFriendlyFireTitle",
			InvitesTitle = "InvitesTitle",
			AllyInvites = "AllyInvites",
			ClanInvitesTitle = "ClanInvitesTitle",
			IncomingAllyTitle = "IncomingAllyTitle",
			LeaderTransferTitle = "LeaderTransferTitle",
			SelectSkinTitle = "SelectSkinTitle",
			EnterSkinTitle = "EnterSkinTitle",
			NotModer = "NotModer",
			SuccsessKick = "SuccsessKick",
			WasKicked = "WasKicked",
			NotClanMember = "NotClanMember",
			NotClanLeader = "NotClanLeader",
			AlreadyClanMember = "AlreadyClanMember",
			ClanTagLimit = "ClanTagLimit",
			ClanExists = "ClanExists",
			ClanCreated = "ClanCreated",
			ClanDisbandedTitle = "ClanDisbandedTitle",
			ClanLeft = "ClanLeft",
			PlayerNotFound = "PlayerNotFound",
			ClanNotFound = "ClanNotFound",
			ClanAlreadyModer = "ClanAlreadyModer",
			PromotedToModer = "PromotedToModer",
			NotClanModer = "NotClanModer",
			DemotedModer = "DemotedModer",
			FFOn = "FFOn",
			AllyFFOn = "AllyFFOn",
			FFOff = "FFOff",
			AllyFFOff = "AllyFFOff",
			Help = "Help",
			ModerHelp = "ModerHelp",
			AdminHelp = "AdminHelp",
			HeAlreadyClanMember = "HeAlreadyClanMember",
			AlreadyInvitedInClan = "AlreadyInvitedInClan",
			SuccessInvited = "SuccessInvited",
			SuccessInvitedSelf = "SuccessInvitedSelf",
			ClanJoined = "ClanJoined",
			WasInvited = "WasInvited",
			DeclinedInvite = "DeclinedInvite",
			DeclinedInviteSelf = "DeclinedInviteSelf",
			DidntReceiveInvite = "DidntReceiveInvite",
			YourInviteDeclined = "YourInviteDeclined",
			CancelledInvite = "CancelledInvite",
			CancelledYourInvite = "CancelledYourInvite",
			CannotDamage = "CannotDamage",
			AllyCannotDamage = "AllyCannotDamage",
			SetDescription = "SetDescription",
			MaxDescriptionSize = "MaxDescriptionSize",
			NotDescription = "NotDescription",
			ContainsForbiddenWords = "ContainsForbiddenWords",
			NoPermCreateClan = "NoPermCreateClan",
			NoPermJoinClan = "NoPermJoinClan",
			NoPermKickClan = "NoPermKickClan",
			NoPermLeaveClan = "NoPermLeaveClan",
			NoPermDisbandClan = "NoPermDisbandClan",
			NoPermClanSkins = "NoPermClanSkins",
			NoAllies = "NoAllies",
			NoInvites = "NoInvites",
			AllInviteExist = "AllInviteExist",
			AlreadyAlliance = "AlreadyAlliance",
			AllySendedInvite = "AllySendedInvite",
			YouAllySendedInvite = "YouAllySendedInvite",
			SelfAllySendedInvite = "SelfAllySendedInvite",
			NoFoundInviteAlly = "NoFoundInviteAlly",
			AllyAcceptInviteTitle = "AllyAcceptInviteTitle",
			RejectedInviteTitle = "RejectedInviteTitle",
			SelfRejectedInviteTitle = "SelfRejectedInviteTitle",
			WithdrawInviteTitle = "WithdrawInviteTitle",
			SelfWithdrawInviteTitle = "SelfWithdrawInviteTitle",
			SendAllyInvite = "SendAllyInvite",
			CancelAllyInvite = "CancelAllyInvite",
			WithdrawAllyInvite = "WithdrawAllyInvite",
			ALotOfMembers = "ALotOfMembers",
			ALotOfModers = "ALotOfModers",
			ALotOfAlliances = "ALotOfAlliances",
			NextBtn = "NextBtn",
			BackBtn = "BackBtn",
			NoAlly = "NoAlly",
			BreakAlly = "BreakAlly",
			SelfBreakAlly = "SelfBreakAlly",
			AllyRevokeTitle = "AllyRevokeTitle";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[ClansMenuTitle] = "Clans menu",
				[AboutClan] = "About Clan",
				[ChangeAvatar] = "Change avatar",
				[EnterLink] = "Enter link",
				[LeaderTitle] = "Leader",
				[GatherTitle] = "Gather",
				[RatingTitle] = "Rating",
				[MembersTitle] = "Members",
				[DescriptionTitle] = "Description",
				[NameTitle] = "Name",
				[SteamIdTitle] = "SteamID",
				[ProfileTitle] = "Profile",
				[InvitedToClan] = "You have been invited to the clan",
				[BackPage] = "<",
				[NextPage] = ">",
				[TopClansTitle] = "Top Clans",
				[TopPlayersTitle] = "Top Players",
				[TopTitle] = "Top",
				[ScoreTitle] = "Score",
				[KillsTitle] = "Kills",
				[DeathsTitle] = "Deaths",
				[KDTitle] = "K/D",
				[ResourcesTitle] = "Resources",
				[LeftTitle] = "Left",
				[EditTitle] = "Edit",
				[InviteTitle] = "Invite",
				[SearchTitle] = "Search...",
				[ClanInvitation] = "Clan invitation",
				[InviterTitle] = "Inviter",
				[AcceptTitle] = "Accept",
				[CancelTitle] = "Cancel",
				[PlayerTitle] = "Player",
				[ClanTitle] = "Clan",
				[NotMemberOfClan] = "You are not a member of a clan :(",
				[SelectItemTitle] = "Select item",
				[CloseTitle] = "✕",
				[SelectTitle] = "Select",
				[ClanCreationTitle] = "Clan creation",
				[ClanNameTitle] = "Clan name",
				[AvatarTitle] = "Avatar",
				[UrlTitle] = "http://...",
				[CreateTitle] = "Create",
				[LastLoginTitle] = "Last login",
				[DemoteModerTitle] = "Demote moder",
				[PromoteModerTitle] = "Promote moder",
				[PromoteLeaderTitle] = "Promote leader",
				[KickTitle] = "Kick",
				[GatherRatesTitle] = "Gather rates",
				[CreateClanTitle] = "Create a clan",
				[FriendlyFireTitle] = "Friendly Fire",
				[AllyFriendlyFireTitle] = "Ally FF",
				[InvitesTitle] = "Invites",
				[AllyInvites] = "Ally Invites",
				[ClanInvitesTitle] = "Clan Invites",
				[IncomingAllyTitle] = "Incoming Ally",
				[LeaderTransferTitle] = "Leadership Transfer Confirmation",
				[SelectSkinTitle] = "Select skin",
				[EnterSkinTitle] = "Enter skin...",
				[NotModer] = "You are not a clan moderator!",
				[SuccsessKick] = "You have successfully kicked player '{0}' from the clan!",
				[WasKicked] = "You have been kicked from the clan :(",
				[NotClanMember] = "You are not a member of a clan!",
				[NotClanLeader] = "You are not a clan leader!",
				[AlreadyClanMember] = "You are already a member of the clan!",
				[ClanTagLimit] = "Clan tag must contain from {0} to {1} characters!",
				[ClanExists] = "Clan with that tag already exists!",
				[ClanCreated] = "Clan '{0}' has been successfully created!",
				[ClanDisbandedTitle] = "You have successfully disbanded the clan",
				[ClanLeft] = "You have successfully left the clan!",
				[PlayerNotFound] = "Player `{0}` not found!",
				[ClanNotFound] = "Clan `{0}` not found!",
				[ClanAlreadyModer] = "Player `{0}` is already a moderator!",
				[PromotedToModer] = "You've promoted `{0}` to moderator!",
				[NotClanModer] = "Player `{0}` is not a moderator!",
				[DemotedModer] = "You've demoted `{0}` to member!",
				[FFOn] = "Friendly Fire turned <color=#7FFF00>on</color>!",
				[AllyFFOn] = "Ally Friendly Fire turned <color=#7FFF00>on</color>!",
				[FFOff] = "Friendly Fire turned <color=#FF0000>off</color>!",
				[AllyFFOff] = "Ally Friendly Fire turned <color=#FF0000>off</color>!",
				[Help] =
					"Available commands:\n/clan - display clan menu\n/clan create \n/clan leave - Leave your clan\n/clan ff - Toggle friendlyfire status",
				[ModerHelp] =
					"\nModerator commands:\n/clan invite <name/steamid> - Invite a player\n/clan withdraw <name/steamid> - Cancel an invite\n/clan kick <name/steamid> - Kick a member\n/clan allyinvite <clanTag> - Invite the clan an alliance\n/clan allywithdraw <clanTag> - Cancel the invite of an alliance of clans\n/clan allyaccept <clanTag> - Accept the invite of an alliance with the clan\n/clan allycancel <clanTag> - Cancel the invite of an alliance with the clan\n/clan allyrevoke <clanTag> - Revoke an allyiance with the clan",
				[AdminHelp] =
					"\nOwner commands:\n/clan promote <name/steamid> - Promote a member\n/clan demote <name/steamid> - Demote a member\n/clan disband - Disband your clan",
				[HeAlreadyClanMember] = "The player is already a member of the clan.",
				[AlreadyInvitedInClan] = "The player has already been invited to your clan!",
				[SuccessInvited] = "You have successfully invited the player '{0}' to the '{1}' clan",
				[SuccessInvitedSelf] = "Player '{0}' invited you to the '{1}' clan",
				[ClanJoined] = "Congratulations! You have joined the clan '{0}'.",
				[WasInvited] = "Player '{0}' has accepted your invitation to the clan!",
				[DeclinedInvite] = "You have declined an invitation to join the '{0}' clan",
				[DeclinedInviteSelf] = "Player '{0}' declined the invitation to the clan!",
				[DidntReceiveInvite] = "Player `{0}` did not receive an invitation from your clan",
				[YourInviteDeclined] = "Your invitation to player '{0}' to the clan was declined by `{1}`",
				[CancelledInvite] = "Clan '{0}' canceled the invitation",
				[CancelledYourInvite] = "You canceled the invitation to the clan for the player '{0}'",
				[CannotDamage] = "You cannot damage your clanmates! (<color=#7FFF00>/clan ff</color>)",
				[AllyCannotDamage] = "You cannot damage your ally clanmates! (<color=#7FFF00>/clan allyff</color>)",
				[SetDescription] = "You have set a new clan description",
				[MaxDescriptionSize] = "The maximum number of characters for describing a clan is {0}",
				[NotDescription] = "Clan leader didn't set description",
				[ContainsForbiddenWords] = "The title contains forbidden words!",
				[NoPermCreateClan] = "You do not have permission to create a clan",
				[NoPermJoinClan] = "You do not have permission to join a clan",
				[NoPermKickClan] = "You do not have permission to kick clan members",
				[NoPermLeaveClan] = "You do not have permission to leave this clan",
				[NoPermDisbandClan] = "You do not have permission to disband this clan",
				[NoPermClanSkins] = "You do not have permission to use clan skins",
				[NoAllies] = "Unfortunately\nYou have no allies :(",
				[NoInvites] = "No invitations :(",
				[AllInviteExist] = "Invitation has already been sent to this clan",
				[AlreadyAlliance] = "You already have an alliance with this clan",
				[AllySendedInvite] = "'{0}' invited the '{1}' clan to join an alliance",
				[YouAllySendedInvite] = "You invited the '{0}' clan to join an alliance",
				[SelfAllySendedInvite] = "Clan '{0}' invited you to join an alliance",
				[NoFoundInviteAlly] = "'{0}' clan invitation not found",
				[AllyAcceptInviteTitle] = "You have formed an alliance with the '{0}' clan",
				[RejectedInviteTitle] = "Your clan has rejected an alliance invite from the '{0}' clan",
				[SelfRejectedInviteTitle] = "'{0}' clan rejects the alliance proposal",
				[WithdrawInviteTitle] = "Your clan has withdrawn an invitation to an alliance with the '{0}' clan",
				[SelfWithdrawInviteTitle] = "'{0}' clan withdrew invitation to alliance",
				[SendAllyInvite] = "Send Invite",
				[CancelAllyInvite] = "Cancel Invite",
				[WithdrawAllyInvite] = "Withdraw Invite",
				[ALotOfMembers] = "The clan has the maximum amount of players!",
				[ALotOfModers] = "The clan has the maximum amount of moderators!",
				[ALotOfAlliances] = "The clan has the maximum amount of alliances!",
				[NextBtn] = "▼",
				[BackBtn] = "▲",
				[NoAlly] = "You have no alliance with the '{0}' clan",
				[SelfBreakAlly] = "Your clan has breaking its alliance with the '{0}' clan",
				[BreakAlly] = "Clan '{0}' broke an alliance with your clan",
				[AllyRevokeTitle] = "Revoke Ally",
				[UseClanSkins] = "Use clan skins",
				[AdminDisbandClan] = "An administrator has disbanded your clan",
				[AdminDemote] = "An administrator has demoted {0} to member",
				[AdminPromote] = "An administrator has promoted {0} to moderator",
				[AdminInvite] = "An administrator has invited {0} to join your clan",
				[AdminKick] = "An administrator has kicked you from <color=#74884A>[{0}]</color>",
				[AdminKickBroadcast] = "An administrator has kicked <color=#B43D3D>[{0}]</color> from your clan",
				[AdminJoin] = "An administrator has forced you to join <color=#74884A>[{0}]</color>",
				[AdminBroadcast] = "<color=#B43D3D>[ADMIN]</color>: {0}",
				[AdminSetLeader] = "An administrator has set {0} as the clan leader",
				[AdminRename] = "An administrator changed your clan tag to <color=#74884A>[{0}]</color>",
				[ClanInfoTitle] =
					"<size=18><color=#ffa500>Clans</color></size>",
				[ClanInfoTag] = "\nClanTag: <color=#b2eece>{0}</color>",
				[ClanInfoDescription] = "\nDescription: <color=#b2eece>{0}</color>",
				[ClanInfoOnline] = "\nMembers Online: {0}",
				[ClanInfoOffline] = "\nMembers Offline: {0}",
				[ClanInfoEstablished] = "\nEstablished: <color=#b2eece>{0}</color>",
				[ClanInfoLastOnline] = "\nLast Online: <color=#b2eece>{0}</color>",
				[ClanInfoAlliances] = "\nAlliances: <color=#b2eece>{0}</color>",
				[ClanInfoAlliancesNone] = "None",
				[NoPermissions] = "You have insufficient permission to use that command",
				[TagColorFormat] = "The hex string must be 6 characters long, and be a valid hex color",
				[TagColorInstalled] = "You have set a new clan tag color: #{0}!",
				[TagColorTitle] = "Tag Color",
				[PlayTimeTitle] = "Play Time",
				[AllyChatSyntax] = "Error syntax! Usage: /{0} [message]",
				[AllyChatFormat] = "[{0}] [{1}]{2}[/#]: {3}",
				[AllyChatPrefix] = "[#a1ff46][ALLY CHAT][/#]: {0}",
				[ClanChatSyntax] = "Error syntax! Usage: /{0} [message]",
				[ClanChatFormat] = "[{0}] [{1}]{2}[/#]: {3}",
				[ClanChatPrefix] = "[#a1ff46][CLAN CHAT][/#]: {0}",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
				[NotAllowedEditClanImage] = "You're not allowed to edit the clan image!",
				[NotMoney] = "You don't have enough money!",
				[PaidCreateTitle] = "Create for ${0}",
				[PaidJoinMsg] = "You don't have enough money to join the clan, it costs ${0}.",
				[PaidLeaveMsg] = "You don't have enough money to leave the clan, it costs ${0}.",
				[PaidDisbandMsg] = "You don't have enough money to disband the clan, it costs ${0}.",
				[PaidKickMsg] = "You don't have enough money to kick the clan member, it costs ${0}.",
				[PaidSetSkinMsg] = "You don't have enough money to set the clan skin, it costs ${0}.",
				[PaidSetAvatarMsg] = "You don't have enough money to set the clan avatar, it costs ${0}.",
				[PaidSendInviteMsg] = "You don't have enough money to send an invitation to the clan, it costs ${0}.",
				[LeaveTitle] = "Leave",
				[ConfirmLeaveTitle] = "Leaving the clan",
				[ConfirmLeaveMessage] = "To confirm, type <b>{0}</b> in the box below",
				[ConfirmAvatarTitle] = "Change avatar",
				[ConfirmAvatarMessage] = "Enter the avatar link in the box below",
				[ConfirmResourceTitle] = "Remove resource",
				[ConfirmResourceMessage] = "Are you sure you want to remove this resource?",
				["aboutclan"] = "About Clan",
				["memberslist"] = "Members",
				["clanstop"] = "Top Clans",
				["playerstop"] = "Top Players",
				["resources"] = "Gather Rates",
				["skins"] = "Skins",
				["playerslist"] = "Players List",
				["alianceslist"] = "Aliances"
			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				[ClansMenuTitle] = "Кланы",
				[AboutClan] = "О клане",
				[ChangeAvatar] = "Сменить аватар",
				[EnterLink] = "Введите ссылку",
				[LeaderTitle] = "Лидер",
				[GatherTitle] = "Добыча",
				[RatingTitle] = "Рейтинг",
				[MembersTitle] = "Участники",
				[DescriptionTitle] = "Описание",
				[NameTitle] = "Имя",
				[SteamIdTitle] = "SteamID",
				[ProfileTitle] = "Профиль",
				[InvitedToClan] = "Вы приглашены в клан",
				[BackPage] = "<",
				[NextPage] = ">",
				[TopClansTitle] = "Топ Кланов",
				[TopPlayersTitle] = "Топ Игроков",
				[TopTitle] = "Топ",
				[ScoreTitle] = "Очки",
				[KillsTitle] = "Убийства",
				[DeathsTitle] = "Смерти",
				[KDTitle] = "У/С",
				[ResourcesTitle] = "Ресурсы",
				[LeftTitle] = "Слева",
				[EditTitle] = "Редактировать",
				[InviteTitle] = "Пригласить",
				[SearchTitle] = "Поиск...",
				[ClanInvitation] = "Приглашение в клан",
				[InviterTitle] = "Приглащающий",
				[AcceptTitle] = "Принять",
				[CancelTitle] = "Отменить",
				[PlayerTitle] = "Игрок",
				[ClanTitle] = "Клан",
				[NotMemberOfClan] = "Вы не являетесь членом клана :(",
				[SelectItemTitle] = "Выбрать предмет",
				[CloseTitle] = "✕",
				[SelectTitle] = "Выбрать",
				[ClanCreationTitle] = "Создание клана",
				[ClanNameTitle] = "Название клана",
				[AvatarTitle] = "Аватар",
				[UrlTitle] = "http://...",
				[CreateTitle] = "Создать",
				[LastLoginTitle] = "Последняя активность",
				[DemoteModerTitle] = "Понизить до игрока",
				[PromoteModerTitle] = "Повысить до модератора",
				[PromoteLeaderTitle] = "Повысить до лидера",
				[KickTitle] = "Исключить",
				[GatherRatesTitle] = "Норма добычи",
				[CreateClanTitle] = "Создать клан",
				[FriendlyFireTitle] = "Дружеский Огонь",
				[AllyFriendlyFireTitle] = "Включить FF",
				[InvitesTitle] = "Приглашения",
				[AllyInvites] = "Приглашения в альянс",
				[ClanInvitesTitle] = "Приглашения в клан",
				[IncomingAllyTitle] = "Приглашения к альянсу",
				[LeaderTransferTitle] = "Подтверждение передачи лидерства",
				[SelectSkinTitle] = "Выбрать скин",
				[EnterSkinTitle] = "Введите скин...",
				[NotModer] = "Вы не являетесь модератором клана!",
				[SuccsessKick] = "Вы успешно выгнали игрока '{0}' из клана!",
				[WasKicked] = "Вас выгнали из клана :(",
				[NotClanMember] = "Вы не являетесь членом клана!",
				[NotClanLeader] = "Вы не являетесь лидером клана!",
				[AlreadyClanMember] = "Вы уже являетесь членом клана!",
				[ClanTagLimit] = "Название клана должно содержать от {0} до {1} символов!",
				[ClanExists] = "Клан с таким названием уже существует!",
				[ClanCreated] = "Клан '{0}' успешно создан!",
				[ClanDisbandedTitle] = "Вы успешно распустили клан",
				[ClanLeft] = "Вы успешно покинули клан!",
				[PlayerNotFound] = "Игрок `{0}` не найден!",
				[ClanNotFound] = "Клан `{0}` не найден!",
				[ClanAlreadyModer] = "Игрок `{0}` уже является модератором!",
				[PromotedToModer] = "Вы повысили `{0}` до модератора!",
				[NotClanModer] = "Игрок `{0}` не является модератором!",
				[DemotedModer] = "Вы понизили `{0}` до участника!",
				[FFOn] = "Дружественный огонь <color=#7FFF00>включен</color>!",
				[AllyFFOn] = "Дружественный огонь альянса <color=#7FFF00>включен</color>!",
				[FFOff] = "Дружественный огонь <color=#FF0000>выключен</color>!",
				[AllyFFOff] = "Дружественный огонь альянса <color=#FF0000>выключен</color>!",
				[Help] =
					"Доступные команды:\n/clan - отобразить меню клана\n/clan create - создать клан \n/clan leave - покинуть клан\n/clan ff - изменить режим дружественного огня",
				[ModerHelp] =
					"\nКоманды модератора:\n/clan invite <name/steamid> - пригласить игрока\n/clan withdraw <name/steamid> - отменить приглашение\n/clan kick <name/steamid> - исключить участника\n/clan allyinvite <clanTag> - пригласить клан в альянс\n/clan allywithdraw <clanTag> - Отменить приглашение альянса от клана\n/clan allyaccept <clanTag> - принять приглашение вступить в альянс с кланом\n/clan allycancel <clanTag> - отменить приглашение в альянс с кланом\n/clan allyrevoke <clanTag> - аннулировать альянс с кланом",
				[AdminHelp] =
					"\nКоманды лидера:\n/clan promote <name/steamid> - повысить участника\n/clan demote <name/steamid> - понизить участника\n/clan disband - распустить свой клан",
				[HeAlreadyClanMember] = "Игрок уже является членом клана.",
				[AlreadyInvitedInClan] = "Игрок уже приглашен в ваш клан!",
				[SuccessInvited] = "Вы успешно пригласили игрока '{0}' в клан '{1}'",
				[SuccessInvitedSelf] = "Игрок '{0}' пригласил вас в клан '{1}'",
				[ClanJoined] = "Поздравляю! Вы вступили в клан '{0}'.",
				[WasInvited] = "Игрок '{0}' принял ваше приглашение в клан!",
				[DeclinedInvite] = "Вы отклонили приглашение вступить в клан '{0}'",
				[DeclinedInviteSelf] = "Игрок '{0}' отклонил приглашение в клан!",
				[DidntReceiveInvite] = "Игрок `{0}` не получил приглашение от вашего клана",
				[YourInviteDeclined] = "Ваше приглашение игрока '{0}' в клан было отклонено `{1}`",
				[CancelledInvite] = "Клан '{0}' отменил приглашение",
				[CancelledYourInvite] = "Вы отменили приглашение в клан для игрока '{0}'",
				[CannotDamage] = "Вы не можете повредить своим соклановцам! (<color=#7FFF00>/clan ff</color>)",
				[AllyCannotDamage] = "Вы не можете повредить своим союзникам! (<color=#7FFF00>/clan allyff</color>)",
				[SetDescription] = "Вы установили новое описание клана",
				[MaxDescriptionSize] = "Максимальное количество символов для описания клана: {0}",
				[NotDescription] = "Лидер клана не установил описание",
				[ContainsForbiddenWords] = "Название содержит запрещенные слова!",
				[NoPermCreateClan] = "У вас нет необходимого разрешения на создание клана",
				[NoPermJoinClan] = "У вас нет необходимого разрешения на вступление в клан",
				[NoPermKickClan] = "У вас нет необходимого разрешения для исключения членов клана",
				[NoPermLeaveClan] = "У вас нет необходимого разрешения чтобы покидать клан",
				[NoPermDisbandClan] = "У вас нет необходимого разрешения для роспуска клана",
				[NoPermClanSkins] = "У вас нет необходимого разрешения на использование клановых скинов",
				[NoAllies] = "К сожалению\nУ вас нет союзников :(",
				[NoInvites] = "Приглашения отсутствуют :(",
				[AllInviteExist] = "Приглашение уже отправлено этому клану",
				[AlreadyAlliance] = "У вас уже есть альянс с этим кланом",
				[AllySendedInvite] = "'{0}' предложил клану '{1}' вступить в альянс",
				[YouAllySendedInvite] = "Вы предложили клан '{0}' вступить в альянс",
				[SelfAllySendedInvite] = "Клан '{0}' предложил вам вступить в альянс",
				[NoFoundInviteAlly] = "Приглашение от клана '{0}' не найдено",
				[AllyAcceptInviteTitle] = "Вы заключили альянс с кланом '{0}'",
				[RejectedInviteTitle] = "Ваш клан отклонил приглашение в альянс от клана '{0}'",
				[SelfRejectedInviteTitle] = "Клан '{0}' отклоняет предложение о вступлении в альянс",
				[WithdrawInviteTitle] = "Ваш клан отозвал приглашение к альянсу с кланом '{0}'",
				[SelfWithdrawInviteTitle] = "Клан '{0}' отозвал приглашение в альянс",
				[SendAllyInvite] = "Отправить приглашение",
				[CancelAllyInvite] = "Отменить приглашение",
				[WithdrawAllyInvite] = "Отозвать приглашение",
				[ALotOfMembers] = "В клане максимальное количество игроков!",
				[ALotOfModers] = "В клане максимальное количество модераторов!",
				[ALotOfAlliances] = "Клан имеет максимальное количество альянсов!",
				[NextBtn] = "▼",
				[BackBtn] = "▲",
				[NoAlly] = "У вас нет альянса с кланом '{0}'",
				[SelfBreakAlly] = "Ваш клан разорвал свой альянс с кланом '{0}'",
				[BreakAlly] = "Клан '{0}' разорвал альянс с вашим кланом",
				[AllyRevokeTitle] = "Разорвать альянс",
				[UseClanSkins] = "Использовать клановые скины",
				[AdminDisbandClan] = "Администратор распустил ваш клан",
				[AdminDemote] = "Администратор понизил {0} до участника",
				[AdminPromote] = "Администратор повысил {0} до модератора",
				[AdminInvite] = "Администратор пригласил {0} в ваш клан",
				[AdminKick] = "Администратор выгнал вас из <color=#74884A>[{0}]</color>",
				[AdminKickBroadcast] = "Администратор выгнал <color=#B43D3D>[{0}]</color> из вашего кланаn",
				[AdminJoin] = "Администратор заставил вас присоединиться к клану <color=#74884A>[{0}]</color>",
				[AdminBroadcast] = "<color=#B43D3D>[ADMIN]</color>: {0}",
				[AdminSetLeader] = "Администратор назначил {0} лидером клана",
				[AdminRename] = "Администратор изменил название вашего клана на <color=#74884A>[{0}]</color>.",
				[ClanInfoTitle] =
					"<size=18><color=#ffa500>Clans</color></size>",
				[ClanInfoTag] = "\nНазвание: <color=#b2eece>{0}</color>",
				[ClanInfoDescription] = "\nОписание: <color=#b2eece>{0}</color>",
				[ClanInfoOnline] = "\nУчастники онлайн: {0}",
				[ClanInfoOffline] = "\nУчастники оффлайн: {0}",
				[ClanInfoEstablished] = "\nСоздано: <color=#b2eece>{0}</color>",
				[ClanInfoLastOnline] = "\nПоследняя актиность: <color=#b2eece>{0}</color>",
				[ClanInfoAlliances] = "\nАльянсы: <color=#b2eece>{0}</color>",
				[ClanInfoAlliancesNone] = "Ничего",
				[NoPermissions] = "У вас недостаточно прав для использования этой команды",
				[TagColorFormat] = "Строка HEX должна содержать 6 символов и быть допустимого HEX цвета",
				[TagColorInstalled] = "Вы установили новый цвет названия клана: #{0}!",
				[TagColorTitle] = "Цвет",
				[PlayTimeTitle] = "Игровое время",
				[AllyChatSyntax] = "Ошибка синтаксиса! Использование: /{0} [сообщение]",
				[AllyChatFormat] = "[{0}] [{1}]{2}[/#]: {3}",
				[AllyChatPrefix] = "[#a1ff46][АЛЬЯНС][/#]: {0}",
				[ClanChatSyntax] = "Ошибка синтаксиса! Использование: /{0} [сообщение]",
				[ClanChatFormat] = "[{0}] [{1}]{2}[/#]: {3}",
				[ClanChatPrefix] = "[#a1ff46][КЛАН][/#]: {0}",
				[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!",
				[NotAllowedEditClanImage] = "Вам запрещено редактировать изображение клана!",
				[NotMoney] = "У вас недостаточно денег!",
				[PaidCreateTitle] = "Создать за {0}$",
				[PaidJoinMsg] = "У вас недостаточно денег для присоединения к клану, это стоит {0}$.",
				[PaidLeaveMsg] = "У вас недостаточно денег чтобы покинуть клан, это стоит {0}$.",
				[PaidDisbandMsg] = "У вас недостаточно денег чтобы распустить клан, это стоит {0}$.",
				[PaidKickMsg] = "У вас недостаточно денег для исключения игрока из клана, это стоит {0}$.",
				[PaidSetSkinMsg] = "У вас недостаточно денег для установки скина клана, это стоит {0}$.",
				[PaidSetAvatarMsg] = "У вас недостаточно денег для установки аватара клана, это стоит {0}$.",
				[PaidSendInviteMsg] = "У вас недостаточно денег для отправления приглашения в клан, это стоит {0}$.",
				[LeaveTitle] = "Покинуть",
				[ConfirmLeaveTitle] = "Выход из клана",
				[ConfirmLeaveMessage] = "Для подтверждения введите <b>{0}</b> в поле ниже",
				[ConfirmAvatarTitle] = "Изменение аватара",
				[ConfirmAvatarMessage] = "Введите ссылку на аватар в поле ниже",
				[ConfirmResourceTitle] = "Удаление ресурса",
				[ConfirmResourceMessage] = "Вы уверены, что хотите удалить этот ресурс?",
				["aboutclan"] = "О клане",
				["memberslist"] = "Участники",
				["clanstop"] = "Топ кланов",
				["playerstop"] = "Топ игроков",
				["resources"] = "Норма добычи",
				["skins"] = "Скины",
				["playerslist"] = "Список игроков",
				["alianceslist"] = "Альянсы"
			}, this, "ru");
		}

		private string Msg(string playerID, string key, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, playerID), obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			if (player == null) return;

			SendReply(player, Msg(player.UserIDString, key, obj));
		}

		private void Reply(IPlayer player, string key, params object[] obj)
		{
			player?.Reply(string.Format(lang.GetMessage(key, this, player.Id), obj));
		}

		#endregion

		#region Convert

		#region Clans Reborn

		private readonly DateTime _epoch = new DateTime(1970, 1, 1);

		private readonly double _maxUnixSeconds = (DateTime.MaxValue - new DateTime(1970, 1, 1)).TotalSeconds;

		[ConsoleCommand("clans.reborn.convert")]
		private void CmdConsoleConvertOldClans(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			_actionConvert = ServerMgr.Instance.StartCoroutine(ConvertClansReborn());
		}

		private IEnumerator ConvertClansReborn()
		{
			ClansReborn.StoredData oldClans = null;

			try
			{
				oldClans = Interface.Oxide.DataFileSystem.GetFile("Clans")?.ReadObject<ClansReborn.StoredData>();
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (oldClans == null) yield break;

			foreach (var check in oldClans.clans)
			{
				var newClan = new ClanData
				{
					ClanTag = check.Key,
					LeaderID = check.Value.OwnerID,
					LeaderName = covalence.Players.FindPlayer(check.Value.OwnerID.ToString())?.Name,
					Avatar = string.Empty,
					Members = check.Value.ClanMembers.Keys.ToList(),
					Moderators = check.Value.ClanMembers.Where(x => x.Value.Role == ClansReborn.MemberRole.Moderator)
						.Select(x => x.Key).ToList(),
					Top = _clansList.Count + 1,
					CreationTime = ConvertTimeReborn(check.Value.CreationTime),
					LastOnlineTime = ConvertTimeReborn(check.Value.LastOnlineTime)
				};

				if (_config.AutoTeamCreation)
				{
					var leader = RelationshipManager.FindByID(check.Value.OwnerID);
					if (leader != null) newClan.FindOrCreateTeam();
				}

				_clansList.Add(newClan);
				_clanByTag[newClan.ClanTag] = newClan;

				yield return null;
			}

			yield return CoroutineEx.waitForFixedUpdate;

			Puts($"{oldClans.clans.Count} clans was converted!");
		}

		private DateTime ConvertTimeReborn(double lastTime)
		{
			return lastTime > _maxUnixSeconds
				? _epoch.AddMilliseconds(lastTime)
				: _epoch.AddSeconds(lastTime);
		}

		private static class ClansReborn
		{
			public class StoredData
			{
				public Hash<string, Clan> clans = new Hash<string, Clan>();

				public int timeSaved;

				public Hash<ulong, List<string>> playerInvites = new Hash<ulong, List<string>>();
			}

			public class Clan
			{
				public string Tag;

				public string Description;

				public ulong OwnerID;

				public double CreationTime;

				public double LastOnlineTime;

				public Hash<ulong, Member> ClanMembers = new Hash<ulong, Member>();

				public HashSet<string> Alliances = new HashSet<string>();

				public Hash<string, double> AllianceInvites = new Hash<string, double>();

				public HashSet<string> IncomingAlliances = new HashSet<string>();

				public string TagColor = string.Empty;
			}

			public class Member
			{
				public string DisplayName = string.Empty;

				public MemberRole Role;

				public bool MemberFFEnabled;

				public bool AllyFFEnabled;
			}

			public enum MemberRole
			{
				Owner,
				Council,
				Moderator,
				Member
			}
		}

		#endregion

		#region Clans (uMod)

		[ConsoleCommand("clans.umod.convert")]
		private void CmdConsoleConvertOldClansUMod(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			_actionConvert = ServerMgr.Instance.StartCoroutine(ConvertClansUMod());
		}

		private IEnumerator ConvertClansUMod()
		{
			if (!Interface.Oxide.DataFileSystem.ExistsDatafile("clan_data"))
			{
				PrintError("Clans plugin data from uMod not found!");
				yield break;
			}

			ClansUmod.StoredData oldClans = null;

			try
			{
				oldClans = Interface.Oxide.DataFileSystem.GetFile("clan_data")?.ReadObject<ClansUmod.StoredData>();
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (oldClans == null) yield break;
			
			foreach (var (clanTag, clanData) in oldClans.clans)
			{
				var newClan = new ClanData
				{
					ClanTag = clanTag,
					LeaderID = Convert.ToUInt64(clanData.OwnerID),
					LeaderName = covalence.Players.FindPlayer(clanData.OwnerID)?.Name ?? string.Empty,
					Avatar = string.Empty,
					Members = clanData.ClanMembers.Keys.Select(Convert.ToUInt64).ToList(),
					Moderators = clanData.ClanMembers
						.Where(x => x.Value.Role == ClansUmod.Member.MemberRole.Moderator)
						.Select(x => Convert.ToUInt64(x.Key)).ToList(),
					Top = _clansList.Count + 1,
					CreationTime = ConvertTimeReborn(clanData.CreationTime),
					LastOnlineTime = ConvertTimeReborn(clanData.LastOnlineTime)
				};

				if (_config.AutoTeamCreation)
				{
					var leader = RelationshipManager.FindByID(Convert.ToUInt64(clanData.OwnerID));
					if (leader != null) newClan.FindOrCreateTeam();
				}

				_clansList.Add(newClan);
				_clanByTag[newClan.ClanTag] = newClan;

				yield return null;
			}

			yield return CoroutineEx.waitForFixedUpdate;

			Puts($"{oldClans.clans.Count} clans was converted!");
		}

		private static class ClansUmod
		{
			public class StoredData
			{
				public Hash<string, Clan> clans = new Hash<string, Clan>();

				public Hash<string, List<string>> playerInvites = new Hash<string, List<string>>();
			}

			public class Clan
			{
				public string Tag;

				public string Description;

				public string OwnerID;

				public double CreationTime;

				public double LastOnlineTime;

				public Hash<string, Member> ClanMembers = new();

				public Hash<string, MemberInvite> MemberInvites = new();

				public HashSet<string> Alliances = new();

				public Hash<string, double> AllianceInvites = new();

				public HashSet<string> IncomingAlliances = new();

				public string TagColor;
			}

			public class Member
			{
				public string Name;

				public MemberRole Role;

				public enum MemberRole
				{
					Owner,
					Moderator,
					Member
				}
			}

			public class MemberInvite
			{
				public string Name;

				public double ExpiryTime;
			}
		}

		#endregion

		#region Data 2.0

		[ConsoleCommand("clans.convert.olddata")]
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

					PrintWarning($"{data.Count} players was converted!");
				});
		}

		private Dictionary<ulong, OldData> LoadOldData()
		{
			Dictionary<ulong, OldData> players = null;
			try
			{
				players = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, OldData>>($"{Name}/PlayersList");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			return players ?? new Dictionary<ulong, OldData>();
		}

		private void CondertOldData(Dictionary<ulong, OldData> players)
		{
			foreach (var check in players)
			{
				var userId = check.Key.ToString();

				var data = PlayerData.GetOrCreate(userId);
				data.SteamID = userId;
				data.DisplayName = check.Value.DisplayName;
				data.LastLogin = check.Value.LastLogin;
				data.FriendlyFire = check.Value.FriendlyFire;
				data.AllyFriendlyFire = check.Value.AllyFriendlyFire;
				data.ClanSkins = check.Value.ClanSkins;
				data.Stats = check.Value.Stats;

				PlayerData.SaveAndUnload(userId);
			}
		}

		private class OldData
		{
			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Last Login")]
			public DateTime LastLogin;

			[JsonProperty(PropertyName = "Friendly Fire")]
			public bool FriendlyFire;

			[JsonProperty(PropertyName = "Ally Friendly Fire")]
			public bool AllyFriendlyFire;

			[JsonProperty(PropertyName = "Use Clan Skins")]
			public bool ClanSkins;

			[JsonProperty(PropertyName = "Stats", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> Stats = new Dictionary<string, float>();

			[JsonProperty(PropertyName = "Invites", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, OldInviteData> Invites = new Dictionary<string, OldInviteData>();
		}

		private class OldInviteData
		{
			[JsonProperty(PropertyName = "Inviter Name")]
			public string InviterName;

			[JsonProperty(PropertyName = "Inviter Id")]
			public ulong InviterId;
		}

		#endregion

		#region OldStats
		
		private bool _convertOldStats;

		#region OldStats.Commands
		
		[ConsoleCommand("clans.convert.old.stats")]
		private void ConvertOldStats(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;
			
			if (_convertOldStats)
			{
				SendReply(arg, "Already converting old stats");
				return;
			}
			
			var players = PlayerData.GetFiles();
			if (players is not {Length: > 0})
			{
				SendReply(arg, "No players found");
				return;
			}
			
			_convertOldStats = true;
				
			SendReply(arg, "Converting old stats");
				
			Parallel.For(0, players.Length, i =>
			{
				ConvertPlayerStats(players[i]);
			});
				
			SendReply(arg, "Converting old stats was finished");
				
			_convertOldStats = false;
		}

		#endregion
        
		#region OldStats.Helpers
		
		private static void ConvertPlayerStats(string userId)
		{
			var data = PlayerData.GetOrLoad(userId);
			data.StatsStorage = new StatsStorage();
			
			foreach (var item in data.Stats)
			{
				var lootType = ConvertShortnameToLootType(item.Key);

				data.StatsStorage.Add(lootType, item.Key, item.Value);
			}

			PlayerData.Save(userId);
		}
		
		#endregion

		#endregion
		
		#endregion

		#region Data 2.0

		#region Player

		private Dictionary<string, PlayerData> _usersData = new Dictionary<string, PlayerData>();

		private class PlayerData
		{
			#region Main

			#region Fields

			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Steam ID")]
			public string SteamID;

			[JsonProperty(PropertyName = "Last Login")]
			public DateTime LastLogin;

			[JsonProperty(PropertyName = "Friendly Fire")]
			public bool FriendlyFire;

			[JsonProperty(PropertyName = "Ally Friendly Fire")]
			public bool AllyFriendlyFire;

			[JsonProperty(PropertyName = "Use Clan Skins")]
			public bool ClanSkins;

			[JsonProperty(PropertyName = "Stats", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> Stats = new Dictionary<string, float>();

			[JsonProperty(PropertyName = "Stats Storage")]
			public StatsStorage StatsStorage = new StatsStorage();
			
			#endregion

			#region Stats

			[JsonIgnore]
			public float Kills
			{
				get
				{
					var kills = StatsStorage.GetAmount(LootSettings.LootType.Kill, "kills");
					return float.IsNaN(kills) || float.IsInfinity(kills) ? 0 : kills;
				}
			}

			[JsonIgnore]
			public float Deaths
			{
				get
				{
					var deaths = StatsStorage.GetAmount(LootSettings.LootType.Kill, "deaths");
					return float.IsNaN(deaths) || float.IsInfinity(deaths) ? 0 : deaths;
				}
			}

			[JsonIgnore]
			public float KD
			{
				get
				{
					var kd = Kills / Deaths;
					return float.IsNaN(kd) || float.IsInfinity(kd) ? 0 : kd;
				}
			}

			[JsonIgnore]
			public float Resources
			{
				get
				{
					var resources = _config.Resources.Sum(x => StatsStorage.GetAmountByShortName(x));
					return float.IsNaN(resources) || float.IsInfinity(resources) ? 0 : resources;
				}
			}

			[JsonIgnore]
			public float Score => Mathf.Round(StatsStorage.GetScores());

			public float GetAmount(string key)
			{
				return Mathf.Round(StatsStorage.GetAmountByShortName(key));
			}

			public float GetTotalFarm(ClanData clan)
			{
				return (float) Math.Round(
					clan.ResourceStandarts.Values.Sum(check => Mathf.Min(GetAmount(check.ShortName) / check.Amount, 1)) /
					clan.ResourceStandarts.Count, 3);
			}

			public string GetParams(string key, ClanData clan)
			{
				switch (key)
				{
					case "gather":
					{
						var progress = GetTotalFarm(clan);
						return $"{(progress > 0 ? Math.Round(progress * 100f) : 0)}";
					}

					case "lastlogin":
					{
						return $"{LastLogin:g}";
					}

					case "playtime":
					{
						return $"{FormatTime(_instance.PlayTimeRewards_GetPlayTime(SteamID))}";
					}

					case "score":
					{
						return Score.ToString(CultureInfo.InvariantCulture);
					}

					case "kills":
					{
						return Kills.ToString(CultureInfo.InvariantCulture);
					}

					case "deaths":
					{
						return Deaths.ToString(CultureInfo.InvariantCulture);
					}

					case "kd":
					{
						return KD.ToString(CultureInfo.InvariantCulture);
					}

					default:
						return GetAmount(key).ToString(CultureInfo.InvariantCulture);
				}
			}

			#endregion

			#region Utils

			public ClanData GetClan() => _instance?.FindClanByPlayer(SteamID);

			public ClanInviteData GetInviteByTag(string clanTag) => _invites.GetClanInvite(Convert.ToUInt64(SteamID), clanTag);

			#endregion

			#endregion

			#region Data.Helpers

			private static string BaseFolder()
			{
				return "Clans" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
			}

			public static PlayerData GetOrLoad(string userId)
			{
				if (!userId.IsSteamId()) return null;

				return _config.PlayerDatabase.Enabled ? _instance.LoadOrCreatePlayerDatabaseData(userId) : GetOrLoad(BaseFolder(), userId);
			}

			public static PlayerData GetNotLoad(string userId)
			{
				if (!userId.IsSteamId()) return null;

				if (_config.PlayerDatabase.Enabled) return _instance.LoadPlayerDatabaseData(userId);

				var data = GetOrLoad(BaseFolder(), userId, false);

				return data;
			}

			public static PlayerData GetOrLoad(string baseFolder, string userId, bool load = true)
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

				if (_config.PlayerDatabase.Enabled)
					return _instance.LoadOrCreatePlayerDatabaseData(userId);

				return GetOrLoad(userId) ?? (_instance._usersData[userId] = new PlayerData
				{
					SteamID = userId,
					ClanSkins = _config.Skins.DefaultValueDisableSkins,
					FriendlyFire = _config.FriendlyFire.FriendlyFire,
					AllyFriendlyFire = _config.AllianceSettings.DefaultFF
				});
			}

			public static bool IsLoaded(string userId)
			{
				return _instance._usersData.ContainsKey(userId);
			}

			public static void Save()
			{
#if TESTING
				using (new StopwatchWrapper("Save players"))
#endif
				{

					_instance?._usersData?.Keys.ToList().ForEach(Save);
				}
			}

			public static void Save(string userId)
			{
				if (!_instance._usersData.TryGetValue(userId, out var data))
					return;

				if (_config.PlayerDatabase.Enabled)
				{
					_instance.SaveData(userId, _instance.LoadPlayerDatabaseData(userId));
					return;
				}

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

			#region Data.Utils

			public static string[] GetFiles()
			{
				return _config.PlayerDatabase.Enabled ? _instance.GetKnownPlayersFromPlayerDatabase() : GetFiles(BaseFolder());
			}

			private static string[] GetFiles(string baseFolder)
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

			#region Data.Wipe

			public static void DoWipe(string userId)
			{
				Interface.Oxide.DataFileSystem.DeleteDataFile(BaseFolder() + userId);
			}

			#endregion

			#region All Players

			public static IEnumerator InitTopCoroutine()
			{
#if TESTING
				using (new StopwatchWrapper("Init top coroutine"))
#endif
				{
					while (_instance._wipePlayers != null)
					{
						yield return CoroutineEx.waitForSeconds(1);
					}


					var users = GetFiles();

					yield return CoroutineEx.waitForFixedUpdate;

					var list = new List<PlayerData>(users.Length);

					for (var i = 0; i < users.Length; i++)
					{
						var data = GetNotLoad(users[i]);
						if (data == null || string.IsNullOrEmpty(data.DisplayName)) continue;

						list.Add(data);

						if (i % 100 == 0)
							yield return CoroutineEx.waitForFixedUpdate;
					}

					yield return CoroutineEx.waitForFixedUpdate;

					var topPlayers = list.Select(x => new TopPlayerData(x));

					list.Clear();

					_instance.SortPlayers(ref topPlayers);

					topPlayers.Clear();
					
					yield return CoroutineEx.waitForFixedUpdate;

					_instance.SortClans();

					ClanTopUpdated();

					yield return HandleTopCoroutine();
				}
			}

			public static IEnumerator HandleTopCoroutine()
			{
#if TESTING
				using (new StopwatchWrapper("Handle top coroutine"))
#endif
				{
					foreach (var (_, topPlayerData) in _instance.TopPlayers)
						topPlayerData.UpdateData();

					_instance.TopPlayers = Enumerable.OrderByDescending(_instance.TopPlayers, data => data.Value.Score).ToDictionary(x => x.Key, y => y.Value);

					_instance._topPlayerList = _instance.TopPlayers.Keys.ToList();

					var playerTop = 0;
					foreach (var (_, topPlayerData) in _instance.TopPlayers)
					{
						topPlayerData.Top = ++playerTop;
					}

					_instance._lastPlayerTop = playerTop;

					yield return CoroutineEx.waitForFixedUpdate;

					_instance.SortClans();

					ClanTopUpdated();

					_instance._topHandle = null;
				}
			}
			
			#endregion
		}

		private class StatsStorage
		{
			#region Fields

            [JsonProperty(PropertyName = "Loot Storage", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<LootSettings.LootType, LootStorageItems> LootStorage = new Dictionary<LootSettings.LootType, LootStorageItems>();
            
			#endregion

			#region Loot Storage

			public class LootStorageItems
			{
				[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, float> Items = new Dictionary<string, float>();
			}

			#endregion
            
			#region Cache Fields
			
			[JsonIgnore]
			private readonly Dictionary<LootSettings.LootType, float> _typeAmountsCache = new Dictionary<LootSettings.LootType, float>();
			
			[JsonIgnore]
			private readonly Dictionary<string, float> _shortNameAmountsCache = new Dictionary<string, float>();
			
			[JsonIgnore]
			private float _totalScoresCache;

			[JsonIgnore]
			private bool _needsCacheUpdate = true;

			#endregion
			
			#region Public Methods

			public void Add(LootSettings.LootType type, string key, float value)
			{
				if (string.IsNullOrEmpty(key)) return;
				
				if (!LootStorage.TryGetValue(type, out var storage)) 
					LootStorage.TryAdd(type, storage = new LootStorageItems());

				if (storage.Items.ContainsKey(key))
					storage.Items[key] += value;
				else
					storage.Items.TryAdd(key, value);

				_needsCacheUpdate = true;
			}

			public float GetScores()
			{
				EnsureCache();
				return _totalScoresCache;
			}

			public float GetAmountByShortName(string shortName)
			{
				EnsureCache();
				return _shortNameAmountsCache.GetValueOrDefault(shortName, 0f);
			}

			public float GetAmountByLootType(LootSettings.LootType type)
			{
				EnsureCache();
				return _typeAmountsCache.GetValueOrDefault(type, 0f);
			}
			
			public float GetAmount(LootSettings.LootType type, string shortName)
			{
				return LootStorage.TryGetValue(type, out var storage) ? storage.Items.GetValueOrDefault(shortName, 0f) : 0f;
			}

			#endregion
			
			#region Private Methods
			
			private void UpdateCaches()
			{
				_totalScoresCache = 0f;
				_typeAmountsCache.Clear();
				_shortNameAmountsCache.Clear();

				foreach (var (type, loot) in LootStorage)
				{
					UpdateTypeAmountCache(type, loot.Items);
					UpdateShortNameAmountCache(loot.Items);
					UpdateTotalScoresCache(type, loot.Items);
				}
			}
			
			private void EnsureCache()
			{
				if (_needsCacheUpdate)
				{
					UpdateCaches();
					
					_needsCacheUpdate = false;
				}
			}
			
			private void UpdateTypeAmountCache(LootSettings.LootType type, Dictionary<string, float> loot)
			{
				_typeAmountsCache[type] = loot.Values.Sum();
			}

			private void UpdateShortNameAmountCache(Dictionary<string, float> loot)
			{
				foreach (var (shortName, amount) in loot)
				{
					_shortNameAmountsCache[shortName] = _shortNameAmountsCache.GetValueOrDefault(shortName) + amount;
				}
			}

			private void UpdateTotalScoresCache(LootSettings.LootType lootType, Dictionary<string, float> loot)
			{
				foreach (var (shortName, amount) in loot)
				{
					if (_config.Loot.TryGetLoot(lootType, shortName, out var score))
					{
						_totalScoresCache += amount * score;
					}
				}
			}
			
			#endregion
		}

		#region PlayerDatabase

		private PlayerData LoadOrCreatePlayerDatabaseData(string userId)
		{
			if (_usersData.TryGetValue(userId, out var data))
				return data;

			data = LoadPlayerDatabaseData(userId);
			if (data != null) 
				_usersData[userId] = data;

			return data;
		}
		private PlayerData LoadPlayerDatabaseData(string userId)
		{
			if (_usersData.TryGetValue(userId, out var data))
				return data;

			var success =
				PlayerDatabase?.Call<string>("GetPlayerDataRaw", userId, _config.PlayerDatabase.Field);
			if (string.IsNullOrEmpty(success))
			{
				data = new PlayerData
				{
					SteamID = userId,
					ClanSkins = _config.Skins.DefaultValueDisableSkins,
					FriendlyFire = _config.FriendlyFire.FriendlyFire,
					AllyFriendlyFire = _config.AllianceSettings.DefaultFF
				};

				SaveData(userId, data);
				return data;
			}

			if ((data = JsonConvert.DeserializeObject<PlayerData>(success)) == null)
			{
				data = new PlayerData
				{
					SteamID = userId,
					ClanSkins = _config.Skins.DefaultValueDisableSkins,
					FriendlyFire = _config.FriendlyFire.FriendlyFire,
					AllyFriendlyFire = _config.AllianceSettings.DefaultFF
				};

				SaveData(userId, data);
				return data;
			}

			return data;
		}

		private void SaveData(string userId, PlayerData data)
		{
			if (data == null) return;

			var serializeObject = JsonConvert.SerializeObject(data);
			if (serializeObject == null) return;

			PlayerDatabase?.Call("SetPlayerData", userId, _config.PlayerDatabase.Field, serializeObject);
		}

		private string[] GetKnownPlayersFromPlayerDatabase()
		{
			return (PlayerDatabase?.Call("GetAllKnownPlayers") as List<string>)?.ToArray() ?? Array.Empty<string>();
		}
		
		#endregion

		#endregion

		#region Invites

		private static InvitesData _invites;

		private void SaveInvites()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Invites", _invites);
		}

		private void LoadInvites()
		{
			try
			{
				_invites = Interface.Oxide.DataFileSystem.ReadObject<InvitesData>($"{Name}/Invites");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			_invites ??= new InvitesData();
		}

		private class InvitesData
		{
			#region Player Invites

			[JsonProperty(PropertyName = "Player Invites", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ClanInviteData> PlayersInvites =
				new List<ClanInviteData>();

			public bool CanSendInvite(ulong userId, string clanTag)
			{
				return !PlayersInvites.Exists(invite => invite.RetrieverId == userId && invite.ClanTag == clanTag);
			}

			public ClanInviteData GetClanInvite(ulong userId, string clanTag)
			{
				return PlayersInvites.Find(x => x.RetrieverId == userId && x.ClanTag == clanTag);
			}

			public List<ClanInviteData> GetPlayerClanInvites(ulong userId)
			{
				return PlayersInvites.FindAll(x => x.RetrieverId == userId);
			}

			public List<ClanInviteData> GetClanPlayersInvites(string clanTag)
			{
				return PlayersInvites.FindAll(x => x.ClanTag == clanTag);
			}

			public void AddPlayerInvite(ulong userId, ulong senderId, string senderName, string clanTag)
			{
				PlayersInvites.Add(new ClanInviteData
				{
					InviterId = senderId,
					InviterName = senderName,
					RetrieverId = userId,
					ClanTag = clanTag
				});
			}

			public void RemovePlayerInvites(ulong userId)
			{
				PlayersInvites.RemoveAll(x => x.RetrieverId == userId);
			}

			public void RemovePlayerClanInvites(string tag)
			{
				PlayersInvites.RemoveAll(x => x.ClanTag == tag);
			}

			public void RemovePlayerClanInvites(ClanInviteData data)
			{
				PlayersInvites.Remove(data);
			}

			#endregion

			#region Alliance Invites

			[JsonProperty(PropertyName = "Alliance Invites", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<AllyInviteData> AllianceInvites =
				new List<AllyInviteData>();

			public List<AllyInviteData> GetAllyTargetInvites(string clanTag)
			{
				return AllianceInvites?.FindAll(invite => invite.SenderClanTag == clanTag);
			}

			public List<AllyInviteData> GetAllyIncomingInvites(string clanTag)
			{
				return AllianceInvites.FindAll(invite => invite.TargetClanTag == clanTag);
			}

			public bool CanSendAllyInvite(string senderClanTag, string retrivierClanTag)
			{
				if (AllianceInvites.Exists(invite =>
					    invite.TargetClanTag == retrivierClanTag &&
					    invite.SenderClanTag == senderClanTag))
					return false;

				return true;
			}

			public void AddAllyInvite(ulong senderId, string senderName, string senderClanTag, string retrivierClanTag)
			{
				AllianceInvites.Add(new AllyInviteData
				{
					SenderId = senderId,
					SenderName = senderName,
					SenderClanTag = senderClanTag,
					TargetClanTag = retrivierClanTag
				});
			}

			public void RemoveAllyInvite(string retrivierClanTag)
			{
				AllianceInvites.RemoveAll(invite => invite.TargetClanTag == retrivierClanTag);
			}

			public void RemoveAllyInviteByClan(string retrivierClanTag, string senderClan)
			{
				AllianceInvites.RemoveAll(invite =>
					invite.TargetClanTag == retrivierClanTag &&
					invite.SenderClanTag == senderClan);
			}

			#endregion

			#region Utils

			public void DoWipe()
			{
				PlayersInvites?.Clear();

				AllianceInvites?.Clear();
			}

			#endregion
		}

		private class AllyInviteData
		{
			[JsonProperty(PropertyName = "Sender ID")]
			public ulong SenderId;

			[JsonProperty(PropertyName = "Sender Name")]
			public string SenderName;

			[JsonProperty(PropertyName = "Sender Clan Tag")]
			public string SenderClanTag;

			[JsonProperty(PropertyName = "Retriever Clan Tag")]
			public string TargetClanTag;
		}

		private class ClanInviteData
		{
			[JsonProperty(PropertyName = "Inviter ID")]
			public ulong InviterId;

			[JsonProperty(PropertyName = "Inviter Name")]
			public string InviterName;

			[JsonProperty(PropertyName = "Retriever ID")]
			public ulong RetrieverId;

			[JsonProperty(PropertyName = "Clan Tag")]
			public string ClanTag;
		}

		#endregion

		#endregion

		#region Testing functions

#if TESTING
		private static void SayDebug(string message)
		{
			Debug.Log($"[Clans.Testing] {message}");
		}
		
		private void DebugMessage(string format, long time)
		{
			PrintWarning(format, time);
		}

		private void SendLogMessage(string hook, long time)
		{
			LogToFile("metrics", string.Join(";", DateTime.UtcNow.ToLongTimeString(), hook, time), this);
		}

		private class StopwatchWrapper : IDisposable
		{
			public StopwatchWrapper(string hook)
			{
				Sw = Stopwatch.StartNew();
				Hook = hook;
			}

			public static Action<string, long> OnComplete { private get; set; }

			private string Hook { get; }
			private Stopwatch Sw { get; }

			public long Time { get; private set; }

			public void Dispose()
			{
				Sw.Stop();
				Time = Sw.ElapsedMilliseconds;
				OnComplete(Hook, Time);
			}
		}

		#region Testing Players

		private string GenerateNickname()
		{
			string[] adjectives =
			{
				"Mighty", "Shining", "Crazy", "Legendary", "Merciless",
				"Fiery", "Invincible", "Deadly", "Insane", "Soulless"
			};

			string[] nouns =
			{
				"Killer", "Demon", "Mage", "Warrior", "Paladin",
				"Master", "Chaos", "Overlord", "Ninja", "Pirate"
			};

			return $"{adjectives.GetRandom()} {nouns.GetRandom()}";
		}

		private ulong GetRandomSteamID()
		{
			return (ulong) ulong.Parse($"{76561197960265728UL}{Random.Range(0, 100)}");
		}

		private List<string> _testingAvatars = new List<string>
		{
			"https://i.ibb.co/rFt1dkM/1477351892n-QCh-W.jpg",
			"https://i.ibb.co/FmnGvyR/1477351881-Ng4zu.png",
			"https://i.ibb.co/Jv2DqqG/1477351885n-TMx5.png",
			"https://i.ibb.co/S6m1BJm/1477351894-QTys-S.jpg",
			"https://i.ibb.co/7S8WgDB/1477351897d-U2f-O.jpg",
			"https://i.ibb.co/YTQ4bKn/1477351899v6i-Qb.jpg",
			"https://i.ibb.co/jLSF1xX/1477351901ks-Sl-L.png",
			"https://i.ibb.co/zs9tcm5/1477351906o9rtl.jpg",
			"https://i.ibb.co/hMKJPdX/1477351908-MRp35.jpg",
			"https://i.ibb.co/njDq8Tn/14773518832-Dogz.png",
			"https://i.ibb.co/3YhdWT1/14773519040-Sv21.jpg",
			"https://i.ibb.co/3hT84Cm/14773519105-Dak-G.jpg",
			"https://i.ibb.co/182nnHF/1477353364ru-Cm4.png",
			"https://i.ibb.co/b50XYRC/1477353367ev-DTd.jpg",
			"https://i.ibb.co/DtZ6pGS/1477396948h8m-Ew.jpg",
			"https://i.ibb.co/cN6cXXY/1477396950gm10-A.png",
			"https://i.ibb.co/sjJTshx/1477684920-Rj-KMB.png",
			"https://i.ibb.co/ngTG7qy/1477684922-UZel6.png",
			"https://i.ibb.co/C6N6fwx/1477684924-HQ6-JK.png",
			"https://i.ibb.co/C82BcpK/1477684926-Qx9f-W.png",
			"https://i.ibb.co/2FqmjDX/1477684929rnu-Yv.jpg",
			"https://i.ibb.co/1GqD1Cv/1477684931q-Uq6l.png"
		};

		private Dictionary<ulong, string> _testingPlayers = new Dictionary<ulong, string>();

		private void LoadTestingPlayers()
		{
			for (var i = 0; i < 20; i++)
			{
				var steamID = GetRandomSteamID();
				if (BasePlayer.activePlayerList.Exists(x => x.userID == steamID))
					continue;

				_testingPlayers.TryAdd(steamID, GenerateNickname());

				if (i < _testingAvatars.Count)
					AddImage(_testingAvatars[i], $"avatar_{steamID}");
			}
		}

		#endregion

		[ConsoleCommand("clans.test.gather")]
		private void CmdTestGather(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			var shortname = "stones";
			var amount = 100;
			var userID = ulong.Parse(_playerToClan.Keys.ToList().GetRandom());

			OnItemGather(userID, shortname, amount);
		}

		[ConsoleCommand("clans.test.data")]
		private void CmdTestData(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;
			
			var userID = "76561198122331656";
			
			var data = PlayerData.GetOrCreate(userID);
			if (data == null)
			{
				SendReply(arg, "Failed to get player data");
				return;
			}
			
			SendReply(arg, $"Player data: {JsonConvert.SerializeObject(data)}");
			
			PlayerData.SaveAndUnload(userID);
			
			SendReply(arg, "Player data saved");

			var players = PlayerData.GetFiles();
			if (players == null || players.Length == 0)
			{
				SendReply(arg, "Failed to get player data files");
				return;
			}

			SendReply(arg, players.Contains(userID) ? "Player data file found" : "Player data file not found");
		}
		
		[ConsoleCommand("clans.test.invite")]
		private void CmdTestAcceptInvute(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;
			
			var teamLeader = "76561199107748034";
			var userID = "76561198256217000";
			var player = new BasePlayer()
			{
				userID = 76561198256217000UL,
				UserIDString = "76561198256217025"
			};
			
			var data = PlayerData.GetOrCreate(userID);
			if (data == null)
			{
				SendReply(arg, "Failed to get player data");
				return;
			}
			
			if (data.GetClan() != null)
			{
				SendReply(arg, AlreadyClanMember);
				return;
			}
			
			var clan = FindClanByPlayer(teamLeader.ToString());
			if (clan == null) return;

			var clanTag = clan.ClanTag;
			
			clan.Join(player);
			
			NextTick(() =>
			{
				var targetClan = GetClan(clanTag);
				if (targetClan != null)
				{
					Puts($"targetClan: {targetClan}");
				}
				else
				{
					Puts($"targetClan: null");
				}
			});
		}


#endif

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.ClansExtensionMethods
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

		public static List<TResult> Select<T, TResult>(this List<T> source, Func<T, TResult> selector)
		{
			if (source == null || selector == null) return new List<TResult>();

			var r = new List<TResult>(source.Count);
			for (var i = 0; i < source.Count; i++) r.Add(selector(source[i]));

			return r;
		}

		public static List<TResult> Select<T, TResult>(this IEnumerable<T> source, Func<T, TResult> selector)
		{
			if (source == null || selector == null) return new List<TResult>();

			var r = new List<TResult>();

			using var item = source.GetEnumerator();
			while (item.MoveNext())
			{
				var converted = selector(item.Current);
				if (converted != null)
					r.Add(converted);
			}

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

		public static List<T> SkipAndTake<T>(this List<T> source, int skip, int take)
		{
			var index = Mathf.Min(Mathf.Max(skip, 0), source.Count);
			return source.GetRange(index, Mathf.Min(take, source.Count - index));
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

		private static bool HasPermission(this string a, string b)
		{
			p ??= Interface.Oxide.GetLibrary<Permission>();
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

		public static float Sum(this IEnumerable<float> source)
		{
			var sum = 0f;

			using var element = source.GetEnumerator();
			
			while (element.MoveNext()) sum += element.Current;

			return sum;
		}

		public static float Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, float> selector)
		{
			var sum = 0f;

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

		public static bool Any<TSource>(this IEnumerable<TSource> source)
		{
			if (source == null) return false;

			using var element = source.GetEnumerator();
			while (element.MoveNext())
					return true;

			return false;
		}

		public static int Count<TSource>(this IEnumerable<TSource> source)
		{
			if (source == null) return 0;

			if (source is ICollection<TSource> collectionOfT)
				return collectionOfT.Count;

			if (source is ICollection collection)
				return collection.Count;

			var count = 0;
			using var e = source.GetEnumerator();
			checked
			{
				while (e.MoveNext()) count++;
			}

			return count;
		}

		public static List<TSource> OrderByDescending<TSource, TKey>(this List<TSource> source,
			Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
		{
			if (source == null) return new List<TSource>();

			if (keySelector == null) return new List<TSource>();

			comparer ??= Comparer<TKey>.Default;

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
	}
}

#endregion Extension Methods