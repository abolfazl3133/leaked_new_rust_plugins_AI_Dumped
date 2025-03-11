using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Loadouts", "WhitePlugins.Ru", "0.1.20")]
    class Loadouts : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Economics, ImageLibrary, ServerRewards, ZoneManager;

        private LoadoutData loadoutData;
        private CostData costData;
        private Hash<string, int> itemLimits;

        private DynamicConfigFile loadout;
        private DynamicConfigFile costs;
        private DynamicConfigFile supplysignals;
        private DynamicConfigFile limits;

        private int scrapItemID;

        private Hash<ulong, CreatorData> loadoutCreators = new Hash<ulong, CreatorData>();

        private Hash<uint, LoadoutData.InventoryData> activeSupplySignals = new Hash<uint, LoadoutData.InventoryData>();

        //For FancyDrop
        private HashSet<uint> activeSupplySignalEntIds = new HashSet<uint>();

        private Hash<ItemCategory, List<ItemDefinition>> itemsByCategory = new Hash<ItemCategory, List<ItemDefinition>>();

        public static Loadouts Instance { get; private set; }

        private static GiveType giveType;

        private GiveType giveTypeAutoKit;

        private static RaycastHit raycastHit;

        private const string PERM_ITEM_SELECTOR = "loadouts.itemselector";

        private const string PERM_NO_CREATION_COOLDOWN = "loadouts.nocreationcooldown";

        private const string UI_LOADOUT_MENU = "loadouts.menu";

        private const string SUPPLYDROP_PREFAB = "assets/prefabs/misc/supply drop/supply_drop.prefab";

        private const string SMOKE_PREFAB = "assets/bundled/prefabs/fx/smoke_signal_full.prefab";

        private const string PARACHUTE_PREFAB = "assets/prefabs/misc/parachute/parachute.prefab";

        private const string HELIEXPLOSION_EFFECT = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";

        private const string BRADLEY_CANNON_EFFECT = "assets/prefabs/npc/m2bradley/effects/maincannonattack.prefab";

        private const string SUPPLYSIGNAL_ITEM = "supply.signal";

        private const ulong SUPPLYSIGNAL_SKIN = 2216930422;

        private const int RAYCAST_LAYERS = 1 << 0 | 1 << 4 | 1 << 8 | 1 << 10 | 1 << 16 | 1 << 21 | 1 << 23 | 1 << 25 | 1 << 26;

        private enum GiveType { SupplyDrop, GiveItems }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            loadout = Interface.Oxide.DataFileSystem.GetFile("Loadouts/player_loadouts");
            costs = Interface.Oxide.DataFileSystem.GetFile("Loadouts/item_costs");
            supplysignals = Interface.Oxide.DataFileSystem.GetFile("Loadouts/supply_signals");
            limits = Interface.Oxide.DataFileSystem.GetFile("Loadouts/item_limits");

            Instance = this;

            lang.RegisterMessages(Messages, this);

            permission.RegisterPermission(PERM_ITEM_SELECTOR, this);
            permission.RegisterPermission(PERM_NO_CREATION_COOLDOWN, this);

            foreach (string perm in configData.Limits.Keys)
                permission.RegisterPermission(perm, this);

            foreach (string command in configData.Commands)
                cmd.AddChatCommand(command, this, cmdLoadout);

            ExitColor = configData.Colors.Exit.ToColor();
            ProfileColor = configData.Colors.Profile.ToColor();
            HeaderColor = configData.Colors.Header.ToColor();
            PanelColor = configData.Colors.Panel.ToColor();
            BackgroundColor = configData.Colors.Background.ToColor();

            giveType = configData.GiveType.Equals("SupplyDrop", StringComparison.InvariantCultureIgnoreCase) ? GiveType.SupplyDrop : GiveType.GiveItems;
            giveTypeAutoKit = configData.AutoKitGiveType.Equals("SupplyDrop", StringComparison.InvariantCultureIgnoreCase) ? GiveType.SupplyDrop : GiveType.GiveItems;

            if (!configData.AutoKits)
            {
                Unsubscribe(nameof(CanRedeemKit));
                Unsubscribe(nameof(OnPlayerRespawned));
            }

            if (giveType != GiveType.SupplyDrop)
            {
                Unsubscribe(nameof(OnExplosiveThrown));
                Unsubscribe(nameof(OnExplosiveDropped));
                Unsubscribe(nameof(OnSupplyDropLanded));
            }
        }

        private void OnServerInitialized()
        {
			PrintWarning("\n-----------------------------\n" +
" Author - WhitePlugins.Ru\n" +
" VK - https://vk.com/rustnastroika/n" +
" Forum - https://whiteplugins.ru/n" +
" Discord - https://discord.gg/5DPTsRmd3G/n" +
"-----------------------------");
            LoadData();
            SortItemsByCategory();
        }

        private void OnExplosiveThrown(BasePlayer player, SupplySignal supplySignal, ThrownWeapon thrownWeapon) => OnExplosiveDropped(player, supplySignal, thrownWeapon);

        private void OnExplosiveDropped(BasePlayer player, SupplySignal supplySignal, ThrownWeapon thrownWeapon)
        {
            LoadoutData.InventoryData inventoryData;
            if (!activeSupplySignals.TryGetValue(thrownWeapon?.GetItem()?.uid ?? 0, out inventoryData))
                return;

            activeSupplySignals.Remove(thrownWeapon.GetItem().uid);

            if (supplySignal.IsValid())
            {
                activeSupplySignalEntIds.Add(supplySignal.net.ID);
                timer.In(1f, () => activeSupplySignalEntIds.Remove(supplySignal.net.ID));
            }

            supplySignal.CancelInvoke(supplySignal.Explode);

            ChatMessage(player, string.Format(msg("Notification.LoadoutInbound", player.UserIDString), configData.Drop.Time));

            Effect.server.Run(SMOKE_PREFAB, supplySignal, 0, Vector3.zero, Vector3.zero, null, false);

            supplySignal.SetFlag(BaseEntity.Flags.On, true, false, true);
            supplySignal.SendNetworkUpdateImmediate(false);

            supplySignal.Invoke(() => SendDrop(supplySignal, inventoryData), configData.Drop.Time);
        }

        private void OnSupplyDropLanded(SupplyDrop supplyDrop) => supplyDrop.GetComponent<DropBehaviour>()?.OnGroundCollision();

        private object CanStackItem(Item item1, Item item2)
        {
            if (item1 == null || item2 == null)
                return null;

            if (!item1.info.shortname.Equals(SUPPLYSIGNAL_ITEM))
                return null;

            if (item1.skin.Equals(SUPPLYSIGNAL_SKIN) || item2.skin.Equals(SUPPLYSIGNAL_SKIN))
                return false;

            return null;
        }

        private object CanRedeemKit(BasePlayer player)
        {
            if (!HasAnyPermission(player))
                return null;

            string loadout = loadoutData.GetRespawnLoadout(player);
            if ((string.IsNullOrEmpty(loadout) || loadoutData.GetLoadouts(player)?.Count == 0) && loadoutData.defaultLoadout == null)
                return null;

            if ((uint)Facepunch.Math.Epoch.Current - player.lifeStory.timeBorn > 5)
                return null;

            return false;
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
                return;

            if (!HasAnyPermission(player))
                return;

            LoadoutData.InventoryData inventoryData = null;
            bool isDefaultLoadout = false;

            string loadout = loadoutData.GetRespawnLoadout(player);

            if (!string.IsNullOrEmpty(loadout))
            {
                loadoutData.GetLoadouts(player)?.TryGetValue(loadout, out inventoryData);
            }

            if (loadoutData.defaultLoadout != null && inventoryData == null)
            {
                inventoryData = loadoutData.defaultLoadout;
                isDefaultLoadout = true;
            }

            if (inventoryData != null)
            {
                player.inventory.Strip();

                if (giveTypeAutoKit == GiveType.SupplyDrop)
                {
                    Item item = ItemManager.CreateByName(SUPPLYSIGNAL_ITEM, 1, SUPPLYSIGNAL_SKIN);
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);

                    activeSupplySignals.Add(item.uid, inventoryData);

                    ChatMessage(player, msg("Notification.SignalGiven", player.UserIDString));
                }
                else
                {
                    inventoryData.Give(player);
                    ChatMessage(player, isDefaultLoadout ? msg("Notification.ClaimedDefaultLoadout", player.UserIDString) : string.Format(msg("Notification.ClaimedLoadout", player.UserIDString), loadout));
                }
            }
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)            
                CuiHelper.DestroyUi(player, UI_LOADOUT_MENU);

            DropBehaviour[] activeDrops = UnityEngine.Object.FindObjectsOfType<DropBehaviour>();
            for (int i = 0; i < activeDrops.Length; i++)
                UnityEngine.Object.Destroy(activeDrops[i]);

            SaveSupplySignals();

            configData = null;
            Instance = null;
        }
        #endregion

        #region Functions
        private void SortItemsByCategory()
        {
            scrapItemID = ItemManager.FindItemDefinition("scrap").itemid;

            foreach(ItemDefinition itemDefinition in ItemManager.itemList)
            {
                if (itemDefinition.shortname.Contains("vehicle.chassis") || itemDefinition.shortname.Contains("vehicle.module"))
                    continue;

                if (configData.BlockedItems.Contains(itemDefinition.shortname))
                    continue;

                if (!configData.AllowedCategories.Contains(itemDefinition.category.ToString()) && !configData.AllowedItems.Contains(itemDefinition.shortname))
                    continue;

                if (!itemsByCategory.ContainsKey(itemDefinition.category))
                    itemsByCategory[itemDefinition.category] = new List<ItemDefinition>();

                itemsByCategory[itemDefinition.category].Add(itemDefinition);

                loadImageBuffer.Add(new KeyValuePair<string, ulong>(itemDefinition.shortname, 0UL));
            }

            foreach (KeyValuePair<ItemCategory, List<ItemDefinition>> kvp in itemsByCategory)
            {
                kvp.Value.Sort(delegate (ItemDefinition a, ItemDefinition b)
                {
                    return a.shortname.CompareTo(b.shortname);
                });
            }

            ImageLibrary.Call("LoadImageList", "Loadout item icon pre-load", loadImageBuffer, null);
            ImageLibrary.Call("AddImage", configData.DefaultIcon, DEFAULT_LOADOUT, 0UL);
        }

        private bool IsAllowedItem(ItemDefinition itemDefinition)
        {
            List<ItemDefinition> list;
            if (!itemsByCategory.TryGetValue(itemDefinition.category, out list))
                return false;

            if (!list.Contains(itemDefinition))
                return false;

            return true;
        }

        private void SendDrop(SupplySignal supplySignal, LoadoutData.InventoryData inventoryData)
        {
            Vector3 position = supplySignal.transform.position;

            position.y = 450f;

            SupplyDrop supplyDrop = GameManager.server.CreateEntity(SUPPLYDROP_PREFAB, position, Quaternion.identity) as SupplyDrop;
            supplyDrop.initialLootSpawn = false;
            supplyDrop.Spawn();
            supplyDrop.gameObject.AddComponent<DropBehaviour>().Setup(supplySignal, inventoryData);
            
            Effect.server.Run(BRADLEY_CANNON_EFFECT, supplyDrop.transform.position, Vector3.zero, null, true);

            supplyDrop.Invoke(() => Effect.server.Run(HELIEXPLOSION_EFFECT, supplyDrop.transform.position, Vector3.zero, null, true), 0.5f);
        }

        private static void ClearContainer(ItemContainer itemContainer)
        {
            if (itemContainer == null || itemContainer.itemList == null)
                return;

            while (itemContainer.itemList.Count > 0)
            {
                Item item = itemContainer.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }

        private bool ChargeForLoadout(BasePlayer player, int cost)
        {
            if (configData.Cost.Enabled)
            {
                int playerCurrency = 0;

                switch (configData.Cost.Currency)
                {
                    case "ServerRewards":
                        playerCurrency = (int)ServerRewards?.Call("CheckPoints", player.userID);
                        break;
                    case "Economics":
                        playerCurrency = Convert.ToInt32((double)Economics?.Call("Balance", player.UserIDString));
                        break;
                    case "Scrap":
                        playerCurrency = player.inventory.GetAmount(scrapItemID);
                        break;
                    default:
                        PrintError($"Invalid currency type set in config. Unable to issue loadouts!");
                        return false;
                }

                if (playerCurrency < cost)
                    return false;                

                switch (configData.Cost.Currency)
                {
                    case "ServerRewards":
                        ServerRewards?.Call("TakePoints", player.userID, cost);
                        break;
                    case "Economics":
                        Economics?.Call("Withdraw", player.UserIDString, cost);
                        break;
                    case "Scrap":
                        player.inventory.Take(null, scrapItemID, cost);
                        break;
                }
            }
            return true;
        }

        private bool InClaimZone(BasePlayer player)
        {
            if (configData.Zones.Length > 0 && ZoneManager)
            {
                bool isInZone = false;

                for (int i = 0; i < configData.Zones.Length; i++)
                {
                    if ((bool)ZoneManager.Call("IsPlayerInZone", configData.Zones[i], player))
                    {
                        isInZone = true;
                        break;
                    }
                }

                if (!isInZone)
                    return false;
            }
            return true;
        }

        private bool HasLoadoutSignal(BasePlayer player)
        {
            List<Item> list = Facepunch.Pool.GetList<Item>();
            player.inventory.AllItemsNoAlloc(ref list);

            bool hasItem = false;
            foreach (Item item in list)
            {
                if (item.info.shortname.Equals(SUPPLYSIGNAL_ITEM) && item.skin.Equals(SUPPLYSIGNAL_SKIN))
                {
                    hasItem = true;
                    break;
                }
            }

            return hasItem;
        }

        private object ShouldFancyDrop(uint netId) => activeSupplySignalEntIds.Contains(netId) ? (object)true : null;

        private int GetItemLimit(string shortname)
        {
            int limit;
            if (itemLimits.TryGetValue(shortname, out limit))
                return limit;

            return 1;
        }
        #endregion

        #region ImageLibrary
        private List<KeyValuePair<string, ulong>> loadImageBuffer = new List<KeyValuePair<string, ulong>>();

        private string GetImage(string shortname, ulong skinID = 0UL)
        {
            if (skinID != 0UL)
            {
                if (!(bool)ImageLibrary?.Call("HasImage", shortname, skinID))
                {
                    loadImageBuffer.Clear();
                    loadImageBuffer.Add(new KeyValuePair<string, ulong>(shortname, skinID));
                    ImageLibrary?.Call("LoadImageList", Title, loadImageBuffer, null);
                }
            }
            return (string)ImageLibrary?.Call("GetImage", shortname, skinID) ?? string.Empty;
        }
        #endregion

        #region Helpers       
        private static void ChatMessage(BasePlayer player, string text) => player.ChatMessage(msg("Message.Prefix", player.UserIDString) + text);

        private bool HasAnyPermission(BasePlayer player) => configData.Limits.Keys.Any(x => permission.UserHasPermission(player.UserIDString, x));

        private int CurrentProfileCount(BasePlayer player) => loadoutData.GetLoadouts(player)?.Count ?? 0;

        private int MaxAllowedProfiles(BasePlayer player)
        {
            int max = 0;

            foreach(KeyValuePair<string, ConfigData.Limitations> kvp in configData.Limits)
            {
                if (permission.UserHasPermission(player.UserIDString, kvp.Key) && kvp.Value.MaxProfiles > max)
                    max = kvp.Value.MaxProfiles;
            }

            return max;
        }

        private static int GetCooldownTime(BasePlayer player)
        {
            int time = int.MaxValue;

            foreach (KeyValuePair<string, ConfigData.Limitations> kvp in configData.Limits)
            {
                if (Instance.permission.UserHasPermission(player.UserIDString, kvp.Key) && kvp.Value.Cooldown < time)
                    time = kvp.Value.Cooldown;
            }

            return time;
        }

        private static int GetCreationCooldownTime(BasePlayer player)
        {
            int time = int.MaxValue;

            foreach (KeyValuePair<string, ConfigData.Limitations> kvp in configData.Limits)
            {
                if (Instance.permission.UserHasPermission(player.UserIDString, kvp.Key) && kvp.Value.CreateCooldown < time)
                    time = kvp.Value.CreateCooldown;
            }

            return time;
        }

        private static ConfigData.Limitations GetLimitations(BasePlayer player)
        {
            ConfigData.Limitations limits = null;
            foreach (KeyValuePair<string, ConfigData.Limitations> kvp in configData.Limits)
            {
                if (Instance.permission.UserHasPermission(player.UserIDString, kvp.Key))
                    limits = kvp.Value;
            }
            return limits;
        }

        private bool HasSpace(ItemContainer itemContainer, int count) => itemContainer.capacity - itemContainer.itemList.Count >= count;

        private static double CurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours;
            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;

            if (days > 0)
                return string.Format("{0:00}d:{1:00}h:{2:00}m:{3:00}s", days, hours, mins, secs);
            else if (hours > 0)
                return string.Format("{0:00}h:{1:00}m:{2:00}s", hours, mins, secs);
            else if (mins > 0)
                return string.Format("{0:00}m:{1:00}s", mins, secs);
            else return string.Format("{0}s", secs);
        }
        #endregion

        #region Drop Component
        private class DropBehaviour : MonoBehaviour
        {
            private SupplyDrop entity;
            private Rigidbody rb;
            private Transform tr;

            private float heightAtPosition;
            private float distToTarget;
            private bool hasDeployedChute = false;

            private SupplySignal supplySignal;

            private BaseEntity parachute;
            private Vector3 velocityEnter;

            private void Awake()
            {
                entity = GetComponent<SupplyDrop>();
                rb = GetComponent<Rigidbody>();
                tr = entity.transform;
            }

            private void Start()
            {                
                entity.RemoveParachute();                

                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = 1.25f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.drag = 0.1f;
                rb.angularDrag = 0.1f;
                rb.AddForce(Vector3.down * configData.Drop.Velocity, ForceMode.Impulse);

                if (Physics.Raycast(tr.position + Vector3.down, Vector3.down, out raycastHit, 500f, RAYCAST_LAYERS, QueryTriggerInteraction.Collide))
                    heightAtPosition = raycastHit.point.y;
                else heightAtPosition = TerrainMeta.HeightMap.GetHeight(tr.position);
            }

            internal void Setup(SupplySignal supplySignal, LoadoutData.InventoryData inventoryData)
            {
                this.supplySignal = supplySignal;

                entity.Invoke(()=> inventoryData.Give(entity), 2f);
            }

            private void Update()
            {
                distToTarget = tr.position.y - heightAtPosition;

                if (distToTarget < configData.Drop.DeployDistance)
                {
                    if (!hasDeployedChute)
                    {
                        parachute = GameManager.server.CreateEntity(PARACHUTE_PREFAB, tr.position, Quaternion.identity);
                        parachute.SetParent(entity, "parachute_attach", false, false);
                        parachute.transform.localPosition = Vector3.zero;
                        parachute.transform.localRotation = Quaternion.identity;
                        parachute.enableSaving = false;
                        parachute.Spawn();

                        velocityEnter = rb.velocity;

                        hasDeployedChute = true;
                    }

                    rb.velocity = Vector3.Lerp(velocityEnter, Vector3.zero, 1f - Mathf.InverseLerp(0f, configData.Drop.DeployDistance, distToTarget));
                }
            }

            internal void OnGroundCollision()
            {
                supplySignal?.Kill(BaseNetworkable.DestroyMode.None);
                parachute?.Kill(BaseNetworkable.DestroyMode.None);

                Destroy(this);
            }
        }
        #endregion

        #region UI         
        public static class UI
        {
            public static CuiElementContainer Container(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName.ToString()
                    }
                };
                return container;
            }

            public static CuiElementContainer BlurContainer(string panelName, UI4 dimensions, string color = "0 0 0 0.55")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = true
                        },
                        new CuiElement().Parent = "Hud",
                        panelName.ToString()
                    }
                };
                return container;
            }

            public static void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel.ToString());
            }

            public static void BlurPanel(ref CuiElementContainer container, string panel, UI4 dimensions, string color = "0 0 0 0.5")
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = false
                },
                panel, CuiHelper.GetGuid());
            }

            public static void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter, FontStyle fontStyle = FontStyle.RobotoCondensed)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = ToFontString(fontStyle) },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel.ToString());

            }

            public static void Button(ref CuiElementContainer container, string panel, string color, string text, int size, UI4 dimensions, string command, TextAnchor align = TextAnchor.MiddleCenter, FontStyle fontStyle = FontStyle.RobotoCondensed)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    Text = { Text = text, FontSize = size, Align = align, Font = ToFontString(fontStyle) }
                },
                panel.ToString());
            }

            public static void Image(ref CuiElementContainer container, string panel, string png, UI4 dimensions)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent { Png = png },
                        new CuiRectTransformComponent { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                });
            }

            public static void Input(ref CuiElementContainer container, string panel, string text, int size, string command, UI4 dimensions)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 300,
                            Command = command,
                            FontSize = size,
                            IsPassword = false,
                            Text = text
                        },
                        new CuiRectTransformComponent {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                });
            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }

            public enum FontStyle { DroidSansMono, RobotoCondensed, RobotoCondensedBold, PermanantMarker }

            private static string ToFontString(FontStyle fontStyle)
            {
                switch (fontStyle)
                {
                    case FontStyle.DroidSansMono:
                        return "droidsansmono.ttf";
                    case FontStyle.PermanantMarker:
                        return "permanentmarker.ttf";
                    case FontStyle.RobotoCondensed:                    
                        return "robotocondensed-regular.ttf";
                    case FontStyle.RobotoCondensedBold:
                    default:
                        return "robotocondensed-bold.ttf";
                }
            }
        }

        public class UI4
        {
            [JsonProperty(PropertyName = "Left (0.0 - 1.0)")]
            public float xMin;

            [JsonProperty(PropertyName = "Bottom (0.0 - 1.0)")]
            public float yMin;

            [JsonProperty(PropertyName = "Right (0.0 - 1.0)")]
            public float xMax;

            [JsonProperty(PropertyName = "Top (0.0 - 1.0)")]
            public float yMax;

            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }

            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Methods     
        public string ExitColor { get; private set; }
        public string ProfileColor { get; private set; }
        public string HeaderColor { get; private set; }
        public string PanelColor { get; private set; }
        public string BackgroundColor { get; private set; }

        public const string COLOR_INVISIBLE = "0 0 0 0";

        public const string DEFAULT_LOADOUT = "loadouts-default";

        private string _defaultLoadoutIcon;

        private string DefaultLoadoutIcon
        {
            get
            {
                if (string.IsNullOrEmpty(_defaultLoadoutIcon))
                    _defaultLoadoutIcon = GetImage(DEFAULT_LOADOUT, 0UL);

                return _defaultLoadoutIcon;
            }
        }

        #region Loadout Menu
        private void OpenLoadoutMenu(BasePlayer player, string loadout, int page = 0)
        {
            CuiElementContainer container = UI.BlurContainer(UI_LOADOUT_MENU, new UI4(0.3f, 0.275f, 0.7185f, 0.725f), BackgroundColor);

            Hash<string, LoadoutData.InventoryData> loadouts = loadoutData.GetLoadouts(player);

            // Header
            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.005f, 0.9f, 0.995f, 0.99f));
            UI.Label(ref container, UI_LOADOUT_MENU, string.IsNullOrEmpty(loadout) ? msg("UI.Title", player.UserIDString) : string.Format(msg("UI.Title.Name", player.UserIDString), loadout), 18, new UI4(0.01f, 0.9f, 0.995f, 0.99f), TextAnchor.MiddleLeft, UI.FontStyle.RobotoCondensedBold);
            UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, "✘", 12, new UI4(0.95f, 0.91f, 0.99f, 0.98f), "loadoutui.close");

            // Loadout Selection
            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.005f, 0.01f, 0.3f, 0.89f));
            UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.005f, 0.83f, 0.3f, 0.89f));

            UI.Label(ref container, UI_LOADOUT_MENU, msg("UI.Profiles", player.UserIDString), 12, new UI4(0.01f, 0.83f, 0.3f, 0.89f), TextAnchor.MiddleLeft, UI.FontStyle.RobotoCondensed);
                        
            UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, msg("UI.CreateLoadout", player.UserIDString), 12, new UI4(0.01f, 0.76f, 0.295f, 0.82f), "loadoutui.create", TextAnchor.MiddleCenter);

            if (loadouts != null)
            {
                const float loadoutMin = 0.69f;
                const float loadoutHeight = 0.06f;
                const float loadoutSpacing = 0.01f;

                int index = page * 10;
                int limit = Mathf.Min((page + 1) * 10, loadouts.Count);

                string lastClaimedLoadout = loadoutData.GetRespawnLoadout(player);

                for (int i = index; i < limit; i++)
                {
                    float yMin = loadoutMin - ((loadoutHeight + loadoutSpacing) * (i - index));
                    string loadoutName = loadouts.ElementAt(i).Key;

                    UI.Button(ref container, UI_LOADOUT_MENU, ProfileColor, loadoutName, 12, new UI4(0.01f, yMin, 0.295f, yMin + loadoutHeight), $"loadoutui.open {loadouts.ElementAt(i).Key} {page}", TextAnchor.MiddleCenter);

                    if (!string.IsNullOrEmpty(lastClaimedLoadout) && loadoutName.Equals(lastClaimedLoadout))                                            
                        UI.Image(ref container, UI_LOADOUT_MENU, DefaultLoadoutIcon, new UI4(0.26f, yMin, 0.295f, yMin + loadoutHeight));                    
                }

                if (page > 0)
                    UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, "< < <", 12, new UI4(0.01f, 0.01f, 0.145f, 0.05f), $"loadoutui.open {loadout} {page -1}");
                if (limit < loadouts.Count)
                    UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, "> > >", 12, new UI4(0.15f, 0.01f, 0.295f, 0.05f), $"loadoutui.open {loadout} {page + 1}");

                // Loadout Viewer
                if (!string.IsNullOrEmpty(loadout))
                {
                    LoadoutData.InventoryData inventoryData = loadouts[loadout];

                    UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.31f, 0.01f, 0.995f, 0.89f));

                    //Belt
                    UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.31f, 0.83f, 0.995f, 0.89f));
                    UI.Label(ref container, UI_LOADOUT_MENU, msg("Container.Belt", player.UserIDString), 12, new UI4(0.315f, 0.83f, 0.995f, 0.89f), TextAnchor.MiddleLeft);
                    LayoutProfileItems(ref container, UI_LOADOUT_MENU, inventoryData.containerBelt, 0.695f);
                    
                    //Wear
                    UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.31f, 0.625f, 0.995f, 0.685f));
                    UI.Label(ref container, UI_LOADOUT_MENU, msg("Container.Wear", player.UserIDString), 12, new UI4(0.315f, 0.625f, 0.995f, 0.685f), TextAnchor.MiddleLeft);
                    LayoutProfileItems(ref container, UI_LOADOUT_MENU, inventoryData.containerWear, 0.49f);
                    
                    //Main
                    UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.31f, 0.42f, 0.995f, 0.48f));
                    UI.Label(ref container, UI_LOADOUT_MENU, msg("Container.Main", player.UserIDString), 12, new UI4(0.315f, 0.42f, 0.995f, 0.48f), TextAnchor.MiddleLeft);
                    LayoutProfileItems(ref container, UI_LOADOUT_MENU, inventoryData.containerMain, 0.285f);

                    //Spawn Helper
                    UI.BlurPanel(ref container, UI_LOADOUT_MENU, new UI4(0f, -0.07f, 1f, 0f), BackgroundColor);

                    if (inventoryData.IsOnCooldown())
                        UI.Button(ref container, UI_LOADOUT_MENU, HeaderColor, string.Format(msg("UI.Cooldown", player.UserIDString), FormatTime(inventoryData.CooldownRemaining)), 12, new UI4(0.71f, -0.06f, 0.995f, 0f), "");
                    else
                    {
                        if (configData.Cost.Enabled)
                            UI.Button(ref container, UI_LOADOUT_MENU, HeaderColor, string.Format(msg("UI.Claim.Cost", player.UserIDString), inventoryData.GetCost(player), msg("Currency."+configData.Cost.Currency, player.UserIDString)), 12, new UI4(0.71f, -0.06f, 0.995f, 0f), $"loadoutui.claim {loadout}");
                        else UI.Button(ref container, UI_LOADOUT_MENU, HeaderColor, msg("UI.Claim", player.UserIDString), 12, new UI4(0.71f, -0.06f, 0.995f, 0f), $"loadoutui.claim {loadout}");
                    }

                    UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, msg("UI.Delete", player.UserIDString), 12, new UI4(0.415f, -0.06f, 0.7f, 0f), $"loadoutui.delete {loadout}");

                    if (configData.AutoKits)
                        UI.Button(ref container, UI_LOADOUT_MENU, HeaderColor, !string.IsNullOrEmpty(lastClaimedLoadout) && lastClaimedLoadout.Equals(loadout) ? msg("UI.RemoveDefault", player.UserIDString) : 
                            msg("UI.SetDefault", player.UserIDString), 12, new UI4(0.005f, -0.06f, 0.295f, 0f), $"loadoutui.setdefault {loadout}");
                }
            }

            // Send to client
            CuiHelper.DestroyUi(player, UI_LOADOUT_MENU);
            CuiHelper.AddUi(player, container);
        }

        private void LayoutProfileItems(ref CuiElementContainer container, string panel, LoadoutData.InventoryData.ItemData[] items, float yMin, float xMin = 0.315f, float width = 0.08f, float height = 0.125f)
        {
            for (int i = 0; i < items.Length; i++)
            {
                float x = xMin + ((width + 0.005f) * (i >= 16 ? i - 16 : i >= 8 ? i - 8 : i));
                float y = i >= 16 ? yMin - 0.26f : i >= 8 ? yMin - 0.13f : yMin;

                UI4 pos = new UI4(x, y, x + width, y + height);
                UI.BlurPanel(ref container, panel, pos);

                LoadoutData.InventoryData.ItemData item = items[i];
                if (item != null)
                {
                    UI.Image(ref container, panel, GetImage(item.shortname, item.skin), pos);

                    if (item.amount > 1)
                        UI.Label(ref container, panel, $"x{item.amount}", 8, pos, TextAnchor.LowerRight);
                }                              
            }            
        }
        #endregion

        #region Creator Menu
        private void OpenCreationMenu(BasePlayer player, string loadout)
        {
            CuiElementContainer container = UI.BlurContainer(UI_LOADOUT_MENU, new UI4(0.355f, 0.275f, 0.645f, 0.725f), BackgroundColor);

            CreatorData creatorData;
            if (!loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                creatorData = loadoutCreators[player.userID] = new CreatorData(player);

                if (!creatorData.CanItemSelect)
                    creatorData.Inventory.CopyItemsFrom(player);
            }

            // Header
            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.005f, 0.9f, 0.99f, 0.99f));
            UI.Label(ref container, UI_LOADOUT_MENU, creatorData.IsDefaultLoadout ? msg("Creator.Title.DefaultLoadout", player.UserIDString) : msg("Creator.Title", player.UserIDString), 18, new UI4(0.02f, 0.9f, 0.99f, 0.99f), TextAnchor.MiddleLeft, UI.FontStyle.RobotoCondensedBold);
            UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, "✘", 12, new UI4(0.92f, 0.91f, 0.985f, 0.98f), "loadoutui.open 0");

            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.01f, 0.01f, 0.99f, 0.89f));

            //Belt
            UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.01f, 0.83f, 0.99f, 0.89f));
            UI.Label(ref container, UI_LOADOUT_MENU, msg("Container.Belt", player.UserIDString), 12, new UI4(0.02f, 0.83f, 0.99f, 0.89f), TextAnchor.MiddleLeft);
            LayoutCreatorProfileButtons(ref container, UI_LOADOUT_MENU, creatorData.Inventory.containerBelt, ContainerType.Belt, creatorData.CanItemSelect, 0.695f);

            //Wear
            UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.01f, 0.625f, 0.99f, 0.685f));
            UI.Label(ref container, UI_LOADOUT_MENU, msg("Container.Wear", player.UserIDString), 12, new UI4(0.02f, 0.625f, 0.99f, 0.685f), TextAnchor.MiddleLeft);
            LayoutCreatorProfileButtons(ref container, UI_LOADOUT_MENU, creatorData.Inventory.containerWear, ContainerType.Wear, creatorData.CanItemSelect, 0.49f);

            //Main
            UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.01f, 0.42f, 0.99f, 0.48f));
            UI.Label(ref container, UI_LOADOUT_MENU, msg("Container.Main", player.UserIDString), 12, new UI4(0.02f, 0.42f, 0.99f, 0.48f), TextAnchor.MiddleLeft);
            LayoutCreatorProfileButtons(ref container, UI_LOADOUT_MENU, creatorData.Inventory.containerMain, ContainerType.Main, creatorData.CanItemSelect, 0.285f);

            //Save Helper
            UI.BlurPanel(ref container, UI_LOADOUT_MENU, new UI4(0f, -0.14f, 1f, 0f), BackgroundColor);

            if (configData.Cost.Enabled)
                UI.Label(ref container, UI_LOADOUT_MENU, string.Format(msg("Creator.Costs", player.UserIDString), creatorData.Inventory.CalculateCosts(creatorData.Limits.CostMultiplier), msg("Currency." + configData.Cost.Currency, player.UserIDString)), 12, new UI4(0.02f, -0.13f, 0.99f, -0.07f), TextAnchor.MiddleLeft);

            UI.Button(ref container, UI_LOADOUT_MENU, HeaderColor, msg("Creator.Save", player.UserIDString), 12, new UI4(0.7f, -0.13f, 0.99f, -0.07f), $"loadoutcreator.save {loadout}");

            UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, msg("Creator.CopyInv", player.UserIDString), 12, new UI4(0.396f, -0.13f, 0.686f, -0.07f), "loadoutcreator.copyinv");

            //Input Field Fuckery
            if (!creatorData.IsDefaultLoadout)
            {
                UI.Panel(ref container, UI_LOADOUT_MENU, "0 0 0 0.7", new UI4(0.235f, -0.06f, 0.99f, 0f));
                UI.Label(ref container, UI_LOADOUT_MENU, msg("Creator.Profile", player.UserIDString), 12, new UI4(0.02f, -0.06f, 0.25f, 0f), TextAnchor.MiddleLeft);
                if (string.IsNullOrEmpty(creatorData.Name))
                    UI.Input(ref container, UI_LOADOUT_MENU, string.Empty, 12, "loadoutcreator.setname", new UI4(0.25f, -0.06f, 0.99f, 0f));
                else
                {
                    UI.Label(ref container, UI_LOADOUT_MENU, creatorData.Name, 12, new UI4(0.25f, -0.06f, 0.99f, 0f), TextAnchor.MiddleLeft);
                    UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, "X", 12, new UI4(0.925f, -0.06f, 0.99f, 0f), "loadoutcreator.clearname");
                }
            }

            CuiHelper.DestroyUi(player, UI_LOADOUT_MENU);
            CuiHelper.AddUi(player, container);
        }

        private void LayoutCreatorProfileButtons(ref CuiElementContainer container, string panel, LoadoutData.InventoryData.ItemData[] items, ContainerType containerType, bool canSelectItems, float yMin, float xMin = 0.02f, float width = 0.1125f, float height = 0.125f)
        {
            for (int i = 0; i < items.Length; i++)
            {
                float x = xMin + ((width + 0.007f) * (i >= 16 ? i - 16 : i >= 8 ? i - 8 : i));
                float y = i >= 16 ? yMin - 0.26f : i >= 8 ? yMin - 0.13f : yMin;

                UI4 pos = new UI4(x, y, x + width, y + height);
                UI.BlurPanel(ref container, panel, pos);

                LoadoutData.InventoryData.ItemData item = items[i];
                if (item != null)
                {
                    UI.Image(ref container, panel, GetImage(item.shortname, item.skin), pos);

                    if (item.amount > 1)
                        UI.Label(ref container, panel, $"x{item.amount}", 8, pos, TextAnchor.LowerRight);
                }

                if (canSelectItems)
                {
                    UI.Button(ref container, panel, COLOR_INVISIBLE, string.Empty, 0, pos, $"loadoutcreator.chooseitem {(int)containerType} {i}");

                    if (item != null)
                    {
                        ItemDefinition itemDefinition = ItemManager.itemDictionaryByName[item.shortname];
                        int maxStackable = itemDefinition.category == ItemCategory.Weapon || itemDefinition.category == ItemCategory.Attire ? 1 : itemDefinition.stackable;

                        if (maxStackable > 1)
                            UI.Button(ref container, panel, ProfileColor, "+", 10, new UI4(pos.xMax - (width * 0.25f), pos.yMax - (height * 0.25f), pos.xMax, pos.yMax), $"loadoutcreator.changeamount {(int)containerType} {i} {maxStackable} {item.shortname} {item.amount}");
                    }
                }
            }
        }

        private void OpenItemAmountMenu(BasePlayer player, string loadout, string shortname, int current, int max)
        {
            CuiElementContainer container = UI.BlurContainer(UI_LOADOUT_MENU, new UI4(0.355f, 0.4f, 0.645f, 0.6f), BackgroundColor);

            CreatorData creatorData;
            if (!loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                creatorData = loadoutCreators[player.userID] = new CreatorData(player);

                if (!creatorData.CanItemSelect)
                    creatorData.Inventory.CopyItemsFrom(player);
            }

            // Header
            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.005f, 0.775f, 0.99f, 0.975f));
            UI.Label(ref container, UI_LOADOUT_MENU, creatorData.IsDefaultLoadout ? msg("Creator.Title.DefaultLoadout", player.UserIDString) : msg("Creator.Title", player.UserIDString), 18, new UI4(0.02f, 0.775f, 0.99f, 0.975f), TextAnchor.MiddleLeft, UI.FontStyle.RobotoCondensedBold);
            UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, "✘", 12, new UI4(0.92f, 0.8f, 0.985f, 0.955f), "loadoutcreator.setamount 0");

            UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.01f, 0.6f, 0.99f, 0.75f));
            UI.Label(ref container, UI_LOADOUT_MENU, string.Format("Set amount for item : {0}", ItemManager.itemDictionaryByName[shortname].displayName.english), 12, new UI4(0.02f, 0.60f, 0.99f, 0.75f), TextAnchor.MiddleLeft);
                
            //Input Field Fuckery
            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.01f, 0.025f, 0.99f, 0.6f));

            UI.Label(ref container, UI_LOADOUT_MENU, msg("Creator.Current", player.UserIDString), 12, new UI4(0.02f, 0.43f, 0.25f, 0.53f), TextAnchor.MiddleLeft);
            UI.Label(ref container, UI_LOADOUT_MENU, current.ToString(), 12, new UI4(0.175f, 0.43f, 0.9f, 0.53f), TextAnchor.MiddleLeft);

            UI.Label(ref container, UI_LOADOUT_MENU, msg("Creator.Max", player.UserIDString), 12, new UI4(0.02f, 0.31f, 0.25f, 0.41f), TextAnchor.MiddleLeft);
            UI.Label(ref container, UI_LOADOUT_MENU, max.ToString(), 12, new UI4(0.175f, 0.31f, 0.9f, 0.41f), TextAnchor.MiddleLeft);

            UI.Label(ref container, UI_LOADOUT_MENU, msg("Creator.Amount", player.UserIDString), 12, new UI4(0.02f, 0.15f, 0.25f, 0.3f), TextAnchor.MiddleLeft);

            UI.Panel(ref container, UI_LOADOUT_MENU, "0 0 0 0.7", new UI4(0.15f, 0.15f, 0.98f, 0.3f));
            UI.Input(ref container, UI_LOADOUT_MENU, string.Empty, 12, "loadoutcreator.setamount", new UI4(0.175f, 0.15f, 0.98f, 0.3f));
            
            CuiHelper.DestroyUi(player, UI_LOADOUT_MENU);
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Item Selector Menu
        private void OpenItemSelectionMenu(BasePlayer player, ItemCategory category, ContainerType containerType, int slot)
        {
            CuiElementContainer container = UI.BlurContainer(UI_LOADOUT_MENU, new UI4(0.25f, 0.275f, 0.85f, 0.725f), BackgroundColor);

            // Header
            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.004f, 0.9f, 0.996f, 0.99f));
            UI.Label(ref container, UI_LOADOUT_MENU, msg("ItemSelector.Title", player.UserIDString), 18, new UI4(0.014f, 0.9f, 0.99f, 0.99f), TextAnchor.MiddleLeft, UI.FontStyle.RobotoCondensedBold);
            UI.Button(ref container, UI_LOADOUT_MENU, ExitColor, "✘", 12, new UI4(0.965f, 0.91f, 0.994f, 0.98f), "loadoutcreator.cancelitemselect");

            UI.Panel(ref container, UI_LOADOUT_MENU, PanelColor, new UI4(0.004f, 0.01f, 0.996f, 0.89f));
                        
            UI.Panel(ref container, UI_LOADOUT_MENU, HeaderColor, new UI4(0.004f, 0.83f, 0.996f, 0.89f));

            if (containerType != ContainerType.Wear)
            {
                float width = Mathf.Min(0.994f / itemsByCategory.Count, 0.1f);

                for (int i = 0; i < itemsByCategory.Count; i++)
                {
                    ItemCategory itemCategory = itemsByCategory.Keys.ElementAt(i);

                    float x = 0.004f + (width * i);

                    UI4 pos = new UI4(x, 0.83f, x + width, 0.89f);

                    if (itemCategory == category)
                    {
                        UI.Panel(ref container, UI_LOADOUT_MENU, "0 0 0 0.5", pos);
                        UI.Label(ref container, UI_LOADOUT_MENU, itemCategory.ToString(), 10, pos);
                    }
                    else UI.Button(ref container, UI_LOADOUT_MENU, COLOR_INVISIBLE, itemCategory.ToString(), 10, pos, $"loadoutcreator.changecategory {(int)itemCategory} {(int)containerType} {slot}");
                }
            }

            LayoutSelectorItems(ref container, UI_LOADOUT_MENU, containerType == ContainerType.Wear ? ItemCategory.Attire : category, containerType, slot, 1, 0.69f);

            CuiHelper.DestroyUi(player, UI_LOADOUT_MENU);
            CuiHelper.AddUi(player, container);
        }

        private void LayoutSelectorItems(ref CuiElementContainer container, string panel, ItemCategory category, ContainerType containerType, int slot, int amount, float yMin, float xMin = 0.01f, float width = 0.0525f, float height = 0.125f)
        {
            List<ItemDefinition> items;
            if (!itemsByCategory.TryGetValue(category, out items))
                return;

            int i = 0;
            foreach (ItemDefinition itemDefinition in items)
            {                
                int columnNumber = i == 0 ? 0 : Mathf.FloorToInt(i / 17f);
                int rowNumber = i - (columnNumber * 17);

                float x = xMin + ((width + 0.005f) * rowNumber);
                float y = yMin - ((height + 0.01f) * columnNumber);

                UI4 pos = new UI4(x, y, x + width, y + height);

                UI.BlurPanel(ref container, panel, pos);
                UI.Image(ref container, panel, GetImage(itemDefinition.shortname, 0UL), pos);
                UI.Button(ref container, panel, COLOR_INVISIBLE, string.Empty, 0, pos, $"loadoutcreator.selectitem {(int)containerType} {slot} {itemDefinition.shortname} {amount}");
                i++;
            }
        }

        public enum ContainerType { Main, Belt, Wear }
        #endregion

        #endregion

        #region UI Commands
        [ConsoleCommand("loadoutcreator.chooseitem")]
        private void ccmdChooseItem(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                OpenItemSelectionMenu(player, itemsByCategory.First().Key, (ContainerType)arg.GetInt(0), arg.GetInt(1));
            }
        }

        [ConsoleCommand("loadoutcreator.changeamount")]
        private void ccmdChangeItemAmount(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                creatorData.amountContainerType = (ContainerType)arg.GetInt(0);
                creatorData.amountIndex = arg.GetInt(1);
                creatorData.amountMax = arg.GetInt(2);

                string shortname = arg.GetString(3);

                int limit = GetItemLimit(shortname);
                int current = creatorData.Inventory.FindItemAmount(shortname);

                OpenItemAmountMenu(player, creatorData.Name, shortname, arg.GetInt(4), Mathf.Min(creatorData.amountMax, limit - current));
            }
        }

        [ConsoleCommand("loadoutcreator.setamount")]
        private void ccmdSetItemAmount(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                int amount = arg.GetInt(0);

                if (amount > 0)
                {
                    LoadoutData.InventoryData.ItemData[] inventory = creatorData.amountContainerType == ContainerType.Belt ? creatorData.Inventory.containerBelt :
                                                                     creatorData.amountContainerType == ContainerType.Wear ? creatorData.Inventory.containerWear : creatorData.Inventory.containerMain;

                    LoadoutData.InventoryData.ItemData itemData = inventory[creatorData.amountIndex];
                    if (itemData != null)                    
                        itemData.amount = Mathf.Clamp(Mathf.Min(amount, GetItemLimit(itemData.shortname)), 1, creatorData.amountMax);                    
                }

                OpenCreationMenu(player, creatorData.Name);
            }
        }

        [ConsoleCommand("loadoutcreator.selectitem")]
        private void ccmdSelectItem(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                LoadoutData.InventoryData.ItemData[] itemList = (ContainerType)arg.GetInt(0) == ContainerType.Belt ? creatorData.Inventory.containerBelt :
                                                                (ContainerType)arg.GetInt(0) == ContainerType.Wear ? creatorData.Inventory.containerWear : creatorData.Inventory.containerMain;

                string shortname = arg.GetString(2);
                int limit = GetItemLimit(shortname);
                int current = creatorData.Inventory.FindItemAmount(shortname);

                if (current < limit)
                {
                    itemList[arg.GetInt(1)] = new LoadoutData.InventoryData.ItemData()
                    {
                        shortname = shortname,
                        amount = arg.GetInt(3),
                        position = arg.GetInt(1)
                    };
                }
                else ChatMessage(player, msg("Notification.MaxAmount", player.UserIDString));
               
                OpenCreationMenu(player, creatorData.Name);
            }
        }

        [ConsoleCommand("loadoutcreator.cancelitemselect")]
        private void ccmdCancelSelectItem(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                OpenCreationMenu(player, creatorData.Name);                
            }
        }

        [ConsoleCommand("loadoutcreator.changecategory")]
        private void ccmdChangeCategory(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
                OpenItemSelectionMenu(player, (ItemCategory)arg.GetInt(0), (ContainerType)arg.GetInt(1), arg.GetInt(2));
        }

        [ConsoleCommand("loadoutcreator.clearname")]
        private void ccmdCreatorClearName(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                creatorData.Name = string.Empty;
                OpenCreationMenu(player, creatorData.Name);
            }            
        }

        [ConsoleCommand("loadoutcreator.setname")]
        private void ccmdCreatorSetName(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                string name = string.Join("", arg.Args);
                if (!string.IsNullOrEmpty(name))
                    creatorData.Name = name;
                OpenCreationMenu(player, creatorData.Name);
            }
        }

        [ConsoleCommand("loadoutcreator.copyinv")]
        private void ccmdCopyInventory(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                creatorData.Inventory.CopyItemsFrom(player);
                OpenCreationMenu(player, creatorData.Name);
            }
        }

        [ConsoleCommand("loadoutcreator.save")]
        private void ccmdSaveProfile(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CreatorData creatorData;
            if (loadoutCreators.TryGetValue(player.userID, out creatorData))
            {
                string name = string.Empty;
                if (!creatorData.IsDefaultLoadout)
                {
                    if (string.IsNullOrEmpty(creatorData.Name))
                    {
                        ChatMessage(player, msg("Notification.NoProfileName", player.UserIDString));
                        return;
                    }

                    name = creatorData.Name.Replace(" ", "");

                    Hash<string, LoadoutData.InventoryData> loadouts;
                    loadoutData.GetLoadouts(player, out loadouts);

                    if (loadouts.ContainsKey(name))
                    {
                        ChatMessage(player, msg("Notification.ProfileNameExists", player.UserIDString));
                        return;
                    }

                    loadouts[name] = creatorData.Inventory;

                    loadoutData.OnLoadoutCreated(player, CurrentTime() + GetCreationCooldownTime(player));
                }
                else
                {
                    name = "Default Loadout";
                    loadoutData.defaultLoadout = creatorData.Inventory;
                }

                SaveData();

                ChatMessage(player, string.Format(msg("Notification.SavedLoadout", player.UserIDString), name));

                loadoutCreators.Remove(player.userID);
                OpenLoadoutMenu(player, string.Empty);
            }
        }

        [ConsoleCommand("loadoutui.close")]
        private void ccmdCloseLoadoutUI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            loadoutCreators.Remove(player.userID);

            CuiHelper.DestroyUi(player, UI_LOADOUT_MENU);
        }
                
        [ConsoleCommand("loadoutui.claim")]
        private void ccmdSpawnLoadout(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            Hash<string, LoadoutData.InventoryData> loadouts = loadoutData.GetLoadouts(player);

            LoadoutData.InventoryData inventoryData;
            if (loadouts.TryGetValue(arg.GetString(0), out inventoryData))
            {
                if (giveType == GiveType.GiveItems && (!HasSpace(player.inventory.containerBelt, inventoryData.BeltCount) || 
                                                       !HasSpace(player.inventory.containerWear, inventoryData.WearCount) || 
                                                       !HasSpace(player.inventory.containerMain, inventoryData.MainCount)))
                {
                    ChatMessage(player, msg("Notification.NoFreeSlots", player.UserIDString));
                    return;
                }

                if (!InClaimZone(player))
                {
                    ChatMessage(player, msg("Notification.DenyClaimArea", player.UserIDString));
                    return;
                }

                object canClaim = Interface.CallHook("CanClaimLoadout", player);
                if (canClaim != null)
                {
                    ChatMessage(player, msg("Notification.DenyClaimArea", player.UserIDString));
                    return;
                }

                if (giveType == GiveType.SupplyDrop && configData.Drop.OnlySingleDrop && HasLoadoutSignal(player))
                {
                    ChatMessage(player, msg("Notification.HasLoadoutSignal", player.UserIDString));
                    return;
                }

                if (!ChargeForLoadout(player, inventoryData.GetCost(player)))
                {
                    ChatMessage(player, string.Format(msg("Notification.NoEnoughFunds", player.UserIDString), msg("Currency." + configData.Cost.Currency, player.UserIDString)));
                    return;
                }

                if (inventoryData.IsOnCooldown())
                {
                    ChatMessage(player, string.Format(msg("Notification.Cooldown", player.UserIDString), FormatTime(inventoryData.CooldownRemaining)));
                    return;
                }

                inventoryData.nextUsageTime = CurrentTime() + GetCooldownTime(player);

                if (string.IsNullOrEmpty(loadoutData.GetRespawnLoadout(player)))
                    loadoutData.SetRespawnLoadout(player, arg.GetString(0));

                if (giveType == GiveType.SupplyDrop)
                {                    
                    Item item = ItemManager.CreateByName(SUPPLYSIGNAL_ITEM, 1, SUPPLYSIGNAL_SKIN);
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);

                    activeSupplySignals.Add(item.uid, inventoryData);

                    ChatMessage(player, msg("Notification.SignalGiven", player.UserIDString));
                }
                else
                {
                    inventoryData.Give(player);
                    ChatMessage(player, string.Format(msg("Notification.ClaimedLoadout", player.UserIDString), arg.GetString(0)));
                }

                CuiHelper.DestroyUi(player, UI_LOADOUT_MENU);
                return;
            }
        }
                
        [ConsoleCommand("loadoutui.delete")]
        private void ccmdDeleteLoadout(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            Hash<string, LoadoutData.InventoryData> loadouts = loadoutData.GetLoadouts(player);

            if (loadouts.ContainsKey(arg.GetString(0)))
            {
                loadouts.Remove(arg.GetString(0));
                SaveData();
            }

            OpenLoadoutMenu(player, string.Empty);
        }

        [ConsoleCommand("loadoutui.setdefault")]
        private void ccmdSetDefaultLoadout(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            string loadout = arg.GetString(0);
            string currentRespawnLoadout = loadoutData.GetRespawnLoadout(player);

            if (!string.IsNullOrEmpty(currentRespawnLoadout) && currentRespawnLoadout.Equals(loadout, StringComparison.OrdinalIgnoreCase))
                loadoutData.SetRespawnLoadout(player, string.Empty);
            else loadoutData.SetRespawnLoadout(player, loadout);
            
            OpenLoadoutMenu(player, loadout);
        }

        [ConsoleCommand("loadoutui.create")]
        private void ccmdCreateLoadout(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            int count = CurrentProfileCount(player);
            int max = MaxAllowedProfiles(player);

            if (count >= max)
            {
                ChatMessage(player, msg("Notification.MaxAllowedProfiles", player.UserIDString));
                return;
            }

            double cooldownRemaining;
            if (!permission.UserHasPermission(player.UserIDString, PERM_NO_CREATION_COOLDOWN) && loadoutData.IsOnCreationCooldown(player, out cooldownRemaining))
            {
                ChatMessage(player, string.Format(msg("Notification.CreationCooldown", player.UserIDString), FormatTime(cooldownRemaining)));
                return;
            }

            OpenCreationMenu(player, string.Empty);
        }

        [ConsoleCommand("loadoutui.open")]
        private void ccmdOpenLoadout(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            
            if (arg.Args.Length == 2)
                OpenLoadoutMenu(player, arg.GetString(0), arg.GetInt(1));
            else OpenLoadoutMenu(player, string.Empty, arg.GetInt(0));
        }
        #endregion

        #region Commands
        private void cmdLoadout(BasePlayer player, string command, string[] args)
        {
            if (!HasAnyPermission(player))
            {
                ChatMessage(player, msg("Notification.NoPermission", player.UserIDString));
                return;
            }

            OpenLoadoutMenu(player, string.Empty);
        }

        [ChatCommand("editdefaultloadout")]
        private void cmdCreateDefaultLoadout(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                ChatMessage(player, msg("Notification.Admin.NoPermission", player.UserIDString));
                return;
            }

            loadoutCreators[player.userID] = new CreatorData(player, true);

            if (loadoutData.defaultLoadout != null)
                loadoutCreators[player.userID].Inventory = loadoutData.defaultLoadout;

            OpenCreationMenu(player, string.Empty);
        }

        [ChatCommand("deletedefaultloadout")]
        private void cmdDeleteDefaultLoadout(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                ChatMessage(player, msg("Notification.Admin.NoPermission", player.UserIDString));
                return;
            }

            if (loadoutData.defaultLoadout != null)
            {
                ChatMessage(player, msg("Notification.Admin.NoDefaultLoadout", player.UserIDString));
                return;
            }

            loadoutData.defaultLoadout = null;
            ChatMessage(player, msg("Notification.Admin.DeletedDefaultLoadout", player.UserIDString));
        }

        [ConsoleCommand("loadouts.givedefaultloadout")]
        private void ccmdGiveDefaultLoadout(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Connection.player as BasePlayer;
                if (player != null && !player.IsAdmin)
                    return;
            }

            if (arg.Args.Length != 1)
            {
                SendReply(arg, "You must specify a user ID");
                return;
            }

            if (loadoutData.defaultLoadout == null)
            {
                SendReply(arg, "You have not setup a default loadout");
                return;
            }

            BasePlayer target = BasePlayer.Find(arg.GetString(0));
            if (target == null)
            {
                SendReply(arg, $"Unable to find the target player: {arg.GetString(0)}");
                return;
            }

            if (giveType == GiveType.SupplyDrop)
            {
                Item item = ItemManager.CreateByName(SUPPLYSIGNAL_ITEM, 1, SUPPLYSIGNAL_SKIN);
                target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);

                activeSupplySignals.Add(item.uid, loadoutData.defaultLoadout);

                ChatMessage(target, msg("Notification.SignalGiven.Admin", target.UserIDString));
            }
            else
            {
                loadoutData.defaultLoadout.Give(target);
                ChatMessage(target, msg("Notification.ClaimedLoadout.Admin", target.UserIDString));
            }
        }
        #endregion

        #region Config        
        private static ConfigData configData;
        private class ConfigData
        {            
            [JsonProperty(PropertyName = "Profile Limitations (permission / options)")]
            public Dictionary<string, Limitations> Limits { get; set; }

            [JsonProperty(PropertyName = "Cost Options")]
            public CostOptions Cost { get; set; }

            [JsonProperty(PropertyName = "Commands to open loadout menu")]
            public string[] Commands { get; set; }
                        
            [JsonProperty(PropertyName = "Allowed categories of items allowed to be saved in loadout profiles (shortname)")]
            public string[] AllowedCategories { get; set; }

            [JsonProperty(PropertyName = "Items allowed to be saved in loadout profiles (shortname) (ignores disabled categories)")]
            public string[] AllowedItems { get; set; }

            [JsonProperty(PropertyName = "Items not allowed to be saved in loadout profiles (shortname)")]
            public string[] BlockedItems { get; set; }

            [JsonProperty(PropertyName = "Method to give items to player (SupplyDrop, GiveItems)")]
            public string GiveType { get; set; }

            [JsonProperty(PropertyName = "Give last purchased loadout as auto-kit on respawn (there is no cost associated with this)")]
            public bool AutoKits { get; set; }

            [JsonProperty(PropertyName = "Method to give items to player when claimed as a auto-kit (SupplyDrop, GiveItems)")]
            public string AutoKitGiveType { get; set; }

            [JsonProperty(PropertyName = "Supply Drop Options")]
            public DropOptions Drop { get; set; }
            
            [JsonProperty(PropertyName = "UI Colors")]
            public UIColors Colors { get; set; }

            [JsonProperty(PropertyName = "Only allow loadouts to be claimed in these zones")]
            public string[] Zones { get; set; }

            [JsonProperty(PropertyName = "Auto-loadout icon URL")]
            public string DefaultIcon { get; set; }

            public class Limitations
            {
                [JsonProperty(PropertyName = "Maximum number of profiles allowed")]
                public int MaxProfiles { get; set; }

                [JsonProperty(PropertyName = "Number of belt slots")]
                public int BeltSlots { get; set; }

                [JsonProperty(PropertyName = "Number of main slots")]
                public int MainSlots { get; set; }

                [JsonProperty(PropertyName = "Number of wear slots")]
                public int WearSlots { get; set; }

                [JsonProperty(PropertyName = "Claim cooldown time (seconds)")]
                public int Cooldown { get; set; }

                [JsonProperty(PropertyName = "Create cooldown time (seconds)")]
                public int CreateCooldown { get; set; }

                [JsonProperty(PropertyName = "Cost multiplier")]
                public float CostMultiplier { get; set; }
            }

            public class DropOptions
            {
                [JsonProperty(PropertyName = "Drop velocity")]
                public float Velocity { get; set; }

                [JsonProperty(PropertyName = "Time from when supply signal is thrown until drop is launched (seconds)")]
                public float Time { get; set; }

                [JsonProperty(PropertyName = "Parachute deploy distance from ground")]
                public float DeployDistance { get; set; }

                [JsonProperty(PropertyName = "Disallow players from claiming a loadout signal if they already have one in their inventory")]
                public bool OnlySingleDrop { get; set; }
            }

            public class CostOptions
            {
                [JsonProperty(PropertyName = "Claiming loadouts cost currency")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Currency type (ServerRewards, Economics, Scrap)")]
                public string Currency { get; set; }
            }

            public class UIColors
            {
                public Color Background { get; set; }

                public Color Header { get; set; }

                public Color Panel { get; set; }

                public Color Profile { get; set; }

                public Color Exit { get; set; }

                public class Color
                {
                    public string Hex { get; set; }
                    public float Alpha { get; set; }

                    public string ToColor() => UI.Color(Hex, Alpha);
                }                
            }
            
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Limits = new Dictionary<string, ConfigData.Limitations>
                {
                    ["loadouts.default"] = new ConfigData.Limitations()
                    {
                        BeltSlots = 6,
                        MainSlots = 0,
                        WearSlots = 3,
                        MaxProfiles = 2,
                        Cooldown = 1800,
                        CreateCooldown = 600,
                        CostMultiplier = 1f,
                    },
                    ["loadouts.vip1"] = new ConfigData.Limitations()
                    {
                        BeltSlots = 6,
                        MainSlots = 6,
                        WearSlots = 6,
                        MaxProfiles = 3,
                        Cooldown = 900,
                        CreateCooldown = 300,
                        CostMultiplier = 0.75f,
                    },
                    ["loadouts.vip2"] = new ConfigData.Limitations()
                    {
                        BeltSlots = 6,
                        MainSlots = 24,
                        WearSlots = 7,
                        MaxProfiles = 4,
                        Cooldown = 600,
                        CreateCooldown = 120,
                        CostMultiplier = 0.5f,
                    },
                },
                Cost = new ConfigData.CostOptions
                {
                    Enabled = false,
                    Currency = "Scrap"
                },
                GiveType = "SupplyDrop",
                Drop = new ConfigData.DropOptions
                {
                    Time = 20f,
                    Velocity = 120f,
                    DeployDistance = 50f,
                    OnlySingleDrop = false
                },
                AutoKits = false,
                AutoKitGiveType = "SupplyDrop",
                Commands = new string[] { "loadout", "l" },
                AllowedCategories = new string[] { "Weapon", "Construction", "Items", "Resources", "Attire", "Tool", "Medical", "Food", "Ammunition", "Traps", "Misc", "Component", "Electrical", "Fun" },
                AllowedItems = new string[0],
                BlockedItems = new string[] { "explosive.timed", "explosive.satchel" },
                Colors = new ConfigData.UIColors
                {
                    Background = new ConfigData.UIColors.Color
                    {
                        Hex = "000000",
                        Alpha = 0.6f
                    },
                    Header = new ConfigData.UIColors.Color
                    {
                        Hex = "6a8b38",
                        Alpha = 0.9f
                    },
                    Panel = new ConfigData.UIColors.Color
                    {
                        Hex = "000000",
                        Alpha = 0.7f
                    },
                    Profile = new ConfigData.UIColors.Color
                    {
                        Hex = "387097",
                        Alpha = 0.9f
                    },
                    Exit = new ConfigData.UIColors.Color
                    {
                        Hex = "d85540",
                        Alpha = 0.9f
                    }
                },
                Zones = new string[0],
                DefaultIcon = "https://www.rustedit.io/images/loadouts-default.png",
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (configData.Version < new VersionNumber(0, 1, 4))
                configData.Zones = new string[0];

            if (configData.Version < new VersionNumber(0, 1, 7))
            {
                configData.AutoKits = false;
                configData.AutoKitGiveType = "SupplyDrop";
            }

            if (configData.Version < new VersionNumber(0, 1, 17))
                configData.DefaultIcon = "https://www.rustedit.io/images/loadouts-default.png";

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        private void SaveData() => loadout.WriteObject(loadoutData);

        private void SaveCosts() => costs.WriteObject(costData);

        private void SaveSupplySignals() => supplysignals.WriteObject(activeSupplySignals);

        private void LoadData()
        {
            loadoutData = loadout.ReadObject<LoadoutData>();
            if (loadoutData == null || loadoutData.loadouts == null)
                loadoutData = new LoadoutData();

            activeSupplySignals = supplysignals.ReadObject<Hash<uint, LoadoutData.InventoryData>>() ?? new Hash<uint, LoadoutData.InventoryData>();

            costData = costs.ReadObject<CostData>();
            if (costData == null || costData.costs?.Count == 0)
            {
                costData = new CostData();
                costData.GenerateDefaultCosts();
                SaveCosts();
            }
            else costData.CheckUpdate();

            itemLimits = limits.ReadObject<Hash<string, int>>();
            if (itemLimits == null || itemLimits.Count == 0)
                GenerateItemLimits();
            else UpdateItemLimits();
        }
        
        private void GenerateItemLimits()
        {
            itemLimits = new Hash<string, int>();

            ItemManager.itemList.ForEach(x =>
            {
                itemLimits[x.shortname] = x.stackable;
            });

            limits.WriteObject<Hash<string, int>>(itemLimits);
        }

        private void UpdateItemLimits()
        {
            bool hasChanged = false;

            ItemManager.itemList.ForEach(x =>
            {
                if (!itemLimits.ContainsKey(x.shortname))
                {
                    itemLimits[x.shortname] = x.stackable;
                    hasChanged = true;
                }
            });

            if (hasChanged)
                limits.WriteObject<Hash<string, int>>(itemLimits);
        }

        private class LoadoutData
        {
            public Hash<ulong, Hash<string, InventoryData>> loadouts = new Hash<ulong, Hash<string, InventoryData>>();

            public Hash<ulong, string> respawnLoadouts = new Hash<ulong, string>();

            public Hash<ulong, double> creationCooldown = new Hash<ulong, double>();

            public InventoryData defaultLoadout = null;

            public void GetLoadouts(BasePlayer player, out Hash<string, InventoryData> list)
            {
                if (!loadouts.TryGetValue(player.userID, out list))
                    list = loadouts[player.userID] = new Hash<string, InventoryData>();                
            }

            public Hash<string, InventoryData> GetLoadouts(BasePlayer player)
            {
                Hash<string, InventoryData> list;
                loadouts.TryGetValue(player.userID, out list);
                return list;
            }

            public void SetRespawnLoadout(BasePlayer player, string loadout)
            {
                if (respawnLoadouts == null)                
                    respawnLoadouts = new Hash<ulong, string>();                

                respawnLoadouts[player.userID] = loadout;
            }

            public string GetRespawnLoadout(BasePlayer player)
            {
                string loadout;
                respawnLoadouts.TryGetValue(player.userID, out loadout);
                return loadout;
            }

            public void OnLoadoutCreated(BasePlayer player, double cooldown) => creationCooldown[player.userID] = cooldown;

            public bool IsOnCreationCooldown(BasePlayer player, out double remaining)
            {
                remaining = 0;

                double nextCreationTime;
                if (!creationCooldown.TryGetValue(player.userID, out nextCreationTime))
                    return false;

                remaining = nextCreationTime - CurrentTime();
                return remaining > 0;
            }

            public class InventoryData
            {
                public ItemData[] containerMain;
                public ItemData[] containerBelt;
                public ItemData[] containerWear;

                [JsonIgnore]
                private int _mainCount = -1;

                [JsonIgnore]
                private int _beltCount = -1;

                [JsonIgnore]
                private int _wearCount = -1;

                [JsonIgnore]
                public int MainCount
                {
                    get
                    {
                        if (_mainCount == -1)
                        {
                            _mainCount = 0;

                            for (int i = 0; i < containerMain.Length; i++)
                            {
                                if (containerMain[i] != null)
                                    _mainCount++;
                            }
                        }
                        return _mainCount;
                    }
                }

                [JsonIgnore]
                public int BeltCount
                {
                    get
                    {
                        if (_beltCount == -1)
                        {
                            _beltCount = 0;

                            for (int i = 0; i < containerBelt.Length; i++)
                            {
                                if (containerBelt[i] != null)
                                    _beltCount++;
                            }
                        }
                        return _beltCount;
                    }
                }

                [JsonIgnore]
                public int WearCount
                {
                    get
                    {
                        if (_wearCount == -1)
                        {
                            _wearCount = 0;

                            for (int i = 0; i < containerWear.Length; i++)
                            {
                                if (containerWear[i] != null)
                                    _wearCount++;
                            }
                        }
                        return _wearCount;
                    }
                }

                public double nextUsageTime;

                [JsonIgnore]
                private int _cost = -1;
                
                public InventoryData() { }

                public InventoryData(BasePlayer player)
                {
                    containerMain = new ItemData[player.inventory.containerMain.capacity];
                    containerBelt = new ItemData[player.inventory.containerBelt.capacity];
                    containerWear = new ItemData[player.inventory.containerWear.capacity];

                    CopyItemsFrom(player);
                }

                public InventoryData(BasePlayer player, int beltSlots, int wearSlots, int mainSlots)
                {
                    containerMain = new ItemData[mainSlots];
                    containerBelt = new ItemData[beltSlots];
                    containerWear = new ItemData[wearSlots];
                }

                public void CopyItemsFrom(BasePlayer player)
                {
                    foreach (Item item in player.inventory.containerMain.itemList)
                    {
                        if (!Instance.IsAllowedItem(item.info))
                            continue;

                        int limit = Instance.GetItemLimit(item.info.shortname);
                        int current = FindItemAmount(item.info.shortname);

                        if (current >= limit)
                            continue;

                        if (item.position < containerMain.Length)
                            containerMain[item.position] = new ItemData(item, limit - current);
                    }

                    foreach (Item item in player.inventory.containerBelt.itemList)
                    {
                        if (!Instance.IsAllowedItem(item.info))
                            continue;

                        int limit = Instance.GetItemLimit(item.info.shortname);
                        int current = FindItemAmount(item.info.shortname);

                        if (current >= limit)
                            continue;

                        if (item.position < containerBelt.Length)
                            containerBelt[item.position] = new ItemData(item, limit - current);
                    }

                    foreach (Item item in player.inventory.containerWear.itemList)
                    {
                        if (!Instance.IsAllowedItem(item.info))
                            continue;

                        int limit = Instance.GetItemLimit(item.info.shortname);
                        int current = FindItemAmount(item.info.shortname);

                        if (current >= limit)
                            continue;

                        if (item.position < containerWear.Length)
                            containerWear[item.position] = new ItemData(item, limit - current);
                    }

                    _mainCount = -1;
                    _beltCount = -1; 
                    _wearCount = -1;
                }

                public int FindItemAmount(string shortname)
                {
                    int amount = 0;

                    foreach(ItemData item in containerBelt)
                    {
                        if (item?.shortname.Equals(shortname) ?? false)
                            amount += item.amount;
                    }

                    return amount;
                }

                public bool IsOnCooldown()
                {
                    return CurrentTime() < nextUsageTime;
                }
               
                [JsonIgnore]
                public double CooldownRemaining
                {
                    get
                    {
                        return nextUsageTime - CurrentTime();
                    }
                }

                public void Give(BasePlayer player)
                {
                    List<Item> list = Facepunch.Pool.GetList<Item>();

                    for (int i = 0; i < containerBelt.Length; i++)
                    {
                        ItemData itemData = containerBelt[i];
                        if (itemData != null)
                        {
                            Item item = itemData.Create();
                            if (!item.MoveToContainer(player.inventory.containerBelt, itemData.position, false))
                                list.Add(item);
                        }
                    }

                    for (int i = 0; i < containerWear.Length; i++)
                    {
                        ItemData itemData = containerWear[i];
                        if (itemData != null)
                        {
                            Item item = itemData.Create();
                            if (!item.MoveToContainer(player.inventory.containerWear, itemData.position, false))
                                list.Add(item);
                        }
                    }

                    for (int i = 0; i < containerMain.Length; i++)
                    {
                        ItemData itemData = containerMain[i];
                        if (itemData != null)
                        {
                            Item item = itemData.Create();
                            if (!item.MoveToContainer(player.inventory.containerMain, itemData.position, false))
                                list.Add(item);
                        }
                    }

                    foreach (Item item in list)
                    {
                        if (!player.inventory.GiveItem(item))
                            item.Drop(player.GetDropPosition(), player.GetDropVelocity());
                    }

                    Facepunch.Pool.FreeList(ref list);
                }

                public void Give(LootContainer lootContainer)
                {
                    lootContainer.panelName = "generic_resizable";
                    lootContainer.inventory.capacity = BeltCount + MainCount + WearCount;

                    for (int i = 0; i < containerBelt.Length; i++)
                    {
                        ItemData itemData = containerBelt[i];
                        if (itemData != null)
                        {
                            Item item = itemData.Create();
                            if (!item.MoveToContainer(lootContainer.inventory, -1, false))
                                item.Drop(lootContainer.GetDropPosition(), lootContainer.GetDropVelocity());
                        }
                    }

                    for (int i = 0; i < containerWear.Length; i++)
                    {
                        ItemData itemData = containerWear[i];
                        if (itemData != null)
                        {
                            Item item = itemData.Create();
                            if (!item.MoveToContainer(lootContainer.inventory, -1, false))
                                item.Drop(lootContainer.GetDropPosition(), lootContainer.GetDropVelocity());
                        }
                    }

                    for (int i = 0; i < containerMain.Length; i++)
                    {
                        ItemData itemData = containerMain[i];
                        if (itemData != null)
                        {
                            Item item = itemData.Create();
                            if (!item.MoveToContainer(lootContainer.inventory, -1, false))
                                item.Drop(lootContainer.GetDropPosition(), lootContainer.GetDropVelocity());
                        }
                    }

                    lootContainer.inventory.MarkDirty();
                }

                public int GetCost(BasePlayer player)
                {
                    if (_cost == -1)
                    {
                        _cost = CalculateCosts(GetLimitations(player).CostMultiplier);
                    }
                    return _cost;
                }

                public int CalculateCosts(float costMultiplier)
                {
                    float cost = 0;

                    foreach(ItemData item in containerBelt)
                    {
                        if (item != null)
                        {
                            cost += Instance.costData.Find(item.shortname) * item.amount;
                        }
                    }

                    foreach (ItemData item in containerMain)
                    {
                        if (item != null)
                        {
                            cost += Instance.costData.Find(item.shortname) * item.amount;
                        }
                    }

                    foreach (ItemData item in containerWear)
                    {
                        if (item != null)
                        {
                            cost += Instance.costData.Find(item.shortname) * item.amount;
                        }
                    }

                    return Mathf.RoundToInt(cost * costMultiplier);
                }
                
                public class ItemData
                {
                    public string shortname;
                    public ulong skin;
                    public int amount;                   
                    public string ammotype;
                    public int position;
                    public InstanceData instanceData;
                    public ItemData[] contents;

                    public ItemData() { }

                    public ItemData(Item item, int max)
                    {
                        shortname = item.info.shortname;
                        skin = item.skin;
                        amount = Mathf.Min(item.info.category == ItemCategory.Weapon || item.info.category == ItemCategory.Attire ? 1 : item.amount, max);                        
                        ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null;
                        position = item.position;
                        instanceData = new ItemData.InstanceData(item);
                        contents = item.contents?.itemList.Select(x => new ItemData(x, int.MaxValue)).ToArray();
                    }

                    public Item Create()
                    {
                        Item item = ItemManager.CreateByName(shortname, amount, skin);
                        
                        if (instanceData?.IsValid() ?? false)
                            instanceData.Restore(item);

                        BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                        if (weapon != null)
                        {
                            if (!string.IsNullOrEmpty(ammotype))
                                weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
                            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                        }

                        FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
                        if (flameThrower != null)
                            flameThrower.ammo = flameThrower.maxAmmo;

                        if (contents != null && item.contents != null)
                        {
                            foreach (ItemData contentData in contents)
                            {
                                Item newContent = ItemManager.CreateByName(contentData.shortname, contentData.amount);
                                if (newContent != null)
                                {
                                    newContent.MoveToContainer(item.contents);
                                }
                            }
                        }
                        return item;
                    }

                    public class InstanceData
                    {
                        public int dataInt;
                        public int blueprintTarget;
                        public int blueprintAmount;

                        public InstanceData() { }
                        public InstanceData(Item item)
                        {
                            if (item.instanceData == null)
                                return;

                            dataInt = item.instanceData.dataInt;
                            blueprintAmount = item.instanceData.blueprintAmount;
                            blueprintTarget = item.instanceData.blueprintTarget;
                        }

                        public void Restore(Item item)
                        {
                            if (item.instanceData == null)
                                item.instanceData = new ProtoBuf.Item.InstanceData();

                            item.instanceData.ShouldPool = false;

                            item.instanceData.blueprintAmount = blueprintAmount;
                            item.instanceData.blueprintTarget = blueprintTarget;
                            item.instanceData.dataInt = dataInt;

                            item.MarkDirty();
                        }

                        public bool IsValid()
                        {
                            return dataInt != 0 || blueprintAmount != 0 || blueprintTarget != 0;
                        }
                    }
                }
            }
        }

        private class CreatorData
        {
            public string Name;            
            public bool CanItemSelect;
            public bool IsDefaultLoadout;
            public LoadoutData.InventoryData Inventory;
            public ConfigData.Limitations Limits;

            public ContainerType amountContainerType;
            public int amountIndex;
            public int amountMax;

            public CreatorData() { }

            public CreatorData(BasePlayer player, bool isDefaultLoadout = false)
            {
                if (isDefaultLoadout)
                {
                    IsDefaultLoadout = true;
                    CanItemSelect = true;
                    Limits = new ConfigData.Limitations() { BeltSlots = 6, MainSlots = 24, WearSlots = 7 };
                }
                else
                {
                    CanItemSelect = Instance.permission.UserHasPermission(player.UserIDString, PERM_ITEM_SELECTOR);

                    Limits = GetLimitations(player);
                }

                Inventory = new LoadoutData.InventoryData(player, Limits.BeltSlots, Limits.WearSlots, Limits.MainSlots);
            }
        }

        private class CostData
        {
            public Hash<string, float> costs = new Hash<string, float>();

            public void GenerateDefaultCosts()
            {
                List<ItemDefinition> list = Facepunch.Pool.GetList<ItemDefinition>();
                list.AddRange(ItemManager.itemList);

                list.Sort(delegate (ItemDefinition a, ItemDefinition b)
                {
                    return a.shortname.CompareTo(b.shortname);
                });

                list.ForEach(x => costs[x.shortname] = x.category == ItemCategory.Resources || x.category == ItemCategory.Food ? ((int)x.rarity + 1) * 0.1f : ((int)x.rarity + 1));
                
                Facepunch.Pool.FreeList(ref list);
            }

            public void CheckUpdate()
            {
                bool hasChanged = false;

                foreach (ItemDefinition itemDefinition in ItemManager.itemList)
                {
                    if (!costs.ContainsKey(itemDefinition.shortname))
                    {
                        costs[itemDefinition.shortname] = ((int)itemDefinition.rarity + 1);
                        hasChanged = true;
                    }
                }

                if (hasChanged)
                    Instance.SaveCosts();
            }

            public float Find(string shortname) => costs[shortname];
        }

        #endregion

        #region Localization
        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Message.Prefix"] = "<color=#ce422b>[ Loadouts ]</color> ",

            ["UI.Title"] = "Loadouts",
            ["UI.Title.Name"] = "Loadouts - {0}",
            ["UI.Profiles"] = "Profiles",
            ["UI.CreateLoadout"] = "Create Loadout",
            ["UI.Cooldown"] = "Cooldown : {0}",
            ["UI.Claim"] = "Claim Loadout",
            ["UI.Claim.Cost"] = "Claim ({0} {1})",
            ["UI.Delete"] = "Delete Loadout",

            ["UI.SetDefault"] = "Set Respawn Loadout",
            ["UI.RemoveDefault"] = "Remove Respawn Loadout",

            ["Creator.Title"] = "Loadout Creator",
            ["Creator.Title.DefaultLoadout"] = "Loadout Creator - Default Loadout",
            ["Creator.Save"] = "Save Loadout",
            ["Creator.CopyInv"] = "Copy Inventory",
            ["Creator.Costs"] = "Cost : {0} {1}",
            ["Creator.Profile"] = "Profile Name :",
            ["Creator.Current"] = "Current :",
            ["Creator.Max"] = "Max :",
            ["Creator.Amount"] = "Amount :",

            ["ItemSelector.Title"] = "Item Selector",

            ["Container.Belt"] = "Belt",
            ["Container.Main"] = "Main",
            ["Container.Wear"] = "Wear",

            ["Notification.NoProfileName"] = "You must enter a profile name",
            ["Notification.ProfileNameExists"] = "A loadout already exists with that name",
            ["Notification.SavedLoadout"] = "Saved loadout <color=#ce422b>{0}</color>",
            ["Notification.NoFreeSlots"] = "You do not have enough free slots to claim this loadout",
            ["Notification.ClaimedLoadout"] = "You have claimed the loadout <color=#ce422b>{0}</color>",
            ["Notification.ClaimedDefaultLoadout"] = "You have claimed the default loadout",
            ["Notification.MaxAllowedProfiles"] = "You already have the maximum number of loadout profiles allowed",
            ["Notification.NoPermission"] = "You do not have permission to use Loadouts",
            ["Notification.NoEnoughFunds"] = "You do not have enough {0} to claim this loadout!",
            ["Notification.DenyClaimArea"] = "You can not claim a loadout in this area",
            ["Notification.Cooldown"] = "You have a cooldown remaining of {0}",
            ["Notification.LoadoutInbound"] = "Loadout inbound. ETA {0}s",
            ["Notification.HasLoadoutSignal"] = "You already have a loadout supply signal in your inventory",
            ["Notification.SignalGiven"] = "Throw the supply signal to call your loadout",
            ["Notification.SignalGiven.Admin"] = "You have been given a Loadout. Throw the supply signal to call your loadout",
            ["Notification.ClaimedLoadout.Admin"] = "You have been given a Loadout",
            ["Notification.MaxAmount"] = "You already have the maximum amount allowed for that item in this loadout",
            ["Notification.CreationCooldown"] = "You can not create another loadout for another {0}",

            ["Notification.Admin.NoPermission"] = "You do not have permission to use this command",
            ["Notification.Admin.NoDefaultLoadout"] = "There is not currently a default loadout saved",
            ["Notification.Admin.DeletedDefaultLoadout"] = "You have deleted the default loadout",

            ["Currency.ServerRewards"] = "RP",
            ["Currency.Economics"] = "Eco",
            ["Currency.Scrap"] = "Scrap",
        };
        #endregion
    }
}