using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("ZComandRplaser", "Zwe1st", "1.0.0")]
    [Description("Замена команд в чате")]
    public class ZComandRplaser : RustPlugin
    {
        private ConfigData configData;

        class ConfigData
        {
            public bool Enabled { get; set; }
            public string OldCommand { get; set; }
            public string NewCommand { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                Enabled = true,
                OldCommand = ".лше",
                NewCommand = "/kit"
            };
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        private void OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (!configData.Enabled) return;

            if (message.Contains(configData.OldCommand))
            {
                string newMessage = message.Replace(configData.OldCommand, configData.NewCommand);
                ConsoleNetwork.SendClientCommand(player.Connection, "chat.say", new object[] { newMessage });
                return;
            }
        }

        private void Init()
        {
            permission.RegisterPermission("chatreplacer.use", this);
            LoadConfig();
        }
    }
} 