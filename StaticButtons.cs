using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StaticButtons","Напиши себя малой","0.1")]
    public class StaticButtons : RustPlugin
    {

        #region Hooks

        void OnPlayerConnected(BasePlayer player)
        {
            StaticeButtonUI(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyLayers(player);
            }
            
        }

        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                StaticeButtonUI(player);
            }
        }

        #endregion

        #region UI

        void DestroyLayers(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CTaxiBtn");
            CuiHelper.DestroyUi(player, "CFreindBtn");
            CuiHelper.DestroyUi(player, "CTaxiBtns");
            CuiHelper.DestroyUi(player, "CAlertBtn");
            CuiHelper.DestroyUi(player, "CAlertBtn2");
            CuiHelper.DestroyUi(player, "CAlertBtn3");
            CuiHelper.DestroyUi(player, "CMenuBtn");
            CuiHelper.DestroyUi(player, "CBackpackBtn");
            
        }

        void StaticeButtonUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiButton
            {
                Button = {Color = "0.9686 0.9216 0.8824 0.2",Command = "chat.say /skin"},
                Text = {Text = "SKIN", Align = TextAnchor.MiddleCenter, FontSize = 9},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "-199 0",OffsetMax = "-140 16"}
            }, "Overlay", "CTaxiBtn");
			
            container.Add(new CuiButton
            {
                Button = {Color = "0.9686 0.9216 0.8824 0.2",Command = "chat.say /report"},
                Text = {Text = "Report", Align = TextAnchor.MiddleCenter, FontSize = 9},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "-135 0",OffsetMax = "-76 16"}
            }, "Overlay", "CTaxiBtns");
			
            container.Add(new CuiButton
            {
                Button = {Color = "0.9686 0.9216 0.8824 0.2",Command = "chat.say /top"},
                Text = {Text = "TOP",Align = TextAnchor.MiddleCenter, FontSize = 9},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "-71 0",OffsetMax = "52 16"}
            }, "Overlay", "CFreindBtn");
			
			
			container.Add(new CuiButton
            {
                Button = {Color = "0.9686 0.9216 0.8824 0.2",Command = "chat.say /chat"},
                Text = {Text = "CHAT",Align = TextAnchor.MiddleCenter, FontSize = 9},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "57 0",OffsetMax = "116 16"}
            }, "Overlay", "CAlertBtn2");
			
			container.Add(new CuiButton
            {
                Button = {Color = "0.9686 0.9216 0.8824 0.2",Command = "chat.say /spawn"},
                Text = {Text = "SPAWN",Align = TextAnchor.MiddleCenter, FontSize = 9},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "121 0",OffsetMax = "180 16"}
            }, "Overlay", "CAlertBtn3");

            CuiHelper.AddUi(player, container);
        }

        

        #endregion
    }
}