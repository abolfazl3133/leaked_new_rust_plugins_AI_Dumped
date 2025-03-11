using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Auto Clans", "YaMang -w-", "1.0.4")]
	public class AutoClans : RustPlugin
    {
        #region Fleids
        [PluginReference] Plugin Clans;
		private bool _debug = false;

		System.Random random = new System.Random();
		Dictionary<string, string> noclan = new Dictionary<string, string>();
		Dictionary<string, Timer> leftTime = new Dictionary<string, Timer>();

		private string BypassPerm = "AutoClans.bypass";
		#endregion

		#region Command
		private void ClanCreate(BasePlayer player)
        {
			if (permission.UserHasPermission(player.UserIDString, BypassPerm)) return;
			if (HasClan(player)) return;

			string ClanTag = SetClanTag();

			player.SendConsoleCommand($"chat.say \"/clan create {ClanTag}\"");
		}
        #endregion

        #region OxideHook

        private void OnServerInitialized()
        {
			if(Clans == null)
            {
				PrintError("Clans plugin not loaded.");
				Unsubscribe(nameof(OnPlayerConnected));
				Unsubscribe(nameof(OnPlayerDisconnected));
				return;
            }

            _debug = _config.generalSettings.Debug;
			permission.RegisterPermission(BypassPerm, this);
			cmd.AddChatCommand(_config.generalSettings.Command, this, nameof(ClanCreate));

            if(_config.generalSettings.PluginLoadClanCheck)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (permission.UserHasPermission(player.UserIDString, BypassPerm)) continue;
                    InitTimer(player);
				}
            }

        }

        private void OnPlayerConnected(BasePlayer player)
        {
			if (_config.clanSettings.AutoCreateClan)
				ClanCreate(player);
			else
            {
				InitTimer(player);
			}
        }
		
		void Unload()
		{
			foreach (var bp in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(bp, "WarnUI");
			}
		}

		void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (noclan.ContainsKey(player.UserIDString))
                noclan.Remove(player.UserIDString);

			if(leftTime.ContainsKey(player.UserIDString))
            {
				leftTime[player.UserIDString].Destroy();
				leftTime.Remove(player.UserIDString);
            }
        }

		#endregion

		#region Funtions
		private bool HasClan(BasePlayer player)
        {
			if (string.IsNullOrEmpty(GetClanOf(player.UserIDString)))
			{
				return false;
			}

			return true;
		}
		private void InitTimer(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, BypassPerm)) return;
            if (HasClan(player) || player.IsConnected == false || leftTime.ContainsKey(player.UserIDString)) return;

			var lefttimer = timer.Repeat(1f, _config.clanSettings.NotJoinWarnTime + 2, () =>
			{
				HasCountDown(player);
			});

			leftTime.Add(player.UserIDString, lefttimer);

		}

		#region Clans Hook
		private string GetClanOf(string playerID) => (string)Clans?.Call("GetClanOf", playerID);

		private JObject GetClan(string tag) => (JObject)Clans?.Call("GetClan", tag);

		private void OnClanCreate(string tag)
        {
			var getClan = GetClan(tag);
			var owner = BasePlayer.FindByID(Convert.ToUInt64(getClan["owner"]));

            if (leftTime.ContainsKey(owner.UserIDString))
            {
                if (!leftTime[owner.UserIDString].Destroyed)
                    leftTime[owner.UserIDString].Destroy();
                leftTime.Remove(owner.UserIDString);
				noclan.Remove(owner.UserIDString);
				CuiHelper.DestroyUi(owner, "WarnUI");
			}
        }

		//Clans - Chaos Code
		private void OnClanMemberJoined(ulong userID, string tag) => OnClanMemberJoined(userID.ToString(), tag);
		private void OnClanMemberJoined(string userID, string tag)
        {
			if (leftTime.ContainsKey(userID))
			{
				var player = BasePlayer.FindByID(Convert.ToUInt64(userID));
				if (!leftTime[userID].Destroyed)
					leftTime[userID].Destroy();
				leftTime.Remove(userID);
				noclan.Remove(userID);
				CuiHelper.DestroyUi(player, "WarnUI");
			}
        }
		//Clans - Chaos Code
		private void OnClanMemberGone(ulong userID, string tag) => OnClanMemberGone(userID.ToString(), tag);
		private void OnClanMemberGone(string userID, string tag)
        {
			var player = BasePlayer.FindByID(Convert.ToUInt64(userID));
			if (player == null) return;

			InitTimer(player);
			
		}
        #region Clans - Chaos Code
        private void OnClanDisbanded(List<ulong> memberUserIDs) 
		{
			List<string> memberUserIDs_String = new List<string>();

            foreach (var member in memberUserIDs)
            {
				memberUserIDs_String.Add(member.ToString());
			}

			OnClanDisbanded(memberUserIDs_String);
		}
        #endregion
        private void OnClanDisbanded(List<string> memberUserIDs)
        {
			foreach (var id in memberUserIDs)
            {
				var player = BasePlayer.FindByID(Convert.ToUInt64(id));
				if (player == null) continue;

				InitTimer(player);
            }
        }

		private void HasCountDown(BasePlayer player)
		{
			
			if(!noclan.ContainsKey(player.UserIDString))
            {
				var expiringTime = DateTime.Now.AddSeconds(_config.clanSettings.NotJoinWarnTime).ToString("yyyy/MM/dd HH:mm:ss");

				noclan.Add(player.UserIDString, expiringTime);
			}
			else
            {
				DateTime time = Convert.ToDateTime(noclan[player.UserIDString]);
				DateTime now = Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));

				TimeSpan remainingTime = time - now;
				if (remainingTime.TotalSeconds >= 0)
                {
					WarnUI(player, Lang("HasNotClan", string.Format("{0:hh\\:mm\\:ss}", remainingTime)));
				}
				else
                {
					CuiHelper.DestroyUi(player, "WarnUI");

                    switch (_config.clanSettings.NotJoinProcess)
                    {
                        case 1:
							if (!HasClan(player))
								ClanCreate(player);

							break;

						case 2:
							var cmd = _config.clanSettings.NotJoinCustom;
								cmd = cmd.Replace("$player.id", player.UserIDString);
							ConsoleSystem.Run(ConsoleSystem.Option.Server, cmd);
							break;
                    }


				}

			}
		}
		#endregion

		public string SetClanTag()
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			return new string(Enumerable.Repeat(chars, _config.clanSettings.TagSize)
			  .Select(s => s[random.Next(s.Length)]).ToArray());
		}

		private void Messages(BasePlayer player, string text) => player.SendConsoleCommand("chat.add", 2, _config.generalSettings.SteamID, $"{_config.generalSettings.Prefix} {text}");

		#endregion

		#region Config        
		private ConfigData _config;
		private class ConfigData
		{

			[JsonProperty(PropertyName = "General Settings")] public GeneralSettings generalSettings { get; set; }
			[JsonProperty(PropertyName = "Clan Settings")] public ClanSettings clanSettings { get; set; }
			public Oxide.Core.VersionNumber Version { get; set; }
		}
		public class GeneralSettings
		{
			public bool Debug { get; set; }
			public bool PluginLoadClanCheck { get; set; }
			public ulong SteamID { get; set; }
			public string Prefix { get; set; }
			public string Command { get; set; }
		}
		public class ClanSettings
		{
			[JsonProperty(PropertyName = "Tag Size (6 - ex] AFBAFE)", Order = 1)] public int TagSize { get; set; }
			[JsonProperty(PropertyName = "Server Connected Auto Create Clan (false -> Show Warn Time)", Order = 2)]  public bool AutoCreateClan { get; set; }
			[JsonProperty(PropertyName = "Not Join Warn Time", Order = 3)] public int NotJoinWarnTime { get; set; }
			[JsonProperty(PropertyName = "Not Join Process (1 - auto clan create, 2 - custom)", Order = 4)] public int NotJoinProcess { get; set; }
			[JsonProperty(PropertyName = "Not Join Custom (ex] kick $player.id reason)", Order = 5)] public string NotJoinCustom { get; set; }
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			_config = Config.ReadObject<ConfigData>();

			if (_config.Version < Version)
				UpdateConfigValues();

			Config.WriteObject(_config, true);
		}

		protected override void LoadDefaultConfig() => _config = GetBaseConfig();

		private ConfigData GetBaseConfig()
		{
			return new ConfigData
			{
				generalSettings = new GeneralSettings
				{
					Debug = false,
					PluginLoadClanCheck = false,
					SteamID = 0,
					Command = "ac",
					Prefix = "<b><size=18><color=#4F728D>Auto</color> <color=#ECBD16>Clans</color></size></b> "
				},
				clanSettings = new ClanSettings
                {
					TagSize = 6,
					AutoCreateClan = true,
					NotJoinProcess = 1,
					NotJoinWarnTime = 60,
					NotJoinCustom = "kick $player.id \"Clan Not Join\""
                },
				Version = Version
			};
		}

		protected override void SaveConfig() => Config.WriteObject(_config, true);

		private void UpdateConfigValues()
		{
			PrintWarning("Config update detected! Updating config values...");
			if(Version == new Core.VersionNumber(1, 0, 3))
            {
				_config.generalSettings.Debug = false;
				_config.generalSettings.PluginLoadClanCheck = false;
            }
			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Lang

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{ "HasNotClan", "<color=red><size=15>if the player doesnt join a clan withing <color=#00ff00>{0}</color> a clan will be created automatically</size></color>" }

			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				{ "NotExist", "<color=red>아이템 (<color=yellow>{0}</color>) 을 찾을수 없습니다. 유효한 SkinID 를 사용하십시오!</color>\n<color=red>사용법: /{1} <skinid></color>" }

			}, this, "ko");
		}

		private string Lang(string key, params object[] args)
		{
			return string.Format(lang.GetMessage(key, this), args);
		}

		#endregion

		private void WarnUI(BasePlayer player, string msg)
		{
			var container = new CuiElementContainer();
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0 0 0 0.5882353" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-174.288 173.199", OffsetMax = "226.51 306.223" }
			}, "Overlay", "WarnUI");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0.06941973 0.2373427 0.3773585 0.772549" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-199.356 48.956", OffsetMax = "201.444 66.512" }
			}, "WarnUI", "Panel_Top");

			container.Add(new CuiElement
			{
				Name = "Label_Top",
				Parent = "Panel_Top",
				Components = {
					new CuiTextComponent { Text = "Auto Clans", Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.UpperCenter, Color = "0.6132076 0.06652725 0.06652725 1" },
					new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
					new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200.401 -8.779", OffsetMax = "200.399 8.778" }
				}
			});

			container.Add(new CuiElement
			{
				Name = "WarnUI_Text",
				Parent = "WarnUI",
				Components = {
					new CuiTextComponent { Text = msg, Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
					new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
					new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-194.132 -60.577", OffsetMax = "194.132 41.512" }
				}
			});

			CuiHelper.DestroyUi(player, "WarnUI");
			CuiHelper.AddUi(player, container);
		}
	}
}
