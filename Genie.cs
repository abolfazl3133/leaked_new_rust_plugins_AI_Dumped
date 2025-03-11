using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Genie", "https://discord.gg/TrJ7jnS233", "1.0.5")]
	public class Genie : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			UINotify = null,
			Notify = null;

		private static Genie _instance;

		private bool _enabledImageLibrary;

		private const string Layer = "UI.Genie";

		private readonly Dictionary<BasePlayer, Item> ItemByPlayer = new Dictionary<BasePlayer, Item>();

		private enum ItemType
		{
			Item,
			Command,
			Plugin
		}

		private bool _initialized;

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Genie Image")]
			public string GenieImage = "https://i.imgur.com/aNIcQzk.png";

			[JsonProperty(PropertyName = "Enable opening progress?")]
			public bool Progress = true;

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Delay to receive")]
			public float Delay = 5;

			[JsonProperty(PropertyName = "Update Frequency")]
			public float UpdateFrequency = 0.1f;

			[JsonProperty(PropertyName = "Opening effect (empty - disable)")]
			public string OpeningEffect = "assets/bundled/prefabs/fx/gestures/lick.prefab";

			[JsonProperty(PropertyName = "Progress effect (empty - disable)")]
			public string ProgressEffect = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";

			[JsonProperty(PropertyName = "Finish effect (empty - disable)")]
			public string FinishEffect =
				"assets/prefabs/misc/xmas/presents/effects/wrap.prefab";

			[JsonProperty(PropertyName = "Permission to rub the lamp")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = "Genie lamp Settings")]
			public LampSettings Lamp = new LampSettings
			{
				ControlStack = true,
				DisplayName = "Lamp",
				ShortName = "xmas.present.small",
				Skin = 2540200362
			};

			[JsonProperty(PropertyName = "Awards", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Award> Awards = new List<Award>
			{
				new Award
				{
					Type = ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "wood",
					Skin = 0,
					Amount = 3500,
					Chance = 70,
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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
					}
				},
				new Award
				{
					Type = ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "stones",
					Skin = 0,
					Amount = 2500,
					Chance = 70,
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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
					}
				},
				new Award
				{
					Type = ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "leather",
					Skin = 0,
					Amount = 1000,
					Chance = 55,
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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
					}
				},
				new Award
				{
					Type = ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "cloth",
					Skin = 0,
					Amount = 1000,
					Chance = 55,
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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
					}
				},
				new Award
				{
					Type = ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "lowgradefuel",
					Skin = 0,
					Amount = 500,
					Chance = 50,
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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
					}
				},
				new Award
				{
					Type = ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "metal.fragments",
					Skin = 0,
					Amount = 1500,
					Chance = 65,
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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
					}
				},
				new Award
				{
					Type = ItemType.Item,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = "metal.refined",
					Skin = 0,
					Amount = 150,
					Chance = 65,
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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
					}
				}
			};

			[JsonProperty(PropertyName = "Drop Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<DropInfo> Drop = new List<DropInfo>
			{
				new DropInfo
				{
					PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
					DropChance = 50
				},
				new DropInfo
				{
					PrefabName = "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
					DropChance = 5
				},
				new DropInfo
				{
					PrefabName = "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
					DropChance = 5
				}
			};

			public VersionNumber Version;
		}

		public class DropInfo
		{
			[JsonProperty(PropertyName = "Prefab")]
			public string PrefabName;

			[JsonProperty(PropertyName = "Chance")]
			public int DropChance;
		}

		private class LampSettings
		{
			[JsonProperty(PropertyName =
				"Enable item stack control? (if there are errors with stack plugins - it is worth turning off)")]
			public bool ControlStack;

			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			public Item ToItem()
			{
				var newItem = ItemManager.CreateByName(ShortName, 1, Skin);
				if (newItem == null)
				{
					_instance?.PrintError($"Error creating item with ShortName '{ShortName}'");
					return null;
				}

				if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

				return newItem;
			}
		}

		private class Award
		{
			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ItemType Type;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Command (%steamid%)")]
			public string Command;

			[JsonProperty(PropertyName = "Plugin")]
			public PluginItem Plugin;

			[JsonProperty(PropertyName = "Display Name (empty - default)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Chance")]
			public float Chance;

			[JsonProperty(PropertyName = "Content")]
			public ItemContent Content;

			[JsonProperty(PropertyName = "Weapon")]
			public ItemWeapon Weapon;

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

			public int GetAmount()
			{
				switch (Type)
				{
					case ItemType.Plugin:
						return Plugin.Amount;
					default:
						return Amount;
				}
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
				}
			}

			private void ToItem(BasePlayer player, int count)
			{
				var def = ItemManager.FindItemDefinition(ShortName);
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

					if (Weapon != null && Weapon.Enabled)
						Weapon.Build(newItem);

					if (Content != null && Content.Enabled)
						Content.Build(newItem);

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
				var list = new List<int>();
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

		private class ItemContent
		{
			#region Fields

			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Contents", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ContentInfo> Contents = new List<ContentInfo>();

			#endregion

			#region Utils

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

				[JsonProperty(PropertyName = "Condition")]
				public float Condition;

				[JsonProperty(PropertyName = "Amount")]
				public int Amount;

				[JsonProperty(PropertyName = "Position")]
				public int Position = -1;

				#region Utils

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

		private class ItemWeapon
		{
			#region Fields

			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Ammo Type")]
			public string AmmoType;

			[JsonProperty(PropertyName = "Ammo Amount")]
			public int AmmoAmount;

			#endregion

			#region Utils

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

		private class PluginItem
		{
			[JsonProperty(PropertyName = "Hook")] public string Hook = "Withdraw";

			[JsonProperty(PropertyName = "Plugin name")]
			public string Plugin = "Economics";

			[JsonProperty(PropertyName = "Amount")]
			public int Amount = 1;

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

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();

				if (_config.Version < Version) UpdateConfigValues();

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

			if (_config.Version == default(VersionNumber) || _config.Version < new VersionNumber(1, 0, 4))
				_config.Awards.ForEach(item =>
				{
					item.Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new ItemContent.ContentInfo
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

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Hooks

		private void OnServerInitialized()
		{
			_instance = this;

			LoadImages();

			if (_config.Drop.Count == 0)
				Unsubscribe(nameof(OnLootSpawn));

			if (_config.Lamp.ControlStack == false)
				Unsubscribe(nameof(CanStackItem));

			if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
				permission.RegisterPermission(_config.Permission, this);

			AddCovalenceCommand("genie.give", nameof(CmdGive));

			_initialized = true;
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

			_instance = null;
			_config = null;
		}

		#region Item

		private object OnItemAction(Item item, string action, BasePlayer player)
		{
			if (item == null || string.IsNullOrEmpty(action) || item.info.shortname != _config.Lamp.ShortName ||
			    item.skin != _config.Lamp.Skin || player == null)
				return null;

			switch (action)
			{
				case "unwrap":
				{
					if (_enabledImageLibrary == false)
					{
						SendNotify(player, NoILError, 1);

						BroadcastILNotInstalled();
						return false;
					}

					ItemByPlayer[player] = item;

					MainUi(player, First: true);
					return true;
				}
				case "upgrade_item":
					return true;
				default:
					return null;
			}
		}

		#endregion

		#region Split

		private object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem targetItem)
		{
			if (droppedItem == null || targetItem == null) return null;

			var item = droppedItem.GetItem();
			if (item != null && item.skin == _config.Lamp.Skin)
				return false;

			item = targetItem.GetItem();
			if (item != null && item.skin == _config.Lamp.Skin)
				return false;

			return null;
		}

		private object CanStackItem(Item item, Item targetItem)
		{
			if (item == null || targetItem == null) return null;

			return item.info.shortname == targetItem.info.shortname &&
			       (item.skin == _config.Lamp.Skin || targetItem.skin == _config.Lamp.Skin) &&
			       item.skin == targetItem.skin
				? (object) false
				: null;
		}

		#endregion

		#region Loot

		private void OnLootSpawn(LootContainer container)
		{
			if (!_initialized || container == null) return;

			var dropInfo = _config.Drop.Find(x => x.PrefabName.Contains(container.PrefabName));
			if (dropInfo == null || Random.Range(0, 100) > dropInfo.DropChance) return;

			NextTick(() =>
			{
				if (container == null || container.inventory == null) return;

				if (container.inventory.capacity <= container.inventory.itemList.Count)
					container.inventory.capacity = container.inventory.itemList.Count + 1;

				_config.Lamp.ToItem()?.MoveToContainer(container.inventory);
			});
		}

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

		[ConsoleCommand("UI_Genie")]
		private void CmdConsoleGenie(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "close":
				{
					ItemByPlayer.Remove(player);
					break;
				}

				case "page":
				{
					int page;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page))
						return;

					MainUi(player, page);
					break;
				}

				case "open":
				{
					if (!ItemByPlayer.ContainsKey(player))
						return;

					if (!string.IsNullOrEmpty(_config.Permission) &&
					    !permission.UserHasPermission(player.UserIDString, _config.Permission))
					{
						SendNotify(player, NoPermission, 1);
						ItemByPlayer.Remove(player);
						return;
					}

					ItemByPlayer[player].Remove();
					ItemByPlayer.Remove(player);

					SendEffect(player, _config.OpeningEffect);

					if (_config.Progress)
					{
						player.gameObject.AddComponent<OpenCase>().StartOpen();
					}
					else
					{
						var award = GetRandomAward();
						if (award == null) return;

						SendEffect(player, _config.FinishEffect);

						GiveAward(player, award);

						AwardUi(player, award);
					}

					break;
				}
			}
		}

		private void CmdGive(IPlayer player, string command, string[] args)
		{
			if (!player.IsAdmin) return;

			if (args.Length == 0)
			{
				PrintError($"Error syntax! Use: /{command} [targetId]");
				return;
			}

			var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
			if (target == null)
			{
				PrintError($"Player '{args[0]}' not found!");
				return;
			}

			var item = _config.Lamp?.ToItem();
			if (item == null) return;

			target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, int page = 0, bool First = false)
		{
			if (!ItemByPlayer.ContainsKey(player)) return;

			var container = new CuiElementContainer();

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

				container.Add(new CuiElement
				{
					Parent = Layer,
					Components =
					{
						new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", _config.GenieImage)},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-370 -360", OffsetMax = "370 360"
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
						Command = "UI_Genie close"
					}
				}, Layer);

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "10 -220",
						OffsetMax = "280 180"
					},
					Image = {Color = "0 0 0 0"}
				}, Layer, Layer + ".Background");
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Background", Layer + ".Main");

			#region Button

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-90 -45",
					OffsetMax = "80 0"
				},
				Text =
				{
					Text = Msg(player, RubLamp),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = "1 1 1 0.95"
				},
				Button =
				{
					Color = HexToCuiColor("#927845"),
					Command = "UI_Genie open"
				}
			}, Layer + ".Main");

			#endregion

			#region Items

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-100 -85",
					OffsetMax = "100 -65"
				},
				Text =
				{
					Text = Msg(player, AwardsTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 16,
					Color = HexToCuiColor("#927845")
				}
			}, Layer + ".Main");

			#endregion

			#region Loop

			var itemsOnString = 3;
			var strings = 3;
			var totalAmount = itemsOnString * strings;

			var Width = 75f;
			var Height = 75f;
			var xMargin = 20f;
			var yMargin = 32.5f;

			var constSwitch = -(itemsOnString * Width + (itemsOnString - 1) * xMargin) / 2f;

			var xSwitch = constSwitch;
			var ySwitch = -90f;

			var i = 1;
			foreach (var item in _config.Awards.Skip(page * totalAmount).Take(totalAmount))
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
						Color = "0 0 0 0"
					}
				}, Layer + ".Main", Layer + $".Item.{i}");

				CreateOutLine(ref container, Layer + $".Item.{i}", HexToCuiColor("#927845"), 1);

				#region Image

				container.Add(new CuiElement
				{
					Parent = Layer + $".Item.{i}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = !string.IsNullOrEmpty(item.Image)
								? ImageLibrary.Call<string>("GetImage", item.Image)
								: ImageLibrary.Call<string>("GetImage", item.ShortName, item.Skin)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-18 -18", OffsetMax = "18 18"
						}
					}
				});

				#endregion

				#region Amount

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "1 0.5",
						OffsetMin = "0 -35", OffsetMax = "0 -15"
					},
					Text =
					{
						Text = Msg(player, AmountTitle, item.GetAmount()),
						Align = TextAnchor.MiddleCenter,
						FontSize = 10,
						Font = "robotocondensed-regular.ttf",
						Color = HexToCuiColor("#927845")
					}
				}, Layer + $".Item.{i}");

				#endregion

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0",
						OffsetMin = "0 -20",
						OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{item.PublicTitle}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = HexToCuiColor("#927845")
					}
				}, Layer + $".Item.{i}");

				#endregion

				if (i % itemsOnString == 0)
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

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0",
					AnchorMax = "1 0",
					OffsetMin = "5 47",
					OffsetMax = "25 67"
				},
				Text =
				{
					Text = Msg(player, BtnBack),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 0.95"
				},
				Button =
				{
					Color = HexToCuiColor("#927845"),
					Command = page != 0 ? $"UI_Genie page {page - 1}" : ""
				}
			}, Layer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0",
					AnchorMax = "1 0",
					OffsetMin = "5 25",
					OffsetMax = "25 45"
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
					Color = HexToCuiColor("#927845"),
					Command = _config.Awards.Count > (page + 1) * totalAmount ? $"UI_Genie page {page + 1}" : ""
				}
			}, Layer + ".Main");

			#endregion

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void OpenUi(BasePlayer player, double progress = 0)
		{
			var container = new CuiElementContainer();

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Background", Layer + ".Main");

			#endregion

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-127 30",
					OffsetMax = "122 50"
				},
				Text =
				{
					Text = Msg(player, OpeningTitle),
					Align = TextAnchor.MiddleCenter,
					FontSize = 14,
					Font = "robotocondensed-regular.ttf",
					Color = HexToCuiColor("#927845")
				}
			}, Layer + ".Main");

			#endregion

			#region Progress

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-127 20",
					OffsetMax = "122 25"
				},
				Image =
				{
					Color = HexToCuiColor("#3D3D3D", 40)
				}
			}, Layer + ".Main", Layer + ".Progress");

			if (progress > 0)
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progress} 0.95"},
					Image =
					{
						Color = HexToCuiColor("#B4965D")
					}
				}, Layer + ".Progress");

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void AwardUi(BasePlayer player, Award award)
		{
			if (player == null) return;

			var container = new CuiElementContainer();

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Background", Layer + ".Main");

			#endregion

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-100 160",
					OffsetMax = "100 180"
				},
				Text =
				{
					Text = Msg(player, CongratulationsTitle),
					Align = TextAnchor.MiddleCenter,
					FontSize = 16,
					Font = "robotocondensed-bold.ttf",
					Color = HexToCuiColor("#927845")
				}
			}, Layer + ".Main");

			#endregion

			#region Your Award

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-100 140",
					OffsetMax = "100 160"
				},
				Text =
				{
					Text = Msg(player, YourAward),
					Align = TextAnchor.MiddleCenter,
					FontSize = 14,
					Font = "robotocondensed-regular.ttf",
					Color = HexToCuiColor("#927845")
				}
			}, Layer + ".Main");

			#endregion

			#region Award

			if (award != null)
			{
				#region Image

				container.Add(new CuiElement
				{
					Parent = Layer + ".Main",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = !string.IsNullOrEmpty(award.Image)
								? ImageLibrary.Call<string>("GetImage", award.Image)
								: ImageLibrary.Call<string>("GetImage", award.ShortName, award.Skin)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-90 -60", OffsetMax = "90 120"
						}
					}
				});

				#endregion

				#region Name

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-100 -90",
						OffsetMax = "100 -70"
					},
					Text =
					{
						Text = $"{award.PublicTitle}",
						Align = TextAnchor.MiddleCenter,
						FontSize = 14,
						Font = "robotocondensed-regular.ttf",
						Color = HexToCuiColor("#927845")
					}
				}, Layer + ".Main");

				#endregion
			}

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-80 -155",
					OffsetMax = "80 -115"
				},
				Text =
				{
					Text = Msg(player, CloseTitle),
					Align = TextAnchor.MiddleCenter,
					FontSize = 14,
					Font = "robotocondensed-regular.ttf",
					Color = "1 1 1 0.95"
				},
				Button =
				{
					Color = HexToCuiColor("#927845"),
					Close = Layer
				}
			}, Layer + ".Main");

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Component

		private class OpenCase : FacepunchBehaviour
		{
			private BasePlayer _player;

			public bool Started;

			private float StartTime;

			private void Awake()
			{
				_player = GetComponent<BasePlayer>();
			}

			public void StartOpen()
			{
				Started = true;

				StartTime = 0;

				_instance.OpenUi(_player);

				Handle();
			}

			private void Handle()
			{
				CancelInvoke(Handle);

				if (!Started)
					return;

				StartTime += _config.UpdateFrequency;

				if (StartTime >= _config.Delay)
				{
					Finish();
					return;
				}

				_instance.OpenUi(_player, Math.Round(StartTime / _config.Delay, 3));

				SendEffect(_player, _config.ProgressEffect);

				Invoke(Handle, _config.UpdateFrequency);
			}

			public void Finish(bool unload = false)
			{
				Started = false;

				var award = GetRandomAward();

				SendEffect(_player, _config.FinishEffect);

				_instance.GiveAward(_player, award);

				_instance.AwardUi(_player, award);

				Kill();
			}

			private void OnDestroy()
			{
				CancelInvoke();

				Destroy(this);
			}

			public void Kill()
			{
				DestroyImmediate(this);
			}
		}

		#endregion

		#region Utils

		private static string HexToCuiColor(string HEX, float Alpha = 100)
		{
			if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

			var str = HEX.Trim('#');
			if (str.Length != 6) throw new Exception(HEX);
			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100f}";
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

		private static Award GetRandomAward()
		{
			Award item = null;

			var iteration = 0;
			do
			{
				iteration++;

				var randomItem = _config.Awards.GetRandom();
				if (randomItem.Chance < 1 || randomItem.Chance > 100)
					continue;

				if (Random.Range(0f, 100f) <= randomItem.Chance)
					item = randomItem;
			} while (item == null && iteration < 1000);

			return item;
		}

		private void GiveAward(BasePlayer player, Award award)
		{
			if (player == null || award == null) return;

			award.Get(player);
		}

		private static void SendEffect(BasePlayer player, string effect)
		{
			if (player == null || string.IsNullOrEmpty(effect)) return;

			EffectNetwork.Send(new Effect(effect, player, 0, new Vector3(), new Vector3()), player.Connection);
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

				imagesList.Add(_config.GenieImage, _config.GenieImage);

				_config.Awards.ForEach(item =>
				{
					if (!string.IsNullOrEmpty(item.Image))
						imagesList.TryAdd(item.Image, item.Image);

					itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.Skin));
				});

				if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		#endregion

		#region Lang

		private const string
			NoILError = "NoILError",
			NoPermission = "NoPermission",
			RubLamp = "RubLamp",
			AwardsTitle = "AwardsTitle",
			AmountTitle = "AmountTitle",
			BtnNext = "BtnNext",
			BtnBack = "BtnBack",
			OpeningTitle = "OpeningTitle",
			CongratulationsTitle = "CongratulationsTitle",
			YourAward = "YourAward",
			CloseTitle = "CloseTitle";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoPermission] = "You don't have permission to use this command!",
				[RubLamp] = "RUB THE LAMP",
				[AwardsTitle] = "Awards:",
				[AmountTitle] = "{0} pcs",
				[BtnBack] = "▲",
				[BtnNext] = "▼",
				[OpeningTitle] = "Opening in progress ...",
				[CongratulationsTitle] = "Congratulations!",
				[YourAward] = "Your award:",
				[CloseTitle] = "Close",
				[NoILError] = "The plugin does not work correctly, contact the administrator!"
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
	}
}