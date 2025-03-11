using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Carbon.Plugins;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Rust.Modular;
using UnityEngine;
using Random = UnityEngine.Random;

//FastAirDrop
//AutoLootFromBarrels
//InstantCraft
//1-st Level Blueprint Unlock
//BetterVendingMachine
//HackableCrateTimer
//SlowerMetabolism
//Queue
//NPCLootMultiplier
//RecyclerSpeed
//Decay scale

namespace Oxide.Plugins;

[Info("QoLPlugins", "ahigao", "1.0.0")]
internal class QoLPlugins : CarbonPlugin
{
    #region Static

    private string SkipQueuePermission = "qolplugins.skipqueue";
    private string AutoLootFromBarrelPermission = "qolplugins.autobarrel";
    private string NoFallDamagePermission = "qolplugins.nofalldamage";
    private string ProdPermission = "qolplugins.prod";
    private List<ItemDefinition> FirstLevelBlueprints = new List<ItemDefinition>();
    private string LandedEffect = "assets/bundled/prefabs/fx/survey_explosion.prefab";

    private List<string> EngineParts = new List<string>
    {
        "carburetor3",
        "crankshaft3",
        "piston3",
        "sparkplug3",
        "valve3",
        "diving.tank",
        "nightvisiongoggles"
    };
    private List<string> Barrels = new List<string>
    {
        "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
        "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",
        "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
        "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
        "assets/bundled/prefabs/radtown/oil_barrel.prefab",
        "assets/content/props/roadsigns/roadsign1.prefab",
        "assets/content/props/roadsigns/roadsign2.prefab",
        "assets/content/props/roadsigns/roadsign3.prefab",
        "assets/content/props/roadsigns/roadsign4.prefab",
        "assets/content/props/roadsigns/roadsign5.prefab",
        "assets/content/props/roadsigns/roadsign6.prefab",
        "assets/content/props/roadsigns/roadsign7.prefab",
        "assets/content/props/roadsigns/roadsign8.prefab",
        "assets/content/props/roadsigns/roadsign9.prefab"
    };

    private List<string> Messages = new List<string>
    {
        "Устали искать взрывчатку? Умираете от урона от падения?\nВ нашем магазине <color=#face80>storeproxy.ru</color> можно приобрести привилегии, которые решат ваши проблемы!",
        "Надоело писать команды в чат? Используйте быстрое меню сервера, расположенное под вашими быстрыми слотами инвентаря.",
        "У нашего сервера есть свой Discord канал <color=#face80>discord.gg/2bMgyJGaHZ</color>, а также группа в ВК <color=#face80>vk.com/proxyrustx1000000</color>",
        "Не хватает патрон в магазине, что бы убить врага? Не проблема! В нашем магазине <color=#face80>storeproxy.ru</color> вы можете приобрести перк на бесконечные патроны!",
        "Вы можете заспавнить свой личный Миникоптер при помощи команды /mymini или нажав на иконку Миникоптера около вашего инвентаря!\n Не забывайте, что доступ к личному Миникоптеру есть только у тех игроков, которые связали свой Discord и Steam аккаунт или приобрели привилегию."
    };

    #region Classes

    private class Configuration
    {
        [JsonProperty(PropertyName = "Cargo Plane speed(Vannila = 50)")]
        public float CargoPlaneSpeed = 500;

        [JsonProperty(PropertyName = "AirDrop Drag")]
        public float AirDropDrag = 0.4f;

        [JsonProperty(PropertyName = "Supply Drop duration")]
        public int SupplyDuration = 25;

        [JsonProperty(PropertyName = "Starting fuel in vehicle")]
        public int StartingFuel = 10000;

        [JsonProperty(PropertyName = "HackableCrate second to hack")]
        public int SecondToHack = 180;

        [JsonProperty(PropertyName = "Increase oven speed")]
        public float OvenSpeed = 1000;

        [JsonProperty(PropertyName = "Reduce fuel consumption")]
        public float FuelConsumptionReduce = 2;
    }

    private class Data
    {
        public List<ulong> SpawnedVehiclesID = new List<ulong>();
        public Dictionary<ulong, string> PlayersNames = new Dictionary<ulong, string>();
    }

    #endregion

    #endregion

    #region OxideHooks

    #region BaseHooks

    private void Init()
    {
        LoadData();
        LoadBlueprintList();
        RegisterPermissions();
        RemoveWorkbenchRequirement();

        ConVar.Stability.stabilityqueue = 19;
        ConVar.Stability.strikes = 20;
        ConVar.Decay.scale = 50;
        ConVar.Server.max_sleeping_bags = 25;

        foreach (var check in BaseNetworkable.serverEntities.OfType<Minicopter>())
            OnEntitySpawned(check);
        
        foreach (var check in BasePlayer.activePlayerList)
            OnPlayerConnected(check);

        timer.Every(1200, () =>
        {
            var message = $"<color=#a8b2e1>[ProxyRust]</color>\n<color=#cdc2b2>{Messages.GetRandom()}</color>";
            foreach (var check in BasePlayer.activePlayerList)
                Player.Message(check, message, 76561198297741077);
        });

        timer.Every(60, () =>
        {
            Server.Command("env.time 12");
        });
    }

    private void OnServerInitialized()
    {
        foreach (var check in UnityEngine.Object.FindObjectsOfType<TriggerRadiation>())
            UnityEngine.Object.Destroy(check);
    }

    private void Unload()
    {
        SaveData();
    }

    private void OnPlayerConnected(BasePlayer player)
    {
        if (player == null)
            return;

        if (!_data.PlayersNames.TryAdd(player.userID, player.displayName))
            _data.PlayersNames[player.userID] = player.displayName;

        BlueprintUnlock(player);
        
        player.ClientRPCPlayer(null, player, "craftMode", 1);
    }
    
    

    #endregion

    #region NPCLootMultiplier

    private void OnCorpsePopulate(HumanNPC humanNpc, NPCPlayerCorpse npcPlayerCorpse)
    {
        NextTick(() =>
        {
			if (humanNpc == null || npcPlayerCorpse == null || npcPlayerCorpse.containers == null)
				return;
		
            foreach (var check in npcPlayerCorpse.containers)
            {
				if (check == null)
					continue;
				 
                foreach (var item in check.itemList)
                {
					if (item == null)
						continue;
					
                    item.amount *= 10000;
                    if (item.amount > item.info.stackable)
                        item.amount = item.info.stackable;
                }
            }
        });
    }

    #endregion

    #region QueueSkip

    private object CanBypassQueue(Connection connection)
    {
        if (connection == null)
            return null;
        
        if (connection.authLevel >= 1 || permission.UserHasPermission(connection.userid.ToString(), SkipQueuePermission))
            return true;

        return null;
    }

    #endregion

    #region Metabolism

    private object OnRunPlayerMetabolism(PlayerMetabolism player, BaseCombatEntity ownerEntity, float delta)
    {
        if (player == null || ownerEntity == null)
            return null;

        RunCustomMetabolism(player, ownerEntity, delta);
        return false;
    }

    #endregion

    #region FastAirDrop

    private void OnEntitySpawned(CargoPlane plane)
    {
        if (plane == null)
            return;

        plane.secondsToTake = Vector3.Distance(plane.startPos, plane.endPos) / _config.CargoPlaneSpeed;
        plane.secondsToTake *= UnityEngine.Random.Range(0.95f, 1.05f);
    }

    private void OnParachuteRemove(SupplyDrop supplyDrop)
    {
        if (supplyDrop == null)
            return;

        foreach (var check in BaseNetworkable.GetConnectionsWithin(supplyDrop.transform.position, 128))
            EffectNetwork.Send(new Effect(LandedEffect, supplyDrop, 0, new Vector3(), new Vector3()), check);
    }

    private void OnEntitySpawned(SupplyDrop supplyDrop)
    {
        if (supplyDrop == null)
            return;

        var rigidBody = supplyDrop.GetComponent<Rigidbody>();
        rigidBody.drag = _config.AirDropDrag;
        rigidBody.angularDrag = _config.AirDropDrag;
    }

    private void OnSupplySignalExplode(SupplySignal supplySignal)
    {
        if (supplySignal == null)
            return;

        var entity = GameManager.server.CreateEntity(supplySignal.EntityToCreate.resourcePath);
        entity.SendMessage("InitDropPosition", supplySignal.transform.position, SendMessageOptions.DontRequireReceiver);
        entity.Spawn();
        supplySignal.Invoke(supplySignal.FinishUp, _config.SupplyDuration);
        supplySignal.SetFlag(BaseEntity.Flags.On, true);
        supplySignal.SendNetworkUpdateImmediate();
    }

    #endregion

    #region AutoLootFromBarrels

    private void OnEntityDeath(LootContainer container, HitInfo info)
    {
        if (container == null || info == null || !Barrels.Contains(container.PrefabName))
            return;

        var player = info.InitiatorPlayer;
        if (player == null || !permission.UserHasPermission(player.UserIDString, AutoLootFromBarrelPermission) || Vector3.Distance(player.transform.position, container.transform.position) > 25)
            return;

        for (var i = container.inventory.itemList.Count - 1; i != -1; i--)
        {
            var check = container.inventory.itemList[i];
            var amount = check.amount;
            if (!check.MoveToContainer(player.inventory.containerMain) && !check.MoveToContainer(player.inventory.containerBelt))
            {
                check.DropAndTossUpwards(container.transform.position);
                continue;
            }

            player.Command("note.inv", check.info.itemid, amount, string.Empty, (int)BaseEntity.GiveItemReason.Generic);
        }
    }

    #endregion

    #region InstantCraft

    private object OnItemCraft(ItemCraftTask task, BasePlayer owner)
    {
        if (task == null || owner == null || task.cancelled)
            return null;

        var stacks = GetStacks(task.blueprint.targetItem, task.amount * task.blueprint.amountToCreate);
        var slots = FreeSlots(owner);
        if (!HasPlace(slots, stacks))
        {
            CancelTask(task, owner, "Slots", stacks.Count, slots);
            return false;
        }

        if (!GiveItem(task, owner, stacks))
        {
            return null;
        }

        return true;
    }

    #endregion

    #region BetterVending

    private void CanPurchaseItem(BasePlayer buyer, Item soldItem, Action<BasePlayer, Item> onItemPurchased, NPCVendingMachine vm)
    {
        if (vm == null || soldItem == null)
            return;

        var item = ItemManager.Create(soldItem.info, soldItem.amount, soldItem.skin);
        if (soldItem.blueprintTarget != 0)
            item.blueprintTarget = soldItem.blueprintTarget;

        if (soldItem.instanceData != null)
            item.instanceData.dataInt = soldItem.instanceData.dataInt;

        NextTick(() =>
        {
            if (item == null)
                return;

            if (vm == null) 
            {
                item.Remove();
                return;
            }

            vm.transactionActive = true;
            if (!item.MoveToContainer(vm.inventory))
                item.Remove();

            vm.transactionActive = false;
            vm.FullUpdate();
        });
    }
    
    private object OnBuyVendingItem(VendingMachine machine, BasePlayer player, int sellOrderID, int amount)
    {
        if (machine == null || player == null || (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())) 
            return null;

        machine.ClientRPC(null, "CLIENT_StartVendingSounds", sellOrderID);
        machine.DoTransaction(player, sellOrderID, amount);
        return false;
    }

    #endregion

    #region BetterVehicle

    private void OnEntitySpawned(BaseVehicle baseVehicle)
    {
        NextTick(() =>
        {
            var fuelSystem = baseVehicle?.GetFuelSystem();
            if (fuelSystem == null || baseVehicle.net == null || _data?.SpawnedVehiclesID == null || _data.SpawnedVehiclesID.Contains(baseVehicle.net.ID.Value))
                return;

            fuelSystem.AddStartingFuel(_config.StartingFuel);
            _data.SpawnedVehiclesID.Add(baseVehicle.net.ID.Value);

            if (baseVehicle is not ModularCar modularCar)
                return;
            
            foreach (var check in modularCar.AttachedModuleEntities)
            {
                if (check is not VehicleModuleEngine vehicleModuleEngine)
                    continue;
                
                FillCarEngine(vehicleModuleEngine.GetContainer() as EngineStorage);
            }
        });
    }

    private void OnEntityDeath(BaseVehicle baseVehicle)
    {
        if (baseVehicle == null || !_data.SpawnedVehiclesID.Contains(baseVehicle.net.ID.Value))
            return;

        _data.SpawnedVehiclesID.Remove(baseVehicle.net.ID.Value);
    }
    
    private object OnLoseCondition(Item item, ref float amount)
    {
        if (item == null)
            return null;

        item.condition = item.info.condition.max;
        return false;
    }
    
    #endregion

    #region HackableCrate

    private void OnCrateHack(HackableLockedCrate hackableLockedCrate)
    {
        if (hackableLockedCrate == null)
            return;

        hackableLockedCrate.hackSeconds = 900 - _config.SecondToHack;
    }

    #endregion

    #region Oven

    private object OnOvenStart(BaseOven baseOven)
    {
        if (baseOven == null)
            return null;
        
        if (baseOven.FindBurnable() == null && !baseOven.CanRunWithNoFuel)
            return false;

        baseOven.inventory.temperature = baseOven.cookingTemperature;
        baseOven.UpdateAttachmentTemperature();
        baseOven.InvokeRepeating(baseOven.Cook, 0.5f / _config.OvenSpeed, 0.5f / _config.OvenSpeed);
        baseOven.SetFlag(BaseEntity.Flags.On, true);
        Interface.CallHook("OnOvenStarted", this);
        return false;
    }

    private object OnOvenCook(BaseOven oven, Item item)
    {
        if (oven == null || item == null)
            return null;

        foreach (var item2 in oven.inventory.itemList)
        {
            if (item2.position < oven._inputSlotIndex || item2.position >= oven._inputSlotIndex + oven.inputSlots || item2.HasFlag(Item.Flag.Cooking))
                continue;

            item2.SetFlag(Item.Flag.Cooking, true);
            item2.MarkDirty();
        }

        oven.IncreaseCookTime(0.5f * oven.GetSmeltingSpeed());
        var slot = oven.GetSlot(BaseEntity.Slot.FireMod);

        if (slot)
            slot.SendMessage("Cook", 0.5f, SendMessageOptions.DontRequireReceiver);

        var component = item.info.GetComponent<ItemModBurnable>();
        item.fuel -= 0.5f * (oven.cookingTemperature / 200f) / _config.FuelConsumptionReduce;
        
        if (!item.HasFlag(Item.Flag.OnFire))
        {
            item.SetFlag(Item.Flag.OnFire, true);
            item.MarkDirty();
        }

        if (item.fuel <= 0f)
            oven.ConsumeFuel(item, component);

        oven.OnCooked();
        Interface.CallHook("OnOvenCooked", oven, item, slot);

        return false;
    }

    #endregion

    #region RecyclerSpeed

    private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
    {
        if (recycler == null || recycler.IsOn()) 
            return;

        timer.In(0.1f, () =>
        {
            recycler.CancelInvoke(nameof(recycler.RecycleThink));
            recycler.InvokeRepeating(recycler.RecycleThink, 0.1f, 0.1f);
        });
    }

    #endregion

    #region AutoCodeLock

    private void OnEntityBuilt(HeldEntity plan, GameObject go)
    {
        if (plan == null || go == null)
            return;
        
        var player = plan.GetOwnerPlayer();
        if (player == null)
            return;
        
        var entity = go.ToBaseEntity() as DecayEntity;
        var container = entity as StorageContainer;
        if (entity == null || entity.IsLocked() || container != null && container.inventorySlots < 12 ||
            !container && !(entity is AnimatedBuildingBlock))
            return;

        var code = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
        if (code == null)
            return;
        
        code.gameObject.Identity();
        code.OwnerID = player.userID;
        code.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
        code.Spawn();
        code.code = Random.Range(1000, 9999).ToString();
        code.hasCode = true;
        entity.SetSlot(BaseEntity.Slot.Lock, code);
        Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab",
            code.transform.position);
        code.whitelistPlayers.Add(player.userID);
        code.SetFlag(BaseEntity.Flags.Locked, true);
    }

    #endregion

    #region StartMetabolism

    private void OnPlayerRespawned(BasePlayer player)
    {
        if (player == null || !player.userID.IsSteamId())
            return;

        player.Heal(100);
        player.metabolism.hydration.SetValue(500);
        player.metabolism.calories.SetValue(500);
    }
    
    private void OnPlayerSpawn(BasePlayer player)
    {
        if (player == null || !player.userID.IsSteamId())
            return;

        player.Heal(100);
        player.metabolism.hydration.SetValue(500);
        player.metabolism.calories.SetValue(500);
    }

    #endregion

    #region NoFallDamage

    private void OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
    {
        if (player == null || hitInfo == null || !player.userID.IsSteamId())
            return;

        var damageType = hitInfo.damageTypes.GetMajorityDamageType();
        if (damageType != Rust.DamageType.Fall || !permission.UserHasPermission(player.UserIDString, NoFallDamagePermission))
            return;
        
        hitInfo.damageTypes.ScaleAll(0);
    }

    #endregion

    #region Minicopter

    private object OnEngineStart(PlayerHelicopter playerHelicopter, BasePlayer player)
    {
        if (player == null || playerHelicopter == null)
            return null;
        
        playerHelicopter.SetFlag(playerHelicopter.engineController.engineStartingFlag, true, false, true);
        playerHelicopter.SetFlag(global::BaseEntity.Flags.On, false, false, true);
        playerHelicopter.SetFlag(global::BaseEntity.Flags.On, true, false, true);
        playerHelicopter.SetFlag(playerHelicopter.engineController.engineStartingFlag, false, false, true);
        return false;
    }

    private void OnEntitySpawned(Minicopter minicopter)
    {
        if (minicopter == null)
            return;
        
        minicopter.liftFraction = 1f;
        minicopter.torqueScale.x = 800;
        minicopter.torqueScale.y = 800;
        minicopter.torqueScale.z = 400;
    }

    #endregion

    #region Corpse

    private void OnEntitySpawned(PlayerCorpse playerCorpse)
    {
        NextTick(() =>
        {
            if (playerCorpse == null)
                return;
            
            playerCorpse.Kill();
        });
    }

    #endregion

    #endregion

    #region Commands

    [ChatCommand("prod")]
    private void ChatCommandprod(BasePlayer player, string command, string[] args)
    {
        if (!permission.UserHasPermission(player.UserIDString, ProdPermission))
        {
            Player.Message(player, "<color=#cdc2b2>У вас нет прав на использования этой команды</color>", 76561198297741077);
            return;
        }

        if (!Physics.Raycast(player.eyes.HeadRay(), out var hit, 15f))
            return;
        
        if (hit.collider == null || hit.GetEntity() is not DecayEntity decayEntity)
        {
            Player.Message(player, "<color=#cdc2b2>Объект не найден</color>", 76561198297741077);
            return;
        }

        if (decayEntity.OwnerID == 0 || !_data.PlayersNames.TryGetValue(decayEntity.OwnerID, out var name))
        {
            Player.Message(player, "<color=#cdc2b2>Владелец объекта не является игроком</color>", 76561198297741077);
            return;
        }
        
        Player.Message(player, $"<color=#cdc2b2><color=#face80>Имя Владельца:</color> {name}\n<color=#face80>SteamID Владельца:</color> {decayEntity.OwnerID}</color>", 76561198297741077);
    }

    #endregion

    #region Functions

    #region BlueprintWorckbench

    private void RemoveWorkbenchRequirement()
    {
        foreach (var check in ItemManager.bpList)
            check.workbenchLevelRequired = 0;
    }

    #endregion
    
    #region CustomMetabolism

    private void RunCustomMetabolism(PlayerMetabolism metabolism, BaseCombatEntity ownerEntity, float delta)
    {
        var currentTemperature = metabolism.owner.currentTemperature;
        var fTarget = metabolism.owner.currentComfort;
		metabolism.UpdateWorkbenchFlags();
		metabolism.owner.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, metabolism.owner.InSafeZone());
		metabolism.owner.SetPlayerFlag(BasePlayer.PlayerFlags.NoRespawnZone, metabolism.owner.InNoRespawnZone());
		metabolism.owner.SetPlayerFlag(BasePlayer.PlayerFlags.ModifyClan, metabolism.owner.CanModifyClan());
        metabolism.temperature.SetValue(10);
        metabolism.wetness.SetValue(0);
        
		metabolism.comfort.MoveTowards(fTarget, delta / 5f);
        var num5 = 0.6f + 0.4f * metabolism.comfort.value;
		if (metabolism.calories.value > 100f && metabolism.owner.healthFraction < num5 && metabolism.radiation_poison.Fraction() < 0.25f && metabolism.owner.SecondsSinceAttacked > 10f && !metabolism.SignificantBleeding() && metabolism.temperature.value >= 10f && metabolism.hydration.value > 40f)
		{
            var num6 = Mathf.InverseLerp(metabolism.calories.min, metabolism.calories.max, metabolism.calories.value);
            var num7 = 5f;
            var num8 = num7 * metabolism.owner.MaxHealth() * 0.8f / 600f;
			num8 += num8 * num6 * 0.5f;
            var num9 = num8 / num7;
			num9 += num9 * metabolism.comfort.value * 6f;
			ownerEntity.Heal(num9 * delta);
			metabolism.calories.Subtract((num8 * delta) / 2f);
			metabolism.hydration.Subtract((num8 * delta * 0.2f) / 2f);
		}
        var num10 = metabolism.owner.estimatedSpeed2D / metabolism.owner.GetMaxSpeed() * 0.75f;
        var fTarget2 = Mathf.Clamp(0.05f + num10, 0f, 1f);
		metabolism.heartrate.MoveTowards(fTarget2, delta * 0.1f);
		if (!metabolism.owner.IsGod())
		{
            var num11 = metabolism.heartrate.Fraction() * 0.375f;
			metabolism.calories.MoveTowards(0f, (delta * num11) / 2f);
            var num12 = 0.008333334f;
			num12 += Mathf.InverseLerp(40f, 60f, metabolism.temperature.value) * 0.083333336f;
			num12 += metabolism.heartrate.value * 0.06666667f;
			metabolism.hydration.MoveTowards(0f, (delta * num12) / 2f);
		}
        var b = metabolism.hydration.Fraction() <= 0f || metabolism.radiation_poison.value >= 100f;
		metabolism.owner.SetPlayerFlag(global::BasePlayer.PlayerFlags.NoSprint, b);
		if (metabolism.temperature.value > 40f)
		{
			metabolism.hydration.Add(Mathf.InverseLerp(40f, 200f, metabolism.temperature.value) * delta * -1f);
		}
		if (metabolism.temperature.value < 10f)
		{
            var num13 = Mathf.InverseLerp(20f, -100f, metabolism.temperature.value);
			metabolism.heartrate.MoveTowards(Mathf.Lerp(0.2f, 1f, num13), delta * 2f * num13);
		}
        var num14 = metabolism.owner.AirFactor();
        var num15 = num14 > metabolism.oxygen.value ? 1f : 0.1f;
		metabolism.oxygen.MoveTowards(num14, delta * num15);
        var f = 0f;
        var f2 = 0f;
		if (metabolism.owner.IsOutside(metabolism.owner.eyes.position))
		{
			f = Climate.GetRain(metabolism.owner.eyes.position) * ConVar.Weather.wetness_rain;
			f2 = Climate.GetSnow(metabolism.owner.eyes.position) * ConVar.Weather.wetness_snow;
		}
        var flag = metabolism.owner.baseProtection.amounts[4] > 0f;
        var num16 = metabolism.owner.currentEnvironmentalWetness;
		num16 = Mathf.Clamp(num16, 0f, 0.8f);
        var num17 = metabolism.owner.WaterFactor();
		if (!flag && num17 > 0f)
		{
			metabolism.wetness.value = Mathf.Max(metabolism.wetness.value, Mathf.Clamp(num17, metabolism.wetness.min, metabolism.wetness.max));
		}
        var num18 = Mathx.Max(metabolism.wetness.value, f, f2, num16);
		num18 = Mathf.Min(num18, flag ? 0f : num18);
		metabolism.wetness.MoveTowards(num18, delta * 0.05f);
		if (num17 < metabolism.wetness.value && num16 <= 0f)
		{
			metabolism.wetness.MoveTowards(0f, delta * 0.2f * Mathf.InverseLerp(0f, 100f, currentTemperature));
		}
		metabolism.poison.MoveTowards(0f, delta * 0.5555556f);
		if (metabolism.wetness.Fraction() > 0.4f && metabolism.owner.estimatedSpeed > 0.25f && metabolism.radiation_level.Fraction() == 0f)
		{
			metabolism.radiation_poison.Subtract(metabolism.radiation_poison.value * 0.2f * metabolism.wetness.Fraction() * delta * 0.2f);
		}
		if (ConVar.Server.radiation)
		{
			if (!metabolism.owner.IsGod())
			{
				metabolism.radiation_level.value = metabolism.owner.radiationLevel;
				if (metabolism.radiation_level.value > 0f)
				{
					metabolism.radiation_poison.Add(metabolism.radiation_level.value * delta);
				}
			}
			else if (metabolism.radiation_level.value > 0f)
			{
				metabolism.radiation_level.value = 0f;
				metabolism.radiation_poison.value = 0f;
			}
		}

        if (!(metabolism.pending_health.value > 0f))
            return;
        
        var num19 = Mathf.Min(1f * delta, metabolism.pending_health.value);
        ownerEntity.Heal(num19);
        if (ownerEntity.healthFraction == 1f)
        {
            metabolism.pending_health.value = 0f;
            return;
        }
        metabolism.pending_health.Subtract(num19);
    }

    #endregion

    #region BlueprintUnlock

    private void LoadBlueprintList()
    {
        foreach (var check in ItemManager.itemList)
        {
            var blueprint = check?.Blueprint;
            if (blueprint == null)
                continue;
            
            FirstLevelBlueprints.Add(check);
        }
    }
    
    private void BlueprintUnlock(BasePlayer player)
    {
        var bluePrint = player.blueprints;
        if (bluePrint == null)
            return;
        
        foreach (var check in FirstLevelBlueprints)
        {
            if (bluePrint.IsUnlocked(check))
                continue;
            
            bluePrint.Unlock(check);
        }
    }

    #endregion

    #region Other

    private void RegisterPermissions()
    {
        permission.RegisterPermission(SkipQueuePermission, this);
        permission.RegisterPermission(AutoLootFromBarrelPermission, this);
        permission.RegisterPermission(NoFallDamagePermission, this);
        permission.RegisterPermission(ProdPermission, this);
    }

    #endregion

    #region Craft
    
    public void CancelTask(ItemCraftTask task, BasePlayer owner, string reason, params object[] args)
    {
        task.cancelled = true;
        SendMessage(owner, "CM_INSTA_CRAFT_NO_SPACE", args);
        GiveRefund(task, owner);
    }

    public void GiveRefund(ItemCraftTask task, BasePlayer owner)
    {
        if (task.takenItems is not { Count: > 0 })
            return;
        
        foreach (var item in task.takenItems)
            owner.inventory.GiveItem(item);
    }

    public bool GiveItem(ItemCraftTask task, BasePlayer owner, List<int> stacks)
    {
        var i = 0;
        var skin = ItemDefinition.FindSkin(task.blueprint.targetItem.itemid, task.skinID);

        foreach (var stack in stacks)
        {
            if (!Give(task, owner, stack, skin) && i <= 0)
                return false;

            i++;
        }

        task.cancelled = true;
        return true;
    }

    public bool Give(ItemCraftTask task, BasePlayer owner, int amount, ulong skin)
    {
        var item = ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, amount, skin);

        if (item == null)
            return false;

        if (item.hasCondition && task.conditionScale != 1f)
        {
            item.maxCondition *= task.conditionScale;
            item.condition = item.maxCondition;
        }

        item.OnVirginSpawn();

        if (task.instanceData != null)
            item.instanceData = task.instanceData;

        if (owner.inventory.GiveItem(item))
        {
            owner.Command("note.inv", item.info.itemid, amount);
            return true;
        }

        var itemContainer = owner.inventory.crafting.containers.FirstOrDefault();
        owner.Command("note.inv", item.info.itemid, item.amount);
        owner.Command("note.inv", item.info.itemid, -item.amount);
        item.Drop(itemContainer.dropPosition, itemContainer.dropVelocity);

        return true;
    }

    public int FreeSlots(BasePlayer player)
    {
        var slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
        var taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
        return slots - taken;
    }

    public List<int> GetStacks(ItemDefinition item, int amount)
    {
        var list = new List<int>();
        var maxStack = item.stackable;

        if (maxStack == 0)
            maxStack = 1;

        while (amount > maxStack)
        {
            amount -= maxStack;
            list.Add(maxStack);
        }

        list.Add(amount);

        return list;
    }

    public bool HasPlace(int slots, List<int> stacks)
    {
        if (slots - stacks.Count < 0)
            return false;

        return slots > 0;
    }

    #endregion

    #region Vehicle

    private void FillCarEngine(EngineStorage engineStorage)
    {
        engineStorage.inventory.SetLocked(true);

        for (var slot = 0; slot < engineStorage.inventory.capacity; slot++)
            TryAddEngineItem(engineStorage, slot);
    }
    
    private static bool TryAddEngineItem(EngineStorage engineStorage, int slot)
    {
        ItemModEngineItem output;
        if (!engineStorage.allEngineItems.TryGetItem(3, engineStorage.slotTypes[slot], out output))
            return false;

        var component = output.GetComponent<ItemDefinition>();
        var item = ItemManager.Create(component);
        if (item == null)
            return false;

        if (item.MoveToContainer(engineStorage.inventory, slot, allowStack: false))
            return true;
        
        item.Remove();
        return false;
    }

    #endregion

    #endregion

    #region Language

    private void SendMessage(BasePlayer player, string msg, params object[] args) => Player.Message(player, GetMsg(player.UserIDString, msg, args), 76561198297741077);

    private string GetMsg(string player, string msg, params object[] args) => string.Format(lang.GetMessage(msg, this, player), args);

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            ["CM_INSTA_CRAFT_NO_SPACE"] = "<color=#cdc2b2>You don't have enough place to craft! Need <color=#face80>{0}</color>, have <color=#face80>{1}</color>!</color>"
        }, this);
        
        lang.RegisterMessages(new Dictionary<string, string>
        {
            ["CM_INSTA_CRAFT_NO_SPACE"] = "<color=#cdc2b2>У вас недостаточно места в инвентаре для крафта! Требуется <color=#face80>{0}</color>, имеется <color=#face80>{1}</color>!</color>"
        }, this, "ru");
    }

    #endregion

    #region Config

    private Configuration _config;

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

    #endregion

    #region Data

    private Data _data;

    private void LoadData() => _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data") ? Interface.Oxide.DataFileSystem.ReadObject<Data>($"{Name}/data") : new Data();
    private void OnServerSave() => SaveData();

    private void SaveData()
    {
        if (_data != null)
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
    }

    #endregion
}