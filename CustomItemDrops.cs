using System.Data;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Custom Item Drops", "MACHIN3", "1.1.200")]
	[Description("Drops items from custom lists for gathering, mining, looting, and more.")]
	public class CustomItemDrops : RustPlugin
	{
        #region Update Log

        /*****************************************************
		【 𝓜𝓐𝓒𝓗𝓘𝓝𝓔 】
        Website: https://www.rustlevels.com/
        Discord: http://discord.rustlevels.com/
        *****************************************************/
        #region 1.1.200
        /*
		--------------
		Version 1.1.200
		--------------
		✯ Added option to disable drop chance when using power tools
		✯ Fix for Rust update

		*/
        #endregion
        #region 1.1.1
        /*
		--------------
		Version 1.1.1
		--------------
		✯ Fix for Rust May Update

		*/
        #endregion
        #region 1.1.0
        /*
		--------------
		Version 1.1.0
		--------------
		✯ Fix for OnEntityDeath error

		*/
        #endregion
        #region 1.0.9
        /*
		--------------
		Version 1.0.9
		--------------
		✯ Added API for player kill item list
		✯ Fixed OnCollectiblePickup hook changes
		*/
        #endregion
        #region 1.0.8
        /*
		--------------
		Version 1.0.8
		--------------
		✯ Added custom item list for player kills
		*/
        #endregion
        #region 1.0.7
        /*
		--------------
		Version 1.0.7
		--------------
		✯ Fixed NPC drop list not dropping correct list items
		*/
        #endregion
        #region 1.0.6
        /*
		--------------
		Version 1.0.6
		--------------
		✯ Added API for XPerienceAddon support
		*/
        #endregion
        #region 1.0.5
        /*
		--------------
		Version 1.0.5
		--------------
		✯ Added custom item list for animal kills
		✯ Option to exclude certain animals
		✯ Added custom item list for npc kills
		✯ Option to exclude certain NPCs
		*/
        #endregion
        #region 1.0.4
        /*
		--------------
		Version 1.0.4
		--------------
		✯ Separated hooks and cleaned up coding
		✯ Added custom item name option to lists
		✯ Now shows item names instead of shortnames in chat message
		*/
        #endregion
        #region 1.0.3
        /*
		--------------
		Version 1.0.3
		--------------
		✯ Added option to enable/disable certain resources within gathering and looting lists
		*/
        #endregion
        #region 1.0.2
        /*
		--------------
		Version 1.0.2
		--------------
		✯ Added option to add SkinID to custom item lists
		*/
        #endregion
        #region 1.0.1
        /*
		--------------
		Version 1.0.1
		--------------
		✯ Added option to disable chat message
		✯ Fixed gathering chance not working on some sources
		*/
        #endregion
        #region 1.0.0
        /*
		--------------
		Version 1.0.0
		--------------
		✯ Initial Release
		✯ 3 Custom Item Lists (Gathering, Mining, Looting)
		✯ Percentage drop option for each list
		✯ Each list has their own permissions
		✯ VIP chance increase permission
		*/
        #endregion
        #endregion

        #region Fields
        private readonly LootData _CID_lootData;
		private DynamicConfigFile _CID_LootContainData;
		private Dictionary<NetworkableId, Loot> _CID_lootCache;
		private Configuration config;
		private const string PermGatheringChance = "customitemdrops.gathering";
		private const string PermMiningChance = "customitemdrops.mining";
		private const string PermLootingChance = "customitemdrops.looting";
		private const string PermAnimalChance = "customitemdrops.animal";
		private const string PermNPCChance = "customitemdrops.npc";
		private const string PermPlayerChance = "customitemdrops.player";
		private const string PermVIPChance = "customitemdrops.vipchance";
		#endregion

		#region Config
		private class Configuration : SerializableConfiguration
		{
			[JsonProperty("General Settings")]
			public GeneralSettings generalSettings = new GeneralSettings();
			[JsonProperty("Gathering Options / List")]
			public GatheringOptions gatheringoptions = new GatheringOptions();
			[JsonProperty("Mining Options / List")]
			public MiningOptions miningoptions = new MiningOptions();
			[JsonProperty("Looting Options / List")]
			public LootingOptions lootingoptions = new LootingOptions();
			[JsonProperty("Animal Kill Options / List")]
			public AnimalOptions animaloptions = new AnimalOptions();
			[JsonProperty("NPC Kill Options / List")]
			public NPCOptions NPCoptions = new NPCOptions();
			[JsonProperty("Player Kill Options / List")]
			public PlayerOptions Playeroptions = new PlayerOptions();
		}
		public class GeneralSettings
		{
			public bool showchatmessage = true;
			public bool disablepowertoolchance = true;
		}
		public class GatheringOptions
		{
			public int dropchance = 10;
			public int vipdropchance = 20;
			public bool trees = true;
			public bool berries = true;
			public bool wood = true;
			public bool stones = true;
			public bool ore = true;
			public bool hemp = true;
			public bool mushrooms = true;
			public bool pumpkins = true;
			public bool corn = true;
			public bool potatos = true;
			public Dictionary<int, GatheringItemList> gatheringItemList = new Dictionary<int, GatheringItemList>
			{
				[0] = new GatheringItemList
				{
					shortname = "apple",
					displayname = "",
					amount = 1,
					SkinID = 0
				},
				[1] = new GatheringItemList
				{
					shortname = "bandage",
					displayname = "",
					amount = 1,
					SkinID = 0
				},
			};
		}
		public class MiningOptions
		{
			public int dropchance = 10;
			public int vipdropchance = 20;
			public Dictionary<int, MiningItemList> miningItemList = new Dictionary<int, MiningItemList>
			{
				[0] = new MiningItemList
				{
					shortname = "metal.fragments",
					displayname = "",
					amount = 5,
					SkinID = 0
				},
				[1] = new MiningItemList
				{
					shortname = "metal.refined",
					displayname = "",
					amount = 2,
					SkinID = 0
				},
			};
		}
		public class LootingOptions
		{
			public int dropchance = 10;
			public int vipdropchance = 20;
			public bool lootcontainer = true;
			public bool freeablelootcontainer = true;
			public bool lockedbyentcrate = true;
			public bool hackablelockedcrate = true;
			public Dictionary<int, LootingItemList> lootingItemList = new Dictionary<int, LootingItemList>
			{
				[0] = new LootingItemList
				{
					shortname = "scrap",
					displayname = "",
					amount = 2,
					SkinID = 0
				},
				[1] = new LootingItemList
				{
					shortname = "metal.fragments",
					displayname = "",
					amount = 1,
					SkinID = 0
				},
			};
		}
		public class AnimalOptions
		{
			public int dropchance = 10;
			public int vipdropchance = 20;
			public bool chicken = true;
			public bool boar = true;
			public bool stag = true;
			public bool wolf = true;
			public bool bear = true;
			public bool polarbear = true;
			public bool horse = true;
			public bool shark = true;
			public Dictionary<int, AnimalItemList> AnimalItemList = new Dictionary<int, AnimalItemList>
			{
				[0] = new AnimalItemList
				{
					shortname = "scrap",
					displayname = "",
					amount = 2,
					SkinID = 0
				},
				[1] = new AnimalItemList
				{
					shortname = "metal.fragments",
					displayname = "",
					amount = 1,
					SkinID = 0
				},
			};
		}
		public class NPCOptions
		{
			public int dropchance = 10;
			public int vipdropchance = 20;
			public bool scientist = true;
			public bool dweller = true;
			public bool bradley = true;
			public bool heli = true;
			public bool scarcrow = true;
			public bool customnpc = true;
			public bool zombie = true;
			public Dictionary<int, NPCItemList> NPCItemList = new Dictionary<int, NPCItemList>
			{
				[0] = new NPCItemList
				{
					shortname = "scrap",
					displayname = "",
					amount = 2,
					SkinID = 0
				},
				[1] = new NPCItemList
				{
					shortname = "metal.fragments",
					displayname = "",
					amount = 1,
					SkinID = 0
				},
			};
		}
		public class PlayerOptions
		{
			public int dropchance = 10;
			public int vipdropchance = 20;
			public bool enableplayers = true;
			public Dictionary<int, PlayerItemList> PlayerItemList = new Dictionary<int, PlayerItemList>
			{
				[0] = new PlayerItemList
				{
					shortname = "scrap",
					displayname = "",
					amount = 2,
					SkinID = 0
				},
				[1] = new PlayerItemList
				{
					shortname = "metal.fragments",
					displayname = "",
					amount = 1,
					SkinID = 0
				},
			};
		}
		public class GatheringItemList
		{
			public string shortname = "";
			public string displayname = "";
			public int amount = 1;
			public ulong SkinID = 0;
		}
		public class MiningItemList
		{
			public string shortname = "";
			public string displayname = "";
			public int amount = 1;
			public ulong SkinID = 0;
		}
		public class LootingItemList
		{
			public string shortname = "";
			public string displayname = "";
			public int amount = 1;
			public ulong SkinID = 0;
		}
		public class AnimalItemList
		{
			public string shortname = "";
			public string displayname = "";
			public int amount = 1;
			public ulong SkinID = 0;
		}
		public class NPCItemList
		{
			public string shortname = "";
			public string displayname = "";
			public int amount = 1;
			public ulong SkinID = 0;
		}
		public class PlayerItemList
		{
			public string shortname = "";
			public string displayname = "";
			public int amount = 1;
			public ulong SkinID = 0;
		}
		protected override void LoadDefaultConfig() => config = new Configuration();
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null)
				{
					throw new JsonException();
				}
				if (MaybeUpdateConfig(config))
				{
					PrintWarning("Configuration appears to be outdated; updating and saving");
					SaveConfig();
				}
			}
			catch
			{
				PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
				LoadDefaultConfig();
			}
		}
		protected override void SaveConfig()
		{
			PrintWarning($"Configuration changes saved to {Name}.json");
			Config.WriteObject(config, true);
		}
		#endregion

		#region UpdateChecker
		internal class SerializableConfiguration
		{
			public string ToJson() => JsonConvert.SerializeObject(this);

			public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
		}
		private static class JsonHelper
		{
			public static object Deserialize(string json) => ToObject(JToken.Parse(json));

			private static object ToObject(JToken token)
			{
				switch (token.Type)
				{
					case JTokenType.Object:
						return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
					case JTokenType.Array:
						return token.Select(ToObject).ToList();

					default:
						return ((JValue)token).Value;
				}
			}
		}
		private bool MaybeUpdateConfig(SerializableConfiguration config)
		{
			var currentWithDefaults = config.ToDictionary();
			var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
			return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
		}
		private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
		{
			bool changed = false;

			foreach (var key in currentWithDefaults.Keys)
			{
				object currentRawValue;
				if (currentRaw.TryGetValue(key, out currentRawValue))
				{
					var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
					var currentDictValue = currentRawValue as Dictionary<string, object>;

					if (defaultDictValue != null)
					{
						if (currentDictValue == null)
						{
							currentRaw[key] = currentWithDefaults[key];
							changed = true;
						}
						else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
							changed = true;
					}
				}
				else
				{
					currentRaw[key] = currentWithDefaults[key];
					changed = true;
				}
			}

			return changed;
		}
		#endregion

		#region Storage
		private void SaveLoot()
		{
			if (_CID_lootData != null)
			{
				_CID_lootData.CID_LootRecords = _CID_lootCache;
				_CID_LootContainData.WriteObject(_CID_lootData);
			}
		}
		private class LootData
		{
			public Dictionary<NetworkableId, Loot> CID_LootRecords = new Dictionary<NetworkableId, Loot>();
		}
		private class Loot
		{
			public NetworkableId lootcontainer;
			public List<string> id;
		}
		private void AddLootData(BasePlayer player, LootContainer lootcontainer)
		{
			Loot loot;
			if (!_CID_lootCache.TryGetValue(lootcontainer.net.ID, out loot))
			{
				_CID_lootCache.Add(lootcontainer.net.ID, loot = new Loot
				{
					lootcontainer = lootcontainer.net.ID,
					id = new List<string>(),
				});
			}
			if (!loot.id.Contains(player.UserIDString))
			{
				loot.id.Add(player.UserIDString);
			}
		}
		#endregion

		#region Load/Save
		private void Init()
		{
			permission.RegisterPermission(PermGatheringChance, this);
			permission.RegisterPermission(PermMiningChance, this);
			permission.RegisterPermission(PermLootingChance, this);
			permission.RegisterPermission(PermAnimalChance, this);
			permission.RegisterPermission(PermNPCChance, this);
			permission.RegisterPermission(PermPlayerChance, this);
			permission.RegisterPermission(PermVIPChance, this);
			_CID_lootCache = new Dictionary<NetworkableId, Loot>();
		}
		private void OnServerInitialized()
		{
			_CID_LootContainData = Interface.Oxide.DataFileSystem.GetFile(nameof(CustomItemDrops) + "/CIDLootData");
			SaveLoot();
		}
		private void Unload()
        {
			SaveLoot();
		}
		private void OnServerShutdown()
		{
			_CID_lootCache.Clear();
			_CID_LootContainData.Clear();
			SaveLoot();
		}
		private void OnServerSave()
		{
			SaveLoot();
		}
		#endregion

		#region Hooks
		private void OnLootSpawn(LootContainer container)
		{
			if (container != null && _CID_lootCache.ContainsKey(container.net.ID))
			{
				_CID_lootCache[container.net.ID].id.Clear();
			}
		}
		private void OnLootEntity(BasePlayer player, LootContainer lootcontainer)
		{
			// Null Checks
			if (player == null || !player.userID.Get().IsSteamId() || !lootcontainer.IsValid()) return;

			// Check Settings, Permissions, and Data
			if (config.lootingoptions.dropchance == 0) return;
			if (!permission.UserHasPermission(player.UserIDString, PermLootingChance)) return;
			var loot = lootcontainer.GetType().Name.ToLower();
			var lootid = lootcontainer.net.ID;
			if (_CID_lootCache.ContainsKey(lootid) && _CID_lootCache[lootid].id.Contains(player.UserIDString))
			{
				return;
			}
			int chance = config.lootingoptions.dropchance;
			if (permission.UserHasPermission(player.UserIDString, PermVIPChance))
			{
				chance = config.lootingoptions.vipdropchance;
			}

			// Check Container Types
			bool resource = false;
			switch (loot)
			{
				case "lootcontainer":
					if (config.lootingoptions.lootcontainer) resource = true;
					break;
				case "freeablelootcontainer":
					if (config.lootingoptions.freeablelootcontainer) resource = true;
					break;
				case "lockedbyentcrate":
					if (config.lootingoptions.lockedbyentcrate) resource = true;
					break;
				case "hackablelockedcrate":
					if (config.lootingoptions.hackablelockedcrate) resource = true;
					break;
			}

			// Calculate Chance & Give Items
			if ((Random.Range(0, 101) <= chance) == true && resource)
			{
				CreateItemFromList(player, "lootinglist");
			}

			// Add Player & Container to Data to Prevent Exploits
			AddLootData(player, lootcontainer);
		}
		private void OnContainerDropItems(ItemContainer lootcontainer)
		{
			// Null Checks
			if (lootcontainer == null) return;
			var lootentity = lootcontainer.entityOwner as LootContainer;
			if (lootentity == null || lootentity.IsDestroyed) return;
			var player = lootentity.lastAttacker as BasePlayer;
			if (player == null || player.IsNpc) return;

			// Check Settings & Permissions
			if (!permission.UserHasPermission(player.UserIDString, PermLootingChance)) return;
			int chance = config.lootingoptions.dropchance;
			if (permission.UserHasPermission(player.UserIDString, PermVIPChance))
			{
				chance = config.lootingoptions.vipdropchance;
			}

			// Calculate Chance & Give Items
			if ((Random.Range(0, 101) <= chance) == true)
			{
				CreateItemFromList(player, "lootinglist");
			}
		}
		private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
		{
			// Check for null or Suicide
			if (entity == null || hitInfo == null || hitInfo.Initiator == null || entity == hitInfo.Initiator) return;

			// Get Killer & Ignore NPCs
			var attacker = hitInfo.Initiator as BasePlayer;
			if (attacker == null || !attacker.userID.Get().IsSteamId() || attacker.IsNpc) return;

			// Detect Teamkills
			if (attacker.Team != null)
			{
				foreach (var team in attacker.Team.members)
				{
					if (team == attacker.userID) continue;
					BasePlayer teammember = RelationshipManager.FindByID(team);
					var isteammember = entity as BasePlayer;
					if (isteammember != null && isteammember.userID.Get().IsSteamId()) continue;
					if (teammember == isteammember) return;
				}
			}

			// Detect Kill Type & Pick Proper List
			string KillType = entity?.GetType().Name.ToLower();
			bool animalkill = false;
			bool npckill = false;
			bool playerkill = false;
			string droplist = "none";
			switch (KillType)
			{
				case "chicken":
					if(config.animaloptions.chicken)
                    {
						animalkill = true;
						droplist = "animal";
                    }
					break;
				case "boar":
					if (config.animaloptions.boar)
					{
						animalkill = true;
						droplist = "animal";
					}
					break;
				case "stag":
					if (config.animaloptions.stag)
					{
						animalkill = true;
						droplist = "animal";
					}
					break;
				case "wolf":
					if (config.animaloptions.wolf)
					{
						animalkill = true;
						droplist = "animal";
					}
					break;
				case "bear":
					if (config.animaloptions.bear)
					{
						animalkill = true;
						droplist = "animal";
					}
					break;
				case "polarbear":
					if (config.animaloptions.polarbear)
					{
						animalkill = true;
						droplist = "animal";
					}
					break;
				case "simpleshark":
					if (config.animaloptions.shark)
					{
						animalkill = true;
						droplist = "animal";
					}
					break;
				case "horse":
				case "ridablehorse":
					if (config.animaloptions.horse)
					{
						animalkill = true;
						droplist = "animal";
					}
					break;
				case "scientistnpc":
				case "scientist":
					if (config.NPCoptions.scientist)
					{
						npckill = true;
						droplist = "npc";
					}
					break;
				case "tunneldweller":
				case "underwaterdweller":
					if (config.NPCoptions.dweller)
					{
						npckill = true;
						droplist = "npc";
					}
					break;
				case "bradleyapc":
					if (config.NPCoptions.bradley)
					{
						npckill = true;
						droplist = "npc";
					}
					break;
				case "patrolhelicopter":
					if (config.NPCoptions.heli)
					{
						npckill = true;
						droplist = "npc";
					}
					break;
				case "scarecrownpc":
					if (config.NPCoptions.scarcrow)
					{
						npckill = true;
						droplist = "npc";
					}
					break;
				case "customscientistnpc":
					if (config.NPCoptions.customnpc)
					{
						npckill = true;
						droplist = "npc";
					}
					break;
				case "zombienpc":
					if (config.NPCoptions.zombie)
					{
						npckill = true;
						droplist = "npc";
					}
					break;
				case "baseplayer":
					if(config.Playeroptions.enableplayers)
                    {
						playerkill = true;
						droplist = "player";
                    }
					break;
			}

			// Check Settings & Permissions
			int chance = 0;
			if (animalkill)
			{
				// Check Settings & Permissions
				if (!permission.UserHasPermission(attacker.UserIDString, PermAnimalChance)) return;
				chance = config.animaloptions.dropchance;
				if (permission.UserHasPermission(attacker.UserIDString, PermVIPChance))
				{
					chance = config.animaloptions.vipdropchance;
				}
			}
			else if(npckill)
            {
				if (!permission.UserHasPermission(attacker.UserIDString, PermNPCChance)) return;
				chance = config.NPCoptions.dropchance;
				if (permission.UserHasPermission(attacker.UserIDString, PermVIPChance))
				{
					chance = config.NPCoptions.vipdropchance;
				}
			}
			else if(playerkill)
            {
				if (!permission.UserHasPermission(attacker.UserIDString, PermPlayerChance)) return;
				chance = config.Playeroptions.dropchance;
				if (permission.UserHasPermission(attacker.UserIDString, PermVIPChance))
                {
					chance = config.Playeroptions.vipdropchance;
                }
            }

			// Calculate Chance & Give Items
			if ((Random.Range(0, 101) <= chance) == true && (animalkill || npckill || playerkill))
			{
				CreateItemFromList(attacker, droplist);
			}
		}
		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
			// Null Checks
			var player = entity.ToPlayer();
			if (player == null || !player.userID.Get().IsSteamId() || dispenser == null || entity == null || item == null) return;
			bool resource = false;
            var tool = player.GetActiveItem().ToString().ToLower();
			if (config.generalSettings.disablepowertoolchance && (tool.Contains("chainsaw") || tool.Contains("jackhammer"))) return;
            switch (dispenser.gatherType)
            {
				// Trees - Gathering
				case ResourceDispenser.GatherType.Tree:
					
					// Check Settings & Permissions
					if (config.gatheringoptions.trees) resource = true;
					if (!permission.UserHasPermission(player.UserIDString, PermGatheringChance)) return;
					if (config.gatheringoptions.dropchance == 0) return;
					int gchance = config.gatheringoptions.dropchance;
					if (permission.UserHasPermission(player.UserIDString, PermVIPChance))
					{
						gchance = config.gatheringoptions.vipdropchance;
					}
					
					// Calculate Chance & Give Items
					if ((Random.Range(0, 101) <= gchance) == true && resource)
					{
						CreateItemFromList(player, "gatheringlist");
					}
					break;

				// Ore - Mining
				case ResourceDispenser.GatherType.Ore:

					// Check Settings & Permissions
					if (!permission.UserHasPermission(player.UserIDString, PermMiningChance)) return;
					if (config.miningoptions.dropchance == 0) return;
					int mchance = config.miningoptions.dropchance;
					if (permission.UserHasPermission(player.UserIDString, PermVIPChance))
					{
						mchance = config.miningoptions.vipdropchance;
					}
					
					// Calculate Chance & Give Items
					if ((Random.Range(0, 101) <= mchance) == true)
					{
						CreateItemFromList(player, "mininglist");
					}
					break;
			}
		}
		private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
		{
			// Null Checks
			if (player == null || !player.userID.Get().IsSteamId() || collectible == null) return;

			// Check Settings & Permissions
			if (config.gatheringoptions.dropchance == 0) return;
			if (!permission.UserHasPermission(player.UserIDString, PermGatheringChance)) return;
			int gchance = config.gatheringoptions.dropchance;
			if (permission.UserHasPermission(player.UserIDString, PermVIPChance))
			{
				gchance = config.gatheringoptions.vipdropchance;
			}

			// Check Gathering Types
			bool resource = false;
			foreach (var itemAmount in collectible.itemList)
			{
				var name = itemAmount.itemDef.shortname;
				if (name.Contains("berry") && !name.Contains("seed"))
				{
					name = "berry";
				}
				else if (name.Contains("ore"))
				{
					name = "ore";
				}
				switch (name)
				{
					case "wood":
						if (config.gatheringoptions.wood) resource = true;
						break;
					case "ore":
						if (config.gatheringoptions.ore) resource = true;
						break;
					case "stones":
						if (config.gatheringoptions.stones) resource = true;
						break;
					case "berry":
						if (config.gatheringoptions.berries) resource = true;
						break;
					case "mushroom":
						if (config.gatheringoptions.mushrooms) resource = true;
						break;
					case "cloth":
						if (config.gatheringoptions.hemp) resource = true;
						break;
					case "pumpkin":
						if (config.gatheringoptions.pumpkins) resource = true;
						break;
					case "corn":
						if (config.gatheringoptions.corn) resource = true;
						break;
					case "potato":
						if (config.gatheringoptions.potatos) resource = true;
						break;
				}
				// Calculate Chance & Give Items
				if ((Random.Range(0, 101) <= gchance) == true && resource)
				{
					CreateItemFromList(player, "gatheringlist");
				}
			}
		}
		private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
		{
			// Null Checks
			if (player == null || !player.userID.Get().IsSteamId() || growable == null || item == null) return;

			// Check Settings & Permissions
			if (config.gatheringoptions.dropchance == 0) return;
			if (!permission.UserHasPermission(player.UserIDString, PermGatheringChance)) return;
			int gchance = config.gatheringoptions.dropchance;
			if (permission.UserHasPermission(player.UserIDString, PermVIPChance))
			{
				gchance = config.gatheringoptions.vipdropchance;
			}

			// Check Gathering Types
			bool resource = false;
			var name = item.info.shortname;
			if (name.Contains("berry") && !name.Contains("seed"))
			{
				name = "berry";
			}
			else if (name.Contains("ore"))
			{
				name = "ore";
			}
			switch (name)
			{
				case "wood":
					if (config.gatheringoptions.wood) resource = true;
					break;
				case "ore":
					if (config.gatheringoptions.ore) resource = true;
					break;
				case "stones":
					if (config.gatheringoptions.stones) resource = true;
					break;
				case "berry":
					if (config.gatheringoptions.berries) resource = true;
					break;
				case "mushroom":
					if (config.gatheringoptions.mushrooms) resource = true;
					break;
				case "cloth":
					if (config.gatheringoptions.hemp) resource = true;
					break;
				case "pumpkin":
					if (config.gatheringoptions.pumpkins) resource = true;
					break;
				case "corn":
					if (config.gatheringoptions.corn) resource = true;
					break;
				case "potato":
					if (config.gatheringoptions.potatos) resource = true;
					break;
			}

			// Calculate Chance & Give Items
			if ((Random.Range(0, 101) <= gchance) == true && resource)
			{
				CreateItemFromList(player, "gatheringlist");
			}
		}
		private void CreateItemFromList(BasePlayer player, string selectedlist)
        {
			if (player == null || selectedlist == null) return;
			switch (selectedlist)
            {
                // Gathering List
                #region Gathering
                case "gatheringlist":

					// Get Random Item From List
					int randomroll = Random.Range(0, config.gatheringoptions.gatheringItemList.Count);
					
					// Check for Valid Item & Get Item Details
					var gselected = config.gatheringoptions.gatheringItemList[randomroll];
					ItemDefinition gdefinition = ItemManager.FindItemDefinition(gselected.shortname);
					if (gdefinition == null)
					{
						Puts($"[Gathering List] Invalid shortname in config for item {gselected.shortname}");
						return;
					}

					// Create Item & Get Skin Needed
					Item gcreateitem = ItemManager.CreateByItemID(gdefinition.itemid, gselected.amount, gselected.SkinID);

					// Change Name if Needed
					var gitemdisplayname = gcreateitem.info.displayName.english;
					if (!string.IsNullOrEmpty(gselected.displayname))
					{
						gcreateitem.name = gselected.displayname;
						gcreateitem.MarkDirty();
						gitemdisplayname = gselected.displayname;
					}

					// Return if Item is Null
					if (gcreateitem == null)
					{
						Puts($"[Looting List] Error creating item with skinid {gselected.SkinID} for item {gselected.shortname}");
						return;
					}

					// Give Item to Player
					player.GiveItem(gcreateitem);

					// Send Chat Message if Enabled
					if (config.generalSettings.showchatmessage)
					{
						player.ChatMessage(CIDLang("giveitem", player.UserIDString, gselected.amount, gitemdisplayname));
					}
					break;
                #endregion
                // Mining List
                #region Mining
                case "mininglist":

					// Get Random Item From List
					randomroll = Random.Range(0, config.miningoptions.miningItemList.Count);

					// Check for Valid Item & Get Item Details
					var mselected = config.miningoptions.miningItemList[randomroll];
					ItemDefinition mdefinition = ItemManager.FindItemDefinition(mselected.shortname);
					if (mdefinition == null)
					{
						Puts($"[Mining List] Invalid shortname in config for item {mselected.shortname}");
						return;
					}

					// Create Item & Get Skin Needed
					Item mcreateitem = ItemManager.CreateByItemID(mdefinition.itemid, mselected.amount, mselected.SkinID);

					// Change Name if Needed
					var mitemdisplayname = mcreateitem.info.displayName.english;
					if (!string.IsNullOrEmpty(mselected.displayname))
					{
						mcreateitem.name = mselected.displayname;
						mcreateitem.MarkDirty();
						mitemdisplayname = mselected.displayname;
					}

					// Return if Item is Null
					if (mcreateitem == null)
					{
						Puts($"[Looting List] Error creating item with skinid {mselected.SkinID} for item {mselected.shortname}");
						return;
					}

					// Give Item to Player
					player.GiveItem(mcreateitem);

					// Send Chat Message if Enabled
					if (config.generalSettings.showchatmessage)
					{
						player.ChatMessage(CIDLang("giveitem", player.UserIDString, mselected.amount, mitemdisplayname));
					}
					break;
                #endregion
                // Looting List
                #region Looting
                case "lootinglist":

					// Get Random Item From List
					randomroll = Random.Range(0, config.lootingoptions.lootingItemList.Count);

					// Check for Valid Item & Get Item Details
					var lselected = config.lootingoptions.lootingItemList[randomroll];
					ItemDefinition ldefinition = ItemManager.FindItemDefinition(lselected.shortname);
					if (ldefinition == null)
					{
						Puts($"[Looting List] Invalid shortname in config for item {lselected.shortname}");
						return;
					}

					// Create Item & Get Skin Needed
					Item lcreateitem = ItemManager.CreateByItemID(ldefinition.itemid, lselected.amount, lselected.SkinID);

					// Change Name if Needed
					var litemdisplayname = lcreateitem.info.displayName.english;
					if (!string.IsNullOrEmpty(lselected.displayname))
					{	
						lcreateitem.name = lselected.displayname;
						lcreateitem.MarkDirty();
						litemdisplayname = lselected.displayname;
					}

					// Return if Item is Null
					if (lcreateitem == null)
					{
						Puts($"[Looting List] Error creating item with skinid {lselected.SkinID} for item {lselected.shortname}");
						return;
					}

					// Give Item to Player
					player.GiveItem(lcreateitem);

					// Send Chat Message if Enabled
					if (config.generalSettings.showchatmessage)
					{
						player.ChatMessage(CIDLang("giveitem", player.UserIDString, lselected.amount, litemdisplayname));
					}
					break;
				#endregion
				// Animal Kills
				#region AnimalKills
				case "animal":

					// Get Random Item From List
					randomroll = Random.Range(0, config.animaloptions.AnimalItemList.Count);

					// Check for Valid Item & Get Item Details
					var aselected = config.animaloptions.AnimalItemList[randomroll];
					ItemDefinition adefinition = ItemManager.FindItemDefinition(aselected.shortname);
					if (adefinition == null)
					{
						Puts($"[Animal List] Invalid shortname in config for item {aselected.shortname}");
						return;
					}

					// Create Item & Get Skin Needed
					Item acreateitem = ItemManager.CreateByItemID(adefinition.itemid, aselected.amount, aselected.SkinID);

					// Change Name if Needed
					var aitemdisplayname = acreateitem.info.displayName.english;
					if (!string.IsNullOrEmpty(aselected.displayname))
					{
						acreateitem.name = aselected.displayname;
						acreateitem.MarkDirty();
						aitemdisplayname = aselected.displayname;
					}

					// Return if Item is Null
					if (acreateitem == null)
					{
						Puts($"[Animal List] Error creating item with skinid {aselected.SkinID} for item {aselected.shortname}");
						return;
					}

					// Give Item to Player
					player.GiveItem(acreateitem);

					// Send Chat Message if Enabled
					if (config.generalSettings.showchatmessage)
					{
						player.ChatMessage(CIDLang("giveitem", player.UserIDString, aselected.amount, aitemdisplayname));
					}

					break;
				#endregion
				// NPC Kills
				#region NPCKills
				case "npc":

					// Get Random Item From List
					randomroll = Random.Range(0, config.NPCoptions.NPCItemList.Count);

					// Check for Valid Item & Get Item Details
					var nselected = config.NPCoptions.NPCItemList[randomroll];
					ItemDefinition ndefinition = ItemManager.FindItemDefinition(nselected.shortname);
					if (ndefinition == null)
					{
						Puts($"[NPC List] Invalid shortname in config for item {nselected.shortname}");
						return;
					}

					// Create Item & Get Skin Needed
					Item ncreateitem = ItemManager.CreateByItemID(ndefinition.itemid, nselected.amount, nselected.SkinID);

					// Change Name if Needed
					var nitemdisplayname = ncreateitem.info.displayName.english;
					if (!string.IsNullOrEmpty(nselected.displayname))
					{
						ncreateitem.name = nselected.displayname;
						ncreateitem.MarkDirty();
						nitemdisplayname = nselected.displayname;
					}

					// Return if Item is Null
					if (ncreateitem == null)
					{
						Puts($"[NPC List] Error creating item with skinid {nselected.SkinID} for item {nselected.shortname}");
						return;
					}

					// Give Item to Player
					player.GiveItem(ncreateitem);

					// Send Chat Message if Enabled
					if (config.generalSettings.showchatmessage)
					{
						player.ChatMessage(CIDLang("giveitem", player.UserIDString, nselected.amount, nitemdisplayname));
					}

					break;


                #endregion
				// Player Kills
				#region PlayerKills
				case "player":

					// Get Random Item From List
					randomroll = Random.Range(0, config.Playeroptions.PlayerItemList.Count);

					// Check for Valid Item & Get Item Details
					var pselected = config.Playeroptions.PlayerItemList[randomroll];
					ItemDefinition pdefinition = ItemManager.FindItemDefinition(pselected.shortname);
					if (pdefinition == null)
					{
						Puts($"[Player List] Invalid shortname in config for item {pselected.shortname}");
						return;
					}

					// Create Item & Get Skin Needed
					Item pcreateitem = ItemManager.CreateByItemID(pdefinition.itemid, pselected.amount, pselected.SkinID);

					// Change Name if Needed
					var pitemdisplayname = pcreateitem.info.displayName.english;
					if (!string.IsNullOrEmpty(pselected.displayname))
					{
						pcreateitem.name = pselected.displayname;
						pcreateitem.MarkDirty();
						pitemdisplayname = pselected.displayname;
					}

					// Return if Item is Null
					if (pcreateitem == null)
					{
						Puts($"[Player List] Error creating item with skinid {pselected.SkinID} for item {pselected.shortname}");
						return;
					}

					// Give Item to Player
					player.GiveItem(pcreateitem);

					// Send Chat Message if Enabled
					if (config.generalSettings.showchatmessage)
					{
						player.ChatMessage(CIDLang("giveitem", player.UserIDString, pselected.amount, pitemdisplayname));
					}

					break;


                #endregion
            }
        }
		#endregion

		#region Lang
		protected override void LoadDefaultMessages()
        {
	        lang.RegisterMessages(new Dictionary<string, string>
	        {
		        ["giveitem"] = "You have received {0} {1} in your inventory.",

			}, this);
        }
        private string CIDLang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
		#endregion

		#region API
		private int GetItemDropChance(string list, string type)
        {
			if (list == null) return 0;
			int chance = 0;
			switch(list)
            {
				case "gathering":
					switch(type)
                    {
						case "default":
							chance = config.gatheringoptions.dropchance;
							break;
						case "vip":
							chance = config.gatheringoptions.vipdropchance;
							break;
                    }
					break;
				case "mining":
					switch (type)
					{
						case "default":
							chance = config.miningoptions.dropchance;
							break;
						case "vip":
							chance = config.miningoptions.vipdropchance;
							break;
					}
					break;
				case "looting":
					switch (type)
					{
						case "default":
							chance = config.lootingoptions.dropchance;
							break;
						case "vip":
							chance = config.lootingoptions.vipdropchance;
							break;
					}
					break;
				case "animal":
					switch (type)
					{
						case "default":
							chance = config.animaloptions.dropchance;
							break;
						case "vip":
							chance = config.animaloptions.vipdropchance;
							break;
					}
					break;
				case "npc":
					switch (type)
					{
						case "default":
							chance = config.NPCoptions.dropchance;
							break;
						case "vip":
							chance = config.NPCoptions.vipdropchance;
							break;
					}
					break;
				case "player":
					switch(type)
                    {
						case "default":
							chance = config.Playeroptions.dropchance;
							break;
						case "vip":
							chance = config.Playeroptions.vipdropchance;
							break;
                    }
					break;
			}
			return chance;
        }
		private string GetItemDropLists(string list)
        {
			if (list == null) return null;
			switch(list)
            {
				case "gathering":
					list = JsonConvert.SerializeObject(config.gatheringoptions.gatheringItemList);
					break;
				case "mining":
					list = JsonConvert.SerializeObject(config.miningoptions.miningItemList);
					break;
				case "looting":
					list = JsonConvert.SerializeObject(config.lootingoptions.lootingItemList);
					break;
				case "animal":
					list = JsonConvert.SerializeObject(config.animaloptions.AnimalItemList);
					break;
				case "npc":
					list = JsonConvert.SerializeObject(config.NPCoptions.NPCItemList);
					break;
				case "player":
					list = JsonConvert.SerializeObject(config.Playeroptions.PlayerItemList);
					break;
			}
			return list;
        }
		#endregion

	}
}