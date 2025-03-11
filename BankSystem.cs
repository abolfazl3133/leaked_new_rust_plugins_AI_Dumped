using Enumerable = System.Linq.Enumerable;
// #define TESTING

#if TESTING
using System.Diagnostics;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.BankSystemExtensionMethods;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	/*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Bank System", "https://discord.gg/dNGbxafuJn", "1.3.8")]
	public class BankSystem : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			Notify = null,
			UINotify = null,
			Economics = null,
			LangAPI = null;

		private const string Layer = "UI.BankSystem";

		private static BankSystem _instance;

		private readonly Dictionary<ulong, ATMData> _atmByPlayer = new Dictionary<ulong, ATMData>();

		private readonly List<VendingMachine> _vendingMachines = new List<VendingMachine>();

		private const string VendingMachinePrefab =
			"assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab";

		private enum Transaction
		{
			Deposit,
			Withdrawal,
			Transfer
		}

		private bool _enabledImageLibrary;

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Work with LangAPI?")]
			public bool UseLangAPI = true;

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"bank"};

			[JsonProperty(PropertyName = "Permission (example: banksystem.use)")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = "Starting balance")]
			public int StartingBalance = 100;

			[JsonProperty(PropertyName = "Card Image")]
			public string CardImage = "https://i.ibb.co/ZMXtF1d/image.png";

			[JsonProperty(PropertyName = "Transit Image")]
			public string DepositImage = "https://i.ibb.co/R2NT67x/image.png";

			[JsonProperty(PropertyName = "Withdraw Image")]
			public string WithdrawImage = "https://i.ibb.co/qp6VLDm/image.png";

			[JsonProperty(PropertyName = "Transfer Image")]
			public string TransferImage = "https://i.ibb.co/T2ZNvFf/image.png";

			[JsonProperty(PropertyName = "Exit Image")]
			public string ExitImage = "https://i.ibb.co/BgssZ0p/image.png";

			[JsonProperty(PropertyName = "Disable the close button in the ATM header")]
			public bool AtmDisableCloser = false;

			[JsonProperty(PropertyName = "Currency Settings")]
			public CurrencySettings Currency = new CurrencySettings
			{
				ControlSplit = true,
				DisplayName = "RUSTNote",
				ShortName = "sticks",
				Skin = 2536195910
			};

			[JsonProperty(PropertyName = "Card auto-creation")]
			public bool CardAutoCreation = false;

			[JsonProperty(PropertyName = "Use card expiration date?")]
			public bool UseCardExpiry = true;

			[JsonProperty(PropertyName = "Card expiry date (in days)")]
			public int CardExpiryDays = 7;

			[JsonProperty(PropertyName = "ATM Settings")]
			public ATMSettings Atm = new ATMSettings
			{
				MinWithdrawal = 1,
				MinDeposit = 1,
				EnableDepositFee = true,
				MinDepositFee = 0,
				MaxDepositFee = 10,
				StepDepositFee = 0.1f,
				MinWithdrawalFee = 0,
				MaxWithdrawalFee = 10,
				EnableBreakage = true,
				StepWithdrawalFee = 0.1f,
				DisplayName = "ATM",
				Skin = 2551771822,
				Repair = new RepairSettings
				{
					Items = new List<RepairItemConf>
					{
						new RepairItemConf
						{
							ShortName = "scrap",
							Amount = 2,
							Skin = 0,
							Title = string.Empty
						},
						new RepairItemConf
						{
							ShortName = "metalpipe",
							Amount = 1,
							Skin = 0,
							Title = string.Empty
						},
						new RepairItemConf
						{
							ShortName = "metal.fragments",
							Amount = 15,
							Skin = 0,
							Title = string.Empty
						}
					}
				},
				DefaultDepositFee = 1,
				EnableWithdrawalFee = true,
				DefaultWithdrawalFee = 1,
				DefaultBreakPercent = 1,
				BreakPercent = new Dictionary<string, float>
				{
					["banksystem.vip"] = 0.7f,
					["banksystem.premium"] = 0.5f
				},
				Spawn = new SpawnSettings
				{
					ConfAddons = new ATMConf
					{
						DisplayName = "ATM",
						DepositFee = 0,
						WithdrawFee = 0
					},
					Monuments = new Dictionary<string, ATMPosition>
					{
						["compound"] = new ATMPosition
						{
							Enabled = true,
							DisplayName = "ATM",
							Position = new Vector3(-3.5f,
								1.15f,
								2.7f),
							Rotation = -90,
							DepositFee = 0,
							WithdrawFee = 0
						},
						["bandit"] = new ATMPosition
						{
							Enabled = true,
							DisplayName = "ATM",
							Position = new Vector3(34.2f,
								2.35f,
								-24.7f),
							Rotation = 135,
							DepositFee = 0,
							WithdrawFee = 0
						}
					}
				},
				ShopName = "ATM #{id}",
				Commands = new[]
				{
					"secret.open.atm"
				},
				PermissionToOpenATMviaCommand = "banksystem.openatm",
				NotifyPlayerWhenNoPermissionInCommandATM = false
			};

			[JsonProperty(PropertyName = "Tracking Settings")]
			public TrackingSettings Tracking = new TrackingSettings
			{
				InHand = false,
				CostTable = new Dictionary<string, float>
				{
					["sulfur.ore"] = 5f,
					["metal.ore"] = 5f,
					["hq.metal.ore"] = 5f,
					["stone.ore"] = 5f,
					["crate_elite"] = 10f,
					["crate_normal"] = 7f,
					["crate_normal_2"] = 4
				},
				CollectibleCostTable = new Dictionary<string, float>
				{
					["sulfur.ore"] = 0.5f,
					["metal.ore"] = 0.5f,
					["hq.metal.ore"] = 0.5f,
					["stones"] = 0.5f
				}
			};

			[JsonProperty(PropertyName = "Wipe Settings")]
			public WipeSettings Wipe = new WipeSettings
			{
				Players = false,
				Logs = true,
				ATMs = true
			};

			[JsonProperty(PropertyName = "NPC Settings")]
			public NPCSettings NPC = new NPCSettings
			{
				NPCs = new List<string>
				{
					"1234567",
					"7654321",
					"4644687478"
				}
			};

			[JsonProperty(PropertyName = "Economy Settings")]
			public EconomySettings Economy = new EconomySettings
			{
				Self = true,
				AddHook = "Deposit",
				BalanceHook = "Balance",
				RemoveHook = "Withdraw",
				PluginName = "Economics"
			};

			[JsonProperty(PropertyName = "Drop Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<DropInfo> Drop = new List<DropInfo>
			{
				new DropInfo
				{
					Enabled = true,
					PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
					DropChance = 50,
					MinAmount = 2,
					MaxAmount = 5
				},
				new DropInfo
				{
					Enabled = true,
					PrefabName = "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
					DropChance = 5,
					MinAmount = 2,
					MaxAmount = 5
				},
				new DropInfo
				{
					Enabled = true,
					PrefabName = "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
					DropChance = 5,
					MinAmount = 2,
					MaxAmount = 5
				}
			};

			[JsonProperty(PropertyName = "Colors")]
			public Colors Colors = new Colors
			{
				Color1 = new IColor("#0E0E10"),
				Color2 = new IColor("#161617"),
				Color3 = new IColor("#4B68FF"),
				Color4 = new IColor("#74884A"),
				Color5 = new IColor("#FF6060"),
				Color6 = new IColor("#C4C4C4"),
				Color7 = new IColor("#CD4632"),
				Color8 = new IColor("#595651"),
				Color9 = new IColor("#4B68FF", 50),
				Color10 = new IColor("#4B68FF", 33),
				Color11 = new IColor("#FFFFFF", 20),
				Color12 = new IColor("#C4C4C4", 20),
				Color13 = new IColor("#FFFFFF"),
				Color14 = new IColor("#FFFFFF", 50),
				Color15 = new IColor("#FFFFFF", 40),
				Color16 = new IColor("#FFFFFF", 70),
				Color17 = new IColor("#FFFFFF", 10),
				Color18 = new IColor("#FFFFFF", 95),
				Color19 = new IColor("#FFFFFF", 30),
			};

			[JsonProperty(PropertyName = "UI Settings")]
			public UserInterface UI = new UserInterface
			{
				BankWidth = 900f,
				BankHeight = 595f,
				BankHeaderHeight = 70f,
				BankHeaderWidth = 850f,
				BankHeaderUpIndent = 65f,
				ShowTransactionsHistory = true,
				THFieldWidth = 190f,
				THButtonMargin = 5f,
				THButtonSize = 20f,
				ShowGatherHistory = true,
				GHFieldWidth = 190f,
				GHButtonMargin = 5f,
				GHButtonSize = 20f,
				ShowCard = true,
				CardWidth = 245f,
				CardLeftIndent = 25f,
				ShowTransfers = true,
				Transfers = new LabelSettings
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "25 -345",
					OffsetMax = "175 -325",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 16,
					Color = new IColor("#FFFFFF")
				},
				RecentTransfersTitle = new LabelSettings
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "25 -360",
					OffsetMax = "175 -345",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = new IColor("#FFFFFF", 50)
				},
				NoTransactions = new LabelSettings
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "25 -490",
					OffsetMax = "405 -360",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = new IColor("#FFFFFF", 10)
				},
				TransferCard = new LabelSettings
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "25 -515",
					OffsetMax = "175 -500",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = new IColor("#FFFFFF", 50)
				},
				TransferCardNumber = new LabelSettings
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "25 -550",
					OffsetMax = "175 -520",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = new IColor("#FFFFFF")
				},
				TransferCardAmount = new LabelSettings
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "185 -550",
					OffsetMax = "335 -520",
					FontSize = 10,
					Font = "robotocondensed-regular.ttf",
					Align = TextAnchor.MiddleLeft,
					Color = new IColor("#FFFFFF")
				},
				TransferCardButton = new LabelSettings
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "340 -550",
					OffsetMax = "400 -520",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 11,
					Color = new IColor("#FFFFFF")
				}
			};

			[JsonProperty(PropertyName = "Log Settings")]
			public LogInfo Logs = new LogInfo
			{
				CollectGatherLogs = true,
				CollectTransfersLogs = true
			};

			public VersionNumber Version;
		}

		private class LogInfo
		{
			[JsonProperty(PropertyName = "Collect Gather logs?")]
			public bool CollectGatherLogs;

			[JsonProperty(PropertyName = "Collect Transfers logs?")]
			public bool CollectTransfersLogs;
		}

		private class UserInterface
		{
			[JsonProperty(PropertyName = "Bank | Width")]
			public float BankWidth;

			[JsonProperty(PropertyName = "Bank | Height")]
			public float BankHeight;

			[JsonProperty(PropertyName = "Bank Header | Width")]
			public float BankHeaderWidth;

			[JsonProperty(PropertyName = "Bank Header | Height")]
			public float BankHeaderHeight;

			[JsonProperty(PropertyName = "Bank Header | Up Indent")]
			public float BankHeaderUpIndent;

			[JsonProperty(PropertyName = "Show Transactions history?")]
			public bool ShowTransactionsHistory;

			[JsonProperty(PropertyName = "Transactions history | Field width")]
			public float THFieldWidth;

			[JsonProperty(PropertyName = "Transactions history | Page button margin")]
			public float THButtonMargin;

			[JsonProperty(PropertyName = "Transactions history | Page button size")]
			public float THButtonSize;

			[JsonProperty(PropertyName = "Show Gather history?")]
			public bool ShowGatherHistory;

			[JsonProperty(PropertyName = "Gather history | Field width")]
			public float GHFieldWidth;

			[JsonProperty(PropertyName = "Gather history | Page button margin")]
			public float GHButtonMargin;

			[JsonProperty(PropertyName = "Gather history | Page button size")]
			public float GHButtonSize;

			[JsonProperty(PropertyName = "Show card?")]
			public bool ShowCard;

			[JsonProperty(PropertyName = "Card | Width")]
			public float CardWidth;

			[JsonProperty(PropertyName = "Card | Left indent")]
			public float CardLeftIndent;

			[JsonProperty(PropertyName = "Show Transfers?")]
			public bool ShowTransfers;

			[JsonProperty(PropertyName = "Title: Transfers")]
			public LabelSettings Transfers;

			[JsonProperty(PropertyName = "Title: Recent Transfers")]
			public LabelSettings RecentTransfersTitle;

			[JsonProperty(PropertyName = "Title: Player has no transactions Title")]
			public LabelSettings NoTransactions;

			[JsonProperty(PropertyName = "Title: Transfer by card")]
			public LabelSettings TransferCard;

			[JsonProperty(PropertyName = "Transfer by card | Card number")]
			public LabelSettings TransferCardNumber;

			[JsonProperty(PropertyName = "Transfer by card | Amount")]
			public LabelSettings TransferCardAmount;

			[JsonProperty(PropertyName = "Transfer by card | Transfer button")]
			public LabelSettings TransferCardButton;
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

		private class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;
		}

		private class IColor
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

		private class Colors
		{
			[JsonProperty(PropertyName = "First color")]
			public IColor Color1;

			[JsonProperty(PropertyName = "Second color")]
			public IColor Color2;

			[JsonProperty(PropertyName = "Third color")]
			public IColor Color3;

			[JsonProperty(PropertyName = "Fourth color")]
			public IColor Color4;

			[JsonProperty(PropertyName = "Fifth color")]
			public IColor Color5;

			[JsonProperty(PropertyName = "Sixth color")]
			public IColor Color6;

			[JsonProperty(PropertyName = "Seventh color")]
			public IColor Color7;

			[JsonProperty(PropertyName = "Eighth color")]
			public IColor Color8;

			[JsonProperty(PropertyName = "Ninth color")]
			public IColor Color9;

			[JsonProperty(PropertyName = "Tenth color")]
			public IColor Color10;

			[JsonProperty(PropertyName = "Eleventh color")]
			public IColor Color11;

			[JsonProperty(PropertyName = "Twelfth color")]
			public IColor Color12;

			[JsonProperty(PropertyName = "Thirteenth color")]
			public IColor Color13;

			[JsonProperty(PropertyName = "Fourteenth color")]
			public IColor Color14;

			[JsonProperty(PropertyName = "Fifteenth color")]
			public IColor Color15;

			[JsonProperty(PropertyName = "Sixteenth color")]
			public IColor Color16;

			[JsonProperty(PropertyName = "Seventeenth color")]
			public IColor Color17;

			[JsonProperty(PropertyName = "Eighteenth color")]
			public IColor Color18;

			[JsonProperty(PropertyName = "Nineteenth color")]
			public IColor Color19;
		}

		public class DropInfo
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Prefab")]
			public string PrefabName;

			[JsonProperty(PropertyName = "Chance")]
			public int DropChance;

			[JsonProperty(PropertyName = "Min Amount")]
			public int MinAmount;

			[JsonProperty(PropertyName = "Max Amount")]
			public int MaxAmount;
		}

		private class EconomySettings
		{
			[JsonProperty(PropertyName = "Use own economic system?")]
			public bool Self;

			[JsonProperty(PropertyName = "Plugin name")]
			public string PluginName;

			[JsonProperty(PropertyName = "Balance add hook")]
			public string AddHook;

			[JsonProperty(PropertyName = "Balance remove hook")]
			public string RemoveHook;

			[JsonProperty(PropertyName = "Balance show hook")]
			public string BalanceHook;

			public double ShowBalance(BasePlayer player)
			{
				return ShowBalance(player.UserIDString);
			}

			public double ShowBalance(ulong player)
			{
				return ShowBalance(player.ToString());
			}

			private double ShowBalance(string player)
			{
				if (Self) return _instance.Balance(player);

				var plugin = _instance?.plugins?.Find(PluginName);
				if (plugin == null) return 0;

				return Convert.ToDouble(Math.Floor(Convert.ToDouble(plugin.Call(BalanceHook, player)))
					.ToString("0.00"));
			}

			public void AddBalance(BasePlayer player, int amount)
			{
				AddBalance(player.UserIDString, amount);
			}

			private void AddBalance(ulong player, int amount)
			{
				AddBalance(player.ToString(), amount);
			}

			private void AddBalance(string player, int amount)
			{
				if (Self)
				{
					_instance.Deposit(player, amount);
					return;
				}

				var plugin = _instance?.plugins.Find(PluginName);
				if (plugin == null) return;

				switch (PluginName)
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
				return RemoveBalance(player.UserIDString, amount);
			}

			private bool RemoveBalance(ulong player, int amount)
			{
				return RemoveBalance(player.ToString(), amount);
			}

			private bool RemoveBalance(string player, int amount)
			{
				if (ShowBalance(player) < amount) return false;

				if (Self) return _instance.Withdraw(player, amount);

				var plugin = _instance?.plugins.Find(PluginName);
				if (plugin == null) return false;

				switch (PluginName)
				{
					case "Economics":
						plugin.Call(RemoveHook, player, (double) amount);
						break;
					default:
						plugin.Call(RemoveHook, player, amount);
						break;
				}

				return true;
			}

			public bool Transfer(ulong member, ulong target, int amount)
			{
				if (!RemoveBalance(member, amount)) return false;

				AddBalance(target, amount);
				return true;
			}
		}

		private class NPCSettings
		{
			[JsonProperty(PropertyName = "NPCs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> NPCs = new List<string>();
		}

		private class WipeSettings
		{
			[JsonProperty(PropertyName = "Wipe Players?")]
			public bool Players;

			[JsonProperty(PropertyName = "Wipe Logs?")]
			public bool Logs;

			[JsonProperty(PropertyName = "Wipe ATMs?")]
			public bool ATMs;
		}

		private class SpawnSettings
		{
			[JsonProperty(PropertyName = "Monuments", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, ATMPosition> Monuments;

			[JsonProperty(PropertyName = "Settings for ATMs from MonumentAddons")]
			public ATMConf ConfAddons;
		}

		private class ATMConf
		{
			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Deposit Fee")]
			public float DepositFee;

			[JsonProperty(PropertyName = "Withdraw Fee")]
			public float WithdrawFee;
		}

		private class ATMPosition : ATMConf
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Position")]
			public Vector3 Position;

			[JsonProperty(PropertyName = "Rotation")]
			public float Rotation;
		}

		private class TrackingSettings
		{
			[JsonProperty(PropertyName = "Handing out an award?")]
			public bool InHand;

			[JsonProperty(PropertyName = "Cost Table (shortname - cost)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> CostTable;

			[JsonProperty(PropertyName = "Collectible Items Cost Table (shortname - cost)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> CollectibleCostTable;
		}

		private class CurrencySettings
		{
			[JsonProperty(PropertyName =
				"Enable item split control? (if there are errors with stack plugins - it is worth turning off)")]
			public bool ControlSplit;

			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Short Name")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			public Item ToItem(int amount = 1)
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

		private class ATMSettings
		{
			#region Fields

			[JsonProperty(PropertyName = "Minimum deposit (amount)")]
			public float MinDeposit;

			[JsonProperty(PropertyName = "Minimum withdrawal (amount)")]
			public float MinWithdrawal;

			[JsonProperty(PropertyName = "Enable Deposit fee?")]
			public bool EnableDepositFee;

			[JsonProperty(PropertyName = "Minimum deposit fee")]
			public float MinDepositFee;

			[JsonProperty(PropertyName = "Maximum deposit fee")]
			public float MaxDepositFee;

			[JsonProperty(PropertyName = "Default deposit fee")]
			public float DefaultDepositFee;

			[JsonProperty(PropertyName = "Step deposit fee")]
			public float StepDepositFee;

			[JsonProperty(PropertyName = "Enable Withdrawal fee?")]
			public bool EnableWithdrawalFee;

			[JsonProperty(PropertyName = "Minimum withdrawal fee")]
			public float MinWithdrawalFee;

			[JsonProperty(PropertyName = "Maximum withdrawal fee")]
			public float MaxWithdrawalFee;

			[JsonProperty(PropertyName = "Default withdrawal fee")]
			public float DefaultWithdrawalFee;

			[JsonProperty(PropertyName = "Step withdrawal fee")]
			public float StepWithdrawalFee;

			[JsonProperty(PropertyName = "Enable breakage?")]
			public bool EnableBreakage = true;

			[JsonProperty(PropertyName = "Default breakage percentage during operation")]
			public float DefaultBreakPercent;

			[JsonProperty(PropertyName = "Breakage percentage during operation",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> BreakPercent = new Dictionary<string, float>();

			[JsonProperty(PropertyName = "Repair Settings")]
			public RepairSettings Repair;

			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			[JsonProperty(PropertyName = "Spawn Settings")]
			public SpawnSettings Spawn;

			[JsonProperty(PropertyName = "Shop Name ({id} {owner})")]
			public string ShopName;

			[JsonProperty(PropertyName = "Commands to open ATM menu",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = { };

			[JsonProperty(PropertyName = "Permission to open ATM menu via command")]
			public string PermissionToOpenATMviaCommand = string.Empty;

			[JsonProperty(PropertyName =
				"Notify a player when they don't have permissions when opening the ATM menu via command?")]
			public bool NotifyPlayerWhenNoPermissionInCommandATM;

			#endregion

			public Item ToItem()
			{
				var item = ItemManager.CreateByName("vending.machine", 1, Skin);
				if (item == null)
					return null;

				if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

				return item;
			}
		}

		private class RepairSettings
		{
			[JsonProperty(PropertyName = "Items (for 1%)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<RepairItemConf> Items = new List<RepairItemConf>();
		}

		private class RepairItemConf
		{
			[JsonProperty(PropertyName = "Short Name")]
			public string ShortName;

			[JsonProperty(PropertyName = "Amount (for 1%)")]
			public float Amount;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			[JsonProperty(PropertyName = "Title (empty - default)")]
			public string Title;

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
				Debug.LogError(ex.Message);
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

			//var baseConfig = new Configuration();

			if (_config.Version != default(VersionNumber))
			{
				if (_config.Version < new VersionNumber(1, 3, 0))
				{
					ConvertPlayerData();

					ConvertPlayerLogs();
				}

				if (_config.Version < new VersionNumber(1, 3, 6))
				{
					var color1 = Config["Colors", "Color 1"].ToString();
					var color2 = Config["Colors", "Color 2"].ToString();
					var color3 = Config["Colors", "Color 3"].ToString();
					var color4 = Config["Colors", "Color 4"].ToString();
					var color5 = Config["Colors", "Color 5"].ToString();
					var color6 = Config["Colors", "Color 6"].ToString();
					var color7 = Config["Colors", "Color 7"].ToString();
					var color8 = Config["Colors", "Color 8"].ToString();

					_config.Colors.Color1 = new IColor(color1);
					_config.Colors.Color2 = new IColor(color2);
					_config.Colors.Color3 = new IColor(color3);
					_config.Colors.Color4 = new IColor(color4);
					_config.Colors.Color5 = new IColor(color5);
					_config.Colors.Color6 = new IColor(color6);
					_config.Colors.Color7 = new IColor(color7);
					_config.Colors.Color8 = new IColor(color8);
					_config.Colors.Color9 = new IColor(color3, 50);
					_config.Colors.Color10 = new IColor(color3, 33);
					_config.Colors.Color12 = new IColor(color6, 20);

					if (_config.CardImage.Equals("https://i.imgur.com/Br9z7Ou.png"))
						_config.CardImage = "https://i.ibb.co/ZMXtF1d/image.png";

					if (_config.DepositImage.Equals("https://i.imgur.com/h2bqMu4.png"))
						_config.DepositImage = "https://i.ibb.co/R2NT67x/image.png";

					if (_config.WithdrawImage.Equals("https://i.imgur.com/lwVwxm3.png"))
						_config.WithdrawImage = "https://i.ibb.co/qp6VLDm/image.png";

					if (_config.TransferImage.Equals("https://i.imgur.com/TBIxUnz.png"))
						_config.TransferImage = "https://i.ibb.co/T2ZNvFf/image.png";

					if (_config.ExitImage.Equals("https://i.imgur.com/OGoMu9N.png"))
						_config.ExitImage = "https://i.ibb.co/BgssZ0p/image.png";
				}
			}

			_config.Version = Version;
			PrintWarning("Config update completed!");

			SaveConfig();
		}

		#endregion

		#region Data

		private ATMsData _atms;

		#region Save

		private void SaveATMs()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ATMs", _atms);
		}

		#endregion

		#region Load

		private void LoadData()
		{
			LoadATMs();
		}

		private void LoadATMs()
		{
			try
			{
				_atms = Interface.Oxide.DataFileSystem.ReadObject<ATMsData>($"{Name}/ATMs");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_atms == null) _atms = new ATMsData();
		}

		#endregion

		#region Wipe

		private void WipeData()
		{
			WipeATMs();
		}

		private void WipeATMs()
		{
			if (_atms == null)
				LoadATMs();

			_atms.ATMs?.Clear();
			_atms.LastATMID = 0;
			PrintWarning("ATMs wiped!");
		}

		#endregion

		#region Logs

		private class TransferData
		{
			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public Transaction Type;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Sender ID")]
			public ulong SenderId;

			[JsonProperty(PropertyName = "Target ID")]
			public ulong TargetId;

			public void Get(BasePlayer player, ref CuiElementContainer container, string parent)
			{
				var color = string.Empty;
				var icon = string.Empty;
				var symbol = string.Empty;
				var title = string.Empty;
				var description = string.Empty;

				#region Icon

				switch (Type)
				{
					case Transaction.Deposit:
					{
						color = _config.Colors.Color3.Get;
						icon = _instance.Msg(player, DepositIconTitle);
						symbol = _instance.Msg(player, DepositSymbolTitle);
						title = _instance.Msg(player, DepositOperationTitle);
						description = _instance.Msg(player, DepositOperationDescription);
						break;
					}
					case Transaction.Withdrawal:
					{
						color = _config.Colors.Color5.Get;
						icon = _instance.Msg(player, WithdrawalIconTitle);
						symbol = _instance.Msg(player, WithdrawalSymbolTitle);
						title = _instance.Msg(player, WithdrawalOperationTitle);
						description = _instance.Msg(player, WithdrawalOperationDescription);
						break;
					}
					case Transaction.Transfer:
					{
						var self = TargetId == 0 && SenderId != 0;
						if (self)
						{
							color = _config.Colors.Color3.Get;
							icon = _instance.Msg(player, SelfTransferlIconTitle);
							symbol = _instance.Msg(player, SelfTransferSymbolTitle);
							description = _instance.Msg(player, SelfTransferOperationDescription,
								PlayerData.GetNotLoad(SenderId.ToString())?.DisplayName);
						}
						else
						{
							color = _config.Colors.Color5.Get;
							icon = _instance.Msg(player, TransferlIconTitle);
							symbol = _instance.Msg(player, TransferSymbolTitle);
							description = _instance.Msg(player, TransferOperationDescription,
								PlayerData.GetNotLoad(TargetId.ToString())?.DisplayName);
						}

						title = _instance.Msg(player, TransferOperationTitle);
						break;
					}
				}

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0",
						OffsetMin = "5 5", OffsetMax = "30 30"
					},
					Image =
					{
						Color = color
					}
				}, parent, parent + ".Icon");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Text =
					{
						Text = $"{icon}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 20,
						Color = _config.Colors.Color13.Get
					}
				}, parent + ".Icon");

				#endregion

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "1 1",
						OffsetMin = "35 0", OffsetMax = "-37.5 0"
					},
					Text =
					{
						Text = $"{title}",
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = _config.Colors.Color13.Get
					}
				}, parent);

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0.5",
						OffsetMin = "35 0", OffsetMax = "-37.5 0"
					},
					Text =
					{
						Text = $"{description}",
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = _config.Colors.Color14.Get
					}
				}, parent);

				#endregion

				#region Value

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "-10 0"
					},
					Text =
					{
						Text = _instance.Msg(player, OperationsValueFormat, symbol, Amount),
						Align = TextAnchor.MiddleRight,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = color
					}
				}, parent);

				#endregion
			}
		}

		private enum GatherLogType
		{
			Gather,
			Loot,
			Collect,
			Kill
		}

		private class GatherLogData
		{
			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public GatherLogType Type;

			[JsonProperty(PropertyName = "Short Name")]
			public string ShortName;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;
		}

		#endregion

		#region ATMs

		private class ATMsData
		{
			[JsonProperty(PropertyName = "Last ATM ID")]
			public int LastATMID;

			[JsonProperty(PropertyName = "ATMs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ATMData> ATMs = new List<ATMData>();
		}

		private class ATMData
		{
			[JsonProperty(PropertyName = "Enity ID")]
			public ulong EntityID;

			[JsonProperty(PropertyName = "ATM ID")]
			public int ID;

			[JsonProperty(PropertyName = "Owner Id")]
			public ulong OwnerId;

			[JsonProperty(PropertyName = "Withdrawal Fee")]
			public float WithdrawalFee;

			[JsonProperty(PropertyName = "Deposit Fee")]
			public float DepositFee;

			[JsonProperty(PropertyName = "Condition")]
			public float Condition;

			[JsonProperty(PropertyName = "Balance")]
			public float Balance;

			[JsonProperty(PropertyName = "Is Admin")]
			public bool IsAdmin;

			public bool IsOwner(BasePlayer player)
			{
				return IsOwner(player.userID);
			}

			public bool IsOwner(ulong userId)
			{
				return OwnerId == userId;
			}

			public void LoseCondition()
			{
				if (IsAdmin)
					return;

				Condition -= LoseConditionAmount();
			}

			private float LoseConditionAmount()
			{
				var result = _config.Atm.DefaultBreakPercent;

				_config.Atm.BreakPercent.Where(x => OwnerId.HasPermission(x.Key)).ForEach(check =>
				{
					if (result > check.Value) result = check.Value;
				});

				return result;
			}

			public bool CanOpen()
			{
				return Condition - LoseConditionAmount() > 0;
			}
		}

		#endregion

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadData();

			if (_config.Drop.Count == 0)
				Unsubscribe(nameof(OnLootSpawn));

#if TESTING
			StopwatchWrapper.OnComplete = DebugMessage;
#endif
		}

		private void OnServerInitialized(bool initial)
		{
			LoadImages();

			if (_config.Currency.ControlSplit == false)
				Unsubscribe(nameof(OnItemSplit));

			if (Economics)
				Unsubscribe(nameof(Balance));

			RegisterPermissions();

			RegisterCommands();

			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);

			RenameATMs();

			timer.In(20, SpawnATMs);
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

			GetAvatar(player.userID,
				avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));

#if TESTING
			Debug.Log($"[OnPlayerConnected] called GetOrCreate with id={player.UserIDString}");
#endif
			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return;

			data.DisplayName = player.displayName;

			if (_config.UseCardExpiry)
				CheckCard(player.userID);

			if (_config.CardAutoCreation && !HasCard(player))
				CreateCard(player);
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			UnloadAndSavePlayer(player.UserIDString);
		}

		private void OnServerSave()
		{
			timer.In(Random.Range(2, 7), ClearDataCache);
			timer.In(Random.Range(2, 7), SaveATMs);
		}

		private void Unload()
		{
			try
			{
				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, Layer);

					UnloadAndSavePlayer(player.UserIDString);
				}

				_vendingMachines.ForEach(entity =>
				{
					if (entity != null)
						entity.Kill();
				});

				SaveATMs();
			}
			finally
			{
				_instance = null;

				_config = null;
			}
		}

		#region Wipe

		private void OnNewSave(string filename)
		{
			if (_config.Wipe.ATMs) WipeATMs();
		}

		#endregion

		#region Loot

		private void OnLootSpawn(LootContainer container)
		{
			if (container == null || _config.Drop.Count <= 0) return;

			var dropInfo = _config.Drop.Find(x => x.Enabled && x.PrefabName.Contains(container.PrefabName));
			if (dropInfo == null || Random.Range(0, 100) > dropInfo.DropChance) return;

			NextTick(() =>
			{
				if (container == null) return;

				var inventory = container.inventory;
				if (inventory == null) return;

				if (inventory.capacity <= inventory.itemList.Count)
					inventory.capacity = inventory.itemList.Count + 1;

				_config.Currency.ToItem(Random.Range(dropInfo.MinAmount, dropInfo.MaxAmount))
					?.MoveToContainer(inventory);
			});
		}

		#endregion

		#region NPC

		private void OnUseNPC(BasePlayer npc, BasePlayer player)
		{
			if (player == null || npc == null || !_config.NPC.NPCs.Contains(npc.UserIDString)) return;

			if (!string.IsNullOrEmpty(_config.Permission) && !player.HasPermission(_config.Permission))
			{
				SendNotify(player, NoPermissions, 1);
				return;
			}

			MainUi(player, first: true);
		}

		#endregion

		#region ATM

		private void OnEntitySpawned(VendingMachine entity)
		{
			NextTick(() =>
			{
				if (entity == null ||
				    entity.skinID != _config.Atm.Skin ||
				    _atms.ATMs.Exists(x => x.EntityID == entity.net.ID.Value))
					return;

				var data = new ATMData
				{
					ID = ++_atms.LastATMID,
					EntityID = entity.net.ID.Value,
					OwnerId = 0,
					Balance = 0,
					Condition = 100,
					DepositFee = _config.Atm.Spawn.ConfAddons.DepositFee,
					WithdrawalFee = _config.Atm.Spawn.ConfAddons.WithdrawFee,
					IsAdmin = true
				};

				_atms.ATMs.Add(data);

				if (!string.IsNullOrEmpty(_config.Atm.ShopName))
				{
					entity.shopName = _config.Atm.ShopName
						.Replace("{id}", data.ID.ToString())
						.Replace("{owner}", _config.Atm.Spawn.ConfAddons.DisplayName);
					entity.UpdateMapMarker();
				}
			});
		}

		private void OnEntityBuilt(Planner plan, GameObject go)
		{
			if (plan == null || go == null) return;

			var entity = go.ToBaseEntity() as VendingMachine;
			if (entity == null) return;

			var player = plan.GetOwnerPlayer();
			if (player == null) return;

			var item = player.GetActiveItem();
			if (item == null || item.skin != _config.Atm.Skin) return;

			var data = new ATMData
			{
				ID = ++_atms.LastATMID,
				EntityID = entity.net.ID.Value,
				OwnerId = player.userID,
				Balance = 0,
				Condition = 100,
				DepositFee = _config.Atm.DefaultDepositFee,
				WithdrawalFee = _config.Atm.DefaultWithdrawalFee
			};

			_atms.ATMs.Add(data);

			if (!string.IsNullOrEmpty(_config.Atm.ShopName))
			{
				entity.shopName = _config.Atm.ShopName
					.Replace("{id}", data.ID.ToString())
					.Replace("{owner}", player.displayName);
				entity.UpdateMapMarker();
			}
		}

		private void OnEntityDeath(VendingMachine entity, HitInfo info)
		{
			if (entity == null || entity.net == null)
				return;

			_atms.ATMs.RemoveAll(x => x.EntityID == entity.net.ID.Value);
		}

		private bool? CanUseVending(BasePlayer player, VendingMachine machine)
		{
			if (machine == null ||
			    player == null ||
			    machine.skinID != _config.Atm.Skin) return null;

			var data = _atms.ATMs.Find(x => x.EntityID == machine.net.ID.Value);
			if (data == null)
				return null;

			if (!HasCard(player))
			{
				SendNotify(player, NotBankCard, 1);
				return false;
			}

			if (!data.CanOpen())
			{
				SendNotify(player, BrokenATM, 1);
				return false;
			}

			_atmByPlayer[player.userID] = data;

			ATMUi(player, First: true);
			return false;
		}

		private void OnEntityKill(VendingMachine machine)
		{
			if (machine == null || machine.skinID != _config.Atm.Skin) return;

			LogToFile(Name, Environment.StackTrace, this);
		}

		#endregion

		#region Split

		private Item OnItemSplit(Item item, int amount)
		{
			if (item == null || item.info.shortname != _config.Currency.ShortName ||
			    _config.Currency.Skin != item.skin) return null;

			item.amount -= amount;
			var newItem = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
			newItem.amount = amount;
			newItem.condition = item.condition;

			if (!string.IsNullOrEmpty(item.name)) newItem.name = item.name;

			if (item.IsBlueprint()) newItem.blueprintTarget = item.blueprintTarget;
			item.MarkDirty();
			return newItem;
		}

		private object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem targetItem)
		{
			if (droppedItem == null || targetItem == null) return null;

			var item = droppedItem.GetItem();
			if (item == null) return null;

			var tItem = targetItem.GetItem();
			if (tItem == null || item.skin == tItem.skin) return null;

			return item.skin == _config.Currency.Skin || tItem.skin == _config.Currency.Skin;
		}

		private object CanStackItem(Item item, Item targetItem)
		{
			if (item == null || targetItem == null || item.skin == targetItem.skin) return null;

			return item.info.shortname == targetItem.info.shortname &&
			       (item.skin == _config.Currency.Skin || targetItem.skin == _config.Currency.Skin) &&
			       item.skin == targetItem.skin
				? (object) (item.amount + targetItem.amount < item.info.stackable)
				: null;
		}

		#endregion

		#region Tracking

		#region Gather Tracking

		private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
		{
			foreach (var item in collectible.itemList) OnCollect(player, item.itemDef.shortname, (int) item.amount);
		}

		private void OnCropGather(GrowableEntity plant, Item item, BasePlayer player)
		{
			OnGather(player, item.info.shortname, item.amount);
		}

		private void OnEntityDeath(ResourceEntity entity, HitInfo info)
		{
			if (entity == null || info == null) return;

			var initiatorPlayer = info.InitiatorPlayer;
			if (initiatorPlayer == null) return;

			var resourceEntity = entity.resourceDispenser;
			if (resourceEntity == null || resourceEntity.gatherType != ResourceDispenser.GatherType.Ore) return;

			OnGather(initiatorPlayer, entity.ShortPrefabName, 1);
		}

		private void OnGather(BasePlayer player, string shortname, int amount)
		{
			if (player == null || string.IsNullOrEmpty(shortname) || amount <= 0) return;

			AddPlayerTracking(GatherLogType.Gather, player, shortname, amount);
		}

		private void OnCollect(BasePlayer player, string shortname, int amount)
		{
			if (player == null || string.IsNullOrEmpty(shortname) || amount <= 0) return;

			AddPlayerTracking(GatherLogType.Collect, player, shortname, amount);
		}

		#endregion

		#region Loot

		private readonly Dictionary<ulong, ulong> _lootedContainers = new Dictionary<ulong, ulong>();

		private void OnLootEntity(BasePlayer player, LootContainer container)
		{
			if (player == null || container == null || container.net == null || !container.net.ID.IsValid) return;

			var netID = container.net.ID.Value;

			if (_lootedContainers.ContainsKey(netID)) return;

			_lootedContainers.Add(netID, player.userID);

			AddPlayerTracking(GatherLogType.Loot, player, container.ShortPrefabName);
		}

		#endregion

		#region Players

		private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || entity.net == null || info == null)
				return;

			var initiator = info.InitiatorPlayer;
			if (initiator == null) return;

			var isNpc = entity.IsNpc || (entity as BasePlayer)?.userID.IsSteamId() == false;
			if (!isNpc) return;

			AddPlayerTracking(GatherLogType.Kill, initiator, entity.ShortPrefabName);
		}

		#endregion

		#endregion

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

		private void CmdOpenBank(IPlayer cov, string command, string[] args)
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
			    !player.HasPermission(_config.Permission))
			{
				SendNotify(player, NoPermissions, 1);
				return;
			}

			MainUi(player, first: true);
		}

		[ConsoleCommand("UI_BankSystem")]
		private void CmdConsoleBank(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

#if TESTING
			try
			{
#endif

			switch (arg.Args[0])
			{
				case "close":
				{
					_atmByPlayer.Remove(player.userID);
					break;
				}

				case "cardcreate":
				{
					CreateCard(player);

					MainUi(player);
					break;
				}

				case "page":
				{
					ulong targetId;
					int amount, transactionPage, gatherPage;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out amount) ||
					    !ulong.TryParse(arg.Args[2], out targetId) ||
					    !int.TryParse(arg.Args[3], out transactionPage) ||
					    !int.TryParse(arg.Args[4], out gatherPage)) return;

					MainUi(player, amount, targetId, transactionPage, gatherPage);
					break;
				}

				case "setamount":
				{
					ulong targetId;
					int amount, transactionPage, gatherPage;
					if (!arg.HasArgs(5) ||
					    !ulong.TryParse(arg.Args[1], out targetId) ||
					    !int.TryParse(arg.Args[2], out transactionPage) ||
					    !int.TryParse(arg.Args[3], out gatherPage) ||
					    !int.TryParse(arg.Args[4], out amount)) return;

					MainUi(player, amount, targetId, transactionPage, gatherPage);
					break;
				}

				case "select":
				{
					int amount, type, transactionPage, gatherPage;
					ulong target;
					if (!arg.HasArgs(6)
					    || !int.TryParse(arg.Args[1], out type)
					    || !int.TryParse(arg.Args[2], out amount)
					    || !ulong.TryParse(arg.Args[3], out target)
					    || !int.TryParse(arg.Args[4], out transactionPage)
					    || !int.TryParse(arg.Args[5], out gatherPage)) return;

					if (type == 0)
						MainUi(player, amount, target, transactionPage, gatherPage, true);
					else
						ATMUi(player, 4, amount, target, true);
					break;
				}

				case "settransferinfo":
				{
					int amount;
					ulong target;
					if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out amount) || amount <= 0
					    || !ulong.TryParse(arg.Args[2], out target)) return;

					MainUi(player, amount, target);
					break;
				}

				case "ui_transfer":
				{
					int amount;
					ulong target;
					if (!arg.HasArgs(3) ||
					    !int.TryParse(arg.Args[1], out amount) || amount <= 0 ||
					    !ulong.TryParse(arg.Args[2], out target)) return;

					if (_config.Economy.ShowBalance(player) < amount)
					{
						SendNotify(player, NotEnoughMoney, 1);
						return;
					}

					_config.Economy.Transfer(player.userID, target, amount);

					var targetPlayer = RelationshipManager.FindByID(target);
					if (targetPlayer != null)
					{
						SendNotify(player, TransferedMoney, 0, amount,
							targetPlayer.displayName);

						SendNotify(targetPlayer, TargetTransferedMoney, 0, player.displayName, amount);
					}
					else
					{
						SendNotify(player, TransferedMoney, 0, amount,
							PlayerData.GetNotLoad(target.ToString())?.DisplayName);
					}

					AddTransactionLog(Transaction.Transfer, player.userID, target, amount);
					_atmByPlayer.Remove(player.userID);

					MainUi(player, amount);
					break;
				}

				case "selectpage":
				{
					ulong target;
					int amount, type, transactionPage = 0, gatherPage = 0;
					if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out type) ||
					    !int.TryParse(arg.Args[2], out amount) || !ulong.TryParse(arg.Args[3], out target)) return;

					var page = 0;
					if (type == 0)
					{
						if (!arg.HasArgs(6) || !int.TryParse(arg.Args[4], out transactionPage) ||
						    !int.TryParse(arg.Args[5], out gatherPage)) return;

						if (arg.HasArgs(7))
							int.TryParse(arg.Args[6], out page);
					}
					else
					{
						if (arg.HasArgs(5))
							int.TryParse(arg.Args[4], out page);
					}

					SelectPlayerUi(player, type, amount, target, transactionPage, gatherPage, page);
					break;
				}

				case "atmpage":
				{
					int page;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

					var amount = 0;
					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out amount);

					var targetId = 0UL;
					if (arg.HasArgs(4))
						ulong.TryParse(arg.Args[3], out targetId);

					var first = false;
					if (arg.HasArgs(5))
						bool.TryParse(arg.Args[4], out first);

					ATMUi(player, page, amount, targetId, first);
					break;
				}

				case "atm_input":
				{
					int page;
					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out page)) return;

					var targetId = 0UL;
					if (arg.HasArgs(3))
						ulong.TryParse(arg.Args[2], out targetId);

					var amount = 0;
					if (arg.HasArgs(4))
						int.TryParse(arg.Args[3], out amount);

					ATMUi(player, page, amount, targetId);
					break;
				}

				case "atm_setdepositfee":
				{
					float amount;
					if (!arg.HasArgs(2) ||
					    !float.TryParse(arg.Args[1], out amount)) return;

					if (amount < _config.Atm.MinDepositFee || amount > _config.Atm.MaxDepositFee)
						return;

					ATMData ATM;
					if (!_atmByPlayer.TryGetValue(player.userID, out ATM) || ATM == null ||
					    !ATM.IsOwner(player)) return;

					ATM.DepositFee = Convert.ToSingle(Math.Round(amount, 2));

					ATMUi(player, 1);
					break;
				}

				case "atm_setwithdrawalfee":
				{
					float amount;
					if (!arg.HasArgs(2) ||
					    !float.TryParse(arg.Args[1], out amount)) return;

					if (amount < _config.Atm.MinWithdrawalFee || amount > _config.Atm.MaxWithdrawalFee)
						return;

					ATMData ATM;
					if (!_atmByPlayer.TryGetValue(player.userID, out ATM) || ATM == null ||
					    !ATM.IsOwner(player)) return;

					ATM.WithdrawalFee = Convert.ToSingle(Math.Round(amount, 2));

					ATMUi(player, 1);
					break;
				}

				case "atm_tryrepair":
				{
					RepairUi(player);
					break;
				}

				case "atm_repair":
				{
					ATMData ATM;
					if (!_atmByPlayer.TryGetValue(player.userID, out ATM) ||
					    ATM == null ||
					    !ATM.IsOwner(player)) return;

					var needPercent = 100 - Mathf.RoundToInt(ATM.Condition);
					if (needPercent <= 0) return;

					var allItems = Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()));

					if (_config.Atm.Repair.Items.Any(x =>
						    !HasAmount(Enumerable.ToArray(allItems), x.ShortName, x.Skin, Mathf.CeilToInt(x.Amount * needPercent))))
					{
						SendNotify(player, NotEnoughItems, 1);
						return;
					}

					_config.Atm.Repair.Items.ForEach(item =>
						Take(allItems, item.ShortName, item.Skin, Mathf.CeilToInt(item.Amount * needPercent)));

					ATM.Condition = 100;

					ATMUi(player, 1, First: true);
					break;
				}

				case "atm_deposit":
				{
					int amount;
					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out amount)) return;

					if (amount < _config.Atm.MinDeposit)
					{
						SendNotify(player, ForbiddenDeposit, 1, _config.Atm.MinDeposit);
						return;
					}

					ATMData ATM;
					if (!_atmByPlayer.TryGetValue(player.userID, out ATM) || ATM == null) return;

					if (!ATM.CanOpen())
					{
						SendNotify(player, BrokenATM, 1);
						return;
					}

					var commission = 0f;

					var depositAmount = amount;
					if (_config.Atm.EnableDepositFee && ATM.DepositFee > 0.0)
					{
						commission = amount * ATM.DepositFee / 100f;

						amount = Mathf.RoundToInt(amount + commission);
					}

					var items = Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()));

					if (!HasAmount(Enumerable.ToArray(items), _config.Currency.ShortName, _config.Currency.Skin, amount))
					{
						SendNotify(player, NotEnoughMoney, 1);
						return;
					}

					if (commission > 0 && depositAmount != amount)
						ATM.Balance += commission;

					Take(items, _config.Currency.ShortName, _config.Currency.Skin, amount);

					_config.Economy.AddBalance(player, depositAmount);

					if (_config.Atm.EnableBreakage)
						ATM.LoseCondition();

					SendNotify(player, DepositedMoney, 0, depositAmount);

					AddTransactionLog(Transaction.Deposit, player.userID, 0, depositAmount);
					_atmByPlayer.Remove(player.userID);
					break;
				}

				case "atm_withdraw":
				{
					int amount;
					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out amount)) return;

					if (amount < _config.Atm.MinWithdrawal)
					{
						SendNotify(player, ForbiddenWithdraw, 1, _config.Atm.MinWithdrawal);
						return;
					}

					ATMData ATM;
					if (!_atmByPlayer.TryGetValue(player.userID, out ATM) || ATM == null) return;

					if (!ATM.CanOpen())
					{
						SendNotify(player, BrokenATM, 1);
						return;
					}

					var commission = 0f;

					var withdrawAmount = amount;
					if (_config.Atm.EnableWithdrawalFee && ATM.WithdrawalFee > 0)
					{
						commission = amount * ATM.WithdrawalFee / 100f;

						amount = Mathf.RoundToInt(amount + commission);
					}

					if (_config.Economy.ShowBalance(player) < amount)
					{
						SendNotify(player, NotEnoughMoney, 1);
						return;
					}

					if (commission > 0 && withdrawAmount != amount)
						ATM.Balance += commission;

					_config.Economy.RemoveBalance(player, amount);

					if (_config.Atm.EnableBreakage)
						ATM.LoseCondition();

					var item = _config.Currency.ToItem(withdrawAmount);
					if (item != null)
						player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);

					SendNotify(player, WithdrawnMoney, 0, withdrawAmount);

					AddTransactionLog(Transaction.Withdrawal, player.userID, 0, withdrawAmount);
					_atmByPlayer.Remove(player.userID);
					break;
				}

				case "atm_transfer":
				{
					int amount;
					ulong target;
					if (!arg.HasArgs(3) ||
					    !int.TryParse(arg.Args[1], out amount) ||
					    amount <= 0 ||
					    !ulong.TryParse(arg.Args[2], out target)) return;

					ATMData ATM;
					if (!_atmByPlayer.TryGetValue(player.userID, out ATM) || ATM == null) return;

					if (!ATM.CanOpen())
					{
						SendNotify(player, BrokenATM, 1);
						return;
					}

					if (_config.Economy.ShowBalance(player) < amount)
					{
						SendNotify(player, NotEnoughMoney, 1);
						return;
					}

					_config.Economy.Transfer(player.userID, target, amount);

					var targetPlayer = RelationshipManager.FindByID(target);
					if (targetPlayer != null)
					{
						SendNotify(player, TransferedMoney, 0, amount,
							targetPlayer.displayName);

						SendNotify(targetPlayer, TargetTransferedMoney, 0, player.displayName, amount);
					}
					else
					{
						SendNotify(player, TransferedMoney, 0, amount,
							PlayerData.GetNotLoad(target.ToString())?.DisplayName);
					}

					AddTransactionLog(Transaction.Transfer, player.userID, target, amount);
					_atmByPlayer.Remove(player.userID);
					break;
				}

				case "atm_admin_withdraw":
				{
					ATMData ATM;
					if (!_atmByPlayer.TryGetValue(player.userID, out ATM) || ATM == null || ATM.Balance <= 0.0) return;

					var amount = Mathf.CeilToInt(ATM.Balance);
					_config.Economy.AddBalance(player, amount);
					ATM.Balance = 0;

					SendNotify(player, AtmOwnWithdrawnMoney, 0);

					AddTransactionLog(Transaction.Deposit, player.userID, 0, amount);
					_atmByPlayer.Remove(player.userID);
					break;
				}
			}

#if TESTING
			}
			catch (Exception ex)
			{
				PrintError($"In the command 'UI_BankSystem' there was an error:\n{ex}");

				Debug.LogException(ex);
			}

			Puts($"Main command used with: {string.Join(", ", arg.Args)}");
#endif
		}

		private void CmdGiveNotes(IPlayer player, string command, string[] args)
		{
			if (!(player.IsAdmin || player.IsServer)) return;

			if (args.Length == 0)
			{
				player.Reply($"Error syntax! Use: /{command} [targetId] [amount]");
				return;
			}

			var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
			if (target == null)
			{
				PrintError($"Player '{args[0]}' not found!");
				return;
			}

			var amount = 1;
			if (args.Length > 1)
				int.TryParse(args[1], out amount);

			var item = _config.Currency?.ToItem(amount);
			if (item == null) return;

			target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			Puts($"Player {target.displayName} ({target.userID}) received {amount} banknotes");
		}

		private void CmdGiveATM(IPlayer player, string command, string[] args)
		{
			if (!(player.IsAdmin || player.IsServer)) return;

			if (args.Length == 0)
			{
				player.Reply($"Error syntax! Use: /{command} [targetId] [amount]");
				return;
			}

			var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
			if (target == null)
			{
				PrintError($"Player '{args[0]}' not found!");
				return;
			}

			var item = _config.Atm?.ToItem();
			if (item == null) return;

			target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			Puts($"Player {target.displayName} ({target.userID}) received ATM");
		}

		private void AdminCommands(IPlayer player, string command, string[] args)
		{
			if (!(player.IsAdmin || player.IsServer)) return;

			switch (command)
			{
				case "bank.setbalance":
				{
					if (args.Length < 2)
					{
						PrintError($"Error syntax! Use: /{command} [targetId] [amount]");
						return;
					}

					var target = BasePlayer.FindAwakeOrSleeping(args[0]);
					if (target == null)
					{
						PrintError($"Player '{args[0]}' not found!");
						return;
					}

					int amount;
					if (!int.TryParse(args[1], out amount)) return;

#if TESTING
					Debug.Log($"[bank.setbalance] called GetOrCreate with id={target.UserIDString}");
#endif
					var data = PlayerData.GetOrCreate(target.UserIDString);
					if (data == null) return;

					data.Balance = amount;
					break;
				}

				case "bank.deposit":
				{
					if (args.Length < 2)
					{
						PrintError($"Error syntax! Use: /{command} [targetId] [amount]");
						return;
					}

					var target = BasePlayer.FindAwakeOrSleeping(args[0]);
					if (target == null)
					{
						PrintError($"Player '{args[0]}' not found!");
						return;
					}

					int amount;
					if (!int.TryParse(args[1], out amount)) return;

					_config.Economy.AddBalance(target, amount);
					break;
				}

				case "bank.withdraw":
				{
					if (args.Length < 2)
					{
						PrintError($"Error syntax! Use: /{command} [targetId] [amount]");
						return;
					}

					var target = BasePlayer.FindAwakeOrSleeping(args[0]);
					if (target == null)
					{
						PrintError($"Player '{args[0]}' not found!");
						return;
					}

					int amount;
					if (!int.TryParse(args[1], out amount)) return;

					_config.Economy.RemoveBalance(target, amount);
					break;
				}

				case "bank.transfer":
				{
					if (args.Length < 3)
					{
						PrintError($"Error syntax! Use: /{command} [playerId] [targetId] [amount]");
						return;
					}

					var member = BasePlayer.FindAwakeOrSleeping(args[0]);
					if (member == null)
					{
						PrintError($"Player '{args[0]}' not found!");
						return;
					}

					var target = BasePlayer.FindAwakeOrSleeping(args[1]);
					if (target == null)
					{
						PrintError($"Target player '{args[1]}' not found!");
						return;
					}

					int amount;
					if (!int.TryParse(args[2], out amount)) return;

					_config.Economy.Transfer(member.userID, target.userID, amount);
					break;
				}
			}
		}

		private void WipeCommands(IPlayer player, string command, string[] args)
		{
			if (!(player.IsAdmin || player.IsServer)) return;

			WipeATMs();
		}

		private void CmdOpenATM(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (_enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			if (!string.IsNullOrEmpty(_config.Atm.PermissionToOpenATMviaCommand) &&
			    player.HasPermission(_config.Atm.PermissionToOpenATMviaCommand))
			{
				if (_config.Atm.NotifyPlayerWhenNoPermissionInCommandATM)
					SendNotify(player, NoPermissions, 1);

				return;
			}

			if (!HasCard(player))
			{
				SendNotify(player, NotBankCard, 1);
				return;
			}

			if (!_atmByPlayer.ContainsKey(player.userID))
				_atmByPlayer.TryAdd(player.userID, new ATMData
				{
					ID = ++_atms.LastATMID,
					EntityID = 0,
					OwnerId = 0,
					Balance = 0,
					Condition = 100,
					DepositFee = _config.Atm.Spawn.ConfAddons.DepositFee,
					WithdrawalFee = _config.Atm.Spawn.ConfAddons.WithdrawFee,
					IsAdmin = true
				});

			ATMUi(player, First: true);
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, int amount = 0, ulong targetId = 0, int transactionPage = 0,
			int gatherPage = 0, bool first = false)
		{
			var container = new CuiElementContainer();

			var hasCard = HasCard(player);

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
						Close = Layer,
						Command = "UI_BankSystem close"
					}
				}, Layer);

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = $"-{_config.UI.BankWidth / 2f} -{_config.UI.BankHeight / 2f}",
						OffsetMax = $"{_config.UI.BankWidth / 2f} {_config.UI.BankHeight / 2f}"
					},
					Image =
					{
						Color = _config.Colors.Color1.Get
					}
				}, Layer, Layer + ".Background");
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
					Color = "0 0 0 0"
				}
			}, Layer + ".Background", Layer + ".Main", Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = _config.Colors.Color2.Get}
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
					Color = _config.Colors.Color13.Get
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
					Color = _config.Colors.Color13.Get
				},
				Button =
				{
					Close = Layer,
					Color = _config.Colors.Color3.Get,
					Command = "UI_BankSystem close"
				}
			}, Layer + ".Header");

			#endregion

			#region Second Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin =
						$"-{_config.UI.BankHeaderWidth / 2f} -{_config.UI.BankHeaderUpIndent + _config.UI.BankHeaderHeight}",
					OffsetMax = $"{_config.UI.BankHeaderWidth / 2f} -{_config.UI.BankHeaderUpIndent}"
				},
				Image =
				{
					Color = _config.Colors.Color2.Get
				}
			}, Layer + ".Main", Layer + ".Second.Header");

			#region Logo

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 1",
					OffsetMin = "15 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, MainTitle),
					Align = TextAnchor.LowerLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 16,
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#region Welcome

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0.5",
					OffsetMin = "15 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, WelcomeTitle, player.displayName),
					Align = TextAnchor.UpperLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#region Avatar

			container.Add(new CuiElement
			{
				Parent = Layer + ".Second.Header",
				Components =
				{
					new CuiRawImageComponent
						{Png = ImageLibrary?.Call<string>("GetImage", $"avatar_{player.userID}")},
					new CuiRectTransformComponent
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-60 -50", OffsetMax = "-25 -15"
					}
				}
			});

			#endregion

			#region Balance Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "-75 0"
				},
				Text =
				{
					Text = Msg(player, YourBalance),
					Align = TextAnchor.LowerRight,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = _config.Colors.Color14.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#region Balance

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0.5",
					OffsetMin = "0 0", OffsetMax = "-75 0"
				},
				Text =
				{
					Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
					Align = TextAnchor.UpperRight,
					Font = "robotocondensed-bold.ttf",
					FontSize = 13,
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#endregion

			#region Menu

			if (hasCard)
			{
#if TESTING
				Debug.Log($"[MainUi] called GetOrCreate with id={player.UserIDString}");
#endif
				var data = PlayerData.GetOrCreate(player.UserIDString);
				var logs = PlayerLogs.GetOrCreate(player.UserIDString);

				var constXSwitch = -425f;
				var xSwitch = constXSwitch;
				int amountOnPage;
				float ySwitch;
				float Height;
				float Width;
				float Margin;
				int amountOnString;

				#region Card

				if (_config.UI.ShowCard)
				{
					#region Card Image

					container.Add(new CuiElement
					{
						Name = Layer + ".Card",
						Parent = Layer + ".Main",
						Components =
						{
							new CuiRawImageComponent {Png = ImageLibrary?.Call<string>("GetImage", _config.CardImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{_config.UI.CardLeftIndent} -300",
								OffsetMax = $"{_config.UI.CardLeftIndent + _config.UI.CardWidth} -150"
							}
						}
					});

					#endregion

					#region Logo

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "20 110", OffsetMax = "0 130"
						},
						Text =
						{
							Text = Msg(player, CardBankTitle),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 16,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Card");

					#endregion

					#region Number

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "20 40", OffsetMax = "0 65"
						},
						Text =
						{
							Text = $"{GetFormattedCardNumber(data.Card)}",
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color17.Get
						}
					}, Layer + ".Card");

					#endregion

					#region Name

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "20 20", OffsetMax = "0 40"
						},
						Text =
						{
							Text = $"{player.displayName.ToUpper()}",
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Card");

					#endregion

					#region Date

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "140 20", OffsetMax = "0 40"
						},
						Text =
						{
							Text = _config.UseCardExpiry
								? $"{CardDateFormating(player.userID)}"
								: Msg(player, CardExpiryDate),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = _config.Colors.Color17.Get
						}
					}, Layer + ".Card");

					#endregion
				}

				#endregion

				#region Transactions

				if (_config.UI.ShowTransfers)
				{
					#region Title

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.Transfers.AnchorMin,
							AnchorMax = _config.UI.Transfers.AnchorMax,
							OffsetMin = _config.UI.Transfers.OffsetMin,
							OffsetMax = _config.UI.Transfers.OffsetMax
						},
						Text =
						{
							Text = Msg(player, TransfersTitle),
							Align = _config.UI.Transfers.Align,
							Font = _config.UI.Transfers.Font,
							FontSize = _config.UI.Transfers.FontSize,
							Color = _config.UI.Transfers.Color.Get
						}
					}, Layer + ".Main");

					#endregion

					#region Recent Transfers

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.RecentTransfersTitle.AnchorMin,
							AnchorMax = _config.UI.RecentTransfersTitle.AnchorMax,
							OffsetMin = _config.UI.RecentTransfersTitle.OffsetMin,
							OffsetMax = _config.UI.RecentTransfersTitle.OffsetMax
						},
						Text =
						{
							Text = Msg(player, FrequentTransfers),
							Align = _config.UI.RecentTransfersTitle.Align,
							Font = _config.UI.RecentTransfersTitle.Font,
							FontSize = _config.UI.RecentTransfersTitle.FontSize,
							Color = _config.UI.RecentTransfersTitle.Color.Get
						}
					}, Layer + ".Main");

					var i = 1;

					ySwitch = -360f;
					Height = 60f;
					Width = 120f;
					amountOnString = 3;
					Margin = 10f;

					var topTransfers = GetTopTransfers(player);
					if (topTransfers == null || topTransfers.Count == 0)
					{
						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = _config.UI.NoTransactions.AnchorMin,
								AnchorMax = _config.UI.NoTransactions.AnchorMax,
								OffsetMin = _config.UI.NoTransactions.OffsetMin,
								OffsetMax = _config.UI.NoTransactions.OffsetMax
							},
							Image =
							{
								Color = _config.Colors.Color2.Get
							}
						}, Layer + ".Main", Layer + ".Transfer.Not");

						container.Add(new CuiLabel
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text =
							{
								Text = Msg(player, HaventTransactions),
								Align = _config.UI.NoTransactions.Align,
								Font = _config.UI.NoTransactions.Font,
								FontSize = _config.UI.NoTransactions.FontSize,
								Color = _config.UI.NoTransactions.Color.Get
							}
						}, Layer + ".Transfer.Not");
					}
					else
					{
						topTransfers.ForEach(member =>
						{
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
										Color = _config.Colors.Color2.Get
									}
								}, Layer + ".Main", Layer + $".Transfer.{member}");

							#region Avatar

							container.Add(new CuiElement
							{
								Parent = Layer + $".Transfer.{member}",
								Components =
								{
									new CuiRawImageComponent
										{Png = ImageLibrary?.Call<string>("GetImage", $"avatar_{member}")},
									new CuiRectTransformComponent
									{
										AnchorMin = "0 0", AnchorMax = "0 0",
										OffsetMin = "5 20", OffsetMax = "40 55"
									}
								}
							});

							#endregion

							#region Name

							container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 0",
										OffsetMin = "5 10", OffsetMax = "0 20"
									},
									Text =
									{
										Text = $"{PlayerData.GetNotLoad(member.ToString())?.DisplayName}",
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-bold.ttf",
										FontSize = 8,
										Color = _config.Colors.Color13.Get
									}
								}, Layer + $".Transfer.{member}");

							#endregion

							#region Card Number

							container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 0",
										OffsetMin = "55 10", OffsetMax = "0 20"
									},
									Text =
									{
										Text =
											$"{GetLastCardNumbers(player, PlayerData.GetNotLoad(member.ToString())?.Card)}",
										Align = TextAnchor.MiddleLeft,
										Font = "robotocondensed-bold.ttf",
										FontSize = 6,
										Color = _config.Colors.Color19.Get
									}
								}, Layer + $".Transfer.{member}");

							#endregion

							#region Button

							container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "1 1", AnchorMax = "1 1",
										OffsetMin = "-65 -40", OffsetMax = "-5 -10"
									},
									Text =
									{
										Text = Msg(player, TransferTitle),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 10,
										Color = _config.Colors.Color13.Get
									},
									Button =
									{
										Color = _config.Colors.Color3.Get,
										Command = $"UI_BankSystem ui_transfer {amount} {member}"
									}
								}, Layer + $".Transfer.{member}");

							#endregion

							if (i % amountOnString == 0)
							{
								ySwitch = ySwitch - Height - Margin;
								xSwitch = constXSwitch;
							}
							else
							{
								xSwitch += Width + Margin;
							}

							i++;
						});
					}

					#endregion

					#region Card Transfers

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.TransferCard.AnchorMin,
							AnchorMax = _config.UI.TransferCard.AnchorMax,
							OffsetMin = _config.UI.TransferCard.OffsetMin,
							OffsetMax = _config.UI.TransferCard.OffsetMax
						},
						Text =
						{
							Text = Msg(player, TransferByCard),
							Align = _config.UI.TransferCard.Align,
							Font = _config.UI.TransferCard.Font,
							FontSize = _config.UI.TransferCard.FontSize,
							Color = _config.UI.TransferCard.Color.Get
						}
					}, Layer + ".Main");

					#region Number

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = _config.UI.TransferCardNumber.AnchorMin,
							AnchorMax = _config.UI.TransferCardNumber.AnchorMax,
							OffsetMin = _config.UI.TransferCardNumber.OffsetMin,
							OffsetMax = _config.UI.TransferCardNumber.OffsetMax
						},
						Text =
						{
							Text = targetId != 0
								? GetLastCardNumbers(player, PlayerData.GetNotLoad(targetId.ToString())?.Card)
								: Msg(player, CardNumberTitle),
							Align = _config.UI.TransferCardNumber.Align,
							Font = _config.UI.TransferCardNumber.Font,
							FontSize = _config.UI.TransferCardNumber.FontSize,
							Color = _config.UI.TransferCardNumber.Color.Get
						},
						Button =
						{
							Color = _config.Colors.Color2.Get,
							Command = $"UI_BankSystem selectpage 0 {amount} {targetId} {transactionPage} {gatherPage}"
						}
					}, Layer + ".Main");

					#endregion

					#region Amount

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.TransferCardAmount.AnchorMin,
							AnchorMax = _config.UI.TransferCardAmount.AnchorMax,
							OffsetMin = _config.UI.TransferCardAmount.OffsetMin,
							OffsetMax = _config.UI.TransferCardAmount.OffsetMax
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Enter.Amount");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Enter.Amount",
						Components =
						{
							new CuiInputFieldComponent
							{
								Command = $"UI_BankSystem setamount {targetId} {transactionPage} {gatherPage} ",
								CharsLimit = 10,
								NeedsKeyboard = true,
								Text = $"{amount}",
								Align = _config.UI.TransferCardAmount.Align,
								Font = _config.UI.TransferCardAmount.Font,
								FontSize = _config.UI.TransferCardAmount.FontSize,
								Color = _config.UI.TransferCardAmount.Color.Get
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "10 0", OffsetMax = "-10 0"
							}
						}
					});

					if (amount > 0)
						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "1 0", AnchorMax = "1 1",
								OffsetMin = "-20 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, RemoveAmountTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = _config.Colors.Color13.Get
							},
							Button =
							{
								Color = "0 0 0 0",
								Command = $"UI_BankSystem setamount {targetId} {transactionPage} {gatherPage} 0"
							}
						}, Layer + ".Enter.Amount");

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = _config.UI.TransferCardButton.AnchorMin,
							AnchorMax = _config.UI.TransferCardButton.AnchorMax,
							OffsetMin = _config.UI.TransferCardButton.OffsetMin,
							OffsetMax = _config.UI.TransferCardButton.OffsetMax
						},
						Text =
						{
							Text = Msg(player, TransferTitle),
							Align = _config.UI.TransferCardButton.Align,
							Font = _config.UI.TransferCardButton.Font,
							FontSize = _config.UI.TransferCardButton.FontSize,
							Color = _config.UI.TransferCardButton.Color.Get
						},
						Button =
						{
							Color = _config.Colors.Color9.Get,
							Command = targetId != 0 ? $"UI_BankSystem ui_transfer {amount} {targetId}" : ""
						}
					}, Layer + ".Main");

					#endregion

					#endregion
				}

				#endregion

				#region Transaction history

				xSwitch = 0f;

				if (_config.UI.ShowTransactionsHistory && logs.Transfers.Count > 0)
				{
					xSwitch += 5f;

					#region Title

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} -175",
							OffsetMax = $"{xSwitch + _config.UI.THFieldWidth} -150"
						},
						Text =
						{
							Text = Msg(player, TransactionHistory),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 14,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Main");

					#endregion

					#region List

					amountOnPage = 9;
					Height = 35f;
					Margin = 7.5f;

					ySwitch = -175f;

					var transactions = logs.Transfers.ReverseSkipTake(transactionPage * amountOnPage, amountOnPage);
					for (var i = 0; i < transactions.Count; i++)
					{
						var transaction = transactions[i];

						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0.5 1", AnchorMax = "0.5 1",
									OffsetMin = $"{xSwitch} {ySwitch - Height}",
									OffsetMax = $"{xSwitch + _config.UI.THFieldWidth} {ySwitch}"
								},
								Image =
								{
									Color = _config.Colors.Color2.Get
								}
							}, Layer + ".Main", Layer + $".Transaction.{ySwitch}");

						transaction.Get(player, ref container, Layer + $".Transaction.{ySwitch}");

						ySwitch = ySwitch - Margin - Height;
					}

					#endregion

					#region Pages

					if (logs.Transfers.Count > amountOnPage)
					{
						var maxSwitch = xSwitch + _config.UI.THFieldWidth;

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = $"{maxSwitch - _config.UI.THButtonSize} -170",
								OffsetMax = $"{maxSwitch} -150"
							},
							Text =
							{
								Text = Msg(player, BtnNext),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = _config.Colors.Color18.Get
							},
							Button =
							{
								Color = _config.Colors.Color3.Get,
								Command = logs.Transfers.Count > (transactionPage + 1) * amountOnPage
									? $"UI_BankSystem page {amount} {targetId} {transactionPage + 1} {gatherPage}"
									: ""
							}
						}, Layer + ".Main");

						maxSwitch = maxSwitch - _config.UI.THButtonMargin - _config.UI.THButtonSize;

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = $"{maxSwitch - _config.UI.THButtonSize} -170",
								OffsetMax = $"{maxSwitch} -150"
							},
							Text =
							{
								Text = Msg(player, BtnBack),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 9,
								Color = _config.Colors.Color18.Get
							},
							Button =
							{
								Color = _config.Colors.Color10.Get,
								Command = transactionPage != 0
									? $"UI_BankSystem page {amount} {targetId} {transactionPage - 1} {gatherPage}"
									: ""
							}
						}, Layer + ".Main");
					}

					#endregion

					xSwitch += _config.UI.THFieldWidth;
				}

				#endregion

				#region Gather History

				if (_config.UI.ShowGatherHistory && logs.GatherLogs.Count > 0)
				{
					xSwitch += 35f;

					#region Title

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} -175",
							OffsetMax = $"{xSwitch + _config.UI.GHFieldWidth} -150"
						},
						Text =
						{
							Text = Msg(player, GatherHistory),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 14,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Main");

					#endregion

					#region List

					amountOnPage = 10;
					Height = 35f;
					Margin = 2.5f;

					ySwitch = -175f;

					var gatherLogs = logs.GatherLogs.ReverseSkipTake(gatherPage * amountOnPage, amountOnPage);
					for (var i = 0; i < gatherLogs.Count; i++)
					{
						var gather = gatherLogs[i];

						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0.5 1", AnchorMax = "0.5 1",
									OffsetMin = $"{xSwitch} {ySwitch - Height}",
									OffsetMax = $"{xSwitch + _config.UI.GHFieldWidth} {ySwitch}"
								},
								Image =
								{
									Color = _config.Colors.Color2.Get
								}
							}, Layer + ".Main", Layer + $".Gather.{ySwitch}");

						container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "7.5 0", OffsetMax = "0 0"
								},
								Text =
								{
									Text = gather.Type == GatherLogType.Gather
										? Msg(player, MiningFee, GetItemTitle(player, gather.ShortName))
										: gather.Type == GatherLogType.Collect
											? Msg(player, CollectFee, GetItemTitle(player, gather.ShortName))
											: Msg(player, LootFee, Msg(player, gather.ShortName)),
									Align = TextAnchor.MiddleLeft,
									Font = "robotocondensed-regular.ttf",
									FontSize = 10,
									Color = _config.Colors.Color13.Get
								}
							}, Layer + $".Gather.{ySwitch}");

						container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "0 0", OffsetMax = "-10 0"
								},
								Text =
								{
									Text = Msg(player, MiningValue, gather.Amount),
									Align = TextAnchor.MiddleRight,
									Font = "robotocondensed-regular.ttf",
									FontSize = 10,
									Color = _config.Colors.Color3.Get
								}
							}, Layer + $".Gather.{ySwitch}");

						ySwitch = ySwitch - Margin - Height;
					}

					#endregion

					#region Pages

					if (logs.GatherLogs.Count > amountOnPage)
					{
						var maxSwitch = xSwitch + _config.UI.GHFieldWidth;

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = $"{maxSwitch - _config.UI.GHButtonSize} -170",
								OffsetMax = $"{maxSwitch} -150"
							},
							Text =
							{
								Text = Msg(player, BtnNext),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = _config.Colors.Color18.Get
							},
							Button =
							{
								Color = _config.Colors.Color3.Get,
								Command = logs.GatherLogs.Count > (gatherPage + 1) * amountOnPage
									? $"UI_BankSystem page {amount} {targetId} {transactionPage} {gatherPage + 1}"
									: ""
							}
						}, Layer + ".Main");

						maxSwitch = maxSwitch - _config.UI.GHButtonMargin - _config.UI.GHButtonSize;

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = $"{maxSwitch - _config.UI.GHButtonSize} -170",
								OffsetMax = $"{maxSwitch} -150"
							},
							Text =
							{
								Text = Msg(player, BtnBack),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 9,
								Color = _config.Colors.Color18.Get
							},
							Button =
							{
								Color = _config.Colors.Color10.Get,
								Command = gatherPage != 0
									? $"UI_BankSystem page {amount} {targetId} {transactionPage} {gatherPage - 1}"
									: ""
							}
						}, Layer + ".Main");
					}

					#endregion
				}

				#endregion
			}
			else
			{
				#region Create Card

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-425 -300",
						OffsetMax = "-275 -150"
					},
					Image =
					{
						Color = _config.Colors.Color2.Get
					}
				}, Layer + ".Main", Layer + ".Crate.Card");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-2.5 -22.5", OffsetMax = "2.5 37.5"
					},
					Image =
					{
						Color = _config.Colors.Color15.Get
					}
				}, Layer + ".Crate.Card");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-30 5", OffsetMax = "-2.5 10"
					},
					Image =
					{
						Color = _config.Colors.Color15.Get
					}
				}, Layer + ".Crate.Card");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "2.5 5", OffsetMax = "30 10"
					},
					Image =
					{
						Color = _config.Colors.Color15.Get
					}
				}, Layer + ".Crate.Card");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0.5",
						OffsetMin = "0 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, CreateCardTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = _config.Colors.Color15.Get
					}
				}, Layer + ".Crate.Card");

				CreateOutLine(ref container, Layer + ".Crate.Card", _config.Colors.Color11.Get, 1);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = "UI_BankSystem cardcreate"
					}
				}, Layer + ".Crate.Card");

				#endregion
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private const int SELECT_PLAYER_AMOUNT_ON_STRING = 4;
		private const int SELECT_PLAYER_STRINGS = 5;
		private const int SELECT_PLAYER_TOTAL_AMOUNT = SELECT_PLAYER_AMOUNT_ON_STRING * SELECT_PLAYER_STRINGS;
		private const float SELECT_PLAYER_ITEM_WIDTH = 180f;
		private const float SELECT_PLAYER_ITEM_HEIGHT = 50f;
		private const float SELECT_PLAYER_ITEM_X_MARGIN = 20f;
		private const float SELECT_PLAYER_ITEM_Y_MARGIN = 30f;

		private const float SELECT_PLAYER_DEFAULT_X_INDENT =
			-(SELECT_PLAYER_AMOUNT_ON_STRING * SELECT_PLAYER_ITEM_WIDTH +
			  (SELECT_PLAYER_AMOUNT_ON_STRING - 1) * SELECT_PLAYER_ITEM_X_MARGIN) / 2f;

		private const float SELECT_PLAYER_PAGE_X_MARGIN = 5f;
		private const float SELECT_PLAYER_PAGE_SIZE = 25f;
		private const float SELECT_PLAYER_PAGE_SELECTED_SIZE = 40f;

		private void SelectPlayerUi(BasePlayer player, int type, int amount, ulong target, int transactionPage,
			int gatherPage, int page = 0)
		{
			#region Fields

			var xSwitch = SELECT_PLAYER_DEFAULT_X_INDENT;
			var ySwitch = -180f;

			var totalPlayers = BasePlayer.activePlayerList.Where(x => player != x && HasCard(x));

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
			}, "Overlay", Layer, Layer);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Close = Layer,
					Command = $"UI_BankSystem select {type} {amount} {target} {transactionPage} {gatherPage}"
				}
			}, Layer);

			#endregion

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
					Color = _config.Colors.Color13.Get
				}
			}, Layer);

			#endregion

			#region Players

			var members = totalPlayers.SkipAndTake(page * SELECT_PLAYER_TOTAL_AMOUNT, SELECT_PLAYER_TOTAL_AMOUNT);

			for (var i = 0; i < members.Count; i++)
			{
				var member = members[i];

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - SELECT_PLAYER_ITEM_HEIGHT}",
							OffsetMax = $"{xSwitch + SELECT_PLAYER_ITEM_WIDTH} {ySwitch}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, Layer, Layer + $".Player.{i}");

				container.Add(new CuiElement
				{
					Parent = Layer + $".Player.{i}",
					Components =
					{
						new CuiRawImageComponent
							{Png = ImageLibrary?.Call<string>("GetImage", $"avatar_{member.userID}")},
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
							Color = _config.Colors.Color13.Get
						}
					}, Layer + $".Player.{i}");

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "55 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = $"{GetLastCardNumbers(player, PlayerData.GetNotLoad(member.UserIDString)?.Card)}",
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 14,
							Color = _config.Colors.Color13.Get
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
								$"UI_BankSystem select {type} {amount} {member.userID} {transactionPage} {gatherPage}"
						}
					}, Layer + $".Player.{i}");

				if ((i + 1) % SELECT_PLAYER_AMOUNT_ON_STRING == 0)
				{
					xSwitch = SELECT_PLAYER_DEFAULT_X_INDENT;
					ySwitch = ySwitch - SELECT_PLAYER_ITEM_HEIGHT - SELECT_PLAYER_ITEM_Y_MARGIN;
				}
				else
				{
					xSwitch += SELECT_PLAYER_ITEM_WIDTH + SELECT_PLAYER_ITEM_X_MARGIN;
				}
			}

			#endregion

			#region Pages

			var pages = (int) Math.Ceiling((double) totalPlayers.Count / SELECT_PLAYER_TOTAL_AMOUNT);
			if (pages > 1)
			{
				xSwitch = -((pages - 1) * SELECT_PLAYER_PAGE_SIZE + (pages - 1) * SELECT_PLAYER_PAGE_X_MARGIN +
				            SELECT_PLAYER_PAGE_SELECTED_SIZE) / 2f;

				for (var j = 0; j < pages; j++)
				{
					var selected = page == j;

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{xSwitch} 60",
							OffsetMax =
								$"{xSwitch + (selected ? SELECT_PLAYER_PAGE_SELECTED_SIZE : SELECT_PLAYER_PAGE_SIZE)} {60 + (selected ? SELECT_PLAYER_PAGE_SELECTED_SIZE : SELECT_PLAYER_PAGE_SIZE)}"
						},
						Text =
						{
							Text = $"{j + 1}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = selected ? 18 : 12,
							Color = _config.Colors.Color13.Get
						},
						Button =
						{
							Color = _config.Colors.Color3.Get,
							Command =
								$"UI_BankSystem selectpage {type} {amount} {target} {transactionPage} {gatherPage} {j}"
						}
					}, Layer);

					xSwitch += (selected ? SELECT_PLAYER_PAGE_SELECTED_SIZE : SELECT_PLAYER_PAGE_SIZE) +
					           SELECT_PLAYER_PAGE_X_MARGIN;
				}
			}

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ATMUi(BasePlayer player, int page = 0, int amount = 0, ulong targetId = 0, bool First = false)
		{
			ATMData atmData;
			if (!_atmByPlayer.TryGetValue(player.userID, out atmData) || atmData == null)
				return;

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
						Close = Layer,
						Command = "UI_BankSystem close"
					}
				}, Layer);

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-425 -225",
						OffsetMax = "425 225"
					},
					Image =
					{
						Color = _config.Colors.Color1.Get
					}
				}, Layer, Layer + ".Background");
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
					Color = "0 0 0 0"
				}
			}, Layer + ".Background", Layer + ".Main", Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = _config.Colors.Color2.Get}
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
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Header");

			if (!_config.AtmDisableCloser)
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
						Color = _config.Colors.Color13.Get
					},
					Button =
					{
						Close = Layer,
						Color = _config.Colors.Color3.Get,
						Command = "UI_BankSystem close"
					}
				}, Layer + ".Header");

			#endregion

			#region Second Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-400 -135",
					OffsetMax = "400 -65"
				},
				Image =
				{
					Color = _config.Colors.Color2.Get
				}
			}, Layer + ".Main", Layer + ".Second.Header");

			#region Logo

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 1",
					OffsetMin = "15 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, MainTitle),
					Align = TextAnchor.LowerLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 16,
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#region Welcome

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0.5",
					OffsetMin = "15 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, WelcomeTitle, player.displayName),
					Align = TextAnchor.UpperLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#region Avatar

			container.Add(new CuiElement
			{
				Parent = Layer + ".Second.Header",
				Components =
				{
					new CuiRawImageComponent
						{Png = ImageLibrary?.Call<string>("GetImage", $"avatar_{player.userID}")},
					new CuiRectTransformComponent
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-60 -50", OffsetMax = "-25 -15"
					}
				}
			});

			#endregion

			#region Balance Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "-75 0"
				},
				Text =
				{
					Text = Msg(player, YourBalance),
					Align = TextAnchor.LowerRight,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = _config.Colors.Color14.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#region Balance

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0.5",
					OffsetMin = "0 0", OffsetMax = "-75 0"
				},
				Text =
				{
					Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
					Align = TextAnchor.UpperRight,
					Font = "robotocondensed-bold.ttf",
					FontSize = 13,
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Second.Header");

			#endregion

			#endregion

			switch (page)
			{
				case 0:
				{
					#region Deposit

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-400 -255",
							OffsetMax = "-110 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Deposit");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Deposit",
						Components =
						{
							new CuiRawImageComponent
								{Png = ImageLibrary?.Call<string>("GetImage", _config.DepositImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "25 25", OffsetMax = "75 75"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, DepositTitle),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Deposit");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, DepositDescription),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Deposit");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command = "UI_BankSystem atmpage 2"
						}
					}, Layer + ".Deposit");

					#endregion

					#region Withdraw

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -255",
							OffsetMax = "200 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Withdraw");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Withdraw",
						Components =
						{
							new CuiRawImageComponent
								{Png = ImageLibrary?.Call<string>("GetImage", _config.WithdrawImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "25 25", OffsetMax = "75 75"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, WithdrawTitle),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Withdraw");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, WithdrawDescription),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Withdraw");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command = "UI_BankSystem atmpage 3"
						}
					}, Layer + ".Withdraw");

					#endregion

					#region Transfer

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-400 -380",
							OffsetMax = "-110 -280"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Transfer");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Transfer",
						Components =
						{
							new CuiRawImageComponent
								{Png = ImageLibrary?.Call<string>("GetImage", _config.TransferImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "25 25", OffsetMax = "75 75"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TransferTitle),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Transfer");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TransferDescription),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Transfer");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command = "UI_BankSystem atmpage 4"
						}
					}, Layer + ".Transfer");

					#endregion

					#region Exit

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -380",
							OffsetMax = "200 -280"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Exit");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Exit",
						Components =
						{
							new CuiRawImageComponent
								{Png = ImageLibrary?.Call<string>("GetImage", _config.ExitImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "25 25", OffsetMax = "75 75"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitTitle),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitDescription),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Close = Layer,
							Command = "UI_BankSystem close"
						}
					}, Layer + ".Exit");

					#endregion

					InfoUi(player, ref container, atmData, page);
					break;
				}

				case 1:
				{
					float fullFee;
					float progress;
					int steps;
					float Width;
					float stepSize;
					float xSwitch;

					#region Profit

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-400 -255",
							OffsetMax = "-110 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Profit");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ATMProfit),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Profit");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ProfitValue, atmData.Balance),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 16,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Profit");

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-155 -15", OffsetMax = "-25 20"
						},
						Text =
						{
							Text = Msg(player, WithdrawTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 14,
							Color = _config.Colors.Color13.Get
						},
						Button =
						{
							Color = _config.Colors.Color3.Get,
							Command = atmData.Balance > 0.0 ? "UI_BankSystem atm_admin_withdraw" : "",
							Close = atmData.Balance > 0.0 ? Layer : ""
						}
					}, Layer + ".Profit");

					#endregion

					#region Condition

					if (_config.Atm.EnableBreakage)
					{
						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-90 -255",
								OffsetMax = "200 -155"
							},
							Image =
							{
								Color = _config.Colors.Color2.Get
							}
						}, Layer + ".Main", Layer + ".Condition");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0.5", AnchorMax = "1 1",
								OffsetMin = "25 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, ATMCondition),
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".Condition");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0.5",
								OffsetMin = "25 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, ConditionValue, atmData.Condition),
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = 16,
								Color = _config.Colors.Color13.Get
							}
						}, Layer + ".Condition");

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "1 0.5", AnchorMax = "1 0.5",
								OffsetMin = "-155 -15", OffsetMax = "-25 20"
							},
							Text =
							{
								Text = Msg(player, ATMRepair),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 14,
								Color = _config.Colors.Color13.Get
							},
							Button =
							{
								Color = _config.Colors.Color3.Get,
								Command = atmData.Condition < 100 ? "UI_BankSystem atm_tryrepair" : ""
							}
						}, Layer + ".Condition");
					}

					#endregion

					#region Deposit Fee

					if (_config.Atm.EnableDepositFee)
					{
						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-400 -380",
								OffsetMax = "-110 -280"
							},
							Image =
							{
								Color = _config.Colors.Color2.Get
							}
						}, Layer + ".Main", Layer + ".DepositFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = "25 -30",
								OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, DepositFeeTitle),
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".DepositFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = "25 -50", OffsetMax = "0 -30"
							},
							Text =
							{
								Text = Msg(player, FeeValue, atmData.DepositFee),
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = 14,
								Color = _config.Colors.Color13.Get
							}
						}, Layer + ".DepositFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "25 20", OffsetMax = "0 35"
							},
							Text =
							{
								Text = Msg(player, FeeValue, _config.Atm.MinDepositFee),
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".DepositFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "0 20", OffsetMax = "-15 35"
							},
							Text =
							{
								Text = Msg(player, FeeValue, _config.Atm.MaxDepositFee),
								Align = TextAnchor.UpperRight,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".DepositFee");

						#region Buttons

						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "25 35", OffsetMax = "-15 40"
							},
							Image =
							{
								Color = _config.Colors.Color12.Get
							}
						}, Layer + ".DepositFee", Layer + ".DepositFee.Progress");

						fullFee = _config.Atm.MaxDepositFee - _config.Atm.MinDepositFee;
						progress = atmData.DepositFee / fullFee;
						if (progress > 0)
						{
							container.Add(new CuiPanel
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progress} 0.95"},
								Image =
								{
									Color = _config.Colors.Color3.Get
								}
							}, Layer + ".DepositFee.Progress", Layer + ".DepositFee.Progress.Finish");

							container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "1 0.5", AnchorMax = "1 0.5",
									OffsetMin = "-5 -5", OffsetMax = "5 5"
								},
								Image =
								{
									Color = _config.Colors.Color13.Get
								}
							}, Layer + ".DepositFee.Progress.Finish");
						}

						steps = Mathf.CeilToInt(fullFee / _config.Atm.StepDepositFee);
						Width = 250f;
						stepSize = Width / steps;

						xSwitch = 0f;
						for (var z = 0; z < steps; z++)
						{
							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "0 1",
									OffsetMin = $"{xSwitch} 0",
									OffsetMax = $"{xSwitch + stepSize} 0"
								},
								Text =
								{
									Text = ""
								},
								Button =
								{
									Color = "0 0 0 0",
									Command =
										$"UI_BankSystem atm_setdepositfee {_config.Atm.StepDepositFee * (z + 1)}"
								}
							}, Layer + ".DepositFee.Progress");

							xSwitch += stepSize;
						}

						#endregion
					}

					#endregion

					#region Withdrawal Fee

					if (_config.Atm.EnableWithdrawalFee)
					{
						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-90 -380",
								OffsetMax = "200 -280"
							},
							Image =
							{
								Color = _config.Colors.Color2.Get
							}
						}, Layer + ".Main", Layer + ".WithdrawalFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = "25 -30",
								OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, WithdrawalFeeTitle),
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".WithdrawalFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = "25 -50", OffsetMax = "0 -30"
							},
							Text =
							{
								Text = Msg(player, FeeValue, atmData.WithdrawalFee),
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = 14,
								Color = _config.Colors.Color13.Get
							}
						}, Layer + ".WithdrawalFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "25 20", OffsetMax = "0 35"
							},
							Text =
							{
								Text = Msg(player, FeeValue, _config.Atm.MinWithdrawalFee),
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".WithdrawalFee");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "0 20", OffsetMax = "-15 35"
							},
							Text =
							{
								Text = Msg(player, FeeValue, _config.Atm.MaxWithdrawalFee),
								Align = TextAnchor.UpperRight,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".WithdrawalFee");

						#region Buttons

						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "25 35", OffsetMax = "-15 40"
							},
							Image =
							{
								Color = _config.Colors.Color12.Get
							}
						}, Layer + ".WithdrawalFee", Layer + ".WithdrawalFee.Progress");

						fullFee = _config.Atm.MaxWithdrawalFee - _config.Atm.MinWithdrawalFee;
						progress = atmData.WithdrawalFee / fullFee;
						if (progress > 0)
						{
							container.Add(new CuiPanel
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progress} 0.95"},
								Image =
								{
									Color = _config.Colors.Color3.Get
								}
							}, Layer + ".WithdrawalFee.Progress", Layer + ".WithdrawalFee.Progress.Finish");

							container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "1 0.5", AnchorMax = "1 0.5",
									OffsetMin = "-5 -5", OffsetMax = "5 5"
								},
								Image =
								{
									Color = _config.Colors.Color13.Get
								}
							}, Layer + ".WithdrawalFee.Progress.Finish");
						}

						steps = Mathf.CeilToInt(fullFee / _config.Atm.StepWithdrawalFee);

						Width = 250f;

						stepSize = Width / steps;

						xSwitch = 0f;
						for (var z = 0; z < steps; z++)
						{
							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "0 1",
									OffsetMin = $"{xSwitch} 0",
									OffsetMax = $"{xSwitch + stepSize} 0"
								},
								Text =
								{
									Text = ""
								},
								Button =
								{
									Color = "0 0 0 0",
									Command =
										$"UI_BankSystem atm_setwithdrawalfee {_config.Atm.StepWithdrawalFee * (z + 1)}"
								}
							}, Layer + ".WithdrawalFee.Progress");

							xSwitch += stepSize;
						}

						#endregion
					}

					#endregion

					InfoUi(player, ref container, atmData, page);
					break;
				}

				case 2: //Deposit
				{
					#region Input

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-400 -255",
							OffsetMax = "-110 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Input");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, EnterAmount),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Input");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Input",
						Components =
						{
							new CuiInputFieldComponent
							{
								FontSize = 16,
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-bold.ttf",
								Command = $"UI_BankSystem atm_input {page} {targetId} ",
								Color = _config.Colors.Color13.Get,
								CharsLimit = 9,
								NeedsKeyboard = true
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 0.5",
								OffsetMin = "25 0", OffsetMax = "0 0"
							}
						}
					});

					#endregion

					#region Button

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -255",
							OffsetMax = "200 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Button");


					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TotalDeposit),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Button");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TotalValue,
								atmData.DepositFee > 0
									? Mathf.RoundToInt(amount + amount * atmData.DepositFee / 100f)
									: amount),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 16,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Button");

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-110 -15", OffsetMax = "-20 20"
						},
						Text =
						{
							Text = Msg(player, DepositTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = _config.Colors.Color13.Get
						},
						Button =
						{
							Color = _config.Colors.Color3.Get,
							Command = amount > 0 ? $"UI_BankSystem atm_deposit {amount}" : "",
							Close = Layer
						}
					}, Layer + ".Button");

					#endregion

					#region Exit

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -380",
							OffsetMax = "200 -280"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Exit");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Exit",
						Components =
						{
							new CuiRawImageComponent
								{Png = ImageLibrary?.Call<string>("GetImage", _config.ExitImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "25 25", OffsetMax = "75 75"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitTitle),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitDescription),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Close = Layer,
							Command = "UI_BankSystem close"
						}
					}, Layer + ".Exit");

					#endregion

					InfoUi(player, ref container, atmData, page);
					break;
				}

				case 3: //Withdrawal
				{
					#region Input

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-400 -255",
							OffsetMax = "-110 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Input");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, EnterAmount),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Input");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Input",
						Components =
						{
							new CuiInputFieldComponent
							{
								FontSize = 16,
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-bold.ttf",
								Command = $"UI_BankSystem atm_input {page} {targetId} ",
								Color = _config.Colors.Color13.Get,
								CharsLimit = 9,
								NeedsKeyboard = true
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 0.5",
								OffsetMin = "25 0", OffsetMax = "0 0"
							}
						}
					});

					#endregion

					#region Button

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -255",
							OffsetMax = "200 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Button");


					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TotalWithdraw),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Button");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TotalValue,
								atmData.WithdrawalFee > 0
									? Mathf.RoundToInt(amount + amount * atmData.WithdrawalFee / 100f)
									: amount),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 16,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Button");

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-110 -15", OffsetMax = "-20 20"
						},
						Text =
						{
							Text = Msg(player, WithdrawTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = _config.Colors.Color13.Get
						},
						Button =
						{
							Color = _config.Colors.Color3.Get,
							Command = amount > 0 ? $"UI_BankSystem atm_withdraw {amount}" : "",
							Close = Layer
						}
					}, Layer + ".Button");

					#endregion

					#region Exit

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -380",
							OffsetMax = "200 -280"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Exit");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Exit",
						Components =
						{
							new CuiRawImageComponent
								{Png = ImageLibrary?.Call<string>("GetImage", _config.ExitImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "25 25", OffsetMax = "75 75"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitTitle),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitDescription),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Close = Layer,
							Command = "UI_BankSystem close"
						}
					}, Layer + ".Exit");

					#endregion

					InfoUi(player, ref container, atmData, page);
					break;
				}

				case 4: //Transfer
				{
					#region Input

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-400 -255",
							OffsetMax = "-110 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Input");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, EnterAmount),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Input");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Input",
						Components =
						{
							new CuiInputFieldComponent
							{
								FontSize = 16,
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-bold.ttf",
								Command = $"UI_BankSystem atm_input {page} {targetId} ",
								Color = _config.Colors.Color13.Get,
								CharsLimit = 9,
								NeedsKeyboard = true
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 0.5",
								OffsetMin = "25 0", OffsetMax = "0 0"
							}
						}
					});

					#endregion

					#region Button

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -255",
							OffsetMax = "200 -155"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Button");


					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TotalTransfer),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Button");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "25 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, TotalValue, amount),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 16,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Button");

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-110 -15", OffsetMax = "-20 20"
						},
						Text =
						{
							Text = Msg(player, TransferTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = _config.Colors.Color13.Get
						},
						Button =
						{
							Color = _config.Colors.Color3.Get,
							Command = targetId.IsSteamId()
								? $"UI_BankSystem atm_transfer {amount} {targetId}"
								: string.Empty,
							Close = targetId.IsSteamId() ? Layer : string.Empty
						}
					}, Layer + ".Button");

					#endregion

					#region Select Players

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-400 -380",
							OffsetMax = "-110 -280"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Select");

					if (targetId != 0)
					{
						container.Add(new CuiElement
						{
							Parent = Layer + ".Select",
							Components =
							{
								new CuiRawImageComponent
									{Png = ImageLibrary?.Call<string>("GetImage", $"avatar_{targetId}")},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "0 0",
									OffsetMin = "25 25", OffsetMax = "75 75"
								}
							}
						});

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0.5", AnchorMax = "1 1",
								OffsetMin = "85 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = $"{PlayerData.GetNotLoad(targetId.ToString())?.DisplayName}",
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = 18,
								Color = _config.Colors.Color13.Get
							}
						}, Layer + ".Select");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0.5",
								OffsetMin = "85 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text =
									$"{GetLastCardNumbers(player, PlayerData.GetNotLoad(targetId.ToString())?.Card)}",
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = _config.Colors.Color14.Get
							}
						}, Layer + ".Select");
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
								Text = Msg(player, SelectPlayerSecond),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 18,
								Color = _config.Colors.Color17.Get
							}
						}, Layer + ".Select");
					}

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command = $"UI_BankSystem selectpage 1 {amount} {targetId}"
						}
					}, Layer + ".Select");

					#endregion

					#region Exit

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-90 -380",
							OffsetMax = "200 -280"
						},
						Image =
						{
							Color = _config.Colors.Color2.Get
						}
					}, Layer + ".Main", Layer + ".Exit");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Exit",
						Components =
						{
							new CuiRawImageComponent
								{Png = ImageLibrary?.Call<string>("GetImage", _config.ExitImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "25 25", OffsetMax = "75 75"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "1 1",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitTitle),
							Align = TextAnchor.LowerLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.Colors.Color13.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0.5",
							OffsetMin = "85 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, ExitDescription),
							Align = TextAnchor.UpperLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + ".Exit");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Close = Layer,
							Command = "UI_BankSystem close"
						}
					}, Layer + ".Exit");

					#endregion

					InfoUi(player, ref container, atmData, page);
					break;
				}
			}

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void InfoUi(BasePlayer player, ref CuiElementContainer container, ATMData atmData, int page)
		{
			#region Info

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "220 -380",
					OffsetMax = "400 -155"
				},
				Image =
				{
					Color = _config.Colors.Color2.Get
				}
			}, Layer + ".Main", Layer + ".Info", Layer + ".Info");

			#region Title

			var ySwitch = 0;

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {ySwitch - 35}", OffsetMax = $"0 {ySwitch}"
				},
				Text =
				{
					Text = atmData.IsAdmin ? Msg(player, ATMAdminTitle) : Msg(player, ATMTitle, atmData.ID),
					Align = TextAnchor.LowerLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.Colors.Color13.Get
				}
			}, Layer + ".Info");

			#endregion

			#region Owner

			if (!atmData.IsAdmin)
			{
				ySwitch -= 50;

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {ySwitch - 15}", OffsetMax = $"0 {ySwitch}"
					},
					Text =
					{
						Text = Msg(player, ATMOwner),
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.Colors.Color11.Get
					}
				}, Layer + ".Info");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {ySwitch - 30}", OffsetMax = $"0 {ySwitch - 15}"
					},
					Text =
					{
						Text = $"{PlayerData.GetNotLoad(atmData.OwnerId.ToString())?.DisplayName}",
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = _config.Colors.Color13.Get
					}
				}, Layer + ".Info");
			}

			#endregion

			ySwitch -= 45;

			#region Deposit Percent

			if (_config.Atm.EnableDepositFee)
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {ySwitch - 15}", OffsetMax = $"0 {ySwitch}"
					},
					Text =
					{
						Text = Msg(player, InfoDepositTitle),
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.Colors.Color11.Get
					}
				}, Layer + ".Info");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {ySwitch - 35}", OffsetMax = $"0 {ySwitch - 15}"
					},
					Text =
					{
						Text = Msg(player, InfoValueTitle, atmData.DepositFee),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = _config.Colors.Color13.Get
					}
				}, Layer + ".Info");
			}

			#endregion

			#region Withdrawal Percent

			if (_config.Atm.EnableWithdrawalFee)
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"110 {ySwitch - 15}", OffsetMax = $"0 {ySwitch}"
					},
					Text =
					{
						Text = Msg(player, InfoWithdrawalTitle),
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.Colors.Color11.Get
					}
				}, Layer + ".Info");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"110 {ySwitch - 35}", OffsetMax = $"0 {ySwitch - 15}"
					},
					Text =
					{
						Text = Msg(player, InfoValueTitle, atmData.WithdrawalFee),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = _config.Colors.Color13.Get
					}
				}, Layer + ".Info");
			}

			#endregion

			#region Mange

			if (atmData.IsOwner(player))
			{
				if (page == 0)
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-55 25", OffsetMax = "55 60"
						},
						Text =
						{
							Text = Msg(player, InfoManageBtn),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = _config.Colors.Color13.Get
						},
						Button =
						{
							Color = _config.Colors.Color3.Get,
							Command = "UI_BankSystem atmpage 1"
						}
					}, Layer + ".Info");
				else
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-55 25", OffsetMax = "55 60"
						},
						Text =
						{
							Text = Msg(player, InfoBackBtn),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = _config.Colors.Color13.Get
						},
						Button =
						{
							Color = _config.Colors.Color3.Get,
							Command = "UI_BankSystem atmpage 0"
						}
					}, Layer + ".Info");
			}

			#endregion

			#endregion
		}

		private void RepairUi(BasePlayer player)
		{
			ATMData atmData;
			if (!_atmByPlayer.TryGetValue(player.userID, out atmData) || atmData == null)
				return;

			var needPercent = 100 - Mathf.RoundToInt(atmData.Condition);
			var allItems = Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()));

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
			}, "Overlay", Layer, Layer);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Close = Layer,
					Command = "UI_BankSystem atmpage 0 0 0 true"
				}
			}, Layer);

			#endregion

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-200 -210", OffsetMax = "200 -185"
				},
				Text =
				{
					Text = Msg(player, RepairTitle),
					Align = TextAnchor.LowerCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 24,
					Color = _config.Colors.Color15.Get
				}
			}, Layer);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-200 -245", OffsetMax = "200 -210"
				},
				Text =
				{
					Text = Msg(player, RepairSecondTitle),
					Align = TextAnchor.UpperCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 24,
					Color = _config.Colors.Color13.Get
				}
			}, Layer);

			#endregion

			#region Items

			var Width = 130f;
			var Margin = 35f;

			var notItem = false;

			var xSwitch = -(_config.Atm.Repair.Items.Count * Width + (_config.Atm.Repair.Items.Count - 1) * Margin) /
			              2f;
			_config.Atm.Repair.Items.ForEach(item =>
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} -450",
							OffsetMax = $"{xSwitch + Width} -285"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, Layer, Layer + $".Item.{xSwitch}");

				container.Add(new CuiElement
				{
					Parent = Layer + $".Item.{xSwitch}",
					Components =
					{
						new CuiRawImageComponent
							{Png = ImageLibrary.Call<string>("GetImage", item.ShortName, item.Skin)},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-64 -128", OffsetMax = "64 0"
						}
					}
				});

				var amount = Mathf.CeilToInt(item.Amount * needPercent);

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "-100 10", OffsetMax = "100 30"
						},
						Text =
						{
							Text = Msg(player, RepairItemFormat, item.PublicTitle, amount),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 16,
							Color = _config.Colors.Color14.Get
						}
					}, Layer + $".Item.{xSwitch}");

				var hasAmount = HasAmount(Enumerable.ToArray(allItems), item.ShortName, item.Skin, amount);

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
							Color = hasAmount ? _config.Colors.Color4.Get : _config.Colors.Color7.Get,
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
						}
					}, Layer + $".Item.{xSwitch}");

				xSwitch += Width + Margin;
			});

			#endregion

			#region Buttons

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-120 -530",
					OffsetMax = "-10 -485"
				},
				Text =
				{
					Text = Msg(player, RepairButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = notItem ? _config.Colors.Color16.Get : _config.Colors.Color13.Get
				},
				Button =
				{
					Color = notItem ? _config.Colors.Color8.Get : _config.Colors.Color4.Get,
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
					Command = notItem ? "" : "UI_BankSystem atm_repair"
				}
			}, Layer);

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "10 -530",
					OffsetMax = "120 -485"
				},
				Text =
				{
					Text = Msg(player, RepairCancelButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = _config.Colors.Color16.Get
				},
				Button =
				{
					Color = _config.Colors.Color8.Get,
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
					Close = Layer,
					Command = "UI_BankSystem atmpage 0 0 0 true"
				}
			}, Layer);

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

		private void RegisterPermissions()
		{
			if (!string.IsNullOrEmpty(_config.Permission) &&
			    !permission.PermissionExists(_config.Permission))
				permission.RegisterPermission(_config.Permission, this);

			if (!string.IsNullOrEmpty(_config.Atm.PermissionToOpenATMviaCommand) &&
			    !permission.PermissionExists(_config.Atm.PermissionToOpenATMviaCommand))
				permission.RegisterPermission(_config.Atm.PermissionToOpenATMviaCommand, this);
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.Commands, nameof(CmdOpenBank));

			AddCovalenceCommand(_config.Atm.Commands, nameof(CmdOpenATM));

			AddCovalenceCommand("bank.givenote", nameof(CmdGiveNotes));

			AddCovalenceCommand("bank.giveatm", nameof(CmdGiveATM));

			AddCovalenceCommand(new[] {"bank.setbalance", "bank.deposit", "bank.withdraw", "bank.transfer"},
				nameof(AdminCommands));

			AddCovalenceCommand("bank.wipe", nameof(WipeCommands));
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

		private string GetCardNumber()
		{
			var number = "4";

			for (var i = 0; i < 15; i++) number += $"{Random.Range(0, 10)}";

			return number;
		}

		private string GetFormattedCardNumber(string number)
		{
			var result = string.Empty;

			var chars = number.ToCharArray();

			for (var i = 0; i < chars.Length; i++)
			{
				result += chars[i];

				if ((i + 1) % 4 == 0)
					result += " ";
			}

			return result;
		}

		private string GetLastCardNumbers(BasePlayer player, string number)
		{
			return player == null || string.IsNullOrEmpty(number)
				? string.Empty
				: Msg(player, CardFormat, number.Substring(12, 4));
		}

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

		private List<ulong> GetTopTransfers(BasePlayer player)
		{
			var logs = PlayerLogs.GetOrLoad(player.UserIDString);
			if (logs == null) return new List<ulong>();

			var topDict = new Dictionary<ulong, int>();

			logs.Transfers
				.FindAll(x => x.Type == Transaction.Transfer && x.TargetId.IsSteamId())
				.ForEach(transfer =>
				{
					if (topDict.ContainsKey(transfer.TargetId))
						topDict[transfer.TargetId] += 1;
					else
						topDict.Add(transfer.TargetId, 1);
				});

			return topDict.ToList()
				.OrderByDescending(x => x.Value)
				.Select(x => x.Key);
		}

		private void CreateCard(BasePlayer player)
		{
			if (player == null || Interface.CallHook("CanPlayerCreateCard", player) != null)
				return;

			if (HasCard(player))
			{
				SendNotify(player, AlreadyHaveCard, 1);
				return;
			}

#if TESTING
			Debug.Log($"[CreateCard] called GetOrCreate with id={player.UserIDString}");
#endif
			var data = PlayerData.GetOrCreate(player.UserIDString);
			if (data == null) return;

			data.Card = GetCardNumber();
			data.CardDate = DateTime.Now;

			if (_config.StartingBalance > 0)
				data.Balance = _config.StartingBalance;

			Interface.CallHook("OnPlayerCreatedCard", player);

			NextTick(() => SendNotify(player, BecameCardOwner, 0, data.Card));
		}

		private void RemoveCard(ulong member)
		{
			var data = PlayerData.GetOrLoad(member.ToString());
			if (data == null) return;

			data.Card = null;
		}

		private void CheckCard(ulong member)
		{
			var data = PlayerData.GetOrLoad(member.ToString());
			if (data == null) return;

			if (DateTime.Now.Subtract(data.CardDate).TotalDays > _config.CardExpiryDays)
				RemoveCard(member);
		}

		private string CardDateFormating(ulong member)
		{
			var data = PlayerData.GetOrLoad(member.ToString());
			if (data == null || string.IsNullOrEmpty(data.Card)) return string.Empty;

			return data.CardDate.AddDays(_config.CardExpiryDays).ToString("dd/MM");
		}

		private static bool HasAmount(Item[] items, string shortname, ulong skin, int amount)
		{
			return ItemCount(items, shortname, skin) >= amount;
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
					num1 += num2;
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

		private void AddPlayerTracking(GatherLogType type, BasePlayer member, string shortname, int amount = 1)
		{
			if (!member.userID.IsSteamId() || string.IsNullOrEmpty(shortname) || !HasCard(member)) return;

			FixNames(ref shortname);

			float cost;
			if (!(_config.Tracking.CollectibleCostTable.TryGetValue(shortname, out cost) ||
			      _config.Tracking.CostTable.TryGetValue(shortname, out cost)) ||
			    cost <= 0) return;

			var price = Mathf.CeilToInt(cost * amount);

			if (_config.Logs.CollectGatherLogs)
				PlayerLogs.GetOrCreate(member.UserIDString)?.GatherLogs.Add(new GatherLogData
				{
					Type = type,
					ShortName = shortname,
					Amount = price
				});

			if (_config.Tracking.InHand)
			{
				var item = _config.Currency?.ToItem(price);
				if (item != null)
					member.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			}
			else
			{
				_config.Economy.AddBalance(member, price);
			}
		}

		private void FixNames(ref string shortname)
		{
			if (shortname.Contains("ore"))
				shortname = shortname.Replace("-", ".");
		}

		private void AddTransactionLog(Transaction type, ulong sender, ulong target, int amount)
		{
			if (!sender.IsSteamId() || _config.Logs.CollectTransfersLogs == false) return;

			if (type == Transaction.Transfer)
				PlayerLogs.GetOrCreate(target.ToString())?.Transfers.Add(new TransferData
				{
					Type = type,
					SenderId = sender,
					TargetId = 0,
					Amount = amount
				});

			PlayerLogs.GetOrCreate(sender.ToString())?.Transfers.Add(new TransferData
			{
				Type = type,
				SenderId = sender,
				TargetId = target,
				Amount = amount
			});
		}

		private void SpawnATMs()
		{
			if (_config.Atm.Spawn.Monuments.Count > 0)
				TerrainMeta.Path.Monuments.ForEach(monument =>
				{
					if (monument == null || !_config.Atm.Spawn.Monuments.Any(x => monument.name.Contains(x.Key)))
						return;

					var conf = _config.Atm.Spawn.Monuments.FirstOrDefault(x => monument.name.Contains(x.Key)).Value;
					if (conf == null || !conf.Enabled || conf.Position == Vector3.zero) return;

					var transform = monument.transform;
					var rot = transform.rotation;
					var pos = transform.position + rot * conf.Position;

					SpawnATM(pos, transform.localEulerAngles, conf);
				});

			BaseNetworkable.serverEntities.OfType<VendingMachine>()
				.Where(x => x.skinID == _config.Atm.Skin)
				.ForEach(OnEntitySpawned);
		}

		private void SpawnATM(Vector3 pos, Vector3 rot, ATMPosition conf)
		{
			var entity =
				GameManager.server.CreateEntity(VendingMachinePrefab, pos,
					Quaternion.Euler(rot.x, rot.y + conf.Rotation, rot.z)) as VendingMachine;
			if (entity == null) return;

			entity.enableSaving = false;

			entity.skinID = _config.Atm.Skin;

			UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());

			UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());

			entity.Spawn();

			entity.lifestate = BaseCombatEntity.LifeState.Dead;

			entity.shopName = conf.DisplayName;
			entity.UpdateMapMarker();

			_vendingMachines.Add(entity);

			var data = new ATMData
			{
				ID = ++_atms.LastATMID,
				EntityID = entity.net.ID.Value,
				OwnerId = 0,
				Balance = 0,
				Condition = 100,
				DepositFee = conf.DepositFee,
				WithdrawalFee = conf.WithdrawFee,
				IsAdmin = true
			};

			_atms.ATMs.Add(data);
		}

		private void LoadImages()
		{
			if (!ImageLibrary)
			{
				BroadcastILNotInstalled();
			}
			else
			{
				_enabledImageLibrary = true;

				var imagesList = new Dictionary<string, string>();

				var itemIcons = new List<KeyValuePair<string, ulong>>();

				imagesList.Add(_config.CardImage, _config.CardImage);
				imagesList.Add(_config.DepositImage, _config.DepositImage);
				imagesList.Add(_config.WithdrawImage, _config.WithdrawImage);
				imagesList.Add(_config.TransferImage, _config.TransferImage);
				imagesList.Add(_config.ExitImage, _config.ExitImage);

				_config.Atm.Repair.Items.ForEach(item =>
					itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.Skin)));

				if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		private readonly Dictionary<string, string> _itemsTitles = new Dictionary<string, string>();

		private string GetItemTitle(BasePlayer player, string shortName)
		{
			string result;
			if (!_itemsTitles.TryGetValue(shortName, out result))
				_itemsTitles.Add(shortName, result = ItemManager.FindItemDefinition(shortName)?.displayName.english);

			if (_config.UseLangAPI && LangAPI != null && LangAPI.IsLoaded &&
			    LangAPI.Call<bool>("IsDefaultDisplayName", result))
				return LangAPI.Call<string>("GetItemDisplayName", shortName, result, player.UserIDString) ?? result;

			return result;
		}

		private void RenameATMs()
		{
			foreach (var entity in BaseNetworkable.serverEntities.OfType<VendingMachine>()
				         .Where(x => x.skinID == _config.Atm.Skin))
			{
				if (entity == null) continue;

				var data = _atms.ATMs.Find(z => z.EntityID == entity.net.ID.Value);
				if (data == null) continue;

				if (!string.IsNullOrEmpty(_config.Atm.ShopName))
				{
					entity.shopName = _config.Atm.ShopName
						.Replace("{id}", data.ID.ToString())
						.Replace("{owner}", PlayerData.GetNotLoad(data.OwnerId.ToString())?.DisplayName);
					entity.UpdateMapMarker();
				}
			}
		}

		#endregion

		#region API

		private bool HasCard(BasePlayer player)
		{
			return HasCard(player.userID);
		}

		private bool HasCard(ulong member)
		{
			var data = PlayerData.GetOrLoad(member.ToString());
			return data != null && !string.IsNullOrEmpty(data.Card);
		}

		private int Balance(BasePlayer player)
		{
			return Balance(player.userID);
		}

		private int Balance(string member)
		{
			return Balance(ulong.Parse(member));
		}

		private int Balance(ulong member)
		{
			return PlayerData.GetOrLoad(member.ToString())?.Balance ?? 0;
		}

		private bool Deposit(BasePlayer player, int amount)
		{
			return Deposit(player.userID, amount);
		}

		private bool Deposit(string member, int amount)
		{
			return Deposit(ulong.Parse(member), amount);
		}

		private bool Deposit(ulong member, int amount)
		{
#if TESTING
			Debug.Log($"[Deposit] called GetOrCreate with id={member.ToString()}");
#endif
			var data = PlayerData.GetOrCreate(member.ToString());
			if (data == null) return false;

			data.Balance += amount;

			Interface.CallHook("OnBalanceChanged", member, amount);
			return true;
		}

		private bool Withdraw(BasePlayer player, int amount)
		{
			return Withdraw(player.userID, amount);
		}

		private bool Withdraw(string member, int amount)
		{
			return Withdraw(ulong.Parse(member), amount);
		}

		private bool Withdraw(ulong member, int amount)
		{
			var data = PlayerData.GetOrLoad(member.ToString());
			if (data == null || data.Balance < amount)
				return false;

			data.Balance -= amount;

			Interface.CallHook("OnBalanceChanged", member, amount);
			return true;
		}

		private bool Transfer(BasePlayer member, BasePlayer target, int amount)
		{
			return Transfer(member.userID, target.userID, amount);
		}

		private bool Transfer(string member, string target, int amount)
		{
			return Transfer(ulong.Parse(member), ulong.Parse(target), amount);
		}

		private bool Transfer(ulong member, ulong target, int amount)
		{
			if (!Withdraw(member, amount)) return false;

			Deposit(target, amount);
			return true;
		}

		#endregion

		#region Lang

		private const string
			NoILError = "NoILError",
			ForbiddenWithdraw = "ForbiddenWithdraw",
			ForbiddenDeposit = "ForbiddenDeposit",
			CardExpiryDate = "CardExpiryDate",
			BtnNext = "BtnNext",
			BtnBack = "BtnBack",
			NoPermissions = "NoPermissions",
			CloseButton = "CloseButton",
			TitleMenu = "TitleMenu",
			BalanceTitle = "BalanceTitle",
			DepositIconTitle = "DepositIconTitle",
			DepositSymbolTitle = "DepositSymbolTitle",
			WithdrawalIconTitle = "WithdrawalIconTitle",
			WithdrawalSymbolTitle = "WithdrawalSymbolTitle",
			SelfTransferlIconTitle = "SelfTransferlIconTitle",
			SelfTransferSymbolTitle = "SelfTransferSymbolTitle",
			TransferlIconTitle = "TransferlIconTitle",
			TransferSymbolTitle = "TransferSymbolTitle",
			DepositOperationTitle = "DepositOperationTitle",
			WithdrawalOperationTitle = "WithdrawalOperationTitle",
			TransferOperationTitle = "TransferOperationTitle",
			DepositOperationDescription = "DepositOperationDescription",
			WithdrawalOperationDescription = "WithdrawalOperationDescription",
			SelfTransferOperationDescription = "SelfTransferOperationDescription",
			TransferOperationDescription = "TransferOperationDescription",
			OperationsValueFormat = "OperationsValueFormat",
			NotEnoughItems = "NotEnoughItems",
			NotEnoughMoney = "NotEnoughMoney",
			BrokenATM = "BrokenATM",
			NotBankCard = "NotBankCard",
			AlreadyHaveCard = "AlreadyHaveCard",
			BecameCardOwner = "BecameCardOwner",
			WelcomeTitle = "WelcomeTitle",
			MainTitle = "MainTitle",
			YourBalance = "YourBalance",
			CardBankTitle = "CardBankTitle",
			TransfersTitle = "TransfersTitle",
			FrequentTransfers = "FrequentTransfers",
			HaventTransactions = "HaventTransactions",
			TransferTitle = "TransferTitle",
			TransferByCard = "TransferByCard",
			CardNumberTitle = "CardNumberTitle",
			RemoveAmountTitle = "RemoveAmountTitle",
			TransactionHistory = "TransactionHistory",
			GatherHistory = "GatherHistory",
			CollectFee = "CollectFee",
			MiningFee = "MiningFee",
			LootFee = "LootFee",
			MiningValue = "MiningValue",
			CreateCardTitle = "CreateCardTitle",
			SelectPlayerTitle = "SelectPlayerTitle",
			DepositTitle = "DepositTitle",
			DepositDescription = "DepositDescription",
			WithdrawTitle = "WithdrawTitle",
			WithdrawDescription = "WithdrawDescription",
			TransferDescription = "TransferDescription",
			ExitTitle = "ExitTile",
			ExitDescription = "ExitDescription",
			ATMProfit = "ATMProfit",
			ProfitValue = "ProfitValue",
			ATMCondition = "ATMCondition",
			ConditionValue = "ConditionValue",
			ATMRepair = "ATMRepair",
			DepositFeeTitle = "DepositFee",
			FeeValue = "FeeValue",
			WithdrawalFeeTitle = "WithdrawalFee",
			EnterAmount = "EnterAmount",
			TotalDeposit = "TotalDeposit",
			TotalValue = "TotalValue",
			TotalWithdraw = "TotalWithdraw",
			TotalTransfer = "TotalTransfer",
			RepairTitle = "RepairTitle",
			RepairSecondTitle = "RepairSecondTitle",
			RepairItemFormat = "RepairItemFormat",
			RepairButton = "RepairButton",
			RepairCancelButton = "RepairCancelButton",
			CardFormat = "CardFormat",
			InfoDepositTitle = "InfoDepositTitle",
			InfoWithdrawalTitle = "InfoWithdrawalTitle",
			InfoValueTitle = "InfoValueTitle",
			InfoManageBtn = "InfoManageBtn",
			InfoBackBtn = "InfoBackBtn",
			SelectPlayerSecond = "SelectPlayerSecond",
			ATMTitle = "ATMTitle",
			ATMAdminTitle = "ATMAdminTitle",
			ATMOwner = "ATMOwner",
			TargetTransferedMoney = "TargetTransferedMoney",
			TransferedMoney = "TransferedMoney",
			DepositedMoney = "DepositedMoney",
			WithdrawnMoney = "WithdrawnMoney",
			AtmOwnWithdrawnMoney = "AtmOwnWithdrawnMoney";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoPermissions] = "You don't have permissions!",
				[CloseButton] = "✕",
				[TitleMenu] = "Bank",
				[BalanceTitle] = "{0}$",
				[DepositIconTitle] = "+",
				[DepositSymbolTitle] = "+",
				[WithdrawalIconTitle] = "—",
				[WithdrawalSymbolTitle] = "-",
				[SelfTransferlIconTitle] = "+",
				[SelfTransferSymbolTitle] = "+",
				[TransferlIconTitle] = "—",
				[TransferSymbolTitle] = "-",
				[DepositOperationTitle] = "Deposit",
				[WithdrawalOperationTitle] = "Withdrawal",
				[TransferOperationTitle] = "Transfer",
				[DepositOperationDescription] = "You have funded your bank account",
				[WithdrawalOperationDescription] = "You have withdrawn money from your bank account",
				[SelfTransferOperationDescription] = "{0} has transferred money to you",
				[TransferOperationDescription] = "You have transferred money to player {0}",
				[OperationsValueFormat] = "{0}{1}$",
				[NotEnoughItems] = "You don't have enough items!",
				[NotEnoughMoney] = "You don't have enough money!",
				[BrokenATM] = "ATM is broken!",
				[NotBankCard] = "You do not have a credit card",
				[AlreadyHaveCard] = "You already have a credit card!",
				[BecameCardOwner] = "Congratulations! You became the owner of the card {0}!",
				[WelcomeTitle] = "Welcome <b>{0}</b>",
				[MainTitle] = "RUST<color=#4B68FF>Bank</color>",
				[YourBalance] = "Your balance:",
				[CardBankTitle] = "RUSTBank",
				[TransfersTitle] = "Transfers",
				[FrequentTransfers] = "Recent transfers:",
				[HaventTransactions] = "You have no transactions yet :(",
				[TransferTitle] = "Transfer",
				[TransferByCard] = "Transfer by card:",
				[CardNumberTitle] = "     Card number",
				[RemoveAmountTitle] = "X",
				[TransactionHistory] = "Transactions history:",
				[GatherHistory] = "Gather history:",
				[CollectFee] = "{0} collection fee",
				[MiningFee] = "{0} mining fee",
				[LootFee] = "{0} loot fee",
				[MiningValue] = "+{0}$",
				[CreateCardTitle] = "Create Card",
				[SelectPlayerTitle] = "Select player to transfer",
				[DepositTitle] = "Deposit",
				[DepositDescription] = "Deposit cash to your bank account",
				[WithdrawTitle] = "Withdraw",
				[WithdrawDescription] = "Withdraw cash to your balance",
				[TransferDescription] = "Transfer money to another player",
				[ExitTitle] = "Exit",
				[ExitDescription] = "Exit ATM",
				[ATMProfit] = "Profit by ATM",
				[ProfitValue] = "${0}",
				[ATMCondition] = "ATM Condition",
				[ConditionValue] = "{0}%",
				[ATMRepair] = "Repair",
				[DepositFeeTitle] = "Deposit fee:",
				[FeeValue] = "{0}%",
				[WithdrawalFeeTitle] = "Withdrawal fee:",
				[EnterAmount] = "Enter the amount:",
				[RepairTitle] = "TO REPAIR THE ATM",
				[RepairSecondTitle] = "YOU NEED",
				[RepairItemFormat] = "{0} ({1} pcs)",
				[RepairButton] = "REPAIR",
				[RepairCancelButton] = "CANCEL",
				[CardFormat] = "**** **** **** {0}",
				[InfoDepositTitle] = "Deposit:",
				[InfoValueTitle] = "{0}%",
				[InfoWithdrawalTitle] = "Withdrawal:",
				[InfoManageBtn] = "MANAGE",
				[InfoBackBtn] = "BACK",
				[TotalDeposit] = "Total for deposit:",
				[TotalValue] = "${0}",
				[TotalWithdraw] = "Total for withdraw:",
				[TotalTransfer] = "Total for transfer:",
				[SelectPlayerSecond] = "Select a player",
				[ATMTitle] = "ATM #{0}",
				[ATMAdminTitle] = "ATM",
				[ATMOwner] = "Owner:",
				[TargetTransferedMoney] = "Player `{0}` transferred ${1} to you",
				[TransferedMoney] = "You have successfully transferred ${0} to player '{1}'",
				[DepositedMoney] = "You have successfully replenished your balance for ${0}!",
				[WithdrawnMoney] = "You have successfully withdrawn ${0}",
				[AtmOwnWithdrawnMoney] = "You have successfully withdrawn money from your ATM!",
				[BtnBack] = "▲",
				[BtnNext] = "▼",
				[CardExpiryDate] = "**/**",
				[ForbiddenDeposit] = "It is forbidden to deposit less than {0}$!",
				[ForbiddenWithdraw] = "It is forbidden to withdraw less than {0}$!",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
				["crate_elite"] = "Crate Elite",
				["crate_normal"] = "Crate Normal"
			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoPermissions] = "У вас нет необходимого разрешения",
				[CloseButton] = "✕",
				[TitleMenu] = "Банк",
				[BalanceTitle] = "{0}$",
				[DepositIconTitle] = "+",
				[DepositSymbolTitle] = "+",
				[WithdrawalIconTitle] = "—",
				[WithdrawalSymbolTitle] = "-",
				[SelfTransferlIconTitle] = "+",
				[SelfTransferSymbolTitle] = "+",
				[TransferlIconTitle] = "—",
				[TransferSymbolTitle] = "-",
				[DepositOperationTitle] = "Пополнение",
				[WithdrawalOperationTitle] = "Снятие",
				[TransferOperationTitle] = "Перевод",
				[DepositOperationDescription] = "Вы пополнили свой банковский счет",
				[WithdrawalOperationDescription] = "Вы сняли деньги со своего банковского счета",
				[SelfTransferOperationDescription] = "{0} перевел вам деньги",
				[TransferOperationDescription] = "Вы перевели деньги игроку {0}",
				[OperationsValueFormat] = "{0}{1}$",
				[NotEnoughItems] = "У вас недостаточно предметов!",
				[NotEnoughMoney] = "У вас недостаточно денег!",
				[BrokenATM] = "Банкомат сломан!",
				[NotBankCard] = "У вас нет кредитной карты",
				[AlreadyHaveCard] = "У вас уже есть кредитная карта!",
				[BecameCardOwner] = "Поздравляем! Вы стали владельцем {0}!",
				[WelcomeTitle] = "Добро пожаловать, <b>{0}</b>",
				[MainTitle] = "RUST<color=#4B68FF>Bank</color>",
				[YourBalance] = "Ваш баланс:",
				[CardBankTitle] = "RUSTBank",
				[TransfersTitle] = "Переводы",
				[FrequentTransfers] = "Последние переводы:",
				[HaventTransactions] = "У вас еще нет транзакций :(",
				[TransferTitle] = "Перевести",
				[TransferByCard] = "Перевод по карте:",
				[CardNumberTitle] = "     Номер карты",
				[RemoveAmountTitle] = "X",
				[TransactionHistory] = "Истогрия транзакций:",
				[GatherHistory] = "История добычи:",
				[CollectFee] = "Плата за сбор {0}",
				[MiningFee] = "Плата за добычу {0}",
				[LootFee] = "Плата за лут {0}",
				[MiningValue] = "+{0}$",
				[CreateCardTitle] = "Создать Карту",
				[SelectPlayerTitle] = "Выберите игрока для перевода",
				[DepositTitle] = "Пополнение",
				[DepositDescription] = "Вносите наличные на свой банковский счет",
				[WithdrawTitle] = "Снятие",
				[WithdrawDescription] = "Выводите наличные со своего банковского счёта",
				[TransferDescription] = "Переводите денег другому игроку",
				[ExitTitle] = "Выход",
				[ExitDescription] = "Покинуть Банкомат",
				[ATMProfit] = "Доход Банкомата",
				[ProfitValue] = "{0}$",
				[ATMCondition] = "Состояние Банкомата",
				[ConditionValue] = "{0}%",
				[ATMRepair] = "Починить",
				[DepositFeeTitle] = "Комиссия за пополнение:",
				[FeeValue] = "{0}%",
				[WithdrawalFeeTitle] = "Комиссия за снятие:",
				[EnterAmount] = "Введите количество:",
				[RepairTitle] = "ДЛЯ РЕМОНТА БАНКОМАТА",
				[RepairSecondTitle] = "ВАМ ТРЕБУЕТСЯ",
				[RepairItemFormat] = "{0} ({1} шт)",
				[RepairButton] = "ПОЧИНИТЬ",
				[RepairCancelButton] = "ОТМЕНА",
				[CardFormat] = "**** **** **** {0}",
				[InfoDepositTitle] = "Пополнение:",
				[InfoValueTitle] = "{0}%",
				[InfoWithdrawalTitle] = "Снятие:",
				[InfoManageBtn] = "УПРАВЛЕНИЕ",
				[InfoBackBtn] = "НАЗАД",
				[TotalDeposit] = "Итого к пополнению:",
				[TotalValue] = "{0}$",
				[TotalWithdraw] = "Итого к снятию:",
				[TotalTransfer] = "Итого к переводу:",
				[SelectPlayerSecond] = "Выберите игрока",
				[ATMTitle] = "ATM #{0}",
				[ATMAdminTitle] = "ATM",
				[ATMOwner] = "Владелец:",
				[TargetTransferedMoney] = "Игрок `{0}` перевёл вам {1}$",
				[TransferedMoney] = "Вы успешно перевели {0}$ игроку '{1}'",
				[DepositedMoney] = "Вы успешно пополнили баланс на ${0}!",
				[WithdrawnMoney] = "Вы успешня сняли {0}$",
				[AtmOwnWithdrawnMoney] = "Вы успешно сняли деньги в своём банкомате!",
				[BtnBack] = "▲",
				[BtnNext] = "▼",
				[CardExpiryDate] = "**/**",
				[ForbiddenDeposit] = "Запрещено вносить в банкомат меньше {0}$!",
				[ForbiddenWithdraw] = "Запрещено снимать в банкомате меньше {0}$!",
				[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!",
				["crate_elite"] = "Crate Elite",
				["crate_normal"] = "Crate Normal"
			}, this, "ru");
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

		#region Convert

		#region Economics

		[ConsoleCommand("bank.convert.economics")]
		private void CmdConsoleConvertEconomics(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			ConvertFromEconomics();
		}

		private class EconomicsData
		{
			public Dictionary<string, double> Balances = new Dictionary<string, double>();
		}

		private void ConvertFromEconomics()
		{
			EconomicsData data = null;

			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<EconomicsData>("Economics");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (data == null) return;

			var amount = 0;
			foreach (var check in data.Balances)
			{
				ulong member;
				if (!ulong.TryParse(check.Key, out member)) continue;

				var newBalance = Mathf.CeilToInt((float) check.Value);

				var playerData = PlayerData.GetOrLoad(member.ToString());
				if (playerData != null)
				{
					playerData.Balance += newBalance;
				}
				else
				{
#if TESTING
					Debug.Log($"[ConvertFromEconomics] called GetOrCreate with id={member.ToString()}");
#endif
					playerData = PlayerData.GetOrCreate(member.ToString());

					playerData.Balance = newBalance;

					var name = covalence.Players.FindPlayer(check.Key)?.Name;
					if (!string.IsNullOrEmpty(name))
						playerData.DisplayName = name;
				}

				amount++;
			}

			Puts($"{amount} players was converted!");
		}

		#endregion

		#region Server Rewards

		[ConsoleCommand("bank.convert.serverrewards")]
		private void CmdConsoleConvertServerRewards(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			ConvertFromServerRewards();
		}

		private class ServerRewardsData
		{
			public Dictionary<ulong, int> playerRP = new Dictionary<ulong, int>();
		}

		private void ConvertFromServerRewards()
		{
			ServerRewardsData data = null;

			try
			{
				data =
					Interface.Oxide.DataFileSystem.ReadObject<ServerRewardsData>("ServerRewards/player_data");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (data == null) return;

			var amount = 0;
			foreach (var check in data.playerRP)
			{
				var playerData = PlayerData.GetOrLoad(check.Key.ToString());
				if (playerData != null)
				{
					playerData.Balance += check.Value;
				}
				else
				{
#if TESTING
					Debug.Log($"[ConvertFromServerRewards] called GetOrCreate with id={check.Key.ToString()}");
#endif
					playerData = PlayerData.GetOrCreate(check.Key.ToString());

					playerData.Balance = check.Value;

					var name = covalence.Players.FindPlayer(check.Key.ToString())?.Name;
					if (!string.IsNullOrEmpty(name))
						playerData.DisplayName = name;
				}

				amount++;
			}

			Puts($"{amount} players was converted!");
		}

		#endregion

		#region Data 2.0

		#region Player Data

		private void ConvertPlayerData()
		{
			var data = LoadOldPlayerData();
			if (data != null)
			{
				ConvertOldPlayerData(data);

				ClearDataCache();
			}
		}

		private OldPluginData LoadOldPlayerData()
		{
			OldPluginData data = null;
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<OldPluginData>($"{Name}/Players");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			return data ?? new OldPluginData();
		}

		private void ConvertOldPlayerData(OldPluginData data)
		{
			data.Players.ToList().ForEach(playerData =>
			{
#if TESTING
				Debug.Log($"[ConvertOldPlayerData] called GetOrCreate with id={playerData.Key.ToString()}");
#endif
				var newData = PlayerData.GetOrCreate(playerData.Key.ToString());

				newData.DisplayName = playerData.Value.DisplayName;
				newData.Balance = playerData.Value.Balance;
				newData.Card = playerData.Value.Card;
				newData.CardDate = playerData.Value.CardDate;
			});
		}

		#endregion

		#region Player Logs

		private void ConvertPlayerLogs()
		{
			var logs = LoadOldPlayerData();
			if (logs != null)
			{
				ConvertOldPlayerData(logs);

				ClearDataCache();
			}
		}

		private OldLogsData LoadOldPlayerLogs()
		{
			OldLogsData data = null;
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<OldLogsData>($"{Name}/Logs");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			return data ?? new OldLogsData();
		}

		private void ConvertOldPlayerLogs(OldLogsData logs)
		{
			logs.Players.ToList().ForEach(playerData =>
			{
				var newData = PlayerLogs.GetOrCreate(playerData.Key.ToString());

				newData.Transfers = playerData.Value.Transfers;
				newData.GatherLogs = playerData.Value.GatherLogs;
			});
		}

		#endregion

		#region Classes

		#region Players

		private class OldPluginData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, OldPlayerData> Players = new Dictionary<ulong, OldPlayerData>();
		}

		private class OldPlayerData
		{
			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Balance")]
			public int Balance;

			[JsonProperty(PropertyName = "Card Number")]
			public string Card;

			[JsonProperty(PropertyName = "Card Date")]
			public DateTime CardDate;
		}

		#endregion

		#region Logs

		private class OldLogsData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, OldPlayerLogs> Players = new Dictionary<ulong, OldPlayerLogs>();
		}

		private class OldPlayerLogs
		{
			[JsonProperty(PropertyName = "Transfers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<TransferData> Transfers = new List<TransferData>();

			[JsonProperty(PropertyName = "Gather Logs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<GatherLogData> GatherLogs = new List<GatherLogData>();
		}

		#endregion

		#endregion

		#endregion

		#endregion

		#region Data 2.0

		#region Player Data

		private Dictionary<string, PlayerData> _usersData = new Dictionary<string, PlayerData>();

		private class PlayerData
		{
			#region Fields

			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Balance")]
			public int Balance;

			[JsonProperty(PropertyName = "Card Number")]
			public string Card;

			[JsonProperty(PropertyName = "Card Date")]
			public DateTime CardDate;

			#endregion

			#region Utils

			public static string BaseFolder()
			{
				return "BankSystem" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
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
				_instance._usersData.Remove(userId);
			}

			public static void SaveAndUnload(string userId)
			{
				Save(userId);

				Unload(userId);
			}

			public static PlayerData GetOrLoad(string userId)
			{
				if (!userId.IsSteamId()) return null;

				var data = GetOrLoad(BaseFolder(), userId);

				TryToWipe(userId, ref data);

				return data;
			}

			public static PlayerData GetNotLoad(string userId)
			{
				if (!userId.IsSteamId()) return null;

				var data = GetOrLoad(BaseFolder(), userId, false);

				return data;
			}

			public static PlayerData GetOrCreate(string userId)
			{
				if (!userId.IsSteamId()) return null;

				return GetOrLoad(userId) ?? (_instance._usersData[userId] = new PlayerData());
			}

			public static bool IsLoaded(string userId)
			{
				return _instance._usersData.ContainsKey(userId);
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

			#region Utils

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

			#endregion

			#region Wipe

			[JsonProperty(PropertyName = "Last Wipe")]
			public DateTime LastWipe;

			public static void TryToWipe(string userId, ref PlayerData data)
			{
				if (_config.Wipe.Players && data != null &&
				    SaveRestore.SaveCreatedTime.ToUniversalTime() <= data.LastWipe.ToUniversalTime())
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

		#region Player Logs

		private Dictionary<string, PlayerLogs> _usersLogs = new Dictionary<string, PlayerLogs>();

		private class PlayerLogs
		{
			#region Fields

			[JsonProperty(PropertyName = "Transfers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<TransferData> Transfers = new List<TransferData>();

			[JsonProperty(PropertyName = "Gather Logs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<GatherLogData> GatherLogs = new List<GatherLogData>();

			#endregion

			#region Utils

			public static string BaseFolder()
			{
				return "BankSystem" + Path.DirectorySeparatorChar + "Logs" + Path.DirectorySeparatorChar;
			}

			public static void Save(string userId)
			{
				PlayerLogs logs;
				if (!_instance._usersLogs.TryGetValue(userId, out logs))
					return;

				Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, logs);
			}

			public static void Unload(string userId)
			{
				_instance._usersLogs.Remove(userId);
			}

			public static void SaveAndUnload(string userId)
			{
				Save(userId);

				Unload(userId);
			}

			public static PlayerLogs GetOrLoad(string userId)
			{
				if (!userId.IsSteamId()) return null;

				var data = GetOrLoad(BaseFolder(), userId);

				TryToWipe(userId, ref data);

				return data;
			}

			public static PlayerLogs GetOrCreate(string userId)
			{
				if (!userId.IsSteamId()) return null;

				return GetOrLoad(userId) ?? (_instance._usersLogs[userId] = new PlayerLogs());
			}

			public static bool IsLoaded(string userId)
			{
				return _instance._usersLogs.ContainsKey(userId);
			}

			public static PlayerLogs GetOrLoad(string baseFolder, string userId, bool load = true)
			{
				PlayerLogs logs;
				if (_instance._usersLogs.TryGetValue(userId, out logs)) return logs;

				try
				{
					logs = ReadOnlyObject(baseFolder + userId);
				}
				catch (Exception e)
				{
					Interface.Oxide.LogError(e.ToString());
				}

				return load
					? _instance._usersLogs[userId] = logs
					: logs;
			}

			#region Utils

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

			private static PlayerLogs ReadOnlyObject(string name)
			{
				return Interface.Oxide.DataFileSystem.ExistsDatafile(name)
					? Interface.Oxide.DataFileSystem.GetFile(name).ReadObject<PlayerLogs>()
					: null;
			}

			#endregion

			#endregion

			#region Wipe

			[JsonProperty(PropertyName = "Last Wipe")]
			public DateTime LastWipe;

			public static void TryToWipe(string userId, ref PlayerLogs logs)
			{
				if (_config.Wipe.Logs && logs != null &&
				    SaveRestore.SaveCreatedTime.ToUniversalTime() <= logs.LastWipe.ToUniversalTime())
				{
					_instance._usersLogs[userId] = logs = new PlayerLogs()
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
			ServerMgr.Instance.StartCoroutine(ClearDataCacheCoroutine());
		}

		private IEnumerator ClearDataCacheCoroutine()
		{
			var players = BasePlayer.activePlayerList.Select(x => x.UserIDString).ToList();

			_usersData.Where(x => !players.Contains(x.Key)).ForEach(data => { PlayerData.SaveAndUnload(data.Key); });

			yield return CoroutineEx.waitForEndOfFrame;

			_usersLogs.Where(x => !players.Contains(x.Key))
				.ForEach(data => { PlayerLogs.SaveAndUnload(data.Key); });
		}

		private void UnloadAndSavePlayer(string userID)
		{
			PlayerData.SaveAndUnload(userID);

			PlayerLogs.SaveAndUnload(userID);
		}

		#endregion

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

namespace Oxide.Plugins.BankSystemExtensionMethods
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

		public static List<T> ReverseSkipTake<T>(this List<T> source, int skip, int take)
		{
			var startIndex = source.Count - skip - take;
			if (startIndex < 0)
			{
				take += startIndex;
				startIndex = 0;
			}

			var sublist = source.GetRange(startIndex, take);
			sublist.Reverse();
			return sublist;
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
					if (c.Current is T)
						b.Add(c.Current as T);
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
	}
}

#endregion Extension Methods
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */