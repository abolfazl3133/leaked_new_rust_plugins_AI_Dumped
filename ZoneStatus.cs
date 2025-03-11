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
*  Codefling plugin page: https://codefling.com/plugins/zone-status
*  Codefling license: https://codefling.com/plugins/zone-status?tab=downloads_field_4
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
	[Info("ZoneStatus", "IIIaKa", "0.1.4")]
	[Description("The plugin displays the current zone or monument to the player in the status bar. Depends on ZoneManager, MonumentsWatcher and AdvancedStatus plugins.")]
	class ZoneStatus : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, ZoneManager, AdvancedStatus, MonumentsWatcher;
		
		#region ~Variables~
        private bool _imgLibIsLoaded = false;
		private bool _zoneIsLoaded = false;
		private bool _watcherIsLoaded = false;
		private bool _statusIsLoaded = false;
		private const string ZoneID = "ZoneStatus_ZoneManager", MonumentsID = "ZoneStatus_MonumentsWatcher", StatusCreateBar = "CreateBar", StatusUpdateContent = "UpdateContent", StatusDeleteBar = "DeleteBar", StatusDeleteCategory = "DeleteCategory", HttpScheme = "http://", HttpsScheme = "https://";
		#endregion

        #region ~Configuration~
        private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Is it worth deleting all saved Zone bars upon detecting a wipe?")]
			public bool Wipe_Zones = true;
			
			[JsonProperty(PropertyName = "Is it worth deleting all saved Monument bars upon detecting a wipe?")]
			public bool Wipe_Monuments = true;
			
			[JsonProperty(PropertyName = "The name of the zone which has no name")]
			public string Zone_Noname = "No name zone";
			
			[JsonProperty(PropertyName = "The subtext of the zone with no subtext (leave empty to disable)")]
			public string Zone_SubText = "";
			
			[JsonProperty(PropertyName = "Status. Bar - Default Display")]
			public bool Status_Bar_Display = true;
			
			[JsonProperty(PropertyName = "Status. Bar - Default Height")]
			public int Status_Bar_Height = 26;
			
			[JsonProperty(PropertyName = "Status. Bar - Default Order")]
			public int Status_Bar_Order = 10;

			[JsonProperty(PropertyName = "Status. Background - Default Color")]
			public string Status_Background_Color = "#A064A0";
			
			[JsonProperty(PropertyName = "Status. Background - Default Transparency")]
			public float Status_Background_Transparency = 0.8f;

			[JsonProperty(PropertyName = "Status. Background - Default Material(empty to disable)")]
			public string Status_Background_Material = "";

			[JsonProperty(PropertyName = "Status. Image - Default Url")]
			public string Status_Image_Url = "https://i.imgur.com/mn8reWg.png";
			
			[JsonProperty(PropertyName = "Status. Image - Default Local(Leave empty to use Image_Url)")]
            public string Status_Image_Local = "ZoneStatus_Default";
			
			[JsonProperty(PropertyName = "Status. Image - Default Sprite(Leave empty to use Image_Local or Image_Url)")]
			public string Status_Image_Sprite = "";

			[JsonProperty(PropertyName = "Status. Image - Default Is raw image")]
			public bool Status_Image_IsRawImage = false;

			[JsonProperty(PropertyName = "Status. Image - Default Color")]
			public string Status_Image_Color = "#A064A0";
			
			[JsonProperty(PropertyName = "Status. Image - Default Transparency")]
            public float Status_Image_Transparency = 1f;
			
			[JsonProperty(PropertyName = "Status. Text - Default Size")]
			public int Status_Text_Size = 12;

			[JsonProperty(PropertyName = "Status. Text - Default Color")]
			public string Status_Text_Color = "#FFFFFF";
			
			[JsonProperty(PropertyName = "Status. Text - Default Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
			public string Status_Text_Font = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Status. SubText - Default Size")]
			public int Status_SubText_Size = 12;

			[JsonProperty(PropertyName = "Status. SubText - Default Color")]
			public string Status_SubText_Color = "#FFFFFF";

			[JsonProperty(PropertyName = "Status. SubText - Default Font")]
			public string Status_SubText_Font = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Wipe ID")]
			public string WipeID = "";
			
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
            SaveConfig();
        }
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion
		
		#region ~Oxide Hooks~
		void OnPlayerConnected(BasePlayer player)
		{
			if (_zoneIsLoaded)
            {
				var playerZones = (string[])(ZoneManager?.Call("GetPlayerZoneIDs", player) ?? Array.Empty<string>());
				foreach (var zoneID in playerZones)
                    SendZoneBar(player, zoneID);
			}
			if (_watcherIsLoaded)
			{
				var monuments = (string[])(MonumentsWatcher?.Call("GetPlayerMonuments", player.userID) ?? Array.Empty<string>());
				foreach (var monumentID in monuments)
					SendMonumentBar(player, monumentID);
			}
		}
		
		void OnPlayerLanguageChanged(BasePlayer player, string key)
		{
			if (_zoneIsLoaded) {}
			if (_watcherIsLoaded)
            {
				var monuments = (string[])(MonumentsWatcher?.Call("GetPlayerMonuments", player.userID) ?? Array.Empty<string>());
				foreach (var monumentID in monuments)
					UpdateText(player, monumentID, MonumentsID);
			}
		}
		
		void OnEnterZone(string zoneID, BasePlayer player) => SendZoneBar(player, zoneID);
		
		void OnExitZone(string zoneID, BasePlayer player) => DeleteBar(player, zoneID, ZoneID);
		
		void OnPlayerEnteredMonument(string monumentID, BasePlayer player, string type, string oldMonumentID)
		{
			SendMonumentBar(player, monumentID);
			if (!string.IsNullOrWhiteSpace(oldMonumentID))
				DeleteBar(player, oldMonumentID, MonumentsID);
		}
		
		void OnPlayerExitedMonument(string monumentID, BasePlayer player, string type, string reason, string newMonumentID)
        {
			DeleteBar(player, monumentID, MonumentsID);
			if (!string.IsNullOrWhiteSpace(newMonumentID))
                SendMonumentBar(player, newMonumentID);
		}
		
		void OnMonumentsWatcherLoaded()
		{
			if (_statusIsLoaded)
				InitMonuments();
		}
		
		void OnAdvancedStatusLoaded()
        {
			_statusIsLoaded = true;
			InitZones();
			InitMonuments();
		}
		
		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin == ImageLibrary)
				_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			else if (plugin == ZoneManager)
			{
				if (_statusIsLoaded)
					InitZones();
			}
		}

		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
				_imgLibIsLoaded = false;
			else if (plugin.Name == "AdvancedStatus")
			{
				_statusIsLoaded = false;
				Unsubscribe(nameof(OnPlayerConnected));
				Unsubscribe(nameof(OnPlayerLanguageChanged));
				if (_zoneIsLoaded)
					UnloadZones();
				if (_watcherIsLoaded)
					UnloadMonuments();
			}
			else if (plugin.Name == "ZoneManager")
			{
				_zoneIsLoaded = false;
				UnloadZones();
				if (_statusIsLoaded)
					AdvancedStatus?.Call(StatusDeleteCategory, ZoneID, Name);
				if (!_watcherIsLoaded)
				{
					Unsubscribe(nameof(OnPlayerConnected));
					Unsubscribe(nameof(OnPlayerLanguageChanged));
				}
			}
			else if (plugin.Name == "MonumentsWatcher")
			{
				_watcherIsLoaded = false;
				UnloadMonuments();
				if (_statusIsLoaded)
					AdvancedStatus?.Call(StatusDeleteCategory, MonumentsID, Name);
				if (!_zoneIsLoaded)
				{
					Unsubscribe(nameof(OnPlayerConnected));
					Unsubscribe(nameof(OnPlayerLanguageChanged));
				}
			}
		}
		
		void Init()
		{
			Unsubscribe(nameof(OnPlayerConnected));
			Unsubscribe(nameof(OnPlayerLanguageChanged));
			Unsubscribe(nameof(OnEnterZone));
			Unsubscribe(nameof(OnExitZone));
			Unsubscribe(nameof(OnMonumentsWatcherLoaded));
			Unsubscribe(nameof(OnPlayerEnteredMonument));
			Unsubscribe(nameof(OnPlayerExitedMonument));
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
		}
		
		void OnServerInitialized()
		{
			if (string.IsNullOrWhiteSpace(_config.WipeID) || _config.WipeID != SaveRestore.WipeId)
			{
				_config.WipeID = SaveRestore.WipeId;
				if (_config.Wipe_Zones)
				{
					Interface.Oxide.DataFileSystem.DeleteDataFile(_zonesPath);
					PrintWarning("Wipe detected! Saved Zone bars have been reset!");
				}
				if (_config.Wipe_Monuments)
				{
					Interface.Oxide.DataFileSystem.DeleteDataFile(_monumentsPath);
					PrintWarning("Wipe detected! Saved Monument bars have been reset!");
				}
				SaveConfig();
			}
			_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			if (AdvancedStatus != null && AdvancedStatus?.Call("IsReady") != null)
				OnAdvancedStatusLoaded();
			Subscribe(nameof(OnMonumentsWatcherLoaded));
			Subscribe(nameof(OnAdvancedStatusLoaded));
		}
		
		void Unload()
        {
			_zonesList = null;
			_monumentsList = null;
			_config = null;
		}
        #endregion

        #region ~Status Bar~
        private void SendZoneBar(BasePlayer player, string zoneID)
        {
            if (player.IsNpc || !player.IsConnected) return;
            string barID = $"{ZoneID}_{zoneID}";
            string zoneName = (string)(ZoneManager?.Call("GetZoneName", zoneID) ?? _config.Zone_Noname);
			if (!_zonesList.ContainsKey(zoneID))
			{
				LoadZonesConfig();
                CreateZoneSettings(zoneID);
				SaveZones();
            }
            ZoneSettings barSettings = _zonesList[zoneID];
            if (!barSettings.Display) return;
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Id", barID },
				{ "Plugin", Name },
				{ "Category", ZoneID },
				{ "Order", barSettings.Order },
                { "Height", barSettings.Height },
                { "Main_Color", barSettings.Background_Color },
                { "Main_Transparency", barSettings.Background_Transparency },
                { "Main_Material", barSettings.Background_Material },
				{ "Image", _imgLibIsLoaded && barSettings.Image_Url.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }) ? barID : barSettings.Image_Url },
				{ "Image_Local", barSettings.Image_Local },
				{ "Image_Sprite", barSettings.Image_Sprite },
                { "Is_RawImage", barSettings.Image_IsRawImage },
                { "Image_Color", barSettings.Image_Color },
				{ "Image_Transparency", barSettings.Image_Transparency },
				{ "Text", zoneName },
                { "Text_Size", barSettings.Text_Size },
                { "Text_Color", barSettings.Text_Color },
                { "Text_Font", barSettings.Text_Font }
            };
            if (!string.IsNullOrWhiteSpace(barSettings.SubText))
            {
                parameters.Add("SubText", barSettings.SubText);
                parameters.Add("SubText_Size", barSettings.SubText_Size);
                parameters.Add("SubText_Color", barSettings.SubText_Color);
                parameters.Add("SubText_Font", barSettings.SubText_Font);
            }
            AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
        }
		
		private void SendMonumentBar(BasePlayer player, string monumentID)
        {
            if (player.IsNpc || !player.IsConnected) return;
            string zoneName, imageID, barID = $"{MonumentsID}_{monumentID}";
			ZoneSettings barSettings;
			if (monumentID.StartsWith("CargoShip"))
            {
				zoneName = (string)(MonumentsWatcher?.Call("GetMonumentDisplayName", monumentID, player.UserIDString, false) ?? _config.Zone_Noname);
				imageID = $"{MonumentsID}_CargoShip";
				barSettings = _monumentsList["CargoShip"];
			}
			else
            {
				zoneName = (string)(MonumentsWatcher?.Call("GetMonumentDisplayName", monumentID, player.UserIDString) ?? _config.Zone_Noname);
				imageID = barID;
				barSettings = _monumentsList[monumentID];
			}
            if (!barSettings.Display) return;
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Id", barID },
                { "Plugin", Name },
				{ "Category", MonumentsID },
				{ "Order", barSettings.Order },
                { "Height", barSettings.Height },
                { "Main_Color", barSettings.Background_Color },
                { "Main_Transparency", barSettings.Background_Transparency },
                { "Main_Material", barSettings.Background_Material },
				{ "Image", _imgLibIsLoaded && barSettings.Image_Url.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }) ? imageID : barSettings.Image_Url },
				{ "Image_Local", barSettings.Image_Local },
				{ "Image_Sprite", barSettings.Image_Sprite },
                { "Is_RawImage", barSettings.Image_IsRawImage },
                { "Image_Color", barSettings.Image_Color },
				{ "Image_Transparency", barSettings.Image_Transparency },
				{ "Text", zoneName },
                { "Text_Size", barSettings.Text_Size },
                { "Text_Color", barSettings.Text_Color },
                { "Text_Font", barSettings.Text_Font }
            };
            if (!string.IsNullOrWhiteSpace(barSettings.SubText))
            {
                parameters.Add("SubText", barSettings.SubText);
                parameters.Add("SubText_Size", barSettings.SubText_Size);
                parameters.Add("SubText_Color", barSettings.SubText_Color);
                parameters.Add("SubText_Font", barSettings.SubText_Font);
            }
            AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), parameters);
		}
		
		private void UpdateText(BasePlayer player, string id, string pluginID)
		{
			string zoneName;
			Hash<string, ZoneSettings> list;
			if (pluginID == MonumentsID)
			{
				zoneName = (string)(MonumentsWatcher?.Call("GetMonumentDisplayName", id, player.UserIDString, !id.StartsWith("CargoShip")) ?? _config.Zone_Noname);
				list = _monumentsList;
			}
			else
			{
				zoneName = (string)(ZoneManager?.Call("GetZoneName", id) ?? _config.Zone_Noname);
				list = _zonesList;
			}
			ZoneSettings barSettings = list[id];
			Dictionary<string, object> parameters = new Dictionary<string, object>
			{
				{ "Id", $"{pluginID}_{id}" },
				{ "Plugin", Name },
				{ "Text", zoneName }
			};
			if (!string.IsNullOrWhiteSpace(barSettings.SubText))
				parameters.Add("SubText", barSettings.SubText);
			AdvancedStatus?.Call(StatusUpdateContent, player.userID.Get(), parameters);
		}
		
		private void DeleteBar(BasePlayer player, string id, string pluginID) => AdvancedStatus?.Call(StatusDeleteBar, player.userID.Get(), $"{pluginID}_{id}", Name);
		
		private void CreateZoneSettings(string zoneID)
		{
			var barSettings = new ZoneSettings()
            {
                Display = _config.Status_Bar_Display,
                Order = _config.Status_Bar_Order,
                Height = _config.Status_Bar_Height,
                Background_Color = _config.Status_Background_Color,
                Background_Transparency = _config.Status_Background_Transparency,
                Background_Material = _config.Status_Background_Material,
				Image_Url = _config.Status_Image_Url,
				Image_Local = string.Empty,
				Image_Sprite = _config.Status_Image_Sprite,
                Image_IsRawImage = _config.Status_Image_IsRawImage,
                Image_Color = _config.Status_Image_Color,
                Image_Transparency = _config.Status_Image_Transparency,
                Text_Size = _config.Status_Text_Size,
                Text_Color = _config.Status_Text_Color,
                Text_Font = _config.Status_Text_Font,
                SubText = _config.Zone_SubText,
                SubText_Size = _config.Status_SubText_Size,
                SubText_Color = _config.Status_SubText_Color,
                SubText_Font = _config.Status_SubText_Font
            };
			_zonesList[zoneID] = barSettings;
            string barImg = $"{ZoneID}_{zoneID}";
			if (!string.IsNullOrWhiteSpace(_config.Status_Image_Local))
            {
				AdvancedStatus?.Call("CopyImage", _config.Status_Image_Local, barImg);
				barSettings.Image_Local = barImg;
			}
			if (_imgLibIsLoaded && barSettings.Image_Url.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }))
                ImageLibrary?.Call("AddImage", barSettings.Image_Url, barImg, 0UL);
		}
		
		private void CreateMonumentSettings(string monumentID)
        {
			var barSettings = new ZoneSettings()
            {
                Display = _config.Status_Bar_Display,
                Order = _config.Status_Bar_Order,
                Height = _config.Status_Bar_Height,
                Background_Color = _config.Status_Background_Color,
                Background_Transparency = _config.Status_Background_Transparency,
                Background_Material = _config.Status_Background_Material,
                Image_Url = _config.Status_Image_Url,
				Image_Local = string.Empty,
				Image_Sprite = _config.Status_Image_Sprite,
                Image_IsRawImage = _config.Status_Image_IsRawImage,
                Image_Color = _config.Status_Image_Color,
                Image_Transparency = _config.Status_Image_Transparency,
                Text_Size = _config.Status_Text_Size,
                Text_Color = _config.Status_Text_Color,
                Text_Font = _config.Status_Text_Font,
                SubText = _config.Zone_SubText,
                SubText_Size = _config.Status_SubText_Size,
                SubText_Color = _config.Status_SubText_Color,
                SubText_Font = _config.Status_SubText_Font
            };
			_monumentsList[monumentID] = barSettings;
			
			if (!string.IsNullOrWhiteSpace(_config.Status_Image_Local))
            {
				string barImg = $"{MonumentsID}_{monumentID}";
				AdvancedStatus?.Call("CopyImage", _config.Status_Image_Local, barImg);
				barSettings.Image_Local = barImg;
			}
        }
		
		public class ZoneSettings
		{
			public bool Display { get; set; }
			public int Order { get; set; }
			public int Height { get; set; }
			public string Background_Color { get; set; }
			public float Background_Transparency { get; set; }
			public string Background_Material { get; set; }
			public string Image_Url { get; set; }
			
			[JsonProperty(PropertyName = "Image_Local(Leave empty to use Image_Url)")]
			public string Image_Local { get; set; }
			
			[JsonProperty(PropertyName = "Image_Sprite(Leave empty to use Image_Local or Image_Url)")]
			public string Image_Sprite { get; set; }
			
			public bool Image_IsRawImage { get; set; }
			public string Image_Color { get; set; }
			public float Image_Transparency { get; set; }
			public int Text_Size { get; set; }
			public string Text_Color { get; set; }
			public string Text_Font { get; set; }
			public string SubText { get; set; }
			public int SubText_Size { get; set; }
			public string SubText_Color { get; set; }
			public string SubText_Font { get; set; }
		}
		#endregion
		
		#region ~Zones~
		private void InitZones()
		{
			_zoneIsLoaded = ZoneManager != null && ZoneManager.IsLoaded;
			if (!_zoneIsLoaded) return;
			LoadZonesConfig();
            var imgList = new List<string>();
            string imgLink;
            foreach (var barSettings in _zonesList.Values)
            {
                imgLink = barSettings.Image_Local;
                if (!string.IsNullOrWhiteSpace(imgLink) && !imgList.Contains(imgLink))
                    imgList.Add(imgLink);
            }
            AdvancedStatus?.Call("LoadImages", imgList);
            if (_imgLibIsLoaded)
            {
                var libList = new Dictionary<string, string>();
                foreach (var kvp in _zonesList)
                {
                    imgLink = kvp.Value.Image_Url;
                    if (imgLink.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }))
                        libList.Add($"{ZoneID}_{kvp.Key}", imgLink);
                }
                ImageLibrary?.Call("ImportImageList", Name, libList, 0UL, true);
            }
            var zones = (string[])(ZoneManager?.Call("GetZoneIDs") ?? Array.Empty<string>());
            foreach (var zoneID in zones)
            {
                if (_zonesList.ContainsKey(zoneID)) continue;
                CreateZoneSettings(zoneID);
                var zonePlayers = ZoneManager?.Call("GetPlayersInZone", zoneID) as List<BasePlayer>;
                if (zonePlayers == null) continue;
                foreach (var player in zonePlayers)
                    SendZoneBar(player, zoneID);
            }
            SaveZones();
            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPlayerLanguageChanged));
            Subscribe(nameof(OnEnterZone));
            Subscribe(nameof(OnExitZone));
		}
		
		private void UnloadZones()
		{
			Unsubscribe(nameof(OnEnterZone));
			Unsubscribe(nameof(OnExitZone));
			_zonesList = null;
		}
		
		private static Hash<string, ZoneSettings> _zonesList;
		private const string _zonesPath = "ZoneStatus/Zones";

        private void LoadZonesConfig()
		{
			if (Interface.Oxide.DataFileSystem.ExistsDatafile(_zonesPath))
			{
				try { _zonesList = Interface.Oxide.DataFileSystem.ReadObject<Hash<string, ZoneSettings>>(_zonesPath); }
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
			}
			if (_zonesList == null)
				_zonesList = new Hash<string, ZoneSettings>();
			SaveZones();
		}
		private void SaveZones() => Interface.Oxide.DataFileSystem.WriteObject(_zonesPath, _zonesList);
		#endregion

        #region ~Monuments~
        private void InitMonuments()
		{
			_watcherIsLoaded = MonumentsWatcher != null && MonumentsWatcher.IsLoaded;
			if (!_watcherIsLoaded) return;
			LoadMonumentsConfig();
            var monuments = (string[])(MonumentsWatcher?.Call("GetMonumentsList") ?? Array.Empty<string>());
            if (!_monumentsList.ContainsKey("CargoShip"))
                CreateMonumentSettings("CargoShip");
            foreach (var monumentID in monuments)
            {
                if (!monumentID.StartsWith("CargoShip") && !_monumentsList.ContainsKey(monumentID))
                    CreateMonumentSettings(monumentID);
            }
            SaveMonuments();
            var imgList = new List<string>();
            string imgLink;
            foreach (var barSettings in _monumentsList.Values)
            {
                imgLink = barSettings.Image_Local;
                if (!string.IsNullOrWhiteSpace(imgLink) && !imgList.Contains(imgLink))
                    imgList.Add(imgLink);
            }
            AdvancedStatus?.Call("LoadImages", imgList);
            if (_imgLibIsLoaded)
            {
                var libList = new Dictionary<string, string>();
                if (_monumentsList["CargoShip"].Image_Url.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }))
                    libList.Add($"{MonumentsID}_CargoShip", _monumentsList["CargoShip"].Image_Url);
                foreach (var kvp in _monumentsList)
                {
                    if (kvp.Key.StartsWith("CargoShip")) continue;
                    imgLink = kvp.Value.Image_Url;
                    if (imgLink.StartsWithAny(new string[2] { HttpScheme, HttpsScheme }))
                        libList.Add($"{MonumentsID}_{kvp.Key}", imgLink);
                }
                ImageLibrary?.Call("ImportImageList", Name, libList, 0UL, true);
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!player.userID.IsSteamId()) continue;
                string monumentID = (string)(MonumentsWatcher?.Call("GetPlayerMonument", player.userID)) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(monumentID))
                    SendMonumentBar(player, monumentID);
            }
            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPlayerLanguageChanged));
            Subscribe(nameof(OnPlayerEnteredMonument));
            Subscribe(nameof(OnPlayerExitedMonument));
		}
		
		private void UnloadMonuments()
        {
			Unsubscribe(nameof(OnPlayerEnteredMonument));
			Unsubscribe(nameof(OnPlayerExitedMonument));
			_monumentsList = null;
		}
		
		private static Hash<string, ZoneSettings> _monumentsList;
		private const string _monumentsPath = "ZoneStatus/Monuments";
		
		private void LoadMonumentsConfig()
		{
			if (Interface.Oxide.DataFileSystem.ExistsDatafile(_monumentsPath))
			{
				try { _monumentsList = Interface.Oxide.DataFileSystem.ReadObject<Hash<string, ZoneSettings>>(_monumentsPath); }
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
			}
			if (_monumentsList == null)
				_monumentsList = new Hash<string, ZoneSettings>();
			SaveMonuments();
		}
		private void SaveMonuments() => Interface.Oxide.DataFileSystem.WriteObject(_monumentsPath, _monumentsList);
		#endregion
	}
}