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
*  Codefling plugin page: https://codefling.com/plugins/twigs-decay
*  Codefling license: https://codefling.com/plugins/twigs-decay?tab=downloads_field_4
*
*  Copyright © 2024 IIIaKa
*/

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Facepunch;

namespace Oxide.Plugins
{
	[Info("TwigsDecay", "IIIaKa", "0.1.5")]
	[Description("This plugin is designed for the forced decay of Building Blocks with Twigs grade on PvE servers, specifically tailored to integrate with the RealPVE plugin.")]
	class TwigsDecay : RustPlugin
	{
		#region ~Configuration~
        private static Configuration _config;
		
		private class Configuration
        {
			[JsonProperty(PropertyName = "Chat command")]
			public string Command = "twig";
			
			[JsonProperty(PropertyName = "Is it worth enabling GameTips for messages?")]
			public bool GameTips_Enabled = true;
			
			[JsonProperty(PropertyName = "GameTips message style type - Blue_Normal(0), Red_Normal(1), Blue_Long(2), Blue_Short(3), Server_Event(4), Error(5).")]
            public int GameTips_Type = -1;
			
			[JsonProperty(PropertyName = "Is it worth upgrading Building Blocks from Twigs grade to Wood grade during plugin initialization if the players are offline?")]
			public bool ForceUpgrade = false;
			
			[JsonProperty(PropertyName = "Is it worth forcing the upgrade to apply only to undamaged blocks? This is an addition to the setting above.")]
            public bool ForceUpgrade_Undamaged = true;
			
			[JsonProperty(PropertyName = "Is it worth disallowing the repair of Building Blocks with Twigs grade?")]
			public bool DisallowRepair = true;
			
			[JsonProperty(PropertyName = "The interval, in seconds, at which damage is inflicted on the building.")]
			public float InvokeTime = 60f;
			
			[JsonProperty(PropertyName = "The periodic damage inflicted. Ranges from 0 to 10. Set to 0 to disable.")]
			public float InvokeDamage = 1f;
			
			[JsonProperty(PropertyName = "Is it worth enabling the tracking list? If it's disabled, it will track all types of building blocks without needing to check the list each time.")]
            public bool TrackList_Enabled = false;
			
			[JsonProperty(PropertyName = "List of tracked building block types. Leave empty or null to return the default list.")]
            public List<string> TrackList = new List<string>();
			
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
                PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}...");
                _config.Version = Version;
                PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
            }
			
			if (_config.GameTips_Type < (int)GameTip.Styles.Blue_Normal || _config.GameTips_Type > (int)GameTip.Styles.Error)
				_config.GameTips_Type = (int)GameTip.Styles.Error;
			if (_config.InvokeDamage != 0f)
				_config.InvokeDamage = Mathf.Clamp(_config.InvokeDamage, float.Epsilon, 10f);
			if (_config.TrackList == null || !_config.TrackList.Any())
				_config.TrackList = new List<string>() { "foundation", "foundation.triangle", "foundation.steps", "ramp", "floor", "floor.triangle", "floor.frame", "floor.triangle.frame", "wall", "wall.doorway", "wall.window", "wall.frame", "wall.half", "wall.low", "block.stair.ushape", "block.stair.lshape", "block.stair.spiral", "block.stair.spiral.triangle", "roof", "roof.triangle" };
			
			SaveConfig();
		}
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
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
				["MsgWarningEnabled"] = "Forced decay warnings are enabled!",
				["MsgWarningDisabled"] = "Forced decay warnings are disabled!",
				["MsgOnTwigsPlace"] = "On this server, forced decay of Building Blocks with Twigs grade is enabled!\n<size=12><color=#9A9A9A>To toggle notifications, use </color><color=#BBBBBB>/{0} warn</color></size>",
				["MsgOnTwigsRepair"] = "Repairing of Building Blocks with Twigs grade is prohibited!\n<size=12><color=#9A9A9A>To toggle notifications, use </color><color=#BBBBBB>/{0} warn</color></size>"
            }, this);
			lang.RegisterMessages(new Dictionary<string, string>
            {
				["MsgWarningEnabled"] = "Предупреждения принудительного гниения включены!",
				["MsgWarningDisabled"] = "Предупреждения принудительного гниения выключены!",
				["MsgOnTwigsPlace"] = "На данном сервере включено принудительное гниение построек из соломы!\n<size=12><color=#9A9A9A>Чтобы включить или выключить уведомления, введите </color><color=#BBBBBB>/{0} warn</color></size>",
				["MsgOnTwigsRepair"] = "Ремонт построек из соломы запрещен!\n<size=12><color=#9A9A9A>Чтобы включить или выключить уведомления, введите </color><color=#BBBBBB>/{0} warn</color></size>"
            }, this, "ru");
		}
		#endregion

		#region ~Oxide Hooks~
		void OnEntitySpawned(BuildingBlock block)
		{
			if (!block.OwnerID.IsSteamId() || !IsRightBlock(block)) return;
			NextTick(() =>
            {
				if (block == null || block.gameObject == null || block.grade != BuildingGrade.Enum.Twigs) return;
				var player = BasePlayer.FindByID(block.OwnerID);
                if (player != null && !player.IsNpc && player.IsConnected && (!_storedData.PlayersList.TryGetValue(player.userID, out var canShow) || canShow))
                {
					if (_config.GameTips_Enabled)
                        player.IPlayer.Command("gametip.showtoast", _config.GameTips_Type, string.Format(lang.GetMessage("MsgOnTwigsPlace", this, player.UserIDString), _config.Command), string.Empty);
                    else
                        player.IPlayer.Reply(string.Format(lang.GetMessage("MsgOnTwigsPlace", this, player.UserIDString), _config.Command));
				}
                InitBlockDecay(block);
			});
		}
		
		object OnStructureRepair(BuildingBlock block, BasePlayer player)
		{
			if (!IsRightBlock(block)) return null;
			if (!_storedData.PlayersList.TryGetValue(player.userID, out var canShow) || canShow)
			{
				if (_config.GameTips_Enabled)
					player.IPlayer.Command("gametip.showtoast", _config.GameTips_Type, string.Format(lang.GetMessage("MsgOnTwigsRepair", this, player.UserIDString), _config.Command), string.Empty);
				else
					player.IPlayer.Reply(string.Format(lang.GetMessage("MsgOnTwigsRepair", this, player.UserIDString), _config.Command));
			}
			return false;
		}
		
		object OnDecayHeal(BuildingBlock block) => !IsRightBlock(block) ? null : false;
		
		void Init()
		{
			Unsubscribe(nameof(OnEntitySpawned));
			Unsubscribe(nameof(OnStructureRepair));
			Unsubscribe(nameof(OnDecayHeal));
			AddCovalenceCommand(_config.Command, nameof(TwigsDecay_Command));
			_storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
		}
		
		void OnServerInitialized()
        {
            if (_config.InvokeDamage > 0f)
            {
                if (_config.ForceUpgrade)
                {
					var players = Pool.Get<List<ulong>>();
                    foreach (var player in BasePlayer.activePlayerList)
                        players.Add(player.userID);
                    foreach (var entity in BaseNetworkable.serverEntities)
                    {
                        if (entity is not BuildingBlock block || !block.OwnerID.IsSteamId() || !IsRightBlock(block)) continue;
                        if (players.Contains(block.OwnerID) || (_config.ForceUpgrade_Undamaged && block.health < block.MaxHealth()))
                            InitBlockDecay(block);
                        else
                        {
                            block.SetGrade(BuildingGrade.Enum.Wood);
                            block.SetHealthToMax();
                            block.SendNetworkUpdate();
                            block.UpdateSkin();
                            block.ResetUpkeepTime();
                            block.GetBuilding()?.Dirty();
                        }
                    }
                    Pool.FreeUnmanaged(ref players);
                }
                else
                {
                    foreach (var entity in BaseNetworkable.serverEntities)
                    {
                        if (entity is BuildingBlock block && block.OwnerID.IsSteamId() && IsRightBlock(block))
                            InitBlockDecay(block);
                    }
                }
                Subscribe(nameof(OnEntitySpawned));
                if (_config.DisallowRepair)
                {
                    Subscribe(nameof(OnStructureRepair));
                    Subscribe(nameof(OnDecayHeal));
                }
            }
        }
		
		void Unload()
		{
			if (_config.InvokeDamage > 0f)
			{
				foreach (var entity in BaseNetworkable.serverEntities)
				{
					if (entity is not BuildingBlock block || block.grade != BuildingGrade.Enum.Twigs) continue;
					var antiTwigs = block.gameObject.GetComponent<AntiTwigs>();
					if (antiTwigs != null)
						UnityEngine.Object.DestroyImmediate(antiTwigs);
				}
            }
			_config = null;
			_storedData = null;
		}
        #endregion
		
		#region ~Commands~
        private void TwigsDecay_Command(IPlayer player, string command, string[] args)
        {
            if (args != null && args.Length > 0)
            {
                string replyKey = string.Empty;
                string[] replyArgs = new string[5];
                bool isWarning = false;
                if (args[0] == "warn")
                {
                    if (player.Object is BasePlayer bPlayer)
                    {
                        if (!_storedData.PlayersList.ContainsKey(bPlayer.userID))
                            _storedData.PlayersList[bPlayer.userID] = true;
                        _storedData.PlayersList[bPlayer.userID] = !_storedData.PlayersList[bPlayer.userID];
						if (_storedData.PlayersList[bPlayer.userID])
							replyKey = "MsgWarningEnabled";
						else
						{
							replyKey = "MsgWarningDisabled";
							isWarning = true;
						}
					}
                }
				
				if (!string.IsNullOrWhiteSpace(replyKey))
                {
                    if (!player.IsServer && _config.GameTips_Enabled)
                        player.Command("gametip.showtoast", (int)(isWarning ? GameTip.Styles.Error : GameTip.Styles.Blue_Normal), string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs), string.Empty);
                    else
                        player.Reply(string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs));
                }
            }
        }
        #endregion

        #region ~AntiTwigs~
		private void InitBlockDecay(BuildingBlock block) => block.gameObject.AddComponent<AntiTwigs>();
		private static bool IsRightBlock(BuildingBlock block) => block.grade == BuildingGrade.Enum.Twigs && (!_config.TrackList_Enabled || _config.TrackList.Contains(block.ShortPrefabName));
		
		public class AntiTwigs : MonoBehaviour
		{
			private BuildingBlock _block { get; set; }
			private float _damage { get; set; }

			private void Awake()
			{
				var block = GetComponentInParent<BuildingBlock>();
				if (block != null && block.grade == BuildingGrade.Enum.Twigs)
				{
					_damage = _config.InvokeDamage;
					_block = block;
					block.InvokeRepeating(DamageBuilding, _config.InvokeTime, _config.InvokeTime);
				}
				else
					Destroy(this);
			}

			public void DamageBuilding()
			{
				if (_block != null && !_block.IsDestroyed && _block.grade == BuildingGrade.Enum.Twigs)
				{
					_block.health -= _damage;
					if (_block.health <= 0f)
						_block.Kill();
					else
						_block.SendNetworkUpdate();
				}
				else
					Destroy(this);
			}
			
			private void OnDestroy()
			{
				if (_block != null)
					_block.CancelInvoke(DamageBuilding);
			}
		}
		#endregion
	}
}