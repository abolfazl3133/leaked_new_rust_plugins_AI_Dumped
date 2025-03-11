#define USE_FIXES
// ^^^ Use shop front fixes (not compatible with Stacks Extended!)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Trade", "Def", "1.2.15")]
    public class Trade : RustPlugin
    {
        #region Fields

        private static Trade _instance;
        private static Cfg _cfg;
        private static readonly Dictionary<ulong, DateTime> Cooldowns = new Dictionary<ulong, DateTime>();
        private static readonly Dictionary<NetworkableId, TradeController> Trades = new Dictionary<NetworkableId, TradeController>();
        private static readonly Dictionary<ulong, PendingTrade> PendingTrades = new Dictionary<ulong, PendingTrade>();
        private static readonly Effect EffectInstance = new Effect();

        #endregion

        #region Plugin References

        [PluginReference]
        private Plugin Ignore;

        #endregion

        #region Constants

        private const int CurrentConfigRevision = 2;
        private const string PermAllowUse = "trade.use";
        private const uint VisChkAcceptClick = 1159607245u;
        private const uint VisChkCancelClick = 3168107540u;
        private const uint ShopFrontPrefabId = 1180657261u;
        private const string ShopFrontPrefab = "assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.metal.prefab";
        private static readonly HashSet<uint> MonitoredVisChecks = new HashSet<uint>
        {
            VisChkAcceptClick, VisChkCancelClick
        };
        private static readonly object FalseObj = false;
        private static readonly object TrueObj = true;

        #endregion

        #region Configuration

        private class Cfg
        {
            [JsonProperty("Disable accepting requests in Block zone")]
            public bool DisallowAcceptInBuildingBlock;
            [JsonProperty("Disable sending requests in Block zone")]
            public bool DisallowRequestInBuildingBlock;
            [JsonProperty("Disable trading in air")]
            public bool DisallowInAir;
            [JsonProperty("Disable trading in water")]
            public bool DisallowInWater;
            [JsonProperty("Disable trading while wounded")]
            public bool DisallowInWound;
            [JsonProperty("Disable trading in transport")]
            public bool DisallowInTransport;
            [JsonProperty("Enable individual trade slot count")]
            public bool EnableIndividualTradeSlots;
            [JsonProperty("Trade request timeout (seconds)")]
            public float RequestTimeout;
            [JsonProperty("Max distance from trade spot (0 - disabled)")]
            public float MaxTradeSpotDistance;
            [JsonProperty("AntiScam trade accept delay (0 - disabled)")]
            public float AntiScamDelay;
            [JsonProperty("Chat icon id")]
            public ulong ChatIconId;
            [JsonProperty("Allow trade logging")]
            public bool AllowTradeLogs;
            [JsonProperty("Permissions (first one is always default)")]
            public List<PermissionDefinition> Permissions;
            [JsonProperty(PropertyName = "Config revision (do not edit)")]
            public int ConfigRev;

            public static Cfg DefaultConfiguration() => new Cfg
            {
                DisallowAcceptInBuildingBlock = true,
                DisallowRequestInBuildingBlock = true,
                DisallowInAir = true,
                DisallowInWater = true,
                DisallowInWound = true,
                DisallowInTransport = false,
                EnableIndividualTradeSlots = false,
                RequestTimeout = 30f,
                MaxTradeSpotDistance = 5f,
                AntiScamDelay = 0f,
                ChatIconId = 0UL,
                AllowTradeLogs = true,
                Permissions = new List<PermissionDefinition>
                {
                    new PermissionDefinition{Order = 1, Name = "default", Cooldown = 30f, MaxDist = 0f, TradingSlots = 6, BannedItems = new List<string> {"note"}},
                    new PermissionDefinition{Order = 2, Name = "vip_example", Cooldown = 10f, MaxDist = 0f, TradingSlots = 12}
                },
                ConfigRev = CurrentConfigRevision,
            };

            public bool UpgradeConfig()
            {
                if(ConfigRev >= CurrentConfigRevision)
                    return false;
                for (; ConfigRev < CurrentConfigRevision; ConfigRev++)
                {
                    switch (ConfigRev) // current
                    {
                        case 1: // 1->2
                            AntiScamDelay = 0f;
                            break;
                    }
                }
                return true;
            }

            [OnDeserialized]
            private void OnDeserialized(StreamingContext _)
            {
                if (Permissions == null || Permissions.Count == 0)
                {
                    _instance.PrintError("No permissions defined! Check config or reset it.");
                    _instance.NextTick(()=>Interface.Oxide.UnloadPlugin(nameof(Trade)));
                    return;
                }
                if(Permissions.Count == 1) // No reg if just only one perm is defined.
                    return;
                Permissions.Sort((a, b) => a.Order.CompareTo(b.Order));
                for (var i = 1; i < Permissions.Count; i++) // skip deflt. perm. No need to register it.
                {
                    Permissions[i].PermName = $"trade.{Permissions[i].Name}";
                    _instance.permission.RegisterPermission(Permissions[i].PermName, _instance);
                }
            }
        }

        protected override void LoadDefaultConfig() => _cfg = Cfg.DefaultConfiguration();

        protected override void LoadConfig()
        {
            _instance = this;
            base.LoadConfig();
            _cfg = Config.ReadObject<Cfg>();
            if (_cfg.UpgradeConfig())
            {
                SaveConfig();
                PrintWarning($"Config upgraded to rev. {_cfg.ConfigRev}. Make sure to check out config!");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_cfg);

        #endregion

        #region Hooks

        private void Loaded() =>  permission.RegisterPermission(PermAllowUse, this);

        private void Unload()
        {
            try
            {
                foreach (var trade in Trades.Values.ToArray())
                    InterruptTrade(trade);
            }
            finally
            {
                Trades.Clear();
                Cooldowns.Clear();
                PendingTrades.Clear();
                EffectInstance.Clear(true);
                _instance = null;
            }
        }

        private object OnEntityVisibilityCheck(BaseEntity entity, BasePlayer player, uint rpcId, string debugName, float _)
        {
            if (!MonitoredVisChecks.Contains(rpcId))
                return null;
            var trade = GetTrade(entity.net.ID);
            if (!trade)
                return null;
            return !trade.ProcessUiClick(rpcId, player) ? FalseObj : TrueObj;
        }

        private void OnShopCompleteTrade(ShopFront shop)
        {
            var trade = GetTrade(shop.net.ID);
            if (trade == null)
                return;
            trade.isCompleted = true;

            // Transfer vendor items to customer inventory
            foreach (var item in shop.vendorInventory.itemList.ToArray())
            {
                item.RemoveFromContainer();
                if (!item.MoveToContainer(trade.customer.inventory.containerMain, -1, true))
                {
                    // If main inventory is full, try belt
                    if (!item.MoveToContainer(trade.customer.inventory.containerBelt, -1, true))
                    {
                        // If both are full, drop the item
                        item.Drop(trade.customer.transform.position + (Vector3.up * 1f), Vector3.zero);
                    }
                }
                item.MarkDirty();
            }

            // Transfer customer items to vendor inventory
            foreach (var item in shop.customerInventory.itemList.ToArray())
            {
                item.RemoveFromContainer();
                if (!item.MoveToContainer(trade.vendor.inventory.containerMain, -1, true))
                {
                    // If main inventory is full, try belt
                    if (!item.MoveToContainer(trade.vendor.inventory.containerBelt, -1, true))
                    {
                        // If both are full, drop the item
                        item.Drop(trade.vendor.transform.position + (Vector3.up * 1f), Vector3.zero);
                    }
                }
                item.MarkDirty();
            }

            trade.vendor.inventory.containerMain.MarkDirty();
            trade.vendor.inventory.containerBelt.MarkDirty();
            trade.customer.inventory.containerMain.MarkDirty();
            trade.customer.inventory.containerBelt.MarkDirty();

            Player.Message(trade.vendor, _("Msg.TradeSuccessful", trade.vendor, SanitizeName(trade.customer.displayName)), _cfg.ChatIconId);
            Player.Message(trade.customer, _("Msg.TradeSuccessful", trade.customer, SanitizeName(trade.vendor.displayName)), _cfg.ChatIconId);
            Cooldowns[trade.vendor.userID] = DateTime.Now.AddSeconds(GetPermission(trade.vendor).Cooldown);
            Cooldowns[trade.customer.userID] = DateTime.Now.AddSeconds(GetPermission(trade.customer).Cooldown);
            SendEffect(shop.transactionCompleteEffect.resourcePath, trade.vendor, trade.customer);
            NextFrame(()=>DropTrade(trade));
            trade.OnTransactionCompleted();
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (!inventory.entitySource.IsValid() || inventory.entitySource.prefabID != ShopFrontPrefabId)
                return;
            var trade = GetTrade(inventory.entitySource.net.ID);
            if (trade == null)
                return;
            InterruptTrade(trade);
        }
        
        #endregion

        #region Hooks - Fixes

        #if USE_FIXES
        
        // Global: shopfront fixes.
        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer)
        {
            if (playerLoot == null || !(playerLoot.loot.entitySource is ShopFront))
                return null;
            var rootCont = item.GetRootContainer();
            if (rootCont == null)
                return null;
            var player = playerLoot.containerMain.playerOwner;
            if (player == null)
                return null;
            var shopFront = (ShopFront) playerLoot.loot.entitySource;
        
            var tc = playerLoot.FindContainer(targetContainer);
            if (tc?.parent != null)
                targetContainer = tc.parent.GetRootContainer()?.uid ?? targetContainer;
            if (targetContainer == shopFront.vendorInventory.uid && !shopFront.IsPlayerVendor(player)
                || targetContainer == shopFront.customerInventory.uid && !shopFront.IsPlayerCustomer(player))
                return FalseObj;
            if (rootCont == shopFront.vendorInventory && !shopFront.IsPlayerVendor(player)
                || rootCont == shopFront.customerInventory && !shopFront.IsPlayerCustomer(player))
                return FalseObj;
            shopFront.ResetTrade();
            return null;
        }

        // Global: shopfront item split scam fix. Deprecated.
        // private void OnItemSplit(Item item, int amount)
        // {
        //     var containerEntity = item.parent?.entityOwner;
        //     if (!containerEntity.IsValid() || !(containerEntity is ShopFront))
        //         return;
        //     ((ShopFront)containerEntity).ResetTrade();
        // }
        
        #endif

        #endregion

        #region Hooks - API

        // Stub for plugins using old trade plugin.
        private bool IsTradeBox(BaseNetworkable bn) => bn.net != null && Trades.ContainsKey(bn.net.ID);

        #endregion

        #region Commands

        [ConsoleCommand("trade")]
        private void CmdTrade(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;
            CmdChatTrade(player, string.Empty, new[] { arg.Args[0] });
        }


        [ChatCommand("trade")]
        private void CmdChatTrade(BasePlayer player, string command, string[] args)
        {
            if (player == null) 
                return;
            
            if (!permission.UserHasPermission(player.UserIDString, PermAllowUse))
            {
                Player.Message(player, _("Error.NoPerm", player, _("Msg.You", player)), _cfg.ChatIconId);
                return;
            }

            if (args.Length == 0)
            {
                Player.Message(player, _("Msg.TradeIntro",player), _cfg.ChatIconId);
                return;
            }

            PendingTrade pendingTrade;

            if (!PendingTrade.HasPendingTrade(player)) // No trade, listen for any player names, ignoring commands.
            {
                var name = string.Join(" ", args);
                var targets = FindOnline(name);
                if (targets.Length == 0)
                {
                    Player.Message(player, _("Error.NoSuchPlayer", player), _cfg.ChatIconId);
                    return;
                }
                if (targets.Length > 1)
                {
                    Player.Message(player, _("Error.MultiplePlayers", player), _cfg.ChatIconId);
                    return;
                }
                var target = targets[0];
                if (target == player)
                {
                    Player.Message(player, _("Error.SelfTrade", player), _cfg.ChatIconId);
                    return;
                }
                if (Ignore != null)
                {
                    var rslt = Ignore.CallHook("IsIgnored", player.UserIDString, target.UserIDString) ??
                               Ignore.CallHook("IsIgnoredS", player.UserIDString, target.UserIDString);
                    if (rslt != null && (bool)rslt) 
                    {
                        Player.Message(player, _("Error.Ignored", player), _cfg.ChatIconId);
                        return;
                    }
                }
                if (!CanPlayerTrade(player, player) || !CanPlayerTrade(target, player))
                    return;
                pendingTrade = PendingTrade.GetPendingTrade(player, true);
                pendingTrade.Customer = target;
                PendingTrades[player.userID] = pendingTrade;
                PendingTrades[target.userID] = pendingTrade;
                Player.Message(player, _("Msg.TradeRequestSent", player, SanitizeName(target.displayName)), _cfg.ChatIconId);
                Player.Message(target, _("Msg.TradeRequestReceived", target, SanitizeName(player.displayName)), _cfg.ChatIconId);
                SendEffect("assets/bundled/prefabs/fx/invite_notice.prefab", target);
                pendingTrade.CreateRequestTimer(_cfg.RequestTimeout);
                return;
            }
            switch (args[0].ToLower())
            {
                case "accept":
                case "yes":
                case "+":
                    pendingTrade = PendingTrade.GetPendingTrade(player);
                    if (pendingTrade == null || pendingTrade.Vendor == player)
                    {
                        Player.Message(player, _("Error.NoPendingRequest", player), _cfg.ChatIconId);
                        return;
                    }
                    if (!CanPlayerTrade(player, player) || !CanPlayerTrade(pendingTrade.Vendor, player))
                        return;
                    pendingTrade.Remove();
                    timer.Once(.2f, () => OpenTrade(pendingTrade.Vendor, player));
                    return;
                case "cancel":
                case "no":
                case "-":
                    pendingTrade = PendingTrade.GetPendingTrade(player);
                    if (pendingTrade == null)
                    {
                        Player.Message(player, _("Error.NoPendingRequest", player), _cfg.ChatIconId);
                        return;
                    }
                    pendingTrade.Remove();
                    Player.Message(player, _("Msg.TradeCancelledCustomer", player), _cfg.ChatIconId);
                    var customer = pendingTrade.GetOppositeTrader(player);
                    if(customer.IsValid())
                        Player.Message(customer, _("Msg.TradeCancelledVendor", pendingTrade.Vendor, SanitizeName(player.displayName)), _cfg.ChatIconId);
                    return;

                default:
                    Player.Message(player, _("Error.UnknownCommand", player), _cfg.ChatIconId);
                    return;
            }
        }
        #endregion

        #region Core Funcs

        private PermissionDefinition GetPermission(BasePlayer player)
        {
            var highestOrderPerm = _cfg.Permissions[0]; // default perm first
            for (var i = 0; i < _cfg.Permissions.Count; i++)
            {
                if(i == 0)
                    continue;
                var perm = _cfg.Permissions[i];
                if(!permission.UserHasPermission(player.UserIDString, perm.PermName))
                    continue;
                if (perm.Order > highestOrderPerm.Order)
                    highestOrderPerm = perm;
            }
            return highestOrderPerm;
        }
        
        private static BasePlayer[] FindOnline(string nameOrUserId)
        {
            var players = BasePlayer.activePlayerList.Where(p => !p.limitNetworking &&
                (p.UserIDString == nameOrUserId || p.displayName.StartsWith(nameOrUserId, StringComparison.InvariantCultureIgnoreCase) ||
                p.displayName.IndexOf(nameOrUserId, StringComparison.InvariantCultureIgnoreCase) >= 0)).ToArray();
            return players;
        }

        private static PluginTimers GetTimer()
        {
            return _instance.timer;
        }

        private static TradeController GetTrade(NetworkableId shopFrontId)
        {
            TradeController tradeCtrl;
            return !Trades.TryGetValue(shopFrontId, out tradeCtrl) ? null : tradeCtrl;
        }

        private static ShopFront GetShopFront(NetworkableId id)
        {
            TradeController tradeCtrl;
            return !Trades.TryGetValue(id, out tradeCtrl) ? null : tradeCtrl.shop;
        }

        private static ShopFront CreateShopFront(BasePlayer vendorPlayer, BasePlayer customerPlayer)
        {
            var pos = new Vector3(ValidBounds.Instance.worldBounds.extents.x, TerrainMeta.LowestPoint.y, 0);
            var shopFront = (ShopFront) GameManager.server.CreateEntity(ShopFrontPrefab, pos);
            shopFront.syncPosition = false;
            shopFront.enableSaving = false;
            shopFront.globalBroadcast = true;
            shopFront.Spawn();
            shopFront.decay = null;
            BuildingManager.server.Remove(shopFront);
            UnityEngine.Object.Destroy(shopFront.GetComponent<GroundWatch>());
            UnityEngine.Object.Destroy(shopFront.GetComponent<DestroyOnGroundMissing>());
            var vendorPerms = _instance.GetPermission(vendorPlayer);
            var customerPerms = _instance.GetPermission(customerPlayer);
            shopFront.vendorInventory.capacity = Mathf.Clamp(vendorPerms.TradingSlots, 1, 12);
            shopFront.customerInventory.capacity = Mathf.Clamp(_cfg.EnableIndividualTradeSlots ? customerPerms.TradingSlots : vendorPerms.TradingSlots, 1, 12);
            shopFront.vendorInventory.canAcceptItem = (item, i) => shopFront.ItemFilter(item, i) && CanAcceptVendorItem(shopFront, item, i) && TradeItemFilter(item, i, vendorPerms);
            shopFront.customerInventory.canAcceptItem = (item, i) => shopFront.ItemFilter(item, i) && CanAcceptCustomerItem(shopFront, item, i) && TradeItemFilter(item, i, customerPerms);
            SendEntitiesSnapshot(vendorPlayer, customerPlayer);
            SendEntitiesSnapshot(customerPlayer, vendorPlayer);
            vendorPlayer.EndLooting();
            customerPlayer.EndLooting();
            var netId = shopFront.net.ID;
            GetTimer().Once(.2f, () =>
            {
                StartLooting(netId, vendorPlayer);
                StartLooting(netId, customerPlayer);
            });
            return shopFront;
        }

        private void InterruptTrade(TradeController trade)
        {
            if(trade == null)
                return;
            if (!trade.isCompleted)
            {
                if(trade.vendor.IsValid() && trade.vendor.IsConnected)
                    Player.Message(trade.vendor, _("Msg.TradeInterrupted", trade.vendor), _cfg.ChatIconId);
                if(trade.customer.IsValid() && trade.customer.IsConnected)
                    Player.Message(trade.customer, _("Msg.TradeInterrupted", trade.customer), _cfg.ChatIconId);
            }
            DropTrade(trade);
        }

        // Cancel, Remove Trade & Shopfront.
        private static void DropTrade(TradeController trade)
        {
            if (trade == null)
                return;
            Trades.Remove(trade.shopId);
            trade.CancelInvoke(trade.CheckShop);
            trade.vendor.EndLooting();
            trade.customer.EndLooting();
            var shop = trade.shop;
            trade.isDestroying = true;
            shop.customerInventory.Kill();
            shop.Kill();
        }

        private static void OpenTrade(BasePlayer vendorPlayer, BasePlayer customerPlayer)
        {
            var shopFront = CreateShopFront(vendorPlayer, customerPlayer);
            var tradeCtrl = shopFront.gameObject.AddComponent<TradeController>();
            tradeCtrl.Init(vendorPlayer, customerPlayer);
            Trades.Add(tradeCtrl.shopId, tradeCtrl);
        }

        private static void StartLooting(NetworkableId shopId, BasePlayer player)
        {
            var shopFront = GetShopFront(shopId);
            if (shopFront == null)
                return;
            player.EndLooting();
            player.inventory.loot.StartLootingEntity(shopFront, false);
            player.inventory.loot.AddContainer(shopFront.vendorInventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "shopfront");
            player.inventory.loot.AddContainer(shopFront.customerInventory);
            player.inventory.loot.SendImmediate();
            if (shopFront.vendorPlayer == null)
                shopFront.vendorPlayer = player;
            else
                shopFront.customerPlayer = player;
            shopFront.ResetTrade();
            shopFront.UpdatePlayers();
        }

        private bool CanPlayerTrade(BasePlayer player, BasePlayer requestor)
        {
            var appeal = _(player == requestor ? "Msg.You" : "Msg.YourPartner", requestor);
            if (!permission.UserHasPermission(player.UserIDString, PermAllowUse))
            {
                Player.Message(requestor, _("Error.NoPerm", requestor, appeal, _cfg.ChatIconId));
                return false;
            }
            if (!player.IsValid() || !player.IsConnected)
            {
                Player.Message(requestor, _("Error.CantTradeOffline", requestor, _("Msg.YourPartner", player)), _cfg.ChatIconId);
                return false;
            }
            if (player.IsDead())
            {
                Player.Message(requestor, _("Error.CantTradeDead", requestor, appeal), _cfg.ChatIconId);
                return false;
            }
            if (player.IsSleeping())
            {
                Player.Message(requestor, _("Error.CantTradeSleeping", requestor, appeal), _cfg.ChatIconId);
                return false;
            }
            if (_cfg.DisallowInWater)
            {
                if (player.IsSwimming())
                {
                    Player.Message(requestor, _("Error.CantTradeInWater", requestor, appeal), _cfg.ChatIconId);
                    return false;
                }
            }
            if (_cfg.DisallowRequestInBuildingBlock && _cfg.DisallowAcceptInBuildingBlock)
            {
                if (player.limitNetworking || !player.CanBuild())
                {
                    Player.Message(requestor, _("Error.CantTradeInBuildingBlock", requestor, appeal), _cfg.ChatIconId);
                    return false;
                }
            }
            if (_cfg.DisallowInAir)
            {
                if (!player.IsOnGround() || player.IsFlying)
                {
                    Player.Message(requestor, _("Error.CantTradeInAir", requestor, appeal), _cfg.ChatIconId);
                    return false;
                }
            }
            if (_cfg.DisallowInWound)
            {
                if (player.IsWounded())
                {
                    Player.Message(requestor, _("Error.CantTradeWounded", requestor, appeal), _cfg.ChatIconId);
                    return false;
                }
            }
            // TODO: Mount state type none?
            if ((_cfg.DisallowInTransport && player.GetMountedVehicle()) || player.GetParentEntity())
            {
                Player.Message(requestor, _("Error.CantTradeInVehicle", requestor, appeal), _cfg.ChatIconId);
                return false;
            }
            if (Cooldowns.ContainsKey(requestor.userID))
            {
                var seconds = Cooldowns[requestor.userID].Subtract(DateTime.Now).TotalSeconds;
                if (seconds >= 0)
                {
                    Player.Message(requestor, _("Error.TradeCooldown", requestor, TimeSpan.FromSeconds(seconds)), _cfg.ChatIconId);
                    return false;
                }
            }
            var requestorPerms = GetPermission(requestor);
            if (requestorPerms.MaxDist > 0f && Vector3.Distance(requestor.transform.position, player.transform.position) > requestorPerms.MaxDist)
            {
                Player.Message(requestor, _("Error.TooFar", requestor, _("Msg.YourPartner", player)), _cfg.ChatIconId);
                return false;
            }
            var canTrade = Interface.CallHook("CanTrade", player);
            if (canTrade == null)
                return true;
            if (canTrade is string)
            {
                Player.Message(requestor, canTrade.ToString().Replace("You", appeal), _cfg.ChatIconId);
                return false;
            }
            Player.Message(requestor, _("Error.CantTradeRightNow", requestor, appeal), _cfg.ChatIconId);
            return false;
        }

        private static void SendEffect(string effectStr, params BasePlayer[] players)
        {
            foreach (var player in players)
            {
                if(!player.IsConnected)
                    continue;
                EffectInstance.Init(Effect.Type.Generic,player,0,Vector3.one,Vector3.zero);
                EffectInstance.pooledString = effectStr;
                EffectNetwork.Send(EffectInstance, player.net.connection);
                EffectInstance.Clear(true);
            }
        }

        private static string SanitizeName(string name)
        {
            if (name.Length > 24)
                name = name.Substring(0, 24).Trim();
            return name.EscapeRichText();
        }

        private static bool TradeItemFilter(Item item, int itemPos, PermissionDefinition perms) =>
            perms.BannedItems == null || !perms.BannedItems.Contains(item.info.shortname);
        
        private static bool CanAcceptVendorItem(ShopFront shop, Item item, int targetSlot) => 
            (shop.vendorPlayer != null && item.GetOwnerPlayer() == shop.vendorPlayer) || shop.vendorInventory.itemList.Contains(item) || item.parent == null;


        private static bool CanAcceptCustomerItem(ShopFront shop, Item item, int targetSlot) =>
            (shop.customerPlayer != null && item.GetOwnerPlayer() == shop.customerPlayer) || shop.customerInventory.itemList.Contains(item) 
                                                                                          || item.parent == null;

        private static void SendEntitiesSnapshot(BasePlayer recipientPlayer, params BaseNetworkable[] ents)
        {
            if (!recipientPlayer.IsConnected)
                return;
            foreach (var ent in ents)
                SendEntitySnapshotEx(recipientPlayer, ent);
        }

        private static void SendEntitySnapshotEx(BaseNetworkable receiver, BaseNetworkable ent)
        {
            if (ent == null || ent.net == null)
                return;
            ++receiver.net.connection.validate.entityUpdates;
            var saveInfo = new BaseNetworkable.SaveInfo
            {
                forConnection = receiver.net.connection,
                forDisk = false
            };
            var nw = Net.sv.StartWrite();
            nw.PacketID(Message.Type.Entities);
            nw.UInt32(receiver.net.connection.validate.entityUpdates);
            ent.ToStreamForNetwork(nw, saveInfo);
            nw.Send(new SendInfo(receiver.net.connection));
        }

        private static string GetItemName(Item item) => string.IsNullOrEmpty(item.name) ? item.info.displayName.english : item.name;
        
        #endregion

        #region Classes

        private class TradeController : FacepunchBehaviour
        {
            public NetworkableId shopId;
            public bool isCompleted, isDestroying;
            public ShopFront shop;
            public BasePlayer vendor, customer;
            private bool _checkDist;
            private bool _tempLocked;
            private Vector3 _vendorLoc, _customerLoc;

            public void Init(BasePlayer vendorPlayer, BasePlayer customerPlayer)
            {
                vendor = vendorPlayer;
                customer = customerPlayer;
                if (_cfg.MaxTradeSpotDistance > 0f)
                {
                    _vendorLoc = vendorPlayer.ServerPosition; // local
                    _customerLoc = customerPlayer.ServerPosition; // local
                    _checkDist = true;
                }
                if (_cfg.AntiScamDelay > 0f)
                {
                    shop.inventory.onItemAddedRemoved = OnItemAddedOrRemoved + shop.inventory.onItemAddedRemoved;
                    shop.customerInventory.onItemAddedRemoved = OnItemAddedOrRemoved + shop.customerInventory.onItemAddedRemoved;
                }
#if !DEV
                InvokeRepeating(CheckShop, 1f, .40f);
#endif
            }

            public bool ProcessUiClick(uint rpcId, BasePlayer caller)
            {
                switch (rpcId)
                {
                    case VisChkAcceptClick:
                        if (shop.vendorPlayer != null && !_instance.CanPlayerTrade(shop.vendorPlayer, caller) 
                            || shop.customerPlayer != null && !_instance.CanPlayerTrade(shop.customerPlayer, caller))
                        {
                            SendEffect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", caller);
                            return false;
                        }
                        if (_tempLocked)
                            return false;
                        return true;
                    case VisChkCancelClick:
                        return true;
                    default:
                        return false;
                }
            }

            private void OnItemAddedOrRemoved(Item item, bool added)
            {
                if((shop.flags & (ShopFront.ShopFrontFlags.VendorAccepted|ShopFront.ShopFrontFlags.CustomerAccepted)) == 0)
                    return;
                if(_tempLocked || IsInvoking(AntiScamUnlock))
                    return;
                _tempLocked = true;
                Invoke(AntiScamUnlock, _cfg.AntiScamDelay);
            }

            private void AntiScamUnlock() => _tempLocked = false;

            public void CheckShop()
            {
                if (!vendor.IsConnected || !customer.IsConnected)
                {
                    _instance.InterruptTrade(this);
                    return;
                }
                if (_checkDist)
                {
                    // local transforms
                    if (Vector3.Distance(vendor.ServerPosition, _vendorLoc) > _cfg.MaxTradeSpotDistance ||
                        Vector3.Distance(customer.ServerPosition, _customerLoc) > _cfg.MaxTradeSpotDistance)
                    {
                        _instance.InterruptTrade(this);
                    } 
                }
            }

            public void OnTransactionCompleted()
            {
                vendor.SignalBroadcast(BaseEntity.Signal.Gesture, "victory");
                customer.SignalBroadcast(BaseEntity.Signal.Gesture, "victory");
                if (!_cfg.AllowTradeLogs)
                    return;
                var vendorItems = shop.vendorInventory.itemList.Select(i => $"{GetItemName(i)} x{i.amount}").DefaultIfEmpty("(Empty)").ToSentence();
                var customerItems = shop.customerInventory.itemList.Select(i => $"{GetItemName(i)} x{i.amount}").DefaultIfEmpty("(Empty)").ToSentence();
                var sb = new StringBuilder($"[{DateTime.Now:G}] Trade between: {vendor.displayName}({vendor.userID}) and {customer.displayName}({customer.userID}):{Environment.NewLine}");
                sb.AppendLine($"{shop.vendorPlayer.displayName}'s offer: {vendorItems}");
                sb.AppendLine($"{shop.customerPlayer.displayName}'s offer: {customerItems}");
                _instance.LogToFile("Log", sb.ToString(), _instance);
            }

            private void Awake()
            {
                shop = GetComponent<ShopFront>();
                shopId = shop.net.ID;
            }

            private void OnDestroy()
            {
                if (!isDestroying) 
                    _instance.PrintError("Illegally destroying an active ShopFront!");
                shop = null;
                vendor = null;
                customer = null;
            }
        }

        private class PendingTrade
        {
            public readonly BasePlayer Vendor;
            public BasePlayer Customer;
            private Timer _requestTimer;

            private PendingTrade(BasePlayer vendor)
            {
                Vendor = vendor;
            }

            public void CreateRequestTimer(float requestTimeout)
            {
                _requestTimer?.Destroy();
                _requestTimer = GetTimer().Once(requestTimeout, () =>
                {
                    if (!PendingTrades.ContainsKey(Customer.userID))
                        return;
                    Remove();
                    if(Vendor.IsConnected)
                        _instance.Player.Message(Vendor, _instance._("Msg.TradeTimeoutVendor", Vendor, SanitizeName(Customer.displayName)), _cfg.ChatIconId);
                    if(Customer.IsConnected)
                        _instance.Player.Message(Customer, _instance._("Msg.TradeTimeoutCustomer", Customer, SanitizeName(Vendor.displayName)), _cfg.ChatIconId);
                });
            }

            public void Remove()
            {
                if (Vendor != null)
                    PendingTrades.Remove(Vendor.userID);
                if (Customer != null)
                    PendingTrades.Remove(Customer.userID);
                _requestTimer?.Destroy();
                _requestTimer = null;
            }

            public BasePlayer GetOppositeTrader(BasePlayer player)
            {
                BasePlayer opp = null;
                if (Vendor != null && Vendor != player)
                    opp = Vendor;
                if (Customer != null && Customer != player)
                    opp = Customer;
                return opp;
            }

            public static bool HasPendingTrade(BasePlayer player) => PendingTrades.ContainsKey(player.userID);

            public static PendingTrade GetPendingTrade(BasePlayer player, bool createIfNotExists = false)
            {
                PendingTrade pt;
                if (!PendingTrades.TryGetValue(player.userID, out pt) && createIfNotExists)
                    pt = new PendingTrade(player);
                return pt;
            }
        }

        private class PermissionDefinition
        {
            [JsonProperty]
            public int Order;
            [JsonProperty] 
            public string Name;
            [JsonIgnore]
            internal string PermName; // real perm name, with prefix. never saved to config.
            [JsonProperty]
            public float Cooldown;
            [JsonProperty]
            public float MaxDist;
            [JsonProperty]
            public int TradingSlots;
            [JsonProperty("Banned Items")]
            public List<string> BannedItems;
        }
        
        #endregion

        #region Localization

        private string _(string key, BasePlayer player = null, params object[] args)
        {
            var message = lang.GetMessage(key, this, player?.UserIDString);
            return message != null ? args.Length > 0 ? string.Format(message, args) : message : key;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Msg.TradeIntro", "To begin trade, type <color=#81B67A>/trade <Partial name or Steam ID></color>." },

                { "Msg.TradeRequestSent", "You've sent a trade request to <color=#81B67A>{0}</color>." },
                { "Msg.TradeRequestReceived", "<color=#81B67A>{0}</color> wants to trade with you!\n<color=#81B67a>/trade yes</color> - Accept request.\n<color=#DA5757>/trade no</color> - Deny request." },

                { "Msg.TradeSuccessful", "Your trade with <color=#81B67A>{0}</color> succeed." },
                { "Msg.TradeTimeoutVendor", "<color=#81B67A>{0}</color> didn't anwered to your trade request." },
                { "Msg.TradeTimeoutCustomer", "You haven't answered to <color=#81B67A>{0}</color>'s trade request." },
                { "Msg.TradeCancelledVendor", "<color=#81B67A>{0}</color> has cancelled a trade request." },
                { "Msg.TradeCancelledCustomer", "You have cancelled a trade request." },
                { "Msg.TradeInterrupted", "Trade was interrupted!" },

                { "Msg.You", "<color=#81B67A>You</color>" },
                { "Msg.YourPartner", "<color=#81B67A>Your partner</color>" },

                { "Error.NoPerm", "{0} don't have permission to trade." },
                { "Error.NoSuchPlayer", "No such player found or he is offline." },
                { "Error.Ignored", "That player is ignoring you." },
                { "Error.MultiplePlayers", "Found multiple players with this name!\nRefine your search please or use SteamID." },
                { "Error.SelfTrade", "Obviously, you can't trade with yourself :)" },
                { "Error.NoPendingRequest", "You have no pending requests." },

                { "Error.CantTradeInWater", "{0} can't trade while in water!" },
                { "Error.CantTradeInBuildingBlock", "{0} can't trade while in Building Block zone." },
                { "Error.CantTradeInAir", "{0} can't trade while flying." },
                { "Error.CantTradeWounded", "{0} can't trade while wounded." },
                { "Error.CantTradeSleeping", "{0} can't trade while sleeping." },
                { "Error.CantTradeInVehicle", "{0} can't trade in transport." },
                { "Error.CantTradeDead", "{0} can't trade while dead." },
                { "Error.CantTradeOffline", "{0} is offline." },
                { "Error.CantTradeRightNow", "{0} can't trade right now." },
                { "Error.TradeCooldown", "Trade is on cooldown. Please wait <color=#81B67A>{0:mm\\:ss}</color>." },
                { "Error.TooFar", "{0} is too far away from you." },
                { "Error.UnknownCommand", "Unrecognized command.\nType either <color=#81B67a>/trade yes</color> or <color=#DA5757>/trade no</color>" },
            }, this);
        }

        #endregion

        #region Dev - Testing tools

#if DEV
        [ChatCommand("tradetest")]
        private void CmdChatTradeTest(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin)
                return;
            var rndPos = args.Length > 0 && args[0] == "far" ? new Vector3(4000,0,4000) : player.transform.position + (UnityEngine.Random.onUnitSphere * 6f);
            rndPos.y = TerrainMeta.HeightMap.GetHeight(rndPos);
            var dummy = (BasePlayer) GameManager.server.CreateEntity(player.PrefabName, rndPos);
            dummy.enableSaving = false;
            dummy.Spawn();
            OpenTrade(player, dummy);
            dummy.Invoke(dummy.KillMessage, 60f);
        }
#endif

        #endregion

    }
}
                         
