using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Game.Rust.Cui;
using System;
using System.Diagnostics;
using System.Globalization;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Auto Recycler", "M&B-Studios & Mevent", "1.0.7")]
	public class AutoRecycler : RustPlugin
	{
		#region Classes

		public struct EntityAndPlayer
		{
			public BaseEntity Entity;
			public BasePlayer Player;
		}

		#endregion

		#region Fields

		private const string
			Layer = "ui.AutoRecycler.bg",
			PermAdmin = "autorecycler.admin",
			PermAutorec = "autorecycler.autorec",
			PermRecvirtual = "autorecycler.recvirtual",
			PermRecyclercratetest = "autorecycler.recyclercratetest",
			PermCharacteristics = "autorecycler.characteristics",
			PermRecboxBuy = "autorecycler.recboxbuy";

		private Dictionary<ulong, bool> PlayerAutoRecState = new Dictionary<ulong, bool>();

		private readonly Dictionary<ulong, EntityAndPlayer> _recyclers = new Dictionary<ulong, EntityAndPlayer>();
		private const int ItemsPerRow = 7;
		private const int ItemsPerPage = 35;


		private readonly Dictionary<string, List<ItemAmount>> Chances = new()
		{
			["rifle.ak"] = new()
			{
				new()
				{
					Shortname = "riflebody",
					Chance = 50f
				}
			},
			["ammo.grenadelauncher.he"] = new()
			{
				new()
				{
					Shortname = "explosives",
					Chance = 25f
				},
				new()
				{
					Shortname = "metalpipe",
					Chance = 25f
				}
			},
			["ammo.grenadelauncher.smoke"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 25f
				}
			},
			["pistol.m92"] = new()
			{
				new()
				{
					Shortname = "semibody",
					Chance = 50f
				},
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				},
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				}
			},
			["smg.mp5"] = new()
			{
				new()
				{
					Shortname = "smgbody",
					Chance = 50f
				}
			},
			["shotgun.pump"] = new()
			{
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				}
			},
			["pistol.python"] = new()
			{
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				}
			},
			["pistol.revolver"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["rifle.semiauto"] = new()
			{
				new()
				{
					Shortname = "semibody",
					Chance = 50f
				},
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				}
			},
			["pistol.semiauto"] = new()
			{
				new()
				{
					Shortname = "semibody",
					Chance = 50f
				},
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["smg.thompson"] = new()
			{
				new()
				{
					Shortname = "smgbody",
					Chance = 50f
				},
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				}
			},
			["holosight"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 0f
				}
			},
			["weapon.mod.lasersight"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["paddle"] = new()
			{
				new()
				{
					Shortname = "metalblade",
					Chance = 50f
				}
			},
			["knife.combat"] = new()
			{
				new()
				{
					Shortname = "metal.refined",
					Chance = 50f
				}
			},
			["speargun"] = new()
			{
				new()
				{
					Shortname = "propanetank",
					Chance = 50f
				}
			},
			["stone.spear"] = new()
			{
				new()
				{
					Shortname = "spear.wooden",
					Chance = 50f
				}
			},
			["cleaver.salvaged"] = new()
			{
				new()
				{
					Shortname = "roadsigns",
					Chance = 50f
				}
			},
			["sword.salvaged"] = new()
			{
				new()
				{
					Shortname = "metalblade",
					Chance = 50f
				}
			},
			["gun.lmg.m2"] = new()
			{
				new()
				{
					Shortname = "riflebody",
					Chance = 50f
				}
			},
			["gun.hmlmg"] = new()
			{
				new()
				{
					Shortname = "riflebody",
					Chance = 50f
				}
			},
			["rifle.bolt"] = new()
			{
				new()
				{
					Shortname = "riflebody",
					Chance = 50f
				},
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				}
			},
			["gun.mgl"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["gun.rocket.launcher"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["trap.flameturret"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["trap.bear"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["tool.binoculars"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["tool.instant_camera"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["jackhammer"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["rf.detonator"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["axe.salvaged"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["hammer.salvaged"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["icepick.salvaged"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["explosive.satchet"] = new()
			{
				new()
				{
					Shortname = "stash.small",
					Chance = 50f
				},
				new()
				{
					Shortname = "rope",
					Chance = 50f
				}
			},
			["torch"] = new()
			{
				new()
				{
					Shortname = "cloth",
					Chance = 50f
				},
				new()
				{
					Shortname = "lowgradefuel",
					Chance = 50f
				}
			},
			["lowgradefuel"] = new()
			{
				new()
				{
					Shortname = "fat.animal",
					Chance = 38f
				},
				new()
				{
					Shortname = "cloth",
					Chance = 13f
				}
			},
			["fogmachine"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["snowmachine"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["drone"] = new()
			{
				new()
				{
					Shortname = "cctv.camera",
					Chance = 50f
				}
			},
			["skull_fire_pit"] = new()
			{
				new()
				{
					Shortname = "skull.human",
					Chance = 50f
				}
			},
			["planter.small"] = new()
			{
				new()
				{
					Shortname = "tarp",
					Chance = 50f
				}
			},
			["tunalight"] = new()
			{
				new()
				{
					Shortname = "can.tuna.empty",
					Chance = 50f
				}
			},
			["water.barrel"] = new()
			{
				new()
				{
					Shortname = "tarp",
					Chance = 50f
				}
			},
			["water.purifier"] = new()
			{
				new()
				{
					Shortname = "propanetank",
					Chance = 50f
				}
			},
			["boogieboard"] = new()
			{
				new()
				{
					Shortname = "tarp",
					Chance = 50f
				}
			},
			["innertube"] = new()
			{
				new()
				{
					Shortname = "tarp",
					Chance = 50f
				}
			},
			["paddlingpool"] = new()
			{
				new()
				{
					Shortname = "tarp",
					Chance = 50f
				}
			},
			["skullspikes"] = new()
			{
				new()
				{
					Shortname = "skull.human",
					Chance = 50f
				}
			},
			["telephone"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["autoturret"] = new()
			{
				new()
				{
					Shortname = "targeting.computer",
					Chance = 50f
				},
				new()
				{
					Shortname = "cctv.camera",
					Chance = 50f
				}
			},
			["computerstation"] = new()
			{
				new()
				{
					Shortname = "targeting.computer",
					Chance = 50f
				},
				new()
				{
					Shortname = "electric.rf.broadcaster",
					Chance = 50f
				},
				new()
				{
					Shortname = "electric.rf.receiver",
					Chance = 50f
				}
			},
			["elevator"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["electric.solarpanel.large"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["electric.battery.rechargable.medium"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["modularcarlift"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["ptz.cctv.camera"] = new()
			{
				new()
				{
					Shortname = "cctv.camera",
					Chance = 50f
				}
			},
			["electric.pressurepad"] = new()
			{
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				},
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["electric.rf.broadcaster"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["rf_pager"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["electric.rf.receiver"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["target.reactive"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["smart.alarm"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["smart.switch"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["storage.monitor"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["electric.teslacoil"] = new()
			{
				new()
				{
					Shortname = "techparts",
					Chance = 50f
				}
			},
			["waterpump"] = new()
			{
				new()
				{
					Shortname = "gears",
					Chance = 50f
				}
			},
			["barricade.wood"] = new()
			{
				new()
				{
					Shortname = "rope",
					Chance = 50f
				}
			},
			["door.closer"] = new()
			{
				new()
				{
					Shortname = "metalspring",
					Chance = 50f
				}
			},
			["floor.ladder.hatch"] = new()
			{
				new()
				{
					Shortname = "ladder.wooden.wall",
					Chance = 50f
				}
			},
			["water.catcher.small"] = new()
			{
				new()
				{
					Shortname = "tarp",
					Chance = 50f
				}
			},
			["floor.triangle.ladder.hatch"] = new()
			{
				new()
				{
					Shortname = "ladder.wooden.wall",
					Chance = 50f
				}
			},
			["barricade.woodwire"] = new()
			{
				new()
				{
					Shortname = "rope",
					Chance = 50f
				}
			},
			["ducttape"] = new()
			{
				new()
				{
					Shortname = "glue",
					Chance = 50f
				}
			},
			["shoes.boots"] = new()
			{
				new()
				{
					Shortname = "sewingkit",
					Chance = 50f
				}
			},
			["coffeecan.helmet"] = new()
			{
				new()
				{
					Shortname = "sewingkit",
					Chance = 50f
				}
			},
			["boots.frog"] = new()
			{
				new()
				{
					Shortname = "tarp",
					Chance = 50f
				}
			},
			["heavy.plate.helmet"] = new()
			{
				new()
				{
					Shortname = "sheetmetal",
					Chance = 50f
				}
			},
			["heavy.plate.pants"] = new()
			{
				new()
				{
					Shortname = "sheetmetal",
					Chance = 50f
				}
			},
			["hoodie"] = new()
			{
				new()
				{
					Shortname = "sewingkit",
					Chance = 50f
				}
			},
			["burlap.gloves"] = new()
			{
				new()
				{
					Shortname = "sewingkit",
					Chance = 50f
				}
			},
			["lumberjack hoodie"] = new()
			{
				new()
				{
					Shortname = "sewingkit",
					Chance = 50f
				}
			},
			["attire.nesthat"] = new()
			{
				new()
				{
					Shortname = "skull.wolf",
					Chance = 50f
				}
			},
			["hat.oxmask"] = new()
			{
				new()
				{
					Shortname = "skull.wolf",
					Chance = 50f
				}
			},
			["pants"] = new()
			{
				new()
				{
					Shortname = "sewingkit",
					Chance = 50f
				}
			},
			["hat.ratmask"] = new()
			{
				new()
				{
					Shortname = "skull.wolf",
					Chance = 50f
				}
			},
			["jacket.snow"] = new()
			{
				new()
				{
					Shortname = "sewingkit",
					Chance = 50f
				}
			},
			["hat.wolf"] = new()
			{
				new()
				{
					Shortname = "skull.wolf",
					Chance = 50f
				}
			},
			["wood.armor.pants"] = new()
			{
				new()
				{
					Shortname = "rope",
					Chance = 50f
				}
			},
			["wood.armor.jacket"] = new()
			{
				new()
				{
					Shortname = "rope",
					Chance = 50f
				}
			},
			["ammo.grenadelauncher.he"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 25f
				},
				new()
				{
					Shortname = "explosives",
					Chance = 25f
				}
			},
			["ammo.grenadelauncher.smoke"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 25f
				}
			},
			["arrow.fire"] = new()
			{
				new()
				{
					Shortname = "cloth",
					Chance = 25f
				}
			},
			["ammo.rocket.hv"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				}
			},
			["ammo.rocket.seeker"] = new()
			{
				new()
				{
					Shortname = "metalpipe",
					Chance = 50f
				},
				new()
				{
					Shortname = "techparts",
					Chance = 25f
				}
			}
		};

		private List<string> _itemBlacklist = new List<string>
		{
			"attackhelicopter",
			"minicopter",
			"transporthelicopter",
			"rhib",
			"rowboat",
			"tugboat",
			"submarinesolo",
			"submarineduo",
			"snowmobile",
			"snowmobiletomaha",
			"locomotive",
			"wagon",
			"workcart",
			"mlrs",
			"scraptransportheli.repair",
			"vehicle.chassis",
			"vehicle.module"
		};

		private List<ItemCategory> Categories = new();

		private Dictionary<ulong, List<string>> _players = new();

		private enum ActionType : byte
		{
			AutoRec = 0,
			RecVirtual = 1,
			RecyclerCrateTest = 2,
			Characteristics = 3
		}

		#endregion

		#region Hooks

		private void Init() => Unsubscribe(nameof(CanNetworkTo));

		private object CanNetworkTo(BaseNetworkable e, BasePlayer p)
		{
			if (e == null || p == null || p == e || p.IsAdmin) return null;

			if (IsRecycleBox(e))
				return (PlayerFromRecycler(e.net.ID.Value)?.userID ?? 0) == p.userID;
			return null;
		}

		private void OnServerSave() => SaveData();

		private void Unload()
		{
			SaveData();

			foreach (var player in BasePlayer.activePlayerList) UI_Destroy(player);

			DestroyRecyclers();
		}

		private void OnServerInitialized()
		{
			LoadData();
			RegisterPermissions();
			RegisterCommands();

			Dictionary<string, List<ItemAmount>> itemsWhichRecycleResult = new();
			foreach (var x in ItemManager.itemList)
			{
				if (_itemBlacklist.Contains(x.shortname))
					continue;

				var item = ItemManager.Create(x);
				if (item == null)
				{
					PrintError($"Item is null - {x.shortname}");
					continue;
				}

				if (item.info.Blueprint == null)
				{
					continue;
				}

				itemsWhichRecycleResult.Add(x.shortname, new());

				if (item.info.Blueprint.scrapFromRecycle > 0)
				{
					itemsWhichRecycleResult[x.shortname].Add(new ItemAmount
					{
						Shortname = "scrap",
						Amount = item.info.Blueprint.scrapFromRecycle,
						Enabled = true,
						Chance = 100
					});
				}

				foreach (var y in item.info.Blueprint.ingredients.Where(x => x.itemDef.shortname != "scrap"))
				{
					if (itemsWhichRecycleResult[x.shortname].Any(x => x.Shortname == y.itemDef.shortname))
						continue;

					float chance = -1f;

					if (Chances.ContainsKey(x.shortname))
					{
						var buf = Chances[x.shortname].FirstOrDefault(z => z.Shortname == y.itemDef.shortname);

						if (buf != null)
							chance = buf.Chance;
					}

					float num4 = y.amount / x?.Blueprint?.amountToCreate ?? 1;
					int num5 = 0;
					if (num4 <= 1f)
					{
						for (int j = 0; j < 1; j++)
						{
							if (UnityEngine.Random.Range(0f, 1f) <= num4)
							{
								num5++;
							}
						}
					}
					else
					{
						num5 = Mathf.CeilToInt(Mathf.Clamp(num4 * UnityEngine.Random.Range(1f, 1f), 0f, y.amount)) / 2;
					}


					// if (num5 > 0)
					// {
					//     int num6 = Mathf.CeilToInt((float)num5 / (float)y.itemDef.stackable);
					// }
					itemsWhichRecycleResult[x.shortname].Add(new ItemAmount
					{
						//
						Shortname = y.itemDef.shortname,
						Amount = (int) num5,
						Enabled = true,
						Chance = chance < 0 ? 100f : chance
					});
				}
			}

			if (itemsWhichRecycleResult.Count > config.RecycleItems.Count)
			{
				foreach (var item in itemsWhichRecycleResult.Where(x => !config.RecycleItems.ContainsKey(x.Key)))
				{
					config.RecycleItems.Add(item.Key, new ItemSettings()
					{
						IsEnabled = true,
						ItemAmount = item.Value
					});
				}

				SaveConfig();
			}

			FillCategories();
		}

		private void OnItemAddedToContainer(ItemContainer container, Item item)
		{
			if (container == null || item == null)
				return;
			var player = container.playerOwner;

			if (item.skin == 1)
			{
				item.skin = 0;
				return;
			}

			var itemShortname = item.info.shortname;
			if (!config.RecycleItems.TryGetValue(itemShortname, out var recycleItems))
			{
				return;
			}

			if (player == null)
			{
				if (container.entityOwner == null)
					return;

				if (!container.entityOwner.TryGetComponent<StorageContainer>(out var comp))
					return;
				if (comp.HasFlag(BaseEntity.Flags.Disabled))
					return;

				var skinid = comp.skinID;

				if (config._CrateRecycler.SkinID == skinid)
				{
					RecycleItem(player, item, recycleItems.ItemAmount, comp);
				}

				return;
			}

			if (!AutoRecycleItem(player, itemShortname))
				return;


			RecycleItem(player, item, recycleItems.ItemAmount);
		}

		private object OnEntityVisibilityCheck(BaseEntity ent, BasePlayer player, uint id, string debugName,
			float maximumDistance)
		{
			var recycler = ent as Recycler;
			if (recycler == null) return null;

			var playerFromRec = PlayerFromRecycler(ent.net.ID.Value);

			if (playerFromRec != null && player == playerFromRec)
			{
				return true;
			}

			return null;
		}

		private object OnEntityDistanceCheck(BaseEntity ent, BasePlayer player, uint id, string debugName,
			float maximumDistance, bool checkParent)
		{
			var recycler = ent as Recycler;
			if (recycler == null) return null;

			var playerFromRec = PlayerFromRecycler(ent.net.ID.Value);

			if (playerFromRec != null && playerFromRec == player)
			{
				return true;
			}

			return null;
		}

		private object OnLootEntityEnd(BasePlayer player, Recycler recycler)
		{
			var playerRecycler = RecyclerFromPlayer(player.userID);

			if (playerRecycler == null)
				return null;

			if (playerRecycler == recycler)
			{
				foreach (var x in recycler.inventory.itemList.ToArray())
					x.Drop(player.GetDropPosition(), player.GetDropVelocity());

				DestroyRecycler(recycler);
			}

			return null;
		}

		#endregion

		#region Methods

		private void CreateRecycler(BasePlayer p)
		{
			var posAdd = p.transform.position.y > 500 ? Vector3.down * 40 : Vector3.down * 500;

			var recycler =
				GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab",
						p.transform.position + posAdd) as
					Recycler;
			if (recycler == null) return;

			recycler.enableSaving = false;
			recycler.globalBroadcast = true;
			recycler.Spawn();

			recycler.SetFlag(BaseEntity.Flags.Locked, true);
			recycler.UpdateNetworkGroup();

			if (!recycler.isSpawned) return;
			recycler.gameObject.layer = 0;
			recycler.SendNetworkUpdateImmediate(true);
			Subscribe(nameof(CanNetworkTo));
			OpenContainer(p, recycler);

			_recyclers.Add(recycler.net.ID.Value, new EntityAndPlayer {Entity = recycler, Player = p});
		}

		public BasePlayer PlayerFromRecycler(ulong netID) =>
			!IsRecycler(netID) ? null : _recyclers[netID].Player;

		public bool IsRecycler(ulong netID) => _recyclers.ContainsKey(netID);

		public bool IsRecycleBox(BaseNetworkable e)
		{
			if (e == null || e.net == null) return false;
			return IsRecycler(e.net.ID.Value);
		}

		private void DestroyRecycler(BaseEntity e)
		{
			if (IsRecycleBox(e))
			{
				_recyclers.Remove(e.net.ID.Value);
				e.Kill();
			}

			if (_recyclers.Count == 0) Unsubscribe(nameof(CanNetworkTo));
		}

		private void DestroyRecyclers()
		{
			while (_recyclers.Count > 0)
				DestroyRecycler(_recyclers.FirstOrDefault().Value.Entity);

			Unsubscribe(nameof(CanNetworkTo));
			_recyclers.Clear();
		}

		private void OpenContainer(BasePlayer p, StorageContainer con)
		{
			timer.In(.1f, () =>
			{
				p.EndLooting();
				if (!p.inventory.loot.StartLootingEntity(con, false)) return;
				p.inventory.loot.AddContainer(con.inventory);
				p.inventory.loot.SendImmediate();
				p.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", p), con.panelName);
				p.SendNetworkUpdate();
			});
		}

		private void SetRecyclingState(BasePlayer player, string shortname, bool flag)
		{
			if (!_players.ContainsKey(player.userID))
				_players.Add(player.userID, new());

			if (flag)
			{
				if (!_players[player.userID].Contains(shortname))
					_players[player.userID].Add(shortname);

				return;
			}

			_players[player.userID].Remove(shortname);
		}

		private bool AutoRecycleItem(BasePlayer player, string shortname)
		{
			if (PlayerAutoRecState.TryGetValue(player.userID, out var state))
			{
				if (state == false)
					return false;
			}

			if (!_players.ContainsKey(player.userID))
				return false;

			if (!config.RecycleItems.TryGetValue(shortname, out var item))
				return false;

			if (!item.IsEnabled)
				return false;

			if (!_players[player.userID].Contains(shortname))
				return false;

			return true;
		}

		private bool CanUseAction(BasePlayer player, ActionType type)
		{
			if (player == null)
				return true;

			if (HasPermission(player, PermAdmin))
				return true;

			return type switch
			{
				ActionType.Characteristics => HasPermission(player, PermCharacteristics),
				ActionType.AutoRec => HasPermission(player, PermAutorec),
				ActionType.RecVirtual => HasPermission(player, PermRecvirtual),
				ActionType.RecyclerCrateTest => HasPermission(player, PermRecyclercratetest),
				_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
			};
		}

		private void FillCategories()
		{
			Categories.Add(ItemCategory.All);
			foreach (var x in config.RecycleItems)
			{
				var def = ItemManager.FindItemDefinition(x.Key);

				if (Categories.Contains(def.category))
					continue;
				Categories.Add(def.category);
			}
		}

		private void RegisterCommands()
		{
			foreach (var x in config.VirtualRecyclerCommands)
				cmd.AddChatCommand(x, this, nameof(CmdOpenVirtualRecycler));
			foreach (var x in config.AutoRecyclerUICommands)
				cmd.AddChatCommand(x, this, nameof(CmdAutorec));
		}

		private void RegisterPermissions()
		{
			permission.RegisterPermission(PermAdmin, this);
			permission.RegisterPermission(PermAutorec, this);
			permission.RegisterPermission(PermCharacteristics, this);
			permission.RegisterPermission(PermRecyclercratetest, this);
			permission.RegisterPermission(PermRecvirtual, this);
			permission.RegisterPermission(PermRecboxBuy, this);
		}

		private bool HasPermission(BasePlayer player, string perm) => player.IPlayer.HasPermission(perm);
		private ItemCategory GetItemCategory(string shortname) => ItemManager.FindItemDefinition(shortname).category;

		private IEnumerable<KeyValuePair<string, List<ItemAmount>>> GetItemsByCategory(BasePlayer player,
			ItemCategory category)
		{
			var playerHasPermission = CanUseAction(player, ActionType.Characteristics);

			if (category == ItemCategory.All)
				return playerHasPermission
					? config.RecycleItems
						.Select(x =>
							new KeyValuePair<string, List<ItemAmount>>(x.Key, x.Value.ItemAmount))
					: config.RecycleItems
						.Where(x => x.Value.IsEnabled)
						.Select(x =>
							new KeyValuePair<string, List<ItemAmount>>(x.Key, x.Value.ItemAmount));

			return playerHasPermission
				? config.RecycleItems
					.Where(x => ItemManager.FindItemDefinition(x.Key).category == category)
					.Select(y => new KeyValuePair<string, List<ItemAmount>>(y.Key, y.Value.ItemAmount))
				: config.RecycleItems
					.Where(x =>
						ItemManager.FindItemDefinition(x.Key).category == category && x.Value.IsEnabled)
					.Select(y => new KeyValuePair<string, List<ItemAmount>>(y.Key, y.Value.ItemAmount));
		}

		public BaseEntity RecyclerFromPlayer(ulong uid)
		{
			foreach (EntityAndPlayer eap in _recyclers.Values)
				if (eap.Player.userID == uid)
					return eap.Entity;
			return null;
		}

		public void OpenRecycler(BasePlayer p)
		{
			if (p == null) return;
			BaseEntity result = RecyclerFromPlayer(p.userID);
			if (result == null) CreateRecycler(p);
			else
			{
				DestroyRecycler(result);
				CreateRecycler(p);
			}
		}

		private bool IsRare(float rare) => rare >= UnityEngine.Random.Range(0.0f, 100.1f);

		private void RecycleItem(BasePlayer player, Item item, List<ItemAmount> recycleItems,
			StorageContainer sc = null)
		{
			int originalItemCount = item.amount;
			item.Remove();
			foreach (var recycleItem in recycleItems)
			{
				if (!recycleItem.Enabled) continue;

				int totalAmount =
					recycleItem.Amount *
					originalItemCount;
				if (recycleItem.Chance < 99f)
				{
					int availableAmount = 0;
					for (int i = 0; i < originalItemCount; i++)
						if (IsRare(recycleItem.Chance))
							availableAmount++;

					if (availableAmount <= 0)
						continue;

					totalAmount = recycleItem.Amount * availableAmount;
				}

				var definition = ItemManager.FindItemDefinition(recycleItem.Shortname);
				if (definition == null)
				{
					Puts($"Item-Definition f r '{recycleItem.Shortname}' nicht gefunden.");
					continue;
				}


				var remaining = (float) totalAmount;
				while (remaining > 0)
				{
					var newItem = ItemManager.CreateByItemID(definition.itemid, 1);

					newItem.amount = remaining < definition.stackable ? (int) remaining : definition.stackable;

					if (newItem == null)
					{
						Puts($"Konnte Item '{recycleItem.Shortname}' nicht erstellen.");
						continue;
					}

					NextTick(() =>
					{
						Unsubscribe(nameof(OnItemAddedToContainer));
						if (sc != null)
						{
							if (!sc.inventory.GiveItem(newItem))
							{
								newItem.Drop(sc.GetDropPosition(), sc.GetDropVelocity());
								Puts($"Inventar voll. Dropping Item '{recycleItem.Shortname}' auf den Boden.");
							}
						}
						else if (!player.inventory.GiveItem(newItem))
						{
							newItem.Drop(player.GetDropPosition(), player.GetDropVelocity());
							Puts($"Inventar voll. Dropping Item '{recycleItem.Shortname}' auf den Boden.");
						}

						Subscribe(nameof(OnItemAddedToContainer));
					});

					remaining -= newItem.amount;
				}
			}
		}

		#endregion

		#region UI

		private void UI_UpdateItem(BasePlayer player, string shortname)
		{
			CuiHelper.DestroyUi(player, Layer + ".main" + ".items.div" + $".item.{shortname}" + ".bg");
			var container = new CuiElementContainer();

			var canRecycle = AutoRecycleItem(player, shortname);

			container.Add(new CuiPanel
				{
					CursorEnabled = false,
					Image =
					{
						Color = canRecycle
							? "0.5108469 0.735849 0.4408152 0.3882353"
							: "0.8156863 0.5568628 0.4745098 0.3882353"
					},
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-39.544 -39.544",
						OffsetMax = "39.544 39.544"
					}
				}, Layer + ".main" + ".items.div" + $".item.{shortname}",
				Layer + ".main" + ".items.div" + $".item.{shortname}" + ".bg");

			var itemdef = ItemManager.itemDictionaryByName[shortname];

			container.Add(new CuiPanel
				{
					CursorEnabled = false,
					Image = {Color = "1 1 1 1", ItemId = itemdef.itemid},
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-39.544 -39.544",
						OffsetMax = "39.544 39.544"
					}
				}, Layer + ".main" + ".items.div" + $".item.{shortname}.bg",
				Layer + ".main" + ".items.div" + $".item.{shortname}" + ".image");

			container.Add(new CuiButton
				{
					Button =
					{
						Color = canRecycle ? "0.5108469 0.735849 0.4408152 1" : "0.8156863 0.5568628 0.4745098 1",
						Command = $"ar.setstate {shortname} {!canRecycle}",
						Sprite = canRecycle ? "assets/icons/check.png" : "assets/icons/close.png"
					},
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "15.026 15.725",
						OffsetMax = "36.174 36.875"
					}
				}, Layer + ".main" + ".items.div" + $".item.{shortname}.bg",
				Layer + ".main" + ".items.div" + $".item.{shortname}" + ".enabled");

			if (CanUseAction(player, ActionType.Characteristics))
			{
				var isEnabled = config.RecycleItems[shortname].IsEnabled;

				container.Add(new CuiButton
					{
						Button =
						{
							Color = isEnabled ? "0.09545212 0.3679245 0.1133182 0.8235294" : "1 0.03030553 0 0.8235294",
							Command = $"ar.togglestate {shortname} {!isEnabled}"
						},
						Text =
						{
							Text = isEnabled ? "ENABLED" : "DISABLED", Font = "robotocondensed-bold.ttf", FontSize = 13,
							Align = TextAnchor.MiddleCenter,
							Color = isEnabled ? "0.263648 1 0 1" : "0.6226415 0.1380384 0.1380384 1"
						},
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-39.544 -39.543",
							OffsetMax = "39.544 -20.039"
						}
					}, Layer + ".main" + ".items.div" + $".item.{shortname}.bg",
					Layer + ".autorecycler" + ".main" + ".items.div" + $".item.{shortname}" + ".adminbtn");
			}

			CuiHelper.DestroyUi(player, Layer + ".search.bg");
			CuiHelper.AddUi(player, container);
		}

		private void UI_DrawSearch(BasePlayer player)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = {Color = "0.5254902 0.5019608 0.4666667 1"},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-104.786 -46.797",
					OffsetMax = "104.786 -20.326"
				}
			}, Layer + ".main" + ".header.div", Layer + ".main" + ".header.div" + ".search.bg");

			container.Add(new CuiElement
			{
				Name = Layer + ".main" + ".header.div" + ".search.bg" + ".input",
				Parent = Layer + ".main" + ".header.div" + ".search.bg",
				Components =
				{
					new CuiInputFieldComponent
					{
						Color = "0.8078431 0.7803922 0.7411765 1", Font = "robotocondensed-bold.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, CharsLimit = 20, IsPassword = false, NeedsKeyboard = true
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-96.1 -13.235",
						OffsetMax = "96.172 13.236"
					}
				}
			});

			CuiHelper.DestroyUi(player, Layer + ".search.bg");
			CuiHelper.AddUi(player, container);
		}

		private void UI_DrawMain(BasePlayer player)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiElement()
			{
				Name = Layer + ".close",
				Parent = "Overlay",
				Components =
				{
					new CuiButtonComponent()
					{
						Command = "ar.close",
						Color = "0 0 0 0.5",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
					}
				}
			});

			container.Add(new CuiPanel
			{
				CursorEnabled = true,
				Image = {Color = "0.317 0.317 0.317 0.7490196"},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-434.913 -260.419",
					OffsetMax = "434.913 260.419"
				}
			}, "Overlay", Layer);

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = {Color = "1 1 1 0"},
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.09 -260.419", OffsetMax = "660.17 260.421"
				}
			}, Layer, Layer + ".main");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = {Color = "1 1 1 0"},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-330.04 219.767",
					OffsetMax = "330.04 260.42"
				}
			}, Layer + ".main", Layer + ".main" + ".header.div");

			container.Add(new CuiButton
			{
				Button = {Color = "1 1 1 0"},
				Text =
				{
					Text = "SEARCH", Font = "robotocondensed-bold.ttf", FontSize = 29, Align = TextAnchor.MiddleCenter,
					Color = "0.8078431 0.7803922 0.7411765 1"
				},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-76.091 -20.326",
					OffsetMax = "76.091 20.326"
				}
			}, Layer + ".main" + ".header.div", Layer + ".main" + ".header.div" + ".search.btn");

			UI_Destroy(player);
			CuiHelper.AddUi(player, container);

			UI_DrawCategories(player, ItemCategory.All);
			UI_DrawItems(player, ItemCategory.All);
		}

		private void UI_DrawItems(BasePlayer player, ItemCategory activeCategory, int page = 0)
		{
			CuiHelper.DestroyUi(player, Layer + ".main" + ".items.div");
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = {Color = "1 1 1 0.1490196"},
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "-0.04 -226.279", OffsetMax = "660.04 219.771"
				}
			}, Layer + ".main", Layer + ".main" + ".items.div");

			float minx = -305.04f;
			float maxx = -225.9524f;
			float miny = 143.9324f;
			float maxy = 223.02f;

			int i = 0;
			CuiHelper.AddUi(player, container);
			UI_DrawPages(player, activeCategory, page);
			float fadein = 0.01f;

			foreach (var item in GetItemsByCategory(player, activeCategory).Skip(page * ItemsPerPage)
				         .Take(ItemsPerPage))
			{
				fadein = Mathf.Lerp(0.01f, 0.5f, i / 16f);
				container.Clear();
				if (i % ItemsPerRow == 0 && i != 0)
				{
					minx = -305.04f;
					maxx = -225.9524f;
					miny -= 85.476f;
					maxy -= 85.476f;
				}

				container.Add(new CuiPanel
					{
						CursorEnabled = false,
						Image = {Color = "0.8156863 0.5568628 0.4745098 0.3882353", FadeIn = fadein},
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}",
							OffsetMax = $"{maxx} {maxy}"
						}
					}, Layer + ".main" + ".items.div", Layer + ".main" + ".items.div" + $".item.{item.Key}");


				minx += 85.496f;
				maxx += 85.496f;
				i++;
				CuiHelper.AddUi(player, container);
				UI_UpdateItem(player, item.Key);
			}
		}

		private void UI_DrawPages(BasePlayer player, ItemCategory activeCategory, int page = 0)
		{
			var container = new CuiElementContainer();

			var canNext = GetItemsByCategory(player, activeCategory).Skip((page + 1) * ItemsPerPage).Any();
			var canPrevious = page - 1 >= 0;

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = {Color = "0.317 0.317 0.317 0.6392157"},
				RectTransform =
					{AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 -34.166", OffsetMax = "118.486 -0.024"}
			}, Layer + ".main" + ".items.div", Layer + ".main" + ".items.div" + ".pages");

			container.Add(new CuiElement
			{
				Name = Layer + ".main" + ".items.div" + ".pages" + ".page.now",
				Parent = Layer + ".main" + ".items.div" + ".pages",
				Components =
				{
					new CuiTextComponent
					{
						Text = (page + 1).ToString(), Font = "robotocondensed-bold.ttf", FontSize = 16,
						Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-21.202 -17.071",
						OffsetMax = "21.202 17.071"
					}
				}
			});

			container.Add(new CuiButton
			{
				Button =
				{
					Color = "1 1 1 0", Command = canPrevious ? $"ar.page {activeCategory.ToString()} {page - 1}" : ""
				},
				Text =
				{
					Text = "<", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter,
					Color = canPrevious ? "0.8078431 0.7803922 0.7411765 1" : "0.5254902 0.5019608 0.4666667 0.7607843"
				},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-59.243 -17.071",
					OffsetMax = "-21.202 17.071"
				}
			}, Layer + ".main" + ".items.div" + ".pages", Layer + ".main" + ".items.div" + ".pages" + ".previous");

			container.Add(new CuiButton
			{
				Button =
					{Color = "1 1 1 0", Command = canNext ? $"ar.page {activeCategory.ToString()} {page + 1}" : ""},
				Text =
				{
					Text = ">", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter,
					Color = canNext ? "0.8078431 0.7803922 0.7411765 1" : "0.5254902 0.5019608 0.4666667 0.7607843"
				},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "21.202 -17.071",
					OffsetMax = "59.243 17.071"
				}
			}, Layer + ".main" + ".items.div" + ".pages", Layer + ".main" + ".items.div" + ".pages" + ".next");

			CuiHelper.DestroyUi(player, Layer + ".main.items.div.pages");
			CuiHelper.AddUi(player, container);
		}

//
		private void UI_DrawCategories(BasePlayer player, ItemCategory activeCategory)
		{
			var container = new CuiElementContainer();
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = {Color = "0.317 0.317 0.317 0.9137255"},
				RectTransform =
				{
					AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-209.746 -260.42", OffsetMax = "-0.09 260.42"
				}
			}, Layer, Layer + ".categoies");
			float minx = -104.83f;
			float maxx = 104.83f;
			float miny = 218.6598f;
			float maxy = 245.42f;

			for (int i = 0; i < Categories.Count; i++)
			{
				var category = Categories.ElementAtOrDefault(i);

				bool isActive = category == activeCategory;

				container.Add(new CuiButton
					{
						Button = {Color = "0 0 0 0", Command = $"ar.setcategory {category.ToString()}"},
						Text = {Text = ""},
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}",
							OffsetMax = $"{maxx} {maxy}"
						}
					}, Layer + ".categoies", Layer + ".categoies" + $".category.{i}");

				container.Add(new CuiElement
				{
					Name = Layer + ".categoies" + $".category.{i}" + ".name",
					Parent = Layer + ".categoies" + $".category.{i}",
					Components =
					{
						new CuiTextComponent
						{
							Text = category.ToString().ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 20,
							Align = TextAnchor.MiddleRight,
							Color = isActive ? "0.8078431 0.7803922 0.7411765 1" : "0.6470588 0.6235294 0.6 1"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-104.83 -20.88",
							OffsetMax = "95.141 20.88"
						}
					}
				});

				miny -= 26.76f;
				maxy -= 26.76f;
			}

			CuiHelper.DestroyUi(player, Layer + ".categories");
			CuiHelper.AddUi(player, container);
		}


		private void UI_Destroy(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, Layer + ".close");
			CuiHelper.DestroyUi(player, Layer);
		}

		#endregion

		#region Commands

		[ChatCommand("recboxtest")]
		private void CmdGiveRecBoxTest(BasePlayer player)
		{
			if (!player.IsAdmin)
				return;

			Item item = ItemManager.CreateByName(config._CrateRecycler.Shortname, 1000, config._CrateRecycler.SkinID);
			if (!string.IsNullOrEmpty(config._CrateRecycler.DisplayName))
				item.name = config._CrateRecycler.DisplayName;

			player.GiveItem(item);
		}

		[ChatCommand("autorectoggle")]
		private void CmdAutoRecToggle(BasePlayer player)
		{
			if (player == null)
				return;

			if (!PlayerAutoRecState.ContainsKey(player.userID))
				PlayerAutoRecState.Add(player.userID, true);

			PlayerAutoRecState[player.userID] = !PlayerAutoRecState[player.userID];

			player.ChatMessage(
				$"Autorecycling is now {(PlayerAutoRecState[player.userID] ? "<color=green>enabled</color>" : "<color=red>disabled</color>")}");
		}

		[ChatCommand("recbox")]
		private void CmdRecboxInfo(BasePlayer player)
		{
			string costInfo = "Recbox price:\n";
			foreach (var cost in config.RecboxCost)
			{
				costInfo += $"{cost.Amount}x {cost.Shortname}\n";
			}

			costInfo += "Use /recboxbuy to purchase.";
			SendReply(player, costInfo);
		}

		[ChatCommand("recboxbuy")]
		private void CmdBuyRecbox(BasePlayer player)
		{
			if (!permission.UserHasPermission(player.UserIDString, PermRecboxBuy))
			{
				SendReply(player, "You do not have permission to use this command.");
				return;
			}

			foreach (var cost in config.RecboxCost)
			{
				var itemDef = ItemManager.FindItemDefinition(cost.Shortname);
				if (itemDef == null) continue;
				var playerItemCount = player.inventory.GetAmount(itemDef.itemid);
				if (playerItemCount < cost.Amount)
				{
					SendReply(player, $"Not enough {cost.Shortname}.");
					return;
				}
			}

			foreach (var cost in config.RecboxCost)
			{
				var itemDef = ItemManager.FindItemDefinition(cost.Shortname);
				player.inventory.Take(null, itemDef.itemid, cost.Amount);
			}

			GiveRecBox(player);
			SendReply(player, "You have bought a Recbox.");
		}


		private void GiveRecBox(BasePlayer player)
		{
			Item item = ItemManager.CreateByName(config._CrateRecycler.Shortname, 1, config._CrateRecycler.SkinID);
			if (!string.IsNullOrEmpty(config._CrateRecycler.DisplayName))
				item.name = config._CrateRecycler.DisplayName;

			if (!player.inventory.GiveItem(item))
			{
				item.Drop(player.GetDropPosition(), player.GetDropVelocity());
				//Puts($"Failed to add Recbox to inventory of {player.displayName}, dropped on the ground.");
			}
			else
			{
				//Puts($"Recbox successfully given to {player.displayName}.");
			}
		}

		[ConsoleCommand("ar.togglestate")]
		private void CmdToggleState(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;

			if (!CanUseAction(arg.Player(), ActionType.Characteristics))
				return;

			string shortname = arg.Args[0];
			if (!bool.TryParse(arg.Args[1], out var state))
				return;

			if (!config.RecycleItems.ContainsKey(shortname))
				return;

			config.RecycleItems[shortname].IsEnabled = state;
			UI_UpdateItem(arg.Player(), shortname);

			SaveConfig();
		}

		[ConsoleCommand("recyclercrate.give")]
		private void CmdRecGive(ConsoleSystem.Arg arg)
		{
			if (arg.Player() != null)
				if (!arg.Player().IsAdmin)
					return;

			Item item = ItemManager.CreateByName(config._CrateRecycler.Shortname, 1, config._CrateRecycler.SkinID);
			if (!string.IsNullOrEmpty(config._CrateRecycler.DisplayName))
				item.name = config._CrateRecycler.DisplayName;

			if (arg.Args.IsNullOrEmpty())
			{
				if (arg.Player() == null)
					return;

				if (!arg.Player().inventory.GiveItem(item))
					item.Drop(arg.Player().GetDropPosition(), arg.Player().GetDropVelocity());
				return;
			}

			var target = FindPlayer(arg.Args[0]);

			if (target.IsNullOrEmpty())
			{
				Puts($"Player '{arg.Args[0]}' not found");
				return;
			}

			if (target.Count > 1)
			{
				Puts($"Finded many players:");
				foreach (var x in target.Select(x => x.displayName))
					Puts($" - {x}");
				return;
			}

			var pl = target.FirstOrDefault();

			if (!pl.inventory.GiveItem(item))
				item.Drop(pl.GetDropPosition(), pl.GetDropVelocity());
		}

		private List<BasePlayer> FindPlayer(string id)
		{
			List<BasePlayer> players = new List<BasePlayer>();
			players.AddRange(BasePlayer.activePlayerList.Where(x =>
				x.UserIDString == id || x.displayName.Contains(id, CompareOptions.IgnoreCase)));
			return players;
		}

		[ConsoleCommand("ar.setcategory")]
		private void CmdSetCategory(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;

			if (!Enum.TryParse(arg.Args[0], out ItemCategory category))
				return;

			UI_DrawCategories(arg.Player(), category);
			UI_DrawItems(arg.Player(), category);
		}

		[ConsoleCommand("ar.setstate")]
		private void CmdSetState(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;

			if (!bool.TryParse(arg.Args[1], out var state))
				return;

			var shortname = arg.Args[0];
			if (!config.RecycleItems.TryGetValue(shortname, out var item))
				return;
			if (!item.IsEnabled)
				return;

			SetRecyclingState(arg.Player(), shortname, state);
			UI_UpdateItem(arg.Player(), shortname);
		}

		[ConsoleCommand("ar.page")]
		private void CmdPage(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;

			BasePlayer player = arg.Player();
			if (!int.TryParse(arg.Args[1], out var page))
				return;

			if (!Enum.TryParse(arg.Args[0], out ItemCategory category))
				return;


			UI_DrawItems(player, category, page);
		}

		[ConsoleCommand("ar.close")]
		private void CmdClose(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null)
				return;

			UI_Destroy(arg.Player());
		}

		private void CmdAutorec(BasePlayer player)
		{
			if (!CanUseAction(player, ActionType.AutoRec))
				return;

			UI_DrawMain(player);
		}

		private void CmdOpenVirtualRecycler(BasePlayer player)
		{
			if (!CanUseAction(player, ActionType.RecVirtual))
				return;

			OpenRecycler(player);
		}

		#endregion

		#region Config

		private ConfigData config;

		private class ConfigData
		{
			[JsonProperty("Auto recycler UI commands", Order = 1)]
			public List<string> AutoRecyclerUICommands;

			[JsonProperty("Virtual recycler commands", Order = 0)]
			public List<string> VirtualRecyclerCommands;

			[JsonProperty("Crate-recycler settings", Order = 5)]
			public CrateRecycler _CrateRecycler;

			[JsonProperty("Items to Recycle", Order = 100)]
			public Dictionary<string, ItemSettings> RecycleItems { get; set; }

			[JsonProperty("Recbox cost", Order = 6)] // Hinzugef gte Konfiguration f r die Recbox-Kosten
			public List<ItemAmount> RecboxCost;
		}

		private class CrateRecycler
		{
			public string Shortname;
			public string DisplayName;
			public ulong SkinID;
		}

		private class ItemAmount
		{
			[JsonIgnore] public string GUID;

			[JsonProperty("Shortname")] public string Shortname { get; set; }

			[JsonProperty("Amount")] public int Amount { get; set; }


			[JsonProperty("Chance (0.0 - 100.0)")] public float Chance;

			[JsonProperty("Enabled")] public bool Enabled { get; set; } = true;
		}

		private class ItemSettings
		{
			[JsonProperty("Items")] public List<ItemAmount> ItemAmount;
			[JsonProperty("Is enabled?")] public bool IsEnabled;

			public IEnumerable<ItemAmount> GetItemsByCategory(ItemCategory category) =>
				ItemAmount.Where(x => ItemManager.FindItemDefinition(x.Shortname).category == category);
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			config = Config.ReadObject<ConfigData>();
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig()
		{
			config = new ConfigData
			{
				RecycleItems = new(),
				_CrateRecycler = new CrateRecycler()
				{
					DisplayName = "Crate-recycler",
					Shortname = "box.wooden.large",
					SkinID = 3100475476
				},
				VirtualRecyclerCommands = new()
				{
					"virtualrec",
					"vrec",
					"recvirtual"
				},
				AutoRecyclerUICommands = new()
				{
					"autorec",
					"ar",
					"autorecycler"
				},
				RecboxCost = new List<ItemAmount>() // Standardkosten f r die Recbox
				{
					new ItemAmount {Shortname = "scrap", Amount = 100} // Beispielkosten
				}
			};
		}

		#endregion

		#region Data

		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Title}/players", _players);
		}

		void LoadData()
		{
			_players = Interface.Oxide?.DataFileSystem?.ReadObject<Dictionary<ulong, List<string>>>($"{Title}/players")
			           ?? new Dictionary<ulong, List<string>>();
		}

		#endregion
	}
}