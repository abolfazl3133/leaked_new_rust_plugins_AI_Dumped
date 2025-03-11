using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.IO;
using Facepunch.Steamworks;

namespace Oxide.Plugins
{
    [Info("AdminRadio", "Arch_Dem0n, sell Ernieleo", "1.0.0")]
    public class AdminRadio : RustPlugin
    {
        object OnNpcPlayerResume(NPCPlayerApex apex) => voiceNPCs.Contains(apex) ? false : (object)null;
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info) => voiceNPCs.Contains(entity) ? false : (object)null;
        static List<NPCPlayerApex> voiceNPCs = new List<NPCPlayerApex>();

        void OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (speaker == player)
            {
                foreach (var user in BasePlayer.activePlayerList.Where(p => permission.UserHasPermission(p.UserIDString, "adminradio.reader")))
                {
                    SpeakerReader reader = user.GetComponent<SpeakerReader>() ?? user.gameObject.AddComponent<SpeakerReader>();
                    reader.Send(data);
                }
            }
        }
        void Unload()
        {
            foreach (var reader in Resources.FindObjectsOfTypeAll<SpeakerReader>()) reader?.Destroy();
        }
        public static BasePlayer speaker = null;
        void Loaded()
        {
            List<string> comps = new List<string>()
            {
                "SpeakerReader",
            };
            foreach (var edits in BaseNetworkable.serverEntities.Where(ent => ent.GetComponents<Component>().Where(comp => comps.Contains(comp.GetType().Name)).Count() > 0).Select(ent => ent.GetComponents<Component>().Where(comp => comps.Contains(comp.GetType().Name))))
                foreach (var edit in edits)
                    UnityEngine.Object.Destroy(edit);
            permission.RegisterPermission("adminradio.speaker", this);
            permission.RegisterPermission("adminradio.reader", this);
        }

        static void PlayerInvoke(ConsoleSystem.Arg arg, Action<BasePlayer> action)
        {
            if (arg?.Player() != null) action?.Invoke(arg.Player());
        }
        [ConsoleCommand("start.voice")] void ConsoleCommand_Start_Voice(ConsoleSystem.Arg arg) => PlayerInvoke(arg, (player) => ChatCommand_Start_Voice(player, "start.voice", arg.Args));
        [ChatCommand("start.voice")]
        void ChatCommand_Start_Voice(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "adminradio.speaker")) return;
            if (speaker == null)
            {
                speaker = player;
                Player.Message(player, "Radio started!");
            }
            else
            {
                Player.Message(player, "Radio already started!");
            }
        }
        [ConsoleCommand("stop.voice")] void ConsoleCommand_Stop_Voice(ConsoleSystem.Arg arg) => PlayerInvoke(arg, (player) => ChatCommand_Stop_Voice(player, "stop.voice", arg.Args));
        [ChatCommand("stop.voice")]
        void ChatCommand_Stop_Voice(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "adminradio.speaker")) return;
            if (speaker != null)
            {
                speaker = null;
                foreach (var reader in BasePlayer.activePlayerList)
                    reader.GetComponent<SpeakerReader>()?.Destroy();
                Player.Message(player, "Radio stopped!");
            }
            else
            {
                Player.Message(player, "Radio already stopped!");
            }
        }
        static Vector3 lposition = new Vector3(0, -10, 0);
        public class SpeakerReader : MonoBehaviour
        {
            public BasePlayer player => GetComponent<BasePlayer>();
            public NPCPlayerApex apex = null;
            public void Destroy()
            {
                voiceNPCs.Remove(apex);
                apex.Kill();
                Destroy(this);
            }
            public void Start()
            {
                apex = GameManager.server.CreateEntity("Assets/Prefabs/NPC/Scientist/ScientistStationary.prefab".ToLower(), this.transform.TransformPoint(lposition)) as NPCPlayerApex;
                apex.Spawn();
                apex.IsStuck = true;
                NPCHumanContext human = apex.AiContext;
                string name = "200";
                apex.AiContext.AiLocationManager = new Rust.Ai.AiLocationManager() { MainSpawner = null, LocationWhenMainSpawnerIsNull = Rust.Ai.AiLocationSpawner.SquadSpawnerLocation.None };
                (apex.GetActiveItem()?.GetHeldEntity() as HeldEntity)?.SetHeld(false);
                voiceNPCs.Add(apex);
                Started = true;
            }
            public void Update()
            {
                if (apex?.net?.ID == null || player?.Connection == null)
                {
                    Destroy();
                    return;
                }
                apex.transform.position = this.transform.TransformPoint(lposition);
            }
            bool Started = false;
            public void Send(byte[] data)
            {
                try
                {
                    if (!Started) return;
                    if (apex?.net?.ID == null || player?.Connection == null)
                    {
                        Destroy();
                        return;
                    }
                    if (Network.Net.sv.write.Start())
                    {
                        Network.Net.sv.write.PacketID(Network.Message.Type.VoiceData);
                        Network.Net.sv.write.UInt32(apex.net.ID);
                        Network.Net.sv.write.BytesWithSize(data);
                        Network.Net.sv.write.Send(new Network.SendInfo(player.Connection) { priority = Network.Priority.Immediate });
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{e}");
                }
            }
        }
    }
}
