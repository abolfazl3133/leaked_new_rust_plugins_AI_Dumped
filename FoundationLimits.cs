using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("FoundationLimits", "AhigaO#4485", "1.1.2")]
    internal class FoundationLimits : RustPlugin
    {
        #region Static

        private Configuration _config;
        private Dictionary<string, int> data = new Dictionary<string, int>();
        [PluginReference] private Plugin Clans;

        #endregion

        #region Config
 
        private class Configuration
        {
            [JsonProperty(PropertyName = "SteamID for icon in chat messages")]
            public ulong iconID = 0;
            
            [JsonProperty(PropertyName = "Command for check limit")]
            public string command = "flimit";
            
            [JsonProperty(PropertyName = "Additional foundations for each player (Percentage)")]
            public float percent = 0.05f;
            
            [JsonProperty(PropertyName = "Permission - Limit for foundations(Should go from standard to best)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> limits = new Dictionary<string, int>
            {
                ["foundationlimits.default"] = 50, 
                ["foundationlimits.vip"] = 100,
                ["foundationlimits.premium"] = 200
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
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

        #region OxideHooks

        private void OnServerInitialized()
        {
            cmd.AddChatCommand(_config.command, this, nameof(cmdChatlimit));
            
            foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingBlock>())
                if (check.PrefabName.Contains("foundation") && !check.PrefabName.Contains("steps") && check.OwnerID.IsSteamId() && !data.TryAdd(check.OwnerID.ToString(), 1))
                    ++data[check.OwnerID.ToString()];

            foreach (var check in _config.limits) 
                permission.RegisterPermission(check.Key, this);
            foreach (var check in BasePlayer.activePlayerList) 
                OnPlayerConnected(check);
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || data.ContainsKey(player.UserIDString)) return;
            if (!permission.UserHasPermission(player.UserIDString, _config.limits.First().Key)) permission.GrantUserPermission(player.UserIDString, _config.limits.First().Key, this);
            data.Add(player.UserIDString, 0);
        }
   
        private void OnEntitySpawned(BuildingBlock component)
        {
            var block = component?.ShortPrefabName;
            if (!block.Contains("foundation") || block.Contains("steps")) return;
            var userID = component.OwnerID;
            NextTick(() =>
            {
                if (!userID.IsSteamId()) return;
                var limits = GetLimit(userID.ToString());
                if (limits[0] >= limits[1])
                {
                    var player = BasePlayer.FindByID(userID);
                    if (player != null) SendMessage(player, "CM_LIMIT", _config.command);
                    component.Kill(BaseNetworkable.DestroyMode.Gib);
                }

                data[userID.ToString()]++; 
            });
        }
        
        private void OnEntityKill(BuildingBlock block)
        {
            if (block == null || !block.PrefabName.Contains("foundation") || block.PrefabName.Contains("steps") || !data.ContainsKey(block.OwnerID.ToString())) return;
            data[block.OwnerID.ToString()]--;
        }

        #endregion

        #region Commands

        [ConsoleCommand("playerflimit")]
        private void cmdConsoleplayerflimit(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var userID = arg.GetString(0);
            if (!userID.IsSteamId())
            {
                PrintError("Correct command is - playerflimit steamid");
                return;
            } 

            int currentEntities; 
            if (!data.TryGetValue(userID, out currentEntities))
            {
                foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingBlock>())
                    if (check.PrefabName.Contains("foundation") && !check.PrefabName.Contains("steps") && check.OwnerID.ToString() == userID)
                        ++currentEntities;

                if (currentEntities == 0)
                {
                    PrintError("This player doesn't exist or he didn't build anything");
                    return;
                }
            }
            
            PrintWarning($"Player limit: {currentEntities}/{GetMaxLimit(userID)}");
        }
        
        [ChatCommand("playerflimit")]
        private void cmdChatplayerflimit(BasePlayer player, string command, string[] args)
        {
            if (player == null || args.Length != 1) return;
            
            if (!player.IsAdmin)
            {
                SendReply(player, "You are not admin");
                return;
            }
            
            var userID = args[0];
            if (!userID.IsSteamId())
            {
                PrintError("Correct command is - playerflimit steamid");
                return;
            } 
 
            int currentEntities; 
            if (!data.TryGetValue(userID, out currentEntities))
            {
                foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingBlock>())
                    if (check.PrefabName.Contains("foundation") && !check.PrefabName.Contains("steps") && check.OwnerID.ToString() == userID)
                        ++currentEntities;

                if (currentEntities == 0)
                {
                    PrintError("This player doesn't exist or he didn't build anything");
                    return;
                }
            } 
            
            SendReply(player, $"Player limit: {currentEntities}/{GetMaxLimit(userID)}");
            
        }
        
        private void cmdChatlimit(BasePlayer player, string command, string[] args)
        {
            var limits = GetLimit(player.UserIDString);
            SendMessage(player, limits[0] >= limits[1] ? "CM_LIMITFULL" : "CM_NOTFULLLIMIT", limits[0].ToString(), limits[1].ToString());
        }

        #endregion

        #region Functions

        private List<int> GetLimit(string id)
        {
            var list = new List<int>();
            var members = Clans?.Call<List<string>>("GetClanMembers", id);
            var currentLimit = 0;
            var maxLimit = 0;
            if (!permission.UserHasPermission(id, _config.limits.First().Key)) permission.GrantUserPermission(id, _config.limits.First().Key, this);
            data.TryAdd(id, 0);
            if (members == null || members.Count <= 1)
            {
                currentLimit = data[id];
                maxLimit = GetMaxLimit(id);
            }
            else
            {
                foreach (var check in members)
                {
                    if (!data.ContainsKey(check))
                    {
                        data.Add(check, 0);
                        permission.GrantUserPermission(check, _config.limits.First().Key, this);
                    }
                    currentLimit += data[check];
                    maxLimit += GetMaxLimit(check);
                }

                if (members.Count > 1) maxLimit += (int)(_config.percent * members.Count * maxLimit);
            }
            list.Add(currentLimit);
            list.Add(maxLimit);
            return list;
        }

        private int GetMaxLimit(string id)
        {
            var maxLimit = 0;
            foreach (var check in _config.limits)
                if (permission.UserHasPermission(id, check.Key)) maxLimit = check.Value;
            return maxLimit;
        }

        #endregion

        #region Language
        private void SendMessage(BasePlayer player, string name, params object[] args) => Player.Message(player, GetMsg(player.UserIDString, name, args), _config.iconID);

        private string GetMsg(string player, string msg, params object[] args) => string.Format(lang.GetMessage(msg, this, player), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CM_LIMIT"] = "You have exceeded the limit for building foundations (type /{0})",
                ["CM_LIMITFULL"] = "Your limit for foundations: <color=red>{0}/{1}</color>",
                ["CM_NOTFULLLIMIT"] = "Your limit for foundations: <color=green>{0}/{1}</color>"
            }, this);
        }
        
        #endregion
    }
}