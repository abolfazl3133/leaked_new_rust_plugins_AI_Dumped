// #define TESTING

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
    [Info("Skip Night", "Mevent", "1.1.0")]
    public class SkipNight : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary = null;

        private static SkipNight _instance;

#if CARBON
		private ImageDatabaseModule imageDatabase;
#endif

        private const string
            PERM_ADMIN = "skipnight.admin",
            Layer = "UI.SkipNight";

        private SkipEngine _engine;

        #endregion

        #region Config

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Economy")]
            public EconomyConf Economy = new()
            {
                Type = EconomyType.Plugin,
                AddHook = "Deposit",
                BalanceHook = "Balance",
                RemoveHook = "Withdraw",
                Plug = "Economics",
                ShortName = "scrap",
                DisplayName = string.Empty,
                Skin = 0
            };

            [JsonProperty(PropertyName = "Time Settings")]
            public TimeSettings Time = new()
            {
                DayStart = "08:00",
                NightStart = "20:00",
                TimeStart = "20:00",
                TimeEnd = "21:00",
                TimeSet = "08:00",
                TimeVote = 60,
                ForceSkip = true,
                LengthNight = 5,
                LengthFastNight = 2,
                LengthDay = 45,
                FullMoon = true,
                MissedNights = 0,
                FullMoonDates = new List<DateTime>
                {
                    new(2024, 1, 25),
                    new(2024, 2, 24),
                    new(2024, 3, 25),
                    new(2024, 4, 23),
                    new(2024, 5, 23),
                    new(2024, 6, 21),
                    new(2024, 7, 21),
                    new(2024, 8, 19),
                    new(2024, 9, 17),
                    new(2024, 10, 17),
                    new(2024, 11, 15),
                    new(2024, 12, 15)
                }
            };

            [JsonProperty(PropertyName = "UI Settings")]
            public InterfaceSettings UI = new()
            {
                InitialDisplay = true,
                DestroyTime = 5,
                ShowImage = true,
                Image =
                    "https://github.com/TheMevent/PluginsStorage/blob/main/Images/SkipNight/skipnight-logo.png?raw=true",
                ImageWidth = 42,
                ImageHeight = 33,
                ImageUpIndent = 16,
                LeftIndent = 212,
                BottomIndent = 16,
                Width = 178,
                DefaultHeight = 82,
                UnfoldedHeight = 184,
                BackgroundColor = new IColor("#F8EBE3",
                    4),
                BackgroundMaterial = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                VotingButton = new InterfaceSettings.BtnInfo
                {
                    Width = 150,
                    Height = 30,
                    BottomIndent = 15
                },
                ProgressBar = new InterfaceSettings.ProgressInfo
                {
                    Width = 150,
                    Height = 20,
                    BottomIndent = 55
                },
                Colors = new InterfaceSettings.ColorsInfo
                {
                    Color1 = new IColor("#ABE04E"),
                    Color2 = new IColor("#595651",
                        75),
                    Color3 = new IColor("#74884A",
                        95),
                    Color4 = new IColor("#FFFFFF")
                },
                HideBtn = new InterfaceSettings.InterfacePosition
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "0 -20",
                    OffsetMax = "20 0"
                },
                CloseBtn = new InterfaceSettings.BtnConf
                {
                    Enabled = false,
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-20 -20",
                    OffsetMax = "0 0"
                },
                CloseAfterVoting = false
            };

            [JsonProperty(PropertyName = "Votes Settings")]
            public VotesSettings Votes = new()
            {
                VoteCommands = new[] {"voteday", "vt"},
                OnStartBroadcasting = false,
                VotesCount = 5,
                UsePercent = true,
                Percent = 30,
                CommandsAfterLostVoting = "example.cmd",
                CommandsAfterSuccessfulVoting = "cmd1|cmd2",
                EnabledPay = false,
                RefundWhenCanceled = false,
                SkipCost = 100
            };

            public VersionNumber Version;
        }

        private enum EconomyType
        {
            Plugin,
            Item
        }

        private class EconomyConf
        {
            [JsonProperty(PropertyName = "Type (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Type;

            [JsonProperty(PropertyName = "Plugin name")]
            public string Plug;

            [JsonProperty(PropertyName = "Balance add hook")]
            public string AddHook;

            [JsonProperty(PropertyName = "Balance remove hook")]
            public string RemoveHook;

            [JsonProperty(PropertyName = "Balance show hook")]
            public string BalanceHook;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Display Name (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            public double ShowBalance(BasePlayer player)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return 0;

                        return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.UserIDString)));
                    }
                    case EconomyType.Item:
                    {
                        return PlayerItemsCount(player, ShortName, Skin);
                    }
                    default:
                        return 0;
                }
            }

            public void AddBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(AddHook, player.UserIDString, (int) amount);
                                break;
                            default:
                                plugin.Call(AddHook, player.UserIDString, amount);
                                break;
                        }

                        break;
                    }
                    case EconomyType.Item:
                    {
                        var am = (int) amount;

                        var item = ToItem(am);
                        if (item == null) return;

                        player.GiveItem(item);
                        break;
                    }
                }
            }

            public bool RemoveBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        if (ShowBalance(player) < amount) return false;

                        var plugin = _instance?.plugins.Find(Plug);
                        if (plugin == null) return false;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(RemoveHook, player.UserIDString, (int) amount);
                                break;
                            default:
                                plugin.Call(RemoveHook, player.UserIDString, amount);
                                break;
                        }

                        return true;
                    }
                    case EconomyType.Item:
                    {
                        var playerItems = Pool.Get<List<Item>>();
                        try
                        {
                            player.inventory.GetAllItems(playerItems);

                            var am = (int) amount;

                            if (ItemCount(playerItems, ShortName, Skin) < am)
                            {
                                return false;
                            }

                            Take(playerItems, ShortName, Skin, am);
                            return true;
                        }
                        finally
                        {
                            Pool.Free(ref playerItems);
                        }
                    }
                    default:
                        return false;
                }
            }

            private Item ToItem(int amount)
            {
                var item = ItemManager.CreateByName(ShortName, amount, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

                return item;
            }
        }

        private class VotesSettings
        {
            [JsonProperty(PropertyName = "Vote Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] VoteCommands;

            [JsonProperty(PropertyName = "Chat notification when voting starts")]
            public bool OnStartBroadcasting;

            [JsonProperty(PropertyName = "Number of votes")]
            public int VotesCount;

            [JsonProperty(PropertyName = "Use a percentage of the online?")]
            public bool UsePercent;

            [JsonProperty(PropertyName = "Percentage of the online")]
            public float Percent;

            [JsonProperty(PropertyName = "Commands after lost voting")]
            public string CommandsAfterLostVoting;

            [JsonProperty(PropertyName = "Commands after successful voting")]
            public string CommandsAfterSuccessfulVoting;

            [JsonProperty(PropertyName = "Enable a voting fee for skipping the night?")]
            public bool EnabledPay;

            [JsonProperty(PropertyName = "Refunds if a vote is canceled?")]
            public bool RefundWhenCanceled;

            [JsonProperty(PropertyName = "Cost of skipping the night")]
            public int SkipCost;

            public int GetVotesCount()
            {
                return UsePercent
                    ? Mathf.Max(Mathf.RoundToInt(Percent / 100f * BasePlayer.activePlayerList.Count), 1)
                    : VotesCount;
            }

            public void StartCommands(bool successful)
            {
                var commands = successful ? CommandsAfterSuccessfulVoting : CommandsAfterLostVoting;

                foreach (var cmd in commands.Split('|')) StartCommand(cmd);
            }

            private void StartCommand(string command)
            {
                _instance?.Server.Command(command);
            }
        }

        private class InterfaceSettings
        {
            [JsonProperty(PropertyName = "Type of initial display (true - collapsed, false - expanded)")]
            public bool InitialDisplay;

            [JsonProperty(PropertyName = "Destroy Time")]
            public float DestroyTime;

            [JsonProperty(PropertyName = "Show Image?")]
            public bool ShowImage;

            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "Image Width")]
            public float ImageWidth;

            [JsonProperty(PropertyName = "Image Height")]
            public float ImageHeight;

            [JsonProperty(PropertyName = "Image Up Indent")]
            public float ImageUpIndent;

            [JsonProperty(PropertyName = "Left Indent")]
            public float LeftIndent;

            [JsonProperty(PropertyName = "Bottom Indent")]
            public float BottomIndent;

            [JsonProperty(PropertyName = "Width")] public float Width;

            [JsonProperty(PropertyName = "Height for default version")]
            public float DefaultHeight;

            [JsonProperty(PropertyName = "Height for unfolded version")]
            public float UnfoldedHeight;

            [JsonProperty(PropertyName = "Background Color")]
            public IColor BackgroundColor;

            [JsonProperty(PropertyName = "Background Materal")]
            public string BackgroundMaterial;

            [JsonProperty(PropertyName = "Voting Button")]
            public BtnInfo VotingButton;

            [JsonProperty(PropertyName = "Progress Bar")]
            public ProgressInfo ProgressBar;

            [JsonProperty(PropertyName = "Colors")]
            public ColorsInfo Colors;

            [JsonProperty(PropertyName = "Hide button position")]
            public InterfacePosition HideBtn;

            [JsonProperty(PropertyName = "Close button position")]
            public BtnConf CloseBtn;

            [JsonProperty(PropertyName = "Close the interface after voting?")]
            public bool CloseAfterVoting;

            public class BtnConf : InterfacePosition
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool Enabled;
            }

            public class InterfacePosition
            {
                [JsonProperty(PropertyName = "AnchorMin")]
                public string AnchorMin;

                [JsonProperty(PropertyName = "AnchorMax")]
                public string AnchorMax;

                [JsonProperty(PropertyName = "OffsetMin")]
                public string OffsetMin;

                [JsonProperty(PropertyName = "OffsetMax")]
                public string OffsetMax;
            }

            public class ProgressInfo
            {
                [JsonProperty(PropertyName = "Width")] public float Width;

                [JsonProperty(PropertyName = "Height")]
                public float Height;

                [JsonProperty(PropertyName = "Bottom Indent")]
                public float BottomIndent;
            }

            public class BtnInfo
            {
                [JsonProperty(PropertyName = "Width")] public float Width;

                [JsonProperty(PropertyName = "Height")]
                public float Height;

                [JsonProperty(PropertyName = "Bottom Indent")]
                public float BottomIndent;
            }

            public class ColorsInfo
            {
                [JsonProperty(PropertyName = "Color 1")]
                public IColor Color1;

                [JsonProperty(PropertyName = "Color 2")]
                public IColor Color2;

                [JsonProperty(PropertyName = "Color 3")]
                public IColor Color3;

                [JsonProperty(PropertyName = "Color 4")]
                public IColor Color4;
            }
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string Hex;

            [JsonProperty(PropertyName = "Opacity (0 - 100)")]
            public readonly float Alpha;

            [JsonIgnore] private string _color;

            [JsonIgnore]
            public string Get
            {
                get
                {
                    if (string.IsNullOrEmpty(_color))
                        _color = GetColor();

                    return _color;
                }
            }

            private string GetColor()
            {
                if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                var str = Hex.Trim('#');
                if (str.Length != 6) throw new Exception(Hex);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
            }

            public IColor()
            {
            }

            public IColor(string hex, float alpha = 100)
            {
                Hex = hex;
                Alpha = alpha;
            }
        }

        private class TimeSettings
        {
            #region Fields

            [JsonProperty(PropertyName = "Day Start")]
            public string DayStart;

            [JsonProperty(PropertyName = "Night Start")]
            public string NightStart;

            [JsonProperty(PropertyName = "Voting time")]
            public short TimeVote;

            [JsonProperty(PropertyName = "Voting start time (time to check)")]
            public string TimeStart;

            [JsonProperty(PropertyName = "Time until which hour the voting will take place (time to check)")]
            public string TimeEnd;

            [JsonProperty(PropertyName = "Time after voting (to which the night passes)")]
            public string TimeSet;

            [JsonProperty(PropertyName = "Fast skip the night")]
            public bool ForceSkip;

            [JsonProperty(PropertyName = "Length of the night (minutes)")]
            public float LengthNight;

            [JsonProperty(PropertyName = "Length of the FAST night (minutes)")]
            public float LengthFastNight;

            [JsonProperty(PropertyName = "Length of the day (minutes)")]
            public float LengthDay;

            [JsonProperty(PropertyName =
                "The number of skipped nights, after which it is impossible to skip a single night?")]
            public int MissedNights;

            [JsonProperty(PropertyName = "Night with a full moon")]
            public bool FullMoon;

            [JsonProperty(PropertyName = "Full Moon Dates", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<DateTime> FullMoonDates;

            #endregion

            #region Public Methods

            public TimeSpan GetTimeSet()
            {
                return TimeSpan.Parse(TimeSet);
            }

            #endregion

            #region Cache

            [JsonIgnore] public TimeSpan DayStartHours;
            [JsonIgnore] public TimeSpan NightStartHours;
            [JsonIgnore] public TimeSpan TimeStartHours;
            [JsonIgnore] public TimeSpan TimeEndHours;

            public void Init()
            {
                DayStartHours = TimeSpan.Parse(_config.Time.DayStart);
                NightStartHours = TimeSpan.Parse(_config.Time.NightStart);
                TimeStartHours = TimeSpan.Parse(_config.Time.TimeStart);
                TimeEndHours = TimeSpan.Parse(_config.Time.TimeEnd);
            }

            #endregion
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();

                if (_config == null)
                    throw new Exception();

                if (_config.Version < Version)
                    UpdateConfigValues();

                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
                Debug.LogException(ex);
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        private void UpdateConfigValues()
        {
            if (_config.Version == default)
            {
                if (_config.UI is {Image: "https://i.ibb.co/cJh7Ytx/image.png"})
                {
                    PrintWarning("Config update detected! Updating config values...");

                    _config.UI.Image =
                        "https://github.com/TheMevent/PluginsStorage/blob/main/Images/SkipNight/skipnight-logo.png?raw=true";
                }
            }

            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;
        }

        private void OnServerInitialized()
        {
            LoadImages();

            _config.Time.Init();

            InitEngine();

            RegisterCommands();

            RegisterPermissions();
        }

        private void Unload()
        {
            DestroyEngine();

            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

            _instance = null;
            _config = null;
        }

        #endregion

        #region Commands

        private void CmdVote(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            _engine.Vote(player);

            Reply(player, MsgVoted);
        }

        private void CmdAdminSkipNight(IPlayer covPlayer, string command, string[] args)
        {
            if (!covPlayer.IsServer && !covPlayer.HasPermission(PERM_ADMIN)) return;

            if (args.Length == 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"SkipNight Admins Commands:");
                sb.AppendLine($"– {command} start_manually – Start voting manually");
                covPlayer.Reply(sb.ToString());
                return;
            }

            switch (args[0])
            {
                case "start_manually":
                {
                    _engine.StartVote();

                    covPlayer.Reply($"You have started voting manually");
                    break;
                }
            }
        }

        [ConsoleCommand("UI_SkipNight")]
        private void CmdSkipNight(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "vote":
                {
                    _engine.Vote(player);
                    break;
                }

                case "hide":
                {
                    _engine.SwitchHide(player);
                    break;
                }

                case "close":
                {
                    _engine.Close(player);
                    break;
                }
            }
        }

        #endregion

        #region Component

        private class SkipEngine : FacepunchBehaviour
        {
            #region Fields

            private CuiElementContainer _tempContainer = new();

            private float _leftVoteTime;

            private int needVotes;

            private Stage _stage = Stage.Stopped;

            private bool _isSkippedVoting;

            private enum Stage
            {
                Started,
                InProgress,
                Stopped,
                Skipped
            }

            private uint componentSearchAttempts;

            private TOD_Time timeComponent;

            private bool isDay;

            private TimeSpan CurrentTime => TOD_Sky.Instance.Cycle.DateTime.TimeOfDay;

            private int _skippedNight;

            #endregion

            #region Init

            private void Awake()
            {
                GetTimeComponent();
            }

            private void GetTimeComponent()
            {
                if (TOD_Sky.Instance == null)
                {
                    ++componentSearchAttempts;
                    if (componentSearchAttempts < 50)
                    {
                        Invoke(GetTimeComponent, 3);
                        return;
                    }
                }

                timeComponent = TOD_Sky.Instance.Components.Time;

                if (timeComponent == null)
                {
                    _instance.RaiseError("Could not fetch time component. Plugin will not work without it.");
                    return;
                }

                timeComponent.OnMinute += OnMinute;

                timeComponent.OnDay += OnDay;

                OnMinute();
            }

            #endregion

            #region Main

            public void StartVote()
            {
#if TESTING
    Debug.Log($"[StartVote] MissedNights={_config.Time.MissedNights}, _skippedNight={_skippedNight}, canStart={!IsLimitedSkippingNight()}, stage={_stage}");
#endif

                if (_stage != Stage.Stopped)
                {
#if TESTING
				Debug.Log("[StartVote] can't start");
#endif
                    return;
                }
                
                if (IsLimitedSkippingNight())
                {
                    if (_skippedNight == _config.Time.MissedNights) _skippedNight++;
#if TESTING
        Debug.Log("[StartVote] can't start via skipped nights");
#endif
                    return;
                }

#if TESTING
			Debug.Log("[StartVote] before cancel invoke");
#endif

                CancelInvoke();

                _leftVoteTime = _config.Time.TimeVote;

                _stage = Stage.Started;

                _isSkippedVoting = false;

                needVotes = _config.Votes.GetVotesCount();

                if (_config.UI.InitialDisplay)
                    _openedUi = new HashSet<BasePlayer>(BasePlayer.activePlayerList);

                if (_config.Votes.OnStartBroadcasting)
                    foreach (var player in BasePlayer.activePlayerList)
                        _instance.Reply(player, VotingBroadcast);

                DrawUIs();

#if TESTING
			Debug.Log("[StartVote] call InvokeRepeating.UpdatePlayers");
#endif
                InvokeRepeating(UpdatePlayers, 1, 1);
            }

            private void StopVote()
            {
#if TESTING
			Debug.Log("[StopVote]");
#endif

                CancelInvoke(UpdatePlayers);

                if (_stage != Stage.Started)
                    return;

                _stage = Stage.InProgress;

#if TESTING
				Debug.Log("[StopVote] invoke destroy");
#endif

                var canBeSkip = CanBeSkip();
                if (canBeSkip)
                {
                    StartSkip();
                }
                else
                {
                    _skippedNight = 0;
                    if (_config.Votes.EnabledPay && _config.Votes.RefundWhenCanceled)
                        foreach (var votedPlayer in _votedPlayers)
                            _config.Economy.AddBalance(votedPlayer, _config.Votes.SkipCost);
                }

#if TESTING
				Debug.Log($"[StopVote] invoke destroy.canBeSkip={canBeSkip}");
#endif

                _isSkippedVoting = !canBeSkip;

                _config.Votes.StartCommands(canBeSkip);

                DrawUIs();

                Invoke(() =>
                {
                    DestroyUIs();

                    _stage = Stage.Stopped;
                }, _config.UI.DestroyTime);
            }

            private void UpdatePlayers()
            {
#if TESTING
			    // Debug.Log($"[UpdatePlayers] with _leftVoteTime={_leftVoteTime}");
#endif
                if (--_leftVoteTime <= 0)
                {
                    StopVote();
                    return;
                }

#if TESTING
			    // Debug.Log("[UpdatePlayers] before change update UI");
#endif

                foreach (var player in _openedUi.ToArray())
                {
                    _tempContainer.Clear();

                    ProgressUi(ref _tempContainer, player);

                    TimerUi(ref _tempContainer, player);

                    CuiHelper.AddUi(player, _tempContainer);
                }
                
                _tempContainer.Clear();
            }

            #endregion

            #region Interface

            private void MainUi(BasePlayer player)
            {
                var container = new CuiElementContainer();

                var hideVersion = IsHided(player);

                var voted = IsVoted(player);

                if (hideVersion)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0", AnchorMax = "1 0",
                            OffsetMin = $"-{_config.UI.LeftIndent + _config.UI.Width} {_config.UI.BottomIndent}",
                            OffsetMax = $"-{_config.UI.LeftIndent} {_config.UI.BottomIndent + _config.UI.DefaultHeight}"
                        },
                        Image =
                        {
                            Color = _config.UI.BackgroundColor.Get,
                            Material = _config.UI.BackgroundMaterial
                        }
                    }, "Overlay", Layer, Layer);

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "0 -35",
                            OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, TitleSkipNight),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = _config.UI.Colors.Color4.Get
                        }
                    }, Layer);
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0", AnchorMax = "1 0",
                            OffsetMin = $"-{_config.UI.LeftIndent + _config.UI.Width} {_config.UI.BottomIndent}",
                            OffsetMax =
                                $"-{_config.UI.LeftIndent} {_config.UI.BottomIndent + _config.UI.UnfoldedHeight}"
                        },
                        Image =
                        {
                            Color = _config.UI.BackgroundColor.Get,
                            Material = _config.UI.BackgroundMaterial
                        }
                    }, "Overlay", Layer, Layer);

                    if (_config.UI.ShowImage)
                        container.Add(new CuiElement
                        {
                            Parent = Layer,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = _instance.GetImage(_config.UI.Image)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                    OffsetMin =
                                        $"-{_config.UI.ImageWidth / 2f} -{_config.UI.ImageUpIndent + _config.UI.ImageHeight}",
                                    OffsetMax = $"{_config.UI.ImageWidth / 2f} -{_config.UI.ImageUpIndent}"
                                }
                            }
                        });

                    ProgressUi(ref container, player);

                    TimerUi(ref container, player);
                }

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = $"-{_config.UI.VotingButton.Width / 2f} {_config.UI.VotingButton.BottomIndent}",
                        OffsetMax =
                            $"{_config.UI.VotingButton.Width / 2f} {_config.UI.VotingButton.BottomIndent + _config.UI.VotingButton.Height}"
                    },
                    Text =
                    {
                        Text = _stage != Stage.Started
                            ? CanBeSkip() ? Msg(player, WillBeSkip) : Msg(player, VoteCancelled)
                            : voted
                                ? Msg(player, YouVoted)
                                : Msg(player, VoteBtn),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = _config.UI.Colors.Color1.Get
                    },
                    Button =
                    {
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Command = voted ? "" : "UI_SkipNight vote",
                        Color = voted
                            ? _config.UI.Colors.Color2.Get
                            : _config.UI.Colors.Color3.Get
                    }
                }, Layer, Layer + ".VoteBtn");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = _config.UI.HideBtn.AnchorMin,
                        AnchorMax = _config.UI.HideBtn.AnchorMax,
                        OffsetMin = _config.UI.HideBtn.OffsetMin,
                        OffsetMax = _config.UI.HideBtn.OffsetMax
                    },
                    Text =
                    {
                        Text = hideVersion ? Msg(player, UnHideBtn) : Msg(player, HideBtn),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Color = _config.UI.Colors.Color4.Get
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = "UI_SkipNight hide"
                    }
                }, Layer);

                if (_config.UI.CloseBtn.Enabled)
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.UI.CloseBtn.AnchorMin,
                            AnchorMax = _config.UI.CloseBtn.AnchorMax,
                            OffsetMin = _config.UI.CloseBtn.OffsetMin,
                            OffsetMax = _config.UI.CloseBtn.OffsetMax
                        },
                        Text =
                        {
                            Text = Msg(player, CloseBtn),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Color = _config.UI.Colors.Color4.Get
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UI_SkipNight close"
                        }
                    }, Layer);

                CuiHelper.AddUi(player, container);
            }

            private void TimerUi(ref CuiElementContainer container, BasePlayer player)
            {
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "0 85",
                        OffsetMax = "0 120"
                    },
                    Text =
                    {
                        Text = Msg(player, MainTitle, FormatTime(_leftVoteTime)),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = _config.UI.Colors.Color4.Get
                    }
                }, Layer, Layer + ".Timer", Layer + ".Timer");
            }

            private void ProgressUi(ref CuiElementContainer container, BasePlayer player)
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = $"-{_config.UI.ProgressBar.Width / 2f} {_config.UI.ProgressBar.BottomIndent}",
                        OffsetMax =
                            $"{_config.UI.ProgressBar.Width / 2f} {_config.UI.ProgressBar.BottomIndent + _config.UI.ProgressBar.Height}"
                    },
                    Image =
                    {
                        Color = _config.UI.Colors.Color3.Get
                    }
                }, Layer, Layer + ".Progress", Layer + ".Progress");

                var progress = _votedPlayers.Count / (float) needVotes;
                if (progress > 0)
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = $"{progress} 1",
                            OffsetMin = "2 2", OffsetMax = "-2 -2"
                        },
                        Image =
                        {
                            Color = _config.UI.Colors.Color1.Get
                        }
                    }, Layer + ".Progress");

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Progress",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, NeedVotes),
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 10,
                            Color = "1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "5 0", OffsetMax = "-5 0"
                        }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "5 0", OffsetMax = "-5 0"
                    },
                    Text =
                    {
                        Text = $"{needVotes - _votedPlayers.Count}",
                        Align = TextAnchor.MiddleRight,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 10,
                        Color = _config.UI.Colors.Color4.Get
                    }
                }, Layer + ".Progress");
            }

            #endregion

            #region Helpers

            private void DrawUIs()
            {
                foreach (var player in BasePlayer.activePlayerList) MainUi(player);
            }

            private void DestroyUIs()
            {
                CancelInvoke();

                _votedPlayers.Clear();
                _openedUi.Clear();

                foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);
            }

            private HashSet<BasePlayer> _openedUi = new();

            private bool IsHided(BasePlayer player)
            {
                return !_openedUi.Contains(player);
            }

            public void SwitchHide(BasePlayer player)
            {
                if (!_openedUi.Remove(player))
                    _openedUi.Add(player);

                MainUi(player);
            }

            public void Close(BasePlayer player)
            {
                _openedUi.Remove(player);

                CuiHelper.DestroyUi(player, Layer);
            }

            private HashSet<BasePlayer> _votedPlayers = new();

            private bool IsVoted(BasePlayer player)
            {
                return _votedPlayers.Contains(player);
            }

            public void Vote(BasePlayer player)
            {
                if (_config.Votes.EnabledPay)
                    if (!_config.Economy.RemoveBalance(player, _config.Votes.SkipCost))
                    {
                        _instance?.Reply(player, NotEnoughMoney);
                        return;
                    }

                if (_votedPlayers.Count < needVotes)
                    _votedPlayers.Add(player);

                MainUi(player);

                if (CanBeSkip())
                {
                    StopVote();
                }
                else
                {
                    if (_config.UI.CloseAfterVoting)
                        Invoke(() => Close(player), _config.UI.DestroyTime);
                }
            }

            private bool CanBeSkip()
            {
                return _votedPlayers.Count == needVotes;
            }

            public void StartSkip()
            {
#if TESTING
    Debug.Log($"[StartSkip] _skippedNight incremented: {_skippedNight}");
#endif
                _skippedNight++;

                if (_config.Time.ForceSkip)
                {
                    timeComponent.AddHours(CalculateHoursToSkip(TOD_Sky.Instance.Cycle.DateTime), false);
                    return;
                }

                if (_config.Time.FullMoon)
                    SetFullMoon();
                
                isDay = false;
                UpdateDayLenght(_config.Time.LengthFastNight, true);
                Interface.Oxide.CallHook("OnNightStart");
            }

            private void SetFullMoon()
            {
                var secondToMoon = CalculateSecondToMoonDate(TOD_Sky.Instance.Cycle.DateTime);

                timeComponent.AddSeconds(secondToMoon);
            }

            public void OnMinute()
            {
                if (CurrentTime >= _config.Time.TimeStartHours &&
                    CurrentTime < _config.Time.TimeEndHours)
                {
                    if (_stage == Stage.Stopped && !_isSkippedVoting)
                    {
#if TESTING
					Debug.Log($"[OnMinute] call StartVote after _stage={_stage} and _isSkippedVoting={_isSkippedVoting}");
#endif
                        StartVote();
                    }

                    return;
                }

                _isSkippedVoting = false;

                if (_config.Time.DayStartHours <= CurrentTime &&
                    CurrentTime < _config.Time.NightStartHours)
                {
                    if (isDay) return;
                    isDay = true;

                    UpdateDayLenght(_config.Time.LengthDay, false);
                    Interface.Oxide.CallHook("OnDayStart");
                }
                else
                {
                    if (!isDay) return;
                    isDay = false;
                    UpdateDayLenght(_config.Time.LengthNight, true);
                    Interface.Oxide.CallHook("OnNightStart");
                }
            }

            public void OnDay()
            {
#if TESTING
    Debug.Log($"[OnDay] Current _skippedNight={_skippedNight}, MissedNights={_config.Time.MissedNights}");
#endif

                if (CanResetSkippingNight())
                {
#if TESTING
        Debug.Log($"[OnDay] Resetting _skippedNight to 0");
#endif
                    _skippedNight = 0;
                }
            }

            private void UpdateDayLenght(float Lenght, bool night)
            {
                var dif = (float) (_config.Time.NightStartHours -
                                   _config.Time.DayStartHours).TotalHours;

                if (night) dif = 24 - dif;

                var part = 24.0f / dif;

                var newLenght = part * Lenght;

                if (newLenght <= 0) newLenght = 0.1f;

                timeComponent.DayLengthInMinutes = newLenght;
            }

            private bool IsLimitedSkippingNight()
            {
                return _config.Time.MissedNights > 0 && _skippedNight >= _config.Time.MissedNights;
            }

            private bool CanResetSkippingNight() =>
                _skippedNight > _config.Time.MissedNights && _config.Time.MissedNights > 0;

            #endregion

            #region Destroy

            public void Kill()
            {
                DestroyImmediate(this);
            }

            private void OnDestroy()
            {
                CancelInvoke(UpdatePlayers);

                CancelInvoke();

                if (timeComponent != null)
                {
                    timeComponent.OnMinute -= OnMinute;
                    timeComponent.OnDay -= OnDay;
                }

                Destroy(gameObject);
                Destroy(this);
            }

            #endregion
        }

        #endregion

        #region Utils

        private void InitEngine()
        {
            _engine = new GameObject().AddComponent<SkipEngine>();
        }

        private void DestroyEngine()
        {
            if (_engine != null)
                _engine.Kill();
        }

        private static string FormatTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);

            var result = new List<int>();
            if (time.Days != 0)
                result.Add(time.Days);

            if (time.Hours != 0)
                result.Add(time.Hours);

            if (time.Minutes != 0)
                result.Add(time.Minutes);

            if (time.Seconds != 0)
                result.Add(time.Seconds);

            return string.Join(":", result.Select(x => x.ToString()));
        }

        private static float CalculateHoursToSkip(DateTime now)
        {
            var targetTime = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).Add(_config.Time.GetTimeSet());

            if (now > targetTime) targetTime = targetTime.AddDays(1);

            return Convert.ToSingle(targetTime.Subtract(now).TotalHours);
        }

        private static float CalculateSecondToMoonDate(DateTime now)
        {
            var targetTime = _config.Time.FullMoonDates.GetRandom();

            return (float) targetTime.Subtract(now).TotalSeconds;
        }

        #region Working with Images

        private string GetImage(string name)
        {
#if CARBON
		return imageDatabase.GetImageString(name);
#else
            return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
        }

        private void LoadImages()
        {
#if CARBON
		imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif

            var imagesList = new Dictionary<string, string>();

            if (_config.UI.ShowImage)
                RegisterImage(ref imagesList, _config.UI.Image);

#if CARBON
		imageDatabase.Queue(false, imagesList);
#else
            timer.In(1f, () =>
            {
                if (ImageLibrary is not {IsLoaded: true})
                {
                    BroadcastILNotInstalled();
                    return;
                }

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            });
#endif
        }

        private void BroadcastILNotInstalled()
        {
            for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
        }

        private void RegisterImage(ref Dictionary<string, string> imagesList, string image)
        {
            if (!string.IsNullOrEmpty(image))
                imagesList.TryAdd(image, image);
        }

        #endregion

        private void RegisterCommands()
        {
            AddCovalenceCommand(_config.Votes.VoteCommands, nameof(CmdVote));

            AddCovalenceCommand("sn.admin", nameof(CmdAdminSkipNight));
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(PERM_ADMIN, this);
        }

        private static int PlayerItemsCount(BasePlayer player, string shortname, ulong skin)
        {
            var items = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(items);

            var result = ItemCount(items, shortname, skin);

            Pool.Free(ref items);
            return result;
        }

        private static int ItemCount(List<Item> items, string shortname, ulong skin)
        {
            return items.FindAll(item =>
                    item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
                .Sum(item => item.amount);
        }

        private static void Take(List<Item> itemList, string shortname, ulong skinId, int amountToTake)
        {
            if (amountToTake == 0) return;

            var takenAmount = 0;

            var itemsToTake = Pool.Get<List<Item>>();

            foreach (var item in itemList)
            {
                if (item.info.shortname != shortname ||
                    (skinId != 0 && item.skin != skinId) || item.isBroken) continue;

                var remainingAmount = amountToTake - takenAmount;
                if (remainingAmount <= 0) continue;

                if (item.amount > remainingAmount)
                {
                    item.MarkDirty();
                    item.amount -= remainingAmount;
                    break;
                }

                if (item.amount <= remainingAmount)
                {
                    takenAmount += item.amount;
                    itemsToTake.Add(item);
                }

                if (takenAmount == amountToTake)
                    break;
            }

            foreach (var itemToTake in itemsToTake)
                itemToTake.RemoveFromContainer();

            Pool.FreeUnmanaged(ref itemsToTake);
        }

        #endregion

        #region Lang

        private const string
            NotEnoughMoney = "NotEnoughMoney",
            VotingBroadcast = "VotingBroadcast",
            CloseBtn = "CloseBtn",
            MsgVoted = "MsgVoted",
            NeedVotes = "NeedVotes",
            MainTitle = "MainTitle",
            UnHideBtn = "UnHideBtn",
            HideBtn = "HideBtn",
            VoteBtn = "VoteBtn",
            YouVoted = "YouVoted",
            VoteCancelled = "VoteCancelled",
            WillBeSkip = "WillBeSkip",
            TitleSkipNight = "TitleSkipNight";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [TitleSkipNight] = "Skip Night",
                [WillBeSkip] = "The night will be skipped",
                [VoteCancelled] = "Voting canceled",
                [YouVoted] = "You voted",
                [VoteBtn] = "Vote",
                [HideBtn] = "▼",
                [UnHideBtn] = "▲",
                [MainTitle] = "Skip Night:\n{0}",
                [NeedVotes] = "Votes required",
                [MsgVoted] = "You have voted to skip night",
                [VotingBroadcast] = "Voting has begun for skip the night!",
                [CloseBtn] = "✕",
                [NotEnoughMoney] = "You don't have enough money!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [TitleSkipNight] = "Пропустить ночь",
                [WillBeSkip] = "Ночь будет пропущена",
                [VoteCancelled] = "Голосование отменено",
                [YouVoted] = "Вы проголосовали",
                [VoteBtn] = "Проголосовать",
                [HideBtn] = "▼",
                [UnHideBtn] = "▲",
                [MainTitle] = "Пропустить ночь:\n{0}",
                [NeedVotes] = "Требуется голосов",
                [VotingBroadcast] = "Началось голосование за пропуск ночи!",
                [CloseBtn] = "✕",
                [NotEnoughMoney] = "У вас недостаточно денег!"
            }, this, "ru");
        }

        private static string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(_instance.lang.GetMessage(key, _instance, userid), obj);
        }

        private static string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(_instance.lang.GetMessage(key, _instance, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(player, key, obj));
        }

        #endregion

        #region Testing Functions

#if TESTING
	[ConsoleCommand("sn.test.vote")]
	private void CmdTestVote(ConsoleSystem.Arg arg)
	{
		if (!arg.IsServerside) return;
		
		_engine?.StartSkip();
	}
#endif

        #endregion
    }
}