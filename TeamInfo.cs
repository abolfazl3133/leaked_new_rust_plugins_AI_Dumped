using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("TeamInfo", "discord.gg/9vyTXsJyKR", "1.2.13")]
    internal class TeamInfo : RustPlugin 
    {
        #region Static

        private const string Layer = "UI_TeamInfo";
        private Configuration _config;
        private Dictionary<ulong, bool> _data; 
        private Dictionary<RelationshipManager.PlayerTeam, List<BasePlayer>> playersTeams = new Dictionary<RelationshipManager.PlayerTeam, List<BasePlayer>>();
        private bool update;
        private float Size;
        private float Square;
        private string perm;

        #region Image

        [PluginReference] private Plugin ImageLibrary;
        private int ILCheck = 0;
        private Dictionary<string, string> Images = new Dictionary<string, string>();

        private void AddImage(string url)
        {
            if (!ImageLibrary.Call<bool>("HasImage", url)) ImageLibrary.Call("AddImage", url, url);
            Images.Add(url, ImageLibrary.Call<string>("GetImage", url));
        }

        private void LoadImages() => AddImage("https://i.imgur.com/23mB8WD.png");

        #endregion

        #region Classes

        private class Configuration
        {
            [JsonProperty("Command for open/close the UI")]
            public string Command = "teaminfo";
            
            [JsonProperty("True - left angle | False - right angle")]
            public bool IsLeftAngle = true;

            [JsonProperty("Offset from current angle")]
            public int offsetX = 5;

            [JsonProperty("Offset from bottom")] 
            public int offsetY = 350;

            [JsonProperty("Display players offline")]
            public bool DisplayOfflinePLayers = false;

            [JsonProperty("Enable visible player position")]
            public bool playerPosVisible = true;

            [JsonProperty("If the figure in the player’s position is displayed not correctly change to True")]
            public bool playerPosNum = false;

            [JsonProperty("Enable visible player item")]
            public bool playerItemVisible = true;

            [JsonProperty("Method of displaying active items(true - ImageLibrary display items with skins(but have change to bug) | false - rust method display items without skins(always works))")]
            public bool useDefault = true;

            [JsonProperty("Name permission")] public string permission = "teaminfo.use";
        }

        #endregion

        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
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

        #endregion

        #region Data

        private void LoadData() => _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data") ? Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>($"{Name}/data") : new Dictionary<ulong, bool>();
        private void OnServerSave() => SaveData();

        private void SaveData()
        {
            if (_data != null) Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
        }

        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                if (ILCheck == 3)
                {
                    PrintError("ImageLibrary not found!Unloading");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }

                timer.In(1, () =>
                {
                    ILCheck++;
                    OnServerInitialized();
                });
                return;
            }

            LoadData();
            LoadImages();
            
            cmd.AddChatCommand(_config.Command, this, nameof(cmdChat));

            perm = _config.permission;
            permission.RegisterPermission(perm, this);

            var size = ConVar.Server.worldsize;
            Size = size / 2f;
            Square = Mathf.Floor(size / 146.3f);
            foreach (var check in RelationshipManager.ServerInstance.teams)
            {
                var playerList = new List<BasePlayer>();
                foreach (var checkTeamPlayer in check.Value.members)
                    playerList.Add(RelationshipManager.FindByID(checkTeamPlayer));

                var team = check.Value;
                playersTeams.TryAdd(team, playerList);

                SortTeams(team);
            }

            update = true;
            foreach (var check in BasePlayer.activePlayerList) 
                OnPlayerConnected(check);
            if (_config.playerPosVisible) 
                ServerMgr.Instance.StartCoroutine(UpdateTime());
        }

        private void Unload()
        {
            update = false;
            foreach (var check in BasePlayer.activePlayerList) CuiHelper.DestroyUi(check, Layer + ".bg");
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player?.Team == null) return;
            ShowUIMain(player.Team); 
        }

        private void OnUserPermissionGranted(string id, string permName)
        {
            if (permName != perm) return;
            ShowUIMain(BasePlayer.FindAwakeOrSleeping(id)?.Team);
        }

        private void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName != perm) return;
            CuiHelper.DestroyUi(BasePlayer.FindAwakeOrSleeping(id), Layer + ".bg");
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (oldItem == newItem) return;
            ShowUIItem(player?.Team);
        }

        private void OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue) => ShowUIHPBar(player?.Team);

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            CuiHelper.DestroyUi(player, Layer + ".bg");
            NextTick(() => ShowUIMain(player.Team));
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player) =>
            NextTick(() =>
            {
                if (player == null || team == null) return;
                List<BasePlayer> teamPlayers;
                if (!playersTeams.TryGetValue(team, out teamPlayers))
                {
                    teamPlayers = new List<BasePlayer>();
                    foreach (var checkTeamPlayer in team.members)
                        teamPlayers.Add(RelationshipManager.FindByID(checkTeamPlayer));
                    playersTeams.Add(team, teamPlayers);
                    SortTeams(team);
                    ShowUIMain(team);
                    return;
                }
                
                playersTeams[team].Add(player);
                SortTeams(team); 
                ShowUIMain(team);
            });

        private void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader) => NextTick(() => ShowUIMain(team));

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player) =>
            NextTick(() =>
            {      
                if (player == null || team == null) return;
                List<BasePlayer> basePlayers;
                CuiHelper.DestroyUi(player, Layer + ".bg");

                if (!playersTeams.TryGetValue(team, out basePlayers))
                {
                    var playerList = new List<BasePlayer>();
                    foreach (var checkTeamPlayer in team.members)
                    {
                        var playerTeam = RelationshipManager.FindByID(checkTeamPlayer);
                        if (playerTeam != null) playerList.Add(RelationshipManager.FindByID(checkTeamPlayer));
                    }

                    playersTeams.Add(team, playerList);

                    SortTeams(team);

                    if (team.members.Count != 0) return;
                    playersTeams.Remove(team);
                    return;
                }

                basePlayers.Remove(player);
                if (team.members.Count == 0)
                {
                    playersTeams.Remove(team);
                    return;
                }

                SortTeams(team);
                ShowUIMain(team);
            });

        private void OnPlayerSleepEnded(BasePlayer player) => ShowUIMain(player?.Team);

        private void OnLoseCondition(Item item, ref float amount) => ShowUIItem(item?.GetOwnerPlayer()?.Team);

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            CuiHelper.DestroyUi(player, Layer + ".bg");
            NextTick(() => { ShowUIMain(player?.Team); });
        }

        private void OnPlayerRecover(BasePlayer player) => NextTick(() => { ShowUIHPBar(player?.Team); });

        private void OnPlayerWound(BasePlayer player, HitInfo info) => NextTick(() => { ShowUIHPBar(player?.Team); });

        private void OnTeamCreate(BasePlayer player) 
        {
            NextTick(() =>
            {
                if (player == null) return;
                var playerTeam = player.Team;
                if (playerTeam == null) return;
                playersTeams.TryAdd(playerTeam, new List<BasePlayer> { player });
                SortTeams(playerTeam);
                ShowUIMain(playerTeam);
            });
        }

        #endregion

        #region Commands

        private void cmdChat(BasePlayer player, string command, string[] args)
        {
            if (player?.Team == null || args.Length != 0) return;
            _data[player.userID] = !_data[player.userID];
            if (_data[player.userID]) ShowUIMain(player.Team);
            else CuiHelper.DestroyUi(player, Layer + ".bg");
        }

        #endregion

        #region Functions

        private void SortTeams(RelationshipManager.PlayerTeam team)
        {
            var teamList = playersTeams[team];
            var playerList = new List<BasePlayer>();
            var leader = team.teamLeader;
            foreach (var check in teamList)
                if (check?.userID == leader)
                    playerList.Add(check);
            foreach (var check in teamList)
                if (check?.userID != leader)
                    playerList.Add(check);

            playersTeams[team] = playerList;
        }

        private bool GetPlayerData(BasePlayer player)
        {
            bool status;
            if (_data.TryGetValue(player.userID, out status)) return status;
            _data.Add(player.userID, true);
            return true;
        }

        private string GetCorrectName(string name, int length)
        {
            string res = name;
            if (name.Length > length)
                res = name.Substring(0, length);
            return res;
        }

        private string GetGrid(Vector3 pos)
        {
            var letter = 'A';
            var xCoordinate = Mathf.Floor((pos.x + Size) / 146.3f);
            var z = Square - Mathf.Floor((pos.z + Size) / 146.3f) - (_config.playerPosNum ? 0 : 1);
            letter = (char)(letter + xCoordinate % 26);
            return xCoordinate > 25 ? $"A{letter}{z}" : $"{letter}{z}";
        }

        private bool HasPermission(string id) => permission.UserHasPermission(id, perm);

        private IEnumerator UpdateTime()
        {
            while (update)
            {
                foreach (var check in BasePlayer.activePlayerList) 
                    ShowUIPos(check?.Team);
                yield return new WaitForSeconds(5f);
            }
        }

        #endregion

        #region UI

        private void ShowUIPos(RelationshipManager.PlayerTeam team)
        {
            if (team == null || !update) return;
            var container = new CuiElementContainer();
            List<BasePlayer> playerList;
            playersTeams.TryGetValue(team, out playerList);
            var leader = team.teamLeader;
            if (playerList == null || !leader.IsSteamId()) return;
            var posY = 0;

            container.Add(new CuiPanel 
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                },
                Image = { Color = "0 0 0 0.8" }
            }, Layer + ".bg", Layer + ".pos");

            foreach (var check in playerList.OrderBy(x => leader == x?.userID))
            {
                if (check == null || (!check.IsConnected && !_config.DisplayOfflinePLayers)) continue;

                container.Add(new CuiElement
                {
                    Parent = Layer + ".pos",
                    Name = Layer + ".label",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"120 {posY}", OffsetMax = $"148 {posY + 26}"
                        },
                        new CuiTextComponent
                        {
                            Text = GetGrid(check.transform.position),
                            FontSize = 12, Color = "1 1 1 1",
                            Align = TextAnchor.UpperRight, Font = "robotocondensed-regular.ttf",
                        },
                        new CuiOutlineComponent { Distance = "-0.5 -0.5", Color = "0 0 0 1"},
                    },
                });

                posY += 28;
            }

            foreach (var check in playerList)
            {
                if (check == null || !check.IsConnected || !GetPlayerData(check) || !HasPermission(check.UserIDString)) continue;
                CuiHelper.DestroyUi(check, Layer + ".pos"); 
                CuiHelper.AddUi(check, container);
            }
        }

        private void ShowUIItem(RelationshipManager.PlayerTeam team)
        {
            if (!_config.playerItemVisible || team == null || !update) return;
            var container = new CuiElementContainer();
            List<BasePlayer> playerList;
            playersTeams.TryGetValue(team, out playerList);
            var leader = team.teamLeader;
            if (playerList == null || !leader.IsSteamId()) return;
            var posY = 0;

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                },
                Image = { Color = "0 0 0 0.8" }
            }, Layer + ".bg", Layer + ".item");
            

            foreach (var check in playerList.OrderBy(x => leader == x?.userID))
            {
                if (check == null || (!check.IsConnected && !_config.DisplayOfflinePLayers)) continue;
                var item = check.GetActiveItem();
                if (item != null)
                {
                    var weapon = item.GetHeldEntity()?.GetComponent<BaseProjectile>();

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = $"154 {posY}", OffsetMax = $"180 {posY + 26}"
                        },
                        Image = { Color = "0 0 0 0.8" }
                    }, Layer + ".item", Layer + posY + ".item");
 
                    if (_config.useDefault)
                        container.Add(new CuiElement
                        {
                            Parent = Layer + posY + ".item",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    ItemId = item.info.itemid,
                                    SkinId = item.skin
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "2 2", OffsetMax = "-2 -2"
                                }
                            }
                        });
                    else
                        container.Add(new CuiElement
                        {
                            Parent = Layer + posY + ".item",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = ImageLibrary.Call<string>("GetImage", item.info.shortname, item.skin)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "2 2", OffsetMax = "-2 -2"
                                }
                            }
                        });

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = $"0 {item.condition / item.maxCondition}",
                            OffsetMin = "0 0", OffsetMax = "2 0"
                        },
                        Image = { Color = "0 0.73 0 0.85" }
                    }, Layer + posY + ".item");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "0 -2"
                        },
                        Text =
                        {
                            Text = weapon == null ? item.amount + "x" : weapon.primaryMagazine.contents.ToString(),
                            FontSize = 8,
                            Font = "robotocondensed-regular.ttf",
                            Color = "1 1 1 1",
                            Align = TextAnchor.LowerRight,
                        }
                    }, Layer + posY + ".item");
                }

                posY += 28;
            }

            foreach (var check in playerList)
            {
                if (check == null || !check.IsConnected || !GetPlayerData(check) || !HasPermission(check.UserIDString)) continue;
                CuiHelper.DestroyUi(check, Layer + ".item");
                CuiHelper.AddUi(check, container);
            }
        }

        private void ShowUIHPBar(RelationshipManager.PlayerTeam team)
        {
            if (team == null || !update) return;
            var container = new CuiElementContainer();
            List<BasePlayer> playerList;
            playersTeams.TryGetValue(team, out playerList);
            var leader = team.teamLeader;
            if (playerList == null || !leader.IsSteamId()) return;
            var posY = 0;

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                },
                Image = { Color = "0 0 0 0.8" }
            }, Layer + ".bg", Layer + ".hpBar");

            foreach (var check in playerList.OrderBy(x => leader == x?.userID))
            {
                if (check == null || (!check.IsConnected && !_config.DisplayOfflinePLayers)) continue;
                var curHp = Math.Ceiling(check.Health());
                var maxHp = check.MaxHealth();
                var isWounded = check.IsWounded();
                var isDead = check.IsDead();

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = $"28 {posY}", OffsetMax = $"149 {posY + 12}"
                    },
                    Image = { Color = "0.25 0.25 0.25 0.85" }
                }, Layer + ".hpBar", Layer + posY + ".hpBar");

                if (!isWounded && !isDead)
                {
                    var newValue = (int)(curHp / maxHp * 121);
                    var pendingHealth = check.metabolism.pending_health.value;

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0 1",
                            OffsetMin = "0 0", OffsetMax = $"{newValue} 0"
                        },
                        Image = { Color = $"{1 - curHp / maxHp - 0.27} {curHp / maxHp - 0.27} 0 1" }
                    }, Layer + posY + ".hpBar");
                    if (pendingHealth > 0)
                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0 1",
                                OffsetMin = $"{newValue} 0", OffsetMax = $"{(pendingHealth + newValue > 121 ? 121 : pendingHealth + newValue)} 0"
                            },
                            Image = { Color = "0 0.73 0 0.35" }
                        }, Layer + posY + ".hpBar");
                }

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 -4", OffsetMax = "0 5"
                    },
                    Text =
                    {
                        Text = isDead || curHp == 0 ? "<color=#EE2C43>ÖLDÜ</color>" : isWounded ? "<color=#EE8843>YARALI</color>" : curHp.ToString(CultureInfo.InvariantCulture),
                        FontSize = 13,
                        Font = "robotocondensed-bold.ttf",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer + posY + ".hpBar");
                posY += 28;
            } 

            foreach (var check in playerList)
            {
                if (check == null || !check.IsConnected || !GetPlayerData(check) || !HasPermission(check.UserIDString)) continue;
                CuiHelper.DestroyUi(check, Layer + ".hpBar");
                CuiHelper.AddUi(check, container);
            }
        }

        private void ShowUIMain(RelationshipManager.PlayerTeam team)
        {
            if (team == null || !update) return;
            var container = new CuiElementContainer();
            List<BasePlayer> playerList;
            playersTeams.TryGetValue(team, out playerList);
            var leader = team.teamLeader;
            if (playerList == null || !leader.IsSteamId()) return;
            var posY = 0;

            container.Add(new CuiPanel
            {
                RectTransform = {
                    AnchorMin = $"{(_config.IsLeftAngle ? "0.005" : "1")} 0.24", AnchorMax = $"{(_config.IsLeftAngle ? "0.005" : "1")}  0.24", 
                    OffsetMin = $"{(_config.IsLeftAngle ? _config.offsetX : _config.offsetX * -1 - 180)} {_config.offsetY}",
                    OffsetMax = $"{(_config.IsLeftAngle ? _config.offsetX : _config.offsetX * -1 - 180)} {_config.offsetY}"
                },
                Image = { Color = "0 0 0 0" } 
            }, "Under", Layer + ".bg");

            foreach (var check in playerList.OrderBy(x => leader == x?.userID))
            {
                if (check == null || (!check.IsConnected && !_config.DisplayOfflinePLayers)) continue;
                var isLeader = check.userID == leader;

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = $"0 {posY}", OffsetMax = $"180 {posY + 26}"
                    },
                    Image = { Color = "0.9686 0.9216 0.8824 0" }
                }, Layer + ".bg", Layer + posY);
     
                
                container.Add(new CuiElement
                {
                    Parent = Layer + posY,
                    Name = Layer + posY + ".avatar",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary.Call<string>("GetImage", check.UserIDString)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 1",
                            OffsetMin = "0 0", OffsetMax = "26 0"
                        }
                    }
                });

                if (isLeader)
                    container.Add(new CuiElement
                    {
                        Parent = Layer + posY,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = ImageLibrary.Call<string>("GetImage", "https://i.imgur.com/23mB8WD.png"),
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1",
                                OffsetMin = "28 -12", OffsetMax = "45 0"
                            }
                        }
                    });

                container.Add(new CuiElement
                {
                    Parent = Layer + posY,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetCorrectName(check.displayName, 20),
                            FontSize = 12,
                            Font = "robotocondensed-bold.ttf",
                            Color = "1 1 1 1",
                            Align = TextAnchor.UpperLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = isLeader ? "46 0" : "28 0", OffsetMax = "0 0"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 0.9",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });
                posY += 28;
            }


            foreach (var check in playerList)
            {
                if (check == null || !check.IsConnected || !GetPlayerData(check) || !HasPermission(check.UserIDString)) continue;
                CuiHelper.DestroyUi(check, Layer + ".bg");
                CuiHelper.AddUi(check, container);
            }

            ShowUIHPBar(team);
            ShowUIItem(team);
            if (_config.playerPosVisible) ShowUIPos(team);
        }

        #endregion
    }
}