using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Loading Messages", "CosaNostra/Def/klauz24", "1.0.9")]
    [Description("Shows custom texts on loading screen")]
    public class LoadingMessages : RustPlugin
    {
        private readonly Dictionary<ulong, Connection> _clients = new Dictionary<ulong, Connection>();
        private readonly List<ulong> _disconnectedClients = new List<ulong>();

        private static MsgConfig _config;
        private Timer _timer;
        private static MsgCollection _messages, _messagesQueue;

        private class MsgCollection
        {
            public List<MsgEntry> MessagesList;
            public MsgEntry CurrentMessage;
            private int _messageIndex;
            private float _nextMessageChange;

            public void AdvanceMessage()
            {
                if (!_config.EnableCyclicity || Time.realtimeSinceStartup < _nextMessageChange)
                    return;
                _nextMessageChange = Time.realtimeSinceStartup + _config.CyclicityFreq;
                if (_config.EnableRandomCyclicity)
                    CurrentMessage = PickRandom(MessagesList);
                else
                {
                    CurrentMessage = MessagesList[_messageIndex++];
                    if (_messageIndex >= MessagesList.Count)
                        _messageIndex = 0;
                }
            }

            public void SelectFirst() => CurrentMessage = MessagesList.First();
        }

        private class MsgConfig
        {
            [JsonProperty("Text Display Frequency (Seconds)")]
            public float TimerFreq;
            [JsonProperty("Enable Messages Cyclicity")]
            public bool EnableCyclicity;
            [JsonProperty("Use Random Cyclicity (Instead of sequential)")]
            public bool EnableRandomCyclicity;
            [JsonProperty("Cycle Messages Every ~N Seconds")]
            public float CyclicityFreq;
            [JsonProperty("Messages")]
            public List<MsgEntry> Msgs;
            [JsonProperty("Enable Queue Messages")]
            public bool EnableQueueMessages;
            [JsonProperty("Queue Messages")]
            public List<MsgEntry> QueueMsgs;
            [JsonProperty("Last Message (When entering game)")]
            public MsgEntry LastMessage;
        }

        private class MsgEntry
        {
            [JsonProperty("Top Status")]
            public string TopString;
            [JsonProperty("Bottom Status")]
            public string BottomString;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<MsgConfig>();
            _messages = new MsgCollection { MessagesList = _config.Msgs };
            _messagesQueue = new MsgCollection { MessagesList = _config.QueueMsgs };
            if (_config.EnableQueueMessages || _config.QueueMsgs != null)
                return;
            _config.QueueMsgs = new List<MsgEntry>
            {
                new MsgEntry{TopString = "<color=#ff0>You're in queue...</color>", BottomString = "<color=#9cf>{AHEAD} players ahead of you.</color>"},
                new MsgEntry{TopString = "<color=#9cf>You're in queue...</color>", BottomString = "<color=#ff0>{BEHIND} players behind you.</color>"}
            };
            SaveConfig();
            PrintWarning("Detected probably outdated config. New entries added. Check your config.");
        }

        protected override void LoadDefaultConfig()
        {
            _config = new MsgConfig
            {
                TimerFreq = .30f,
                EnableCyclicity = false,
                EnableRandomCyclicity = false,
                CyclicityFreq = 3.0f,
                Msgs = new List<MsgEntry>
                {
                    new MsgEntry{TopString = "<color=#ff0>Welcome {PLAYERNAME}!</color>", BottomString = "<color=#9cf>Enjoy your stay.</color>"},
                    new MsgEntry{TopString = "<color=#9cf>Hi there!</color>", BottomString = "<color=#ff0>Enjoy your stay.</color>"}
                },
                EnableQueueMessages = true,
                QueueMsgs = new List<MsgEntry>
                {
                    new MsgEntry{TopString = "<color=#ff0>You're in queue...</color>", BottomString = "<color=#9cf>{AHEAD} players ahead of you.</color>"},
                    new MsgEntry{TopString = "<color=#ff0>You're in queue...</color>", BottomString = "<color=#ff0>{BEHIND} players behind you.</color>"}
                },
                LastMessage = new MsgEntry { TopString = "<color=#ff0>Welcome to our server!</color>", BottomString = "<color=#060>Entering game...</color>" }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        private void Unload()
        {
            _messages = null;
            _messagesQueue = null;
        }

        private void Loaded()
        {
            if (_config?.Msgs == null || _config.Msgs.Count == 0)
            {
                Unsubscribe(nameof(OnUserApprove));
                Unsubscribe(nameof(OnPlayerConnected));
                PrintWarning("No loading messages defined! Check your config.");
                return;
            }
            if (_config.EnableCyclicity && _config.Msgs.Count <= 1)
            {
                _config.EnableCyclicity = false;
                PrintWarning("You have message cyclicity enabled, but only 1 message is defined. Check your config.");
            }
            if (_config.EnableQueueMessages && _config.QueueMsgs == null || _config.QueueMsgs.Count == 0)
            {
                Unsubscribe(nameof(OnQueueMessage));
                _config.EnableQueueMessages = false;
                PrintWarning("You have queue messages enabled, but no queue messages is defined. Check your config.");
            }
            _messages.SelectFirst();
            if (_config.EnableQueueMessages)
            {
                _messagesQueue.SelectFirst();
            }
        }

        private void OnUserApprove(Connection connection)
        {
            _clients[connection.userid] = connection;
            if (_timer == null)
            {
                _timer = timer.Every(_config.TimerFreq, HandleClients);
            }
            DisplayMessage(connection, GetCurrentMessage());
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            _clients.Remove(player.userID);
            DisplayMessage(player.Connection, GetLastMessage() ?? GetCurrentMessage());
        }

        private void HandleClients()
        {
            if (_clients.Count == 0)
            {
                _timer.Destroy();
                _timer = null;
                return;
            }
            UpdateCurrentMessages();
            foreach (var client in _clients.Values)
            {
                if (!client.active)
                {
                    _disconnectedClients.Add(client.userid);
                    continue;
                }
                if (client.state == Connection.State.InQueue)
                {
                    continue;
                }
                DisplayMessage(client, GetCurrentMessage());
            }
            if (_disconnectedClients.Count > 0)
            {
                _disconnectedClients.ForEach(uid => _clients.Remove(uid));
                _disconnectedClients.Clear();
            }
        }

        private static void DisplayMessage(Connection conn, MsgEntry msgEntry)
        {
            SendPacket(conn, HandleString(msgEntry.TopString, GetPlayerName(conn), false), HandleString(msgEntry.BottomString, GetPlayerName(conn), false));
        }

        private object OnQueueMessage(Connection conn, int pos)
        {
            if (conn != null)
            {
                var behind = (ServerMgr.Instance.connectionQueue.Queued - pos) - 1;
                SendPacket(conn, HandleString(_messagesQueue.CurrentMessage.TopString, GetPlayerName(conn), true, pos, behind), HandleString(_messagesQueue.CurrentMessage.BottomString, GetPlayerName(conn), true, pos, behind));
                return true;
            }
            return null;
        }

        private static void SendPacket(Connection conn, string top, string bottom)
        {
            var net = Net.sv.StartWrite();
            net.PacketID(Message.Type.Message);
            net.String(top);
            net.String(bottom);
            net.Send(new SendInfo(conn));
        }

        private static string HandleString(string raw, string playerName, bool isQueue, int ahead = 0, int behind = 0)
        {
            if (isQueue)
            {
                return raw.Replace("{AHEAD}", ahead.ToString()).Replace("{BEHIND}", behind.ToString()).Replace("{PLAYERNAME}", playerName);
            }
            else
            {
                return raw.Replace("{PLAYERNAME}", playerName);
            }
        }

        private static T PickRandom<T>(IReadOnlyList<T> list) => list[Random.Range(0, list.Count - 1)];
        private static MsgEntry GetCurrentMessage() => _messages.CurrentMessage;
        private static MsgEntry GetLastMessage() => _config.LastMessage;
        private static MsgCollection GetMessagesCollection() => _messages;
        private static MsgCollection GetQueueMessagesCollection() => _messagesQueue;
        private static void UpdateCurrentMessages()
        {
            GetMessagesCollection().AdvanceMessage();
            GetQueueMessagesCollection().AdvanceMessage();
        }
        private static string GetPlayerName(Connection conn) => conn.username;
    }
}