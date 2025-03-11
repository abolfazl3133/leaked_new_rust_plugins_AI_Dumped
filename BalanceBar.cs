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
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://lone.design/vendor/iiiaka/
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/balance-bar
*  Codefling license: https://codefling.com/plugins/balance-bar?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/balance-bar/
*
*  Copyright © 2023-2024 IIIaKa
*/

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Balance Bar", "IIIaKa", "0.1.6")]
	[Description("The plugin displays the player's balance in the status bar. Depends on BankSystem/ServerRewards/Economics and AdvancedStatus plugins.")]
	class BalanceBar : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, AdvancedStatus, BankSystem, ServerRewards, Economics;

		#region ~Variables~
		private static BalanceBar Instance { get; set; }
		private bool _imgLibIsLoaded = false, _statusIsLoaded = false;
		private const string BankSystemBar = "BalanceBar_BankSystem", ServerRewardsBar = "BalanceBar_ServerRewards", EconomicsBar = "BalanceBar_Economics", StatusCreateBar = "CreateBar", StatusUpdateContent = "UpdateContent", StatusDeleteBar = "DeleteBar", StatusInBuilding = "InBuildingPrivilege";
		private readonly string[] HttpScheme = new string[2] { "http://", "https://" };
		private readonly HashSet<string> _defaultPlugins = new HashSet<string>() { "BankSystem", "ServerRewards", "Economics" };
		private HashSet<EcoPlugin> _ecoPlugins = new HashSet<EcoPlugin>();
		#endregion

        #region ~Configuration~
        private static Configuration _config;

		private class Configuration
        {
			[JsonProperty(PropertyName = "Display the balance only when players are in the safe zone or have building privilege?")]
            public bool Status_InSafeZone = true;
			
			[JsonProperty(PropertyName = "Value after which text will be displayed instead of balance")]
			public double OverLimit = 1000000000d;
			
			[JsonProperty(PropertyName = "List of plugins for displaying the balance bar. Leave null or empty to use the default list")]
			public HashSet<string> EcoPlugins = new HashSet<string>();
			
			[JsonProperty(PropertyName = "List of status bar settings for each plugin. Leave null or empty to recreate the list")]
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
			
			_config.OverLimit = Math.Max(_config.OverLimit, 1);
			if (_config.EcoPlugins == null || !_config.EcoPlugins.Any())
				_config.EcoPlugins = new HashSet<string>(_defaultPlugins);
			
			if (_config.BarsList == null)
				_config.BarsList = new List<BarSettings>();
			if (!_config.BarsList.Any(b => b.BarID == BankSystemBar))
			{
				_config.BarsList.Add(new BarSettings()
				{
					BarID = BankSystemBar,
					Image_Local = "BalanceBar_BankSystem",
					Text_Key = "MsgBankSystem",
					SubText_OverLimit = "MsgBankSystemOverLimit"
                });
			}
			if (!_config.BarsList.Any(b => b.BarID == ServerRewardsBar))
			{
				_config.BarsList.Add(new BarSettings()
				{
					BarID = ServerRewardsBar,
					Image_Local = "BalanceBar_ServerRewards",
					Text_Key = "MsgServerRewards",
					SubText_Format = "{0}RP",
					SubText_OverLimit = "MsgServerRewardsOverLimit"
                });
			}
			if (!_config.BarsList.Any(b => b.BarID == EconomicsBar))
			{
				_config.BarsList.Add(new BarSettings()
				{
					BarID = EconomicsBar,
					Image_Local = "BalanceBar_Economics",
					Text_Key = "MsgEconomics",
					SubText_OverLimit = "MsgEconomicsOverLimit"
                });
			}
			
			SaveConfig();
		}
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion

		#region ~Language~
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
            {
				["MsgBankSystem"] = "Balance",
				["MsgBankSystemOverLimit"] = "> $1kkk",
				["MsgServerRewards"] = "Points",
				["MsgServerRewardsOverLimit"] = "> 1kkk RP",
				["MsgEconomics"] = "Balance",
				["MsgEconomicsOverLimit"] = "> $1kkk"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
            {
				["MsgBankSystem"] = "Баланс",
				["MsgBankSystemOverLimit"] = "> $1 млрд",
				["MsgServerRewards"] = "Очки",
				["MsgServerRewardsOverLimit"] = "> 1 млрд RP",
				["MsgEconomics"] = "Баланс",
				["MsgEconomicsOverLimit"] = "> $1 млрд"
			}, this, "ru");
		}
        #endregion

        #region ~Methods~
		private void LoadImages()
        {
			var imgList = new Dictionary<string, string>();
            foreach (var bar in _config.BarsList)
            {
				if (string.IsNullOrWhiteSpace(bar.Image_Sprite) && string.IsNullOrWhiteSpace(bar.Image_Local) && bar.Image_Url.StartsWithAny(HttpScheme))
                    imgList.Add(bar.BarID, bar.Image_Url);
            }
			if (imgList.Any())
				ImageLibrary?.Call("ImportImageList", Name, imgList, 0uL, true);
		}
		
		private void SendBar(BasePlayer player, EcoPlugin ecoPlugin)
        {
			ulong userID = player.userID.Get();
			var balance = Convert.ToDouble(ecoPlugin.Plugin.Call(ecoPlugin.GetBalance, userID));
			var parameters = new Dictionary<int, object>(ecoPlugin.Parameters)
			{
				{ 15, lang.GetMessage(ecoPlugin.Bar.Text_Key, this, player.UserIDString) },
				{ 22, _config.OverLimit > balance ? string.Format(ecoPlugin.Bar.SubText_Format, balance) : lang.GetMessage(ecoPlugin.Bar.SubText_OverLimit, this, player.UserIDString) }
			};
			AdvancedStatus?.Call(StatusCreateBar, userID, parameters);
		}
		
		private void UpdateText(ulong userID, EcoPlugin ecoPlugin, object amount = null)
        {
			var balance = Convert.ToDouble(amount != null ? amount : ecoPlugin.Plugin.Call(ecoPlugin.GetBalance, userID));
			string userIDString = userID.ToString();
			var parameters = new Dictionary<int, object>
			{
				{ 0, ecoPlugin.Bar.BarID },
				{ 1, Name },
				{ 15, lang.GetMessage(ecoPlugin.Bar.Text_Key, this, userIDString) },
				{ 22, _config.OverLimit > balance ? string.Format(ecoPlugin.Bar.SubText_Format, balance) : lang.GetMessage(ecoPlugin.Bar.SubText_OverLimit, this, userIDString) }
			};
			AdvancedStatus?.Call(StatusUpdateContent, userID, parameters);
		}
		
		private bool CanDisplay(BasePlayer player)
        {
			if (_config.Status_InSafeZone && !player.InSafeZone() && !InBuildingPrivilege(player.userID))
				return false;
            return true;
        }
		
		private void SubscribeToHooks(bool isSub = true)
		{
			if (isSub)
			{
				Subscribe(nameof(OnPlayerConnected));
				Subscribe(nameof(OnPlayerLanguageChanged));
				if (_config.Status_InSafeZone)
				{
					Subscribe(nameof(OnEntityEnter));
					Subscribe(nameof(OnEntityLeave));
					Subscribe(nameof(OnPlayerGainedBuildingPrivilege));
					Subscribe(nameof(OnPlayerLostBuildingPrivilege));
				}
			}
			else
			{
				Unsubscribe(nameof(OnPlayerConnected));
				Unsubscribe(nameof(OnPlayerLanguageChanged));
				Unsubscribe(nameof(OnEntityEnter));
				Unsubscribe(nameof(OnEntityLeave));
				Unsubscribe(nameof(OnPlayerGainedBuildingPrivilege));
				Unsubscribe(nameof(OnPlayerLostBuildingPrivilege));
				Unsubscribe(nameof(OnBalanceChanged));
				Unsubscribe(nameof(OnPointsUpdated));
				Unsubscribe(nameof(OnEconomicsBalanceUpdated));
			}
		}
		
		private bool TryGetEco(string name, out EcoPlugin result)
        {
			result = null;
			foreach (var eco in _ecoPlugins)
            {
				if (eco.Name == name)
                {
					result = eco;
					break;
				}
			}
			return result != null;
		}
		
		private bool TryGetEco(ulong userID, string barID, out EcoPlugin result)
        {
            result = null;
			if (_config.Status_InSafeZone)
			{
				var player = BasePlayer.FindByID(userID);
				if (player == null || (!player.InSafeZone() && !InBuildingPrivilege(userID)))
					return false;
			}
            foreach (var eco in _ecoPlugins)
            {
                if (eco.Bar.BarID == barID)
                {
                    result = eco;
                    break;
                }
            }
            return result != null;
        }
		
		private bool InBuildingPrivilege(ulong userID) => (bool)(AdvancedStatus?.Call(StatusInBuilding, userID) ?? false);
		private void DestroyBar(ulong userID, string id) => AdvancedStatus?.Call(StatusDeleteBar, userID, id, Name);
		#endregion

        #region ~Oxide Hooks~
        void OnPlayerConnected(BasePlayer player)
        {
			if (!CanDisplay(player)) return;
			foreach (var ecoPlugin in _ecoPlugins)
				SendBar(player, ecoPlugin);
		}
		
		void OnPlayerLanguageChanged(BasePlayer player, string key)
        {
			if (!CanDisplay(player)) return;
			foreach (var ecoPlugin in _ecoPlugins)
				UpdateText(player.userID, ecoPlugin);
		}
		
		void OnEntityEnter(TriggerSafeZone trigger, BasePlayer player)
        {
			if (player.userID.IsSteamId())
            {
				foreach (var ecoPlugin in _ecoPlugins)
					SendBar(player, ecoPlugin);
			}
		}
		
		void OnEntityLeave(TriggerSafeZone trigger, BasePlayer player)
		{
			if (player.userID.IsSteamId() && !CanDisplay(player))
            {
				foreach (var ecoPlugin in _ecoPlugins)
					DestroyBar(player.userID, ecoPlugin.Bar.BarID);
			}
		}
		
		void OnPlayerGainedBuildingPrivilege(BasePlayer player)
        {
			foreach (var ecoPlugin in _ecoPlugins)
				SendBar(player, ecoPlugin);
		}
		
		void OnPlayerLostBuildingPrivilege(BasePlayer player)
        {
			if (player.InSafeZone()) return;
			foreach (var ecoPlugin in _ecoPlugins)
				DestroyBar(player.userID, ecoPlugin.Bar.BarID);
		}
		
		void OnBalanceChanged(ulong userID, int amount)
        {
			if (TryGetEco(userID, BankSystemBar, out var ecoPlugin))
                UpdateText(userID, ecoPlugin, amount);
		}
		
		void OnPointsUpdated(ulong userID, int amount)
		{
			if (TryGetEco(userID, ServerRewardsBar, out var ecoPlugin))
                UpdateText(userID, ecoPlugin, amount);
		}
		
		void OnEconomicsBalanceUpdated(string playerId, double amount)
		{
			if (ulong.TryParse(playerId, out var userID) && TryGetEco(userID, EconomicsBar, out var ecoPlugin))
                UpdateText(userID, ecoPlugin, amount);
		}
		
		void OnAdvancedStatusLoaded()
        {
			_statusIsLoaded = true;
			var imgList = new List<string>();
			BarSettings bar;
			for (int i = 0; i < _config.BarsList.Count; i++)
			{
				bar = _config.BarsList[i];
				if (!string.IsNullOrWhiteSpace(bar.Image_Local))
					imgList.Add(bar.Image_Local);
			}
			if (imgList.Any())
				AdvancedStatus?.Call("LoadImages", imgList);
			if (!_ecoPlugins.Any()) return;
			foreach (var ecoPlugin in _ecoPlugins)
				ecoPlugin.Load();
			foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID.IsSteamId() && CanDisplay(player))
                {
                    foreach (var ecoPlugin in _ecoPlugins)
                        SendBar(player, ecoPlugin);
                }
            }
			SubscribeToHooks();
		}
		
		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin == ImageLibrary)
			{
				_imgLibIsLoaded = true;
				LoadImages();
				foreach (var ecoPlugin in _ecoPlugins)
					ecoPlugin.SelectBarImage();
			}
			else if (plugin == BankSystem || plugin == ServerRewards || plugin == Economics)
			{
				if (!_config.EcoPlugins.Contains(plugin.Name)) return;
				int before = _ecoPlugins.Count;
				switch (plugin.Name)
				{
					case "BankSystem":
						_ecoPlugins.Add(new EcoPlugin(BankSystem, BankSystemBar, "Balance", nameof(OnBalanceChanged)));
						break;
					case "ServerRewards":
						_ecoPlugins.Add(new EcoPlugin(ServerRewards, ServerRewardsBar, "CheckPoints", nameof(OnPointsUpdated)));
						break;
					case "Economics":
						_ecoPlugins.Add(new EcoPlugin(Economics, EconomicsBar, "Balance", nameof(OnEconomicsBalanceUpdated)));
						break;
					default:
						break;
				}
				
				if (_statusIsLoaded && TryGetEco(plugin.Name, out var ecoPlugin))
                {
					if (before == 0)
						SubscribeToHooks();
					ecoPlugin.Load();
					foreach (var player in BasePlayer.activePlayerList)
                    {
						if (player.userID.IsSteamId() && CanDisplay(player))
							SendBar(player, ecoPlugin);
					}
				}
			}
		}
		
		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
			{
				_imgLibIsLoaded = false;
				foreach (var ecoPlugin in _ecoPlugins)
					ecoPlugin.SelectBarImage();
			}
			else if (plugin.Name == "AdvancedStatus")
			{
				_statusIsLoaded = false;
				SubscribeToHooks(false);
			}
			else if (plugin.Name == "BankSystem" || plugin.Name == "ServerRewards" || plugin.Name == "Economics")
			{
				if (!_config.EcoPlugins.Contains(plugin.Name)) return;
				if (TryGetEco(plugin.Name, out var ecoPlugin))
                {
					ecoPlugin.Unload();
					_ecoPlugins.Remove(ecoPlugin);
				}
				if (!_ecoPlugins.Any())
					SubscribeToHooks(false);
			}
		}
		
		void Init()
        {
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
			SubscribeToHooks(false);
			Instance = this;
		}
		
		void OnServerInitialized(bool initial)
        {
			_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			if (_imgLibIsLoaded)
				LoadImages();
			foreach (var plugin in _config.EcoPlugins)
            {
				switch (plugin)
                {
					case "BankSystem":
						if (BankSystem != null && BankSystem.IsLoaded)
							_ecoPlugins.Add(new EcoPlugin(BankSystem, BankSystemBar, "Balance", nameof(OnBalanceChanged)));
						break;
					case "ServerRewards":
						if (ServerRewards != null && ServerRewards.IsLoaded)
							_ecoPlugins.Add(new EcoPlugin(ServerRewards, ServerRewardsBar, "CheckPoints", nameof(OnPointsUpdated)));
						break;
					case "Economics":
						if (Economics != null && Economics.IsLoaded)
							_ecoPlugins.Add(new EcoPlugin(Economics, EconomicsBar, "Balance", nameof(OnEconomicsBalanceUpdated)));
						break;
					default:
						break;
				}
			}
			if (AdvancedStatus == null || AdvancedStatus?.Call("IsReady") == null)
            {
                if (initial && AdvancedStatus != null)
                    PrintWarning("AdvancedStatus plugin found, but not ready yet. Waiting for it to load...");
                else
                    PrintWarning("AdvancedStatus plugin not found! To function, it is necessary to install it!\n* https://codefling.com/plugins/advanced-status\n* https://lone.design/product/advanced-status/");
            }
            else
                OnAdvancedStatusLoaded();
            Subscribe(nameof(OnAdvancedStatusLoaded));
		}
		
		void Unload()
        {
			Instance = null;
			_config = null;
		}
		#endregion

		#region ~Classes~
		public class EcoPlugin
		{
			public Plugin Plugin { get; set; }
			public string Name { get; set; }
			public string GetBalance { get; private set; }
			private string UpdateHook;
			public BarSettings Bar { get; set; }
			public Dictionary<int, object> Parameters { get; private set; }
			
			public EcoPlugin(Plugin plugin, string id, string balance, string hook)
			{
				Plugin = plugin;
				Name = plugin.Name;
				GetBalance = balance;
				UpdateHook = hook;
				foreach (var bar in _config.BarsList)
                {
                    if (bar.BarID == id)
                    {
                        Bar = bar;
                        break;
                    }
                }
			}

			public void Load()
			{
				Parameters = new Dictionary<int, object>
                {
					{ 0, Bar.BarID },
					{ 1, Instance.Name },
					{ 4, Bar.Order },
					{ 5, Bar.Height },
					{ 6, Bar.Main_Color },
					{ -6, Bar.Main_Transparency },
					{ 7, Bar.Main_Material },
					{ 11, Bar.Image_IsRawImage },
					{ 12, Bar.Image_Color },
					{ -12, Bar.Image_Transparency },
					{ 16, Bar.Text_Size },
					{ 17, Bar.Text_Color },
					{ 18, Bar.Text_Font },
					{ 23, Bar.SubText_Size },
					{ 24, Bar.SubText_Color },
					{ 25, Bar.SubText_Font }
				};
				if (Bar.Image_Outline_Enabled)
                {
                    Parameters.Add(13, Bar.Image_Outline_Color);
                    Parameters.Add(-13, Bar.Image_Outline_Transparency);
                    Parameters.Add(14, Bar.Image_Outline_Distance);
                }
                if (Bar.Text_Outline_Enabled)
                {
                    Parameters.Add(20, Bar.Text_Outline_Color);
                    Parameters.Add(-20, Bar.Text_Outline_Transparency);
                    Parameters.Add(21, Bar.Text_Outline_Distance);
                }
                if (Bar.SubText_Outline_Enabled)
                {
                    Parameters.Add(26, Bar.SubText_Outline_Color);
                    Parameters.Add(-26, Bar.SubText_Outline_Transparency);
                    Parameters.Add(27, Bar.SubText_Outline_Distance);
                }
				SelectBarImage();
				
				Instance.Subscribe(UpdateHook);
			}
			
			public void SelectBarImage()
            {
				Parameters.Remove(10);
				Parameters.Remove(9);
				Parameters.Remove(8);
				if (!string.IsNullOrWhiteSpace(Bar.Image_Sprite))
                    Parameters.Add(10, Bar.Image_Sprite);
                else if (!string.IsNullOrWhiteSpace(Bar.Image_Local))
                    Parameters.Add(9, Bar.Image_Local);
                else
                    Parameters.Add(8, Instance._imgLibIsLoaded && Bar.Image_Url.StartsWithAny(Instance.HttpScheme) ? Bar.BarID : Bar.Image_Url);
            }
			
			public void Unload()
            {
				Instance.Unsubscribe(UpdateHook);
				Parameters = null;
				if (Instance._statusIsLoaded)
				{
					foreach (var player in BasePlayer.activePlayerList)
						Instance.DestroyBar(player.userID, Bar.BarID);
				}
			}
		}
		
		public class BarSettings
        {
			[JsonProperty(PropertyName = "BarID. Do not touch this parameter")]
			public string BarID { get; set; }
			
			public int Order { get; set; } = 20;
			public int Height { get; set; } = 26;
			
			[JsonProperty(PropertyName = "Main_Color(Hex or RGBA)")]
			public string Main_Color { get; set; } = "#6375B3";
			
			public float Main_Transparency { get; set; } = 0.8f;
			public string Main_Material { get; set; } = string.Empty;
			public string Image_Url { get; set; } = "https://i.imgur.com/jKeUqSD.png";
			
			[JsonProperty(PropertyName = "Image_Local(Leave empty to use Image_Url)")]
			public string Image_Local { get; set; }
			
			[JsonProperty(PropertyName = "Image_Sprite(Leave empty to use Image_Local or Image_Url)")]
			public string Image_Sprite { get; set; } = string.Empty;
			
			public bool Image_IsRawImage { get; set; } = false;
			
			[JsonProperty(PropertyName = "Image_Color(Hex or RGBA)")]
			public string Image_Color { get; set; } = "#A1DBE6";
			
			public float Image_Transparency { get; set; } = 1f;
			
			[JsonProperty(PropertyName = "Is it worth enabling an outline for the image?")]
			public bool Image_Outline_Enabled { get; set; }
			
			[JsonProperty(PropertyName = "Image_Outline_Color(Hex or RGBA)")]
			public string Image_Outline_Color { get; set; } = "0.1 0.3 0.8 0.9";
			
			public float Image_Outline_Transparency { get; set; }
			public string Image_Outline_Distance { get; set; } = "0.75 0.75";
			public string Text_Key { get; set; }
			public int Text_Size { get; set; } = 12;
			
			[JsonProperty(PropertyName = "Text_Color(Hex or RGBA)")]
			public string Text_Color { get; set; } = "#FFFFFF";
			
			[JsonProperty(PropertyName = "Text_Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
			public string Text_Font { get; set; } = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Is it worth enabling an outline for the text?")]
			public bool Text_Outline_Enabled { get; set; }
			
			[JsonProperty(PropertyName = "Text_Outline_Color(Hex or RGBA)")]
			public string Text_Outline_Color { get; set; } = "#000000";
			
			public float Text_Outline_Transparency { get; set; } = 1f;
			public string Text_Outline_Distance { get; set; } = "0.75 0.75";
			public string SubText_Format { get; set; } = "${0}";
			public string SubText_OverLimit { get; set; }
			public int SubText_Size { get; set; } = 12;
			
			[JsonProperty(PropertyName = "SubText_Color(Hex or RGBA)")]
			public string SubText_Color { get; set; } = "#FFFFFF";
			
			public string SubText_Font { get; set; } = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Is it worth enabling an outline for the sub text?")]
			public bool SubText_Outline_Enabled { get; set; }
			
			[JsonProperty(PropertyName = "SubText_Outline_Color(Hex or RGBA)")]
			public string SubText_Outline_Color { get; set; } = "0.5 0.6 0.7 0.5";
			
			public float SubText_Outline_Transparency { get; set; }
			public string SubText_Outline_Distance { get; set; } = "0.75 0.75";
		}
		#endregion
	}
}