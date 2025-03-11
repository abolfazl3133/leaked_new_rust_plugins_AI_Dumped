using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.Networking;
using Time = Oxide.Core.Libraries.Time;

namespace Oxide.Plugins
{
    [Info("Discord Report", "misticos", "1.2.0")]
    [Description("Send reports from players ingame to a Discord channel")]
    class DiscordReport : CovalencePlugin
    {
        #region Variables

        [PluginReference("PlaceholderAPI")]
        private Plugin _placeholders = null;

        private Action<IPlayer, StringBuilder, bool> _placeholderProcessor = null;

        private static DiscordReport _ins;

        private const string SteamProfileXML = "https://steamcommunity.com/profiles/{0}?xml=1";
        private const string SteamProfile = "https://steamcommunity.com/profiles/";

        private readonly Regex _steamProfileIconRegex =
            new Regex(@"(?<=<avatarIcon>[\w\W]+)https://.+\.jpg(?=[\w\W]+<\/avatarIcon>)", RegexOptions.Compiled);

        private Dictionary<string, uint> _cooldownData = new Dictionary<string, uint>();

        private Time _time = GetLibrary<Time>();

        private const string PermissionIgnoreCooldown = "discordreport.ignorecooldown";
        private const string PermissionUse = "discordreport.use";
        private const string PermissionAdmin = "discordreport.admin";

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Webhook URL")]
            public string Webhook = "YOUR WEBHOOK LINK HERE";

            [JsonProperty("Message Content")]
            public string MessageContent = "";

            [JsonProperty("Embed Title")]
            public string EmbedTitle = "My Server Report";

            [JsonProperty("Embed Description")]
            public string EmbedDescription = "Report sent by a player from your server";

            [JsonProperty("Embed Color")]
            public int EmbedColor = 1484265;

            [JsonProperty("Set Author Icon From Player Profile")]
            public bool AuthorIcon = true;

            [JsonProperty("Use Reporter (True) Or Suspect (False) As Author")]
            public bool IsReporterIcon = true;

            [JsonProperty("Allow Reporting Admins")]
            public bool ReportAdmins = false;

            [JsonProperty("Report Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ReportCommands = new List<string> { "report" };

            [JsonProperty("Allow Only Online Suspects Reports")]
            public bool OnlyOnlineSuspects = true;

            [JsonProperty("Threshold Before Sending Reports")]
            public int Threshold = 0;

#if RUST
            [JsonProperty("Show Recent Suspect Combatlog")]
            public bool ShowCombatlog = false;

            [JsonProperty("Show In-Game Subject")]
            public bool ShowInGameSubject = true;

            [JsonProperty("Minimum In-Game Report Subject Length")]
            public int SubjectMinimumGame = 0;

            [JsonProperty("Minimum In-Game Report Message Length")]
            public int MessageMinimumGame = 0;

            [JsonProperty("Allowed In-Game Report Types", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] AllowedTypes = { "cheat", "abusive", "name", "spam" };

            [JsonProperty("Recent Combatlog Entries")]
            public int CombatlogEntries = 2;
#endif

            [JsonProperty("Cooldown In Seconds")]
            public uint Cooldown = 300;

            [JsonProperty("User Cache Validity In Seconds")]
            public uint UserCacheValidity = 86400;

            [JsonProperty("Minimum Message Length")]
            public int MessageMinimum = 0;
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

        #region Work with Data

        private Dictionary<string, PlayerData> _loadedData = new Dictionary<string, PlayerData>();

        private void SaveData(string id)
        {
            PlayerData data;
            if (!_loadedData.TryGetValue(id, out data))
                return;

            Interface.Oxide.DataFileSystem.WriteObject(nameof(DiscordReport) + '/' + id, data);
        }

        private PlayerData GetOrLoadData(string id)
        {
            PlayerData data;
            if (_loadedData.TryGetValue(id, out data))
                return data;

            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>(nameof(DiscordReport) + '/' + id);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            return _loadedData[id] = data ?? new PlayerData();
        }

        private class PlayerData
        {
            public string ImageURL = string.Empty;
            public string LastKnownAddress = string.Empty;

            public HashSet<string> Reporters = new HashSet<string>();

            public uint LastImageUpdate = 0;
        }

        #endregion

        #region Discord Classes

        // ReSharper disable NotAccessedField.Local

        private class WebhookBody
        {
            [JsonProperty("embeds")]
            public List<EmbedBody> Embeds;

            [JsonProperty("content")]
            public string Content = null;
        }

        private class EmbedBody
        {
            [JsonProperty("title")]
            public string Title;

            [JsonProperty("type")]
            public string Type;

            [JsonProperty("description")]
            public string Description;

            [JsonProperty("color")]
            public int Color;

            [JsonProperty("author")]
            public AuthorBody Author;

            [JsonProperty("fields")]
            public List<FieldBody> Fields;

            public class AuthorBody
            {
                [JsonProperty("name")]
                public string Name;

                [JsonProperty("url")]
                public string AuthorURL;

                [JsonProperty("icon_url")]
                public string AuthorIconURL;
            }

            public class FieldBody
            {
                [JsonProperty("name")]
                public string Name;

                [JsonProperty("value")]
                public string Value;

                [JsonProperty("inline")]
                public bool Inline;
            }
        }

        // ReSharper restore NotAccessedField.Local

        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Webhook: Reporter Data Title", "Reporter" },
                { "Webhook: Suspect Data Title", "Suspect" },
#if RUST
                { "Webhook: Combatlog Title", "Suspect's Combatlog #{n}" },
                { "Webhook: Combatlog Attacker Title", "Attacker" },
                { "Webhook: Combatlog Target Title", "Target" },
                { "Webhook: Combatlog Time Title", "Time" },
                { "Webhook: Combatlog Weapon Title", "Weapon" },
                { "Webhook: Combatlog Ammo Title", "Ammo" },
                { "Webhook: Combatlog Distance Title", "Distance" },
                { "Webhook: Combatlog Old HP Title", "Old HP" },
                { "Webhook: Combatlog New HP Title", "New HP" },
                { "Webhook: Combatlog Info Title", "Info" },
#endif
                {
                    "Webhook: Reporter Data", "#{discordreport.total} {name} ({id}). IP: {ip}.\n" +
                                              "Ping: {ping}ms. Connected: {connected}"
                },
                {
                    "Webhook: Suspect Data", "#{discordreport.total} {name} ({id}). IP: {ip}.\n" +
                                             "Ping: {ping}ms. Connected: {connected}"
                },
                { "Webhook: Report Subject", "Report Subject" },
                { "Webhook: Report Message", "Report Message" },
                {
                    "Command: Syntax", "Syntax:\n" +
                                       "report (ID / \"Name\") (Message)\n" +
                                       "WARNING! Use quotes for names."
                },
                { "Command: User Not Found", "We were unable to find this user or multiple were found." },
                { "Command: Report Sent", "Thank you for your report, it was sent to our administration." },
                { "Command: Exceeded Cooldown", "You have exceeded your cooldown on reports." },
                { "Command: Cannot Report Admins", "You cannot report admins." },
                { "Command: Cannot Use", "You cannot use this command since you do not have enough permissions." },
                { "Command: Message Length", "Please add more information to the message." }
            }, this);
        }

        private void Init()
        {
            _ins = this;
            
            permission.RegisterPermission(PermissionIgnoreCooldown, this);
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);

        }

        private void Loaded()
        {
            foreach (var player in players.Connected)
                OnUserConnected(player);
        }

        private void Unload()
        {
            _ins = null;
            foreach (var check in BasePlayer.allPlayerList)
                CuiHelper.DestroyUi(check, Layer);
                
        }

        private void OnUserConnected(IPlayer player)
        {
            var user = GetOrLoadData(player.Id);

            if (_config.AuthorIcon)
                UpdateCachedImage(player, user);

            user.LastKnownAddress = player.Address;

            SaveData(player.Id);
        }

        private void OnPlaceholderAPIReady()
        {
            _placeholderProcessor =
                _placeholders.Call<Action<IPlayer, StringBuilder, bool>>("GetProcessPlaceholders", 1);

            _placeholders.Call("AddPlaceholder", this, "discordreport.total", new Func<IPlayer, string, object>(
                (player, s) => GetOrLoadData(player.Id).Reporters.Count));
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin?.Name != "PlaceholderAPI")
                return;

            _placeholderProcessor = null;
        }


        #endregion


        #region Helpers

        private void UpdateCachedImage(IPlayer player, PlayerData data)
        {
            var now = _time.GetUnixTimestamp();

            // If cached and still valid, return
            if (!string.IsNullOrEmpty(data.ImageURL) &&
                data.LastImageUpdate + _config.UserCacheValidity > now)
                return;

            webrequest.Enqueue(string.Format(SteamProfileXML, player.Id), string.Empty,
                (code, result) =>
                {
                    data.ImageURL = _steamProfileIconRegex.Match(result).Value;
                    data.LastImageUpdate = now;

                    SaveData(player.Id);
                },
                this);
        }

        #region UI
        
        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
                return;
            
            CuiHelper.DestroyUi(player, Layer);
        }
        
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || player.IsBot || info.InitiatorPlayer == null || info.InitiatorPlayer.IsBot)
                return;

            timer.Once(2f, () => ShowUIBG(player, info.InitiatorPlayer));
        }
        
        [ConsoleCommand("UI_DRREPORT")]
        private void cmdConsoleUI_DRREPORT(ConsoleSystem.Arg arg)
        {
            var iPlayer = arg.Player().IPlayer;
            var iInitiator = BasePlayer.FindAwakeOrSleeping(arg.GetString(0)).IPlayer;

            var field = new WWWForm();
            field.AddField("content", $"{iInitiator.Name}[{iInitiator.Id}] подозревается в читерстве игроком {iPlayer.Name}[{iPlayer.Id}]");

            var request = UnityWebRequest.Post(_config.Webhook, field);
            request.SendWebRequest();

            CuiHelper.DestroyUi(arg.Player(), Layer);
        }

        private string Layer = "DiscordReport_UI";

        
        private void ShowUIBG(BasePlayer player, BasePlayer initiator)
        {
			if (player == null || initiator == null)
				return; 
			
            var container = new CuiElementContainer();
        
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "176 294", OffsetMax = "246 324"
                },
                Text =
                {
                    Text = "РЕПОРТ",
                    FontSize = 12, Color = "1 1 1 1",
                    Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                },
                Button =
                {
                    Command = $"UI_DRREPORT {initiator.userID}",
                    Color = "0 0 0 1",
                },
            }, "Overlay", Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Webhook

        private void SendReport(IPlayer reporter, IPlayer suspect, string subject, string message)
        {
            // Threshold

            var cached = GetOrLoadData(suspect.Id);

            cached.Reporters.Add(reporter.Id);
            if (cached.Reporters.Count < _config.Threshold)
                return;

            // Author Icon

            var authorIconURL = string.Empty;
            var author = _config.IsReporterIcon ? reporter : suspect;
            if (_config.AuthorIcon)
            {
                var user = GetOrLoadData(author.Id);
                if (!string.IsNullOrEmpty(user.ImageURL))
                    authorIconURL = user.ImageURL;

                UpdateCachedImage(author, user); // Won't get update now but will be for any other reports for this user
            }

            // Embed data

            const string type = "rich";
            var body = new WebhookBody
            {
                Embeds = Pool.GetList<EmbedBody>(),
                Content = string.IsNullOrEmpty(_config.MessageContent) ? null : _config.MessageContent
            };

            var fields = Pool.GetList<EmbedBody.FieldBody>();

            if (!string.IsNullOrEmpty(subject))
            {
                fields.Add(new EmbedBody.FieldBody
                {
                    Name = GetMsg("Webhook: Report Subject"),
                    Value = subject,
                    Inline = false
                });
            }

            if (!string.IsNullOrEmpty(message))
            {
                fields.Add(new EmbedBody.FieldBody
                {
                    Name = GetMsg("Webhook: Report Message"),
                    Value = message,
                    Inline = false
                });
            }

            fields.Add(new EmbedBody.FieldBody
            {
                Name = GetMsg("Webhook: Reporter Data Title"),
                Value =
                    FormatUserDetails(new StringBuilder(GetMsg("Webhook: Reporter Data")), reporter),
                Inline = false
            });

            fields.Add(new EmbedBody.FieldBody
            {
                Name = GetMsg("Webhook: Suspect Data Title"),
                Value = FormatUserDetails(new StringBuilder(GetMsg("Webhook: Suspect Data")), suspect),
                Inline = false
            });

            body.Embeds.Add(new EmbedBody
            {
                Title = _config.EmbedTitle,
                Description = _config.EmbedDescription,
                Type = type,
                Color = _config.EmbedColor,
                Author = new EmbedBody.AuthorBody
                {
                    AuthorIconURL = authorIconURL,
                    AuthorURL = SteamProfile + author.Id,
                    Name = author.Name
                },
                Fields = fields
            });

            // Rust-specific embed data

#if RUST
            if (_config.ShowCombatlog && suspect.Object is BasePlayer)
            {
                var events = CombatLog.Get(((BasePlayer)suspect.Object).userID).ToArray();
                for (var i = 1; i <= _config.CombatlogEntries && i <= events.Length; i++)
                {
                    var combat = events[events.Length - i];

                    var combatFields = Pool.GetList<EmbedBody.FieldBody>();

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Attacker Title"),
                        Value = combat.attacker,
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Target Title"),
                        Value = combat.target,
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Time Title"),
                        Value = (UnityEngine.Time.realtimeSinceStartup - combat.time).ToString("0.0s"),
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Weapon Title"),
                        Value = combat.weapon,
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Ammo Title"),
                        Value = combat.ammo,
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Distance Title"),
                        Value = combat.distance.ToString("0.0m"),
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Old HP Title"),
                        Value = combat.health_old.ToString("0.0"),
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog New HP Title"),
                        Value = combat.health_new.ToString("0.0"),
                        Inline = true
                    });

                    combatFields.Add(new EmbedBody.FieldBody
                    {
                        Name = GetMsg("Webhook: Combatlog Info Title"),
                        Value = string.IsNullOrEmpty(combat.info) ? "none" : combat.info,
                        Inline = true
                    });

                    body.Embeds.Add(new EmbedBody
                    {
                        Title = GetMsg("Webhook: Combatlog Title").Replace("{n}", $"{i}"),
                        Type = type,
                        Color = _config.EmbedColor,
                        Fields = combatFields
                    });
                }
            }
#endif

            // Send a web request

            webrequest.Enqueue(_config.Webhook, JObject.FromObject(body).ToString(),
                (code, result) =>
                {
                    if (code == 204)
                    {
                        SetCooldown(reporter);
                    }
                    else
                    {
                        PrintWarning($"Discord Webhook returned {code}:\n{result}");
                    }
                }, this, RequestMethod.POST,
                new Dictionary<string, string> { { "Content-Type", "application/json" } });

            foreach (var embed in body.Embeds)
            {
                Pool.FreeList(ref embed.Fields);
            }

            Pool.FreeList(ref body.Embeds);
        }

        private string FormatUserDetails(StringBuilder builder, IPlayer player)
        {
            // Apply placeholders if possible
            _placeholderProcessor?.Invoke(player, builder, false);

            return builder
                .Replace("{name}", player.Name).Replace("{id}", player.Id)
                .Replace("{ip}", GetOrLoadData(player.Id)?.LastKnownAddress ?? "Unknown")
                .Replace("{ping}", player.IsConnected ? player.Ping.ToString() : "0")
                .Replace("{connected}", player.IsConnected.ToString()).ToString();
        }

        #endregion

        #region Cooldown

        private bool ExceedsCooldown(IPlayer player)
        {
            if (player.HasPermission(PermissionIgnoreCooldown))
                return false;

            var currentTime = _time.GetUnixTimestamp();
            if (_cooldownData.ContainsKey(player.Id))
                return _cooldownData[player.Id] - currentTime < _config.Cooldown;

            return false;
        }

        private void SetCooldown(IPlayer player) => _cooldownData[player.Id] = _time.GetUnixTimestamp();

        #endregion

        private bool CanUse(IPlayer player) => player.HasPermission(PermissionUse);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}