using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("HomeRecycler", "wazzzup", "2.0.10")]
    [Description("Allows to have Recycler at home")]
    class HomeRecycler : RustPlugin
    {
        [PluginReference] Plugin Friends;
        
        private readonly Dictionary<NetworkableId, ulong> startedRecyclers = new Dictionary<NetworkableId, ulong>();
        private readonly Dictionary<int, KeyValuePair<string, int>> itemsNeededToCraft = new Dictionary<int, KeyValuePair<string, int>>();
        private readonly Dictionary<BasePlayer, RecyclerEntity> pickupRecyclers = new Dictionary<BasePlayer, RecyclerEntity>();
       
        #region Fields
        private int recyclerItemId;

        private const ulong RECYCLER_SKIN_ID = 1321253094UL;

        private const string PERMISSION_GENERIC = "homerecycler.{0}";
        private const string PERMISSION_CANGET = "homerecycler.canget";
        private const string PERMISSION_CANCRAFT = "homerecycler.cancraft";
        private const string PERMISSION_IGNORE_COOLDOWN = "homerecycler.ignorecooldown";
        private const string PERMISSION_IGNORE_CRAFT_COOLDOWN = "homerecycler.ignorecraftcooldown";

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            LoadData();
            
            Unsubscribe(nameof(OnLootSpawn));
            
            if (!configData.allowRepair && !configData.allowPickupByHammerHit)
                Unsubscribe(nameof(OnHammerHit));
            
            if (!configData.trackStacking)
            {
                Unsubscribe(nameof(CanCombineDroppedItem));
                Unsubscribe(nameof(CanStackItem));
                Unsubscribe(nameof(OnItemSplit));
            }
            
            if (!configData.dropRecyclerItemWhenFloorDestroyed)
                Unsubscribe(nameof(OnEntityGroundMissing));

            if (configData.useSpawning)
            {
                cmd.AddChatCommand(configData.chatCommand, this, "cmdRec");
            }
            else PrintWarning($"Spawning/getting by command {configData.chatCommand} is disabled, check config if needed");

            if (configData.useCrafting)
            {
                if (configData.itemsNeededToCraft.Count < 1)
                {
                    PrintWarning("no items set to craft, check config");
                }
                else
                {
                    cmd.AddChatCommand(configData.craftCommand, this, "cmdCraft");
                }
            }
            else PrintWarning($"Crafting by command {configData.craftCommand} is disabled, check config if needed");

            RegisterPermissions();
        }

        private void OnServerInitialized()
        {
            recyclerItemId = ItemManager.FindItemDefinition(configData.recyclerItemName).itemid;

            Recycler[] allobjects = UnityEngine.Object.FindObjectsOfType<Recycler>();
            foreach (Recycler r in allobjects)
            {
                if (r.OwnerID != 0)
                {
                    if (!r.gameObject.GetComponent<RecyclerEntity>())
                        r.gameObject.AddComponent<RecyclerEntity>();
                    
                    ApplyRecyclerSettings(r, true);
                }
            }

            if (configData.spawnInLoot)
            {
                bool hasChanged = false;
                foreach (LootContainer container in UnityEngine.Resources.FindObjectsOfTypeAll<LootContainer>())
                {
                    ConfigData.Loot l;
                    if (!configData.FindLootContainer(container.ShortPrefabName, out l))
                    {
                        configData.loot.Add(new ConfigData.Loot { containerName = container.ShortPrefabName });
                        hasChanged = true;
                    }
                }
                
                if (hasChanged)
                    SaveConfig();

                Subscribe(nameof(OnLootSpawn));
                
                foreach (LootContainer container in UnityEngine.Object.FindObjectsOfType<LootContainer>())
                    container.Invoke(container.SpawnLoot, Random.Range(0f, 10f));
            }

            if (configData.useCrafting)
            {
                foreach (KeyValuePair<string, int> kvp in configData.itemsNeededToCraft)
                {
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(kvp.Key);
                    if (!itemDefinition)
                    {
                        PrintWarning($"Invalid shortname for crafting item {kvp.Key}");
                        continue;
                    }

                    itemsNeededToCraft[itemDefinition.itemid] = new KeyValuePair<string, int>(itemDefinition.displayName.english, kvp.Value);
                }
            }
        }
        
        private void OnNewSave()
        {
            pluginData = new PluginData();
            SaveData();
        }

        private void OnServerSave() => SaveData();
        
        private void OnEntityBuilt(Planner plan, GameObject obj)
        {
            BaseEntity entity = obj.GetComponent<BaseEntity>();
            if (entity && entity.ShortPrefabName == configData.recyclerShortPrefabName && entity.skinID == RECYCLER_SKIN_ID)
            {
                BasePlayer player = plan.GetOwnerPlayer();
                if (!configData.allowDeployOnGround && player.net.connection.authLevel < 2)
                {
                    if (!IsOnBuildingBlock(entity))
                    {
                        GiveRecycler(player);
                        SendMsg(player, "place on construction");
                        
                        NextTick(() =>
                        {
                            if (entity && !entity.IsDestroyed)
                                entity.Kill();
                        });
                        return;
                    }
                }

                Recycler recycler = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", entity.transform.position, entity.transform.rotation) as Recycler;
                recycler.OwnerID = configData.adminSpawnsPublicRecycler && player.net.connection.authLevel == 2 ? 0UL : player.userID.Get();
                recycler.Spawn();
                
                NextTick(() =>
                {
                    if (entity && !entity.IsDestroyed)
                    {
                        if (entity.HasParent() && recycler && !recycler.IsDestroyed)
                            recycler.SetParent(entity.GetParentEntity(), true, true);

                        entity.Kill();
                    }
                });
                
                recycler.gameObject.AddComponent<RecyclerEntity>();
                ApplyRecyclerSettings(recycler);
            }
        }

        private void OnEntityGroundMissing(Recycler recycler)
        {
            if (recycler.OwnerID != 0UL)
                CreateRecycler().Drop(recycler.transform.position, Vector3.up);
        }
        
        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!player || info == null || !info.HitEntity) 
                return;
            
            RecyclerEntity rec = info.HitEntity.GetComponent<RecyclerEntity>();
            if (rec && rec.OwnerID != 0)
            {
                if (configData.allowRepair)
                    ShowUIHealth(player, $"{(int)info.HitEntity.Health()} / {(int)info.HitEntity.MaxHealth()}");

                if (configData.allowPickupByHammerHit)
                {
                    if (pickupRecyclers.ContainsKey(player))
                        return;

                    if (configData.restrictUseByCupboard && !player.CanBuild())
                    {
                        SendMsg(player, "buldingBlocked");
                        return;
                    }

                    if (configData.pickupOnlyOwnerFriends && !(rec.OwnerID == player.userID || (bool) (Friends?.Call("AreFriends", rec.OwnerID, player.userID) ?? false)))
                    {
                        SendMsg(player, "cant pick");
                        return;
                    }

                    pickupRecyclers.Add(player, rec);
                    
                    timer.In(30f, () =>
                    {
                        if (!player) 
                            return;
                        
                        if (pickupRecyclers.ContainsKey(player))
                        {
                            pickupRecyclers.Remove(player);
                        }
                    });
                    
                    if (configData.allowRepair && info.HitEntity.Health() < info.HitEntity.MaxHealth())
                    {
                        SendMsg(player, "repair first");
                        return;
                    }

                    ShowUIPickup(player);
                }
            }
        }

        private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn() || (recycler.OwnerID == 0 && !configData.canChangePublicRecyclerParams))
                return;
            
            if (configData.restrictUseByCupboard && recycler.OwnerID != 0 && !player.CanBuild())
            {
                NextTick(() =>
                {
                    SendMsg(player, "buldingBlocked");
                    recycler.StopRecycling();
                });
                return;
            }

            startedRecyclers[recycler.net.ID] = player.userID;
            NextTick(() =>
            {
                if (!recycler.IsOn()) 
                    return;
                
                foreach (KeyValuePair<string, ConfigData.Rates> perm in configData.PermissionsRates)
                {
                    if (permission.UserHasPermission(player.UserIDString, string.Format(PERMISSION_GENERIC, perm.Key)))
                    {
                        recycler.CancelInvoke(recycler.RecycleThink);
                        recycler.InvokeRepeating(recycler.RecycleThink, perm.Value.Speed, perm.Value.Speed);
                        return;
                    }
                }

                if (configData.DefaultRates.Speed != 5f)
                {
                    recycler.CancelInvoke(recycler.RecycleThink);
                    recycler.InvokeRepeating(recycler.RecycleThink, configData.DefaultRates.Speed, configData.DefaultRates.Speed);
                }
            });
        }

        private object OnItemRecycle(Item item, Recycler recycler)
        {
            if (recycler.OwnerID == 0 && !configData.canChangePublicRecyclerParams) 
                return null;
            
            if (item.info.Blueprint == null || configData.blackList.Contains(item.info.shortname))
                return true;
            
            bool flag = false;
            float num = configData.DefaultRates.Ratio;
            float percentToTake = configData.DefaultRates.percentOfMaxStackToTake;
            if (startedRecyclers.ContainsKey(recycler.net.ID))
            {
                foreach (KeyValuePair<string, ConfigData.Rates> perm in configData.PermissionsRates)
                {
                    if (permission.UserHasPermission(startedRecyclers[recycler.net.ID].ToString(), string.Format(PERMISSION_GENERIC, perm.Key)))
                    {
                        num = perm.Value.Ratio;
                        percentToTake = perm.Value.percentOfMaxStackToTake;
                        break;
                    }
                }
            }

            if (item.hasCondition)
            {
                num = Mathf.Clamp01(num * Mathf.Clamp(item.conditionNormalized * item.maxConditionNormalized, 0.1f, 1f));
            }

            int num2 = 1;
            if (item.amount > 1)
            {
                num2 = Mathf.CeilToInt(Mathf.Min((float) item.amount, (float) item.info.stackable * percentToTake));
            }

            if (item.info.Blueprint.scrapFromRecycle > 0)
            {
                float ratioScrap = configData.DefaultRates.RatioScrap;
                if (startedRecyclers.ContainsKey(recycler.net.ID))
                {
                    foreach (KeyValuePair<string, ConfigData.Rates> perm in configData.PermissionsRates)
                    {
                        if (permission.UserHasPermission(startedRecyclers[recycler.net.ID].ToString(), string.Format(PERMISSION_GENERIC, perm.Key)))
                        {
                            ratioScrap = perm.Value.RatioScrap;
                            break;
                        }
                    }
                }

                int scrap = 0;
                try
                {
                    float scrp = item.info.Blueprint.scrapFromRecycle * num2 * ratioScrap;
                    scrap = (int) (scrp);
                    if (scrp > 0f && scrp < 1f)
                    {
                        if (Random.Range(0f, 1f) < ratioScrap)
                        {
                            scrap = 1;
                            Item newItem = ItemManager.CreateByName("scrap", scrap, 0uL);
                            recycler.MoveItemToOutput(newItem);
                        }
                    }
                    else
                    {
                        Item newItem = ItemManager.CreateByName("scrap", scrap, 0uL);
                        recycler.MoveItemToOutput(newItem);
                    }
                }
                catch
                {
                    LogToFile("debug", $"[{DateTime.Now}] GOT ERROR recycler of player {recycler.OwnerID} tried to recycle {item.info.shortname}x{num2} to scrap {scrap}", this);
                }
            }

            item.UseItem(num2);
            foreach (ItemAmount ingredient in item.info.Blueprint.ingredients)
            {
                if (ingredient.itemDef.shortname != "scrap")
                {
                    float num3 = num2 * num * (float) ingredient.amount / (float) item.info.Blueprint.amountToCreate;
                    int num4 = Mathf.FloorToInt(num3);
                    if (num3 < 1f && UnityEngine.Random.Range(0f, 1f) > 0.5f) num4 = 1;
                    if (num4 > 0)
                    {
                        int num5 = Mathf.CeilToInt((float) num4 / (float) ingredient.itemDef.stackable);
                        for (int k = 0; k < num5; k++)
                        {
                            int num6 = 0;
                            try
                            {
                                num6 = (num4 <= ingredient.itemDef.stackable) ? num4 : ingredient.itemDef.stackable;
                                Item newItem2 = ItemManager.Create(ingredient.itemDef, num6, 0uL);
                                if (!recycler.MoveItemToOutput(newItem2))
                                {
                                    flag = true;
                                }
                            }
                            catch
                            {
                                LogToFile("debug", $"[{DateTime.Now}] GOT ERROR recycler of player {recycler.OwnerID} tried to recycle {item.info.shortname}x{num2} to ingredient {ingredient.itemDef.shortname}x{num6}", this);
                            }

                            num4 -= num6;
                            if (num4 <= 0)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            if (flag) recycler.StopRecycling();
            return true;
        }

        private void OnLootSpawn(LootContainer container)
        {
            timer.In(1f, () =>
            {
                if (!container || container.IsDestroyed) 
                    return;
                
                ConfigData.Loot cont;
                if (!configData.FindLootContainer(container.ShortPrefabName, out cont))
                    return;
                
                if (cont.probability < 1) 
                    return;
                
                if (Random.Range(0, 100) <= cont.probability)
                {
                    container.inventorySlots = container.inventory.itemList.Count() + 5;
                    container.inventory.capacity = container.inventory.itemList.Count() + 5;
                    container.SendNetworkUpdateImmediate();
                    TryGiveRecycler(container.inventory);
                }
            });
        }

        private object CanStackItem(Item item, Item anotherItem)
        {
            if (item.info.itemid == recyclerItemId && item.skin != anotherItem.skin) 
                return false;
            return null;
        }

        private object OnItemSplit(Item item, int split_Amount)
        {
            if (item.info.itemid == recyclerItemId && item.skin == RECYCLER_SKIN_ID)
            {
                Item byItemId = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                item.amount -= split_Amount;
                byItemId.amount = split_Amount;
                byItemId.name = item.name;
                item.MarkDirty();
                return byItemId;
            }

            return null;
        }

        private object CanCombineDroppedItem(DroppedItem drItem, DroppedItem anotherDrItem)
        {
            if (drItem.item.info.itemid == recyclerItemId && drItem.item.info.itemid == anotherDrItem.item.info.itemid && drItem.item.skin != anotherDrItem.item.skin) 
                return false;
            return null;
        }
        
        private void Unload()
        {
            RecyclerEntity.OnUnload();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_PICKUP);
                CuiHelper.DestroyUi(player, UI_CRAFT);
                CuiHelper.DestroyUi(player, UI_HEALTH);
            }
        }
        #endregion

        #region Functions
        private void RegisterPermissions()
        {
            foreach (KeyValuePair<string, ConfigData.Rates> perm in configData.PermissionsRates)
                permission.RegisterPermission(string.Format(PERMISSION_GENERIC, perm.Key), this);

            permission.RegisterPermission(PERMISSION_CANGET, this);
            permission.RegisterPermission(PERMISSION_CANCRAFT, this);
            permission.RegisterPermission(PERMISSION_IGNORE_COOLDOWN, this);
            permission.RegisterPermission(PERMISSION_IGNORE_CRAFT_COOLDOWN, this);
        }
        
        private bool IsOnBuildingBlock(BaseEntity entity)
        {
            GroundWatch component = entity.gameObject.GetComponent<GroundWatch>();
            List<Collider> list = Facepunch.Pool.Get<List<Collider>>();
            
            Vis.Colliders(entity.transform.TransformPoint(component.groundPosition), component.radius, list, component.layers, QueryTriggerInteraction.Collide);
            
            foreach (Collider collider in list)
            {
                if (collider.transform.root != entity.gameObject.transform.root)
                {
                    BaseEntity baseEntity = collider.gameObject.ToBaseEntity();
                    
                    if (baseEntity && !baseEntity.IsDestroyed && baseEntity is BuildingBlock)
                    {
                        Facepunch.Pool.FreeUnmanaged(ref list);
                        return true;
                    }
                }
            }

            Facepunch.Pool.FreeUnmanaged(ref list);
            return false;
        }
        
        private void ApplyRecyclerSettings(BaseCombatEntity recycler, bool init = false)
        {
            if (configData.allowDamage || configData.allowRepair)
            {
                BaseCombatEntity itemToClone = GameManager.server.FindPrefab(configData.prefabToCloneDamage).GetComponent<BaseCombatEntity>();
                recycler._maxHealth = configData.health;
                
                if (!init)
                    recycler.health = recycler.MaxHealth();
                
                if (configData.allowRepair)
                {
                    recycler.repair.enabled = true;
                    recycler.repair.itemTarget = itemToClone.repair.itemTarget;
                }

                if (configData.allowDamage)
                    recycler.baseProtection = itemToClone.baseProtection;
            }
        }

        private ConfigData.Rates GetRatesForPlayer(BasePlayer player)
        {
            foreach (KeyValuePair<string, ConfigData.Rates> perm in configData.PermissionsRates)
            {
                if (permission.UserHasPermission(player.UserIDString, string.Format(PERMISSION_GENERIC, perm.Key)))
                    return perm.Value;
            }

            return configData.DefaultRates;
        }

        private double CurrentTimeStamp() => DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
        
        private float GetCraftLimit(BasePlayer player) => GetRatesForPlayer(player).craftLimit;

        private float GetSpawnLimit(BasePlayer player) => GetRatesForPlayer(player).spawnLimit;

        private float GetCraftCooldown(BasePlayer player) => GetRatesForPlayer(player).craftCooldown;

        private float GetSpawnCooldown(BasePlayer player) => GetRatesForPlayer(player).spawnCooldown;
        
        private void EndCrafting(BasePlayer player)
        {
            string message = string.Empty;
            bool hasEnoughResources = true;
            
            foreach (KeyValuePair<int, KeyValuePair<string, int>> item in itemsNeededToCraft)
            {
                if (player.inventory.GetAmount(item.Key) >= item.Value.Value) 
                    continue;
                
                message += string.Format(Message("not enough ingredient", player) + "\n", item.Value.Key, item.Value.Value);
                hasEnoughResources = false;
            }

            if (!hasEnoughResources)
            {
                SendReply(player, message);
                return;
            }

            if (configData.useCraftCooldown)
            {
                float craftCooldown = GetCraftCooldown(player);
                pluginData.userCooldownsCraft[player.userID] = CurrentTimeStamp() + craftCooldown;
            }

            if (configData.useCraftLimit)
            {
                if (!pluginData.userCrafted.ContainsKey(player.userID)) 
                    pluginData.userCrafted.Add(player.userID, 0);
                
                pluginData.userCrafted[player.userID]++;
            }

            foreach (KeyValuePair<int, KeyValuePair<string, int>> item in itemsNeededToCraft)
                player.inventory.Take(null, item.Key, item.Value.Value);
            
            SendMsg(player, GiveRecycler(player) ? "recycler crafted" : "inventory full");
        }

        private Item CreateRecycler()
        {
            Item item = ItemManager.CreateByName(configData.recyclerItemName, 1, RECYCLER_SKIN_ID);
            item.name = "Recycler";
            return item;
        }
        
        private void TryGiveRecycler(ItemContainer container)
        {
            Item item = CreateRecycler();
            
            if (!item.MoveToContainer(container, -1, false))
                item.Remove(0f);
        }

        private bool GiveRecycler(BasePlayer player)
        {
            Item item = CreateRecycler();
            
            if (!player.inventory.GiveItem(item))
            {
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                return false;
            }
            
            if (!string.IsNullOrEmpty(item.name))
                player.Command("note.inv", item.info.itemid, 1, item.name, (int) BaseEntity.GiveItemReason.PickedUp);
            else player.Command("note.inv", item.info.itemid, 1, string.Empty, (int) BaseEntity.GiveItemReason.PickedUp);

            return true;
        }
        #endregion

        #region Commands
        private void cmdRec(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_CANGET))
            {
                SendMsg(player, "badCommand");
                return;
            }

            if (configData.useSpawnCooldown)
            {
                if (!permission.UserHasPermission(player.UserIDString, PERMISSION_IGNORE_COOLDOWN))
                {
                    double time = CurrentTimeStamp();
                    float spawnCooldown = GetSpawnCooldown(player);
                    
                    if (!pluginData.userCooldowns.ContainsKey(player.userID)) 
                        pluginData.userCooldowns.Add(player.userID, time + spawnCooldown);
                    else
                    {
                        double nextUseTime = pluginData.userCooldowns[player.userID];
                        if (nextUseTime > time)
                        {
                            SendMsg(player, "cooldown", true, (int) (nextUseTime - time));
                            return;
                        }
                        
                        pluginData.userCooldowns[player.userID] = time + spawnCooldown;
                    }
                }
            }

            if (configData.useSpawnLimit)
            {
                if (!pluginData.userSpawned.ContainsKey(player.userID)) 
                    pluginData.userSpawned.Add(player.userID, 0);
                
                float spawnLimit = GetSpawnLimit(player);
                if (pluginData.userSpawned[player.userID] >= spawnLimit)
                {
                    SendMsg(player, "limit", true, spawnLimit);
                    return;
                }

                pluginData.userSpawned[player.userID]++;
            }

            SendMsg(player, GiveRecycler(player) ? "recycler got" : "inventory full");
        }

        private void cmdCraft(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_CANCRAFT))
            {
                SendMsg(player, "cannot craft");
                return;
            }

            if (configData.useCraftCooldown)
            {
                if (!permission.UserHasPermission(player.UserIDString, PERMISSION_IGNORE_CRAFT_COOLDOWN))
                {
                    double time = CurrentTimeStamp();
                    if (pluginData.userCooldownsCraft.ContainsKey(player.userID))
                    {
                        double nextUseTime = pluginData.userCooldownsCraft[player.userID];
                        if (nextUseTime > time)
                        {
                            SendMsg(player, "cooldown craft", true, (int)(nextUseTime - time));
                            return;
                        }
                    }
                }
            }

            if (configData.useCraftLimit)
            {
                if (!pluginData.userCrafted.ContainsKey(player.userID)) 
                    pluginData.userCrafted.Add(player.userID, 0);
                
                float craftLimit = GetCraftLimit(player);
                if (pluginData.userCrafted[player.userID] >= craftLimit)
                {
                    SendMsg(player, "limit", true, craftLimit);
                    return;
                }
            }

            if (configData.useUIForCrafting)
            {
                ShowUICraft(player);
                return;
            }

            EndCrafting(player);
        }
        #endregion

        #region Console Commands
        [ConsoleCommand("craftrecycler")]
        private void CcmdCraft(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            
            CuiHelper.DestroyUi(player, UI_CRAFT);

            if (arg.Args == null)
            {
                if (!permission.UserHasPermission(player.UserIDString, PERMISSION_CANCRAFT))
                {
                    SendMsg(player, "cannot craft");
                    return;
                }

                EndCrafting(player);
            }
        }

        [ConsoleCommand("giverecycler")]
        private void cmdGiveRecycler(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Connection.player as BasePlayer;
                if (player == null)
                    return;

                if (player.net.connection.authLevel < 2)
                    return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "bad syntax");
                return;
            }

            BasePlayer targetPlayer = BasePlayer.Find(arg.Args[0]);
            if (targetPlayer == null)
            {
                SendReply(arg, "error player not found for give");
                return;
            }

            SendReply(targetPlayer, Message(GiveRecycler(targetPlayer) ? "recycler got" : "inventory full", targetPlayer));
        }
        
        [ConsoleCommand("pickuprecycler")]
        private void cmdPickupRecycler(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            
            CuiHelper.DestroyUi(player, UI_PICKUP);

            if (arg.Args != null)
                return;
            
            if (!pickupRecyclers.ContainsKey(player)) 
                return;

            RecyclerEntity recyclerEntity;
            if (!pickupRecyclers.TryGetValue(player, out recyclerEntity))
                return;
            
            pickupRecyclers.Remove(player);

            Recycler recycler = recyclerEntity.Recycler;
            
            if (recycler && !recycler.IsDestroyed)
            {
                recycler.inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", recycler.transform.position + new Vector3(0f, 1f, 0f), recycler.transform.rotation, 0f);

                recycler.Kill();
                
                SendMsg(player, GiveRecycler(player) ? "recycler got" : "inventory full");
            }
        }
        #endregion
        
        #region UI Helper
        public static class UI
        {
            private const string COLOR_CLEAR = "0 0 0 0";

            public static CuiElementContainer Container(string panel, Anchor anchor, Offset offset)
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = COLOR_CLEAR },
                            RectTransform = { AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString() },
                            CursorEnabled = false
                        },
                        new CuiElement().Parent = "Hud",
                        panel
                    }
                };
                return container;
            }

            public static CuiElementContainer Container(string panel, string color, Anchor anchor, Offset offset)
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                            RectTransform = { AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString() },
                            CursorEnabled = true
                        },
                        new CuiElement().Parent = "Hud",
                        panel
                    }
                };
                return container;
            }

            public static CuiElement Panel(CuiElementContainer container, string panel, string color, Anchor anchor, Offset offset)
            {
                CuiElement cuiElement;
                container.Add(cuiElement = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiImageComponent {Color = color, Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/iconmaterial.mat"},
                        new CuiRectTransformComponent {AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString()},
                    }
                });
                return cuiElement;
            }

            public static void Label(CuiElementContainer container, string panel, string text, int size, Anchor anchor, Offset offset, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent { FontSize = size, Align = align, Text = text },
                        new CuiRectTransformComponent {AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString()},
                    }
                });
            }

            public static void Image(CuiElementContainer container, string panel, int itemId, ulong skinId, Anchor anchor, Offset offset)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiImageComponent { ItemId = itemId, SkinId = skinId },
                        new CuiRectTransformComponent { AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString() },
                    }
                });
            }

            public static void Button(CuiElementContainer container, string panel, string color, string text, int fontSize, Anchor anchor, Offset offset, string command)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiImageComponent { Color = color, Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/iconmaterial.mat" },
                        new CuiRectTransformComponent {AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString()},
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString() },
                        new CuiTextComponent {Text = text, FontSize = fontSize, Align = TextAnchor.MiddleCenter}
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiButtonComponent {Color = COLOR_CLEAR, Command = command},
                        new CuiRectTransformComponent { AnchorMin = anchor.Min.ToString(), AnchorMax = anchor.Max.ToString(), OffsetMin = offset.Min.ToString(), OffsetMax = offset.Max.ToString() },
                    }
                });
            }

            public struct Anchor
            {
                public Bounds Min;
                public Bounds Max;

                public Anchor(float xMin, float yMin, float xMax, float yMax)
                {
                    this.Min = new Bounds(xMin, yMin);
                    this.Max = new Bounds(xMax, yMax);
                }
                
                public static Anchor TopLeft = new Anchor(0f, 1f, 0f, 1f);
                public static Anchor TopCenter = new Anchor(0.5f, 1f, 0.5f, 1f);
                public static Anchor TopRight = new Anchor(1f, 1f, 1f, 1f);
                public static Anchor CenterLeft = new Anchor(0f, 0.5f, 0f, 0.5f);
                public static Anchor Center = new Anchor(0.5f, 0.5f, 0.5f, 0.5f);
                public static Anchor CenterRight = new Anchor(1f, 0.5f, 1f, 0.5f);
                public static Anchor BottomLeft = new Anchor(0f, 0f, 0f, 0f);
                public static Anchor BottomCenter = new Anchor(0.5f, 0f, 0.5f, 0f);
                public static Anchor BottomRight = new Anchor(1f, 0f, 1f, 0f);

                public static Anchor FullStretch = new Anchor(0f, 0f, 1f, 1f);
                public static Anchor TopStretch = new Anchor(0f, 1f, 1f, 1f);
                public static Anchor HoriztonalCenterStretch = new Anchor(0f, 0.5f, 1f, 0.5f);
                public static Anchor BottomStretch = new Anchor(0f, 0f, 1f, 0f);
                public static Anchor LeftStretch = new Anchor(0f, 0f, 0f, 1f);
                public static Anchor VerticalCenterStretch = new Anchor(0.5f, 0f, 0.5f, 1f);
                public static Anchor RightStretch = new Anchor(1f, 0f, 1f, 1f);

                public override string ToString() => $"{Min.ToString()} {Max.ToString()}";
            }

            public struct Offset
            {
                public Bounds Min;
                public Bounds Max;

                public static Offset zero = new Offset(0, 0, 0, 0);

                public Offset(float xMin, float yMin, float xMax, float yMax)
                {
                    this.Min = new Bounds(xMin, yMin);
                    this.Max = new Bounds(xMax, yMax);
                }
                
                public override string ToString() => $"{Min.ToString()} {Max.ToString()}";
            }

            public struct Bounds
            {
                public readonly float X;
                public readonly float Y;

                public Bounds(float x, float y)
                {
                    this.X = x;
                    this.Y = y;
                }

                public override string ToString() => $"{X} {Y}";
            }
        }
        #endregion

        #region UI
        private const string UI_CRAFT = "homerecycler.craft";
        private const string UI_PICKUP = "homerecycler.pickup";
        private const string UI_HEALTH = "homerecycler.health";
        
        private const string RED = "#CE422B";
        private const string GREEN = "#BAFF00";

        private readonly Hash<BasePlayer, Timer> m_HealthTimers = new Hash<BasePlayer, Timer>();
        
        private void ShowUIHealth(BasePlayer player, string message)
        {
            CuiElementContainer container = UI.Container(UI_HEALTH, UI.Anchor.Center, new UI.Offset(-125, -125, 125, 100));
            UI.Label(container, UI_HEALTH, message, 14, UI.Anchor.FullStretch, UI.Offset.zero);
            
            CuiHelper.DestroyUi(player, UI_HEALTH);
            CuiHelper.AddUi(player, container);

            Timer t;
            if (m_HealthTimers.TryGetValue(player, out t))
                t.Destroy();

            m_HealthTimers[player] = timer.In(3f, () =>
            {
                CuiHelper.DestroyUi(player, UI_HEALTH);
                m_HealthTimers.Remove(player);
            });
        }

        private void ShowUICraft(BasePlayer player)
        {            
            const int ITEM_PIXELS = 68;

            float height = (float)((configData.itemsNeededToCraft.Count * ITEM_PIXELS) + 50) * 0.5f;

            CuiElementContainer container = UI.Container(UI_CRAFT, "0 0 0 0.9", UI.Anchor.Center, new UI.Offset(-125, -height, 125, height));

            UI.Label(container, UI_CRAFT, Message("CraftTitle", player), 16, UI.Anchor.TopStretch, new UI.Offset(0, -25, 0, 0));

            bool hasEnoughResources = true;

            int i = 0;
            foreach (KeyValuePair<string, int> cost in configData.itemsNeededToCraft)
            {
                ItemDefinition itemDefinition;
                if (ItemManager.itemDictionaryByName.TryGetValue(cost.Key, out itemDefinition))
                {
                    CuiElement parent = UI.Panel(container, UI_CRAFT, "0 0 0 0", UI.Anchor.TopStretch, new UI.Offset(2, -25 - (ITEM_PIXELS * (i + 1)), -2, -25 - (ITEM_PIXELS * i)));
                    UI.Panel(container, parent.Name, "1 1 1 0.1", UI.Anchor.FullStretch, new UI.Offset(0, 1, 0, -1));
                    UI.Image(container, parent.Name, itemDefinition.itemid, 0UL, UI.Anchor.CenterLeft, new UI.Offset(2, -32, 66, 32));
                    UI.Label(container, parent.Name, itemDefinition.displayName.english, 12, UI.Anchor.TopStretch, new UI.Offset(70, -22.66667f, 0, 0), TextAnchor.MiddleLeft);
                    UI.Label(container, parent.Name, string.Format(Message("Required", player), cost.Value), 12, UI.Anchor.HoriztonalCenterStretch, new UI.Offset(70, -11.33333f, 0, 11.33333f), TextAnchor.MiddleLeft);

                    int playerAmount = player.inventory.GetAmount(itemDefinition.itemid);
                    string have;
                    if (playerAmount < cost.Value)
                    {
                        hasEnoughResources = false;
                        have = string.Format(Message("Have", player), RED, playerAmount);
                    }
                    else have = string.Format(Message("Have", player), GREEN, playerAmount);

                    UI.Label(container, parent.Name, have, 12, UI.Anchor.BottomStretch, new UI.Offset(70, 0f, 0, 22.66667f), TextAnchor.MiddleLeft);
                }

                i++;
            }

            CuiElement buttonContainer = UI.Panel(container, UI_CRAFT, "0 0 0 0", UI.Anchor.BottomStretch, new UI.Offset(0, 0, 0, 25));
            UI.Button(container, buttonContainer.Name, "0.8078 0.2588 0.1686 0.8", Message("Cancel", player), 14, UI.Anchor.FullStretch, new UI.Offset(2, 2, -150, -2), "craftrecycler close");

            if (hasEnoughResources)
                UI.Button(container, buttonContainer.Name, "0.7292 1 0 0.8", Message("Craft", player), 14, UI.Anchor.FullStretch, new UI.Offset(102, 2, -2, -2), "craftrecycler");
            else UI.Button(container, buttonContainer.Name, "0.9622 0.9022 0.5129 0.8", Message("InsuffRes", player), 14, UI.Anchor.FullStretch, new UI.Offset(102, 2, -2, -2), string.Empty);

            CuiHelper.DestroyUi(player, UI_CRAFT);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIPickup(BasePlayer player)
        {
            CuiElementContainer container = UI.Container(UI_PICKUP, "0 0 0 0.9", UI.Anchor.Center, new UI.Offset(-125, -25, 125, 25));

            UI.Label(container, UI_PICKUP, Message("UIPickup", player), 16, UI.Anchor.TopStretch, new UI.Offset(0, -25, 0, 0));

            CuiElement buttonContainer = UI.Panel(container, UI_PICKUP, "0 0 0 0", UI.Anchor.BottomStretch, new UI.Offset(0, 0, 0, 25));
            UI.Button(container, buttonContainer.Name, "0.8078 0.2588 0.1686 0.8", Message("UIPickupNo", player), 14, UI.Anchor.FullStretch, new UI.Offset(2, 2, -126, -2), "pickuprecycler close");

            UI.Button(container, buttonContainer.Name, "0.7292 1 0 0.8", Message("UIPickupYes", player), 14, UI.Anchor.FullStretch, new UI.Offset(126, 2, -2, -2), "pickuprecycler");
            
            CuiHelper.DestroyUi(player, UI_PICKUP);
            CuiHelper.AddUi(player, container);
        }
        #endregion
        
        #region Component
        private class RecyclerEntity : MonoBehaviour
        {
            private Recycler m_Entity;

            public Recycler Recycler => m_Entity;
            
            public ulong OwnerID => m_Entity ? m_Entity.OwnerID : 0UL;

            private static readonly List<RecyclerEntity> m_AllRecyclers = new List<RecyclerEntity>();

            private void Awake()
            {
                m_Entity = GetComponent<Recycler>();
                
                AddComponentIfMissing<DestroyOnGroundMissing>();
                AddComponentIfMissing<GroundWatch>();
                
                m_AllRecyclers.Add(this);
            }

            private void OnDestroy() =>  m_AllRecyclers.Remove(this);

            private void AddComponentIfMissing<T>() where T : Component
            {
                if (!gameObject.GetComponent<T>())
                    gameObject.AddComponent<T>();
            }

            public static void OnUnload()
            {
                for (int i = m_AllRecyclers.Count - 1; i >= 0; i--)
                {
                    RecyclerEntity recyclerEntity = m_AllRecyclers[i];
                    if (recyclerEntity)
                        Destroy(recyclerEntity);
                }
                
                m_AllRecyclers.Clear();
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {      
            public string chatCommand { get; set; }
            public string craftCommand { get; set; }
            public bool useUIForCrafting { get; set; }
            public string recyclerItemName { get; set; }
            public string recyclerShortPrefabName { get; set; }
            public bool allowDamage { get; set; }
            public bool allowRepair { get; set; }
            public string prefabToCloneDamage { get; set; }
            public float health { get; set; }
            public bool canChangePublicRecyclerParams { get; set; }
            public bool restrictUseByCupboard { get; set; }
            public bool adminSpawnsPublicRecycler { get; set; }
            public bool useSpawning { get; set; }
            public bool useCrafting { get; set; }
            public bool useSpawnCooldown { get; set; }
            public bool useCraftCooldown { get; set; }
            public bool useSpawnLimit { get; set; }
            public bool useCraftLimit { get; set; }
            public bool allowDeployOnGround { get; set; }
            public bool allowPickupByHammerHit { get; set; }
            public bool pickupOnlyOwnerFriends { get; set; }
            public bool spawnInLoot { get; set; }
            public bool trackStacking { get; set; }
            public bool dropRecyclerItemWhenFloorDestroyed { get; set; }
            public Rates DefaultRates { get; set; }
            public List<string> blackList { get; set; }
            public Dictionary<string, int> itemsNeededToCraft { get; set; }
            public Dictionary<string, Rates> PermissionsRates { get; set; }

            [JsonProperty("Loot")] public List<Loot> loot { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
            
            public class Loot
            {
                public string containerName;
                public int probability = 0;
            }

            public class Rates
            {
                public int Priority = 1;
                public float spawnCooldown = 86400f;
                public float craftCooldown = 86400f;
                public int craftLimit = 1;
                public int spawnLimit = 1;
                public float Ratio = 0.5f;
                public float RatioScrap = 1f;
                public float Speed = 5f;
                public float percentOfMaxStackToTake = 0.1f;
            }

            public bool FindLootContainer(string name, out Loot result)
            {
                for (int i = 0; i < loot.Count; i++)
                {
                    Loot l = loot[i];
                    if (l.containerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        result = l;
                        return true;
                    }
                }

                result = null;
                return false;
            }
        }        

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();
            
            configData.PermissionsRates = configData.PermissionsRates.OrderBy(i => -i.Value.Priority).ToDictionary(x => x.Key, x => x.Value);

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                chatCommand = "rec",
                craftCommand = "craftrecycler",
                useUIForCrafting = true,
                recyclerItemName = "research.table",
                recyclerShortPrefabName = "researchtable_deployed",
                allowDamage = true,
                allowRepair = true,
                prefabToCloneDamage = "assets/prefabs/deployable/research table/researchtable_deployed.prefab",
                health = 500f,
                canChangePublicRecyclerParams = false,
                restrictUseByCupboard = true,
                adminSpawnsPublicRecycler = false,
                useSpawning = false,
                useCrafting = false,
                useSpawnCooldown = true,
                useCraftCooldown = true,
                useSpawnLimit = false,
                useCraftLimit = false,
                allowDeployOnGround = false,
                allowPickupByHammerHit = true,
                pickupOnlyOwnerFriends = true,
                spawnInLoot = false,
                trackStacking = false,
                DefaultRates = new ConfigData.Rates(),
                blackList = new List<string>(),
                itemsNeededToCraft = new Dictionary<string, int>()
                {
                    ["scrap"] = 750, 
                    ["gears"] = 25, 
                    ["metalspring"] = 25
                },
                PermissionsRates = new Dictionary<string, ConfigData.Rates>
                {
                    ["viptest"] = new ConfigData.Rates(), 
                    ["viptest2"] = new ConfigData.Rates
                    {
                        Priority = 2, 
                        Ratio = 0.7f, 
                        Speed = 3f
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
       
        #endregion
        
        #region Data
        private PluginData pluginData;

        private class PluginData
        {
            public Dictionary<ulong, double> userCooldowns = new Dictionary<ulong, double>();
            public Dictionary<ulong, int> userSpawned = new Dictionary<ulong, int>();
            public Dictionary<ulong, double> userCooldownsCraft = new Dictionary<ulong, double>();
            public Dictionary<ulong, int> userCrafted = new Dictionary<ulong, int>();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, pluginData);

        private void LoadData()
        {
            pluginData = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(this.Title);
            
            if (pluginData == null)
                pluginData = new PluginData();
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"Title", "Recycler:"},
                {"badCommand", "you can get recycler in kit home"},
                {"buldingBlocked", "you need building privilege"},
                {"cooldown", "Сooldown, wait {0} seconds"},
                {"cooldown craft", "Сooldown, wait {0} seconds"},
                {"recycler crafted", "You have crafted a recycler"},
                {"recycler got", "You have got a recycler"},
                {"cannot craft", "Sorry, you can't craft a recycler"},
                {"not enough ingredient", "You should have {0} x{1}"},
                {"inventory full", "You should have space in inventory"},
                {"limit", "You have reached the limit of {0} recyclers"},
                {"place on construction", "You can't place it on ground"},
                {"cant pick", "You can pickup only your own or friend recycler"},
                {"UIPickup", "Pickup recycler?"},
                {"UIPickupYes", "Yes"},
                {"UIPickupNo", "No"},
                {"UICraft", "Crafting recycler"},
                {"UICraftYes", "Craft"},
                {"UICraftNo", "Cancel"},
                {"notEnough", "not enough"},
                {"repair first", "You should repair it first"},
                {"Have", "Have : <color={0}>{1}</color>"},
                {"Required", "Required : {0}"},
                {"CraftTitle", "Recycler Crafting"},
                {"Cancel", "Cancel"},
                {"Craft", "Craft"},
                {"InsuffRes", "Insufficient Resources"},
            }, this, "en");
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"Title", "Переработчик:"},
                {"badCommand", "ты можешь получить его в kit home"},
                {"buldingBlocked", "нужна авторизация в шкафу"},
                {"cooldown", "Подождите еще {0} секунд"},
                {"cooldown craft", "Подождите еще {0} секунд"},
                {"recycler crafted", "Ты скрафтил переработчик"},
                {"recycler got", "Ты получил переработчик"},
                {"cannot craft", "Ты не можешь крафтить переработчик"},
                {"not enough ingredient", "Тебе нужно {0} x{1}"},
                {"inventory full", "Нет места в инвентаре"},
                {"limit", "Достигнут лимит в {0} переработчика"},
                {"place on construction", "Нельзя ставить на землю"},
                {"cant pick", "Ты можешь поднять только свой переработчик или друга"},
                {"UIPickup", "Поднять переработчик?"},
                {"UIPickupYes", "Да"},
                {"UIPickupNo", "Нет"},
                {"UICraft", "Крафт переработчика"},
                {"UICraftYes", "Скрафтить"},
                {"UICraftNo", "Отмена"},
                {"notEnough", "недостаточно"},
                {"repair first", "Сначала отремонтируй"},
                {"Have", "Есть : <color={0}>{1}</color>"},
                {"Required", "Необходимый : {0}"},
                {"CraftTitle", "Крафт"},
                {"Cancel", "Отмена"},
                {"Craft", "Ремесло"},
                {"InsuffRes", "Недостаточно ресурсов"},
            }, this, "ru");
        }
        
        private string Message(string key, BasePlayer player = null) => lang.GetMessage(key, this, player ? player.UserIDString : string.Empty);

        private void SendMsg(BasePlayer player, string langkey, bool title = true, params object[] args)
        {
            string message = args?.Length > 0 ? string.Format(Message(langkey, player), args) : Message(langkey, player);
            
            if (title) 
                message = $"<color=orange>{Message("Title", player)}</color> {message}";
            
            SendReply(player, message);
        }
        #endregion

    }
}