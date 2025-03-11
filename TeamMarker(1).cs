using System;
 using System.Collections.Generic;
 using Newtonsoft.Json;
using Oxide.Core;
 using UnityEngine;
 using VLB;

 namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("TeamMarker", "https://discord.gg/dNGbxafuJn", "1.0.3")]
    [Description("Displays marker for you and your teammates.")]
    
    class TeamMarker : RustPlugin
    {
        #region Classes

        private class PluginConfig
        {
            [JsonProperty("Activation button (Check list for valid buttons below)")] 
            public string ActivateButton;
            [JsonProperty("Marker symbol (From ASCII table: https://www.ascii-code.com)")]
            public char MarkerSymbol;
            [JsonProperty("The time how long marker is visible")]
            public int MarkerTime;
            [JsonProperty("Cooldown time")]
            public int MarkerDelay;
            [JsonProperty("Max distance to locate the marker")]
            public int MarkerDist;
            [JsonProperty("Marker color")] 
            public string MarkerColor;
            [JsonProperty("Font size of marker info")]
            public int InfoFontSize;
            [JsonProperty("Color of text")] 
            public string InfoTextColor;
            [JsonProperty("Effect prefab")] 
            public string EffectPrefab;

            [JsonIgnore] public BUTTON ActButton;
            
            [JsonProperty("Buttons list (Read only)")] 
            public string[] Buttons = Enum.GetNames(typeof(BUTTON));
        }
        
        #endregion
        
        #region Variables

        private static TeamMarker _plugin;
        
        private static PluginConfig _config;
        private const string PermissionUse = "teammarker.use";
        private Effect _effect = new Effect();

        #endregion
        
        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }

                LoadDefaultConfig();
                return;
            }

            if (_config != null)
            {
                _config.ActButton = BUTTON.FIRE_THIRD;
                Enum.TryParse(_config.ActivateButton, out _config.ActButton);
            }
            
            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("OnConfigValidate") != null)
            {
                PrintWarning("Using default configuration...");
                _config = GetDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }
        
        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig()
            {
                ActivateButton = "FIRE_THIRD",
                MarkerSymbol = '¤',
                MarkerTime = 4,
                MarkerDelay = 1,
                MarkerDist = 200,
                MarkerColor = "#cc0000",
                InfoFontSize = 12,
                InfoTextColor = "#222222",
                EffectPrefab = "assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab"
            };
        }
        
        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            _plugin = this;
            
            RegPermission(PermissionUse);

            _effect.Init(Effect.Type.Generic, Vector3.zero, Vector3.zero);
            _effect.pooledString = _config.EffectPrefab;
            
            for (var i = BasePlayer.activePlayerList.Count - 1; i >= 0; i--)
            {
                OnPlayerConnected(BasePlayer.activePlayerList[i]);
            }
        }

        void Unload()
        {
            InputController.DestroyAll();
        }
        
        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(2f, () => OnPlayerConnected(player));
                return;
            }

            if (!player.IsConnected) return;
            
            InputController.Init(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var comp = player.GetComponent<InputController>();
            if(comp != null)
                UnityEngine.Object.Destroy(comp);
        }

        #endregion
        
        #region Utils

        void PlayFx(BasePlayer player)
        {
            EffectNetwork.Send(_effect, player.net.connection);
        }

        public void RegPermission(string name)
        {
            if (permission.PermissionExists(name)) return;
            permission.RegisterPermission(name, this);
        }
        
        public bool HasPermission(BasePlayer player, string name)
        {
            if (player.IsAdmin)
                return true;
            
            return permission.UserHasPermission(player.UserIDString, name);
        }

        #endregion

        #region Scripts

        private class InputController : MonoBehaviour
        {
            public static List<InputController> InputControllers = new List<InputController>();
            private BasePlayer _player;
            private RealTimeSince _markerCooldown;
            private RealTimeSince _markerLifeTime;
            private Vector3 _lastMarkerPosition;

            public static void Init(BasePlayer player)
            {
                player.GetOrAddComponent<InputController>();
            }

            public static void DestroyAll()
            {
                for (var i = InputControllers.Count - 1; i >= 0; i--)
                {
                    Destroy(InputControllers[i]);
                }
            }
            
            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                if (_player == null)
                {
                    Destroy(this);
                    return;
                }
                
                InputControllers.Add(this);
            }

            private void Update()
            {
                if (_player.serverInput.WasJustPressed(_config.ActButton) == false) 
                    return;
                
                if (_markerCooldown < _config.MarkerDelay)
                    return;
                
                _markerCooldown = 0;

                MarkerCheck();
            }

            private void MarkerCheck()
            {
                if(_plugin.HasPermission(_player, PermissionUse) == false) 
                    return;
                
                if(IsInvoking(nameof(MarkerUpdate)))
                    CancelInvoke(nameof(MarkerUpdate));
                
                DrawMarker();
            }

            private void DrawMarker()
            {
                Ray ray = new Ray(_player.eyes.position, _player.eyes.HeadForward());
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit, _config.MarkerDist,
                    LayerMask.GetMask(new[] {"Terrain", "World", "Construction", "Player (Server)", "Deployed"})) == false) return;
                
                _lastMarkerPosition = hit.point;

                if (_lastMarkerPosition == Vector3.zero)
                    return;

                _markerLifeTime = 0;

                if (_player.currentTeam == 0)
                    _plugin.PlayFx(_player);
                else
                {
                    foreach (var member in _player.Team.GetOnlineMemberConnections())
                    {
                        if (member.player as BasePlayer != null)
                            _plugin.PlayFx(member.player as BasePlayer);
                    }
                }

                InvokeRepeating(nameof(MarkerUpdate), 0f, 0.1f);
            }

            private void MarkerUpdate()
            {
                if (_markerLifeTime >= _config.MarkerTime)
                {
                    CancelInvoke(nameof(MarkerUpdate));
                    return;
                }

                if (_player.currentTeam == 0)
                {
                    _player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    _player.SendEntityUpdate();
                    _player.SendConsoleCommand("ddraw.text", 0.1f, Color.white, _lastMarkerPosition,
                        $"<color={_config.MarkerColor}>{_config.MarkerSymbol}</color>\n<color={_config.InfoTextColor}><size={_config.InfoFontSize}>{Math.Round(Vector3.Distance(_player.transform.position, _lastMarkerPosition))} m</size></color>");
                    _player.SendConsoleCommand("camspeed 0");

                    if(_player.Connection.authLevel < 2)
                        _player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            
                    _player.SendEntityUpdate();
                }
                else
                {
                    var members = _player.Team.GetOnlineMemberConnections();

                    for (var i = members.Count - 1; i >= 0; i--)
                    {
                        var member = members[i].player as BasePlayer;

                        if (member == null)
                            continue;
                        
                        member.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        member.SendEntityUpdate();
                        member.SendConsoleCommand("ddraw.text", 0.1f, Color.white, _lastMarkerPosition,
                            $"<color={_config.MarkerColor}>{_config.MarkerSymbol}</color>\n<color={_config.InfoTextColor}><size={_config.InfoFontSize}>{Math.Round(Vector3.Distance(member.transform.position, _lastMarkerPosition))} m\n{_player.displayName}</size></color>");
                        member.SendConsoleCommand("camspeed 0");

                        if(member.Connection.authLevel < 2)
                            member.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            
                        member.SendEntityUpdate();
                    }
                }
            }
        }

        #endregion
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */