using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.Data;
using Oxide.Ext.Chaos.Json;
using Oxide.Ext.Discord.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Ext.Chaos.UIFramework;
using Oxide.Ext.Discord.Clients;
using Oxide.Ext.Discord.Connections;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Interfaces;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

using Chaos = Oxide.Ext.Chaos;
using Color = Oxide.Ext.Chaos.UIFramework.Color;
using Font = Oxide.Ext.Chaos.UIFramework.Font;
using VerticalLayoutGroup = Oxide.Ext.Chaos.UIFramework.VerticalLayoutGroup;
using HorizontalLayoutGroup = Oxide.Ext.Chaos.UIFramework.HorizontalLayoutGroup;
using Layer = Oxide.Ext.Chaos.UIFramework.Layer;

namespace Oxide.Plugins
{
    [Info("Discord Rewards", "k1lly0u", "2.0.3")]
    [Description("Reward players with items, kits and commands for being a member of your Discord")]
    class DiscordRewards : ChaosPlugin, IDiscordPlugin
    {
        #region Fields
        public DiscordClient Client { get; set; }

        private static DiscordGuild Guild;
        private static DiscordRole NitroRole;
        private DiscordChannel ValidationChannel;

        private Datafile<UserData> userData;
        private Datafile<RewardData> rewardData;

        [Chaos.Permission]
        private const string ADMIN_PERMISSION = "discordrewards.admin";

        private const int BLUEPRINT_ITEM_ID = -996920608;
        
        private bool discordConnected = false;
        private bool serverInitialized = false;
        
        private bool needsWipe = false;
        private int statusIndex = 0;

        private readonly RewardType[] rewardTypes = (RewardType[])Enum.GetValues(typeof(RewardType));

        private static Func<DiscordClient> ClientInstance;

        private enum RewardType
        {
            Item,
            Kit,
            Command
        }
        
        #endregion

        #region Oxide Hooks

        private void Init()
        {
            userData = new Datafile<UserData>("DiscordRewards/userdata");
            rewardData = new Datafile<RewardData>("DiscordRewards/rewarddata");
            
            ClientInstance ??= () => Client;
            
            if (rewardData.Data.ValidateRewards())
                rewardData.Save();

            SetupPermissionHelpers();
            SetupUIComponents();
        }

        private void OnServerInitialized()
        {
            if (needsWipe)
            {
                if (Configuration.Token.WipeReset)
                    WipeData();
                else if (Configuration.Token.WipeResetRewards)
                    WipeRewardCooldowns();
            }

            serverInitialized = true;
            
            if (discordConnected)
                RegisterImages();
        }

        private void OnServerSave() => userData.Save();

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player)
                return;

            ValidateUser(player);
            UpdateStatus();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            UpdateStatus();
            DequeueRevalidation(player);
        }

        private void OnNewSave(string filename) => needsWipe = true;

        private void Unload()
        {
            userData.Save();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                ChaosUI.Destroy(player, UI_MENU);

            Client?.Disconnect();
            Client = null;
            Guild = null;
            NitroRole = null;
        }

        #endregion

        #region Discord Hooks

        private void OnDiscordClientCreated()
        {
            if (string.IsNullOrEmpty(Configuration.Settings.APIKey))
            {
                Debug.LogError("[Discord Rewards] No API token set in config... Unable to continue!");
                return;
            }

            if (string.IsNullOrEmpty(Configuration.Settings.BotID))
            {
                Debug.LogError("[Discord Rewards] No bot client ID set in config... Unable to continue!");
                return;
            }

            Debug.Log("[Discord Rewards] Establishing connection to your Discord server...");

            BotConnection settings = new BotConnection();
            settings.ApiToken = Configuration.Settings.APIKey;
            settings.LogLevel = Configuration.Settings.LogLevel;
            settings.Intents = GatewayIntents.Guilds | GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.GuildMembers | GatewayIntents.MessageContent;

            Client.Connect(settings);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildCreated)]
        private void OnDiscordGuildCreated(DiscordGuild guild)
        {
            if (guild == null)
            {
                Debug.LogError("[Discord Rewards] Failed to connect to guild. Unable to continue...");
                return;
            }

            Guild = guild;

            Debug.Log($"[Discord Rewards] Connection to {Guild.Name} established");
        }

        [HookMethod(DiscordExtHooks.OnDiscordBotFullyLoaded)]
        private void OnDiscordBotFullyLoaded()
        {
            Debug.Log($"[Discord Rewards] Client fully loaded! DiscordRewards is now active");
            
            NitroRole = GetBoosterRole();

            Debug.Log(NitroRole == null ? "[Discord Rewards] No nitro role found" : $"[Discord Rewards] Nitro role found {NitroRole.Name} ({NitroRole.Id})");

            if (!string.IsNullOrEmpty(Configuration.Token.ValidationChannel))
                ValidationChannel = Guild.GetChannel(Configuration.Token.ValidationChannel);

            if (serverInitialized)
                RegisterImages();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                ValidateUser(player);

            UpdateStatus();

            discordConnected = true;
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMemberUpdated)]
        private void OnDiscordGuildMemberUpdated(GuildMember update, GuildMember previous, DiscordGuild guild)
        {
            if (guild != Guild)
                return;

            if (!userData.Data.FindById(update.Id, out ulong steamId)) 
                return;

            UserData.User user = userData.Data.GetUser(steamId);
            if (user != null)
            {
                bool wasNitroBooster = user.IsNitroBooster;
                bool isNitroBooster = NitroRole != null && update.HasRole(NitroRole);

                if (wasNitroBooster == isNitroBooster)
                    return;
                
                user.IsNitroBooster = isNitroBooster;
                
                BasePlayer player = FindPlayer(steamId);
                if (!player)
                    return;
                
                Configuration.Rewards.UpdateNitroRewards(update, player, user, wasNitroBooster);
            }
        }

        private DiscordRole GetBoosterRole()
        {
            foreach (DiscordRole role in Guild.Roles.Values)
            {
                if (role.IsBoosterRole())
                    return role;
            }

            return null;
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMemberRemoved)]
        private void OnDiscordGuildMemberRemoved(GuildMemberRemovedEvent member, DiscordGuild guild)
        {
            if (userData.Data.FindById(member.User.Id, out ulong steamId))
            {
                if (userData.Data.HasPendingToken(steamId, out int code))
                    userData.Data.InvalidateToken(code);

                UserData.User user = userData.Data.GetUser(steamId);
                if (user != null)
                    Configuration.Rewards.RevokeRewards(steamId, user);
            }
        }

        [HookMethod(DiscordExtHooks.OnDiscordDirectMessageCreated)]
        private void OnDiscordDirectMessageCreated(DiscordMessage message, DiscordChannel channel)
        {
            if (message == null || message.Author.Bot == true)
                return;

            if (int.TryParse(message.Content, out int code) && AttemptTokenValidation(message.Author, code))
                return;

            message.Author.SendDirectMessage(Client, GetString("Discord.InvalidToken", string.Empty));
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMessageCreated)]
        private void OnDiscordGuildMessageCreated(DiscordMessage message, DiscordChannel channel, DiscordGuild guild)
        {
            if (message == null || message.Author.Bot == true)
                return;

            if (ValidationChannel == null || channel != ValidationChannel)
                return;

            if (int.TryParse(message.Content, out int code) && AttemptTokenValidation(message.Author, code))
            {
                message.Delete(Client);
                return;
            }

            message.Author.SendDirectMessage(Client, GetString("Discord.InvalidToken", string.Empty));
            message.Delete(Client);
        }

        #endregion

        #region Token Validation

        private bool AttemptTokenValidation(DiscordUser discordUser, int code)
        {
            if (userData.Data.IsValidToken(code, out UserData.DiscordToken token))
            {
                BasePlayer player = FindPlayer(token.UserId);

                if (token.ExpireTime < CurrentTime())
                {
                    discordUser.SendDirectMessage(Client, GetString("Discord.TokenExpired", player));
                    userData.Data.InvalidateToken(code);
                    return true;
                }

                if (!player)
                {
                    discordUser.SendDirectMessage(Client, FormatString("Discord.FailedToFindPlayer", player, token.UserId));
                    return true;
                }

                if (!player.IsConnected)
                {
                    discordUser.SendDirectMessage(Client, GetString("Discord.NotOnServer", player));
                    return true;
                }

                if (player.IsDead())
                {
                    discordUser.SendDirectMessage(Client, GetString("Discord.UserIsDead", player));
                    return true;
                }

                userData.Data.InvalidateToken(code);

                UserData.User user = userData.Data.GetUser(token.UserId);
                if (user == null)
                {
                    user = userData.Data.AddNewUser(token.UserId, discordUser.Id);
                    user.AddTokens(Configuration.Rewards.RewardTokensInitial);
                }
                else user.AddTokens(Configuration.Rewards.RewardTokensPerValidation);

                user.SetExpiryDate(Configuration.Token.RevalidationInterval);

                string response = GetString("Discord.ValidatedToken", player);

                if (Configuration.Token.RequireRevalidation)
                    response += $" {FormatString("Discord.TokenExpires", player, FormatTime(Configuration.Token.RevalidationInterval))}";

                if (Configuration.UISettings.Enabled)
                {
                    response += $" {GetString("Discord.OpenStore", player)}";

                    if (!OpenStore(player))
                        ChaosUI.Destroy(player, UI_MENU);
                }

                discordUser.SendDirectMessage(Client, response);

                if (player)
                {
                    player.ChatMessage(response);
                    Configuration.Rewards.IssueRewards(player, user);
                }

                userData.Save();
                UpdateStatus();
                return true;
            }

            return false;
        }

        private static BasePlayer FindPlayer(ulong userId)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.userID == userId)
                    return player;
            }

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
                if (player.userID == userId)
                    return player;
            }

            return null;
        }

        #endregion

        #region Status

        private void UpdateStatus()
        {
            statusIndex += 1;

            if (statusIndex >= (Configuration.Settings.StatusMessages?.Length ?? 0))
                statusIndex = 0;

            if (Client?.Bot != null)
            {
                if (Configuration.Settings.StatusMessages is { Length: > 0 })
                {
                    string message = Configuration.Settings.StatusMessages[statusIndex];

                    if (!string.IsNullOrEmpty(message))
                    {
                        string str = message.Replace("{playersMin}", BasePlayer.activePlayerList.Count.ToString())
                            .Replace("{playersMax}", ConVar.Server.maxplayers.ToString())
                            .Replace("{rewardPlayers}", (!Configuration.Token.RequireRevalidation ? userData.Data.UserCount.ToString() : userData.Data.ValidUsers.ToString()));

                        Client.UpdateStatus(new UpdatePresenceCommand
                        {
                            Activities = new List<DiscordActivity>
                            {
                                new DiscordActivity
                                {
                                    Name = str,
                                    Type = ActivityType.Game
                                }
                            }
                        });
                    }
                }
            }

            timer.In(Mathf.Clamp(Configuration.Settings.StatusCycle, 60, int.MaxValue), UpdateStatus);
        }

        #endregion

        #region Helpers

        private static GuildMember FindMember(Snowflake id)
        {
            foreach (GuildMember guildMember in Guild.Members.Values)
            {
                if (guildMember.Id.Equals(id))
                    return guildMember;
            }

            return null;
        }

        private static DiscordRole GetRoleByID(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (DiscordRole role in Guild.Roles.Values)
                {
                    if (role.Id.ToString().Equals(id, StringComparison.OrdinalIgnoreCase))
                        return role;
                }
            }

            return null;
        }

        private int GenerateToken()
        {
            int token = UnityEngine.Random.Range(100000, 999999);
            if (userData.Data.TokenToUser.ContainsKey(token))
                return GenerateToken();
            return token;
        }

        #endregion

        #region Groups and Permission Helpers

        private static Action<string, string> AddToGroup;
        private static Action<string, string> RemoveFromGroup;
        private static Func<string, bool> GroupExists;
        private static Func<string, string, bool> HasGroup;
        private static Action<string, string> GrantPermission;
        private static Action<string, string> RevokePermission;
        private static Func<string, string, bool> HasPermission;
        private static Func<string, bool> PermissionExists;

        private void SetupPermissionHelpers()
        {
            AddToGroup = permission.AddUserGroup;
            RemoveFromGroup = permission.RemoveUserGroup;
            GroupExists = permission.GroupExists;
            HasGroup = permission.UserHasGroup;
            GrantPermission = (string playerId, string perm) => permission.GrantUserPermission(playerId, perm, this);
            RevokePermission = permission.RevokeUserPermission;
            HasPermission = permission.UserHasPermission;
            PermissionExists = (string perm) => permission.PermissionExists(perm, this);
        }

        private void SyncGroupsAndPermissions(string userId, UserData.User user)
        {
            foreach (string groupId in user.Groups)
                AddToGroup(userId, groupId);

            foreach (string groupId in user.NitroGroups)
                AddToGroup(userId, groupId);

            foreach (string perm in user.Permissions)
                GrantPermission(userId, perm);

            foreach (string perm in user.NitroPermissions)
                GrantPermission(userId, perm);
        }

        #endregion

        #region User Validation

        private void ValidateUser(BasePlayer player)
        {
            if (!player)
                return;

            if (!Configuration.Token.RequireRevalidation)
                return;

            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(3, () => ValidateUser(player));
                return;
            }

            if (!userData.Data.Users.TryGetValue(player.userID, out UserData.User user))
                return;

            double timeRemaining = user.ExpireTime - CurrentTime();
            if (timeRemaining <= 0)
            {
                if (Configuration.Token.AutoRevalidation)
                {
                    if (FindMember(user.Id) != null)
                    {
                        user.SetExpiryDate(Configuration.Token.RevalidationInterval);
                        user.AddTokens(Configuration.Rewards.RewardTokensPerValidation);
                        Configuration.Rewards.IssueRewards(player, user);
                        player.LocalizedMessage(this, "Message.AutoValidated");
                        
                        timeRemaining = user.ExpireTime - CurrentTime();
                        QueueRevalidation(player, (float)timeRemaining);
                        return;
                    }
                }

                player.LocalizedMessage(this, "Message.ValidationExpired");
                Configuration.Rewards.RevokeRewards(player.userID, user);
            }
            else QueueRevalidation(player, (float)timeRemaining);
        }
        
        private readonly Hash<ulong, Timer> m_ValidationTimers = new Hash<ulong, Timer>();

        private void QueueRevalidation(BasePlayer player, float timeRemaining)
        {
            if (m_ValidationTimers.TryGetValue(player.userID, out Timer t) && t != null)
                t.Destroy();
               
            m_ValidationTimers[player.userID] = timer.Once(timeRemaining, ()=> ValidateUser(player));
        }

        private void DequeueRevalidation(BasePlayer player)
        {
            if (m_ValidationTimers.TryGetValue(player.userID, out Timer t) && t != null)
            {
                t.Destroy();
                m_ValidationTimers.Remove(player.userID);
            }
        }
        
        #endregion
        
        #region UI

        private const string UI_MENU = "discordrewards.ui";
        private const string UI_BOT_AVATAR = "discordrewards.botavatar";
        private const string UI_BOT_DEFAULT_AVATAR = "discordrewards.defaultbotavatar";

        private string m_BotAvatar;
        private CommandCallbackHandler m_CallbackHandler;

        private Style m_SegmentStyle;
        private Style m_PanelStyle;
        private Style m_ButtonStyle;
        private Style m_ButtonSelectedStyle;
        private Style m_ButtonDisabledStyle;
        private Style m_InputStyle;
        private Style m_ClaimStyle;
        private Style m_NitroStyle;
        private Style m_InsufficientStyle;
        private ScrollbarComponent.Style m_ScrollbarStyle;

        private readonly ScrollViewComponent m_ScrollView = new ScrollViewComponent
        {
            Vertical = true,
            MovementType = ScrollRect.MovementType.Clamped,
        };
        
        private readonly HorizontalLayoutGroup m_TokenLayout = new HorizontalLayoutGroup
        {
            Area = new Area(-145f, -15f, 145f, 15f),
            Spacing = new Spacing(5f, 0f),
            Padding = new Padding(0f, 0f, 0f, 0f),
            Corner = Corner.Centered,
            FixedSize = new Vector2(30, 30),
            FixedCount = new Vector2Int(6, 1),
        };
        
        private readonly VerticalLayoutGroup m_ScrollLayout = new VerticalLayoutGroup
        {
            Spacing = new Spacing(0f, 2f),
            Padding = new Padding(4f, 4f, 10f, 4f),
            Corner = Corner.TopLeft,
            IsScrollable = true,
            ViewportSize = new float2(350f, 482.5f),
            FixedSize = new Vector2(332, 56),
        };

        private readonly HorizontalLayoutGroup m_NavLayout = new HorizontalLayoutGroup(3)
        {
            Area = new Area(-175f, -12.5f, 175f, 12.5f),
            Spacing = new Spacing(2f, 0f),
            Padding = new Padding(3f, 3f, 3f, 3f),
            Corner = Corner.TopLeft,
        };

        private void SetupUIComponents()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);

            // Build Styles
            m_SegmentStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.Segment,
                FontColor = Configuration.UISettings.Colors.Text,
                FontSize = 14,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleLeft
            };

            m_PanelStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.Panel,
                FontColor = Configuration.UISettings.Colors.Text,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleLeft
            };

            m_ButtonStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.Button,
                FontColor = Configuration.UISettings.Colors.ButtonText,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter
            };

            m_ButtonSelectedStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.ButtonSelected,
                FontColor = Configuration.UISettings.Colors.ButtonSelectedText,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter
            };

            m_ButtonDisabledStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.ButtonDisabled,
                FontColor = Configuration.UISettings.Colors.ButtonDisabledText,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter
            };

            m_InputStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.Input,
                FontColor = Configuration.UISettings.Colors.InputText,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleLeft
            };

            m_ClaimStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.Claim,
                FontColor = Configuration.UISettings.Colors.ClaimText,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter
            };

            m_NitroStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.Nitro,
                FontColor = Configuration.UISettings.Colors.NitroText,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter
            };

            m_InsufficientStyle = new Style
            {
                ImageColor = Configuration.UISettings.Colors.Insufficient,
                FontColor = Configuration.UISettings.Colors.InsufficientText,
                FontSize = 12,
                Font = Font.RobotoCondensedRegular,
                Material = Materials.GreyOut,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter
            };
            
            m_ScrollbarStyle = new ScrollbarComponent.Style
            {
                Size = 10f,
                HandleSprite = Sprites.DEFAULT,
                HandleColor = Configuration.UISettings.Colors.ScrollbarHandle,
                HighlightColor = Configuration.UISettings.Colors.ScrollbarHighlight,
                PressedColor = Configuration.UISettings.Colors.ScrollbarPressed,
                TrackColor = Configuration.UISettings.Colors.ScrollbarBackground,
            };

            m_ScrollView
                .WithContentTransform(Anchor.FullStretch, new Offset(0f, 0f, -10f, 0f))
                .WithVerticalScrollbar(m_ScrollbarStyle);
        }

        #region Token
        private void CreateTokenUI(BasePlayer player, int code)
        {
            BaseContainer root = BaseContainer.Create(UI_MENU, Layer.Overlay, Anchor.Center, new Offset(-150f, -107f, 150f, 107f))
                .WithChildren(parent =>
                {
                    // Header
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -25f, 0f, 0f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(header =>
                        {
                            TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                .WithStyle(m_SegmentStyle)
                                .WithText(Title.ToUpper());

                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-22.5f, -10f, -2.5f, 10f))
                                .WithColor(Color.Clear)
                                .WithChildren(exit =>
                                {
                                    TextContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(m_ButtonStyle)
                                        .WithText("✘")
                                        .WithWrapMode(VerticalWrapMode.Overflow);

                                    ButtonContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => ChaosUI.Destroy(player, UI_MENU), $"{player.UserIDString}.token.close");
                                });
                        });

                    // Token
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -91.5f, 0f, -27.5f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(top =>
                        {
                            TextContainer.Create(top, Anchor.TopStretch, new Offset(0f, -25f, 0f, 0f))
                                .WithStyle(m_SegmentStyle)
                                .WithSize(16)
                                .WithText(GetString("UI.Token", player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ImageContainer.Create(top, Anchor.TopStretch, new Offset(4f, -59.5f, -4f, -25f))
                                .WithStyle(m_PanelStyle);

                            char[] chars = code.ToString().ToCharArray();

                            BaseContainer.Create(top, Anchor.TopStretch, new Offset(0f, -60f, 0f, -25f))
                                .WithLayoutGroup(m_TokenLayout, chars, 0, (int i, char t, BaseContainer token, Anchor anchor, Offset offset) =>
                                {
                                    ImageContainer.Create(token, anchor, offset)
                                        .WithStyle(m_SegmentStyle)
                                        .WithChildren(number =>
                                        {
                                            TextContainer.Create(number, Anchor.FullStretch, Offset.zero)
                                                .WithSize(25)
                                                .WithText($"{t}")
                                                .WithAlignment(TextAnchor.MiddleCenter);
                                        });
                                });
                        });

                    // Bottom
                    ImageContainer.Create(parent, Anchor.TopStretch, ValidationChannel != null ? new Offset(0f, -214f, 0f, -94f) : new Offset(0f, -172f, 0f, -94f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(bottom =>
                        {
                            TextContainer.Create(bottom, Anchor.TopStretch, new Offset(4f, -44f, -4f, -4f))
                                .WithStyle(m_SegmentStyle)
                                .WithText(GetString("UI.VerifyExplain", player));

                            ImageContainer.Create(bottom, Anchor.TopStretch, new Offset(4f, -74f, -4f, -44f))
                                .WithStyle(m_PanelStyle)
                                .WithChildren(botUser =>
                                {
                                    if (!string.IsNullOrEmpty(m_BotAvatar))
                                    {
                                        RawImageContainer.Create(botUser, Anchor.CenterLeft, new Offset(8f, -12f, 32f, 12f))
                                            .WithPNG(m_BotAvatar);
                                    }

                                    TextContainer.Create(botUser, Anchor.FullStretch, new Offset(40f, 0f, 0f, 0f))
                                        .WithStyle(m_SegmentStyle)
                                        .WithText(Client.Bot.BotUser.Username);
                                });

                            if (ValidationChannel != null)
                            {
                                TextContainer.Create(bottom, Anchor.TopStretch, new Offset(4f, -116.9f, -4f, -76.9f))
                                    .WithStyle(m_SegmentStyle)
                                    .WithText(FormatString("UI.Verify.Channel", player, ValidationChannel.Name))
                                    .WithAlignment(TextAnchor.MiddleLeft);
                            }
                        });
                })
                .DestroyExisting()
                .NeedsCursor();

            ChaosUI.Show(player, root);
        }

        #endregion

        #region Store

        private bool OpenStore(BasePlayer player)
        {
            if (rewardData.Data.Items.Count > 0)
            {
                CreateStoreUI(player, rewardData.Data.Items);
                return true;
            }

            if (rewardData.Data.Kits.Count > 0)
            {
                CreateStoreUI(player, rewardData.Data.Kits);
                return true;
            }

            if (rewardData.Data.Commands.Count > 0)
            {
                CreateStoreUI(player, rewardData.Data.Commands);
                return true;
            }

            return false;
        }

        private void CreateStoreUI<T>(BasePlayer player, List<T> list, bool isAdmin = false) where T : RewardData.BaseReward
        {
            if (!userData.Data.Users.TryGetValue(player.userID, out UserData.User user))
                return;

            Type type = typeof(T);

            RewardType rewardType =
                type == typeof(RewardData.RewardItem) ? RewardType.Item :
                type == typeof(RewardData.RewardKit) ? RewardType.Kit :
                RewardType.Command;

            BaseContainer root = BaseContainer.Create(UI_MENU, Layer.Overall, Anchor.Center, new Offset(-175f, -282.5f, 175f, 282.5f))
                .WithChildren(parent =>
                {
                    // Header
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -25f, 0f, 0f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(header =>
                        {
                            TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                .WithStyle(m_SegmentStyle)
                                .WithText(Title.ToUpper());

                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-22.5f, -10f, -2.5f, 10f))
                                .WithColor(Color.Clear)
                                .WithChildren(exit =>
                                {
                                    TextContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(m_ButtonStyle)
                                        .WithText("✘")
                                        .WithWrapMode(VerticalWrapMode.Overflow);

                                    ButtonContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => ChaosUI.Destroy(player, UI_MENU), $"{player.UserIDString}.menu.close");
                                });
                        });

                    // Navigation
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -52.5f, 0f, -27.5f))
                        .WithStyle(m_SegmentStyle)
                        .WithLayoutGroup(m_NavLayout, rewardTypes, 0, (int i, RewardType t, BaseContainer navigation, Anchor anchor, Offset offset) =>
                        {
                            bool enabled = isAdmin || rewardData.Data.HasRewardsForType(t);

                            Style style = !enabled ? m_ButtonDisabledStyle : (t == rewardType ? m_ButtonSelectedStyle : m_ButtonStyle);

                            ImageContainer.Create(navigation, anchor, offset)
                                .WithStyle(style)
                                .WithChildren(button =>
                                {
                                    TextContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(style)
                                        .WithText(GetString($"UI.{t}", player));

                                    if (enabled && t != rewardType)
                                    {
                                        ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                                {
                                                    switch (t)
                                                    {
                                                        case RewardType.Item:
                                                            CreateStoreUI(player, rewardData.Data.Items, isAdmin);
                                                            return;
                                                        case RewardType.Kit:
                                                            CreateStoreUI(player, rewardData.Data.Kits, isAdmin);
                                                            return;
                                                        case RewardType.Command:
                                                            CreateStoreUI(player, rewardData.Data.Commands, isAdmin);
                                                            return;
                                                    }
                                                }, $"{player.UserIDString}.category.{t}");
                                    }
                                });
                        });

                    // Tokens
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -80f, 0f, -55f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(tokens =>
                        {
                            TextContainer.Create(tokens, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                .WithStyle(m_PanelStyle)
                                .WithText(FormatString("UI.Tokens", player, user.Tokens))
                                .WithAlignment(TextAnchor.MiddleRight);

                            if (isAdmin)
                            {
                                ImageContainer.Create(tokens, Anchor.CenterLeft, new Offset(2.5f, -10f, 22.5f, 10f))
                                    .WithStyle(m_ButtonStyle)
                                    .WithChildren(button =>
                                    {
                                        ImageContainer.Create(button, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                                            .WithSprite(Icon.Icons_Add)
                                            .WithColor(m_PanelStyle.FontColor);

                                        ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                                {
                                                    RewardData.BaseReward reward = rewardType switch
                                                    {
                                                        RewardType.Command => new RewardData.RewardCommand
                                                        {
                                                            EditorCommands = new string[5]
                                                        },
                                                        RewardType.Kit => new RewardData.RewardKit
                                                        {
                                                            Kit = string.Empty
                                                        },
                                                        _ => new RewardData.RewardItem
                                                        {
                                                            Shortname = string.Empty,
                                                            SkinID = 0,
                                                            Amount = 1,
                                                            IsBlueprint = false
                                                        }
                                                    };

                                                    reward.Name = string.Empty;
                                                    reward.Description = string.Empty;
                                                    reward.Price = 1;
                                                    reward.Cooldown = 600;
                                                    reward.Nitro = false;
                                                    reward.Icon = string.Empty;

                                                    CreateRewardEditor(player, reward);
                                                },
                                                $"{player.UserIDString}.addreward");
                                    });
                            }
                        });

                    // Scroll View
                    ImageContainer.Create(parent, Anchor.FullStretch, new Offset(0f, 0f, 0f, -82.5f))
                        .WithStyle(m_SegmentStyle)
                        .WithScrollView(m_ScrollView)
                        .WithScrollLayoutGroup(m_ScrollLayout, list, (int i, T t, ScrollContentContainer contents, Anchor anchor, Offset offset) =>
                            CreateItemEntry(player, user, list, t, isAdmin, i, contents, anchor, offset));
                })
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting();

            ChaosUI.Show(player, root);
        }

        private void CreateItemEntry<T>(BasePlayer player, UserData.User user, List<T> list, T t, bool isAdmin, int i, BaseContainer contents, Anchor anchor, Offset offset) where T : RewardData.BaseReward
        {
            bool isItems = t.Type == RewardType.Item;
            ImageContainer.Create(contents, anchor, offset)
                .WithStyle(m_PanelStyle)
                .WithChildren(item =>
                {
                    BaseContainer.Create(item, Anchor.FullStretch, new Offset(!isAdmin ? 4f : 28f, 0f, 0f, 0f))
                        .WithChildren(layoutContent =>
                        {
                            // Icon
                            ImageContainer.Create(layoutContent, Anchor.CenterLeft, new Offset(0f, -24f, 48f, 24f))
                                .WithStyle(m_PanelStyle)
                                .WithChildren(iconBg =>
                                {
                                    ImageContainer.Create(iconBg, Anchor.CenterLeft, new Offset(0f, -24f, 48f, 24f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(icon =>
                                        {
                                            if (!string.IsNullOrEmpty(t.Icon))
                                            {
                                                if (!string.IsNullOrEmpty(t.Png))
                                                {
                                                    RawImageContainer.Create(icon, Anchor.FullStretch, Offset.zero)
                                                        .WithPNG(t.Png);
                                                }

                                            }
                                            else if (t is RewardData.RewardItem rewardItem)
                                            {
                                                ImageContainer.Create(icon, Anchor.FullStretch, Offset.zero)
                                                    .WithIcon(rewardItem.Definition.itemid, rewardItem.SkinID)
                                                    .WithChildren(itemIcon =>
                                                    {
                                                        if (rewardItem.IsBlueprint)
                                                        {
                                                            ImageContainer.Create(itemIcon, Anchor.BottomRight, new Offset(-20f, 0f, 0f, 20f))
                                                                .WithIcon(BLUEPRINT_ITEM_ID, 0UL);
                                                        }
                                                    });

                                                if (rewardItem.Amount > 1)
                                                {
                                                    TextContainer.Create(iconBg, Anchor.BottomLeft, new Offset(1f, 0f, 47f, 20f))
                                                        .WithSize(8)
                                                        .WithText($"x{rewardItem.Amount}")
                                                        .WithColor(m_PanelStyle.FontColor)
                                                        .WithAlignment(TextAnchor.LowerLeft);
                                                }
                                            }
                                            else
                                            {
                                                ImageContainer.Create(icon, Anchor.FullStretch, Offset.zero)
                                                    .WithSprite(Icon.Icons_Loot)
                                                    .WithColor(m_PanelStyle.FontColor);
                                            }
                                        });
                                });

                            // Name
                            TextContainer.Create(layoutContent, Anchor.TopStretch, new Offset(52f, -24f, -4f, -4f))
                                .WithStyle(m_PanelStyle)
                                .WithText(t.Name);

                            // Info Popup
                            if (!isItems && !string.IsNullOrEmpty(t.Description))
                            {
                                ImageContainer.Create(layoutContent, Anchor.BottomRight, new Offset(-24f, 4f, -4f, 24f))
                                    .WithStyle(m_ButtonStyle)
                                    .WithChildren(info =>
                                    {
                                        ImageContainer.Create(info, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                            .WithSprite(Icon.Icons_Info)
                                            .WithColor(m_ButtonStyle.FontColor);

                                        ButtonContainer.Create(info, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg => CreateItemDescription(player, user, list, isAdmin, t), $"{player.UserIDString}.info.{t.Type}.{i}");
                                    });
                            }

                            bool onCooldown = (Configuration.Cooldown.Enabled && user.HasCooldown(out double remaining)) || user.HasCooldown(t.UniqueId, out remaining);

                            // Button UI.OnCooldown
                            string buttonText =
                                t.Nitro && !user.IsNitroBooster ? GetString("UI.NitroOnly", player) :
                                onCooldown ? GetString("UI.OnCooldown", player) :
                                t.Price == 0 ? GetString("UI.ClaimReward.Free", player) :
                                FormatString("UI.ClaimReward", player, t.Price);

                            Style buttonStyle = t.Nitro && !user.IsNitroBooster ? m_NitroStyle :
                                !user.CanAfford(t.Price) || onCooldown ? m_InsufficientStyle : m_ClaimStyle;

                            Offset buttonOffset = !isItems && !string.IsNullOrEmpty(t.Description) ? new Offset(52f, 4f, -28f, 24f) : new Offset(52f, 4f, -4f, 24f);

                            ImageContainer.Create(layoutContent, Anchor.BottomStretch, buttonOffset)
                                .WithStyle(buttonStyle)
                                .WithChildren(claim =>
                                {
                                    TextContainer.Create(claim, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(buttonStyle)
                                        .WithText(buttonText);

                                    ButtonContainer.Create(claim, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (t.Nitro && !user.IsNitroBooster)
                                                {
                                                    player.LocalizedMessage(this, "Message.NitroOnly");
                                                    return;
                                                }

                                                if (!user.CanAfford(t.Price))
                                                {
                                                    player.LocalizedMessage(this, "Message.CantAfford");
                                                    return;
                                                }

                                                if (Configuration.Cooldown.Enabled && user.HasCooldown(out double remaining))
                                                {
                                                    player.LocalizedMessage(this, "Message.OnCooldownGlobal", FormatTime(remaining));
                                                    return;
                                                }

                                                if (user.HasCooldown(t.UniqueId, out remaining))
                                                {
                                                    player.LocalizedMessage(this, "Message.OnCooldown", FormatTime(remaining));
                                                    return;
                                                }

                                                if (Configuration.Cooldown.Enabled)
                                                    user.AddCooldown(Configuration.Cooldown.Time);
                                                else user.AddCooldown(t.UniqueId, t.Cooldown);

                                                user.DeductTokens(t.Price);
                                                t.GiveReward(player);
                                                player.LocalizedMessage(this, "Message.RewardGiven");

                                                CreateStoreUI(player, list, isAdmin);
                                            }, $"{player.UserIDString}.claim.{t.Type}.{i}");
                                });
                        });

                    if (isAdmin)
                    {
                        BaseContainer.Create(item, Anchor.LeftStretch, new Offset(0f, 0f, 28f, 0f))
                            .WithChildren(layoutAdmin =>
                            {

                                // Edit
                                ImageContainer.Create(layoutAdmin, Anchor.TopLeft, new Offset(4f, -24f, 24f, -4f))
                                    .WithStyle(m_ButtonStyle)
                                    .WithChildren(edit =>
                                    {
                                        ImageContainer.Create(edit, Anchor.FullStretch, Offset.zero)
                                            .WithSprite(Icon.Icons_Eraser)
                                            .WithColor(m_PanelStyle.FontColor);

                                        ButtonContainer.Create(edit, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg => { CreateRewardEditor(player, t.Clone()); }, $"{player.UserIDString}.edit.{t.Type}.{i}");
                                    });

                                // Delete
                                ImageContainer.Create(layoutAdmin, Anchor.BottomLeft, new Offset(4f, 4f, 24f, 24f))
                                    .WithStyle(m_ButtonStyle)
                                    .WithChildren(delete =>
                                    {
                                        ImageContainer.Create(delete, Anchor.FullStretch, new Offset(3f, 3f, -3f, -3f))
                                            .WithSprite(Icon.Icons_Clear)
                                            .WithColor(m_PanelStyle.FontColor);

                                        ButtonContainer.Create(delete, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                                {
                                                    rewardData.Data.DeleteReward(t);
                                                    rewardData.Save();
                                                    CreateStoreUI(player, list, isAdmin);
                                                }, $"{player.UserIDString}.delete.{t.Type}.{i}");
                                    });
                            });
                    }
                });
        }

        private void CreateItemDescription<T>(BasePlayer player, UserData.User user, List<T> list, bool isAdmin, T t) where T : RewardData.BaseReward
        {
            BaseContainer root = BaseContainer.Create(UI_MENU, Layer.Overall, Anchor.Center, new Offset(-175f, -62f, 175f, 62f))
                .WithChildren(parent =>
                {
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -25f, 0f, 0f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(header =>
                        {
                            TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                .WithStyle(m_SegmentStyle)
                                .WithText(t.Name);

                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-22.5f, -10f, -2.5f, 10f))
                                .WithColor(Color.Clear)
                                .WithChildren(exit =>
                                {
                                    TextContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(m_ButtonStyle)
                                        .WithText("✘")
                                        .WithWrapMode(VerticalWrapMode.Overflow);

                                    ButtonContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => CreateStoreUI(player, list, isAdmin), $"{player.UserIDString}.description.close");
                                });
                        });

                    ImageContainer.Create(parent, Anchor.FullStretch, new Offset(0f, 0f, 0f, -27.5f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(content =>
                        {
                            // Icon
                            ImageContainer.Create(content, Anchor.CenterLeft, new Offset(4f, -3.75f, 52f, 44.25f))
                                .WithStyle(m_PanelStyle)
                                .WithChildren(iconBg =>
                                {
                                    ImageContainer.Create(iconBg, Anchor.CenterLeft, new Offset(0f, -24f, 48f, 24f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(icon =>
                                        {
                                            if (!string.IsNullOrEmpty(t.Icon))
                                            {
                                                if (!string.IsNullOrEmpty(t.Png))
                                                {
                                                    RawImageContainer.Create(icon, Anchor.FullStretch, Offset.zero)
                                                        .WithPNG(t.Png);
                                                }
                                            }
                                            else if (t is RewardData.RewardItem rewardItem)
                                            {
                                                ImageContainer.Create(icon, Anchor.FullStretch, Offset.zero)
                                                    .WithIcon(rewardItem.Definition.itemid, rewardItem.SkinID)
                                                    .WithChildren(itemIcon =>
                                                    {
                                                        if (rewardItem.IsBlueprint)
                                                        {
                                                            ImageContainer.Create(itemIcon, Anchor.BottomRight, new Offset(-20f, 0f, 0f, 20f))
                                                                .WithIcon(BLUEPRINT_ITEM_ID, 0UL);
                                                        }
                                                    });

                                                if (rewardItem.Amount > 1)
                                                {
                                                    TextContainer.Create(iconBg, Anchor.BottomLeft, new Offset(1f, 0f, 47f, 20f))
                                                        .WithSize(8)
                                                        .WithText($"x{rewardItem.Amount}")
                                                        .WithColor(m_PanelStyle.FontColor)
                                                        .WithAlignment(TextAnchor.LowerLeft);
                                                }
                                            }
                                            else
                                            {
                                                ImageContainer.Create(icon, Anchor.FullStretch, Offset.zero)
                                                    .WithSprite(Icon.Icons_Loot)
                                                    .WithColor(m_PanelStyle.FontColor);
                                            }
                                        });
                                });

                            // Description
                            ImageContainer.Create(content, Anchor.FullStretch, new Offset(56f, 28f, -4f, -4f))
                                .WithStyle(m_PanelStyle)
                                .WithChildren(text =>
                                {
                                    TextContainer.Create(text, Anchor.FullStretch, new Offset(4f, 4f, -4f, -4f))
                                        .WithStyle(m_PanelStyle)
                                        .WithAlignment(TextAnchor.UpperLeft)
                                        .WithText(t.Description);
                                });

                            bool onCooldown = (Configuration.Cooldown.Enabled && user.HasCooldown(out double remaining)) || user.HasCooldown(t.UniqueId, out remaining);
                           
                            // Claim
                            string buttonText =
                                t.Nitro && !user.IsNitroBooster ? GetString("UI.NitroOnly", player) :
                                onCooldown ? GetString("UI.OnCooldown", player) :
                                t.Price == 0 ? GetString("UI.ClaimReward.Free", player) :
                                FormatString("UI.ClaimReward", player, t.Price);

                            Style buttonStyle = t.Nitro && !user.IsNitroBooster ? m_NitroStyle :
                                !user.CanAfford(t.Price) || onCooldown ? m_InsufficientStyle : m_ClaimStyle;

                            ImageContainer.Create(content, Anchor.BottomStretch, new Offset(56f, 4f, -4f, 24f))
                                .WithStyle(buttonStyle)
                                .WithChildren(claim =>
                                {
                                    TextContainer.Create(claim, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(buttonStyle)
                                        .WithText(buttonText);

                                    ButtonContainer.Create(claim, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (t.Nitro && !user.IsNitroBooster)
                                                {
                                                    player.LocalizedMessage(this, "Message.NitroOnly");
                                                    return;
                                                }

                                                if (!user.CanAfford(t.Price))
                                                {
                                                    player.LocalizedMessage(this, "Message.CantAfford");
                                                    return;
                                                }

                                                if (Configuration.Cooldown.Enabled && user.HasCooldown(out double remaining))
                                                {
                                                    player.LocalizedMessage(this, "Message.OnCooldownGlobal", FormatTime(remaining));
                                                    return;
                                                }

                                                if (user.HasCooldown(t.UniqueId, out remaining))
                                                {
                                                    player.LocalizedMessage(this, "Message.OnCooldown", FormatTime(remaining));
                                                    return;
                                                }

                                                if (Configuration.Cooldown.Enabled)
                                                    user.AddCooldown(Configuration.Cooldown.Time);
                                                else user.AddCooldown(t.UniqueId, t.Cooldown);

                                                user.DeductTokens(t.Price);
                                                t.GiveReward(player);
                                                player.LocalizedMessage(this, "Message.RewardGiven");

                                                CreateStoreUI(player, list, isAdmin);
                                            }, $"{player.UserIDString}.desc.claim.{t.Type}");
                                });
                        });
                })
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting();
            
            ChaosUI.Show(player, root);

        }

        #endregion

        #region Editor
        private void CreateRewardEditor(BasePlayer player, RewardData.BaseReward reward)
        {
            const float BASE_HEIGHT = 302f;

            float typeHeight = reward.Type switch
            {
                RewardType.Kit => 25f,
                RewardType.Command => 115f,
                RewardType.Item => 25f,
                _ => 0f
            };

            float halfHeight = (BASE_HEIGHT + typeHeight) * 0.5f;

            BaseContainer root = BaseContainer.Create(UI_MENU, Layer.Overall, Anchor.Center, new Offset(-175f, -halfHeight, 175f, halfHeight))
                .WithChildren(parent =>
                {
                    // Header
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -25f, 0f, 0f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(header =>
                        {
                            TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                .WithStyle(m_SegmentStyle)
                                .WithText(Title.ToUpper());

                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-22.5f, -10f, -2.5f, 10f))
                                .WithColor(Color.Clear)
                                .WithChildren(exit =>
                                {
                                    TextContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(m_ButtonStyle)
                                        .WithText("✘")
                                        .WithWrapMode(VerticalWrapMode.Overflow);

                                    ButtonContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (reward.Type == RewardType.Item)
                                                    CreateStoreUI(player, rewardData.Data.Items, true);
                                                else if (reward.Type == RewardType.Command)
                                                    CreateStoreUI(player, rewardData.Data.Commands, true);
                                                else CreateStoreUI(player, rewardData.Data.Kits, true);
                                            }, $"{player.UserIDString}.editor.close");
                                });
                        });

                    // Preview
                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, -88.5f, 0f, -27.5f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(preview =>
                        {
                            ImageContainer.Create(preview, Anchor.FullStretch, new Offset(2.5f, 2.5f, -2.5f, -2.5f))
                                .WithStyle(m_PanelStyle)
                                .WithChildren(previewInset =>
                                {
                                    
                                    BaseContainer.Create(previewInset, Anchor.FullStretch, new Offset(4f, 0f, 0f, 0f))
                                        .WithChildren(layoutContent =>
                                        {
                                            // Icon
                                            ImageContainer.Create(layoutContent, Anchor.CenterLeft, new Offset(0f, -24f, 48f, 24f))
                                                .WithStyle(m_PanelStyle)
                                                .WithChildren(iconBg =>
                                                {
                                                    if (!string.IsNullOrEmpty(reward.Icon))
                                                    {
                                                        RawImageContainer.Create(iconBg, Anchor.FullStretch, Offset.zero)
                                                            .WithURL(reward.Icon);
                                                    }
                                                    else if (reward.Type == RewardType.Item && reward is RewardData.RewardItem rewardItem && !string.IsNullOrEmpty(rewardItem.Shortname))
                                                    {
                                                        ItemDefinition itemDefinition = ItemManager.FindItemDefinition(rewardItem.Shortname);
                                                        if (itemDefinition)
                                                        {
                                                            ImageContainer.Create(iconBg, Anchor.FullStretch, Offset.zero)
                                                                .WithIcon(rewardItem.Definition.itemid, rewardItem.SkinID)
                                                                .WithChildren(itemIcon =>
                                                                {
                                                                    if (rewardItem.IsBlueprint)
                                                                    {
                                                                        ImageContainer.Create(itemIcon, Anchor.BottomRight, new Offset(-20f, 0f, 0f, 20f))
                                                                            .WithIcon(BLUEPRINT_ITEM_ID, 0UL);
                                                                    }
                                                                });
                                                        }

                                                        if (rewardItem.Amount > 1)
                                                        {
                                                            TextContainer.Create(iconBg, Anchor.BottomLeft, new Offset(1f, 0f, 47f, 20f))
                                                                .WithSize(8)
                                                                .WithText($"x{rewardItem.Amount}")
                                                                .WithColor(m_PanelStyle.FontColor)
                                                                .WithAlignment(TextAnchor.LowerLeft);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        ImageContainer.Create(iconBg, Anchor.FullStretch, Offset.zero)
                                                            .WithSprite(Icon.Icons_Loot)
                                                            .WithColor(m_PanelStyle.FontColor);
                                                    }
                                                });

                                            // Name
                                            if (!string.IsNullOrEmpty(reward.Name))
                                            {
                                                TextContainer.Create(layoutContent, Anchor.TopStretch, new Offset(52f, -24f, -4f, -4f))
                                                    .WithStyle(m_PanelStyle)
                                                    .WithText(reward.Name);
                                            }

                                            // Info Popup
                                            if (reward.Type != RewardType.Item && !string.IsNullOrEmpty(reward.Description))
                                            {
                                                ImageContainer.Create(layoutContent, Anchor.BottomRight, new Offset(-24f, 4f, -4f, 24f))
                                                    .WithStyle(m_ButtonStyle)
                                                    .WithChildren(info =>
                                                    {
                                                        ImageContainer.Create(info, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                                            .WithSprite(Icon.Icons_Info)
                                                            .WithColor(m_ButtonStyle.FontColor);
                                                    });
                                            }

                                            // Button
                                            string buttonText =
                                                reward.Nitro ? GetString("UI.NitroOnly", player) :
                                                reward.Price > 0 ? FormatString("UI.ClaimReward", player, reward.Price) :
                                                FormatString("UI.ClaimReward.Free", player);

                                            Style buttonStyle = reward.Nitro ? m_NitroStyle : m_ClaimStyle;

                                            Offset buttonOffset = reward.Type != RewardType.Item && !string.IsNullOrEmpty(reward.Description) ? new Offset(52f, 4f, -28f, 24f) : new Offset(52f, 4f, -4f, 24f);

                                            ImageContainer.Create(layoutContent, Anchor.BottomStretch, buttonOffset)
                                                .WithStyle(buttonStyle)
                                                .WithChildren(claim =>
                                                {
                                                    TextContainer.Create(claim, Anchor.FullStretch, Offset.zero)
                                                        .WithStyle(buttonStyle)
                                                        .WithText(buttonText);
                                                });
                                        });
                                });
                        });


                    float offset = 0f;
                    ImageContainer.Create(parent, Anchor.FullStretch, new Offset(0f, 27.5f, 0f, -91.00003f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(contents =>
                        {
                            BaseContainer.Create(contents, Anchor.FullStretch, new Offset(4f, 4f, -4f, -4f))
                                .WithChildren(container =>
                                {
                                    switch (reward.Type)
                                    {
                                        case RewardType.Kit:
                                            RewardData.RewardKit rewardKit = reward as RewardData.RewardKit;
                                            CreateTextField(player, reward, rewardKit.Kit, container, ref offset, GetString("UI.KitName", player), kit =>
                                            {
                                                if (!Kits.IsKit(kit))
                                                {
                                                    player.ChatMessage("The kit you entered does not exist");
                                                    rewardKit.Kit = string.Empty;
                                                }
                                                else
                                                {
                                                    rewardKit.Kit = kit;

                                                    if (string.IsNullOrEmpty(rewardKit.Icon))
                                                    {
                                                        string kitImage = Kits.GetKitImage(kit);
                                                        if (!string.IsNullOrEmpty(kitImage))
                                                            rewardKit.Icon = kitImage;
                                                    }

                                                    if (string.IsNullOrEmpty(rewardKit.Name))
                                                        rewardKit.Name = kit;

                                                    if (string.IsNullOrEmpty(rewardKit.Description))
                                                        rewardKit.Description = Kits.GetKitDescription(kit);
                                                }
                                            });
                                            break;
                                        case RewardType.Item:
                                            RewardData.RewardItem item = reward as RewardData.RewardItem;

                                            const string BLUEPRINT = " Blueprint";

                                            CreateTextField(player, reward, item.Shortname, container, ref offset, GetString("UI.Shortname", player), shortname =>
                                            {
                                                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
                                                if (itemDefinition == null)
                                                {
                                                    player.ChatMessage("Invalid item shortname entered");
                                                    item.Shortname = string.Empty;
                                                }
                                                else
                                                {
                                                    item.Shortname = shortname;
                                                    item.Name = itemDefinition.displayName.english + (item.IsBlueprint ? BLUEPRINT : string.Empty);
                                                }
                                                
                                                item.SetDefinition(itemDefinition);
                                            });
                                            CreateIntField(player, reward, item.Amount, container, ref offset, GetString("UI.Amount", player), amount => { item.Amount = Mathf.Max(amount, 1); });
                                            CreateUlongField(player, reward, item.SkinID, container, ref offset, GetString("UI.SkinID", player), skin => { item.SkinID = skin; });
                                            CreateBoolField(player, reward, item.IsBlueprint, container, ref offset, GetString("UI.Blueprint", player), isBlueprint =>
                                            {
                                                item.IsBlueprint = isBlueprint;

                                                if (!string.IsNullOrEmpty(item.Name))
                                                {
                                                    if (isBlueprint && !item.Name.EndsWith(BLUEPRINT))
                                                        item.Name += BLUEPRINT;

                                                    if (!isBlueprint && item.Name.EndsWith(BLUEPRINT))
                                                        item.Name = item.Name.Substring(0, item.Name.Length - BLUEPRINT.Length);
                                                }
                                            });
                                            break;
                                        case RewardType.Command:
                                            CreateStringArrayField(player, reward, (reward as RewardData.RewardCommand).EditorCommands, container, ref offset, GetString("UI.Commands", player));
                                            break;
                                    }

                                    CreateTextField(player, reward, reward.Name, container, ref offset, GetString("UI.Name", player), name => reward.Name = name);
                                    if (reward.Type != RewardType.Item)
                                        CreateMultilineTextField(player, reward, reward.Description, container, ref offset, GetString("UI.Description", player), description => reward.Description = description);
                                    CreateIntField(player, reward, reward.Cooldown, container, ref offset, GetString("UI.Cooldown", player), cooldown => reward.Cooldown = Mathf.Max(cooldown, 0));
                                    CreateTextField(player, reward, reward.Icon, container, ref offset, GetString("UI.Icon", player), icon => reward.Icon = icon);
                                    CreateIntField(player, reward, reward.Price, container, ref offset, GetString("UI.Price", player), price => reward.Price = Mathf.Max(price, 1));
                                    CreateBoolField(player, reward, reward.Nitro, container, ref offset, GetString("UI.Nitro", player), nitro => reward.Nitro = nitro);
                                });
                        });

                    // Footer
                    ImageContainer.Create(parent, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 25f))
                        .WithStyle(m_SegmentStyle)
                        .WithChildren(footer =>
                        {
                            ImageContainer.Create(footer, Anchor.CenterRight, new Offset(-82.5f, -10f, -2.5f, 10f))
                                .WithStyle(m_ClaimStyle)
                                .WithChildren(save =>
                                {
                                    TextContainer.Create(save, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(m_ClaimStyle)
                                        .WithText(GetString("UI.Save", player));

                                    ButtonContainer.Create(save, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (string.IsNullOrEmpty(reward.Name))
                                                {
                                                    player.LocalizedMessage(this, "UI.EnterName");
                                                    return;
                                                }

                                                RegisterRewardIcon(reward);

                                                bool isExisting = reward.UniqueId > 0;

                                                switch (reward.Type)
                                                {
                                                    case RewardType.Kit:
                                                    {
                                                        if (string.IsNullOrEmpty((reward as RewardData.RewardKit).Kit))
                                                        {
                                                            player.LocalizedMessage(this, "UI.EnterKit");
                                                            return;
                                                        }

                                                        if (reward.UniqueId <= 0)
                                                        {
                                                            reward.UniqueId = rewardData.Data.GetUniqueId();
                                                            rewardData.Data.Kits.Add(reward as RewardData.RewardKit);
                                                        }
                                                        else
                                                        {
                                                            rewardData.Data.Kits.First(x => x.UniqueId == reward.UniqueId).CopyFrom(reward);
                                                        }

                                                        rewardData.Save();
                                                        player.LocalizedMessage(this, isExisting ? "UI.UpdatedReward" : "UI.CreatedReward");
                                                        CreateStoreUI(player, rewardData.Data.Kits, true);
                                                        break;
                                                    }

                                                    case RewardType.Item:
                                                    {
                                                        RewardData.RewardItem item = reward as RewardData.RewardItem;

                                                        if (string.IsNullOrEmpty(item.Shortname))
                                                        {
                                                            player.LocalizedMessage(this, "UI.EnterShortname");
                                                            return;
                                                        }

                                                        ItemDefinition itemDefinition = ItemManager.FindItemDefinition(item.Shortname);
                                                        if (itemDefinition == null)
                                                        {
                                                            player.LocalizedMessage(this, "UI.InvalidShortname");
                                                            return;
                                                        }

                                                        if (reward.UniqueId <= 0)
                                                        {
                                                            reward.UniqueId = rewardData.Data.GetUniqueId();
                                                            rewardData.Data.Items.Add(reward as RewardData.RewardItem);
                                                        }
                                                        else
                                                        {
                                                            rewardData.Data.Items.First(x => x.UniqueId == reward.UniqueId).CopyFrom(reward);
                                                        }

                                                        rewardData.Save();
                                                        player.LocalizedMessage(this, isExisting ? "UI.UpdatedReward" : "UI.CreatedReward");
                                                        CreateStoreUI(player, rewardData.Data.Items, true);
                                                        break;
                                                    }
                                                    case RewardType.Command:
                                                    {
                                                        RewardData.RewardCommand command = reward as RewardData.RewardCommand;

                                                        if (command.EditorCommands == null || command.EditorCommands.All(string.IsNullOrEmpty))
                                                        {
                                                            player.LocalizedMessage(this, "UI.NoCommands");
                                                            return;
                                                        }

                                                        if (reward.UniqueId <= 0)
                                                        {
                                                            command.UniqueId = rewardData.Data.GetUniqueId();
                                                            command.Commands = command.EditorCommands.Where(x => !string.IsNullOrEmpty(x)).ToList();
                                                            rewardData.Data.Commands.Add(command);
                                                        }
                                                        else
                                                        {
                                                            rewardData.Data.Items.First(x => x.UniqueId == reward.UniqueId).CopyFrom(command);
                                                        }

                                                        rewardData.Save();
                                                        player.LocalizedMessage(this, isExisting ? "UI.UpdatedReward" : "UI.CreatedReward");
                                                        CreateStoreUI(player, rewardData.Data.Commands, true);
                                                        break;
                                                    }

                                                    default:
                                                        break;
                                                }
                                            }, $"{player.UserIDString}.save");
                                });
                        });

                })
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting();

            ChaosUI.Show(player, root);
        }

        private void CreateTextField<T>(BasePlayer player, T reward, string value,
            BaseContainer parent, ref float offset, string name, Action<string> onChange) where T : RewardData.BaseReward
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, offset - 20f, 0f, offset))
                .WithStyle(m_PanelStyle)
                .WithChildren(container =>
                {
                    TextContainer.Create(container, Anchor.CenterLeft, new Offset(4f, -10f, 100f, 10f))
                        .WithText(name)
                        .WithStyle(m_PanelStyle);

                    ImageContainer.Create(container, Anchor.CenterRight, new Offset(-250f, -10f, 0f, 10f))
                        .WithStyle(m_InputStyle)
                        .WithChildren(input =>
                        {
                            InputFieldContainer.Create(input, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                .WithText(value)
                                .WithStyle(m_InputStyle)
                                .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        onChange.Invoke(arg.Args?.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty);
                                        CreateRewardEditor(player, reward);
                                    }, $"{player.UserIDString}.drui.field.{name}");
                        });
                });

            offset -= 22.5f;
        }

        private void CreateIntField<T>(BasePlayer player, T reward, int value,
            BaseContainer parent, ref float offset, string name, Action<int> onChange) where T : RewardData.BaseReward
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, offset - 20f, 0f, offset))
                .WithStyle(m_PanelStyle)
                .WithChildren(container =>
                {
                    TextContainer.Create(container, Anchor.CenterLeft, new Offset(4f, -10f, 100f, 10f))
                        .WithText(name)
                        .WithStyle(m_PanelStyle);

                    ImageContainer.Create(container, Anchor.CenterRight, new Offset(-250f, -10f, 0f, 10f))
                        .WithStyle(m_InputStyle)
                        .WithChildren(input =>
                        {
                            InputFieldContainer.Create(input, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                .WithText(value.ToString())
                                .WithStyle(m_InputStyle)
                                .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        onChange?.Invoke(arg.GetInt(1));
                                        CreateRewardEditor(player, reward);
                                    }, $"{player.UserIDString}.drui.field.{name}");
                        });
                });
            offset -= 22.5f;
        }

        private void CreateUlongField<T>(BasePlayer player, T reward, ulong value,
            BaseContainer parent, ref float offset, string name, Action<ulong> onChange) where T : RewardData.BaseReward
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, offset - 20f, 0f, offset))
                .WithStyle(m_PanelStyle)
                .WithChildren(container =>
                {
                    TextContainer.Create(container, Anchor.CenterLeft, new Offset(4f, -10f, 100f, 10f))
                        .WithText(name)
                        .WithStyle(m_PanelStyle);

                    ImageContainer.Create(container, Anchor.CenterRight, new Offset(-250f, -10f, 0f, 10f))
                        .WithStyle(m_InputStyle)
                        .WithChildren(input =>
                        {
                            InputFieldContainer.Create(input, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                .WithText(value.ToString())
                                .WithStyle(m_InputStyle)
                                .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        onChange?.Invoke(arg.GetUInt64(1));
                                        CreateRewardEditor(player, reward);
                                    }, $"{player.UserIDString}.drui.field.{name}");
                        });
                });
            offset -= 22.5f;
        }

        private void CreateBoolField<T>(BasePlayer player, T reward, bool value,
            BaseContainer parent, ref float offset, string name, Action<bool> onChange) where T : RewardData.BaseReward
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, offset - 20f, 0f, offset))
                .WithStyle(m_PanelStyle)
                .WithChildren(container =>
                {
                    TextContainer.Create(container, Anchor.CenterLeft, new Offset(4f, -10f, 100f, 10f))
                        .WithText(name)
                        .WithStyle(m_PanelStyle);

                    ImageContainer.Create(container, Anchor.CenterRight, new Offset(-250f, -10f, -230f, 10f))
                        .WithStyle(m_InputStyle);

                    if (value)
                    {
                        TextContainer.Create(container, Anchor.CenterRight, new Offset(-250f, -10f, -230f, 10f))
                            .WithStyle(m_InputStyle)
                            .WithSize(40)
                            .WithText("•")
                            .WithAlignment(TextAnchor.MiddleCenter)
                            .WithWrapMode(VerticalWrapMode.Overflow);
                    }

                    ButtonContainer.Create(container, Anchor.CenterRight, new Offset(-250f, -10f, -230f, 10f))
                        .WithColor(Color.Clear)
                        .WithCallback(m_CallbackHandler, arg =>
                            {
                                onChange?.Invoke(!value);
                                CreateRewardEditor(player, reward);
                            }, $"{player.UserIDString}.drui.field.{name}");
                });
            offset -= 22.5f;
        }

        private void CreateStringArrayField<T>(BasePlayer player, T reward, string[] value,
            BaseContainer parent, ref float offset, string name) where T : RewardData.BaseReward
        {
            float yStart = offset;
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                BaseContainer.Create(parent, Anchor.TopStretch, new Offset(0f, offset - 20f, 0f, offset))
                    .WithChildren(container =>
                    {
                        
                        if (index == 0)
                        {
                            ImageContainer.Create(container, Anchor.FullStretch, new Offset(0f, -90f, 0f, 0f))
                                .WithStyle(m_PanelStyle);

                            TextContainer.Create(container, Anchor.CenterLeft, new Offset(4f, -10f, 100f, 10f))
                                .WithText(name)
                                .WithStyle(m_PanelStyle);

                            TextContainer.Create(container, Anchor.CenterLeft, new Offset(4f, -100f, 100f, -10f))
                                .WithStyle(m_PanelStyle)
                                .WithSize(10)
                                .WithText("Replacements:\n<i>$player.id\n$player.name\n$player.x\n$player.y\n$player.z</i>")
                                .WithAlignment(TextAnchor.UpperLeft);
                        }

                        ImageContainer.Create(container, Anchor.CenterRight, new Offset(-250f, -10f, 0f, 10f))
                            .WithStyle(m_InputStyle)
                            .WithChildren(input =>
                            {
                                InputFieldContainer.Create(input, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                    .WithText(value[i] ?? string.Empty)
                                    .WithStyle(m_InputStyle)
                                    .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            value[index] = arg.Args?.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                            CreateRewardEditor(player, reward);
                                        }, $"{player.UserIDString}.drui.field.{name}.{index}");
                            });
                    });

                offset -= 22.5f;
            }
        }

        private void CreateMultilineTextField<T>(BasePlayer player, T reward, string value,
            BaseContainer parent, ref float offset, string name, Action<string> onChange) where T : RewardData.BaseReward
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(0f, offset - 65f, 0f, offset))
                .WithStyle(m_PanelStyle)
                .WithChildren(container =>
                {
                    TextContainer.Create(container, Anchor.TopLeft, new Offset(4f, -20f, 100f, 00f))
                        .WithText(name)
                        .WithStyle(m_PanelStyle);

                    ImageContainer.Create(container, Anchor.TopRight, new Offset(-250f, -65f, 0f, 0f))
                        .WithStyle(m_InputStyle)
                        .WithChildren(input =>
                        {
                            InputFieldContainer.Create(input, Anchor.FullStretch, new Offset(5f, 2.5f, -5f, -5f))
                                .WithStyle(m_InputStyle)
                                .WithLineType(InputField.LineType.MultiLineNewline)
                                .WithText(value)
                                .WithAlignment(TextAnchor.UpperLeft)
                                .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        string result = string.Empty;
                                        if (arg.Args?.Length > 0)
                                            result = arg.FullString.Replace(arg.Args[0], "").TrimStart();

                                        onChange.Invoke(result);
                                        CreateRewardEditor(player, reward);
                                    }, $"{player.UserIDString}.drui.field.{name}");
                        });
                });
            offset -= 67.5f;
        }

        #endregion

        #endregion
        
        #region Imagery
        
        private void RegisterImages()
        {
            if (!ImageLibrary.IsLoaded)
            {
                Debug.Log($"[DiscordRewards] ImageLibrary not loaded. Unable to register images");
                return;
            }

            if (!string.IsNullOrEmpty(Client.Bot.BotUser.Avatar))
                ImageLibrary.AddImage($"https://cdn.discordapp.com/avatars/{Client.Bot.BotUser.Id.Id}/{Client.Bot.BotUser.Avatar}", UI_BOT_AVATAR, 0UL, () => m_BotAvatar = ImageLibrary.GetImage(UI_BOT_AVATAR));
            else ImageLibrary.AddImage(Configuration.UISettings.BotIcon, UI_BOT_DEFAULT_AVATAR, 0UL, () => { m_BotAvatar = ImageLibrary.GetImage(UI_BOT_DEFAULT_AVATAR); });

            void AddRewardImages<T>(List<T> list) where T : RewardData.BaseReward
            {
                foreach (T t in list)
                    RegisterRewardIcon(t);
            }
            
            AddRewardImages(rewardData.Data.Commands);
            AddRewardImages(rewardData.Data.Items);
            AddRewardImages(rewardData.Data.Kits);
        }

        private void RegisterRewardIcon(RewardData.BaseReward r)
        {
            if (ImageLibrary.IsLoaded && !string.IsNullOrEmpty(r.Icon))
            {
                ImageLibrary.AddImage(r.Icon, r.Icon, 0UL, () =>
                {
                    r.Png = ImageLibrary.GetImage(r.Icon, 0UL);
                });
            }
        }
        
        #endregion

        #region Chat Commands

        [ChatCommand("discord")]
        private void cmdDiscord(BasePlayer player, string command, string[] args)
        {
            if (!discordConnected)
                return;

            UserData.User user = userData.Data.GetUser(player.userID);
            if (user == null || (Configuration.Token.RequireRevalidation && CurrentTime() > user.ExpireTime))
            {
                if (!userData.Data.HasPendingToken(player.userID, out int code))
                {
                    code = GenerateToken();
                    userData.Data.AddToken(code, player.userID, Configuration.Token.TokenLife);
                }

                CreateTokenUI(player, code);
                return;
            }

            if (Configuration.UISettings.Enabled)
            {
                if (!OpenStore(player))
                    player.LocalizedMessage(this, "Error.NoItems");
            }
            else player.LocalizedMessage(this, "Message.AlreadyRegistered");
        }

        [ChatCommand("discord.admin")]
        private void cmdDiscordAdmin(BasePlayer player, string command, string[] args)
        {
            if (!discordConnected)
                return;

            if (!player.HasPermission(ADMIN_PERMISSION))
            {
                player.LocalizedMessage(this, "Error.NoPermission");
                return;
            }

            CreateStoreUI(player, rewardData.Data.Items, true);
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("discord.admin")]
        private void ccmdDiscordAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel != 2)
                return;

            string[] args = arg.Args;
            if (args == null || args.Length == 0)
            {
                SendReply(arg, $"{Title}  v{Version}");
                SendReply(arg, "discord.admin purge - Clear out all expired user data");
                SendReply(arg, "discord.admin wipe - Revoke rewards from all players and invalidate their tokens");
                SendReply(arg, "discord.admin revoke <player ID> - Revoke all rewards from the target player and invalidate their token");
                SendReply(arg, "discord.admin resyncusergroups <opt:player ID> - Resyncs the Oxide usergroup and permission rewards with the target player. Don't specify a player ID to resync all registered discord users");
                return;
            }

            switch (args[0].ToLower())
            {
                case "purge":
                {
                    if (!Configuration.Token.RequireRevalidation)
                    {
                        SendReply(arg, "You can not purge the data file because you have Require Validation set to false in your config");
                        return;
                    }

                    double currentTime = CurrentTime();
                    int count = 0;

                    for (int i = userData.Data.UserCount - 1; i >= 0; i--)
                    {
                        KeyValuePair<ulong, UserData.User> kvp = userData.Data.Users.ElementAt(i);
                        if (currentTime > kvp.Value.ExpireTime || string.IsNullOrEmpty(kvp.Value.DiscordId))
                        {
                            Configuration.Rewards.RevokeRewards(kvp.Key, kvp.Value);
                            userData.Data.RemoveUser(kvp.Key);
                            count++;
                        }
                    }

                    userData.Save();
                    UpdateStatus();

                    SendReply(arg, $"Revoked rewards and purged {count} users with expired tokens from the data file");
                }
                    return;

                case "wipe":
                    WipeData();
                    SendReply(arg, "Revoked all user rewards and wiped user data");
                    return;

                case "revoke":
                    if (args.Length == 2)
                    {
                        if (!ulong.TryParse(args[1], out ulong playerId))
                        {
                            SendReply(arg, "Invalid Steam ID entered");
                            return;
                        }

                        if (!userData.Data.Users.TryGetValue(playerId, out UserData.User user))
                        {
                            SendReply(arg, "The specified user does not have any data saved");
                            return;
                        }

                        Configuration.Rewards.RevokeRewards(playerId, user);
                        userData.Data.RemoveUser(playerId);
                        userData.Save();
                        UpdateStatus();

                        SendReply(arg, $"Successfully revoked rewards for user: {playerId}");
                    }
                    else SendReply(arg, "You must enter a players Steam ID");

                    return;

                case "resyncusergroups":
                {
                    if (args.Length > 1)
                    {
                        if (!ulong.TryParse(args[1], out ulong userId))
                        {
                            SendReply(arg, "Invalid User ID specified");
                            return;
                        }

                        if (!userData.Data.Users.TryGetValue(userId, out UserData.User user))
                        {
                            SendReply(arg, "There is no user with the specified ID in the data file");
                            return;
                        }

                        if (Configuration.Token.RequireRevalidation && !Configuration.Token.AutoRevalidation)
                        {
                            double currentTime = CurrentTime();
                            if (currentTime > user.ExpireTime)
                            {
                                Configuration.Rewards.RevokeRewards(userId, user);
                                userData.Data.RemoveUser(userId);

                                SendReply(arg, "The specified users Discord validation has expired. Revoked all rewards");
                                return;
                            }
                        }

                        SyncGroupsAndPermissions(userId.ToString(), user);
                        SendReply(arg, $"Resynced Oxide usergroup and permission rewards for user {userId}");
                    }
                    else
                    {
                        int purgeCount = 0;
                        int reinstateCount = 0;

                        if (Configuration.Token.RequireRevalidation && !Configuration.Token.AutoRevalidation)
                        {
                            double currentTime = CurrentTime();
                            for (int i = userData.Data.UserCount - 1; i >= 0; i--)
                            {
                                KeyValuePair<ulong, UserData.User> kvp = userData.Data.Users.ElementAt(i);
                                if (currentTime > kvp.Value.ExpireTime)
                                {
                                    Configuration.Rewards.RevokeRewards(kvp.Key, kvp.Value);
                                    userData.Data.RemoveUser(kvp.Key);
                                    purgeCount++;
                                }
                            }
                        }

                        foreach (KeyValuePair<ulong, UserData.User> kvp in userData.Data.Users)
                        {
                            SyncGroupsAndPermissions(kvp.Key.ToString(), kvp.Value);
                            reinstateCount++;
                        }

                        SendReply(arg, $"Purged {purgeCount} inactive users and resynced Oxide usergroup and permission rewards for {reinstateCount} users");
                    }
                }
                    return;

                default:
                    break;
            }
        }

        [ConsoleCommand("discord.rewards")]
        private void ccmdDiscordRewards(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel != 2)
                return;

            string[] args = arg.Args;
            if (args == null || args.Length == 0)
            {
                SendReply(arg, $"{Title}  v{Version}");
                SendReply(arg, "--- List Rewards ---");
                SendReply(arg, "discord.rewards list <items | kits | commands> - Display a list of rewards for the specified category, which information on each item");
                SendReply(arg, "--- Add Rewards ---");
                SendReply(arg, "discord.rewards add item <shortname> <skinId> <amount> <cooldown> <opt:bp> - Add a new reward item to the store (add \"bp\" to add the item as a blueprint)");
                SendReply(arg, "discord.rewards add kit <name> <kitname> <cooldown> - Add a new reward kit to the store");
                SendReply(arg, "discord.rewards add command <name> <command> <cooldown> - Add a new reward command to the store");
                SendReply(arg, "--- Editing Rewards ---");
                SendReply(arg, "discord.rewards edit item <ID> <name | amount | cooldown> \"edit value\" - Edit the specified field of the item with ID number <ID>");
                SendReply(arg, "discord.rewards edit kit <ID> <name | description | icon | cooldown> \"edit value\" - Edit the specified field of the kit with ID number <ID>");
                SendReply(arg, "discord.rewards edit command <ID> <name | amount | description | icon | add | remove | cooldown> \"edit value\" - Edit the specified field of the kit with ID number <ID>");
                SendReply(arg, "Icon field : The icon field can either be a URL, or a image saved to disk under the folder \"oxide/data/DiscordRewards/Images/\"");
                SendReply(arg, "Command add/remove field: Here you add additional commands or remove existing commands. Be sure to type the command inside quotation marks");
                SendReply(arg, "--- Removing Rewards ---");
                SendReply(arg, "discord.rewards remove item <ID #> - Removes the item with the specified ID number");
                SendReply(arg, "discord.rewards remove kit <ID #> - Removes the kit with the specified ID number");
                SendReply(arg, "discord.rewards remove command <ID #> - Removes the command with the specified ID number");
                SendReply(arg, "--- Important Note ---");
                SendReply(arg, "Removing rewards may change each rewards ID number. Be sure to list your rewards before removing them");
                SendReply(arg, "To set a reward for Nitro Boosters only add the word 'nitro' to the end of the command when adding the reward!");
                return;
            }

            bool isNitro = arg.Args.Last().ToLower() == "nitro";

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    #region Lists

                    case "list":
                        if (args.Length >= 2)
                        {
                            int i = 0;
                            switch (args[1].ToLower())
                            {
                                case "items":
                                    foreach (RewardData.RewardItem entry in rewardData.Data.Items)
                                    {
                                        SendReply(arg, $"Item ID: {i} || Shortname: {entry.Shortname} ||  Amount: {entry.Amount} || Skin ID: {entry.SkinID} || Is Blueprint {entry.IsBlueprint} || Cooldown : {entry.Cooldown}");
                                        i++;
                                    }

                                    return;

                                case "kits":
                                    i = 0;
                                    foreach (RewardData.RewardKit entry in rewardData.Data.Kits)
                                    {
                                        SendReply(arg, $"Kit ID: {i} || Name: {entry.Kit} || Description: {entry.Description} || Cooldown : {entry.Cooldown}");
                                        i++;
                                    }

                                    return;

                                case "commands":
                                    i = 0;
                                    foreach (RewardData.RewardCommand entry in rewardData.Data.Commands)
                                    {
                                        SendReply(arg, $"Command ID: {i} || Name: {entry.Name} || Description: {entry.Description} || Commands: {entry.Commands.ToSentence()} || Cooldown : {entry.Cooldown}");
                                        i++;
                                    }

                                    return;

                                default:
                                    return;
                            }
                        }

                        return;

                    #endregion

                    #region Additions

                    case "add":
                        if (args.Length >= 2)
                        {
                            switch (args[1].ToLower())
                            {
                                case "item":
                                    if (args.Length >= 6)
                                    {
                                        string shortname = args[2];

                                        if (!ulong.TryParse(args[3], out ulong skinId))
                                        {
                                            SendReply(arg, "You must enter a number for the skin ID. If you dont wish to select any skin use 0");
                                            return;
                                        }

                                        if (!int.TryParse(args[4], out int amount))
                                        {
                                            SendReply(arg, "You must enter an amount of this item");
                                            return;
                                        }

                                        if (!int.TryParse(args[5], out int cooldown))
                                        {
                                            SendReply(arg, "You must enter a cooldown for this item");
                                            return;
                                        }

                                        ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
                                        if (itemDefinition != null)
                                        {

                                            RewardData.RewardItem newItem = new RewardData.RewardItem
                                            {
                                                UniqueId = rewardData.Data.GetUniqueId(),
                                                Amount = amount,
                                                Name = itemDefinition.displayName.translated,
                                                SkinID = skinId,
                                                Shortname = shortname,
                                                Cooldown = cooldown,
                                                IsBlueprint = (args.Length >= 7 && args[6].ToLower() == "bp"),
                                                Nitro = isNitro
                                            };

                                            RegisterRewardIcon(newItem);
                                            
                                            rewardData.Data.Items.Add(newItem);
                                            rewardData.Save();
                                            SendReply(arg, $"You have added {itemDefinition.displayName.english} to DiscordRewards");
                                        }
                                        else SendReply(arg, "Invalid item selected!");
                                    }
                                    else SendReply(arg, "discord.rewards add item <shortname> <skinId> <amount> <cooldown> <opt:bp>");

                                    return;

                                case "kit":
                                    if (args.Length >= 5)
                                    {
                                        if (!Kits.IsLoaded)
                                        {
                                            SendReply(arg, "Kits plugin not found");
                                            return;
                                        }

                                        if (!int.TryParse(args[4], out int cooldown))
                                        {
                                            SendReply(arg, "You must enter a cooldown for this kit");
                                            return;
                                        }

                                        bool isKit = Kits.IsKit(args[3]);
                                        if (isKit)
                                        {
                                            RewardData.RewardKit rewardKit = new RewardData.RewardKit
                                            {
                                                UniqueId = rewardData.Data.GetUniqueId(),
                                                Name = args[2],
                                                Kit = args[3],
                                                Description = "",
                                                Cooldown = cooldown,
                                                Nitro = isNitro
                                            };
                                            
                                            RegisterRewardIcon(rewardKit);

                                            rewardData.Data.Kits.Add(rewardKit);
                                            rewardData.Save();
                                            SendReply(arg, $"You have added {args[3]} to DiscordRewards");
                                        }
                                        else SendReply(arg, "Invalid kit selected");
                                    }
                                    else SendReply(arg, "discord.rewards add kit <Name> <kitname> <cooldown>");

                                    return;

                                case "command":
                                    if (args.Length >= 5)
                                    {
                                        if (!int.TryParse(args[4], out int cooldown))
                                        {
                                            SendReply(arg, "You must enter a cooldown for this kit");
                                            return;
                                        }

                                        RewardData.RewardCommand rewardCommand = new RewardData.RewardCommand
                                        {
                                            UniqueId = rewardData.Data.GetUniqueId(),
                                            Name = arg.GetString(2),
                                            Commands = new List<string> { args[3] },
                                            Description = "",
                                            Cooldown = cooldown,
                                            Icon = string.Empty,
                                            Nitro = isNitro,
                                            Price = arg.GetInt(5)
                                        };
                                        
                                        RegisterRewardIcon(rewardCommand);
                                        rewardData.Data.Commands.Add(rewardCommand);
                                        rewardData.Save();
                                        SendReply(arg, $"You have added a new command group to DiscordRewards");
                                    }
                                    else SendReply(arg, "discord.rewards add command <name> <command> <cooldown> <price>");

                                    return;
                            }
                        }

                        return;

                    #endregion

                    #region Removal

                    case "remove":
                        if (args.Length == 3)
                        {
                            if (!int.TryParse(args[2], out int id) || id < 0)
                            {
                                SendReply(arg, "You must enter a valid ID number");
                                return;
                            }

                            switch (args[1].ToLower())
                            {
                                case "kit":
                                    if (id < rewardData.Data.Kits.Count)
                                    {
                                        rewardData.Data.Kits.RemoveAt(id);
                                        rewardData.Save();
                                        SendReply(arg, $"Successfully removed kit with ID: {id}");
                                    }
                                    else SendReply(arg, "Not kit found with the specified ID");

                                    return;
                                case "item":
                                    if (id < rewardData.Data.Items.Count)
                                    {
                                        rewardData.Data.Items.RemoveAt(id);
                                        rewardData.Save();
                                        SendReply(arg, $"Successfully removed item with ID: {id}");
                                    }
                                    else SendReply(arg, "Not item found with the specified ID");

                                    return;
                                case "command":
                                    if (id < rewardData.Data.Commands.Count)
                                    {
                                        rewardData.Data.Commands.RemoveAt(id);
                                        rewardData.Save();
                                        SendReply(arg, $"Successfully removed command with ID: {id}");
                                    }
                                    else SendReply(arg, "Not command found with the specified ID");

                                    return;
                            }
                        }

                        return;

                    #endregion

                    #region Editing

                    case "edit":
                        if (args.Length >= 3)
                        {
                            if (!int.TryParse(args[2], out int id) || id < 0)
                            {
                                SendReply(arg, "You must enter a valid ID number");
                                return;
                            }

                            switch (args[1].ToLower())
                            {
                                case "kit":
                                    if (id < rewardData.Data.Kits.Count)
                                    {
                                        if (args.Length >= 5)
                                        {
                                            switch (args[3].ToLower())
                                            {
                                                case "description":
                                                    rewardData.Data.Kits.ElementAt(id).Description = args[4];
                                                    rewardData.Save();
                                                    SendReply(arg, $"Kit {args[2]} description set to {args[4]}");
                                                    return;
                                                case "name":
                                                    rewardData.Data.Kits.ElementAt(id).Name = args[4];
                                                    rewardData.Save();
                                                    SendReply(arg, $"Kit {args[2]} name set to {args[4]}");
                                                    return;
                                                case "icon":
                                                    RewardData.RewardKit r = rewardData.Data.Kits.ElementAt(id);
                                                    r.Icon = args[4];
                                                    RegisterRewardIcon(r);
                                                    rewardData.Save();
                                                    SendReply(arg, $"Kit {args[2]} icon set to {args[4]}");
                                                    return;
                                                case "cooldown":
                                                    if (int.TryParse(args[4], out int cooldown))
                                                    {
                                                        rewardData.Data.Kits.ElementAt(id).Cooldown = cooldown;
                                                        rewardData.Save();
                                                        SendReply(arg, $"Kit {args[2]} cooldown set to {args[4]} seconds");
                                                    }
                                                    else SendReply(arg, "You must enter a cooldown number");

                                                    return;
                                                default:
                                                    SendReply(arg, "discord.rewards edit kit <ID> <description|name|icon|cooldown> \"info here\"");
                                                    return;
                                                    ;
                                            }
                                        }
                                        else SendReply(arg, "discord.rewards edit kit <ID> <description|name|icon|cooldown> \"info here\"");
                                    }
                                    else SendReply(arg, "Invalid ID number selected");

                                    return;
                                case "item":
                                    if (id < rewardData.Data.Items.Count)
                                    {
                                        if (args.Length >= 5)
                                        {
                                            switch (args[3].ToLower())
                                            {
                                                case "amount":
                                                    if (int.TryParse(args[4], out int amount))
                                                    {
                                                        rewardData.Data.Items.ElementAt(id).Amount = amount;
                                                        rewardData.Save();
                                                        SendReply(arg, $"Item {args[2]} amount set to {amount}");
                                                    }
                                                    else SendReply(arg, "Invalid amount entered");

                                                    return;
                                                case "skinid":
                                                    if (ulong.TryParse(args[4], out ulong skinId))
                                                    {
                                                        rewardData.Data.Items.ElementAt(id).SkinID = skinId;
                                                        rewardData.Save();
                                                        SendReply(arg, $"Item {args[2]} skin set to {skinId}");
                                                    }
                                                    else SendReply(arg, "Invalid skin ID entered");

                                                    return;
                                                case "isbp":
                                                    if (bool.TryParse(args[4], out bool isBp))
                                                    {
                                                        rewardData.Data.Items.ElementAt(id).IsBlueprint = isBp;
                                                        rewardData.Save();
                                                        SendReply(arg, $"Item {args[2]} blueprint set to {isBp}");
                                                    }
                                                    else SendReply(arg, "You must enter true or false");

                                                    return;
                                                case "icon":
                                                    RewardData.RewardItem r = rewardData.Data.Items.ElementAt(id);
                                                    r.Icon = args[4];
                                                    RegisterRewardIcon(r);
                                                    rewardData.Save();
                                                    SendReply(arg, $"Item {args[2]} icon set to {args[4]}");
                                                    return;
                                                case "cooldown":
                                                    if (int.TryParse(args[4], out int cooldown))
                                                    {
                                                        rewardData.Data.Items.ElementAt(id).Cooldown = cooldown;
                                                        rewardData.Save();
                                                        SendReply(arg, $"Item {args[2]} cooldown set to {args[4]} seconds");
                                                    }
                                                    else SendReply(arg, "You must enter a cooldown number");

                                                    return;
                                                default:
                                                    SendReply(arg, "discord.rewards edit item <ID> <amount|skinid|isbp|icon|cooldown> \"info here\"");
                                                    return;
                                            }
                                        }
                                        else SendReply(arg, "discord.rewards edit item <ID> <amount|skinid|isbp|icon|cooldown> \"info here\"");
                                    }
                                    else SendReply(arg, "Invalid ID number selected");

                                    return;
                                case "command":
                                    if (id < rewardData.Data.Commands.Count)
                                    {
                                        if (args.Length >= 5)
                                        {
                                            switch (args[3].ToLower())
                                            {
                                                case "description":
                                                    rewardData.Data.Commands.ElementAt(id).Description = args[4];
                                                    rewardData.Save();
                                                    SendReply(arg, $"Command {args[2]} description set to {args[4]}");
                                                    return;
                                                case "name":
                                                    rewardData.Data.Commands.ElementAt(id).Name = args[4];
                                                    rewardData.Save();
                                                    SendReply(arg, $"Command {args[2]} name set to {args[4]}");
                                                    return;
                                                case "icon":
                                                    RewardData.RewardCommand r = rewardData.Data.Commands.ElementAt(id);
                                                    r.Icon = args[4];
                                                    RegisterRewardIcon(r);
                                                    rewardData.Save();
                                                    SendReply(arg, $"Command {args[2]} icon set to {args[4]}");
                                                    return;
                                                case "add":
                                                    if (!rewardData.Data.Commands.ElementAt(id).Commands.Contains(args[4]))
                                                    {
                                                        rewardData.Data.Commands.ElementAt(id).Commands.Add(args[4]);
                                                        rewardData.Save();
                                                        SendReply(arg, string.Format("Added command \"{1}\" to Reward Command {0}", args[2], args[4]));
                                                    }
                                                    else SendReply(arg, string.Format("The command \"0\" is already registered to this reward command", args[4]));

                                                    return;
                                                case "remove":
                                                    if (rewardData.Data.Commands.ElementAt(id).Commands.Contains(args[4]))
                                                    {
                                                        rewardData.Data.Commands.ElementAt(id).Commands.Remove(args[4]);
                                                        rewardData.Save();
                                                        SendReply(arg, string.Format("Removed command \"{1}\" to Command {0}", args[2], args[4]));
                                                    }
                                                    else SendReply(arg, $"The command \"{args[4]}\" is not registered to this reward command");

                                                    return;
                                                case "cooldown":
                                                    if (int.TryParse(args[4], out int cooldown))
                                                    {
                                                        rewardData.Data.Commands.ElementAt(id).Cooldown = cooldown;
                                                        rewardData.Save();
                                                        SendReply(arg, $"Command {args[2]} cooldown set to {args[4]} seconds");
                                                    }
                                                    else SendReply(arg, "You must enter a cooldown number");

                                                    return;
                                                default:
                                                    SendReply(arg, "discord.rewards edit command <ID> <description|name|icon|add|remove|cooldown> \"info here\"");
                                                    return;
                                            }
                                        }
                                        else SendReply(arg, "discord.rewards edit command <ID> <description|name|icon|add|remove|cooldown> \"info here\"");
                                    }
                                    else SendReply(arg, "Invalid ID number selected");

                                    return;
                            }
                        }

                        return;

                    #endregion
                }
            }
        }

        #endregion

        #region API

        private string SteamToDiscordID(ulong playerId)
        {
            if (userData.Data.FindById(playerId, out UserData.User discordUser))
                return discordUser.DiscordId;

            return string.Empty;
        }

        private string DiscordToSteamID(string discordId)
        {
            if (userData.Data.FindById(discordId, out ulong playerId))
                return playerId.ToString();

            return string.Empty;
        }

        #endregion

        #region Config

        private ConfigData Configuration => ConfigurationData as ConfigData;

        private class ConfigData : BaseConfigData
        {
            public DiscordSettings Settings { get; set; }

            [JsonProperty(PropertyName = "Rewards")]
            public RewardOptions Rewards { get; set; }

            [JsonProperty(PropertyName = "Validation Tokens")]
            public Validation Token { get; set; }

            [JsonProperty(PropertyName = "Global Cooldown")]
            public GlobalCooldown Cooldown { get; set; }

            [JsonProperty(PropertyName = "UI Options")]
            public UIOptions UISettings { get; set; }

            public class DiscordSettings
            {
                [JsonProperty(PropertyName = "Bot Token")]
                public string APIKey { get; set; }

                [JsonProperty(PropertyName = "Bot Client ID")]
                public string BotID { get; set; }

                [JsonProperty(PropertyName = "Bot Status Messages")]
                public string[] StatusMessages { get; set; }

                [JsonProperty(PropertyName = "Bot Status Cycle Time (seconds)")]
                public int StatusCycle { get; set; }

                [JsonConverter(typeof(StringEnumConverter))]
                [JsonProperty(PropertyName = "Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
                public DiscordLogLevel LogLevel { get; set; }
            }

            public class UIOptions
            {
                [JsonProperty(PropertyName = "Enable Reward Menu")]
                public bool Enabled { get; set; }

                [JsonProperty("UI Colors")]
                public UiColors Colors { get; set; }

                [JsonProperty("Default bot profile icon shown in UI (overridden by actual bot profile icon if set)")]
                public string BotIcon { get; set; }

                public class UiColors
                {
                    [JsonProperty(PropertyName = "Segment")]
                    public Color Segment { get; set; } = new Color { Hex = "27241D", Alpha = 0.97f };

                    [JsonProperty(PropertyName = "Panel")]
                    public Color Panel { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.25f };

                    [JsonProperty(PropertyName = "Text")]
                    public Color Text { get; set; } = new Color { Hex = "BAB1A8", Alpha = 1f };


                    [JsonProperty(PropertyName = "Button")]
                    public Color Button { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.6f };

                    [JsonProperty(PropertyName = "Button Text")]
                    public Color ButtonText { get; set; } = new Color { Hex = "BAB1A8", Alpha = 1f };

                    [JsonProperty(PropertyName = "Button Selected")]
                    public Color ButtonSelected { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.5f };

                    [JsonProperty(PropertyName = "Button Selected Text")]
                    public Color ButtonSelectedText { get; set; } = new Color { Hex = "FFFFFF", Alpha = 0.8f };
                    
                    [JsonProperty(PropertyName = "Button Disabled Text")]
                    public Color ButtonDisabledText { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.3f };

                    [JsonProperty(PropertyName = "Button Disabled")]
                    public Color ButtonDisabled { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.3f };
                   
                    
                    [JsonProperty(PropertyName = "Claim")]
                    public Color Claim { get; set; } = new Color { Hex = "738D45", Alpha = 1f };

                    [JsonProperty(PropertyName = "Claim Text")]
                    public Color ClaimText { get; set; } = new Color { Hex = "AAEE31", Alpha = 0.94f };

                    [JsonProperty(PropertyName = "Nitro")]
                    public Color Nitro { get; set; } = new Color { Hex = "DC16F5", Alpha = 0.42f };

                    [JsonProperty(PropertyName = "Nitro Text")]
                    public Color NitroText { get; set; } = new Color { Hex = "FACFFF", Alpha = 0.87f };

                    [JsonProperty(PropertyName = "Insufficient")]
                    public Color Insufficient { get; set; } = new Color { Hex = "AB2021", Alpha = 0.6f };

                    [JsonProperty(PropertyName = "Insufficient Text")]
                    public Color InsufficientText { get; set; } = new Color { Hex = "FFBDBE", Alpha = 0.87f };


                    [JsonProperty(PropertyName = "Input")]
                    public Color Input { get; set; } = new Color { Hex = "BAB1A8", Alpha = 0.3f };

                    [JsonProperty(PropertyName = "Input Text")]
                    public Color InputText { get; set; } = new Color { Hex = "FFFFFF", Alpha = 0.8f };

                    [JsonProperty(PropertyName = "Scrollbar Background")]
                    public Color ScrollbarBackground { get; set; } = new Color { Hex = "27241D", Alpha = 0.9f };

                    [JsonProperty(PropertyName = "Scrollbar Handle")]
                    public Color ScrollbarHandle { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.6f };

                    [JsonProperty(PropertyName = "Scrollbar Highlight")]
                    public Color ScrollbarHighlight { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.6f };

                    [JsonProperty(PropertyName = "Scrollbar Pressed")]
                    public Color ScrollbarPressed { get; set; } = new Color { Hex = "6F6B64", Alpha = 0.6f };

                    public class Color
                    {
                        public string Hex { get; set; }

                        public float Alpha { get; set; }

                        public static implicit operator Chaos.UIFramework.Color(Color c)
                        {
                            return new Chaos.UIFramework.Color(c.Hex, c.Alpha);
                        }
                    }
                }
                /*public class UIColors
                {
                    public Color Background { get; set; }

                    public Color Panel { get; set; }

                    public Color Header { get; set; }

                    public Color Button { get; set; }

                    public Color Close { get; set; }

                    public Color Highlight { get; set; }

                    public Color Nitro { get; set; }

                    public Color Claim { get; set; }

                    public Color InsufficientTokens { get; set; }

                    public class Color
                    {
                        public string Hex { get; set; }

                        public float Alpha { get; set; }
                    }
                }*/
            }

            public class GlobalCooldown
            {
                [JsonProperty(PropertyName = "Use Global Cooldown")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Global Cooldown Time (seconds)")]
                public int Time { get; set; }
            }

            public class Validation
            {
                [JsonProperty(PropertyName = "Token Lifetime (seconds)")]
                public int TokenLife { get; set; }

                [JsonProperty(PropertyName = "Require Re-validation")]
                public bool RequireRevalidation { get; set; }

                [JsonProperty(PropertyName = "Automatically try and re-validate users when their token has expired")]
                public bool AutoRevalidation { get; set; }

                [JsonProperty(PropertyName = "Revalidation Interval (seconds)")]
                public int RevalidationInterval { get; set; }

                [JsonProperty(PropertyName = "Revoke rewards and wipe token data on map wipe")]
                public bool WipeReset { get; set; }

                [JsonProperty(PropertyName = "Reset reward cooldowns on map wipe")]
                public bool WipeResetRewards { get; set; }

                [JsonProperty(PropertyName = "Validation channel")]
                public string ValidationChannel { get; set; }
            }

            public class RewardOptions
            {
                [JsonProperty(PropertyName = "Amount of reward tokens to give by linking Discord account")]
                public int RewardTokensInitial { get; set; }

                [JsonProperty(PropertyName = "Amount of reward tokens to give every time a linked account is re-validated")]
                public int RewardTokensPerValidation { get; set; }


                [JsonProperty(PropertyName = "Basic Rewards")]
                public AutomaticRewards BasicRewards { get; set; } = new AutomaticRewards();

                [JsonProperty(PropertyName = "Nitro Rewards")]
                public AutomaticRewards NitroRewards { get; set; } = new AutomaticRewards();


                public void IssueRewards(BasePlayer player, UserData.User user)
                {
                    DiscordClient client = ClientInstance();
                    if (client == null)
                    {
                        Debug.Log($"[DiscordRewards] Tried issuing rewards to {player.displayName} but the Discord client is not connected");
                        return;
                    }
                    
                    Guild.GetMember(client, user.Id).Then((GuildMember guildMember) =>
                    {
                        GuildMemberUpdate guildMemberUpdate = new GuildMemberUpdate
                        {
                            Roles = guildMember.Roles
                        };

                        bool sendUpdate = false;

                        BasicRewards.IssueRewards(player, user, guildMemberUpdate, ref sendUpdate);

                        bool wasNitroBooster = user.IsNitroBooster;

                        user.IsNitroBooster = NitroRole != null && guildMember.HasRole(NitroRole);

                        if (user.IsNitroBooster)
                            NitroRewards.IssueRewards(player, user, guildMemberUpdate, ref sendUpdate);
                        else if (wasNitroBooster)
                            NitroRewards.RevokeRewards(player.userID, user, guildMemberUpdate, ref sendUpdate);

                        if (sendUpdate)
                            Guild.EditMember(client, user.Id, guildMemberUpdate);
                    });
                }

                public void UpdateNitroRewards(GuildMember guildMember, BasePlayer player, UserData.User user, bool wasNitro)
                {
                    DiscordClient client = ClientInstance();
                    if (client == null)
                    {
                        Debug.Log($"[DiscordRewards] Tried issuing rewards to {player.displayName} but the Discord client is not connected");
                        return;
                    }
                    
                    GuildMemberUpdate guildMemberUpdate = new GuildMemberUpdate
                    {
                        Roles = guildMember.Roles
                    };
                    
                    bool sendUpdate = false;
                    
                    if (user.IsNitroBooster)
                        NitroRewards.IssueRewards(player, user, guildMemberUpdate, ref sendUpdate);
                    else if (wasNitro)
                        NitroRewards.RevokeRewards(player.userID, user, guildMemberUpdate, ref sendUpdate);

                    if (sendUpdate)
                        Guild.EditMember(client, user.Id, guildMemberUpdate);
                }

                public void RevokeRewards(ulong playerId, UserData.User user)
                {
                    DiscordClient client = ClientInstance();
                    if (client == null)
                    {
                        Debug.Log($"[DiscordRewards] Tried revoking rewards from {playerId} but the Discord client is not connected");
                        return;
                    }
                    
                    Guild.GetMember(client, user.Id).Then((GuildMember guildMember) =>
                    {
                        GuildMemberUpdate guildMemberUpdate = new GuildMemberUpdate
                        {
                            Roles = guildMember.Roles
                        };

                        bool sendUpdate = false;

                        BasicRewards.RevokeRewards(playerId, user, guildMemberUpdate, ref sendUpdate);

                        if (user.IsNitroBooster)
                            NitroRewards.RevokeRewards(playerId, user, guildMemberUpdate, ref sendUpdate);

                        if (sendUpdate)
                            Guild.EditMember(client, user.Id, guildMemberUpdate);
                    });
                }

                public class AutomaticRewards
                {
                    [JsonProperty(PropertyName = "Add user to Oxide user groups")]
                    public string[] Groups { get; set; } = Array.Empty<string>();

                    [JsonProperty(PropertyName = "Commands to run on successful validation")]
                    public string[] Commands { get; set; } = Array.Empty<string>();

                    [JsonProperty(PropertyName = "Permissions to grant on successful validation")]
                    public string[] Permissions { get; set; } = Array.Empty<string>();

                    [JsonProperty(PropertyName = "Discord roles to grant on successful validation")]
                    public string[] Roles { get; set; } = Array.Empty<string>();

                    [JsonProperty(PropertyName = "Discord roles to revoke on successful validation")]
                    public string[] RevokeRoles { get; set; } = Array.Empty<string>();

                    [JsonIgnore]
                    private DiscordRole[] roleCache;

                    [JsonIgnore]
                    private DiscordRole[] revokeRoleCache;

                    public void IssueRewards(BasePlayer player, UserData.User user, GuildMemberUpdate guildMemberUpdate, ref bool sendUpdate)
                    {
                        foreach (string group in Groups)
                        {
                            if (GroupExists(group) && !HasGroup(player.UserIDString, group))
                            {
                                AddToGroup(player.UserIDString, group);
                                user.Groups.Add(group);
                            }
                        }

                        foreach (string perm in Permissions)
                        {
                            if (PermissionExists(perm) && !HasPermission(player.UserIDString, perm))
                            {
                                GrantPermission(player.UserIDString, perm);
                                user.Permissions.Add(perm);
                            }
                        }

                        foreach (string cmd in Commands)
                        {
                            ConsoleSystem.Run(ConsoleSystem.Option.Server, cmd.Replace("$player.id", player.UserIDString)
                                .Replace("$player.name", player.displayName)
                                .Replace("$player.x", player.transform.position.x.ToString())
                                .Replace("$player.y", player.transform.position.y.ToString())
                                .Replace("$player.z", player.transform.position.z.ToString())
                            );
                        }

                        FindCacheDiscordRoles();

                        for (int i = 0; i < Roles.Length; i++)
                        {
                            string roleName = Roles[i];

                            DiscordRole discordRole = roleCache[i] ?? Guild.GetRole(roleName);
                            if (discordRole == null)
                                discordRole = GetRoleByID(roleName);

                            if (discordRole != null)
                            {
                                if (guildMemberUpdate.Roles.All(x => x.Id != discordRole.Id))
                                {
                                    user.Roles.Add(discordRole.Id);
                                    guildMemberUpdate.Roles.Add(discordRole.Id);
                                    sendUpdate = true;
                                }
                            }
                        }

                        for (int i = 0; i < RevokeRoles.Length; i++)
                        {
                            string roleName = RevokeRoles[i];

                            DiscordRole discordRole = revokeRoleCache[i] ?? Guild.GetRole(roleName);
                            if (discordRole == null)
                                discordRole = GetRoleByID(roleName);

                            if (discordRole != null)
                            {
                                for (int y = guildMemberUpdate.Roles.Count - 1; y >= 0; y--)
                                {
                                    if (guildMemberUpdate.Roles[y].Id == discordRole.Id)
                                        guildMemberUpdate.Roles.RemoveAt(y);
                                }
                            }
                        }
                    }

                    public void RevokeRewards(ulong playerId, UserData.User user, GuildMemberUpdate guildMemberUpdate, ref bool sendUpdate)
                    {
                        foreach (string group in user.Groups)
                            RemoveFromGroup(playerId.ToString(), group);

                        user.Groups.Clear();

                        foreach (string perm in user.Permissions)
                            RevokePermission(playerId.ToString(), perm);

                        user.Permissions.Clear();

                        FindCacheDiscordRoles();

                        for (int i = 0; i < Roles.Length; i++)
                        {
                            string roleName = Roles[i];

                            DiscordRole discordRole = roleCache[i] ?? Guild.GetRole(roleName);
                            if (discordRole == null)
                                discordRole = GetRoleByID(roleName);

                            if (discordRole != null)
                            {
                                for (int y = guildMemberUpdate.Roles.Count - 1; y >= 0; y--)
                                {
                                    if (guildMemberUpdate.Roles[y].Id == discordRole.Id)
                                        guildMemberUpdate.Roles.RemoveAt(y);
                                }

                                sendUpdate = true;
                            }
                        }

                        for (int i = 0; i < RevokeRoles.Length; i++)
                        {
                            string roleName = RevokeRoles[i];

                            DiscordRole discordRole = revokeRoleCache[i] ?? Guild.GetRole(roleName);
                            if (discordRole == null)
                                discordRole = GetRoleByID(roleName);

                            if (discordRole != null)
                            {
                                if (guildMemberUpdate.Roles.All(x => x.Id != discordRole.Id))
                                {
                                    guildMemberUpdate.Roles.Add(discordRole.Id);
                                    sendUpdate = true;
                                }
                            }
                        }

                        user.Roles.Clear();
                    }

                    private void FindCacheDiscordRoles()
                    {
                        roleCache ??= new DiscordRole[Roles.Length];
                        revokeRoleCache ??= new DiscordRole[RevokeRoles.Length];

                        for (int i = 0; i < Roles.Length; i++)
                        {
                            if (roleCache[i] == null)
                            {
                                string roleName = Roles[i];

                                DiscordRole discordRole = Guild.GetRole(roleName);
                                if (discordRole == null)
                                    discordRole = GetRoleByID(roleName);

                                if (discordRole != null)
                                    roleCache[i] = discordRole;
                            }
                        }

                        for (int i = 0; i < RevokeRoles.Length; i++)
                        {
                            if (revokeRoleCache[i] == null)
                            {
                                string roleName = RevokeRoles[i];

                                DiscordRole discordRole = Guild.GetRole(roleName);
                                if (discordRole == null)
                                    discordRole = GetRoleByID(roleName);

                                if (discordRole != null)
                                    revokeRoleCache[i] = discordRole;
                            }
                        }
                    }
                }
            }
        }

        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Cooldown = new ConfigData.GlobalCooldown
                {
                    Enabled = false,
                    Time = 84600
                },
                Settings = new ConfigData.DiscordSettings
                {
                    APIKey = "",
                    BotID = "",
                    LogLevel = DiscordLogLevel.Info,
                    StatusMessages = new string[0],
                    StatusCycle = 120,
                },
                Token = new ConfigData.Validation
                {
                    RevalidationInterval = 84600,
                    TokenLife = 3600,
                    AutoRevalidation = true,
                    RequireRevalidation = true,
                    WipeReset = false,
                    ValidationChannel = string.Empty,
                    WipeResetRewards = false
                },
                Rewards = new ConfigData.RewardOptions
                {
                    RewardTokensInitial = 5,
                    RewardTokensPerValidation = 3,
                    BasicRewards = new ConfigData.RewardOptions.AutomaticRewards(),
                    NitroRewards = new ConfigData.RewardOptions.AutomaticRewards()
                },
                UISettings = new ConfigData.UIOptions
                {
                    Enabled = true,
                    Colors = new ConfigData.UIOptions.UiColors(),
                    BotIcon = "https://better-default-discord.netlify.app/Icons/Blast-Blue.png"
                },
                Version = Version
            } as T;
        }

        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();

            if (Configuration.Version < new VersionNumber(0, 1, 2))
            {
                Configuration.UISettings.Enabled = true;
                Configuration.Token.RequireRevalidation = true;
                Configuration.Rewards = baseConfigData.Rewards;
            }

            if (Configuration.Version < new VersionNumber(0, 1, 10))
            {
                Configuration.Settings.StatusMessages = new string[0];
                Configuration.Settings.StatusCycle = 120;
            }

            if (Configuration.Version < new VersionNumber(0, 1, 12))
                Configuration.Token.WipeReset = false;

            if (Configuration.Version < new VersionNumber(0, 2, 0))
            {
                Configuration.Settings.LogLevel = DiscordLogLevel.Info;
                Configuration.Token.ValidationChannel = string.Empty;
            }

            if (Configuration.Version < new VersionNumber(0, 3, 0))
            {
                Configuration.Rewards = new ConfigData.RewardOptions();
                Configuration.Rewards.RewardTokensInitial = 1;
                Configuration.Rewards.RewardTokensPerValidation = 1;
                Configuration.UISettings.Colors = baseConfigData.UISettings.Colors;
                Configuration.UISettings.BotIcon = baseConfigData.UISettings.BotIcon;
            }

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management

        private void WipeData()
        {
            foreach (KeyValuePair<ulong, UserData.User> kvp in userData.Data.Users)
                Configuration.Rewards.RevokeRewards(kvp.Key, kvp.Value);

            userData.Data.Users.Clear();
            userData.Save();
            UpdateStatus();
        }

        private void WipeRewardCooldowns()
        {
            foreach (KeyValuePair<ulong, UserData.User> kvp in userData.Data.Users)
                kvp.Value.WipeCooldowns();

            userData.Save();
        }

        private class UserData
        {
            [FormerlySerializedAs("users")]
            public Dictionary<ulong, User> Users = new Dictionary<ulong, User>();
            
            [FormerlySerializedAs("tokenToUser")]
            public Hash<int, DiscordToken> TokenToUser = new Hash<int, DiscordToken>();

            public User AddNewUser(ulong playerId, string discordId)
            {
                User userData = new User(discordId);
                Users.Add(playerId, userData);
                return userData;
            }

            public User GetUser(ulong userId)
            {
                if (Users.TryGetValue(userId, out User user))
                    return user;
                return null;
            }

            [JsonIgnore]
            public int UserCount => Users.Count;

            [JsonIgnore]
            public int ValidUsers => Users.Count(x => CurrentTime() < x.Value.ExpireTime);

            public void RemoveUser(ulong userId) => Users.Remove(userId);

            public bool FindById(ulong userId, out User discordUser)
                => Users.TryGetValue(userId, out discordUser);

            public bool FindById(string discordId, out ulong userId)
            {
                foreach (KeyValuePair<ulong, User> user in Users)
                {
                    if (user.Value.DiscordId.Equals(discordId))
                    {
                        userId = user.Key;
                        return true;
                    }
                }

                userId = 0UL;
                return false;
            }

            public void AddToken(int code, ulong playerId, int duration)
            {
                TokenToUser.Add(code, new DiscordToken(playerId, duration));
            }

            public bool HasPendingToken(ulong playerId, out int code)
            {
                code = -1;
                foreach (KeyValuePair<int, DiscordToken> kvp in TokenToUser)
                {
                    if (kvp.Value.ExpireTime < CurrentTime())
                    {
                        TokenToUser.Remove(kvp.Key);
                        return false;
                    }

                    code = kvp.Key;
                    return true;
                }

                return false;
            }

            public bool IsValidToken(int code, out DiscordToken token)
            {
                return TokenToUser.TryGetValue(code, out token);
            }

            public void InvalidateToken(int token) => TokenToUser.Remove(token);

            public class DiscordToken
            {
                [FormerlySerializedAs("playerId")]
                public ulong UserId;
                
                [FormerlySerializedAs("expireTime")]
                public double ExpireTime;

                public DiscordToken(ulong userId, int duration)
                {
                    this.UserId = userId;
                    this.ExpireTime = CurrentTime() + duration;
                }
            }

            public class User
            {
                [FormerlySerializedAs("discordId")]
                public string DiscordId;

                [FormerlySerializedAs("expireTime")]
                public double ExpireTime;
                
                [FormerlySerializedAs("globalTime")]
                public double GlobalTime;

                [FormerlySerializedAs("isNitroBooster")]
                public bool IsNitroBooster = false;

                public int Tokens = 0;

                [FormerlySerializedAs("groups")]
                public HashSet<string> Groups = new HashSet<string>();
                
                [FormerlySerializedAs("permissions")]
                public HashSet<string> Permissions = new HashSet<string>();
                
                [FormerlySerializedAs("roles")]
                public HashSet<string> Roles = new HashSet<string>();

                [FormerlySerializedAs("nitroGroups")]
                public HashSet<string> NitroGroups = new HashSet<string>();
                
                [FormerlySerializedAs("nitroPermissions")]
                public HashSet<string> NitroPermissions = new HashSet<string>();

                public Hash<int, double> Cooldowns = new Hash<int, double>();

                [JsonIgnore]
                private Snowflake _id;

                [JsonIgnore]
                public Snowflake Id
                {
                    get
                    {
                        if (_id.Equals(default(Snowflake)))
                        {
                            _id = new Snowflake(ulong.Parse(DiscordId));
                        }

                        return _id;
                    }
                    set
                    {
                        DiscordId = value.Id.ToString();
                        _id = value;
                    }
                }

                public User(string discordId)
                {
                    this.DiscordId = discordId;
                }

                public void SetExpiryDate(int duration)
                {
                    this.ExpireTime = CurrentTime() + duration;
                }

                public void AddTokens(int tokens) => this.Tokens += tokens;

                public bool CanAfford(int tokens) => this.Tokens >= tokens;

                public void DeductTokens(int tokens) => this.Tokens -= tokens;

                public void AddCooldown(int id, int time)
                    => Cooldowns[id] = time + CurrentTime();

                public void AddCooldown(int time)
                    => GlobalTime = CurrentTime() + time;

                public bool HasCooldown(int id, out double remaining)
                {
                    remaining = 0;
                    if (Cooldowns.TryGetValue(id, out double time))
                    {
                        double currentTime = CurrentTime();
                        if (time > currentTime)
                        {
                            remaining = time - currentTime;
                            return true;
                        }
                    }

                    return false;
                }

                public bool HasCooldown(out double remaining)
                {
                    remaining = GlobalTime - CurrentTime();
                    return remaining > 0;
                }

                public void WipeCooldowns()
                    => Cooldowns.Clear();
            }
        }

        private class RewardData
        {
            [FormerlySerializedAs("items")]
            public List<RewardItem> Items = new List<RewardItem>();
            
            [FormerlySerializedAs("kits")]
            public List<RewardKit> Kits = new List<RewardKit>();
            
            [FormerlySerializedAs("commands")]
            public List<RewardCommand> Commands = new List<RewardCommand>();
            
            public int IncrementalId = 0;

            public int GetUniqueId()
            {
                IncrementalId++;
                return IncrementalId;
            }

            public bool ValidateRewards()
            {
                bool hasChanged = false;
                
                void Validate<T>(List<T> list) where T : BaseReward
                {
                    foreach (T t in list)
                    {
                        if (t.UniqueId <= 0)
                        {
                            t.UniqueId = GetUniqueId();
                            hasChanged = true;
                        }

                        if (t.Price <= 0)
                        {
                            t.Price = 1;
                            hasChanged = true;
                        }
                    }
                }
                
                Validate(Items);
                Validate(Kits);
                Validate(Commands);

                return hasChanged;
            }

            public bool HasRewardsForType(RewardType type)
            {
                return type switch
                {
                    RewardType.Kit => Kits.Count > 0,
                    RewardType.Item => Items.Count > 0,
                    RewardType.Command => Commands.Count > 0,
                    _ => false
                };
            }

            public void DeleteReward(BaseReward reward)
            {
                switch (reward.Type)
                {
                    case RewardType.Item:
                        Items.RemoveAll(x => x.UniqueId == reward.UniqueId);
                        break;
                    case RewardType.Kit:
                        Kits.RemoveAll(x => x.UniqueId == reward.UniqueId);
                        break;
                    case RewardType.Command:
                        Commands.RemoveAll(x => x.UniqueId == reward.UniqueId);
                        break;
                }
            }

            public class RewardItem : BaseReward
            {
                public string Shortname { get; set; }
                public int Amount { get; set; }
                public ulong SkinID { get; set; }
                public bool IsBlueprint { get; set; }

                public override RewardType Type => RewardType.Item;

                [JsonIgnore]
                private ItemDefinition _definition;

                [JsonIgnore]
                private static ItemDefinition _blueprintDefinition;

                [JsonIgnore]
                public ItemDefinition Definition
                {
                    get
                    {
                        if (!_definition)
                        {
                            _definition = ItemManager.FindItemDefinition(Shortname);

                            if (_definition == null)
                                Debug.LogError("[Discord Rewards] An invalid item shortname was found in the rewards data: " + Shortname);
                        }

                        return _definition;
                    }
                }

                [JsonIgnore]
                public ItemDefinition BlueprintDefinition
                {
                    get
                    {
                        if (!_blueprintDefinition)
                            _blueprintDefinition = ItemManager.FindItemDefinition(BLUEPRINT_ITEM_ID);

                        return _blueprintDefinition;
                    }
                }

                public void SetDefinition(ItemDefinition itemDefinition) => _definition = itemDefinition;

                public RewardItem()
                {
                }

                public RewardItem(int id, string displayName, string shortname, int amount = 1, ulong skinId = 0UL, bool isBlueprint = false, string description = "", int cooldown = 0, string icon = "", bool nitro = false, int price = 0) : base(id, displayName, description, cooldown, icon, nitro, price)
                {
                    Shortname = shortname;
                    Amount = amount;
                    SkinID = skinId;
                    IsBlueprint = isBlueprint;
                }

                public override void GiveReward(BasePlayer player)
                {
                    ItemDefinition definition = Definition;
                    if (!definition)
                        return;

                    Item item;
                    if (IsBlueprint)
                    {
                        item = ItemManager.Create(BlueprintDefinition, Amount, SkinID);
                        item.blueprintTarget = definition.itemid;
                    }
                    else item = ItemManager.Create(definition, Amount, SkinID);

                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                }

                public override BaseReward Clone()
                {
                    RewardItem reward = new RewardItem();
                    CopyValuesTo(reward);
                    reward.Shortname = Shortname;
                    reward.Amount = Amount;
                    reward.SkinID = SkinID;
                    reward.IsBlueprint = IsBlueprint;
                    return reward;
                }

                public override void CopyFrom(BaseReward t)
                {
                    t.CopyValuesTo(this);
                    if (t is RewardItem reward)
                    {
                        Shortname = reward.Shortname;
                        Amount = reward.Amount;
                        SkinID = reward.SkinID;
                        IsBlueprint = reward.IsBlueprint;
                    }
                }
            }

            public class RewardCommand : BaseReward
            {
                public List<string> Commands { get; set; }

                public override RewardType Type => RewardType.Command;

                [JsonIgnore]
                public string[] EditorCommands;

                public RewardCommand()
                {
                }

                public RewardCommand(int id, string name, List<string> commands, string description = "", int cooldown = 0, string icon = "", bool nitro = false, int price = 0) : base(id, name, description, cooldown, icon, nitro, price)
                {
                    Commands = commands;
                }

                public override void GiveReward(BasePlayer player)
                {
                    foreach (string cmd in Commands)
                        Interface.Oxide.GetLibrary<Game.Rust.Libraries.Rust>().RunServerCommand(
                            cmd.Replace("$player.id", player.UserIDString)
                                .Replace("$player.name", player.displayName)
                                .Replace("$player.x", player.transform.position.x.ToString())
                                .Replace("$player.y", player.transform.position.y.ToString())
                                .Replace("$player.z", player.transform.position.z.ToString()));
                }

                public override BaseReward Clone()
                {
                    RewardCommand reward = new RewardCommand();
                    CopyValuesTo(reward);
                    reward.EditorCommands = Commands.ToArray();
                    return reward;
                }

                public override void CopyFrom(BaseReward t)
                {
                    t.CopyValuesTo(this);
                    if (t is RewardCommand reward)
                        Commands = reward.EditorCommands.Where(x => !string.IsNullOrEmpty(x)).ToList();
                }
            }

            public class RewardKit : BaseReward
            {
                public string Kit { get; set; }

                public override RewardType Type => RewardType.Kit;

                public RewardKit()
                {
                }

                public RewardKit(int id, string name, string kit, string description = "", int cooldown = 0, string icon = "", bool nitro = false, int price = 0) : base(id, name, description, cooldown, icon, nitro, price)
                {
                    Kit = kit;
                }

                public override void GiveReward(BasePlayer player)
                {
                    if (!ChaosPlugin.Kits.IsLoaded)
                        return;

                    ChaosPlugin.Kits.GiveKit(player, Kit);
                }

                public override BaseReward Clone()
                {
                    RewardKit reward = new RewardKit();
                    CopyValuesTo(reward);
                    reward.Kit = Kit;
                    return reward;
                }

                public override void CopyFrom(BaseReward t)
                {
                    t.CopyValuesTo(this);
                    if (t is RewardKit reward)
                        Kit = reward.Kit;
                }
            }

            public abstract class BaseReward
            {
                public int UniqueId { get; set; }

                public string Name { get; set; }

                public string Description { get; set; }

                public int Cooldown { get; set; }

                public string Icon { get; set; }
                
                [JsonIgnore]
                public string Png { get; set; }

                public bool Nitro { get; set; }

                public int Price { get; set; } = 1;

                [JsonIgnore]
                public abstract RewardType Type { get; }

                public abstract void GiveReward(BasePlayer player);

                public abstract BaseReward Clone();

                public abstract void CopyFrom(BaseReward t);

                public void CopyValuesTo(BaseReward t)
                {
                    t.UniqueId = UniqueId;
                    t.Name = Name;
                    t.Description = Description;
                    t.Cooldown = Cooldown;
                    t.Icon = Icon;
                    t.Nitro = Nitro;
                    t.Price = Price;
                }

                public BaseReward()
                {
                }

                public BaseReward(int id, string name, string description = "", int cooldown = 0, string icon = "", bool nitro = false, int price = 0)
                {
                    UniqueId = id;
                    Name = name;
                    Description = description;
                    Cooldown = cooldown;
                    Icon = icon;
                    Nitro = nitro;
                    Price = price;
                }
            }
        }

        #endregion

        #region Localization

        protected override void PopulatePhrases()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help.Token"] = "Type <color=#ce422b>/discord token</color> to get a unique 6 digit token.",
                ["Help.BotOnly"] = "When you have your unique token, DM the token to our bot (<color=#ce422b>{0}</color>) on Discord to verify your account",
                ["Help.BotOrChannel"] = "When you have your unique token, either DM the token to our bot (<color=#ce422b>{0}</color>) on Discord or post the token in the <color=#ce422b>#{1}</color> channel to verify your account",

                ["Message.Token"] = "Your unique token is <color=#ce422b>{0}</color>",
                ["Message.RewardGiven"] = "<color=#ce422b>Thanks for being a part of our community!</color> You have received your reward",
                ["Message.OnCooldown"] = "You have cooldown on this reward for another <color=#ce422b>{0}</color>",
                ["Message.OnCooldownGlobal"] = "You have cooldown for another <color=#ce422b>{0}</color>",
                ["Message.CantAfford"] = "You do not have enough tokens to claim this reward.",
                ["Message.ValidationExpired"] = "Your Discord validation token has expired! Type <color=#ce422b>/discord</color> to re-validate",
                ["Message.AutoValidated"] = "Your Discord validation token has expired, however we can see you are still in our Discord so you have been automatically re-validated!",
                ["Message.AlreadyRegistered"] = "You are already a member of the Discord group",
                ["Message.NitroOnly"] = "This reward is for Nitro boosters only",

                ["Error.NoItems"] = "The Discord Reward store currently has no items...",
                ["Error.PendingToken"] = "<color=#ce422b>You already have a token pending validation.</color> DM your unique token (<color=#ce422b>{0}</color>) to our bot (<color=#ce422b>{1}</color>) to continue!",
                ["Error.NoPermission"] = "You do not have permission to use this command",

                ["Discord.TokenExpires"] = "This token will expire in {0}.",
                ["Discord.ValidatedToken"] = "Your token has been validated!",
                ["Discord.InvalidToken"] = "The token you entered is invalid. Please copy the 6 digit token you recieved from ingame chat",
                ["Discord.TokenExpired"] = "The token you entered has expired. Please request a new token via the /discord command ingame",
                ["Discord.FailedToFindPlayer"] = "Failed to find a online player with the Steam ID {0}. Unable to complete validation",
                ["Discord.NotOnServer"] = "You must be online in the game server to complete validation",
                ["Discord.UserIsDead"] = "You are currently dead. Some rewards issued on validation may not work whilst you are dead. Try again when you are alive",
                ["Discord.OpenStore"] = "Type /discord in game to open the reward selection menu",

                ["UI.Title"] = "DISCORD REWARDS",
                ["UI.ClaimReward"] = "CLAIM ({0} TOKENS)",
                ["UI.ClaimReward.Free"] = "CLAIM",
                ["UI.NitroOnly"] = "NITRO BOOSTERS ONLY",
                ["UI.Kit"] = "KITS",
                ["UI.Item"] = "ITEMS",
                ["UI.Commands"] = "COMMANDS",
                ["UI.Tokens"] = "{0} TOKENS",
                
                ["UI.OnCooldown"] = "ON COOLDOWN",

                ["UI.Save"] = "SAVE",

                ["UI.KitName"] = "KIT NAME",
                ["UI.Shortname"] = "SHORTNAME",
                ["UI.Amount"] = "AMOUNT",
                ["UI.SkinID"] = "SKIN ID",
                ["UI.Blueprint"] = "BLUEPRINT",
                ["UI.Command"] = "COMMANDS",
                ["UI.Name"] = "NAME",
                ["UI.Description"] = "DESCRIPTION",
                ["UI.Cooldown"] = "COOLDOWN",
                ["UI.Icon"] = "ICON URL",
                ["UI.Price"] = "COST",
                ["UI.Nitro"] = "NITRO ONLY",
                ["UI.EnterName"] = "You must enter a name.",
                ["UI.EnterKit"] = "You must enter a kit name.",
                ["UI.EnterShortname"] = "You must enter an item shortname.",
                ["UI.InvalidShortname"] = "Invalid item shortname entered",
                ["UI.NoCommands"] = "No commands have been entered",
                ["UI.EnterCooldown"] = "You must enter a cooldown time in seconds",
                ["UI.UpdatedReward"] = "You have successfully updated the reward",
                ["UI.CreatedReward"] = "You have added a new reward to the store",

                ["UI.Token"] = "Your token is...",
                ["UI.VerifyExplain"] = "To verify your account, DM this token to our bot on Discord",
                ["UI.Verify.Bot"] = "DM this token to our bot on Discord",
                ["UI.Verify.Channel"] = "Or post your token in the channel\n<color=#00BAFB>#{0}</color>",
            }, this);
        }

        #endregion
    }
}

