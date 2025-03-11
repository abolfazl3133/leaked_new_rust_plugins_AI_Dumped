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
using Rust.Ai;

namespace Oxide.Plugins
{
    [Info("AdminRadio", "Arch_Dem0n, sell Ernieleo", "1.0.0")]
    public class AdminRadio : RustPlugin
    {
        object OnNpcPlayerResume(ScientistNPC scientist) => voiceNPCs.Contains(scientist) ? false : (object)null;
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info) => voiceNPCs.Contains(entity) ? false : (object)null;
        static List<ScientistNPC> voiceNPCs = new List<ScientistNPC>();

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
            public ScientistNPC scientist = null;
            public void Destroy()
            {
                voiceNPCs.Remove(scientist);
                scientist?.Kill();
                Destroy(this);
            }
            public void Start()
            {
                scientist = GameManager.server.CreateEntity("Assets/Prefabs/NPC/Scientist/ScientistStationary.prefab".ToLower(), this.transform.TransformPoint(lposition)) as ScientistNPC;
                if (scientist == null)
                {
                    Debug.LogError("Failed to create NPC entity");
                    return;
                }
                scientist.Spawn();
                (scientist.GetActiveItem()?.GetHeldEntity() as HeldEntity)?.SetHeld(false);
                voiceNPCs.Add(scientist);
            }
            public void Update()
            {
                if (scientist?.net?.ID == null || player?.Connection == null)
                {
                    Destroy();
                    return;
                }
                scientist.transform.position = this.transform.TransformPoint(lposition);
            }
            public void Send(byte[] data)
            {
                try
                {
                    if (scientist?.net?.ID == null || player?.Connection == null)
                    {
                        Destroy();
                        return;
                    }

                    var write = Network.Net.sv.StartWrite();
                    if (write != null)
                    {
                        write.PacketID(Network.Message.Type.VoiceData);
                        write.UInt32((uint)scientist.net.ID.Value); // Convert ulong to uint
                        write.BytesWithSize(data);
                        write.Send(new Network.SendInfo(player.Connection) { priority = Network.Priority.Immediate });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error sending voice data: {e}");
                }
            }
        }
    }
}
