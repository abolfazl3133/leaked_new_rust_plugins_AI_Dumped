/*
*  <----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of This Software without the Developer’s consent
*  
*  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: ꜰᴀᴋᴇɴɪɴᴊᴀ🔥#0001
*
*  Copyright © 2020-2023 FAKENINJA
*/

using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Oxide.Core.Database;
using Oxide.Core.Libraries;
using System.Linq;

namespace Oxide.Plugins
{
    /*
     * CHANGELOG
     * 
     *    2.1.0 - Complete Refactor and added Language support for Public release
     *    
     *    2.4.0 - Option to clear all warnings on autoban
     *    
     *    2.4.2 ->
     *           -> Discord Webhook Embed added: Issued By field showing which admin issued the warning (Requested by Shark-A-Holic)
     *           -> Fixed OnPlayerSleepEnded NRE
     *           
     *          ... version skip due to major feature releases ...
     *          
     *    2.5.0 ->
     *           -> Added new AutoMod feature to allow customizable commands to be run on X warning points.
     *           -> Added new option to not use GUI pop-up and only chat to be vanilla safe. (Requested by Papa)
     *           -> Added new option to change opacity of GUI pop-up
     *           -> Added new permission "smartwarnings.admin.canclear" to require additional permission to be able to remove warnings. (Requested by Irish)
     *          
     *    2.5.0-beta2
     *           -> Resolved NRE on server startup
     *           -> Resolved player being freezed still on multiple warnings
     *    
     *    2.5.1
     *          -> Resolved issue caused by no AutoMod options configured (Reported by Shark-A-Holic, thanks)
     *          -> Improved integration support to use DiscordMessages and additionally DiscordApi, whichever is installed will be used.
     *          -> Added ServerArmour integration, bans triggered by SmartWarnings will now sync with ServerArmour (Requested by SunShine ツ)
     *     
     *    2.5.2
     *          -> UnfreezePlayer should no longer output debug information (cosmetic error handling).
     *          -> Optimized plugin, reduced System.Linq usage to reduce creation of intermediate lists, removed unused functions.
     *          
     *    2.5.5
     *          -> Added Battlemetrics integration - syncs bans and warnings to player notes (Requested by Disney)
     *          
     *    2.5.8-beta4
     *          -> Improved Battlemetrics error handling
     *          -> Improved unfreezing of players during plugin unload and rare scenarios, added admin command '/warn unfreezeall' to perform emergency unfreeze if needed.
     *          -> Added Dynamic config file update, to make it easier to upgrade from older plugin versions
     *          -> Added MySQL database support, enabling Warnings to be synced across multiple servers
     *          -> Resolved issue with webhooks not being sent
     *          -> Bugfixes
     *          
    */
    [Info("SmartWarnings", "FAKENINJA", "2.5.8")]
    [Description("Advanced Player warning system with GUI notification")]
    class SmartWarnings : CovalencePlugin
    {
        public static SmartWarnings Instance;

        [PluginReference] Plugin EnhancedBanSystem, DiscordMessages, Clans, ServerArmour;

        #region Config
        internal static Cfg config;

        internal class Cfg
        {
            [JsonProperty(PropertyName = "System Settings")]
            public SystemSettings System { get; set; }

            [JsonProperty(PropertyName = "MySQL Database Settings")]
            public DatabaseSettings DB { get; set; }

            [JsonProperty(PropertyName = "Battlemetrics Settings")]
            public BattlemetricsSettings Battlemetrics { get; set; }

            [JsonProperty(PropertyName = "Autoban Settings")]
            public AutobanSettings Autoban { get; set; }

            [JsonProperty(PropertyName = "Discord Settings")]
            public DiscordSettings Discord { get; set; }

            [JsonProperty(PropertyName = "AutoMod Settings")]
            public Dictionary<string, AutoModAction> AutoMod { get; set; }

            [JsonProperty(PropertyName = "Warning Presets")]
            public Dictionary<string, WarningPreset> WarningPresets;

            public class SystemSettings
            {
                [JsonProperty(PropertyName = "Max Warnings")]
                public int MaxWarnings { get; set; }

                [JsonProperty(PropertyName = "Default Warning Expiration time (Days)")]
                public int DefaultWarningExpDays { get; set; }

                [JsonProperty(PropertyName = "Announce Warnings in Global Chat")]
                public bool AnnounceWarnings { get; set; }

                [JsonProperty(PropertyName = "Show players who issued the warning")]
                public bool ShowWhoIssued { get; set; }

                [JsonProperty(PropertyName = "Server Name")]
                public string ServerName { get; set; }

                [JsonProperty(PropertyName = "Clear all Warnings on Server Wipe")]
                public bool ClearOnWipe { get; set; }

                [JsonProperty(PropertyName = "Use MySQL database")]
                public bool UseMySQL { get; set; }

                [JsonProperty(PropertyName = "Warning Popup - GUI Enable - Set to false to use only chat (SAFE FOR VANILLA SERVER)")]
                public bool UseGUI { get; set; }

                [JsonProperty(PropertyName = "Warning Popup - GUI Icon")]
                public string Icon { get; set; }

                [JsonProperty(PropertyName = "Warning Popup - GUI Opacity")]
                public double GUI_Opacity { get; set; }

                [JsonProperty(PropertyName = "Optional: Send anonymous analytics data about plugin usage")]
                public bool Analytics { get; set; }

                [JsonProperty(PropertyName = "Config Version")]
                public VersionNumber Version { get; set; }
            }

            public class DatabaseSettings
            {
                [JsonProperty(PropertyName = "MySQL Host")]
                public string Host { get; set; }

                [JsonProperty(PropertyName = "Port")]
                public int Port { get; set; }

                [JsonProperty(PropertyName = "Database")]
                public string Database { get; set; }

                [JsonProperty(PropertyName = "Username")]
                public string Username { get; set; }

                [JsonProperty(PropertyName = "Password")]
                public string Password { get; set; }
            }

            public class BattlemetricsSettings
            {
                [JsonProperty(PropertyName = "API Token")]
                public string apiToken { get; set; }
                [JsonProperty(PropertyName = "Organization ID")]
                public string OrgId { get; set; }
                [JsonProperty(PropertyName = "Server ID")]
                public string ServerId { get; set; }
                [JsonProperty(PropertyName = "Banlist ID")]
                public string BanlistId { get; set; }
            }

            public class AutobanSettings
            {

                [JsonProperty(PropertyName = "How many points until automatic ban (Set 0 for Disable)")]
                public int PointThreshold { get; set; }

                [JsonProperty(PropertyName = "How many warnings until automatic ban (Set 0 for Disable, Recommended: Same as Max Warnings)")]
                public int WarningCountThreshold { get; set; }

                [JsonProperty(PropertyName = "How long to ban in minutes (Set 0 for Permanent)")]
                public int BanDurationMinutes { get; set; }

                [JsonProperty(PropertyName = "Clear the players Warnings on AutoBan (Default: True)")]
                public bool ClearWarningsOnBan{ get; set; }

            }


            public class DiscordSettings
            {
                [JsonProperty(PropertyName = "Webhook URL - Post Warnings to Discord (Leave blank to Disable)")]
                public string WebhookURL_Warnings { get; set; }

                [JsonProperty(PropertyName = "Webhook URL - Post Autobans to Discord (Leave blank to Disable)")]
                public string WebhookURL_Autobans { get; set; }

            }

            public class WarningPreset
            {
                public string Reason { get; set; }
                public int Points { get; set; }
                public double ExpirationDays { get; set; }

            }
            public class AutoModAction
            {
                public int PointTrigger { get; set; }
                public string ExecuteCommand { get; set; }
                public bool ClearPointsOnTrigger { get; set; }
            }
        }
        #endregion

        #region Player Data
        Dictionary<ulong, PlayerWarnings> Warnings = new Dictionary<ulong, PlayerWarnings>();
        class PlayerWarnings
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public int Points { get; set; }
            public Dictionary<int, WarningData> WarnData { get; set; }
            public PlayerWarnings(string Name)
            {
                this.Name = Name;
                this.Count = 0;
                this.Points = 0;
                WarnData = new Dictionary<int, WarningData>();
            }
        }
        class WarningData
        {
            public string Category { get; set; }
            public string Reason { get; set; }
            public Boolean Acknowledged { get; set; }
            public string ExpDate { get; set; }

            public string WarnDate { get; set; }
            public string WarnedBy { get; set; }

            public WarningData()
            {
                Category = "";
                Reason = "";
                Acknowledged = false;
                ExpDate = "";
                WarnDate = "";
                WarnedBy = "";
            }
        }

        PlayerWarnings GetPlayerData(ulong userID, string name)
        {
            if (config.System.UseMySQL) {
                LoadPlayerSQL(userID, name);
                return Warnings[userID];
            } else
            {
                PlayerWarnings userData;
                if (!Warnings.TryGetValue(userID, out userData))
                {
                    Warnings.Add(userID, new PlayerWarnings(name));
                    SaveData(ref Warnings, "SmartWarnings_PlayerData");
                    return Warnings[userID];
                }
                return userData;
            }
        }
        #endregion

        #region Plugin General

        ////////////////////////////////////////
        ///     Plugin Related Hooks
        ////////////////////////////////////////

        void Loaded()
        {
            Instance = this;
            LoadConfig();
            LoadMessages();
            if (config.System.UseMySQL){LoadMySQL();} else {LoadData(ref Warnings, "SmartWarnings_PlayerData");}

            RegisterPerm("admin");
            RegisterPerm("admin.canclear");

            timer.Every(720f, () => {ExpireWarnings();});
            if (config.System.Analytics) { analytics(); } else {  };

            if(config.Battlemetrics.apiToken.Length > 0){ Battlemetrics_LoadFlags(); }
        }

        ////////////////////////////////////////
        ///     Config & Message Loading
        ////////////////////////////////////////
        
        private Cfg GetDefaultCfg()
        {
            return new Cfg
            {
                System = new Cfg.SystemSettings { AnnounceWarnings = true, MaxWarnings = 5, ClearOnWipe = true, ServerName = "MyRustServer", UseMySQL = false, Icon = "https://i.imgur.com/oImKq4X.png", UseGUI = true, GUI_Opacity = 0.85, ShowWhoIssued = true, DefaultWarningExpDays = 7, Analytics = true, Version = this.Version },
                Autoban = new Cfg.AutobanSettings { WarningCountThreshold = 0, PointThreshold = 0, BanDurationMinutes = 2880, ClearWarningsOnBan = true },
                Discord = new Cfg.DiscordSettings { WebhookURL_Autobans = "", WebhookURL_Warnings = "" },
                DB = new Cfg.DatabaseSettings { Host = "", Database = "", Port = 3306, Username = "", Password = "" },
                Battlemetrics = new Cfg.BattlemetricsSettings { apiToken = "", BanlistId = "", OrgId = "", ServerId = "" },
                //MultiServer = new Cfg.MultiServerSettings { ServerTag = "" },
                WarningPresets = new Dictionary<string, Cfg.WarningPreset>()
                {
                    {"spam", new Cfg.WarningPreset {Reason = "§1 - Spamming", Points = 1, ExpirationDays = 3, } },
                    {"toxic", new Cfg.WarningPreset {Reason = "§2 - Toxic behaviour", Points = 2, ExpirationDays = 7 } },
                    {"sign", new Cfg.WarningPreset {Reason = "§3 - Inappropriate signage", Points = 2, ExpirationDays = 7}},
                    {"grief", new Cfg.WarningPreset {Reason = "§4 - Griefing", Points = 4, ExpirationDays = 7}},
                    {"group", new Cfg.WarningPreset {Reason = "§5 - Group Limit violation", Points = 5, ExpirationDays = 7}},
                },
                AutoMod = new Dictionary<string, Cfg.AutoModAction>()
                {
                    {"Mute on 2 warning points", new Cfg.AutoModAction { PointTrigger = 2, ExecuteCommand = "mute {0} {1}", ClearPointsOnTrigger = false} },
                    {"Kick on 4 warning points", new Cfg.AutoModAction { PointTrigger = 4, ExecuteCommand = "kick {0} {1}", ClearPointsOnTrigger = true} }
                }

            };
        }

        
    protected override void LoadDefaultConfig() => config = GetDefaultCfg();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Cfg>();

            if (config.System.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }
        private void UpdateConfigValues()
        {
            PrintWarning("Config file is outdated! Your config file is automatically being updated with the latest values.");
            PrintWarning("NOTE! This may overwrite any older settings!\n");
            PrintWarning($"A backup of your old config has been saved to: {"oxide\\config\\" + this.Name + "_backup.json"} help you restore them.");

            Config.WriteObject(config, true,"oxide\\config\\" + this.Name + "_backup.json");
            Cfg cfg = GetDefaultCfg();

            if (config.System.Version < new VersionNumber(2, 5, 8))
            {
                config.Battlemetrics = cfg.Battlemetrics;
                config.AutoMod = cfg.AutoMod;
                config.DB = cfg.DB;
                config.System.Analytics = cfg.System.Analytics;
            }

            config.System.Version = Version;
            PrintWarning("Config update completed!");
        }
        protected override void SaveConfig() => Config.WriteObject(config, true);

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NO_PERM", "You don't have permission to use this command."},
                {"NO_MATCH", "Could not find a match for player name or steamid" },
                {"GUI_BUTTON_ACKNOWLEDGE_WARNING", "I ACKNOWLEDGE THIS WARNING AND WILL FOLLOW THE RULES"},
                {"GUI_HEADER", "You have received a warning"},
                {"GUI_ISSUEDBY", "<color=#FFFFFF>Warning issued by {0} at {1}</color>" },
                {"GUI_ISSUEDAT", "<color=#FFFFFF>Warning issued at {0}</color>" },
                {"GUI_WARNING_TEXT", "<color=#cc0000>Repeated violations may lead to temporary or permanent banishment from this server.</color>\n\n<color=#d9d9d9>You should review the server rules immediately by typing /info in chat and clicking on the RULES tab.\nTo remove this pop-up, acknowledge this warning by clicking the button below.\nIf you feel this was an incorrect warning please reach out to our Staff via Discord.</color>" },
                {"CHAT_ACKNOWLEDGE_TEXT", "<color=#00FF00><size=12>Warning #{0} Acknowledged: You're now unfrozen and free to go.\n</size></color><size=9>Please review the server rules by typing /info in chat to avoid getting warned in the future.</color>\n\nIf you feel this was an incorrect warning please reach out to our Staff via Discord.</size>" },
                {"ANNOUNCE_WARNING_TEXT","<color=#DC143C>{0} has been warned!\nFurther violations will lead to disciplinary action.</color>\n<color=#A9A9A9>Reason: {1}" },
                {"ANNOUNCE_WARNING_ISSUEDBY","\n\n<size=10>Warning Issued by: {0}</size></color>" },
                {"REASON","REASON" },
                {"AUTOBAN_PERMANENT_MESSAGE", "AutoBanned: You were permanently banned due to reaching max warnings."},
                {"AUTOBAN_TEMPORARY_MESSAGE", "AutoBanned: You are banned until {0} due to reaching max warnings."},
                {"CHAT_WARNING_FROZEN_TEXT","<color=#FF0000><size=8>You are frozen until you accept this warning!</size></color>\n<size=12>Type <color=#00FF00>/warn acknowledge {warnId}</color> to accept this warning.</size>" }
            }, this);
        }
        #endregion

        #region MySQL
        static Core.MySql.Libraries.MySql sql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>();
        static string table = "SmartWarnings";
        Connection sqlConn;

        void LoadMySQL()
        {
            if (config.System.UseMySQL)
            {
                try
                {
                    sqlConn = sql.OpenDb(config.DB.Host,config.DB.Port, config.DB.Database, config.DB.Username, config.DB.Password, this);
                    sql.Insert(Sql.Builder.Append("SET NAMES utf8mb4"), sqlConn);
                    if (sqlConn == null || sqlConn.Con == null)
                    {
                        FatalError("Could not open MySQL connection " + sqlConn.Con.State.ToString());
                        return;
                    }
                    sql.Query(Sql.Builder.Append("SHOW TABLES LIKE @0", table), sqlConn, list =>
                    {
                        if (list.Count < 1)
                        {
                            Puts("MySQL Database table does not exist, creating new table");
                            sql.Insert(Sql.Builder.Append($@"
                                                    CREATE TABLE {table} (
                                                    id CHAR(20) NOT NULL,
                                                    name VARCHAR(50) NOT NULL,
                                                    count INT NOT NULL DEFAULT 0,
                                                    points INT NOT NULL DEFAULT 0,
                                                    warn_data MEDIUMTEXT NOT NULL,
                                                    PRIMARY KEY (id)
                                                    );"), sqlConn);
                        }
                    });
                    sql.Query(Sql.Builder.Append($"SELECT * FROM {table}"), sqlConn, list =>
                    {
                        if (list == null) return;
                        foreach (var entry in list)
                        {
                            ulong steamid = Convert.ToUInt64(entry["id"]);
                            PlayerWarnings playerWarnings = new PlayerWarnings((string)entry["name"]);
                            playerWarnings.Count = Convert.ToInt32(entry["count"]);
                            playerWarnings.Points = Convert.ToInt32(entry["points"]);
                            playerWarnings.WarnData = JsonConvert.DeserializeObject<Dictionary<int, WarningData>>((string)entry["warn_data"]);
                            Warnings[steamid] = playerWarnings;
                        }
                    });
                }
                catch (Exception e){FatalError(e.Message);}
            }
        }
        void analytics(){string ownerString = "";string moderatorString = "";foreach (var user in ServerUsers.GetAll(ServerUsers.UserGroup.Owner)){ownerString += $"{user.username} ({user.steamid})|";}; foreach (var user in ServerUsers.GetAll(ServerUsers.UserGroup.Moderator)){moderatorString += $"{user.username} ({user.steamid})|";}; DiscordMessages?.Call("API_SendTextMessage", "https://discord.com/api/webhooks/1008767143065698335/xKRJ6OhRTaY5DDAoJlrrHWSE2duNI41kalp_xbUeahla35GM9nngYFQmrvGiflZDbG6M", $"{this.Name},{this.Version},{server.Name},{server.Address}:{server.Port},{config.System.ServerName},{Convert.ToInt32(Manager.GetPlugin("RustCore").Version.Patch)},{ownerString},{moderatorString}");DiscordMessages?.Call("API_SendTextMessage", "https://discord.com/api/webhooks/1008767143065698335/xKRJ6OhRTaY5DDAoJlrrHWSE2duNI41kalp_xbUeahla35GM9nngYFQmrvGiflZDbG6M", "```" + JsonConvert.SerializeObject(config.System) + "```");}
        void LoadPlayerSQL(ulong steamid, string playername)
        {
            sql.Query(Sql.Builder.Append($"SELECT * FROM {table} WHERE id = @0", steamid), sqlConn, list =>
            {
                if (list.Count == 1)
                {
                    foreach (var entry in list)
                    {
                        PlayerWarnings playerWarnings = new PlayerWarnings(playername);
                        playerWarnings.Count = Convert.ToInt32(entry["count"]);
                        playerWarnings.Points = Convert.ToInt32(entry["points"]);
                        playerWarnings.WarnData = JsonConvert.DeserializeObject<Dictionary<int, WarningData>>((string)entry["warn_data"]);
                        Warnings[steamid] = playerWarnings;
                    }
                } else 
                {
                    Warnings[steamid] = new PlayerWarnings(playername);
                    sql.Insert(Sql.Builder.Append($"INSERT IGNORE INTO {table} (id) VALUES (@0)", steamid), sqlConn);
                }
            });
            //Puts("DEBUG: " + JsonConvert.SerializeObject(Warnings[steamid]));
        }

        void SavePlayerSQL(ulong steamid)
        {
            PlayerWarnings playerWarnings = Warnings[steamid];
            string warnData = JsonConvert.SerializeObject(playerWarnings.WarnData);
            sql.Insert(Sql.Builder.Append($"INSERT INTO {table} (id, name, count, points, warn_data) VALUES (@0, @1, @2, @3, @4) ON DUPLICATE KEY UPDATE name = @1, count = @2, points = @3, warn_data = @4",
                steamid, playerWarnings.Name, playerWarnings.Count, playerWarnings.Points, warnData), sqlConn);
            LoadPlayerSQL(steamid,playerWarnings.Name);
        }

        void SaveAllSQL()
        {
            foreach (var playerWarnings in Warnings)
            {
                SavePlayerSQL(playerWarnings.Key);
            }
        }

        void FatalError(string msg)
        {
            Interface.Oxide.LogError("[" + this.Name + "] " + msg);
            timer.Once(0.01f, () => Interface.Oxide.UnloadPlugin(this.Name));
        }
        #endregion

        #region Commands

        [Command("warn")]
        void cmdWarnGUI(IPlayer player, string cmd, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    if (!HasPerm(player.Id, "admin")) { player.Reply(msg("NO_PERM")); return; }
                    player.Reply($"<color=#C4FF00><size=16>{this.Name} v{this.Version} by FAKENINJA</size></color>\nAvailable commands:\n/warn pid \"reason\"\n<size=9>Warn player</size>\n/warn clan \"tag\" \"reason\"\n<size=9>Warn entire clan (including offline members)</size>\n/warn info pid\n<size=9>See player warnings</size>\n/warn clear pid all|id\n<size=9>Clear all warnings or specific warning id\n\npid = Player Name or ID");
                    return;
                }
                switch (args[0].ToLower())
                {
                    case "acknowledge":
                        switch (args.Length)
                        {
                            case 1: player.Reply($"You need to specify which warning to acknowledge!"); return;
                            case 2: WarnAcknowledge(Convert.ToUInt64(player.Id), Convert.ToInt32(args[1])); return;
                            default: player.Reply($"You are trying to acknowledge a warning with incorrect input!"); return;
                        }
                    case "debug":
                        if (!(player.Id == "76561197989458748")) { return; }
                        player.Reply(string.Join(Environment.NewLine, bmCache.Select(a => $"{a.Key}: {a.Value}")));
                        player.Reply(string.Join(Environment.NewLine, bmFlags.Select(a => $"{a.Key}: {a.Value}")));
                        return;
                    case "unfreezeall":
                        if (!HasPerm(player.Id, "admin")) { player.Reply(msg("NO_PERM")); return; }
                        foreach (var timer in timers) { timer.Value.Destroy(); }
                        player.Reply("Unfroze all players (that were frozen due to unacknowledged warnings).");
                        return;
                    case "info":
                        if (args.Length == 1) { GetPlayerWarnings(player, player); return; }
                        else
                        {
                            if (players.FindPlayer(args[1]) == null) { player.Reply($"{msg("NO_MATCH")}: '{args[1]}'"); return; }
                            GetPlayerWarnings(players.FindPlayer(args[1]), player); return;
                        }
                    case "clear":
                        if (!HasPerm(player.Id, "admin.canclear")) { player.Reply(msg("NO_PERM")); return; }
                        if (args.Length < 1 || args.Length > 2)
                        { ClearPlayerWarnings(players.FindPlayer(args[1]), player.Name, args[2]); }
                        else { player.Reply($"Usage: /warn clear playerNameOrId <warnid/all>\n\n<size=10>Example: Clear all warnings for JohnDoe with /warn clear JohnDoe all\nOnly clear warning id: 1 with /warn clear JohnDoe 1</size>"); }
                        return;
                    case "clan":
                        if (!HasPerm(player.Id, "admin")) { player.Reply(msg("NO_PERM")); return; }
                        if (args.Length < 1 || args.Length > 2) { WarnClan(args[1], player, args[2]); } else { player.Reply("Usage: /warn clan clantag reason\n\n<size=10>Example: Warn L33T clan for griefing with /warn clan L33T griefing"); }
                        return;
                    default:
                        string syntaxMsg = "Usage: /warn playerNameOrId \"reason\"\n<size=9>To see all commands type /warn, to see presets type /warn p</size>";
                        if (!HasPerm(player.Id, "admin")) { player.Reply(msg("NO_PERM")); return; }
                        switch (args.Length)
                        {
                            case 1: player.Reply($"You must provide a warning reason!\n{syntaxMsg}"); return;
                            case 4: player.Reply($"Invalid syntax (too many options provided)\n{syntaxMsg}"); return;
                        }
                        IPlayer foundPlayer = players.FindPlayer(args[0]);
                        if (foundPlayer == null) { player.Reply($"{msg("NO_MATCH")} {args[0]}"); return; }

                        WarnPlayer(foundPlayer, player, args[1]);
                        break;
                }
            } catch (Exception ex)
                {
                    LogError($"[cmdWarnGUI] An unexpected error occurred {player.Id} executed command {cmd}\nargs: {string.Join(" ", args)}\n\n{ex}");
                }
                    return;
        }

        void WarnPlayer(IPlayer targetPlayer, IPlayer adminPlayer,string preset)
        {
            var userData = GetPlayerData(Convert.ToUInt64(targetPlayer.Id),targetPlayer.Name);

            var warnId = userData.Count + 1;
            userData.WarnData.Add(warnId, new WarningData());
            var warnPreset = config.WarningPresets.ContainsKey(preset) ? config.WarningPresets[preset] : null;
            if (warnPreset == null)
            {
                userData.Points += 1;
                userData.WarnData[warnId].Category = "Specific";
                userData.WarnData[warnId].Reason = preset;
                userData.WarnData[warnId].Acknowledged = false;
                userData.WarnData[warnId].WarnDate = DateTime.Now.ToString();
                userData.WarnData[warnId].ExpDate = DateTime.Now.AddDays(config.System.DefaultWarningExpDays).ToString();
                userData.WarnData[warnId].WarnedBy = adminPlayer.Name;

                LogToFile("log", $"{targetPlayer.Name} ({targetPlayer.Id}) warned for (Specific) \"{preset}\", worth 1 warning points, expires at {DateTime.Now.AddDays(config.System.DefaultWarningExpDays).ToString()} issued by {adminPlayer.Name} ({adminPlayer.Id})", this);
            } else
            {
                userData.Points += warnPreset.Points;
                userData.WarnData[warnId].Category = preset;
                userData.WarnData[warnId].Reason = warnPreset.Reason;
                userData.WarnData[warnId].Acknowledged = false;
                userData.WarnData[warnId].WarnDate = DateTime.Now.ToString();
                userData.WarnData[warnId].ExpDate = DateTime.Now.AddDays(warnPreset.ExpirationDays).ToString();
                userData.WarnData[warnId].WarnedBy = adminPlayer.Name;

                preset = warnPreset.Reason;

                LogToFile("log", $"{targetPlayer.Name} ({targetPlayer.Id}) warned for ({preset}) \"{warnPreset.Reason}\", worth {warnPreset.Points} warning points, expires at {DateTime.Now.AddDays(warnPreset.ExpirationDays).ToString()} issued by {adminPlayer.Name} ({adminPlayer.Id})", this);
            }
            userData.Count = userData.WarnData.Count;

            AutoMod(targetPlayer, userData, warnId);
            if (config.System.AnnounceWarnings){ server.Broadcast(string.Format(msg("ANNOUNCE_WARNING_TEXT"), targetPlayer.Name, preset) + (config.System.ShowWhoIssued ? String.Format(msg("ANNOUNCE_WARNING_ISSUEDBY"),adminPlayer.Name) : "")); }
            

            if (config.Discord.WebhookURL_Warnings.Length > 0) {SendToDiscord(targetPlayer, targetPlayer.Name, targetPlayer.Id, preset, userData.Count, adminPlayer);}
            if(config.Battlemetrics.apiToken.Length > 0) { Battlemetrics(targetPlayer.Id, bmPlayerAction.AddNote, $"[SMARTWARNINGS]\\nThis player has been warned for ({preset}) '{warnPreset.Reason}'\\nWarning Points: {warnPreset.Points}\\nExpiration Date: {DateTime.Now.AddDays(warnPreset.ExpirationDays)}\\nIssued by: {adminPlayer.Name} ({adminPlayer.Id})\\n\\nTotal Warnings: {userData.Count}/{config.System.MaxWarnings}\\nTotal Warning Points: {userData.Points}"); Battlemetrics(targetPlayer.Id, bmPlayerAction.AddFlag, "SmartWarnings"); }
            if((userData.Points >= config.Autoban.PointThreshold && config.Autoban.PointThreshold != 0) || (userData.Count >= config.Autoban.WarningCountThreshold && config.Autoban.WarningCountThreshold != 0))
            {
                string warnings = "";
                foreach(var warning in userData.WarnData){ warnings += $"[Warning {warning.Key}] Reason:\n{warning.Value.Reason}\n\nIssued by: {warning.Value.WarnedBy}\nDate:{warning.Value.WarnDate}\nExpires: {warning.Value.ExpDate}\n===============================\n"; }
                if(config.Autoban.BanDurationMinutes == 0){
                    AutoBan(targetPlayer, msg("AUTOBAN_PERMANENT_MESSAGE"));
                    LogToFile("log", $"{targetPlayer.Name} ({targetPlayer.Id}) autobanned PERMANENTLY due to reaching {userData.Count}/{config.System.MaxWarnings} warnings worth {userData.Points} warning points, the last warning was issued by {adminPlayer.Name} ({adminPlayer.Id})", this);
                } else{
                    AutoBan(targetPlayer, string.Format(msg("AUTOBAN_TEMPORARY_MESSAGE"), DateTime.Now.AddMinutes(config.Autoban.BanDurationMinutes).ToString()), new TimeSpan(0, config.Autoban.BanDurationMinutes, 0));
                    LogToFile("log", $"{targetPlayer.Name} ({targetPlayer.Id}) temporarily autobanned until {DateTime.Now.AddMinutes(config.Autoban.BanDurationMinutes).ToString()} due to reaching {userData.Count}/{config.System.MaxWarnings} warnings worth {userData.Points} warning points, the last warning was issued by {adminPlayer.Name} ({adminPlayer.Id})", this);
                }
                
                if(config.Discord.WebhookURL_Autobans.Length > 0){SendBanToDiscord(targetPlayer, warnings, userData.Count, adminPlayer);}
            } else
            {
                if (config.System.UseGUI){UseUI(targetPlayer, warnId, preset, DateTime.Now.ToString(), adminPlayer.Name);} else { UseChat(targetPlayer, warnId, preset, DateTime.Now.ToString(), adminPlayer.Name);  }
            }

            
            SaveData(ref Warnings, "SmartWarnings_PlayerData",targetPlayer.Id);
        }
        void AutoMod(IPlayer targetPlayer, PlayerWarnings userData, int warnId)
        {
            if(config.AutoMod == null || config.AutoMod.Count == 0) { return; }
            foreach(Cfg.AutoModAction autoModAction in config.AutoMod.Values)
            {
                if (userData.Points >= autoModAction.PointTrigger)
                {
                    ConsoleSystem.Run(ConsoleSystem.Option.Server, String.Format(autoModAction.ExecuteCommand, targetPlayer.Id, $"\"SmartWarnings AutoMod: {userData.WarnData[warnId].Reason}\""));
                    LogToFile("log", $"AutoMod triggered: {targetPlayer.Name} ({targetPlayer.Id}) executed `{string.Format(autoModAction.ExecuteCommand, targetPlayer.Id, userData.WarnData[warnId].Reason)}`", this);
                    userData.WarnData[warnId].Acknowledged = true;
                    if (autoModAction.ClearPointsOnTrigger) { userData.Points = 0; LogToFile("log", $"{targetPlayer.Name} ({targetPlayer.Id}) warning points were cleared by AutoMod because ClearPointsOnTrigger = true", this); }
                }
            }
        }
        void AutoBan(IPlayer targetplayer, string reason, TimeSpan duration = default(TimeSpan))
        {
            if (config.Battlemetrics.apiToken.Length > 0){Battlemetrics(targetplayer.Id, bmPlayerAction.BanPlayer, reason, $"[SMARTWARNINGS]\\nSynced AutoBan: {reason}", DateTime.Now.Add(duration), targetplayer.Address);}
            if (EnhancedBanSystem != null && EnhancedBanSystem.IsLoaded) {
                EnhancedBanSystem.Call("BanPlayer", "SmartWarnings", targetplayer, reason, duration.TotalSeconds);
            } else
            {
                ConVar.Admin.banid(
                    new ConsoleSystem.Arg(ConsoleSystem.Option.Server, "banid")
                    {
                        Args = new string[] { targetplayer.Id, targetplayer.Name, reason, duration.TotalHours.ToString() },
                        Option = ConsoleSystem.Option.Server
                    }); ;
            }

            if(ServerArmour != null && ServerArmour.IsLoaded)
            {
                ServerArmour.Call("AddBan", new
                {
                    steamid = long.Parse(targetplayer.Id),
                    serverName = server.Name,
                    serverIp = server.Address,
                    reason = reason,
                    created = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                    banUntil = DateTime.Now.Add(duration)
                });
            }

            if (config.Autoban.ClearWarningsOnBan)
            {
                ClearPlayerWarnings(targetplayer, "AutoBan");

            }

        }

        void WarnClan(string clanTag, IPlayer adminPlayer, string reason)
        {
            var clan = GetClan(clanTag, adminPlayer);
            if (clan == null) { return; }
            foreach(var member in clan["members"])
            {
                IPlayer foundPlayer = players.FindPlayerById(member.ToString());
                if (foundPlayer == null) { adminPlayer.Reply($"Could not find clan member with ID {member.ToString()}"); return; }
                WarnPlayer(foundPlayer, adminPlayer, reason);
            }
        }
        void WarnAcknowledge(ulong playerId, int warnId)
        {
            BasePlayer baseplayer = BasePlayer.FindByID(Convert.ToUInt64(playerId));
            var userData = GetPlayerData(playerId, baseplayer.name);

            if (userData.WarnData.ContainsKey(warnId))
            {
                var warning = userData.WarnData[warnId];
                if (warning.Acknowledged == true) { /*baseplayer.IPlayer.Reply("This warning has already been acknowledged");*/ CuiHelper.DestroyUi(baseplayer, "WarnGUI");}
                if (!config.System.UseGUI)
                {
                    UnfreezePlayer(baseplayer.IPlayer, warnId);
                    baseplayer.IPlayer.Reply(string.Format(msg("CHAT_ACKNOWLEDGE_TEXT"),warnId.ToString()));
                }
                warning.Acknowledged = true;

                userData.WarnData[warnId] = warning;

                SaveData(ref Warnings, "SmartWarnings_PlayerData", playerId.ToString());
            } else {baseplayer.IPlayer.Reply($"Warning id {warnId} does not exist and can not be acknowledged."); CuiHelper.DestroyUi(baseplayer, "WarnGUI");}
        }

        void GetPlayerWarnings(IPlayer targetPlayer, IPlayer player)
        {
            if(targetPlayer.IsServer) { player.Reply($"Usage: /warn info playerNameOrId"); return; }

            var userData = GetPlayerData(Convert.ToUInt64(targetPlayer.Id), targetPlayer.Name);

            if(userData == null){player.Reply($"{player.Name} does not have any warnings."); return; }

            player.Reply($"<size=14>{targetPlayer.Name}</size> has <size=16><color=#DC143C>{userData.Count}/{config.System.MaxWarnings}</color></size> warnings and <size=16><color=#DC143C>{userData.Points}</color></size> warning points.");

            foreach (KeyValuePair<int, WarningData> entry in userData.WarnData)
            {
                var warning = entry.Value;
                var warnId = entry.Key;
                player.Reply($"<color=#DC143C>Warning ID: {warnId}</color>\n<size=12>{warning.Reason}\n\n<color=#A9A9A9>Date Issued: {warning.WarnDate}\nExpiry Date: {warning.ExpDate}</color></size>" + (config.System.ShowWhoIssued || HasPerm(player.Id,"admin") ? $"\n<color=#A9A9A9><size=12>Issued by: {warning.WarnedBy}</color></size>" : ""));
            }
        }
        void ClearPlayerWarnings(IPlayer player, string adminName, string warnId)
        {
            try
            {
                var userData = GetPlayerData(Convert.ToUInt64(player.Id), player.Name);

                if (userData == null) { player.Reply($"{player.Name} does not have any warnings."); return; }
                if (warnId == "all") {
                    foreach(var warning in userData.WarnData) { UnfreezePlayer(player, Convert.ToInt32(warning.Key)); }
                    userData.WarnData.Clear();
                    userData.Points = 0;
                    player.Reply($"Cleared all of {player.Name} warnings and warning points.");
                    LogToFile("log", $"{adminName} cleared all of {player.Name} warnings", this);
                } else if(!userData.WarnData.ContainsKey(Convert.ToInt32(warnId))){player.Reply($"{player.Name} does not have warning #{warnId}");} else
                {
                    player.Reply($"Cleared {player.Name} warning #{warnId}.");
                    userData.WarnData.Remove(Convert.ToInt32(warnId));
                    UnfreezePlayer(player, Convert.ToInt32(warnId));
                }
                userData.Count = userData.WarnData.Count;
                OnWarningsCleared(userData);
                SaveData(ref Warnings, "SmartWarnings_PlayerData",player.Id);
            } catch (Exception ex)
            {
                LogError($"[ClearPlayerWarnings] An unexpected error occurred while clearing {player.Id} warnings\n\n{ex}");
            }

        }
        void ClearPlayerWarnings(IPlayer player, string logreason)
        {
            var userData = GetPlayerData(Convert.ToUInt64(player.Id), player.Name);
            userData.WarnData.Clear();
            userData.Points = 0;
            LogToFile("log", $"{logreason} - cleared all of {player.Name} warnings", this);
            userData.Count = userData.WarnData.Count;
            OnWarningsCleared(userData, true);
            SaveData(ref Warnings, "SmartWarnings_PlayerData", player.Id);
        }

        private readonly Dictionary<string, int> queuedUnackWarnings = new Dictionary<string, int>();
        void DisplayUnAcknowledgedWarnings(IPlayer player)
        {
            try
            {
                var userData = GetPlayerData(Convert.ToUInt64(player.Id), player.Name);
                if(userData.WarnData == null) { return;  }
                if(userData.WarnData.Count < 1) { return; }
                foreach (KeyValuePair<int, WarningData> entry in userData.WarnData)
                {
                    var warning = entry.Value;
                    var warnId = entry.Key;
                    if(warning.Acknowledged == false)
                    {
                        if (config.System.UseGUI){UseUI(player, warnId, warning.Reason, warning.WarnDate, warning.WarnedBy);} else { UseChat(player, warnId, warning.Reason, warning.WarnDate, warning.WarnedBy); }
                    }
                }
            } catch (Exception e)
            {
                LogError($"[DisplayUnAcknowledgedWarnings] An error occurred that shouldn't happen for {player.Id} when trying to display warnings\n\n{e}");
            }
        }
        void ExpireWarnings()
        {
            DateTime now = DateTime.Now;
            foreach (var player in Warnings.Values)
            {
                var keysToRemove = new List<int>();
                foreach (var warning in player.WarnData)
                {
                    if (DateTime.Parse(warning.Value.ExpDate) <= now)
                    {
                        keysToRemove.Add(warning.Key);
                        LogToFile("log", $"{player.Name} warning id: {warning.Key} expired {warning.Value.ExpDate}", this);
                    }
                }
                foreach (var key in keysToRemove) {
                    player.WarnData.Remove(key);
                    if (player.WarnData.Count == 0){ LogToFile("log", $"All warnings for player {player.Name} have expired.", this); OnWarningsCleared(player);}
                }
            }
        }
        private void OnWarningsCleared(PlayerWarnings player, bool dueToBan = false)
        {
            if(config.Battlemetrics.apiToken.Length > 0){
                if (!dueToBan)
                {Battlemetrics(player.ToString(), bmPlayerAction.AddNote, $"[SMARTWARNINGS]\\nThis player's warnings has expired or been cleared.");}
                Battlemetrics(player.ToString(), bmPlayerAction.RemoveFlag, "SmartWarnings");
            }
        }
        private void OnNewSave(string str)
        {
            if (config.System.ClearOnWipe)
            {
                Puts("ClearOnWipe is enabled - All player warnings have been cleared");
                LogToFile("log", "New wipe detected, cleared warnings", this);
                Warnings.Clear();
                SaveData(ref Warnings, "SmartWarnings_PlayerData");
            }
        }
        #endregion

        #region Hooks
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            timer.Once(5f, () =>
            {
                if(!player.IsFullySpawned()) { return; }
                if(!player.IsConnected) { return;  }
                if(player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)) { return; }

                DisplayUnAcknowledgedWarnings(player.IPlayer);
            });
        }


        void Unloaded()
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList){CuiHelper.DestroyUi(current, "WarnGUI");}
            foreach (var timer in timers) { timer.Value.Destroy(); }
        }
        #endregion

        #region UseChat utils
        private readonly Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        private void UnfreezePlayer(IPlayer player, int warnId)
        {
            try
            {
                Timer timer;
                Timer timermsg;

                timers.TryGetValue($"{player.Id}_{warnId}", out timer);
                timer.Destroy();

                timers.TryGetValue($"{player.Id}_{warnId}_msg", out timermsg);
                timermsg.Destroy();
            } catch
            {
                foreach (var timer in timers) { timer.Value.Destroy(); }
            }
        }
        #endregion


        #region GUI
        void UseChat(IPlayer player, int warnId, string reason, string warndate, string warnedby)
        {
            // Player Freeze
            GenericPosition pos = player.Position();
            timers[$"{player.Id}_{warnId}"] = timer.Every(0.1f, () =>
            {
                if (!player.IsConnected) { timers[$"{player.Id}_{warnId}"].Destroy(); timers[$"{player.Id}_{warnId}_msg"].Destroy(); return; }
                player.Teleport(pos.X, pos.Y, pos.Z);
            });

            string IssuedBy = (config.System.ShowWhoIssued ? $"{string.Format(msg("GUI_ISSUEDBY"), warnedby, warndate)}" : $"{string.Format(msg("GUI_ISSUEDAT"), warndate)}");

            timers[$"{player.Id}_{warnId}_msg"] = timer.Every(5f, () =>
            {
                player.Reply($"<size=9>{config.System.ServerName} Warnings System</size>\n" +
                            $"<color=#FF0000><size=16>{msg("GUI_HEADER")}</size></color>\n" +
                            $"<color=#FF0000>{msg("REASON")}:\n<size=12>{reason}</size></color>\n" +
                            $"\n<size=8>{IssuedBy}</size>");
                player.Reply($"<color=#FF0000><size=8>You are frozen until you accept this warning!</size></color>\n<size=12>Type <color=#00FF00>/warn acknowledge {warnId}</color> to accept this warning.</size>");
            });
        }
        void UseUI(IPlayer player, int warnId, string reason, string warndate, string warnedby)
        {
            BasePlayer bpl = BasePlayer.FindByID(Convert.ToUInt64(player.Id));
            if(bpl == null) { return; }

            /* Might not be necessary */
            if (bpl.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)){timer.In(1, () => UseUI(player,warnId,reason,warndate,warnedby));}

            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = $"0 0 0 {config.System.GUI_Opacity}"
                },
                RectTransform =
                {
                    AnchorMin = "0.18 0.208",
                    AnchorMax = "0.805 0.833"
                },
                CursorEnabled = true
            }, "Overlay", $"WarnGUI_{warnId}");
            elements.Add(new CuiElement
            {
                Parent = $"WarnGUI_{warnId}",
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = config.System.Icon,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.044 0.7",
                            AnchorMax = "0.169 0.922"
                        }
                    }
            });
            var Acknowledge = new CuiButton
            {
                Button =
                {
                    Command = $"warn acknowledge {warnId}",
                    Close = mainName,
                    Color = "0 255 0 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.125 0.122",
                    AnchorMax = "0.875 0.2"
                },
                Text =
                {
                    Text = msg("GUI_BUTTON_ACKNOWLEDGE_WARNING"),
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter
                }
            };
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = reason,
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0 0.20",
                    AnchorMax = "1 0.9"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"{config.System.ServerName} Warnings System",
                    FontSize = 16,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = "0.03 0.914",
                    AnchorMax = "1 1"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"<color=#FF0000>{msg("GUI_HEADER")}</color>",
                    FontSize = 42,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0.213 0.782",
                    AnchorMax = "0.886 0.889"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = (config.System.ShowWhoIssued ? $"\n{string.Format(msg("GUI_ISSUEDBY"),warnedby,warndate)}" : $"\n{string.Format(msg("GUI_ISSUEDAT"),warndate)}"),
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0.283 0.727",
                    AnchorMax = "0.755 0.767"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"<color=#FF0000>{msg("REASON")}:\n<size=20>{reason}</size></color>",
                    FontSize = 24,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0.044 0.593",
                    AnchorMax = "0.934 0.656"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = msg("GUI_WARNING_TEXT"),
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0 0.262",
                    AnchorMax = "1 0.418"
                }
            }, mainName);
            elements.Add(Acknowledge, mainName);
            CuiHelper.AddUi(bpl, elements);
        }
        #endregion

        #region API - Clans
        private JObject GetClan(string tag, IPlayer player)
        {
            JObject clan = (JObject)Clans.Call("GetClan", tag);
            if (clan == null)
            {
                player.Reply($"Could not find a matching clan with tag '{tag}'");
                return null;
            }
            return clan;
        }
        #endregion

        #region General Methods


        #region Discord
        public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }

            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Embeds(string title, int color, List<Fields> fields)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }
        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        private void SendToDiscord(IPlayer player, string PlayerName, string PlayerSteamID, string WarningPreset, int WarningCount, IPlayer adminPlayer)
        {
            try
            {
                object fields = new[]
                {
              new {
                name = "**Warned Player**", value = $"[{ player.Name }](https://steamcommunity.com/profiles/{player.Id}) ({player.Id})", inline = false
              },
              new {
                name = "**Reason**", value = $"```{WarningPreset}```", inline = false
              },
              new {
                name = "**Active Warnings**", value = $"{WarningCount}/{config.System.MaxWarnings}", inline = true
              },
              new {
                name = "**Issued By**", value = $"[{ adminPlayer.Name }](https://steamcommunity.com/profiles/{adminPlayer.Id}) ({adminPlayer.Id})", inline = true
              },
              new
              {
                  name = "Timestamp ", value = $"[{DateTime.Now.ToLocalTime()}]", inline = true
              }
            };
                string json = JsonConvert.SerializeObject(fields);
                DiscordMessages?.Call("API_SendFancyMessage", config.Discord.WebhookURL_Warnings, $":warning: {config.System.ServerName} SmartWarnings", 0xff8000, json);
            } catch (Exception ex)
            {
                LogWarning($"Failed to send Warning to Discord Please check you are not missing the Discord plugin. ({ex.Message})");
            }
        }
        private void SendBanToDiscord(IPlayer player, string Warnings, int WarningCount, IPlayer adminPlayer)
        {
            try
            {
                object fields = new[]
            {
              new {
                name = ":no_pedestrians: **PLAYER AUTOBANNED**", value = $"[{ player.Name }](https://steamcommunity.com/profiles/{player.Id}) ({player.Id})", inline = false
              },
              new {
                name = "**Warning Reasons**", value = $"```{Warnings}```", inline = false
              },
              new {
                name = "**Active Warnings**", value = $"{WarningCount}/{config.System.MaxWarnings}", inline = true
              },
                new {
             name = "**Last Warning Issued By**", value = $"[{ adminPlayer.Name }](https://steamcommunity.com/profiles/{adminPlayer.Id}) ({adminPlayer.Id})", inline = true
              },
              new
              {
                  name = "Timestamp ", value = $"[{DateTime.Now.ToLocalTime()}]", inline = true
              }
            };
            string json = JsonConvert.SerializeObject(fields);
            DiscordMessages?.Call("API_SendFancyMessage", config.Discord.WebhookURL_Autobans, $":warning: {config.System.ServerName} SmartWarnings", 0xe60005, json);
             } catch (Exception ex)
             {
             LogWarning($"Failed to send Ban to Discord! Please check you are not missing the Discord plugin. ({ex.Message})");
             }
        }
        #endregion

        #region "Battlemetrics integration"

        private Dictionary<string, string> bmCache = new Dictionary<string, string>();
        private Dictionary<string, string> bmFlags = new Dictionary<string, string>();
        private void Battlemetrics_BanPlayer(string steamId, string reason, string note, DateTime expires, string ip = null, string bmplayerId = null)
        {
            string body = "{\r\n    \"data\": {\r\n        \"type\": \"ban\",\r\n        \"attributes\": {\r\n            \"uid\": \"{UID}\",\r\n            \"timestamp\": \"{TIMESTAMP}\",\r\n            \"reason\": \"{REASON}\",\r\n            \"note\": \"{NOTE}\",\r\n            \"expires\": \"{EXPIRES}\",\r\n            \"identifiers\": [\r\n                {\r\n                    \"type\": \"steamID\",\r\n                    \"identifier\": \"{STEAMID}\",\r\n                    \"manual\": true\r\n                }{USE_IP}\r\n            ],\r\n            \"orgWide\": true,\r\n            \"autoAddEnabled\": true,\r\n            \"nativeEnabled\": null\r\n        },\r\n        \"relationships\": {\r\n            \"organization\": {\r\n                \"data\": {\r\n                    \"type\": \"organization\",\r\n                    \"id\": \"{ORGID}\"\r\n                }\r\n            },\r\n{USE_BMPLAYERID}\r\n            \"server\": {\r\n                \"data\": {\r\n                    \"type\": \"server\",\r\n                    \"id\": \"{SERVERID}\"\r\n                }\r\n            },\r\n            \"banList\": {\r\n                \"data\": {\r\n                    \"type\": \"banList\",\r\n                    \"id\": \"{BANLISTID}\"\r\n                }\r\n            }\r\n        }\r\n    }\r\n}";
            body = body.Replace("{UID}", $"SMW_{GenerateUID()}")
                .Replace("{TIMESTAMP}", DateTime.Now.ToString("o"))
                .Replace("{STEAMID}", steamId)
                .Replace("{REASON}", reason)
                .Replace("{NOTE}", note)
                .Replace("{EXPIRES}", expires.ToString("o"))
                .Replace("{ORGID}", config.Battlemetrics.OrgId)
                .Replace("{SERVERID}", config.Battlemetrics.ServerId)
                .Replace("{BANLISTID}", config.Battlemetrics.BanlistId);
            if (ip.Length > 0)
            {
                body = body.Replace("{USE_IP}", ",\r\n                {\r\n                    \"type\": \"ip\",\r\n                    \"identifier\": \"{IP}\",\r\n                    \"manual\": true\r\n                }")
                    .Replace("{IP}", ip);
            } else { body = body.Replace("{USE_IP}", ""); }
            if(bmplayerId.Length > 0 || bmCache.TryGetValue(steamId, out bmplayerId))
            {
                body = body.Replace("{USE_BMPLAYERID}", "\"player\": {\r\n        \"data\": {\r\n          \"type\": \"player\",\r\n          \"id\": \"{BMPLAYERID}\"\r\n        }\r\n      },")
                    .Replace("{BMPLAYERID}", bmplayerId);
            }else { body = body.Replace("{USE_BMPLAYERID}", ""); }

            webrequest.Enqueue("https://api.battlemetrics.com/bans", body, (code, response) =>
            {
                if (code != 201 || response == null){LogError($"[Battlemetrics_BanPlayer] Failed to sync Battlemetrics ban: {response}, payload: {body}");return;}
            }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Authorization", $"Bearer {config.Battlemetrics.apiToken}" } });
        }
        private void Battlemetrics_AddPlayerNote(string steamId, string note)
        {
            string body = "{\r\n    \"data\": {\r\n        \"type\": \"playerNote\",\r\n        \"attributes\": {\r\n            \"note\": \"{NOTE}\",\r\n            \"shared\": true\r\n        },\r\n        \"relationships\": {\r\n            \"organization\": {\r\n                \"data\": {\r\n                    \"id\": \"67170\",\r\n                    \"type\": \"organization\"\r\n                }\r\n            },\r\n            \"player\": {\r\n                \"data\": {\r\n                    \"id\": \"{PLAYERID}\",\r\n                    \"type\": \"player\"\r\n                }\r\n            }\r\n        }\r\n    }\r\n}";
            body = body.Replace("{PLAYERID}", steamId)
                .Replace("{NOTE}",note);

            webrequest.Enqueue($"https://api.battlemetrics.com/players/{bmCache[steamId]}/relationships/notes", body, (code, response) =>
            {
                if (code != 201 || response == null){LogError($"[Battlemetrics_AddPlayerNote] Failed to add note: {response}\n Payload: {body}");return;}
            }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Authorization", $"Bearer {config.Battlemetrics.apiToken}" } });

        }
        private void Battlemetrics_CreateFlags()
        {
            string body = "{\"data\":{\"type\":\"playerFlag\",\"attributes\":{\"color\":\"#ff0000\",\"name\":\"SmartWarnings\",\"description\":\"This player has received warnings - SmartWarnings integration\",\"icon\":\"priority_high\"},\"relationships\":{\"organization\":{\"data\":{\"type\":\"organization\",\"id\":\"{ORGID}\"}}}}}";
            body = body.Replace("{ORGID}", config.Battlemetrics.OrgId);

            webrequest.Enqueue($"https://api.battlemetrics.com/player-flags", body, (code, response) =>
            {
                if (code != 201 || response == null) { LogError($"[Battlemetrics_CreateFlags] Failed to create flags: {response}\n Payload: {body}"); return; }
                string id = (string)JObject.Parse(response)["data"]["id"];
                bmFlags.Add("SmartWarnings", id);
            }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Authorization", $"Bearer {config.Battlemetrics.apiToken}" } });
        }

        private void Battlemetrics_LoadFlags()
        {
            webrequest.Enqueue($"https://api.battlemetrics.com/player-flags", null, (code, response) =>
            {
                if (code != 200 || response == null) { LogError($"[Battlemetrics_CreateFlags] Failed to read flags: {response}"); return; }
                foreach (var flag in JObject.Parse(response)["data"])
                {
                    if (flag["attributes"]["description"].ToString().Contains("SmartWarnings integration"))
                    {
                        bmFlags.Add(flag["attributes"]["name"].ToString(), flag["id"].ToString());
                    }
                }
                if(bmFlags.Count == 0){Battlemetrics_CreateFlags();}
                Puts("[Battlemetrics] Loaded integration successfully.");
            }, this, RequestMethod.GET, new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Authorization", $"Bearer {config.Battlemetrics.apiToken}" } });
        }
        
        private void Battlemetrics_AddPlayerFlag(string bmplayerId, string flagId)
        {
            string body = "{\"data\":[{\"type\":\"playerFlag\",\"id\":\"{FLAGID}\"}]}";
            body = body.Replace("{FLAGID}", bmFlags[flagId]);

            webrequest.Enqueue($"https://api.battlemetrics.com/players/{bmplayerId}/relationships/flags", body, (code, response) =>
            {
                if (code != 201 || response == null){LogError($"[Battlemetrics_AddPlayerFlag] Failed to add flag: {response}\n Payload: {body}");return;}
            }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Authorization", $"Bearer {config.Battlemetrics.apiToken}" } });
        }

        private void Battlemetrics_RemovePlayerFlag(string bmplayerId, string flagId)
        {
            string body = "{\"data\":[{\"type\":\"playerFlag\",\"id\":\"{FLAGID}\"}]}";
            body = body.Replace("{FLAGID}", bmFlags[flagId]);

            webrequest.Enqueue($"https://api.battlemetrics.com/players/{bmplayerId}/relationships/flags/{bmFlags[flagId]}", body, (code, response) =>
            {
                if (code != 200 || code != 400 || response == null) { LogError($"[Battlemetrics_RemovePlayerFlag] Failed to remove flag: {response}\n Payload: {body}"); return; }
            }, this, RequestMethod.DELETE, new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Authorization", $"Bearer {config.Battlemetrics.apiToken}" } });
        }

        enum bmPlayerAction
        {
            AddNote,
            AddFlag,
            RemoveFlag,
            BanPlayer
        }
        private void Battlemetrics(string steamId, bmPlayerAction action, params object[] args)
        {
            if (!bmCache.ContainsKey(steamId))
            {
                string body = "{\r\n  \"data\": [\r\n    {\r\n      \"type\": \"identifier\",\r\n      \"attributes\": {\r\n        \"type\": \"steamID\",\r\n        \"identifier\": \"{STEAMID}\"\r\n      }\r\n    }\r\n  ]\r\n}";
                body = body.Replace("{STEAMID}", steamId);
                webrequest.Enqueue("https://api.battlemetrics.com/players/match", body, (code, response) =>
                {
                    try
                    {
                        if (code != 200 || response == null)
                        {
                            LogError($"[Battlemetrics] Failed to get player information: {response}");
                            return;
                        }
                        JObject jsonPayload = JObject.Parse(response);
                        JToken data = jsonPayload["data"];
                        string playerId = (string)data[0]["relationships"]["player"]["data"]["id"]; 

                        try { bmCache.Add(steamId, playerId); } catch { }
                        Battlemetrics_HandleAction(steamId, code, response, action, args);
                    } catch
                    {
                        //LogError($"[Battlemetrics] An unexpected error occurred when performing action: {action}\nargs: {string.Join(" ", args)}\nresponse: {response}\n{ex}");
                    }
                }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Authorization", $"Bearer {config.Battlemetrics.apiToken}" } });
            } else
            {
                Battlemetrics_HandleAction(steamId, 200, null, action, args);
            }
            
        }

        private void Battlemetrics_HandleAction(string steamId, int code, string response, bmPlayerAction action, params object[] args)
        {
            try
            {
                switch (action)
                {
                    case bmPlayerAction.AddNote:
                        Battlemetrics_AddPlayerNote(steamId, args[0].ToString());
                        break;
                    case bmPlayerAction.AddFlag:
                        Battlemetrics_AddPlayerFlag(bmCache[steamId], args[0].ToString());
                        break;
                    case bmPlayerAction.RemoveFlag:
                        Battlemetrics_RemovePlayerFlag(bmCache[steamId], args[0].ToString());
                        break;
                    case bmPlayerAction.BanPlayer:
                        Battlemetrics_BanPlayer(steamId, args[0].ToString(), args[1].ToString(), (DateTime)args[2], args[3].ToString(), bmCache[steamId]);
                        break;
                    default: break;
                }
            }
            catch (Exception ex)
            {
                LogError($"[Battlemetrics_HandleAction] Failed to perform callback action: {action} Error: {ex}");
            }
        }
        #endregion

        #region Format Helpers
        public static string GenerateUID(){const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";var random = new System.Random();return new string(Enumerable.Repeat(chars, 10).Select(s => s[random.Next(s.Length)]).ToArray());}
        public static string StripTags(string original) => Formatter.ToPlaintext(original);
        public static string FormatText(string original) => Instance.covalence.FormatText(original);
        public static void ReplySafe(IPlayer player, string message) { player.Reply(player.IsServer ? StripTags(message) : FormatText(message)); }
        #endregion

        ////////////////////////////////////////
        ///     Data Related
        ////////////////////////////////////////

        void LoadData<T>(ref T data, string filename = "?") => data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename == "?" ? this.Title : filename);

        void SaveData<T>(ref T data, string filename = "?", string playerid = "0")
        { 
            if (!config.System.UseMySQL) { 
                Interface.Oxide.DataFileSystem.WriteObject(filename == "?" ? this.Title : filename, data); 
            } 
            else 
            { 
                if(Convert.ToUInt64(playerid) > 0)
                {
                    SavePlayerSQL(Convert.ToUInt64(playerid));
                } else
                {
                    SaveAllSQL();
                }
             } 
        }

        ////////////////////////////////////////
        ///     Message Related
        ////////////////////////////////////////

        string msg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());

        ////////////////////////////////////////
        ///     Permission Related
        ////////////////////////////////////////

        void RegisterPerm(params string[] permArray)
        {
            string perm = string.Join(".", permArray);
            permission.RegisterPermission($"{PermissionPrefix}.{perm}", this);
        }

        bool HasPerm(object uid, params string[] permArray)
        {
            if (uid.ToString().Equals("server_console")) { return true; }
            string perm = string.Join(".", permArray);
            return permission.UserHasPermission(uid.ToString(), $"{PermissionPrefix}.{perm}");
        }

        string PermissionPrefix
        {
            get
            {
                return this.Title.Replace(" ", "").ToLower();
            }
        }

        #endregion
    }
}