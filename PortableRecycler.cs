using Facepunch;
using Network;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("PortableRecycler", "Raul-Sorin Sorban", "1.2.3")]
	[Description("A system that provides portable recyclers for players.")]
	public class PortableRecycler : RustPlugin
	{
		public static PortableRecycler Instance { get; set; }

		public Dictionary<ulong, KeyValuePair<Recycler, Timer>> Recyclers { get; set; } = new();
		public Dictionary<ulong, float> CooledDownPlayers { get; set; } = new();

		public Recycler GetRecycler(BasePlayer player)
		{
			if (player == null) return null;

			foreach (var x in Recyclers)
			{
				if (x.Key == player.userID)
				{
					var recycler = x.Value.Key;

					if (recycler != null)
					{
						recycler.limitNetworking = false;
						recycler.transform.position = GetPosition(player);
						recycler.SendNetworkUpdateImmediate();
						recycler.UpdateNetworkGroup();
						recycler.net.SwitchSecondaryGroup(player.net.group);
						ApplySettings(recycler, player);
					}

					return recycler;
				}
			}

			return null;
		}
		public static T Get<T>(string type, string property, object instance)
		{
			return (T)typeof(RootConfig).GetNestedType(type).GetProperty(property).GetValue(instance);
		}
		public Recycler SetRecycler(BasePlayer player, Recycler recycler)
		{
			if (Recyclers.ContainsKey(player.userID))
				Recyclers[player.userID] = new KeyValuePair<Recycler, Timer>(recycler, null);
			else Recyclers.Add(player.userID, new KeyValuePair<Recycler, Timer>(recycler, null));

			return recycler;
		}
		public bool CanOpenRecycler(BasePlayer player, out string reason)
		{
			reason = null;

			if (IsCooledDown(player, false))
			{
				reason = string.Format(Config.Phrases.CooledDown, GetCooldownSecondsLeft(player).ToString("0"));
				return false;
			}

			if (!Config.Rules.CanUseRecyclerWhileBuildingBlocked && !player.CanBuild())
			{
				reason = Config.Phrases.BuildingBlocked;
				return false;
			}

			if (NoEscape != null)
			{
				if (!Config.Rules.CanUseRecyclerWhileCombatBlocked && NoEscape.Call<bool>("IsCombatBlocked", player))
				{
					reason = Config.Phrases.CombatBlocked;
					return false;
				}

				if (!Config.Rules.CanUseRecyclerWhileRaidBlocked && NoEscape.Call<bool>("IsRaidBlocked", player))
				{
					reason = Config.Phrases.RaidBlocked;
					return false;
				}
			}

			return true;
		}
		public bool HasLockerContents(BasePlayer player)
		{
			var locker = GetRecycler(player);
			if (locker == null) return false;

			return locker.inventory.itemList.Count > 0;
		}
		public bool IsCooledDown(BasePlayer player, bool coolDownPlayer = true)
		{
			foreach (var group in Config.GroupCooldowns)
			{
				if (permission.UserHasGroup(player.UserIDString, group.Key))
				{
					if (CooledDownPlayers.ContainsKey(player.userID))
					{
						if (Time.realtimeSinceStartup - CooledDownPlayers[player.userID] > group.Value)
						{
							if (coolDownPlayer)
							{
								CooledDownPlayers[player.userID] = Time.realtimeSinceStartup;
								return true;
							}
							else
							{
								CooledDownPlayers.Remove(player.userID);
								return false;
							}
						}
						else
						{
							return true;
						}
					}
					else if (coolDownPlayer)
					{
						CooledDownPlayers.Add(player.userID, Time.realtimeSinceStartup);
						return true;
					}
				}
			}

			return false;
		}
		public float GetCooldownSecondsLeft(BasePlayer player)
		{
			if (IsCooledDown(player, false))
			{
				foreach (var group in Config.GroupCooldowns)
				{
					if (permission.UserHasGroup(player.UserIDString, group.Key))
					{
						return group.Value - (Time.realtimeSinceStartup - CooledDownPlayers[player.userID]);
					}
				}
			}

			return 0f;
		}
		public float GetRate(BasePlayer player)
		{
			foreach (var group in Config.GroupRate)
			{
				if (permission.UserHasGroup(player.UserIDString, group.Key))
				{
					return group.Value;
				}
			}

			return 5f;
		}
		public float GetQuality(BasePlayer player)
		{
			foreach (var group in Config.GroupQuality)
			{
				if (permission.UserHasGroup(player.UserIDString, group.Key))
				{
					return group.Value;
				}
			}

			return 0.5f;
		}

		public void StartTracking(BasePlayer player, Recycler recycler)
		{
			Recyclers[player.userID] = new KeyValuePair<Recycler, Timer>(recycler, timer.Every(1f, () =>
			{
				recycler.ServerPosition = recycler.transform.position = GetPosition(player);
				recycler.SendNetworkUpdate();
				recycler.SendNetworkUpdate_Position();
			}));
		}

		public void StopTracking(BasePlayer player)
		{
			Recyclers[player.userID].Value?.Destroy();
		}

		public void ApplySettings(Recycler recycler, BasePlayer player)
		{
			recycler.recycleEfficiency = GetQuality(player);
		}

		public const string RecyclerPrefab = "assets/bundled/prefabs/static/recycler_static.prefab";
		public const string OpenEffect = "assets/prefabs/deployable/locker/effects/locker-deploy.prefab";

		#region Permissions

		public const string UsePerm = "portablerecycler.use";

		public void InstallPermissions()
		{
			permission.RegisterPermission(UsePerm, this);
		}

		private bool HasPermission(BasePlayer player, string perm, bool quiet = false)
		{
			if (!permission.UserHasPermission(player.UserIDString, perm))
			{
				if (!quiet) Print($"You need to have the \"{perm}\" permission.", player);
				return false;
			}

			return true;
		}

		#endregion

		#region Hooks

		private void OnServerInitialized()
		{
			Instance = this;

			Loaded();

			InstallPermissions();
			InstallCommands();

			RefreshPlugins();
		}
		private void Loaded()
		{
			if (Instance == null) return;

			if (ConfigFile == null) ConfigFile = new Core.Configuration.DynamicConfigFile($"{Manager.ConfigPath}{Path.DirectorySeparatorChar}{Name}.json");
			if (DataFile == null) DataFile = Interface.Oxide.DataFileSystem.GetFile($"{Name}_data");

			if (!ConfigFile.Exists())
			{
				Config = new RootConfig();
				ConfigFile.WriteObject(Config);
			}
			else
			{
				try
				{
					Config = ConfigFile.ReadObject<RootConfig>();
				}
				catch (Exception exception)
				{
					Puts($"Broken configuration: {exception.Message}");
				}
			}

			if (Config != null)
			{
				var updated = false;

				if (Config.GroupCooldowns == null)
				{
					Config.GroupCooldowns = new Dictionary<string, float>();
					Config.GroupCooldowns.Add("default", 60f);
					updated = true;
				}

				if (Config.GroupRate == null)
				{
					Config.GroupRate = new Dictionary<string, float>();
					Config.GroupRate.Add("default", 5);
					updated = true;
				}

				if (Config.GroupQuality == null)
				{
					Config.GroupQuality = new Dictionary<string, float>();
					Config.GroupQuality.Add("default", 0.5f);
					updated = true;
				}

				if (updated)
				{
					ConfigFile.WriteObject(Config ?? (Config = new RootConfig()));
				}
			}
		}
		private void Unload()
		{
			foreach (var locker in Recyclers)
			{
				if (locker.Value.Key == null || locker.Value.Key.IsDestroyed) continue;

				var player = BasePlayer.FindByID(locker.Key);
				if (player != null) DropUtil.DropItems(locker.Value.Key.inventory, player.eyes.transform.position);

				if (locker.Value.Key != null)
				{
					locker.Value.Key.Kill();
				}
			}

			Recyclers.Clear();
		}
		private void OnPluginLoaded(Plugin name)
		{
			RefreshPlugins();
		}
		private void OnPluginUnloaded(Plugin name)
		{
			RefreshPlugins();
		}

		private object OnEntityVisibilityCheck(BaseEntity ent, BasePlayer player, uint id, string debugName, float maximumDistance)
		{
			var recycler = ent as Recycler;
			if (recycler == null) return null;

			if (Recyclers.TryGetValue(player.userID, out var value) && value.Key == ent)
			{
				return true;
			}

			return null;
		}

		private object OnEntityDistanceCheck(BaseEntity ent, BasePlayer player, uint id, string debugName, float maximumDistance)
		{
			var recycler = ent as Recycler;
			if (recycler == null) return null;

			if (Recyclers.TryGetValue(player.userID, out var value) && value.Key == ent)
			{
				return true;
			}

			return null;
		}
		private object CanLootPlayer(BasePlayer looted, BasePlayer looter)
		{
			var recycler = GetRecycler(looter);

			if (recycler != null && looter.inventory.loot.containers.Count > 0 && recycler.inventory == looter.inventory.loot.containers[0])
			{
				return true;
			}

			return null;
		}
		private object OnLootEntityEnd(BasePlayer player, Recycler recycler)
		{
			if (recycler == GetRecycler(player))
			{
				DropUtil.DropItems(recycler.inventory, player.eyes.transform.position);
				StopTracking(player);
				// recycler.Kill ();
			}

			return null;
		}
		object OnRecyclerToggle(Recycler recycler, BasePlayer player)
		{
			if (recycler == GetRecycler(player))
			{
				if (recycler.IsOn())
				{
					recycler.StopRecycling();
					Effect.server.Run(recycler.stopSound.resourcePath, player, 0u, Vector3.zero, Vector3.zero);
				}
				else if (!recycler.IsOn())
				{
					foreach (Item item in recycler.inventory.itemList)
					{
						item.CollectedForCrafting(player);
					}

					var rate = GetRate(player);
					recycler.InvokeRepeating(recycler.RecycleThink, rate, rate);

					Effect.server.Run(recycler.startSound.resourcePath, player, 0u, Vector3.zero, Vector3.zero);
					recycler.SetFlag(BaseEntity.Flags.On, b: true);
				}

				recycler.SendNetworkUpdateImmediate();
				return false;
			}

			return null;
		}
		private void OnServerSave()
		{
			if (Instance == null) return;

			ConfigFile.WriteObject(Config ?? (Config = new RootConfig()));
		}

		#endregion

		#region Plugins

		private Plugin NoEscape;

		#endregion

		#region Helpers

		public void Print(object message, BasePlayer player = null)
		{
			if (player == null) ConsoleNetwork.BroadcastToAllClients("chat.add", 2, Config.ChatIconSteamId, $"<color=orange>{Config.Prefix}</color>: {message}");
			else player.SendConsoleCommand("chat.add", 2, Config.ChatIconSteamId, $"<color=orange>{Config.Prefix}</color> (OY): {message}");
		}
		public float Scale(float oldValue, float oldMin, float oldMax, float newMin, float newMax)
		{
			var num = oldMax - oldMin;
			var num2 = newMax - newMin;
			return (oldValue - oldMin) * num2 / num + newMin;
		}
		public string Join(string[] array, string separator, string lastSeparator = null)
		{
			if (string.IsNullOrEmpty(lastSeparator))
			{
				lastSeparator = separator;
			}

			if (array.Length == 0)
			{
				return string.Empty;
			}

			if (array.Length == 1)
			{
				return array[0];
			}

			var list = Pool.GetList<string>();
			for (int i = 0; i < array.Length - 1; i++)
			{
				list.Add(array[i]);
			}
			var value = string.Join(separator, list.ToArray());
			Pool.FreeList(ref list);
			return value + $"{lastSeparator}{array[array.Length - 1]}";
		}
		public void SendEffectTo(string effect, BasePlayer player)
		{
			if (player == null) return;

			var effectInstance = new Effect();
			effectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
			effectInstance.pooledstringid = StringPool.Get(effect);
			NetWrite netWrite = Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.Effect);
			effectInstance.WriteToStream(netWrite);
			netWrite.Send(new SendInfo(player.net.connection));
			effectInstance.Clear();
		}
		public Vector3 GetPosition(BasePlayer player)
		{
			var playerPosition = player.transform.position;
			var heightOffset = TerrainMeta.HeightMap.GetHeight(playerPosition);
			return new Vector3(playerPosition.x, heightOffset - 500f, playerPosition.z);
		}
		public Recycler CreateRecycler(BasePlayer forPlayer)
		{
			var recycler = GameManager.server.CreateEntity(RecyclerPrefab, GetPosition(forPlayer), Quaternion.identity) as Recycler;
			recycler.skinID = 8008132;
			recycler.OwnerID = forPlayer.userID;
			recycler.Spawn();
			recycler.EnableSaving(false);
			ProcessRecycler(recycler);

			return recycler;

		}
		public void ProcessRecycler(Recycler recycler)
		{
			if (recycler == null) return;

			UnityEngine.Object.DestroyImmediate(recycler.GetComponent<DestroyOnGroundMissing>());
			UnityEngine.Object.DestroyImmediate(recycler.GetComponent<GroundWatch>());
			recycler.GetComponent<DecayEntity>().decay = null;
		}
		public void StartLooting(BasePlayer player, Recycler recycler)
		{
			if (Config.PlayOpenEffect) SendEffectTo(OpenEffect, player);

			timer.In(0.2f, () =>
			{
				player.inventory.loot.StartLootingEntity(recycler, false);
				player.inventory.loot.AddContainer(recycler.inventory);
				player.inventory.loot.PositionChecks = false;
				player.inventory.loot.SendImmediate();
				player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", recycler.panelName);
			});

			IsCooledDown(player);
		}
		public void EnsureEntityToPlayer(BasePlayer player, BaseEntity player2)
		{
			var connection = player.Connection;
			var write = Net.sv.StartWrite();
			if (write == null || connection == null) return;
			++connection.validate.entityUpdates;
			var saveInfo = new BaseNetworkable.SaveInfo() { forConnection = connection, forDisk = false };
			write.PacketID(Message.Type.Entities);
			write.UInt32(connection.validate.entityUpdates);
			player2.ToStreamForNetwork(write, saveInfo);
			write.Send(new SendInfo(connection));
		}
		public void RefreshPlugins()
		{
			if (NoEscape == null || !NoEscape.IsLoaded) NoEscape = plugins.Find(nameof(NoEscape));
		}

		#endregion

		#region Methods

		public void InstallCommands()
		{
			cmd.AddChatCommand(Config.OpenCommand, this, (player, command, args) => OpenRecycler(player, args));
			cmd.AddConsoleCommand(Config.OpenCommand, this, (arg) => { OpenRecycler(arg.Player(), arg.Args); return true; });
		}

		public void OpenRecycler(BasePlayer player, string[] args)
		{
			if (player == null || !HasPermission(player, UsePerm)) return;

			var reason = string.Empty;
			if (!CanOpenRecycler(player, out reason))
			{
				Print(reason, player);
				return;
			}

			var recycler = GetRecycler(player);

			if (recycler == null) recycler = SetRecycler(player, CreateRecycler(player));

			EnsureEntityToPlayer(player, recycler);
			NextTick(() => StartLooting(player, recycler));
			StartTracking(player, recycler);
		}

		#endregion

		#region Config

		public Core.Configuration.DynamicConfigFile ConfigFile { get; set; }
		public Core.Configuration.DynamicConfigFile DataFile { get; set; }

		public RootConfig Config { get; set; } = new RootConfig();

		public class RootConfig
		{
			public string Prefix { get; set; } = "Portable Recycler";
			public string OpenCommand { get; set; } = "recycler";
			public bool PlayOpenEffect { get; set; } = true;
			public ulong ChatIconSteamId { get; set; } = 0UL;
			public RuleSettings Rules { get; set; } = new RuleSettings();
			public PhraseSettings Phrases { get; set; } = new PhraseSettings();
			public Dictionary<string, float> GroupCooldowns { get; set; }
			public Dictionary<string, float> GroupRate { get; set; }
			public Dictionary<string, float> GroupQuality { get; set; }

			public class RuleSettings
			{
				public bool CanUseRecyclerWhileBuildingBlocked { get; set; } = true;
				public bool CanUseRecyclerWhileCombatBlocked { get; set; } = true;
				public bool CanUseRecyclerWhileRaidBlocked { get; set; } = true;
			}
			public class PhraseSettings
			{
				public string CooledDown { get; set; } = "You're in cooldown.\n<size=12><color=orange>{0}</color> seconds left.</size>";
				public string BuildingBlocked { get; set; } = "You're building-blocked.";
				public string CombatBlocked { get; set; } = "You're combat-blocked.";
				public string RaidBlocked { get; set; } = "You're raid-blocked.";
				public string CloseClear { get; set; } = "Your <color=orange>Portable Recycler</color> emptied as you cannot store items inside of it.";
			}
		}

		#endregion
	}
}
