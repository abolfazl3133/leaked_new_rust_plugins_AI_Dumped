using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BetterTeams", "ahigao", "1.0.5")]
    internal class BetterTeams : RustPlugin
    {
        #region Static

        private Data _data;
        private float Scale;
        private Configuration _config;
        private static BetterTeams _ins;
        private const string Layer = "UI_BetterTeams";

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
        private Plugin ImageLibrary;

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
            Interface.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(BetterChat_FormattedClanTag));

            if (SaveRestore.WipeId != _data.WipeID)
            {
                _data.WipeID = SaveRestore.WipeId;
                _data.Teams.Clear();
                SaveData();
            }
            
            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }
        
        private string BetterChat_FormattedClanTag(IPlayer player)
        {
            var playerTeam = FindTeamByUserID(ulong.Parse(player.Id));
            if (playerTeam == null)
                return string.Empty;

            var team = GetTeamData(playerTeam.teamID);
            if (team == null || string.IsNullOrEmpty(team.TeamName))
                return string.Empty;

            return $"<color=#55aaff>[{team.TeamName}]</color>";
        }

        private void Unload()
        {
            foreach (var check in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(check, Layer + ".bg");
                CuiHelper.DestroyUi(check, Layer + ".TeamHud");
                CuiHelper.DestroyUi(check, Layer + ".TeamCreateMenu");
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
                    team.Remove(player);
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
                if (player == null)
                    continue;

                if (!authList.Contains(player.userID))
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
                PrintWarning("ADDED");
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

        private object OnTeamCreate(BasePlayer player)
        {
            if (player == null)
                return null;
            
            ShowUITeamCreation(player);
            return false;
        }

        private void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader)
        {
            NextTick(() =>
            {
                if (newLeader == null || team == null || !_config.Options.EnableTeamHud)
                    return;

                foreach (var check in GetOnlineTeam(team.teamID))
                    ShowHudParentUI(check);
            });
        }

        private object OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (player == null || team == null)
                return null;

            if (team.members.Count >= _config.TeamLimit)
                return false;
            
            NextTick(() =>
            {
                if (player == null || team == null)
                    return;

                var onlineTeam = GetOnlineTeam(team.teamID);
                if (!onlineTeam.Contains(player))
                    onlineTeam.Add(player);

                var teamData = GetTeamData(team.teamID);
                teamData.AuthorizationSettings.TryAdd(player.userID, new AuthorizationSettings());
                AuthorizeTo(team, player.userID, "all", true);

                foreach (var check in team.members)
                {
                    var aaSettings = teamData.AuthorizationSettings[check];

                    if (aaSettings.EnableCodelocks)
                        AuthorizeTo(player.userID, check, "cl", true);

                    if (aaSettings.EnableTC)
                        AuthorizeTo(player.userID, check, "tc", true);

                    if (aaSettings.EnableAutoTurrets)
                        AuthorizeTo(player.userID, check, "at", true);

                    if (aaSettings.EnableSS)
                        AuthorizeTo(player.userID, check, "ss", true);
                }

                if (!_config.Options.EnableTeamHud)
                    return;

                foreach (var check in onlineTeam)
                    ShowHudParentUI(check);
            });
            
            return null;
        }
        
        private object OnTeamInvite(BasePlayer player, BasePlayer target)
        {
            if (player == null || target == null || player.Team == null)
                return null;

            if (player.Team.members.Count >= _config.TeamLimit)
                return false;

            return null;
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null || player == null)
                return;

            var onlineTeam = GetOnlineTeam(team.teamID);
            if (onlineTeam.Contains(player))
                onlineTeam.Remove(player);

            CuiHelper.DestroyUi(player, Layer + ".TeamHud");
            var teamData = GetTeamData(team.teamID);
            teamData.AuthorizationSettings.Remove(player.userID);

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

            foreach (var check in team.members)
                AuthorizeTo(team, check, "all", false);

            foreach (var check in GetOnlineTeam(team.teamID))
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

        #region FriendlyFire

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null)
                return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null || !player.userID.IsSteamId() || !attacker.userID.IsSteamId() || attacker == player || player.Team == null || !player.Team.members.Contains(attacker.userID))
                return;

            var playerData = GetPlayerData(player);
            var attackerData = GetPlayerData(attacker);
            
            if (playerData.EnableFriendlyFire || attackerData.EnableFriendlyFire)
                return;
            
            info.damageTypes?.ScaleAll(0);
        }

        #endregion

        #endregion

        #region Functions

        private string GetNameWithoutTeamName(BasePlayer player)
        {
            if (player.Team == null)
                return player.displayName;

            var teamData = GetTeamData(player.Team.teamID);
            if (!string.IsNullOrEmpty(teamData.TeamName))
                return player.displayName.Replace($"[{teamData.TeamName}] ", "");

            return GetNameWithoutTeamName(player.userID);
        }

        private string GetNameWithoutTeamName(ulong userID)
        {
            var name = GetPlayerData(userID).Name;
            var team = FindTeamByUserID(userID);
            if (team == null)
                return name;

            var teamData = GetTeamData(team.teamID);
            if (string.IsNullOrEmpty(teamData.TeamName))
                return name;
            
            return name.Replace($"[{teamData.TeamName}] ", "");
        }

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
            var note = new MapNote();
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
            timer.Every(_config.HudSettings.SquareUpdateRate, () =>
            {
                foreach (var check in OnlineTeams)
                {
                    var container = new CuiElementContainer();
                    foreach (var player in check.Value)
                        FillGridInfo(container, player);

                    foreach (var player in check.Value)
                    {
                        if (_config.Permissions.HudNeedPermission && !permission.UserHasPermission(player.UserIDString, _config.Permissions.HudUsePermission))
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

        private TeamSettings GetTeamData(ulong id, string name = "")
        {
            foreach (var check in _data.Teams)
                if (check.ID == id)
                    return check;
            
            var data = new TeamSettings(id, name, new Dictionary<ulong, AuthorizationSettings>());
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

        [ChatCommand("cff")]
        private void ChatCommandcff(BasePlayer player, string command, string[] args)
        {
            var data = GetPlayerData(player);
            data.EnableFriendlyFire = !data.EnableFriendlyFire;
            Player.Message(player, data.EnableFriendlyFire ? "<color=#cdc2b2>Огонь по союзникам:</color> <color=green>Включен</color>" : "<color=#cdc2b2>Огонь по союзникам:</color> <color=red>Выключен</color>", 76561198297741077);
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
                            ShowUISkins(player, arg.GetInt(3));
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

                Facepunch.Pool.Free(ref foundEntities);
                return;
            }

            foreach (var check in foundEntities)
            {
                if (!(check is OreResourceEntity))
                    continue;

                var position = check.transform.position;
                PlayerPing(player, position.WithY(position.y + check.bounds.extents.y * 2), 10, 4);

                Facepunch.Pool.Free(ref foundEntities);
                return;
            }

            foreach (var check in foundEntities)
            {
                if (!(check is StorageContainer))
                    continue;

                var position = check.transform.position;
                PlayerPing(player, position.WithY(position.y + check.bounds.extents.y * 2), 11, 0);

                Facepunch.Pool.Free(ref foundEntities);
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

                Facepunch.Pool.Free(ref foundEntities);
                return;
            }

            PlayerPing(player, info.point, 0, 2);

            Facepunch.Pool.Free(ref foundEntities);
        }

        [ConsoleCommand("UI_BTC")]
        private void ConsoleCommandUI_BTC(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
                return;
            var player = arg.Player();
            switch (arg.GetString(0))
            {
                case "name":
                    ShowUITeamCreation(player, arg.GetString(1));
                    break;
                case "create":
                    var team = _data.Teams.FirstOrDefault(x => x.TeamName == arg.GetString(1));
                    if (team != null)
                    {
                        Player.Message(player, $"Team with name {arg.GetString(1)} is already exists.", 76561198297741077);
                        return;
                    }

                    if (arg.GetString(1).Length < 3)
                    {
                        Player.Message(player, "Minimum team name length is 3.", 76561198297741077);
                        return;
                    }

                    Player.Message(player, "Team successfully created!", 76561198297741077);
                    CuiHelper.DestroyUi(player, Layer + ".TeamCreateMenu");
                    
                    var playerTeam = RelationshipManager.ServerInstance.CreateTeam();
                    playerTeam.teamLeader = player.userID;
                    playerTeam.AddPlayer(player);
                    
                    OnlineTeams.Add(player.Team.teamID, new List<BasePlayer> { player });
                    var teamData = GetTeamData(playerTeam.teamID, arg.GetString(1));

                    teamData.AuthorizationSettings.TryAdd(player.userID, new AuthorizationSettings());

                    foreach (var check in _config.Skins)
                        teamData.Skins.TryAdd(check.Key, 0);

                    if (!_config.Options.EnableTeamHud)
                        return;

                    ShowHudParentUI(player);
                    break;
            }
        }   

        #endregion

        #region UI

        #region TeamCreationMenu
        
        private void ShowUITeamCreation(BasePlayer player, string name = "")
        {
            if (player == null)
                return;

            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Overlay",
                DestroyUi = Layer + ".TeamCreateMenu",
                Name = Layer + ".TeamCreateMenu",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.20 0.18 0.17 1.00",
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-190 460", OffsetMax = "180 610"
                    },
                    new CuiNeedsKeyboardComponent(),
                    new CuiNeedsCursorComponent()
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamCreateMenu",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.15 0.13 0.11 1.00",
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 0", OffsetMax = "370 100"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamCreateMenu",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "ВВЕДИТЕ НАЗВАНИЕ ВАШЕЙ КОМАНДЫ",
                        Color = "0.82 0.78 0.75 1.00",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 100", OffsetMax = "370 150"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamCreateMenu",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.9686 0.9216 0.8824 0.1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-100 55", OffsetMax = "100 90"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamCreateMenu",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Command = "UI_BTC name ",
                        Text = name,
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        CharsLimit = 8
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-100 55", OffsetMax = "100 90"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "5 5", OffsetMax = "185 45"
                },
                Button =
                {
                    Color = "0.35 0.45 0.25 1.00",
                    Command = $"UI_BTC create {name}"
                },
                Text =
                {
                    Text = "ПОДТВЕРДИТЬ",
                    Color = "0.76 0.96 0.42 1.00",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter
                }
            }, Layer + ".TeamCreateMenu");
            
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "185 5", OffsetMax = "365 45"
                },
                Button =
                {
                    Color = "0.45 0.24 0.19 1.00",
                    Close = Layer + ".TeamCreateMenu" 
                },
                Text =
                {
                    Text = "ЗАКРЫТЬ",
                    Color = "1.00 0.53 0.32 1.00",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter
                }
            }, Layer + ".TeamCreateMenu");
            
            CuiHelper.AddUi(player, container);
        }

        #endregion

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

            var y = -50;
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
                            OffsetMin = "40 0", OffsetMax = "520 400"
                        }
                    }
                });

                AddCategories(container, "Mates");
                CuiHelper.AddUi(player, container);
                return;
            }

            foreach (var check in team.members.Take(8))
            {
                var data = GetPlayerData(check);

                container.Add(new CuiElement
                {
                    Parent = Layer,
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

                container.Add(new CuiElement
                {
                    Parent = Layer + ".member" + check,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"<size=13><color=#F7ECE2>{data.Name}</color></size>\n{check}",
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
                    }, Layer);

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
                        Text = "Back",
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
                        Text = "Next",
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
                FillPlayerUI(container, y, x, check, check == player.Team.teamLeader);
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

        private void FillPlayerUI(CuiElementContainer container, int y, int x, ulong id, bool leader)
        {
            var player = FindByID(id);
            var currentLayer = Layer + ".TeamHud" + id;
            var data = player == null ? GetPlayerData(id) : null;

            if (player == null && data == null)
                return;

            var name = player == null ? data.Name : player.displayName;

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
                FillGridInfo(container, player);

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
            FillGridInfo(container, player);
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

        private void FillGridInfo(CuiElementContainer container, BasePlayer player)
        {
            container.Add(new CuiElement
            {
                Parent = Layer + ".TeamHud" + player.UserIDString,
                Name = Layer + ".TeamHud" + player.UserIDString + ".Grid",
                DestroyUi = Layer + ".TeamHud" + player.UserIDString + ".Grid",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = PositionToGridCoord(player.transform.position),
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

            [JsonProperty(PropertyName = "Team limit")]
            public int TeamLimit = 3;

            [JsonProperty(PropertyName = "Enabled functions")]
            public PluginOptions Options = new PluginOptions
            {
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
            public Dictionary<int, List<ulong>> Skins = new Dictionary<int, List<ulong>>
            {
                [1545779598] = new List<ulong>
                {
                    0,
                    849047662,
                    887494035,
                    1359893925,
                    1202410378,
                    1372945520,
                    859845460,
                    1259716979,
                    1826520371,
                    1750654242,
                    2249370680,
                    809212871,
                    809190373,
                    1435827815,
                    1112904406,
                    2128372674,
                    1385673487,
                    1679665505,
                    2245084157,
                    2041629387,
                    2101560738,
                    924020531,
                    1349512142,
                    2248879512,
                    937864743,
                    1196676446,
                    875130056,
                    1915393587,
                    1174389582,
                    1583542371,
                    1102750231,
                    840477492,
                    1306351416,
                    885146172,
                    1137915903,
                    1245563496,
                    1349324364,
                    1929819581,
                    1092674304,
                    10135,
                    2017002938,
                    920472848,
                    2075372682,
                    1804885723,
                    928950425,
                    1448221547,
                    2319710097,
                    939180121,
                    940035827,
                    903882218,
                    1476966011,
                    1588206436,
                    1167207039,
                    889710179,
                    1362212220,
                    1265322092,
                    1364985829,
                    1993279809,
                    1599157678,
                    1575397458,
                    1886272847,
                    925720043,
                    1272989639,
                    1434027951,
                    908297014,
                    1213092632,
                    1428980348,
                    1983066869,
                    1457951707,
                    10137,
                    618543834,
                    2085083014,
                    1338305091,
                    2179386526,
                    1118706219,
                    1539318940,
                    1760078043,
                    1161844853,
                    1522034435,
                    1120500163,
                    1685375084,
                    1549426268,
                    2304993742,
                    1907342157,
                    2172715245,
                    1870705926,
                    911726740,
                    2012334520,
                    2059352465,
                    2108685486,
                    1252554814,
                    895307805,
                    2268418002,
                    1352844155,
                    1746886322,
                    1288866247,
                    654502185,
                    934891737,
                    1175238674,
                    1539409946,
                    1372566409,
                    1277707212,
                    1396630285,
                    1076214414,
                    1882821847,
                    1850236015,
                    2109182691,
                    1230963555,
                    1539007965,
                    10138,
                    1088459232,
                    1129886223,
                    1124932043,
                    1467269924,
                    2006878490,
                    1659781652,
                    1402320927,
                    2323019876,
                    2242791041,
                    1324932956,
                    1140366289,
                    2240088463,
                    1159593268,
                    2352506845,
                    1309470544
                },
                [-1812555177] = new List<ulong>
                {
                    0,
                    1621894466,
                    2100059186,
                    1741459108,
                    2151920583,
                    1535995784,
                    1239079767,
                    1569062511,
                    1173523145,
                    1553359638,
                    1812045814,
                    1174458060,
                    1604879931,
                    1225499752,
                    2304318648,
                    1725240606,
                    1883559335,
                    1419392688,
                    1700177871,
                    1612152593,
                    1578816958,
                    1660691287,
                    1173459827,
                    1644715625,
                    1177273104,
                    1264358358,
                    2092965663,
                    1953108368,
                    2122513705,
                    2146248175,
                    2229046238,
                    1671985039,
                    1652362426,
                    1308037543,
                    1779949198,
                    1635559091,
                    1787012455,
                    1176479716,
                    2319796265,
                    2058268475,
                    1481478360,
                    1967805281,
                    2195318269,
                    2016313108,
                    1282171260,
                    1203322875,
                    1906355162
                },
                [-2069578888] = new List<ulong>
                {
                    0,
                    1719536313,
                    1740639585,
                    1707973294,
                    1992981006,
                    1831294069,
                    1712378771
                },
                [1588298435] = new List<ulong>
                {
                    0,
                    1852284996,
                    818403150,
                    1795984246,
                    1535660827,
                    1517933342,
                    1581664321,
                    897023403,
                    10117,
                    10115,
                    933509449,
                    1687042408,
                    1587273896,
                    875259050,
                    943036098,
                    2024514125,
                    840105253,
                    819149392,
                    972020573,
                    1592946955,
                    947954942,
                    1119629516,
                    1161165984,
                    10116,
                },
                [28201841] = new List<ulong>
                {
                    0,
                    1708343082,
                    1708365495,
                    1736532811,
                    1720530850,
                    1707880195
                },
                [1318558775] = new List<ulong>
                {
                    0,
                    1137434899,
                    1603970802,
                    2201971147,
                    2172493867,
                    1693898215,
                    904404578,
                    796679172,
                    833639834,
                    1637174724,
                    796687275,
                    1865099372,
                    911612956,
                    1673754411,
                    808554348,
                    892100306,
                    853438134,
                    800974015,
                    914624163,
                    1654499223,
                    1413917236,
                    1084800708,
                    1087199678
                },
                [442886268] = new List<ulong>
                {
                    0,
                    879708939,
                    813795591,
                    894679426,
                    1936188783,
                    1926503780,
                    1162978825,
                    1657103887,
                    1815384807,
                    875930670,
                    853494512,
                    2268211267,
                    1839729563,
                    926279011,
                    812737524,
                    1905848285,
                    1162085821,
                    1137393412
                },
                [-904863145] = new List<ulong>
                {
                    0,
                    2222645873,
                    1772028068,
                    828616457,
                    1195821858,
                    2193203225,
                    839302795,
                    2131324289,
                    1168002579,
                    1359059068,
                    1667097058,
                    1385736095,
                    1818125194,
                    1616628843,
                    1652791742,
                    1819195444,
                    2171565192,
                    1517644801,
                    2296659119,
                    900921542,
                    922119054,
                    1291766032,
                    1395475969,
                    1936035303,
                    959955205,
                    1448088345,
                    1099992405,
                    840023839,
                    1129722099,
                    1113987623,
                    1217394290,
                    1863834018,
                    1966875478,
                    942919370,
                    1621472496,
                    1429032578,
                    2108652282,
                    1098038362,
                    1788152335,
                    1576671137,
                    2123641710,
                    1310522106,
                    1135415770,
                    1876226129,
                    1522185915,
                    2249445756,
                    1446861251,
                    1182015913,
                    2252306404,
                    1170719113,
                    1240340535,
                    1566048269,
                    1298949573,
                    1933250593,
                    1193105339,
                    1300239738,
                    875259741,
                    1112906123,
                    1814170373,
                    1313600286,
                    899564368,
                    2076615773,
                    1279791283,
                    1300137383,
                    818613234
                },
                [-1758372725] = new List<ulong>
                {
                    0,
                    1772377862,
                    1720001936,
                    1689944021,
                    1345464512,
                    561462394,
                    839819171
                },
                [1796682209] = new List<ulong>
                {
                    0,
                    1329096680,
                    820350952,
                    820402694,
                    866745136,
                    1081305198,
                    2186437441,
                    1128840196,
                    931547202,
                    1185311263,
                    816728172,
                    1839296742,
                    1805101270,
                    897099822,
                    892212957,
                    904964438,
                    1597038037,
                    2172135020,
                    1446184061,
                    1961720552,
                    1685722307,
                    1198145190,
                    1753609137,
                    1987573278,
                    1114032911,
                    1107572641,
                    854914986,
                    1523699528,
                    2281845451,
                    822943156,
                    970682025
                },
                [1373971859] = new List<ulong>
                {
                    0,
                    1839518047,
                    1752928667,
                    1631920728,
                    1457537882,
                    1216163401,
                    2200875381,
                    1421351634,
                    1624620555,
                    1305704747,
                    1228154190,
                    1215390635,
                    1563667918,
                    1455062983,
                    1864788236,
                    1217395562,
                    1914959779,
                    1373936691,
                    1406640269,
                    1265214612,
                    1296687971,
                    1342464644,
                    1335582211,
                    1917523660,
                    1435364672,
                    1418647979,
                    1290876678,
                    1445908611,
                    1812135451,
                    1277518447,
                    1605379554,
                    1364964220,
                    1461918011,
                    1214609010,
                    1435858834,
                    1328632407,
                    1269667577,
                    1796388607,
                    1362224646,
                    1672707813,
                    1356665596,
                    1377347733,
                    2100486270,
                    1276136280,
                    1529514494,
                    2025046773,
                    1223105431,
                    2059988260,
                    1235690536,
                    1258109891
                },
                [818877484] = new List<ulong>
                {
                    0,
                    902487409,
                    1720501333,
                    919535259,
                    883156158,
                    924018875,
                    937863988,
                    876007573,
                    1428766159,
                    10087,
                    515313072,
                    1183693235,
                    1158943049,
                    863443112,
                    953126981,
                    910681058,
                    1811814491,
                    1105853708,
                    1167255900,
                    904356033,
                    893118140,
                    2041571172,
                    1121906926,
                    805925675,
                    10108,
                    539539196,
                    1630961792,
                    954520976,
                    1113544521,
                    830255284,
                    899942580,
                    853927198,
                    1571099329,
                    950037016,
                    972059802,
                    830606037,
                    804337360,
                    1328323548,
                    798375117,
                    868298519,
                    10081,
                    531196714,
                    10073,
                    529604373,
                    1446715780,
                    908722214,
                    827533196,
                    975102848,
                    938007886,
                    1118707296,
                    936623315,
                    829983759,
                    1553005167,
                    1092676141,
                    935205778
                },
                [649912614] = new List<ulong>
                {
                    0,
                    910665268,
                    1901240954,
                    2049887645,
                    1192708586,
                    855087726,
                    1235107237,
                    1265936882,
                    1141054826,
                    815532676,
                    1517889157,
                    937338314,
                    970737094,
                    1536482422,
                    873242795,
                    2000389791,
                    809865395,
                    809822151,
                    933056389,
                    1448503557,
                    1349358875,
                    1580859157,
                    10114,
                    522224500,
                    809897611,
                    1346870567,
                    911828654,
                    1235996786,
                    2340195521,
                    1428863076,
                    950956238,
                    2312068016,
                    1787802800,
                    1435664860,
                    1161550991,
                    1309517474,
                    1408242038,
                    1447877728,
                    1099177489,
                    973871108,
                    1217455695,
                    887846629
                },
                [-765183617] = new List<ulong>
                {
                    0,
                    916790605,
                    865019380,
                    1448142776,
                    1660175523,
                    2059815527,
                    1119662164,
                    2118688615,
                    1680595474,
                    1213074188,
                    1378519774,
                    948113632,
                    1818232860,
                    854987948,
                    1260964187,
                    1522902588,
                    860153737,
                    1414878365,
                    1295701369,
                    2107885378,
                    1408050439,
                    1174675399,
                    858957151,
                    1910558629,
                    1465627520,
                    1341524782,
                    1127266590,
                    1441939951,
                    1870693079,
                    1282137884,
                    1590495543,
                    1229950256,
                    1277558450,
                    2249169000,
                    1225880743,
                    1247696065,
                    1616108563,
                    1569952704
                },
                [-194953424] = new List<ulong>
                {
                    0,
                    1805321162,
                    901668040,
                    832021670,
                    792649975,
                    1787201365,
                    2229280260,
                    1581351961,
                    784316334,
                    2105454370,
                    799044333,
                    821441043,
                    2304448562,
                    1969741836,
                    835026584,
                    1121237616,
                    831923884,
                    1711049678,
                    939788004,
                    1270065112,
                    2092488951,
                    1753711761,
                    792079554,
                    962694769,
                    1092671728,
                    2005707226,
                    2226597543,
                    1335769610,
                    1993775723,
                    1084823878,
                    1886184322,
                    1934943101,
                    915572534,
                    1349988105,
                    2012095496,
                    2252919011,
                    807821375,
                    1771714129,
                    1638135398,
                    1172409741,
                    794837198,
                    932233099,
                    1886920683,
                    812933296,
                    881687672,
                    1575391468,
                    806983252,
                    899001394,
                    2215908400,
                    1313529548,
                    895067146,
                    828888629,
                    1894591519,
                    841012325,
                    1083628316,
                    924019464,
                    2296503845,
                    900645045,
                    1586135331,
                    2242198470,
                    1777973800,
                    2193149013,
                    943128194,
                    1693643930,
                    2100738972,
                    1388857962,
                    803894027,
                    1900843064,
                    2001712636,
                    2131549928,
                    903628875,
                    792905158,
                    792827436,
                    1353724450,
                    1587225942,
                    1137533438,
                    896211631,
                    907176719,
                    971433920,
                    824898622,
                    816530945,
                    915693648,
                    1845950558,
                    1130407273,
                    800980236,
                    1203888714,
                    2143679757,
                    1932619423,
                    2178447488,
                    1547874663,
                    2059935666,
                    1680454451,
                    1522955755,
                    1135160079,
                    1113983678,
                    1421829383,
                    832934294
                },
                [1110385766] = new List<ulong>
                {
                    0,
                    1805322456,
                    1787198707,
                    2229279338,
                    1581353262,
                    2105505757,
                    2304445825,
                    1969743263,
                    1711048020,
                    832233112,
                    842577956,
                    1270065959,
                    2092488087,
                    1753699785,
                    2005709642,
                    2226598382,
                    1335770974,
                    1993774875,
                    924019814,
                    1886179838,
                    1934946028,
                    796728308,
                    1349989767,
                    794169855,
                    2013723517,
                    2252998412,
                    1771804406,
                    1638812721,
                    823132085,
                    944835223,
                    1886922099,
                    1575392727,
                    2215911873,
                    895066686,
                    828888000,
                    817022417,
                    1094522474,
                    2296501936,
                    2352188221,
                    1586132318,
                    2242200040,
                    1777967326,
                    819160334,
                    2193157606,
                    797410767,
                    1693641239,
                    2100740608,
                    1388861988,
                    798736321,
                    1900842357,
                    799657859,
                    2001706617,
                    2131557341,
                    934627208,
                    900807753,
                    1353723648,
                    1587225313,
                    970700662,
                    1845931269,
                    2144720791,
                    1932615190,
                    2178449205,
                    1578628782,
                    2059931054,
                    1680452167,
                    1522963149,
                    1135168998,
                    1113984430,
                    1421841596
                },
                [1751045826] = new List<ulong>
                {
                    0,
                    1660290744,
                    1787216403,
                    1100931254,
                    897890977,
                    2351687115,
                    1961464025,
                    1408832378,
                    2282178792,
                    10142,
                    626133126,
                    2192919905,
                    1997532879,
                    14179,
                    2080975449,
                    10052,
                    492800372,
                    14178,
                    2295666190,
                    2293185782,
                    1373714814,
                    1968538819,
                    1432964453,
                    2000507925,
                    1552703337,
                    1209453497,
                    1564974974,
                    1911980598,
                    2289427434,
                    1371314541,
                    1305321596,
                    1587744366,
                    1150818496,
                    2099705103,
                    2067569919,
                    1766644324,
                    10133,
                    619935833,
                    904963081,
                    889718910,
                    1950853975,
                    2200988844,
                    1581817010,
                    2131784896,
                    1234693807,
                    1296608954,
                    954392337,
                    2147365537,
                    14072,
                    2176988889,
                    959900137,
                    1768733505,
                    2215209664,
                    895065994,
                    1196751864,
                    914621940,
                    2349487658,
                    1368417352,
                    1448343881,
                    1894585931,
                    1385322661,
                    1106582025,
                    1700935391,
                    1740562395,
                    939604165,
                    1328751626,
                    797128321,
                    1784482745,
                    2207288699,
                    1356328924,
                    2022463110,
                    1810592176,
                    2124528682,
                    1150760640,
                    1274163900,
                    1858310941,
                    1638742127,
                    661319427,
                    919353761,
                    1282111884,
                    10132,
                    612956053,
                    1993891915,
                    1927124747,
                    971807764,
                    2256109331,
                    1936131747,
                    10129,
                    539943199,
                    1170989053,
                    1292091712,
                    835836079,
                    1356748246,
                    2349484905,
                    10086,
                    519147220,
                    1111669350,
                    2138199381,
                    959641236,
                    1623185000,
                    1883624383,
                    975500312,
                    677636990,
                    1703216433,
                    1740503746,
                    2182271278,
                    803249256,
                    2076428294,
                    1317554978,
                    961066582,
                    2329780962,
                    954947279,
                    941172099,
                    926162531
                },
                [1850456855] = new List<ulong>
                {
                    0,
                    953112839,
                    1797483140,
                    865679836,
                    1759482713,
                    2120628865,
                    2350090284,
                    1865210905,
                    801837047,
                    2296714510,
                    1342123902,
                    2142383374,
                    1442346890,
                    818612271,
                    1121447954,
                    1102986622,
                    1251419748,
                    934937654,
                    1084396407,
                    1332333384,
                    1986050287,
                    1349943069,
                    944586866,
                    947949717,
                    1349158079,
                    1657108026,
                    784577443,
                    1380028657,
                    1159599284,
                    1740068457,
                    1944165903,
                    879861153,
                    934742835,
                    1269612137,
                    1974809731,
                    1154469089,
                    1894376712,
                    1106569231,
                    794291485,
                    2320203004,
                    1539570583,
                    2076261294,
                    892414125,
                    2199787450,
                    1438090382,
                    2147209635,
                    953123363,
                    1441848470,
                    1737733590,
                    1723851847,
                    1103687152,
                    950176525,
                    1151205503,
                    1353722661,
                    1539652650,
                    974345761,
                    1202976443,
                    1388416860,
                    1779983158,
                    828173323,
                    1915398061,
                    1130405286,
                    1906531526,
                    1558579257,
                    1130610212,
                    1400837602,
                    1442167045
                },
                [237239288] = new List<ulong>
                {
                    0,
                    1660293384,
                    1787243248,
                    1100930207,
                    888360095,
                    1961465777,
                    1406835139,
                    2282181821,
                    2192914821,
                    1997534121,
                    2080977144,
                    10001,
                    10049,
                    490773561,
                    2295664263,
                    2293180981,
                    1441311938,
                    1968533197,
                    1432965178,
                    909888619,
                    1987863036,
                    1552705077,
                    1911973450,
                    2289433771,
                    1371313777,
                    1305364315,
                    1587846022,
                    1150816693,
                    2099701364,
                    2067568367,
                    1766646393,
                    889714798,
                    1950854989,
                    2200988235,
                    1581822078,
                    2131787171,
                    1234956405,
                    1296612316,
                    10019,
                    2147367433,
                    2176989787,
                    969289969,
                    1768737448,
                    2215211982,
                    1196747617,
                    1368418893,
                    1448346336,
                    1894589800,
                    1385326314,
                    1106596145,
                    1700938224,
                    1740563572,
                    939586076,
                    1328753424,
                    10078,
                    504687841,
                    823154911,
                    1784474755,
                    2207291626,
                    1356324187,
                    2022464363,
                    1810590744,
                    2124531088,
                    1150763210,
                    1274163146,
                    10048,
                    494003754,
                    1858308973,
                    1638743634,
                    1287193745,
                    1993902344,
                    1927127023,
                    2256110716,
                    1936132863,
                    1229552157,
                    1170988006,
                    1292094174,
                    1356749671,
                    1111673876,
                    10021,
                    2138201022,
                    960252273,
                    798690647,
                    1623181884,
                    1883629284,
                    975498827,
                    1125254090,
                    1703218418,
                    1740505052,
                    2076980911,
                    10020,
                    1317553480,
                    961084105,
                    2329782748,
                    955615160,
                    930559188
                },
                [-1549739227] = new List<ulong>
                {
                    0,
                    1839313604,
                    1100926907,
                    882570089,
                    869090082,
                    826587881,
                    10080,
                    507940691,
                    1406796292,
                    2199934989,
                    2009426933,
                    10023,
                    2090776132,
                    10088,
                    513629119,
                    1441308562,
                    1432967312,
                    899942107,
                    838205144,
                    2075527039,
                    1960694026,
                    919261524,
                    1657109993,
                    784559403,
                    1196740980,
                    1395755190,
                    1106548545,
                    920390242,
                    2304198263,
                    10034,
                    493534620,
                    1864539854,
                    1995685684,
                    613481881,
                    10044,
                    493064563,
                    1111680681,
                    962503020,
                    809586899,
                    1915397286,
                    1084392788,
                    10022,
                    1915955573,
                    961096730,
                    944997041
                },
                [1366282552] = new List<ulong>
                {
                    0,
                    1839312425,
                    1100928373,
                    874488180,
                    1406800025,
                    2199937414,
                    2009427605,
                    816473273,
                    2090790324,
                    10128,
                    565678598,
                    883476299,
                    1432966221,
                    904961862,
                    1552705918,
                    2075536045,
                    1960696487,
                    661317919,
                    1296614997,
                    1633859273,
                    938394833,
                    1394040487,
                    1196737780,
                    1368419860,
                    1448347837,
                    1395757825,
                    921030333,
                    1106600389,
                    610098458,
                    2304196579,
                    1727356926,
                    1864540635,
                    1993913813,
                    1111677889,
                    949616124,
                    962495909,
                    1623175153,
                    1865178625,
                    1084390180,
                    1915956499,
                    961103399
                },
                [-803263829] = new List<ulong>
                {
                    0,
                    848645884,
                    1797478191,
                    914060966,
                    1759479029,
                    2120618167,
                    2350097716,
                    1865208631,
                    2296710564,
                    1342122459,
                    2142393198,
                    1445131741,
                    1121458604,
                    1251411840,
                    938020581,
                    1332335200,
                    1986043465,
                    1349946203,
                    955675586,
                    1349166206,
                    1129809202,
                    1380023142,
                    1740061403,
                    1944168755,
                    970583835,
                    843676357,
                    1269589560,
                    1974807032,
                    1154453278,
                    1894381558,
                    1174375607,
                    2320222274,
                    1539575334,
                    2076260082,
                    891592450,
                    2199783358,
                    1438088592,
                    2147200135,
                    1441850738,
                    1743856800,
                    1104118217,
                    948491992,
                    806212029,
                    1151227603,
                    1248435433,
                    1539650632,
                    974321420,
                    1202978872,
                    784910461,
                    1388417865,
                    1804649832,
                    814098474,
                    919595880,
                    1906527802,
                    1130589746,
                    1400824309,
                    1442169133,
                    809816871,
                    854460770
                },
                [-2002277461] = new List<ulong>
                {
                    0,
                    953104456,
                    1797481354,
                    865659101,
                    1759481001,
                    2120615642,
                    2350092536,
                    1865210028,
                    801873550,
                    2296713508,
                    1342125487,
                    2142378618,
                    1442341176,
                    818611894,
                    1121456497,
                    1102966153,
                    1251431494,
                    934926427,
                    1084394793,
                    1332334593,
                    1986047563,
                    1349940035,
                    944577714,
                    947950933,
                    1349163491,
                    784581113,
                    1380025789,
                    1159597292,
                    1740065674,
                    1944167671,
                    879861502,
                    934744263,
                    1269597852,
                    1974808139,
                    1154446174,
                    1894379005,
                    1119760089,
                    2320209237,
                    1539573170,
                    2076262389,
                    892402754,
                    2199785536,
                    1438089648,
                    2147211029,
                    953124938,
                    1441844877,
                    1743991748,
                    1098029034,
                    950173158,
                    1151219812,
                    1248434418,
                    1539651543,
                    974336556,
                    1202977830,
                    1388417448,
                    1779981832,
                    828175620,
                    1130406273,
                    1234957719,
                    1906530247,
                    1558586741,
                    1130599258,
                    1400828574,
                    1442162947,
                    932778217
                },
                [1221063409] = new List<ulong>
                {
                    0,
                    1911994581,
                    2318482252,
                    2254750609,
                    1874611109,
                    1925748582
                },
                [1353298668] = new List<ulong>
                {
                    0,
                    1228341388,
                    1206145767,
                    1114020299,
                    1402412287,
                    1414795168,
                    930478674,
                    1395469801,
                    948938468,
                    1557857999,
                    869475498,
                    1176460121,
                    804286931,
                    911652483,
                    1477263064,
                    1376526519,
                    801889927,
                    801937986,
                    933057923,
                    801831553,
                    1605324677,
                    839925176,
                    1999927543,
                    809638761,
                    807729959,
                    1092678229,
                    1135412861,
                    885928673,
                    2288730131
                },
                [-148794216] = new List<ulong>
                {
                    0,
                    2317872477,
                    1398568170,
                    1415167317,
                    1334974046,
                    1772483395,
                    1617613419,
                    2010495833,
                    2289421450,
                    1209586977,
                    2156585400,
                    1180981036,
                    1935858699,
                    1529742943,
                    1805270622,
                    1186351868,
                    1926583818,
                    1415079530,
                    1523814360,
                    2323315927,
                    1788350229,
                    1180968592,
                    1886876765,
                    1649777840,
                    1575268855,
                    2238632740,
                    1238292260,
                    2041819488,
                    2222230165,
                    1380090862,
                    1529558717,
                    2123310382,
                    1539143998,
                    2186094580,
                    1465843732,
                    1856195647,
                    1968391406,
                    2146118486,
                    1871289078,
                    1871289078,
                    1645407409,
                    1461027316,
                    1819106723,
                    2255437587,
                    2014975420,
                    2351178062,
                    2155588318,
                    1309406283,
                    1804915784,
                    2006000003,
                    1183127702,
                    1733848365,
                    1759641728,
                    1358030533,
                    1826250647,
                    1747024635,
                    1839473397,
                    1973756459,
                    2101163537,
                    1306203844,
                    2111916381,
                    1846000839,
                    1428456080,
                    1680120997
                },
                [1390353317] = new List<ulong>
                {
                    0,
                    1874961152,
                    1900646657,
                    1936126874,
                    1882782756,
                    1918077744,
                    1926125252,
                    2088339038,
                    1876575703,
                    1904509199,
                    2101572637,
                    1895120296,
                    1885983859,
                    2235414618,
                    1883500337,
                    1911036760,
                    1914299009,
                    2305997989,
                    2253675722,
                    2040477404,
                    1926973479
                },
                [-2067472972] = new List<ulong>
                {
                    0,
                    1852469574,
                    901194793,
                    2131148230,
                    1457845730,
                    950560231,
                    836815358,
                    1328395850,
                    1170684837,
                    1309566989,
                    1415394917,
                    1984902763,
                    1313458951,
                    1109694864,
                    1356364616,
                    1066783524,
                    1733664175,
                    1290467327,
                    1952448742,
                    1362595551,
                    1354718926,
                    1412241247,
                    2346936208,
                    1124738987,
                    1653322594,
                    1175547229,
                    1687047599,
                    1443957299,
                    1466554259,
                    827190175,
                    1539115581,
                    948930384,
                    1227441654,
                    1565096963,
                    1599702939,
                    1617363766,
                    1213613030,
                    1759765099,
                    1447671986,
                    1260208160,
                    1631261352,
                    1852769999,
                    1707455661,
                    915684869,
                    1438420454,
                    2282199805,
                    1281626747,
                    1933669766,
                    942658960,
                    1294718018,
                    1795304359,
                    1992539569,
                    1176406578,
                    2255950540,
                    2051016981,
                    849614068,
                    1120339199,
                    2075130889,
                    10189,
                    928503162,
                    1141051963,
                    917719889,
                    1524017223,
                    921076360,
                    1205721755,
                    1211678957,
                    1239808532,
                    2146923368,
                    1356332123,
                    2264763191,
                    2200677375,
                    832957536,
                    959898495,
                    1870735722,
                    922419554,
                    2091097349,
                    2116010484,
                    1747674239,
                    1886472768,
                    1926577314,
                    1435254318,
                    1587777999,
                    1680572723,
                    1999996993,
                    1812049396,
                    2009712630,
                    1083653685,
                    1514174191,
                    1576050073,
                    859864870,
                    1412186246,
                    1523940330,
                    961909886,
                    1342459239,
                    1390896848,
                    835119969,
                    1383063240,
                    2156515064,
                    934924536,
                    1772296521,
                    1727356485,
                    1117884427,
                    962391797,
                    2320063891,
                    1595324955,
                    897274189,
                    1306412169,
                    1119310953,
                    883741993,
                    1839905607,
                    1587119000,
                    1362729705,
                    1321264697,
                    1780241717,
                    1401769919,
                    1447958101,
                    1380022034,
                    914869833,
                    1328566466,
                    2124140548,
                    1845208821,
                    1448354224,
                    1974769574
                }
            };
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
            [JsonProperty(PropertyName = "Enable Team Hud")]
            public bool EnableTeamHud;

            [JsonProperty(PropertyName = "Enable global team voice chat")]
            public bool EnableTeamVoice;

            [JsonProperty(PropertyName = "Enable team skins")]
            public bool EnableTeamSkins;

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
            public List<PlayerSettings> Players = new List<PlayerSettings>();
            public List<TeamSettings> Teams = new List<TeamSettings>();
            public string WipeID = string.Empty;
        }

        private class PlayerSettings
        {
            public ulong ID;
            public string Name;

            public bool EnableTeamVoice;
            public bool EnableCustomMarker;
            public bool EnableTeamSkins;
            public bool EnableFriendlyFire;

            public PlayerSettings(ulong id, string name, bool enableCustomMarker, bool enableTeamSkins)
            {
                ID = id;
                Name = name;
                EnableTeamVoice = false;
                EnableTeamSkins = enableTeamSkins;
                EnableCustomMarker = enableCustomMarker;
                EnableFriendlyFire = true;
            }

            public void UpdateName(string name) => Name = name;
        }

        private class TeamSettings
        {
            public ulong ID;
            public string TeamName;
            public Dictionary<ulong, AuthorizationSettings> AuthorizationSettings;
            public Dictionary<int, ulong> Skins;
            
            public TeamSettings(ulong id, string teamName, Dictionary<ulong, AuthorizationSettings> authorizationSettings)
            {
                TeamName = teamName;
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

        private void SendMessage(BasePlayer player, string msg, params object[] args) => Player.Message(player, GetMsg(player.UserIDString, msg, args), 76561198297741077);

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
                ["UI_S_ENABLE_TEAM_SKIN_DES"] = "Yes - all items you pick up will automatically receive your team's skin.",
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
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_CHANGE"] = "Изменить",
                ["UI_CHOOSE"] = "Выбрать",
                ["UI_BETTERTEAMS"] = "BETTER TEAMS",
                ["UI_BETTERTEAMSDESCRIPTION"] = "Эта модификация позволит вам получить больше удовольствия от игры с друзьями. Некоторые вещи были изменены и добавлены новые, чтобы вам было удобнее играть с друзьями!",
                ["UI_CHANGES"] = "ИЗМЕНЕНИЯ:",
                ["UI_CHANGEHUD"] = "• Team Hud - позволяет вам видеть статус ваших союзников",
                ["UI_CHANGETeamVoice"] = "• Team Voice - позволяет вам заменить внутриигровой голосовой чат на командный чат. Вас будут слышать только ваши союзники. Работает на любой дистанции!",
                ["UI_CHANGESkins"] = "• Team Skins - позволяет вам выбирать скины для своей команды",
                ["UI_CHANGEEasyTeamMabrkers"] = "• Easy Team Marker - позволяет вам пользоваться маркера без бинокля при помощи бинда",
                ["UI_CHANGEAutoAuthTC"] = "• AutoAuthorization TC - добавляет автоматическую авторизацию в Шкафу",
                ["UI_CHANGEAutoAuthCodeLocks"] = "• AutoAuthorization CodeLocks - добавляет автоматическую авторизацию в Кодовых замках",
                ["UI_CHANGEAutoAuthTurrets"] = "• AutoAuthorization Auto Turrets - добавляет автоматическую авторизацию в Турелях",
                ["UI_CHANGESamSiteAuthorization"] = "• AutoAuthorization SamSite - добавляет автоматическую авторизацию в ПВО",
                ["UI_CHANGEAuthorizationManager"] = "• Team Managment - позволяет лидеру команды менять автоматичекую авторизацию для союзников",
                ["UI_SETTINGSNAME"] = "НАСТРОЙКИ",
                ["UI_S_ENABLE_TEAM_VOICE"] = "КОМАНДНЫЙ ГОЛОСОВОЙ ЧАТ",
                ["UI_S_ENABLE_TEAM_VOICE_DES"] = "Да - заменяет стандартный голосовой чат на командный. Вас слышат только ваши союзники(работает на всю карту)",
                ["UI_S_ENABLE_TEAM_SKINS"] = "КОМАНДНЫЙ СКИНЫ",
                ["UI_S_ENABLE_TEAM_SKIN_DES"] = "Да - все предметы, которые попадают в ваш инвентарь автоматически перекрашиваются в командный скин",
                ["UI_S_ENABLE_MARKERS"] = "КОМАНДНЫЙ МАРКЕР",
                ["UI_S_ENABLE_MARKERS_DES"] = "Да - даёт возможность использовать маркеры без бинокля. Используйте бинд указанный ниже, что бы использовать маркеры",
                ["UI_S_MARKER_BUTTON"] = "БИНД КОМАНДНОГО МАРКЕРА",
                ["UI_S_MARKER_BUTTON_DES"] = "Забиндите команду на использование маркера. Просто скопируйте команду, заменить BUTTON на удобную для вас кнопку и вставьте команду в консоль!\nПример: bind BUTTON ftmark -> binb v ftmark",
                ["UI_S_YES"] = "ДА",
                ["UI_S_NO"] = "НЕТ",
                ["UI_M_MEMBERS"] = "МЕНЕДЖМЕНТ КОМАНДЫ",
                ["UI_M_ONLY_LEADER"] = "Доступно только лидеру команды",
                ["UI_M_AA_H1"] = "АВТОМАТИЧЕСКАЯ АВТОРИЗАЦИЯ",
                ["UI_M_AA_INFO_DES"] = "Все места авторизации (шкафы, кодовые замки, автотурели и т.д.) складываются в единый авторизационный список для всей команды. Если вы заблокируете любому союзнику доступ к списку авторизации, он потеряет доступ ко всем местам авторизации, кроме тех, которые он установил лично (поставил кодлок или шкаф сам), при этом ваша команда не потеряет доступ к его местам авторизации. Полезно, если вы примете в свою команду нового случайного игрока.",
                ["UI_M_SELECT_MATE"] = "Выберите одного из ваших союзников",
                ["UI_M_AA_TC"] = "Авто-авторизация в Шкафу",
                ["UI_M_AA_CL"] = "Авто-авторизация в Кодовых замках",
                ["UI_M_AA_AT"] = "Авто-авторизация в Турелях",
                ["UI_M_AA_SAMSITE"] = "Авто-авторизация в ПВО",
                ["UI_M_KICK"] = "Выгнать из команды",
                ["UI_M_KICK_B"] = "ВЫГНАТЬ",
                ["UI_S_TEAM_SKINS"] = "КОМАНДНЫЕ СКИНЫ",
                ["CM_MARKER_COOLDOWN"] = "Ваш командный маркер перезаряжается: осталось {0}",
                ["UI_H_INFO_TEXT"] = "ОТКРЫТЬ МЕНЮ КОМАНДЫ",
                ["UI_S_PERM"] = "Your team must have at least 1 player with privilege that have access to skins",
                ["CM_DONT_HAVE_PERM"] = "У вас нет прав для использования данной команды",
                ["UI_IS_DISABLED"] = "Данная функция отключена администратором",
                ["CM_NO_TEAM"] = "У вас нет команды. Если вы хотите использовать Better Teams создайте или вступите в команду"
            }, this, "ru");
        }

        #endregion
    }
}
