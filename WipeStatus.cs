using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Wipe Status", "IIIaKa", "0.1.5")]
	[Description("The plugin displays the time until the next wipe in the status bar. Depends on AdvancedStatus plugin.")]
	class WipeStatus : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, AdvancedStatus;

		#region ~Variables~
		private int _imgLibCheck = 0;
		private bool _imgLibIsLoaded = false;
		private bool _statusIsLoaded = false;
		private const string PERMISSION_ADMIN = "wipestatus.admin";
		private const string _barID = "WipeStatus_";
		private double _timeStamp = 0d;
		private Timer _timer;
		#endregion

		#region ~Configuration~
		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "ImageLibrary Counter Check")]
			public int ImgLibCounter = 5;
			
			[JsonProperty(PropertyName = "Wipe command")]
			public string Command = "wipe";
			
			[JsonProperty(PropertyName = "Use GameTip for messages?")]
			public bool Use_GameTips = true;
			
			[JsonProperty("Is it worth displaying the wipe timer only when players in the safe zone or building privilege?")]
			public bool Status_InSafeZone = false;
			
			[JsonProperty(PropertyName = "When should it start displaying? Based on how many days are left(0 to display always)")]
			public int Status_Days_Left = 0;
			
			[JsonProperty(PropertyName = "Custom wipe dates list(empty to use default). Format: yyyy-MM-dd HH:mm. Example: 2023-12-10 13:00")]
			public List<string> Custom_Date_List = new List<string>();
			
			[JsonProperty(PropertyName = "Status. Bar - Height")]
			public int Status_Bar_Height = 26;
			
			[JsonProperty(PropertyName = "Status. Bar - Order")]
			public int Status_Bar_Order = 10;
			
			[JsonProperty("Status. Background - Color")]
			public string Status_Background_Color = "#0370A4";
			
			[JsonProperty(PropertyName = "Status. Background - Transparency")]
			public float Status_Background_Transparency = 0.7f;
			
			[JsonProperty(PropertyName = "Status. Background - Material(empty to disable)")]
			public string Status_Background_Material = "";
			
			[JsonProperty("Status. Image - URL")]
			public string Status_Image_Url = "https://i.imgur.com/FKrFYN5.png";
			
			[JsonProperty(PropertyName = "Status. Image - Sprite(empty to use image from URL)")]
			public string Status_Image_Sprite = "";
			
			[JsonProperty("Status. Image - Is raw image")]
			public bool Status_Image_IsRawImage = false;
			
			[JsonProperty("Status. Image - Color")]
			public string Status_Image_Color = "#0370A4";
			
			[JsonProperty(PropertyName = "Status. Text - Size")]
			public int Status_Text_Size = 12;
			
			[JsonProperty("Status. Text - Color")]
			public string Status_Text_Color = "#FFFFFF";
			
			[JsonProperty(PropertyName = "Status. Text - Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
			public string Status_Text_Font = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Status. SubText - Size")]
			public int Status_SubText_Size = 12;

			[JsonProperty("Status. SubText - Color")]
			public string Status_SubText_Color = "#FFFFFF";

			[JsonProperty(PropertyName = "Status. SubText - Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
			public string Status_SubText_Font = "RobotoCondensed-Bold.ttf";
			
			public Oxide.Core.VersionNumber Version;
		}
		
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null)
				{
					PrintWarning("Configuration file not found. Creating a new one...");
					LoadDefaultConfig();
				}
				else if (_config.Version < Version)
				{
					PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
					LoadDefaultConfig();
					_config.Version = Version;
					PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
				}
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration();
		#endregion
		
		#region ~DataFile~
		private static StoredData _storedData;

		private class StoredData
		{
			[JsonProperty(PropertyName = "List of players.")]
			public Dictionary<ulong, bool> PlayersList = new Dictionary<ulong, bool>();
		}
		
		private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
		#endregion
		
		#region ~Language~
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgText"] = "WIPE IN",
				["MsgNewDateAdded"] = "The new date {0} has been successfully added.",
				["MsgNewDateAddFailed"] = "Invalid format or date is earlier than the current one. Date format: yyyy-MM-dd HH:mm",
				["MsgClearDates"] = "Custom dates list has been successfully cleared!",
				["MsgSetBar"] = "Displaying the bar: {0}"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgText"] = "ВАЙП ЧЕРЕЗ",
				["MsgNewDateAdded"] = "Новая дата {0} успешно добавлена.",
				["MsgNewDateAddFailed"] = "Не верный формат или дата меньше текущей. Формат даты: yyyy-MM-dd HH:mm",
				["MsgClearDates"] = "Список дат был успешно очищен!",
				["MsgSetBar"] = "Отображение бара: {0}"
			}, this, "ru");
		}
		#endregion
		
		#region ~Methods~
		private void ImgLibCheck()
		{
			if (ImageLibrary == null || !ImageLibrary.IsLoaded)
			{
				_imgLibCheck++;

				if (_imgLibCheck >= _config.ImgLibCounter)
				{
					PrintError("ImageLibrary appears to be missing or occupied by other plugins load orders. For full plugin functionality, it is recommended to load ImageLibrary plugin.", _config.ImgLibCounter, Name);
					return;
				}

				timer.In(60, ImgLibCheck);
				if (ImageLibrary == null)
					PrintWarning("ImageLibrary is NOT loaded! Waiting ImageLibrary...");
				else
					PrintWarning("ImageLibrary appears to be occupied will check again in 1 minute.");
				return;
			}

			_imgLibCheck = 0;
			_imgLibIsLoaded = true;
			LoadImgs();
		}

		private void LoadImgs()
		{
			if (_config.Status_Image_Url.StartsWithAny(new string[2] { "http://", "https://" }))
			{
				Dictionary<string, string> imgList = new Dictionary<string, string>() { { $"{Name}_Wipe", _config.Status_Image_Url } };
				ImageLibrary?.Call("ImportImageList", Name, imgList, 0UL, true);
			}
		}
		
		private void SendBar(BasePlayer player)
		{
			if (!player.IsConnected || (_storedData.PlayersList.TryGetValue(player.userID, out var canShow) && !canShow)) return;
			if (_config.Status_InSafeZone && !player.InSafeZone() && !InBuildingPrivilege(player)) return;
			Dictionary<string, object> parameters = new Dictionary<string, object>
			{
				{ "Id", _barID },
				{ "BarType", "TimeCounter" },
				{ "Plugin", Name },
				{ "Order", _config.Status_Bar_Order },
				{ "Height", _config.Status_Bar_Height },
				{ "Main_Color", _config.Status_Background_Color },
				{ "Main_Transparency", _config.Status_Background_Transparency },
				{ "Main_Material", _config.Status_Background_Material },
				{ "Image", _imgLibIsLoaded && _config.Status_Image_Url.StartsWithAny(new string[2] { "http://", "https://" }) ? $"{Name}_Wipe" : _config.Status_Image_Url },
				{ "Image_Sprite", _config.Status_Image_Sprite },
				{ "Is_RawImage", _config.Status_Image_IsRawImage },
				{ "Image_Color", _config.Status_Image_Color },
				{ "Text", lang.GetMessage("MsgText", this, player.UserIDString) },
				{ "Text_Size", _config.Status_Text_Size },
				{ "Text_Color", _config.Status_Text_Color },
				{ "Text_Font", _config.Status_Text_Font },
				{ "SubText_Size", _config.Status_SubText_Size },
				{ "SubText_Color", _config.Status_SubText_Color },
				{ "SubText_Font", _config.Status_SubText_Font },
				{ "TimeStamp", _timeStamp }
			};
			AdvancedStatus?.Call("CreateBar", player.userID, parameters);
		}
		
		private bool InBuildingPrivilege(BasePlayer player) => (bool)(AdvancedStatus?.Call("InBuildingPrivilege", player) ?? false);
		
		private bool GetSpanUntilWipe(out TimeSpan result)
		{
			if (!GetCustomSpan(out result) && WipeTimer.serverinstance != null)
				result = WipeTimer.serverinstance.GetTimeSpanUntilWipe();
			else
				PrintWarning("The custom date list is empty, and the vanilla wipe date could not be obtained! You need to add the date manually.");
			return result != TimeSpan.Zero;
			
			bool GetCustomSpan(out TimeSpan customSpan)
			{
				customSpan = TimeSpan.Zero;
				if (_config.Custom_Date_List.Any())
				{
					var minDate = DateTime.MaxValue;
					foreach (var dateString in _config.Custom_Date_List)
					{
						if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm", null, DateTimeStyles.None, out var date) && date > DateTime.Now && date < minDate)
							minDate = date;
					}
					if (minDate != DateTime.MaxValue)
						customSpan = minDate.Subtract(DateTime.Now);
				}
				return customSpan != TimeSpan.Zero;
			}
		}
		
		private void TryDisplay()
		{
			if (!_statusIsLoaded || !GetSpanUntilWipe(out var timeSpan)) return;
			_timeStamp = timeSpan.TotalSeconds + Network.TimeEx.currentTimestamp;
			if (_config.Status_Days_Left == 0 || ((int)Math.Ceiling(timeSpan.TotalDays) <= _config.Status_Days_Left))
			{
				foreach (var player in BasePlayer.activePlayerList)
				{
					if (player.IsNpc) continue;
					SendBar(player);
				}
				Subscribe(nameof(OnPlayerConnected));
				Subscribe(nameof(OnPlayerSetInfo));
				if (_config.Status_InSafeZone)
				{
					Subscribe(nameof(OnEntityEnter));
					Subscribe(nameof(OnEntityLeave));
					Subscribe(nameof(OnPlayerGainedBuildingPrivilege));
					Subscribe(nameof(OnPlayerLostBuildingPrivilege));
				}
				if (_timer != null)
					_timer.Destroy();
				_timer = timer.Once((float)(timeSpan.TotalSeconds + 1), () =>
				{
					Unsubscribe(nameof(OnPlayerConnected));
					Unsubscribe(nameof(OnPlayerSetInfo));
					Unsubscribe(nameof(OnEntityEnter));
					Unsubscribe(nameof(OnEntityLeave));
					Unsubscribe(nameof(OnPlayerGainedBuildingPrivilege));
					Unsubscribe(nameof(OnPlayerLostBuildingPrivilege));
					TryDisplay();
				});
			}
			else
            {
				_timeStamp = 0d;
				_timer = timer.Once((float)(timeSpan.TotalSeconds - TimeSpan.FromDays(_config.Status_Days_Left).TotalSeconds), () => { TryDisplay(); });
			}
		}
		#endregion

		#region ~Hooks~
		void OnPlayerConnected(BasePlayer player) => SendBar(player);
		
		void OnPlayerSetInfo(Network.Connection connection, string key, string value)
		{
			if (key == "global.language" && BasePlayer.FindByID(connection?.userid ?? 0) is BasePlayer player)
            {
				if (_storedData.PlayersList.TryGetValue(player.userID, out var canShow) && !canShow) return;
				if (_config.Status_InSafeZone && !player.InSafeZone() && !InBuildingPrivilege(player)) return;
				AdvancedStatus?.Call("UpdateContent", player.userID, new Dictionary<string, object>
				{
					{ "Id", _barID },
					{ "Plugin", Name },
					{ "Text", lang.GetMessage("MsgText", this, player.UserIDString) }
				});
			}
		}
		
		void OnEntityEnter(TriggerSafeZone trigger, BasePlayer player) => SendBar(player);
		
		void OnEntityLeave(TriggerSafeZone trigger, BasePlayer player)
		{
			if (!player.InSafeZone())
				AdvancedStatus?.Call("DeleteBar", player, _barID, Name);
		}
		
		void OnPlayerGainedBuildingPrivilege(BasePlayer player) => SendBar(player);
		
		void OnPlayerLostBuildingPrivilege(BasePlayer player) => AdvancedStatus?.Call("DeleteBar", player, _barID, Name);
		
		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin == ImageLibrary)
				ImgLibCheck();
			else if (plugin == AdvancedStatus)
			{
				_statusIsLoaded = plugin.IsLoaded;
				TryDisplay();
			}
		}
		
		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
				_imgLibIsLoaded = false;
			if (plugin.Name == "AdvancedStatus")
            {
				_statusIsLoaded = false;
				if (_timer != null)
					_timer.Destroy();
				Unsubscribe(nameof(OnPlayerConnected));
				Unsubscribe(nameof(OnPlayerSetInfo));
				Unsubscribe(nameof(OnEntityEnter));
				Unsubscribe(nameof(OnEntityLeave));
				Unsubscribe(nameof(OnPlayerGainedBuildingPrivilege));
				Unsubscribe(nameof(OnPlayerLostBuildingPrivilege));
				_timeStamp = 0d;
			}
		}
		
		void Init()
		{
			Unsubscribe(nameof(OnPlayerConnected));
			Unsubscribe(nameof(OnPlayerSetInfo));
			Unsubscribe(nameof(OnEntityEnter));
			Unsubscribe(nameof(OnEntityLeave));
			Unsubscribe(nameof(OnPlayerGainedBuildingPrivilege));
			Unsubscribe(nameof(OnPlayerLostBuildingPrivilege));
			permission.RegisterPermission(PERMISSION_ADMIN, this);
			AddCovalenceCommand(_config.Command, nameof(WipeStatus_Command));
			_storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
		}
		
		void OnServerInitialized()
		{
			_statusIsLoaded = AdvancedStatus != null && AdvancedStatus.IsLoaded;
			ImgLibCheck();
			if (_statusIsLoaded)
				TryDisplay();
		}
		#endregion
		
		#region ~Commands~
		private void WipeStatus_Command(IPlayer player, string command, string[] args)
		{
			if (args != null && args.Length > 0)
			{
				string replyKey = string.Empty;
				string[] replyArgs = new string[5];
				bool isWarning = false;
				if (args[0] == "bar")
				{
					if (player.Object is BasePlayer bPlayer)
                    {
						if (!_storedData.PlayersList.ContainsKey(bPlayer.userID))
							_storedData.PlayersList[bPlayer.userID] = true;
						_storedData.PlayersList[bPlayer.userID] = !_storedData.PlayersList[bPlayer.userID];
						if (_timeStamp > 0d)
                        {
							if (_storedData.PlayersList[bPlayer.userID])
								SendBar(bPlayer);
							else
								AdvancedStatus?.Call("DeleteBar", bPlayer.userID, _barID, Name);
						}
						replyKey = "MsgSetBar";
						replyArgs[0] = $"{_storedData.PlayersList[bPlayer.userID]}";
					}
				}
				else if (player.IsServer || permission.UserHasPermission(player.Id, PERMISSION_ADMIN))
				{
					if (args[0] == "add" && args.Length > 1)
					{
						if (DateTime.TryParseExact(args[1], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var newDate) && newDate > DateTime.Now)
						{
							_config.Custom_Date_List.Add(args[1]);
							SaveConfig();
							if (_timeStamp == 0d || newDate.Subtract(DateTime.Now).TotalSeconds + DateTimeOffset.UtcNow.ToUnixTimeSeconds() < _timeStamp)
								TryDisplay();
							replyKey = "MsgNewDateAdded";
						}
						else
						{
							replyKey = "MsgNewDateAddFailed";
							isWarning = true;
						}
						replyArgs[0] = args[1];
					}
					else if (args[0] == "clear")
					{
						_config.Custom_Date_List.Clear();
						SaveConfig();
						if (_timeStamp > 0d)
                        {
							AdvancedStatus?.Call("DeleteBar", _barID, Name);
							TryDisplay();
						}
						replyKey = "MsgClearDates";
					}
				}
				
				if (!string.IsNullOrWhiteSpace(replyKey))
                {
					if (!player.IsServer && _config.Use_GameTips)
						player.Command("gametip.showtoast", isWarning ? 1 : 0, string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs), null, null);
					else
						player.Reply(string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs));
				}
			}
		}
		#endregion
		
		#region ~Unload~
		void Unload()
		{
			if (_timer != null)
				_timer.Destroy();
			_config = null;
			_storedData = null;
		}
		#endregion
	}
}