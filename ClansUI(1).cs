//Requires: Clans

using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Ext.Chaos;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Ext.Chaos.UIFramework;
using UnityEngine.UI;

using Color = Oxide.Ext.Chaos.UIFramework.Color;
using Font = Oxide.Ext.Chaos.UIFramework.Font;
using GridLayoutGroup = Oxide.Ext.Chaos.UIFramework.GridLayoutGroup;
using HorizontalLayoutGroup = Oxide.Ext.Chaos.UIFramework.HorizontalLayoutGroup;
using VerticalLayoutGroup = Oxide.Ext.Chaos.UIFramework.VerticalLayoutGroup;

namespace Oxide.Plugins
{
    [Info("ClansUI", "k1lly0u", "3.0.5")]
    class ClansUI : ChaosPlugin
    {
        #region Fields
        [PluginReference] Clans Clans;

        private int memberLimit;
        private int inviteLimit;

        private bool alliancesEnabled;
        private int allianceLimit;
        private int allianceInviteLimit;

        private bool canToggleAFF;
        private bool canToggleMFF;
        private bool ownerOnlyMFF;
        private bool ownerOnlyAFF;
        
        private const string COLORED_LABEL = "<color={0}>{1}</color>";
        #endregion

        #region Oxide Hooks       
        private void OnServerInitialized()
        {
            cmd.AddChatCommand(Configuration.Command, this, CommandClanUI);
            cmd.AddConsoleCommand(Configuration.Command, this, "cmdClanUI");

            SetupUIComponents();

            memberLimit = Clans.configData.Clans.MemberLimit;
            inviteLimit = Clans.configData.Clans.Invites.MemberInviteLimit;

            alliancesEnabled = Clans.configData.Clans.Alliance.Enabled;
            allianceLimit = Clans.configData.Clans.Alliance.AllianceLimit;
            allianceInviteLimit = Clans.configData.Clans.Invites.AllianceInviteLimit;

            canToggleMFF = Clans.configData.Clans.MemberFF;
            canToggleAFF = Clans.configData.Clans.Alliance.AllyFF;

            ownerOnlyMFF = Clans.configData.Clans.OwnerFF;
            ownerOnlyAFF = Clans.configData.Clans.Alliance.OwnerFF;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            ChaosUI.Destroy(player, UI_MENU);
            ChaosUI.Destroy(player, UI_MOUSE);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player);
        }
        #endregion

        #region Localization
        protected override void PopulatePhrases()
        {
            m_Messages = new Dictionary<string, string>
            {
                ["UI.Title"] = "Clan Administration",
                ["UI.Clan"] = "Clan",
                ["UI.Leader"] = "Leader",
                ["UI.ViewAlliances"] = "Alliances",
                ["UI.Leave"] = "Leave Clan",
                ["UI.Disband"] = "Disband Clan",
                ["UI.FriendlyFire"] = "Friendly Fire",
                ["UI.ViewMembers"] = "Members",
                ["UI.Allies"] = "Allies",
                ["UI.Members"] = "Members  ({0} / {1})",
                ["UI.MemberInvites"] = "Invites ({0} / {1})",
                ["UI.InviteMember"] = "Invite Member",
                ["UI.Kick"] = "KICK",
                ["UI.Demote"] = "DEMOTE",
                ["UI.Promote"] = "PROMOTE",
                ["UI.Alliances"] = "Alliances  ({0} / {1})",
                ["UI.AllyInvites"] = "Alliance Invites ({0} / {1})",
                ["UI.RequestAlliance"] = "Request Alliance",
                ["UI.AllianceRequests"] = "Alliance Requests",
                ["UI.MemberInvite"] = "Select a player to invite",
                ["UI.AllianceInvite"] = "Select a clan to offer an alliance",
                ["UI.EnterTag"] = "Enter Tag",
                ["UI.Create"] = "Create",
                ["Notification.NotInClan"] = "You are not in a clan"
            };
        }
        #endregion
        
        #region UI
        private const string UI_MENU = "clansui.menu";
        private const string UI_MOUSE = "clansui.mouse";
        
        private enum Menu { Members, Alliances }

        private string m_MagnifyImage;
        
        private Style m_BackgroundStyle;
        private Style m_PanelStyle;
        private Style m_MemberPageStyle;
        private Style m_MemberPageDisabledStyle;
        private Style m_MemberHeaderStyle;
        private Style m_AllianceHeaderStyle;
        private Style m_ButtonStyle;
        private Style m_ButtonDisabledStyle;
        private Style m_TitleStyle;
        private Style m_InfoFieldStyle;
        
        private OutlineComponent m_OutlineGreen;
        private OutlineComponent m_OutlineRed;
        private OutlineComponent m_OutlineAlliance;
        private OutlineComponent m_OutlineMember;

        private OutlineComponent m_OutlineClose;
        private OutlineComponent m_OutlineDark = new OutlineComponent(new Color(0.1647059f, 0.1803922f, 0.1921569f, 1f));
        
         private CommandCallbackHandler m_CallbackHandler;

        private void SetupUIComponents()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);

            m_BackgroundStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.Background.Hex, Configuration.Colors.Background.Alpha),
                Material = Materials.BackgroundBlur,
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled
            };

            m_PanelStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.Panel.Hex, Configuration.Colors.Panel.Alpha),
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled
            };

            m_InfoFieldStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.MembersHeader.Hex, Configuration.Colors.MembersHeader.Alpha),
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled,
            };
            
            m_MemberHeaderStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.MembersHeader.Hex, Configuration.Colors.MembersHeader.Alpha),
                Sprite = Sprites.Background_Rounded_top,
                ImageType = Image.Type.Tiled,
                FontSize = 14,
                Alignment = TextAnchor.MiddleLeft
            };
            
            m_AllianceHeaderStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.AllianceHeader.Hex, Configuration.Colors.AllianceHeader.Alpha),
                Sprite = Sprites.Background_Rounded_top,
                ImageType = Image.Type.Tiled,
                FontSize = 14,
                Alignment = TextAnchor.MiddleLeft
            };

            m_ButtonStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.Button.Hex, Configuration.Colors.Button.Alpha),
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter,
                FontSize = 14
            };
            
            m_ButtonDisabledStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.Button.Hex, Mathf.Min(Configuration.Colors.Button.Alpha, 0.8f)),
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter,
                FontSize = 14,
                FontColor = new Color(1f, 1f, 1f, 0.2f),
            };

            m_MemberPageStyle = new Style
            {
                FontColor = Color.White,
                Alignment = TextAnchor.MiddleCenter
            };

            m_MemberPageDisabledStyle = new Style
            {
                FontColor = new Color(1f, 1f, 1f, 0.3f),
                Alignment = TextAnchor.MiddleCenter
            };
            
            m_TitleStyle = new Style
            {
                FontSize = 18,
                Font = Font.PermanentMarker,
                Alignment = TextAnchor.MiddleLeft,
                WrapMode = VerticalWrapMode.Overflow
            };
            
            m_OutlineGreen = new OutlineComponent(new Color(Configuration.Colors.Highlight1.Hex, Configuration.Colors.Highlight1.Alpha));
            m_OutlineRed = new OutlineComponent(new Color(Configuration.Colors.Highlight3.Hex, Configuration.Colors.Highlight3.Alpha));
            m_OutlineAlliance = new OutlineComponent(new Color(Configuration.Colors.AllianceHeader.Hex, Configuration.Colors.AllianceHeader.Alpha));
            m_OutlineMember = new OutlineComponent(new Color(Configuration.Colors.MembersHeader.Hex, Configuration.Colors.MembersHeader.Alpha));
            m_OutlineClose = new OutlineComponent(new Color(Configuration.Colors.Close.Hex, Configuration.Colors.Close.Alpha));
            
            if (ImageLibrary.IsLoaded)
            {
                ImageLibrary.AddImage("https://chaoscode.io/oxide/Images/magnifyingglass.png", "clansui.search", 0UL, () =>
                {
                    m_MagnifyImage = ImageLibrary.GetImage("clansui.search", 0UL);
                });
            }
        }
        
        #region Layout Groups
        private VerticalLayoutGroup m_InviteLayout = new VerticalLayoutGroup()
        {
	        Area = new Area(-75f, -165f, 75f, 165f),
	        Spacing = new Spacing(0f, 5f),
	        Padding = new Padding(5f, 5f, 5f, 5f),
	        Corner = Corner.TopLeft,
	        FixedSize = new Vector2(140, 20),
            FixedCount = new Vector2Int(1, 13)
        };

        private VerticalLayoutGroup m_MemberLayout = new VerticalLayoutGroup()
        {
            Area = new Area(-317.5f, -177.5f, 317.5f, 177.5f),
            Spacing = new Spacing(0f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedSize = new Vector2(625, 20),
            FixedCount = new Vector2Int(1, 14)
        };

        private HorizontalLayoutGroup m_UserActionLayout = new HorizontalLayoutGroup()
        {
            Area = new Area(-185f, -10f, 185f, 10f),
            Spacing = new Spacing(5f, 0f),
            Padding = new Padding(0f, 0f, 0f, 0f),
            Corner = Corner.TopLeft,
            FixedSize = new Vector2(36.5f, 20),
            FixedCount = new Vector2Int(9, 1)
        };

        private GridLayoutGroup m_InviteMenuLayout = new GridLayoutGroup(6, 16, Axis.Horizontal)
        {
            Area = new Area(-395f, -205f, 395f, 205f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
        };

        private VerticalLayoutGroup m_CurrentAllianceLayout = new VerticalLayoutGroup()
        {
            Area = new Area(-130f, -177.5f, 130f, 177.5f),
            Spacing = new Spacing(0f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedSize = new Vector2(250, 20),
            FixedCount = new Vector2Int(1, 14)
        };
        
        private VerticalLayoutGroup m_AllianceRequestsLayout = new VerticalLayoutGroup()
        {
            Area = new Area(-130f, -177.5f, 130f, 177.5f),
            Spacing = new Spacing(0f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedSize = new Vector2(250, 20),
            FixedCount = new Vector2Int(1, 14)
        };

        private VerticalLayoutGroup m_AllianceInvitesLayout = new VerticalLayoutGroup()
        {
            Area = new Area(-130f, -165f, 130f, 165f),
            Spacing = new Spacing(0f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedSize = new Vector2(250, 20),
            FixedCount = new Vector2Int(1, 13)
        };
        #endregion

        private class UserPage
        {
            public int Page1 = 0;
            public int Page2 = 0;
            public int Page3 = 0;

            public void Reset()
            {
                Page1 = 0;
                Page2 = 0;
                Page3 = 0;
            }
        }

        private void CreateClanMenu(BasePlayer player, string tag = "")
        {
            BaseContainer root = ImageContainer.Create(UI_MENU, Layer.Overlay, Anchor.Center, new Offset(-150f, -37.5f, 150f, 37.5f))
                .WithStyle(m_BackgroundStyle)
                .WithChildren(parent =>
                {
                    CreateTitleBar(player, parent, ()=> 
                    {
                        ChaosUI.Destroy(player, UI_MENU);
                        ChaosUI.Destroy(player, UI_MOUSE);
                    });

                    ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -70f, -5f, -40f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(contents =>
                        {
                            ImageContainer.Create(contents, Anchor.HorizontalCenterStretch, new Offset(5f, -10f, -90f, 10f))
                                .WithStyle(m_PanelStyle)
                                .WithChildren(clanTag =>
                                {
                                    ImageContainer.Create(clanTag, Anchor.TopLeft, new Offset(0f, -20f, 80f, 0f))
                                        .WithStyle(m_InfoFieldStyle)
                                        .WithChildren(panel =>
                                        {
                                            TextContainer.Create(panel, Anchor.FullStretch, Offset.zero)
                                                .WithText(GetString("UI.EnterTag", player))
                                                .WithAlignment(TextAnchor.MiddleCenter);

                                        });

                                    InputFieldContainer.Create(clanTag, Anchor.FullStretch, new Offset(85f, 0f, -5f, 0f))
                                        .WithText(tag)
                                        .WithAlignment(TextAnchor.MiddleLeft)
                                        .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                tag = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                                CreateClanMenu(player, tag);
                                            }, $"{player.userID}.createclan.input");
                                });
                            
                            ImageContainer.Create(contents, Anchor.CenterRight, new Offset(-85f, -10f, -5f, 10f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(accept =>
                                {
                                    TextContainer.Create(accept, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(m_ButtonStyle)
                                        .WithSize(12)
                                        .WithText(GetString("UI.Create", player))
                                        .WithWrapMode(VerticalWrapMode.Overflow);

                                    ButtonContainer.Create(accept, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                Clans.CreateClan(player, tag, "");
                                                
                                                Clans.Clan clan = Clans.Instance.storedData.FindClanByID(player.userID);
                                                if (clan == null)
                                                    CreateClanMenu(player, tag);
                                                else OpenClanMenu(player, clan);
                                            }, $"{player.UserIDString}.createclan.create");
                                })
                                .WithOutline(m_OutlineAlliance);
                        });
                });
            
            ChaosUI.Destroy(player, UI_MENU);
            ChaosUI.Show(player, root);
        }

        private void OpenClanMenu(BasePlayer player, Clans.Clan clan, Menu menu = Menu.Members, UserPage userPage = null)
        {
            if (userPage == null)
                userPage = new UserPage();
            
            BaseContainer root = ImageContainer.Create(UI_MENU, Layer.Overlay, Anchor.Center, new Offset(-200f, -245f, 600f, 245f))
	            .WithStyle(m_BackgroundStyle)
	            .WithChildren(parent =>
	            {
		            CreateTitleBar(player, parent, ()=> 
                    {
                        ChaosUI.Destroy(player, UI_MENU);
                        ChaosUI.Destroy(player, UI_MOUSE);
                    });

                    CreateHeaderBar(player, parent, clan, menu, userPage);

                    if (menu == Menu.Members)
                        CreateMemberView(player, parent, clan, userPage);
                    else CreateAllianceView(player, parent, clan, userPage);

                    CreateFooterBar(player, parent, clan);
                });
            
            ChaosUI.Destroy(player, UI_MENU);
            ChaosUI.Show(player, root);
        }

        private void CreateTitleBar(BasePlayer player, BaseContainer parent, Action onExitAction)
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -35f, -5f, -5f))
                .WithStyle(m_PanelStyle)
                .WithChildren(titleBar =>
                {
                    TextContainer.Create(titleBar, Anchor.CenterLeft, new Offset(5f, -15f, 205f, 15f))
                        .WithStyle(m_TitleStyle)
                        .WithText(GetString("UI.Title", player))
                        .WithOutline(m_OutlineDark);

                    ImageContainer.Create(titleBar, Anchor.CenterRight, new Offset(-25f, -10f, -5f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithOutline(m_OutlineClose)
                        .WithChildren(exit =>
                        {
                            TextContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                .WithText("✘")
                                .WithAlignment(TextAnchor.MiddleCenter)
                                .WithWrapMode(VerticalWrapMode.Overflow);

                            ButtonContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg => onExitAction(), $"{player.UserIDString}.exit");

                        });

                });
        }

        private void CreateHeaderBar(BasePlayer player, BaseContainer parent, Clans.Clan clan, Menu menu, UserPage userPage)
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -70f, -5f, -40f))
			.WithStyle(m_PanelStyle)
			.WithChildren(header =>
			{
				ImageContainer.Create(header, Anchor.TopLeft, new Offset(5f, -25f, 105f, -5f))
                    .WithStyle(m_PanelStyle)
					.WithChildren(clanTag =>
					{
						ImageContainer.Create(clanTag, Anchor.TopLeft, new Offset(0f, -20f, 50f, 0f))
                            .WithStyle(m_InfoFieldStyle)
							.WithChildren(panel =>
							{
								TextContainer.Create(panel, Anchor.FullStretch, Offset.zero)
									.WithText(GetString("UI.Clan", player))
									.WithAlignment(TextAnchor.MiddleCenter);

							});

						TextContainer.Create(clanTag, Anchor.FullStretch, new Offset(0f, 0f, -5f, 0f))
							.WithText(clan.Tag)
							.WithAlignment(TextAnchor.MiddleRight);

					});

				ImageContainer.Create(header, Anchor.TopLeft, new Offset(110f, -25f, 340f, -5f))
                    .WithStyle(m_PanelStyle)
					.WithChildren(leader =>
					{
						ImageContainer.Create(leader, Anchor.TopLeft, new Offset(0f, -20f, 60f, 0f))
                            .WithStyle(m_InfoFieldStyle)
							.WithChildren(panel =>
							{
								TextContainer.Create(panel, Anchor.FullStretch, Offset.zero)
									.WithText(GetString("UI.Leader", player))
									.WithAlignment(TextAnchor.MiddleCenter);

							});

						TextContainer.Create(leader, Anchor.FullStretch, new Offset(50f, 0f, -5f, 0f))
							.WithText(clan.GetOwner().DisplayName)
							.WithAlignment(TextAnchor.MiddleRight);

					});

                Clans.Clan.Member member = Clans.storedData.FindMemberByID(player.userID);
                
                if (member != null && ((canToggleAFF && alliancesEnabled) || canToggleMFF))
                {
                    ImageContainer.Create(header, Anchor.TopLeft, new Offset(345f, -25f, 600.5f, -5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(friendlyfire =>
                        {
                            ImageContainer.Create(friendlyfire, Anchor.TopLeft, new Offset(0f, -20f, 90f, 0f))
                                .WithStyle(m_InfoFieldStyle)
                                .WithChildren(panel =>
                                {
                                    TextContainer.Create(panel, Anchor.FullStretch, Offset.zero)
                                        .WithText(GetString("UI.FriendlyFire", player))
                                        .WithAlignment(TextAnchor.MiddleCenter);
                                });

                            if (canToggleAFF && alliancesEnabled)
                            {
                                if (!ownerOnlyAFF || (ownerOnlyAFF && (clan.IsOwner(player.userID) || clan.IsCouncil(player.userID))))
                                {
                                    TextContainer.Create(friendlyfire, Anchor.Center, new Offset(-37.75f, -10f, 12.25f, 10f))
                                        .WithText(GetString("UI.Allies", player))
                                        .WithAlignment(TextAnchor.MiddleRight);

                                    ImageContainer.Create(friendlyfire, Anchor.CenterRight, new Offset(-110.5f, -7.5f, -95.5f, 7.5f))
                                        .WithStyle(m_ButtonStyle)
                                        .WithChildren(allyToggle =>
                                        {
                                            if (member.AllyFFEnabled)
                                            {
                                                ImageContainer.Create(allyToggle, Anchor.FullStretch, new Offset(2.5f, 2.5f, -2.5f, -2.5f))
                                                    .WithColor(m_OutlineGreen.Color)
                                                    .WithSprite(Sprites.Background_Rounded)
                                                    .WithImageType(Image.Type.Tiled);
                                            }

                                            ButtonContainer.Create(allyToggle, Anchor.FullStretch, Offset.zero)
                                                .WithColor(Color.Clear)
                                                .WithCallback(m_CallbackHandler, arg =>
                                                {
                                                    if (ownerOnlyAFF && member.Role >= Clans.Clan.Member.MemberRole.Moderator)
                                                    {
                                                        player.ChatMessage(lang.GetMessage("Notification.FF.ToggleNotOwner", Clans, player.UserIDString));
                                                        return;
                                                    }

                                                    member.AllyFFEnabled = !member.AllyFFEnabled;

                                                    if (ownerOnlyAFF)
                                                    {
                                                        foreach (KeyValuePair<ulong, Clans.Clan.Member> kvp in clan.ClanMembers)
                                                        {
                                                            if (kvp.Key.Equals(player.userID))
                                                                continue;

                                                            kvp.Value.AllyFFEnabled = member.AllyFFEnabled;

                                                            BasePlayer memberPlayer = BasePlayer.Find(kvp.Key.ToString());

                                                            if (memberPlayer != null && memberPlayer.IsConnected)
                                                            {
                                                                memberPlayer.ChatMessage(string.Format(lang.GetMessage("Notification.FF.OwnerAllyToggle", Clans, memberPlayer.UserIDString),
                                                                    string.Format(COLORED_LABEL, clan.GetRoleColor(member.Role), player.displayName),
                                                                    !kvp.Value.MemberFFEnabled ? lang.GetMessage("Notification.FF.MemberEnabled", Clans, memberPlayer.UserIDString) : 
                                                                        lang.GetMessage("Notification.FF.MemberDisabled", Clans, memberPlayer.UserIDString)));
                                                            }
                                                        }
                                                    }

                                                    OpenClanMenu(player, clan, menu, userPage);
                                                }, $"{player.UserIDString}.toggleallyff");
                                        });
                                }
                            }

                            if (canToggleMFF)
                            {
                                TextContainer.Create(friendlyfire, Anchor.Center, new Offset(32.25f, -10f, 102.25f, 10f))
                                    .WithText(GetString("UI.ViewMembers", player))
                                    .WithAlignment(TextAnchor.MiddleRight);

                                ImageContainer.Create(friendlyfire, Anchor.CenterRight, new Offset(-20f, -7.5f, -5f, 7.5f))
                                    .WithStyle(m_ButtonStyle)
                                    .WithChildren(memberToggle =>
                                    {
                                        if (member.MemberFFEnabled)
                                        {
                                            ImageContainer.Create(memberToggle, Anchor.FullStretch, new Offset(2.5f, 2.5f, -2.5f, -2.5f))
                                                .WithColor(m_OutlineGreen.Color)
                                                .WithSprite(Sprites.Background_Rounded)
                                                .WithImageType(Image.Type.Tiled);
                                        }

                                        ButtonContainer.Create(memberToggle, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (ownerOnlyMFF && member.Role >= Clans.Clan.Member.MemberRole.Moderator)
                                                {
                                                    player.ChatMessage(lang.GetMessage("Notification.FF.ToggleNotOwner", Clans, player.UserIDString));
                                                    return;
                                                }

                                                member.MemberFFEnabled = !member.MemberFFEnabled;

                                                if (ownerOnlyMFF)
                                                {
                                                    foreach (KeyValuePair<ulong, Clans.Clan.Member> kvp in clan.ClanMembers)
                                                    {
                                                        if (kvp.Key.Equals(player.userID))
                                                            continue;

                                                        kvp.Value.MemberFFEnabled = member.MemberFFEnabled;

                                                        BasePlayer memberPlayer = BasePlayer.Find(kvp.Key.ToString());

                                                        if (memberPlayer != null && memberPlayer.IsConnected)
                                                        {
                                                            memberPlayer.ChatMessage(string.Format(lang.GetMessage("Notification.FF.OwnerToggle", Clans, memberPlayer.UserIDString),
                                                                string.Format(COLORED_LABEL, clan.GetRoleColor(member.Role), player.displayName),
                                                                !kvp.Value.MemberFFEnabled ? lang.GetMessage("Notification.FF.MemberEnabled", Clans, memberPlayer.UserIDString) : lang.GetMessage("Notification.FF.MemberDisabled", Clans, memberPlayer.UserIDString)));
                                                        }
                                                    }
                                                }
                                                
                                                OpenClanMenu(player, clan, menu, userPage);
                                            }, $"{player.userID}.togglememberff");
                                    });
                            }
                        });
                }

                if (menu == Menu.Members)
                {
                    if (alliancesEnabled)
                    {
                        ImageContainer.Create(header, Anchor.CenterRight, new Offset(-85f, -10f, -5f, 10f))
                            .WithStyle(m_ButtonStyle)
                            .WithOutline(m_OutlineAlliance)
                            .WithChildren(viewalliances =>
                            {
                                TextContainer.Create(viewalliances, Anchor.FullStretch, Offset.zero)
                                    .WithText(GetString("UI.ViewAlliances", player))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                ButtonContainer.Create(viewalliances, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        userPage.Reset();
                                        OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                    }, $"{player.UserIDString}.viewalliances");
                            });
                    }
                }
                else
                {
                    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-85f, -10f, -5f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithOutline(m_OutlineMember)
                        .WithChildren(viewalliances =>
                        {
                            TextContainer.Create(viewalliances, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.ViewMembers", player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ButtonContainer.Create(viewalliances, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    userPage.Reset();
                                    OpenClanMenu(player, clan, Menu.Members, userPage);
                                }, $"{player.UserIDString}.viewmembers");
                        });
                }
            });
        }

        private void CreateFooterBar(BasePlayer player, BaseContainer parent, Clans.Clan clan)
        {
            ImageContainer.Create(parent, Anchor.BottomStretch, new Offset(5f, 5f, -5f, 35f))
                .WithStyle(m_PanelStyle)
                .WithChildren(footer =>
                {
                    ImageContainer.Create(footer, Anchor.CenterRight, new Offset(-105f, -10f, -5f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithOutline(m_OutlineRed)
                        .WithChildren(leaveclan =>
                        {
                            TextContainer.Create(leaveclan, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.Leave", player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ButtonContainer.Create(leaveclan, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    Clans.LeaveClan(player);
                                    ChaosUI.Destroy(player, UI_MENU);
                                    ChaosUI.Destroy(player, UI_MOUSE);
                                }, $"{player.UserIDString}.leaveclan");
                        });

                    if (clan.IsOwner(player.userID))
                    {
                        ImageContainer.Create(footer, Anchor.CenterLeft, new Offset(5f, -10f, 105f, 10f))
                            .WithStyle(m_ButtonStyle)
                            .WithOutline(m_OutlineRed)
                            .WithChildren(disbandclan =>
                            {
                                TextContainer.Create(disbandclan, Anchor.FullStretch, Offset.zero)
                                    .WithText(GetString("UI.Disband", player))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                ButtonContainer.Create(disbandclan, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        Clans.DisbandClan(player);
                                        ChaosUI.Destroy(player, UI_MENU);
                                        ChaosUI.Destroy(player, UI_MOUSE);
                                    }, $"{player.UserIDString}.disbandclan");

                            });
                    }
                });
        }
        #endregion
        
        #region Member View
        private void CreateMemberView(BasePlayer player, BaseContainer parent, Clans.Clan clan, UserPage userPage)
        {
            bool canManageInvites = clan.IsOwner(player.userID) || clan.IsCouncil(player.userID) || clan.IsModerator(player.userID);
            
            BaseContainer.Create(parent, Anchor.FullStretch, new Offset(5f, 40f, -5f, -75f))
                .WithChildren(members =>
                {
                    ImageContainer.Create(members, Anchor.CenterLeft, new Offset(0f, -187.5f, 635f, 187.5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(memberView =>
                        {
                            ImageContainer.Create(memberView, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                                .WithStyle(m_MemberHeaderStyle)
                                .WithChildren(header =>
                                {
                                    TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                        .WithText(FormatString("UI.Members", player, clan.MemberCount, memberLimit))
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                    
                                    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-40f, -10f, -20f, 10f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(prevpage =>
                                        {
                                            TextContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                                .WithText("◀")
                                                .WithStyle(userPage.Page1 > 0 ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                            if (userPage.Page1 > 0)
                                            {
                                                ButtonContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                                .WithColor(Color.Clear)
                                                .WithCallback(m_CallbackHandler, arg=>
                                                {
                                                    userPage.Page1 -= 1;
                                                    OpenClanMenu(player, clan, Menu.Members, userPage);
                                                }, $"{player.UserIDString}.memberpage.back");
                                            }
                                        });

                                    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-20f, -10f, 0f, 10f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(nextpage =>
                                        {
                                            bool hasNextPage = m_MemberLayout.HasNextPage(userPage.Page1, clan.MemberCount); 
                                            TextContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                .WithText("▶")
                                                .WithStyle(hasNextPage ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                            if (hasNextPage)
                                            {
                                                ButtonContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                    .WithColor(Color.Clear)
                                                    .WithCallback(m_CallbackHandler, arg=>
                                                    {
                                                        userPage.Page1 += 1;
                                                        OpenClanMenu(player, clan, Menu.Members, userPage);
                                                    },$"{player.UserIDString}.memberpage.next");
                                            }
                                        });
                                });

                            BaseContainer memberLayout = BaseContainer.Create(memberView, Anchor.FullStretch, new Offset(0f, 0f, 0f, -20f));
                            
                            m_MemberLayout.RecalculateSize();

                            int index = 0;
                            for (int i = m_MemberLayout.PerPage * userPage.Page1; i < Mathf.Min(clan.ClanMembers.Count, (userPage.Page1 + 1) * m_MemberLayout.PerPage); i++)
                            {
                                KeyValuePair<ulong, Clans.Clan.Member> kvp = clan.ClanMembers.ElementAt(i);

                                m_MemberLayout.Evaluate(index, out Anchor anchor, out Offset offset);
                                
                                CreateMemberField(player, clan, userPage, ulong.Parse(kvp.Key.ToString()), kvp.Value, memberLayout, anchor, offset);
                                index += 1;
                            }
                        });

                    ImageContainer.Create(members, Anchor.CenterRight, new Offset(-150f, -187.5f, 0f, 187.5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(inviteView =>
                        {
                            ImageContainer.Create(inviteView, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                                .WithStyle(m_MemberHeaderStyle)
                                .WithChildren(header =>
                                {
                                    TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                        .WithText(FormatString("UI.MemberInvites", player, clan.MemberInviteCount, inviteLimit))
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                    
                                    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-40f, -10f, -20f, 10f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(prevpage =>
                                        {
                                            TextContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                                .WithText("◀")
                                                .WithStyle(userPage.Page2 > 0 ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                            if (userPage.Page2 > 0)
                                            {
                                                ButtonContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                                .WithColor(Color.Clear)
                                                .WithCallback(m_CallbackHandler, arg=>
                                                {
                                                    userPage.Page2 -= 1;
                                                    OpenClanMenu(player, clan, Menu.Members, userPage);
                                                }, $"{player.UserIDString}.memberinvites.back");
                                            }
                                        });

                                    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-20f, -10f, 0f, 10f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(nextpage =>
                                        {
                                            bool hasNextPage = m_InviteLayout.HasNextPage(userPage.Page2, clan.MemberInviteCount); 
                                            TextContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                .WithText("▶")
                                                .WithStyle(hasNextPage ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                            if (hasNextPage)
                                            {
                                                ButtonContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                    .WithColor(Color.Clear)
                                                    .WithCallback(m_CallbackHandler, arg=>
                                                    {                                                    
                                                        userPage.Page2 += 1;
                                                        OpenClanMenu(player, clan, Menu.Members, userPage);
                                                    }, $"{player.UserIDString}.memberinvites.next");
                                            }
                                        });
                                });

                            BaseContainer invitePlayerButton = ImageContainer.Create(inviteView, Anchor.TopCenter, new Offset(-69f, -44f, 69f, -26f))
                                .WithStyle(canManageInvites ? m_ButtonStyle : m_ButtonDisabledStyle)
                                .WithChildren(invitePlayer =>
                                {
                                    TextContainer.Create(invitePlayer, Anchor.FullStretch, Offset.zero)
                                        .WithStyle(canManageInvites ? m_ButtonStyle : m_ButtonDisabledStyle)
                                        .WithText(GetString("UI.InviteMember", player))
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    List<BasePlayer> allPlayerList = Facepunch.Pool.Get<List<BasePlayer>>();
                                    allPlayerList.AddRange(BasePlayer.activePlayerList);
                                    allPlayerList.AddRange(BasePlayer.sleepingPlayerList);
                                    allPlayerList.Remove(player);
                                    
                                    ButtonContainer.Create(invitePlayer, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            if (canManageInvites)
                                            {
                                                InviteData<BasePlayer> data = new InviteData<BasePlayer>
                                                {
                                                    Clan = clan,
                                                    Menu = Menu.Members,
                                                    Collection = allPlayerList,
                                                    DisplayFunction = basePlayer => basePlayer.displayName,
                                                    SearchFunction = (basePlayer, s) => basePlayer.displayName.Contains(s, CompareOptions.OrdinalIgnoreCase),
                                                    OnSelectFunction = (basePlayer) =>
                                                    {
                                                        Clans.InvitePlayer(player, basePlayer.userID);
                                                        OpenClanMenu(player, clan, Menu.Members, userPage);
                                                        Facepunch.Pool.FreeUnmanaged(ref allPlayerList);
                                                    }
                                                };

                                                OpenInviteMenu(player, data, userPage);
                                            }
                                            else player.ChatMessage(lang.GetMessage("Notification.Invite.NoPermissions", Clans, player.UserIDString));
                                        }, $"{player.UserIDString}.invitemember");
                                });

                            if (canManageInvites)
                                invitePlayerButton.WithOutline(m_OutlineMember);

                            BaseContainer inviteLayout = BaseContainer.Create(inviteView, Anchor.FullStretch, new Offset(0f, 0f, 0f, -45f));
                            m_InviteLayout.RecalculateSize();
                            int index = 0;
                            for (int i = userPage.Page2 * m_InviteLayout.PerPage; i < Mathf.Min(clan.MemberInviteCount, (userPage.Page2 + 1) * m_InviteLayout.PerPage); i++)
                            {
                                KeyValuePair<ulong, Clans.Clan.MemberInvite> kvp = clan.MemberInvites.ElementAt(i);

                                m_InviteLayout.Evaluate(index, out Anchor anchor, out Offset offset);
                                CreateMemberInviteField(player, clan, userPage, canManageInvites, kvp.Key.ToString(), kvp.Value, inviteLayout, anchor, offset);
                                index += 1;
                            }
                        });
                });
        }

        private Hash<Clans.Clan.Member.MemberRole, Color> m_RoleColors = new Hash<Clans.Clan.Member.MemberRole, Color>();
        
        private void CreateMemberField(BasePlayer player, Clans.Clan clan, UserPage userPage, ulong memberId, Clans.Clan.Member member, BaseContainer parent, Anchor anchor, Offset offset)
        {
            ImageContainer.Create(parent, anchor, offset)
                .WithStyle(m_PanelStyle)
                .WithChildren(template =>
                {
                    if (!m_RoleColors.TryGetValue(member.Role, out Color roleColor))
                        roleColor = m_RoleColors[member.Role] = new Color(clan.GetRoleColor(member.Role));
                    
                    ImageContainer.Create(template, Anchor.CenterLeft, new Offset(0f, -10f, 120f, 10f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(memberName =>
                        {
                            TextContainer.Create(memberName, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                .WithText(member.DisplayName)
                                .WithColor(roleColor)
                                .WithAlignment(TextAnchor.MiddleLeft);
                        });
                    
                    bool isClanOwner = clan.IsOwner(player.userID) && memberId != player.userID;
                    bool canDemote = isClanOwner && (clan.IsCouncil(memberId) || clan.IsModerator(memberId));
                    bool canPromote = isClanOwner && (!clan.IsOwner(memberId) && !clan.IsCouncil(memberId));
                    bool canKick = member.Role == Clans.Clan.Member.MemberRole.Member && Clans.storedData.FindMemberByID(player.userID).Role <= Clans.Clan.Member.MemberRole.Moderator;
                    
                    BaseContainer promoteButton = ImageContainer.Create(template, Anchor.CenterLeft, new Offset(125f, -9f, 185f, 9f))
                        .WithStyle(canPromote ? m_ButtonStyle : m_ButtonDisabledStyle)
                        .WithChildren(promote =>
                        {
                            TextContainer.Create(promote, Anchor.FullStretch, Offset.zero)
                                .WithStyle(canPromote ? m_ButtonStyle : m_ButtonDisabledStyle)
                                .WithSize(12)
                                .WithText(GetString("UI.Promote", player));

                            if (canPromote)
                            {
                                ButtonContainer.Create(promote, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        Clans.PromotePlayer(player, memberId);
                                        OpenClanMenu(player, clan, Menu.Members, userPage);
                                    }, $"{player.UserIDString}.promote.{memberId}");
                            }

                        });
                    
                    if (canPromote)
                        promoteButton.WithOutline(m_OutlineMember);

                    if (canKick)
                    {
                        ImageContainer.Create(template, Anchor.CenterLeft, new Offset(190f, -9f, 250f, 9f))
                            .WithStyle(m_ButtonStyle)
                            .WithOutline(m_OutlineRed)
                            .WithChildren(demote =>
                            {
                                TextContainer.Create(demote, Anchor.FullStretch, Offset.zero)
                                    .WithStyle(m_ButtonStyle)
                                    .WithSize(12)
                                    .WithText(GetString("UI.Kick", player));

                                ButtonContainer.Create(demote, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        Clans.KickPlayer(player, memberId);
                                        OpenClanMenu(player, clan, Menu.Members, userPage);
                                    }, $"{player.UserIDString}.kick.{memberId}");
                            });
                    }
                    else
                    {
                        BaseContainer demoteButton = ImageContainer.Create(template, Anchor.CenterLeft, new Offset(190f, -9f, 250f, 9f))
                            .WithStyle(canDemote ? m_ButtonStyle : m_ButtonDisabledStyle)
                            .WithChildren(demote =>
                            {
                                TextContainer.Create(demote, Anchor.FullStretch, Offset.zero)
                                    .WithStyle(canDemote ? m_ButtonStyle : m_ButtonDisabledStyle)
                                    .WithSize(12)
                                    .WithText(GetString("UI.Demote", player));

                                if (canDemote)
                                {
                                    ButtonContainer.Create(demote, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            Clans.DemotePlayer(player, memberId);
                                            OpenClanMenu(player, clan, Menu.Members, userPage);
                                        }, $"{player.UserIDString}.demote.{memberId}");
                                }
                            });

                        if (canDemote)
                            demoteButton.WithOutline(m_OutlineRed);
                    }
                    

                    BaseContainer.Create(template, Anchor.FullStretch, new Offset(255f, 0f, 0f, 0f))
                        .WithLayoutGroup(m_UserActionLayout, Configuration.MemberCommands, 0, (int i, ConfigData.ClanCommand t, BaseContainer commands, Anchor cmdAnchor, Offset cmdOffset) =>
                            {
                                ImageContainer.Create(commands, cmdAnchor, cmdOffset)
                                    .WithStyle(m_InfoFieldStyle)
                                    .WithChildren(command =>
                                    {
                                        TextContainer.Create(command, Anchor.FullStretch, Offset.zero)
                                            .WithSize(10)
                                            .WithText(t.Name)
                                            .WithAlignment(TextAnchor.MiddleCenter);

                                        ButtonContainer.Create(command, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, 
                                                arg => rust.RunClientCommand(player, "chat.say", t.Command.Replace("{playerName}", member.DisplayName).Replace("{playerId}", memberId.ToString())), 
                                                $"{player.UserIDString}.command.{memberId}.{i}");
                                    });

                            });

                });
        }

        private void CreateMemberInviteField(BasePlayer player, Clans.Clan clan, UserPage userPage, bool canManageInvites, string memberId, Clans.Clan.MemberInvite member, BaseContainer parent, Anchor anchor, Offset offset)
        {
            ImageContainer.Create(parent, anchor, offset)
                .WithStyle(m_PanelStyle)
                .WithChildren(template =>
                {
                    TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -20f, 0f))
                        .WithText(member.DisplayName)
                        .WithAlignment(TextAnchor.MiddleLeft);

                    BaseContainer revokeInviteButton = ImageContainer.Create(template, Anchor.CenterRight, new Offset(-19f, -9f, -1f, 9f))
                        .WithStyle(canManageInvites ? m_ButtonStyle : m_ButtonDisabledStyle)
                        .WithChildren(revoke =>
                        {
                            TextContainer.Create(revoke, Anchor.FullStretch, Offset.zero)
                                .WithStyle(canManageInvites ? m_ButtonStyle : m_ButtonDisabledStyle)
                                .WithSize(12)
                                .WithText("✘")
                                .WithAlignment(TextAnchor.MiddleCenter)
                                .WithWrapMode(VerticalWrapMode.Overflow);

                            ButtonContainer.Create(revoke, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    Clans.WithdrawInvite(player, memberId);
                                    OpenClanMenu(player, clan, Menu.Members, userPage);
                                }, $"{player.UserIDString}.revokeinvite.{memberId}");

                        });

                    if (canManageInvites)
                        revokeInviteButton.WithOutline(m_OutlineRed);
                });
        }

        #endregion
        
        #region Alliance View

        private void CreateAllianceView(BasePlayer player, BaseContainer parent, Clans.Clan clan, UserPage userPage)
        {
            BaseContainer.Create(parent, Anchor.FullStretch, new Offset(5f, 40f, -5f, -75f))
			.WithChildren(alliances =>
			{
                bool canManageAlliance = clan.IsOwner(player.userID) || clan.IsCouncil(player.userID);
                
                // Current Alliances
				ImageContainer.Create(alliances, Anchor.CenterLeft, new Offset(0f, -187.5f, 260f, 187.5f))
					.WithStyle(m_PanelStyle)
					.WithChildren(current =>
					{
						ImageContainer.Create(current, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
							.WithStyle(m_AllianceHeaderStyle)
							.WithChildren(header =>
							{
								TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
									.WithText(FormatString("UI.Alliances", player, clan.AllianceCount, allianceLimit))
									.WithAlignment(TextAnchor.MiddleLeft);
                                
                                ImageContainer.Create(header, Anchor.CenterRight, new Offset(-40f, -10f, -20f, 10f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(prevpage =>
                                        {
                                            TextContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                                .WithText("◀")
                                                .WithStyle(userPage.Page1 > 0 ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                            if (userPage.Page1 > 0)
                                            {
                                                ButtonContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                                .WithColor(Color.Clear)
                                                .WithCallback(m_CallbackHandler, arg=>
                                                {
                                                    userPage.Page1 -= 1;
                                                    OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                }, $"{player.UserIDString}.alliancepage.back");
                                            }
                                        });

                                    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-20f, -10f, 0f, 10f))
                                        .WithColor(Color.Clear)
                                        .WithChildren(nextpage =>
                                        {
                                            bool hasNextPage = m_CurrentAllianceLayout.HasNextPage(userPage.Page1, clan.AllianceCount); 
                                            TextContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                .WithText("▶")
                                                .WithStyle(hasNextPage ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                            if (hasNextPage)
                                            {
                                                ButtonContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                    .WithColor(Color.Clear)
                                                    .WithCallback(m_CallbackHandler, arg=>
                                                    {
                                                        userPage.Page1 += 1;
                                                        OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                    },$"{player.UserIDString}.alliancepage.next");
                                            }
                                        });
                            });

						BaseContainer.Create(current, Anchor.FullStretch, new Offset(0f, 0f, 0f, -20f))
							.WithLayoutGroup(m_CurrentAllianceLayout, clan.Alliances, userPage.Page1, (int i, string t, BaseContainer layout, Anchor anchor, Offset offset) =>
								{
									ImageContainer.Create(layout, anchor, offset)
										.WithStyle(m_PanelStyle)
										.WithChildren(template =>
										{
											TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -20f, 0f))
												.WithText(t)
												.WithAlignment(TextAnchor.MiddleLeft);

											BaseContainer revokeAllianceButton = ImageContainer.Create(template, Anchor.CenterRight, new Offset(-19f, -9f, -1f, 9f))
                                                .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
                                                .WithChildren(revoke =>
												{
													TextContainer.Create(revoke, Anchor.FullStretch, Offset.zero)
                                                        .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
														.WithSize(12)
														.WithText("✘")
														.WithWrapMode(VerticalWrapMode.Overflow);

                                                    ButtonContainer.Create(revoke, Anchor.FullStretch, Offset.zero)
                                                        .WithColor(Color.Clear)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            Clans.RevokeAlliance(player, t);
                                                            OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                        }, $"{player.UserIDString}.revokealliance.{i}");
                                                });

                                            if (canManageAlliance)
                                                revokeAllianceButton.WithOutline(m_OutlineRed);
                                        });
                                });
                    });

                // Alliance Invites
				ImageContainer.Create(alliances, Anchor.Center, new Offset(-130f, -187.5f, 130f, 187.5f))
					.WithStyle(m_PanelStyle)
					.WithChildren(invites =>
					{
						ImageContainer.Create(invites, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithStyle(m_AllianceHeaderStyle)
							.WithChildren(header =>
							{
								TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
									.WithText(FormatString("UI.AllyInvites", player, clan.AllianceInviteCount, allianceInviteLimit))
									.WithAlignment(TextAnchor.MiddleLeft);
                                
                                ImageContainer.Create(header, Anchor.CenterRight, new Offset(-40f, -10f, -20f, 10f))
                                    .WithColor(Color.Clear)
                                    .WithChildren(prevpage =>
                                    {
                                        TextContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                            .WithText("◀")
                                            .WithStyle(userPage.Page2 > 0 ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                        if (userPage.Page2 > 0)
                                        {
                                            ButtonContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg=>
                                            {
                                                userPage.Page2 -= 1;
                                                OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                            }, $"{player.UserIDString}.allianceinvitepage.back");
                                        }
                                    });

                                ImageContainer.Create(header, Anchor.CenterRight, new Offset(-20f, -10f, 0f, 10f))
                                    .WithColor(Color.Clear)
                                    .WithChildren(nextpage =>
                                    {
                                        bool hasNextPage = m_CurrentAllianceLayout.HasNextPage(userPage.Page2, clan.AllianceInviteCount); 
                                        TextContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                            .WithText("▶")
                                            .WithStyle(hasNextPage ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                        if (hasNextPage)
                                        {
                                            ButtonContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                .WithColor(Color.Clear)
                                                .WithCallback(m_CallbackHandler, arg=>
                                                {
                                                    userPage.Page2 += 1;
                                                    OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                },$"{player.UserIDString}.allianceinvitepage.next");
                                        }
                                    });
                            });
                        
						BaseContainer requestAllianceButton = ImageContainer.Create(invites, Anchor.TopCenter, new Offset(-125f, -44f, 125f, -26f))
							.WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
                            .WithChildren(invite =>
							{
								TextContainer.Create(invite, Anchor.FullStretch, Offset.zero)
									.WithText(GetString("UI.RequestAlliance", player))
                                    .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
									.WithAlignment(TextAnchor.MiddleCenter);

                                List<string> allClanTags = Facepunch.Pool.Get<List<string>>();
                                allClanTags.AddRange(Clans.storedData.clans.Keys);
                                allClanTags.Remove(clan.Tag);
                                
                                ButtonContainer.Create(invite, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        if (canManageAlliance)
                                        {
                                            InviteData<string> data = new InviteData<string>
                                            {
                                                Clan = clan,
                                                Menu = Menu.Alliances,
                                                Collection = allClanTags,
                                                DisplayFunction = s => s,
                                                SearchFunction = (tag, s) => tag.Contains(s, CompareOptions.OrdinalIgnoreCase),
                                                OnSelectFunction = (s) =>
                                                {
                                                    Clans.OfferAlliance(player, s);
                                                    OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                    Facepunch.Pool.FreeUnmanaged(ref allClanTags);
                                                }
                                            };

                                            OpenInviteMenu(player, data, userPage);
                                        }
                                        else player.ChatMessage(lang.GetMessage("Notification.Alliance.NoPermissions", Clans, player.UserIDString));
                                    }, $"{player.UserIDString}.invitealliance");
                            });

                        if (canManageAlliance)
                            requestAllianceButton.WithOutline(m_OutlineAlliance);

						BaseContainer.Create(invites, Anchor.FullStretch, new Offset(0f, 0f, 0f, -45f))
							.WithLayoutGroup(m_AllianceInvitesLayout, clan.AllianceInvites.Keys, userPage.Page2, (int i, string t, BaseContainer layout, Anchor anchor, Offset offset) =>
								{
									ImageContainer.Create(layout, anchor, offset)
										.WithStyle(m_PanelStyle)
										.WithChildren(template =>
										{
											TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -20f, 0f))
												.WithText(t)
												.WithAlignment(TextAnchor.MiddleLeft);

											BaseContainer withdrawAllianceButton = ImageContainer.Create(template, Anchor.CenterRight, new Offset(-19f, -9f, -1f, 9f))
												.WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
                                                .WithChildren(revoke =>
												{
													TextContainer.Create(revoke, Anchor.FullStretch, Offset.zero)
                                                        .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
														.WithSize(12)
														.WithText("✘")
														.WithWrapMode(VerticalWrapMode.Overflow);

                                                    ButtonContainer.Create(revoke, Anchor.FullStretch, Offset.zero)
                                                        .WithColor(Color.Clear)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            Clans.WithdrawAlliance(player, t);
                                                            OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                        }, $"{player.UserIDString}.withdrawallianceinvite.{i}");
                                                });

                                            if (canManageAlliance)
                                                withdrawAllianceButton.WithOutline(m_OutlineRed);
                                        });

								});
                    });

                // Alliance Requests
				ImageContainer.Create(alliances, Anchor.CenterRight, new Offset(-260f, -187.5f, 0f, 187.5f))
					.WithStyle(m_PanelStyle)
					.WithChildren(requests =>
					{
						ImageContainer.Create(requests, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                            .WithStyle(m_AllianceHeaderStyle)
							.WithChildren(header =>
							{
								TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
									.WithText(GetString("UI.AllianceRequests", player))
									.WithAlignment(TextAnchor.MiddleLeft);
                                
                                ImageContainer.Create(header, Anchor.CenterRight, new Offset(-40f, -10f, -20f, 10f))
                                    .WithColor(Color.Clear)
                                    .WithChildren(prevpage =>
                                    {
                                        TextContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                            .WithText("◀")
                                            .WithStyle(userPage.Page3 > 0 ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                        if (userPage.Page3 > 0)
                                        {
                                            ButtonContainer.Create(prevpage, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg=>
                                            {
                                                userPage.Page3 -= 1;
                                                OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                            }, $"{player.UserIDString}.alliancerequestpage.back");
                                        }
                                    });

                                ImageContainer.Create(header, Anchor.CenterRight, new Offset(-20f, -10f, 0f, 10f))
                                    .WithColor(Color.Clear)
                                    .WithChildren(nextpage =>
                                    {
                                        bool hasNextPage = m_CurrentAllianceLayout.HasNextPage(userPage.Page3, clan.AllianceInviteCount); 
                                        TextContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                            .WithText("▶")
                                            .WithStyle(hasNextPage ? m_MemberPageStyle : m_MemberPageDisabledStyle);

                                        if (hasNextPage)
                                        {
                                            ButtonContainer.Create(nextpage, Anchor.FullStretch, Offset.zero)
                                                .WithColor(Color.Clear)
                                                .WithCallback(m_CallbackHandler, arg=>
                                                {
                                                    userPage.Page3 += 1;
                                                    OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                },$"{player.UserIDString}.alliancerequestpage.next");
                                        }
                                    });
                            });

						BaseContainer.Create(requests, Anchor.FullStretch, new Offset(0f, 0f, 0f, -20f))
							.WithLayoutGroup(m_AllianceRequestsLayout, clan.IncomingAlliances, userPage.Page3, (int i, string t, BaseContainer layout, Anchor anchor, Offset offset) =>
								{
									ImageContainer.Create(layout, anchor, offset)
										.WithStyle(m_PanelStyle)
										.WithChildren(template =>
										{
											TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -20f, 0f))
												.WithText(t)
												.WithAlignment(TextAnchor.MiddleLeft);

											BaseContainer acceptAllianceButton = ImageContainer.Create(template, Anchor.CenterRight, new Offset(-44f, -9f, -26f, 9f))
                                                .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
                                                .WithChildren(accept =>
												{
													TextContainer.Create(accept, Anchor.FullStretch, Offset.zero)
                                                        .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
                                                        .WithSize(12)
														.WithText("✔")
														.WithWrapMode(VerticalWrapMode.Overflow);

                                                    ButtonContainer.Create(accept, Anchor.FullStretch, Offset.zero)
                                                        .WithColor(Color.Clear)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            Clans.AcceptAlliance(player, t);
                                                            OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                        }, $"{player.UserIDString}.acceptalliance.{i}");
                                                });

                                            if (canManageAlliance)
                                                acceptAllianceButton.WithOutline(m_OutlineGreen);

											BaseContainer rejectAllianceButton = ImageContainer.Create(template, Anchor.CenterRight, new Offset(-19f, -9f, -1f, 9f))
                                                .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
												.WithChildren(reject =>
												{
													TextContainer.Create(reject, Anchor.FullStretch, Offset.zero)
                                                        .WithStyle(canManageAlliance ? m_ButtonStyle : m_ButtonDisabledStyle)
                                                        .WithSize(12)
														.WithText("✘")
														.WithWrapMode(VerticalWrapMode.Overflow);

													ButtonContainer.Create(reject, Anchor.FullStretch, Offset.zero)
														.WithColor(Color.Clear)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            Clans.RejectAlliance(player, t);
                                                            OpenClanMenu(player, clan, Menu.Alliances, userPage);
                                                        }, $"{player.UserIDString}.rejecttalliance.{i}");
                                                });

                                            if (canManageAlliance)
                                                rejectAllianceButton.WithOutline(m_OutlineRed);
                                        });
                                });

					});

			});





        }
        #endregion
                
        #region Invite Menu
        private class InviteData<T>
        {
            public Clans.Clan Clan;
            public Menu Menu;
            public int Page = 0;
            public string SearchString = string.Empty;
            public List<T> Collection;
            public Func<T, string, bool> SearchFunction;
            public Func<T, string> DisplayFunction;
            public Action<T> OnSelectFunction;
        }

        private void OpenInviteMenu<T>(BasePlayer player, InviteData<T> data, UserPage userPage)
        {
            BaseContainer root = ImageContainer.Create(UI_MENU, Layer.Overall, Anchor.Center, new Offset(-200f, -245f, 600f, 245f))
                .WithStyle(m_BackgroundStyle)
                .WithChildren(parent =>
                {
                    CreateTitleBar(player, parent, () => OpenClanMenu(player, data.Clan, data.Menu, userPage));

                    List<T> dst = Facepunch.Pool.Get<List<T>>();
                    if (!string.IsNullOrEmpty(data.SearchString))
                    {
                        for (int i = 0; i < data.Collection.Count; i++)
                        {
                            T t = data.Collection[i];
                            if (data.SearchFunction(t, data.SearchString))
                                dst.Add(t);
                        }
                    }
                    else dst.AddRange(data.Collection);

                    CreateInviteHeader(player, parent, data, dst.Count, userPage);

                    ImageContainer.Create(parent, Anchor.FullStretch, new Offset(5f, 5f, -5f, -75f))
                        .WithStyle(m_PanelStyle)
                        .WithLayoutGroup(m_InviteMenuLayout, dst, data.Page, (int i, T t, BaseContainer layout, Anchor anchor, Offset offset) =>
                        {
                            ImageContainer.Create(layout, anchor, offset)
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(button =>
                                {
                                    TextContainer.Create(button, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                        .WithSize(12)
                                        .WithText(data.DisplayFunction(t))
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => data.OnSelectFunction(t), $"{player.UserIDString}.invitemenu.select.{i}");
                                });
                        });

                });
            
            ChaosUI.Destroy(player, UI_MENU);
            ChaosUI.Show(player, root);
        }

        private void CreateInviteHeader<T>(BasePlayer player, BaseContainer parent, InviteData<T> data, int listCount, UserPage userPage)
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -70f, -5f, -40f))
			.WithStyle(m_PanelStyle)
			.WithChildren(header =>
            {
                bool hasPreviousPage = data.Page > 0;
                bool hasNextPage = m_InviteLayout.HasNextPage(data.Page, listCount);
                
				ImageContainer.Create(header, Anchor.CenterLeft, new Offset(5f, -10f, 35f, 10f))
					.WithStyle(hasPreviousPage ? m_ButtonStyle : m_ButtonDisabledStyle)
					.WithChildren(backButton =>
					{
						TextContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
							.WithText("<<<")
							.WithStyle(hasPreviousPage ? m_ButtonStyle : m_ButtonDisabledStyle);

                        if (hasPreviousPage)
                        {
                            ButtonContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    data.Page -= 1;
                                    OpenInviteMenu(player, data, userPage);
                                }, $"{player.UserIDString}.invitemenu.back");
                        }
                    });

				ImageContainer.Create(header, Anchor.CenterRight, new Offset(-35f, -10f, -5f, 10f))
					.WithStyle(hasNextPage ? m_ButtonStyle : m_ButtonDisabledStyle)
					.WithChildren(nextButton =>
					{
						TextContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
							.WithText(">>>")
                            .WithStyle(hasNextPage ? m_ButtonStyle : m_ButtonDisabledStyle);

                        if (hasNextPage)
                        {
                            ButtonContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    data.Page += 1;
                                    OpenInviteMenu(player, data, userPage);
                                }, $"{player.UserIDString}.invitemenu.next");
                        }
                    });

				TextContainer.Create(header, Anchor.Center, new Offset(-200f, -12.5f, 200f, 12.5f))
					.WithText(GetString(data.Menu == Menu.Members ? "UI.MemberInvite" : "UI.AllianceInvite", player))
					.WithAlignment(TextAnchor.MiddleCenter);

				ImageContainer.Create(header, Anchor.CenterRight, new Offset(-240f, -10f, -40f, 10f))
					.WithStyle(m_ButtonStyle)
					.WithChildren(searchInput =>
                    {
                        InputFieldContainer.Create(searchInput, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                            .WithText(data.SearchString)
                            .WithAlignment(TextAnchor.MiddleLeft)
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                data.Page = 0;
                                data.SearchString = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                OpenInviteMenu<T>(player, data, userPage);
                            }, $"{player.UserIDString}.invitemenu.search");
                    });

                if (!string.IsNullOrEmpty(m_MagnifyImage))
                {
                    RawImageContainer.Create(header, Anchor.CenterRight, new Offset(-260f, -10f, -240f, 10f))
                        .WithPNG(m_MagnifyImage);
                }
            });
        }
        #endregion
     
        #region Commands
        private void CommandClanUI(BasePlayer player, string command, string[] args)
        {
            Clans.Clan clan = Clans.Instance.storedData.FindClanByID(player.userID);
            if (clan == null)
            {
                BaseContainer root = BaseContainer.Create(UI_MOUSE, Layer.Hud, Anchor.Center, Offset.Default)
                    .NeedsCursor()
                    .NeedsKeyboard();
			    
                ChaosUI.Show(player, root);
                
                CreateClanMenu(player, "");
                //player.LocalizedMessage(this, "Notification.NotInClan");
            }
            else
            {
                BaseContainer root = BaseContainer.Create(UI_MOUSE, Layer.Hud, Anchor.Center, Offset.Default)
                    .NeedsCursor()
                    .NeedsKeyboard();
			    
                ChaosUI.Show(player, root);
                
                OpenClanMenu(player, clan);
            }
        }
        #endregion       

        #region Config        
        private ConfigData Configuration =>  ConfigurationData as ConfigData;

        private class ConfigData : BaseConfigData
        {
            [JsonProperty(PropertyName = "Menu chat command")]
            public string Command { get; set; }

            [JsonProperty(PropertyName = "Clan member commands")]
            public List<ClanCommand> MemberCommands { get; set; }

            [JsonProperty(PropertyName = "UI Colors")]
            public UIColors Colors { get; set; }

            public class ClanCommand
            {
                public string Name { get; set; }

                public string Command { get; set; }
            }

            public class UIColors
            {                
                public Color Background { get; set; }

                public Color Panel { get; set; }
                
                public Color MembersHeader { get; set; }
                
                public Color AllianceHeader { get; set; }

                public Color Button { get; set; }

                public Color Highlight1 { get; set; }

                public Color Highlight3 { get; set; }

                public Color Close { get; set; }
                
                public class Color
                {
                    public string Hex { get; set; }

                    public float Alpha { get; set; }
                }
            }
        }

        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();

            if (oldVersion < new VersionNumber(3, 0, 0))
            {
                (ConfigurationData as ConfigData).Colors = baseConfigData.Colors;
            }
        }
        
        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Command = "cmenu",
                MemberCommands = new List<ConfigData.ClanCommand>
                {
                    new ConfigData.ClanCommand
                    {
                        Command = "/tpr {playerName}",
                        Name = "TPR"
                    },
                    new ConfigData.ClanCommand
                    {
                        Command = "/trade {playerId}",
                        Name = "TRADE"
                    }
                },
                Colors = new ConfigData.UIColors
                {
                    Background = new ConfigData.UIColors.Color
                    {
                        Hex = "151515",
                        Alpha = 0.94f
                    },
                    Panel = new ConfigData.UIColors.Color
                    {
                        Hex = "FFFFFF",
                        Alpha = 0.165f
                    },
                    MembersHeader = new ConfigData.UIColors.Color
                    {
                        Hex = "C4FF00",
                        Alpha = 0.314f
                    },
                    AllianceHeader = new ConfigData.UIColors.Color
                    {
                        Hex = "FFEC00",
                        Alpha = 0.314f
                    },
                    Button = new ConfigData.UIColors.Color
                    {
                        Hex = "2A2E32",
                        Alpha = 1f
                    },
                    Highlight1 = new ConfigData.UIColors.Color
                    {
                        Hex = "C4FF00",
                        Alpha = 1f
                    },
                    Highlight3 = new ConfigData.UIColors.Color
                    {
                        Hex = "CE422B",
                        Alpha = 1f
                    },
                    Close = new ConfigData.UIColors.Color
                    {
                        Hex = "CE422B",
                        Alpha = 1f
                    }
                }
            } as T;
        }
        #endregion
    }
}
