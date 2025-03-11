#if CARBON
using HarmonyLib;
#else
#endif
using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Rust;
using static Oxide.Plugins.CustomItemDefinitions;

namespace Oxide.Plugins
{
    [Info("Customizable Protection", "0xF [dsc.gg/0xf-plugins]", "2.1.8")]
    public class CustomizableProtection : RustPlugin
    {
        #region Consts
        public const ItemContainer.Flag NoSortify = (ItemContainer.Flag)(8192) | (ItemContainer.Flag)(16384);
        private const ItemDefinition.Flag CUSTOMATTIRE_FLAG = (ItemDefinition.Flag)(65536);
        #endregion

        #region Variables
        public static CustomizableProtection PluginInstance;
        #endregion

        #region PluginReference
        [PluginReference] private Plugin RustTranslationAPI;
        #endregion

        #region Permissions
        const string giveCommandPerm = "customizableprotection.give";
        const string iconShowPerm = "customizableprotection.icon.show";
        const string iconHidePerm = "customizableprotection.icon.hide";
        #endregion

        #region Harmony

        public static class Harmony
        {
            private static string Name;
#if CARBON
            private static HarmonyLib.Harmony Instance;
#else
            private static HarmonyLib.Harmony Instance;
#endif
            public static void Init(string name)
            {
                Name = name;
#if CARBON
                Instance = new HarmonyLib.Harmony(name);
#else
                Instance = new HarmonyLib.Harmony(name);
#endif
            }

            public static void Patch()
            {
                Instance.Patch(AccessTools.Method(typeof(BasePlayer), "UpdateProtectionFromClothing"), new HarmonyMethod(typeof(Patches), "UpdateProtectionFromClothing"));
                Instance.Patch(AccessTools.Method(typeof(Item), "Save"), postfix: new HarmonyMethod(typeof(Patches), "Item_Save"));
            }
            public static void UnpatchAll()
            {
                Instance.UnpatchAll(Name);
            }
        }

        private static class Patches
        {
            internal static bool UpdateProtectionFromClothing(BasePlayer __instance)
            {
                PluginInstance.UpdatePlayerProtection(__instance);
                return false;
            }
           
            internal static void Item_Save(Item __instance, ref ProtoBuf.Item __result)
            {
                if (!SaveRestore.IsSaving &&
                    __instance.info.category == ItemCategory.Attire &&
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
        #endregion

        #region Hooks

        private void Init()
        {
            PluginInstance = this;

            DamageTypes = new Dictionary<Rust.DamageType, KeyValuePair<string, int>>();
            var names = Enum.GetNames(typeof(Rust.DamageType));
            for (int i = 0; i < names.Length; i++)
            {
                Rust.DamageType damageType = (Rust.DamageType)i;
                if (!DamageTypeExceptions.Contains(damageType))
                    DamageTypes.Add(damageType, new KeyValuePair<string, int>(names[i], i));
            }
            DamageTypesDictionary = DamageTypes.Values.ToDictionary(x => x.Key, x => x.Value);
            DamageTypesDictionary.Add("OxygenExposure", -1); ;

            permission.RegisterPermission(giveCommandPerm, this);
            permission.RegisterPermission(iconShowPerm, this);
            permission.RegisterPermission(iconHidePerm, this);

            LoadConfig();
            config.DataItems.Initialize();
            RegisterCustomItemDefinitions();

            foreach (var pair in DamageTypesDictionary)
            {
                if (!config.ProtectionMultipliers.Types.ContainsKey(pair.Key))
                {
                    config.ProtectionMultipliers.Types.Add(pair.Key, 1.0f);
                }
                if (!config.Default.ContainsKey(pair.Key))
                {
                    config.Default.Add(pair.Key, 0.0f);
                }
            }
            SaveConfig();
        }


        void Loaded()
        {
            Harmony.Init(this.Name);
            Harmony.Patch();
        }
       
        private void OnServerInitialized()
        {
            LoadIcon();

            BasePlayer.activePlayerList.ToList().ForEach(player => AddUiIcon(player));

            foreach (var player in BasePlayer.activePlayerList)
                UpdatePlayerProtection(player);
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
        private void OnPlayerSleepEnded(BasePlayer player) => UpdatePlayerProtection(player);
       
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            BasePlayer player = entity.ToPlayer();
            if (player == null) return;
            if (player.baseProtection == null) return;

            if (info.UseProtection)
            {
                HitArea boneArea = info.boneArea;
                if (boneArea == (HitArea)(-1) && info.HitPositionWorld != null && info.Initiator != null)
                {
                    float minDistance = float.MaxValue;
                    for (int i = 0; i < player.skeletonProperties.bones.Length; i++)
                    {
                        var bone = player.skeletonProperties.bones[i];
                        var distance = Vector3.Distance(info.HitPositionWorld, player.transform.TransformPoint(bone.bone.transform.position));
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            boneArea = bone.area;
                        }
                    }
                }

                float[] amounts = CalculateProtection(player, boneArea);
                if (amounts.Max() > 0)
                {
                    for (int i = 0; i < amounts.Length; i++)
                    {
                        if (amounts[i] != 0f)
                        {
                            info.damageTypes.Scale((Rust.DamageType)i, 1f - amounts[i]);
                        }
                    }
                    info.UseProtection = false;
                }
                
            }


        }


        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity ownerEntity, float delta)
        {
            BasePlayer player = ownerEntity as BasePlayer;
            if (!player) return;
            if (player.AirFactor() == 1f) return;

            float oe = CalculateOxygenExposure(player);
            if (oe > 0)
                metabolism.oxygen.MoveTowards(0f, -(delta * 0.1f * Mathf.Clamp(oe, 0, 1)));
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!item.info.isWearable) return;
            if (!container.HasFlag(ItemContainer.Flag.IsPlayer)) return;
            if (container.playerOwner?.inventory?.loot?.IsLooting() == true) return;
            NextTick(() =>
            {
                ItemDefinition itemDef = item.info;
                if (itemDef == null)
                    return;

                CustomItem customItem;
                if (itemDef.HasFlag(CUSTOMATTIRE_FLAG) && TryGetCustomItem(itemDef, out customItem))
                {
                    var nameLangKey = $"{itemDef.shortname}:Name";
                    var itemName = GetMessage(nameLangKey, container.playerOwner?.UserIDString);
                    if (itemName == nameLangKey)
                        itemName = customItem.Name;


                    SetName(item, customItem.Protection, itemName);
                    if (customItem.SkinId != 0)
                        item.skin = customItem.SkinId;
                    item.MarkDirty();
                }
                else
                {
                    Dictionary<string, float> data;
                    if (TryGetDefaultProtection( item, out data))
                    {
                        SetName(item, data);
                        item.MarkDirty();
                    }
                }
            });
        }


        private void OnPlayerLootEnd(PlayerLoot playerLoot)
        {
            var player = (BasePlayer)playerLoot.gameObject.ToBaseEntity();
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

        private void OnPlayerRespawned(BasePlayer player) => AddUiIcon(player);
        private void OnPlayerConnected(BasePlayer player) => AddUiIcon(player);

        #endregion

        #region Commands
        [ConsoleCommand("cp.reload")]
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
        [ConsoleCommand("cp.create")]
        private void ConsoleCommandAdd(ConsoleSystem.Arg arg)
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
                    SendReply(arg, "Usage: cp.create <name> <shortname>");
                    return;
                }
                PrintWarning("Usage: cp.create <name> <shortname>");
                return;
            }
            var name = arg.Args[0];
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
                    SendReply(arg, $"No protection options for shortname '{shortname}', try another one, you can find available shortnames in the 'Default' folder");
                    return;
                }
                PrintWarning($"No protection options for shortname '{shortname}', try another one, you can find available shortnames in the 'Default' folder");
                return;
            }
            CustomItem newItem = new CustomItem()
            {
                Shortname = shortname,
                SkinId = (ulong)new System.Random().Next(1000, 999999),
                Name = name,
                Description = "",
                Protection = config.DataItems.Default[shortname]
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
        [ConsoleCommand("cp.give")]
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
                    arg.ReplyWith("Usage: cp.give <steamid/nickname> <custom item name>");
                    return;
                }
                PrintWarning("Usage: cp.give <steamid/nickname> <custom item name>");
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
                if (!playerToGive.inventory.GiveItem(itemToGive))
                {
                    itemToGive.Remove(0f);
                    arg.ReplyWith("Couldn't give item (inventory full?)");
                    PrintWarning($"{nickname}[{(player == null ? "0" : player.UserIDString)}] failed to give a custom item [{uniqueShortname}] to player {playerToGive.displayName}[{(player == null ? "0" : playerToGive.UserIDString)}]!");
                    return;
                }
                SetName(itemToGive, customItem.Protection, itemName);
                itemToGive.skin = customItem.SkinId;
                itemToGive.MarkDirty();
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
        public class CustomItem
        {
            public class UISettings
            {
                [JsonProperty(PropertyName = "Name Color (or use <color></color> in name)")]
                public string NameColor { get; set; } = "1 1 1 1";
                [JsonProperty(PropertyName = "Frame Color")]
                public string FrameColor { get; set; } = "0.94 0.75 0.15 1";
            }

            [JsonProperty(PropertyName = "Shortname")]
            public string Shortname { get; set; } = "";
            [JsonProperty(PropertyName = "SkinId")]
            public ulong SkinId { get; set; } = 0;
            [JsonProperty(PropertyName = "SkinId to recognize (MUST BE UNIQUE)")]
            public ulong RecognizeSkinId { get; set; } = (ulong)UnityEngine.Random.Range(1000, 10000000);
            [JsonProperty(PropertyName = "Name")]
            public string Name { get; set; } = "";
            [JsonProperty(PropertyName = "Description")]
            public string Description { get; set; } = "";
            [JsonProperty(PropertyName = "UI Settings")]
            public UISettings UiSettings { get; set; } = new UISettings();
            [JsonProperty(PropertyName = "Protection")]
            public Dictionary<string, float> Protection { get; set; } = new Dictionary<string, float>();
        }
        #endregion

        #region Fields
        private static List<Rust.DamageType> DamageTypeExceptions = new List<Rust.DamageType>
        {
            Rust.DamageType.Generic,
            Rust.DamageType.LAST,
            Rust.DamageType.Decay,
            Rust.DamageType.AntiVehicle,
            Rust.DamageType.Collision,
            Rust.DamageType.Fun_Water
        };
        private static Dictionary<Rust.DamageType, KeyValuePair<string, int>> DamageTypes = new Dictionary<Rust.DamageType, KeyValuePair<string, int>>();
        private static Dictionary<string, int> DamageTypesDictionary;
        #endregion

        #region Methods
void RegisterCustomItemDefinitions()
{
    foreach (var item in config.DataItems.Custom.Values)
    {
        ItemDefinition parentDefinition = ItemManager.FindItemDefinition(item.Shortname);
        if (parentDefinition == null)
            continue;

        CustomItemDefinitions.RegisterPluginItemDefinition(new CustomItemDefinition
        {
            shortname = item.Shortname,
            parentItemId = parentDefinition.itemid,
            maxStackSize = 1,
            flags = CUSTOMATTIRE_FLAG,
            defaultName = item.Name,
            defaultDescription = item.Description,
            defaultSkinId = item.SkinId,
            itemMods = parentDefinition.itemMods
        }, this);
    }
}
        private void SetName(Item item, Dictionary<string, float> data, string itemName = null)
        {
            if (item == null || data == null) return;
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


            float[] protectionAmounts = GetProtectionForItem(item);
            Dictionary<string, float> FieldsForRender = DamageTypes.Values.ToDictionary(x => GetMessage(x.Key, owner?.UserIDString), x => (float)Math.Round(protectionAmounts[x.Value] * 100, 2));
            FieldsForRender.Add(GetMessage(LangKeys.OxygenExposure, owner?.UserIDString), GetOxygenExposureForItem(item) * 100);
            FieldsForRender = FieldsForRender.Where(i => i.Value != 0f).ToDictionary(i => i.Key, i => i.Value);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(string.Concat(Enumerable.Repeat("\n", FieldsForRender.Count)));
            stringBuilder.Append($"\n{itemName}");
            stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t" + GetMessage(LangKeys.ItemInfoPanelHeader, owner?.UserIDString));
            foreach (var pair in FieldsForRender)
            {
                string keyWithoutRichtext = Regex.Replace(pair.Key, "<.*?>", String.Empty);
                stringBuilder.Append("\n\t\t\t\t\t\t\t\t\t\t\t\t   ");
                stringBuilder.Append($" {keyWithoutRichtext}    {pair.Value}%");
            }
            item.streamerName = stringBuilder.ToString();
            item.MarkDirty();
        }

        public bool TryGetCustomItem(Item item, out CustomItem customItem)
           => TryGetCustomItem(item.info, out customItem);

        private bool TryGetCustomItem(ItemDefinition info, out CustomItem customItem)
        {
            if (!info.HasFlag(CUSTOMATTIRE_FLAG) || !TryGetCustomItemByKey(info.shortname, out customItem))
            {
                customItem = null;
                return false;
            }
            return true;
        }

        public bool HasCustomItemWithKey(string key)
        {
            return config.DataItems.Custom.ContainsKey(key);
        }
        public bool TryGetCustomItemByKey(string key, out CustomItem customItem)
        {
            return config.DataItems.Custom.TryGetValue(key, out customItem);
        }

        public bool TryGetProtection(Item item, out Dictionary<string, float> data)
        {
            return TryGetProtection(item.info, out data);
        }


        public bool TryGetProtection(ItemDefinition info, out Dictionary<string, float> data)
        {
            CustomItem customItem;
            if (TryGetCustomItem(info, out customItem))
            {
                data = customItem.Protection;
                return true;
            }
            else
                return TryGetDefaultProtection(info.shortname, out data);
        }

        public bool TryGetDefaultProtection(Item item, out Dictionary<string, float> data)
               => TryGetDefaultProtection(item.info.shortname, out data);

        public bool TryGetDefaultProtection(string shortname, out Dictionary<string, float> data)
        {
            return config.DataItems.Default.TryGetValue(shortname, out data);
        }
        public void UpdatePlayerProtection(BasePlayer player)
        {
            if (player == null) return;
            var amounts = CalculateProtection(player, (HitArea)(-1));
            player.baseProtection.amounts = amounts;
        }
        public float GetOxygenExposureForItem(Item item)
        {
            float @return = config.Default["OxygenExposure"];
            Dictionary<string, float> protection = null;
            if (TryGetProtection(item, out protection))
                @return += protection["OxygenExposure"];
            @return *= config.ProtectionMultipliers.Types["OxygenExposure"];
            @return *= config.ProtectionMultipliers.Common;
            @return *= 0.01f;
            return Mathf.Clamp(@return, 0, 1);
        }


        public float CalculateOxygenExposure(BasePlayer player)
        {
            float @return = config.Default["OxygenExposure"];
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                Dictionary<string, float> protection = null;
                if (!TryGetProtection(item, out protection))
                    continue;
                @return += protection["OxygenExposure"];
            }
            @return *= config.ProtectionMultipliers.Types["OxygenExposure"];
            @return *= config.ProtectionMultipliers.Common;
            @return *= 0.01f;
            return Mathf.Clamp(@return, 0, 1);
        }


        public float[] GetProtectionForItem(Item item)
        {
            float[] protectionAmounts = new float[(int)Rust.DamageType.LAST];
            foreach (var pair in config.Default)
            {
                if (pair.Value == 0) continue;

                var value = pair.Value * config.ProtectionMultipliers.Types[pair.Key] * config.ProtectionMultipliers.Common;
                var index = DamageTypesDictionary[pair.Key];
                if (index >= 0)
                    protectionAmounts[index] = value;
            }
            Dictionary<string, float> protection;
            if (TryGetProtection(item, out protection))
            {
                foreach (var pair in protection)
                {
                    if (pair.Value == 0) continue;

                    var index = DamageTypesDictionary[pair.Key];
                    if (index >= 0)
                        protectionAmounts[index] += pair.Value;

                }
            }
            for (int i = 0; i < protectionAmounts.Length; i++)
            {
                if (i == 22)
                {
                    protectionAmounts[i] = 1f;
                    continue;
                }
                Rust.DamageType damageType = (Rust.DamageType)i;
                if (!DamageTypes.ContainsKey(damageType)) continue;
                string DamageTypeName = DamageTypes[damageType].Key;
                protectionAmounts[i] *= config.ProtectionMultipliers.Types[DamageTypeName];
                protectionAmounts[i] *= config.ProtectionMultipliers.Common;
                protectionAmounts[i] *= 0.01f;
                protectionAmounts[i] = (config.LockNormalValues ? Mathf.Clamp(protectionAmounts[i], 0, 1) : protectionAmounts[i]);
            }
            return protectionAmounts;
        }

        public float[] CalculateProtection(BasePlayer player, HitArea hitArea)
        {
            float[] protectionAmounts = new float[(int)Rust.DamageType.LAST];
            foreach (var pair in config.Default)
            {
                if (pair.Value == 0) continue;

                var value = pair.Value * config.ProtectionMultipliers.Types[pair.Key] * config.ProtectionMultipliers.Common;
                var index = DamageTypesDictionary[pair.Key];
                if (index >= 0)
                    protectionAmounts[index] = value;
            }
            foreach (Item item in player.inventory.containerWear.itemList)
            {

                ItemDefinition itemDefinition = item.info;
                ItemModWearable itemModWearable = itemDefinition.ItemModWearable;
                if (itemModWearable != null && (hitArea == (HitArea)(-1) || itemModWearable.ProtectsArea(hitArea)))
                {
                    Dictionary<string, float> data;
                    if (!TryGetProtection(item, out data))
                        continue;

                    foreach (var pair in data)
                    {
                        if (pair.Value == 0) continue;

                        var index = DamageTypesDictionary[pair.Key];
                        if (index >= 0)
                        {
                            protectionAmounts[index] += pair.Value;
                        }
                    }
                }
            }
            var itemListCount = player.inventory.containerWear.itemList.Where(item => item.position < player.inventory.containerWear.capacity).Count();
            for (int i = 0; i < protectionAmounts.Length; i++)
            {
                if (i == 22)
                {
                    protectionAmounts[i] = 1f;
                    continue;
                }
                Rust.DamageType damageType = (Rust.DamageType)i;
                if (!DamageTypes.ContainsKey(damageType)) continue;
                string DamageTypeName = DamageTypes[damageType].Key;
                if (hitArea == (HitArea)(-1) && i != 17)
                    protectionAmounts[i] = itemListCount == 0 ? 0 : protectionAmounts[i] / itemListCount;
                protectionAmounts[i] *= config.ProtectionMultipliers.Types[DamageTypeName];
                protectionAmounts[i] *= config.ProtectionMultipliers.Common;
                protectionAmounts[i] *= 0.01f;
                protectionAmounts[i] = (config.LockNormalValues ? Mathf.Clamp(protectionAmounts[i], 0, 1) : protectionAmounts[i]);
               
            }

            return protectionAmounts;
        }
        #endregion

        #region UI

        private string IconString = null;
        private byte[] IconData = Convert.FromBase64String("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");
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
                name: "CustomizableProtection_Icon");
            cui.AddImage(
                IconString,
                anchorMin: "0 0",
                anchorMax: "1 1",
                parent: UI);
            cui.AddButton("customizableprotection.ui open", parent: UI);
            cui.RenderWithDestroy(player);
        }

        void SetupInventorySelector(BasePlayer player)
        {
            var cui = new CUI();
            string fullscreen = cui.AddPanel(
                anchorMin: "0 0",
                anchorMax: "1 1",
                parent: "Hud.Menu",
                name: "CustomizableProtection_InventorySelectorContainer");

            var wear = player.inventory.containerWear;
            for (int i = wear.capacity; i >= 0; i--)
            {
                Item item = wear.GetSlot(6 - i);
                if (item == null) continue;
                string color = "1 1 1 1";
                CustomItem customItem = null;
                if (item.info != null && item.info.HasFlag(CUSTOMATTIRE_FLAG) != null && TryGetCustomItem(item.info, out customItem))
                    color = customItem.UiSettings.FrameColor;
                cui.AddButton(
                       command: $"customizableprotection.ui view {item.uid.Value}",
                       color: color,
                       sprite: "assets/content/ui/ui.box.tga",
                       imageType: UnityEngine.UI.Image.Type.Tiled,
                       anchorMin: ".5 0",
                       anchorMax: ".5 0",
                       offsetMin: $"{-266 - 54 * i} 114",
                       offsetMax: $"{-214 - 54 * i} 166",
                       parent: fullscreen);
            }
            var belt = player.inventory.containerBelt;
            for (int i = 0; i < belt.capacity; i++)
            {
                Item item = belt.GetSlot(i);
                if (item == null || !item.info.isWearable) continue;
                string color = "1 1 1 1";
                CustomItem customItem = null;
                if (item.info != null && item.info.HasFlag(CUSTOMATTIRE_FLAG) != null && TryGetCustomItem(item.info, out customItem))
                    color = customItem.UiSettings.FrameColor;
                cui.AddButton(
                       command: $"customizableprotection.ui view {item.uid}",
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
                    if (item == null || !item.info.isWearable) continue;
                    string color = "1 1 1 1";
                    CustomItem customItem = null;
                    if (item.info != null && item.info.HasFlag(CUSTOMATTIRE_FLAG) != null && TryGetCustomItem(item.info, out customItem))
                        color = customItem.UiSettings.FrameColor;
                    cui.AddButton(
                        command: $"customizableprotection.ui view {item.uid}",
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
            if (item == null) 
                return;

            ItemDefinition definition = item.info;
            if (definition == null)
                return;


            string itemName = string.Empty;
            string itemNameColor = "1 1 1 1";
            string itemDescription = string.Empty;
            int itemId = 0;
            bool isCustomItem = false;
            CustomItem customItem;
            if (TryGetCustomItem(definition, out customItem))
            {
                var nameLangKey = $"{definition.shortname}:Name";
                var descriptionLangKey = $"{definition.shortname}:Description";
                itemName = GetMessage(nameLangKey, item.GetOwnerPlayer()?.UserIDString);
                if (itemName == nameLangKey)
                    itemName = customItem.Name;
                itemName = customItem.Name;
                itemNameColor = customItem.UiSettings.NameColor;
                itemDescription = GetMessage(descriptionLangKey, player.UserIDString);
                if (itemDescription == descriptionLangKey)
                    itemDescription = customItem.Description;
                isCustomItem = true;
            }
            ItemDefinition itemDefinition = item.info;
            Dictionary<string, string> translation = null;
            if (RustTranslationAPI != null && (itemName == string.Empty || itemDescription == string.Empty)) translation = RustTranslationAPI.Call<Dictionary<string, string>>("GetTranslations", lang.GetLanguage(player.UserIDString));
            if (itemName == string.Empty)
            {
                if (translation == null || !translation.TryGetValue($"{itemDefinition.shortname}", out itemName))
                    itemName = itemDefinition.displayName.translated;
            }

            if (itemDescription == string.Empty)
            {
                if (translation == null || !translation.TryGetValue($"{itemDefinition.shortname}.desc", out itemDescription))
                    itemDescription = itemDefinition.displayDescription.translated;
            }

            itemId = itemDefinition.itemid;

            var cui = new CUI();
            string fullscreen = cui.AddContainer(
                 anchorMin: "0 0",
                 anchorMax: "1 1",
                 parent: "Hud.Menu",
                 name: "CustomizableProtection_ItemInfoContainer");
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
            string statsPanel = cui.AddColorPanel(
               color: ".2 .2 .2 0.95",
               sprite: "assets/content/ui/ui.background.rounded.png",
               imageType: UnityEngine.UI.Image.Type.Tiled,
               anchorMin: "0.5 0",
               anchorMax: "0.5 0",
               offsetMin: "190 235",
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

            float[] protectionAmounts = GetProtectionForItem(item);
            Dictionary<string, float> FieldsForRender = DamageTypes.Values.ToDictionary(x => x.Key, x => (float)Math.Round(protectionAmounts[x.Value] * 100, 2));
            FieldsForRender.Add(GetMessage(LangKeys.OxygenExposure, player.UserIDString), GetOxygenExposureForItem(item) * 100);
            FieldsForRender = FieldsForRender.Where(i => i.Value != 0f).ToDictionary(i => i.Key, i => i.Value);
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
                    text: $"{pair.Value}%",
                    font: CUI.Font.RobotoCondensedRegular,
                    fontSize: 12,
                    align: TextAnchor.MiddleCenter,
                    anchorMin: "0 1",
                    anchorMax: "0 1",
                    offsetMin: $"215 {-10 + (-18 * i)}",
                    offsetMax: $"280 {-10 + (-18 * i + 10)}",
                    parent: statsContainer);
            }

            cui.RenderWithDestroy(player);
        }

        [ConsoleCommand("customizableprotection.ui")]
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
                    ShowItemInfo(player, Convert.ToUInt64(arg.Args[1]));
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
            CuiHelper.DestroyUi(player, "CustomizableProtection_ItemInfoContainer");
            CuiHelper.DestroyUi(player, "CustomizableProtection_GearInfoContainer");
            CuiHelper.DestroyUi(player, "CustomizableProtection_InventorySelectorContainer");
        }
        private void DestroyIcon(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CustomizableProtection_Icon");
        }
        #endregion

        #region Config

        private static ConfigData config;

        private class ConfigData
        {
            public class ProtectionMultipliersClass
            {
                [JsonProperty(PropertyName = "Common")]
                public float Common { get; set; } = 1f;
                [JsonProperty(PropertyName = "Types")]
                public Hash<string, float> Types { get; set; } = new Hash<string, float>();

            }
            [JsonProperty(PropertyName = "Displaying information about a weapon when it is selected in the inventory")]
            public bool ItemSelectInfo { get; set; } = true;
            [JsonProperty(PropertyName = "Icon Position (0 - Off | -1 - left by 1 slot, 1 - right by 1 slot | ..)")]
            public int IconPosition { get; set; } = -1;
            [JsonProperty(PropertyName = "Lock values at normal values")]
            public bool LockNormalValues { get; set; } = true;
            [JsonProperty(PropertyName = "Protection Multipliers")]
            public ProtectionMultipliersClass ProtectionMultipliers { get; set; } = new ProtectionMultipliersClass();
            [JsonProperty(PropertyName = "Default Protection")]
            public Hash<string, float> Default { get; set; } = new Hash<string, float>();

            [JsonIgnore]
            public DataItems DataItems { get; set; } = new DataItems("CustomizableProtection");

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
                    LoadDefaultConfig();
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
                {
                    Puts($"Exception: {ex}");
                    LoadDefaultConfig();
                    return;
                }
                throw;
            }
        }
        protected override void LoadDefaultConfig()
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
            public const string ItemInfoPanelHeader = nameof(ItemInfoPanelHeader);
            public const string GearInfoPanelHeader = nameof(GearInfoPanelHeader);
            public const string Hunger = nameof(Hunger);
            public const string Thirst = nameof(Thirst);
            public const string Cold = nameof(Cold);
            public const string Drowned = nameof(Drowned);
            public const string Heat = nameof(Heat);
            public const string Bleeding = nameof(Bleeding);
            public const string Poison = nameof(Poison);
            public const string Suicide = nameof(Suicide);
            public const string Bullet = nameof(Bullet);
            public const string Slash = nameof(Slash);
            public const string Blunt = nameof(Blunt);
            public const string Fall = nameof(Fall);
            public const string Radiation = nameof(Radiation);
            public const string Bite = nameof(Bite);
            public const string Stab = nameof(Stab);
            public const string Explosion = nameof(Explosion);
            public const string RadiationExposure = nameof(RadiationExposure);
            public const string ColdExposure = nameof(ColdExposure);
            public const string ElectricShock = nameof(ElectricShock);
            public const string Arrow = nameof(Arrow);
            public const string OxygenExposure = nameof(OxygenExposure);
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.ItemInfoPanelHeader] = "Information about item protection:",
                [LangKeys.GearInfoPanelHeader] = "Information about gear protection:",
                [LangKeys.Hunger] = "Reduced <color=#fcd186>hunger</color> damage",
                [LangKeys.Thirst] = "Reduced <color=#3b9ad1>thirst</color> damage",
                [LangKeys.Cold] = "Reduced <color=#60d4eb>cold</color> damage",
                [LangKeys.Drowned] = "Resistance to <color=#1e2985>humidity</color>",
                [LangKeys.Heat] = "Reduced <color=#e66d10>heat</color> damage",
                [LangKeys.Bleeding] = "Reduced <color=#b0000c>bleeding</color> damage",
                [LangKeys.Poison] = "Reduced <color=#8a15cf>poison</color> damage",
                [LangKeys.Suicide] = "Reduced <color=#cf1534>suicide</color> damage",
                [LangKeys.Bullet] = "Reduced <color=#ffca42>bullet</color> damage",
                [LangKeys.Slash] = "Reduced <color=#a1a1a1>slash</color> damage",
                [LangKeys.Blunt] = "Reduced <color=#a1a1a1>blunt</color> damage",
                [LangKeys.Fall] = "Reduced <color=#a1a1a1>fall</color> damage",
                [LangKeys.Radiation] = "Reduced <color=#6acf48>radiation</color> damage",
                [LangKeys.Bite] = "Reduced <color=#a1a1a1>bite</color> damage",
                [LangKeys.Stab] = "Reduced <color=#a1a1a1>stab</color> damage",
                [LangKeys.Explosion] = "Reduced <color=#e68750>explosion</color> damage",
                [LangKeys.RadiationExposure] = "<color=#6acf48>Radiation</color> resistance",
                [LangKeys.ColdExposure] = "<color=#60d4eb>Cold</color> resistance",
                [LangKeys.ElectricShock] = "Reduced <color=#57f7dd>electric shock</color> damage",
                [LangKeys.Arrow] = "Reduced <color=#a1a1a1>arrow</color> damage",
                [LangKeys.OxygenExposure] = "Increased underwater breathing time"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.ItemInfoPanelHeader] = "Информация о защите предмета:",
                [LangKeys.Hunger] = "Сниж. урона от <color=#fcd186>голода</color>",
                [LangKeys.Thirst] = "Сниж. урона от <color=#3b9ad1>жажды</color>",
                [LangKeys.Cold] = "Сниж. урона от <color=#60d4eb>холода</color>",
                [LangKeys.Drowned] = "Сопротивление <color=#1e2985>влажности</color>",
                [LangKeys.Heat] = "Сниж. урона от <color=#e66d10>жары</color>",
                [LangKeys.Bleeding] = "Сниж. урона от <color=#b0000c>кровотечения</color>",
                [LangKeys.Poison] = "Сниж. урона от <color=#8a15cf>яда</color>",
                [LangKeys.Suicide] = "Сниж. урона от <color=#cf1534>самоубийства</color>",
                [LangKeys.Bullet] = "Сниж. урона от <color=#ffca42>пуль</color>",
                [LangKeys.Slash] = "Сниж. урона от <color=#a1a1a1>разрубающего урона</color>",
                [LangKeys.Blunt] = "Сниж. урона от <color=#a1a1a1>тупого урона</color>",
                [LangKeys.Fall] = "Сниж. урона от <color=#a1a1a1>падения</color>",
                [LangKeys.Radiation] = "Сниж. урон от <color=#6acf48>радиации</color>",
                [LangKeys.Bite] = "Сниж. урона от <color=#a1a1a1>укусов</color>",
                [LangKeys.Stab] = "Сниж. урона от <color=#a1a1a1>режущего урона</color>",
                [LangKeys.Explosion] = "Сниж. урона от <color=#e68750>взрыва</color>",
                [LangKeys.RadiationExposure] = "Устойчивость к <color=#6acf48>радиации</color>",
                [LangKeys.ColdExposure] = "Сопротивление <color=#60d4eb>холоду</color>",
                [LangKeys.ElectricShock] = "Сниж. урона от <color=#57f7dd>электрошока</color>",
                [LangKeys.Arrow] = "Сниж. урона от <color=#a1a1a1>стрел</color>",
                [LangKeys.OxygenExposure] = "Увел. времени дыхания под водой"
            }, this, "ru");
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
            public DataFileSystem DefaultFs { get; set; }
            public Dictionary<string, Dictionary<string, float>> Default { get; set; }
            public DataItems(string folderName)
            {

                DataFileSystem = new DataFileSystem(Path.Combine(Interface.Oxide.DataDirectory, folderName));
                DefaultFs = new DataFileSystem(Path.Combine(DataFileSystem.Directory, "Default"));
                Default = new Dictionary<string, Dictionary<string, float>>();
                CustomFs = new DataFileSystem(Path.Combine(DataFileSystem.Directory, "Custom"));
                Custom = new Dictionary<string, CustomItem>();
            }
            public void Reload()
            {
                Default = new Dictionary<string, Dictionary<string, float>>();
                Custom = new Dictionary<string, CustomItem>();
                Initialize();
            }
            public void Initialize()
            {
                foreach (ItemDefinition itemDefinition in ItemManager.itemList.Where(item => item.isWearable))
                {
                    ItemModWearable itemModWearable = itemDefinition.ItemModWearable;
                    Dictionary<string, float> dictionaryProtection = DefaultFs.ReadObject<Dictionary<string, float>>(itemDefinition.shortname);
                    bool hasChanged = false;
                    foreach (var pair in CustomizableProtection.DamageTypesDictionary)
                    {
                        if (!dictionaryProtection.ContainsKey(pair.Key))
                        {
                            float protectionValue = 0f;
                            if (pair.Value > 0)
                            {
                                protectionValue = itemModWearable.protectionProperties?.amounts?[pair.Value] ?? 0f;
                                if (pair.Value == 22)
                                {
                                    protectionValue = 1f;
                                    continue;
                                }
                                if (protectionValue != 0f)
                                {
                                    protectionValue *= 100f;
                                    switch ((Rust.DamageType)pair.Value)
                                    {
                                        case DamageType.Bite:
                                        case DamageType.Explosion:
                                        case DamageType.ColdExposure:
                                            protectionValue *= 0.16666667f;
                                            break;
                                    }
                                    protectionValue = (float)Math.Round(protectionValue, 2);
                                }
                            }
                            dictionaryProtection[pair.Key] = protectionValue;
                            hasChanged = true;
                        }
                    }
                    if (hasChanged)
                        DefaultFs.WriteObject<Dictionary<string, float>>(itemDefinition.shortname, dictionaryProtection);
                    Default.Add(itemDefinition.shortname, dictionaryProtection);
                }
                if (!System.IO.Directory.Exists(CustomFs.Directory))
                {
                    var example1Protection = CustomizableProtection.DamageTypesDictionary.Keys.ToDictionary(x => x, x => 0f);
                    example1Protection["RadiationExposure"] = 100f;
                    example1Protection["ColdExposure"] = 100f;
                    example1Protection["OxygenExposure"] = 100f;
                    example1Protection["Fall"] = 100f;
                    example1Protection["Drowned"] = 100f;
                    CustomFs.WriteObject<CustomItem>("attire.example1", new CustomItem()
                    {
                        Shortname = "partyhat",
                        SkinId = 111,
                        Name = "Cap of Fortune",
                        Description = "Even though it looks silly, it can save!",
                        Protection = example1Protection
                    });
                    CustomFs.WriteObject<CustomItem>("attire.example2", new CustomItem()
                    {
                        Shortname = "hat.beenie",
                        SkinId = 860462935,
                        Name = "Total of 1%",
                        Description = "Any description",
                        Protection = CustomizableProtection.DamageTypesDictionary.Keys.ToDictionary(x => x, x => 1f)
                    });
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

                Resave();
            }
            void Resave()
            {
                foreach (var item in Default)
                    DefaultFs.WriteObject(item.Key, item.Value);
                foreach (var item in Custom)
                    CustomFs.WriteObject(item.Key, item.Value);
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