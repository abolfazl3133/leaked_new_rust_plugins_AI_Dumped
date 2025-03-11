using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hud", "AhigaO#4485", "2.0.1")]
    internal class Hud : RustPlugin
    {
        #region Static

        private const string Layer = "UI_Hud";
        private Configuration _config;
        private Data _data;
        private bool isLeft;
        private bool isBradley, isHelicopter, isCH47, isCargoShip, isCargoPlane;
        private int active, sleeping, joining;
        private bool update;
        private Coroutine UpdateAction;
        private TOD_Sky sky;
        private float scale;
        private float opacity;

        #region Image

        [PluginReference] private Plugin ImageLibrary, Economics, ServerRewards;
        private int ILCheck = 0;
        private Dictionary<string, string> Images = new Dictionary<string, string>();

        private void AddImage(string url)
        {
            if (!ImageLibrary.Call<bool>("HasImage", url)) ImageLibrary.Call("AddImage", url, url);
            Images.Add(url, ImageLibrary.Call<string>("GetImage", url));
        }

        private string GetImage(string url) => Images[url];

        private void LoadImages()
        {
            AddImage("https://i.imgur.com/uo5gMSz.png");
            AddImage("https://i.imgur.com/Z0lMApg.png");
            AddImage("https://i.imgur.com/IoVGwG7.png");
            AddImage("https://i.imgur.com/qAiXjnk.png");
            AddImage("https://i.imgur.com/u1ifv3O.png");
            AddImage("https://i.imgur.com/wGOtMGr.png");

            AddImage("https://i.imgur.com/dwF8AmR.png");
            AddImage("https://i.imgur.com/6xbipyP.png");
            AddImage("https://i.imgur.com/TfU4XTJ.png");

            AddImage("https://i.imgur.com/lCEwbmj.png");
            AddImage("https://i.imgur.com/mctY9ka.png");

            AddImage("https://i.imgur.com/aod6349.png");
            AddImage("https://i.imgur.com/VV5z3R6.png");
        }

        #endregion

        #region Classes

        private class Configuration
        {
            [JsonProperty(PropertyName = "Don't do Hud on top of everything")]
            public bool hide = true;

            [JsonProperty(PropertyName = "Hud scale")]
            public float scaleC = 1;

            [JsonProperty(PropertyName = "Hud transparency")]
            public float opacityC = 1;

            [JsonProperty(PropertyName = "Name of your server")]
            public string serverName = "YOUR SERVER NAME";

            [JsonProperty(PropertyName = "Server name color")]
            public string serverNameColor = "#B6FFFF";

            [JsonProperty(PropertyName = "Hud position on the screen(Left of Right)")]
            public string screenAngle = "Left";

            [JsonProperty(PropertyName = "Offset from the top of the screen(in px)")]
            public int marginTop = 5;

            [JsonProperty(PropertyName = "Offset from the right/left of the screen")]
            public int marginBorder = 10;

            [JsonProperty(PropertyName = "Use the Economics plugin")]
            public bool useEconomics = false;

            [JsonProperty(PropertyName = "User the Server Rewards plugin")]
            public bool useServerRewards = false;

            [JsonProperty(PropertyName = "Use additional menu")]
            public bool useAdditionalMenu = true;
        }

        private class Data
        {
            public Dictionary<ulong, bool> _players = new Dictionary<ulong, bool>();
            public List<Commands> commands = new List<Commands>();
        }

        private class Commands
        {
            public string text;
            public string color;
            public string command;
            public bool isConsole;
        }

        #endregion

        #endregion

        #region Config

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

        #region Data

        private void LoadData() => _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data") ? Interface.Oxide.DataFileSystem.ReadObject<Data>($"{Name}/data") : new Data();
        private void OnServerSave() => SaveData();

        private void SaveData()
        {
            if (_data != null) Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
        }

        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                if (ILCheck == 3)
                {
                    PrintError("Plugin ImageLibrary not found! You can download this plugin here -> https://umod.org/plugins/image-library");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }

                timer.In(1, () =>
                {
                    ILCheck++;
                    OnServerInitialized();
                });
                return;
            }

            scale = _config.scaleC;
            opacity = _config.opacityC;

            if (_config.useEconomics && _config.useServerRewards)
            {
                _config.useEconomics = false;
                _config.useServerRewards = false;
                SaveConfig();
                PrintError("You cannot use two balance plugins at the same time. Both options were disabled");
            }

            isLeft = _config.screenAngle == "Left";
            active = BasePlayer.activePlayerList.Count;
            sleeping = BasePlayer.sleepingPlayerList.Count(x => x.userID.IsSteamId());
            joining = ServerMgr.Instance.connectionQueue.Joining;
            sky = TOD_Sky.Instance;
            update = true;
            foreach (var check in BaseNetworkable.serverEntities)
            {
                if (!isBradley && check is BradleyAPC)
                {
                    isBradley = true;
                    continue;
                }

                if (!isHelicopter && check is BaseHelicopter)
                {
                    isHelicopter = true;
                    continue;
                }

                if (!isCH47 && check is CH47Helicopter)
                {
                    isCH47 = true;
                    continue;
                }

                if (!isCargoShip && check is CargoShip)
                {
                    isCargoShip = true;
                    continue;
                }

                if (isCargoPlane || !(check is SupplyDrop)) continue;
                isCargoPlane = true;
            }

            LoadData();
            LoadImages();
            foreach (var check in BasePlayer.activePlayerList) OnPlayerConnected(check);
            UpdateAction = ServerMgr.Instance.StartCoroutine(UpdateTime());
        }

        private void OnEntitySpawned(BradleyAPC entity)
        {
            if (entity == null || isBradley || !update) return;
            isBradley = true;
            ShowUIPanelTank();
        }

        private void OnEntitySpawned(BaseHelicopter entity)
        {
            if (entity == null || isHelicopter || !update) return;
            isHelicopter = true;
            ShowUIPanelHelicopter();
        }

        private void OnEntitySpawned(CH47Helicopter entity)
        {
            if (entity == null || isCH47 || !update) return;
            isCH47 = true;
            ShowUIPanelBigHelicopter();
        }

        private void OnEntitySpawned(CargoShip entity)
        {
            if (entity == null || isCargoShip || !update) return;
            isCargoShip = true;
            ShowUIPanelShip();
        }

        private void OnEntitySpawned(SupplyDrop entity)
        {
            if (entity == null || isCargoPlane || !update) return;
            isCargoPlane = true;
            ShowUIPanelAirDrop();
        }

        private void OnEntityKill(BradleyAPC entity)
        {
            if (entity == null || !isBradley || !update) return;
            isBradley = false;
            ShowUIPanelTank();
        }

        private void OnEntityKill(BaseHelicopter entity)
        {
            if (entity == null || !isHelicopter || !update) return;
            isHelicopter = false;
            ShowUIPanelHelicopter();
        }

        private void OnEntityKill(CH47Helicopter entity)
        {
            if (entity == null || !isCH47 || !update) return;
            isCH47 = false;
            ShowUIPanelBigHelicopter();
        }

        private void OnUserApprove(Network.Connection connection)
        {
            if (connection == null) return;
            NextTick(UpdateJoining);
        }

        private void OnEntityKill(CargoShip entity)
        {
            if (entity == null || !isCargoShip) return;
            isCargoShip = false;
            ShowUIPanelShip();
        }

        private void OnEntityKill(SupplyDrop entity)
        {
            if (entity == null || !isCargoPlane) return;
            if (BaseNetworkable.serverEntities.OfType<SupplyDrop>().Any()) return;
            isCargoPlane = false;
            ShowUIPanelAirDrop();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (!_data._players.ContainsKey(player.userID)) _data._players.Add(player.userID, true);
            NextTick(() =>
            {
                UpdateActive();
                UpdateJoining();
            });
            if (!_data._players[player.userID])
            {
                ShowUIMiniPanelOpenLeft(player);
                return;
            }

            ShowUIPanel(player);
        }

        private void OnPlayerSleep(BasePlayer player) => NextTick(UpdateSleeping);
        private void OnPlayerSleepEnded(BasePlayer player) => NextTick(UpdateSleeping);
        private void OnPlayerDisconnected(BasePlayer player) => NextTick(UpdateActive);

        private void OnEconomicsBalanceUpdated(string playerId, double amount)
        {
            if (!_config.useEconomics) return;
            var player = BasePlayer.FindByID(ulong.Parse(playerId));
            if (player == null) return;
            ShowUIPanelMoney(player, (int) amount);
        }

        private void Unload()
        {
            update = false;
            ServerMgr.Instance.StopCoroutine(UpdateTime());
            SaveData();
            foreach (var check in BasePlayer.activePlayerList) CuiHelper.DestroyUi(check, Layer);
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_HD")]
        private void cmdConsole(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null && arg.Args.Length < 1) return;
            var player = arg.Player();
            switch (arg.GetString(0))
            {
                case "OPENADDITIONALMENU":
                    ShowUIAdditionalMenu(player);
                    CuiHelper.DestroyUi(player, Layer + ".btnOpen");
                    ShowUIAdditionalMenuClose(player);
                    break;
                case "CLOSEADDITIONALMENU":
                    CuiHelper.DestroyUi(player, Layer + ".additionalMenu");
                    CuiHelper.DestroyUi(player, Layer + ".btnClose");
                    ShowUIAdditionalMenuOpen(player);
                    break;
                case "CHTCOM":
                    player.SendConsoleCommand($"chat.say \"{string.Join(" ", arg.Args.Skip(1))}\"");
                    break;
                case "SETSCALE":
                    if (arg.Args.Length < 2 || arg.GetFloat(1) < 0.1f) return;
                    _config.scaleC = arg.GetFloat(1);
                    scale = _config.scaleC;
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETOPACITY":
                    if (arg.Args.Length < 2 || arg.GetFloat(1) > 1) return;
                    _config.opacityC = arg.GetFloat(1);
                    opacity = _config.opacityC;
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETOFFSETTOP":
                    if (arg.Args.Length < 2) return;
                    _config.marginTop = arg.GetInt(1);
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETOFFSETBORDER":
                    if (arg.Args.Length < 2) return;
                    _config.marginBorder = arg.GetInt(1);
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETSERVERNAME":
                    if (arg.Args.Length < 2) return;
                    _config.serverName = string.Join(" ", arg.Args.Skip(1));
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "HUDPOS":
                    if (_config.screenAngle == "Left")
                    {
                        _config.screenAngle = "Right";
                        isLeft = false;
                    }
                    else
                    {
                        _config.screenAngle = "Left";
                        isLeft = true;
                    }

                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETECONOMICS":
                    _config.useEconomics = !_config.useEconomics;
                    _config.useServerRewards = false;
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETSERVERREWARDS":
                    _config.useServerRewards = !_config.useServerRewards;
                    _config.useEconomics = false;
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETHIDE":
                    _config.hide = !_config.hide;
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "SETADDITIONALMENU":
                    _config.useAdditionalMenu = !_config.useAdditionalMenu;
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
                case "ADDNEWCOMMAND":
                    ShowUIAddCommand(player, arg.GetInt(1));
                    break;
                case "CHANGEEXISTCOMMAND":
                    var chglist = arg.Args.ToList();
                    var chaColor = chglist.IndexOf("color");
                    var chaCommand = chglist.IndexOf("command");
                    var chaIsConsole = chglist.IndexOf("isConsole");
                    var chaName = chglist.IndexOf("name");
                    ShowUIAddCommand(player, arg.GetInt(1), true, HexToRustFormat(arg.GetString(chaColor + 1)) != "false" ? arg.GetString(chaColor + 1) : "#ffffff", string.Join(" ", chglist.Skip(chaCommand + 1).Take(chaIsConsole - (chaCommand + 1))), arg.GetBool(chaIsConsole + 1), string.Join(" ", chglist.Skip(chaName + 1)));
                    break;
                case "SETBUTTONNAME":
                    var bnlist = arg.Args.ToList();
                    var bnaColor = bnlist.IndexOf("color");
                    var bnaCommand = bnlist.IndexOf("command");
                    var bnaIsConsole = bnlist.IndexOf("isConsole");
                    var bnaName = bnlist.IndexOf("name");
                    if (bnlist.Count > bnaName + 1) ShowUIAddCommand(player, arg.GetInt(1), arg.GetBool(2), HexToRustFormat(arg.GetString(bnaColor + 1)) != "false" ? arg.GetString(bnaColor + 1) : "#ffffff", string.Join(" ", bnlist.Skip(bnaCommand + 1).Take(bnaIsConsole - (bnaCommand + 1))), arg.GetBool(bnaIsConsole + 1), string.Join(" ", bnlist.Skip(bnaName + 1)));
                    break;
                case "SETTEXTCOLOR":
                    var clist = arg.Args.ToList();
                    var caColor = clist.IndexOf("color");
                    var caCommand = clist.IndexOf("command");
                    var caIsConsole = clist.IndexOf("isConsole");
                    var caName = clist.IndexOf("name");
                    if (clist.Count > caColor + 1) ShowUIAddCommand(player, arg.GetInt(1), arg.GetBool(2), HexToRustFormat(arg.GetString(caColor + 1)) != "false" ? arg.GetString(caColor + 1) : "#ffffff", string.Join(" ", clist.Skip(caCommand + 1).Take(caIsConsole - (caCommand + 1))), arg.GetBool(caIsConsole + 1), string.Join(" ", clist.Skip(caName + 1).Take(caColor - (caName + 1))));
                    break;
                case "SETCOMMAND":
                    var cmlist = arg.Args.ToList();
                    var cmaColor = cmlist.IndexOf("color");
                    var cmaCommand = cmlist.IndexOf("command");
                    var cmaIsConsole = cmlist.IndexOf("isConsole");
                    var cmaName = cmlist.IndexOf("name");
                    if (cmlist.Count > cmaCommand + 1) ShowUIAddCommand(player, arg.GetInt(1), arg.GetBool(2), HexToRustFormat(arg.GetString(cmaColor + 1)) != "false" ? arg.GetString(cmaColor + 1) : "#ffffff", string.Join(" ", cmlist.Skip(cmaCommand + 1)), arg.GetBool(cmaIsConsole + 1), string.Join(" ", cmlist.Skip(cmaName + 1).Take(cmaCommand - (cmaName + 1))));
                    break;
                case "SETCONSOLE":
                    var cnlist = arg.Args.ToList();
                    var cnaColor = cnlist.IndexOf("color");
                    var cnaCommand = cnlist.IndexOf("command");
                    var cnaIsConsole = cnlist.IndexOf("isConsole");
                    var cnaName = cnlist.IndexOf("name");
                    if (cnlist.Count > cnaIsConsole + 1) ShowUIAddCommand(player, arg.GetInt(1), arg.GetBool(2), HexToRustFormat(arg.GetString(cnaColor + 1)) != "false" ? arg.GetString(cnaColor + 1) : "#ffffff", string.Join(" ", cnlist.Skip(cnaCommand + 1).Take(cnaIsConsole - (cnaCommand + 1))), !arg.GetBool(cnaIsConsole + 1), string.Join(" ", cnlist.Skip(cnaName + 1)));
                    break;
                case "ADDTOCOMMANDSLIST":
                    var addlist = arg.Args.ToList();
                    var addaColor = addlist.IndexOf("color");
                    var addaCommand = addlist.IndexOf("command");
                    var addaIsConsole = addlist.IndexOf("isConsole");
                    var addaName = addlist.IndexOf("name");
                    if (arg.GetBool(2))
                    {
                        _data.commands[arg.GetInt(1)] = new Commands()

                        {
                            text = string.Join(" ", addlist.Skip(addaName + 1)),
                            color = HexToRustFormat(arg.GetString(addaColor + 1)) != "false" ? arg.GetString(addaColor + 1) : "#ffffff",
                            command = string.Join(" ", addlist.Skip(addaCommand + 1).Take(addaIsConsole - (addaCommand + 1))),
                            isConsole = arg.GetBool(addaIsConsole + 1),
                        };
                    }
                    else
                    {
                        _data.commands.Add(new Commands()
                        {
                            text = string.Join(" ", addlist.Skip(addaName + 1)),
                            color = HexToRustFormat(arg.GetString(addaColor + 1)) != "false" ? arg.GetString(addaColor + 1) : "#ffffff",
                            command = string.Join(" ", addlist.Skip(addaCommand + 1).Take(addaIsConsole - (addaCommand + 1))),
                            isConsole = arg.GetBool(addaIsConsole + 1),
                        });
                    }

                    SaveData();
                    CuiHelper.DestroyUi(player, Layer + ".addcom");
                    ShowUISetup(player);
                    break;
                case "CLOSEADDMENU":
                    if (arg.GetBool(2))
                    {
                        _data.commands.Remove(_data.commands[arg.GetInt(1)]);
                        ShowUISetupPanel(player);
                        UpdateUIForAll();
                    }

                    CuiHelper.DestroyUi(player, Layer + ".addcom");
                    break;
                case "NEXTCOMMAND":
                    ShowUIAddAdditionalMenu(player, arg.GetInt(1));
                    break;
                case "SETCOLOR":
                    _config.serverNameColor = arg.GetString(1);
                    SaveConfig();
                    UpdateUIForAll();
                    ShowUISetupPanel(player);
                    break;
            }
        }

        [ConsoleCommand("UI_HUD")]
        private void cmdConsoleUI_HUD(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;
            _data._players[player.userID] = !_data._players[player.userID];
            if (!_data._players[player.userID])
            {
                ShowUIMiniPanelOpenLeft(player);
                return;
            }

            ShowUIPanel(player);
        }

        [ChatCommand("hsetup")]
        private void cmdChatmenu(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("You haven't permission for use this command");
                return;
            }

            ShowUISetup(player);
        }

        #endregion

        #region Functions

        private void UpdateUIForAll()
        {
            foreach (var check in BasePlayer.activePlayerList) ShowUIPanel(check);
        }

        private static string HexToRustFormat(string hex)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(hex, out color))
                return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            else return "false";
        }

        private IEnumerator UpdateTime()
        {
            while (update)
            {
                ShowUIPanelTime();
                if (_config.useServerRewards)
                    if (isLeft)
                        foreach (var check in BasePlayer.activePlayerList)
                            ShowUIPanelMoney(check, GetBalance(check.userID));
                yield return new WaitForSeconds(1f);
            }
        }

        private int GetBalance(ulong id)
        {
            if (!_config.useEconomics) return !ServerRewards ? 0 : ServerRewards.Call<int>("CheckPoints", id);
            if (!Economics) return 0;
            return (int) Economics.Call<double>("Balance", id);
        }

        private void UpdateActive()
        {
            active = BasePlayer.activePlayerList.Count;
            ShowUIPanelActivePlayers();
        }

        private void UpdateJoining()
        {
            joining = ServerMgr.Instance.connectionQueue.Joining;
            ShowUIPanelLinePlayers();
        }

        private void UpdateSleeping()
        {
            sleeping = BasePlayer.sleepingPlayerList.Count(x => x.userID.IsSteamId());
            ShowUIPanelSleepPlayers();
        }

        #endregion

        #region UI

        #region Hud

        private void ShowUIMiniPanelOpenLeft(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = _config.hide ? "Hud" : "Overlay",
                Name = Layer,
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/uo5gMSz.png"), Color = $"1 1 1 {opacity}"},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0 1" : "1 1", AnchorMax = isLeft ? "0 1" : "1 1", OffsetMin = $"{(isLeft ? -12 + _config.marginBorder : -78 * scale - _config.marginBorder)} {-78 * scale - _config.marginTop}", OffsetMax = $"{(isLeft ? 78 * scale + _config.marginBorder : 10 - _config.marginBorder)} {10 * scale - _config.marginTop}"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Color = "0 0 0 0", Command = "UI_HUD"},
                Text =
                {
                    Text = "", Font = "robotocondensed-bold.ttf", FontSize = (int) (15 * scale), Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Parent = _config.hide ? "Hud" : "Overlay",
                Name = Layer,
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage(isLeft ? _config.useAdditionalMenu ? "https://i.imgur.com/lCEwbmj.png" : "https://i.imgur.com/aod6349.png" : _config.useAdditionalMenu ? "https://i.imgur.com/mctY9ka.png" : "https://i.imgur.com/VV5z3R6.png"), Color = $"1 1 1 {opacity}"},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0 1" : "1 0.995", AnchorMax = isLeft ? "0 1" : "0.995 0.995", OffsetMin = $"{(isLeft ? _config.marginBorder : -320 * scale - _config.marginBorder)} {-132 * scale - _config.marginTop}", OffsetMax = $"{(isLeft ? 320 * scale + _config.marginBorder : -_config.marginBorder)} {-_config.marginTop}"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = isLeft ? "0 1" : "1 1", AnchorMax = isLeft ? "0 1" : "1 1", OffsetMin = $"{(isLeft ? 0 : -74 * scale)} {-78 * scale}", OffsetMax = $"{(isLeft ? 74 * scale : 0)} {-4 * scale}"},
                Button = {Color = "0 0 0 0", Command = "UI_HUD"},
                Text =
                {
                    Text = "", Font = "robotocondensed-bold.ttf", FontSize = (int) (15 * scale), Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = isLeft ? "1 0" : "0 0", AnchorMax = isLeft ? "1 0" : "0 0", OffsetMin = $"{(isLeft ? -180 : 20) * scale} {20 * scale}", OffsetMax = $"{(isLeft ? -20 : 180) * scale} {43 * scale}"},
                Text =
                {
                    Text = _config.serverName, Font = "robotocondensed-bold.ttf", FontSize = (int) (18 * scale), Align = isLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                    Color = HexToRustFormat(_config.serverNameColor)
                }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);

            ShowUIPanelTank(player);
            ShowUIPanelHelicopter(player);
            ShowUIPanelBigHelicopter(player);
            ShowUIPanelShip(player);
            ShowUIPanelAirDrop(player);

            ShowUIPanelLinePlayers(player);
            ShowUIPanelActivePlayers(player);
            ShowUIPanelSleepPlayers(player);
            ShowUIPanelTime(player);

            if (_config.useAdditionalMenu) ShowUIAdditionalMenuOpen(player);
            if (_config.useEconomics) ShowUIPanelMoney(player, GetBalance(player.userID));
        }

        private void ShowUIPanelTank(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".btnTank",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/Z0lMApg.png"), Color = isBradley ? $"0 1 0 {opacity}" : $"1 1 1 {opacity}"},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0 1" : "1 1", AnchorMax = isLeft ? "0 1" : "1 1", OffsetMin = $"{(isLeft ? 74 : -112) * scale} {-45 * scale}", OffsetMax = $"{(isLeft ? 112 : -74) * scale} {-7 * scale}"},
                }
            });

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".btnTank");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".btnTank");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelHelicopter(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".btnHelicopter",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/IoVGwG7.png"), Color = isHelicopter ? $"0 1 0 {opacity}" : $"1 1 1 {opacity}"},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0 1" : "1 1", AnchorMax = isLeft ? "0 1" : "1 1", OffsetMin = $"{(isLeft ? 120 : -158) * scale} {-45 * scale}", OffsetMax = $"{(isLeft ? 158 : -120) * scale} {-7 * scale}"},
                }
            });

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".btnHelicopter");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".btnHelicopter");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelBigHelicopter(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".btnBigHelicopter",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/qAiXjnk.png"), Color = isCH47 ? $"0 1 0 {opacity}" : $"1 1 1 {opacity}"},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0 1" : "1 1", AnchorMax = isLeft ? "0 1" : "1 1", OffsetMin = $"{(isLeft ? 166 : -204) * scale} {-45 * scale}", OffsetMax = $"{(isLeft ? 204 : -166) * scale} {-7 * scale}"},
                }
            });

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".btnBigHelicopter");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".btnBigHelicopter");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelShip(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".btnShip",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/u1ifv3O.png"), Color = isCargoShip ? $"0 1 0 {opacity}" : $"1 1 1 {opacity}"},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0 1" : "1 1", AnchorMax = isLeft ? "0 1" : "1 1", OffsetMin = $"{(isLeft ? 212 : -250) * scale} {-45 * scale}", OffsetMax = $"{(isLeft ? 250 : -212) * scale} {-7 * scale}"},
                }
            });

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".btnShip");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".btnShip");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelAirDrop(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".btnAirDrop",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/wGOtMGr.png"), Color = isCargoPlane ? $"0 1 0 {opacity}" : $"1 1 1 {opacity}"},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0 1" : "1 1", AnchorMax = isLeft ? "0 1" : "1 1", OffsetMin = $"{(isLeft ? 258 : -296) * scale} {-45 * scale}", OffsetMax = $"{(isLeft ? 296 : -258) * scale} {-7 * scale}"},
                }
            });

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".btnAirDrop");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".btnAirDrop");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelMoney(BasePlayer player, int amount)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = isLeft ? "0 0" : "1 0", AnchorMax = isLeft ? "0 0" : "1 0", OffsetMin = $"{(isLeft ? 42 : -120) * scale} {20 * scale}", OffsetMax = $"{(isLeft ? 120 : -42) * scale} {43 * scale}"},
                Text =
                {
                    Text = $"${amount}", Font = "robotocondensed-regular.ttf", FontSize = (int) (16 * scale), Align = isLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".money");


            CuiHelper.DestroyUi(player, Layer + ".money");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelLinePlayers(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = isLeft ? "0 0" : "1 0", AnchorMax = isLeft ? "0 0" : "1 0", OffsetMin = $"{(isLeft ? 65 : -65) * scale} {42 * scale}", OffsetMax = $"{(isLeft ? 95 : -40) * scale} {67 * scale}"},
                Text =
                {
                    Text = joining.ToString(), Font = "robotocondensed-regular.ttf", FontSize = (int) (16 * scale), Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".allPlayers");

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".allPlayers");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".allPlayers");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelSleepPlayers(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = isLeft ? "0 0" : "1 0", AnchorMax = isLeft ? "0 0" : "1 0", OffsetMin = $"{(isLeft ? 130 : -125) * scale} {42 * scale}", OffsetMax = $"{(isLeft ? 168 : -92) * scale} {67 * scale}"},
                Text =
                {
                    Text = sleeping.ToString(), Font = "robotocondensed-regular.ttf", FontSize = (int) (16 * scale), Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".sleepPlayers");

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".sleepPlayers");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".sleepPlayers");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelActivePlayers(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = isLeft ? "0 0" : "1 0", AnchorMax = isLeft ? "0 0" : "1 0", OffsetMin = $"{(isLeft ? 190 : -187) * scale} {42 * scale}", OffsetMax = $"{(isLeft ? 220 : -155) * scale} {67 * scale}"},
                Text =
                {
                    Text = active.ToString(), Font = "robotocondensed-regular.ttf", FontSize = (int) (16 * scale), Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".activePlayers");

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".activePlayers");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".activePlayers");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPanelTime(BasePlayer player = null)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = isLeft ? "1 0" : "0 0", AnchorMax = isLeft ? "1 0" : "0 0", OffsetMin = $"{(isLeft ? -75 : 20) * scale} {42 * scale}", OffsetMax = $"{(isLeft ? -20 : 75) * scale} {70 * scale}"},
                Text =
                {
                    Text = $"{sky.Cycle.DateTime.ToShortTimeString()}", Font = "robotocondensed-bold.ttf", FontSize = (int) (23 * scale), Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer, Layer + ".time");

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".time");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".time");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIAdditionalMenuOpen(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".btnOpen",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/dwF8AmR.png")},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0.545 0" : "0.475 0", AnchorMax = isLeft ? "0.545 0" : "0.475 0", OffsetMin = $"{-15 * scale} {-6 * scale}", OffsetMax = $"{15 * scale} {27 * scale}"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Color = "0 0 0 0", Command = "UI_HD OPENADDITIONALMENU"},
                Text =
                {
                    Text = "", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".btnOpen");

            CuiHelper.DestroyUi(player, Layer + ".btnOpen");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIAdditionalMenuClose(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".btnClose",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/6xbipyP.png")},
                    new CuiRectTransformComponent {AnchorMin = isLeft ? "0.545 0" : "0.475 0", AnchorMax = isLeft ? "0.545 0" : "0.475 0", OffsetMin = $"{-15 * scale} {-6 * scale}", OffsetMax = $"{15 * scale} {27 * scale}"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Color = "0 0 0 0", Command = "UI_HD CLOSEADDITIONALMENU"},
                Text =
                {
                    Text = "", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".btnClose");

            CuiHelper.DestroyUi(player, Layer + ".btnClose");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIAdditionalMenu(BasePlayer player)
        {
            if (!_config.useAdditionalMenu) return;
            var container = new CuiElementContainer();
            var posY = -35;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = isLeft ? "0.525 0" : "0.48 0", AnchorMax = isLeft ? "0.525 0" : "0.48 0"},
                Image = {Color = "0 0 0 0"}
            }, Layer, Layer + ".additionalMenu");

            foreach (var check in _data.commands)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".additionalMenu",
                    Name = Layer + ".line" + posY,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/TfU4XTJ.png"), Color = $"1 1 1 {opacity}"},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"{-80 * scale} {posY * scale}", OffsetMax = $"{80 * scale} {(posY + 30) * scale}"}
                    }
                });
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Button = {Color = "0 0 0 0", Command = check.isConsole ? check.command : $"UI_HD CHTCOM {check.command}"},
                    Text =
                    {
                        Text = check.text, Font = "robotocondensed-bold.ttf", FontSize = (int) (15 * scale), Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat(check.color)
                    }
                }, Layer + ".line" + posY);
                posY -= 37;
            }

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (!_data._players[check.userID]) continue;
                    CuiHelper.DestroyUi(check, Layer + ".additionalMenu");
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            CuiHelper.DestroyUi(player, Layer + ".additionalMenu");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region HudSetup

        private void ShowUISetup(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"}
            }, "Overlay", Layer + ".bgS");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.948 0.907", AnchorMax = "0.99 0.98"},
                Button = {Color = "0 0 0 0", Close = Layer + ".bgS"},
                Text =
                {
                    Text = "×", Font = "robotocondensed-regular.ttf", FontSize = 46, Align = TextAnchor.MiddleCenter,
                    Color = "0.56 0.58 0.64 1.00"
                }
            }, Layer + ".bgS", Layer + ".buttonClose");
            Outline(ref container, Layer + ".buttonClose");

            CuiHelper.DestroyUi(player, Layer + ".bgS");
            CuiHelper.AddUi(player, container);

            ShowUISetupPanel(player);
        }

        private void ShowUISetupPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var posY = -95;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.2 0.375", AnchorMax = "0.815 0.8"},
                Image = {Color = "0.07 0.00 0.56 0.2"}
            }, Layer + ".bgS", Layer + ".setup");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0.52 0.87 0.99 0.3", Sprite = "assets/content/ui/ui.background.transparent.linear.psd"}
            }, Layer + ".setup");
            Outline(ref container, Layer + ".setup", "1 1 1 1", "2");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -60", OffsetMax = "0 -15"},
                Text =
                {
                    Text = "HUD SETUP", Font = "robotocondensed-bold.ttf", FontSize = 28, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            #region Column 1

            #region row - 1

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.5 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = "Hud scale:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.45 1", AnchorMax = "0.5 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".setup", Layer + ".input");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = _config.scaleC.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".input");
            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 4, FontSize = 15,
                        Command = "UI_HD SETSCALE"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            posY -= 30;

            #endregion

            #region row - 2

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.5 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = "Hud transparency:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.45 1", AnchorMax = "0.5 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".setup", Layer + ".input");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = _config.opacityC.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".input");
            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 4, FontSize = 15,
                        Command = "UI_HD SETOPACITY"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            posY -= 30;

            #endregion

            #region row - 3

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.45 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = "Offset from the top of the screen(in px):", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.45 1", AnchorMax = "0.5 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".setup", Layer + ".input");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = _config.marginTop.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".input");
            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 4, FontSize = 15,
                        Command = "UI_HD SETOFFSETTOP"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            posY -= 30;

            #endregion

            #region row - 4

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.45 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = "Offset from the border of the screen(in px):", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.45 1", AnchorMax = "0.5 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".setup", Layer + ".input");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = _config.marginBorder.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 4, FontSize = 15,
                        Command = "UI_HD SETOFFSETBORDER"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            posY -= 30;

            #endregion

            #region row - 5

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.45 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = "Name your server:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.3 1", AnchorMax = "0.5 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".setup", Layer + ".input");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = _config.serverName, Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.5"
                }
            }, Layer + ".input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 18, FontSize = 18,
                        Command = "UI_HD SETSERVERNAME"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            #endregion

            #endregion

            posY = -95;

            #region Column 2

            #region row 1

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.525 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Button = {Color = "0 0 0 0", Command = "UI_HD HUDPOS"},
                Text =
                {
                    Text = $"Hud position on the screen(Left of Right): <color=#9DA1E6>{_config.screenAngle}</color>", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");
            posY -= 30;

            #endregion

            #region row 2

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.525 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = "Use the Economics:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.55 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Button = {Color = "0 0 0 0", Command = "UI_HD SETECONOMICS"},
                Text =
                {
                    Text = $"<color={(_config.useEconomics ? "green" : "red")}>Economics</color>  |", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.825 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Button = {Color = "0 0 0 0", Command = "UI_HD SETSERVERREWARDS"},
                Text =
                {
                    Text = $"<color={(_config.useServerRewards ? "green" : "red")}>Server Rewards</color>", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");
            posY -= 30;

            #endregion

            #region row 3

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.525 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Button = {Color = "0 0 0 0", Command = "UI_HD SETHIDE"},
                Text =
                {
                    Text = $"Don't do Hud on top of everything: {(_config.hide ? "<color=green>ON</color>" : "<color=red>OFF</color>")}", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");
            posY -= 30;

            #endregion

            #region row 4

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.525 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Button = {Color = "0 0 0 0", Command = "UI_HD SETADDITIONALMENU"},
                Text =
                {
                    Text = $"Use additional menu: {(_config.useAdditionalMenu ? "<color=green>ON</color>" : "<color=red>OFF</color>")}", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");
            posY -= 30;

            #endregion
            
            #region row 5

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.525 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = "Color of server name(HEX):", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.8 1", AnchorMax = "0.98 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".setup");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.8 1", AnchorMax = "0.98 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"},
                Text =
                {
                    Text = $"{_config.serverNameColor}", Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter,
                    Color = HexToRustFormat(_config.serverNameColor)
                }
            }, Layer + ".setup");

            container.Add(new CuiElement
            {
                Parent = Layer + ".setup",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 8, FontSize = 15,
                        Command = "UI_HD SETCOLOR"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0.8 1", AnchorMax = "0.98 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 25}"}
                }
            });

            posY -= 35;

            #endregion

            #region row 6

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = "ADDITIONAL MENU COMMANDS", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".setup");

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".setup");
            CuiHelper.AddUi(player, container);

            ShowUIAddAdditionalMenu(player);
        }

        private void ShowUIAddAdditionalMenu(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();
            var countList = _data.commands.Count;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -285", OffsetMax = $"0 -255"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".setup", Layer + ".additional");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-100 0", OffsetMax = "-82 0"},
                Button = {Color = "0 0 0 0", Command = page >= 1 ? $"UI_HD NEXTCOMMAND {page - 1}" : ""},
                Text =
                {
                    Text = "<", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".additional");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "82 0", OffsetMax = $"100 0"},
                Button = {Color = "0 0 0 0", Command = $"UI_HD NEXTCOMMAND {page + 1}"},
                Text =
                {
                    Text = ">", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".additional");

            if (countList != 0 && page < countList)
            {
                var check = _data.commands[page];

                container.Add(new CuiElement
                {
                    Parent = Layer + ".additional",
                    Name = Layer + ".input",
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/TfU4XTJ.png")},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-80 0", OffsetMax = "80 0"}
                    }
                });
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Button = {Color = "0 0 0 0", Command = $"UI_HD CHANGEEXISTCOMMAND {page} color {check.color} command {check.command} isConsole {check.isConsole} name {check.text}"},
                    Text =
                    {
                        Text = check.text, Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat(check.color)
                    }
                }, Layer + ".input");
            }
            else
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-80 0", OffsetMax = "80 0"},
                    Button = {Color = "0 0 0 0", Command = $"UI_HD ADDNEWCOMMAND {page}"},
                    Text =
                    {
                        Text = "+", Font = "robotocondensed-bold.ttf", FontSize = 25, Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, Layer + ".additional");

            CuiHelper.DestroyUi(player, Layer + ".additional");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIAddCommand(BasePlayer player, int i = 0, bool isChange = false, string color = "#ffffff", string command = "none", bool isConsole = true, string text = "ButtonName")
        {
            var container = new CuiElementContainer();
            var posY = -80;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.4 0.075", AnchorMax = "0.6 0.35"},
                Image = {Color = "0.25 0.25 0.25 0.8"}
            }, Layer + ".bgS", Layer + ".addcom");
            Outline(ref container, Layer + ".addcom");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5 -32", OffsetMax = "22 -15"},
                Button = {Color = "1 0 0 1", Sprite = "assets/icons/close.png", Command = $"UI_HD CLOSEADDMENU {i} {isChange}"},
                Text =
                {
                    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".addcom");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-25 -34", OffsetMax = "-5 -14"},
                Button = {Color = "0 1 0 1", Sprite = "assets/icons/check.png", Command = $"UI_HD ADDTOCOMMANDSLIST {i} {isChange} color {color} command {command} isConsole {isConsole} name {text}"},
                Text =
                {
                    Text = "", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".addcom");

            #region AddCom

            container.Add(new CuiElement
            {
                Parent = Layer + ".addcom",
                Name = Layer + ".input",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage("https://i.imgur.com/TfU4XTJ.png")},
                    new CuiRectTransformComponent {AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-80 -40", OffsetMax = "80 -10"}
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = text, Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = HexToRustFormat(color)
                }
            }, Layer + ".input");

            #endregion

            #region Set

            #region row 1

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = "Button name:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".addcom");
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.4 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".addcom", Layer + ".input");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = text, Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.1"
                }
            }, Layer + ".input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 45, FontSize = 18,
                        Command = $"UI_HD SETBUTTONNAME {i} {isChange} color {color} command {command} isConsole {isConsole} name"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            posY -= 35;

            #endregion

            #region row 2

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = "Text Color (HEX):", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".addcom");
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.525 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".addcom", Layer + ".input");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = color, Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.1"
                }
            }, Layer + ".input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 7, FontSize = 18,
                        Command = $"UI_HD SETTEXTCOLOR {i} {isChange} command {command} isConsole {isConsole} name {text} color"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            posY -= 35;

            #endregion

            #region row 3

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = "Set command:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".addcom");
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.525 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".addcom", Layer + ".input");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = command, Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.1"
                }
            }, Layer + ".input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 45, FontSize = 18,
                        Command = $"UI_HD SETCOMMAND {i} {isChange} color {color} isConsole {isConsole} name {text} command"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            posY -= 35;

            #endregion

            #region row 4

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = "Сommand is :", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer + ".addcom");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.55 1", AnchorMax = "1 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = $"UI_HD SETCONSOLE {i} {isChange} color {color} command {command} isConsole {isConsole} name {text}"},
                Text =
                {
                    Text = $"<color={(isConsole ? "green" : "red")}>Console</color> | <color={(!isConsole ? "green" : "red")}>Chat</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".addcom");

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".addcom");
            CuiHelper.AddUi(player, container);
        }

        private void Outline(ref CuiElementContainer container, string parent, string color = "1 1 1 1", string size = "1")
        {
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 0", OffsetMax = $"0 {size}"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{size}", OffsetMax = $"0 0"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 {size}", OffsetMax = $"{size} -{size}"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{size} {size}", OffsetMax = $"0 -{size}"},
                Image = {Color = color}
            }, parent);
        }

        #endregion

        #endregion
    }
}