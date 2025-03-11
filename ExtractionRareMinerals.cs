using VLB;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("ExtractionRareMinerals", "DAez", "1.2.8")]
	[Description("During the extraction of resources, a rare stone may fall out, which, when melted or processed, gives more resources")]
	public class ExtractionRareMinerals : RustPlugin
	{
		#region Version
		class PluginVersion
		{
			public VersionNumber Configuration = new VersionNumber(1, 0, 3);
		}
		#endregion

		#region Reference
		[PluginReference] private Plugin Loottable;
		#endregion

		#region Configuration
		private PluginVersion version;
		private Configuration config;
		private class Configuration
		{
			public PluginVersion Version;
			public int MaxMineralsPerHit;
			public int? MaxStackable;
			public int TimeToSmelting;
			public string ItemShortName;
			public SpecialToolInfo SpecialTool = new SpecialToolInfo();
			public List<RareMineralInfo> Items;

			public static Configuration CreateDefault()
			{
				return new Configuration
				{
					Version = new PluginVersion(),
					MaxMineralsPerHit = 1,
					MaxStackable = null,
					TimeToSmelting = 30,
					ItemShortName = "sticks",
					Items = new List<RareMineralInfo>
					{
                        // large.sulfur
                        new RareMineralInfo
						{
							ID = "large.sulfur",
							SkinID = 2893225931,
							Name = "Large Sulfur Crystal",
							PermittedTool = new List<string> { "stone.pickaxe", "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 3.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = new DropItemInfo
							{
								ShortName = "sulfur",
								MinAmount = 1000,
								MaxAmount = 2500
							},
							PossibleItemsAfterRecycler = null,
							CanExtractOnlySpecialTool = false
						},
                        // large.metal
                        new RareMineralInfo
						{
							ID = "large.metal",
							SkinID = 2893226249,
							Name = "Large Metal Piece",
							PermittedTool = new List<string> { "stone.pickaxe", "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 5,
									Amount = 1
								}
							},
							ItemAfterSmelting = new DropItemInfo
							{
								ShortName = "metal.fragments",
								MinAmount = 1000,
								MaxAmount = 2500
							},
							PossibleItemsAfterRecycler = null,
							CanExtractOnlySpecialTool = false
						},
                        // large.stone
                        new RareMineralInfo
						{
							ID = "large.stone",
							SkinID = 2893226068,
							Name = "Large Stone",
							PermittedTool = new List<string> { "stone.pickaxe", "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "stone-ore",
									DropChance = 4,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "stones",
									MinAmount = 1500,
									MaxAmount = 3500
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // emerald
                        new RareMineralInfo
						{
							ID = "emerald",
							SkinID = 2893105244,
							Name = "Emerald",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 2.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 2.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "wood",
									MinAmount = 2000,
									MaxAmount = 3500
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // jade
                        new RareMineralInfo
						{
							ID = "jade",
							SkinID = 2901473542,
							Name = "Jade",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 2.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 2.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "rope",
									MinAmount = 3,
									MaxAmount = 15
								},
								new DropItemInfo
								{
									ShortName = "cloth",
									MinAmount = 50,
									MaxAmount = 200
								},
								new DropItemInfo
								{
									ShortName = "leather",
									MinAmount = 50,
									MaxAmount = 200
								},
								new DropItemInfo
								{
									ShortName = "fat.animal",
									MinAmount = 70,
									MaxAmount = 300
								},
								new DropItemInfo
								{
									ShortName = "lowgradefuel",
									MinAmount = 30,
									MaxAmount = 120
								},
							},
							CanExtractOnlySpecialTool = false
						},
                        // tanzanite
                        new RareMineralInfo
						{
							ID = "tanzanite",
							SkinID = 2901473839,
							Name = "Tanzanite",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 2.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 2.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "chocholate",
									MinAmount = 3,
									MaxAmount = 15
								},
								new DropItemInfo
								{
									ShortName = "can.beans",
									MinAmount = 3,
									MaxAmount = 15
								},
								new DropItemInfo
								{
									ShortName = "can.tuna",
									MinAmount = 3,
									MaxAmount = 15
								},
								new DropItemInfo
								{
									ShortName = "bandage",
									MinAmount = 10,
									MaxAmount = 30
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // amethyst
                        new RareMineralInfo
						{
							ID = "amethyst",
							SkinID = 2893105387,
							Name = "Amethyst",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 2,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 2,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "largemedkit",
									MinAmount = 1,
									MaxAmount = 3
								},
								new DropItemInfo
								{
									ShortName = "syringe.medical",
									MinAmount = 3,
									MaxAmount = 7
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // topaz
                        new RareMineralInfo
						{
							ID = "topaz",
							SkinID = 2893105314,
							Name = "Topaz",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 1.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 1.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "scrap",
									MinAmount = 50,
									MaxAmount = 100
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // musgravite
                        new RareMineralInfo
						{
							ID = "musgravite",
							SkinID = 2901990088,
							Name = "Musgravite",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 1.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 1.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "hazmatsuit",
									MinAmount = 1,
									MaxAmount = 1
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // ruby
                        new RareMineralInfo
						{
							ID = "ruby",
							SkinID = 2893105456,
							Name = "Ruby",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 1.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 1.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "metalpipe",
									MinAmount = 2,
									MaxAmount = 8
								},
								new DropItemInfo
								{
									ShortName = "gears",
									MinAmount = 2,
									MaxAmount = 8
								},
								new DropItemInfo
								{
									ShortName = "metalblade",
									MinAmount = 2,
									MaxAmount = 8
								},
								new DropItemInfo
								{
									ShortName = "metalspring",
									MinAmount = 2,
									MaxAmount = 8
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // obsidian
                        new RareMineralInfo
						{
							ID = "obsidian",
							SkinID = 2901473758,
							Name = "Obsidian",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 1.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = new DropItemInfo
							{
								ShortName = "metal.refined",
								MinAmount = 10,
								MaxAmount = 30
							},
							PossibleItemsAfterRecycler = null,
							CanExtractOnlySpecialTool = false
						},
                       // black-opal
                        new RareMineralInfo
						{
							ID = "black-opal",
							SkinID = 2901473926,
							Name = "Black Opal",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 1.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 1.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = new DropItemInfo
							{
								ShortName = "charcoal",
								MinAmount = 100,
								MaxAmount = 1500
							},
							PossibleItemsAfterRecycler = null,
							CanExtractOnlySpecialTool = false
						},
                       // pink-diamond
                        new RareMineralInfo
						{
							ID = "pink-diamond",
							SkinID = 2901473998,
							Name = "Pink Diamond",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 1.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 1.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "riflebody",
									MinAmount = 1,
									MaxAmount = 2
								},
								new DropItemInfo
								{
									ShortName = "semibody",
									MinAmount = 1,
									MaxAmount = 4
								},
								new DropItemInfo
								{
									ShortName = "smgbody",
									MinAmount = 1,
									MaxAmount = 2
								},
								new DropItemInfo
								{
									ShortName = "targeting.computer",
									MinAmount = 1,
									MaxAmount = 2
								},
								new DropItemInfo
								{
									ShortName = "cctv.camera",
									MinAmount = 1,
									MaxAmount = 2
								}
							},
							CanExtractOnlySpecialTool = false
						},
                        // diamond
                        new RareMineralInfo
						{
							ID = "diamond",
							SkinID = 2893105180,
							Name = "Diamond",
							PermittedTool = new List<string> { "pickaxe", "hammer.salvaged", "icepick.salvaged", "jackhammer" },
							ExtractionInfo = new List<ExtractionInfo>
							{
								new ExtractionInfo
								{
									PrefabShortName = "sulfur-ore",
									DropChance = 1.5f,
									Amount = 1
								},
								new ExtractionInfo
								{
									PrefabShortName = "metal-ore",
									DropChance = 1.5f,
									Amount = 1
								}
							},
							ItemAfterSmelting = null,
							PossibleItemsAfterRecycler = new List<DropItemInfo>
							{
								new DropItemInfo
								{
									ShortName = "pistol.semiauto",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "rifle.semiauto",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "pistol.python",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "pistol.revolver",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "smg.thompson",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "shotgun.pump",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "shotgun.double",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "shotgun.waterpipe",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "smg.mp5",
									MinAmount = 1,
									MaxAmount = 1
								},
								new DropItemInfo
								{
									ShortName = "pistol.nailgun",
									MinAmount = 1,
									MaxAmount = 1
								}
							},
							CanExtractOnlySpecialTool = false
						}
					}
				};
			}
		}

		private class RareMineralInfo
		{
			public string ID;
			public ulong SkinID;
			public string Name;
			public List<string> PermittedTool = new List<string>();
			public List<ExtractionInfo> ExtractionInfo = new List<ExtractionInfo>();
			public DropItemInfo ItemAfterSmelting;
			public List<DropItemInfo> PossibleItemsAfterRecycler;
			public bool CanExtractOnlySpecialTool;
		}

		private class ExtractionInfo
		{
			public string PrefabShortName;
			public float DropChance;
			public int Amount;
		}

		private class DropItemInfo
		{
			public string ShortName;
			public string Name = "default";
			public ulong SkinID = 0;
			public int MinAmount;
			public int MaxAmount;
		}

		private class SpecialToolInfo
		{
			public string ShortName = "pickaxe";
			public string Name = "High-Strength Pickaxe";
			public ulong SkinID = 3042157107;
			public bool CanLootSpawn = false;
			public int LootSpawnChance = 10;
		}

		private readonly string usePermission = "extractionrareminerals.use";
		private readonly string smeltingPermission = "extractionrareminerals.allowSmelting";
		private readonly string recyclerPermission = "extractionrareminerals.allowRecycler";
		#endregion

		#region Language
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["SmeltingNotPermission"] = "You don't have the permission to melt it down.",
				["RecyclerNotPermission"] = "You don't have the permission to recycle this."
			}, this, "en");
		}
		#endregion

		#region Init
		private void Init()
		{
			timer.In(1f, () =>
			{
				Loottable?.Call("AddCustomItem", this, GetItemId(config.SpecialTool.ShortName), config.SpecialTool.SkinID, config.SpecialTool.Name);
				int itemId = GetItemId(config.ItemShortName);
				config.Items.ForEach((item) =>
				{
					Loottable?.Call("AddCustomItem", this, itemId, item.SkinID, item.Name);
				});
			});
		}

		private void Loaded()
		{
			LoadConfig();
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			version = new PluginVersion();
			config = Config.ReadObject<Configuration>();
			if (config.Version == null || config.Version.Configuration < version.Configuration)
			{
				string updateDetails = "The 'SpecialTool' parameter has been added";
				PrintError($"The config has been updated! You should delete the old one and restart the plugin! {updateDetails}");
			}
			SaveConfig();
		}

		protected override void LoadDefaultConfig()
		{
			config = Configuration.CreateDefault();
		}

		void OnServerInitialized()
		{
			InitBaseItem();
			InitCookableItems();
			permission.RegisterPermission(usePermission, this);
			permission.RegisterPermission(smeltingPermission, this);
			permission.RegisterPermission(recyclerPermission, this);
		}

		private void InitBaseItem()
		{
			ItemDefinition itemInfo = ItemManager.FindItemDefinition(config.ItemShortName);
			if (config.MaxStackable != null)
			{
				itemInfo.stackable = (int)config.MaxStackable;
			}
            ItemModCookable cookable = itemInfo.GetOrAddComponent<ItemModCookable>();
			if (itemInfo.itemMods.FirstOrDefault(a => a == cookable) == null)
			{
				itemInfo.itemMods = itemInfo.itemMods.Concat(new ItemMod[] { cookable }).ToArray();
			}
			cookable.highTemp = 1200;
			cookable.lowTemp = 800;
			cookable.cookTime = config.TimeToSmelting;
			cookable.ModInit();
			ItemDefinition[] Children = itemInfo.Children;
            itemInfo.Initialize(new());
			itemInfo.Children = Children;
        }

		private void InitCookableItems()
		{
			foreach (var entity in BaseNetworkable.serverEntities)
			{
				var container = entity as BaseOven;
				if (container == null)
				{
					continue;
				}
                int itemId = GetItemId(config.ItemShortName);
				List<Item> foundItems = container.inventory.FindItemsByItemID(itemId);
				foundItems.ForEach(item =>
				{
                    if (CanCookableMineral(item))
					{
						item.onCycle -= CycleCooking;
						item.onCycle += CycleCooking;
					}
				});

            }
		}
		#endregion

		#region Commands
		[ConsoleCommand("give.rare.mineral")]
		private void GiveRareMineralsCommand(ConsoleSystem.Arg args)
		{
			if (args.Connection != null)
			{
				BasePlayer player = args.Player();
				if (player != null && !player.IsAdmin) return;
				if (args.Args == null || args.Args.Length < 1)
				{
					PrintToConsole(player, "Command invalid. Format: give.rare.mineral PLAYER ID AMOUNT");
					return;
				}
				if (args.Args.Length > 1)
				{
					BasePlayer target = BasePlayer.Find(args.Args[0]);
					string id = args.Args[1];
					int amount = 1;
					if (args.Args.Length > 2)
					{
						amount = System.Int32.Parse(args.Args[2]);
					}
					if (target != null)
					{
						Item item = CreateMineral(id, amount);
						if (item != null)
						{
							target.GiveItem(item);
							PrintToConsole(player, $"The player {target.displayName} has been successfully issued items");
						}
						else
						{
							PrintToConsole(player, "No such ID has been found");
						}
					}
				}
				else
				{
					PrintToConsole(player, "You have not entered the ID of the subject");
					return;
				}
			}
			else
			{
				if (args.Args.Length > 1)
				{
					BasePlayer target = BasePlayer.Find(args.Args[0]);
					string id = args.Args[1];
					int amount = 1;
					if (args.Args.Length > 2)
					{
						amount = System.Int32.Parse(args.Args[2]);
					}
					if (target != null)
					{
						Item item = CreateMineral(id, amount);
						if (item != null)
						{
							target.GiveItem(item);
							Puts($"The player {target.displayName} has been successfully issued items");
						}
						else
						{
							Puts("No such ID has been found");
						}
					}
				}
			}
		}

		[ConsoleCommand("give.special.tool")]
		private void GiveSpecialToolCommand(ConsoleSystem.Arg args)
		{
			if (args.Connection != null)
			{
				BasePlayer player = args.Player();
				if (player != null && !player.IsAdmin) return;
				if (args.Args == null || args.Args.Length < 1)
				{
					PrintToConsole(player, "Command invalid. Format: give.special.tool PLAYER");
					return;
				}
				if (args.Args.Length > 0)
				{
					BasePlayer target = BasePlayer.Find(args.Args[0]);
					if (target != null)
					{
						Item item = CreateSpecialTool();
						if (item != null)
						{
							target.GiveItem(item);
							PrintToConsole(player, $"The player {target.displayName} has been successfully issued special tool");
						}
						else
						{
							PrintToConsole(player, "No such ID has been found");
						}
					}
				}
				else
				{
					PrintToConsole(player, "You have not entered the ID of the subject");
					return;
				}
			}
			else
			{
				if (args.Args.Length > 0)
				{
					BasePlayer target = BasePlayer.Find(args.Args[0]);
					if (target != null)
					{
						Item item = CreateSpecialTool();
						if (item != null)
						{
							target.GiveItem(item);
							Puts($"The player {target.displayName} has been successfully issued special tool");
						}
						else
						{
							Puts("No such ID has been found");
						}
					}
				}
			}
		}
		#endregion

		#region Logic
		object CanCombineDroppedItem(DroppedItem dropItem, DroppedItem dropTargetItem)
		{
			if (dropItem == null || dropTargetItem == null)
			{
				return null;
			}
			Item item = dropItem.GetItem();
			Item targetItem = dropTargetItem.GetItem();
			if (item != null && targetItem != null)
			{
				if (IsMineral(item) || IsMineral(targetItem))
				{
					return false;
				}
			}
			return null;
		}

		object CanRecycle(Recycler recycler, Item item)
		{
			if (IsMineral(item))
			{
				return CanRecyclerMineral(item);
			}
			return null;
		}

		private object OnItemRecycle(Item item, Recycler recycler)
		{
			if (recycler == null || item == null)
			{
				return null;
			}
			RareMineralInfo itemDef = FindMineral(item);
			if (itemDef != null)
			{
				item.UseItem(1);
				Item itemCreated = GetRandomRecycledItem(itemDef);
				if (itemCreated != null && !recycler.MoveItemToOutput(itemCreated))
				{
					recycler.StopRecycling();
					return false;
				}
				return true;
			}
			return null;
		}

		object OnPlayerAttack(BasePlayer attacker, HitInfo info)
		{
			BaseEntity entity = info.HitEntity;
			if (entity == null || attacker == null)
			{
				return null;
			}
			if (!permission.UserHasPermission(attacker.UserIDString, usePermission))
			{
				return null;
			}
			Item activeItem = attacker.GetActiveItem();
			if (activeItem == null)
			{
				return null;
			}
			GiveMineral(entity, attacker, activeItem);
			return null;
		}

		private void GiveMineral(BaseEntity hitEntity, BasePlayer attacker, Item activeItem)
		{
			int count = 0;
			List<RareMineralInfo> items = config.Items.FindAll(a => a.ExtractionInfo.FirstOrDefault(b => b.PrefabShortName == hitEntity.ShortPrefabName) != null);
			items = Shuffle(items);
			foreach (var item in items)
			{
				if (count >= config.MaxMineralsPerHit)
				{
					break;
				}
				if (item.CanExtractOnlySpecialTool)
				{
					if (!IsSpecialTool(activeItem))
					{
						return;
					}
				}
				else
				{
					if (item.PermittedTool != null && !item.PermittedTool.Contains(activeItem.info.shortname))
					{
						return;
					}
				}
				ExtractionInfo info = item.ExtractionInfo.FirstOrDefault(a => a.PrefabShortName == hitEntity.ShortPrefabName);
				if (TryPull(info.DropChance))
				{
					Item itemCreated = CreateMineral(item.ID, info.Amount);
					attacker.GiveItem(itemCreated, BaseEntity.GiveItemReason.PickedUp);
					count += info.Amount;
				}
			}
		}

		ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
		{
            if (!IsMineral(item) || container.entityOwner == null)
			{
				return null;
			}
            if (container.entityOwner is BaseOven)
			{
				if (CanCookableMineral(item))
				{
					BasePlayer player = item.GetOwnerPlayer();
					if (player != null && player.IPlayer != null && !player.IPlayer.HasPermission(smeltingPermission))
					{
						PlayerReply(player.IPlayer, GetLang("SmeltingNotPermission"));
						return ItemContainer.CanAcceptResult.CannotAccept;
					}
					Item slot = container.GetSlot(targetPos);
					if (slot == null)
					{
						return ItemContainer.CanAcceptResult.CanAccept;
					}
				}
				return ItemContainer.CanAcceptResult.CannotAccept;
			}
			else if (container.entityOwner is Recycler)
			{
				if (CanRecyclerMineral(item))
				{
					BasePlayer player = item.GetOwnerPlayer();
					if (player != null && player.IPlayer != null && !player.IPlayer.HasPermission(recyclerPermission))
					{
						PlayerReply(player.IPlayer, GetLang("RecyclerNotPermission"));
						return ItemContainer.CanAcceptResult.CannotAccept;
					}
					Item slot = container.GetSlot(targetPos);
					if (slot == null)
					{
						return ItemContainer.CanAcceptResult.CanAccept;
					}
				}
				return ItemContainer.CanAcceptResult.CannotAccept;
			}
			return null;
		}

		private void CycleCooking(Item item, float delta)
		{
			ItemModCookable itemMod = item.info.GetComponent<ItemModCookable>();
			if (itemMod == null)
			{
				return;
			}
			if (!itemMod.CanBeCookedByAtTemperature(item.temperature) || item.cookTimeLeft < 0f)
			{
				return;
			}
			item.cookTimeLeft -= delta;
			if (item.cookTimeLeft > 0f)
			{
				return;
			}
			float num = item.cookTimeLeft * -1f;
			int a = 1 + Mathf.FloorToInt(num / itemMod.cookTime);
			item.cookTimeLeft = itemMod.cookTime - num % itemMod.cookTime;
			BaseOven baseOven = item.GetEntityOwner() as BaseOven;
			a = Mathf.Min(a, item.amount);
			if (item.amount > a)
			{
				item.amount -= a;
				item.MarkDirty();
			}
			else
			{
				item.Remove();
			}
			RareMineralInfo itemDef = FindMineral(item);
			int amount = UnityEngine.Random.Range(itemDef.ItemAfterSmelting.MinAmount, itemDef.ItemAfterSmelting.MaxAmount + 1);
			Item item2 = ItemManager.CreateByName(itemDef.ItemAfterSmelting.ShortName, amount * a, 0uL);
			if (item2 != null && !item2.MoveToContainer(item.parent) && !item2.MoveToContainer(item.parent))
			{
				item2.Drop(item.parent.dropPosition, item.parent.dropVelocity);
				if ((bool)item.parent.entityOwner && baseOven != null)
				{
					baseOven.OvenFull();
				}
			}
		}

		void OnItemRemovedFromContainer(ItemContainer container, Item item)
		{
			if (CanCookableMineral(item) && container.entityOwner is BaseOven)
			{
				item.onCycle -= CycleCooking;
			}
		}

		void OnItemAddedToContainer(ItemContainer container, Item item)
		{
			if (CanCookableMineral(item) && container.entityOwner is BaseOven)
			{
				item.onCycle += CycleCooking;
			}
		}

		object OnLootSpawn(LootContainer container)
		{
			if (!config.SpecialTool.CanLootSpawn)
			{
				return null;
			}
			int itemId = GetItemId(config.SpecialTool.ShortName);
			Item foundItem = container.inventory.FindItemByItemID(itemId);
			if (foundItem != null)
			{
				return null;
			}
			if (TryPull(config.SpecialTool.LootSpawnChance))
			{
				CreateSpecialTool().MoveToContainer(container.inventory);
			}
			return null;
		}
		#endregion

		#region Utils
		private Item GetRandomRecycledItem(RareMineralInfo item)
		{
			int randomIndex = UnityEngine.Random.Range(0, item.PossibleItemsAfterRecycler.Count);
			DropItemInfo recycledItem = item.PossibleItemsAfterRecycler[randomIndex];
			int randomAmount = UnityEngine.Random.Range(recycledItem.MinAmount, recycledItem.MaxAmount + 1);
			Item itemCreated = ItemManager.CreateByName(recycledItem.ShortName, randomAmount, recycledItem.SkinID);
			if (itemCreated == null)
			{
				return null;
			}
			if (recycledItem.Name != "default" && recycledItem.Name != "")
			{
				itemCreated.name = recycledItem.Name;
			}
			return itemCreated;
		}

		private RareMineralInfo FindMineral(Item item)
		{
			return config.Items.FirstOrDefault(a => config.ItemShortName == item.info.shortname && a.SkinID == item.skin);
		}

		private bool IsMineral(Item item)
		{
			return item != null && FindMineral(item) != null;
		}

		private bool IsSpecialTool(Item item)
		{
			return item != null && item.info.shortname == config.SpecialTool.ShortName && item.skin == config.SpecialTool.SkinID;
		}

		private bool CanCookableMineral(Item item)
		{
			RareMineralInfo itemDef = FindMineral(item);
			return itemDef != null && itemDef.ItemAfterSmelting != null;
		}

		private bool CanRecyclerMineral(Item item)
		{
			RareMineralInfo itemDef = FindMineral(item);
			return itemDef != null && itemDef.PossibleItemsAfterRecycler != null;
		}

		private Item CreateMineral(string id, int amount)
		{
			RareMineralInfo itemDef = config.Items.FirstOrDefault(a => a.ID == id);
			if (itemDef == null)
			{
				return null;
			}
			Item item = ItemManager.CreateByName(config.ItemShortName, amount, itemDef.SkinID);
			item.name = itemDef.Name;
			return item;
		}

		private Item CreateSpecialTool()
		{
			Item item = ItemManager.CreateByName(config.SpecialTool.ShortName, 1, config.SpecialTool.SkinID);
			item.name = config.SpecialTool.Name;
			return item;
		}

		private readonly Dictionary<string, long> replyDelay = new Dictionary<string, long>();

		private void PlayerReply(IPlayer player, string messageId)
		{
			if (player == null)
			{
				return;
			}
			long lastUnix;
			System.DateTimeOffset now = System.DateTime.UtcNow;
			var currentUnix = now.ToUnixTimeSeconds();
			if (replyDelay.TryGetValue(player.Id, out lastUnix))
			{
				if (currentUnix < lastUnix)
				{
					return;
				}
			}
			replyDelay[player.Id] = currentUnix + 2;
			player.Reply(GetLang(messageId));
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		private string GetLang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

		private int GetItemId(string shortname) => ItemManager.FindItemDefinition(shortname).itemid;

		private bool TryPull(float chance)
		{
			var random = new System.Random();
			double percent = random.NextDouble() * 100;
			if (percent < chance)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		private List<T> Shuffle<T>(List<T> list)
		{
			var rng = new System.Random();
			int n = list.Count;
			while (n > 1)
			{
				n--;
				int k = rng.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
			return list;
		}
		#endregion
	}
}