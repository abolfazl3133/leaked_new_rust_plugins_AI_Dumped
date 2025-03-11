using System;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("TPPanelSystem", "ds:alone_sempai / https://topplugin.ru/", "9.0.0")]
    public class TPPanelSystem : RustPlugin
    {
        #region Fields
        [PluginReference] private Plugin ImageLibrary;
        private Dictionary<ulong, bool> PanelVisibility = new Dictionary<ulong, bool>();
        private List<string> EventNames = new List<string>();
        private readonly string defaultSprite = "assets/content/ui/ui.icon.rust.png";
        private Dictionary<string, string> EventAnMinX = new Dictionary<string, string>();
        private Dictionary<string, string> EventAnMaxX = new Dictionary<string, string>();
        private readonly string Layer = "GRPLayer";
        private readonly string Layer1 = "GRPLayer_Store";
        #endregion

        #region [GUIBUILDER]
        protected CuiElement Panel(string name, string anMin, string anMax, string color, string parent, float fadeout, string png, bool cursor, string offsetmin, string offsetmax)
        {
            var Element = new CuiElement()
            
            {
                Name = name,
                Parent = parent,
                FadeOut = fadeout,
                Components =
                {
                    new CuiRawImageComponent { Png = png, Color = color },
                    new CuiRectTransformComponent { AnchorMin = anMin, AnchorMax = anMax, OffsetMin = offsetmin, OffsetMax = offsetmax }
                }
            };
            if (cursor)
            {
                Element.Components.Add(new CuiNeedsCursorComponent());
            }
            return Element;
        }
        protected CuiElement Text(string name, string parent, string color, string text, TextAnchor pos, int fsize, string anMin, string anMax, string fname = "robotocondensed-bold.ttf")
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiTextComponent() { Color = color, Text = text, Align = pos, Font = fname, FontSize = fsize },
                    new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            return Element;
        }
        protected CuiElement Button(string name, string parent, string sprite, string command, string color, string anMin, string anMax)
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiButtonComponent { Command = command, Color = color, Sprite = sprite },
                    new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            return Element;
        }
        #endregion

        #region Config

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public Settings MainSettings = new Settings();

            [JsonProperty("Сообщения")]
            public MessageSettings SettingsMessages = new MessageSettings();


            public class Settings
            {
                [JsonProperty("Включить показ кнопки магазина?")]
                public bool EnableStore = true;
                [JsonProperty("Фейк онлайн++")]
                public int FakeOnline = 10;
            }

            public class MessageSettings
            {
                [JsonProperty("Время обновления сообщений")]
                public float RefreshTimer = 30f;
                [JsonProperty("Размер текста для автосообщений")]
                public int TextSize = 12;
                [JsonProperty("Список сообщений", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> Messages = new List<string>
                {
                    "<color=lime>Пример сообщения 1</color>",
                    "<color=red>Пример сообщения 2</color>",
                    "<color=blue>Пример сообщения 3</color>"
                };
            }
        }


        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        #endregion

        #region Lang [Локализация]

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"PanelHelpMessage", "Вы должны выбрать один из вариантов:\n/panel <color=green>on</color> - включает показ панели\n/panel <color=red>off</color> - выключает показ панели" },
            {"PanelOff", "Вы <color=red>выключили</color> показ панели" },
            {"PanelOn", "Вы <color=green>включили</color> показ панели" },
        };

        #endregion

        #region Hooks
        void InitializeLang()
        {
            lang.RegisterMessages(Messages, this, "ru");
            Messages = lang.GetMessages("ru", this);
        }
        void Loaded()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
            EventNames.Add("plane"); // 0
            EventNames.Add("heli"); // 1
            EventNames.Add("ch47"); // 2
            EventNames.Add("cargo"); // 3

            EventAnMinX.Add("0.29", "0.39"); // 0
            EventAnMinX.Add("0.365", "0.31"); // 1
            EventAnMinX.Add("0.448", "0.31"); // 2
            EventAnMinX.Add("0.53", "0.32"); // 3

            EventAnMaxX.Add("0.333", "0.61"); // 0
            EventAnMaxX.Add("0.41", "0.69"); // 1
            EventAnMaxX.Add("0.493", "0.69"); // 2
            EventAnMaxX.Add("0.575", "0.68"); // 3

            foreach(BasePlayer p in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(p);
            }
        }
        void OnServerInitialized()
        {
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintWarning("DOWNLOAD 'ImageLibrary'! Plugin kryGrandRustPanel has closed...");
                Interface.Oxide.UnloadPlugin("ImageLibrary");
                return;
            }
            InitializeLang();
            ImageLibrary.Call("AddImage", "https://i.ibb.co/XXPFWGg/S4pX4lR.png", "background0");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/hHGmsD7/B9vjEY1.png", "online");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/tqM5bXs/Logo.png", "logo");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/K2JC8Jp/wkMLkoV.png", "store");

            ImageLibrary.Call("AddImage", "https://i.ibb.co/XDGjr2R/il3NdyE.png", "plane");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/pjvk07P/dc2G5L9.png", "heli");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/zGhTxMj/N99MlcN.png", "ch47");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/YQ1sdCK/WWfMK1S.png", "cargo");

            ImageLibrary.Call("AddImage", "https://i.ibb.co/M7tCv6g/GnW1jFA.png", "plane_called");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/Rbg8gMM/yD5eb3L.png", "heli_called");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/dtrS5XR/Hr6TTXZ.png", "ch47_called");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/BcCCrtt/jA9fIc6.png", "cargo_called");
            InvokeHandler.Instance.InvokeRepeating(DrawMessage, cfg.SettingsMessages.RefreshTimer, cfg.SettingsMessages.RefreshTimer);

            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
        }
        void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(DrawMessage);
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, Layer1);
                CuiHelper.DestroyUi(player, "Message");
            }
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity is CargoPlane || entity is PatrolHelicopter || entity is CargoShip || entity is CH47Helicopter)
            {
                var tag = entity is CargoPlane ? "plane" : entity is PatrolHelicopter ? "heli" : entity is CH47Helicopter ? "ch47" : entity is CargoShip ? "cargo" : "";
                timer.Once(1f, () => { foreach (var players in BasePlayer.activePlayerList) DrawEvents(players, tag); });
            }
            else return;
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            if (!PanelVisibility.ContainsKey(player.userID)) { PanelVisibility.Add(player.userID, true); }

            NextTick(() => 
            {
                DrawMessage();
                DrawMenu(player);
                if (cfg.MainSettings.EnableStore)
                {
                    DrawStoreMenu(player);
                }
                foreach (var players in BasePlayer.activePlayerList)
                {
                    if (PanelVisibility[players.userID] == false) return;
                    refreshPlayers();
                }
            });
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            timer.Once(1f, () => {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    if (PanelVisibility[players.userID] == false) return;
                    refreshPlayers();
                }
            });
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null)  return;
            if (entity is CargoPlane)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "plane");
            if (entity is PatrolHelicopter)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "heli");
            if (entity is CargoShip)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "cargo");
            if (entity is CH47Helicopter)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "ch47");
        }
        #endregion

        #region Custom Bools
        bool HasEntity(string name)
        {
            if (name == "plane") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is CargoPlane) { return true; } }
            }
            if (name == "heli") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is PatrolHelicopter) { return true; } }
            }
            if (name == "ch47") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is CH47Helicopter) { return true; } }
            }
            if (name == "cargo") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is CargoShip) { return true; } }
            }

            return false;
        }

        #endregion

        #region Commands

        [ChatCommand("panel")]
        void PanelCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args.Length < 1)
            {
                player.ChatMessage(Messages["PanelHelpMessage"]);
                return;
            }
            if (args[0] == "off")
            {
                if (!PanelVisibility.ContainsKey(player.userID))
                    PanelVisibility.Add(player.userID, false);
                else if (PanelVisibility.ContainsKey(player.userID) && PanelVisibility[player.userID] == false)
                    PanelVisibility[player.userID] = false;

                player.ChatMessage(Messages["PanelOff"]);
                CuiHelper.DestroyUi(player, Layer);
            }
            else if (args[0] == "on")
            {
                if (!PanelVisibility.ContainsKey(player.userID))
                    PanelVisibility.Add(player.userID, true);
                else if (PanelVisibility.ContainsKey(player.userID) && PanelVisibility[player.userID] == true)
                    PanelVisibility[player.userID] = true;

                player.ChatMessage(Messages["PanelOn"]);
                OnPlayerConnected(player);
            }
            else return;
        }

        #endregion

        #region UI

        void refreshPlayers()
        {
            var online = BasePlayer.activePlayerList.Count + cfg.MainSettings.FakeOnline;
            CuiElementContainer container = new CuiElementContainer();
            container.Add(Text(Layer + ".players", Layer, HexToRustFormat("#FFFFFF"), $"{online}/{ConVar.Server.maxplayers}", TextAnchor.MiddleCenter, 11, "0.65 0", "0.82 1"));
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, Layer + ".players");
                CuiHelper.AddUi(p, container);
            }
        }
        void DrawMessage()
        {
            foreach (var players in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(players, "Message");
                var container = new CuiElementContainer();

                container.Add(Panel("Message", "0.3453124 -0.0009259344", "0.6416667 0.0287037", HexToRustFormat("#FFFFFF00"), "Hud", 0, "", false, "0 0", "0 0"));

                container.Add(Text("Message.Message", "Message", "0 0 0 1", cfg.SettingsMessages.Messages[new System.Random().Next
                (cfg.SettingsMessages.Messages.Count)], TextAnchor.MiddleCenter, cfg.SettingsMessages.TextSize, "0 0", "1 1"));
                CuiHelper.AddUi(players, container);
            }
        }

        void DrawEvents(BasePlayer player, string name)
        {
            string anMinX = "";
            string EventAnMinY = "";
            string anMaxX = "";
            string EventAnMaxY = "";
            if (PanelVisibility[player.userID] == false) return;
            var container = new CuiElementContainer();
            for (int i = 0; i < EventNames.Count; i++)
            {
                if (EventNames[i] == name)
                {
                    anMinX = EventAnMinX.ElementAt(i).Key;
                    EventAnMinY = EventAnMinX.ElementAt(i).Value;
                    anMaxX = EventAnMaxX.ElementAt(i).Key;
                    EventAnMaxY = EventAnMaxX.ElementAt(i).Value;
                }
            }

            CuiHelper.DestroyUi(player, Layer + "." + name);
            container.Add(Panel(Layer + "." + name, $"{anMinX} {EventAnMinY}", $"{anMaxX} {EventAnMaxY}", "", Layer, 0.1f, HasEntity(name) ? (string)ImageLibrary.Call("GetImage", $"{name}_called") : (string)ImageLibrary.Call("GetImage", $"{name}"), false, "", ""));
            CuiHelper.AddUi(player, container);
        }

        private void DrawStoreMenu(BasePlayer player)
        {
            if (!cfg.MainSettings.EnableStore) return;
            CuiHelper.DestroyUi(player, Layer1);
            var container = new CuiElementContainer();

            /*container.Add(Panel(Layer1, "0.001041666 0.9648147", "0.001041666 0.9648147", HexToRustFormat("#FFFFFF00"), "Hud", 0f, "", false, "10 -4", "313 23"));
            container.Add(Panel(Layer1 + ".Store", "0.001849664 0.02469136", "0.08985749 1.012346", "", Layer1, 0f, (string)ImageLibrary.Call("GetImage", "store"), false, "0 0", "0 0"));
            container.Add(Button(Layer1 + ".button", Layer1 + ".Store", defaultSprite, "chat.say /store", "0 0 0 0", "0 0", "1 1"));*/

            CuiHelper.AddUi(player, container);
        }
        private void DrawMenu(BasePlayer player)
        {
            if (PanelVisibility[player.userID] == false) return;
            var online = BasePlayer.activePlayerList.Count().ToString();
            var time = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");

            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.001041666 0.9648147", AnchorMax = "0.001041666 0.9648147", OffsetMin = "10 -13", OffsetMax = "313 15" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", Layer); 

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "background0"), FadeIn = 1f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.08 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "Logo");

            container.Add(new CuiElement
            {
                Parent = "Logo",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "logo"), FadeIn = 1f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.12 0", AnchorMax = $"0.26 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "chat.say /menu" },
                Text = { Text = $"Меню", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.618 0", AnchorMax = "0.7 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "Online");

            container.Add(new CuiElement
            {
                Parent = "Online",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "online"), FadeIn = 1f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 7.5", OffsetMax = "-8 -7.5" }
                }
            });

            
            container.Add(Panel(Layer + ".plane", "0.29 0.39", "0.333 0.61", "", Layer, 0.1f, HasEntity("plane") ? (string)ImageLibrary.Call("GetImage", "plane_called") : (string)ImageLibrary.Call("GetImage", "plane"), false, "0 0", "0 0"));
            container.Add(Panel(Layer + ".heli", "0.365 0.31", "0.41 0.69", "", Layer, 0.1f, HasEntity("heli") ? (string)ImageLibrary.Call("GetImage", "heli_called") : (string)ImageLibrary.Call("GetImage", "heli"), false, "0 0", "0 0"));

            container.Add(Panel(Layer + ".ch47", "0.448 0.31", "0.493 0.69", "", Layer, 0.1f, HasEntity("ch47") ? (string)ImageLibrary.Call("GetImage", "ch47_called") : (string)ImageLibrary.Call("GetImage", "ch47"), false, "0 0", "0 0"));
            container.Add(Panel(Layer + ".cargo", "0.53 0.32", "0.575 0.68", "", Layer, 0.1f, HasEntity("cargo") ? (string)ImageLibrary.Call("GetImage", "cargo_called") : (string)ImageLibrary.Call("GetImage", "cargo"), false, "0 0", "0 0"));

            CuiHelper.AddUi(player, container);
        }

        #endregion
        private void ShowIco(BasePlayer player, float sec, string png)
        {
            PrintWarning("1");
            ImageLibrary.Call("AddImage", png, png);
            CuiHelper.DestroyUi(player, "icc");
            var container = new CuiElementContainer();


            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.818 0", AnchorMax = "0.918 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "icc");

            container.Add(new CuiElement
            {
                Parent = "icc",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", png), FadeIn = 1f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });


            timer.Once(sec, () => {
                CuiHelper.DestroyUi(player, "icc");
            });
            CuiHelper.AddUi(player, container);
        }

        #region Helpers

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion
    }
}