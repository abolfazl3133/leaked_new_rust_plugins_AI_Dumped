using Facepunch;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

/*
 * VERSION HISTORY
 * 
 * V 1.0.1
 * - fix null reference exception when loading
 * - add config option do disable shop location
 * - add default popular items
 * - fix ui with chat command
 * - add config option for chat command
 * - change default command to /market to avoid conflicts
 * 
 * V 1.0.2
 * - fix incorrect amount displayed in shop view
 * - fix possible NRE in AddMarker
 * - change file name to prevent conflict with global::Marketplace
 * - add lang support
 * - add Translation API for item names
 * 
 * V 1.0.3
 * - change overlay to non scaled
 * - move close button away from hostile marker
 * 
 * V 1.0.4
 * - initialize search in OnServerInitialized to make sure item list is loaded
 *
 * V 1.0.5
 * - fix for December rust update
 * 
 */

namespace Oxide.Plugins
{
    [Info(nameof(MarketplaceUi), "The_Kiiiing", "1.0.5")]
    internal class MarketplaceUi : RustPlugin
    {
        #region Fields

        private const string PERM_USE = "marketplaceui.use";

        private const string UI_PANEL = "marketplaceui.ui";
        private const string UI_COMMAND = "marketplaceui.cmd";

        private static MarketplaceUi _instance;

        private readonly List<ulong> openUis = new();

        private readonly Dictionary<ulong, VendingOffer.SortMode> playerSortModes = new();

        private DataStore dataStore;

        private ItemSearch search;

        [PluginReference, UsedImplicitly]
        private readonly Plugin RustTranslationAPI;

        #endregion

        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty("Show vending machine owner")]
            public bool showShopOwner = true;

            [JsonProperty("Show vending machine grid position")]
            public bool showGridPos = true;

            [JsonProperty("Enable access via chat command")]
            public bool enableCommand = true;

            [JsonProperty("Chat command")]
            public string chatCommand = "market";

            [JsonProperty("Enable access via computer station")]
            public bool enableStation = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
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

        private class DataStore
        {
            [JsonProperty]
            private Dictionary<int, int> topItems = new Dictionary<int, int>
            {
                { -932201673 , 1 },
                { -946369541 , 1 },
                {  69511070 , 1 },
                { -1812555177 , 1 },
                { -1581843485 , 1 },
                { -1157596551 , 1 },
            };

            public void AddSearch(int itemid)
            {
                if (topItems.ContainsKey(itemid))
                    topItems[itemid]++;
                else
                    topItems[itemid] = 1;
            }

            private int GetSearchScore(int itemid)
            {
                if (topItems.ContainsKey(itemid))
                    return topItems[itemid];
                return 0;
            }

            public IOrderedEnumerable<int> SortItemsByScore(IEnumerable<int> input)
            {
                return input.OrderByDescending(GetSearchScore);
            }

            public IEnumerable<int> GetPopularItems(int amount)
            {
                //int count = amount > topItems.Count ? topItems.Count : amount;
                var items = topItems
                    .OrderByDescending(x => x.Value)
                    .Select(x => x.Key)
                    .Take(amount);

                return items;
                //if (amount < items.Count())
                //{
                //    items.RemoveRange(amount, items.Count() - amount);
                //}

                //return items.OrderBy(x => 1);
            }
        }

        private void LoadData()
        {
            var data = Interface.Oxide.DataFileSystem.ReadObject<DataStore>(Name);
            if (data == null) data = new DataStore();

            dataStore = data;
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, dataStore);

        #endregion

        #region Core

        private class VendingCache
        {
            public static VendingCache Instance { get; } = new VendingCache();

            private DateTime time;
            private readonly List<Shop> shopCache;

            public bool IsEmpty => shopCache.IsEmpty();

            private VendingCache()
            {
                time = DateTime.Now;
                shopCache = new List<Shop>();
            }

            public VendingOffer FindOfferByGuid(string guid)
            {
                var ids = UnwrapGuid(guid);

                var shop = shopCache.Find(x => x.id == ids[0]);
                var offer = shop.FindOfferById(ids[1]);

                return offer;
            }

            public Shop FindShopByGuid(string guid)
            {
                var ids = UnwrapGuid(guid);
                return shopCache.Find(x => x.id == ids[0]);
            }

            private ulong[] UnwrapGuid(string guid)
            {
                var x = guid.Split(':');
                return new ulong[] { UInt64.Parse(x[0]), UInt64.Parse(x[1]) };
            }

            public IEnumerable<VendingOffer> FindOffers(int itemid)
            {
                if (Mathf.Abs((float)DateTime.Now.Subtract(time).TotalMinutes) > 1)
                {
                    Refresh();
                }

                var offers = new List<VendingOffer>();
                foreach (var s in shopCache)
                {
                    offers.AddRange(Array.FindAll(s.offers, x => x.offer.ItemId == itemid));
                }

                return offers;
            }

            public void Refresh(Action callback = null)
            {
                InvokeHandler.Instance.StartCoroutine(RefreshCoro(callback));
            }

            private IEnumerator RefreshCoro(Action callback)
            {
                DPrint($"refresh shops");
                shopCache.Clear();

                foreach (var vm in BaseNetworkable.serverEntities.OfType<VendingMachine>())
                {
                    if (vm.net == null || !vm.IsBroadcasting())
                    {
                        continue;
                    }

                    // if (false && vm.OwnerID == 0) continue;

                    shopCache.Add(new Shop(vm));
                }

                time = DateTime.Now;

                callback?.Invoke();
                yield break;
            }

        }

        private class VendingItemAmount
        {
            public int ItemId { get; }
            public int Amount { get; }

            public VendingItemAmount(int itemid, int amount)
            {
                ItemId = itemid;
                Amount = amount;
            }
        }

        private class Shop
        {
            public ulong id;
            public VendingOffer[] offers;

            public string shopName;
            public Vector3 position;

            public ulong ownerId;

            public bool IsNpc => ownerId == 0UL;
            public string GridPosition => MapHelper.PositionToString(position);

            public Shop(VendingMachine vm)
            {
                id = vm.net.ID.Value;
                shopName = vm.shopName;
                position = vm.transform.position;
                ownerId = vm.OwnerID;

                var offerList = Pool.Get<List<VendingOffer>>();
                uint oid = 0;

                foreach (var offer in vm.sellOrders.sellOrders)
                {
                    var vo = new VendingOffer(this, oid, offer.inStock, offer.itemToSellID, offer.itemToSellAmount, offer.currencyID, offer.currencyAmountPerItem);
                    offerList.Add(vo);
                    oid++;
                }

                offers = offerList.ToArray();
                Pool.FreeUnmanaged(ref offerList);
            }

            public VendingOffer FindOfferById(ulong id)
            {
                return offers[id];
            }

            public int GetDistance(BasePlayer player)
            {
                return Mathf.RoundToInt(Vector3.Distance(player.transform.position, position));
            }

            public string SerializePosition()
            {
                return $"{position.x:N1}:{position.z:N1}";
            }

            public static Vector3 DeserializePosition(string serialized)
            {
                string[] split = serialized.Split(':');
                if (split.Length != 2)
                {
                    return default(Vector3);
                }

                return new Vector3(Single.Parse(split[0]), 0, Single.Parse(split[1]));
            }
        }

        private class VendingOffer
        {
            public Shop Shop { get; }

            public uint OfferId { get; }
            public string Guid { get; }

            public VendingItemAmount offer;
            public VendingItemAmount cost;

            public int TotalStock => stock * offer.Amount;

            public int stock;

            public VendingOffer(Shop shop, uint id, int stock, int itemid, int amount, int currency_itemid, int currency_amt)
            {
                this.Shop = shop;
                this.OfferId = id;
                this.stock = stock;
                offer = new VendingItemAmount(itemid, amount);
                cost = new VendingItemAmount(currency_itemid, currency_amt);

                Guid = $"{Shop.id}:{OfferId}";
            }

            public int GetDistance(BasePlayer player)
            {
                return Mathf.RoundToInt(Vector3.Distance(player.transform.position, Shop.position));
            }

            public enum SortMode { Distance, Price, Amount }

            public static IOrderedEnumerable<VendingOffer> SortOffers(BasePlayer player, IEnumerable<VendingOffer> offers)
            {
                var mode = GetPlayerSortMode(player);

                if (mode == SortMode.Distance)
                    return offers.OrderBy(x => x.GetDistance(player));
                else if (mode == SortMode.Price)
                    return offers.OrderBy(x => x.cost.Amount);
                else if (mode == SortMode.Amount)
                    return offers.OrderByDescending(x => (x.offer.Amount * x.stock));

                return (IOrderedEnumerable<VendingOffer>)offers;
            }
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            permission.RegisterPermission(PERM_USE, this);

            RegisterLangMessages();

            timer.In(5f, () =>
            {
                if (RustTranslationAPI == null)
                {
                    PrintWarning("RustTranslationAPI is not installed, item names will always be displayed in english. You can download it here: https://umod.org/plugins/rust-translation-api");
                }
            });
        }

        private void OnServerInitialized()
        {
            search = new ItemSearch();

            VendingCache.Instance.Refresh();

            AddCovalenceCommand(UI_COMMAND, nameof(GuiCommandHandler));
            AddCovalenceCommand(_config.chatCommand, nameof(CmdMarketplace));

            LoadData();
        }

        private void Unload()
        {
            SaveData();

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player, true);
            }

            _instance = null;
        }

        private void OnServerSave() => SaveData();

        private void OnEntityMounted(ComputerStation station, BasePlayer player)
        {
            if (_config.enableStation && permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                DrawButtonUi(player);
            }
        }

        private void OnEntityDismounted(ComputerStation station, BasePlayer player)
        {
            DestroyUi(player);
        }

        private void CmdMarketplace(IPlayer iplayer)
        {
            BasePlayer player = iplayer.Object as BasePlayer;

            if (!_config.enableCommand || player == null || iplayer.IsServer)
            {
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                DrawUi(player);
            }
            else
            {
                player.ChatMessage("You don't have permission to do that");
            }
        }

        #endregion

        #region UI Cache

        private class UICache
        {
            public string search = null;

            public IOrderedEnumerable<VendingOffer> offerCache;
            public ItemDefinition offerItem;
            public int offerPage;

            public Shop currentShop;

            private static readonly Dictionary<ulong, UICache> playerCache = new Dictionary<ulong, UICache>();

            public static UICache GetPlayerCache(BasePlayer player)
            {
                if (playerCache.ContainsKey(player.userID))
                    return playerCache[player.userID];

                var cache = new UICache();
                playerCache[player.userID] = cache;
                return cache;
            }

            public static void ClearPlayerCache(BasePlayer player)
            {
                playerCache.Remove(player.userID);
            }
        }

        #endregion

        #region UI Commands

        private void GuiCommandHandler(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer.IsServer) return;
            if (!permission.UserHasPermission(iplayer.Id, PERM_USE)) return;

            BasePlayer player = iplayer.Object as BasePlayer;
            if (args.Length < 1 || player == null) return;

            UICache uiCache = UICache.GetPlayerCache(player);

            DPrint($"{command} {String.Join(" ", args)}");

            switch (args[0])
            {
                case "open":
                    DrawUi(player);
                    break;

                case "close":
                    DestroyUi(player);
                    UICache.ClearPlayerCache(player);
                    break;

                case "dismount":
                    DestroyUi(player);
                    UICache.ClearPlayerCache(player);
                    player.EnsureDismounted();
                    break;

                case "search":
                    if (args.Length < 2) break;
                    string prefix = args[1];
                    uiCache.search = prefix;

                    var langEntry = search.GetEntry(lang.GetLanguage(player.UserIDString), RustTranslationAPI);
                    var results = langEntry.Search(prefix, 30, 10);
                    var orderedResults = dataStore.SortItemsByScore(results);
                    PatchItemResults(player, orderedResults);
                    break;

                case "sort":
                    ChangePlayerSortMode(player);

                    uiCache.offerCache = VendingOffer.SortOffers(player, uiCache.offerCache);

                    PatchOfferResults(player);
                    break;

                case "select_item":
                    if (args.Length < 2) break;
                    int itemid;
                    if (!Int32.TryParse(args[1], out itemid)) break;

                    if (args.Length == 3)
                        ChangePlayerSortMode(player);
                    else
                        dataStore.AddSearch(itemid);

                    var offers = VendingCache.Instance.FindOffers(itemid);
                    var sorted = VendingOffer.SortOffers(player, offers);

                    var itemdef = ItemManager.FindItemDefinition(itemid);

                    uiCache.offerCache = sorted;
                    uiCache.offerItem = itemdef;

                    PatchOfferResults(player);
                    break;

                case "page":
                    if (args.Length < 2) break;
                    int page;
                    if (!Int32.TryParse(args[1], out page)) break;

                    uiCache.offerPage = page;

                    PatchOfferResults(player);
                    break;

                case "details":
                    if (args.Length < 2) break;

                    var offer = VendingCache.Instance.FindShopByGuid(args[1]);
                    uiCache.currentShop = offer;

                    DrawShopOverlay(player);
                    break;

                case "marker":
                    if (args.Length < 2) break;
                    Vector3 pos = Shop.DeserializePosition(args[1]);
                    AddMarker(player, pos);
                    DestroyUi(player, false, UiHelper.shopOverlay);
                    break;
            }
        }

        #endregion

        #region CUI

        private void DrawButtonUi(BasePlayer player)
        {
            DestroyUi(player);

            CuiElementContainer result = new CuiElementContainer();

            string rootPanelName = result.Add(new CuiPanel
            {
                RectTransform =
                {
                    // Does not scale (please bring back ui scale for computer station)
                    AnchorMin = "0.1 0.848",
                    AnchorMax = "0.2 0.894"

                    // Scales with ui scale
                    //AnchorMin = "0.5 0.5",
                    //AnchorMax = "0.5 0.5",
                    //OffsetMin = "-510 251",
                    //OffsetMax = "-350 284"
                },
                Image =
                {
                    Color = UiHelper.transparentColor
                }
            }, "Hud.Menu", UI_PANEL);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Text = GetLangMessage(Lang.OPEN_MARKETPLACE, player),
                    Align = TextAnchor.MiddleCenter,
                    Color = "0.05 0.05 0.05 1",
                    FontSize = 16
                },
                Button =
                {
                    Command = $"{UI_COMMAND} open",
                    //Color = "0.9 0.9 0.9 0.9",
                    Close = UI_PANEL,

                    Color = "0.74 0.74 0.74 1",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                }
            }, rootPanelName);

            CuiHelper.AddUi(player, result);
            openUis.Add(player.userID);
        }

        private void DrawUi(BasePlayer player)
        {
            DestroyUi(player);

            CuiElementContainer result = new CuiElementContainer();

            string rootPanelName = result.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Image =
                {
                    Color = "0 0 0 1"
                },
                CursorEnabled = true,
                KeyboardEnabled = true
            }, "OverlayNonScaled", UI_PANEL);

            #region Head

            string headPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = UiHelper.panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.92",
                    AnchorMax = "0.99 1"
                },
            }, rootPanelName);

            result.Add(new CuiElement
            {
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = "https://i.imgur.com/fwi2NpB.png",
                            Color = "0.8 0.8 0.8 1",
                             
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0.01 0.15", AnchorMax = $"0.01 0.15",
                            OffsetMin = "0 0", OffsetMax = "40 40"
                        }
                    },
                Parent = headPanel
            });

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.05 0",
                    AnchorMax = "0.5 0.9"
                },
                Text =
                {
                    Text = String.Join(" ", GetLangMessage(Lang.MARKETPLACE, player).ToUpperInvariant() as IEnumerable<char>),
                    Align = TextAnchor.MiddleLeft,
                    Color = UiHelper.textColor,
                    FontSize = 21
                }
            }, headPanel);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.95 0.2",
                    AnchorMax = "0.98 0.8"
                },
                Text =
                {
                    Text = "X",
                    Align = TextAnchor.MiddleCenter,
                    Color = UiHelper.buttonTextColor,
                    FontSize = 15
                },
                Button =
                {
                    Command = $"{UI_COMMAND} dismount",
                    Color = UiHelper.redButtonColor,
                    Close = UI_PANEL
                }
            }, headPanel);

            #endregion

            #region Search

            string searchPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = UiHelper.panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.85",
                    AnchorMax = "0.99 0.91"
                },
            }, rootPanelName);

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.01 0.2",
                    AnchorMax = "0.6 0.75"
                },
                Text =
                {
                    Text = $"{GetLangMessage(Lang.SEARCH, player)}:",
                    Align = TextAnchor.MiddleLeft,
                    Color = UiHelper.textColor,
                    FontSize = 12
                }
            }, searchPanel);

            string inputPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = UiHelper.panelColorSec
                },
                RectTransform =
                {
                    AnchorMin = "0.07 0.2",
                    AnchorMax = "0.3 0.7"
                }
            }, searchPanel);

            //Input
            result.Add(new CuiElement
            {
                Parent = inputPanel,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Command = $"{UI_COMMAND} search",
                        Color = UiHelper.textColor,
                        CharsLimit = 30,
                        Font = UiHelper.regularFont
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.02 0", AnchorMax = "1 1",
                    }
                }
            });

            #endregion

            #region Search Results

            string resultPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = UiHelper.panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.5",
                    AnchorMax = "0.99 0.84"
                },
            }, rootPanelName, UiHelper.itemSearchPanel);

            #endregion

            #region Offers

            result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = UiHelper.panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.01",
                    AnchorMax = "0.99 0.49"
                },
            }, rootPanelName, UiHelper.offerPanel);

            #endregion

            CuiHelper.AddUi(player, result);
            openUis.Add(player.userID);

            PatchItemResults(player, dataStore.GetPopularItems(30));
        }

        private void PatchOfferResults(BasePlayer player)
        {
            //DestroyUi(player, panel: UiHelper.offerPanel);

            var uiCache = UICache.GetPlayerCache(player);
            var result = new CuiElementContainer();
            var lang = this.lang.GetLanguage(player.UserIDString);

            #region Head

            result.Add(new CuiElement
            {
                Parent = UI_PANEL,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.01 0.01",
                        AnchorMax = "0.99 0.49"
                    },
                    new CuiImageComponent
                    {
                        Color = UiHelper.panelColor
                    },
                },
                Name = UiHelper.offerPanel,
                DestroyUi = UiHelper.offerPanel
            });

            string offerPanel = UiHelper.offerPanel;/* = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = UiHelper.panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.01",
                    AnchorMax = "0.99 0.49"
                },
            }, UI_PANEL, UiHelper.offerPanel);*/

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.01 0.94",
                    AnchorMax = "0.3 0.98",
                },
                Text =
                {
                    Text = GetLangMessage(Lang.OFFERS_FOR, player, $"<i>{GetItemNameTranslated(uiCache.offerItem, lang)}</i>"),
                    Align = TextAnchor.MiddleLeft,
                    Color = UiHelper.textColor,
                    FontSize = 12
                }
            }, offerPanel);

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.8 0.94",
                    AnchorMax = "0.9 0.98",
                },
                Text =
                {
                    Text = GetLangMessage(Lang.SORT_BY, player),
                    Align = TextAnchor.MiddleRight,
                    Color = UiHelper.textColor,
                    FontSize = 10
                }
            }, offerPanel);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.905 0.94",
                    AnchorMax = "0.99 0.98"
                },
                Button =
                {
                    Command = $"{UI_COMMAND} sort",
                    Color = UiHelper.greyButtonColor,
                },
                Text =
                {
                    Text = GetLangMessage(GetPlayerSortMode(player).ToString(), player).ToUpper(),
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = UiHelper.buttonTextColor
                }
            }, offerPanel);

            #endregion

            #region Offers

            int r = 0; int c = 0;
            int r_max = 4; int c_max = 6;
            int itemsPerPage = r_max * c_max;
            int page = uiCache.offerPage;

            var pageItems = uiCache.offerCache.Skip(page * itemsPerPage).Take(itemsPerPage);

            var view = GetLangMessage(Lang.VIEW, player).ToUpperInvariant();
            var itemFor = GetLangMessage(Lang.FOR, player);
            var inStock = GetLangMessage(Lang.ITEM_STOCK, player);
            var totalStock = GetLangMessage(Lang.TOTAL_STOCK, player);

            foreach (var offer in pageItems)
            {
                string itemPanel = result.Add(new CuiPanel
                {
                    Image = new CuiImageComponent
                    {
                        Color = UiHelper.panelColorSec
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{0.01+r*0.247} {0.79-c*0.15}",
                        AnchorMax = $"{0.25+r*0.247} {0.92-c*0.15}"
                    },
                }, offerPanel);

                // offer
                result.Add(new CuiElement
                {
                    Parent = itemPanel,
                    Components =
                    {
                        GetItemImage(offer.offer.ItemId),
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.03 0.5",
                            AnchorMax = "0.03 0.5",
                            OffsetMin = "0 -15",
                            OffsetMax = "30 15"
                        }
                    }
                });

                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.03 0.5",
                        AnchorMax = "0.03 0.5",
                        OffsetMin = "15 -15",
                        OffsetMax = "40 -5"
                    },
                    Text =
                    {
                        Text = offer.offer.Amount.ToString(),
                        Align = TextAnchor.LowerCenter,
                        Color = UiHelper.textColor,
                        Font = UiHelper.regularFont,
                        FontSize = 8
                    }
                }, itemPanel);

                // for
                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.15 0.1",
                        AnchorMax = "0.22 0.9"
                    },
                    Text =
                    {
                        Text = itemFor,
                        Align = TextAnchor.MiddleCenter,
                        Color = UiHelper.textColor,
                        FontSize = 10
                    }
                }, itemPanel);

                // Cost
                result.Add(new CuiElement
                {
                    Parent = itemPanel,
                    Components =
                    {
                        GetItemImage(offer.cost.ItemId),
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.23 0.5",
                            AnchorMax = "0.23 0.5",
                            OffsetMin = "0 -15",
                            OffsetMax = "30 15"
                        }
                    }
                });

                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.25 0.5",
                        AnchorMax = "0.25 0.5",
                        OffsetMin = "15 -15",
                        OffsetMax = "40 -5"
                    },
                    Text =
                    {
                        Text = offer.cost.Amount.ToString(),
                        Align = TextAnchor.LowerCenter,
                        Color = UiHelper.textColor,
                        Font = UiHelper.regularFont,
                        FontSize = 8
                    }
                }, itemPanel);

                // owner name
                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.4 0.65",
                        AnchorMax = "0.95 0.9"
                    },
                    Text =
                    {
                        Text = offer.Shop.shopName,
                        Align = TextAnchor.LowerLeft,
                        Color = UiHelper.textColor,
                        FontSize = 10
                    }
                }, itemPanel);

                // stock
                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.4 0.32",
                        AnchorMax = "0.8 0.6"
                    },
                    Text =
                    {
                        Text = $"{String.Format(inStock, offer.stock)} ({String.Format(totalStock, offer.TotalStock)})",
                        Align = TextAnchor.LowerLeft,
                        Color = UiHelper.textColor,
                        Font = UiHelper.regularFont,
                        FontSize = 9
                    }
                }, itemPanel);

                // location, distance
                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.4 0.05",
                        AnchorMax = "0.8 0.3"
                    },
                    Text =
                    {
                        Text = $"{offer.Shop.GridPosition} {offer.GetDistance(player)}m",
                        Align = TextAnchor.LowerLeft,
                        Color = UiHelper.textColor,
                        Font = UiHelper.regularFont,
                        FontSize = 9
                    }
                }, itemPanel);

                // set marker
                result.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.81 0.1",
                        AnchorMax = "0.98 0.5"
                    },
                    Button =
                    {
                        Command = $"{UI_COMMAND} details {offer.Guid}",
                        Color = UiHelper.colorBlue,
                    },
                    Text =
                    {
                        Text = view,
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = UiHelper.buttonTextColor
                    }
                }, itemPanel);

                r++;
                if (r >= r_max)
                {
                    r = 0;
                    c++;
                    if (c >= c_max) break;
                }
            }

            #endregion

            #region Page navigation

            int pages = Mathf.CeilToInt((float)uiCache.offerCache.Count() / (float)itemsPerPage);

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.85 0.01",
                    AnchorMax = "0.94 0.06"
                },
                Text =
                {
                    Align = TextAnchor.LowerRight,
                    Text = GetLangMessage(Lang.PAGE, player, page+1, pages),
                    Color = UiHelper.textColor,
                    Font = UiHelper.regularFont,
                    FontSize = 10
                }
            }, offerPanel);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.945 0.01",
                    AnchorMax = "0.965 0.04"
                },
                Button =
                {
                    Command = page-1 >= 0 ? $"{UI_COMMAND} page {page-1}" : string.Empty,
                    Color = page-1 >= 0 ? UiHelper.colorBlue : UiHelper.greyButtonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = "<-",
                    Color = UiHelper.buttonTextColor,
                    FontSize = 10
                }
            }, offerPanel);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.97 0.01",
                    AnchorMax = "0.99 0.04"
                },
                Button =
                {
                    Command = page+1 < pages ? $"{UI_COMMAND} page {page+1}" : string.Empty,
                    Color = page+1 < pages ? UiHelper.colorBlue : UiHelper.greyButtonColor
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Text = "->",
                    Color = UiHelper.buttonTextColor,
                    FontSize = 10
                }
            }, offerPanel);


            #endregion

            NextFrame(() =>
            {
                CuiHelper.AddUi(player, result);
            });
        }

        private void PatchItemResults(BasePlayer player, IEnumerable<int> items)
        {
            DestroyUi(player, panel: UiHelper.itemSearchPanel);
            var lang = this.lang.GetLanguage(player.UserIDString);

            UICache uiCache = UICache.GetPlayerCache(player);
            string title = uiCache.search == null ? GetLangMessage(Lang.POPULAR_ITEMS, player) :  GetLangMessage(Lang.ITEM_RESULTS, player, $"<i>{uiCache.search}</i>");
            var result = new CuiElementContainer();

            string resultPanel = result.Add(new CuiPanel
            {
                Image = new CuiImageComponent
                {
                    Color = UiHelper.panelColor
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.5",
                    AnchorMax = "0.99 0.84"
                },
            }, UI_PANEL, UiHelper.itemSearchPanel);

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.01 0.92",
                    AnchorMax = "0.3 1",
                },
                Text =
                {
                    Text = title,
                    Align = TextAnchor.LowerLeft,
                    Color = UiHelper.textColor,
                    FontSize = 12
                }
            }, resultPanel);

            int r = 0;
            int c = 0;
            foreach (var itemId in items)
            {
                string itemPanel = result.Add(new CuiPanel
                {
                    Image = new CuiImageComponent
                    {
                        Color = UiHelper.panelColorSec
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{0.01+r*0.15} {0.74-c*0.18}",
                        AnchorMax = $"{0.155+r*0.15} {0.90-c*0.18}"
                    },
                }, resultPanel);

                result.Add(new CuiElement
                {
                    Parent = itemPanel,
                    Components =
                    {
                        GetItemImage(itemId),
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.03 0.5",
                            AnchorMax = "0.03 0.5",
                            OffsetMin = "0 -15",
                            OffsetMax = "30 15"
                        }
                    }
                });

                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.25 0.1",
                        AnchorMax = "0.9 0.85"
                    },
                    Text =
                    {
                        Text = GetItemNameTranslated(itemId, lang),
                        Align = TextAnchor.UpperLeft,
                        Color = UiHelper.textColor,
                        FontSize = 10
                    }
                }, itemPanel);

                result.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    Button =
                    {
                        Command = $"{UI_COMMAND} select_item {itemId}",
                        Color = UiHelper.transparentColor,
                    }
                }, itemPanel);

                r++;
                if (r > 5)
                {
                    r = 0;
                    c++;
                    if (c > 4) break;
                }
            }

            NextFrame(() =>
            {
                CuiHelper.AddUi(player, result);
            });
        }

        private void DrawShopOverlay(BasePlayer player)
        {
            DestroyUi(player, panel: UiHelper.shopOverlay);

            var uiCache = UICache.GetPlayerCache(player);
            var result = new CuiElementContainer();

            #region Overlay + Head

            string rootPanelName = result.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Image =
                {
                    Color = "0 0 0 0.8"
                },
            }, UI_PANEL, UiHelper.shopOverlay);

            string panelName = result.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = "-200 -250",
                    OffsetMax = "200 250"
                },
                Image =
                {
                    Color = UiHelper.panelColorNoAlpha
                },
            }, rootPanelName);

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.05 0.93",
                    AnchorMax = "0.95 1"
                },
                Text =
                {
                    Text = uiCache.currentShop.shopName,
                    Align = TextAnchor.MiddleCenter,
                    Color = UiHelper.textColor,
                    FontSize = 13
                }
            }, panelName);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.95 0.98",
                    AnchorMax = "0.95 0.98",
                    OffsetMin = "-20 -20",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = "X",
                    Align = TextAnchor.MiddleCenter,
                    Color = UiHelper.buttonTextColor,
                    FontSize = 10
                },
                Button =
                {
                    Color = UiHelper.redButtonColor,
                    Close = UiHelper.shopOverlay
                }
            }, panelName);

            #endregion

            #region Info + Buttons

            var away = GetLangMessage(Lang.AWAY, player);
            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.05 0.8",
                    AnchorMax = "0.95 0.9"
                },
                Text =
                {
                    Text = (!_config.showShopOwner ? String.Empty : $"{GetLangMessage(Lang.OWNER, player)}: {GetUsername(uiCache.currentShop.ownerId, player)}\n") +
                        (_config.showGridPos ? $"{GetLangMessage(Lang.LOCATION, player)}: {uiCache.currentShop.GridPosition} ({uiCache.currentShop.GetDistance(player)}m {away})" : $"{uiCache.currentShop.GetDistance(player)}m {away}"),
                    Align = TextAnchor.MiddleLeft,
                    Color = UiHelper.textColor,
                    Font = UiHelper.regularFont,
                    FontSize = 10
                }
            }, panelName);

            result.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.95 0.88",
                    AnchorMax = "0.95 0.88",
                    OffsetMin = "-70 -20",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = GetLangMessage(Lang.SET_MARKER, player).ToUpperInvariant(),
                    Align = TextAnchor.MiddleCenter,
                    Color = UiHelper.buttonTextColorDark,
                    FontSize = 10
                },
                Button =
                {
                    Color = UiHelper.colorYellow,
                    Command = $"{UI_COMMAND} marker {uiCache.currentShop.SerializePosition()}"
                }
            }, panelName);

            #endregion

            result.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.05 0.75",
                    AnchorMax = "0.95 0.8"
                },
                Text =
                {
                    Text = GetLangMessage(Lang.OFFERS, player),
                    Align = TextAnchor.LowerLeft,
                    Color = UiHelper.textColor,
                    FontSize = 13
                }
            }, panelName);

            int r = 0;
            int r_max = 7;

            foreach (var offer in uiCache.currentShop.offers)
            {
                string itemPanel = result.Add(new CuiPanel
                {
                    Image = new CuiImageComponent
                    {
                        Color = UiHelper.panelColorSec
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.05 {0.65-r*0.1}",
                        AnchorMax = $"0.95 {0.74-r*0.1}"
                    },
                }, panelName);

                // offer
                result.Add(new CuiElement
                {
                    Parent = itemPanel,
                    Components =
                    {
                        GetItemImage(offer.offer.ItemId),
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.03 0.5",
                            AnchorMax = "0.03 0.5",
                            OffsetMin = "0 -15",
                            OffsetMax = "30 15"
                        }
                    }
                });

                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.03 0.5",
                        AnchorMax = "0.03 0.5",
                        OffsetMin = "10 -15",
                        OffsetMax = "30 -5"
                    },
                    Text =
                    {
                        Text = offer.offer.Amount.ToString(),
                        Align = TextAnchor.LowerRight,
                        Color = UiHelper.textColor,
                        Font = UiHelper.regularFont,
                        FontSize = 8
                    }
                }, itemPanel);

                // for
                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.13 0.1",
                        AnchorMax = "0.22 0.9"
                    },
                    Text =
                    {
                        Text = GetLangMessage(Lang.FOR, player),
                        Align = TextAnchor.MiddleCenter,
                        Color = UiHelper.textColor,
                        FontSize = 10
                    }
                }, itemPanel);

                // Cost
                result.Add(new CuiElement
                {
                    Parent = itemPanel,
                    Components =
                    {
                        GetItemImage(offer.cost.ItemId),
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.23 0.5",
                            AnchorMax = "0.23 0.5",
                            OffsetMin = "0 -15",
                            OffsetMax = "30 15"
                        }
                    }
                });

                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.25 0.5",
                        AnchorMax = "0.25 0.5",
                        OffsetMin = "15 -15",
                        OffsetMax = "40 -5"
                    },
                    Text =
                    {
                        Text = offer.cost.Amount.ToString(),
                        Align = TextAnchor.LowerCenter,
                        Color = UiHelper.textColor,
                        Font = UiHelper.regularFont,
                        FontSize = 8
                    }
                }, itemPanel);

                result.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.4 0.1",
                        AnchorMax = "0.7 0.9"
                    },
                    Text =
                    {
                        Text = $"{GetLangMessage(Lang.ITEM_STOCK, player, offer.stock)}\n{GetLangMessage(Lang.TOTAL_STOCK, player, offer.TotalStock)}",
                        Align = TextAnchor.MiddleLeft,
                        Color = UiHelper.textColor,
                        Font = UiHelper.regularFont,
                        FontSize = 10
                    }
                }, itemPanel);

                r++;
                if (r >= r_max) break;
            }

            CuiHelper.AddUi(player, result);
        }

        private void DestroyUi(BasePlayer player, bool force = false, string panel = null)
        {
            if (openUis.Contains(player.userID) || force)
            {
                CuiHelper.DestroyUi(player, panel ?? UI_PANEL);
                if (panel == null)
                {
                    openUis.Remove(player.userID);
                }
            }
        }

        private static class UiHelper
        {
            public const string shopOverlay = "markteplace.overlay";
            public const string itemSearchPanel = "markteplace.itemsearch";
            public const string offerPanel = "markteplace.offers";

            public const string colorBlue = "0.13 0.52 0.816 0.9";
            public const string colorYellow = "1 0.79 0.227 0.9";

            public const string greenButtonColor = "0.415 0.5 0.258 1";
            public const string redButtonColor = "0.8 0.254 0.254 1";
            public const string greyButtonColor = "0.4 0.4 0.4 1";
            public const string transparentColor = "0 0 0 0";

            public const string buttonTextColor = "0.8 0.8 0.8 1";
            public const string buttonTextColorDark = "0.2 0.2 0.2 1";
            public const string textColor = "0.9 0.9 0.9 1";
            public const string greyTextColor = "0.7 0.7 0.7 1";

            public const string bgColor = "0.08 0.08 0.08 1";

            public const string panelColor = "0.22 0.22 0.22 0.9";
            public const string panelColorNoAlpha = "0.22 0.22 0.22 1";
            public const string panelColorSec = "0.15 0.15 0.15 1";

            public const string regularFont = "robotocondensed-regular.ttf";
            public const string boldFont = "robotocondensed-bold.ttf";
        }

        #endregion

        #region Helpers

        [Conditional("DEBUG")]
        private static void DPrint(string s)
        {
            _instance?.Puts(s);
        }

        private static VendingOffer.SortMode GetPlayerSortMode(BasePlayer player)
        {
            if (!_instance.playerSortModes.TryGetValue(player.userID, out var mode))
            {
                mode = VendingOffer.SortMode.Distance;
            }

            return mode;
        }

        private void ChangePlayerSortMode(BasePlayer player)
        {
            var mode = GetPlayerSortMode(player);

            var arr = (VendingOffer.SortMode[])Enum.GetValues(typeof(VendingOffer.SortMode));
            int idx = Array.IndexOf(arr, mode) + 1;

            playerSortModes[player.userID] = idx >= arr.Length ? arr[0] : arr[idx];
        }

        private string GetUsername(ulong id, BasePlayer langPlayer)
        {
            if (id == 0)
            {
                return $"<i>{GetLangMessage(Lang.NPC, langPlayer)}</i>";
            }

            return BasePlayer.FindAwakeOrSleeping(id.ToString())?.displayName ?? $"<i>{GetLangMessage(Lang.UNKNOWN, langPlayer)}</i>";
        }

        private void AddMarker(BasePlayer player, Vector3 pos)
        {
            if (pos == default || player.State.pointsOfInterest?.Count >= ConVar.Server.maximumMapMarkers)
            {
                return;
            }

            if (player.State.pointsOfInterest == null)
            {
                player.State.pointsOfInterest = new List<MapNote>();
            }
            else
            {
                // Check if marker is possible duplicate
                foreach (var m in player.State.pointsOfInterest)
                {
                    if ((m.worldPosition - pos).sqrMagnitude < 1)
                    {
                        return;
                    }
                }
            }

            var marker = new MapNote
            {
                icon = 1,
                colourIndex = 5,
                worldPosition = pos,
                label = "SHOP",
                noteType = 1
            };

            player.State.pointsOfInterest.Add(marker);
            player.DirtyPlayerState();
            player.SendMarkersToClient();
        }

        private CuiImageComponent GetItemImage(int itemId)
        {
            return new CuiImageComponent { ItemId = itemId };
        }

        #endregion

        #region Lang

        class Lang
        {
            public const string OWNER = "owner";
            public const string LOCATION = "location";
            public const string SET_MARKER = "set_marker";
            public const string MARKETPLACE = "marketplace";
            public const string UNKNOWN = "unknown";
            public const string NPC = "npc";
            public const string POPULAR_ITEMS = "popular_items";
            public const string ITEM_RESULTS = "item_results";
            public const string AWAY = "away";
            public const string FOR = "for";
            public const string VIEW = "view";
            public const string ITEM_STOCK = "in_stock";
            public const string TOTAL_STOCK = "total_stock";
            public const string OFFERS = "offers";
            public const string OFFERS_FOR = "offers_for";
            public const string SORT_BY = "sort_by";
            public const string DISTANCE = nameof(VendingOffer.SortMode.Distance);
            public const string PRICE = nameof(VendingOffer.SortMode.Price);
            public const string AMOUNT = nameof(VendingOffer.SortMode.Amount);
            public const string SEARCH = "search_items";
            public const string OPEN_MARKETPLACE = "open_marketplace";
            public const string PAGE = "page_nav";
        }

        private void RegisterLangMessages()
        {
            lang.RegisterMessages(new()
            {
                { Lang.OWNER, "Owner" },
                { Lang.LOCATION, "Location" },
                { Lang.SET_MARKER, "Set Marker" },
                { Lang.MARKETPLACE, "Marketplace" },
                { Lang.UNKNOWN, "Unknown" },
                { Lang.NPC, "NPC" },
                { Lang.POPULAR_ITEMS, "Popular items" },
                { Lang.ITEM_RESULTS, "Results for {0}" },
                { Lang.AWAY, "away" },
                { Lang.FOR, "for" },
                { Lang.VIEW, "View" },
                { Lang.ITEM_STOCK, "{0} in stock" },
                { Lang.TOTAL_STOCK, "{0} total" },
                { Lang.OFFERS, "Offers" },
                { Lang.OFFERS_FOR, "Offers for {0}" },
                { Lang.SORT_BY, "Sort by" },
                { Lang.DISTANCE, "Distance" },
                { Lang.PRICE, "Price" },
                { Lang.AMOUNT, "Amount" },
                { Lang.SEARCH, "Search items" },
                { Lang.OPEN_MARKETPLACE, "Open marketplace" },
                { Lang.PAGE, "Page {0} of {1}" },
            }, this, "en");
        }

        private string GetLangMessage(string key, BasePlayer player, object arg0, object arg1)
        {
            var msg = lang.GetMessage(key, this, player.UserIDString);
            return String.Format(msg, arg0, arg1);
        }

        private string GetLangMessage(string key, BasePlayer player, object arg0)
        {
            var msg = lang.GetMessage(key, this, player.UserIDString);
            return String.Format(msg, arg0);
        }

        private string GetLangMessage(string key, BasePlayer player)
        {
            return lang.GetMessage(key, this, player.UserIDString);
        }

        private string GetItemNameTranslated(int itemId, string lang) => GetItemNameTranslated(ItemManager.FindItemDefinition(itemId), lang);

        private string GetItemNameTranslated(ItemDefinition itemDef, string lang)
        {
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

        #endregion

        #region Search

        class ItemSearch
        {
            private static readonly HashSet<string> supportedLanguages = new()
            {
                "af", "ar", "ca", "cs", "da", "de", "el", "en-PT", "es-ES", "fi", "fr", "he", "hu", "it", "ja", "ko", "nl", "no", "pl", "pt-BR", "pt-PT", "ro", "ru", "sr", "sv-SE", "tr", "uk", "vi", "zh-CN", "zh-TW"
            };

            public readonly Entry Default;

            public readonly ItemDefinition[] ItemList;

            private readonly Dictionary<string, Entry> languages = new();

            public ItemSearch()
            {
                ItemList = ItemManager.itemList.Where(x => !x.hidden).ToArray();
                Default = CreateEntry(def => def.displayName.english);
            }

            public Entry GetEntry(string lang, Plugin translationApi)
            {
                if (languages.TryGetValue(lang, out var entry))
                {
                    return entry;
                }
                else if (translationApi != null && supportedLanguages.Contains(lang))
                {
                    DPrint($"Translation entry for {lang} not found - creating");
                    entry = CreateEntry(def => translationApi.Call<string>("GetItemTranslationByID", lang, def.itemid));
                    languages.Add(lang, entry);
                    return entry;
                }

                DPrint($"Translation entry for {lang} not found - returning default");
                return Default;
            }

            public void AddEntry(string lang, Func<ItemDefinition, string> translate)
            {
                var entry = CreateEntry(translate);
                languages[lang] = entry;
            }

            private Entry CreateEntry(Func<ItemDefinition, string> translate)
            {
                var entry = new Entry();

                foreach (var item in ItemList)
                {
                    var translated = translate(item);
                    if (translated != null)
                    {
                        entry.Add(translated, item.itemid);
                    }
                }

                return entry;
            }

            public class Entry
            {
                private readonly Dictionary<string, MultiValueEntry<int>> itemLookup;

                private readonly HashSet<int> returnedItems;

                public Entry()
                {
                    itemLookup = new Dictionary<string, MultiValueEntry<int>>();
                    returnedItems = new HashSet<int>();
                }

                public void Add(string value, int itemId)
                {
                    if (itemLookup.ContainsKey(value))
                    {
                        itemLookup[value].Add(itemId);
                    }
                    else
                    {
                        itemLookup.Add(value, new MultiValueEntry<int>(itemId));
                    }
                    
                }

                public IEnumerable<int> Search(string prefix, int limit, int containingSearchIfLessThan = 1)
                {
                    //var sw = Stopwatch.StartNew();
                    int count = 0;

                    foreach(var kv in itemLookup)
                    {
                        if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach(var val in kv.Value.GetValues())
                            {
                                returnedItems.Add(val);
                                yield return val;
                                count++;
                            }

                            if (count > limit)
                            {
                                break;
                            }
                        }
                    }

                    if (count < containingSearchIfLessThan)
                    {
                        foreach (var kv in itemLookup)
                        {
                            if (kv.Key.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var val in kv.Value.GetValues())
                                {
                                    if (!returnedItems.Contains(val))
                                    {
                                        yield return val;
                                    }
                                    
                                    count++;
                                }

                                if (count > limit)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    returnedItems.Clear();
                    //sw.Stop();
                    //_instance.Puts($"Search for {prefix} took {sw.Elapsed.TotalSeconds:N2}");
                }

                private class MultiValueEntry<T>
                {
                    private bool _isMultiValue;
                    private T value;
                    private List<T> values = new List<T>();
                    public MultiValueEntry(T value)
                    {
                        this.value = value;
                        _isMultiValue = false;
                    }

                    public void Add(T value)
                    {
                        values ??= new List<T>(7);
                        values.Add(value);

                        if (!_isMultiValue)
                        {
                            values.Add(this.value);
                            this.value = default;
                            _isMultiValue = true;
                        }
                    }

                    public IEnumerable<T> GetValues()
                    {
                        if (!_isMultiValue)
                        {
                            yield return value;
                        }
                        else
                        {
                            foreach(var val in values)
                            {
                                yield return val;
                            }
                        }
                    }
                }
            }
        }

        #endregion

    }
}
