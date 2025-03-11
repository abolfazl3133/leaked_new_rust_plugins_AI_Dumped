/*
                                   /\  /\  /\
     TTTTT  H   H  EEEE     K  K  |  \/  \/  |  NN   N   GGG
       T    H   H  E        K K   *----------*  N N  N  G
       T    HHHHH  EEE      KK     I  I  I  I   N  N N  G  GG
       T    H   H  E        K K    I  I  I  I   N   NN  G   G
       T    H   H  EEEE     K  K   I  I  I  I   N    N   GGG


This plugin (the software) is © copyright the_kiiiing.

You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without explicit consent from the_kiiiing.

DISCLAIMER:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

//#define DEBUG
using Facepunch;
using \u0048\u0061\u0072\u006D\u006F\u006E\u0079Lib;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Plugins.LoottableExtensions;
using Oxide.Plugins.LoottableInterop;
using PluginComponents.Loottable;
using PluginComponents.Loottable.Assertions;
using PluginComponents.Loottable.Core;
using PluginComponents.Loottable.Cui;
using PluginComponents.Loottable.Cui.Images;
using PluginComponents.Loottable.Cui.Style;
using PluginComponents.Loottable.Extensions.Reflection;
using PluginComponents.Loottable.Extensions.String;
using PluginComponents.Loottable.Extensions.Lang;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Oxide.Core.Libraries;
using PluginComponents.Loottable.Extensions.BaseNetworkable;
using PluginComponents.Loottable.Extensions.Command;
using PluginComponents.Loottable.Tools.Entity;
using Rust;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using UnityEngine;

/*
 * 
 * VERSION HISTORY
 * 
 * V 2.0.0
 * - v2 release
 *
 * V 2.0.1
 * - fix error in OnLootSpawn
 * - fix conflict with HumanNPC plugin
 * - add legacy wood pile
 * - fix hook errors
 * - changed the way NPC loot works (hopefully more reliable)
 * - added popup message when loot profile can not be enabled
 *
 * V 2.0.2
 * - fix NRE in CanStackItem
 * - fix NRE in Unload
 * - fix stack size controller import
 * - add import function for custom items
 * - add settings page, option to manually import profiles
 * - fix min/max item amount ignored by loot generator
 * - add automatic loot refresh back (can be enabled in settings)
 * - fix NRE in OnItemAction
 * - add separate loot handling for scarecrows
 *
 * V 2.0.3
 * - fix NRE in OnLootSpawn
 * - fix memory leak in LootManager.ApplyCustomLoot
 *
 * V 2.1.0
 * - add smelting, supply drop and recycler config back
 * - add import function for recycler, supply drop and smelting config
 * - add separate config for safe zone recyclers
 * - fix custom items not displayed correctly
 * - fix no fuel in miner hat / no water in bottles
 * - fix incorrect vanilla stack size (sometimes)
 * - remove stack size limit (allows larger stacks than max stack size)
 *
 * V 2.1.1
 * - add debugging to some methods
 * - fix NRE in Unload
 * - add search bar to item list
 * - fix stacking problems with water
 *
 * V 2.1.2
 * - fix locked crate marker bug with LootApi
 * - fix NRE in Init
 *
 * V 2.1.3
 * - fix supply drop config not working
 * - add supply signal cooldown
 * - fix empty default config for scarecrow npc
 * - rework gather configurations
 * - add gather multipliers
 * - add wolf and scientist corpse gather config
 * - remove unused code
 *
 * V 2.1.4
 * - suppress errors in legacy import to make sure it is only called once
 * - add skull spikes and space suit items
 * - add radtown ores to ore config
 * - stack size controller fixes
 * 
 */

namespace Oxide.Plugins
{
    [Info(nameof(Loottable), "The_Kiiiing", "2.1.4")]
    internal class Loottable : BasePlugin<Loottable, Loottable.Configuration>
    {
        private const string API_URL = "https://rust-api.the-king.dev";

        private const string DATA_FOLDER = "Loottable2";

        [Perm]
        private const string PERM_EDIT = "loottable.edit";

        private State state;

        private readonly StackSizeController stackSizeController;
        private readonly ImageHelper imageHelper;

        private TheManagerThatManagesEverything manager;

        private readonly OtherConfigManager configurations;

        [PluginReference, UsedImplicitly]
        private readonly Plugin ImageLibrary, RustTranslationAPI, DeployableNature, LootDefender, FancyDrop, SimpleSplitter;

        #region Hooks

        #region Default

        public Loottable()
        {
            stackSizeController = new StackSizeController();
            imageHelper = new ImageHelper(this);
            configurations = new OtherConfigManager();
        }
        
        [Debug]
        protected override void Init()
{string ___flow = string.Empty;
try{___flow += "a";
            base.Init();
___flow += "b";

            Unsubscribe();
___flow += "c";
            
            state = State.Load();
___flow += "d";
            state.Update(Version);
___flow += "e";
            state.Save();
___flow += "f";

            LogDebug($"Fresh install: {state.IsFreshInstall} Update: {state.IsUpdated}");
___flow += "g";
            
            manager = new TheManagerThatManagesEverything();
___flow += "h";
            manager.Init();
___flow += "i";
            
            imageHelper.Update = DEBUG || state.IsUpdated;
___flow += "j";
            imageHelper.AddImages(manager.GetImages());
___flow += "k";

            AddUniversalCommand("loottable", nameof(CmdLoottable));
___flow += "l";

            ItemHelper.Init();
___flow += "m";
            
            configurations.Load();
___flow += "n";
            stackSizeController.Load();
___flow += "o";
            
            if (!state.LegacyImported)
{___flow += "p";
                try
                {
                    ImportLegacyFiles(LegacyImportTarget.EVERYTHING, false);
                }
                catch (Exception ex)
                {
                    LogError($"Failed to import legacy data files: {ex}");
                }
___flow += "q";
                
                state.LegacyImported = true;
___flow += "r";
                state.Save();
}___flow += "s";
            
            Subscribe();
___flow += "t";
            
            UpdateQuarries(false);
___flow += "u";
            
            if (DebugPlayer != null)
{___flow += "v";
                playerUi = new PlayerUi(DebugPlayer);
___flow += "w";
                playerUi.Create();
}}catch(System.Exception ex){BasePlugin.LogError($"Failed to call Init path '{___flow}' {ex}");
throw;
}}
        protected override void Unload()
        {
            playerUi?.Destroy();

            stackSizeController.Unload();

            manager?.Destroy();

            UpdateQuarries(true);

            state.Save();
            
            SmeltingConfiguration.ResetBurnable();
            
            base.Unload();
        }

        protected override void OnServerInitialized()
        {
            base.OnServerInitialized();
            
            stackSizeController.Initialize();
            
            configurations.Initialize();
        }

        protected override void OnServerInitializedDelayed()
        {
            base.OnServerInitializedDelayed();
            
            if (!imageHelper.Initialized && (ImageLibrary != null || CARBONARA))
            {
                imageHelper.Init();
            }
            
            UpdateConfigSubscriptions();
        }

        [Hook] void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == null)
            {
                return;
            }
            
            if (!imageHelper.Initialized && plugin.Name == "ImageLibrary")
            {
                imageHelper.Init();
            }

            if (plugin.Name == nameof(FancyDrop) || plugin.Name == nameof(SimpleSplitter) || plugin.Name == nameof(LootDefender))
            {
                UpdateConfigSubscriptions();
            }
        }

        
        [Hook] void OnPluginUnloaded(Plugin plugin)
        {
            manager.CustomItems.RemoveCustomItems(plugin, false);
            
            if (plugin.Name == nameof(FancyDrop) || plugin.Name == nameof(SimpleSplitter) || plugin.Name == nameof(LootDefender))
            {
                UpdateConfigSubscriptions();
            }
        }

        #endregion

        #region Loot

        // ReSharper disable once RedundantNameQualifier
        [Hook] object OnCorpseApplyLoot(global::HumanNPC npc, NPCPlayerCorpse corpse)
        {
            if (!CanPopulateCorpseHook(corpse))
            {
                return null;
            }

            var config = manager.GetConfig(npc);
            if (config != null && config.Enabled)
            {
                LootManager.PopulateCorpse(npc, corpse, config);
                return true;
            }
            
            return null;
        }
        
        [Hook] object OnCorpseApplyLoot(ScarecrowNPC npc, NPCPlayerCorpse corpse)
        {
            if (!CanPopulateCorpseHook(corpse))
            {
                return null;
            }

            var config = manager.GetConfig(npc);
            if (config != null && config.Enabled)
            {
                LootManager.PopulateCorpse(npc, corpse, config);
                return true;
            }
            
            return null;
        }

        [Hook] object OnLootSpawn(LootContainer container)
        {
            int label = 0;
            try
            {
                if (!container.IsValid())
                {
                    return null;
                }

                label = 1;
                if (!CanPopulateContainerHook(container))
                {
                    return null;
                }

                label = 2;
                var config = manager.GetConfig(container);
                
                label = 3;
                if (config != null && config.Enabled)
                {
                    label = 4;
                    if (config.LootType != LootType.Custom)
                    {
                        label = 5;
                        container.PopulateLoot();
                    }

                    label = 6;
                    LootManager.PopulateContainer(container, config);

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to call OnLootSpawn on {container} (label: {label}) {ex}");
            }

            return null;
        }

        [Hook] object OnCollectiblePickup(CollectibleEntity entity, BasePlayer player, bool eat)
        {
            if (player == null || entity == null || eat)
            {
                return null;
            }

            if (IsDeployableNature(entity))
            {
                return null;
            }

            var config = manager.GetConfig(entity);
            if (config != null && config.Enabled)
            {
                LootManager.GiveItemsToPlayer(player, config, true);
                entity.itemList = null;
                if (entity.pickupEffect.isValid)
                {
                    Effect.server.Run(entity.pickupEffect.resourcePath, entity.transform.position, entity.transform.up);
                }
                entity.Kill();
                return true;
            }

            return null;

            bool IsDeployableNature(BaseEntity entity)
            {
                if (DeployableNature == null)
                {
                    return false;
                }

                var b = DeployableNature.Call("STCanGainXP", null, entity) as bool?;
                if (b == false)
                {
                    return true;
                }

                return false;
            }
        }

        [Hook] object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item?.info != null && action == "unwrap")
            {
                var config = manager.GetConfig(item);
                if (config != null && config.Enabled)
                {
                    if (item.amount > 1)
                    {
                        item.amount--;
                        item.MarkDirty();
                    }
                    else
                    {
                        item.RemoveFromContainer();
                        item.Remove();
                    }

                    LootManager.GiveItemsToPlayer(player, config, false);

                    return true;
                }
            }

            return null;
        }

        [Hook] object OnTrainWagonLootSpawn(TrainCarUnloadable wagon, StorageContainer container)
        {
            var config = manager.GetConfig(wagon);
            if (config != null && config.Enabled)
            {
                LogDebug($"Fill wagon {config.Id}");
                LootManager.PopulateTrainWagon(wagon, container, config);
                return true;
            }


            return null;
        }

        [Hook] float? OnTrainWagonOreLevel(TrainCarUnloadable wagon)
        {
            var config = manager.GetConfig(wagon);
            if (config != null && config.Enabled)
            {
                LogDebug($"Get ore percent {config.Id}");
                return 1f;
            }

            return null;
        }

        #endregion

        #region Quarry

        [Hook] void OnQuarryToggled(MiningQuarry quarry)
        {
            if (!quarry.isStatic)
            {
                return;
            }

            var config = manager.GetConfig(quarry);
            if (config != null && config.Enabled && quarry.IsEngineOn())
            {
                LogDebug("Init quarry");
                LootManager.InitQuarry(quarry, config);
            }
            else
            {
                LootManager.ResetQuarry(quarry);
            }
        }

        [Hook] object OnExcavatorProduceResources(ExcavatorArm arm)
        {
            var config = manager.GetConfig(arm);
            if (config != null && config.Enabled)
            {
                LootManager.ProcessExcavatorResources(arm, config);
                return true;
            }

            return null;
        }

        private void UpdateQuarries(bool reset)
        {
            foreach (var quarry in BaseNetworkable.serverEntities.OfType<MiningQuarry>())
            {
                if (!quarry.IsNullOrDestroyed() && quarry.IsValid())
                {
                    if (reset)
                    {
                        LootManager.ResetQuarry(quarry);
                    }
                    else
                    {
                        OnQuarryToggled(quarry);
                    }
                }
            }
        }

        #endregion

        #region Gathering

        [Hook] void OnResourceDispenserInitialize(ResourceDispenser dispenser)
        {
            if (dispenser.baseEntity == null)
            {
                return;
            }

            var config = manager.GetConfig(dispenser.baseEntity);
            if (config != null && config.Enabled)
            {
                foreach (var item in config.DefaultCategory)
                {
                    if (item.Select())
                    {
                        dispenser.containedItems.Add(CreateItemAmount(item));
                    }
                }

                var finishBonus = config.GetCategories().FirstOrDefault();
                if (finishBonus != null)
                {
                    foreach (var item in finishBonus.Items)
                    {
                        if (item.Select())
                        {
                            dispenser.finishBonus.Add(CreateItemAmount(item));
                        }
                    }
                }
            }

            static ItemAmount CreateItemAmount(LootItem item)
            {
                return new ItemAmount(item.ItemDefinition, item.Amount.Select());
            }
        }
        
        [Hook] void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) => OnDispenserGather(dispenser, player, item);
        [Hook] void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            var config = manager.GetConfig(dispenser.baseEntity);
            if (config != null && config.Enabled)
            {
                config.MultiplyDispenserItem(item);
            }
        }
        
        #endregion

        #region Stacking

        [Hook] bool? CanStackItem(Item target, Item item)
        {
            if (item == null)
            {
                LogError("CanStackItem: item is null");
                return null;
            }
            if (target == null)
            {
                LogError("CanStackItem: target is null");
                return null;
            }
            
            if (target.GetOwnerPlayer().IsUnityNull() || item.GetOwnerPlayer().IsUnityNull())
            {
                return null;
            }
            
            if (target.info.itemid != item.info.itemid
                || target.skin != item.skin
                || target.name != item.name
                || target.text != item.text
                || item.info.condition.enabled && !Mathf.Approximately(target.condition, item.condition)
                || target.blueprintTarget != item.blueprintTarget)
            {
                return false;
            }

            if (target.info.amountType == ItemDefinition.AmountType.Genetics || item.info.amountType == ItemDefinition.AmountType.Genetics)
            {
                if ((target.instanceData?.dataInt ?? -1) != (item.instanceData?.dataInt ?? -1))
                {
                    return false;
                }
            }
            
            if (ItemHelper.IsLiquidContainer(target.info.itemid) && ((item.contents != null && !item.contents.IsEmpty()) || (target.contents != null && !target.contents.IsEmpty())))
            {
                return false;
            }

            return true;
        }

        [Hook] void OnItemStacked(Item target, Item source, ItemContainer destinationContainer)
        {
            if (destinationContainer.GetOwnerPlayer() is not BasePlayer player || player.IsNpc)
            {
                return;
            }

            var itemId = source.info.itemid;
            if (ItemHelper.IsBaseProjectile(itemId) && source.GetHeldEntity() is BaseProjectile weapon)
            {
                var ammoCount = weapon.primaryMagazine.contents;
                if (ammoCount > 0)
                {
                    weapon.SetAmmoCount(0);
                    source.MarkDirty();
                    weapon.SendNetworkUpdateImmediate();
                    ItemHelper.GiveToPlayer(weapon.primaryMagazine.ammoType, ammoCount, player);
                }
            }
            else if (itemId == ItemHelper.CHAINSAW_ITEMID && source.GetHeldEntity() is Chainsaw chainsaw)
            {
                var ammoCount = chainsaw.ammo;
                if (ammoCount > 0)
                {
                    chainsaw.ammo = 0;
                    chainsaw.SendNetworkUpdateImmediate();
                    ItemHelper.GiveToPlayer(chainsaw.fuelType, ammoCount, player);
                }
            }
            else if (itemId == ItemHelper.FLAMETHROWER_ITEMID && source.GetHeldEntity() is FlameThrower flameThrower)
            {
                var ammoCount = flameThrower.ammo;
                if (ammoCount > 0)
                {
                    flameThrower.ammo = 0;
                    flameThrower.SendNetworkUpdateImmediate();
                    ItemHelper.GiveToPlayer(flameThrower.fuelType, ammoCount, player);
                }
            }
            else if (ItemHelper.IsMinerHat(itemId) && source.contents?.IsEmpty() == false)
            {
                var fuelItem = source.contents.itemList.FirstOrDefault();
                if (fuelItem != null)
                {
                    fuelItem.RemoveFromContainer();
                    ItemHelper.GiveToPlayer(fuelItem, player);
                }
            }
        }

        [Hook] Item OnItemSplit(Item item, int splitAmount)
        {
            item.amount -= splitAmount;
            
            var split = ItemManager.Create(item.info, splitAmount, item.skin);
            split.name = item.name;
            split.text = item.text;

            if (item.IsBlueprint())
            {
                split.blueprintTarget = item.blueprintTarget;
            }

            if (item.info.amountType == ItemDefinition.AmountType.Genetics && item.instanceData != null && item.instanceData.dataInt != 0)
            {
                split.instanceData = new ProtoBuf.Item.InstanceData();
                split.instanceData.dataInt = item.instanceData.dataInt;
                split.instanceData.ShouldPool = false;
            }

            if (item.instanceData != null && item.instanceData.dataInt > 0 && item.info != null && item.info.Blueprint != null && item.info.Blueprint.workbenchLevelRequired == 3)
            {
                split.instanceData = new ProtoBuf.Item.InstanceData();
                split.instanceData.dataInt = item.instanceData.dataInt;
                split.instanceData.ShouldPool = false;
                split.SetFlag(global::Item.Flag.IsOn, item.IsOn());
            }

            // ADDED
            if (ItemHelper.IsBaseProjectile(split.info.itemid) && split.GetHeldEntity() is BaseProjectile weapon)
            {
                if (weapon.primaryMagazine.contents > 0)
                {
                    weapon.SetAmmoCount(0);
                    split.MarkDirty();
                    weapon.SendNetworkUpdateImmediate();
                }
            }
            else if (split.info.itemid == ItemHelper.FLAMETHROWER_ITEMID && split.GetHeldEntity() is FlameThrower flameThrower)
            {
                if (flameThrower.ammo > 0)
                {
                    flameThrower.ammo = 0;
                    flameThrower.SendNetworkUpdateImmediate();
                }
            }

            item.MarkDirty();
            return split;
        }

        // returns only true or null
        [Hook] object CanCombineDroppedItem(DroppedItem item1, DroppedItem item2)
        {
            if (!item1.item.CanStack(item2.item))
            {
                return true;
            }
            if (ItemHelper.IsBaseProjectile(item1.item.info.itemid) || item1.item.contents != null || item2.item.contents != null)
            {
                return true;
            }

            return null;
        }

        // Conveyor stack fix
        static class ItemContainer_QuickIndustrialPreCheck
        {
            public static bool Prefix(Item toTransfer, Vector2i range, out int foundSlot, ItemContainer __instance, ref bool __result)
            {
                int num = range.y - range.x + 1;
                int count = __instance.itemList.Count;
                int num2 = 0;
                foundSlot = -1;
                for (int i = 0; i < count; i++)
                {
                    Item item = __instance.itemList[i];
                    int position = item.position;
                    if (position < range.x || position > range.y)
                    {
                        continue;
                    }

                    num2++;
                    if (item.amount >= item.info.stackable)
                    {
                        continue;
                    }

                    //if (toTransfer.IsBlueprint())
                    //{
                    //    if (item.blueprintTarget != toTransfer.blueprintTarget)
                    //    {
                    //        continue;
                    //    }
                    //}
                    //else if (item.info != toTransfer.info)
                    //{
                    //    continue;
                    //}

                    var stack = Instance?.CanStackItem(item, toTransfer);
                    if (stack == null)
                    {
                        // Default behavior
                        if (toTransfer.IsBlueprint())
                        {
                            if (item.blueprintTarget != toTransfer.blueprintTarget)
                            {
                                continue;
                            }
                        }
                        else if (item.info != toTransfer.info)
                        {
                            continue;
                        }
                    }
                    else if (stack == false)
                    {
                        continue;
                    }

                    foundSlot = position;
                    //return true;
                    __result = true;
                    return false;
                }

                //return num2 < num;
                __result = num2 < num;
                return false;
            }
        }

        #endregion

        #region Custom 

        private static bool CanPopulateContainerHook(LootContainer container)
        {
            var result = Interface.CallHook("OnContainerPopulate", container);
            return result == null;
        }

        private static bool CanPopulateCorpseHook(LootableCorpse corpse)
        {
            var result = Interface.CallHook("OnCorpsePopulate", corpse);
            return result == null;
        }
        
        private static bool CanUseCustomAirdropHook(SupplySignal signal, ThrownWeapon item)
        {
            object result = Interface.CallHook("OnCustomAirdrop", signal, item);
            return result == null;
        }

        #endregion
        
        #region Configuration Stuff

        private void UpdateConfigSubscriptions(bool unsubscribe = false)
        {
            if (!unsubscribe && !configurations.IsLoaded)
            {
                return;
            }
            
            if (!unsubscribe && configurations.SupplyDrop.Enabled && FancyDrop == null && LootDefender == null)
            {
                Subscribe(nameof(OnExplosiveDropped));
                Subscribe(nameof(OnExplosiveThrown));
            }
            else
            {
                Unsubscribe(nameof(OnExplosiveDropped));
                Unsubscribe(nameof(OnExplosiveThrown));
            }

            if (!unsubscribe && configurations.Smelting.Enabled && SimpleSplitter == null)
            {
                Subscribe(nameof(OnOvenStarted));
                Subscribe(nameof(OnMixingTableToggle));
            }
            else
            {
                Unsubscribe(nameof(OnOvenStarted));
                Unsubscribe(nameof(OnMixingTableToggle));
            }

            if (!unsubscribe && configurations.Smelting.RecyclerEnabled)
            {
                Subscribe(nameof(OnRecyclerToggle));
            }
            else
            {
                Unsubscribe(nameof(OnRecyclerToggle));
            }
        }

        private readonly Dictionary<ulong, DateTime> _playerCooldowns = new();
        
        [Hook] void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon item) => OnExplosiveThrown(player, entity, item);

        [Hook] void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            if (!configurations.IsLoaded || !configurations.SupplyDrop.Enabled || entity is not SupplySignal signal)
            {
                return;
            }

            if (LootDefender != null
                || FancyDrop != null
                || Interface.CallHook("IsBradleyDrop", signal.skinID) != null
                || Interface.CallHook("IsHeliSignalObject", signal.skinID) != null
                || !CanUseCustomAirdropHook(signal, item))
            {
                return;
            }

            if (configurations.SupplyDrop.CooldownEnabled 
                && _playerCooldowns.TryGetValue(player.userID, out var lastSupplyTime) 
                && DateTime.UtcNow.Subtract(lastSupplyTime).TotalSeconds < configurations.SupplyDrop.CooldownSeconds)
            {
                ItemHelper.GiveToPlayer(item.GetOwnerItemDefinition(), 1, player, signal.skinID);
                signal.Kill();
                
                var remaining = TimeSpan.FromSeconds(configurations.SupplyDrop.CooldownSeconds).Subtract(TimeSpan.FromSeconds(DateTime.UtcNow.Subtract(lastSupplyTime).TotalSeconds));
                player.SendConsoleCommand("gametip.showtoast", 1, lang.GetMessage("sd_cooldown_toast", player, remaining.Minutes, remaining.Seconds) );
                return;
            }

            _playerCooldowns[player.userID] = DateTime.UtcNow;
            configurations.SupplyDrop.DeliverCustomSupplyDrop(signal);
        }
        
        [Hook] void OnMixingTableToggle(MixingTable table, BasePlayer player)
        {
            if (!configurations.IsLoaded || !configurations.Smelting.Enabled || SimpleSplitter != null)
            {
                return;
            }

            configurations.Smelting.ToggleMixingTable(table);
        }
        
        [Hook] void OnOvenStarted(BaseOven oven)
        {
            if (!configurations.IsLoaded || !configurations.Smelting.Enabled || SimpleSplitter != null)
            {
                return;
            }
            
            configurations.Smelting.UpdateOvenSpeed(oven);
        }
        
        [Hook] void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (!configurations.IsLoaded || !configurations.Smelting.Enabled)
            {
                return;
            }

            configurations.Smelting.ToggleRecycler(recycler);
        }
        
        #endregion

        #region Subs

        private void Subscribe()
        {
            //Subscribe(nameof(OnCorpsePopulate));
            Subscribe(nameof(OnLootSpawn));
            Subscribe(nameof(OnCollectiblePickup));
            Subscribe(nameof(OnItemAction));

            Subscribe(nameof(OnQuarryToggled));
            
            Subscribe(nameof(OnDispenserGather));
            Subscribe(nameof(OnDispenserBonus));

            //OnExcavatorProduceResources
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(ExcavatorArm), "ProduceResources"), AccessTools.Method(typeof(ExcavatorArm_ProduceResources), "Prefix"));

            //OnResourceDispenserInitialize
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(ResourceDispenser), "Initialize"), AccessTools.Method(typeof(ResourceDispenser_Initialize), "Prefix"));

            //OnCorpseApplyLoot
            // ReSharper disable once RedundantNameQualifier
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(global::HumanNPC), "ApplyLoot"), AccessTools.Method(typeof(HumanNPC_ApplyLoot), nameof(HumanNPC_ApplyLoot.Prefix)));
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(ScarecrowNPC), "ApplyLoot"), AccessTools.Method(typeof(ScarecrowNPC_ApplyLoot), nameof(ScarecrowNPC_ApplyLoot.Prefix)));
            
            // WAGONS
            //OnTrainWagonLootSpawn
            //\u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(TrainCarUnloadable), "FillWithLoot"), AccessTools.Method(typeof(TrainCarUnloadable_FillWithLoot), "Prefix"));
            //OnTrainWagonOreLevel
            //\u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(TrainCarUnloadable), "GetOrePercent"), AccessTools.Method(typeof(TrainCarUnloadable_GetOrePrecent), "Prefix"));

            UpdateStackingSubscriptions();
        }

        private void Unsubscribe()
        {
            //Unsubscribe(nameof(OnCorpsePopulate));
            Unsubscribe(nameof(OnLootSpawn));
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnItemAction));

            Unsubscribe(nameof(OnQuarryToggled));
            
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnDispenserBonus));

            //OnExcavatorProduceResources
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(ExcavatorArm), "ProduceResources"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, \u0048\u0061\u0072\u006D\u006F\u006E\u0079Id);

            //OnResourceDispenserInitialize
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(ResourceDispenser), "Initialize"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, \u0048\u0061\u0072\u006D\u006F\u006E\u0079Id);
            
            //OnCorpseApplyLoot
            // ReSharper disable once RedundantNameQualifier
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(global::HumanNPC), "ApplyLoot"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, \u0048\u0061\u0072\u006D\u006F\u006E\u0079Id);
            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(ScarecrowNPC), "ApplyLoot"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, \u0048\u0061\u0072\u006D\u006F\u006E\u0079Id);

            // WAGONS
            //OnTrainWagonLootSpawn
            //\u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(TrainCarUnloadable), "FillWithLoot"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, HarmoniId);
            //OnTrainWagonOreLevel
            //\u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(TrainCarUnloadable), "GetOrePercent"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, HarmoniId);

            UpdateStackingSubscriptions(true);
            
            UpdateConfigSubscriptions(true);
        }

        private void UpdateStackingSubscriptions(bool unsubscribe = false)
        {
            if (stackSizeController.Enabled && !unsubscribe)
            {
                Subscribe(nameof(CanStackItem));
                Subscribe(nameof(OnItemStacked));
                Subscribe(nameof(OnItemSplit));
                Subscribe(nameof(CanCombineDroppedItem));
                \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(ItemContainer), "QuickIndustrialPreCheck"), AccessTools.Method(typeof(ItemContainer_QuickIndustrialPreCheck), nameof(ItemContainer_QuickIndustrialPreCheck.Prefix)));
                //\u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(Item), "MoveToContainer"), AccessTools.Method(typeof(Item_MoveToContainer), "Prefix"));
            }
            else
            {
                Unsubscribe(nameof(CanStackItem));
                Unsubscribe(nameof(OnItemStacked));
                Unsubscribe(nameof(OnItemSplit));
                Unsubscribe(nameof(CanCombineDroppedItem));
                \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(ItemContainer), "QuickIndustrialPreCheck"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, \u0048\u0061\u0072\u006D\u006F\u006E\u0079Id);
                //\u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Unpatch(AccessTools.Method(typeof(Item), "MoveToContainer"), \u0048\u0061\u0072\u006D\u006F\u006E\u0079PatchType.Prefix, \u0048\u0061\u0072\u006D\u006F\u006E\u0079Id);
            }
        }

        static class HumanNPC_ApplyLoot
        {
            // ReSharper disable once RedundantNameQualifier
            public static bool Prefix(NPCPlayerCorpse corpse, global::HumanNPC __instance)
            {
                if (Instance?.OnCorpseApplyLoot(__instance, corpse) != null)
                {
                    return false;
                }

                return true;
            }
        }
        
        static class ScarecrowNPC_ApplyLoot
        {
            public static bool Prefix(NPCPlayerCorpse corpse, ScarecrowNPC __instance)
            {
                if (Instance?.OnCorpseApplyLoot(__instance, corpse) != null)
                {
                    return false;
                }

                return true;
            }
        }

        #endregion

        #endregion

        #region Where the magic happens

        static class LootManager
        {
            public static void RefreshLoot(bool toVanilla, IPlayer player = null)
            {
                // Prevent this from showing up in hook times
                InvokeHandler.Instance.Invoke(() => RefreshLootImpl(toVanilla, player), 0);
            }
            
            private static void RefreshLootImpl(bool toVanilla, [CanBeNull] IPlayer player)
            {
                BasePlugin<Loottable>.Instance.lang.BroadcastMessage("loot_refresh_warning");
                Log("Refreshing loot ...");
                
                int crateCount = 0, dispenserCount = 0;
                foreach (var entity in BaseNetworkable.serverEntities.OfType<BaseEntity>())
                {
                    if (entity is LootContainer container)
                    {
                        if (container is HackableLockedCrate crate && !crate.IsFullyHacked())
                        {
                            continue;
                        }

                        container.inventory.Clear();
                        ItemManager.DoRemoves();

                        if (toVanilla || BasePlugin<Loottable>.Instance.OnLootSpawn(container) == null)
                        {
                            container.PopulateLoot();
                            if (container.shouldRefreshContents)
                            {
                                container.CancelInvoke(container.SpawnLoot);
                                container.Invoke(container.SpawnLoot, UnityEngine.Random.Range(container.minSecondsBetweenRefresh, container.maxSecondsBetweenRefresh));
                            }
                        }
                        crateCount++;
                    }
                    else if (entity.GetComponent<ResourceDispenser>() is ResourceDispenser dispenser)
                    {
                        BasePlugin<Loottable>.Instance.OnResourceDispenserInitialize(dispenser);
                        dispenserCount++;
                    }
                }

                var msg = $"Refreshed {crateCount} crates and {dispenserCount} dispensers";
                Log(msg);
                player?.Reply(msg);
            }
            
            public static void GiveItemsToPlayer(BasePlayer player, LootConfig config, bool beltFirst)
            {
                if (player?.inventory == null || !config.Enabled)
                {
                    return;
                }

                if (config.LootType != LootType.Custom)
                {
                    LogError($"Unexpected loot type {config.LootType} for {config.Id}");
                    return;
                }

                LootNotCoro(beltFirst ? player.inventory.containerBelt : player.inventory.containerMain, config, false, altContainer: (beltFirst ? player.inventory.containerMain : player.inventory.containerBelt), forPlayer: true);
            }

            public static void PopulateTrainWagon(TrainCarUnloadable wagon, StorageContainer container, LootConfig config)
            {
                if (container == null || wagon == null || !config.Enabled)
                {
                    return;
                }

                LootNotCoro(container.inventory, config, true, forWagon: true);

                container.inventory.SetLocked(true);
                wagon.SetVisualOreLevel(wagon.GetOrePercent());
                wagon.SendNetworkUpdate();
            }

            public static void PopulateContainer(ItemContainer container, LootConfig lootConfig)
            {
                if (container == null || !lootConfig.Enabled)
                {
                    return;
                }

                LootNotCoro(container, lootConfig, false);
            }

            public static void PopulateContainer(LootContainer container, LootConfig lootConfig)
            {
                if (container == null || !lootConfig.Enabled)
                {
                    return;
                }

                LootNotCoro(container.inventory, lootConfig, false, lootContainer: container);
            }

            public static void PopulateContainer(StorageContainer container, LootConfig lootConfig)
            {
                if (container == null || !lootConfig.Enabled)
                {
                    return;
                }

                LootNotCoro(container.inventory, lootConfig, false);
            }

            // ReSharper disable once RedundantNameQualifier
            public static void PopulateCorpse(global::HumanNPC npc, LootableCorpse corpse, LootConfig config)
            {
                if (!config.Enabled)
                {
                    return;
                }

                if (config.LootType != LootType.Custom)
                {
                    ApplyVanillaLoot();
                }

                LootNotCoro(corpse.containers[0], config, false, 2);

                if (config.NpcRemoveCorpse)
                {
                    corpse.Invoke(() =>
                    {
                        if (corpse.IsValid() && !corpse.IsDestroyed)
                        {
                            corpse.Kill();
                        }
                    }, 0);
                }

                void ApplyVanillaLoot()
                {
                    if (npc.LootSpawnSlots.Length == 0)
                    {
                        return;
                    }
                    foreach (var lootSpawnSlot in npc.LootSpawnSlots)
                    {
                        for (int index = 0; index < lootSpawnSlot.numberToSpawn; ++index)
                        {
                            if ((String.IsNullOrEmpty(lootSpawnSlot.onlyWithLoadoutNamed) || lootSpawnSlot.onlyWithLoadoutNamed == npc.GetLoadoutName()) && (double) UnityEngine.Random.Range(0.0f, 1f) <= lootSpawnSlot.probability)
                            {
                                lootSpawnSlot.definition.SpawnIntoContainer(corpse.containers[0]);
                            }
                        }
                    }
                }
            }
            
            public static void PopulateCorpse(ScarecrowNPC npc, LootableCorpse corpse, LootConfig config)
            {
                if (!config.Enabled)
                {
                    return;
                }

                if (config.LootType != LootType.Custom)
                {
                    ApplyVanillaLoot();
                }

                LootNotCoro(corpse.containers[0], config, false, 2);

                if (config.NpcRemoveCorpse)
                {
                    corpse.Invoke(() =>
                    {
                        if (corpse.IsValid() && !corpse.IsDestroyed)
                        {
                            corpse.Kill();
                        }
                    }, 0);
                }

                void ApplyVanillaLoot()
                {
                    if (npc.LootSpawnSlots.Length != 0)
                    {
                        foreach (var lootSpawnSlot in npc.LootSpawnSlots)
                        {
                            for (int index = 0; index < lootSpawnSlot.numberToSpawn; ++index)
                            {
                                if (UnityEngine.Random.Range(0.0f, 1f) <= lootSpawnSlot.probability)
                                {
                                    lootSpawnSlot.definition.SpawnIntoContainer(corpse.containers[0]);
                                }
                            }
                        }
                    }
                    
                    if (!npc.wasSoulReleased)
                    {
                        return;
                    }
                    
                    foreach (var bonusLootSlot in npc.bonusLootSlots)
                    {
                        for (int index = 0; index < bonusLootSlot.numberToSpawn; ++index)
                        {
                            if (UnityEngine.Random.Range(0.0f, 1f) <= bonusLootSlot.probability)
                            {
                                bonusLootSlot.definition.SpawnIntoContainer(corpse.containers[0]);
                            }
                        }
                    }
                }
            }

            #region Loot Core
            
            private static void LootNotCoro(ItemContainer container, LootConfig config, bool clear, int wait = 1, LootContainer lootContainer = null, ItemContainer altContainer = null, bool forPlayer = false, bool forWagon = false)
            {
                switch (config.LootType)
                {
                    case LootType.Custom:
                        ApplyCustomLoot(container, config, clear, altContainer, forPlayer, forWagon);
                        break;

                    case LootType.Addition:
                        ApplyAdditions(container, config);
                        break;

                    case LootType.Blacklist:
                        ApplyBlacklist(container, config);
                        break;
                }

                if (lootContainer != null)
                {
                    lootContainer.scrapAmount = container.itemList.Sum(x => x.info.itemid == -932201673 ? x.amount : 0);
                }
            }

            private static void ApplyCustomLoot(ItemContainer container, LootConfig config, bool clear, ItemContainer altContainer = null, bool forPlayer = false, bool forWagon = false)
            {
                var builder = Pool.Get<LootBuilder>();

                // Pick items without category
                AddDefaultItems(config, builder);

                // Pick categories
                AddCategories(config, builder);

                // Too many items
                for (int i = 0; i < Config.GeneratorOptions.MaxIterations && builder.Count > config.Amount.Max; i++)
                {
                    LogDebug("Too many items");
                    
                    // Remove items without category
                    var list = Pool.Get<List<LootItem>>();
                    list.AddRange(builder.GetItems());
                    foreach (var item in list)
                    {
                        if (!item.Select())
                        {
                            builder.RemoveItem(item);

                            if (builder.Count <= config.Amount.Max)
                            {
                                break;
                            }
                        }
                    }
                    Pool.FreeUnmanaged(ref list);
                    
                    // Remove items with category
                    foreach (var cat in config.GetCategories())
                    {
                        var catItems = builder.GetItems(cat.Id);
                        if (catItems.Count > 0 && catItems.Count > cat.Amount.Min)
                        {
                            var items = Pool.Get<List<LootItem>>();
                            items.AddRange(catItems);
                            foreach (var item in items)
                            {
                                if (catItems.Count <= cat.Amount.Min)
                                {
                                    break;
                                }
                                if (!item.Select())
                                {
                                    builder.RemoveItem(item, cat.Id);
                                }
                            }
                            Pool.FreeUnmanaged(ref items);
                        }
                    }
                }

                // Not enough items
                for (int i = 0; i < Config.GeneratorOptions.MaxIterations && builder.Count < config.Amount.Min; i++)
                {
                    LogDebug("not enough items");
                    
                    // Pick items without category
                    AddDefaultItems(config, builder, true);

                    // Pick categories
                    AddCategories(config, builder, true);
                }
                

                // Track stats
                // TODO finish stats
                //config.Stats.Track(config, builder.Count);

                // Clear container if necessary
                if (config.LootType == LootType.Custom && clear)
                {
                    container.ClearItems();
                }

                // Adjust capacity and add items to box
                if (container.capacity < builder.Count)
                {
                    container.capacity = builder.Count;
                }

                foreach (var item in builder.GetAllItems())
                {
                    AddItemToContainer(item, container, respectStackSize: forPlayer || forWagon, sendNote: forPlayer, altContainer: altContainer, dropIfContainerFull: forPlayer);

                    if (item.Extra != null)
                    {
                        AddItemToContainer(item.Extra, container, respectStackSize: forPlayer || forWagon, sendNote: forPlayer, altContainer: altContainer, dropIfContainerFull: forPlayer);
                    }
                }

                Pool.Free(ref builder);

                // Apply cover
                if (!forPlayer && !forWagon && container.capacity > container.itemList.Count && Config.CoverSlots)
                {
                    container.capacity = container.itemList.Count;
                }

                static void AddCategories(LootConfig config, LootBuilder builder, bool breakOnMax = false)
                {
                    if (config.CategoryCount < 1)
                    {
                        return;
                    }

                    foreach (var cat in config.GetCategories())
                    {
                        if (!cat.Select() || cat.Items.Count < 1)
                        {
                            continue;
                        }

                        var amount = cat.Amount.Select();
                        var items = cat.Items.GetRandom(amount);

                        if (breakOnMax && builder.Count + amount > config.Amount.Max)
                        {
                            break;
                        }

                        builder.AddItems(items, cat.Id);
                    }
                }

                static void AddDefaultItems(LootConfig config, LootBuilder builder, bool breakOnMax = false)
                {
                    foreach (var item in config.DefaultCategory)
                    {
                        if (builder.ContainsItem(item) || !item.Select())
                        {
                            continue;
                        }

                        builder.AddItem(item);

                        if (breakOnMax && builder.Count + 1 > config.Amount.Min)
                        {
                            break;
                        }
                    }
                }
            }

            private static void ApplyBlacklist(ItemContainer container, LootConfig config)
            {
                foreach (var item in container.itemList)
                {
                    if (config.Blacklist.Contains(item.info.itemid))
                    {
                        item.Remove();
                    }
                }

                ItemManager.DoRemoves();
            }

            private static void ApplyAdditions(ItemContainer container, LootConfig config)
            {
                foreach (var item in config.GetAdditions())
                {
                    if (item.Select())
                    {
                        if (container.IsFull())
                        {
                            LogError($"Failed to add additional items to {container.entityOwner?.ShortPrefabName} - container is full");
                            break;
                        }

                        AddItemToContainer(item, container);
                    }
                }
            }

            #endregion

            #region Item

            private static int AddItemToContainer(LootItem lootItem, ItemContainer container, int overrideAmount = -1, bool sendNote = false, bool respectStackSize = false, ItemContainer altContainer = null, bool dropIfContainerFull = false)
            {
                int amountTotal = overrideAmount > 0 ? overrideAmount : lootItem.Amount.Select();
                int amountLeft = amountTotal;

                var itemDef = lootItem.ItemDefinition;
                if (itemDef == null)
                {
                    LogError($"Failed to find item with id {lootItem.ItemId}");
                    return 0;
                }

                var ownerPlayer = container.GetOwnerPlayer();
                if (ownerPlayer == null || ownerPlayer.IsNpc)
                {
                    sendNote = false;
                }

                var stack = respectStackSize ? itemDef.stackable : amountLeft;

                while (amountLeft > 0)
                {
                    int amt = Mathf.Min(stack, amountLeft);
                    amountLeft -= stack;

                    Item item;
                    if (lootItem.IsBlueprint)
                    {
                        item = ItemManager.CreateByItemID(-996920608, amt, 0);
                        item.blueprintTarget = lootItem.ItemId;
                    }
                    else
                    {
                        item = ItemManager.Create(itemDef, amt, lootItem.SkinId);
                        if (itemDef.condition.enabled)
                        {
                            item.conditionNormalized = lootItem.Condition.Select();
                        }
                    }

                    if (!String.IsNullOrEmpty(lootItem.CustomName))
                    {
                        item.name = lootItem.CustomName;
                    }

                    item.OnVirginSpawn();

                    if (sendNote)
                    {
                        SendNote(ownerPlayer, item);
                    }

                    if (!item.MoveToContainer(container))
                    {
                        if (altContainer != null && item.MoveToContainer(altContainer))
                        {
                            continue;
                        }
                        else if (dropIfContainerFull)
                        {
                            item.Drop(container.dropPosition, container.dropVelocity);
                            if (sendNote)
                            {
                                SendNote(ownerPlayer, item, true);
                            }
                        }
                        else
                        {
                            item.Remove(0f);
                        }
                    }
                }

                return amountTotal;
            }

            private static void SendNote(BasePlayer player, Item item, bool dropped = false)
            {
                var mpl = dropped ? -1 : 1;
                player.Command("note.inv", item.info.itemid, (item.amount * mpl).ToString(), item.name);
            }

            #endregion

            #region Quarry & Excavator

            private const int QUARRY_WORK_FACTOR = 1000;

            public static void InitQuarry(MiningQuarry quarry, LootConfig config)
            {
                if (quarry.isStatic && quarry._linkedDeposit != null)
                {
                    quarry._linkedDeposit._resources.Clear();

                    foreach (var item in config.DefaultCategory)
                    {
                        quarry._linkedDeposit.Add(item.ItemDefinition, 1f, QUARRY_WORK_FACTOR, QUARRY_WORK_FACTOR / (float)item.Amount.Min, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, quarry.canExtractLiquid);
                    }
                }
            }

            public static void ResetQuarry(MiningQuarry quarry)
            {
                if (quarry.isStatic)
                {
                    quarry.UpdateStaticDeposit();
                }
            }

            private static readonly Dictionary<LootItemId, float> excavatorPendingResources = new();

            public static void ProcessExcavatorResources(ExcavatorArm arm, LootConfig config)
            {
                float fractionPerTick = arm.resourceProductionTickRate / arm.timeForFullResources;
                var numPiles = arm.outputPiles.Count;
                foreach (var item in config.DefaultCategory)
                {
                    excavatorPendingResources.TryAdd(item.Uid, 0);
                    var pending = (excavatorPendingResources[item.Uid] += item.Amount.Min * fractionPerTick);

                    if (pending >= numPiles)
                    {
                        var amountPerPile = Mathf.FloorToInt(pending / numPiles);
                        excavatorPendingResources[item.Uid] -= (amountPerPile * numPiles);

                        foreach (var pile in arm.outputPiles)
                        {
                            AddItemToContainer(item, pile.inventory, amountPerPile, dropIfContainerFull: true);
                        }
                    }
                }
            }

            #endregion
        }

        class LootBuilder : Pool.IPooled
        {
            public int Count { get; private set; }

            private readonly Dictionary<int, HashSet<LootItem>> itemDict = new();

            public IEnumerable<LootItem> GetAllItems()
            {
                foreach (var itemList in itemDict.Values)
                {
                    foreach (var item in itemList)
                    {
                        yield return item;
                    }
                }
            }

            public IReadOnlyCollection<LootItem> GetItems(int category = -1)
            {
                if (itemDict.TryGetValue(category, out var itemList))
                {
                    return itemList;
                }

                return Array.Empty<LootItem>();
            }

            public bool ContainsItem(LootItem item, int category = -1)
            {
                if (itemDict.TryGetValue(category, out var catList))
                {
                    return catList.Contains(item);
                }

                return false;
            }

            public void AddItems(IEnumerable<LootItem> items, int category)
            {
                foreach (var itm in items)
                {
                    AddItem(itm, category);
                }
            }

            public void AddItem(LootItem item, int category = -1)
            {
                if (!itemDict.TryGetValue(category, out var catList))
                {
                    catList = new HashSet<LootItem>();
                    itemDict.Add(category, catList);
                }

                if (catList.Add(item))
                {
                    Count += item.Count;
                }
            }

            public void RemoveItem(LootItem item, int category = -1)
            {
                if (itemDict.TryGetValue(category, out var catList))
                {
                    if (catList.Remove(item))
                    {
                        Count -= item.Count;
                    }
                }
            }

            public void EnterPool()
            {
                foreach (var cat in itemDict)
                {
                    cat.Value.Clear();
                    //var list = cat.Value;
                    //Pool.FreeList(ref list);
                }
                LeavePool();
            }

            public void LeavePool()
            {
                itemDict.Clear();
                Count = 0;
            }
        }

        #endregion

        #region Lootable

        class LootableManager : ILootableManager
        {
            private Dictionary<string, Dictionary<string, string[]>> editorTabs = new();

            private readonly Dictionary<string, BaseLootable> defaultLootableLookup = new();

            private readonly Dictionary<NetworkableId, ConfigId> netIdConfigLookup = new();
            private readonly Dictionary<NpcSpawnPointId, ConfigId> npcConfigLookup = new();
            private readonly Dictionary<uint, ConfigId> prefabIdConfigLookup = new();

            private readonly Dictionary<int, ConfigId> itemIdConfigLookup = new();
            private readonly Dictionary<LootableType, ConfigId> typedLootableConfigLookup = new();

            public void Init(LootableDataList list)
            {
                editorTabs = list?.Tabs ?? new();

                if (list == null)
                {
                    return;
                }

                // Npc config lookup
                foreach (var npcData in list.NpcData)
                {
                    var npc = new Npc(npcData);
                    foreach (var prefabId in npc.PrefabIds)
                    {
                        if (prefabId == 0)
                        {
                            LogWarning($"Config {npc.Key} contains invalid prefab id");
                            continue;
                        }

                        var spawnPointId = new NpcSpawnPointId(npc.SpawnPointPrefabId, prefabId);
                        npcConfigLookup.Add(spawnPointId, npc.Id);
                    }
                    npcSpawnPointPrefabIds.Add(npc.SpawnPointPrefabId);

                    defaultLootableLookup.Add(npc.Key, npc);
                }

                // Crate config lookup
                foreach (var crateData in list.CrateData)
                {
                    AddToLookup(new Crate(crateData));
                }

                // Collectible config lookup
                foreach (var collectibleData in list.CollectableData)
                {
                    AddToLookup(new Collectible(collectibleData));
                }

                // Dispenser
                foreach (var dispenserData in list.DispenserData)
                {
                    AddToLookup(new Dispenser(dispenserData));
                }

                // Unwrapable items
                foreach (var unwrapableData in list.UnwrapableData)
                {
                    var unwrapable = new Unwrapable(unwrapableData);
                    itemIdConfigLookup.Add(unwrapable.ItemId, unwrapable.Id);
                    defaultLootableLookup.Add(unwrapable.Key, unwrapable);
                }

                // Quarrys + Wagons
                foreach (var data in list.OtherData)
                {
                    // WAGONS
                    if (data.Type.IsOneOf(LootableType.WagonFuel, LootableType.WagonCharcoal, LootableType.WagonSulfur, LootableType.WagonMetal))
                    {
                        continue;
                    }

                    var quarry = new TypedLootable(data);
                    typedLootableConfigLookup.Add(quarry.Type, quarry.Id);
                    defaultLootableLookup.Add(quarry.Key, quarry);
                }

                void AddToLookup(BaseLootable lootable)
                {
                    foreach (var prefabId in lootable.GetPrefabIds())
                    {
                        if (prefabId == 0)
                        {
                            LogWarning($"Config {lootable.Key} contains invalid prefab id");
                            continue;
                        }

                        if (!prefabIdConfigLookup.TryAdd(prefabId, lootable.Id))
                        {
                            LogError($"Failed to add lootable {lootable.Key} to lookup - prefab id {prefabId} already exists");
                        }
                    }

                    defaultLootableLookup.Add(lootable.Key, lootable);
                }
            }

            #region Config

            public ConfigId GetTrainWagonConfig(TrainCarUnloadable wagon)
            {
                var type = TypedLootable.GetTrainWagonType(wagon);
                if (typedLootableConfigLookup.TryGetValue(type, out var configId))
                {
                    return configId;
                }

                return ConfigId.Invalid;
            }

            public ConfigId GetExcavatorConfigId(ExcavatorArm arm)
            {
                var type = TypedLootable.GetQuarryType(arm);
                if (typedLootableConfigLookup.TryGetValue(type, out var configId))
                {
                    return configId;
                }

                return ConfigId.Invalid;
            }

            public ConfigId GetQuarryConfigId(MiningQuarry quarry)
            {
                var quarryType = TypedLootable.GetQuarryType(quarry);
                if (typedLootableConfigLookup.TryGetValue(quarryType, out var configId))
                {
                    return configId;
                }

                return ConfigId.Invalid;
            }

            public ConfigId GetItemConfigId(Item item)
            {
                if (itemIdConfigLookup.TryGetValue(item.info.itemid, out var configId))
                {
                    return configId;
                }

                return ConfigId.Invalid;
            }

            public ConfigId GetConfigId(BaseEntity entity, bool isNpc)
            {
                if (netIdConfigLookup.Remove(entity.net.ID, out var configId))
                {
                    LogDebug($"Found custom config {configId} for {entity.ShortPrefabName}");
                    return configId;
                }

                if (isNpc)
                {
                    var spId = GetSpawnPointId(entity);
                    if (npcConfigLookup.TryGetValue(spId, out configId))
                    {
                        return configId;
                    }
                }
                else
                {
                    if (prefabIdConfigLookup.TryGetValue(entity.prefabID, out configId))
                    {
                        return configId;
                    }
                }

                //LogDebug($"No config found for {entity.ShortPrefabName}");
                return ConfigId.Invalid;
            }

            public void SetConfigId(BaseEntity entity, ConfigId configId)
            {
                if (entity?.net != null && entity.net.ID.IsValid)
                {
                    netIdConfigLookup[entity.net.ID] = configId;
                }
            }

            public BaseLootable GetLootable(ConfigId id)
            {
                if (id.Plugin == null)
                {
                    return defaultLootableLookup[id.Key];
                }

                return null;
            }

            public bool LoadDefaultConfig(LootConfig config)
            {
                var lootable = GetLootable(config.Id);
                if (lootable == null || !lootable.HasDefaultConfig)
                {
                    return false;
                }

                return lootable.LoadDefaultConfig(config);
            }

            #endregion

            #region NPC

            private readonly HashSet<uint> npcSpawnPointPrefabIds = new();

            private NpcSpawnPointId GetSpawnPointId(BaseEntity npc)
            {
                var spawnPointPrefab = npc.GetComponent<SpawnPointInstance>()?.parentSpawnPoint?.GetComponentInParent<PrefabParameters>()?.name;
                var spawnPointPrefabId = StringPool.Get(spawnPointPrefab);
                if (npcSpawnPointPrefabIds.Contains(spawnPointPrefabId))
                {
                    return new NpcSpawnPointId(spawnPointPrefabId, npc.prefabID);
                }
                else
                {
                    return new NpcSpawnPointId(0, npc.prefabID);
                }
            }

            private readonly record struct NpcSpawnPointId(uint SpawnPointPrefabId, uint PrefabId)
            {
                public override string ToString()
                {
                    return $"[{StringPool.Get(PrefabId)} at {(SpawnPointPrefabId > 0 ? StringPool.Get(SpawnPointPrefabId) : "<any>")}]";
                }
            }

            #endregion

            #region ILootableManager

            ILootable ILootableManager.GetLootable(ConfigId id)
            {
                return GetLootable(id);
            }

            public IEnumerable<BaseLootable> GetLootables()
            {
                return defaultLootableLookup.Values;
            }

            public IEnumerable<string> GetTabs()
            {
                return editorTabs.Keys;
            }

            public IEnumerable<ILootable> GetTab(string tabName)
            {
                if (!editorTabs.TryGetValue(tabName, out var tab))
                {
                    LogError($"Failed to get tab with name {tabName}");
                    yield break;
                }

                foreach (var category in tab)
                {
                    if (category.Key.Length > 0)
                    {
                        yield return LootableCategory.FromLangKey(category.Key);
                    }

                    foreach (var lootableKey in category.Value)
                    {
                        yield return defaultLootableLookup[lootableKey];
                    }
                }
            }

            #endregion
        }

        #endregion

        #region Lootable Classes

        private class Dispenser : BaseLootable
        {
            public override LootConfigFlags ConfigFlags => LootConfigFlags.Dispenser | LootConfigFlags.DisableCategories;
            public override bool HasDefaultConfig => false;

            public readonly uint[] PrefabIds;

            public Dispenser(DispenserData data)
                : base(data)
            {
                PrefabIds = PrefabNamesToIds(data.PrefabNames);
            }

            public override IEnumerable<uint> GetPrefabIds() => PrefabIds;
            
            public override void InitDispenserMultipliers(LootConfig config)
            {
                config.DispenserMultipliers = new Dictionary<int, float>();
                
                var dispenserEntity = CreateEntity<BaseEntity>();
                if (dispenserEntity == null)
                {
                    throw new Exception($"Failed to initialize configuration of {config.Id} - Dispenser entity is null");
                }

                if (dispenserEntity is HelicopterDebris)
                {
                    // From HelicopterDebris.PhysicsInit
                    AddItem(317398316); // HQ Metal
                    AddItem(69511070); // Metal frags
                    AddItem(-1938052175); // Charcoal
                }
                else
                {
                    var dispenser = dispenserEntity.GetComponent<ResourceDispenser>();
                    if (dispenser == null)
                    {
                        DestroyEntity(dispenserEntity);
                        throw new Exception($"Failed to initialize configuration of {config.Id} - Dispenser component is null");
                    }

                    foreach (var item in dispenser.containedItems)
                    {
                        AddItem(item.itemid);
                    }
                    
                    foreach (var item in dispenser.finishBonus)
                    {
                        AddItem(item.itemid);
                    }
                }
                
                DestroyEntity(dispenserEntity);

                void AddItem(int itemId)
                {
                    config.DispenserMultipliers!.TryAdd(itemId, 1f);
                }
            }
        }

        private class TypedLootable : BaseLootable
        {
            public override LootConfigFlags ConfigFlags => IsWagon() ? LootConfigFlags.TrainWagon : (LootConfigFlags.Quarry | LootConfigFlags.DisableCategories);

            public readonly LootableType Type;

            public TypedLootable(TypedLootableData data)
                : base(data)
            {
                Type = data.Type;
            }

            public bool IsWagon()
            {
                return Type == LootableType.WagonFuel || Type == LootableType.WagonMetal || Type == LootableType.WagonCharcoal || Type == LootableType.WagonSulfur;
            }

            public override bool HasDefaultConfig => !IsWagon();
            public override bool LoadDefaultConfig(LootConfig config)
            {
                // Clear config
                LootConfigHelper.Clear(config);

                switch (Type)
                {
                    case LootableType.ExcavatorHQM:
                        AddItem("hq.metal.ore", 100);
                        break;

                    case LootableType.ExcavatorMetal:
                        AddItem("metal.fragments", 5_000);
                        break;

                    case LootableType.ExcavatorSulfur:
                        AddItem("sulfur.ore", 2_000);
                        break;

                    case LootableType.ExcavatorStone:
                        AddItem("stones", 10_000);
                        break;

                    case LootableType.QuarryAny:
                        AddItem("stones", 3_333);
                        AddItem("metal.ore", 200);
                        AddItem("sulfur.ore", 133);
                        AddItem("hq.metal.ore", 13);
                        break;

                    case LootableType.QuarryStone:
                        AddItem("stones", 5_000);
                        AddItem("metal.ore", 1_000);
                        break;

                    case LootableType.QuarrySulfur:
                        AddItem("sulfur.ore", 1_000);
                        break;
                    
                    case LootableType.QuarryHQM:
                        AddItem("hq.metal.ore", 50);
                        break; 

                    case LootableType.QuarryOil:
                        AddItem("crude.oil", 60);
                        AddItem("lowgradefuel", 170);
                        break;

                    default:
                        return false;
                }

                config.LootType = LootType.Custom;
                config.Validate();
                return true;

                void AddItem(string shortname, int amount, int amountMax = default)
                {
                    var itemId = ItemManager.FindItemDefinition(shortname).itemid;
                    config.DefaultCategory.Add(LootItem.Create(itemId, new MinMax(amount, amountMax == default ? amount : amountMax)));
                }
            }

            public static LootableType GetTrainWagonType(TrainCarUnloadable wagon)
            {
                var lootTypeIndex = wagon.GetField<int>("lootTypeIndex");
                if (lootTypeIndex < 0)
                {
                    TrainWagonLootData.instance.GetLootOption(wagon.wagonType, out lootTypeIndex);
                    wagon.SetField("lootTypeIndex", lootTypeIndex);
                    LogDebug($"set wagon type to {lootTypeIndex}");
                }

                return lootTypeIndex switch
                {
                    1001 => LootableType.WagonFuel,
                    //1000 => CrateWagon
                    2 => LootableType.WagonSulfur,
                    1 => LootableType.WagonMetal,
                    0 => LootableType.WagonCharcoal,
                    _ => LootableType.Invalid
                };
            }

            public static LootableType GetQuarryType(MiningQuarry quarry)
            {
                if (quarry.canExtractLiquid)
                {
                    return LootableType.QuarryOil;
                }

                return quarry.staticType switch
                {
                    MiningQuarry.QuarryType.Basic => LootableType.QuarryStone,
                    MiningQuarry.QuarryType.Sulfur => LootableType.QuarrySulfur,
                    MiningQuarry.QuarryType.HQM => LootableType.QuarryHQM,
                    _ => LootableType.QuarryAny
                };
            }

            public static LootableType GetQuarryType(ExcavatorArm arm)
            {
                return arm.resourceMiningIndex switch
                {
                    0 => LootableType.ExcavatorHQM,
                    1 => LootableType.ExcavatorSulfur,
                    2 => LootableType.ExcavatorStone,
                    3 => LootableType.ExcavatorMetal,
                    _ => LootableType.Invalid
                };
            }
        }

        private class Unwrapable : BaseLootable
        {
            public override LootConfigFlags ConfigFlags => LootConfigFlags.Unwrapable;

            public readonly int ItemId;

            public Unwrapable(UnwrapableData data)
                : base(data)
            {
                ItemId = data.ItemId;
            }
        }

        private class Collectible : BaseLootable
        {
            public override LootConfigFlags ConfigFlags => LootConfigFlags.Collectible;

            public readonly uint[] PrefabIds;

            public Collectible(CollectibleData data)
                : base(data)
            {
                PrefabIds = PrefabNamesToIds(data.PrefabNames);
            }

            public override IEnumerable<uint> GetPrefabIds() => PrefabIds;

            public override bool HasDefaultConfig => true;
            public override bool LoadDefaultConfig(LootConfig config)
            {
                var collectible = CreateEntity<CollectibleEntity>();
                if (collectible == null)
                {
                    return false;
                }

                // Clear config               
                LootConfigHelper.Clear(config);

                // Add items
                if (collectible.itemList != null)
                {
                    config.Amount = new MinMax(1, collectible.itemList.Length);

                    foreach (var item in collectible.itemList)
                    {
                        LootConfigHelper.AddItem(config, item, 1f);
                    }

                    var dispenser = PrefabAttribute.server.Find<RandomItemDispenser>(PrefabIds[0]);
                    if (dispenser != null)
                    {
                        config.Amount = new MinMax(config.Amount.Min, config.Amount.Max + (dispenser.OnlyAwardOne ? 1 : dispenser.Chances.Length));
                        foreach (var item in dispenser.Chances)
                        {
                            config.DefaultCategory.Add(new LootItem
                            {
                                Amount = new MinMax(item.Amount),
                                Chance = item.Chance,
                                ItemId = item.Item.itemid
                            });
                        }
                    }
                }

                // Clean up
                DestroyEntity(collectible);
                config.LootType = LootType.Custom;
                config.Validate();
                return true;
            }
        }

        private class Crate : BaseLootable
        {
            public override LootConfigFlags ConfigFlags => LootConfigFlags.CanChangeLootType;

            public override int MaxSlots { get; }
            public override int DefaultSlots { get; }

            public readonly uint[] PrefabIds;

            public Crate(CrateData data)
                : base(data)
            {
                DefaultSlots = data.DefaultSlots;
                MaxSlots = data.MaxSlots;
                PrefabIds = PrefabNamesToIds(data.PrefabNames);
            }

            public override IEnumerable<uint> GetPrefabIds() => PrefabIds;

            public override bool HasDefaultConfig => true;
            public override bool LoadDefaultConfig(LootConfig config)
            {
                var container = CreateEntity<LootContainer>();
                if (container == null)
                {
                    return false;
                }

                // Clear config               
                LootConfigHelper.Clear(config);

                // Set amount
                config.Amount = new MinMax(1, DefaultSlots);

                // Add scrap
                if (container.scrapAmount > 0)
                {
                    config.DefaultCategory.Add(new LootItem
                    {
                        ItemId = -932201673,
                        Amount = new MinMax(container.scrapAmount),
                        Chance = 1f,
                    });
                }

                // Add other items
                if (container.LootSpawnSlots?.Length > 0)
                {
                    foreach (var slot in container.LootSpawnSlots)
                    {
                        LootConfigHelper.AddItems(config, slot.definition, slot.probability);
                    }
                }
                else if (container.lootDefinition != null)
                {
                    LootConfigHelper.AddItems(config, container.lootDefinition);
                }

                // Clean up
                DestroyEntity(container);
                config.LootType = LootType.Custom;
                config.Validate();
                return true;
            }
        }

        private class Npc : BaseLootable
        {
            public override LootConfigFlags ConfigFlags => LootConfigFlags.Npc | LootConfigFlags.CanChangeLootType;
            public override int DefaultSlots => 24;
            public override int MaxSlots => 24;

            protected override string DefaultImage => "https://files.facepunch.com/rust/item/hazmatsuit_scientist_512.png";

            public readonly uint SpawnPointPrefabId;
            public readonly uint[] PrefabIds;

            public Npc(NpcData data)
                : base(data)
            {
                SpawnPointPrefabId = StringPool.Get(data.SpawnPointPrefabName);
                PrefabIds = PrefabNamesToIds(data.PrefabNames);
            }

            public override IEnumerable<uint> GetPrefabIds() => PrefabIds;

            public override bool HasDefaultConfig => true;
            public override bool LoadDefaultConfig(LootConfig config)
            {
                
                var npc = CreateEntity<NPCPlayer>();
                if (npc == null)
                {
                    LogWarning($"Failed to get default config for NPC {StringPool.Get(PrefabIds.FirstOrDefault())}");
                    return false;
                }
                
                // Clear config               
                LootConfigHelper.Clear(config);
                
                // Set amount
                config.Amount = new MinMax(1, DefaultSlots);
                
                // ReSharper disable once RedundantNameQualifier
                if (npc is global::HumanNPC humanNpc)
                {
                    // Add items
                    if (humanNpc.LootSpawnSlots?.Length > 0)
                    {
                        foreach (var slot in humanNpc.LootSpawnSlots)
                        {
                            LootConfigHelper.AddItems(config, slot.definition, slot.probability);
                        }
                    }
                }
                else if (npc is ScarecrowNPC scarecrowNpc)
                {
                    // Add items
                    if (scarecrowNpc.LootSpawnSlots?.Length > 0)
                    {
                        foreach (var slot in scarecrowNpc.LootSpawnSlots)
                        {
                            LootConfigHelper.AddItems(config, slot.definition, slot.probability);
                        }
                    }
                }

                // Clean up
                DestroyEntity(npc);
                config.LootType = LootType.Custom;
                config.Validate();
                return true;
            }
        }

        private class BaseLootable : ILootable, IImage
        {
            bool ILootable.IsCategory => false;

            string IImage.ImageKey { get => ImageKey; set => ImageKey = value; }

            protected virtual string DefaultImage => "https://files.facepunch.com/rust/item/cratecostume_512.png";
            public virtual LootConfigFlags ConfigFlags => LootConfigFlags.None;
            public virtual int DefaultSlots => -1;
            public virtual int MaxSlots => -1;

            public ConfigId Id => new ConfigId(Key);
            public string Key { get; }
            public string ImageUrl { get; }
            public string ImageKey { get; private set; }

            protected BaseLootable(BaseLootableData data)
            {
                Key = data.Key;
                ImageKey = data.Key;
                ImageUrl = API_URL + data.ImageUrl;
            }

            public string GetDisplayName(Lang lang, BasePlayer player)
            {
                return lang?.GetMessage(Key, player) ?? Key;
            }

            public virtual bool HasDefaultConfig => false;
            public virtual bool LoadDefaultConfig(LootConfig config)
            {
                return false;
            }

            public virtual IEnumerable<uint> GetPrefabIds()
            {
                yield break;
            }
            
            public virtual void InitDispenserMultipliers(LootConfig config) { }

            protected static void ClearConfig(LootConfig config)
            {
                foreach (var catId in config.GetCategories().Select(x => x.Id).ToArray())
                {
                    config.RemoveCategory(catId);
                }
                config.DefaultCategory.Clear();
            }

            protected T CreateEntity<T>() where T : BaseEntity
            {
                var prefabId = GetPrefabIds().FirstOrDefault();
                if (prefabId == default)
                {
                    return null;
                }

                var ent = GameManager.server.CreateEntity(StringPool.Get(prefabId), startActive: false);
                if (ent == null)
                {
                    return null;
                }

                var comp = ent as T;
                if (comp == null)
                {
                    DestroyEntity(ent);
                    return null;
                }

                return comp;
            }

            protected static void DestroyEntity(BaseEntity entity)
            {
                UnityEngine.Object.Destroy(entity.gameObject);
            }

            protected static uint[] PrefabNamesToIds(string[] prefabNames)
            {
                var prefabIds = new uint[prefabNames.Length];
                for (int i = 0; i < prefabNames.Length; i++)
                {
                    prefabIds[i] = StringPool.Get(prefabNames[i]);
                }
                return prefabIds;
            }
        }

        private readonly struct LootableCategory : ILootable
        {
            public bool HasDefaultConfig => false;
            public bool IsCategory => true;
            public string Name { get; init; }
            public string LangKey { get; init; }

            public ConfigId Id => throw new NotSupportedException();
            public string ImageKey => throw new NotSupportedException();


            public string GetDisplayName(Lang lang, BasePlayer player)
            {
                return Name ?? lang?.GetMessage(LangKey, player) ?? LangKey;
            }

            public static LootableCategory FromName(string name)
            {
                return new LootableCategory { Name = name };
            }

            public static LootableCategory FromLangKey(string langKey)
            {
                return new LootableCategory { LangKey = langKey };
            }
        }

        private interface ILootable
        {
            ConfigId Id { get; }
            string ImageKey { get; }
            bool IsCategory { get; }
            bool HasDefaultConfig { get; }
            string GetDisplayName([CanBeNull] Lang lang, BasePlayer player);

            public bool IsCustom => Id.Plugin != null;
        }

        #endregion

        #region Loot API

        [PublicAPI]
        private void ClearPresets(Plugin plugin)
        {
            manager.Api.ClearPresets(plugin);
        }

        [PublicAPI]
        private void AddCategory(Plugin plugin, string displayName)
        {
            manager.Api.RegisterCategory(plugin, displayName);
        }

        [PublicAPI]
        private void AddPreset(Plugin plugin, bool isNpc, string key, string displayName, string iconOrUrl)
        {
            manager.Api.RegisterPreset(imageHelper, plugin, key, displayName, iconOrUrl, isNpc);
        }

        [PublicAPI]
        private object AssignPreset(Plugin plugin, string keyOrName, ItemContainer container)
        {
            if (container == null)
            {
                return null;
            }
            
            var configId = new ConfigId(plugin, keyOrName);
            var config = manager.GetConfig(configId);
            if (config == null)
            {
                LogWarning($"Plugin {plugin.Name} failed to assign config to {container.entityOwner?.ShortPrefabName} - Config '{configId}' does not exist");
                return null;
            }
            if (!config.Enabled)
            {
                return null;
            }

            LogDebug($"Assigned preset {configId} to {container.entityOwner?.ShortPrefabName} enabled: {config.Enabled}");
            
            container.ClearItems();
            LootManager.PopulateContainer(container, config);

            return true;
        }

        [PublicAPI]
        private object AssignPreset(Plugin plugin, string keyOrName, BaseEntity entity)
        {
            var configId = new ConfigId(plugin, keyOrName);
            var config = manager.GetConfig(configId);
            if (config == null)
            {
                LogWarning($"Plugin {plugin.Name} failed to assign config to {entity.ShortPrefabName} - Config '{configId}' does not exist");
                return null;
            }
            if (!config.Enabled)
            {
                return null;
            }

            LogDebug($"Assigned preset {configId} to {entity.ShortPrefabName} enabled: {config.Enabled}");
            if (entity.IsFullySpawned() && entity is StorageContainer container)
            {
                var lockedCrate = entity as HackableLockedCrate;
                if (lockedCrate != null)
                {
                    lockedCrate.inventory.onItemAddedRemoved -= lockedCrate.OnItemAddedOrRemoved;
                }
                
                container.inventory.ClearItems();
                LootManager.PopulateContainer(container, config);
                
                if (lockedCrate != null)
                {
                    lockedCrate.inventory.onItemAddedRemoved += lockedCrate.OnItemAddedOrRemoved;
                }
            }
            else
            {
                manager.SetCustomConfig(entity, configId);
            }

            return true;
        }

        class Api : IConfigManager, ILootableManager
        {
            private bool pluginListLoaded;
            private readonly Dictionary<string, ApiPluginData> pluginData = new();

            private void LoadPluginList()
            {
                if (pluginListLoaded)
                {
                    return;
                }

                LogDebug("Loading plugin list");
                var info = ReadDataFile<ApiPluginList>("plugins", "_info");
                if (info != null)
                {
                    Log($"Loaded plugin list ({info.Plugins.Count} plugins)");
                    foreach (var plugin in info.Plugins)
                    {
                        GetPluginData(plugin, true);
                    }
                }

                pluginListLoaded = true;
            }

            private void SavePluginList()
            {
                if (pluginData.Count < 1)
                {
                    return;
                }

                var info = new ApiPluginList();
                foreach (var plugin in pluginData.Keys)
                {
                    info.Plugins.Add(plugin);
                }

                Log($"Saving plugin list ({info.Plugins.Count} plugins)");

                WriteDataFile(info, "plugins", "_info");
            }

            public void RegisterPreset(ImageHelper imageHelper, Plugin plugin, string key, string displayName, string iconOrUrl, bool isNpc)
            {
                var data = GetPluginData(plugin);
                var id = new ConfigId(plugin, key);

                if (!data.LootableDict.TryGetValue(key, out var preset))
                {
                    preset = new ApiLootable
                    {
                        Id = id,
                        LootConfig = LootConfig.Create(id),
                    };
                    data.LootableDict[key] = preset;
                }

                preset.LootConfig.SetFlag(LootConfigFlags.Plugin | (isNpc ? LootConfigFlags.Npc : LootConfigFlags.None), true);
                preset.DisplayName = displayName;
                preset.Category = data.CurrentCategory;
                preset.Hidden = false;
                preset.LastUpdate = ++data.CurrentUpdate;

                iconOrUrl ??= string.Empty;
                // Image is icon key
                if (imageHelper.HasImage(iconOrUrl))
                {
                    preset.ImageKey = iconOrUrl;
                }
                // Image is url
                else if (iconOrUrl.StartsWith("https") || iconOrUrl.StartsWith("http"))
                {
                    preset.ImageKey = imageHelper.AddImage(id.ToString(), iconOrUrl);
                }
                // Invalid image, use placeholder
                else
                {
                    LogWarning($"{plugin.Name} registered an invalid image for preset '{key}'");
                    preset.ImageKey = imageHelper.PlaceholderKey;
                }

                LogDebug($"{plugin.Name} registered preset '{key}' name: '{displayName}' image: '{preset.ImageKey}' cat: '{preset.Category}'");

                SavePluginData(plugin);
            }

            public void RegisterCategory(Plugin plugin, string name)
            {
                var data = GetPluginData(plugin);
                data.CurrentCategory = name;
            }

            public void ClearPresets(Plugin plugin)
            {
                var data = GetPluginData(plugin);

                foreach (var preset in data.LootableDict.Values)
                {
                    preset.Hidden = true;
                }
                data.CurrentCategory = null;

                SavePluginData(plugin);
            }

            private ApiPluginData GetPluginData(Plugin plugin) => GetPluginData(plugin.Name);
            private ApiPluginData GetPluginData(string plugin, bool isLoading = false)
            {
                if (!isLoading)
                {
                    LoadPluginList();
                }

                if (!pluginData.TryGetValue(plugin, out var data))
                {
                    data = ReadDataFile<ApiPluginData>("plugins", plugin.ToLower());
                    if (isLoading && data == null)
                    {
                        LogWarning($"Failed to get loot configurations for plugin {plugin} - File not found");
                        return null;
                    }

                    data ??= new ApiPluginData();
                    pluginData.Add(plugin, data);

                    if (!isLoading)
                    {
                        SavePluginList();
                    }
                }

                return data;
            }

            private void SavePluginData(Plugin plugin) => SavePluginData(plugin.Name);
            private void SavePluginData(string plugin)
            {
                if (pluginData.TryGetValue(plugin, out var data))
                {
                    WriteDataFile(data, "plugins", plugin.ToLower());
                }
                else
                {
                    LogWarning($"Failed to save plugin data for {plugin} - Plugin data not found");
                }
            }

            #region IConfigManager

            LootConfig IConfigManager.GetConfig(ConfigId id, bool createIfNotExists, out bool justCreated)
            {
                justCreated = false;
                var cfg = GetPluginData(id.Plugin);
                if (cfg.LootableDict.TryGetValue(id.Key, out var config))
                {
                    return config.LootConfig;
                }

                if (createIfNotExists)
                {
                    throw new NotSupportedException("Plugin api configs can not be created");
                }

                return null;
            }

            void IConfigManager.SaveConfig(LootConfig config)
            {
                SavePluginData(config.Id.Plugin);
            }

            #endregion

            #region ILootableManager

            ILootable ILootableManager.GetLootable(ConfigId id)
            {
                var data = GetPluginData(id.Plugin);

                if (data.LootableDict.TryGetValue(id.Key, out var lootable))
                {
                    return lootable;
                }

                return null;
            }

            IEnumerable<ILootable> ILootableManager.GetTab(string plugin)
            {
                plugin = plugin.Replace("#", string.Empty);
                var data = GetPluginData(plugin);
                LogDebug($"Got {data.LootableDict.Count} lootables for {plugin}");

                string currentCat = null;
                foreach (var lootable in data.LootableDict.Values.OrderBy(x => x.LastUpdate))
                {
                    if (lootable.Hidden)
                    {
                        continue;
                    }

                    if (lootable.Category != currentCat)
                    {
                        yield return LootableCategory.FromName(lootable.Category);
                        currentCat = lootable.Category;
                    }

                    LogDebug($"Last update of {lootable.Id.Key} is {lootable.LastUpdate}");
                    yield return lootable;
                }
            }

            IEnumerable<string> ILootableManager.GetTabs()
            {
                LoadPluginList();

                return pluginData.Keys.Select(x => '#' + x);
            }

            #endregion
        }

        #region Classes

        [ProtoContract]
        class ApiPluginList
        {
            [ProtoMember(1)]
            public HashSet<string> Plugins { get; init; } = new();
        }

        [ProtoContract]
        class ApiPluginData
        {
            public ulong CurrentUpdate { get; set; }
            public string CurrentCategory { get; set; }

            [ProtoMember(1)]
            public Dictionary<string, ApiLootable> LootableDict { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        }

        [ProtoContract]
        class ApiLootable : ILootable
        {
            public bool HasDefaultConfig => false;
            public bool IsCategory => false;
            public ulong LastUpdate { get; set; }

            [ProtoMember(1)]
            public ConfigId Id { get; init; }
            [ProtoMember(2)]
            public string ImageKey { get; set; }
            [ProtoMember(3)]
            public string DisplayName { get; set; }
            [ProtoMember(4)]
            public LootConfig LootConfig { get; init; }
            [ProtoMember(5)]
            public string Category { get; set; }
            [ProtoMember(6)]
            public bool Hidden { get; set; }

            public string GetDisplayName(Lang lang, BasePlayer player)
            {
                return DisplayName;
            }
        }

        #endregion

        #endregion

        #region Gui classes

        [Flags]
        private enum ItemEditorFlags
        {
            DisableSkinId = 2,
            DisableCustomName = 4,
            DisableAmount = 8,
            DisableRangedAmount = 16,
            DisableBlueprint = 32,
            //DisableExtras = 64,
            //DisableCategory = 128,
            DisableChance = 256,
            DisableCondition = 512,
        }

        #endregion

        #region GUI

        class PlayerUi
        {
            private const float BOUNDS_X_L = 0.005f;
            private const float BOUNDS_X_L_TEXT = 0.007f;
            private const float BOUNDS_X_R = 0.992f;

            public bool Destroyed { get; private set; }

            public readonly BasePlayer Player;
            private readonly Lang Lang;

            private UiRef rootView;
            private UiRef mainContainer;
            private UiRef mainContainerChild;

            #region Default

            public PlayerUi(BasePlayer player)
            {
                Player = player;
                Lang = Instance.lang;
            }

            public void Create()
            {
                if (!rootView.IsValid)
                {
                    CreateView();
                }

                Home();
            }

            public void Destroy()
            {
                if (Destroyed)
                {
                    return;
                }

                Cui.Destroy(Player, rootView);
                Destroyed = true;
            }

            private void CreateView()
            {
                using var cui = Cui.Create(Instance, Anchor.Fill, UiColor.Black, UiParentLayer.OverlayNonScaled, UiFlags.MouseAndKeyboard);

                cui.AddLabel(Anchor.Relative(0.3f, 0.7f, 0, 1), "<size=48>:(</size>\n\nYour Loottable encountered an error and needs a restart.\nPlease report any errors in the server console to the developer");

                using (cui.CreateContainer(Anchor.Relative(0, 1, 0.95f, 1f), Style.ColorGrey))
                {
                    cui.AddLabel(Anchor.Padding(0.1f, 0.1f), $"Loottable v{Instance?.Version}{(!DEBUG ? String.Empty : "-debug")} {(DEBUG ? API_URL : string.Empty)}", Style.TextHeading);
                    cui.AddButton(Anchor.Relative(0.972f, 0.99f, 0.15f, 0.75f), "\u2717", _ =>
                    {
                        Destroy();
                        if (Instance?.state?.AutoLootRefresh ?? false)
                        {
                            LootManager.RefreshLoot(false, Instance.covalence.Players.FindPlayerById(Player.UserIDString));
                        }
                    }, Style.ButtonRed);
                }

                using (cui.CreateContainer(Anchor.Relative(0, 1, 0, 0.95f), new UiColor(0.08f, 0.08f, 0.08f)))
                {
                    mainContainer = cui.CurrentRef;
                }

                rootView = cui.Send(Player);
            }

            #endregion

            #region Home

            private string home_currentTab;

            private void Home()
            {
                using var cui = Cui.Create(Instance, mainContainer);

                using (cui.CreateContainer(Anchor.Relative(0, 0.15f, 0, 1), Style.ColorGrey05))
                {
                    var move = MovingAnchorProperties.Create(0.03f, 0.99f, 0.94f, 0.05f, 0, 0.01f);

                    cui.AddLabel(Anchor.MoveColumn(ref move), TU("default"), Style.TextHeading with { TextAnchor = TextAnchor.LowerCenter });
                    foreach (var tab in Instance.manager.GetTabsVanilla())
                    {
                        home_currentTab ??= tab;
                        cui.AddButton(Anchor.MoveColumn(ref move), T(tab), _ => SwitchTab(tab), home_currentTab == tab ? Style.ButtonBlue : Style.ButtonGrey);
                    }

                    cui.AddLabel(Anchor.MoveColumn(ref move), TU("plugins"), Style.TextHeading with { TextAnchor = TextAnchor.LowerCenter });
                    foreach (var tab in Instance.manager.GetTabsPlugin())
                    {
                        cui.AddButton(Anchor.MoveColumn(ref move), tab.Substring(1).WithSpaceBeforeUppercase(), _ => SwitchTab(tab), home_currentTab == tab ? Style.ButtonBlue : Style.ButtonGrey);
                    }

                    cui.AddButton((0.03f, 0.97f, 0.12f, 0.16f), T("stack_size_control"), _ => StackSize(), Style.ButtonGrey);
                    
                    cui.AddButton((0.03f, 0.97f, 0.07f, 0.11f), T("custom_item_manager"), _ => CustomItems(), Style.ButtonGrey);
                    
                    cui.AddButton((0.03f, 0.97f, 0.02f, 0.06f), T("settings"), _ => Settings(), Style.ButtonGrey);
                    
                    cui.AddButton((0.03f, 0.97f, 0.17f, 0.21f), T("configuration"), _ => ConfigPage(), Style.ButtonGrey);
                }

                Home_DrawTab(cui, home_currentTab);

                cui.Send(Player, ref mainContainerChild);

                void SwitchTab(string tab)
                {
                    home_currentTab = tab;
                    Home();
                }
            }

            private void Home_DrawTab(Cui cui, string name)
            {
                var move = MovingAnchorProperties.Create(0.15f, 0.98f, 0.168f, 0.1f, 0.006f, 0.01f);
                foreach (var lootable in Instance.manager.GetTabContents(name))
                {
                    if (!move.CheckBoundsY())
                    {
                        LogWarning("UI container moved out of bounds");
                        break;
                    }

                    if (lootable.IsCategory)
                    {
                        move.NextRow();
                        cui.AddLabel(move.Anchor().WithOffset(yMin: 0.06f), lootable.GetDisplayName(Lang, Player), Style.TextHeading with { TextAnchor = TextAnchor.LowerLeft });
                        move.offsetY += 0.065f;
                        move.NextRow(true);
                        continue;
                    }

                    using (cui.CreateContainer(Anchor.MoveRow(ref move, 5), Style.ColorLightGrey05))
                    {
                        var png = Instance.imageHelper.GetPng(lootable.ImageKey);
                        cui.AddImage(Anchor.AbsoluteCentered(0.15f, 0.5f, 60, 60), png);
                        cui.AddLabel(Anchor.Relative(0.31f, 0.98f, 0.35f, 0.95f), lootable.GetDisplayName(Lang, Player), Style.TextNormal with { TextAnchor = TextAnchor.MiddleLeft });

                        var cfg = Instance.manager.GetConfig(lootable.Id);
                        var (color, text) = GetConfigInfo(cfg, lootable.IsCustom);
                        using (cui.CreateContainer(Anchor.Relative(0.31f, 0.8f, 0.05f, 0.3f), color))
                        {
                            cui.AddLabel(Anchor.Fill, T(text), Style.TextNormal with { TextAnchor = TextAnchor.MiddleCenter });
                        }

                        cui.AddButton(Anchor.Fill, string.Empty, _ => SelectLootable(lootable.Id), Style.ButtonTransparent);
                    }
                }

                static (UiColor color, string text) GetConfigInfo(LootConfig config, bool isPlugin)
                {
                    // Config might be null when not created
                    if (config == null || !config.Enabled)
                    {
                        return (Style.ColorLightGrey, "vanilla");
                    }

                    return config.LootType switch
                    {
                        LootType.Addition => (Style.ColorBlue, "vanilla_a"),
                        LootType.Blacklist => (Style.ColorRed, "blacklist"),
                        LootType.Custom => (Style.ColorGreen, "custom_loot"),
                        _ => (Style.ColorLightGrey, "invalid")
                    };
                }

                void SelectLootable(ConfigId id)
                {
                    editor_config = Instance.manager.GetOrCreateConfig(id);
                    Editor();
                }
            }

            #endregion

            #region Config Editor

            private LootConfig editor_clipboardConfig;

            private LootConfig editor_config;
            private int editor_itemPage;
            private ItemOrder editor_ordering;

            private List<LootItemCategory> editor_selection;

            private void Editor()
            {
                using var cui = Cui.Create(Instance, mainContainer);
                var lootable = Instance.manager.GetLootable(editor_config.Id);
                var pluginTitle = lootable.IsCustom ? lootable.Id.Plugin : T("default");
                var isCustomLoot = editor_config.LootType == LootType.Custom;

                cui.AddButton((0.9f, BOUNDS_X_R, 0.95f, 0.99f), TU("btn_save"), Exit, Style.ButtonGreen);
                cui.AddLabel((BOUNDS_X_L_TEXT, 0.4f, 0.95f, 0.99f), T("editor_head", lootable.GetDisplayName(Lang, Player), pluginTitle), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });

                using (cui.CreateContainer(Anchor.Relative(BOUNDS_X_L, 0.8f, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    if (editor_config.HasFlag(LootConfigFlags.Dispenser))
                    {
                        Assert.NotNull(editor_config.DispenserMultipliers, $"DispenserMultipliers is null for dispenser config {editor_config.Id}");
                        
                        cui.AddLabel(Anchor.Relative(0.01f, 1, 0.940f, 0.980f), T("gather_multipliers"), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });
                        
                        var move = MovingAnchorProperties.Create(0.003f, 0.925f, 0.12f, 0.07f, 0.007f, 0.015f);
                        foreach (var multiplier in editor_config.DispenserMultipliers!)
                        {
                            using (cui.CreateContainer(Anchor.MoveRow(ref move, 7), Style.ColorGrey))
                            {
                                cui.AddItemImage(Anchor.AbsoluteCentered(0.2f, 0.5f, 30, 30), multiplier.Key);

                                var isOverride = !Mathf.Approximately(multiplier.Value, 1);
                                using (cui.CreateContainer(Anchor.Relative(0.2f, 0.9f, 0.15f, 0.85f, 15 + 10), isOverride ? Style.ColorYellow05 : Style.ColorLightGrey05))
                                {
                                    cui.AddInputField(Anchor.Fill with { OffsetXMin = 5 }, multiplier.Value.ToString(CultureInfo.InvariantCulture), (_, s) =>
                                    {
                                        if (Single.TryParse(s, out var newMpl))
                                        {
                                            editor_config.DispenserMultipliers[multiplier.Key] = newMpl;
                                        }
                                        else
                                        {
                                            editor_config.DispenserMultipliers[multiplier.Key] = 1;
                                        }
                                        Editor();
                                    }, Style.TextNormal);
                                }
                            }
                        }
                        
                        cui.AddLabel(Anchor.Relative(0.01f, 1, 0.79f, 0.83f), T("gather_additions"), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });
                        
                        using (cui.CreateContainer(Anchor.Fill with { OffsetYMax = -130, OffsetYMin = -130 }, UiColor.Transparent))
                        {
                            Editor_DrawItemPanel(cui, true);
                        }
                    }
                    else
                    {
                        Editor_DrawItemPanel(cui);
                    }
                }

                using (cui.CreateContainer(Anchor.Relative(0.805f, BOUNDS_X_R, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    cui.AddButton(Anchor.Relative(0.05f, 0.95f, 0.94f, 0.98f), TU("btn_additem"), _ => Editor__EditItem(default), Style.ButtonBlue);

                    var canLoadDefaultConfig = isCustomLoot && lootable.HasDefaultConfig;
                    cui.AddButton(Anchor.Relative(0.52f, 0.95f, 0.885f, 0.925f), TU("btn_ltdefault"), _ =>
                    {
                        if (canLoadDefaultConfig)
                        {
                            CreateConfirmDialog(T("ltdefault_title"), T("ltdefault_desc"), () =>
                            {
                                Instance.manager.LoadDefaultConfig(editor_config);
                                Editor();
                            });
                        }
                        else
                        {
                            Editor();
                        }
                    }, canLoadDefaultConfig ? Style.ButtonBlue : Style.ButtonDisabled);


                    cui.AddButton((0.05f, 0.48f, 0.885f, 0.925f), TU("btn_copy"), _ =>
                    {
                        editor_clipboardConfig = editor_config.Copy();
                        Editor();
                    }, Style.ButtonBlue);

                    var pasteName = editor_clipboardConfig == null ? String.Empty : Instance.manager.GetLootable(editor_clipboardConfig.Id).GetDisplayName(Lang, Player);
                    cui.AddButton((0.05f, 0.95f, 0.83f, 0.87f), TU("btn_paste"), _ =>
                    {
                        if (editor_clipboardConfig != null)
                        {
                            CreateConfirmDialog(T("paste_title", pasteName), T("paste_desc"), () =>
                            {
                                editor_clipboardConfig.CopyTo(ref editor_config);
                                Editor();
                            });
                        }
                        else
                        {
                            Editor();
                        }
                    }, editor_clipboardConfig != null ? Style.ButtonBlue : Style.ButtonDisabled);


                    cui.AddLabel(Anchor.Relative(0, 1, 0.79f, 0.84f), T("editor_settings"), Style.TextHeading with { TextAnchor = TextAnchor.LowerCenter });

                    CreateInputButton(cui, Anchor.Relative(0.05f, 0.48f, 0.71f, 0.79f), T("enabled"), editor_config.Enabled, b =>
                    {
                        var successful = editor_config.SetEnabled(b, out var error);
                        Editor();
                        if (!successful)
                        {
                            CreateConfirmDialog(T("enable_failed"), T(error), null, false);
                        }
                    });

                    var canChangeLootType = editor_config.HasFlag(LootConfigFlags.CanChangeLootType);
                    CreateInputButton(cui, Anchor.Relative(0.52f, 0.95f, 0.71f, 0.79f), T("editor_loot_type"), editor_config.LootType.ToString().ToUpper(), canChangeLootType ? Style.ButtonBlue : Style.ButtonDisabled, () =>
                    {
                        if (canChangeLootType)
                        {
                            editor_config.CycleLootType();
                            Editor__EndSelect(false);
                        }
                        Editor();
                    });


                    if (editor_config.LootType == LootType.Custom && !editor_config.HasFlag(LootConfigFlags.Quarry) && !editor_config.HasFlag(LootConfigFlags.Dispenser))
                    {
                        CreateInputField(cui, Anchor.Relative(0.05f, 0.95f, 0.63f, 0.71f), T("editor_total_amount"), editor_config.Amount.ToString(), s =>
                        {
                            if (MinMax.TryParse(s, out var result))
                            {
                                editor_config.Amount = result;
                            }
                            Editor();
                        });
                    }

                    if (editor_config.HasFlag(LootConfigFlags.Npc))
                    {
                        CreateInputButton(cui, Anchor.Relative(0.05f, 0.48f, 0.55f, 0.63f), T("editor_remove_corpse"), editor_config.NpcRemoveCorpse, b =>
                        {
                            editor_config.NpcRemoveCorpse = b;
                            Editor();
                        });
                    }


                    if (editor_config.HasFlag(LootConfigFlags.Dispenser))
                    {
                        using (cui.CreateContainer((0.05f, 0.95f, 0.35f, 0.54f), Style.ColorGrey))
                        {
                            cui.AddLabel((0, 1, 0.8f, 1), T("tip"), Style.TextHeading with { TextAnchor = TextAnchor.LowerCenter });
                            cui.AddLabel((0.05f, 0.95f, 0.1f, 0.75f), T("dispenser_info_2"), Style.TextNormal with { TextAnchor = TextAnchor.UpperLeft });
                        }

                    }
                    else if (isCustomLoot)
                    {
                        Editor_DrawCategories(cui);
                    }
                }

                cui.Send(Player, ref mainContainerChild);

                void Exit(BasePlayer _)
                {
                    Editor__EndSelect(false);
                    Instance.manager.SaveConfig(editor_config);
                    editor_itemPage = 0;
                    editor_config = null;
                    Home();
                }
            }

            private void Editor_DrawItemPanel(Cui cui, bool smallPanel = false)
            {
                var isSelecting = editor_selection != null;

                if (!isSelecting)
                {
                    cui.AddButton(Anchor.Relative(0.01f, 0.1f, 0.940f, 0.980f), TU("btn_select"), _ =>
                    {
                        editor_selection = Pool.Get<List<LootItemCategory>>();
                        Editor();
                    }, Style.ButtonBlue);
                }
                else
                {
                    var anySelected = editor_selection != null && editor_selection.Count > 0;

                    cui.AddButton(Anchor.Relative(0.01f, 0.1f, 0.940f, 0.980f), TU("btn_done"), _ => Editor__EndSelect(), Style.ButtonLightGrey);

                    cui.AddButton(Anchor.Relative(0.11f, 0.16f, 0.940f, 0.980f), TU("btn_all"), _ =>
                    {
                        editor_selection.Clear();
                        editor_selection.AddRange(editor_config.GetAllItems());
                        Editor();
                    }, Style.ButtonBlue);

                    if (editor_config.LootType == LootType.Custom && editor_config.CategoryCount > 0)
                    {
                        cui.AddButton(Anchor.Relative(0.2f, 0.3f, 0.940f, 0.980f), TU("btn_moveto"), _ =>
                        {
                            if (anySelected)
                            {
                                Editor_DrawCategoryOverlay();
                            }
                            else
                            {
                                Editor();
                            }
                        }, anySelected ? Style.ButtonBlue : Style.ButtonDisabled);
                    }

                    if (editor_config.LootType != LootType.Blacklist)
                    {
                        cui.AddButton(Anchor.Relative(0.31f, 0.41f, 0.940f, 0.980f), TU("btn_multiply"), _ =>
                        {
                            if (anySelected)
                            {
                                Editor_DrawMultiplierOverlay();
                            }
                            else
                            {
                                Editor();
                            }
                        }, anySelected ? Style.ButtonBlue : Style.ButtonDisabled);
                    }

                    cui.AddButton(Anchor.Relative(0.42f, 0.52f, 0.940f, 0.980f), TU("btn_delete"), _ =>
                    {
                        CreateConfirmDialog(T("delete_title"), T("delete_desc"), () =>
                        {
                            foreach (var item in editor_selection)
                            {
                                editor_config.RemoveItem(item);
                            }
                            editor_selection.Clear();
                            Editor();
                        });
                    }, anySelected ? Style.ButtonRed : Style.ButtonDisabled);
                }

                if (editor_config.LootType != LootType.Blacklist)
                {
                    cui.AddLabel(Anchor.Relative(0.8f, 0.87f, 0.940f, 0.980f), T("order_label"), Style.TextNormal with { TextAnchor = TextAnchor.MiddleRight });
                    cui.AddButton(Anchor.Relative(0.88f, 0.99f, 0.940f, 0.980f), TU(editor_ordering.ToString()), _ =>
                    {
                        editor_ordering = editor_ordering.Next();
                        Editor();
                    }, Style.ButtonLightGrey);
                }

                const int row_size = 14;
                int col_size = smallPanel ? 5 : 7;

                var move = MovingAnchorProperties.Create(0.003f, 0.92f, 0.070f, 0.125f, 0.007f, 0.015f);
                var itemList = Pool.Get<List<LootItemCategory>>();
                itemList.AddRange(editor_config.GetAllItems());
                foreach (var item in OrderItemList(itemList, editor_ordering).Skip(row_size * col_size * editor_itemPage).Take(row_size * col_size))
                {
                    UiColor? bgOverride = (isSelecting && editor_selection.Contains(item)) ? Style.ColorBlue : null;
                    void OnItemClick(BasePlayer _)
                    {
                        if (!isSelecting)
                        {
                            Editor__EditItem(item);
                        }
                        else
                        {
                            if (editor_selection.Contains(item))
                            {
                                editor_selection.Remove(item);
                            }
                            else
                            {
                                editor_selection.Add(item);
                            }
                            Editor();
                        }
                    }

                    if (editor_config.LootType == LootType.Blacklist)
                    {
                        CreateItemPanel(cui, Anchor.MoveRow(ref move, row_size), item.ItemId, 0ul, String.Empty, OnItemClick, backgroundOverride: bgOverride);
                    }
                    else
                    {
                        var hideChance = editor_config.HasFlag(LootConfigFlags.Quarry);
                        CreateItemPanel(cui, Anchor.MoveRow(ref move, row_size), item.Category, item.Item, OnItemClick, bgOverride, hideChance);
                    }
                }

                float y = smallPanel ? 0.23f : 0.01f;
                var maxPage = Mathf.CeilToInt((float)itemList.Count / (row_size * col_size));
                cui.AddLabel(Anchor.Relative(0, 1, y, y + 0.03f), T("pagecount", editor_itemPage + 1, maxPage));
                cui.AddButton(Anchor.Relative(0.01f, 0.1f, y, y + 0.03f), "<--", _ =>
                {
                    if (editor_itemPage > 0)
                    {
                        editor_itemPage--;
                        Editor();
                    }
                }, editor_itemPage > 0 ? Style.ButtonBlue : Style.ButtonDisabled);
                cui.AddButton(Anchor.Relative(0.9f, 0.99f, y, y + 0.03f), "-->", _ =>
                {
                    if (editor_itemPage + 1 < maxPage)
                    {
                        editor_itemPage++;
                        Editor();
                    }
                }, editor_itemPage + 1 < maxPage ? Style.ButtonBlue : Style.ButtonDisabled);

                Pool.FreeUnmanaged(ref itemList);

                static IEnumerable<LootItemCategory> OrderItemList(IEnumerable<LootItemCategory> items, ItemOrder order)
                {
                    return order switch
                    {
                        ItemOrder.order_category => items.OrderByDescending(x => x.Category < 0 ? x.Item.Chance : 1).ThenBy(x => x.Category),
                        ItemOrder.order_category_desc => items.OrderByDescending(x => x.Category < 0 ? x.Item.Chance : 1).ThenByDescending(x => x.Category),
                        ItemOrder.order_chance => items.OrderBy(x => x.Item.Chance),
                        ItemOrder.order_chance_desc => items.OrderByDescending(x => x.Item.Chance),
                        _ => items
                    };
                }
            }

            private void Editor__EndSelect(bool redraw = true)
            {
                if (editor_selection == null)
                {
                    return;
                }

                Pool.FreeUnmanaged(ref editor_selection);

                if (redraw)
                {
                    Editor();
                }
            }

            private void Editor__EditItem(LootItemCategory item)
            {
                if (editor_config.HasFlag(LootConfigFlags.Quarry))
                {
                    itemEditor_flags = ItemEditorFlags.DisableRangedAmount | ItemEditorFlags.DisableBlueprint | ItemEditorFlags.DisableChance;
                }
                else if (editor_config.HasFlag(LootConfigFlags.Dispenser))
                {
                    itemEditor_flags = ItemEditorFlags.DisableBlueprint | ItemEditorFlags.DisableSkinId | ItemEditorFlags.DisableCustomName;
                }

                itemEditor_original = item;
                itemEditor_item = item == default ? editor_config.LootType == LootType.Blacklist ? new LootItemCategory(-932201673) : new LootItemCategory(new LootItem
                {
                    ItemId = -932201673,
                    Amount = itemEditor_flags.HasFlag(ItemEditorFlags.DisableRangedAmount) ? new MinMax(10) : new MinMax(10, 20),
                    Chance = 1,
                    Condition = new MinMaxFloat(1)
                }, -1) : item.Copy();

                ItemEditor();
            }

            private void Editor_DrawCategories(Cui cui)
            {
                cui.AddLabel(Anchor.Relative(0, 1, 0.50f, 0.54f), T("categories"), Style.TextHeading with { TextAnchor = TextAnchor.LowerCenter });

                var move = MovingAnchorProperties.Create(0.05f, 0.49f, 0.9f, 0.08f, 0, 0.01f);
                var catIdx = 0;
                foreach (var cat in editor_config.GetCategories())
                {
                    using (cui.CreateContainer(Anchor.MoveColumn(ref move), Style.ColorLightGrey05))
                    {
                        cui.AddBox(Anchor.Relative(0, 0.05f, 0, 0.98f), Style.GetCategoryColor(catIdx++));

                        CreateInputField(cui, Anchor.Relative(0.1f, 0.5f, 0.1f, 0.95f), T("chance_percent"), (cat.Chance * 100).ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var result))
                            {
                                cat.Chance = Mathf.Clamp01(result / 100f);
                            }
                            Editor();
                        }, labelStyle: Style.TextNormalBold);

                        CreateInputField(cui, Anchor.Relative(0.55f, 0.95f, 0.1f, 0.95f), T("amount"), cat.Amount.ToString(), s =>
                        {
                            if (MinMax.TryParse(s, out var result) && result.Min >= 0)
                            {
                                cat.Amount = result;
                            }
                            Editor();
                        }, labelStyle: Style.TextNormalBold);

                        cui.AddButton(Anchor.Relative(0.9f, 0.95f, 0.65f, 0.9f), "\u2717", _ =>
                        {
                            editor_config.RemoveCategory(cat.Id);
                            Editor();
                        }, Style.ButtonRed with { TextStyle = Style.TextItem });
                    }
                }

                if (editor_config.CategoryCount < 6 && !editor_config.HasFlag(LootConfigFlags.DisableCategories))
                {
                    cui.AddButton(Anchor.Relative(0.25f, 0.75f, move.YMin() + 0.02f, move.YMax() - 0.01f), TU("btn_new_cat"), _ =>
                    {
                        editor_config.AddCategory();
                        Editor();
                    }, Style.ButtonGreen);
                }
            }

            #region Overlays

            private float editor_mulitplierOverlay_mpl;
            private UiRef editor_mulitplierOverlay_panel;
            private void Editor_DrawMultiplierOverlay()
            {
                if (editor_mulitplierOverlay_mpl <= 0)
                {
                    editor_mulitplierOverlay_mpl = 1;
                }

                using var cui = Cui.Create(Instance, rootView);
                cui.AddButton(Anchor.Fill, string.Empty, _ => Destroy(), Style.ButtonTransparentDark);
                using (cui.CreateContainer(Anchor.Relative(0.4f, 0.6f, 0.5f, 0.7f), Style.ColorGrey))
                {
                    cui.AddLabel((0, 1, 0.8f, 0.95f), T("multiply_title"), Style.TextHeading);

                    using (cui.CreateContainer((0.05f, 0.95f, 0.5f, 0.7f), Style.ColorLightGrey))
                    {
                        cui.AddInputField(Anchor.Fill, editor_mulitplierOverlay_mpl.ToString(CultureInfo.InvariantCulture), (_, s) =>
                        {
                            if (Single.TryParse(s, out var result))
                            {
                                editor_mulitplierOverlay_mpl = Mathf.Abs(result);
                            }
                            Editor_DrawMultiplierOverlay();
                        }, Style.TextNormal with { TextAnchor = TextAnchor.MiddleCenter });
                    }

                    cui.AddButton((0.05f, 0.95f, 0.1f, 0.3f), TU("btn_multiply"), _ =>
                    {
                        foreach (var item in editor_selection)
                        {
                            item.Item.Amount = item.Item.Amount.Multiply(editor_mulitplierOverlay_mpl);
                        }
                        Destroy();
                    }, Style.ButtonBlue);
                }

                cui.Send(Player, ref editor_mulitplierOverlay_panel);

                // ReSharper disable once LocalFunctionHidesMethod
                void Destroy()
                {
                    // ReSharper disable once AccessToDisposedClosure
                    Cui.Destroy(Player, cui.RootRef);
                    Editor();
                }
            }

            private void Editor_DrawCategoryOverlay()
            {
                using var cui = Cui.Create(Instance, rootView);
                cui.AddButton(Anchor.Fill, string.Empty, _ => Destroy(), Style.ButtonTransparentDark);
                using (cui.CreateContainer(Anchor.Relative(0.4f, 0.6f, 0.4f, 0.7f), Style.ColorGrey))
                {
                    cui.AddLabel((0, 1, 0.85f, 1), T("category_select"), Style.TextHeading);

                    var move = MovingAnchorProperties.Create(0.025f, 0.8f, 0.45f, 0.15f, 0.05f, 0.05f);
                    cui.AddButton(Anchor.MoveX(ref move, 0.85f, true), T("category_default"), _ => ChangeCategory(editor_config.DefaultCategoryId), Style.ColorLightGrey, Style.TextButton);
                    move.NextRow(true);

                    int catIdx = 0;
                    foreach (var cat in editor_config.GetCategories())
                    {
                        cui.AddButton(Anchor.MoveRow(ref move, 2), T("category_id", catIdx + 1), _ => ChangeCategory(cat.Id), Style.GetCategoryColor(catIdx), Style.TextButton);
                        catIdx++;
                    }
                }

                cui.Send(Player);

                void ChangeCategory(int categoryId)
                {
                    for (int i = 0; i < editor_selection.Count; i++)
                    {
                        editor_selection[i] = editor_config.SwitchCategory(editor_selection[i], categoryId);
                    }

                    Destroy();
                }

                // ReSharper disable once LocalFunctionHidesMethod
                void Destroy()
                {
                    // ReSharper disable once AccessToDisposedClosure
                    Cui.Destroy(Player, cui.RootRef);
                    Editor();
                }
            }

            #endregion

            #endregion

            #region Item Editor

            LootItemCategory itemEditor_original;
            LootItemCategory itemEditor_item;
            ItemEditorFlags itemEditor_flags;

            const int itemEditor_itemsPerPage = 70;
            int itemEditor_itemPage;
            string itemEditor_itemTab;
            string itemEditor_search = String.Empty;

            private void ItemEditor()
            {
                using var cui = Cui.Create(Instance, mainContainer);
                cui.AddButton(Anchor.Relative(0.8f, 0.895f, 0.95f, 0.99f), TU("btn_save"), _ => Back(true), Style.ButtonGreen);
                cui.AddButton(Anchor.Relative(0.9f, BOUNDS_X_R, 0.95f, 0.99f), TU("btn_cancel"), _ => Back(false), Style.ButtonRed);
                cui.AddLabel(Anchor.Relative(BOUNDS_X_L_TEXT, 0.4f, 0.95f, 0.99f), T("ieditor_head", Instance.GetItemNameTranslated(itemEditor_item.Item?.ItemId ?? itemEditor_item.ItemId, Lang.GetLanguage(Player)), editor_config.Id), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });

                using (cui.CreateContainer(Anchor.Relative(BOUNDS_X_L, 0.4975f, 0.01f, 0.94f), Style.ColorGrey05))
                {
                    var move = MovingAnchorProperties.Create(0, 0.99f, 0.28f, 0.09f, 0.02f, 0.01f);

                    CreateInputField(cui, Anchor.MoveRow(ref move), T("shortname"), itemEditor_item.ItemDefinition.shortname, s =>
                    {
                        var def = ItemManager.FindItemDefinition(s);
                        if (def != null)
                        {
                            UpdateItemId(def.itemid);
                        }
                        ItemEditor();
                    });

                    if (itemEditor_item.Item != null && IsNotEnabled(ItemEditorFlags.DisableBlueprint))
                    {
                        CreateInputButton(cui, Anchor.MoveX(ref move, 0.12f, true), T("blueprint"), itemEditor_item.Item.IsBlueprint, b =>
                        {
                            itemEditor_item.Item.IsBlueprint = b;
                            ItemEditor();
                        });
                    }

                    if (itemEditor_item.Item != null && IsNotEnabled(ItemEditorFlags.DisableAmount))
                    {
                        CreateInputField(cui, Anchor.MoveRow(ref move), T("amount"), itemEditor_item.Item.Amount.ToString(), s =>
                        {
                            if (MinMax.TryParse(s, out var result) && result.Min > 0 && (IsNotEnabled(ItemEditorFlags.DisableRangedAmount) || result.IsFixed))
                            {
                                itemEditor_item.Item.Amount = result;
                            }
                            ItemEditor();
                        });
                    }

                    if (itemEditor_item.Item != null && IsNotEnabled(ItemEditorFlags.DisableCondition) && itemEditor_item.ItemDefinition.condition.enabled)
                    {
                        CreateInputField(cui, Anchor.MoveRow(ref move), T("condition_percent"), (itemEditor_item.Item.Condition * 100).ToString("N0"), s =>
                        {
                            if (MinMaxFloat.TryParse(s, out var result))
                            {
                                itemEditor_item.Item.Condition = MinMaxFloat.Clamp01(result * 0.01f);
                            }
                            ItemEditor();
                        });
                    }

                    move.NextRow();

                    if (itemEditor_item.Item != null && IsNotEnabled(ItemEditorFlags.DisableCustomName))
                    {
                        CreateInputField(cui, Anchor.MoveRow(ref move), T("customname"), itemEditor_item.Item.CustomName, s =>
                        {
                            itemEditor_item.Item.CustomName = s;
                            ItemEditor();
                        }, InputFieldFlags.None, "CLEAR", () =>
                        {
                            itemEditor_item.Item.CustomName = null;
                            ItemEditor();
                        });
                    }

                    if (itemEditor_item.Item != null && IsNotEnabled(ItemEditorFlags.DisableSkinId))
                    {
                        CreateInputField(cui, Anchor.MoveRow(ref move), T("skinid"), itemEditor_item.Item.SkinId.ToString(), s =>
                        {
                            if (UInt64.TryParse(s, out var result))
                            {
                                itemEditor_item.Item.SkinId = result;
                            }
                            ItemEditor();
                        });
                    }

                    if (itemEditor_item.Item != null && IsNotEnabled(ItemEditorFlags.DisableChance))
                    {
                        CreateInputField(cui, Anchor.MoveRow(ref move), T("chance_percent"), (itemEditor_item.Item.Chance * 100).ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var result))
                            {
                                itemEditor_item.Item.Chance = Mathf.Clamp01(result / 100f);
                            }
                            ItemEditor();
                        });
                    }

                    cui.AddLabel(Anchor.Relative(0.45f, 0.55f, 0.75f, 0.8f), T("preview"));
                    CreateItemPanel(cui, Anchor.Relative(0.45f, 0.55f, 0.63f, 0.75f), itemEditor_item.Item?.ItemId ?? itemEditor_item.ItemId, itemEditor_item.Item?.SkinId ?? 0, string.Empty, _ => { });
                }

                using (cui.CreateContainer(Anchor.Relative(0.5025f, BOUNDS_X_R, 0.01f, 0.94f), Style.ColorGrey05))
                {
                    var textStyle = new LabelStyle
                    {
                        Font = UiFont.robotoBold,
                        FontSize = 12,
                        TextAnchor = TextAnchor.MiddleCenter,
                        TextColor = UiColor.White
                    };

                    cui.AddLabel(Anchor.Relative(0.005f, 0.99f, 0.96f, 1f), T("item_list"), Style.TextHeading);

                    using (cui.CreateContainer((0.6f, 0.99f, 0.965f, 0.995f), Style.ColorGrey05))
                    {
                        if (String.IsNullOrEmpty(itemEditor_search))
                        {
                            cui.AddLabel(Anchor.Fill  with { OffsetXMin = 5 }, T("search"), Style.TextNormal with { TextColor = Style.ColorLightGrey });
                        }
                        
                        cui.AddInputField(Anchor.Fill with { OffsetXMin = 5 }, itemEditor_search, (_, s) =>
                        {
                            itemEditor_search = s;
                            itemEditor_itemTab = CustomItemManager.SEARCH_TAB;
                            Instance.manager.CustomItems.Search(s);
                            ItemEditor();
                        }, Style.TextNormal);
                    }
                    
                    var move = MovingAnchorProperties.Create(0.01f, 0.96f, 0.122f, 0.04f, 0, 0);
                    foreach (var tab in Instance.manager.CustomItems.GetTabs())
                    {
                        itemEditor_itemTab ??= tab;

                        cui.AddButton(Anchor.MoveRow(ref move, 8), tab.ToUpper(), _ =>
                        {
                            itemEditor_itemTab = tab;
                            itemEditor_search = String.Empty;
                            itemEditor_itemPage = 0;
                            ItemEditor();
                        }, tab == itemEditor_itemTab ? new ButtonStyle { ButtonColor = Style.ColorBlue, TextStyle = textStyle } : new ButtonStyle { ButtonColor = Style.ColorGrey, TextStyle = textStyle });
                    }

                    move = MovingAnchorProperties.Create(0, 0.87f, 0.099f, 0.119f, 0.01f, 0.01f);
                    int itemCount;
                    var lastPlugin = String.Empty;
                    foreach (var item in Instance.manager.CustomItems.GetItemsForTab(itemEditor_itemTab, out itemCount).Skip(itemEditor_itemPage * itemEditor_itemsPerPage).Take(itemEditor_itemsPerPage))
                    {
                        if (itemEditor_itemTab == CustomItemManager.CUSTOM_TAB && lastPlugin != item.GetPlugin())
                        {
                            lastPlugin = item.GetPlugin();
                            move.NextRow();
                            if (lastPlugin != Instance.Name)
                            {
                                cui.AddLabel(Anchor.MoveOffsetY(ref move, 0.08f, true) with { XMax = 0.8f }, lastPlugin.WithSpaceBeforeUppercase(), Style.TextNormalBold with { TextAnchor = TextAnchor.LowerLeft });
                            }
                        }

                        CreateItemPanel(cui, Anchor.MoveRow(ref move, 10), item.ItemId, item.SkinId, item.GetDisplayName(Lang, Player), _ =>
                        {
                            UpdateItemId(item.ItemId);
                            if (itemEditor_item.Item != null)
                            {
                                itemEditor_item.Item.SkinId = item.SkinId;
                                itemEditor_item.Item.CustomName = item.CustomName;
                            }
                            Instance.manager.CustomItems.SetRecentItem(item, itemEditor_itemsPerPage);
                            ItemEditor();
                        });
                    }

                    var maxPage = Mathf.CeilToInt((float)itemCount / itemEditor_itemsPerPage);
                    cui.AddLabel(Anchor.Relative(0, 1, 0.01f, 0.035f), T("pagecount", itemEditor_itemPage + 1, maxPage));
                    cui.AddButton(Anchor.Relative(0.01f, 0.1f, 0.01f, 0.035f), "<--", _ =>
                    {
                        if (itemEditor_itemPage > 0)
                        {
                            itemEditor_itemPage--;
                            ItemEditor();
                        }
                    }, itemEditor_itemPage > 0 ? Style.ButtonBlue : Style.ButtonDisabled);
                    cui.AddButton(Anchor.Relative(0.9f, 0.99f, 0.01f, 0.035f), "-->", _ =>
                    {
                        if (itemEditor_itemPage + 1 < maxPage)
                        {
                            itemEditor_itemPage++;
                            ItemEditor();
                        }
                    }, itemEditor_itemPage + 1 < maxPage ? Style.ButtonBlue : Style.ButtonDisabled);
                }

                cui.Send(Player, ref mainContainerChild);

                void UpdateItemId(int itemId)
                {
                    if (itemEditor_item.Item != null)
                    {
                        itemEditor_item.Item.ItemId = itemId;
                    }
                    else
                    {
                        itemEditor_item = itemEditor_item with { ItemId = itemId };
                    }
                }

                void Back(bool save)
                {
                    if (save)
                    {
                        editor_config.ReplaceItem(itemEditor_original, itemEditor_item);
                    }

                    itemEditor_flags = default;
                    itemEditor_item = default;
                    itemEditor_original = default;
                    Editor();
                }

                bool IsNotEnabled(ItemEditorFlags flag)
                {
                    return !itemEditor_flags.HasFlag(flag);
                }
            }

            #endregion

            #region Stack Size Editor

            private ItemCategory stackSize_tab;
            private int stackSize_tabPage;
            private StackSizeDataEntry stackSize_entry;

            private readonly Lazy<Dictionary<ItemCategory, ItemDefinition[]>> stackSize_itemCategories = new(() =>
            {
                return ItemManager.itemList
                    .Where(x => !StackSizeController.IsHidden(x))
                    .GroupBy(x => x.category)
                    .ToDictionary(x => x.Key, x => x.ToArray());
            });

            private void StackSize()
            {
                stackSize_entry = Instance.stackSizeController.GetDefaultEntry();
                StackSizeEditor();
                return;
                
                using var cui = Cui.Create(Instance, mainContainer);

                cui.AddButton(Anchor.Relative(0.9f, BOUNDS_X_R, 0.95f, 0.99f), TU("btn_done"), Exit, Style.ButtonBlue);
                //cui.AddLabel(Anchor.Relative(BOUNDS_X_L_TEXT, 0.4f, 0.95f, 0.99f), T("stack_size_control"), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });

                CreateInputButton(cui, (BOUNDS_X_L, 0.12f, 0.92f, 1), T("seditor_enabled"), Instance.stackSizeController.Enabled, b =>
                {
                    Instance.stackSizeController.SetEnabled(b);
                    StackSize();
                });

                CreateInputButton(cui, (0.14f, 0.26f, 0.92f, 1), T("seditor_hotbar_stacks"), Instance.stackSizeController.GetLimitHotbarStacks(), b =>
                {
                    Instance.stackSizeController.SetLimitHotbarStacks(b);
                    StackSize();
                });

                cui.AddBox((0, 1, 0.9f, 0.91f), Style.ColorLightGrey);

                using (cui.CreateContainer((BOUNDS_X_L, 0.5f, 0.01f, 0.88f), Style.ColorGrey05))
                {
                    cui.AddLabel((0, 1, 0.92f, 1), T("seditor_prefabs"), Style.TextHeading);
                    cui.AddButton((0.85f, 0.98f, 0.94f, 0.98f), TU("btn_addgroup"), _ =>
                    {
                        stackSize_entry = Instance.stackSizeController.CreateEntry();
                        StackSize();
                    }, Style.ButtonGreen);

                    var move = MovingAnchorProperties.Create(0, 0.9f, 0.45f, 0.1f, 0.05f, 0.02f);
                    foreach (var entry in Instance.stackSizeController.GetEntries())
                    {
                        var isDefault = IsDefault(entry);
                        using (cui.CreateContainer(Anchor.MoveRow(ref move, 2), entry == stackSize_entry ? Style.ColorBlue : Style.ColorGrey05))
                        {
                            cui.AddLabel(Anchor.Relative(0, 1, 0.5f, 1, 8), isDefault ? "<i>DEFAULT</i>" : entry.GetDisplayName(T), Style.TextLabel);

                            var label = isDefault ? T("seditor_default_desc") : T("seditor_prefab_count", entry.Prefabs.Count);
                            cui.AddLabel(Anchor.Relative(0, 1, 0, 0.5f, 8), label, Style.TextNormal);


                            cui.AddButton(Anchor.Fill, string.Empty, _ =>
                            {
                                stackSize_entry = entry;
                                StackSize();
                            }, Style.ButtonTransparent);
                        }
                    }
                }

                using (cui.CreateContainer((0.5f, BOUNDS_X_R, 0.01f, 0.88f), UiColor.Transparent))
                {
                    if (stackSize_entry != null)
                    {
                        var isDefault = IsDefault(stackSize_entry);
                        if (!isDefault)
                        {
                            cui.AddButton((0.86f, 0.95f, 0.95f, 0.99f), TU("btn_delgroup"), _ =>
                            {
                                Instance.stackSizeController.DeleteEntry(ref stackSize_entry);
                                StackSize();
                            }, Style.ButtonRed);
                        }

                        cui.AddLabel((0.05f, 0.75f, 0.95f, 0.99f), isDefault ? "DEFAULT" : stackSize_entry.GetDisplayName(T), Style.TextLabel);

                        cui.AddButton((0.05f, 0.2f, 0.85f, 0.93f), TU("btn_edit_stacksize"), _ =>
                        {
                            StackSizeEditor();
                        }, Style.ButtonYellow);

                        if (!isDefault)
                        {
                            CreateInputField(cui, (0.05f, 0.95f, 0.74f, 0.84f), T("seditor_add_prefab"), null, s =>
                            {
                                var prefabId = StringPool.Get(s);
                                if (prefabId > 0)
                                {
                                    stackSize_entry.Prefabs.Add(prefabId);
                                }
                                StackSize();
                            }, onButton: () => { }, buttonText: TU("btn_add"), buttonColor: Style.ColorGreen, buttonXMin: 0.9f);

                            var move = MovingAnchorProperties.Create(0.05f, 0.73f, 0.9f, 0.05f, 0, 0.005f);
                            foreach (var prefabId in stackSize_entry.Prefabs)
                            {
                                using (cui.CreateContainer(Anchor.MoveColumn(ref move), UiColor.Transparent))
                                {
                                    cui.AddButton(Anchor.Absolute(0, 0.5f, 0, 20, -10, 10), "X", _ =>
                                    {
                                        stackSize_entry.Prefabs.Remove(prefabId);
                                        StackSize();
                                    }, Style.ButtonRed);
                                    cui.AddLabel(Anchor.Relative(0, 1, 0, 1, 30), StringPool.Get(prefabId), Style.TextNormal);
                                }
                            }
                        }

                    }
                    else
                    {
                        cui.AddLabel(Anchor.Fill, T("seditor_no_group"), Style.TextLabel with { TextAnchor = TextAnchor.MiddleCenter });
                    }
                }


                cui.Send(Player, ref mainContainerChild);

                void Exit(BasePlayer _ = default)
                {
                    Instance.stackSizeController.Save();
                    Instance.UpdateStackingSubscriptions();
                    Home();
                }

                static bool IsDefault(StackSizeDataEntry entry)
                {
                    return entry == Instance.stackSizeController.GetDefaultEntry();
                }
            }

            private void StackSizeEditor()
            {
                Assert.NotNull(stackSize_entry);

                var isDefault = false;//Instance.stackSizeController.GetDefaultEntry() == stackSize_entry;
                using var cui = Cui.Create(Instance, mainContainer);

                cui.AddButton(Anchor.Relative(0.9f, BOUNDS_X_R, 0.95f, 0.99f), TU("btn_save"), Exit, Style.ButtonGreen);
                var head = T("stack_size_control"); //T("seditor_head", !isDefault ? stackSize_entry.GetDisplayName(T) : "<i>DEFAULT</i>");
                cui.AddLabel(Anchor.Relative(BOUNDS_X_L_TEXT, 0.4f, 0.95f, 0.99f), head, Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });

                using (cui.CreateContainer(Anchor.Relative(BOUNDS_X_L, 0.8f, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    var move = MovingAnchorProperties.Create(0, 1, 1f / 7, 0.04f, 0, 0);
                    foreach (var category in stackSize_itemCategories.Value.Keys)
                    {
                        cui.AddButton(Anchor.MoveRow(ref move, 7), T(category.ToString().ToLower()), _ =>
                        {
                            stackSize_tab = category;
                            stackSize_tabPage = 0;
                            StackSizeEditor();
                        }, stackSize_tab == category ? Style.ColorBlue : Style.ColorGrey, Style.TextButton);
                    }

                    const int row_size = 5, col_size = 8;
                    const int image_size = 48;

                    var itemMove = MovingAnchorProperties.Create(0, 0.9f, 0.198f, 0.105f, 0.008f, 0.014f);
                    foreach (var item in stackSize_itemCategories.Value[stackSize_tab].Skip(stackSize_tabPage * row_size * col_size).Take(row_size * col_size))
                    {
                        using (cui.CreateContainer(Anchor.MoveRow(ref itemMove, row_size), Style.ColorGrey))
                        {
                            cui.AddItemImage(Anchor.AbsoluteCentered(0.2f, 0.5f, image_size, image_size), item.itemid);

                            var vss = Instance.stackSizeController.GetVanillaStackSize(item.itemid);
                            cui.AddLabel((0.4f, 0.65f, 0.55f, 0.9f), $"{T("vanilla")}:", Style.TextNormal with { TextAnchor = TextAnchor.MiddleRight });
                            cui.AddLabel((0.7f, 0.95f, 0.55f, 0.9f), vss.ToString(), Style.TextNormal);

                            var css = stackSize_entry.GetCustomStackSize(item.itemid);
                            cui.AddLabel((0.4f, 0.65f, 0.1f, 0.45f), $"{T("custom")}:", Style.TextNormal with { TextAnchor = TextAnchor.MiddleRight });
                            using (cui.CreateContainer((0.7f, 0.95f, 0.1f, 0.45f), css > 0 ? Style.ColorYellow05 : Style.ColorLightGrey05))
                            {
                                cui.AddInputField(Anchor.Fill, css > 0 ? css.ToString() : string.Empty, (_, s) =>
                                {
                                    if (Int32.TryParse(s, out var result) && result > 0)
                                    {
                                        stackSize_entry.SetCustomStackSize(item.itemid, result);
                                    }
                                    else
                                    {
                                        stackSize_entry.ClearCustomStackSize(item.itemid);
                                    }
                                    StackSizeEditor();
                                });
                            }

                            cui.AddLabel(Anchor.Absolute(0.2f, 0.5f, -image_size, image_size / 2, -image_size / 2, 0), stackSize_entry.GetStackSize(vss, item).ToString("N0"), Style.TextLabel with { TextAnchor = TextAnchor.LowerRight });
                        }
                    }

                    var maxPage = Mathf.CeilToInt((float)stackSize_itemCategories.Value[stackSize_tab].Count() / (row_size * col_size));
                    cui.AddLabel(Anchor.Relative(0, 1, 0.01f, 0.04f), T("pagecount", stackSize_tabPage + 1, maxPage));
                    cui.AddButton(Anchor.Relative(0.01f, 0.1f, 0.01f, 0.04f), "<--", _ =>
                    {
                        if (stackSize_tabPage > 0)
                        {
                            stackSize_tabPage--;
                            StackSizeEditor();
                        }
                    }, stackSize_tabPage > 0 ? Style.ButtonBlue : Style.ButtonDisabled);
                    cui.AddButton(Anchor.Relative(0.9f, 0.99f, 0.01f, 0.04f), "-->", _ =>
                    {
                        if (stackSize_tabPage + 1 < maxPage)
                        {
                            stackSize_tabPage++;
                            StackSizeEditor();
                        }
                    }, stackSize_tabPage + 1 < maxPage ? Style.ButtonBlue : Style.ButtonDisabled);
                }

                using (cui.CreateContainer(Anchor.Relative(0.805f, BOUNDS_X_R, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    CreateInputButton(cui, (0.05f, 0.95f, 0.9f, 0.98f), T("enabled"), Instance.stackSizeController.Enabled /*stackSize_entry.Enabled || isDefault*/, b =>
                    {
                        //stackSize_entry.Enabled = b;
                        stackSize_entry.Enabled = true;
                        Instance.stackSizeController.SetEnabled(b);
                        StackSizeEditor();
                    }, enabledStyle: isDefault ? Style.ButtonDisabled : null);
                    
                    CreateInputButton(cui, (0.05f, 0.95f, 0.8f, 0.88f), T("seditor_hotbar_stacks"), Instance.stackSizeController.GetLimitHotbarStacks(), b =>
                    {
                        Instance.stackSizeController.SetLimitHotbarStacks(b);
                        StackSizeEditor();
                    });

                    CreateInputField(cui, (0.05f, 0.95f, 0.7f, 0.78f), T("seditor_global_multiplier"), stackSize_entry.GlobalMultiplier.ToString(), s =>
                    {
                        if (Single.TryParse(s, out var result) && result > 0)
                        {
                            stackSize_entry.GlobalMultiplier = result;
                        }
                        StackSizeEditor();
                    });

                    CreateInputField(cui, (0.05f, 0.95f, 0.6f, 0.68f), T("seditor_cat_multiplier", stackSize_tab), stackSize_entry.GetCategoryMultiplier(stackSize_tab).ToString(), s =>
                    {
                        if (Single.TryParse(s, out var result) && result > 0)
                        {
                            stackSize_entry.SetCategoryMultiplier(stackSize_tab, result);
                        }
                        StackSizeEditor();
                    });

                    cui.AddLabel((0.05f, 0.95f, 0.4f, 0.58f), T("seditor_info"), Style.TextNormal with { TextAnchor = TextAnchor.UpperLeft });

                    cui.AddButton((0.05f, 0.95f, 0.02f, 0.07f), TU("seditor_reset"), _ =>
                    {
                        CreateConfirmDialog(T("seditor_reset"), T("seditor_reset_text"), () =>
                        {
                            stackSize_entry.ResetCustomStackSize();
                            StackSizeEditor();
                        });
                    }, Style.ButtonRed);
                }

                cui.Send(Player, ref mainContainerChild);

                void Exit(BasePlayer _)
                {
                    Instance.stackSizeController.Save();
                    Instance.UpdateStackingSubscriptions();
                    Home();
                }
            }

            #endregion

            #region Custom Item Manager

            private const int customItems_items_perPage = 30;
            private int customItems_itemPage;

            private void CustomItems()
            {
                using var cui = Cui.Create(Instance, mainContainer);

                cui.AddButton(Anchor.Relative(0.9f, BOUNDS_X_R, 0.95f, 0.99f), TU("btn_done"), _ => Home(), Style.ButtonBlue);
                cui.AddButton(Anchor.Relative(0.79f, 0.89f, 0.95f, 0.99f), TU("btn_addcustom"), _ => EditItem(CustomItemDefinition.Create(963906841, 2979393128, "Hampter")), Style.ButtonGreen);
                cui.AddLabel(Anchor.Relative(BOUNDS_X_L_TEXT, 0.4f, 0.95f, 0.99f), T("custom_item_manager"), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });

                using (cui.CreateContainer(Anchor.Relative(0.805f, BOUNDS_X_R, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    cui.AddLabel((0.05f, 0.95f, 0.6f, 0.98f), T("custom_item_info"), Style.TextNormal with { TextAnchor = TextAnchor.UpperLeft });
                }

                using (cui.CreateContainer(Anchor.Relative(BOUNDS_X_L, 0.8f, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    var move = MovingAnchorProperties.Create(0, 0.98f, 0.07f, 0.119f, 0.01f, 0.013f);
                    var lastPlugin = String.Empty;
                    int itemCount;
                    foreach (var item in Instance.manager.CustomItems.GetCustomItems(out itemCount).Skip(customItems_itemPage * customItems_items_perPage).Take(customItems_items_perPage))
                    {
                        if (lastPlugin != item.GetPlugin())
                        {
                            lastPlugin = item.GetPlugin();
                            move.NextRow();
                            if (lastPlugin != Instance.Name)
                            {
                                cui.AddLabel(Anchor.MoveOffsetY(ref move, 0.06f, true) with { XMax = 0.8f }, lastPlugin.WithSpaceBeforeUppercase(), Style.TextNormalBold with { TextAnchor = TextAnchor.LowerLeft });
                            }
                        }

                        Action<BasePlayer> callback = lastPlugin == Instance.Name ? _ =>
                        {
                            EditItem(item);
                        } : null;

                        CreateItemPanel(cui, Anchor.MoveRow(ref move, 14), item.ItemId, item.SkinId, item.GetDisplayName(Lang, Player), callback);
                    }

                    var maxPage = Mathf.CeilToInt((float)itemCount / customItems_items_perPage);
                    cui.AddLabel(Anchor.Relative(0, 1, 0.01f, 0.035f), T("pagecount", customItems_itemPage + 1, maxPage));
                    cui.AddButton(Anchor.Relative(0.01f, 0.1f, 0.01f, 0.035f), "<--", _ =>
                    {
                        if (customItems_itemPage > 0)
                        {
                            customItems_itemPage--;
                            CustomItems();
                        }
                    }, customItems_itemPage > 0 ? Style.ButtonBlue : Style.ButtonDisabled);
                    cui.AddButton(Anchor.Relative(0.9f, 0.99f, 0.01f, 0.035f), "-->", _ =>
                    {
                        if (customItems_itemPage + 1 < maxPage)
                        {
                            customItems_itemPage++;
                            CustomItems();
                        }
                    }, customItems_itemPage + 1 < maxPage ? Style.ButtonBlue : Style.ButtonDisabled);
                }

                cui.Send(Player, ref mainContainerChild);

                void EditItem(CustomItemDefinition def)
                {
                    customItems_original = def;
                    customItems_editDef = def;
                    CustomItems_EditOverlay();
                }
            }


            private CustomItemDefinition customItems_editDef;
            private CustomItemDefinition customItems_original;
            private UiRef customItems_mulitplierOverlay_panel;

            private void CustomItems_EditOverlay()
            {
                using var cui = Cui.Create(Instance, rootView);
                cui.AddButton(Anchor.Fill, string.Empty, _ => Destroy(), Style.ButtonTransparentDark);
                using (cui.CreateContainer(Anchor.Relative(0.3f, 0.7f, 0.5f, 0.8f), Style.ColorGrey))
                {
                    cui.AddLabel((0, 1, 0.85f, 0.98f), T("custom_items_edit"), Style.TextHeading);

                    CreateInputField(cui, (0.05f, 0.48f, 0.6f, 0.85f), T("shortname"), customItems_editDef.Shortname, s =>
                    {
                        var itemDef = ItemManager.FindItemDefinition(s);
                        if (itemDef != null)
                        {
                            customItems_editDef = customItems_editDef with { ItemId = itemDef.itemid };
                        }

                        CustomItems_EditOverlay();
                    });

                    CreateInputField(cui, (0.52f, 0.95f, 0.6f, 0.85f), T("skinid"), customItems_editDef.SkinId.ToString(), s =>
                    {
                        if (UInt64.TryParse(s, out var skin))
                        {
                            customItems_editDef = customItems_editDef with { SkinId = skin };
                        }

                        CustomItems_EditOverlay();
                    });

                    CreateInputField(cui, (0.05f, 0.48f, 0.3f, 0.55f), T("customname"), customItems_editDef.CustomName, s =>
                    {
                        customItems_editDef = customItems_editDef with { CustomName = s };
                        CustomItems_EditOverlay();
                    }, buttonText: T("clear"), onButton: () =>
                    {
                        customItems_editDef = customItems_editDef with { CustomName = null };
                        CustomItems_EditOverlay();
                    });

                    cui.AddButton((0.05f, 0.28f, 0.05f, 0.2f), TU("btn_cancel"), _ =>
                    {
                        Destroy();
                    }, Style.ButtonRed);

                    cui.AddButton((0.3f, 0.58f, 0.05f, 0.2f), TU("btn_delete"), _ =>
                    {
                        Instance.manager.CustomItems.UpdateCustomItem(customItems_original, null);
                        Destroy();
                    }, Style.ButtonRed);
                    
                    cui.AddButton((0.6f, 0.95f, 0.05f, 0.2f), TU("btn_save"), _ =>
                    {
                        Instance.manager.CustomItems.UpdateCustomItem(customItems_original, customItems_editDef);
                        Destroy();
                    }, Style.ButtonGreen);
                }

                cui.Send(Player, ref customItems_mulitplierOverlay_panel);

                void Destroy()
                {
                    customItems_editDef = null;
                    customItems_original = null;
                    Cui.Destroy(Player, cui.RootRef);
                    CustomItems();
                }
            }

            #endregion
            
            #region Settings
            
            private void Settings()
            {
                using var cui = Cui.Create(Instance, mainContainer);

                cui.AddButton(Anchor.Relative(0.9f, BOUNDS_X_R, 0.95f, 0.99f), TU("btn_done"), _ => Home(), Style.ButtonBlue);
                cui.AddLabel(Anchor.Relative(BOUNDS_X_L_TEXT, 0.4f, 0.95f, 0.99f), T("settings"), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });

                // using (cui.CreateContainer(Anchor.Relative(0.805f, BOUNDS_X_R, 0.02f, 0.94f), Style.ColorGrey05))
                // {
                //     cui.AddLabel((0.05f, 0.95f, 0.6f, 0.98f), T("custom_item_info"), Style.TextNormal with { TextAnchor = TextAnchor.UpperLeft });
                // }

                using (cui.CreateContainer(Anchor.Relative(BOUNDS_X_L, BOUNDS_X_R, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    CreateInputButton(cui, (0.02f, 0.13f, 0.9f, 0.99f), T("loot_refresh"), Instance.state.AutoLootRefresh, b =>
                    {
                        Instance.state.AutoLootRefresh = b;
                        Instance.state.Save();
                        Settings();
                    });

                    var canImport = GetLegacyDataFolder(out _);
                    
                    CreateInputButton(cui, (0.02f, 0.15f, 0.73f, 0.87f), T("import_loot"), TU("import"), canImport ? Style.ButtonRed : Style.ButtonDisabled, () =>
                    {
                        CreateConfirmDialog(T("import_confirmation"), T("import_confirmation_loot"), () =>
                        {
                            Instance.ImportLegacyFiles(LegacyImportTarget.LOOT, true);  
                        }, confirmColor: Style.ColorRed, cancelColor: Style.ColorGreen);
                        Settings();
                    });
                    
                    CreateInputButton(cui, (0.16f, 0.29f, 0.73f, 0.87f), T("import_stacksize"), TU("import"), canImport ? Style.ButtonRed : Style.ButtonDisabled, () =>
                    {
                        CreateConfirmDialog(T("import_confirmation"), T("import_confirmation_stacksize"), () =>
                        {
                            Instance.ImportLegacyFiles(LegacyImportTarget.STACK_SIZE, true);  
                        }, confirmColor: Style.ColorRed, cancelColor: Style.ColorGreen);
                        Settings();
                    });
                    
                    CreateInputButton(cui, (0.30f, 0.43f, 0.73f, 0.87f), T("import_customitems"), TU("import"), canImport ? Style.ButtonRed : Style.ButtonDisabled, () =>
                    {
                        CreateConfirmDialog(T("import_confirmation"), T("import_confirmation_customitems"), () =>
                        {
                            Instance.ImportLegacyFiles(LegacyImportTarget.CUSTOM_ITEMS, false);  
                        }, confirmColor: Style.ColorRed, cancelColor: Style.ColorGreen);
                        Settings();
                    });
                    
                    CreateInputButton(cui, (0.44f, 0.57f, 0.73f, 0.87f), T("import_configuration"), TU("import"), canImport ? Style.ButtonRed : Style.ButtonDisabled, () =>
                    {
                        CreateConfirmDialog(T("import_confirmation"), T("import_confirmation_configuration"), () =>
                        {
                            Instance.ImportLegacyFiles(LegacyImportTarget.SMELTING_AND_SUPPLY, false);  
                        }, confirmColor: Style.ColorRed, cancelColor: Style.ColorGreen);
                        Settings();
                    });
                }

                cui.Send(Player, ref mainContainerChild);
            }
            
            #endregion
            
            #region Config Manager
            
            private void ConfigPage()
            {
                using var cui = Cui.Create(Instance, mainContainer);

                cui.AddButton(Anchor.Relative(0.9f, BOUNDS_X_R, 0.95f, 0.99f), TU("btn_save"), _ =>
                {
                    Instance.configurations.Save();
                    Instance.configurations.Initialize();
                    Instance.UpdateConfigSubscriptions();
                    Home();
                }, Style.ButtonGreen);
                cui.AddLabel(Anchor.Relative(BOUNDS_X_L_TEXT, 0.4f, 0.95f, 0.99f), T("configuration"), Style.TextHeading with { TextAnchor = TextAnchor.MiddleLeft });

                const float h = 0.08f;
                const float hi = 0.06f;
                const float wi = 0.11f;
                const float di = 0.02f;
                const float si = 38f;
                const float ds = 0.03f;
                const float d = 0.09f;
                
                using (cui.CreateContainer((BOUNDS_X_L, 0.32f, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    cui.AddLabel((ds, 1f - ds, 0.96f, 1), T("configuration_smelting"), Style.TextLabel);
                    
                    if (Instance.SimpleSplitter != null)
                    {
                        cui.AddLabel((ds, 1 - ds, 0.1f, 0.9f), T("sp_disabled_by_plugin"), Style.TextHeading with { Font = UiFont.robotoRegular});
                    }
                    else
                    {
                        var sc = Instance.configurations.Smelting;
                        float i = 0.935f - h;
                        
                        CreateInputButton(cui, (ds, 1f - ds, i, i + h), T("enabled"), sc.Enabled, b =>
                        {
                            sc.Enabled = b;
                            ConfigPage();
                        });
                        i -= d;
                        
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -1999722522);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_furnace_small"), sc.SmallFurnaceSpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.SmallFurnaceSpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                            }
                            ConfigPage();
                        });
                        i -= d;
                        
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -1992717673);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_furnace_large"), sc.LargeFurnaceSpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.LargeFurnaceSpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                            }
                            ConfigPage();
                        });
                        i -= d;
                        
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -1196547867);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_furnace_electric"), sc.ElectricFurnaceSpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.ElectricFurnaceSpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                            }
                            ConfigPage();
                        });
                        i -= d;
                        
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -1293296287);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_refinery"), sc.RefinerySpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.RefinerySpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                            }
                            ConfigPage();
                        });
                        i -= d;
                        
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), 1099314009);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_bbq"), sc.BBQSpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.BBQSpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                            }
                            ConfigPage();
                        });
                        i -= d;
                        
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), 1946219319);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_campfire"), sc.CampfireSpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.CampfireSpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                            }
                            ConfigPage();
                        });
                        i -= d;
                        
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), 1259919256);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_mixing_table"), sc.MixingSpeedMultiplier.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.MixingSpeedMultiplier = Mathf.Clamp(mpl, 0.001f, 100_000f);
                            }
                            ConfigPage();
                        });
                        
                        i -= (d + 0.03f);
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -1938052175);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("charcoal_chance_percent"), (sc.CharcoalChance * 100).ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var chance))
                            {
                                sc.CharcoalChance = Mathf.Clamp(chance / 100f, 0, 1);
                            }
                            ConfigPage();
                        });
                        
                        i -= d;
                        cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -1938052175);
                        CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("charcoal_amount"), sc.CharcoalAmount.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Int32.TryParse(s, out var amount))
                            {
                                sc.CharcoalAmount = Mathf.Max(1, amount);
                            }
                            ConfigPage();
                        });
                    }
                    
                }

                using (cui.CreateContainer((0.33f, 0.65f, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    cui.AddLabel((ds, 1f - ds, 0.96f, 1), T("configuration_supply_drop"), Style.TextLabel);
                    
                    if (Instance.FancyDrop != null || Instance.LootDefender != null)
                    {
                        cui.AddLabel((ds, 1 - ds, 0.1f, 0.9f), T("sd_disabled_by_plugin"), Style.TextHeading with { Font = UiFont.robotoRegular});
                    }
                    else
                    {
                        var sc = Instance.configurations.SupplyDrop;
                        float i = 0.935f - h;
                        
                        CreateInputButton(cui, (ds, 1f - ds, i, i + h), T("enabled"), sc.Enabled, b =>
                        {
                            sc.Enabled = b;
                            ConfigPage();
                        });
                        
                        i -= d;
                        CreateInputField(cui, (ds, 1 - ds, i, i + h), T("sd_plane_speed"), sc.PlaneSpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var mpl))
                            {
                                sc.PlaneSpeed = Mathf.Clamp(mpl, 0.01f, 100f);
                            }
                            ConfigPage();
                        });
                        
                        i -= d;
                        CreateInputField(cui, (ds, 1 - ds, i, i + h), T("sd_plane_height"), sc.PlaneHeight.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Int32.TryParse(s, out var height))
                            {
                                sc.PlaneHeight = Mathf.Clamp(height, -400, 1000);
                            }
                            ConfigPage();
                        });
                        
                        i -= d;
                        CreateInputField(cui, (ds, 1 - ds, i, i + h), T("sd_smoke_duration"), sc.SmokeDuration.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Int32.TryParse(s, out var duration))
                            {
                                sc.SmokeDuration = Mathf.Clamp(duration, 0, 300);
                            }
                            ConfigPage();
                        });
                        
                        i -= d;
                        CreateInputField(cui, (ds, 1 - ds, i, i + h), T("sd_fall_speed"), sc.DropFallSpeed.ToString(CultureInfo.InvariantCulture), s =>
                        {
                            if (Single.TryParse(s, out var speed))
                            {
                                sc.DropFallSpeed = Mathf.Clamp(speed, 0.6f, 10);
                            }
                            ConfigPage();
                        });
                        
                        i -= d;
                        CreateInputButton(cui, (ds, 1 - ds, i, i + h), T("sd_drop_smoke"), sc.DropFallSmoke, b =>
                        {
                            sc.DropFallSmoke = b;
                            ConfigPage();
                        });
                        
                        i -= d;
                        CreateInputButton(cui, (ds, 1 - ds, i, i + h), T("sd_exact_position"), sc.ExactDropPosition, b =>
                        {
                            sc.ExactDropPosition = b;
                            ConfigPage();
                        });

                        if (!sc.ExactDropPosition)
                        {
                            i -= d;
                            CreateInputField(cui, (ds, 1 - ds, i, i + h), T("sd_position_tolerance"), sc.RandomPosTolerance.ToString(CultureInfo.InvariantCulture), s =>
                            {
                                if (Single.TryParse(s, out var tolerance))
                                {
                                    sc.RandomPosTolerance = Mathf.Clamp(tolerance, 1, 200);
                                }
                                ConfigPage();
                            });
                        }
                        
                        i -= d;
                        CreateInputButton(cui, (ds, 1 - ds, i, i + h), T("sd_cooldown"), sc.CooldownEnabled, b =>
                        {
                            sc.CooldownEnabled = b;
                            ConfigPage();
                        });

                        if (sc.CooldownEnabled)
                        {
                            i -= d;
                            CreateInputField(cui, (ds, 1 - ds, i, i + h), T("sd_cooldown_time"), sc.CooldownSeconds.ToString(), s =>
                            {
                                if (Int32.TryParse(s, out var cooldown))
                                {
                                    sc.CooldownSeconds = Mathf.Max(1, cooldown);
                                }
                                ConfigPage();
                            });
                        }
                    }
                    
                }
                
                using (cui.CreateContainer((0.66f, BOUNDS_X_R, 0.02f, 0.94f), Style.ColorGrey05))
                {
                    var sc = Instance.configurations.Smelting;
                    cui.AddLabel((ds, 1f - ds, 0.96f, 1), T("configuration_recycler"), Style.TextLabel);
                    
                    float i = 0.935f - h;
                    
                    CreateInputButton(cui, (ds, 1f - ds, i, i + h), T("enabled"), sc.RecyclerEnabled, b =>
                    {
                        sc.RecyclerEnabled = b;
                        ConfigPage();
                    });
                    
                    i -= d;
                    cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -180129657, 3036100875);
                    CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_speed"), sc.RecyclerSpeed.ToString(CultureInfo.InvariantCulture), s =>
                    {
                        if (Single.TryParse(s, out var mpl))
                        {
                            sc.RecyclerSpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                        }
                        ConfigPage();
                    });

                    i -= d;
                    cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -180129657, 3036100875);
                    CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_efficiency_percent"), (sc.RecyclerEfficiency * 100).ToString(CultureInfo.InvariantCulture), s =>
                    {
                        if (Single.TryParse(s, out var efficiency))
                        {
                            sc.RecyclerEfficiency = Mathf.Clamp(efficiency / 100f, 0.01f, 1f);
                        }
                        ConfigPage();
                    });
                    
                    i -= (d + 0.03f);
                    cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -180129657, 0); // TODO recycler skin sz
                    CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_sz_speed"), sc.RecyclerSafeSpeed.ToString(CultureInfo.InvariantCulture), s =>
                    {
                        if (Single.TryParse(s, out var mpl))
                        {
                            sc.RecyclerSafeSpeed = Mathf.Clamp(mpl, 0.001f, 100_000f);
                        }
                        ConfigPage();
                    });

                    i -= d;
                    cui.AddItemImage(Anchor.AbsoluteCentered((ds / 2) + (wi / 2), i + (hi / 2), si, si), -180129657, 0);
                    CreateInputField(cui, (wi + di, 1 - ds, i, i + h), T("sp_sz_efficiency_percent"), (sc.RecyclerSafeEfficiency * 100).ToString(CultureInfo.InvariantCulture), s =>
                    {
                        if (Single.TryParse(s, out var efficiency))
                        {
                            sc.RecyclerSafeEfficiency = Mathf.Clamp(efficiency / 100f, 0.01f, 1f);
                        }
                        ConfigPage();
                    });
                }

                cui.Send(Player, ref mainContainerChild);
            }
            
            #endregion

            #region Utility

            private static void CreateItemPanel(Cui cui, in Anchor anchor, int itemCat, LootItem item, Action<BasePlayer> onClick, UiColor? backgroundOverride = null, bool hideChance = false)
                => CreateItemPanel(cui, anchor, item.ItemId, item.SkinId, (itemCat != -1 || hideChance) ? $"x{item.Amount}" : $"x{item.Amount}\n{(item.Chance * 100)}%", onClick, item.Condition, item.IsBlueprint, itemCat, backgroundOverride);

            private static void CreateItemPanel(Cui cui, in Anchor anchor, int itemId, ulong skinId, string text, Action<BasePlayer> onClick,
                in MinMaxFloat condition = default, bool isBlueprint = false, int categoryId = -1, UiColor? backgroundOverride = null)
            {
                var hasCondition = !isBlueprint && condition != default && (ItemManager.FindItemDefinition(itemId)?.condition.enabled ?? false);
                var background = backgroundOverride ?? (categoryId < 0 ? Style.ColorLightGrey05 : Style.GetCategoryColor(categoryId));

                using (cui.CreateContainer(anchor, background))
                {
                    var imgSize = isBlueprint ? 50 : 40;
                    cui.AddItemImage(Anchor.AbsoluteCentered(hasCondition ? 0.58f : 0.5f, 0.66f, imgSize, imgSize), isBlueprint ? -996920608 : itemId, isBlueprint ? 0 : skinId);
                    if (isBlueprint)
                    {
                        cui.AddItemImage(Anchor.AbsoluteCentered(0.5f, 0.66f, 38, 38), itemId, skinId);
                    }

                    cui.AddLabel(Anchor.Relative(0.01f, 0.99f, 0.005f, 0.39f), text, Style.TextItem);

                    if (hasCondition)
                    {
                        using (cui.CreateContainer(Anchor.Absolute(0.58f, 0.65f, -28, -25, -20, 20), UiColor.Transparent))
                        {
                            cui.AddBox(Anchor.Fill, new UiColor(0.16f, 0.16f, 0.16f));

                            if (!condition.IsFixed)
                            {
                                cui.AddBox(Anchor.Relative(0, 1, 0, condition.Max), new UiColor(0.541f, 0.788f, 0.149f, 0.5f));
                            }

                            cui.AddBox(Anchor.Relative(0, 1, 0, condition.Min), new UiColor(0.541f, 0.788f, 0.149f, 1f));
                        }
                    }

                    if (onClick != null)
                    {
                        cui.AddButton(Anchor.Fill, String.Empty, onClick, Style.ButtonTransparent);
                    }
                }
            }

            private static void CreateInputField(Cui cui, in Anchor anchor, string label, [CanBeNull] string value, Action<string> onChange, InputFieldFlags flags = InputFieldFlags.None,
                string buttonText = null, Action onButton = null, UiColor? buttonColor = null, in LabelStyle labelStyle = default, float buttonXMin = 0.7f)
            {
                using (cui.CreateContainer(anchor, UiColor.Transparent))
                {
                    cui.AddLabel(Anchor.Relative(0, 1, 0.55f, 1f), label, labelStyle == default ? Style.TextLabel : labelStyle);
                    using (cui.CreateContainer(Anchor.Relative(0, 1, 0, 0.5f), Style.ColorLightGrey05))
                    {
                        cui.AddInputField(Anchor.Relative(0, 1, 0, 1, 10), value ?? string.Empty, (_, s) => onChange(s), Style.TextNormal, flags);
                    }

                    if (buttonText != null && onButton != null)
                    {
                        cui.AddButton(Anchor.Relative(buttonXMin, 1, 0, 0.5f), buttonText, _ => onButton(), new ButtonStyle
                        {
                            ButtonColor = buttonColor ?? Style.ColorLightGrey,
                            TextStyle = new LabelStyle
                            {
                                Font = UiFont.robotoBold,
                                FontSize = 12,
                                TextColor = UiColor.White,
                                TextAnchor = TextAnchor.MiddleCenter
                            }
                        });
                    }
                }
            }

            private void CreateInputButton(Cui cui, in Anchor anchor, string label, bool value, Action<bool> onChange, ButtonStyle? enabledStyle = null, ButtonStyle? disabledStyle = null)
            {
                using (cui.CreateContainer(anchor, UiColor.Transparent))
                {
                    cui.AddLabel(Anchor.Relative(0, 1, 0.55f, 1f), label, Style.TextLabel);
                    cui.AddButton(Anchor.Relative(0, 1, 0, 0.5f), value ? TU("enabled") : TU("disabled"), _ => onChange(!value), value ? (enabledStyle ?? Style.ButtonGreen) : (disabledStyle ?? Style.ButtonRed));
                }
            }

            private static void CreateInputButton(Cui cui, in Anchor anchor, string label, string value, ButtonStyle style, Action onClick)
            {
                using (cui.CreateContainer(anchor, UiColor.Transparent))
                {
                    cui.AddLabel(Anchor.Relative(0, 1, 0.55f, 1f), label, Style.TextLabel);
                    cui.AddButton(Anchor.Relative(0, 1, 0, 0.5f), value, _ => onClick(), style);
                }
            }

            private void CreateConfirmDialog(string title, string text, [CanBeNull] Action onConfirm, bool showCancel = true, UiColor confirmColor = default, UiColor cancelColor = default)
            {
                confirmColor = confirmColor == default ? showCancel ? Style.ColorGreen : Style.ColorBlue : confirmColor;
                cancelColor = cancelColor == default ? Style.ColorRed : cancelColor;

                using var cui = Cui.Create(Instance, rootView, "assets/content/ui/uibackgroundblur.mat", 0.3f);
                cui.AddButton(Anchor.Fill, String.Empty, Destroy, Style.ButtonTransparentDark);
                using (cui.CreateContainer(Anchor.Relative(0.38f, 0.62f, 0.45f, 0.7f), Style.ColorGrey))
                {
                    cui.AddLabel((0.05f, 0.95f, 0.8f, 0.95f), title, Style.TextHeading);
                    cui.AddLabel((0.05f, 0.95f, 0.35f, 0.8f), text, Style.TextNormal);

                    if (showCancel)
                    {
                        cui.AddButton((0.05f, 0.48f, 0.1f, 0.3f), TU("btn_cancel"), Destroy, cancelColor, Style.TextButton);
                    }

                    cui.AddButton((showCancel ? 0.52f : 0.05f, 0.95f, 0.1f, 0.3f), TU("btn_ok"), _ =>
                    {
                        Cui.Destroy(Player, cui.RootRef);
                        onConfirm?.Invoke();
                    }, confirmColor, Style.TextButton);
                }

                cui.Send(Player, ref editor_mulitplierOverlay_panel);

                void Destroy(BasePlayer _)
                {
                    Cui.Destroy(Player, cui.RootRef);
                }
            }

            #endregion

            #region Translation

            private string TU(string key)
            {
                return T(key).ToUpper();
            }

            private string T(string key)
            {
                return Lang?.GetMessage(key, Player) ?? key;
            }

            private string T(string key, object arg1)
            {
                return Lang?.GetMessage(key, Player, arg1) ?? key;
            }

            private string T(string key, object arg1, object arg2)
            {
                return Lang?.GetMessage(key, Player, arg1, arg2) ?? key;
            }

            #endregion

            #region Style

            private static class Style
            {
                public static readonly UiColor ColorBlue = new(0.254f, 0.254f, 0.8f);
                public static readonly UiColor ColorRed = new(0.8f, 0.254f, 0.254f);
                public static readonly UiColor ColorGreen = new(0.254f, 0.8f, 0.254f);
                public static readonly UiColor ColorGreen05 = new(0.254f, 0.8f, 0.254f, 0.5f);
                public static readonly UiColor ColorYellow = new(1, 0.8f, 0.23f);
                public static readonly UiColor ColorYellow05 = new(1, 0.8f, 0.23f, 0.5f);

                public static readonly UiColor ColorGrey = new(0.2f, 0.2f, 0.2f);
                public static readonly UiColor ColorLightGrey = new(0.4f, 0.4f, 0.4f);

                public static readonly UiColor ColorGrey05 = new(0.22f, 0.22f, 0.22f, 0.5f);
                public static readonly UiColor ColorLightGrey05 = new(0.4f, 0.4f, 0.4f, 0.5f);

                public static readonly LabelStyle TextItem = new()
                {
                    TextAnchor = TextAnchor.MiddleCenter,
                    TextColor = Color.white,
                    Font = UiFont.robotoRegular,
                    FontSize = 10,
                };

                public static readonly LabelStyle TextHeading = new()
                {
                    TextAnchor = TextAnchor.MiddleCenter,
                    TextColor = Color.white,
                    Font = UiFont.robotoBold,
                    FontSize = 14,
                };

                public static readonly LabelStyle TextNormal = new()
                {
                    TextAnchor = TextAnchor.MiddleLeft,
                    TextColor = Color.white,
                    Font = UiFont.robotoRegular,
                    FontSize = 12,
                };

                public static readonly LabelStyle TextNormalBold = TextNormal with { Font = UiFont.robotoBold };

                public static readonly LabelStyle TextBold = new()
                {
                    TextAnchor = TextAnchor.MiddleLeft,
                    TextColor = Color.white,
                    Font = UiFont.robotoBold,
                    FontSize = 12,
                };

                public static readonly LabelStyle TextLabel = new()
                {
                    TextAnchor = TextAnchor.LowerLeft,
                    TextColor = Color.white,
                    Font = UiFont.robotoBold,
                    FontSize = 14,
                };

                public static readonly LabelStyle TextButton = new()
                {
                    Font = UiFont.robotoBold,
                    FontSize = 13,
                    TextColor = Color.white,
                    TextAnchor = TextAnchor.MiddleCenter
                };

                #region Button

                public static readonly ButtonStyle ButtonTransparent = new()
                {
                    ButtonColor = UiColor.Transparent,
                    TextStyle = LabelStyle.Default
                };

                public static readonly ButtonStyle ButtonTransparentDark = new()
                {
                    ButtonColor = UiColor.Black with { Opacity = 0.8f },
                    TextStyle = LabelStyle.Default
                };

                public static readonly ButtonStyle ButtonRed = new()
                {
                    ButtonColor = ColorRed,
                    TextStyle = TextButton
                };

                public static readonly ButtonStyle ButtonGreen = new()
                {
                    ButtonColor = ColorGreen,
                    TextStyle = TextButton
                };

                public static readonly ButtonStyle ButtonGrey = new()
                {
                    ButtonColor = ColorGrey,
                    TextStyle = TextButton
                };

                public static readonly ButtonStyle ButtonLightGrey = new()
                {
                    ButtonColor = ColorLightGrey,
                    TextStyle = TextButton
                };

                public static readonly ButtonStyle ButtonDisabled = new()
                {
                    ButtonColor = ColorGrey,
                    TextStyle = TextButton with { TextColor = ColorLightGrey }
                };

                public static readonly ButtonStyle ButtonYellow = new()
                {
                    ButtonColor = ColorYellow,
                    TextStyle = TextButton
                };

                public static readonly ButtonStyle ButtonBlue = new()
                {
                    ButtonColor = ColorBlue,
                    TextStyle = TextButton
                };

                #endregion

                public static UiColor GetCategoryColor(int categoryIdx)
                {
                    return categoryIdx switch
                    {
                        0 => new UiColor(0.31f, 0.847f, 0.56f) * 0.5f,
                        1 => new UiColor(0.078f, 0.71f, 1) * 0.5f,
                        2 => new UiColor(1, 0.755f, 0.341f) * 0.7f,
                        3 => new UiColor(0.906f, 0.094f, 0.486f) * 0.7f,
                        4 => new UiColor(0, 0.251f, 0.576f) * 0.6f,
                        5 => new UiColor(0.6f, 0, 0),
                        420 => ColorBlue,
                        _ => ColorGrey
                    };
                }
            }

            #endregion

            private enum ItemOrder : byte
            {
                // DO NOT CHANGE NAMES, they are used as lang keys
                order_category,
                order_category_desc,
                order_chance,
                order_chance_desc
            }
        }

        private PlayerUi playerUi;

        private void CmdLoottable(IPlayer iPlayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(iPlayer.Id, PERM_EDIT))
            {
                iPlayer.Reply("No permission");
                return;
            }

            if (args.GetString(0) == "reload")
            {
                iPlayer.Reply("Reloading loot tables ...");
                LootManager.RefreshLoot(args.GetString(1) == "vanilla", iPlayer);
                return;
            }

            if (iPlayer.Object is not BasePlayer player)
            {
                return;
            }

            if (playerUi == null || playerUi.Destroyed)
            {
                playerUi = new PlayerUi(player);
                playerUi.Create();
            }
            else if (playerUi.Player == player)
            {
                playerUi.Destroy();
                playerUi = null;
            }
            else
            {
                iPlayer.Reply("Only one player at a time can edit the loot table");
            }
        }

        #endregion

        #region Manager

        // Manager was already taken, so thats the new class name I guess
        private class TheManagerThatManagesEverything
        {
            public readonly Api Api = new();
            public readonly CustomItemManager CustomItems = new();
            
            private readonly VanillaConfigManager vanillaConfigManager = new();
            private readonly LootableManager lootableManager = new();

            #region Init

            public void Destroy()
            {
                CustomItems.Save();
            }

            public void Init()
            {
                lootableManager.Init(LootableList.Get());
                CustomItems.Init();
            }

            public IEnumerable<IImage> GetImages()
            {
                return lootableManager.GetLootables();
            }
            
            #endregion

            #region Get Config

            public LootConfig GetConfig(TrainCarUnloadable wagon)
            {
                var id = lootableManager.GetTrainWagonConfig(wagon);
                return InternalGetConfig(id);
            }

            public LootConfig GetConfig(ExcavatorArm arm)
            {
                var id = lootableManager.GetExcavatorConfigId(arm);
                return InternalGetConfig(id);
            }

            public LootConfig GetConfig(MiningQuarry quarry)
            {
                var id = lootableManager.GetQuarryConfigId(quarry);
                return InternalGetConfig(id);
            }

            public LootConfig GetConfig(BaseEntity entity)
            {
                var id = lootableManager.GetConfigId(entity, false);
                return InternalGetConfig(id);
            }

            public LootConfig GetConfig(NPCPlayer npc)
            {
                var id = lootableManager.GetConfigId(npc, true);
                return InternalGetConfig(id);
            }

            public LootConfig GetConfig(Item item)
            {
                var id = lootableManager.GetItemConfigId(item);
                return InternalGetConfig(id);
            }

            public LootConfig GetConfig(ConfigId id)
            {
                if (!id.IsValid)
                {
                    LogDebug("Attempting to get config for invalid id");
                    return null;
                }

                return InternalGetConfig(id);
            }

            #endregion

            #region Config

            public bool LoadDefaultConfig(LootConfig config)
            {
                return lootableManager.LoadDefaultConfig(config);
            }

            public void SetCustomConfig(BaseEntity entity, ConfigId id)
            {
                lootableManager.SetConfigId(entity, id);
            }

            // Used for UI configs
            public LootConfig GetOrCreateConfig(ConfigId id)
            {
                if (!id.IsValid)
                {
                    throw new ArgumentException("Config id is invalid");
                }

                var cfg = GetConfigManager(id).GetConfig(id, true, out var justCreated);

                // Set default flags for vanilla lootables only
                if (id.Plugin == null)
                {
                    var lootable = lootableManager.GetLootable(id);
                    LogDebug($"Set flags for {lootable.Id}: {lootable.ConfigFlags}");
                    cfg.SetFlag(lootable.ConfigFlags, true);

                    if (justCreated && lootable.HasDefaultConfig)
                    {
                        lootable.LoadDefaultConfig(cfg);
                    }

                    // Init dispenser multipliers
                    if (lootable.ConfigFlags.HasFlag(LootConfigFlags.Dispenser) && (cfg.DispenserMultipliers == null || cfg.Version == 0))
                    {
                        LogDebug($"Init dispenser multipliers for {cfg.Id}");
                        lootable.InitDispenserMultipliers(cfg);
                    }
                }

                cfg.Version = LootConfig.CURRENT_VERSION;
                return cfg;
            }

            // Used for hooks
            private LootConfig InternalGetConfig(ConfigId id)
            {
                if (!id.IsValid)
                {
                    return null;
                }

                return GetConfigManager(id).GetConfig(id, false, out _);
            }
            
            public void SaveConfig(LootConfig config)
            {
                config.Validate();
                GetConfigManager(config.Id).SaveConfig(config);
            }

            private IConfigManager GetConfigManager(ConfigId id)
            {
                if (id.Plugin == null)
                {
                    return vanillaConfigManager;
                }
                else
                {
                    return Api;
                }
            }

            #endregion

            #region Lootable

            public IEnumerable<BaseLootable> GetLegacyLootables()
            {
                return lootableManager.GetLootables();
            }

            public IEnumerable<string> GetTabsVanilla()
            {
                return GetLootableManager(false).GetTabs();
            }

            public IEnumerable<string> GetTabsPlugin()
            {
                return GetLootableManager(true).GetTabs();
            }

            public IEnumerable<ILootable> GetTabContents(string tabName)
            {
                if (tabName == null)
                {
                    return Enumerable.Empty<ILootable>();
                }

                return GetLootableManager(tabName.StartsWith('#')).GetTab(tabName);
            }

            public ILootable GetLootable(ConfigId id)
            {
                return GetLootableManager(id.Plugin != null).GetLootable(id);
            }

            private ILootableManager GetLootableManager(bool isPlugin)
            {
                if (isPlugin)
                {
                    return Api;
                }
                else
                {
                    return lootableManager;
                }
            }

            #endregion

        }

        private class VanillaConfigManager : IConfigManager
        {
            private readonly Dictionary<string, LootConfig> cachedConfigs = new();

            public void SaveConfig(LootConfig config)
            {
                cachedConfigs[config.Id.Key] = config;
                WriteDataFile(config, "loot", config.Id.Key);
            }

            public LootConfig GetConfig(ConfigId id, bool createIfNotExists, out bool justCreated)
            {
                justCreated = false;
                if (cachedConfigs.TryGetValue(id.Key, out var config))
                {
                    return config;
                }
                
                config = ReadDataFile<LootConfig>("loot", id.Key);
                if (config != null)
                {
                    cachedConfigs.Add(config.Id.Key, config);
                }

                if (config == null && createIfNotExists)
                {
                    config = LootConfig.Create(id);
                    justCreated = true;
                }

                return config;
            }
        }

        interface IConfigManager
        {
            LootConfig GetConfig(ConfigId id, bool createIfNotExists, out bool justCreated);
            void SaveConfig(LootConfig config);
        }

        interface ILootableManager
        {
            ILootable GetLootable(ConfigId id);
            IEnumerable<ILootable> GetTab(string tab);
            IEnumerable<string> GetTabs();
        }

        #endregion

        #region Config classes

        [ProtoContract]
        private class LootConfig
        {
            public const int CURRENT_VERSION = 2;
            
            [ProtoMember(1)]
            public ConfigId Id { get; private set; }

            [ProtoMember(2)]
            public bool Enabled { get; private set; }

            [ProtoMember(3)]
            public LootType LootType { get; set; }

            [ProtoMember(4)]
            public MinMax Amount { get; set; } = new MinMax(1, 6);

            [ProtoMember(5)]
            [CanBeNull]
            private List<LootItem> additions;

            [ProtoMember(6)]
            public HashSet<int> Blacklist { get; init; } = new();

            [ProtoMember(7)]
            public List<LootItem> DefaultCategory { get; init; } = new();

            [ProtoIgnore]
            public int DefaultCategoryId => -1;

            [ProtoMember(8)]
            private Dictionary<int, LootCategory> Categories { get; init; } = new();

            [ProtoMember(9)]
            public LootConfigFlags Flags { get; private set; }

            [ProtoMember(10)]
            public bool NpcRemoveCorpse { get; set; }
            
            [ProtoMember(11)]
            [CanBeNull] 
            public Dictionary<int, float> DispenserMultipliers { get; set; }
            
            [ProtoMember(12)]
            public int Version { get; set; }

            //public LootConfigStats Stats { get; init; } = new();

            public int CategoryCount => Categories.Count;

            public void MultiplyDispenserItem(Item item)
            {
                if (DispenserMultipliers == null)
                {
                    LogError($"MultiplyDispenserItems called but DispenserMultipliers is null {Id}");
                    throw new InvalidOperationException($"MultiplyGatherItems is null in {Id}");
                }
                
                if (DispenserMultipliers.TryGetValue(item.info.itemid, out var multiplier))
                {
                    LogDebug($"Multiply {item.info.shortname}");
                    item.amount = Mathf.Max(1, Mathf.RoundToInt(item.amount * multiplier));
                }
                else
                {
                    LogDebug($"Don't multiply {item.info.shortname}");
                }
            }

            public IEnumerable<LootItemCategory> GetAllItems()
            {
                if (LootType == LootType.Blacklist)
                {
                    foreach (var item in Blacklist)
                    {
                        yield return new LootItemCategory(item);
                    }
                }
                else if (LootType == LootType.Addition)
                {
                    foreach (var item in GetAdditions())
                    {
                        yield return new LootItemCategory(item, -1);
                    }
                }
                else if (LootType == LootType.Custom)
                {
                    foreach (var item in DefaultCategory)
                    {
                        yield return new LootItemCategory(item, DefaultCategoryId);
                    }

                    foreach (var cat in GetCategories())
                    {
                        foreach (var item in cat.Items)
                        {
                            yield return new LootItemCategory(item, cat.Id);
                        }
                    }
                }
            }

            #region Categories

            public IEnumerable<LootCategory> GetCategories()
            {
                return Categories.OrderBy(x => x.Key).Select(x => x.Value);
            }

            private List<LootItem> GetCategoryItems(int categoryId)
            {
                if (categoryId < 0)
                {
                    return DefaultCategory;
                }
                else if (Categories.TryGetValue(categoryId, out var lootCategory))
                {
                    return lootCategory.Items;
                }

                return null;
            }

            public void AddCategory() => AddCategory(new LootCategory());

            public void AddCategory(LootCategory category)
            {
                var cat = category with
                {
                    Id = NextCategoryId()
                };

                Categories.Add(cat.Id, cat);
            }

            public void RemoveCategory(int categoryId)
            {
                if (Categories.Remove(categoryId, out var cat))
                {
                    // Move all remaining items to default category with category chance
                    foreach (var itm in cat.Items)
                    {
                        itm.Chance = cat.Chance;
                        DefaultCategory.Add(itm);
                    }
                    cat.Items.Clear();
                }
            }

            private int NextCategoryId()
            {
                if (Categories.Count < 1)
                {
                    return 0;
                }

                return Categories.Keys.Max() + 1;
            }

            #endregion

            #region Additions

            public IEnumerable<LootItem> GetAdditions()
            {
                return additions ?? Enumerable.Empty<LootItem>();
            }

            public void AddAddition(LootItem item)
            {
                additions ??= new List<LootItem>();
                additions.Add(item);
            }

            public void RemoveAddition(LootItem item)
            {
                if (additions == null || item == null)
                {
                    return;
                }

                additions.Remove(item);
                if (additions.Count < 1)
                {
                    additions = null;
                }
            }

            #endregion

            #region Items

            public void ReplaceItem(LootItemCategory original, LootItemCategory newItem)
            {
                if (LootType == LootType.Custom)
                {
                    if (original.Item != null)
                    {
                        var originalCat = GetCategoryItems(original.Category);
                        originalCat?.Remove(original.Item);
                    }

                    var newCat = GetCategoryItems(newItem.Category);
                    if (newCat != null)
                    {
                        newCat.Add(newItem.Item);
                    }
                    else
                    {
                        LogError($"Failed to save item {newItem.Item} - Category {newItem.Category} does not exist");
                    }
                }
                else if (LootType == LootType.Addition)
                {
                    if (original.Item != null)
                    {
                        additions?.Remove(original.Item);
                    }

                    additions ??= new List<LootItem>();
                    additions.Add(newItem.Item);
                }
                else if (LootType == LootType.Blacklist)
                {
                    LogDebug($"Replace item {original.ItemId} with {newItem.ItemId}");
                    Blacklist.Remove(original.ItemId);
                    if (newItem.ItemId != 0)
                    {
                        Blacklist.Add(newItem.ItemId);
                    }
                }
            }

            public LootItemCategory SwitchCategory(LootItemCategory lootItem, int categoryId)
            {
                var itemList = GetCategoryItems(lootItem.Category);
                var newItemList = GetCategoryItems(categoryId);
                if (itemList != null && newItemList != null)
                {
                    itemList.Remove(lootItem.Item);
                    newItemList.Add(lootItem.Item);

                    return new LootItemCategory(lootItem.Item, categoryId);
                }

                return lootItem;
            }

            public void RemoveItem(LootItemCategory lootItem)
            {
                if (LootType == LootType.Custom)
                {
                    var itemList = GetCategoryItems(lootItem.Category);
                    itemList?.Remove(lootItem.Item);
                }
                else if (LootType == LootType.Addition)
                {
                    RemoveAddition(lootItem.Item);
                }
                else if (LootType == LootType.Blacklist)
                {
                    Blacklist.Remove(lootItem.ItemId);
                }
            }

            #endregion

            public void Validate() => Validate(out _);
            
            private void Validate([CanBeNull] out string error)
            {
                error = null;
                if (!Flags.HasFlag(LootConfigFlags.CanChangeLootType))
                {
                    LootType = LootType.Custom;
                }

                if (Flags.HasFlag(LootConfigFlags.Dispenser))
                {
                    if (CategoryCount < 1)
                    {
                        AddCategory();
                    }
                    else if (CategoryCount > 1)
                    {
                        var list = Pool.Get<List<int>>();
                        list.AddRange(Categories.Keys.OrderBy(x => x).Skip(1));

                        foreach (var catId in list)
                        {
                            RemoveCategory(catId);
                        }

                        Pool.FreeUnmanaged(ref list);
                    }
                }
                else if (Flags.HasFlag(LootConfigFlags.DisableCategories) && CategoryCount > 0)
                {
                    LogDebug($"Remove categories of {Id} - disabled by flag");

                    var list = Pool.Get<List<int>>();
                    list.AddRange(Categories.Keys);
                    foreach (var catId in list)
                    {
                        RemoveCategory(catId);
                    }

                    Pool.FreeUnmanaged(ref list);
                }

                // Dispenser don't need min item count
                if (Flags.HasFlag(LootConfigFlags.Dispenser))
                {
                    return;
                }
                
                if (LootType == LootType.Custom && DefaultCategory.Count < 1)
                {
                    LogDebug($"Not enough custom items in {Id}");
                    Enabled = false;
                    error = "not_enough_items_custom";
                }
                else if (LootType == LootType.Addition && (additions == null || additions.Count < 1))
                {
                    LogDebug("Not enough additions");
                    Enabled = false;
                    error = "not_enough_items_addition";
                }
                else if (LootType == LootType.Blacklist && Blacklist.Count < 1)
                {
                    LogDebug("Not enough blacklist items");
                    Enabled = false;
                    error = "not_enough_items_blacklist";
                }
            }

            // Must not return void or proto serializer gets confused and calls this method
            public bool SetEnabled(bool enabled, out string error)
            {
                Enabled = enabled;
                if (Enabled)
                {
                    Validate(out error);
                    if (error != null)
                    {
                        return false;
                    }
                }

                error = String.Empty;
                return true;
            }

            public void CycleLootType()
            {
                if (!HasFlag(LootConfigFlags.CanChangeLootType))
                {
                    LootType = LootType.Custom;
                }
                else
                {
                    LootType = LootType switch
                    {
                        LootType.Custom => LootType.Addition,
                        LootType.Addition => LootType.Blacklist,
                        _ => LootType.Custom
                    };
                }

                Validate();
            }

            public float CalculateMean()
            {
                var mean = 0f;
                foreach (var item in DefaultCategory)
                {
                    mean += item.Chance * (item.Extra != null ? 2 : 1);
                }

                foreach (var cat in GetCategories())
                {
                    mean += cat.Chance * cat.Amount.Mean();
                }

                return mean;
            }

            public void SetFlag(LootConfigFlags flag, bool enabled)
            {
                Flags = enabled ? (Flags | flag) : (Flags & ~flag);
            }

            public bool HasFlag(LootConfigFlags flags)
            {
                return Flags.HasFlag(flags);
            }

            /// <summary>
            /// Creates a 1:1 copy of the config
            /// </summary>
            public LootConfig Copy()
            {
                using var stream = new MemoryStream();
                Serializer.Serialize(stream, this);
                stream.Position = 0;
                return Serializer.Deserialize<LootConfig>(stream);
            }

            /// <summary>
            /// Copies the config to another config, preserves Flags and Id. Use for pasting
            /// </summary>
            public void CopyTo(ref LootConfig other)
            {
                var copy = Copy();

                copy.Id = other.Id;
                copy.Flags = other.Flags;
                copy.Validate();

                other = copy;
            }

            public static LootConfig Create(ConfigId configId, LootConfigFlags flags = LootConfigFlags.None)
            {
                if (String.IsNullOrEmpty(configId.Key))
                {
                    throw new ArgumentException("Config id key not be null or empty");
                }

                return new LootConfig
                {
                    Id = configId,
                    Flags = flags
                };
            }
        }

        [ProtoContract]
        private record LootCategory : IRandom<bool>
        {
            [ProtoMember(1)]
            public int Id { get; init; }

            [ProtoMember(2)]
            public MinMax Amount { get; set; } = new(1);

            [ProtoMember(3)]
            public float Chance { get; set; }

            [ProtoMember(4)]
            public List<LootItem> Items { get; init; } = new();

            public bool Select()
            {
                return IRandom.Chance(Chance);
            }
        }

        [ProtoContract]
        private record LootItem : IRandom<bool>
        {
            [ProtoIgnore]
            public LootItemId Uid => new LootItemId(ItemId, SkinId, IsBlueprint);

            [ProtoMember(1)]
            public int ItemId { get; set; }

            [ProtoMember(2)]
            public ulong SkinId { get; set; }

            [ProtoMember(3)] 
            [CanBeNull] 
            public string CustomName { get; set; }

            [ProtoMember(4)]
            public MinMax Amount { get; set; } = new(1);

            [ProtoMember(5)]
            public float Chance { get; set; }

            [ProtoMember(6)]
            public bool IsBlueprint { get; set; }

            [ProtoMember(7)]
            public MinMaxFloat Condition { get; set; } = new(1);

            [ProtoMember(8)]
            public LootItem Extra { get; set; }

            public ItemDefinition ItemDefinition => ItemManager.FindItemDefinition(ItemId);

            public bool HasCondition => ItemDefinition?.condition.enabled ?? false;

            public int Count => Extra != null ? 2 : 1;

            public LootItem Copy()
            {
                return this with { Extra = Extra?.Copy() };
            }

            public bool Select()
            {
                return UnityEngine.Random.Range(0f, 1f) <= Chance;
            }

            public override string ToString()
            {
                return $"{ItemDefinition?.shortname ?? ItemId.ToString()}:{SkinId} x{Amount} {Chance * 100:N2}%";
            }

            public static LootItem Create(int itemId, MinMax amount, float chance = 1f, MinMaxFloat condition = default)
            {
                return new LootItem
                {
                    ItemId = itemId,
                    Amount = amount,
                    Chance = chance,
                    Condition = condition == default ? new MinMaxFloat(1) : condition,
                };
            }
        }

        private static class LootConfigHelper
        {
            public static void Clear(LootConfig config)
            {
                foreach (var catId in config.GetCategories().Select(x => x.Id).ToArray())
                {
                    config.RemoveCategory(catId);
                }
                config.DefaultCategory.Clear();
            }

            public static void AddItems(LootConfig config, LootSpawn def, float chanceMultiplier = 1f)
            {
                if (def.subSpawn != null && def.subSpawn.Length != 0)
                {
                    var totalWheight = def.subSpawn.Sum(x => x.weight);
                    LogDebug($"total weight {totalWheight}");
                    foreach (var sb in def.subSpawn)
                    {
                        if (sb.category != null)
                        {
                            AddItems(config, sb.category, ((float)sb.weight / totalWheight) * chanceMultiplier);
                        }
                    }
                }
                else
                {
                    if (def.items == null)
                    {
                        return;
                    }

                    var count = def.items.Count(x => x != null);
                    foreach (var item in def.items)
                    {
                        var chance = (float)Math.Round((1f / count) * chanceMultiplier, 3);
                        AddItem(config, item, chance);
                    }
                }
            }

            public static void AddItem(LootConfig config, ItemAmount itemAmount, float chance = 1f)
            {
                if (itemAmount == null)
                {
                    return;
                }

                var item = Createtem(itemAmount, chance);
                var existing = config.DefaultCategory.FirstOrDefault(x => x.ItemId == item.ItemId);
                if (existing != null)
                {
                    existing.Amount = new MinMax(Mathf.Min(existing.Amount.Min, item.Amount.Min), existing.Amount.Max + item.Amount.Max);
                    existing.Chance += item.Chance;
                }
                else
                {
                    config.DefaultCategory.Add(item);
                }
            }

            public static LootItem Createtem(ItemAmount itemAmount, float chance = 1f)
            {
                if (itemAmount == null)
                {
                    return null;
                }

                var amountMin = Mathf.Max(Mathf.RoundToInt(itemAmount.startAmount), 1);
                var amountMax = amountMin;

                if (itemAmount is ItemAmountRanged ranged)
                {
                    amountMax = Mathf.Max(Mathf.RoundToInt(ranged.maxAmount), 1);
                }

                return new LootItem
                {
                    ItemId = itemAmount.itemid,
                    Amount = new MinMax(amountMin, amountMax),
                    Condition = new MinMaxFloat(itemAmount.itemDef.condition.foundCondition.fractionMin, itemAmount.itemDef.condition.foundCondition.fractionMax),
                    IsBlueprint = itemAmount.itemDef.spawnAsBlueprint,
                    Chance = chance
                };
            }
        }

        private readonly record struct LootItemId(int ItemId, ulong SkinId, bool IsBlueprint);

        private enum LootType : byte
        {
            Custom,
            Blacklist,
            Addition
        }

        [Flags]
        private enum LootConfigFlags : ushort
        {
            None = 0,

            Plugin = 2,

            Npc = 1,
            Collectible = 4,
            Unwrapable = 8,
            Quarry = 16,
            TrainWagon = 64,
            Dispenser = 256,

            CanChangeLootType = 32,
            DisableCategories = 128,
        }

        private readonly record struct LootItemCategory
        {
            public ItemDefinition ItemDefinition => Item?.ItemDefinition ?? ItemManager.FindItemDefinition(ItemId);

            public readonly int ItemId { get; init; }
            public readonly LootItem Item { get; init; }
            public readonly int Category { get; init; }

            public LootItemCategory(int itemId)
            {
                ItemId = itemId;
            }

            public LootItemCategory(LootItem item, int category)
            {
                Item = item;
                Category = category;
            }

            public LootItemCategory Copy()
            {
                return new LootItemCategory(Item.Copy(), Category);
            }
        }

        #endregion

        #region Utility classes

        //[ProtoContract]
        //private struct Color
        //{
        //    [ProtoMember(1)]
        //    public float R { get; set; }

        //    [ProtoMember(2)]
        //    public float G { get; set; }

        //    [ProtoMember(3)]
        //    public float B { get; set; }

        //    public Color(float r, float g, float b)
        //    {
        //        R = r;
        //        G = g;
        //        B = b;
        //    }
        //}

        [ProtoContract]
        private readonly record struct MinMax : IRandom<int>
        {
            [ProtoMember(1)]
            public int Min { get; init; }

            [ProtoMember(2)]
            public int Max { get; init; }

            public readonly bool IsFixed => Min == Max;

            public MinMax(int value)
            {
                Min = Max = value;
            }

            public MinMax(int min, int max)
            {
                if (min > max)
                {
                    Min = max;
                    Max = min;
                }
                else
                {
                    Min = min;
                    Max = max;
                }
            }

            public readonly float Mean()
            {
                return (Max - Min) / 2f;
            }

            public readonly int Select()
            {
                return IRandom.Range(Min, Max);
            }

            public readonly MinMax Multiply(float multiplier)
            {
                return new MinMax(Mathf.RoundToInt(multiplier * Min), Mathf.RoundToInt(multiplier * Max));
            }

            public readonly override string ToString()
            {
                return IsFixed ? Min.ToString() : $"{Min}-{Max}";
            }

            public static bool TryParse(string str, out MinMax result)
            {
                var parts = str.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1 && Int32.TryParse(parts[0], out var min))
                {
                    result = new MinMax(min);
                    return true;
                }
                if (parts.Length == 2 && Int32.TryParse(parts[0], out min) && Int32.TryParse(parts[1], out var max))
                {
                    result = new MinMax(min, max);
                    return true;
                }

                result = default;
                return false;
            }
        }

        [ProtoContract]
        private readonly record struct MinMaxFloat : IRandom<float>
        {
            [ProtoMember(1)]
            public float Min { get; init; }

            [ProtoMember(2)]
            public float Max { get; init; }

            public readonly bool IsFixed => Mathf.Approximately(Min, Max);

            public MinMaxFloat(float value)
            {
                Min = Max = value;
            }

            public MinMaxFloat(float min, float max)
            {
                if (min > max)
                {
                    Min = max;
                    Max = min;
                }
                else
                {
                    Min = min;
                    Max = max;
                }
            }

            public readonly float Select()
            {
                return IRandom.Range(Min, Max);
            }

            public readonly override string ToString()
            {
                return IsFixed ? Min.ToString(CultureInfo.InvariantCulture) : $"{Min}-{Max}";
            }

            public readonly string ToString(string format)
            {
                return IsFixed ? Min.ToString(CultureInfo.InvariantCulture) : $"{Min.ToString(format)}-{Max.ToString(format)}";
            }

            public static bool TryParse(string str, out MinMaxFloat result)
            {
                var parts = str.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1 && Single.TryParse(parts[0], out var min))
                {
                    result = new MinMaxFloat(min);
                    return true;
                }
                if (parts.Length == 2 && Single.TryParse(parts[0], out min) && Single.TryParse(parts[1], out var max))
                {
                    result = new MinMaxFloat(min, max);
                    return true;
                }

                result = default;
                return false;
            }

            public static MinMaxFloat Clamp01(MinMaxFloat value)
            {
                var min = value.Min >= 0 ? value.Min > 1 ? 1 : value.Min : 0;
                var max = value.Max >= 0 ? value.Max > 1 ? 1 : value.Max : 0;
                return new MinMaxFloat(min, max);
            }

            public static MinMaxFloat operator *(MinMaxFloat lhs, float rhs)
            {
                return new MinMaxFloat(lhs.Min * rhs, lhs.Max * rhs);
            }

            public static MinMaxFloat operator *(float lhs, MinMaxFloat rhs)
            {
                return rhs * lhs;
            }
        }

        [ProtoContract]
        readonly struct ConfigId : IEquatable<ConfigId>
        {
            public static readonly ConfigId Invalid = default;

            public bool IsValid => Key != null && Key.Length > 0;

            [ProtoMember(1)]
            public string Plugin { get; init; }
            [ProtoMember(2)]
            public string Key { get; init; }

            public ConfigId(string key)
            {
                Plugin = null;
                Key = key;
            }

            public ConfigId(string plugin, string key)
            {
                Plugin = plugin;
                Key = key;
            }

            public ConfigId(Plugin plugin, string key)
            {
                Plugin = plugin.Name;
                Key = key;
            }

            public override bool Equals(object obj)
            {
                return obj is ConfigId id && Equals(id);
            }

            public bool Equals(ConfigId other)
            {
                return Plugin == other.Plugin &&
                       Key == other.Key;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Plugin, Key);
            }

            public override string ToString()
            {
                return $"{Plugin}_{Key}";
            }

            public static bool operator ==(ConfigId left, ConfigId right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(ConfigId left, ConfigId right)
            {
                return !(left == right);
            }
        }

        private class LootConfigStats
        {
            public int TotalCount { get; private set; }
            public int BelowMinCount { get; private set; }
            public int AboveMaxCount { get; private set; }

            public int TotalItemCount { get; private set; }
            public float AvgItemCount => (float)TotalItemCount / TotalCount;

            public void Track(LootConfig config, int itemCount)
            {
                TotalCount++;
                TotalItemCount++;
                if (itemCount < config.Amount.Min)
                {
                    BelowMinCount++;
                }
                else if (itemCount > config.Amount.Max)
                {
                    AboveMaxCount++;
                }
            }

            public void Reset()
            {
                TotalCount = 0;
                TotalItemCount = 0;
                AboveMaxCount = 0;
                BelowMinCount = 0;
            }
        }

        interface IRandom<out T>
        {
            T Select();
        }

        interface IRandom
        {
            public static bool Chance(float chance)
            {
                return UnityEngine.Random.Range(0f, 1f) <= chance;
            }

            public static int Range(int minIncl, int maxIncl)
            {
                return UnityEngine.Random.Range(minIncl, maxIncl + 1);
            }

            public static float Range(float minIncl, float maxIncl)
            {
                return UnityEngine.Random.Range(minIncl, maxIncl);
            }
        }

        #endregion

        #region Custom Items

        #region Custom Item API

        [PublicAPI]
        private void ClearCustomItems(Plugin plugin)
        {
            manager.CustomItems.RemoveCustomItems(plugin, true);
        }

        [PublicAPI]
        private void AddCustomItem(Plugin plugin, int itemId, ulong skinId, string customName = null, bool persistent = false)
        {
            manager.CustomItems.AddCustomItem(new CustomItemDefinition { Plugin = plugin.Name, ItemId = itemId, SkinId = skinId, CustomName = customName, Persistent = persistent });
        }

        #endregion

        class CustomItemManager
        {
            public const string RECENT_TAB = "recent";
            public const string CUSTOM_TAB = "custom";
            public const string SEARCH_TAB = "search";

            private readonly List<BaseItemDefinition> allItems = new();
            private readonly LinkedList<BaseItemDefinition> recentItems = new();
            private readonly Dictionary<string, BaseItemDefinition[]> vanillaTabs = new();
            private readonly List<CustomItemDefinition> customItems = new();
            private readonly List<BaseItemDefinition> search = new();

            public void Init()
            {
                var tabs = new Dictionary<string, LinkedList<BaseItemDefinition>>();

                foreach (var item in ItemManager.GetItemDefinitions())
                {
                    if (item.itemType == ItemContainer.ContentsType.Liquid)
                    {
                        continue;
                    }
                    
                    var def = new BaseItemDefinition
                    {
                        ItemId = item.itemid
                    };
                    allItems.Add(def);
                    
                    if (item.hidden)
                    {
                        continue;
                    }

                    var cat = item.category.ToString();
                    if (!tabs.TryGetValue(cat, out var defs))
                    {
                        defs = new LinkedList<BaseItemDefinition>();
                        tabs.Add(cat, defs);
                    }
                    
                    defs.AddLast(def);
                }

                foreach (var kv in tabs)
                {
                    vanillaTabs.Add(kv.Key, kv.Value.ToArray());
                }

                var save = ReadDataFile<CustomItemSave>("items", CUSTOM_TAB);
                if (save?.Items != null)
                {
                    if (DateTime.UtcNow.Subtract(save.Time).TotalMinutes < 10)
                    {
                        LogDebug("Load temp & persistent custom items");
                        customItems.AddRange(save.Items);
                    }
                    else
                    {
                        LogDebug("Load persistent custom items");
                        customItems.AddRange(save.Items.Where(x => x.Persistent));
                    }
                }

                var recent = ReadDataFile<RecentItemSave>("items", RECENT_TAB);
                if (recent?.Items != null)
                {
                    foreach (var item in recent.Items)
                    {
                        recentItems.AddLast(item);
                    }
                }
            }

            public void Save()
            {
                var save = new CustomItemSave
                {
                    Items = customItems,
                    Time = DateTime.UtcNow
                };
                WriteDataFile(save, "items", CUSTOM_TAB);

                var recent = new RecentItemSave
                {
                    Items = recentItems.ToList()
                };
                WriteDataFile(recent, "items", RECENT_TAB);
            }

            public IEnumerable<string> GetTabs()
            {
                yield return RECENT_TAB;
                yield return CUSTOM_TAB;

                foreach (var tab in vanillaTabs.Keys)
                {
                    yield return tab;
                }
            }

            public void Search(string searchStr)
            {
                search.Clear();
                
                var searchHidden = searchStr.StartsWith("h:", StringComparison.OrdinalIgnoreCase);
                if (searchHidden)
                {
                    searchStr = searchStr.Substring(2);
                }

                AddItems(x => x.StartsWith(searchStr, StringComparison.OrdinalIgnoreCase));

                if (search.Count < 20)
                {
                    AddItems(x => x.Contains(searchStr, StringComparison.OrdinalIgnoreCase));
                }
                
                void AddItems(Func<string, bool> selector)
                {
                    foreach (var item in allItems)
                    {
                        var def = item.GetItemDefinition();
                        if (def == null || (def.hidden && !searchHidden) || def.itemType == ItemContainer.ContentsType.Liquid)
                        {
                            continue;
                        }

                        if (selector.Invoke(def.displayName.english))
                        {
                            if (search.Find(x => x.ItemId == item.ItemId && !x.IsCustom) == null)
                            {
                                search.Add(item);
                            }
                        }
                    }
                
                    foreach (var customItem in customItems)
                    {
                        if (customItem.CustomName != null && selector.Invoke(customItem.CustomName))
                        {
                            search.Add(customItem);
                        }
                    }
                }
                
                static int GetMatchingChars(string str1, string str2)
                {
                    var count = 0;
                    for (int i = 0; i < Math.Min(str1.Length, str2.Length); i++)
                    {
                        if (Char.ToLower(str1[i]) != Char.ToLower(str2[i]))
                        {
                            break;
                        }

                        count++;
                    }

                    return count;
                }
            }

            public IEnumerable<BaseItemDefinition> GetItemsForTab(string tabName, out int count)
            {
                if (tabName == SEARCH_TAB)
                {
                    count = search.Count;
                    return search;
                }
                else if (tabName == RECENT_TAB)
                {
                    count = recentItems.Count;
                    return recentItems;
                }
                else if (tabName == CUSTOM_TAB)
                {
                    return GetCustomItems(out count);
                }
                else if (vanillaTabs.TryGetValue(tabName, out var itemDefs))
                {
                    count = itemDefs.Length;
                    return itemDefs;
                }

                count = 0;
                return Enumerable.Empty<BaseItemDefinition>();
            }

            public void SetRecentItem(BaseItemDefinition item, int maxCount)
            {
                if (recentItems.Count >= maxCount)
                {
                    recentItems.RemoveLast();
                }

                if (!recentItems.Any(x => x.ValueEquals(item)))
                {
                    recentItems.AddFirst(item);
                }
            }

            public void AddCustomItem(CustomItemDefinition item)
            {
                if (customItems.RemoveAll(x => x.ValueEquals(item)) > 0)
                {
                    LogWarning($"Plugin {item.Plugin} is overwriting an existing custom item with skin id {item.SkinId}. Consider removing custom items before re-registering them");
                }

                customItems.Add(item);
                LogDebug($"{item.Plugin} added custom item {item.CustomName} persistent: {item.Persistent}");
            }

            public void RemoveCustomItems(Plugin plugin, bool removePersistent)
            {
                LogDebug($"Remove custom items of {plugin.Name} includePersistent: {removePersistent}");
                customItems.RemoveAll(x => x.Plugin == plugin.Name && (removePersistent || !x.Persistent));
            }

            public IEnumerable<CustomItemDefinition> GetCustomItems(out int count)
            {
                count = customItems.Count;
                return customItems.OrderBy(x => x.Plugin == Instance.Name ? "AAAAAAAAAAAAA" : x.Plugin);
            }

            public void UpdateCustomItem(CustomItemDefinition original, CustomItemDefinition replace)
            {
                if (replace == null)
                {
                    customItems.Remove(original);
                    return;
                }

                int idx = customItems.IndexOf(original);
                if (idx >= 0)
                {
                    customItems[idx] = replace;
                }
                else
                {
                    customItems.Add(replace);
                }
            }
        }

        [ProtoContract]
        class RecentItemSave
        {
            [ProtoMember(1)]
            public List<BaseItemDefinition> Items { get; init; }
        }

        [ProtoContract]
        class CustomItemSave
        {
            [ProtoMember(1)]
            public List<CustomItemDefinition> Items { get; init; }

            [ProtoMember(2)]
            public DateTime Time { get; init; }
        }

        [ProtoContract]
        record CustomItemDefinition : BaseItemDefinition
        {
            [ProtoIgnore]
            public override bool IsCustom => true;

            [ProtoIgnore]
            public string Shortname => ItemManager.FindItemDefinition(ItemId).shortname;

            [ProtoMember(1)]
            public string Plugin { get; init; }

            [ProtoMember(2)]
            public bool Persistent { get; init; }

            public override string GetPlugin() => Plugin;

            public bool ValueEquals(CustomItemDefinition other)
            {
                return base.ValueEquals(other) && Plugin == other.Plugin;
            }

            //public CustomItemDefinition Clone()
            //{
            //    return new CustomItemDefinition
            //    {
            //        ItemId = ItemId,
            //        SkinId = SkinId,
            //        CustomName = CustomName,
            //        Plugin = Plugin,
            //        Persistent = Persistent
            //    };
            //}

            public static CustomItemDefinition Create(int itemId, ulong skinId, string customName)
            {
                return new CustomItemDefinition
                {
                    CustomName = customName,
                    ItemId = itemId,
                    SkinId = skinId,
                    Persistent = true,
                    Plugin = Instance.Name
                };
            }
        }

        [ProtoContract]
        [ProtoInclude(20, typeof(CustomItemDefinition))]
        record BaseItemDefinition
        {
            [ProtoIgnore]
            public virtual bool IsCustom => false;

            [ProtoMember(1)]
            public int ItemId { get; init; }

            [ProtoMember(2)]
            public ulong SkinId { get; init; }

            [ProtoMember(3)] 
            [CanBeNull] 
            public string CustomName { get; init; }

            public virtual string GetPlugin() => null;

            public string GetDisplayName(Lang lang, BasePlayer player)
            {
                var language = lang?.GetLanguage(player) ?? "en";
                return CustomName ?? Instance?.GetItemNameTranslated(ItemId, language) ?? ItemManager.FindItemDefinition(ItemId).displayName.english;
            }

            public bool ValueEquals(BaseItemDefinition other)
            {
                return ItemId == other.ItemId && SkinId == other.SkinId && CustomName == other.CustomName;
            }

            [CanBeNull]
            public ItemDefinition GetItemDefinition()
            {
                return ItemManager.FindItemDefinition(ItemId);
            }
        }

        #endregion

        #region Config

        public class Configuration
        {
            // [JsonProperty("Enable lazy loading of loot configs")]
            // public bool lazyLoading = true;

            [JsonProperty("Cover up free slots in containers")]
            public bool CoverSlots { get; set; } = true;

            [JsonProperty("Loot generator options")]
            public LootGeneratorOptions GeneratorOptions { get; set; } = new()
            {
                MaxIterations = 50
            };

            public class LootGeneratorOptions
            {
                [JsonProperty("Maximum amount of iterations - higher values may lead to degraded performance, but more accurate results")]
                public int MaxIterations { get; set; }
            }
        }

        #endregion

        #region Data

        [ProtoContract]
        private class State
        {
            public bool IsFreshInstall { get; private set; }
            public bool IsUpdated { get; private set; }

            [ProtoMember(1)]
            private ProtoVersionNumber LastVersion { get; set; } = new();

            [ProtoMember(2)]
            public bool LegacyImported { get; set; }
            
            [ProtoMember(3)]
            public bool AutoLootRefresh { get; set; }

            public void Update(VersionNumber currentVersion)
            {
                if (LastVersion.IsDefault)
                {
                    IsFreshInstall = true;
                }
                if (LastVersion.IsOlderThan(currentVersion))
                {
                    IsUpdated = true;
                }

                LastVersion = new ProtoVersionNumber(currentVersion);
            }

            public void Save()
            {
                WriteDataFile(this, "state");
            }

            public static State Load()
            {
                var state = ReadDataFile<State>("state");
                state ??= new State();
                return state;
            }

            [ProtoContract]
            private class ProtoVersionNumber
            {
                public bool IsDefault => Major == 0 && Minor == 0 && Patch == 0;

                [ProtoMember(1)]
                public int Major { get; set; }

                [ProtoMember(2)]
                public int Minor { get; set; }

                [ProtoMember(3)]
                public int Patch { get; set; }

                public ProtoVersionNumber() { }

                public ProtoVersionNumber(VersionNumber version)
                {
                    Major = version.Major;
                    Minor = version.Minor;
                    Patch = version.Patch;
                }

                public bool IsOlderThan(VersionNumber version)
                {
                    return version.Major > Major || version.Minor > Minor || version.Patch > Patch;
                }
            }
        }

        
        [CanBeNull]
        private static TProto ReadDataFile<TProto>(string folder, string name) where TProto : class
        {
            return ReadDataFile<TProto>(Path.Combine(folder, name));
        }

        [CanBeNull]
        private static TProto ReadDataFile<TProto>(string name) where TProto : class
        {
            var path = GetFullPath(name);
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return Serializer.Deserialize<TProto>(stream);
        }

        private static void WriteDataFile<TProto>(TProto data, string folder, string name) => WriteDataFile(data, Path.Combine(folder, name));
        private static void WriteDataFile<TProto>(TProto data, string name)
        {
            var path = GetFullPath(name);

            using var stream = File.Open(path, FileMode.Create);
            Serializer.Serialize(stream, data);
        }

        private static string GetFullPath(string name)
        {
            if (!name.EndsWith(".pb"))
            {
                name += ".pb";
            }

            var fullPath = Path.Combine(Interface.Oxide.DataDirectory, DATA_FOLDER, name);

            var dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }

            return fullPath;
        }

        #endregion
        
        #region Other Config

        private class OtherConfigManager
        {
            public bool IsLoaded { get; private set; }
            
            public SupplyDropConfiguration SupplyDrop { get; private set; }

            public SmeltingConfiguration Smelting { get; private set; }

            public void Load()
            {
                SupplyDrop = ReadDataFile<SupplyDropConfiguration>("configuration", "supply_drop.pb");
                SupplyDrop ??= new SupplyDropConfiguration();
                
                Smelting = ReadDataFile<SmeltingConfiguration>("configuration", "smelting.pb");
                Smelting ??= new SmeltingConfiguration();
                
                IsLoaded = true;
            }

            public void Initialize()
            {
                if (IsLoaded)
                {
                    Smelting.UpdateBurnable();
                    Smelting.InitializeFurnaces();
                }
                else
                {
                    LogError("Failed to initialize furnaces - Configuration not loaded");
                }
            }

            public void Save()
            {
                if (SupplyDrop != null)
                {
                    WriteDataFile(SupplyDrop, "configuration", "supply_drop.pb");
                }
                if (Smelting != null)
                {
                    WriteDataFile(Smelting, "configuration", "smelting.pb");
                }
            }
        }
        
        [ProtoContract]
        private class SmeltingConfiguration
        {
            private const float RECYCLER_TICK_RATE = 5f;
            private const float SMELTING_TICK_RATE = 0.5f;

            [ProtoMember(1)]
            public bool Enabled { get; set; }
            [ProtoMember(3)]
            public float SmallFurnaceSpeed { get; set; } = 1f;
            [ProtoMember(4)]
            public float LargeFurnaceSpeed { get; set; } = 1f;
            [ProtoMember(5)]
            public float RefinerySpeed { get; set; } = 1f;
            [ProtoMember(6)]
            public float CampfireSpeed { get; set; } = 1f;
            [ProtoMember(7)]
            public float BBQSpeed { get; set; } = 1f;
            [ProtoMember(8)]
            public float ElectricFurnaceSpeed { get; set; } = 1f;

            [ProtoMember(2)]
            public bool RecyclerEnabled { get; set; } = false;
            [ProtoMember(9)]
            public float RecyclerSpeed { get; set; } = 1f;
            [ProtoMember(13)]
            public float RecyclerEfficiency { get; set; } = 0.6f;
            [ProtoMember(14)]
            public float RecyclerSafeSpeed { get; set; } = 1f;
            [ProtoMember(15)]
            public float RecyclerSafeEfficiency { get; set; } = 0.4f;

            [ProtoMember(10)]
            public float MixingSpeedMultiplier { get; set; } = 1f;

            [ProtoMember(11)]
            public float CharcoalChance { get; set; } = 0.75f;
            [ProtoMember(12)]
            public int CharcoalAmount { get; set; } = 1;

            public static void ResetBurnable()
            {
                var burnable = ItemManager.FindItemDefinition(-151838493)?.GetComponent<ItemModBurnable>();
                if (burnable == null)
                {
                    LogError("Failed to find burnable item. Smelting configuration might not work as expected");
                    return;
                }
                
                burnable.byproductChance = 0.25f;
                burnable.byproductAmount = 1;
            }

            public void UpdateBurnable()
            {
                if (!Enabled)
                {
                    return;
                }

                var burnable = ItemManager.FindItemDefinition(-151838493)?.GetComponent<ItemModBurnable>();
                if (burnable == null)
                {
                    LogError("Failed to find burnable item. Smelting configuration might not work as expected");
                    return;
                }
                
                // Invert chance due to a bug in ConsumeFuel:
                // UnityEngine.Random.Range(0f, 1f) > burnable.byproductChance
                burnable.byproductChance = 1f - CharcoalChance;

                burnable.byproductAmount = CharcoalAmount;
            }

            public void InitializeFurnaces()
            {
                foreach (var oven in BaseNetworkable.serverEntities.OfType<BaseOven>())
                {
                    UpdateOvenSpeed(oven);
                }
            }

            public void ToggleMixingTable(MixingTable table)
            {
                if (!Enabled || table.IsOn())
                {
                    return;
                }

                Instance.NextTick(() =>
                {
                    table.RemainingMixTime /= MixingSpeedMultiplier;
                    table.TotalMixTime /= MixingSpeedMultiplier;
                    table.SendNetworkUpdateImmediate();

                    if (table.RemainingMixTime < 1f)
                    {
                        table.CancelInvoke(table.TickMix);
                        table.Invoke(table.TickMix, table.RemainingMixTime);
                    }
                });
            }

            public void ToggleRecycler(Recycler recycler)
            {
                if (!RecyclerEnabled || recycler.IsOn())
                {
                    return;
                }

                var speedMultiplier = recycler.IsSafezoneRecycler() ? RecyclerSafeSpeed : RecyclerSpeed;
                var recyclerTickTime = RECYCLER_TICK_RATE / speedMultiplier;

                recycler.CancelInvoke(recycler.RecycleThink);
                
                recycler.radtownRecycleEfficiency = RecyclerEfficiency;
                recycler.safezoneRecycleEfficiency = RecyclerSafeEfficiency;

                Instance.NextTick(() =>
                {
                    recycler.InvokeRepeating(recycler.RecycleThink, recyclerTickTime, recyclerTickTime);
                });
            }

            public void UpdateOvenSpeed(BaseOven oven)
            {
                if (!oven.IsOn())
                {
                    return;
                }

                var speed = GetFurnaceSpeed(oven);
                if (!speed.HasValue)
                {
                    return;
                }

                float tickTime = SMELTING_TICK_RATE / speed.Value;
                Instance.NextTick(() =>
                {
                    // Oven go brrrrrrrrrrr
                    oven.CancelInvoke(oven.Cook);
                    oven.InvokeRepeating(oven.Cook, tickTime, tickTime);
                });
            }

            private float? GetFurnaceSpeed(BaseOven oven)
            {
                return oven.ShortPrefabName switch
                {
                    "small_refinery_static" or "refinery_small_deployed" => RefinerySpeed,
                    "furnace" or "legacy_furnace" => SmallFurnaceSpeed,
                    "furnace.large" => LargeFurnaceSpeed,
                    "campfire" => CampfireSpeed,
                    "bbq.deployed" or "bbq.static" => BBQSpeed,
                    "electricfurnace.deployed" or "electricfurnace" => ElectricFurnaceSpeed,
                    _ => null
                };
            }
        }
        
        [ProtoContract]
        private class SupplyDropConfiguration
        {
            private const string SMOKE_EFFECT = "assets/bundled/prefabs/fx/smoke_signal_full.prefab";
            private const string PLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
            private const string DROP_PREFAB = "assets/prefabs/misc/supply drop/supply_drop.prefab";

            [ProtoMember(1)]
            public bool Enabled { get; set; }
            [ProtoMember(2)]
            public float PlaneSpeed { get; set; } = 1f;
            [ProtoMember(3)]
            public int PlaneHeight { get; set; } = 0;
            [ProtoMember(4)]
            public float SmokeDuration { get; set; } = 60f;
            [ProtoMember(5)]
            public bool ExactDropPosition { get; set; } = false;
            [ProtoMember(6)]
            public float DropFallSpeed { get; set; } = 2f;
            [ProtoMember(7)]
            public bool DropFallSmoke { get; set; } = false;
            [ProtoMember(8)]
            public float RandomPosTolerance { get; set; } = 20f;
            [ProtoMember(9)]
            public bool CooldownEnabled { get; set; }
            [ProtoMember(10)]
            public int CooldownSeconds { get; set; } = 60;
            
            private readonly HashSet<NetworkableId> signals = new();
            
            public void DeliverCustomSupplyDrop(SupplySignal signal)
            {
                var netId = signal.net.ID;
                if (!signals.Add(netId))
                {
                    return;
                }
                
                LogDebug("Serve custom airdrop");

                Instance.timer.In(3f, () =>
                {
                    signal.CancelInvoke(signal.Explode);
                });

                Instance.timer.In(3.3f, () =>
                {
                    signals.Remove(netId);

                    if (signal == null)
                    {
                        LogError("Failed to spawn cargo plane: supply signal is null");
                        return;
                    }

                    signal.Invoke(signal.FinishUp, SmokeDuration);

                    signal.SetFlag(BaseEntity.Flags.On, true, false);
                    signal.SendNetworkUpdateImmediate();

                    var dropPos = signal.transform.position;
                    if (!ExactDropPosition)
                    {
                        dropPos.x += UnityEngine.Random.Range(-RandomPosTolerance, RandomPosTolerance);
                        dropPos.z += UnityEngine.Random.Range(-RandomPosTolerance, RandomPosTolerance);
                    }

                    var plane = EntityTools.CreateEntity<CargoPlane>(PLANE_PREFAB);

                    Interface.CallHook("OnCargoPlaneSignaled", plane, signal);

                    InitPlane(plane, dropPos);
                });
            }

            private void InitPlane(CargoPlane plane, Vector3? dropPos = null)
            {
                float y = TerrainMeta.HighestPoint.y + PlaneHeight;

                var dropController = plane.gameObject.AddComponent<DropController>();
                dropController.config = this;
                
                plane.dropped = true;
                if (dropPos.HasValue)
                {
                    plane.InitDropPosition(dropPos.Value);
                }

                plane.Spawn();

                plane.startPos.y = y;
                plane.endPos.y = y;
                plane.secondsToTake /= PlaneSpeed;

                dropController.SetRunningIn(1f);
            }

            public class DropController : FacepunchBehaviour
            {
                private CargoPlane plane;

                private float lastDist;
                private bool dropped;
                private bool running;

                public SupplyDropConfiguration config;

                void Awake()
                {
                    plane = GetComponent<CargoPlane>();
                    lastDist = 0;
                    dropped = false;
                    running = false;
                }

                void Update()
                {
                    if (dropped || !running)
                    {
                        return;
                    }

                    var pPos = new Vector2(plane.transform.position.x, plane.transform.position.z);
                    var dPos = new Vector2(plane.dropPosition.x, plane.dropPosition.z);
                    var dist = Vector2.Distance(pPos, dPos);

                    if ((dist > lastDist && lastDist > 0) || dist < 2f)
                    {
                        dropped = true;
                        var dropPos = plane.dropPosition;
                        dropPos.y = plane.transform.position.y-2f;
                        Drop(dropPos, plane.transform.rotation);
                    }
                    
                    lastDist = dist;
                }

                void Drop(Vector3 pos, Quaternion rot)
                {
                    var drop = EntityTools.CreateEntity<SupplyDrop>(DROP_PREFAB, pos, rot, true);
                    
                    drop.GetComponent<Rigidbody>().drag = config.DropFallSpeed; // default 2, lowest 0.6

                    drop.Spawn();

                    Interface.CallHook("OnSupplyDropDropped", drop, plane);

                    if (config.DropFallSmoke)
                    {
                        Effect.server.Run(SMOKE_EFFECT, drop, 0, Vector3.zero, Vector3.up, null, true);
                    }
                }

                public void SetRunningIn(float seconds)
                {
                    Invoke(() =>
                    {
                        running = true;
                    }, seconds);   
                }
            }
        }
        
        #endregion

        #region Stack Size

        class StackSizeController
        {
            private static readonly int[] UnhideItems = new int[]
            {
                1036321299, 1223900335, -602717596, // Dog tags
                1364514421, -455286320, 1762167092, 1223729384, 1572152877, -282193997, 180752235, -1386082991, 70102328, 22947882, 81423963, // Id tags
                -560304835, 1081921512, -25740268, -1078639462, -1073015016 // Space suit, card table, skull spikes
            };

            public static bool IsHidden(ItemDefinition itemDef)
            {
                return itemDef.itemType == ItemContainer.ContentsType.Liquid || (itemDef.hidden && !UnhideItems.Contains(itemDef.itemid));
            }

            public bool LimitHotBarStackSize => data?.LimitHotBarStacks ?? false;
            public bool Enabled => data?.Enabled ?? false;
            public bool Initialized { get; private set; }
            public bool UsesPrefabStackSize => prefabEntries.Count > 0;

            private readonly Dictionary<uint, StackSizeDataEntry> prefabEntries = new();

            private readonly Dictionary<int, int> vanillaStackSize = new();

            private StackSizeData data;

            public void Load()
            {
                data = ReadDataFile<StackSizeData>("stacksize", "stacksize");
                data ??= new StackSizeData();
            }
            
            public void Initialize()
            {
                // Init prefab entry dict
                UpdateEntries();

                // Init vanilla stack size
                LoadVanillaStackSizes();

                Initialized = true;

                ApplyStackSize();
            }

            public void Unload()
            {
                if (Initialized)
                {
                    RevertStackSize();
                }
            }

            public void Save()
            {
                if (Initialized)
                {
                    SaveOnly();
                    RevertStackSize();
                    UpdateEntries();
                    ApplyStackSize();
                }
            }

            private void SaveOnly()
            {
                if (data != null)
                {
                    WriteDataFile(data, "stacksize", "stacksize");
                }
            }

            private void UpdateEntries()
            {
                prefabEntries.Clear();
                foreach (var entry in data.Entries)
                {
                    if (!entry.Enabled)
                    {
                        continue;
                    }

                    foreach (var prefab in entry.Prefabs)
                    {
                        prefabEntries[prefab] = entry;
                    }
                }
            }

            public int GetStackSize(ItemDefinition itemDef, uint prefabId = 0)
            {
                // Item is not handled by stack size controller
                if (!vanillaStackSize.TryGetValue(itemDef.itemid, out var vs))
                {
                    return -1;
                }

                if (prefabId != 0 && prefabEntries.TryGetValue(prefabId, out var entry))
                {
                    return entry.GetStackSize(vs, itemDef);
                }
                else
                {
                    return data.DefaultEntry.GetStackSize(vs, itemDef);
                }
            }

            #region UI

            public StackSizeDataEntry GetDefaultEntry() => data.DefaultEntry;

            public IEnumerable<StackSizeDataEntry> GetEntries()
            {
                yield return data.DefaultEntry;

                foreach (var entry in data.Entries)
                {
                    yield return entry;
                }
            }

            public StackSizeDataEntry CreateEntry()
            {
                var entry = new StackSizeDataEntry();
                data.Entries.Add(entry);
                return entry;
            }

            public void DeleteEntry(ref StackSizeDataEntry entry)
            {
                data.Entries.Remove(entry);
                entry = null;
            }

            public bool GetLimitHotbarStacks() => data.LimitHotBarStacks;
            public void SetLimitHotbarStacks(bool disable)
            {
                data.LimitHotBarStacks = disable;
            }

            public void SetEnabled(bool enabled)
            {
                data.Enabled = enabled;
            }

            public int GetVanillaStackSize(int itemId) => vanillaStackSize.TryGetValue(itemId, out var ss) ? ss : -1;
            public int GetCurrentStackSize(StackSizeDataEntry entry, ItemDefinition itemDef) => entry.GetStackSize(GetVanillaStackSize(itemDef.itemid), itemDef);

            #endregion

            #region Apply & Revert

            private void ApplyStackSize()
            {
                if (!data.Enabled)
                {
                    return;
                }

                foreach (var item in ItemManager.itemList)
                {
                    if (IsHidden(item))
                    {
                        continue;
                    }
                    
                    var stackSize = GetStackSize(item);
                    if (stackSize > 0)
                    {
                        item.stackable = stackSize;
                    }
                }

            }

            private void RevertStackSize()
            {
                foreach (var kv in vanillaStackSize)
                {
                    if (ItemManager.FindItemDefinition(kv.Key) is ItemDefinition def)
                    {
                        def.stackable = kv.Value;
                    }
                }
            }

            #endregion

            private void LoadVanillaStackSizes()
            {
                if (data.Version < Protocol.save)
                {
                    Log("Loading vanilla stack sizes from file ...");
                    
                    var itemFolder = Path.Combine("Bundles", "items");
                    if (!Directory.Exists(itemFolder))
                    {
                        LogError("Can not load vanilla stack size - Failed to find item folder");
                        return;
                    }

                    int count = 0;
                    foreach (var file in Directory.GetFiles(itemFolder, "*.json"))
                    {
                        var json = File.ReadAllText(file);
                        var itemDef = JsonConvert.DeserializeObject<JsonItemDefinition>(json);

                        if (itemDef.itemid != 0 && itemDef.stackable != 0)
                        {
                            data.VanillaStackSize[itemDef.itemid] = itemDef.stackable;
                            count++;
                        }
                        else
                        {
                            LogError($"Invalid item definition '{Path.GetFileNameWithoutExtension(file)}'");
                        }
                    }
                    
                    Log($"Loaded vanilla stack sizes for {count} items");

                    data.Version = Protocol.save;
                    SaveOnly();
                }

                foreach (var entry in data.VanillaStackSize)
                {
                    vanillaStackSize[entry.Key] = entry.Value;
                }
            }

            [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
            class JsonItemDefinition
            {
                public int itemid;
                public int stackable;
            }
        }

        [ProtoContract]
        class StackSizeData
        {
            [ProtoMember(1)]
            public bool Enabled { get; set; }

            [ProtoMember(2)]
            public StackSizeDataEntry DefaultEntry { get; init; } = new();

            [ProtoMember(3)]
            public List<StackSizeDataEntry> Entries { get; init; } = new();

            [ProtoMember(4)]
            public bool LimitHotBarStacks { get; set; }

            [ProtoMember(5)]
            public Dictionary<int, int> VanillaStackSize { get; init; } = new();
            
            [ProtoMember(6)]
            public int Version { get; set; }
        }

        [ProtoContract]
        class StackSizeDataEntry
        {
            [ProtoMember(1)]
            public float GlobalMultiplier { get; set; } = 1;

            [ProtoMember(2)]
            public Dictionary<ItemCategory, float> CategoryMultipliers { get; init; } = new();

            [ProtoMember(3)]
            public Dictionary<int, int> CustomStackSize { get; init; } = new();

            [ProtoMember(4)]
            public HashSet<uint> Prefabs { get; init; } = new();

            [ProtoMember(5)]
            public bool Enabled { get; set; }

            public int GetStackSize(int vanillaStackSize, ItemDefinition itemDef)
            {
                // Item has custom stack size
                if (CustomStackSize.TryGetValue(itemDef.itemid, out var cs))
                {
                    return cs;
                }

                // Item uses category multiplier
                if (CategoryMultipliers.TryGetValue(itemDef.category, out var catMpl) && !Mathf.Approximately(catMpl, 1))
                {
                    return Mathf.Max(1, Mathf.RoundToInt(vanillaStackSize * catMpl));
                }

                // Item uses global multiplier
                return Mathf.Max(1, Mathf.RoundToInt(vanillaStackSize * GlobalMultiplier));
            }

            #region UI Methods

            public float GetCategoryMultiplier(ItemCategory category) => CategoryMultipliers.TryGetValue(category, out var multiplier) ? multiplier : 1;
            public void SetCategoryMultiplier(ItemCategory category, float multiplier)
            {
                CategoryMultipliers[category] = multiplier;
            }

            public int GetCustomStackSize(int itemId) => CustomStackSize.TryGetValue(itemId, out var ss) ? ss : -1;
            public void SetCustomStackSize(int itemId, int stackSize)
            {
                CustomStackSize[itemId] = Mathf.Max(1, stackSize);
            }
            public void ClearCustomStackSize(int itemId)
            {
                CustomStackSize.Remove(itemId);
            }

            public void ResetCustomStackSize()
            {
                GlobalMultiplier = 1;
                CustomStackSize.Clear();
                CategoryMultipliers.Clear();
            }

            internal string GetDisplayName(Func<string, object, object, string> t)
            {
                if (Prefabs.Count < 1)
                {
                    return t("seditor_entry_empty", null, null);
                }

                var prefabName = Path.GetFileNameWithoutExtension(StringPool.Get(Prefabs.First())).UpperFirst();
                if (Prefabs.Count > 1)
                {
                    return t("seditor_entry_multiple", prefabName, Prefabs.Count - 1);
                }
                else
                {
                    return prefabName;
                }
            }

            #endregion
        }

        #endregion

        #region Item Helper

        static class ItemHelper
        {
            public const int CHAINSAW_ITEMID = 1104520648;
            public const int FLAMETHROWER_ITEMID = -1215753368;

            private static readonly HashSet<int> baseProjectileItems = new();
            private static readonly HashSet<int> baseLiquidVesselItems = new();
            private static readonly HashSet<int> heldEntitiesWithDurability = new();

            public static void Init()
            {
                foreach (var def in ItemManager.GetItemDefinitions())
                {
                    var entityMod = def.GetComponent<ItemModEntity>();
                    if (entityMod != null)
                    {
                        var entity = GameManager.server.CreateEntity(entityMod.entityPrefab.resourcePath, startActive: false);

                        if (def.condition.enabled && entity is not Planner && entity is not Deployer && entity is not PoweredLightsDeployer)
                        {
                            heldEntitiesWithDurability.Add(def.itemid);
                            //LogDebug($"Add HeldEntity {def.shortname}");
                        }

                        if (entity is BaseProjectile)
                        {
                            baseProjectileItems.Add(def.itemid);
                            //LogDebug($"Add BaseProjectile {def.shortname}");
                        }
                        else if (entity is BaseLiquidVessel)
                        {
                            baseLiquidVesselItems.Add(def.itemid);
                            //LogDebug($"Add BaseLiquidVessel {def.shortname}");
                        }

                        UnityEngine.Object.Destroy(entity.gameObject);
                    }
                }
            }

            public static bool IsHeldEntity(int itemId)
            {
                return heldEntitiesWithDurability.Contains(itemId);
            }

            public static bool IsBaseProjectile(int itemId)
            {
                return baseProjectileItems.Contains(itemId);
            }

            public static bool IsLiquidContainer(int itemId)
            {
                return baseLiquidVesselItems.Contains(itemId);
            }

            public static bool IsMinerHat(int itemId)
            {
                return itemId == -1539025626 || itemId == 1714496074;
            }

            public static void GiveToPlayer(ItemDefinition itemDef, int amount, BasePlayer player, ulong skinId = 0)
            {
                var item = ItemManager.Create(itemDef, amount, skinId);
                GiveToPlayer(item, player);
            }

            public static void GiveToPlayer(Item item, BasePlayer player)
            {
                if (!item.MoveToContainer(player.inventory.containerMain))
                {
                    item.Drop(player.GetDropPosition(), player.GetDropVelocity());
                }
            }
        }

        #endregion

        #region \u0048\u0061\u0072\u006D\u006F\u006E\u0079

        private static class ExcavatorArm_ProduceResources
        {
            private static bool Prefix(ExcavatorArm __instance)
            {
                return Instance?.OnExcavatorProduceResources(__instance) == null;
            }
        }

        private static class TrainCarUnloadable_FillWithLoot
        {
            private static bool Prefix(StorageContainer sc, TrainCarUnloadable __instance)
            {
                if (__instance.wagonType == TrainCarUnloadable.WagonType.Lootboxes)
                {
                    return true;
                }

                return Instance?.OnTrainWagonLootSpawn(__instance, sc) == null;
            }
        }

        private static class TrainCarUnloadable_GetOrePrecent
        {
            private static bool Prefix(TrainCarUnloadable __instance, ref float __result)
            {
                var result = Instance?.OnTrainWagonOreLevel(__instance);
                if (result.HasValue)
                {
                    __result = result.Value;
                    return false;
                }

                return true;
            }
        }

        private static class ResourceDispenser_Initialize
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                Instance?.OnResourceDispenserInitialize(__instance);
            }
        }

        #endregion

        #region Legacy
        
        [Flags]
        private enum LegacyImportTarget
        {
            NOTHING = 0,
            STACK_SIZE = 1,
            LOOT = 2,
            CUSTOM_ITEMS = 4,
            SMELTING_AND_SUPPLY = 8,
            EVERYTHING = STACK_SIZE | LOOT | CUSTOM_ITEMS | SMELTING_AND_SUPPLY,
        }

        [Debug]
        private void ImportLegacyFiles(LegacyImportTarget targets, bool reload)
        {
            Log("Attempting to import legacy files");
            
            if (targets.HasFlag(LegacyImportTarget.LOOT))
            {
                ImportLootConfigs();
            }
            if (targets.HasFlag(LegacyImportTarget.STACK_SIZE))
            {
                ImportStackSizeConfig();
            }
            if (targets.HasFlag(LegacyImportTarget.CUSTOM_ITEMS))
            {
                ImportCustomItems();
            }
            if (targets.HasFlag(LegacyImportTarget.SMELTING_AND_SUPPLY))
            {
                ImportSmeltingAndSupplyConfig();
            }

            if (reload)
            {
                NextTick(() => Interface.Oxide.ReloadPlugin(nameof(Loottable)));
            }
        }
        
        private static bool GetLegacyDataFolder(out string oldDataFolder)
        {
            oldDataFolder = Path.Combine(Interface.Oxide.DataDirectory, "Loottable");
            return Directory.Exists(oldDataFolder);
        }

        private bool ImportSmeltingAndSupplyConfig()
        {
            if (!GetLegacyDataFolder(out var oldDataFolder))
            {
                return false;
            }
            
            var smeltingDataFile = Path.Combine(oldDataFolder, "config_smelting.json");
            if (File.Exists(smeltingDataFile))
            {
                var json = File.ReadAllText(smeltingDataFile);
                var config = JsonConvert.DeserializeObject<LoottableLegacy.FurnaceConfiguration>(json);
                var cfg = configurations.Smelting;

                cfg.Enabled = config.enabled;
                cfg.RecyclerEnabled = config.recyclerEnabled;
                cfg.SmallFurnaceSpeed = config.sFsmeltingSpeedMultiplier;
                cfg.LargeFurnaceSpeed = config.lFsmeltingSpeedMultiplier;
                cfg.RefinerySpeed = config.rFsmeltingSpeedMultiplier;
                cfg.CampfireSpeed = config.campfireSmeltingSpeedMultiplier;
                cfg.BBQSpeed = config.bbqSmeltingSpeedMultiplier;
                cfg.ElectricFurnaceSpeed = config.electricFurnaceSpeed;
                cfg.RecyclerSpeed = config.recyclingSpeedMultiplier;
                cfg.MixingSpeedMultiplier = config.mixingSpeedMultiplier;
                cfg.CharcoalChance = config.charcoalChance;
                cfg.CharcoalAmount = config.charcoalAmount;
            }
            
            var supplyDropDataFile = Path.Combine(oldDataFolder, "config_airdrop.json");
            if (File.Exists(supplyDropDataFile))
            {
                var json = File.ReadAllText(supplyDropDataFile);
                var config = JsonConvert.DeserializeObject<LoottableLegacy.AirDropConfiguration>(json);
                var cfg = configurations.SupplyDrop;

                cfg.Enabled = config.enabled;
                cfg.PlaneSpeed = config.planeSpeedMultiplier;
                cfg.PlaneHeight = config.planeHeight;
                cfg.SmokeDuration = config.smokeDuration;
                cfg.ExactDropPosition = config.exactDropPosition;
                cfg.DropFallSpeed = config.dropFallSpeed;
                cfg.DropFallSmoke = config.dropFallSmoke;
                cfg.RandomPosTolerance = config.randomPosTolerance;
            }
            
            configurations.Save();
            configurations.Initialize();
            
            return true;
        }
        
        private bool ImportLootConfigs()
        {
            if (!GetLegacyDataFolder(out var oldDataFolder))
            {
                return false;
            }
            
            foreach (var lootable in manager.GetLegacyLootables())
            {
                var dataFile = Path.Combine(oldDataFolder, $"{lootable.Key}.json");
                if (File.Exists(dataFile))
                {
                    var json = File.ReadAllText(dataFile);
                    var config = ConvertLegacyProfile(lootable, json);
                    manager.SaveConfig(config);
                }
            }

            return true;

            static LootConfig ConvertLegacyProfile(BaseLootable lootable, string json)
            {
                Log($"Importing legacy loot profile {lootable.Key}");

                var config = JsonConvert.DeserializeObject<LoottableLegacy.LootableConfig>(json);

                var newConfig = LootConfig.Create(lootable.Id);
                newConfig.SetFlag(lootable.ConfigFlags, true);

                var categories = new Dictionary<int, LootCategory>();
                foreach (var item in config.items)
                {
                    var newItem = ConvertLootItem(item);
                    if (newItem == null)
                    {
                        continue;
                    }

                    if (item.category == 0)
                    {
                        newConfig.DefaultCategory.Add(newItem);
                    }
                    else
                    {
                        if (!categories.TryGetValue(item.category, out var category) && config.categories.TryGetValue(item.category, out var oldCat))
                        {
                            category = new LootCategory
                            {
                                Amount = ConvertMinMax(oldCat.itemAmount),
                                Chance = Mathf.Clamp01(oldCat.chance),
                            };
                            categories.Add(item.category, category);
                        }

                        category?.Items.Add(newItem);
                    }
                }

                foreach (var cat in categories.Values)
                {
                    newConfig.AddCategory(cat);
                }

                foreach (var item in config.blacklist ?? Enumerable.Empty<LoottableLegacy.BlacklistItem>())
                {
                    newConfig.Blacklist.Add(item.itemid);
                }

                foreach (var item in config.additions ?? Enumerable.Empty<LoottableLegacy.LootItem>())
                {
                    var newItem = ConvertLootItem(item);
                    if (newItem != null)
                    {
                        newConfig.AddAddition(newItem);
                    }
                }

                newConfig.Amount = ConvertMinMax(config.item_amount);
                newConfig.LootType = config.lootType switch
                {
                    LoottableLegacy.LootableConfig.LootType.BlackList => LootType.Blacklist,
                    LoottableLegacy.LootableConfig.LootType.Addition => LootType.Addition,
                    _ => LootType.Custom
                };
                if (config.enabled)
                {
                    if (!newConfig.SetEnabled(true, out var error))
                    {
                        LogWarning($"Failed to enable imported loot profile {lootable.Key}: {error}");
                    }
                }
                else
                {
                    newConfig.Validate();
                }

                return newConfig;
            }

            static LootItem ConvertLootItem(LoottableLegacy.LootItem item)
            {
                if (!ItemManager.itemDictionary.ContainsKey(item.itemid))
                {
                    LogWarning($"Item with item id {item.itemid} was not found");
                    return null;
                }

                var condition = new MinMaxFloat(item.condition.min, item.condition.max);
                if (condition.Min <= 0 || condition.Max <= 0)
                {
                    condition = new MinMaxFloat(1);
                }

                return new LootItem
                {
                    ItemId = item.itemid,
                    SkinId = item.skin,
                    Chance = item.chance,
                    CustomName = (item.displayname?.Length > 0) ? item.displayname : null,
                    Condition = condition,
                    Amount = ConvertMinMax(item.amount),
                    IsBlueprint = item.isBlueprint,
                    Extra = ConvertExtraItem(item.extras?.FirstOrDefault())
                };
            }

            static LootItem ConvertExtraItem(LoottableLegacy.ExtraItem item)
            {
                if (item == null)
                {
                    return null;
                }

                if (!ItemManager.itemDictionary.ContainsKey(item.itemid))
                {
                    LogWarning($"Extra item with item id was not found");
                    return null;
                }
                
                return new LootItem
                {
                    ItemId = item.itemid,
                    Amount = ConvertMinMax(item.amount),
                };
            }

            static MinMax ConvertMinMax(LoottableLegacy.MinMax minMax)
            {
                var amount = new MinMax(minMax.min, minMax.max);
                if (amount.Min <= 0 || amount.Max <= 0)
                {
                    amount = new MinMax(1, amount.Max > 0 ? amount.Max : 1);
                }

                return amount;
            }
        }

        private bool ImportStackSizeConfig()
        {
            if (!GetLegacyDataFolder(out var oldDataFolder))
            {
                return false;
            }
            
            var stackSizeFile = Path.Combine(oldDataFolder, "stacksize.json");
            if (!File.Exists(stackSizeFile))
            {
                return false;
            }
            
            var json = File.ReadAllText(stackSizeFile);
            var config = JsonConvert.DeserializeObject<LoottableLegacy.StacksizeConfig>(json);
            var entry = stackSizeController.GetDefaultEntry();

            entry.GlobalMultiplier = config.globalMultiplier;

            foreach (var kv in config.categoryMultipliers)
            {
                var category = (ItemCategory)kv.Key;
                entry.SetCategoryMultiplier(category, kv.Value);
            }

            foreach (var kv in config.itemStacksize)
            {
                config.vanillaStacksize.TryGetValue(kv.Key, out var vs);
                var itemDef = ItemManager.FindItemDefinition(kv.Key);
                if (itemDef != null && (vs == 0 || kv.Value != vs))
                {
                    entry.SetCustomStackSize(kv.Key, kv.Value);
                    LogDebug($"Import custom stack size for {itemDef.shortname} st {itemDef.stackable} vs {vs} cs {kv.Value}");
                }
            }

            stackSizeController.SetEnabled(config.enabled);

            return true;
        }

        private bool ImportCustomItems()
        {
            if (!GetLegacyDataFolder(out var oldDataFolder))
            {
                return false;
            }

            var customItemsFile = Path.Combine(oldDataFolder, "custom_items_v2.json");
            if (!File.Exists(customItemsFile))
            {
                return false;
            }

            var json = File.ReadAllText(customItemsFile);
            var customItems = JsonConvert.DeserializeObject<LoottableLegacy.CustomItemData>(json);

            foreach (var customItem in customItems.persistent)
            {
                manager.CustomItems.AddCustomItem(new CustomItemDefinition
                {
                    Plugin = customItem.plugin,
                    CustomName = customItem.customName,
                    ItemId = customItem.itemId,
                    Persistent = customItem.persistent,
                    SkinId = customItem.skinId
                });
            }
            
            return true;
        }

        #endregion

        #region Lang

        private string GetItemNameTranslated(int itemId, string lang) => GetItemNameTranslated(ItemManager.FindItemDefinition(itemId), lang);

        private string GetItemNameTranslated(ItemDefinition itemDef, string lang)
        {
            if (itemDef == null)
            {
                return "NULL";
            }

            if (RustTranslationAPI != null)
            {
                var result = RustTranslationAPI.Call<string>("GetItemTranslationByID", lang, itemDef.itemid);
                if (result != null)
                {
                    return result;
                }
                else
                {
                    PrintWarning($"Failed to get translation for {itemDef.shortname} (lang: {lang})");
                }
            }

            return itemDef.displayName.english;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["pagecount"] = "Page {0} of {1}",

                ["btn_new_cat"] = "New Category",
                ["btn_save"] = "Save",
                ["btn_cancel"] = "Cancel",
                ["btn_done"] = "Done",
                ["btn_all"] = "All",
                ["btn_ok"] = "OK",
                ["btn_multiply"] = "Multiply",
                ["btn_delete"] = "Delete",
                ["btn_moveto"] = "Move to",
                ["btn_select"] = "Select",
                ["btn_paste"] = "Paste",
                ["btn_copy"] = "Copy",
                ["btn_ltdefault"] = "Load default",
                ["btn_additem"] = "Add item",
                ["btn_addcustom"] = "New Custom Item",
                ["btn_addgroup"] = "New group",
                ["btn_delgroup"] = "Delete group",
                ["btn_edit_stacksize"] = "Edit stack sizes",
                ["btn_add"] = "Add",

                ["multiply_title"] = "Multiply item amount",

                ["tip"] = "Tip",
                ["dispenser_info_2"] = "Additional items are not required for the gather config to work.\nAdditional items in the first category will be rewarded once the player has finished gathering.",
                ["gather_multipliers"] = "Gather multipliers:",
                ["gather_additions"] = "Additional gather items:",

                ["delete_title"] = "Delete selected items",
                ["delete_desc"] = "Are you sure you want to delete all selected items? This action can not be undone.",

                ["paste_title"] = "Paste config of {0}",
                ["paste_desc"] = "This will overwrite the current config. This action can not be undone.",

                ["ltdefault_title"] = "Load default loot table",
                ["ltdefault_desc"] = "This action will overwrite the current loot table." +
                                "\n<color=red><b>IMPORTANT: The default loot table might slightly differ from the rust vanilla loot table for technical reasons. It is recommended to use it only as a reference point</b></color>",

                ["editor_head"] = "Edit {0} ({1})",
                ["editor_remove_corpse"] = "Remove corpse",
                ["editor_total_amount"] = "Total item amount",
                ["editor_loot_type"] = "Loot type",
                ["editor_settings"] = "Settings",

                ["order_label"] = "Order by",
                ["order_category_desc"] = "Category",
                ["order_category"] = "Category desc.",
                ["order_chance"] = "Chance",
                ["order_chance_desc"] = "Chance desc.",

                ["categories"] = "Categories",
                ["category_select"] = "Select category",
                ["category_default"] = "Default Category",
                ["category_id"] = "Category {0}",

                ["ieditor_head"] = "Edit {0} in {1}",
                ["seditor_cat_multiplier"] = "Category multiplier for {0}",
                ["seditor_global_multiplier"] = "Global multiplier",
                ["seditor_info"] = "Items with a custom category multiplier are not affected by the global multiplier.",
                ["seditor_reset"] = "Reset to vanilla",
                ["seditor_reset_text"] = "Are you sure you want to reset your configuration to vanilla? This action can not be undone.",
                ["seditor_hotbar_stacks"] = "Limit hotbar stack size",
                ["seditor_no_group"] = "No prefab group selected",
                ["seditor_prefabs"] = "Prefab groups",
                ["seditor_enabled"] = "Stack size controller",
                ["seditor_prefab_count"] = "{0} Prefabs",
                ["seditor_entry_empty"] = "Empty prefab group",
                ["seditor_entry_multiple"] = "{0} +{1} more",
                ["seditor_add_prefab"] = "Add prefab",
                ["seditor_default_desc"] = "Used for all prefabs that don't have a group",
                ["seditor_head"] = "Edit stack sizes for {0}",

                ["chance_percent"] = "Chance %",
                ["condition_percent"] = "Condition %",
                ["amount"] = "Amount",
                ["shortname"] = "Shortname",
                ["blueprint"] = "Blueprint",
                ["customname"] = "Custom name",
                ["skinid"] = "Skin id",
                ["preview"] = "Preview",
                ["item_list"] = "Item list",

                ["enabled"] = "Enabled",
                ["disabled"] = "Disabled",
                ["default"] = "Default",
                ["invalid"] = "Invalid",
                ["custom_loot"] = "Custom Loot",
                ["vanilla"] = "Vanilla",
                ["custom"] = "Custom",
                ["vanilla_a"] = "Vanilla + Additions",
                ["blacklist"] = "Blacklist",
                ["stack_size_control"] = "Stack Size Controller",
                ["gather_control"] = "Gather Control",
                ["plugins"] = "Plugins",
                ["custom_item_manager"] = "Custom Item Manager",
                ["custom_item_info"] = "Click on a Custom Item to edit it.\n\nCustom Items created by other plugins can not be edited.",
                ["custom_items_edit"] = "Edit Custom Item",

                // Default tabs
                ["crates"] = "Crates",
                ["crates_normal"] = "Default",
                ["crates_diving"] = "Diving",
                ["crates_roadside"] = "Roadside",
                ["crates_special"] = "Special",
                ["collectibles"] = "Collectibles",
                ["gathering"] = "Gathering",
                ["ore"] = "Ore",
                ["animals"] = "Animals",
                ["cactus"] = "Cactus",
                ["driftwood_logs"] = "Driftwood",
                ["palms"] = "Palm Tree",
                ["trees"] = "Trees",
                ["quarries"] = "Quarries",
                ["excavator"] = "Excavator",
                ["train_wagons"] = "Train Wagons",
                ["unwrapable"] = "Unwrapable Items",
                ["crates_other"] = "Other Crates",
                ["crates_underwater"] = "Underwater Crates",
                ["crates_invisible"] = "Invisible Crates",
                ["crates_deathmatch"] = "Deathmatch Crates",
                ["crates_train"] = "Train Wagon Crates",

                // Crates tab
                ["crate_basic"] = "Basic Crate",
                ["crate_normal"] = "Normal Crate",
                ["crate_military"] = "Military Crate",
                ["crate_elite"] = "Elite Crate",
                ["crate_mine"] = "Mine Crate",
                ["crate_food"] = "Food Crate",
                ["crate_medical"] = "Medical Crate",
                ["crate_tools"] = "Tool Crate",
                ["crate_food_2"] = "Food Box",
                ["crate_minecart"] = "Minecart",
                ["crate_vehicle_parts"] = "Vehicle Parts Crate",
                ["crate_underwater_basic"] = "Basic Underwater Crate",
                ["crate_underwater_advanced"] = "Advanced Underwater Crate",
                ["barrel"] = "Barrel",
                ["barrel_oil"] = "Oil Barrel",
                ["roadsign"] = "Roadsign",
                ["crate_hackable"] = "Hackable Crate",
                ["crate_hackable_oilrig"] = "Oil Rig Hackable Crate",
                ["crate_apc"] = "APC Crate",
                ["crate_heli"] = "Heli Crate",
                ["crate_supplydrop"] = "Supply Drop",
                ["crate_food_cache"] = "Food Cache",
                ["crates_food"] = "Food Crates",

                // NPC tab
                ["npcs"] = "NPCs",
                ["npc_militunnel"] = "Military Tunnel Scientist",
                ["npc_cargoship"] = "Cargo Ship Scientist",
                ["npc_tunneldweller"] = "Tunneldweller",
                ["npc_scarecrow"] = "Scarecrow",
                ["npc_excavator"] = "Excavator Scientist",
                ["npc_heavy"] = "Heavy Scientist",
                ["npc_oilrig"] = "Oil Rig Scientist",
                ["npc_chinook"] = "CH47 Scientist",
                ["npc_junkpile"] = "Junk Pile Scientist",
                ["npc_underwaterdweller"] = "Underwater Lab Scientist",
                ["npc_desert"] = "Desert Military Base Scientist",
                ["npc_missilesilo_nvg"] = "Missile Silo Scientist (NVG)",
                ["npc_missilesilo"] = "Missile Silo Scientist",
                ["npc_trainyard"] = "Trainyard Scientist",
                ["npc_airfield"] = "Airfield Scientist",
                ["npc_launchsite"] = "Launch Site Scientist",
                ["npc_arctic"] = "Arctic Base Scientist",
                ["npc_gingerbread"] = "Gingerbread NPC",
                ["npc_bradley"] = "Bradley Scientist",
                ["npc_bradley_heavy"] = "Heavy Bradley Scientist",

                // Collectible tab
                ["collectible_diesel"] = "Diesel Barrel",
                ["collectible_wood"] = "Stump",
                ["collectible_stone"] = "Stone",
                ["collectible_sulfur"] = "Sulfur Ore",
                ["collectible_metal"] = "Metal Ore",
                ["collectible_pumpkin"] = "Pumpkin",
                ["collectible_potato"] = "Potato",
                ["collectible_mushroom"] = "Mushroom",
                ["collectible_hemp"] = "Hemp",
                ["collectible_corn"] = "Corn",
                ["collectible_berry_yellow"] = "Yellow Berry",
                ["collectible_berry_white"] = "White Berry",
                ["collectible_berry_red"] = "Red Berry",
                ["collectible_berry_green"] = "Green Berry",
                ["collectible_berry_blue"] = "Blue Berry",
                ["collectible_berry_black"] = "Black Berry",

                // Gathering tab
                ["ore_stone"] = "Stone Ore",
                ["ore_metal"] = "Metal Ore",
                ["ore_sulfur"] = "Sulfur Ore",
                ["corpse_chicken"] = "Chicken",
                ["corpse_boar"] = "Boar",
                ["corpse_stag"] = "Stag",
                ["corpse_bear"] = "Bear",
                ["corpse_polarbear"] = "Polar Bear",
                ["tree_swamp"] = "Swamp Tree",
                ["special"] = "Special",
                ["gibs_bradley"] = "Bradley Debris",
                ["gibs_heli"] = "Patrol Heli Debris",
                ["wood_pile"] = "Legacy Wood Pile",
                ["tree"] = "Tree",
                ["tree_palm"] = "Palm Tree",
                ["logs"] = "Logs",
                ["corpses"] = "Corpses",
                ["driftwood"] = "Driftwood",
                ["corpse_scientist"] = "Scientist",
                ["corpse_wolf"] = "Wolf",
                ["cactus"] = "Cactus",

                // Quarries tab
                ["quarry_oil"] = "Pump Jack",
                ["quarry_any"] = "Everything Quarry",
                ["quarry_stone"] = "Stone Quarry",
                ["quarry_sulfur"] = "Sulfur Quarry",
                ["quarry_hqm"] = "HQM Quarry",
                ["quarry_excavator_stone"] = "Excavator Stone",
                ["quarry_excavator_sulfur"] = "Excavator Sulfur Ore",
                ["quarry_excavator_metal"] = "Excavator Metal",
                ["quarry_excavator_hqm"] = "Excavator HQM Ore",
                ["wagon_metal"] = "Metal Wagon",
                ["wagon_sulfur"] = "Sulfur Wagon",
                ["wagon_charcoal"] = "Charcoal Wagon",
                ["wagon_fuel"] = "Fuel Wagon",

                // Unwrapable tab
                ["present_large"] = "Large Present",
                ["present_medium"] = "Medium Present",
                ["present_small"] = "Small Present",
                ["egg_bronze"] = "Bronze Egg",
                ["egg_silver"] = "Silver Egg",
                ["egg_gold"] = "Gold Egg",
                ["lootbag_small"] = "Small Lootbag",
                ["lootbag_medium"] = "Medium Lootbag",
                ["lootbag_large"] = "Large Lootbag",

                // Other crates tab
                ["crate_basic_underwater"] = "Underwater Basic Crate",
                ["crate_military_underwater"] = "Underwater Military Crate",
                ["crate_elite_underwater"] = "Underwater Elite Crate",
                ["crate_ammo_underwater"] = "Underwater Ammo Crate",
                ["crate_food_underwater"] = "Underwater Food Crate",
                ["crate_fuel_underwater"] = "Underwater Fuel Crate",
                ["crate_medical_underwater"] = "Underwate Medical Crate",
                ["crate_tools_underwater"] = "Underwater Tool Crate",
                ["crate_techparts_underwater"] = "Underwater Techparts Crate",
                ["crate_vehicle_parts_underwater"] = "Underwater Vehicle Parts Crate",
                ["dm_ammo"] = "DM Ammo",
                ["dm_c4"] = "DM C4",
                ["dm_construction_resources"] = "DM Construction Resources",
                ["dm_construction_tools"] = "DM Construction Tools",
                ["dm_food"] = "DM Food",
                ["dm_medical"] = "DM Medical",
                ["dm_resources"] = "DM Resources",
                ["dm_tier1"] = "DM Tier 1",
                ["dm_tier2"] = "DM Tier 2",
                ["dm_tier3"] = "DM Tier 3",
                ["crate_basic_invisible"] = "Invisible Basic Crate",
                ["crate_normal_invisible"] = "Invisible Normal Crate",
                ["crate_military_invisible"] = "Invisible Military Crate",
                ["crate_elite_invisible"] = "Invisible Elite Crate",
                ["crate_food_invisible"] = "Invisible Food Crate",
                ["crate_medical_invisible"] = "Invisible Medical Crate",
                ["crate_tools_invisible"] = "Invisible Tool Crate",
                ["crate_food_2_invisible"] = "Invisible Food Box",
                ["crate_vehicle_parts_invisible"] = "Invisible Vehicle Parts Crate",
                ["crate_hackable_invisible"] = "Invisible Locked Crate",
                ["wagon_crate_normal"] = "Normal Wagon Crate",
                ["wagon_crate_military"] = "Military Wagon Crate",
                ["wagon_crate_food"] = "Food Wagon Crate",
                ["wagon_crate_medical"] = "Medical Wagon Crate",

                // Item categories
                ["weapon"] = "Weapon",
                ["construction"] = "Construction",
                ["items"] = "Items",
                ["resources"] = "Resources",
                ["attire"] = "Attire",
                ["tool"] = "Tool",
                ["medical"] = "Medical",
                ["food"] = "Food",
                ["ammunition"] = "Ammunition",
                ["traps"] = "Traps",
                ["misc"] = "Misc",
                ["all"] = "All",
                ["common"] = "Common",
                ["component"] = "Component",
                ["search"] = "Search",
                ["favourite"] = "Favourite",
                ["electrical"] = "Electrical",
                ["fun"] = "Fun",
                
                ["enable_failed"] = "Failed to enable loot profile",
                ["not_enough_items_custom"] = "There has to be at least 1 item in the default category (grey category)",
                ["not_enough_items_addition"] = "There has to be at least 1 additional item",
                ["not_enough_items_blacklist"] = "There has to be at least 1 item in the black list",
                
                // Settings
                ["loot_refresh"] = "Automatic loot refresh",
                ["settings"] = "Settings",
                ["import"] = "Import",
                ["import_confirmation"] = "Confirm import",
                ["import_loot"] = "Import Loot configurations from V1",
                ["import_confirmation_loot"] = "This action will overwrite your current loot profiles and can not be undone!\nAfter the import is completed, the plugin will reload automatically.",
                ["import_stacksize"] = "Import Stack Size configuration from V1",
                ["import_confirmation_stacksize"] = "This action will overwrite your current stack size configuration and can not be undone!\nAfter the import is completed, the plugin will reload automatically.",
                ["import_customitems"] = "Import Custom Items from V1",
                ["import_confirmation_customitems"] = "You are about to import Custom Items from v1. After the import is completed, the plugin will reload automatically.",
                ["loot_refresh_warning"] = "Refreshing loot. Expect some lag.",
                ["import_configuration"] = "Import Smelting, Recycler and Supply Drop configuration from V1",
                ["import_confirmation_configuration"] = "This action will overwrite your current Smelting, Recycler and Supply Drop configuration!",
                
                // Configuration
                ["configuration"] = "Configuration",
                ["configuration_smelting"] = "Smelting Configuration",
                ["sp_furnace_small"] = "Small Furnace speed multiplier",
                ["sp_furnace_large"] = "Large Furnace speed multiplier",
                ["sp_furnace_electric"] = "Electric Furnace speed multiplier",
                ["sp_refinery"] = "Oil Refinery speed multiplier",
                ["sp_bbq"] = "Barbeque speed multiplier",
                ["sp_campfire"] = "Campfire speed multiplier",
                ["sp_mixing_table"] = "Mixing Table speed multiplier",
                ["charcoal_amount"] = "Amount of charcoal produced by 1 wood",
                ["charcoal_chance_percent"] = "Chance for wood to turn into charcoal when burnt (%)",
                ["sp_disabled_by_plugin"] = "Smelting Configuration is not available when <b>SimpleSplitter</b> is loaded.",
                
                ["configuration_supply_drop"] = "Supply Drop Configuration",
                ["sd_plane_speed"] = "Cargo Plane speed multiplier",
                ["sd_plane_height"] = "Cargo Plane fly height (can also be negative)",
                ["sd_smoke_duration"] = "Supply Signal smoke duration (seconds)",
                ["sd_fall_speed"] = "Supply Drop fall speed (lower = faster)",
                ["sd_drop_smoke"] = "Supply Drop smoke effect",
                ["sd_exact_position"] = "Supply Drop lands exactly where the Supply Signal was thrown",
                ["sd_position_tolerance"] = "Supply Drop position tolerance",
                ["sd_disabled_by_plugin"] = "Supply Drop Configuration is not available when <b>FancyDrop</b> or <b>LootDefender</b> is loaded.",
                ["sd_cooldown_toast"] = "You are on cooldown! Time remaining: {0}m {1}s",
                ["sd_cooldown"] = "Cooldown between supply signals",
                ["sd_cooldown_time"] = "Cooldown time (per player, seconds)",
                
                ["configuration_recycler"] = "Recycler Configuration",
                ["sp_speed"] = "Radtown Recycler speed multiplier",
                ["sp_efficiency_percent"] = "Radtown Recycler efficiency (%)",
                ["sp_sz_speed"] = "Safe Zone Recycler speed multiplier",
                ["sp_sz_efficiency_percent"] = "Safe Zone Recycler efficiency (%)",
                
            }, this);
        }

        #endregion
    }
}
namespace Oxide.Plugins.LoottableInterop
{
    internal class LootableDataList
    {
        public DispenserData[] DispenserData { get; init; } = Array.Empty<DispenserData>();
        public TypedLootableData[] OtherData { get; init; } = Array.Empty<TypedLootableData>();
        public UnwrapableData[] UnwrapableData { get; init; } = Array.Empty<UnwrapableData>();
        public CollectibleData[] CollectableData { get; init; } = Array.Empty<CollectibleData>();
        public NpcData[] NpcData { get; init; } = Array.Empty<NpcData>();
        public CrateData[] CrateData { get; init; } = Array.Empty<CrateData>();

        public Dictionary<string, Dictionary<string, string[]>> Tabs { get; init; } = new();

        public bool IsValid()
        {
            return OtherData.Length > 0 && UnwrapableData.Length > 0 && CollectableData.Length > 0 && NpcData.Length > 0 && CrateData.Length > 0 && Tabs.Count > 0;
        }

        public void AddTab<T>(string tabKey, T[] lootables) where T : BaseLootableData
            => AddTab(tabKey, lootables, 0, lootables.Length - 1);

        public void AddTab<T>(string tabKey, T[] lootables, int startIndex, int endIndex)  where T : BaseLootableData
            => AddTab(tabKey, string.Empty, lootables, startIndex, endIndex);

        public void AddTab<T>(string tabKey, string categoryKey, T[] lootables, int startIndex, int endIndex) where T : BaseLootableData
        {
            var keyList = new List<string>();
            for (int i = startIndex; i <= endIndex; i++)
            {
                keyList.Add(lootables[i].Key);
            }

            if (!Tabs.TryGetValue(tabKey, out var categoryDict))
            {
                categoryDict = new Dictionary<string, string[]>();
                Tabs.Add(tabKey, categoryDict);
            }
            categoryDict[categoryKey] = keyList.ToArray();
        }

    }

    internal class DispenserData : BaseLootableData
    {
        public string[] PrefabNames { get; init; }

        [JsonConstructor]
        private DispenserData() { }

        public DispenserData(string key, string imageUrl, params string[] prefabNames)
            : base(key, imageUrl)
        {
            PrefabNames = prefabNames;
        }
    }

    internal class TypedLootableData : BaseLootableData
    {
        public LootableType Type { get; init; }

        [JsonConstructor]
        private TypedLootableData() { }

        public TypedLootableData(string key, string imageUrl, LootableType type)
            : base(key, imageUrl)
        {
            Type = type;
        }
    }

    internal class UnwrapableData : BaseLootableData
    {
        public int ItemId { get; init; }

        [JsonConstructor]
        private UnwrapableData() { }

        public UnwrapableData(int itemId, string key, string imageUrl)
            : base(key, imageUrl)
        {
            ItemId = itemId;
        }
    }

    internal class CollectibleData : BaseLootableData
    {
        public string[] PrefabNames { get; init; }

        [JsonConstructor]
        private CollectibleData() { }

        public CollectibleData(string key, string imageUrl, params string[] prefabNames)
            : base(key, imageUrl)
        {
            PrefabNames = prefabNames;
        }
    }

    internal class CrateData : BaseLootableData
    {
        public int MaxSlots { get; init; }
        public int DefaultSlots { get; init; }
        public string[] PrefabNames { get; init; }

        [JsonConstructor]
        private CrateData() { }

        public CrateData(string key, string imageUrl, params string[] prefabNames)
            : this(key, imageUrl, 6, 12, prefabNames) { }

        public CrateData(string key, string image, int defaultSlots, int maxSlots, params string[] prefabNames)
            : base(key, image)
        {
            DefaultSlots = defaultSlots;
            MaxSlots = maxSlots;
            PrefabNames = prefabNames;
        }
    }

    internal class NpcData : BaseLootableData
    {
        protected override string DefaultImage => Url("hazmatsuit_scientist_512.png");

        public string SpawnPointPrefabName { get; init; }
        public string[] PrefabNames { get; init; }

        [JsonConstructor]
        private NpcData() { }

        public NpcData(string key, string imageUrl, string spawnPointPrefabName, params string[] prefabNames)
            : base(key, imageUrl)
        {
            SpawnPointPrefabName = spawnPointPrefabName;
            PrefabNames = prefabNames;
        }
    }

    internal class BaseLootableData
    {
        protected virtual string DefaultImage => Url("cratecostume_512.png");

        public string Key { get; init; }
        public string ImageUrl { get; init; }

        [JsonConstructor]
        protected BaseLootableData() { }

        public BaseLootableData(string key, string imageUrl)
        {
            Key = key;
            // ReSharper disable once VirtualMemberCallInConstructor
            ImageUrl = imageUrl ?? DefaultImage;
        }

        public static string Url(string fileName)
        {
            return fileName != null ? $"/file/loottable/{fileName}" : null;
        }
    }

    internal enum LootableType : byte
    {
        Invalid,

        QuarryAny,
        QuarryOil,
        QuarryStone,
        QuarrySulfur,
        QuarryHQM,

        ExcavatorStone,
        ExcavatorSulfur,
        ExcavatorMetal,
        ExcavatorHQM,

        WagonSulfur,
        WagonMetal,
        WagonCharcoal,
        WagonFuel
    }

    internal static class LootableList
    {
        private static string Url(string fileName) => fileName != null ? $"/file/loottable/{fileName}" : null;
        
        public static LootableDataList Get()
        {
            var list = new LootableDataList
            {
                DispenserData = new DispenserData[]
                {
                    new("ore_stone", Url("ore_stone.png"), "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab", 
                        "assets/bundled/prefabs/autospawn/resource/ores_sand/stone-ore.prefab", 
                        "assets/bundled/prefabs/autospawn/resource/ores_snow/stone-ore.prefab",
                        "assets/bundled/prefabs/radtown/ore_stone.prefab"),
                    new("ore_metal", Url("ore_metal.png"), "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab", 
                        "assets/bundled/prefabs/autospawn/resource/ores_sand/metal-ore.prefab", 
                        "assets/bundled/prefabs/autospawn/resource/ores_snow/metal-ore.prefab",
                        "assets/bundled/prefabs/radtown/ore_metal.prefab"),
                    new("ore_sulfur", Url("ore_sulfur.png"), "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab", 
                        "assets/bundled/prefabs/autospawn/resource/ores_sand/sulfur-ore.prefab", 
                        "assets/bundled/prefabs/autospawn/resource/ores_snow/sulfur-ore.prefab",
                        "assets/bundled/prefabs/radtown/ore_sulfur.prefab"),

                    // 3 Trees
                    new("tree", null,
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_b_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_c_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest_saplings/pine_sapling_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest_saplings/pine_sapling_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest_saplings/pine_sapling_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_sapling_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_sapling_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_sapling_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_sapling_d_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_sapling_e_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_d_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_e_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest_saplings/pine_sapling_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest_saplings/pine_sapling_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_sapling_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_sapling_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_d_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/douglas_fir_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/douglas_fir_d_small.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/pine_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/american_beech_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/oak_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/oak_f.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/pine_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/american_beech_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/american_beech_e_dead.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside_pine/douglas_fir_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside_pine/pine_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_c_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_b snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/birch_big_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/birch_large_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/birch_medium_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/birch_small_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/birch_tiny_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/pine_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_beachside/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_beachside/birch_tiny_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field_large/oak_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field_large/oak_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field_large/oak_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/american_beech_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/birch_tiny_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/pine_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_big_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_large_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_medium_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/american_beech_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/birch_medium_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/birch_tiny_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/birch_big_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/birch_large_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/pine_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/pine_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside_pine/pine_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/birch_big_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/birch_large_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/birch_medium_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/birch_small_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/birch_tiny_tundra.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/douglas_fir_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/douglas_fir_b_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/douglas_fir_c_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_dead_snow_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_dead_snow_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_dead_snow_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_dead_snow_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_dead_snow_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_dead_snow_f.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_dead_snow_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_dead_snow_f.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/douglas_fir_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/douglas_fir_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/douglas_fir_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_dead_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_dead_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_dead_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_dead_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_dead_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_dead_f.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_a_dead.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/american_beech_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/american_beech_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/american_beech_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/douglas_fir_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/douglas_fir_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/douglas_fir_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/pine_dead_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/pine_dead_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/pine_dead_f.prefab"
                    ),
                    
                    new("tree_swamp", null,
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_f.prefab"
                    ),
                    
                    new("tree_palm", null,
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_short_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_short_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_short_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_short_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_short_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_short_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_small_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_small_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_small_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_small_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_small_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_tall_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_tall_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_med_a_entity.prefab"
                    ),
                    
                    new("cactus", Url("cactus_small.png"),
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-3.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-4.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-5.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-6.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-7.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-2.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-1.prefab"
                    ),
                    
                    new("wood_pile", null, "assets/bundled/prefabs/autospawn/resource/wood_log_pile/wood-pile.prefab"),
                    
                    new("driftwood", null,
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_1.prefab",
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_2.prefab",
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_3.prefab",
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_4.prefab",
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_5.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_dry/dead_log_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_dry/dead_log_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_dry/dead_log_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_snow/dead_log_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_snow/dead_log_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_snow/dead_log_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_wet/dead_log_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_wet/dead_log_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/logs_wet/dead_log_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_set_1.prefab",
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_set_2.prefab",
                        "assets/bundled/prefabs/autospawn/resource/driftwood/driftwood_set_3.prefab"
                    ),

                    new("corpse_chicken", Url("chicken.png"), "assets/rust.ai/agents/chicken/chicken.corpse.prefab"),
                    new("corpse_boar", Url("boar.png"), "assets/rust.ai/agents/boar/boar.corpse.prefab"),
                    new("corpse_stag", Url("stag.png"), "assets/rust.ai/agents/stag/stag.corpse.prefab"),
                    new("corpse_bear", Url("bear.png"), "assets/rust.ai/agents/bear/bear.corpse.prefab"),
                    new("corpse_polarbear", Url("polarbear.png"), "assets/rust.ai/agents/bear/polarbear.corpse.prefab"),
                    new("corpse_wolf", null, "assets/rust.ai/agents/wolf/wolf.corpse.prefab"),
                    new("corpse_scientist", null, "assets/prefabs/npc/scientist/scientist_corpse.prefab"),
                    
                    // 16 Bradley & Heli
                    new("gibs_bradley", null, "assets/prefabs/npc/m2bradley/servergibs_bradley.prefab"),
                    new("gibs_heli", null, "assets/prefabs/npc/patrol helicopter/servergibs_patrolhelicopter.prefab"),
                },
                OtherData = new TypedLootableData[]
                {
                    new("quarry_oil", Url("pump_jack.png"), LootableType.QuarryOil),
                    new("quarry_any", Url("mining_quarry.png"), LootableType.QuarryAny),
                    new("quarry_stone", Url("mining_quarry.png"), LootableType.QuarryStone),
                    new("quarry_sulfur", Url("mining_quarry.png"), LootableType.QuarrySulfur),
                    new("quarry_hqm", Url("mining_quarry.png"), LootableType.QuarryHQM),

                    // 5 Excavator
                    new("quarry_excavator_stone", Url("excv0.png"), LootableType.ExcavatorStone),
                    new("quarry_excavator_sulfur", Url("excv0.png"), LootableType.ExcavatorSulfur),
                    new("quarry_excavator_metal", Url("excv0.png"), LootableType.ExcavatorMetal),
                    new("quarry_excavator_hqm", Url("excv0.png"), LootableType.ExcavatorHQM),

                    // 9 Train Wagons
                    //new("wagon_metal", Url("wagon_ore0.png"), LootableType.WagonMetal),
                    //new("wagon_sulfur", Url("wagon_ore0.png"), LootableType.WagonSulfur),
                    //new("wagon_charcoal", Url("wagon_ore0.png"), LootableType.WagonCharcoal),
                    //new("wagon_fuel", Url("wagon_fuel0.png"), LootableType.WagonFuel),
                },
                UnwrapableData = new UnwrapableData[]
                {
                    new(-1622660759, "present_large", Url("present_large.png")),
                    new(756517185, "present_medium", Url("present_medium.png")),
                    new(-722241321, "present_small", Url("present_small.png")),
                    new(844440409, "egg_bronze", Url("egg_bronze.png")),
                    new(1757265204, "egg_silver", Url("egg_silver.png")),
                    new(-1002156085, "egg_gold", Url("egg_gold.png")),
                    new(1319617282, "lootbag_small", Url("lootbag_small.png")),
                    new(1899610628, "lootbag_medium", Url("lootbag_medium.png")),
                    new(479292118, "lootbag_large", Url("lootbag_large.png")),
                },
                CollectableData = new CollectibleData[]
                {
                    new("collectible_diesel", Url("diesel0.png"), "assets/content/structures/excavator/prefabs/diesel_collectable.prefab"),
                    new("collectible_wood", Url("wood_coll0.png"), "assets/bundled/prefabs/autospawn/collectable/wood/wood-collectable.prefab", "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-wood-collectable.prefab"),
                    new("collectible_stone", Url("stone_coll0.png"), "assets/bundled/prefabs/autospawn/collectable/stone/stone-collectable.prefab", "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-stone-collectable.prefab"),
                    new("collectible_sulfur", Url("sulfur_ore0.png"),"assets/bundled/prefabs/autospawn/collectable/stone/sulfur-collectable.prefab", "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-sulfur-collectible.prefab"),
                    new("collectible_metal", Url("metal_ore0.png"), "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-metal-collectable.prefab", "assets/bundled/prefabs/autospawn/collectable/stone/metal-collectable.prefab"),
                    new("collectible_pumpkin", Url("pumpkin_coll.png"), "assets/bundled/prefabs/autospawn/collectable/pumpkin/pumpkin-collectable.prefab"),
                    new("collectible_potato", Url("potato_coll.png"), "assets/bundled/prefabs/autospawn/collectable/potato/potato-collectable.prefab"),
                    new("collectible_mushroom", Url("mushroom.png"), "assets/bundled/prefabs/autospawn/collectable/mushrooms/mushroom-cluster-6.prefab", "assets/bundled/prefabs/autospawn/collectable/mushrooms/mushroom-cluster-5.prefab"),
                    new("collectible_hemp", Url("hemp_coll0.png"), "assets/bundled/prefabs/autospawn/collectable/hemp/hemp-collectable.prefab"),
                    new("collectible_corn", Url("corn.png"), "assets/bundled/prefabs/autospawn/collectable/corn/corn-collectable.prefab"),
                    new("collectible_berry_yellow", Url("berry_yellow0.png"), "assets/bundled/prefabs/autospawn/collectable/berry-yellow/berry-yellow-collectable.prefab"),
                    new("collectible_berry_white", Url("berry_white0.png"), "assets/bundled/prefabs/autospawn/collectable/berry-white/berry-white-collectable.prefab"),
                    new("collectible_berry_red", Url("berry_red0.png"), "assets/bundled/prefabs/autospawn/collectable/berry-red/berry-red-collectable.prefab"),
                    new("collectible_berry_green", Url("berry_green0.png"), "assets/bundled/prefabs/autospawn/collectable/berry-green/berry-green-collectable.prefab"),
                    new("collectible_berry_blue", Url("berry_blue0.png"), "assets/bundled/prefabs/autospawn/collectable/berry-blue/berry-blue-collectable.prefab"),
                    new("collectible_berry_black", Url("berry_black0.png"), "assets/bundled/prefabs/autospawn/collectable/berry-black/berry-black-collectable.prefab")
                },
                NpcData = new NpcData[]
                {
                    new("npc_militunnel", null, null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_lr300.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_mp5.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_pistol.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_shotgun.prefab"
                    ),
                    new("npc_cargoship", null, null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cargo.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cargo_turret_any.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cargo_turret_lr300.prefab"
                    ),
                    new("npc_tunneldweller", Url("npc_tunneldweller.png"), null,
                        "assets/rust.ai/agents/npcplayer/humannpc/tunneldweller/npc_tunneldwellerspawned.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/tunneldweller/npc_tunneldweller.prefab"
                    ),
                    new("npc_scarecrow", Url("npc_scarecrow.png"), null,
                        "assets/prefabs/npc/scarecrow/scarecrow.prefab",
                        "assets/prefabs/npc/scarecrow/scarecrow_dungeon.prefab",
                        "assets/prefabs/npc/scarecrow/scarecrow_dungeonnoroam.prefab"
                    ),
                    new("npc_excavator", null, null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_excavator.prefab"
                    ),
                    new("npc_heavy", Url("npc_heavy.png"), null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab"
                    ),
                    new("npc_oilrig", null, null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab"
                    ),
                    new("npc_chinook", null, null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_ch47_gunner.prefab"
                    ),
                    new("npc_junkpile", null, null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_junkpile_pistol.prefab"
                    ),
                    new("npc_underwaterdweller", Url("npc_underwater.png"), null,
                        "assets/rust.ai/agents/npcplayer/humannpc/underwaterdweller/npc_underwaterdweller.prefab"
                    ),
                    new("npc_desert", null, null,
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab"
                    ),
                    new("npc_missilesilo_nvg", Url("npc_nvg.png"), "assets/bundled/prefabs/autospawn/monument/medium/nuclear_missile_silo.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam_nvg_variant.prefab"
                    ),
                    new("npc_missilesilo", null, "assets/bundled/prefabs/autospawn/monument/medium/nuclear_missile_silo.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam.prefab"
                    ),
                    new("npc_trainyard", null, "assets/bundled/prefabs/autospawn/monument/large/trainyard_1.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_patrol.prefab"
                    ),
                    new("npc_airfield", null, "assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_patrol.prefab"
                    ),
                    new("npc_launchsite", null, "assets/bundled/prefabs/autospawn/monument/xlarge/launch_site_1.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_patrol.prefab"
                    ),
                    new("npc_arctic", Url("npc_arctic.png"), "assets/bundled/prefabs/autospawn/monument/arctic_bases/arctic_research_base_a.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_patrol.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam.prefab",
                        "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab"
                    ),
                    new("npc_gingerbread", Url("npc_gingerbread.png"), null,
                        "assets/prefabs/npc/gingerbread/gingerbread_dungeon.prefab",
                        "assets/prefabs/npc/gingerbread/gingerbread_meleedungeon.prefab"
                    ),
                    new("npc_bradley", null, null, "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_bradley.prefab"),
                    new("npc_bradley_heavy", Url("npc_heavy.png"), null, "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_bradley_heavy.prefab"),
                },
                CrateData = new CrateData[]
                {
                    // 0 Normal
                    new("crate_basic", Url("crate_basic0.png"), "assets/bundled/prefabs/radtown/crate_basic.prefab"),
                    new("crate_normal", Url("cratecostume_512.png"), "assets/bundled/prefabs/radtown/crate_normal_2.prefab"),
                    new("crate_military", Url("dm_t20.png"), "assets/bundled/prefabs/radtown/crate_normal.prefab"),
                    new("crate_elite", Url("crate_t30.png"), "assets/bundled/prefabs/radtown/crate_elite.prefab"),

                    new("crate_mine", Url("cratecostume_512.png"), "assets/bundled/prefabs/radtown/crate_mine.prefab"),
                    new("crate_food", Url("crate_food0.png"), "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab"),
                    new("crate_medical", Url("crate_medical0.png"), "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab"),
                    new("crate_tools", Url("crate_tools0.png"), "assets/bundled/prefabs/radtown/crate_tools.prefab"),
                    new("crate_food_2", Url("crate_food_20.png"), "assets/bundled/prefabs/radtown/foodbox.prefab", "assets/bundled/prefabs/autospawn/resource/loot/trash-pile-1.prefab"),
                    new("crate_minecart", Url("minecart0.png"), "assets/bundled/prefabs/radtown/minecart.prefab"),
                    new("crate_vehicle_parts", Url("vp0.png"), "assets/bundled/prefabs/radtown/vehicle_parts.prefab"),

                    // 11 Diving
                    new("crate_underwater_basic", Url("crate_basic0.png"), "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab"),
                    new("crate_underwater_advanced", Url("adv_underwater0.png"), "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab"),               

                    // 13 Barrels & Roadsign
                    new("barrel", Url("barrel0.png"),
                        "assets/bundled/prefabs/radtown/loot_barrel_1.prefab", "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
                        "assets/bundled/prefabs/radtown/loot_barrel_2.prefab", "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"
                    ),
                    new("barrel_oil", Url("barrel_oil0.png"), "assets/bundled/prefabs/radtown/oil_barrel.prefab"),
                    new("roadsign", Url("road_signs.png"),
                        "assets/content/props/roadsigns/roadsign1.prefab", "assets/content/props/roadsigns/roadsign2.prefab",
                        "assets/content/props/roadsigns/roadsign3.prefab", "assets/content/props/roadsigns/roadsign4.prefab",
                        "assets/content/props/roadsigns/roadsign5.prefab", "assets/content/props/roadsigns/roadsign6.prefab",
                        "assets/content/props/roadsigns/roadsign7.prefab", "assets/content/props/roadsigns/roadsign8.prefab",
                        "assets/content/props/roadsigns/roadsign9.prefab"
                    ),

                    // 16 Special
                    new("crate_hackable", Url("crate_hackable0.png"), 18, 36, "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab"),
                    new("crate_hackable_oilrig", Url("crate_hackable0.png"), 18, 36, "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab" ),
                    new("crate_apc", Url("crate_t30.png"), "assets/prefabs/npc/m2bradley/bradley_crate.prefab" ),
                    new("crate_heli", Url("dm_t20.png"), "assets/prefabs/npc/patrol helicopter/heli_crate.prefab" ),
                    new("crate_supplydrop", Url("spl0.png"), 18, 18, "assets/prefabs/misc/supply drop/supply_drop.prefab" ),

                    // 21 Invisible
                    new("crate_basic_invisible", Url("crate_basic0.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_crate_basic.prefab"),
                    new("crate_normal_invisible", Url("cratecostume_512.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_crate_normal_2.prefab"),
                    new("crate_military_invisible", Url("dm_t20.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_crate_normal.prefab"),
                    new("crate_elite_invisible", Url("crate_t30.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_crate_elite.prefab"),
                    new("crate_food_invisible", Url("crate_food0.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_crate_normal_2_food.prefab"),
                    new("crate_medical_invisible", Url("crate_medical0.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_crate_normal_2_medical.prefab"),
                    new("crate_tools_invisible", Url("crate_tools0.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_crate_tools.prefab"),
                    new("crate_food_2_invisible", Url("crate_food_20.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_foodbox.prefab"),
                    new("crate_vehicle_parts_invisible", Url("vp0.png"), "assets/bundled/prefabs/modding/lootables/invisible/invisible_lootable_prefabs/invisible_vehicle_parts.prefab"),
                    new("crate_hackable_invisible", Url("crate_hackable0.png"), 18, 36, "assets/bundled/prefabs/modding/asset_store/hiddenhackablecrate.prefab"),

                    // 31 Underwater
                    new("crate_basic_underwater", Url("normal_uw0.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab"),
                    new("crate_military_underwater", Url("military_uw0.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab"),
                    new("crate_elite_underwater", Url("crate_t30.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab"),
                    new("crate_ammo_underwater", Url("ammo_uw0.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab"),
                    new("crate_food_underwater", Url("food_uw0.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab", "assets/bundled/prefabs/radtown/underwater_labs/crate_food_2.prefab"),
                    new("crate_fuel_underwater", Url("fuel_uw0.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_fuel.prefab"),
                    new("crate_medical_underwater", Url("meds_uw0.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab"),
                    new("crate_tools_underwater", Url("crate_tools0.png"), "assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab"),
                    new("crate_techparts_underwater", Url("techparts_uw0.png"), "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab", "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_2.prefab"),
                    new("crate_vehicle_parts_underwater", Url("vp0.png"), "assets/bundled/prefabs/radtown/underwater_labs/vehicle_parts.prefab"),


                    // 41 Deathmatch
                    new("dm_ammo", Url("dm_ammo0.png"), "assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab"),
                    new("dm_c4", Url("dm_c40.png"), "assets/bundled/prefabs/radtown/dmloot/dm c4.prefab"),
                    new("dm_construction_resources", null, "assets/bundled/prefabs/radtown/dmloot/dm construction resources.prefab"),
                    new("dm_construction_tools", null, "assets/bundled/prefabs/radtown/dmloot/dm construction tools.prefab"),
                    new("dm_food", Url("crate_food0.png"), "assets/bundled/prefabs/radtown/dmloot/dm food.prefab" ),
                    new("dm_medical", Url("crate_medical0.png"), "assets/bundled/prefabs/radtown/dmloot/dm medical.prefab" ),
                    new("dm_resources", null, "assets/bundled/prefabs/radtown/dmloot/dm res.prefab"),
                    new("dm_tier1", null, "assets/bundled/prefabs/radtown/dmloot/dm tier1 lootbox.prefab"),
                    new("dm_tier2", Url("dm_t20.png"), "assets/bundled/prefabs/radtown/dmloot/dm tier2 lootbox.prefab" ),
                    new("dm_tier3", Url("crate_t30.png"), "assets/bundled/prefabs/radtown/dmloot/dm tier3 lootbox.prefab" ),

                    // 51 Train Wagons
                    new("wagon_crate_normal", Url("cratecostume_512.png"), "assets/content/vehicles/trains/wagons/subents/wagon_crate_normal_2.prefab"),
                    new("wagon_crate_military", Url("dm_t20.png"), "assets/content/vehicles/trains/wagons/subents/wagon_crate_normal.prefab"),
                    new("wagon_crate_food", Url("crate_food0.png"), "assets/content/vehicles/trains/wagons/subents/wagon_crate_normal_2_food.prefab"),
                    new("wagon_crate_medical", Url("crate_medical0.png"), "assets/content/vehicles/trains/wagons/subents/wagon_crate_normal_2_medical.prefab"),
                    
                    // 55 Food cache
                    new ("crate_food_cache", null, "assets/prefabs/misc/food cache/food_cache_001.prefab", "assets/prefabs/misc/food cache/food_cache_002.prefab",
                        "assets/prefabs/misc/food cache/food_cache_003.prefab", "assets/prefabs/misc/food cache/food_cache_004.prefab", "assets/prefabs/misc/food cache/food_cache_005.prefab")
                }
            };

            list.AddTab("crates", "crates_normal", list.CrateData, 0, 10);
            list.AddTab("crates", "crates_diving", list.CrateData, 11, 12);
            list.AddTab("crates", "crates_roadside", list.CrateData, 13, 15);
            list.AddTab("crates", "crates_special", list.CrateData, 16, 20);
            list.AddTab("crates", "crates_food", list.CrateData, 55, 55);

            list.AddTab("npcs", list.NpcData);

            list.AddTab("collectibles", list.CollectableData);

            list.AddTab("gathering", "ore", list.DispenserData, 0, 2);
            list.AddTab("gathering", "trees", list.DispenserData, 3, 6);
            list.AddTab("gathering", "logs", list.DispenserData, 7, 8);
            list.AddTab("gathering", "corpses", list.DispenserData, 9, 15);
            list.AddTab("gathering", "special", list.DispenserData, 16, 17);

            list.AddTab("quarries", list.OtherData, 0, 4);
            list.AddTab("quarries", "excavator", list.OtherData, 5, 8);
            //list.AddTab("quarries", "train_wagons", list.OtherData, 9, 12);

            list.AddTab("unwrapable", list.UnwrapableData);

            list.AddTab("crates_other", "crates_underwater", list.CrateData, 31, 40);
            list.AddTab("crates_other", "crates_deathmatch", list.CrateData, 41, 50);
            list.AddTab("crates_other", "crates_invisible", list.CrateData, 21, 30);
            list.AddTab("crates_other", "crates_train", list.CrateData, 51, 54);

            return list;
        }
    }
}
namespace Oxide.Plugins.LoottableExtensions
{
    public static class LoottableExtensions
    {
        /// <summary>
        /// Clear the container and remove items
        /// </summary>
        public static void ClearItems(this ItemContainer container)
        {
            container.Clear();
            ItemManager.DoRemoves();
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var buffer = Pool.Get<List<T>>();
            buffer.AddRange(source);

            for (int i = 0; i < buffer.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }

            Pool.FreeUnmanaged(ref buffer);
        }

        public static IEnumerable<T> GetRandom<T>(this IList<T> list, int count)
        {
            if (list.Count < 1)
            {
                throw new InvalidOperationException("Can not take random element from empty list");
            }

            if (list.Count <= count)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    yield return list[i];
                }

                yield break;
            }

            var returned = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var chance = (float)(count - returned) / (list.Count - i);
                if (UnityEngine.Random.Range(0f, 1f) <= chance)
                {
                    returned++;
                    yield return list[i];
                }
            }
        }

        public static T Next<T>(this T src) where T : struct, Enum
        {
            var values = Enum.GetValues(src.GetType());
            int nextIdx = Array.IndexOf(values, src) + 1;
            nextIdx %= values.Length;
            return (T)values.GetValue(nextIdx)!;
        }

        public static void RemoveConflictingSlots(this Item dis, ItemContainer container, BaseEntity entityOwner, BasePlayer sourcePlayer)
        {
            dis.CallMethod("RemoveConflictingSlots", container, entityOwner, sourcePlayer);
        }

        public static bool IsOneOf<T>(this T value, params T[] values) where T : struct
        {
            if (!typeof(T).IsEnum)
            {
                throw new NotSupportedException("Only enums are supported");
            }

            foreach (var val in values)
            {
                if (value.Equals(val))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
namespace Oxide.Plugins.LoottableLegacy
{
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
    public class FurnaceConfiguration
    {
        [JsonProperty("enabled")]
        public bool enabled = false;
        [JsonProperty("recycler")]
        public bool recyclerEnabled = false;
        [JsonProperty("smallFurnaceSpeed")]
        public float sFsmeltingSpeedMultiplier = 1f;
        [JsonProperty("largeFurnaceSpeed")]
        public float lFsmeltingSpeedMultiplier = 1f;
        [JsonProperty("refinerySpeed")]
        public float rFsmeltingSpeedMultiplier = 1f;
        [JsonProperty("campfireSpeed")]
        public float campfireSmeltingSpeedMultiplier = 1f;
        [JsonProperty("bbqSpeed")]
        public float bbqSmeltingSpeedMultiplier = 1f;
        [JsonProperty("electricSpeed")]
        public float electricFurnaceSpeed = 1f;

        [JsonProperty("recyclerSpeed")]
        public float recyclingSpeedMultiplier = 1f;

        [JsonProperty("mixingSpeed")]
        public float mixingSpeedMultiplier = 1f;

        [JsonProperty("charcoalChance")]
        public float charcoalChance = 0.75f;
        [JsonProperty("charcoalAmount")]
        public int charcoalAmount = 1;
    }
    
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
    public class AirDropConfiguration
    {
        [JsonProperty("enabled")]
        public bool enabled = false;
        [JsonProperty("planeSpeed")]
        public float planeSpeedMultiplier = 1f;
        [JsonProperty("planeHeight")]
        public int planeHeight = 0;
        [JsonProperty("smokeDuration")]
        public float smokeDuration = 60f;
        [JsonProperty("exactDropPosition")]
        public bool exactDropPosition = false;
        [JsonProperty("dropFallSpeed")]
        public float dropFallSpeed = 2f;
        [JsonProperty("dropFallSmoke")]
        public bool dropFallSmoke = false;
        [JsonProperty("randomPosTolerance")]
        public float randomPosTolerance = 20f;
    }
    
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    public class CustomItemData
    {
        [JsonProperty("persistent")]
        public List<CustomItem> persistent = new();
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class CustomItem
    {
        public string plugin;
        public int itemId;
        public ulong skinId;
        public string customName;
        public bool persistent;
    }
    
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class CollectibleConfig
    {
        public bool IsValid => collectible > 0;

        [JsonProperty("enabled")]
        public bool enabled;
        [JsonProperty("type")]
        public int collectible;
        [JsonProperty("items")]
        public List<LootItem> items = new();
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    public class StacksizeConfig
    {
        [JsonProperty("ssc")]
        public Dictionary<int, int> itemStacksize = new();
        [JsonProperty("ssv")]
        public Dictionary<int, int> vanillaStacksize = new();
        [JsonProperty("vanilla")]
        public bool enabled;
        [JsonProperty("catMpl")]
        public Dictionary<int, float> categoryMultipliers = new();
        [JsonProperty("multiplier")]
        public float globalMultiplier = 1f;
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class LootableConfig
    {
        public enum LootType { Custom, BlackList, Addition }

        [JsonProperty("enabled")]
        public bool enabled;
        //[JsonProperty("type")]
        //public LootManager.Lootables crate { get; protected set; }
        [JsonProperty("amount")]
        public MinMax item_amount;
        [JsonProperty("categories")]
        public Dictionary<int, LootCategory> categories;
        [JsonProperty("items")]
        public List<LootItem> items;
        //[JsonProperty("seed")]
        //public uint seed;
        [JsonProperty("blacklist")]
        public List<BlacklistItem> blacklist;
        [JsonProperty("addtions")]
        public List<LootItem> additions;
        [JsonProperty("loottype")]
        public LootType lootType;
    }

    [Serializable]
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class LootCategory
    {
        [JsonProperty("id")]
        public int id { get; private set; }
        [JsonProperty("amount")]
        public MinMax itemAmount;
        [JsonProperty("chance")]
        public float chance;
    }

    [Serializable]
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class LootItem
    {
        [JsonProperty("itemid")]
        public int itemid;
        [JsonProperty("amount")]
        public MinMax amount;
        [JsonProperty("chance")]
        public float chance;
        [JsonProperty("skin")]
        public ulong skin;
        [JsonProperty("category")]
        public int category;
        [JsonProperty("displayname")]
        public string displayname;
        [JsonProperty("work")]
        public float work;
        [JsonProperty("liquid")]
        public bool liquid;
        [JsonProperty("extras")]
        public List<ExtraItem> extras;
        [JsonProperty("condition")]
        public MinMaxFloat condition;
        [JsonProperty("blueprint")]
        public bool isBlueprint;
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class ExtraItem
    {
        [JsonProperty("itemid")]
        public int itemid;
        [JsonProperty("amount")]
        public MinMax amount;
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class BlacklistItem
    {
        [JsonProperty("itemid")]
        public int itemid;
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public struct MinMax
    {
        [JsonProperty("min")]
        public int min;
        [JsonProperty("max")]
        public int max;
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public struct MinMaxFloat
    {
        [JsonProperty("min")]
        public float min;
        [JsonProperty("max")]
        public float max;
    }
}
namespace PluginComponents.Loottable{using JetBrains.Annotations;using Oxide.Plugins;using System;[AttributeUsage(AttributeTargets.Field,AllowMultiple=false),MeansImplicitUse]public sealed class PermAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,AllowMultiple=false),MeansImplicitUse]public sealed class UniversalCommandAttribute:Attribute{public UniversalCommandAttribute(string name){Name=name;}public string Name{get;set;}public string Permission{get;set;}}[AttributeUsage(AttributeTargets.Method),MeansImplicitUse]public sealed class HookAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,Inherited=false)]public sealed class DebugAttribute:Attribute{}public class MinMaxInt{public int min;public int max;public MinMaxInt(){}public MinMaxInt(int value):this(value,value){}public MinMaxInt(int min,int max){this.min=min;this.max=max;}public int Random(){return UnityEngine.Random.Range(min,max+1);}}}namespace PluginComponents.Loottable.Assertions{using System;using System.Runtime.CompilerServices;using PluginComponents.Loottable;
#if IDE_ONLY
internal class CallerArgumentExpressionAttribute:Attribute{public CallerArgumentExpressionAttribute(string argumentName){}}
#endif
public static class Assert{public static void True(bool value,string message=null,[CallerArgumentExpression(nameof(value))]string valExp=""){if(!value){throw new AssertionException($"'{valExp}' must be true. {message}");}}public static void False(bool value,string message=null,[CallerArgumentExpression(nameof(value))]string valExp=""){if(value){throw new AssertionException($"'{valExp}' must be false. {message}");}}public static void NotNull(object value,string message=null,[CallerArgumentExpression(nameof(value))]string valExp=""){if(value==null){throw new AssertionException($"'{valExp}' is null. {message}");}}public static void Equals<T>(T expected,T actual,string message=null)where T:IEquatable<T>{if(!expected.Equals(actual)){throw new AssertionException($"{message??"Values are not equal"}. expected<{expected}> actual<{actual}>");}}public static void Fail(string message){throw new AssertionException(message);}}public class AssertionException:Exception{public AssertionException(string message):base(message){}public override string ToString(){return$"AssertionException: {Message}\n{StackTrace}";}}}namespace PluginComponents.Loottable.Core{using Oxide.Core.Plugins;using Oxide.Core;using Oxide.Plugins;using Newtonsoft.Json;using System.IO;using UnityEngine;using System;using System.Diagnostics;using System.Collections.Generic;using System.Linq;using Facepunch.Extend;using System.Reflection;using PluginComponents.Loottable;public abstract class BasePlugin<TPlugin,TConfig>:BasePlugin<TPlugin>where TConfig:class,new()where TPlugin:RustPlugin{protected new static TConfig Config{get;private set;}private string ConfigPath=>Path.Combine(Interface.Oxide.ConfigDirectory,$"{Name}.json");protected override void LoadConfig()=>ReadConfig();protected override void SaveConfig()=>WriteConfig();protected override void LoadDefaultConfig()=>Config=new TConfig();private void ReadConfig(){if(File.Exists(ConfigPath)){Config=JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(ConfigPath));if(Config==null){LogError("[CONFIG] Your configuration file contains an error. Using default configuration values.");LoadDefaultConfig();}}else{LoadDefaultConfig();}WriteConfig();}private void WriteConfig(){var directoryName=Utility.GetDirectoryName(ConfigPath);if(directoryName!=null&&!Directory.Exists(directoryName)){Directory.CreateDirectory(directoryName);}if(Config!=null){string text=JsonConvert.SerializeObject(Config,Formatting.Indented);File.WriteAllText(ConfigPath,text);}else{LogError("[CONFIG] Saving failed - config is null");}}}public abstract class BasePlugin<TPlugin>:BasePlugin where TPlugin:RustPlugin{public new static TPlugin Instance{get;private set;}protected static string DataFolder=>Path.Combine(Interface.Oxide.DataDirectory,typeof(TPlugin).Name);protected override void Init(){base.Init();Instance=this as TPlugin;}protected override void Unload(){Instance=null;base.Unload();}}public abstract class BasePlugin:RustPlugin{public const int OSI_DELAY=5;public const bool CARBONARA=
#if CARBON
true;
#else
false;
#endif
public const bool DEBUG=
#if DEBUG
true;
#else
false;
#endif
public static BasePlayer DebugPlayer=>DEBUG?BasePlayer.activePlayerList.FirstOrDefault(x=>!x.IsNpc):null;public static string PluginName=>Instance?.Name??"NULL";public static BasePlugin Instance{get;private set;}protected virtual UnityEngine.Color ChatColor=>default;protected virtual string ChatPrefix=>ChatColor!=default?$"<color=#{ColorUtility.ToHtmlStringRGB(ChatColor)}>[{Title}]</color>":$"[{Title}]";[HookMethod("Init")]protected virtual void Init(){Instance=this;foreach(var field in GetType().GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)){if(field.IsLiteral&&!field.IsInitOnly&&field.FieldType==typeof(string)&&field.HasAttribute(typeof(PermAttribute))){if(field.GetValue(null)is string perm){LogDebug($"Auto-registered permission '{perm}'");permission.RegisterPermission(perm,this);}}}foreach(var method in GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)){if(method.GetCustomAttributes(typeof(UniversalCommandAttribute),true).FirstOrDefault()is UniversalCommandAttribute attribute){var commandName=attribute.Name??method.Name.ToLower().Replace("cmd",string.Empty);if(attribute.Permission!=null){LogDebug($"Auto-registered command '{commandName}' with permission '{attribute.Permission??"<null>"}'");}else{LogDebug($"Auto-registered command '{commandName}'");}AddUniversalCommand(commandName,method.Name,attribute.Permission);}}}[HookMethod("Unload")]protected virtual void Unload(){Instance=null;}[HookMethod("OnServerInitialized")]protected virtual void OnServerInitialized(bool initial){if(!CARBONARA){OnServerInitialized();}timer.In(OSI_DELAY,OnServerInitializedDelayed);}
#if CARBON
[HookMethod("OnServerInitialized")]
#endif
protected virtual void OnServerInitialized(){}protected virtual void OnServerInitializedDelayed(){}public static void Log(string s){if(Instance!=null){Interface.Oxide.LogInfo($"[{Instance.Title}] {s}");}}[Conditional("DEBUG")]public static void LogDebug(string s){if(DEBUG&&Instance!=null){if(CARBONARA){LogWarning("[DEBUG] "+s);}else{Interface.Oxide.LogDebug($"[{Instance.Title}] {s}");}}}public static void LogWarning(string s){if(Instance!=null){Interface.Oxide.LogWarning($"[{Instance.Title}] {s}");}}public static void LogError(string s){if(Instance!=null){Interface.Oxide.LogError($"[{Instance.Title}] {s}");}}private Dictionary<string,CommandCallback>uiCallbacks;private string uiCommandBase;private void PrepareCommandHandler(){if(uiCallbacks==null){uiCallbacks=new();uiCommandBase=$"{Title.ToLower()}.uicmd";cmd.AddConsoleCommand(uiCommandBase,this,HandleCommand);}}private bool HandleCommand(ConsoleSystem.Arg arg){var cmd=arg.GetString(0);if(uiCallbacks.TryGetValue(cmd,out var callback)){var player=arg.Player();try{callback.ButtonCallback?.Invoke(player);callback.InputCallback?.Invoke(player,string.Join(' ',arg.Args?.Skip(1)??Enumerable.Empty<string>()));}catch(Exception ex){PrintError($"Failed to run UI command {cmd}: {ex}");}}return false;}public string CreateUiCommand(string guid,Action<BasePlayer>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}public string CreateUiCommand(string guid,Action<BasePlayer,string>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}private readonly struct CommandCallback{public readonly bool SingleUse;public readonly Action<BasePlayer>ButtonCallback;public readonly Action<BasePlayer,string>InputCallback;public CommandCallback(Action<BasePlayer>buttonCallback,bool singleUse){ButtonCallback=buttonCallback;InputCallback=null;SingleUse=singleUse;}public CommandCallback(Action<BasePlayer,string>inputCallback,bool singleUse){ButtonCallback=null;InputCallback=inputCallback;SingleUse=singleUse;}}public void ChatMessage(BasePlayer player,string message){if(player){player.SendConsoleCommand("chat.add",2,0,$"{ChatPrefix} {message}");}}}}namespace PluginComponents.Loottable.Cui{using System;using UnityEngine;using PluginComponents.Loottable;using Facepunch;using Oxide.Game.Rust.Cui;using PluginComponents.Loottable.Core;using PluginComponents.Loottable.Cui.Style;using System.Collections.Generic;using UnityEngine.UI;public readonly record struct Anchor{public float XMin{get;init;}public float XMax{get;init;}public float YMin{get;init;}public float YMax{get;init;}public float OffsetXMin{get;init;}public float OffsetXMax{get;init;}public float OffsetYMin{get;init;}public float OffsetYMax{get;init;}internal readonly string Min()=>$"{XMin:N3} {YMin:N3}";internal readonly string Max()=>$"{XMax:N3} {YMax:N3}";internal readonly string OffsetMin()=>$"{OffsetXMin:N3} {OffsetYMin:N3}";internal readonly string OffsetMax()=>$"{OffsetXMax:N3} {OffsetYMax:N3}";public Anchor WithOffsetY(float offset){return this with{YMax=YMax+offset,YMin=YMin+offset};}public Anchor WithOffset(float xMin=0,float xMax=0,float yMin=0,float yMax=0){return this with{XMin=XMin+xMin,XMax=XMax+xMax,YMin=YMin+yMin,YMax=YMax+yMax,};}public override string ToString(){return$"(X: {XMin}-{XMax}, Y: {YMin}-{YMax})";}public static readonly Anchor Fill=Relative(0,1,0,1);public static Anchor Padding(float horizontal,float vertical){return Relative(horizontal,1-horizontal,vertical,1-vertical);}public static Anchor Relative(float xMin,float xMax,float yMin,float yMax,float offsetXMin=0,float offsetXMax=0,float offsetYMin=0,float offsetYMax=0){return new Anchor{XMin=xMin,XMax=xMax,YMin=yMin,YMax=yMax,OffsetXMin=offsetXMin,OffsetXMax=offsetXMax,OffsetYMin=offsetYMin,OffsetYMax=offsetYMax};}public static Anchor Absolute(float centerX,float centerY,float xMin,float xMax,float yMin,float yMax){return new Anchor{XMin=centerX,XMax=centerX,YMin=centerY,YMax=centerY,OffsetXMin=xMin,OffsetXMax=xMax,OffsetYMin=yMin,OffsetYMax=yMax};}public static Anchor AbsoluteCentered(float centerX,float centerY,float height,float width){return new Anchor{XMin=centerX,XMax=centerX,YMin=centerY,YMax=centerY,OffsetXMin=-width/2f,OffsetXMax=width/2f,OffsetYMin=-height/2f,OffsetYMax=height/2f};}public static Anchor MoveRow(ref MovingAnchorProperties p,int max=-1){var anchor=p.Anchor();p.StepColumn(max);return anchor;}public static Anchor MoveColumn(ref MovingAnchorProperties p,int max=-1){var anchor=p.Anchor();p.StepRow(max);return anchor;}public static Anchor MoveOffsetY(ref MovingAnchorProperties p,float offset,bool breakRow=false){var anchor=p.Anchor();p.offsetY+=offset;if(breakRow){p.NextRow(true);}return anchor with{YMin=anchor.YMin+offset};}public static Anchor MoveX(ref MovingAnchorProperties p,float offsetX,bool resetOnNewRow){var anchor=p.Anchor();anchor=anchor with{XMax=anchor.XMin+offsetX};p.offsetX+=offsetX+p.SpaceX;p.resetOffset=resetOnNewRow;return anchor;}public static implicit operator Anchor(in ValueTuple<float,float,float,float>tuple){return Relative(tuple.Item1,tuple.Item2,tuple.Item3,tuple.Item4);}public static Anchor FillOffset(float xMin,float xMax,float yMin,float yMax){return new Anchor{XMin=0,YMin=0,XMax=1,YMax=1,OffsetXMin=xMin,OffsetXMax=xMax,OffsetYMin=yMin,OffsetYMax=yMax,};}public static float CenteredDistribution(int i,int count,float d=1,float pivot=0){var even=count%2==0;var s=i%2==0?-1:1;var x=Mathf.FloorToInt((i+(even?2:1))/2)*s*d-(even?s*d*0.5f:0);return pivot+x;}}public struct MovingAnchorProperties{public int row;public int column;public float offsetX;public float offsetY;public bool resetOffset;public float StartX{get;init;}public float StartY{get;init;}public float Width{get;init;}public float Height{get;init;}public float SpaceX{get;init;}public float SpaceY{get;init;}public void NextRow(bool force=false){if(column!=0||force){row++;column=0;ResetOffsets();}}public void NextColumn(bool force=false){if(row!=0||force){column++;row=0;ResetOffsets();}}public void StepRow(int max){row++;if(row>=max&&max>0){row=0;column++;ResetOffsets();}}public void StepColumn(int max){column++;if(column>=max&&max>0){column=0;row++;ResetOffsets();}}private void ResetOffsets(){if(resetOffset){offsetX=0;offsetY=0;resetOffset=false;}}public readonly float XMin(){return StartX+offsetX+SpaceX+column*Width;}public readonly float XMax(){return StartX+offsetX+(column+1)*Width;}public readonly float YMin(){return StartY+offsetY+SpaceY-(row+1)*Height;}public readonly float YMax(){return StartY+offsetY-row*Height;}public readonly Anchor Anchor(){return PluginComponents.Loottable.Cui.Anchor.Relative(XMin(),XMax(),YMin(),YMax());}public readonly bool CheckBoundsY(){return YMin()>=0&&YMax()<=1;}public static MovingAnchorProperties Create(float startX,float startY,float width,float height,float spaceX,float spaceY){return new MovingAnchorProperties{StartX=startX,StartY=startY,Width=width,Height=height,SpaceX=spaceX,SpaceY=spaceY};}}public class Cui:IDisposable{private readonly BasePlugin plugin;private readonly Stack<string>parents;private List<CuiElement>elements;private string Parent=>parents.Peek();public UiRef CurrentRef=>new UiRef(Parent);public UiRef RootRef{get;private set;}public Cui(BasePlugin plugin,string parent){this.plugin=plugin;elements=Pool.GetList<CuiElement>();parents=new Stack<string>();parents.Push(parent);RootRef=CurrentRef;}public void AddBox(in Anchor anchor,in UiColor color){CreateContainer(anchor,color,0).Dispose();}public IDisposable CreateContainer(in Anchor anchor)=>CreateContainer(anchor,UiColor.Black,0);public IDisposable CreateContainer(in Anchor anchor,in UiColor background)=>CreateContainer(anchor,background,0);public IDisposable CreateContainer(in Anchor anchor,in UiColor background,float fadeOutTime){var element=CuiPool.GetElement(Guid(),Parent,anchor);element.FadeOut=fadeOutTime;var img=CuiPool.GetComponent<CuiImageComponent>();img.Color=background;AddElement(element.WithComponent(img));return CreateParentScope(element.Name);}public IDisposable CreateContainer(in Anchor anchor,in UiColor background,out UiRef self){var container=CreateContainer(anchor,background);self=CurrentRef;return container;}public IDisposable CreateOrUpdateContainer(in Anchor anchor,in UiColor background,ref UiRef self){var element=CuiPool.GetElement(Guid(),Parent,anchor);if(self.IsValid){element.DestroyUi=self.Name;}var img=CuiPool.GetComponent<CuiImageComponent>();img.Color=background;AddElement(element.WithComponent(img));self=new UiRef(element.Name);return CreateParentScope(element.Name);}public IDisposable CreateScrollContainer(in Anchor anchor,in Anchor contentAnchor,in ScrollViewOptions options){var element=CuiPool.GetElement(Guid(),Parent,anchor);var scroll=CuiPool.GetComponent<CuiScrollViewComponent>();scroll.MovementType=options.ScrollType;scroll.Elasticity=options.Elasticity;scroll.Horizontal=options.HorizontalScrollBar!=null;scroll.HorizontalScrollbar=options.HorizontalScrollBar;scroll.Vertical=options.VerticalScrollBar!=null;scroll.VerticalScrollbar=options.VerticalScrollBar;scroll.ScrollSensitivity=options.ScrollSensitivity;scroll.ContentTransform=CuiPool.GetTransform(contentAnchor);AddElement(element.WithComponent(scroll));return CreateParentScope(element.Name);}public IDisposable CreateScrollContainer(in Anchor anchor,in Anchor contentAnchor,in ScrollViewOptions options,out UiRef self){var container=CreateScrollContainer(anchor,contentAnchor,options);self=CurrentRef;return container;}private void CreateRootContainer(in Anchor anchor,in UiColor background,float fadeOutTime,bool keyboard,bool mouse,string material){var element=CuiPool.GetElement(Guid(),Parent,anchor);element.FadeOut=fadeOutTime;var img=CuiPool.GetComponent<CuiImageComponent>();img.Color=background;img.Material=material;AddElement(element.WithComponent(img).WithCursor(mouse).WithKeyboard(keyboard));parents.Push(element.Name);RootRef=CurrentRef;}public void AddImage(in Anchor anchor,string png){var image=CuiPool.GetComponent<CuiRawImageComponent>();image.Png=png;var el=CuiPool.GetElement(Guid(),Parent,anchor);AddElement(el.WithComponent(image));}public void AddImageUrl(in Anchor anchor,string url){var image=CuiPool.GetComponent<CuiRawImageComponent>();image.Url=url;var el=CuiPool.GetElement(Guid(),Parent,anchor);AddElement(el.WithComponent(image));}public void AddItemImage(in Anchor anchor,int itemId)=>AddItemImage(anchor,itemId,0);public void AddItemImage(in Anchor anchor,int itemId,ulong skinId){var el=CuiPool.GetElement(Guid(),Parent,anchor);var image=CuiPool.GetComponent<CuiImageComponent>();image.ItemId=itemId;image.SkinId=skinId;AddElement(el.WithComponent(image));}public void AddLabel(in Anchor anchor,string text)=>AddLabel(anchor,text,LabelStyle.Default);public void AddLabel(in Anchor anchor,string text,in LabelStyle style){var element=CuiPool.GetElement(Guid(),Parent,anchor);var textComp=CuiPool.GetComponent<CuiTextComponent>();textComp.Text=text;textComp.Color=style.TextColor;textComp.Align=style.TextAnchor;textComp.Font=style.Font;textComp.FontSize=style.FontSize;if(style.Outline!=default){var outlineComp=CuiPool.GetComponent<CuiOutlineComponent>();outlineComp.Color=style.Outline;outlineComp.Distance=$"{style.OutlineDistance.x} {style.OutlineDistance.y}";element.WithComponent(outlineComp);}AddElement(element.WithComponent(textComp));}public void AddButton(in Anchor anchor,string text,Action<BasePlayer>callback,bool singleUse=true)=>AddButton(anchor,text,callback,ButtonStyle.Default,singleUse);public void AddButton(in Anchor anchor,string text,Action<BasePlayer>callback,in ButtonStyle style,bool singleUse=true)=>AddButton(anchor,text,callback,style.ButtonColor,style.TextStyle,singleUse);public void AddButton(in Anchor anchor,string text,Action<BasePlayer>callback,in UiColor color,in LabelStyle textStyle,bool singleUse=true){var buttonGuid=Guid();var buttonComp=CuiPool.GetComponent<CuiButtonComponent>();buttonComp.Color=color;buttonComp.Command=callback==null?null:plugin.CreateUiCommand(buttonGuid,callback,singleUse);var button=CuiPool.GetElement(buttonGuid,Parent,anchor);AddElement(button.WithComponent(buttonComp));var textComp=CuiPool.GetComponent<CuiTextComponent>();textComp.Text=text;textComp.Color=textStyle.TextColor;textComp.Align=textStyle.TextAnchor;textComp.Font=textStyle.Font;textComp.FontSize=textStyle.FontSize;var textElement=CuiPool.GetElement(Guid(),buttonGuid,Anchor.Fill);AddElement(textElement.WithComponent(textComp));}public void AddInputField(in Anchor anchor,string value,Action<BasePlayer,string>callback,InputFieldFlags flags=InputFieldFlags.None,bool singleUse=true)=>AddInputField(anchor,value,callback,LabelStyle.Default,flags,singleUse);public void AddInputField(in Anchor anchor,string value,Action<BasePlayer,string>callback,in LabelStyle style,InputFieldFlags flags=InputFieldFlags.None,bool singleUse=true){var guid=Guid();var inputComp=CuiPool.GetComponent<CuiInputFieldComponent>();inputComp.Command=plugin.CreateUiCommand(guid,callback,singleUse);inputComp.Text=value;inputComp.Color=style.TextColor;inputComp.Align=style.TextAnchor;inputComp.Font=style.Font;inputComp.FontSize=style.FontSize;inputComp.Autofocus=flags.HasFlag(InputFieldFlags.Autofocus);inputComp.ReadOnly=flags.HasFlag(InputFieldFlags.ReadOnly);inputComp.HudMenuInput=flags.HasFlag(InputFieldFlags.HudMenuInput);inputComp.IsPassword=flags.HasFlag(InputFieldFlags.Password);if(flags.HasFlag(InputFieldFlags.MultiLine)){inputComp.LineType=InputField.LineType.MultiLineNewline;}else if(flags.HasFlag(InputFieldFlags.WordWrap)){inputComp.LineType=InputField.LineType.MultiLineSubmit;}var element=CuiPool.GetElement(guid,Parent,anchor);AddElement(element.WithComponent(inputComp));}public UiRef Send(BasePlayer player)=>Send(player,default);public UiRef Send(BasePlayer player,UiRef old){if(old.IsValid&&elements.Count>0){elements[0].DestroyUi=old.Name;}CuiHelper.AddUi(player,elements);return new UiRef(Parent);}public void Send(BasePlayer player,ref UiRef containerRef){containerRef=Send(player,containerRef);}public static Cui Create(BasePlugin plugin,UiRef parent)=>Create(plugin,parent,null,0);public static Cui Create(BasePlugin plugin,UiRef parent,string material,float opacity){var cui=new Cui(plugin,parent.Name);cui.CreateRootContainer(Anchor.Fill,new UiColor(0,0,0,opacity),0,false,false,material);return cui;}public static Cui CreateWithoutContainer(BasePlugin plugin,UiParentLayer parent,UiFlags flags=UiFlags.None){var parentName=parent switch{UiParentLayer.Overlay=>"Overlay",UiParentLayer.OverlayNonScaled=>"OverlayNonScaled",UiParentLayer.Hud=>"Hud",UiParentLayer.Menu=>"Hud.Menu",_=>"Under"};return new Cui(plugin,parentName);}public static Cui Create(BasePlugin plugin,in Anchor anchor,in UiColor background,UiParentLayer parent,UiFlags flags=UiFlags.None,float fadeOutTime=0,string material=null){var parentName=parent switch{UiParentLayer.Overlay=>"Overlay",UiParentLayer.OverlayNonScaled=>"OverlayNonScaled",UiParentLayer.Hud=>"Hud",UiParentLayer.Menu=>"Hud.Menu",_=>"Under"};var cui=new Cui(plugin,parentName);cui.CreateRootContainer(anchor,background,fadeOutTime,flags.HasFlag(UiFlags.Keyboard),flags.HasFlag(UiFlags.Mouse),material);return cui;}public static void Destroy(BasePlayer player,UiRef cuiRef){CuiHelper.DestroyUi(player,cuiRef.Name);}public void Dispose(){CuiPool.FreeElements(elements);Pool.FreeList(ref elements);}private IDisposable CreateParentScope(string parent){parents.Push(parent);return DisposeAction.Create(()=>parents.Pop());}private void AddElement(CuiElement element){elements.Add(element);}private static string Guid(){return System.Guid.NewGuid().ToString();}private struct DisposeAction:IDisposable{public Action Action;public DisposeAction(Action action){Action=action;}public void Dispose(){Action?.Invoke();Action=null;}public static IDisposable Create(Action action){return new DisposeAction(action);}}}public readonly struct UiRef{internal string Name{get;init;}public bool IsValid{get;init;}internal UiRef(string name){Name=name;IsValid=true;}}public enum UiParentLayer{Overlay,Hud,Menu,Under,OverlayNonScaled}[Flags]public enum UiFlags{None=0,Mouse=1,Keyboard=2,MouseAndKeyboard=Mouse|Keyboard,}public enum InputFieldFlags{None=0,ReadOnly=1<<0,Password=1<<1,Autofocus=1<<2,HudMenuInput=1<<3,MultiLine=1<<4,WordWrap=1<<5,}public struct ScrollViewOptions{public CuiScrollbar VerticalScrollBar{get;set;}public CuiScrollbar HorizontalScrollBar{get;set;}public ScrollRect.MovementType ScrollType{get;set;}public float Elasticity{get;set;}public float ScrollSensitivity{get;set;}}internal static class CuiElementEx{private static CuiNeedsCursorComponent cursorComponent;private static CuiNeedsKeyboardComponent keyboardComponent;internal static CuiElement WithKeyboard(this CuiElement element,bool enabled=true){if(!enabled){return element;}keyboardComponent??=new CuiNeedsKeyboardComponent();element.Components.Add(keyboardComponent);return element;}internal static CuiElement WithCursor(this CuiElement element,bool enabled=true){if(!enabled){return element;}cursorComponent??=new CuiNeedsCursorComponent();element.Components.Add(cursorComponent);return element;}internal static CuiElement WithComponent(this CuiElement element,ICuiComponent component){element.Components.Add(component);return element;}}internal static class CuiPool{public static void FreeElements(List<CuiElement>elements){for(int i=0;i<elements.Count;i++){var el=elements[i];FreeElement(ref el);elements[i]=null;}}public static CuiElement GetElement(string name,string parent,Anchor anchor){var el=Pool.Get<CuiElement>();el.Name=name;el.Parent=parent;el.Components.Add(GetTransform(anchor));return el;}public static CuiRectTransformComponent GetTransform(Anchor anchor){var transform=Pool.Get<CuiRectTransformComponent>();transform.AnchorMin=anchor.Min();transform.AnchorMax=anchor.Max();transform.OffsetMin=anchor.OffsetMin();transform.OffsetMax=anchor.OffsetMax();return transform;}public static T GetComponent<T>()where T:class,ICuiComponent,new(){return Pool.Get<T>();}public static void FreeElement(ref CuiElement element){for(int i=0;i<element.Components.Count;i++){var comp=element.Components[i];if(comp is CuiRectTransformComponent rect){FreeComponent(ref rect);}else if(comp is CuiImageComponent image){FreeComponent(ref image);}else if(comp is CuiRawImageComponent rawImage){FreeComponent(ref rawImage);}else if(comp is CuiTextComponent text){FreeComponent(ref text);}else if(comp is CuiButtonComponent button){FreeComponent(ref button);}element.Components[i]=null;}element.Components.Clear();element.DestroyUi=default;element.FadeOut=default;element.Name=default;element.Parent=default;element.Update=default;Pool.FreeUnsafe(ref element);}public static void FreeComponent<T>(ref T component)where T:class,ICuiComponent,new(){if(component is CuiNeedsCursorComponent||component is CuiNeedsKeyboardComponent){return;}foreach(var prop in component.GetType().GetProperties()){if(prop.CanWrite){prop.SetValue(component,null);}}Pool.FreeUnsafe(ref component);}}}namespace PluginComponents.Loottable.Cui.Style{using System;using UnityEngine;using PluginComponents.Loottable;using PluginComponents.Loottable.Cui;public readonly record struct ButtonStyle{public UiColor ButtonColor{get;init;}public LabelStyle TextStyle{get;init;}public static ButtonStyle Default{get;}=new ButtonStyle{ButtonColor=Color.blue,TextStyle=LabelStyle.Default};public static implicit operator ButtonStyle(in ValueTuple<UiColor,LabelStyle>valueTuple){return new ButtonStyle{ButtonColor=valueTuple.Item1,TextStyle=valueTuple.Item2};}}public readonly record struct LabelStyle{public TextAnchor TextAnchor{get;init;}public UiColor TextColor{get;init;}public UiFont Font{get;init;}public int FontSize{get;init;}public UiColor Outline{get;init;}public Vector2 OutlineDistance{get;init;}public LabelStyle(){Font=UiFont.robotoRegular;TextColor=UiColor.White;OutlineDistance=new Vector2(1,-1);}public static readonly LabelStyle Default=new LabelStyle{TextAnchor=TextAnchor.MiddleCenter,TextColor=Color.white,Font=UiFont.robotoRegular,FontSize=12};}public readonly record struct UiColor{public float R{get;init;}public float G{get;init;}public float B{get;init;}public float Opacity{get;init;}public UiColor(float r,float g,float b,float opacity=1f){R=r;G=g;B=b;Opacity=opacity;}public static implicit operator Color(UiColor color){return new Color(color.R,color.G,color.B,color.Opacity);}public static implicit operator UiColor(Color color){return new UiColor(color.r,color.g,color.b,color.a);}public static implicit operator string(UiColor color){return color.ToString();}public static UiColor operator*(UiColor color,float f){return new UiColor(Mathf.Clamp01(color.R*f),Mathf.Clamp01(color.G*f),Mathf.Clamp01(color.B*f),color.Opacity);}public override string ToString(){return$"{R:N3} {G:N3} {B:N3} {Opacity:N3}";}public static readonly UiColor Transparent=new UiColor(0f,0f,0f,0f);public static readonly UiColor White=new UiColor(1f,1f,1f,1f);public static readonly UiColor Black=new UiColor(0f,0f,0f,1f);}public class UiFont{public static readonly UiFont robotoRegular=new UiFont("robotocondensed-regular.ttf");public static readonly UiFont robotoBold=new UiFont("robotocondensed-bold.ttf");public static readonly UiFont permanentMarker=new UiFont("permanentmarker.ttf");public static readonly UiFont droidSansMono=new UiFont("droidsansmono.ttf");public string ResourceFile{get;}private UiFont(string resourceFile){ResourceFile=resourceFile;}public static implicit operator string(UiFont font){return font.ResourceFile;}}}namespace PluginComponents.Loottable.Cui.Images{using PluginComponents.Loottable;using PluginComponents.Loottable.Cui;using Oxide.Core.Plugins;using PluginComponents.Loottable.Core;using PluginComponents.Loottable.DoubleDictionary;using System;using System.Collections.Generic;using System.Text;public interface IImage{string ImageUrl{get;}string ImageKey{get;set;}}public class ImageHelper{public readonly string PlaceholderKey;private readonly BasePlugin plugin;public bool Update{get;set;}public bool Initialized{get;private set;}
#if CARBON
private readonly Carbon.Modules.ImageDatabaseModule imageDatabase;
#endif
private Plugin ImageLibrary=>plugin.Manager.GetPlugin("ImageLibrary");private readonly DoubleDictionary<string,string>imageKeyToUrl=new();public ImageHelper(BasePlugin plugin){this.plugin=plugin;PlaceholderKey=$"{plugin.Name}_placeholder";
#if CARBON
imageDatabase=Carbon.Base.BaseModule.GetModule<Carbon.Modules.ImageDatabaseModule>();
#endif
}public void AddImages(IEnumerable<IImage>imageList,bool overwrite=false){if(Initialized){throw new InvalidOperationException("Cannot bulk add images when already initialized");}foreach(var img in imageList){if(img.ImageKey==null||img.ImageUrl==null){continue;}if(imageKeyToUrl.ContainsKey(img.ImageKey)&&overwrite){imageKeyToUrl.Set(img.ImageKey,img.ImageUrl);}else if(!imageKeyToUrl.ContainsKey(img.ImageKey)){if(imageKeyToUrl.TryGetKey(img.ImageUrl,out var key)){img.ImageKey=key;}else{imageKeyToUrl.Add(img.ImageKey,img.ImageUrl);}}}}public void Init(){Initialized=true;BasePlugin.LogDebug($"Init ImageHelper {BasePlugin.PluginName} Carbon: {BasePlugin.CARBONARA} ImageLibrary: {ImageLibrary!=null} Images: {imageKeyToUrl.Count} Update: {Update}");if(!BasePlugin.CARBONARA&&ImageLibrary==null){BasePlugin.LogError("ImageLibrary is required to display images. You can download it here: https://umod.org/plugins/image-library");return;}imageKeyToUrl.Add(PlaceholderKey,"https://i.imgur.com/tptafCV.png");
#if CARBON
imageDatabase.Queue(Update,imageKeyToUrl.ToKeyValueDictionary());
#else
ImageLibrary.Call("ImportImageList",BasePlugin.PluginName,imageKeyToUrl.ToKeyValueDictionary(),0UL,Update);
#endif
}public string GetPng(IImage image){return GetPng(image.ImageKey);}public string GetPng(string key){if(!imageKeyToUrl.ContainsKey(key)){BasePlugin.LogWarning($"Failed to get image '{key}' - Image does not exist");return key!=PlaceholderKey?GetPng(PlaceholderKey):"0";}
#if CARBON
return imageDatabase.GetImageString(key);
#else
return ImageLibrary?.Call<string>("GetImage",key);
#endif
}public bool HasImage(string key){return imageKeyToUrl.ContainsKey(key);}public string AddImage(string key,string url){if(imageKeyToUrl.TryGetKey(url,out var imageKey)){return imageKey;}imageKeyToUrl.Add(key,url);if(Initialized){
#if CARBON
imageDatabase.AddMap(key,url);var list=Pool.GetList<string>();list.Add(url);imageDatabase.QueueBatch(true,list);Pool.FreeList(ref list);
#else
ImageLibrary?.Call("AddImage",url,key,0UL);
#endif
}return key;}public IEnumerable<KeyValuePair<string,string>>GetImageList(){return imageKeyToUrl;}public void PrintImageList(){var sb=new StringBuilder();foreach(var img in imageKeyToUrl){sb.AppendLine($"{img.Key,20} {img.Value}");}BasePlugin.Log(sb.ToString());}}}namespace PluginComponents.Loottable.DoubleDictionary{using System;using System.Collections;using System.Collections.Generic;using System.Linq;using PluginComponents.Loottable;public class DoubleDictionary<TKey,TVal>:IEnumerable<KeyValuePair<TKey,TVal>>{public int Count=>keyToVal.Count;public IReadOnlyCollection<TKey>Keys=>keyToVal.Keys;public IReadOnlyCollection<TVal>Values=>keyToVal.Values;private readonly Dictionary<TKey,TVal>keyToVal;private readonly Dictionary<TVal,TKey>valToKey;public DoubleDictionary(){keyToVal=new Dictionary<TKey,TVal>();valToKey=new Dictionary<TVal,TKey>();}public void Add(TKey key,TVal value){if(!TryAdd(key,value)){throw new InvalidOperationException($"Key {key} or {value} already exists");}}public bool TryAdd(TKey key,TVal value){if(keyToVal.ContainsKey(key)||valToKey.ContainsKey(value)){return false;}keyToVal.Add(key,value);valToKey.Add(value,key);return true;}public bool ContainsKey(TKey key){return keyToVal.ContainsKey(key);}public bool ContainsValue(TVal value){return valToKey.ContainsKey(value);}public bool RemoveKey(TKey key){if(keyToVal.Remove(key,out var val)){return valToKey.Remove(val);}return false;}public bool RemoveKey(TKey key,out TVal val){if(keyToVal.Remove(key,out val)){return valToKey.Remove(val);}return false;}public bool RemoveValue(TVal value){if(valToKey.Remove(value,out var key)){return keyToVal.Remove(key);}return false;}public bool RemoveValue(TVal value,out TKey key){if(valToKey.Remove(value,out key)){return keyToVal.Remove(key);}return false;}public bool TryGetKey(TVal value,out TKey key){return valToKey.TryGetValue(value,out key);}public bool TryGetValue(TKey key,out TVal value){return keyToVal.TryGetValue(key,out value);}public void Set(TKey key,TVal value){keyToVal[key]=value;valToKey[value]=key;}public void Clear(){keyToVal.Clear();valToKey.Clear();}public Dictionary<TKey,TVal>ToKeyValueDictionary(){return keyToVal.ToDictionary(x=>x.Key,x=>x.Value);}public Dictionary<TVal,TKey>ToValueKeyDictionary(){return valToKey.ToDictionary(x=>x.Key,x=>x.Value);}public IEnumerator<KeyValuePair<TKey,TVal>>GetEnumerator(){return keyToVal.GetEnumerator();}IEnumerator IEnumerable.GetEnumerator(){return GetEnumerator();}}}namespace PluginComponents.Loottable.Extensions.Reflection{using Oxide.Core;using System;using System.Linq;using System.Reflection;using System.Text.RegularExpressions;using PluginComponents.Loottable;using PluginComponents.Loottable.Extensions;public static class ReflectionExtensions{public static T GetField<T>(this object obj,string fieldName){var field=obj.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){return(T)field.GetValue(obj);}else{Interface.Oxide.LogError($"Failed to get value of field {obj.GetType().Name}.{fieldName}");return default;}}public static void SetField(this object obj,string fieldName,object value){var field=obj.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){field.SetValue(obj,value);}else{Interface.Oxide.LogError($"Failed to set value of field {obj.GetType().Name}.{fieldName}");}}public static void SetField<T>(this T obj,string fieldName,object value){var field=typeof(T).GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){field.SetValue(obj,value);}else{Interface.Oxide.LogError($"Failed to set value of field {obj.GetType().Name}.{fieldName}");}}public static void CallMethod(this object obj,string methodName,params object[]args){var method=obj.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(method!=null){method.Invoke(obj,args);}else{Interface.Oxide.LogError($"Failed to invoke method {obj.GetType().Name}.{methodName} with {args.Length} args");}}public static T CallMethod<T>(this object obj,string methodName,params object[]args){var method=obj.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(method!=null){return(T)method.Invoke(obj,args);}else{Interface.Oxide.LogError($"Failed to invoke method {obj.GetType().Name}.{methodName} with {args.Length} args");return default;}}public static MethodInfo FindLocalMethod(this Type type,string surroundingName,string localName,bool capturesVariables){var methods=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Public|(capturesVariables?BindingFlags.Instance:BindingFlags.Static));return methods.FirstOrDefault(m=>m.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>()!=null&&Regex.IsMatch(m.Name,$@"^<{surroundingName}>g__{localName}\|\d+(_\d+)?"));}}}namespace PluginComponents.Loottable.Extensions.String{using System.Text;using PluginComponents.Loottable;using PluginComponents.Loottable.Extensions;public static class StringEx{public static string WithSpaceBeforeUppercase(this string str){var sb=new StringBuilder();var wasLower=false;foreach(var c in str){if(char.IsUpper(c)&&wasLower){sb.Append(' ');}sb.Append(c);wasLower=char.IsLower(c);}return sb.ToString();}public static string UpperFirst(this string str){if(!System.String.IsNullOrEmpty(str)){var chars=str.ToCharArray();chars[0]=char.ToUpperInvariant(chars[0]);return new string(chars);}return str;}}}namespace PluginComponents.Loottable.Extensions.Lang{using PluginComponents.Loottable.Core;using System.Collections.Generic;using PluginComponents.Loottable;using PluginComponents.Loottable.Extensions;public static class LangEx{public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player)=>GetMessage(lang,key,player.userID.Get());public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId){return lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());}public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args)=>GetMessage(lang,key,player.userID,args);public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId,params object[]args){var msg=lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());return string.Format(msg,args);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players){foreach(var player in players){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,params object[]args)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList,args);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players,params object[]args){foreach(var player in players){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}}public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,BasePlayer player)=>GetLanguage(lang,player.userID);public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,ulong userId){return lang.GetLanguage(userId.ToString());}}}namespace PluginComponents.Loottable.Extensions.BaseNetworkable{using PluginComponents.Loottable;using PluginComponents.Loottable.Extensions;public static class BaseNetworkableEx{public static bool IsNullOrDestroyed(this global::BaseNetworkable baseNetworkable){return!baseNetworkable||baseNetworkable.IsDestroyed;}}}namespace PluginComponents.Loottable.Extensions.Command{using Oxide.Core.Libraries.Covalence;using System;using PluginComponents.Loottable;using PluginComponents.Loottable.Extensions;public static class CommandExtensions{public static string GetString(this string[]args,int index,string def=""){if(args.Length>index){return args[index];}else{return def;}}public static ulong GetUlong(this string[]args,int index,ulong def=0){if(UInt64.TryParse(args.GetString(index),out var value)){return value;}return def;}public static int GetInt(this string[]args,int index,int def=0){if(Int32.TryParse(args.GetString(index),out var value)){return value;}return def;}public static BasePlayer ToBasePlayer(this IPlayer iPlayer){return iPlayer.Object as BasePlayer;}public static bool TryGetBasePlayer(this IPlayer iplayer,out BasePlayer player){player=iplayer.IsServer?null:iplayer.Object as BasePlayer;return player!=null;}}}namespace PluginComponents.Loottable.Tools.Entity{using Facepunch;using JetBrains.Annotations;using PluginComponents.Loottable.Extensions.BaseNetworkable;using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;using PluginComponents.Loottable;using PluginComponents.Loottable.Tools;public static class EntityTools{public static TCustom CreateCustomEntity<TEnt,TCustom>(string prefab,Vector3 position=default,Quaternion rotation=default)where TEnt:BaseEntity where TCustom:TEnt{var entity=CreateEntity<TEnt>(prefab,position,rotation,false,false);var customEntity=entity.gameObject.AddComponent<TCustom>();var fields=typeof(TEnt).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);foreach(var field in fields){field.SetValue(customEntity,field.GetValue(entity));}UnityEngine.Object.DestroyImmediate(entity,true);customEntity.gameObject.AwakeFromInstantiate();return customEntity;}public static T CreateEntity<T>(string prefab,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,default,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Vector3 rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.Euler(rotation),save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,rotation,save,true);private static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save,bool active)where T:BaseEntity{var ent=GameManager.server.CreateEntity(prefab,position,rotation,active);if(ent is not T entity){UnityEngine.Object.Destroy(ent);throw new InvalidCastException($"Failed to create entity of type '{typeof(T).Name}' from '{prefab}'");}ent.enableSaving=save;return entity;}public static void KillSafe<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{var list=Pool.Get<List<T>>();list.AddRange(entities);Kill(list);Pool.FreeUnmanaged(ref list);}public static void Kill<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{foreach(var entity in entities){Kill(entity);}}public static void Kill([CanBeNull]BaseEntity entity){if(entity is not null&&!entity.IsDestroyed){entity.Kill();}}}}