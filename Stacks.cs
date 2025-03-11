//#define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.StacksExtensionMethods;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Stacks", "Mevent", "1.5.14")]
	public class Stacks : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			FurnaceSplitter = null,
			Notify = null,
			UINotify = null,
			LangAPI = null;

		private static Stacks _instance;

		private const string Layer = "UI.Stacks";

		private const string AdminPerm = "stacks.admin";

		private readonly Dictionary<ulong, float> _multiplierByItem = new Dictionary<ulong, float>();

		private readonly List<CategoryInfo> _categories = new List<CategoryInfo>();

		private class CategoryInfo
		{
			public string Title;

			public List<ItemInfo> Items;
		}

		private readonly Dictionary<string, int> _defaultItemStackSize = new Dictionary<string, int>();

		private bool _hasFurnaceSplitter;

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly string[] Commands = {"stacks"};

			[JsonProperty(PropertyName = "Work with Notify?")]
			public readonly bool UseNotify = true;

			[JsonProperty(PropertyName = "Work with LangAPI?")]
			public readonly bool UseLangAPI = true;

			[JsonProperty(PropertyName = "Changing multiplies in containers using a hammer")]
			public bool UserHammer = false;

			[JsonProperty(PropertyName = "Default Multiplier for new containers")]
			public readonly float DefaultContainerMultiplier = 1f;

			[JsonProperty(PropertyName = "Blocked List")]
			public readonly BlockList BlockList = new BlockList
			{
				Items = new List<string>
				{
					"item",
					"short name"
				},
				Skins = new List<ulong>
				{
					111111111111,
					222222222222
				}
			};
		}

		private class BlockList
		{
			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Items;

			[JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ulong> Skins;

			public bool Exists(Item item)
			{
				return Items.Contains(item.info.shortname) || Skins.Contains(item.skin);
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

		#endregion

		#region Data

		private List<ItemInfo> _items;

		private Dictionary<string, ContainerData> _containers;

		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Containers", _containers);

			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Items", _items);
		}

		private void LoadData()
		{
			try
			{
				_containers =
					Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ContainerData>>($"{Name}/Containers");

				_items = Interface.Oxide.DataFileSystem.ReadObject<List<ItemInfo>>($"{Name}/Items");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_containers == null) _containers = new Dictionary<string, ContainerData>();
			if (_items == null) _items = new List<ItemInfo>();
		}

		private class ContainerData
		{
			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Multiplier")] [DefaultValue(1f)]
			public float Multiplier;

			[JsonIgnore] private int _id = -1;

			[JsonIgnore]
			public int ID
			{
				get
				{
					while (_id == -1)
					{
						var id = Random.Range(0, int.MaxValue);
						if (_instance._containerBase.ContainsKey(id)) continue;

						_id = id;
						break;
					}

					return _id;
				}
			}

			[JsonIgnore] public string Prefab;

			[JsonIgnore] private CuiRawImageComponent _image;

			public CuiElement GetImage(string aMin, string aMax, string oMin, string oMax, string parent,
				string name = null)
			{
				if (_image == null)
					_image = new CuiRawImageComponent
					{
						Png = _instance.ImageLibrary.Call<string>("GetImage",
							string.IsNullOrEmpty(Image) ? "NONE" : Image)
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

		private class ItemInfo
		{
			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Name")] public string Name;

			[JsonIgnore]
			public int DefaultStackSize
			{
				get
				{
					int stack;
					return _instance._defaultStackByID.TryGetValue(itemId, out stack) ? stack : 0;
				}
			}

			[JsonProperty(PropertyName = "Custom Stack Size")]
			public int CustomStackSize;

			[JsonIgnore] public int ID;

			public string GetItemDisplayName(BasePlayer player)
			{
				return _instance._config.UseLangAPI && _instance.LangAPI != null &&
				       _instance.LangAPI.Call<bool>("IsDefaultDisplayName", Name)
					? _instance.LangAPI.Call<string>("GetItemDisplayName", ShortName, Name, player.UserIDString) ?? Name
					: Name;
			}

			public static ItemInfo Find(int id)
			{
				ItemInfo info;
				return _instance._itemByID.TryGetValue(id, out info) ? info : null;
			}

			[JsonIgnore] public int itemId;

			[JsonIgnore] private ICuiComponent _image;

			public CuiElement GetImage(string aMin, string aMax, string oMin, string oMax, string parent,
				string name = null)
			{
				if (_image == null)
					_image = new CuiImageComponent
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

		private readonly Dictionary<string, string> _constContainers = new Dictionary<string, string>
		{
			["assets/bundled/prefabs/static/bbq.static.prefab"] = "https://i.imgur.com/L28375p.png",
			["assets/bundled/prefabs/static/hobobarrel_static.prefab"] = "https://i.imgur.com/v8sDTaP.png",
			["assets/bundled/prefabs/static/recycler_static.prefab"] = "https://i.imgur.com/V1smQYs.png",
			["assets/bundled/prefabs/static/repairbench_static.prefab"] = "https://i.imgur.com/8qV6Z10.png",
			["assets/bundled/prefabs/static/researchtable_static.prefab"] = "https://i.imgur.com/guoVK66.png",
			["assets/bundled/prefabs/static/small_refinery_static.prefab"] = "https://i.imgur.com/o4iHwpz.png",
			["assets/bundled/prefabs/static/wall.frame.shopfront.metal.static.prefab"] =
				"https://i.imgur.com/aJIU90I.png",
			["assets/bundled/prefabs/static/water_catcher_small.static.prefab"] = "https://i.imgur.com/ZdaXU6q.png",
			["assets/bundled/prefabs/static/workbench1.static.prefab"] = "https://i.imgur.com/0Trejvg.png",
			["assets/content/props/fog machine/fogmachine.prefab"] = "https://i.imgur.com/v33hmbo.png",
			["assets/content/structures/excavator/prefabs/engine.prefab"] = "",
			["assets/content/structures/excavator/prefabs/excavator_output_pile.prefab"] = "",
			["assets/content/vehicles/boats/rhib/subents/fuel_storage.prefab"] = "https://i.imgur.com/QXjLWzj.png",
			["assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab"] = "https://i.imgur.com/QXjLWzj.png",
			["assets/content/vehicles/boats/rowboat/subents/fuel_storage.prefab"] = "https://i.imgur.com/FLr37Mb.png",
			["assets/content/vehicles/boats/rowboat/subents/rowboat_storage.prefab"] =
				"https://i.imgur.com/FLr37Mb.png",
			["assets/content/vehicles/minicopter/subents/fuel_storage.prefab"] = "https://i.imgur.com/BRBfPjB.png",
			["assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab"] =
				"https://i.imgur.com/3CSoGly.png",
			["assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab"] =
				"https://i.imgur.com/HpteUCe.png",
			["assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab"] =
				"https://i.imgur.com/QI7tzYJ.png",
			["assets/content/vehicles/modularcar/subents/modular_car_1mod_storage.prefab"] = "",
			["assets/content/vehicles/modularcar/subents/modular_car_2mod_fuel_tank.prefab"] = "",
			["assets/content/vehicles/modularcar/subents/modular_car_fuel_storage.prefab"] = "",
			["assets/content/vehicles/modularcar/subents/modular_car_i4_engine_storage.prefab"] = "",
			["assets/content/vehicles/modularcar/subents/modular_car_v8_engine_storage.prefab"] = "",
			["assets/content/vehicles/scrap heli carrier/subents/fuel_storage_scrapheli.prefab"] = "",
			["assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.metal.prefab"] =
				"https://i.imgur.com/aJIU90I.png",
			["assets/prefabs/deployable/bbq/bbq.deployed.prefab"] = "https://i.imgur.com/L28375p.png",
			["assets/prefabs/deployable/campfire/campfire.prefab"] = "https://i.imgur.com/FIznmKI.png",
			["assets/prefabs/deployable/composter/composter.prefab"] = "https://i.imgur.com/glcIjOS.png",
			["assets/prefabs/deployable/dropbox/dropbox.deployed.prefab"] = "https://i.imgur.com/HmoyaIU.png",
			["assets/prefabs/deployable/fireplace/fireplace.deployed.prefab"] = "https://i.imgur.com/XsMSlNY.png",
			["assets/prefabs/deployable/fridge/fridge.deployed.prefab"] = "https://i.imgur.com/ERNmHjz.png",
			["assets/prefabs/deployable/furnace.large/furnace.large.prefab"] = "https://i.imgur.com/GWaSIUw.png",
			["assets/prefabs/deployable/furnace/furnace.prefab"] = "https://i.imgur.com/cnFpbOj.png",
			["assets/prefabs/deployable/hitch & trough/hitchtrough.deployed.prefab"] =
				"https://i.imgur.com/FiSIYh9.png",
			["assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab"] =
				"https://i.imgur.com/KaGFKkM.png",
			["assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"] = "https://i.imgur.com/iPBEYf3.png",
			["assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab"] = "https://i.imgur.com/brKtJJj.png",
			["assets/prefabs/deployable/lantern/lantern.deployed.prefab"] = "https://i.imgur.com/LqfkTKp.png",
			["assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"] =
				"https://i.imgur.com/wecMrji.png",
			["assets/prefabs/deployable/liquidbarrel/waterbarrel.prefab"] = "https://i.imgur.com/LAHPuI9.png",
			["assets/prefabs/deployable/locker/locker.deployed.prefab"] = "https://i.imgur.com/jZ4raNL.png",
			["assets/prefabs/deployable/mailbox/mailbox.deployed.prefab"] = "https://i.imgur.com/egLTaYb.png",
			["assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab"] = "https://i.imgur.com/sbyHOjn.png",
			["assets/prefabs/deployable/oil jack/crudeoutput.prefab"] = "",
			["assets/prefabs/deployable/oil jack/fuelstorage.prefab"] = "",
			["assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab"] =
				"https://i.imgur.com/1KMt1eu.png",
			["assets/prefabs/deployable/planters/planter.large.deployed.prefab"] = "https://i.imgur.com/POcQ0Ya.png",
			["assets/prefabs/deployable/planters/planter.small.deployed.prefab"] = "https://i.imgur.com/fMO8cJF.png",
			["assets/prefabs/deployable/playerioents/generators/fuel generator/small_fuel_generator.deployed.prefab"] =
				"https://i.imgur.com/fghbYKE.png",
			["assets/prefabs/deployable/playerioents/poweredwaterpurifier/poweredwaterpurifier.deployed.prefab"] =
				"https://i.imgur.com/Tg2dX8b.png",
			["assets/prefabs/deployable/playerioents/poweredwaterpurifier/poweredwaterpurifier.storage.prefab"] =
				"https://i.imgur.com/Tg2dX8b.png",
			["assets/prefabs/deployable/playerioents/waterpump/water.pump.deployed.prefab"] =
				"https://i.imgur.com/FZG19ki.png",
			["assets/prefabs/deployable/quarry/fuelstorage.prefab"] = "https://i.imgur.com/U1y3pmJ.png",
			["assets/prefabs/deployable/quarry/hopperoutput.prefab"] = "https://i.imgur.com/U1y3pmJ.png",
			["assets/prefabs/deployable/repair bench/repairbench_deployed.prefab"] = "https://i.imgur.com/8qV6Z10.png",
			["assets/prefabs/deployable/research table/researchtable_deployed.prefab"] =
				"https://i.imgur.com/guoVK66.png",
			["assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab"] = "https://i.imgur.com/rGstq6A.png",
			["assets/prefabs/deployable/small stash/small_stash_deployed.prefab"] = "https://i.imgur.com/ToPKE7j.png",
			["assets/prefabs/deployable/survivalfishtrap/survivalfishtrap.deployed.prefab"] =
				"https://i.imgur.com/2D6jZ7j.png",
			["assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab"] =
				"https://i.imgur.com/0Trejvg.png",
			["assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab"] =
				"https://i.imgur.com/cM5F6SO.png",
			["assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab"] =
				"https://i.imgur.com/ToyPHJK.png",
			["assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab"] =
				"https://i.imgur.com/mD9KsAL.png",
			["assets/prefabs/deployable/tuna can wall lamp/tunalight.deployed.prefab"] =
				"https://i.imgur.com/EWXtCJg.png",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_attire.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_building.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_components.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_extra.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_farming.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_resources.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_tools.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_vehicleshigh.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/npcvendingmachine_weapons.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/npcvendingmachines/shopkeeper_vm_invis.prefab"] = "",
			["assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab"] =
				"https://i.imgur.com/8Kfvfgp.png",
			["assets/prefabs/deployable/water catcher/water_catcher_large.prefab"] = "https://i.imgur.com/MF90xE7.png",
			["assets/prefabs/deployable/water catcher/water_catcher_small.prefab"] = "https://i.imgur.com/ZdaXU6q.png",
			["assets/prefabs/deployable/water well/waterwellstatic.prefab"] = "",
			["assets/prefabs/deployable/water well/waterwellstatic.prefab"] = "https://i.imgur.com/FyQJnhX.png",
			["assets/prefabs/deployable/waterpurifier/waterstorage.prefab"] = "https://i.imgur.com/FyQJnhX.png",
			["assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"] = "https://i.imgur.com/gwhRYjt.png",
			["assets/prefabs/io/electric/switches/fusebox/fusebox.prefab"] = "",
			["assets/prefabs/misc/casino/bigwheel/bigwheelbettingterminal.prefab"] = "",
			["assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern.deployed.prefab"] =
				"https://i.imgur.com/WUa0fN2.png",
			["assets/prefabs/misc/halloween/coffin/coffinstorage.prefab"] = "https://i.imgur.com/zHbT59P.png",
			["assets/prefabs/misc/halloween/cursed_cauldron/cursedcauldron.deployed.prefab"] =
				"https://i.imgur.com/z6QnrT3.png",
			["assets/prefabs/misc/halloween/skull_fire_pit/skull_fire_pit.prefab"] = "https://i.imgur.com/9phq5Bu.png",
			["assets/prefabs/misc/halloween/trophy skulls/skulltrophy.deployed.prefab"] =
				"https://i.imgur.com/TmntgJT.png",
			["assets/prefabs/misc/item drop/item_drop.prefab"] = "",
			["assets/prefabs/misc/item drop/item_drop_backpack.prefab"] = "",
			["assets/prefabs/misc/marketplace/marketterminal.prefab"] = "",
			["assets/prefabs/misc/summer_dlc/abovegroundpool/abovegroundpool.deployed.prefab"] = "",
			["assets/prefabs/misc/summer_dlc/paddling_pool/paddlingpool.deployed.prefab"] =
				"https://i.imgur.com/v2V6T7d.png",
			["assets/prefabs/misc/summer_dlc/photoframe/photoframe.landscape.prefab"] =
				"https://i.imgur.com/nH2jf5j.png",
			["assets/prefabs/misc/summer_dlc/photoframe/photoframe.large.prefab"] = "https://i.imgur.com/sPfBcVt.png",
			["assets/prefabs/misc/summer_dlc/photoframe/photoframe.portrait.prefab"] =
				"https://i.imgur.com/gvbD7Pm.png",
			["assets/prefabs/misc/supply drop/supply_drop.prefab"] = "https://i.imgur.com/VAtGtQB.png",
			["assets/prefabs/misc/xmas/snow_machine/models/snowmachine.prefab"] = "https://i.imgur.com/pAqw9It.png",
			["assets/prefabs/misc/twitch/hobobarrel/hobobarrel.deployed.prefab"] = "https://i.imgur.com/v8sDTaP.png",
			["assets/prefabs/misc/xmas/xmastree/xmas_tree.deployed.prefab"] = "https://i.imgur.com/wQU9ojJ.png",
			["assets/prefabs/npc/autoturret/autoturret_deployed.prefab"] = "https://i.imgur.com/VUiBkC5.png",
			["assets/prefabs/npc/flame turret/flameturret.deployed.prefab"] = "https://i.imgur.com/TcIOwLa.png",
			["assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab"] = "https://i.imgur.com/SNBPqIX.png",
			["assets/content/vehicles/submarine/subents/submarineitemstorage.prefab"] = "",
			["assets/rust.ai/nextai/ridablehorse/items/horse.saddlebag.item.prefab"] = "https://i.imgur.com/BpZPZYH.png"
		};

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadData();

			if (!_config.UserHammer)
				Unsubscribe(nameof(OnHammerHit));
		}

		private void OnServerInitialized(bool init)
		{
			LoadItems();

			LoadCategories();

			UniqueLoadContainers();

			LoadReferences();

			LoadImages();

			RegisterPermissions();

			AddCovalenceCommand(_config.Commands, nameof(CmdOpenStacks));
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

			foreach (var check in _defaultItemStackSize)
			{
				var info = ItemManager.FindItemDefinition(check.Key);
				if (info != null)
					info.stackable = check.Value;
			}

			SaveData();

			_instance = null;
		}

		private object CanMoveItem(Item movedItem, PlayerInventory playerInventory, ItemContainerId targetContainer, int targetSlot, int amount)
		{
			if (movedItem == null || playerInventory == null || _config.BlockList.Exists(movedItem)) return null;

			_multiplierByItem.Remove(movedItem.uid.Value);

			var container = playerInventory.FindContainer(targetContainer);
			var player = playerInventory.GetComponent<BasePlayer>();

			if (container != null)
			{
				var entity = container.entityOwner;
				if (entity != null)
					_multiplierByItem[movedItem.uid.Value] = GetContainerMultiplier(entity.name);
			}

			#region Right-Click Overstack into Player Inventory

			if (targetSlot == -1)
			{
#if TESTING
				Puts("Right-Click Overstack into Player Inventory");
#endif

				//Right click overstacks into player inventory
				if (container == null)
				{
					if (movedItem.amount > movedItem.info.stackable)
					{
						var loops = 1;
						if (player.serverInput.IsDown(BUTTON.SPRINT))
							loops = Mathf.CeilToInt((float) movedItem.amount / movedItem.info.stackable);
						for (var i = 0; i < loops; i++)
						{
							if (movedItem.amount <= movedItem.info.stackable)
							{
								playerInventory.GiveItem(movedItem);
								break;
							}

							if (movedItem.info.stackable > 0)
							{
								var itemToMove = movedItem.SplitItem(movedItem.info.stackable);
								if (!playerInventory.GiveItem(itemToMove))
								{
									movedItem.amount += itemToMove.amount;
									itemToMove.Remove();
									break;
								}

								movedItem.MarkDirty();
							}
						}

						playerInventory.ServerUpdate(0f);
						return true;
					}
				}
				//Shift Right click into storage container
				else
				{
					if (player.serverInput.IsDown(BUTTON.SPRINT))
					{
						if (_hasFurnaceSplitter && container.entityOwner is BaseOven)
							return null;

						ItemHelper.MoveShiftRight(movedItem, container, playerInventory);
						return true;
					}
				}
			}

			#endregion

			#region Moving Overstacks Around In Chest

			if (amount > movedItem.info.stackable && container != null)
			{
#if TESTING
				Puts("Moving Overstacks Around In Chest");
#endif

				var targetItem = container.GetSlot(targetSlot);
				if (targetItem == null)
				{
					//Split item into chest
					ItemHelper.SplitMoveItem(movedItem, movedItem.info.stackable, container, targetSlot);
				}
				else
				{
					//Stacking items when amount > info.stacksize
					if (targetItem.CanStack(movedItem))
					{
						if (amount < movedItem.amount)
						{
							ItemHelper.SplitMoveItem(movedItem, movedItem.info.stackable, playerInventory);
						}
						else
						{
							if (targetItem.info.stackable < targetItem.amount + movedItem.amount)
							{
								var splitAmount = targetItem.info.stackable - targetItem.amount;
								if (splitAmount > 0)
								{
									var itemToMove = movedItem.SplitItem(splitAmount);
									if (!itemToMove.MoveToContainer(container, targetSlot))
									{
										movedItem.amount += itemToMove.amount;
										itemToMove.Remove();

										playerInventory.ServerUpdate(0f);
										return true;
									}

									playerInventory.ServerUpdate(0f);
									return true;
								}
							}

							movedItem.MoveToContainer(container, targetSlot);
						}
					}
				}

				playerInventory.ServerUpdate(0f);
				return true;
			}

			#endregion

			#region Prevent Moving Overstacks To Inventory

			if (container != null)
			{
#if TESTING
				Puts("Prevent Moving Overstacks To Inventory");
#endif
				var targetItem = container.GetSlot(targetSlot);
				if (targetItem != null && !movedItem.CanStack(targetItem) &&
				    targetItem.amount > targetItem.info.stackable)
					return true;
			}

			#endregion

			return null;
		}

		private int? OnMaxStackable(Item item)
		{
			if (item == null ||
			    item.info.itemType == ItemContainer.ContentsType.Liquid ||
			    item.info.stackable == 1 ||
			    _config.BlockList.Exists(item))
				return null;

			float multiplier;
			if (_multiplierByItem.TryGetValue(item.uid.Value, out multiplier))
			{
#if TESTING
				Puts($"OnMaxStackable.check multiplierByItem: {item} | stack: {Mathf.FloorToInt(item.info.stackable * multiplier)}");
#endif
				return Mathf.FloorToInt(item.info.stackable * multiplier);
			}

			if (item.parent == null || item.parent.entityOwner == null) return null;

			return Mathf.FloorToInt(GetContainerMultiplier(item.parent.entityOwner.name) * item.info.stackable);
		}

		private void OnItemDropped(Item item, BaseEntity entity)
		{
#if TESTING
			Puts($"OnItemDropped: {item}");
#endif
			_multiplierByItem.Remove(item.uid.Value);

			item.RemoveFromContainer();
			var stackSize = item.info.stackable;

#if TESTING
			Puts($"OnItemDropped.stackSize: {stackSize} | item.amount: {item.amount}");
#endif

			if (stackSize <= 0f || item.amount <= stackSize) return;

			var loops = Mathf.FloorToInt((float) item.amount / stackSize);
			if (loops > 20) return;
			for (var i = 0; i < loops; i++)
			{
				if (stackSize <= 0f || item.amount <= stackSize) break;

				item.SplitItem(stackSize)?.Drop(entity.transform.position,
					entity.GetComponent<Rigidbody>().velocity + Vector3Ex.Range(-1f, 1f));
			}
		}

		private void OnHammerHit(BasePlayer player, HitInfo info)
		{
			if (!_config.UserHammer || player == null ||
			    !permission.UserHasPermission(player.UserIDString, AdminPerm) || info == null)
				return;

			var container = info.HitEntity;
			if (container == null)
				return;

			ContainerData data;
			if (_containers.TryGetValue(container.name, out data))
				SettingsUi(player, 1, 0, 0, data.ID, first: true);
		}

		private void OnItemDespawn(Item item)
		{
			if (item == null) return;

			_multiplierByItem.Remove(item.uid.Value);
		}

		#region FurnaceSplitter

		private void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Name == "FurnaceSplitter") _hasFurnaceSplitter = true;
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "FurnaceSplitter") _hasFurnaceSplitter = false;
		}

		#endregion

		#endregion

		#region Commands

		private void CmdOpenStacks(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!permission.UserHasPermission(player.UserIDString, AdminPerm))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
				MainUi(player, first: true);
				return;
			}

			switch (args[0])
			{
				case "sethandstack":
				{
					int stackSize;
					if (args.Length < 2 || !int.TryParse(args[1], out stackSize))
					{
						cov.Reply($"Error syntax! Use: /{command} {args[0]} [stackSize]");
						return;
					}

					var activeItem = player.GetActiveItem();
					if (activeItem == null)
					{
						cov.Reply("You are missing an item in your hand!");
						return;
					}

					var item = _items.Find(x => x.ShortName == activeItem.info.shortname);
					if (item == null)
					{
						cov.Reply("Item not found!");
						return;
					}

					item.CustomStackSize = stackSize;

					UpdateItemStack(item);

					SendNotify(player, SetStack, 0, stackSize, item.GetItemDisplayName(player));

					SaveData();
					break;
				}

				case "setstack":
				{
					int stackSize;
					if (args.Length < 3 || !int.TryParse(args[2], out stackSize))
					{
						cov.Reply($"Error syntax! Use: /{command} {args[0]} [shortName] [stackSize]");
						return;
					}

					var item = _items.Find(x => x.ShortName == args[1]);
					if (item == null)
					{
						cov.Reply($"Item '{args[1]}' not found!");
						return;
					}

					item.CustomStackSize = stackSize;

					UpdateItemStack(item);

					SendNotify(player, SetStack, 0, stackSize, item.GetItemDisplayName(player));

					SaveData();
					break;
				}
			}
		}

		[ConsoleCommand("UI_Stacks")]
		private void CmdConsoleStacks(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "page":
				{
					int type, category = -1, page = 0;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out type)) return;

					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out category);

					if (arg.HasArgs(4))
						int.TryParse(arg.Args[3], out page);

					var search = string.Empty;
					if (arg.HasArgs(5))
						search = arg.Args[4];

					MainUi(player, type, category, page, search);
					break;
				}

				case "enter_page":
				{
					int type, category, page;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out type) ||
					    !int.TryParse(arg.Args[2], out category) ||
					    !int.TryParse(arg.Args[4], out page)) return;

					var search = arg.Args[3];
					if (string.IsNullOrEmpty(search)) return;

					MainUi(player, type, category, page, search);
					break;
				}

				case "settings":
				{
					int type, category, page, id;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out type) ||
					    !int.TryParse(arg.Args[2], out category) ||
					    !int.TryParse(arg.Args[3], out page) ||
					    !int.TryParse(arg.Args[4], out id)) return;

					var enterValue = -1f;
					if (arg.HasArgs(6))
					{
						float.TryParse(arg.Args[5], out enterValue);

						enterValue = (float) Math.Round(enterValue, 2);
					}

					SettingsUi(player, type, category, page, id, enterValue);
					break;
				}

				case "apply_settings":
				{
					int type, category, page, id;
					float nowValue;
					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[1], out type) ||
					    !int.TryParse(arg.Args[2], out category) ||
					    !int.TryParse(arg.Args[3], out page) ||
					    !int.TryParse(arg.Args[4], out id) ||
					    !float.TryParse(arg.Args[5], out nowValue) || nowValue <= 0) return;

					switch (type)
					{
						case 0: //item
						{
							SetItemMultiplier(id, nowValue);
							break;
						}

						case 1: //container
						{
							SetContainerMultiplier(id, nowValue);
							break;
						}
					}

					MainUi(player, type, category, page);
					break;
				}
			}
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, int type = 0, int category = -1, int page = 0, string search = "",
			bool first = false)
		{
			var lines = 0;
			float height;
			float ySwitch;

			var container = new CuiElementContainer();

			#region Background

			if (first)
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
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-300 -250",
					OffsetMax = "300 255"
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
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = HexToCuiColor("#161617")}
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
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Header");

			float xSwitch = -25;
			float width = 25;
			float margin = 5;

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
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Close = Layer,
					Color = HexToCuiColor("#4B68FF")
				}
			}, Layer + ".Header");

			xSwitch = xSwitch - margin - width;
			width = 80;

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
					Text = Msg(player, ContainerTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = type == 1 ? HexToCuiColor("#4B68FF") : HexToCuiColor("#FFFFFF", 5),
					Command = "UI_Stacks page 1"
				}
			}, Layer + ".Header");

			xSwitch = xSwitch - margin - width;

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
					Text = Msg(player, ItemTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = type == 0 ? HexToCuiColor("#4B68FF") : HexToCuiColor("#FFFFFF", 5),
					Command = "UI_Stacks page 0"
				}
			}, Layer + ".Header");

			#region Search

			xSwitch = xSwitch - margin - width;
			width = 140;

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
					Color = HexToCuiColor("#000000")
				}
			}, Layer + ".Header", Layer + ".Header.Search");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0", OffsetMax = "-10 0"
				},
				Text =
				{
					Text = string.IsNullOrEmpty(search) ? Msg(player, SearchTitle) : $"{search}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 0.65"
				}
			}, Layer + ".Header.Search");

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
						Command = $"UI_Stacks page {type} {category} {page} ",
						Color = "1 1 1 0.95",
						CharsLimit = 32,
						NeedsKeyboard = true
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});

			#endregion

			#endregion

			var maxCount = 0;

			switch (type)
			{
				case 0: //Item
				{
					#region Categories

					var amountOnString = 6;
					width = 90;
					margin = 5;
					height = 25;
					ySwitch = -60;

					xSwitch = 20;

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{xSwitch} {ySwitch - height}",
							OffsetMax = $"{xSwitch + width} {ySwitch}"
						},
						Text =
						{
							Text = Msg(player, AllTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = -1 == category ? HexToCuiColor("#4B68FF") : HexToCuiColor("#161617"),
							Command = $"UI_Stacks page {type} {-1}"
						}
					}, Layer + ".Main");

					xSwitch += width + margin;

					for (var i = 0; i < _categories.Count; i++)
					{
						var info = _categories[i];

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch} {ySwitch - height}",
								OffsetMax = $"{xSwitch + width} {ySwitch}"
							},
							Text =
							{
								Text = $"{info.Title}",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = i == category ? HexToCuiColor("#4B68FF") : HexToCuiColor("#161617"),
								Command = $"UI_Stacks page {type} {i}"
							}
						}, Layer + ".Main");

						if ((i + 2) % amountOnString == 0)
						{
							ySwitch = ySwitch - height - margin;
							xSwitch = 20;
						}
						else
						{
							xSwitch += width + margin;
						}
					}

					#endregion

					#region Items

					ySwitch = ySwitch - height - 5;

					margin = 5;
					height = 50f;
					xSwitch = 20;
					width = 565;
					lines = 6;

					var categoryInfo = category == -1
						? _categories.SelectMany(x => x.Items).ToList()
						: _categories[category].Items;

					var items = string.IsNullOrEmpty(search) || search.Length < 2
						? categoryInfo
						: categoryInfo.FindAll(x => x.GetItemDisplayName(player).StartsWith(search) ||
						                            x.GetItemDisplayName(player).Contains(search)
						                            || x.Name.StartsWith(search) || x.Name.Contains(search) ||
						                            x.ShortName.StartsWith(search) || x.ShortName.Contains(search));

					maxCount = items.Count;

					foreach (var item in items.Skip(page * lines).Take(lines))
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
								Color = HexToCuiColor("#161617")
							}
						}, Layer + ".Main", Layer + $".Panel.{item.ShortName}");

						container.Add(item.GetImage("0 0.5", "0 0.5", "20 -15", "50 15",
							Layer + $".Panel.{item.ShortName}"));

						#region Name

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "55 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = $"{item.GetItemDisplayName(player)}",
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							}
						}, Layer + $".Panel.{item.ShortName}");

						#endregion

						#region Default Stack

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0.5", AnchorMax = "1 1",
								OffsetMin = "180 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, DefaultStack),
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Panel.{item.ShortName}");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0.5",
								OffsetMin = "180 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, StackFormat, item.DefaultStackSize),
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							}
						}, Layer + $".Panel.{item.ShortName}");

						#endregion

						#region Custom Stack

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0.5", AnchorMax = "1 1",
								OffsetMin = "310 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, CustomStack),
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Panel.{item.ShortName}");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0.5",
								OffsetMin = "310 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, StackFormat,
									item.CustomStackSize == 0 ? item.DefaultStackSize : item.CustomStackSize),
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							}
						}, Layer + $".Panel.{item.ShortName}");

						#endregion

						#region Settings

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "1 0.5", AnchorMax = "1 0.5",
								OffsetMin = "-110 -12.5",
								OffsetMax = "-10 12.5"
							},
							Text =
							{
								Text = Msg(player, SettingsTitle),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = HexToCuiColor("#4B68FF"),
								Command =
									$"UI_Stacks settings {type} {category} {(string.IsNullOrEmpty(search) ? page : 0)} {item.ID}"
							}
						}, Layer + $".Panel.{item.ShortName}");

						#endregion

						ySwitch = ySwitch - height - margin;
					}

					#endregion

					break;
				}

				case 1: //Container
				{
					#region Items

					ySwitch = -65;

					margin = 10;
					height = 60;
					xSwitch = 20;
					width = 565;
					lines = 6;

					var containers = string.IsNullOrEmpty(search) || search.Length < 2
						? _containerBase.ToList()
						: _containerBase
							.Where(x => x.Value.Prefab.EndsWith(search) ||
							            x.Value.Prefab.Contains(search)).ToList();

					maxCount = containers.Count;

					foreach (var check in containers.Skip(page * lines).Take(lines))
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
								Color = HexToCuiColor("#161617")
							}
						}, Layer + ".Main", Layer + $".Panel.{ySwitch}");

						#region Image

						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "5 5", OffsetMax = "55 55"
							},
							Image =
							{
								Color = HexToCuiColor("#0E0E10", 50)
							}
						}, Layer + $".Panel.{ySwitch}", Layer + $".Panel.{ySwitch}.Image");

						container.Add(check.Value.GetImage("0 0", "1 1", "0 0", "0 0",
							Layer + $".Panel.{ySwitch}.Image"));

						#endregion

						#region Prefab

						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "1 1", AnchorMax = "1 1",
								OffsetMin = "-500 -25", OffsetMax = "0 0 0 0"
							},
							Image =
							{
								Color = HexToCuiColor("#0E0E10", 50)
							}
						}, Layer + $".Panel.{ySwitch}", Layer + $".Panel.{ySwitch}.Prefab");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "5 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = $"{check.Value.Prefab}",
								Align = TextAnchor.MiddleLeft,
								FontSize = 12,
								Font = "robotocondensed-regular.ttf",
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Panel.{ySwitch}.Prefab");

						#endregion

						#region Info

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "70 0",
								OffsetMax = "200 35"
							},
							Text =
							{
								Text = Msg(player, DefaultMultiplier, _config.DefaultContainerMultiplier),
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Panel.{ySwitch}");

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "260 0",
								OffsetMax = "400 35"
							},
							Text =
							{
								Text = Msg(player, CustomMultiplier, check.Value.Multiplier),
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Panel.{ySwitch}");

						#endregion

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "1 0", AnchorMax = "1 0",
								OffsetMin = "-90 7.5", OffsetMax = "-10 27.5"
							},
							Text =
							{
								Text = Msg(player, SettingsTitle),
								Align = TextAnchor.MiddleCenter,
								FontSize = 14,
								Font = "robotocondensed-regular.ttf",
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = HexToCuiColor("#4B68FF"),
								Command =
									$"UI_Stacks settings {type} {category} {(string.IsNullOrEmpty(search) ? page : 0)} {check.Value.ID}"
							}
						}, Layer + $".Panel.{ySwitch}");

						ySwitch = ySwitch - height - margin;
					}

					#endregion

					break;
				}
			}

			#region Pages

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-50 5", OffsetMax = "50 25"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Pages");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "0 1",
					OffsetMin = "-20 0",
					OffsetMax = "0 0"
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
					Color = HexToCuiColor("#4B68FF", 33),
					Command = page != 0 ? $"UI_Stacks page {type} {category} {page - 1} {search}" : ""
				}
			}, Layer + ".Pages");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0",
					AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "20 0"
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
					Color = HexToCuiColor("#4B68FF"),
					Command = maxCount > (page + 1) * lines
						? $"UI_Stacks page {type} {category} {page + 1} {search}"
						: ""
				}
			}, Layer + ".Pages");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{page + 1}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 0.5"
				}
			}, Layer + ".Pages");

			if (string.IsNullOrEmpty(search))
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
							Command = string.IsNullOrEmpty(search) || search.Length < 2
								? $"UI_Stacks page {type} {category} "
								: $"UI_Stacks enter_page {type} {category} {search} ",
							Color = "1 1 1 0.95",
							CharsLimit = 32,
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void SettingsUi(BasePlayer player, int type, int category, int page, int id, float enterValue = -1f,
			bool first = false)
		{
			var nowValue = 0f;

			var container = new CuiElementContainer();

			#region Background

			if (first)
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
						Close = Layer
					}
				}, Layer);
			}

			#endregion

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-300 -160",
					OffsetMax = "300 160"
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
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".Main", Layer + ".Header");

			switch (type)
			{
				case 0:
				{
					var item = ItemInfo.Find(id);
					if (item == null) return;

					nowValue = item.CustomStackSize == 0 ? item.DefaultStackSize : item.CustomStackSize;

					container.Add(new CuiElement
					{
						Parent = Layer + ".Header",
						Components =
						{
							new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", item.ShortName)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 0",
								OffsetMin = "5 5", OffsetMax = "45 45"
							}
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "50 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = $"{item.Name}",
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + ".Header");
					break;
				}

				case 1:
				{
					var cont = GetContainerByID(id);

					nowValue = cont.Multiplier;

					container.Add(cont.GetImage("0 0", "0 0", "5 5", "45 45", Layer + ".Header"));

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "50 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = $"{cont.Prefab}",
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + ".Header");
					break;
				}
			}

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-80 -37.5",
					OffsetMax = "-55 -12.5"
				},
				Text =
				{
					Text = Msg(player, BtnBack),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = $"UI_Stacks page {type} {category} {page}"
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
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Close = Layer,
					Color = HexToCuiColor("#4B68FF")
				}
			}, Layer + ".Header");

			#endregion

			#region Now

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-150 -145",
					OffsetMax = "150 -105"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Value.Now");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 30"
				},
				Text =
				{
					Text = Msg(player, type == 0 ? CurrentStack : CurrentMultiplier),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 0.4"
				}
			}, Layer + ".Value.Now");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0", OffsetMax = "-10 0"
				},
				Text =
				{
					Text = $"{nowValue}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 0.2"
				}
			}, Layer + ".Value.Now");

			#endregion

			#region Input

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-150 -235",
					OffsetMax = "150 -195"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Value.Input");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 30"
				},
				Text =
				{
					Text = Msg(player, type == 0 ? EnterStack : EnterMultiplier),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 0.4"
				}
			}, Layer + ".Value.Input");

			if (enterValue > 0)
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "-10 0"
					},
					Text =
					{
						Text = $"{enterValue}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 0.2"
					}
				}, Layer + ".Value.Input");

			container.Add(new CuiElement
			{
				Parent = Layer + ".Value.Input",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						Color = "1 1 1 0.95",
						CharsLimit = 32,
						Command = $"UI_Stacks settings {type} {category} {page} {id} ",
						NeedsKeyboard = true
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "-10 0"
					}
				}
			});

			#endregion

			#region Apply

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-150 -300",
					OffsetMax = "150 -260"
				},
				Text =
				{
					Text = Msg(player, AcceptTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = enterValue > 0
						? $"UI_Stacks apply_settings {type} {category} {page} {id} {enterValue}"
						: $"UI_Stacks page {type} {category} {page}"
				}
			}, Layer + ".Main");

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

		private int GetItemStack(ItemDefinition definition)
		{
			var itemInfo = _items.Find(x => x.itemId == definition.itemid);
			return itemInfo != null
				? itemInfo.CustomStackSize > 0 ? itemInfo.CustomStackSize : itemInfo.DefaultStackSize
				: definition.stackable;
		}

		private int GetDefaultStack(ItemDefinition definition)
		{
			int stack;
			return _defaultStackByID.TryGetValue(definition.itemid, out stack) ? stack : definition.stackable;
		}

		private void LoadReferences()
		{
			if (FurnaceSplitter != null && FurnaceSplitter.IsLoaded) _hasFurnaceSplitter = true;
		}

		private Dictionary<int, string> _containerPrefabByID = new Dictionary<int, string>();

		private Dictionary<string, int> _containerPrefabID = new Dictionary<string, int>();

		private Dictionary<int, ContainerData> _containerBase = new Dictionary<int, ContainerData>();

		private void UniqueLoadContainers()
		{
			var loaded = Pool.GetList<string>();

			foreach (var container in _containers)
			{
				loaded.Add(container.Key);

				var data = container.Value;
				data.Prefab = container.Key;

				_containerBase.Add(container.Value.ID, data);
			}

			foreach (var container in _constContainers)
			{
				if (loaded.Contains(container.Key)) continue;

				var data = new ContainerData
				{
					Image = container.Value,
					Multiplier = _config.DefaultContainerMultiplier,
					Prefab = container.Key
				};

				_containerBase.Add(data.ID, data);
			}

			Pool.FreeList(ref loaded);

			// load prefabs by id
			foreach (var check in _containerBase)
				_containerPrefabByID[check.Key] = check.Value.Prefab;

			// load ids by prefab
			foreach (var check in _containerBase)
				_containerPrefabID[check.Value.Prefab] = check.Key;
		}

		private string GetPrefabByID(int id)
		{
			string prefab;
			return _containerPrefabByID.TryGetValue(id, out prefab) ? prefab : string.Empty;
		}

		private int GetPrefabID(string prefab)
		{
			int id;
			return _containerPrefabID.TryGetValue(prefab, out id) ? id : 0;
		}

		private ContainerData GetContainerByID(string prefab)
		{
			return GetContainerByID(GetPrefabID(prefab));
		}

		private ContainerData GetContainerByID(int id)
		{
			ContainerData data;
			return _containerBase.TryGetValue(id, out data) ? data : null;
		}

		private void SetContainerMultiplier(int id, float multiplier)
		{
			var prefab = GetPrefabByID(id);
			if (string.IsNullOrEmpty(prefab)) return;

			ContainerData data;
			if (_containers.TryGetValue(prefab, out data))
			{
				data.Multiplier = multiplier;
				SaveData();
				return;
			}

			if ((data = _containerBase.Values.FirstOrDefault(x => x.Prefab == prefab)) == null) return;

			data.Multiplier = multiplier;
			_containers[prefab] = data;
			SaveData();
		}

		private void SetItemMultiplier(int id, float nowValue)
		{
			var item = ItemInfo.Find(id);
			if (item == null) return;

			item.CustomStackSize = (int) nowValue;

			UpdateItemStack(item);

			SaveData();
		}

		private float GetContainerMultiplier(string prefab)
		{
			return GetContainerByID(prefab)?.Multiplier ?? 1f;
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

		private void RegisterPermissions()
		{
			if (!permission.PermissionExists(AdminPerm))
				permission.RegisterPermission(AdminPerm, this);
		}

		private void LoadCategories()
		{
			var dict = new Dictionary<ItemCategory, List<ItemDefinition>>();

			ItemManager.itemList.ForEach(info =>
			{
				List<ItemDefinition> category;
				if (dict.TryGetValue(info.category, out category))
					category.Add(info);
				else
					dict.Add(info.category, new List<ItemDefinition>
					{
						info
					});
			});

			foreach (var check in dict)
				_categories.Add(new CategoryInfo
				{
					Title = check.Key.ToString(),
					Items = _items.FindAll(x => check.Value.Exists(info => info.shortname == x.ShortName))
				});
		}

		private int GetContainerId()
		{
			var result = -1;

			do
			{
				var val = Random.Range(int.MinValue, int.MaxValue);
				if (_containers.All(x => x.Value.ID != val))
					result = val;
			} while (result == -1);

			return result;
		}

		private Dictionary<int, ItemInfo> _itemByID = new Dictionary<int, ItemInfo>();

		private Dictionary<int, int> _defaultStackByID = new Dictionary<int, int>();

		private void LoadItems()
		{
			// load default stacks
			var source = FileSystem.LoadAllFromBundle<GameObject>("items.preload.bundle", "l:ItemDefinition");
			var itemDefinitions = source
				.Select(x => x.GetComponent<ItemDefinition>())
				.Where(x => x != null);

			var anyAdded = false;
			itemDefinitions.ForEach(info =>
			{
				_defaultStackByID[info.itemid] = info.stackable;
				_defaultItemStackSize[info.shortname] = info.stackable;

				var item = _items.Find(x => x.ShortName == info.shortname);
				if (item == null)
				{
					_items.Add(item = new ItemInfo
					{
						ShortName = info.shortname,
						Name = info.displayName.english,
						CustomStackSize = 0,
						itemId = info.itemid
					});

					anyAdded = true;
				}
				else
				{
					item.itemId = info.itemid;

					if (item.CustomStackSize != 0) UpdateItemStack(item);
				}

				item.ID = GetItemId();

				_itemByID[item.ID] = item;
			});

			if (anyAdded)
				SaveData();
		}

		private void UpdateItemStack(ItemInfo info)
		{
			var def = ItemManager.FindItemDefinition(info.ShortName);
			if (def == null) return;

			def.stackable = info.CustomStackSize;
		}

		private int GetItemId()
		{
			var result = -1;

			do
			{
				var val = Random.Range(int.MinValue, int.MaxValue);
				if (!_items.Exists(x => x.ID == val))
					result = val;
			} while (result == -1);

			return result;
		}

		private void LoadImages()
		{
			if (!ImageLibrary)
			{
				PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
			}
			else
			{
				var imagesList = new Dictionary<string, string>();

				foreach (var container in _containerBase.Values)
					if (!string.IsNullOrEmpty(container.Image))
						imagesList.TryAdd(container.Image, container.Image);

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		private static class ItemHelper
		{
			public static void SplitMoveItem(Item item, int amount, ItemContainer targetContainer, int targetSlot)
			{
				if (amount <= 0) return;

				var splitItem = item.SplitItem(amount);
				if (splitItem == null) return;

				if (!splitItem.MoveToContainer(targetContainer, targetSlot))
				{
					item.amount += splitItem.amount;
					splitItem.Remove();
				}
			}

			public static bool SplitMoveItem(Item item, int amount, BasePlayer player)
			{
				return SplitMoveItem(item, amount, player.inventory);
			}

			public static bool SplitMoveItem(Item item, int amount, PlayerInventory inventory)
			{
				if (amount <= 0) return false;

				var splitItem = item.SplitItem(amount);
				if (splitItem == null) return false;
				if (!inventory.GiveItem(splitItem))
				{
					item.amount += splitItem.amount;
					splitItem.Remove();
				}

				return true;
			}

			public static void MoveShiftRight(Item movedItem, ItemContainer container, PlayerInventory playerInventory)
			{
				var items = Pool.GetList<Item>();
				items.AddRange(playerInventory.containerMain.itemList.Where(x => x.info == movedItem.info));
				items.AddRange(playerInventory.containerBelt.itemList.Where(x => x.info == movedItem.info));

				foreach (var item in items)
				{
					var loops = Mathf.CeilToInt((float) item.amount / movedItem.info.stackable);
					for (var i = 0; i < loops; i++)
					{
						if (item.amount <= movedItem.info.stackable)
						{
							item.MoveToContainer(container);
							break;
						}

						if (movedItem.info.stackable > 0)
						{
							var itemToMove = item.SplitItem(movedItem.info.stackable);
							if (!itemToMove.MoveToContainer(container))
							{
								movedItem.amount += itemToMove.amount;
								itemToMove.Remove();
								break;
							}

							item.MarkDirty();
							movedItem.MarkDirty();
						}
					}
				}

				playerInventory.ServerUpdate(0f);
				Pool.FreeList(ref items);
			}
		}

		#endregion

		#region Lang

		private const string
			SetStack = "SettedStack",
			NoPermission = "NoPermission",
			AcceptTitle = "AcceptTitle",
			EnterMultiplier = "EnterMultiplier",
			CurrentMultiplier = "CurrentMultiplier",
			EnterStack = "EnterStack",
			CurrentStack = "CurrentStack",
			CustomMultiplier = "CustomMultiplier",
			DefaultMultiplier = "DefaultMultiplier",
			SettingsTitle = "SettingsTitle",
			StackFormat = "StackFormat",
			CustomStack = "CustomStack",
			DefaultStack = "DefaultStack",
			AllTitle = "AllTitle",
			SearchTitle = "SearchTitle",
			BtnBack = "BtnBack",
			BtnNext = "BtnNext",
			ItemTitle = "ItemTitle",
			ContainerTitle = "ContainerTitle",
			CloseButton = "CloseButton",
			TitleMenu = "TitleMenu";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoPermission] = "You don't have the required permission",
				[AllTitle] = "All",
				[CloseButton] = "✕",
				[TitleMenu] = "Stacks",
				[ContainerTitle] = "Container",
				[ItemTitle] = "Item",
				[BtnBack] = "◀",
				[BtnNext] = "▶",
				[SearchTitle] = "Search...",
				[DefaultStack] = "Default stack size",
				[CustomStack] = "Custom stack size",
				[StackFormat] = "x{0}",
				[SettingsTitle] = "Settings",
				[DefaultMultiplier] = "Default Multiplier: <color=white>x{0}</color>",
				[CustomMultiplier] = "Now Multiplier: <color=white>x{0}</color>",
				[CurrentStack] = "Current stack size:",
				[CurrentMultiplier] = "Current multiplier:",
				[EnterStack] = "Enter the new stack size:",
				[EnterMultiplier] = "Enter the multiplier:",
				[AcceptTitle] = "ACCEPT",
				[SetStack] = "You have set the stack size to {0} for the '{1}'"
			}, this);
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

#region Extension Methods

namespace Oxide.Plugins.StacksExtensionMethods
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
	}
}

#endregion Extension Methods