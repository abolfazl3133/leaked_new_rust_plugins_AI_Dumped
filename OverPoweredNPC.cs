using System;
using System.Collections.Generic;
using System.Linq;
using Carbon.Plugins;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins;

[Info("OverPoweredNPC", "sdapro", "1.0.0")]
internal class OverPoweredNPC : CarbonPlugin
{
    #region Static
 
    private HashSet<BotInfo> Bots = new (); 

    public class BotInfo
    {
        public ScientistNPC Entity;
        public Vector3 SpawnPosition;
    }
    
    [PluginReference]
    private Plugin MonumentFinder;
    #endregion

    #region OxideHooks
    
    private void OnServerInitialized()
    {
        LoadData();

        foreach (var check in _data.ScientistBots)
        {
            var monuments = MonumentFinder.Call<List<Dictionary<string, object>>>("API_FindByShortName", check.Key);
            foreach (var monumentItem in monuments)
            {
                var monument = new MonumentAdapter(monumentItem);
                foreach (var position in check.Value)
                {
                    var bot = (ScientistNPC)GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab", monument.TransformPoint(position.Position));
                    bot.Spawn();
                    bot.enableSaving = false;

                    Bots.Add(new BotInfo{Entity = bot, SpawnPosition = bot.transform.position});
                }
            }
        }
    }

    private void OnEntityDeath(ScientistNPC scientistNpc)
    {
        if (scientistNpc == null)
            return;

        var botInfo = Bots.FirstOrDefault(x => x.Entity == scientistNpc);
        if (botInfo == null)
            return;

        timer.In(900, () =>
        {
            botInfo.Entity = (ScientistNPC)GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab", botInfo.SpawnPosition);
            botInfo.Entity.Spawn();
            botInfo.Entity.enableSaving = false;
        });
    }

    private void Unload()
    {
        foreach (var check in Bots)
            if (check.Entity.IsAlive())
                check.Entity.Kill();
        
        SaveData();
    }

    class MonumentAdapter
    {
        public MonoBehaviour Object => (MonoBehaviour)_monumentInfo["Object"];
        public string PrefabName => (string)_monumentInfo["PrefabName"];
        public string ShortName => (string)_monumentInfo["ShortName"];
        public string Alias => (string)_monumentInfo["Alias"];
        public Vector3 Position => (Vector3)_monumentInfo["Position"];
        public Quaternion Rotation => (Quaternion)_monumentInfo["Rotation"];

        private Dictionary<string, object> _monumentInfo;
  
        public MonumentAdapter(Dictionary<string, object> monumentInfo)
        {
            _monumentInfo = monumentInfo;
        }

        public Vector3 TransformPoint(Vector3 localPosition) =>
            ((Func<Vector3, Vector3>)_monumentInfo["TransformPoint"]).Invoke(localPosition);

        public Vector3 InverseTransformPoint(Vector3 worldPosition) =>
            ((Func<Vector3, Vector3>)_monumentInfo["InverseTransformPoint"]).Invoke(worldPosition);

        public Vector3 ClosestPointOnBounds(Vector3 position) =>
            ((Func<Vector3, Vector3>)_monumentInfo["ClosestPointOnBounds"]).Invoke(position);

        public bool IsInBounds(Vector3 position) =>
            ((Func<Vector3, bool>)_monumentInfo["IsInBounds"]).Invoke(position); 
    }

    MonumentAdapter GetClosestMonument(Vector3 position)
    {
        var dictResult = MonumentFinder?.Call("API_GetClosest", position) as Dictionary<string, object>;
        return dictResult != null ? new MonumentAdapter(dictResult) : null;
    }
    #endregion
    
    #region Classes

    private class Data
    { 
        public Dictionary<string, List<PosBot>> ScientistBots = new();
    }

    private class PosBot
    {
        public Vector3 Position;
    };
    

    #endregion

    #region Stuff

    #region Data

    private Data _data;

    private void LoadData() => _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data") ? Interface.Oxide.DataFileSystem.ReadObject<Data>($"{Name}/data") : new Data();
    private void OnServerSave() => SaveData();

    private void SaveData()
    {
        if (_data != null)
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
    }

    #endregion

    #endregion
}