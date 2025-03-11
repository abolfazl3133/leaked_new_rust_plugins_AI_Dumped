using Network;
using Oxide.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("WordOfGod", "WebGhost", "1.0.0")]
    public class WordOfGod: RustPlugin
    {
        private static readonly string Perm = "WordOfGod.admin";
        private float MegaphoneDistance = 100 f;
        private int nodespace = 600;
        private List < ulong > Silenced = new List < ulong > ();
        private List < BasePlayer > OutPutNodes = new List < BasePlayer > ();
        private Dictionary < BasePlayer, List < BasePlayer >> Passthough = new Dictionary < BasePlayer, List < BasePlayer >> ();
        private void Init() => permission.RegisterPermission(Perm, this);
        void Unload()
        {
            foreach(BasePlayer bp in OutPutNodes)
            {
                bp.Kill();
            }
            Silenced.Clear();
            Passthough.Clear();
        }
        private object OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (Passthough.ContainsKey(player))
            {
                foreach(BasePlayer mouths in Passthough[player].ToArray())
                {
                    InvokeHandler.Instance.StartCoroutine(Playpacket(data, mouths));
                }
                return true;
            }
            if (Silenced.Contains(player.userID))
            {
                return true;
            }
            return null;
        }
        private IEnumerator Playpacket(byte[] VD, BasePlayer newPlayer)
        {
            {
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.VoiceData);
                netWrite.UInt64(newPlayer.net.ID.Value);
                netWrite.BytesWithSize(VD);
                netWrite.Send(new SendInfo(global::BaseNetworkable.GetConnectionsWithin(newPlayer.transform.position, 800 f))
                {
                    priority = Priority.Immediate
                });
            }
            yield
            return new WaitForSeconds(0.01 f);
        }
        private BasePlayer Findplayer(BasePlayer player, string target = "")
            {
                BasePlayer Found = null;
                if (target != "")
                {
                    Found = BasePlayer.FindAwakeOrSleeping(target);
                    if (Found == null)
                    {
                        Found = BasePlayer.FindBotClosestMatch(target);
                    }
                    if (Found == null)
                    {
                        foreach(BasePlayer p in BasePlayer.allPlayerList)
                        {
                            if (p.displayName.Contains(target))
                            {
                                Found = p;
                                continue;
                            }
                        }
                    }
                    if (Found == null)
                    {
                        foreach(BasePlayer p in BasePlayer.bots)
                        {
                            if (p.displayName.Contains(target))
                            {
                                Found = p;
                                continue;
                            }
                        }
                    }
                }
                else
                {
                    RaycastHit hit;
                    if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 15 f, -1, QueryTriggerInteraction.Ignore))
                    {
                        return null;
                    }
                    var entity = hit.GetEntity();
                    if (entity == null) return null;
                    Found = entity.ToPlayer();
                }
                if (Found == null) return null;
                return Found;
            }
            [ChatCommand("STFU")]
        private void cmdshutthefu(BasePlayer player, string command, string[] args)
            {
                if (!player.IPlayer.HasPermission(Perm)) return;
                BasePlayer target = null;
                if (args.Count() == 1)
                {
                    target = Findplayer(player, args[0]);
                }
                else
                {
                    target = Findplayer(player);
                }
                if (target == null)
                {
                    player.ChatMessage("Unable to find player/bot");
                    return;
                }
                if (Silenced.Contains(target.userID))
                {
                    Silenced.Remove(target.userID);
                    player.ChatMessage("Returned " + target.displayName + " [" + target.UserIDString + "] voice.");
                }
                else
                {
                    Silenced.Add(target.userID);
                    player.ChatMessage("Removed " + target.displayName + " [" + target.UserIDString + "] voice.");
                }
            }
            [ChatCommand("WOG")]
        private void cmdwordofgod(BasePlayer player, string command, string[] args)
            {
                if (!player.IPlayer.HasPermission(Perm)) return;
                if (Passthough.ContainsKey(player))
                {
                    Passthough.Remove(player);
                    player.ChatMessage("Stopped passthough of voice.");
                    return;
                }
                BasePlayer target = null;
                if (args.Count() == 1)
                {
                    target = Findplayer(player, args[0]);
                }
                else
                {
                    target = Findplayer(player);
                }
                if (target == null)
                {
                    player.ChatMessage("Player not found.");
                    return;
                }
                Passthough.Add(player, new List < BasePlayer >
                {
                    target
                });
                player.ChatMessage("Talking though " + target.displayName);
            }
            [ChatCommand("WOGADD")]
        private void cmdaddmouth(BasePlayer player, string command, string[] args)
            {
                if (!player.IPlayer.HasPermission(Perm)) return;
                if (Passthough.ContainsKey(player))
                {
                    BasePlayer target = null;
                    if (args.Count() == 1)
                    {
                        target = Findplayer(player, args[0]);
                    }
                    else
                    {
                        target = Findplayer(player);
                    }
                    if (target == null)
                    {
                        player.ChatMessage("Player not found.");
                        return;
                    }
                    if (!Passthough[player].Contains(target))
                    {
                        Passthough[player].Add(target);
                        player.ChatMessage("Talking though " + target.displayName);
                    }
                    else
                    {
                        Passthough[player].Remove(target);
                        player.ChatMessage("Removed talk though " + target.displayName);
                    }
                }
            }
            [ChatCommand("cbot")]
        private void cmdCreateBot(BasePlayer player, string command, string[] args)
            {
                if (!player.IPlayer.HasPermission(Perm)) return;
                Vector3 botPosition = player.transform.position;
                CreateVoiceNode(botPosition);
                BasePlayer bot = OutPutNodes.LastOrDefault();
                if (bot != null)
                {
                    if (!Passthough.ContainsKey(player)) Passthough.Add(player, new List < BasePlayer > ());
                    Passthough[player].Add(bot);
                    player.ChatMessage("Created a bot and set it to transmit your voice.");
                }
            }
            [ChatCommand("rbot")]
        private void cmdRemoveBots(BasePlayer player, string command, string[] args)
        {
            foreach(BasePlayer bp in OutPutNodes)
            {
                bp.Kill();
            }
            Silenced.Clear();
            Passthough.Clear();
            if (!player.IPlayer.HasPermission(Perm)) return;
            if (OutPutNodes != null)
            {
                foreach(BasePlayer bot in OutPutNodes)
                {
                    if (bot != null && !bot.IsDestroyed)
                    {
                        bot.Kill();
                    }
                }
                OutPutNodes.Clear();
            }
            player.ChatMessage("Removed all bots.");
        }
        private Dictionary < BasePlayer, BaseEntity > Drones = new Dictionary < BasePlayer, BaseEntity > ();
        [ChatCommand("createdrone")]
        private void cmdCreateDrone(BasePlayer player, string command, string[] args)
            {
                if (!player.IPlayer.HasPermission(Perm)) return;
                Vector3 dronePosition = player.transform.position + new Vector3(0, 2, 0);
                BaseEntity drone = GameManager.server.CreateEntity("assets/prefabs/deployable/drone/drone.deployed.prefab", dronePosition);
                if (drone != null)
                {
                    drone.Spawn();
                    Drones[player] = drone;
                    BasePlayer bot = CreateVoiceNode(dronePosition + new Vector3(0, 1, 0));
                    if (!Passthough.ContainsKey(player)) Passthough.Add(player, new List < BasePlayer > ());
                    Passthough[player].Add(bot);
                    FollowDrone(bot, drone);
                    player.ChatMessage("Drone created and bot assigned to follow it.");
                }
                else
                {
                    player.ChatMessage("Failed to create drone.");
                }
            }
            [ChatCommand("removedrone")]
        private void cmdRemoveDrone(BasePlayer player, string command, string[] args)
        {
            if (!player.IPlayer.HasPermission(Perm)) return;
            if (Drones.TryGetValue(player, out BaseEntity drone))
            {
                drone.Kill();
                Drones.Remove(player);
                player.ChatMessage("Drone removed.");
            }
            else
            {
                player.ChatMessage("No drone to remove.");
            }
        }
        private void FollowDrone(BasePlayer bot, BaseEntity drone)
        {
            bot.StartCoroutine(FollowDroneCoroutine(bot, drone));
        }
        private IEnumerator FollowDroneCoroutine(BasePlayer bot, BaseEntity drone)
        {
            while (bot != null && drone != null && !drone.IsDestroyed)
            {
                bot.transform.position = drone.transform.position + new Vector3(0, 1, 0);
                yield
                return new WaitForSeconds(0.5 f);
            }
        }
        private BasePlayer CreateVoiceNode(Vector3 pos)
            {
                BasePlayer newPlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", pos).ToPlayer();
                newPlayer.Spawn();
                Interface.Oxide.CallHook("OnNPCRespawn", newPlayer);
                OutPutNodes.Add(newPlayer);
                return newPlayer;
            }
            [ChatCommand("WOGALL")]
        private void cmdVoiceToAll(BasePlayer player, string command, string[] args)
        {
            if (!player.IPlayer.HasPermission(Perm)) return;
            if (OutPutNodes.Count != 0)
            {
                Megaphone.MegaphoneVoiceRange = MegaphoneDistance;
                foreach(BasePlayer bp in OutPutNodes)
                {
                    bp.Kill();
                }
                OutPutNodes.Clear();
                player.ChatMessage("Removed Voice Bots.");
                return;
            }
            var size = ConVar.Server.worldsize / 2 f;
            for (int x = (int)(size * -1); x < size;)
            {
                for (int z = (int)(size * -1); z < size;)
                {
                    CreateVoiceNode(new Vector3(x, TerrainMeta.HeightMap.GetHeight(new Vector3(x, 0, z)) - 5, z));
                    z += nodespace;
                }
                x += nodespace;
            }
            if (Passthough.ContainsKey(player))
            {
                Passthough.Remove(player);
            }
            Passthough.Add(player, OutPutNodes);
            MegaphoneDistance = Megaphone.MegaphoneVoiceRange;
            Megaphone.MegaphoneVoiceRange = 2000 f;
            player.ChatMessage("Created " + OutPutNodes.Count().ToString() + " Voice Bots.");
        }
    }
}