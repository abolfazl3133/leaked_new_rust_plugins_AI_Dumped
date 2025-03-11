/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/activity-rewards
*  Codefling license: https://codefling.com/plugins/activity-rewards?tab=downloads_field_4
*
*  Copyright © 2024 IIIaKa
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("ActivityRewards", "IIIaKa", "0.1.3")]
	[Description("Plugin rewarding players for their in-game activity.")]
	class ActivityRewards : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, BankSystem, ServerRewards, Economics, AdvancedStatus, ServerPanel;

        #region ~Variables~
		private bool _imgLibIsLoaded = false;
		private bool _statusIsLoaded = false;
		private const string BankSystemBar = "ActivityRewards_BankSystem", ServerRewardsBar = "ActivityRewards_ServerRewards", EconomicsBar = "ActivityRewards_Economics", StatusCreateBar = "CreateBar", HttpScheme = "http://", HttpsScheme = "https://";
		private Hash<ulong, ulong> _patrolLastHit = new Hash<ulong, ulong>();
		private HashSet<EcoPlugin> _ecoPlugins = new HashSet<EcoPlugin>();
		private HashSet<string> _hooks = new HashSet<string>();
		private HashSet<string> _defaultEco = new HashSet<string>() { "BankSystem", "ServerRewards", "Economics" };
		private readonly Dictionary<string, PluginsRewards> _defaultGatherReward = new Dictionary<string, PluginsRewards>() { { "wood", new PluginsRewards(5, 0.5f) }, { "stones", new PluginsRewards(10, 1f) }, { "metal.ore", new PluginsRewards(15, 1.5f) }, { "sulfur.ore", new PluginsRewards(20, 2f) } };
		private readonly Dictionary<string, PluginsRewards> _defaultKillReward = new Dictionary<string, PluginsRewards>()
		{
			{ "player", new PluginsRewards(10, 1f) }, { "suicide", new PluginsRewards(-5, -0.5f) }, { "loot-barrel", new PluginsRewards(5, 0.5f) }, { "loot_barrel", new PluginsRewards(5, 0.5f) }, { "oil_barrel", new PluginsRewards(10, 1f) }, { "roadsign", new PluginsRewards(5, 0.5f) },
			{ "scientistnpc", new PluginsRewards(15, 1.5f) }, { "npc_tunneldweller", new PluginsRewards(15, 1.5f) }, { "npc_underwaterdweller", new PluginsRewards(15, 1.5f) }, { "scientistnpc_junkpile_pistol", new PluginsRewards(10, 1f) }, { "scientistnpc_heavy", new PluginsRewards(20, 2f) },
			{ "chicken", new PluginsRewards(5, 0.5f) }, { "boar", new PluginsRewards(10, 1f) }, { "stag", new PluginsRewards(15, 1.5f) }, { "wolf", new PluginsRewards(20, 2f) }, { "bear", new PluginsRewards(20, 2f) }, { "simpleshark", new PluginsRewards(30, 3f) },
			{ "chicken.corpse", new PluginsRewards(3, 0.25f) }, { "boar.corpse", new PluginsRewards(5, 0.5f) }, { "stag.corpse", new PluginsRewards(7, 0.75f) }, { "wolf.corpse", new PluginsRewards(10, 1f) }, { "bear.corpse", new PluginsRewards(10, 1f) }, { "shark.corpse", new PluginsRewards(15, 1.5f) },
			{ "patrolhelicopter", new PluginsRewards(100, 10f) }, { "bradleyapc", new PluginsRewards(100, 10f) }
		};
		private readonly Dictionary<string, PluginsRewards> _defaultOpenningReward = new Dictionary<string, PluginsRewards>()
		{
			{ "foodbox", new PluginsRewards(5, 0.5f) }, { "crate_food_1", new PluginsRewards(5, 0.5f) }, { "crate_food_2", new PluginsRewards(5, 0.5f) },
			{ "crate_normal_2_food", new PluginsRewards(10, 1f) }, { "wagon_crate_normal_2_food", new PluginsRewards(10, 1f) }, { "crate_normal_2_medical", new PluginsRewards(10, 1f) }, { "vehicle_parts", new PluginsRewards(5, 0.5f) },
			{ "crate_basic", new PluginsRewards(5, 0.5f) }, { "crate_normal_2", new PluginsRewards(10, 1f) }, { "crate_mine", new PluginsRewards(10, 1f) }, { "crate_tools", new PluginsRewards(15, 1.5f) }, { "crate_normal", new PluginsRewards(20, 2f) }, { "crate_elite", new PluginsRewards(25, 2.5f) },
			{ "crate_underwater_basic", new PluginsRewards(5, 0.5f) }, { "crate_underwater_advanced", new PluginsRewards(10, 1f) },
			{ "crate_medical", new PluginsRewards(5, 0.5f) }, { "crate_fuel", new PluginsRewards(10, 1f) }, { "crate_ammunition", new PluginsRewards(10, 1f) }, { "heli_crate", new PluginsRewards(30, 3f) }, { "bradley_crate", new PluginsRewards(30, 3f) },
			{ "codelockedhackablecrate", new PluginsRewards(100, 10f) }, { "codelockedhackablecrate_oilrig", new PluginsRewards(100, 10f) }
		};
		private readonly Dictionary<string, PluginsRewards> _defaultPickupReward = new Dictionary<string, PluginsRewards>()
        {
            { "Wood", new PluginsRewards(1, 0.1f) }, { "Stone", new PluginsRewards(2, 0.25f) }, { "Metal Ore", new PluginsRewards(5, 0.5f) }, { "Sulfur Ore", new PluginsRewards(7, 0.75f) }, { "Green Keycard", new PluginsRewards(10, 1f) }, { "Blue Keycard", new PluginsRewards(20, 2f) }, { "Red Keycard", new PluginsRewards(30, 3f) },
            { "Diesel Fuel", new PluginsRewards(10, 1f) }, { "Bones", new PluginsRewards(1, 0.1f) }, { "Corn", new PluginsRewards(1, 0.1f) }, { "Potato", new PluginsRewards(1, 0.1f) }, { "Pumpkin", new PluginsRewards(1, 0.1f) }, { "Wild Mushroom", new PluginsRewards(1, 0.1f) }, { "Hemp Fibers", new PluginsRewards(1, 0.1f) },
            { "Black Berry", new PluginsRewards(1, 0.1f) }, { "Blue Berry", new PluginsRewards(1, 0.1f) }, { "Green Berry", new PluginsRewards(1, 0.1f) }, { "Red Berry", new PluginsRewards(1, 0.1f) }, { "White Berry", new PluginsRewards(1, 0.1f) }, { "Yellow Berry", new PluginsRewards(1, 0.1f) }
        };
		private readonly Dictionary<string, PluginsRewards> _defaultPlantReward = new Dictionary<string, PluginsRewards>()
		{
			{ "hemp.entity", new PluginsRewards(1, 0.1f) }, { "corn.entity", new PluginsRewards(1, 0.1f) }, { "pumpkin.entity", new PluginsRewards(1, 0.1f) }, { "potato.entity", new PluginsRewards(1, 0.1f) }, { "black_berry.entity", new PluginsRewards(1, 0.1f) },
			{ "blue_berry.entity", new PluginsRewards(1, 0.1f) }, { "green_berry.entity", new PluginsRewards(1, 0.1f) }, { "red_berry.entity", new PluginsRewards(1, 0.1f) }, { "white_berry.entity", new PluginsRewards(1, 0.1f) }, { "yellow_berry.entity", new PluginsRewards(1, 0.1f) }
		};
		private readonly Dictionary<string, PluginsRewards> _defaultFishingReward = new Dictionary<string, PluginsRewards>()
        {
			{ "fish.minnows", new PluginsRewards(1, 0.1f) }, { "fish.anchovy", new PluginsRewards(3, 0.3f) }, { "fish.herring", new PluginsRewards(3, 0.3f) }, { "fish.sardine", new PluginsRewards(3, 0.3f) }, { "fish.troutsmall", new PluginsRewards(5, 0.5f) },
			{ "fish.yellowperch", new PluginsRewards(5, 0.5f) }, { "fish.salmon", new PluginsRewards(10, 1f) }, { "fish.catfish", new PluginsRewards(10, 1f) }, { "fish.orangeroughy", new PluginsRewards(10, 1f) }, { "fish.smallshark", new PluginsRewards(50, 5f) }
		};
		private readonly Dictionary<string, float> _defaultPermissions = new Dictionary<string, float>() { { "realpve.default", 1f }, { "realpve.vip", 1.1f } };
        private readonly BarSettings _defaultBankSystem = new BarSettings()
        {
            BarID = BankSystemBar,
            Order = 20,
            Height = 26,
            Main_Color = "#84AB49",
            Main_Transparency = 0.8f,
            Main_Material = "",
            Image_Url = "https://i.imgur.com/k8jq7yY.png",
            Image_Local = "ActivityRewards_BankSystem",
            Image_Sprite = "",
            Image_IsRawImage = false,
            Image_Color = "#B9D134",
            Image_Transparency = 1f,
            Text_Key = "MsgBankSystem",
            Text_Size = 12,
            Text_Color = "#DAEBAD",
            Text_Font = "RobotoCondensed-Bold.ttf",
            SubText_Size = 12,
            SubText_Color = "#DAEBAD",
            SubText_Font = "RobotoCondensed-Bold.ttf"
        };
        private readonly BarSettings _defaultServerRewards = new BarSettings()
        {
            BarID = ServerRewardsBar,
            Order = 20,
            Height = 26,
            Main_Color = "#84AB49",
            Main_Transparency = 0.8f,
            Main_Material = "",
            Image_Url = "https://i.imgur.com/k8jq7yY.png",
            Image_Local = "ActivityRewards_ServerRewards",
            Image_Sprite = "",
            Image_IsRawImage = false,
            Image_Color = "#B9D134",
            Image_Transparency = 1f,
            Text_Key = "MsgServerRewards",
            Text_Size = 12,
            Text_Color = "#DAEBAD",
            Text_Font = "RobotoCondensed-Bold.ttf",
            SubText_Size = 12,
            SubText_Color = "#DAEBAD",
            SubText_Font = "RobotoCondensed-Bold.ttf"
        };
        private readonly BarSettings _defaultEconomics = new BarSettings()
        {
            BarID = EconomicsBar,
            Order = 20,
            Height = 26,
            Main_Color = "#84AB49",
            Main_Transparency = 0.8f,
            Main_Material = "",
            Image_Url = "https://i.imgur.com/k8jq7yY.png",
            Image_Local = "ActivityRewards_Economics",
            Image_Sprite = "",
            Image_IsRawImage = false,
            Image_Color = "#B9D134",
            Image_Transparency = 1f,
            Text_Key = "MsgEconomics",
            Text_Size = 12,
            Text_Color = "#DAEBAD",
            Text_Font = "RobotoCondensed-Bold.ttf",
            SubText_Size = 12,
            SubText_Color = "#DAEBAD",
            SubText_Font = "RobotoCondensed-Bold.ttf"
        };
		#endregion

        #region ~Configuration~
        private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "List of reward plugins")]
			public HashSet<string> EcoPlugins = new HashSet<string>();
			
			[JsonProperty(PropertyName = "Is it worth enabling the Gather Rewards?")]
            public bool Gather_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Kill Rewards?")]
            public bool Kill_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Loot Open Rewards?")]
            public bool LootOpen_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Pickup Rewards?")]
            public bool Pickup_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Planting Rewards?")]
            public bool Planting_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Fishing Rewards?")]
            public bool Fishing_Enabled = true;
			
			[JsonProperty(PropertyName = "List of multipliers for rewards, for each group permission")]
            public Dictionary<string, float> PermissionsList = new Dictionary<string, float>();
			
			[JsonProperty(PropertyName = "Is it worth using the AdvancedStatus plugin?")]
            public bool AdvancedStatus_Enabled = true;
			
			[JsonProperty(PropertyName = "List of status bar settings for each plugin")]
            public List<BarSettings> BarsList = new List<BarSettings>();
			
			public Oxide.Core.VersionNumber Version;
		}
		
		protected override void LoadConfig()
        {
            base.LoadConfig();
            try { _config = Config.ReadObject<Configuration>(); }
            catch (Exception ex) { PrintError($"{ex.Message}\n\n[{Title}] Your configuration file contains an error."); }
            if (_config == null || _config.Version == new VersionNumber())
            {
                PrintWarning("The configuration file is not found or contains errors. Creating a new one...");
                LoadDefaultConfig();
            }
            else if (_config.Version < Version)
            {
                PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
                _config.Version = Version;
                PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
            }
			
			if (!_config.BarsList.Any(b => b.BarID == BankSystemBar))
				_config.BarsList.Add(_defaultBankSystem);
			if (!_config.BarsList.Any(b => b.BarID == ServerRewardsBar))
				_config.BarsList.Add(_defaultServerRewards);
			if (!_config.BarsList.Any(b => b.BarID == EconomicsBar))
				_config.BarsList.Add(_defaultEconomics);
			SaveConfig();
        }
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { EcoPlugins = _defaultEco, PermissionsList = _defaultPermissions, BarsList = new List<BarSettings>() { _defaultBankSystem, _defaultServerRewards, _defaultEconomics }, Version = Version };
		#endregion
		
		#region ~Language~
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgBankSystem"] = "Bonus",
				["MsgServerRewards"] = "Bonus",
				["MsgEconomics"] = "Bonus"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgBankSystem"] = "Бонус",
				["MsgServerRewards"] = "Бонус",
				["MsgEconomics"] = "Бонус"
            }, this, "ru");
		}
        #endregion

        #region ~Methods~
		private void LoadImages()
        {
            Dictionary<string, string> imgList = new Dictionary<string, string>();
            foreach (var bar in _config.BarsList)
            {
                if (bar.Image_Url.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }))
                    imgList.Add(bar.BarID, bar.Image_Url);
            }
            ImageLibrary?.Call("ImportImageList", Name, imgList, 0UL, true);
        }
		
		private void CheckPlugins(Plugin plugin = null, bool isLoad = false)
		{
			int before = _ecoPlugins.Count;
			if (plugin == null)
			{
				before = 0;
				_ecoPlugins.Clear();
				if (BankSystem != null && _config.EcoPlugins.Contains(BankSystem.Name) && BankSystem.IsLoaded)
					_ecoPlugins.Add(new EcoPlugin(BankSystem, BankSystemBar));
				if (ServerRewards != null && _config.EcoPlugins.Contains(ServerRewards.Name) && ServerRewards.IsLoaded)
					_ecoPlugins.Add(new EcoPlugin(ServerRewards, ServerRewardsBar, "AddPoints", "TakePoints"));
				if (Economics != null && _config.EcoPlugins.Contains(Economics.Name) && Economics.IsLoaded)
					_ecoPlugins.Add(new EcoPlugin(Economics, EconomicsBar));
			}
			else if (isLoad && _config.EcoPlugins.Contains(plugin.Name))
			{
				switch (plugin.Name)
                {
                    case "BankSystem":
                        _ecoPlugins.Add(new EcoPlugin(BankSystem, BankSystemBar));
                        break;
                    case "ServerRewards":
                        _ecoPlugins.Add(new EcoPlugin(ServerRewards, ServerRewardsBar, "AddPoints", "TakePoints"));
                        break;
                    case "Economics":
                        _ecoPlugins.Add(new EcoPlugin(Economics, EconomicsBar));
                        break;
                    default:
                        break;
                }
			}
			else
				_ecoPlugins.RemoveWhere(e => e.Plugin == plugin);

			if (_ecoPlugins.Any())
			{
				if (before == 0)
                {
					foreach (var hook in _hooks)
						Subscribe(hook);
				}
			}
			else
			{
				foreach (var hook in _hooks)
					Unsubscribe(hook);
				_patrolLastHit.Clear();
			}
		}
		
		private void InitReward(BasePlayer player, string shortName, string type)
		{
			PluginsRewards rewData = null;
			switch (type)
			{
				case "gather":
					_gatherConfig.TryGetValue(shortName, out rewData);
					break;
				case "kill":
					if (!_killConfig.TryGetValue(shortName, out rewData))
					{
						foreach (var kvp in _killConfig)
						{
							if (shortName.StartsWith(kvp.Key))
							{
								rewData = kvp.Value;
								break;
							}
						}
					}
					break;
				case "open":
					_openConfig.TryGetValue(shortName, out rewData);
					break;
				case "pickup":
					_pickupConfig.TryGetValue(shortName, out rewData);
					break;
				case "plant":
					_plantingConfig.TryGetValue(shortName, out rewData);
					break;
				case "fishing":
                    _fishingConfig.TryGetValue(shortName, out rewData);
                    break;
				default:
					break;
			}
			if (rewData == null) return;
			foreach (var plugin in _ecoPlugins)
            {
				if (plugin.Bar.BarID == EconomicsBar)
					GiveReward(plugin, player, rewData.FloatReward);
				else
					GiveReward(plugin, player, rewData.IntReward);
			}
		}
		
		private void GiveReward(EcoPlugin plugin, BasePlayer player, int reward)
        {
            int amount = (int)(reward * GetRewardMultiplier(player.UserIDString));
			if (amount == 0) return;
			if (amount > 0)
				plugin.Plugin.Call(plugin.Deposit, player.userID.Get(), amount);
			else
				plugin.Plugin.Call(plugin.Withdraw, player.userID.Get(), Math.Abs(amount));
			ShowBar(player, amount, plugin.Bar);
		}
		
		private void GiveReward(EcoPlugin plugin, BasePlayer player, float reward)
		{
			float amount = reward * GetRewardMultiplier(player.UserIDString);
			if (amount == 0f) return;
			if (amount > 0f)
				plugin.Plugin.Call(plugin.Deposit, player.userID.Get(), (double)amount);
			else
				plugin.Plugin.Call(plugin.Withdraw, player.userID.Get(), (double)Abs(amount));
			ShowBar(player, amount, plugin.Bar);
		}
		
		private void ShowBar(BasePlayer player, float amount, BarSettings bar)
        {
			if (!_config.AdvancedStatus_Enabled || !_statusIsLoaded)
			{
				player.SendConsoleCommand("note.inv", 963906841, amount > 0f ? 1 : 0, $"<color=#A3FF00>{(amount > 0f ? "+" : "")}{amount} {lang.GetMessage("MsgTextBar", this, player.UserIDString)}</color>");
				return;
			}
			double timestamp = Network.TimeEx.currentTimestamp + 4;
			Dictionary<string, object> parameters = new Dictionary<string, object>
			{
				{ "Id", bar.BarID },
				{ "Plugin", Name },
				{ "BarType", "Timed" },
				{ "Order", bar.Order },
				{ "Height", bar.Height },
				{ "Main_Color", bar.Main_Color },
				{ "Main_Transparency", bar.Main_Transparency },
				{ "Main_Material", bar.Main_Material },
				{ "Image", _imgLibIsLoaded && bar.Image_Url.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }) ? bar.BarID : bar.Image_Url },
				{ "Image_Local", bar.Image_Local },
				{ "Image_Sprite", bar.Image_Sprite },
				{ "Image_IsRawImage", bar.Image_IsRawImage },
				{ "Image_Color", bar.Image_Color },
				{ "Image_Transparency", bar.Image_Transparency },
				{ "Text", lang.GetMessage(bar.Text_Key, this, player.UserIDString) },
				{ "Text_Size", bar.Text_Size },
				{ "Text_Color", bar.Text_Color },
				{ "Text_Font", bar.Text_Font },
				{ "SubText", $"{(amount > 0 ? "+" : "")}{amount}" },
				{ "SubText_Size", bar.SubText_Size },
				{ "SubText_Color", bar.SubText_Color },
				{ "SubText_Font", bar.SubText_Font },
				{ "TimeStamp", timestamp }
			};
			AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
		}
		
		private float GetRewardMultiplier(string userID)
		{
			float result = 1f;
			foreach (var kvp in _config.PermissionsList)
			{
				if (kvp.Value > result && permission.UserHasPermission(userID, kvp.Key))
					result = kvp.Value;
			}
			return result;
		}
		
		private static float Abs(float value) => value < 0 ? -value : value;
		
		private static Dictionary<string, PluginsRewards> CopyRewardDictionary(Dictionary<string, PluginsRewards> original)
        {
			Dictionary<string, PluginsRewards> clone = new Dictionary<string, PluginsRewards>(original.Count);
			foreach (var kvp in original)
				clone.Add(kvp.Key, new PluginsRewards(kvp.Value));
			return clone;
        }
        #endregion

        #region ~Oxide Hooks~
		void OnAdvancedStatusLoaded()
		{
			_statusIsLoaded = true;
			AdvancedStatus?.Call("LoadImages", _config.BarsList.Select(b => b.Image_Local).ToList());
		}
		
		void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == ImageLibrary)
			{
				_imgLibIsLoaded = true;
				LoadImages();
			}
			else if (plugin == BankSystem || plugin == ServerRewards || plugin == Economics)
				CheckPlugins(plugin, true);
		}
		
		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
				_imgLibIsLoaded = false;
			else if (plugin.Name == "BankSystem" || plugin.Name == "ServerRewards" || plugin.Name == "Economics")
				CheckPlugins(plugin);
		}
		
		void Init()
        {
			Unsubscribe(nameof(OnDispenserBonus));
			Unsubscribe(nameof(OnEntityDeath));
			Unsubscribe(nameof(OnPatrolHelicopterTakeDamage));
			Unsubscribe(nameof(CanLootEntity));
			Unsubscribe(nameof(OnCollectiblePickup));
			Unsubscribe(nameof(OnGrowableGathered));
			Unsubscribe(nameof(OnItemPickup));
			Unsubscribe(nameof(OnEntitySpawned));
			Unsubscribe(nameof(OnFishCatch));
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
			LoadGatherConfig();
			LoadKillConfig();
			LoadOpenConfig();
			LoadPickupConfig();
			LoadPlantingConfig();
			LoadFishingConfig();
		}
		
		void OnServerInitialized()
        {
			_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			if (_imgLibIsLoaded)
				LoadImages();
			if (_config.Gather_Enabled)
				_hooks.Add("OnDispenserBonus");
			if (_config.Kill_Enabled)
			{
				_hooks.Add("OnEntityDeath");
				_hooks.Add("OnPatrolHelicopterTakeDamage");
			}
			if (_config.LootOpen_Enabled)
				_hooks.Add("CanLootEntity");
			if (_config.Pickup_Enabled)
            {
				_hooks.Add("OnCollectiblePickup");
				_hooks.Add("OnGrowableGathered");
				_hooks.Add("OnItemPickup");
            }
			if (_config.Planting_Enabled)
				_hooks.Add("OnEntitySpawned");
			if (_config.Fishing_Enabled)
				_hooks.Add("OnFishCatch");
			CheckPlugins();
			if (AdvancedStatus != null && AdvancedStatus?.Call("IsReady") != null)
				OnAdvancedStatusLoaded();
			Subscribe(nameof(OnAdvancedStatusLoaded));
		}
		
		void Unload()
		{
            _gatherConfig = null;
            _killConfig = null;
            _openConfig = null;
            _pickupConfig = null;
            _plantingConfig = null;
            _config = null;
		}
        #endregion

        #region ~Activity Rewards~
		void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) => InitReward(player, item.info.shortname, "gather");
		
		void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (info != null && info.InitiatorPlayer is BasePlayer player && player.userID.IsSteamId())
				InitReward(player, entity.ShortPrefabName, "kill");
		}
		
		void OnEntityDeath(BasePlayer player, HitInfo info)
        {
			if (info != null && info.InitiatorPlayer is BasePlayer attacker && attacker.userID.IsSteamId())
				InitReward(attacker, info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Suicide ? "suicide" : player.ShortPrefabName, "kill");
		}
		
		void OnEntityDeath(PatrolHelicopter patrol)
		{
			if (!_patrolLastHit.ContainsKey(patrol.net.ID.Value)) return;
			BasePlayer player = BasePlayer.FindByID(_patrolLastHit[patrol.net.ID.Value]);
			_patrolLastHit.Remove(patrol.net.ID.Value);
			if (player != null)
				InitReward(player, patrol.ShortPrefabName, "kill");
		}
		
		void OnPatrolHelicopterTakeDamage(PatrolHelicopter patrol)
		{
			if (patrol.lastAttacker is BasePlayer attacker && attacker != null && !attacker.IsNpc)
				_patrolLastHit[patrol.net.ID.Value] = attacker.userID;
		}
		
		void CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container.LastLootedBy == 0)
            {
                NextTick(() =>
                {
                    if (container.LastLootedBy == player.userID)
                        InitReward(player, container.ShortPrefabName, "open");
                });
            }
        }
		
		void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            NextTick(() =>
            {
                if (collectible.IsDestroyed)
                    InitReward(player, collectible.itemName.english, "pickup");
            });
        }
		
		void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player) => InitReward(player, item.info.displayName.english, "pickup");
		
		object OnItemPickup(Item item, BasePlayer player)
        {
            if (_pickupConfig.ContainsKey(item.info.displayName.english) && item.GetWorldEntity() is DroppedItem dropped && dropped.DroppedBy == 0)
            {
                NextTick(() =>
                {
                    if (dropped.item == null)
                        InitReward(player, item.info.displayName.english, "pickup");
                });
            }
            return null;
        }
		
		void OnEntitySpawned(GrowableEntity growable)
        {
            if (BasePlayer.FindByID(growable.OwnerID) is BasePlayer player)
                InitReward(player, growable.ShortPrefabName, "plant");
        }
		
		void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
			NextTick(() =>
            {
				if (item != null)
                    InitReward(player, item.info.shortname, "fishing");
            });
		}
		#endregion

        #region ~Gather Rewards Config~
        private static Dictionary<string, PluginsRewards> _gatherConfig;
		private const string _gatherPath = "ActivityRewards/GatherRewards";
		
		private void LoadGatherConfig()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_gatherPath))
            {
                try { _gatherConfig = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PluginsRewards>>(_gatherPath); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
			if (_gatherConfig == null)
				_gatherConfig = CopyRewardDictionary(_defaultGatherReward);
			Interface.Oxide.DataFileSystem.WriteObject(_gatherPath, _gatherConfig);
		}
        #endregion

        #region ~Kill Rewards Config~
        private static Dictionary<string, PluginsRewards> _killConfig;
        private const string _killPath = "ActivityRewards/KillRewards";

        private void LoadKillConfig()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_killPath))
            {
                try { _killConfig = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PluginsRewards>>(_killPath); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
            if (_killConfig == null)
                _killConfig = CopyRewardDictionary(_defaultKillReward);
            Interface.Oxide.DataFileSystem.WriteObject(_killPath, _killConfig);
        }
        #endregion
		
		#region ~First Loot Open Rewards Config~
        private static Dictionary<string, PluginsRewards> _openConfig;
        private const string _openPath = "ActivityRewards/FirstLootOpenRewards";

        private void LoadOpenConfig()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_openPath))
            {
                try { _openConfig = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PluginsRewards>>(_openPath); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
            if (_openConfig == null)
                _openConfig = CopyRewardDictionary(_defaultOpenningReward);
            Interface.Oxide.DataFileSystem.WriteObject(_openPath, _openConfig);
        }
        #endregion

        #region ~Pickup Rewards Config~
        private static Dictionary<string, PluginsRewards> _pickupConfig;
        private const string _pickupPath = "ActivityRewards/PickupRewards";

        private void LoadPickupConfig()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_pickupPath))
            {
                try { _pickupConfig = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PluginsRewards>>(_pickupPath); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
            if (_pickupConfig == null)
                _pickupConfig = CopyRewardDictionary(_defaultPickupReward);
            Interface.Oxide.DataFileSystem.WriteObject(_pickupPath, _pickupConfig);
        }
        #endregion

        #region ~Planting Rewards Config~
        private static Dictionary<string, PluginsRewards> _plantingConfig;
        private const string _plantingPath = "ActivityRewards/PlantingRewards";

        private void LoadPlantingConfig()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_plantingPath))
            {
                try { _plantingConfig = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PluginsRewards>>(_plantingPath); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
            if (_plantingConfig == null)
                _plantingConfig = CopyRewardDictionary(_defaultPlantReward);
            Interface.Oxide.DataFileSystem.WriteObject(_plantingPath, _plantingConfig);
        }
        #endregion
		
		#region ~Fishing Rewards Config~
        private static Dictionary<string, PluginsRewards> _fishingConfig;
        private const string _fishingPath = "ActivityRewards/FishingRewards";

        private void LoadFishingConfig()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_fishingPath))
            {
                try { _fishingConfig = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PluginsRewards>>(_fishingPath); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
            if (_fishingConfig == null)
                _fishingConfig = CopyRewardDictionary(_defaultFishingReward);
            Interface.Oxide.DataFileSystem.WriteObject(_fishingPath, _fishingConfig);
        }
        #endregion
		
		#region ~Classes~
		public class PluginsRewards
        {
            public int IntReward { get; set; }
            public float FloatReward { get; set; }

            public PluginsRewards() { }
            public PluginsRewards(int rInt, float rFloat)
            {
                IntReward = rInt;
                FloatReward = rFloat;
            }
            public PluginsRewards(PluginsRewards other)
            {
                IntReward = other.IntReward;
                FloatReward = other.FloatReward;
            }
        }

        public class EcoPlugin
        {
            public Plugin Plugin { get; set; }
            public string Name { get; set; }
            public string Deposit { get; set; }
            public string Withdraw { get; set; }
            public BarSettings Bar { get; set; }

            public EcoPlugin(Plugin plugin, string id, string deposit = "Deposit", string withdraw = "Withdraw")
            {
                Plugin = plugin;
                Name = plugin.Name;
                Deposit = deposit;
                Withdraw = withdraw;
                foreach (var bar in _config.BarsList)
                {
                    if (bar.BarID == id)
                    {
                        Bar = bar;
                        break;
                    }
                }
            }
        }

        public class BarSettings
        {
            public string BarID { get; set; }
            public int Order { get; set; }
            public int Height { get; set; }
            public string Main_Color { get; set; }
            public float Main_Transparency { get; set; }
            public string Main_Material { get; set; }
            public string Image_Url { get; set; }

            [JsonProperty(PropertyName = "Image_Local(Leave empty to use Image_Url)")]
            public string Image_Local { get; set; }

            [JsonProperty(PropertyName = "Image_Sprite(Leave empty to use Image_Local or Image_Url)")]
            public string Image_Sprite { get; set; }

            public bool Image_IsRawImage { get; set; }
            public string Image_Color { get; set; }
            public float Image_Transparency { get; set; }
            public string Text_Key { get; set; }
            public int Text_Size { get; set; }
            public string Text_Color { get; set; }
            public string Text_Font { get; set; }
            public int SubText_Size { get; set; }
            public string SubText_Color { get; set; }
            public string SubText_Font { get; set; }
        }
		#endregion
    }
}