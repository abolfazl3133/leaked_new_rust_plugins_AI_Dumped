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
	[Info("PortableRepairBench", "Raul-Sorin Sorban", "1.0.4")]
	[Description("A system that provides portable Repair Benches for players.")]
	public class PortableRepairBench : RustPlugin
	{
		public static PortableRepairBench Instance { get; set; }

		public Dictionary<ulong, RepairBench> RepairBenches { get; set; } = new Dictionary<ulong, RepairBench>();
		public Dictionary<ulong, float> CooledDownPlayers { get; set; } = new Dictionary<ulong, float>();

		public RepairBench GetRepairBench(BasePlayer player)
		{
			if (player == null) return null;

			foreach (var x in RepairBenches)
			{
				if (x.Key == player.userID)
				{
					var repairBench = x.Value;
					if (repairBench != null)
					{
						repairBench.transform.position = GetPosition(player);
						repairBench.SendNetworkUpdateImmediate();
						repairBench.UpdateNetworkGroup();
					}

					return repairBench;
				}
			}

			return null;
		}
		public static T Get<T>(string type, string property, object instance)
		{
			return (T)typeof(RootConfig).GetNestedType(type).GetProperty(property).GetValue(instance);
		}
		public RepairBench SetRepairBench(BasePlayer player, RepairBench repairBench)
		{
			if (RepairBenches.ContainsKey(player.userID))
				RepairBenches[player.userID] = repairBench;
			else RepairBenches.Add(player.userID, repairBench);

			return repairBench;
		}
		public bool CanOpenRepairBench(BasePlayer player, out string reason)
		{
			reason = null;

			if (IsCooledDown(player, false))
			{
				reason = string.Format(Config.Phrases.CooledDown, CooldownSecondsLeft(player).ToString("0"));
				return false;
			}

			if (!Config.Rules.CanUseRepairBenchWhileBuildingBlocked && !player.CanBuild())
			{
				reason = Config.Phrases.BuildingBlocked;
				return false;
			}

			if (NoEscape != null)
			{
				if (!Config.Rules.CanUseRepairBenchWhileCombatBlocked && NoEscape.Call<bool>("IsCombatBlocked", player))
				{
					reason = Config.Phrases.CombatBlocked;
					return false;
				}

				if (!Config.Rules.CanUseRepairBenchWhileRaidBlocked && NoEscape.Call<bool>("IsRaidBlocked", player))
				{
					reason = Config.Phrases.RaidBlocked;
					return false;
				}
			}

			return true;
		}
		public bool HasLockerContents(BasePlayer player)
		{
			var locker = GetRepairBench(player);
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
		public float CooldownSecondsLeft(BasePlayer player)
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

		public const string RepairBenchPrefab = "assets/prefabs/deployable/repair bench/repairbench_deployed.prefab";
		public const string OpenEffect = "assets/prefabs/deployable/locker/effects/locker-deploy.prefab";

		#region Permissions

		public const string UsePerm = "portablerepairbench.use";

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

				if (updated)
				{
					ConfigFile.WriteObject(Config ?? (Config = new RootConfig()));
				}
			}
		}
		private void Unload()
		{
			foreach (var locker in RepairBenches)
			{
				if (locker.Value == null || locker.Value.IsDestroyed) continue;

				var player = BasePlayer.FindByID(locker.Key);
				if (player != null) DropUtil.DropItems(locker.Value.inventory, player.eyes.transform.position);

				locker.Value?.Kill();
			}

			RepairBenches.Clear();
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
			var repairBench = ent as RepairBench;
			if (repairBench == null) return null;

			if (RepairBenches.ContainsKey(player.userID) && RepairBenches[player.userID] == ent)
			{
				return true;
			}

			return null;
		}

		private object OnEntityDistanceCheck(BaseEntity ent, BasePlayer player, uint id, string debugName, float maximumDistance)
		{
			var repairBench = ent as RepairBench;
			if (repairBench == null) return null;

			if (RepairBenches.ContainsKey(player.userID) && RepairBenches[player.userID] == ent)
			{
				return true;
			}

			return null;
		}
		private object CanLootPlayer(BasePlayer looted, BasePlayer looter)
		{
			var repairBench = GetRepairBench(looter);

			if (repairBench != null && looter.inventory.loot.containers.Count > 0 && repairBench.inventory == looter.inventory.loot.containers[0])
			{
				return true;
			}

			return null;
		}
		private object OnLootEntityEnd(BasePlayer player, RepairBench repairBench)
		{
			if (repairBench == GetRepairBench(player))
			{
				DropUtil.DropItems(repairBench.inventory, player.eyes.transform.position);
			}

			return null;
		}
		private void OnServerSave()
		{
			if (Instance == null) return;

			ConfigFile.WriteObject(Config ?? (Config = new RootConfig()));
		}
		private object OnItemRepair(BasePlayer player, Item item)
		{
			var repairBench = GetRepairBench(player);
			if (player.inventory.loot.entitySource == null || player.inventory.loot.entitySource != repairBench) return null;

			timer.In(0.15f, () => { if (item.conditionNormalized == 1f) Effect.server.Run("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", player, 0u, Vector3.zero, Vector3.zero); });

			return null;
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
			var position = player.transform.position;
			return new Vector3(position.x, position.y - 50f, position.z);
		}
		public RepairBench CreateRepairBench(BasePlayer forPlayer)
		{
			var repairBench = GameManager.server.CreateEntity(RepairBenchPrefab, GetPosition(forPlayer), Quaternion.identity) as RepairBench;
			repairBench.skinID = 8008132;
			repairBench.OwnerID = forPlayer.userID;
			repairBench.Spawn();
			ProcessRepairBench(repairBench);

			return repairBench;

		}
		public void ProcessRepairBench(RepairBench repairBench)
		{
			if (repairBench == null) return;

			UnityEngine.Object.DestroyImmediate(repairBench.GetComponent<DestroyOnGroundMissing>());
			UnityEngine.Object.DestroyImmediate(repairBench.GetComponent<GroundWatch>());
			repairBench.GetComponent<DecayEntity>().decay = null;
		}
		public void StartLooting(BasePlayer player, RepairBench repairBench)
		{
			if (Config.PlayOpenEffect) SendEffectTo(OpenEffect, player);

			timer.In(0.2f, () =>
			{
				player.inventory.loot.StartLootingEntity(repairBench, false);
				player.inventory.loot.AddContainer(repairBench.inventory);
				player.inventory.loot.PositionChecks = false;
				player.inventory.loot.SendImmediate();
				player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", repairBench.panelName);
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
			cmd.AddChatCommand(Config.OpenCommand, this, (player, command, args) => OpenRepairBench(player, args));
			cmd.AddConsoleCommand(Config.OpenCommand, this, (arg) => { OpenRepairBench(arg.Player(), arg.Args); return true; });
		}

		public void OpenRepairBench(BasePlayer player, string[] args)
		{
			if (player == null || !HasPermission(player, UsePerm)) return;

			var reason = string.Empty;
			if (!CanOpenRepairBench(player, out reason))
			{
				Print(reason, player);
				return;
			}

			var repairBench = GetRepairBench(player);

			if (repairBench == null) repairBench = SetRepairBench(player, CreateRepairBench(player));

			EnsureEntityToPlayer(player, repairBench);
			StartLooting(player, repairBench);
		}

		#endregion

		#region Config

		public Core.Configuration.DynamicConfigFile ConfigFile { get; set; }
		public Core.Configuration.DynamicConfigFile DataFile { get; set; }

		public RootConfig Config { get; set; } = new RootConfig();

		public class RootConfig
		{
			public string Prefix { get; set; } = "Portable Repair Bench";
			public string OpenCommand { get; set; } = "rbench";
			public bool PlayOpenEffect { get; set; } = true;
			public ulong ChatIconSteamId { get; set; } = 0UL;
			public RuleSettings Rules { get; set; } = new RuleSettings();
			public PhraseSettings Phrases { get; set; } = new PhraseSettings();
			public Dictionary<string, float> GroupCooldowns { get; set; }

			public class RuleSettings
			{
				public bool CanUseRepairBenchWhileBuildingBlocked { get; set; } = true;
				public bool CanUseRepairBenchWhileCombatBlocked { get; set; } = true;
				public bool CanUseRepairBenchWhileRaidBlocked { get; set; } = true;
			}
			public class PhraseSettings
			{
				public string CooledDown { get; set; } = "You're in cooldown.\n<size=12><color=orange>{0}</color> seconds left.</size>";
				public string BuildingBlocked { get; set; } = "You're building-blocked.";
				public string CombatBlocked { get; set; } = "You're combat-blocked.";
				public string RaidBlocked { get; set; } = "You're raid-blocked.";
				public string CloseClear { get; set; } = "Your <color=orange>Portable Repair Bench</color> emptied as you cannot store items inside of it.";
			}
		}

		#endregion
	}
}
