using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

// ReSharper disable MemberHidesStaticFromOuterClass

namespace Oxide.Plugins
{
	[Info("Referrals", "Mevent", "1.3.3")]
	public class ReferralsRu : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary = null, NoEscape = null, Notify = null, UINotify = null;

		private const string Layer = "UI.Referrals";

		private static ReferralsRu _instance;

#if CARBON
	    private ImageDatabaseModule imageDatabase;
#endif

		private bool _enabledImageLibrary;

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Команды", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"ref", "referal"};

			[JsonProperty(PropertyName = "Команды для активации промокода",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] ActivePromoCommands =
			{
				"promo",
				"code"
			};

			[JsonProperty(PropertyName = "Разрешение (пример: referralsru.use)")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = "Включить работу с Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Включиить авто-вайп?")]
			public bool AutoWipe = false;

			[JsonProperty(PropertyName = "Символы для промокода")]
			public string PromoCodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

			[JsonProperty(PropertyName = "Длина промокода")]
			public int PromoCodeLength = 8;

			[JsonProperty(PropertyName = "Минимальное наигранное время (секунды)")]
			public int MinPlayTime = 3600;

			[JsonProperty(PropertyName = "Награды", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Award> Awards = new List<Award>
			{
				new Award
				{
					InvitesAmount = 1,
					ID = 1,
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "wood",
					Skin = 0,
					Amount = 20000,
					ShowDescription = false,
					Description = new List<string>()
				},
				new Award
				{
					InvitesAmount = 2,
					ID = 2,
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "stones",
					Skin = 0,
					Amount = 15000,
					ShowDescription = false,
					Description = new List<string>()
				},
				new Award
				{
					InvitesAmount = 5,
					ID = 3,
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "leather",
					Skin = 0,
					Amount = 2400,
					ShowDescription = false,
					Description = new List<string>()
				},
				new Award
				{
					InvitesAmount = 7,
					ID = 4,
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "cloth",
					Skin = 0,
					Amount = 2300,
					ShowDescription = false,
					Description = new List<string>()
				},
				new Award
				{
					InvitesAmount = 10,
					ID = 5,
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "lowgradefuel",
					Skin = 0,
					Amount = 1500,
					ShowDescription = false,
					Description = new List<string>()
				}
			};

			[JsonProperty(PropertyName = "Давать награду игроку, активирующему промокод?")]
			public bool GiveSelfAward = true;

			[JsonProperty(PropertyName = "Награда для игрока, активирующего промокод",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<SelfAward> SelfAwards = new List<SelfAward>
			{
				new SelfAward
				{
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "wood",
					Skin = 0,
					Amount = 20000,
					Chance = 50
				},
				new SelfAward
				{
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "stones",
					Skin = 0,
					Amount = 15000,
					Chance = 50
				},
				new SelfAward
				{
					Type = ItemConf.ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "leather",
					Skin = 0,
					Amount = 2400,
					Chance = 50
				}
			};

			[JsonProperty(PropertyName = "Блокировки наград")]
			public RewardBlocks RewardBlocks = new()
			{
				UseBlockRewardsAfterWipe = false,
				BlockAfterWipeTime = 7200,
				UseBlockRewardsDuringRaidBlock = false,
				UseBlockRewardsDuringCombatBlock = false
			};

			[JsonProperty(PropertyName = "Интерфейс")]
			public InterfaceSettings UI = new()
			{
				Color1 = new IColor("#0E0E10"),
				Color2 = new IColor("#161617"),
				Color3 = new IColor("#FFFFFF"),
				Color4 = new IColor("#4B68FF"),
				Color5 = new IColor("#FFFFFF", 5),
				Color6 = new IColor("#FFFFFF", 20),
				Color7 = new IColor("#4B68FF", 33),
				Color8 = new IColor("#74884A"),
				UseScrollForAwards = true,
				UseScrollForInvitedPlayers = true,
			};
		}

		private class RewardBlocks
		{
			[JsonProperty(PropertyName = "Использовать блокировку наград после вайпа??")]
			public bool UseBlockRewardsAfterWipe;

			[JsonProperty(PropertyName = "Время наград блокировки после вайпа")]
			public float BlockAfterWipeTime;

			[JsonProperty(PropertyName = "Использовать блокировку наград во вреймя рейд блока?")]
			public bool UseBlockRewardsDuringRaidBlock;

			[JsonProperty(PropertyName = "Использовать блокировку наград во вреймя комбат блока?")]
			public bool UseBlockRewardsDuringCombatBlock;
		}

		private class InterfaceSettings
		{
			[JsonProperty(PropertyName = "Цвет 1")]
			public IColor Color1;

			[JsonProperty(PropertyName = "Цвет 2")]
			public IColor Color2;

			[JsonProperty(PropertyName = "Цвет 3")]
			public IColor Color3;

			[JsonProperty(PropertyName = "Цвет 4")]
			public IColor Color4;

			[JsonProperty(PropertyName = "Цвет 5")]
			public IColor Color5;

			[JsonProperty(PropertyName = "Цвет 6")]
			public IColor Color6;

			[JsonProperty(PropertyName = "Цвет 7")]
			public IColor Color7;

			[JsonProperty(PropertyName = "Цвет 8")]
			public IColor Color8;

			[JsonProperty(PropertyName = "Использовать скролл для наград?")]
			public bool UseScrollForAwards;

			[JsonProperty(PropertyName = "Использовать скролл для приглашенных игроков?")]
			public bool UseScrollForInvitedPlayers;
		}

		private class IColor
		{
			[JsonProperty(PropertyName = "HEX")] public string Hex;

			[JsonProperty(PropertyName = "Непрозрачность (0 - 100)")]
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

		private abstract class ItemConf
		{
			public enum ItemType
			{
				Item,
				Command,
				Plugin,
				Kit
			}

			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ItemType Type;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Command (%steamid%)")]
			public string Command;

			[JsonProperty(PropertyName = "Kit")] public string Kit;

			[JsonProperty(PropertyName = "Plugin")]
			public PluginItem Plugin;

			[JsonProperty(PropertyName = "Display Name (empty - default)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

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

			[JsonIgnore] private ItemDefinition _def;

			[JsonIgnore]
			public ItemDefinition Definition
			{
				get
				{
					if (_def == null) _def = ItemManager.FindItemDefinition(ShortName);

					return _def;
				}
			}

			private string GetName()
			{
				if (!string.IsNullOrEmpty(Title))
					return Title;

				if (!string.IsNullOrEmpty(DisplayName))
					return DisplayName;

				var def = Definition;
				if (!string.IsNullOrEmpty(ShortName) && def != null)
					return def.displayName.translated;

				return string.Empty;
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
				var def = Definition;
				if (def == null)
				{
					Debug.LogError($"Error creating item with ShortName '{ShortName}'");
					return;
				}

				GetStacks(def, Amount * count)?.ForEach(stack =>
				{
					var newItem = ItemManager.Create(def, stack, Skin);
					if (newItem == null)
					{
						_instance?.PrintError($"Error creating item with ShortName '{ShortName}'");
						return;
					}

					if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

					player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
				});
			}

			private void ToCommand(BasePlayer player, int count)
			{
				for (var i = 0; i < count; i++)
				{
					var command = Command.Replace("\n", "|")
						.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
							"%username%",
							player.displayName, StringComparison.OrdinalIgnoreCase);

					foreach (var check in command.Split('|')) _instance?.Server.Command(check);
				}
			}

			private static List<int> GetStacks(ItemDefinition item, int amount)
			{
				var list = Pool.GetList<int>();
				var maxStack = item.stackable;

				if (maxStack == 0) maxStack = 1;

				while (amount > maxStack)
				{
					amount -= maxStack;
					list.Add(maxStack);
				}

				list.Add(amount);

				return list;
			}
		}

		private class SelfAward : ItemConf
		{
			[JsonProperty(PropertyName = "Chance")]
			public float Chance;
		}

		private class Award : ItemConf
		{
			[JsonProperty(PropertyName = "Invites Amount")]
			public int InvitesAmount;

			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = "Показывать описание")]
			public bool ShowDescription;

			[JsonProperty(PropertyName = "Описание", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Description = new List<string>();

			[JsonIgnore] private string _description = string.Empty;

			public string GetDescription()
			{
				if (string.IsNullOrEmpty(_description) && Description != null)
					_description = string.Join("\n", Description);

				return _description ?? string.Empty;
			}
		}

		private class PluginItem
		{
			[JsonProperty(PropertyName = "Hook")] public string Hook = string.Empty;

			[JsonProperty(PropertyName = "Plugin name")]
			public string Plugin = string.Empty;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount = 1;

			[JsonProperty("(GameStores) Store ID in the service")]
			public readonly string ShopID = "UNDEFINED";

			[JsonProperty("(GameStores) Server ID in the service")]
			public readonly string ServerID = "UNDEFINED";

			[JsonProperty("(GameStores) Secret Key")]
			public readonly string SecretKey = "UNDEFINED";

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
					case "RustStore":
					{
						plug.CallHook(Hook, player.userID, Amount * count, new Action<string>(result =>
						{
							if (result == "SUCCESS")
							{
								Interface.Oxide.LogDebug(
									$"Player {player.displayName} ({player.UserIDString}) received {Amount * count} to the balance in {plug}");
								return;
							}

							Interface.Oxide.LogDebug(
								$"The balance of the player {player.userID}  has not been changed, error: {result}");
						}));
						break;
					}
					case "GameStoresRUST":
					{
						_instance?.webrequest.Enqueue(
							$"https://gamestores.ru/api/?shop_id={ShopID}&secret={SecretKey}&server={ServerID}&action=moneys&type=plus&steam_id={player.UserIDString}&amount={Amount * count}",
							"", (code, response) =>
							{
								switch (code)
								{
									case 0:
									{
										_instance?.PrintError("Api does not responded to a request");
										break;
									}
									case 200:
									{
										Interface.Oxide.LogDebug(
											$"Player {player.displayName} ({player.UserIDString}) received {Amount * count} to the balance in {plug}");
										break;
									}
									case 404:
									{
										_instance?.PrintError("Please check your configuration! [404]");
										break;
									}
								}
							}, _instance);
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

		private PluginData _data;

		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
		}

		private class PluginData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
		}

		private class PlayerData
		{
			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Activated")]
			public bool Activated;

			[JsonProperty(PropertyName = "Promo Code")]
			public string PromoCode;

			[JsonProperty(PropertyName = "Play Time")]
			public int PlayTime;

			[JsonProperty(PropertyName = "Received Awards", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<int> ReceivedAwards = new List<int>();

			[JsonProperty(PropertyName = "Invited Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ulong> InvitedPlayers = new List<ulong>();
		}

		private PlayerData GetPlayerData(BasePlayer player)
		{
			return GetPlayerData(player.userID);
		}

		private PlayerData GetPlayerData(ulong member)

		{
			if (!_data.Players.ContainsKey(member))
				_data.Players.Add(member, new PlayerData
				{
					PromoCode = GetRandomPromoCode()
				});

			return _data.Players[member];
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadData();
		}

		private void OnServerInitialized()
		{
			LoadImages();

			if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
				permission.RegisterPermission(_config.Permission, this);

			AddCovalenceCommand(_config.Commands, nameof(CmdOpenUi));
			AddCovalenceCommand(_config.ActivePromoCommands, nameof(CmdActivePromo));

			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);

			if (_config.MinPlayTime > 0)
				timer.Every(1, DataHandle);
		}

		private void OnServerSave()
		{
			timer.In(Random.Range(2f, 7f), SaveData);
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

			SaveData();

			_instance = null;
		}

		private void OnNewSave(string filename)
		{
			if (!_config.AutoWipe) return;

			if (_data == null)
				LoadData();

			_data?.Players.Clear();

			SaveData();

			PrintWarning($"Wipe detected! {Name} wiped!");
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

			GetAvatar(player.userID,
				avatar => AddImage(avatar, $"avatar_{player.UserIDString}"));

			var data = GetPlayerData(player);
			if (data == null || string.IsNullOrEmpty(player.displayName)) return;

			data.DisplayName = player.displayName;
		}

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

		[ConsoleCommand("UI_Referrals")]
		private void CmdConsoleReferrals(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "page":
				{
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var page)) return;

					var zPage = 0;
					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out zPage);

					MainUi(player, page, zPage);
					break;
				}

				case "infoaward":
				{
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var id)) return;

					var item = _config.Awards.Find(x => x.ID == id);
					if (item == null) return;

					ShowDescription(player, item);
					break;
				}

				case "getaward":
				{
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var id)) return;

					var item = _config.Awards.Find(x => x.ID == id);
					if (item == null) return;

					var data = GetPlayerData(player);
					if (data == null || data.ReceivedAwards.Contains(id)) return;

					if (!CanGetAward(player, out var errorMsg))
					{
						SendNotify(player, errorMsg, 1);
						return;
					}

					data.ReceivedAwards.Add(id);

					item.Get(player);

					SendNotify(player, ReceivedItem, 0, item.PublicTitle);

					UpdateUI(player,
						container => ShowAwardUI(player, ref container, item, data,
							data.InvitedPlayers.Count(CheckInvitedPlayer)));
					break;
				}
			}
		}

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
				SendNotify(player, NoPermission, 1);
				return;
			}

			MainUi(player, first: true);
		}

		private void CmdActivePromo(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!string.IsNullOrEmpty(_config.Permission) &&
			    !permission.UserHasPermission(player.UserIDString, _config.Permission))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
				cov.Reply(Msg(ErrorSyntax, cov.Id, command));
				return;
			}

			var data = GetPlayerData(player);
			if (data == null) return;

			if (data.Activated)
			{
				SendNotify(player, AlreadyActivated, 1);
				return;
			}

			if (_config.MinPlayTime > 0 && data.PlayTime < _config.MinPlayTime)
			{
				SendNotify(player, NotEnoughTime, 1,
					FormatShortTime(player, TimeSpan.FromSeconds(_config.MinPlayTime - data.PlayTime)));
				return;
			}

			var promoCode = string.Join(" ", args);
			if (string.IsNullOrEmpty(promoCode))
				return;

			if (_data.Players.All(x => x.Value.PromoCode != promoCode))
			{
				SendNotify(player, PromoCode, 1, promoCode);
				return;
			}

			var check = _data.Players.FirstOrDefault(x => x.Value.PromoCode == promoCode);
			if (check.Value.InvitedPlayers.Contains(player.userID)) return;

			if (check.Value.PromoCode == data.PromoCode)
			{
				SendNotify(player, CannotActivateSelfPromo, 1);
				return;
			}

			check.Value.InvitedPlayers.Add(player.userID);
			data.Activated = true;

			var target = BasePlayer.FindByID(check.Key);
			if (target != null)
				SendNotify(target, ActivatedPromoCode, 0, player.displayName);

			SendNotify(player, ActivatedSelfPromoCode, 0, promoCode);

			if (_config.GiveSelfAward)
				GetSelfAward()?.Get(player);

			Interface.CallHook("OnPromoCodeActivated", player, promoCode);
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, int page = 0, int rPage = 0, bool first = false)
		{
			var data = GetPlayerData(player);
			if (data == null) return;

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
						Close = Layer,
						Command = "UI_Referrals close"
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
					OffsetMin = "-360 -250", OffsetMax = "360 300"
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
					OffsetMin = "0 -45",
					OffsetMax = "0 0"
				},
				Image = {Color = _config.UI.Color2.Get}
			}, Layer + ".Main", Layer + ".Header", Layer + ".Header");

			#region Title

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

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-50 -35",
					OffsetMax = "-25 -10"
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
					Command = "UI_Referrals close"
				}
			}, Layer + ".Header");

			#endregion

			#endregion

			#region My PromoCode

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-305 -130",
					OffsetMax = "-85 -85"
				},
				Image =
				{
					Color = _config.UI.Color5.Get
				}
			}, Layer + ".Main", Layer + ".My.PromoCode");

			CreateOutLine(ref container, Layer + ".My.PromoCode", _config.UI.Color6.Get, 1);

			container.Add(new CuiElement
			{
				Parent = Layer + ".My.PromoCode",
				Components =
				{
					new CuiInputFieldComponent
					{
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1",
						Command = $"UI_Referrals page {page} {rPage}",
						CharsLimit = 150,
						Text = $"{data.PromoCode}",
						NeedsKeyboard = true,
						HudMenuInput = true
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "20 0", OffsetMax = "-20 0"
					}
				}
			});

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-305 -185",
					OffsetMax = "-45 -145"
				},
				Text =
				{
					Text = Msg(player, YourPromoDescription),
					Align = TextAnchor.UpperLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 0.3"
				}
			}, Layer + ".Main");

			#endregion

			#region Enter PromoCode

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "25 -130",
					OffsetMax = "245 -85"
				},
				Image =
				{
					Color = _config.UI.Color5.Get
				}
			}, Layer + ".Main", Layer + ".Enter.PromoCode");

			CreateOutLine(ref container, Layer + ".Enter.PromoCode", _config.UI.Color6.Get, 1);

			container.Add(new CuiElement
			{
				Parent = Layer + ".Enter.PromoCode",
				Components =
				{
					new CuiInputFieldComponent
					{
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1",
						Command = data.Activated ? "" : $"{_config.ActivePromoCommands[0]} ",
						CharsLimit = 150,
						NeedsKeyboard = true,
						HudMenuInput = true
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "20 0", OffsetMax = "-20 0"
					}
				}
			});

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "25 -185",
					OffsetMax = "285 -145"
				},
				Text =
				{
					Text = Msg(player, EnterPromoDescription),
					Align = TextAnchor.UpperLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 0.3"
				}
			}, Layer + ".Main");

			#endregion

			#region Invited Players

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-305 -500",
					OffsetMax = "-85 -205"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer + ".Main", Layer + ".InvitedPlayers");

			CreateOutLine(ref container, Layer + ".InvitedPlayers", _config.UI.Color6.Get, 1);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "20 -30", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, InvitedPlayersTitle),
					Align = TextAnchor.LowerLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 0.5"
				}
			}, Layer + ".InvitedPlayers");

			#region List

			var amountOnPage = 10;
			var ySwitch = -40f;
			var height = 20f;
			var margin = 5f;

			var invitedPlayers = data.InvitedPlayers.FindAll(CheckInvitedPlayer);
			var invitedPlayersCount = invitedPlayers.Count;
			var hasInvitedPages = invitedPlayersCount > amountOnPage;
			var invitedPlayersParent = Layer + ".InvitedPlayers";

			if (_config.UI.UseScrollForInvitedPlayers && hasInvitedPages)
			{
				var totalHeight = invitedPlayersCount * height + (invitedPlayersCount - 1) * margin;

				ySwitch = 0;

				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 20",
						OffsetMax = "-20 -40"
					},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, Layer + ".InvitedPlayers", Layer + ".InvitedPlayers.Panel");

				container.Add(new CuiElement()
				{
					Parent = Layer + ".InvitedPlayers.Panel",
					Name = Layer + ".InvitedPlayers.Scroll",
					DestroyUi = Layer + ".InvitedPlayers.Scroll",
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
								Size = 5f, AutoHide = true,
								HighlightColor = new IColor("#FFFFFF", 100).Get,
								HandleColor = new IColor("#FFFFFF", 100).Get,
								PressedColor = new IColor("#FFFFFF", 50).Get,
								TrackColor = new IColor("#C4C4C4", 10).Get
							},
						}
					}
				});

				invitedPlayersParent = Layer + ".InvitedPlayers.Scroll";
			}
			else
				invitedPlayers = invitedPlayers.Skip(page * amountOnPage).Take(amountOnPage).ToList();

			foreach (var member in invitedPlayers)
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"20 {ySwitch - height}",
							OffsetMax = $"0 {ySwitch}"
						},
						Image = {Color = "0 0 0 0"}
					}, invitedPlayersParent,
					Layer + $".InvitedPlayers.Player.{member}",
					Layer + $".InvitedPlayers.Player.{member}");

				container.Add(new CuiElement
				{
					Parent = Layer + $".InvitedPlayers.Player.{member}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage($"avatar_{member}")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "0 0",
							OffsetMin = "0 0", OffsetMax = "20 20"
						}
					}
				});

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "45 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = GetPlayerData(member)?.DisplayName ?? "UNKNOWN",
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						}
					}, Layer + $".InvitedPlayers.Player.{member}");

				ySwitch = ySwitch - height - margin;
			}

			#endregion

			#region Pages

			if (!_config.UI.UseScrollForInvitedPlayers && hasInvitedPages)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-40 30",
						OffsetMax = "-20 50"
					},
					Text =
					{
						Text = Msg(player, NextBtn),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get,
						Command = invitedPlayersCount > (page + 1) * amountOnPage
							? $"UI_Referrals page {page + 1}"
							: ""
					}
				}, Layer + ".InvitedPlayers");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-40 55",
						OffsetMax = "-20 75"
					},
					Text =
					{
						Text = Msg(player, BackBtn),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color7.Get,
						Command = page != 0 ? $"UI_Referrals page {page - 1}" : ""
					}
				}, Layer + ".InvitedPlayers");
			}

			#endregion

			#endregion

			#region Awards

			amountOnPage = 3;

			ySwitch = -205;
			height = 90;
			margin = 10;

			var awards = GetAwards(player);
			var awardsCount = awards.Count;

			var awardsParent = Layer + ".Main";
			if (_config.UI.UseScrollForAwards)
			{
				var totalHeight = awardsCount * height + (awardsCount - 1) * margin;

				totalHeight += 20f;

				ySwitch = -10;

				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"25 -495",
						OffsetMax = $"315 -205"
					},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, Layer + ".Main", Layer + ".Awards.Panel");

				container.Add(new CuiElement()
				{
					Parent = Layer + ".Awards.Panel",
					Name = Layer + ".Awards.Scroll",
					DestroyUi = Layer + ".Awards.Scroll",
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
								Size = 5f, AutoHide = true,
								HighlightColor = new IColor("#FFFFFF", 100).Get,
								HandleColor = new IColor("#FFFFFF", 100).Get,
								PressedColor = new IColor("#FFFFFF", 50).Get,
								TrackColor = new IColor("#C4C4C4", 10).Get
							},
						}
					}
				});

				awardsParent = Layer + ".Awards.Scroll";
			}
			else
			{
				awards = awards.Skip(rPage * amountOnPage).Take(amountOnPage).ToList();
			}

			foreach (var award in awards)
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.UseScrollForAwards ? "0 1" : "0.5 1",
							AnchorMax = _config.UI.UseScrollForAwards ? "0 1" : "0.5 1",
							OffsetMin = $"{(_config.UI.UseScrollForAwards ? 0 : 25)} {ySwitch - height}",
							OffsetMax = $"{(_config.UI.UseScrollForAwards ? 280 : 305)} {ySwitch}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, awardsParent, Layer + $".Award.{award.ID}.Background", Layer + $".Award.{award.ID}.Background");

				ShowAwardUI(player, ref container, award, data, invitedPlayersCount);

				ySwitch = ySwitch - height - margin;
			}

			#region Pages

			if (!_config.UI.UseScrollForAwards)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-50 55",
						OffsetMax = "-30 75"
					},
					Text =
					{
						Text = Msg(player, NextBtn),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get,
						Command = awardsCount > (rPage + 1) * amountOnPage
							? $"UI_Referrals page {page} {rPage + 1}"
							: ""
					}
				}, Layer + ".Main");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-50 80",
						OffsetMax = "-30 100"
					},
					Text =
					{
						Text = Msg(player, BackBtn),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color7.Get,
						Command = rPage != 0 ? $"UI_Referrals page {page} {rPage - 1}" : ""
					}
				}, Layer + ".Main");
			}

			#endregion

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowAwardUI(BasePlayer player, ref CuiElementContainer container, Award award, PlayerData data,
			int invitedPlayersCount)
		{
			container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, Layer + $".Award.{award.ID}.Background", Layer + $".Award.{award.ID}", Layer + $".Award.{award.ID}");

			CreateOutLine(ref container, Layer + $".Award.{award.ID}",
				data.ReceivedAwards.Contains(award.ID) ? _config.UI.Color8.Get : _config.UI.Color6.Get,
				1);

			if (!string.IsNullOrEmpty(award.Image))
				container.Add(new CuiElement
				{
					Parent = Layer + $".Award.{award.ID}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(award.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "0 0",
							OffsetMin = "15 15", OffsetMax = "75 75"
						}
					}
				});
			else if (award.Definition != null)
				container.Add(new CuiElement
				{
					Parent = Layer + $".Award.{award.ID}",
					Components =
					{
						new CuiImageComponent
						{
							ItemId = award.Definition.itemid,
							SkinId = award.Skin
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "0 0",
							OffsetMin = "15 15", OffsetMax = "75 75"
						}
					}
				});

			#region Name

			container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "1 1",
						OffsetMin = "90 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, ItemNameTitle),
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 8,
						Color = "1 1 1 0.5"
					}
				}, Layer + $".Award.{award.ID}");

			container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0.5",
						OffsetMin = "90 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{award.PublicTitle}",
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					}
				}, Layer + $".Award.{award.ID}");

			#endregion

			#region Invited

			container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "1 1",
						OffsetMin = "175 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, InvitedAmountTitle),
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 8,
						Color = "1 1 1 0.5"
					}
				}, Layer + $".Award.{award.ID}");

			container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0.5",
						OffsetMin = "175 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{Mathf.Min(invitedPlayersCount, award.InvitesAmount)}/{award.InvitesAmount}",
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					}
				}, Layer + $".Award.{award.ID}");

			#endregion

			#region Button

			if (data.ReceivedAwards.Contains(award.ID))
				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-40 -15",
							OffsetMax = "-10 15"
						},
						Text =
						{
							Text = Msg(player, ReceivedItemIcon),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Color8.Get
						}
					}, Layer + $".Award.{award.ID}");
			else if (invitedPlayersCount >= award.InvitesAmount)
				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-40 -15",
							OffsetMax = "-10 15"
						},
						Text =
						{
							Text = Msg(player, ReceiveItemIcon),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Color4.Get,
							Command = $"UI_Referrals getaward {award.ID}"
						}
					}, Layer + $".Award.{award.ID}");

			#endregion

			#region Info

			if (award.ShowDescription)
				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-18 -18", OffsetMax = "-5 -5"
						},
						Text = {Text = ""},
						Button =
						{
							Sprite = "assets/icons/warning.png",
							Color = _config.UI.Color6.Get,
							Command = $"UI_Referrals infoaward {award.ID}"
						}
					}, Layer + $".Award.{award.ID}");

			#endregion
		}

		private void ShowDescription(BasePlayer player, Award award)
		{
			CuiHelper.AddUi(player, new CuiElementContainer
			{
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-150 -110", OffsetMax = "150 0"
						},
						Text =
						{
							Text = $"{award.GetDescription()}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					},
					Layer + ".Main", Layer + ".Notice", Layer + ".Notice"
				}
			});
		}

		private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback)
		{
			if (player == null) return;

			var container = new CuiElementContainer();

			callback?.Invoke(container);

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

		#region Blocks

		private bool CanGetAward(BasePlayer player, out string msg)
		{
			msg = null;

			if (_config.RewardBlocks.UseBlockRewardsAfterWipe)
			{
				var timeLeft = Mathf.RoundToInt(_config.RewardBlocks.BlockAfterWipeTime - GetSecondsFromWipe());
				if (timeLeft > 0)
				{
					msg = Msg(player, MsgBlockAfterWipeCooldown, FormatShortTime(timeLeft));
					return false;
				}
			}

			if (_config.RewardBlocks.UseBlockRewardsDuringCombatBlock && NE_IsCombatBlocked(player))
			{
				msg = MsgBlockCombat;
				return false;
			}

			if (_config.RewardBlocks.UseBlockRewardsDuringRaidBlock && NE_IsRaidBlocked(player))
			{
				msg = MsgBlockRaid;
				return false;
			}

			return true;
		}

		private int GetSecondsFromWipe()
		{
			return (int) DateTime.UtcNow
				.Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalSeconds;
		}

		private static string FormatShortTime(int seconds)
		{
			return TimeSpan.FromSeconds(seconds).ToShortString();
		}

		private bool NE_IsCombatBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player));
		}

		private bool NE_IsRaidBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player));
		}

		#endregion

		private SelfAward GetSelfAward()
		{
			SelfAward item = null;
			var iteration = 0;
			do
			{
				iteration++;

				var randomItem = _config.SelfAwards.GetRandom();
				if (randomItem.Chance < 1 || randomItem.Chance > 100)
					continue;

				if (Random.Range(0f, 100f) <= randomItem.Chance)
					item = randomItem;
			} while (item == null && iteration < 1000);

			return item;
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

		private List<Award> GetAwards(BasePlayer player)
		{
			var result = new List<Award>();

			var data = GetPlayerData(player);

			var receivedAwards = _config.Awards.FindLast(x => data.ReceivedAwards.Contains(x.ID));
			if (receivedAwards != null)
				result.Add(receivedAwards);

			result.AddRange(_config.Awards.FindAll(x => !data.ReceivedAwards.Contains(x.ID))
				.OrderBy(x => x.InvitesAmount).ThenByDescending(x => data.InvitedPlayers.Count >= x.InvitesAmount));
			return result;
		}

		private string GetRandomPromoCode()
		{
			string promo;
			do
			{
				promo = new string(
					Enumerable.Repeat(_config.PromoCodeChars, _config.PromoCodeLength)
						.Select(s => s[Random.Range(0, s.Length)])
						.ToArray());
			} while (_data.Players.Any(x => x.Value.PromoCode == promo));

			return promo;
		}

		private void DataHandle()
		{
			foreach (var check in _data.Players)
				check.Value.PlayTime++;
		}

		private bool CheckInvitedPlayer(ulong target)
		{
			var data = GetPlayerData(target);
			if (data == null) return false;

			return _config.MinPlayTime <= 0 || data.PlayTime >= _config.MinPlayTime;
		}

		private string FormatShortTime(BasePlayer player, TimeSpan time)
		{
			var list = Pool.GetList<string>();

			if (time.Days != 0)
				list.Add(Msg(player, DaysFormat, time.Days));

			if (time.Hours != 0)
				list.Add(Msg(player, HoursFormat, time.Hours));

			if (time.Minutes != 0)
				list.Add(Msg(player, MinutesFormat, time.Minutes));

			if (time.Seconds != 0)
				list.Add(Msg(player, SecondsFormat, time.Seconds));

			var result = string.Join(" ", list);
			Pool.FreeList(ref list);
			return result;
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

			var imagesList = new Dictionary<string, string>();

			_config.Awards.ForEach(item =>
			{
				if (!string.IsNullOrEmpty(item.Image))
					imagesList.TryAdd(item.Image, item.Image);
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

		#endregion

		#endregion

		#region API

		private string GetPromoCode(BasePlayer member)
		{
			return GetPromoCode(member.userID);
		}

		private string GetPromoCode(string member)
		{
			return GetPromoCode(ulong.Parse(member));
		}

		private string GetPromoCode(ulong member)
		{
			return GetPlayerData(member)?.PromoCode;
		}

		#endregion

		#region Lang

		private const string
			MsgBlockAfterWipeCooldown = "MsgBlockAfterWipeCooldown",
			MsgBlockCombat = "MsgBlockCombat",
			MsgBlockRaid = "MsgBlockRaid",
			NoILError = "NoILError",
			DaysFormat = "DaysFormat",
			HoursFormat = "HoursFormat",
			MinutesFormat = "MinutesFormat",
			SecondsFormat = "SecondsFormat",
			CloseButton = "CloseButton",
			TitleMenu = "TitleMenu",
			PromoCode = "PromoCode",
			ActivatedPromoCode = "ActivatedPromoCode",
			ActivatedSelfPromoCode = "ActivatedSelfPromoCode",
			CannotActivateSelfPromo = "CannotActivateSelfPromo",
			AlreadyActivated = "AlreadyActivated",
			ReceivedItem = "ReceivedItem",
			InvitedPlayersTitle = "InvitedPlayersTitle",
			NextBtn = "NextBtn",
			BackBtn = "BackBtn",
			ItemNameTitle = "ItemNameTitle",
			InvitedAmountTitle = "InvitedAmountTitle",
			ReceivedItemIcon = "ReceivedItemIcon",
			ReceiveItemIcon = "ReceiveItemIcon",
			YourPromoDescription = "YourPromoDescription",
			EnterPromoDescription = "EnterPromoDescription",
			NoPermission = "NoPermission",
			ErrorSyntax = "ErrorSyntax",
			NotEnoughTime = "NotEnoughTime";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[CloseButton] = "✕",
				[TitleMenu] = "Referral System",
				[PromoCode] = "Promo code '{0}' not found",
				[ActivatedPromoCode] = "Player '{0}' has activated your promo code!",
				[ActivatedSelfPromoCode] = "You have activated the promo code '{0}'!",
				[CannotActivateSelfPromo] = "You cannot activate your promo code!",
				[AlreadyActivated] = "You have already activated the promo code!",
				[ReceivedItem] = "Congratulations! You received '{0}'",
				[InvitedPlayersTitle] = "Invited players:",
				[NextBtn] = "▼",
				[BackBtn] = "▲",
				[ItemNameTitle] = "Item name",
				[InvitedAmountTitle] = "Invited:",
				[ReceivedItemIcon] = "✔",
				[ReceiveItemIcon] = "＋",
				[YourPromoDescription] =
					"This is your promo code. You can share it with other players, if they activate it, you will receive prizes.",
				[EnterPromoDescription] =
					"Enter the promo code of the player who invited you, in return he will receive a reward.",
				[NoPermission] = "You don't have permission to use this command!",
				[ErrorSyntax] = "Error syntax! Use: /{0} [promo code]",
				[NotEnoughTime] = "You need to play on the server for another {0}.",
				[DaysFormat] = " {0} d.",
				[HoursFormat] = " {0} h.",
				[MinutesFormat] = " {0} m.",
				[SecondsFormat] = " {0} s.",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",

				[MsgBlockAfterWipeCooldown] = "You can't get award after wipe! Please wait {0}.",
				[MsgBlockCombat] = "You can't get award while in combat!",
				[MsgBlockRaid] = "You can't get award while in raid!"
			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				[CloseButton] = "✕",
				[TitleMenu] = "Реферальная Система",
				[PromoCode] = "Промокод '{0}' не найден",
				[ActivatedPromoCode] = "Игрок '{0}' активировал ваш промокод!",
				[ActivatedSelfPromoCode] = "Вы активировали промокод '{0}'!",
				[CannotActivateSelfPromo] = "Вы не можете активировать свой промокод!",
				[AlreadyActivated] = "Вы уже активировали промокод!",
				[ReceivedItem] = "Поздравляем! Вы получили '{0}'",
				[InvitedPlayersTitle] = "Приглашённые игроки:",
				[NextBtn] = "▼",
				[BackBtn] = "▲",
				[ItemNameTitle] = "Предмет",
				[InvitedAmountTitle] = "Приглашений:",
				[ReceivedItemIcon] = "✔",
				[ReceiveItemIcon] = "＋",
				[YourPromoDescription] =
					"Это ваш промокод. Вы можете поделиться им с другими игроками, если они активируют, вы получите призы.",
				[EnterPromoDescription] =
					"Введите промокод игрока, который вас пригласил, взамен он получит вознаграждение.",
				[NoPermission] = "У вас нет разрешения на использование этой команды!",
				[ErrorSyntax] = "Ошибка синтаксиса! Используйте: /{0} [promo code]",
				[NotEnoughTime] = "Вам необходимо наиграть ещё {0}.",
				[DaysFormat] = " {0} д.",
				[HoursFormat] = " {0} ч.",
				[MinutesFormat] = " {0} м.",
				[SecondsFormat] = " {0} с.",

				[MsgBlockAfterWipeCooldown] =
					"Вы не можете получить вознаграждение после вайпа! Пожалуйста, подождите  {0}.",
				[MsgBlockCombat] = "Вы не можете получить вознаграждение во время комбата!",
				[MsgBlockRaid] = "Вы не можете получить вознаграждение во время рейда!"
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
	}
}