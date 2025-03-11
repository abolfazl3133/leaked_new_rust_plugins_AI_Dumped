using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI; 

namespace Oxide.Plugins
{
    [Info("Hud", "ahigao", "3.3.5")]
    internal class Hud : RustPlugin
    {
        #region Static

        private const string PERM_STREAMER = "hud.streamer";
        private Timer InfoMessageUpdate;

        private int EnableEventsCount, EventsPosStartPosX;
        private Vector3 largeOilRigPosition, smallOilRigPosition;

        [PluginReference]
        private Plugin Economics, ServerRewards;

        private enum States
        {
            Full,
            Events,
            Hide,
            Close
        }

        #region ConfigAutoReplace

        private void ReplaceOldImages()
        {
            var linkReplace = new Dictionary<string, string>
            {
                ["https://www.dropbox.com/scl/fi/9j4fv475el3gfbq9wvoox/logo.png?rlkey=rq710tuayr8syuk54fljluqqa&dl=1"] = "https://i.ibb.co/3F3566J/logo.png",
                ["https://www.dropbox.com/scl/fi/weax6ug55o0u4f87bhchs/active.png?rlkey=lbpuhpqs7tutpgv1i4t3b1107&dl=1"] = "https://i.ibb.co/7vG49W1/active.png",
                ["https://www.dropbox.com/scl/fi/3d76i9rg4i34zprgekr3j/sleep.png?rlkey=s57ehdeoc16h58ace6s5yuhcl&dl=1"] = "https://i.ibb.co/2q5mzmC/sleep.png",
                ["https://www.dropbox.com/scl/fi/aneas55vx6pnrwmg8bh1z/line.png?rlkey=kwmlqihaopckijm98ta0qn9p2&dl=1"] = "https://i.ibb.co/Cm65TY9/line.png",
                ["https://www.dropbox.com/scl/fi/ypbnnbs07i2t322x1pu4c/command.png?rlkey=a693i43bn70wiy2p0v0216uw7&dl=1"] = "https://i.ibb.co/6Z5SCrW/command.png",
                ["https://www.dropbox.com/scl/fi/i7531lubr8jy62w6zugkt/bradley.png?rlkey=fpnpmqndjgt63hamyrd0jzyc2&dl=1"] = "https://i.ibb.co/X8xVTjc/bradley.png",
                ["https://www.dropbox.com/scl/fi/140cku9l167y8i778br2i/heli.png?rlkey=25wxs16n0s9dk8uea7bn0p7sd&dl=1"] = "https://i.ibb.co/m6wwkhH/heli.png",
                ["https://www.dropbox.com/scl/fi/a0cuylwwtrwqrxr933jpd/ch47.png?rlkey=w9rv4ap58hmr3fwtkp4et4dn1&dl=1"] = "https://i.ibb.co/CVCPVpN/ch47.png",
                ["https://www.dropbox.com/scl/fi/lt4b833tybzzyas7bxst0/cargo.png?rlkey=i0xn03ctyz93a4rbybol2x5c2&dl=1"] = "https://i.ibb.co/QmT6MFX/cargo.png",
                ["https://www.dropbox.com/scl/fi/udt3221xk7yk2zwqdd7id/airdrop.png?rlkey=7642d4ye3n63rby8nz0ej9ayr&dl=1"] = "https://i.ibb.co/580XRGw/airdrop.png",
                ["https://www.dropbox.com/scl/fi/s2ovq1yo8umvkn213cno0/convoy.png?rlkey=9mxlg4ypbypk5be59b57qgck3&dl=1"] = "https://i.ibb.co/qRyD0DY/convoy.png",
                ["https://www.dropbox.com/scl/fi/tlys9ulw51i74fpm6oec0/sputnik.png?rlkey=oe27iqf5ali445c90mgmcilea&dl=1"] = "https://i.ibb.co/9Tkb44Z/sputnik.png",
                ["https://www.dropbox.com/scl/fi/d78jg4yrer0vqdspkxpg2/train.png?rlkey=9m4xll5wv6qrr0u2kb3pd2o4z&dl=1"] = "https://i.ibb.co/3v3JcQ8/train.png",
                ["https://www.dropbox.com/scl/fi/3qzyayoa4dxog2t8gyvzw/harbor.png?rlkey=v7jnpk8tyngwywvhbgrfhk5a4&dl=1"] = "https://i.ibb.co/P91x3zR/harbor.png",
            };

            _config.MainSetup.Logo = Replace(_config.MainSetup.Logo);
            _config.MainSetup.SleepPlayersAppearance.Icon = Replace(_config.MainSetup.SleepPlayersAppearance.Icon);
            _config.MainSetup.QueuePlayersAppearance.Icon = Replace(_config.MainSetup.QueuePlayersAppearance.Icon);
            _config.MainSetup.ActivePlayersAppearance.Icon = Replace(_config.MainSetup.ActivePlayersAppearance.Icon);

            foreach (var check in _config.MainSetup.AdditionalMenu.Commands)
            {
                check.Icon = Replace(check.Icon);
                check.Image = Replace(check.Image);
            }

            foreach (var check in _config.BaseEvents)
                check.Icon = Replace(check.Icon);

            foreach (var check in _config.CustomEvents)
                check.Icon = Replace(check.Icon);
            
            SaveConfig();
            return;

            string Replace(string url)
            {
                var newString = url;
                foreach (var check in linkReplace)
                    newString = newString.Replace(check.Key, check.Value);

                return newString;
            }
        }

        #endregion

        #region Classes

        private class Configuration
        {
            [JsonIgnore] public string JSON;

            [JsonProperty("Auto reload [If you change the config and save the file the plugin will reload itself]")]
            public bool AutoReload = false;

            [JsonProperty(PropertyName = "Other images")]
            public OtherImages Images = new OtherImages();

            [JsonProperty("Main setup")]
            public MainSetup MainSetup = new MainSetup
            { 
                IsOverall = false,
                Size = 80,
                Logo = "https://i.ibb.co/3F3566J/logo.png",
                EventsBGOpacity = 80,
                BGOpacity = 60,
                Position = new Postition
                {
                    Align = "TopLeft",
                    LeftOffSet = 40,
                    TopOffSet = 25,
                },
                ServerName = "Your Server Name",
                ActivePlayersAppearance = new ActiveAppearance
                {
                    Color = "#fff",
                    Icon = "https://i.ibb.co/7vG49W1/active.png",
                    IsEnable = true,
                    IsAdminsCount = false,
                },
                SleepPlayersAppearance = new BaseAppearance
                {
                    Color = "#fff",
                    Icon = "https://i.ibb.co/2q5mzmC/sleep.png",
                    IsEnable = true,
                },
                QueuePlayersAppearance = new BaseAppearance
                {
                    Color = "#fff",
                    Icon = "https://i.ibb.co/Cm65TY9/line.png",
                    IsEnable = true,
                },
                Time = new TimeAppearance
                {
                    IsAutomaticDetection = true,
                    IsAmPm = false,
                    Color = "#fff",
                    IsEnable = true,
                },
                PlayerPosition = new PlayerPositionAppearance
                {
                    IsGrid = true,
                    Color = "cyan",
                    IsEnable = true,
                },
                Economy = new EconomyAppearance()
                {
                    Currency = "$",
                    Plugin = "ServerRewards",
                    Color = "#10ff10",
                    IsEnable = false,
                },
                InfoMessages = new InfoMessage
                {
                    UpdateInterval = 60,
                    Align = "BottomCenter",
                    Width = 260,
                    OffsetTB = 0,
                    OffsetR = 0,
                    OutlineColor = "#000",
                    IsOverall = true,
                    IsEnable = true,
                    Messages = new List<string>
                    {
                        "Welcome to Your Server Name",
                        "Good luck"
                    }
                },
                AdditionalMenu = new AdditionalMenu
                {
                    AutoCloseTime = 60,
                    AutoCloseCommand = true,
                    ButtonColor = "yellow",
                    CommandsOpacity = 100,
                    IsEnable = true,
                    Commands = new List<CommandAppearance>
                    {
                        new CommandAppearance
                        {
                            Command = "chat.say Hello there",
                            Icon = "https://i.ibb.co/7vG49W1/active.png",
                            Image = "https://i.ibb.co/6Z5SCrW/command.png",
                            Text = "Say Something",
                            OutlineColor = "#000",
                            IsConsole = true,
                        },
                        new CommandAppearance
                        {
                            Command = "/shop",
                            Icon = "",
                            Image = "https://i.ibb.co/6Z5SCrW/command.png",
                            Text = "Say Something",
                            OutlineColor = "#000",
                            IsConsole = false,
                        },
                    },
                }
            };

            [JsonProperty("Base Events", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<BaseEventsAppearance> BaseEvents = new List<BaseEventsAppearance>
            {
                new BaseEventsAppearance
                {
                    Name = "Bradley",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/X8xVTjc/bradley.png",
                    IsEnable = true,
                },
                new BaseEventsAppearance
                {
                    Name = "PatrolHeli",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/m6wwkhH/heli.png",
                    IsEnable = true,
                },
                new BaseEventsAppearance
                {
                    Name = "CH47",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/CVCPVpN/ch47.png",
                    IsEnable = true,
                },
                new BaseEventsAppearance
                {
                    Name = "Cargo",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/QmT6MFX/cargo.png",
                    IsEnable = true,
                },
                new BaseEventsAppearance
                {
                    Name = "AirDrop",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/580XRGw/airdrop.png",
                    IsEnable = true,
                },   
                new BaseEventsAppearance
                {
                    Name = "HarborCargoArrive",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/qysRW6X/harborevent2.png",
                    IsEnable = true,
                }, 
                new BaseEventsAppearance
                {
                    Name = "SmallOilRig",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/cbDJj7r/harborevent3.png",
                    IsEnable = true,
                },
                new BaseEventsAppearance
                {
                    Name = "LargeOilRig",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/QPGXQHr/harborevent2.png",
                    IsEnable = true,
                }
            };

            [JsonProperty("Custom Events", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CustomEventsAppearance> CustomEvents = new List<CustomEventsAppearance>
            {
                new CustomEventsAppearance
                {
                    Name = "Convoy",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/qRyD0DY/convoy.png",
                    IsEnable = false,
                    OnEventStart = new List<string>
                    {
                        "OnConvoyStart"
                    },
                    OnEventEnd = new List<string>
                    {
                        "OnConvoyStop"
                    }
                },
                new CustomEventsAppearance 
                {
                    Name = "Sputnik",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/9Tkb44Z/sputnik.png",
                    IsEnable = false,
                    OnEventStart = new List<string>
                    {
                        "OnSputnikEventStart"
                    },
                    OnEventEnd = new List<string>
                    {
                        "OnSputnikEventStop"
                    }
                },
                new CustomEventsAppearance
                {
                    Name = "ArmoredTrain",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/3v3JcQ8/train.png",
                    IsEnable = false,
                    OnEventStart = new List<string>
                    {
                        "OnArmoredTrainEventStart"
                    },
                    OnEventEnd = new List<string>
                    {
                        "OnArmoredTrainEventStop"
                    }
                },
                new CustomEventsAppearance
                {
                    Name = "Harbor",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/P91x3zR/harbor.png",
                    IsEnable = false,
                    OnEventStart = new List<string>
                    {
                        "OnHarborEventStart"
                    },
                    OnEventEnd = new List<string>
                    {
                        "OnHarborEventEnd"
                    }
                },
            };

            public BaseEventsAppearance GetEventByName(string name)
            {
                foreach (var check in BaseEvents)
                    if (string.Equals(check.Name, name, StringComparison.CurrentCultureIgnoreCase))
                        return check;

                foreach (var check in CustomEvents)
                    if (string.Equals(check.Name, name, StringComparison.CurrentCultureIgnoreCase))
                        return check;

                return null;
            }
        }
        
        private class OtherImages
        {
            [JsonProperty(PropertyName = "Backgound Main")]
            public string Background = "https://i.ibb.co/Zf9GxqZ/background.png";
            
            [JsonProperty(PropertyName = "Backgound Events")]
            public string Events = "https://i.ibb.co/1fk9m74/events.png";
            
            [JsonProperty(PropertyName = "Lip")]
            public string Lip = "https://i.ibb.co/10Gg3j5/lip.png";
            
            [JsonProperty(PropertyName = "Drop menu")]
            public string DropMenu = "https://i.ibb.co/vqPQ3nv/dropmenu.png";
            
            [JsonProperty(PropertyName = "Close drop menu")]
            public string CloseDropMenu = "https://i.ibb.co/PwyWzwH/closedropmenu.png";
        }

        private class Data
        {
            [JsonProperty("Players' state hud", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, States> PlayersState = new Dictionary<string, States>();

            [JsonProperty(PropertyName = "First Run")]
            public bool FirstRun = true;
        }

        private class Postition
        {
            [JsonProperty("Align [TopLeft | TopRight | BottomLeft | BottomRight")]
            public string Align;

            [JsonProperty("Left | Right - offset")]
            public int LeftOffSet;

            [JsonProperty("Top | Bottom - offset")]
            public int TopOffSet;
        }

        private class MainSetup
        {
            [JsonProperty("Overall layer [you will see the hud in your inventory]")]
            public bool IsOverall;  
            
            [JsonProperty("Show only active events")]
            public bool OnlyActive;
            
            [JsonProperty("Size ALL [0% - inf]")]
            public int Size;

            [JsonProperty("Logo [HUD interact button]")]
            public string Logo;

            [JsonProperty("Background opacity [0% - 100%]")]
            public int EventsBGOpacity;

            [JsonProperty("Events background opacity [0% - 100%]")]
            public int BGOpacity;

            [JsonProperty("Position")]
            public Postition Position;

            [JsonProperty("Server name")]
            public string ServerName;

            [JsonProperty("Active players")]
            public ActiveAppearance ActivePlayersAppearance;

            [JsonProperty("Sleep players")]
            public BaseAppearance SleepPlayersAppearance;

            [JsonProperty("Queue players")]
            public BaseAppearance QueuePlayersAppearance;

            [JsonProperty("Time")]
            public TimeAppearance Time = new TimeAppearance();

            [JsonProperty("Player position [hide permisson - hud.streamer]")]
            public PlayerPositionAppearance PlayerPosition = new PlayerPositionAppearance();

            [JsonProperty("Economy plugin [Economics | ServerRewards]")]
            public EconomyAppearance Economy = new EconomyAppearance();
            
            [JsonProperty("Info messages")]
            public InfoMessage InfoMessages = new InfoMessage();

            [JsonProperty("Additional menu")]
            public AdditionalMenu AdditionalMenu = new AdditionalMenu();
        }

        private class EconomyAppearance
        {
            [JsonProperty("Currency")]
            public string Currency;

            [JsonProperty("Value color")]
            public string Color;

            [JsonProperty("Enable")]
            public bool IsEnable;

            [JsonProperty("Which plugin use [ServerRewards | Economics]")]
            public string Plugin;
        }

        private class TimeAppearance
        {
            [JsonProperty("Use automatic detection of the time format for each player")]
            public bool IsAutomaticDetection;
            
            [JsonProperty("Use AM/PM format [If you use this turn off automatic detection]")]
            public bool IsAmPm;     

            [JsonProperty("Value color")]
            public string Color;

            [JsonProperty("Enable")]
            public bool IsEnable;
        }

        private class BaseAppearance
        {
            [JsonProperty("Icon")]
            public string Icon;

            [JsonProperty("Color")]
            public string Color;

            [JsonProperty("Enable")]
            public bool IsEnable;
        } 
        
        private class ActiveAppearance : BaseAppearance
        {
            [JsonProperty("Count admins")]
            public bool IsAdminsCount;
        }

        private class BaseEventsAppearance : BaseAppearance
        {
            [JsonIgnore] public bool isActive;

            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("Active color")]
            public string ActiveColor;
        }

        private class CustomEventsAppearance : BaseEventsAppearance
        {
            [JsonProperty("Hook OnEventStart", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> OnEventStart;

            [JsonProperty("Hook OnEventStop", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> OnEventEnd;
        }

        private class AdditionalMenu
        {
            [JsonProperty("Auto close timer [seconds | 0 - disable]")]
            public int AutoCloseTime;

            [JsonProperty("Auto close after command use")]
            public bool AutoCloseCommand;

            [JsonProperty("Open/Close button color")]
            public string ButtonColor;

            [JsonProperty("Commands background opacity [0% - 100%]")]
            public int CommandsOpacity;

            [JsonProperty("Enable")]
            public bool IsEnable;

            [JsonProperty("Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CommandAppearance> Commands = new List<CommandAppearance>();
        }

        private class CommandAppearance
        {
            [JsonProperty("Background image")]
            public string Image;

            [JsonProperty("Icon [optional]")]
            public string Icon;

            [JsonProperty("Command")]
            public string Command;

            [JsonProperty("Permission to see")]
            public string PermissionToSee;

            [JsonProperty("Text")]
            public string Text;

            [JsonProperty("Outline color")]
            public string OutlineColor;

            [JsonProperty("Is Console")]
            public bool IsConsole;
        }

        private class InfoMessage
        {
            [JsonProperty("Update interval [in seconds]")]
            public int UpdateInterval;
            
            [JsonProperty("Align [BottomCenter | TopCenter | TopRight]")]
            public string Align;
            
            [JsonProperty("Width [in px]")]
            public int Width;    
            
            [JsonProperty("Offset [top | bottom]")]
            public int OffsetTB;
            
            [JsonProperty("Offset [Center]")]
            public int OffsetR;

            [JsonProperty("Use Outline")]
            public bool UseOutline = true;
            
            [JsonProperty("Outline color")]
            public string OutlineColor;
            
            [JsonProperty("Overall [you will see messages in your invenotory]")]
            public bool IsOverall;
            
            [JsonProperty("Enable")]
            public bool IsEnable;
            
            [JsonProperty("Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Messages = new List<string>();
        }

        private class PlayerPositionAppearance
        {
            [JsonProperty("Enable")]
            public bool IsEnable;
            
            [JsonProperty("true - grid | false - x,z coordinates")]
            public bool IsGrid;

            [JsonProperty("Color")]
            public string Color;
            
        }
        
        #endregion

        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            if (_config.AutoReload)
                ServerMgr.Instance.StartCoroutine(ConfigCheck());

            if (_config.GetEventByName("HarborCargoArrive") == null)
            {
                _config.BaseEvents.Add(new BaseEventsAppearance
                {
                    Name = "HarborCargoArrive",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/qysRW6X/harborevent2.png",
                    IsEnable = true,
                }); 
            }
            
            if (_config.GetEventByName("SmallOilRig") == null)
            {
                _config.BaseEvents.Add(new BaseEventsAppearance
                {
                    Name = "SmallOilRig",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/cbDJj7r/harborevent3.png",
                    IsEnable = true,
                });
            }
            
            if (_config.GetEventByName("LargeOilRig") == null)
            {
                _config.BaseEvents.Add(new BaseEventsAppearance
                {
                    Name = "LargeOilRig",
                    Color = "#fff",
                    ActiveColor = "#10ff10",
                    Icon = "https://i.ibb.co/QPGXQHr/harborevent2.png",
                    IsEnable = true,
                });
            }

            LoadImages();
            LoadData();

            foreach (var check in _config.MainSetup.AdditionalMenu.Commands)
            {
                if (string.IsNullOrEmpty(check.PermissionToSee))
                    continue;
                
                permission.RegisterPermission(check.PermissionToSee, this);
            }

            FillCustomHooks();

            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.name == "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_1.prefab")
                    largeOilRigPosition = monument.transform.position;
                else if (monument.name == "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_2.prefab")
                    smallOilRigPosition = monument.transform.position;
            }

            EventsInit();

            if (_data.FirstRun)
                timer.In(5, () =>
                {
                    _data.FirstRun = false;
                    Interface.Oxide.ReloadPlugin("ImageLibrary");
                    Interface.Oxide.ReloadPlugin("Hud");
                });
                  
            permission.RegisterPermission(PERM_STREAMER, this);

            UI.Size = _config.MainSetup.Size / 100f; 

            if (!_config.MainSetup.QueuePlayersAppearance.IsEnable)
                Unsubscribe(nameof(OnConnectionQueue));
            
            if (!_config.MainSetup.SleepPlayersAppearance.IsEnable)
            {
                Unsubscribe(nameof(OnPlayerSleep));
                Unsubscribe(nameof(OnPlayerSleepEnded));
            }
  
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
            
            if (_config.MainSetup.InfoMessages.IsEnable)
                InfoMessageUpdate = timer.Every(_config.MainSetup.InfoMessages.UpdateInterval, () =>
                {
                    foreach (var check in BasePlayer.activePlayerList)
                        ShowUIMessagesInfo(check);
                });
 
            ServerMgr.Instance.StartCoroutine(InfoUpdate());

            timer.Once(60f, () => 
            {
                var isUpdateNeeded = false;
                foreach (var item in AddedImages) 
                    if (!uint.TryParse(GetImage(item), out var resultCrc) || (FileStorage.server.Get(resultCrc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID) == null))
                    {
                        AddImage(item);
                        isUpdateNeeded = true;
                    }

                if (isUpdateNeeded)
                    foreach (var player in BasePlayer.activePlayerList)
                        OnPlayerConnected(player);
            });
        }

        private void Unload()
        {
            SaveData();
            
            UI.DestroyToAll(".bg");
            UI.DestroyToAll(".bg.Messages.bg");
            UI.DestroyToAll(".bgSettings");
        }

        private void OnPlayerConnected(BasePlayer player) 
        {
            if (player == null)
                return;

            _data.PlayersState.TryAdd(player.UserIDString, States.Full);

            ShowUIBG(player);
            NextTick(() => ShowUIPlayersCounts());
        }

        private void OnConnectionQueue(Network.Connection connection) => NextTick(() => ShowUIPlayersCounts());
        private void OnPlayerSleep(BasePlayer player) => NextTick(() => ShowUIPlayersCounts());
        
        private void OnPlayerDisconnected(BasePlayer player) => NextTick(() => ShowUIPlayersCounts());
        private void OnPlayerSleepEnded(BasePlayer player) => NextTick(() => ShowUIPlayersCounts());

        #region EVENTS

        private void OnEntitySpawned(BradleyAPC entity) => EventTouch(entity);

        private void OnEntityKill(BradleyAPC entity) => EventTouch(entity);

        private void OnEntitySpawned(PatrolHelicopter entity) => EventTouch(entity);

        private void OnEntityKill(PatrolHelicopter entity) => EventTouch(entity);

        private void OnEntitySpawned(CH47Helicopter entity) => EventTouch(entity);

        private void OnEntityKill(CH47Helicopter entity) => EventTouch(entity);

        private void OnEntitySpawned(CargoShip entity) => EventTouch(entity);

        private void OnEntityKill(CargoShip entity) => EventTouch(entity);

        private void OnEntitySpawned(SupplyDrop entity) => EventTouch(entity);

        private void OnEntityKill(SupplyDrop entity) => EventTouch(entity);

        private void OnCargoShipHarborArrived(CargoShip entity) 
        {
            _config.GetEventByName("HarborCargoArrive").isActive = true;

            NextTick(() =>
            { 
               _config.GetEventByName("Cargo").isActive = BaseNetworkable.serverEntities.OfType<CargoShip>().Any(x => !x.IsShipDocked);
                ShowUIEvents();
            });
        }

        private void OnCargoShipHarborLeave(CargoShip entity)
        {
            _config.GetEventByName("HarborCargoArrive").isActive = false;

            NextTick(() =>
            {
                _config.GetEventByName("Cargo").isActive = BaseNetworkable.serverEntities.OfType<CargoShip>().Any(x => !x.IsShipDocked);
                ShowUIEvents();
            });
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate is null || (!IsSmallOilRig(crate.transform.position) && !IsLargeOilRig(crate.transform.position)))
                return;
            
            _config.GetEventByName(IsSmallOilRig(crate.transform.position) ? "SmallOilRig" : "LargeOilRig").isActive = true;
            ShowUIEvents();
        }

        private void OnCrateHackEnd(HackableLockedCrate crate)
        {
            if (crate is null || (!IsSmallOilRig(crate.transform.position) && !IsLargeOilRig(crate.transform.position)))
                return;

            _config.GetEventByName(IsSmallOilRig(crate.transform.position) ? "SmallOilRig" : "LargeOilRig").isActive = false;
            ShowUIEvents();
        }

        private void OnEntityKill(HackableLockedCrate crate)
        {
            if (crate is null || (!IsSmallOilRig(crate.transform.position) && !IsLargeOilRig(crate.transform.position)))
                return;

            _config.GetEventByName(IsSmallOilRig(crate.transform.position) ? "SmallOilRig" : "LargeOilRig").isActive = false;
            ShowUIEvents();
        }

        private void EventTouch(BaseEntity entity) =>
            NextTick(() =>
            {
                if (entity?.OwnerID != 0)
                    return;

                if (entity is BradleyAPC)
                    _config.GetEventByName("Bradley").isActive = BaseNetworkable.serverEntities.OfType<BradleyAPC>().Any();

                else if (entity is PatrolHelicopter)
                    _config.GetEventByName("PatrolHeli").isActive = BaseNetworkable.serverEntities.OfType<PatrolHelicopter>().Any();

                else if (entity is CH47Helicopter)
                    _config.GetEventByName("CH47").isActive = BaseNetworkable.serverEntities.OfType<CH47Helicopter>().Any();

                else if (entity is CargoShip)
                    _config.GetEventByName("Cargo").isActive = BaseNetworkable.serverEntities.OfType<CargoShip>().Any();

                else if (entity is SupplyDrop)
                    _config.GetEventByName("AirDrop").isActive = BaseNetworkable.serverEntities.OfType<SupplyDrop>().Any();

                else
                    return;

                ShowUIEvents();
            });

        #endregion

        #endregion

        #region Commands

        [ConsoleCommand("UI_H")]
        private void cmdConsoleUI_H(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
                return;

            var player = arg.Player();
            switch (arg.GetString(0))
            { 
                case "CHANGESTATE":
                    var state = _data.PlayersState[player.UserIDString];
                    if (Interface.CallHook("CanHudChangeState", player, state.ToString(), (state == States.Full ? States.Events : state == States.Events ? States.Hide : States.Full).ToString()) != null)
                        return;

                    _data.PlayersState[player.UserIDString] = state == States.Full ? States.Events : state == States.Events ? States.Hide : States.Full;
                    ShowUIMain(player);
                    break;
                case "CHANGEADMENUSTATE":
                    ShowUIAdditionalMenu(player, !arg.GetBool(1));

                    if (!arg.GetBool(1) && _config.MainSetup.AdditionalMenu.AutoCloseTime > 0) 
                        timer.Once(_config.MainSetup.AdditionalMenu.AutoCloseTime, () => ShowUIAdditionalMenu(player));
                    break;
                case "USECOMMAND":
                    player.SendConsoleCommand(arg.GetBool(1) ? $"{string.Join(" ", arg.Args.Skip(2))}" : $"chat.say \"{string.Join(" ", arg.Args.Skip(2))}\""); 
  
                    if (_config.MainSetup.AdditionalMenu.AutoCloseCommand)
                        ShowUIAdditionalMenu(player);
                    break;
                case "s":
                    BaseAppearance ap = null;
                    switch (arg.GetString(1))
                    {
                        case "io":
                            _config.MainSetup.IsOverall = !_config.MainSetup.IsOverall;
                            ShowUISettings(player);
                            break; 
                        case "oa":
                            _config.MainSetup.OnlyActive = !_config.MainSetup.OnlyActive;
                            CalculateEventsWidth();
                            ShowUISettings(player);
                            break;
                        case "size":
                            _config.MainSetup.Size = arg.GetInt(2);
                            UI.Size = _config.MainSetup.Size / 100f;
                            ShowUISettings(player);
                            break;
                        case "logo":
                            _config.MainSetup.Logo = arg.GetString(2);
                            AddImage(_config.MainSetup.Logo);
                            ShowUISettings(player);
                            break;
                        case "eventsbgop":
                            _config.MainSetup.EventsBGOpacity = arg.GetInt(2);
                            ShowUISettings(player);
                            break;
                        case "bgop":
                            _config.MainSetup.BGOpacity = arg.GetInt(2);
                            ShowUISettings(player);
                            break;
                        case "posalign":
                            _config.MainSetup.Position.Align = arg.GetString(2);
                            ShowUISettings(player);
                            break;
                        case "posleft":
                            _config.MainSetup.Position.LeftOffSet = arg.GetInt(2);
                            ShowUISettings(player);
                            break;
                        case "postop":
                            _config.MainSetup.Position.TopOffSet = arg.GetInt(2);
                            ShowUISettings(player);
                            break;
                        case "name":
                            _config.MainSetup.ServerName = string.Join(" ", arg.Args.Skip(2));
                            ShowUISettings(player);
                            break;
                        case "activeopen":
                            OpenActivePlayersConfiguration(player);
                            break;   
                        case "sleepopen":
                            ap = _config.MainSetup.SleepPlayersAppearance;
                            OpenPlayersConfiguration(player, "Sleep players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s senable", "UI_H s sicon ", "UI_H s scolor ");
                            break;
                        case "queueopen":
                            ap = _config.MainSetup.QueuePlayersAppearance;
                            OpenPlayersConfiguration(player, "Queue players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s qenable", "UI_H s qicon ", "UI_H s qcolor ");
                            break;
                        case "aenable":
                            ap = _config.MainSetup.ActivePlayersAppearance;
                            ap.IsEnable = !ap.IsEnable;
                            OpenActivePlayersConfiguration(player);
                            break;  
                        case "aadminscount":
                            var activePlayers = _config.MainSetup.ActivePlayersAppearance;
                            activePlayers.IsAdminsCount = !activePlayers.IsAdminsCount;
                            OpenActivePlayersConfiguration(player);
                            break;
                        case "aicon":
                            _config.MainSetup.ActivePlayersAppearance.Icon = arg.GetString(2);
                            AddImage(ap.Icon);
                            OpenActivePlayersConfiguration(player);
                            break;
                        case "acolor":
                            _config.MainSetup.ActivePlayersAppearance.Color = arg.GetString(2);
                            OpenActivePlayersConfiguration(player);
                            break;
                        case "senable":
                            ap = _config.MainSetup.SleepPlayersAppearance;
                            ap.IsEnable = !ap.IsEnable;
                            OpenPlayersConfiguration(player, "Active players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s senable", "UI_H s sicon ", "UI_H s scolor ");
                            break;
                        case "sicon":
                            ap = _config.MainSetup.SleepPlayersAppearance;
                            ap.Icon = arg.GetString(2);
                            AddImage(ap.Icon);
                            OpenPlayersConfiguration(player, "Active players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s senable", "UI_H s sicon ", "UI_H s scolor ");
                            break;
                        case "scolor":
                            ap = _config.MainSetup.SleepPlayersAppearance;
                            ap.Color = arg.GetString(2);
                            OpenPlayersConfiguration(player, "Active players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s senable", "UI_H s sicon ", "UI_H s scolor ");
                            break;
                        case "qenable":
                            ap = _config.MainSetup.QueuePlayersAppearance;
                            ap.IsEnable = !ap.IsEnable;
                            OpenPlayersConfiguration(player, "Active players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s qenable", "UI_H s qicon ", "UI_H s qcolor ");
                            break;
                        case "qicon":
                            ap = _config.MainSetup.QueuePlayersAppearance;
                            ap.Icon = arg.GetString(2);
                            AddImage(ap.Icon);
                            OpenPlayersConfiguration(player, "Active players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s qenable", "UI_H s qicon ", "UI_H s qcolor ");
                            break;
                        case "qcolor":
                            ap = _config.MainSetup.QueuePlayersAppearance;
                            ap.Color = arg.GetString(2);
                            OpenPlayersConfiguration(player, "Active players", ap.IsEnable, ap.Icon, ap.Color, "UI_H s qenable", "UI_H s qicon ", "UI_H s qcolor ");
                            break;
                        case "timeopen":
                            OpenClockConfiguration(player);
                            break;
                        case "playerposopen":
                            OpenPlayerPositionConfiguration(player);
                            break;
                        case "economyopen":
                            OpenEconomyConfiguration(player);
                            break;
                        case "tautodetect":
                            _config.MainSetup.Time.IsAutomaticDetection = !_config.MainSetup.Time.IsAutomaticDetection;
                            OpenClockConfiguration(player);
                            break;
                        case "tisam":
                            _config.MainSetup.Time.IsAmPm = !_config.MainSetup.Time.IsAmPm;
                            OpenClockConfiguration(player);
                            break;
                        case "tcolor":
                            _config.MainSetup.Time.Color = arg.GetString(2);
                            OpenClockConfiguration(player);
                            break;
                        case "tenable":
                            _config.MainSetup.Time.IsEnable = !_config.MainSetup.Time.IsEnable;
                            OpenClockConfiguration(player);
                            break;
                        case "ppisgrid":
                            _config.MainSetup.PlayerPosition.IsGrid = !_config.MainSetup.PlayerPosition.IsGrid;
                            OpenPlayerPositionConfiguration(player);
                            break;
                        case "ppcolor":
                            _config.MainSetup.PlayerPosition.Color = arg.GetString(2);
                            OpenPlayerPositionConfiguration(player);
                            break;
                        case "ppenabled":
                            _config.MainSetup.PlayerPosition.IsEnable = !_config.MainSetup.PlayerPosition.IsEnable;
                            OpenPlayerPositionConfiguration(player);
                            break;
                        case "ecurrency":
                            _config.MainSetup.Economy.Currency = arg.GetString(2);
                            OpenEconomyConfiguration(player);
                            break;
                        case "eplugin":
                            _config.MainSetup.Economy.Plugin = arg.GetString(2);
                            OpenEconomyConfiguration(player);
                            break;
                        case "ecolor":
                            _config.MainSetup.Economy.Color = arg.GetString(2);
                            OpenEconomyConfiguration(player);
                            break;
                        case "eenable":
                            _config.MainSetup.Economy.IsEnable = !_config.MainSetup.Economy.IsEnable;
                            OpenEconomyConfiguration(player);
                            break;
                        case "openinfo":
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "ienable":
                            _config.MainSetup.InfoMessages.IsEnable = !_config.MainSetup.InfoMessages.IsEnable;

                            if (_config.MainSetup.InfoMessages.IsEnable)
                            {
                                InfoMessageUpdate?.Destroy();
                                InfoMessageUpdate = timer.Every(_config.MainSetup.InfoMessages.UpdateInterval, () =>
                                {
                                    foreach (var check in BasePlayer.activePlayerList)
                                        ShowUIMessagesInfo(check);
                                });
                            }
                            else
                            {
                                InfoMessageUpdate?.Destroy(); 
                                UI.DestroyToAll(UI.Layer + ".Messages.bg");
                            }

                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "ioverall":
                            _config.MainSetup.InfoMessages.IsOverall = !_config.MainSetup.InfoMessages.IsOverall;
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "iuseoutline":
                            _config.MainSetup.InfoMessages.UseOutline = !_config.MainSetup.InfoMessages.UseOutline;
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "iupdateint":
                            _config.MainSetup.InfoMessages.UpdateInterval = arg.GetInt(2);
                            if (_config.MainSetup.InfoMessages.IsEnable)
                            {
                                InfoMessageUpdate?.Destroy();
                                InfoMessageUpdate = timer.Every(_config.MainSetup.InfoMessages.UpdateInterval, () =>
                                {
                                    foreach (var check in BasePlayer.activePlayerList)
                                        ShowUIMessagesInfo(check);
                                });
                            }
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "ialign":
                            _config.MainSetup.InfoMessages.Align = arg.GetString(2);
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "iwidth":
                            _config.MainSetup.InfoMessages.Width = arg.GetInt(2);
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "itboff":
                            _config.MainSetup.InfoMessages.OffsetTB = arg.GetInt(2);
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "iroff":
                            _config.MainSetup.InfoMessages.OffsetR = arg.GetInt(2);
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "ioutlinecolor":
                            _config.MainSetup.InfoMessages.OutlineColor = arg.GetString(2);
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "imremove":
                            _config.MainSetup.InfoMessages.Messages.RemoveAt(arg.GetInt(2));
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "imadd":
                            _config.MainSetup.InfoMessages.Messages.Add(string.Join(" ", arg.Args.Skip(2)));
                            OpenInfoMessagesConfiguration(player);
                            break;
                        case "amenable":
                            _config.MainSetup.AdditionalMenu.IsEnable = !_config.MainSetup.AdditionalMenu.IsEnable;
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "amautoclose":
                            _config.MainSetup.AdditionalMenu.AutoCloseCommand = !_config.MainSetup.AdditionalMenu.AutoCloseCommand;
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "amclosetimer":
                            _config.MainSetup.AdditionalMenu.AutoCloseTime = arg.GetInt(2);
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "amopacity":
                            _config.MainSetup.AdditionalMenu.CommandsOpacity = arg.GetInt(2);
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "amcolor":
                            _config.MainSetup.AdditionalMenu.ButtonColor = arg.GetString(2);
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "amopen":
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "amcreatenew":
                            _config.MainSetup.AdditionalMenu.Commands.Add(new CommandAppearance
                            {
                                Text = "Nex Example",
                                Command = "chat.say Hello there",
                                Icon = "https://www.dropbox.com/scl/fi/weax6ug55o0u4f87bhchs/active.png?rlkey=lbpuhpqs7tutpgv1i4t3b1107&dl=1",
                                Image = "https://www.dropbox.com/scl/fi/ypbnnbs07i2t322x1pu4c/command.png?rlkey=a693i43bn70wiy2p0v0216uw7&dl=1",
                                IsConsole = true,
                                OutlineColor = "#000",
                                PermissionToSee = "",
                            });
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "amedit":
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "ceventhookedit":
                            OpenEventHookList(player, arg.GetInt(2), arg.GetInt(3));
                            break;
                        case "amebtext":
                            _config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].Text = string.Join(" ", arg.Args.Skip(3));
                            OpenAdditionalMenuConfiguration(player);
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "amebcommand":
                            _config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].Command = string.Join(" ", arg.Args.Skip(3));
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "ameboutlinecolor":
                            _config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].OutlineColor = arg.GetString(3);
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "amebicon":
                            _config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].Icon = arg.GetString(3);
                            AddImage(_config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].Icon);
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "amebimage":
                            _config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].Image = arg.GetString(3);
                            AddImage(_config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].Image);
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "amebisconsole":
                            _config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].IsConsole = !_config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].IsConsole;
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "apermtosee":
                            _config.MainSetup.AdditionalMenu.Commands[arg.GetInt(2)].PermissionToSee = arg.GetString(3);
                            OpenAdditionalMenuButtonConfiguration(player, arg.GetInt(2));
                            break;
                        case "ambdelete":
                            _config.MainSetup.AdditionalMenu.Commands.RemoveAt(arg.GetInt(2));
                            OpenAdditionalMenuConfiguration(player);
                            break;
                        case "beopen":
                            OpenBaseEventConfiguration(player, arg.GetInt(2)); 
                            break;
                        case "beenable":
                            _config.BaseEvents[arg.GetInt(2)].IsEnable = !_config.BaseEvents[arg.GetInt(2)].IsEnable;
                            CalculateEventsWidth();
                            OpenBaseEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "beicon":
                            _config.BaseEvents[arg.GetInt(2)].Icon = arg.GetString(3);
                            AddImage(_config.BaseEvents[arg.GetInt(2)].Icon);
                            ShowUISettings(player);
                            OpenBaseEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "beacolor":
                            _config.BaseEvents[arg.GetInt(2)].ActiveColor = arg.GetString(3);
                            OpenBaseEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "becolor":
                            _config.BaseEvents[arg.GetInt(2)].Color = arg.GetString(3);
                            OpenBaseEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "ceopen":
                            OpenCustomEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "ceenable":
                            _config.CustomEvents[arg.GetInt(2)].IsEnable = !_config.CustomEvents[arg.GetInt(2)].IsEnable;
                            CalculateEventsWidth();
                            OpenCustomEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "cename":
                            _config.CustomEvents[arg.GetInt(2)].Name = arg.GetString(3);
                            OpenCustomEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "ceicon":
                            _config.CustomEvents[arg.GetInt(2)].Icon = arg.GetString(3);
                            AddImage(_config.CustomEvents[arg.GetInt(2)].Icon);
                            OpenCustomEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "ceactivecolor":
                            _config.CustomEvents[arg.GetInt(2)].ActiveColor = arg.GetString(3);
                            OpenCustomEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "cecolor":
                            _config.CustomEvents[arg.GetInt(2)].Color = arg.GetString(3);
                            OpenCustomEventConfiguration(player, arg.GetInt(2));
                            break;
                        case "cedelete":
                            _config.CustomEvents.RemoveAt(arg.GetInt(2));
                            CalculateEventsWidth();
                            ShowUISettings(player);
                            break;
                        case "ceventhooksstartrem":
                            _config.CustomEvents[arg.GetInt(2)].OnEventStart.RemoveAt(arg.GetInt(3));
                            OpenEventHookList(player, arg.GetInt(2), 1);
                            break;
                        case "ceventhooksendrem":
                            _config.CustomEvents[arg.GetInt(2)].OnEventEnd.RemoveAt(arg.GetInt(3));
                            OpenEventHookList(player, arg.GetInt(2), 0);
                            break;
                        case "ceventhooksaddstart":
                            _config.CustomEvents[arg.GetInt(2)].OnEventStart.Add(arg.GetString(3));
                            OpenEventHookList(player, arg.GetInt(2), 1);
                            break;
                        case "ceventhooksaddend":
                            _config.CustomEvents[arg.GetInt(2)].OnEventEnd.Add(arg.GetString(3));
                            OpenEventHookList(player, arg.GetInt(2), 0);
                            break;
                        case "ceadd":
                            _config.CustomEvents.Add(new CustomEventsAppearance
                            {
                                Name = "CustomEvent",
                                IsEnable = true,
                                Color = "#fff",
                                Icon = "https://www.dropbox.com/scl/fi/3qzyayoa4dxog2t8gyvzw/harbor.png?rlkey=v7jnpk8tyngwywvhbgrfhk5a4&dl=1",
                                ActiveColor = "#10ff10",
                                OnEventStart = new List<string>
                                {
                                    "OnHarborEventStart"
                                },
                                OnEventEnd = new List<string>
                                {
                                    "OnHarborEventEnd"
                                }
                            });
                            CalculateEventsWidth();
                            ShowUISettings(player);
                            break;
                    }
                    SaveConfig(); 
                    foreach (var check in BasePlayer.activePlayerList)
                        ShowUIBG(check);
                    break;
            } 
        }

        [ChatCommand("h")]
        private void cmdChat(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            var text = "All commands:\n<color=yellow>/h open</color> - open ServerHud UI\n<color=yellow>/h events</color> - open Events only\n<color=yellow>/h hide</color> - hide ServerHud UI\n<color=yellow>/h close</color> - close ServerHud UI";
            if (args.Length != 1)
            {
                SendReply(player, text);
                return;
            }
            
            var playerState = _data.PlayersState[player.UserIDString];
            if (!player.IsAdmin && Interface.CallHook("CanHudChangeState", player, playerState.ToString(), (playerState == States.Full ? States.Events : playerState == States.Events ? States.Hide : States.Full).ToString()) != null)
                return;
             
            switch (args[0])
            {
                case "open":
                    _data.PlayersState[player.UserIDString] = States.Full;
                    break;
                case "events":
                    _data.PlayersState[player.UserIDString] = States.Events;
                    break;
                case "hide":
                    _data.PlayersState[player.UserIDString] = States.Hide;
                    break; 
                case "setup":
                    if (!player.IsAdmin)
                        return;
                    
                    ShowUISettingsBg(player);
                    return;
                case "close":
                    _data.PlayersState[player.UserIDString] = States.Close;
                    
                    UI.Destroy(player,".bg");
                    UI.Destroy(player, ".bg.Messages.bg"); 
                    return; 
                default:
                    SendReply(player, text);
                    break;
            } 

            if (playerState == States.Close)
                ShowUIBG(player);
            
            ShowUIMain(player);
        }

        #endregion

        #region Functions

        private void FillCustomHooks()
        {
            var used = new List<string>();
            var pluginCode = File.ReadAllText(Utility.CleanPath(Interface.Oxide.PluginDirectory + Path.DirectorySeparatorChar + "Hud.cs"));
            var lastIndex = pluginCode.LastIndexOf("/*CUSTOMEVENTAUTOCODE-START*/");
            var length = pluginCode.Length;
            var customarea = pluginCode.Substring(lastIndex, length - lastIndex - (length - pluginCode.LastIndexOf("*/")) + 2);
            var currentCode = "/*CUSTOMEVENTAUTOCODE-START*/";
            
            foreach (var check in _config.CustomEvents)
            {
                if (!check.IsEnable)
                    continue;
                
                foreach (var hook in check.OnEventStart)
                {
                    if (used.Contains(hook))
                        continue;
                    
                    currentCode += $"private void {hook}()=>OnEventTouch(System.Reflection.MethodBase.GetCurrentMethod().Name);";
                    used.Add(hook);
                }
                
                foreach (var hook in check.OnEventEnd)
                {
                    if (used.Contains(hook))
                        continue;
                    
                    currentCode += $"private void {hook}()=>OnEventTouch(System.Reflection.MethodBase.GetCurrentMethod().Name);";
                    used.Add(hook);
                }
            }

            currentCode += "/*CUSTOMEVENTAUTOCODE-END*/";
            
            if (customarea == currentCode)
                return;

            PrintWarning("We noticed a change in CustomEventHooks, we are rewriting the plugin with your changes. The plugin will be automatically reloaded!");
            var newPluginCode = pluginCode.Replace(customarea, currentCode);
            File.WriteAllText(Utility.CleanPath(Interface.Oxide.PluginDirectory + Path.DirectorySeparatorChar + "Hud.cs"), newPluginCode);
        }

        private void OnEventTouch(string name)
        {
            foreach (var check in _config.CustomEvents)
            {
                if (!check.IsEnable || (!check.OnEventStart.Contains(name) && !check.OnEventEnd.Contains(name)))
                    continue;
                
                check.isActive = check.OnEventStart.Contains(name);
                
                ShowUIEvents();
                break;
            }
        }
            
        private string PositionToGridCoord(Vector3 position)
        {
            var a = new Vector2(TerrainMeta.NormalizeX(position.x), TerrainMeta.NormalizeZ(position.z));
            var num = TerrainMeta.Size.x / 1024f;
            var num2 = 7;
            var vector = a * num * num2;
            var num3 = Mathf.Floor(vector.x) + 1f;
            var num4 = Mathf.Floor(num * num2 - vector.y);
            var text = string.Empty;
            var num5 = num3 / 26f;
            var num6 = num3 % 26f;
    
            if (num6 == 0f)
                num6 = 26f;
    
            if (num5 > 1f)
                text += Convert.ToChar(64 + (int)num5).ToString();
    
            text += Convert.ToChar(64 + (int)num6).ToString();
    
            return text + num4;
        }
        
        private IEnumerator InfoUpdate()
        {
            while (true)
            {
                if (!IsLoaded)
                    yield break;

                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (_config.MainSetup.Time.IsEnable)
                        ShowUITime(check);

                    if (_config.MainSetup.PlayerPosition.IsEnable)
                        ShowUIPosition(check);

                    if (_config.MainSetup.Economy.IsEnable)
                        ShowUIBalance(check); 
                }
         
                yield return new WaitForSeconds(1f);
            }
        }
 
        private void EventsInit()
        {
            CalculateEventsWidth();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null || entity.IsDestroyed || !(entity is BaseEntity) || (entity as BaseEntity).OwnerID != 0)
                    continue;

                if (entity is BradleyAPC)
                    _config.GetEventByName("Bradley").isActive = BaseNetworkable.serverEntities.OfType<BradleyAPC>().Any();

                else if (entity is PatrolHelicopter)
                    _config.GetEventByName("PatrolHeli").isActive = BaseNetworkable.serverEntities.OfType<PatrolHelicopter>().Any();

                else if (entity is CH47Helicopter)
                    _config.GetEventByName("CH47").isActive = BaseNetworkable.serverEntities.OfType<CH47Helicopter>().Any();

                else if (entity is CargoShip)
                {
                    _config.GetEventByName("Cargo").isActive = BaseNetworkable.serverEntities.OfType<CargoShip>().Any(x => !x.IsShipDocked);
                    _config.GetEventByName("HarborCargoArrive").isActive = BaseNetworkable.serverEntities.OfType<CargoShip>().Any(x => x.IsShipDocked);
                }

                else if (entity is SupplyDrop)
                    _config.GetEventByName("AirDrop").isActive = BaseNetworkable.serverEntities.OfType<SupplyDrop>().Any();
            }

            foreach (var crate in BaseNetworkable.serverEntities.OfType<HackableLockedCrate>())
            {
                if (crate is null || !crate.IsBeingHacked() || (!IsSmallOilRig(crate.transform.position) && !IsLargeOilRig(crate.transform.position)))
                    continue;

                _config.GetEventByName(IsSmallOilRig(crate.transform.position) ? "SmallOilRig" : "LargeOilRig").isActive = true;
            }
        }

        private bool IsSmallOilRig(Vector3 position) => Vector3.Distance(position, smallOilRigPosition) <= 50;
        private bool IsLargeOilRig(Vector3 position) => Vector3.Distance(position, largeOilRigPosition) <= 50;

        private void CalculateEventsWidth()
        {
            EventsPosStartPosX = 2;
            foreach (var check in _config.BaseEvents)
                if (check.IsEnable && (!_config.MainSetup.OnlyActive || _config.MainSetup.OnlyActive && check.isActive))
                    EventsPosStartPosX -= 17;

            foreach (var check in _config.CustomEvents)
                if (check.IsEnable && (!_config.MainSetup.OnlyActive || _config.MainSetup.OnlyActive && check.isActive))
                    EventsPosStartPosX -= 17;

            EnableEventsCount = Math.Abs(EventsPosStartPosX - 2) / 17;

            if (EnableEventsCount > 5)
                EventsPosStartPosX += _config.MainSetup.Position.Align.ToLower().Contains("right") ? -4 : 14;

        }

        #endregion
        
        #region API

        private string API_PlayerHudState(string id) => _data.PlayersState[id].ToString();
        
        #endregion

        #region UI

        private void ShowUISettingsBg(BasePlayer player)
        {
            var container = new CuiElementContainer();
            
            UI.MainParent(ref container, "Settings", "0 0", "1 1", sprite: "assets/content/ui/ui.background.transparent.radial.psd", color: "0.1691 0.1619 0.143 0.741");
            UI.Panel(ref container, ".bgSettings", aMin: "0 0", aMax: "1 1", bgColor: "0.1691 0.1619 0.143 0.747", sprite: "assets/content/ui/ui.background.tile.psd", material: "assets/content/ui/uibackgroundblur-ingamemenu.mat", ignoreSize: true);
            UI.Label(ref container, ".bgSettings", aMin: "0.5 0.5", aMax: "0.5 0.5", oXMin: -190, oXMax: 190, oYMin: -238, oYMax: -208, text: "Click anywhere outside the panel to exit", color: "0.9686 0.9216 0.8824 0.502", fontSize: 12, ignoreSize: true);
            UI.TextButton(ref container, ".bgSettings", aMin: "0 0", aMax: "1 1", close: ".bgSettings", ignoreSize: true);
            
            UI.Create(player, container);
            ShowUISettings(player);
        }

        private void ShowUISettings(BasePlayer player)
        {
            var y = -30;
            var main = _config.MainSetup;
            var container = new CuiElementContainer();

            UI.Panel(ref container, ".bgSettings", ".bgSettings.Main", ".bgSettings.Main", "0.5 0.5", "0.5 0.5", -198, -238, 198, 268, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main", null, null, "0.5 1", "0.5 1", -198, 0, 198, 35, "HUD SETUP", 28, outlineDistance: "0.5 0.5", color: "0.9686 0.9216 0.8824 0.797", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            UI.Label(ref container, ".bgSettings.Main", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, "Main Setup", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

            AddBoolConfiguration(ref container, ".bgSettings.Main", ref y, "Use [Overlay] layer for Hud:", main.IsOverall, "UI_H s io");
            AddBoolConfiguration(ref container, ".bgSettings.Main", ref y, "Show only active events:", main.OnlyActive, "UI_H s oa");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Server Name:", main.ServerName, "UI_H s name ");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Logo URL [HUD interact button]:", main.Logo, "UI_H s logo ");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Hud scale:", main.Size.ToString(), "UI_H s size");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Events background opacity:", main.BGOpacity.ToString(), "UI_H s bgop");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Background opacity:", main.EventsBGOpacity.ToString(), "UI_H s eventsbgop");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Hud align [TopLeft | TopRight | BottomLeft | BottomRight]:", main.Position.Align, "UI_H s posalign");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Left/Right Offset:", main.Position.LeftOffSet.ToString(), "UI_H s posleft");
            AddInputConfiguration(ref container, ".bgSettings.Main", ref y, "Top/Bottom Offset:", main.Position.TopOffSet.ToString(), "UI_H s postop");

            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Active players:", "UI_H s activeopen");
            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Sleep players:", "UI_H s sleepopen");
            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Queue players:", "UI_H s queueopen");
            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Clock settings:", "UI_H s timeopen");
            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Player position:", "UI_H s playerposopen");
            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Economy plugin:", "UI_H s economyopen");
            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Info message:", "UI_H s openinfo");
            AddButtonConfiguration(ref container, ".bgSettings.Main", ref y, "Additional menu:", "UI_H s amopen");
            
            UI.Label(ref container, ".bgSettings.Main", null, null, "0.5 1", "0.5 1", -198, y - 20, 198, y, "Events", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

            y -= 25;
            
            AddBaseEventsConfiguration(ref container, ref y);
            AddCustomEventsConfiguration(ref container, ref y);
            
            UI.Create(player, container);
        }

        private void AddCustomEventsConfiguration(ref CuiElementContainer container, ref int y)
        {
            var x = (4 * (_config.CustomEvents.Count - 1) + 30 * _config.CustomEvents.Count) / -2;

            for (int i = 0; i < _config.CustomEvents.Count; i++)
            {
                UI.Image(ref container, ".bgSettings.Main", aMin: "0.5 1", aMax: "0.5 1", oXMin: x, oYMin: y -30, oXMax: x + 30, oYMax: y, image: GetImage(_config.CustomEvents[i].Icon), color: _config.CustomEvents[i].Color, ignoreSize: true);
                UI.TextButton(ref container, ".bgSettings.Main", null, null, "0.5 1", "0.5 1", x, y - 30, x + 30, y, command: $"UI_H s ceopen {i}", ignoreSize: true);

                x += 34;
            }
            
            if (_config.CustomEvents.Count < 11)
                UI.TextButton(ref container, ".bgSettings.Main", null, null, "0.5 1", "0.5 1", x, y - 30, x + 30, y, text: "+", fontSize: 25, command: "UI_H s ceadd", ignoreSize: true);


            y -= 34;
        }
        
        private void AddBaseEventsConfiguration(ref CuiElementContainer container, ref int y)
        {
            var x = (4 * (_config.BaseEvents.Count - 1) + 30 * _config.BaseEvents.Count) / -2;

            for (int i = 0; i < _config.BaseEvents.Count; i++)
            {
                UI.Image(ref container, ".bgSettings.Main", aMin: "0.5 1", aMax: "0.5 1", oXMin: x, oYMin: y -30, oXMax: x + 30, oYMax: y, image: GetImage( _config.BaseEvents[i].Icon), color: _config.BaseEvents[i].Color, ignoreSize: true);
                UI.TextButton(ref container, ".bgSettings.Main", null, null, "0.5 1", "0.5 1", x, y - 30, x + 30, y, command: $"UI_H s beopen {i}", ignoreSize: true);

                x += 34;
            }

            y -= 34;
        }
        
        private void OpenCustomEventConfiguration(BasePlayer player, int index)
        {
            var y = -30;
            var menu = _config.CustomEvents[index];
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -205, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, $"{menu.Name} settings", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", menu.IsEnable, $"UI_H s ceenable {index}");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Name:", menu.Name, $"UI_H s cename {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Icon:", menu.Icon, $"UI_H s ceicon {index} ");
            
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, "OnEventStart Hooks:", 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
            UI.TextButton(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", 125, y - 15, 173, y, "Edit", 12, command: $"UI_H s ceventhookedit {index} 1", bgColor: "0 0 0 0", color: "0.1471 0.6496 1 1", font: "robotocondensed-regular.ttf", ignoreSize: true);

            y -= 20;
            
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, "OnEventEnd Hooks:", 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
            UI.TextButton(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", 125, y - 15, 173, y, "Edit", 12, command: $"UI_H s ceventhookedit {index} 0", bgColor: "0 0 0 0", color: "0.1471 0.6496 1 1", font: "robotocondensed-regular.ttf", ignoreSize: true);

            y -= 20;

            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Active color:", menu.ActiveColor, $"UI_H s ceactivecolor {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Default color:", menu.Color, $"UI_H s cecolor {index} ");
            
            UI.Image(ref container, ".bgSettings.Main.Additional", aMin: "0.5 1", aMax: "0.5 1", oXMin: -32, oYMin: y -30, oXMax: -2, oYMax: y, image: GetImage(menu.Icon), color: menu.Color, ignoreSize: true);
            UI.Image(ref container, ".bgSettings.Main.Additional", aMin: "0.5 1", aMax: "0.5 1", oXMin: 2, oYMin: y -30, oXMax: 32, oYMax: y, image: GetImage(menu.Icon), color: menu.ActiveColor, ignoreSize: true);
            
            UI.TextButton(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", 118, y - 25, 180, y - 5, "Delete", 12, command: $"UI_H s cedelete {index}", bgColor: "0.1691 0.1619 0.143 0.409", color: "0.9686 0.9216 0.8824 0.522", font: "robotocondensed-bold.ttf", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", aMin: "0.5 0", aMax: "0.5 0", oXMin: -190, oXMax: 190, oYMin: -40, oYMax: -5, text: "After changing OnEventStart/OnEventEnd Hook,\nreload the plugin for the changes to take effect", color: "0.9686 0.9216 0.8824 0.502", fontSize: 12, ignoreSize: true);
            
            UI.Create(player, container);
        }
        
        private void OpenBaseEventConfiguration(BasePlayer player, int index)
        {
            var y = -30;
            var menu = _config.BaseEvents[index];
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -165, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, $"{menu.Name} settings", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", menu.IsEnable, $"UI_H s beenable {index}");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Name:", menu.Name, "", false);
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Icon:", menu.Icon, $"UI_H s beicon {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Active color:", menu.ActiveColor, $"UI_H s beacolor {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Default color:", menu.Color, $"UI_H s becolor {index} ");
            
            UI.Image(ref container, ".bgSettings.Main.Additional", aMin: "0.5 1", aMax: "0.5 1", oXMin: -32, oYMin: y -30, oXMax: -2, oYMax: y, image: GetImage(menu.Icon), color: menu.Color, ignoreSize: true);
            UI.Image(ref container, ".bgSettings.Main.Additional", aMin: "0.5 1", aMax: "0.5 1", oXMin: 2, oYMin: y -30, oXMax: 32, oYMax: y, image: GetImage(menu.Icon), color: menu.ActiveColor, ignoreSize: true);
            
            UI.Create(player, container);
        }
        
        private void OpenEventHookList(BasePlayer player, int index, int start)
        {
            var y = -30;
            var menu = _config.CustomEvents[index];
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main.Additional", ".bgSettings.Main.Additional.Button", ".bgSettings.Main.Additional.Button", "0 1", "0 1", -804, -180, -404, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, $"{menu.Name} ", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

            if (start == 1)
            {
                for (var i = 0; i < menu.OnEventStart.Count; i++)
                {
                    UI.Label(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, menu.OnEventStart[i], 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
                    UI.TextButton(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", 125, y - 15, 173, y, "Remove", 12, command: $"UI_H s ceventhooksstartrem {index} {i}", bgColor: "0 0 0 0", color: "1 0.5279 0.1838 1", font: "robotocondensed-regular.ttf", ignoreSize: true);

                    y -= 20;
                }

                if (menu.OnEventStart.Count < 6)
                {
                    UI.Label(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -198, y - 20, 198, y, "Add hook", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

                    y -= 25;

                    UI.Panel(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -173, y - 15, 173, y, bgColor: "0.1137 0.1255 0.1216 0.403", sprite: "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
                    UI.Input(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -173, y - 15, 173, y, command: $"UI_H s ceventhooksaddstart {index} ", color: "0.9686 0.9216 0.8824 0.502", text: "Enter your new hook here", align: TextAnchor.MiddleLeft, fontSize: 12, font: "robotocondensed-regular.ttf", ignoreSize: true);
                }
            }
            else
            {
                for (var i = 0; i < menu.OnEventEnd.Count; i++)
                {
                    UI.Label(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, menu.OnEventEnd[i], 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
                    UI.TextButton(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", 125, y - 15, 173, y, "Remove", 12, command: $"UI_H s ceventhooksendrem {index} {i}", bgColor: "0 0 0 0", color: "1 0.5279 0.1838 1", font: "robotocondensed-regular.ttf", ignoreSize: true);

                    y -= 20;
                }

                if (menu.OnEventEnd.Count < 6)
                {

                    UI.Label(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -198, y - 20, 198, y, "Add hook", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

                    y -= 25;

                    UI.Panel(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -173, y - 15, 173, y, bgColor: "0.1137 0.1255 0.1216 0.403", sprite: "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
                    UI.Input(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -173, y - 15, 173, y, command: $"UI_H s ceventhooksaddend {index} ", color: "0.9686 0.9216 0.8824 0.502", text: "Enter your new hook here...", align: TextAnchor.MiddleLeft, fontSize: 12, font: "robotocondensed-regular.ttf", ignoreSize: true);
                }
            }

            UI.Create(player, container);
        }
        
        private void OpenAdditionalMenuButtonConfiguration(BasePlayer player, int index)
        {
            var y = -30;
            var menu = _config.MainSetup.AdditionalMenu.Commands[index];
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main.Additional", ".bgSettings.Main.Additional.Button", ".bgSettings.Main.Additional.Button", "0 1", "0 1", -804, -205, -404, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, $"'{menu.Text}' settings", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

            AddInputConfiguration(ref container, ".bgSettings.Main.Additional.Button", ref y, "Text:", menu.Text, $"UI_H s amebtext {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional.Button", ref y, "Command:", menu.Command, $"UI_H s amebcommand {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional.Button", ref y, "Outline color:", menu.OutlineColor, $"UI_H s ameboutlinecolor {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional.Button", ref y, "Icon [optional]:", menu.Icon, $"UI_H s amebicon {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional.Button", ref y, "Background image URL:", menu.Image, $"UI_H s amebimage {index} ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional.Button", ref y, "Permission to see:", menu.PermissionToSee, $"UI_H s apermtosee {index} ");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional.Button", ref y, "Is console command:", menu.IsConsole, $"UI_H s amebisconsole {index}");

            UI.TextButton(ref container, ".bgSettings.Main.Additional.Button", null, null, "0.5 1", "0.5 1", 118, y - 25, 180, y - 5, "Delete", 12, command: $"UI_H s ambdelete {index}", bgColor: "0.1691 0.1619 0.143 0.409", color: "0.9686 0.9216 0.8824 0.522", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            UI.Create(player, container);
        }
        
        private void OpenAdditionalMenuConfiguration(BasePlayer player)
        {
            var y = -30;
            var menu = _config.MainSetup.AdditionalMenu;
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -476, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, "Additional menu", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", menu.IsEnable, "UI_H s amenable");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Auto close after command use:", menu.AutoCloseCommand, "UI_H s amautoclose");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Auto close timer [seconds] [0 - disable]:", menu.AutoCloseTime.ToString(), "UI_H s amclosetimer ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Command background opacity:", menu.CommandsOpacity.ToString(), "UI_H s amopacity ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Open/Close button color:", menu.ButtonColor, "UI_H s amcolor ");
            
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, y - 20, 198, y, "Commands list", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

            y -= 25;

            for (int i = 0; i < menu.Commands.Count; i++)
            {
                if (i > 15)
                    break;

                UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, menu.Commands[i].Text, 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
                UI.TextButton(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", 125, y - 15, 173, y, "Edit", 12, command: $"UI_H s amedit {i}", bgColor: "0 0 0 0", color: "0.1471 0.6496 1 1", font: "robotocondensed-regular.ttf", ignoreSize: true);
                
                y -= 20;
            }
            
            if (menu.Commands.Count < 16)
                UI.TextButton(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -45, y - 15, 45, y, "New", 12, command: "UI_H s amcreatenew", bgColor: "0 0 0 0", color: "0.711 0.8676 0.4338 1", font: "robotocondensed-regular.ttf", ignoreSize: true);

            UI.Create(player, container);
        }

        private void OpenInfoMessagesConfiguration(BasePlayer player)
        {
            var y = -30;
            var info = _config.MainSetup.InfoMessages;
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -476, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, "Info messages", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", info.IsEnable, "UI_H s ienable");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Use [Overlay] layer for messages:", info.IsOverall, "UI_H s ioverall");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Use Outline:", info.UseOutline, "UI_H s iuseoutline");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Align [BottomCenter | TopCenter | TopRight]:", info.Align, "UI_H s ialign ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Update interval [in seconds]:", info.UpdateInterval.ToString(), "UI_H s iupdateint ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Width [px]:", info.Width.ToString(), "UI_H s iwidth ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Top/Bottom Offset:", info.OffsetTB.ToString(), "UI_H s itboff ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Center Offset:", info.OffsetR.ToString(), "UI_H s iroff ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Outline color:", info.OutlineColor, "UI_H s ioutlinecolor ");

            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, y - 20, 198, y, "Messages list", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

            y -= 25;

            for (int i = 0; i < info.Messages.Count; i++)
            {
                if (i > 10)
                    break;
                
                UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, info.Messages[i], 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
                UI.TextButton(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", 125, y - 15, 173, y, "Remove", 12, command: $"UI_H s imremove {i}", bgColor: "0 0 0 0", color: "1 0.5279 0.1838 1", font: "robotocondensed-regular.ttf", ignoreSize: true);
                y -= 20;
            }
            
            if (info.Messages.Count < 11)
            {
                UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, y - 20, 198, y, "Add message", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

                y -= 25;
                
                UI.Panel(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -173, y - 15, 173, y, bgColor: "0.1137 0.1255 0.1216 0.403", sprite: "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
                UI.Input(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -173, y - 15, 173, y, command: $"UI_H s imadd", color: "0.9686 0.9216 0.8824 0.502", text: "Enter your new message here...", align: TextAnchor.MiddleLeft, fontSize: 12, font: "robotocondensed-regular.ttf", ignoreSize: true);
            }
            
            UI.Create(player, container);
        }
        
        private void OpenEconomyConfiguration(BasePlayer player)
        {
            var y = -30;
            var economy = _config.MainSetup.Economy;
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -110, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, "Economy plugin", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", economy.IsEnable, "UI_H s eenable");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Economy plugin name[ServerRewards | Economics]:", economy.Plugin, "UI_H s eplugin");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Currency symbol:", economy.Currency, "UI_H s ecurrency");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Color:", economy.Color, "UI_H s ecolor");

            UI.Create(player, container);
        }
        
        private void OpenPlayerPositionConfiguration(BasePlayer player)
        {
            var y = -30;
            var pos = _config.MainSetup.PlayerPosition;
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -90, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, "Player position", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", pos.IsEnable, "UI_H s ppenabled");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Use Grid(True - Grid | False - X|Y coordinates):", pos.IsGrid, "UI_H s ppisgrid");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Color:", pos.Color, "UI_H s ppcolor");

            UI.Create(player, container);
        }
        
        private void OpenClockConfiguration(BasePlayer player)
        {
            var y = -30;
            var time = _config.MainSetup.Time;
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -110, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, "Clock Settings", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", time.IsEnable, "UI_H s tenable");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Use automatic detection of the time format for each player:", time.IsAutomaticDetection, "UI_H s tautodetect ");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Use AM/PM format [If you use this turn off automatic detection]:", time.IsAmPm, "UI_H s tisam ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Color:", time.Color, "UI_H s tcolor ");

            UI.Create(player, container);
        }
        
        private void OpenActivePlayersConfiguration(BasePlayer player)
        {
            var y = -30;
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -115, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, "Active players", 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);

            var ap = _config.MainSetup.ActivePlayersAppearance;
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", ap.IsEnable, "UI_H s aenable");
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Count admins:", !ap.IsAdminsCount, "UI_H s aadminscount");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Icon URL:", ap.Icon, "UI_H s aicon ");
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Color:", ap.Color, "UI_H s acolor ");
            
            UI.Create(player, container);
        }
        
        private void OpenPlayersConfiguration(BasePlayer player, string header, bool status, string url, string color, string command1, string command2, string command3)
        {
            var y = -30;
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bgSettings.Main", ".bgSettings.Main.Additional", ".bgSettings.Main.Additional", "1 1", "1 1", 4, -90, 400, 0, "0.9686 0.9216 0.8824 0.0392", "assets/icons/greyout.mat", "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Label(ref container, ".bgSettings.Main.Additional", null, null, "0.5 1", "0.5 1", -198, -25, 198, -5, header, 16, color: "0.9686 0.9216 0.8824 0.547", font: "robotocondensed-bold.ttf", ignoreSize: true);
            
            AddBoolConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Enabled:", status, command1);
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Icon URL:", url, command2);
            AddInputConfiguration(ref container, ".bgSettings.Main.Additional", ref y, "Color:", color, command3);
            
            UI.Create(player, container);
        }
        
        private void AddButtonConfiguration(ref CuiElementContainer container,string layer, ref int y, string text, string command)
        {
            UI.Label(ref container, layer, null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, text, 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
            UI.TextButton(ref container, layer, null, null, "0.5 1", "0.5 1", 125, y - 15, 173, y, "Configure", 12, command: command, bgColor: "0 0 0 0", color: "0.1471 0.6496 1 1", font: "robotocondensed-regular.ttf", ignoreSize: true);

            y -= 20;
        }

        private void AddInputConfiguration(ref CuiElementContainer container, string layer, ref int y, string text, string inputText, string command, bool editable = true)
        {
            UI.Label(ref container, layer, null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, text, 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
            UI.Panel(ref container, layer, null, null, "0.5 1", "0.5 1", 118, y - 15, 180, y, bgColor: "0.1137 0.1255 0.1216 0.403", sprite: "assets/content/ui/ui.background.tile.psd", ignoreSize: true);
            UI.Input(ref container, layer, null, null, "0.5 1", "0.5 1", 118, y - 15, 180, y, command: command, color: "0.9686 0.9216 0.8824 0.502", text: inputText, fontSize: 12, font: "robotocondensed-regular.ttf", readOnly: !editable, ignoreSize: true);

            y -= 20;
        }
        
        private void AddBoolConfiguration(ref CuiElementContainer container, string layer, ref int y, string text, bool state, string command)
        {
            UI.Label(ref container, layer, null, null, "0.5 1", "0.5 1", -190, y - 15, 198, y, text, 12, color: "0.9686 0.9216 0.8824 0.502", align: TextAnchor.MiddleLeft, font: "robotocondensed-regular.ttf", ignoreSize: true);
            UI.TextButton(ref container, layer, null, null, "0.5 1", "0.5 1", 128, y - 15, 170, y, state ? "True" : "False", 12, command: command, bgColor: "0 0 0 0", color: state ? "0.711 0.8676 0.4338 1" : "1 0.5279 0.1838 1", font: "robotocondensed-regular.ttf", ignoreSize: true);

            y -= 20;
        }

        private void ShowUIEvents(BasePlayer player = null)
        {
            if (player != null && (_data.PlayersState[player.UserIDString] == States.Hide || _data.PlayersState[player.UserIDString] == States.Close))
                return;

            if (_config.MainSetup.OnlyActive)
                CalculateEventsWidth();

            var container = new CuiElementContainer();

            UI.Panel(ref container, ".Events.bg", ".Events", ".Events", "0.5 0.5", "0.5 0.5", bgColor: null);

            var posX = EventsPosStartPosX;
            foreach (var check in _config.BaseEvents)
            {
                if (!check.IsEnable || (_config.MainSetup.OnlyActive && !check.isActive))
                    continue;
                
                UI.Image(ref container, ".Events", aMin: "0 0", aMax: "0 0", oXMin: posX, oYMin: -4, oXMax: posX + 30, oYMax: 26, image: GetImage(check.Icon), color: check.isActive ? check.ActiveColor : check.Color);

                posX += 34; 
            }

            foreach (var check in _config.CustomEvents)
            {
                if (!check.IsEnable || (_config.MainSetup.OnlyActive && !check.isActive))
                    continue;

                UI.Image(ref container, ".Events", aMin: "0 0", aMax: "0 0", oXMin: posX, oYMin: -4, oXMax: posX + 30, oYMax: 26, image: GetImage(check.Icon), color: check.isActive ? check.ActiveColor : check.Color);

                posX += 34;
            }

            if (player == null)
                foreach (var check in BasePlayer.activePlayerList)
                    if (_data.PlayersState[check.UserIDString] != States.Hide && _data.PlayersState[check.UserIDString] != States.Close)
                        UI.Create(check, container);

            UI.Create(player, container);
        }

        private void ShowUIAdditionalMenuCommands(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var up = _config.MainSetup.Position.Align.ToLower().Contains("bottom");
            var posY = up ? 104 : -50;
            foreach (var check in _config.MainSetup.AdditionalMenu.Commands)
            {
                if (permission.PermissionExists(check.PermissionToSee) && !permission.UserHasPermission(player.UserIDString, check.PermissionToSee))
                    continue;

                UI.Panel(ref container, ".AdditionalMenuButton.bg", ".AdditionalMenu.Command.bg" + posY, ".AdditionalMenu.Command.bg" + posY, "0.5 1", "0.5 1", oXMin: -80, oYMin: posY, oXMax: 80, oYMax: posY + 30, bgColor: null);

                if (!string.IsNullOrEmpty(check.Image))
                    UI.Image(ref container, ".AdditionalMenu.Command.bg" + posY, image: GetImage(check.Image), color:$"1 1 1 {_config.MainSetup.AdditionalMenu.CommandsOpacity / 100f}");

                if (!string.IsNullOrEmpty(check.Icon))
                    UI.Image(ref container, ".AdditionalMenu.Command.bg" + posY, aMin: "0 0.5", aMax: "0 0.5", oXMin: 10, oYMin: -10, oXMax: 30, oYMax: 10, image: GetImage(check.Icon));

                UI.Label(ref container, ".AdditionalMenu.Command.bg" + posY, aMin: "0 0", aMax: "1 1", oXMin: string.IsNullOrEmpty(check.Icon) ? 10 : 40, oYMin: 0, oXMax: -10, oYMax: 0, text: check.Text, align: string.IsNullOrEmpty(check.Icon) ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, outlineColor:check.OutlineColor, outlineDistance:"-0.5 -0.5"); 

                UI.ImageButton(ref container, ".AdditionalMenu.Command.bg" + posY, $"UI_H USECOMMAND {check.IsConsole} {check.Command}");

                posY -= up ? -35 : 35;
            }

            UI.Create(player, container);
        }

        private void ShowUIAdditionalMenu(BasePlayer player, bool isOpen = false)
        {
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, _data.PlayersState[player.UserIDString] == States.Full ? ".Main.bg" : ".Events.bg", ".AdditionalMenuButton.bg", ".AdditionalMenuButton.bg", "0.5 0", "0.5 0", bgColor: null); 

            UI.Image(ref container, ".AdditionalMenuButton.bg", ".AdditionalMenuButton", oXMin: -16, oYMin: _data.PlayersState[player.UserIDString] == States.Full ? -17 : 4, oXMax: 16, oYMax: _data.PlayersState[player.UserIDString] == States.Full ? 4 : 25, image: GetImage(_config.Images.Lip), color:$"1 1 1 {_config.MainSetup.BGOpacity / 100f}");

            UI.Image(ref container, ".AdditionalMenuButton", image: GetImage(isOpen ? _config.Images.CloseDropMenu : _config.Images.DropMenu), color: _config.MainSetup.AdditionalMenu.ButtonColor);

            UI.ImageButton(ref container, ".AdditionalMenuButton", $"UI_H CHANGEADMENUSTATE {isOpen}");
 
            UI.Create(player, container);

            if (isOpen)
                ShowUIAdditionalMenuCommands(player);
        }
        
        private void ShowUIBalance(BasePlayer player) 
        {
            if (_data.PlayersState[player.UserIDString] != States.Full)
                return;
            
            var container = new CuiElementContainer();  

            UI.Label(ref container, ".Main.bg", ".Balance", ".Balance", "0 0", "0.3 0", oXMin: 10, oYMin: 7, oXMax: 0, oYMax: 24, text:_config.MainSetup.Economy.Currency + (string.Equals(_config.MainSetup.Economy.Plugin, "ServerRewards", StringComparison.CurrentCultureIgnoreCase) ? ServerRewards?.Call<int>("CheckPoints", player.UserIDString) : Economics == null ? 0 : (int) Economics.Call<double>("Balance", player.UserIDString)), fontSize:14, align:TextAnchor.MiddleLeft, color:_config.MainSetup.Economy.Color);
              
            UI.Create(player, container);  
        } 
        
        private void ShowUIPosition(BasePlayer player)
        {
            if (_data.PlayersState[player.UserIDString] != States.Full)
                return;

            if (permission.UserHasPermission(player.UserIDString, PERM_STREAMER))
            {
                UI.Destroy(player, ".Position");
                return;
            }
            
            var container = new CuiElementContainer();
  
            UI.Label(ref container, ".Main.bg", ".Position", ".Position", "1 1", "1 1", oXMin: -45, oYMin: -75, oXMax: -5, oYMax: -45, text: _config.MainSetup.PlayerPosition.IsGrid ? PositionToGridCoord(player.transform.position) : $"X:{(int) player.transform.position.x}\nZ:{(int) player.transform.position.z}", fontSize:_config.MainSetup.PlayerPosition.IsGrid ? 18 : 10, align:TextAnchor.MiddleLeft, color:_config.MainSetup.PlayerPosition.Color);
            
            UI.Create(player, container);
        }
    
        private void ShowUITime(BasePlayer player) 
        {
            if (_data.PlayersState[player.UserIDString] != States.Full)
                return;
            
            var container = new CuiElementContainer();
 
            UI.Label(ref container, ".Main.bg", ".Time", ".Time", "0 1", "0 1", oXMin: 180, oYMin: -75, oXMax: 250, oYMax: -45, text:_config.MainSetup.Time.IsAutomaticDetection ? TOD_Sky.Instance.Cycle.DateTime.ToString(GetCulture(lang.GetLanguage(player.UserIDString)).DateTimeFormat.ShortTimePattern) : TOD_Sky.Instance.Cycle.DateTime.ToString(_config.MainSetup.Time.IsAmPm ? "hh:mm tt" : "HH:mm"), fontSize:18, color:_config.MainSetup.Time.Color);
            
            UI.Create(player, container);
        }

        private void ShowUIPlayersCounts(BasePlayer player = null)
        {
            if (player != null && _data.PlayersState[player.UserIDString] != States.Full)
                return;
            
            var container = new CuiElementContainer();

            if (_config.MainSetup.ActivePlayersAppearance.IsEnable)
                UI.Label(ref container, ".Main.bg", ".ActivePlayers.count", ".ActivePlayers.count", "0 1", "1 1", oXMin: 44, oYMin: -70, oXMax: 0, oYMax: -50, text: BasePlayer.activePlayerList.Count(x => _config.MainSetup.ActivePlayersAppearance.IsAdminsCount ? !x.IsAdmin : true).ToString(), align:TextAnchor.MiddleLeft);
   
            if (_config.MainSetup.SleepPlayersAppearance.IsEnable)
                UI.Label(ref container, ".Main.bg",".SleepPlayers.count", ".SleepPlayers.count", "0 1", "1 1", oXMin: 99, oYMin: -70, oXMax: 0, oYMax: -50,text: BasePlayer.sleepingPlayerList.Count.ToString(), align:TextAnchor.MiddleLeft);

            if (_config.MainSetup.QueuePlayersAppearance.IsEnable)
                UI.Label(ref container, ".Main.bg",".QueuePlayers.count", ".QueuePlayers.count", "0 1", "1 1", oXMin: 154, oYMin: -70, oXMax: 0, oYMax: -50,text: (ServerMgr.Instance.connectionQueue.Queued + ServerMgr.Instance.connectionQueue.Joining ).ToString(), align:TextAnchor.MiddleLeft);

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                    if (_data.PlayersState[check.UserIDString] == States.Full)
                        UI.Create(check, container);
            }
             
            else
                UI.Create(player, container);
        } 

        private void ShowUIBaseInfo(BasePlayer player)
        {
            var container = new CuiElementContainer();

            UI.Label(ref container, ".Main.bg", aMin:$"{(_config.MainSetup.Economy.IsEnable ? "0.3" : "0")} 0", aMax:"1 0", oXMin: 5, oYMin: 7, oXMax: -5, oYMax: 24, text:_config.MainSetup.ServerName, fontSize:14);

            if (_config.MainSetup.ActivePlayersAppearance.IsEnable)
                UI.Image(ref container, ".Main.bg", aMin:"0 1", aMax:"0 1", oXMin: 20, oYMin: -70, oXMax: 40, oYMax: -50, image:GetImage(_config.MainSetup.ActivePlayersAppearance.Icon), color:_config.MainSetup.ActivePlayersAppearance.Color);

            if (_config.MainSetup.SleepPlayersAppearance.IsEnable)
                UI.Image(ref container, ".Main.bg", aMin:"0 1", aMax:"0 1", oXMin: 75, oYMin: -70, oXMax: 95, oYMax: -50, image:GetImage(_config.MainSetup.SleepPlayersAppearance.Icon), color:_config.MainSetup.SleepPlayersAppearance.Color);

            if (_config.MainSetup.QueuePlayersAppearance.IsEnable)
                UI.Image(ref container, ".Main.bg", aMin:"0 1", aMax:"0 1", oXMin: 130, oYMin: -70, oXMax: 150, oYMax: -50, image:GetImage(_config.MainSetup.QueuePlayersAppearance.Icon), color:_config.MainSetup.QueuePlayersAppearance.Color);

            UI.Create(player, container);

            ShowUIPlayersCounts(player);

            if (_config.MainSetup.Time.IsEnable) 
                ShowUITime(player);
            
            if (_config.MainSetup.PlayerPosition.IsEnable)
                ShowUIPosition(player);

            if (_config.MainSetup.Economy.IsEnable)
              ShowUIBalance(player);
        }

        private void ShowUIMainButton(BasePlayer player)
        {
            var container = new CuiElementContainer();
 
            UI.Image(ref container, ".Offsets.bg", ".Burger",  ".Burger", $"{(_config.MainSetup.Position.Align.ToLower().Contains("right") ? "1" : "0")} 1",$"{(_config.MainSetup.Position.Align.ToLower().Contains("right") ? "1" : "0")} 1", oXMin: -35, oYMin: -65, oXMax: 45, oYMax: 15, image: GetImage(_config.MainSetup.Logo));

            UI.ImageButton(ref container, ".Burger", "UI_H CHANGESTATE"); 

            UI.Create(player, container); 
        }

        private void ShowUIMain(BasePlayer player)
        {
            var container = new CuiElementContainer();

            if (_data.PlayersState[player.UserIDString] == States.Full)
                UI.Image(ref container, ".Offsets.bg", ".Main.bg", null, null, null, oXMin: _config.MainSetup.Position.Align.ToLower().Contains("right") ? -300 : 0, oYMin: -100, oXMax: _config.MainSetup.Position.Align.ToLower().Contains("right") ? 0 : 300, oYMax: 0, image: GetImage(_config.Images.Background), color: $"1 1 1 {_config.MainSetup.EventsBGOpacity / 100f}");

            if (_data.PlayersState[player.UserIDString] != States.Hide || _data.PlayersState[player.UserIDString] == States.Close)
                UI.Image(ref container, ".Offsets.bg", ".Events.bg", null, "0 1", "0 1", oXMin: _config.MainSetup.Position.Align.ToLower().Contains("right") ? -300 - (EnableEventsCount > 7 ? (EnableEventsCount - 7) * 34 : 0) : 0, oYMin: -60, oXMax: _config.MainSetup.Position.Align.ToLower().Contains("right") ? 0 : 300 + (EnableEventsCount > 7 ? (EnableEventsCount - 7) * 34 : 0), oYMax: 0, image: GetImage(_config.Images.Events), color: $"1 1 1 {_config.MainSetup.BGOpacity / 100f}");

            UI.Destroy(player, ".Main.bg"); 
            UI.Destroy(player, ".Events.bg");
            UI.Destroy(player, ".bg.Messages.bg");

            UI.Create(player, container);

            if (_config.MainSetup.AdditionalMenu.IsEnable && _data.PlayersState[player.UserIDString] != States.Hide && _data.PlayersState[player.UserIDString] != States.Close)
                ShowUIAdditionalMenu(player);

            if (_config.MainSetup.InfoMessages.IsEnable)
                ShowUIMessagesInfo(player);
            
            if (_data.PlayersState[player.UserIDString] == States.Full)
                ShowUIBaseInfo(player);

            
            ShowUIEvents(player);

            ShowUIMainButton(player);
        } 

        private void ShowUIMessagesInfo(BasePlayer player)
        {
            if (_data.PlayersState[player.UserIDString] == States.Hide || _data.PlayersState[player.UserIDString] == States.Close)
                return;
            
            var container = new CuiElementContainer();

            var mainSetupInfoMessages = _config.MainSetup.InfoMessages;
            var align = mainSetupInfoMessages.Align.ToLower();    
            
            UI.MainParent(ref container, ".Messages.bg", align.Contains("bottom") ? "0.5 0" : align.Contains("right") ? "1 1" : "0.5 1", align.Contains("bottom") ? "0.5 0" : align.Contains("right") ? "1 1" : "0.5 1", mainSetupInfoMessages.IsOverall, false, false);

            switch (align)
            {
                case "bottomcenter":
                    UI.Label(ref container, ".bg.Messages.bg", aMin:"0 0", aMax:"0 0", oXMin: mainSetupInfoMessages.Width / -2 + mainSetupInfoMessages.OffsetR, oYMin: mainSetupInfoMessages.OffsetTB, oXMax: mainSetupInfoMessages.Width / 2 + mainSetupInfoMessages.OffsetR, oYMax: mainSetupInfoMessages.OffsetTB + 20, text:mainSetupInfoMessages.Messages.GetRandom(), fontSize:14, outlineDistance: mainSetupInfoMessages.UseOutline ? "-0.5 -0.5" : null, outlineColor:mainSetupInfoMessages.OutlineColor, wrapMode:VerticalWrapMode.Overflow);    
                    break;
                case "topcenter":
                    UI.Label(ref container, ".bg.Messages.bg", aMin:"0 0", aMax:"0 0", oXMin: mainSetupInfoMessages.Width / -2 + mainSetupInfoMessages.OffsetR, oYMin: -mainSetupInfoMessages.OffsetTB - 20, oXMax: mainSetupInfoMessages.Width / 2 + mainSetupInfoMessages.OffsetR, oYMax: -mainSetupInfoMessages.OffsetTB, text:mainSetupInfoMessages.Messages.GetRandom(), fontSize:14, outlineDistance: mainSetupInfoMessages.UseOutline ? "-0.5 -0.5" : null, outlineColor:mainSetupInfoMessages.OutlineColor, wrapMode:VerticalWrapMode.Overflow);    
                    break;
                case "topright":
                    UI.Label(ref container, ".bg.Messages.bg", aMin:"0 0", aMax:"0 0", oXMin: -mainSetupInfoMessages.Width - mainSetupInfoMessages.OffsetR, oYMin: -mainSetupInfoMessages.OffsetTB - 20, oXMax: -mainSetupInfoMessages.OffsetR, oYMax: -mainSetupInfoMessages.OffsetTB, text:mainSetupInfoMessages.Messages.GetRandom(), fontSize:14, outlineDistance: mainSetupInfoMessages.UseOutline ? "-0.5 -0.5" : null, outlineColor:mainSetupInfoMessages.OutlineColor, wrapMode:VerticalWrapMode.Overflow);    
                    break;
            }
            
            UI.Create(player, container);
        }

        private void ShowUIBG(BasePlayer player)
        {
            if (_data.PlayersState[player.UserIDString] == States.Close)
                return;
            
            var container = new CuiElementContainer();

            switch (_config.MainSetup.Position.Align.ToLower())
            {
                case "topright":
                    UI.MainParent(ref container, null, "1 1", "1 1", _config.MainSetup.IsOverall, false, false);
                    UI.Panel(ref container, ".bg", ".Offsets.bg", null, "0.5 0","0.5 0", oXMin: -_config.MainSetup.Position.LeftOffSet, oYMin: -_config.MainSetup.Position.TopOffSet, oXMax: -_config.MainSetup.Position.LeftOffSet, oYMax: -_config.MainSetup.Position.TopOffSet, bgColor:null);
                    break;
                case "bottomleft":
                    UI.MainParent(ref container, null, "0 0", "0 0", _config.MainSetup.IsOverall, false, false);
                    UI.Panel(ref container, ".bg", ".Offsets.bg", null, "0.5 0","0.5 0", oXMin: _config.MainSetup.Position.LeftOffSet, oYMin: 120 + _config.MainSetup.Position.TopOffSet, oXMax: _config.MainSetup.Position.LeftOffSet, oYMax: 120 + _config.MainSetup.Position.TopOffSet, bgColor:null);
                    break;
                case "bottomright":
                    UI.MainParent(ref container, null, "1 0", "1 0", _config.MainSetup.IsOverall, false, false);
                    UI.Panel(ref container, ".bg", ".Offsets.bg", null, "0.5 0","0.5 0", oXMin: -_config.MainSetup.Position.LeftOffSet, oYMin: 120 + _config.MainSetup.Position.TopOffSet, oXMax: -_config.MainSetup.Position.LeftOffSet, oYMax: 120 + _config.MainSetup.Position.TopOffSet, bgColor:null);
                    break;
                default: 
                    UI.MainParent(ref container, null, "0 1", "0 1", _config.MainSetup.IsOverall, false, false);
                    UI.Panel(ref container, ".bg", ".Offsets.bg", null, "0.5 0","0.5 0", oXMin: _config.MainSetup.Position.LeftOffSet, oYMin: -_config.MainSetup.Position.TopOffSet, oXMax: _config.MainSetup.Position.LeftOffSet, oYMax: -_config.MainSetup.Position.TopOffSet, bgColor:null);
                    break;
            }
            
            UI.Create(player, container);

            ShowUIMain(player);
        }

        #region Culture

        private Dictionary<string, CultureInfo> CachedCultures = new Dictionary<string, CultureInfo>();
        private CultureInfo GetCulture(string name)
        {
            if (CachedCultures.TryGetValue(name, out var cultureInfo))
                return cultureInfo;

            if (!CultureInfo.GetCultures(CultureTypes.AllCultures).Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                CachedCultures.TryAdd(name, cultureInfo = CultureInfo.GetCultureInfo("en"));
                return cultureInfo;
            }

            CachedCultures.TryAdd(name, cultureInfo = CultureInfo.GetCultureInfo(name));
            return cultureInfo;
        }

        #endregion

        #endregion

        #region Image

        [PluginReference] private Plugin ImageLibrary;

        private HashSet<string> AddedImages = new();
        private void AddImage(string url)
        {
            AddedImages.Add(url);

            ImageLibrary.Call("AddImage", url, url);
        }

        private string GetImage(string url)
        {
            var img = ImageLibrary?.Call<string>("GetImage", url);

            return img ?? url;
        }

        private int ILCheck;
        
        private void EnsureImageLibraryLoaded()
        {
            if (!ImageLibrary)
            {
                if (ILCheck == 5)
                {
                    PrintError("ImageLibrary not found. You can download the plugin here - https://umod.org/plugins/image-library.");
                    return;
                }
                
                ILCheck++;
                timer.In(1, EnsureImageLibraryLoaded);
                return;
            }

            LoadImages();
        }

        private void LoadImages()
        {
            if (!ImageLibrary)
            {
                EnsureImageLibraryLoaded();
                return;
            }
            
            ReplaceOldImages();

            AddImage(_config.Images.Background);
            AddImage(_config.Images.Events);

            AddImage(_config.Images.Lip);
            AddImage(_config.Images.DropMenu);
            AddImage(_config.Images.CloseDropMenu);

            var configMainSetup = _config.MainSetup;
            
            AddImage(configMainSetup.Logo);
            
            if (configMainSetup.ActivePlayersAppearance.IsEnable)
                AddImage(configMainSetup.ActivePlayersAppearance.Icon);
            
            if (configMainSetup.SleepPlayersAppearance.IsEnable)
                AddImage(configMainSetup.SleepPlayersAppearance.Icon);
            
            if (configMainSetup.QueuePlayersAppearance.IsEnable)
             AddImage(configMainSetup.QueuePlayersAppearance.Icon); 

            if (configMainSetup.AdditionalMenu.IsEnable)
                foreach (var check in configMainSetup.AdditionalMenu.Commands)
                {
                    AddImage(check.Image);
                    AddImage(check.Icon);
                }
            
            foreach (var check in _config.BaseEvents)
                if (check.IsEnable)
                    AddImage(check.Icon);

            foreach (var check in _config.CustomEvents)
                if (check.IsEnable)
                    AddImage(check.Icon);
        }

        #endregion

        #region Config

        private Configuration _config;

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

        protected override void SaveConfig()
        {
            if (_config.AutoReload)
                _config.JSON = JsonConvert.SerializeObject(_config);

            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        private IEnumerator ConfigCheck()
        {
            var jError = string.Empty;

            while (true)
            {
                if (!IsLoaded)
                    yield break;

                try
                {
                    var checkConfig = Config.ReadObject<Configuration>();
                    if (checkConfig == null || JsonConvert.SerializeObject(Config.ReadObject<Configuration>()) == _config.JSON)
                        throw new Exception();

                    Interface.Oxide.ReloadPlugin(Name);

                    jError = string.Empty;
                }

                catch
                {
                    if (!string.IsNullOrEmpty(jError) && jError != File.ReadAllText(Path.Combine(Manager.ConfigPath, Name + ".json")))
                    {
                        jError = File.ReadAllText(Path.Combine(Manager.ConfigPath, Name + ".json"));

                        PrintError("Your configuration file contains an error. Using default configuration values.");
                    }
                }

                yield return new WaitForSeconds(2f);
            }
        }
        #endregion

        #region Data

        private Data _data;

        private void LoadData() => _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data") ? Interface.Oxide.DataFileSystem.ReadObject<Data>($"{Name}/data") : new Data();
        private void OnServerSave() => SaveData();

        private void SaveData()
        {
            if (_data != null) Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
        } 

        #endregion

        #region GUI

        private class UI
        {
            public static float Size = 1;
            public const string Layer = "UI_Hud";

            public static void MainParent(ref CuiElementContainer container, string name = null, string aMin = "0.5 0.5", string aMax = "0.5 0.5", bool overAll = true, bool keyboardEnabled = true, bool cursorEnabled = true, string color = "0 0 0 0", string material = null, string sprite = null) =>
                container.Add(new CuiPanel
                {
                    KeyboardEnabled = keyboardEnabled,
                    CursorEnabled = cursorEnabled,
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Image = { Color = color, Material = material, Sprite = sprite }
                }, overAll ? "Overlay" : "Hud", Layer + ".bg" + name, Layer + ".bg" + name);

            public static void Panel(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", int oXMin = 0, int oYMin = 0, int oXMax = 0, int oYMax = 0, string bgColor = "0.33 0.33 0.33 1", string material = null, string sprite = null, int itemID = 0, ulong skinID = 0, bool ignoreSize = false) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = ignoreSize ? $"{oXMin} {oYMin}" : $"{oXMin * Size} {oYMin * Size}", OffsetMax = ignoreSize ? $"{oXMax} {oYMax}" : $"{oXMax * Size} {oYMax * Size}" },
                        new CuiImageComponent { Color = HexToRustFormat(bgColor), Material = material, Sprite = sprite, ItemId = itemID, SkinId = skinID },
                    },
                });

            public static void Icon(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", int oXMin = 0, int oYMin = 0, int oXMax = 0, int oYMax = 0, int itemID = 0, ulong skinID = 0, bool ignoreSize = false) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = ignoreSize ? $"{oXMin} {oYMin}" : $"{oXMin * Size} {oYMin * Size}", OffsetMax = ignoreSize ? $"{oXMax} {oYMax}" : $"{oXMax * Size} {oYMax * Size}" },
                        new CuiImageComponent { ItemId = itemID, SkinId = skinID },
                    },
                });

            public static void Image(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", int oXMin = 0, int oYMin = 0, int oXMax = 0, int oYMax = 0, string image = "", string color = "1 1 1 1", bool ignoreSize = false) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = ignoreSize ? $"{oXMin} {oYMin}" : $"{oXMin * Size} {oYMin * Size}", OffsetMax = ignoreSize ? $"{oXMax} {oYMax}" : $"{oXMax * Size} {oYMax * Size}" },
                        new CuiRawImageComponent { Png = !image.StartsWith("http") && !image.StartsWith("www") ? image : null, Url = image.StartsWith("http") || image.StartsWith("www") ? image : null, Color = HexToRustFormat(color), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    },
                });

            public static void Label(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", int oXMin = 0, int oYMin = 0, int oXMax = 0, int oYMax = 0, string text = null, int fontSize = 16, string color = "1 1 1 1", TextAnchor align = TextAnchor.MiddleCenter, string outlineDistance = null, string outlineColor = "0 0 0 1", VerticalWrapMode wrapMode = VerticalWrapMode.Truncate, string font = "robotocondensed-regular.ttf", bool ignoreSize = false) =>
                container.Add(new CuiElement
                { 
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = ignoreSize ? $"{oXMin} {oYMin}" : $"{oXMin * Size} {oYMin * Size}", OffsetMax = ignoreSize ? $"{oXMax} {oYMax}" : $"{oXMax * Size} {oYMax * Size}" },
                        new CuiTextComponent { Text = text, FontSize = ignoreSize ? fontSize : (int) (fontSize * Size), Color = HexToRustFormat(color), Align = align, Font = font, VerticalOverflow = wrapMode },
                        outlineDistance == null ? new CuiOutlineComponent { Distance = "0 0", Color = "0 0 0 0" } : new CuiOutlineComponent { Distance = outlineDistance, Color = HexToRustFormat(outlineColor) },
                    },
                });

            public static void TextButton(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", int oXMin = 0, int oYMin = 0, int oXMax = 0, int oYMax = 0, string text = null, int fontSize = 16, string color = "1 1 1 1", string command = null, string bgColor = "0 0 0 0", VerticalWrapMode wrapMode = VerticalWrapMode.Truncate, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string material = null, string close = null, bool ignoreSize = false) =>
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = ignoreSize ? $"{oXMin} {oYMin}" : $"{oXMin * Size} {oYMin * Size}", OffsetMax = ignoreSize ? $"{oXMax} {oYMax}" : $"{oXMax * Size} {oYMax * Size}" },
                    Text = { Text = text, FontSize = ignoreSize ? fontSize :  (int) (fontSize * Size), Color = HexToRustFormat(color), Align = align, Font = font, VerticalOverflow = wrapMode },
                    Button = { Command = command, Close = close != null ? Layer + close : command == null ? Layer + name : null, Color = HexToRustFormat(bgColor), Material = material }
                }, Layer + parent, Layer + name, destroy == null ? null : Layer + destroy);
            
            public static void ImageButton(ref CuiElementContainer container, string parent, string command = null, string name = null, string aMin = "0 0", string aMax = "1 1", int oXMin = 0, int oYMin = 0, int oXMax = 0, int oYMax = 0, bool ignoreSize = false) =>
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = ignoreSize ? $"{oXMin} {oYMin}" : $"{oXMin * Size} {oYMin * Size}", OffsetMax = ignoreSize ? $"{oXMax} {oYMax}" : $"{oXMax * Size} {oYMax * Size}" },
                    Button = { Command = command, Close = command == null ? name : null, Color = "0 0 0 0" }
                }, Layer + parent, Layer + name);

            public static void Input(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", int oXMin = 0, int oYMin = 0, int oXMax = 0, int oYMax = 0, string text = null, int limit = 256, int fontSize = 16, string color = "1 1 1 1", string command = null, TextAnchor align = TextAnchor.MiddleCenter, bool autoFocus = false, bool hudMenuInput = false, bool readOnly = false, bool isPassword = false, bool needsKeyboard = false, bool singleLine = true, string font = "robotocondensed-regular.ttf", bool ignoreSize = false) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = ignoreSize ? $"{oXMin} {oYMin}" : $"{oXMin * Size} {oYMin * Size}", OffsetMax = ignoreSize ? $"{oXMax} {oYMax}" : $"{oXMax * Size} {oYMax * Size}" },
                        new CuiInputFieldComponent { Text = text, Command = command, CharsLimit = limit, FontSize = ignoreSize ? fontSize :  (int) (fontSize * Size), Color = HexToRustFormat(color), Align = align, Font = font, Autofocus = autoFocus, IsPassword = isPassword, ReadOnly = readOnly, HudMenuInput = hudMenuInput, NeedsKeyboard = needsKeyboard, LineType = singleLine ? InputField.LineType.SingleLine : InputField.LineType.MultiLineNewline },
                    }
                });
            
            public static string HexToRustFormat(string hex)
            {
                if (string.IsNullOrEmpty(hex))
                    return "0 0 0 0";

                Color color;

                if (hex.Contains(":"))
                    return ColorUtility.TryParseHtmlString(hex.Substring(0, hex.IndexOf(":")), out color) ? $"{color.r:F2} {color.g:F2} {color.b:F2} {hex.Substring(hex.IndexOf(":") + 1, hex.Length - hex.IndexOf(":") - 1)}" : hex;

                return ColorUtility.TryParseHtmlString(hex, out color) ? $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}" : hex;
            }

            public static void Create(BasePlayer player, CuiElementContainer container)
            {
                if (player != null)
                    CuiHelper.AddUi(player, container);
            }

            public static void CreateToAll(CuiElementContainer container, string layer)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    if (player != null)
                        CuiHelper.AddUi(player, container);
            }

            public static void Destroy(BasePlayer player, string layer) => CuiHelper.DestroyUi(player, Layer + layer);

            public static void DestroyToAll(string layer)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    Destroy(player, layer);
            }
        }

        #endregion
        
        /*CUSTOMEVENTAUTOCODE-START*//*CUSTOMEVENTAUTOCODE-END*/
    }
}