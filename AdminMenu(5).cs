using HarmonyLib;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Reflection;
using Network;
using Newtonsoft.Json;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using UnityEngine.UI;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using System.IO;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Libraries;
using System.Xml;
using System.Text;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("AdminMenu", "0xF // dsc.gg/0xf-plugins", "1.1.11")]
    [Description("Multifunctional in-game admin menu.")]
    public class AdminMenu : RustPlugin
    {
        [PluginReference]
        private Plugin ImageLibrary, Economics, ServerRewards, Clans;
        private const string PERMISSION_USE = "adminmenu.use";
        private const string PERMISSION_FULLACCESS = "adminmenu.fullaccess";
        private const string PERMISSION_CONVARS = "adminmenu.convars";
        private const string PERMISSION_PERMISSIONMANAGER = "adminmenu.permissionmanager";
        private const string PERMISSION_PLUGINMANAGER = "adminmenu.pluginmanager";
        private const string PERMISSION_GIVE = "adminmenu.give";
        private const string PERMISSION_USERINFO_IP = "adminmenu.userinfo.ip";
        private const string PERMISSION_USERINFO_STEAMINFO = "adminmenu.userinfo.steaminfo";
        private const string ADMINMENU_IMAGEBASE64 = "iVBORw0KGgoAAAANSUhEUgAAAWcAAAD3CAYAAADBqZV6AAAACXBIWXMAAAsTAAALEwEAmpwYAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAYeSURBVHgB7d3hbdtWGIbRj0EG8AZxJ4g3qLJBN4g7QZMJ6kyQdgNng2wQeQNtUI3gTMBeoilQFKKc2CT1mjoHIAj4/mDuFfWEoAmzCgAAAAAAAAAAAAAAAAAAAAAAAIDz1fV9f1Mcsu26bntooK3Zpu02tQ67Ns/PtSDn3JPs2+d1e2igretF272r6Y2eI+2YV233S63D5zbP3aGBGec5esyXbfu9GLMd+fmmVrRu7cS7bifIp1qOc+7x7tp2OzI2xHmOtb1t29h/4Fe1ns9z37bdyNhc89yPHfNFQfvytUC/LSCGOPMvgYYg4sx/CTSEEGf+T6AhgDhziEDDiYkzYwQaTkicOUag4UTEmYcINJyAOPM9BBoWJs58L4GGBYkzP0KgYSHizI8SaFiAOPMYAg0zE2ceS6BhRuLMUwg0zESceSqBhhmIM1MQaJiYODMVgYYJiTNTEmiYiDgzNYGGCbysaQ0vKnxfyxpevPix1mFYu11N77ptSwbztm1LvjB28NC596XWccxj7tv2pqa3r+X9WeMvld3UGbwkeOo4f+26blsLaldptSK7mdZv+22d1nxFe/Tcm+k8OcUxR7V/yxDnba3D6HehretlnQG3Nc5EO9Gva/mrWeCRxPmMCDQ8H+J8ZgQangdxPkMCDfnE+UwJNGSb+mkNnpEh0O0336/rn8cReebaZ3nRdu9qesOTE5+LRYkz98VaDHGe4/nf2xp/5piZuK0BEEicAQKJM0AgcQYIJM4AgcQZIJA4AwQSZ4BA4gwQSJwBAokzQCBxBggkzgCBxBkgkDgDBBJngEDiDBBInAECiTNAIHEGCDT1C15f933/pZZ1UQArM3Wch1BuCoAncVsDIJA4AwQSZ4BA4gwQSJwBAk39tAak+lDT2xfMRJw5C13X3RQ8I25rAAQSZ4BA4gwQSJwBAokzQCBxBggkzgCBxBkgkDgDBBJngEDiDBBInAECiTNAoKnjfNctrB3zTQGsjCtngEDiDBBInAECiTNAIHEGCCTOAIG84DXLx77v72tZVwUMfmvfv7cjY5e1MHHOIpRwOlHfP7c1AAKJM0AgcQYIJM4AgcQZIJA4AwQSZ4BA4gwQSJwBAokzQCBxBggkzgCBxBkgkDgDBBJngEDiDBBInAECiTNAIHEGCCTOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwpuv7/qb4Ufuu624PDbT1vGy76+KY27Z++0MDbf2u2+6yVqDN8WZsbK55PnDMd213UdO6b8f8oxbU5jHM4V1Nb9fm8rlCDHHuix911z7EzaGBtpzDz78Ux7xp67c9NNDWb1i7Ta1Am2M3NjbXPB845r7tXtW0hguVn2pB3y6A/qrpDRcNv1aIFwVAHHEGCCTOAIHEGSCQOAMEEmeAQOIMEEicAQKJM0AgcQYIJM4AgcQZIJA4AwQSZ4BA4gwQSJwBAokzQCBxBggkzgCBxBkg0Mua1q5t74+Mz/Hi01Mc8xQ+te221mFXy3tzZOxj264Kgkwd569jb1UezPSi71Mc8xT2x+bJcQ+cI/cFYdzWAAgkzgCBxBkgkDgDBBJngEDiDBBInAECiTNAIHEGCCTOAIHEGSCQOAMEmvoPHzGfV33fb2oddl3X+WNDcIQ4Px/X37Y1GP5857aAUW5rAAQSZ4BA4gwQSJwBAokzQCBxBggkzgCBxBkgkDgDBBJngEDiDBBInAECiTNAIHEGCCTOAIHEGSCQOAMEEmeAQOIMEEicAQIt/YLXDzW9fZ2Hbdvuah32BRy1aJy7rrspHuvO+sH5cFsDIJA4AwQSZ4BA4gwQSJwBAokzQCBxBggkzgCBxBkgkDgDBBJngEDiDBBInAECiTNAIHEGCCTOAIHEGSCQOAMEEmeAQOIMEGjpt2/zeG/7vv+51uF913W7Wrn2eX05MnxV63DxwDxnOWadAXF+Pi6/bWtwFl+uZlPrN3yWm2JybmsABBJngEDiDBBInAECiTNAIHEGCCTOAIHEGSCQOAMEEmeAQOIMEEicAQKJM0AgcQYIJM4AgcQZIJA4AwQSZ4BA4gwQSJwBAv0Nie8vXooXAzkAAAAOZVhJZk1NACoAAAAIAAAAAAAAANJTkwAAAABJRU5ErkJggg==";
        private static Dictionary<string, string> HEADERS = new Dictionary<string, string>
        {
            {
                "Content-Type",
                "application/json"
            }
        };
#if !CARBON
        private static FieldInfo PERMISSIONS_DICTIONARY_FIELD = typeof(Oxide.Core.Libraries.Permission).GetField("registeredPermissions", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo CONSOLECOMMANDS_DICTIONARY_FIELD = typeof(Oxide.Game.Rust.Libraries.Command).GetField("consoleCommands", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo CONSOLECOMMAND_CALLBACK_FIELD = CONSOLECOMMANDS_DICTIONARY_FIELD.FieldType.GetGenericArguments()[1].GetField("Callback", BindingFlags.Public | BindingFlags.Instance);
        private static FieldInfo PLUGINCALLBACK_PLUGIN_FIELD = CONSOLECOMMAND_CALLBACK_FIELD.FieldType.GetField("Plugin", BindingFlags.Public | BindingFlags.Instance);
        private static FieldInfo CHATCOMMANDS_DICTIONARY_FIELD = typeof(Oxide.Game.Rust.Libraries.Command).GetField("chatCommands", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo CHATCOMMAND_PLUGIN_FIELD = CHATCOMMANDS_DICTIONARY_FIELD.FieldType.GetGenericArguments()[1].GetField("Plugin", BindingFlags.Public | BindingFlags.Instance);
#endif
        private static AdminMenu Instance;
        private static Dictionary<string, Panel> panelList;
        private static Dictionary<ulong, SteamInfo> cachedSteamInfo = new Dictionary<ulong, SteamInfo>();
        private static string ADMINMENU_IMAGECRC;
        private MainMenu mainMenu;
        private Dictionary<string, string> defaultLang = new Dictionary<string, string>();
        private Dictionary<PlayerLoot, Item> viewingBackpacks = new Dictionary<PlayerLoot, Item>();
        static Configuration config;
        public AdminMenu()
        {
            Instance = this;
        }

        public class ButtonArray<T> : List<T> where T : Button
        {
            public ButtonArray() : base()
            {
            }

            public ButtonArray(IEnumerable<T> collection) : base(collection)
            {
            }

            public IEnumerable<T> GetAllowedButtons(Connection connection)
            {
                return GetAllowedButtons(connection.userid.ToString());
            }

            public IEnumerable<T> GetAllowedButtons(string userId)
            {
                return this.Where(b => b == null || b.UserHasPermission(userId));
            }
        }

        public class ButtonGrid<T> : List<ButtonGrid<T>.Item> where T : Button
        {
            public class Item
            {
                public int row;
                public int column;
                public T button;
                public Item(int row, int column, T button)
                {
                    this.row = row;
                    this.column = column;
                    this.button = button;
                }
            }

            public IEnumerable<Item> GetAllowedButtons(Connection connection)
            {
                return GetAllowedButtons(connection.userid.ToString());
            }

            public IEnumerable<Item> GetAllowedButtons(string userId)
            {
                return this.Where(b => b.button == null || b.button.UserHasPermission(userId));
            }
        }

        public class ButtonArray : ButtonArray<Button>
        {
        }

        public class Button
        {
            public enum State
            {
                None,
                Normal,
                Pressed,
                Toggled
            }

            private string label = null;
            private string permission = null;
            public string Command { get; set; }
            public string[] Args { get; set; }
            public Label Label { get; set; }
            public virtual int FontSize { get; set; } = 14;
            public ButtonStyle Style { get; set; } = ButtonStyle.Default;

            public string Permission
            {
                get
                {
                    return permission;
                }

                set
                {
                    if (string.IsNullOrEmpty(value))
                        return;
                    permission = string.Format("adminmenu.{0}", value);
                    if (!Instance.permission.PermissionExists(permission))
                        Instance.permission.RegisterPermission(permission, Instance);
                }
            }

            public string FullCommand
            {
                get
                {
                    return $"{Command} {string.Join(" ", Args)}";
                }
            }

            public Button()
            {
            }

            public Button(string label, string command, params string[] args)
            {
                Label = new Label(label);
                Command = command;
                Args = args;
                if (!all.ContainsKey(FullCommand))
                    all.Add(FullCommand, this);
            }

            public virtual Button.State GetState(ConnectionData connectionData)
            {
                if (connectionData.userData.TryGetValue($"button_{this.GetHashCode()}.state", out object state))
                    return (Button.State)state;
                return Button.State.None;
            }

            public virtual void SetState(ConnectionData connectionData, Button.State state)
            {
                connectionData.userData[$"button_{this.GetHashCode()}.state"] = state;
            }

            public bool UserHasPermission(Connection connection)
            {
                return UserHasPermission(connection.userid.ToString());
            }

            public bool UserHasPermission(string userId)
            {
                return Permission == null || Instance.UserHasPermission(userId, Permission);
            }

            public virtual bool IsPressed(ConnectionData connectionData)
            {
                return GetState(connectionData) == State.Pressed;
            }

            public virtual bool IsHidden(ConnectionData connectionData)
            {
                return false;
            }

            internal static Dictionary<string, Button> all = new Dictionary<string, Button>();
        }

        public class ButtonStyle : ICloneable
        {
            public string BackgroundColor { get; set; }
            public string ActiveBackgroundColor { get; set; }
            public string TextColor { get; set; }

            public object Clone()
            {
                return this.MemberwiseClone();
            }

            public static ButtonStyle Default => new ButtonStyle
            {
                BackgroundColor = "0.3 0.3 0.3 0.6",
                ActiveBackgroundColor = "0.2 0.4 0.2 0.6",
                TextColor = "1 1 1 1",
            };
        }

        public class CategoryButton : Button
        {
            public override int FontSize { get; set; } = 22;

            public CategoryButton(string label, string command, params string[] args) : base(label, command, args)
            {
            }
        }

        public class HideButton : Button
        {
            public HideButton(string label, string command, params string[] args) : base(label, command, args)
            {
            }

            public override bool IsHidden(ConnectionData connectionData)
            {
                return connectionData.userData["backcommand"] == null;
            }
        }

        public class ToggleButton : Button
        {
            public ToggleButton(string label, string command, params string[] args) : base(label, command, args)
            {
            }

            public virtual void Toggle(ConnectionData connectionData)
            {
                Button.State currentState = GetState(connectionData);
                SetState(connectionData, (currentState == State.Normal || currentState == State.None ? Button.State.Toggled : Button.State.Normal));
            }
        }

        public class ConditionToggleButton : ToggleButton
        {
            public Func<ConnectionData, bool> Condition { get; set; }

            public ConditionToggleButton(string label, string command, params string[] args) : base(label, command, args)
            {
            }

            public override State GetState(ConnectionData connectionData)
            {
                if (Condition == null)
                    return State.Normal;
                return Condition(connectionData) ? State.Toggled : State.Normal;
            }

            public override void Toggle(ConnectionData connectionData)
            {
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class BaseCustomButton
        {
            [JsonProperty]
            public string Label { get; set; } = string.Empty;

            [JsonProperty("Execution as server")]
            public bool ExecutionAsServer { get; set; }

            [JsonProperty("Commands")]
            public string[] Commands { get; set; } = new string[0];

            [JsonProperty("Commands for Toggled state")]
            public string[] ToggledStateCommands { get; set; } = new string[0];

            [JsonProperty]
            public string Permission { get; set; } = string.Empty;

            [JsonProperty]
            public ButtonStyle Style { get; set; } = ButtonStyle.Default;

            [JsonProperty]
            public int[] Position { get; set; } = new int[2]
            {
                0,
                0
            };
            protected virtual string BaseCommand => "custombutton";

            private Button _button;
            public Button Button
            {
                get
                {
                    if (_button == null)
                        _button = GetButton();
                    return _button;
                }
            }

            private Button GetButton()
            {
                if (ToggledStateCommands != null && ToggledStateCommands.Length > 0)
                    return new ToggleButton(Label, BaseCommand, "cb.exec", this.GetHashCode().ToString())
                    {
                        Permission = Permission.ToLower(),
                        Style = Style
                    };
                return new Button(Label, BaseCommand, "cb.exec", this.GetHashCode().ToString())
                {
                    Permission = Permission.ToLower(),
                    Style = Style
                };
            }
        }

        public class QMCustomButton : BaseCustomButton
        {
            public enum Recievers
            {
                None,
                Online,
                Offline,
                Everyone
            }

            protected override string BaseCommand => "quickmenu.action";

            [JsonProperty("Bulk sending of command to each player. Available values: None, Online, Offline, Everyone")]
            [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public Recievers PlayerReceivers { get; set; }
        }

        public class UserInfoCustomButton : BaseCustomButton
        {
            protected override string BaseCommand => "userinfo.action";
        }

        public class Configuration
        {
            [JsonProperty(PropertyName = "Text under the ADMIN MENU")]
            public string Subtext { get; set; } = "BY 0XF";

            [JsonProperty(PropertyName = "Button to hook (X | F | OFF)")]
            [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public ButtonHook ButtonToHook { get; set; } = ButtonHook.X;

            [JsonProperty(PropertyName = "Chat command to show admin menu")]
            public string ChatCommand { get; set; } = "admin";

            [JsonProperty(PropertyName = "Theme")]
            [JsonConverter(typeof(ThemeConverter))]
            public Theme Theme { get => Themes.CurrentTheme; set => Themes.CurrentTheme = value; }

            [JsonProperty(PropertyName = "Custom Quick Buttons")]
            public List<QMCustomButton> CustomQuickButtons { get; set; } = new List<QMCustomButton>();

            [JsonProperty(PropertyName = "User Custom Buttons")]
            public List<UserInfoCustomButton> UserInfoCustomButtons { get; set; } = new List<UserInfoCustomButton>();

            [JsonProperty(PropertyName = "Give menu item presets (add your custom items for easy give)")]
            public List<ItemPreset> GiveItemPresets { get; set; } = new List<ItemPreset>();

            [JsonProperty(PropertyName = "Favorite Plugins")]
            public HashSet<string> FavoritePlugins { get; set; } = new HashSet<string>();

            [JsonProperty(PropertyName = "Logs Properties")]
            public LogsProperties Logs { get; set; } = new LogsProperties();

            [JsonIgnore]
            public Dictionary<int, string[]> HashedCommands { get; set; } = new Dictionary<int, string[]>();

            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    CustomQuickButtons = new List<QMCustomButton>
                    {
                        new QMCustomButton
                        {
                            Label = "Custom Button",
                            Commands = new[]
                            {
                                "chat.say \"/custom\"",
                                "adminmenu openinfopanel custom_buttons"
                            },
                            Permission = "fullaccess",
                            Style = new ButtonStyle()
                            {
                                BackgroundColor = "1 0.2 0.2 0.6",
                                TextColor = "0.95 0.6 0 1",
                            },
                            Position = new int[2]
                            {
                                0,
                                4
                            }
                        }
                    },
                    UserInfoCustomButtons = new List<UserInfoCustomButton>
                    {
                        new UserInfoCustomButton
                        {
                            Label = "Custom Button",
                            Commands = new[]
                            {
                                "chat.say \"/custom {steamID}\"",
                                "adminmenu openinfopanel custom_buttons"
                            },
                            Permission = "fullaccess",
                            Position = new int[2]
                            {
                                9,
                                0
                            }
                        }
                    },
                    GiveItemPresets = new List<ItemPreset>()
                    {
                        ItemPreset.Example
                    }
                };
            }

            public class ItemPreset
            {
                [JsonProperty(PropertyName = "Short Name")]
                public string ShortName { get; set; }

                [JsonProperty(PropertyName = "Skin Id")]
                public ulong SkinId { get; set; }

                [JsonProperty(PropertyName = "Name")]
                public string Name { get; set; }

                [JsonProperty(PropertyName = "Category Name")]
                public string Category { get; set; }

                [JsonIgnore]
                public static ItemPreset Example
                {
                    get
                    {
                        return new ItemPreset
                        {
                            ShortName = "chocolate",
                            SkinId = 3161523786,
                            Name = "Delicious cookies with chocolate",
                            Category = "Food",
                        };
                    }
                }
            }

            public class LogsProperties
            {
                [JsonProperty(PropertyName = "Discord Webhook URL for logs (https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks)")]
                public string WebhookURL { get; set; } = string.Empty;

                [JsonProperty(PropertyName = "Give")]
                public bool Give { get; set; } = true;

                [JsonProperty(PropertyName = "Admin teleports")]
                public bool AdminTeleport { get; set; } = true;

                [JsonProperty(PropertyName = "Spectate")]
                public bool Spectate { get; set; } = true;

                [JsonProperty(PropertyName = "Heal")]
                public bool Heal { get; set; } = true;

                [JsonProperty(PropertyName = "Kill")]
                public bool Kill { get; set; } = true;

                [JsonProperty(PropertyName = "Look inventory")]
                public bool LookInventory { get; set; } = true;

                [JsonProperty(PropertyName = "Strip inventory")]
                public bool StripInventory { get; set; } = true;

                [JsonProperty(PropertyName = "Blueprints")]
                public bool Blueprints { get; set; } = true;

                [JsonProperty(PropertyName = "Mute/Unmute")]
                public bool MuteUnmute { get; set; } = true;

                [JsonProperty(PropertyName = "Toggle Creative")]
                public bool ToggleCreative { get; set; } = true;

                [JsonProperty(PropertyName = "Cuff")]
                public bool Cuff { get; set; } = true;

                [JsonProperty(PropertyName = "Kick the player")]
                public bool Kick { get; set; } = true;

                [JsonProperty(PropertyName = "Ban the player")]
                public bool Ban { get; set; } = true;

                [JsonProperty(PropertyName = "Using custom buttons")]
                public bool CustomButtons { get; set; } = true;

                [JsonProperty(PropertyName = "Spawn entities")]
                public bool SpawnEntities { get; set; } = true;

                [JsonProperty(PropertyName = "Set time")]
                public bool SetTime { get; set; } = true;

                [JsonProperty(PropertyName = "ConVars")]
                public bool ConVars { get; set; } = true;

                [JsonProperty(PropertyName = "Plugin Manager")]
                public bool PluginManager { get; set; } = true;
            }
        }

        public class ConnectionData
        {
            public Connection connection;
            public MainMenu currentMainMenu;
            public Panel currentPanel;
            public Sidebar currentSidebar;
            public Content currentContent;
            public Translator15 translator;
            public Dictionary<string, object> userData;
            public ConnectionData(BasePlayer player) : this(player.Connection)
            {
            }

            public ConnectionData(Connection connection)
            {
                this.connection = connection;
                this.translator = new Translator15(connection.userid);
                this.translator.Get("test");
                this.userData = new Dictionary<string, object>()
                {
                    {
                        "userId",
                        connection.userid
                    },
                    {
                        "userinfo.lastuserid",
                        connection.userid
                    },
                    {
                        "backcommand",
                        null
                    },
                };
                this.UI = new ConnectionUI(this);
                try
                {
                    Init();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }

            public ConnectionUI UI { get; private set; }
            public bool IsAdminMenuDisplay { get; set; }
            public bool IsDestroyed { get; set; }

            public void Init()
            {
                UI.RenderMainMenu(Instance.mainMenu);
                this.currentMainMenu = Instance.mainMenu;
            }

            public void ShowAdminMenu()
            {
                UI.ShowAdminMenu();
                IsAdminMenuDisplay = true;
            }

            public void HideAdminMenu()
            {
                UI.HideAdminMenu();
                IsAdminMenuDisplay = false;
            }

            public ConnectionData OpenPanel(string panelName)
            {
                if (panelList.TryGetValue(panelName, out Panel panel))
                {
                    if (currentPanel == panel)
                        return null;
                    if (currentContent != null)
                        currentContent.RestoreUserData(userData);
                    currentContent = null;
                    if (currentPanel != null)
                        currentPanel.OnClose(this);
                    currentPanel = panel;
                    currentSidebar = currentPanel.Sidebar;
                    UI.RenderPanel(currentPanel);
                    currentPanel.OnOpen(this);
                    Content defaultPanelContent = panel.DefaultContent;
                    if (defaultPanelContent != null)
                        ShowPanelContent(defaultPanelContent);
                    if (panel.Sidebar != null && panel.Sidebar.AutoActivateCategoryButtonIndex.HasValue)
                        Instance.HandleCommand(connection, "uipanel.sidebar.button_pressed", panel.Sidebar.AutoActivateCategoryButtonIndex.Value.ToString(), panel.Sidebar.CategoryButtons.GetAllowedButtons(connection).Count().ToString());
                    return this;
                }
                else
                {
                    Instance.PrintError($"Panel with name \"{panelName}\" not founded!");
                    return null;
                }
            }

            public void SetSidebar(Sidebar sidebar)
            {
                bool needsChangeContentSize = (currentSidebar != sidebar);
                currentSidebar = sidebar;
                CUI.Root root = new CUI.Root("AdminMenu_Panel");
                if (sidebar != null)
                    UI.AddSidebar(root, sidebar);
                else
                    root.Add(new CUI.Element { DestroyUi = "AdminMenu_Panel_Sidebar" });
                root.Render(connection);
                if (needsChangeContentSize)
                {
                    CUI.Root updateRoot = new CUI.Root();
                    updateRoot.Add(new CUI.Element { Components = { new CuiRectTransformComponent { OffsetMin = $"{(sidebar != null ? 250 : 0)} 0", } }, Name = "AdminMenu_Panel_Content" });
                    updateRoot.Update(connection);
                }
            }

            public void ShowPanelContent(Content content)
            {
                if (content == null)
                {
                    CUI.Root root = new CUI.Root();
                    root.Add(new CUI.Element { DestroyUi = "AdminMenu_Panel_TempContent" });
                    root.Render(connection);
                    return;
                }

                if (currentContent != null)
                    currentContent.RestoreUserData(userData);
                currentContent = content;
                currentContent.LoadDefaultUserData(userData);
                UI.RenderContent(content);
            }

            public void ShowPanelContent(string contentId)
            {
                ShowPanelContent(currentPanel.TryGetContent(contentId));
            }

            public void Dispose()
            {
                all.Remove(connection);
            }

            public static Dictionary<Connection, ConnectionData> all = new Dictionary<Connection, ConnectionData>();
            public static ConnectionData Get(Connection connection)
            {
                if (connection == null)
                    return null;
                ConnectionData data;
                if (all.TryGetValue(connection, out data))
                    return data;
                return null;
            }

            public static ConnectionData Get(BasePlayer player)
            {
                return Get(player.Connection);
            }

            public static ConnectionData GetOrCreate(Connection connection)
            {
                if (connection == null)
                    return null;
                ConnectionData data = Get(connection);
                if (data == null)
                    data = all[connection] = new ConnectionData(connection);
                return data;
            }

            public static ConnectionData GetOrCreate(BasePlayer player)
            {
                return GetOrCreate(player.Connection);
            }
        }

        public class ConnectionUI
        {
            Connection connection;
            ConnectionData connectionData;
            public ConnectionUI(ConnectionData connectionData)
            {
                this.connectionData = connectionData;
                this.connection = connectionData.connection;
            }

            public void ShowAdminMenu()
            {
                CUI.Root root = new CUI.Root();
                root.Add(new CUI.Element { Components = { new CuiRectTransformComponent { AnchorMin = "0 0.00001", AnchorMax = "1 1.00001" } }, Name = "AdminMenu", Update = true });
                root.Add(new CUI.Element { Components = { new CuiNeedsCursorComponent() }, Parent = "AdminMenu", Name = "AdminMenu_Cursor" });
                root.Render(connection);
            }

            public void HideAdminMenu()
            {
                CUI.Root root = new CUI.Root();
                root.Add(new CUI.Element { Components = { new CuiRectTransformComponent { AnchorMin = "1000 1000", AnchorMax = "1001 1001" } }, DestroyUi = "AdminMenu_Cursor", Name = "AdminMenu", Update = true });
                root.Render(connection);
            }

            public void DestroyAdminMenu()
            {
                CuiHelper.DestroyUi(connection.player as BasePlayer, "AdminMenu");
                CuiHelper.DestroyUi(connection.player as BasePlayer, "AdminMenu_Cursor");
                connectionData.IsDestroyed = true;
            }

            public void DestroyAll()
            {
                DestroyAdminMenu();
                CuiHelper.DestroyUi(connection.player as BasePlayer, "AdminMenu_OpenButton");
            }

            public void AddSidebar(CUI.Element element, Sidebar sidebar)
            {
                if (sidebar == null)
                    return;
                var sidebarPanel = element.AddPanel(color: Themes.CurrentTheme.GetColorString(Theme.KeyCollection.PANEL_SIDEBAR_BACKGROUND), material: "assets/content/ui/uibackgroundblur-ingamemenu.mat", imageType: Image.Type.Tiled, anchorMin: "0 0", anchorMax: "0 1", offsetMin: "0 0", offsetMax: "250 0", name: "AdminMenu_Panel_Sidebar").AddDestroySelfAttribute();
                sidebarPanel.AddPanel(color: "0.217 0.217 0.217 0.796", sprite: "assets/content/ui/ui.background.transparent.linear.psd", material: "assets/content/ui/namefontmaterial.mat", anchorMin: "0 0", anchorMax: "1 1", name: "UIPanel_SideBar_Linear");
                IEnumerable<CategoryButton> categoryButtons = sidebar.CategoryButtons.GetAllowedButtons(connection);
                if (categoryButtons != null)
                {
                    int categoryButtonsCount = categoryButtons.Count();
                    if (categoryButtonsCount == 0)
                        return;
                    var sidebarButtonGroup = sidebarPanel.AddPanel(color: "0 0 0 0", name: "UIPanel_SideBar_Scrollview");
                    sidebarButtonGroup.Components.AddScrollView(vertical: true, anchorMin: "0 1", offsetMin: $"0 -{categoryButtonsCount * 48}");
                    for (int i = 0; i < categoryButtonsCount; i++)
                    {
                        CategoryButton categoryButton = categoryButtons.ElementAt(i);
                        sidebarButtonGroup.AddButton(command: $"adminmenu uipanel.sidebar.button_pressed {i} {categoryButtonsCount}", color: $"0 0 0 0", anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"16 -{(i + 1) * 48}", offsetMax: $"0 -{i * 48}", name: $"UIPanel_SideBar_Button{i}").AddText(text: categoryButton.Label.Localize(connection), color: "0.969 0.922 0.882 1", font: CUI.Font.RobotoCondensedBold, fontSize: categoryButton.FontSize, align: TextAnchor.MiddleRight, offsetMin: "16 0", offsetMax: "-16 0");
                    }
                }
            }

            private void AddNavButtons(CUI.Element element, MainMenu mainMenu)
            {
                IEnumerable<Button> navButtons = mainMenu.NavButtons.GetAllowedButtons(connection);
                int navButtonsCount = navButtons.Count();
                var navButtonGroup = element.AddContainer(anchorMin: "0 0", anchorMax: "1 0", offsetMin: "64 64", offsetMax: $"0 {64 + navButtonsCount * 42}", name: "Navigation ButtonGroup").AddDestroySelfAttribute();
                for (int i = 0; i < navButtonsCount; i++)
                {
                    Button navButton = navButtons.ElementAtOrDefault(i);
                    if (navButton == null)
                        continue;
                    navButtonGroup.AddButton(command: navButton.IsHidden(connectionData) ? null : $"adminmenu navigation.button_pressed {i} {navButtonsCount}", color: "0 0 0 0", anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"0 -{(i + 1) * 42}", offsetMax: $"0 -{i * 42}").AddText(text: navButton.Label.Localize(connection).ToUpper(), color: $"0.969 0.922 0.882 {(navButton.IsHidden(connectionData) ? 0 : (navButton.IsPressed(connectionData) ? 1 : 0.180f))}", fontSize: 28, font: CUI.Font.RobotoCondensedBold, align: TextAnchor.LowerLeft, overflow: VerticalWrapMode.Truncate, offsetMin: "10 5", name: $"NavigationButtonText{i}");
                }
            }

            public void UpdateNavButtons(MainMenu mainMenu)
            {
                CUI.Root root = new CUI.Root("AdminMenu_Navigation");
                AddNavButtons(root, mainMenu);
                root.Render(connection);
            }

            public void RenderOverlayOpenButton()
            {
                CUI.Root root = new CUI.Root("Overlay");
                root.AddButton(command: "adminmenu", color: "0.969 0.922 0.882 0.035", material: "assets/icons/greyout.mat", anchorMin: "0 0", anchorMax: "0 0", offsetMin: "0 0", offsetMax: "100 30", name: "AdminMenu_OpenButton").AddDestroySelfAttribute().AddText(text: "ADMIN MENU", color: "0.969 0.922 0.882 0.45", font: CUI.Font.RobotoCondensedBold, fontSize: 14, align: TextAnchor.MiddleCenter);
                root.Render(connection);
            }

            public void RenderMainMenu(MainMenu mainMenu)
            {
                if (mainMenu == null)
                    return;
                CUI.Root root = new CUI.Root("Overall");
                var container = root.AddPanel(color: "0.169 0.162 0.143 1", material: "assets/content/ui/uibackgroundblur-mainmenu.mat", imageType: Image.Type.Tiled, anchorMin: "1000 1000", anchorMax: "1001 1001", name: "AdminMenu").AddDestroySelfAttribute();
                container.AddPanel(color: "0.301 0.283 0.235 1", sprite: "assets/content/ui/ui.background.transparent.radial.psd", material: "assets/content/ui/namefontmaterial.mat", anchorMin: "0 0", anchorMax: "1 1");
                container.AddPanel(color: "0.169 0.162 0.143 0.384", sprite: "assets/content/ui/ui.background.transparent.radial.psd", material: "assets/content/ui/namefontmaterial.mat", anchorMin: "0 0", anchorMax: "1 1");
                var navigation = container.AddContainer(anchorMin: "0 0", anchorMax: "0 1", offsetMin: "0 0", offsetMax: "350 0", name: "AdminMenu_Navigation");
                var homeButton = navigation.AddContainer(//command: "adminmenu homebutton",
                anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"64 -{102f + 32}", offsetMax: "0 -32");
                homeButton.AddImage(content: ADMINMENU_IMAGECRC, color: "0.811 0.811 0.811 1", material: "assets/content/ui/namefontmaterial.mat", anchorMin: "0 0", anchorMax: "0 1", offsetMax: $"146.4 0");
                homeButton.AddText(text: config.Subtext, color: "0.824 0.824 0.824 1", font: CUI.Font.RobotoCondensedBold, fontSize: 16, anchorMin: "0 0", anchorMax: "0 0", offsetMin: "0 -35", offsetMax: "146.4 -10");
                container.AddText(text: $"v{Instance.Version}", color: "0.5 0.5 0.5 0.2", font: CUI.Font.RobotoCondensedBold, fontSize: 12, align: TextAnchor.MiddleRight, anchorMin: "1 0", anchorMax: "1 0", offsetMin: "-100 0", offsetMax: "-10 20");
                AddNavButtons(navigation, mainMenu);
                var body = container.AddContainer(anchorMin: "0 0", anchorMax: "1 1", offsetMin: "350 0", offsetMax: "-64 0", name: "AdminMenu_Body");
                var right = container.AddContainer(anchorMin: "1 0", anchorMax: "1 1", offsetMin: "-64 0", offsetMax: "0 0");
                root.Render(connection);
                connectionData.IsDestroyed = false;
            }

            public void RenderPanel(Panel panel)
            {
                if (panel == null)
                    return;
                CUI.Root root = new CUI.Root("AdminMenu_Body");
                var container = root.AddContainer(anchorMin: "0 0", anchorMax: "1 1", name: "AdminMenu_Panel").AddDestroySelfAttribute();
                Sidebar sidebar = panel.Sidebar;
                if (sidebar != null)
                    AddSidebar(container, sidebar);
                CUI.Element panelBackground = container.AddContainer(anchorMin: "0 0", anchorMax: "1 1", offsetMin: $"{(sidebar != null ? 250 : 0)} 0", offsetMax: "0 0", name: "AdminMenu_Panel_Content");
                if (true)
                {
                    panelBackground = panelBackground.AddPanel(color: "1 1 1 1", material: "assets/content/ui/menuui/mainmenu.panel.mat", imageType: Image.Type.Tiled);
                }

                root.Render(connection);
            }

            public void RenderContent(Content content)
            {
                if (content == null)
                    return;
                CUI.Root root = new CUI.Root("AdminMenu_Panel_Content");
                var container = root.AddContainer(name: "AdminMenu_Panel_TempContent").AddDestroySelfAttribute();
                root.Render(connection);
                content.Render(connectionData);
            }
        }

        public static class Extensions
        {
            public static string ToCuiString(Color color)
            {
                return string.Format("{0} {1} {2} {3}", new object[] { color.r, color.g, color.b, color.a });
            }
        }

        [HarmonyPatch(typeof(BasePlayer), "Tick_Spectator")]
        private static class SpectatorStaff
        {
            private static bool Prefix(BasePlayer __instance)
            {
                if (__instance.serverInput.WasJustPressed(BUTTON.RELOAD))
                {
                    __instance.Respawn();
                    return false;
                }

                int num = 0;
                if (__instance.serverInput.WasJustPressed(BUTTON.LEFT))
                {
                    num--;
                }
                else if (__instance.serverInput.WasJustPressed(BUTTON.RIGHT))
                {
                    num++;
                }

                if (num != 0)
                {
                    __instance.SpectateOffset += num;
                    using (TimeWarning.New("UpdateSpectateTarget", 0))
                        __instance.UpdateSpectateTarget(__instance.spectateFilter);
                }

                return true;
            }
        }

        public class Label
        {
            private static readonly Regex richTextRegex = new Regex(@"<[^>]*>");
            string label;
            string langKey;
            public Label(string label)
            {
                this.label = label;
                if (!string.IsNullOrEmpty(label))
                {
                    this.langKey = richTextRegex.Replace(label.ToPrintable(), string.Empty).Trim();
                    if (!Instance.defaultLang.ContainsKey(this.langKey))
                        Instance.defaultLang.Add(this.langKey, label);
                }
            }

            public override string ToString()
            {
                return label;
            }

            public string Localize(string userId)
            {
                return this.langKey != null ? Instance.lang.GetMessage(this.langKey, Instance, userId) : this.label;
            }

            public string Localize(Connection connection)
            {
                return Localize(connection.userid.ToString());
            }
        }

        public class MainMenu
        {
            public ButtonArray NavButtons { get; set; }
        }

        public class Panel
        {
            public virtual Sidebar Sidebar { get; set; }
            public virtual Dictionary<string, Content> Content { get; set; }
            public Content DefaultContent { get => TryGetContent("default"); }

            public Content TryGetContent(string id)
            {
                if (Content == null)
                    return null;
                if (Content.TryGetValue(id, out Content content))
                    return content;
                return null;
            }

            public virtual void OnOpen(ConnectionData connectionData)
            {
                connectionData.UI.UpdateNavButtons(connectionData.currentMainMenu);
            }

            public virtual void OnClose(ConnectionData connectionData)
            {
                connectionData.userData["backcommand"] = null;
            }
        }

        public class UserInfoPanel : Panel
        {
        }

        public class PermissionPanel : Panel
        {
            public override Sidebar Sidebar { get => null; }

            public override void OnClose(ConnectionData connectionData)
            {
                connectionData.userData["backcommand"] = null;
            }
        }

        public class Sidebar
        {
            public ButtonArray<CategoryButton> CategoryButtons { get; set; }
            public int? AutoActivateCategoryButtonIndex { get; set; } = 0;
        }

        public class SteamInfo
        {
            public string Location { get; set; }
            public string[] Avatars { get; set; }
            public string RegistrationDate { get; set; }
            public string RustHours { get; set; }

            public override string ToString()
            {
                return string.Join(", ", Location, RegistrationDate, RustHours);
            }
        }

        public class Translator15
        {
            public static void Init(string language)
            {
                Dictionary<string, string> lang = new Dictionary<string, string>();
                TextAsset textAsset = FileSystem.Load<TextAsset>($"assets/localization/{language}/engine.json", true);
                if (textAsset == null)
                    return;
                Dictionary<string, string> @object = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
                if (@object == null)
                    return;
                foreach (ItemDefinition itemDefinition in ItemManager.itemList)
                {
                    string key = itemDefinition.displayName.token;
                    if (@object.ContainsKey(key))
                        lang[key] = @object[key];
                }

                translations[language] = lang;
            }

            private static Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>();
            public class Phrase : Translate.Phrase
            {
                private Translator15 translator;
                public Phrase(Translator15 translator)
                {
                    this.translator = translator;
                }

                public override string translated
                {
                    get
                    {
                        if (string.IsNullOrEmpty(this.token))
                        {
                            return this.english;
                        }

                        return translator.Get(this.token, this.english);
                    }
                }
            }

            protected ulong userID;
            public Translator15(ulong userID)
            {
                this.userID = userID;
            }

            public Translator15.Phrase Convert(Translate.Phrase phrase)
            {
                if (phraseCache.TryGetValue(phrase, out Translator15.Phrase phrase1))
                    return phrase1;
                else
                    return phraseCache[phrase] = new Translator15.Phrase(this)
                    {
                        english = phrase.english,
                        token = phrase.token
                    };
            }

            private string GetLanguage()
            {
                return Instance.lang.GetLanguage(userID.ToString());
            }

            public string Get(string key, string def = null)
            {
                if (def == null)
                    def = "#" + key;
                if (string.IsNullOrEmpty(key))
                    return def;
                string language = GetLanguage();
                if (!Translator15.translations.ContainsKey(language))
                    Translator15.Init(language);
                if (Translator15.translations[language].TryGetValue(key, out string result))
                    return result;
                return def;
            }

            private Dictionary<Translate.Phrase, Translator15.Phrase> phraseCache = new Dictionary<Translate.Phrase, Phrase>();
        }

        public class CUI
        {
            public enum Font
            {
                RobotoCondensedBold,
                RobotoCondensedRegular,
                RobotoMonoRegular,
                DroidSansMono,
                PermanentMarker,
                PressStart2PRegular,
                LSD,
                NotoSansArabicBold,
                NotoSansArabicRegular,
                NotoSansHebrewBold,
            }

            private static readonly Dictionary<Font, string> FontToString = new Dictionary<Font, string>
            {
                {
                    Font.RobotoCondensedBold,
                    "RobotoCondensed-Bold.ttf"
                },
                {
                    Font.RobotoCondensedRegular,
                    "RobotoCondensed-Regular.ttf"
                },
                {
                    Font.RobotoMonoRegular,
                    "RobotoMono-Regular.ttf"
                },
                {
                    Font.DroidSansMono,
                    "DroidSansMono.ttf"
                },
                {
                    Font.PermanentMarker,
                    "PermanentMarker.ttf"
                },
                {
                    Font.PressStart2PRegular,
                    "PressStart2P-Regular.ttf"
                },
                {
                    Font.LSD,
                    "lcd.ttf"
                },
                {
                    Font.NotoSansArabicBold,
                    "_nonenglish/arabic/notosansarabic-bold.ttf"
                },
                {
                    Font.NotoSansArabicRegular,
                    "_nonenglish/arabic/notosansarabic-regular.ttf"
                },
                {
                    Font.NotoSansHebrewBold,
                    "_nonenglish/notosanshebrew-bold.ttf"
                },
            };
            public enum InputType
            {
                None,
                Default,
                HudMenuInput
            }

            private static readonly Dictionary<TextAnchor, string> TextAnchorToString = new Dictionary<TextAnchor, string>
            {
                {
                    TextAnchor.UpperLeft,
                    TextAnchor.UpperLeft.ToString()
                },
                {
                    TextAnchor.UpperCenter,
                    TextAnchor.UpperCenter.ToString()
                },
                {
                    TextAnchor.UpperRight,
                    TextAnchor.UpperRight.ToString()
                },
                {
                    TextAnchor.MiddleLeft,
                    TextAnchor.MiddleLeft.ToString()
                },
                {
                    TextAnchor.MiddleCenter,
                    TextAnchor.MiddleCenter.ToString()
                },
                {
                    TextAnchor.MiddleRight,
                    TextAnchor.MiddleRight.ToString()
                },
                {
                    TextAnchor.LowerLeft,
                    TextAnchor.LowerLeft.ToString()
                },
                {
                    TextAnchor.LowerCenter,
                    TextAnchor.LowerCenter.ToString()
                },
                {
                    TextAnchor.LowerRight,
                    TextAnchor.LowerRight.ToString()
                }
            };
            private static readonly Dictionary<VerticalWrapMode, string> VWMToString = new Dictionary<VerticalWrapMode, string>
            {
                {
                    VerticalWrapMode.Truncate,
                    VerticalWrapMode.Truncate.ToString()
                },
                {
                    VerticalWrapMode.Overflow,
                    VerticalWrapMode.Overflow.ToString()
                },
            };
            private static readonly Dictionary<Image.Type, string> ImageTypeToString = new Dictionary<Image.Type, string>
            {
                {
                    Image.Type.Simple,
                    Image.Type.Simple.ToString()
                },
                {
                    Image.Type.Sliced,
                    Image.Type.Sliced.ToString()
                },
                {
                    Image.Type.Tiled,
                    Image.Type.Tiled.ToString()
                },
                {
                    Image.Type.Filled,
                    Image.Type.Filled.ToString()
                },
            };
            private static readonly Dictionary<InputField.LineType, string> LineTypeToString = new Dictionary<InputField.LineType, string>
            {
                {
                    InputField.LineType.MultiLineNewline,
                    InputField.LineType.MultiLineNewline.ToString()
                },
                {
                    InputField.LineType.MultiLineSubmit,
                    InputField.LineType.MultiLineSubmit.ToString()
                },
                {
                    InputField.LineType.SingleLine,
                    InputField.LineType.SingleLine.ToString()
                },
            };
            private static readonly Dictionary<ScrollRect.MovementType, string> MovementTypeToString = new Dictionary<ScrollRect.MovementType, string>
            {
                {
                    ScrollRect.MovementType.Unrestricted,
                    ScrollRect.MovementType.Unrestricted.ToString()
                },
                {
                    ScrollRect.MovementType.Elastic,
                    ScrollRect.MovementType.Elastic.ToString()
                },
                {
                    ScrollRect.MovementType.Clamped,
                    ScrollRect.MovementType.Clamped.ToString()
                },
            };
            public static class Defaults
            {
                public const string VectorZero = "0 0";
                public const string VectorOne = "1 1";
                public const string Color = "1 1 1 1";
                public const string OutlineColor = "0 0 0 1";
                public const string Sprite = "assets/content/ui/ui.background.tile.psd";
                public const string Material = "assets/content/ui/namefontmaterial.mat";
                public const string IconMaterial = "assets/icons/iconmaterial.mat";
                public const Image.Type ImageType = Image.Type.Simple;
                public const CUI.Font Font = CUI.Font.RobotoCondensedRegular;
                public const int FontSize = 14;
                public const TextAnchor Align = TextAnchor.UpperLeft;
                public const VerticalWrapMode VerticalOverflow = VerticalWrapMode.Overflow;
                public const InputField.LineType LineType = InputField.LineType.SingleLine;
            }

            public static Color GetColor(string colorStr)
            {
                return ColorEx.Parse(colorStr);
            }

            public static string GetColorString(Color color)
            {
                return string.Format("{0} {1} {2} {3}", color.r, color.g, color.b, color.a);
            }

            public static void AddUI(Connection connection, string json)
            {
                CommunityEntity.ServerInstance.ClientRPCEx<string>(new SendInfo { connection = connection }, null, "AddUI", json);
            }

            private static void SerializeType(ICuiComponent component, JsonWriter jsonWriter)
            {
                jsonWriter.WritePropertyName("type");
                jsonWriter.WriteValue(component.Type);
            }

            private static void SerializeField(string key, object value, object defaultValue, JsonWriter jsonWriter)
            {
                if (value != null && !value.Equals(defaultValue))
                {
                    if (value is string && defaultValue != null && string.IsNullOrEmpty(value as string))
                        return;
                    jsonWriter.WritePropertyName(key);
                    if (value is ICuiComponent)
                        SerializeComponent(value as ICuiComponent, jsonWriter);
                    else
                        jsonWriter.WriteValue(value ?? defaultValue);
                }
            }

            private static void SerializeField(string key, CuiScrollbar scrollbar, JsonWriter jsonWriter)
            {
                const string defaultHandleSprite = "assets/content/ui/ui.rounded.tga";
                const string defaultHandleColor = "0.15 0.15 0.15 1";
                const string defaultHighlightColor = "0.17 0.17 0.17 1";
                const string defaultPressedColor = "0.2 0.2 0.2 1";
                const string defaultTrackSprite = "assets/content/ui/ui.background.tile.psd";
                const string defaultTrackColor = "0.09 0.09 0.09 1";
                if (scrollbar == null)
                    return;
                jsonWriter.WritePropertyName(key);
                jsonWriter.WriteStartObject();
                SerializeField("invert", scrollbar.Invert, false, jsonWriter);
                SerializeField("autoHide", scrollbar.AutoHide, false, jsonWriter);
                SerializeField("handleSprite", scrollbar.HandleSprite, defaultHandleSprite, jsonWriter);
                SerializeField("size", scrollbar.Size, 20f, jsonWriter);
                SerializeField("handleColor", scrollbar.HandleColor, defaultHandleColor, jsonWriter);
                SerializeField("highlightColor", scrollbar.HighlightColor, defaultHighlightColor, jsonWriter);
                SerializeField("pressedColor", scrollbar.PressedColor, defaultPressedColor, jsonWriter);
                SerializeField("trackSprite", scrollbar.TrackSprite, defaultTrackSprite, jsonWriter);
                SerializeField("trackColor", scrollbar.TrackColor, defaultTrackColor, jsonWriter);
                jsonWriter.WriteEndObject();
            }

            private static void SerializeComponent(ICuiComponent IComponent, JsonWriter jsonWriter)
            {
                const string vector2zero = "0 0";
                const string vector2one = "1 1";
                const string colorWhite = "1 1 1 1";
                const string backgroundTile = "assets/content/ui/ui.background.tile.psd";
                const string iconMaterial = "assets/icons/iconmaterial.mat";
                const string fontBold = "RobotoCondensed-Bold.ttf";
                const string defaultOutlineDistance = "1.0 -1.0";
                void SerializeType() => CUI.SerializeType(IComponent, jsonWriter);
                void SerializeField(string key, object value, object defaultValue) => CUI.SerializeField(key, value, defaultValue, jsonWriter);
                void SerializeScrollbar(string key, CuiScrollbar value) => CUI.SerializeField(key, value, jsonWriter);
                switch (IComponent.Type)
                {
                    case "RectTransform":
                    {
                        CuiRectTransformComponent component = IComponent as CuiRectTransformComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("anchormin", component.AnchorMin, vector2zero);
                        SerializeField("anchormax", component.AnchorMax, vector2one);
                        SerializeField("offsetmin", component.OffsetMin, vector2zero);
                        SerializeField("offsetmax", component.OffsetMax, vector2zero);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "UnityEngine.UI.Image":
                    {
                        CuiImageComponent component = IComponent as CuiImageComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("sprite", component.Sprite, backgroundTile);
                        SerializeField("material", component.Material, iconMaterial);
                        SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Image.Type.Simple]);
                        SerializeField("png", component.Png, null);
                        SerializeField("itemid", component.ItemId, 0);
                        SerializeField("skinid", component.SkinId, 0UL);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "UnityEngine.UI.RawImage":
                    {
                        CuiRawImageComponent component = IComponent as CuiRawImageComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("sprite", component.Sprite, backgroundTile);
                        SerializeField("material", component.Material, iconMaterial);
                        SerializeField("url", component.Url, null);
                        SerializeField("png", component.Png, null);
                        SerializeField("steamid", component.SteamId, null);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "UnityEngine.UI.Text":
                    {
                        CuiTextComponent component = IComponent as CuiTextComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("text", component.Text, null);
                        SerializeField("font", component.Font, fontBold);
                        SerializeField("fontSize", component.FontSize, 14);
                        SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[TextAnchor.UpperLeft]);
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("verticalOverflow", VWMToString[component.VerticalOverflow], VWMToString[VerticalWrapMode.Truncate]);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "UnityEngine.UI.Button":
                    {
                        CuiButtonComponent component = IComponent as CuiButtonComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("sprite", component.Sprite, backgroundTile);
                        SerializeField("material", component.Material, iconMaterial);
                        SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Image.Type.Simple]);
                        SerializeField("command", component.Command, null);
                        SerializeField("close", component.Close, null);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "UnityEngine.UI.InputField":
                    {
                        CuiInputFieldComponent component = IComponent as CuiInputFieldComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("text", component.Text, null);
                        SerializeField("font", component.Font, fontBold);
                        SerializeField("fontSize", component.FontSize, 14);
                        SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[TextAnchor.UpperLeft]);
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("command", component.Command, null);
                        SerializeField("characterLimit", component.CharsLimit, 0);
                        SerializeField("lineType", LineTypeToString[component.LineType], LineTypeToString[InputField.LineType.SingleLine]);
                        SerializeField("readOnly", component.ReadOnly, false);
                        SerializeField("password", component.IsPassword, false);
                        SerializeField("needsKeyboard", component.NeedsKeyboard, false);
                        SerializeField("hudMenuInput", component.HudMenuInput, false);
                        SerializeField("autofocus", component.Autofocus, false);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "UnityEngine.UI.ScrollView":
                    {
                        CuiScrollViewComponent component = IComponent as CuiScrollViewComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("contentTransform", component.ContentTransform, null);
                        SerializeField("horizontal", component.Horizontal, false);
                        SerializeField("vertical", component.Vertical, false);
                        SerializeField("movementType", MovementTypeToString[component.MovementType], MovementTypeToString[ScrollRect.MovementType.Clamped]);
                        SerializeField("elasticity", component.Elasticity, 0.1f);
                        SerializeField("inertia", component.Inertia, false);
                        SerializeField("decelerationRate", component.DecelerationRate, 0.135f);
                        SerializeField("scrollSensitivity", component.ScrollSensitivity, 1f);
                        SerializeScrollbar("horizontalScrollbar", component.HorizontalScrollbar);
                        SerializeScrollbar("verticalScrollbar", component.VerticalScrollbar);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "UnityEngine.UI.Outline":
                    {
                        CuiOutlineComponent component = IComponent as CuiOutlineComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("distance", component.Distance, defaultOutlineDistance);
                        SerializeField("useGraphicAlpha", component.UseGraphicAlpha, false);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "Countdown":
                    {
                        CuiCountdownComponent component = IComponent as CuiCountdownComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("endTime", component.EndTime, 0);
                        SerializeField("startTime", component.StartTime, 0);
                        SerializeField("step", component.Step, 1);
                        SerializeField("command", component.Command, null);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }

                    case "NeedsKeyboard":
                    case "NeedsCursor":
                    {
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        jsonWriter.WriteEndObject();
                        break;
                    }
                }
            }

            [JsonObject(MemberSerialization.OptIn)]
            public class Element : CuiElement
            {
                public new string Name { get; set; } = null;
                public Element ParentElement { get; set; }
                public virtual List<Element> Container => ParentElement?.Container;
                public ComponentList Components { get; set; } = new ComponentList();

                [JsonProperty("name")]
                public string JsonName
                {
                    get
                    {
                        if (Name == null)
                        {
                            string result = this.GetHashCode().ToString();
                            if (ParentElement != null)
                                result.Insert(0, ParentElement.JsonName);
                            return result.GetHashCode().ToString();
                        }

                        return Name;
                    }
                }

                public Element()
                {
                }

                public Element(Element parent)
                {
                    AssignParent(parent);
                }

                public CUI.Element AssignParent(Element parent)
                {
                    if (parent == null)
                        return this;
                    ParentElement = parent;
                    Parent = ParentElement.JsonName;
                    return this;
                }

                public Element AddDestroy(string elementName)
                {
                    this.DestroyUi = elementName;
                    return this;
                }

                public Element AddDestroySelfAttribute()
                {
                    return AddDestroy(this.Name);
                }

                public virtual void WriteJson(JsonWriter jsonWriter)
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("name");
                    jsonWriter.WriteValue(this.JsonName);
                    if (!string.IsNullOrEmpty(Parent))
                    {
                        jsonWriter.WritePropertyName("parent");
                        jsonWriter.WriteValue(this.Parent);
                    }

                    if (!string.IsNullOrEmpty(this.DestroyUi))
                    {
                        jsonWriter.WritePropertyName("destroyUi");
                        jsonWriter.WriteValue(this.DestroyUi);
                    }

                    if (this.Update)
                    {
                        jsonWriter.WritePropertyName("update");
                        jsonWriter.WriteValue(this.Update);
                    }

                    if (this.FadeOut > 0f)
                    {
                        jsonWriter.WritePropertyName("fadeOut");
                        jsonWriter.WriteValue(this.FadeOut);
                    }

                    jsonWriter.WritePropertyName("components");
                    jsonWriter.WriteStartArray();
                    for (int i = 0; i < this.Components.Count; i++)
                    {
                        SerializeComponent(this.Components[i], jsonWriter);
                    }

                    jsonWriter.WriteEndArray();
                    jsonWriter.WriteEndObject();
                }

                public Element Add(Element element)
                {
                    if (element.ParentElement == null)
                        element.AssignParent(this);
                    Container.Add(element);
                    return element;
                }

                public Element AddEmpty(string name = null)
                {
                    return Add(new Element(this) { Name = name });
                }

                public Element AddUpdateElement(string name = null)
                {
                    Element element = AddEmpty(name);
                    element.Parent = null;
                    element.Update = true;
                    return element;
                }

                public Element AddText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Add(ElementContructor.CreateText(text, color, font, fontSize, align, overflow, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddOutlinedText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow, string outlineColor = Defaults.OutlineColor, int outlineWidth = 1, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Add(ElementContructor.CreateOutlinedText(text, color, font, fontSize, align, overflow, outlineColor, outlineWidth, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddInputfield(string command = null, string text = "", string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, InputField.LineType lineType = Defaults.LineType, CUI.InputType inputType = CUI.InputType.Default, bool @readonly = false, bool autoFocus = false, bool isPassword = false, int charsLimit = 0, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Add(ElementContructor.CreateInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddPanel(string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, bool cursorEnabled = false, bool keyboardEnabled = false, string name = null)
                {
                    return Add(ElementContructor.CreatePanel(color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, cursorEnabled, keyboardEnabled, name));
                }

                public Element AddButton(string command = null, string close = null, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Add(ElementContructor.CreateButton(command, close, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddImage(string content, string color = Defaults.Color, string material = null, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Add(ElementContructor.CreateImage(content, color, material, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddHImage(string content, string color = Defaults.Color, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return AddImage(content, color, Defaults.IconMaterial, anchorMin, anchorMax, offsetMin, offsetMax, name);
                }

                public Element AddIcon(int itemId, ulong skin = 0, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.IconMaterial, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Add(ElementContructor.CreateIcon(itemId, skin, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public Element AddContainer(string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Add(ElementContructor.CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name));
                }

                public CUI.Element WithRect(string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero)
                {
                    if (this.Components.Count > 0)
                        this.Components.RemoveAll(c => c is CuiRectTransformComponent);
                    this.Components.Add(new CuiRectTransformComponent() { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax });
                    return this;
                }

                public CUI.Element WithFade(float @in = 0f, float @out = 0f)
                {
                    this.FadeOut = @out;
                    foreach (ICuiComponent component in this.Components)
                    {
                        CuiImageComponent imageComponent = component as CuiImageComponent;
                        if (imageComponent != null)
                        {
                            imageComponent.FadeIn = @in;
                            continue;
                        }

                        CuiButtonComponent buttonComponent = component as CuiButtonComponent;
                        if (buttonComponent != null)
                        {
                            buttonComponent.FadeIn = @in;
                            continue;
                        }

                        CuiTextComponent textComponent = component as CuiTextComponent;
                        if (textComponent != null)
                        {
                            textComponent.FadeIn = @in;
                            continue;
                        }
                    }

                    return this;
                }

                public void AddComponents(params ICuiComponent[] components)
                {
                    this.Components.AddRange(components);
                }

                public CUI.Element WithComponents(params ICuiComponent[] components)
                {
                    AddComponents(components);
                    return this;
                }

                public CUI.Element CreateChild(string name = null, params ICuiComponent[] components)
                {
                    return CUI.Element.Create(name, components).AssignParent(this);
                }

                public static CUI.Element Create(string name = null, params ICuiComponent[] components)
                {
                    return new CUI.Element()
                    {
                        Name = name
                    }.WithComponents(components);
                }

                public class ComponentList : List<ICuiComponent>
                {
                    public T Get<T>()
                        where T : ICuiComponent
                    {
                        return (T)this.Find(c => c.GetType() == typeof(T));
                    }

                    public ComponentList AddImage(string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, int itemId = 0, ulong skinId = 0UL)
                    {
                        Add(new CuiImageComponent { Color = color, Sprite = sprite, Material = material, ImageType = imageType, ItemId = itemId, SkinId = skinId, });
                        return this;
                    }

                    public ComponentList AddRawImage(string content, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.IconMaterial)
                    {
                        CuiRawImageComponent rawImageComponent = new CuiRawImageComponent
                        {
                            Color = color,
                            Sprite = sprite,
                            Material = material,
                        };
                        if (!string.IsNullOrEmpty(content))
                        {
                            if (content.Contains("://"))
                            {
                                rawImageComponent.Url = content;
                            }
                            else if (content.IsNumeric())
                            {
                                if (content.IsSteamId())
                                    rawImageComponent.SteamId = content;
                                else
                                    rawImageComponent.Png = content;
                            }
                        }

                        Add(rawImageComponent);
                        return this;
                    }

                    public ComponentList AddButton(string command = null, string close = null, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType)
                    {
                        Add(new CuiButtonComponent { Command = command, Close = close, Color = color, Sprite = sprite, Material = material, ImageType = imageType, });
                        return this;
                    }

                    public ComponentList AddText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow)
                    {
                        Add(new CuiTextComponent { Text = text, Color = color, Font = FontToString[font], FontSize = fontSize, Align = align, VerticalOverflow = overflow });
                        return this;
                    }

                    public ComponentList AddInputfield(string command = null, string text = "", string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, InputField.LineType lineType = Defaults.LineType, CUI.InputType inputType = CUI.InputType.Default, bool @readonly = false, bool autoFocus = false, bool isPassword = false, int charsLimit = 0)
                    {
                        Add(new CuiInputFieldComponent { Command = command, Text = text, Color = color, Font = FontToString[font], FontSize = fontSize, Align = align, NeedsKeyboard = inputType == InputType.Default, HudMenuInput = inputType == InputType.HudMenuInput, Autofocus = autoFocus, ReadOnly = @readonly, CharsLimit = charsLimit, IsPassword = isPassword, LineType = lineType });
                        return this;
                    }

                    public ComponentList AddScrollView(bool horizontal = false, CuiScrollbar horizonalScrollbar = null, bool vertical = false, CuiScrollbar verticalScrollbar = null, bool inertia = false, ScrollRect.MovementType movementType = ScrollRect.MovementType.Clamped, float decelerationRate = 0.135f, float elasticity = 0.1f, float scrollSensitivity = 1f, string anchorMin = "0 0", string anchorMax = "1 1", string offsetMin = "0 0", string offsetMax = "0 0")
                    {
                        Add(new CuiScrollViewComponent() { ContentTransform = new CuiRectTransformComponent() { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax }, Horizontal = horizontal, HorizontalScrollbar = horizonalScrollbar, Vertical = vertical, VerticalScrollbar = verticalScrollbar, Inertia = inertia, DecelerationRate = decelerationRate, Elasticity = elasticity, ScrollSensitivity = scrollSensitivity, MovementType = movementType, });
                        return this;
                    }

                    public ComponentList AddOutline(string color = Defaults.OutlineColor, int width = 1)
                    {
                        Add(new CuiOutlineComponent { Color = color, Distance = string.Format("{0} -{0}", width) });
                        return this;
                    }

                    public ComponentList AddNeedsKeyboard()
                    {
                        Add(new CuiNeedsKeyboardComponent());
                        return this;
                    }

                    public ComponentList AddNeedsCursor()
                    {
                        Add(new CuiNeedsCursorComponent());
                        return this;
                    }

                    public ComponentList AddCountdown(string command = null, int endTime = 0, int startTime = 0, int step = 1)
                    {
                        Add(new CuiCountdownComponent { Command = command, EndTime = endTime, StartTime = startTime, Step = step });
                        return this;
                    }
                }
            }

            public static class ElementContructor
            {
                public static CUI.Element CreateText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddText(text, color, font, fontSize, align, overflow);
                    return element;
                }

                public static CUI.Element CreateOutlinedText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow, string outlineColor = Defaults.OutlineColor, int outlineWidth = 1, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    CUI.Element element = CreateText(text, color, font, fontSize, align, overflow, anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddOutline(outlineColor, outlineWidth);
                    return element;
                }

                public static CUI.Element CreateInputfield(string command = null, string text = "", string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, InputField.LineType lineType = Defaults.LineType, CUI.InputType inputType = CUI.InputType.Default, bool @readonly = false, bool autoFocus = false, bool isPassword = false, int charsLimit = 0, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit);
                    return element;
                }

                public static CUI.Element CreateButton(string command = null, string close = null, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddButton(command, close, color, sprite, material, imageType);
                    return element;
                }

                public static CUI.Element CreatePanel(string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, bool cursorEnabled = false, bool keyboardEnabled = false, string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType);
                    if (cursorEnabled)
                        element.Components.AddNeedsCursor();
                    if (keyboardEnabled)
                        element.Components.AddNeedsKeyboard();
                    return element;
                }

                public static CUI.Element CreateImage(string content, string color = Defaults.Color, string material = null, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddRawImage(content, color, material: material);
                    return element;
                }

                public static CUI.Element CreateIcon(int itemId, ulong skin = 0, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.IconMaterial, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType, itemId, skin);
                    return element;
                }

                public static Element CreateContainer(string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Element.Create(name).WithRect(anchorMin, anchorMax, offsetMin, offsetMax);
                }
            }

            public class Root : Element
            {
                public bool wasRendered = false;
                private static StringBuilder stringBuilder = new StringBuilder();
                public Root()
                {
                    Name = string.Empty;
                }

                public Root(string rootObjectName = "Overlay")
                {
                    Name = rootObjectName;
                }

                public override List<Element> Container { get; } = new List<Element>();

                public string ToJson(List<Element> elements)
                {
                    stringBuilder.Clear();
                    try
                    {
                        using (StringWriter stringWriter = new StringWriter(stringBuilder))
                        {
                            using (JsonWriter jsonWriter = new JsonTextWriter(stringWriter))
                            {
                                jsonWriter.WriteStartArray();
                                foreach (Element element in elements)
                                    element.WriteJson(jsonWriter);
                                jsonWriter.WriteEndArray();
                            }
                        }

                        return stringBuilder.ToString().Replace("\\n", "\n");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError(ex.Message + "\n" + ex.StackTrace);
                        return string.Empty;
                    }
                }

                public string ToJson()
                {
                    return ToJson(Container);
                }

                public void Render(Connection connection)
                {
                    if (connection == null || !connection.connected)
                        return;
                    wasRendered = true;
                    CUI.AddUI(connection, ToJson(Container));
                }

                public void Render(BasePlayer player)
                {
                    Render(player.Connection);
                }

                public void Update(Connection connection)
                {
                    foreach (Element element in Container)
                        element.Update = true;
                    CUI.AddUI(connection, ToJson(Container));
                }

                public void Update(BasePlayer player)
                {
                    Update(player.Connection);
                }
            }
        }

        public class CenteredTextContent : TextContent
        {
            public CenteredTextContent()
            {
                font = CUI.Font.RobotoCondensedBold;
                fontSize = 24;
                align = TextAnchor.MiddleCenter;
                margin = Vector2.zero;
            }
        }

        public class Content
        {
            public virtual void Render(ConnectionData connectionData)
            {
                CUI.Root root = new CUI.Root("AdminMenu_Panel_TempContent");
                Render(root, connectionData, connectionData.userData);
                root.Render(connectionData.connection);
            }

            protected virtual void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
            }

            public virtual void LoadDefaultUserData(Dictionary<string, object> userData)
            {
            }

            public virtual void RestoreUserData(Dictionary<string, object> userData)
            {
            }
        }

        public class ConvarsContent : Content
        {
            private static readonly Label SEARCH_LABEL = new Label("Search..");
            public static readonly Button SAVE_BUTTON = new Button("Save", "convars.save")
            {
                Permission = "convars.save",
                Style = new ButtonStyle
                {
                    BackgroundColor = "0.455 0.667 0.737 1"
                }
            };
            private static List<Timer> sequentialLoad = new List<Timer>();
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["convars.searchQuery"] = string.Empty;
            }

            public override void RestoreUserData(Dictionary<string, object> userData)
            {
                base.RestoreUserData(userData);
                StopSequentialLoad();
            }

            public void StopSequentialLoad()
            {
                foreach (Timer timer in sequentialLoad)
                    if (!timer.Destroyed)
                        timer.Destroy();
                sequentialLoad.Clear();
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                string searchQuery = (string)userData["convars.searchQuery"];
                var container = root.AddContainer(anchorMin: "0.03 0.04", anchorMax: "0.97 0.99", name: "AdminMenu_Convars").AddDestroySelfAttribute();
                int rows = 16;
                float width = 1f / rows;
                List<ConsoleSystem.Command> commands = ConsoleGen.All.Where(command => command.ServerAdmin && command.Variable && (string.IsNullOrEmpty(searchQuery) || command.FullName.Contains(searchQuery.ToLower()))).ToList();
                int pageCount = Mathf.CeilToInt(commands.Count / (float)rows);
                var layout = root.AddPanel(color: "0 0 0 0", anchorMin: "0.01 0.04", anchorMax: "0.99 0.99", name: "AdminMenu_Convars_Layout").AddDestroySelfAttribute();
                layout.Components.AddScrollView(vertical: true, scrollSensitivity: 20, anchorMin: $"0 -{pageCount - 1}", anchorMax: $"1 1");
                var bottom = root.AddContainer(anchorMin: "0.02 0", anchorMax: "0.98 0.035", name: "AdminMenu_Convars_Bottom").AddDestroySelfAttribute();
                var searchPanel = bottom.AddButton(command: "adminmenu convars.opensearch", color: "0.15 0.15 0.15 1", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "0 0", offsetMax: "250 0", name: "Search");
                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "-2 0", offsetMax: "0 0");
                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "1 0", anchorMax: "1 1", offsetMin: "0 0", offsetMax: "2 0");
                if (string.IsNullOrEmpty(searchQuery))
                {
                    searchPanel.AddText(text: SEARCH_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Placeholder");
                }
                else
                {
                    searchPanel.AddInputfield(command: "adminmenu convars.search.input", text: searchQuery, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Inputfield");
                }

                if (SAVE_BUTTON.UserHasPermission(connectionData.connection))
                {
                    bottom.AddButton(command: $"adminmenu {SAVE_BUTTON.Command}", sprite: "assets/content/ui/ui.background.rounded.png", imageType: UnityEngine.UI.Image.Type.Tiled, color: SAVE_BUTTON.Style.BackgroundColor, anchorMin: "1 0", anchorMax: "1 1", offsetMin: $"-100 0", offsetMax: $"0 0").AddText(text: SAVE_BUTTON.Label.Localize(connectionData.connection), align: TextAnchor.MiddleCenter, offsetMin: "10 0", offsetMax: "-10 0");
                }

                StopSequentialLoad();
                for (int i = 0; i < pageCount; i++)
                {
                    CUI.Root layoutRoot = new CUI.Root(layout.Name);
                    var screenContainer = layoutRoot.AddContainer(anchorMin: $"0 {(pageCount - (i + 1)) / (float)pageCount}", anchorMax: $"1 {(pageCount - i) / (float)pageCount}", name: $"{layoutRoot.Name}_Screen_{i}").AddDestroySelfAttribute();
                    for (int j = 0; j < rows; j++)
                    {
                        ConsoleSystem.Command command = commands.ElementAtOrDefault((i * rows) + j);
                        if (command == null)
                            break;
                        var convarContainer = screenContainer.AddContainer(anchorMin: $"0 {1f - width * (j + 1)}", anchorMax: $"1 {1f - width * j}");
                        convarContainer.AddText(text: $"<b>{command.FullName}</b>{(!string.IsNullOrEmpty(command.Description) ? $"\n<color=#7A7A7A><size=11>{command.Description}</size></color>" : string.Empty)}", align: TextAnchor.MiddleLeft, anchorMax: "0.7 1");
                        convarContainer.AddPanel(sprite: "assets/content/ui/ui.background.rounded.png", imageType: UnityEngine.UI.Image.Type.Tiled, color: "0.2 0.2 0.2 1", anchorMin: "0.75 0.1", anchorMax: "1 0.9").AddInputfield(command: $"adminmenu convar.setvalue {command.FullName} ", text: command.String, align: TextAnchor.MiddleCenter, offsetMin: "10 0", offsetMax: "-10 0");
                    }

                    sequentialLoad.Add(Instance.timer.Once(i * 0.1f, () => layoutRoot.Render(connectionData.connection)));
                }
            }

            public void OpenSearch(Connection connection)
            {
                CUI.Root root = new CUI.Root("Search");
                root.AddInputfield(command: "adminmenu convars.search.input", text: "", align: TextAnchor.MiddleLeft, autoFocus: true, offsetMin: "10 0", name: "Search_Inputfield").DestroyUi = "Search_Placeholder";
                root.Render(connection);
            }
        }

        public class GiveMenuContent : Content
        {
            private static readonly Label NAME_LABEL = new Label("NAME");
            private static readonly Label SKINID_LABEL = new Label("SKIN ID");
            private static readonly Label AMOUNT_LABEL = new Label("AMOUNT");
            private static readonly Label GIVE_LABEL = new Label("GIVE");
            private static readonly Label BLUEPRINT_LABEL = new Label("BLUEPRINT?");
            private static readonly Label SEARCH_LABEL = new Label("Search..");
            private static readonly Label ITEM_COUNTER_LABEL = new Label("{0} items");
            private static List<Timer> sequentialLoad = new List<Timer>();
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["givemenu.category"] = ItemCategory.All;
                userData["givemenu.searchQuery"] = string.Empty;
                userData["givemenu.popup.shown"] = false;
            }

            public override void RestoreUserData(Dictionary<string, object> userData)
            {
                base.RestoreUserData(userData);
                StopSequentialLoad();
            }

            public void StopSequentialLoad()
            {
                foreach (Timer timer in sequentialLoad)
                    if (!timer.Destroyed)
                        timer.Destroy();
                sequentialLoad.Clear();
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                if ((bool)userData["givemenu.popup.shown"])
                {
                    Popup(root, connectionData, userData);
                    return;
                }
                else
                {
                    root.AddEmpty().AddDestroy("AdminMenu_GiveMenu_GivePopup");
                }

                ItemCategory category = (ItemCategory)userData["givemenu.category"];
                string searchQuery = (string)userData["givemenu.searchQuery"];
                const int columns = 10;
                const int rows = 11;
                const int itemsPerPage = columns * rows;
                const float width = 1f / columns;
                const float height = 1f / rows;
                const float panelSize = 60;
                const float dividedPanelSize = panelSize / 2;
                List<ItemDefinition> itemList = ItemManager.itemList.Where(def => (category == ItemCategory.All || def.category == category) && (string.IsNullOrEmpty(searchQuery) || def.shortname.Contains(searchQuery, CompareOptions.IgnoreCase) || connectionData.translator.Convert(def.displayName).translated.Contains(searchQuery, CompareOptions.IgnoreCase))).ToList();
                foreach (Configuration.ItemPreset preset in config.GiveItemPresets)
                {
                    if (!Enum.TryParse<ItemCategory>(preset.Category, out ItemCategory presetCategory) || category != ItemCategory.All && category != presetCategory)
                        continue;
                    if (!string.IsNullOrEmpty(searchQuery) && !preset.Name.Contains(searchQuery, CompareOptions.IgnoreCase))
                        continue;
                    itemList.Add(new ItemDefinition { itemid = preset.ShortName.GetHashCode(), shortname = preset.Name, category = presetCategory });
                }

                int pageCount = Mathf.CeilToInt(itemList.Count / (float)itemsPerPage);
                var layout = root.AddPanel(color: "0 0 0 0", anchorMin: "0.01 0.04", anchorMax: "0.99 0.99", name: "AdminMenu_ItemList_Layout").AddDestroySelfAttribute();
                layout.Components.AddScrollView(vertical: true, scrollSensitivity: 60, anchorMin: $"0 -{pageCount - 1}", anchorMax: $"1 1");
                layout.AddEmpty().AddDestroy("AdminMenu_GiveMenu_Give");
                var bottom = root.AddPanel(color: "0.25 0.25 0.25 0", anchorMin: "0.02 0", anchorMax: "0.98 0.035", name: "AdminMenu_ItemList_Bottom").AddDestroySelfAttribute();
                bottom.AddText(text: string.Format(ITEM_COUNTER_LABEL.Localize(connectionData.connection), itemList.Count), font: CUI.Font.RobotoCondensedBold, align: TextAnchor.MiddleRight, offsetMax: "-10 0");
                var searchPanel = bottom.AddButton(command: "adminmenu givemenu.opensearch", color: "0.15 0.15 0.15 1", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "0 0", offsetMax: "250 0", name: "Search");
                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "-2 0", offsetMax: "0 0");
                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "1 0", anchorMax: "1 1", offsetMin: "0 0", offsetMax: "2 0");
                if (string.IsNullOrEmpty(searchQuery))
                {
                    searchPanel.AddText(text: SEARCH_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Placeholder");
                }
                else
                {
                    searchPanel.AddInputfield(command: "adminmenu givemenu.search.input", text: searchQuery, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Inputfield");
                }

                StopSequentialLoad();
                for (int i = 0; i < pageCount; i++)
                {
                    CUI.Root layoutRoot = new CUI.Root(layout.Name);
                    var screenContainer = layoutRoot.AddContainer(anchorMin: $"0 {(pageCount - (i + 1)) / (float)pageCount}", anchorMax: $"1 {(pageCount - i) / (float)pageCount}", name: $"{layoutRoot.Name}_Screen_{i}").AddDestroySelfAttribute();
                    for (int a = 0; a < rows; a++)
                    {
                        for (int b = 0; b < columns; b++)
                        {
                            int index = (i * itemsPerPage) + a * columns + b;
                            if (index > itemList.Count - 1)
                                break;
                            ItemDefinition itemDef = itemList.ElementAtOrDefault(index);
                            ulong skinId = 0UL;
                            string command = $"adminmenu givemenu.popup show {itemDef.itemid}";
                            if (itemDef == null)
                            {
                                Configuration.ItemPreset preset = config.GiveItemPresets.Find(p => p.ShortName.GetHashCode() == itemDef.itemid && p.Name == itemDef.shortname);
                                command = $"adminmenu givemenu.popup show_custom {itemDef.itemid} {preset.SkinId} {preset.Name}";
                                skinId = preset.SkinId;
                            }

                            var itemButton = screenContainer.AddContainer(anchorMin: $"{b * width} {1 - (a + 1) * height}", anchorMax: $"{(b + 1) * width} {1 - a * height}").AddButton(command: command, color: "0.25 0.25 0.25 0.6", sprite: "assets/content/ui/ui.rounded.tga", anchorMin: "0.5 0.5", anchorMax: "0.5 0.5", offsetMin: $"-{dividedPanelSize} -{dividedPanelSize}", offsetMax: $"{dividedPanelSize} {dividedPanelSize}");
                            itemButton.AddIcon(itemId: itemDef.itemid, skin: skinId, offsetMin: "4 4", offsetMax: "-4 -4");
                        }
                    }

                    sequentialLoad.Add(Instance.timer.Once(i * 0.1f, () => layoutRoot.Render(connectionData.connection)));
                }
            }

            private void Popup(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                int itemId = (int)userData["givemenu.popup.itemid"];
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemId);
                if (itemDefinition == null)
                    return;
                int amount = (int)userData["givemenu.popup.amount"];
                ulong skinId = (ulong)userData["givemenu.popup.skin"];
                string name = (string)userData["givemenu.popup.name"];
                bool isBluprint = (bool)userData["givemenu.popup.isblueprint"];
                string itemTranslatedDisplayName = connectionData.translator.Convert(itemDefinition.displayName).translated;
                var backgroundButton = root.AddButton(color: "0 0 0 0.8", command: "adminmenu givemenu.popup close", close: "AdminMenu_GiveMenu_GivePopup", anchorMin: "0 0", anchorMax: "1 1", name: "AdminMenu_GiveMenu_GivePopup").AddDestroySelfAttribute();
                var panel = backgroundButton.AddButton(command: null, anchorMin: "0.5 0.5", anchorMax: "0.5 0.5", offsetMin: "-200 -250", offsetMax: "200 250").AddPanel(color: "0.05 0.05 0.05 1");
                var header = panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0 1", anchorMax: "1 1", offsetMin: "0 -40", offsetMax: "0 0");
                header.AddText(text: itemTranslatedDisplayName.ToUpper(), font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter, overflow: VerticalWrapMode.Truncate);
                var iconContainer = panel.AddContainer(anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-100 -265", offsetMax: "100 -65", name: "GiveMenu_ItemIconContainer");
                if (skinId == 0UL && Instance.ImageLibrary != null && !itemDefinition.HasFlag((ItemDefinition.Flag)128))
                {
                    string iconCRC = Instance.ImageLibrary.Call<string>("GetImage", itemDefinition.shortname, 0UL);
                    if (iconCRC is string)
                    {
                        iconContainer.AddImage(iconCRC, name: "GiveMenu_ItemIcon");
                    }
                    else
                    {
                        Instance.ImageLibrary.Call("AddImage", $"https://carbonmod.gg/assets/media/items/{itemDefinition.shortname}.png", itemDefinition.shortname, 0UL, new Action(() =>
                        {
                            if (!(bool)userData["givemenu.popup.shown"] || (int)userData["givemenu.popup.itemid"] != itemDefinition.itemid)
                                return;
                            string iconCRC = Instance.ImageLibrary.Call<string>("GetImage", itemDefinition.shortname, 0UL);
                            if (iconCRC is string)
                            {
                                CUI.Root root = new CUI.Root("GiveMenu_ItemIconContainer");
                                root.AddImage(iconCRC, name: "GiveMenu_ItemIcon").AddDestroySelfAttribute();
                                root.Render(connectionData.connection);
                            }
                        }));
                        iconContainer.AddIcon(itemDefinition.itemid, name: "GiveMenu_ItemIcon");
                    }
                }
                else
                    iconContainer.AddIcon(itemDefinition.itemid, skinId, name: "GiveMenu_ItemIcon");
                var outer = panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0 0", anchorMax: "1 0", offsetMin: "0 0", offsetMax: "0 200");
                outer.AddInputfield(command: null, text: itemDefinition.shortname, color: "0.25 0.25 0.25 0.8", font: CUI.Font.NotoSansArabicBold, fontSize: 16, align: TextAnchor.MiddleCenter, @readonly: true, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "10 0", offsetMax: "-10 35");
                outer.AddText(text: NAME_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter, overflow: VerticalWrapMode.Truncate, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -40", offsetMax: "-60 -15");
                outer.AddPanel(color: "0.05 0.05 0.05 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -40", offsetMax: "170 -15").AddInputfield(command: "adminmenu givemenu.popup set_name", text: name ?? itemTranslatedDisplayName, font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                outer.AddText(text: SKINID_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter, overflow: VerticalWrapMode.Truncate, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -75", offsetMax: "-60 -50");
                outer.AddPanel(color: "0.05 0.05 0.05 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -75", offsetMax: "170 -50").AddInputfield(command: "adminmenu givemenu.popup set_skin", text: skinId.ToString(), font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                outer.AddText(text: AMOUNT_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter, overflow: VerticalWrapMode.Truncate, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -110", offsetMax: "-60 -85");
                outer.AddPanel(color: "0.05 0.05 0.05 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -110", offsetMax: "50 -85").AddInputfield(command: "adminmenu givemenu.popup set_amount", text: amount.ToString(), font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                outer.AddButton(color: "0.2 0.2 0.2 1", command: $"adminmenu givemenu.popup set_amount {amount + 1}", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "55 -110", offsetMax: "78.75 -85").AddText(text: "+1", font: CUI.Font.RobotoCondensedBold, fontSize: 10, align: TextAnchor.MiddleCenter);
                outer.AddButton(color: "0.2 0.2 0.2 1", command: $"adminmenu givemenu.popup set_amount {amount + 100}", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "83.75 -110", offsetMax: "107.5 -85").AddText(text: "+100", font: CUI.Font.RobotoCondensedBold, fontSize: 10, align: TextAnchor.MiddleCenter);
                outer.AddButton(color: "0.2 0.2 0.2 1", command: $"adminmenu givemenu.popup set_amount {amount + 1000}", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "112.5 -110", offsetMax: "136.25 -85").AddText(text: "+1k", font: CUI.Font.RobotoCondensedBold, fontSize: 10, align: TextAnchor.MiddleCenter);
                outer.AddButton(color: "0.2 0.2 0.2 1", command: $"adminmenu givemenu.popup set_amount {amount + 10000}", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "141.25 -110", offsetMax: "170 -85").AddText(text: "+10k", font: CUI.Font.RobotoCondensedBold, fontSize: 10, align: TextAnchor.MiddleCenter);
                outer.AddText(text: BLUEPRINT_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter, overflow: VerticalWrapMode.Truncate, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -145", offsetMax: "-60 -120");
                var blueprintCheckbox = outer.AddButton(command: "adminmenu givemenu.popup isblueprint_toggle", color: "0.05 0.05 0.05 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -145", offsetMax: "-15 -120");
                if (isBluprint)
                {
                    blueprintCheckbox.AddPanel(color: "0.698 0.878 0.557 0.6", offsetMin: "4 4", offsetMax: "-4 -4").WithFade(0.2f);
                }

                outer.AddButton(color: "0.2 0.2 0.2 1", command: "adminmenu givemenu.popup give", anchorMin: "0.5 0", anchorMax: "0.5 0", offsetMin: "-80 10", offsetMax: "80 37").AddText(text: GIVE_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter);
            }

            public void OpenSearch(Connection connection)
            {
                CUI.Root root = new CUI.Root("Search");
                root.AddInputfield(command: "adminmenu givemenu.search.input", text: "", align: TextAnchor.MiddleLeft, autoFocus: true, offsetMin: "10 0", name: "Search_Inputfield").DestroyUi = "Search_Placeholder";
                root.Render(connection);
            }
        }

        public class GroupInfoContent : Content
        {
            private static readonly Label GROUPNAME_LABEL = new Label("Group Name: {0}");
            private static readonly Label USERS_LABEL = new Label("Users: {0}");
            private static readonly CloneGroupPopup CloneGroupPopup = new CloneGroupPopup();
            private static readonly RemoveGroupPopup ConfirmRemovePopup = new RemoveGroupPopup();
            public ButtonArray[] buttons;
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["groupinfo[popup:clonegroup]"] = false;
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                if ((bool)userData["groupinfo[popup:clonegroup]"])
                {
                    CloneGroupPopup.AddUI(root, connectionData, userData);
                    return;
                }
                else
                {
                    root.Add(new CUI.Element { DestroyUi = "AdminMenu_Popup_CloneGroupPopup" });
                }

                if (buttons == null)
                    return;
                string connectionUserId = ((ulong)userData["userId"]).ToString();
                string groupName = userData["groupinfo.groupName"].ToString();
                if (groupName == null)
                    return;
                if (!Instance.permission.GroupExists(groupName))
                    return;
                var container = root.AddContainer(name: "AdminMenu_GroupInfo_Info").AddDestroySelfAttribute();
                var basic_info_container = container.AddContainer(anchorMin: "0 1", anchorMax: "1 1", offsetMin: "30 -180", offsetMax: "-30 -30");
                basic_info_container.AddInputfield(command: null, text: string.Format(GROUPNAME_LABEL.Localize(connectionData.connection), groupName), font: CUI.Font.RobotoCondensedBold, fontSize: 24, align: TextAnchor.MiddleLeft, @readonly: true, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "20 -30", offsetMax: "0 0");
                basic_info_container.AddText(text: string.Format(USERS_LABEL.Localize(connectionData.connection), Instance.permission.GetUsersInGroup(groupName).Length), font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleLeft, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "20 -60", offsetMax: "0 -35");
                var actionButtonsContainer = container.AddContainer(anchorMin: "0 0", anchorMax: "1 1", offsetMin: "30 10", offsetMax: "-30 -230");
                float offset = 10f;
                for (int a = 0; a < buttons.Length; a++)
                {
                    IEnumerable<Button> rowButtons = buttons[a].GetAllowedButtons(connectionUserId);
                    for (int b = 0; b < rowButtons.Count(); b++)
                    {
                        Button button = rowButtons.ElementAtOrDefault(b);
                        if (button == null)
                            continue;
                        actionButtonsContainer.AddButton(color: "0.3 0.3 0.3 0.6", command: $"adminmenu {button.Command} {string.Join(" ", button.Args)}", anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"{b * 150 + b * offset} -{(a + 1) * 35 + a * offset}", offsetMax: $"{(b + 1) * 150 + b * offset} -{a * 35 + a * offset}").AddText(text: button.Label.Localize(connectionUserId), font: CUI.Font.RobotoMonoRegular, fontSize: 12, align: TextAnchor.MiddleCenter);
                    }
                }
            }

            public void RemoveConfirmPopup(ConnectionData connectionData)
            {
                CUI.Root root = new CUI.Root("AdminMenu_Panel_TempContent");
                ConfirmRemovePopup.AddUI(root, connectionData, connectionData.userData);
                root.Render(connectionData.connection);
            }
        }

        public class GroupListContent : Content
        {
            private static readonly Label CREATEGROUP_LABEL = new Label("CREATE GROUP");
            private static readonly CreateGroupPopup CreateGroupPopup = new CreateGroupPopup();
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["grouplist[popup:creategroup]"] = false;
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                const int columns = 4;
                const int rows = 16;
                const int perPage = rows * columns;
                const float width = 1f / columns;
                const float height = 1f / rows;
                if ((bool)userData["grouplist[popup:creategroup]"])
                {
                    CreateGroupPopup.AddUI(root, connectionData, userData);
                    return;
                }
                else
                {
                    root.Add(new CUI.Element { DestroyUi = "AdminMenu_Popup_CreateGroupPopup" });
                }

                string[] groups = Instance.permission.GetGroups();
                int pageCount = Mathf.CeilToInt(groups.Length / (float)perPage);
                var layout = root.AddPanel(color: "0 0 0 0", anchorMin: "0.01 0.04", anchorMax: "0.99 0.99", name: "AdminMenu_GroupList_Layout").AddDestroySelfAttribute();
                layout.Components.AddScrollView(vertical: true, scrollSensitivity: 20, anchorMin: $"0 -{pageCount - 1}", anchorMax: $"1 1");
                for (int page = 0; page < pageCount; page++)
                {
                    var screenContainer = layout.AddContainer(anchorMin: $"0 {(pageCount - (page + 1)) / (float)pageCount}", anchorMax: $"1 {(pageCount - page) / (float)pageCount}").AddDestroySelfAttribute();
                    for (int a = 0; a < rows; a++)
                    {
                        for (int b = 0; b < columns; b++)
                        {
                            int index = page * perPage + a * columns + b;
                            if (index >= groups.Length)
                                break;
                            string group = groups[index];
                            var container = screenContainer.AddContainer(anchorMin: $"{b * width} {1 - (a + 1) * height}", anchorMax: $"{(b + 1) * width} {1 - a * height}");
                            var button = container.AddButton(command: $"adminmenu permissionmanager.select_group {group}", color: "0.25 0.25 0.25 0.6", anchorMin: "0.5 0.5", anchorMax: "0.5 0.5", offsetMin: $"-73 -20", offsetMax: $"73 20");
                            button.AddText(text: group, fontSize: 12, align: TextAnchor.MiddleCenter, offsetMin: "6 0", offsetMax: "-6 0");
                        }
                    }

                    root.AddButton(command: "adminmenu grouplist[popup:creategroup] show", color: "0.25 0.25 0.25 0.6", anchorMin: "0 0", anchorMax: "1 0.035").AddText(text: CREATEGROUP_LABEL.Localize(connectionData.connection), fontSize: 12, align: TextAnchor.MiddleCenter, offsetMin: "6 0", offsetMax: "-6 0");
                }
            }
        }

        public class NewPermissionManagerContent : Content
        {
            const float buttonHeight = 30;
            const float halfbuttonHeight = buttonHeight * 0.5f;
            const float lineThickness = 2f;
            const float halfLineThickness = lineThickness * 0.5f;
            private static Label GROUP_LABEL = new Label("GROUP");
            private static Label USER_LABEL = new Label("USER");
            private static Label GROUPS_LABEL = new Label("Groups");
            private static Label PERMISSIONS_LABEL = new Label("Permissions");
            private static Label MANAGE_LABEL = new Label("Manage");
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                base.LoadDefaultUserData(userData);
                userData["permissions.target_type"] = null;
                userData["permissions.target"] = null;
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var layout = root.AddPanel(color: "0 0 0 0", anchorMin: "0.01 0.01", anchorMax: "0.99 0.99", name: "AdminMenu_PermissionManager_Layout").AddDestroySelfAttribute();
                string type = userData["permissions.target_type"]?.ToString();
                string target = userData["permissions.target"]?.ToString();
                int x = 30;
                int y = 30;
                int totalY = 0;
                bool isTargetNull = type == null || target == null;
                float targetContainerHeight = isTargetNull ? buttonHeight * 2 + 5 : buttonHeight + 10;
                float halfTargetContainerHeight = targetContainerHeight * 0.5f;
                var targetContainer = layout.AddContainer(anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"{x} -{y + targetContainerHeight}", offsetMax: $"{x += 150} -{y}");
                if (isTargetNull)
                {
                    targetContainer.AddButton(command: $"adminmenu permissionmanager.show_groups", color: ButtonStyle.Default.BackgroundColor, anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"0 -{buttonHeight}").AddText(text: GROUP_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, align: TextAnchor.MiddleCenter);
                    targetContainer.AddButton(command: $"adminmenu permissionmanager.show_players", color: ButtonStyle.Default.BackgroundColor, anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"0 -{buttonHeight * 2 + 5}", offsetMax: $"0 -{buttonHeight + 5}").AddText(text: USER_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, align: TextAnchor.MiddleCenter);
                    return;
                }

                string targetButtonText;
                switch (type)
                {
                    case "group":
                        targetButtonText = $"{GROUP_LABEL.Localize(connectionData.connection)}\n<b>{target}</b>";
                        break;
                    case "user":
                        targetButtonText = $"{USER_LABEL.Localize(connectionData.connection)}\n<b>{ServerMgr.Instance.persistance.GetPlayerName(ulong.Parse(target))}</b>";
                        break;
                    default:
                        return;
                }

                targetContainer.AddButton(command: "adminmenu permissionmanager.reset", color: ButtonStyle.Default.BackgroundColor, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "0 -40").AddText(text: targetButtonText, align: TextAnchor.MiddleCenter, overflow: VerticalWrapMode.Truncate);
                y += (int)halfTargetContainerHeight;
                switch (type)
                {
                    case "group":
                    {
                        targetContainer.AddPanel(anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: $"-{halfLineThickness} -{buttonHeight + 35}", offsetMax: $"{halfLineThickness} -{buttonHeight + 15}");
                        targetContainer.AddButton(command: $"adminmenu groupinfo.open {target}", color: ButtonStyle.Default.BackgroundColor, anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"0 -{buttonHeight * 2 + 40}", offsetMax: $"0 -{buttonHeight + 40}").AddText(text: MANAGE_LABEL.Localize(connectionData.connection), align: TextAnchor.MiddleCenter, overflow: VerticalWrapMode.Truncate);
                        AddPluginsBranch(layout, userData, ref x, ref y, out totalY);
                        break;
                    }

                    case "user":
                    {
                        AddButtonBranch(layout, new string[] { GROUPS_LABEL.Localize(connectionData.connection), PERMISSIONS_LABEL.Localize(connectionData.connection) }.Select((text, index) =>
                        {
                            CUI.Root root = new CUI.Root();
                            root.AddButton(command: $"adminmenu permissionmanager.show_user_category {index} {x + 200} {y + index * 35 + halfLineThickness}", color: ButtonStyle.Default.BackgroundColor, anchorMin: "0 1", anchorMax: "0 1", offsetMax: $"150 {buttonHeight}").AddText(text: text, overflow: VerticalWrapMode.Truncate, align: TextAnchor.MiddleCenter, offsetMin: "3 0", offsetMax: "-3 0");
                            return root;
                        }).ToList(), ref x, ref y, out totalY, "Branch1");
                        break;
                    }
                }

                userData["permissionmanager.totalY"] = totalY;
                layout.Components.AddScrollView(vertical: true, horizontal: true, scrollSensitivity: 50, anchorMin: "0 1", anchorMax: "0 1", offsetMin: "0 -10000", offsetMax: "10000 0");
            }

            private static void AddButtonBranch(CUI.Element parent, List<CUI.Root> buttons, ref int x, ref int y, out int totalY, string name = null)
            {
                totalY = y;
                Vector2 maxButtonSize = default(Vector2);
                var container = parent.AddContainer(name: name).AddDestroySelfAttribute();
                container.AddPanel(anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"{x += 5} -{y + halfLineThickness}", offsetMax: $"{x += 30} -{y - halfLineThickness}");
                for (int i = 0; i < buttons.Count; i++)
                {
                    CUI.Root root = buttons[i];
                    CUI.Element button = root.Container.First();
                    CuiRectTransformComponent rect = button.Components.Get<CuiRectTransformComponent>();
                    Vector2 offsetMin = Vector2Ex.Parse(rect.OffsetMin);
                    Vector2 offsetMax = Vector2Ex.Parse(rect.OffsetMax);
                    Vector2 difference = offsetMax - offsetMin;
                    if (i > 0)
                    {
                        container.AddPanel(anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"{x - halfLineThickness} -{y + i * (difference.y + 5) + 1}", offsetMax: $"{x + halfLineThickness} -{y + (i - 1) * (difference.y + 5)}");
                    }

                    if (difference.x > maxButtonSize.x)
                        maxButtonSize = difference;
                    container.AddPanel(anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"{x} -{y + i * (difference.y + 5) + halfLineThickness}", offsetMax: $"{x + 10} -{y + i * (difference.y + 5) - halfLineThickness}");
                    rect.AnchorMin = "0 1";
                    rect.AnchorMax = "0 1";
                    rect.OffsetMin = $"{x + 15} -{y + i * (difference.y + 5) + halfLineThickness + difference.y * 0.5f}";
                    rect.OffsetMax = $"{x + 15 + difference.x} -{y + i * (difference.y + 5) - halfLineThickness - difference.y * 0.5f}";
                    button.AssignParent(container);
                    container.Container.AddRange(root.Container);
                }

                x += 15 + (int)maxButtonSize.x;
                totalY = y + buttons.Count * ((int)maxButtonSize.y + 5);
            }

            public static void AddGroupsBranch(CUI.Element parent, ConnectionData connectionData, ref int x, ref int y, out int totalY)
            {
                string userIdString = connectionData.userData["permissions.target"].ToString();
                int xx = x;
                int yy = y;
                AddButtonBranch(parent, Instance.permission.GetGroups().OrderBy(s => s).OrderBy(s => s).Select((groupName, index) =>
                {
                    bool hasGroup = Instance.permission.UserHasGroup(userIdString, groupName);
                    CUI.Root root = new CUI.Root();
                    CUI.Element button = root.AddButton(command: $"adminmenu permissionmanager.usergroups {(!hasGroup ? "grant" : "revoke")} {groupName}", color: ButtonStyle.Default.BackgroundColor, anchorMin: "0 1", anchorMax: "0 1", offsetMax: $"150 {buttonHeight}", name: groupName);
                    button.AddText(text: groupName, overflow: VerticalWrapMode.Truncate, align: TextAnchor.MiddleCenter, offsetMin: "3 0", offsetMax: "-3 0");
                    string color = hasGroup ? "0.3 0.6 0.7 1" : "0 0 0 0";
                    button.AddPanel(color: color, anchorMin: "0 0", anchorMax: "1 0", offsetMin: "0 0", offsetMax: "0 2", name: $"{groupName} - COLOR");
                    return root;
                }).ToList(), ref x, ref y, out totalY, "Branch2");
                connectionData.userData["permissionmanager.totalY"] = totalY;
            }

            public static void AddPluginsBranch(CUI.Element parent, Dictionary<string, object> userData, ref int x, ref int y, out int totalY)
            {
                int xx = x;
                int yy = y;
                AddButtonBranch(parent, Instance.plugins.GetAll().Select(p => p.Name).Where(name => Instance.GetPermissions(name)?.Length > 0).OrderBy(s => s).OrderBy(s => s).Select((pluginName, index) =>
                {
                    CUI.Root root = new CUI.Root();
                    root.AddButton(command: $"adminmenu permissionmanager.show_permissions {pluginName} {xx + 200} {yy + index * 35 + halfLineThickness}", color: ButtonStyle.Default.BackgroundColor, anchorMin: "0 1", anchorMax: "0 1", offsetMax: $"150 {buttonHeight}").AddText(text: pluginName, overflow: VerticalWrapMode.Truncate, align: TextAnchor.MiddleCenter, offsetMin: "3 0", offsetMax: "-3 0");
                    return root;
                }).ToList(), ref x, ref y, out totalY, "Branch2");
                userData["permissionmanager.totalY"] = totalY;
            }

            public static void AddPermissionsBranch(CUI.Element parent, Dictionary<string, object> userData, string pluginName, ref int x, ref int y, out int totalY)
            {
                string type = userData["permissions.target_type"]?.ToString();
                string target = userData["permissions.target"]?.ToString();
                bool isTargetUser = false;
                if (type == "user")
                    isTargetUser = true;
                string[] permissions = Instance.GetPermissions(pluginName);
                if (permissions == null || permissions.Length == 0)
                {
                    parent.AddDestroy("PermissionsBranch");
                    totalY = y;
                    return;
                }

                AddButtonBranch(parent, permissions.OrderBy(s => s).Select(permission =>
                {
                    CUI.Root root = new CUI.Root();
                    CUI.Element button = root.AddButton(color: ButtonStyle.Default.BackgroundColor, offsetMax: "300 30", name: permission);
                    string permissionPrefix = $"{pluginName.ToLower()}.";
                    string visualName;
                    if (permission.StartsWith(permissionPrefix))
                        visualName = permission.Replace(permissionPrefix, string.Empty);
                    else
                        visualName = $"<i>{permission}</i>";
                    button.AddText(text: visualName, align: TextAnchor.MiddleCenter, offsetMin: "3 0", offsetMax: "-3 0");
                    if (target != null)
                    {
                        const string userColor = "0.5 0.7 0.4 1";
                        const string groupColor = "0.3 0.6 0.7 1";
                        bool hasUser = false;
                        bool hasGroup = false;
                        if (isTargetUser)
                        {
                            var permUserData = Instance.permission.GetUserData(target);
                            if (permUserData.Perms.Contains(permission, StringComparer.OrdinalIgnoreCase))
                            {
                                hasUser = true;
                            }
                            else if (Instance.permission.GroupsHavePermission(permUserData.Groups, permission))
                            {
                                hasGroup = true;
                            }
                        }
                        else
                        {
                            hasGroup = Instance.permission.GroupHasPermission(target, permission);
                        }

                        string color = "0 0 0 0";
                        if (hasUser || hasGroup)
                            color = hasGroup ? groupColor : userColor;
                        button.AddPanel(color: color, anchorMin: "0 0", anchorMax: "1 0", offsetMin: "0 0", offsetMax: "0 2", name: $"{permission} - COLOR");
                        button.Components.Get<CuiButtonComponent>().Command = $"adminmenu permission.action {(isTargetUser && !hasUser || !isTargetUser && !hasGroup ? "grant" : "revoke")} {permission}";
                    }

                    return root;
                }).ToList(), ref x, ref y, out totalY, "PermissionsBranch");
            }
        }

        public class PlayerListContent : Content
        {
            private static readonly Label SEARCH_LABEL = new Label("Search..");
            private static List<Timer> sequentialLoad = new List<Timer>();
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["playerlist.executeCommand"] = "adminmenu userinfo.open";
                if (!userData.ContainsKey("playerlist.filter"))
                    userData["playerlist.filter"] = (Func<IPlayer, bool>)((IPlayer player) => true);
                userData["playerlist.searchQuery"] = string.Empty;
            }

            public override void RestoreUserData(Dictionary<string, object> userData)
            {
                userData["playerlist.filter"] = (Func<IPlayer, bool>)((IPlayer player) => true);
                StopSequentialLoad();
            }

            public void StopSequentialLoad()
            {
                foreach (Timer timer in sequentialLoad)
                    if (!timer.Destroyed)
                        timer.Destroy();
                sequentialLoad.Clear();
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                Func<IPlayer, bool> filter = (Func<IPlayer, bool>)userData["playerlist.filter"];
                string searchQuery = (string)userData["playerlist.searchQuery"];
                int columns = 4;
                int rows = 16;
                int playersPerPage = columns * rows;
                List<IPlayer> players = Instance.covalence.Players.All.Where(filter).OrderBy(p => p.Name).ToList();
                if (!string.IsNullOrEmpty(searchQuery))
                    players = players.Where(player => player.Name.ToLower().Contains(searchQuery.ToLower()) || player.Id == searchQuery).ToList();
                int pageCount = Mathf.CeilToInt(players.Count / (float)playersPerPage);
                int playersCount = players.Count;
                var layout = root.AddPanel(color: "0 0 0 0", anchorMin: "0.01 0.04", anchorMax: "0.99 0.99", name: "AdminMenu_PlayerList_Layout").AddDestroySelfAttribute();
                layout.Components.AddScrollView(vertical: true, scrollSensitivity: 20, anchorMin: $"0 -{pageCount - 1}", anchorMax: $"1 1");
                float width = 1f / columns;
                float height = 1f / rows;
                var bottom = root.AddContainer(anchorMin: "0.02 0", anchorMax: "0.98 0.035", name: "AdminMenu_PlayerList_Bottom").AddDestroySelfAttribute();
                var searchPanel = bottom.AddButton(command: "adminmenu playerlist.opensearch", color: "0.15 0.15 0.15 1", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "0 0", offsetMax: "200 0", name: "Search");
                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "-2 0", offsetMax: "0 0");
                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "1 0", anchorMax: "1 1", offsetMin: "0 0", offsetMax: "2 0");
                if (string.IsNullOrEmpty(searchQuery))
                {
                    searchPanel.AddText(text: SEARCH_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Placeholder");
                }
                else
                {
                    searchPanel.AddInputfield(command: "adminmenu playerlist.search.input", text: searchQuery, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Inputfield");
                }

                StopSequentialLoad();
                for (int i = 0; i < pageCount; i++)
                {
                    CUI.Root layoutRoot = new CUI.Root(layout.Name);
                    var screenContainer = layoutRoot.AddContainer(anchorMin: $"0 {(pageCount - (i + 1)) / (float)pageCount}", anchorMax: $"1 {(pageCount - i) / (float)pageCount}", name: $"{layoutRoot.Name}_Screen_{i}").AddDestroySelfAttribute();
                    for (int a = 0; a < rows; a++)
                    {
                        for (int b = 0; b < columns; b++)
                        {
                            int index = i * playersPerPage + a * columns + b;
                            IPlayer player = players.ElementAtOrDefault(index);
                            if (player == null)
                                break;
                            var container = screenContainer.AddContainer(anchorMin: $"{b * width} {1 - (a + 1) * height}", anchorMax: $"{(b + 1) * width} {1 - a * height}");
                            var button = container.AddButton(command: $"{userData["playerlist.executeCommand"]} {player.Id}", color: "0.25 0.25 0.25 0.6", anchorMin: "0.5 0.5", anchorMax: "0.5 0.5", offsetMin: $"-73 -20", offsetMax: $"73 20");
                            string frameColor = null;
                            var serverUser = ServerUsers.Get(ulong.Parse(player.Id));
                            if (serverUser != null)
                            {
                                switch (serverUser.group)
                                {
                                    case ServerUsers.UserGroup.Owner:
                                        frameColor = "0.8 0.2 0.2 0.6";
                                        break;
                                    case ServerUsers.UserGroup.Moderator:
                                        frameColor = "1 0.6 0.3 0.6";
                                        break;
                                    case ServerUsers.UserGroup.Banned:
                                        frameColor = "0 0 0 1";
                                        break;
                                }
                            }

                            if (frameColor != null)
                            {
                                button.AddPanel(color: frameColor, anchorMin: "0 0", anchorMax: "0 1", offsetMin: "0 0", offsetMax: "1.5 0");
                                button.AddPanel(color: frameColor, anchorMin: "1 0", anchorMax: "1 1", offsetMin: "-1.5 0", offsetMax: "0 0");
                                button.AddPanel(color: frameColor, anchorMin: "0 0", anchorMax: "1 0", offsetMin: "0 0", offsetMax: "0 1.5");
                                button.AddPanel(color: frameColor, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "0 -1.5", offsetMax: "0 0");
                            }

                            button.AddText(text: player.Name, fontSize: 12, align: TextAnchor.MiddleCenter, offsetMin: "6 0", offsetMax: "-6 0");
                        }
                    }

                    sequentialLoad.Add(Instance.timer.Once(i * 0.1f, () => layoutRoot.Render(connectionData.connection)));
                }
            }

            public void OpenSearch(Connection connection)
            {
                CUI.Root root = new CUI.Root("Search");
                root.AddInputfield(command: "adminmenu playerlist.search.input", text: "", align: TextAnchor.MiddleLeft, autoFocus: true, offsetMin: "10 0", name: "Search_Inputfield").DestroyUi = "Search_Placeholder";
                root.Render(connection);
            }
        }

        public class PluginManagerContent : Content
        {
            private static readonly MethodInfo GetPluginMethod = typeof(PluginLoader).GetMethod("GetPlugin", (BindingFlags)(-1));
            public static readonly Button COMMANDS_BUTTON = new Button("Commands", "pluginmanager.check_commands")
            {
                Permission = "pluginmanager.check_commands"
            };
            public static readonly Button PERMISSIONS_BUTTON = new Button("Permissions", "pluginmanager.check_permissions")
            {
                Permission = "permissionmanager"
            };
            public static readonly Button LOAD_BUTTON = new Button("Load", "pluginmanager.load")
            {
                Permission = "pluginmanager.load",
                Style = new ButtonStyle
                {
                    BackgroundColor = "0.451 0.737 0.349 1"
                }
            };
            public static readonly Button UNLOAD_BUTTON = new Button("Unload", "pluginmanager.unload")
            {
                Permission = "pluginmanager.unload",
                Style = new ButtonStyle
                {
                    BackgroundColor = "0.737 0.353 0.349 1"
                }
            };
            public static readonly Button RELOAD_BUTTON = new Button("Reload", "pluginmanager.reload")
            {
                Permission = "pluginmanager.reload",
                Style = new ButtonStyle
                {
                    BackgroundColor = "0.455 0.667 0.737 1"
                }
            };
            public static readonly Button RELOADALL_BUTTON = new Button("Reload All", "pluginmanager.reload_all")
            {
                Permission = "pluginmanager.reload_all",
                Style = new ButtonStyle
                {
                    BackgroundColor = "0.455 0.667 0.737 1"
                }
            };
            public static readonly Button FAVORITE_BUTTON = new Button(null, "pluginmanager.favorite")
            {
                Permission = "pluginmanager.favorite"
            };
            public static readonly Button UNFAVORITE_BUTTON = new Button(null, "pluginmanager.unfavorite")
            {
                Permission = "pluginmanager.favorite",
                Style = new ButtonStyle
                {
                    BackgroundColor = ButtonStyle.Default.BackgroundColor,
                    TextColor = "1 0.85 0.5 1"
                }
            };
            private static readonly Label SEARCH_LABEL = new Label("Search..");
            private static List<Timer> sequentialLoad = new List<Timer>();
            private const string BAR_BACKGROUND_COLOR = "0.25 0.25 0.25 0.4";
            private const string BAR_BACKGROUND_COLOR_LAST = "0.1 0.1 0.1 1";
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["pluginmanager.searchQuery"] = string.Empty;
                userData["pluginmanager.array"] = new string[0];
            }

            public override void RestoreUserData(Dictionary<string, object> userData)
            {
                base.RestoreUserData(userData);
                StopSequentialLoad();
            }

            public void StopSequentialLoad()
            {
                foreach (Timer timer in sequentialLoad)
                    if (!timer.Destroyed)
                        timer.Destroy();
                sequentialLoad.Clear();
            }

            public void UpdateForPlugin(ConnectionData connectionData, string pluginName)
            {
                CUI.Root root = new CUI.Root($"{pluginName} Bar");
                if (connectionData.userData.TryGetValue("pluginmanager.array", out object @obj) && connectionData.userData.TryGetValue("pluginmanager.lastusedplugin", out object @obj2))
                {
                    string[] array = (string[])@obj;
                    string lastusedplugin = (string)@obj2;
                    foreach (string pluginName2 in array)
                    {
                        root.AddUpdateElement($"{pluginName2} Bar").Components.AddImage(color: (pluginName2 == lastusedplugin) ? BAR_BACKGROUND_COLOR_LAST : BAR_BACKGROUND_COLOR, sprite: "assets/content/ui/ui.background.rounded.png", imageType: UnityEngine.UI.Image.Type.Tiled);
                    }
                }

                AddButtons(root, pluginName, Core.Interface.Oxide.RootPluginManager.GetPlugin(pluginName), connectionData, out _);
                root.Render(connectionData.connection);
            }

            private void AddButtons(CUI.Element root, string pluginName, Plugin plugin, ConnectionData connectionData, out int count)
            {
                var buttonsContainer = root.AddContainer(name: $"{pluginName} Buttons").AddDestroySelfAttribute();
                ;
                List<Button> buttonsToRender = new List<Button>();
                if (plugin != null && plugin.IsLoaded)
                {
                    if (UNLOAD_BUTTON.UserHasPermission(connectionData.connection))
                        buttonsToRender.Add(UNLOAD_BUTTON);
                    if (RELOAD_BUTTON.UserHasPermission(connectionData.connection))
                        buttonsToRender.Add(RELOAD_BUTTON);
                    if (COMMANDS_BUTTON.UserHasPermission(connectionData.connection) && (Instance.GetConsoleCommands(pluginName).Length > 0 || Instance.GetChatCommands(pluginName).Length > 0))
                        buttonsToRender.Add(COMMANDS_BUTTON);
                    if (PERMISSIONS_BUTTON.UserHasPermission(connectionData.connection) && Instance.GetPermissions(pluginName)?.Length > 0)
                        buttonsToRender.Add(PERMISSIONS_BUTTON);
                }
                else
                {
                    if (LOAD_BUTTON.UserHasPermission(connectionData.connection))
                        buttonsToRender.Add(LOAD_BUTTON);
                }

                for (int k = 0; k < buttonsToRender.Count; k++)
                {
                    Button button = buttonsToRender[k];
                    buttonsContainer.AddButton(command: $"adminmenu {button.Command} {pluginName}", sprite: "assets/content/ui/ui.background.rounded.png", imageType: UnityEngine.UI.Image.Type.Tiled, color: button.Style.BackgroundColor, anchorMin: "0.99 0.1", anchorMax: "0.99 0.9", offsetMin: $"-{(k + 1) * 100 + k * 5} 0", offsetMax: $"-{k * 100 + k * 5} 0").AddText(text: button.Label.Localize(connectionData.connection), align: TextAnchor.MiddleCenter, offsetMin: "10 0", offsetMax: "-10 0");
                }

                count = buttonsToRender.Count;
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                const int rows = 15;
                const float width = 1f / rows;
                string searchQuery = (string)userData["pluginmanager.searchQuery"];
                string lastUsedPluginName = null;
                if (userData.TryGetValue("pluginmanager.lastusedplugin", out object obj))
                    lastUsedPluginName = obj as string;
                var container = root.AddPanel(color: "0 0 0 0", anchorMin: "0.01 0.045", anchorMax: "0.99 0.99", name: "AdminMenu_PluginManager").AddDestroySelfAttribute();
                IEnumerable<FileInfo> enumerable =
                    from f in new DirectoryInfo(Core.Interface.Oxide.PluginDirectory).GetFiles("*.cs")
                    where (f.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden
                    select f;
                List<string> pluginNames = enumerable.Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.FullName)).Where(name => name.Contains(searchQuery, System.Globalization.CompareOptions.IgnoreCase)).OrderByDescending(name => config.FavoritePlugins.Contains(name)).ToList();
                int pageCount = Mathf.CeilToInt(pluginNames.Count / (float)rows);
                container.Components.AddScrollView(vertical: true, scrollSensitivity: 20, anchorMin: $"0 -{pageCount - 1}", anchorMax: $"1 1");
                var bottom = root.AddContainer(anchorMin: "0.01 0", anchorMax: "0.99 0.035", name: "AdminMenu_PluginManager_Bottom").AddDestroySelfAttribute();
                if (RELOADALL_BUTTON.UserHasPermission(connectionData.connection))
                {
                    bottom.AddButton(command: $"adminmenu {RELOADALL_BUTTON.Command}", sprite: "assets/content/ui/ui.background.rounded.png", imageType: UnityEngine.UI.Image.Type.Tiled, color: RELOADALL_BUTTON.Style.BackgroundColor, anchorMin: "1 0", anchorMax: "1 1", offsetMin: $"-100 0", offsetMax: $"0 0").AddText(text: RELOADALL_BUTTON.Label.Localize(connectionData.connection), align: TextAnchor.MiddleCenter, offsetMin: "10 0", offsetMax: "-10 0");
                }

                var searchPanel = bottom.AddButton(command: "adminmenu pluginmanager.opensearch", color: "0.15 0.15 0.15 0.7", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "0 0", offsetMax: "250 0", name: "Search");
                if (string.IsNullOrEmpty(searchQuery))
                {
                    searchPanel.AddText(text: SEARCH_LABEL.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Placeholder");
                }
                else
                {
                    searchPanel.AddInputfield(command: "adminmenu pluginmanager.search.input", text: searchQuery, align: TextAnchor.MiddleLeft, offsetMin: "10 0", name: "Search_Inputfield");
                }

                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "0 0", anchorMax: "0 1", offsetMin: "-2 0", offsetMax: "0 0");
                searchPanel.AddPanel(color: "0.9 0.4 0.4 0.5", anchorMin: "1 0", anchorMax: "1 1", offsetMin: "0 0", offsetMax: "2 0");
                StopSequentialLoad();
                for (int i = 0; i < pageCount; i++)
                {
                    CUI.Root layoutRoot = new CUI.Root(container.Name);
                    var screenContainer = layoutRoot.AddContainer(anchorMin: $"0 {(pageCount - (i + 1)) / (float)pageCount}", anchorMax: $"1 {(pageCount - i) / (float)pageCount}", name: $"{layoutRoot.Name}_Screen_{i}").AddDestroySelfAttribute();
                    for (int j = 0; j < rows; j++)
                    {
                        string pluginName = pluginNames.ElementAtOrDefault(i * rows + j);
                        if (pluginName == null)
                            break;
                        Plugin plugin = Core.Interface.Oxide.RootPluginManager.GetPlugin(pluginName);
                        var convarContainer = screenContainer.AddContainer(anchorMin: $"0 {1f - width * (j + 1)}", anchorMax: $"1 {1f - width * j}");
                        var panel = convarContainer.AddPanel(color: (pluginName == lastUsedPluginName ? BAR_BACKGROUND_COLOR_LAST : BAR_BACKGROUND_COLOR), sprite: "assets/content/ui/ui.background.rounded.png", imageType: UnityEngine.UI.Image.Type.Tiled, anchorMin: $"0 0.045", anchorMax: $"1 0.955", name: $"{pluginName} Bar");
                        AddButtons(panel, pluginName, plugin, connectionData, out int rightButtonCount);
                        Button favoriteButton = null;
                        string favoriteButtonSprite = null;
                        if (!config.FavoritePlugins.Contains(pluginName))
                        {
                            if (FAVORITE_BUTTON.UserHasPermission(connectionData.connection))
                            {
                                favoriteButton = FAVORITE_BUTTON;
                                favoriteButtonSprite = "assets/icons/favourite_inactive.png";
                            }
                        }
                        else
                        {
                            if (UNFAVORITE_BUTTON.UserHasPermission(connectionData.connection))
                            {
                                favoriteButton = UNFAVORITE_BUTTON;
                                favoriteButtonSprite = "assets/icons/favourite_active.png";
                            }
                        }

                        if (favoriteButton != null)
                        {
                            string command;
                            if (favoriteButton.UserHasPermission(connectionData.connection))
                                command = $"adminmenu {favoriteButton.Command} {pluginName}";
                            else
                                command = null;
                            panel.AddButton(command: command, sprite: "assets/content/ui/ui.background.rounded.png", imageType: UnityEngine.UI.Image.Type.Tiled, color: favoriteButton.Style.BackgroundColor, anchorMin: "0.005 0.1", anchorMax: "0.025 0.9").AddPanel(sprite: favoriteButtonSprite, material: CUI.Defaults.IconMaterial, color: favoriteButton.Style.TextColor, anchorMin: "0.5 0.5", anchorMax: "0.5 0.5", offsetMin: "-6 -6", offsetMax: "6 6");
                        }

                        panel.AddText(text: $"<b>{(plugin != null ? $"{plugin.Name} v{plugin.Version} by {plugin.Author}" : pluginName)}</b>{(plugin != null && !string.IsNullOrEmpty(plugin.Description) ? $"\n<color=#7A7A7A><size=11>{plugin.Description}</size></color>" : string.Empty)}", align: TextAnchor.MiddleLeft, overflow: VerticalWrapMode.Truncate, anchorMin: $"0.035 0.1", anchorMax: "0.99 0.9", offsetMax: $"-{rightButtonCount * 100 + (rightButtonCount - 1) * 5} 0");
                    }

                    sequentialLoad.Add(Instance.timer.Once(i * 0.1f, () => layoutRoot.Render(connectionData.connection)));
                    userData["pluginmanager.array"] = pluginNames.ToArray();
                }
            }

            public void OpenSearch(Connection connection)
            {
                CUI.Root root = new CUI.Root("Search");
                root.AddInputfield(command: "adminmenu pluginmanager.search.input", text: "", align: TextAnchor.MiddleLeft, autoFocus: true, offsetMin: "10 0", name: "Search_Inputfield").DestroyUi = "Search_Placeholder";
                root.Render(connection);
            }
        }

        public class QuickMenuContent : Content
        {
            public ButtonGrid<Button> buttonGrid;
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                const float offset = 10f;
                if (this.buttonGrid == null)
                    return;
                string connectionUserId = ((ulong)userData["userId"]).ToString();
                var container = root.AddPanel(color: "0 0 0 0", anchorMin: "0.01 0.01", anchorMax: "0.99 0.99", name: "AdminMenu_QuickMenu").AddDestroySelfAttribute();
                var buttons = this.buttonGrid.GetAllowedButtons(connectionUserId);
                container.Components.AddScrollView(vertical: true, scrollSensitivity: 10, verticalScrollbar: new Game.Rust.Cui.CuiScrollbar() { Size = 20, HandleColor = ButtonStyle.Default.BackgroundColor, HandleSprite = "assets/content/ui/ui.background.rounded.png", HighlightColor = ButtonStyle.Default.BackgroundColor, TrackColor = "0 0 0 0.2", TrackSprite = "assets/content/ui/ui.background.rounded.png", AutoHide = true }, offsetMin: $"0 -{(buttons.Max(b => b.row) - 15) * (35 + offset)}");
                foreach (var item in buttons)
                {
                    Button button = item.button;
                    if (button == null)
                        continue;
                    string backgroundColor;
                    if (button is ToggleButton toggleButton)
                    {
                        if (toggleButton.GetState(connectionData) == Button.State.Toggled)
                            backgroundColor = toggleButton.Style.ActiveBackgroundColor ?? ButtonStyle.Default.ActiveBackgroundColor;
                        else
                            backgroundColor = button.Style.BackgroundColor;
                    }
                    else
                    {
                        backgroundColor = button.Style.BackgroundColor;
                    }

                    container.AddButton(color: backgroundColor, command: $"adminmenu {button.Command} {string.Join(" ", button.Args)}", anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"{item.column * 150 + item.column * offset} -{(item.row + 1) * 35 + item.row * offset}", offsetMax: $"{(item.column + 1) * 150 + item.column * offset} -{item.row * 35 + item.row * offset}").AddText(text: button.Label.Localize(connectionUserId), color: button.Style.TextColor, font: CUI.Font.RobotoMonoRegular, fontSize: 12, align: TextAnchor.MiddleCenter);
                }
            }
        }

        public class TextContent : Content
        {
            public string text = string.Empty;
            public CUI.Font font = CUI.Font.RobotoCondensedRegular;
            public TextAnchor align = TextAnchor.UpperLeft;
            public Vector2 margin = new Vector2(10, 10);
            public int fontSize = 18;
            public bool allowCopy = false;
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                if (allowCopy)
                {
                    int textHashCode = text.GetHashCode();
                    root.AddInputfield(command: null, text: null, font: font, fontSize: fontSize, align: align, lineType: UnityEngine.UI.InputField.LineType.MultiLineNewline, @readonly: true, anchorMin: "0 0", anchorMax: "1 1", offsetMin: $"{margin.x} {margin.y}", offsetMax: $"-{margin.x} -{margin.y}", name: textHashCode.ToString()).AddDestroySelfAttribute();
                    root.Add(new CUI.Element { Components = { new CuiInputFieldComponent { Text = text } }, Name = textHashCode.ToString(), Update = true });
                }
                else
                {
                    root.AddText(text: text, font: font, fontSize: fontSize, align: align, offsetMin: $"{margin.x} {margin.y}", offsetMax: $"-{margin.x} -{margin.y}").AddDestroySelfAttribute();
                }
            }
        }

        public class UserInfoContent : Content
        {
            private static readonly Label HEALTH_LABEL = new Label("Health: {0}/{1}");
            private static readonly Label GRID_LABEL = new Label("Grid: {0}");
            private static readonly Label CONNECTIONTIME_LABEL = new Label("CTIME: {0:D2}h {1:D2}m {2:D2}s");
            private static readonly Label BALANCE_LABEL = new Label("Balance: {0}$");
            private static readonly Label CLAN_LABEL = new Label("Clan: {0}");
            private static readonly Label UNKNOWN_LABEL = new Label("Unknown");
            private static readonly Label NOTCONNECTED_LABEL = new Label("Not connected");
            private static readonly Label STEAMINFO_LOCATION_LABEL = new Label("[Location: {0}]");
            private static readonly Label STEAMINFO_REGISTRATION_LABEL = new Label("[Registration: {0}]");
            private static readonly Label STEAMINFO_HOURSINRUST_LABEL = new Label("[{0}h in Rust]");
            private static readonly KickPopup kickPopup = new KickPopup();
            private static readonly BanPopup banPopup = new BanPopup();
            public ButtonGrid<Button> buttonGrid;
            public static string GetDisplayName(IPlayer player)
            {
                string name = player.Name;
                var serverUser = ServerUsers.Get(ulong.Parse(player.Id));
                if (serverUser != null)
                {
                    switch (serverUser.group)
                    {
                        case ServerUsers.UserGroup.Owner:
                            name = "[Admin] " + name;
                            break;
                        case ServerUsers.UserGroup.Moderator:
                            name = "[Moderator] " + name;
                            break;
                        case ServerUsers.UserGroup.Banned:
                            name = "[Banned] " + name;
                            break;
                    }
                }

                return name;
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                if (buttonGrid == null)
                    return;
                string connectionUserId = ((ulong)userData["userId"]).ToString();
                string userid = userData["userinfo.userid"].ToString();
                if (userid == null)
                    return;
                IPlayer player = Instance.covalence.Players.FindPlayerById(userid);
                if (player == null)
                    return;
                ulong playerIdUlong = ulong.Parse(player.Id);
                SteamInfo steamInfo = null;
                if (playerIdUlong.IsSteamId())
                    Instance.RequestSteamInfo(playerIdUlong, out steamInfo, new Action<SteamInfo>((steamInfo) => Render(connectionData)));
                BasePlayer playerInWorld = BasePlayer.FindAwakeOrSleeping(userid);
                Vector3 position = Vector3.zero;
                float health = 0f;
                float maxHealth = 0f;
                bool isMuted = false;
                if (playerInWorld != null)
                {
                    position = playerInWorld.transform.position;
                    health = playerInWorld.health;
                    maxHealth = playerInWorld.MaxHealth();
                    isMuted = playerInWorld.HasPlayerFlag(BasePlayer.PlayerFlags.ChatMute);
                }

                var container = root.AddContainer(name: "AdminMenu_UserInfo_Info").AddDestroySelfAttribute();
                var basic_info_container = container.AddContainer(anchorMin: "0 1", anchorMax: "1 1", offsetMin: "30 -180", offsetMax: "-30 -30");
                var avatarContainer = basic_info_container.AddContainer(anchorMin: "0 0", anchorMax: "0 0", offsetMin: "0 0", offsetMax: "150 150", name: "AdminMenu_UserInfo_AvatarContainer");
                string avatarId = string.Format("adm_{0}_H", player.Id);
                bool hasAvatar = Instance.ImageLibrary?.Call<bool>("HasImage", avatarId, 0UL) == true;
                if (hasAvatar)
                {
                    string avatar = Instance.ImageLibrary?.Call<string>("GetImage", avatarId, 0UL);
                    if (avatar != null)
                    {
                        avatarContainer.AddImage(content: avatar, color: "1 1 1 1", name: "AdminMenu_UserInfo_Avatar").AddDestroySelfAttribute();
                    }
                }
                else
                {
                    avatarContainer.AddPanel(color: "0.3 0.3 0.3 0.5", name: "AdminMenu_UserInfo_Avatar").AddDestroySelfAttribute().AddOutlinedText(text: "NO\nAVATAR", outlineWidth: 2, outlineColor: "0 0 0 0.5", font: CUI.Font.RobotoCondensedBold, fontSize: 25, align: TextAnchor.MiddleCenter);
                    if (steamInfo != null && Instance.ImageLibrary != null)
                    {
                        string hAvatar = steamInfo.Avatars[2];
                        if (!string.IsNullOrEmpty(hAvatar))
                            Instance.ImageLibrary.Call("AddImage", hAvatar, avatarId, 0UL, new Action(() => Render(connectionData)));
                    }
                }

                string name = GetDisplayName(player);
                var text_info_container = basic_info_container.AddContainer(anchorMin: "0 0", anchorMax: "1 1", offsetMin: "180 0");
                text_info_container.AddPanel(color: "1 1 1 1", sprite: $"assets/icons/flags/{Instance.lang.GetLanguage(player.Id)}.png", material: "assets/content/ui/namefontmaterial.mat", anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"0 -30", offsetMax: $"35 0");
                text_info_container.AddInputfield(command: null, text: name, font: CUI.Font.RobotoCondensedBold, fontSize: 24, align: TextAnchor.MiddleLeft, @readonly: true, inputType: CUI.InputType.HudMenuInput, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "40 -30", offsetMax: "0 0");
                text_info_container.AddText(text: "Steam ID:", font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleLeft, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "0 -60", offsetMax: "0 -35");
                text_info_container.AddInputfield(command: null, color: "1 1 1 1", text: player.Id, font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleLeft, @readonly: true, inputType: CUI.InputType.HudMenuInput, anchorMin: "0 1", anchorMax: "1 1", offsetMin: "90 -60", offsetMax: "0 -35");
                List<string> leftColumn = new List<string>
                {
                    string.Format(HEALTH_LABEL.Localize(connectionUserId), Mathf.Round(health), Mathf.Round(maxHealth)),
                    string.Format(GRID_LABEL.Localize(connectionUserId), (position != Vector3.zero ? MapHelper.PositionToGrid(position) : UNKNOWN_LABEL.Localize(connectionUserId))),
                    string.Format("{0} (P: {1})", player.Address != null ? (Instance.UserHasPermission(connectionUserId, PERMISSION_USERINFO_IP) ? player.Address : "***.***.*.**") : NOTCONNECTED_LABEL.Localize(connectionUserId), (player.IsConnected ? player.Ping : -1))
                };
                List<string> rightColumn = new List<string>();
                if (Instance.Economics || Instance.ServerRewards)
                {
                    double? balance = null;
                    if (Instance.Economics)
                    {
                        balance = Instance.Economics.Call<double>("Balance", new object[] { playerIdUlong });
                    }
                    else if (Instance.ServerRewards)
                    {
                        object points = Instance.ServerRewards.Call("CheckPoints", new object[] { playerIdUlong });
                        if (points is int)
                            balance = (int)points;
                    }

                    if (balance.HasValue)
                        rightColumn.Add(string.Format(BALANCE_LABEL.Localize(connectionUserId), balance.Value));
                }

                if (Instance.Clans != null)
                {
                    string clanTag = Instance.Clans.Call<string>("GetClanOf", userid);
                    if (!string.IsNullOrEmpty(clanTag))
                        rightColumn.Add(string.Format(CLAN_LABEL.Localize(connectionUserId), clanTag));
                }

                if (playerInWorld != null && playerInWorld.IsConnected)
                {
                    TimeSpan timeSpan = TimeSpan.FromSeconds(playerInWorld.Connection.GetSecondsConnected());
                    rightColumn.Add(string.Format(CONNECTIONTIME_LABEL.Localize(connectionUserId), timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
                }

                for (int i = 0; i < leftColumn.Count; i++)
                {
                    text_info_container.AddText(text: leftColumn[i], font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleLeft, overflow: VerticalWrapMode.Truncate, anchorMin: "0 1", anchorMax: "0.49 1", offsetMin: $"0 -{65 + (i + 1) * 25}", offsetMax: $"0 -{65 + i * 25}");
                }

                for (int i = 0; i < rightColumn.Count; i++)
                {
                    text_info_container.AddText(text: rightColumn[i], font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleLeft, overflow: VerticalWrapMode.Truncate, anchorMin: "0.51 1", anchorMax: "1 1", offsetMin: $"0 -{65 + (i + 1) * 25}", offsetMax: $"0 -{65 + i * 25}");
                }

                if (steamInfo != null)
                {
                    List<string> info = new List<string>();
                    if (Instance.UserHasPermission(connectionUserId, PERMISSION_USERINFO_STEAMINFO))
                    {
                        if (!string.IsNullOrEmpty(steamInfo.Location))
                            info.Add(string.Format(STEAMINFO_LOCATION_LABEL.Localize(connectionUserId), steamInfo.Location));
                        if (!string.IsNullOrEmpty(steamInfo.RegistrationDate))
                            info.Add(string.Format(STEAMINFO_REGISTRATION_LABEL.Localize(connectionUserId), steamInfo.RegistrationDate));
                        if (!string.IsNullOrEmpty(steamInfo.RustHours))
                            info.Add(string.Format(STEAMINFO_HOURSINRUST_LABEL.Localize(connectionUserId), steamInfo.RustHours));
                    }

                    if (info.Count > 0)
                    {
                        basic_info_container.AddButton(command: "adminmenu userinfo.action steaminfo_update", color: "0 0 0 0", anchorMin: "0 0", anchorMax: "1 0", offsetMin: "0 -35", offsetMax: "0 -10").AddText(text: string.Format("<color=#4755BD>[</color><color=#4859BD>S</color><color=#495DBE>t</color><color=#4A61BE>e</color><color=#4B65BF>a</color><color=#4C69BF>m</color><color=#4E6DC0>I</color><color=#4F71C0>n</color><color=#5075C1>f</color><color=#5179C1>o</color><color=#527DC2>]</color><color=#5381C2>:</color> {0}", string.Join(" ", info)), color: "0.8 0.8 0.8 1", fontSize: 14, align: TextAnchor.MiddleCenter);
                    }
                }

                const float offset = 10f;
                int lastRow = buttonGrid.Max(b => b.row);
                var actionButtonsContainer = container.AddPanel(color: "0 0 0 0", anchorMin: "0 0", anchorMax: "1 1", offsetMin: "30 10", offsetMax: "-30 -230");
                actionButtonsContainer.Components.AddScrollView(vertical: true, scrollSensitivity: 10, verticalScrollbar: new Game.Rust.Cui.CuiScrollbar() { Size = 20, HandleColor = ButtonStyle.Default.BackgroundColor, HandleSprite = "assets/content/ui/ui.background.rounded.png", HighlightColor = ButtonStyle.Default.BackgroundColor, TrackColor = "0 0 0 0.2", TrackSprite = "assets/content/ui/ui.background.rounded.png", AutoHide = true }, offsetMin: $"0 -{(lastRow - 10) * (35 + offset)}");
                foreach (var item in buttonGrid.GetAllowedButtons(connectionUserId))
                {
                    Button button = item.button;
                    if (button == null)
                        continue;
                    string backgroundColor;
                    if (button is ToggleButton)
                    {
                        ToggleButton toggleButton = button as ToggleButton;
                        backgroundColor = toggleButton.GetState(connectionData) == Button.State.Toggled ? toggleButton.Style.ActiveBackgroundColor : button.Style.BackgroundColor;
                    }
                    else
                    {
                        backgroundColor = button.Style.BackgroundColor;
                    }

                    actionButtonsContainer.AddButton(color: backgroundColor, command: $"adminmenu {button.Command} {string.Join(" ", button.Args)}", anchorMin: "0 1", anchorMax: "0 1", offsetMin: $"{item.column * 150 + item.column * offset} -{(item.row + 1) * 35 + item.row * offset}", offsetMax: $"{(item.column + 1) * 150 + item.column * offset} -{item.row * 35 + item.row * offset}").AddText(text: button.Label.Localize(connectionUserId), color: button.Style.TextColor, font: CUI.Font.RobotoMonoRegular, fontSize: 12, align: TextAnchor.MiddleCenter);
                }
            }

            public void ShowKickPopup(ConnectionData connectionData)
            {
                CUI.Root root = new CUI.Root("AdminMenu_Panel_TempContent");
                KickPopup.LoadDefaultUserData(connectionData.userData);
                kickPopup.AddUI(root, connectionData, connectionData.userData);
                root.Render(connectionData.connection);
            }

            public void ShowBanPopup(ConnectionData connectionData)
            {
                CUI.Root root = new CUI.Root("AdminMenu_Panel_TempContent");
                BanPopup.LoadDefaultUserData(connectionData.userData);
                banPopup.AddUI(root, connectionData, connectionData.userData);
                root.Render(connectionData.connection);
            }
        }

        public class Theme : Dictionary<string, Color>
        {
            public static class KeyCollection
            {
                public static string PANEL_SIDEBAR_BACKGROUND = nameof(PANEL_SIDEBAR_BACKGROUND);
                public static string PANEL_SIDEBAR_SELECTED = nameof(PANEL_SIDEBAR_SELECTED);
                public static string POPUP_HEADER = nameof(POPUP_HEADER);
                public static string POPUP_HEADER_TEXT = nameof(POPUP_HEADER_TEXT);
            }

            public string GetColorString(string key)
            {
                Color color = this[key];
                return string.Format("{0} {1} {2} {3}", color.r, color.g, color.b, color.a);
            }
        }

        public class ThemeConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                Theme theme = (Theme)value;
                writer.WriteStartObject();
                foreach (var pair in theme)
                {
                    Color color = pair.Value;
                    writer.WritePropertyName(pair.Key);
                    writer.WriteValue(string.Format("{0} {1} {2} {3}", color.r, color.g, color.b, color.a));
                }

                writer.WriteEndObject();
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(Theme);
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return default(Dictionary<string, Color>);
                JObject jobject = JObject.Load(reader);
                Theme result = new Theme();
                foreach (var pair in jobject)
                    result.Add(pair.Key, ColorEx.Parse(pair.Value.ToString()));
                return result;
            }

            public override bool CanRead
            {
                get
                {
                    return true;
                }
            }
        }

        public static class Themes
        {
            public static Theme dark;
            public static Theme current;
            public static Theme CurrentTheme
            {
                get => current;
                set
                {
                    foreach (var pair in value)
                        current[pair.Key] = pair.Value;
                }
            }

            static Themes()
            {
                dark = new Theme();
                dark[Theme.KeyCollection.PANEL_SIDEBAR_BACKGROUND] = new Color(0.784f, 0.329f, 0.247f, 0.427f);
                dark[Theme.KeyCollection.PANEL_SIDEBAR_SELECTED] = new Color(0f, 0f, 0f, 0.6f);
                dark[Theme.KeyCollection.POPUP_HEADER] = new Color(0.1f, 0.1f, 0.1f, 1);
                dark[Theme.KeyCollection.POPUP_HEADER_TEXT] = new Color(0.85f, 0.85f, 0.85f, 0.8f);
                current = new Theme();
                foreach (var pair in dark)
                    current[pair.Key] = pair.Value;
            }
        }

        public class BanPopup : BasePopup
        {
            private static readonly Label REASON_LABEL = new Label("REASON");
            private static readonly Button BAN_BUTTON = new Button("BAN", "userinfo.action", "ban")
            {
                Permission = "userinfo.ban"
            };
            public BanPopup()
            {
                Width = 400;
                Height = 180;
                Modules.Add(new HeaderModule("BAN", 30) { TextColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER_TEXT], BackgroundColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER] });
            }

            public static void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["userinfo[popup:ban].reason"] = "No reason given";
                userData["userinfo[popup:ban].weeks"] = 0;
                userData["userinfo[popup:ban].days"] = 0;
                userData["userinfo[popup:ban].hours"] = 0;
                userData["userinfo[popup:ban].minutes"] = 0;
            }

            public override CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var panel = base.AddUI(parent, connectionData, userData);
                string reason = (string)userData["userinfo[popup:ban].reason"];
                int weeks = (int)userData["userinfo[popup:ban].weeks"];
                int days = (int)userData["userinfo[popup:ban].days"];
                int hours = (int)userData["userinfo[popup:ban].hours"];
                int minutes = (int)userData["userinfo[popup:ban].minutes"];
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-180 -90", offsetMax: "-130 -55").AddInputfield(command: "adminmenu userinfo[popup:ban] set_weeks", text: weeks.ToString(), font: CUI.Font.RobotoCondensedRegular, fontSize: 24, align: TextAnchor.MiddleCenter, charsLimit: 2, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddText(text: "W", color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 32, align: TextAnchor.MiddleLeft, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-125 -90", offsetMax: "-100 -55");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-90 -90", offsetMax: "-40 -55").AddInputfield(command: "adminmenu userinfo[popup:ban] set_days", text: days.ToString(), font: CUI.Font.RobotoCondensedRegular, fontSize: 24, align: TextAnchor.MiddleCenter, charsLimit: 2, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddText(text: "D", color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 32, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-30 -90", offsetMax: "-10 -55");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "0 -90", offsetMax: "50 -55").AddInputfield(command: "adminmenu userinfo[popup:ban] set_hours", text: hours.ToString(), font: CUI.Font.RobotoCondensedRegular, fontSize: 24, align: TextAnchor.MiddleCenter, charsLimit: 2, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddText(text: "H", color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 32, align: TextAnchor.MiddleLeft, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "60 -90", offsetMax: "85 -55");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "95 -90", offsetMax: "145 -55").AddInputfield(command: "adminmenu userinfo[popup:ban] set_minutes", text: minutes.ToString(), font: CUI.Font.RobotoCondensedRegular, fontSize: 24, align: TextAnchor.MiddleCenter, charsLimit: 2, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddText(text: "M", color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 32, align: TextAnchor.MiddleLeft, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "155 -90", offsetMax: "180 -55");
                panel.AddText(text: REASON_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 16, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -130", offsetMax: "-60 -105");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -130", offsetMax: "170 -105").AddInputfield(command: "adminmenu userinfo[popup:ban] set_reason", text: reason ?? string.Empty, font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddButton(color: "0.1 0.1 0.1 1", command: $"adminmenu {BAN_BUTTON.FullCommand}", anchorMin: "0.5 0", anchorMax: "0.5 0", offsetMin: "-80 10", offsetMax: "80 37").AddText(text: BAN_BUTTON.Label.Localize(connectionData.connection), color: "0.8 0.8 0.8 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter);
                return panel;
            }
        }

        public abstract class BasePopup : IUIModule
        {
            public float Width { get; set; } = 300;
            public float Height { get; set; } = 150;
            public Color Color { get; set; } = new Color(0.05f, 0.05f, 0.05f, 1);
            public List<IUIModule> Modules { get; set; } = new List<IUIModule>();
            public string CloseCommand { get; set; }

            public virtual CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                string typeName = GetType().Name;
                string elementName = $"AdminMenu_Popup_{typeName}";
                var container = parent.AddContainer(name: elementName).AddDestroySelfAttribute();
                container.AddButton(color: "0 0 0 0.8", command: CloseCommand ?? $"adminmenu popup {typeName} onClose", close: CloseCommand == null ? elementName : null, anchorMin: "0 0", anchorMax: "1 1");
                var panel = container.AddPanel(color: Extensions.ToCuiString(Color), anchorMin: "0.5 0.5", anchorMax: "0.5 0.5", offsetMin: $"-{Width / 2} -{Height / 2}", offsetMax: $"{Width / 2} {Height / 2}");
                foreach (IUIModule module in Modules)
                {
                    module.AddUI(panel, connectionData, userData);
                }

                return panel;
            }
        }

        public class CloneGroupPopup : BasePopup
        {
            private static readonly Label CGP_CLONE_LABEL = new Label("CLONE");
            private static readonly Label CGP_NAME_LABEL = new Label("NAME <color=#bb0000>*</color>");
            private static readonly Label CGP_TITLE_LABEL = new Label("TITLE   ");
            private static readonly Label CGP_CLONEUSERS_LABEL = new Label("CLONE USERS");
            public CloneGroupPopup()
            {
                Width = 400;
                Height = 210;
                CloseCommand = "adminmenu groupinfo[popup:clonegroup] close";
                Modules.Add(new HeaderModule("CLONE GROUP", 30) { TextColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER_TEXT], BackgroundColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER] });
            }

            public static void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["groupinfo[popup:clonegroup].name"] = null;
                userData["groupinfo[popup:clonegroup].title"] = null;
                userData["groupinfo[popup:clonegroup].cloneusers"] = false;
            }

            public override CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var panel = base.AddUI(parent, connectionData, userData);
                string name = (string)userData["groupinfo[popup:clonegroup].name"];
                string title = (string)userData["groupinfo[popup:clonegroup].title"];
                bool cloneUsers = (bool)userData["groupinfo[popup:clonegroup].cloneusers"];
                panel.AddText(text: CGP_NAME_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 16, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -80", offsetMax: "-60 -55");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -80", offsetMax: "170 -55").AddInputfield(command: "adminmenu groupinfo[popup:clonegroup] set_name", text: name ?? string.Empty, font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddText(text: CGP_TITLE_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 16, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -120", offsetMax: "-60 -95");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -120", offsetMax: "170 -95").AddInputfield(command: "adminmenu groupinfo[popup:clonegroup] set_title", text: title ?? string.Empty, font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddText(text: CGP_CLONEUSERS_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 16, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -160", offsetMax: "-60 -135");
                var cloneUsersCheckbox = panel.AddButton(command: "adminmenu groupinfo[popup:clonegroup] cloneusers_toggle", color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -160", offsetMax: "-15 -135");
                if (cloneUsers)
                {
                    cloneUsersCheckbox.AddPanel(color: "0.698 0.878 0.557 0.6", offsetMin: "4 4", offsetMax: "-4 -4").WithFade(0.2f);
                }

                panel.AddButton(color: "0.1 0.1 0.1 1", command: "adminmenu groupinfo[popup:clonegroup] clone", anchorMin: "0.5 0", anchorMax: "0.5 0", offsetMin: "-80 10", offsetMax: "80 37").AddText(text: CGP_CLONE_LABEL.Localize(connectionData.connection), color: "0.8 0.8 0.8 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter);
                return panel;
            }
        }

        public class CreateGroupPopup : BasePopup
        {
            private static readonly Label CGP_NAME_LABEL = new Label("NAME <color=#bb0000>*</color>");
            private static readonly Label CGP_TITLE_LABEL = new Label("TITLE   ");
            private static readonly Label CGP_CREATE_LABEL = new Label("CREATE");
            public CreateGroupPopup()
            {
                Width = 400;
                Height = 210;
                CloseCommand = "adminmenu grouplist[popup:creategroup] close";
                Modules.Add(new HeaderModule("CREATE GROUP", 30) { TextColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER_TEXT], BackgroundColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER] });
            }

            public static void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["grouplist[popup:creategroup].name"] = null;
                userData["grouplist[popup:creategroup].title"] = null;
            }

            public override CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var panel = base.AddUI(parent, connectionData, userData);
                string name = (string)userData["grouplist[popup:creategroup].name"];
                string title = (string)userData["grouplist[popup:creategroup].title"];
                panel.AddText(text: CGP_NAME_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 16, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -80", offsetMax: "-60 -55");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -80", offsetMax: "170 -55").AddInputfield(command: "adminmenu grouplist[popup:creategroup] set_name", text: name ?? string.Empty, font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddText(text: CGP_TITLE_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 16, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -120", offsetMax: "-60 -95");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -120", offsetMax: "170 -95").AddInputfield(command: "adminmenu grouplist[popup:creategroup] set_title", text: title ?? string.Empty, font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddButton(color: "0.1 0.1 0.1 1", command: "adminmenu grouplist[popup:creategroup] create", anchorMin: "0.5 0", anchorMax: "0.5 0", offsetMin: "-80 10", offsetMax: "80 37").AddText(text: CGP_CREATE_LABEL.Localize(connectionData.connection), color: "0.8 0.8 0.8 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter);
                return panel;
            }
        }

        public class HeaderModule : IUIModule
        {
            public Label Label { get; set; }
            public CUI.Font Font { get; set; }
            public float Height { get; set; }
            public int FontSize { get; set; }
            public Color TextColor { get; set; } = Color.white;
            public Color BackgroundColor { get; set; } = Color.black;

            public HeaderModule(string label, float height, CUI.Font font = CUI.Font.RobotoCondensedBold, int fontSize = 14)
            {
                Label = new Label(label);
                Height = height;
                Font = font;
                FontSize = fontSize;
            }

            public CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                return parent.AddPanel(color: Extensions.ToCuiString(BackgroundColor), anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"0 -{Height}").AddText(text: Label.Localize(connectionData.connection), font: CUI.Font.RobotoCondensedBold, fontSize: FontSize, align: TextAnchor.MiddleCenter);
            }
        }

        public class KickPopup : BasePopup
        {
            private static readonly Label REASON_LABEL = new Label("REASON");
            private static readonly Button KICK_BUTTON = new Button("KICK", "userinfo.action", "kick")
            {
                Permission = "userinfo.kick"
            };
            public KickPopup()
            {
                Width = 400;
                Height = 140;
                Modules.Add(new HeaderModule("KICK", 30) { TextColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER_TEXT], BackgroundColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER] });
            }

            public static void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["userinfo[popup:kick].reason"] = "No reason given";
            }

            public override CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var panel = base.AddUI(parent, connectionData, userData);
                string reason = (string)userData["userinfo[popup:kick].reason"];
                panel.AddText(text: REASON_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 16, align: TextAnchor.MiddleCenter, anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-170 -80", offsetMax: "-60 -55");
                panel.AddPanel(color: "0.1 0.1 0.1 1", anchorMin: "0.5 1", anchorMax: "0.5 1", offsetMin: "-40 -80", offsetMax: "170 -55").AddInputfield(command: "adminmenu userinfo[popup:kick] set_reason", text: reason ?? string.Empty, font: CUI.Font.RobotoCondensedRegular, fontSize: 14, align: TextAnchor.MiddleLeft, offsetMin: "10 0", offsetMax: "-10 0");
                panel.AddButton(color: "0.1 0.1 0.1 1", command: $"adminmenu {KICK_BUTTON.FullCommand}", anchorMin: "0.5 0", anchorMax: "0.5 0", offsetMin: "-80 10", offsetMax: "80 37").AddText(text: KICK_BUTTON.Label.Localize(connectionData.connection), color: "0.8 0.8 0.8 0.8", font: CUI.Font.RobotoCondensedBold, fontSize: 18, align: TextAnchor.MiddleCenter);
                ;
                return panel;
            }
        }

        public class Popup : BasePopup
        {
            public override CUI.Element AddUI(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                return base.AddUI(root, connectionData, userData);
            }
        }

        public class RemoveGroupPopup : BasePopup
        {
            private static readonly Label REMOVECONFIRM_LABEL = new Label("Are you sure you want to <color=red>remove the group</color>?");
            private static readonly Label REMOVE_LABEL = new Label("Remove");
            private static readonly Label CANCEL_LABEL = new Label("Cancel");
            public RemoveGroupPopup()
            {
                Width = 400;
                Height = 170;
            }

            public override CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var panel = base.AddUI(parent, connectionData, userData);
                panel.AddText(text: REMOVECONFIRM_LABEL.Localize(connectionData.connection), color: "0.8 0.8 0.8 1", font: CUI.Font.RobotoCondensedBold, fontSize: 20, align: TextAnchor.MiddleCenter, anchorMin: "0.1 0.5", anchorMax: "0.9 0.5", offsetMin: "0 -20", offsetMax: "0 40");
                panel.AddButton(command: "adminmenu groupinfo.action remove.confirmed", color: "0.749 0.243 0.243 1", anchorMin: "0.5 0", anchorMax: "0.5 0", offsetMin: "-150 20", offsetMax: "-30 50").AddText(text: REMOVE_LABEL.Localize(connectionData.connection), color: "1 0.8 0.8 1", align: TextAnchor.MiddleCenter);
                panel.AddButton(command: null, close: "AdminMenu_Popup_RemoveGroupPopup", color: "0.25 0.25 0.25 1", anchorMin: "0.5 0", anchorMax: "0.5 0", offsetMin: "30 20", offsetMax: "150 50").AddText(text: CANCEL_LABEL.Localize(connectionData.connection), color: "0.9 0.9 0.9 1", align: TextAnchor.MiddleCenter);
                return panel;
            }
        }

        public class TextPopup : BasePopup
        {
            public string title;
            public string text;
            public TextPopup(string title, string text)
            {
                this.title = title;
                this.text = text;
                Width = 400;
                Height = 210;
                if (!string.IsNullOrEmpty(title))
                    Modules.Add(new HeaderModule(title, 30) { TextColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER_TEXT], BackgroundColor = Themes.CurrentTheme[Theme.KeyCollection.POPUP_HEADER] });
            }

            public override CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var panel = base.AddUI(parent, connectionData, userData);
                var body = panel.AddPanel(color: "0 0 0 0", offsetMin: "10 10", offsetMax: "-10 -40");
                body.Components.AddScrollView(vertical: true, scrollSensitivity: 30, anchorMin: "0 -9");
                body.AddText(text: text);
                return panel;
            }
        }

        private void adminmenu_chatcmd(BasePlayer player)
        {
            HandleCommand(player.Connection, "");
        }

        [ConsoleCommand("adminmenu")]
        private void adminmenu_cmd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;
            HandleCommand(player.Connection, arg.GetString(0), arg.Args?.Skip(1).ToArray());
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    LoadDefaultConfig();
                if (config.GiveItemPresets.Count == 0)
                    config.GiveItemPresets.Add(Configuration.ItemPreset.Example);
                SaveConfig();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        public enum ButtonHook
        {
            OFF,
            X,
            F
        }

        private bool CanUseCommand(Connection connection, string permission)
        {
            string userId = connection.userid.ToString();
            return UserHasPermission(userId, permission);
        }

        private string FormatCommand(string command, BasePlayer admin)
        {
            string result = command;
            if (command.Contains("{adminUID}"))
                result = result.Replace("{adminUID}", admin.UserIDString);
            if (command.Contains("{position}"))
            {
                Vector3 position = admin.transform.position;
                result = result.Replace("{position}", $"\"{position}\"");
            }

            if (command.Contains("{view_position}"))
            {
                Vector3 viewPosition;
                Ray ray = admin.eyes.HeadRay();
                float distance = 100f;
                if (Physics.Raycast(ray, out RaycastHit raycastHit, distance, 229731073))
                    viewPosition = raycastHit.point;
                else
                    viewPosition = ray.origin + ray.direction * distance;
                result = result.Replace("{view_position}", $"\"{viewPosition}\"");
            }

            if (command.Contains("{view_direction_forward}"))
                result = result.Replace("{view_direction_forward}", $"\"{(admin.eyes.GetLookRotation() * Vector3.forward).XZ3D()}\"");
            if (command.Contains("{view_direction_backward}"))
                result = result.Replace("{view_direction_backward}", $"\"{(admin.eyes.GetLookRotation() * -Vector3.forward).XZ3D()}\"");
            if (command.Contains("{view_direction_left}"))
                result = result.Replace("{view_direction_left}", $"\"{(admin.eyes.GetLookRotation() * Vector3.left).XZ3D()}\"");
            if (command.Contains("{view_direction_right}"))
                result = result.Replace("{view_direction_left}", $"\"{(admin.eyes.GetLookRotation() * Vector3.right).XZ3D()}\"");
            return result;
        }

        private string FormatCommandToUser(string command, BasePlayer user, BasePlayer admin)
        {
            return FormatCommand(command, admin).Replace("{steamid}", user.UserIDString).Replace("{steamID}", user.UserIDString).Replace("{userID}", user.UserIDString).Replace("{STEAMID}", user.UserIDString).Replace("{USERID}", user.UserIDString).Replace("{target_position}", $"\"{user.transform.position}\"");
            ;
        }

        private void HandleCommand(Connection connection, string command, params string[] args)
        {
            if (connection == null || !CanUseAdminMenu(connection))
                return;
            ConnectionData connectionData;
            switch (command)
            {
                case "":
                case "True":
                {
                    connectionData = ConnectionData.GetOrCreate(connection);
                    if (!connectionData.IsAdminMenuDisplay)
                    {
                        connectionData.ShowAdminMenu();
                        BasePlayer player = connection.player as BasePlayer;
                        if (player)
                        {
                            BasePlayer spectatingTarget = GetSpectatingTarget(player);
                            if (spectatingTarget != null && spectatingTarget.IsConnected)
                                HandleCommand(connection, "userinfo.open", spectatingTarget.UserIDString);
                            else
                                OpenUserInfoAtCrosshair(connection.player as BasePlayer);
                        }
                    }
                    else
                    {
                        HandleCommand(connection, "close");
                    }

                    break;
                }

                case "show":
                    connectionData = ConnectionData.GetOrCreate(connection);
                    connectionData.ShowAdminMenu();
                    break;
                case "close":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        foreach (Button navButton in connectionData.currentMainMenu.NavButtons)
                            if (navButton != null)
                                navButton.SetState(connectionData, Button.State.Normal);
                        connectionData.HideAdminMenu();
                    }

                    break;
                case "openpanel":
                    ConnectionData.GetOrCreate(connection).OpenPanel(args[0]);
                    break;
                case "openinfopanel":
                    ConnectionData.GetOrCreate(connection).OpenPanel("info").ShowPanelContent(args[0]);
                    break;
                case "homebutton":
                    // Action when clicking on the AdminMenu inscription at the top left
                    break;
                case "uipanel.sidebar.button_pressed":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        int buttonIndex = int.Parse(args[0]);
                        int buttonCount = int.Parse(args[1]);
                        UpdateOneActiveImageElement(connection, "UIPanel_SideBar_Button", buttonIndex, buttonCount, Themes.CurrentTheme.GetColorString(Theme.KeyCollection.PANEL_SIDEBAR_SELECTED), "0 0 0 0");
                        if (buttonIndex >= 0)
                        {
                            Button button = connectionData.currentSidebar.CategoryButtons.GetAllowedButtons(connection).ElementAt(buttonIndex);
                            if (!button.UserHasPermission(connection))
                                return;
                            HandleCommand(connection, button.Command, button.Args);
                        }
                    }

                    break;
                case "navigation.button_pressed":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        int buttonIndex2 = int.Parse(args[0]);
                        int buttonCount2 = int.Parse(args[1]);
                        IEnumerable<Button> navButtons = connectionData.currentMainMenu.NavButtons.GetAllowedButtons(connection);
                        if (buttonIndex2 != 0)
                        {
                            for (int i = 0; i < navButtons.Count(); i++)
                            {
                                Button navButton = navButtons.ElementAtOrDefault(i);
                                if (navButton == null)
                                    continue;
                                if (i == buttonIndex2)
                                    navButton.SetState(connectionData, Button.State.Pressed);
                                else
                                    navButton.SetState(connectionData, Button.State.Normal);
                            }
                        }

                        if (buttonIndex2 >= 0)
                        {
                            Button navButton = navButtons.ElementAt(buttonIndex2);
                            if (navButton != null && navButton.UserHasPermission(connection))
                                HandleCommand(connection, navButton.Command, navButton.Args);
                        }

                        connectionData.UI.UpdateNavButtons(connectionData.currentMainMenu);
                    }

                    break;
                case "showcontent":
                    ConnectionData.GetOrCreate(connection).ShowPanelContent(args[0]);
                    break;
                case "back":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string backcommand = (string)connectionData.userData["backcommand"];
                        if (backcommand != null)
                        {
                            string[] a = backcommand.Split(' ');
                            HandleCommand(connection, a[0], a.Skip(1).ToArray());
                            connectionData.userData["backcommand"] = null;
                        }
                    }

                    break;
                case "playerlist.opensearch":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        (connectionData.currentContent as PlayerListContent)?.OpenSearch(connection);
                    break;
                case "playerlist.search.input":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string searchQuery = string.Empty;
                        if (args.Length > 0)
                            searchQuery = string.Join(" ", args);
                        connectionData.userData["playerlist.searchQuery"] = searchQuery;
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "playerlist.filter":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        Func<IPlayer, bool> filterFunc;
                        switch (args[0])
                        {
                            case "online":
                                filterFunc = (IPlayer player) => player.IsConnected;
                                break;
                            case "offline":
                                filterFunc = (IPlayer player) => !player.IsConnected && BasePlayer.FindSleeping(player.Id);
                                break;
                            case "banned":
                                filterFunc = (IPlayer player) => player.IsBanned;
                                break;
                            case "admins":
                                filterFunc = (IPlayer player) => ServerUsers.Get(ulong.Parse(player.Id))?.group == ServerUsers.UserGroup.Owner;
                                break;
                            case "moders":
                                filterFunc = (IPlayer player) => ServerUsers.Get(ulong.Parse(player.Id))?.group == ServerUsers.UserGroup.Moderator;
                                break;
                            default:
                                filterFunc = (IPlayer player) => true;
                                break;
                        }

                        connectionData.userData["playerlist.filter"] = filterFunc;
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "permissionmanager.reset":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.currentContent.LoadDefaultUserData(connectionData.userData);
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "permissionmanager.show_user_category":
                {
                    if (args.Length != 3)
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        int categoryIndex = int.Parse(args[0]);
                        int x = int.Parse(args[1]);
                        int y = int.Parse(args[2]);
                        CUI.Root root = new CUI.Root("AdminMenu_PermissionManager_Layout");
                        switch (categoryIndex)
                        {
                            case 0:
                            {
                                NewPermissionManagerContent.AddGroupsBranch(root, connectionData, ref x, ref y, out int totalY);
                                root.AddEmpty().AddDestroy("PermissionsBranch");
                                break;
                            }

                            case 1:
                            {
                                NewPermissionManagerContent.AddPluginsBranch(root, connectionData.userData, ref x, ref y, out int totalY);
                                break;
                            }

                            default:
                                return;
                        }

                        root.Render(connection);
                    }

                    break;
                }

                case "permissionmanager.show_permissions":
                {
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    if (args.Length != 3)
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string pluginName = args[0];
                        if (!plugins.Exists(pluginName))
                            return;
                        int x = int.Parse(args[1]);
                        int y = int.Parse(args[2]);
                        CUI.Root root = new CUI.Root("AdminMenu_PermissionManager_Layout");
                        NewPermissionManagerContent.AddPermissionsBranch(root, connectionData.userData, pluginName, ref x, ref y, out int totalY);
                        root.Render(connection);
                    }

                    break;
                }

                case "permissionmanager.show_groups":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.userData["permissions.target_type"] = "group";
                        connectionData.ShowPanelContent("groups");
                    }

                    break;
                case "permissionmanager.show_players":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.userData["permissions.target_type"] = "user";
                        connectionData.OpenPanel("playerlist");
                        connectionData.userData["playerlist.executeCommand"] = "adminmenu permissionmanager.select_user";
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "permissionmanager.select_group":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.ShowPanelContent("default");
                        connectionData.userData["permissions.target_type"] = "group";
                        connectionData.userData["permissions.target"] = args[0];
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "permissionmanager.select_user":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.OpenPanel("permissionmanager");
                        connectionData.userData["permissions.target_type"] = "user";
                        connectionData.userData["permissions.target"] = args[0];
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "permissionmanager.usergroups":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string type = connectionData.userData["permissions.target_type"]?.ToString();
                        string target = connectionData.userData["permissions.target"]?.ToString();
                        if (type != "user" || string.IsNullOrEmpty(target))
                            return;
                        bool isGrant;
                        switch (args[0])
                        {
                            case "grant":
                                isGrant = true;
                                break;
                            case "revoke":
                                isGrant = false;
                                break;
                            default:
                                return;
                        }

                        string groupName = args[1];
                        if (isGrant)
                            Instance.permission.AddUserGroup(target, groupName);
                        else
                            Instance.permission.RemoveUserGroup(target, groupName);
                        LogToDiscord(connection, isGrant ? "Grant user group to the player" : "Revoke user group from the player", $"The administrator change **{groupName}** user group to **[{ServerMgr.Instance.persistance.GetPlayerName(ulong.Parse(target))}](<https://steamcommunity.com/profiles/{target}>)** player!");
                        bool hasGroup = Instance.permission.UserHasGroup(target, groupName);
                        string color = hasGroup ? "0.3 0.6 0.7 1" : "0 0 0 0";
                        CUI.Root root = new CUI.Root();
                        root.AddUpdateElement(groupName).Components.AddButton($"adminmenu permissionmanager.usergroups {(!hasGroup ? "grant" : "revoke")} {groupName}");
                        root.AddUpdateElement($"{groupName} - COLOR").Components.AddImage(color: color);
                        root.Render(connection);
                    }

                    break;
                case "grouplist[popup:creategroup]":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        switch (args[0])
                        {
                            case "show":
                                connectionData.userData["grouplist[popup:creategroup]"] = true;
                                CreateGroupPopup.LoadDefaultUserData(connectionData.userData);
                                break;
                            case "close":
                                connectionData.userData["grouplist[popup:creategroup]"] = false;
                                break;
                            case "set_name":
                                connectionData.userData["grouplist[popup:creategroup].name"] = string.Join(" ", args.Skip(1));
                                break;
                            case "set_title":
                                connectionData.userData["grouplist[popup:creategroup].title"] = string.Join(" ", args.Skip(1));
                                break;
                            case "create":
                                bool creategroup_result = false;
                                string name = (string)connectionData.userData["grouplist[popup:creategroup].name"];
                                string title = (string)connectionData.userData["grouplist[popup:creategroup].title"];
                                if (permission.GroupExists(name))
                                {
                                    connectionData.userData["grouplist[popup:creategroup].name"] = null;
                                    creategroup_result = false;
                                }
                                else
                                {
                                    creategroup_result = permission.CreateGroup(name, title, 0);
                                }

                                if (creategroup_result)
                                    connectionData.userData["grouplist[popup:creategroup]"] = false;
                                break;
                        }

                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "groupinfo.open":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.userData["groupinfo.groupName"] = args[0];
                        if (connectionData.currentContent is GroupInfoContent)
                            connectionData.currentContent.Render(connectionData);
                        else
                            connectionData.OpenPanel("groupinfo");
                    }

                    break;
                case "groupinfo.permissions":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.OpenPanel("permissionmanager");
                        connectionData.userData["backcommand"] = $"groupinfo.open {connectionData.userData["groupinfo.groupName"]}";
                        connectionData.UI.UpdateNavButtons(connectionData.currentMainMenu);
                        connectionData.userData["permissions.target"] = connectionData.userData["groupinfo.groupName"];
                        connectionData.userData["permissions.target_type"] = "group";
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "groupinfo.users.open":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.userData["playerlist.filter"] = (Func<IPlayer, bool>)((IPlayer player) => permission.UserHasGroup(player.Id, connectionData.userData["groupinfo.groupName"].ToString()));
                        connectionData.ShowPanelContent("users");
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "groupinfo.action":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        switch (args[0])
                        {
                            case "remove":
                                (connectionData.currentContent as GroupInfoContent).RemoveConfirmPopup(connectionData);
                                break;
                            case "remove.confirmed":
                                string groupName = (string)connectionData.userData["groupinfo.groupName"];
                                if (groupName == null)
                                    return;
                                if (permission.RemoveGroup(groupName))
                                    connectionData.OpenPanel("permissionmanager");
                                break;
                        }
                    }

                    break;
                case "groupinfo[popup:clonegroup]":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        switch (args[0])
                        {
                            case "show":
                                connectionData.userData["groupinfo[popup:clonegroup]"] = true;
                                CloneGroupPopup.LoadDefaultUserData(connectionData.userData);
                                break;
                            case "close":
                                connectionData.userData["groupinfo[popup:clonegroup]"] = false;
                                break;
                            case "set_name":
                                connectionData.userData["groupinfo[popup:clonegroup].name"] = string.Join(" ", args.Skip(1));
                                break;
                            case "set_title":
                                connectionData.userData["groupinfo[popup:clonegroup].title"] = string.Join(" ", args.Skip(1));
                                break;
                            case "cloneusers_toggle":
                                connectionData.userData["groupinfo[popup:clonegroup].cloneusers"] = !(bool)connectionData.userData["groupinfo[popup:clonegroup].cloneusers"];
                                break;
                            case "clone":
                                string groupName = (string)connectionData.userData["groupinfo.groupName"];
                                if (groupName == null)
                                    return;
                                bool creategroup_result = false;
                                string name = (string)connectionData.userData["groupinfo[popup:clonegroup].name"];
                                string title = (string)connectionData.userData["groupinfo[popup:clonegroup].title"];
                                bool cloneUsers = (bool)connectionData.userData["groupinfo[popup:clonegroup].cloneusers"];
                                if (permission.GroupExists(name))
                                {
                                    connectionData.userData["groupinfo[popup:clonegroup].name"] = null;
                                    creategroup_result = false;
                                }
                                else
                                {
                                    if (permission.CreateGroup(name, title, 0))
                                    {
                                        string[] perms = permission.GetGroupPermissions(groupName);
                                        for (int i = 0; i < perms.Length; i++)
                                            permission.GrantGroupPermission(name, perms[i], null);
                                        if (cloneUsers)
                                        {
                                            string[] users = permission.GetUsersInGroup(groupName);
                                            for (int i = 0; i < users.Length; i++)
                                            {
                                                string userId = users[i].Split(' ')?[0];
                                                if (!string.IsNullOrEmpty(userId))
                                                    permission.AddUserGroup(userId, name);
                                            }
                                        }

                                        creategroup_result = true;
                                    }
                                }

                                if (creategroup_result)
                                {
                                    connectionData.userData["groupinfo[popup:clonegroup]"] = false;
                                    HandleCommand(connection, "groupinfo.open", name);
                                }

                                break;
                        }

                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "userinfo.open":
                    connectionData = ConnectionData.GetOrCreate(connection);
                    if (connectionData != null)
                    {
                        ulong userid;
                        switch (args[0])
                        {
                            case "self":
                                userid = connection.userid;
                                break;
                            case "last":
                                userid = (ulong)connectionData.userData["userinfo.lastuserid"];
                                break;
                            default:
                                ulong.TryParse(args[0], out userid);
                                break;
                        }

                        if (userid == 0)
                            return;
                        if (!connectionData.IsAdminMenuDisplay)
                            connectionData.ShowAdminMenu();
                        connectionData.userData["userinfo.userid"] = userid;
                        connectionData.userData["userinfo.lastuserid"] = userid;
                        if (connectionData.currentContent is UserInfoContent)
                            connectionData.currentContent.Render(connectionData);
                        else
                            connectionData.OpenPanel("userinfo");
                    }

                    break;
                case "userinfo.givemenu.open":
                    if (!CanUseCommand(connection, PERMISSION_GIVE))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.userData["givemenu.targets"] = new ulong[]
                        {
                            (ulong)connectionData.userData["userinfo.userid"]
                        };
                        connectionData.ShowPanelContent("give");
                    }

                    break;
                case "userinfo.permissions":
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.OpenPanel("permissionmanager");
                        connectionData.userData["backcommand"] = $"userinfo.open {connectionData.userData["userinfo.userid"]}";
                        connectionData.UI.UpdateNavButtons(connectionData.currentMainMenu);
                        connectionData.userData["permissions.target"] = connectionData.userData["userinfo.userid"];
                        connectionData.userData["permissions.target_type"] = "user";
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "userinfo.action":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string userid = connectionData.userData["userinfo.userid"].ToString();
                        if (userid == null)
                            return;
                        UserInfoContent userInfoContent = (connectionData.currentContent as UserInfoContent);
                        Button button = null;
                        if (!Button.all.TryGetValue($"{command} {string.Join(" ", args)}", out button))
                        {
                            if (button == null)
                            {
                                ConsoleNetwork.SendClientCommand(connection, "echo Button not found.");
                                return;
                            }
                        }

                        if (!button.UserHasPermission(connection))
                        {
                            ConsoleNetwork.SendClientCommand(connection, "echo You don't have permission to use it.");
                            return;
                        }

                        BasePlayer admin = connection.player as BasePlayer;
                        BasePlayer user = BasePlayer.FindAwakeOrSleeping(userid);
                        if (admin == null || user == null)
                            return;
                        string action = args[0];
                        switch (action)
                        {
                            case "steaminfo_update":
                                RequestSteamInfo(user.userID, out SteamInfo steamInfo, (s) => connectionData.currentContent.Render(connectionData), true);
                                connectionData.currentContent.Render(connectionData);
                                break;
                            case "teleportselfto":
                                admin.Teleport(user);
                                if (config.Logs.AdminTeleport)
                                    LogToDiscord(connection, "Teleport admin to player", $"The administrator teleported to **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "teleporttoself":
                                user.Teleport(admin);
                                if (config.Logs.AdminTeleport)
                                    LogToDiscord(connection, "Teleport player to admin", $"The administrator teleported **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player to himself!");
                                break;
                            case "teleporttoauth":
                                BaseEntity[] entities = BaseEntity.Util.FindTargetsAuthedTo(user.userID, string.Empty);
                                if (entities.Length > 0)
                                    admin.Teleport(entities.GetRandom().transform.position);
                                break;
                            case "teleporttodeathpoint":
                                ProtoBuf.MapNote UserDeathNote = user.ServerCurrentDeathNote;
                                if (UserDeathNote != null)
                                    admin.Teleport(UserDeathNote.worldPosition);
                                break;
                            case "heal":
                                if (user.IsWounded())
                                    user.StopWounded();
                                user.Heal(user.MaxHealth());
                                user.metabolism.calories.value = user.metabolism.calories.max;
                                user.metabolism.hydration.value = user.metabolism.hydration.max;
                                user.metabolism.radiation_level.value = 0;
                                user.metabolism.radiation_poison.value = 0;
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.Heal)
                                    LogToDiscord(connection, "Full healing of the player", $"The administrator heal **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "heal50":
                                if (user.IsWounded())
                                    user.StopWounded();
                                user.Heal(user.MaxHealth() / 50);
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.Heal)
                                    LogToDiscord(connection, "Half healing of the player", $"The administrator heal **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "kill":
                                user.DieInstantly();
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.Kill)
                                    LogToDiscord(connection, "Kill the player", $"The administrator kill **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "viewinv":
                                PlayerLoot playerLoot = admin.inventory.loot;
                                bool IsLooting = playerLoot.IsLooting();
                                playerLoot.containers.Clear();
                                playerLoot.entitySource = null;
                                playerLoot.itemSource = null;
                                if (IsLooting)
                                    playerLoot.SendImmediate();
                                NextFrame(() =>
                                {
                                    playerLoot.PositionChecks = false;
                                    playerLoot.entitySource = RelationshipManager.ServerInstance;
                                    playerLoot.AddContainer(user.inventory.containerMain);
                                    playerLoot.AddContainer(user.inventory.containerWear);
                                    playerLoot.AddContainer(user.inventory.containerBelt);
                                    playerLoot.SendImmediate();
                                    admin.ClientRPC<string>(RpcTarget.Player("RPC_OpenLootPanel", admin), "player_corpse");
                                    Item backpackWithInventory = user.inventory.GetBackpackWithInventory();
                                    if (backpackWithInventory != null)
                                        AddViewingBackpack(playerLoot, backpackWithInventory);
                                });
                                HandleCommand(connection, "close");
                                if (config.Logs.LookInventory)
                                    LogToDiscord(connection, "Look the player's inventory", $"The administrator look the **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)**'s inventory!");
                                break;
                            case "stripinventory":
                                user.inventory.Strip();
                                if (config.Logs.StripInventory)
                                    LogToDiscord(connection, "Strip the player's inventory", $"The administrator strip the **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)**'s inventory!");
                                break;
                            case "unlockblueprints":
                                user.blueprints.UnlockAll();
                                if (config.Logs.Blueprints)
                                    LogToDiscord(connection, "Unlock the player's blueprints", $"The administrator unlock the **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)**'s blueprints!");
                                break;
                            case "revokeblueprints":
                                user.blueprints.Reset();
                                if (config.Logs.Blueprints)
                                    LogToDiscord(connection, "Revoke the player's blueprints", $"The administrator revoke the **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)**'s blueprints!");
                                break;
                            case "spectate":
                                if (!admin.IsDead())
                                    admin.DieInstantly();
                                if (admin.IsDead())
                                {
                                    admin.StartSpectating();
                                    admin.UpdateSpectateTarget(user.userID);
                                }

                                if (config.Logs.Spectate)
                                    LogToDiscord(connection, "Spectate to the player", $"The administrator spectate to **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "mute":
                                user.SetPlayerFlag(BasePlayer.PlayerFlags.ChatMute, true);
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.MuteUnmute)
                                    LogToDiscord(connection, "Mute the player", $"The administrator mute **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "unmute":
                                user.SetPlayerFlag(BasePlayer.PlayerFlags.ChatMute, false);
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.MuteUnmute)
                                    LogToDiscord(connection, "Unmute the player", $"The administrator unmute **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "creative":
                                user.SetPlayerFlag(BasePlayer.PlayerFlags.CreativeMode, !user.HasPlayerFlag(BasePlayer.PlayerFlags.CreativeMode));
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.ToggleCreative)
                                    LogToDiscord(connection, "Toggle creative mode for player", $"The administrator toggle creative mode for **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player to **{user.IsInCreativeMode}**!");
                                break;
                            case "cuff":
                                user.SetPlayerFlag(BasePlayer.PlayerFlags.IsRestrained, !user.HasPlayerFlag(BasePlayer.PlayerFlags.IsRestrained));
                                user.inventory.SetLockedByRestraint(user.IsRestrained);
                                connectionData.currentContent.Render(connectionData);
                                if (user.IsRestrained)
                                {
                                    user.Server_CancelGesture();
                                    Item slot = user.inventory.containerBelt.GetSlot(0);
                                    if (slot != null)
                                    {
                                        if (!slot.MoveToContainer(user.inventory.containerMain, -1, true, false, null, true))
                                        {
                                            slot.DropAndTossUpwards(user.transform.position, 2f);
                                        }
                                    }

                                    user.ClientRPC<int, ItemId>(RpcTarget.Player("SetActiveBeltSlot", user), 0, default(ItemId));
                                }

                                if (config.Logs.Cuff)
                                    LogToDiscord(connection, "Toggle cuff for player", $"The administrator toggle cuff for **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player to **{user.IsRestrained}**!");
                                break;
                            case "kick":
                                if (args.Length == 2 && args[1] == "showpopup")
                                {
                                    (connectionData.currentContent as UserInfoContent).ShowKickPopup(connectionData);
                                    return;
                                }

                                string kickReason = "No reason given";
                                if (connectionData.userData.TryGetValue("userinfo[popup:kick].reason", out object kickReasonObj))
                                    kickReason = (string)kickReasonObj;
                                user.Kick(kickReason);
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.Kick)
                                    LogToDiscord(connection, "Kick the player", $"The administrator kick **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            case "ban":
                                if (args.Length == 2 && args[1] == "showpopup")
                                {
                                    (connectionData.currentContent as UserInfoContent).ShowBanPopup(connectionData);
                                    return;
                                }

                                int banWeeks = 0;
                                int banDays = 0;
                                int banHours = 0;
                                int banMinutes = 0;
                                string banReason = "No reason given";
                                if (connectionData.userData.TryGetValue("userinfo[popup:ban].reason", out object banReasonObj))
                                    banReason = (string)banReasonObj;
                                if (connectionData.userData.TryGetValue("userinfo[popup:ban].weeks", out object banWeeksObj))
                                    banWeeks = (int)banWeeksObj;
                                if (connectionData.userData.TryGetValue("userinfo[popup:ban].days", out object banDaysObj))
                                    banDays = (int)banDaysObj;
                                if (connectionData.userData.TryGetValue("userinfo[popup:ban].hours", out object banHoursObj))
                                    banHours = (int)banHoursObj;
                                if (connectionData.userData.TryGetValue("userinfo[popup:ban].minutes", out object banMinutesObj))
                                    banMinutes = (int)banMinutesObj;
                                long expiry = -1L;
                                if (!(banWeeks == 0 && banDays == 0 && banHours == 0 && banMinutes == 0))
                                    expiry = (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + new TimeSpan(banWeeks * 7 + banDays, banHours, banMinutes).TotalSeconds);
                                global::ServerUsers.User serverUser = global::ServerUsers.Get(user.userID);
                                if (serverUser != null && serverUser.group == global::ServerUsers.UserGroup.Banned)
                                {
                                    admin.ConsoleMessage(string.Format("User {0} is already banned", user.userID));
                                    return;
                                }

                                ServerUsers.Set(user.userID, global::ServerUsers.UserGroup.Banned, user.displayName, banReason);
                                if (user.IsConnected && user.net.connection.ownerid != 0UL && user.net.connection.ownerid != user.net.connection.userid)
                                    global::ServerUsers.Set(user.net.connection.ownerid, global::ServerUsers.UserGroup.Banned, user.displayName, string.Format("Family share owner of {0}", user.net.connection.userid), expiry);
                                ServerUsers.Save();
                                Net.sv.Kick(user.net.connection, banReason, false);
                                connectionData.currentContent.Render(connectionData);
                                if (config.Logs.Ban)
                                    LogToDiscord(connection, "Ban the player", $"The administrator ban **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player for {expiry} with the reason {banReason}!");
                                break;
                            case "cb.exec":
                                int hashCode = int.Parse(args[1]);
                                UserInfoCustomButton uicb = config.UserInfoCustomButtons.Find(button => button.GetHashCode() == hashCode);
                                if (uicb == null)
                                    break;
                                if (!uicb.Button.UserHasPermission(connection))
                                    break;
                                foreach (string cmd in uicb.Commands)
                                {
                                    string formatedCmd = FormatCommandToUser(cmd, user, admin);
                                    if (uicb.ExecutionAsServer)
                                        ConsoleSystem.Run(ConsoleSystem.Option.Server, formatedCmd);
                                    else
                                        admin.SendConsoleCommand(formatedCmd);
                                }

                                if (config.Logs.CustomButtons)
                                    LogToDiscord(connection, "Using a custom button", $"The administrator used a custom button with name **{uicb.Label}** at **[{user.displayName}](<https://steamcommunity.com/profiles/{user.UserIDString}>)** player!");
                                break;
                            default:
                                break;
                        }
                    }

                    break;
                case "userinfo[popup:kick]":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        switch (args[0])
                        {
                            case "set_reason":
                                connectionData.userData["userinfo[popup:kick].reason"] = string.Join(" ", args.Skip(1));
                                break;
                        }
                    }

                    break;
                case "userinfo[popup:ban]":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        switch (args[0])
                        {
                            case "show":
                                connectionData.userData["userinfo[popup:ban]"] = true;
                                BanPopup.LoadDefaultUserData(connectionData.userData);
                                break;
                            case "close":
                                connectionData.userData["userinfo[popup:ban]"] = false;
                                break;
                            case "set_reason":
                                connectionData.userData["userinfo[popup:ban].reason"] = string.Join(" ", args.Skip(1));
                                break;
                            case "set_weeks":
                                int.TryParse(args[1], out int weeks);
                                connectionData.userData["userinfo[popup:ban].weeks"] = Mathf.Clamp(weeks, 0, 99);
                                break;
                            case "set_days":
                                int.TryParse(args[1], out int days);
                                connectionData.userData["userinfo[popup:ban].days"] = Mathf.Clamp(days, 0, 6);
                                break;
                            case "set_hours":
                                int.TryParse(args[1], out int hours);
                                connectionData.userData["userinfo[popup:ban].hours"] = Mathf.Clamp(hours, 0, 23);
                                break;
                            case "set_minutes":
                                int.TryParse(args[1], out int minutes);
                                connectionData.userData["userinfo[popup:ban].minutes"] = Mathf.Clamp(minutes, 0, 59);
                                break;
                        }
                    }

                    break;
                case "permission.action":
                {
                    if (!CanUseCommand(connection, PERMISSION_PERMISSIONMANAGER))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string type = connectionData.userData["permissions.target_type"].ToString();
                        string target = connectionData.userData["permissions.target"].ToString();
                        if (type == null || target == null)
                            return;
                        bool isTargetUser;
                        switch (type)
                        {
                            case "user":
                                isTargetUser = true;
                                break;
                            case "group":
                                isTargetUser = false;
                                break;
                            default:
                                return;
                        }

                        bool isGrant;
                        switch (args[0])
                        {
                            case "grant":
                                isGrant = true;
                                break;
                            case "revoke":
                                isGrant = false;
                                break;
                            default:
                                return;
                        }

                        string permission = args[1];
                        if (permission == "adminmenu.fullaccess")
                            return;
                        if (isTargetUser)
                        {
                            if (isGrant)
                                Instance.permission.GrantUserPermission(target, permission, null);
                            else
                                Instance.permission.RevokeUserPermission(target, permission);
                            LogToDiscord(connection, isGrant ? "Grant permission to the player" : "Revoke permission from the player", $"The administrator change **{permission}** permission to **[{ServerMgr.Instance.persistance.GetPlayerName(ulong.Parse(target))}](<https://steamcommunity.com/profiles/{target}>)** player!");
                        }
                        else
                        {
                            if (isGrant)
                                Instance.permission.GrantGroupPermission(target, permission, null);
                            else
                                Instance.permission.RevokeGroupPermission(target, permission);
                            LogToDiscord(connection, isGrant ? "Grant permission to the group" : "Revoke permission from the group", $"The administrator change **{permission}** permission to **{target}** group!");
                        }

                        const string userColor = "0.5 0.7 0.4 1";
                        const string groupColor = "0.3 0.6 0.7 1";
                        bool hasUser = false;
                        bool hasGroup = false;
                        if (isTargetUser)
                        {
                            var permUserData = Instance.permission.GetUserData(target);
                            if (permUserData.Perms.Contains(permission, StringComparer.OrdinalIgnoreCase))
                            {
                                hasUser = true;
                            }
                            else if (Instance.permission.GroupsHavePermission(permUserData.Groups, permission))
                            {
                                hasGroup = true;
                            }
                        }
                        else
                        {
                            hasGroup = Instance.permission.GroupHasPermission(target, permission);
                        }

                        string color = "0 0 0 0";
                        if (hasUser || hasGroup)
                            color = hasGroup ? groupColor : userColor;
                        CUI.Root root = new CUI.Root();
                        root.AddUpdateElement(permission).Components.AddButton($"adminmenu permission.action {(isTargetUser && !hasUser || !isTargetUser && !hasGroup ? "grant" : "revoke")} {permission}");
                        root.AddUpdateElement($"{permission} - COLOR").Components.AddImage(color: color);
                        root.Render(connection);
                    }

                    break;
                }

                case "givemenu.open":
                    if (!CanUseCommand(connection, PERMISSION_GIVE))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        ulong userid;
                        switch (args[0])
                        {
                            case "self":
                                userid = connection.userid;
                                break;
                            default:
                                ulong.TryParse(args[0], out userid);
                                break;
                        }

                        connectionData.userData["givemenu.targets"] = new ulong[]
                        {
                            userid
                        };
                        connectionData.OpenPanel("givemenu");
                    }

                    break;
                case "givemenu.opensearch":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        (connectionData.currentContent as GiveMenuContent)?.OpenSearch(connection);
                    break;
                case "givemenu.search.input":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string searchQuery = string.Empty;
                        if (args.Length > 0)
                            searchQuery = string.Join(" ", args);
                        connectionData.userData["givemenu.searchQuery"] = searchQuery;
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "givemenu.filter":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        ItemCategory recievedCategory = (ItemCategory)int.Parse(args[0]);
                        ItemCategory currentCategory = (ItemCategory)connectionData.userData["givemenu.category"];
                        connectionData.userData["givemenu.category"] = recievedCategory;
                        connectionData.userData["givemenu.page"] = 1;
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "givemenu.popup":
                    if (!CanUseCommand(connection, PERMISSION_GIVE))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        switch (args[0])
                        {
                            case "show":
                                connectionData.userData["givemenu.popup.shown"] = true;
                                connectionData.userData["givemenu.popup.itemid"] = int.Parse(args[1]);
                                connectionData.userData["givemenu.popup.amount"] = 1;
                                connectionData.userData["givemenu.popup.skin"] = 0UL;
                                connectionData.userData["givemenu.popup.name"] = null;
                                connectionData.userData["givemenu.popup.isblueprint"] = false;
                                break;
                            case "show_custom":
                                connectionData.userData["givemenu.popup.shown"] = true;
                                connectionData.userData["givemenu.popup.itemid"] = int.Parse(args[1]);
                                connectionData.userData["givemenu.popup.amount"] = 1;
                                connectionData.userData["givemenu.popup.skin"] = ulong.Parse(args[2]);
                                connectionData.userData["givemenu.popup.name"] = string.Join(" ", args.Skip(3));
                                connectionData.userData["givemenu.popup.isblueprint"] = false;
                                break;
                            case "close":
                                connectionData.userData["givemenu.popup.shown"] = false;
                                return;
                            case "set_amount":
                                if (int.TryParse(args[1], out int set_amount))
                                    connectionData.userData["givemenu.popup.amount"] = set_amount;
                                break;
                            case "set_skin":
                                if (ulong.TryParse(args[1], out ulong set_skinid))
                                    connectionData.userData["givemenu.popup.skin"] = set_skinid;
                                break;
                            case "set_name":
                                connectionData.userData["givemenu.popup.name"] = string.Join(" ", args.Skip(1));
                                break;
                            case "isblueprint_toggle":
                                connectionData.userData["givemenu.popup.isblueprint"] = !(bool)connectionData.userData["givemenu.popup.isblueprint"];
                                break;
                            case "give":
                                connectionData = ConnectionData.Get(connection);
                                if (connectionData != null)
                                {
                                    int give_itemId = (int)connectionData.userData["givemenu.popup.itemid"];
                                    int give_amount = (int)connectionData.userData["givemenu.popup.amount"];
                                    ulong give_skin = (ulong)connectionData.userData["givemenu.popup.skin"];
                                    string give_name = (string)connectionData.userData["givemenu.popup.name"];
                                    bool isBlueprint = (bool)connectionData.userData["givemenu.popup.isblueprint"];
                                    ulong[] targets = (ulong[])connectionData.userData["givemenu.targets"];
                                    foreach (ulong targetUserId in targets)
                                    {
                                        BasePlayer playerToGive = BasePlayer.FindAwakeOrSleeping(targetUserId.ToString());
                                        if (playerToGive == null)
                                            continue;
                                        Item newItem = ItemManager.CreateByItemID(isBlueprint ? ItemManager.blueprintBaseDef.itemid : give_itemId, give_amount, give_skin);
                                        if (newItem != null)
                                        {
                                            if (isBlueprint)
                                            {
                                                newItem.blueprintTarget = give_itemId;
                                                newItem.OnVirginSpawn();
                                            }
                                            else
                                            {
                                                if (give_name != null)
                                                    newItem.name = give_name;
                                            }

                                            playerToGive.GiveItem(newItem);
                                            LogToDiscord(connection, targetUserId == connection.userid ? "Giving an item to yourself" : "Giving an item to a player", $"The item \"**{newItem.info.displayName.english}**\" with the skin **{newItem.skin}** in the amount of **{newItem.amount}** was given to **[{playerToGive.displayName}](<https://steamcommunity.com/profiles/{playerToGive.UserIDString}>)** player!\n||[{newItem.info.shortname}:{newItem.skin}]x{newItem.amount}||", $"https://rustlabs.com/img/items40/{newItem.info.shortname}.png");
                                        }
                                    }

                                    connectionData.userData["givemenu.popup.shown"] = false;
                                }

                                break;
                        }

                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "quickmenu.action":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        BasePlayer admin = connection.player as BasePlayer;
                        if (admin == null)
                            return;
                        QuickMenuContent quickMenuContent = (connectionData.currentContent as QuickMenuContent);
                        Button button = null;
                        if (!Button.all.TryGetValue($"{command} {string.Join(" ", args)}", out button))
                        {
                            if (button == null)
                            {
                                ConsoleNetwork.SendClientCommand(connection, "echo Button not found.");
                                return;
                            }
                        }

                        if (!button.UserHasPermission(connection))
                        {
                            ConsoleNetwork.SendClientCommand(connection, "echo You don't have permission to use it.");
                            return;
                        }

                        string action = args[0];
                        switch (action)
                        {
                            case "teleportto_000":
                                admin.Teleport(Vector3.zero);
                                if (config.Logs.AdminTeleport)
                                    LogToDiscord(connection, "Teleport admin to 0 0 0", $"The administrator teleported to 0 0 0 coordinates!");
                                break;
                            case "teleportto_deathpoint":
                                var deathMapNote = admin.ServerCurrentDeathNote;
                                if (deathMapNote != null)
                                    admin.Teleport(deathMapNote.worldPosition);
                                if (config.Logs.AdminTeleport)
                                    LogToDiscord(connection, "Teleported to death point", $"The administrator teleported to his point of death.");
                                break;
                            case "teleportto_randomspawnpoint":
                                global::BasePlayer.SpawnPoint spawnPoint = global::ServerMgr.FindSpawnPoint(admin);
                                if (spawnPoint != null)
                                    admin.Teleport(spawnPoint.pos);
                                break;
                            case "toggle_teleport_to_marker":
                                (button as ToggleButton).Toggle(connectionData);
                                connectionData.currentContent.Render(connectionData);
                                break;
                            case "healself":
                                if (admin.IsWounded())
                                    admin.StopWounded();
                                admin.Heal(admin.MaxHealth());
                                admin.metabolism.calories.value = admin.metabolism.calories.max;
                                admin.metabolism.hydration.value = admin.metabolism.hydration.max;
                                admin.metabolism.radiation_level.value = 0;
                                admin.metabolism.radiation_poison.value = 0;
                                if (config.Logs.Heal)
                                    LogToDiscord(connection, "Heal self", $"The administrator healed himself.");
                                break;
                            case "killself":
                                admin.DieInstantly();
                                if (config.Logs.Kill)
                                    LogToDiscord(connection, "Kill self", $"The administrator killed himself.");
                                break;
                            case "helicall":
                                global::BaseEntity heliEntity = global::GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", default(Vector3), default(Quaternion), true);
                                if (heliEntity)
                                {
                                    heliEntity.GetComponent<global::PatrolHelicopterAI>().SetInitialDestination(admin.transform.position + new Vector3(0f, 10f, 0f), 0.25f);
                                    heliEntity.Spawn();
                                }

                                if (config.Logs.SpawnEntities)
                                    LogToDiscord(connection, "Heli call", $"The administrator called a **patrol helicopter**!");
                                break;
                            case "spawnbradley":
                                GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", admin.CenterPoint(), default(Quaternion), true).Spawn();
                                if (config.Logs.SpawnEntities)
                                    LogToDiscord(connection, "Spawn Bradley", $"The administrator spawned a **bradley**!");
                                break;
                            case "spawncargo":
                                BaseEntity cargo = global::GameManager.server.CreateEntity("assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", default(Vector3), default(Quaternion), true);
                                if (cargo != null)
                                {
                                    cargo.SendMessage("TriggeredEventSpawn", SendMessageOptions.DontRequireReceiver);
                                    cargo.Spawn();
                                    return;
                                }

                                if (config.Logs.SpawnEntities)
                                    LogToDiscord(connection, "Spawn Cargo", $"The administrator spawned a **cargo**!");
                                break;
                            case "giveaway_online":
                                connectionData.userData["givemenu.targets"] = BasePlayer.activePlayerList.Select(p => p.userID.Get()).ToArray();
                                connectionData.OpenPanel("givemenu");
                                break;
                            case "giveaway_everyone":
                                connectionData.userData["givemenu.targets"] = BasePlayer.allPlayerList.Select(p => p.userID.Get()).ToArray();
                                connectionData.OpenPanel("givemenu");
                                break;
                            case "settime":
                                float time = float.Parse(args[1]);
                                ConVar.Env.time = time;
                                if (config.Logs.SetTime)
                                    LogToDiscord(connection, "Set Time", $"The administrator set the time at **{time}**!");
                                break;
                            case "cb.exec":
                                int hashCode = int.Parse(args[1]);
                                QMCustomButton qmcb = config.CustomQuickButtons.Find(button => button.GetHashCode() == hashCode);
                                if (qmcb == null)
                                    break;
                                if (!qmcb.Button.UserHasPermission(connection))
                                    break;
                                string[] commands;
                                ToggleButton toggleButton = qmcb.Button as ToggleButton;
                                if (toggleButton != null && toggleButton.GetState(connectionData) == Button.State.Toggled)
                                    commands = qmcb.ToggledStateCommands;
                                else
                                    commands = qmcb.Commands;
                                List<string> commandsToExec = new List<string>();
                                switch (qmcb.PlayerReceivers)
                                {
                                    case QMCustomButton.Recievers.Online:
                                        foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                                            commandsToExec.AddRange(commands.Select(cmd => FormatCommandToUser(cmd, activePlayer, admin)));
                                        break;
                                    case QMCustomButton.Recievers.Offline:
                                        foreach (BasePlayer sleeper in BasePlayer.sleepingPlayerList)
                                            commandsToExec.AddRange(commands.Select(cmd => FormatCommandToUser(cmd, sleeper, admin)));
                                        break;
                                    case QMCustomButton.Recievers.Everyone:
                                        foreach (BasePlayer player in BasePlayer.allPlayerList)
                                            commandsToExec.AddRange(commands.Select(cmd => FormatCommandToUser(cmd, player, admin)));
                                        break;
                                    case QMCustomButton.Recievers.None:
                                        commandsToExec.AddRange(commands.Select(cmd => FormatCommand(cmd, admin)));
                                        break;
                                }

                                foreach (string cmd in commandsToExec)
                                {
                                    if (qmcb.ExecutionAsServer)
                                        ConsoleSystem.Run(ConsoleSystem.Option.Server, cmd);
                                    else
                                        admin.SendConsoleCommand(cmd);
                                }

                                if (toggleButton != null)
                                {
                                    toggleButton.Toggle(connectionData);
                                    connectionData.currentContent.Render(connectionData);
                                }

                                if (config.Logs.CustomButtons)
                                    LogToDiscord(connection, "Using a custom button", $"The administrator used a custom button with name **{qmcb.Label}** from quick menu!");
                                break;
                        }
                    }

                    break;
                case "convars.opensearch":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        (connectionData.currentContent as ConvarsContent)?.OpenSearch(connection);
                    break;
                case "convars.save":
                    if (ConvarsContent.SAVE_BUTTON.UserHasPermission(connection))
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.writecfg", Array.Empty<object>());
                        if (config.Logs.ConVars)
                            LogToDiscord(connection, "Config save", $"The administrator saved config with convars!");
                    }

                    break;
                case "convars.search.input":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string searchQuery = string.Empty;
                        if (args.Length > 0)
                            searchQuery = string.Join(" ", args);
                        connectionData.userData["convars.searchQuery"] = searchQuery;
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "convar.setvalue":
                    var convar = ConsoleGen.All.FirstOrDefault(c => c.FullName == args[0]);
                    if (convar != null)
                    {
                        convar.Set(string.Join(" ", args.Skip(1)));
                        convar.Saved = (convar.String != convar.DefaultValue);
                        connectionData = ConnectionData.Get(connection);
                        if (connectionData != null)
                            connectionData.currentContent?.Render(connectionData);
                        if (config.Logs.ConVars)
                            LogToDiscord(connection, "ConVar set value", $"The administrator set convar **{convar.FullName}** to **{convar.String}**!");
                    }

                    break;
                case "pluginmanager.opensearch":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        (connectionData.currentContent as PluginManagerContent)?.OpenSearch(connection);
                    break;
                case "pluginmanager.search.input":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string searchQuery = string.Empty;
                        if (args.Length > 0)
                            searchQuery = string.Join(" ", args);
                        connectionData.userData["pluginmanager.searchQuery"] = searchQuery;
                        connectionData.currentContent.Render(connectionData);
                    }

                    break;
                case "pluginmanager.check_commands":
                {
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string pluginName = args[0];
                        if (string.IsNullOrEmpty(pluginName))
                            return;
                        CUI.Root root = new CUI.Root("AdminMenu_Panel_TempContent");
                        string text = string.Empty;
                        IEnumerable<string> consoleCommands = GetConsoleCommands(pluginName);
                        if (consoleCommands.Count() > 0)
                            text += "Console Commands:\n" + string.Join(string.Empty, consoleCommands.Select(s => string.Format("   {0}\n", s)));
                        IEnumerable<string> chatCommands = GetChatCommands(pluginName);
                        if (chatCommands.Count() > 0)
                            text += "Chat Commands:\n" + string.Join(string.Empty, chatCommands.Select(s => string.Format("   {0}\n", s)));
                        new TextPopup($"{pluginName} Commands", text).AddUI(root, connectionData, connectionData.userData);
                        root.Render(connection);
                    }

                    break;
                }

                case "pluginmanager.check_permissions":
                {
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string pluginName = args[0];
                        if (string.IsNullOrEmpty(pluginName))
                            return;
                        CUI.Root root = new CUI.Root("AdminMenu_Panel_TempContent");
                        new TextPopup($"{pluginName} Permissions", string.Join("\n", Instance.GetPermissions(pluginName) ?? new string[0])).AddUI(root, connectionData, connectionData.userData);
                        root.Render(connection);
                    }

                    break;
                }

                case "pluginmanager.favorite":
                    if (args.Length > 1)
                        return;
                    if (!PluginManagerContent.FAVORITE_BUTTON.UserHasPermission(connection))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        if (config.FavoritePlugins.Add(args[0]))
                        {
                            SaveConfig();
                            connectionData.currentContent.Render(connectionData);
                        }

                        if (config.Logs.PluginManager)
                            LogToDiscord(connection, "Favorite plugin", $"The administrator favorite **{args[0]}** plugin!");
                    }

                    break;
                case "pluginmanager.unfavorite":
                    if (args.Length > 1)
                        return;
                    if (!PluginManagerContent.UNFAVORITE_BUTTON.UserHasPermission(connection))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        if (config.FavoritePlugins.Remove(args[0]))
                        {
                            SaveConfig();
                            connectionData.currentContent.Render(connectionData);
                        }

                        if (config.Logs.PluginManager)
                            LogToDiscord(connection, "Unfavorite plugin", $"The administrator unfavorite **{args[0]}** plugin!");
                    }

                    break;
                case "pluginmanager.load":
                    if (args.Length > 1)
                        return;
                    if (!PluginManagerContent.LOAD_BUTTON.UserHasPermission(connection))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        connectionData.userData["pluginmanager.lastusedplugin"] = args[0];
                    Oxide.Core.Interface.Oxide.LoadPlugin(args[0]);
                    if (config.Logs.PluginManager)
                        LogToDiscord(connection, "Load plugin", $"The administrator load **{args[0]}** plugin!");
                    break;
                case "pluginmanager.unload":
                    if (args.Length > 1)
                        return;
                    if (!PluginManagerContent.UNLOAD_BUTTON.UserHasPermission(connection))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        connectionData.userData["pluginmanager.lastusedplugin"] = args[0];
                    Oxide.Core.Interface.Oxide.UnloadPlugin(args[0]);
                    if (config.Logs.PluginManager)
                        LogToDiscord(connection, "Unload plugin", $"The administrator unload **{args[0]}** plugin!");
                    break;
                case "pluginmanager.reload":
                    if (args.Length > 1)
                        return;
                    if (!PluginManagerContent.RELOAD_BUTTON.UserHasPermission(connection))
                        return;
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        connectionData.userData["pluginmanager.lastusedplugin"] = args[0];
                    Oxide.Core.Interface.Oxide.ReloadPlugin(args[0]);
                    if (config.Logs.PluginManager)
                        LogToDiscord(connection, "Reload plugin", $"The administrator reload **{args[0]}** plugin!");
                    break;
                case "pluginmanager.reload_all":
                    if (!PluginManagerContent.RELOADALL_BUTTON.UserHasPermission(connection))
                        return;
#if CARBON
                    Carbon.Community.Runtime.ReloadPlugins();
#else
                    Oxide.Core.Interface.Oxide.ReloadAllPlugins();
#endif
                    if (config.Logs.PluginManager)
                        LogToDiscord(connection, "Reload all plugins", $"The administrator reload **all** plugins!");
                    break;
                case "changename":
                {
                    if (!UserHasPermission(connection.userid.ToString(), "adminmenu.changename"))
                        return;
                    BasePlayer admin2 = connection.player as BasePlayer;
                    string name2 = null;
                    if (args.Length > 0)
                    {
                        switch (args[0])
                        {
                            case "random":
                                IEnumerable<IPlayer> offlinePlayers = Instance.covalence.Players.All.Where(player => !player.IsConnected);
                                if (offlinePlayers.Count() > 1)
                                {
                                    IPlayer player = offlinePlayers.ElementAtOrDefault(UnityEngine.Random.Range(0, offlinePlayers.Count() - 1));
                                    if (player != null)
                                        name2 = player.Name;
                                }
                                else
                                {
                                    name2 = RandomUsernames.Get(UnityEngine.Random.Range(0, 34647853));
                                }

                                break;
                            case "reset":
                            default:
                                name2 = ServerMgr.Instance.persistance.GetPlayerName(connection.userid);
                                break;
                        }
                    }

                    admin2.displayName = name2;
                    admin2.SendNetworkUpdateImmediate(false);
                    if (admin2.net.group == BaseNetworkable.LimboNetworkGroup)
                    {
                        return;
                    }

                    List<Connection> list = Pool.GetList<Connection>();
                    for (int i = 0; i < Net.sv.connections.Count; i++)
                    {
                        Connection c = Net.sv.connections[i];
                        if (c.connected && c.isAuthenticated && c.player is BasePlayer && c.player != admin2)
                        {
                            list.Add(connection);
                        }
                    }

                    admin2.OnNetworkSubscribersLeave(list);
                    Pool.FreeList<Connection>(ref list);
                    if (admin2.limitNetworking)
                    {
                        return;
                    }

                    admin2.syncPosition = false;
                    admin2._limitedNetworking = true;
                    Interface.Oxide.NextTick(delegate
                    {
                        admin2.syncPosition = true;
                        admin2._limitedNetworking = false;
                        admin2.UpdateNetworkGroup();
                        admin2.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    });
                    break;
                }

                default:
                    break;
            }
        }

        void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_FULLACCESS, this);
            permission.RegisterPermission(PERMISSION_CONVARS, this);
            permission.RegisterPermission(PERMISSION_PERMISSIONMANAGER, this);
            permission.RegisterPermission(PERMISSION_PLUGINMANAGER, this);
            permission.RegisterPermission(PERMISSION_GIVE, this);
            permission.RegisterPermission(PERMISSION_USERINFO_IP, this);
            permission.RegisterPermission(PERMISSION_USERINFO_STEAMINFO, this);
            switch (config.ButtonToHook)
            {
                case ButtonHook.X:
                    cmd.AddConsoleCommand("swapseats", this, "swapseats_hook");
                    break;
                case ButtonHook.F:
                    cmd.AddConsoleCommand("lighttoggle", this, "lighttoggle_hook");
                    break;
            }

            cmd.AddChatCommand(config.ChatCommand, this, "adminmenu_chatcmd");
            FormatMainMenu();
            FormatPanelList();
            Unsubscribe(nameof(OnPlayerLootEnd));
            Unsubscribe(nameof(CanMoveItem));
        }

        void OnServerInitialized()
        {
            ADMINMENU_IMAGECRC = FileStorage.server.Store(Convert.FromBase64String(ADMINMENU_IMAGEBASE64), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
            lang.RegisterMessages(defaultLang, this);
            foreach (var pair in ConnectionData.all)
            {
                Connection connection = pair.Key;
                ConnectionData data = pair.Value;
                if (connection?.connected == true && data.IsDestroyed)
                    data.Init();
            }
        }

        void Loaded()
        {
            foreach (Type type in this.GetType().GetNestedTypes(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object[] attribute = type.GetCustomAttributes(typeof(HarmonyPatch), false);
                if (attribute.Length >= 1)
                {
                    PatchClassProcessor patchClassProcessor = this.HarmonyInstance.CreateClassProcessor(type);
                    patchClassProcessor.Patch();
                }
            }
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                ConnectionData.Get(player)?.UI.DestroyAll();
            Button.all.Clear();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            ConnectionData.Get(player)?.Dispose();
        }

        void OnPluginLoaded(Plugin plugin)
        {
            string fileName = Path.GetFileNameWithoutExtension(plugin.Filename);
            foreach (ConnectionData connectionData in ConnectionData.all.Values)
            {
                if (!connectionData.connection.connected)
                    continue;
                if (connectionData.currentContent is PluginManagerContent content)
                {
                    string[] array = (string[])connectionData.userData["pluginmanager.array"];
                    if (array.Contains(fileName))
                        content.UpdateForPlugin(connectionData, fileName);
                    else
                        connectionData.currentContent.Render(connectionData);
                }
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            OnPluginLoaded(plugin);
        }

        object OnMapMarkerAdd(BasePlayer player, ProtoBuf.MapNote note)
        {
            ConnectionData connectionData = ConnectionData.Get(player);
            if (connectionData == null)
                return null;
            Button teleportToMarkerButton = (panelList["quickmenu"].DefaultContent as QuickMenuContent).buttonGrid.Find(b => b.button.Args[0] == "toggle_teleport_to_marker")?.button;
            if (teleportToMarkerButton == null)
                return null;
            if (teleportToMarkerButton.GetState(connectionData) == Button.State.Toggled)
            {
                float y = TerrainMeta.HeightMap.GetHeight(note.worldPosition) + 2.5f;
                float highestPoint = TerrainMeta.HighestPoint.y + 250f;
                RaycastHit[] hits = Physics.RaycastAll(note.worldPosition.WithY(highestPoint), Vector3.down, ++highestPoint, Layers.Mask.World | Layers.Mask.Terrain | Layers.Mask.Default, QueryTriggerInteraction.Ignore);
                if (hits.Length > 0)
                {
                    GamePhysics.Sort(hits);
                    y = hits.Max(hit => hit.point.y);
                }

                if (player.IsFlying)
                    y = Mathf.Max(y, player.transform.position.y);
                player.Teleport(note.worldPosition.WithY(y));
                return false;
            }

            return null;
        }

        void OnPlayerLootEnd(PlayerLoot loot)
        {
            RemoveViewingBackpack(loot);
        }

        object CanMoveItem(Item item, PlayerInventory playerInventory, ItemContainerId targetContainer, int targetSlot, int amount, ItemMoveModifier itemMoveModifier)
        {
            if (!item.IsBackpack())
                return null;
            PlayerLoot loot = playerInventory.loot;
            if (!loot.IsLooting() || loot.entitySource != RelationshipManager.ServerInstance || !viewingBackpacks.TryGetValue(loot, out Item backpackItem) || backpackItem != item)
                return null;
            if (item.IsBackpack())
            {
                ItemContainer contents = item.contents;
                if (contents != null && !contents.IsEmpty())
                {
                    ViewContainer(loot.baseEntity, contents);
                    return false;
                }
            }

            return null;
        }

        void FormatMainMenu()
        {
            mainMenu = new MainMenu
            {
                NavButtons = new ButtonArray
                {
                    new HideButton("BACK", "back"),
                    null,
                    new Button("QUICK MENU", "openpanel", "quickmenu"),
                    new Button("PLAYER LIST", "openpanel", "playerlist"),
                    new Button("CONVARS", "openpanel", "convars")
                    {
                        Permission = "convars"
                    },
                    new Button("PERMISSION MANAGER", "openpanel", "permissionmanager")
                    {
                        Permission = "permissionmanager"
                    },
                    new Button("PLUGIN MANAGER", "openpanel", "pluginmanager")
                    {
                        Permission = "pluginmanager"
                    },
                    null,
                    new Button("GIVE SELF", "givemenu.open", "self")
                    {
                        Permission = "give"
                    },
                    new Button("SELECT LAST USER", "userinfo.open", "last"),
                    null,
                    new Button("CLOSE", "close"),
                }
            };
        }

        private void OpenUserInfoAtCrosshair(BasePlayer player)
        {
            if (player == null)
                return;
            ConnectionData connectionData = ConnectionData.Get(player.Connection);
            if (connectionData == null)
                return;
            ulong lastUserId = (ulong)connectionData.userData["userinfo.lastuserid"];
            Ray ray = player.eyes.HeadRay();
            RaycastHit raycastHit;
            if (Physics.Raycast(ray, out raycastHit, 10, 1218652417))
            {
                BasePlayer hitPlayer = null;
                BaseEntity hitEntity = raycastHit.GetEntity();
                if (hitEntity != null)
                {
                    hitPlayer = hitEntity as BasePlayer;
                    if (hitPlayer == null)
                    {
                        BaseVehicle hitVehicle = hitEntity as BaseVehicle;
                        if (hitVehicle != null)
                        {
                            hitPlayer = hitVehicle.GetMounted();
                        }
                    }
                }

                if (hitPlayer == null)
                {
                    List<BasePlayer> list = Facepunch.Pool.GetList<global::BasePlayer>();
                    Vis.Entities<BasePlayer>(raycastHit.point, 3, list, 131072, QueryTriggerInteraction.UseGlobal);
                    list = list.Where(basePlayer => basePlayer != null && !basePlayer.IsNpc && basePlayer.userID.IsSteamId() && basePlayer.userID != player.userID && basePlayer.userID != lastUserId).ToList();
                    hitPlayer = list.GetRandom();
                    Facepunch.Pool.FreeList<BasePlayer>(ref list);
                }

                if (hitPlayer == null || hitPlayer.IsNpc || !hitPlayer.userID.IsSteamId())
                    return;
                if (hitPlayer.userID == player.userID)
                    return;
                HandleCommand(player.Connection, "userinfo.open", hitPlayer.UserIDString);
            }
        }

        private void ToggleMenu(BasePlayer player)
        {
            Connection connection = player.Connection;
            if (connection == null)
                return;
            HandleCommand(connection, "");
        }

        private void RequestSteamInfo(ulong steamId, out SteamInfo steamInfo, Action<SteamInfo> callback, bool force_update = false)
        {
            if (!force_update && cachedSteamInfo.TryGetValue(steamId, out steamInfo))
                return;
            steamInfo = null;
            cachedSteamInfo.Remove(steamId);
            webrequest.Enqueue($"https://steamcommunity.com/profiles/{steamId}?xml=1", null, (code, response) =>
            {
                if (code != 200 || response == null)
                    return;
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(response);
                string location = xmlDoc.SelectSingleNode("//location")?.InnerText.Trim();
                string avatarLow = xmlDoc.SelectSingleNode("//avatarIcon")?.InnerText.Trim();
                string avatarMedium = xmlDoc.SelectSingleNode("//avatarMedium")?.InnerText.Trim();
                string avatarFull = xmlDoc.SelectSingleNode("//avatarFull")?.InnerText.Trim();
                string memberSince = xmlDoc.SelectSingleNode("//memberSince")?.InnerText.Trim();
                XmlNode mostPlayedGameNode = xmlDoc.SelectSingleNode("//mostPlayedGames/mostPlayedGame[contains(gameLink, 'https://steamcommunity.com/app/252490')]");
                string hoursOnRecord = mostPlayedGameNode?.SelectSingleNode("hoursOnRecord")?.InnerText.Trim().Replace(",", "");
                SteamInfo steamInfo = new SteamInfo
                {
                    Location = location,
                    Avatars = new string[3]
                    {
                        avatarLow,
                        avatarMedium,
                        avatarFull
                    },
                    RegistrationDate = memberSince,
                    RustHours = hoursOnRecord
                };
                cachedSteamInfo[steamId] = steamInfo;
                if (callback != null && steamInfo != null)
                    callback(steamInfo);
            }, this);
        }

        public long GetTimestamp(string timestampSTR, long def = 0L)
        {
            if (timestampSTR == null)
            {
                return def;
            }

            int num = 3600;
            if (timestampSTR.Length > 1 && char.IsLetter(timestampSTR[timestampSTR.Length - 1]))
            {
                char c = timestampSTR[timestampSTR.Length - 1];
                if (c <= 'd')
                {
                    if (c != 'M')
                    {
                        if (c != 'Y')
                        {
                            if (c == 'd')
                            {
                                num = 86400;
                            }
                        }
                        else
                        {
                            num = 31536000;
                        }
                    }
                    else
                    {
                        num = 2592000;
                    }
                }
                else if (c <= 'm')
                {
                    if (c != 'h')
                    {
                        if (c == 'm')
                        {
                            num = 60;
                        }
                    }
                    else
                    {
                        num = 3600;
                    }
                }
                else if (c != 's')
                {
                    if (c == 'w')
                    {
                        num = 604800;
                    }
                }
                else
                {
                    num = 1;
                }

                timestampSTR = timestampSTR.Substring(0, timestampSTR.Length - 1);
            }

            long num2;
            if (long.TryParse(timestampSTR, out num2))
            {
                if (num2 > 0L && num2 <= 315360000L)
                {
                    num2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + num2 * (long)num;
                }

                return num2;
            }

            return def;
        }

        private bool CanUseAdminMenu(Connection connection)
        {
            return UserHasPermission(connection.userid.ToString(), PERMISSION_USE);
        }

        private bool CanUseAdminMenu(BasePlayer player)
        {
            return CanUseAdminMenu(player.Connection);
        }

        public bool UserHasPermission(string userId, string permission)
        {
            if (!permission.StartsWith("adminmenu."))
                permission = $"adminmenu.{permission}";
            return Instance.permission.UserHasPermission(userId, PERMISSION_FULLACCESS) || Instance.permission.UserHasPermission(userId, permission);
        }

        private void LogToDiscord(Connection author, string title, string content, string thumbnail = null, string image = null)
        {
            if (string.IsNullOrEmpty(config.Logs.WebhookURL))
                return;
            string jsonString = @$"{{
                  ""content"": null,
                  ""embeds"": [
                    {{
                      ""title"": ""{title}"",
                      ""description"": ""{content.Replace("\"", "\\\"").Replace("\n", "\\n")}"",
                      ""color"": 56266,
                      ""author"": {{
                        ""name"": ""{UserInfoContent.GetDisplayName(covalence.Players.FindPlayerById(author.userid.ToString()))}"",
                        ""url"": ""https://steamcommunity.com/profiles/{author.userid}""
                      }},
                      ""thumbnail"": {{
                        ""url"": ""{thumbnail}""
                      }},
                      ""image"": {{
                        ""url"": ""{image}""
                      }}
                    }}
                  ],
                  ""attachments"": []
                }}";
            webrequest.Enqueue(config.Logs.WebhookURL, jsonString, DiscordSendMessageCallback, this, RequestMethod.POST, HEADERS);
        }

        private void DiscordSendMessageCallback(int code, string message)
        {
            switch (code)
            {
                case 204:
                {
                    return;
                }

                case 401:
                    var objectJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                    int messageCode;
                    if (objectJson["code"] != null && int.TryParse(objectJson["code"].ToString(), out messageCode))
                        if (messageCode == 50027)
                        {
                            PrintError("Invalid Webhook Token");
                            return;
                        }

                    break;
                case 404:
                    PrintError("Invalid Webhook (404: Not Found)");
                    return;
                case 405:
                    PrintError("Invalid Webhook (405: Method Not Allowed)");
                    return;
                case 429:
                    message = "You are being rate limited. To avoid this try to increase queue interval in your config file.";
                    break;
                case 500:
                    message = "There are some issues with Discord server (500 Internal Server Error)";
                    break;
                case 502:
                    message = "There are some issues with Discord server (502 Bad Gateway)";
                    break;
                default:
                    message = $"DiscordSendMessageCallback: code = {code} message = {message}";
                    break;
            }

            PrintError(message);
        }

        private BasePlayer GetSpectatingTarget(BasePlayer spectator)
        {
            if (!spectator.IsSpectating())
                return null;
            return spectator.parentEntity.Get(true) as BasePlayer;
        }

        private string[] GetPermissions(string pluginName)
        {
#if CARBON
            HashSet<string> permissions = permission.permset.FirstOrDefault(p => p.Key.Name == pluginName).Value;
#else
            HashSet<string> permissions = ((Dictionary<Core.Plugins.Plugin, HashSet<string>>)PERMISSIONS_DICTIONARY_FIELD.GetValue(permission)).FirstOrDefault(p => p.Key.Name == pluginName).Value;
#endif
            return permissions?.ToArray();
        }

        private string[] GetConsoleCommands(string pluginName)
        {
            List<string> commands = new List<string>();
#if CARBON
            foreach (API.Commands.Command command in Carbon.Community.Runtime.CommandManager.ClientConsole)
            {
                Carbon.Base.BaseHookable plugin = (Carbon.Base.BaseHookable)command.Reference;
                if (plugin == null)
                   continue;
                if (plugin.Name == pluginName)
                    commands.Add(command.Name);
            }
#else
            var dictionary = (IDictionary)CONSOLECOMMANDS_DICTIONARY_FIELD.GetValue(cmd);
            foreach (DictionaryEntry entry in dictionary)
            {
                string command = (string)entry.Key;
                object consoleCommand = entry.Value;
                object callback = CONSOLECOMMAND_CALLBACK_FIELD.GetValue(consoleCommand);
                Plugin plugin = (Plugin)PLUGINCALLBACK_PLUGIN_FIELD.GetValue(callback);
                if (plugin.Name == pluginName)
                    commands.Add(command);
            }

#endif
            return commands.ToArray();
        }

        private string[] GetChatCommands(string pluginName)
        {
            List<string> commands = new List<string>();
#if CARBON
            foreach (API.Commands.Command command in Carbon.Community.Runtime.CommandManager.Chat)
            {
                Carbon.Base.BaseHookable plugin = (Carbon.Base.BaseHookable)command.Reference;
                if (plugin == null)
                   continue;
                if (plugin.Name == pluginName)
                    commands.Add(command.Name);
            }
#else
            var dictionary = (IDictionary)CHATCOMMANDS_DICTIONARY_FIELD.GetValue(cmd);
            foreach (DictionaryEntry entry in dictionary)
            {
                string command = (string)entry.Key;
                object chatCommand = entry.Value;
                Plugin plugin = (Plugin)CHATCOMMAND_PLUGIN_FIELD.GetValue(chatCommand);
                if (plugin.Name == pluginName)
                    commands.Add(command);
            }

#endif
            return commands.ToArray();
        }

        void AddViewingBackpack(PlayerLoot loot, Item item)
        {
            viewingBackpacks.Add(loot, item);
            Subscribe(nameof(OnPlayerLootEnd));
            Subscribe(nameof(CanMoveItem));
        }

        void RemoveViewingBackpack(PlayerLoot loot)
        {
            viewingBackpacks.Remove(loot);
            if (viewingBackpacks.Count == 0)
            {
                Unsubscribe(nameof(OnPlayerLootEnd));
                Unsubscribe(nameof(CanMoveItem));
            }
        }

        void ViewContainer(BasePlayer player, ItemContainer container)
        {
            PlayerLoot playerLoot = player.inventory.loot;
            bool IsLooting = playerLoot.IsLooting();
            playerLoot.containers.Clear();
            playerLoot.entitySource = null;
            playerLoot.itemSource = null;
            if (IsLooting)
                playerLoot.SendImmediate();
            NextFrame(() =>
            {
                playerLoot.PositionChecks = false;
                playerLoot.entitySource = RelationshipManager.ServerInstance;
                playerLoot.AddContainer(container);
                playerLoot.SendImmediate();
                player.ClientRPC<string>(RpcTarget.Player("RPC_OpenLootPanel", player), "generic_resizable");
            });
        }

        void UpdateOneActiveImageElement(Connection connection, string baseId, int activeButtonIndex, int buttonCount, string activeColor, string disableColor) => UpdateOneActiveElement(connection, baseId, activeButtonIndex, buttonCount, activeColor, disableColor, true);
        void UpdateOneActiveTextElement(Connection connection, string baseId, int activeButtonIndex, int buttonCount, string activeColor, string disableColor) => UpdateOneActiveElement(connection, baseId, activeButtonIndex, buttonCount, activeColor, disableColor, false);
        void UpdateOneActiveElement(Connection connection, string baseId, int activeButtonIndex, int buttonCount, string activeColor, string disableColor, bool isImage)
        {
            CUI.Root root = new CUI.Root();
            for (int i = 0; i < buttonCount; i++)
            {
                CUI.Element element = new CUI.Element
                {
                    Name = $"{baseId}{i}"};
                if (isImage)
                {
                    element.Components.Add(new CuiImageComponent { Color = (i == activeButtonIndex ? activeColor : disableColor) });
                }
                else
                {
                    element.Components.Add(new CuiTextComponent { Color = (i == activeButtonIndex ? activeColor : disableColor) });
                }

                root.Add(element);
            }

            root.Update(connection);
        }

        void FormatPanelList()
        {
            Button.all.Clear();
            ButtonArray<CategoryButton> givemenuCategories = new ButtonArray<CategoryButton>()
            {
                new CategoryButton("ALL", "showcontent", "all")
            };
            string[] categoryNames = Enum.GetNames(typeof(ItemCategory));
            for (int i = 0; i < categoryNames.Length; i++)
            {
                string categoryName = categoryNames[i];
                if (categoryName == "All" || categoryName == "Search" || categoryName == "Favourite" || categoryName == "Common")
                    continue;
                givemenuCategories.Add(new CategoryButton(categoryName.ToUpper(), "givemenu.filter", i.ToString()));
            }

            GiveMenuContent giveMenuContent = new GiveMenuContent();
            QuickMenuContent quickMenuContent = new QuickMenuContent()
            {
                buttonGrid = new ButtonGrid<Button>()
                {
                    new ButtonGrid<Button>.Item(0, 0, new Button("Teleport to 0 0 0", "quickmenu.action", "teleportto_000") { Permission = "quickmenu.teleportto000" }),
                    new ButtonGrid<Button>.Item(0, 1, new Button("Teleport to\nDeathpoint", "quickmenu.action", "teleportto_deathpoint") { Permission = "quickmenu.teleporttodeath" }),
                    new ButtonGrid<Button>.Item(0, 2, new Button("Teleport to\nSpawn point", "quickmenu.action", "teleportto_randomspawnpoint") { Permission = "quickmenu.teleporttospawnpoint" }),
                    new ButtonGrid<Button>.Item(0, 3, new ToggleButton("Teleport to Marker", "quickmenu.action", "toggle_teleport_to_marker") { Permission = "quickmenu.teleport_to_marker" }),
                    new ButtonGrid<Button>.Item(1, 0, new Button("Kill Self", "quickmenu.action", "killself")),
                    new ButtonGrid<Button>.Item(1, 1, new Button("Heal Self", "quickmenu.action", "healself") { Permission = "quickmenu.healself" }),
                    new ButtonGrid<Button>.Item(1, 2, new Button("Time to 12", "quickmenu.action", "settime", "12") { Permission = "quickmenu.settime" }),
                    //new ButtonGrid<Button>.Item(1, 3, new Button("Random Nickname\n(Beta)", "changename", "random") { Permission = "changename" }),
                    new ButtonGrid<Button>.Item(2, 0, new Button("Giveaway\nto online players", "quickmenu.action", "giveaway_online") { Permission = "quickmenu.giveaway" }),
                    new ButtonGrid<Button>.Item(2, 1, new Button("Giveaway\nto everyone", "quickmenu.action", "giveaway_everyone") { Permission = "quickmenu.giveaway" }),
                    new ButtonGrid<Button>.Item(2, 2, new Button("Time to 0", "quickmenu.action", "settime", "0") { Permission = "quickmenu.settime" }),
                    //new ButtonGrid<Button>.Item(2, 3, new Button("Reset Nickname", "changename", "reset") { Permission = "changename" }),
                    new ButtonGrid<Button>.Item(3, 0, new Button("Call Heli", "quickmenu.action", "helicall") { Permission = "quickmenu.helicall" }),
                    new ButtonGrid<Button>.Item(3, 1, new Button("Spawn Bradley", "quickmenu.action", "spawnbradley") { Permission = "quickmenu.spawnbradley" }),
                    new ButtonGrid<Button>.Item(3, 2, new Button("Spawn Cargo", "quickmenu.action", "spawncargo") { Permission = "quickmenu.spawncargo" }),
                }
            };
            foreach (QMCustomButton cb in config.CustomQuickButtons)
            {
                int row = cb.Position[0];
                int index = cb.Position[1];
                ButtonGrid<Button>.Item existButton = quickMenuContent.buttonGrid.Find(t => t.row == row && t.column == index);
                if (existButton != null)
                {
                    foreach (var item in quickMenuContent.buttonGrid)
                    {
                        if (item.row == row && item.column >= index)
                            item.column++;
                    }
                }

                quickMenuContent.buttonGrid.Add(new ButtonGrid<Button>.Item(row, index, cb.Button));
            }

            UserInfoContent userInfoContent = new UserInfoContent()
            {
                buttonGrid = new ButtonGrid<Button>()
                {
                    new ButtonGrid<Button>.Item(0, 0, new Button("Teleport Self To", "userinfo.action", "teleportselfto") { Permission = "userinfo.teleportselfto" }),
                    new ButtonGrid<Button>.Item(0, 1, new Button("Teleport To Self", "userinfo.action", "teleporttoself") { Permission = "userinfo.teleporttoself" }),
                    new ButtonGrid<Button>.Item(0, 2, new Button("Teleport To Auth", "userinfo.action", "teleporttoauth") { Permission = "userinfo.teleporttoauth" }),
                    new ButtonGrid<Button>.Item(1, 0, new Button("Heal", "userinfo.action", "heal") { Permission = "userinfo.fullheal" }),
                    new ButtonGrid<Button>.Item(1, 1, new Button("Heal 50%", "userinfo.action", "heal50") { Permission = "userinfo.halfheal" }),
                    new ButtonGrid<Button>.Item(1, 2, new Button("Teleport to\nDeathpoint", "userinfo.action", "teleporttodeathpoint") { Permission = "userinfo.teleporttodeath" }),
                    new ButtonGrid<Button>.Item(2, 0, new Button("View Inventory", "userinfo.action", "viewinv") { Permission = "userinfo.viewinv" }),
                    new ButtonGrid<Button>.Item(2, 1, new Button("Unlock Blueprints", "userinfo.action", "unlockblueprints") { Permission = "userinfo.unlockblueprints" }),
                    new ButtonGrid<Button>.Item(2, 2, new Button("Spectate", "userinfo.action", "spectate") { Permission = "userinfo.spectate" }),
                    new ButtonGrid<Button>.Item(3, 0, new Button("Mute", "userinfo.action", "mute") { Permission = "userinfo.mute" }),
                    new ButtonGrid<Button>.Item(3, 1, new Button("Unmute", "userinfo.action", "unmute") { Permission = "userinfo.unmute" }),
                    new ButtonGrid<Button>.Item(3, 2, new ConditionToggleButton("Toggle Creative", "userinfo.action", "creative") { Permission = "userinfo.creative", Condition = (ConnectionData connectionData) => BasePlayer.FindAwakeOrSleeping(connectionData.userData["userinfo.userid"].ToString())?.IsInCreativeMode ?? false }),
                    new ButtonGrid<Button>.Item(5, 0, new ConditionToggleButton("Cuff", "userinfo.action", "cuff") { Permission = "userinfo.cuff", Condition = (ConnectionData connectionData) => BasePlayer.FindAwakeOrSleeping(connectionData.userData["userinfo.userid"].ToString())?.IsRestrained ?? false }),
                    new ButtonGrid<Button>.Item(5, 1, new Button("<color=olive>Strip Inventory</color>", "userinfo.action", "stripinventory") { Permission = "userinfo.stripinventory" }),
                    new ButtonGrid<Button>.Item(5, 2, new Button("<color=olive>Revoke Blueprints</color>", "userinfo.action", "revokeblueprints") { Permission = "userinfo.revokeblueprints" }),
                    new ButtonGrid<Button>.Item(6, 0, new Button("Kill", "userinfo.action", "kill") { Permission = "userinfo.kill" }),
                    new ButtonGrid<Button>.Item(6, 1, new Button("<color=red>Kick</color>", "userinfo.action", "kick", "showpopup") { Permission = "userinfo.kick" }),
                    new ButtonGrid<Button>.Item(6, 2, new Button("<color=red>Ban</color>", "userinfo.action", "ban", "showpopup") { Permission = "userinfo.ban" }),
                }
            };
            foreach (UserInfoCustomButton cb in config.UserInfoCustomButtons)
            {
                int row = cb.Position[0];
                int index = cb.Position[1];
                ButtonGrid<Button>.Item existButton = userInfoContent.buttonGrid.Find(t => t.row == row && t.column == index);
                if (existButton != null)
                {
                    foreach (var item in userInfoContent.buttonGrid)
                    {
                        if (item.row == row && item.column >= index)
                            item.column++;
                    }
                }

                userInfoContent.buttonGrid.Add(new ButtonGrid<Button>.Item(row, index, cb.Button));
            }

            GroupInfoContent groupInfoContent = new GroupInfoContent
            {
                buttons = new ButtonArray[1]
                {
                    new ButtonArray
                    {
                        new Button("<color=#dd0000>Remove Group</color>", "groupinfo.action", "remove")
                        {
                            Permission = "groupinfo.removegroup"
                        },
                        new Button("<color=olive>Clone Group</color>", "groupinfo[popup:clonegroup]", "show")
                        {
                            Permission = "groupinfo.clonegroup"
                        },
                    }
                }
            };
            panelList = new Dictionary<string, Panel>()
            {
                {
                    "empty",
                    new Panel
                    {
                        Sidebar = null,
                        Content = null
                    }
                },
                {
                    "quickmenu",
                    new Panel
                    {
                        Sidebar = null,
                        Content = new Dictionary<string, Content>()
                        {
                            {
                                "default",
                                quickMenuContent
                            }
                        }
                    }
                },
                {
                    "permissionmanager",
                    new Panel
                    {
                        Sidebar = null,
                        Content = new Dictionary<string, Content>
                        {
                            {
                                "default",
                                new NewPermissionManagerContent()
                            },
                            {
                                "groups",
                                new GroupListContent()
                            },
                            {
                                "players",
                                new PlayerListContent()
                            },
                        }
                    //Sidebar = new Sidebar
                    //{
                    //    CategoryButtons = new ButtonArray<CategoryButton>
                    //    {
                    //        new CategoryButton("GROUPS", "showcontent", "groups"),
                    //        new CategoryButton("USERS", "showcontent", "users"),
                    //    }
                    //},
                    //Content = new Dictionary<string, Content>
                    //{
                    //    {
                    //        "groups", new GroupListContent()
                    //    },
                    //    {
                    //        "users", new PlayerListContent()
                    //    }
                    //}
                    }
                },
                {
                    "pluginmanager",
                    new Panel
                    {
                        Sidebar = null,
                        Content = new Dictionary<string, Content>
                        {
                            {
                                "default",
                                new PluginManagerContent()
                            }
                        }
                    }
                },
                {
                    "permissions",
                    new PermissionPanel
                    {
                        Content = new Dictionary<string, Content>
                        {
                            {
                                "default",
                                new CenteredTextContent()
                                {
                                    text = "Please select the plugin from left side",
                                }
                            }
                        }
                    }
                },
                {
                    "userinfo",
                    new Panel
                    {
                        Sidebar = new Sidebar()
                        {
                            CategoryButtons = new ButtonArray<CategoryButton>
                            {
                                new CategoryButton("INFO", "showcontent", "info"),
                                new CategoryButton("GIVE", "userinfo.givemenu.open")
                                {
                                    Permission = "give"
                                },
                                new CategoryButton("PERMISSIONS", "userinfo.permissions")
                                {
                                    Permission = "permissionmanager"
                                }
                            }
                        },
                        Content = new Dictionary<string, Content>()
                        {
                            {
                                "info",
                                userInfoContent
                            },
                            {
                                "give",
                                giveMenuContent
                            }
                        }
                    }
                },
                {
                    "groupinfo",
                    new Panel
                    {
                        Sidebar = new Sidebar()
                        {
                            CategoryButtons = new ButtonArray<CategoryButton>
                            {
                                new CategoryButton("INFO", "showcontent", "info"),
                                new CategoryButton("USERS", "groupinfo.users.open"),
                                new CategoryButton("PERMISSIONS", "groupinfo.permissions")
                                {
                                    Permission = "permissionmanager"
                                }
                            }
                        },
                        Content = new Dictionary<string, Content>()
                        {
                            {
                                "info",
                                groupInfoContent
                            },
                            {
                                "users",
                                new PlayerListContent()
                            }
                        }
                    }
                },
                {
                    "playerlist",
                    new Panel
                    {
                        Sidebar = new Sidebar
                        {
                            CategoryButtons = new ButtonArray<CategoryButton>
                            {
                                new CategoryButton("ONLINE", "playerlist.filter", "online"),
                                new CategoryButton("OFFLINE", "playerlist.filter", "offline"),
                                new CategoryButton("BANNED", "playerlist.filter", "banned"),
                                new CategoryButton("ADMINS", "playerlist.filter", "admins"),
                                new CategoryButton("MODERS", "playerlist.filter", "moders"),
                                new CategoryButton("ALL", "playerlist.filter", "all"),
                            },
                        },
                        Content = new Dictionary<string, Content>
                        {
                            {
                                "default",
                                new PlayerListContent()
                            }
                        }
                    }
                },
                {
                    "givemenu",
                    new Panel
                    {
                        Sidebar = new Sidebar
                        {
                            CategoryButtons = givemenuCategories
                        },
                        Content = new Dictionary<string, Content>()
                        {
                            {
                                "all",
                                giveMenuContent
                            }
                        }
                    }
                },
                {
                    "convars",
                    new Panel
                    {
                        Sidebar = null,
                        Content = new Dictionary<string, Content>()
                        {
                            {
                                "default",
                                new ConvarsContent()
                            }
                        }
                    }
                },
                {
                    "info",
                    new Panel
                    {
                        Sidebar = null,
                        Content = new Dictionary<string, Content>()
                        {
                            {
                                "custom_buttons",
                                new TextContent()
                                {
                                    text = "<b><size=16>CUSTOM BUTTONS</size></b>\n" + "Custom buttons are buttons that when pressed will execute commands on behalf of the administrator, commands can be several, below will be the details.\n" + "At the moment, custom buttons can be created in two places: in the quick menu and in the menu when selecting a player. These places are separated in the config.\n" + "\n<b><size=14>Button Fields:</size></b>\n" + "   'Label' - Text that will be on the button, for each such inscription creates a field in the lang file, accordingly you can translate this text into several languages.\n" + "   'Commands' - Array of commands that will be executed on behalf of the administrator. Chat commands require a special entry, see examples.\n" + "   'Permission' - permission to display and use the button. You need to write the permission that will be after   '<b>adminmenu.</b>'. Example: if you enter the permission 'test' the permission will be <b>adminmenu.test</b>\n" + "   'Position' - The location for the button, the first number is responsible for the row number, the second for the position within the row, starts from 0.\nThere are limits of locations, if you have gone beyond the limit - the button will not be displayed.\n" + "   Common Tags:\n" + "        <b>​​​​​​​​​​​​​​{adminUID}</b> - administrator's id.\n" + "        <b>​​​​​​​​​​​​​​{position}</b> - administrator coordinates (underfoot)\n" + "        <b>{view_position}</b> - the position where the administrator is looking (can be used for spawning or something similar)\n" + "        <b>{view_direction_forward}</b> - forward view direction of the administrator\n" + "        <b>{view_direction_backward}</b> - the direction the administrator is looking backwards\n" + "        <b>{view_direction_left}</b> - direction of the administrator’s view to the left\n" + "        <b>{view_direction_right}</b> - administrator’s view direction to the right\n" + "   Tags for target only:\n" + "        <b>{steamid}, {steamID}, {userID}, {STEAMID}, {USERID}</b> - mean the same thing, namely the id of the selected player.\n" + "        <b>{target_position}</b> - target coordinates (underfoot)\n" + "\n<b><size=14>Example of commands:</size></b>\n" + "chat.say \"/{chat command}\"\n" + "vanish\n" + "ban {steamID}\n" + "teleport {steamID} {adminUID}\n",
                                    font = CUI.Font.RobotoMonoRegular,
                                    fontSize = 12,
                                    allowCopy = true,
                                }
                            }
                        }
                    }
                }
            };
        }

        private void swapseats_hook(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;
            Connection connection = player.Connection;
            if (connection != null && CanUseAdminMenu(connection) && !player.isMounted)
                ToggleMenu(player);
            else
                ConVar.vehicle.swapseats(arg);
        }

        private void lighttoggle_hook(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;
            Connection connection = player.Connection;
            if (connection != null && CanUseAdminMenu(connection) && !player.IsDucked())
                ToggleMenu(player);
            else
                ConVar.Inventory.lighttoggle_sv(arg);
        }

        public interface IUIModule
        {
            public abstract CUI.Element AddUI(CUI.Element parent, ConnectionData connectionData, Dictionary<string, object> userData);
        }
    }
}