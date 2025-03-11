using Oxide.Core;
using UnityEngine;
using System.Collections;
using System.Reflection;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("ChaosNPCDownloader", "k1lly0u", "0.1.0")]
    class ChaosNPCDownloader : RustPlugin
    {
        private const string DLL_URI = "https://chaoscode.io/oxide/Oxide.Ext.ChaosNPC.dll";

        private void OnServerInitialized()
        {
            if (Interface.Oxide.GetExtension("ChaosNPC") == null)
                ServerMgr.Instance.StartCoroutine(DownloadAndSave(true));
        }
        
        private IEnumerator DownloadAndSave(bool loadInstantly)
        {
            Debug.Log($"[ChaosNPC] - Downloading latest version of ChaosNPC...");

            UnityWebRequest www = UnityWebRequest.Get(DLL_URI);

            string downloadPath = UnityEngine.Application.platform == RuntimePlatform.WindowsPlayer ? $@"{Interface.Oxide.ExtensionDirectory}\Oxide.Ext.ChaosNPC.dll" :
                $@"{Interface.Oxide.ExtensionDirectory}/Oxide.Ext.ChaosNPC.dll";

            www.downloadHandler = new DownloadHandlerFile(downloadPath);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogError($"[ChaosNPC] - Failed to connect to chaoscode.io : {www.error}");
                www.Dispose();
                yield break;
            }

            yield return new WaitUntil(()=> (www.downloadHandler as DownloadHandlerFile).isDone);

            if (loadInstantly)
            {
                Debug.Log($"[ChaosNPC] - Download completed! Loading extension...");
                Interface.Oxide.LoadExtension("Oxide.Ext.ChaosNPC");
                
                timer.In(1, ()=> Interface.Oxide.LoadAllPlugins());
            }
            else Debug.Log($"[ChaosNPC] - Download completed! Restart your server for file to load");
        }
    }
}
