using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
using Oxide.Game.Rust.Cui;



namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("TCAuth", "https://discord.gg/dNGbxafuJn", "1.2.1")]
    internal class TCAuth : RustPlugin
    {
        #region Static

        private const string Layer = "UI_TCAuth";
        private const string perm = "tcauth.bypass";
        private const string permUI = "tcauth.use";
        private Configuration _config;
        
        #region Classes

        private class Configuration
        {
            [JsonProperty("The maximum number of players that can register in the TC")]
            public int maxPlayers = 0;
            
            [JsonProperty("Only registered players can open chests (when registered on TC)")]
            public bool OAChest = true;
            
            [JsonProperty("Only registered players can open furnaces (when registered on TC)")]
            public bool OAOven = true;
            
            [JsonProperty("Automatically when players are registered on TC (Autohorized on Turrets, SAM Site)")]
            public bool ARTurrets = true;
            
            [JsonProperty("Automatically when players are registered on the TC (remove building parts)")]
            public bool ARRemove = true;
            
            [JsonProperty("Automatically when players are registered on the TC (open codelocks without a code)")]
            public bool AROpen = true;  
            
            [JsonProperty("Automatically registered your teammates on the TC")]
            public bool ARTeam = true;
            
            [JsonProperty("List of shortprefabs containers ")]
            public List<string> StorageContainers = new List<string>
            {
                "woodbox_deployed",
                "composter",
                "fridge.deployed",
                "box.wooden.large",
                "locker.deployed",
                "small_stash_deployed",
                "dropbox.deployed",
                "coffinstorage",
            };
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

        #region OxideHooks

        private void OnServerInitialized()
        {
            if (_config.ARRemove) SetAlwaysDemolish();
            permission.RegisterPermission(perm, this);
            permission.RegisterPermission(permUI, this);
            if (_config.AROpen)
                foreach (var codeLock in BaseNetworkable.serverEntities.OfType<CodeLock>())
                {
                    if (codeLock == null) continue;
                    var allTCPlayers = codeLock.GetBuildingPrivilege()?.authorizedPlayers;
                    if (allTCPlayers == null) continue;
                    foreach (var playerNameID in allTCPlayers)
                    {
                        if (codeLock.guestPlayers.Contains(playerNameID.userid)) continue;
                        codeLock.guestPlayers.Add(playerNameID.userid);
                    }
                    codeLock.SendNetworkUpdate();
                }
        }

        private void Unload()
        {
            foreach (var check in BasePlayer.activePlayerList) 
                CuiHelper.DestroyUi(check, Layer + ".bg");
        }
        
        private void OnEntitySpawned(AutoTurret entity) =>
            NextTick(() =>
            {
                if (entity == null) 
                    return;
                
                var authorizedPlayers = entity.GetBuildingPrivilege()?.authorizedPlayers;
                if (authorizedPlayers == null)
                    return;
                
                var isOnline = entity.IsOnline();
                if (isOnline)
                    entity.SetIsOnline(false);

                foreach (var player in authorizedPlayers)
                {
                    entity.authorizedPlayers.ToList().RemoveAll(x => x.userid == player.userid);
                    entity.authorizedPlayers.Add(new PlayerNameID {username = "Player", userid = player.userid});                
                }

                if (isOnline)
                    entity.SetIsOnline(true);

                entity.UpdateMaxAuthCapacity();
                entity.SendNetworkUpdate(); 
            });

        private void OnEntitySpawned(BuildingPrivlidge entity) =>
            NextTick(() =>
            {
                if (entity == null)
                    return;

                if (!entity.OwnerID.IsSteamId())
                    return;

                var ownerPlayer = BasePlayer.FindAwakeOrSleeping(entity.OwnerID.ToString());
                if (ownerPlayer == null || ownerPlayer.Team?.members == null) 
                    return;

                foreach (var member in ownerPlayer.Team.members)
                    OnCupboardAuthorize(entity, BasePlayer.FindAwakeOrSleeping(member.ToString()));
            });


        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            NextTick(() =>
            {
                if (!_config.ARTeam || team == null || player == null)
                    return;

                foreach (var tc in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>())
                {
                    if (tc.authorizedPlayers.FirstOrDefault(x => x.userid == team.teamLeader) == null || tc.authorizedPlayers.FirstOrDefault(x => x.userid == player.userID) != null)
                        continue;
                    tc.authorizedPlayers.Add(new PlayerNameID { username = player.displayName, userid = player.userID });
                    tc.UpdateMaxAuthCapacity();
                    tc.SendNetworkUpdate();
                    OnCupboardAuthorize(tc, player);
                }
            });
        }

        private object CanLootEntity(BasePlayer player, BaseOven oven)
        {
            if (player == null || oven == null || permission.UserHasPermission(player.UserIDString, perm) || !_config.OAOven) return null;
            var tc = oven.GetBuildingPrivilege();
            if (tc == null || tc.IsAuthed(player)) return null;
            return false;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer box)
        {
            if (player == null || box == null || permission.UserHasPermission(player.UserIDString, perm) || !_config.OAChest || !_config.StorageContainers.Contains(box.ShortPrefabName)) return null;
            var tc = box.GetBuildingPrivilege();
            if (tc == null || tc.IsAuthed(player)) return null;
            return false;
        }
        
        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null || permission.UserHasPermission(player.UserIDString, perm) || !_config.AROpen) return null;
            var tc = baseLock.GetBuildingPrivilege();
            if (tc == null) return null;
            if (tc.IsAuthed(player) && tc.IsAuthed(baseLock.OwnerID)) return true;
            return null;
        }

        private void OnEntitySpawned(BuildingBlock block)
        {
            NextTick(() =>
            {
                if (block == null || permission.UserHasPermission(block.OwnerID.ToString(), perm) || !_config.ARRemove) return;
                block.SetFlag(BaseEntity.Flags.Reserved2, true);
            });
        }

        private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege == null || player == null || permission.UserHasPermission(player.UserIDString, perm)) return null;
            if (privilege.authorizedPlayers.Count >= _config.maxPlayers) return false;
            NextTick(() =>
            {
                if (_config.ARTurrets)
                    AuthPlayerInTurrets(player.userID, privilege.transform.position);
                if (_config.AROpen)
                    AuthPlayerInLock(player.userID, privilege.transform.position, privilege.authorizedPlayers.ToList());   
            });
            return null;
        }

        private void OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege == null || player == null || permission.UserHasPermission(player.UserIDString, perm)) return;
            if (_config.ARTurrets)
                DeauthAllPlayerInTurrets(privilege.transform.position);
            if (_config.AROpen)
                DeauthAllPlayerInLock(privilege.transform.position);
        }
        
        private object OnSamSiteTarget(SamSite samSite, PlayerHelicopter target)
        {
            if (_config.ARTurrets && target != null && samSite != null && target.GetDriver() != null && samSite.GetBuildingPrivilege() != null && samSite.GetBuildingPrivilege().IsAuthed(target.GetDriver()))
                return false;
            return null;
        }

        private void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player) =>
            NextTick(() =>
            {
                
                if (privilege == null || player == null || permission.UserHasPermission(player.UserIDString, perm)) return;
                if (_config.ARTurrets)
                    DeauthPlayerInTurrets(player.userID, privilege.transform.position);
                if (_config.AROpen) 
                    DeauthPlayerInLock(player.userID, privilege.transform.position);            
            });

        #endregion

        #region Commands
        
        [ChatCommand("tssettings")]
        private void cmdChattssettings(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUI))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            ShowUIMain(player);
        }

        [ConsoleCommand("UI_TA")]
        private void cmdConsole(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null && arg.Args.Length < 1) return;
            var player = arg.Player();
            switch (arg.GetString(0))
            {
                case "CHGMAXPLAYERS":
                    _config.maxPlayers = arg.GetInt(1);
                    break;
                case "CHGCHEST":
                    _config.OAChest = !_config.OAChest;
                    break;
                case "CHGOVEN":
                    _config.OAOven = !_config.OAOven;
                    break;
                case "CHGTURRETS":
                    _config.ARTurrets = !_config.ARTurrets;
                    break;
                case "CHGREMOVE":
                    _config.ARRemove = !_config.ARRemove;
                    if (_config.ARRemove)
                        SetAlwaysDemolish();
                    else 
                        RemoveAlwaysDemolist();
                    break;
                case "CHGOPEN":
                    _config.AROpen = !_config.AROpen;
                    break;
                case "CHGTEAM":
                    _config.ARTeam = !_config.ARTeam;
                    break;
            }

            SaveConfig();
            ShowUISetup(player);
        }

        #endregion

        #region Functions

        private void SetAlwaysDemolish()
        {
            foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingBlock>()) check.SetFlag(BaseEntity.Flags.Reserved2, true);
        }

        private void RemoveAlwaysDemolist()
        {
            foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingBlock>()) check.StartBeingDemolishable();
        } 
        
        private void AuthPlayerInLock(ulong id, Vector3 position, List<PlayerNameID> authorizedPlayers)
        {
            var locks = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(position, 18.5f, locks);
            foreach (var check in locks) 
            {
                foreach (var child in check.children.ToArray())
                {
                    if (!(child is CodeLock)) continue;
                    var codeLock = child as CodeLock; 
                    if (authorizedPlayers.FirstOrDefault(x => x.userid == codeLock.OwnerID) == null) 
                        continue;
                    if (!codeLock.guestPlayers.Contains(id))
                    {
                        codeLock.guestPlayers.Add(id);
                    }

                    codeLock.SendNetworkUpdate();
                }
            }

            Facepunch.Pool.FreeList(ref locks);
        }
         
        private void CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
        {
            if (codeLock == null || isGuestCode) return;
            var allTCPlayers = codeLock.GetBuildingPrivilege()?.authorizedPlayers;
            if (allTCPlayers == null) return;
            foreach (var check in allTCPlayers)
            {
                if (codeLock.guestPlayers.Contains(check.userid)) continue;
                codeLock.guestPlayers.Add(check.userid);
            }
            codeLock.SendNetworkUpdate();

        }
        
        private void DeauthAllPlayerInLock(Vector3 position)
        {
            var locks = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(position, 18.5f, locks);
            foreach (var check in locks)
            {
                foreach (var child in check.children.ToArray())
                {
                    if (!(child is CodeLock)) continue;
                    (child as CodeLock).guestPlayers.Clear(); 
                    (child as CodeLock).SendNetworkUpdate();
                }
            }

            Facepunch.Pool.FreeList(ref locks);
        }
        
        private void DeauthPlayerInLock(ulong id, Vector3 position)
        {
            var locks = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(position, 18.5f, locks);
            foreach (var check in locks)
            {
                foreach (var child in check.children.ToArray())
                {
                    if (!(child is CodeLock)) continue;
                    var codeLock = child as CodeLock;
                    foreach (var whitePlayer in codeLock.guestPlayers.ToArray())
                    {
                        if (whitePlayer == id)
                            codeLock.guestPlayers.Remove(whitePlayer);
                        codeLock.SendNetworkUpdate();
                    }
                    
                }
            }
            Facepunch.Pool.FreeList(ref locks);
        }

        private void AuthPlayerInTurrets(ulong id, Vector3 position)
        {
            var turrets = Facepunch.Pool.GetList<AutoTurret>();
            Vis.Entities(position, 18.5f, turrets);
            foreach (var check in turrets)
            {
                var isOnline = check.IsOnline();
                if (isOnline)
                {
                    check.SetIsOnline(false);
                }
                
                check.authorizedPlayers.ToList().RemoveAll(x => x.userid == id);
                check.authorizedPlayers.Add(new PlayerNameID {username = "Player", userid = id});
                if (isOnline)
                {
                    check.SetIsOnline(true);
                }
                check.UpdateMaxAuthCapacity();
                check.SendNetworkUpdate();
            }

            Facepunch.Pool.FreeList(ref turrets);
        }

        private void DeauthAllPlayerInTurrets(Vector3 position)
        {
            var turrets = Facepunch.Pool.GetList<AutoTurret>();
            Vis.Entities(position, 18.5f, turrets);
            foreach (var check in turrets)
            {
                check.authorizedPlayers.Clear();
                check.UpdateMaxAuthCapacity();
                check.SendNetworkUpdate();
            }

            Facepunch.Pool.FreeList(ref turrets);
        }

        private void DeauthPlayerInTurrets(ulong id, Vector3 position)
        {
            var turrets = Facepunch.Pool.GetList<AutoTurret>();
            Vis.Entities(position, 18.5f, turrets);
            foreach (var check in turrets)
            {
                check.authorizedPlayers.ToList().RemoveAll(x => x.userid == id);
                check.UpdateMaxAuthCapacity();
                check.SendNetworkUpdate();
            }

            Facepunch.Pool.FreeList(ref turrets);
        }

        #endregion

        #region UI

        private void ShowUISetup(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var posY = -50;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.9"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".mainPanel", Layer);

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = "The maximum number of players that can register in the TC: ", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.9 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}" },
                Image = { Color = "0.3 0.3 0.3 0.92"}
            }, Layer, Layer + ".input");
            
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = _config.maxPlayers.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.3"
                }
            }, Layer + ".input");
            
            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Command = "UI_TA CHGMAXPLAYERS",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        CharsLimit = 3,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                    }
                }
            });

            posY -= 35;
            
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.OAChest ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGCHEST"},
                Text =
                {
                    Text = "Only registered players can open chests (when registered on TC)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.OAOven ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGOVEN"},
                Text =
                {
                    Text = "Only registered players can open furnaces (when registered on TC)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.ARTurrets ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGTURRETS "},
                Text =
                {
                    Text = "Automatically when players are registered on TC (Autohorized on Turrets and SAM site)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.ARRemove ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGREMOVE "},
                Text =
                {
                    Text = "Automatically when players are registered on the TC (remove building parts)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.AROpen ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGOPEN "},
                Text =
                {
                    Text = "Automatically when players are registered on the TC (open codelocks without a code)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);
            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.ARTeam ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGTEAM "},
                Text =
                {
                    Text = "Automatically registered your teammates on the TC", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIMain(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"}
            }, "Overlay", Layer + ".bg");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.2 0.4", AnchorMax = "0.8 0.8"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".bg", Layer + ".mainPanel");
            Outline(ref container, Layer + ".mainPanel");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0.9", AnchorMax = "1 1"},
                Text =
                {
                    Text = "SETUP TC", Font = "robotocondensed-bold.ttf", FontSize = 25,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".mainPanel");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0.9", AnchorMax = "1 0.9", OffsetMin = "0 0", OffsetMax = "0 2"},
                Image = {Color = "1 1 1 1"}
            }, Layer + ".mainPanel");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-29 -29", OffsetMax = "0 0"},
                Button = {Color = "0 0 0 0", Close = Layer + ".bg"},
                Text =
                {
                    Text = "×", Font = "robotocondensed-regular.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter,
                    Color = "0.56 0.58 0.64 1.00"
                }
            }, Layer + ".mainPanel", Layer + ".buttonClose");
            Outline(ref container, Layer + ".buttonClose");

            CuiHelper.DestroyUi(player, Layer + ".bg");
            CuiHelper.AddUi(player, container);

            ShowUISetup(player);
        }

        private void Outline(ref CuiElementContainer container, string parent, string color = "1 1 1 1",
            string size = "2")
        {
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 0", OffsetMax = $"0 {size}"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{size}", OffsetMax = $"0 0"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 {size}", OffsetMax = $"{size} -{size}"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{size} {size}", OffsetMax = $"0 -{size}"},
                Image = {Color = color}
            }, parent);
        }

        #endregion
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */