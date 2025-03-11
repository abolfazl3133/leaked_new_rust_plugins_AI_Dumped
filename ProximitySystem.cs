using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using System;
using Oxide.Core.Plugins;
using System.Diagnostics;
using ConVar;
using Physics = UnityEngine.Physics;
using Network;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("ProximitySystem", "Visagalis", "1.0.8")]
    [Description("Announces to DiscordMessages if players are wandering around in bigger groups.")]
    class ProximitySystem : RustPlugin
    {
        private int trackTimer;
        private int trackMeters;
        private int announceThreshold;
        private int banThreshold;
        private int kickThreshold;
        private int groupSize;
        private int keepHistoryForMinutes;
        private bool UseDiscordMessages;
		private bool ClearOnKill;
        private string discordMessagesWebhookUrl;
        private bool AddHereTag;

        private Dictionary<string, List<DateTime>> proximityHistory = new Dictionary<string, List<DateTime>>();

        [PluginReference("DiscordMessages")]
        Plugin discordMessages;

        public class ProximityData
        {
            public ulong playerA;
            public ulong playerB;
            public int range;
            public DateTime date;
        }

        void LoadConfig()
        {
            SetConfig("Settings", "Run check every x seconds", "60"); // trackTimer
            SetConfig("Settings", "Investigate players who are around x meters", "60"); // trackMeters
            SetConfig("Settings", "Announce threshold", "5"); //announceThreshold
            SetConfig("Settings", "Ban threshold", "0"); //banThreshold
            SetConfig("Settings", "Kick threshold", "0"); //kickThreshold
            SetConfig("Settings", "Group size", "4"); // groupSize
            SetConfig("Settings", "Clear data on kill", "1"); // ClearOnKill
            SetConfig("Settings", "Use DiscordMessages", "0"); // UseDiscordMessages
            SetConfig("Settings", "DiscordMessages WebhookURL", ""); // discordMessagesWebhookUrl
            SetConfig("Settings", "Keep history for x minutes", "60"); // keepHistoryForMinutes
            SetConfig("Settings", "Add @here tag", "1"); // AddHereTag
        }

        void InitConfig()
        {
            trackTimer = int.Parse(Config["Settings", "Run check every x seconds"].ToString());
            trackMeters = int.Parse(Config["Settings", "Investigate players who are around x meters"].ToString());
            announceThreshold = int.Parse(Config["Settings", "Announce threshold"].ToString());
            banThreshold = int.Parse(Config["Settings", "Ban threshold"].ToString());
            kickThreshold = int.Parse(Config["Settings", "Kick threshold"].ToString());
            groupSize = int.Parse(Config["Settings", "Group size"].ToString());
			ClearOnKill = int.Parse(Config["Settings", "Clear data on kill"].ToString()) == 1;
            UseDiscordMessages = int.Parse(Config["Settings", "Use DiscordMessages"].ToString()) == 1;
            discordMessagesWebhookUrl =  Config["Settings", "DiscordMessages WebhookURL"].ToString();
            keepHistoryForMinutes = int.Parse(Config["Settings", "Keep history for x minutes"].ToString());
            AddHereTag = int.Parse(Config["Settings", "Add @here tag"].ToString()) == 1;
        }

        void LoadDefaultConfig()
        {
            Puts("Generating new config file...");
            LoadConfig();
        }

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

		void OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			if (info?.InitiatorPlayer != null)
			{
				var playerA = Math.Min(player.userID, info.InitiatorPlayer.userID);
				var playerB = Math.Max(player.userID, info.InitiatorPlayer.userID);
				List<string> keysToRemove = new List<string>();
				foreach (var key in proximityHistory.Keys)
				{
					if (key.Contains(playerA.ToString()) && key.Contains(playerB.ToString()))
					   keysToRemove.Add(key);
				}

				foreach (var key in keysToRemove)
				{
					if (proximityHistory.ContainsKey(key))
						proximityHistory.Remove(key);
				}
			}
		}

        void OnServerInitialized()
        {
            LoadConfig();
            InitConfig();
			timer.Once(trackTimer, () =>
			{
				if(ClearOnKill)
                    Subscribe(nameof(OnPlayerDeath));
				else
                    Unsubscribe(nameof(OnPlayerDeath));
				
				RefreshData();
			});
        }

        void RefreshData()
        {
            Stopwatch perf = new Stopwatch();
            perf.Start();
            List<ProximityData> dataList = new List<ProximityData>();
            foreach (var currentPlayer in BasePlayer.activePlayerList)
            {
                if (dataList.Any(p => p.playerA == currentPlayer.userID))
                    continue;
                foreach (var distantPlayer in BasePlayer.activePlayerList)
                {
                    if (distantPlayer.IsAdmin || currentPlayer.IsAdmin)
                        continue;

                    if (distantPlayer.InSafeZone() || currentPlayer.InSafeZone())
                        continue;

                    if (distantPlayer.GetComponentInParent<CargoShip>() != null || currentPlayer.GetComponentInParent<CargoShip>() != null)
                        continue;

                    if (distantPlayer.IsWounded() || currentPlayer.IsWounded()
					    || distantPlayer.IsDead() || currentPlayer.IsDead())
                        continue;

                    if (currentPlayer.userID == distantPlayer.userID // same user
                        || dataList.Exists(d =>
                            d.playerA == Math.Min(currentPlayer.userID, distantPlayer.userID) 
                            && 
                            d.playerB == Math.Max(currentPlayer.userID, distantPlayer.userID))) // already in list
                        continue;


                    var distance = Vector3.Distance(currentPlayer.eyes.position, distantPlayer.eyes.position);
                    if (distance <= trackMeters && distantPlayer.IsAlive())
                    {
                        RaycastHit hitInfo;
                        if (!Physics.Linecast(currentPlayer.eyes.position, distantPlayer.eyes.position, out hitInfo,
                            LayerMask.GetMask("Player (Server)", "Construction", "Deployed", "World", "Default")))
                            continue;

                        if (hitInfo.collider.name.Contains("player.prefab"))
                            dataList.Add(new ProximityData
                            {
                                playerA = Math.Min(currentPlayer.userID, distantPlayer.userID),
                                playerB = Math.Max(currentPlayer.userID, distantPlayer.userID),
                                range = (int) distance
                            });
                    }
                }
            }

            Dictionary<ulong, ulong[]> playersInBiggerGroups = new Dictionary<ulong, ulong[]>();
            foreach(var playerId in BasePlayer.activePlayerList.Select(p => p.userID))
            {
                int playersInGroup = dataList.Count(d => d.playerA == playerId || d.playerB == playerId) + 1;
                if (playersInGroup >= groupSize)
                {
                    if (playersInBiggerGroups.All(d => d.Key != playerId && !d.Value.Contains(playerId))) {
                        var playersWhoAreClose = dataList
                            .Where(d => d.playerB == playerId).Select(p => p.playerA)
                            .Concat(dataList.Where(d => d.playerA == playerId).Select(p => p.playerB));
                        playersInBiggerGroups.Add(playerId, playersWhoAreClose.ToArray());
                    }
                }
            }

            DateTime currTime = DateTime.Now;
            foreach (var playerId in playersInBiggerGroups.Keys.OrderBy(p => p))
            {
                BasePlayer currPlr = BasePlayer.FindByID(playerId);
                string playerNames = $"http://steamcommunity.com/profiles/{currPlr.userID} | {currPlr.displayName}";
                var orderedList =  playersInBiggerGroups[playerId].OrderBy(p => p).ToArray();
                var keyForHistory = playerId + "-" + string.Join("-", orderedList.Select(u => u.ToString()).ToArray());
                if (!proximityHistory.ContainsKey(keyForHistory))
                    proximityHistory.Add(keyForHistory, new List<DateTime>());
                proximityHistory[keyForHistory].Add(currTime);
                int minTimesFound = 99999;
                foreach (var newPlayer in orderedList)
                {
                    var otherPlayer = BasePlayer.FindByID(newPlayer);
                    int timesFound = TimesFoundTogether(playerId, newPlayer);
                    if (minTimesFound > timesFound)
                        minTimesFound = timesFound;
                    playerNames += $"\nhttp://steamcommunity.com/profiles/{otherPlayer.userID} | {otherPlayer.displayName} ({timesFound})";
                }

                if(minTimesFound % announceThreshold == 0) 
                {
                    Puts($"Found players who are in group of *{playersInBiggerGroups[playerId].Length + 1}*:\n{playerNames}");                    
                    AnnounceGroup(playerId, playersInBiggerGroups[playerId]);
                    if (AddHereTag)
                        AnnounceToExternal("@here");
                }

                if (banThreshold > 0 && minTimesFound >= banThreshold)
                {
                    AnnounceToExternal($"Banned group of *{playersInBiggerGroups[playerId].Length + 1}*:\n{playerNames}");
                    BanGroup(playerId, playersInBiggerGroups[playerId]);
                }

                if (kickThreshold > 0 && minTimesFound >= kickThreshold)
                {
                    AnnounceToExternal($"Kicked group of *{playersInBiggerGroups[playerId].Length + 1}*:\n{playerNames}");
                    KickGroup(playerId, playersInBiggerGroups[playerId]);
                }
            }

            perf.Stop();
			if(perf.ElapsedMilliseconds > 500) // only log if performance is bad.
				timer.Once(1 , () => AnnounceToExternal($"Found {playersInBiggerGroups.Count} groups which are equal or bigger than {groupSize} took {perf.ElapsedMilliseconds}ms!"));

            timer.Once(trackTimer, RefreshData);
        }

        private void BanGroup(ulong playerId, ulong[] playersInGroup)
        {
            var leadPlayer = BasePlayer.FindByID(playerId);
            Ban(leadPlayer, $"Groups bigger than {groupSize-1} are automatically banned.");
            foreach (var groupPlayerId in playersInGroup)
            {
                var groupPlayer = BasePlayer.FindByID(groupPlayerId);
                Ban(groupPlayer, $"Groups bigger than {groupSize - 1} are automatically banned. ({leadPlayer.displayName})");
            }
        }

        private void KickGroup(ulong playerId, ulong[] playersInGroup)
        {
            var leadPlayer = BasePlayer.FindByID(playerId);
            Kick(leadPlayer, $"Groups bigger than {groupSize - 1} are automatically kicked.");
            foreach (var groupPlayerId in playersInGroup)
            {
                var groupPlayer = BasePlayer.FindByID(groupPlayerId);
                Kick(groupPlayer, $"Groups bigger than {groupSize - 1} are automatically kicked. ({leadPlayer.displayName})");
            }
        }

        private void Ban(BasePlayer player, string reason)
        {
            ServerUsers.User user = ServerUsers.Get(player.userID);
            if (user != null && user.group == ServerUsers.UserGroup.Banned)
            {
                Puts($"Failed to ban player: {player.userID}.");
            }
            else
            {
                ServerUsers.Set(player.userID, ServerUsers.UserGroup.Banned, player.displayName, reason);
                string str = "";
                if (player.IsConnected && (long) player.net.connection.ownerid != (long) player.net.connection.userid)
                {
                    str = str + " and also banned ownerid " + (object) player.net.connection.ownerid;
                    ServerUsers.Set(player.net.connection.ownerid, ServerUsers.UserGroup.Banned, player.displayName,
                        $"Family share owner of {player.net.connection.userid}");
                }

                ServerUsers.Save();
                Puts("Kickbanned User: " + player.userID + " - " + player.displayName + str);
                Network.Net.sv.Kick(player.net.connection, "Banned: " + reason);
            }
        }

        private void Kick(BasePlayer player, string reason)
        {
            player.Kick(reason);
        }

        private int TimesFoundTogether(ulong player, ulong otherPlayer)
        {
            ClearOldEntries();
            return proximityHistory.Where(p => p.Key.Contains(player.ToString()) && p.Key.Contains(otherPlayer.ToString())).Sum(p=> p.Value.Count);
        }

        private void ClearOldEntries()
        {
            foreach (var key in proximityHistory.Keys)
            {
                proximityHistory[key].RemoveAll(p => p < DateTime.Now.AddMinutes(keepHistoryForMinutes * -1));
            }

            foreach (var key in proximityHistory.Where(p => p.Value.Count == 0).Select(k => k.Key).ToArray())
            {
                proximityHistory.Remove(key);
            }
        }

        private void AnnounceToExternal(string message)
        {
            Puts(message);
            if (UseDiscordMessages)
                discordMessages?.Call("API_SendTextMessage", discordMessagesWebhookUrl, message);
        }

        private void AnnounceGroup(ulong playerId, ulong[] list)
        {
            if (!UseDiscordMessages)
                return;

            BasePlayer player = BasePlayer.FindByID(playerId);
            var players = list.Select(x => new { p = BasePlayer.FindByID(x), foundTogether = TimesFoundTogether(playerId, x) }).ToArray();
            var playersData = $"[{player?.displayName}](https://steamcommunity.com/profiles/{player?.userID})\n";

            foreach(var plr in players)
            {
                playersData += $"[{plr.p?.displayName}](https://steamcommunity.com/profiles/{plr.p?.userID}) ({plr.foundTogether})\n";
            }

            object payload = new List<dynamic>()
            {
                new{name="Players", value=playersData.Trim(), inline = false },
                new{name="Location", value=GetPositionNameInGrid(player.transform.position), inline = false },
            };

            string json = JsonConvert.SerializeObject(payload);
            discordMessages?.Call("API_SendFancyMessage", discordMessagesWebhookUrl, $"Group of {list.Length + 1} detected", 3329330, json);
        }


        string GetPositionNameInGrid(Vector3 position) // Credit: Jake_Rich
        {
            Vector2 roundedPos = new Vector2(World.Size / 2 + position.x, World.Size / 2 - position.z);

            string grid = $"{NumberToLetter((int)(roundedPos.x / 150))}{(int)(roundedPos.y / 150)} {position}";

            return grid;
        }

        public static string NumberToLetter(int num) // Credit: Jake_Rich
        {
            int num2 = Mathf.FloorToInt((float)(num / 26));
            int num3 = num % 26;
            string text = string.Empty;
            if (num2 > 0)
            {
                for (int i = 0; i < num2; i++)
                {
                    text += Convert.ToChar(65 + i);
                }
            }
            return text + Convert.ToChar(65 + num3);
        }

        [ConsoleCommand("testprox")]
        private void ccmdTestProx(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            if (BasePlayer.activePlayerList.Any()) 
            { 
                var testUserId = BasePlayer.activePlayerList.First().userID;
                AnnounceGroup(testUserId, new ulong[] { testUserId, testUserId, testUserId });
            }
            AnnounceToExternal("Testing ProximitySystem announcement work.");
        }
    }
}
