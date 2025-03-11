using Oxide.Core;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("Supply Signal Limit", "bsdinis", "0.0.6")]
	class SupplySignalLimit : RustPlugin
	{
		void Init()
		{
			try
			{
				config = Config.ReadObject<ConfigData>();
				if (config == null)
				{
					throw new Exception();
				}
				else
				{
					SaveConfig();
				}
			}
			catch
			{
				PrintError("CONFIG FILE IS INVALID!\nCheck config file and reload SupplySignalLimit.");
				Interface.Oxide.UnloadPlugin(Name);
				return;
			}

			Unsubscribe(nameof(OnEntitySpawned));
			Unsubscribe(nameof(OnEntityKill));
			Unsubscribe(nameof(OnSupplyDropLanded));
			if (!string.IsNullOrWhiteSpace(config.BypassPermission) && !permission.PermissionExists(config.BypassPermission))
			{
				permission.RegisterPermission(config.BypassPermission, this);
			}
		}

		void OnServerInitialized()
		{
			foreach (BaseNetworkable entity in BaseNetworkable.serverEntities)
			{
				Count(entity);
			}
			Subscribe(nameof(OnEntitySpawned));
			Subscribe(nameof(OnEntityKill));
			Subscribe(nameof(OnSupplyDropLanded));
		}

		protected override void LoadDefaultConfig()
		{
			config = new ConfigData
			{
				Max = 10,
				BypassPermission = "supplysignallimit.bypass"
			};
		}

		protected override void SaveConfig() => Config.WriteObject(config, true);

		ConfigData config;
		class ConfigData
		{
			public int Max;
			public string BypassPermission;
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(
				new Dictionary<string, string>
				{
					["Limit"] = "There can only be {0} active Supply Signals at any time, please wait a few seconds."
				},
				this,
				"en"
			);
		}

		uint signals;
		uint planes;
		Dictionary<ulong, Timer> drops = new Dictionary<ulong, Timer>();

		void Count(BaseNetworkable entity, bool remove = false)
		{
			if (!entity.IsValid())
			{
				return;
			}
			if (entity.ShortPrefabName == "grenade.supplysignal.deployed")
			{
				if (remove)
				{
					signals--;
				}
				else
				{
					signals++;
				}
			}
			else if (entity.ShortPrefabName == "cargo_plane")
			{
				if (remove)
				{
					planes--;
				}
				else
				{
					planes++;
				}
			}
			else if (entity.ShortPrefabName == "supply_drop")
			{
				if (remove)
				{
					if (drops.Remove(entity.net.ID.Value, out Timer t))
					{
						t.Destroy();
					}
				}
				else
				{
					SupplyDrop drop = entity as SupplyDrop;
					if (drop != null && !drop.isLootable)
					{
						Timer t = timer.Once(
							300.0f,
							() =>
							{
								drops.Remove(entity.net.ID.Value, out Timer t);
								t.Destroy();
							}
						);
						drops.Add(entity.net.ID.Value, t);
					}
				}
			}
		}

		void OnEntitySpawned(BaseNetworkable entity) => Count(entity);
		void OnEntityKill(BaseNetworkable entity) => Count(entity, true);
		void OnSupplyDropLanded(SupplyDrop drop)
		{
			if (drops.Remove(drop.net.ID.Value, out Timer t))
			{
				t.Destroy();
			}
		}

		void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon weapon) => OnExplosiveThrown(player, entity, weapon);
		void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon weapon)
		{
			if (entity.ShortPrefabName != "grenade.supplysignal.deployed")
			{
				return;
			}
			if (!string.IsNullOrEmpty(config.BypassPermission) && permission.UserHasPermission(player.UserIDString, config.BypassPermission))
			{
				return;
			}
			if (signals <= config.Max && planes < config.Max && drops.Count < config.Max)
			{
				return;
			}
			NextTick(
				() =>
				{
					if (entity != null)
					{
						entity.Kill();
					}
				}
			);
			player.ChatMessage(string.Format(lang.GetMessage("Limit", this, player.UserIDString), config.Max));
			Item item = weapon.GetItem();
			if (item == null)
			{
				return;
			}
			Item newItem = ItemManager.Create(item.info, 1, item.skin);
			if (newItem == null)
			{
				return;
			}
			player.GiveItem(newItem);
		}
	}
}