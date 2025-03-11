using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("BetterTeams", "ahigao", "1.3.1")]
    internal class BetterTeams : RustPlugin
    {
        #region Static

        private Data _data;
        private float Scale;
        private Configuration _config;
        private static BetterTeams _ins;
        private const string Layer = "UI_BetterTeams";

        private List<BasePlayer> Spectators = new List<BasePlayer>();
        private Dictionary<BasePlayer, DateTime> TipRemove = new Dictionary<BasePlayer, DateTime>();
        private Dictionary<ulong, DateTime> PlayerMarkerCooldown = new Dictionary<ulong, DateTime>();
        private Dictionary<ulong, VoiceTranslator> VoiceTranslators = new Dictionary<ulong, VoiceTranslator>();

        private Dictionary<ulong, List<BasePlayer>> OnlineTeams = new Dictionary<ulong, List<BasePlayer>>();

        private HashSet<CodeLock> CodeLocks = new HashSet<CodeLock>();
        private HashSet<AutoTurret> AutoTurrets = new HashSet<AutoTurret>();
        private HashSet<BuildingPrivlidge> BuildingPrivlidges = new HashSet<BuildingPrivlidge>();
        private Dictionary<SamSite, List<ulong>> SamSiteAuthorization = new Dictionary<SamSite, List<ulong>>();

        private Dictionary<string, string> Categories = new Dictionary<string, string>
        {
            ["Info"] = "assets/icons/connection.png",
            ["Mates"] = "assets/icons/friends_servers.png",
            ["Skins"] = "assets/icons/inventory.png",
            ["Settings"] = "assets/icons/gear.png"
        };

        #region Image

        [PluginReference]
        private Plugin ImageLibrary, Clans;

        private int ILCheck = 0;

        private void LoadImages()
        {
            if (!ImageLibrary.Call<bool>("HasImage", "https://www.dropbox.com/scl/fi/fqskxss8g0b84cv8yfrpg/SFMlnyl.png?rlkey=gvctrkqj1qxip0bnsqgfi6rxy&dl=1"))
                ImageLibrary.Call("AddImage", "https://www.dropbox.com/scl/fi/fqskxss8g0b84cv8yfrpg/SFMlnyl.png?rlkey=gvctrkqj1qxip0bnsqgfi6rxy&dl=1", "https://www.dropbox.com/scl/fi/fqskxss8g0b84cv8yfrpg/SFMlnyl.png?rlkey=gvctrkqj1qxip0bnsqgfi6rxy&dl=1");
        }

        private bool HasImageLibrary()
        {
            if (ImageLibrary)
            {
                LoadImages();
                return true;
            }

            if (ILCheck == 3)
            {
                PrintError("ImageLibrary not found! Download link -> https://umod.org/plugins/image-library");
                Interface.Oxide.UnloadPlugin(Name);
                return false;
            }

            timer.In(1, () =>
            {
                ILCheck++;
                OnServerInitialized();
            });
            return false;
        }

        #endregion

        #region Classes

        private class VoiceTranslator : FacepunchBehaviour
        {
            private BasePlayer _target;
            private BasePlayer _translator;
            private ulong _teamId, _steamId;

            public VoiceTranslator Init(BasePlayer target)
            {
                _target = target;
                _teamId = target.Team.teamID;

                _translator = (BasePlayer)GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", Vector3.down * 5 + Vector3.back * 5);
                _translator.enableSaving = false;
                _translator.limitNetworking = true;
                _translator.Spawn();
                _translator.RemovePlayerRigidbody();
                _translator.DisablePlayerCollider();
                _translator.SetParent(target);
                target.children.Remove(_translator);

                return this;
            }

            public void Translate(byte[] data, ulong victim)
            {
                _steamId = victim;
                StartTranslate();

                var netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.VoiceData);
                netWrite.EntityID(_translator.net.ID);
                netWrite.BytesWithSize(data);
                netWrite.Send(new SendInfo(_target.Connection) { priority = Priority.Immediate });
            }

            private void StartTranslate()
            {
                _ins.UpdateSpeakerInfo(_teamId, _steamId, true);
                CancelInvoke(StopTranslate);
                _translator.SendAsSnapshot(_target.Connection);
                Invoke(StopTranslate, 1f);
            }

            private void StopTranslate()
            {
                _ins.UpdateSpeakerInfo(_teamId, _steamId, false);
                _translator.OnNetworkSubscribersLeave(new List<Connection> { _target.Connection });
            }

            public void Kill()
            {
                _translator.Hurt(10000);
                _translator.Kill();
                Destroy(gameObject);
            }
        }

        #endregion

        #endregion

        #region OxideHooks

        #region BaseHooks

        private void Init() => LoadData();

        private void OnServerInitialized()
        {
            if (!HasImageLibrary())
                return;

            _ins = this;

            UpdateTeamList();
            ApplyConfigVariables();
            EntityCheckForAA();
            
            if (SaveRestore.WipeId != _data.WipeID)
            {
                _data.WipeID = SaveRestore.WipeId;
                _data.TeamFix.Clear();
                _data.Teams.Clear();
                SaveData();
            }
            
            LoadTeamFix();

            foreach (var check in _config.Skins)
            {
                if (check.Value.Contains(0) || check.Value.Count < 2)
                    continue;
                
                check.Value.Add(0);
                check.Value[check.Value.Count - 1] = check.Value[0];
                check.Value[0] = 0;
            }
            
            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        private void Unload()
        {
            foreach (var check in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(check, Layer + ".TeamHud");
                CuiHelper.DestroyUi(check, Layer + ".bg");
            }

            foreach (var check in VoiceTranslators)
                check.Value.Kill();

            SaveData();
            _ins = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;

            var data = GetPlayerData(player);
            if (data == null)
                _data.Players.Add(new PlayerSettings(player.userID, player.displayName, true, true));
            else
                data.UpdateName(player.displayName);

            if (player.Team == null)
                return;

            var teamid = player.Team.teamID;

            if (_config.Options.AutoTeamLeaderSwtich)
            {
                var teamData = GetTeamData(teamid);
                if (teamData != null && teamData.TeamLeader == player.userID && player.Team.teamLeader != player.userID)
                    player.Team.SetTeamLeader(player.userID);
            }

            if (!OnlineTeams.TryAdd(teamid, new List<BasePlayer> { player }))
                GetOnlineTeam(teamid).Add(player);

            if (_config.Options.EnableTeamHud)
                timer.In(1, () =>
                {
                    if (!OnlineTeams.TryGetValue(teamid, out var team))
                        return;

                    foreach (var check in team)
                        ShowHudParentUI(check);
                });
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null)
                return;

            if (VoiceTranslators.TryGetValue(player.userID, out var translator))
            {
                translator.Kill();
                VoiceTranslators.Remove(player.userID);
            }

            if (player.Team == null)
                return;

            var teamid = player.Team.teamID;
            
            if (OnlineTeams.TryGetValue(teamid, out var team))
            {
                if (team.Count > 1)
                {
                    team.Remove(player);
                    if (_config.Options.AutoTeamLeaderSwtich)
                    {
                        var teamData = GetTeamData(teamid);
                        if (teamData != null && teamData.TeamLeader == player.userID)
                            player.Team.SetTeamLeader(team.FirstOrDefault().userID);
                    }
                }
                else
                    OnlineTeams.Remove(player.Team.teamID);
            }

            if (_config.Options.EnableTeamHud)
                timer.In(1, () =>
                {
                    if (!OnlineTeams.ContainsKey(teamid))
                        return;

                    foreach (var check in team)
                        ShowHudParentUI(check);
                });
        }

        #endregion

        #region AuthorizationHooks

        private object OnSamSiteTarget(SamSite samSite, PlayerHelicopter target)
        {
            if (samSite == null || samSite.OwnerID == 0)
                return null;

            if (!SamSiteAuthorization.TryGetValue(samSite, out var authList))
                return null;

            foreach (var check in target.allMountPoints)
            {
                var player = check?.mountable?.GetMounted();
                if (player == null || !authList.Contains(player.userID))
                    continue;
                
                return false;
            }

            return null;
        }

        private void OnEntitySpawned(BuildingPrivlidge tc)
        {
            if (tc == null || tc.OwnerID == 0)
                return;

            BuildingPrivlidges.Add(tc);

            if (!_config.Options.AutoAuthorization.EnableTC)
                return;

            var team = FindTeamByUserID(tc.OwnerID);
            if (team == null)
                return;

            var teamData = GetTeamData(team.teamID);
            foreach (var memberID in team.members)
            {
                if (memberID == tc.OwnerID || !teamData.AuthorizationSettings.ContainsKey(memberID) || !teamData.AuthorizationSettings[memberID].EnableTC)
                    continue;

                tc.authorizedPlayers.RemoveWhere(x => x.userid == memberID);
                tc.authorizedPlayers.Add(new ProtoBuf.PlayerNameID { username = "Player", userid = memberID });
            }

            tc.UpdateMaxAuthCapacity();
            tc.SendNetworkUpdate();
        }

        private void OnCodeChanged(BasePlayer player, CodeLock codeLock)
        {
            if (codeLock == null || codeLock.OwnerID == 0)
                return;

            CodeLocks.Add(codeLock);

            if (!_config.Options.AutoAuthorization.EnableCodelocks)
                return;

            var team = FindTeamByUserID(codeLock.OwnerID);
            if (team == null)
                return;

            var teamData = GetTeamData(team.teamID);
            foreach (var memberID in team.members)
            {
                if (memberID == codeLock.OwnerID || !teamData.AuthorizationSettings.ContainsKey(memberID) || !teamData.AuthorizationSettings[memberID].EnableCodelocks)
                    continue;

                codeLock.whitelistPlayers.RemoveAll(x => x == memberID);
                codeLock.whitelistPlayers.Add(memberID);
            }

            codeLock.SendNetworkUpdate();
        }

        private void OnEntitySpawned(CodeLock codeLock)
        {
            if (codeLock == null || codeLock.OwnerID == 0)
                return;

            CodeLocks.Add(codeLock);

            if (!_config.Options.AutoAuthorization.EnableCodelocks)
                return;

            var team = FindTeamByUserID(codeLock.OwnerID);
            if (team == null)
                return;

            var teamData = GetTeamData(team.teamID);
            foreach (var memberID in team.members)
            {
                if (memberID == codeLock.OwnerID || !teamData.AuthorizationSettings.ContainsKey(memberID) || !teamData.AuthorizationSettings[memberID].EnableCodelocks)
                    continue;

                codeLock.whitelistPlayers.RemoveAll(x => x == memberID);
                codeLock.whitelistPlayers.Add(memberID);
            }

            codeLock.SendNetworkUpdate();
        }

        private void OnEntitySpawned(AutoTurret autoTurret)
        {
            if (autoTurret == null || autoTurret.OwnerID == 0)
                return;

            AutoTurrets.Add(autoTurret);

            if (!_config.Options.AutoAuthorization.EnableAutoTurrets)
                return;

            var team = FindTeamByUserID(autoTurret.OwnerID);
            if (team == null)
                return;

            var isOnline = autoTurret.IsOnline();
            if (isOnline)
                autoTurret.SetIsOnline(false);

            var teamData = GetTeamData(team.teamID);
            foreach (var memberID in team.members)
            {
                if (memberID == autoTurret.OwnerID || !teamData.AuthorizationSettings.ContainsKey(memberID) || !teamData.AuthorizationSettings[memberID].EnableAutoTurrets)
                    continue;

                autoTurret.authorizedPlayers.RemoveWhere(x => x.userid == memberID);
                autoTurret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID { username = "Player", userid = memberID });
            }

            if (isOnline)
                autoTurret.SetIsOnline(true);

            autoTurret.UpdateMaxAuthCapacity();
            autoTurret.SendNetworkUpdate();
        }

        private void OnEntitySpawned(SamSite samSite)
        {
            if (samSite == null || samSite.OwnerID == 0 || !SamSiteAuthorization.TryAdd(samSite, new List<ulong>()))
                return;

            if (!_config.Options.AutoAuthorization.EnableSS)
                return;

            var team = FindTeamByUserID(samSite.OwnerID);
            if (team == null)
                return;

            var authList = SamSiteAuthorization[samSite];
            var teamData = GetTeamData(team.teamID);

            authList.Clear();

            foreach (var memberID in team.members)
            {
                if (!teamData.AuthorizationSettings.ContainsKey(memberID) || !teamData.AuthorizationSettings[memberID].EnableSS)
                    continue;

                authList.Add(memberID);
            }
        }

        private void OnEntityDeath(SamSite samSite)
        {
            if (samSite == null)
                return;

            SamSiteAuthorization.Remove(samSite);
        }

        private void OnEntityDeath(BuildingPrivlidge buildingPrivlidge)
        {
            if (buildingPrivlidge == null || !BuildingPrivlidges.Contains(buildingPrivlidge))
                return;

            BuildingPrivlidges.Remove(buildingPrivlidge);
        }

        private void OnEntityDeath(CodeLock codeLock)
        {
            if (codeLock == null || !CodeLocks.Contains(codeLock))
                return;

            CodeLocks.Remove(codeLock);
        }

        private void OnEntityDeath(AutoTurret autoTurret)
        {
            if (autoTurret == null || !AutoTurrets.Contains(autoTurret))
                return;

            AutoTurrets.Remove(autoTurret);
        }

        #endregion

        #region HudHooks

        private void OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
            if (player == null || !player.IsConnected || player.Team == null || player.IsWounded() || player.IsDead())
                return;

            UpdateHealthUI(GetOnlineTeam(player.Team.teamID), player);
        }

        private void OnActiveItemChange(BasePlayer player, Item oldItem, ItemId newItemId)
        {
            NextTick(() =>
            {
                if (player == null || !player.IsConnected || player.Team == null)
                    return;

                UpdateActiveItemUI(GetOnlineTeam(player.Team.teamID), player);
            });
        }

        private void OnPlayerWound(BasePlayer player, HitInfo info)
        {
            NextTick(() =>
            {
                if (player == null || !player.IsConnected || player.Team == null)
                    return;

                UpdatePlayerState(GetOnlineTeam(player.Team.teamID), player);
            });
        }

        private void OnPlayerRecover(BasePlayer player)
        {
            NextTick(() =>
            {
                if (player == null || player.Team == null)
                    return;

                UpdatePlayerState(GetOnlineTeam(player.Team.teamID), player);
            });
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            NextTick(() =>
            {
                if (player == null || !player.IsConnected || player.Team == null)
                    return;

                UpdatePlayerState(GetOnlineTeam(player.Team.teamID), player);
            });
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            NextTick(() =>
            {
                if (player == null || player.Team == null)
                    return;

                UpdatePlayerState(GetOnlineTeam(player.Team.teamID), player);
            });
        }

        #endregion

        #region TeamVoice

        private object OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            if (player == null || player.Team == null || !GetPlayerData(player).EnableTeamVoice || (_config.Permissions.TeamVoiceNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.TeamVoicePermission)))
                return null;

            foreach (var check in GetOnlineTeam(player.Team.teamID))
            {
                if (check.userID == player.userID)
                    continue;

                GetTranslator(check).Translate(data, player.userID);
            }

            return false;
        }

        #endregion

        #region TeamHooks

        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            NextTick(() =>
            {
                if (player == null || player.Team == null || team == null)
                    return;

                if (!_data.TeamFix.TryAdd(team.teamID, new TeamFixData { TeamLeader = player.userID, Members = new List<ulong> { player.userID } }))
                {
                    var teamFixData = _data.TeamFix[team.teamID];
                    teamFixData.Members.Clear();
                    teamFixData.Members.Add(player.userID);
                    teamFixData.TeamLeader = player.userID;
                    SaveData();
                }

                OnlineTeams.Add(player.Team.teamID, new List<BasePlayer> { player });
                var teamData = GetTeamData(team.teamID);
                teamData.TeamLeader = player.userID;
                teamData.AuthorizationSettings.TryAdd(player.userID, new AuthorizationSettings());

                foreach (var check in _config.Skins)
                    teamData.Skins.TryAdd(check.Key, 0);

                if (!_config.Options.EnableTeamHud)
                    return;

                ShowHudParentUI(player);
            });
        }

        private void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader)
        {
            NextTick(() =>
            {
                if (newLeader == null || team == null)
                    return;

                if (_data.TeamFix.TryGetValue(team.teamID, out var teamFixData))
                {
                    teamFixData.TeamLeader = newLeader.userID;
                    SaveData();
                }

                var teamData = GetTeamData(team.teamID);
                if (teamData != null)
                    teamData.TeamLeader = newLeader.userID;

                if (!_config.Options.EnableTeamHud)
                    return;
                
                foreach (var check in GetOnlineTeam(team.teamID))
                    ShowHudParentUI(check);
            });
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            NextTick(() =>
            {
                if (player == null || team == null)
                    return;
                
                if (_data.TeamFix.TryGetValue(team.teamID, out var teamFixData))
                {
                    teamFixData.Members.Add(player.userID);
                    SaveData();
                }

                var onlineTeam = GetOnlineTeam(team.teamID);
                if (!onlineTeam.Contains(player))
                    onlineTeam.Add(player);

                var teamData = GetTeamData(team.teamID);
                teamData.AuthorizationSettings.TryAdd(player.userID, new AuthorizationSettings());
                AuthorizeTo(team, player.userID, "all", true);

                foreach (var check in team.members)
                {
                    var aaSettings = teamData.AuthorizationSettings[check];

                    if (_config.Options.AutoAuthorization.EnableCodelocks && aaSettings.EnableCodelocks)
                        AuthorizeTo(player.userID, check, "cl", true);

                    if (_config.Options.AutoAuthorization.EnableTC && aaSettings.EnableTC)
                        AuthorizeTo(player.userID, check, "tc", true);

                    if (_config.Options.AutoAuthorization.EnableAutoTurrets && aaSettings.EnableAutoTurrets)
                        AuthorizeTo(player.userID, check, "at", true);

                    if (_config.Options.AutoAuthorization.EnableSS && aaSettings.EnableSS)
                        AuthorizeTo(player.userID, check, "ss", true);
                }

                if (!_config.Options.EnableTeamHud)
                    return;

                foreach (var check in onlineTeam)
                    ShowHudParentUI(check);
            });
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null || player == null)
                return;
            
            if (_data.TeamFix.TryGetValue(team.teamID, out var teamFixData))
            {
                teamFixData.Members.Remove(player.userID);
                SaveData();
            }
            var onlineTeam = GetOnlineTeam(team.teamID);
            if (onlineTeam.Contains(player))
                onlineTeam.Remove(player);

            CuiHelper.DestroyUi(player, Layer + ".TeamHud");
            GetTeamData(team.teamID).AuthorizationSettings.Remove(player.userID);

            if (VoiceTranslators.TryGetValue(player.userID, out var translator))
            {
                translator.Kill();
                VoiceTranslators.Remove(player.userID);
            }

            if (team.members.Count > 1)
            {
                foreach (var check in team.members)
                {
                    if (check == player.userID)
                        continue;

                    AuthorizeTo(player.userID, check, "all", false);
                }

                AuthorizeTo(team, player.userID, "all", false);
            }

            NextTick(() =>
            {
                if (!_config.Options.EnableTeamHud || !OnlineTeams.ContainsKey(team.teamID))
                    return;

                foreach (var check in GetOnlineTeam(team.teamID))
                    ShowHudParentUI(check);
            });
        }

        private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer kicker, ulong target)
        {
            if (team == null)
                return;
            
            if (_data.TeamFix.TryGetValue(team.teamID, out var teamFixData))
            {
                teamFixData.Members.Remove(target);
                SaveData();
            }

            var player = GetOnlineTeam(team.teamID).FirstOrDefault(x => x.userID == target);
            if (player != null)
            {
                CuiHelper.DestroyUi(player, Layer + ".TeamHud");
                GetOnlineTeam(team.teamID).Remove(player);

                if (VoiceTranslators.TryGetValue(player.userID, out var translator))
                {
                    translator.Kill();
                    VoiceTranslators.Remove(player.userID);
                }
            }

            GetTeamData(team.teamID).AuthorizationSettings.Remove(target);

            if (team.members.Count > 1)
            {
                foreach (var check in team.members)
                {
                    if (check == target)
                        continue;

                    AuthorizeTo(target, check, "all", false);
                }

                AuthorizeTo(team, target, "all", false);
            }

            NextTick(() =>
            {
                if (!_config.Options.EnableTeamHud)
                    return;

                foreach (var check in GetOnlineTeam(team.teamID))
                    ShowHudParentUI(check);
            });
        }

        private void OnTeamDisband(RelationshipManager.PlayerTeam team)
        {
            if (team == null)
                return;

            _data.TeamFix.Remove(team.teamID);
            SaveData();

            foreach (var check in team.members)
                AuthorizeTo(team, check, "all", false);

            var onlineTeam = GetOnlineTeam(team.teamID);
            if (onlineTeam != null)
                foreach (var check in onlineTeam)
                    CuiHelper.DestroyUi(check, Layer + ".TeamHud");

            OnlineTeams.Remove(team.teamID);
            _data.Teams.Remove(GetTeamData(team.teamID));
        }

        #endregion

        #region SkinsHooks

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item == null || container == null || _data == null)
                return;

            var player = container.GetOwnerPlayer();
            if (player == null || player.Team == null || !player.userID.IsSteamId() || !GetPlayerData(player).EnableTeamSkins || (_config.Permissions.TeamSkinsNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.TeamSkinsPermission)))
                return;

            var team = GetTeamData(player.Team.teamID);
            if (!team.Skins.TryGetValue(item.info.itemid, out var skin) || skin == 0 || skin == item.skin)
                return;

            item.skin = skin;

            var held = item.GetHeldEntity();
            if (held == null)
                return;

            held.skinID = skin;
            held.SendNetworkUpdate();
        }

        #endregion

        #endregion
        
        #region Spectating

        private void StartSpectating(BasePlayer player)
        {
            if (player == null || !player.IsConnected || Spectators.Contains(player) || player.IsSpectating())
                return;

            if (player.IsWounded())
                player.StopWounded();

            foreach (var check in player.GetSubscribers())
                if (check != player.net.connection)
                    player.DestroyOnClient(check);
            
            player.Teleport(Vector3.zero);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.gameObject.SetLayerRecursive(10);

            Spectators.Add(player);
            ChangeSpectatingTarget(player, true);
        }

        public void ChangeSpectatingTarget(BasePlayer player, bool next)
        {
            if (player == null || !player.IsConnected || !Spectators.Contains(player) || !player.IsSpectating())
                return;

            var target = GetSpectatingTarget(player, next);
            if (target == null)
            {
                StopSpectating(player);
                return;
            }

            player.SendEntitySnapshot(target);
            player.gameObject.Identity();
            player.SetParent(target);
            
            //ShowUISpectator(player, target.displayName);
        }

        private void StopSpectating(BasePlayer player)
        {
            if (player == null || !Spectators.Contains(player) || !player.IsSpectating())
                return;

            player.SetParent(null);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.gameObject.SetLayerRecursive(17);
            player.SendNetworkUpdateImmediate();

            Spectators.Remove(player);
            CuiHelper.DestroyUi(player, Layer + ".Spec");
        }

        private BasePlayer GetSpectatingTarget(BasePlayer player, bool next)
        {
            var currentTarget = player.GetParentEntity() != null ? player.GetParentEntity() as BasePlayer : null;
            var availableList = GetOnlineTeam(player.userID).Where(x => x.IsConnected && x.IsAlive() && !x.IsSpectating()).ToList();

            if (availableList.Count == 0 || (availableList.Count == 1 && currentTarget == availableList[0]))
                return null;
            
            if (availableList.Count == 1 || currentTarget == null)
                return availableList[0];

            var currentTargetIndex = availableList.IndexOf(currentTarget);
            if (next)
                return currentTargetIndex + 1 <= availableList.Count - 1 ? availableList[currentTargetIndex + 1] : availableList[0];

            return currentTargetIndex - 1 >= 0 ? availableList[currentTargetIndex - 1] : availableList[availableList.Count - 1];
        }

        #endregion

        #region Functions

        private void AuthorizeTo(RelationshipManager.PlayerTeam team, ulong target, string type, bool auth)
        {
            foreach (var check in team.members)
            {
                if (check == target)
                    continue;

                AuthorizeTo(check, target, type, auth);
            }
        }

        private void AuthorizeTo(ulong owner, ulong target, string type, bool auth)
        {
            if (owner == target)
                return;

            switch (type)
            {
                case "cl":
                    if (!_config.Options.AutoAuthorization.EnableCodelocks)
                        return;
                    
                    foreach (var check in CodeLocks)
                    {
                        if (check == null || check.OwnerID != owner)
                            continue;

                        if (auth)
                        {
                            if (check.whitelistPlayers.Contains(target))
                                continue;

                            check.whitelistPlayers.Add(target);
                        }
                        else
                        {
                            if (!check.whitelistPlayers.Contains(target))
                                continue;

                            check.whitelistPlayers.Remove(target);
                        }

                        check.SendNetworkUpdate();
                    }

                    break;
                case "at":
                    if (!_config.Options.AutoAuthorization.EnableAutoTurrets)
                        return;
                    
                    foreach (var check in AutoTurrets)
                    {
                        if (check == null || check.OwnerID != owner)
                            continue;

                        var isOnline = check.IsOnline();
                        if (isOnline)
                            check.SetIsOnline(false);

                        check.authorizedPlayers.RemoveWhere(x => x.userid == target);

                        if (auth)
                            check.authorizedPlayers.Add(new ProtoBuf.PlayerNameID { userid = target, username = "Player" });

                        if (isOnline)
                            check.SetIsOnline(true);

                        check.UpdateMaxAuthCapacity();
                        check.SendNetworkUpdate();
                    }

                    break;
                case "tc":
                    if (!_config.Options.AutoAuthorization.EnableTC)
                        return;
                    
                    foreach (var check in BuildingPrivlidges)
                    {
                        if (check == null || check.OwnerID != owner)
                            continue;

                        check.authorizedPlayers.RemoveWhere(x => x.userid == target);

                        if (auth)
                            check.authorizedPlayers.Add(new ProtoBuf.PlayerNameID { userid = target, username = "Player" });

                        check.UpdateMaxAuthCapacity();
                        check.SendNetworkUpdate();
                    }

                    break;
                case "ss":
                    if (!_config.Options.AutoAuthorization.EnableSS)
                        return;
                    
                    foreach (var check in SamSiteAuthorization)
                    {
                        if (check.Key == null || check.Key.OwnerID != owner)
                            continue;

                        if (auth)
                        {
                            if (check.Value.Contains(target))
                                continue;

                            check.Value.Add(target);
                        }
                        else
                        {
                            if (!check.Value.Contains(target))
                                continue;

                            check.Value.Remove(target);
                        }
                    }

                    break;
                case "all":
                    AuthorizeTo(owner, target, "cl", auth);
                    AuthorizeTo(owner, target, "at", auth);
                    AuthorizeTo(owner, target, "tc", auth);
                    AuthorizeTo(owner, target, "ss", auth);
                    break;
            }
        }

        private void ApplyConfigVariables()
        {
            Scale = _config.HudSettings.Scale;
            cmd.AddChatCommand(_config.TeamSettingsCommand, this, ChatCommandTeamUI);

            permission.RegisterPermission(_config.Permissions.HudUsePermission, this);
            permission.RegisterPermission(_config.Permissions.TeamMarkerPermission, this);
            permission.RegisterPermission(_config.Permissions.TeamVoicePermission, this);
            permission.RegisterPermission(_config.Permissions.TeamSkinsPermission, this);

            if (_config.Options.EnableTeamMarker)
            {
                cmd.AddConsoleCommand("ftmark", this, nameof(ConsoleCommandMarker));
                timer.Every(1f, () =>
                {
                    foreach (var check in TipRemove.ToArray())
                    {
                        if (!(DateTime.Now.Subtract(check.Value).TotalSeconds > 2))
                            continue;

                        check.Key.SendConsoleCommand("gametip.hidegametip");
                        TipRemove.Remove(check.Key);
                    }
                });
            }

            if (!_config.Options.AutoAuthorization.EnableSS)
                Unsubscribe(nameof(OnSamSiteTarget));

            if (!_config.Options.EnableTeamVoice)
                Unsubscribe("OnPlayerVoice");

            if (!_config.Options.EnableTeamSkins)
                Unsubscribe("OnItemAddedToContainer");

            if (!_config.Options.EnableTeamHud)
            {
                Unsubscribe("OnPlayerHealthChange");
                Unsubscribe("OnActiveItemChange");
                Unsubscribe("OnPlayerWound");
                Unsubscribe("OnPlayerRecover");
                Unsubscribe("OnPlayerDeath");
                Unsubscribe("OnPlayerRespawned");
            }
            else
                CreateSquareUpdateTimer();
        }

        private void PlayerPing(BasePlayer player, Vector3 targetPosition, int type, int color)
        {
            var note = Facepunch.Pool.Get<ProtoBuf.MapNote>();
            note.worldPosition = targetPosition;
            note.isPing = true;
            note.timeRemaining = note.totalDuration = _config.Options.TeamMarkers.Duration;
            note.colourIndex = color;
            note.icon = type;
            player.State.pings ??= new List<MapNote>();
            player.State.pings.Add(note);
            player.DirtyPlayerState();
            player.SendPingsToClient();
            player.TeamUpdate(true);

            if (!PlayerMarkerCooldown.TryAdd(player.userID, DateTime.Now))
                PlayerMarkerCooldown[player.userID] = DateTime.Now;
        }

        private void LoadTeamFix()
        {
            if (!_config.Options.EnableTeamFix)
                return;
            
            if (_data.TeamFix.Count != 0)
            {
                foreach (var check in _data.TeamFix.ToArray())
                {
                    var team = RelationshipManager.ServerInstance.FindTeam(check.Key);
                    if (team == null)
                    {
                        team = Facepunch.Pool.Get<RelationshipManager.PlayerTeam>();
                        team.teamID = check.Key;
                        team.teamStartTime = Time.realtimeSinceStartup;
                        RelationshipManager.ServerInstance.teams.Add(team.teamID, team);
                    }

                    foreach (var member in check.Value.Members.ToArray())
                    {
                        if (team.members.Contains(member))
                            continue;

                        RelationshipManager.ServerInstance.FindPlayersTeam(member)?.RemovePlayer(member);
                        var memberPlayer = RelationshipManager.FindByID(member);
                        if (memberPlayer != null)
                        {
                            team.AddPlayer(memberPlayer);
                            continue;
                        }
                        
                        team.members.Add(member);
                        RelationshipManager.ServerInstance.playerToTeam.Add(member, team);
                        team.MarkDirty();
                    }
                    
                    if (check.Value.TeamLeader !=  team.teamLeader)
                        team.SetTeamLeader(check.Value.TeamLeader);
                    
                }
                
                return;
            }

            foreach (var check in RelationshipManager.ServerInstance.teams.ToArray())
            {
                if (check.Value?.members == null || check.Value.members.Count == 0)
                    continue;

                var teamData = new TeamFixData();
                teamData.TeamLeader = check.Value.teamLeader;
                teamData.Members.AddRange(check.Value.members);
                _data.TeamFix.Add(check.Key, teamData);
            }
            SaveData();
        }
        
        private void UpdateTeamList()
        {
            var removeList = new List<TeamSettings>();
            foreach (var team in RelationshipManager.ServerInstance.teams)
            {
                var remove = team.Value == null || team.Value.members.Count == 0;
                foreach (var dataTeam in _data.Teams)
                {
                    if (dataTeam.ID != team.Key || !remove)
                        continue;

                    removeList.Add(dataTeam);
                    break;
                }
            }

            foreach (var check in removeList)
                _data.Teams.Remove(check);

            foreach (var team in _data.Teams)
            foreach (var check in _config.Skins)
                team.Skins.TryAdd(check.Key, 0);
        }

        private VoiceTranslator GetTranslator(BasePlayer player)
        {
            if (VoiceTranslators.TryGetValue(player.userID, out var translator))
                return translator;

            translator = new GameObject().AddComponent<VoiceTranslator>().Init(player);
            VoiceTranslators.Add(player.userID, translator);

            return translator;
        }

        private void CreateSquareUpdateTimer()
        {
            if (!_config.Options.EnableGridPosition)
                return;
            
            timer.Every(_config.HudSettings.SquareUpdateRate, () =>
            {
                foreach (var check in OnlineTeams)
                {
                    var container = new CuiElementContainer();
                    foreach (var player in check.Value)
                        FillGridInfo(container, null, player);

                    foreach (var player in check.Value)
                    {
                        if (_config.Permissions.HudNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.HudUsePermission))
                            continue;

                        var data = GetPlayerData(player.userID);
                        if (data is not { EnableTeamGridPoisition: true })
                            continue;
                        
                        CuiHelper.AddUi(player, container);
                    }
                }
            });
        }

        private RelationshipManager.PlayerTeam FindTeamByUserID(ulong id)
        {
            foreach (var check in RelationshipManager.ServerInstance.teams)
                if (check.Value.members.Contains(id))
                    return check.Value;

            return null;
        }

        private BasePlayer FindByID(ulong id)
        {
            foreach (var check in BasePlayer.allPlayerList)
                if (check.userID == id)
                    return check;

            return null;
        }

        private PlayerSettings GetPlayerData(BasePlayer player) => GetPlayerData(player.userID);

        private List<BasePlayer> GetOnlineTeam(ulong id)
        {
            if (OnlineTeams.TryGetValue(id, out var list))
                return list;

            var team = RelationshipManager.ServerInstance.FindTeam(id);
            if (team == null || team.members.Count == 0)
            {
                PrintWarning("If you see this message, contact with me via discord: ahigao");
                return null;
            }

            list = new List<BasePlayer>();
            foreach (var check in team.members)
            {
                if (BasePlayer.TryFindByID(check, out var player))
                    list.Add(player);
            }

            OnlineTeams.Add(id, list);
            return list;
        }

        private TeamSettings GetTeamData(ulong id)
        {
            foreach (var check in _data.Teams)
                if (check.ID == id)
                    return check;
            
            var data = new TeamSettings(id, new Dictionary<ulong, AuthorizationSettings>());
            var team = RelationshipManager.ServerInstance.FindTeam(id);
            if (team != null)
                foreach (var check in team.members)
                    data.AuthorizationSettings.Add(check, new AuthorizationSettings());
            
            foreach (var check in _config.Skins)
                data.Skins.TryAdd(check.Key, 0);
            
            _data.Teams.Add(data);
            return data;
        }

        private PlayerSettings GetPlayerData(ulong id)
        {
            foreach (var check in _data.Players)
                if (check.ID == id)
                    return check;

            var player = FindByID(id);
            var settings = new PlayerSettings(id, player == null ? "Player" : player.displayName, true, true);
            _data.Players.Add(settings);
            return settings;
        }

        private void EntityCheckForAA()
        {
            foreach (var check in BaseNetworkable.serverEntities)
            {
                if (check is BuildingPrivlidge)
                {
                    OnEntitySpawned(check as BuildingPrivlidge);
                    continue;
                }

                if (check is CodeLock)
                {
                    OnEntitySpawned(check as CodeLock);
                    continue;
                }

                if (check is AutoTurret)
                {
                    OnEntitySpawned(check as AutoTurret);
                    continue;
                }

                if (!(check is SamSite))
                    continue;

                OnEntitySpawned(check as SamSite);
            }
        }

        private string PositionToGridCoord(Vector3 position)
        {
            var a = new Vector2(TerrainMeta.NormalizeX(position.x), TerrainMeta.NormalizeZ(position.z));
            var num = TerrainMeta.Size.x / 1024f;
            var num2 = 7;
            var vector = a * num * num2;
            var num3 = Mathf.Floor(vector.x) + 1f;
            var num4 = Mathf.Floor(num * num2 - vector.y);
            var text = string.Empty;
            var num5 = num3 / 26f;
            var num6 = num3 % 26f;

            if (num6 == 0f)
                num6 = 26f;

            if (num5 > 1f)
                text += Convert.ToChar(64 + (int)num5).ToString();

            text += Convert.ToChar(64 + (int)num6).ToString();

            return text + num4;
        }

        private void SendGameTip(BasePlayer player, string msg)
        {
            player.SendConsoleCommand("gametip.showgametip", msg);
            if (!TipRemove.TryAdd(player, DateTime.Now))
                TipRemove[player] = DateTime.Now;
        }

        #endregion

        #region Commands

        private void ChatCommandTeamUI(BasePlayer player, string command, string[] args)
        {
            if (player.Team == null)
            {
                SendMessage(player, "CM_NO_TEAM");
                return;
            }
            
            ShowUIBG(player);
        }

        private void ConsoleCommandMarker(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player?.Team == null || !GetPlayerData(player).EnableCustomMarker)
                return;

            if (_config.Permissions.TeamMarkerNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.TeamMarkerPermission))
            {
                SendMessage(player, "CM_DONT_HAVE_PERM");
                return;
            }

            if (PlayerMarkerCooldown.ContainsKey(player.userID) && DateTime.Now.Subtract(PlayerMarkerCooldown[player.userID]).TotalSeconds < _config.Options.TeamMarkers.Cooldown)
            {
                SendGameTip(player, GetMsg(player.UserIDString, "CM_MARKER_COOLDOWN", _config.Options.TeamMarkers.Cooldown - (int)DateTime.Now.Subtract(PlayerMarkerCooldown[player.userID]).TotalSeconds));
                return;
            }

            Physics.Raycast(player.eyes.HeadRay(), out var info, _config.Options.TeamMarkers.MaxDistance);

            if (info.point == Vector3.zero)
                return;


            var foundEntities = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(info.point, _config.Options.TeamMarkers.SearchRadius, foundEntities);

            foreach (var check in foundEntities)
            {
                if (!(check is BasePlayer))
                    continue;

                var targetPlayer = check as BasePlayer;
                if (targetPlayer.Team != null && targetPlayer.Team.teamID == player.Team.teamID)
                    continue;

                var position = check.transform.position;
                PlayerPing(player, position.WithY(position.y + check.bounds.extents.y * 2), 6, 3);

                Facepunch.Pool.FreeUnmanaged(ref foundEntities);
                return;
            }

            foreach (var check in foundEntities)
            {
                if (!(check is OreResourceEntity))
                    continue;

                var position = check.transform.position;
                PlayerPing(player, position.WithY(position.y + check.bounds.extents.y * 2), 10, 4);

                Facepunch.Pool.FreeUnmanaged(ref foundEntities);
                return;
            }

            foreach (var check in foundEntities)
            {
                if (!(check is StorageContainer))
                    continue;

                var position = check.transform.position;
                PlayerPing(player, position.WithY(position.y + check.bounds.extents.y * 2), 11, 0);

                Facepunch.Pool.FreeUnmanaged(ref foundEntities);
                return;
            }

            foreach (var check in foundEntities)
            {
                if (!(check is DroppedItem))
                    continue;

                var item = check as DroppedItem;
                if (item.item?.GetHeldEntity()?.GetComponent<BaseProjectile>() == null)
                    continue;

                var position = check.transform.position;
                PlayerPing(player, position.WithY(position.y + check.bounds.extents.y * 2), 9, 5);

                Facepunch.Pool.FreeUnmanaged(ref foundEntities);
                return;
            }

            PlayerPing(player, info.point, 0, 2);

            Facepunch.Pool.FreeUnmanaged(ref foundEntities);
        }

        [ConsoleCommand("UI_BT")]
        private void ConsoleCommandUI_BT(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs())
                return;

            PlayerSettings data;
            AuthorizationSettings authorizationSettings;

            switch (arg.GetString(0))
            {
                case "category":
                    switch (arg.GetString(1))
                    {
                        case "Skins":
                            ShowUISkins(player, arg.GetInt(2));
                            return;
                        case "Info":
                            ShowUIInfo(player);
                            return;
                        case "Settings":
                            ShowUISettings(player);
                            return;
                        case "Mates":
                            ShowUIMembers(player);
                            return;
                    }

                    return;
                case "openskins":
                    ShowUIChangeSkin(player, arg.GetInt(1), arg.GetInt(2), arg.GetInt(3));
                    return;
                case "chooseskin":
                    var team = GetTeamData(player.Team.teamID);
                    team.Skins[arg.GetInt(1)] = arg.GetULong(2);
                    ShowUISkins(player, arg.GetInt(3));
                    return;
                case "chosemate":
                    ShowUIMembers(player, arg.GetULong(1));
                    return;
                case "svoice":
                    if (_config.Permissions.TeamVoiceNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.TeamVoicePermission))
                    {
                        SendMessage(player, "CM_DONT_HAVE_PERM");
                        return;
                    }
                    data = GetPlayerData(player);
                    data.EnableTeamVoice = !data.EnableTeamVoice;
                    ShowUISettings(player);
                    return;
                case "sskins":
                    if (_config.Permissions.TeamSkinsNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.TeamSkinsPermission))
                    {
                        SendMessage(player, "CM_DONT_HAVE_PERM");
                        return;
                    }
                    data = GetPlayerData(player);
                    data.EnableTeamSkins = !data.EnableTeamSkins;
                    ShowUISettings(player);
                    return;
                case "grid":
                    data = GetPlayerData(player);
                    data.EnableTeamGridPoisition = !data.EnableTeamGridPoisition;

                    if (!data.EnableTeamGridPoisition)
                        foreach (var check in player.Team.members)
                            CuiHelper.DestroyUi(player, Layer + ".TeamHud" + check + ".Grid");
                    
                    ShowUISettings(player);
                    return;
                case "smarker":
                    if (_config.Permissions.TeamMarkerNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.TeamMarkerPermission))
                    {
                        SendMessage(player, "CM_DONT_HAVE_PERM");
                        return;
                    }
                    data = GetPlayerData(player);
                    data.EnableCustomMarker = !data.EnableCustomMarker;
                    ShowUISettings(player);
                    return;
                case "mtcaa":
                    authorizationSettings = GetTeamData(player.Team.teamID).AuthorizationSettings[arg.GetULong(1)];
                    authorizationSettings.EnableTC = !authorizationSettings.EnableTC;
                    AuthorizeTo(player.Team, arg.GetULong(1), "tc", authorizationSettings.EnableTC);
                    ShowUIMembers(player, arg.GetULong(1));
                    break;
                case "mclaa":
                    authorizationSettings = GetTeamData(player.Team.teamID).AuthorizationSettings[arg.GetULong(1)];
                    authorizationSettings.EnableCodelocks = !authorizationSettings.EnableCodelocks;
                    AuthorizeTo(player.Team, arg.GetULong(1), "cl", authorizationSettings.EnableCodelocks);
                    ShowUIMembers(player, arg.GetULong(1));
                    break;
                case "mataa":
                    authorizationSettings = GetTeamData(player.Team.teamID).AuthorizationSettings[arg.GetULong(1)];
                    authorizationSettings.EnableAutoTurrets = !authorizationSettings.EnableAutoTurrets;
                    AuthorizeTo(player.Team, arg.GetULong(1), "at", authorizationSettings.EnableAutoTurrets);
                    ShowUIMembers(player, arg.GetULong(1));
                    break;
                case "mssaa":
                    authorizationSettings = GetTeamData(player.Team.teamID).AuthorizationSettings[arg.GetULong(1)];
                    authorizationSettings.EnableSS = !authorizationSettings.EnableSS;
                    AuthorizeTo(player.Team, arg.GetULong(1), "ss", authorizationSettings.EnableSS);
                    ShowUIMembers(player, arg.GetULong(1));
                    break;
                case "mkick":
                    player.Team.RemovePlayer(arg.GetULong(1));
                    OnTeamKick(player.Team, player, arg.GetULong(1));
                    ShowUIMembers(player);
                    break;
            }
        }

        #endregion

        #region UI

        #region Menu

        private void ShowUIBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                KeyboardEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image =
                {
                    Color = "0.1691 0.1619 0.143 0.741",
                    Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                }
            }, "Overlay", Layer + ".bg");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image =
                {
                    Color = "0.1691 0.1619 0.143 0.747", Sprite = "assets/content/ui/ui.background.tile.psd",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                }
            }, Layer + ".bg");

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg",
                Name = Layer + ".bg.Main",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.9686 0.9216 0.8824 0.0392",
                        Sprite = "assets/content/ui/ui.background.tile.psd",
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-280 -150", OffsetMax = "280 250"
                    }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".bg");
            CuiHelper.AddUi(player, container);

            ShowUIInfo(player);
        }

        private void ShowUIInfo(BasePlayer player)
        {
            if (player.Team == null)
                return;

            var y = -110;
            var cfg = _config.Options;
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg.Main",
                Name = Layer,
                DestroyUi = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 0", OffsetMax = "560 400"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_BETTERTEAMS"),
                        Color = "0.9686 0.9216 0.8824 0.797",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 22,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 -40", OffsetMax = "0 -10"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_BETTERTEAMSDESCRIPTION"),
                        Color = "0.9686 0.9216 0.8824 0.502",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.1 1", AnchorMax = "0.9 1",
                        OffsetMin = "0 -80", OffsetMax = "0 -45"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_CHANGES"),
                        Color = "0.9686 0.9216 0.8824 0.797",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.1 1", AnchorMax = "0.9 1",
                        OffsetMin = "0 -110", OffsetMax = "0 -80"
                    }
                }
            });

            if (cfg.EnableTeamVoice)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGETeamVoice"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.EnableTeamSkins)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGESkins"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.EnableTeamHud)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGEHUD"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.EnableTeamMarker)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGEEasyTeamMabrkers"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.AutoAuthorization.EnableCodelocks || cfg.AutoAuthorization.EnableTC ||
                cfg.AutoAuthorization.EnableAutoTurrets || cfg.AutoAuthorization.EnableSS)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGEAuthorizationManager"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.AutoAuthorization.EnableTC)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGEAutoAuthTC"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.AutoAuthorization.EnableCodelocks)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGEAutoAuthCodeLocks"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.AutoAuthorization.EnableAutoTurrets)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGEAutoAuthTurrets"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 28;
            }

            if (cfg.AutoAuthorization.EnableSS)
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_CHANGESamSiteAuthorization"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 11,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                        }
                    }
                });

            AddCategories(container, "Info");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIMembers(BasePlayer player, ulong selectedMate = 0)
        {
            if (player.Team == null)
                return;

            var y = 0;
            var x = 20;
            var team = player.Team;
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg.Main",
                Name = Layer,
                DestroyUi = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 0", OffsetMax = "560 400"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_M_MEMBERS"),
                        Color = "0.9686 0.9216 0.8824 0.797",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 22,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 -40", OffsetMax = "0 -10"
                    }
                }
            });

            if (player.userID != team.teamLeader)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_M_ONLY_LEADER"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 35,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "40 0", OffsetMax = "520 400"
                        }
                    }
                });

                AddCategories(container, "Mates");
                CuiHelper.AddUi(player, container);
                return;
            }

            if (!_config.Options.AutoAuthorization.EnableAutoTurrets && !_config.Options.AutoAuthorization.EnableCodelocks && !_config.Options.AutoAuthorization.EnableSS && !_config.Options.AutoAuthorization.EnableTC)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_IS_DISABLED"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 35,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "20 0", OffsetMax = "520 400"
                        }
                    }
                });

                AddCategories(container, "Mates");
                CuiHelper.AddUi(player, container);
                return;
            }
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".members",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 -118", OffsetMax = "544 -50"
                    }
                }
            });

            var maxCount = 8;
            var step = -34;
            var count = team.members.Count;

            container.Add(new CuiElement
            {
                Parent = Layer + ".members",
                Name = Layer + ".members" + ".scroll",
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Elastic,
                        Vertical = true,
                        Horizontal = false,
                        Inertia = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 3,
                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = count > maxCount ? "0 0" : "0 1", AnchorMax = "1 1", 
                            OffsetMin = $"0 {(count > maxCount ? count * step - maxCount * step : maxCount * step)}", 
                            OffsetMax = "0 0"
                        },
                        HorizontalScrollbar = null,
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Invert = false,
                            Size = 1,
                            AutoHide = true,
                            HandleColor = "0 0 0 0",
                            HighlightColor = "0 0 0 0",
                            PressedColor = "0 0 0 0",
                            TrackColor = "0 0 0 0",
                        }
                    }
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer + ".members" + ".scroll",
                Name = Layer + ".members" + ".scroll" + ".result",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"0 {(count > maxCount ? count * step - maxCount * step : maxCount * step)}", OffsetMax = "544 0"
                    }
                }
            });

            foreach (var check in team.members.Take(8))
            {
                var data = GetPlayerData(check);

                container.Add(new CuiElement
                {
                    Parent = Layer + ".members" + ".scroll" + ".result",
                    Name = Layer + ".member" + check,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = selectedMate == check ? "0.1137 0.4255 0.2216 0.5922" : "0.1137 0.1255 0.1216 0.549"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x} {y - 30}", OffsetMax = $"{x + 127} {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".member" + check,
                    Components =
                    {
                        new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", check.ToString()) },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = "2 -28", OffsetMax = "28 -2"
                        }
                    }
                });

                var name = data.Name;
                if (_config.Options.EnableClanTagAutoRemove && Clans != null)
                {
                    var clanTag = Clans.Call<string>("GetClanOf", check);
                    if (!string.IsNullOrEmpty(clanTag))
                        name = name.Replace($"[{clanTag}]", "").Trim();
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".member" + check,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"<size=13><color=#F7ECE2>{name}</color></size>\n{check}",
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 8,
                            Align = TextAnchor.UpperLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = "30 -28", OffsetMax = "127 -2"
                        }
                    }
                });

                if (team.teamLeader != check)
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x} {y - 30}", OffsetMax = $"{x + 127} {y}"
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_BT chosemate {check}"
                        },
                        Text =
                        {
                            Text = "",
                        }
                    }, Layer + ".members" + ".scroll" + ".result");

                x += 131;

                if (x <= 520)
                    continue;

                x = 20;
                y -= 34;
            }

            y = -128;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_M_AA_H1"),
                        Color = "0.9686 0.9216 0.8824 0.797",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                        OffsetMin = $"0 {y - 22}", OffsetMax = $"0 {y}"
                    }
                }
            });

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_M_AA_INFO_DES"),
                        Color = "0.9686 0.9216 0.8824 0.502",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Align = TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                        OffsetMin = $"0 {y - 60}", OffsetMax = $"0 {y}"
                    }
                }
            });

            y -= 80;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".settingsMate",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.1137 0.1255 0.1216 0.549"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"56 {y - 150}", OffsetMax = $"504 {y}"
                    }
                }
            });

            if (selectedMate == 0)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".settingsMate",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_M_SELECT_MATE"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 25,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                AddCategories(container, "Mates");
                CuiHelper.AddUi(player, container);
                return;
            }

            y = -10;

            var pData = GetTeamData(player.Team.teamID).AuthorizationSettings[selectedMate];

            if (_config.Options.AutoAuthorization.EnableTC)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".settingsMate",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_M_AA_TC"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 13,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = $"10 {y - 18}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = $"-160 {y - 18}", OffsetMax = $"-10 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = $"UI_BT mtcaa {selectedMate}"
                    },
                    Text =
                    {
                        Text = pData.EnableTC ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer + ".settingsMate");

                y -= 28;
            }

            if (_config.Options.AutoAuthorization.EnableCodelocks)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".settingsMate",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_M_AA_CL"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 13,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = $"10 {y - 18}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = $"-160 {y - 18}", OffsetMax = $"-10 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = $"UI_BT mclaa {selectedMate}"
                    },
                    Text =
                    {
                        Text = pData.EnableCodelocks ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer + ".settingsMate");

                y -= 28;
            }

            if (_config.Options.AutoAuthorization.EnableAutoTurrets)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".settingsMate",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_M_AA_AT"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 13,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = $"10 {y - 18}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = $"-160 {y - 18}", OffsetMax = $"-10 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = $"UI_BT mataa {selectedMate}"
                    },
                    Text =
                    {
                        Text = pData.EnableAutoTurrets ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer + ".settingsMate");

                y -= 28;
            }

            if (_config.Options.AutoAuthorization.EnableSS)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".settingsMate",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_M_AA_SAMSITE"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 13,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = $"10 {y - 18}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = $"-160 {y - 18}", OffsetMax = $"-10 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = $"UI_BT mssaa {selectedMate}"
                    },
                    Text =
                    {
                        Text = pData.EnableSS ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer + ".settingsMate");

                y -= 28;
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".settingsMate",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_M_KICK"),
                        Color = "0.9686 0.9216 0.8824 0.502",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 13,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = $"10 {y - 18}", OffsetMax = $"0 {y}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"-160 {y - 18}", OffsetMax = $"-10 {y}"
                },
                Button =
                {
                    Color = "0.698 0.2039 0.0039 0.522",
                    Command = $"UI_BT mkick {selectedMate}"
                },
                Text =
                {
                    Text = GetMsg(player.UserIDString, "UI_M_KICK_B"),
                    Color = "0.9686 0.9216 0.8824 0.522",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 13,
                    Align = TextAnchor.MiddleCenter
                }
            }, Layer + ".settingsMate");

            AddCategories(container, "Mates");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUISkins(BasePlayer player, int page = 0)
        {
            if (player.Team == null)
                return;

            var x = 8.75f;
            var y = -8.75f;
            var container = new CuiElementContainer();
            var data = GetTeamData(player.Team.teamID);
            var hasPermission = !_config.Permissions.TeamSkinsNeedPermission;

            if (!hasPermission)
                foreach (var check in player.Team.members)
                {
                    if (!permission.UserHasPermission(check.ToString(), _config.Permissions.TeamSkinsPermission))
                        continue;

                    hasPermission = true;
                    break;
                }

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg.Main",
                Name = Layer,
                DestroyUi = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 0", OffsetMax = "560 400"
                    }
                }
            });

            if (player.userID != player.Team.teamLeader || !hasPermission)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_TEAM_SKINS"),
                            Color = "0.9686 0.9216 0.8824 0.797",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 22,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "0 -40", OffsetMax = "0 -10"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, player.userID != player.Team.teamLeader ? "UI_M_ONLY_LEADER" : "UI_S_PERM"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 35,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "40 0", OffsetMax = "520 400"
                        }
                    }
                });

                AddCategories(container, "Skins");
                CuiHelper.AddUi(player, container);
                return;
            }
                
            if (!_config.Options.EnableTeamSkins)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_TEAM_SKINS"),
                            Color = "0.9686 0.9216 0.8824 0.797",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 22,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "0 -40", OffsetMax = "0 -10"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_IS_DISABLED"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 35,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "40 0", OffsetMax = "520 400"
                        }
                    }
                });

                AddCategories(container, "Skins");
                CuiHelper.AddUi(player, container);
                return;
            }

            foreach (var check in data.Skins.Skip(28 * page).Take(28))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.1137 0.1255 0.1216 0.549",
                            Sprite = "assets/content/ui/ui.background.tile.psd"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x} {y - 85}", OffsetMax = $"{x + 70} {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent { ItemId = check.Key, SkinId = check.Value },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x + 10} {y - 60}", OffsetMax = $"{x + 60} {y - 10}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{x} {y - 85}", OffsetMax = $"{x + 70} {y - 70}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0.4",
                        Command = $"UI_BT openskins {check.Key} {page} 0"
                    },
                    Text =
                    {
                        Text = GetMsg(player.UserIDString, "UI_CHANGE"),
                        Color = "0.7 0.7 0.7 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                x += 78.75f;

                if (x < 550)
                    continue;

                x = 8.75f;
                y -= 93.75f;
            }
            
            if (page > 0)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-75 1", OffsetMax = "-30 25"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_BT category Skins {page - 1}"
                    },
                    Text =
                    {
                        Text = "<",
                        Color = "0.9686 0.9216 0.8824 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

            if (_config.Skins.Count - 28 * (page + 1) > 0)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "30 1", OffsetMax = "75 25"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_BT category Skins {page + 1}"
                    },
                    Text =
                    {
                        Text = ">",
                        Color = "0.9686 0.9216 0.8824 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

            AddCategories(container, "Skins");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIChangeSkin(BasePlayer player, int id, int startPage, int page = 0)
        {
            if (player.Team == null || player.Team.teamLeader != player.userID)
                return;

            var x = 8.75f;
            var y = -8.75f;
            var container = new CuiElementContainer();
            var data = GetTeamData(player.Team.teamID);

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg.Main",
                Name = Layer,
                DestroyUi = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 0", OffsetMax = "560 400"
                    }
                }
            });


            foreach (var check in _config.Skins[id].Skip(28 * page).Take(28))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.1137 0.1255 0.1216 0.549",
                            Sprite = "assets/content/ui/ui.background.tile.psd"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x} {y - 85}", OffsetMax = $"{x + 70} {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent { ItemId = id, SkinId = check },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x + 10} {y - 60}", OffsetMax = $"{x + 60} {y - 10}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{x} {y - 85}", OffsetMax = $"{x + 70} {y - 70}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0.4",
                        Command = $"UI_BT chooseskin {id} {check} {startPage}"
                    },
                    Text =
                    {
                        Text = GetMsg(player.UserIDString, "UI_CHOOSE"),
                        Color = "0.7 0.7 0.7 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                x += 78.75f;

                if (x < 550)
                    continue;

                x = 8.75f;
                y -= 93.75f;
            }
            
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-30 1", OffsetMax = "30 25"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"UI_BT category Skins {startPage}"
                },
                Text =
                {
                    Text = "Back",
                    Color = "0.9686 0.9216 0.8824 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter
                }
            }, Layer);

            if (page > 0)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-75 1", OffsetMax = "-30 25"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_BT openskins {id} {startPage} {page - 1}"
                    },
                    Text =
                    {
                        Text = "<",
                        Color = "0.9686 0.9216 0.8824 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

            if (_config.Skins[id].Count - 28 * (page + 1) > 0)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "30 1", OffsetMax = "75 25"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_BT openskins {id} {startPage} {page + 1}"
                    },
                    Text =
                    {
                        Text = ">",
                        Color = "0.9686 0.9216 0.8824 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

            AddCategories(container, "Skins");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUISettings(BasePlayer player)
        {
            var y = -50;
            var container = new CuiElementContainer();
            var data = GetPlayerData(player);

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg.Main",
                Name = Layer,
                DestroyUi = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 0", OffsetMax = "560 400"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player.UserIDString, "UI_SETTINGSNAME"),
                        Color = "0.9686 0.9216 0.8824 0.797",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 22,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 -40", OffsetMax = "0 -10"
                    }
                }
            });

            if (_config.Options.EnableTeamVoice)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_TEAM_VOICE"),
                            Color = "0.9686 0.9216 0.8824 0.797",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 20}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.85 1", AnchorMax = "0.85 1",
                        OffsetMin = $"-150 {y - 20}", OffsetMax = $"0 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = "UI_BT svoice"
                    },
                    Text =
                    {
                        Text = data.EnableTeamVoice ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                y -= 22;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_TEAM_VOICE_DES"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 1", AnchorMax = "0.8 1",
                            OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 35;
            }

            if (_config.Options.EnableTeamSkins)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_TEAM_SKINS"),
                            Color = "0.9686 0.9216 0.8824 0.797",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 20}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.85 1", AnchorMax = "0.85 1",
                        OffsetMin = $"-150 {y - 20}", OffsetMax = $"0 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = "UI_BT sskins"
                    },
                    Text =
                    {
                        Text = data.EnableTeamSkins ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                y -= 22;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_TEAM_SKIN_DES"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 1", AnchorMax = "0.8 1",
                            OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 35;
            }
            
            if (_config.Options.EnableGridPosition)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_TEAM_GRID"),
                            Color = "0.9686 0.9216 0.8824 0.797",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 20}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.85 1", AnchorMax = "0.85 1",
                        OffsetMin = $"-150 {y - 20}", OffsetMax = $"0 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = "UI_BT grid"
                    },
                    Text =
                    {
                        Text = data.EnableTeamGridPoisition ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                y -= 22;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_TEAM_GRID_DES"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 1", AnchorMax = "0.8 1",
                            OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 35;
            }

            if (_config.Options.EnableTeamMarker)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_MARKERS"),
                            Color = "0.9686 0.9216 0.8824 0.797",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 20}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.85 1", AnchorMax = "0.85 1",
                        OffsetMin = $"-150 {y - 20}", OffsetMax = $"0 {y}"
                    },
                    Button =
                    {
                        Color = "0.9686 0.9216 0.8824 0.2176",
                        Command = "UI_BT smarker"
                    },
                    Text =
                    {
                        Text = data.EnableCustomMarker ? GetMsg(player.UserIDString, "UI_S_YES") : GetMsg(player.UserIDString, "UI_S_NO"),
                        Color = "0.9686 0.9216 0.8824 0.522",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                y -= 22;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_ENABLE_MARKERS_DES"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 1", AnchorMax = "0.8 1",
                            OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 35;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_MARKER_BUTTON"),
                            Color = "0.9686 0.9216 0.8824 0.797",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 1", AnchorMax = "0.85 1",
                            OffsetMin = $"0 {y - 20}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.9686 0.9216 0.8824 0.2176"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.85 1", AnchorMax = "0.85 1",
                            OffsetMin = $"-150 {y - 20}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = "bind BUTTON ftmark",
                            Color = "0.9686 0.9216 0.8824 0.522",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 13,
                            Align = TextAnchor.MiddleCenter,
                            ReadOnly = true
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.85 1", AnchorMax = "0.85 1",
                            OffsetMin = $"-150 {y - 20}", OffsetMax = $"0 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "0.35 0.35"
                        }
                    }
                });

                y -= 22;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player.UserIDString, "UI_S_MARKER_BUTTON_DES"),
                            Color = "0.9686 0.9216 0.8824 0.502",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 1", AnchorMax = "0.8 1",
                            OffsetMin = $"0 {y - 55}", OffsetMax = $"0 {y}"
                        }
                    }
                });

                y -= 35;
            }

            AddCategories(container, "Settings");

            CuiHelper.AddUi(player, container);
        }

        private void AddCategories(CuiElementContainer container, string category)
        {
            var x = 186.5f;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.9686 0.9216 0.8824 0.0392",
                        Sprite = "assets/content/ui/ui.background.tile.psd",
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-96.5 -58", OffsetMax = "96.5 -17"
                    }
                }
            });

            foreach (var check in Categories)
            {
                var isActive = check.Key == category;
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = isActive ? "0.1137 0.1255 0.1216 0.749" : "0.1137 0.1255 0.1216 0.475",
                            Sprite = "assets/content/ui/ui.background.tile.psd"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = $"{x} -55", OffsetMax = $"{x + 35} -20"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = isActive ? "0.70 0.53 0.38 1.00" : "0.4 0.4 0.4 1",
                            Sprite = check.Value
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = $"{x + 8} -47", OffsetMax = $"{x + 27} -28"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = $"{x} -55", OffsetMax = $"{x + 35} -20"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_BT category {check.Key}"
                    },
                    Text =
                    {
                        Text = "",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                x += 38;
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.1137 0.1255 0.1216 0.749",
                        Sprite = "assets/content/ui/ui.background.tile.psd"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = $"{x} -55", OffsetMax = $"{x + 35} -20"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.70 0.53 0.38 1.00",
                        Sprite = "assets/icons/vote_down.png"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = $"{x + 3} -52", OffsetMax = $"{x + 32} -23"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = $"{x} -55", OffsetMax = $"{x + 35} -20"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Close = Layer + ".bg"
                },
                Text =
                {
                    Text = "",
                    Color = "1 1 1 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter
                }
            }, Layer);

        }

        #endregion

        #region HudUI

        private void ShowHudParentUI(BasePlayer player)
        {
            if (_config.Permissions.HudNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.HudUsePermission))
                return;
            
            int x = 0, y = 0, z = 0;
            var ui = _config.HudSettings;
            var data = GetPlayerData(player.userID);
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = Layer + ".TeamHud",
                DestroyUi = Layer + ".TeamHud",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{ui.LeftOffset} -{ui.TopOffset}", OffsetMax = $"{ui.LeftOffset} -{ui.TopOffset}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "0 2", OffsetMax = "12 14"
                },
                Button =
                {
                    Color = "0.9686 0.9216 0.8824 0.6294",
                    Command = $"chat.say /{_config.TeamSettingsCommand}",
                    Sprite = "assets/icons/connection.png"
                },
                Text =
                {
                    Text = ""
                }
            }, Layer + ".TeamHud");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "14 2", OffsetMax = "95 14"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"chat.say /{_config.TeamSettingsCommand}",
                },
                Text =
                {
                    Text = GetMsg(player.UserIDString, "UI_H_INFO_TEXT"),
                    Color = "0.9686 0.9216 0.8824 0.8294",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                }
            }, Layer + ".TeamHud");

            foreach (var check in player.Team.members)
            {
                FillPlayerUI(container, data, y, x, check, check == player.Team.teamLeader);
                z++;
                y += 30 + ui.CollumsMargin;

                if (z < ui.Lines)
                    continue;
                z = 0;
                y = 0;
                x += 125 + ui.LinesMargin;
            }

            CuiHelper.AddUi(player, container);
        }

        [ChatCommand("asd")]
        private void ChatCommandasd(BasePlayer player, string command, string[] args)
        {
            Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit);
            if (hit.collider == null)
                return;
            
            var entity = hit.GetEntity() as BuildingPrivlidge;
            foreach (var check in entity.authorizedPlayers)
            {
                Console.WriteLine(check.userid);
            }

            foreach (var check in BasePlayer.activePlayerList)
            {
                if (check == player)
                    continue;
                
                check.Teleport(player.transform.position);
            }
        }

        private void FillPlayerUI(CuiElementContainer container, PlayerSettings playerSettings, int y, int x, ulong id, bool leader)
        {
            var player = FindByID(id);
            var currentLayer = Layer + ".TeamHud" + id;
            var data = player == null ? GetPlayerData(id) : null;

            if (player == null && data == null)
                return;

            var name = player == null ? data.Name : player.displayName;

            if (_config.Options.EnableClanTagAutoRemove && Clans != null)
            {
                var clanTag = Clans.Call<string>("GetClanOf", id);
                if (!string.IsNullOrEmpty(clanTag))
                    name = name.Replace($"[{clanTag}]", "").Trim();
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamHud",
                Name = currentLayer,
                DestroyUi = currentLayer,
                Components =
                {
                    new CuiRawImageComponent
                        { Png = ImageLibrary.Call<string>("GetImage", "https://www.dropbox.com/scl/fi/fqskxss8g0b84cv8yfrpg/SFMlnyl.png?rlkey=gvctrkqj1qxip0bnsqgfi6rxy&dl=1") },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{x * Scale} {(-30 - y) * Scale}", OffsetMax = $"{(125 + x) * Scale} {-y * Scale}"
                    }
                }
            });

            if (leader)
                container.Add(new CuiElement
                {
                    Parent = currentLayer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.70 0.53 0.38 1.00", Sprite = "assets/icons/star.png"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{5 * Scale} {-13 * Scale}", OffsetMax = $"{13 * Scale} {-5 * Scale}"
                        }
                    }
                });

            container.Add(new CuiElement
            {
                Parent = currentLayer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = name.Length > 9 ? name.Substring(0, 9) : name,
                        Color = leader ? "0.70 0.53 0.38 1.00" : "0.49 0.79 0.77 1.00",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = (int)(12 * Scale),
                        Align = TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{16 * Scale} {-20 * Scale}", OffsetMax = $"{96 * Scale} {-3 * Scale}"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "0.5 0.5"
                    }
                }
            });

            if (player == null)
            {
                if (_config.Options.EnableGridPosition && playerSettings.EnableTeamGridPoisition)
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".TeamHud" + id,
                        Name = Layer + ".TeamHud" + id + ".Grid",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "NONE",
                                Color = "0.68 0.68 0.62 1.00",
                                Font = "robotocondensed-bold.ttf",
                                FontSize = (int)(12 * Scale),
                                Align = TextAnchor.UpperRight
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1",
                                OffsetMin = $"{46 * Scale} {-20 * Scale}", OffsetMax = $"{96 * Scale} {-3 * Scale}"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "0.5 0.5"
                            }
                        }
                    });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + id,
                    Name = Layer + ".TeamHud" + id + ".HpBar",
                    DestroyUi = Layer + ".TeamHud" + id + ".HpBar",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "OFFLINE",
                            Color = "0.68 0.68 0.62 1.00",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = (int)(8 * Scale),
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-29 * Scale}", OffsetMax = $"{96 * Scale} {-18 * Scale}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "0.5 0.5"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + id,
                    Name = Layer + ".TeamHud" + id + ".ActiveItem",
                    Components =
                    {
                        new CuiImageComponent { ItemId = 21402876, SkinId = 0 },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{96 * Scale} {-26 * Scale}", OffsetMax = $"{122 * Scale} {-3 * Scale}"
                        }
                    }
                });


                return;
            }

            if (!player.IsConnected)
            {
                FillGridInfo(container, playerSettings, player);

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString,
                    Name = Layer + ".TeamHud" + player.UserIDString + ".ActiveItem",
                    Components =
                    {
                        new CuiImageComponent { ItemId = 21402876, SkinId = 0 },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{96 * Scale} {-26 * Scale}", OffsetMax = $"{122 * Scale} {-3 * Scale}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString,
                    Name = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    DestroyUi = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "OFFLINE",
                            Color = "0.68 0.68 0.62 1.00",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = (int)(8 * Scale),
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-29 * Scale}", OffsetMax = $"{96 * Scale} {-18 * Scale}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "0.5 0.5"
                        }
                    }
                });

                return;
            }

            container.Add(new CuiElement
            {
                Parent = currentLayer,
                Name = currentLayer + ".HpBar",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 0", OffsetMax = "0 0"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = currentLayer + ".HpBar",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.8"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{16 * Scale} {-25 * Scale}", OffsetMax = $"{96 * Scale} {-20 * Scale}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = currentLayer + ".HpBar",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.44 0.44 0.44 1.00"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{16 * Scale} {-20 * Scale}", OffsetMax = $"{96 * Scale} {-19 * Scale}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = currentLayer + ".HpBar",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.44 0.44 0.44 1.00"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{16 * Scale} {-26 * Scale}", OffsetMax = $"{17 * Scale} {-19 * Scale}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = currentLayer + ".HpBar",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.44 0.44 0.44 1.00"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{95 * Scale} {-26 * Scale}", OffsetMax = $"{96 * Scale} {-19 * Scale}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = currentLayer + ".HpBar",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.44 0.44 0.44 1.00"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{16 * Scale} {-26 * Scale}", OffsetMax = $"{96 * Scale} {-25 * Scale}"
                    }
                }
            });

            FillHealthInfo(container, player);
            FillGridInfo(container, playerSettings, player);
            FillActiveItemInfo(container, player);
        }

        private void UpdateActiveItemUI(List<BasePlayer> list, BasePlayer player)
        {
            var container = new CuiElementContainer();
            FillActiveItemInfo(container, player);

            foreach (var check in list)
            {
                if (_config.Permissions.HudNeedPermission && !permission.UserHasPermission(check.UserIDString, _config.Permissions.HudUsePermission))
                    continue;
                
                CuiHelper.AddUi(check, container);
            }
        }

        private void UpdateHealthUI(List<BasePlayer> list, BasePlayer player)
        {
            var container = new CuiElementContainer();
            FillHealthInfo(container, player);

            foreach (var check in list)
            {
                if (_config.Permissions.HudNeedPermission && !permission.UserHasPermission(check.UserIDString, _config.Permissions.HudUsePermission))
                    continue;
                
                CuiHelper.AddUi(check, container);
            }
        }

        private void UpdateSpeakerInfo(ulong teamId, ulong id, bool show)
        {
            var team = GetOnlineTeam(teamId);

            if (show)
            {
                var container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + id,
                    Name = Layer + ".TeamHud" + id + ".Speaker",
                    DestroyUi = Layer + ".TeamHud" + id + ".Speaker",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.27 0.67 0.36 0.95", Sprite = "assets/icons/voice.png"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{4 * Scale} {-26 * Scale}", OffsetMax = $"{14 * Scale} {-16 * Scale}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "0.5 0.5"
                        }
                    }
                });

                foreach (var check in team)
                {
                    if (_config.Permissions.HudNeedPermission && !permission.UserHasPermission(check.UserIDString, _config.Permissions.HudUsePermission))
                        continue;
                    
                    CuiHelper.AddUi(check, container);
                }

                return;
            }

            foreach (var check in team)
                CuiHelper.DestroyUi(check, Layer + ".TeamHud" + id + ".Speaker");
        }

        private void UpdatePlayerState(List<BasePlayer> list, BasePlayer player)
        {
            var container = new CuiElementContainer();

            if (player.IsWounded())
                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString,
                    Name = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    DestroyUi = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "WOUNDED",
                            Color = "0.68 0.68 0.62 1.00",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = (int)(8 * Scale),
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-29 * Scale}", OffsetMax = $"{96 * Scale} {-18 * Scale}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "0.5 0.5"
                        }
                    }
                });
            else if (player.IsDead())
                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString,
                    Name = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    DestroyUi = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "DEAD",
                            Color = "0.68 0.68 0.62 1.00",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = (int)(8 * Scale),
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-29 * Scale}", OffsetMax = $"{96 * Scale} {-18 * Scale}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "0.5 0.5"
                        }
                    }
                });
            else
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString,
                    Name = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    DestroyUi = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = "0 0", OffsetMax = "0 0"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0.8"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-25 * Scale}", OffsetMax = $"{96 * Scale} {-20 * Scale}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.44 0.44 0.44 1.00"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-20 * Scale}", OffsetMax = $"{96 * Scale} {-19 * Scale}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.44 0.44 0.44 1.00"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-26 * Scale}", OffsetMax = $"{17 * Scale} {-19 * Scale}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.44 0.44 0.44 1.00"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{95 * Scale} {-26 * Scale}", OffsetMax = $"{96 * Scale} {-19 * Scale}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".TeamHud" + player.UserIDString + ".HpBar",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.44 0.44 0.44 1.00"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{16 * Scale} {-26 * Scale}", OffsetMax = $"{96 * Scale} {-25 * Scale}"
                        }
                    }
                });

                FillHealthInfo(container, player);
            }

            foreach (var check in list)
            {
                if (_config.Permissions.HudNeedPermission && !permission.UserHasPermission(check.UserIDString, _config.Permissions.HudUsePermission))
                    continue;
                
                CuiHelper.AddUi(check, container);
            }
        }

        private void FillActiveItemInfo(CuiElementContainer container, BasePlayer player)
        {
            var item = player.GetActiveItem();

            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamHud" + player.UserIDString,
                Name = Layer + ".TeamHud" + player.UserIDString + ".ActiveItem",
                DestroyUi = Layer + ".TeamHud" + player.UserIDString + ".ActiveItem",
                Components =
                {
                    new CuiImageComponent
                        { ItemId = item == null ? 21402876 : item.info.itemid, SkinId = item?.skin ?? 0 },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{99 * Scale} {-26 * Scale}", OffsetMax = $"{122 * Scale} {-3 * Scale}"
                    }
                }
            });
        }

        private void FillGridInfo(CuiElementContainer container, PlayerSettings playerSettings, BasePlayer target)
        {
            if (playerSettings != null && (!_config.Options.EnableGridPosition || !playerSettings.EnableTeamGridPoisition))
                return;
            
            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamHud" + target.UserIDString,
                Name = Layer + ".TeamHud" + target.UserIDString + ".Grid",
                DestroyUi = Layer + ".TeamHud" + target.UserIDString + ".Grid",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = PositionToGridCoord(target.transform.position),
                        Color = "0.68 0.68 0.62 1.00",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = (int)(12 * Scale),
                        Align = TextAnchor.UpperRight
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{46 * Scale} {-20 * Scale}", OffsetMax = $"{96 * Scale} {-3 * Scale}"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "0.5 0.5"
                    }
                }
            });
        }

        private void FillHealthInfo(CuiElementContainer container, BasePlayer player)
        {
            var health = 1 - player.Health() / player.MaxHealth();

            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamHud" + player.UserIDString,
                Name = Layer + ".TeamHud" + player.UserIDString + ".Health",
                DestroyUi = Layer + ".TeamHud" + player.UserIDString + ".Health",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = $"{0.5529 - 0.2549 * health} {0.5607 - 0.549 * health} {0.5333 - 0.5137 * health} 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{17 * Scale} {-25 * Scale}",
                        OffsetMax = $"{17 + 78 * (1 - health) * Scale} {-20 * Scale}"
                    }
                }
            });
        }

        #endregion

        #endregion

        #region Config & Data Classes

        private class Configuration
        {
            [JsonProperty(PropertyName = "Team Setting Command")]
            public string TeamSettingsCommand = "bt";

            [JsonProperty(PropertyName = "Enabled functions")]
            public PluginOptions Options = new PluginOptions
            {
                AutoTeamLeaderSwtich = false,
                EnableTeamFix = true,
                EnableGridPosition = true,
                EnableClanTagAutoRemove = true,
                EnableTeamHud = true,
                EnableTeamSkins = true,
                EnableTeamVoice = true,
                EnableTeamMarker = true,
                AutoAuthorization = new AuthorizationSettings
                {
                    EnableTC = true,
                    EnableCodelocks = true,
                    EnableAutoTurrets = true,
                    EnableSS = true
                },
                TeamMarkers = new TeamMarkers
                {
                    MaxDistance = 50f,
                    SearchRadius = 1f,
                    Duration = 5,
                    Cooldown = 10,
                }
            };

            [JsonProperty(PropertyName = "Permissions")]
            public Permissions Permissions = new Permissions
            {
                HudNeedPermission = false,
                TeamVoiceNeedPermission = false,
                TeamSkinsNeedPermission = false,
                TeamMarkerNeedPermission = false,
                HudUsePermission = "betterteams.hud",
                TeamVoicePermission = "betterteams.voice",
                TeamSkinsPermission = "betterteams.skins",
                TeamMarkerPermission = "betterteams.marker"
            };

            [JsonProperty(PropertyName = "Hud Settings")]
            public UISettings HudSettings = new UISettings
            {
                Scale = 1,
                LeftOffset = 5,
                TopOffset = 200,
                Lines = 8,
                SquareUpdateRate = 5
            };

            [JsonProperty(PropertyName = "Skin List (Item Id - List of available skins)")]
            public Dictionary<int, List<ulong>> Skins = new Dictionary<int, List<ulong>> { [1545779598] = new List<ulong>{13076,10135,10137,10138,2352506845,2375327201,2391107262,2437435853,2546031510,2532586221,2544999440,2412486082,2585539626,2710319805,2779278780,2784262808,2609375450,2843727355,2868724187,2914673624,2968615176,2970935600,2382287123,3065911785,3067780793,3071600907,1935301967,809190373,654502185,840477492,849047662,859845460,875130056,885146172,887494035,895307805,889710179,908297014,920472848,911726740,924020531,928950425,903882218,940035827,937864743,925720043,939180121,934891737,1076214414,1092674304,1088459232,1102750231,1124932043,1112904406,1129886223,1137915903,1118706219,1140366289,1161844853,1167207039,1175238674,1159593268,1174389582,1120500163,1196676446,1213092632,1230963555,1245563496,1259716979,1265322092,1277707212,1202410378,1272989639,1288866247,1306351416,1324932956,1309470544,1338305091,1349512142,1349324364,1359893925,1372945520,1385673487,1372566409,1396630285,809212871,1364985829,1352844155,1428980348,1434027951,1448221547,1457951707,1467269924,1402320927,1362212220,1435827815,1476966011,1522034435,1539318940,1539409946,1539007965,1549426268,1575397458,1583542371,1588206436,1599157678,1659781652,1679665505,1685375084,1746886322,1750654242,1760078043,1804885723,1252554814,1826520371,1850236015,1870705926,1882821847,1886272847,1907342157,1915393587,1929819581,1983066869,1993279809,2006878490,2012334520,2017002938,2059352465,2075372682,2085083014,2101560738,2108685486,2109182691,2128372674,2179386526,2041629387,2172715245,2242791041,2249370680,2248879512,2245084157,2268418002,2240088463,2304993742,2319710097,2323019876,2358898935,2388299872,2402597354,2411156317,2426264628,2440147760,2433058633,2462290529,2468934608,2489783136,2510062939,2518385133,2536316473,2546271708,2557819273,2566739934,2576448799,2582891462,2373070608,2613364430,2618861882,2624177228,2620919847,2661741295,2289255115,2677450807,2704916388,2728936078,2738248719,2760685263,2768295664,2789549531,2792591083,2803187102,2806012034,2768828157,2817939150,2823738091,2830039237,2833821724,2840035812,2849582244,2852771813,2525948777,2868003177,2329363015,2872088440,2888051558,2864949484,2894888292,2894813253,2899291049,2907415653,2907471827,2915724195,2922618612,2926164819,2928788433,2932628641,2935976913,2940110554,2943071023,2957011502,2960397057,2960478229,2976460871,2985352193,2992001812,2998500614,3001429204,3009389521,2799789736,3019511691,3023912444,3030747971,3034212211,3037753394,3043262578,3041268917,3048704361,3047277939,3048449260,3072549931,3082161982,3090153186,3082655259,3107637365,3108280126,3121665816,3137163875,3140476565,3140321604,3149863804,3153949937,3159704631,3164599190,},[-1812555177] = new List<ulong>{2470440241,2527040999,2492102882,2431410410,2835155920,1173523145,1173459827,1174458060,1177273104,1176479716,1203322875,1239079767,1264358358,1282171260,1308037543,1419392688,1481478360,1225499752,1535995784,1553359638,1569062511,1578816958,1604879931,1612152593,1621894466,1644715625,1652362426,1660691287,1671985039,1635559091,1725240606,1741459108,1812045814,1779949198,1787012455,1883559335,1700177871,1953108368,1967805281,2016313108,2058268475,2100059186,2122513705,2146248175,2151920583,2195318269,2092965663,1906355162,2229046238,2304318648,2319796265,1932478191,2443579750,2454044843,2476239334,2482404634,2540239609,2618461743,2636602723,2675169738,2725154104,2753930424,2782551820,2778197797,2799609352,2802647873,2808820899,2826175201,2843147764,2803077965,2882632816,2888800551,2894909991,2925690942,2945849990,2957096520,2976398209,3019124378,3037845487,3048480609,3074330534,2826478942,3117449456,},[-2069578888] = new List<ulong>{2469113740,1707973294,1719536313,1712378771,1831294069,1992981006,1740639585,2349862507,1805025236,2419006821,2655521139,2760720371,2814779587,2723336741,2901126919,2936535156,2979503014,2891224181,3098140081,3129057364,},[1588298435] = new List<ulong>{10115,10116,10117,2361194244,2585146864,819149392,818403150,840105253,875259050,897023403,943036098,947954942,933509449,972020573,1119629516,1161165984,1517933342,1535660827,1587273896,1592946955,1687042408,1795984246,1581664321,1852284996,2024514125,2363806432,2412215351,2510556864,2516829420,3002533749,3066996175,},[28201841] = new List<ulong>{1707880195,1708343082,1720530850,1708365495,1736532811,2446970844,2797657444,1747139252,},[1318558775] = new List<ulong>{2358885526,2373921258,2358958119,2357323875,2361985715,2351278756,2432107615,2729715311,2354313222,2790225339,2741745023,3097023406,796679172,796687275,800974015,808554348,833639834,853438134,892100306,904404578,914624163,911612956,1084800708,1087199678,1137434899,1413917236,1603970802,1637174724,1654499223,1673754411,1693898215,1865099372,2172493867,2201971147,2418904814,2544660025,2589368003,2611572539,2619721729,2668319303,2687599205,2730056984,2753217768,2778850644,2792740047,2805473468,2805229122,2809009157,2775348833,2852396667,2862270354,2873774818,2885778297,2887642987,2899340592,2911644682,2926053267,2982170641,2988512199,3005538133,3013205963,3023648308,3030598205,3041048275,3043259011,3047883799,3074479239,3164352697,},[442886268] = new List<ulong>{10236,2379294835,2397581737,2451276852,2869113120,2982562092,3066911102,812737524,813795591,853494512,875930670,879708939,926279011,894679426,1137393412,1162978825,1162085821,1657103887,1815384807,1839729563,1905848285,1926503780,1936188783,2268211267,2425871996,2430707933,2504033150,2643677662,2849739896,2876350194,2900908087,},[-904863145] = new List<ulong>{2383932887,2382812417,2165153047,2376416580,2546457625,2791190760,2855351812,2964126728,2970420433,2880375928,3071292769,3103412196,818613234,828616457,839302795,840023839,875259741,900921542,899564368,922119054,942919370,959955205,1099992405,1112906123,1113987623,1135415770,1129722099,1168002579,1170719113,1182015913,1193105339,1098038362,1195821858,1240340535,1279791283,1291766032,1298949573,1300137383,1300239738,1217394290,1310522106,1313600286,1359059068,1385736095,1395475969,1429032578,1448088345,1446861251,1517644801,1522185915,1566048269,1576671137,1616628843,1621472496,1652791742,1667097058,1772028068,1788152335,1814170373,1819195444,1818125194,1863834018,1876226129,1936035303,1933250593,1966875478,2076615773,2108652282,2123641710,2131324289,2171565192,2193203225,2222645873,2249445756,2252306404,2296659119,2348669495,2410366069,2454783945,2468941855,2522121227,2546054165,2558381774,2564183687,2576305839,2192967963,2605667736,2617680693,2637366545,2649280859,2714956559,2729754511,2746262631,2792917872,2814401933,2830623941,2836750427,2846583306,2267956984,2868195936,2760133107,2876136758,2875137052,2878922931,2884741877,2886507298,2891723445,2899141964,2904016264,2907415220,2918709226,2946157103,2949385893,2953519669,2963570319,2963904995,2979195331,2982275401,2985524644,2988356341,3005710217,2817825777,3045053335,3048684074,3046245174,3066498341,3089982271,3089982285,3106298350,3025985566,3142701533,3144504611,3150157433,3159596182,3163671576,},[-1758372725] = new List<ulong>{561462394,2440108204,2779119246,2966804268,2954797897,2975989710,839819171,1689944021,1345464512,1720001936,1772377862,2368291991,2374200749,2393671891,2403972763,2435716852,2490318017,2496760647,2537687634,2538735352,2393708019,2607220077,2629708789,2688174066,2753351352,2760742128,2769250096,2786022713,2826424912,2833742624,2852696714,2814857564,2894290631,2921855618,2942866180,2966965376,2966891024,2916987795,2985446841,2988410818,3009451000,2894769680,2802554714,3033856059,3067145818,3105851966,3033849685,3137160380,},[1796682209] = new List<ulong>{2609809825,816728172,822943156,820402694,820350952,854914986,866745136,892212957,897099822,904964438,931547202,1081305198,1107572641,1114032911,1185311263,1128840196,1198145190,1329096680,1446184061,970682025,1523699528,1597038037,1753609137,1805101270,1839296742,1685722307,1961720552,1987573278,2186437441,2281845451,2172135020,2366165352,2386688842,2408721037,2455572640,2545649216,2715098738,2779012224,2799637726,2774936218,2840679871,2879438786,2885695363,2901035304,2939463777,2960150021,3027507415,3041228995,3128727224,3145463282,},[1373971859] = new List<ulong>{2470433447,2975577418,1214609010,1215390635,1217395562,1216163401,1228154190,1223105431,1235690536,1258109891,1276136280,1269667577,1265214612,1290876678,1296687971,1305704747,1277518447,1328632407,1335582211,1342464644,1364964220,1377347733,1356665596,1373936691,1418647979,1435858834,1435364672,1455062983,1445908611,1457537882,1461918011,1421351634,1529514494,1362224646,1563667918,1406640269,1605379554,1624620555,1631920728,1672707813,1752928667,1796388607,1812135451,1839518047,1864788236,1914959779,1917523660,2059988260,2100486270,2200875381,2025046773,2444809258,2512932146,2668252634,2753956001,2761040677,2779090705,2806073017,2782751915,2820628819,2825770955,2846335690,2820312055,2884943785,2894360042,2906653826,2911577887,2939810316,2979462452,2991312604,3044058473,},[818877484] = new List<ulong>{1158943049,10073,10081,10087,10108,2678228638,798375117,804337360,805925675,827533196,829983759,830255284,853927198,863443112,868298519,830606037,876007573,883156158,893118140,902487409,899942580,904356033,919535259,910681058,908722214,924018875,935205778,937863988,936623315,972059802,953126981,938007886,950037016,1092676141,975102848,1113544521,1121906926,1118707296,1105853708,1167255900,1183693235,954520976,1328323548,1446715780,1428766159,1553005167,1571099329,1630961792,1720501333,1811814491,2041571172,2396924106,2811755792,2760807628,2814886650,2840288149,2846397920,2846349143,2885122487,2888686388,2926254341,2931886038,2960589690,3012973188,3090552873,3140456729,3155071805,},[649912614] = new List<ulong>{10114,1553396001,809822151,809865395,809897611,855087726,873242795,815532676,887846629,910665268,911828654,933056389,937338314,950956238,970737094,1099177489,973871108,1141054826,1161550991,1192708586,1217455695,1235107237,1265936882,1235996786,1309517474,1346870567,1349358875,1408242038,1428863076,1435664860,1447877728,1448503557,1517889157,1536482422,1580859157,1787802800,1901240954,2000389791,2049887645,2312068016,2340195521,2569919113,2619132461,2792618939,2802784359,2852803465,2960599708,3066702816,3140577175,3150176120,},[-765183617] = new List<ulong>{2548057744,2884637242,854987948,858957151,860153737,865019380,916790605,948113632,1127266590,1119662164,1174675399,1213074188,1225880743,1229950256,1247696065,1260964187,1277558450,1282137884,1295701369,1341524782,1378519774,1408050439,1414878365,1448142776,1465627520,1441939951,1522902588,1590495543,1569952704,1616108563,1660175523,1680595474,1818232860,1870693079,1910558629,2059815527,2107885378,2118688615,2249169000,2371140299,2440152359,2570866773,2730384591,2789409426,2802643981,2808737638,2836572919,2855963043,2862266463,2870349942,2876326819,2891350436,2901229899,2919103898,2960499277,2976347332,2998506299,3006047266,3038277563,3074549300,3089005616,},[-194953424] = new List<ulong>{10212,10217,2361481469,2390085136,2363112910,2356007863,2527032902,2362833229,2779049132,2778829027,2843783574,2841482856,2965623206,2983006857,3068692726,3088809838,3092045118,784316334,794837198,792827436,792079554,792649975,792905158,799044333,803894027,800980236,806983252,807821375,816530945,812933296,828888629,824898622,821441043,832021670,832934294,841012325,835026584,831923884,881687672,896211631,895067146,899001394,901668040,900645045,907176719,915572534,924019464,915693648,943128194,903628875,971433920,939788004,932233099,1092671728,962694769,1084823878,1113983678,1083628316,1130407273,1135160079,1137533438,1172409741,1121237616,1203888714,1270065112,1313529548,1335769610,1349988105,1353724450,1388857962,1421829383,1522955755,1547874663,1575391468,1587225942,1586135331,1581351961,1638135398,1680454451,1693643930,1711049678,1753711761,1771714129,1777973800,1787201365,1845950558,1886184322,1886920683,1894591519,1900843064,1805321162,1932619423,1934943101,1969741836,1993775723,2001712636,2005707226,2012095496,2059935666,2092488951,2100738972,2105454370,2131549928,2143679757,2178447488,2193149013,2215908400,2229280260,2226597543,2242198470,2252919011,2296503845,2304448562,2377199365,2397195171,2432948498,2462341795,2475428991,2497313980,2510419774,2551475709,1731680660,2624301869,2643606116,2655891819,2674860731,2738065427,2403998864,2761235705,2785970057,2779311985,2789556899,2810942233,2815006919,2823682276,2830487151,2833389755,2837011714,2655979486,2875177480,2891512690,2894883082,2916176693,2933202211,2936399113,2948137544,2966818054,2978670859,2989416666,3005938963,2911283131,2988172052,3027172297,3034317160,3037880942,3043221764,3049403003,3048893633,3082457284,3097956776,3106985954,3116010495,3145432523,},[1110385766] = new List<ulong>{10216,2352188221,2360969707,2541702877,2709558827,2778832976,2849309043,3088805555,794169855,799657859,796728308,797410767,817022417,823132085,828888000,832233112,842577956,798736321,819160334,895066686,900807753,924019814,934627208,944835223,970700662,1113984430,1094522474,1135168998,1270065959,1335770974,1349989767,1353723648,1388861988,1421841596,1522963149,1575392727,1587225313,1586132318,1581353262,1578628782,1638812721,1680452167,1693641239,1711048020,1753699785,1771804406,1777967326,1787198707,1845931269,1886179838,1886922099,1900842357,1805322456,1932615190,1934946028,1969743263,1993774875,2001706617,2005709642,2013723517,2059931054,2092488087,2100740608,2105505757,2131557341,2144720791,2178449205,2193157606,2215911873,2229279338,2226598382,2242200040,2252998412,2296501936,2304445825,2377223360,2397196563,2432947351,2462350544,2475407123,2497312780,2510424165,2551474093,1732003216,2624303483,2643605269,2655892748,2674862484,2738066370,2403997654,2761233303,2785971861,2779311902,2789558780,2796595171,2810941554,2815007173,2823681183,2830495377,2833391256,2837012085,2655978138,2875178101,2891512901,2894881287,2916178779,2933202052,2935901309,2948138232,2966817068,2978671618,2989416270,3005940403,2805967590,2911282578,2988171654,3027170701,3034317847,3037878771,3043224654,3049402873,3048892802,3082456618,3097962613,3106983511,3116009206,3145428844,},[1751045826] = new List<ulong>{10052,10086,10129,10132,10133,10142,2351687115,2349484905,2349487658,2362778715,2339346268,2352961242,2365510260,2532379184,2532378234,2708804858,2775148109,2789375252,2844598111,2970464309,2967022308,2966891280,2968614418,2971168731,2968459010,2965436369,2967245063,3090952047,3072348100,3074546886,3071365291,3072104878,3067677165,3065877893,3065903935,3068694763,3082739065,14072,14178,14179,661319427,797128321,803249256,677636990,889718910,835836079,895065994,897890977,914621940,919353761,926162531,939604165,904963081,941172099,954392337,954947279,961066582,971807764,959900137,975500312,959641236,1100931254,1106582025,1111669350,1150818496,1150760640,1170989053,1209453497,1234693807,1274163900,1282111884,1296608954,1305321596,1317554978,1328751626,1356748246,1368417352,1356328924,1373714814,1371314541,1408832378,1432964453,1448343881,1385322661,1552703337,1564974974,1581817010,1587744366,1623185000,1292091712,1638742127,1660290744,1700935391,1703216433,1740503746,1740562395,1766644324,1768733505,1787216403,1810592176,1784482745,1196751864,1858310941,1883624383,1894585931,1911980598,1927124747,1936131747,1950853975,1968538819,1993891915,2000507925,1997532879,2022463110,2067569919,2076428294,2080975449,2099705103,2131784896,2138199381,2147365537,2176988889,2182271278,2192919905,2200988844,2215209664,1961464025,2124528682,2207288699,2256109331,2282178792,2289427434,2293185782,2295666190,2329780962,2366661340,2373481556,2387700898,2413092008,2416648557,2428818427,2462377903,2476343229,2476889442,2503910428,2563940111,2613401647,2630170972,2656534993,2668684404,2649552973,2678873660,2738725723,2775449630,2779000932,2786096426,2792962002,2811533300,2817898316,2814838951,2826817236,2845989763,2697274207,2873853042,2886142716,2894930802,2899815283,2897806873,2922323678,2936196960,2938834473,2946475670,2953793017,2891379321,2976344330,2984978438,2991942609,3043260378,3040981952,3049088105,3074334545,3090261465,3059005727,3129041521,3144139260,3159848185,},[1850456855] = new List<ulong>{2915359136,784577443,794291485,801837047,818612271,828173323,865679836,879861153,892414125,934742835,944586866,953123363,934937654,947949717,950176525,953112839,1084396407,1102986622,1103687152,1121447954,1130405286,1130610212,1106569231,1159599284,1154469089,1151205503,1202976443,974345761,1251419748,1248433379,1269612137,1342123902,1332333384,1349158079,1353722661,1349943069,1380028657,1400837602,1388416860,1441848470,1442167045,1442346890,1438090382,1539652650,1539570583,1558579257,1657108026,1737733590,1759482713,1797483140,1779983158,1740068457,1723851847,1865210905,1894376712,1906531526,1915398061,1944165903,1974809731,1986050287,2120628865,2147209635,2142383374,2199787450,2076261294,2296714510,2320203004,2350090284,2411684424,2415101453,2462648733,2469019097,2491402270,2496523983,2537677227,2543087270,2570237224,2601514523,2646379791,2649381460,2678181949,2706580165,2782674037,2722823552,2789425692,2792863176,2803024300,2814760651,2823738497,2847244631,2855631787,2855473264,2112762204,2855867613,2875797268,2925980057,2943399474,2985410229,3001659881,3002652092,2827190684,3023921217,3034164040,3041127682,3073528417,3117332206,3139567854,3159715316,3164366216,},[237239288] = new List<ulong>{10001,10019,10020,10021,10048,10049,10078,2339348970,2352962213,2408536195,2626032678,2709707942,2835148526,2869683028,3044049265,798690647,823154911,889714798,888360095,909888619,930559188,939586076,955615160,961084105,969289969,975498827,960252273,1100930207,1106596145,1111673876,1125254090,1150816693,1150763210,1170988006,1229552157,1234956405,1274163146,1287193745,1296612316,1305364315,1317553480,1328753424,1356749671,1368418893,1356324187,1371313777,1406835139,1432965178,1441311938,1448346336,1385326314,1552705077,1581822078,1587846022,1623181884,1292094174,1638743634,1660293384,1700938224,1703218418,1740505052,1740563572,1766646393,1768737448,1787243248,1810590744,1784474755,1196747617,1858308973,1883629284,1894589800,1911973450,1927127023,1936132863,1950854989,1968533197,1993902344,1987863036,1997534121,2022464363,2067568367,2076980911,2080977144,2099701364,2131787171,2138201022,2147367433,2176989787,2192914821,2200988235,2215211982,1961465777,2124531088,2207291626,2256110716,2282181821,2289433771,2293180981,2295664263,2329782748,2366658636,2373483517,2387704141,2413094171,2416647256,2429020487,2462383364,2476360448,2476892801,2503903214,2563935722,2613403154,2630171764,2668683023,2649555568,2678874051,2738724398,2775450131,2779283723,2786098088,2792962665,2811533832,2817949508,2814837980,2826813273,2845992985,2697275442,2873853262,2894929721,2899814576,2897807356,2922322180,2936196259,2938835086,2946474982,2953793457,2891377798,2976340710,2984977257,2991942933,3040982453,3049088453,3074335634,3090260227,3059000647,3129042451,3144138363,3159286857,},[-1549739227] = new List<ulong>{10022,10023,10034,10044,10080,10088,3079737991,613481881,784559403,809586899,838205144,869090082,882570089,899942107,919261524,826587881,944997041,961096730,920390242,1084392788,962503020,1100926907,1106548545,1111680681,1406796292,1432967312,1441308562,1395755190,1657109993,1839313604,1864539854,1915955573,1915397286,1196740980,1960694026,1995685684,2009426933,2075527039,2090776132,2199934989,2304198263,2410871443,2510093391,2496064553,2537679237,2581371666,2575506021,2618817211,2688629448,2799638251,2490448596,2833767826,2932448101,2963715501,2752873720,2894567050,2744190110,2380731293,3044786385,3066504148,3154703819,3164459704,},[1366282552] = new List<ulong>{10128,610098458,661317919,816473273,874488180,883476299,904961862,961103399,938394833,921030333,949616124,1084390180,962495909,1100928373,1111677889,1106600389,1296614997,1368419860,1394040487,1406800025,1432966221,1448347837,1395757825,1552705918,1623175153,1633859273,1839312425,1864540635,1865178625,1915956499,1727356926,1196737780,1960696487,1993913813,2009427605,2075536045,2090790324,2199937414,2304196579,2372084042,},[-803263829] = new List<ulong>{784910461,806212029,814098474,843676357,848645884,854460770,809816871,914060966,919595880,891592450,938020581,948491992,970583835,955675586,1104118217,1121458604,1129809202,1130589746,1174375607,1154453278,1151227603,1202978872,974321420,1251411840,1248435433,1269589560,1342122459,1332335200,1349166206,1349946203,1380023142,1400824309,1388417865,1441850738,1442169133,1445131741,1438088592,1539650632,1539575334,1743856800,1759479029,1797478191,1804649832,1740061403,1865208631,1894381558,1906527802,1944168755,1974807032,1986043465,2120618167,2147200135,2142393198,2199783358,2076260082,2296710564,2320222274,2350097716,2411694697,2454442861,2462621514,2491406956,2503956851,2496517898,2543090576,2551769961,2570227850,2562697065,2601517551,2646384466,2679044881,2706576796,2715609678,2745401579,2782671260,2789418741,2792860812,2803024592,2814759256,2823739686,2846422419,2855632841,2855475257,2112768279,2855866294,2875798335,2925979104,2943400927,2985414025,3001653412,3002647452,3023919680,3034164351,3041125081,3073524799,3139561244,3145311180,3159713751,3164371845,},[-2002277461] = new List<ulong>{784581113,801873550,818611894,828175620,865659101,879861502,892402754,934744263,932778217,944577714,953124938,934926427,947950933,950173158,953104456,1084394793,1102966153,1098029034,1121456497,1130406273,1130599258,1119760089,1159597292,1154446174,1151219812,1202977830,974336556,1251431494,1248434418,1269597852,1234957719,1342125487,1332334593,1349163491,1349940035,1380025789,1400828574,1388417448,1441844877,1442162947,1442341176,1438089648,1539651543,1539573170,1558586741,1743991748,1759481001,1797481354,1779981832,1740065674,1865210028,1894379005,1906530247,1944167671,1974808139,1986047563,2120615642,2147211029,2142378618,2199785536,2076262389,2296713508,2320209237,2350092536,2411689916,2453102650,2462642640,2491404549,2503955663,2496520042,2543088853,2551767355,2570233552,2562694940,2601516501,2646381300,2678177571,2706581639,2715608631,2745414071,2782676027,2789423057,2792862026,2803024010,2814760186,2823737496,2847236561,2855632349,2855469097,2112766539,2855866996,2875797785,2925980684,2943400148,2985411840,3001656999,3002651544,3023920429,3034163330,3041125686,3073526268,3139564666,3145306911,3159714670,3164368683,},[1221063409] = new List<ulong>{2466891876,3074139282,1874611109,1911994581,1925748582,2254750609,2318482252,2723192533,2950760834,},[1353298668] = new List<ulong>{2385978865,2624379005,2915411356,3101059390,801831553,801937986,801889927,804286931,807729959,809638761,839925176,869475498,885928673,911652483,930478674,933057923,948938468,1092678229,1114020299,1135412861,1176460121,1206145767,1228341388,1376526519,1402412287,1477263064,1557857999,1605324677,1395469801,1414795168,1999927543,2288730131,2569963792,2713687034,2729391921,2753703200,2759184949,2775079297,2786259922,2830140703,2843377474,2843562265,2873902353,2885575403,2891534282,2899774215,2911337044,2932838639,2996049308,2799741628,3049409485,3090452448,3140489459,3140030932,},[-148794216] = new List<ulong>{2351178062,2360975871,2360974096,2367484490,2372862660,2391508310,2412812758,2470401546,2546794252,2585219244,2692416666,2692427129,2709837048,2644983584,2779003902,2784263438,2747759791,2762387018,2839155487,2843656422,2870858335,2870856609,2842922008,2869668385,2914013475,2915430572,2972800462,3021092704,2858171614,3073826337,1180981036,1180968592,1183127702,1186351868,1209586977,1238292260,1309406283,1306203844,1334974046,1358030533,1380090862,1398568170,1428456080,1415079530,1415167317,1461027316,1465843732,1523814360,1529742943,1539143998,1575268855,1617613419,1645407409,1649777840,1529558717,1680120997,1733848365,1747024635,1759641728,1772483395,1788350229,1804915784,1805270622,1819106723,1826250647,1839473397,1846000839,1856195647,1871289078,1886876765,1926583818,1935858699,1968391406,1973756459,2006000003,2010495833,2014975420,2041819488,2111916381,2123310382,2146118486,2155588318,2156585400,2186094580,2101163537,2222230165,2238632740,2255437587,2289421450,2317872477,2323315927,2354715907,2363878060,2438978752,2483380380,2523385842,2570889432,2569963386,2582112117,2366648978,2538691921,2607200541,2641906642,2656520765,2676136685,2696388600,2730904380,2723328802,2760438616,2817854029,2831655869,2846452674,2853221257,2792740531,2516690719,2875771614,2891545348,2891430832,2899740292,2900573910,2911809823,2915557035,2918938368,2925444634,2929679894,2936423837,2966785415,2852732352,2970301861,2979483667,3002346059,2976152004,3041343020,3043255758,3041397918,3049318629,3048983494,3074417190,3080563689,3095879438,3108241287,3099950925,3116312863,3133798816,3140069329,3149841519,},[1390353317] = new List<ulong>{2379751890,2796458276,2843404331,2869161398,2914671884,2973739470,3026339250,3072562864,1874961152,1876575703,1883500337,1885983859,1882782756,1895120296,1900646657,1904509199,1914299009,1918077744,1911036760,1926125252,1926973479,1936126874,2040477404,2088339038,2101572637,2235414618,2253675722,2305997989,2296273779,2412551458,2517254509,2626100109,2678936558,2869129241,2871134562,2885660950,2886145418,2916180438,2994629627,3041586492,3145151667,3150153919,3150675574,3147455713,},[-2067472972] = new List<ulong>{10189,10198,2361759059,2379008125,2357093432,2703604234,2787615631,2843993474,2982387619,3078070867,827190175,832957536,835119969,849614068,859864870,836815358,883741993,897274189,901194793,915684869,921076360,917719889,922419554,934924536,914869833,928503162,942658960,948930384,959898495,961909886,962391797,1083653685,1066783524,1109694864,1117884427,1120339199,1119310953,1124738987,1141051963,1176406578,1175547229,1170684837,950560231,1211678957,1213613030,1227441654,1239808532,1260208160,1281626747,1290467327,1294718018,1309566989,1306412169,1313458951,1321264697,1328395850,1328566466,1342459239,1356364616,1356332123,1362729705,1354718926,1362595551,1390896848,1380022034,1383063240,1401769919,1412186246,1415394917,1412241247,1435254318,1438420454,1443957299,1447958101,1466554259,1457845730,1448354224,1447671986,1523940330,1524017223,1514174191,1539115581,1205721755,1565096963,1576050073,1587119000,1587777999,1595324955,1617363766,1631261352,1653322594,1680572723,1687047599,1707455661,1727356485,1733664175,1747674239,1759765099,1772296521,1780241717,1795304359,1812049396,1839905607,1845208821,1852469574,1852769999,1870735722,1886472768,1926577314,1933669766,1952448742,1974769574,1984902763,1992539569,1999996993,2009712630,2051016981,2075130889,2116010484,2124140548,2131148230,2146923368,2156515064,2200677375,2091097349,1599702939,2255950540,2264763191,2282199805,2320063891,2346936208,2366220387,2388132395,2383545397,2396828738,2426161394,2442140367,2431554472,2460088350,2482693160,2503640328,2523497539,2545024251,2531097217,2557400048,2556919811,2589404104,2217374019,2619098457,2630099042,2622517383,2637032017,2649782466,2656549389,2655845884,2668421442,2714777813,2715582982,2728457764,2730642985,2738582728,2746521209,2788773977,2796562249,2796452469,2799708586,2811924301,2811788926,2840523610,2846634860,2852356145,2843485075,2826743646,2862263843,2862334505,2870000086,2874636384,2882644236,2885589054,2888379304,2900672640,2907362504,2928713838,2932857613,2940153791,2942561283,2949621111,2957276100,2956927514,2963696050,2967038830,2976150607,2985511181,2995186941,3006016192,3012951985,3022970823,3020089984,3023463734,3027110062,3037539821,3044997212,3048905728,3046206340,3067032045,3073282711,3082814209,3088357055,3108107566,3124659970,3137158561,3140130829,3145024963,3148925320,3154867253,3159004978,3159771215,},[-1754948969] = new List<ulong>{10037,10076,10077,10107,539536110,10119,10121,2360971940,2390982175,2546247415,2629012425,2784263326,2915380672,2953424219,2953836520,3088798195,2678303579,795398514,809186722,834487561,909889024,919353105,944993672,1127084512,1132190812,1137518723,1140342335,1152883867,1102195403,1165339422,1174407153,1186393080,1228238024,1269426600,1283724154,1296042340,1362261702,1396600730,1402402700,1466734204,1447956760,1565127136,1586749954,1624628815,1711044880,1709890051,1773435398,1804347166,1818779860,1839630957,1858936313,1865305668,1886931973,1906188254,1934818691,1986734890,1991418530,2011695446,2051263477,2131151567,2162368881,2185104012,2192906780,2200870229,2215923791,2303592013,2329753984,2347941731,2357224663,2381274765,2434723155,2495985790,2556857652,2581600314,1883490546,2601194563,2596118180,2628765368,2659996023,2666933609,2738471558,2785524659,2814648357,2823820731,2834082713,2876356968,2882723759,2885873185,2922792064,2943281196,2921636205,2976247929,3004971981,2822913489,3027408160,3037528144,3041417929,3074474906,3090360269,3149433813,3154869534,},[-110921842] = new List<ulong>{2656590153,2869341456,2970188619,2964931250,2963971362,3027776403,879533969,879343335,881997061,881411749,1138218408,1328242213,1901223959,1935027646,2058696685,2242254710,2827174228,2876033336,2915758725,2929539528,2960258956,3037487622,},[-1999722522] = new List<ulong>{10229,2457229995,2585220036,2843657577,2555067982,3068048275,3083869219,1231071505,1230386524,1230680321,1230539924,1230975843,1239083962,1241065527,1230272984,1247357204,1261530424,1260546874,1277183788,1282172459,1260305347,1277616284,1305743950,1305332196,1320779999,1335260783,1335598661,1349367380,1354714819,1356581838,1354629493,1373663659,1367939983,1377346304,1384844042,1408433814,1391109786,1427551950,1421455358,1467078851,1448350353,1458047080,1457061230,1529559770,1387619038,1539436979,1539005624,1575177513,1588458946,1587226587,1587959070,1632040649,1636919294,1640730462,1652555216,1630511618,1645019295,1559242743,1583218948,1701971555,1723387671,1772650961,1775718906,1805182793,1810979800,1865321291,1892656766,1936112920,1935093227,1949401987,1975322284,1992130386,2031915217,2060469349,2115314246,2138871357,2179924680,2193152729,2198708104,2253998989,2288536484,2316854479,2358813423,2373010783,2384496570,2419091279,2438390138,2515400074,2549320779,2551907520,2558320375,2576697492,2661939586,2667678352,2729798683,2775145680,2779162781,2774656937,2789620390,2809018041,2814559348,2823692520,2837056218,2849626008,2867717260,2863659023,2874080696,2874536645,2894806172,2915219167,2915474390,2922331113,2885799692,2943311826,2953632748,2991757766,2995076195,3001337101,3002293484,3012893139,2960680697,3027502742,3041394478,3045007369,3048716721,3047816188,3073511178,3090491918,3090139747,3106365612,3117569348,3140211283,3150037802,3149292315,},[833533164] = new List<ulong>{10122,10123,10124,10141,2350415758,2359689129,2390945038,2382083841,2533136959,2562564212,2710310819,2710914222,2787617098,2784263066,2797703896,2842043026,2974412971,3043912456,1594151070,798455489,797422750,810383121,576569265,813269955,842083350,854002617,854718942,878850459,881249489,851053322,882223700,890915277,892062620,904962497,809975811,928502682,932160919,942917320,942678679,969292267,978112601,1067191615,1102266445,1116497962,1119263507,1135412156,1159589238,1169231428,1192724938,1199632980,1206100969,1209454231,1212838382,1251062707,1261541803,1269932286,1277330503,1204070852,1285113124,1282838099,1306240898,1315566388,1320924107,1342462175,1353721544,1356773002,1362394666,1382429236,1394363785,1443958473,1447962258,1456233168,1466559128,1517816237,1524018833,1535990243,1539262307,1196352289,1547157690,1557427113,1565532295,1575872461,1582501552,1617361106,1630549771,1627583152,1651859603,1673157060,1680678559,1686299197,1727351952,1740339091,1753777701,1766238308,1795321839,1825978701,1840296976,1853539864,1877231477,1882223552,1886917011,1900496901,1911972056,1915836108,1926650593,1936186181,1973015940,2012513964,2012102778,2012098446,2023776102,2017867878,2012101320,2017869441,2031688330,2040458769,2068850952,2023780547,2023770995,2108972947,2106939007,2144339321,2157494407,2165312390,2172084040,2185190213,2212102023,2031113316,2049364586,2049362180,2248888457,2156384652,2039982308,2249242717,2256347841,2289415427,2311523302,2320069344,2323036838,2350377855,2373719711,2385905595,2394221911,2424873646,2425896549,2447526502,2458681561,2446262619,2473566198,2489929747,2498742171,2502887845,2509860100,2517917450,2521490768,2530697138,2538049119,2537853096,2551641004,2550592376,2556823724,2565285602,2569839654,2563085691,2579209697,2581449517,2575565606,2601577757,2600869376,2619003522,2628941989,2643489217,2656528452,2661006668,2666152838,2673407085,2654384040,2703280784,2712557157,2653948638,2730330209,2728385816,2738574378,2745462252,2751378878,2764183607,2774314742,2778291400,2782148695,2720605090,2647784304,2789573903,2795071027,2806049134,2809022710,2810790746,2813482695,2812699140,2818868279,2822417754,2826612382,2828942903,2833878788,2806120802,2840187395,2835067905,2844541788,2847539533,2852512920,2838333280,2862225402,2871440934,2881793914,2886269124,2907475062,2915569498,2922376468,2932341777,2936256876,2946386963,2950047652,2960602941,2960366278,2976139475,2979351354,2988442185,3002155203,3037938069,3067013384,3105840079,3121478607,3137156520,3115118779,},[-180129657] = new List<ulong>{2968107939,2982389320,3020105317,3044903681,787716105,885103417,889212734,930694436,1127078435,1212709764,1316294242,1455068496,1529745641,1644263172,1679876778,1818868472,1865304630,2000024196,2040776062,2051308391,2138650218,2146145661,2186894702,2234050484,2255658925,2268670063,2258758426,2329411820,2384642529,2394650621,2544654811,2613348603,2629994931,2637294616,2661173963,2656964966,2707065696,2715538585,2730920626,2738277799,2781761204,2799725602,2874599508,2897346639,2911301119,2915497189,2922040340,2925747081,2949654512,2953166749,2957389091,2943080198,2978714978,3013207648,3031087967,3048560827,3066926349,3090558608,3107710831,3149628100,3154820721,3159432079,},[198438816] = new List<ulong>{861548229,860916780,862291005,861029759,862137836,869474635,871927557,1161727529,1335113551,1373851317,1780228760,1927092059,2018506223,2146714479,2186443994,2311659818,2676512603,2901234142,2995199862,},[-1780802565] = new List<ulong>{820810719,844666224,843454856,911446362,1121804393,1124734833,933760454,1276627079,} };
        }

        private class Permissions
        {
            [JsonProperty(PropertyName = "Need permission for Team Hud?(true - will work only for players with permission / false - work for all players)")]
            public bool HudNeedPermission;

            [JsonProperty(PropertyName = "Need permission for Team Voice?(true - will work only for players with permission / false - work for all players)")]
            public bool TeamVoiceNeedPermission;

            [JsonProperty(PropertyName = "Need permission for Team Skins?(Need at least one player with this permission in team to set skins in menu)(true - will work only for players with permission / false - work for all players)")]
            public bool TeamSkinsNeedPermission;

            [JsonProperty(PropertyName = "Need permission for Team marker?(true - will work only for players with permission / false - work for all players)")]
            public bool TeamMarkerNeedPermission;
            
            [JsonProperty(PropertyName = "Team hud using permission")]
            public string HudUsePermission;
 
            [JsonProperty(PropertyName = "Team Voice using permission")]
            public string TeamVoicePermission;

            [JsonProperty(PropertyName = "Team Skins using permission(only for setting skins. it's mean that only team leader will need this permission)")]
            public string TeamSkinsPermission;

            [JsonProperty(PropertyName = "Team marker using permission")]
            public string TeamMarkerPermission;
        }

        private class PluginOptions
        {
            [JsonProperty(PropertyName = "Enable auto teamleader switch(If current teamleader go offline, next player from online automaticcaly set to teamleader. Teamleader get his leader role after join online)")]
            public bool AutoTeamLeaderSwtich;
            
            [JsonProperty(PropertyName = "Enable Team Fix(Will fix teams if your server is crashed)")]
            public bool EnableTeamFix;

            [JsonProperty(PropertyName = "Enable clan tag removing from Team Hud(Work only if has Clans plugin)")]
            public bool EnableClanTagAutoRemove;
            
            [JsonProperty(PropertyName = "Enable Team Hud")]
            public bool EnableTeamHud;

            [JsonProperty(PropertyName = "Enable global team voice chat")]
            public bool EnableTeamVoice;

            [JsonProperty(PropertyName = "Enable team skins")]
            public bool EnableTeamSkins;

            [JsonProperty(PropertyName = "Enable players grid position")]
            public bool EnableGridPosition;
            
            [JsonProperty(PropertyName = "Enable easy team markers")]
            public bool EnableTeamMarker;

            [JsonProperty(PropertyName = "Enable team auto authorization")]
            public AuthorizationSettings AutoAuthorization;

            [JsonProperty(PropertyName = "Easy team markers")]
            public TeamMarkers TeamMarkers;
        }

        private class TeamMarkers
        {
            [JsonProperty("Max distance")]
            public float MaxDistance;

            [JsonProperty("Search radius")]
            public float SearchRadius;

            [JsonProperty("Duration [seconds]")]
            public int Duration;

            [JsonProperty("Cooldown [seconds]")]
            public int Cooldown;
        }

        private class AuthorizationSettings
        {
            [JsonProperty("TC Authorization")]
            public bool EnableTC = true;

            [JsonProperty("Codelocks authorization")]
            public bool EnableCodelocks = true;

            [JsonProperty("AutoTurrets authorization")]
            public bool EnableAutoTurrets = true;

            [JsonProperty("SAMSite authorization")]
            public bool EnableSS = true;
        }

        private class UISettings
        {
            [JsonProperty(PropertyName = "UI Scale")]
            public float Scale;

            [JsonProperty(PropertyName = "Left Offset")]
            public int LeftOffset;

            [JsonProperty(PropertyName = "Top Offset")]
            public int TopOffset;

            [JsonProperty(PropertyName = "Player grid refresh rate")]
            public int SquareUpdateRate;

            [JsonProperty(PropertyName = "Lines margin")]
            public int LinesMargin = 5;

            [JsonProperty(PropertyName = "Collums margin")]
            public int CollumsMargin = 5;

            [JsonProperty(PropertyName = "Max amount of player displays in line")]
            public int Lines;
        }

        private class Data
        {
            public List<TeamSettings> Teams = new List<TeamSettings>();
            public List<PlayerSettings> Players = new List<PlayerSettings>();
            public Dictionary<ulong, TeamFixData> TeamFix = new Dictionary<ulong, TeamFixData>();
            public string WipeID = string.Empty;
        }

        private class TeamFixData
        {
            public ulong TeamLeader;
            public List<ulong> Members = new List<ulong>();
        }

        private class PlayerSettings
        {
            public ulong ID;
            public string Name;

            public bool EnableTeamVoice;
            public bool EnableCustomMarker;
            public bool EnableTeamSkins;
            public bool EnableTeamGridPoisition;

            public PlayerSettings(ulong id, string name, bool enableCustomMarker, bool enableTeamSkins)
            {
                ID = id;
                Name = name;
                EnableTeamVoice = false;
                EnableTeamGridPoisition = true;
                EnableTeamSkins = enableTeamSkins;
                EnableCustomMarker = enableCustomMarker;
            }

            public void UpdateName(string name) => Name = name;
        }

        private class TeamSettings
        {
            public ulong ID;
            public ulong TeamLeader;
            public Dictionary<ulong, AuthorizationSettings> AuthorizationSettings;
            public Dictionary<int, ulong> Skins;
            

            public TeamSettings(ulong id, Dictionary<ulong, AuthorizationSettings> authorizationSettings)
            {
                ID = id;
                AuthorizationSettings = authorizationSettings;
                Skins = new Dictionary<int, ulong>();
            }
        }

        #endregion

        #region Config & Data

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();


        private void LoadData() =>
            _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data")
                ? Interface.Oxide.DataFileSystem.ReadObject<Data>($"{Name}/data")
                : new Data();

        private void OnServerSave() => SaveData();

        private void SaveData()
        {
            if (_data != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
        }

        #endregion

        #region Language

        private void SendMessage(BasePlayer player, string msg, params object[] args) => Player.Message(player, GetMsg(player.UserIDString, msg, args), 0);

        private string GetMsg(string player, string msg, params object[] args) => string.Format(lang.GetMessage(msg, this, player), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_CHANGE"] = "Change",
                ["UI_CHOOSE"] = "Select",
                ["UI_BETTERTEAMS"] = "BETTER TEAMS",
                ["UI_BETTERTEAMSDESCRIPTION"] = "This modification will allow you to get a better experience playing with friends. Some things have been changed, and new ones have been added, so that you get the better experience playing with your friends!",
                ["UI_CHANGES"] = "CHANGES:",
                ["UI_CHANGEHUD"] = "• Team Hud - allows you to see status of your mates",
                ["UI_CHANGETeamVoice"] = "• Team Voice - allows you to use in-game voice chat to communicate only with your mates(works at any distance). Other players will not hear you. Can be enable in settings",
                ["UI_CHANGESkins"] = "• Team Skins - allows you to choose skins for your team. Items you pick up will automatically receive your team's skin",
                ["UI_CHANGEEasyTeamMabrkers"] = "• Easy Team Marker - allows you to use fast marker to your team without using binoculars",
                ["UI_CHANGEAutoAuthTC"] = "• AutoAuthorization TC - adds automatic authorization of allies in TC",
                ["UI_CHANGEAutoAuthCodeLocks"] = "• AutoAuthorization CodeLocks - adds automatic authorization of allies in Code Locks",
                ["UI_CHANGEAutoAuthTurrets"] = "• AutoAuthorization Auto Turrets - adds automatic authorization of allies in Auto Turrets",
                ["UI_CHANGESamSiteAuthorization"] = "• AutoAuthorization SamSite - adds automatic authorization of allies in Sam Sites",
                ["UI_CHANGEAuthorizationManager"] = "• Team Managment - allows the team leader to manage auto authorization for allies.",
                ["UI_SETTINGSNAME"] = "PERSONAL SETTINGS",
                ["UI_S_ENABLE_TEAM_VOICE"] = "USE TEAM VOICE CHAT ",
                ["UI_S_ENABLE_TEAM_VOICE_DES"] = "Yes - Replace vanilla voice to to team chat. Only your allies will hear you. Works at any distance (on the entire map)",
                ["UI_S_ENABLE_TEAM_SKINS"] = "USE TEAM SKINS",
                ["UI_S_ENABLE_TEAM_GRID"] = "SHOW MATES GRID POSITION",
                ["UI_S_ENABLE_TEAM_SKIN_DES"] = "Yes - all items you pick up will automatically receive your team's skin.",
                ["UI_S_ENABLE_TEAM_GRID_DES"] = "Yes - will show mates grid position in team hud.",
                ["UI_S_ENABLE_MARKERS"] = "USE EASY TEAM MARKERS",
                ["UI_S_ENABLE_MARKERS_DES"] = "Yes - will give you the ability to place quick time markers in the world at the location you are looking at. Markers are placed using a key combination and do not require binoculars.",
                ["UI_S_MARKER_BUTTON"] = "EASY TEAM MARKER BIND",
                ["UI_S_MARKER_BUTTON_DES"] = "Bind your easy team marker command. Just copy command, replace BUTTON to the button you need and enter in console!\nExample: bind BUTTON ftmark -> binb v ftmark",
                ["UI_S_YES"] = "YES",
                ["UI_S_NO"] = "NO",
                ["UI_M_MEMBERS"] = "TEAM MANAGMENT",
                ["UI_M_ONLY_LEADER"] = "Available only to the team leader",
                ["UI_M_AA_H1"] = "AUTO AUTHORIZATION",
                ["UI_M_AA_INFO_DES"] = "All authorization places (tcs, codelocks, auto turrets, etc) add up to a single authorization list for the entire team. If you block any ally from accessing the authorization list, he will lose access to all authorization places, except for those that he installed personally (set codelock or tc himself), while your team will not lose access to his authorization places. Good if you accept new random player in your team.",
                ["UI_M_SELECT_MATE"] = "Select one of your allies",
                ["UI_M_AA_TC"] = "Tool Cupboard Auto Authorization",
                ["UI_M_AA_CL"] = "CodeLocks Auto Authorization",
                ["UI_M_AA_AT"] = "Auto Turrets Auto Authorization",
                ["UI_M_AA_SAMSITE"] = "Sam Sites Auto Authorization",
                ["UI_M_KICK"] = "Kick from team",
                ["UI_M_KICK_B"] = "KICK",
                ["UI_S_TEAM_SKINS"] = "TEAM SKINS",
                ["CM_MARKER_COOLDOWN"] = "Your team ping is on cooldown: {0} seconds left",
                ["UI_H_INFO_TEXT"] = "OPEN TEAM MENU",
                ["UI_S_PERM"] = "Your team must have at least 1 player with privilege that have access to skins",
                ["CM_DONT_HAVE_PERM"] = "You do not have permission to use this command",
                ["UI_IS_DISABLED"] = "This function is disabled by admin",
                ["CM_NO_TEAM"] = "You don't have team. If you want use better teams menu, you must create team or join in team."

            }, this);
        }

        #endregion
    }
}
