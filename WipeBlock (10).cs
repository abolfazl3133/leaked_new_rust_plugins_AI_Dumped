using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Wipe Block", "Mevent", "1.0.2")]
	internal class WipeBlock : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary, Notify;

		private const string Layer = "UI.WipeBlock";

		private const string ScreenLayer = "UI.WipeBlock.Screen";

		private const string IgnorePermission = "WipeBlock.ignore";

		private Timer UpdateTimer;

		private readonly List<ItemConf> ItemsAll = new List<ItemConf>();

		private readonly Dictionary<ItemConf, int> CooldownByItems = new Dictionary<ItemConf, int>();

		private readonly Dictionary<int, List<ItemConf>> ItemsByCooldown = new Dictionary<int, List<ItemConf>>();

		private readonly Dictionary<BasePlayer, List<ItemConf>>
			UpdatePlayers = new Dictionary<BasePlayer, List<ItemConf>>();

		private class ItemsData
		{
			public string Category;

			public List<ItemConf> Items;
		}

		private bool AnyBlocked;

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"block", "wipeblock"};

			[JsonProperty(PropertyName = "Show on screen?")]
			public bool ShowOnScreen = true;

			[JsonProperty(PropertyName = "Use auto-wipe?")]
			public bool AutoWipe = true;

			[JsonProperty(PropertyName = "Time Indent (seconds)")]
			public float Indent;

			[JsonProperty(PropertyName = "Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Category> Categories = new List<Category>
			{
				new Category
				{
					LangKey = "Weapons",
					Items = new Dictionary<int, List<ItemConf>>
					{
						[3600] = new List<ItemConf>
						{
							new ItemConf("pistol.revolver", 0),
							new ItemConf("shotgun.double", 0)
						},
						[7200] = new List<ItemConf>
						{
							new ItemConf("pistol.semiauto", 0),
							new ItemConf("pistol.python", 0),
							new ItemConf("shotgun.pump", 0)
						},
						[10800] = new List<ItemConf>
						{
							new ItemConf("pistol.m92", 0),
							new ItemConf("shotgun.spas12", 0),
							new ItemConf("rifle.semiauto", 0)
						},
						[14400] = new List<ItemConf>
						{
							new ItemConf("smg.2", 0),
							new ItemConf("smg.mp5", 0),
							new ItemConf("smg.thompson", 0),
							new ItemConf("rifle.m39", 0)
						},
						[21600] = new List<ItemConf>
						{
							new ItemConf("rifle.ak", 0),
							new ItemConf("rifle.lr300", 0),
							new ItemConf("rifle.bolt", 0),
							new ItemConf("rifle.l96", 0)
						},
						[86400] = new List<ItemConf>
						{
							new ItemConf("lmg.m249", 0)
						}
					}
				},
				new Category
				{
					LangKey = "Explosives",
					Items = new Dictionary<int, List<ItemConf>>
					{
						[14400] = new List<ItemConf>
						{
							new ItemConf("grenade.beancan", 0)
						},
						[64800] = new List<ItemConf>
						{
							new ItemConf("explosive.satchel", 0)
						},
						[86400] = new List<ItemConf>
						{
							new ItemConf("ammo.rifle.explosive", 0),
							new ItemConf("explosive.timed", 0),
							new ItemConf("ammo.grenadelauncher.he", 0),
							new ItemConf("ammo.rocket.basic", 0)
						}
					}
				},
				new Category
				{
					LangKey = "Attire",
					Items = new Dictionary<int, List<ItemConf>>
					{
						[10800] = new List<ItemConf>
						{
							new ItemConf("coffeecan.helmet", 0),
							new ItemConf("roadsign.jacket", 0),
							new ItemConf("roadsign.kilt", 0)
						},
						[14400] = new List<ItemConf>
						{
							new ItemConf("metal.facemask", 0),
							new ItemConf("metal.plate.torso", 0)
						},
						[21600] = new List<ItemConf>
						{
							new ItemConf("heavy.plate.helmet", 0),
							new ItemConf("heavy.plate.jacket", 0),
							new ItemConf("heavy.plate.pants", 0)
						},
						[86400] = new List<ItemConf>
						{
							new ItemConf("lmg.m249", 0)
						}
					}
				}
			};

			[JsonProperty(PropertyName = "Interface")]
			public UserInterface UI = new UserInterface
			{
				Width = 820,
				Height = 530,
				ItemHeight = 70,
				ItemWidth = 70,
				ItemXMargin = 7.5f,
				ItemYMargin = 10,
				ItemsOnString = 10,
				XSwitch = 25,
				Gradients = new List<string>
				{
					"#4B68FF",
					"#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B",
					"#FFD01B",
					"#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B",
					"#FFD01B",
					"#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B",
					"#FFD01B",
					"#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B",
					"#FFD01B",
					"#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B", "#FFD01B",
					"#FFD01B",
					"#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060",
					"#FF6060",
					"#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060",
					"#FF6060",
					"#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060",
					"#FF6060",
					"#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060",
					"#FF6060",
					"#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060", "#FF6060",
					"#FF6060"
				}
			};
		}

		private class UserInterface
		{
			[JsonProperty(PropertyName = "Width")] public float Width;

			[JsonProperty(PropertyName = "Height")]
			public float Height;

			[JsonProperty(PropertyName = "Item Height")]
			public float ItemHeight;

			[JsonProperty(PropertyName = "Item Width")]
			public float ItemWidth;

			[JsonProperty(PropertyName = "Item X Margin")]
			public float ItemXMargin;

			[JsonProperty(PropertyName = "Item Y Margin")]
			public float ItemYMargin;

			[JsonProperty(PropertyName = "Items On String")]
			public int ItemsOnString;

			[JsonProperty(PropertyName = "X Indent")]
			public float XSwitch;

			[JsonProperty(PropertyName = "Gradients", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Gradients;
		}

		private class Category
		{
			[JsonProperty(PropertyName = "Lang Key")]
			public string LangKey;

			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, List<ItemConf>> Items;
		}

		private class ItemConf
		{
			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			[JsonIgnore] public readonly string GUID = CuiHelper.GetGuid();

			public ItemConf(string shortName, ulong skin)
			{
				ShortName = shortName;
				Skin = skin;
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

		#region Hooks

		private void OnServerInitialized()
		{
			LoadImages();

			FillingItems();

			if (_config.UI.Gradients.Count < 101)
				PrintError("Gradients less than 101. Check the config!!!");

			if (!permission.PermissionExists(IgnorePermission))
				permission.RegisterPermission(IgnorePermission, this);

			AddCovalenceCommand(_config.Commands, nameof(CmdOpenBlock));

			AddCovalenceCommand("wb.indent", nameof(CmdChangeIndent));

			CheckActive();

			CheckPlayers();

			if (_config.ShowOnScreen)
				foreach (var player in BasePlayer.activePlayerList)
					OnScreenUi(player);
			else
				Unsubscribe(nameof(OnPlayerConnected));
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, ScreenLayer);
			}
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;

			OnScreenUi(player);
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if (player == null) return;

			UpdatePlayers.Remove(player);
		}

		private object CanWearItem(PlayerInventory inventory, Item item)
		{
			var player = inventory.GetComponent<BasePlayer>();
			if (!IsValid(player)) return null;

			var isBlocked = IsBlocked(item.info);
			if (isBlocked)
			{
				SendNotify(player, ItemLocked, 1);
				return false;
			}

			return null;
		}

		private object CanEquipItem(PlayerInventory inventory, Item item, int targetPos)
		{
			var player = inventory.GetComponent<BasePlayer>();
			if (!IsValid(player)) return null;

			var isBlocked = IsBlocked(item.info);
			if (isBlocked)
			{
				SendNotify(player, ItemLocked, 1);
				return false;
			}

			return null;
		}

		private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer)
		{
			if (inventory == null || item == null)
				return null;

			var player = inventory.GetComponent<BasePlayer>();
			if (!IsValid(player)) return null;

			var container = inventory.FindContainer(new ItemContainerId(targetContainer));

			if (container == null || container.entityOwner == null)
				return null;

			if (container.entityOwner is AutoTurret && IsBlocked(item.info.shortname, item.skin))
			{
				SendNotify(player, ItemLocked, 1);
				return true;
			}

			return null;
		}

		private object CanAcceptItem(ItemContainer container, Item item)
		{
			if (container == null || item == null || container.entityOwner == null)
				return null;

			if (container.entityOwner is AutoTurret)
			{
				var player = item.GetOwnerPlayer();
				if (!IsValid(player)) return null;

				if (IsBlocked(item.info.shortname, item.skin))
				{
					SendNotify(player, ItemLocked, 1);
					return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
				}
			}

			return null;
		}

		private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
{
    if (!IsValid(player)) return null;

    var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType);
    if (isBlocked)
    {
        var list = player.inventory.containerMain?.itemList
            .Where(item => item.info.itemid == projectile.primaryMagazine.ammoType.itemid)
            .ToList();

        list.AddRange(player.inventory.containerBelt?.itemList
            .Where(item => item.info.itemid == projectile.primaryMagazine.ammoType.itemid) ?? Enumerable.Empty<Item>());

        list.AddRange(player.inventory.containerWear?.itemList
            .Where(item => item.info.itemid == projectile.primaryMagazine.ammoType.itemid) ?? Enumerable.Empty<Item>());

        if (list.Count == 0)
        {
            var list2 = new List<Item>();
            player.inventory.FindAmmo(list2, projectile.primaryMagazine.definition.ammoTypes);
            if (list2.Count > 0) isBlocked = IsBlocked(list2[0].info);
        }

        if (isBlocked) SendNotify(player, AmmoLocked, 1);

        return !isBlocked;
    }

    return null;
}


		private object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
		{
			if (!IsValid(player)) return null;

			NextTick(() =>
			{
				if (IsBlocked(projectile.primaryMagazine.ammoType))
				{
					player.GiveItem(ItemManager.CreateByItemID(projectile.primaryMagazine.ammoType.itemid,
						projectile.primaryMagazine.contents));
					projectile.primaryMagazine.contents = 0;
					projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
					projectile.SendNetworkUpdate();
					player.SendNetworkUpdate();
				}
			});

			return null;
		}

		#endregion

		#region Commands

		private void CmdOpenBlock(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			MainUi(player, GetStartPage(), true);
		}

		private void CmdChangeIndent(IPlayer cov, string command, string[] args)
		{
			if (!cov.IsAdmin) return;

			int seconds;
			if (args.Length == 0 || !int.TryParse(args[0], out seconds))
			{
				cov.Reply($"Error syntax! Use: /{command} [seconds]");
				return;
			}

			_config.Indent += seconds;
			SaveConfig();

			cov.Reply($"Indent from wipe date changed on '{seconds}'!");

			CheckActive();
		}

		[ConsoleCommand("UI_WipeBlock")]
		private void CmdConsoleWipeBlock(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "close":
				{
					UpdatePlayers.Remove(player);
					break;
				}

				case "page":
				{
					int page;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

					MainUi(player, page);
					break;
				}
			}
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, int type = 0, bool First = false)
		{
			#region Fields

			var container = new CuiElementContainer();

			var ySwitch = -50f;
			var xSwitch = _config.UI.XSwitch;

			#endregion

			#region Background

			if (First)
			{
				CuiHelper.DestroyUi(player, Layer);

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
						Command = "UI_WipeBlock close"
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
					OffsetMin = $"-{_config.UI.Width / 2f} -{_config.UI.Height / 2f}",
					OffsetMax = $"{_config.UI.Width / 2f} {_config.UI.Height / 2f}"
				},
				Image =
				{
					Color = HexToCuiColor("#0E0E10")
				}
			}, Layer, Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -45",
					OffsetMax = "0 0"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".Main", Layer + ".Header");

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
					Color = HexToCuiColor("#FFFFFF")
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
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Close = Layer,
					Command = "UI_WipeBlock close",
					Color = HexToCuiColor("#ff4b4b")
				}
			}, Layer + ".Header");

			#endregion

			#region Blocked Items

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-275 -35",
					OffsetMax = "-205 -10"
				},
				Text =
				{
					Text = Msg(player, BlockedItems),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = HexToCuiColor("#ff4b4b", type == 0 ? 100 : 65),
					Command = "UI_WipeBlock page 0"
				}
			}, Layer + ".Header");

			#endregion

			#region Unlocked Items

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-200 -35",
					OffsetMax = "-130 -10"
				},
				Text =
				{
					Text = Msg(player, UnlokedItems),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = HexToCuiColor("#ff4b4b", type == 1 ? 100 : 65),
					Command = "UI_WipeBlock page 1"
				}
			}, Layer + ".Header");

			#endregion

			#region All Items

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-125 -35",
					OffsetMax = "-55 -10"
				},
				Text =
				{
					Text = Msg(player, AllItems),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = HexToCuiColor("#ff4b4b", type == 2 ? 100 : 65),
					Command = "UI_WipeBlock page 2"
				}
			}, Layer + ".Header");

			#endregion

			#endregion

			#region Items

			var items = GetItems(type);
			if (items.Count == 0)
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 25", OffsetMax = "0 -85"
					},
					Text =
					{
						Text = Msg(player,
							type == 0 ? TitleItemsUnlocked : type == 1 ? TitleItemsLocked : TitleItemsMissing),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 20,
						Color = "1 1 1 0.45"
					}
				}, Layer + ".Main");
			else
				items.ForEach(category =>
				{
					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{xSwitch} {ySwitch - 25}",
							OffsetMax = $"{xSwitch + 150} {ySwitch}"
						},
						Text =
						{
							Text = $"{Msg(player, category.Category)}",
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + ".Main");

					ySwitch -= 25;

					var i = 1;
					category.Items.ForEach(item =>
					{
						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch} {ySwitch - _config.UI.ItemHeight}",
								OffsetMax = $"{xSwitch + _config.UI.ItemWidth} {ySwitch}"
							},
							Image =
							{
								Color = HexToCuiColor("#161617")
							}
						}, Layer + ".Main", Layer + $".Item.{item.GUID}");

						container.Add(new CuiElement
						{
							Parent = Layer + $".Item.{item.GUID}",
							Components =
							{
								new CuiRawImageComponent
									{Png = ImageLibrary.Call<string>("GetImage", item.ShortName, item.Skin)},
								new CuiRectTransformComponent
								{
									AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
									OffsetMin = "-25 -25", OffsetMax = "25 25"
								}
							}
						});

						ItemUi(player, ref container, item);

						if (i % _config.UI.ItemsOnString == 0 && i != category.Items.Count)
						{
							xSwitch = _config.UI.XSwitch;
							ySwitch = ySwitch - _config.UI.ItemHeight - _config.UI.ItemYMargin;
						}
						else
						{
							xSwitch += _config.UI.ItemXMargin + _config.UI.ItemWidth;
						}

						i++;
					});

					xSwitch = _config.UI.XSwitch;
					ySwitch = ySwitch - _config.UI.ItemHeight - _config.UI.ItemYMargin;
				});

			if (type == 0 || type == 2)
				UpdatePlayers[player] = items.SelectMany(x => x.Items).ToList();
			else
				UpdatePlayers.Remove(player);

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void ItemUi(BasePlayer player, ref CuiElementContainer container, ItemConf item)
		{
			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = IsBlocked(item)
						? $"<color=#A0A0A0>LEFT:</color>\n<b>{TimeSpan.FromSeconds(LeftTime(item)).ToShortString()}</b>"
						: "AVAILABLE",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 9,
					Color = "1 1 1 1"
				}
			}, Layer + $".Item.{item.GUID}", Layer + $".Item.{item.GUID}.Title");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 0", OffsetMax = "0 2"
				},
				Image =
				{
					Color = HexToCuiColor(GetGradient(item))
				}
			}, Layer + $".Item.{item.GUID}", Layer + $".Item.{item.GUID}.Color");

			CuiHelper.DestroyUi(player, Layer + $".Item.{item.GUID}.Title");
			CuiHelper.DestroyUi(player, Layer + $".Item.{item.GUID}.Color");
		}

		private void OnScreenUi(BasePlayer player)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-140 -40",
					OffsetMax = "-20 -10"
				},
				Image = {Color = "0 0 0 0"}
			}, "Hud", ScreenLayer);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 1"
				},
				Text =
				{
					Text = Msg(player, OnScreenTitle),
					Align = TextAnchor.LowerCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				}
			}, ScreenLayer);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0.5"
				},
				Text =
				{
					Text = Msg(player, OnScreenDescription),
					Align = TextAnchor.UpperCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 0.5"
				}
			}, ScreenLayer);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Command = _config.Commands[0]
				}
			}, ScreenLayer);

			CuiHelper.DestroyUi(player, ScreenLayer);
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

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

		private void LoadImages()
		{
			timer.In(5, () =>
			{
				if (!ImageLibrary)
				{
					PrintWarning("IMAGE LIBRARY IS NOT FOUND!");
				}
				else
				{
					var itemIcons = new List<KeyValuePair<string, ulong>>();

					_config.Categories.ForEach(category =>
					{
						foreach (var itemConfs in category.Items.Values)
							itemConfs.ForEach(item =>
								itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.Skin)));
					});


					if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);
				}
			});
		}

		private void FillingItems()
		{
			_config.Categories.ForEach(category =>
			{
				foreach (var check in category.Items)
					check.Value.ForEach(item =>
					{
						CooldownByItems[item] = check.Key;
						ItemsAll.Add(item);

						if (!ItemsByCooldown.ContainsKey(check.Key))
							ItemsByCooldown.Add(check.Key, new List<ItemConf>());

						if (!ItemsByCooldown[check.Key].Contains(item))
							ItemsByCooldown[check.Key].Add(item);
					});
			});
		}

		private List<ItemsData> GetItems(int type)
		{
			var list = new List<ItemsData>();

			_config.Categories.ForEach(category =>
			{
				var data = new ItemsData
				{
					Category = category.LangKey,
					Items = new List<ItemConf>()
				};

				foreach (var items in category.Items.Values)
					items.ForEach(item =>
					{
						switch (type)
						{
							case 0:
							{
								if (IsBlocked(item))
									data.Items.Add(item);
								break;
							}
							case 1:
							{
								if (!IsBlocked(item))
									data.Items.Add(item);
								break;
							}
							case 2:
							{
								data.Items.Add(item);
								break;
							}
						}
					});

				if (data.Items.Count > 0)
					list.Add(data);
			});

			return list;
		}

		private void UpdateController()
		{
			var keysToRemove = Pool.GetList<BasePlayer>();

			foreach (var check in UpdatePlayers)
			{
				var toRemove = Pool.GetList<ItemConf>();

				var container = new CuiElementContainer();

				check.Value.ForEach(item =>
				{
					ItemUi(check.Key, ref container, item);

					if (!IsBlocked(item))
						toRemove.Add(item);
				});

				CuiHelper.AddUi(check.Key, container);

				toRemove.ForEach(item => check.Value.Remove(item));
				Pool.FreeList(ref toRemove);

				if (check.Value.Count == 0) keysToRemove.Add(check.Key);
			}

			keysToRemove.ForEach(x => UpdatePlayers.Remove(x));
			Pool.FreeList(ref keysToRemove);
		}

		private bool IsValid(BasePlayer player)
		{
			return player != null && player.userID.IsSteamId() &&
			       !permission.UserHasPermission(player.UserIDString, IgnorePermission);
		}

		private void CheckActive()
		{
			UpdateTimer?.Destroy();

			if (ItemsAll.Exists(IsBlocked))
			{
				AnyBlocked = true;

				SubscribeHooks(true);

				UpdateTimer = timer.Every(1, UpdateController);
			}
			else
			{
				AnyBlocked = false;

				SubscribeHooks(false);
			}
		}

		private void SubscribeHooks(bool subscribe)
		{
			if (subscribe)
			{
				Subscribe(nameof(CanWearItem));
				Subscribe(nameof(CanEquipItem));
				Subscribe(nameof(OnReloadWeapon));
				Subscribe(nameof(OnReloadMagazine));
				Subscribe(nameof(CanAcceptItem));
				Subscribe(nameof(CanMoveItem));
			}
			else
			{
				Unsubscribe(nameof(CanWearItem));
				Unsubscribe(nameof(CanEquipItem));
				Unsubscribe(nameof(OnReloadWeapon));
				Unsubscribe(nameof(OnReloadMagazine));
				Unsubscribe(nameof(CanAcceptItem));
				Unsubscribe(nameof(CanMoveItem));
			}
		}

		private int GetStartPage()
		{
			return AnyBlocked ? 0 : 1;
		}

		private void CheckPlayers()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				player.inventory.containerBelt.itemList.ToList().ForEach(item => CheckBlockedItem(player, item));
				player.inventory.containerWear.itemList.ToList().ForEach(item => CheckBlockedItem(player, item));
			}
		}

		private void CheckBlockedItem(BasePlayer player, Item item)
		{
			if (!IsBlocked(item.info)) return;

			SendNotify(player, ItemLocked, 1);

			if (item.MoveToContainer(player.inventory.containerMain))
				player.Command("note.inv", item.info.itemid, item.amount,
					!string.IsNullOrEmpty(item.name) ? item.name : string.Empty,
					(int) BaseEntity.GiveItemReason.PickedUp);
			else
				item.Drop(player.inventory.containerMain.dropPosition,
					player.inventory.containerMain.dropVelocity);
		}

		#endregion

		#region API

		private int SecondsFromWipe()
		{
			return (int) DateTime.Now.ToUniversalTime()
				.Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime().AddSeconds(_config.Indent)).TotalSeconds;
		}

		private bool IsBlocked(ItemDefinition def)
		{
			return IsBlocked(def.shortname, 0);
		}

		private bool IsBlocked(string shortName, ulong skin)
		{
			var item = ItemsAll.Find(x => x.ShortName == shortName && (skin == 0 || x.Skin == skin));
			return item != null && IsBlocked(item);
		}

		private bool IsBlocked(ItemConf item)
		{
			return CooldownByItems.ContainsKey(item) && IsBlocked(CooldownByItems[item]);
		}

		private bool IsBlocked(int cooldown)
		{
			return SecondsFromWipe() < cooldown;
		}

		private int LeftTime(ItemConf item)
		{
			return LeftTime(CooldownByItems[item]);
		}

		private int LeftTime(int cooldown)
		{
			var seconds = cooldown - SecondsFromWipe();
			return seconds < 0 ? 0 : seconds;
		}

		private string GetGradient(ItemConf item)
		{
			var percent = Mathf.CeilToInt((float) LeftTime(item) / CooldownByItems[item] * 100f);
			return percent >= 0 && _config.UI.Gradients.Count > percent
				? _config.UI.Gradients[percent]
				: _config.UI.Gradients.LastOrDefault();
		}

		#endregion

		#region Lang

		private const string
			CloseButton = "CloseButton",
			TitleMenu = "TitleMenu",
			AllItems = "AllItems",
			BlockedItems = "BlockItems",
			UnlokedItems = "UnlockedItems",
			TitleItemsUnlocked = "TitleItemsUnlocked",
			TitleItemsLocked = "TitleItemsLocked",
			TitleItemsMissing = "TitleItemsMissing",
			ItemLocked = "ItemLocked",
			AmmoLocked = "AmmoLocked",
			OnScreenTitle = "OnScreenTitle",
			OnScreenDescription = "OnScreenDescription";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[CloseButton] = "✕",
				[TitleMenu] = "Wipe-Block",
				[AllItems] = "Все",
				[BlockedItems] = "заблокированы",
				[UnlokedItems] = "Раблокированы",
				[TitleItemsUnlocked] = "Все предметы разблокированы!",
				[TitleItemsLocked] = "Все предметы заблокированы :(",
				[TitleItemsMissing] = "Предметов не хватает :(",
				[ItemLocked] = "Предмет, который вы хотите взять, заблокирован",
				[AmmoLocked] = "Вы не можете использовать этот тип боеприпасов!",
				[OnScreenTitle] = "WIPE-BLOCK",
				[OnScreenDescription] = "Открыть",
				["Weapons"] = "Оружие",
				["Explosives"] = "Взрывчатые вещества",
				["Attire"] = "Боеприпасы"
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
			if (Notify)
				Notify?.Call("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion
	}
}