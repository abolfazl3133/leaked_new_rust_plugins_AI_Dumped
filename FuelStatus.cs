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
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/fuel-status
*  Codefling license: https://codefling.com/plugins/fuel-status?tab=downloads_field_4
*
*  Copyright © 2024 IIIaKa
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
	[Info("Fuel Status", "IIIaKa", "0.1.3")]
	[Description("The plugin displays the vehicle's fuel level in the status bar. Depends on AdvancedStatus plugin.")]
	class FuelStatus : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, AdvancedStatus;

		#region ~Variables~
		private bool _imgLibIsLoaded = false, _runEffect = false;
		private const string BarID = "FuelStatus_Fuel", ZeroLang = "MsgProgressZero", LowLang = "MsgFuelLow", StatusCreateBar = "CreateBar", StatusUpdateContent = "UpdateContent", StatusDeleteBar = "DeleteBar", Pedal = "pedal";
		private readonly string[] HttpScheme = new string[2] { "http://", "https://" };
		private (string Key, string Value) _barImage;
		private Hash<ulong, IFuelSystem> _fuels = new Hash<ulong, IFuelSystem>();
		private Hash<ulong, float> _prevLevel = new Hash<ulong, float>();
		private Timer _timer;
		private readonly int[] _driverSit = new int[] { 1, 5, 9, 11, 21, 23, 26, 28, 31 };
		private List<GaugeIndicator> _defaultIndicators = new List<GaugeIndicator>() { new GaugeIndicator(0.0f, 0.2f, "#F70000"), new GaugeIndicator(0.2f, 0.6f, "#F7BB00"), new GaugeIndicator(0.6f, 1.0f, "#B1C06E") };
		#endregion

		#region ~Configuration~
		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Fuel indicator refresh interval in seconds")]
			public float Fuel_Update_Time = 5f;
			
			[JsonProperty(PropertyName = "Notifications - The percentage(0.0 to 1.0) of fuel at which notifications will occur. A value of 0 disables this")]
            public float Notifications_Level = 0.2f;
			
			[JsonProperty(PropertyName = "Notifications - The effect that will be triggered upon a warning. Choose the effect carefully! An empty string disables the effect call")]
            public string Notifications_Effect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
			
			[JsonProperty(PropertyName = "Status. Bar - Height")]
			public int Status_Bar_Height = 26;

			[JsonProperty(PropertyName = "Status. Bar - Order")]
			public int Status_Bar_Order = 1;

			[JsonProperty(PropertyName = "Status. Background - Color")]
			public string Status_Background_Color = "#FFFFFF";
			
			[JsonProperty(PropertyName = "Status. Background - Transparency")]
			public float Status_Background_Transparency = 0.15f;
			
			[JsonProperty(PropertyName = "Status. Background - Material(empty to disable)")]
			public string Status_Background_Material = "";

			[JsonProperty(PropertyName = "Status. Image - Url")]
			public string Status_Image_Url = "https://i.imgur.com/LP54lLZ.png";
			
			[JsonProperty(PropertyName = "Status. Image - Local(Leave empty to use Image_Url)")]
            public string Status_Image_Local = "FuelStatus_Fuel";
			
			[JsonProperty(PropertyName = "Status. Image - Sprite(Leave empty to use Image_Local or Image_Url)")]
			public string Status_Image_Sprite = "";

			[JsonProperty(PropertyName = "Status. Image - Is raw image")]
			public bool Status_Image_IsRawImage = false;

			[JsonProperty(PropertyName = "Status. Image - Color")]
			public string Status_Image_Color = "#E2DBD6";
			
			[JsonProperty(PropertyName = "Status. Image - Transparency")]
			public float Status_Image_Transparency = 0.55f;
			
			[JsonProperty(PropertyName = "Status. Text - Size")]
			public int Status_Text_Size = 15;
			
			[JsonProperty(PropertyName = "Status. Text - Color")]
			public string Status_Text_Color = "#E2DBD6";

			[JsonProperty(PropertyName = "Status. Text - Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
			public string Status_Text_Font = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Status. Text - Offset Horizontal")]
			public int Status_Text_Offset_Horizontal = 7;
			
			[JsonProperty(PropertyName = "Status. Progress - Transparency")]
			public float Status_Progress_Transparency = 0.8f;
			
			[JsonProperty(PropertyName = "Status. Progress - OffsetMin")]
			public string Status_Progress_OffsetMin = "25 2.5";
			
			[JsonProperty(PropertyName = "Status. Progress - OffsetMax")]
			public string Status_Progress_OffsetMax = "-3.5 -3.5";
			
			[JsonProperty(PropertyName = "Status. Progress - Zero Text Size")]
			public int Status_Progress_Zero_Size = 12;
			
			[JsonProperty(PropertyName = "Status. Progress - Zero Text Color")]
			public string Status_Progress_Zero_Color = "#F70000";
			
			[JsonProperty(PropertyName = "List of Gauge Indicators")]
			public List<GaugeIndicator> GaugeIndicators = new List<GaugeIndicator>();

			public Oxide.Core.VersionNumber Version;
		}
		
		public class GaugeIndicator
        {
			private float _minRange;
			public float MinRange
			{
				get => _minRange;
				set => _minRange = Mathf.Clamp(value, 0f, 1f);
			}
			
			private float _maxRange;
			public float MaxRange
			{
				get => _maxRange;
				set => _maxRange = Mathf.Clamp(value, 0f, 1f);
			}
			
			public string Color { get; set; } = string.Empty;
			
			public GaugeIndicator() {}
			public GaugeIndicator(float min, float max, string color)
            {
				MinRange = min;
				MaxRange = max;
				Color = color;
			}
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
			
			_config.Notifications_Level = Mathf.Clamp(_config.Notifications_Level, 0f, 1f);
			if (!string.IsNullOrWhiteSpace(_config.Notifications_Effect))
				_runEffect = true;
			if (_config.GaugeIndicators == null || !_config.GaugeIndicators.Any())
				_config.GaugeIndicators = new List<GaugeIndicator>(_defaultIndicators);
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
				["MsgProgressZero"] = "Out of fuel, refill required!",
				["MsgFuelLow"] = "Warning: Fuel level is low!"
            }, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgProgressZero"] = "Нет топлива!",
				["MsgFuelLow"] = "Внимание: уровень топлива низкий!"
            }, this, "ru");
		}
		#endregion
		
		#region ~Methods~
		private void LoadImages()
		{
			if (_imgLibIsLoaded && string.IsNullOrWhiteSpace(_config.Status_Image_Local) && _config.Status_Image_Url.StartsWithAny(HttpScheme))
				ImageLibrary?.Call("AddImage", _config.Status_Image_Url, BarID, 0uL);
		}
		
		private void InitPlugin()
        {
			if (!string.IsNullOrEmpty(_config.Status_Image_Local))
				AdvancedStatus?.Call("LoadImage", BarID);
			InitSelectedImage();
			foreach (var player in BasePlayer.activePlayerList)
            {
				if (!player.IsNpc && TryGetFuel(player, out var fuelSystem))
					SendBar(player.userID, fuelSystem);
			}
            Subscribe(nameof(OnEntityMounted));
            Subscribe(nameof(OnEntityDismounted));
            Subscribe(nameof(OnEntityKill));
            _timer = timer.Every(_config.Fuel_Update_Time, () => { UpdateFuel(); });
        }
		
		private void SendBar(ulong userID, IFuelSystem fuelSystem)
        {
			if (fuelSystem == null) return;
			float progress = fuelSystem.GetFuelFraction();
			Dictionary<string, object> parameters = new Dictionary<string, object>
			{
				{ "Id", BarID },
				{ "Plugin", Name },
				{ "Order", _config.Status_Bar_Order },
				{ "Height", _config.Status_Bar_Height },
				{ "Main_Color", _config.Status_Background_Color },
				{ "Main_Transparency", _config.Status_Background_Transparency },
				{ "Main_Material", _config.Status_Background_Material },
				{ _barImage.Key, _barImage.Value },
				{ "Is_RawImage", _config.Status_Image_IsRawImage },
				{ "Image_Color", _config.Status_Image_Color },
				{ "Image_Transparency", _config.Status_Image_Transparency },
				{ "Text_Font", _config.Status_Text_Font },
				{ "Text_Offset_Horizontal", _config.Status_Text_Offset_Horizontal },
				{ "Progress", progress }
			};
			if (progress > 0f)
			{
				parameters.Add("Text", fuelSystem.GetFuelAmount().ToString());
				parameters.Add("Text_Size", _config.Status_Text_Size);
				parameters.Add("Text_Color", _config.Status_Text_Color);
				parameters.Add("Progress_Color", GetProgressColor(progress));
				parameters.Add("Progress_Transparency", _config.Status_Progress_Transparency);
				parameters.Add("Progress_OffsetMin", _config.Status_Progress_OffsetMin);
				parameters.Add("Progress_OffsetMax", _config.Status_Progress_OffsetMax);
			}
			else
			{
				parameters.Add("Text", lang.GetMessage(ZeroLang, this, userID.ToString()));
				parameters.Add("Text_Size", _config.Status_Progress_Zero_Size);
				parameters.Add("Text_Color", _config.Status_Progress_Zero_Color);
			}
			AdvancedStatus?.Call(StatusCreateBar, userID, parameters);
			_fuels[userID] = fuelSystem;
		}
		
		private void UpdateFuelValue(ulong userID, IFuelSystem fuelSystem)
        {
			if (fuelSystem == null) return;
			string userIDString = userID.ToString();
			float progress = fuelSystem.GetFuelFraction();
			Dictionary<string, object> parameters = new Dictionary<string, object>
			{
				{ "Id", BarID },
				{ "Plugin", Name },
				{ "Progress", progress }
			};
			if (progress > 0f)
			{
				parameters.Add("Text", fuelSystem.GetFuelAmount().ToString());
				parameters.Add("Text_Size", _config.Status_Text_Size);
				parameters.Add("Text_Color", _config.Status_Text_Color);
				parameters.Add("Progress_Color", GetProgressColor(progress));
				parameters.Add("Progress_Transparency", _config.Status_Progress_Transparency);
				parameters.Add("Progress_OffsetMin", _config.Status_Progress_OffsetMin);
				parameters.Add("Progress_OffsetMax", _config.Status_Progress_OffsetMax);
			}
			else
			{
				parameters.Add("Text", lang.GetMessage(ZeroLang, this, userIDString));
				parameters.Add("Text_Size", _config.Status_Progress_Zero_Size);
				parameters.Add("Text_Color", _config.Status_Progress_Zero_Color);
			}
			AdvancedStatus?.Call(StatusUpdateContent, userID, parameters);

			if (_config.Notifications_Level <= 0f) return;
			if (progress <= _config.Notifications_Level && _prevLevel.TryGetValue(userID, out var oldLevel) && oldLevel > _config.Notifications_Level)
            {
                var player = BasePlayer.FindByID(userID);
                if (player != null)
                {
                    player.Command("gametip.showtoast", (int)GameTip.Styles.Error, lang.GetMessage(LowLang, this, userIDString), string.Empty);
                    if (_runEffect)
                        Effect.server.Run(_config.Notifications_Effect, player, 0u, Vector3.zero, Vector3.zero);
                }
            }
            _prevLevel[userID] = progress;
		}
		
		private void DestroyBar(ulong userID)
        {
			if (_fuels.ContainsKey(userID))
            {
				_fuels.Remove(userID);
				_prevLevel.Remove(userID);
				AdvancedStatus?.Call(StatusDeleteBar, userID, BarID, Name);
			}
		}
		
		private void InitSelectedImage()
        {
			if (!string.IsNullOrEmpty(_config.Status_Image_Sprite))
				_barImage = ("Image_Sprite", _config.Status_Image_Sprite);
			else if (!string.IsNullOrEmpty(_config.Status_Image_Local))
				_barImage = ("Image_Local", _config.Status_Image_Local);
			else
				_barImage = ("Image", _imgLibIsLoaded && _config.Status_Image_Url.StartsWithAny(HttpScheme) ? BarID : _config.Status_Image_Url);
		}
		
		private string GetProgressColor(float progress)
        {
			foreach (var indicator in _config.GaugeIndicators)
			{
				if (progress >= indicator.MinRange && progress <= indicator.MaxRange)
					return indicator.Color;
			}
			Puts($"Could not find a color for the value {progress}f. Default color will be used.");
			return "#B1C06E";
		}
		
		private bool TryGetFuel(BasePlayer player, out IFuelSystem result)
        {
			result = null;
			if (!player.isMounted)
				return false;
			var mount = player.GetMounted();
			if (mount.IsValid() && mount.isMobile && _driverSit.Contains((int)mount.mountPose))
			{
				var vehicle = mount.VehicleParent();
				if (vehicle != null && !vehicle.ShortPrefabName.Contains(Pedal))
					result = vehicle.GetFuelSystem();
			}
			return result != null;
		}
		
		private void UpdateFuel()
        {
			foreach (var kvp in _fuels)
			{
				if (kvp.Value != null)
					UpdateFuelValue(kvp.Key, kvp.Value);
			}
		}
		#endregion

        #region ~Oxide Hooks~
        void OnEntityMounted(BaseMountable mount, BasePlayer player)
        {
			if (TryGetFuel(player, out var fuelSystem))
				SendBar(player.userID, fuelSystem);
		}
		
		void OnEntityMounted(DiverPropulsionVehicle dpv, BasePlayer player)
        {
			var fuelSystem = dpv.GetFuelSystem();
			if (fuelSystem != null)
				SendBar(player.userID, fuelSystem);
		}
		
		void OnEntityDismounted(BaseMountable mount, BasePlayer player) => DestroyBar(player.userID);
		
		void OnEntityKill(BaseVehicle vehicle)
        {
			var driver = vehicle.GetDriver();
			if (driver != null)
				DestroyBar(driver.userID);
		}
		
		void OnEntityKill(DiverPropulsionVehicle dpv)
        {
			var driver = dpv.GetMounted();
			if (driver != null)
                DestroyBar(driver.userID);
        }
		
		void OnAdvancedStatusLoaded() => InitPlugin();
		
		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin == ImageLibrary)
			{
				_imgLibIsLoaded = true;
				LoadImages();
				InitSelectedImage();
			}
		}
		
		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
				_imgLibIsLoaded = false;
			else if (plugin.Name == "AdvancedStatus")
			{
				Unsubscribe(nameof(OnEntityMounted));
				Unsubscribe(nameof(OnEntityDismounted));
				Unsubscribe(nameof(OnEntityKill));
				if (_timer != null)
					_timer.Destroy();
				_fuels.Clear();
				_prevLevel.Clear();
			}
		}

		void Init()
		{
			Unsubscribe(nameof(OnEntityMounted));
			Unsubscribe(nameof(OnEntityDismounted));
			Unsubscribe(nameof(OnEntityKill));
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
		}

		void OnServerInitialized(bool initial)
        {
			_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			if (_imgLibIsLoaded)
				LoadImages();
			bool statusIsLoaded = AdvancedStatus != null && AdvancedStatus?.Call("IsReady") != null;
			if (!statusIsLoaded)
            {
				if (initial && AdvancedStatus != null)
                    PrintWarning("AdvancedStatus plugin found, but not ready yet. Waiting for it to load...");
                else
                    PrintWarning("AdvancedStatus plugin not found! To function, it is necessary to install it!\nhttps://codefling.com/plugins/advanced-status");
            }
			else
				OnAdvancedStatusLoaded();
			Subscribe(nameof(OnAdvancedStatusLoaded));
		}

		void Unload()
		{
			if (_timer != null)
				_timer.Destroy();
			_config = null;
		}
		#endregion
	}
}