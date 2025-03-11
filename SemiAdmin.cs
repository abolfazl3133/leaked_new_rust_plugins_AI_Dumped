using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SemiAdmin", "MaltrzD", "0.0.2")]
    class SemiAdmin : RustPlugin
    {
        private static SemiAdmin _ins;
        private ConfigData _config;

        #region [ OXIDE HOOK ] 
        private void Loaded()
        {
            ReadConfig();

            _ins = this;

            PermissionService.RegisterPermissions(_config.SemiAdminCmds.Select(x => x.Permission).ToList());

            foreach (var sac in _config.SemiAdminCmds)
            {
                cmd.AddChatCommand(sac.ChatCmd, this, SemiAdminCommand_ChatCommand);
            }
        }
        #endregion

        private void SemiAdminCommand_ChatCommand(BasePlayer player, string cmd, string[] args)
        {
            SemiAdminCmd sac = _config.SemiAdminCmds.Where(x => x.ChatCmd == cmd).FirstOrDefault();
            if (string.IsNullOrEmpty(cmd) == false)
            {
                if (PermissionService.HasPermission(player.UserIDString, sac.Permission) == false)
                {
                    player.ChatMessage("У вас нет прав чтобы использовать эту команду!");
                    return;
                }

                string argsToString = string.Empty;
                foreach (var arg in args)
                {
                    argsToString += arg + " ";
                }
             
                if (player.IsAdmin == false)
                {
                    SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);

                    player.Command($"{sac.ConsoleCmd}", args);

                    SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);
                }
                else
                {
                    player.Command($"{sac.ConsoleCmd} \"{argsToString}\"");
                }
            }
        }

        #region [ EXT ]
        public static class PermissionService
        {
            public static void RegisterPermissions(List<string> perms)
            {
                foreach (var perm in perms)
                    if (_ins.permission.PermissionExists(perm, _ins) == false)
                        _ins.permission.RegisterPermission(perm, _ins);
            }
            public static void RegisterPermission(string perm)
            {
                if (_ins.permission.PermissionExists(perm, _ins) == false)
                    _ins.permission.RegisterPermission(perm, _ins);
            }
            public static bool HasPermission(string uid, string perm)
                => _ins.permission.UserHasPermission(uid, perm);
        }
        private void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b) 
        { 
            if (b)
            {
                if (player.HasPlayerFlag(f)) return;

                player.playerFlags |= f;
            } 
            else 
            {
                if (!player.HasPlayerFlag(f)) return; 

                player.playerFlags &= ~f; 
            } 

            player.SendNetworkUpdateImmediate(false);
        }
        #endregion

        #region [ CONFIG ]
        class ConfigData
        {
            public List<SemiAdminCmd> SemiAdminCmds = new List<SemiAdminCmd>()
            {
                new SemiAdminCmd(),
                new SemiAdminCmd() { ChatCmd = "respawn", ConsoleCmd = "respawn", Permission = "semiadmin.respawn" },
                new SemiAdminCmd() { ChatCmd = "dcamera", ConsoleCmd = "debugcamera", Permission = "semiadmin.debugcamera" },
                new SemiAdminCmd() { ChatCmd = "noclip", ConsoleCmd = "noclip", Permission = "semiadmin.noclip" },
            };
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();

            SaveConfig(config);
        }
        void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }
        void ReadConfig()
        {
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            _config = Config.ReadObject<ConfigData>();
            SaveConfig(_config);
        }
        #endregion
        public class SemiAdminCmd
        {
            [JsonProperty("Консольная команда")]
            public string ConsoleCmd { get; set; } = "spectate";
            [JsonProperty("Чат команда")]
            public string ChatCmd { get; set; } = "spectate";
            [JsonProperty("Право на использование чат команды")]
            public string Permission { get; set; } = "semiadmin.spectate";
        }
    }
}
