using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Ext.Chaos;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Ext.Chaos.UIFramework;
using UnityEngine;
using UnityEngine.UI;

using Color = Oxide.Ext.Chaos.UIFramework.Color;
using Font = Oxide.Ext.Chaos.UIFramework.Font;

namespace Oxide.Plugins
{
    [Info("DynamicCupShare", "Nuclear", "3.1.5")]
    [Description("Dynamic sharing of cupboards/doors/boxes/lockers/turrets between friends, clan members and team members")]
    class DynamicCupShare : ChaosPlugin
    {
        #region Fields
        private static List<ShareType> allowedShareTypes;
      
        private RaycastHit raycastHit;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        #endregion
       
        #region Oxide Hooks
        private void Loaded()
        {
            SetupUIComponents();
            
            permission.RegisterPermission(Configuration.Permission.ClanShare.Permission, this);
            permission.RegisterPermission(Configuration.Permission.FriendShare.Permission, this);
            permission.RegisterPermission(Configuration.Permission.TeamShare.Permission, this);
            permission.RegisterPermission(Configuration.Permission.AdminPermission, this);

            allowedShareTypes = Configuration.Sharing.Allowed.AllowedShareTypes;

            if (!Configuration.Building.PreventIceberg && !Configuration.Building.PreventIcelake && !Configuration.Building.PreventIcesheet)
                Unsubscribe(nameof(CanBuild));

            if (!CanShare(ShareType.Turret))
            {
                Unsubscribe(nameof(OnTurretTarget));
                Unsubscribe(nameof(CanBeTargeted));
                Unsubscribe(nameof(OnSamSiteTarget));
            }
            
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
            
            cmd.AddChatCommand(Configuration.Sharing.ChatCommand, this, cmdShare);

            LoadData();
        }

        protected override void PopulatePhrases()
        {
            m_Messages = new Dictionary<string, string>
            {
                ["Message.Title"] = "<color=#ce422b>[ DCS ]</color> ",

                ["UI.Title"] = "Dynamic Share",

                ["UI.Share.Clan"] = "Clan Share",
                ["UI.Share.Friend"] = "Friend Share",
                ["UI.Share.Team"] = "Team Share",

                ["UI.Type.Box"] = "Boxes",
                ["UI.Type.Cupboard"] = "Cupboards",
                ["UI.Type.Door"] = "Doors",
                ["UI.Type.Locker"] = "Lockers",
                ["UI.Type.Turret"] = "Turrets",
                ["UI.Type.Hitch"] = "Hitch & Troughs",
                ["UI.Type.Composter"] = "Composters",
                ["UI.Type.Dropbox"] = "Drop Boxes",
                ["UI.Type.VendingMachine"] = "Vending Machines",
                ["UI.Type.Furnace"] = "Furnaces",
                ["UI.Type.Refinery"] = "Refineries",
                ["UI.Type.Bbq"] = "BBQs",
                ["UI.Type.Planters"] = "Planters",
                ["UI.Type.MixingTable"] = "Mixing Tables",
                
                ["Chat.NoTurretToggle"] = "<color=#ce422b>Turret share can not be toggled</color>",
                ["Chat.ShareEnabled"] = "You have enabled <color=#ce422b>{0}</color> sharing for <color=#ce422b>{1}s</color>",
                ["Chat.ShareDisabled"] = "You have disabled <color=#ce422b>{0}</color> sharing for <color=#ce422b>{1}s</color>",
            
                ["Message.AdminEnabled"] = "<color=#ce422b>[ DCS ]</color> Admin mode enabled!",
                ["Message.AdminDisabled"] = "<color=#ce422b>[ DCS ]</color> Admin mode disabled!",

                ["Error.NoPermissions"] = "<color=#ce422b>You do not have permission to use this command</color>",
                ["Error.MaxCupboardAuth"] = "<color=#ce422b>This cupboard already has the maximum allowed authorizations</color>",
                ["Error.AuthDenied"] = "<color=#ce422b>Authorization denied!</color>",
                ["Error.ClearAuthDenied"] = "<color=#ce422b>Clear authorization denied!</color>",
                ["Error.NoBuild.Iceberg"] = "You are not allowed to build on <color=#ce422b>icebergs</color>",
                ["Error.NoBuild.IceSheet"] = "You are not allowed to build on <color=#ce422b>ice sheets</color>",
                ["Error.NoBuild.IceLake"] = "You are not allowed to build on <color=#ce422b>ice lakes</color>",
            };
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityKill));
            
            SetupLockableContainers();
            
            if (Configuration.Data.PurgeAfter > 0)
                PurgeOldData();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            timer.In(5, FindRegisterEntities);
        }

        private void OnServerSave()
        {
            SaveData();
            TemporaryShares.Save();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            PlayerPrivilege.AddPlayer(player);
            storedData.SetupPlayer(player.userID).lastOnline = UnixTimeStampUtc();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            PlayerPrivilege.RemovePlayer(player);
            ChaosUI.Destroy(player, UI_MENU);
        }

        #region Entity Management
        private void OnEntitySpawned(BuildingPrivlidge buildingPrivlidge) => NextTick(()=>
        {
            if (buildingPrivlidge)
                PlayerEntities.GetOrCreate(buildingPrivlidge.OwnerID)?.AddEntity(buildingPrivlidge, true);
        });
        
        private void OnEntitySpawned(AutoTurret autoTurret) => NextTick(()=>
        {
            if (autoTurret)
                PlayerEntities.GetOrCreate(autoTurret.OwnerID)?.AddEntity(autoTurret, true);
        });

        private void OnEntitySpawned(CodeLock codeLock) => NextTick(() =>
        {
            if (!codeLock)
                return;

            PlayerEntities.GetOrCreate(codeLock.OwnerID)?.AddEntity(codeLock, true);

            StorageContainer storageContainer = codeLock.GetParentEntity() as StorageContainer;
            if (!storageContainer)
                return;

            SetLockPositionRotation(storageContainer, codeLock);
        });
        
        private void OnEntityKill(BuildingPrivlidge buildingPrivlidge) => PlayerEntities.Get(buildingPrivlidge.OwnerID)?.RemoveEntity(buildingPrivlidge, true);
        
        private void OnEntityKill(AutoTurret autoTurret) => PlayerEntities.Get(autoTurret.OwnerID)?.RemoveEntity(autoTurret, true);
        
        private void OnEntityKill(CodeLock codeLock) => PlayerEntities.Get(codeLock.OwnerID)?.RemoveEntity(codeLock, true);
        #endregion
        
        #region Lock Usage

        private void CanChangeCode(BasePlayer player, CodeLock codeLock, string code, bool guestCode) => NextTick(() =>
        {
            if (codeLock)
            {
                ShareType shareType = GetShareTypeFromEntity(codeLock.GetParentEntity());
                if (shareType != ShareType.None)
                    AuthorizationQueue.Enqueue(TemporaryShares.RebuildSharesFor(shareType, codeLock));
            }
        });
        
        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (!player || !baseLock || !baseLock.IsLocked())
                return null;

            if (InAdminMode(player))
                return true;

            if (baseLock is CodeLock)
                return null;

            if (baseLock.OwnerID == player.userID)
                return true;

            if (baseLock is KeyLock && !Configuration.Sharing.DisableKeylocks)
            {
                bool result = CanUseLockedObject(player, baseLock.GetParentEntity());
                return result ? (object)true : null;
            }

            return null;
        }

        private object CanUnlock(BasePlayer player, BaseLock baseLock)
        {
            if (!player || !baseLock)
                return null;

            if (InAdminMode(player))
            {
                if (baseLock is CodeLock)
                    Effect.server.Run((baseLock as CodeLock).effectUnlocked.resourcePath, baseLock, 0U, Vector3.zero, Vector3.forward, null, false);

                baseLock.SetFlag(BaseEntity.Flags.Locked, false, false);
                baseLock.SendNetworkUpdate();
                return true;
            }

            if (Configuration.Security.ShareLockUnlock && CanUseLockedObject(player, baseLock.GetParentEntity()))
            {
                if (baseLock is CodeLock)
                {
                    CodeLock codeLock = baseLock as CodeLock;
                    
                    if (codeLock.IsCodeEntryBlocked())
                        return null;
                    
                    Effect.server.Run(codeLock.effectUnlocked.resourcePath, codeLock, 0, Vector3.zero, Vector3.forward, null, false);
                    codeLock.SetFlag(BaseEntity.Flags.Locked, false, false, true);
                    codeLock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    return true;
                }
                
                KeyLock keyLock = baseLock as KeyLock;
                if (keyLock)
                {
                    keyLock.SetFlag(BaseEntity.Flags.Locked, false, false, true);
                    keyLock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    return true;
                }
            }

            return null;
        }

        private object CanLock(BasePlayer player, BaseLock baseLock)
        {
            if (!player || !baseLock)
                return null;

            if (InAdminMode(player))
            {
                if (baseLock is CodeLock)
                    Effect.server.Run((baseLock as CodeLock).effectLocked.resourcePath, baseLock, 0u, Vector3.zero, Vector3.forward, null, false);

                baseLock.SetFlag(BaseEntity.Flags.Locked, true, false);
                baseLock.SendNetworkUpdate();
                return true;
            }
            
            if (Configuration.Security.ShareLockUnlock && CanUseLockedObject(player, baseLock.GetParentEntity()))
            {
                if (baseLock is CodeLock)
                {
                    CodeLock codeLock = baseLock as CodeLock;
                    
                    if (codeLock.IsCodeEntryBlocked())
                        return null;
                    
                    Effect.server.Run(codeLock.effectLocked.resourcePath, codeLock, 0, Vector3.zero, Vector3.forward, null, false);
                    codeLock.SetFlag(BaseEntity.Flags.Locked, true, false, true);
                    codeLock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    return true;
                }
                
                KeyLock keyLock = baseLock as KeyLock;
                if (keyLock)
                {
                    keyLock.SetFlag(BaseEntity.Flags.Locked, true, false, true);
                    keyLock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    return true;
                }
            }

            return null;
        }
        #endregion

        #region Targeting 
        private object OnTurretTarget(AutoTurret autoTurret, BasePlayer player)
        {
            if (!player || !autoTurret || autoTurret.OwnerID == 0UL)
                return null;

            if (InAdminMode(player))
                return true;

            if (!autoTurret.AnyAuthed() || !autoTurret.IsAuthed(autoTurret.OwnerID))
                return null;

            bool result = CanUseTurret(player, autoTurret);

            if (result)
                TemporaryShares.RegisterPlayerTo(player, autoTurret);

            return result ? (object)true : null;
        }

        private object CanBeTargeted(BasePlayer player, FlameTurret flameTurret)
        {
            if (!player || !flameTurret || flameTurret.OwnerID == 0UL || !Configuration.Turrets.IncludeFlameTurrets)
                return null;

            if (InAdminMode(player))
                return false;

            return CanUseTurret(player, flameTurret) ? (object)false : null;
        }

        private object CanBeTargeted(BasePlayer player, GunTrap gunTrap)
        {
            if (!player || !gunTrap || gunTrap.OwnerID == 0UL || !Configuration.Turrets.IncludeGunTraps)
                return null;

            if (InAdminMode(player))
                return false;

            return CanUseTurret(player, gunTrap) ? (object)false : null;
        }

        private object OnSamSiteTarget(SamSite samSite, BaseCombatEntity baseCombatEntity)
        {
            if (!baseCombatEntity || !samSite || !Configuration.Turrets.IncludeSameSites || samSite.OwnerID == 0UL)
                return null;

            if (baseCombatEntity is HotAirBalloon)
            {
                HotAirBalloon hotAirBalloon = (baseCombatEntity as HotAirBalloon);
                if (hotAirBalloon && hotAirBalloon.children.Count == 0)
                    return true;

                for (int i = 0; i < hotAirBalloon.children.Count; i++)
                {
                    if (CanUseTurret(hotAirBalloon.children[i] as BasePlayer, samSite))
                        return true;
                }

                return null;
            }

            if (baseCombatEntity is BaseVehicle)
            {
                BaseVehicle baseVehicle = baseCombatEntity as BaseVehicle;
                if (!baseVehicle || !baseVehicle.AnyMounted())
                    return true;

                for (int i = 0; i < baseVehicle.mountPoints.Count; i++)
                {
                    BaseVehicle.MountPointInfo mountPoint = baseVehicle.mountPoints[i];
                    if (mountPoint != null && mountPoint.mountable && CanUseTurret(mountPoint.mountable.GetMounted(), samSite))
                        return true;
                }
            }

            return null;
        }
        #endregion
        
		private object CanBuild(Planner planner, Construction construction, Construction.Target target)
		{
			BasePlayer player = planner.GetOwnerPlayer();

			Construction.Placement placement = new Construction.Placement();

			if (target.socket != null)
			{
				List<Socket_Base> list = Facepunch.Pool.GetList<Socket_Base>();
				construction.FindMaleSockets(target, list);

				foreach (Socket_Base current in list)
				{
					if (!(target.entity) || !(target.socket) || !target.entity.IsOccupied(target.socket))
					{
						placement = current.DoPlacement(target);
						break;
					}
				}

				Facepunch.Pool.FreeList<Socket_Base>(ref list);
			}
			else
			{
				placement.position = target.position;
				placement.rotation = Quaternion.Euler(target.rotation);

				if (placement.rotation == Quaternion.identity)
					placement.rotation = Quaternion.Euler(0, planner.GetOwnerPlayer().transform.rotation.eulerAngles.y, 0);
			}

			if (Physics.Raycast(placement.position, Vector3.down, out var raycastHit, placement.position.y, 65536))
			{
				if (Configuration.Building.PreventIceberg && raycastHit.collider.name.ToLower().StartsWith("iceberg"))
				{
					player.LocalizedMessage(this, "Error.NoBuild.Iceberg");
					return false;
				}

				if (Configuration.Building.PreventIcelake && raycastHit.collider.name.ToLower().StartsWith("ice_lake"))
				{
					player.LocalizedMessage(this, "Error.NoBuild.IceLake");
					return false;
				}

				if (Configuration.Building.PreventIcesheet && raycastHit.collider.name.ToLower().StartsWith("ice_sheet"))
				{
					player.LocalizedMessage(this, "Error.NoBuild.IceSheet");
					return false;
				}
			}

			return (object)placement;
		}

        #region Turret Authorization
        private object OnTurretAuthorize(AutoTurret autoTurret, BasePlayer player)
        {
            if (!autoTurret || !player)
                return null;
            
            if (autoTurret.OwnerID == player.userID || !autoTurret.IsAuthed(autoTurret.OwnerID))
                return null;

            if (InAdminMode(player))
                return null;
            
            bool isFriendly = (Configuration.Sharing.Clan.Enabled && AreClanMates(autoTurret.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Friend.Enabled && AreFriends(autoTurret.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Team.Enabled && AreTeamMates(autoTurret.OwnerID, player.userID));

            if (Configuration.Security.BlockNonAuth && !isFriendly)
            {
                player.LocalizedMessage(this, "Error.ClearAuthDenied");
                return true;
            }

            if (Configuration.Security.BlockAuth && isFriendly)
            {
                player.LocalizedMessage(this, "Error.ClearAuthDenied");
                return true;
            }

            return null;
        }

        private object OnTurretClearList(AutoTurret autoTurret, BasePlayer player)
        {
            if (!autoTurret || !player)
                return null;
            
            if (autoTurret.OwnerID == player.userID)
                return null;

            if (InAdminMode(player))
                return null;
            
            bool isFriendly = (Configuration.Sharing.Clan.Enabled && AreClanMates(autoTurret.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Friend.Enabled && AreFriends(autoTurret.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Team.Enabled && AreTeamMates(autoTurret.OwnerID, player.userID));

            if (Configuration.Security.BlockNonAuth && !isFriendly)
            {
                player.LocalizedMessage(this, "Error.ClearAuthDenied");
                return true;
            }

            if (Configuration.Security.BlockAuth && isFriendly)
            {
                player.LocalizedMessage(this, "Error.ClearAuthDenied");
                return true;
            }

            return null;
        }
        #endregion

        #region Cupboard Authorization
        private object OnCupboardClearList(BuildingPrivlidge buildingPrivlidge, BasePlayer player)
        {
            if (!buildingPrivlidge || !player)
                return null;
            
            if (buildingPrivlidge.OwnerID == player.userID)
                return null;

            if (InAdminMode(player))
                return null;
            
            bool isFriendly = (Configuration.Sharing.Clan.Enabled && AreClanMates(buildingPrivlidge.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Friend.Enabled && AreFriends(buildingPrivlidge.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Team.Enabled && AreTeamMates(buildingPrivlidge.OwnerID, player.userID));

            if (Configuration.Security.BlockNonAuth && !isFriendly)
            {
                player.LocalizedMessage(this, "Error.ClearAuthDenied");
                return true;
            }

            if (Configuration.Security.BlockAuth && isFriendly)
            {
                player.LocalizedMessage(this, "Error.ClearAuthDenied");
                return true;
            }

            return null;
        }

        private object OnCupboardAuthorize(BuildingPrivlidge buildingPrivlidge, BasePlayer player)
        {
            if (!buildingPrivlidge || !player)
                return null;

            if (InAdminMode(player))
                return null;

            if (Configuration.Security.MaxCupboardAuth > 0 && buildingPrivlidge.authorizedPlayers.Count >= Configuration.Security.MaxCupboardAuth)
            {
                player.LocalizedMessage(this, "Error.MaxCupboardAuth");
                return true;
            }

            if (buildingPrivlidge.OwnerID == player.userID || !buildingPrivlidge.IsAuthed(buildingPrivlidge.OwnerID))
                return null;
            
            bool isFriendly = (Configuration.Sharing.Clan.Enabled && AreClanMates(buildingPrivlidge.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Friend.Enabled && AreFriends(buildingPrivlidge.OwnerID, player.userID)) ||
                              (Configuration.Sharing.Team.Enabled && AreTeamMates(buildingPrivlidge.OwnerID, player.userID));

            if (Configuration.Security.BlockNonAuth && !isFriendly)
            {
                player.LocalizedMessage(this, "Error.AuthDenied");
                return true;
            }

            if (Configuration.Security.BlockAuth && isFriendly)
            {
                player.LocalizedMessage(this, "Error.AuthDenied");
                return true;
            }

            return null;
        }
        #endregion
     
        private void Unload()
        {
            AuthorizationQueue.OnUnload();
            UpdateCycler.OnUnload();
              
            TemporaryShares.Save();
            TemporaryShares.OnUnload();
            
            ResetLockableContainerPrefabs();
            
            PlayerPrivilege.OnUnload();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player);

            if (!Interface.Oxide.IsShuttingDown)
                SaveData();

            Configuration = null;
        }
        #endregion

        #region Cupboard Component
		private class UpdateCycler : MonoBehaviour
		{
			private static UpdateCycler m_Instance;
			
			private static readonly UpdateCupboardQueue _UpdateCupboardQueue = new UpdateCupboardQueue();

			private void Update()
			{
				_UpdateCupboardQueue.RunQueue(0.5);
			}

			private void OnDestroy()
			{
				_UpdateCupboardQueue.Clear();
			}

			public static void Enqueue(PlayerPrivilege playerPrivilege)
			{
				if (!m_Instance)
					m_Instance = new GameObject("UpdateCupboardWorker").AddComponent<UpdateCycler>();
				
				_UpdateCupboardQueue.Add(playerPrivilege);
			}

			public static void OnUnload()
			{
				if (m_Instance)
					Destroy(m_Instance.gameObject);
			}

			public class UpdateCupboardQueue : ObjectWorkQueue<PlayerPrivilege>
			{
				public override void RunJob(PlayerPrivilege playerPrivilege)
				{
					if (!ShouldAdd(playerPrivilege))
						return;
					
					playerPrivilege.UpdateNearbyCupboards();
					Add(playerPrivilege);
				}

				public override bool ShouldAdd(PlayerPrivilege playerPrivilege)
				{
					return playerPrivilege;
				}
			}
		}
			   
        private class PlayerPrivilege : MonoBehaviour
        {
            private BasePlayer m_Player;

            private ulong m_PlayerID;

            private BuildingPrivlidge m_LastRegisteredTo;

            private bool m_InAdminMode = false;
            
            public bool InAdminMode
            {
                get { return m_InAdminMode; }
                set
                {
                    if (m_InAdminMode && !value)
                    {
                        if (m_LastRegisteredTo)
                            TemporaryShares.RemovePlayerFrom(m_Player, m_LastRegisteredTo);

                        m_LastRegisteredTo = null;
                    }

                    m_InAdminMode = value;
                }
            }

            private static Hash<BasePlayer, PlayerPrivilege> m_PlayerLookup = new Hash<BasePlayer, PlayerPrivilege>();

            private void Awake()
            {
                m_Player = GetComponent<BasePlayer>();
                m_PlayerID = m_Player.userID;
                
                UpdateCycler.Enqueue(this);
            }
            
            private void OnDestroy()
            {
                if (InAdminMode && m_LastRegisteredTo && !m_LastRegisteredTo.IsDestroyed)
                    TemporaryShares.RemovePlayerFrom(m_Player, m_LastRegisteredTo);
            }

            public void UpdateNearbyCupboards()
            {
                if (m_Player)
                {
                    BuildingPrivlidge buildingPrivilege = m_Player.GetBuildingPrivilege();
                    if (!buildingPrivilege || buildingPrivilege == m_LastRegisteredTo)
                        return;

                    if (!buildingPrivilege.IsAuthed(m_PlayerID))
                    {
                        if (InAdminMode)
                        {
                            TemporaryShares.RegisterPlayerTo(m_Player, buildingPrivilege);
                           
                            if (buildingPrivilege != m_LastRegisteredTo)
                                TemporaryShares.RemovePlayerFrom(m_Player, m_LastRegisteredTo);

                            m_LastRegisteredTo = buildingPrivilege;
                            return;
                        }

                        if (ShouldRegisterToCupboard(buildingPrivilege))
                        {
                            TemporaryShares.RegisterPlayerTo(m_Player, buildingPrivilege);
                            m_LastRegisteredTo = buildingPrivilege;
                        }
                    }
                }
            }

            private bool ShouldRegisterToCupboard(BuildingPrivlidge buildingPrivilege)
            {
                if (Configuration.Security.PreventShareNoOwner && !buildingPrivilege.IsAuthed(buildingPrivilege.OwnerID))
                    return false;
                
                StoredData.PlayerData data = storedData.FindPlayerData(buildingPrivilege.OwnerID);
                if (data == null)
                    return false;

                if (!CanShare(ShareType.Cupboard))
                    return false;

                if (CanShare(TeamType.Clan, buildingPrivilege.OwnerID) && data.IsSharing(TeamType.Clan, ShareType.Cupboard) && AreClanMates(buildingPrivilege.OwnerID, m_Player.userID))
                    return true;
            
                if (CanShare(TeamType.Friend, buildingPrivilege.OwnerID) && data.IsSharing(TeamType.Friend, ShareType.Cupboard) && AreFriends(buildingPrivilege.OwnerID, m_Player.userID))
                    return true;
            
                if (CanShare(TeamType.Team, buildingPrivilege.OwnerID) && data.IsSharing(TeamType.Team, ShareType.Cupboard) && AreTeamMates(buildingPrivilege.OwnerID, m_Player.userID))
                    return true;

                return false;
            }
            
            public static bool Find(BasePlayer player, out PlayerPrivilege playerPrivilege) => m_PlayerLookup.TryGetValue(player, out playerPrivilege);

            public static bool IsAdmin(BasePlayer player)
            {
                PlayerPrivilege playerPrivilege;
                if (m_PlayerLookup.TryGetValue(player, out playerPrivilege) && playerPrivilege)
                    return playerPrivilege.InAdminMode;

                return false;
            }
            
            public static void AddPlayer(BasePlayer player)
            {
                PlayerPrivilege playerPrivilege;
                if (m_PlayerLookup.TryGetValue(player, out playerPrivilege) && playerPrivilege)
                    return;

                playerPrivilege = player.gameObject.AddComponent<PlayerPrivilege>();

                playerPrivilege.InAdminMode = Configuration.Permission.ToggleAdminPermissionOnJoin && player.HasPermission(Configuration.Permission.AdminPermission);
                
                m_PlayerLookup[player] = playerPrivilege;
            }

            public static void RemovePlayer(BasePlayer player)
            {
                PlayerPrivilege playerPrivilege;
                if (!m_PlayerLookup.TryGetValue(player, out playerPrivilege))
                    return;
                
                m_PlayerLookup.Remove(player);
                Destroy(playerPrivilege);
            }

            public static void OnUnload()
            {
                List<PlayerPrivilege> components = m_PlayerLookup.Values.ToList();
                for (int i = components.Count - 1; i >= 0; i--)
                {
                    PlayerPrivilege admin = components[i];
                    if (admin)
                        Destroy(admin);
                }
                
                m_PlayerLookup.Clear();
            }
        }
        
        private class AuthorizationQueue : MonoBehaviour
        {
            private readonly Queue<IEnumerator> m_AuthorizationQueue = new Queue<IEnumerator>();

            private bool m_QueueRunning = false;

            private Coroutine m_Current;

            private static AuthorizationQueue m_Instance;

            private void Awake()
            {
                m_Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            protected void OnDestroy()
            {
                m_AuthorizationQueue.Clear();

                if (m_Current != null)
                    StopCoroutine(m_Current);
                
                m_Instance = null;
            }

            public static void Enqueue(IEnumerator enumerator)
            {
                if (!m_Instance)
                    m_Instance = new GameObject("DCS_AuthorizationQueue").AddComponent<AuthorizationQueue>();

                m_Instance.m_AuthorizationQueue.Enqueue(enumerator);

                if (!m_Instance.m_QueueRunning)
                    m_Instance.StartProcessingQueue();
            }

            public static void OnUnload()
            {
                if (m_Instance)
                    Destroy(m_Instance.gameObject);
            }
            
            private void StartProcessingQueue()
            {
                m_Current = StartCoroutine(RunQueue());
            }
            
            private IEnumerator RunQueue()
            {
                m_QueueRunning = true;
                
                while (m_AuthorizationQueue.Count > 0)
                {
                    IEnumerator enumerator = m_AuthorizationQueue.Dequeue();
                    yield return StartCoroutine(enumerator);
                }

                m_QueueRunning = false;
            }

            public static Coroutine Run(IEnumerator function) => m_Instance.StartCoroutine(function);
        }
        
        private class PlayerEntities
        {
            private List<BuildingPrivlidge> m_BuildingPrivileges;
            private List<AutoTurret> m_AutoTurrets;
            private List<CodeLock> m_CodeLocks;

            private static Hash<ulong, PlayerEntities> m_PlayerEntities = new Hash<ulong, PlayerEntities>();

            public static PlayerEntities GetOrCreate(ulong playerId)
            {
                if (!playerId.IsSteamId())
                    return null;
                
                PlayerEntities playerEntities;
                if (!m_PlayerEntities.TryGetValue(playerId, out playerEntities))
                    playerEntities = m_PlayerEntities[playerId] = new PlayerEntities();

                return playerEntities;
            }

            public static PlayerEntities Get(ulong playerId)
            {
                if (!playerId.IsSteamId())
                    return null;
                
                PlayerEntities playerEntities;
                if (m_PlayerEntities.TryGetValue(playerId, out playerEntities))
                    return playerEntities;

                return null;
            }

            private PlayerEntities(){}
            
            public void AddEntity(BuildingPrivlidge buildingPrivlidge, bool rebuild)
            {
                if (!buildingPrivlidge)
                    return;
                
                if (rebuild)
                    AuthorizationQueue.Enqueue(TemporaryShares.RebuildSharesFor(buildingPrivlidge));
                
                if (m_BuildingPrivileges == null)
                    m_BuildingPrivileges = Facepunch.Pool.GetList<BuildingPrivlidge>();
                else if (m_BuildingPrivileges.Contains(buildingPrivlidge))
                    return;
                
                m_BuildingPrivileges.Add(buildingPrivlidge);
            }

            public void RemoveEntity(BuildingPrivlidge buildingPrivlidge, bool destroyed)
            {
                if (!buildingPrivlidge)
                    return;
                
                if (destroyed)
                    TemporaryShares.OnEntityDestroyed(buildingPrivlidge);
                
                if (m_BuildingPrivileges == null)
                    return;

                m_BuildingPrivileges.Remove(buildingPrivlidge);
                
                if (m_BuildingPrivileges.Count == 0)
                    Facepunch.Pool.FreeList(ref m_BuildingPrivileges);
            }
            
            public void AddEntity(AutoTurret autoTurret, bool rebuild)
            {
                if (!autoTurret)
                    return;
                
                if (rebuild)
                    AuthorizationQueue.Enqueue(TemporaryShares.RebuildSharesFor(autoTurret));
                
                if (m_AutoTurrets == null)
                    m_AutoTurrets = Facepunch.Pool.GetList<AutoTurret>();
                else if (m_AutoTurrets.Contains(autoTurret))
                    return;
                
                m_AutoTurrets.Add(autoTurret);
            }

            public void RemoveEntity(AutoTurret autoTurret, bool destroyed)
            {
                if (!autoTurret)
                    return;
                
                if (destroyed)
                    TemporaryShares.OnEntityDestroyed(autoTurret);
                
                if (m_AutoTurrets == null)
                    return;

                m_AutoTurrets.Remove(autoTurret);
                
                if (m_AutoTurrets.Count == 0)
                    Facepunch.Pool.FreeList(ref m_AutoTurrets);
            }
            
            public void AddEntity(CodeLock codeLock, bool rebuild)
            {
                if (!codeLock)
                    return;
                
                ShareType shareType = GetShareTypeFromEntity(codeLock.GetParentEntity());
                if (shareType == ShareType.None)
                    return;
                
                if (rebuild)
                    AuthorizationQueue.Enqueue(TemporaryShares.RebuildSharesFor(shareType, codeLock));

                if (m_CodeLocks == null)
                    m_CodeLocks = Facepunch.Pool.GetList<CodeLock>();
                else if (m_CodeLocks.Contains(codeLock))
                    return;
                
                m_CodeLocks.Add(codeLock);
            }

            public void RemoveEntity(CodeLock codeLock, bool destroyed)
            {
                if (!codeLock)
                    return;
                
                if (destroyed)
                    TemporaryShares.OnEntityDestroyed(codeLock);
                
                if (m_CodeLocks == null)
                    return;

                m_CodeLocks.Remove(codeLock);
                
                if (m_CodeLocks.Count == 0)
                    Facepunch.Pool.FreeList(ref m_CodeLocks);
            }

            public void OnToggleShareType(ShareType shareType)
            {
                if (shareType == ShareType.Cupboard)
                {
                    if (m_BuildingPrivileges?.Count > 0)
                        TemporaryShares.RebuildSharesFor(m_BuildingPrivileges);
                }
                else if (shareType == ShareType.Turret)
                {
                    if (m_AutoTurrets?.Count > 0)
                        TemporaryShares.RebuildSharesFor(m_AutoTurrets);
                }
                else
                {
                    if (m_CodeLocks?.Count > 0)
                        TemporaryShares.RebuildSharesFor(shareType, m_CodeLocks);
                }
            }

            public void RebuildAll()
            {
                if (m_BuildingPrivileges?.Count > 0)
                    TemporaryShares.RebuildSharesFor(m_BuildingPrivileges);
                
                if (m_AutoTurrets?.Count > 0)
                    TemporaryShares.RebuildSharesFor(m_AutoTurrets);
                
                if (m_CodeLocks?.Count > 0)
                    TemporaryShares.RebuildSharesFor(m_CodeLocks);
            }
        }
          
        private static class TemporaryShares
        {
            private static Hash<BuildingPrivlidge, List<ulong>> m_BuildingPrivilegeShares = new Hash<BuildingPrivlidge, List<ulong>>();
            private static Hash<AutoTurret, List<ulong>> m_AutoTurretShares = new Hash<AutoTurret, List<ulong>>();
            private static Hash<CodeLock, List<ulong>> m_CodeLockShares = new Hash<CodeLock, List<ulong>>();

            private static Covalence m_Covalence = Interface.Oxide.GetLibrary<Covalence>();

            private static List<ulong> m_MemberShareBuffer = new List<ulong>();
            private static List<ulong> m_TempShareBuffer = new List<ulong>();

            
            #region Tool Cupboards
            public static void RebuildSharesFor(List<BuildingPrivlidge> list)
            {
                for (int i = 0; i < list.Count; i++)
                    AuthorizationQueue.Enqueue(RebuildSharesFor(list[i]));
            }
            
            public static IEnumerator RebuildSharesFor(BuildingPrivlidge buildingPrivlidge)
			{
				yield return null;
				
				if (buildingPrivlidge && buildingPrivlidge.OwnerID != 0UL && (!Configuration.Security.PreventShareNoOwner || buildingPrivlidge.IsAuthed(buildingPrivlidge.OwnerID)))
				{
					bool hasChanges = false;
					List<ulong> currentShares;
					if (m_BuildingPrivilegeShares.TryGetValue(buildingPrivlidge, out currentShares))
					{
						// Create a list to hold authorized players that need to be removed
						List<PlayerNameID> playersToRemove = new List<PlayerNameID>();

						foreach (var authorizedPlayer in buildingPrivlidge.authorizedPlayers)
						{
							if (currentShares.Contains(authorizedPlayer.userid))
							{
								playersToRemove.Add(authorizedPlayer);
								hasChanges = true;
							}
						}

						// Remove players from authorizedPlayers
						foreach (var player in playersToRemove)
						{
							buildingPrivlidge.authorizedPlayers.Remove(player);
						}

						currentShares.Clear();
					}

					m_MemberShareBuffer.Clear();
					m_TempShareBuffer.Clear();

					if (CanShare(ShareType.Cupboard))
					{
						StoredData.PlayerData playerData = storedData.FindPlayerData(buildingPrivlidge.OwnerID);
						if (playerData != null)
						{
							if (Configuration.Sharing.Clan.Enabled && playerData.IsSharing(TeamType.Clan, ShareType.Cupboard))
								GetClanMembers(buildingPrivlidge.OwnerID, ref m_MemberShareBuffer);

							if (Configuration.Sharing.Friend.Enabled && playerData.IsSharing(TeamType.Friend, ShareType.Cupboard))
								GetFriends(buildingPrivlidge.OwnerID, ref m_MemberShareBuffer);

							if (Configuration.Sharing.Team.Enabled && playerData.IsSharing(TeamType.Team, ShareType.Cupboard))
								GetTeamMembers(buildingPrivlidge.OwnerID, ref m_MemberShareBuffer);
						}

						foreach (ulong memberId in m_MemberShareBuffer)
						{
							if (m_TempShareBuffer.Contains(memberId) || memberId == buildingPrivlidge.OwnerID)
								continue;

							if (!buildingPrivlidge.IsAuthed(memberId))
							{
								IPlayer player = m_Covalence.Players.FindPlayerById(memberId.ToString());
								buildingPrivlidge.authorizedPlayers.Add(new PlayerNameID { userid = memberId, username = player != null ? player.Name : string.Empty });
							}
							m_TempShareBuffer.Add(memberId);
							hasChanges = true;
						}

						if (currentShares == null)
							currentShares = m_BuildingPrivilegeShares[buildingPrivlidge] = Facepunch.Pool.GetList<ulong>();

						if (m_TempShareBuffer.Count > 0)
						{
							for (int i = 0; i < m_TempShareBuffer.Count; i++)
							{
								if (!currentShares.Contains(m_TempShareBuffer[i]))
									currentShares.Add(m_TempShareBuffer[i]);
							}
						}
					}

					if (hasChanges)
						buildingPrivlidge.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
				}
			}

            public static void OnEntityDestroyed(BuildingPrivlidge buildingPrivlidge)
            {
                List<ulong> currentShares;
                if (m_BuildingPrivilegeShares.TryGetValue(buildingPrivlidge, out currentShares))
                {
                    Facepunch.Pool.FreeList(ref currentShares);
                    m_BuildingPrivilegeShares.Remove(buildingPrivlidge);
                }
            }
            
            public static void RegisterPlayerTo(BasePlayer player, BuildingPrivlidge buildingPrivlidge)
            {
                List<ulong> currentShares;
                if (!m_BuildingPrivilegeShares.TryGetValue(buildingPrivlidge, out currentShares))
                    currentShares = m_BuildingPrivilegeShares[buildingPrivlidge] = Facepunch.Pool.GetList<ulong>();
                    
                if (!buildingPrivlidge.IsAuthed(player.userID))
                {
                    buildingPrivlidge.authorizedPlayers.Add(new PlayerNameID{ userid = player.userID, username = player.displayName});
                    buildingPrivlidge.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    
                    if (!currentShares.Contains(player.userID))
                        currentShares.Add(player.userID);
                }
            }
            
			public static void RemovePlayerFrom(BasePlayer player, BuildingPrivlidge buildingPrivlidge)
			{
				List<ulong> currentShares;
				if (m_BuildingPrivilegeShares.TryGetValue(buildingPrivlidge, out currentShares) && currentShares.Contains(player.userID))
					currentShares.Remove(player.userID);

				// Create a list to hold players to remove
				List<PlayerNameID> playersToRemove = new List<PlayerNameID>();

				foreach (var playerNameID in buildingPrivlidge.authorizedPlayers)
				{
					if (playerNameID.userid == player.userID)
						playersToRemove.Add(playerNameID);
				}

				// Remove players from the hashset
				foreach (var playerToRemove in playersToRemove)
				{
					buildingPrivlidge.authorizedPlayers.Remove(playerToRemove);
				}

				buildingPrivlidge.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
			}
            #endregion
            
            #region Auto Turrets
            public static void RebuildSharesFor(List<AutoTurret> list)
            {
                for (int i = 0; i < list.Count; i++)
                    AuthorizationQueue.Enqueue(RebuildSharesFor(list[i]));
            }
            
			public static IEnumerator RebuildSharesFor(AutoTurret autoTurret)
			{
				yield return null;

				if (autoTurret && autoTurret.OwnerID != 0UL && (!Configuration.Security.PreventShareNoOwner || autoTurret.IsAuthed(autoTurret.OwnerID)))
				{
					bool hasChanges = false;
					List<ulong> currentShares;
					if (m_AutoTurretShares.TryGetValue(autoTurret, out currentShares))
					{
						// Create a list to hold authorized players that need to be removed
						List<PlayerNameID> playersToRemove = new List<PlayerNameID>();

						foreach (var authorizedPlayer in autoTurret.authorizedPlayers)
						{
							if (currentShares.Contains(authorizedPlayer.userid))
							{
								playersToRemove.Add(authorizedPlayer);
								hasChanges = true;
							}
						}

						// Remove players from authorizedPlayers
						foreach (var player in playersToRemove)
						{
							autoTurret.authorizedPlayers.Remove(player);
						}

						currentShares.Clear();
					}

					m_MemberShareBuffer.Clear();
					m_TempShareBuffer.Clear();

					if (CanShare(ShareType.Turret))
					{
						StoredData.PlayerData playerData = storedData.FindPlayerData(autoTurret.OwnerID);
						if (playerData != null)
						{
							if (Configuration.Sharing.Clan.Enabled && playerData.IsSharing(TeamType.Clan, ShareType.Turret))
								GetClanMembers(autoTurret.OwnerID, ref m_MemberShareBuffer);

							if (Configuration.Sharing.Friend.Enabled && playerData.IsSharing(TeamType.Friend, ShareType.Turret))
								GetFriends(autoTurret.OwnerID, ref m_MemberShareBuffer);

							if (Configuration.Sharing.Team.Enabled && playerData.IsSharing(TeamType.Team, ShareType.Turret))
								GetTeamMembers(autoTurret.OwnerID, ref m_MemberShareBuffer);
						}

						foreach (ulong memberId in m_MemberShareBuffer)
						{
							if (m_TempShareBuffer.Contains(memberId) || memberId == autoTurret.OwnerID)
								continue;

							if (!autoTurret.IsAuthed(memberId))
							{
								IPlayer player = m_Covalence.Players.FindPlayerById(memberId.ToString());
								autoTurret.authorizedPlayers.Add(new PlayerNameID { userid = memberId, username = player != null ? player.Name : string.Empty });
							}

							m_TempShareBuffer.Add(memberId);
							hasChanges = true;
						}

						if (currentShares == null)
							currentShares = m_AutoTurretShares[autoTurret] = Facepunch.Pool.GetList<ulong>();

						if (m_TempShareBuffer.Count > 0)
						{
							for (int i = 0; i < m_TempShareBuffer.Count; i++)
							{
								if (!currentShares.Contains(m_TempShareBuffer[i]))
									currentShares.Add(m_TempShareBuffer[i]);
							}
						}
					}

					if (hasChanges)
						autoTurret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
				}
			}

           public static void OnEntityDestroyed(AutoTurret autoTurret)
            {
                List<ulong> currentShares;
                if (m_AutoTurretShares.TryGetValue(autoTurret, out currentShares))
                {
                    Facepunch.Pool.FreeList(ref currentShares);
                    m_AutoTurretShares.Remove(autoTurret);
                }
            }
            
            public static void RegisterPlayerTo(BasePlayer player, AutoTurret autoTurret)
            {
                List<ulong> currentShares;
                if (!m_AutoTurretShares.TryGetValue(autoTurret, out currentShares))
                    currentShares = m_AutoTurretShares[autoTurret] = Facepunch.Pool.GetList<ulong>();

                if (!autoTurret.IsAuthed(player.userID))
                {
                    autoTurret.authorizedPlayers.Add(new PlayerNameID {userid = player.userID, username = player.displayName});
                    autoTurret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                    if (!currentShares.Contains(player.userID))
                        currentShares.Add(player.userID);
                }
            }

			public static void RemovePlayerFrom(BasePlayer player, AutoTurret autoTurret)
			{
				List<ulong> currentShares;
				if (m_AutoTurretShares.TryGetValue(autoTurret, out currentShares) && currentShares.Contains(player.userID))
					currentShares.Remove(player.userID);

				// Create a list to hold players to remove
				List<PlayerNameID> playersToRemove = new List<PlayerNameID>();

				foreach (var playerNameID in autoTurret.authorizedPlayers)
				{
					if (playerNameID.userid == player.userID)
						playersToRemove.Add(playerNameID);
				}

				// Remove players from the HashSet
				foreach (var playerToRemove in playersToRemove)
				{
					autoTurret.authorizedPlayers.Remove(playerToRemove);
				}

				autoTurret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
			}

            #endregion
            
            #region Code Locks
            public static void RebuildSharesFor(ShareType shareType, List<CodeLock> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CodeLock codeLock = list[i];

                    if (codeLock)
                    {
                        ShareType entityShareType = GetShareTypeFromEntity(codeLock.GetParentEntity());

                        if (entityShareType == shareType)
                            AuthorizationQueue.Enqueue(RebuildSharesFor(shareType, list[i]));
                    }
                }
            }
            
            public static void RebuildSharesFor(List<CodeLock> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CodeLock codeLock = list[i];
                    ShareType shareType = GetShareTypeFromEntity(codeLock.GetParentEntity());
                    
                    AuthorizationQueue.Enqueue(RebuildSharesFor(shareType, list[i]));
                }
            }
            
            public static IEnumerator RebuildSharesFor(ShareType shareType, CodeLock codeLock)
            {
                yield return null;

                if (codeLock && codeLock.OwnerID != 0UL && (!Configuration.Security.PreventShareNoOwner || codeLock.whitelistPlayers.Contains(codeLock.OwnerID)))
                {
                    bool hasChanges = false;
                    List<ulong> currentShares;
                    if (m_CodeLockShares.TryGetValue(codeLock, out currentShares))
                    {
                        for (int i = currentShares.Count - 1; i >= 0; i--)
                        {
                            ulong memberId = currentShares[i];
                            if (codeLock.guestPlayers.Contains(memberId))
                            {
                                codeLock.guestPlayers.Remove(memberId);
                                hasChanges = true;
                            }
                        }
                        
                        currentShares.Clear();
                    }
                    
                    m_MemberShareBuffer.Clear();
                    m_TempShareBuffer.Clear();

                    if (CanShare(shareType))
                    {
                        StoredData.PlayerData playerData = storedData.FindPlayerData(codeLock.OwnerID);
                        if (playerData != null)
                        {
                            if (Configuration.Sharing.Clan.Enabled && playerData.IsSharing(TeamType.Clan, shareType))
                                GetClanMembers(codeLock.OwnerID, ref m_MemberShareBuffer);

                            if (Configuration.Sharing.Friend.Enabled && playerData.IsSharing(TeamType.Friend, shareType))
                                GetFriends(codeLock.OwnerID, ref m_MemberShareBuffer);

                            if (Configuration.Sharing.Team.Enabled && playerData.IsSharing(TeamType.Team, shareType))
                                GetTeamMembers(codeLock.OwnerID, ref m_MemberShareBuffer);
                        }

                        foreach (ulong memberId in m_MemberShareBuffer)
                        {
                            if (m_TempShareBuffer.Contains(memberId) || memberId == codeLock.OwnerID || codeLock.whitelistPlayers.Contains(memberId))
                                continue;

                            if (!codeLock.guestPlayers.Contains(memberId))
                            {
                                codeLock.guestPlayers.Add(memberId);
                                hasChanges = true;
                            }
                                
                            m_TempShareBuffer.Add(memberId);
                        }

                        if (currentShares == null)
                            currentShares = m_CodeLockShares[codeLock] = Facepunch.Pool.GetList<ulong>();

                        if (m_TempShareBuffer.Count > 0)
                        {
                            for (int i = 0; i < m_TempShareBuffer.Count; i++)
                            {
                                if (!currentShares.Contains(m_TempShareBuffer[i]))
                                    currentShares.Add(m_TempShareBuffer[i]);
                            }
                        }
                    }
                    
                    if (hasChanges)
                        codeLock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
            }

            public static void OnEntityDestroyed(CodeLock codeLock)
            {
                List<ulong> currentShares;
                if (m_CodeLockShares.TryGetValue(codeLock, out currentShares))
                {
                    Facepunch.Pool.FreeList(ref currentShares);
                    m_CodeLockShares.Remove(codeLock);
                }
            }
            #endregion
            
            #region Get Members
            private static void GetClanMembers(ulong playerId, ref List<ulong> list)
            {
                if (!Clans.IsLoaded)
                    return;
            
                string tag = Clans.GetClanOf(playerId);
                if (string.IsNullOrEmpty(tag))
                    return;

                JObject clan = Clans.GetClan(tag);
                if (clan == null)
                    return;

                JArray members = clan["members"] as JArray;

                for (int i = 0; i < members?.Count; i++)
                    list.Add((ulong)members[i]);
            }

            private static void GetFriends(ulong playerId, ref List<ulong> list)
            {
                if (!Friends.IsLoaded)
                    return;
            
                ulong[] array = Friends.GetFriends(playerId);
                
                for (int i = 0; i < array?.Length; i++)
                    list.Add(array[i]);
            }

            private static void GetTeamMembers(ulong playerId, ref List<ulong> list)
            {            
                RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);
                if (playerTeam == null)
                    return;

                for (int i = 0; i < playerTeam.members.Count; i++)
                    list.Add(playerTeam.members[i]);
            }
            #endregion

            public static void Save()
            {
                TemporaryShareData temporaryShareData = new TemporaryShareData();

                foreach (KeyValuePair<BuildingPrivlidge, List<ulong>> kvp in m_BuildingPrivilegeShares)
                {
                    if (kvp.Key && kvp.Key.net != null && kvp.Value?.Count > 0)
                        temporaryShareData.temporaryCupboardShares.Add(kvp.Key.net.ID.Value, kvp.Value);
                }
                
                foreach (KeyValuePair<AutoTurret, List<ulong>> kvp in m_AutoTurretShares)
                {
                    if (kvp.Key && kvp.Key.net != null && kvp.Value?.Count > 0)
                        temporaryShareData.temporaryTurretShares.Add(kvp.Key.net.ID.Value, kvp.Value);
                }
                
                foreach (KeyValuePair<CodeLock, List<ulong>> kvp in m_CodeLockShares)
                {
                    if (kvp.Key && kvp.Key.net != null && kvp.Value?.Count > 0)
                        temporaryShareData.temporaryCodeLockShare.Add(kvp.Key.net.ID.Value, kvp.Value);
                }

                Interface.Oxide.DataFileSystem.WriteObject("DynamicCupShare/temporary_shares", temporaryShareData);
            }
            
            public static void OnUnload()
            {
                foreach (KeyValuePair<BuildingPrivlidge, List<ulong>> kvp in m_BuildingPrivilegeShares)
                {
                    List<ulong> list = kvp.Value;
                    Facepunch.Pool.FreeList(ref list);
                }
                
                foreach (KeyValuePair<AutoTurret, List<ulong>> kvp in m_AutoTurretShares)
                {
                    List<ulong> list = kvp.Value;
                    Facepunch.Pool.FreeList(ref list);
                }
                
                foreach (KeyValuePair<CodeLock, List<ulong>> kvp in m_CodeLockShares)
                {
                    List<ulong> list = kvp.Value;
                    Facepunch.Pool.FreeList(ref list);
                }
                
                m_BuildingPrivilegeShares.Clear();
                m_AutoTurretShares.Clear();
                m_CodeLockShares.Clear();
            }
        }
        #endregion
        
        #region Lockable Containers
        private Hash<string, StorageContainer> m_CustomLockableReferences = new Hash<string, StorageContainer>();

        private void SetupLockableContainers()
        {
            SetupLockableContainer("assets/prefabs/deployable/composter/composter.prefab", Configuration.Sharing.Allowed.Composters);
            SetupLockableContainer("assets/prefabs/deployable/dropbox/dropbox.deployed.prefab", Configuration.Sharing.Allowed.DropBoxes);
            SetupLockableContainer("assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab", Configuration.Sharing.Allowed.VendingMachines);
            SetupLockableContainer("assets/prefabs/deployable/furnace/furnace.prefab", Configuration.Sharing.Allowed.Furnace);
            SetupLockableContainer("assets/prefabs/deployable/furnace.large/furnace.large.prefab", Configuration.Sharing.Allowed.Furnace);
            SetupLockableContainer("assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab", Configuration.Sharing.Allowed.Refinery);
            SetupLockableContainer("assets/prefabs/deployable/bbq/bbq.deployed.prefab", Configuration.Sharing.Allowed.Bbq);
            SetupLockableContainer("assets/prefabs/deployable/planters/planter.small.deployed.prefab", Configuration.Sharing.Allowed.Planters);
            SetupLockableContainer("assets/prefabs/deployable/planters/planter.large.deployed.prefab", Configuration.Sharing.Allowed.Planters);
            SetupLockableContainer("assets/prefabs/deployable/hitch & trough/hitchtrough.deployed.prefab", Configuration.Sharing.Allowed.Hitch);
            SetupLockableContainer("assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab", Configuration.Sharing.Allowed.MixingTable);
            
            foreach (BaseNetworkable baseNetworkable in BaseNetworkable.serverEntities)
            {
                if (!(baseNetworkable is StorageContainer))
                    continue;
                
                StorageContainer storageContainer;
                if (m_CustomLockableReferences.TryGetValue(baseNetworkable.PrefabName, out storageContainer))
                    (baseNetworkable as StorageContainer).isLockable = storageContainer.isLockable;
            }
        }

        private void SetupLockableContainer(string prefabPath, bool enabled)
        {
            StorageContainer storageContainer = GameManager.server.FindPrefab(prefabPath).GetComponent<StorageContainer>();
            if (storageContainer)
            {
                storageContainer.isLockable = enabled;
                m_CustomLockableReferences[prefabPath] = storageContainer;
            }
        }

        private void ResetLockableContainerPrefabs()
        {
            foreach (StorageContainer storageContainer in m_CustomLockableReferences.Values)
            {
                if (storageContainer)
                    storageContainer.isLockable = false;
            }
        }

        private void SetLockPositionRotation(StorageContainer storageContainer, BaseLock baseLock)
        {
            if (storageContainer is HitchTrough)
            {
                baseLock.transform.localPosition = new Vector3(0.79f, 0.73f, -0.32f);
                baseLock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                return;
            }

            if (storageContainer is Composter)
            {
                baseLock.transform.localPosition = new Vector3(0f, 0.75f, 0.59f);
                baseLock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                return;
            }

            if (storageContainer is PlanterBox)
            {
                if (storageContainer.ShortPrefabName.Equals("planter.small.deployed"))
                {
                    baseLock.transform.localPosition = new Vector3(0f, 0.3f, 0.55f);
                    baseLock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    return;
                }

                if (storageContainer.ShortPrefabName.Equals("planter.large.deployed"))
                {
                    baseLock.transform.localPosition = new Vector3(0f, 0.3f, 1.47f);
                    baseLock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    return;
                }
            }

            if (storageContainer is MixingTable)
            {
                baseLock.transform.localPosition = new Vector3(-0.27f, 0.685f, 0.38f);
                baseLock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                return;
            }

            if (storageContainer is BaseOven)
            {
                if (storageContainer.ShortPrefabName.Equals("furnace"))
                {
                    baseLock.transform.localPosition = new Vector3(-0.035f, 0.375f, 0.45f);
                    baseLock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    return;
                }

                if (storageContainer.ShortPrefabName.Equals("furnace.large"))
                {
                    baseLock.transform.localPosition = new Vector3(-0.93f, 0.56f, 0.93f);
                    baseLock.transform.localRotation = Quaternion.Euler(0, 45, 0);
                    return;
                }

                if (storageContainer.ShortPrefabName.Equals("refinery_small_deployed"))
                {
                    baseLock.transform.localPosition = new Vector3(-0.01f, 1.25f, -0.6f);
                    baseLock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    return;
                }

                if (storageContainer.ShortPrefabName.Equals("bbq.deployed"))
                {
                    baseLock.transform.localPosition = new Vector3(0.25f, 0.545f, 0f);
                    baseLock.transform.localRotation = Quaternion.identity;
                    return;
                }
            }
        }

        #endregion

        #region Functions
        private static ShareType GetShareTypeFromEntity(BaseEntity entity)
        {
            if (entity is Door)
                return ShareType.Door;

            if (entity is BoxStorage)
                return ShareType.Box;

            if (entity is Locker)
                return ShareType.Locker;

            if (entity is BuildingPrivlidge)
                return ShareType.Cupboard;

            if (entity is Composter)
                return ShareType.Composter;

            if (entity is DropBox)
                return ShareType.Dropbox;

            if (entity is VendingMachine)
                return ShareType.VendingMachine;

            if (entity is BaseOven)
            {
                if (entity.ShortPrefabName.Equals("furnace") || entity.ShortPrefabName.Equals("furnace.large"))
                    return ShareType.Furnace;

                if (entity.ShortPrefabName.Equals("refinery_small_deployed"))
                    return ShareType.Refinery;

                if (entity.ShortPrefabName.Equals("bbq.deployed"))
                    return ShareType.Bbq;
            }

            if (entity is PlanterBox)
                return ShareType.Planters;
            
            if (entity is HitchTrough)
                return ShareType.Hitch;

            if (entity is MixingTable)
                return ShareType.MixingTable;

            return ShareType.None;
        }
        
        private void PurgeOldData()
        {
            List<ulong> purgeList = Facepunch.Pool.GetList<ulong>();

            int currentTimeStamp = UnixTimeStampUtc();

            foreach (KeyValuePair<ulong, StoredData.PlayerData> kvp in storedData.playerData)
            {
                if (currentTimeStamp - kvp.Value.lastOnline > (Configuration.Data.PurgeAfter * 86400))
                    purgeList.Add(kvp.Key);
            }

            for (int i = 0; i < purgeList.Count; i++)
                storedData.playerData.Remove(purgeList[i]);

            Facepunch.Pool.FreeList(ref purgeList);
        }

        private void FindRegisterEntities()
		{
			TemporaryShareData temporaryShareData = Interface.Oxide.DataFileSystem.GetFile("DynamicCupShare/temporary_shares").ReadObject<TemporaryShareData>();

			List<ulong> tempShareData = null;

			foreach (BaseNetworkable baseNetworkable in BaseNetworkable.serverEntities)
			{
				if (baseNetworkable is BuildingPrivlidge buildingPrivlidge)
				{
					if (temporaryShareData.temporaryTurretShares.TryGetValue(baseNetworkable.net.ID.Value, out tempShareData))
					{
						// Create a list to hold players to remove
						List<PlayerNameID> playersToRemove = new List<PlayerNameID>();

						foreach (var playerNameID in buildingPrivlidge.authorizedPlayers)
						{
							if (tempShareData.Contains(playerNameID.userid))
								playersToRemove.Add(playerNameID);
						}

						// Remove players from the hashset
						foreach (var player in playersToRemove)
						{
							buildingPrivlidge.authorizedPlayers.Remove(player);
						}
					}

					PlayerEntities.GetOrCreate(buildingPrivlidge.OwnerID)?.AddEntity(buildingPrivlidge, true);
				}
				else if (baseNetworkable is AutoTurret autoTurret)
				{
					if (temporaryShareData.temporaryTurretShares.TryGetValue(baseNetworkable.net.ID.Value, out tempShareData))
					{
						// Create a list to hold players to remove
						List<PlayerNameID> playersToRemove = new List<PlayerNameID>();

						foreach (var playerNameID in autoTurret.authorizedPlayers)
						{
							if (tempShareData.Contains(playerNameID.userid))
								playersToRemove.Add(playerNameID);
						}

						// Remove players from the hashset
						foreach (var player in playersToRemove)
						{
							autoTurret.authorizedPlayers.Remove(player);
						}
					}

					PlayerEntities.GetOrCreate(autoTurret.OwnerID)?.AddEntity(autoTurret, true);
				}
				else if (baseNetworkable is CodeLock codeLock)
				{
					if (temporaryShareData.temporaryCodeLockShare.TryGetValue(baseNetworkable.net.ID.Value, out tempShareData))
					{
						// Create a list to hold guest players to remove
						List<ulong> guestsToRemove = new List<ulong>();

						foreach (var playerId in codeLock.guestPlayers)
						{
							if (tempShareData.Contains(playerId))
								guestsToRemove.Add(playerId);
						}

						// Remove guest players from the hashset
						foreach (var guest in guestsToRemove)
						{
							codeLock.guestPlayers.Remove(guest);
						}
					}

					PlayerEntities.GetOrCreate(codeLock.OwnerID)?.AddEntity(codeLock, true);
				}
			}
		}
          
        private bool CanUseLockedObject(BasePlayer player, BaseEntity entity)
        {
            if (!entity)
                return false;

            ShareType shareType = GetShareTypeFromEntity(entity);

            if (!CanShare(shareType))
                return false;

            StoredData.PlayerData data = storedData.FindPlayerData(entity.OwnerID);
            if (data == null)
                return false;
            
            if (CanShare(TeamType.Clan, entity.OwnerID) && data.IsSharing(TeamType.Clan, shareType) && AreClanMates(entity.OwnerID, player.userID))
                return true;

            if (CanShare(TeamType.Friend, entity.OwnerID) && data.IsSharing(TeamType.Friend, shareType) && AreFriends(entity.OwnerID, player.userID))
                return true;

            if (CanShare(TeamType.Team, entity.OwnerID) && data.IsSharing(TeamType.Team, shareType) && AreTeamMates(entity.OwnerID, player.userID))
                return true;

            return false;
        }

        private bool CanUseTurret(BasePlayer player, BaseEntity entity)
        {
            if (!player || !entity)
                return false;

            StoredData.PlayerData data = storedData.FindPlayerData(entity.OwnerID);
            if (data == null)
                return false;

            if (!CanShare(ShareType.Turret) && !Configuration.Security.TurretShareOverride)
                return false;

            if (CanShare(TeamType.Clan, entity.OwnerID) && (data.IsSharing(TeamType.Clan, ShareType.Turret) || Configuration.Security.TurretShareOverride) && AreClanMates(entity.OwnerID, player.userID))
                return true;

            if (CanShare(TeamType.Friend, entity.OwnerID) && (data.IsSharing(TeamType.Friend, ShareType.Turret) || Configuration.Security.TurretShareOverride) && AreFriends(entity.OwnerID, player.userID))
                return true;

            if (CanShare(TeamType.Team, entity.OwnerID) && (data.IsSharing(TeamType.Team, ShareType.Turret) || Configuration.Security.TurretShareOverride) && AreTeamMates(entity.OwnerID, player.userID))
                return true;

            return false;
        }
        #endregion

        #region Helpers
        private static bool CanShare(TeamType teamType, ulong playerId)
        {
            switch (teamType)
            {
                case TeamType.Clan:
                    return Clans.IsLoaded && Configuration.Sharing.Clan.Enabled && (!Configuration.Permission.ClanShare.Enabled || playerId.HasPermission(Configuration.Permission.ClanShare.Permission));
                case TeamType.Friend:
                    return Friends.IsLoaded && Configuration.Sharing.Friend.Enabled && (!Configuration.Permission.FriendShare.Enabled || playerId.HasPermission(Configuration.Permission.FriendShare.Permission));
                case TeamType.Team:
                    return RelationshipManager.maxTeamSize > 0 && Configuration.Sharing.Team.Enabled && (!Configuration.Permission.TeamShare.Enabled || playerId.HasPermission(Configuration.Permission.TeamShare.Permission));               
            }

            return false;
        }

        private static bool CanShare(ShareType shareType) => allowedShareTypes.Contains(shareType);

        private bool InAdminMode(BasePlayer player) => PlayerPrivilege.IsAdmin(player);

        private static bool AreClanMates(ulong owner, ulong player)
        {
            if (!Clans.IsLoaded)
                return false;
            
            if (Configuration.Sharing.Clan.Alliances) 
                return Clans.IsMemberOrAlly(owner, player);
                    
            return Clans.IsClanMember(owner, player);
        }

        private static bool AreTeamMates(ulong owner, ulong player) => RelationshipManager.ServerInstance.FindPlayersTeam(owner)?.members?.Contains(player) ?? false;

        private static bool AreFriends(ulong owner, ulong player)
        {
            if (!Friends.IsLoaded)
                return false;
            
            return Friends.HasFriend(owner, player);
        }

        private int UnixTimeStampUtc() => (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
        #endregion
        
        #region Plugin Hooks
        private void OnFriendAdded(string playerId, string friendId) => OnFriendRemoved(playerId, friendId);

        private void OnFriendRemoved(string playerId, string friendId)
        {
            if (!Configuration.Sharing.Friend.Enabled)
                return;

            PlayerEntities.Get(ulong.Parse(playerId))?.RebuildAll();
        }

        private void OnClanMemberJoined(ulong playerId, List<ulong> clanMembers) => OnClanMemberGone(playerId, clanMembers);

        private void OnClanMemberGone(ulong playerId, List<ulong> clanMembers)
        {
            if (!Configuration.Sharing.Clan.Enabled)
                return;

            PlayerEntities.Get(playerId)?.RebuildAll();
            clanMembers.ForEach((ulong memberId) => PlayerEntities.Get(memberId)?.RebuildAll());
        }

        private void OnClanDisbanded(List<ulong> clanMembers)
        {
            if (!Configuration.Sharing.Clan.Enabled)
                return;

            clanMembers.ForEach((ulong playerId) => PlayerEntities.Get(playerId)?.RebuildAll());
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam playerTeam, BasePlayer player) => OnTeamLeave(playerTeam, player);

        private void OnTeamLeave(RelationshipManager.PlayerTeam playerTeam, BasePlayer player)
        {
            if (!Configuration.Sharing.Team.Enabled)
                return;

            playerTeam.members.ForEach((ulong playerId) => PlayerEntities.Get(playerId)?.RebuildAll());
        }

        private void OnTeamDisband(RelationshipManager.PlayerTeam playerTeam)
        {
            if (!Configuration.Sharing.Team.Enabled)
                return;

            playerTeam.members.ForEach((ulong playerId) => PlayerEntities.Get(playerId)?.RebuildAll());
        }
        #endregion

        #region Flags
        internal enum TeamType { Clan, Friend, Team }

        [Flags]
        internal enum ShareType
        {
            None = 0,
            Cupboard = 1 << 0,
            Door = 1 << 1,
            Box = 1 << 2,
            Locker = 1 << 3,
            Turret = 1 << 4,
            Furnace = 1 << 5,
            Bbq = 1 << 6,
            Refinery = 1 << 7,
            Composter = 1 << 8,
            Planters = 1 << 9,
            Dropbox = 1 << 10,
            VendingMachine = 1 << 11,
            MixingTable = 1 << 12,
            Hitch = 1 << 13,
        }
        #endregion

        #region UI Creation
        private const string UI_MENU = "dcs.menu";

        private void cmdShare(BasePlayer player, string command, string[] args)
        {
            if (allowedShareTypes.Count == 0)
                return;

            if (!CanShare(TeamType.Clan, player.userID) && !CanShare(TeamType.Friend, player.userID) && !CanShare(TeamType.Team, player.userID))
            {
                player.LocalizedMessage(this, "Error.NoPermissions");
                return;
            }

            StoredData.PlayerData playerData = storedData.SetupPlayer(player.userID);

            OpenShareMenu(player, playerData, 0UL);
        }
        
        [ChatCommand("shareplayer")]
        private void cmdSharePlayer(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            if (args.Length != 1)
            {
                player.ChatMessage("/shareplayer <steamID>");
                return;
            }

            ulong target;
            if (!ulong.TryParse(args[0], out target))
            {
                player.ChatMessage("Invalid Steam ID entered");
                return;
            }
            
            StoredData.PlayerData playerData = storedData.SetupPlayer(target);
            if (playerData == null)
            {
                player.ChatMessage("Failed to get or create data for the target user ID");
                return;
            }

            OpenShareMenu(player, playerData, target);
        }

        [ChatCommand("dcsadmin")]
        private void cmdDCSAdmin(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(Configuration.Permission.AdminPermission))
            {
                player.LocalizedMessage(this, "Error.NoPermissions");
                return;
            }

            PlayerPrivilege playerPrivilege;
            if (PlayerPrivilege.Find(player, out playerPrivilege))
            {
                if (playerPrivilege.InAdminMode)
                {
                    playerPrivilege.InAdminMode = false;
                    player.LocalizedMessage(this, "Message.AdminDisabled");
                }
                else
                {
                    playerPrivilege.InAdminMode = true;
                    player.LocalizedMessage(this, "Message.AdminEnabled");
                }
            }
        }
        
        private Style m_BackgroundStyle;
        private Style m_PanelStyle;
        private Style m_ButtonStyle;
        private Style m_TitleStyle;
        private Style m_CloseStyle;
        
        private OutlineComponent m_OutlineGreen;
        private OutlineComponent m_OutlineRed;

        private CommandCallbackHandler m_CallbackHandler;

        private void SetupUIComponents()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);

            m_BackgroundStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.Background.Hex, Configuration.Colors.Background.Alpha),
                Material = Materials.BackgroundBlur,
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled
            };

            m_PanelStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.Panel.Hex, Configuration.Colors.Panel.Alpha),
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled
            };

            m_ButtonStyle = new Style
            {
                ImageColor = new Color(Configuration.Colors.Button.Hex, Configuration.Colors.Button.Alpha),
                Sprite = Sprites.Background_Rounded,
                ImageType = Image.Type.Tiled,
                Alignment = TextAnchor.MiddleCenter,
                FontSize = 14
            };
            
            m_TitleStyle = new Style
            {
                FontSize = 18,
                Font = Font.PermanentMarker,
                Alignment = TextAnchor.MiddleLeft,
                WrapMode = VerticalWrapMode.Overflow
            };
            
            m_CloseStyle = new Style
            {
                FontSize = 18,
                Alignment = TextAnchor.MiddleCenter,
                WrapMode = VerticalWrapMode.Overflow,
            };

            m_OutlineGreen = new OutlineComponent(new Color(Configuration.Colors.Highlight.Hex, Configuration.Colors.Highlight.Alpha));
            m_OutlineRed = new OutlineComponent(new Color(Configuration.Colors.Close.Hex, Configuration.Colors.Close.Alpha));
        }
        
        private void OpenShareMenu(BasePlayer player, StoredData.PlayerData playerData, ulong shareTarget)
        {
            List<TeamType> list = Facepunch.Pool.GetList<TeamType>();
            
            if (CanShare(TeamType.Clan, shareTarget != 0UL ? shareTarget : player.userID))
                list.Add(TeamType.Clan);

            if (CanShare(TeamType.Friend, shareTarget != 0UL ? shareTarget : player.userID))
                list.Add(TeamType.Friend);

            if (CanShare(TeamType.Team, shareTarget != 0UL ? shareTarget : player.userID))
                list.Add(TeamType.Team);

            float width = Mathf.Max(250, list.Count * 130);
            float halfWidth = width * 0.5f;
            
            float height = 40 + (25 * (allowedShareTypes.Count + 1)) + 5;
            float halfHeight = height * 0.5f;

            float shareWidth = width / (float)list.Count;

            BaseContainer root = ImageContainer.Create(UI_MENU, Layer.Overall, Anchor.Center, new Offset(-halfWidth, -halfHeight, halfWidth, halfHeight))
                .WithStyle(m_BackgroundStyle)
                .NeedsCursor()
                .DestroyExisting()
                .WithChildren(parent =>
                {
                    BaseContainer.Create(parent, Anchor.TopStretch, new Offset(5, -35, -5, -5))
                        .WithChildren(title =>
                        {
                            ImageContainer.Create(title, Anchor.FullStretch, Offset.zero)
                                .WithStyle(m_PanelStyle);
                            
                            TextContainer.Create(title, Anchor.FullStretch, new Offset(5, 0 ,0,0))
                                .WithText(GetString("UI.Title", player) + (shareTarget == 0UL ? "" : $" <size=8>({shareTarget})</size>"))
                                .WithStyle(m_TitleStyle);

                            ImageContainer.Create(title, Anchor.CenterRight, new Offset(-25, -10, -5, 10))
                                .WithStyle(m_ButtonStyle)
                                .WithOutline(m_OutlineRed)
                                .WithChildren(close =>
                                {
                                    TextContainer.Create(close, Anchor.FullStretch, Offset.zero)
                                        .WithText("<b>×</b>")
                                        .WithStyle(m_CloseStyle);

                                    ButtonContainer.Create(close, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => ChaosUI.Destroy(player, UI_MENU), $"{player.UserIDString}.close");
                                });
                        });
                    
                    for (int i = 0; i < list.Count; i++)
                    {
                        TeamType teamType = list[i];
                
                        Offset offset = new Offset(-halfWidth + (shareWidth * i), -halfHeight, -halfWidth + (shareWidth * (i + 1)), halfHeight - 40);

                        BaseContainer.Create(parent, Anchor.Center, offset)
                            .WithChildren(column =>
                            {
                                ImageContainer.Create(column, Anchor.FullStretch, new Offset(list.Count > 1 && i > 0 ? 2.5f : 5, 5, i < list.Count - 1 ? -2.5f : -5, 0))
                                    .WithStyle(m_PanelStyle)
                                    .WithChildren(type =>
                                    {
                                        TextContainer.Create(type, Anchor.TopStretch, new Offset(0, -25, 0, 0))
                                            .WithText(GetString($"UI.Share.{teamType}", player))
                                            .WithStyle(m_ButtonStyle);
                                        
                                        for (int j = 0; j < allowedShareTypes.Count; j++)
                                        {
                                            ShareType shareType = allowedShareTypes[j];

                                            int index = j + 1;

                                            bool isSharing = playerData.IsSharing(teamType, shareType);

                                            BaseContainer button = ImageContainer.Create(type, Anchor.TopStretch, new Offset(5, -((20 * (index + 1)) + (5 * index)), -5, -(25 * index)))
                                                .WithStyle(m_ButtonStyle);

                                            TextContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                                .WithText(GetString($"UI.Type.{shareType}", player))
                                                .WithStyle(m_ButtonStyle);

                                            ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                                .WithColor(Color.Clear)
                                                .WithCallback(m_CallbackHandler, arg =>
                                                {
                                                    if (!isSharing)
                                                        playerData.Share(teamType, shareType);
                                                    else
                                                    {
                                                        if (shareType == ShareType.Turret && Configuration.Security.TurretShareOverride)
                                                        {
                                                            player.LocalizedMessage(this, "Chat.NoTurretToggle");
                                                            return;
                                                        }

                                                        playerData.Unshare(teamType, shareType);
                                                    }

                                                    PlayerEntities.GetOrCreate(shareTarget != 0UL ? shareTarget : player.userID)?.OnToggleShareType(shareType);
                                                    
                                                    player.ChatMessage(string.Format(!isSharing ? GetString("Chat.ShareEnabled", player) : GetString("Chat.ShareDisabled", player), shareType, teamType));

                                                    OpenShareMenu(player, playerData, shareTarget);
                                                }, $"{player.UserIDString}.{teamType}.{shareType}");

                                            if (isSharing)
                                                button.WithOutline(m_OutlineGreen);
                                        }
                                    });
                            });
                    }
                });

            Facepunch.Pool.FreeList(ref list);

            ChaosUI.Show(player, root);
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;
        
        private class ConfigData : BaseConfigData
        {
            [JsonProperty(PropertyName = "Sharing Options")]
            public ShareDefaults Sharing { get; set; }

            [JsonProperty(PropertyName = "Permission Options")]
            public Permissions Permission { get; set; }

            [JsonProperty(PropertyName = "Turret Share Options")]
            public TurretTargeting Turrets { get; set; }

            [JsonProperty(PropertyName = "Building Restrictions")]
            public BuildBlocker Building { get; set; }

            [JsonProperty(PropertyName = "Security Options")]
            public SecurityOptions Security { get; set; }
            
            [JsonProperty(PropertyName = "Data Management")]
            public DataManagement Data { get; set; }

            [JsonProperty(PropertyName = "UI Colors")]
            public UIColors Colors { get; set; }

            public class ShareDefaults
            {
                [JsonProperty(PropertyName = "Allowed share types")]
                public AllowedShares Allowed { get; set; }

                public ClanDefaults Clan { get; set; }
                public Defaults Friend { get; set; }
                public Defaults Team { get; set; }
                
                [JsonProperty(PropertyName = "Chat command")]
                public string ChatCommand { get; set; }
                
                [JsonProperty(PropertyName = "Disable key lock sharing")]
                public bool DisableKeylocks { get; set; }

                public class AllowedShares
                {
                    [JsonProperty(PropertyName = "Allow cupboard sharing")]
                    public bool Cupboards { get; set; }

                    [JsonProperty(PropertyName = "Allow door sharing")]
                    public bool Doors { get; set; }

                    [JsonProperty(PropertyName = "Allow box sharing")]
                    public bool Boxes { get; set; }

                    [JsonProperty(PropertyName = "Allow locker sharing")]
                    public bool Lockers { get; set; }

                    [JsonProperty(PropertyName = "Allow turret sharing")]
                    public bool Turrets { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow composter sharing (also enables locks to be placed on composters)")]
                    public bool Composters { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow dropbox sharing (also enables locks to be placed on dropboxes)")]
                    public bool DropBoxes { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow vending machine sharing (also enables locks to be placed on vending machines)")]
                    public bool VendingMachines { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow furnace sharing (also enables locks to be placed on furnaces)")]
                    public bool Furnace { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow bbq sharing (also enables locks to be placed on bbqs)")]
                    public bool Bbq { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow refinery sharing (also enables locks to be placed on refinery)")]
                    public bool Refinery { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow planter sharing (also enables locks to be placed on planters)")]
                    public bool Planters { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow hitch and trough sharing (also enables locks to be placed on hitch and troughs)")]
                    public bool Hitch { get; set; }
                    
                    [JsonProperty(PropertyName = "Allow mixing table sharing (also enables locks to be placed on mixing table)")]
                    public bool MixingTable { get; set; }
                    

                    [JsonIgnore]
                    public List<ShareType> AllowedShareTypes
                    {
                        get
                        {
                            List<ShareType> list = new List<ShareType>();

                            if (Boxes)
                                list.Add(ShareType.Box);

                            if (Cupboards)
                                list.Add(ShareType.Cupboard);

                            if (Doors)
                                list.Add(ShareType.Door);
                            
                            if (Lockers)
                                list.Add(ShareType.Locker);

                            if (Turrets)
                                list.Add(ShareType.Turret);

                            if (Furnace)
                                list.Add(ShareType.Furnace);
                            
                            if (Refinery)
                                list.Add(ShareType.Refinery);
                            
                            if (Bbq)
                                list.Add(ShareType.Bbq);
                            
                            if (Composters)
                                list.Add(ShareType.Composter);
                            
                            if (Planters)
                                list.Add(ShareType.Planters);
                            
                            if (DropBoxes)
                                list.Add(ShareType.Dropbox);

                            if (VendingMachines)
                                list.Add(ShareType.VendingMachine);

                            if (Hitch)
                                list.Add(ShareType.Hitch);
                            
                            if (MixingTable)
                                list.Add(ShareType.MixingTable);
                            
                            return list;
                        }
                    }
                }

                public class ClanDefaults : Defaults
                {
                    [JsonProperty(PropertyName = "Clan sharing includes alliances?")]
                    public bool Alliances { get; set; }
                }

                public class Defaults
                {
                    [JsonProperty(PropertyName = "Is this share type allowed?")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Enable cupboard sharing by default")]
                    public bool Cupboards { get; set; }

                    [JsonProperty(PropertyName = "Enable door sharing by default")]
                    public bool Doors { get; set; }

                    [JsonProperty(PropertyName = "Enable box sharing by default")]
                    public bool Boxes { get; set; }

                    [JsonProperty(PropertyName = "Enable locker sharing by default")]
                    public bool Lockers { get; set; }

                    [JsonProperty(PropertyName = "Enable turret sharing by default")]
                    public bool Turrets { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable furnace sharing by default")]
                    public bool Furnace { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable refinery sharing by default")]
                    public bool Refinery { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable bbq sharing by default")]
                    public bool Bbq { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable composter sharing by default")]
                    public bool Composters { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable planter sharing by default")]
                    public bool Planters { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable dropbox sharing by default")]
                    public bool DropBoxes { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable vending machine sharing by default")]
                    public bool VendingMachines { get; set; }

                    [JsonProperty(PropertyName = "Enable hitch and trough sharing by default")]
                    public bool Hitch { get; set; }
                    
                    [JsonProperty(PropertyName = "Enable mixing table sharing by default")]
                    public bool MixingTable { get; set; }
                    
                    [JsonIgnore]
                    public ShareType ShareType
                    {
                        get
                        {
                            ShareType type = ShareType.None;

                            if (Cupboards && Configuration.Sharing.Allowed.Cupboards)
                                type |= ShareType.Cupboard;

                            if (Doors && Configuration.Sharing.Allowed.Doors)
                                type |= ShareType.Door;

                            if (Boxes && Configuration.Sharing.Allowed.Boxes)
                                type |= ShareType.Box;

                            if (Lockers && Configuration.Sharing.Allowed.Lockers)
                                type |= ShareType.Locker;

                            if ((Turrets && Configuration.Sharing.Allowed.Turrets) || Configuration.Security.TurretShareOverride)
                                type |= ShareType.Turret;

                            if (Composters && Configuration.Sharing.Allowed.Composters)
                                type |= ShareType.Composter;

                            if (DropBoxes && Configuration.Sharing.Allowed.DropBoxes)
                                type |= ShareType.Dropbox;

                            if (VendingMachines && Configuration.Sharing.Allowed.VendingMachines)
                                type |= ShareType.VendingMachine;

                            if (Furnace && Configuration.Sharing.Allowed.Furnace)
                                type |= ShareType.Furnace;
                            
                            if (Refinery && Configuration.Sharing.Allowed.Refinery)
                                type |= ShareType.Refinery;
                            
                            if (Bbq && Configuration.Sharing.Allowed.Bbq)
                                type |= ShareType.Bbq;

                            if (Planters && Configuration.Sharing.Allowed.Planters)
                                type |= ShareType.Planters;

                            if (Hitch && Configuration.Sharing.Allowed.Hitch)
                                type |= ShareType.Hitch;

                            if (MixingTable && Configuration.Sharing.Allowed.MixingTable)
                                type |= ShareType.MixingTable;

                            return type;
                        }
                    }
                }
            }

            public class Permissions
            {
                [JsonProperty(PropertyName = "Clan Share Permission (if enabled, players will need this permission to use Clan share)")]
                public TogglablePermission  ClanShare { get; set; }

                [JsonProperty(PropertyName = "Friend Share Permission (if enabled, players will need this permission to use Friend share)")]
                public TogglablePermission FriendShare { get; set; }

                [JsonProperty(PropertyName = "Team Share Permission (if enabled, players will need this permission to use Team share)")]
                public TogglablePermission TeamShare { get; set; }
                
                [JsonProperty(PropertyName = "Admin Permission (required to toggle admin mode)")]
                public string AdminPermission { get; set; }
                
                [JsonProperty(PropertyName = "Toggle admin mode when player connects (requires the admin permission)")]
                public bool ToggleAdminPermissionOnJoin { get; set; }

                public struct TogglablePermission
                {
                    public string Permission { get; set; }
                    public bool Enabled { get; set; }

                    public TogglablePermission(string permission, bool enabled)
                    {
                        this.Permission = permission;
                        this.Enabled = enabled;
                    }
                }
            }

            public class TurretTargeting
            {
                [JsonProperty(PropertyName = "Turret share includes gun traps")]
                public bool IncludeGunTraps { get; set; }

                [JsonProperty(PropertyName = "Turret share includes flame turrets")]
                public bool IncludeFlameTurrets { get; set; }

                [JsonProperty(PropertyName = "Turret share includes sam sites")]
                public bool IncludeSameSites { get; set; }
            }

            public class DataManagement
            {
                [JsonProperty(PropertyName = "Save data in ProtoBuf format")]
                public bool UseProtoStorage { get; set; }

                [JsonProperty(PropertyName = "Purge user data after X days of inactivity (0 is disabled)")]
                public int PurgeAfter { get; set; }
            }

            public class BuildBlocker
            {
                [JsonProperty(PropertyName = "Prevent building on icebergs")]
                public bool PreventIceberg { get; set; }

                [JsonProperty(PropertyName = "Prevent building on ice sheets")]
                public bool PreventIcesheet { get; set; }

                [JsonProperty(PropertyName = "Prevent building on ice lakes")]
                public bool PreventIcelake { get; set; }
            }

            public class SecurityOptions
            {
                [JsonProperty(PropertyName = "Permanently enable turret sharing between clan and team members")]
                public bool TurretShareOverride { get; set; }

                [JsonProperty(PropertyName = "Prevent friendly players from accessing cupboard and turret auth lists")]
                public bool BlockAuth { get; set; }

                [JsonProperty(PropertyName = "Prevent non-friendly players from accessing cupboard and turret auth lists")]
                public bool BlockNonAuth { get; set; }
                
                [JsonProperty(PropertyName = "Prevent cupboard and turret sharing if owner is not in the authorized list of that entity")]
                public bool PreventShareNoOwner { get; set; }

                [JsonProperty(PropertyName = "Maximum allowed authorizations on a tool cupboard (0 = disabled)")]
                public int MaxCupboardAuth { get; set; }
                
                [JsonProperty(PropertyName = "Allow friendly players to lock/unlock shared locks")]
                public bool ShareLockUnlock { get; set; }
            }

            public class UIColors
            {                
                public Color Background { get; set; }

                public Color Panel { get; set; }
                
                public Color Button { get; set; }

                public Color Highlight { get; set; }

                public Color Close { get; set; }
                
                public class Color
                {
                    public string Hex { get; set; }

                    public float Alpha { get; set; }
                }
            }
        }

        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();

            if (oldVersion < new VersionNumber(3, 0, 0))
                ConfigurationData = baseConfigData;
            
            if (oldVersion < new VersionNumber(3, 0, 1))
                (ConfigurationData as ConfigData).Permission.AdminPermission = baseConfigData.Permission.AdminPermission;

            if (oldVersion < new VersionNumber(3, 0, 2))
                (ConfigurationData as ConfigData).Colors = baseConfigData.Colors;

            if (oldVersion < new VersionNumber(3, 0, 3))
                (ConfigurationData as ConfigData).Security.PreventShareNoOwner = true;

            if (oldVersion < new VersionNumber(3, 0, 5))
                (ConfigurationData as ConfigData).Sharing.ChatCommand = baseConfigData.Sharing.ChatCommand;
            
            Configuration = ConfigurationData as ConfigData;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = ConfigurationData as ConfigData;
        }

        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Permission = new ConfigData.Permissions
                {
                    ClanShare = new ConfigData.Permissions.TogglablePermission("dynamiccupshare.canclanshare", false),
                    FriendShare = new ConfigData.Permissions.TogglablePermission("dynamiccupshare.canfriendshare", false),
                    TeamShare = new ConfigData.Permissions.TogglablePermission("dynamiccupshare.canteamshare", false),
                    AdminPermission = "dynamiccupshare.adminmode",
                },
                Sharing = new ConfigData.ShareDefaults
                {
                    Allowed = new ConfigData.ShareDefaults.AllowedShares
                    {
                        Boxes = true,
                        Cupboards = true,
                        Doors = true,
                        Lockers = true,
                        Turrets = true,
                    },
                    Clan = new ConfigData.ShareDefaults.ClanDefaults
                    {
                        Enabled = true,
                        Boxes = true,
                        Cupboards = true,
                        Doors = true,
                        Lockers = true,
                        Turrets = true,
                        Alliances = false
                    },
                    Friend = new ConfigData.ShareDefaults.Defaults
                    {
                        Enabled = false,
                        Boxes = true,
                        Cupboards = true,
                        Doors = true,
                        Lockers = true,
                        Turrets = true
                    },
                    Team = new ConfigData.ShareDefaults.Defaults
                    {
                        Enabled = true,
                        Boxes = true,
                        Cupboards = true,
                        Doors = true,
                        Lockers = true,
                        Turrets = true
                    },
                    ChatCommand = "share",
                    DisableKeylocks = false
                },
                Building = new ConfigData.BuildBlocker
                {
                    PreventIceberg = false,
                    PreventIcelake = false,
                    PreventIcesheet = false
                },
                Turrets = new ConfigData.TurretTargeting
                {
                    IncludeFlameTurrets = true,
                    IncludeGunTraps = true,
                    IncludeSameSites = true
                },
                Security = new ConfigData.SecurityOptions
                {
                    BlockAuth = true,
                    BlockNonAuth = false,
                    TurretShareOverride = false,
                    MaxCupboardAuth = 0,
                    PreventShareNoOwner = true
                },
                Colors = new ConfigData.UIColors
                {
                    Background = new ConfigData.UIColors.Color
                    {
                        Hex = "151515",
                        Alpha = 0.94f
                    },
                    Panel = new ConfigData.UIColors.Color
                    {
                        Hex = "FFFFFF",
                        Alpha = 0.165f
                    },
                    Button = new ConfigData.UIColors.Color
                    {
                        Hex = "2A2E32",
                        Alpha = 1f
                    },
                    Highlight = new ConfigData.UIColors.Color
                    {
                        Hex = "C4FF00",
                        Alpha = 1f
                    },
                    Close = new ConfigData.UIColors.Color
                    {
                        Hex = "CE422B",
                        Alpha = 1f
                    }
                },
                Data = new ConfigData.DataManagement
                {
                    UseProtoStorage = false,
                    PurgeAfter = 7
                }
            } as T;
        }
        #endregion

        #region Data Management
        private static StoredData storedData;

        private const string DATAFILE_NAME = "DynamicCupShare/user_data";

        private void SaveData()
        {
            storedData.timeSaved = UnixTimeStampUtc();

            if (Configuration.Data.UseProtoStorage)
                ProtoStorage.Save<StoredData>(storedData, DATAFILE_NAME);
            else Interface.Oxide.DataFileSystem.WriteObject(DATAFILE_NAME, storedData);
        }

        private void LoadData()
        {            
            try
            {
                StoredData protoStorage = ProtoStorage.Exists(DATAFILE_NAME) ? ProtoStorage.Load<StoredData>(new string[] { DATAFILE_NAME }) : null;
                StoredData jsonStorage = Interface.GetMod().DataFileSystem.ExistsDatafile(DATAFILE_NAME) ? Interface.GetMod().DataFileSystem.ReadObject<StoredData>(DATAFILE_NAME) : null;

                if (protoStorage == null && jsonStorage == null)
                {
                    Puts("No data file found! Creating new data file");
                    storedData = new StoredData();
                }
                else
                {
                    if (protoStorage == null && jsonStorage != null)
                        storedData = jsonStorage;
                    else if (protoStorage != null && jsonStorage == null)
                        storedData = protoStorage;
                    else
                    {
                        if (protoStorage.timeSaved > jsonStorage.timeSaved)
                        {
                            storedData = protoStorage;
                            Puts("Multiple data files found! ProtoBuf storage time stamp is newer than JSON storage. Loading ProtoBuf data file");
                        }
                        else
                        {
                            storedData = jsonStorage;
                            Puts("Multiple data files found! JSON storage time stamp is newer than ProtoBuf storage. Loading JSON data file");
                        }
                    }
                }
            }
            catch { }

            if (storedData?.playerData == null)
                storedData = new StoredData();
        }

        [Serializable, ProtoContract]
        private class StoredData
        {
            [ProtoMember(1)]
            public Hash<ulong, PlayerData> playerData = new Hash<ulong, PlayerData>();

            [ProtoMember(2)]
            public int timeSaved;

            internal PlayerData SetupPlayer(ulong playerId)
            {
                if (playerId < 76561197960265729UL)
                    return null;

                PlayerData data;
                if (!playerData.TryGetValue(playerId, out data))                
                    playerData[playerId] = data = new PlayerData(Configuration.Sharing.Clan.ShareType, Configuration.Sharing.Friend.ShareType, Configuration.Sharing.Team.ShareType); 

                return data;
            }

            internal PlayerData FindPlayerData(ulong playerId)
            {
                PlayerData data;
                if (playerData.TryGetValue(playerId, out data))
                    return data;
                return null;
            }

            [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
            public class PlayerData
            {
                public ShareType clan = ShareType.None;
                public ShareType friend = ShareType.None;
                public ShareType team = ShareType.None;
                public int lastOnline;

                public PlayerData(){}
                
                public PlayerData(ShareType clans, ShareType friends, ShareType teams)
                {
                    clan = clans;
                    friend = friends;
                    team = teams;                    
                }

                internal bool IsSharing(TeamType type, ShareType share)
                {
                    switch (type)
                    {
                        case TeamType.Clan:                            
                            return (clan & share) == share;
                        case TeamType.Friend:
                            return (friend & share) == share;
                        case TeamType.Team:
                            return (team & share) == share;
                    }
                    return false;
                }

                internal void Share(TeamType type, ShareType share)
                {
                    switch (type)
                    {
                        case TeamType.Clan:
                            clan |= share;
                            return;
                        case TeamType.Friend:
                            friend |= share;
                            return;
                        case TeamType.Team:
                            team |= share;
                            return;
                    } 
                }

                internal void Unshare(TeamType type, ShareType share)
                {
                    switch (type)
                    {
                        case TeamType.Clan:
                            clan &= ~share;
                            return;
                        case TeamType.Friend:
                            friend &= ~share;
                            return;
                        case TeamType.Team:
                            team &= ~share;
                            return;
                    }                    
                }
            }
        }
        
        private class TemporaryShareData
        {
            public Hash<ulong, List<ulong>> temporaryCupboardShares = new Hash<ulong, List<ulong>>();
            public Hash<ulong, List<ulong>> temporaryTurretShares = new Hash<ulong, List<ulong>>();
            public Hash<ulong, List<ulong>> temporaryCodeLockShare = new Hash<ulong, List<ulong>>();

            public TemporaryShareData() { }
        }
        #endregion
    }
}
