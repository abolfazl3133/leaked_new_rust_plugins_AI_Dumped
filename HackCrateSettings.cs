using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
 using UnityEngine;

 namespace Oxide.Plugins
{
    [Info("HackCrateSettings", "CRACED", "1.0.2")]
    public class HackCrateSettings : RustPlugin
    {
        #region cfg
        private ConfigData cfg { get; set; }

        private class ConfigData
        {
            
            [JsonProperty("Время открытия ящика в сек(900 стандарт)")] public Dictionary<string, float> HackTimeList = new Dictionary<string, float>();
            
            [JsonProperty("Лутать ящик может только тот кто начал взлом?")]
            public bool hackOwner = false;
            
            [JsonProperty("Если включен параметр выше. Друзья могут лутать?(Только SoFriends)")]
            public bool friendsLoot = false;
            [JsonProperty("Если включен параметр выше. Люди из зеленой команды могут лутать?")]
            public bool teamLoot = false;
            public static ConfigData GetNewConf() 
            {
                var newConfig = new ConfigData();
                newConfig.HackTimeList = new Dictionary<string, float>()
                {
                    ["hackcratesettings.default"] = 500,
                    ["hackcratesettings.vip"] = 100,
                    ["hackcratesettings.prem"] = 25,
                };
                return newConfig; 
            }
        } 

        protected override void LoadDefaultConfig() => cfg = ConfigData.GetNewConf();
        protected override void SaveConfig() => Config.WriteObject(cfg);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        void OnEntityKill(HackableLockedCrate entity)
        {
            if (hackList.ContainsKey(entity.net.ID.Value))
                hackList.Remove(entity.net.ID.Value);
        }
        #endregion
        Dictionary<ulong, ulong> hackList = new Dictionary<ulong, ulong>();
        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null) return null;
            crate.hackSeconds = 900 - GetTime(player.userID.Get());
            if(cfg.hackOwner) if(!hackList.ContainsKey(crate.net.ID.Value)) hackList.Add(crate.net.ID.Value, player.userID.Get());
            return null;
        }  
        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null) return null;
            if (!cfg.hackOwner) return null;
            ulong ownerId;
            if (container.GetEntity() == null) return null;
            if (!hackList.TryGetValue(container.GetEntity().net.ID.Value, out ownerId)) return null;
            var owner = BasePlayer.FindByID(ownerId);
            if(owner == null || !owner.IsConnected)
            {
                hackList.Remove(container.GetEntity().net.ID.Value);
                return null;
            }
            if (cfg.friendsLoot && IsFriends(owner.userID.Get(), player.userID.Get())) return null;
            if (cfg.teamLoot && owner.Team != null && owner.Team.members.Contains(player.userID.Get())) return null;
            if (owner.userID.Get() != player.userID.Get())
            {
                SendReply(player, "Вы не можете залутать данный ящик!");
                return false;
            }
            return null;
        }
        private float GetTime(ulong uid)
        {
            float min = 900;
            foreach (var privilege in cfg.HackTimeList) if (permission.UserHasPermission(uid.ToString(), privilege.Key)) min = Mathf.Min(min, privilege.Value);
            return min;
        }
        private void OnServerInitialized()
        {
            foreach (var perm in cfg.HackTimeList)
            {
                if(!permission.PermissionExists(perm.Key)) permission.RegisterPermission(perm.Key, this);
            }
        }

        [PluginReference] public Plugin SoFriends;
        private bool IsFriends(ulong owner, ulong player)
        {
            if (SoFriends)
                return (bool) SoFriends.CallHook("IsFriend", player, owner);
            return false;
        }
    }
}
