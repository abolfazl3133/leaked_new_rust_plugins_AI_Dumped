// Requires: CustomItemDefinitions
// Reference: 0Harmony
#if CARBON
using HarmonyLib;
#else
using HarmonyLib;
#endif
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Reflection.Emit;
using static Oxide.Plugins.CustomItemDefinitions;


namespace Oxide.Plugins
{
    [Info("Customizable Weapons", "0xF [dsc.gg/0xf-plugins]", "1.5.21")]
    public class CustomizableWeapons : RustPlugin
    {
        #region Consts
        public const ItemContainer.Flag NoSortify = (ItemContainer.Flag)(8192) | (ItemContainer.Flag)(16384);
        private const ItemDefinition.Flag CUSTOMWEAPON_FLAG = (ItemDefinition.Flag)(32768);
        #endregion

        #region Variables
        private static CustomizableWeapons Plugin;
        #endregion

        #region PluginReference
        [PluginReference] private Plugin RustTranslationAPI;
        #endregion

        #region Permissions
        const string giveCommandPerm = "customizableweapons.give";
        const string iconShowPerm = "customizableweapons.icon.show";
        const string iconHidePerm = "customizableweapons.icon.hide";
        #endregion

        #region Hooks

        private void Init()
        {
            Plugin = this;
            permission.RegisterPermission(giveCommandPerm, this);
            permission.RegisterPermission(iconShowPerm, this);
            permission.RegisterPermission(iconHidePerm, this);

            LoadConfig();

            if (config.GlobalSettings.GiveOptions.Attachments == null)
                config.GlobalSettings.GiveOptions.Attachments = ItemManager.itemDictionaryByName.Keys.Where(shortname => shortname.StartsWith("weapon.mod")).ToDictionary(x => x, x => false);

            config.DataItems.Initialize(this);
            RegisterCustomItemDefinitions();


            SaveConfig();
            LoadLang();

            List<WeaponData> weaponDatas = new List<WeaponData>();
            weaponDatas.AddRange(config.DataItems.Default.Values);
            foreach (var list in config.DataItems.Permissions)
                weaponDatas.AddRange(list.Value.Values);
            weaponDatas.AddRange(config.DataItems.Custom.Values);
            foreach (var _wd in weaponDatas)
            {
                if (_wd.DamageTypes == null)
                    continue;
                if (_wd.DamageTypeList == null)
                    _wd.DamageTypeList = new Dictionary<string, Rust.DamageTypeList>();

                foreach (var ammo_pair in _wd.DamageTypes)
                {
                    if (!_wd.DamageTypeList.ContainsKey(ammo_pair.Key))
                        _wd.DamageTypeList[ammo_pair.Key] = new Rust.DamageTypeList();
                    for (int i = 0; i < DamageTypes.Count; i++)
                    {
                        Rust.DamageType type = DamageTypes[i];
                        string DamageTypeName = DamageTypeNames.Values.ElementAt(i);
                        if (_wd.DamageTypes[ammo_pair.Key].ContainsKey(DamageTypeName))
                            _wd.DamageTypeList[ammo_pair.Key].Add(type, _wd.DamageTypes[ammo_pair.Key][DamageTypeName]);
                    };
                }
            }


            foreach (var perm in config.DataItems.Permissions)
                permission.RegisterPermission($"customizableweapons.{perm.Key}", this);
        }

        void RegisterCustomItemDefinitions()
        {
            foreach ((string shortname, CustomItem item) in config.DataItems.Custom)
            {
                ItemDefinition parentDefinition = ItemManager.FindItemDefinition(item.Shortname);
                if (parentDefinition == null)
                    continue;

                ItemDefinition registeredDefinition = CustomItemDefinitions.RegisterPluginItemDefinition(new CustomItemDefinition
                {
                    shortname = shortname,
                    parentItemId = parentDefinition.itemid,
                    maxStackSize = 1,
                    flags = CUSTOMWEAPON_FLAG,
                    defaultName = item.Name,
                    defaultDescription = item.Description,
                    defaultSkinId = item.SkinId,
                    itemMods = new List<ItemMod>(parentDefinition.itemMods)
                    {
                         new ItemModEntitySkinner()
                         {
                             skinID = item.SkinId
                         }
                    }.ToArray()
                }, this);
            }
        }

        private class ItemModEntitySkinner : ItemMod
        {
            public override void OnItemCreated(Item item)
            {
                base.OnItemCreated(item);
                if (skinID > 0)
                {
                    BaseEntity heldEntity = item.GetHeldEntity();
                    if (heldEntity == null)
                        return;

                    heldEntity.skinID = skinID;
                    heldEntity.SendNetworkUpdate();
                }
                
            }

            public ulong skinID;
        }



        private void OnServerInitialized()
        {
            LoadIcon();
            BasePlayer.activePlayerList.ToList().ForEach(player => AddUiIcon(player));
        }


        private void OnUserPermissionGranted(string playerId, string perm)
        {
            if (perm != iconShowPerm && perm != iconHidePerm) return;

            BasePlayer player = BasePlayer.Find(playerId);
            if (player == null) return;

            OnPermissionsChanged(player);
        }
        private void OnUserPermissionRevoked(string playerId, string perm)
        {
            if (perm != iconShowPerm && perm != iconHidePerm) return;

            BasePlayer player = BasePlayer.Find(playerId);
            if (player == null) return;

            OnPermissionsChanged(player);
        }
        private void OnGroupPermissionGranted(string groupName, string perm)
        {
            if (perm != iconShowPerm && perm != iconHidePerm) return;

            BasePlayer.activePlayerList.ToList().ForEach(p => OnPermissionsChanged(p));
        }
        private void OnGroupPermissionRevoked(string groupName, string perm)
        {
            if (perm != iconShowPerm && perm != iconHidePerm) return;

            BasePlayer.activePlayerList.ToList().ForEach(p => OnPermissionsChanged(p));
        }


        private object OnWeaponReload(BaseProjectile weapon, BasePlayer player)
        {
            Item item = weapon.GetCachedItem();
            if (item == null) 
                return null;
            WeaponData weaponData;
            if (TryGetWeaponData(player, item, out weaponData))
            {
                if (weaponData.FastReload)
                {
                    if (weapon.TryReloadMagazine(player.inventory, -1))
                    {
                        weapon.SetHeld(false);
                        weapon.SendChildrenNetworkUpdateImmediate();
                        timer.Once(0.2f, () =>
                        {
                            if (player.GetActiveItem().uid == weapon.ownerItemUID)
                                weapon.SetHeld(true);
                        });
                        return false;
                    }
                }
            }
            return null;
        }

        void OnMagazineReload(BaseProjectile weapon, IAmmoContainer ammoSource, BasePlayer owner)
        {
            SetupMagizineCapacity(weapon);
        }


        private object OnWeaponModChange(BaseProjectile weapon, BasePlayer player)
        {
            if (weapon == null) return null;

            var currentCapacity = weapon.primaryMagazine.capacity;
            SetupMagizineCapacity(weapon);
            if (!config.GlobalSettings.DisableUnload)
            {
                if (currentCapacity == weapon.primaryMagazine.capacity) return false;
                if (weapon.primaryMagazine.contents > 0 && weapon.primaryMagazine.contents > weapon.primaryMagazine.capacity)
                {
                    Item item = weapon.GetItem();
                    if (item != null && player != null)
                        weapon.UnloadAmmo(item, player);
                }
            }
            return false;
        }

        void OnImpactEffectCreate(HitInfo info)
        {
            AttackEntity weapon = info.Weapon;
            if (weapon != null)
            {
                CustomItem customItem;
                Item item = weapon.GetCachedItem();
                if (item == null || item.info)
                    return;

                if (item.info.HasFlag(CUSTOMWEAPON_FLAG) && TryGetCustomItem(item.info, out customItem))
                {
                    BaseEntity hitEntity = info.HitEntity;
                    if (hitEntity.IsValid())
                    {
                        if (customItem.EffectsEntity.ElecticBalls > 0)
                            for (int i = 0; i < customItem.EffectsEntity.ElecticBalls; i++)
                                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", info.HitPositionWorld + UnityEngine.Random.insideUnitSphere * 1.5f);
                    }
                    else
                    {
                        if (customItem.EffectsGround.ElecticBall)
                            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", info.HitPositionWorld);
                    }

                }

            }

        }

        void UpdateItemName(Item item)
        {
            if (item == null)
                return;
            NextTick(() =>
            {
                ItemDefinition itemDef = item.info;
                if (itemDef == null)
                    return;

                CustomItem customItem;
                if (itemDef.HasFlag(CUSTOMWEAPON_FLAG) && TryGetCustomItem(itemDef, out customItem))
                {
                    var nameLangKey = $"{itemDef.shortname}:Name";
                    var itemName = GetMessage(nameLangKey, item.GetOwnerPlayer()?.UserIDString);
                    if (itemName == nameLangKey)
                        itemName = customItem.Name;


                    SetName(item, customItem, itemName);
                    if (customItem.SkinId != 0)
                        item.skin = customItem.SkinId;
                    item.MarkDirty();
                }
                else
                {
                    WeaponData weaponData;
                    if (TryGetWeaponData(item.GetOwnerPlayer(), item, out weaponData))
                    {
                        SetName(item, weaponData);
                        item.MarkDirty();
                    }
                }
            });
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!container.HasFlag(ItemContainer.Flag.IsPlayer)) return;
            if (container.playerOwner?.inventory?.loot?.IsLooting() != false) return;
            if (!IsWeapon(item)) return;
            UpdateItemName(item);
            NextTick(() =>
            {
                SetupMagizineCapacity(item.GetHeldEntity() as BaseProjectile);
            });

        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var explosion = entity?.GetComponent<TimedExplosive>();
            if (explosion == null) return;

            Item item = player.GetActiveItem();
            if (item == null) return;

            UpdateHitDamage(explosion.damageTypes, item, player);
        }
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            var explosion = entity?.GetComponent<TimedExplosive>();
            if (explosion == null) return;

            Item item = player.GetActiveItem();
            if (item == null) return;

            UpdateHitDamage(explosion.damageTypes, item, player);
        }
        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            var explosion = entity?.GetComponent<TimedExplosive>();
            if (explosion == null) return;

            Item item = player.GetActiveItem();
            if (item == null) return;

            UpdateHitDamage(explosion.damageTypes, item, player);
        }


        void OnEntityTakeDamage(BaseCombatEntity victim, HitInfo info)
        {

            BasePlayer initiator = info.Initiator as BasePlayer;
            if (initiator == null) return;

            if (info.Weapon == null)
            {
                Item activeItem = initiator.GetActiveItem();
                if (activeItem != null)
                {
                    AttackEntity attackHeldEntity = activeItem.GetHeldEntity() as AttackEntity;
                    if (info.WeaponPrefab != null && attackHeldEntity != null && attackHeldEntity.PrefabName != info.WeaponPrefab.PrefabName)
                        return;
                    info.Weapon = attackHeldEntity;
                }
            }

            if (info.Weapon == null)
                return;

            var item = info.Weapon.GetCachedItem();
            if (item == null) return;
            var ammoType = string.Empty;
            if (info.Weapon is BaseProjectile) ammoType = (info.Weapon as BaseProjectile).primaryMagazine.ammoType.shortname;
            else if (info.Weapon is BaseMelee) ammoType = "melee";
            else if(info.Weapon is ThrownWeapon) ammoType = "throw";
            else if(info.Weapon is FlameThrower) ammoType = "ammo.lowgrade";
            if (!string.IsNullOrEmpty(ammoType))
            {
                float coef = 1f;
                if (DefaultDamageWeapons.TryGetValue(item.info.shortname, out Dictionary<string, Rust.DamageTypeList> damageListByAmmo) && damageListByAmmo.TryGetValue(ammoType, out Rust.DamageTypeList damageList))
                    coef = info.damageTypes.Total() / damageList.Total();
                UpdateHitDamage(info, initiator, coef);
            }
            else
                UpdateHitDamage(info, initiator);

            if (info.HitEntity as DecayEntity)
            {
                if (info.damageTypes.Has(Rust.DamageType.Cold))
                    info.damageTypes.Scale(Rust.DamageType.Cold, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Poison))
                    info.damageTypes.Scale(Rust.DamageType.Poison, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Bleeding))
                    info.damageTypes.Scale(Rust.DamageType.Bleeding, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Hunger))
                    info.damageTypes.Scale(Rust.DamageType.Hunger, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Thirst))
                    info.damageTypes.Scale(Rust.DamageType.Thirst, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Suicide))
                    info.damageTypes.Scale(Rust.DamageType.Suicide, 0f);
                if (info.damageTypes.Has(Rust.DamageType.ElectricShock))
                    info.damageTypes.Scale(Rust.DamageType.ElectricShock, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Drowned))
                    info.damageTypes.Scale(Rust.DamageType.Drowned, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Fall))
                    info.damageTypes.Scale(Rust.DamageType.Fall, 0f);
                if (info.damageTypes.Has(Rust.DamageType.Heat))
                    info.damageTypes.Scale(Rust.DamageType.Heat, 0f);
            }

            //Effects
            BasePlayer victimPlayer = victim.ToPlayer();
            if (victimPlayer && TryGetWeaponData(initiator as BasePlayer, item, out WeaponData weaponData))
            {
                var effects = weaponData.EffectsEntity;
                if (!effects.RadPoison.Equals(0))
                {
                    float rad = effects.RadPoison * victimPlayer.RadiationExposureFraction();
                    if (rad > 0)
                        victimPlayer.metabolism.radiation_poison.value = Mathf.Clamp(victimPlayer.metabolism.radiation_poison.value + rad, victimPlayer.metabolism.radiation_poison.min, effects.ValuesSettings.MaxRadPoison);
                }
                if (!effects.Bleeding.Equals(0))
                    victimPlayer.metabolism.bleeding.value = Mathf.Clamp(victimPlayer.metabolism.bleeding.value + effects.Bleeding, victimPlayer.metabolism.bleeding.min, effects.ValuesSettings.MaxBleading);
                if (!effects.Temperature.Equals(0))
                {
                    float temperature = effects.Temperature;
                    if (temperature < 0)
                    {
                        var ColdExposureFraction = (1f - Mathf.Clamp(victimPlayer.baseProtection.amounts[(int)Rust.DamageType.ColdExposure], 0f, 1f));
                        temperature *= ColdExposureFraction;
                    }
                    if (temperature != 0)
                        victimPlayer.metabolism.temperature.value = Mathf.Clamp(victimPlayer.metabolism.temperature.value + temperature, effects.ValuesSettings.MinTemperature, effects.ValuesSettings.MaxTemperature);
                }
                if (!effects.Hunger.Equals(0))
                    victimPlayer.metabolism.calories.Subtract(effects.Hunger);
                if (!effects.Thirst.Equals(0))
                    victimPlayer.metabolism.hydration.Subtract(effects.Thirst);
                if (!effects.Wetness.Equals(0))
                    victimPlayer.metabolism.wetness.value = Mathf.Clamp(victimPlayer.metabolism.wetness.value + effects.Wetness, victimPlayer.metabolism.wetness.min, effects.ValuesSettings.MaxWetness);
            }
        }


        private void OnLoseCondition(Item item, ref float amount)
        {
            if (!IsWeapon(item)) return;

            BasePlayer owner = item.GetOwnerPlayer();
            if (owner == null) return;

            WeaponData weaponData;
            if (TryGetWeaponData(owner, item, out weaponData))
            {
                if (weaponData.Unbreakable)
                    amount = 0f;
                else
                    amount = (amount / (weaponData.Durability / item.info.condition.max)) * (item.maxCondition / item.info.condition.max);
            }
        }

        private void OnPlayerRespawned(BasePlayer player) => AddUiIcon(player);

        private void OnPlayerConnected(BasePlayer player) => AddUiIcon(player);

        private void OnPlayerLootEnd(PlayerLoot playerLoot)
        {
            var player = (BasePlayer)playerLoot.baseEntity;
            if (playerLoot.entitySource == RelationshipManager.ServerInstance)
            {
                DestroyUI(player);
                playerLoot.containers.Where(c => c.entityOwner == RelationshipManager.ServerInstance && c.capacity == 0).ToList().ForEach(c => c.Kill());
            }
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            NextTick(() =>
            {
                foreach (var item in player.inventory.AllItems())
                {
                    item.MarkDirty();
                }
            });
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            NextTick(() =>
            {
                foreach (var item in player.inventory.AllItems())
                {
                    item.MarkDirty();
                }
            });
        }


        void OnAmmoSwitch(BaseProjectile baseProjectile, BasePlayer owner)
        {
            UpdateItemName(baseProjectile.GetCachedItem());
        }

        void Loaded()
        {
            Harmony.Init(this.Name);
            Harmony.Patch();
        }
        void Unload()
        {

            Harmony.UnpatchAll();
            BasePlayer.allPlayerList.ToList().ForEach(player =>
            {
                DestroyUI(player);
                DestroyIcon(player);
            });
        }
        #endregion

        public static class Harmony
        {
            private static string Name;
#if CARBON
            private static Harmony _harmony;
#else
            //private static Harmony _harmony;
#endif
            public static void Init(string name)
            {
                Name = name;
#if CARBON
                Instance = new HarmonyLib.Harmony(name);
#else
                //Instance = HarmonyInstance.Create(name);
#endif
            }

            public static void Patch()
            {
                //Instance.Patch(AccessTools.Method(typeof(Item), "Save"), postfix: new HarmonyMethod(typeof(Patches), "Item_Save"));
            }
            public static void UnpatchAll()
            {
                //Instance.UnpatchAll(Name);
            }
        }
        static partial class Patches
        {
            internal static void Item_Save(Item __instance, ref ProtoBuf.Item __result)
            {
                if (!SaveRestore.IsSaving &&
                    Plugin.IsWeapon(__instance) &&
                    __result.streamerName != null &&
                    __result.streamerName.StartsWith("\n"))
                {
                    ItemContainer container = __instance.parent;
                    if (container != null)
                    {
                        BasePlayer player = container.playerOwner;
                        if (player != null && !player.IsNpc && !player.inventory.loot.IsLooting())
                            __result.name = __result.streamerName;
                    }
                }
            }
        }

        #region Commands
        [ConsoleCommand("cw.reload")]
        private void ConsoleCommandReload(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                if (!arg.Player().IsAdmin)
                {
                    SendReply(arg, "This command is only allowed to administrators");
                    return;
                }
            }
            Server.Command($"o.reload {Name}");
            if (arg.Player() != null)
            {
                SendReply(arg, "Items successfully reloaded!");
                return;
            }
            Puts("Items successfully reloaded");
        }
        [ConsoleCommand("cw.create")]
        private void ConsoleCommandCreate(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                if (!arg.Player().IsAdmin)
                {
                    SendReply(arg, "This command is only allowed to administrators");
                    return;
                }
            }
            if (arg.Args.IsNullOrEmpty() || arg.Args?.Count() != 2)
            {
                if (arg.Player() != null)
                {
                    SendReply(arg, "Usage: cw.create <new unique shortname> <shortname>");
                    return;
                }
                PrintWarning("Usage: cw.create <new unique shortname> <shortname>");
                return;
            }
            var name = arg.Args[0];
            if (name.Contains(" "))
            {
                if(arg.Player() != null)
                {
                    SendReply(arg, "No spaces are allowed in the unique shortname!");
                    return;
                }
                PrintError("No spaces are allowed in the unique shortname!");
                return;
            }
            var shortname = arg.Args[1];
            if (config.DataItems.CustomFs.ExistsDatafile(name))
            {
                if (arg.Player() != null)
                {
                    SendReply(arg, "A file with this name already exists, delete it or create it under another name.");
                    return;
                }
                PrintWarning("A file with this name already exists, delete it or create it under another name.");
                return;
            }
            if (!config.DataItems.Default.ContainsKey(shortname))
            {
                if (arg.Player() != null)
                {
                    SendReply(arg, $"The shortname '{shortname}' is not suitable, try another one, you can find available shortnames in the 'Default' folder");
                    return;
                }
                PrintWarning($"The shortname '{shortname}' is not suitable, try another one, you can find available shortnames in the 'Default' folder");
                return;
            }
            var defaultItem = config.DataItems.Default[shortname];
            CustomItem newItem = new CustomItem()
            {
                Shortname = shortname,
                SkinId = 0,
                Name = name,
                Description = "",
                Unbreakable = defaultItem.Unbreakable,
                Durability = defaultItem.Durability,
                MagazineCapacity = defaultItem.MagazineCapacity,
                DamageTypes = defaultItem.DamageTypes,
                DamageTypeList = defaultItem.DamageTypeList
            };
            config.DataItems.CustomFs.WriteObject<CustomItem>(name, newItem);
            config.DataItems.Custom[name] = newItem;
            if (arg.Player() != null)
            {
                SendReply(arg, $"Custom item with name '{name}' added!");
                return;
            }
            Puts($"Custom item with name '{name}' added!");
        }
        [ConsoleCommand("cw.give")]
        private void ConsoleCommandGive(ConsoleSystem.Arg arg)
        {
            string nickname = "SERVER";
            BasePlayer player = arg.Player();
            if (player != null)
            {
                if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, giveCommandPerm))
                {
                    arg.ReplyWith("You don't have permission to use this command.");
                    return;
                }

                nickname = player.displayName;
            }
            if (arg.Args.IsNullOrEmpty() || arg.Args?.Count() > 2 || arg.Args?.Count() < 1)
            {
                if (player != null)
                {
                    arg.ReplyWith("Usage: cw.give <steamid/nickname> <custom item name>");
                    return;
                }
                PrintWarning("Usage: cw.give <steamid/nickname> <custom item name>");
                return;
            }

            BasePlayer playerToGive = arg.Player();
            if (arg.Args?.Count() == 2)
            {
                playerToGive = BasePlayer.Find(arg.Args[0]);
                if (playerToGive == null)
                {
                    if (player != null)
                    {
                        arg.ReplyWith($"Player '{arg.Args[0]}' is offline or not exists.");
                        return;
                    }
                    PrintError($"Player '{arg.Args[0]}' is offline or not exists.");
                    return;
                }
            }
            string uniqueShortname = arg.Args?.Last();
            CustomItem customItem;
            if (TryGetCustomItemByKey(uniqueShortname, out customItem))
            {

                Item itemToGive = ItemManager.CreateByName(uniqueShortname, 1, customItem.SkinId);
                var nameLangKey = $"{uniqueShortname}:Name";
                var itemName = GetMessage(nameLangKey, playerToGive.UserIDString);
                if (itemName == nameLangKey)
                    itemName = customItem.Name;


                if (itemToGive.contents != null)
                {
                    foreach (var shortname in config.GlobalSettings.GiveOptions.Attachments.Where(pair => pair.Value).Select(x => x.Key))
                    {
                        Item item = ItemManager.CreateByName(shortname, 1, 0UL);
                        if (!item.MoveToContainer(itemToGive.contents, -1, true, false, null, true))
                            item.Remove(0f);
                    }
                }
                var heldEntity = itemToGive.GetHeldEntity();
                if (heldEntity is BaseProjectile)
                {
                    BaseProjectile weapon = (heldEntity as BaseProjectile);
                    weapon.primaryMagazine.capacity = customItem.MagazineCapacity;
                    weapon.primaryMagazine.definition.builtInSize = customItem.MagazineCapacity;
                    weapon.primaryMagazine.contents = customItem.MagazineCapacity;
                }
                if (heldEntity is ThrownWeapon && itemToGive.MaxStackable() > 1) itemToGive.amount = 100;
                if (!playerToGive.inventory.GiveItem(itemToGive))
                {
                    itemToGive.Remove(0f);
                    arg.ReplyWith("Couldn't give item (inventory full?)");
                    PrintWarning($"{nickname}[{(player == null ? "0" : player.UserIDString)}] failed to give a custom item [{uniqueShortname}] to player {playerToGive.displayName}[{(player == null ? "0" : playerToGive.UserIDString)}]!");
                    return;
                }
                SetName(itemToGive, customItem, itemName);
                itemToGive.skin = customItem.SkinId;
                itemToGive.MarkDirty();
                heldEntity.skinID = customItem.SkinId;
                heldEntity.SendNetworkUpdate();
                if (arg.Player() != null)
                    arg.ReplyWith($"Item [{uniqueShortname}] added to {playerToGive.displayName}'s inventory!");
                PrintWarning($"{nickname}[{(player == null ? "0" : player.UserIDString)}] gave {playerToGive.displayName}[{(playerToGive == null ? "0" : playerToGive.UserIDString)}] custom item [{uniqueShortname}]!");
            }
            else
            {
                if (arg.Player() != null)
                {
                    arg.ReplyWith($"Item with name \"{uniqueShortname}\" was not found!");
                    return;
                }
                PrintError($"Item with name \"{uniqueShortname}\" was not found!");
            }
        }
        #endregion

        #region Classes
        public class CustomItem : WeaponData
        {
            public class UISettings
            {
                [JsonProperty(PropertyName = "Name Color (or use <color></color> in name)")]
                public string NameColor { get; set; } = "1 1 1 1";
                [JsonProperty(PropertyName = "Frame Color")]
                public string FrameColor { get; set; } = "0.94 0.75 0.15 1";
            }

            [JsonProperty(PropertyName = "Name")]
            public string Name { get; set; } = "";
            [JsonProperty(PropertyName = "Description")]
            public string Description { get; set; } = "";
            [JsonProperty(PropertyName = "SkinId")]
            public ulong SkinId { get; set; } = 0;
            //[JsonProperty(PropertyName = "Permission for use")]
            //public string Permission { get; set; } = string.Empty;
            [JsonProperty(PropertyName = "UI Settings")]
            public UISettings UiSettings { get; set; } = new UISettings();

        }

        public class WeaponData
        {
            public class EffectsEntitySettings
            {
                public class __ValuesSettings
                {
                    [JsonProperty(PropertyName = "Max Radiation Poison")]
                    public float MaxRadPoison { get; set; } = 500;
                    [JsonProperty(PropertyName = "Max Bleading")]
                    public float MaxBleading { get; set; } = 100;
                    [JsonProperty(PropertyName = "Min Temperature")]
                    public float MinTemperature { get; set; } = -100;
                    [JsonProperty(PropertyName = "Max Temperature")]
                    public float MaxTemperature { get; set; } = 100;
                    [JsonProperty(PropertyName = "Max Wetness")]
                    public float MaxWetness { get; set; } = 100;
                }
                [JsonProperty(PropertyName = "Maximum and minimum values in the accumulation")]
                public __ValuesSettings ValuesSettings { get; set; } = new __ValuesSettings();
                [JsonProperty(PropertyName = "Radiation Poison")]
                public float RadPoison { get; set; }
                [JsonProperty(PropertyName = "Bleading")]
                public float Bleeding { get; set; }
                [JsonProperty(PropertyName = "Temperature")]
                public float Temperature { get; set; }
                [JsonProperty(PropertyName = "Hunger")]
                public float Hunger { get; set; }
                [JsonProperty(PropertyName = "Thirst")]
                public float Thirst { get; set; }
                [JsonProperty(PropertyName = "Wetness")]
                public float Wetness { get; set; }
                [JsonProperty(PropertyName = "Number of electric balls (recommended to 10)")]
                public int ElecticBalls { get; set; }
            }
            public class EffectsGroundSettings
            {
                [JsonProperty(PropertyName = "Electric Ball")]
                public bool ElecticBall { get; set; }
            }

            [JsonProperty(PropertyName = "Shortname")]
            public string Shortname { get; set; }
            [JsonProperty(PropertyName = "Unbreakable", NullValueHandling = NullValueHandling.Ignore)]
            public bool Unbreakable { get; set; }
            [JsonProperty(PropertyName = "Durability", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float Durability { get; set; }
            [JsonProperty(PropertyName = "Fast Reload")]
            public bool FastReload { get; set; } = false;
            [JsonProperty(PropertyName = "Magazine Capacity", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int MagazineCapacity { get; set; }
            [JsonProperty(PropertyName = "Effects when hit")]
            public EffectsEntitySettings EffectsEntity { get; set; } = new EffectsEntitySettings();

            [JsonProperty(PropertyName = "Effects when hit the ground")]
            public EffectsGroundSettings EffectsGround { get; set; } = new EffectsGroundSettings();

            [JsonProperty(PropertyName = "Base damage by type & ammo", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, Dictionary<string, float>> DamageTypes { get; set; }

            [JsonIgnore]
            public Dictionary<string, Rust.DamageTypeList> DamageTypeList { get; set; } = new Dictionary<string, Rust.DamageTypeList>();
        }
        #endregion

        #region Fields
        private static List<Rust.DamageType> DamageTypesExсeptions = new List<Rust.DamageType>()
        {
            Rust.DamageType.Generic,
            Rust.DamageType.LAST,
            Rust.DamageType.Decay,
            Rust.DamageType.RadiationExposure,
            Rust.DamageType.ColdExposure,
            Rust.DamageType.Fun_Water,
            Rust.DamageType.Suicide,
            Rust.DamageType.Fall,
        };
        private static List<Rust.DamageType> DamageTypes = Enum.GetValues(typeof(Rust.DamageType)).Cast<Rust.DamageType>().Where(type => !DamageTypesExсeptions.Contains(type)).ToList();
        private static Dictionary<Rust.DamageType, string> DamageTypeNames = DamageTypes.ToDictionary(type => type, type => Enum.GetName(typeof(Rust.DamageType), type));
        private static Dictionary<string, Dictionary<string, Rust.DamageTypeList>> DefaultDamageWeapons = new Dictionary<string, Dictionary<string, Rust.DamageTypeList>>();
        #endregion

        #region Methods
        private void SetName(Item item, WeaponData weaponData, string itemName = null)
        {
            if (item == null || weaponData == null) return;
            item.name = itemName;
            item.streamerName = null;
            if (!config.ItemSelectInfo)
                return;

            BasePlayer owner = item.GetOwnerPlayer();

            Dictionary<string, string> translation = null;
            if (RustTranslationAPI != null && (itemName == null || itemName == string.Empty))
                translation = RustTranslationAPI.Call<Dictionary<string, string>>("GetTranslations", lang.GetLanguage(owner?.UserIDString));
            if (itemName == null || itemName == string.Empty)
            {
                if (translation == null || !translation.TryGetValue($"{item.info.shortname}", out itemName))
                    itemName = item.info.displayName.translated;
            }

            Rust.DamageTypeList damageTypeListToRender = null;
            BaseEntity heldEntity = item.GetHeldEntity();
            if (heldEntity != null)
            {
                BaseProjectile baseProjectile = heldEntity as BaseProjectile;
                if (baseProjectile != null && baseProjectile.primaryMagazine != null)
                {
                    ItemDefinition ammoDef = baseProjectile.primaryMagazine.ammoType;
                    if (ammoDef != null)
                    {
                        weaponData.DamageTypeList.TryGetValue(ammoDef.shortname, out damageTypeListToRender);
                    }
                }
            }

            if (damageTypeListToRender == null && weaponData.DamageTypeList.Count > 0)
                damageTypeListToRender = weaponData.DamageTypeList.First().Value;
            if (damageTypeListToRender == null)
                return;

            Dictionary<string, float> FieldsForRender = DamageTypes.ToDictionary(x => GetMessage(DamageTypeNames[x], owner?.UserIDString), x => damageTypeListToRender.Get(x));
            FieldsForRender = FieldsForRender.Where(i => i.Value != 0f).ToDictionary(i => i.Key, i => i.Value);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(string.Concat(Enumerable.Repeat("\n", FieldsForRender.Count * 2 + 8)));
            stringBuilder.Append($"\n{itemName}");
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t" + GetMessage(LangKeys.ItemInfoPanelHeader, owner?.UserIDString));
            foreach (var pair in FieldsForRender)
            {
                string keyWithoutRichtext = Regex.Replace(pair.Key, "<.*?>", String.Empty);
                stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t   ");
                stringBuilder.Append(" " + keyWithoutRichtext);
                stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t\t   ");
                stringBuilder.Append(pair.Value);
            }
            stringBuilder.Append("\n\n\t\t\t\t\t\t\t\t\t\t\t\t" + GetMessage(LangKeys.CharacteristicsPanelHeader, owner?.UserIDString));
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t    " + GetMessage(LangKeys.MagazineCapacity, owner?.UserIDString));
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t\t    " + weaponData.MagazineCapacity);
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t    " + GetMessage(LangKeys.MaxDurability, owner?.UserIDString));
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t\t    " + weaponData.Durability);
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t    " + GetMessage(LangKeys.Unbreakable, owner?.UserIDString));
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t\t    " + (weaponData.Unbreakable ? GetMessage(LangKeys.Yes, owner?.UserIDString) : GetMessage(LangKeys.No, owner?.UserIDString)));
            item.streamerName = stringBuilder.ToString();

            item.MarkDirty();
        }




        private bool TryGetCustomItem(Item item, out CustomItem customItem)
            => TryGetCustomItem(item.info, out customItem);
        private bool TryGetCustomItem(ItemDefinition info, out CustomItem customItem)
        {
            if (!info.HasFlag(CUSTOMWEAPON_FLAG) || !TryGetCustomItemByKey(info.shortname, out customItem))
            {
                customItem = null;
                return false;
            }
            return true;
        }

        private bool HasCustomItemWithKey(string key)
        {
            return config.DataItems.Custom.ContainsKey(key);
        }
        private bool TryGetCustomItemByKey(string key, out CustomItem customItem)
        {
            if (key == null || config?.DataItems?.Custom == null)
            {
                customItem = null;
                return false;
            }
            return config.DataItems.Custom.TryGetValue(key, out customItem);
        }

        private bool TryGetWeaponData(BasePlayer player, Item item, out WeaponData weaponData)
                => TryGetWeaponData(player, item.info, out weaponData);
        private bool TryGetWeaponData(BasePlayer player, ItemDefinition info, out WeaponData weaponData)
        {
            CustomItem customItem;
            if (TryGetCustomItem(info, out customItem))
            {
                weaponData = customItem;
                return true;
            }
            else
                return TryGetDefaultWeaponData(player, info, out weaponData);
        }

        private bool TryGetDefaultWeaponData(BasePlayer player, Item item, out WeaponData weaponData)
               => TryGetDefaultWeaponData(player, item.info, out weaponData);

        private bool TryGetDefaultWeaponData(BasePlayer player, ItemDefinition info, out WeaponData weaponData)
        {
            try
            {
                if (!config.DataItems.Default.TryGetValue(info.shortname, out weaponData))
                    return false;
                if (player != null)
                {
                    foreach (var perm in config.DataItems.Permissions)
                    {
                        WeaponData tempWd = null;
                        if (perm.Value.TryGetValue(info.shortname, out tempWd) && permission.UserHasPermission(player.UserIDString, $"customizableweapons.{perm.Key}"))
                        {
                            weaponData = tempWd;
                            break;
                        }
                    }
                }
                return weaponData != null;
            }
            catch
            {
                weaponData = default(WeaponData);
                return false;
            }
        }

        private bool IsWeapon(Item item)
        {
            var heldEntity = item.GetHeldEntity();
            return heldEntity is BaseProjectile || heldEntity is BaseMelee || heldEntity is ThrownWeapon || heldEntity is FlameThrower;
        }

        private void UpdateHitDamage(HitInfo info, BaseEntity entity, float scale = 1f)
        {
			    // Yeni eklenen kontrol
    if (config.GlobalSettings.DisableWeaponDamage)
    {
        return; // Eğer yapılandırmada silah hasarı devre dışı bırakıldıysa, bu işlemi atla
    }

    if (info.Weapon is BaseProjectile)
    {
        var shootableWeapon = info.Weapon as BaseProjectile;
        var item = shootableWeapon.GetCachedItem();
        if (item == null) return;

        var ammotypeShortname = shootableWeapon.primaryMagazine.ammoType.shortname;
        if (ammotypeShortname == null) return;

        WeaponData weaponData = null;
        if (TryGetWeaponData(entity.ToPlayer(), item, out weaponData))
        {
            Rust.DamageTypeList damageTypeList = null;
            if (!weaponData.DamageTypeList.TryGetValue(ammotypeShortname, out damageTypeList))
            {
                PrintWarning($"There is no damage list in the item configuration (Shortname: {weaponData.Shortname})");
                return;
            }
            for (int i = 0; i < damageTypeList.types.Length; i++)
            {
                info.damageTypes.types[i] = damageTypeList.types[i] * scale;
            }
        }
    }
    // Melee ve diğer silah tipleri için devam eder...
            if (info.Weapon is BaseProjectile)
            {
                var shootableWeapon = info.Weapon as BaseProjectile;
                var item = shootableWeapon.GetCachedItem();
                if (item == null) return;

                var ammotypeShortname = shootableWeapon.primaryMagazine.ammoType.shortname;
                if (ammotypeShortname == null) return;

                WeaponData weaponData = null;
                if (TryGetWeaponData(entity.ToPlayer(), item, out weaponData))
                {
                    Rust.DamageTypeList damageTypeList = null;
                    if (!weaponData.DamageTypeList.TryGetValue(ammotypeShortname, out damageTypeList))
                    {
                        PrintWarning($"There is no damage list in the item configuration (Shortname: {weaponData.Shortname})");
                        return;
                    }
                    for (int i = 0; i < damageTypeList.types.Length; i++)
                    {
                        info.damageTypes.types[i] = damageTypeList.types[i] * scale;
                    }

                       
                }
            }
            else if (info.Weapon is BaseMelee)
            {
                var meleeWeapon = info.Weapon as BaseMelee;

                var item = meleeWeapon.GetCachedItem();
                if (item == null) return;

                string attackType = (info.IsProjectile() ? "throw" : "melee");

                WeaponData weaponData = null;
                if (TryGetWeaponData(entity.ToPlayer(), item, out weaponData))
                {
                    Rust.DamageTypeList damageTypeList = null;
                    if (!weaponData.DamageTypeList.TryGetValue(attackType, out damageTypeList))
                    {
                        PrintWarning($"There is no damage list in the item configuration (Shortname: {weaponData.Shortname})");
                        return;
                    }
                    for (int i = 0; i < damageTypeList.types.Length; i++)
                        info.damageTypes.types[i] = damageTypeList.types[i] * scale;
                }
            }
            else if (info.Weapon is FlameThrower)
            {
                var flameThrower = info.Weapon as FlameThrower;

                var item = flameThrower.GetCachedItem();
                if (item == null) return;

                WeaponData weaponData = null;
                if (TryGetWeaponData(entity.ToPlayer(), item, out weaponData))
                {
                    Rust.DamageTypeList damageTypeList = null;
                    if (!weaponData.DamageTypeList.TryGetValue("ammo.lowgrade", out damageTypeList))
                    {
                        PrintWarning($"There is no damage list in the item configuration (Shortname: {weaponData.Shortname})");
                        return;
                    }
                    for (int i = 0; i < damageTypeList.types.Length; i++)
                        info.damageTypes.types[i] = damageTypeList.types[i] * scale;
                }
            }

        }
        private void UpdateHitDamage(List<Rust.DamageTypeEntry> damageTypes, Item item, BaseEntity entity, float scale = 1f)
        {
            if (item == null || damageTypes == null) return;

            var type = string.Empty;

            var heldEntity = item.GetHeldEntity();
            if (heldEntity is BaseProjectile) type = (heldEntity as BaseProjectile).primaryMagazine.ammoType.shortname;
            if (heldEntity is ThrownWeapon) type = "throw";
            if (heldEntity is FlameThrower) type = "ammo.lowgrade";

            WeaponData weaponData;
            if (TryGetWeaponData(entity.ToPlayer(), item, out weaponData))
            {
                if (weaponData.DamageTypeList == null) return;
                Rust.DamageTypeList damageTypeList = null;
                if (!weaponData.DamageTypeList.TryGetValue(type, out damageTypeList))
                {
                    PrintWarning($"There is no damage list in the item configuration (Shortname: {weaponData.Shortname})");
                    return;
                }
                if (damageTypeList.types.Length > 0)
                {
                    damageTypes.Clear();
                    for (int i = 0; i < damageTypeList.types.Length; i++)
                    {
                        damageTypes.Add(new Rust.DamageTypeEntry()
                        {
                            type = (Rust.DamageType)i,
                            amount = damageTypeList.types[i] * scale
                        });
                    }
                }
            }
        }
void SetupMagizineCapacity(BaseProjectile weapon)
{
    if (weapon == null) return;

    // Yeni eklenen kontrol
    if (config.GlobalSettings.DisableMagazineCapacity)
    {
        return; // Eğer yapılandırmada mermi kapasitesi devre dışı bırakıldıysa, bu işlemi atla
    }

    Item item = weapon.GetItem();
    if (item == null) return;

    BasePlayer owner = weapon.GetOwnerPlayer();
    if (owner == null) return;

    WeaponData weaponData;
    if (TryGetWeaponData(owner, item, out weaponData))
    {
        int capacity = config.GlobalSettings.DisableMagazineBonus ? weaponData.MagazineCapacity : Mathf.CeilToInt(ProjectileWeaponMod.Mult(weapon, (ProjectileWeaponMod x) => x.magazineCapacity, (ProjectileWeaponMod.Modifier y) => y.scalar, 1f) * weaponData.MagazineCapacity);

        weapon.primaryMagazine.capacity = capacity;
        weapon.primaryMagazine.definition.builtInSize = capacity;

        if (weapon.primaryMagazine.contents > capacity)
            weapon.primaryMagazine.contents = capacity;
        weapon.SendNetworkUpdate();
        item.MarkDirty();
    }
}
        #endregion

        #region UI

        private string IconString = null;
        private byte[] IconData = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAADwAAAA8CAYAAAA6/NlyAAAACXBIWXMAAAsTAAALEwEAmpwYAAADT0lEQVR4nO2aS2gTQRjHpz6wggc9qSD15sFLxYOKHupJBA9p1B5MqiU701Vrm8ykwSb5Jm6sfdJHEqrWR7G0SaoFk+rBB/Si6M0HgqC3ggcRtIWKSIugK2lNG2raJNtqdtv5wf+QTXZ2fpnZb2ZhERIIBAKBQCAQCPIDYfwUZnw4mUrmPbas+hLoHdoYig7JgWisLhF/6Oawu7VLTcbbdmU0ELnrSX6fmqbr/c2uSx0PM8XT0jWQ7vxMWbAv/ff2apINRGIjwUhc1ZLG7j6VMJ4xjgutmtpfKIFIbDRn4WAkfmYxF82ncCIo5xEOx/yLuWDbrUFVdvoyCp9vDi0P4eCU9B218WrfvGm6FlaD4djyEQ7mMUgIZ2DFjbCzvu1JNlVWL0msCitWOKBlHXY1dfoqnb6JfItkG3+oJxwMx1uCkVhDRzS+B2mhwqVsIZQ/yrdMxlAYq6pSNqClQJbltZjyZ3mXShNM4Seh/C12wMElkU0iU+9u7R2CduJ07ygrK1uN5oAd/Gjyt5KDlyI9gRl8yFX4BLGPm61k27xtUrg99ccw+MYYW4/0BGH8RS6yNrtHLbVitdQijZgsZNfc9ioUpZAw+DotzAeQnqhQlELM4HsuwierXK+mhKelJ00WyVVSoqxJN50J85qRnsAMbLlXTn7cZLVRk0X6kRQ3WaSu2Tb5gF6ncwGh8GYhudMuRW3tGVBbeqKq7FKmjsnUuzVxstlK9pms0vtpYRxMHJPtShFmfFKX05kwrznTaFZ7Gmd2Oee8DapEYWzu0mYutxUnKjW2ezYTCi9nK7nvANIRBdkWK2d9u+q82PHnM3wkjNelBlPuIxSiiSk8I8ugG+kJycFL/92mgd9QFGUV0tm9+3qJRT9jCvcxg8NIb0jUe0STFIVPyIgQBs81TVUGg8hoVDphv+Z7k/mqkdEgFMJahW2MFyMjcdbt3pTcFGiovOPpnop0DWa8XHMVpvAAGQ3MYFD7/cs9yGgQBu+0byb0tU3MilwfA1Om80RNTc06ZDQwhS8aR/gpMiIkw6PgAiPcgIwIZvyyBuFftlrPTmREZLtSlNjk51adwY+MjFQL2wmDTszgceqLI3+FQq9E4VC++ysQCAQCgUAgEKD/y29aqwDtIdBxPQAAAABJRU5ErkJggg==");
        private void LoadIcon() => IconString = FileStorage.server.Store(IconData, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();


        private void OnPermissionsChanged(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, iconHidePerm) && !permission.UserHasPermission(player.UserIDString, iconShowPerm))
                DestroyIcon(player);
            else
                AddUiIcon(player);
        }

        private void AddUiIcon(BasePlayer player)
        {
            if (config.IconPosition == 0) return;

            if (permission.UserHasPermission(player.UserIDString, iconHidePerm) && !permission.UserHasPermission(player.UserIDString, iconShowPerm)) return;

            if (config.IconPosition > 5)
            {
                PrintWarning("The maximum position to the right is 5, it was set to 5");
                config.IconPosition = 5;
            }
            else if (config.IconPosition < -8)
            {
                PrintWarning("The maximum position to the left is -8, it was set to -8");
                config.IconPosition = -8;
            }

            int x = (config.IconPosition > 0 ? 121 : -201) + (config.IconPosition * 64);
            var cui = new CUI();
            string UI = cui.AddPanel(
                color: "1 0.96 0.88 .15",
                anchorMin: ".5 0",
                anchorMax: ".5 0",
                offsetMin: $"{x} 18",
                offsetMax: $"{x + 60} 78",
                parent: "Hud.Menu",
                name: "CustomizableWeapons_Icon");
            cui.AddImage(
                IconString,
                anchorMin: "0 0",
                anchorMax: "1 1",
                parent: UI);
            cui.AddButton("customizableweapons.ui open", parent: UI);
            cui.RenderWithDestroy(player);
        }

        void SetupInventorySelector(BasePlayer player)
        {
            var cui = new CUI();
            string fullscreen = cui.AddPanel(
                anchorMin: "0 0",
                anchorMax: "1 1",
                parent: "Hud.Menu",
                name: "CustomizableWeapons_InventorySelectorContainer");

            var belt = player.inventory.containerBelt;
            for (int i = 0; i < belt.capacity; i++)
            {
                Item item = belt.GetSlot(i);
                if (item == null || !IsWeapon(item)) continue;
                string color = "1 1 1 1";
                CustomItem customItem;
                if (TryGetCustomItem(item, out customItem))
                    color = customItem.UiSettings.FrameColor;
                cui.AddButton(
                       command: $"customizableweapons.ui view {item.uid}",
                       color: color,
                       sprite: "assets/content/ui/ui.box.tga",
                       imageType: UnityEngine.UI.Image.Type.Tiled,
                       anchorMin: ".5 0",
                       anchorMax: ".5 0",
                       offsetMin: $"{-201 + 64 * i} 17",
                       offsetMax: $"{-139 + 64 * i} 79",
                       parent: fullscreen);
            }
            var main = player.inventory.containerMain;

            int startY = 85 + 64 * (main.capacity / 6);
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Item item = main.GetSlot(i * 6 + j);
                    if (item == null || !IsWeapon(item)) continue;
                    string color = "1 1 1 1";
                    CustomItem customItem;
                    if (TryGetCustomItem(item, out customItem))
                        color = customItem.UiSettings.FrameColor;
                    cui.AddButton(
                        command: $"customizableweapons.ui view {item.uid}",
                        color: color,
                        sprite: "assets/content/ui/ui.box.tga",
                        imageType: UnityEngine.UI.Image.Type.Tiled,
                        anchorMin: ".5 0",
                        anchorMax: ".5 0",
                        offsetMin: $"{-201 + 64 * j} {startY - 64 * (i + 1)}",
                        offsetMax: $"{-139 + 64 * j} {startY - 2 - 64 * i}",
                        parent: fullscreen);
                }
            }
            cui.RenderWithDestroy(player);
        }

        void ShowItemInfo(BasePlayer player, ulong uid)
        {
            Item item = player.inventory.FindItemByUID(new ItemId(uid));
            if (item == null) return;

            ItemDefinition itemDef = item.info;

            string itemName = string.Empty;
            string itemNameColor = "1 1 1 1";
            string itemDescription = string.Empty;
            WeaponData weaponData = null;
            WeaponData weaponDataDefault = null;
            int itemId = 0;

            CustomItem customItem;
            if (TryGetCustomItem(item, out customItem))
            {
                var nameLangKey = $"{itemDef.shortname}:Name";
                var descriptionLangKey = $"{itemDef.shortname}:Description";
                itemName = GetMessage(nameLangKey, player.UserIDString);
                if (itemName == nameLangKey)
                    itemName = customItem.Name;
                itemNameColor = customItem.UiSettings.NameColor;
                itemDescription = GetMessage(descriptionLangKey, player.UserIDString);
                if (itemDescription == descriptionLangKey)
                    itemDescription = customItem.Description;
                weaponData = customItem;
            }
            else
            {
                if (!TryGetDefaultWeaponData(player, item, out weaponData))
                    return;
                weaponDataDefault = weaponData;
            }
            if (weaponData == null) return;
            Dictionary<string, string> translation = null;
            if (RustTranslationAPI != null && (itemName == string.Empty || itemDescription == string.Empty)) translation = RustTranslationAPI.Call<Dictionary<string, string>>("GetTranslations", lang.GetLanguage(player.UserIDString));
            if (itemName == string.Empty)
            {
                if (translation == null || !translation.TryGetValue($"{itemDef.shortname}", out itemName))
                    itemName = itemDef.displayName.translated;
            }

            if (itemDescription == string.Empty)
            {
                if (translation == null || !translation.TryGetValue($"{itemDef.shortname}.desc", out itemDescription))
                    itemDescription = itemDef.displayDescription.translated;
            }

            itemId = itemDef.itemid;

            var cui = new CUI();
            string fullscreen = cui.AddContainer(
                 anchorMin: "0 0",
                 anchorMax: "1 1",
                 parent: "Hud.Menu",
                 name: "CustomizableWeapons_ItemInfoContainer");
            string blur = cui.AddColorPanel(
                color: "0 0 0 0.5",
                material: "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                imageType: UnityEngine.UI.Image.Type.Tiled,
                anchorMin: "0.5 0",
                anchorMax: "0.5 0",
                offsetMin: "-200 360",
                offsetMax: "182 650",
                parent: fullscreen);
            string infoPanel = cui.AddColorPanel(
                color: ".2 .2 .2 0.95",
                sprite: "assets/content/ui/ui.background.rounded.png",
                imageType: UnityEngine.UI.Image.Type.Tiled,
                anchorMin: "0.5 0",
                anchorMax: "0.5 0",
                offsetMin: "-200 360",
                offsetMax: "182 650",
                parent: fullscreen);
            string nameTextbox = cui.AddText(
                color: itemNameColor,
                text: itemName,
                font: CUI.Font.RobotoCondensedBold,
                fontSize: 16,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: "10 -30",
                offsetMax: "252 -10",
                parent: infoPanel);
            string descriptionPanel = cui.AddColorPanel(
               color: "0.147 0.147 0.147 0.95",
               sprite: "assets/content/ui/ui.background.rounded.png",
               imageType: UnityEngine.UI.Image.Type.Tiled,
               anchorMin: "0 1",
               anchorMax: "0 1",
               offsetMin: "10 -280",
               offsetMax: "262 -40",
               parent: infoPanel);
            string descriptionTextbox = cui.AddText(
                text: itemDescription,
                font: CUI.Font.RobotoCondensedRegular,
                fontSize: 12,
                align: TextAnchor.UpperLeft,
               anchorMin: "0 0",
               anchorMax: "0 0",
               offsetMin: "10 10",
               offsetMax: "242 230",
               parent: descriptionPanel);
            string iconPanel = cui.AddColorPanel(
               color: "0.14 0.14 0.14 0.95",
               sprite: "assets/content/ui/ui.background.rounded.png",
               imageType: UnityEngine.UI.Image.Type.Tiled,
               anchorMin: "1 1",
               anchorMax: "1 1",
               offsetMin: "-110 -110",
               offsetMax: "-10 -10",
               parent: infoPanel);
            string icon = cui.AddIcon(
               itemId: itemId,
               skin: item.skin,
               anchorMin: "1 1",
               anchorMax: "1 1",
               offsetMin: "-110 -110",
               offsetMax: "-10 -10",
               parent: infoPanel);

            Dictionary<string, float> FieldsForRender = DamageTypes.ToDictionary(x => DamageTypeNames[x], x => weaponData.DamageTypeList.First().Value.Get(x));
            FieldsForRender = FieldsForRender.Where(i => i.Value != 0f).ToDictionary(i => i.Key, i => i.Value);

            int y = 618 - FieldsForRender.Count * 18;
            
            string statsPanel = cui.AddColorPanel(
               color: ".2 .2 .2 0.95",
               sprite: "assets/content/ui/ui.background.rounded.png",
               imageType: UnityEngine.UI.Image.Type.Tiled,
               anchorMin: "0.5 0",
               anchorMax: "0.5 0",
               offsetMin: $"190 {y}",
               offsetMax: "500 650",
               parent: fullscreen);
            string statsHeader = cui.AddText(
                text: GetMessage(LangKeys.ItemInfoPanelHeader, player.UserIDString),
                font: CUI.Font.RobotoCondensedBold,
                fontSize: 14,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: "10 -20",
                offsetMax: "290 -10",
                parent: statsPanel);
            string statsContainer = cui.AddColorPanel(
                sprite: "assets/content/ui/ui.background.rounded.png",
                imageType: UnityEngine.UI.Image.Type.Tiled,
                anchorMin: "0 0",
                anchorMax: "1 1",
                offsetMin: "10 10",
                offsetMax: "-10 -30",
                parent: statsPanel);

            string permColor = "0.94 0.75 0.15 1";

            for (int i = 0; i < FieldsForRender.Count; i++)
            {
                var pair = FieldsForRender.ElementAt(i);
                cui.AddText(
                       text: GetMessage(pair.Key, player.UserIDString),
                       font: CUI.Font.RobotoCondensedBold,
                       fontSize: 12,
                       align: TextAnchor.MiddleLeft,
                       anchorMin: "0 1",
                       anchorMax: "0 1",
                       offsetMin: $"10 {-10 + (-18 * i)}",
                       offsetMax: $"210 {-10 + (-18 * i + 10)}",
                       parent: statsContainer);
                cui.AddText(
                    color: weaponDataDefault != null && pair.Value != weaponDataDefault?.DamageTypes?.First().Value[pair.Key] ? permColor : "1 1 1 1",
                    text: $"{pair.Value}",
                    font: CUI.Font.RobotoCondensedRegular,
                    fontSize: 12,
                    align: TextAnchor.MiddleCenter,
                    anchorMin: "0 1",
                    anchorMax: "0 1",
                    offsetMin: $"215 {-10 + (-18 * i)}",
                    offsetMax: $"280 {-10 + (-18 * i + 10)}",
                    parent: statsContainer); ;
            }

            string tipsPanel = cui.AddColorPanel(
              color: ".2 .2 .2 0.95",
              sprite: "assets/content/ui/ui.background.rounded.png",
              imageType: UnityEngine.UI.Image.Type.Tiled,
              anchorMin: "0.5 0",
              anchorMax: "0.5 0",
              offsetMin: $"190 {y - 165}",
              offsetMax: $"500 {y - 115}",
              parent: fullscreen);

            string tipsHeader = cui.AddText(
                text: GetMessage(LangKeys.TipsHeader, player.UserIDString),
                font: CUI.Font.RobotoCondensedBold,
                fontSize: 14,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: "10 -20",
                offsetMax: "290 -10",
                parent: tipsPanel);

            string yellowQuad = cui.AddColorPanel(
                color: permColor,
                imageType: UnityEngine.UI.Image.Type.Tiled,
                anchorMin: "0 0",
                anchorMax: "0 0",
                offsetMin: "20 10",
                offsetMax: "35 25",
                parent: tipsPanel);
            string yellowQuadDesc = cui.AddText(
                text: GetMessage(LangKeys.StatYellowTextDescription, player.UserIDString),
                font: CUI.Font.RobotoCondensedRegular,
                fontSize: 12,
                align: TextAnchor.MiddleLeft,
                anchorMin: "0 0",
                anchorMax: "0 0",
                offsetMin: "45 10",
                offsetMax: "285 25",
                parent: tipsPanel);

            
            string characteristicsPanel = cui.AddColorPanel(
               color: ".2 .2 .2 0.95",
               sprite: "assets/content/ui/ui.background.rounded.png",
               imageType: UnityEngine.UI.Image.Type.Tiled,
               anchorMin: "0.5 0",
               anchorMax: "0.5 0",
               offsetMin: $"190 { y - 110}",
               offsetMax: $"500 { y - 5}",
               parent: fullscreen);
            string characteristicsHeader = cui.AddText(
                text: GetMessage(LangKeys.CharacteristicsPanelHeader, player.UserIDString),
                font: CUI.Font.RobotoCondensedBold,
                fontSize: 14,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: "10 -20",
                offsetMax: "290 -10",
                parent: characteristicsPanel);
            string characteristicsContainer = cui.AddColorPanel(
                sprite: "assets/content/ui/ui.background.rounded.png",
                imageType: UnityEngine.UI.Image.Type.Tiled,
                anchorMin: "0 0",
                anchorMax: "1 1",
                offsetMin: "10 10",
                offsetMax: "-10 -30",
                parent: characteristicsPanel);
            cui.AddText(
                       text: GetMessage(LangKeys.MagazineCapacity, player.UserIDString),
                       font: CUI.Font.RobotoCondensedBold,
                       fontSize: 12,
                       align: TextAnchor.MiddleLeft,
                       anchorMin: "0 1",
                       anchorMax: "0 1",
                       offsetMin: $"10 {-10 + (-18 * 0)}",
                       offsetMax: $"210 {-10 + (-18 * 0 + 10)}",
                       parent: characteristicsContainer);
            cui.AddText(
                color: weaponDataDefault != null && weaponData.MagazineCapacity != weaponDataDefault?.MagazineCapacity ? permColor : "1 1 1 1",
                text: $"{weaponData.MagazineCapacity}",
                font: CUI.Font.RobotoCondensedRegular,
                fontSize: 12,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: $"215 {-10 + (-18 * 0)}",
                offsetMax: $"280 {-10 + (-18 * 0 + 10)}",
                parent: characteristicsContainer); ;
            cui.AddText(
                       text: GetMessage(LangKeys.MaxDurability, player.UserIDString),
                       font: CUI.Font.RobotoCondensedBold,
                       fontSize: 12,
                       align: TextAnchor.MiddleLeft,
                       anchorMin: "0 1",
                       anchorMax: "0 1",
                       offsetMin: $"10 {-10 + (-18 * 1)}",
                       offsetMax: $"210 {-10 + (-18 * 1 + 10)}",
                       parent: characteristicsContainer);
            cui.AddText(
                color: weaponDataDefault != null && weaponData.Durability != weaponDataDefault?.Durability ? permColor : "1 1 1 1",
                text: $"{weaponData.Durability}",
                font: CUI.Font.RobotoCondensedRegular,
                fontSize: 12,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: $"215 {-10 + (-18 * 1)}",
                offsetMax: $"280 {-10 + (-18 * 1 + 10)}",
                parent: characteristicsContainer); ;
            cui.AddText(
                     text: GetMessage(LangKeys.Unbreakable, player.UserIDString),
                     font: CUI.Font.RobotoCondensedBold,
                     fontSize: 12,
                     align: TextAnchor.MiddleLeft,
                     anchorMin: "0 1",
                     anchorMax: "0 1",
                     offsetMin: $"10 {-10 + (-18 * 2)}",
                     offsetMax: $"210 {-10 + (-18 * 2 + 10)}",
                     parent: characteristicsContainer);
            cui.AddText(
                color: weaponDataDefault != null && weaponData.Unbreakable != weaponDataDefault?.Unbreakable ? permColor : "1 1 1 1",
                text: $"{(weaponData.Unbreakable ? GetMessage(LangKeys.Yes, player.UserIDString) : GetMessage(LangKeys.No, player.UserIDString))}",
                font: CUI.Font.RobotoCondensedRegular,
                fontSize: 12,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: $"215 {-10 + (-18 * 2)}",
                offsetMax: $"280 {-10 + (-18 * 2 + 10)}",
                parent: characteristicsContainer); ;
            cui.AddText(
                text: GetMessage(LangKeys.FastReload, player.UserIDString),
                font: CUI.Font.RobotoCondensedBold,
                fontSize: 12,
                align: TextAnchor.MiddleLeft,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: $"10 {-10 + (-18 * 3)}",
                offsetMax: $"210 {-10 + (-18 * 3 + 10)}",
                parent: characteristicsContainer);
            cui.AddText(
                color: weaponDataDefault != null && weaponData.FastReload != weaponDataDefault?.FastReload ? permColor : "1 1 1 1",
                text: $"{(weaponData.FastReload ? GetMessage(LangKeys.Yes, player.UserIDString) : GetMessage(LangKeys.No, player.UserIDString))}",
                font: CUI.Font.RobotoCondensedRegular,
                fontSize: 12,
                align: TextAnchor.MiddleCenter,
                anchorMin: "0 1",
                anchorMax: "0 1",
                offsetMin: $"215 {-10 + (-18 * 3)}",
                offsetMax: $"280 {-10 + (-18 * 3 + 10)}",
                parent: characteristicsContainer); ;
            cui.RenderWithDestroy(player);
        }


        [ConsoleCommand("customizableweapons.ui")]
        private void ui(ConsoleSystem.Arg arg)
        {
            if (arg?.Args?.Count() == 0 || arg.Player() == null)
                return;

            BasePlayer player = arg.Player();
            switch (arg.Args[0])
            {
                case "open":
                    DestroyUI(player);
                    try
                    {
                        if (player.inventory.loot.entitySource == RelationshipManager.ServerInstance)
                            player.inventory.loot.containers.Where(c => c.entityOwner == RelationshipManager.ServerInstance && c.capacity == 0).ToList().ForEach(c => c.Kill());
                    }
                    catch { }
                    OpenEmptyContainer(player);
                    SetupInventorySelector(player);
                    break;
                case "view":
                    ShowItemInfo(player, Convert.ToUInt32(arg.Args[1]));
                    break;
            }
        }

        void OpenEmptyContainer(BasePlayer player)
        {
            ItemContainer container = new ItemContainer();
            container.isServer = true;
            container.flags = NoSortify;
            container.allowedContents = ItemContainer.ContentsType.Generic;
            container.maxStackSize = 0;
            container.capacity = 0;
            container.entityOwner = RelationshipManager.ServerInstance;
            container.SetFlag(ItemContainer.Flag.NoItemInput, true);
            container.GiveUID();

            player.inventory.loot.Clear();
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.entitySource = container.entityOwner;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.AddContainer(container);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic_resizable");
        }


        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CustomizableWeapons_ItemInfoContainer");
            CuiHelper.DestroyUi(player, "CustomizableWeapons_GearInfoContainer");
            CuiHelper.DestroyUi(player, "CustomizableWeapons_InventorySelectorContainer");
        }
        private void DestroyIcon(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CustomizableWeapons_Icon");
        }
        #endregion

        #region Config

        private static ConfigData config;

        private class ConfigData
        {

            public class GlobalSettingsClass
            {

                [JsonProperty(PropertyName = "Disable the mechanics of unloading ammunition when removing the magazine?")]
                public bool DisableUnload { get; set; } = false;
                [JsonProperty(PropertyName = "Disable the magazine bonus when you change capacity?")]
                public bool DisableMagazineBonus { get; set; } = false;
				    [JsonProperty(PropertyName = "Disable Magazine Capacity Modifications")]
    public bool DisableMagazineCapacity { get; set; } = false;

    [JsonProperty(PropertyName = "Disable Weapon Damage Modifications")]
    public bool DisableWeaponDamage { get; set; } = false;
                [JsonProperty(PropertyName = "Limit the number of bullets loaded in the weapon to the capacity of the magazine set for the player holding the weapon?")]
                public bool LimitBullets { get; set; } = true;

                public class GiveOptionsClass
                {
                    [JsonProperty(PropertyName = "Attachments")]
                    public Dictionary<string, bool> Attachments { get; set; }
                }

                [JsonProperty(PropertyName = "Give Options")]
                public GiveOptionsClass GiveOptions = new GiveOptionsClass();
            }

            [JsonProperty(PropertyName = "Displaying information about a weapon when it is selected in the inventory")]
            public bool ItemSelectInfo { get; set; } = true;
            [JsonProperty(PropertyName = "Icon Position (0 - Off | -1 - left by 1 slot, 1 - right by 1 slot | ..)")]
#if CARBON
             public int IconPosition { get; set; } = Carbon.Community.Runtime.Plugins.Plugins.Exists(p => p.Name == "CustomizableProtection") ? -2 : -1;
#else
            public int IconPosition { get; set; } = CSharpPluginLoader.Instance.LoadedPlugins.ContainsKey("CustomizableProtection") ? -2 : -1;
#endif
            [JsonProperty(PropertyName = "Global Settings")]
            public GlobalSettingsClass GlobalSettings { get; set; } = new GlobalSettingsClass();
            [JsonIgnore]
            public DataItems DataItems { get; set; } = new DataItems("CustomizableWeapons");

        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                    _LoadDefaultConfig();
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
                {
                    Puts($"Exception: {ex}");
                    _LoadDefaultConfig();
                    return;
                }
                throw;
            }
        }
        private void _LoadDefaultConfig()
        {
            Puts("Configuration file missing or corrupt, creating default config file.");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
#endregion

#region Lang

        public class LangKeys
        {
            public const string Yes = nameof(Yes);
            public const string No = nameof(No);
            public const string ItemInfoPanelHeader = nameof(ItemInfoPanelHeader);
            public const string TipsHeader = nameof(TipsHeader);
            public const string CharacteristicsPanelHeader = nameof(CharacteristicsPanelHeader);
            public const string MagazineCapacity = nameof(MagazineCapacity);
            public const string MaxDurability = nameof(MaxDurability);
            public const string Unbreakable = nameof(Unbreakable);
            public const string FastReload = nameof(FastReload);
            public const string StatYellowTextDescription = nameof(StatYellowTextDescription);
            public const string Hunger = nameof(Hunger);
            public const string Thirst = nameof(Thirst);
            public const string Cold = nameof(Cold);
            public const string Drowned = nameof(Drowned);
            public const string Heat = nameof(Heat);
            public const string Bleeding = nameof(Bleeding);
            public const string Poison = nameof(Poison);
            public const string Bullet = nameof(Bullet);
            public const string Slash = nameof(Slash);
            public const string Blunt = nameof(Blunt);
            public const string Radiation = nameof(Radiation);
            public const string Bite = nameof(Bite);
            public const string Stab = nameof(Stab);
            public const string Explosion = nameof(Explosion);
            public const string ElectricShock = nameof(ElectricShock);
            public const string Arrow = nameof(Arrow);
            public const string AntiVehicle = nameof(AntiVehicle);
            public const string Collision = nameof(Collision);
        }
        private void LoadLang()
        {
            var en = new Dictionary<string, string>
            {
                [LangKeys.Yes] = "Yes",
                [LangKeys.No] = "No",
                [LangKeys.ItemInfoPanelHeader] = "Item base damage information:",
                [LangKeys.TipsHeader] = "Tips:",
                [LangKeys.CharacteristicsPanelHeader] = "Weapon Characteristics:",
                [LangKeys.MagazineCapacity] = "Magazine Capacity",
                [LangKeys.MaxDurability] = "Maximum weapon durability",
                [LangKeys.Unbreakable] = "The ability not to break",
                [LangKeys.FastReload] = "Fast Reload",
                [LangKeys.StatYellowTextDescription] = "Advantage of a permission",
                [LangKeys.Hunger] = "Damage from <color=#fcd186>hunger</color>",
                [LangKeys.Thirst] = "Damage from <color=#3b9ad1>thirst</color>",
                [LangKeys.Cold] = "Damage from <color=#60d4eb>cold</color>",
                [LangKeys.Drowned] = "<color=#1e2985>Wet</color> damage",
                [LangKeys.Heat] = "Damage from <color=#e66d10>heat</color>",
                [LangKeys.Bleeding] = "Damage from <color=#b0000c>bleeding</color>",
                [LangKeys.Poison] = "Damage from <color=#8a15cf>poison</color>",
                [LangKeys.Bullet] = "Damage from <color=#ffca42>bullets</color>",
                [LangKeys.Slash] = "Damage from <color=#a1a1a1>slash damage</color>",
                [LangKeys.Blunt] = "Damage from <color=#a1a1a1>blunt damage</color>",
                [LangKeys.Radiation] = "Damage from <color=#6acf48>radiation</color>",
                [LangKeys.Bite] = "Damage from <color=#a1a1a1>bites</color>",
                [LangKeys.Stab] = "Damage from <color=#a1a1a1>stab damage</color>",
                [LangKeys.Explosion] = "Damage from <color=#e68750>explosion</color>",
                [LangKeys.ElectricShock] = "Damage from <color=#57f7dd>electroshock</color>",
                [LangKeys.Arrow] = "Damage from <color=#a1a1a1>arrows</color>",
                [LangKeys.AntiVehicle] = "Damage against <color=#639443>vehicle</color>",
                [LangKeys.Collision] = "Damage from <color=#5e5e5e>collision</color>",
            };
            var ru = new Dictionary<string, string>
            {
                [LangKeys.Yes] = "Да",
                [LangKeys.No] = "Нет",
                [LangKeys.TipsHeader] = "Подсказки:",
                [LangKeys.ItemInfoPanelHeader] = "Информация о базовом уроне предмета:",
                [LangKeys.CharacteristicsPanelHeader] = "Характеристики оружия:",
                [LangKeys.MagazineCapacity] = "Вместимость магазина",
                [LangKeys.Unbreakable] = "Способность не ломаться",
                [LangKeys.MaxDurability] = "Максимальная прочность оружия",
                [LangKeys.FastReload] = "Быстрая перезарядка",
                [LangKeys.StatYellowTextDescription] = "Преимущество разрешения",
                [LangKeys.Hunger] = "Урон от <color=#fcd186>голода</color>",
                [LangKeys.Thirst] = "Урон от <color=#3b9ad1>жажды</color>",
                [LangKeys.Cold] = "Урон от <color=#60d4eb>холода</color>",
                [LangKeys.Drowned] = "<color=#1e2985>Влажный</color> урон",
                [LangKeys.Heat] = "Урон от <color=#e66d10>жары</color>",
                [LangKeys.Bleeding] = "Урон от <color=#b0000c>кровотечения</color>",
                [LangKeys.Poison] = "Урон от <color=#8a15cf>яда</color>",
                [LangKeys.Bullet] = "Урон от <color=#ffca42>пуль</color>",
                [LangKeys.Slash] = "Урон от <color=#a1a1a1>разрубающего урона</color>",
                [LangKeys.Blunt] = "Урон от <color=#a1a1a1>тупого урона</color>",
                [LangKeys.Radiation] = "Урон от <color=#6acf48>радиации</color>",
                [LangKeys.Bite] = "Урон от <color=#a1a1a1>укусов</color>",
                [LangKeys.Stab] = "Урон от <color=#a1a1a1>режущего урона</color>",
                [LangKeys.Explosion] = "Урон от <color=#e68750>взрыва</color>",
                [LangKeys.ElectricShock] = "Урон от <color=#57f7dd>электрошока</color>",
                [LangKeys.Arrow] = "Урон от <color=#a1a1a1>стрел</color>",
                [LangKeys.AntiVehicle] = "Урон против <color=#639443>транспорта</color>",
                [LangKeys.Collision] = "Урон от <color=#5e5e5e>столкновений</color>",
            };
            foreach (var item in config.DataItems.Custom)
            {
                en.Add($"{item.Key}:Name", item.Value.Name);
                en.Add($"{item.Key}:Description", item.Value.Description);
                ru.Add($"{item.Key}:Name", item.Value.Name);
                ru.Add($"{item.Key}:Description", item.Value.Description);
            }

            lang.RegisterMessages(en, this);

            lang.RegisterMessages(ru, this, "ru");
        }

        private string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, this, userID);

        private string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
#endregion Lang

#region Data Items
        public class DataItems
        {
            private DataFileSystem DataFileSystem { get; set; }
            public DataFileSystem CustomFs { get; set; }
            public Dictionary<string, CustomItem> Custom { get; set; }
            public Dictionary<string, DataFileSystem> PermissionsFss { get; set; }
            public List<KeyValuePair<string, Dictionary<string, WeaponData>>> Permissions { get; set; }
            public DataFileSystem DefaultFs { get; set; }
            public Dictionary<string, WeaponData> Default { get; set; }
            public DataItems(string folderName)
            {

                DataFileSystem = new DataFileSystem(Path.Combine(Interface.Oxide.DataDirectory, folderName));
                DefaultFs = new DataFileSystem(Path.Combine(DataFileSystem.Directory, "Default"));
                Default = new Dictionary<string, WeaponData>();
                PermissionsFss = new Dictionary<string, DataFileSystem>()
                {
                    ["root"] = new DataFileSystem(Path.Combine(DataFileSystem.Directory, "Permissions"))
                };

                Permissions = new List<KeyValuePair<string, Dictionary<string, WeaponData>>>();
                CustomFs = new DataFileSystem(Path.Combine(DataFileSystem.Directory, "Custom"));
                Custom = new Dictionary<string, CustomItem>();
            }
            public void Reload(CustomizableWeapons plugin)
            {
                Default = new Dictionary<string, WeaponData>();
                Custom = new Dictionary<string, CustomItem>();
                Permissions = new List<KeyValuePair<string, Dictionary<string, WeaponData>>>();
                Initialize(plugin);
            }


            public void InitiliazeDefaultItems()
            {
                foreach (var def in ItemManager.itemList)
                {
                    if (def.category != ItemCategory.Weapon && def.category != ItemCategory.Tool) continue;

                    GameObjectRef heldEntityPrefabRef = def.GetComponent<ItemModEntity>()?.entityPrefab;
                    if (heldEntityPrefabRef == null || !heldEntityPrefabRef.isValid)
                        continue;

                    GameObject heldEntityPrefab = heldEntityPrefabRef.Get();

                    Component component;
                    Type baseProjectileType = typeof(BaseProjectile);
                    Type baseMeleeType = typeof(BaseMelee);
                    Type throwWeaponType = typeof(ThrownWeapon);
                    Type flameThrowerType = typeof(FlameThrower);
                    if (heldEntityPrefab.TryGetComponent(baseProjectileType, out component) || heldEntityPrefab.TryGetComponent(baseMeleeType, out component) || heldEntityPrefab.TryGetComponent(throwWeaponType, out component) || heldEntityPrefab.TryGetComponent(flameThrowerType, out component))
                    {

                        if (!CustomizableWeapons.DefaultDamageWeapons.ContainsKey(def.shortname))
                            CustomizableWeapons.DefaultDamageWeapons[def.shortname] = new Dictionary<string, Rust.DamageTypeList>();
                        bool hasChanged = false;
                        var itemFromData = DefaultFs.ReadObject<WeaponData>(def.shortname);
                        if (itemFromData.Shortname == null)
                        {
                            itemFromData = new WeaponData()
                            {
                                Shortname = def.shortname,
                                Durability = def.condition.max,
                                Unbreakable = !def.condition.enabled,
                            };
                            if (baseProjectileType.IsAssignableFrom(component.GetType()))
                            {
                                itemFromData.MagazineCapacity = (component as BaseProjectile).primaryMagazine.definition.builtInSize;
                            }
                            hasChanged = true;
                        }

                        if (itemFromData.DamageTypes == null)
                            itemFromData.DamageTypes = new Dictionary<string, Dictionary<string, float>>();


                        if (baseProjectileType.IsAssignableFrom(component.GetType()))
                        {
                            BaseProjectile weapon = component as BaseProjectile;
                            List<ItemDefinition> availableAmmos = ItemManager.GetItemDefinitions().Where(d => d.GetComponent<ItemModProjectile>()?.IsAmmo(weapon.primaryMagazine.definition.ammoTypes) == true).ToList();
                            foreach (var ammo in availableAmmos)
                            {
                                if (!itemFromData.DamageTypes.ContainsKey(ammo.shortname))
                                    itemFromData.DamageTypes[ammo.shortname] = new Dictionary<string, float>();
                                Projectile projectile = ammo.GetComponent<ItemModProjectile>().projectileObject.Get().GetComponent<Projectile>();
                                if (projectile != null)
                                {
                                    HitInfo getter = new HitInfo();
                                    projectile.CalculateDamage(getter, Projectile.Modifier.Default, 1f);
                                    getter.damageTypes.ScaleAll(weapon.GetDamageScale(false));
                                    CustomizableWeapons.DefaultDamageWeapons[def.shortname][ammo.shortname] = getter.damageTypes;
                                    for (int i = 0; i < DamageTypes.Count; i++)
                                    {
                                        Rust.DamageType type = DamageTypes[i];
                                        string DamageTypeName = DamageTypeNames.Values.ElementAt(i);
                                        if (!itemFromData.DamageTypes[ammo.shortname].ContainsKey(DamageTypeName))
                                        {
                                            float value = getter.damageTypes.Get(type);
                                            itemFromData.DamageTypes[ammo.shortname][DamageTypeName] = value;
                                            hasChanged = true;
                                        }
                                    };
                                }
                                else
                                {
                                    var timedExplosive = ammo.GetComponent<ItemModProjectile>()?.projectileObject.Get().GetComponent<TimedExplosive>();
                                    var newDamageTypeList = new Rust.DamageTypeList();
                                    foreach (var damageTypeEntry in timedExplosive.damageTypes)
                                        newDamageTypeList.Add(damageTypeEntry.type, damageTypeEntry.amount);

                                    CustomizableWeapons.DefaultDamageWeapons[def.shortname][ammo.shortname] = newDamageTypeList;
                                    for (int i = 0; i < DamageTypes.Count; i++)
                                    {
                                        Rust.DamageType type = DamageTypes[i];
                                        string DamageTypeName = DamageTypeNames.Values.ElementAt(i);
                                        if (!itemFromData.DamageTypes[ammo.shortname].ContainsKey(DamageTypeName))
                                        {
                                            float value = newDamageTypeList.Get(type);
                                            itemFromData.DamageTypes[ammo.shortname][DamageTypeName] = value;
                                            hasChanged = true;
                                        }
                                    };
                                }
                            }
                          
                        }
                        else if (baseMeleeType.IsAssignableFrom(component.GetType()))
                        {
                            BaseMelee weaponMelee = component as BaseMelee;
                            if (!itemFromData.DamageTypes.ContainsKey("melee"))
                                itemFromData.DamageTypes["melee"] = new Dictionary<string, float>();

                            var newDamageTypeList = new Rust.DamageTypeList();
                            foreach (var damageTypeEntry in weaponMelee.damageTypes)
                                newDamageTypeList.Add(damageTypeEntry.type, damageTypeEntry.amount);

                            CustomizableWeapons.DefaultDamageWeapons[def.shortname]["melee"] = newDamageTypeList;
                            for (int i = 0; i < DamageTypes.Count; i++)
                            {
                                Rust.DamageType type = DamageTypes[i];
                                string DamageTypeName = DamageTypeNames.Values.ElementAt(i);
                                if (!itemFromData.DamageTypes["melee"].ContainsKey(DamageTypeName))
                                {
                                    float value = newDamageTypeList.Get(type);
                                    itemFromData.DamageTypes["melee"][DamageTypeName] = value;
                                    hasChanged = true;
                                }
                            };

                            Projectile projectile = def.GetComponent<ItemModProjectile>()?.projectileObject?.Get()?.GetComponent<Projectile>();
                            if (projectile != null)
                            {
                                if (!itemFromData.DamageTypes.ContainsKey("throw"))
                                    itemFromData.DamageTypes["throw"] = new Dictionary<string, float>();
                                var ThrowNewDamageTypeList = new Rust.DamageTypeList();
                                foreach (var damageTypeEntry in projectile.damageTypes)
                                    ThrowNewDamageTypeList.Add(damageTypeEntry.type, damageTypeEntry.amount);

                                CustomizableWeapons.DefaultDamageWeapons[def.shortname]["throw"] = ThrowNewDamageTypeList;

                                for (int i = 0; i < DamageTypes.Count; i++)
                                {
                                    Rust.DamageType type = DamageTypes[i];
                                    string DamageTypeName = DamageTypeNames.Values.ElementAt(i);
                                    if (!itemFromData.DamageTypes["throw"].ContainsKey(DamageTypeName))
                                    {
                                        float value = ThrowNewDamageTypeList.Get(type);
                                        itemFromData.DamageTypes["throw"][DamageTypeName] = value;
                                        hasChanged = true;
                                    }
                                };
                            }
                        }
                        else if(throwWeaponType.IsAssignableFrom(component.GetType()))
                        {
                            ThrownWeapon thrownableWeapon = component as ThrownWeapon;

                            TimedExplosive timedExplosive = thrownableWeapon.prefabToThrow?.Get()?.GetComponent<TimedExplosive>();
                            if (timedExplosive != null)
                            {
                                if (!itemFromData.DamageTypes.ContainsKey("throw"))
                                    itemFromData.DamageTypes["throw"] = new Dictionary<string, float>();

                                var ThrowNewDamageTypeList = new Rust.DamageTypeList();
                                foreach (var damageTypeEntry in timedExplosive.damageTypes)
                                    ThrowNewDamageTypeList.Add(damageTypeEntry.type, damageTypeEntry.amount);

                                CustomizableWeapons.DefaultDamageWeapons[def.shortname]["throw"] = ThrowNewDamageTypeList;

                                for (int i = 0; i < DamageTypes.Count; i++)
                                {
                                    Rust.DamageType type = DamageTypes[i];
                                    string DamageTypeName = DamageTypeNames.Values.ElementAt(i);
                                    if (!itemFromData.DamageTypes["throw"].ContainsKey(DamageTypeName))
                                    {
                                        float value = ThrowNewDamageTypeList.Get(type);
                                        itemFromData.DamageTypes["throw"][DamageTypeName] = value;
                                        hasChanged = true;
                                    }
                                };
                            }
                        }
                        else if (flameThrowerType.IsAssignableFrom(component.GetType()))
                        {
                            FlameThrower flameThrower = component as FlameThrower;

                            if (!itemFromData.DamageTypes.ContainsKey("ammo.lowgrade"))
                                itemFromData.DamageTypes["ammo.lowgrade"] = new Dictionary<string, float>();

                            var damageList = new Rust.DamageTypeList();
                            foreach (var damageTypeEntry in flameThrower.damagePerSec)
                                damageList.Add(damageTypeEntry.type, damageTypeEntry.amount);

                            CustomizableWeapons.DefaultDamageWeapons[def.shortname]["ammo.lowgrade"] = damageList;

                            for (int i = 0; i < DamageTypes.Count; i++)
                            {
                                Rust.DamageType type = DamageTypes[i];
                                string DamageTypeName = DamageTypeNames.Values.ElementAt(i);
                                if (!itemFromData.DamageTypes["ammo.lowgrade"].ContainsKey(DamageTypeName))
                                {
                                    float value = damageList.Get(type);
                                    itemFromData.DamageTypes["ammo.lowgrade"][DamageTypeName] = value;
                                    hasChanged = true;
                                }
                            }
                        }

                        if (hasChanged)
                            DefaultFs.WriteObject<WeaponData>(def.shortname, itemFromData);
                        Default.Add(itemFromData.Shortname, itemFromData);
                    }
                }
            }
            public void Initialize(CustomizableWeapons plugin)
            {
                InitiliazeDefaultItems();


                if (System.IO.Directory.Exists(PermissionsFss["root"].Directory))
                    foreach (var folderName in System.IO.Directory.GetDirectories(PermissionsFss["root"].Directory).Select(path => new DirectoryInfo(path).Name))
                    {
                        string trimmedKey = String.Concat(folderName.Where(c => !Char.IsWhiteSpace(c)));
                        int indexSecondBracket = trimmedKey.IndexOf(']');
                        var perm = trimmedKey.Substring(indexSecondBracket + 1);
                        int priorityInName;
                        if (!int.TryParse(trimmedKey.Substring(1, indexSecondBracket - 1), out priorityInName))
                        {
                            plugin.PrintError($"'{folderName}' folder has an incorrect name format, required '[priority] permissionname'!");
                            continue;
                        }
                        if (PermissionsFss.ContainsKey(perm))
                        {
                            plugin.PrintWarning($"Skip duplicate permission folder ({perm})");
                            continue;
                        }
                        var path = Path.Combine(PermissionsFss["root"].Directory, folderName);
                        if (!Directory.Exists(path)) continue;
                        var fs = new DataFileSystem(path);
                        PermissionsFss.Add(perm, fs);
                        Permissions.Insert(priorityInName, new KeyValuePair<string, Dictionary<string, WeaponData>>(trimmedKey.Substring(indexSecondBracket + 1), fs.GetFiles().Select(name => Path.GetFileNameWithoutExtension(name)).ToDictionary(name => name, name => fs.ReadObject<WeaponData>(name))));
                    }
                if (System.IO.Directory.Exists(CustomFs.Directory))
                    foreach (var name in CustomFs.GetFiles().Select(name => Path.GetFileNameWithoutExtension(name)))
                    {
                        var dataFile = CustomFs.GetDatafile(name);
                        CustomItem customItem = dataFile.ReadObject<CustomItem>();
                        object oldSkinIdField = dataFile["Unique SkinId"];
                        if (oldSkinIdField != null)
                            customItem.SkinId = Convert.ToUInt64(oldSkinIdField);
                        Custom.Add(name, customItem);
                    }

                var defaultLr = Default["rifle.lr300"];
                if (defaultLr == null) return;
                if (!PermissionsFss.ContainsKey("vip2"))
                {
                    var fs = new DataFileSystem(Path.Combine(PermissionsFss["root"].Directory, "[0] vip2"));
                    PermissionsFss.Add("vip2", fs);
                    var wd = new WeaponData()
                    {
                        Shortname = defaultLr.Shortname,
                        Unbreakable = defaultLr.Unbreakable,
                        Durability = defaultLr.Durability + 200,
                        MagazineCapacity = defaultLr.MagazineCapacity + 50,
                        DamageTypes = defaultLr.DamageTypes,
                        DamageTypeList = defaultLr.DamageTypeList,
                    };
                    fs.WriteObject<WeaponData>(defaultLr.Shortname, wd);
                    Permissions.Insert(0, new KeyValuePair<string, Dictionary<string, WeaponData>>("vip2", new Dictionary<string, WeaponData>() { [defaultLr.Shortname] = wd }));

                }
                if (!PermissionsFss.ContainsKey("vip1"))
                {
                    var fs = new DataFileSystem(Path.Combine(PermissionsFss["root"].Directory, "[1] vip1"));
                    PermissionsFss.Add("vip1", fs);
                    var wd = new WeaponData()
                    {
                        Shortname = defaultLr.Shortname,
                        Unbreakable = defaultLr.Unbreakable,
                        Durability = defaultLr.Durability + 100,
                        MagazineCapacity = defaultLr.MagazineCapacity + 20,
                        DamageTypes = defaultLr.DamageTypes,
                        DamageTypeList = defaultLr.DamageTypeList,
                    };
                    fs.WriteObject<WeaponData>(defaultLr.Shortname, wd);
                    Permissions.Insert(1, new KeyValuePair<string, Dictionary<string, WeaponData>>("vip1", new Dictionary<string, WeaponData>() { [defaultLr.Shortname] = wd }));

                }

                if (!Custom.ContainsKey("admin.lr"))
                {
                    CustomItem exampleCustomItem = new CustomItem
                    {
                        Shortname = "rifle.lr300",
                        SkinId = 2400056213,
                        Name = "Admin LR300",
                        Description = "Powerful admin gun",
                        UiSettings = new CustomItem.UISettings()
                        {
                            FrameColor = "1 0 0 1",
                            NameColor = "1 0 0 1"
                        },
                        Unbreakable = true,
                        Durability = 1000000,
                        MagazineCapacity = 10000,
                        DamageTypeList = new Dictionary<string, Rust.DamageTypeList>(),
                        DamageTypes = defaultLr.DamageTypes.ToDictionary(x => x.Key, x => x.Value.ToDictionary(b => b.Key, b => 1000000f)),
                    };
                    CustomFs.WriteObject("admin.lr", exampleCustomItem);
                    Custom.Add("admin.lr", exampleCustomItem);
                }
                Resave();
            }

            void Resave()
            {
                foreach (var item in Default)
                    DefaultFs.WriteObject(item.Key, item.Value);
                foreach (var item in Custom)
                    CustomFs.WriteObject(item.Key, item.Value);
                foreach (var item in Permissions)
                {
                    DataFileSystem fs = null;
                    if (!PermissionsFss.TryGetValue(item.Key, out fs)) continue;
                    foreach (var wd in item.Value)
                        fs.WriteObject(wd.Key, wd.Value);

                }
            }
        }

#endregion

#region API
        private bool API_ExistCustom(string name)
        {
            if (name == null || name == string.Empty) return false;
            return config.DataItems.Custom.ContainsKey(name);
        }
#endregion

#region 0xF UI Library
        public class CUI
        {
            public CuiElementContainer ElementContainer { get; set; } = new CuiElementContainer();

            readonly string[] FontNames = new string[] {
            "RobotoCondensed-Bold.ttf",
            "RobotoCondensed-Regular.ttf",
            "DroidSansMono.ttf",
            "PermanentMarker.ttf"
        };

            public enum Font
            {
                RobotoCondensedBold,
                RobotoCondensedRegular,
                DroidSansMono,
                PermanentMarker,
                PressStart2PRegular
            }
            public string AddText(
               string text = "Text",
               string color = "1 1 1 1",
               Font font = Font.RobotoCondensedRegular,
               int fontSize = 14,
               TextAnchor align = TextAnchor.UpperLeft,
               VerticalWrapMode overflow = VerticalWrapMode.Overflow,
               string anchorMin = "0 0",
               string anchorMax = "1 1",
               string offsetMin = "0 0",
               string offsetMax = "0 0",
               string parent = "Hud",
               string name = null)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiElement element = new CuiElement
                {
                    Components =
                    {
                         new CuiTextComponent
                         {
                             Text = text,
                             Color = color,
                             Font = FontNames[(int)font],
                             VerticalOverflow = overflow,
                             FontSize = fontSize,
                             Align = align
                         },
                         new  CuiRectTransformComponent()
                         {
                              AnchorMin = anchorMin,
                              AnchorMax = anchorMax,
                              OffsetMin = offsetMin,
                             OffsetMax =  offsetMax
                         },
                    },
                    Parent = parent,
                    Name = name
                };
                ElementContainer.Add(element);
                return name;
            }
            public string AddPanel(
               string color = "0 0 0 0",
               string sprite = "assets/content/ui/ui.background.tile.psd",
               string material = "assets/icons/iconmaterial.mat",
               UnityEngine.UI.Image.Type imageType = UnityEngine.UI.Image.Type.Simple,
               string anchorMin = "0 0",
               string anchorMax = "1 1",
               string offsetMin = "0 0",
               string offsetMax = "0 0",
               string parent = "Hud",
               string name = null,
               bool cursorEnabled = false)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiPanel panel = new CuiPanel
                {
                    Image =
                {
                    Color = color,
                    Sprite = sprite,
                    Material = material,
                    ImageType = imageType
                },
                    RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax,
                },
                    CursorEnabled = cursorEnabled
                };
                ElementContainer.Add(panel, parent, name);
                return name;
            }
            public string AddButton(
                string command,
                string color = "0 0 0 0",
                string sprite = "assets/content/ui/ui.background.tile.psd",
                string material = "assets/icons/iconmaterial.mat",
                UnityEngine.UI.Image.Type imageType = UnityEngine.UI.Image.Type.Simple,
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0",
                string parent = "Hud",
                 string name = null)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiButton button = new CuiButton
                {
                    Button =
                {
                    Close = "",
                    Command = command,
                    Color = color,
                    Sprite = sprite,
                    Material = material,
                    ImageType = imageType
                },
                    RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax,
                }
                };
                ElementContainer.Add(button, parent, name);
                return name;
            }
            public string AddImage(string content,
                string color = "1 1 1 1",
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0",
                string parent = "Hud",
                 string name = null)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiElement element = new CuiElement
                {
                    Components =
                    {
                         new CuiRawImageComponent()
                         {
                             Color = color,
                             Png = content,
                             Sprite = "assets/content/textures/generic/fulltransparent.tga"
                         },
                         new  CuiRectTransformComponent()
                         {
                              AnchorMin = anchorMin,
                              AnchorMax = anchorMax,
                              OffsetMin = offsetMin,
                              OffsetMax =  offsetMax
                         },
                    },
                    Parent = parent,
                    Name = name
                };
                ElementContainer.Add(element);
                return name;
            }
            public string AddIcon(
                int itemId,
                ulong skin = 0,
                string color = "1 1 1 1",
                string sprite = "assets/content/ui/ui.background.tile.psd",
                string material = "assets/icons/iconmaterial.mat",
                UnityEngine.UI.Image.Type imageType = UnityEngine.UI.Image.Type.Simple,
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0",
                string parent = "Hud",
                 string name = null)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiElement element = new CuiElement
                {
                    Components =
                    {
                         new CuiImageComponent()
                         {
                             Color = color,
                             ItemId = itemId,
                             SkinId = skin,
                             Sprite = sprite,
                             Material = material,
                             ImageType = imageType,

                         },
                         new  CuiRectTransformComponent()
                         {
                              AnchorMin = anchorMin,
                              AnchorMax = anchorMax,
                              OffsetMin = offsetMin,
                              OffsetMax =  offsetMax
                         },
                    },
                    Parent = parent,
                    Name = name
                };
                ElementContainer.Add(element);
                return name;
            }
            public string AddColorPanel(
                string color = "0 0 0 0",
                string sprite = "assets/content/ui/ui.background.tile.psd",
                string material = "assets/icons/iconmaterial.mat",
                UnityEngine.UI.Image.Type imageType = UnityEngine.UI.Image.Type.Simple,
                string outlineColor = "0 0 0 0",
                float outlineWidth = 2,
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0",
                string parent = "Hud",
                 string name = null)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiElement element = new CuiElement
                {
                    Components =
                    {
                         new CuiImageComponent
                         {
                             Color = color,
                             Sprite = sprite,
                             Material = material,
                             ImageType = imageType,

                         },
                         new CuiOutlineComponent
                         {
                             Color = outlineColor,
                             Distance = $"{outlineWidth} {outlineWidth}"
                         },
                         new  CuiRectTransformComponent
                         {
                              AnchorMin = anchorMin,
                              AnchorMax = anchorMax,
                              OffsetMin = offsetMin,
                              OffsetMax =  offsetMax
                         },
                    },
                    Parent = parent,
                    Name = name
                };
                ElementContainer.Add(element);
                return name;
            }
            public string AddContainer(
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0",
                string parent = "Hud",
                string name = null)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiElement element = new CuiElement
                {
                    Components =
                    {
                         new  CuiRectTransformComponent()
                         {
                              AnchorMin = anchorMin,
                              AnchorMax = anchorMax,
                              OffsetMin = offsetMin,
                              OffsetMax =  offsetMax
                         },
                    },
                    Parent = parent,
                    Name = name
                };
                ElementContainer.Add(element);
                return name;
            }

            public void Render(BasePlayer player) => CuiHelper.AddUi(player, ElementContainer);
            public void RenderWithDestroy(BasePlayer player)
            {
                if (ElementContainer.Count > 0)
                {
                    var element = ElementContainer.ElementAt(0);
                    if (element != null && element.Name != null && element.Name != string.Empty)
                        element.DestroyUi = element.Name;
                }

                Render(player);
            }
        }
#endregion
    }
}
