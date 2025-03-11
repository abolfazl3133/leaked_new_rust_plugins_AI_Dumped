using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Serialization;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.UIFramework;
using ProtoBuf;
using UnityEngine;

using Color = Oxide.Ext.Chaos.UIFramework.Color;

namespace Oxide.Plugins
{
    [Info("SkinBox", "rustmods.ru", "2.2.16")]
    [Description("Allows you to reskin item's by placing it in the SkinBox and selecting a new skin")]
    class SkinBox : ChaosPlugin
    {
        #region Fields
        private Hash<ulong, string> _skinPermissions = new Hash<ulong, string>();

        private readonly Hash<string, HashSet<ulong>> _skinList = new Hash<string, HashSet<ulong>>();

        private readonly Hash<ulong, LootHandler> _activeSkinBoxes = new Hash<ulong, LootHandler>();

        private readonly Hash<string, string> _shortnameToDisplayname = new Hash<string, string>();

        private readonly Hash<ulong, string> _skinSearchLookup = new Hash<ulong, string>();

        private readonly Hash<ulong, string> _skinNameLookup = new Hash<ulong, string>();

        private readonly Hash<ulong, double> _cooldownTimes = new Hash<ulong, double>();


        private bool _apiKeyMissing = false;

        private bool _skinsLoaded = false;


        private static SkinBox Instance { get; set; }
        
        private static Func<string, BasePlayer, string> GetMessage;

        private const ulong SPRAYCAN_SKIN = 2937962221UL;

        private const string LOOT_PANEL = "generic_resizable";

        private const int SCRAP_ITEM_ID = -932201673;

        private const int SET_SLOTS = 12;

        [JsonConverter(typeof(StringEnumConverter))]
        private enum CostType { Scrap, ServerRewards, Economics }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum SortBy { Config = 0, ConfigReversed = 1, Alphabetical = 2 }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;

            InitializeUI();

            Configuration.Permission.RegisterPermissions(permission, this);

            Configuration.Command.RegisterCommands(cmd, this);

            if (!Configuration.Command.SprayCan)
            {
                Unsubscribe(nameof(OnSprayCreate));
                Unsubscribe(nameof(OnActiveItemChanged));
            }

            if (Configuration.Favourites.Enabled)            
                StoredData.Load();

            GetMessage = GetString;// (string key, ulong userId) => lang.GetMessage(key, this, userId.ToString());
        }

        private void OnServerInitialized()
        {
            UnsubscribeHooks();

            if (string.IsNullOrEmpty(Configuration.Skins.APIKey))
            {
                _apiKeyMissing = true;
                SendAPIMissingWarning();                
                return;
            }

            if (Configuration.Skins.UseApproved || Configuration.Skins.ApprovedItems.Count > 0)
                CheckSteamDefinitionsUpdated();
            else StartSkinRequest();
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (container.entityOwner)
            {
                LootHandler lootHandler = container.entityOwner.GetComponent<LootHandler>();
                if (!lootHandler)
                    return null;

                if (Configuration.ReskinItemBlocklist.Contains(item.info.shortname))
                {
                    lootHandler.PopupMessage(GetMessage("BlockedItem.Item", lootHandler.Looter));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }
                
                if (Configuration.ReskinSkinBlocklist.Contains(item.skin))
                {
                    lootHandler.PopupMessage(GetMessage("BlockedItem.Skin", lootHandler.Looter));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                if (lootHandler is SkinSetHandler skinSetHandler)
                {
                    skinSetHandler.OnCanAcceptItem(item);
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                if (item.isBroken)
                {
                    lootHandler.PopupMessage(GetMessage("BrokenItem", lootHandler.Looter));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                if (lootHandler.HasItem)
                {
                    lootHandler.PopupMessage(GetMessage("HasItem", lootHandler.Looter));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                if (!HasItemPermissions(lootHandler.Looter, item))
                {
                    lootHandler.PopupMessage(GetMessage("InsufficientItemPermission", lootHandler.Looter));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                string shortname = GetRedirectedShortname(item.info.shortname);
                if (!Configuration.Skins.UseRedirected && !shortname.Equals(item.info.shortname, StringComparison.OrdinalIgnoreCase))
                {
                    lootHandler.PopupMessage(GetMessage("RedirectsDisabled", lootHandler.Looter));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                if (!_skinList.TryGetValue(shortname, out HashSet<ulong> skins) || skins.Count == 0)
                {
                    lootHandler.PopupMessage(GetMessage("NoSkinsForItem", lootHandler.Looter));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }

                int reskinCost = GetReskinCost(item);
                if (reskinCost > 0 && !CanAffordToUse(lootHandler.Looter, reskinCost))
                {
                    lootHandler.PopupMessage(string.Format(GetMessage("NotEnoughBalanceUse", lootHandler.Looter),
                                            reskinCost,
                                            GetCostType(lootHandler.Looter),
                                            item.info.displayName.english));

                    return ItemContainer.CanAcceptResult.CannotAccept;

                }

                string result = Interface.Call<string>("SB_CanAcceptItem", lootHandler.Looter, item);
                if (!string.IsNullOrEmpty(result))
                {
                    lootHandler.PopupMessage(result);
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }
            
            if (item.parent?.entityOwner)
            {
                SkinSetHandler lootHandler = item.parent.entityOwner.GetComponent<SkinSetHandler>();
                if (lootHandler)
                    return ItemContainer.CanAcceptResult.CannotAccept;
            }

            return null;
        }

        private void OnItemSplit(Item item, int amount)
        {
            if (item.parent?.entityOwner)
            {
                LootHandler lootHandler = item.parent.entityOwner.GetComponent<LootHandler>();
                if (lootHandler != null)
                    lootHandler.CheckItemHasSplit(item);
            }
        }        

        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainerID, int targetSlot, int amount, ItemMoveModifier itemMoveModifier)
        {
            if (item.parent?.entityOwner)
            {
                LootHandler lootHandler = item.parent.entityOwner.GetComponent<LootHandler>(); 
                if (lootHandler)
                {
                    if (lootHandler is SkinSetHandler skinSetHandler)
                    {
                        if (targetSlot == -1)
                            skinSetHandler.RemoveItemFromSet(item);
                        return false;
                    }
                    
                    if (lootHandler.InputAmount > 1)
                    {
                        if (targetContainerID == default(ItemContainerId) || targetSlot == -1)
                            return false;

                        ItemContainer targetContainer = inventory.FindContainer(targetContainerID);
                        if (targetContainer?.GetSlot(targetSlot) != null)
                            return false;
                    }
                }
            }
            return null;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!_activeSkinBoxes.TryGetValue(player.userID, out LootHandler lootHandler))
                return null;

            const string DROP_ACTION = "drop";
            if (!action.Equals(DROP_ACTION, StringComparison.OrdinalIgnoreCase))            
                return null;

            if (lootHandler.Entity.inventory?.itemList?.Contains(item) ?? false)
                return true;

            return null;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container?.entityOwner == null || container.entityOwner.IsDestroyed)
                return;

            LootHandler lootHandler = container.entityOwner.GetComponent<LootHandler>();
            if (lootHandler != null)
                lootHandler.OnItemAdded(item);
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container?.entityOwner == null || container.entityOwner.IsDestroyed)
                return;
            
            LootHandler lootHandler = container.entityOwner.GetComponent<LootHandler>();
            if (lootHandler != null)            
                lootHandler.OnItemRemoved(item);            
        }

        private void OnLootEntityEnd(BasePlayer player, StorageContainer storageContainer)
        {
            if (_activeSkinBoxes.TryGetValue(player.userID, out LootHandler lootHandler))            
                UnityEngine.Object.DestroyImmediate(lootHandler);            
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            UnityEngine.Object.Destroy(player.GetComponent<SprayCanWatcher>());

            if (!_activeSkinBoxes.TryGetValue(player.userID, out LootHandler lootHandler))
                return;

            lootHandler.ReturnItemInstantly();
            player.EndLooting();
            UnityEngine.Object.DestroyImmediate(lootHandler);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            UnityEngine.Object.Destroy(player.GetComponent<SprayCanWatcher>());
        }

        private void OnServerSave()
        {
            if (Configuration.Favourites.Enabled)
                StoredData.Save();
        }

        private void Unload()
        {
            SprayCanWatcher[] sprayCanWatchers = UnityEngine.Object.FindObjectsOfType<SprayCanWatcher>();
            for (int i = 0; i < sprayCanWatchers.Length; i++)
                UnityEngine.Object.Destroy(sprayCanWatchers[i]);
            
            LootHandler[] lootHandlers = UnityEngine.Object.FindObjectsOfType<LootHandler>();
            for (int i = 0; i < lootHandlers.Length; i++)
            {
                LootHandler lootHandler = lootHandlers[i];
                if (lootHandler.Looter != null)                
                    lootHandler.Looter.EndLooting();                
                UnityEngine.Object.Destroy(lootHandler);
            }

            Configuration = null;
            Instance = null;
        }
        #endregion
        
        #region Spraycan Shit
        private object OnSprayCreate(SprayCan sprayCan, Vector3 position, Quaternion rotation)
        {
            Item item = sprayCan.GetItem();
            if (item.skin == SPRAYCAN_SKIN)
                return false;

            return null;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (oldItem?.skin == SPRAYCAN_SKIN)
                UnityEngine.Object.Destroy(player.GetComponent<SprayCanWatcher>());

            if (newItem?.skin == SPRAYCAN_SKIN)
                player.gameObject.AddComponent<SprayCanWatcher>();
        }
        #endregion

        #region Functions
        private void SendAPIMissingWarning()
        {
            Debug.LogWarning("You must enter a Steam API key in the config!\nYou can get a API key here -> https://steamcommunity.com/dev/apikey \nOnce you have your API key copy it to the 'Skin Options/Steam API Key' field in your SkinBox.json config file");
        }

        private void ChatMessage(BasePlayer player, string key, params object[] args)
        {
            if (args == null)
                player.ChatMessage(lang.GetMessage(key, this, player.UserIDString));
            else player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
        }

        private LootHandler CreateSkinBox<T>(BasePlayer player, DeployableHandler.ReskinTarget reskinTarget, Action<LootHandler> onOpened = null) where T : LootHandler
        {
            const string COFFIN_PREFAB = "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab";

            StorageContainer container = GameManager.server.CreateEntity(COFFIN_PREFAB, player.transform.position + (Vector3.down * (typeof(T) == typeof(SkinSetHandler) ? 240f : 250f))) as StorageContainer;
            container.limitNetworking = true;
            container.enableSaving = false;
            container.inventorySlots = typeof(T) == typeof(SkinSetHandler) ? SET_SLOTS : 48;
            
            UnityEngine.Object.Destroy(container.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(container.GetComponent<GroundWatch>());
            container.Spawn();

            T lootHandler = container.gameObject.AddComponent<T>();
            
            player.inventory.loot.Clear();
            player.inventory.loot.SendImmediate();
            
            timer.In(0.05f, ()=>
            {
                lootHandler.Looter = player;
                
                if (reskinTarget != null && lootHandler is DeployableHandler deployableHandler)
                    deployableHandler.Target = reskinTarget;
                
                player.inventory.loot.PositionChecks = false;
                player.inventory.loot.entitySource = container;
                player.inventory.loot.itemSource = null;
                player.inventory.loot.MarkDirty();
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.SendImmediate();

                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", LOOT_PANEL);
                container.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            
                if (Configuration.Cost.Enabled && !player.HasPermission(Configuration.Permission.NoCost))
                {
                    lootHandler.PopupMessage(string.Format(GetMessage("CostToUse2", lootHandler.Looter), Configuration.Cost.Deployable, 
                                                                                                        Configuration.Cost.Weapon, 
                                                                                                        Configuration.Cost.Attire,
                                                                                                        GetCostType(player)));
                }
                
                onOpened?.Invoke(lootHandler);
            });
            
            _activeSkinBoxes[player.userID] = lootHandler;

            ToggleHooks();

            return lootHandler;
        }
        #endregion

        #region Hook Subscriptions
        private void ToggleHooks()
        {
            if (_activeSkinBoxes.Count > 0)
                SubscribeHooks();
            else UnsubscribeHooks();
        }

        private void SubscribeHooks()
        {
            Subscribe(nameof(OnLootEntityEnd));
            Subscribe(nameof(OnItemRemovedFromContainer));
            Subscribe(nameof(OnItemAddedToContainer));
            Subscribe(nameof(CanMoveItem));
            Subscribe(nameof(OnItemSplit));
            Subscribe(nameof(CanAcceptItem));
        }

        private void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnLootEntityEnd));
            Unsubscribe(nameof(OnItemRemovedFromContainer));
            Unsubscribe(nameof(OnItemAddedToContainer));
            Unsubscribe(nameof(CanMoveItem));
            Unsubscribe(nameof(OnItemSplit));
            Unsubscribe(nameof(CanAcceptItem));
        }
        #endregion

        #region Helpers
        private void GetSkinsFor(BasePlayer player, string shortname, ref List<ulong> list)
        {
            list.Clear();

            List<ulong> skinOverrides = Interface.Call<List<ulong>>("SB_GetSkinOverrides", player, shortname);
            if (skinOverrides != null && skinOverrides.Count > 0)
            {
                list.AddRange(skinOverrides);
                goto SORT_FAVOURITES;                
            }

            if (_skinList.TryGetValue(shortname, out HashSet<ulong> skins))
            {                
                foreach(ulong skinId in skins)
                {
                    if (_skinPermissions.TryGetValue(skinId, out string perm) && !player.HasPermission(perm))
                        continue;

                    if (Configuration.Blacklist.Contains(skinId) && player.net.connection.authLevel < Configuration.Other.BlacklistAuth)
                        continue;

                    list.Add(skinId);
                }
            }

            SORT_FAVOURITES:
            if (Configuration.Favourites.Enabled)            
                Data.SortForPlayer(player, shortname, ref list);            
        }

        private bool HasItemPermissions(BasePlayer player, Item item)
        {
            switch (item.info.category)
            {
                case ItemCategory.Weapon:
                case ItemCategory.Tool:
                    return player.HasPermission(Configuration.Permission.Weapon);
                case ItemCategory.Construction:                   
                case ItemCategory.Items:
                    return player.HasPermission(Configuration.Permission.Deployable);
                case ItemCategory.Attire:
                    return player.HasPermission(Configuration.Permission.Attire);
                default:
                    return true;
            }            
        }
        #endregion

        #region Usage Costs 
        private static string GetCostType(BasePlayer player) => GetMessage($"Cost.{Configuration.Cost.Currency}", player);

        private static int GetReskinCost(Item item)
        {
            if (!Configuration.Cost.Enabled)
                return 0;

            return GetReskinCost(item.info);
        }
        
        private static int GetReskinCost(ItemDefinition itemDefinition)
        {
            switch (itemDefinition.category)
            {
                case ItemCategory.Weapon:
                case ItemCategory.Tool:
                    return Configuration.Cost.Weapon;
                case ItemCategory.Construction:
                case ItemCategory.Items:
                    return Configuration.Cost.Deployable;
                case ItemCategory.Attire:
                    return Configuration.Cost.Attire;
                default:
                    return 0;
            }
        }

        private bool CanAffordToUse(BasePlayer player, int amount)
        {
            if (!Configuration.Cost.Enabled || amount == 0 || player.HasPermission(Configuration.Permission.NoCost))
                return true;

            switch (Configuration.Cost.Currency)
            {
                case CostType.Scrap:
                    return player.inventory.GetAmount(SCRAP_ITEM_ID) >= amount;
                case CostType.ServerRewards:
                    return ServerRewards.IsLoaded && ServerRewards.CheckPoints(player.userID) >= amount;
                case CostType.Economics:
                    return Economics.IsLoaded && Economics.Balance(player.userID) >= amount;                
            }

            return false;
        }

        private static bool ChargePlayer(BasePlayer player, ItemCategory itemCategory)
        {
            if (!Configuration.Cost.Enabled || player.HasPermission(Configuration.Permission.NoCost))
                return true;

            int amount = itemCategory is ItemCategory.Weapon or ItemCategory.Tool ? Configuration.Cost.Weapon :
                         itemCategory is ItemCategory.Items or ItemCategory.Construction ? Configuration.Cost.Deployable :
                         itemCategory == ItemCategory.Attire ? Configuration.Cost.Attire : 0;

            return ChargePlayer(player, amount);
        }

        private static bool ChargePlayer(BasePlayer player, int amount)
        {
            if (amount == 0 || !Configuration.Cost.Enabled || player.HasPermission(Configuration.Permission.NoCost))
                return true;

            switch (Configuration.Cost.Currency)
            {
                case CostType.Scrap:
                    if (amount <= player.inventory.GetAmount(SCRAP_ITEM_ID))
                    {
                        player.inventory.Take(null, SCRAP_ITEM_ID, amount);
                        return true;
                    }
                    return false;
                case CostType.ServerRewards:
                    return ServerRewards.IsLoaded && (bool)ServerRewards.TakePoints(player.userID, amount);
                case CostType.Economics:
                    return Economics.IsLoaded && Economics.Withdraw(player.userID, (double)amount);
            }
            return false;
        }
        #endregion

        #region Cooldown
        private void ApplyCooldown(BasePlayer player)
        {
            if (!Configuration.Cooldown.Enabled || player.HasPermission(Configuration.Permission.NoCooldown))
                return;

            _cooldownTimes[player.userID] = CurrentTime() + Configuration.Cooldown.Time;
        }

        private bool IsOnCooldown(BasePlayer player)
        {
            if (!Configuration.Cooldown.Enabled || player.HasPermission(Configuration.Permission.NoCooldown))
                return false;

            if (_cooldownTimes.TryGetValue(player.userID, out double time) && time > CurrentTime())
                return true;

            return false;
        }

        private bool IsOnCooldown(BasePlayer player, out double remaining)
        {
            remaining = 0;

            if (!Configuration.Cooldown.Enabled || player.HasPermission(Configuration.Permission.NoCooldown))
                return false;

            double currentTime = CurrentTime();
            if (_cooldownTimes.TryGetValue(player.userID, out double time) && time > currentTime)
            {
                remaining = time - currentTime;
                return true;
            }
            
            return false;
        }
        #endregion

        #region Approved Skins
        private float m_SteamDefinitionsWaitStarted = 0;
        
        private void CheckSteamDefinitionsUpdated()
        {
            float time = UnityEngine.Time.time;
            
            if (m_SteamDefinitionsWaitStarted == 0)
                m_SteamDefinitionsWaitStarted = time;

            float timeWaited = time - m_SteamDefinitionsWaitStarted;
            if (Configuration.Skins.ApprovedTimeout > 0 && timeWaited > Configuration.Skins.ApprovedTimeout)
            {
                Debug.LogWarning($"[SkinBox] - Aborting approved skin processing as timeout period has elapsed ({Configuration.Skins.ApprovedTimeout}s). The server has not yet downloaded the approved skin manifest. Only workshop skins will be available");

                if (!Configuration.Skins.UseWorkshop)
                {
                    PrintError("You have workshop skins disabled. This leaves no skins available to use in SkinBox!");
                    return;
                }

                VerifyWorkshopSkins();
                return;
            }

            if ((Steamworks.SteamInventory.Definitions?.Length ?? 0) == 0)
            {
                if (timeWaited % 10f == 0)
                    Debug.LogWarning($"[SkinBox] - Waiting for Steam inventory definitions to be updated. Total wait time : {Math.Round(time, 1)}s");
                
                timer.In(1f, CheckSteamDefinitionsUpdated);
                return;
            }
            
            StartSkinRequest();
        }
        
        private void StartSkinRequest()
        {
            UpdateWorkshopNameConversionList();

            FindItemRedirects();

            FixLR300InvalidShortname();

            if ((!Configuration.Skins.UseApproved && Configuration.Skins.ApprovedItems.Count == 0) && !Configuration.Skins.UseWorkshop)
            {
                PrintError("You have approved skins and workshop skins disabled. This leaves no skins available to use in SkinBox!");
                return;
            }

            if ((!Configuration.Skins.UseApproved && Configuration.Skins.ApprovedItems.Count == 0) && Configuration.Skins.UseWorkshop)
            {
                VerifyWorkshopSkins();
                return;
            }

            PrintWarning("Retrieving approved skin lists...");

            CollectApprovedSkins();
        }

        private void CollectApprovedSkins()
        {
            int count = 0;

            bool addApprovedPermission = Configuration.Permission.Approved != Configuration.Permission.Use;

            bool updateConfig = false;

            List<ulong> list = Pool.Get<List<ulong>>();

            List<int> itemSkinDirectory = Pool.Get<List<int>>();
            itemSkinDirectory.AddRange(ItemSkinDirectory.Instance.skins.Select(x => x.id));
            
            bool useManuallyAssignedSkinsOnly = !Configuration.Skins.UseApproved && Configuration.Skins.ApprovedItems.Count > 0;

            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                list.Clear();    
                
                if (useManuallyAssignedSkinsOnly && !Configuration.Skins.ApprovedItems.Contains(itemDefinition.shortname))
                    continue;

                foreach (Steamworks.InventoryDef item in Steamworks.SteamInventory.Definitions)
                {
                    string shortname = item.GetProperty("itemshortname");
                    if (string.IsNullOrEmpty(shortname) || item.Id < 100)
                        continue;
                                        
                    if (_workshopNameToShortname.ContainsKey(shortname))
                        shortname = _workshopNameToShortname[shortname];

                    if (!shortname.Equals(itemDefinition.shortname, StringComparison.OrdinalIgnoreCase))  
                        continue;                    

                    ulong skinId;

                    if (itemSkinDirectory.Contains(item.Id))
                        skinId = (ulong)item.Id;
                    else if (!ulong.TryParse(item.GetProperty("workshopid"), out skinId))
                        continue;

                    if (list.Contains(skinId) || Configuration.Skins.ApprovedLimit > 0 && list.Count >= Configuration.Skins.ApprovedLimit)                    
                        continue;
                    
                    list.Add(skinId);

                    _skinNameLookup[skinId] = item.Name;
                    _skinSearchLookup[skinId] = $"{skinId} {item.Name}";
                }

                if (list.Count > 1)
                {
                    count += list.Count;

                    if (!_skinList.TryGetValue(itemDefinition.shortname, out HashSet<ulong> skins))
                        skins = _skinList[itemDefinition.shortname] = new HashSet<ulong>();

                    int removeCount = 0;

                    list.ForEach((ulong skin) =>
                    {
                        if (Configuration.Skins.RemoveApproved && Configuration.SkinList.ContainsKey(itemDefinition.shortname) && 
                                                                  Configuration.SkinList[itemDefinition.shortname].Contains(skin))
                        {
                            Configuration.SkinList[itemDefinition.shortname].Remove(skin);
                            removeCount++;
                            updateConfig = true;
                        }

                        skins.Add(skin);

                        if (addApprovedPermission)
                            _skinPermissions[skin] = Configuration.Permission.Approved;
                    });

                    if (removeCount > 0)
                        Debug.Log($"[SkinBox] Removed {removeCount} approved skin ID's for {itemDefinition.shortname} from the config skin list");
                }
            }

            if (updateConfig)
                SaveConfiguration();

            Pool.FreeUnmanaged(ref list);
            Pool.FreeUnmanaged(ref itemSkinDirectory);

            Debug.Log($"[SkinBox] - Loaded {count} approved skins");

            if (Configuration.Skins.UseWorkshop && Configuration.SkinList.Sum(x => x.Value.Count) > 0)
                VerifyWorkshopSkins();
            else
            {
                SortSkinLists();

                _skinsLoaded = true;
                
                Hash<string, HashSet<ulong>> skinList = new Hash<string, HashSet<ulong>>();
                foreach (KeyValuePair<string, HashSet<ulong>> kvp in _skinList)
                {
                    HashSet<ulong> skins = new HashSet<ulong>();

                    foreach (ulong skinId in kvp.Value)
                        skins.Add(skinId);
                    
                    skinList.Add(kvp.Key, skins);
                }
                
                Configuration.Permission.ReverseCustomSkinPermissions(ref _skinPermissions);

                Interface.Oxide.CallHook("OnSkinBoxSkinsLoaded", skinList);
                
                Debug.Log($"[SkinBox] - SkinBox has loaded all required skins and is ready to use! ({_skinList.Values.Sum(x => x.Count)} skins acrosss {_skinList.Count} items)");
            }
        }

        private void SortSkinLists()
        {
            List<ulong> list = Pool.Get<List<ulong>>();

            foreach (KeyValuePair<string, HashSet<ulong>> kvp in _skinList)
            {
                list.AddRange(kvp.Value);

                SortSkinList(kvp.Key, ref list);

                kvp.Value.Clear();

                list.ForEach((ulong skinId) => kvp.Value.Add(skinId));

                list.Clear();
            }

            Pool.FreeUnmanaged(ref list);
        }

        private void SortSkinList(string shortname, ref List<ulong> list)
        {
            if (Configuration.Skins.Sorting == SortBy.Alphabetical)
            {
                list.Sort((ulong a, ulong b) =>
                {
                    _skinNameLookup.TryGetValue(a, out string nameA);
                    _skinNameLookup.TryGetValue(b, out string nameB);

                    return nameA.CompareTo(nameB);
                });

                return;
            }
            else
            {
                Configuration.SkinList.TryGetValue(shortname, out HashSet<ulong> configList);
                if (configList != null)
                {
                    List<ulong> l = configList.ToList();

                    list.Sort((ulong a, ulong b) =>
                    {
                        int indexA = l.IndexOf(a);
                        int indexB = l.IndexOf(b);

                        return Configuration.Skins.Sorting == SortBy.Config ? indexA.CompareTo(indexB) : indexA.CompareTo(indexB) * -1;
                    });
                }
            }            
        }
        #endregion

        #region Workshop Skins
        private List<ulong> _skinsToVerify = new List<ulong>();
        private Queue<ulong> _collectionsToVerify = new Queue<ulong>();

        private const string PUBLISHED_FILE_DETAILS = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
        private const string COLLECTION_DETAILS = "https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/";
        private const string ITEMS_BODY = "?key={0}&itemcount={1}";
        private const string ITEM_ENTRY = "&publishedfileids[{0}]={1}";
        private const string COLLECTION_BODY = "?key={0}&collectioncount=1&publishedfileids[0]={1}";

        private void VerifyWorkshopSkins()
        {
            foreach (HashSet<ulong> list in Configuration.SkinList.Values)
                _skinsToVerify.AddRange(list);

            SendWorkshopQuery();
        }

        private void SendWorkshopQuery(int page = 0, int success = 0, ConsoleSystem.Arg arg = null, string perm = null)
        {
            int totalPages = Mathf.CeilToInt((float)_skinsToVerify.Count / 100f);
            int index = page * 100;
            int limit = Mathf.Min((page + 1) * 100, _skinsToVerify.Count);
            string details = string.Format(ITEMS_BODY, Configuration.Skins.APIKey, (limit - index));

            for (int i = index; i < limit; i++)
            {
                details += string.Format(ITEM_ENTRY, i - index, _skinsToVerify[i]);
            }

            try
            {
                webrequest.Enqueue(PUBLISHED_FILE_DETAILS, details, (code, response) => ServerMgr.Instance.StartCoroutine(ValidateRequiredSkins(code, response, page + 1, totalPages, success, arg, perm)), this, RequestMethod.POST);
            }
            catch { }
        }

        public enum CollectionAction { AddSkin, RemoveSkin, ExcludeSkin, RemoveExludeSkin }

        private JsonSerializerSettings m_ErrorHandling = new JsonSerializerSettings()
        {
            Error = delegate(object sender, ErrorEventArgs args)
            {
                Debug.Log($"[SkinBox] Steam response JSON deserialization error;\n{args.ErrorContext.Error.Message}\nThis message can be ignored");
                args.ErrorContext.Handled = true;
            }
        };

        private void SendWorkshopCollectionQuery(ulong collectionId, CollectionAction action, int success, ConsoleSystem.Arg arg = null, string perm = null)
        {
            string details = string.Format(COLLECTION_BODY, Configuration.Skins.APIKey, collectionId);

            try
            {
                webrequest.Enqueue(COLLECTION_DETAILS, details, (code, response) => ServerMgr.Instance.StartCoroutine(ProcessCollectionRequest(collectionId, code, response, action, success, arg, perm)), this, RequestMethod.POST);
            }
            catch { }
        }
       
        private IEnumerator ValidateRequiredSkins(int code, string response, int page, int totalPages, int success, ConsoleSystem.Arg arg, string perm)
        {
            bool hasChanged = false;
            int newSkins = 0;
            if (response != null && code == 200)
            {
                QueryResponse queryRespone = JsonConvert.DeserializeObject<QueryResponse>(response, m_ErrorHandling);
                if (queryRespone != null && queryRespone.response != null && queryRespone.response.publishedfiledetails?.Length > 0)
                {
                    SendResponse($"Processing workshop response. Page: {page} / {totalPages}", arg);                    

                    foreach (PublishedFileDetails publishedFileDetails in queryRespone.response.publishedfiledetails)
                    {
                        if (publishedFileDetails.tags != null)
                        {
                            foreach (PublishedFileDetails.Tag tag in publishedFileDetails.tags)
                            {                                
                                if (string.IsNullOrEmpty(tag.tag))
                                    continue;

                                ulong workshopid = Convert.ToUInt64(publishedFileDetails.publishedfileid);

                                string adjTag = tag.tag.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "").Replace(".item", "");
                                if (_workshopNameToShortname.ContainsKey(adjTag))
                                {
                                    string shortname = _workshopNameToShortname[adjTag];

                                    if (shortname == "ammo.snowballgun")
                                        continue;

                                    if (!_skinList.TryGetValue(shortname, out HashSet<ulong> skins))
                                        skins = _skinList[shortname] = new HashSet<ulong>();

                                    if (!skins.Contains(workshopid))
                                    {                                        
                                        skins.Add(workshopid);
                                        _skinNameLookup[workshopid] = publishedFileDetails.title;
                                        _skinSearchLookup[workshopid] = $"{workshopid} {publishedFileDetails.title}";
                                    }

                                    if (!Configuration.SkinList.TryGetValue(shortname, out HashSet<ulong> configSkins))
                                        configSkins = Configuration.SkinList[shortname] = new HashSet<ulong>();

                                    if (!configSkins.Contains(workshopid))
                                    {
                                        hasChanged = true;
                                        configSkins.Add(workshopid);
                                        newSkins += 1;
                                    }

                                    if (!string.IsNullOrEmpty(perm) && !Configuration.Permission.Custom[perm].Contains(workshopid))
                                    {
                                        hasChanged = true;
                                        Configuration.Permission.Custom[perm].Add(workshopid);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            yield return CoroutineEx.waitForEndOfFrame;
            yield return CoroutineEx.waitForEndOfFrame;

            if (hasChanged)
                SaveConfiguration();

            if (page < totalPages)
                SendWorkshopQuery(page, success + newSkins);
            else
            {
                if (_collectionsToVerify.Count != 0)
                {
                    SendWorkshopCollectionQuery(_collectionsToVerify.Dequeue(), CollectionAction.AddSkin, success + newSkins, arg, perm);
                    yield break;
                }

                if (!_skinsLoaded)
                {
                    SortSkinLists();

                    _skinsLoaded = true;
                    
                    Hash<string, HashSet<ulong>> skinList = new Hash<string, HashSet<ulong>>();
                    foreach (KeyValuePair<string, HashSet<ulong>> kvp in _skinList)
                    {
                        HashSet<ulong> skins = new HashSet<ulong>();

                        foreach (ulong skinId in kvp.Value)
                            skins.Add(skinId);
                    
                        skinList.Add(kvp.Key, skins);
                    }
                    
                    Configuration.Permission.ReverseCustomSkinPermissions(ref _skinPermissions);

                    Interface.Oxide.CallHook("OnSkinBoxSkinsLoaded", skinList);
                    
                    Debug.Log($"[SkinBox] - SkinBox has loaded all required skins and is ready to use! ({_skinList.Values.Sum(x => x.Count)} skins acrosss {_skinList.Count} items)");
                }
                else SendResponse($"{success + newSkins} new skins have been added!", arg);
            }
        }

        private IEnumerator ProcessCollectionRequest(ulong collectionId, int code, string response, CollectionAction action, int success, ConsoleSystem.Arg arg, string perm)
        {
            if (response != null && code == 200)
            {
                SendResponse($"Processing response for collection {collectionId}", arg);

                CollectionQueryResponse collectionQuery = JsonConvert.DeserializeObject<CollectionQueryResponse>(response, m_ErrorHandling);
                if (collectionQuery == null || !(collectionQuery is CollectionQueryResponse))
                {
                    SendResponse("Failed to receive a valid workshop collection response", arg);
                    yield break;
                }

                if (collectionQuery.response.resultcount == 0 || collectionQuery.response.collectiondetails == null ||
                    collectionQuery.response.collectiondetails.Length == 0 || collectionQuery.response.collectiondetails[0].result != 1)
                {
                    SendResponse("Failed to receive a valid workshop collection response", arg);
                    yield break;
                }

                _skinsToVerify.Clear();

                foreach (CollectionChild child in collectionQuery.response.collectiondetails[0].children)
                {
                    try
                    {
                        switch (child.filetype)
                        {
                            case 1:
                                _skinsToVerify.Add(Convert.ToUInt64(child.publishedfileid));
                                break;
                            case 2:
                                _collectionsToVerify.Enqueue(Convert.ToUInt64(child.publishedfileid));
                                SendResponse($"Collection {collectionId} contains linked collection {child.publishedfileid}", arg);
                                break;
                            default:
                                break;
                        }                      
                    }
                    catch { }
                }

                if (_skinsToVerify.Count == 0)
                {
                    if (_collectionsToVerify.Count != 0)
                    {
                        SendWorkshopCollectionQuery(_collectionsToVerify.Dequeue(), action, success, arg, perm);
                        yield break;
                    }

                    SendResponse("No valid skin ID's in the specified collection", arg);
                    yield break;
                }

                switch (action)
                {
                    case CollectionAction.AddSkin:
                        SendWorkshopQuery(0, success, arg, perm);
                        break;

                    case CollectionAction.RemoveSkin:
                        RemoveSkins(arg, perm);
                        break;

                    case CollectionAction.ExcludeSkin:
                        AddSkinExclusions(arg);
                        break;

                    case CollectionAction.RemoveExludeSkin:
                        RemoveSkinExclusions(arg);
                        break;
                }              
            }
            else SendResponse($"[SkinBox] Collection response failed. Error code {code}", arg);
        }

        private void RemoveSkins(ConsoleSystem.Arg arg, string perm = null)
        {
            int removedCount = 0;
            for (int y = _skinList.Count - 1; y >= 0; y--)
            {
                KeyValuePair<string, HashSet<ulong>> skin = _skinList.ElementAt(y);

                for (int i = 0; i < _skinsToVerify.Count; i++)
                {
                    ulong skinId = _skinsToVerify[i];
                    if (skin.Value.Contains(skinId))
                    {
                        skin.Value.Remove(skinId);
                        Configuration.SkinList[skin.Key].Remove(skinId);
                        removedCount++;

                        if (!string.IsNullOrEmpty(perm))
                            Configuration.Permission.Custom[perm].Remove(skinId);
                    }
                }

            }

            if (removedCount > 0)
                SaveConfiguration();

            SendReply(arg, $"[SkinBox] - Removed {removedCount} skins");

            if (_collectionsToVerify.Count != 0)
                SendWorkshopCollectionQuery(_collectionsToVerify.Dequeue(), CollectionAction.RemoveSkin, 0, arg, perm);                
        }

        private void AddSkinExclusions(ConsoleSystem.Arg arg)
        {
            int count = 0;
            foreach (ulong skinId in _skinsToVerify)
            {
                if (!Configuration.Blacklist.Contains(skinId))
                {
                    Configuration.Blacklist.Add(skinId);
                    count++;
                }
            }

            if (count > 0)
            {
                SendResponse($"Added {count} new skin ID's to the excluded list", arg);
                SaveConfiguration();
            }
        }

        private void RemoveSkinExclusions(ConsoleSystem.Arg arg)
        {
            int count = 0;
            foreach (ulong skinId in _skinsToVerify)
            {
                if (Configuration.Blacklist.Contains(skinId))
                {
                    Configuration.Blacklist.Remove(skinId);
                    count++;
                }
            }

            if (count > 0)
            {
                SendResponse($"Removed {count} skin ID's from the excluded list", arg);
                SaveConfiguration();
            }
        }

        private void SendResponse(string message, ConsoleSystem.Arg arg)
        {
            if (arg != null)
                SendReply(arg, message);
            else Debug.Log($"[SkinBox] - {message}");
        }
        #endregion

        #region Workshop Name Conversions
        private Dictionary<string, string> _workshopNameToShortname = new Dictionary<string, string>
        {
            {"longtshirt", "tshirt.long" },
            {"cap", "hat.cap" },
            {"beenie", "hat.beenie" },
            {"boonie", "hat.boonie" },
            {"balaclava", "mask.balaclava" },
            {"pipeshotgun", "shotgun.waterpipe" },
            {"woodstorage", "box.wooden" },
            {"ak47", "rifle.ak" },
            {"bearrug", "rug.bear" },
            {"boltrifle", "rifle.bolt" },
            {"bandana", "mask.bandana" },
            {"hideshirt", "attire.hide.vest" },
            {"snowjacket", "jacket.snow" },
            {"buckethat", "bucket.helmet" },
            {"semiautopistol", "pistol.semiauto" },            
            {"roadsignvest", "roadsign.jacket" },
            {"roadsignpants", "roadsign.kilt" },
            {"burlappants", "burlap.trousers" },
            {"collaredshirt", "shirt.collared" },
            {"mp5", "smg.mp5" },
            {"sword", "salvaged.sword" },
            {"workboots", "shoes.boots" },
            {"vagabondjacket", "jacket" },
            {"hideshoes", "attire.hide.boots" },
            {"deerskullmask", "deer.skull.mask" },
            {"minerhat", "hat.miner" },
            {"lr300", "rifle.lr300" },
            {"lr300.item", "rifle.lr300" },
            {"burlapgloves", "burlap.gloves" },
            {"burlap.gloves", "burlap.gloves"},
            {"leather.gloves", "burlap.gloves"},
            {"python", "pistol.python" },
            {"m39", "rifle.m39" },
            {"l96", "rifle.l96" },
            {"woodendoubledoor", "door.double.hinged.wood" }
        };

        private void UpdateWorkshopNameConversionList()
        {     
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                _shortnameToDisplayname[item.shortname] = item.displayName.english;

                string workshopName = item.displayName.english.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
                
                if (!_workshopNameToShortname.ContainsKey(workshopName))
                    _workshopNameToShortname[workshopName] = item.shortname;

                if (!_workshopNameToShortname.ContainsKey(item.shortname))
                    _workshopNameToShortname[item.shortname] = item.shortname;

                if (!_workshopNameToShortname.ContainsKey(item.shortname.Replace(".", "")))
                    _workshopNameToShortname[item.shortname.Replace(".", "")] = item.shortname;
            }

            foreach (Skinnable skin in Skinnable.All.ToList())
            {
                if (string.IsNullOrEmpty(skin.Name) || string.IsNullOrEmpty(skin.ItemName) || _workshopNameToShortname.ContainsKey(skin.Name.ToLower()))
                    continue;

                _workshopNameToShortname[skin.Name.ToLower()] = skin.ItemName.ToLower();
            }
        }

        private void FixLR300InvalidShortname()
        {
            const string LR300_ITEM = "lr300.item";
            const string LR300 = "rifle.lr300";

            if (Configuration.SkinList.TryGetValue(LR300_ITEM, out HashSet<ulong> list))
            {
                Configuration.SkinList.Remove(LR300_ITEM);
                Configuration.SkinList[LR300] = list;

                SaveConfiguration();
            }
        }
        #endregion

        #region Item Skin Redirects
        private Hash<string, string> _itemSkinRedirects = new Hash<string, string>();

        private void FindItemRedirects()
        {            
            bool addApprovedPermission = Configuration.Permission.Approved != Configuration.Permission.Use;

            foreach (ItemSkinDirectory.Skin skin in ItemSkinDirectory.Instance.skins)
            {
                ItemSkin itemSkin = skin.invItem as ItemSkin;
                if (itemSkin == null || itemSkin.Redirect == null)                
                    continue;

                _itemSkinRedirects[itemSkin.Redirect.shortname] = itemSkin.itemDefinition.shortname;

                if (Configuration.Skins.UseRedirected)
                {
                    if (!_skinList.TryGetValue(itemSkin.itemDefinition.shortname, out HashSet<ulong> skins))
                        skins = _skinList[itemSkin.itemDefinition.shortname] = new HashSet<ulong>();

                    skins.Add((ulong)skin.id);

                    _skinNameLookup[(ulong)skin.id] = itemSkin.displayName.english;
                    _skinSearchLookup[(ulong)skin.id] = $"{(ulong)skin.id} {itemSkin.displayName.english}";

                    if (addApprovedPermission)
                        _skinPermissions[(ulong)skin.id] = Configuration.Permission.Approved;
                }
            }
        }

        private string GetRedirectedShortname(string shortname)
        {
            if (_itemSkinRedirects.TryGetValue(shortname, out string redirectedName))
                return redirectedName;

            return shortname;
        }
        #endregion

        #region SkinBox Component
        private class DeployableHandler : LootHandler
        {
            internal ReskinTarget Target
            {
                set
                {
                    reskinTarget = value;
                    Populate();
                }
            }

            protected override ItemDefinition Definition => reskinTarget.itemDefinition.isRedirectOf ?? reskinTarget.itemDefinition;

            protected override ulong InputSkin => reskinTarget.entity.skinID;

            private ReskinTarget reskinTarget;

            protected override void Awake()
            {
                _filteredSkins = Pool.Get<List<ulong>>();

                Entity = GetComponent<StorageContainer>();

                if (!Configuration.Other.AllowStacks)
                {
                    Entity.maxStackSize = 1;
                    Entity.inventory.maxStackSize = 1;
                }

                Entity.SetFlag(BaseEntity.Flags.Open, true, false);
            }

            internal void Populate()
            {
                if (HasItem)
                    return;

                HasItem = true;

                InputShortname = reskinTarget.itemDefinition.isRedirectOf != null ? reskinTarget.itemDefinition.isRedirectOf.shortname : reskinTarget.itemDefinition.shortname;

                _availableSkins = reskinTarget.skins;
                _availableSkins.Remove(0UL);

                if (InputSkin != 0UL)
                    _availableSkins.Remove(InputSkin);

                _filteredSkins.AddRange(_availableSkins);

                _itemsPerPage = InputSkin == 0UL ? Entity.inventory.capacity - 1: Entity.inventory.capacity - 2;

                _currentPage = 0;
                _maximumPages = Mathf.Min(Configuration.Skins.MaximumPages, Mathf.CeilToInt((float)_filteredSkins.Count / (float)_itemsPerPage));

                CreateOverlay();
                CreateFavouritesButton();
                CreatePageButtons();
                CreateSearchBar();
                
                ClearContainer();

                StartCoroutine(FillContainer());
            }

            internal override void OnItemRemoved(Item item)
            {
                if (!HasItem)
                    return;

                ChaosUI.Destroy(Looter, UI_OVERLAY);

                bool skinChanged = item.info != reskinTarget.itemDefinition || (item.skin != InputSkin);
                bool wasSuccess = false;

                if (!skinChanged)
                    goto IGNORE_RESKIN;

                if (skinChanged && !ChargePlayer(Looter, Definition.category))
                {
                    item.skin = InputSkin;

                    if (item.GetHeldEntity() != null)
                        item.GetHeldEntity().skinID = InputSkin;

                    PopupMessage(string.Format(GetMessage("NotEnoughBalanceTake", Looter), item.info.displayName.english, GetCostType(Looter)));
                    goto IGNORE_RESKIN;
                }

                string result2 = Interface.Call<string>("SB_CanReskinDeployableWith", Looter, reskinTarget.entity, reskinTarget.itemDefinition, item.skin);
                if (!string.IsNullOrEmpty(result2))
                {
                    PopupMessage(result2);
                    goto IGNORE_RESKIN;
                }

                wasSuccess = ReskinEntity(Looter, reskinTarget.entity, reskinTarget.itemDefinition, item);

                IGNORE_RESKIN:

                if (wasSuccess && Configuration.Favourites.Enabled)
                    Data.OnSkinClaimed(Looter, item);

                ItemDefinition itemDefinition = item.info;
                ulong itemSkin = item.skin;
                Looter.Invoke(()=> TakeOrDestroyItem(itemDefinition, itemSkin, item), 0.2f);

                ClearContainer();

                Entity.inventory.MarkDirty();
                
                HasItem = false;

                reskinTarget = null;

                if (wasSuccess && Configuration.Cooldown.ActivateOnTake)
                    Instance.ApplyCooldown(Looter);

                Looter.EndLooting();
            }

            public static bool ReskinEntity(BasePlayer looter, BaseEntity targetEntity, ItemDefinition defaultDefinition, Item targetItem)
            {
                if (!CanEntityBeRespawned(targetEntity, out string reason))
                {
                    Instance.ChatMessage(looter, reason);
                    return false;
                }

                if (defaultDefinition != targetItem.info)
                    return ChangeSkinForRedirectedItem(looter, targetEntity, targetItem);
                return ChangeSkinForExistingItem(targetEntity, targetItem);
            }

            private static bool ChangeSkinForRedirectedItem(BasePlayer looter, BaseEntity targetEntity, Item targetItem)
            {
                if (!GetEntityPrefabPath(targetItem.info, out string resourcePath))
                {
                    Instance.ChatMessage(looter, "ReskinError.InvalidResourcePath");
                    return false;
                }

                Vector3 position = targetEntity.transform.position;
                Quaternion rotation = targetEntity.transform.rotation;
                BaseEntity parentEntity = targetEntity.GetParentEntity();

                float health = targetEntity.Health();
                float lastAttackedTime = targetEntity is BaseCombatEntity ? (targetEntity as BaseCombatEntity).lastAttackedTime : 0;

                ulong owner = targetEntity.OwnerID;
                
                float sleepingBagUnlockTime = 0;
                if (targetEntity is SleepingBag)
                    sleepingBagUnlockTime = (targetEntity as SleepingBag).unlockTime;

                Rigidbody rb = targetEntity.GetComponent<Rigidbody>();
                
                bool isDoorOrPriv = targetEntity is Door or BuildingPrivlidge;
                HashSet<PlayerNameID> hashSet = targetEntity is BuildingPrivlidge ? new HashSet<PlayerNameID>((targetEntity as BuildingPrivlidge).authorizedPlayers) : null;
                
                Dictionary<ContainerSet, List<Item>> containerSets = new Dictionary<ContainerSet, List<Item>>();
                SaveEntityStorage(targetEntity, containerSets, 0);
                EntityRef[] slots = targetEntity.GetSlots();
                
                List<ChildPreserveInfo> list = Pool.Get<List<ChildPreserveInfo>>();
                if (!isDoorOrPriv)
                {
                    for (int i = 0; i < targetEntity.children.Count; i++)
                        SaveEntityStorage(targetEntity.children[i], containerSets, i + 1);
                }
                else
                {
                    foreach (BaseEntity child in targetEntity.children)
                    {
                        ChildPreserveInfo childPreserveInfo = new ChildPreserveInfo()
                        {
                            TargetEntity = child,
                            TargetBone = child.parentBone,
                            LocalPosition = child.transform.localPosition,
                            LocalRotation = child.transform.localRotation,
                            Slot = GetEntitySlot(targetEntity, child)
                        };
                        list.Add(childPreserveInfo);
                    }

                    foreach (ChildPreserveInfo childPreserveInfo in list)
                        childPreserveInfo.TargetEntity.SetParent(null, true, false);
                }

                targetEntity.Kill(BaseNetworkable.DestroyMode.None);

                BaseEntity newEntity = GameManager.server.CreateEntity(resourcePath, position, rotation, true);
                
                if (rb)
                {
                    if (newEntity.TryGetComponent<Rigidbody>(out Rigidbody rigidbody) && !rigidbody.isKinematic && rigidbody.useGravity)
                    {
                        rigidbody.useGravity = false;
                        newEntity.Invoke(() => RestoreRigidbody(rigidbody), 0.1f);
                    }
                }

                newEntity.SetParent(parentEntity, false, false);
                newEntity.transform.SetPositionAndRotation(position, rotation);

                newEntity.skinID = targetItem.skin;
                newEntity.OwnerID = owner;

                newEntity.Spawn();

                if (newEntity is BaseCombatEntity)
                {
                    (newEntity as BaseCombatEntity).SetHealth(health);
                    (newEntity as BaseCombatEntity).lastAttackedTime = lastAttackedTime;
                }

                if (newEntity is SleepingBag)
                {
                    (newEntity as SleepingBag).unlockTime = sleepingBagUnlockTime;
                    (newEntity as SleepingBag).deployerUserID = owner;
                }

                if (newEntity is DecayEntity)
                    (newEntity as DecayEntity).AttachToBuilding(null);

                if (containerSets.Count > 0)
                {
                    RestoreEntityStorage(newEntity, 0, containerSets);
                    if (!isDoorOrPriv)
                    {
                        for (int j = 0; j < newEntity.children.Count; j++)
                            RestoreEntityStorage(newEntity.children[j], j + 1, containerSets);
                    }

                    foreach (KeyValuePair<ContainerSet, List<Item>> containerSet in containerSets)
                    {
                        foreach (Item value in containerSet.Value)
                        {
                            value.Remove(0f);
                        }
                    }
                }

                if (newEntity is BuildingPrivlidge buildingPrivlidge)
                    buildingPrivlidge.authorizedPlayers = hashSet;
                
                if (isDoorOrPriv)
                {
                    foreach (ChildPreserveInfo child in list)
                    {
                        child.TargetEntity.SetParent(newEntity, child.TargetBone, true, false);
                        child.TargetEntity.transform.localPosition = child.LocalPosition;
                        child.TargetEntity.transform.localRotation = child.LocalRotation;
                        child.TargetEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    }
                    newEntity.SetSlots(slots);
                }

                Pool.FreeUnmanaged<ChildPreserveInfo>(ref list);

                return true;
            }
                        
            private static bool ChangeSkinForExistingItem(BaseEntity targetEntity, Item targetItem)
            {
                targetEntity.skinID = targetItem.skin;
                targetEntity.SendNetworkUpdateImmediate();

                targetEntity.ClientRPC<int, NetworkableId>(null, "Client_ReskinResult", 1, targetEntity.net.ID);
                return true;
            }

            private static int GetEntitySlot(BaseEntity rootEntity, BaseEntity childEntity)
            {
                for (int i = 0; i < rootEntity.GetSlots().Length; i++)
                {
                    if (rootEntity.GetSlot((BaseEntity.Slot)i) == childEntity)
                    {
                        return i;
                    }
                }
                return -1;
            }

            private void TakeOrDestroyItem(ItemDefinition itemDefinition, ulong skinId, Item item)
            {
                if (item != null && item.parent == null)
                {
                    List<Item> list = Pool.Get<List<Item>>();
                    list.AddRange(Looter.inventory.containerBelt.itemList);
                    list.AddRange(Looter.inventory.containerMain.itemList);

                    for (int i = 0; i < list.Count; i++)
                    {
                        Item y = list[i];
                        if (y.info == itemDefinition && y.skin == skinId)
                        {
                            if (y.amount > 1)
                            {
                                y.amount -= 1;
                                y.MarkDirty();
                            }
                            else
                            {
                                item.RemoveFromContainer();
                                item.Remove(0f);
                            }

                            break;
                        }
                    }

                    Pool.FreeUnmanaged(ref list);
                }

                if (item == null)
                    return;

                item.RemoveFromContainer();
                item.Remove(0f);
            }

            #region Helpers
            private static RaycastHit raycastHit;

            internal static BaseEntity FindReskinTarget(BasePlayer player)
            {
                const int LAYERS = 1 << 0 | 1 << 8 | 1 << 15 | 1 << 16 | 1 << 21;

                BaseEntity baseEntity = null;

                if (Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 5f, LAYERS, QueryTriggerInteraction.Ignore))                
                    baseEntity = raycastHit.GetEntity();
              
                return baseEntity;
            }

            internal static bool CanEntityBeRespawned(BaseEntity targetEntity, out string reason)
            {
                if (!targetEntity || targetEntity.IsDestroyed)
                {
                    reason = "ReskinError.TargetNull";
                    return false;
                }

                BaseMountable baseMountable = targetEntity as BaseMountable;
                if (baseMountable && baseMountable.AnyMounted())
                {
                    reason = "ReskinError.MountBlocked";
                    return false;
                }

                if (targetEntity.isServer)
                {
                    BaseVehicle baseVehicle = targetEntity as BaseVehicle;
                    if (baseVehicle && (baseVehicle.HasDriver() || baseVehicle.AnyMounted()))
                    {
                        reason = "ReskinError.MountBlocked";
                        return false;
                    }
                }

                IOEntity ioEntity = targetEntity as IOEntity;
                if (ioEntity && (HasIOConnection(ioEntity.inputs) || HasIOConnection(ioEntity.outputs)))
                {
                    reason = "ReskinError.IOConnected";
                    return false;
                }

                reason = null;
                return true;
            }

            private static bool GetEntityPrefabPath(ItemDefinition itemDefinition, out string resourcePath)
            {
                resourcePath = string.Empty;

                if (itemDefinition.TryGetComponent<ItemModDeployable>(out ItemModDeployable itemModDeployable))
                {
                    resourcePath = itemModDeployable.entityPrefab.resourcePath;
                    return true;
                }

                if (itemDefinition.TryGetComponent<ItemModEntity>(out ItemModEntity itemModEntity))
                {
                    resourcePath = itemModEntity.entityPrefab.resourcePath;
                    return true;
                }   
                
                if (itemDefinition.TryGetComponent<ItemModEntityReference>(out ItemModEntityReference itemModEntityReference))
                {
                    resourcePath = itemModEntityReference.entityPrefab.resourcePath;
                    return true;
                }

                return false;
            }

            internal static bool HasIOConnection(IOEntity.IOSlot[] slots)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].connectedTo.Get(true))
                        return true;
                }
                return false;
            }

            internal static bool GetItemDefinitionForEntity(BaseEntity entity, out ItemDefinition itemDefinition, bool useRedirect = true)
            {
                itemDefinition = null;

                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity)
                {
                    if (baseCombatEntity.pickup.enabled && baseCombatEntity.pickup.itemTarget)
                        itemDefinition = baseCombatEntity.pickup.itemTarget;

                    else if (baseCombatEntity.repair.enabled && baseCombatEntity.repair.itemTarget)
                        itemDefinition = baseCombatEntity.repair.itemTarget;
                }

                if (useRedirect && itemDefinition && itemDefinition.isRedirectOf)
                    itemDefinition = itemDefinition.isRedirectOf;

                return itemDefinition;
            }

            private static void SaveEntityStorage(BaseEntity baseEntity, Dictionary<ContainerSet, List<Item>> dictionary, int index)
            {
                IItemContainerEntity itemContainerEntity = baseEntity as IItemContainerEntity;

                if (itemContainerEntity != null)
                {
                    ContainerSet containerSet = new ContainerSet() { ContainerIndex = index, PrefabId = index == 0 ? 0 : baseEntity.prefabID };
                  
                    dictionary.Add(containerSet, new List<Item>());

                    while(itemContainerEntity.inventory.itemList.Count > 0)
                    {
                        Item item = itemContainerEntity.inventory.itemList[0];

                        dictionary[containerSet].Add(item);
                        item.RemoveFromContainer();
                    }                    
                }
            }

            private static void RestoreEntityStorage(BaseEntity baseEntity, int index, Dictionary<ContainerSet, List<Item>> copy)
            {
                IItemContainerEntity itemContainerEntity = baseEntity as IItemContainerEntity;
                if (itemContainerEntity != null)
                {
                    ContainerSet containerSet = new ContainerSet() { ContainerIndex = index, PrefabId = index == 0 ? 0 : baseEntity.prefabID };

                    if (copy.ContainsKey(containerSet))
                    {
                        foreach (Item item in copy[containerSet])                        
                            item.MoveToContainer(itemContainerEntity.inventory, -1, true, false);
                        
                        copy.Remove(containerSet);
                    }
                }
            }

            private static void RestoreRigidbody(Rigidbody rb)
            {
                if (rb)                
                    rb.useGravity = true;                
            }
            #endregion

            internal class ReskinTarget
            {
                public BaseEntity entity;
                public ItemDefinition itemDefinition;
                public List<ulong> skins;

                public ReskinTarget(BaseEntity entity, ItemDefinition itemDefinition, List<ulong> skins)
                {
                    this.entity = entity;
                    this.itemDefinition = itemDefinition;
                    this.skins = skins;
                }
            }

            private struct ChildPreserveInfo
            {
                public BaseEntity TargetEntity;

                public uint TargetBone;

                public Vector3 LocalPosition;

                public Quaternion LocalRotation;

                public int Slot;
            }

            private struct ContainerSet
            {
                public int ContainerIndex;

                public uint PrefabId;
            }
        }

        private class LootHandler : MonoBehaviour
        {
            public StorageContainer Entity { get; protected set; }

            internal BasePlayer m_Looter;
            
            internal virtual BasePlayer Looter
            {
                get => m_Looter;
                set
                {
                    m_Looter = value;
                    CreateOverlay();
                } 
            }
            

            public bool HasItem { get; protected set; }

            public int InputAmount => inputItem?.amount ?? 0;

            public string InputShortname { get; protected set; }

            protected virtual ulong InputSkin => inputItem.skin;

            protected virtual ItemDefinition Definition => inputItem?.itemDefinition;



            private InputItem inputItem;


            protected int _currentPage = 0;

            protected int _maximumPages = 0;

            protected int _itemsPerPage;

            protected List<ulong> _availableSkins;

            protected List<ulong> _filteredSkins;

            protected bool _isFillingContainer;


            protected virtual void Awake()
            {                
                _availableSkins = Pool.Get<List<ulong>>();
                _filteredSkins = Pool.Get<List<ulong>>();

                Entity = GetComponent<StorageContainer>();

                if (!Configuration.Other.AllowStacks)
                {
                    Entity.maxStackSize = 1;
                    Entity.inventory.maxStackSize = 1;
                }

                Entity.SetFlag(BaseEntity.Flags.Open, true, false);
            }

            protected virtual void OnDestroy()
            {
                ChaosUI.Destroy(Looter, UI_OVERLAY);
                ChaosUI.Destroy(Looter, UI_POPUP);
                
                Instance?._activeSkinBoxes?.Remove(Looter.userID);

                if (HasItem && inputItem != null)                
                    Looter.GiveItem(inputItem.Create(), BaseEntity.GiveItemReason.PickedUp);
                
                Pool.FreeUnmanaged(ref _availableSkins);
                Pool.FreeUnmanaged(ref _filteredSkins);

                if (Entity != null && !Entity.IsDestroyed)
                {
                    if (Entity.inventory.itemList.Count > 0)
                        ClearContainer();

                    Entity.Kill(BaseNetworkable.DestroyMode.None);
                }

                Instance?.ToggleHooks();
            }

            public void ReturnItemInstantly()
            {
                if (HasItem && Looter && inputItem != null)
                {
                    Looter.GiveItem(inputItem.Create(), BaseEntity.GiveItemReason.PickedUp);
                    HasItem = false;
                }
            }

            internal virtual void OnItemAdded(Item item)
            {
                if (HasItem)
                    return;

                HasItem = true;

                InputShortname = Instance.GetRedirectedShortname(item.info.shortname);

                Instance.GetSkinsFor(Looter, InputShortname, ref _availableSkins);

                _availableSkins.Remove(0UL);

                if (item.skin != 0UL)
                    _availableSkins.Remove(item.skin);

                _filteredSkins.Clear();
                _filteredSkins.AddRange(_availableSkins);

                inputItem = new InputItem(InputShortname, item);

                _itemsPerPage = InputSkin == 0UL ? Entity.inventory.capacity - 1 : Entity.inventory.capacity - 2;

                _currentPage = 0;
                _maximumPages = Mathf.Min(Configuration.Skins.MaximumPages, Mathf.CeilToInt((float)_filteredSkins.Count / (float)_itemsPerPage));

                CreateFavouritesButton();
                CreatePageButtons();
                CreateSearchBar();
                
                if (Configuration.Favourites.Enabled)
                    PopupMessage(GetMessage("FavouritesEnabled", Looter));

                RemoveItem(item);
                ClearContainer();

                StartCoroutine(FillContainer());
            }

            internal void ResetSkinOrder()
            {
                Instance.GetSkinsFor(Looter, InputShortname, ref _availableSkins);

                _availableSkins.Remove(0UL);

                if (InputSkin != 0UL)
                    _availableSkins.Remove(InputSkin);

                _filteredSkins.Clear();
                _filteredSkins.AddRange(_availableSkins);
                
                ChaosUI.Destroy(Looter, UI_FAVOURITES);

                ChangePage(0);
            }

            internal virtual void OnItemRemoved(Item item)
            {
                if (!HasItem)
                    return;
               
                bool skinChanged = item.skin != 0UL && item.skin != InputSkin;
                bool aborted = false;
                inputItem.CloneTo(item);

                BaseEntity heldEntity = item.GetHeldEntity();
                
                if (skinChanged && !ChargePlayer(Looter, Definition.category))
                {
                    item.skin = InputSkin;

                    if (heldEntity)
                        heldEntity.skinID = InputSkin;

                    aborted = true;

                    PopupMessage(string.Format(GetMessage("NotEnoughBalanceTake", Looter), item.info.displayName.english, GetCostType(Looter)));
                }

                string result = Interface.Call<string>("SB_CanReskinItem", Looter, item, item.skin);
                if (!string.IsNullOrEmpty(result))
                {
                    item.skin = InputSkin;

                    if (heldEntity)
                        heldEntity.skinID = InputSkin;

                    aborted = true;

                    PopupMessage(result);                    
                }

                if (!aborted && Configuration.Other.ApplyWorkshopName && (item.skin != 0UL || Configuration.Skins.ShowSkinIDs))
                    Instance._skinNameLookup.TryGetValue(item.skin, out item.name);
                else item.name = inputItem.name;

                if (!aborted && Configuration.Favourites.Enabled)
                    Data.OnSkinClaimed(Looter, item);

                item.MarkDirty();

                ClearContainer();

                Entity.inventory.MarkDirty();

                inputItem.Dispose();
                inputItem = null;
                HasItem = false;

                if (Configuration.Cooldown.ActivateOnTake)
                    Instance.ApplyCooldown(Looter);

                if (Instance.IsOnCooldown(Looter))
                {
                    Looter.EndLooting();
                }
                else
                {
                    ChaosUI.Destroy(Looter, UI_PAGE);
                    ChaosUI.Destroy(Looter, UI_FAVOURITES);
                    ChaosUI.Destroy(Looter, UI_SEARCH);
                    ChaosUI.Destroy(Looter, UI_POPUP);
                }
            }

            private void ChangePage(int change)
            {
                if (_isFillingContainer)
                    return;

                _currentPage = (_currentPage + change) % _maximumPages;
                if (_currentPage < 0)
                    _currentPage = _maximumPages - 1;
                
                CreatePageButtons();
                
                StartCoroutine(RefillContainer());
            }

            private string _searchString = string.Empty;
            
            private void SetSearchParameters(string s)
            {
                _searchString = s;
                
                _filteredSkins.Clear();
                if (string.IsNullOrEmpty(s))
                    _filteredSkins.AddRange(_availableSkins);
                else
                {
                    for (int i = 0; i < _availableSkins.Count; i++)
                    {
                        Instance._skinSearchLookup.TryGetValue(_availableSkins[i], out string skinLabel);

                        if (!string.IsNullOrEmpty(skinLabel) && skinLabel.Contains(s, CompareOptions.OrdinalIgnoreCase))
                            _filteredSkins.Add(_availableSkins[i]);
                    }                    
                }

                _currentPage = 0;
                _maximumPages = Mathf.Min(Configuration.Skins.MaximumPages, Mathf.CeilToInt((float)_filteredSkins.Count / (float)_itemsPerPage));

                ChaosUI.Destroy(Looter, UI_PAGE);
                CreatePageButtons();
                
                StartCoroutine(RefillContainer());
            }

            protected IEnumerator RefillContainer()
            {
                ClearContainer();

                yield return StartCoroutine(FillContainer());
            }

            protected virtual IEnumerator FillContainer()
            {
                _isFillingContainer = true;

                ItemDefinition definition = Definition;
                if (definition)
                {
                    CreateItem(definition, 0UL);

                    if (InputSkin != 0UL)
                        CreateItem(definition, InputSkin);

                    for (int i = _currentPage * _itemsPerPage; i < Mathf.Min(_filteredSkins.Count, (_currentPage + 1) * _itemsPerPage); i++)
                    {
                        if (!HasItem)
                            break;

                        CreateItem(definition, _filteredSkins[i]);

                        if (i % 2 == 0)
                            yield return null;
                    }
                }

                _isFillingContainer = false;
            }

            protected void ClearContainer()
            {               
                for (int i = Entity.inventory.itemList.Count - 1; i >= 0; i--)
                    RemoveItem(Entity.inventory.itemList[i]);                
            }

            protected Item CreateItem(ItemDefinition definition, ulong skinId)
            {
                Item item = ItemManager.Create(definition, 1, skinId);
                item.contents?.SetFlag(ItemContainer.Flag.IsLocked, true);
                
                BaseProjectile heldEntity = item.GetHeldEntity() as BaseProjectile;
                if (heldEntity)
                    heldEntity.primaryMagazine.contents = 0;
                
                if (skinId != 0UL)
                {
                    Instance._skinNameLookup.TryGetValue(skinId, out item.name);

                    if (Configuration.Skins.ShowSkinIDs && Looter.IsAdmin)                    
                        item.name = $"{item.name} ({skinId})";
                }

                if (!InsertItem(item))
                    item.Remove(0f);
                else item.MarkDirty();

                return item;
            }

            private bool InsertItem(Item item)
            {
                if (Entity.inventory.itemList.Contains(item))
                    return false;

                if (Entity.inventory.IsFull())
                    return false;

                Entity.inventory.itemList.Add(item);
                item.parent = Entity.inventory;

                if (!Entity.inventory.FindPosition(item))
                    return false;

                Entity.inventory.MarkDirty();
                Entity.inventory.onItemAddedRemoved?.Invoke(item, true);

                return true;
            }

            protected void RemoveItem(Item item)
            {
                if (!Entity.inventory.itemList.Contains(item))
                    return;

                Entity.inventory.onPreItemRemove?.Invoke(item);

                Entity.inventory.itemList.Remove(item);
                item.parent = null;

                Entity.inventory.MarkDirty();

                Entity.inventory.onItemAddedRemoved?.Invoke(item, false);

                item.Remove(0f);
            }

            internal void CheckItemHasSplit(Item item) => StartCoroutine(CheckItemHasSplit(item, item.amount)); // Item split dupe solution?

            private IEnumerator CheckItemHasSplit(Item item, int originalAmount)
            {
                yield return null;

                if (item != null && item.amount != originalAmount)
                {
                    int splitAmount = originalAmount - item.amount;
                    Looter.inventory.Take(null, item.info.itemid, splitAmount);
                    item.amount += splitAmount;
                }
            }

            private class InputItem
            {
                public ItemDefinition itemDefinition;
                public string name;
                public int amount;
                public ulong skin;

                public string text;

                public float condition;
                public float maxCondition;

                public int magazineContents;
                public int magazineCapacity;
                public ItemDefinition ammoType;

                public List<InputItem> contents;

                internal InputItem(string shortname, Item item)
                {
                    if (!item.info.shortname.Equals(shortname))
                        itemDefinition = ItemManager.FindItemDefinition(shortname);
                    else itemDefinition = item.info;

                    name = item.name;
                    amount = Mathf.Max(item.amount, 1);
                    skin = item.skin;
                    text = item.text;

                    if (item.hasCondition)
                    {
                        condition = item.condition;
                        maxCondition = item.maxCondition;
                    }

                    BaseProjectile baseProjectile = item.GetHeldEntity() as BaseProjectile;
                    if (baseProjectile != null)
                    {
                        magazineContents = baseProjectile.primaryMagazine.contents;
                        magazineCapacity = baseProjectile.primaryMagazine.capacity;
                        ammoType = baseProjectile.primaryMagazine.ammoType;
                    }

                    if (item.contents?.itemList?.Count > 0)
                    {
                        contents = Pool.Get<List<InputItem>>();

                        for (int i = 0; i < item.contents.itemList.Count; i++)
                        {
                            Item content = item.contents.itemList[i];
                            if (content == null)
                                continue;

                            contents.Add(new InputItem(content.info.shortname, content));
                        }
                    }
                }

                internal void Dispose()
                {
                    if (contents != null)
                        Pool.FreeUnmanaged(ref contents);
                }

                internal Item Create()
                {
                    Item item = ItemManager.Create(itemDefinition, amount, skin);

                    item.name = name;
                    item.text = text;
                    
                    if (item.hasCondition)
                    {
                        item.condition = condition;
                        item.maxCondition = maxCondition;
                    }

                    BaseProjectile baseProjectile = item.GetHeldEntity() as BaseProjectile;
                    if (baseProjectile != null)
                    {
                        baseProjectile.primaryMagazine.contents = magazineContents;
                        baseProjectile.primaryMagazine.capacity = magazineCapacity;
                        baseProjectile.primaryMagazine.ammoType = ammoType;
                    }

                    if (contents?.Count > 0)
                    {
                        for (int i = 0; i < contents.Count; i++)
                        {
                            InputItem content = contents[i];

                            Item attachment = ItemManager.Create(content.itemDefinition, Mathf.Max(content.amount, 1), content.skin);
                            if (attachment != null)
                            {
                                if (attachment.hasCondition)
                                {
                                    attachment.condition = content.condition;
                                    attachment.maxCondition = content.maxCondition;
                                }

                                attachment.MoveToContainer(item.contents, -1, false);
                                attachment.MarkDirty();
                            }
                        }

                        item.contents.MarkDirty();
                    }

                    item.MarkDirty();
                    
                    return item;
                }

                internal void CloneTo(Item item)
                {
                    item.contents?.SetFlag(ItemContainer.Flag.IsLocked, false);
                    item.contents?.SetFlag(ItemContainer.Flag.NoItemInput, false);

                    item.amount = amount;
                    item.text = text;
                    
                    if (item.hasCondition)
                    {
                        item.condition = condition;
                        item.maxCondition = maxCondition;
                    }

                    BaseProjectile baseProjectile = item.GetHeldEntity() as BaseProjectile;
                    if (baseProjectile != null && baseProjectile.primaryMagazine != null)
                    {
                        baseProjectile.primaryMagazine.contents = magazineContents;
                        baseProjectile.primaryMagazine.capacity = magazineCapacity;
                        baseProjectile.primaryMagazine.ammoType = ammoType;
                    }

                    if (contents?.Count > 0)
                    {
                        for (int i = 0; i < contents.Count; i++)
                        {
                            InputItem content = contents[i];

                            Item attachment = ItemManager.Create(content.itemDefinition, content.amount, content.skin);
                            if (attachment.hasCondition)
                            {
                                attachment.condition = content.condition;
                                attachment.maxCondition = content.maxCondition;
                            }

                            attachment.MoveToContainer(item.contents, -1, false);
                            attachment.MarkDirty();
                        }

                        item.contents.MarkDirty();
                    }

                    item.MarkDirty();                    
                }
            }

            #region UI
            protected virtual void CreateOverlay()
            {
                BaseContainer root = BaseContainer.Create(UI_OVERLAY, Layer.HudMenu, Anchor.FullStretch, Offset.zero)
                    .WithChildren(parent =>
                    {
                        BaseContainer menu = BaseContainer.Create(parent, Anchor.FullStretch, new Offset(17.5f, 17.5f, -17.5f, -17.5f));
                        BaseContainer container = BaseContainer.Create(menu, Anchor.BottomCenter, new Offset(-580.5f, 64f, 580.5f, 635f));
                        BaseContainer right = BaseContainer.Create(container, Anchor.RightStretch, new Offset(-380f, 28f, 0f, 5f));
                        BaseContainer contents = BaseContainer.Create(right, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 950.22f));
                        BaseContainer loot =  BaseContainer.Create(contents, Anchor.TopLeft, new Offset(0f, -950.22f, 380f, -393.22f));
                        BaseContainer panel = BaseContainer.Create(loot, Anchor.TopLeft, new Offset(0f, -557f, 380f, -8f))
                            .WithName(UI_PANEL_PARENT);
                        
                        // Header
                        ImageContainer.Create(panel, Anchor.TopLeft, new Offset(-8f, -49f, 372f, -26f))
                            .WithColor(m_HeaderColor)
                            .WithName(UI_HEADER_PARENT)
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, new Offset(10, 0, 0, 0))
                                    .WithText(GetMessage("UI.Title", Looter))
                                    .WithStyle(m_TextStyle)
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                // Create Sets button
                                if (!(this is DeployableHandler))
                                {
                                    if (Configuration.Sets.Enabled && Looter.HasPermission(Configuration.Permission.Sets))
                                    {
                                        ImageContainer.Create(header, Anchor.CenterRight, new Offset(-41.5f, -10f, -1.5f, 10f))
                                            .WithStyle(m_SetStyle)
                                            .WithChildren(prev =>
                                            {
                                                TextContainer.Create(prev, Anchor.FullStretch, Offset.zero)
                                                    .WithText(GetMessage("UI.Sets", Looter))
                                                    .WithStyle(m_SetStyle);

                                                ButtonContainer.Create(prev, Anchor.FullStretch, Offset.zero)
                                                    .WithColor(Color.Clear)
                                                    .WithCallback(m_CallbackHandler,
                                                        (arg) => Instance.CreateSkinBox<SkinSetHandler>(Looter, null), $"{Looter.userID}.sets");
                                            });
                                    }
                                }
                            });
                    })
                    .DestroyExisting();

                ChaosUI.Show(Looter, root);
            }
            
            protected void CreateFavouritesButton()
            {
                // Favourites system
                if (HasItem && Configuration.Favourites.Enabled && Data.UserHasFavouritesFor(Looter, InputShortname))
                {
                    BaseContainer root = ImageContainer.Create(UI_FAVOURITES, Layer.HudMenu, Anchor.BottomLeft, new Offset(-2f, -22f, 150f, -2f))
                        .WithStyle(m_PanelStyle)
                        .WithParent(UI_PANEL_PARENT)
                        .WithChildren(favourites =>
                        {
                            TextContainer.Create(favourites, Anchor.FullStretch, Offset.zero)
                                .WithText(GetMessage("UI.ClearFavourites", Looter))
                                .WithStyle(m_PanelStyle);

                            ButtonContainer.Create(favourites, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, (arg) =>
                                    {
                                        Data.ClearForPlayer(Looter, InputShortname);
                                        ResetSkinOrder();
                                        
                                    }, $"{Looter.userID}.clearfavourites");
                        }).DestroyExisting();
                    
                    ChaosUI.Show(Looter, root);
                }
            }

            protected void CreatePageButtons()
            {
                bool shouldOffset = this is DeployableHandler;
                
                BaseContainer root = BaseContainer.Create(UI_PAGE, Layer.HudMenu, Anchor.FullStretch, Offset.zero)
                    .WithParent(UI_HEADER_PARENT)
                    .WithChildren(header =>
                    {
                        float offset = 41.5f;
                        if (!shouldOffset && Configuration.Sets.Enabled && Looter.HasPermission(Configuration.Permission.Sets))
                            offset = 0f;
                        
                        // Next page
                        ImageContainer.Create(header, Anchor.CenterRight, new Offset(-73f + offset, -10f, -43f + offset, 10f))
                            .WithStyle(m_PageStyle)
                            .WithChildren(next =>
                            {
                                TextContainer.Create(next, Anchor.FullStretch, Offset.zero)
                                    .WithText("▶")
                                    .WithStyle(m_PageStyle);

                                if (_maximumPages > 1)
                                {
                                    ButtonContainer.Create(next, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, (arg) => ChangePage(1), $"{Looter.userID}.nextpage");
                                }
                            });

                        // Page label
                        TextContainer.Create(header, Anchor.CenterRight, new Offset(-123f + offset, -10.5f, -73f + offset, 10.5f))
                            .WithText($"{_currentPage + 1} / {_maximumPages}")
                            .WithStyle(m_PanelStyle);

                        // Previous page
                        ImageContainer.Create(header, Anchor.CenterRight, new Offset(-153f + offset, -10f, -123f + offset, 10f))
                            .WithStyle(m_PageStyle)
                            .WithChildren(prev =>
                            {
                                TextContainer.Create(prev, Anchor.FullStretch, Offset.zero)
                                    .WithText("◀")
                                    .WithStyle(m_PageStyle);

                                if (_maximumPages > 1)
                                {
                                    ButtonContainer.Create(prev, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, (arg) => ChangePage(-1), $"{Looter.userID}.prevpage");
                                }
                            });
                    })
                    .DestroyExisting();

                ChaosUI.Show(Looter, root);
            }

            protected void CreateSearchBar()
            {
                if (HasItem)
                {
                    // Search bar
                    BaseContainer root = ImageContainer.Create(UI_SEARCH, Layer.HudMenu, Anchor.CenterRight, new Offset(-153f, 13f, 0f, 33f))
                        .WithStyle(m_PanelStyle)
                        .WithParent(UI_HEADER_PARENT)
                        .WithChildren(search =>
                        {
                            InputFieldContainer.Create(search, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                .WithText(_searchString)
                                .WithStyle(m_InputStyle)
                                .InHudMenu()
                                .WithCallback(m_CallbackHandler, (arg) =>
                                        SetSearchParameters(arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty), $"{Looter.userID}.search");

                            ImageContainer.Create(search, Anchor.CenterLeft, new Offset(-20, -10, 0, 10))
                                .WithStyle(m_PanelStyle);

                            RawImageContainer.Create(search, Anchor.CenterLeft, new Offset(-20, -10, 0, 10))
                                .WithURL(MAGNIFY_URL)
                                .WithColor(m_PanelStyle.FontColor);
                        })
                        .DestroyExisting();
                    
                    ChaosUI.Show(Looter, root);
                }
            }
            
            internal void PopupMessage(string message)
            {
                BaseContainer root = ImageContainer.Create(UI_POPUP, Layer.HudMenu, Anchor.TopRight, new Offset(-315f, -65f, -5f, -5f))
                    .WithStyle(m_PanelStyle)
                    .WithChildren(parent =>
                    {
                        TextContainer.Create(parent, Anchor.FullStretch, new Offset(5, 5, -5, -5))
                            .WithText(message)
                            .WithStyle(m_PanelStyle);
                        
                        ImageContainer.Create(parent, Anchor.TopRight, new Offset(-20f, -20f, 0f, 0f))
                            .WithStyle(m_SetStyle)
                            .WithChildren(button =>
                            {
                                TextContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                    .WithStyle(m_SetStyle)
                                    .WithText("✘");

                                ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, 
                                        (arg)=> ChaosUI.Destroy(Looter, UI_POPUP), $"{Looter.userID}.dismiss.popup");

                            });

                    })
                    .DestroyExisting();

                ChaosUI.Show(Looter, root);
            }
            #endregion
        }
        #endregion
        
        #region Skin Sets
        private class SkinSetHandler : LootHandler
        {
            private List<StoredData.SetItem[]> _sets;
            private List<Item> _itemList;
            private int _reskinCost;
            
            internal override BasePlayer Looter
            {
                get => m_Looter;
                set
                {
                    m_Looter = value;
                    
                    _sets = Data.GetSkinSetsForPlayer(value);
                    _maximumPages = Mathf.Max(Configuration.Sets.NumberSets, 1);

                    if (_sets.Count < _maximumPages)
                    {
                        int total = _maximumPages - _sets.Count;
                        
                        for (int i = 0; i < total; i++)
                            _sets.Add(new StoredData.SetItem[SET_SLOTS]);
                    }

                    for (int i = 0; i < _sets.Count; i++)
                    {
                        StoredData.SetItem[] set = _sets[i];
                        if (set == null)
                            _sets[i] = new StoredData.SetItem[SET_SLOTS];
                    }
                    
                    _itemList = Pool.Get<List<Item>>();
                    Looter.inventory.GetAllItems(_itemList);
                    
                    _itemList.RemoveAll((item) =>
                    {
                        string result = Interface.Call<string>("SB_CanReskinItem", Looter, item, item.skin);
                        return !string.IsNullOrEmpty(result);
                    });

                    StartCoroutine(RefillContainer());
                    
                    CreateOverlay();
                    CreatePageButtons();
                } 
            }
            
            protected override void Awake()
            {
                Entity = GetComponent<StorageContainer>();
                
                Entity.maxStackSize = 1;
                Entity.inventory.maxStackSize = 1;

                Entity.SetFlag(BaseEntity.Flags.Open, true, false);

                _itemsPerPage = 12;
            }

            protected override void OnDestroy()
            {
                ChaosUI.Destroy(Looter, UI_OVERLAY);
                ChaosUI.Destroy(Looter, UI_POPUP);
                
                Instance?._activeSkinBoxes?.Remove(Looter.userID);
                
                Pool.FreeUnmanaged(ref _itemList);
                
                if (Entity != null && !Entity.IsDestroyed)
                {
                    if (Entity.inventory.itemList.Count > 0)
                        ClearContainer();

                    Entity.Kill(BaseNetworkable.DestroyMode.None);
                }

                Instance?.ToggleHooks();
            }

            public void OnCanAcceptItem(Item item)
            {
                if (item.skin == 0UL || Configuration.Blacklist.Contains(item.skin))
                    return;

                int freeIndex = -1;
                
                StoredData.SetItem[] currentSet = _sets[_currentPage];
                for (int i = 0; i < currentSet.Length; i++)
                {
                    StoredData.SetItem setItem = currentSet[i];
                    if (setItem == null)
                    {
                        if (freeIndex == -1)
                            freeIndex = i;
                        
                        continue;
                    }

                    if (setItem.Shortname == item.info.shortname)
                    {
                        if (setItem.SkinId != item.skin)
                        {
                            setItem.SkinId = item.skin;
                            Item inventoryItem = Entity.inventory.GetSlot(i);
                            inventoryItem.skin = item.skin;
                            inventoryItem.MarkDirty();
                        }
                        return;
                    }
                }

                if (freeIndex != -1)
                {
                    currentSet[freeIndex] = new StoredData.SetItem
                    {
                        Shortname = item.info.shortname,
                        SkinId = item.skin
                    };

                    Item inventoryItem = CreateItem(item.info, item.skin);
                    inventoryItem.position = freeIndex;
                    inventoryItem.MarkDirty();
                    
                    UpdateReskinCost();
                }
            }
            
            internal override void OnItemAdded(Item item)
            {
            }
            
            internal override void OnItemRemoved(Item item)
            {
            }

            public void RemoveItemFromSet(Item item)
            {
                StartCoroutine(DelayedRemoved());
                
                IEnumerator DelayedRemoved()
                {
                    yield return null;

                    if (item != null)
                    {
                        StoredData.SetItem[] currentSet = _sets[_currentPage];
                        for (int i = 0; i < currentSet.Length; i++)
                        {
                            StoredData.SetItem setItem = currentSet[i];
                            if (setItem != null && setItem.Shortname == item.info.shortname && setItem.SkinId == item.skin)
                            {
                                currentSet[i] = null;
                                item.RemoveFromContainer();
                                item.Remove();

                                UpdateReskinCost();
                                break;
                            }
                        }
                    }
                }    
            }
            
            #region UI
            protected override void CreateOverlay()
            {
                BaseContainer root = BaseContainer.Create(UI_OVERLAY, Layer.HudMenu, Anchor.FullStretch, Offset.zero)
                    .WithChildren(parent =>
                    {
                        BaseContainer menu = BaseContainer.Create(parent, Anchor.FullStretch, new Offset(17.5f, 17.5f, -17.5f, -17.5f));
                        BaseContainer container = BaseContainer.Create(menu, Anchor.BottomCenter, new Offset(-580.5f, 64f, 580.5f, 635f));
                        BaseContainer right = BaseContainer.Create(container, Anchor.RightStretch, new Offset(-380f, 28f, 0f, 5f));
                        BaseContainer contents = BaseContainer.Create(right, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 580.11f));
                        BaseContainer loot =  BaseContainer.Create(contents, Anchor.TopLeft, new Offset(0f, -579.5f, 380f, -392.5f));
                        BaseContainer panel = BaseContainer.Create(loot, Anchor.TopLeft, new Offset(0f, -187f, 380f, -8f))
                            .WithName(UI_PANEL_PARENT);
                        
                        // Buttons
                        ImageContainer.Create(panel, Anchor.BottomLeft, new Offset(-2f, -25f, 201f, -2f))
                            .WithStyle(m_PanelStyle)
                            .WithChildren(buttons =>
                            {
                                // Apply set
                                ImageContainer.Create(buttons, Anchor.CenterRight, new Offset(-100f, -10f, -1.5f, 10f))
                                    .WithStyle(m_PageStyle)
                                    .WithChildren(apply =>
                                    {
                                        TextContainer.Create(apply, Anchor.FullStretch, Offset.zero)
                                            .WithText(GetMessage("UI.Apply", Looter))
                                            .WithStyle(m_PageStyle);

                                        ButtonContainer.Create(apply, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, (arg) => ApplySet(), $"{Looter.userID}.applyset");
                                    });

                                // Clear set
                                ImageContainer.Create(buttons, Anchor.CenterLeft, new Offset(1.5f, -10f, 100f, 10f))
                                    .WithStyle(m_SetStyle)
                                    .WithChildren(clear =>
                                    {
                                        TextContainer.Create(clear, Anchor.FullStretch, Offset.zero)
                                            .WithText(GetMessage("UI.Clear", Looter))
                                            .WithStyle(m_SetStyle);

                                        ButtonContainer.Create(clear, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, (arg) => ClearSet(), $"{Looter.userID}.clearset");
                                    });
                            });
                            
                        // Header
                        ImageContainer.Create(panel, Anchor.TopLeft, new Offset(-8f, -51f, 372f, -28f))
                            .WithColor(m_HeaderColor)
                            .WithName(UI_HEADER_PARENT)
                            .WithChildren(header =>
                            {
                                TextContainer.Create(header, Anchor.FullStretch, new Offset(10, 0, 0, 0))
                                    .WithText(GetMessage("UI.Title.Sets", Looter))
                                    .WithStyle(m_TextStyle)
                                    .WithAlignment(TextAnchor.MiddleLeft);

                                // SkinBox
                                ImageContainer.Create(header, Anchor.CenterRight, new Offset(-41.5f, -10f, -1.5f, 10f))
                                    .WithStyle(m_SetStyle)
                                    .WithChildren(prev =>
                                    {
                                        TextContainer.Create(prev, Anchor.FullStretch, Offset.zero)
                                            .WithText(GetMessage("UI.Box", Looter))
                                            .WithStyle(m_SetStyle);

                                        ButtonContainer.Create(prev, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, 
                                                (arg) => Instance.CreateSkinBox<LootHandler>(Looter, null), $"{Looter.userID}.box");
                                    });

                                ImageContainer.Create(header, Anchor.FullStretch, new Offset(0f, 55f, 0f, 105f))
                                    .WithStyle(m_PageStyle)
                                    .WithChildren(help =>
                                    {
                                        TextContainer.Create(help, Anchor.FullStretch, new Offset(5, 5, -5, -5))
                                            .WithStyle(m_PageStyle)
                                            .WithAlignment(TextAnchor.MiddleLeft)
                                            .WithText(GetMessage("Sets.Help", Looter));
                                    });
                            });
                    })
                    .DestroyExisting();

                ChaosUI.Show(Looter, root);
            }

            protected override IEnumerator FillContainer()
            {
                _isFillingContainer = true;
                
                StoredData.SetItem[] currentSet = _sets[_currentPage];
                
                for (int i = 0; i < currentSet.Length; i++)
                {
                    StoredData.SetItem setItem = currentSet[i];
                    if (setItem == null)
                        continue;

                    ItemDefinition itemDefinition = setItem.Definition;
                    if (!itemDefinition)
                    {
                        currentSet[i] = null;
                        continue;
                    }

                    CreateItem(itemDefinition, setItem.SkinId);
                    
                    if (i % 2 == 0)
                        yield return null;
                }

                UpdateReskinCost();
                _isFillingContainer = false;
            }

            private void UpdateReskinCost()
            {
                if (!Configuration.Cost.Enabled)
                    return;
                
                _reskinCost = CalculateCost(_sets[_currentPage], _itemList);
                
                // Cost info
                BaseContainer root = ImageContainer.Create(UI_SEARCH, Layer.HudMenu, Anchor.CenterRight, new Offset(-153f, 13f, 0f, 33f))
                    .WithStyle(m_PanelStyle)
                    .WithParent(UI_HEADER_PARENT)
                    .WithChildren(search =>
                    {
                        TextContainer.Create(search, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                            .WithStyle(m_PanelStyle)
                            .WithText(string.Format(GetMessage("Sets.Cost", Looter), _reskinCost, GetCostType(Looter)));
                    })
                    .DestroyExisting();
                    
                ChaosUI.Show(Looter, root);
            }

            internal static int CalculateCost(StoredData.SetItem[] currentSet, List<Item> itemList)
            {
                int cost = 0;
                for (int i = 0; i < currentSet.Length; i++)
                {
                    StoredData.SetItem setItem = currentSet[i];
                    if (setItem == null)
                        continue;

                    ItemDefinition itemDefinition = setItem.Definition;
                    if (itemDefinition)
                    {
                        if (itemList.Any(x => x.info.shortname == setItem.Shortname && x.skin != setItem.SkinId))
                            cost += GetReskinCost(itemDefinition);
                    }
                }

                return cost;
            }

            private void ApplySet()
            {
                if (_reskinCost > 0 && !ChargePlayer(Looter, _reskinCost))
                {
                    PopupMessage(string.Format(GetMessage("NotEnoughBalanceSet", Looter), _reskinCost, GetCostType(Looter)));
                    return;
                }
                
                StoredData.SetItem[] currentSet = _sets[_currentPage];
                for (int i = 0; i < currentSet.Length; i++)
                {
                    StoredData.SetItem setItem = currentSet[i];
                    if (setItem == null)
                        continue;
                    
                    _itemList.Sort((a, b) => a.skin.CompareTo(b.skin));
                    
                    foreach (Item item in _itemList)
                    {
                        if (item.info.shortname == setItem.Shortname && item.skin != setItem.SkinId)
                        {
                            item.skin = setItem.SkinId;
                            item.MarkDirty();
                            
                            if (Configuration.Other.ApplyWorkshopName && (item.skin != 0UL || Configuration.Skins.ShowSkinIDs))
                                Instance._skinNameLookup.TryGetValue(item.skin, out item.name);
                            
                            BaseEntity heldEntity = item.GetHeldEntity();
                            if (heldEntity)
                            {
                                heldEntity.skinID = setItem.SkinId;
                                heldEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            }
                            
                            if (Looter.svActiveItemID == item.uid)
                            {
                                Looter.UpdateActiveItem(default);
                                
                                int slot = item.position;
                                item.SetParent(null);
                                item.MarkDirty();
                                
                                Looter.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, item.parent, false);
                                
                                item.SetParent(Looter.inventory.containerBelt);
                                item.position = slot;
                                item.MarkDirty();
                                
                                Looter.UpdateActiveItem(item.uid);
                            }
                            
                            break;
                        }
                    }
                }

                UpdateReskinCost();
            }

            private void ClearSet()
            {
                StoredData.SetItem[] currentSet = _sets[_currentPage];
                for (int i = 0; i < currentSet.Length; i++)
                    currentSet[i] = null;
                
                StartCoroutine(RefillContainer());
            }
            #endregion
        }
        #endregion

        #region Spraycan Component
        private class SprayCanWatcher : MonoBehaviour
        {
            private BasePlayer m_Player;

            private static RaycastHit m_RaycastHit;

            private float m_LastPressTime;

            private const int LAYER_MASK = 1 << (int)Rust.Layer.Reserved1 | 1 << (int) Rust.Layer.Construction | 1 << (int) Rust.Layer.Deployed | 1 << (int)Rust.Layer.Physics_Debris;
            private void Awake()
            {
                m_Player = GetComponent<BasePlayer>();
            }

            private void LateUpdate()
            {
                if (UnityEngine.Time.time - m_LastPressTime < 1f)
                    return;
                
                if (m_Player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    Item activeItem = m_Player.GetActiveItem();
                    if (!(activeItem is { skin: SPRAYCAN_SKIN }))
                    {
                        Destroy(this);
                        return;
                    }
                    
                    m_LastPressTime = UnityEngine.Time.time;
                    
                    if (Physics.SphereCast(m_Player.eyes.HeadRay(), 0.1f, out m_RaycastHit, 3f, LAYER_MASK, QueryTriggerInteraction.Ignore))
                    {
                        BaseEntity baseEntity = m_RaycastHit.GetEntity();
                        if (!baseEntity)
                            return;

                        if (baseEntity is WorldItem worldItem)
                        {
                            if (!Instance.CanOpenSkinBox(m_Player))
                                return;
                            
                            if (Configuration.ReskinItemBlocklist.Contains(activeItem.info.shortname))
                            {
                                Instance.ChatMessage(m_Player, "BlockedItem.Item");
                                return;
                            }

                            if (Configuration.ReskinSkinBlocklist.Contains(activeItem.skin))
                            {
                                Instance.ChatMessage(m_Player, "BlockedItem.Skin");
                                return;
                            }
            
                            if (!ChargePlayer(m_Player, Configuration.Cost.Open))
                            {
                                Instance.ChatMessage(m_Player, "NotEnoughBalanceOpen", Configuration.Cost.Open, GetCostType(m_Player));
                                return;
                            }

                            Instance.CreateSkinBox<LootHandler>(m_Player, null, (lootHandler) =>
                            {
                                Item item = worldItem.item;
                                worldItem.RemoveItem();
                                lootHandler.Entity.inventory.Insert(item);
                            });
                        }
                        else
                        {
                            if (!DeployableHandler.GetItemDefinitionForEntity(baseEntity, out _, false))
                                return;
                            
                            Instance.cmdDeployedSkinBox(m_Player, string.Empty, Array.Empty<string>());
                        }
                    }
                }
            }
        }

        [ConsoleCommand("skinbox.spraycan")]
        private void ccmdSkinBoxSprayCan(ConsoleSystem.Arg arg)
        {
            if (!Configuration.Command.SprayCan)
            {
                Debug.Log($"[SkinBox] The console command 'skinbox.spraycan' was used, but spray cans are disabled in the config");
                return;
            }
            
            BasePlayer player = arg.Connection?.player as BasePlayer;
            if (player)
            {
                if (!player.HasPermission(Configuration.Permission.Admin))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }

                if (arg.Args == null || arg.Args.Length == 0)
                {
                    player.GiveItem(CreateSprayCanItem(), BaseEntity.GiveItemReason.PickedUp);
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "skinbox.spraycan <playerNameOrID>");
                return;
            }

            List<BasePlayer> candidates = Pool.Get<List<BasePlayer>>();
            string search = arg.GetString(0);

            foreach (BasePlayer target in BasePlayer.activePlayerList)
            {
                if (target.UserIDString == search)
                {
                    candidates.Clear();
                    candidates.Add(target);
                    break;
                }

                if (target.displayName.Contains(search, CompareOptions.OrdinalIgnoreCase))
                    candidates.Add(target);
            }

            if (candidates.Count == 0)
            {
                SendReply(arg, $"No players found with the name or ID '{search}'");
                Pool.FreeUnmanaged(ref candidates);
                return;
            }
            
            if (candidates.Count > 1)
            {
                SendReply(arg, $"Multiple players found with the name or ID '{search}'");
                Pool.FreeUnmanaged(ref candidates);
                return;
            }
            
            candidates[0].GiveItem(CreateSprayCanItem(), BaseEntity.GiveItemReason.PickedUp);
            SendReply(arg, $"Gave a SkinBox Spray Can to {candidates[0].displayName}");
            Pool.FreeUnmanaged(ref candidates);
        }

        private Item CreateSprayCanItem()
        {
            const string SPRAYCAN_ITEM = "spraycan";
            Item item = ItemManager.CreateByName(SPRAYCAN_ITEM, 1, SPRAYCAN_SKIN);
            item.name = "SkinBox Spray Can";
            return item;
        }
        #endregion
        
        #region UI
        private const string UI_OVERLAY = "skinbox.overlay";
        private const string UI_FAVOURITES = "skinbox.favourites";
        private const string UI_PAGE = "skinbox.pages";
        private const string UI_SEARCH = "skinbox.search";
        private const string UI_POPUP = "skinbox.popup";

        private const string UI_PANEL_PARENT = "skinbox.panel";
        private const string UI_HEADER_PARENT = "skinbox.header";

        private const string MAGNIFY_URL = "https://chaoscode.io/oxide/Images/magnifyingglass.png";

        private static CommandCallbackHandler m_CallbackHandler;

        private static Style m_TextStyle;
        private static Style m_PanelStyle;
        private static Style m_SetStyle;
        private static Style m_PageStyle;
        private static Style m_InputStyle;

        private static Color m_HeaderColor;

        private void InitializeUI()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);
            
            m_HeaderColor = new Color(0.1019608f, 0.1019608f, 0.1019608f, 1f);
            
            m_TextStyle = new Style
            {
                FontColor = new Color(0.9686275f, 0.9215686f, 0.8823529f, 0.7843137f),
                FontSize = 13,
                Alignment = TextAnchor.MiddleCenter
            };

            m_PanelStyle = new Style
            {
                ImageColor = new Color(0.9686275f, 0.9215686f, 0.8823529f, 0.03921569f),
                Material = Materials.GreyOut,
                FontSize = 13,
                FontColor = new Color(0.9686275f, 0.9215686f, 0.8823529f, 0.7843137f),
                Alignment = TextAnchor.MiddleCenter
            };

            m_SetStyle = new Style
            {
                ImageColor = new Color(0.6132076f, 0.2283664f, 0.1595001f, 1f),
                Material = Materials.GreyOut,
                FontColor = new Color(0.9245283f, 0.5805449f, 0.493414f, 1f),
                FontSize = 13,
                Alignment = TextAnchor.MiddleCenter
            };

            m_PageStyle = new Style
            {
                ImageColor = new Color(0.4509804f, 0.5529412f, 0.2705882f, 1f),
                Material = Materials.GreyOut,
                FontColor = new Color(0.7098039f, 0.8666667f, 0.4352941f, 1f),
                FontSize = 13,
                Alignment = TextAnchor.MiddleCenter
            };

            m_InputStyle = new Style
            {
                FontColor = new Color(0.9686275f, 0.9215686f, 0.8823529f, 0.7843137f),
                FontSize = 13,
                Alignment = TextAnchor.MiddleLeft
            };
        }
        #endregion
        
        #region Chat Commands   
        private bool CanOpenSkinBox(BasePlayer player)
        {
            if (_apiKeyMissing)
            {
                SendAPIMissingWarning();
                ChatMessage(player, "NoAPIKey");
                return false;
            }

            if (!_skinsLoaded)
            {
                ChatMessage(player, "SkinsLoading");
                return false;
            }

            if (player.inventory.loot.IsLooting())
                return false;

            if (Configuration.Other.RequireBuildingPrivilege && !player.IsBuildingAuthed())
            {
                ChatMessage(player, "NoBuildingAuth");
                return false;
            }

            if (!player.IsAdmin && !player.HasPermission(Configuration.Permission.Use))
            {
                ChatMessage(player, "NoPermission");
                return false;
            }

            if (IsOnCooldown(player, out double cooldownRemaining))
            {
                ChatMessage(player, "CooldownTime", Mathf.RoundToInt((float)cooldownRemaining));
                return false;
            }

            string result = Interface.Call<string>("SB_CanUseSkinBox", player);
            if (!string.IsNullOrEmpty(result))
            {
                ChatMessage(player, result);
                return false;
            }

            return true;
        }
        private void cmdSkinBox(BasePlayer player, string command, string[] args)
        {
            if (!CanOpenSkinBox(player))
                return;
            
            if (!ChargePlayer(player, Configuration.Cost.Open))
            {
                ChatMessage(player, "NotEnoughBalanceOpen", Configuration.Cost.Open, GetCostType(player));
                return;
            }

            if (args.Length > 0)
            {
                if (ulong.TryParse(args[0], out ulong targetSkin))
                {
                    Item activeItem = player.GetActiveItem();
                    if (activeItem == null)
                    {
                        ChatMessage(player, "NoItemInHands");
                        return;
                    }

                    if (targetSkin == activeItem.skin)
                    {
                        ChatMessage(player, "TargetSkinIsCurrentSkin");
                        return;
                    }

                    if (Configuration.ReskinItemBlocklist.Contains(activeItem.info.shortname))
                    {
                        ChatMessage(player, "BlockedItem.Item");
                        return;
                    }

                    if (Configuration.ReskinSkinBlocklist.Contains(activeItem.skin))
                    {
                        ChatMessage(player, "BlockedItem.Skin");
                        return;
                    }

                    List<ulong> skins = Pool.Get<List<ulong>>();

                    GetSkinsFor(player, activeItem.info.shortname, ref skins);

                    bool contains = skins.Contains(targetSkin);

                    Pool.FreeUnmanaged(ref skins);

                    if (!contains && targetSkin != 0UL)
                    {
                        ChatMessage(player, "TargetSkinNotInList");
                        return;
                    }

                    bool skinChanged = targetSkin != 0UL && targetSkin != activeItem.skin;

                    if (skinChanged && !ChargePlayer(player, activeItem.info.category))
                    {                        
                        ChatMessage(player, "NotEnoughBalanceTake", activeItem.info.displayName.english, GetCostType(player));
                        return;
                    }

                    string result = Interface.Call<string>("SB_CanReskinItem", player, activeItem, targetSkin);
                    if (!string.IsNullOrEmpty(result))
                    {
                        ChatMessage(player, result);
                        return;
                    }

                    ChatMessage(player, "ApplyingSkin", targetSkin);

                    activeItem.skin = targetSkin;
                    activeItem.MarkDirty();
                    
                    if (activeItem.skin != 0UL || Configuration.Skins.ShowSkinIDs)
                        Instance._skinNameLookup.TryGetValue(activeItem.skin, out activeItem.name);

                    BaseEntity heldEntity = activeItem.GetHeldEntity();
                    if (heldEntity != null)
                    {
                        heldEntity.skinID = targetSkin;
                        heldEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    }

                    player.UpdateActiveItem(default);
                                
                    int slot = activeItem.position;
                    activeItem.SetParent(null);
                    activeItem.MarkDirty();
                                
                    player.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, activeItem.parent, false);
                                
                    activeItem.SetParent(player.inventory.containerBelt);
                    activeItem.position = slot;
                    activeItem.MarkDirty();
                                
                    player.UpdateActiveItem(activeItem.uid);

                    if (Configuration.Cooldown.ActivateOnTake)
                        Instance.ApplyCooldown(player);
                    
                    return;
                }
            }

            timer.In(0.2f, () => CreateSkinBox<LootHandler>(player, null));
        }

        private void cmdDeployedSkinBox(BasePlayer player, string command, string[] args)
        {
            if (!CanOpenSkinBox(player))
                return;

            if (!player.CanBuild())
            {
                ChatMessage(player, "ReskinError.NoAuth");
                return;
            }

            BaseEntity entity = DeployableHandler.FindReskinTarget(player);
            if (!entity || entity.IsDestroyed)
            {
                ChatMessage(player, "ReskinError.NoTarget");
                return;
            }

            if (!DeployableHandler.CanEntityBeRespawned(entity, out string reason))
            {
                ChatMessage(player, reason);
                return;
            }

            if (!DeployableHandler.GetItemDefinitionForEntity(entity, out ItemDefinition itemDefinition, false))
            {
                ChatMessage(player, "NoDefinitionForEntity");
                return;
            }

            string shortname = GetRedirectedShortname(itemDefinition.shortname);
            if (!Configuration.Skins.UseRedirected && !shortname.Equals(itemDefinition.shortname, StringComparison.OrdinalIgnoreCase))
            {
                ChatMessage(player, "RedirectsDisabled");
                return;
            }

            List<ulong> skins = Pool.Get<List<ulong>>();
            GetSkinsFor(player, shortname, ref skins);

            if (skins.Count == 0)
            {
                ChatMessage(player, "NoSkinsForItem");
                return;
            }

            if (!ChargePlayer(player, Configuration.Cost.Open))
            {
                ChatMessage(player, "NotEnoughBalanceOpen", Configuration.Cost.Open, GetCostType(player));
                return;
            }

            string result = Interface.Call<string>("SB_CanReskinDeployable", player, entity, itemDefinition);
            if (!string.IsNullOrEmpty(result))
            {
                ChatMessage(player, result);
                return;
            }

            if (args.Length > 0)
            {
                if (ulong.TryParse(args[0], out ulong targetSkin))
                {
                    if (targetSkin == entity.skinID)
                    {
                        ChatMessage(player, "TargetSkinIsCurrentSkin");
                        return;
                    }

                    if (!skins.Contains(targetSkin) && targetSkin != 0UL)
                    {
                        ChatMessage(player, "TargetSkinNotInList");
                        return;
                    }

                    Item item = ItemManager.CreateByName(shortname, 1, targetSkin);

                    bool skinChanged = item.info != itemDefinition || (item.skin != entity.skinID);
                    bool wasSuccess = false;

                    if (!skinChanged)
                        goto IGNORE_RESKIN;

                    if (skinChanged && !ChargePlayer(player, itemDefinition.category))
                    {                        
                        ChatMessage(player, "NotEnoughBalanceTake", item.info.displayName.english, GetCostType(player));
                        goto IGNORE_RESKIN;
                    }

                    string result2 = Interface.Call<string>("SB_CanReskinDeployableWith", player, entity, itemDefinition, item.skin);
                    if (!string.IsNullOrEmpty(result2))
                    {
                        ChatMessage(player, result2);
                        goto IGNORE_RESKIN;
                    }

                    wasSuccess = DeployableHandler.ReskinEntity(player, entity, itemDefinition, item);

                    ChatMessage(player, "ApplyingSkin", targetSkin);

                    IGNORE_RESKIN:
                    item.Remove(0f);

                    if (wasSuccess && Configuration.Cooldown.ActivateOnTake)
                        Instance.ApplyCooldown(player);

                    return;
                }
            }

            timer.In(0.2f, () => CreateSkinBox<DeployableHandler>(player, new DeployableHandler.ReskinTarget(entity, itemDefinition, skins)));
        }
        
        private void cmdSkinSetBox(BasePlayer player, string command, string[] args)
        {
            if (!CanOpenSkinBox(player))
                return;
            
            if (!player.HasPermission(Configuration.Permission.Sets))
            {
                ChatMessage(player, "NoPermission.Sets");
                return;
            }

            if (args.Length > 0 && int.TryParse(args[0], out int setId))
            {
                List<StoredData.SetItem[]> sets = Data.GetSkinSetsForPlayer(player);
                
                if (setId < 1 || setId > sets.Count)
                {
                    ChatMessage(player, "Sets.Error.InvalidSetID", sets.Count);
                    return;
                }

                StoredData.SetItem[] currentSet = sets[setId - 1];
                if (currentSet.All(x => x == null))
                {
                    ChatMessage(player, "Sets.Error.EmptySet", setId);
                    return;
                }

                List<Item> itemList = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(itemList);

                itemList.RemoveAll((item) =>
                {
                    string result = Interface.Call<string>("SB_CanReskinItem", player, item, item.skin);
                    return !string.IsNullOrEmpty(result);
                });
                
                int reskinCost = Configuration.Cost.Enabled ? SkinSetHandler.CalculateCost(currentSet, itemList) : 0;
               
                if (reskinCost > 0 && !ChargePlayer(player, reskinCost))
                {
                    player.LocalizedMessage(this, "NotEnoughBalanceSet", reskinCost, GetCostType(player));
                    Pool.FreeUnmanaged(ref itemList);
                    return;
                }

                int appliedItemCount = 0;
                
                for (int i = 0; i < currentSet.Length; i++)
                {
                    StoredData.SetItem setItem = currentSet[i];
                    if (setItem == null)
                        continue;
                    
                    itemList.Sort((a, b) => a.skin.CompareTo(b.skin));
                    
                    foreach (Item item in itemList)
                    {
                        if (item.info.shortname == setItem.Shortname && item.skin != setItem.SkinId)
                        {
                            item.skin = setItem.SkinId;
                            item.MarkDirty();

                            BaseEntity heldEntity = item.GetHeldEntity();
                            if (heldEntity)
                            {
                                heldEntity.skinID = setItem.SkinId;
                                heldEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            }

                            if (Configuration.Other.ApplyWorkshopName && (item.skin != 0UL || Configuration.Skins.ShowSkinIDs))
                                Instance._skinNameLookup.TryGetValue(item.skin, out item.name);
                            
                            if (player.svActiveItemID == item.uid)
                            {
                                player.UpdateActiveItem(default);
                                
                                int slot = item.position;
                                item.SetParent(null);
                                item.MarkDirty();
                                
                                player.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, item.parent, false);
                                
                                item.SetParent(player.inventory.containerBelt);
                                item.position = slot;
                                item.MarkDirty();
                                
                                player.UpdateActiveItem(item.uid);
                            }

                            appliedItemCount++;
                            
                            break;
                        }
                    }
                }
                
                Pool.FreeUnmanaged(ref itemList);
                
                if (appliedItemCount == 0)
                    player.LocalizedMessage(this, "Sets.NoItems");
                else
                {
                    if (reskinCost == 0)
                        player.LocalizedMessage(this, "Sets.Applied", setId, appliedItemCount);
                    else player.LocalizedMessage(this, "Sets.Applied.Cost", setId, appliedItemCount, reskinCost, GetCostType(player));
                }

                return;
            }
            
            timer.In(0.2f, () => CreateSkinBox<SkinSetHandler>(player, null));
        }
        #endregion
      
        #region Console Commands
        [ConsoleCommand("skinbox.cmds")]
        private void cmdListCmds(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
                return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n> SkinBox command overview <");

            TextTable textTable = new TextTable();
            textTable.AddColumn("Command");
            textTable.AddColumn("Description");
            textTable.AddRow(new[] { "skinbox.addskin", "Add one or more skin-id's to the workshop skin list" });
            textTable.AddRow(new[] { "skinbox.removeskin", "Remove one or more skin-id's from the workshop skin list" });
            textTable.AddRow(new[] { "skinbox.addvipskin", "Add one or more skin-id's to the workshop skin list for the specified permission" });
            textTable.AddRow(new[] { "skinbox.removevipskin", "Remove one or more skin-id's from the workshop skin list for the specified permission" });
            textTable.AddRow(new[] { "skinbox.addexcluded", "Add one or more skin-id's to the exclusion list (for players)" });
            textTable.AddRow(new[] { "skinbox.removeexcluded", "Remove one or more skin-id's from the exclusion list" });
            textTable.AddRow(new[] { "skinbox.addcollectionexclusion", "Add a skin collection to the exclusion list (for players)" });
            textTable.AddRow(new[] { "skinbox.removecollectionexclusion", "Remove a skin collection from the exclusion list (for players)" });
            textTable.AddRow(new[] { "skinbox.addcollection", "Adds a whole skin-collection to the workshop skin list"});
            textTable.AddRow(new[] { "skinbox.removecollection", "Removes a whole collection from the workshop skin list" });
            textTable.AddRow(new[] { "skinbox.addvipcollection", "Adds a whole skin-collection to the workshop skin list for the specified permission" });
            textTable.AddRow(new[] { "skinbox.removevipcollection", "Removes a whole collection from the workshop skin list for the specified permission" });

            sb.AppendLine(textTable.ToString());
            SendReply(arg, sb.ToString());
        }

        #region Add/Remove Skins
        [ConsoleCommand("skinbox.addskin")]
        private void consoleAddSkin(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in one or more workshop skin ID's");
                return;
            }

            _skinsToVerify.Clear();

            for (int i = 0; i < arg.Args.Length; i++)
            {
                if (!ulong.TryParse(arg.Args[i], out ulong fileId))
                {
                    SendReply(arg, $"Ignored '{arg.Args[i]}' as it's not a number");
                    continue;
                }
                else
                {
                    if (arg.Args[i].Length < 9 || arg.Args[i].Length > 10)
                    {
                        SendReply(arg, $"Ignored '{arg.Args[i]}' as it is not the correct length (9 - 10 digits)");
                        continue;
                    }

                    _skinsToVerify.Add(fileId);
                }
            }

            if (_skinsToVerify.Count > 0)
                SendWorkshopQuery(0, 0, arg);
            else SendReply(arg, "No valid skin ID's were entered");
        }

        [ConsoleCommand("skinbox.removeskin")]
        private void consoleRemoveSkin(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in one or more workshop skin ID's");
                return;
            }

            _skinsToVerify.Clear();

            for (int i = 0; i < arg.Args.Length; i++)
            {
                if (!ulong.TryParse(arg.Args[i], out ulong fileId))
                {
                    SendReply(arg, $"Ignored '{arg.Args[i]}' as it's not a number");
                    continue;
                }
                else
                {
                    if (arg.Args[i].Length < 9 || arg.Args[i].Length > 10)
                    {
                        SendReply(arg, $"Ignored '{arg.Args[i]}' as it is not the correct length (9 - 10 digits)");
                        continue;
                    }

                    _skinsToVerify.Add(fileId);
                }
            }

            if (_skinsToVerify.Count > 0)
                RemoveSkins(arg);
            else SendReply(arg, "No valid skin ID's were entered");
        }

        [ConsoleCommand("skinbox.addvipskin")]
        private void consoleAddVIPSkin(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 2)
            {
                SendReply(arg, "You need to type in a permission and one or more workshop skin ID's");
                return;
            }

            string perm = arg.Args[0];
            if (!Configuration.Permission.Custom.ContainsKey(perm))
            {
                SendReply(arg, $"The permission {perm} does not exist in the custom permission section of the config");
                return;
            }

            _skinsToVerify.Clear();
            
            for (int i = 1; i < arg.Args.Length; i++)
            {
                if (!ulong.TryParse(arg.Args[i], out ulong fileId))
                {
                    SendReply(arg, $"Ignored '{arg.Args[i]}' as it's not a number");
                    continue;
                }
                else
                {
                    if (arg.Args[i].Length < 9 || arg.Args[i].Length > 10)
                    {
                        SendReply(arg, $"Ignored '{arg.Args[i]}' as it is not the correct length (9 - 10 digits)");
                        continue;
                    }

                    _skinsToVerify.Add(fileId);
                }
            }

            if (_skinsToVerify.Count > 0)
                SendWorkshopQuery(0, 0, arg, perm);
            else SendReply(arg, "No valid skin ID's were entered");
        }

        [ConsoleCommand("skinbox.removevipskin")]
        private void consoleRemoveVIPSkin(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 2)
            {
                SendReply(arg, "You need to type in a permission and one or more workshop skin ID's");
                return;
            }

            string perm = arg.Args[0];
            if (!Configuration.Permission.Custom.ContainsKey(perm))
            {
                SendReply(arg, $"The permission {perm} does not exist in the custom permission section of the config");
                return;
            }

            _skinsToVerify.Clear();

            for (int i = 1; i < arg.Args.Length; i++)
            {
                if (!ulong.TryParse(arg.Args[i], out ulong fileId))
                {
                    SendReply(arg, $"Ignored '{arg.Args[i]}' as it's not a number");
                    continue;
                }
                else
                {
                    if (arg.Args[i].Length < 9 || arg.Args[i].Length > 10)
                    {
                        SendReply(arg, $"Ignored '{arg.Args[i]}' as it is not the correct length (9 - 10 digits)");
                        continue;
                    }

                    _skinsToVerify.Add(fileId);
                }
            }

            if (_skinsToVerify.Count > 0)
                RemoveSkins(arg, perm);
            else SendReply(arg, "No valid skin ID's were entered");
        }

        [ConsoleCommand("skinbox.validatevipskins")]
        private void consoleValidateVIPSkins(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            _skinsToVerify.Clear();

            foreach (List<ulong> list in Configuration.Permission.Custom.Values)
            {
                foreach (ulong skinId in list)
                {
                    if (skinId == 0UL)
                        continue;

                    ItemSkinDirectory.Skin skin = ItemSkinDirectory.Instance.skins.FirstOrDefault<ItemSkinDirectory.Skin>((ItemSkinDirectory.Skin x) => x.id == (int)skinId);
                    if (skin.invItem != null)
                        continue;

                    if (!Configuration.SkinList.Values.Any(x => x.Contains(skinId)))
                        _skinsToVerify.Add(skinId);
                }
            }

            if (_skinsToVerify.Count > 0)
            {
                SendReply(arg, $"Found {_skinsToVerify.Count} permission based skin IDs that are not in the skin list. Sending workshop request");
                SendWorkshopQuery(0, 0, arg);
            }
            else SendReply(arg, "No permission based skin ID's are missing from the skin list");
        }
        #endregion

        #region Add/Remove Collections
        [ConsoleCommand("skinbox.addcollection")]
        private void consoleAddCollection(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in a skin collection ID");
                return;
            }

            if (!ulong.TryParse(arg.Args[0], out ulong collectionId))
            {
                SendReply(arg, $"{arg.Args[0]} is an invalid collection ID");
                return;
            }

            SendWorkshopCollectionQuery(collectionId, CollectionAction.AddSkin, 0, arg);
        }

        [ConsoleCommand("skinbox.removecollection")]
        private void consoleRemoveCollection(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in a skin collection ID");
                return;
            }

            if (!ulong.TryParse(arg.Args[0], out ulong collectionId))
            {
                SendReply(arg, $"{arg.Args[0]} is an invalid collection ID");
                return;
            }

            SendWorkshopCollectionQuery(collectionId, CollectionAction.RemoveSkin, 0, arg);
        }

        [ConsoleCommand("skinbox.addvipcollection")]
        private void consoleAddVIPCollection(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 2)
            {
                SendReply(arg, "You need to type in a permission and one or more workshop skin ID's");
                return;
            }

            string perm = arg.Args[0];
            if (!Configuration.Permission.Custom.ContainsKey(perm))
            {
                SendReply(arg, $"The permission {perm} does not exist in the custom permission section of the config");
                return;
            }

            if (!ulong.TryParse(arg.Args[1], out ulong collectionId))
            {
                SendReply(arg, $"{arg.Args[1]} is an invalid collection ID");
                return;
            }

            SendWorkshopCollectionQuery(collectionId, CollectionAction.AddSkin, 0, arg, perm);
        }

        [ConsoleCommand("skinbox.removevipcollection")]
        private void consoleRemoveVIPCollection(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 2)
            {
                SendReply(arg, "You need to type in a permission and one or more workshop skin ID's");
                return;
            }

            string perm = arg.Args[0];
            if (!Configuration.Permission.Custom.ContainsKey(perm))
            {
                SendReply(arg, $"The permission {perm} does not exist in the custom permission section of the config");
                return;
            }

            if (!ulong.TryParse(arg.Args[1], out ulong collectionId))
            {
                SendReply(arg, $"{arg.Args[1]} is an invalid collection ID");
                return;
            }

            SendWorkshopCollectionQuery(collectionId, CollectionAction.RemoveSkin, 0, arg, perm);
        }
        #endregion

        #region Blacklisted Skins
        [ConsoleCommand("skinbox.addexcluded")]
        private void consoleAddExcluded(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in one or more skin ID's");
                return;
            }

            int count = 0;

            for (int i = 0; i < arg.Args.Length; i++)
            {
                if (!ulong.TryParse(arg.Args[i], out ulong skinId))
                {
                    SendReply(arg, $"Ignored '{arg.Args[i]}' as it's not a number");
                    continue;
                }
                else
                {
                    Configuration.Blacklist.Add(skinId);
                    count++;
                }
            }

            if (count > 0)
            {
                SaveConfiguration();
                SendReply(arg, $"Blacklisted {count} skin ID's");
            }
            else SendReply(arg, "No skin ID's were added to the blacklist");
        }

        [ConsoleCommand("skinbox.removeexcluded")]
        private void consoleRemoveExcluded(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in one or more skin ID's");
                return;
            }

            int count = 0;

            for (int i = 0; i < arg.Args.Length; i++)
            {
                if (!ulong.TryParse(arg.Args[i], out ulong skinId))
                {
                    SendReply(arg, $"Ignored '{arg.Args[i]}' as it's not a number");
                    continue;
                }
                else
                {
                    if (Configuration.Blacklist.Contains(skinId))
                    {
                        Configuration.Blacklist.Remove(skinId);
                        SendReply(arg, $"The skin ID {skinId} is not on the blacklist");
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                SaveConfiguration();
                SendReply(arg, $"Removed {count} skin ID's from the blacklist");
            }
            else SendReply(arg, "No skin ID's were removed from the blacklist");
        }

        [ConsoleCommand("skinbox.addcollectionexclusion")]
        private void consoleAddCollectionExclusion(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in a collection ID");
                return;
            }

            if (!ulong.TryParse(arg.Args[0], out ulong collectionId))
            {
                SendReply(arg, $"{arg.Args[0]} is an invalid collection ID");
                return;
            }

            SendWorkshopCollectionQuery(collectionId, CollectionAction.ExcludeSkin, 0, arg);
        }

        [ConsoleCommand("skinbox.removecollectionexclusion")]
        private void consoleRemoveCollectionExclusion(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), Configuration.Permission.Admin))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "You need to type in a collection ID");
                return;
            }

            if (!ulong.TryParse(arg.Args[0], out ulong collectionId))
            {
                SendReply(arg, $"{arg.Args[0]} is an invalid collection ID");
                return;
            }

            SendWorkshopCollectionQuery(collectionId, CollectionAction.RemoveExludeSkin, 0, arg);
        }
        #endregion

        [ConsoleCommand("skinbox.open")]
        private void consoleSkinboxOpen(ConsoleSystem.Arg arg)
        {
            if (arg == null)
                return;

            if (!_skinsLoaded)
            {
                SendReply(arg, "SkinBox is still loading skins. Please wait");
                return;
            }

            if (arg.Connection == null)
            {
                if (arg.Args == null || arg.Args.Length == 0)
                {
                    SendReply(arg, "This command requires a Steam ID of the target user");
                    return;
                }

                if (!ulong.TryParse(arg.Args[0], out ulong targetUserID) || !Oxide.Core.ExtensionMethods.IsSteamId(targetUserID))
                {
                    SendReply(arg, "Invalid Steam ID entered");
                    return;
                }

                BasePlayer targetPlayer = BasePlayer.FindByID(targetUserID);
                if (targetPlayer == null || !targetPlayer.IsConnected)
                {
                    SendReply(arg, $"Unable to find a player with the specified Steam ID");
                    return;
                }

                if (targetPlayer.IsDead())
                {
                    SendReply(arg, $"The specified player is currently dead");
                    return;
                }

                if (!targetPlayer.inventory.loot.IsLooting())
                    CreateSkinBox<LootHandler>(targetPlayer, null);
            }
            else if (arg.Connection != null && arg.Connection.player != null)
            {
                BasePlayer player = arg.Player();

                cmdSkinBox(player, string.Empty, Array.Empty<string>());
            }
        }
        #endregion

        #region API
        private bool IsSkinBoxPlayer(ulong playerId) => _activeSkinBoxes.ContainsKey(playerId);
        #endregion

        #region Config        
        private static ConfigData Configuration;

        private class ConfigData : BaseConfigData
        {
            [JsonProperty(PropertyName = "Skin Options")]
            public SkinOptions Skins { get; set; }

            [JsonProperty(PropertyName = "Cooldown Options")]
            public CooldownOptions Cooldown { get; set; }

            [JsonProperty(PropertyName = "Command Options")]
            public CommandOptions Command { get; set; }

            [JsonProperty(PropertyName = "Permission Options")]
            public PermissionOptions Permission { get; set; }

            [JsonProperty(PropertyName = "Usage Cost Options")]
            public CostOptions Cost { get; set; }

            [JsonProperty(PropertyName = "Other Options")]
            public OtherOptions Other { get; set; }

            [JsonProperty(PropertyName = "Favourites Options")]
            public FavouritesOptions Favourites { get; set; }
            
            [JsonProperty(PropertyName = "Set Options")]
            public SetOptions Sets { get; set; }

            [JsonProperty(PropertyName = "Imported Workshop Skins")]
            public Hash<string, HashSet<ulong>> SkinList { get; set; }

            [JsonProperty(PropertyName = "Blacklisted Skin ID's")]
            public HashSet<ulong> Blacklist { get; set; }

            [JsonProperty(PropertyName = "Prevent items with the following Skin ID's from being re-skinned")]
            public HashSet<ulong> ReskinSkinBlocklist { get; set; } = new HashSet<ulong>();
            
            [JsonProperty(PropertyName = "Prevent these items from being re-skinned (shortname)")]
            public HashSet<string> ReskinItemBlocklist { get; set; } = new HashSet<string>();

            public class SkinOptions
            {
                [JsonProperty(PropertyName = "Maximum number of approved skins allowed for each item (-1 is unlimited)")]
                public int ApprovedLimit { get; set; }

                [JsonProperty(PropertyName = "Maximum number of pages viewable")]
                public int MaximumPages { get; set; }

                [JsonProperty(PropertyName = "Include approved skins")]
                public bool UseApproved { get; set; }

                [JsonProperty(PropertyName = "Only use approved skins for these item shortnames (only used when 'Include approved skins' is disabled)")]
                public HashSet<string> ApprovedItems { get; set; } = new HashSet<string>();
                
                [JsonProperty(PropertyName = "Approved skin timeout (seconds)")]
                public int ApprovedTimeout { get; set; }

                [JsonProperty(PropertyName = "Include manually imported workshop skins")]
                public bool UseWorkshop { get; set; }

                [JsonProperty(PropertyName = "Remove approved skin ID's from config workshop skin list")]
                public bool RemoveApproved { get; set; }

                [JsonProperty(PropertyName = "Include redirected skins")]
                public bool UseRedirected { get; set; }

                [JsonProperty(PropertyName = "Show skin ID's in the name for admins")]
                public bool ShowSkinIDs { get; set; }

                [JsonProperty(PropertyName = "Skin list order (Config, ConfigReversed, Alphabetical)")]
                public SortBy Sorting { get; set; }

                [JsonProperty(PropertyName = "Steam API key for workshop skins (https://steamcommunity.com/dev/apikey)")]
                public string APIKey { get; set; }
            }

            public class CooldownOptions
            {
                [JsonProperty(PropertyName = "Enable cooldowns")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Cooldown time start's when a item is removed from the box")]
                public bool ActivateOnTake { get; set; }

                [JsonProperty(PropertyName = "Length of cooldown time (seconds)")]
                public int Time { get; set; }
            }

            public class PermissionOptions
            {
                [JsonProperty(PropertyName = "Permission required to use SkinBox")]
                public string Use { get; set; }

                [JsonProperty(PropertyName = "Permission required to reskin deployed items")]
                public string UseDeployed { get; set; }

                [JsonProperty(PropertyName = "Permission required to use admin functions")]
                public string Admin { get; set; }

                [JsonProperty(PropertyName = "Permission that bypasses usage costs")]
                public string NoCost { get; set; }

                [JsonProperty(PropertyName = "Permission that bypasses usage cooldown")]
                public string NoCooldown { get; set; }

                [JsonProperty(PropertyName = "Permission required to skin weapons")]
                public string Weapon { get; set; }

                [JsonProperty(PropertyName = "Permission required to skin deployables")]
                public string Deployable { get; set; }

                [JsonProperty(PropertyName = "Permission required to skin attire")]
                public string Attire { get; set; }

                [JsonProperty(PropertyName = "Permission required to view approved skins")]
                public string Approved { get; set; }
                
                [JsonProperty(PropertyName = "Permission required to use skin sets")]
                public string Sets { get; set; }

                [JsonProperty(PropertyName = "Custom permissions per skin")]
                public Hash<string, List<ulong>> Custom { get; set; }

                public void RegisterPermissions(Permission permission, Plugin plugin)
                {
                    permission.RegisterPermission(Use, plugin);

                    if (!permission.PermissionExists(UseDeployed, plugin))
                        permission.RegisterPermission(UseDeployed, plugin);

                    if (!permission.PermissionExists(Admin, plugin))
                        permission.RegisterPermission(Admin, plugin);

                    if (!permission.PermissionExists(NoCost, plugin))
                        permission.RegisterPermission(NoCost, plugin);

                    if (!permission.PermissionExists(NoCooldown, plugin))
                        permission.RegisterPermission(NoCooldown, plugin);

                    if (!permission.PermissionExists(Weapon, plugin))
                        permission.RegisterPermission(Weapon, plugin);

                    if (!permission.PermissionExists(Deployable, plugin))
                        permission.RegisterPermission(Deployable, plugin);

                    if (!permission.PermissionExists(Attire, plugin))
                        permission.RegisterPermission(Attire, plugin);

                    if (!permission.PermissionExists(Approved, plugin))
                        permission.RegisterPermission(Approved, plugin);

                    if (!permission.PermissionExists(Sets, plugin))
                        permission.RegisterPermission(Sets, plugin);
                    
                    foreach (string perm in Custom.Keys)
                    {
                        if (!permission.PermissionExists(perm, plugin))
                            permission.RegisterPermission(perm, plugin);
                    }
                }

                public void ReverseCustomSkinPermissions(ref Hash<ulong, string> list)
                {
                    foreach (KeyValuePair<string, List<ulong>> kvp in Custom)
                    {
                        for (int i = 0; i < kvp.Value.Count; i++)
                        {
                            list[kvp.Value[i]] = kvp.Key;
                        }
                    }
                }
            }

            public class CommandOptions
            {
                [JsonProperty(PropertyName = "Commands to open the SkinBox")]
                public string[] Commands { get; set; }

                [JsonProperty(PropertyName = "Commands to open the deployed item SkinBox")]
                public string[] DeployedCommands { get; set; }
                
                [JsonProperty(PropertyName = "Commands to open the skin set box")]
                public string[] SetCommands { get; set; }
                
                [JsonProperty(PropertyName = "Allow skinning via Skinbox spray can")]
                public bool SprayCan { get; set; }

                internal void RegisterCommands(Game.Rust.Libraries.Command cmd, Plugin plugin)
                {
                    for (int i = 0; i < Commands.Length; i++)                    
                        cmd.AddChatCommand(Commands[i], plugin, "cmdSkinBox");

                    for (int i = 0; i < DeployedCommands.Length; i++)
                        cmd.AddChatCommand(DeployedCommands[i], plugin, "cmdDeployedSkinBox");
                    
                    for (int i = 0; i < SetCommands.Length; i++)                    
                        cmd.AddChatCommand(SetCommands[i], plugin, "cmdSkinSetBox");
                }
            }

            public class CostOptions
            {
                [JsonProperty(PropertyName = "Enable usage costs")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Currency used for usage costs (Scrap, Economics, ServerRewards)")]
                public CostType Currency { get; set; }

                [JsonProperty(PropertyName = "Cost to open the SkinBox")]
                public int Open { get; set; }

                [JsonProperty(PropertyName = "Cost to skin deployables")]
                public int Deployable { get; set; }

                [JsonProperty(PropertyName = "Cost to skin attire")]
                public int Attire { get; set; }

                [JsonProperty(PropertyName = "Cost to skin weapons")]
                public int Weapon { get; set; }
            }  
            
            public class OtherOptions
            {
                [JsonProperty(PropertyName = "Allow stacked items")]
                public bool AllowStacks { get; set; }

                [JsonProperty(PropertyName = "Auth-level required to view blacklisted skins")]
                public int BlacklistAuth { get; set; }
                
                [JsonProperty(PropertyName = "Spray can max uses")]
                public int SprayCanMaxUses { get; set; }
                
                [JsonProperty(PropertyName = "Only allow skinning of items in areas where the player has building privilege")]
                public bool RequireBuildingPrivilege { get; set; }
                
                [JsonProperty(PropertyName = "Apply skin workshop name to item if available (false keeps the name of the input item)")]
                public bool ApplyWorkshopName { get; set; }
            }

            public class FavouritesOptions
            {
                [JsonProperty(PropertyName = "Enable favourites system")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Enable purging of favourites data")]
                public bool Purge { get; set; }

                [JsonProperty(PropertyName = "Purge user favourites that haven't been online for x amount of days")]
                public int PurgeDays { get; set; }
            }
            
            public class SetOptions
            {
                [JsonProperty(PropertyName = "Enable set system")]
                public bool Enabled { get; set; }
                
                [JsonProperty(PropertyName = "Number of skin sets allowed")]
                public int NumberSets { get; set; }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = ConfigurationData as ConfigData;
        }

        protected override void PrepareConfigFile(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Skins = new ConfigData.SkinOptions
                {
                    APIKey = string.Empty,
                    ApprovedLimit = -1,
                    MaximumPages = 3,
                    UseApproved = true,
                    ApprovedTimeout = 180,
                    RemoveApproved = false,
                    UseRedirected = true,
                    UseWorkshop = true,
                    ShowSkinIDs = true,
                    Sorting = SortBy.Config
                },
                Command = new ConfigData.CommandOptions
                {
                    Commands = new [] { "skinbox", "sb" },
                    DeployedCommands = new[] { "skindeployed", "sd" },
                    SetCommands = new [] { "skinset", "ss" },
                    SprayCan = true
                },
                Permission = new ConfigData.PermissionOptions
                {
                    Admin = "skinbox.admin",
                    NoCost = "skinbox.ignorecost",
                    NoCooldown = "skinbox.ignorecooldown",
                    Use = "skinbox.use",
                    UseDeployed = "skinbox.use",
                    Approved = "skinbox.use",
                    Attire = "skinbox.use",
                    Deployable = "skinbox.use",
                    Weapon = "skinbox.use",   
                    Sets = "skinbox.use",
                    Custom = new Hash<string, List<ulong>>
                    {
                        ["skinbox.example1"] = new List<ulong>() { 9990, 9991, 9992 },
                        ["skinbox.example2"] = new List<ulong>() { 9993, 9994, 9995 },
                        ["skinbox.example3"] = new List<ulong>() { 9996, 9997, 9998 }
                    }
                },
                Cooldown = new ConfigData.CooldownOptions
                {
                    Enabled = false,
                    ActivateOnTake = true,
                    Time = 60
                },
                Cost = new ConfigData.CostOptions
                {
                    Enabled = false,
                    Currency = CostType.Scrap,
                    Open = 5,
                    Weapon = 30,
                    Attire = 20,
                    Deployable = 10
                },
                Other = new ConfigData.OtherOptions
                {
                    AllowStacks = false,
                    BlacklistAuth = 2,
                    ApplyWorkshopName = true,
                    RequireBuildingPrivilege = false
                },
                Favourites = new ConfigData.FavouritesOptions
                {
                    Enabled = true,
                    Purge = true,
                    PurgeDays = 7
                },
                Sets = new ConfigData.SetOptions
                {
                    Enabled = true,
                    NumberSets = 3
                },
                SkinList = new Hash<string, HashSet<ulong>>(),
                Blacklist = new HashSet<ulong>(),
            } as T;
        }


        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfig = GenerateDefaultConfiguration<ConfigData>();

            if (oldVersion < new VersionNumber(2, 0, 0))
                ConfigurationData = baseConfig;

            if (oldVersion < new VersionNumber(2, 0, 10))
            {
                (ConfigurationData as ConfigData).Skins.Sorting = SortBy.Config;
                (ConfigurationData as ConfigData).Skins.ShowSkinIDs = true;
            }

            if (oldVersion < new VersionNumber(2, 1, 0))
            {
                (ConfigurationData as ConfigData).Permission.UseDeployed = baseConfig.Permission.UseDeployed;
                (ConfigurationData as ConfigData).Command.DeployedCommands = baseConfig.Command.DeployedCommands;
            }

            if (oldVersion < new VersionNumber(2, 1, 3))
                (ConfigurationData as ConfigData).Skins.ApprovedTimeout = 90;

            if (oldVersion < new VersionNumber(2, 1, 9) && (ConfigurationData as ConfigData).Skins.ApprovedTimeout == 90)
                (ConfigurationData as ConfigData).Skins.ApprovedTimeout = 180;

            if (oldVersion < new VersionNumber(2, 1, 14))
            {
                (ConfigurationData as ConfigData).Favourites = baseConfig.Favourites;
            }

            if (oldVersion < new VersionNumber(2, 1, 23))
            {
                (ConfigurationData as ConfigData).Other.SprayCanMaxUses = 10;
            }

            if (oldVersion < new VersionNumber(2, 1, 25))
                (ConfigurationData as ConfigData).Other.ApplyWorkshopName = true;

            if (oldVersion < new VersionNumber(2, 2, 0))
            {
                (ConfigurationData as ConfigData).Command.SetCommands = baseConfig.Command.SetCommands;
                (ConfigurationData as ConfigData).Permission.Sets = baseConfig.Permission.Sets;
                (ConfigurationData as ConfigData).Sets = baseConfig.Sets;
            }
            
            if (oldVersion < new VersionNumber(2, 2, 1))
            {
                (ConfigurationData as ConfigData).Command.SetCommands = baseConfig.Command.SetCommands;
            }
            
            Configuration = ConfigurationData as ConfigData;
        }

        #endregion

        #region JSON Deserialization        
        public class QueryResponse
        {
            public Response response;
        }

        public class Response
        {
            public int total;
            public PublishedFileDetails[] publishedfiledetails;
        }

        public class PublishedFileDetails
        {
            public int result;
            public string publishedfileid;
            public string creator;
            public int creator_appid;
            public int consumer_appid;
            public int consumer_shortcutid;
            public string filename;
            public string file_size;
            public string preview_file_size;
            public string file_url;
            public string preview_url;
            public string url;
            public string hcontent_file;
            public string hcontent_preview;
            public string title;
            public string file_description;
            public int time_created;
            public int time_updated;
            public int visibility;
            public int flags;
            public bool workshop_file;
            public bool workshop_accepted;
            public bool show_subscribe_all;
            public int num_comments_public;
            public bool banned;
            public string ban_reason;
            public string banner;
            public bool can_be_deleted;
            public string app_name;
            public int file_type;
            public bool can_subscribe;
            public int subscriptions;
            public int favorited;
            public int followers;
            public int lifetime_subscriptions;
            public int lifetime_favorited;
            public int lifetime_followers;
            public string lifetime_playtime;
            public string lifetime_playtime_sessions;
            public int views;
            public int num_children;
            public int num_reports;
            public Preview[] previews;
            public Tag[] tags;
            public int language;
            public bool maybe_inappropriate_sex;
            public bool maybe_inappropriate_violence;

            public class Tag
            {
                public string tag;
                public bool adminonly;
            }
        }

        public class PublishedFileQueryResponse
        {
            public FileResponse response { get; set; }
        }

        public class FileResponse
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public PublishedFileQueryDetail[] publishedfiledetails { get; set; }
        }

        public class PublishedFileQueryDetail
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public string creator { get; set; }
            public int creator_app_id { get; set; }
            public int consumer_app_id { get; set; }
            public string filename { get; set; }
            public int file_size { get; set; }
            public string preview_url { get; set; }
            public string hcontent_preview { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public int time_created { get; set; }
            public int time_updated { get; set; }
            public int visibility { get; set; }
            public int banned { get; set; }
            public string ban_reason { get; set; }
            public int subscriptions { get; set; }
            public int favorited { get; set; }
            public int lifetime_subscriptions { get; set; }
            public int lifetime_favorited { get; set; }
            public int views { get; set; }
            public Tag[] tags { get; set; }

            public class Tag
            {
                public string tag { get; set; }
            }
        }

        public class Preview
        {
            public string previewid;
            public int sortorder;
            public string url;
            public int size;
            public string filename;
            public int preview_type;
            public string youtubevideoid;
            public string external_reference;
        }


        public class CollectionQueryResponse
        {
            public CollectionResponse response { get; set; }
        }

        public class CollectionResponse
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public CollectionDetails[] collectiondetails { get; set; }
        }

        public class CollectionDetails
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public CollectionChild[] children { get; set; }
        }

        public class CollectionChild
        {
            public string publishedfileid { get; set; }
            public int sortorder { get; set; }
            public int filetype { get; set; }
        }

        #endregion

        #region Data Management
        private static StoredData Data;
       
        private class StoredData
        {
            public Hash<ulong, UserFavourites> Users = new Hash<ulong, UserFavourites>();
            public Hash<ulong, List<SetItem[]>> Sets = new Hash<ulong, List<SetItem[]>>();
            
            public void SortForPlayer(BasePlayer player, string shortname, ref List<ulong> list)
            {
                if (!Users.TryGetValue(player.userID, out UserFavourites userFavourites))
                    return;

                if (!userFavourites.Usage.TryGetValue(shortname, out Hash<ulong, int> usage))
                    return;

                int index = 0;
                foreach(KeyValuePair<ulong, int> kvp in usage.OrderByDescending(x => x.Value))
                {
                    if (list.Contains(kvp.Key))
                    {
                        list.Remove(kvp.Key);
                        list.Insert(index, kvp.Key);
                        index++;
                    }
                }                
            }

            public bool UserHasFavouritesFor(BasePlayer player, string shortname)
            {
                if (!Users.TryGetValue(player.userID, out UserFavourites userFavourites))
                    return false;

                return userFavourites.Usage.ContainsKey(shortname);
            }

            public void ClearForPlayer(BasePlayer player, string shortname)
            {
                if (!Users.TryGetValue(player.userID, out UserFavourites userFavourites))
                    return;

                userFavourites.Usage.Remove(shortname);
            }

            public void OnSkinClaimed(BasePlayer looter, Item item)
            {
                if (!Users.TryGetValue(looter.userID, out UserFavourites userFavourites))
                    Users[looter.userID] = userFavourites = new UserFavourites();

                userFavourites.OnSkinClaimed(item.info.shortname, item.skin);
            }

            public void PurgeOldEntries()
            {
                if (!Configuration.Favourites.Purge)
                    return;

                List<ulong> list = Pool.Get<List<ulong>>();

                foreach(KeyValuePair<ulong, UserFavourites> kvp in Users)
                {
                    if (kvp.Value.HasExpired(Configuration.Favourites.PurgeDays))
                        list.Add(kvp.Key);
                }

                foreach (ulong userId in list)
                    Users.Remove(userId);

                Pool.FreeUnmanaged(ref list);
            }

            public List<SetItem[]> GetSkinSetsForPlayer(BasePlayer player)
            {
                if (!Sets.TryGetValue(player.userID, out List<SetItem[]> sets))
                    Sets[player.userID] = sets = new List<SetItem[]>();

                return sets;
            }

            public class SetItem
            {
                public string Shortname;
                public ulong SkinId;

                [JsonIgnore]
                private ItemDefinition _itemDefinition;

                [JsonIgnore]
                public ItemDefinition Definition
                {
                    get
                    {
                        if (!_itemDefinition)
                            _itemDefinition = ItemManager.FindItemDefinition(Shortname);
                        return _itemDefinition;
                    }
                }
            }

            public class UserFavourites
            {
                public Hash<string, Hash<ulong, int>> Usage = new Hash<string, Hash<ulong, int>>();

                public double LastOnline = TimeSinceEpoch();
                
                public bool HasExpired(int days) => (TimeSinceEpoch() - LastOnline) > (days * 86400);
                
                public void OnSkinClaimed(string shortname, ulong skinId)
                {
                    if (skinId == 0)
                        return;

                    if (!Usage.TryGetValue(shortname, out Hash<ulong, int> usage))
                        Usage[shortname] = usage = new Hash<ulong, int>();

                    usage[skinId] += 1;
                }

                private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

                private static int TimeSinceEpoch() => (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
                
                
            }
            
            private static DynamicConfigFile data;

            public static void Save() => data.WriteObject(Data);

            public static void Load()
            {
                data = Interface.Oxide.DataFileSystem.GetFile("SkinBox/user_favourites");
                data.Settings.Converters.Add(new SetItemConverter());
                
                Data = data.ReadObject<StoredData>();
                
                if (Data == null)
                    Data = new StoredData();

                Data.PurgeOldEntries();
            }
        }
        #endregion
        
        #region Converters
        public class SetItemConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Hash<ulong, List<StoredData.SetItem[]>>);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Hash<ulong, List<StoredData.SetItem[]>> sets = (Hash<ulong, List<StoredData.SetItem[]>>)value;
                Dictionary<ulong, List<StoredData.SetItem[]>> filteredSets = sets.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(array => array.Where(item => item != null).ToArray()).ToList()
                );
                serializer.Serialize(writer, filteredSets);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                Dictionary<ulong, List<StoredData.SetItem[]>> sets = serializer.Deserialize<Dictionary<ulong, List<StoredData.SetItem[]>>>(reader);
                Hash<ulong, List<StoredData.SetItem[]>> results = new Hash<ulong, List<StoredData.SetItem[]>>();
                
                foreach (KeyValuePair<ulong, List<StoredData.SetItem[]>> kvp in sets)
                {
                    results[kvp.Key] = new List<StoredData.SetItem[]>();
                    foreach (StoredData.SetItem[] set in kvp.Value)
                    {
                        StoredData.SetItem[] array = set;
                        if (array.Length != SET_SLOTS)
                            Array.Resize(ref array, SET_SLOTS);
                        
                        results[kvp.Key].Add(array);
                    }
                }
                return results;
            }
        }
        #endregion

        protected override Dictionary<string, string> Messages => new Dictionary<string, string>
        {
            ["NoAPIKey"] = "The server owner has not entered a Steam API key in the config. Unable to continue!",
            ["SkinsLoading"] = "SkinBox is still gathering skins. Please try again soon",
            ["NoPermission"] = "You don't have permission to use the SkinBox",
            ["NoPermission.Sets"] = "You don't have permission to use skin sets",
            ["NoBuildingAuth"] = "You must have building privilege to use the SkinBox",
            ["ToNearPlayer"] = "The SkinBox is currently not usable at this place",
            ["CooldownTime"] = "You need to wait {0} seconds to use the SkinBox again",

            ["NotEnoughBalanceOpen"] = "You need at least {0} {1} to open the SkinBox",
            ["NotEnoughBalanceUse"] = "You would need at least {0} {1} to skin {2}",
            ["NotEnoughBalanceTake"] = "{0} was not skinned. You do not have enough {1}",
            ["NotEnoughBalanceSet"] = "You need at least {0} {1} to apply this skin set",
            ["CostToUse2"] = "{0} {3} to skin a deployable\n{1} {3} to skin a weapon\n{2} {3} to skin attire",

            ["TargetSkinNotInList"] = "That skin is not defined in the skin list for that item",
            ["TargetSkinIsCurrentSkin"] = "The target item is already skinned with that skin ID",
            ["ApplyingSkin"] = "Applying skin {0}",
            ["NoItemInHands"] = "You must have a item in your hands to use the quick skin method",

            ["NoSkinsForItem"] = "There are no skins setup for that item",
            ["BlockedItem.Item"] = "That item is not allowed in the skin box",
            ["BlockedItem.Skin"] = "That skin is not allowed in the skin box",
            ["BrokenItem"] = "You can not skin broken items",
            ["HasItem"] = "The skin box already contains an item",
            ["RedirectsDisabled"] = "Redirected skins are disabled on this server",
            ["InsufficientItemPermission"] = "You do not have permission to skin this type of item",

            ["Cost.Scrap"] = "Scrap",
            ["Cost.ServerRewards"] = "RP",
            ["Cost.Economics"] = "Eco",

            ["ReskinError.InvalidResourcePath"] = "Failed to find resource path for deployed item",
            ["ReskinError.TargetNull"] = "The target deployable has been destroyed",
            ["ReskinError.MountBlocked"] = "You can not skin this while a player is mounted",
            ["ReskinError.IOConnected"] = "You can not skin this while it is connected",
            ["ReskinError.NoAuth"] = "You need building auth to reskin deployed items",
            ["ReskinError.NoTarget"] = "Unable to find a valid deployed item",
            ["NoDefinitionForEntity"] = "Failed to find the definition for the target item",

            ["FavouritesEnabled"] = "Favourites system enabled\nYour most used skins will be displayed first",
            ["UI.ClearFavourites"] = "Clear Favourites",
            ["UI.Sets"] = "Sets",
            ["UI.Box"] = "Box",
            ["UI.Title"] = "SkinBox",
            ["UI.Title.Sets"] = "Skin Sets",
            ["UI.Apply"] = "Apply",
            ["UI.Clear"] = "Clear",
            
            ["Sets.Cost"] = "Cost : {0} {1}",
            ["Sets.NoItems"] = "No items in your inventory are applicable for the chosen set",
            ["Sets.Applied"] = "You have applied skin set {0} to {1} item(s) in your inventory",
            ["Sets.Applied.Cost"] = "You have applied skin set {0} to {1} item(s) in your inventory for a price of {2} {3}",
            ["Sets.Error.InvalidSetID"] = "The set ID you entered is invalid, the range is between 1 and {0}",
            ["Sets.Error.EmptySet"] = "Skin set {0} has no skins in it",
            ["Sets.Help"] = "- Dragged skinned items into the container to add it to the set\n- Right click or hover loot to remove items from the set\n- A set can only contain unique items\n- Applying a set will skin the first matching item in your inventory"
        };
    }
}
