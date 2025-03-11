using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Dance", "senyaa", "1.1.0")]
    [Description("This plugin allows players to dance, even if they don't own a VoiceProps DLC")]
    class Dance : RustPlugin
    {
        #region Configuration
        private class PluginConfig
        {
            public uint[] gestureIds;
			public bool enableFourthDance;
        }
        PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                gestureIds = new uint[] {478760625, 1855420636, 1702547860, 834887525},
				enableFourthDance = false
            };
        }

        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notAllowed"] = "You are not allowed to use this command",
                ["usage"] = "Usage: /dance 1/2/3"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notAllowed"] = "У вас нет доступа к этой команде",
                ["usage"] = "Используйте: /dance 1/2/3"
            }, this, "ru");
            
        }
        #endregion

        private void Init()
        {
            permission.RegisterPermission("dance.use", this);
            config = Config.ReadObject<PluginConfig>();
        }
		
        private bool? CanUseGesture(BasePlayer player, GestureConfig gesture)
        {
            if (config.gestureIds.Contains(gesture.gestureId) && player.IPlayer.HasPermission("dance.use"))
                return true;
            return null;
        }
		
        [ChatCommand("dance")]
        private void DanceCommand(BasePlayer player, string command, string[] args)
        {
            if(args.Length != 1 || args[0].Length != 1 || !("123"+ (config.enableFourthDance ? "4" : "")).Contains(args[0])) {
				
                player.IPlayer.Reply(lang.GetMessage("usage", this, player.IPlayer.Id) + (config.enableFourthDance ? "/4" : ""));
                return;
            }

            if (!player.IPlayer.HasPermission("dance.use"))
            {
                player.IPlayer.Reply(lang.GetMessage("notAllowed", this, player.IPlayer.Id));
                return;
            }
            
            foreach (var gesture in player.gestureList.AllGestures)
            {
                if (gesture.gestureId == config.gestureIds[Convert.ToInt64(args[0]) - 1])
                {
                    player.Server_StartGesture(gesture);
                }
            }
        }
    }
}