using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ItemSort", "Tangerine", "1.0.9")]
    [Description("Universal Item Sort")]
    class UItemSort : RustPlugin 
    {
        #region Classes
        
        private class PluginConfig
        {
            [JsonProperty("Use button images?")] 
            public bool UseImages = true;
            [JsonProperty("Send plugin messages/reply?")]
            public bool PluginReply = true;
            [JsonProperty("Sort button color.")]
            public string SortBtnColor = "0.96 0.92 0.88 0.2";
            [JsonProperty("Take similar button color.")]
            public string SimilarBtnColor = "0.96 0.92 0.88 0.2";
            [JsonProperty("Take all button color.")]
            public string AllBtnColor = "0.96 0.92 0.88 0.2";
            [JsonProperty("Sort image.")] 
            public string SortImg = "https://i.imgur.com/uEiuf3N.png";
            [JsonProperty("Similar image.")] 
            public string SimilarImg = "https://i.imgur.com/3PkbAH8.png";
            [JsonProperty("Take/Put all.")] 
            public string AllImg = "https://i.imgur.com/beKBAl3.png";
            [JsonProperty("Allow only containers with owners?")] 
            public bool OwnedContainers = false;
            [JsonProperty("All containers allowed?")] 
            public bool AllAllowed = false;
            [JsonProperty("Black list if all allowed else this is white list.", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
            public string[] BlackListedItems = new []
            {
                "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab",
                "assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab", 
                "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
                "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab",
                "assets/prefabs/deployable/research table/researchtable_deployed.prefab",
                "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
                "assets/prefabs/deployable/furnace.large/furnace.large.prefab",
                "assets/prefabs/misc/twitch/hobobarrel/hobobarrel.deployed.prefab",
                "assets/prefabs/deployable/furnace/furnace.prefab",
                "assets/prefabs/deployable/fridge/fridge.deployed.prefab",
                "assets/prefabs/deployable/campfire/campfire.prefab",
                "assets/prefabs/deployable/bbq/bbq.deployed.prefab",
                "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab",
                "assets/prefabs/deployable/fireplace/fireplace.deployed.prefab",
                "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab",
                "assets/prefabs/npc/flame turret/flameturret.deployed.prefab",
                "assets/prefabs/npc/autoturret/autoturret_deployed.prefab",
                "assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab",
                "assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab",
            };
        }
        
        #endregion

        #region Variables
        
        private const string permissionUse = "uitemsort.use";
        private const string permissionIgnore = "uitemsort.ignore";
        private const string UI_Layer = "UI_UItemSort";
        private const string UI_LayerMain = "UI_UItemSortMain";

        private static PluginConfig _config;

        private const int BaseLanguageId = 10;

        private readonly Dictionary<string, string> _customLanguageIds = new();
        
        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }

                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("OnConfigValidate") != null)
            {
                PrintWarning("Using default configuration...");
                _config = GetDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
            };
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UILootSimilar"] = "Same items, that you already own, were looted! (if exist)",
                ["UILootAll"] = "You've looted everything you could!",
                ["UISort"] = "Items were successfully sorted!",
                ["UIPutSimilar"] = "You've put same items that are in a storage! (if exist)",
                ["UIPutAll"] = "You've put all the items you could!",
                ["UIBtnSort"] = "Sort",
                ["UIBtnTakeSimilar"] = "Take Similar",
                ["UIBtnTakeAll"] = "Take All",
                ["UIBtnPutSimilar"] = "Put Similar",
                ["UIBtnPutAll"] = "Put All",
            }, this, "en");
        }

        private string GetLocal(string mesKey, string userId = null) => lang.GetMessage(mesKey, this, userId);

        #endregion
        
        #region Utils
        
        public string GetMessageByCode(string key, string keyCode = null)
        {
            return lang.GetMessage(key, this, _customLanguageIds.GetValueOrDefault(keyCode));
        }
        
        private void MoveItems(ItemContainer from, ItemContainer to)
        {
            var storageItems = from.itemList;
            var itemList = to.itemList;
            
            int i = storageItems.Count - 1;
            while (itemList.Count < to.capacity)
            {
                if (i < 0)
                    break;

                var storageItem = storageItems[i];
                storageItem.MoveToContainer(to);
                i--;
            }
        }
        
        private void MoveSimilarItems(ItemContainer from, ItemContainer to)
        {
            var storageItems = from.itemList;

            for (int i = storageItems.Count - 1; i >= 0; i--)
            {
                var contItem = storageItems[i];
                if (to.GetAmount(contItem.info.itemid, false) > 0)
                {
                    contItem.MoveToContainer(to);
                }

                if (to.itemList.Count >= to.capacity)
                    break; // inventory full;
            }
        }

        private void SortItemContainer(BasePlayer player, ItemContainer container)
        {
            if (container == null)
                return;

            var storageItems = Facepunch.Pool.Get<List<Item>>();

            try
            {
                storageItems.AddRange(container.itemList);
                
                for (var i = storageItems.Count - 1; i >= 0; i--)
                {
                    storageItems[i].RemoveFromContainer();
                }

                if (container.entityOwner is AutoTurret) // Fast and temporary fix ( weapon should be put into turret first )
                {
                    storageItems.Sort((item1, item) => string.Compare(item.info.shortname, item1.info.shortname, StringComparison.Ordinal));
                }
                else
                {
                    storageItems.Sort((item, item1) => string.Compare(item.info.shortname, item1.info.shortname, StringComparison.Ordinal));
                }

                for (var index = 0; index < storageItems.Count; index++)
                {
                    var storageItem = storageItems[index];
                    if (storageItem.MoveToContainer(container) == false)
                    {
                        storageItem.Drop(player.GetDropPosition(), player.GetDropVelocity());
                    }
                }

                container.MarkDirty();
            }
            catch
            {
                //no check
            }
            finally
            {
                Facepunch.Pool.FreeUnmanaged(ref storageItems);
            }
        }

        private void RegPermission(string name)
        {
            if (permission.PermissionExists(name)) return;
            permission.RegisterPermission(name, this);
        }
        
        private bool HasPermission(BasePlayer player, string name)
        {
            return permission.UserHasPermission(player.UserIDString, name);
        }

        #endregion
        
        #region Hooks
        void Init()
        {
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnPlayerLootEnd));
            Unsubscribe(nameof(OnLootEntityEnd));
        }
        
        void OnServerInitialized()
        {
            RegPermission(permissionUse);
            RegPermission(permissionIgnore);

            var strings = lang.GetLanguages();
            for (var i = 0; i < strings.Length; i++)
            {
                var language = strings[i];
                var languageId = (BaseLanguageId + i).ToString();
                lang.SetLanguage(language, languageId);
                _customLanguageIds.Add(language, languageId);
            }

            StartPluginLoad();
        }

        private bool IsInFront(Vector3 forwardVector, Vector3 entityPosition, Vector3 targetPosition)
        {
            var directionToTarget = entityPosition - targetPosition;
            var angle = Vector3.Angle(forwardVector, directionToTarget);
            if (Mathf.Abs(angle) < 90)
                return true;

            return false;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        { 
            if (HasPermission(player, permissionUse) == false)
                return;

            if (HasPermission(player, permissionIgnore))
                return;

            if (_config.OwnedContainers && entity.OwnerID == 0)
                return;

            if (IsValidEntity(entity.PrefabName) == false)
                return;
            
            var machineTransform = entity.transform;
            var vendingPosition = machineTransform.position;
            if ((entity is DropBox || entity is VendingMachine || entity is NPCShopKeeper) && 
                IsInFront(machineTransform.forward * -1, vendingPosition, player.transform.position))
                return;

            DrawUI(player);
        }
        
        private void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (!container)
                return;
            
            DestroyUI(player);
        }
        
        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player = inventory._baseEntity;
            if (player == null)
                return;
            
            DestroyUI(player);
        }

        private bool IsValidEntity(string prefab)
        {
            if (string.IsNullOrWhiteSpace(prefab))
                return false;

            if (_config.AllAllowed && _config.BlackListedItems.Contains(prefab))
                return false;
            
            if (_config.AllAllowed == false && _config.BlackListedItems.Contains(prefab) == false)
                return false;

            return true;
        }

        void OnBackpackOpened(BasePlayer player, object backpackOwnerID, ItemContainer backpackContainer) 
        {
            if (HasPermission(player, permissionUse) == false)
                return;

            if (HasPermission(player, permissionIgnore))
                return;
            
            DrawUI(player);
        }
        
        void OnBackpackClosed(BasePlayer player, object backpackOwnerID, ItemContainer backpackContainer)
        {
            DestroyUI(player);
        }
        #endregion
        
        #region PluginLoading v0.0.4

        private static bool _initiated = false;
        private List<string> _imagesLoading = new List<string>();
        
        private void StartPluginLoad()
        {
            if (ImageLibrary != null)
            {
                //Load your images here
                if (_config.UseImages)
                {
                    AddImage(_config.SortImg);
                    AddImage(_config.SimilarImg);
                    AddImage(_config.AllImg);
                }
                CheckStatus();
            }
            else
            {
                PrintError($"ImageLibrary not found! Please, check your plugins list.");
            }
        }

        private void CheckStatus()
        {
            int loadedImages = 0;
            foreach (var value in _imagesLoading)
            {
                if (HasImage(value) == false)
                    continue;

                loadedImages++;
            }
            
            if (loadedImages < _imagesLoading.Count - 1 && (bool)ImageLibrary.Call("IsReady") == false)
            {
                PrintWarning($"Plugin is not ready! Loaded: {loadedImages}/{_imagesLoading.Count} images.");
                timer.Once(10f, CheckStatus);
            }
            else
            {
                FullLoad();
                PrintWarning("Plugin successfully loaded! Author: Tangerine.");
            }
        }

        private void FullLoad()
        {
            BuildInterface();
            Subscribe(nameof(OnLootEntity));
            Subscribe(nameof(OnPlayerLootEnd));
            Subscribe(nameof(OnLootEntityEnd));
        }
        
        #region ImageLibrary
        [PluginReference] private Plugin ImageLibrary;
        
        private string GetSizedImage(string name, int size) => GetImage($"{name}_{size}");
        
        private void AddImage(string url, string name, int size)
        {
            if (!(bool)ImageLibrary.Call("HasImage", $"{name}_{size}"))
                ImageLibrary.Call("AddImage", url, $"{name}_{size}");
        }
        
        private string GetImage(string name)
        {
            string ID = (string)ImageLibrary?.Call("GetImage", name);
            if (ID == "")
                ID = (string)ImageLibrary?.Call("GetImage", name) ?? ID;
        
            return ID;
        }
        
        private void AddImage(string name)
        {
            if (HasImage(name))
                return;
            
            ImageLibrary?.Call("AddImage", name, name);
            _imagesLoading.Add(name);
        }

        private bool HasImage(string name) => (bool)(ImageLibrary?.Call("HasImage", name) ?? false);

        #endregion

        
        #endregion 
        
        #region Interface

        private readonly Dictionary<string, string> _languageToUi = new();
        
        private void BuildInterface() 
        {
            var container = new CuiElementContainer
            {
                {
                    new CuiElement
                    {
                        Parent = "Overlay",
                        Name = UI_Layer,
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = "1 0 0 0"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0",
                                AnchorMax = "0.5 0",
                                OffsetMin = "199 86",
                                OffsetMax = "382 108"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = $"{UI_Layer}.SortBtn",
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = _config.SortBtnColor, Command = "UI_Sort sortloot"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "0 0",
                                OffsetMax = "58 22"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.SortBtn",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(_config.SortImg), Color = "%imc%"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 1",
                                OffsetMin = "0 0",
                                OffsetMax = "22 0"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.SortBtn",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "%ls%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 12
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "%sbo% 0",
                                OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1", Distance = "0.5 0.5"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = $"{UI_Layer}.TakeSimilar",
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = _config.SimilarBtnColor, Command = "UI_Sort takesimilar"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "62 0",
                                OffsetMax = "120 22"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.TakeSimilar",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(_config.SimilarImg), Color = "%imc%"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 1",
                                OffsetMin = "0 0",
                                OffsetMax = "22 0"
                            },
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.TakeSimilar",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "%lts%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "%sbo% 0",
                                OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1", Distance = "0.5 0.5"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = $"{UI_Layer}.TakeAll",
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = _config.AllBtnColor, Command = "UI_Sort takeall"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "124 0",
                                OffsetMax = "182 22"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.TakeAll",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(_config.AllImg), Color = "%imc%"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 1",
                                OffsetMin = "0 0",
                                OffsetMax = "22 0"
                            },
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.TakeAll",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "%lta%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "%sbo% 0",
                                OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1", Distance = "0.5 0.5"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = "Overlay",
                        Name = UI_LayerMain,
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = "1 0 0 0"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0",
                                AnchorMax = "0.5 0",
                                OffsetMin = "-72 342",
                                OffsetMax = "116 364"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_LayerMain,
                        Name = $"{UI_LayerMain}.SortMain",
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = _config.SortBtnColor, Command = "UI_Sort sortmain"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "0 0",
                                OffsetMax = "60 22"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_LayerMain}.SortMain",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(_config.SortImg), Color = "%imc%"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 1",
                                OffsetMin = "0 0",
                                OffsetMax = "22 0"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_LayerMain}.SortMain",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "%ls%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 12
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "%sbo% 0",
                                OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1", Distance = "0.5 0.5"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_LayerMain,
                        Name = $"{UI_LayerMain}.PutSimilar",
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = _config.SimilarBtnColor, Command = "UI_Sort movesimilar"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "64 0",
                                OffsetMax = "124 22"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_LayerMain}.PutSimilar",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(_config.SimilarImg), Color = "%imc%"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 1",
                                OffsetMin = "0 0",
                                OffsetMax = "22 0"
                            },
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_LayerMain}.PutSimilar",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "%lps%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "%sbo% 0",
                                OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1", Distance = "0.5 0.5"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_LayerMain,
                        Name = $"{UI_LayerMain}.PutAll",
                        Components =
                        {
                            new CuiButtonComponent()
                            {
                                Color = _config.AllBtnColor, Command = "UI_Sort moveall"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "128 0",
                                OffsetMax = "188 22"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_LayerMain}.PutAll",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(_config.AllImg), Color = "%imc%"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 1",
                                OffsetMin = "0 0",
                                OffsetMax = "22 0"
                            },
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_LayerMain}.PutAll",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "%lpa%", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 9
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "%sbo% 0",
                                OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1", Distance = "0.5 0.5"
                            }
                        }
                    }
                },
            };
            var json = container.ToJson();
            
            foreach (var customLanguageId in _customLanguageIds)
            {
                var langCode = customLanguageId.Key;
                var gui = json;
                
                gui = gui.Replace("{UILootAll}", GetMessageByCode("UILootAll", langCode));
            
                gui = gui.Replace("%ls%", GetMessageByCode("UIBtnSort", langCode));
                gui = gui.Replace("%lts%", GetMessageByCode("UIBtnTakeSimilar", langCode));
                gui = gui.Replace("%lta%", GetMessageByCode("UIBtnTakeAll", langCode));
                gui = gui.Replace("%lps%", GetMessageByCode("UIBtnPutSimilar", langCode));
                gui = gui.Replace("%lpa%", GetMessageByCode("UIBtnPutAll", langCode)); 
                
                if (_config.UseImages == false)
                {
                    gui = gui.Replace("%imc%", "0 0 0 0");
                    gui = gui.Replace("%sbo%", "0");
                }
                else
                {
                    gui = gui.Replace("%imc%", "1 1 1 1");
                    gui = gui.Replace("%sbo%", "22"); 
                }
                
                _languageToUi.Add(langCode, gui); 
            }
            
        }

        private void DrawUI(BasePlayer player)
        {
            var playerLang = lang.GetLanguage(player.UserIDString);
            DestroyUI(player);
            CuiHelper.AddUi(player, _languageToUi.GetValueOrDefault(playerLang) ?? _languageToUi.GetValueOrDefault(lang.GetServerLanguage()));
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_Layer);
            CuiHelper.DestroyUi(player, UI_LayerMain);
        }

        #endregion
        
        #region Commands

        [ConsoleCommand("UI_Sort")]
        private void Console_SortItems(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg?.Player();
            if (player == null)
                return;

            if (arg.Args == null || arg.Args.Length < 1)
                return;
            
            var containers = player.inventory.loot.containers;
            if (containers == null)
                return;

            if (HasPermission(player, permissionUse) == false)
                return;

            if (HasPermission(player, permissionIgnore))
                return;
            
            switch (arg.Args[0])
            {
                case "sortloot":
                {
                    for (var i = 0; i < containers.Count; i++)
                        SortItemContainer(player, containers[i]);
                    
                    if(_config.PluginReply)
                        SendReply(player, GetLocal("UISort", player.UserIDString));
                    break;
                }
                case "sortmain":
                {
                    SortItemContainer(player, player.inventory.containerMain);
                    
                    if(_config.PluginReply)
                        SendReply(player, GetLocal("UISort", player.UserIDString));
                    break;
                }
                case "takesimilar":
                {
                    for (var i = 0; i < containers.Count; i++)
                        MoveSimilarItems(containers[i], player.inventory.containerMain);
                    
                    if(_config.PluginReply)
                        SendReply(player, GetLocal("UILootSimilar", player.UserIDString));
                    break;
                }
                case "takeall":
                {
                    for (var i = 0; i < containers.Count; i++)
                        MoveItems(containers[i], player.inventory.containerMain);
                    
                    if(_config.PluginReply)
                        SendReply(player, GetLocal("UILootAll", player.UserIDString));
                    break;
                }
                case "movesimilar":
                {
                    for (var i = 0; i < containers.Count; i++)
                        MoveSimilarItems(player.inventory.containerMain, containers[i]);
                    
                    if(_config.PluginReply)
                        SendReply(player, GetLocal("UIPutSimilar", player.UserIDString));
                    break;
                }
                case "moveall":
                {
                    for (var i = 0; i < containers.Count; i++)
                        MoveItems(player.inventory.containerMain, containers[i]);
                    
                    if(_config.PluginReply)
                        SendReply(player, GetLocal("UIPutAll", player.UserIDString));
                    break;
                }
            }
            
        }

        #endregion
    }
}