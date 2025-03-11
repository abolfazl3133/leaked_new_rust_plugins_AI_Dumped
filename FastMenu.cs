using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("FastMenu", "lilmagg", "1.0.0")]
    public class FastMenu : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;
        private string Layer11 = "UI_DrawInterface12";
        private string Layer = "UI_DrawInterface123";
        private string Layer1 = "UI_DrawInterface1";
        private string Layer2 = "UI_DrawInterface2";
        private string Layer3 = "UI_DrawInterface3";
        private CuiElementContainer currentContainer;

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer11);
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, Layer1);
                CuiHelper.DestroyUi(player, Layer2);
                CuiHelper.DestroyUi(player, Layer3);
            }

        }


        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }


        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            UI_DrawInterface12(player);
            UI_DrawInterface123(player);
            UI_DrawInterface1(player);
            UI_DrawInterface2(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return;
            CuiHelper.DestroyUi(player, Layer11);
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer1);
            CuiHelper.DestroyUi(player, Layer2);
            CuiHelper.DestroyUi(player, Layer3);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
            {
            }
            UI_DrawInterface12(player);
            UI_DrawInterface123(player);
            UI_DrawInterface1(player);
            UI_DrawInterface2(player);

        }

         private void UI_DrawInterface12(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "0 110", OffsetMax = "0 68" },
                Image = { Color = "0 0 0 0", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer11);

            container.Add(new CuiElement
            {
                Parent = Layer11,
                Components = {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = $"chat.say /kit", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "КИТЫ", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer11);

            CuiHelper.DestroyUi(player, Layer11);
            CuiHelper.AddUi(player, container);

        }

        private void UI_DrawInterface123(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "0 90", OffsetMax = "0 48" },
                Image = { Color = "0 0 0 0", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = $"chat.say /skin", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "СКИНЫ", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);

        }

        private void UI_DrawInterface1(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "0 70", OffsetMax = "0 28" },
                Image = { Color = "0 0 0 0", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer1);

            container.Add(new CuiElement
            {
                Parent = Layer1,
                Components = {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = $"chat.say /craft", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "КРАФТ", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer1);


            CuiHelper.DestroyUi(player, Layer1);
            CuiHelper.AddUi(player, container);
        }
        private void UI_DrawInterface2(BasePlayer player)        
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "0 48", OffsetMax = "0 8" },
                Image = { Color = "0 0 0 0", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer2);


            container.Add(new CuiElement
            {
                Parent = Layer2,
                Components = {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = $"chat.say /up", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "АПГРЕЙД", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer2);

            CuiHelper.DestroyUi(player, Layer2);
            CuiHelper.AddUi(player, container);
        }
    }
}
