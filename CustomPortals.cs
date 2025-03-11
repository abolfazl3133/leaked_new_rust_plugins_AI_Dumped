// #define DEBUG

using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;


namespace Oxide.Plugins
{
    [Info("CustomPortals", "_senyaa", "1.3.11")]
    class CustomPortals : RustPlugin
    {
        #region Dependencies

        [PluginReference] Plugin ServerRewards, NoEscape, Clans, Economics;

        static bool? TakePoints(ulong playerID, int amount) => Instance.ServerRewards?.Call("TakePoints", playerID, amount) as bool?;
        static int? CheckPoints(ulong ID) => Instance.ServerRewards?.Call("CheckPoints", ID) as int?;

        static bool? IsRaidBlocked(string target) => Instance.NoEscape?.Call("IsRaidBlocked", target) as bool?;
        static bool? IsCombatBlocked(string target) => Instance.NoEscape?.Call("IsCombatBlocked", target) as bool?;

        static bool? IsClanMember(string playerId, string otherId) => Instance.Clans?.Call("IsClanMember", playerId, otherId) as bool?;
        static bool? IsMemberOrAlly(string playerId, string otherId) => Instance.Clans?.Call("IsMemberOrAlly", playerId, otherId) as bool?;

        static bool? Economics_Withdraw(string playerId, double amount) => Instance.Economics?.Call("Withdraw", playerId, amount) as bool?;

        #endregion

        #region Constants

        const string PORTAL_PREFAB = "assets/prefabs/missions/portal/halloweenportalexit.prefab";
        const string RADIUS_MAP_MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        const string VENDING_MAP_MARKER_PREFAB = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";

        const string SET_PORTAL_NAME_CCMD = "customportals.portalname.set";
        const string SAVE_PORTAL_NAME_CCMD = "customportals.portalname.save";
        const string CANCEL_PORTAL_NAME_CCMD = "customportals.portalname.cancel";
        const string TOGGLE_PORTAL_BROADCAST_CCMD = "customportals.togglebroadcast";

        const string SET_PORTAL_PERMISSION_CCMD = "customportals.permission.set";
        const string SAVE_PORTAL_PERMISSION_CCMD = "customportals.permission.save";

        const string RENAMING_CUI_NAME = "CustomPortals_RenameUI";
        const string DEBUG_CUI_NAME = "CustomPortals_6ebugUI";
        const string HP_BAR_CUI_NAME = "CustomPortals_HPBar";
        const string PICKUP_CUI_NAME = "CustomPortals_PickupUI";

        const int HAMMER_ITEMID = 200773292;

        const string ADMIN_PERMISSION = "customportals.admin";
        const string PORTAL_BUY_PERMISSION = "customportals.buy";
        const string PORTAL_FREE_PERMISSION = "customportals.free";
        const string PORTAL_USE_PERMISSION = "customportals.use";
        const string PORTAL_FREE_ENTRY_PERMISSION = "customportals.freeuse";
        const string PORTAL_LIMIT_BYPASS_PERMISSION = "customportals.bypasslimit";
        const string PORTAL_BYPASS_LINKING_LIMITATIONS = "customportals.bypasslinkinglimits";
        const string PORTAL_SPAWN_PERMISSION = "customportals.spawn";

        const string PERMISSION_GROUP_PREFIX = "customportals.group.";

        const string PLAYER_DATA_FILENAME = "CustomPortals_PlayerGeneratedData";
        const string MONUMENT_DATA_FILENAME = "CustomPortals_MonumentData";
        const string CACHED_MAP_DATA_FILENAME = "CustomPortals_CachedCustomMapData";

        static Vector3 PORTAL_PLACEMENT_OFFSET = new Vector3(0, 0.35f, 0);
        static LayerMask PLAYER_LAYER_MASK = LayerMask.GetMask("Player (Server)");
        static LayerMask OBSTRUCTION_MASK = LayerMask.GetMask(new[] { "Construction", "Deployed", "World", "Terrain" });

        #endregion

        #region Configuration

        class Configuration : SerializableConfiguration
        {
            [JsonProperty("(0.1) Portal Price (set value to 0 to make it free, use ServerRewards or Economics as a shortname to use RP points or Economics balance respectively)")]
            public Price PortalPrice = new Price
            {
                ShortName = "scrap",
                Amount = 150,
                SkinID = 0
            };
            //            public KeyValuePair<string, int> PortalPrice = new KeyValuePair<string, int>("scrap", 150);

            [JsonProperty("(0.2) Entry price (set value to 0 to make it free, use ServerRewards or Economics as a shortname to use RP points or Economics balance respectively)")]
            public Price PricePerEntry = new Price
            {
                ShortName = "scrap",
                Amount = 0,
                SkinID = 0
            };
            //            public KeyValuePair<string, int> PricePerEntry = new KeyValuePair<string, int>("scrap", 0);

            [JsonProperty("(0.3) How much HP is reduced when the portal is picked up (0-100)")]
            public float HP_Reduction = 0f;

            [JsonProperty("(0.4.0) Breakable portals")]
            public bool Breakable_Portals = false;

            [JsonProperty("(0.4.1) Total portal HP")]
            public int PortalHP = 250;

            [JsonProperty("(0.4.2) Portal Damage table (projectiles)")]
            public Dictionary<string, int> Portal_Damage_Table_Projectiles = new Dictionary<string, int>()
            {
                { "riflebullet_explosive", 4 },
                { "riflebullet", 0 },
                { "pistolbullet", 0 },
            };

            [JsonProperty("(0.4.3) Portal Damage table (explosives)")]
            public Dictionary<string, int> Portal_Damage_Table_Explosives = new Dictionary<string, int>()
            {
                { "explosive.timed.deployed", 250 },
                { "explosive.satchel.deployed", 70 },
                { "rocket_basic", 220 },
                { "40mm_grenade_he", 28 }
            };

            [JsonProperty("(1) Portal Item Name")] public string PortalItemName = "Portal";

            [JsonProperty("(2) Portal Item ID")] public int PortalItemID = 198438816;

            [JsonProperty("(3) Portal Skin ID")] public ulong PortalSkinID = 2878854808;

            [JsonProperty("(4) Amount of portals /buyportal command gives")]
            public int AmountPerBuy = 1;

            [JsonProperty("(5) Display portal name")]
            public bool Display_Portal_Name = false;

            [JsonProperty("(6.1) Allow placing portals ONLY when player has building privilege")]
            public bool Allow_Placement_Only_In_Building_Privilege = false;

            [JsonProperty("(6.2) Allow picking up the portal only in building privilege")]
            public bool Allow_Pickup_Only_In_Building_Privilege = false;

            [JsonProperty("(6.2.1) Pickup time (seconds)")]
            public float Pickup_Time = 1.0f;

            [JsonProperty("(6.3) Allow setting up the portal only in building privilege")]
            public bool Allow_Setting_Up_Only_In_Building_Privilege = false;
            
            [JsonProperty("(6.4) Portal use cooldown (seconds)")]
            public float Portal_Use_Cooldown = 5f;

            [JsonProperty("(7.1) Enforce player portal limits")]
            public bool Enforce_Player_Portal_Limits = false;

            [JsonProperty("(7.2) Portal limits | Each entry in this list creates a permission (e.g. 4 -> customportals.limit.4), grant this permission to players to enforce limits.")]
            public List<int> Portal_Limits = new List<int>()
            {
                4, 8, 12, 16
            };

            [JsonProperty("(7.3) Portal group list. You can specify as many as you want, they are not the same as Oxide groups.")]
            public string[] Groups = new string[]
            {
                "vip"
            };

            [JsonProperty("(7.4) Only allow players that are in the same group to use each other's portals")]
            public bool Allow_Only_In_The_Same_Group = false;

            [JsonProperty("(8.1) Allow players to enable broadcast on their portals (show portals on map)")]
            public bool Allow_Players_To_Enable_Broadcast = true;

            [JsonProperty("(8.2) Show monument portals on map")]
            public bool Show_Monument_Portals_On_Map = false;

            [JsonProperty("(9) Allow to link portals from 0 - everybody; 1 - only yourself; 2 - only yourself and teammates")]
            public int AllowLinkingFrom = 0;

            [JsonProperty("[NoEscape] Block portal if the player is combat blocked")]
            public bool Disable_If_Combat_Blocked = false;

            [JsonProperty("[NoEscape] Block portal if the player is raid blocked")]
            public bool Disable_If_Raid_Blocked = false;

            [JsonProperty("[Clans] Restrict portal usage only to clan members")]
            public bool Restrict_To_Clans_Members = false;

            [JsonProperty("[Clans] Restrict portal usage only to clan members and allies")]
            public bool Restrict_To_Clans_Members_And_Allies = false;
        }

        #endregion

        #region Configuration Boilerplate

        static Configuration config;

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                            .ToDictionary(prop => prop.Name,
                                prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool ValidateConfig(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;
            var oldKeys = new List<string>();

            foreach (var key in currentRaw.Keys)
            {
                if (currentWithDefaults.Keys.Contains(key))
                {
                    continue;
                }

                changed = true;
                oldKeys.Add(key);
            }

            foreach (var key in oldKeys)
            {
                currentRaw.Remove(key);
            }

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                            continue;
                        }

                        if (ValidateConfig(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
                    }

                    continue;
                }

                currentRaw[key] = currentWithDefaults[key];
                changed = true;
            }


            return changed;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                var currentWithDefaults = config.ToDictionary();
                var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);

                if (ValidateConfig(currentWithDefaults, currentRaw))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["portalGiven"] = "Portal has been given to you",
                ["alreadyLinked"] = "Portals with this name are already linked",
                ["noPerms"] = "You don't have permission to use this command",
                ["notEnoughPoints"] = "You don't have enough points to buy the portal",
                ["notEnoughResources"] = "You don't have enough {0} to buy the portal",
                ["uiInfoText"] = "Portals with the same name will link with each other",
                ["uiHeader"] = "RENAME PORTAL",
                ["changeBtnText"] = "CHANGE NAME",
                ["cancelBtnText"] = "CANCEL",
                ["noMonument"] = "You are not on a monument",
                ["adminCommandUsage"] = "<size=15>Usage:</size>\n/customportals add <portal name> - adds portal to the monument\n/customportals remove - removes portal from the monument\n/customportals reset - removes all portals from the monument",
                ["lookAtSurface"] = "Look at the surface you want to spawn on",
                ["addedToMonument"] = "Portal with the name \"{0}\" has been added to {1}",
                ["removedFromMonument"] = "Removed from {0}",
                ["lookAtPortal"] = "Make sure you are looking directly at the portal",
                ["allRemoved"] = "Wiped all data for {0}",
                ["cannotUse"] = "You can't use this portal",
                ["setPerm"] = "SET PERMISSION",
                ["uiAdminInfoText"] = "Set permission for this portal (admin only)",
                ["combatBlocked"] = "Can't use portals when combat blocked",
                ["raidBlocked"] = "Can't use portals when raid blocked",
                ["notInClan"] = "You are not in the same clan as the owner of the portal",
                ["notInClanOrAlliedClan"] = "You are not in the same or allied clan as the owner of the portal",
                ["notEnoughPointsToEnter"] = "You don't have enough points to use this portal",
                ["notEnoughResourcesToEnter"] = "You don't have enough {0} to use this portal",
                ["canPlaceOnlyInBuildingPrivilege"] = "You need to have building privilege to place portals",
                ["canSetupOnlyInBuildingPrivilege"] = "You need to have building privilege to setup this portal",
                ["limitExceeded"] = "You can't place more portals. You've exceeded your limit of {0} portals",
                ["broadcastOn"] = "ENABLE BROADCAST",
                ["broadcastOff"] = "DISABLE BROADCAST",
                ["cantLinkToOthersPortal"] = "Can't link to that portal",
                ["notInSameGroup"] = "You need to be in the same group as the portal owner in order to use it",
                ["cooldown"] = "You are on cooldown!"
            }, this, "en");


            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["portalGiven"] = "Вам был выдан портал",
                ["alreadyLinked"] = "Порталы с таким названием уже соединены",
                ["noPerms"] = "У вас нет доступа к этой команде",
                ["notEnoughPoints"] = "Вам не хватает очков для покупки портала",
                ["notEnoughResources"] = "Вам не хватает {0} для покупки портала",
                ["uiInfoText"] = "Порталы с одинаковым названием будут соединены",
                ["uiHeader"] = "ПЕРЕИМЕНОВАТЬ",
                ["changeBtnText"] = "ПЕРЕИМЕНОВАТЬ",
                ["cancelBtnText"] = "ОТМЕНА",
                ["noMonument"] = "Вы не на РТ",
                ["adminCommandUsage"] = "<size=15>Использование:</size>\n/customportals add <название портала> - добавить портал на РТ\n/customportals remove - удалить портал с РТ\n/customportals reset - удалить все порталы с РТ",
                ["lookAtSurface"] = "Смотрите на поверхность на которой хотите заспаунить портал",
                ["addedToMonument"] = "Портал с названием \"{0}\" был добавлен на {1}",
                ["removedFromMonument"] = "Удалён с {0}",
                ["lookAtPortal"] = "Убедитесь, что смотрите на портал",
                ["allRemoved"] = "Вся информация {0} очищена!",
                ["cannotUse"] = "Вы не можете использовать этот портал",
                ["setPerm"] = "SET PERMISSION",
                ["uiAdminInfoText"] = "Настройте разрешения именно для этого портала",
                ["combatBlocked"] = "Нельзя использовать порталы в комбат-блоке",
                ["raidBlocked"] = "Нельзя использовать порталы в рейд-блоке",
                ["notInClan"] = "Вы не являетесь членом клана владельца портала",
                ["notInClanOrAlliedClan"] = "Вы не являетесь членом или союзником клана владельца портала",
                ["notEnoughPointsToEnter"] = "У вас не хватает валюты для использования этого портала",
                ["notEnoughResourcesToEnter"] = "Вам не хватает {0} для использования этого портала",
                ["canPlaceOnlyInBuildingPrivilege"] = "Ставить порталы можно только когда есть привилегия строительства",
                ["canSetupOnlyInBuildingPrivilege"] = "У вас должна быть привилегия строительства чтобы настроить этот портал",
                ["limitExceeded"] = "Вы больше не можете ставить порталы. Ваш лимит - {0}",
                ["broadcastOn"] = "ПОКАЗЫВАТЬ МЕСТОПОЛОЖЕНИЕ",
                ["broadcastOff"] = "СКРЫВАТЬ МЕСТОПОЛОЖЕНИЕ",
                ["cantLinkToOthersPortal"] = "Нельзя привязать к этому порталу",
                ["notInSameGroup"] = "Вам нужно быть в одной и той же группе, что и владелец портала, чтобы его использовать",
                ["cooldown"] = "Вы пока не можете воспользоваться порталом, немного подождите!"
            }, this, "ru");
        }

        static string GetText(string textName, BasePlayer player) => Instance.lang.GetMessage(textName, Instance, player?.UserIDString);

        #endregion

        #region Fields

        public static CustomPortals Instance;
        static Dictionary<BasePlayer, string> PortalNameBuffer;
        static Dictionary<BasePlayer, string> PortalPermissionBuffer;
        static Dictionary<BasePlayer, float> LastPortalUseTime;
        static Dictionary<BasePortal, CustomPortalComponent> CustomPortalsDict;
        static Dictionary<BasePlayer, CustomPortalComponent> PlayersCurrentlyRenamingPortals;
        static Dictionary<MonumentInfo, List<CustomPortalComponent>> MonumentPortals;
        static Dictionary<string, List<PositionData>> CustomMapPortals;
        static List<BaseNetworkable> ToRemove;
        static List<BasePlayer> PlayersWithHealthBarShowed;
        static DataFileSystem dataFiles;
        #endregion

        #region Types

        public struct Price
        {
            public string ShortName;
            public int Amount;
            public ulong SkinID;

            public Price(string ShortName, int Amount, ulong SkinID)
            {
                this.ShortName = ShortName;
                this.Amount = Amount;
                this.SkinID = SkinID;
            }

            public bool TryPaying(BasePlayer player, string bypass_permission, string noPointsText, string noResourcesText)
            {
                var normalizedName = ShortName.ToLower();

                if ((normalizedName == "serverrewards" || normalizedName == "economics") && !CheckDependencyAndPrintWarning(normalizedName, player))
                {
                    return false;
                }


                if (!Instance.permission.UserHasPermission(player.UserIDString, bypass_permission) && Amount > 0)
                {
                    switch (normalizedName)
                    {
                        case "serverrewards":
                            var points = CheckPoints(player.userID);

                            if (points == null || points < Amount)
                            {
                                Instance.PrintToChat(player, noPointsText);
                                return false;
                            }

                            TakePoints(player.userID, Amount);
                            break;
                        case "economics":
                            if (Economics_Withdraw(player.UserIDString, Amount) == false)
                            {
                                Instance.PrintToChat(player, noPointsText);
                                return false;
                            }

                            break;
                        default:
                            var itemDef = ItemManager.FindItemDefinition(ShortName);

                            if (SkinID != 0)
                            {
                                var playerItems = Pool.GetList<Item>();
                                var itemsToCollect = Pool.GetList<Item>();

                                player.inventory.AllItemsNoAlloc(ref playerItems);

                                foreach (var item in playerItems)
                                {
                                    if (item.skin == SkinID && item.info.shortname == ShortName && item.amount > 0)
                                    {
                                        itemsToCollect.Add(item);
                                    }
                                }

                                if (itemsToCollect.Count < Amount)
                                {
                                    Instance.PrintToChat(player, string.Format(noResourcesText, itemDef.displayName.english.ToLower()));
                                    return false;
                                }

                                foreach (var item in itemsToCollect)
                                {
                                    item.RemoveFromContainer();
                                }

                                Pool.FreeList(ref itemsToCollect);
                                Pool.FreeList(ref playerItems);

                                break;
                            }

                            if (player.inventory.GetAmount(itemDef.itemid) < Amount)
                            {
                                Instance.PrintToChat(player, string.Format(noResourcesText, itemDef.displayName.english.ToLower()));
                                return false;
                            }

                            player.inventory.Take(null, itemDef.itemid, Amount);

                            break;
                    }
                }

                return true;
            }
        }


        public class CustomPortalComponent : FacepunchBehaviour
        {
            class TryPickupPortalComponent : FacepunchBehaviour
            {
                CustomPortalComponent portal;
                BasePlayer _player;
                const float interval = 0.05f;
                float passed = interval;

                void Awake()
                {
                    _player = GetComponent<BasePlayer>();
                }

                public void SetPortal(CustomPortalComponent portal)
                {
                    this.portal = portal;
                    InvokeRepeating(PickupTick, 0, interval);
                }

                public void Cancel()
                {
                    CancelInvoke(PickupTick);
                    CuiHelper.DestroyUi(_player, PICKUP_CUI_NAME);
                    DestroyImmediate(this);
                }

                void PickupTick()
                {
                    if (_player == null || !_player.IsConnected || portal == null || portal?.GetEntity() == null)
                    {
                        Cancel();
                        return;
                    }

                    var activeItem = _player.GetActiveItem();
                    if (activeItem == null || activeItem.info.itemid != HAMMER_ITEMID)
                    {
                        Cancel();
                        return;
                    }

                    RaycastHit hit;
                    if (Physics.Raycast(_player.eyes.position, _player.eyes.HeadForward(), out hit, 5f))
                    {
                        if (hit.GetEntity() != portal.GetEntity())
                        {
                            Cancel();
                            return;
                        }

                        passed += interval;
                        ShowPickupProgess(_player, passed / config.Pickup_Time);
                        if (passed >= config.Pickup_Time)
                        {
                            _player.GiveItem(CreateItem(1, portal.ItemCondition - config.HP_Reduction), BaseEntity.GiveItemReason.PickedUp);
                            portal.GetEntity().AdminKill();
                        }

                        return;
                    }

                    Cancel();
                }
            }

            public static CustomPortalComponent Spawn(Vector3 position, Quaternion rotation, string name = "Unnamed", bool isManuallyPlaced = false, string placedBy = "", string portalPermission = "", bool broadcastEnabled = false, BasePlayer placedByPlayer = null,
                float itemCondition = 100f, string ownerGroup = "", bool placedByAdmin = false)
            {
                var portal = GameManager.server.CreateEntity(PORTAL_PREFAB, position, rotation);
                portal.Spawn();

                if (placedBy != "")
                {
                    portal.OwnerID = Convert.ToUInt64(placedBy);
                }

                portal.EnableSaving(false);


                var comp = portal.gameObject.AddComponent<CustomPortalComponent>();

                comp.IsManuallyPlaced = isManuallyPlaced;
                comp.PlacedBy = placedBy;
                comp.PortalPermission = portalPermission;
                comp.ItemCondition = itemCondition;
                comp.OwnerGroup = ownerGroup;

                if (name != "Unnamed")
                {
                    comp.SetPortalName(name, placedByPlayer);
                }

                comp.SetBroadcastEnabled(broadcastEnabled);

                return comp;
            }

            public static Item CreateItem(int amount = 1, float condition = 100f)
            {
                var item = ItemManager.CreateByItemID(config.PortalItemID, amount, config.PortalSkinID);
                item.name = config.PortalItemName;
                item.condition = condition;
                return item;
            }

            public string PortalName
            {
                get { return _portalName; }
                private set
                {
                    _portalName = value;

                    if (_vendingMarker != null && !_vendingMarker.IsDestroyed)
                    {
                        _vendingMarker.markerShopName = "Portal - " + value;
                        _vendingMarker.SendNetworkUpdateImmediate();
                    }
                }
            }

            public string PortalPermission
            {
                get { return _permission; }
                set
                {
                    _permission = value.Trim();

                    if (value != "" && !Instance.permission.PermissionExists($"{Instance.Name.ToLower()}.portal.{value}", Instance))
                    {
                        Instance.permission.RegisterPermission($"{Instance.Name.ToLower()}.portal.{value}", Instance);
                    }
                }
            }

            public CustomPortalComponent LinkedPortal { get; private set; }
            public bool IsManuallyPlaced { get; private set; }
            public bool IsPlacedByAdmin { get; private set; }
            public string PlacedBy { get; private set; }
            public string OwnerGroup { get; private set; }
            public bool IsBroadcastEnabled { get; private set; }
            public float ItemCondition { get; private set; }
            public int HP { get; private set; }

            MapMarkerGenericRadius _marker;
            VendingMachineMapMarker _vendingMarker;
            BasePortal _portal;
            string _permission;
            string _portalName;

            public BasePortal GetEntity() => _portal;
            public void Kill() => _portal.AdminKill();

            void Awake()
            {
                _portal = GetComponent<BasePortal>();
                PortalName = "Unnamed";
                HP = config.PortalHP;

                CustomPortalsDict.Add(_portal, this);

                if (config.Display_Portal_Name)
                {
                    InvokeRepeating(UpdateNameText, 0f, 1f);
                }

                if (config.Breakable_Portals)
                {
                    InvokeRepeating(CheckAndShowHealthbarRoutine, 0f, 0.5f);
                }
            }

            void OnDestroy()
            {
                DisableBroadcast();
                CancelInvoke(UpdateNameText);
                CancelInvoke(CheckAndShowHealthbarRoutine);
                CheckAndShowHealthbarRoutine();
                CustomPortalsDict?.Remove(_portal);
            }

            public object OnPlayerEnter(BasePlayer player)
            {
                if (LastPortalUseTime.ContainsKey(player) && Time.time - LastPortalUseTime[player] < config.Portal_Use_Cooldown)
                {
                    Instance.PrintToChat(player, GetText("cooldown", player));
                    return false;
                }
                
                if (!Instance.permission.UserHasPermission(player.UserIDString, PORTAL_USE_PERMISSION) || (_permission != "" && !Instance.permission.UserHasPermission(player.UserIDString, $"{Instance.Name.ToLower()}.portal.{PortalPermission}")))
                {
                    Instance.PrintToChat(player, GetText("cannotUse", player));
                    return false;
                }

                if (!PlayerHasPortalGroup(player, OwnerGroup) && config.Allow_Only_In_The_Same_Group)
                {
                    Instance.PrintToChat(player, GetText("notInSameGroup", player));
                    return false;
                }

                if (config.Disable_If_Combat_Blocked && IsCombatBlocked(player.UserIDString) == true)
                {
                    Instance.PrintToChat(player, GetText("combatBlocked", player));
                    return false;
                }

                if (config.Disable_If_Raid_Blocked && IsRaidBlocked(player.UserIDString) == true)
                {
                    Instance.PrintToChat(player, GetText("raidBlocked", player));
                    return false;
                }

                if (PlacedBy != "" && player.UserIDString != PlacedBy)
                {
                    if (config.Restrict_To_Clans_Members && IsClanMember(player.UserIDString, PlacedBy) == false)
                    {
                        Instance.PrintToChat(player, GetText("notInClan", player));
                        return false;
                    }

                    if (config.Restrict_To_Clans_Members_And_Allies && IsMemberOrAlly(player.UserIDString, PlacedBy) == false)
                    {
                        Instance.PrintToChat(player, GetText("notInClanOrAlliedClan", player));
                        return false;
                    }
                }

                var activeItem = player.GetActiveItem();

                if (
                    ((!IsPlacedByAdmin && IsManuallyPlaced && ((!config.Allow_Pickup_Only_In_Building_Privilege && !player.IsBuildingBlocked()) ||
                                                               (config.Allow_Pickup_Only_In_Building_Privilege && player.IsBuildingAuthed())))
                     ||
                     (IsPlacedByAdmin && IsManuallyPlaced && player.IsAdmin))
                    &&
                    activeItem != null && activeItem.info.itemid == HAMMER_ITEMID)
                {
                    if (!player.HasComponent<TryPickupPortalComponent>())
                        player.gameObject.AddComponent<TryPickupPortalComponent>().SetPortal(this);
                    return false;
                }

                if (LinkedPortal == null && IsManuallyPlaced)
                {
                    if (!((!config.Allow_Setting_Up_Only_In_Building_Privilege && !player.IsBuildingBlocked()) ||
                          (config.Allow_Setting_Up_Only_In_Building_Privilege && player.IsBuildingAuthed())))
                    {
                        player.ShowToast(GameTip.Styles.Red_Normal, GetText("canSetupOnlyInBuildingPrivilege", player));
                        return false;
                    }

                    StartSettingUpPortal(player);

                    return false;
                }

                if (!config.PricePerEntry.TryPaying(player, PORTAL_FREE_ENTRY_PERMISSION, GetText("notEnoughPointsToEnter", player), GetText("notEnoughResourcesToEnter", player)))
                {
                    return false;
                }

                if (!LastPortalUseTime.ContainsKey(player)) 
                    LastPortalUseTime.Add(player, Time.time);
                LastPortalUseTime[player] = Time.time;
                
                return null;
            }

            public void StartSettingUpPortal(BasePlayer player)
            {
                if (PlayersCurrentlyRenamingPortals.ContainsKey(player))
                {
                    PlayersCurrentlyRenamingPortals[player] = this;
                }
                else
                {
                    PlayersCurrentlyRenamingPortals.Add(player, this);
                }

                ShowRenamingUI(player, PortalName, config.Allow_Players_To_Enable_Broadcast, IsBroadcastEnabled);

#if DEBUG
                ShowDebugUI(player, this);
#endif

                if (Instance.permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    ShowAdminUI(player, PortalPermission);
                }
            }

            public void TakeDamage(int damage)
            {
                if (!IsManuallyPlaced)
                {
                    return;
                }

                if (!config.Breakable_Portals)
                {
                    return;
                }

                if ((HP - damage) <= 0)
                {
                    Kill();
                    return;
                }

                HP -= damage;
            }


            public bool SetPortalName(string newName, BasePlayer changedBy = null)
            {
                CustomPortalComponent portalWithTheSameName = null;
                var count = 0;

                foreach (var portal in CustomPortalsDict.Values)
                {
                    if (portal == this)
                    {
                        continue;
                    }

                    if (portal.PortalName != newName || portal.PortalName == "Unnamed")
                    {
                        continue;
                    }

                    portalWithTheSameName = portal;

                    count++;
                }


                if (count >= 2)
                {
                    Instance.PrintWarning($"Trying to rename a portal to \"{newName}\", but there are more than 2 portals with that name!");
                    return false;
                }


                var otherPortalOwnerID = portalWithTheSameName?.PlacedBy;

                if (otherPortalOwnerID != null && changedBy != null && config.AllowLinkingFrom > 0 && otherPortalOwnerID != "" && !Instance.permission.UserHasPermission(changedBy.UserIDString, PORTAL_BYPASS_LINKING_LIMITATIONS))
                {
                    var isOk = false;

                    switch (config.AllowLinkingFrom)
                    {
                        case 1:
                            isOk = otherPortalOwnerID == PlacedBy;
                            break;
                        case 2:
                            isOk = otherPortalOwnerID == PlacedBy;
                            if (changedBy?.Team?.members == null)
                            {
                                isOk = true;
                                break;
                            }

                            foreach (var teammateId in changedBy.Team.members)
                            {
                                if (teammateId.ToString() != otherPortalOwnerID)
                                {
                                    continue;
                                }

                                isOk = true;
                                break;
                            }

                            break;
                    }

                    if (!isOk)
                    {
                        ShowRenamingErrorUI(changedBy, GetText("cantLinkToOthersPortal", changedBy));
                        return false;
                    }
                }

                PortalName = newName;

                if (count == 1)
                {
                    LinkPortal(portalWithTheSameName);
                }
                else
                {
                    Unlink();
                }

                return true;
            }

            public void Unlink()
            {
                if (LinkedPortal == null)
                {
                    return;
                }

                LinkedPortal._portal.targetPortal = null;
                LinkedPortal._portal.LinkPortal();

                LinkedPortal.LinkedPortal = null;

                _portal.targetPortal = null;
                _portal.LinkPortal();

                LinkedPortal = null;
            }

            public void LinkPortal(CustomPortalComponent portalToLinkTo)
            {
                if (portalToLinkTo?._portal?.net == null)
                {
                    return;
                }

                LinkedPortal = portalToLinkTo;

                LinkedPortal.LinkedPortal = this;

                _portal.targetPortal = LinkedPortal._portal;
                LinkedPortal._portal.targetPortal = _portal;

                _portal.isUsablePortal = true;
                LinkedPortal._portal.isUsablePortal = true;

                _portal.LinkPortal();
                LinkedPortal._portal.LinkPortal();

                _portal.SendNetworkUpdateImmediate();
                LinkedPortal._portal.SendNetworkUpdateImmediate();
            }

            public void SetBroadcastEnabled(bool isEnabled)
            {
                if (isEnabled)
                {
                    EnableBroadcast();
                }
                else
                {
                    DisableBroadcast();
                }
            }

            public void ToggleBroadcast()
            {
                if (IsBroadcastEnabled)
                {
                    DisableBroadcast();
                }
                else
                {
                    EnableBroadcast();
                }
            }

            void CheckAndShowHealthbarRoutine()
            {
                var visPlayers = Pool.GetList<BasePlayer>();
                var visAndRaycastedPlayers = Pool.GetList<BasePlayer>();

                Vis.Entities(_portal.transform.position, 2f, visPlayers);

                foreach (var player in visPlayers)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 2f, OBSTRUCTION_MASK))
                    {
                        var ent = hit.GetEntity();
                        if (ent == _portal.GetEntity())
                        {
                            visAndRaycastedPlayers.Add(player);
                        }
                    }
                }

                Pool.FreeList(ref visPlayers);


                foreach (var player in PlayersWithHealthBarShowed.ToArray())
                {
                    if (!visAndRaycastedPlayers.Contains(player))
                    {
                        CuiHelper.DestroyUi(player, HP_BAR_CUI_NAME);
                        PlayersWithHealthBarShowed.Remove(player);
                    }
                }

                foreach (var player in visAndRaycastedPlayers)
                {
                    ShowHealthbar(player, HP, config.PortalHP);

                    if (!PlayersWithHealthBarShowed.Contains(player))
                    {
                        PlayersWithHealthBarShowed.Add(player);
                    }
                }

                Pool.FreeList(ref visAndRaycastedPlayers);
            }

            void EnableBroadcast()
            {
                if (_marker != null && !_marker.IsDestroyed)
                {
                    return;
                }

                _marker = GameManager.server.CreateEntity(RADIUS_MAP_MARKER_PREFAB, transform.position) as MapMarkerGenericRadius;

                _marker.alpha = 1f;
                _marker.color1 = Color.magenta;
                _marker.color2 = Color.white;
                _marker.radius = 0.1f;

                _marker.Spawn();
                _marker.SendUpdate();
                _marker.EnableSaving(false);

                _vendingMarker = GameManager.server.CreateEntity(VENDING_MAP_MARKER_PREFAB, transform.position) as VendingMachineMapMarker;
                _vendingMarker.markerShopName = "Portal - " + PortalName;

                _vendingMarker.Spawn();
                _vendingMarker.EnableSaving(false);
                IsBroadcastEnabled = true;
            }

            void DisableBroadcast()
            {
                IsBroadcastEnabled = false;
                _marker?.Kill();
                _vendingMarker?.Kill();
                _vendingMarker = null;
                _marker = null;
            }

            void UpdateNameText()
            {
                var players = Pool.GetList<BasePlayer>();

                Vis.Entities(transform.position, 5f, players, PLAYER_LAYER_MASK, QueryTriggerInteraction.Ignore);

                foreach (var player in players)
                {
                    if (player.IsNpc || player.IPlayer == null)
                    {
                        continue;
                    }

                    if (Physics.Raycast(player.eyes.HeadRay(), player.Distance(transform.position), OBSTRUCTION_MASK, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    var wasAdmin = player.IsAdmin;
                    if (!wasAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendNetworkUpdateImmediate();
                    }

                    player.SendConsoleCommand("ddraw.text", 1f, Color.cyan, transform.position + (Vector3.up * 1.5f), PortalName);

                    if (!wasAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        player.SendNetworkUpdateImmediate();
                    }
                }

                Pool.FreeList(ref players);
            }
        }

        #endregion

        #region Oxide Hooks

        void Init()
        {
            Instance = this;
            dataFiles = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\{Name}");

            CustomPortalsDict = new Dictionary<BasePortal, CustomPortalComponent>();
            PortalNameBuffer = new Dictionary<BasePlayer, string>();
            PortalPermissionBuffer = new Dictionary<BasePlayer, string>();
            CustomMapPortals = new Dictionary<string, List<PositionData>>();
            LastPortalUseTime = new Dictionary<BasePlayer, float>();
            ToRemove = new List<BaseNetworkable>();
            PlayersCurrentlyRenamingPortals = new Dictionary<BasePlayer, CustomPortalComponent>();
            PlayersWithHealthBarShowed = new List<BasePlayer>();
            MonumentPortals = new Dictionary<MonumentInfo, List<CustomPortalComponent>>();

            permission.RegisterPermission(ADMIN_PERMISSION, this);
            permission.RegisterPermission(PORTAL_BUY_PERMISSION, this);
            permission.RegisterPermission(PORTAL_FREE_PERMISSION, this);
            permission.RegisterPermission(PORTAL_USE_PERMISSION, this);
            permission.RegisterPermission(PORTAL_FREE_ENTRY_PERMISSION, this);
            permission.RegisterPermission(PORTAL_LIMIT_BYPASS_PERMISSION, this);
            permission.RegisterPermission(PORTAL_BYPASS_LINKING_LIMITATIONS, this);
            permission.RegisterPermission(PORTAL_SPAWN_PERMISSION, this);

            foreach (var groupName in config.Groups)
            {
                permission.RegisterPermission(PERMISSION_GROUP_PREFIX + groupName, this);
            }

            if (config.Breakable_Portals)
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }
            else
            {
                Unsubscribe(nameof(OnEntityTakeDamage));
            }
#if DEBUG
            PrintWarning("Plugin is in DEBUG mode!");
#endif
        }

        void Loaded()
        {
            foreach (var limit in config.Portal_Limits)
            {
                var perm = $"customportals.limit.{limit}";
                if (permission.PermissionExists(perm, this))
                {
                    continue;
                }

                permission.RegisterPermission(perm, this);
            }
        }

        void Unload()
        {
            SavePlayerData();
            CacheCustomMapData();

            foreach (var player in PlayersWithHealthBarShowed)
            {
                CuiHelper.DestroyUi(player, PICKUP_CUI_NAME);
                CuiHelper.DestroyUi(player, HP_BAR_CUI_NAME);
            }

            foreach (var portal in CustomPortalsDict.Values)
            {
                portal.Kill();
            }
        }

        void OnServerInitialized(bool initial)
        {
            PortalDataLoader.Instantiate().BeginLoadingData(initial);
        }

        void OnServerSave() => SavePlayerData();

        void OnNewSave(string filename)
        {
            PrintWarning("Server wiped! Clearing portal data...");
            ClearPlayerData();
            ClearCustomMapCache();
        }

        object OnPortalUse(BasePlayer player, BasePortal portal)
        {
            if (portal is XmasDungeon)
            {
                return null;
            }

            if (!CustomPortalsDict.ContainsKey(portal))
            {
                return null;
            }

            return CustomPortalsDict[portal].OnPlayerEnter(player);
        }

        object CanBuild(Planner plan, Construction prefab, Construction.Target target)
        {
            if (plan == null)
            {
                return null;
            }

            var item = plan.GetItem();
            if (item == null)
            {
                return null;
            }

            var player = plan.GetOwnerPlayer();
            if (player == null)
            {
                return null;
            }

            if (!(item.info.itemid == config.PortalItemID && item.skin == config.PortalSkinID))
            {
                return null;
            }

            if (config.Enforce_Player_Portal_Limits && !permission.UserHasPermission(player.UserIDString, PORTAL_LIMIT_BYPASS_PERMISSION))
            {
                var playerPortals = 0;
                foreach (var portal in CustomPortalsDict.Values)
                {
                    if (portal.PlacedBy != player.UserIDString)
                    {
                        continue;
                    }

                    playerPortals++;
                }

                var playerLimit = -1;

                foreach (var perm in permission.GetUserPermissions(player.UserIDString))
                {
                    if (!perm.StartsWith("customportals.limit."))
                    {
                        continue;
                    }

                    var limit = Convert.ToInt32(perm.Split('.')[2]);
                    if (playerLimit < limit)
                    {
                        playerLimit = limit;
                    }
                }

                if (playerLimit != -1 && playerPortals >= playerLimit)
                {
                    player.ShowToast(GameTip.Styles.Red_Normal, string.Format(GetText("limitExceeded", player), playerLimit));
                    return false;
                }
            }

            if (config.Allow_Placement_Only_In_Building_Privilege && !player.IsBuildingAuthed())
            {
                player.ShowToast(GameTip.Styles.Red_Normal, GetText("canPlaceOnlyInBuildingPrivilege", player));
                return false;
            }

            return null;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null)
            {
                return;
            }

            var item = plan.GetItem();
            if (item == null)
            {
                return;
            }

            var player = plan.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }

            if (item.info.itemid == config.PortalItemID && item.skin == config.PortalSkinID)
            {
                var portal = CustomPortalComponent.Spawn(go.transform.position - PORTAL_PLACEMENT_OFFSET, go.transform.rotation, isManuallyPlaced: true, placedBy: player.UserIDString, placedByPlayer: player, itemCondition: item.condition, ownerGroup: GetPortalGroup(player));

                NextFrame(() => { go.GetComponent<BaseEntity>().AdminKill(); });
            }
        }

        void OnWorldPrefabSpawned(GameObject gameObject, string category)
        {
            if (!category.ToLower().Contains("customportals"))
            {
                return;
            }

            try
            {
                Debug.LogWarning("portal name - " + category.ToLower());
                var portalName = category.ToLower().Split('.')[1].Split(':')[0].ToLower().Trim();

                var ent = gameObject.GetComponent<BaseNetworkable>();
                var posData = new PositionData(gameObject.transform.position, gameObject.transform.rotation);

                if (!CustomMapPortals.ContainsKey(portalName))
                {
                    CustomMapPortals.Add(portalName, new List<PositionData>());
                }

                CustomMapPortals[portalName].Add(posData);
                ToRemove.Add(ent);
            }
            catch
            {
                PrintWarning($"Portal name \"{category}\" is invalid!");
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity as BasePortal))
            {
                return null;
            }

            CustomPortalComponent comp;

            if (CustomPortalsDict.TryGetValue(entity as BasePortal, out comp))
            {
                if (info?.WeaponPrefab?.ShortPrefabName != null && config.Portal_Damage_Table_Explosives.ContainsKey(info.WeaponPrefab.ShortPrefabName))
                {
                    comp.TakeDamage(config.Portal_Damage_Table_Explosives[info.WeaponPrefab.ShortPrefabName]);
                    return null;
                }


                if (info?.ProjectilePrefab?.name != null && config.Portal_Damage_Table_Projectiles.ContainsKey(info.ProjectilePrefab.name))
                {
                    comp.TakeDamage(config.Portal_Damage_Table_Projectiles[info.ProjectilePrefab.name]);
                    return null;
                }
            }

            return null;
        }

        #endregion

        #region UI

        public static void ShowPickupProgess(BasePlayer player, float progress)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiElement
            {
                Name = PICKUP_CUI_NAME,
                Parent = "Overlay",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-100 23",
                        OffsetMax = "100 44",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = PICKUP_CUI_NAME + "_sprite",
                Parent = PICKUP_CUI_NAME,
                Components =
                {
                    new CuiImageComponent
                    {
                        Sprite = "assets/icons/pickup.png",
                        Color = "0.8 0.28 0.2 1",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "0 8",
                        OffsetMax = "17 21",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = PICKUP_CUI_NAME + "_text",
                Parent = PICKUP_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Pickup",
                        FontSize = 11,
                        Align = TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "20 0",
                        OffsetMax = "20 0",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = PICKUP_CUI_NAME + "_barbg",
                Parent = PICKUP_CUI_NAME,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.7 0.7 0.7 0.7"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = "0 0",
                        OffsetMax = "0 8",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = PICKUP_CUI_NAME + "_barfg",
                Parent = PICKUP_CUI_NAME,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = $"{Mathf.Clamp01(1 * progress)} 0",
                        OffsetMin = "0 0",
                        OffsetMax = "0 8",
                    }
                }
            });

            CuiHelper.DestroyUi(player, PICKUP_CUI_NAME);
            CuiHelper.AddUi(player, elements);
        }

        public static void ShowRenamingUI(BasePlayer player, string initialName, bool showBroadcastBtn, bool isBroadcastEnabled = false)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME,
                Parent = "Overlay",
                Components =
                {
                    new CuiNeedsCursorComponent(),
                    new CuiNeedsKeyboardComponent(),
                    new CuiImageComponent
                    {
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd",
                        Color = "0.1286 0.1808 0.1512 0.954"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_textboxPanel",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.9686 0.9216 0.8824 0.298",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-156 -18",
                        OffsetMax = "156 18"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_textboxPanel_textbox",
                Parent = RENAMING_CUI_NAME + "_textboxPanel",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = "1 1 1 1",
                        LineType = InputField.LineType.SingleLine,
                        Command = SET_PORTAL_NAME_CCMD,
                        IsPassword = false,
                        CharsLimit = 25,
                        ReadOnly = false,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Text = initialName,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });


            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_cancelBtn",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = "0.909 0.458 0.388 0.77",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Command = CANCEL_PORTAL_NAME_CCMD,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-156 -57",
                        OffsetMax = "-2 -23"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_cancelBtn_text",
                Parent = RENAMING_CUI_NAME + "_cancelBtn",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetText("cancelBtnText", player),
                        Color = "0.9686 0.9216 0.8824 1",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_saveBtn",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = "0.5725 0.6549 0.4235 1",
                        Command = SAVE_PORTAL_NAME_CCMD,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "2 -57",
                        OffsetMax = "156 -23"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_saveBtn_text",
                Parent = RENAMING_CUI_NAME + "_saveBtn",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetText("changeBtnText", player),
                        Color = "0.9686 0.9216 0.8824 1",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_title",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetText("uiHeader", player),
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 44,
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.9686 0.9216 0.8824 0.568"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-185 20",
                        OffsetMax = "185 100",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_infotext",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetText("uiInfoText", player),
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Align = TextAnchor.UpperCenter,
                        Color = "0.9686 0.9216 0.8824 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 0.4",
                        Distance = "1 -1",
                        UseGraphicAlpha = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-285 -85",
                        OffsetMax = "285 -65",
                    }
                }
            });

            CuiHelper.DestroyUi(player, RENAMING_CUI_NAME);
            CuiHelper.AddUi(player, elements);

            if (showBroadcastBtn)
            {
                DrawBroadcastBtn(player, isBroadcastEnabled);
            }
        }

        public static void DrawBroadcastBtn(BasePlayer player, bool isBroadcastEnabled)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_broadcastBtn",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = isBroadcastEnabled ? "0.909 0.458 0.388 0.77" : "0.5725 0.6549 0.4235 1",
                        Command = TOGGLE_PORTAL_BROADCAST_CCMD,
                        FadeIn = 0.1f,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-156 -157",
                        OffsetMax = "156 -123"
                    }
                }
            });
            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "broadcastBtn_text",
                Parent = RENAMING_CUI_NAME + "_broadcastBtn",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetText(isBroadcastEnabled ? "broadcastOff" : "broadcastOn", player),

                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        Color = "0.9686 0.9216 0.8824 1",
                        FontSize = 18
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });

            CuiHelper.DestroyUi(player, RENAMING_CUI_NAME + "_broadcastBtn");
            CuiHelper.AddUi(player, elements);
        }

        public static void ShowRenamingErrorUI(BasePlayer player, string error_message)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_errorText",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = error_message,
                        FontSize = 17,
                        Font = "robotocondensed-bold.ttf",
                        FadeIn = 0.1f,
                        Align = TextAnchor.UpperCenter,
                        Color = "0.909 0.258 0.288 1",
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 0.5",
                        Distance = "1 -1",
                        UseGraphicAlpha = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-285 -115",
                        OffsetMax = "285 -90"
                    }
                }
            });

            CuiHelper.DestroyUi(player, RENAMING_CUI_NAME + "_errorText");
            CuiHelper.AddUi(player, elements);
        }

        public static void ShowAdminUI(BasePlayer player, string portalPerm)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_permissiontextboxPanel",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.9686 0.9216 0.8824 0.298",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-156 -260",
                        OffsetMax = "156 -221"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_permissiontextboxPanel_textbox",
                Parent = RENAMING_CUI_NAME + "_permissiontextboxPanel",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = "1 1 1 1",
                        LineType = InputField.LineType.SingleLine,
                        Command = SET_PORTAL_PERMISSION_CCMD,
                        IsPassword = false,
                        CharsLimit = 25,
                        ReadOnly = false,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Text = portalPerm,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_permissiontextboxPanel_saveBtn",
                Parent = RENAMING_CUI_NAME + "_permissiontextboxPanel",
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = "0.5725 0.6549 0.4235 1",
                        Command = SAVE_PORTAL_PERMISSION_CCMD,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "0 -57",
                        OffsetMax = "0 -23"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_permissiontextboxPanel_saveBtn_text",
                Parent = RENAMING_CUI_NAME + "_permissiontextboxPanel_saveBtn",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetText("setPerm", player),
                        Color = "0.9686 0.9216 0.8824 1",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = RENAMING_CUI_NAME + "_Admininfotext",
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetText("uiAdminInfoText", player),
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Align = TextAnchor.UpperCenter,
                        Color = "0.9686 0.9216 0.8824 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 0.4",
                        Distance = "1 -1",
                        UseGraphicAlpha = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-285 -330",
                        OffsetMax = "285 -310",
                    }
                }
            });


            CuiHelper.DestroyUi(player, RENAMING_CUI_NAME + "_permissiontextboxPanel");
            CuiHelper.AddUi(player, elements);
        }

        public static void ShowHealthbar(BasePlayer player, int currentHp, int maxHp)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiElement
            {
                Name = HP_BAR_CUI_NAME,
                Parent = "Hud",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-75 192",
                        OffsetMax = "74 214"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = $"{HP_BAR_CUI_NAME}_bar",
                Parent = HP_BAR_CUI_NAME,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.537 0.439 0.231 1",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.33"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = $"{HP_BAR_CUI_NAME}_bar_slider",
                Parent = $"{HP_BAR_CUI_NAME}_bar",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = $"{currentHp / (float)maxHp} 1"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = $"{HP_BAR_CUI_NAME}_stable",
                Parent = HP_BAR_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "100% STABLE",
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 0.95",
                        FontSize = 11,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = $"{HP_BAR_CUI_NAME}_hp",
                Parent = HP_BAR_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{currentHp} / {maxHp}",
                        Align = TextAnchor.UpperRight,
                        Color = "1 1 1 0.95",
                        FontSize = 11,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });


            CuiHelper.DestroyUi(player, HP_BAR_CUI_NAME);
            CuiHelper.AddUi(player, elements);
        }
#if DEBUG
        public static void ShowDebugUI(BasePlayer player, CustomPortalComponent portal)
        {
            var elements = new CuiElementContainer();

            var sb = new StringBuilder();

            sb.Append("== Properties ==\n");

            foreach (var property in portal.GetType().GetProperties())
            {
                sb.Append($"{property.Name} - {property.GetValue(portal)}\n");
            }

            elements.Add(new CuiElement
            {
                Name = DEBUG_CUI_NAME,
                Parent = RENAMING_CUI_NAME,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = sb.ToString(),
                        FontSize = 14,
                        Color = "1 1 1 1",
                        Align = TextAnchor.UpperLeft,
                        Font = "robotocondensed-regular.ttf",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 0.05",
                        AnchorMax = "0.3 0.95"
                    }
                }
            });

            CuiHelper.DestroyUi(player, DEBUG_CUI_NAME);
            CuiHelper.AddUi(player, elements);
        }
#endif

        #endregion

        #region Save/Load

        static string GetMonumentName(MonumentInfo monument) => monument.displayPhrase.english + monument.Bounds.extents.ToString();

        class PortalDataLoader : MonoBehaviour
        {
            public static PortalDataLoader Instantiate()
            {
                var go = new GameObject();
                return go.AddComponent<PortalDataLoader>();
            }

            public void BeginLoadingData(bool initial)
            {
                StartCoroutine(LoadPlayerData());
                StartCoroutine(LoadMonumentData());

                if (!initial)
                {
                    CustomMapPortals = dataFiles.ReadObject<Dictionary<string, List<PositionData>>>(CACHED_MAP_DATA_FILENAME);
                    if (CustomMapPortals.Count > 0)
                    {
                        Instance.Puts("Plugin is hotloaded - restored custom map data from cache");
                    }
                }

                StartCoroutine(ProcessCustomMapData());
            }

            IEnumerator ProcessCustomMapData()
            {
                var i = 0;

                foreach (var portalName in CustomMapPortals.Keys)
                {
                    if(CustomMapPortals[portalName] == null) continue;
                    
                    foreach (var posData in CustomMapPortals[portalName])
                    {
                        CustomPortalComponent.Spawn(posData.Position, Quaternion.Euler(posData.Rotation), portalName);
                        i++;
                        yield return null;
                    }
                }

                foreach (var ent in ToRemove)
                {
                    ent.Kill();
                }

                if (i > 0)
                {
                    Instance.Puts($"Processed {i} custom map portals");
                }

                yield return null;
            }

            IEnumerator LoadPlayerData()
            {
                var sPlayerData = dataFiles.ReadObject<SerializedPlayerGeneratedData>(PLAYER_DATA_FILENAME);

                if (sPlayerData == null)
                {
                    yield break;
                }

                foreach (var sPortal in sPlayerData.SerializedPortals)
                {
                    CustomPortalComponent.Spawn(sPortal.Position, Quaternion.Euler(sPortal.Rotation), sPortal.Name, isManuallyPlaced: true, placedBy: sPortal.PlacedBy, portalPermission: sPortal.Permission, broadcastEnabled: sPortal.IsBroadcastEnabled,
                        itemCondition: sPortal.ItemCondition, ownerGroup: sPortal.OwnerGroup, placedByAdmin: sPortal.IsPlacedByAdmin);
                    yield return null;
                }

                Instance.Puts("Loaded deployed portals");
            }

            IEnumerator LoadMonumentData()
            {
                var sMonumentData = dataFiles.ReadObject<SerializedMonumentData>(MONUMENT_DATA_FILENAME);
                if (sMonumentData?.SerializedMonumentPortals == null)
                {
                    yield break;
                }

                foreach (var sEntry in sMonumentData.SerializedMonumentPortals)
                {
                    var i = 0;

                    foreach (var monument in TerrainMeta.Path.Monuments)
                    {
                        var monumentName = GetMonumentName(monument);

                        if (monumentName != sEntry.Monument)
                        {
                            continue;
                        }

                        if (!MonumentPortals.ContainsKey(monument))
                        {
                            MonumentPortals.Add(monument, new List<CustomPortalComponent>());
                        }

                        var position = monument.transform.TransformPoint(sEntry.Position);
                        var rotation = sEntry.Rotation + monument.transform.rotation.eulerAngles;
                        MonumentPortals[monument].Add(CustomPortalComponent.Spawn(position, Quaternion.Euler(rotation), i == 0 ? sEntry.Name : sEntry.Name + "|||" + monument.transform.position.ToString(), broadcastEnabled: config.Show_Monument_Portals_On_Map));

                        i++;
                        yield return null;
                    }
                }

                Instance.Puts("Loaded monument portals");
            }
        }

        void SavePlayerData()
        {
            var sData = new SerializedPlayerGeneratedData();

            foreach (var portal in CustomPortalsDict.Values)
            {
                if (!portal.IsManuallyPlaced)
                {
                    continue;
                }

                var sPortal = new SerializedDeployedPortal();

                sPortal.Position = portal.transform.position;
                sPortal.Rotation = portal.transform.rotation.eulerAngles;
                sPortal.Name = portal.PortalName;
                sPortal.PlacedBy = portal.PlacedBy;
                sPortal.IsPlacedByAdmin = portal.IsPlacedByAdmin;
                sPortal.Permission = portal.PortalPermission;
                sPortal.IsBroadcastEnabled = portal.IsBroadcastEnabled;
                sPortal.ItemCondition = portal.ItemCondition;
                sPortal.OwnerGroup = portal.OwnerGroup;

                sData.SerializedPortals.Add(sPortal);
            }

            dataFiles.WriteObject(PLAYER_DATA_FILENAME, sData);
        }

        void ClearPlayerData()
        {
            foreach (var portal in CustomPortalsDict.Values)
            {
                if (!portal.IsManuallyPlaced)
                {
                    continue;
                }

                portal.Kill();
            }

            dataFiles.WriteObject(PLAYER_DATA_FILENAME, new SerializedPlayerGeneratedData());
        }

        void SaveMonumentData()
        {
            var sData = new SerializedMonumentData();
            var savedMonuments = Pool.GetList<string>();

            foreach (var monument in MonumentPortals.Keys)
            {
                var monumentName = GetMonumentName(monument);

                if (savedMonuments.Contains(monumentName))
                {
                    continue;
                }

                foreach (var spawnable in MonumentPortals[monument])
                {
                    var ent = spawnable.GetEntity();
                    if (ent == null || ent.IsDestroyed)
                    {
                        continue;
                    }

                    var sEntry = new SerializedMonumentPortal();

                    sEntry.Position = monument.transform.InverseTransformPoint(ent.transform.position);
                    sEntry.Rotation = ent.transform.rotation.eulerAngles - monument.transform.rotation.eulerAngles;
                    sEntry.Monument = monumentName;
                    sEntry.Name = CustomPortalsDict[ent].PortalName.Split('|')[0];

                    sData.SerializedMonumentPortals.Add(sEntry);
                }

                savedMonuments.Add(monumentName);
            }

            Pool.FreeList(ref savedMonuments);
            dataFiles.WriteObject(MONUMENT_DATA_FILENAME, sData);
        }

        void CacheCustomMapData()
        {
            dataFiles.WriteObject(CACHED_MAP_DATA_FILENAME, CustomMapPortals, true);
        }

        void ClearCustomMapCache()
        {
            dataFiles.WriteObject(CACHED_MAP_DATA_FILENAME, "", true);
        }

        class SerializedPlayerGeneratedData
        {
            public List<SerializedDeployedPortal> SerializedPortals = new List<SerializedDeployedPortal>();
        }

        class SerializedDeployedPortal
        {
            public Vector3 Position;
            public Vector3 Rotation;
            public string Name;
            public string PlacedBy;
            public bool IsPlacedByAdmin;
            public string Permission;
            public bool IsBroadcastEnabled;
            public float ItemCondition;
            public string OwnerGroup;
        }

        class SerializedMonumentData
        {
            public List<SerializedMonumentPortal> SerializedMonumentPortals = new List<SerializedMonumentPortal>();
        }

        class SerializedMonumentPortal
        {
            public string Monument;
            public Vector3 Position;
            public Vector3 Rotation;
            public string Name;
        }

        public struct PositionData
        {
            public Vector3 Position;
            public Vector3 Rotation;

            public PositionData(Vector3 pos, Quaternion rot)
            {
                Position = pos;
                Rotation = rot.eulerAngles;
            }

            public PositionData(Vector3 pos, Vector3 rot)
            {
                Position = pos;
                Rotation = rot;
            }
        }

        #endregion

        #region Misc Methods

        public static string GetPortalGroup(BasePlayer player)
        {
            var userPerms = Instance.permission.GetUserPermissions(player.UserIDString);
            var ownerGroup = "";

            foreach (var groupName in config.Groups)
            {
                if (userPerms.Contains(PERMISSION_GROUP_PREFIX + groupName))
                {
                    ownerGroup = groupName;
                }
            }

            return ownerGroup;
        }

        public static bool PlayerHasPortalGroup(BasePlayer player, string portalGroup)
        {
            if (portalGroup.Trim() == string.Empty)
            {
                return true;
            }

            var userPerms = Instance.permission.GetUserPermissions(player.UserIDString);

            foreach (var perm in userPerms)
            {
                if (!perm.StartsWith(PERMISSION_GROUP_PREFIX))
                {
                    continue;
                }

                if (PERMISSION_GROUP_PREFIX + portalGroup == perm)
                {
                    return true;
                }
            }

            return false;
        }

        public static MonumentInfo GetMonumentOnPosition(Vector3 pos)
        {
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (Vector3.Distance(pos, monument.transform.position) < monument.Bounds.size.x)
                {
                    return monument;
                }
            }

            return null;
        }

        public static void WriteLine(BasePlayer player, string message)
        {
            if (player == null)
            {
                Instance.Puts(message);
                return;
            }

            Instance.PrintToConsole(player, $"[{Instance.Name}] " + message);
        }

        public static bool CheckDependency(string plugin_name)
        {
            var fields = Instance.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(Plugin))
                {
                    continue;
                }

                if (field.Name.ToLower() != plugin_name.ToLower())
                {
                    continue;
                }

                if (field.GetValue(Instance) == null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public static bool CheckDependencyAndPrintWarning(string plugin_name, BasePlayer player = null)
        {
            if (!CheckDependency(plugin_name))
            {
                Instance.PrintError($"{plugin_name} is not installed, but it is used in the config. Execution aborted!");

                if (player != null)
                {
                    Instance.PrintToChat(player, $"<color=red>{plugin_name} is not installed, but it is used in the config. Execution aborted!</color>");
                }

                return false;
            }

            return true;
        }

        #endregion

        #region Console Commands

        [ConsoleCommand(SET_PORTAL_PERMISSION_CCMD)]
        void SetPortalPermissionCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args.Length == 0)
            {
                return;
            }

            if (PortalPermissionBuffer.ContainsKey(player))
            {
                PortalPermissionBuffer[player] = arg.Args[0];
            }
            else
            {
                PortalPermissionBuffer.Add(player, arg.Args[0]);
            }
        }

        [ConsoleCommand(SAVE_PORTAL_PERMISSION_CCMD)]
        void SavePortalPermissionCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (PortalPermissionBuffer.ContainsKey(player))
            {
                if (PlayersCurrentlyRenamingPortals.ContainsKey(player))
                {
                    PlayersCurrentlyRenamingPortals[player].PortalPermission = PortalPermissionBuffer[player];
                    PortalPermissionBuffer.Remove(player);
                }
            }

            CuiHelper.DestroyUi(player, RENAMING_CUI_NAME);
#if DEBUG
            CuiHelper.DestroyUi(player, DEBUG_CUI_NAME);
#endif
        }


        [ConsoleCommand(TOGGLE_PORTAL_BROADCAST_CCMD)]
        void ToggleBroadcastCCmd(ConsoleSystem.Arg arg)
        {
            if (!config.Allow_Players_To_Enable_Broadcast)
            {
                return;
            }

            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (PlayersCurrentlyRenamingPortals.ContainsKey(player))
            {
                var portal = PlayersCurrentlyRenamingPortals[player];
                portal.ToggleBroadcast();
                DrawBroadcastBtn(player, portal.IsBroadcastEnabled);
            }
        }

        [ConsoleCommand(SET_PORTAL_NAME_CCMD)]
        void SetPortalNameCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args.Length == 0)
            {
                return;
            }

            if (PortalNameBuffer.ContainsKey(player))
            {
                PortalNameBuffer[player] = arg.Args[0];
            }
            else
            {
                PortalNameBuffer.Add(player, arg.Args[0]);
            }
        }

        [ConsoleCommand(SAVE_PORTAL_NAME_CCMD)]
        void SavePortalNameCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            if (PortalNameBuffer.ContainsKey(player))
            {
                var count = 0;
                foreach (var portal in CustomPortalsDict.Values)
                {
                    if (portal.PortalName != PortalNameBuffer[player] || portal.PortalName == "Unnamed")
                    {
                        continue;
                    }

                    count++;
                }

                if (count > 1)
                {
                    ShowRenamingErrorUI(player, GetText("alreadyLinked", player));
                    return;
                }

                if (PlayersCurrentlyRenamingPortals.ContainsKey(player))
                {
                    var res = PlayersCurrentlyRenamingPortals[player].SetPortalName(PortalNameBuffer[player], player);
                    PortalNameBuffer.Remove(player);

                    if (!res)
                    {
                        return;
                    }
                }
            }

            CuiHelper.DestroyUi(player, RENAMING_CUI_NAME);
        }

        [ConsoleCommand(CANCEL_PORTAL_NAME_CCMD)]
        void CancelPortalNameChangeCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                return;
            }

            CuiHelper.DestroyUi(player, RENAMING_CUI_NAME);

            if (!PortalNameBuffer.ContainsKey(player))
            {
                return;
            }

            PortalNameBuffer.Remove(player);
        }

        [ConsoleCommand("giveportal")]
        void GivePortalCCmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player != null && !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                WriteLine(player, "You don't have permission to run this console command!");
                return;
            }

            var args = arg.Args;
            if (args == null || args?.Length == 0)
            {
                WriteLine(player, "Usage: giveportal <amount> <player name>");
                return;
            }

            var amount = 1;
            var skips_first_arg = false;

            if (arg.Args[0].IsNumeric() && arg.Args.Length > 1)
            {
                amount = Convert.ToInt32(arg.Args[0]);
                skips_first_arg = true;
            }

            var target = BasePlayer.Find(string.Join(" ", skips_first_arg ? arg.Args.Skip(1) : arg.Args));
            if (target == null)
            {
                WriteLine(player, "Player not found!");
                return;
            }

            target.GiveItem(CustomPortalComponent.CreateItem(amount));
            PrintToChat(target, GetText("portalGiven", target));

            WriteLine(player, $"{amount} portal(s) were given to " + target.displayName);
        }

        #endregion

        #region Chat Commands

        [ChatCommand("buyportal")]
        void BuyPortalChatCommand(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PORTAL_BUY_PERMISSION))
            {
                PrintToChat(player, GetText("noPerms", player));
                return;
            }

            if (!config.PortalPrice.TryPaying(player, PORTAL_FREE_PERMISSION, GetText("notEnoughPoints", player), GetText("notEnoughResources", player)))
            {
                return;
            }

            player.GiveItem(CustomPortalComponent.CreateItem(config.AmountPerBuy));
            PrintToChat(player, GetText("portalGiven", player));
        }

        [ChatCommand("customportals")]
        void CustomPortalsCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                PrintToChat(player, GetText("noPerms", player));
                return;
            }

            if (args.Length == 0)
            {
                PrintToChat(player, GetText("adminCommandUsage", player));
                return;
            }

            var monument = GetMonumentOnPosition(player.transform.position);

            if (monument == null)
            {
                PrintToChat(player, GetText("noMonument", player));
                return;
            }

            var monumentName = GetMonumentName(monument);

            var dataChanged = false;

            switch (args[0].ToLower())
            {
                case "add":
                    if (args.Length <= 1)
                    {
                        PrintToChat(player, GetText("adminCommandUsage", player));
                        return;
                    }

                    RaycastHit spawnHit;
                    if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out spawnHit, 50f))
                    {
                        PrintToChat(player, GetText("lookAtSurface", player));
                        return;
                    }

                    var portalName = string.Join(" ", args.ToList().GetRange(1, args.Length - 1));

                    var globalPosition = spawnHit.point;
                    var positionOffset = monument.transform.InverseTransformPoint(globalPosition);
                    var globalRotation = new Vector3(0, player.eyes.rotation.eulerAngles.y + 180f, 0);
                    var rotationOffset = globalRotation - monument.transform.rotation.eulerAngles;

                    var i = 0;

                    foreach (var monumentToSpawnOn in TerrainMeta.Path.Monuments)
                    {
                        var monumentToSpawnOnName = GetMonumentName(monumentToSpawnOn);

                        if (monumentToSpawnOnName != monumentName)
                        {
                            continue;
                        }

                        // If there is only one monument to spawn on, don't append monument to portal name
                        var finalPortalName = i == 0 ? portalName : portalName + "|||" + monumentToSpawnOn.transform.position.ToString();

                        if (CustomPortalsDict.Values.Count(x => x.PortalName == finalPortalName) >= 2)
                        {
                            PrintToChat(player, GetText("alreadyLinked", player));
                            return;
                        }

                        if (!MonumentPortals.ContainsKey(monumentToSpawnOn))
                        {
                            MonumentPortals.Add(monumentToSpawnOn, new List<CustomPortalComponent>());
                        }

                        var spawnPos = monumentToSpawnOn.transform.TransformPoint(positionOffset);
                        var spawnRot = Quaternion.Euler(monumentToSpawnOn.transform.rotation.eulerAngles + rotationOffset);

                        MonumentPortals[monumentToSpawnOn].Add(CustomPortalComponent.Spawn(spawnPos, spawnRot, finalPortalName));

                        i++;
                        dataChanged = true;
                    }

                    PrintToChat(player, string.Format(GetText("addedToMonument", player), portalName, monumentName));
                    break;

                case "remove":
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 50f))
                    {
                        var ent = hit.GetEntity();
                        if (ent == null)
                        {
                            PrintToChat(player, GetText("lookAtPortal", player));
                            return;
                        }

                        var pos = monument.transform.InverseTransformPoint(ent.transform.position);

                        foreach (var key in MonumentPortals.Keys)
                        {
                            if (monumentName != GetMonumentName(key))
                            {
                                continue;
                            }

                            var toRemove = Pool.GetList<CustomPortalComponent>();

                            foreach (var spawnable in MonumentPortals[key])
                            {
                                var monumentEnt = spawnable.GetEntity();
                                if (monumentEnt == null || monumentEnt.IsDestroyed)
                                {
                                    continue;
                                }

                                var entPos = key.transform.InverseTransformPoint(monumentEnt.transform.position);
                                if (Vector3.Distance(pos, entPos) > 0.1f)
                                {
                                    continue;
                                }

                                toRemove.Add(spawnable);

                                dataChanged = true;
                            }

                            foreach (var entry in toRemove)
                            {
                                MonumentPortals[key].Remove(entry);
                                entry.Kill();
                            }

                            Pool.FreeList(ref toRemove);
                        }

                        if (dataChanged)
                        {
                            PrintToChat(player, string.Format(GetText("removedFromMonument", player), monumentName));
                        }
                        else
                        {
                            PrintToChat(player, GetText("lookAtPortal", player));
                        }
                    }
                    else
                    {
                        PrintToChat(player, GetText("lookAtPortal", player));
                    }

                    break;
                case "reset":
                    foreach (var monumentToReset in TerrainMeta.Path.Monuments)
                    {
                        var monumentToResetName = GetMonumentName(monumentToReset);

                        if (monumentToResetName != monumentName)
                        {
                            continue;
                        }

                        if (MonumentPortals.ContainsKey(monumentToReset))
                        {
                            foreach (var spawnable in MonumentPortals[monumentToReset])
                            {
                                spawnable.Kill();
                            }

                            MonumentPortals[monumentToReset].Clear();
                            dataChanged = true;
                        }
                    }

                    PrintToChat(player, string.Format(GetText("allRemoved", player), monumentName));
                    break;
                default:
                    PrintToChat(player, GetText("adminCommandUsage", player));
                    break;
            }

            if (dataChanged)
            {
                SaveMonumentData();
            }
        }


        [ChatCommand("changeportal")]
        void ChangePortalCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                PrintToChat(player, GetText("noPerms", player));
                return;
            }

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 50f))
            {
                var portal = hit.GetEntity()?.gameObject?.GetComponent<CustomPortalComponent>();
                if (portal == null)
                {
                    PrintToChat(player, GetText("lookAtPortal", player));
                    return;
                }

                portal.StartSettingUpPortal(player);
            }
        }


        [ChatCommand("spawnportal")]
        void SpawnPortalCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PORTAL_SPAWN_PERMISSION))
            {
                PrintToChat(player, GetText("noPerms", player));
                return;
            }

            RaycastHit spawnHit;
            if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out spawnHit, 5f))
            {
                PrintToChat(player, GetText("lookAtSurface", player));
                return;
            }

            if (config.Enforce_Player_Portal_Limits && !permission.UserHasPermission(player.UserIDString, PORTAL_LIMIT_BYPASS_PERMISSION) && !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                var playerPortals = 0;
                foreach (var portal in CustomPortalsDict.Values)
                {
                    if (portal.PlacedBy != player.UserIDString)
                    {
                        continue;
                    }

                    playerPortals++;
                }

                var playerLimit = -1;

                foreach (var perm in permission.GetUserPermissions(player.UserIDString))
                {
                    if (!perm.StartsWith("customportals.limit."))
                    {
                        continue;
                    }

                    var limit = Convert.ToInt32(perm.Split('.')[2]);
                    if (playerLimit < limit)
                    {
                        playerLimit = limit;
                    }
                }

                if (playerLimit != -1 && playerPortals >= playerLimit)
                {
                    PrintToChat(player, string.Format(GetText("limitExceeded", player), playerLimit));
                    return;
                }
            }

            if (config.Allow_Placement_Only_In_Building_Privilege && !player.IsBuildingAuthed() && !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                PrintToChat(player, GetText("canPlaceOnlyInBuildingPrivilege", player));
                return;
            }

            if (!config.PortalPrice.TryPaying(player, PORTAL_FREE_PERMISSION, GetText("notEnoughPoints", player), GetText("notEnoughResources", player)) && !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                return;
            }

            CustomPortalComponent.Spawn(spawnHit.point, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y, 0), isManuallyPlaced: true, placedBy: player.UserIDString, placedByPlayer: player, placedByAdmin: player.IsAdmin);
        }

        #endregion

        #region API

        [HookMethod("SpawnPortal")]
        public BasePortal SpawnPortal(Vector3 position, Quaternion rotation, string name)
        {
            var portal = CustomPortalComponent.Spawn(position, rotation, name);
            return portal.GetEntity();
        }

        [HookMethod("CreatePortalItem")]
        public Item CreatePortalItem()
        {
            return CustomPortalComponent.CreateItem();
        }

        #endregion
    }
}