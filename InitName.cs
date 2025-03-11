using System;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("InitName", "sdapro", "1.0.2")]
    class InitName : RustPlugin
    {
        #region Vars
        [PluginReference]
        private Plugin Clans, Friends;
        private PluginConfig config;
        private static InitName inst;
        private Timer Checker;
        private static PluginData data;
        #endregion

        #region Oxide hooks
        void Init()
        {
            inst = this;
        }
        void OnServerInitialized()
        {
            Checker = timer.Every(1f, CheckComponent);
        }
        void Unload()
        {
            if (Checker != null && !Checker.Destroyed)
                Checker.Destroy();
            var drawers = UnityEngine.Object.FindObjectsOfType<FriendsDrawer>();
            foreach (var drawer in drawers)
                UnityEngine.Object.Destroy(drawer);
            SaveData();
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var comp = player.GetComponent<FriendsDrawer>();
            if (comp != null)
                UnityEngine.Object.Destroy(comp);
        }
        void OnServerSave() => SaveData();
        #endregion

        #region Config
        private class PluginColors
        {
            [JsonProperty("Цвет друзей")]
            public string Friends = "#20f9dc";
            [JsonProperty("Цвет соклановцев")]
            public string ClanMates = "#2576f9";
            [JsonProperty("Цвет здоровья выше среднего")]
            public string HealthHight = "green";
            [JsonProperty("Цвет здоровья на среднем уровне")]
            public string HealthMiddle = "orange";
            [JsonProperty("Цвет здоровья ниже среднего")]
            public string HealthLow = "red";
        }
        private class PluginConfig
        {
            [JsonProperty("Привилегия для показа друзей")]
            public string Permission = "squadnames.see";
            [JsonProperty("Использовать плагин друзей")]
            public bool UseFriends = true;
            [JsonProperty("Использовать плагин кланов")]
            public bool UseClans = true;
            [JsonProperty("Частота обновлений")]
            public float UpdateInterval = 1f;
            [JsonProperty("Максимальная дальность проривсоки(0 - без ограничений)")]
            public double MaxDistance = 100d;
            [JsonProperty("Настройка цветов")]
            public PluginColors Colors = new PluginColors();
            [JsonProperty("Формат прорисорвки")]
            public string Format;
        }
        #endregion

        #region Config and data initialization
        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            if (config.Format == null)
            {
                config.Format = "{Name}\n{Health}\n{Distance}\n{ActiveItem}";
                SaveConfig();
            }
            permission.RegisterPermission(config.Permission, this);
            LoadData();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Localization
        private string GetMsg(string key, BasePlayer player = null, params object[] args)
        {
            string result = lang.GetMessage(key, this, player == null ? null : player.UserIDString);
            if (args.Length > 0)
                return string.Format(result, args);
            return result;
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["HP"] = "{0} HP",
                ["Distance"] = "{0} meters",
                ["Visible"] = "<color=#41f4dc>visible</color> to your friends.",
                ["Hidden"] = "<color=#f48e41>hidden</color> from your friends",
                ["Now"] = "You are now {0}",
                ["Status"] = "You are currently {0}\n",
                ["Syntax"] = "Use <color=#42f462>/sn add(+)/remove(-) player</color> to add or remove player from your hide list\nUse <color=#42f462>/sn list</color> to see the list of people you hide\nOr <color=#42f462>/sn self</color> to hide yourself from your friends",
                ["HideList"] = "Players you are hiding from the screen:\n{0}",
                ["HideListEmpty"] = "You are not hiding any players from the screen",
                ["PlayerNotFound"] = "Player \"{0}\" not found.",
                ["PlayerRemoved"] = "Player \"{0}\" removed from hidden list.",
                ["PlayerAdded"] = "Player \"{0}\" added to hidden list.",
                ["CantAddSelf"] = "You can't add yourself to your hidden list.",
                ["MultiplyFound"] = "Multiply players found:\n{0}",
                ["AlreadyInList"] = "Player \"{0}\" already in hidden list.",
                ["Nothing"] = "Nothing",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["HP"] = "{0} ХП",
                ["Distance"] = "{0} метр.",
                ["Visible"] = "<color=#41f4dc>ВИДИМЫ</color>",
                ["Hidden"] = "<color=#f48e41>СКРЫТЫ</color>",
                ["Now"] = "Вы теперь {0} для ваших друзей",
                ["Status"] = "Вы на данный момент {0} для ваших друзей\n",
                ["Syntax"] = "Используйте <color=#42f462>/sn add(+)/remove(-) ник</color> чтобы добавить игрока в список скрываемых\nИспользвйте <color=#42f462>/sn list</color> чтобы просмотреть список игроков, которых вы скрываете\nИли <color=#42f462>/sn self</color> чтобы скрыть себя от друзей",
                ["HideList"] = "Список игроков, которых вы скрыли с экрана:\n{0}",
                ["HideListEmpty"] = "Список скрываемых игроков пуст",
                ["PlayerNotFound"] = "Игрок \"{0}\" не найден.",
                ["PlayerRemoved"] = "Игрок \"{0}\" удалён из списка скрываемых.",
                ["PlayerAdded"] = "Игрок \"{0}\" добавлен в список скрываемых.",
                ["CantAddSelf"] = "Вы не можете добавить самого себя в список скрываемых.",
                ["MultiplyFound"] = "Найдено несколько подходящих игроков:\n{0}",
                ["AlreadyInList"] = "Игрок \"{0}\" уже в списке скрываемых.",
                ["Nothing"] = "Ничего",
            }, this, "ru");
        }
        #endregion

        #region Data
        private class PlayerData
        {
            public string Name;
            public ulong ID;
        }
        private class PluginData
        {
            public readonly List<ulong> Self = new List<ulong>();
            public Dictionary<ulong, List<PlayerData>> Ignore = new Dictionary<ulong, List<PlayerData>>();
        }
        private void SaveData()
        {
            var savedata = data.Ignore.Where(p => p.Value != null && p.Value.Count != 0).ToDictionary(x => x.Key, x => x.Value);
            data.Ignore = savedata;
            Interface.Oxide.DataFileSystem.WriteObject(Title + "_ignore", data);
        }
        void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Title + "_ignore");
            }
            catch (Exception ex)
            {
                RaiseError($"Failed to load data file with player prefrences (is the file corrupt?) ({ex.Message})");
                data = new PluginData();
            }
        }
        #endregion

        #region DDraw
        private static void DdrawText(BasePlayer player, Vector3 pos, string text, Color color, float duration)
        {
            if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
            {

                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                player.SendConsoleCommand("ddraw.text", duration, color, pos, text);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdate();
            }
            else
                player.SendConsoleCommand("ddraw.text", duration, color, pos, text);
        }
        #endregion

        #region Friends getter

        #region Clans
        private string GetClan(BasePlayer player)
        {
            string clanName = Clans?.Call<string>("GetClanOf", player.UserIDString);
            return string.IsNullOrEmpty(clanName) ? null : clanName;
        }
        private List<string> GetClanMembers(string tag)
        {
            var result = new List<string>();
            var clan = Clans?.Call("GetClan", tag);
            if (clan != null && clan is JObject)
            {
                var members = (clan as JObject).GetValue("members");
                if (members != null && members is JArray)
                {
                    foreach (var member in (JArray)members)
                        result.Add(member.ToString());
                }
            }
            return result;
        }
        private List<BasePlayer> GetClanmates(BasePlayer player)
        {
            List<BasePlayer> result = new List<BasePlayer>();
            string clantag = GetClan(player);
            if (!string.IsNullOrEmpty(clantag))
            {
                var members = GetClanMembers(clantag);
                foreach (var member in members)
                {
                    ulong id;
                    if (!ulong.TryParse(member, out id))
                        continue;
                    BasePlayer founded = BasePlayer.FindByID(id);
                    if (founded != null && founded != player)
                        result.Add(founded);
                }
            }
            return result;
        }
        #endregion

        #region Friends
        List<string> GetFriendsApi(BasePlayer player)
        {
            var friends = Friends?.Call("IsFriendOfS", player.UserIDString);
            if (friends is string[])
            {
                var a = friends as string[];
                return (friends as string[]).ToList();
            }
            return new List<string>();
        }
        List<string> GetUniversalFriends(BasePlayer player)
        {
            var success = Friends?.Call("GetFriendsReverse", player.UserIDString);
            if (success is string[])
            {
                return (success as string[]).ToList();
            }
            return new List<string>();
        }
        List<BasePlayer> GetFriends(BasePlayer player)
        {
            List<BasePlayer> result = new List<BasePlayer>();
            List<string> friendsStr;
            if (Friends)
            {
                if (Friends.ResourceId == 686)
                    friendsStr = GetFriendsApi(player);
                else
                    friendsStr = GetUniversalFriends(player);
                foreach (var friend in friendsStr)
                {
                    ulong id;
                    if (!ulong.TryParse(friend, out id))
                        continue;
                    BasePlayer founded = BasePlayer.FindByID(id);
                    if (founded != null)
                        result.Add(founded);
                }
            }
            return result;
        }
        #endregion

        #endregion

        #region ChatCommand
        [ChatCommand("sn")]
        private void cmdIgnore(BasePlayer player, string command, string[] args)
        {
            string state = data.Self.Contains(player.userID) ? GetMsg("Hidden", player) : GetMsg("Visible", player);
            if (args.Length <= 0)
            {
                player.ChatMessage(GetMsg("Status", player, state) + GetMsg("Syntax", player));
                return;
            }
            if (args.Length == 1 && args[0] == "list")
            {
                if (!data.Ignore.ContainsKey(player.userID) || (data.Ignore.ContainsKey(player.userID) && data.Ignore[player.userID].Count == 0))
                {
                    player.ChatMessage(GetMsg("Status", player, state) + GetMsg("HideListEmpty", player));
                    return;
                }
                player.ChatMessage(GetMsg("Status", player, state) + GetMsg("HideList", player, string.Join("\n", data.Ignore[player.userID].Select(p => $"{p.Name} ({p.ID})").ToArray())));
                return;
            }
            if (args.Length == 1 && args[0] == "self")
            {
                var reply = 540;
                if (data.Self.Contains(player.userID))
                    data.Self.Remove(player.userID);
                else
                    data.Self.Add(player.userID);
                state = data.Self.Contains(player.userID) ? GetMsg("Hidden", player) : GetMsg("Visible", player);
                player.ChatMessage(GetMsg("Now", player, state));
                return;
            }
            if (args.Length < 2)
            {
                player.ChatMessage(GetMsg("Syntax", player, state));
                return;
            }
            switch (args[0])
            {
                case "+":
                case "add":
                    var founded = BasePlayer.activePlayerList.Where(p => p.UserIDString == args[1] || p.displayName.Contains(args[1], CompareOptions.IgnoreCase));
                    if (!founded.Any())
                    {
                        player.ChatMessage(GetMsg("PlayerNotFound", player, args[1]));
                        return;
                    }
                    if (founded.Count() > 1)
                    {
                        player.ChatMessage(GetMsg("MultiplyFound", player, string.Join("\n", founded.Select(p => $"{p.displayName}({p.UserIDString})").ToArray())));
                        return;
                    }
                    BasePlayer adding = founded.First();
                    if (adding == player)
                    {
                        player.ChatMessage(GetMsg("CantAddSelf", player));
                        return;
                    }
                    if (!data.Ignore.ContainsKey(player.userID))
                    {
                        data.Ignore[player.userID] = new List<PlayerData>() { new PlayerData { Name = adding.displayName, ID = adding.userID } };
                        player.ChatMessage(GetMsg("PlayerAdded", player, adding.displayName));
                        return;
                    }
                    if (data.Ignore[player.userID].Any(p => p.ID == adding.userID))
                    {
                        player.ChatMessage(GetMsg("AlreadyInList", player, adding.displayName));
                        return;
                    }
                    data.Ignore[player.userID].Add(new PlayerData { Name = adding.displayName, ID = adding.userID });
                    player.ChatMessage(GetMsg("PlayerAdded", player, adding.displayName));
                    return;
                case "-":
                case "remove":
                    if (!data.Ignore.ContainsKey(player.userID) || data.Ignore.ContainsKey(player.userID) && data.Ignore[player.userID].Count == 0)
                    {
                        player.ChatMessage(GetMsg("HideListEmpty", player));
                        return;
                    }
                    var removing = data.Ignore[player.userID].FirstOrDefault(p => p.ID.ToString() == args[1] || p.Name.Contains(args[1], CompareOptions.IgnoreCase));
                    if (removing == null)
                    {
                        player.ChatMessage(GetMsg("PlayerNotFound", player, args[1]));
                        return;
                    }
                    data.Ignore[player.userID].Remove(removing);
                    player.ChatMessage(GetMsg("PlayerRemoved", player, removing.Name));
                    return;
            }
        }
        #endregion

        #region FriendDrawer class
        private void CheckComponent()
        {
            if (BasePlayer.activePlayerList == null) return;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if(player == null) continue;
                
                var perm = permission.UserHasPermission(player.UserIDString, config.Permission);
                var comp = player.GetComponent<FriendsDrawer>();
                if (comp == null)
                {
                    if (perm)
                        player.gameObject.AddComponent<FriendsDrawer>();
                }
                else
                {
                    if (!perm)
                        UnityEngine.Object.Destroy(comp);
                }
            }
        }
        private class FriendsDrawer : MonoBehaviour
        {
            BasePlayer player;
            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("DisplayFriends", 0f, inst.config.UpdateInterval);
            }
            private void DisplayFriends()
            {
                List<BasePlayer> shown = new List<BasePlayer>();
                if (inst.config.UseFriends)
                {
                    var friends = inst.GetFriends(player);
                    foreach (var friend in friends)
                    {
                        if (IsHidden(player, friend) || IsHidden(friend))
                            continue;
                        shown.Add(friend);
                        DisplayFriend(player, friend, inst.config.Colors.Friends);
                    }
                }
                if (inst.config.UseClans)
                {
                    var clanmates = inst.GetClanmates(player);
                    foreach (var clanmate in clanmates)
                    {
                        if (shown.Contains(clanmate))
                            continue;
                        if (IsHidden(player, clanmate) || IsHidden(clanmate))
                            continue;
                        DisplayFriend(player, clanmate, inst.config.Colors.ClanMates);
                    }
                }
            }
            private bool IsHidden(BasePlayer player, BasePlayer target) => data.Ignore.ContainsKey(player.userID) &&
                data.Ignore[player.userID].Any(p => p.ID == target.userID);
            private bool IsHidden(BasePlayer player) => data.Self.Contains(player.userID);
            private void DisplayFriend(BasePlayer player, BasePlayer friend, string color)
            {
                var colors = inst.config.Colors;
                double distance = Math.Floor(Vector3.Distance(friend.transform.position, player.transform.position));
                if (inst.config.MaxDistance > 0 && distance > inst.config.MaxDistance)
                    return;
                string health = colors.HealthLow;
                if (friend.health >= 33 && 66 > friend.health)
                    health = colors.HealthMiddle;
                if (friend.health >= 66)
                    health = colors.HealthHight;
                string text = inst.config.Format
                    .Replace("{Name}", $"<color={color}>{friend.displayName ?? friend.UserIDString}</color>")
                    .Replace("{Health}", $"<color={health}>{ inst.GetMsg("HP", player, Math.Floor(friend.health))}</color>")
                    .Replace("{Distance}", inst.GetMsg("Distance", player, distance))
                    .Replace("{ActiveItem}", friend.GetActiveItem()?.info.displayName.english ?? inst.GetMsg("Nothing", player));
                DdrawText(player, friend.transform.position + new Vector3(0f, 2f, 0f), text, Color.white, inst.config.UpdateInterval);
            }
        }
        #endregion
    }
}
///////////////////////////////////
                                                   
