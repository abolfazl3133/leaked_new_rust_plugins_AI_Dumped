using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Text;

namespace Oxide.Plugins
{
    [Info("CovertAdmin", "https://discord.gg/TrJ7jnS233", "1.0.9")]
    [Description("Go fully undercover and disguise yourself as another player")]
    internal class CovertAdmin : RustPlugin
    {

        // Maintained by Colon Blow

        #region Load

        [PluginReference] private Plugin BetterChat;

        private bool Changed;

        private Dictionary<string, __CovertInfo> covertPlayerList = new Dictionary<string, __CovertInfo>();

        private class __CovertInfo
        {
            public ulong __covertID;
            public string __covertName;
            public BasePlayer __player;
            public string __restoreName;
            public ulong __restoreID;
        }

        private string covertNameColor = "#54A7FB";
        private const string __permissionName = "covertadmin.use";

        private Dictionary<string, object> covertNameList = new Dictionary<string, object>();

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(__permissionName, this);
        }

        #endregion

        #region Configuration

        private string covertTags = "";
        private string covertTextSize = "15";
        private bool disableOnLogoff = true;
        private bool disableAdminAbilities = false;

        private static Dictionary<string, object> __RandomPlayerNames()
        {
            var __x = new Dictionary<string, object> { { "RandomPlayerName1", "76561198381967577" }, { "RandomPlayerName2", "76561198381967577" } };
            return __x;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var __data = Config[menu] as Dictionary<string, object>;
            if (__data == null)
            {
                __data = new Dictionary<string, object>();
                Config[menu] = __data;
                Changed = true;
            }
            object __value;
            if (__data.TryGetValue(datavalue, out __value)) return __value;
            __value = defaultValue;
            __data[datavalue] = __value;
            Changed = true;
            return __value;
        }

        private void LoadVariables()
        {
            covertNameList = (Dictionary<string, object>)GetConfig("Settings", "Covert Name and SteamID(for profile pic)", __RandomPlayerNames());
            covertTags = Convert.ToString(GetConfig("Settings", "Tags", ""));
            covertNameColor = Convert.ToString(GetConfig("Settings", "Covert Name Colour", "#54A7FB"));
            covertTextSize = Convert.ToString(GetConfig("Settings", "Message Size", "15"));
            disableOnLogoff = Convert.ToBoolean(GetConfig("Settings", "Disable On Logout", true));
            disableAdminAbilities = Convert.ToBoolean(GetConfig("Settings", "Disable Admin Abilities", false));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // chat
                ["format"] = "<size={0}>{1} <color={2}>{3}</color>: {4}</size>",
                ["No Permission"] = "You cannot use this command",
                ["configerror"] = "Error in config, cannot read SteamID, Please check config for errors.",
                ["inputerror"] = "Error processing SteamID, please try again.",
                ["Covert Disabled"] = "Covert mode disabled",
                ["Covert Enabled"] = "Covert mode enabled! You will now appear under the name of \"{0}\"",
                ["Already In Covert"] = "You are already in covert mode",
                ["Login Warning"] = "Your covert mode is still activated! type /covert to disable it"
                // [0] = size
                // [1] = tags
                // [2] = colour
                // [3] = name
                // [4] = text
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("covertstatus")]
        private void CovertStatusCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, __permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (covertPlayerList.ContainsKey(player.UserIDString))
            {
                StringBuilder __sb = new StringBuilder();
                __sb.AppendLine("Current covert status:");
                __sb.AppendLine($"Display Name: {covertPlayerList[player.UserIDString].__covertName}");
                __sb.AppendLine($"Covert ID: {covertPlayerList[player.UserIDString].__covertID}");
                player.ChatMessage(__sb.ToString().TrimEnd());
            }
            else
                player.ChatMessage("Covert mode is currently disabled");
        }

        [ChatCommand("covert")]
        private void CovertCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, __permissionName)) { player.ChatMessage(msg("No Permission", player.UserIDString)); return; }

            switch (args.Length)
            {
                case 0:
                    if (covertPlayerList.ContainsKey(player.UserIDString))
                    {
                        __RenamePlayer(player, covertPlayerList[player.UserIDString].__restoreName, false);
                        covertPlayerList.Remove(player.UserIDString);
                        player.ChatMessage(msg("Covert Disabled", player.UserIDString));
                    }
                    else
                    {
                        var nameList = covertNameList.Keys.ToList();
                        var newCovertName = nameList[Convert.ToInt16(Mathf.Round(Random.Range(0f, Convert.ToSingle(covertNameList.Count) - 1f)))];
                        var newCovertID = new ulong();
                        if (UInt64.TryParse(covertNameList[newCovertName].ToString(), out newCovertID))
                        {
                            covertPlayerList.Add(player.UserIDString, new __CovertInfo { __covertName = newCovertName, __restoreName = player.displayName, __covertID = newCovertID, __player = player, __restoreID = player.userID });
                            __RenamePlayer(player, newCovertName, true);
                            player.ChatMessage(string.Format(msg("Covert Enabled", player.UserIDString), newCovertName));
                            return;
                        }
                        player.ChatMessage(msg("configerror", player.UserIDString));
                    }
                    break;
                case 1:
                    if (covertPlayerList.ContainsKey(player.UserIDString))
                    {
                        player.ChatMessage(msg("Already In Covert", player.UserIDString));
                        return;
                    }
                    covertPlayerList.Add(player.UserIDString, new __CovertInfo { __covertName = args[0], __restoreName = player.displayName, __covertID = 76561198136204161, __player = player, __restoreID = player.userID });
                    __RenamePlayer(player, args[0], true);
                    player.ChatMessage(string.Format(msg("Covert Enabled", player.UserIDString), args[0]));
                    break;
                case 2:
                    if (covertPlayerList.ContainsKey(player.UserIDString))
                    {
                        player.ChatMessage(msg("Already In Covert", player.UserIDString));
                        return;
                    }
                    var typedCovertID = new UInt64();
                    if (UInt64.TryParse(args[1].ToString(), out typedCovertID))
                    {
                        covertPlayerList.Add(player.UserIDString, new __CovertInfo { __covertName = args[0], __restoreName = player.displayName, __covertID = typedCovertID, __player = player, __restoreID = player.userID });
                        __RenamePlayer(player, args[0], true);
                        player.ChatMessage(string.Format(msg("Covert Enabled", player.UserIDString), args[0]));
                        return;
                    }
                    player.ChatMessage(msg("inputerror", player.UserIDString));
                    break;
            }
        }

        #endregion

        #region Hooks

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!covertPlayerList.ContainsKey(player.UserIDString))
                return;
            if (disableOnLogoff)
            {
                __RenamePlayer(player, covertPlayerList[player.UserIDString].__restoreName, false);
                covertPlayerList.Remove(player.UserIDString);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (disableOnLogoff)
                return;
            if (!covertPlayerList.ContainsKey(player.UserIDString))
                return;
            __RenamePlayer(player, covertPlayerList[player.UserIDString].__covertName, true);
            player.ChatMessage(msg("Login Warning", player.UserIDString));
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (hitInfo == null || hitInfo.Initiator == null) return;
            BasePlayer __attacker = hitInfo.Initiator.GetComponent<BasePlayer>();
            if (!__attacker) return;
            if (covertPlayerList.ContainsKey(__attacker.UserIDString))
            {
                __attacker.userID = covertPlayerList[__attacker.UserIDString].__covertID;
                timer.Once(0.2f, () => { if (__attacker != null) __attacker.userID = covertPlayerList[__attacker.UserIDString].__restoreID; });
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (covertPlayerList.ContainsKey(player.UserIDString))
                __RenamePlayer(player, covertPlayerList[player.UserIDString].__covertName, true);
        }

        private void __RenamePlayer(BasePlayer player, string __name, bool __startingCovert)
        {
            player.displayName = __name;
            IPlayer _player = covalence.Players.FindPlayerById(player.UserIDString);
            _player.Rename(__name);
            if (disableAdminAbilities)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, !__startingCovert);
            player.SendNetworkUpdateImmediate();
        }

        private object OnBetterChat(Dictionary<string, object> __data)
        {
            var player = __data["Player"] as IPlayer;
            if (player == null)
                return null;
            if (!covertPlayerList.ContainsKey(player.Id))
                return null;
            rust.BroadcastChat(null, string.Format(msg("format"), covertTextSize, covertTags, covertNameColor, player.Name, __data["Message"]), covertPlayerList[player.Id].__covertID.ToString()); /*  76561198136204161 */
            return false;
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var __player = arg.Player();
            if (__player == null)
                return null;
            if (!covertPlayerList.ContainsKey(__player.UserIDString)) return null;
            __player.userID = covertPlayerList[__player.UserIDString].__covertID;
            timer.Once(0.2f, () =>
            {
                if (!__player) return;
                if (covertPlayerList.ContainsKey(__player.UserIDString))
                    __player.userID = covertPlayerList[__player.UserIDString].__restoreID;
            });
            return true;
        }

        private object OnUserChat(IPlayer __player, string __message)
        {
            if (covertPlayerList.ContainsKey(__player.Id))
            {
                if (!BetterChat)
                    rust.BroadcastChat(null, string.Format(msg("format"), covertTextSize, covertTags, covertNameColor, __player.Name, __message, covertPlayerList[__player.Id].__covertID.ToString()));
                return true;
            }
            return null;
        }

        private void Unload()
        {
            foreach (var entry in covertPlayerList)
                if (entry.Value.__player.IsConnected)
                    __RenamePlayer(entry.Value.__player, entry.Value.__restoreName, false);
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        #endregion
    }
}