using System.Collections.Generic;
using Carbon.Base;
using Carbon.Components;
using Carbon.Modules;
using Carbon.Plugins;
using UnityEngine;

namespace Oxide.Plugins;

[Info("MiniHud", "ahigao", "1.0.0")]
internal class MiniHud : CarbonPlugin
{
    #region Static

    private const string Layer = "MiniHud_UI";
    private ImageDatabaseModule ImageDatabaseModule;

    #endregion

    #region OxideHooks

    #region BaseHooks

    private void OnServerInitialized()
    {
        ImageDatabaseModule = BaseModule.GetModule<ImageDatabaseModule>();
        ImageDatabaseModule.QueueBatch(false, new List<string> { "https://i.postimg.cc/fLnrM73p/Mini-Copter-512.png" });
        
        foreach (var check in BasePlayer.activePlayerList)
            OnPlayerConnected(check);
    }

    private void Unload()
    {
        foreach (var check in BasePlayer.activePlayerList)
        {
            CuiHandler.Destroy(Layer, check);
            CuiHandler.Destroy(Layer + ".button", check);
        }
    }

    private void OnPlayerConnected(BasePlayer player)
    {
        if (player == null)
            return;
        
        ShowUI(player);
   //     timer.In(1, UpdateUI);
    }

    private void OnPlayerDisconnected(BasePlayer player)
    {
        if (player == null)
            return;
        
  //      timer.In(1, UpdateUI);
    }

    private void OnEntityDeath(BasePlayer player)
    {
        if (player == null || !player.userID.IsSteamId())
            return;
        
        CuiHandler.Destroy(Layer, player);
        CuiHandler.Destroy(Layer + ".button", player);
    }

    private void OnPlayerRespawned(BasePlayer player)
    {
        if (player == null || !player.userID.IsSteamId())
            return;
        
        ShowUI(player);
    }

    #endregion

    #endregion

    #region UI

    private void ShowUI(BasePlayer player)
    {
        using var cui = new CUI(CuiHandler);
        var container = cui.CreateContainer(null);
        cui.CreatePanel(container, "Hud", "0 0 0 0", null, 0, 0, 1, 1, 0, 100, -20, 0, id: Layer, destroyUi: Layer);
        cui.CreateText(container, Layer, "#00cfe4", "  proxyrust.gg", 12, align: TextAnchor.UpperLeft);
      
        cui.CreatePanel(container, "Overlay", "0 0 0 0", null, 0.5f, 0.5f, 0, 0, id: Layer + ".button", destroyUi: Layer + ".button");
        cui.CreateButton(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "#a1a09f", "ИНФО", 10, "assets/icons/greyout.mat", 0, 0, 0, 0, -200, -139, 2, 16, command: "chat.say /menu");
        cui.CreateButton(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "#a1a09f", "КИТЫ", 10, "assets/icons/greyout.mat", 0, 0, 0, 0, -136, -75, 2, 16, command: "chat.say /kits");
        cui.CreateButton(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "#a1a09f", "ТОП", 10, "assets/icons/greyout.mat", 0, 0, 0, 0, -72, -11, 2, 16, command: "chat.say /top");
        cui.CreateButton(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "#a1a09f", "СКИНЫ", 10, "assets/icons/greyout.mat", 0, 0, 0, 0, -8, 53, 2, 16, command: "chat.say /skinbox");
        cui.CreateButton(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "#a1a09f", "БГРЕЙД 4", 10, "assets/icons/greyout.mat", 0, 0, 0, 0, 56, 117, 2, 16, command: "chat.say /bgrade4");
        cui.CreateButton(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "#a1a09f", "РЕМУВ", 10, "assets/icons/greyout.mat", 0, 0, 0, 0, 120, 181, 2, 16, command: "chat.say /remove");
        cui.CreateButton(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "#a1a09f", "ЛИМИТЫ", 10, "assets/icons/greyout.mat", 0, 0, 0, 0, 184, 245, 2, 16, command: "chat.say /limits");
        
        var minicopter = cui.CreatePanel(container, Layer + ".button", "0.9686 0.9216 0.8824 0.0292", "assets/icons/greyout.mat", 0, 0, 0, 0, 184, 245, 18, 78);
        cui.CreateImage(container, minicopter, ImageDatabaseModule.GetImage("https://i.postimg.cc/fLnrM73p/Mini-Copter-512.png"), "1 1 1 1", null, 0.1f, 0.9f, 0.1f, 0.9f);
        cui.CreateButton(container, minicopter, "0 0 0 0", "0 0 0 0", "", 0, command: "chat.say /mymini");
        
        //cui.CreateText(container, Layer, "#00cfe4", $"Игроков онлайн: {BasePlayer.activePlayerList.Count}", 12, yMin: 0, yMax: 0, OyMin: 83, OyMax: 96, align: TextAnchor.MiddleCenter, id: Layer + ".online");

        container.Send(player);
    }

    private void UpdateUI()
    {
        using var cui = new CUI(CuiHandler);
        var update = cui.UpdatePool();
        update.Add(cui.UpdateText(Layer + ".online", "#00cfe4", $"Игроков онлайн: {BasePlayer.activePlayerList.Count}", 12, yMin: 0, yMax: 0, OyMin: 83, OyMax: 96, align: TextAnchor.MiddleCenter));

        foreach (var check in BasePlayer.activePlayerList)
            update.Send(check);
    }

    #endregion
}