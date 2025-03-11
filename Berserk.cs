using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Berserk", "@vorvzak0ne_777", "1.0.0")]
    public class Berserk : RustPlugin
    {
        private ConfigData configData;

        private class ConfigData
        {
            public float WoundDuration { get; set; } = 5f;
            public string MessageText { get; set; } = "Ты встал !";
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig();
        }
 
        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData, true);
        }

        void Init()
        {
            permission.RegisterPermission("Berserk.use", this);
        }

        void OnPlayerWound(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "Berserk.use"))
            {
                return;
            }

            CountdownWound(player, configData.WoundDuration);
        }

        private void CountdownWound(BasePlayer player, float duration)
        {
            for (int i = (int)duration; i > 0; i--)
            {
                int secondsLeft = i;
                timer.Once(duration - secondsLeft, () =>
                {
                    if (player.IsWounded())
                    {
                        player.ChatMessage($"Осталось {secondsLeft} сек");
                    }
                });
            }

            timer.Once(duration, () =>
            {
                if (player.IsWounded())
                {
                    player.StopWounded();
                    player.ChatMessage(configData.MessageText);
                }
            });
        }
    }
}
