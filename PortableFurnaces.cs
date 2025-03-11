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
	[Info("PortableFurnaces", "Raul-Sorin Sorban", "1.0.4")]
	[Description("Allows players to privately access furnaces from afar.")]
	public class PortableFurnaces : RustPlugin
	{
		public static PortableFurnaces Instance { get; set; }

		public Dictionary<ulong, BaseOven> Furnaces { get; set; } = new Dictionary<ulong, BaseOven>();
		public Dictionary<ulong, float> CooledDownPlayers { get; set; } = new Dictionary<ulong, float>();

		public BaseOven GetFurnace(BasePlayer player)
		{
			foreach (var x in Furnaces)
			{
				if (x.Key == player.userID)
				{
					var furnace = x.Value;

					if (furnace != null)
					{
						furnace.transform.position = GetPosition(player);
						furnace.SendNetworkUpdateImmediate();
						furnace.UpdateNetworkGroup();
					}

					return furnace;
				}
			}

			return null;
		}
		public BaseOven SetFurnace(BasePlayer player, BaseOven furnace)
		{
			if (Furnaces.ContainsKey(player.userID))
				Furnaces[player.userID] = furnace;
			else Furnaces.Add(player.userID, furnace);

			return furnace;
		}
		public bool CanOpenFurnace(BasePlayer player, out string reason)
		{
			reason = null;

			if (IsCooledDown(player, false))
			{
				reason = string.Format(Config.Phrases.CooledDown, CooldownSecondsLeft(player).ToString("0"));
				return false;
			}

			if (!Config.Rules.CanUseFurnaceWhileBuildingBlocked && !player.CanBuild())
			{
				reason = Config.Phrases.BuildingBlocked;
				return false;
			}

			if (NoEscape != null)
			{
				if (!Config.Rules.CanUseFurnaceWhileCombatBlocked && NoEscape.Call<bool>("IsCombatBlocked", player))
				{
					reason = Config.Phrases.CombatBlocked;
					return false;
				}

				if (!Config.Rules.CanUseFurnaceWhileRaidBlocked && NoEscape.Call<bool>("IsRaidBlocked", player))
				{
					reason = Config.Phrases.RaidBlocked;
					return false;
				}
			}

			return true;
		}
		public bool HasLockerContents(BasePlayer player)
		{
			var locker = GetFurnace(player);
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

		public const string CampfirePrefab = "assets/prefabs/deployable/campfire/campfire.prefab";
		public const string FurnacePrefab = "assets/prefabs/deployable/furnace/furnace.prefab";
		public const string LargeFurnacePrefab = "assets/prefabs/deployable/furnace.large/furnace.large.prefab";
		public const string SmallOilRefineryPrefab = "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab";
		public const string OpenEffect = "assets/prefabs/deployable/furnace/effects/furnace-deploy.prefab";

		public enum FurnaceTypes
		{
			Campfire,
			Furnace,
			LargeFurnace,
			SmallOilRefinery
		}

		#region Permissions

		public const string UsePerm = "portablefurnaces.use";

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

			var furnaces = BaseNetworkable.serverEntities.OfType<BaseOven>();

			Furnaces.Clear();

			foreach (var furnace in furnaces)
			{
				if (Furnaces.ContainsKey(furnace.OwnerID) || furnace.skinID != 8008132) continue;

				Furnaces.Add(furnace.OwnerID, furnace);
				ProcessFurnace(furnace);
			}
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
			var furnace = ent as BaseOven;
			if (furnace == null) return null;

			if (Furnaces.ContainsKey(player.userID) && Furnaces[player.userID] == ent)
			{
				return true;
			}

			return null;
		}
		private object OnEntityDistanceCheck(BaseEntity ent, BasePlayer player, uint id, string debugName, float maximumDistance)
		{
			var furnace = ent as BaseOven;
			if (furnace == null) return null;

			if (Furnaces.ContainsKey(player.userID) && Furnaces[player.userID] == ent)
			{
				return true;
			}

			return null;
		}
		private object CanLootPlayer(BasePlayer looted, BasePlayer looter)
		{
			var furnace = GetFurnace(looter);

			if (furnace != null && looter.inventory.loot.containers.Count > 0 && furnace.inventory == looter.inventory.loot.containers[0])
			{
				return true;
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
			var position = player.transform.position;
			return new Vector3(position.x, position.y - 50f, position.z);
		}
		public BaseOven CreateFurnace(BasePlayer forPlayer, string prefab)
		{
			var furnace = GameManager.server.CreateEntity(prefab, GetPosition(forPlayer), Quaternion.identity) as BaseOven;
			furnace.skinID = 8008132;
			furnace.OwnerID = forPlayer.userID;
			ProcessFurnace(furnace);
			furnace.Spawn();
			furnace.inventory.Clear();

			return furnace;
		}
		public void ProcessFurnace(BaseOven furnace)
		{
			if (furnace == null) return;

			UnityEngine.Object.DestroyImmediate(furnace.GetComponent<DestroyOnGroundMissing>());
			UnityEngine.Object.DestroyImmediate(furnace.GetComponent<GroundWatch>());

			furnace.GetComponent<DecayEntity>().decay = null;
		}
		public void StartLooting(BasePlayer player, BaseOven furnace)
		{
			if (Config.PlayOpenEffect) SendEffectTo(OpenEffect, player);

			timer.In(0.2f, () =>
			{
				player.inventory.loot.StartLootingEntity(furnace, false);
				player.inventory.loot.AddContainer(furnace.inventory);
				player.inventory.loot.PositionChecks = false;
				player.inventory.loot.SendImmediate();
				player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", furnace.panelName);
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
		public FurnaceTypes GetType(BasePlayer forPlayer)
		{
			var furnace = GetFurnace(forPlayer);
			switch (furnace.PrefabName)
			{
				case CampfirePrefab:
					return FurnaceTypes.Campfire;

				case FurnacePrefab:
					return FurnaceTypes.Furnace;

				case LargeFurnacePrefab:
					return FurnaceTypes.LargeFurnace;

				case SmallOilRefineryPrefab:
					return FurnaceTypes.SmallOilRefinery;
			}

			return FurnaceTypes.Furnace;
		}
		public FurnaceTypes FindType(string input)
		{
			for (int i = 0; i < _typesValues.Length; i++)
			{
				var val = _typesValues[i].ToLower();
				if (val.Contains(input.ToLower())) return (FurnaceTypes)i;
			}

			return 0;
		}

		internal string[] _typesValues = Enum.GetNames(typeof(FurnaceTypes));

		#endregion

		#region Methods

		public void InstallCommands()
		{
			cmd.AddChatCommand(Config.OpenCommand, this, (player, command, args) => OpenFurnace(player, args));
			cmd.AddConsoleCommand(Config.OpenCommand, this, (arg) => { OpenFurnace(arg.Player(), arg.Args); return true; });

			cmd.AddChatCommand(Config.HelpCommand, this, (player, command, args) => HelpFurnace(player, args));
			cmd.AddConsoleCommand(Config.HelpCommand, this, (arg) => { HelpFurnace(arg.Player(), arg.Args); return true; });

			cmd.AddChatCommand(Config.SetCommand, this, (player, command, args) => SetFurnace(player, args));
			cmd.AddConsoleCommand(Config.SetCommand, this, (arg) => { SetFurnace(arg.Player(), arg.Args); return true; });
		}

		public void OpenFurnace(BasePlayer player, string[] args)
		{
			if (player == null || !HasPermission(player, UsePerm)) return;

			var reason = string.Empty;
			if (!CanOpenFurnace(player, out reason))
			{
				Print(reason, player);
				return;
			}

			var furnace = GetFurnace(player);

			if (furnace == null) furnace = SetFurnace(player, CreateFurnace(player, FurnacePrefab));

			EnsureEntityToPlayer(player, furnace);
			StartLooting(player, furnace);
		}
		public void HelpFurnace(BasePlayer player, string[] args)
		{
			if (player == null || !HasPermission(player, UsePerm)) return;

			player.ChatMessage(string.Format(Config.Phrases.Help, $"{GetType(player)}"));
		}
		public void SetFurnace(BasePlayer player, string[] args)
		{
			if (player == null || !HasPermission(player, UsePerm)) return;

			var type = args == null || args.Length == 0 ? (FurnaceTypes)1 : !char.IsDigit(args[0][0]) ? FindType(args[0]) : (FurnaceTypes)int.Parse(args[0]);
			if (type < 0) type = 0; else if ((int)type > _typesValues.Length - 1) type = (FurnaceTypes)_typesValues.Length - 1;
			var furnace = GetFurnace(player);
			var previousType = GetType(player);
			if (type == previousType)
			{
				Print(string.Format(Config.Phrases.SwitchedFurnaceTypeAlreadySelected, type.ToString()), player);
				return;
			}
			var prefab = string.Empty;
			{
				switch (type)
				{
					case FurnaceTypes.Campfire:
						prefab = CampfirePrefab;
						break;

					case FurnaceTypes.Furnace:
						prefab = FurnacePrefab;
						break;

					case FurnaceTypes.LargeFurnace:
						prefab = LargeFurnacePrefab;
						break;

					case FurnaceTypes.SmallOilRefinery:
						prefab = SmallOilRefineryPrefab;
						break;
				}
			}

			var changed = (type == FurnaceTypes.Campfire && furnace?.PrefabName != CampfirePrefab) ||
						  (type == FurnaceTypes.Furnace && furnace?.PrefabName != FurnacePrefab) ||
						  (type == FurnaceTypes.LargeFurnace && furnace?.PrefabName != LargeFurnacePrefab) ||
						  (type == FurnaceTypes.SmallOilRefinery && furnace?.PrefabName != SmallOilRefineryPrefab);

			if (changed && furnace != null)
			{
				DropUtil.DropItems(furnace.inventory, player.eyes.position);
				furnace.Kill();
				furnace = null;
			}

			if (furnace == null) furnace = SetFurnace(player, CreateFurnace(player, prefab));

			Print(string.Format(Config.Phrases.SwitchedFurnaceType, previousType.ToString(), type.ToString()), player);
		}

		#endregion

		#region Config

		public Core.Configuration.DynamicConfigFile ConfigFile { get; set; }
		public Core.Configuration.DynamicConfigFile DataFile { get; set; }

		public RootConfig Config { get; set; } = new RootConfig();

		public class RootConfig
		{
			public string Prefix { get; set; } = "Portable Furnaces";
			public string OpenCommand { get; set; } = "furnace";
			public string HelpCommand { get; set; } = "furnaces";
			public string SetCommand { get; set; } = "setfurnace";
			public bool PlayOpenEffect { get; set; } = true;
			public ulong ChatIconSteamId { get; set; } = 0UL;
			public RuleSettings Rules { get; set; } = new RuleSettings();
			public PhraseSettings Phrases { get; set; } = new PhraseSettings();
			public Dictionary<string, float> GroupCooldowns { get; set; }

			public class RuleSettings
			{
				public bool CanUseFurnaceWhileBuildingBlocked { get; set; } = true;
				public bool CanUseFurnaceWhileCombatBlocked { get; set; } = true;
				public bool CanUseFurnaceWhileRaidBlocked { get; set; } = true;
			}
			public class PhraseSettings
			{
				public string CooledDown { get; set; } = "You're in cooldown.\n<size=12><color=orange>{0}</color> seconds left.</size>";
				public string SwitchedFurnaceTypeAlreadySelected { get; set; } = "<color=orange>{0}</color> is already selected.";
				public string SwitchedFurnaceType { get; set; } = "You've changed the furnace type from {0} to <color=orange>{1}</color>.";
				public string BuildingBlocked { get; set; } = "You're building-blocked.";
				public string CombatBlocked { get; set; } = "You're combat-blocked.";
				public string RaidBlocked { get; set; } = "You're raid-blocked.";
				public string Help { get; set; } = "Current: <color=orange>{0}</color>\n0 = Campfire, 1 = Furnace, 2 = Large Furnace, 3 = Small Oil Refinery";
			}
		}

		#endregion
	}
}
