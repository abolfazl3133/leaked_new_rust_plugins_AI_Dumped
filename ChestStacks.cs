using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using ProtoBuf;
using Rust;
using UnityEngine;

namespace Oxide.Plugins;

[Info("Chest Stacks", "supreme", "1.4.1")]
[Description("Allows players to stack chests")]
public class ChestStacks : RustPlugin
{
    #region Class Fields
        
    private static ChestStacks _pluginInstance;
    private PluginConfig _pluginConfig;
    private PluginData _pluginData;
        
    private readonly Hash<ulong, ChestStacking> _cachedComponents = new();
        
    private const string UsePermission = "cheststacks.use";
    private const string LargeBoxEffect = "assets/prefabs/deployable/large wood storage/effects/large-wood-box-deploy.prefab";
    private const string LargeBoxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
    private const string LargeBoxShortname = "box.wooden.large";
    private const string SmallBoxEffect = "assets/prefabs/deployable/woodenbox/effects/wooden-box-deploy.prefab";
    private const string SmallBoxPrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
    private const string SmallBoxDeployedEntity = "woodbox_deployed";
    private const string SmallBoxShortname = "box.wooden";
    private const int BoxLayer = Layers.Mask.Deployed;
    private const int ConstructionBlockingLayer = Layers.Mask.Construction | BoxLayer;
    private const int TugboatBlockingLayer = Layers.Mask.Vehicle_Large | BoxLayer;
    private readonly Vector3 _smallBoxOffset = new(0f, 0.57f);
    private readonly Vector3 _smallBoxSphereOffset = new(0f, 0.3f);
    private readonly Vector3 _largeBoxOffset = new(0f, 0.8f);
    private readonly Vector3 _largeBoxSphereOffset = new(0f, 0.6f);
    private readonly object _returnObject = true;
    
    private const BaseEntity.Flags StackedFlag = BaseEntity.Flags.Reserved1;

    private enum ChestType : byte
    {
        None = 0,
        SmallBox = 1,
        LargeBox = 2
    }

    #endregion

    #region Hooks
        
    private void Init()
    {
        _pluginInstance = this;
        LoadData();

        foreach (string perm in _pluginConfig.ChestStacksAmount.Keys)
        {
            permission.RegisterPermission(perm, this);
        }

        if (permission.PermissionExists(UsePermission))
        {
            return;
        }
        
        permission.RegisterPermission(UsePermission, this);
    }

    private void OnServerInitialized()
    {
        foreach (BasePlayer player in BasePlayer.activePlayerList)
        {
            OnPlayerConnected(player);
        }
            
        SaveData();
    }

    private void Unload()
    {
        List<ChestStacking> chestStackingComponent = Pool.Get<List<ChestStacking>>();
        chestStackingComponent.AddRange(_cachedComponents.Values);
        
        for (int i = 0; i < chestStackingComponent.Count; i++)
        {
            chestStackingComponent[i].Destroy();
        }

        SaveData();
        _pluginInstance = null;
        Pool.FreeUnmanaged(ref chestStackingComponent);
    }

    private void OnNewSave() => _pluginData.StoredBoxes.Clear();

    private void OnPlayerConnected(BasePlayer player)
    {
        ChestStacking chestStacking = _cachedComponents[player.userID];
        if (chestStacking)
        {
            return;
        }
            
        player.gameObject.AddComponent<ChestStacking>();
    }
        
    private void OnPlayerDisconnected(BasePlayer player)
    {
        ChestStacking chestStacking = _cachedComponents[player.userID];
        if (!chestStacking)
        {
            return;
        }
            
        chestStacking.Destroy();
    }
    
    private void OnEntityKill(BoxStorage box)
    {
        if (!box)
        {
            return;
        }
        
        //todo: re-write and optimize to avoid allocations
        if (box.GetParentEntity() as Tugboat)
        {
            BoxStorage[] foundBoxes = OverlapSphere<BoxStorage>(box.transform.position, 2f, BoxLayer);
            if (foundBoxes.Length > 0)
            {
                int foundBoxesCount = foundBoxes.Length;
                for (int i = 0; i < foundBoxesCount; i++)
                {
                    BoxStorage foundBox = foundBoxes[i];
                    if (!foundBox || box == foundBox || !IsStacked(foundBox))
                    {
                        continue;
                    }
                    
                    NextFrame(() => CheckGround(foundBox));
                }
            }
        }
        
        if (!IsStacked(box))
        {
            return;
        }
        
        HandleUnStacking(box.net.ID.Value);
    }

    private object OnEntityGroundMissing(BoxStorage box)
    {
        if (!box || !IsStacked(box))
        {
            return null;
        }
        
        CheckGround(box);
        return _returnObject;
    }

    #endregion
        
    #region Helper Methods
    
    private bool IsStacked(BoxStorage box) => box.HasFlag(StackedFlag);

    private void CheckGround(BoxStorage box)
    {
        if (!box || HasChestBelow(box.transform.position))
        {
            return;
        }
        
        box.Die();
    }

    private bool HasChestBelow(Vector3 boxPosition) => 
        Physics.Raycast(boxPosition, Vector3.down, 0.5f, BoxLayer);

    private bool HasCeiling(Vector3 position, ChestType chestType, bool onTugboat)
    {
        int layerMask = onTugboat ? TugboatBlockingLayer : ConstructionBlockingLayer;
        return chestType switch
        {
            ChestType.SmallBox => Physics.Raycast(position + _smallBoxSphereOffset, Vector3.up, 
                0.5f, layerMask),
            ChestType.LargeBox => Physics.Raycast(position + _largeBoxSphereOffset, Vector3.up, 
                0.9f, layerMask),
            _ => false
        };
    }
    
    private T[] OverlapSphere<T>(Vector3 pos, float radius, int layer)
    {
        return Physics.OverlapSphere(pos, radius, layer).Select(c => c.ToBaseEntity()).OfType<T>().ToArray();
    }

    private ulong GetBottomBoxId(ulong boxId) => _pluginData.StoredBoxes[boxId]?.BottomBoxId ?? 0;

    private int GetStackedBoxes(ulong bottomBoxId) => _pluginData.StoredBoxes[bottomBoxId]?.Boxes ?? 0;

    private void HandleUnStacking(ulong boxId)
    {
        ulong bottomBoxId = GetBottomBoxId(boxId);
        if (bottomBoxId == 0)
        {
            return;
        }
        
        _pluginData.StoredBoxes[bottomBoxId].Boxes--;
    }
        
    private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        
    private int GetPermissionValue(BasePlayer player, Hash<string, ChestTypeConfig> permissions, ChestType chestType)
    {
        foreach ((string perm, ChestTypeConfig chestTypeConfig) in 
                 permissions.OrderByDescending(p => p.Value.ChestTypeLimits[chestType]))
        {
            if (!HasPermission(player, perm))
            {
                continue;
            }
            
            return chestTypeConfig.ChestTypeLimits[chestType];
        }

        return 0;
    }
        
    private Tugboat GetTugboat(BasePlayer player)
    {
        return player.GetParentEntity() as Tugboat;
    }

    #endregion

    #region Chest Stacking Handler
        
    public class ChestStacking : FacepunchBehaviour
    {
        private BasePlayer Player { get; set; }
        
        private float NextTime { get; set; }
            
        private void Awake()
        {
            Player = GetComponent<BasePlayer>();
            _pluginInstance._cachedComponents[Player.userID] = this;
        }

        private void Update()
        {
            if (!Player || !_pluginInstance.permission.UserHasPermission(Player.UserIDString, UsePermission))
            {
                return;
            }

            if (!Player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
            {
                return;
            }
                
            if (NextTime > Time.time)
            {
                return;
            }
                    
            NextTime = Time.time + 0.5f;
                    
            Item activeItem = Player.GetActiveItem();
            if (activeItem == null || activeItem.info.shortname != SmallBoxShortname && 
                activeItem.info.shortname != LargeBoxShortname)
            {
                return;
            }
                    
            if (_pluginInstance._pluginConfig.BlacklistedSkins.Contains(activeItem.skin))
            {
                return;
            }
                        
            BoxStorage box = GetBox(Player);
            if (!box)
            {
                return;
            }
            
            switch (box.ShortPrefabName)
            {
                case SmallBoxDeployedEntity:
                {
                    StackChest(box, box.transform, activeItem, ChestType.SmallBox);
                    break;
                }
                case LargeBoxShortname:
                {
                    StackChest(box, box.transform, activeItem, ChestType.LargeBox);
                    break;
                }
            }
        }

        private void StackChest(BoxStorage box, Transform boxTransform, Item activeItem, ChestType chestType)
        {
            ulong bottomBoxId = _pluginInstance.GetBottomBoxId(box.net.ID.Value);
            int boxes = _pluginInstance.GetStackedBoxes(bottomBoxId);
            int allowedBoxesAmount = _pluginInstance.GetPermissionValue(Player, _pluginInstance._pluginConfig.ChestStacksAmount, 
                chestType);
            if (boxes >= allowedBoxesAmount)
            {
                Player.ChatMessage(_pluginInstance.Lang(LangKeys.MaxStackAmount, null, allowedBoxesAmount));
                return;
            }
            
            Tugboat tugboat = _pluginInstance.GetTugboat(Player);
            if (_pluginInstance._pluginConfig.BuildingPrivilegeRequired && !Player.IsBuildingAuthed() && !tugboat)
            {
                Player.ChatMessage(_pluginInstance.Lang(LangKeys.BuildingBlock));
                return;
            }

            Vector3 boxPosition = boxTransform.position;
            if (_pluginInstance.HasCeiling(boxPosition, chestType, tugboat))
            {
                Player.ChatMessage(_pluginInstance.Lang(LangKeys.CeilingBlock));
                return;
            }
                
            BuildingPrivlidge tc = Player.GetBuildingPrivilege();
            switch (chestType)
            {
                case ChestType.SmallBox:
                {
                    if (activeItem.info.shortname != SmallBoxShortname)
                    {
                        Player.ChatMessage(_pluginInstance.Lang(LangKeys.OnlyStackSameType));
                        return;
                    }
                        
                    BoxStorage smallBox = (BoxStorage)GameManager.server.CreateEntity(SmallBoxPrefab, 
                        boxPosition + _pluginInstance._smallBoxOffset, boxTransform.rotation);
                    if (!smallBox)
                    {
                        return;
                    }
                        
                    smallBox.Spawn();
                    smallBox.OwnerID = Player.userID;
                    smallBox.skinID = activeItem.skin;
                    if (tc)
                    {
                        smallBox.AttachToBuilding(tc.buildingID);
                    }
                        
                    Interface.CallHook("OnEntityBuilt", Player.GetHeldEntity(), smallBox.transform.gameObject);
                    if (tugboat)
                    {
                        smallBox.SetParent(tugboat, true);
                    }
                    
                    Effect.server.Run(SmallBoxEffect, boxPosition);
                    smallBox.SetFlag(StackedFlag, true);
                    smallBox.SendNetworkUpdateImmediate();
                    HandleStacking(box, smallBox);
                    break;
                }
                case ChestType.LargeBox:
                {
                    if (activeItem.info.shortname != LargeBoxShortname)
                    {
                        Player.ChatMessage(_pluginInstance.Lang(LangKeys.OnlyStackSameType));
                        return;
                    }

                    BoxStorage largeBox = (BoxStorage)GameManager.server.CreateEntity(LargeBoxPrefab,
                        boxPosition + _pluginInstance._largeBoxOffset, boxTransform.rotation);
                    if (!largeBox)
                    {
                        return;
                    }
                        
                    largeBox.Spawn(); 
                    largeBox.OwnerID = Player.userID; 
                    largeBox.skinID = activeItem.skin;
                    if (tc)
                    {
                        largeBox.AttachToBuilding(tc.buildingID); 
                    }
                        
                    Interface.CallHook("OnEntityBuilt", Player.GetHeldEntity(), largeBox.transform.gameObject); 
                    if (tugboat)
                    {
                        largeBox.SetParent(tugboat, true);
                    }
                    
                    Effect.server.Run(LargeBoxEffect, boxPosition);
                    largeBox.SetFlag(StackedFlag, true);
                    largeBox.SendNetworkUpdateImmediate();
                    HandleStacking(box, largeBox);
                    break;
                }
            }
                
            activeItem.UseItem();
            _pluginInstance.SaveData();
        }

        private void HandleStacking(BoxStorage lastBox, BoxStorage newBox)
        {
            ulong bottomBoxId = _pluginInstance.GetBottomBoxId(lastBox.net.ID.Value);
            if (bottomBoxId == 0)
            {
                _pluginInstance._pluginData.StoredBoxes[newBox.net.ID.Value] = new BoxData
                {
                    BottomBoxId = newBox.net.ID.Value,
                    Boxes = 2
                };

                return;
            }

            int stackedBoxes = _pluginInstance.GetStackedBoxes(bottomBoxId);
            _pluginInstance._pluginData.StoredBoxes[newBox.net.ID.Value] = new BoxData
            {
                BottomBoxId = bottomBoxId,
            };

            _pluginInstance._pluginData.StoredBoxes[bottomBoxId].Boxes = ++stackedBoxes;
        }
            
        private BoxStorage GetBox(BasePlayer player)
        {
            if (!Physics.Raycast(player.eyes.HeadRay(), out RaycastHit raycastHit, 3f, BoxLayer))
            {
                return null;
            }
            
            return raycastHit.GetEntity() as BoxStorage;
        }

        public void Destroy()
        {
            _pluginInstance._cachedComponents.Remove(Player.userID);
            DestroyImmediate(this);
        }
    }

    #endregion

    #region Configuration
        
    private class PluginConfig
    {

        [DefaultValue(true)]
        [JsonProperty("Building privilege required")]
        public bool BuildingPrivilegeRequired { get; set; }
            
        [JsonProperty("Blacklisted Skins")]
        public HashSet<ulong> BlacklistedSkins { get; set; }
            
        [JsonProperty("Permissions & their amount of stacked chests lmits")]
        public Hash<string, ChestTypeConfig> ChestStacksAmount { get; set; }
    }

    private class ChestTypeConfig
    {
        [JsonProperty("Chest type limits")]
        public Dictionary<ChestType, int> ChestTypeLimits { get; set; }
    }

    protected override void LoadDefaultConfig()
    {
        PrintWarning("Loading Default Config");
    }

    protected override void LoadConfig()
    {
        base.LoadConfig();
        Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
        _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
        Config.WriteObject(_pluginConfig);
    }

    private PluginConfig AdditionalConfig(PluginConfig pluginConfig)
    {
        pluginConfig.BlacklistedSkins ??= new HashSet<ulong>
        {
            2618923347
        };

        pluginConfig.ChestStacksAmount ??= new Hash<string, ChestTypeConfig>
        {
            ["cheststacks.use"] = new ChestTypeConfig
            {
                ChestTypeLimits = new Dictionary<ChestType, int>
                {
                    [ChestType.SmallBox] = 3,
                    [ChestType.LargeBox] = 5
                }
            },
            ["cheststacks.vip"] = new ChestTypeConfig
            {
                ChestTypeLimits = new Dictionary<ChestType, int>
                {
                    [ChestType.SmallBox] = 5,
                    [ChestType.LargeBox] = 10
                }
            },
        };
            
        return pluginConfig;
    }

    #endregion
        
    #region Data

    private void SaveData()
    {
        if (_pluginData == null)
        {
            return;
        }
            
        ProtoStorage.Save(_pluginData, Name);
    }

    private void LoadData()
    {
        _pluginData = ProtoStorage.Load<PluginData>(Name) ?? new PluginData();
    }

    [ProtoContract]
    private class PluginData
    {
        [ProtoMember(1)]
        public Hash<ulong, BoxData> StoredBoxes { get; set; } = new();
    }

    [ProtoContract]
    private class BoxData
    {
        [ProtoMember(1)]
        public ulong BottomBoxId { get; set; }
        
        [ProtoMember(2)]
        public int Boxes { get; set; }
    }
        
    #endregion
        
    #region Language
        
    private class LangKeys
    {
        public const string MaxStackAmount = nameof(MaxStackAmount);
        public const string OnlyStackSameType = nameof(OnlyStackSameType);
        public const string CeilingBlock = nameof(CeilingBlock);
        public const string BuildingBlock = nameof(BuildingBlock);
    }
        
    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            [LangKeys.MaxStackAmount] = "You are trying to stack more than {0} chests!",
            [LangKeys.OnlyStackSameType] = "You can only stack the same type of chests!",
            [LangKeys.CeilingBlock] = "A ceiling is blocking you from stacking this chest!",
            [LangKeys.BuildingBlock] = "You need to be Building Privileged in order to stack chests!"

        }, this);
    }
        
    private string Lang(string key, BasePlayer player = null)
    {
        return lang.GetMessage(key, this, player?.UserIDString);
    }
        
    private string Lang(string key, BasePlayer player = null, params object[] args)
    {
        try
        {
            return string.Format(Lang(key, player), args);
        }
        catch (Exception ex)
        {
            PrintError($"Lang Key '{key}' threw exception:\n{ex}");
            throw;
        }
    }
        
    #endregion
}