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

    [Info("FastMenuDota", "lilmagg", "1.0.0")]
    public class FastMenuDota : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;

        private string Layer = "UI_DrawInterface123";
        private string Layer2 = "UI_DrawInterface2";

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, Layer2);
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

            UI_DrawInterface123(player);
            UI_DrawInterface2(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return;

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer2);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
            {

            }
            UI_DrawInterface123(player);
            UI_DrawInterface2(player);

        }

        private void UI_DrawInterface123(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "447 81", OffsetMax = "458 48" },
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
                Button = { Command = $"chat.say /craft", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "КРАФТ", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);

        }

        private void UI_DrawInterface2(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "447 48", OffsetMax = "458 18" },
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
                Button = { Command = $"backpack.open", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "РЮКЗАК", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer2);

            CuiHelper.DestroyUi(player, Layer2);
            CuiHelper.AddUi(player, container);
        }
    }
}
