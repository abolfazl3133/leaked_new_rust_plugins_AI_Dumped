using Facepunch;
using Network;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("PortableWorkbench", "Raul-Sorin Sorban", "1.1.6")]
	[Description("Access any level of workbench from any point on the map.")]
	public class PortableWorkbench : RustPlugin
	{
		public static PortableWorkbench Instance { get; set; }

		internal Dictionary<ulong, float> Workbench1CooledDownPlayers { get; set; } = new Dictionary<ulong, float>();
		internal Dictionary<ulong, float> Workbench2CooledDownPlayers { get; set; } = new Dictionary<ulong, float>();
		internal Dictionary<ulong, float> Workbench3CooledDownPlayers { get; set; } = new Dictionary<ulong, float>();
		internal Dictionary<BasePlayer, List<Timer>> CachedTimers { get; set; } = new Dictionary<BasePlayer, List<Timer>>();

		public bool CanUseWorkbench(BasePlayer player, int level, out string reason)
		{
			reason = null;

			if (WorkbenchPlacedRequirement(player) && !HasPlacedAtLeastOneWorkbench(player, level))
			{
				reason = string.Format(Config.Phrases.PlacementRequirement, level);
				return false;
			}

			if (IsCooledDown(player, level, false))
			{
				reason = string.Format(Config.Phrases.CooledDown, CooldownSecondsLeft(player, level).ToString("0"));
				return false;
			}

			if (Config.AllBenchCooldownCheck)
			{
				if (IsCooledDown(player, 1, false) || IsCooledDown(player, 2, false) || IsCooledDown(player, 3, false))
				{
					reason = string.Format(Config.Phrases.AlreadyUsedBench);
					return false;
				}
			}

			if (!Config.Rules.CanUseWorkbenchWhileBuildingBlocked && !player.CanBuild())
			{
				reason = Config.Phrases.BuildingBlocked;
				return false;
			}

			if (NoEscape != null)
			{
				if (!Config.Rules.CanUseWorkbenchWhileCombatBlocked && NoEscape.Call<bool>("IsCombatBlocked", player))
				{
					reason = Config.Phrases.CombatBlocked;
					return false;
				}

				if (!Config.Rules.CanUseWorkbenchWhileRaidBlocked && NoEscape.Call<bool>("IsRaidBlocked", player))
				{
					reason = Config.Phrases.RaidBlocked;
					return false;
				}
			}

			return true;
		}
		public bool IsCooledDown(BasePlayer player, int level, bool coolDownPlayer = true)
		{
			var coolDown = level == 1 ? Config.Workbench1GroupCooldowns : level == 2 ? Config.Workbench2GroupCooldowns : level == 3 ? Config.Workbench3GroupCooldowns : Config.Workbench1GroupCooldowns;
			var cooledDown = level == 1 ? Workbench1CooledDownPlayers : level == 2 ? Workbench2CooledDownPlayers : level == 3 ? Workbench3CooledDownPlayers : Workbench1CooledDownPlayers;

			foreach (var group in coolDown)
			{
				if (permission.UserHasGroup(player.UserIDString, group.Key))
				{
					if (cooledDown.ContainsKey(player.userID))
					{
						if (Time.realtimeSinceStartup - cooledDown[player.userID] > group.Value)
						{
							if (coolDownPlayer)
							{
								cooledDown[player.userID] = Time.realtimeSinceStartup;
								return true;
							}
							else
							{
								cooledDown.Remove(player.userID);
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
						cooledDown.Add(player.userID, Time.realtimeSinceStartup);
						return true;
					}
				}
			}

			return false;
		}
		public float CooldownSecondsLeft(BasePlayer player, int level)
		{
			var coolDown = level == 1 ? Config.Workbench1GroupCooldowns : level == 2 ? Config.Workbench2GroupCooldowns : level == 3 ? Config.Workbench3GroupCooldowns : Config.Workbench1GroupCooldowns;
			var cooledDown = level == 1 ? Workbench1CooledDownPlayers : level == 2 ? Workbench2CooledDownPlayers : level == 3 ? Workbench3CooledDownPlayers : Workbench1CooledDownPlayers;

			if (IsCooledDown(player, level, false))
			{
				foreach (var group in coolDown)
				{
					if (permission.UserHasGroup(player.UserIDString, group.Key))
					{
						return group.Value - (Time.realtimeSinceStartup - cooledDown[player.userID]);
					}
				}
			}

			return 0f;
		}
		public float WorkbenchTimeSeconds(BasePlayer player, int level)
		{
			var time = level == 1 ? Config.Workbench1UseTime : level == 2 ? Config.Workbench2UseTime : level == 3 ? Config.Workbench3UseTime : Config.Workbench1UseTime;

			foreach (var group in time)
			{
				if (permission.UserHasGroup(player.UserIDString, group.Key))
				{
					return group.Value;
				}
			}

			return 0f;
		}
		public bool WorkbenchPlacedRequirement(BasePlayer player)
		{
			foreach (var group in Config.WorkbenchPlacedRequirement)
			{
				if (permission.UserHasGroup(player.UserIDString, group.Key))
				{
					return group.Value;
				}
			}

			return false;
		}

		#region Permissions

		public const string Use1Perm = "portableworkbench.use1";
		public const string Use2Perm = "portableworkbench.use2";
		public const string Use3Perm = "portableworkbench.use3";

		public void InstallPermissions()
		{
			permission.RegisterPermission(Use1Perm, this);
			permission.RegisterPermission(Use2Perm, this);
			permission.RegisterPermission(Use3Perm, this);
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

				if (Config.WorkbenchPlacedRequirement == null)
				{
					Config.WorkbenchPlacedRequirement = new Dictionary<string, bool>();
					Config.WorkbenchPlacedRequirement.Add("default", false);
					updated = true;
				}

				if (Config.Workbench1GroupCooldowns == null)
				{
					Config.Workbench1GroupCooldowns = new Dictionary<string, float>();
					Config.Workbench1GroupCooldowns.Add("default", 10f);
					updated = true;
				}

				if (Config.Workbench2GroupCooldowns == null)
				{
					Config.Workbench2GroupCooldowns = new Dictionary<string, float>();
					Config.Workbench2GroupCooldowns.Add("default", 30f);
					updated = true;
				}

				if (Config.Workbench3GroupCooldowns == null)
				{
					Config.Workbench3GroupCooldowns = new Dictionary<string, float>();
					Config.Workbench3GroupCooldowns.Add("default", 60f);
					updated = true;
				}

				if (Config.Workbench1UseTime == null)
				{
					Config.Workbench1UseTime = new Dictionary<string, float>();
					Config.Workbench1UseTime.Add("default", 60f);
					updated = true;
				}

				if (Config.Workbench2UseTime == null)
				{
					Config.Workbench2UseTime = new Dictionary<string, float>();
					Config.Workbench2UseTime.Add("default", 30f);
					updated = true;
				}

				if (Config.Workbench3UseTime == null)
				{
					Config.Workbench3UseTime = new Dictionary<string, float>();
					Config.Workbench3UseTime.Add("default", 10f);
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
			foreach (var list in CachedTimers)
			{
				for (int i = 0; i < list.Value.Count; i++)
				{
					var timer = list.Value[i];
					timer?.Callback?.Invoke();
					timer?.Destroy();
				}
			}

			CachedTimers.Clear();
		}
		private void OnPluginLoaded(Plugin name)
		{
			RefreshPlugins();
		}
		private void OnPluginUnloaded(Plugin name)
		{
			RefreshPlugins();
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
		public bool HasPlacedAtLeastOneWorkbench(BasePlayer player, int level)
		{
			foreach (var workbench in BaseNetworkable.serverEntities.OfType<Workbench>())
			{
				if (workbench.OwnerID == player.userID && workbench.Workbenchlevel == level)
				{
					return true;
				}
			}

			return false;
		}

		public void RefreshPlugins()
		{
			if (NoEscape == null || !NoEscape.IsLoaded) NoEscape = plugins.Find(nameof(NoEscape));
		}

		#endregion

		#region Methods

		public void InstallCommands()
		{
			cmd.AddChatCommand(Config.OpenCommand, this, (player, command, args) => OpenWorkbench(player, command, args));
			cmd.AddConsoleCommand(Config.OpenCommand, this, (arg) => { OpenWorkbench(arg.Player(), arg.cmd.Name, arg.Args); return true; });
		}

		public void AssignPlayerBench(BasePlayer player, int level, float time)
		{
			if (player.triggers == null) player.triggers = new List<TriggerBase>();
			var placebo = Pool.Get<TriggerWorkbench>();
			player.triggers.Add(placebo);
			player.nextCheckTime = UnityEngine.Time.realtimeSinceStartup + time;
			player.cachedCraftLevel = level;
			player.SetPlayerFlag(level == 1 ? BasePlayer.PlayerFlags.Workbench1 : level == 2 ? BasePlayer.PlayerFlags.Workbench2 : level == 3 ? BasePlayer.PlayerFlags.Workbench3 : BasePlayer.PlayerFlags.Workbench1, true);

			var list = (List<Timer>)null;

			if (!CachedTimers.ContainsKey(player)) CachedTimers.Add(player, list = new List<Timer>());
			else list = CachedTimers[player];

			var t = (Timer)null;
			list.Add(t = timer.In(time, () =>
			{
				if (player.triggers != null && player.triggers.Contains(placebo)) player.triggers.Remove(placebo);
				player.nextCheckTime = 0f;
				Pool.Free(ref placebo);
				list.Remove(t);
			}));
		}

		public void OpenWorkbench(BasePlayer player, string command, string[] args)
		{
			var level = args == null || args.Length == 0 || !char.IsDigit(args[0][0]) ? 1 : int.Parse(args[0]);
			if (level < 1) level = 1; else if (level > 3) level = 3;

			if (player == null || !HasPermission(player, $"PortableWorkbench.use{level}")) return;

			var reason = string.Empty;
			if (!CanUseWorkbench(player, level, out reason))
			{
				Print(reason, player);
				return;
			}

			AssignPlayerBench(player, level, WorkbenchTimeSeconds(player, level));
			IsCooledDown(player, level);
		}

		#endregion

		#region Config

		public Core.Configuration.DynamicConfigFile ConfigFile { get; set; }
		public Core.Configuration.DynamicConfigFile DataFile { get; set; }

		public RootConfig Config { get; set; } = new RootConfig();

		public class RootConfig
		{
			public string Prefix { get; set; } = "Portable Workbench";
			public string OpenCommand { get; set; } = "workbench";
			public ulong ChatIconSteamId { get; set; } = 0UL;
			public bool AllBenchCooldownCheck { get; set; } = true;
			public RuleSettings Rules { get; set; } = new RuleSettings();
			public PhraseSettings Phrases { get; set; } = new PhraseSettings();
			public Dictionary<string, bool> WorkbenchPlacedRequirement { get; set; }
			public Dictionary<string, float> Workbench1UseTime { get; set; }
			public Dictionary<string, float> Workbench2UseTime { get; set; }
			public Dictionary<string, float> Workbench3UseTime { get; set; }
			public Dictionary<string, float> Workbench1GroupCooldowns { get; set; }
			public Dictionary<string, float> Workbench2GroupCooldowns { get; set; }
			public Dictionary<string, float> Workbench3GroupCooldowns { get; set; }

			public class RuleSettings
			{
				public bool CanUseWorkbenchWhileBuildingBlocked { get; set; } = true;
				public bool CanUseWorkbenchWhileCombatBlocked { get; set; } = true;
				public bool CanUseWorkbenchWhileRaidBlocked { get; set; } = true;
			}
			public class PhraseSettings
			{
				public string CooledDown { get; set; } = "You're in cooldown.\n<size=12><color=orange>{0}</color> seconds left.</size>";
				public string AlreadyUsedBench { get; set; } = "You're already cooled down by a different Workbench. Please wait until that ends.";
				public string PlacementRequirement { get; set; } = "In order to use level {0} workbench, you must place one somewhere on the map.";
				public string BuildingBlocked { get; set; } = "You're building-blocked.";
				public string CombatBlocked { get; set; } = "You're combat-blocked.";
				public string RaidBlocked { get; set; } = "You're raid-blocked.";
			}
		}

		#endregion
	}
}
