using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.UIFramework;
using UnityEngine;
using ProtoBuf;
using UnityEngine.UI;

using Chaos = Oxide.Ext.Chaos;
using Color = Oxide.Ext.Chaos.UIFramework.Color;
using Font = Oxide.Ext.Chaos.UIFramework.Font;

namespace Oxide.Plugins
{
    [Info("AutoCodeLock", "k1lly0u", "3.0.9")]
    [Description("Codelock & door automation tools")]
    class AutoCodeLock : ChaosPlugin
    {
        #region Fields
        [Chaos.Permission] private const string PERMISSION_DEPLOY_DOOR = "autocodelock.deploydoor";    
        [Chaos.Permission] private const string PERMISSION_DEPLOY_BOX = "autocodelock.deploybox";    
        [Chaos.Permission] private const string PERMISSION_DEPLOY_LOCKER = "autocodelock.deploylocker";    
        [Chaos.Permission] private const string PERMISSION_DEPLOY_CUPBOARD = "autocodelock.deploycup";    
        [Chaos.Permission] private const string PERMISSION_AUTO_LOCK = "autocodelock.autolock";    
        [Chaos.Permission] private const string PERMISSION_NO_LOCK_NEEDED = "autocodelock.nolockneed";    
        [Chaos.Permission] private const string PERMISSION_DOOR_CLOSER = "autocodelock.doorcloser";    
        
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            cmd.AddChatCommand(Configuration.Command, this, CodelockCommand);
            cmd.AddChatCommand(Configuration.CloserCommand, this, AutoDoorCommand);
            SetupUIComponents();
            LoadData();
        }

        private void OnServerInitialized()
        {
            FindRegisterEntities();
            
            if (Configuration.Data.PurgeAfter > 0)
                PurgeOldData();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            PlayerEntities.EnqueueUpdateDoorCloserDelays();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (HasAnyPermission(player))
            {
                StoredData.PlayerData playerData = storedData.FindPlayerData(player.userID);
                if (playerData == null)
                    playerData = storedData.SetupPlayer(player.userID, Configuration);
                
                playerData.UpdateDelays(Configuration.Delay);
                playerData.SetLastOnline(UnixTimeStampUtc());
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            StoredData.PlayerData playerData = storedData.FindPlayerData(player.userID);
            if (playerData != null)
                playerData.SetLastOnline(UnixTimeStampUtc());
        }
        
        private void OnEntitySpawned(CodeLock codeLock) => NextTick(() =>
        {
            if (!codeLock)
                return;

            PlayerEntities.GetOrCreate(codeLock.OwnerID)?.AddEntity(codeLock);
        });
        
        private void OnEntitySpawned(DoorCloser doorCloser) => NextTick(() =>
        {
            if (!doorCloser)
                return;

            PlayerEntities.GetOrCreate(doorCloser.OwnerID)?.AddEntity(doorCloser);
        });

        private void OnEntityKill(CodeLock codeLock)
        {
            if (!codeLock)
                return;

            PlayerEntities.Get(codeLock.OwnerID)?.RemoveEntity(codeLock, true);
        }
        
        private void OnEntityKill(DoorCloser doorCloser)
        {
            if (!doorCloser)
                return;

            PlayerEntities.Get(doorCloser.OwnerID)?.RemoveEntity(doorCloser);
        }
        
        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (!deployer || !entity || entity.OwnerID == 0UL)
                return;

            if (deployer.GetDeployable().slot != BaseEntity.Slot.Lock || !(entity.GetSlot(BaseEntity.Slot.Lock) is CodeLock))
                return;
            
            BasePlayer owner = deployer.GetOwnerPlayer();
            if (!owner)
                return;

            if (owner.HasPermission(PERMISSION_AUTO_LOCK))
            {
                StoredData.PlayerData playerData = storedData.FindPlayerData(owner.userID) ?? storedData.SetupPlayer(owner.userID, Configuration);

                if (!playerData.IsSet(Options.AutoLock) || !CanDeployLock(owner, entity))
                    return;
                
                CodeLock codelock = entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
                if (!codelock)
                    return;
                
                SetCodeLock(codelock, owner, playerData);
            }
        }
        
        private void OnEntityBuilt(Planner planner, GameObject obj)
        {
            if (!obj)
                return;
            
            BaseEntity entity = obj.ToBaseEntity();
            if (!planner || !entity || entity.OwnerID == 0UL)
                return;

            if (!(entity is Door or BoxStorage or Locker or BuildingPrivlidge))
                return;

            BasePlayer player = planner.GetOwnerPlayer();
            if (!player)
                return;

            StoredData.PlayerData playerData = storedData.FindPlayerData(player.userID);
            if (playerData == null)
                return;

            NextTick(() =>
            {
                if (!player || !entity || entity.IsDestroyed)
                    return;
                
                if (entity is Door)
                {
                    if ((entity as Door).canTakeLock && player.HasPermission(PERMISSION_DEPLOY_DOOR) && playerData.IsSet(Options.DeployDoor))
                        PlaceCodelock(player, entity, playerData);

                    if (((entity as Door).canTakeCloser || entity.HasSlot(BaseEntity.Slot.UpperModifier)) &&
                        player.HasPermission(PERMISSION_DOOR_CLOSER) && playerData.IsSet(Options.DeployDoorCloser))
                        PlaceDoorCloser(player, entity, playerData);
                    return;
                }

                if (entity is BoxStorage && entity.HasSlot(BaseEntity.Slot.Lock))
                {
                    if (player.HasPermission(PERMISSION_DEPLOY_BOX) && playerData.IsSet(Options.DeployBox))
                        PlaceCodelock(player, entity, playerData);
                    return;
                }

                if (entity is Locker && entity.HasSlot(BaseEntity.Slot.Lock))
                {
                    if (player.HasPermission(PERMISSION_DEPLOY_LOCKER) && playerData.IsSet(Options.DeployLocker))
                        PlaceCodelock(player, entity, playerData);
                    return;
                }

                if (entity is BuildingPrivlidge && entity.HasSlot(BaseEntity.Slot.Lock))
                {
                    if (player.HasPermission(PERMISSION_DEPLOY_CUPBOARD) && playerData.IsSet(Options.DeployCupboard))
                        PlaceCodelock(player, entity, playerData);
                    return;
                }
            });
        }
        
        private object CanPickupEntity(BasePlayer player, DoorCloser closer)
        {
            if (player.IsAdmin && Configuration.Other.AdminBypass)
                return null;

            if (Configuration.Other.PreventDoorCloserPickup)
                return false;
            
            return null;
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
                SaveData();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                ChaosUI.Destroy(player, UI_MENU);
            
            UpdateQueue.OnUnload();
        }
        #endregion

        #region Functions
        private void FindRegisterEntities()
        {
            foreach (BaseNetworkable baseNetworkable in BaseNetworkable.serverEntities)
            {
                if (baseNetworkable is DoorCloser doorCloser)
                {
                    AdjustDoorCloserPosition(doorCloser.GetParentEntity(), doorCloser);
                    PlayerEntities.GetOrCreate(doorCloser.OwnerID)?.AddEntity(doorCloser);
                }
                else if (baseNetworkable is CodeLock codeLock)
                    PlayerEntities.GetOrCreate(codeLock.OwnerID)?.AddEntity(codeLock);
            }
        }

        private bool CanDeployLock(BasePlayer player, BaseEntity entity)
        {
            if (!(player.IsAdmin && Configuration.Other.AdminBypass))
            {
                if (player.userID != entity.OwnerID)
                {
                    SendMessage(player, "Notification.NotLocked");
                    return false;
                }

                object externalPlugins = Interface.CallHook("CanAutoLock", player);
                if (externalPlugins != null)
                {
                    SendMessage(player, "Notification.NotLocked.Plugin", externalPlugins is string ? (string) externalPlugins : string.Empty);
                    return false;
                }

                if (NoEscape.IsLoaded)
                {
                    if (Configuration.Other.CheckRaidBlock)
                    {
                        if (NoEscape.IsRaidBlocked(player))
                        {
                            SendMessage(player, "Notification.NotLocked.RaidBlock");
                            return false;
                        }
                    }

                    if (Configuration.Other.CheckCombatBlock)
                    {
                        if (NoEscape.IsCombatBlocked(player))
                        {
                            SendMessage(player, "Notification.NotLocked.CombatBlock");
                            return false;
                        }
                    }
                }
            }

            return true;
        }
        
        private void PlaceCodelock(BasePlayer player, BaseEntity entity, StoredData.PlayerData playerData)
        {
            if (!CanDeployLock(player, entity))
                return;
            
            if (player.HasPermission(PERMISSION_NO_LOCK_NEEDED))
            {
                const string CODELOCK_PREFAB = "assets/prefabs/locks/keypad/lock.code.prefab";
                
                CodeLock codelock = GameManager.server.CreateEntity(CODELOCK_PREFAB) as CodeLock;
                if (!codelock) 
                    return;
                
                codelock.OwnerID = player.userID;
                codelock.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
                
                if (player.HasPermission(PERMISSION_AUTO_LOCK) && playerData.IsSet(Options.AutoLock))
                    SetCodeLock(codelock, player, playerData);
                
                codelock.Spawn();
                entity.SetSlot(BaseEntity.Slot.Lock, codelock);
            }
            else
            {
                Item codelock = player.inventory.FindItemByItemID(1159991980);
                if (codelock == null) 
                    return;
                
                Deployer deployer = codelock.GetHeldEntity() as Deployer;
                if (deployer)
                    deployer.DoDeploy_Slot(deployer.GetDeployable(), player.eyes.HeadRay(), entity.net.ID);
            }
        }
        
        private void PlaceDoorCloser(BasePlayer player, BaseEntity entity, StoredData.PlayerData playerData)
        {
            const string DOOR_CLOSER_PREFAB = "assets/prefabs/misc/doorcloser/doorcloser.prefab";
            
            DoorCloser doorCloser = GameManager.server.CreateEntity(DOOR_CLOSER_PREFAB) as DoorCloser;
            if (!doorCloser) 
                return;
            
            doorCloser.gameObject.Identity();

            doorCloser.OwnerID = player.userID;
            
            if (entity.ShortPrefabName is "floor.ladder.hatch" or "floor.triangle.ladder.hatch")
                doorCloser.delay = playerData.hatchCloseDelay;
            else doorCloser.delay = playerData.doorCloseDelay;
            
            doorCloser.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.UpperModifier));
            doorCloser.OnDeployed(entity, null, null);

            AdjustDoorCloserPosition(entity, doorCloser);
            
            doorCloser.Spawn();
            entity.SetSlot(BaseEntity.Slot.UpperModifier, doorCloser);
        }

        private void AdjustDoorCloserPosition(BaseEntity entity, DoorCloser doorCloser)
        {
            if (!entity || !doorCloser)
                return;
            
            bool isHidden = Configuration.HideDoorClosers;
            
            if (entity.ShortPrefabName == "floor.ladder.hatch")
                doorCloser.transform.localPosition = isHidden ? new Vector3(0.75f, 0f, 0f) : new Vector3(0.7f, 0f, 0f);
            else if (entity.ShortPrefabName == "floor.triangle.ladder.hatch")
                doorCloser.transform.localPosition = isHidden ? new Vector3(-0.85f, 0f, 0f) : new Vector3(-0.8f, 0f, 0f);
            else if (entity.ShortPrefabName.StartsWith("door.double.hinged"))
            {
                doorCloser.transform.localPosition = isHidden ? new Vector3(0f, 2.4f, 0f) : new Vector3(0f, 2.3f, 0f);
                doorCloser.transform.localRotation = isHidden ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            }
            else if (entity.ShortPrefabName == "wall.frame.garagedoor") 
                doorCloser.transform.localPosition = isHidden ? new Vector3(-0.15f, 2.9f, 0f) : new Vector3(0f, 2.85f, 0f);
            else
            {
                doorCloser.transform.localPosition = isHidden ? new Vector3(0.01f, 0.21f, 0f) : Vector3.zero;
                doorCloser.transform.localRotation = isHidden ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            }
        }

        private void SetCodeLock(CodeLock codelock, BasePlayer owner, StoredData.PlayerData playerData)
        {
            codelock.code = GetOrSetPin(playerData);
            codelock.hasCode = true;
            codelock.whitelistPlayers.Add(owner.userID);

            bool guestCodeEnabled = playerData.IsSet(Options.EnableGuestCode);
            if (guestCodeEnabled)
            {
                codelock.guestCode = GetOrSetGuest(playerData);
                codelock.hasGuestCode = true;
            }

            codelock.SetFlag(BaseEntity.Flags.Locked, true, false);
            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.updated.prefab", codelock.transform.position);

            bool streamerMode = owner.net.connection.info.GetBool("global.streamermode");
            
            string code = streamerMode ? "****" : codelock.code;
            string guestCode = streamerMode ? "****" : codelock.guestCode;

            if (guestCodeEnabled)
                SendMessage(owner, "Notification.CodelockSecured.Guest", code, guestCode);
            else SendMessage(owner, "Notification.CodelockSecured", code);
        }

        private string GetOrSetPin(StoredData.PlayerData playerData)
        {
            if (string.IsNullOrEmpty(playerData.pinCode))
                playerData.pinCode = $"{UnityEngine.Random.Range(1, 9999)}".PadLeft(4, '0');
            
            return playerData.pinCode;
        }
        
        private string GetOrSetGuest(StoredData.PlayerData playerData)
        {
            if (string.IsNullOrEmpty(playerData.guestCode))
                playerData.guestCode = $"{UnityEngine.Random.Range(1, 9999)}".PadLeft(4, '0');
            
            return playerData.guestCode;
        }
        
        private static int UnixTimeStampUtc() => (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds;

        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
            string prefix = Configuration.Other.ChatPrefix ? lang.GetMessage("Prefix", this, player.UserIDString) : string.Empty;

            if (args?.Length > 0)
                player.ChatMessage(prefix + string.Format(lang.GetMessage(key, this, player.UserIDString), args));
            else player.ChatMessage(prefix + lang.GetMessage(key, this, player.UserIDString));
        }
        
        private void PurgeOldData()
        {
            List<ulong> purgeList = Facepunch.Pool.Get<List<ulong>>();

            int currentTimeStamp = UnixTimeStampUtc();

            foreach (KeyValuePair<ulong, StoredData.PlayerData> kvp in storedData.playerData)
            {
                if (currentTimeStamp - kvp.Value.lastOnline > (Configuration.Data.PurgeAfter * 86400))
                    purgeList.Add(kvp.Key);
            }

            for (int i = 0; i < purgeList.Count; i++)
                storedData.playerData.Remove(purgeList[i]);

            Facepunch.Pool.FreeUnmanaged(ref purgeList);
        }
        #endregion
        
        #region Commands

        private bool HasAnyPermission(BasePlayer player) => player.HasPermission(PERMISSION_AUTO_LOCK) || player.HasPermission(PERMISSION_DEPLOY_BOX) || 
                                                            player.HasPermission(PERMISSION_DEPLOY_DOOR) || player.HasPermission(PERMISSION_DOOR_CLOSER) || 
                                                            player.HasPermission(PERMISSION_DEPLOY_LOCKER) || player.HasPermission(PERMISSION_DEPLOY_CUPBOARD);

        private void CodelockCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasAnyPermission(player))
            {
                SendMessage(player, "Notification.NoPermission");
                return;
            }
            
            CreateCodelockUI(player);
        }
        
        private void AutoDoorCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_DOOR_CLOSER))
            {
                SendMessage(player, "Notification.NoPermission");
                return;
            }

            if (!player.IsBuildingAuthed())
            {
                SendMessage(player, "Notification.NoBuildPriv");
                return;
            }
            
            if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit raycastHit, 3f, 1 << (int)Rust.Layer.Construction, QueryTriggerInteraction.Ignore))
            {
                Door door = raycastHit.GetEntity() as Door;
                if (door)
                {
                    StoredData.PlayerData playerData = storedData.FindPlayerData(player.userID) ?? storedData.SetupPlayer(player.userID, Configuration);
                    
                    if (door.OwnerID != player.userID)
                    {
                        SendMessage(player, "Notification.NotDoorOwner");
                        return;
                    }

                    DoorCloser doorCloser = door.GetSlot(BaseEntity.Slot.UpperModifier) as DoorCloser;
                    if (doorCloser)
                    {
                        doorCloser.Kill(BaseNetworkable.DestroyMode.None);
                        SendMessage(player, "Notification.DoorCloserRemoved");
                    }
                    else
                    {
                        PlaceDoorCloser(player, door, playerData);
                        SendMessage(player, "Notification.DoorCloserPlaced");
                    }

                    return;
                }
            }
            
            SendMessage(player, "Notification.NoDoorFound");
        }
        #endregion
        
        #region UI
        private const string UI_MENU = "acl.menu";
        
        private Style m_BackgroundStyle;
        private Style m_PanelStyle;
        private Style m_ButtonStyle;
        private Style m_TitleStyle;
        private Style m_CloseStyle;

        private Color m_ToggleColor;
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

            m_ToggleColor = new Color(Configuration.Colors.Highlight.Hex, Configuration.Colors.Highlight.Alpha);
            m_OutlineRed = new OutlineComponent(new Color(Configuration.Colors.Close.Hex, Configuration.Colors.Close.Alpha));
        }

        private void CreateCodelockUI(BasePlayer player)
        {
            StoredData.PlayerData playerData = storedData.FindPlayerData(player.userID) ?? storedData.SetupPlayer(player.userID, Configuration);
            
            const float BASE_HEIGHT = 45;
            const float ELEMENT_HEIGHT = 30;
            
            float height = BASE_HEIGHT;
            
            bool canAutoLock = player.HasPermission(PERMISSION_AUTO_LOCK);
            if (canAutoLock)
            {
                if (playerData.IsSet(Options.AutoLock))
                {
                    height += ELEMENT_HEIGHT * 3;
                    if (playerData.IsSet(Options.EnableGuestCode))
                        height += ELEMENT_HEIGHT;
                }
                else height += ELEMENT_HEIGHT;
            }

            bool canDoorCloser = player.HasPermission(PERMISSION_DOOR_CLOSER);
            if (canDoorCloser)
                height += ELEMENT_HEIGHT * 4;

            bool canDoorDeploy = player.HasPermission(PERMISSION_DEPLOY_DOOR);
            if (canDoorDeploy)
                height += ELEMENT_HEIGHT;
            
            bool canBoxDeploy = player.HasPermission(PERMISSION_DEPLOY_BOX);
            if (canBoxDeploy)
                height += ELEMENT_HEIGHT;
            
            bool canCupboardDeploy = player.HasPermission(PERMISSION_DEPLOY_CUPBOARD);
            if (canCupboardDeploy)
                height += ELEMENT_HEIGHT;
            
            bool canLockerDeploy = player.HasPermission(PERMISSION_DEPLOY_LOCKER);
            if (canLockerDeploy)
                height += ELEMENT_HEIGHT;

            height += 5f;
            
            BaseContainer root = ImageContainer.Create(UI_MENU, Layer.Overall, Anchor.Center, new Offset(-125f, -(height * 0.5f), 125f, (height * 0.5f)))
	        .WithStyle(m_BackgroundStyle)
	        .WithChildren(parent =>
            {
                CreateHeaderBar(parent, player);

		        ImageContainer.Create(parent, Anchor.FullStretch, new Offset(5f, 5f, -5f, -40f))
			        .WithStyle(m_PanelStyle)
			        .WithChildren(layout =>
                    {
                        int elementIndex = 0;

                        if (canAutoLock)
                        {
                            CreateToggleElement(layout, player, playerData, Options.AutoLock, ++elementIndex);

                            if (playerData.IsSet(Options.AutoLock))
                            {
                                CreateInputElement(layout, player, playerData, Options.PinCode, ++elementIndex, 4);
                                CreateToggleElement(layout, player, playerData, Options.EnableGuestCode, ++elementIndex);

                                if (playerData.IsSet(Options.EnableGuestCode))
                                    CreateInputElement(layout, player, playerData, Options.GuestCode, ++elementIndex, 4);
                            }
                        }

                        if (canDoorCloser)
                        {
                            CreateToggleElement(layout, player, playerData, Options.DeployDoorCloser, ++elementIndex);
                            CreateInputElement(layout, player, playerData, Options.DoorDelay, ++elementIndex);
                            CreateInputElement(layout, player, playerData, Options.HatchDelay, ++elementIndex);
                            CreateToggleElement(layout, player, playerData, Options.DisableCloser, ++elementIndex);
                        }
                        
                        if (canDoorDeploy)
                            CreateToggleElement(layout, player, playerData, Options.DeployDoor, ++elementIndex);
                        
                        if (canBoxDeploy)
                            CreateToggleElement(layout, player, playerData, Options.DeployBox, ++elementIndex);
                        
                        if (canLockerDeploy)
                            CreateToggleElement(layout, player, playerData, Options.DeployLocker, ++elementIndex);
                        
                        if (canCupboardDeploy)
                            CreateToggleElement(layout, player, playerData, Options.DeployCupboard, ++elementIndex);
                    });
            })
            .NeedsCursor()
            .NeedsKeyboard()
            .DestroyExisting();

            ChaosUI.Show(player, root);
        }

        private void CreateHeaderBar(BaseContainer parent, BasePlayer player)
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -35f, -5f, -5f))
                .WithStyle(m_PanelStyle)
                .WithChildren(titleBar =>
                {
                    TextContainer.Create(titleBar, Anchor.CenterLeft, new Offset(5f, -15f, 205f, 15f))
                        .WithStyle(m_TitleStyle)
                        .WithText(GetString("Label.Title", player));

                    ImageContainer.Create(titleBar, Anchor.CenterRight, new Offset(-25f, -10f, -5f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithOutline(m_OutlineRed)
                        .WithChildren(exit =>
                        {
                            TextContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                .WithText("✘")
                                .WithStyle(m_CloseStyle);

                            ButtonContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, (arg) => ChaosUI.Destroy(player, UI_MENU), $"{player.UserIDString}.exit");
                        });
                });
        }

        private void CreateInputElement(BaseContainer layout, BasePlayer player, StoredData.PlayerData playerData, Options option, int index, int characterLimit = 0)
        {
            const float ELEMENT_HEIGHT = 30;
            float bottom = -(ELEMENT_HEIGHT * index);
            
            BaseContainer.Create(layout, Anchor.TopStretch, new Offset(5f, bottom, -5, bottom + 25f))
                .WithChildren(inputTemplate =>
                {
                    ImageContainer.Create(inputTemplate, Anchor.FullStretch, new Offset(0f, 0f, -95f, 0f))
                        .WithStyle(m_PanelStyle);

                    ImageContainer.Create(inputTemplate, Anchor.FullStretch, new Offset(137.5f, 0f, -27.5f, 0f))
                        .WithStyle(m_PanelStyle);
                    
                    ImageContainer.Create(inputTemplate, Anchor.CenterRight, new Offset(-25f, -12.5f, 0f, 12.5f))
                        .WithStyle(m_PanelStyle);

                    TextContainer.Create(inputTemplate, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                        .WithText(GetString($"Label.{option}", player))
                        .WithAlignment(TextAnchor.MiddleLeft);

                    ImageContainer.Create(inputTemplate, Anchor.CenterRight, new Offset(-90f, -10f, -30f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithChildren(inputField =>
                        {
                            string currentValue = GetValue(player, playerData, option);
                            
                            InputFieldContainer.Create(inputField, Anchor.FullStretch, Offset.zero)
                                .WithText(currentValue)
                                .WithAlignment(TextAnchor.MiddleCenter)
                                .WithCharacterLimit(characterLimit)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    SetValue(playerData, option, arg);
                                    CreateCodelockUI(player);
                                }, $"{player.UserIDString}.{option}");

                        });
                    
                    ImageContainer.Create(inputTemplate, Anchor.CenterRight, new Offset(-22.5f, -10f, -2.5f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithChildren(applyButton =>
                        {
                            ImageContainer.Create(applyButton, Anchor.FullStretch, new Offset(2.5f, 2.5f, -2.5f, -2.5f))
                                .WithSprite(Icon.Download);

                            ButtonContainer.Create(applyButton, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg => ApplyChanges(player, playerData, option), $"{player.UserIDString}.{option}.apply");

                        });
                });
        }

        private void ApplyChanges(BasePlayer player, StoredData.PlayerData playerData, Options option)
        {
            PlayerEntities playerEntities = PlayerEntities.GetOrCreate(player.userID);
            
            switch (option)
            {
                case Options.PinCode:
                    playerEntities.OnCodeChanged(playerData.pinCode, false);
                    SendMessage(player, "Notification.ApplyingCodeChanges");
                    return;
                
                case Options.GuestCode:
                    playerEntities.OnCodeChanged(playerData.pinCode, false);
                    SendMessage(player, "Notification.ApplyingGuestCodeChanges");
                    return;
                
                case Options.DoorDelay:
                    playerEntities.OnCloserDelayChanged(playerData.doorCloseDelay, false);
                    SendMessage(player, "Notification.ApplyingDoorCloserChanges");
                    return;
                
                case Options.HatchDelay:
                    playerEntities.OnCloserDelayChanged(playerData.hatchCloseDelay, true);
                    SendMessage(player, "Notification.ApplyingHatchCloserChanges");
                    return;
                
                default:
                    return;
            }
        }

        private string GetValue(BasePlayer player, StoredData.PlayerData playerData, Options option)
        {
            bool streamerMode = player.net.connection.info.GetBool("global.streamermode");
            
            switch (option)
            {
                case Options.PinCode:
                    return streamerMode ? "****" : playerData.pinCode;
                
                case Options.GuestCode:
                    return streamerMode ? "****" : playerData.guestCode;
                
                case Options.DoorDelay:
                    return playerData.doorCloseDelay.ToString("N2");
                
                case Options.HatchDelay:
                    return playerData.hatchCloseDelay.ToString("N2");
                
                default:
                    return string.Empty;
            }
        }

        private void SetValue(StoredData.PlayerData playerData, Options option, ConsoleSystem.Arg arg)
        {
            switch (option)
            {
                case Options.PinCode:
                    playerData.pinCode = $"{arg.GetInt(1)}".PadLeft(4, '0');
                    return;
                
                case Options.GuestCode:
                    playerData.guestCode = $"{arg.GetInt(1)}".PadLeft(4, '0');
                    return;
                
                case Options.DoorDelay:
                    playerData.doorCloseDelay = Configuration.Delay.DoorCloser.Clamp(arg.GetFloat(1, Configuration.Delay.DoorCloser.Minimum));
                    return;
                
                case Options.HatchDelay:
                    playerData.hatchCloseDelay = Configuration.Delay.LadderHatch.Clamp(arg.GetFloat(1, Configuration.Delay.LadderHatch.Minimum));
                    return;
                
                default:
                    return;
            }
        }

        private void CreateToggleElement(BaseContainer layout, BasePlayer player, StoredData.PlayerData playerData, Options option, int index)
        {
            const float ELEMENT_HEIGHT = 30;
            float bottom = -(ELEMENT_HEIGHT * index);
            
            BaseContainer.Create(layout, Anchor.TopStretch, new Offset(5f, bottom, -5, bottom + 25f))
                .WithChildren(toggleTemplate =>
                {
                    ImageContainer.Create(toggleTemplate, Anchor.FullStretch, new Offset(0f, 0f, -27.5f, 0f))
                        .WithStyle(m_PanelStyle)
                        .WithImageType(Image.Type.Tiled);

                    ImageContainer.Create(toggleTemplate, Anchor.FullStretch, new Offset(205f, 0f, 0f, 0f))
                        .WithStyle(m_PanelStyle)
                        .WithImageType(Image.Type.Tiled);

                    TextContainer.Create(toggleTemplate, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                        .WithText(GetString($"Label.{option}", player))
                        .WithAlignment(TextAnchor.MiddleLeft);

                    ImageContainer.Create(toggleTemplate, Anchor.CenterRight, new Offset(-22.5f, -10f, -2.5f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithChildren(toggle =>
                        {
                            bool isOn = playerData.IsSet(option);
                            
                            if (isOn)
                            {
                                ImageContainer.Create(toggle, Anchor.FullStretch, new Offset(2.5f, 2.5f, -2.5f, -2.5f))
                                    .WithColor(m_ToggleColor)
                                    .WithSprite(Sprites.Background_Rounded)
                                    .WithImageType(Image.Type.Tiled);
                            }

                            ButtonContainer.Create(toggle, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    if (isOn)
                                        playerData.UnsetOption(option);
                                    else playerData.SetOption(option);

                                    CreateCodelockUI(player);
                                }, $"{player.userID}.{option}");
                        });
                });
        }
        #endregion
        
        #region Components
        private class UpdateQueue : MonoBehaviour
        {
            private readonly Queue<IEnumerator> m_UpdateQueue = new Queue<IEnumerator>();

            private bool m_QueueRunning = false;

            private Coroutine m_Current;

            private static UpdateQueue m_Instance;

            private void Awake()
            {
                m_Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            protected void OnDestroy()
            {
                m_UpdateQueue.Clear();

                if (m_Current != null)
                    StopCoroutine(m_Current);
                
                m_Instance = null;
            }

            public static void Enqueue(IEnumerator enumerator)
            {
                if (!m_Instance)
                    m_Instance = new GameObject("ACL_UpdateQueue").AddComponent<UpdateQueue>();

                m_Instance.m_UpdateQueue.Enqueue(enumerator);

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
                
                while (m_UpdateQueue.Count > 0)
                {
                    IEnumerator enumerator = m_UpdateQueue.Dequeue();
                    yield return StartCoroutine(enumerator);
                }

                m_QueueRunning = false;
            }
        }
        
        private class PlayerEntities
        {
            private List<DoorCloser> m_DoorClosers;
            private List<CodeLock> m_CodeLocks;

            private static Hash<ulong, PlayerEntities> m_PlayerEntities = new Hash<ulong, PlayerEntities>();

            public static PlayerEntities GetOrCreate(ulong playerId)
            {
                if (!playerId.IsSteamId())
                    return null;

                if (!m_PlayerEntities.TryGetValue(playerId, out PlayerEntities playerEntities))
                    playerEntities = m_PlayerEntities[playerId] = new PlayerEntities();

                return playerEntities;
            }

            public static void EnqueueUpdateDoorCloserDelays()
            {
                foreach (KeyValuePair<ulong, PlayerEntities> kvp in m_PlayerEntities)
                {
                    if (kvp.Value.m_DoorClosers == null || kvp.Value.m_DoorClosers.Count == 0)
                        continue;
                    
                    StoredData.PlayerData playerData = storedData.FindPlayerData(kvp.Key);
                    if (playerData != null)
                    {
                        foreach (DoorCloser doorCloser in kvp.Value.m_DoorClosers)
                        {
                            BaseEntity parentEntity = doorCloser.GetParentEntity();
                            if (!parentEntity)
                                continue;

                            bool isHatches = parentEntity.ShortPrefabName is "floor.ladder.hatch" or "floor.triangle.ladder.hatch";
                            
                            UpdateQueue.Enqueue(UpdateDoorCloser(doorCloser, isHatches ? playerData.hatchCloseDelay : playerData.doorCloseDelay, isHatches));
                        }
                    }
                }
            }

            public static PlayerEntities Get(ulong playerId)
            {
                if (!playerId.IsSteamId())
                    return null;

                if (m_PlayerEntities.TryGetValue(playerId, out PlayerEntities playerEntities))
                    return playerEntities;

                return null;
            }

            private PlayerEntities(){}
            
            public void AddEntity(DoorCloser doorCloser)
            {
                if (!doorCloser)
                    return;
                
                if (m_DoorClosers == null)
                    m_DoorClosers = Facepunch.Pool.Get<List<DoorCloser>>();
                else if (m_DoorClosers.Contains(doorCloser))
                    return;
                
                m_DoorClosers.Add(doorCloser);
            }

            public void RemoveEntity(DoorCloser doorCloser)
            {
                if (!doorCloser)
                    return;
                
                if (m_DoorClosers == null)
                    return;

                m_DoorClosers.Remove(doorCloser);
                
                if (m_DoorClosers.Count == 0)
                    Facepunch.Pool.FreeUnmanaged(ref m_DoorClosers);
            }
            
            public void AddEntity(CodeLock codeLock)
            {
                if (!codeLock)
                    return;
                
                if (m_CodeLocks == null)
                    m_CodeLocks = Facepunch.Pool.Get<List<CodeLock>>();
                else if (m_CodeLocks.Contains(codeLock))
                    return;
                
                m_CodeLocks.Add(codeLock);
            }

            public void RemoveEntity(CodeLock codeLock, bool destroyed)
            {
                if (!codeLock)
                    return;
                
                if (m_CodeLocks == null)
                    return;

                m_CodeLocks.Remove(codeLock);
                
                if (m_CodeLocks.Count == 0)
                    Facepunch.Pool.FreeUnmanaged(ref m_CodeLocks);
            }

            public void OnCodeChanged(string code, bool isGuestCode)
            {
                if (m_CodeLocks == null || m_CodeLocks.Count == 0)
                    return;

                foreach (CodeLock codelock in m_CodeLocks)
                    UpdateQueue.Enqueue(UpdateDoorCode(codelock, code, isGuestCode));
            }
            
            public void OnCloserDelayChanged(float time, bool isHatches)
            {
                if (m_DoorClosers == null || m_DoorClosers.Count == 0)
                    return;
                
                foreach (DoorCloser doorCloser in m_DoorClosers)
                    UpdateQueue.Enqueue(UpdateDoorCloser(doorCloser, time, isHatches));
            }

            private static IEnumerator UpdateDoorCode(CodeLock codelock, string code, bool isGuestCode)
            {
                if (codelock && !codelock.IsDestroyed)
                {
                    if (isGuestCode)
                    {
                        codelock.guestCode = code;
                        codelock.hasGuestCode = true;
                        codelock.guestPlayers.Clear();
                        codelock.guestPlayers.Add(codelock.OwnerID);
                    }
                    else
                    {
                        codelock.code = code;
                        codelock.hasCode = true;
                    }

                    codelock.SendNetworkUpdate();

                    yield return null;
                }
            }
            
            private static IEnumerator UpdateDoorCloser(DoorCloser doorCloser, float time, bool isHatches)
            {
                if (doorCloser && !doorCloser.IsDestroyed)
                {
                    BaseEntity parent = doorCloser.GetParentEntity();
                    if (parent && !parent.IsDestroyed)
                    {
                        if (parent.ShortPrefabName is "floor.ladder.hatch" or "floor.triangle.ladder.hatch")
                        {
                            if (isHatches)
                                doorCloser.delay = time;
                        }
                        else
                        {
                            if (!isHatches)
                                doorCloser.delay = time;
                        }

                        doorCloser.SendNetworkUpdate();
                    }

                    yield return null;
                }
            }
        }
        #endregion

        #region Config        
        private ConfigData Configuration => ConfigurationData as ConfigData;
        
        private class ConfigData : BaseConfigData
        {
            [JsonProperty("Chat command")]
            public string Command { get; set; }
            
            [JsonProperty("Door Closer chat command")]
            public string CloserCommand { get; set; }
            
            [JsonProperty("Other Options")]
            public OtherOptions Other { get; set; }
            
            [JsonProperty("Delay Options")]
            public DelayOptions Delay { get; set; }
            
            [JsonProperty("Default Settings")]
            public DefaultSettings Defaults { get; set; }
            
            [JsonProperty(PropertyName = "Data Management")]
            public DataManagement Data { get; set; }
            
            [JsonProperty(PropertyName = "UI Colors")]
            public UIColors Colors { get; set; }
            
            [JsonProperty(PropertyName = "Hide door closers")]
            public bool HideDoorClosers { get; set; }
            
            public class DelayOptions
            {
                [JsonProperty("Door closer")]
                public MinMax DoorCloser { get; set; }
                
                [JsonProperty("Ladder hatch")]
                public MinMax LadderHatch { get; set; }
                
                public class MinMax
                {
                    public float Minimum { get; set; }
                    public float Maximum { get; set; }

                    public float Clamp(float input) => Mathf.Clamp(input, Minimum, Maximum);
                }
            }

            public class OtherOptions
            {
                [JsonProperty("Use prefix in chat messages")]
                public bool ChatPrefix { get; set; }
                
                [JsonProperty("Admins bypass restrictions")]
                public bool AdminBypass { get; set; }
                
                [JsonProperty("Prevent use if player is raid blocked")]
                public bool CheckRaidBlock { get; set; }
                
                [JsonProperty("Prevent use if player is combat blocked")]
                public bool CheckCombatBlock { get; set; }
                
                [JsonProperty("Prevent pick up of door closes")]
                public bool PreventDoorCloserPickup { get; set; }
            }

            public class DefaultSettings
            {
                [JsonProperty("Auto-lock on placement")]
                public bool AutoLock { get; set; }
                
                [JsonProperty("Deploy on doors")]
                public bool DeployDoor { get; set; }
                
                [JsonProperty("Deploy on boxes")]
                public bool DeployBox { get; set; }
                
                [JsonProperty("Deploy on lockers")]
                public bool DeployLocker { get; set; }
                
                [JsonProperty("Deploy on cupboards")]
                public bool DeployCupboard { get; set; }
                
                [JsonProperty("Deploy door closer")]
                public bool DeployDoorCloser { get; set; }
                
                [JsonProperty("Door close delay")]
                public float CloseDelay { get; set; }
                
                [JsonProperty("Ladder hatch close delay")]
                public float HatchDelay { get; set; }
                
                [JsonProperty("Use guest code")]
                public bool UseGuestCode { get; set; }

                private Options m_Defaults = Options.None;

                [JsonIgnore]
                public Options DefaultOptions
                {
                    get
                    {
                        if (m_Defaults == Options.None)
                        {
                            if (AutoLock)
                                m_Defaults |= Options.AutoLock;

                            if (DeployDoor)
                                m_Defaults |= Options.DeployDoor;

                            if (DeployBox)
                                m_Defaults |= Options.DeployBox;

                            if (DeployCupboard)
                                m_Defaults |= Options.DeployCupboard;

                            if (DeployLocker)
                                m_Defaults |= Options.DeployLocker;

                            if (DeployDoorCloser)
                                m_Defaults |= Options.DeployDoorCloser;

                            if (UseGuestCode)
                                m_Defaults |= Options.EnableGuestCode;
                        }

                        return m_Defaults;
                    }
                }
            }
            
            public class DataManagement
            {
                [JsonProperty(PropertyName = "Save data in ProtoBuf format")]
                public bool UseProtoStorage { get; set; }

                [JsonProperty(PropertyName = "Purge user data after X days of inactivity (0 is disabled)")]
                public int PurgeAfter { get; set; }
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

            if (ConfigurationData.Version < new VersionNumber(3, 0, 0))
                ConfigurationData = baseConfigData;

            if (Configuration.Version < new VersionNumber(3, 0, 5))
                Configuration.CloserCommand = baseConfigData.CloserCommand;
        }

        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Command = "codelock",
                CloserCommand = "closer",
                Other = new ConfigData.OtherOptions
                {
                    ChatPrefix = true,
                    CheckRaidBlock = true,
                    CheckCombatBlock = false,
                    PreventDoorCloserPickup = true
                },
                Delay = new ConfigData.DelayOptions
                {
                    LadderHatch = new ConfigData.DelayOptions.MinMax
                    {
                        Minimum = 3f,
                        Maximum = 15f
                    },
                    DoorCloser = new ConfigData.DelayOptions.MinMax
                    {
                        Minimum = 2f,
                        Maximum = 15f
                    }
                },
                Defaults = new ConfigData.DefaultSettings
                {
                    AutoLock = false,
                    DeployDoor = false,
                    DeployBox = false,
                    DeployLocker = false,
                    DeployCupboard = false,
                    CloseDelay = 3f,
                    HatchDelay = 5f,
                    DeployDoorCloser = false,
                    UseGuestCode = false
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
        
        #region Data

        [Flags]
        internal enum Options
        {
            AutoLock = 1 << 0, 
            DeployDoor = 1 << 1, 
            DeployBox = 1 << 2, 
            DeployLocker = 1 << 3, 
            DeployCupboard = 1 << 4, 
            DeployDoorCloser = 1 << 5, 
            EnableGuestCode = 1 << 6,
            None = 1 << 7,
            PinCode = 1 << 8,
            GuestCode = 1 << 9,
            DoorDelay = 1 << 10,
            HatchDelay = 1 << 11,
            DisableCloser = 1 << 12
        }
        
        private static StoredData storedData;

        private const string DATAFILE_NAME = "AutoCodeLock/user_data";

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

            internal PlayerData SetupPlayer(ulong playerId, ConfigData configuration)
            {
                if (playerId < 76561197960265729UL)
                    return null;

                if (!playerData.TryGetValue(playerId, out PlayerData data))                
                    playerData[playerId] = data = new PlayerData(configuration); 

                return data;
            }

            internal PlayerData FindPlayerData(ulong playerId) 
                => playerData.TryGetValue(playerId, out PlayerData data) ? data : null;

            [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
            public class PlayerData
            {
                [ProtoMember(1)]
                public Options options = (Options)0;
                
                [ProtoMember(2)]
                public string pinCode;
                
                [ProtoMember(3)]
                public string guestCode;
                
                [ProtoMember(4)]
                public float doorCloseDelay;
                
                [ProtoMember(5)]
                public float hatchCloseDelay;
               
                [ProtoMember(6)]
                public int lastOnline;

                public PlayerData(){}
                
                public PlayerData(ConfigData configuration)
                {
                    options = configuration.Defaults.DefaultOptions;
                    pinCode = $"{UnityEngine.Random.Range(1, 9999)}".PadLeft(4, '0');
                    guestCode = $"{UnityEngine.Random.Range(1, 9999)}".PadLeft(4, '0');
                    doorCloseDelay = configuration.Delay.DoorCloser.Clamp(doorCloseDelay);
                    hatchCloseDelay = configuration.Delay.LadderHatch.Clamp(hatchCloseDelay);
                }

                internal bool IsSet(Options option) => (options & option) == option;

                internal void SetOption(Options option) => options |= option;
                
                internal void UnsetOption(Options option) => options &= ~option;

                internal void UpdateDelays(ConfigData.DelayOptions delayOptions)
                {
                    doorCloseDelay = delayOptions.DoorCloser.Clamp(doorCloseDelay);
                    hatchCloseDelay = delayOptions.LadderHatch.Clamp(hatchCloseDelay);
                }

                internal void SetLastOnline(int i) => lastOnline = UnixTimeStampUtc();
            }
        }
        #endregion

        #region Localization
        protected override void PopulatePhrases()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "[<color=#68cacd>ACL</color>] : ",
                
                ["Notification.CodelockSecured"] = "Codelock secured and locked\nCode: <color=#62cd32>{0}</color>",
                ["Notification.CodelockSecured.Guest"] = "Codelock secured and locked\nCode: <color=#62cd32>{0}</color>\nGuest code: <color=#62cd32>{1}</color>",
                ["Notification.NotLocked"] = "Codelock not auto-locked. You are not the object owner",
                ["Notification.NotLocked.RaidBlock"] = "Codelock not auto-locked. You are currently raid blocked",
                ["Notification.NotLocked.CombatBlock"] = "Codelock not auto-locked. You are currently combat blocked",
                ["Notification.NotLocked.Plugin"] = "Codelock not auto-locked for reason: {0}",
                
                ["Notification.NoPermission"] = "You do not have permission to use this command",
                ["Notification.NoDoorFound"] = "You are not looking at a door",
                ["Notification.NoBuildPriv"] = "You need building privilege to use this command",
                ["Notification.NotDoorOwner"] = "You are not the owner of this door",
                ["Notification.DoorCloserPlaced"] = "You have placed a door closer on this door",
                ["Notification.DoorCloserRemoved"] = "You have removed the door closer from this door",
                
                ["Notification.ApplyingCodeChanges"] = "Applying pin code to all of your codelocks",
                ["Notification.ApplyingGuestCodeChanges"] = "Applying guest code to all of your codelocks",
                ["Notification.ApplyingDoorCloserChanges"] = "Applying close delay to all of your door closers",
                ["Notification.ApplyingHatchCloserChanges"] = "Applying close delay to all of your ladder hatches",
                
                ["Label.Title"] = "AutoCodeLock",
                ["Label.AutoLock"] = "Auto-lock", 
                ["Label.DeployDoor"] = "Deploy on doors", 
                ["Label.DeployBox"] = "Deploy on boxes", 
                ["Label.DeployLocker"] = "Deploy on lockers", 
                ["Label.DeployCupboard"] = "Deploy on cupboards", 
                ["Label.DeployDoorCloser"] = "Deploy door closer", 
                ["Label.EnableGuestCode"] = "Set guest code", 
                ["Label.PinCode"] = "Auto-set pin code", 
                ["Label.GuestCode"] = "Auto-set guest code", 
                ["Label.DoorDelay"] = "Close delay (doors)", 
                ["Label.HatchDelay"] = "Close delay (hatches)",
                ["Label.DisableCloser"] = "Disable door closers"
            },  this, "en");
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["Prefix"] = "[<color=#68cacd>ACL</color>] : ",
                
                ["Notification.CodelockSecured"] = "Кодовый замок защищен и заперт на ключ\nКод: <color=#62cd32>{0}</color>.",
                ["Notification.CodelockSecured.Guest"] = "Кодовый замок защищен и заперт на ключ\nКод: <color=#62cd32>{0}</color>\nГостевой код: <color=#62cd32>{1}</color>.",
                ["Notification.NotLocked"] = "Кодовый замок не блокируется автоматически. Вы не являетесь владельцем объекта.",
                ["Notification.NotLocked.RaidBlock"] = "Кодовый замок не блокируется автоматически. В данный момент вы заблокированы в режиме рейд-блок.",
                ["Notification.NotLocked.CombatBlock"] = "Кодовый замок не блокируется автоматически. В данный момент вы заблокированы в бою.",
                ["Notification.NotLocked.Plugin"] = "Кодовый замок не блокируется автоматически по какой-либо причине: {0}.",
                
                ["Notification.NoPermission"] = "У вас нет разрешения на использование этой команды.",
                ["Notification.NoDoorFound"] = "Вы смотрите не на дверь.",
                ["Notification.NoBuildPriv"] = "Для использования этой команды вам нужны права на постройку.",
                ["Notification.NotDoorOwner"] = "Вы не являетесь владельцем этой двери.",
                ["Notification.DoorCloserPlaced"] = "Вы установили доводчик на эту дверь.",
                ["Notification.DoorCloserRemoved"] = "Вы сняли дверной доводчик с этой двери.",
                
                ["Notification.ApplyingCodeChanges"] = "Применение pin-кода ко всем вашим кодовым замкам.",
                ["Notification.ApplyingGuestCodeChanges"] = "Применение гостевого кода ко всем вашим кодовым замкам.",
                ["Notification.ApplyingDoorCloserChanges"] = "Применение задержки закрытия ко всем вашим дверным доводчикам.",
                ["Notification.ApplyingHatchCloserChanges"] = "Примените близкую задержку ко всем вашим лестничным люкам.",
                
                ["Label.Title"] = "AutoCodeLock",
                ["Label.AutoLock"] = "Авто-установка код. замка", 
                ["Label.DeployDoor"] = "Устанав. на дверях", 
                ["Label.DeployBox"] = "Устанав. на ящиках", 
                ["Label.DeployLocker"] = "Устанав. на шкафе для переодевания", 
                ["Label.DeployCupboard"] = "Устанав. на шкафе", 
                ["Label.DeployDoorCloser"] = "Deploy door closer", 
                ["Label.EnableGuestCode"] = "Гостевой код", 
                ["Label.PinCode"] = "Устанав. код", 
                ["Label.GuestCode"] = "Устанав. гостевой-код", 
                ["Label.DoorDelay"] = "Close delay (doors)", 
                ["Label.HatchDelay"] = "Close delay (hatches)",
                ["Label.DisableCloser"] = "Disable door closers"
			}, this, "ru");
        }
        #endregion

        [AutoPatch, HarmonyPatch(typeof(DoorCloser), nameof(DoorCloser.SendClose))]
        private static class DoorCloser_SendClose
        {
            [HarmonyPrefix]
            public static bool Prefix(DoorCloser __instance)
            {
                BaseEntity entity = __instance.GetParentEntity();
                if (!entity)
                    return true;

                StoredData.PlayerData playerData = storedData.FindPlayerData(entity.OwnerID);
                if (playerData == null)
                    return true;

                return !playerData.IsSet(Options.DisableCloser);
            }
        }
    }
}
