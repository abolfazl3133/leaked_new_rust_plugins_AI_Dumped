using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Clan", "https://discord.gg/TrJ7jnS233", "1.1.2")]
    public class Clan : RustPlugin
    {
        #region Variables


        [PluginReference] private Plugin ImageLibrary = null, TournamentBoloto = null;
        
        private string[] _gatherHooks = {
            "OnDispenserGather",
            "OnDispenserBonus",
        };
        
        private static Clan Instance;

        public List<ulong> playerActive = new List<ulong>();

        private static FieldInfo hookSubscriptions = 
            typeof(PluginManager).GetField("hookSubscriptions", (BindingFlags.Instance | BindingFlags.NonPublic));

        private List<ulong> _lootEntity = new List<ulong>();
        private Dictionary<string, int> ItemID = new Dictionary<string, int>();

        private const string Layer = "UI_LayerClan";

        #endregion
        
        #region Data

        public List<ClanData> _clanList = new List<ClanData>();

        public class ClanData
        {
            #region Member

            public class MemberData
            {
                public int MemberScores = 0;

                public int MemberKill = 0;
                
                public int MemberDeath = 0;

                public bool MemberFriendlyFire = false;

                public float AliveTime;

                public string LastTime;


                public Dictionary<string, int> GatherMember = new Dictionary<string, int>();
            }
            
            
            #endregion
            
            #region Gather

            public class GatherData
            {
                public int TotalFarm;

                public int Need;
            }

            #endregion
            
            #region Variables

            public string ClanTag;

            public string ImageAvatar;

            public ulong  LeaderUlongID;

            public string Task;

            public int    TotalScores;

            public ulong  TeamIDCreate;

            // 1.1.1
            

            public int    Turret;

            public int    SamSite;

            public int    Cupboard;
            
            //

            public Dictionary<ulong, MemberData> Members 
                = new Dictionary<ulong, MemberData>();
            
            public Dictionary<string, ulong> ItemList
                = new Dictionary<string, ulong>();

            public Dictionary<string, GatherData> GatherList
                = new Dictionary<string, GatherData>();

            public List<ulong> Moderators = new List<ulong>();

            public List<ulong> PendingInvites
                = new List<ulong>();


            [JsonIgnore]
            private RelationshipManager.PlayerTeam Team =>
                RelationshipManager.ServerInstance.FindTeam(TeamIDCreate) ?? FindOrCreateTeam();

            #endregion

            #region ClanTeam

            
            public RelationshipManager.PlayerTeam FindOrCreateTeam()
            {
                var leaderTeam = RelationshipManager.ServerInstance.FindPlayersTeam(LeaderUlongID);
                if (leaderTeam != null)
                {
                    if (leaderTeam.teamLeader == LeaderUlongID)
                    if (leaderTeam.teamLeader == LeaderUlongID)
                    {
                        TeamIDCreate = leaderTeam.teamID;
                        return leaderTeam;
                    }

                    leaderTeam.RemovePlayer(LeaderUlongID);
                }

                return CreateTeam();
            }

            private RelationshipManager.PlayerTeam CreateTeam()
            {
                var team = RelationshipManager.ServerInstance.CreateTeam();
                team.teamLeader = LeaderUlongID;
                AddPlayer(LeaderUlongID, team);

                TeamIDCreate = team.teamID;

                return team;
            }
            
            public void AddPlayer(ulong member, RelationshipManager.PlayerTeam team = null)
            {
                if (team == null)
                    team = Team;

                if (!team.members.Contains(member))
                    team.members.Add(member);

                if (member == LeaderUlongID)
                    team.teamLeader = LeaderUlongID;

                RelationshipManager.ServerInstance.playerToTeam[member] = team;

                var player = RelationshipManager.FindByID(member);
                if (player != null)
                {
                    if (player.Team != null && player.Team.teamID != team.teamID)
                    {
                        player.Team.RemovePlayer(player.userID);
                        player.ClearTeam();
                    }

                    player.currentTeam = team.teamID;

                    team.MarkDirty();
                    player.SendNetworkUpdate();
                }
            }


            #endregion
            
            #region AddPlayer


            public void InvitePlayer(BasePlayer player)
            {
                Members.Add(player.userID, new MemberData
                {
                    GatherMember = config.Main.GatherDictionary.ToDictionary(x => x, p => 0),
                });

                
                if (config.Main.EnableTeam)
                {
                    if (player.Team != null)
                        player.Team.RemovePlayer(player.userID);
                    
                    if (Team != null)
                        Team.AddPlayer(player);
                }
                
                if (config.Main.TagInPlayer)
                    player.displayName = $"[{ClanTag}] {player.displayName}";

                PendingInvites.Remove(player.userID);
                

                Interface.CallHook("OnClanUpdate", ClanTag, Members.Keys.ToList());
            }



            #endregion

            #region RemovePlayer


            public void RemovePlayerInClan(ulong player)
            {
                Members.Remove(player);
                
                if (Moderators.Contains(player))
                    Moderators.Remove(player);

                if (config.Main.TagInPlayer)
                {
                    var target = BasePlayer.Find(player.ToString());
                    if (target != null)
                        target.displayName = target.IPlayer.Name;
                }

                if (config.Main.EnableTeam)
                {
                    if (Team != null)
                        Team.RemovePlayer(player);
                }
                
                Interface.CallHook("OnClanUpdate", ClanTag, Members.Keys.ToList());
                
            }
            

            #endregion

            #region Disband

            public void Disband()
            {
                Interface.CallHook("OnClanDestroy", ClanTag);

                var listMember = Members.Keys.ToList();
                
                listMember.ForEach(p => RemovePlayerInClan(p));


                

                Instance._clanList.Remove(this);
            }

            #endregion

            #region SetOwner


            public void SetOwner(ulong playerID)
            {
                Interface.CallHook("OnClanOwnerChanged", ClanTag, LeaderUlongID, playerID);
                
                LeaderUlongID = playerID;

                ImageAvatar = $"avatar_{playerID}";
                
                if (config.Main.EnableTeam && Team != null)
                    Team.SetTeamLeader(playerID);
                

            }

            #endregion
            
            #region Functional Info

            public bool IsOwner(ulong playerID) => LeaderUlongID == playerID;

            public bool IsModerator(ulong playerID) => Moderators.Contains(playerID) || IsOwner(playerID);

            public bool IsMember(ulong playerID) => Members.ContainsKey(playerID);

            #endregion

            #region Gather
            
            public void ProcessingGather(BasePlayer player, Item item, bool bonus = false)
            {
                if (!GatherList.ContainsKey(item.info.shortname)) return;

                var getGather = GatherList[item.info.shortname];

                if (getGather.TotalFarm < getGather.Need)
                {
                    getGather.TotalFarm += item.amount;
                    
                    if (getGather.TotalFarm > getGather.Need)
                        getGather.TotalFarm = getGather.Need;
                }

                if (!Members[player.userID].GatherMember.ContainsKey(item.info.shortname))
                    Members[player.userID].GatherMember.Add(item.info.shortname, item.amount);
                else Members[player.userID].GatherMember[item.info.shortname] += item.amount;

                if (bonus)
                {
                    int ScoresGive;
                    
                    if (config.Point._gatherPoint.TryGetValue(item.info.shortname, out ScoresGive))
                    {
                        TotalScores += ScoresGive;
                        Members[player.userID].MemberScores += ScoresGive;
                    }
                }
            }

            #endregion

            #region GiveScores


            public void GiveScores(BasePlayer player, int scores)
            {
                TotalScores += scores;
                Members[player.userID].MemberScores += scores;
            }


            #endregion

            #region FF

            public void ChangeFriendlyFire(BasePlayer player)
            {
                var memberData = Members[player.userID];

                if (memberData.MemberFriendlyFire == true)
                    memberData.MemberFriendlyFire = false;
                else memberData.MemberFriendlyFire = true;
            }


            public bool GetValueFriendlyFire(BasePlayer player) => Members[player.userID].MemberFriendlyFire;
            
            
            #endregion

            #region Information


            [JsonIgnore]
            public Dictionary<string, string> GetInformation
            {
                get { return new Dictionary<string, string> { ["НАЗВАНИЕ КЛАНА:"] = ClanTag, ["ГЛАВА КЛАНА:"] = Instance.covalence.Players.FindPlayer(LeaderUlongID.ToString()).Name.ToUpper(), ["УЧАСТНИКОВ В ИГРЕ:"] = $"{Members.Keys.Select(p => BasePlayer.Find(p.ToString()) != null).Count()} ИЗ {Members.Count}", ["ОБЩЕЕ КОЛИЧЕСТВО ОЧКОВ:"] = $"{TotalScores}", }; }
            }

            [JsonIgnore]
            public Dictionary<string, string> GetInformationInfo
            {
                get { int totalPercent = GatherList.Sum(p => p.Value.TotalFarm) * 100 / GatherList.Sum(p => p.Value.Need > 0 ? p.Value.Need : 1); if (totalPercent > 100) totalPercent = 100; return new Dictionary<string, string> { ["ГЛАВА КЛАНА:"] = Instance.covalence.Players.FindPlayer(LeaderUlongID.ToString()).Name, ["ИГРОКОВ В КЛАНЕ:"] = $"{Members.Count}", ["НАБРАНО ОЧКОВ:"] = $"{TotalScores}", ["ОБЩАЯ АКТИВНОСТЬ:"] = $"{totalPercent}%", ["ВСЕГО УБИЙСТВ:"] = $"{Members.Sum(p => p.Value.MemberKill)}", ["ВСЕГО СМЕРТЕЙ:"] = $"{Members.Sum(p => p.Value.MemberDeath)}", }; }
            }

            public int TotalAmountFarm(ulong playerID) => Members[playerID].GatherMember.Sum(p => p.Value) * 100 / GatherList.Sum(p => p.Value.Need > 0 ? p.Value.Need : 1);
            
            
            #endregion

            #region JObject

            internal JObject ToJObject()
            {
                var obj = new JObject();
                obj["tag"] = ClanTag;
                obj["owner"] = LeaderUlongID;
                var jmoderators = new JArray();
                foreach (var moderator in Moderators) jmoderators.Add(moderator);
                obj["moderators"] = jmoderators;
                var jmembers = new JArray();
                foreach (var member in Members) jmembers.Add(member.Key);
                obj["members"] = jmembers;
                return obj;
            }

            #endregion
            
        }

        #endregion

        #region Hooks
        
        #region Loaded
        
        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Title == "Better Chat" || plugin.Title == "ChatPlus")
            {
                Interface.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(getFormattedClanTag));  
            }
            
            NextTick(() =>
            {
                
                foreach (string hook in _gatherHooks)
                {
                    Unsubscribe(hook);
                    Subscribe(hook);
                }
            });
        }
        
        #endregion
        
        #region Initialized

        void OnServerInitialized()
        {
            Instance = this;

            LoadData();

            SubscribeInternalHook("IOnBasePlayerHurt");
            
            AddCovalenceCommand(config.Main.CommandsMain.ToArray(), nameof(ClanCmd));
            
            AddCovalenceCommand(config.Main.CommandsTOP.ToArray(), nameof(ClanTopCmd));
            
            LoadMessages();

            if (!config.Stats.Gather)
            {
                Unsubscribe(nameof(OnDispenserGather));
                Unsubscribe(nameof(OnDispenserBonus));
            }

            if (!config.Stats.Entities && !config.Limit.EnableLimit)
                Unsubscribe(nameof(OnEntityDeath));

            if (!config.Stats.Loot)
                Unsubscribe(nameof(OnLootEntity));
            
            // Edited: 1.1.1
            
            if (!config.Main.AutomaticCupboard && !config.Limit.EnableLimit)
                Unsubscribe(nameof(OnEntityBuilt));
            
            if (!config.Main.AutomaticLock)
                Unsubscribe(nameof(CanUseLockedEntity));
            
            if (!config.Main.AutomaticTurret)
                Unsubscribe(nameof(OnTurretTarget));

            ImageLibrary?.Call("AddImage", "https://i.imgur.com/J5l4FWq.png", "lootbox");


            foreach (var key in ItemManager.GetItemDefinitions())
            {
                if (ItemID.ContainsKey(key.shortname)) continue;
                ItemID.Add(key.shortname, key.itemid);
            }
            
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
            
            if (config.ItemSkin.LoadSkinList)
                GenerateSkinList();
            
        }

        #endregion
        
        #region Unload

        void Unload()
        {

            foreach (var tPlayer in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(tPlayer);
            }

            Interface.GetMod().DataFileSystem.WriteObject(Name, _clanList);

            Instance = null;
            config = null;
        }

        #endregion
        
        #region Wipe

        void OnNewSave()
        {
            LoadData();
            
            // Даем время на инициализацию даты-файла

            timer.Once(3, () =>
            {
                
                if (config.Prize.EnablePrize)
                {
                    if (config.Prize.GSSettings.SecretKey == "UNDEFINED" || config.Prize.GSSettings.ShopID == "UNDEFINED") return;

                    int index = 1;

                    foreach (var clan in _clanList.OrderByDescending(p => p.TotalScores))
                    {
                        uint amount = 0;
                        if (config.Prize.RewardDictionary.TryGetValue(index, out amount))
                        {
                            foreach (var clanMember in clan.Members)
                            {
                                Request($"&action=moneys&type=plus&steam_id={clanMember.Key}&amount={amount}", null);
                            }
                        }
                        index++;
                    }
                }
                
                if (!config.Main.ClearWipe) return;

                _clanList.Clear();
            
                Interface.GetMod().DataFileSystem.WriteObject(Name, _clanList);
                
            });

            
        }

        #endregion

        #region ServerSave

        void OnServerSave() => timer.Once(5, () => Interface.GetMod().DataFileSystem.WriteObject(Name, _clanList));

        #endregion

        #region PlayerConnected

        void OnPlayerConnected(BasePlayer player)
        {
            var clan = FindClanByUser(player.userID);

            if (clan != null)
            {
                if (config.Main.TagInPlayer)
                    player.displayName = $"[{clan.ClanTag}] {GetNamePlayer(player)}";
                
                if (config.Main.EnableTeam)
                    clan.AddPlayer(player.userID);

                clan.Members[player.userID].LastTime = $"{DateTime.Now.ToString("t")} {DateTime.Now.Day}/{DateTime.Now.Month}/{DateTime.Now.Year}";
                
                player.SendNetworkUpdateImmediate();
            }
            
            GetAvatar(player.userID,
                avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));
            
        }

        #endregion

        #region PlayerDisconnected

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (playerActive.Contains(player.userID))
                playerActive.Remove(player.userID);
            

            var clan = FindClanByUser(player.userID);
            
            if (clan != null)
            {

                if (config.Main.TagInPlayer)
                    player.displayName = player.IPlayer.Name;
            }
            
        }

        #endregion

        #region Gather


        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null) return;

            FindClanByUser(player.userID)?.ProcessingGather(player, item);
        }

        void OnDispenserBonus(ResourceDispenser disp, BasePlayer player, Item item)
        {
            if (player == null) return;

            FindClanByUser(player.userID)?.ProcessingGather(player, item, true);
            
        }



        #endregion

        #region Limit

        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            if (info == null) return;
            
            if (entity.OwnerID == 0) return;
            
            if (!(entity as AutoTurret) || !(entity as BuildingPrivlidge) || !(entity as SamSite)) return;


            var findEntityClan = FindClanByUser(entity.OwnerID);
            
            if (findEntityClan == null) return;

            if (entity as AutoTurret)
            {
                findEntityClan.Turret--;
            }
            else if (entity as BuildingPrivlidge)
            {
                findEntityClan.Cupboard--;
            }
            else if (entity as SamSite)
            {
                findEntityClan.SamSite--;
            }

        }
        

        #endregion

        #region BradleyAPC
        
        
        

        void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            if (entity == null) return;
            if (info == null) return;
            
            if (info.InitiatorPlayer == null) return;

            var player = info.InitiatorPlayer;

            var clan = FindClanByUser(player.userID);
            if (clan == null) return;
            
            clan.GiveScores(player, config.Point.BradleyAPC);
        }

        #endregion

        #region Helicopter
        
        private readonly Dictionary<ulong, BasePlayer> _lastHeli = new Dictionary<ulong, BasePlayer>();

        private void OnEntityTakeDamage(BaseHelicopter entity, HitInfo info)
        {
            if (entity == null) return;
            if (info == null) return;

            if (info.InitiatorPlayer == null) return;
            
            _lastHeli[entity.net.ID.Value] = info.InitiatorPlayer;

            
        }

        void OnEntityDeath(BaseHelicopter entity, HitInfo info)
        {
            if (entity == null) return;
            if (info == null) return;

            if (_lastHeli.ContainsKey(entity.net.ID.Value))
            {
                var basePlayer = _lastHeli[entity.net.ID.Value];
                if (basePlayer != null)
                {
                    var clan = FindClanByUser(basePlayer.userID);
                    if (clan == null) return;
                    
                    clan.GiveScores(basePlayer, config.Point.Helicopter);
                    
                }
            }
        }
        

        #endregion
        
        #region Barrel

        private void OnEntityDeath(LootContainer entity, HitInfo info)
        {
            if (entity == null) return;
            if (info == null) return;
            
            if (!entity.ShortPrefabName.Contains("barrel")) return;
            
            if (!config.Point._gatherPoint.ContainsKey("lootbox")) return;

            if (info.InitiatorPlayer == null) return;
            var player = info.InitiatorPlayer;

            var clan = FindClanByUser(player.userID);
            if (clan == null) return;
            

            clan.GiveScores(player, config.Point._gatherPoint["lootbox"]);

            clan.GatherList["lootbox"].TotalFarm++;
            clan.Members[player.userID].GatherMember["lootbox"]++;

        }

        #endregion

        #region Loot

        private void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            if (player == null || entity == null) return;
            if (_lootEntity.Contains(entity.net.ID.Value)) return;
            
            if (!config.Point._gatherPoint.ContainsKey("lootbox")) return;

            var clan = FindClanByUser(player.userID);
            if (clan == null) return;
            
            clan.GiveScores(player, config.Point._gatherPoint["lootbox"]);

            clan.GatherList["lootbox"].TotalFarm++;
            clan.Members[player.userID].GatherMember["lootbox"]++;
            
            _lootEntity.Add(entity.net.ID.Value);
        }

        #endregion

        #region Kill && Death && Suicide


        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || !player.userID.IsSteamId()) return;
            
            if (info.damageTypes.Has(DamageType.Suicide))
            {
                var clan = FindClanByUser(player.userID);
                if (clan == null) return;
                
                clan.TotalScores -= config.Point.Suicide;
                var member = clan.Members[player.userID];

                member.MemberDeath++;
                member.MemberScores -= config.Point.Suicide;
                
                return;
            }
            
            var attacker = info.InitiatorPlayer;

            if (attacker == null || !attacker.userID.IsSteamId() || FindClanByUser(player.userID)?.IsMember(attacker.userID) == true) return;

            if (player.userID.IsSteamId())
            {
                var clan = FindClanByUser(player.userID);
                if (clan != null)
                {
                    clan.TotalScores -= config.Point.Death;
                    var member = clan.Members[player.userID];

                    member.MemberDeath++;
                    member.MemberScores -= config.Point.Death;
                }

                var clanAttacker = FindClanByUser(attacker.userID);

                if (clanAttacker != null)
                {
                    clanAttacker.TotalScores += config.Point.Kill;
                    var member = clanAttacker.Members[attacker.userID];

                    member.MemberKill++;
                    member.MemberScores += config.Point.Kill;
                }
            }

        }

        #endregion

        #region Damage


        private object IOnBasePlayerHurt(BasePlayer player, HitInfo info)
        {
            if (player == null) return null;
            if (info == null) return null;

            if (info.InitiatorPlayer == null) return null;

            var initiator = info.InitiatorPlayer;
            if (player == initiator) return null;

            var clan = FindClanByUser(player.userID);
            if (clan == null) return null;

            if (clan.IsMember(initiator.userID) && !clan.GetValueFriendlyFire(initiator))
            {
                if (initiator.SecondsSinceAttacked > 5)
                {
                    initiator.ChatMessage(GetLang("ClanFFActive", initiator.UserIDString));

                    initiator.lastAttackedTime = UnityEngine.Time.time;
                }
                
                DisableDamage(info);

                return false;
            }

            return null;
        }


        #endregion

        #region Skins

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null) return;
            var player = container.GetOwnerPlayer();
            if (player == null) return;
            
            var clan = FindClanByUser(player.userID);
            if (clan == null) return;

            if (clan.ItemList.ContainsKey(item.info.shortname))
            {
                var skin = clan.ItemList[item.info.shortname];
                if (skin > 0)
                {
                    if (item.info.category == ItemCategory.Attire && container == player.inventory.containerWear)
                    {
                        ItemEditSkinID(item, skin);
                    }
                    else
                    {
                        ItemEditSkinID(item, skin);
                    }
                }
            }
        }
        
        

        #endregion

        #region Auth

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;
            var player = plan.GetOwnerPlayer();
            if (player == null) return;

            BaseEntity entity = go.ToBaseEntity();
            if (entity == null) return;


            var clan = FindClanByUser(player.userID);
            if (clan == null) return;
            

            if (entity.GetComponent<BuildingPrivlidge>() != null)
            {
                
            
                var cup = entity.GetComponent<BuildingPrivlidge>();

                if (config.Limit.EnableLimit)
                {
                    if (clan.Cupboard >= config.Limit.LCupboard)
                    {
                        player.GetActiveItem().amount++;
                        NextTick(() => entity.Kill());
                    }
                    else 
                        clan.Cupboard++;
                    
                    
                    player.ChatMessage(GetLang("Cupboard", player.UserIDString, config.Limit.LCupboard - clan.Cupboard, config.Limit.LCupboard));
                }

                if (config.Main.AutomaticCupboard && entity != null)
                {
                    foreach (var member in clan.Members)
                    {
                        cup.authorizedPlayers.Add(new PlayerNameID
                        {
                            userid = member.Key,
                            username = ""
                        });
                    }
                }

                return;
            }

            if (entity.GetComponent<AutoTurret>() != null)
            {
                if (config.Limit.EnableLimit)
                {
                    if (clan.Turret >= config.Limit.LTurret)
                    {
                        player.GetActiveItem().amount++;
                        NextTick(() => entity.Kill());
                    }
                    else 
                        clan.Turret++;
                    
                    
                    player.ChatMessage(GetLang("Turret", player.UserIDString, config.Limit.LTurret - clan.Turret, config.Limit.LTurret));
                }
                
                return;
            }
            
            if (entity.GetComponent<SamSite>() != null)
            {
                if (config.Limit.EnableLimit)
                {
                    if (clan.SamSite >= config.Limit.LSamSite)
                    {
                        player.GetActiveItem().amount++;
                        NextTick(() => entity.Kill());
                    }
                    else 
                        clan.SamSite++;
                    
                    
                    player.ChatMessage(GetLang("SamSite", player.UserIDString, config.Limit.LSamSite - clan.SamSite, config.Limit.LSamSite));
                }
                
                return;
            }
        }
        
        object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null || baseLock.OwnerID == 0 || baseLock.OwnerID == player.userID) return null;
            if (baseLock.GetComponent<CodeLock>() == null || baseLock.GetComponent<CodeLock>().whitelistPlayers.Contains(player.userID)) return null;
            

            if ((bool) IsClanMember(baseLock.OwnerID.ToString(), player.userID.ToString()))
                return true;

            return null;
        }
        
        object OnTurretTarget(AutoTurret turret, BasePlayer player)
        {
            if (turret == null) return null;
            if (player == null) return null;

            if (turret.OwnerID == 0) return null;

            //if (turret.IsAuthed(player)) return null;

            if ((bool)IsClanMember(turret.OwnerID.ToString(), player.userID.ToString()))
            {
                return false;
            }

            return null;
        }
        

        #endregion

        #endregion
        
        #region Functional
        
        #region Request

        private static void Request(string ask, Action<int, string> callback)
        {
            Dictionary<string, string> reqHeaders = new Dictionary<string, string>{{ "User-Agent", "Clan Plugin" }};
            Instance.webrequest.Enqueue($"https://gamestores.app/api/?shop_id={config.Prize.GSSettings.ShopID}&secret={config.Prize.GSSettings.SecretKey}" + ask, "", (code, response) =>
            {

                switch (code)
                {
                    case 200:
                    {
                        break;
                    }

                    case 404:
                    {
                        Instance.PrintWarning($"Please check your configuration! [404] #2");
                        break;
                    }
                }

                if (response.Contains("The authentication or decryption has failed."))
                {
                    Instance.PrintWarning("HTTPS request is broken (broken CA certificate?). Changed to non secure connection!");

                    Interface.Oxide.UnloadPlugin(Instance.Name);
                    return;
                }

                callback?.Invoke(code, response);
            }, Instance, RequestMethod.GET, reqHeaders);
                      
        }

        #endregion
        
        
        #region Formated
        
        string getFormattedClanTag(IPlayer player)
        {
            var clan = FindClanByUser(ulong.Parse(player.Id));
            if (clan != null && !string.IsNullOrEmpty(clan.ClanTag)) return $"[{clan.ClanTag}]";
            return string.Empty;
        }
        
        #endregion


        #region LoadData

        void LoadData()
        {
            if (Interface.GetMod().DataFileSystem.ExistsDatafile(Name))
            {
                _clanList = Interface.GetMod().DataFileSystem.ReadObject<List<ClanData>>(Name);
            }
            else _clanList = new List<ClanData>();
        }

        #endregion

        #region GetAvatar

        private readonly Regex Regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");
        
        private void GetAvatar(ulong userId, Action<string> callback)
        {
            if (callback == null) return;

            webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
            {
                if (code != 200 || response == null)
                    return;

                var avatar = Regex.Match(response).Groups[1].ToString();
                if (string.IsNullOrEmpty(avatar))
                    return;

                callback.Invoke(avatar);
            }, this);
        }

        #endregion

        #region HexColor

        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }

        #endregion
        
        #region GenerateSkins

        public void GenerateSkinList()
        {
            if (!ImageLibrary) return;

            bool any = false;
            
            
            foreach (var wearDictionary in config.Main.WearDictionary)
            {
                var anyDictionary = config.ItemSkin.ItemsSkins.ContainsKey(wearDictionary.Key);
                
                if (!anyDictionary)
                {
                    config.ItemSkin.ItemsSkins[wearDictionary.Key] = ImageLibrary?.Call<List<ulong>>("GetImageList", wearDictionary.Key) ?? new List<ulong>();
                    any = true;
                }
            }
            if (any)
                SaveConfig();
        }

        #endregion

        #region GetNamePlayer

        private string GetNamePlayer(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId())
                return string.Empty;

            var covPlayer = player.IPlayer;

            if (player.net?.connection == null)
            {
                if (covPlayer != null)
                    return covPlayer.Name;

                return player.UserIDString;
            }

            var value = player.net.connection.username;
            var str = value.ToPrintable(32).EscapeRichText().Trim();
            if (string.IsNullOrWhiteSpace(str))
            {
                str = covPlayer.Name;
                if (string.IsNullOrWhiteSpace(str))
                    str = player.UserIDString;
            }

            return str;
        }

        #endregion

        #region Disable Damage

        private void DisableDamage(HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.DidHit = false;
            info.HitEntity = null;
            info.Initiator = null;
            info.DoHitEffects = false;
            info.HitMaterial = 0;
        }
        
        #endregion

        #region InternalHook
        
        private void SubscribeInternalHook(string hook)
        {
            var hookSubscriptions_ = hookSubscriptions.GetValue(Interface.Oxide.RootPluginManager) as IDictionary<string, IList<Plugin>>;
			
            IList<Plugin> plugins;
            if (!hookSubscriptions_.TryGetValue(hook, out plugins))
            {
                plugins = new List<Plugin>();
                hookSubscriptions_.Add(hook, plugins);
            }
			
            if (!plugins.Contains(this))            
                plugins.Add(this);            
        }

        #endregion
        
        #region AddedClan


        public void CreateInClan(BasePlayer player, string clanTag)
        {
            var clanGather = new Dictionary<string, ClanData.GatherData>();
            
            var SkinList = new Dictionary<string, ulong>();
            

            foreach (var key in config.Main.WearDictionary)
            {
                SkinList.Add(key.Key, key.Value);
            }
            
            foreach (var key in config.Main.GatherDictionary)
            {
                clanGather.Add(key, new ClanData.GatherData
                {
                    Need = 1000,
                    TotalFarm = 0,
                });
            }

            var newclan = new ClanData()
            {
                ClanTag = clanTag,

                ImageAvatar = $"avatar_{player.UserIDString}",

                GatherList = clanGather,

                ItemList = SkinList,

                LeaderUlongID = player.userID,

                Task = "",

                TotalScores = 0,

                Members
                    = new Dictionary<ulong, ClanData.MemberData>(),

                Moderators
                    = new List<ulong>(),

                PendingInvites
                    = new List<ulong>()
            };
            

            newclan.Members.Add(player.userID, new ClanData.MemberData
            {
                GatherMember = config.Main.GatherDictionary.ToDictionary(x => x, p => 0),
            });

            //_ins.PrintWarning($"[{DateTime.Now.ToString("g")}] был создан клан {clanTag} игроком {player.displayName}");

            if (config.Main.TagInPlayer)
                player.displayName = $"[{clanTag}] {GetNamePlayer(player)}";

            if (config.Main.EnableTeam)
                newclan.FindOrCreateTeam();

            player.SendNetworkUpdateImmediate();

            _clanList.Add(newclan);

            Interface.CallHook("OnClanCreate", clanTag, player);


        }

        #endregion
        
        #region FormatTime

        private string GetFormatTime(TimeSpan timespan)
        {
            return string.Format("{0:00}м. {1:00}с.", timespan.Minutes, timespan.Seconds);
        }

        #endregion
        
        #region Lang

        public static StringBuilder sb = new StringBuilder();

        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }

            return lang.GetMessage(LangKey, this, userID);
        }

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    ["ClanNotFound"] = "Что бы создать клан введите: /clan create \"название клана\"",
                    ["ClanInviteNotFoundPlayer"] = "/clan invite \"ник или steamid игрока\" - отправить предложение вступить в клан",
                    ["ClanKickNotFoundPlayer"] = "/clan kick \"ник или steamid игрока\" - исключить игрока из клана",
                    ["ClanOwnerNotFoundPlayer"] = "/clan owner \"ник или steamid игрока\" - назначить игрока главой клана",
                    ["ClanTaskNotLength"] = "/clan task \"задача\" - установить задачу",
                    ["ClanTask"] = "Вы успешно установили задачу!",
                    ["TargetNotClan"] = "Игрок {0} не был найден в клане",
                    ["TargetModeratorAndOwner"] = "Игрок является модератором или главой в клане!",
                    ["PlayerNotFound"] = "Игрок {0} не был найден",
                    ["PlayerNotClan"] = "Вы не состоите в клане",
                    ["PlayerOwner"] = "Вы являетесь главой клана",
                    ["PlayerLeave"] = "Вы успешно покинули клан",
                    ["PlayerKickSelf"] = "Вы не можете кикнуть самого себя!",
                    ["PlayerKick"] = "Вы были кикнуты из клана!",
                    ["PlayerModeratorKick"] = "Вы успешно кикнули игрока {0} из клана!",
                    ["NameClanLength"] = "Минимальная длина тега: {0}, максимальная длина тега: {1}",
                    ["ContainsTAG"] = "Данный клан тег уже занят другим кланом",
                    ["ClanTagBlocked"] = "Данный клан тег запрещен",
                    ["PlayerInClan"] = "Вы уже состоите в клане",
                    ["TargetInClan"] = "Данный игрок уже находится в клане",
                    ["PlayerNotOwnerAndModerator"] = "Вы не являетесь модератором или главой клана",
                    ["PlayerNotOwner"] = "Вы не являетесь главой клана",
                    ["PSetLeader"] = "Вы успешно стали главой клана",
                    ["PGiveLeader"] = "Вы успешно передали главу клана",
                    ["ClanDisband"] = "Вы успешно расформировали клан",
                    ["PlayerNotInvite"] = "Вы не имеете активных предложений",
                    ["PlayerStartClan"] = "Вы успешно создали клан: {0}!",
                    ["ClanLimitPlayer"] = "Превышено количество игроков в клане!",
                    ["ClanLimitModerator"] = "Превышено количество модераторов в клане!",
                    ["AcceptInvite"] = "Вы успешно присоединились к клану {0}",
                    ["DenyInvite"] = "Вы успешно отклонили предложение для вступления в клан",
                    ["PlayerStartInvite"] = "Вы получили приглашение в клан: {0}\n/clan accept- принять приглашение\n/clan deny - отклонить предложение",
                    ["InitiatorStartInvite"] = "Вы успешно отправили приглашение игроку: {0}",
                    ["ClanFFActive"] = "Вы не можете нанести урон своему напарнику. Что бы включить FF, пропишите команду /clan ff",
                    ["ClanFFActivate"] = "Вы успешно включили FF",
                    ["ClanFFDeactivation"] = "Вы успешно выключили FF",
                    ["HelpNoClan"] = "Помощь:\n/clan create \"название клана\" - создать клан\n/clan help - список доступных комманд\n/clan accept - принять предложение вступить в клан\n/clan deny - отклонить предложение вступить в клан",
                    ["HelpClanPlayer"] = "Помощь:\n/clan help - список доступных комманд\n/clan leave - покинуть клан\n/c \"сообщение\" - отправить сообщение всему клану",
                    ["HelpClanModerator"] = "Помощь:\n/clan invite \"ник или steamid игрока\" - отправить предложение вступить в клан\n/clan kick \"ник или steamid игрока\" - исключить игрока из клана\n/clan leave - покинуть клан\n",
                    ["HelpClanOwner"] = "Помощь:\n/clan invite \"ник или steamid игрока\" - отправить предложение вступить в клан\n/clan kick \"ник или steamid игрока\" - исключить игрока из клана\n/clan disband - расфомировать клан\n/clan task \"задача\" - установить задачу\n/clan owner \"ник или steamid игрока\" - назначить игрока главой клана",
                    ["Cupboard"] = "Осталось доступных шкафов: {0} из {1}",
                    ["Turret"] = "Осталось доступных турелей: {0} из {1}",
                    ["SamSite"] = "Осталось доступных ПВО: {0} из {1}",
                    ["UI_TaskContent"] = "ТЕКУЩАЯ ЗАДАЧА",
                    ["UI_ClothContent"] = "НАБОР КЛАНОВОЙ ОДЕЖДЫ",
                    ["UI_GatherContent"] = "ДОБЫЧА РЕСУРСОВ",
                    ["UI_NameContent"] = "НИК ИГРОКА",
                    ["UI_ActivityContent"] = "АКТИВНОСТЬ",
                    ["UI_StandartContent"] = "НОРМА",
                    ["UI_ScoresContent"] = "ОЧКИ ТОПА",
                    ["UI_TaskMessageContent"] = "Глава клана еще не указал текущую задачу",
                    ["UI_GatherStart"] = "Установка нормы",
                    ["UI_GatherComment"] = "Укажите количество которое должен добыть участник группы",
                    ["UI_SkinContent"] = "ВЫБЕРИТЕ СКИН",
                    ["UI_TopName"] = "НАЗВАНИЕ КЛАНА",
                    ["UI_TopTournament"] = "ШКАФ",
                    ["UI_TopReward"] = "НАГРАДА",
                    ["UI_TopScores"] = "ОЧКИ",
                    ["UI_TopPlayer"] = "ИГРОКОВ",
                    ["UI_TopInformation"] = "Очки даются:\nУбийство +20, добыча руда +1-2,разрушение бочки +1, сбитие вертолета +1000, уничтожение бредли +650\nОчки отнимаются:\nСмерть -10, самоубийство -30\nНаграда выдается после вайпа на сервере!",
                    ["UI_InfoClanName"] = "ИМЯ ИГРОКА",
                    ["UI_InfoClanScores"] = "ОЧКОВ",
                    ["UI_InfoClanFarm"] = "НОРМА",
                    ["UI_InfoClanKill"] = "УБИЙСТВ",
                    ["UI_InfoClanDeath"] = "СМЕРТЕЙ"

                }
                , this);
        }

        #endregion
        
        #region StartCommand

        
        [ConsoleCommand("Clan_Command")]
        void RunCommand(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            if (args.FullString.Contains("/"))
                player.Command("chat.say", args.FullString);
            else
                player.Command(args.FullString);
        }

        #endregion

        #region ItemEditSkinID

        public void ItemEditSkinID(Item item, ulong SkinID)
        {
            item.skin = SkinID;
            item.MarkDirty();

            var heldEntity = item.GetHeldEntity();
            if (heldEntity == null) return;

            heldEntity.skinID = SkinID;
            heldEntity.SendNetworkUpdate();   
        }
        

        #endregion
        

        #endregion
        
        #region Command
        
        #region ChatCommand

        private void ClanTopCmd(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (args.Length == 0)
            {
                if (playerActive.Contains(player.userID))
                    return;
                
                MainTopUI(player);
                return;
            }
        }

        private void ClanCmd(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (args.Length == 0)
            {
                var clan = FindClanByUser(player.userID);
                if (clan == null)
                {
                    player.ChatMessage(GetLang("ClanNotFound", player.UserIDString));
                    return;
                }

                if (playerActive.Contains(player.userID))
                    return;

                MainUI(player);
                return;
            }

            switch (args[0])
            {
                #region Create
                
                case "create":
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetLang("ClanNotFound", player.UserIDString));
                        return;
                    }

                    var clan = FindClanByUser(player.userID);
                    if (clan != null)
                    {
                        player.ChatMessage(GetLang("PlayerInClan", player.UserIDString));
                        return;
                    }
                    
                    var clanTag = string.Join(" ", args.Skip(1));

                    if (string.IsNullOrEmpty(clanTag))
                    {
                        player.ChatMessage(GetLang("ClanNotFound", player.UserIDString));
                        return;
                    }

                    clanTag = clanTag.Replace(" ", "");
                    
                    
                    if (clanTag.Length < config.Main.MinNameLength || clanTag.Length > config.Main.MaxNameLength)
                    {
                        player.ChatMessage(GetLang("NameClanLength", player.UserIDString, config.Main.MinNameLength, config.Main.MaxNameLength));
                        return;
                    }
                    
                    
                    if (config.Main.ForbiddenTag.Exists(word => $"[{clanTag}]".Contains(word, CompareOptions.OrdinalIgnoreCase))) // Mevent <3
                    {
                        player.ChatMessage(GetLang("ClanTagBlocked", player.UserIDString));
                        return;
                    }

                    var alreadyClan = FindClanByTag(clanTag);
                    if (alreadyClan != null)
                    {
                        player.ChatMessage(GetLang("ContainsTAG", player.UserIDString));
                        return;
                    }


                    CreateInClan(player, clanTag);
                    
                    
                    player.ChatMessage(GetLang("PlayerStartClan", player.UserIDString, clanTag));
                    
                    
                    
                    break;
                }
                
                #endregion

                #region Accept

                case "accept":
                {
                    var clan = FindClanByUser(player.userID);
                    if (clan != null)
                    {
                        player.ChatMessage(GetLang("PlayerInClan", player.UserIDString));
                        return;
                    }

                    var clanInvite = FindClanByInvite(player.userID);
                    if (clanInvite == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotInvite", player.UserIDString));
                        return;
                    }

                    if (clanInvite.Members.Count >= config.Main.MaxCountClan)
                    {
                        player.ChatMessage(GetLang("ClanLimitPlayer", player.UserIDString));
                        clanInvite.PendingInvites.Remove(player.userID);
                        return;
                    }
                    
                    clanInvite.InvitePlayer(player);
                     
                    player.ChatMessage(GetLang("AcceptInvite", player.UserIDString, clanInvite.ClanTag));
                    
                    break;
                }

                #endregion

                #region Deny

                case "deny":
                {
                    var clan = FindClanByUser(player.userID);
                    if (clan != null)
                    {
                        player.ChatMessage(GetLang("PlayerInClan", player.UserIDString));
                        return;
                    }

                    var clanInvite = FindClanByInvite(player.userID);
                    if (clanInvite == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotInvite", player.UserIDString));
                        return;
                    }

                    clanInvite.PendingInvites.Remove(player.userID);
                    
                    player.ChatMessage(GetLang("DenyInvite", player.UserIDString));
                    
                    break;
                }

                #endregion

                #region Invite

                case "invite":
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetLang("ClanInviteNotFoundPlayer", player.UserIDString));
                        return;
                    }

                    var clan = FindClanByUser(player.userID);
                    
                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotClan", player.UserIDString));
                        return;
                    }

                    if (!clan.IsModerator(player.userID))
                    {
                        player.ChatMessage(GetLang("PlayerNotOwnerAndModerator", player.UserIDString));
                        return;
                    }

                    if (clan.Members.Count >= config.Main.MaxCountClan)
                    {
                        player.ChatMessage(GetLang("ClanLimitPlayer", player.UserIDString));
                        return;
                    }


                    string name = string.Join(" ", args.Skip(1));

                    var targetPlayer = BasePlayer.Find(name);
                    if (targetPlayer == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotFound", player.UserIDString, name));
                        return;
                    }
                    
                    if (player == targetPlayer) return;

                    var clanTarget = FindClanByUser(targetPlayer.userID);
                    if (clanTarget != null)
                    {
                        player.ChatMessage(GetLang("TargetInClan", player.UserIDString));
                        return;
                    }
                    
                    clan.PendingInvites.Add(targetPlayer.userID);
                    
                    targetPlayer.ChatMessage(GetLang("PlayerStartInvite", targetPlayer.UserIDString, clan.ClanTag));
                    player.ChatMessage(GetLang("InitiatorStartInvite", player.UserIDString, targetPlayer.displayName));
                    break;
                }

                #endregion

                #region Help

                case "help":
                {

                    var clan = FindClanByUser(player.userID);
                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("HelpNoClan", player.UserIDString));
                        return;
                    }

                    if (!clan.IsModerator(player.userID))
                    {
                        player.ChatMessage(GetLang("HelpClanPlayer", player.UserIDString));
                        
                        return;
                    }

                    if (clan.LeaderUlongID != player.userID && clan.Moderators.Contains(player.userID))
                    {
                        player.ChatMessage(GetLang("HelpClanModerator", player.UserIDString));
                        
                        return;
                    }

                    if (clan.LeaderUlongID == player.userID)
                    {
                        player.ChatMessage(GetLang("HelpClanOwner", player.UserIDString));
                    }
                    
                    break;
                }

                #endregion

                #region Leave

                case "leave":
                {

                    var clan = FindClanByUser(player.userID);
                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotClan", player.UserIDString));
                        return;
                    }

                    if (clan.IsOwner(player.userID))
                    {
                        player.ChatMessage(GetLang("PlayerOwner", player.UserIDString));
                        return;
                    }
                    
                    clan.RemovePlayerInClan(player.userID);
                    
                    player.ChatMessage(GetLang("PlayerLeave", player.UserIDString));
                    
                    break;
                }

                #endregion

                #region Kick
                
                case "kick":
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetLang("ClanKickNotFoundPlayer", player.UserIDString));
                        return;
                    }
                    
                    
                    var clan = FindClanByUser(player.userID);

                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotClan", player.UserIDString));
                        return;
                    }
                    
                    if (!clan.IsModerator(player.userID))
                    {
                        player.ChatMessage(GetLang("PlayerNotOwnerAndModerator", player.UserIDString));
                        return;
                    }
                    
                    string name = string.Join(" ", args.Skip(1));

                    var targetPlayer = covalence.Players.FindPlayer(name);
                    if (targetPlayer == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotFound", player.UserIDString, name));
                        return;
                    }

                    if (player.IPlayer == targetPlayer)
                    {
                        player.ChatMessage(GetLang("PlayerKickSelf", player.UserIDString));
                        return;
                    }

                    if (!clan.IsMember(ulong.Parse(targetPlayer.Id)))
                    {
                        player.ChatMessage(GetLang("TargetNotClan", player.UserIDString, targetPlayer.Name));
                        return;
                    }

                    if (clan.IsModerator(ulong.Parse(targetPlayer.Id)))
                    {
                        player.ChatMessage(GetLang("TargetModeratorAndOwner", player.UserIDString));
                        return;
                    }
                    
                    clan.RemovePlayerInClan(ulong.Parse(targetPlayer.Id));
                    
                    player.ChatMessage(GetLang("PlayerModeratorKick", player.UserIDString, targetPlayer.Name));
                    
                    if (targetPlayer.IsConnected)
                        BasePlayer.Find(targetPlayer.Id).ChatMessage(GetLang("PlayerKick", targetPlayer.Id));
                    
                    
                    break;
                }

                #endregion

                #region Disband

                case "disband":
                {
                    var clan = FindClanByUser(player.userID);
                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotClan", player.UserIDString));
                        return;
                    }
                    
                    if (!clan.IsOwner(player.userID))
                    {
                        player.ChatMessage(GetLang("PlayerNotOwner", player.UserIDString));
                        return;
                    }
                    
                    clan.Disband();
                    
                    player.ChatMessage(GetLang("ClanDisband", player.UserIDString));
                    
                    break;
                }

                #endregion

                #region Owner

                case "owner":
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetLang("ClanOwnerNotFoundPlayer", player.UserIDString));
                        return;
                    }
                    
                    
                    var clan = FindClanByUser(player.userID);

                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotClan", player.UserIDString));
                        return;
                    }
                    
                    if (!clan.IsOwner(player.userID))
                    {
                        player.ChatMessage(GetLang("PlayerNotOwner", player.UserIDString));
                        return;
                    }
                    
                    string name = string.Join(" ", args.Skip(1));

                    var targetPlayer = BasePlayer.Find(name);
                    if (targetPlayer == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotFound", player.UserIDString, name));
                        return;
                    }

                    if (player == targetPlayer) return;

                    if (!clan.IsMember(targetPlayer.userID))
                    {
                        player.ChatMessage(GetLang("TargetNotClan", player.UserIDString, targetPlayer.displayName));
                        return;
                    }
                    
                    clan.SetOwner(targetPlayer.userID);

                    targetPlayer.ChatMessage(GetLang("PSetLeader", targetPlayer.UserIDString));
                    
                    player.ChatMessage(GetLang("PGiveLeader", player.UserIDString));
                    
                    break;
                }

                #endregion

                #region Task
                
                case "task":
                {
                    if (args.Length < 1)
                    {
                        player.ChatMessage(GetLang("ClanTaskNotLength", player.UserIDString));
                        return;
                    }
                    var clan = FindClanByUser(player.userID);

                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotClan", player.UserIDString));
                        return;
                    }
                    
                    if (!clan.IsOwner(player.userID))
                    {
                        player.ChatMessage(GetLang("PlayerNotOwner", player.UserIDString));
                        return;
                    }
                    
                    string task = string.Join(" ", args.Skip(1));
                    if (string.IsNullOrEmpty(task))
                        task = string.Empty;

                    clan.Task = task;
                    
                    player.ChatMessage(GetLang("ClanTask", player.UserIDString));
                    
                    break;
                }

                #endregion

                #region FF

                case "ff":
                {
                    var clan = FindClanByUser(player.userID);
                    if (clan == null)
                    {
                        player.ChatMessage(GetLang("PlayerNotClan", player.UserIDString));
                        return;
                    }
                    clan.ChangeFriendlyFire(player);

                    bool valueBool = clan.GetValueFriendlyFire(player);

                    string langMessage = valueBool ? "ClanFFActivate" : "ClanFFDeactivation";
                    
                    player.ChatMessage(GetLang(langMessage, player.UserIDString));
                    
                    break;
                }

                #endregion
                
            }
        }

        #endregion

        [ConsoleCommand("clan.changeowner")]
        void ChangeOwner(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin) return;
            if (args.Args.Length < 2)
            {
                SendReply(args, "clan.changeowner <tag> <nickname/steamid>");
                return;
            }
            var findClan = FindClanByTag(args.Args[0]);
            if (findClan == null) return;

            var playerCovalence = covalence.Players.FindPlayer(args.Args[1]);
            if (playerCovalence == null)
            {
                SendReply(args, $"Игрок {args.Args[1]} не найден!");
                return;
            }

            if (!findClan.IsMember(ulong.Parse(playerCovalence.Id)))
            {
                SendReply(args, $"Игрок {playerCovalence.Name} не состоит в клане!");
                return;
            }
            findClan.SetOwner(ulong.Parse(playerCovalence.Id));
            
            SendReply(args, $"Игрок {playerCovalence.Name} был установлен новым главой клана {findClan.ClanTag}!");
        }

        #region ConsoleComand

        [ConsoleCommand("UI_ClanHandler")]
        void ClanUIHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            if (args.Args[0] == "close")
            {
                if (playerActive.Contains(player.userID))
                    playerActive.Remove(player.userID);
                    

                CuiHelper.DestroyUi(player, Layer);
            }
            
            #region Skin

            else if (args.Args[0] == "acceptSkin")
            {
                CuiHelper.DestroyUi(player, ".EditSkin");
                
                string ShortName = args.Args[1];
                ulong SkinID = 0;
                
                if (ulong.TryParse(args.Args[2], out SkinID))
                {
                    var clan = FindClanByUser(player.userID);
                    if (clan == null) return;
                    
                    if (!clan.IsOwner(player.userID)) return;
                    
                    if (clan.ItemList[ShortName] == SkinID) return;
                    
                    
                    clan.ItemList[ShortName] = SkinID;
                    LoadSkin(player, clan ,true);
                }
            }
            else if (args.Args[0] == "editSkin")
            {
                string ShortName = args.Args[1];
                
                if (!config.ItemSkin.ItemsSkins.ContainsKey(ShortName)) return;
                
                var clan = FindClanByUser(player.userID);
                if (clan == null) return;
                
                if (!clan.IsOwner(player.userID)) return;
                

                var container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer, ".EditSkin");
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0.9"},
                    Text = { Text = "" }
                }, ".EditSkin");
                
                container.Add(new CuiButton // Main
                {
                    RectTransform = { AnchorMin = "0.2265625 0.1388889", AnchorMax = "0.7734375 0.8611111" },
                    Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

                }, ".EditSkin");
                
                container.Add(new CuiElement
                {
                    Parent = ".EditSkin",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_SkinContent", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.2265625 0.8212963", AnchorMax = "0.7734375 0.8611111" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });
                
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.6781253 0.1425926", AnchorMax = "0.7546877 0.1722223"}, Button = { Close = ".EditSkin", Color = HexToCuiColor("#fffdfc", 20), Material = "assets/content/ui/uibackgroundblur.mat" }, Text = { Text = "ЗАКРЫТЬ", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                }, ".EditSkin");

                CuiHelper.AddUi(player, container);
                
                container.Clear();
                
                LoadEditSkin(player, ShortName, 1, true);
            }

            #endregion

            #region Gather
            
            else if (args.Args[0] == "acceptGather")
            {
                CuiHelper.DestroyUi(player, ".EditSkin");
                
                string ShortName = args.Args[1];
                
                if (!config.Main.GatherDictionary.Contains(ShortName)) return;
                
                var clan = FindClanByUser(player.userID);
                if (clan == null) return;
                
                if (!clan.IsOwner(player.userID)) return;
                
                if (!clan.GatherList.ContainsKey(ShortName)) return;

                int amount = 0;
                if (int.TryParse(args.Args[2], out amount))
                {
                    if (amount < 0) return;
                    
                    clan.GatherList[ShortName].Need = amount;
                    LoadGatherList(player, clan, true);
                }
                
            }
            
            else if (args.Args[0] == "editGather")
            {
                string ShortName = args.Args[1];
                
                if (!config.Main.GatherDictionary.Contains(ShortName)) return;
                
                var clan = FindClanByUser(player.userID);
                if (clan == null) return;
                
                if (!clan.IsOwner(player.userID)) return;
                
                if (!clan.GatherList.ContainsKey(ShortName)) return;
                
                var container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer, ".EditSkin");
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0.9"},
                    Text = { Text = "" }
                }, ".EditSkin");
                
                container.Add(new CuiButton // Main
                {
                    RectTransform = { AnchorMin = "0.3046875 0.4166667", AnchorMax = "0.6953125 0.5833333" },
                    Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

                }, ".EditSkin");
                
                container.Add(new CuiElement
                {
                    Parent = ".EditSkin",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_GatherStart", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3046875 0.55", AnchorMax = "0.6953125 0.5833333" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });
                
                container.Add(new CuiElement
                {
                    Parent = ".EditSkin",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_GatherComment", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3125 0.5074087", AnchorMax = "0.6869792 0.540742" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });
                
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.5937486 0.4212963", AnchorMax = "0.6874986 0.4592593"}, Button = { Close = ".EditSkin", Color = HexToCuiColor("#fffdfc", 20), Material = "assets/content/ui/uibackgroundblur.mat" }, Text = { Text = "ЗАКРЫТЬ", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                }, ".EditSkin");
                
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.4942709 0.4203704", AnchorMax = "0.5880209 0.4583333"}, Button = { Color = HexToCuiColor("#60891B", 100), Material = "assets/content/ui/uibackgroundblur.mat" }, Text = { Text = "УСТАНОВИТЬ", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                }, ".EditSkin");
                
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.3125 0.4666681", AnchorMax = "0.6875 0.5074074"}, Button = { Color = HexToCuiColor($"#000000FF", 50) }, Text = { Text = "", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                }, ".EditSkin");
                
                container.Add(new CuiElement
                {
                    Parent = ".EditSkin",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_ClanHandler acceptGather {ShortName}", CharsLimit = 10, IsPassword = false,
                            Color = HexToCuiColor("#fcf7f6"), Text = $"{clan.GatherList[ShortName].Need}", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent {AnchorMin = "0.3125 0.4666681", AnchorMax = "0.6875 0.5074074"}
                    }
                });


                CuiHelper.AddUi(player, container);

                container.Clear();
            }

            #endregion

            #region LoadInfoClan

            if (args.Args[0] == "loadInfoClan")
            {
                ClanData clan;

                if (args.Args[1] == "myClanLoad")
                {
                    clan = FindClanByUser(player.userID);
                }
                else clan = FindClanByTag(args.Args[1]);
                
                if (clan == null) return;

                CuiHelper.DestroyUi(player, Layer);
                
                LoadInfoClan(player, clan);
            }

            #endregion

            #region InfoMember

            else if (args.Args[0] == "infoPlayer") { ulong member = 0; if (ulong.TryParse(args.Args[1], out member)) { LoadInfoMember(player, member); } }

            #endregion

            #region Page List

            else if (args.Args[0] == "PageInfoClan") { var clan = FindClanByTag(args.Args[1]); if (clan == null) return; int page = 1; if (int.TryParse(args.Args[2], out page)) { LoadMemberInfoClan(player, clan, page, true); } }
            else if (args.Args[0] == "pageEditSkin") { string ShortName = args.Args[1]; int page = 1; if (int.TryParse(args.Args[2], out page)) { LoadEditSkin(player, ShortName, page, true); } }
            else if (args.Args[0] == "pageClanTop") { int page = 1; if (int.TryParse(args.Args[1], out page)) { LoadClanList(player, page, true); } }
            else if (args.Args[0] == "pageClanMember") { int page = 1; if (int.TryParse(args.Args[1], out page)) { LoadMemberList(player, page, true); } }
            
            #endregion

            #region Moderator

            else if (args.Args[0] == "giveModer")
            {
                ulong parse = ulong.Parse(args.Args[1]);
                var clan = FindClanByUser(player.userID);
                if (clan == null) return;
                if (!clan.IsOwner(player.userID)) return;
                if (clan.IsModerator(parse)) return;
                if (clan.Moderators.Count >= config.Main.MaxCountModeratorClan)
                {
                    player.ChatMessage(GetLang("ClanLimitModerator", player.UserIDString));
                    return;
                }
                clan.Moderators.Add(parse);

                CuiHelper.DestroyUi(player, ".EditSkin");
                
                //LoadInfoMember(player, parse);
                
            } else if (args.Args[0] == "removeModer")
            {
                ulong parse = ulong.Parse(args.Args[1]);
                var clan = FindClanByUser(player.userID);
                if (clan == null) return;
                if (!clan.IsOwner(player.userID)) return;
                if (!clan.IsModerator(parse)) return;
                clan.Moderators.Remove(parse);

                CuiHelper.DestroyUi(player, ".EditSkin");
                
                //LoadInfoMember(player, parse);
            }

            #endregion
        }

        #endregion
        
        #endregion
        
        #region UI

        #region TopUI

        public void MainTopUI(BasePlayer player)
        {
            if (!playerActive.Contains(player.userID))
                playerActive.Add(player.userID);
            
            player.SetFlag(BaseEntity.Flags.Reserved3, true);
            CuiHelper.DestroyUi(player, Layer);
            
            #region Panel
            
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer);
            

            #endregion
            
            #region Close

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9"},
                Text = { Text = "" }
            }, Layer);
            

            #endregion
            
            #region Main

            container.Add(new CuiButton // Main Panel
            {
                RectTransform = { AnchorMin = "0.265625 0.3055556", AnchorMax = "0.734375 0.8333333" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);
            
            container.Add(new CuiButton // Information
            {
                RectTransform = { AnchorMin = "0.265625 0.1666667", AnchorMax = "0.734375 0.3018518" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);
            
            #endregion
            
            #region Text

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = "#", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.2677084 0.8018518", AnchorMax = "0.3 0.8333333" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_TopName", player.UserIDString), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3124994 0.8018518", AnchorMax = "0.4182292 0.8333333" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_TopTournament", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.4307268 0.8018518", AnchorMax = "0.4880208 0.8333333" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_TopReward", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.5135381 0.8018518", AnchorMax = "0.570832 0.8333333" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_TopScores", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.5932244 0.8018518", AnchorMax = "0.6604167 0.8333333" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_TopPlayer", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.6671816 0.8018518", AnchorMax = "0.7317709 0.8333333" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_TopInformation", player.UserIDString), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.2692708 0.1722222", AnchorMax = "0.7296875 0.2962963" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            

            #endregion

            #region Button

            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.4526042 0.3111111", AnchorMax = "0.5473958 0.3481482"}, Button = { Command = "UI_ClanHandler loadInfoClan myClanLoad", Color = HexToCuiColor("#60891B", 100), Material = "assets/content/ui/uibackgroundblur.mat" }, Text = { Text = "МОЙ КЛАН", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
            }, Layer);
            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.6406222 0.3111111", AnchorMax = "0.7317709 0.3481482"}, Button = { Close = Layer, Color = HexToCuiColor("#fffdfc", 20), Material = "assets/content/ui/uibackgroundblur.mat", Command = "UI_ClanHandler close"}, Text = { Text = "ЗАКРЫТЬ", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
            }, Layer);

            #endregion

            CuiHelper.AddUi(player, container);

            #region Load

            LoadClanList(player, 1);

            #endregion
            
            container.Clear();
        }

        #endregion
        
        #region Main
        
        public void MainUI(BasePlayer player)
        {
            
            if (!playerActive.Contains(player.userID))
                 playerActive.Add(player.userID);
            
            CuiHelper.DestroyUi(player, Layer);
            
            #region FindClan

            var clan = FindClanByUser(player.userID);
            if (clan == null) return;

            #endregion
            
            #region Panel
            
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer);

            #endregion

            #region Close

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9", Command = "UI_ClanHandler close"},
                Text = { Text = "" }
            }, Layer);
            

            #endregion
            
            #region Main

            container.Add(new CuiButton // Information
            {
                RectTransform = { AnchorMin = "0.1848958 0.6824074", AnchorMax = "0.6083333 0.9268519" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);
            
            container.Add(new CuiButton // Task
            {
                RectTransform = { AnchorMin = "0.6109375 0.6824074", AnchorMax = "0.81875 0.9268519" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);
            
            container.Add(new CuiButton // Skin
            {
                RectTransform = { AnchorMin = "0.1848958 0.4740741", AnchorMax = "0.81875 0.6768519" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);
            
            container.Add(new CuiButton // Members
            {
                RectTransform = { AnchorMin = "0.1848958 0.07500002", AnchorMax = "0.6078125 0.4694445" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);
            
            container.Add(new CuiButton // Gather
            {
                RectTransform = { AnchorMin = "0.6105 0.07500002", AnchorMax = "0.81875 0.4694445" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);

            #endregion

            #region Avatar

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage", clan.ImageAvatar),
                    },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.2041667 0.7157407", AnchorMax = "0.3026042 0.8944445" }
                }
            });

            #endregion

            #region Information

            var dictionary = Pool.Get<Dictionary<string, string>>();
            dictionary = clan.GetInformation;

            foreach (var check in dictionary.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"0.31875 {0.8490741 - Math.Floor((double)check.B / 1) * 0.038}",
                        AnchorMax = $"0.5901042 {0.8805556 - Math.Floor((double)check.B / 1) * 0.038}",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#000000FF", 50),
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{check.B}.Information");

                container.Add(new CuiElement() // Key
                {
                    Parent = Layer + $".{check.B}.Information",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Key}", Align = TextAnchor.MiddleLeft,
                            FontSize = 14, Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.01151645 0", AnchorMax = "0.6238005 1" },
                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                    }
                });

                container.Add(new CuiElement() // Value
                {
                    Parent = Layer + $".{check.B}.Information",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Value}", Align = TextAnchor.MiddleRight,
                            FontSize = 14, Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.6602686 0", AnchorMax = "0.9788868 1" },
                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                    }
                });

            }

            Pool.Free(ref dictionary);

            #endregion

            #region Task

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_TaskContent", player.UserIDString), Align = TextAnchor.MiddleCenter,
                        FontSize = 16, Font = "robotocondensed-bold.ttf"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.6109375 0.8879629", AnchorMax = "0.81875 0.9268519" },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                }
            });

            string text = string.IsNullOrEmpty(clan.Task) ? GetLang("UI_TaskMessageContent", player.UserIDString) : clan.Task;
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.6265625 0.7027778", AnchorMax = "0.8046875 0.8879629" },
                Button = { Command = $"", Color = HexToCuiColor($"#000000FF", 50) },
                Text = { Color = HexToCuiColor("#fcf7f6"), Text = text, Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" },

            }, Layer);

            #endregion
            
            #region Members

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = "#", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.1880208 0.4453703", AnchorMax = "0.2020833 0.4713015" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_NameContent", player.UserIDString), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.20625 0.4453703", AnchorMax = "0.2869792 0.4713015" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_ActivityContent", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3578114 0.4453703", AnchorMax = "0.4494792 0.4713015" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_StandartContent", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.460414 0.4453703", AnchorMax = "0.5057291 0.4713015" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_ScoresContent", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.5296838 0.4453703", AnchorMax = "0.5958334 0.4713015" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });

            #endregion
            
            CuiHelper.AddUi(player, container);

            #region Load

            LoadMemberList(player, 1);
            
            LoadSkin(player, clan);
            
            LoadGatherList(player, clan);

            #endregion

            container.Clear();
        }
        
        #endregion

        #region LoadSkin

        public void LoadSkin(BasePlayer player, ClanData clan, bool refresh = false)
        {
            
            #region Destroy

            if (refresh)
            {
                for (int i = 0; i < 7; i++)
                {
                    CuiHelper.DestroyUi(player, Layer + $".{i}.SkinList");
                }
            }

            #endregion
            
            var container = new CuiElementContainer();

            string command = clan.IsOwner(player.userID) ? $"UI_ClanHandler editSkin " : String.Empty;
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_ClothContent", player.UserIDString), Align = TextAnchor.MiddleCenter,
                        FontSize = 16, Font = "robotocondensed-bold.ttf"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.1848958 0.637963", AnchorMax = "0.81875 0.6768519" },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                }
            });
            
            foreach (var check in clan.ItemList.Select((i, t) => new { A = i, B = t }).Take(7))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{0.2078125 + check.B * 0.085 - Math.Floor((double)check.B / 7) * 7 * 0.085} 0.4962963",
                        AnchorMax = $"{0.2875 + check.B * 0.085 - Math.Floor((double)check.B / 7) * 7 * 0.085} 0.637037",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#000000FF", 50),
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{check.B}.SkinList");

                int itemID = ItemID[check.A.Key];
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.SkinList",
                    Components =
                    {
                        new CuiImageComponent { ItemId = itemID, SkinId = check.A.Value },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                    }
                });
                
                container.Add(new CuiButton { RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"}, Button = { Command = !string.IsNullOrEmpty(command) ? command + $"{check.A.Key}" : "",  Color = "0 0 0 0", }, Text = { Text = "", Color = HexToCuiColor("#FFFFFA"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf" }, }, Layer + $".{check.B}.SkinList");

            }

            CuiHelper.AddUi(player, container);
            
            container.Clear();

        }

        #endregion

        #region LoadMemberList


        public void LoadMemberList(BasePlayer player, int page, bool refresh = false)
        {
            #region FindClan

            var clan = FindClanByUser(player.userID);
            if (clan == null) return;

            #endregion

            #region Variables

            var container = new CuiElementContainer();
            int pagex = page + 1;
            
            #endregion
            
            #region Page
            
            CuiHelper.DestroyUi(player, Layer + ".Page");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2041669 0.08148149", AnchorMax = "0.23125 0.1101904" }, Button = { Color = HexToCuiColor("#000000", 50), }, Text = { Text = $"{page}", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, Layer, Layer + ".Page");
            
            CuiHelper.DestroyUi(player, Layer + ".Forward");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2312506 0.08148149", AnchorMax = "0.2473964 0.1101904" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = pagex > 0 && (pagex - 1) * 9 < clan.Members.Count ? $"UI_ClanHandler pageClanMember {page + 1}" : "" }, Text = { Text = "+", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, Layer, Layer + ".Forward");
            
            CuiHelper.DestroyUi(player, Layer + ".Back");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1880209 0.08148149", AnchorMax = "0.2041667 0.1101904" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = page != 1 ? $"UI_ClanHandler pageClanMember {page - 1}" : "" }, Text = { Text = "-", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, Layer, Layer + ".Back");
            

            #endregion
            
            #region Destroy
            

            if (refresh)
            {

                for (int i = 0; i < 9; i++)
                {
                    CuiHelper.DestroyUi(player, Layer + $".{i}.MemberList");
                }
            }
            
            #endregion

            #region Load
            
            string command = clan.IsModerator(player.userID) ? $"UI_ClanHandler infoPlayer " : String.Empty;

            foreach (var check in clan.Members.Select((i, t) => new { A = i, B = t - (page - 1) * 9 }).Skip((page - 1) * 9).Take(9))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"0.1880208 {0.412963 - Math.Floor((float)check.B / 1) * 0.037}",
                        AnchorMax = $"0.60625 {0.4453703 - Math.Floor((float)check.B / 1) * 0.037}",
                    },
                    Button = { Color = HexToCuiColor($"#000000FF", 50), Command = !string.IsNullOrEmpty(command) ? command + $"{check.A.Key}" : "", }, Text = { Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15 }
                }, Layer, Layer + $".{check.B}.MemberList");

                #region MemberInfo

                var memberPlayer = BasePlayer.Find(check.A.Key.ToString());
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.MemberList", // Online
                    Components = { new CuiTextComponent { Text = memberPlayer != null ? "<color=#80FF00>●</color>" : "<color=#CE4B4B>●</color>", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0 0.1142852", AnchorMax = "0.03362386 0.8857148" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                string name = covalence.Players.FindPlayer(check.A.Key.ToString()) == null
                    ? "Имя неизвестно"
                    : covalence.Players.FindPlayer(check.A.Key.ToString()).Name;

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.MemberList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = name, Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.04358652 0.1142852", AnchorMax = "0.2353673 0.8857149" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.MemberList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = memberPlayer != null ? $"{GetFormatTime(TimeSpan.FromSeconds(memberPlayer.TimeAlive()))}" : $"{GetFormatTime(TimeSpan.FromSeconds(check.A.Value.AliveTime))}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.4059766 0.1142852", AnchorMax = "0.6251557 0.8857149" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                int percent = clan.TotalAmountFarm(check.A.Key);
                if (percent > 100)
                    percent = 100;
                if (percent < 0)
                    percent = 100;

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.MemberList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{percent}%", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.6513046 0.1142852", AnchorMax = "0.7596512 0.8857149" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.MemberList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Value.MemberScores}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.8169321 0.1142852", AnchorMax = "0.9763387 0.8857149" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });
                

                #endregion
            }

            #endregion

            CuiHelper.AddUi(player, container);
            
            container.Clear();
        }

        #endregion

        #region LoadGatherList\
        public void LoadGatherList(BasePlayer player, ClanData clan, bool refresh = false)
        {
            #region Destroy

            if (refresh)
            {
                for (int i = 0; i < 9; i++)
                {
                    CuiHelper.DestroyUi(player, Layer + $".{i}.GatherList");
                }
            }

            #endregion
            
            string command = clan.IsOwner(player.userID) ? $"UI_ClanHandler editGather " : String.Empty;
            
            var container = new CuiElementContainer();
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_GatherContent", player.UserIDString), Align = TextAnchor.MiddleCenter,
                        FontSize = 16, Font = "robotocondensed-bold.ttf"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.6105 0.4222222", AnchorMax = "0.81875 0.4694445" },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                }
            });
            
            foreach (var check in clan.GatherList.Select((i, t) => new { A = i, B = t }).Take(9))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{0.6244792 + check.B * 0.061 - Math.Floor((double)check.B / 3) * 3 * 0.061} {0.3175926 - Math.Floor((float)check.B / 3) * 0.108}",
                        AnchorMax = $"{0.6838542 + check.B * 0.061 - Math.Floor((double)check.B / 3) * 3 * 0.061} {0.4231481 - Math.Floor((float)check.B / 3) * 0.108}",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#000000FF", 50),
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{check.B}.GatherList");

                object itemID = ItemID.ContainsKey(check.A.Key) ? (object) ItemID[check.A.Key] : check.A.Key;

                if (itemID is int)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}.GatherList",
                        Components = { new CuiImageComponent { ItemId = (int) itemID, SkinId = 0 }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"} } 
                    }); 
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}.GatherList",
                        Components = {new CuiRawImageComponent {Png = (string)ImageLibrary.Call("GetImage", (string) itemID)}, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"} } 
                    }); 
                }
                
                container.Add(new CuiElement()
                {
                    Parent = Layer + $".{check.B}.GatherList",
                    Components =
                    {
                        new CuiTextComponent{Color = HexToCuiColor("#fcf7f6"),Text = $"x{check.A.Value.Need}", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 2", OffsetMax = "-3 0"},
                    }
                });
                
                container.Add(new CuiButton { RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"}, Button = { Command = !string.IsNullOrEmpty(command) ? command + $"{check.A.Key}" : "",  Color = "0 0 0 0", }, Text = { Text = "", Color = HexToCuiColor("#FFFFFA"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf" }, }, Layer + $".{check.B}.GatherList");

            }

            CuiHelper.AddUi(player, container);
            
            container.Clear();
        }

        #endregion

        #region LoadEditSkin

        public void LoadEditSkin(BasePlayer player, string ShortName, int page, bool refresh = false)
        {
            #region Variables

            var container = new CuiElementContainer();
            var SkinList = config.ItemSkin.ItemsSkins[ShortName];
            int itemID = ItemManager.FindItemDefinition(ShortName).itemid;

            int pagex = page + 1;

            #endregion
            
            #region Destroy

            if (refresh)
            {
                for (int i = 0; i < 72; i++)
                {
                    CuiHelper.DestroyUi(player, $".{i}.SkinListEdit");
                }
            }
            
            #endregion
            
            #region Page
            
            CuiHelper.DestroyUi(player, ".EditSkin" + ".Page");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2635414 0.1425926", AnchorMax = "0.2874997 0.1722222" }, Button = { Color = HexToCuiColor("#000000", 50), }, Text = { Text = $"{page}", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, ".EditSkin", ".EditSkin" + ".Page");
            
            CuiHelper.DestroyUi(player, ".EditSkin" + ".Forward");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2874996 0.1425926", AnchorMax = "0.3057283 0.1722222" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = pagex > 0 && (pagex - 1) * 72 < SkinList.Count ? $"UI_ClanHandler pageEditSkin {ShortName} {page + 1}" : "" }, Text = { Text = "+", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, ".EditSkin", ".EditSkin" + ".Forward");
            
            CuiHelper.DestroyUi(player, ".EditSkin" + ".Back");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2453122 0.1425926", AnchorMax = "0.2635414 0.1722222" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = page != 1 ? $"UI_ClanHandler pageEditSkin {ShortName} {page - 1}" : "" }, Text = { Text = "-", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, ".EditSkin", ".EditSkin" + ".Back");
            

            #endregion
            
            #region Load

            foreach (var check in SkinList.Select((i, t) => new { A = i, B = t - (page - 1) * 72 }).Skip((page - 1) * 72).Take(72))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{0.2447917 + check.B * 0.0572 - Math.Floor((double)check.B / 9) * 9 * 0.0572} {0.7472222 - Math.Floor((float)check.B / 9) * 0.080}",
                        AnchorMax = $"{0.2973958 + check.B * 0.0572 - Math.Floor((double)check.B / 9) * 9 * 0.0572} {0.8212963 - Math.Floor((float)check.B / 9) * 0.080}",
                    },
                    Button = { Color = HexToCuiColor($"#000000FF", 50), Command = $"", }, Text = { Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15 }
                }, ".EditSkin", $".{check.B}.SkinListEdit");
                
                container.Add(new CuiElement
                {
                    Parent = $".{check.B}.SkinListEdit",
                    Components = { new CuiImageComponent { ItemId = itemID, SkinId = check.A }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"} } 
                });
                
                container.Add(new CuiButton { RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"}, Button = { Command = $"UI_ClanHandler acceptSkin {ShortName} {check.A}", Color = "0 0 0 0", }, Text = { Text = "", Color = HexToCuiColor("#FFFFFA"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf" }, }, $".{check.B}.SkinListEdit");

            }

            #endregion

            CuiHelper.AddUi(player, container);
            
            container.Clear();
        }

        #endregion

        #region LoadClanList

        public void LoadClanList(BasePlayer player, int page, bool refresh = false)
        {
            #region Variables

            var container = new CuiElementContainer();
            int pagex = page + 1;

            #endregion
            
            #region Destroy

            if (refresh)
            {
                for (int i = 0; i < 10; i++)
                {
                    CuiHelper.DestroyUi(player, Layer + $".{i}.ClanList");
                }
            }
            
            #endregion
            
            #region Page
            
            CuiHelper.DestroyUi(player, Layer + ".Page");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2890623 0.3148148", AnchorMax = "0.3130206 0.3444444" }, Button = { Color = HexToCuiColor("#000000", 50), }, Text = { Text = $"{page}", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, Layer, Layer + ".Page");
            
            CuiHelper.DestroyUi(player, Layer + ".Forward");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3130205 0.3148148", AnchorMax = "0.3312492 0.3444444" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = pagex > 0 && (pagex - 1) * 10 < _clanList.Count ? $"UI_ClanHandler pageClanTop {page + 1}" : "" }, Text = { Text = "+", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, Layer, Layer + ".Forward");
            
            CuiHelper.DestroyUi(player, Layer + ".Back");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2708331 0.3148148", AnchorMax = "0.2890623 0.3444444" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = page != 1 ? $"UI_ClanHandler pageClanTop {page - 1}" : "" }, Text = { Text = "-", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, Layer, Layer + ".Back");
            
            #endregion
            
            #region Load

            foreach (var check in _clanList.OrderByDescending(p => p.TotalScores).Select((i, t) => new { A = i, B = t - (page - 1) * 10 }).Skip((page - 1) * 10).Take(10))
            {
                bool OnlineClan = false;

                foreach (var clanMember in check.A.Members.Keys)
                {
                    if (BasePlayer.Find(clanMember.ToString()) != null)
                    {
                        OnlineClan = true;
                        break;
                    }
                }

                int index = (page - 1) * 10 + check.B + 1;

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"0.2677083 {0.7583333 - Math.Floor((float)check.B / 1) * 0.044}",
                        AnchorMax = $"0.7317709 {0.7990741 - Math.Floor((float)check.B / 1) * 0.044}",
                    },
                    Button = { Color = HexToCuiColor($"#000000FF", 50), Command = $"UI_ClanHandler loadInfoClan {check.A.ClanTag}", }, Text = { Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15 }
                }, Layer, Layer + $".{check.B}.ClanList");

                #region ClanInfo
                

                string text = OnlineClan == true ? $"<color=#80FF00>●</color> {check.A.ClanTag}" : $"<color=#CE4B4B>●</color> {check.A.ClanTag}";

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.ClanList", // Online
                    Components = { new CuiTextComponent { Text = $"{index}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0 0.113636", AnchorMax = "0.06958482 0.886364" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.ClanList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = text, Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.09652069 0.113636", AnchorMax = "0.3243547 0.886364" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                string cupBoard = TournamentBoloto == null ? "-" : (bool) TournamentBoloto.CallHook("GetActive", check.A.ClanTag) == true ? "+" : "-";

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.ClanList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = cupBoard, Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3512905 0.113636", AnchorMax = "0.4747475 0.886364" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                uint amount = 0;

                if (config.Prize.RewardDictionary.TryGetValue(index, out amount))
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}.ClanList",
                        Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{amount}руб.", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.529741 0.113636", AnchorMax = "0.6532013 0.886364" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.ClanList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.TotalScores}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.7014614 0.113636", AnchorMax = "0.8462401 0.886364" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.ClanList",
                    Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Members.Count}/{config.Main.MaxCountClan}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.8608359 0.113636", AnchorMax = "1 0.886364" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                #endregion
            }

            #endregion

            CuiHelper.AddUi(player, container);

            container.Clear();
        }
        

        #endregion

        #region LoadInfoClan

        public void LoadInfoClan(BasePlayer player, ClanData clan)
        {
            var container = new CuiElementContainer();
            
            #region Panel

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer);
            
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                            Button = { Color = "0 0 0 0.9"},
                            Text = { Text = "" }
                        }, Layer);

            #endregion

            #region Main

            container.Add(new CuiButton // Main
            {
                RectTransform = { AnchorMin = "0.3 0.1953704", AnchorMax = "0.7020833 0.8055556" },
                Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, Layer);
            
            #endregion
            
            #region Button
            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.6062484 0.205555", AnchorMax = "0.6999984 0.2435179"}, Button = { Command = "UI_ClanHandler close", Color = HexToCuiColor("#fffdfc", 20), Material = "assets/content/ui/uibackgroundblur.mat",}, Text = { Text = "ЗАКРЫТЬ", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
            }, Layer);

            #endregion
            
            #region Avatar

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage", clan.ImageAvatar),
                    },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.3270833 0.5805556", AnchorMax = "0.4265625 0.7601852" }
                }
            });

            #endregion

            #region Name
            
            bool OnlineClan = false;

            foreach (var clanMember in clan.Members.Keys)
            {
                if (BasePlayer.Find(clanMember.ToString()) != null)
                {
                    OnlineClan = true;
                    break;
                }
            }

            string text = OnlineClan == true ? $"<color=#80FF00>●</color> {clan.ClanTag}" : $"<color=#CE4B4B>●</color> {clan.ClanTag}";
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = text, Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3 0.7601852", AnchorMax = "0.7020833 0.8055556" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });

            #endregion
            
            #region Text

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = "#", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3026042 0.4203704", AnchorMax = "0.3302083 0.4481481" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_InfoClanName", player.UserIDString), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.3442702 0.4203704", AnchorMax = "0.425 0.4481481" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_InfoClanScores", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.4932263 0.4203704", AnchorMax = "0.5369791 0.4481481" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_InfoClanFarm", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.5369756 0.4203704", AnchorMax = "0.596875 0.4481481" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_InfoClanKill", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.5968705 0.4203704", AnchorMax = "0.6463541 0.4481481" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_InfoClanDeath", player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.6463541 0.4203704", AnchorMax = "0.6958333 0.4481481" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });

            #endregion
            
            #region Information

            var dictionary = Pool.Get<Dictionary<string, string>>();
            dictionary = clan.GetInformationInfo;

            foreach (var check in dictionary.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"0.4479167 {0.7314815 - Math.Floor((double)check.B / 1) * 0.030}",
                        AnchorMax = $"0.6796875 {0.7583333 - Math.Floor((double)check.B / 1) * 0.030}",
                    },
                    Button = { Color = HexToCuiColor($"#000000FF", 50), }, Text = { Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15 }
                }, Layer, Layer + $".{check.B}.Information");

                container.Add(new CuiElement() // Key
                {
                    Parent = Layer + $".{check.B}.Information",
                    Components =
                    {
                        new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Key}", Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.01151645 0", AnchorMax = "0.6238005 1" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
                });

                container.Add(new CuiElement() // Value
                {
                    Parent = Layer + $".{check.B}.Information",
                    Components =
                    {
                        new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Value}", Align = TextAnchor.MiddleRight, FontSize = 14, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.6602686 0", AnchorMax = "0.9788868 1" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                    }
                });

            }

            Pool.Free(ref dictionary);

            #endregion

            #region Gather

            foreach (var check in clan.GatherList.Select((i, t) => new { A = i, B = t }).Take(9))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{0.3104168 + check.B * 0.043 - Math.Floor((double)check.B / 9) * 9 * 0.043} {0.475 - Math.Floor((float)check.B / 9) * 0.108}",
                        AnchorMax = $"{0.3479167 + check.B * 0.043 - Math.Floor((double)check.B / 9) * 9 * 0.043} {0.5435185 - Math.Floor((float)check.B / 9) * 0.108}",
                    },
                    Button = { Color = HexToCuiColor($"#000000FF", 50), }, Text = { Text = $"", }
                }, Layer, Layer + $".{check.B}.GatherList");

                object itemID = ItemID.ContainsKey(check.A.Key) ? (object) ItemID[check.A.Key] : check.A.Key;

                if (itemID is int)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}.GatherList", Components = { new CuiImageComponent { ItemId = (int) itemID, SkinId = 0 }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"} } 
                    }); 
                }
                else
                {
                    container.Add(new CuiElement
                    { 
                        Parent = Layer + $".{check.B}.GatherList", Components = {new CuiRawImageComponent {Png = (string)ImageLibrary.Call("GetImage", (string) itemID)}, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"} } 
                    }); 
                }
                
                container.Add(new CuiElement()
                {
                    Parent = Layer + $".{check.B}.GatherList", Components = { new CuiTextComponent{Color = HexToCuiColor("#fcf7f6"),Text = $"x{check.A.Value.TotalFarm}", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf"}, new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 2", OffsetMax = "-3 0"}, }
                });
                
            }

            #endregion

            CuiHelper.AddUi(player, container);

            container.Clear();

            #region Load

            LoadMemberInfoClan(player, clan ,1);

            #endregion
        }
        

        #endregion
        
        #region LoadMemberInfoClan

        public void LoadMemberInfoClan(BasePlayer player, ClanData clan, int page, bool refresh = false)
        {
            #region Variables

            var container = new CuiElementContainer();
            int pagex = page + 1;

            #endregion
            
            #region Destroy
            
            if (refresh)
            {
                for (int i = 0; i < 5; i++)
                {
                    CuiHelper.DestroyUi(player, Layer + $".{i}.MemberListInfo");
                }
            }
            
            #endregion
            
            #region Page
            
            CuiHelper.DestroyUi(player, Layer + ".Page");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3223956 0.2092592", AnchorMax = "0.3463539 0.2388887" }, Button = { Color = HexToCuiColor("#000000", 50), }, Text = { Text = $"{page}", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, Layer, Layer + ".Page");
            
            CuiHelper.DestroyUi(player, Layer + ".Forward");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3463538 0.2092592", AnchorMax = "0.3645825 0.2388887" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = pagex > 0 && (pagex - 1) * 10 < _clanList.Count ? $"UI_ClanHandler PageInfoClan {clan.ClanTag} {page + 1}" : "" }, Text = { Text = "+", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, Layer, Layer + ".Forward");
            
            CuiHelper.DestroyUi(player, Layer + ".Back");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3041664 0.2092592", AnchorMax = "0.3223956 0.2388887" }, Button = { Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = page != 1 ? $"UI_ClanHandler PageInfoClan {clan.ClanTag} {page - 1}" : "" }, Text = { Text = "-", Color = HexToCuiColor("#fcf7f6"), Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, Layer, Layer + ".Back");
            
            #endregion
            
            #region Load

            foreach (var check in clan.Members.OrderByDescending(p => p.Value.MemberScores).Select((i, t) => new { A = i, B = t - (page - 1) * 5 }).Skip((page - 1) * 5).Take(5))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"0.3026041 {0.3888889 - Math.Floor((float)check.B / 1) * 0.035}",
                        AnchorMax = $"0.7005209 {0.4212963 - Math.Floor((float)check.B / 1) * 0.035}",
                    },
                    Button = { Color = HexToCuiColor($"#000000FF", 50), Command = $"", }, Text = { Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15 }
                }, Layer, Layer + $".{check.B}.MemberListInfo");
                
                #region MemberInfo
                
                
                int index = (page - 1) * 5 + check.B + 1;
                
            container.Add(new CuiElement
            {
                Parent = Layer + $".{check.B}.MemberListInfo",
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{index}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0 0.05714303", AnchorMax = "0.06937178 0.942857" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer + $".{check.B}.MemberListInfo",
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = covalence.Players.FindPlayer(check.A.Key.ToString()).Name, Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.1047121 0.05714303", AnchorMax = "0.3075917 0.942857" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer + $".{check.B}.MemberListInfo",
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Value.MemberScores}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.4790558 0.05714303", AnchorMax = "0.5890051 0.942857" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });

            int percent = clan.TotalAmountFarm(check.A.Key);
            if (percent > 100)
                percent = 100;
            if (percent < 0)
                percent = 0;

            container.Add(new CuiElement
            {
                Parent = Layer + $".{check.B}.MemberListInfo",
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{percent}%", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.5890044 0.05714303", AnchorMax = "0.7395288 0.942857" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer + $".{check.B}.MemberListInfo",
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Value.MemberKill}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.73953 0.05714303", AnchorMax = "0.8638742 0.942857" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer + $".{check.B}.MemberListInfo",
                Components = { new CuiTextComponent { Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Value.MemberDeath}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }, new CuiRectTransformComponent { AnchorMin = "0.8638772 0.05714303", AnchorMax = "0.9882214 0.942857" }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" }, }
            });
            
            #endregion
            }
            

            #endregion 

            CuiHelper.AddUi(player, container);

            container.Clear();
        }

        #endregion

        #region LoadInfoMember

        public void LoadInfoMember(BasePlayer player, ulong member)
        {
            #region ClanFind

            var clan = FindClanByUser(player.userID);
            if (clan == null) return;

            #endregion

            var container = new CuiElementContainer();

            #region Panel

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer, ".EditSkin");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9", Close = ".EditSkin" },
                Text = { Text = "" }
            }, ".EditSkin");

            #endregion

            #region Main


            container.Add(new CuiButton // Main 1
            {
                RectTransform = { AnchorMin = "0.2036459 0.3333333", AnchorMax = "0.6 0.6601852" }, Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" }, Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" },

            }, ".EditSkin");

            container.Add(new CuiButton // Main 2
            {
                RectTransform = { AnchorMin = "0.6020833 0.3333333", AnchorMax = "0.776039 0.6601852" }, Button = { Command = $"", Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat" }, Text = { Text = "", Color = HexToCuiColor("#fffdfc"), Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" }, 
            }, ".EditSkin");

            #endregion

            #region Avatar

            container.Add(new CuiElement
            {
                Parent = ".EditSkin",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage", $"avatar_{member}"),
                    },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.2213542 0.4481481", AnchorMax = "0.3234375 0.6287037" }
                }
            });

            #endregion

            #region Info

            int percent = clan.TotalAmountFarm(member);
            if (percent > 100)
                percent = 100;
            
            var dictionary =  new Dictionary<string, string>
            {
                ["НИК ИГРОКА:"] = covalence.Players.FindPlayer(member.ToString()).Name,
                ["СТИМ ИГРОКА:"] = member.ToString(),
                ["ПОСЛЕДНИЙ ВХОД:"] = clan.Members[member].LastTime,
                ["ВЫПОЛНЕННАЯ НОРМА:"] = $"{percent}%",
            };
            
            foreach (var check in dictionary.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"0.3421875 {0.5712963 - Math.Floor((double)check.B / 1) * 0.033}",
                        AnchorMax = $"0.5817708 {0.6018527 - Math.Floor((double)check.B / 1) * 0.033}",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#000000FF", 50),
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, ".EditSkin", ".EditSkin" + $".{check.B}.Information");

                container.Add(new CuiElement() // Key
                {
                    Parent = ".EditSkin" + $".{check.B}.Information",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Key}", Align = TextAnchor.MiddleLeft,
                            FontSize = 14, Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.01151645 0", AnchorMax = "0.6238005 1" },
                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                    }
                });

                container.Add(new CuiElement() // Value
                {
                    Parent = ".EditSkin" + $".{check.B}.Information",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = HexToCuiColor("#fcf7f6"), Text = $"{check.A.Value}", Align = TextAnchor.MiddleRight,
                            FontSize = 14, Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.6602686 0", AnchorMax = "0.9788868 1" },
                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                    }
                });

            }

            Pool.Free(ref dictionary);

            #endregion

            #region Gather

            container.Add(new CuiElement
            {
                Parent = ".EditSkin",
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = HexToCuiColor("#fcf7f6"), Text = GetLang("UI_GatherContent", player.UserIDString), Align = TextAnchor.MiddleCenter,
                        FontSize = 16, Font = "robotocondensed-bold.ttf"
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.6020833 0.6212963", AnchorMax = "0.776039 0.6601852" },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.1 0.1" },
                }
            });
            
            foreach (var check in clan.Members[member].GatherMember.Select((i, t) => new { A = i, B = t }).Take(9))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{0.6130209 + check.B * 0.0515 - Math.Floor((double)check.B / 3) * 3 * 0.0515} {0.5296296 - Math.Floor((float)check.B / 3) * 0.094}",
                        AnchorMax = $"{0.6630208 + check.B * 0.0515 - Math.Floor((double)check.B / 3) * 3 * 0.0515} {0.6212963 - Math.Floor((float)check.B / 3) * 0.094}",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#000000FF", 50),
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, ".EditSkin", ".EditSkin" + $".{check.B}.GatherList");

                object itemID = ItemID.ContainsKey(check.A.Key) ? (object) ItemID[check.A.Key] : check.A.Key;

                if (itemID is int)
                {
                    container.Add(new CuiElement
                    {
                        Parent = ".EditSkin" + $".{check.B}.GatherList",
                        Components = { new CuiImageComponent { ItemId = (int) itemID, SkinId = 0 }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"} } 
                    }); 
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = ".EditSkin" + $".{check.B}.GatherList",
                        Components = {new CuiRawImageComponent {Png = (string)ImageLibrary.Call("GetImage", (string) itemID)}, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"} } 
                    }); 
                }
                
                container.Add(new CuiElement()
                {
                    Parent = ".EditSkin" + $".{check.B}.GatherList",
                    Components =
                    {
                        new CuiTextComponent{Color = HexToCuiColor("#fcf7f6"),Text = $"x{check.A.Value}", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 2", OffsetMax = "-3 0"},
                    }
                });
            }

            #endregion

            #region Button

            if (clan.IsModerator(player.userID))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.2213542 0.362037", AnchorMax = "0.3234375 0.4009267" },
                    Button = { Command = $"Clan_Command /clan kick {member}", Color = HexToCuiColor($"#000000FF", 50) },
                    Text = { Color = HexToCuiColor("#fcf7f6"), Text = "ВЫГНАТЬ ИЗ КЛАНА", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" },

                }, ".EditSkin");   
            }
            
            if (clan.IsOwner(player.userID))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.3312492 0.362037", AnchorMax = "0.4333317 0.4009267" },
                    Button = { Command = clan.Moderators.Contains(member) == false ? $"UI_ClanHandler giveModer {member}" : $"UI_ClanHandler removeModer {member}", Color = HexToCuiColor($"#000000FF", 50) },
                    Text = { Color = HexToCuiColor("#fcf7f6"), Text = clan.Moderators.Contains(member) == false ? "ВЫДАТЬ МОДЕРА" : "СНЯТЬ С МОДЕРА", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" },

                }, ".EditSkin");   
            }
            
            if (clan.IsOwner(player.userID))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.4411434 0.362037", AnchorMax = "0.5432259 0.4009267" },
                    Button = { Command = $"Clan_Command /clan owner {member}", Color = HexToCuiColor($"#000000FF", 50) },
                    Text = { Color = HexToCuiColor("#fcf7f6"), Text = "НАЗНАЧИТЬ ГЛАВОЙ", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" },

                }, ".EditSkin");   
            }

            #endregion

            CuiHelper.AddUi(player, container);

            container.Clear();
        }

        #endregion
        
        #endregion
        
        #region Configuration
        
        
        private static Configuration config;

        protected override void LoadDefaultConfig()
        {
            config = Configuration.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            Config.WriteObject(config, true);
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        private class Configuration
        {
            #region Point

            public class PointSettings
            {
                [JsonProperty("Список добываемых предметов и выдаваемое количество очков")]
                public Dictionary<string, int> _gatherPoint;
            
                [JsonProperty("Количество очков за сбитие вертолета")]
                public int Helicopter = 1000;

                [JsonProperty("Количество очков за взрыв танка")]
                public int BradleyAPC = 650;

                [JsonProperty("Добавляемое количество очков при убийстве")]
                public int Kill = 20;

                [JsonProperty("Отбираемое количество очков при смерти")]
                public int Death = 10;

                [JsonProperty("Отбираемое количество очков при суициде")]
                public int Suicide = 30;
            
            }

            #endregion

            #region Main

            public class ClanSettings
            {
                [JsonProperty("Команды")] 
                public List<string> CommandsMain;

                [JsonProperty("Команды для открытия топа")]
                public List<string> CommandsTOP;


                [JsonProperty("Минимальная длина названия клана")]
                public int MinNameLength = 2;
                
                [JsonProperty("Максимальная длина название клана")]
                public int MaxNameLength = 15;
                
                [JsonProperty("Максимальное количество участников в клане")]
                public int MaxCountClan = 7;
                
                [JsonProperty("Максимальное количество модераторов в клане")]
                public int MaxCountModeratorClan = 2;
                
                [JsonProperty("Включить клан теги у игроков?")]
                public bool TagInPlayer = true;
                
                
                [JsonProperty("Автоматическое создание игровой тимы")]
                public bool EnableTeam = true;

                [JsonProperty("Очищать дату при вайпе сервера?")]
                public bool ClearWipe = true;

                [JsonProperty("Включить автоматическую авторизацию в дверях ( соло замки )")]
                public bool AutomaticLock = true;
                
                [JsonProperty("Включить автоматическую авторизацию в шкафах?")]
                public bool AutomaticCupboard = true;

                [JsonProperty("Включить автоматическую авторизацию в турелях?")]
                public bool AutomaticTurret = true;
                
                [JsonProperty("Запретные клан теги")] 
                public List<string> ForbiddenTag;

                [JsonProperty("Начальные добываемые предметы")]
                public List<string> GatherDictionary;
                
                [JsonProperty("Начальныая одежда")]
                public Dictionary<string, ulong> WearDictionary;
                

            }

            #endregion
            
            #region Prize

            public class GameStores
            {
                public class APISettings
                {
                    [JsonProperty("ИД магазина в сервисе")]
                    public string ShopID = "UNDEFINED";
                    [JsonProperty("Секретный ключ (не распространяйте его)")]
                    public string SecretKey = "UNDEFINED";
                }
                
                [JsonProperty("Включить авто выдачу призов при вайпе сервера?")]
                public bool EnablePrize = true;
                
                [JsonProperty("Настройка подключение к GS")]
                public APISettings GSSettings = new APISettings();
                
                [JsonProperty("Место в топе клана и выдаваемый баланс каждому игроку из клана")]
                public Dictionary<int, uint> RewardDictionary;
            }

            #endregion
            
            #region Skin

            public class SkinSettings
            {
                [JsonProperty("Включить ли подгрузку скинов в конфиг при загрузке плагина?")]
                public bool LoadSkinList = true;
                
                [JsonProperty("Скины предметов")] 
                public Dictionary<string, List<ulong>> ItemsSkins;
            }

            #endregion
            
            #region Stats

            public class CollectionStats
            {
                [JsonProperty("Добыча")] 
                public bool Gather = true;

                [JsonProperty("Убийства")] 
                public bool Kill = true;

                [JsonProperty("Лутание")] 
                public bool Loot = true;

                [JsonProperty("Уничтожение объектов ( бочки, вертолет, танк )")]
                public bool Entities = false;
            }

            #endregion

            #region Limit

            public class LimitSettings
            {
                [JsonProperty("Включить лимит кланов?")]
                public bool EnableLimit = true;

                [JsonProperty("Лимит на установку турелей")]
                public int  LTurret = 100;

                [JsonProperty("Лимит на установку ПВО")]
                public int  LSamSite = 10;

                [JsonProperty("Лимит на установку шкафов")]
                public int  LCupboard = 150;
            }

            #endregion
            
            #region Variables

            [JsonProperty("Основная настройка плагина")]
            public ClanSettings Main = new ClanSettings();

            [JsonProperty("Настройка системы очков")]
            public PointSettings Point = new PointSettings();

            [JsonProperty("Настройка системы лимитов")]
            public LimitSettings Limit = new LimitSettings();

            [JsonProperty("Настройка сбора статистики и очков")]
            public CollectionStats Stats = new CollectionStats();

            [JsonProperty("Настройка призов")] 
            public GameStores Prize = new GameStores();
            
            [JsonProperty("Настройка скинов")] 
            public SkinSettings ItemSkin = new SkinSettings();

            #endregion

            #region Loaded

            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    Prize = new GameStores()
                    {
                        RewardDictionary = new Dictionary<int, uint>()
                        {
                            [1] = 100,
                            [2] = 80,
                            [3] = 60,
                            [4] = 40,
                            [5] = 20
                        }
                    },
                    Stats = new CollectionStats(),
                    Main = new ClanSettings()
                    {
                        CommandsMain = new List<string>()
                        {
                            "clan",
                            "clans",
                        },
                        CommandsTOP = new List<string>()
                        {
                            "ctop",
                            "top"
                        },
                        ForbiddenTag = new List<string>()
                        {
                            "[admin]",
                            "[moderator]",
                            "[god]",
                            "[adminteam]"
                        },
                        GatherDictionary = new List<string>()
                        {
                            "wood",
                            "metal.ore",
                            "stones",
                            "sulfur.ore",
                            "hq.metal.ore",
                            "fat.animal",
                            "cloth",
                            "leather",
                            "lootbox",
                         },
                        WearDictionary = new Dictionary<string, ulong>()
                        {
                            ["metal.facemask"] = 0,
                            ["metal.plate.torso"] = 0,
                            ["roadsign.kilt"] = 0,
                            ["hoodie"] = 0,
                            ["pants"] = 0,
                            ["shoes.boots"] = 0,
                            ["rifle.ak"] = 0,
                         }
                    },
                    Point = new PointSettings()
                    {
                        _gatherPoint = new Dictionary<string, int>()
                        {
                            ["wood"] = 2,
                            ["stones"] = 2,
                            ["sulfur.ore"] = 2,
                            ["metal.ore"] = 2,
                            ["hq.metal.ore"] = 1,
                            ["lootbox"] = 1,
                        }
                    },
                    ItemSkin = new SkinSettings()
                    {
                        ItemsSkins = new Dictionary<string, List<ulong>>()
                    }
                };
            }

            #endregion
        }

        #endregion

        #region API

        private ClanData FindClanByUser(ulong playerID)
        {
            return _clanList.FirstOrDefault(clan => clan.IsMember(playerID));
        }
        
        private ClanData FindClanByTag(string tag)
        {
            return _clanList.FirstOrDefault(clan => clan.ClanTag.ToLower() == tag.ToLower());
        }

        private ClanData FindClanByInvite(ulong playerID)
        {
            return _clanList.FirstOrDefault(clan => clan.PendingInvites.Contains(playerID));
        }

        private List<ulong> ApiGetClanMembers(ulong playerId)
        {
            return (List<ulong>)(FindClanByUser(playerId)?.Members.Keys.ToList() ?? new List<ulong>());
        }
        
        private List<ulong> ApiGetClanMembers(string tag)
        {
            return (List<ulong>)(FindClanByTag(tag)?.Members.Keys.ToList() ?? new List<ulong>());
        }
        
        private bool ApiIsClanMember(ulong playerID, ulong friendID)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return false;
            if (clan.IsMember(friendID))
                return true;
            return false;
        }
        
        private object IsClanMember(string userID, string targetID)
        {
            var clan = FindClanByUser(ulong.Parse(userID));
            if (clan == null) return null;
            if (clan.IsMember(ulong.Parse(targetID)))
                return true;
            return null;
        }
        
        private bool ApiIsClanOwner(ulong playerID)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return false;
            if (clan.IsOwner(playerID))
                return true;
            return false;
        }
        
        private bool ApiIsClanModeratorAndOwner(ulong playerID)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return false;
            if (clan.IsModerator(playerID))
                return true;
            return false;
        }
        
        private int? ApiGetMember(ulong playerID)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return null;
            return clan.Members.Count;
        }
        
        private string ApiGetClanTag(ulong playerID)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return null;
            return clan.ClanTag;
        }
        
        private int? ApiGetClanScores(ulong playerID)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return null;
            return clan.TotalScores;
        }

        private void ApiScoresAddClan(ulong playerID, int scores)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return;
            clan.TotalScores += scores;
            clan.Members[playerID].MemberScores += scores;
        }

        private void ApiScoresAddClan(string tag, int scores)
        {
            var clan = FindClanByTag(tag);
            if (clan == null) return;
            clan.TotalScores += scores;
        }

        private void ApiScoresRemove(ulong playerID, int scores)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return;
            clan.TotalScores -= scores;
            clan.Members[playerID].MemberScores -= scores;
        }
        
        private void ApiScoresRemoveClan(string tag, int scores)
        {
            var clan = FindClanByTag(tag);
            if (clan == null) return;
            clan.TotalScores -= scores;
        }
        
        #region API TournamentClan
        
        // Edited: 1.1.1 version

        private void ApiScoresRemoveTournament(string destroyClan, ulong initiatorPlayer, int percent)
        {
            var clanDestroy = FindClanByTag(destroyClan);
            if (clanDestroy == null) return;


            int SumPercent = (clanDestroy.TotalScores / 100) * percent;

            clanDestroy.TotalScores -= SumPercent;
            
            var clanInitiator = FindClanByUser(initiatorPlayer);
            if (clanInitiator == null) return;

            clanInitiator.TotalScores += SumPercent;
            
        }

        #endregion
        
        private List<string> ApiGetClansTags(bool key = true)
        {
            if (_clanList.Count == 0) return null;

            List<string> clantagAll = new List<string>();

            foreach (var clan in _clanList)
            {
                string tagClan = key == true ? clan.ClanTag.ToUpper() : clan.ClanTag;
            
                if (!clantagAll.Contains(tagClan))
                    clantagAll.Add(tagClan);
            }

            return clantagAll;
        }

        private List<ulong> ApiGetActiveClanMembers(BasePlayer player)
        {
            var clan = FindClanByUser(player.userID);
            if (clan == null) return null;
            List<ulong> list = clan.Members.Keys.Where(p => BasePlayer.Find(p.ToString()) != null && p != player.userID).ToList();
            if (list.Count <= 0) return null;
            return list;
        }

        private List<string> GetClanMembers(ulong playerID) =>
            (List<string>)ApiGetActiveClanMembersUserId(playerID).Select(p => p.ToString()) ?? new List<string>();

        private List<ulong> ApiGetActiveClanMembersUserId(ulong playerID)
        {
            var clan = FindClanByUser(playerID);
            if (clan == null) return null;
            List<ulong> list = clan.Members.Keys.Where(p => BasePlayer.Find(p.ToString()) != null & p != playerID).ToList();
            if (list.Count <= 0) return null;
            return list;
        }


        private Dictionary<string, int> GetTops()
        {

            Dictionary<string, int> dictionaryTops = new Dictionary<string, int>();

            int index = 1;

            foreach (var check in _clanList.OrderByDescending(p => p.TotalScores).Take(3))
            {
                if (!dictionaryTops.ContainsKey(check.ClanTag))
                    dictionaryTops.Add(check.ClanTag, check.TotalScores);
                
            }

            return dictionaryTops;
        }

        private List<ulong> ApiGetActiveClanMembersTag(string tag)
        {
            var clan = FindClanByTag(tag);
            if (clan == null) return null;
            List<ulong> list = clan.Members.Keys.Where(p => BasePlayer.Find(p.ToString()) != null).ToList();
            if (list.Count <= 0) return null;
            return list;
        }
        
        private JObject GetClan(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            var clan = FindClanByTag(tag);
            
            if (clan == null) return null;
            
            return clan.ToJObject();
        }

        #endregion
    }
}