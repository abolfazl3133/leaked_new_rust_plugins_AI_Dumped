using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui; 
using ProtoBuf;
using UnityEngine;
using Pool = Facepunch.Pool;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Friends", "https://discord.gg/dNGbxafuJn", "3.0.6")]
    public class Friends : RustPlugin
    {
        #region [DATA&CONFIG]
 
        private Dictionary<ulong, FriendData> friendData = new Dictionary<ulong, FriendData>();
        private Dictionary<ulong, ulong> playerAccept = new Dictionary<ulong, ulong>();
        private static Configs cfg { get; set; }

        private class FriendData
        {
            [JsonProperty("Ник")] public string Name;

            [JsonProperty("Список друзей")]
            public Dictionary<ulong, FriendAcces> friendList = new Dictionary<ulong, FriendAcces>();

            public class FriendAcces
            {
                [JsonProperty("Ник")] public string name;
                [JsonProperty("Урон по человеку")] public bool Damage;

                [JsonProperty("Авторизациия в турелях")]
                public bool Turret;

                [JsonProperty("Авторизациия в дверях")]
                public bool Door;

                [JsonProperty("Авторизациия в пво")] public bool Sam;
                
                [JsonProperty("Авторизациия в шкафу")] public bool bp;
            }
        } 

        private class Configs
        {
            [JsonProperty("Включить сохранение во время сейва карты?")]
            public bool serversave = true;
            [JsonProperty("Включить авто-авторизацию в одинчных замках?")]
            public bool odinlock = true;
            [JsonProperty("Отключить атаку пво на коптер без пилота?")]
            public bool targetPilot = true;
            [JsonProperty("Включить настройку авто авторизации турелей?")]
            public bool Turret;

            [JsonProperty("Включить настройку урона по своим?")]
            public bool Damage;

            [JsonProperty("Включить настройку авто авторизации в дверях?")]
            public bool Door;

            [JsonProperty("Включить настройку авто авторизации в пво?")]
            public bool Sam;
            
            [JsonProperty("Включить настройку авто авторизации в шкафу?")]
            public bool build;
            
            [JsonProperty("Сколько максимум людей может быть в друзьях?")]
            public int MaxFriends;

            [JsonProperty("Урон по человеку(По стандрату у игрока включена?)")]
            public bool SDamage;

            [JsonProperty("Авторизациия в турелях(По стандрату у игрока включена?)")]
            public bool STurret;

            [JsonProperty("Авторизациия в дверях(По стандрату у игрока включена?)")]
            public bool SDoor;

            [JsonProperty("Авторизациия в пво(По стандрату у игрока включена?)")]
            public bool SSam; 
            
            [JsonProperty("Авторизациия в шкафу(По стандрату у игрока включена?)")]
            public bool bp;
            
            [JsonProperty("Время ожидания  ответа на запроса в секнудах")]
            public int otvet;

            [JsonProperty("Вообще включать пво настройку?")]
            public bool SSamOn; 

            public static Configs GetNewConf()
            {
                var newconfig = new Configs();
                newconfig.Damage = true;
                newconfig.Door = true;
                newconfig.build = true;
                newconfig.Turret = true;
                newconfig.Sam = true;
                newconfig.MaxFriends = 3;
                newconfig.SDamage = false;
                newconfig.SDoor = true;
                newconfig.STurret = true;
                newconfig.SSam = true;
                newconfig.SSamOn = true;
                newconfig.otvet = 10;
                return newconfig;
            }
        }

        protected override void LoadDefaultConfig() => cfg = Configs.GetNewConf();
        protected override void SaveConfig() => Config.WriteObject(cfg);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<Configs>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultMessages()
         {
             var ru = new Dictionary<string, string>();
             foreach (var rus in new Dictionary<string, string>()
             {
                 ["SYNTAX"] = "/fmenu - Открыть меню друзей\n/f(riend) add - Добавить в друзья\n/f(riend) remove - Удалить из друзей\n/f(riend) list - Список друзей\n/f(riend) team - Пригласить в тиму всех друзей онлайн\n/f(riend) set - Настройка друзей по отдельности\n/f(riend) setall - Настройка друзей всех сразу",
                 ["NPLAYER"] = "Игрок не найден!",
                 ["CANTADDME"] = "Нельзя добавить себя в друзья!",
                 ["ONFRIENDS"] = "Игрок уже у вас в друзьях!",
                 ["MAXFRIENDSPLAYERS"] = "У игрока максимальное кол-во друзей!",
                 ["MAXFRIENDYOU"] = "У вас максимальное кол-во друзей!",
                 ["HAVEINVITE"] = "Игрок уже имеет запрос в друзья!",
                 ["SENDADD"] = "Вы отправили запрос, ждем ответа!",
                 ["YOUHAVEINVITE"] = "Вам пришел запрос в друзья напишите /f(riend) accept",
                 ["TIMELEFT"] = "Вы не ответили на запрос!",
                 ["HETIMELEFT"] = "Вам не ответили на запрос!",
                 ["DONTHAVE"] = "У вас нет запросов!",
                 ["ADDFRIEND"] = "Успешное добавление в друзья!",
                 ["DENYADD"] = "Отклонение запроса в друзья!",
                 ["PLAYERDHAVE"] = "У тебя нету такого игрока в друзьях!",
                 ["REMOVEFRIEND"] = "Успешное удаление из друзей!",
                 ["LIST"] = "Список пуст!",
                 ["LIST2"] = "Список друзей",
                 ["SYNTAXSET"] = "/f(riend) set damage [Name] - Урон по человеку\n/f(riend) set door [NAME] - Авторизация в дверях для человека\n/f(riend) set turret [NAME] - Авторизация в турелях для человека\n/f(riend) set sam [NAME] - Авторизация в пво для человека",
                 ["SETOFF"] = "Настройка отключена",
                 ["DAMAGEOFF"] = "Урон по игроку {0} выключен!",
                 ["DAMAGEON"] = "Урон по игроку {0} включен!",
                 ["AUTHDOORON"] = "Авторизация в дверях для {0} включена!",
                 ["AUTHDOOROFF"] = "Авторизация в дверях для {0} выключена!",
                 ["AUTHTURRETON"] = "Авторизация в турелях для {0} включена!",
                 ["AUTHTURRETOFF"] = "Авторизация в турелях для {0} выключена!",
                 ["AUTHBUILDON"] = "Авторизация в шкафу для {0} включена!",
                 ["AUTHBUILDOFF"] = "Авторизация в шкафу для {0} выключена!",
                 ["AUTHSAMON"] = "Авторизация в ПВО для {0} включена!",
                 ["AUTHSAMOFF"] = "Авторизация в ПВО для {0} выключена!",
                 ["SYNTAXSETALL"] = "/f(riend) setall damage 0/1 - Урон по всех друзей\n/f(riend) setall door 0/1 - Авторизация в дверях для всех друзей\n/f(riend) setall turret 0/1 - Авторизация в турелях для всех друзей\n/f(riend) setall sam 0/1 - Авторизация в пво для всех друзей",
                 ["DAMAGEOFFALL"] = "Урон по всем друзьям выключен!",
                 ["DAMAGEONALL"] = "Урон по всем друзьям включен!",
                 ["AUTHDOORONALL"] = "Авторизация в дверях для всех друзей включена!",
                 ["AUTHDOOROFFALL"] = "Авторизация в дверях для всех друзей выключена!",
                 ["AUTHBUILDONALL"] = "Авторизация в шкафу для всех друзей включена!",
                 ["AUTHBUILDOFFALL"] = "Авторизация в шкафу для всех друзей выключена!",
                 ["AUTHTURRETONALL"] = "Авторизация в турелях для всех друзей включена!",
                 ["AUTHTURRETOFFALL"] = "Авторизация в турелях для всех друзей выключена!",
                 ["AUTHSAMONALL"] = "Авторизация в ПВО для всех друзей включена!",
                 ["AUTHSAMOFFALL"] = "Авторизация в ПВО для всех друзей выключена!",
                 ["SENDINVITETEAM"] = "Приглашение отправлено: ",
                 ["SENDINVITE"] = "Вам пришло приглашение в команду от",
                 ["DAMAGE"] = "Нельзя аттаковать {0} это ваш друг!",
             }) ru.Add(rus.Key, rus.Value);
             lang.RegisterMessages(ru, this, "ru");
             lang.RegisterMessages(ru, this, "en");
         }
        #endregion

        #region [Func]

        private string PlugName = "<color=red>[SOFRIEND]</color> ";

        [ChatCommand("f")]
        private void FriendCmd(BasePlayer player, string command, string[] arg)
        {
            ulong ss;
            FriendData player1; 
            FriendData targetPlayer;
            if (!friendData.TryGetValue(player.userID, out player1)) return;
            if (arg.Length < 1)
            {
                SendReply(player,
                    $"<size=22>{PlugName}</size>\n{lang.GetMessage("SYNTAX", this, player.UserIDString)}");
                return;
            }

            switch (arg[0])
            {
                case "add":
                    if (arg.Length < 2)
                    {
                        SendReply(player, $"{PlugName}/f(riend) add [NAME or SteamID]");
                        return;
                    }

                    var argLists = arg.ToList();
                    argLists.RemoveRange(0, 1);
                    var name = string.Join(" ", argLists.ToArray()).ToLower();
                    var target = BasePlayer.Find(name);
                    if (target == null || !friendData.TryGetValue(target.userID, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (target.userID == player.userID)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("CANTADDME", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (player1.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDYOU", this, player.UserIDString)}");
                        return;
                    }
                     
                    if (player1.friendList.ContainsKey(target.userID))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("ONFRIENDS", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (targetPlayer.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDSPLAYERS", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (playerAccept.ContainsKey(target.userID))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("HAVEINVITE", this, player.UserIDString)}");
                        return;
                    }

                    playerAccept.Add(target.userID, player.userID);
                    SendReply(player, $"{PlugName}{lang.GetMessage("SENDADD", this, player.UserIDString)}");
                    SendReply(target, $"{PlugName}{lang.GetMessage("YOUHAVEINVITE", this, target.UserIDString)}");
                    InivteStart(player, target);
                    ss = target.userID;
                    timer.Once(cfg.otvet, () =>
                    {
                        if (!playerAccept.ContainsKey(target.userID) || !playerAccept.ContainsValue(player.userID)) return;
                        if (target != null)
                        {
                            CuiHelper.DestroyUi(target, LayerInvite);
                            SendReply(target, $"{PlugName}{lang.GetMessage("TIMELEFT", this, target.UserIDString)}");
                        }
                        
                        SendReply(player, $"{PlugName}{lang.GetMessage("HETIMELEFT", this, player.UserIDString)}");
                        playerAccept.Remove(ss);
                    });
                    break;
                case "accept":

                    if (!playerAccept.TryGetValue(player.userID, out ss))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("DONTHAVE", this, player.UserIDString)}");
                        return;
                    }

                    if (!friendData.TryGetValue(ss, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    if (player1.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDYOU", this, player.UserIDString)}");
                        return;
                    }

                    if (targetPlayer.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDSPLAYERS", this, player.UserIDString)}!");
                        return;
                    }

                    target = BasePlayer.FindByID(ss);
                    player1.friendList.Add(target.userID,
                        new FriendData.FriendAcces()
                        {
                            name = target.displayName, Damage = cfg.SDamage, Door = cfg.SDoor, Turret = cfg.STurret,
                            Sam = cfg.SSam, bp = cfg.bp
                        });
                    targetPlayer.friendList.Add(player.userID,
                        new FriendData.FriendAcces()
                        {
                            name = player.displayName, Damage = cfg.SDamage, Door = cfg.SDoor, Turret = cfg.STurret,
                            Sam = cfg.SSam, bp = cfg.bp
                        });
                    SendReply(player, $"{PlugName}{lang.GetMessage("ADDFRIEND", this, player.UserIDString)}");
                    playerAccept.Remove(player.userID);
                    SendReply(target, $"{PlugName}{lang.GetMessage("ADDFRIEND", this, target.UserIDString)}");
                    if(cfg.bp) AuthBuild(target, player.userID);
                    CuiHelper.DestroyUi(player, LayerInvite);
                    break;
                case "deny":
                    if (!playerAccept.TryGetValue(player.userID, out ss))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("DONTHAVE", this, player.UserIDString)}");
                        return;
                    }

                    if (!friendData.TryGetValue(ss, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    target = BasePlayer.FindByID(ss);
                    playerAccept.Remove(player.userID);
                    SendReply(player, $"{PlugName}{lang.GetMessage("DENYADD", this, player.UserIDString)}");
                    SendReply(target, $"{PlugName}{lang.GetMessage("DENYADD", this, target.UserIDString)}");
                    CuiHelper.DestroyUi(player, LayerInvite);
                    break;
                case "remove":
                    if (arg.Length < 2)
                    {
                        SendReply(player, $"{PlugName}/f(riend) remove [NAME or SteamID]");
                        return;
                    }

                    argLists = arg.ToList();
                    argLists.RemoveRange(0, 1);
                    name = string.Join(" ", argLists.ToArray()).ToLower();
                    ulong tt;
                    if (ulong.TryParse(arg[1], out tt)) { }else tt = player1.friendList.FirstOrDefault(p => p.Value.name.ToLower().Contains(name)).Key;

                    if (!player1.friendList.ContainsKey(tt))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("PLAYERDHAVE", this, player.UserIDString)}");
                        return;
                    }

                    if (!friendData.TryGetValue(tt, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    player1.friendList.Remove(tt);
                    targetPlayer.friendList.Remove(player.userID);
                    SendReply(player, $"{PlugName}{lang.GetMessage("REMOVEFRIEND", this, player.UserIDString)}");
                    target = tt.IsSteamId() ? BasePlayer.FindByID(tt) : BasePlayer.Find(arg[1].ToLower());
                    if (target != null)
                        SendReply(target, $"{PlugName}{lang.GetMessage("REMOVEFRIEND", this, player.UserIDString)}");
                    break;
                case "list":
                    if (player1.friendList.Count < 1)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("LIST", this, player.UserIDString)}");
                        return;
                    }
                    
                    var argList = player1.friendList;
                    var friendlist = $"{PlugName}{lang.GetMessage("LIST2", this, player.UserIDString)}\n";
                    foreach (var keyValuePair in argList)
                        friendlist += keyValuePair.Value.name + $"({keyValuePair.Key})\n";
                    SendReply(player, friendlist);
                    break;
                case "set":
                    if (arg.Length < 3)
                    {
                        SendReply(player, $"<size=22>{PlugName}</size>\n{lang.GetMessage("SYNTAXSET", this, player.UserIDString)}");
                        return;
                    }

                    argLists = arg.ToList();
                    argLists.RemoveRange(0, 2);
                    name = string.Join(" ", argLists.ToArray()).ToLower();
                    FriendData.FriendAcces access;
                    if (ulong.TryParse(arg[2], out ss)) {}else ss = player1.friendList.FirstOrDefault(p => p.Value.name.ToLower().Contains(name)).Key;

                    if (!player1.friendList.TryGetValue(ss, out access))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    switch (arg[1])
                    {
                        case "damage":
                            if (!cfg.Damage)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Damage)
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("DAMAGEOFF", this, player.UserIDString), access.name)}");
                                access.Damage = false;
                            }
                            else
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("DAMAGEON", this, player.UserIDString), access.name)}");
                                access.Damage = true;
                            }

                            break;
                        case "build":
                            if (!cfg.build)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.bp)
                            {
                                SendReply(player,
                                    $"{PlugName}{String.Format(lang.GetMessage("AUTHBUILDOFF", this, player.UserIDString), access.name)}");
                                access.bp = false;
                            }
                            else
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("AUTHBUILDON", this, player.UserIDString), access.name)}");
                                access.bp = true;
                                AuthBuild(player, ss);
                            }

                            break;  
                        case "door":
                            if (!cfg.Door)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Door)
                            {
                                SendReply(player,
                                    $"{PlugName}{String.Format(lang.GetMessage("AUTHDOOROFF", this, player.UserIDString), access.name)}");
                                access.Door = false;
                            }
                            else
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("AUTHDOORON", this, player.UserIDString), access.name)}");
                                access.Door = true;
                            }

                            break;
                        case "turret":
                            if (!cfg.Turret)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Turret)
                            {
                                SendReply(player,
                                    $"{PlugName}{String.Format(lang.GetMessage("AUTHTURRETOFF", this, player.UserIDString), access.name)}");
                                access.Turret = false;
                            }
                            else
                            {
                                SendReply(player,
                                    $"{PlugName}{String.Format(lang.GetMessage("AUTHTURRETON", this, player.UserIDString), access.name)}");
                                access.Turret = true;
                            }

                            break;
                        case "sam":
                            if (!cfg.SSamOn) return;
                            if (!cfg.Sam)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Sam)
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("AUTHSAMOFF", this, player.UserIDString), access.name)}");
                                access.Sam = false;
                            }
                            else
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("AUTHSAMON", this, player.UserIDString), access.name)}");
                                access.Sam = true;
                            }

                            break;
                    }

                    break;
                case "setall":
                    if (arg.Length < 3)
                    {
                        SendReply(player,
                            $"<size=22>{PlugName}</size>\n{lang.GetMessage("SYNTAXSETALL", this, player.UserIDString)}");
                        return;
                    }

                    switch (arg[1])
                    {
                        case "door":
                            if (!cfg.Door)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Door = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHDOORONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Door = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHDOOROFFALL", this, player.UserIDString)}");
                            }

                            break;
                        
                        case "damage":
                            if (!cfg.Damage)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Damage = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("DAMAGEONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Damage = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("DAMAGEOFFALL", this, player.UserIDString)}");
                            }

                            break;
                        case "build":
                            if (!cfg.Turret)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Turret = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHBUILDONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Turret = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHBUILDOFFALL", this, player.UserIDString)}");
                            }

                            break;
                        case "turret":
                            if (!cfg.Turret)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Turret = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHTURRETONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Turret = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHTURRETOFFALL", this, player.UserIDString)}");
                            }

                            break;
                        case "sam":
                            if (!cfg.SSamOn) return;
                            if (!cfg.Sam)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Sam = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHSAMONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Sam = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHSAMOFFALL", this, player.UserIDString)}");
                            }

                            break;
                    }
 
                    break;
                case "team":
                    var team = player.Team; 
                    if (team == null)
                    {
                        team = RelationshipManager.ServerInstance.CreateTeam();
                        team.AddPlayer(player);
                        team.SetTeamLeader(player.userID);
                    }

                    var text = $"{PlugName}{lang.GetMessage("SENDINVITETEAM", this, player.UserIDString)}";
                    foreach (var ts in player1.friendList)
                    {
                        target = BasePlayer.Find(ts.Key.ToString());
                        if (target != null)
                        {
                            if (target.Team == null)
                            {
                                team.SendInvite(target);
                                target.SendNetworkUpdate();
                                text += $"{target.displayName}[{target.userID}]\n";
                                SendReply(target,
                                    $"{PlugName}{lang.GetMessage("SENDINVITE", this, player.UserIDString)} {player.displayName}[{player.userID}]");
                            }
                        }
                    }

                    SendReply(player, text);
                    break;
            }
        }

        [ConsoleCommand("friendui2")]
        private void FriendConsole(ConsoleSystem.Arg arg)
        {
            if(arg.Args == null || arg.Args.Length < 1) return;
            FriendCmd(arg.Player(), "friend", arg.Args);
            if (arg.Args[0] == "set")
            {
                SettingInit(arg.Player(), ulong.Parse(arg.Args[2]), arg.Args[3]);
            }
            if (arg.Args[0] == "remove")
            {
                StartUi(arg.Player());
            }
        }

        [ChatCommand("friend")]
        private void FriendCmd2(BasePlayer player, string command, string[] arg) => FriendCmd(player, command, arg);

        #endregion

        #region [Hooks]

        private void OnEntitySpawned(BuildingPrivlidge entity)
        {
            FriendData fData;
            if(!friendData.TryGetValue(entity.OwnerID, out fData)) return;
            foreach (var ids in fData.friendList.Where(p => p.Value.bp == true))
            {
                entity.authorizedPlayers.Add(new PlayerNameID()
                {
                    ShouldPool = true,
                    userid = ids.Key, 
                    username = ids.Value.name
                });
            }
        }

        private List<ulong> hitPlayer = new List<ulong>();
        
        [PluginReference] private Plugin TruePVE;

        private object CanEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            FriendData player1;
            var targetplayer = entity as BasePlayer;
            var attackerplayer = info.Initiator as BasePlayer;
            if (attackerplayer == null || targetplayer == null) return null;
            if (!friendData.TryGetValue(attackerplayer.userID, out player1)) return null;
            FriendData.FriendAcces ss;
            if (!player1.friendList.TryGetValue(targetplayer.userID, out ss)) return null;
            if (ss.Damage) return null;
            if (hitPlayer.Contains(attackerplayer.userID)) return false;
            hitPlayer.Add(attackerplayer.userID);
            timer.Once(5f, () =>
            {
                if (hitPlayer.Contains(attackerplayer.userID))
                    hitPlayer.Remove(attackerplayer.userID);
            });
            SendReply(attackerplayer, string.Format(lang.GetMessage("DAMAGE",this, attackerplayer.UserIDString),targetplayer.displayName ));
            return false;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (TruePVE != null) return null;
            return CanEntityTakeDamage(entity, info);
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            if (entity == null || turret == null) return null;
            FriendData targetPlayer;
            var targetplayer = entity as BasePlayer;
            if (targetplayer == null) return null;
            if (!friendData.TryGetValue(turret.OwnerID, out targetPlayer)) return null;
            FriendData.FriendAcces ss;
            var owner = turret.authorizedPlayers.ToList().Exists(p => p.userid == turret.OwnerID);
            if (!owner) return null;
            if (!targetPlayer.friendList.TryGetValue(targetplayer.userID, out ss)) return null;
            if (!ss.Turret) return null;
            return false;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null) return null;
            FriendData targetPlayer2;
            if (baseLock.ShortPrefabName == "lock.key" && !cfg.odinlock) return null;
            if (!friendData.TryGetValue(baseLock.OwnerID, out targetPlayer2)) return null;
            FriendData.FriendAcces ss;
            if (!targetPlayer2.friendList.TryGetValue(player.userID, out ss)) return null;
            if (!ss.Door) return null;
            return true;
        }
        private bool TargetPilot(SamSite entity, BaseCombatEntity target)
        {  
            var targetPlayer = (target as BaseVehicle)?.GetDriver();
            return targetPlayer != null; 
        }
        private object OnSamSiteTarget(SamSite entity, BaseCombatEntity target)
        {
            if (cfg.targetPilot && !TargetPilot(entity, target)) return false;
            if (!cfg.SSamOn) return null; 
            if (entity == null || target == null) return null;
            FriendData targetPlayer;
            var targetpcopter = target as PlayerHelicopter;
            var targetpcopterBig = target as ScrapTransportHelicopter;
            if (targetpcopter != null || targetpcopterBig != null)
            {
                var build = entity.GetBuildingPrivilege();
                if (build == null) return null;
                if (!build.authorizedPlayers.ToList().Exists(p => p.userid == entity.OwnerID)) return null;
                BasePlayer targePlayer = null;
                if(targetpcopter != null)targePlayer = targetpcopter.mountPoints[0].mountable._mounted;
                if(targetpcopterBig != null)targePlayer = targetpcopterBig.mountPoints[0].mountable._mounted;
                if (targePlayer == null) return false;
                if (entity.OwnerID == targePlayer.userID) return false;
                if (!friendData.TryGetValue(entity.OwnerID, out targetPlayer)) return null;
                FriendData.FriendAcces ss;
                if (!targetPlayer.friendList.TryGetValue(targePlayer.userID, out ss)) return null;
                if (!ss.Sam) return null; 
            }
            else 
            {
                return null;
            }
            return false;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            FriendData t;
            if (friendData.TryGetValue(player.userID, out t)) return;
            friendData.Add(player.userID, new FriendData() {Name = player.displayName, friendList = { }});
        }

        private void OnServerInitialized()
        {
           permission.RegisterPermission("friends.checkplayer", this);
            ServerConsole.PrintColoured( ConsoleColor.Blue, (object) $"{Name} [{Version}] ",(object) ConsoleColor.Blue, (object) "B", (object) ConsoleColor.Cyan, (object) "Y ", (object) ConsoleColor.Green, (object) "L", (object) ConsoleColor.Magenta, (object) "A", (object) ConsoleColor.Red, (object) "G", (object) ConsoleColor.Yellow, (object) "Z", (object) ConsoleColor.Cyan, (object) "Y", (object) ConsoleColor.DarkCyan, (object) "A");
            if (ImageLibrary == null)
            {
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            if(!cfg.serversave)
                Unsubscribe("OnServerSave");
            friendData =
                Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, FriendData>>("Friends/FriendData");
            foreach (var basePlayer in BasePlayer.activePlayerList)
                OnPlayerConnected(basePlayer);
        }
        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Friends/FriendData", friendData);
            Puts("Произошло сохранение даты!");
        }
        private void Unload() 
        {
            Interface.Oxide.DataFileSystem.WriteObject("Friends/FriendData", friendData);
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, LayerInvite);
                CuiHelper.DestroyUi(basePlayer, Layer);
            }
        }

        #endregion

        #region [UI]

        private static string Layer = "UISoFriends";
        private string Hud = "Hud";
        private string Overlay = "Overlay";
        private string regular = "robotocondensed-regular.ttf";
        private static string Sharp = "assets/content/ui/ui.background.tile.psd";
        private static string Blur = "assets/content/ui/uibackgroundblur.mat";
        private static string radial = "assets/content/ui/ui.background.transparent.radial.psd";
        private CuiPanel Fon = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
            Image = {ImageType = UnityEngine.UI.Image.Type.Filled,
                Png = "assets/standard assets/effects/imageeffects/textures/noise.png",
                Sprite = "assets/content/ui/ui.background.transparent.radial.psd",
                Color = HexToRustFormat("#303038F6"),
                Material = "assets/icons/greyout.mat"}
        };

        private CuiPanel MainFon = new CuiPanel()
        {
            RectTransform =
                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-1920 -1080", OffsetMax = "1920 1080"},
            CursorEnabled = true,
            Image = {Color = "0.24978750 0.2312312 0.312312312 0"}
        };

        private CuiPanel _searchPanel = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0.3364583 0.3573457", AnchorMax = "0.6644097 0.6095061"},
            Image = {Color = "0 0 0 0.42"}
        };
        private CuiElement LableText = new CuiElement()
        {
            Parent = Layer + "off",
            Components =
            {
                new CuiTextComponent(){Text = "СИСТЕМА ДРУЗЕЙ", Color = "0.8 0.8 0.8 0.86", FontSize = 30, Align = TextAnchor.MiddleLeft},
                new CuiRectTransformComponent(){AnchorMin = "0.3442708 0.6361111", AnchorMax = "0.41875 0.6549382"}
            }
        };
        private CuiButton _close = new CuiButton()
        {
            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
            Button = {Close = Layer, Color = "0.64 0.64 0.64 0"},
            Text = {Text = ""}
        };
        private string LayerInvite = "FriendsAcceptLayer";
        private void InivteStart(BasePlayer player, BasePlayer playerName)
        {
            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "-100 90",
                    OffsetMax = "80 130"
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, Overlay, LayerInvite);
            cont.Add(new CuiElement()
            {
                Parent = LayerInvite,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"ЗАПРОС В ДРУЗЬЯ ОТ {player.displayName}",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat("#FF8C00")
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-150 1",
                        OffsetMax = "150 29"
                    }
                }
            });

            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "-34 1",
                    OffsetMax = "-5 30"
                },
                Text =
                {
                    Text = "",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = HexToRustFormat("#01cdd4")
                },
                Button =
                {
                    
                    Close = LayerInvite,
                    Sprite = "assets/icons/vote_up.png",
                    Color = HexToRustFormat("#8ab644"),
                    Command = "friendui2 accept"
                }
            }, LayerInvite);
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "5 1",
                    OffsetMax = "34 30"
                },
                Text =
                {
                    Text = "",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = HexToRustFormat("#ee0078")
                },
                Button =
                {
                    Close = LayerInvite,
                    Sprite = "assets/icons/vote_down.png",
                    Color = HexToRustFormat("#8c472e"),
                    Command = "friendui2 deny"
                }
            }, LayerInvite);
            CuiHelper.AddUi(playerName, cont);
            Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", playerName, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, playerName.Connection);
        }

       
        [ChatCommand("ff")]  
        void FfCommand(BasePlayer player, string command, string[] arg)
        {
            FriendData player1; 
            if (!friendData.TryGetValue(player.userID, out player1)) return;
            if(arg.Length != 1) return;
            switch (arg[0])
            {
                case "0":
                    if (!cfg.Damage)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                        return;
                    }
                    foreach (var friends in player1.friendList)
                    {
                        friends.Value.Damage = false;
                    }
                    SendReply(player, $"{PlugName}{lang.GetMessage("DAMAGEOFFALL", this, player.UserIDString)}");
                    break;
                case "1":
                    foreach (var friends in player1.friendList)
                    {
                        friends.Value.Damage = true;
                    }
                    SendReply(player, $"{PlugName}{lang.GetMessage("DAMAGEONALL", this, player.UserIDString)}");
                    break;
            }
        }
        [ChatCommand("fmenu")]
        private void StartUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var cont = new CuiElementContainer();
            cont.Add(Fon, Overlay, Layer);
            cont.Add(MainFon, Layer, Layer + "off");
            cont.Add(LableText);
            cont.Add(_close, Layer + "off");
            CuiHelper.AddUi(player, cont);
            FriendsInit(player, 1);
        }
        [ConsoleCommand("checkfriends")]
        void CheckPlayer(ConsoleSystem.Arg arg)
        {
            ulong steamId;
            if(arg == null || arg.Args == null || arg.Args.Length !=1 || !ulong.TryParse(arg.Args[0], out steamId)) return;
            if(arg.Player() == null)
            {
                ServerConsole.PrintColoured( ConsoleColor.Yellow, (object) $"{Name} [{Version}]\n",(object) ConsoleColor.White, (object) $"{CheckFriends(steamId)}");
                return;
            }
            var admin = arg.Player();
            if (!permission.UserHasPermission(admin.UserIDString, "friends.checkplayer")) return;
            SendReply(admin, $"{CheckFriends(steamId)}");
        }
        string CheckFriends(ulong playerId)
        {
            var checkPlayer = BasePlayer.FindByID(playerId);
            var text = checkPlayer == null ? $"Информация об {playerId}\n" : $"Информация об {checkPlayer.displayName}[{playerId}]\n";
            if (friendData.ContainsKey(playerId))
            {
                if (friendData[playerId].friendList.Count > 0)
                { 
                    var i = 1;
                    text += "Список друзей:\n";
                    foreach (var friend in GetFriends(playerId))
                    {
                        var checkFriend = BasePlayer.FindByID(friend);
                        text += checkFriend == null ? $"{i}. {friend}\n" : $"{i}. {checkFriend.displayName}[{friend}]\n";
                        i++;
                    }
                    return text;
                }
                return text + "Нет друзей";
            }
            return text + "Нет в базе";
        }
        [ConsoleCommand("friendui")]
        private void FriendUI(ConsoleSystem.Arg arg)
        {
            var targetPlayer = arg?.Player();
            if (targetPlayer == null) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                StartUi(arg.Player());
                return;
            }
            switch (arg.Args[0])
            {
                case "page":
                    if (arg.Args[1].ToInt() < 1) return;
                    FriendsInit(targetPlayer, arg.Args[1].ToInt());
                    break;
                case "setting":
                    SettingInit(targetPlayer, ulong.Parse(arg.Args[1]), arg.Args[2]);
                    break;
            }
        }
        [PluginReference] private Plugin ImageLibrary;

        public string GetImage(string shortname, ulong skin = 0)
        {
            return (string) ImageLibrary.Call("GetImage", shortname, skin);
        }

        private void SettingInit(BasePlayer player, ulong steamdIdTarget, string b)
        {
            FriendData.FriendAcces access;
            FriendData target;
            string panel = Layer + "f" + b;
            if (!friendData.TryGetValue(player.userID, out target)) return;
            if (!target.friendList.TryGetValue(steamdIdTarget, out access)) return;
            CuiHelper.DestroyUi(player, panel);
            var cont = new CuiElementContainer();
            cont.Add(new CuiElement()
            {
                Parent = Layer + "-Search",
                Name = panel,
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin =
                            $"{0.0442401 + b.ToInt() * 0.132 - Math.Floor((double) b.ToInt() / 7) * 7* 0.132} {0.925129 - Math.Floor((double) b.ToInt() / 7) * 0.07}",
                        AnchorMax =
                            $"{0.168343 + b.ToInt() * 0.132 - Math.Floor((double) b.ToInt() / 7) * 7 * 0.132} {0.9854072- Math.Floor((double) b.ToInt() / 7) * 0.07}"
                    }
                }
            });
            if(b.ToInt() <= 47)
            {
                cont.Add(new CuiPanel()
                {
                    RectTransform = {AnchorMin = "0.006101906 -6.051513", AnchorMax = "1 1"},
                    Image =
                    {
                        Color = "0 0 0 0.97"
                    }
                }, panel, Layer + "Set");
            }
            else
            {
                cont.Add(new CuiPanel()
                {
                    RectTransform = {AnchorMin = "0.006101906 0", AnchorMax = "1 7.051513"},
                    Image =
                    {
                        Color = "0 0 0 0.95"
                    }
                }, panel, Layer + "Set");
            }
            cont.Add(new CuiElement()
            {
                Parent = Layer + "Set",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"НАСТРОЙКА {target.Name}", Align = TextAnchor.MiddleCenter, Font = regular, FontSize = 12, Color = HexToRustFormat("#52eb80")
                    },
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.02648985 0.9141861", AnchorMax = "0.4420606 0.9894828"},
                }
            });
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.86 0.92", AnchorMax = "0.9484982 0.9756707"},
                Button =
                {
                    Color = HexToRustFormat("#9fb5b7"), Close = panel, Sprite = "assets/icons/close.png"
                },
                Text =
                {
                    Text = "", Align = TextAnchor.MiddleCenter, FontSize = 12,
                    Font = "robotocondensed-regular.ttf"
                }
            }, Layer + "Set");
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.02648985 0.01144091", AnchorMax = "0.9699578 0.09286223"},
                Button =
                {
                    Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 remove {steamdIdTarget}",
                },
                Text =
                {
                    Text = "Удалить из друзей", Align = TextAnchor.MiddleCenter, FontSize = 10, Color = HexToRustFormat("#8c472e"),
                    Font = "robotocondensed-regular.ttf"
                }
            }, Layer + "Set");
            if (access.Damage)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.7407911", AnchorMax = "0.9699578 0.8606074"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set damage {steamdIdTarget} {b}"},
                    Text = {Text = "     Урон по игроку", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Damage");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Damage",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_up.png",
                            Color = HexToRustFormat("#8ab644")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.7407911", AnchorMax = "0.9699578 0.8606074"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set damage {steamdIdTarget} {b}"},
                    Text = {Text = "     Урон по игроку", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Damage");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Damage",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_down.png",
                            Color = HexToRustFormat("#8c472e")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            }

            if (access.Door)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.603502", AnchorMax = "0.9699578 0.7233183"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set door {steamdIdTarget} {b}"},
                    Text = {Text = "     Доступ к дверям", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Door");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Door",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_up.png",
                            Color = HexToRustFormat("#8ab644")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.603502", AnchorMax = "0.9699578 0.7233183"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set door {steamdIdTarget} {b}"},
                    Text = {Text = "     Доступ к дверям", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Door");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Door",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_down.png",
                            Color = HexToRustFormat("#8c472e")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            }

            if (access.Turret)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.466213", AnchorMax = "0.9699578 0.5860293"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set turret {steamdIdTarget} {b}"},
                    Text = {Text = "     Доступ к турелям", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Turret");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Turret",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_up.png",
                            Color = HexToRustFormat("#8ab644")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            }
            else 
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.466213", AnchorMax = "0.9699578 0.5860293"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set turret {steamdIdTarget} {b}"},
                    Text = {Text = "     Доступ к турелям", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Turret");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Turret",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_down.png",
                            Color = HexToRustFormat("#8c472e")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            } 
            if (access.bp)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.3289239", AnchorMax = "0.9699578 0.4487402"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set build {steamdIdTarget} {b}"},
                    Text = {Text = "     Доступ к шкафу", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Build");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Build",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_up.png",
                            Color = HexToRustFormat("#8ab644")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.02648985 0.3289239", AnchorMax = "0.9699578 0.4487402"},
                    Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set build {steamdIdTarget} {b}"},
                    Text = {Text = "     Доступ к шкафу", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + "Set", Layer + "Set" + "Build");
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set" + "Build",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Sprite = "assets/icons/vote_down.png",
                            Color = HexToRustFormat("#8c472e")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7990546 0.1258519",
                            AnchorMax = "0.9492701 0.8832379"
                        }
                    }
                });
            }
            if (cfg.SSamOn)
            {
                if (access.Sam)
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = "0.02648985 0.1916349", AnchorMax = "0.9699578 0.3114512"},
                        Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set sam {steamdIdTarget} {b}"},
                        Text = {Text = "     Доступ к пво", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                    }, Layer + "Set", Layer + "Set" + "Sam");
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Set" + "Sam",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Sprite = "assets/icons/vote_up.png",
                                Color = HexToRustFormat("#8ab644")
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0.7990546 0.1258519",
                                AnchorMax = "0.9492701 0.8832379"
                            }
                        }
                    });
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = "0.02648985 0.1916349", AnchorMax = "0.9699578 0.3114512"},
                        Button = {Color = "0.64 0.64 0.64 0.24", Command = $"friendui2 set sam {steamdIdTarget} {b}"},
                        Text = {Text = "     Доступ к пво", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10}
                    }, Layer + "Set", Layer + "Set" + "Sam");
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Set" + "Sam",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Sprite = "assets/icons/vote_down.png",
                                Color = HexToRustFormat("#8c472e")
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0.7990546 0.1258519",
                                AnchorMax = "0.9492701 0.8832379"
                            }
                        }
                    });
                }
            }

            CuiHelper.AddUi(player, cont);
        }

        private void AuthBuild(BasePlayer player, ulong friendId)
        {
            var friend = friendData[player.userID].friendList[friendId];
            foreach (BaseNetworkable entity in BaseNetworkable.serverEntities)
            {
                var bp = entity.GetComponent<BuildingPrivlidge>();
                if(bp == null) continue;
                if (bp.authorizedPlayers.ToList().Exists(p => p.userid == player.userID))
                {
                    if(bp.authorizedPlayers.ToList().Exists(p => p.userid == friendId)) continue;
                    bp.authorizedPlayers.Add(new PlayerNameID()
                    {
                        userid = friendId,
                        username = friend.name,
                        ShouldPool = true
                    });
                    bp.SendNetworkUpdate();
                }
            }
        }
        private void FriendsInit(BasePlayer player, int page)
        { 
            CuiHelper.DestroyUi(player, Layer + "-Search");
            var cont = new CuiElementContainer();
            cont.Add(_searchPanel, Layer + "off", Layer + "-Search");
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.9615819 0",
                    AnchorMax = "0.9979069 1"
                },
                Text =
                {
                    Text = "»", 
                    Align = TextAnchor.MiddleCenter, 
                    FontSize = 30,
                    Color = "0.8 0.8 0.8 0.86"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"friendui page {page + 1}"
                }
                
            }, Layer + "-Search");
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.000733387 0",
                    AnchorMax = "0.03705658 1"
                },
                Text =
                {
                    Text = "«", 
                    Align = TextAnchor.MiddleCenter, 
                    FontSize = 30,
                    Color = "0.8 0.8 0.8 0.86"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"friendui page {page -1}"
                }
                
            }, Layer + "-Search");
            var flist = GetFriends(player.userID).Select(@ulong => BasePlayer.FindByID(@ulong)).Where(bp => bp != null).ToList().OrderBy(f => f.displayName);
            var playerList = flist.ToDictionary(basePlayer => basePlayer, basePlayer => true);
            foreach (var basePlayer in BasePlayer.activePlayerList.OrderBy(s=> s.displayName).Where(p => !playerList.ContainsKey(p) && p.displayName != player.displayName))
            {
                playerList.Add(basePlayer, false);
            }
            // for (int i = 0; i < 100; i++)
            // {
            //     var test = new BasePlayer();
            //     test.displayName = "LAGZYA-TESING Bot";
            //     test.userID = ulong.Parse(Random.Range(0, 12157657200).ToString());
            //     if (i < 10) playerList.Add(test, false);
            //     else playerList.Add(test, false);
            // }
            foreach (var sellItem in playerList.Select((i, t) => new {A = i, B = t -(page - 1) * 98}).Skip((page - 1) * 98).Take(98))
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Search",
                    Name = Layer + "-Search" + ".Player" + sellItem.B,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0 0 0 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin =
                                $"{0.0442401 + sellItem.B * 0.132 - Math.Floor((double) sellItem.B / 7) * 7* 0.132} {0.925129 - Math.Floor((double) sellItem.B / 7) * 0.07}",
                            AnchorMax =
                                $"{0.168343 + sellItem.B * 0.132 - Math.Floor((double) sellItem.B / 7) * 7 * 0.132} {0.9854072- Math.Floor((double) sellItem.B / 7) * 0.07}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Search" + ".Player" + sellItem.B,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage(sellItem.A.Key.UserIDString)
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.0121875 0.2109886",
                            AnchorMax = "0.1382471 0.80559"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Search" + ".Player" + sellItem.B,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                           Text = $"{sellItem.A.Key.displayName}",
                           Align = TextAnchor.MiddleLeft,
                           Color = sellItem.A.Value ? HexToRustFormat("#52eb80") : "0.8 0.8 0.8 0.86",
                           FontSize = 12
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.1552933 0.4009242",
                            AnchorMax = "0.9920615 0.8569153"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Search" + ".Player" + sellItem.B,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"{sellItem.A.Key.userID}",
                            FontSize = 6,
                            Align = TextAnchor.LowerLeft,
                            Font = regular,
                            Color = "0.8 0.8 0.8 0.86"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.1552933 0.2509242",
                            AnchorMax = "0.9920615 0.455869153"
                        }
                    }
                });
                if(sellItem.A.Value)
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.87 0.40",
                            AnchorMax = "0.95 0.76"
                        },
                        Text = {Text = ""},
                        Button =
                        {
                            Color = HexToRustFormat("#5c80ba"),
                            Sprite = "assets/icons/gear.png",
                            Command =
                                $"friendui setting {sellItem.A.Key.userID} {sellItem.B}"
                        }
                    }, Layer + "-Search" + ".Player" + sellItem.B);
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.87 0.40",
                            AnchorMax = "0.95 0.76"
                        },
                        Text = {Text = ""},
                        Button =
                        { 
                            Color = HexToRustFormat("#8ab644"),
                            Sprite = "assets/icons/add.png",
                            Close = Layer,
                            
                            Command = $"friendui2 add {sellItem.A.Key.userID}"
                        }
                    }, Layer + "-Search" + ".Player" + sellItem.B);
                }
            }
            CuiHelper.AddUi(player, cont);
        }

        #endregion

        #region [Help]

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion 
        #region API
        private bool HasFriend(string playerId, string friendId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(friendId))
                return false;
            ulong pId =0;
            ulong fId = 0;
            if (!ulong.TryParse(playerId, out pId) || !ulong.TryParse(friendId, out fId)) return false;
            return HasFriend(pId, fId);
        }
        private bool HasFriend(ulong playerId, ulong friendId)
        {
            FriendData playerData;
            if (!friendData.TryGetValue(playerId, out playerData)) return false;
            return playerData.friendList.ContainsKey(friendId);
        }
        private bool AreFriends(string playerId, string friendId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(friendId))
                return false;
            ulong pId =0;
            ulong fId = 0;
            if (!ulong.TryParse(playerId, out pId) || !ulong.TryParse(friendId, out fId)) return false;
            return AreFriends(pId, fId);
        }
        private bool AreFriends(ulong playerId, ulong friendId)
        {
            FriendData playerData;
            FriendData playerData2;
            if (!friendData.TryGetValue(playerId, out playerData) || !friendData.TryGetValue(friendId, out playerData2)) return false;
            return playerData.friendList.ContainsKey(friendId) && playerData2.friendList.ContainsKey(playerId);;
        }
        private bool AddFriend(ulong playerId, ulong friendId)
        {
            FriendData playerData;
            FriendData playerData2;
            if (!friendData.TryGetValue(playerId, out playerData) || !friendData.TryGetValue(friendId, out playerData2)) return false;
            if (playerData.friendList.ContainsKey(friendId)) return false;
            return playerData.friendList.TryAdd(friendId, new FriendData.FriendAcces()
            {
                name = BasePlayer.FindByID(friendId) ? BasePlayer.FindByID(friendId).displayName : "НЕИЗВЕСТНЫЙ",
                Damage = cfg.SDamage, Door = cfg.SDoor, Turret = cfg.STurret,
                Sam = cfg.SSam
            }) && playerData2.friendList.TryAdd(playerId, new FriendData.FriendAcces()
            {
                name = BasePlayer.FindByID(playerId) ? BasePlayer.FindByID(playerId).displayName : "НЕИЗВЕСТНЫЙ",
                Damage = cfg.SDamage, Door = cfg.SDoor, Turret = cfg.STurret,
                Sam = cfg.SSam
            });
        }
        private bool RemoveFriend(ulong playerId, ulong friendId)
        {
            FriendData playerData;
            if (!friendData.TryGetValue(playerId, out playerData)) return false;
            if (!playerData.friendList.ContainsKey(friendId)) return false;
            return playerData.friendList.Remove(friendId);
        }
        private bool IsFriend(string playerId, string friendId)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(friendId))
                return false;
            ulong pId =0;
            ulong fId = 0;
            if (!ulong.TryParse(playerId, out pId) || !ulong.TryParse(friendId, out fId)) return false;
            return IsFriend(pId, fId);
        }
        private bool IsFriend(ulong playerId, ulong friendId)
        {
            FriendData playerData;
            if (!friendData.TryGetValue(playerId, out playerData)) return false;
            return playerData.friendList.ContainsKey(friendId);
        }
        private int GetMaxFriends()
        { 
            return cfg.MaxFriends;
        }
        private ulong[] GetFriends(ulong playerId)
        { 
            FriendData playerData;
            if (!friendData.TryGetValue(playerId, out playerData)) return new ulong[0];
            var test = Pool.GetList<ulong>();
            foreach (var friendId in playerData.friendList)
            {
                test.Add(friendId.Key);
            }  
            return test.ToArray();
        }

        private ulong[] GetFriendList(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return new ulong[0];
            FriendData playerData;
            if (!friendData.TryGetValue(ulong.Parse(playerId), out playerData)) return new ulong[0];
            List<ulong> players = new List<ulong>();
            foreach (var friendId in playerData.friendList)
            {
                players.Add(friendId.Key);
            }
            return players.ToArray();
        }

        private ulong[] GetFriendList(ulong playerId)
        {
            return GetFriendList(playerId.ToString()).ToArray();
        }

        private ulong[] IsFriendOf(ulong playerId)
        {
            FriendData friend;
            return friendData.TryGetValue(playerId, out friend) ? friend.friendList.Keys.ToArray() : new ulong[0];
        }
        #endregion
    }
}

/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */