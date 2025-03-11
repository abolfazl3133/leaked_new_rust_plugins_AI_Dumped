using Facepunch;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("Giveaways", "k1lly0u", "2.0.13")]
    [Description("Create a raffle in which players can win prizes of your choosing")]
    class Giveaways : RustPlugin
    {
        #region Fields
        private StoredData storedData;
        private DynamicConfigFile data;

        [PluginReference]
        private Plugin ServerRewards, Economics, Kits, UINotify;

        private static Giveaways Instance { get; set; }

        private const string UI_PANEL = "gaui.panel";

        private bool isRunning = false;

        private bool wipeData = false;

        private NotificationMode notificationMode;

        private enum NotificationMode { Chat, UI, Hint, UINotify }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("giveaways_data");
            Instance = this;

            lang.RegisterMessages(Messages, this);
        }

        private void OnNewSave(string filename)
        {
            if (configData.WipeData)
                wipeData = true;
        }

        private void OnServerInitialized()
        {
            LoadData();

            notificationMode = ParseType<NotificationMode>(configData.Notification.Mode);

            if (configData.Auto.Enabled && configData.Auto.Interval > 0)
                timer.In(configData.Auto.Interval, TimedEventStart);

            if (configData.Notification.NotificationInterval > 0)
                timer.In(configData.Notification.NotificationInterval, TimedNotification);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;

            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(2, () => OnPlayerConnected(player));
                return;
            }

            if (storedData.HasRewards(player.userID))
                player.ChatMessage(msg("Notification.OnInit", player.userID));
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            DestroyUI();

            configData = null;
            Instance = null;            
        }
        #endregion

        #region Functions
        private void TimedNotification()
        {
            SendChat("Notification.Info", string.Empty, string.Empty);

            timer.In(configData.Notification.NotificationInterval, TimedNotification);
        }

        private void TimedEventStart()
        {
            if (!isRunning)
            {
                if (BasePlayer.activePlayerList.Count >= configData.Auto.MinimumPlayers)                
                    RunEvent(configData.Auto.Items.GetRandom());                
            }

            timer.In(configData.Auto.Interval, TimedEventStart);
        }

        private void RunEvent(Item item) => ProcessEvent(new StoredData.ItemData(item));
        
        private void RunEvent(ConfigData.Automation.Prize prize)
        {
            StoredData.RewardData rewardData;

            if (!string.IsNullOrEmpty(prize.Type))
            {
                if (!int.TryParse(prize.Amount.ToString(), out int amount))
                    return;

                if (prize.Type.ToLower() == "serverrewards")
                    rewardData = new StoredData.ServerRewardsData(amount);
                else rewardData = new StoredData.EconomicsData(amount);
            }
            else if (!string.IsNullOrEmpty(prize.Kit))
                rewardData = new StoredData.KitsData(prize.Kit);
            else if (!string.IsNullOrEmpty(prize.Command))
                rewardData = new StoredData.CommandData(prize.CustomName, prize.Command);
            else
            {
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(prize.Shortname);
                if (itemDefinition == null)
                    return;

                if (!int.TryParse(prize.Amount.ToString(), out int amount))
                    return;

                ulong.TryParse(prize.SkinID.ToString(), out ulong skinId);

                rewardData = new StoredData.ItemData(itemDefinition.itemid, amount, skinId, prize.CustomName);
            }

            ProcessEvent(rewardData);
        }

        private void ProcessEvent(StoredData.RewardData rewardData)
        {
            isRunning = true;

            BasePlayer winner = GetWinner();
            if (winner == null)
                return;

            WinnerData winnerData = new WinnerData(winner, rewardData);

            BeginNotification(winnerData);
        }

        private BasePlayer GetWinner()
        {
            BasePlayer winner = null;

            List<BasePlayer> list = Pool.Get<List<BasePlayer>>();

            list.AddRange(BasePlayer.activePlayerList);

            if (configData.IncludeSleepers)
                list.AddRange(BasePlayer.sleepingPlayerList);

            if (configData.ExcludeAdmins)
                list.RemoveAll(x => x.net?.connection?.authLevel > 0);

            if (list.Count > 0)
                winner = list.GetRandom();

            Pool.FreeUnmanaged(ref list);            

            return winner;
        }

        private void IssueReward(WinnerData winnerData)
        {
            if (winnerData.rewardData is StoredData.EconomicsData or StoredData.ServerRewardsData)
            {
                winnerData.rewardData.GiveReward(winnerData.userId);

                if (winnerData.player != null)
                    winnerData.player.ChatMessage(msg("Notification.Winner.Currency", winnerData.player.userID));
            }
            else
            {
                storedData.AddReward(winnerData.userId, winnerData.rewardData);

                if (winnerData.player != null)
                    winnerData.player.ChatMessage(msg("Notification.Winner.ItemKit", winnerData.player.userID));

                SaveData();
            }

            if (configData.Notification.LogWinners)
                LogToFile("prize_log", $"{BasePlayer.activePlayerList.Count} player(s) online{(configData.IncludeSleepers ? $", {BasePlayer.sleepingPlayerList.Count} player(s) sleeping" : "")} - {winnerData.displayName} ({winnerData.userId}) won {winnerData.rewardData.GetRewardName()}", this);
        }

        private T ParseType<T>(string type)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), type, true);
            }
            catch
            {
                return default(T);
            }
        }
        #endregion

        #region UI
        public class UI
        {
            public static CuiElementContainer Container(string panelName, string color, string aMin, string aMax, bool useCursor = false)
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = "Hud",
                        panelName
                    }
                };
                return container;
            }

            public static void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            public static string Color(string color, float alpha)
            {
                if (color.StartsWith("#"))
                    color = color.Substring(1);
                int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);

                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion

        #region Notifications
        private void BeginNotification(WinnerData winnerData)
        {
            switch (notificationMode)
            {
                case NotificationMode.Chat:
                    SendChat("Notification.InProgress", winnerData.rewardData.GetRewardName(), string.Empty);
                    break;
                case NotificationMode.UI:
                    SendUI("Notification.InProgress", winnerData.rewardData.GetRewardName(), string.Empty, false);
                    break;
                case NotificationMode.Hint:                    
                    SendGameTip("Notification.InProgress", winnerData.rewardData.GetRewardName(), string.Empty, false);
                    break;
                case NotificationMode.UINotify:
                    SendUINotify("Notification.InProgress", winnerData.rewardData.GetRewardName(), string.Empty);
                    break;
                default:
                    break;
            }

            timer.In(configData.Notification.OpenTime, () => SendWinnerNotification(winnerData, 0));
        }

        private void SendWinnerNotification(WinnerData winnerData, int attempt)
        {
            string arg = attempt == 0 ? "." : attempt == 1 ? ".." : attempt == 2 ? "..." : "... " + string.Format(msg("Notification.WinnerName"), winnerData.displayName);

            switch (notificationMode)
            {
                case NotificationMode.Chat:
                    SendChat("Notification.Winner", string.Empty, arg);
                    break;
                case NotificationMode.UI:
                    SendUI("Notification.Winner", string.Empty, arg, attempt == 3);
                    break;
                case NotificationMode.Hint:
                    SendGameTip("Notification.Winner", string.Empty, arg, attempt == 3);
                    break;
                case NotificationMode.UINotify:
                    SendUINotify("Notification.Winner", string.Empty, arg);
                    break;
                default:
                    break;
            }

            if (attempt < 3)
                timer.In(configData.Notification.WinNotificationInterval, () => SendWinnerNotification(winnerData, attempt + 1));
            else
            {
                IssueReward(winnerData);
                isRunning = false;
            }
        }

        private void SendChat(string key, string arg, string add)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];

                player.ChatMessage(string.Format(msg(key, player.userID), arg) + add);
            }
        }

        private void SendGameTip(string key, string arg, string add, bool close)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];

                player.SendConsoleCommand("gametip.hidegametip");

                player.SendConsoleCommand("gametip.showgametip", string.Format(msg(key, player.userID), arg) + add);

                if (close)
                    player.Invoke(() => player.SendConsoleCommand("gametip.hidegametip"), 5f);
            }
        }

        private void SendUI(string key, string arg, string add, bool close)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                CuiElementContainer container = UI.Container(UI_PANEL, "0 0 0 0", configData.Notification.UISettings.Min, configData.Notification.UISettings.Max);
                UI.Label(ref container, UI_PANEL, string.Format(msg(key, player.userID), arg) + add, configData.Notification.UISettings.FontSize, "0 0", "1 1");

                CuiHelper.DestroyUi(player, UI_PANEL);
                CuiHelper.AddUi(player, container);
            }

            if (close)
                timer.In(5, DestroyUI);
        }

        private void SendUINotify(string key, string arg, string add)
        {
            if (UINotify == null)
            {
                Debug.Log("[Giveaways] UINotify plugin is set as notification type, but the plugin is not loaded.");
                SendChat(key, arg, add);
                return;
            }

            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                UINotify.CallHook("SendNotify", player, configData.Notification.UINotifyType, string.Format(msg(key, player.userID), arg) + add);
            }
        }

        private void DestroyUI()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], UI_PANEL);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("giveaway")]
        private void cmdGiveaway(BasePlayer player ,string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage(msg("Chat.Claim.Help", player.userID));

                if (player.IsAdmin)
                {
                    player.ChatMessage("<color=#ce422b>/giveaway create</color> - Start a new give away with the prize being randomly selected from the config");
                    player.ChatMessage("<color=#ce422b>/giveaway create item</color> - Create a new give away with the prize being the item in your hands");
                    player.ChatMessage("<color=#ce422b>/giveaway create item <shortname> <amount> <opt:skinID></color> - Create a new give away with the prize being the item specified");
                    player.ChatMessage("<color=#ce422b>/giveaway create inventory</color> - Create a new give away with the prize being your current inventory");
                    player.ChatMessage("<color=#ce422b>/giveaway create kit <kitname></color> - Create a new give away with the prize being the specified kit");
                    player.ChatMessage("<color=#ce422b>/giveaway create sr <amount></color> - Create a new give away with the prize being the specified amount of RP");
                    player.ChatMessage("<color=#ce422b>/giveaway create eco <amount></color> - Create a new give away with the prize being the specified amount of Eco");
                }
                return;
            }

            if (args[0].ToLower() == "claim")
            {
                if (!storedData.HasRewards(player.userID))
                {
                    player.ChatMessage(msg("Chat.Claim.NoneOutstanding", player.userID));
                    return;
                }

                storedData.ClaimRewards(player);
                player.ChatMessage(msg("Chat.Claim.Success", player.userID));
            }
            else if (args[0].ToLower() == "create")
            {
                if (!player.IsAdmin)
                {
                    player.ChatMessage(msg("Chat.NoPermission", player.userID));
                    return;
                }

                if (isRunning)
                {
                    player.ChatMessage("There is currently a Giveaway in progress");
                    return;
                }

                if (args.Length == 1)
                {
                    RunEvent(configData.Auto.Items.GetRandom());
                    player.ChatMessage("Giveaway event has been started!");
                    return;
                }
                else
                {
                    switch (args[1].ToLower())
                    {
                        case "kit":
                            {
                                if (!Kits)
                                {
                                    player.ChatMessage("Unable to start Giveaway. The Kits plugin is not currently loaded");
                                    return;
                                }

                                if (args.Length != 3)
                                {
                                    player.ChatMessage("Invalid Syntax!");
                                    return;
                                }

                                object success = Kits?.Call("isKit", args[2]);
                                if (success is bool && (bool)success)
                                {
                                    player.ChatMessage("Giveaway event has been started!");
                                    ProcessEvent(new StoredData.KitsData(args[2]));                                    
                                }
                                else player.ChatMessage("Unable to start Giveaway. The chosen kit is invalid");
                                return;
                            }
                        case "sr":
                            {
                                if (!ServerRewards)
                                {
                                    player.ChatMessage("Unable to start Giveaway. The ServerRewards plugin is not currently loaded");
                                    return;
                                }

                                if (args.Length != 3)
                                {
                                    player.ChatMessage("Invalid Syntax!");
                                    return;
                                }

                                if (int.TryParse(args[2], out int amount))
                                {
                                    player.ChatMessage("Giveaway event has been started!");
                                    ProcessEvent(new StoredData.ServerRewardsData(amount));                                    
                                }
                                else player.ChatMessage("You must enter a numerical value");
                                return;
                            }
                        case "eco":
                            {
                                if (!Economics)
                                {
                                    player.ChatMessage("Unable to start Giveaway. The Economics plugin is not currently loaded");
                                    return;
                                }

                                if (args.Length != 3)
                                {
                                    player.ChatMessage("Invalid Syntax!");
                                    return;
                                }

                                if (int.TryParse(args[2], out int amount))
                                {
                                    player.ChatMessage("Giveaway event has been started!");
                                    ProcessEvent(new StoredData.EconomicsData(amount));                                    
                                }
                                else player.ChatMessage("You must enter a numerical value");
                                return;
                            }
                        case "item":
                            {
                                if (args.Length == 2)
                                {
                                    Item item = player.GetActiveItem();
                                    if (item == null)
                                    {
                                        player.ChatMessage("You must have a item in your hands to start a giveaway this way");
                                        return;
                                    }

                                    RunEvent(item);
                                    player.ChatMessage("Giveaway event has been started!");
                                    return;
                                }

                                if (args.Length < 4)
                                {
                                    player.ChatMessage("Invalid Syntax!");
                                    return;
                                }

                                string shortname = args[2];

                                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
                                if (itemDefinition == null)
                                {
                                    player.ChatMessage("Invalid item shortname entered");
                                    return;
                                }

                                if (!int.TryParse(args[3], out int amount))
                                {
                                    player.ChatMessage("You must enter a item amount");
                                    return;
                                }

                                ulong skinId = 0UL;

                                if (args.Length > 4)
                                    ulong.TryParse(args[4], out skinId);

                                player.ChatMessage("Giveaway event has been started!");

                                ProcessEvent(new StoredData.ItemData(itemDefinition.itemid, amount, skinId));
                            }
                            return;
                        case "inventory":
                            {
                                player.ChatMessage("Giveaway event has been started!");

                                ProcessEvent(new StoredData.InventoryData(player));
                                return;
                            }
                        default:

                            return;
                    }
                }
            }
            else player.ChatMessage(msg("Chat.InvalidSyntax", player.userID));
        }

        [ConsoleCommand("giveaway")]
        private void ccmdGiveaway(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args.Length == 0)
            {
                SendReply(arg, "giveaway create - Start a new give away with the prize being randomly selected from the config");
                SendReply(arg, "giveaway create item <shortname> <amount> <opt:skinID> - Create a new give away with the prize being the item specified");
                SendReply(arg, "giveaway create kit <kitname> - Create a new give away with the prize being the specified kit");
                SendReply(arg, "giveaway create sr <amount> - Create a new give away with the prize being the specified amount of RP");
                SendReply(arg, "giveaway create eco <amount> - Create a new give away with the prize being the specified amount of Eco");
                return;
            }

            if (arg.Args[0].ToLower() == "create")
            {                
                if (isRunning)
                {
                    SendReply(arg, "There is currently a Giveaway in progress");
                    return;
                }

                if (arg.Args.Length == 1)
                {
                    RunEvent(configData.Auto.Items.GetRandom());
                    SendReply(arg, "Giveaway event has been started!");
                    return;
                }
                else
                {
                    switch (arg.Args[1].ToLower())
                    {
                        case "kit":
                            {
                                if (!Kits)
                                {
                                    SendReply(arg, "Unable to start Giveaway. The Kits plugin is not currently loaded");
                                    return;
                                }

                                if (arg.Args.Length != 3)
                                {
                                    SendReply(arg, "Invalid Syntax!");
                                    return;
                                }

                                object success = Kits?.Call("isKit", arg.Args[2]);
                                if (success is bool && (bool)success)
                                {
                                    ProcessEvent(new StoredData.KitsData(arg.Args[2]));
                                    SendReply(arg, "Giveaway event has been started!");
                                }
                                else SendReply(arg, "Unable to start Giveaway. The chosen kit is invalid");
                                return;
                            }
                        case "sr":
                            {
                                if (!ServerRewards)
                                {
                                    SendReply(arg, "Unable to start Giveaway. The ServerRewards plugin is not currently loaded");
                                    return;
                                }

                                if (arg.Args.Length != 3)
                                {
                                    SendReply(arg, "Invalid Syntax!");
                                    return;
                                }

                                if (int.TryParse(arg.Args[2], out int amount))
                                {
                                    ProcessEvent(new StoredData.ServerRewardsData(amount));
                                    SendReply(arg, "Giveaway event has been started!");
                                }
                                else SendReply(arg, "You must enter a numerical value");
                                return;
                            }
                        case "eco":
                            {
                                if (!Economics)
                                {
                                    SendReply(arg, "Unable to start Giveaway. The Economics plugin is not currently loaded");
                                    return;
                                }

                                if (arg.Args.Length != 3)
                                {
                                    SendReply(arg, "Invalid Syntax!");
                                    return;
                                }

                                if (int.TryParse(arg.Args[2], out int amount))
                                {
                                    ProcessEvent(new StoredData.EconomicsData(amount));
                                    SendReply(arg, "Giveaway event has been started!");
                                }
                                else SendReply(arg, "You must enter a numerical value");
                                return;
                            }
                        case "item":
                            {
                                if (arg.Args.Length < 4)
                                {
                                    SendReply(arg, "Invalid Syntax!");
                                    return;
                                }

                                string shortname = arg.Args[2];

                                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
                                if (itemDefinition == null)
                                {
                                    SendReply(arg, "Invalid item shortname entered");
                                    return;
                                }

                                if (!int.TryParse(arg.Args[3], out int amount))
                                {
                                    SendReply(arg, "You must enter a item amount");
                                    return;
                                }

                                ulong skinId = 0UL;

                                if (arg.Args.Length > 4)
                                    ulong.TryParse(arg.Args[4], out skinId);

                                ProcessEvent(new StoredData.ItemData(itemDefinition.itemid, amount, skinId));
                                SendReply(arg, "Giveaway event has been started!");
                            }
                            return;
                    }
                }
            }
            else SendReply(arg, "Invalid syntax!");
        }
        #endregion

        #region Config        
        private static ConfigData configData;

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Exclude admins from give aways")]
            public bool ExcludeAdmins { get; set; }

            [JsonProperty(PropertyName = "Include sleepers in give aways")]
            public bool IncludeSleepers { get; set; }

            [JsonProperty(PropertyName = "Automated Give Away")]
            public Automation Auto { get; set; }

            [JsonProperty(PropertyName = "Notifications")]
            public Notifications Notification { get; set; }

            [JsonProperty(PropertyName = "Wipe data when server wipe is detected")]
            public bool WipeData { get; set; }

            public class Automation
            {
                [JsonProperty(PropertyName = "Enabled automated give aways")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Interval between give aways (seconds)")]
                public int Interval { get; set; }

                [JsonProperty(PropertyName = "Minimum players required to run automated give away")]
                public int MinimumPlayers { get; set; }

                [JsonProperty(PropertyName = "Prizes")]
                public List<Prize> Items { get; set; }

                public class Prize
                {
                    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
                    public string Shortname { get; set; }

                    [JsonProperty(PropertyName = "Custom Name", NullValueHandling = NullValueHandling.Ignore)]
                    public string CustomName { get; set; }

                    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
                    public object Amount { get; set; }

                    [JsonProperty(PropertyName = "Skin ID", NullValueHandling = NullValueHandling.Ignore)]
                    public object SkinID { get; set; }

                    [JsonProperty(PropertyName = "Command", NullValueHandling = NullValueHandling.Ignore)]
                    public string Command { get; set; }

                    [JsonProperty(PropertyName = "Currency type (ServerRewards, Economics)", NullValueHandling = NullValueHandling.Ignore)]
                    public string Type { get; set; }

                    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
                    public string Kit { get; set; }
                }
            }

            public class Notifications
            {
                [JsonProperty(PropertyName = "Notification mode (Chat, UI, Hint, UINotify)")]
                public string Mode { get; set; }

                [JsonProperty(PropertyName = "Information interval (seconds)")]
                public int NotificationInterval { get; set; }

                [JsonProperty(PropertyName = "(UINotify Plugin) Notification type")]
                public int UINotifyType { get; set; }

                [JsonProperty(PropertyName = "Amount of time the event is open for (seconds)")]
                public int OpenTime { get; set; }

                [JsonProperty(PropertyName = "Time between message updates when announcing the winner (seconds)")]
                public int WinNotificationInterval { get; set; }

                [JsonProperty(PropertyName = "Log winning players and prizes")]
                public bool LogWinners { get; set; }

                [JsonProperty(PropertyName = "UI Settings")]
                public UI UISettings { get; set; }

                public class UI
                {
                    public int FontSize { get; set; }

                    [JsonProperty(PropertyName = "Horizontal start position (left)")]
                    public float XPosition { get; set; }

                    [JsonProperty(PropertyName = "Vertical start position (bottom)")]
                    public float YPosition { get; set; }

                    [JsonProperty(PropertyName = "Horizontal dimensions")]
                    public float XDimension { get; set; }

                    [JsonProperty(PropertyName = "Vertical dimensions")]
                    public float YDimension { get; set; }

                    [JsonIgnore]
                    public string Min => $"{XPosition} {YPosition}";

                    [JsonIgnore]
                    public string Max => $"{XPosition + XDimension} {YPosition + YDimension}";

                }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Auto = new ConfigData.Automation
                {
                    Enabled = true,
                    Interval = 3600,
                    Items = new List<ConfigData.Automation.Prize>
                    {
                        new ConfigData.Automation.Prize
                        {
                            Amount = 10,
                            Type = "ServerRewards"
                        },
                        new ConfigData.Automation.Prize
                        {
                            Amount = 10,
                            Type = "Economics"
                        },
                        new ConfigData.Automation.Prize
                        {
                            Kit = "kitname"
                        },
                        new ConfigData.Automation.Prize
                        {
                            Amount = 1,
                            Shortname = "rifle.ak",
                            SkinID = 0,
                            CustomName = ""
                        },
                        new ConfigData.Automation.Prize
                        {
                            CustomName = "Some example command",
                            Command = "example.command $player.id $player.name $player.x $player.y $player.z",
                        }
                    },
                    MinimumPlayers = 5
                },
                ExcludeAdmins = true,
                IncludeSleepers = false,
                Notification = new ConfigData.Notifications
                {
                    Mode = "UI",
                    UINotifyType = 0,
                    NotificationInterval = 1800,
                    LogWinners = true,
                    WinNotificationInterval = 2,
                    OpenTime = 10,
                    UISettings = new ConfigData.Notifications.UI
                    {
                        FontSize = 15,
                        XPosition = 0.15f,
                        XDimension = 0.65f,
                        YDimension = 0.05f,
                        YPosition = 0.8f
                    }
                },
                WipeData = false,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (configData.Version < new VersionNumber(2, 0, 0))
                configData = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 3))            
                configData.Notification.WinNotificationInterval = 2;            

            if (configData.Version < new VersionNumber(2, 0, 4))
                configData.Notification.OpenTime = 10;

            if (configData.Version < new VersionNumber(2, 0, 12))
            {
                configData.Notification.Mode = "UI";
                configData.Notification.UINotifyType = 0;
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }

            if (storedData?.pendingRewards == null || wipeData)
            {
                storedData = new StoredData();
                SaveData();
            }
        }

        public class StoredData
        {
            public Hash<ulong, List<JObject>> pendingRewards = new Hash<ulong, List<JObject>>();

            public void AddReward(ulong playerId, RewardData rewardData)
            {
                if (!pendingRewards.TryGetValue(playerId, out List<JObject> list))
                    pendingRewards[playerId] = list = new List<JObject>();

                list.Add(JObject.FromObject(rewardData));
            }

            public bool HasRewards(ulong playerId) => pendingRewards.ContainsKey(playerId);

            public void ClaimRewards(BasePlayer player)
            {
                if (!pendingRewards.TryGetValue(player.userID, out List<JObject> list))
                    return;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    JObject rewardData = list[i];

                    if (rewardData == null || !rewardData.HasValues || rewardData.GetValue("Type") == null || string.IsNullOrEmpty((string)rewardData.GetValue("Type")))
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    bool remove = false;

                    string type = (string)rewardData.GetValue("Type");

                    switch (type)
                    {
                        case "EconomicsData":
                            remove = rewardData.ToObject<EconomicsData>().GiveReward(player);
                            break;

                        case "ServerRewardsData":
                            remove = rewardData.ToObject<ServerRewardsData>().GiveReward(player);
                            break;

                        case "KitsData":
                            remove = rewardData.ToObject<KitsData>().GiveReward(player);
                            break;

                        case "InventoryData":
                            remove = rewardData.ToObject<InventoryData>().GiveReward(player);
                            break;

                        case "ItemData":
                            remove = rewardData.ToObject<ItemData>().GiveReward(player);
                            break;

                        case "CommandData":
                            remove = rewardData.ToObject<CommandData>().GiveReward(player);
                            break;

                        default:
                            remove = true;
                            break;
                    }                    
                    
                    if (remove)
                        list.RemoveAt(i);
                }

                if (list.Count == 0)
                    RemoveRewards(player.userID);
            }

            public void RemoveRewards(ulong playerId) => pendingRewards.Remove(playerId);

            public class RewardData
            {
                public virtual string Type { get; set; }

                public virtual bool GiveReward(BasePlayer player) { return false; }

                public virtual bool GiveReward(ulong playerId) { return false; }

                public virtual string GetRewardName() { return string.Empty; }
            }

            public class EconomicsData : RewardData
            {
                public int amount;

                public override string Type => "EconomicsData";

                public EconomicsData() { }

                public EconomicsData(int amount)
                {
                    this.amount = amount;
                }

                public override bool GiveReward(BasePlayer player)
                {
                    if (player == null || !player.IsValid())
                        return false;

                    return GiveReward(player.userID);
                }

                public override bool GiveReward(ulong playerId)
                {                   
                    if (Instance.Economics == null)
                        return false;

                    Instance.Economics.Call("Deposit", playerId, (double)amount);

                    return true;
                }

                public override string GetRewardName()
                {
                    return $"{amount} x {Instance.msg("Prize.Economics")}";
                }
            }

            public class ServerRewardsData : RewardData
            {
                public int amount;

                public override string Type => "ServerRewardsData";
                public ServerRewardsData() { }

                public ServerRewardsData(int amount)
                {
                    this.amount = amount;
                }

                public override bool GiveReward(BasePlayer player)
                {
                    if (player == null || !player.IsValid())
                        return false;

                    return GiveReward(player.userID);
                }

                public override bool GiveReward(ulong playerId)
                {
                    if (Instance.ServerRewards == null)
                        return false;

                    Instance.ServerRewards.Call("AddPoints", playerId, amount);

                    return true;
                }

                public override string GetRewardName()
                {
                    return $"{amount} x {Instance.msg("Prize.ServerRewards")}";
                }
            }

            public class KitsData : RewardData
            {
                public string kit;

                public override string Type => "KitsData";

                public KitsData() { }

                public KitsData(string kit)
                {
                    this.kit = kit;
                }

                public override bool GiveReward(BasePlayer player)
                {
                    if (player == null || !player.IsValid())
                        return false;

                    if (Instance.Kits == null)
                        return false;

                    object success = Instance.Kits.Call("isKit", kit);
                    if (success is bool && !(bool)success)
                        return false;

                    Instance.Kits.Call("GiveKit", player, kit);
                    return true;
                }

                public override string GetRewardName()
                {
                    return $"{kit} {Instance.msg("Prize.Kit")}";
                }
            }

            public class CommandData : RewardData
            {
                public string name;

                public string command;

                public override string Type => "CommandData";

                public CommandData() { }

                public CommandData(string name, string command)
                {
                    this.name = name;
                    this.command = command;
                }

                public override bool GiveReward(BasePlayer player)
                {
                    if (player == null || !player.IsValid())
                        return false;

                    string cmd = command.Replace("$player.id", player.UserIDString)
                                        .Replace("$player.name", player.displayName)
                                        .Replace("$player.x", player.transform.position.x.ToString())
                                        .Replace("$player.y", player.transform.position.y.ToString())
                                        .Replace("$player.z", player.transform.position.z.ToString());

                    ConsoleSystem.Run(ConsoleSystem.Option.Server, cmd);

                    return true;
                }

                public override string GetRewardName()
                {
                    return name;
                }
            }

            public class InventoryData : RewardData
            {
                public List<ItemData> items;

                public override string Type => "InventoryData";

                public InventoryData() { }

                public InventoryData(BasePlayer player)
                {
                    this.items = new List<ItemData>();

                    List<Item> list = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(list);

                    for (int i = 0; i < list.Count; i++)
                    {
                        this.items.Add(new ItemData(list[i]));
                    }   
                    
                    Pool.FreeUnmanaged(ref list);
                }

                public override bool GiveReward(BasePlayer player)
                {
                    if (player == null || !player.IsValid())
                        return false;

                    for (int i = 0; i < items.Count; i++)
                    {
                        items[i].GiveReward(player);
                    }

                    return true;
                }

                public override string GetRewardName()
                {
                    return "multiple items";
                }
            }

            public class ItemData : RewardData
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public float maxCondition;
                public int ammo;
                public string ammotype;
                public int position;
                public int frequency;
                public string customName;
                public InstanceData instanceData;
                public ItemData[] contents;

                public override string Type => "ItemData";

                public ItemData() { }

                public ItemData(int itemId, int amount, ulong skinId, string customName = null)
                {
                    this.itemid = itemId;
                    this.amount = amount;
                    this.skin = skinId;
                    this.customName = customName;
                }

                public ItemData(Item item)
                {
                    itemid = item.info.itemid;
                    amount = item.amount;
                    ammo = item.GetHeldEntity() is BaseProjectile ? (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents : item.GetHeldEntity() is FlameThrower ? (item.GetHeldEntity() as FlameThrower).ammo : 0;
                    ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null;
                    position = item.position;
                    skin = item.skin;
                    condition = item.condition;
                    maxCondition = item.maxCondition;
                    frequency = ItemModAssociatedEntity<PagerEntity>.GetAssociatedEntity(item)?.GetFrequency() ?? -1;
                    instanceData = new ItemData.InstanceData(item);
                    contents = item.contents?.itemList.Select(item1 => new ItemData
                    {
                        itemid = item1.info.itemid,
                        amount = item1.amount,
                        condition = item1.condition
                    }).ToArray();
                }

                public override bool GiveReward(BasePlayer player)
                {
                    if (player == null)
                        return false;

                    Item item = ItemManager.CreateByItemID(itemid, amount, skin);

                    if (condition != 0)
                        item.condition = condition;

                    if (maxCondition != 0)
                        item.maxCondition = maxCondition;

                    if (frequency > 0)
                    {
                        ItemModRFListener rfListener = item.info.GetComponentInChildren<ItemModRFListener>();
                        if (rfListener != null)
                        {
                            PagerEntity pagerEntity = BaseNetworkable.serverEntities.Find(item.instanceData.subEntity) as PagerEntity;
                            if (pagerEntity != null)
                            {
                                pagerEntity.ChangeFrequency(frequency);
                                item.MarkDirty();
                            }
                        }
                    }

                    if (instanceData?.IsValid() ?? false)
                        instanceData.Restore(item);

                    BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                    if (weapon != null)
                    {
                        if (!string.IsNullOrEmpty(ammotype))
                            weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
                        weapon.primaryMagazine.contents = ammo;
                    }

                    FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
                    if (flameThrower != null)
                        flameThrower.ammo = ammo;

                    if (contents != null)
                    {
                        foreach (ItemData contentData in contents)
                        {
                            Item newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                            if (newContent != null)
                            {
                                newContent.condition = contentData.condition;
                                newContent.MoveToContainer(item.contents);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(customName))
                        item.name = customName;

                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                    return true;
                }

                public override string GetRewardName()
                {
                    return $"{amount} x {(string.IsNullOrEmpty(customName) ? ItemManager.FindItemDefinition(itemid)?.displayName.english : customName)}";
                }

                public class InstanceData
                {
                    public int dataInt;
                    public int blueprintTarget;
                    public int blueprintAmount;
                    public uint subEntity;

                    public InstanceData() { }
                    public InstanceData(Item item)
                    {
                        if (item.instanceData == null)
                            return;

                        dataInt = item.instanceData.dataInt;
                        blueprintAmount = item.instanceData.blueprintAmount;
                        blueprintTarget = item.instanceData.blueprintTarget;
                    }

                    public void Restore(Item item)
                    {
                        if (item.instanceData == null)
                            item.instanceData = new ProtoBuf.Item.InstanceData();

                        item.instanceData.ShouldPool = false;

                        item.instanceData.blueprintAmount = blueprintAmount;
                        item.instanceData.blueprintTarget = blueprintTarget;
                        item.instanceData.dataInt = dataInt;

                        item.MarkDirty();
                    }

                    public bool IsValid()
                    {
                        return dataInt != 0 || blueprintAmount != 0 || blueprintTarget != 0;
                    }                    
                }
            }
        }

        private struct WinnerData
        {
            public BasePlayer player;
            public ulong userId;
            public string displayName;
            public StoredData.RewardData rewardData;

            public WinnerData(BasePlayer player, StoredData.RewardData rewardData)
            {
                this.player = player;
                this.userId = player?.userID ?? 0UL;
                this.displayName = player?.displayName;
                this.rewardData = rewardData;
            }
        }
        #endregion

        #region Localization
        private string msg(string key, ulong playerId = 0UL) => lang.GetMessage(key, this, playerId != 0UL ? playerId.ToString() : null);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Notification.InProgress"] = "A Giveaway raffle for <color=#ce422b>{0}</color> is about to be drawn...",
            ["Notification.Winner"] = "The winner of the Giveaway is ",
            ["Notification.WinnerName"] = "<color=#ce422b>{0}</color>",
            ["Notification.Winner.ItemKit"] = "<color=#ce422b>Congratulations!</color> To claim your prize type <color=#ce422b>/giveaway claim</color>",
            ["Notification.Winner.Currency"] = "<color=#ce422b>Congratulations!</color> Your prize has been added to your account!",
            ["Notification.OnInit"] = "You have prizes that can be claimed! Type <color=#ce422b>/giveaway</color> for more information",
            ["Notification.Info"] = "This server is running <color=#ce422b>Giveaways</color>. Periodically a raffle will begin and you could be the winner!",
            ["Chat.Claim.Help"] = "<color=#ce422b>/giveaway claim</color> - Claim any outstanding prizes",
            ["Chat.Claim.NoneOutstanding"] = "You do not have any outstanding prizes",
            ["Chat.Claim.Success"] = "You have claimed your prizes!",
            ["Chat.NoPermission"] = "You do not have permission to use this command",
            ["Chat.InvalidSyntax"] = "Invalid syntax!",
            ["Prize.Economics"] = "Economics",
            ["Prize.Kit"] = "kit",
            ["Prize.ServerRewards"] = "RP"
        };
        #endregion
    }
}

