using System;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDLC", "sdapro", "1.0.0")]
    class NoDLC : RustPlugin
    {
        private void OnServerInitialized()
        {
            foreach (SteamDLCItem dlcItem in UnityEngine.Resources.FindObjectsOfTypeAll<SteamDLCItem>())
            {
                ModifyDLCItem(dlcItem);
            }
        }

        private void ModifyDLCItem(SteamDLCItem dlcItem)
        {
            dlcItem.bypassLicenseCheck = true; // Обходим проверку DLC
            PrintWarning($"Modified DLCItem: {dlcItem.dlcName} ({dlcItem.dlcAppID})");
        }
    }
}
