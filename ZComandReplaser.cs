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
            public Dictionary<string, string> Commands { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                Enabled = true,
                Commands = new Dictionary<string, string>
                {
                    { ".лше", "/kit" },
                    { ".рщьу", "/home" },
                    { ".ез", "/tp" }
                }
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

        private object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (!configData.Enabled) return null;

            string newMessage = message;
            bool replaced = false;

            foreach (var command in configData.Commands)
            {
                if (message.Contains(command.Key))
                {
                    newMessage = newMessage.Replace(command.Key, command.Value);
                    replaced = true;
                }
            }

            if (replaced)
            {
                NextTick(() => 
                {
                    ConsoleNetwork.SendClientCommand(player.Connection, "chat.say", new object[] { newMessage });
                });
                return false;
            }
            
            return null;
        }

        private void Init()
        {
            permission.RegisterPermission("chatreplacer.use", this);
            LoadConfig();
        }
    }
} 