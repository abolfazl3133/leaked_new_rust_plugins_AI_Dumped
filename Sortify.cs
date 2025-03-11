// Reference: 0Harmony
using Facepunch.Extend;
using HarmonyLib;
using Network;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sortify", "https://discord.gg/TrJ7jnS233", "1.0.1")]
    partial class Sortify : RustPlugin
    {
        #region References
        [PluginReference]
        private RustPlugin ImageLibrary;
        #endregion

        #region Consts
        public const string PERM_TO_USE = "sortify.allow";
        public const ItemCategory ALL_CATEGORIES = (ItemCategory)(-1);
        public const ItemCategory SKINNED_CATEGORY = (ItemCategory)(1378);
        public const int CUSTOM_CATEGORIES_START = 243572345;
        #region You can use these flags in your own plugins to disable this plugin in your containers
        public const ItemContainer.Flag NoSorting = (ItemContainer.Flag)(8192);
        public const ItemContainer.Flag NoFiltering = (ItemContainer.Flag)(16384);
        public const ItemContainer.Flag NoSortify = NoSorting | NoFiltering;
        #endregion
        #endregion

        #region Variables
        static Sortify Instance;
        private int[] SortIndexes;
        internal readonly List<SortButtonPreset> SortButtons = new List<SortButtonPreset> { new SortButtonPreset(-363689972, ALL_CATEGORIES), new SortButtonPreset(-596876839, SKINNED_CATEGORY), new SortButtonPreset(1545779598, ItemCategory.Weapon), new SortButtonPreset(1525520776, ItemCategory.Construction), new SortButtonPreset(-180129657, ItemCategory.Items), new SortButtonPreset(69511070, ItemCategory.Resources), new SortButtonPreset(-2002277461, ItemCategory.Attire), new SortButtonPreset(1176355476, ItemCategory.Tool), new SortButtonPreset(254522515, ItemCategory.Medical), new SortButtonPreset(-1848736516, ItemCategory.Food), new SortButtonPreset(-1211166256, ItemCategory.Ammunition), new SortButtonPreset(-582782051, ItemCategory.Traps), new SortButtonPreset(479292118, ItemCategory.Misc), new SortButtonPreset(73681876, ItemCategory.Component), new SortButtonPreset(-144417939, ItemCategory.Electrical), new SortButtonPreset(-1486461488, ItemCategory.Fun) };
        #endregion

        #region All Category Icon
        private void LoadAllCategoryIcon()
        {
            SortButtons[0].str = FileStorage.server.Store(Convert.FromBase64String("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"), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
        }

        #endregion

        void Init()
        {
            Instance = this;
            permission.RegisterPermission(PERM_TO_USE, this);
            cmd.AddConsoleCommand("sortify.ui.cmd", this, (ConsoleSystem.Arg arg) =>
            {
                BasePlayer user = arg.Player();
                if (user == null) return false;
                PlayersDatabase.Get(user)?.HandleUICommand(arg.Args);
                return true;
            });
            SetupSortIndexes();
        }

        void Loaded()
        {
            // Inicializa Harmony con el nombre del plugin
            HarmonyPatcher.Init(this.Name);

            // Aplica los parches configurados en HarmonyPatcher
            HarmonyPatcher.Patch();
        }


        void OnServerInitialized(bool initial)
        {
            LoadAllCategoryIcon();
            var customCategories = config.Categories;
            if (ImageLibrary != null)
            {
                ImageLibrary.Call("ImportImageList", new object[] { this.Name, customCategories.ToDictionary(v => v.IconUrl, v => v.IconUrl), 0UL, false, new Action(OnImageBatchCompleted) });
            }
            else if (customCategories.Count > 0)
            {
                PrintError("You do not have ImageLibrary installed, you need to install ImageLibrary to display icons for custom categories.");
            }
        }


        void Unload()
        {
            HarmonyPatcher.UnpatchAll();
            foreach (var player in BasePlayer.activePlayerList)
                DisposePlayerData(player);
            PlayersDatabase.Players.Clear();
        }

        void OnImageBatchCompleted()
        {
            for (int i = 0; i < config.Categories.Count; i++)
            {
                var customCategory = config.Categories.ElementAt(i);
                SortButtons.Add(new SortButtonPreset(customCategory.IconUrl, (ItemCategory)(CUSTOM_CATEGORIES_START + i)));
            }
        }

        void OnPlayerLootEnd(PlayerLoot inventory) => DisposePlayerData(inventory.baseEntity);
        void OnPlayerConnected(BasePlayer player) => DisposePlayerData(player);


        #region Methods

        private void SetupSortIndexes()
        {
            List<ItemCategory> AllItemCategories = Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToList();
            AllItemCategories.Sort((a, b) => a.ToString().CompareTo(b.ToString()));

            SortIndexes = new int[AllItemCategories.Count];
            for (int i = 0; i < AllItemCategories.Count; i++)
                SortIndexes[(int)AllItemCategories[i]] = i;

        }
        void DisposePlayerData(BasePlayer player)
        {
            PlayersDatabase.PlayerData playerData = PlayersDatabase.Get(player);
            if (playerData == null)
                return;
            playerData.Dispose();
        }
        #endregion

        #region Classes
        public class SortButtonPreset
        {
            public string str;
            public int itemId;
            public string identificator;

            public SortButtonPreset(string str, ItemCategory category) : this(str, ((int)category).ToString()) { }
            public SortButtonPreset(int itemId, ItemCategory category) : this(itemId, ((int)category).ToString()) { }

            public SortButtonPreset(string str, string identificator)
            {
                this.str = str;
                this.identificator = identificator;
            }
            public SortButtonPreset(int itemId, string identificator)
            {
                this.itemId = itemId;
                this.identificator = identificator;
            }
        }

        public static class HarmonyPatcher
        {
            private static string Name;
            private static Harmony Instance;

            public static void Init(string name)
            {
                Name = name;
                Instance = new Harmony(name); // Crea la instancia de Harmony
            }

            public static void Patch()
            {
                // Patching ClientRPCSend
                var clientRpcSendMethod = AccessTools.Method(typeof(BaseEntity), "ClientRPCSend");
                if (clientRpcSendMethod != null)
                {
                    var clientRpcSendPrefix = new HarmonyMethod(typeof(Patches), nameof(Patches.ClientRPCSend));
                    Instance.Patch(clientRpcSendMethod, prefix: clientRpcSendPrefix);
                }

                // Patching MoveToContainer
                var moveToContainerMethod = AccessTools.Method(typeof(Item), "MoveToContainer");
                if (moveToContainerMethod != null)
                {
                    var moveToContainerPrefix = new HarmonyMethod(typeof(Patches), nameof(Patches.MoveToContainer));
                    Instance.Patch(moveToContainerMethod, prefix: moveToContainerPrefix);
                }

                // Ejemplo comentado para transpilers
                /*
                var clientRpcExMethod = typeof(BaseEntity).GetMethods()
                    .FirstOrDefault(m => m.IsGenericMethod && m.Name == "ClientRPCEx" && m.GetParameters().Length == 4)?
                    .MakeGenericMethod(typeof(PlayerUpdateLoot));
                if (clientRpcExMethod != null)
                {
                    var clientRpcExTranspiler = new HarmonyMethod(typeof(Patches), nameof(Patches.ClientRPCEx));
                    Instance.Patch(clientRpcExMethod, transpiler: clientRpcExTranspiler);
                }
                */
            }

            public static void UnpatchAll()
            {
                if (Instance != null)
                {
                    Instance.UnpatchAll(Name);
                }
            }
        }

        public static class PlayersDatabase
        {
            public class PlayerData : IDisposable
            {
                public ItemCategory category = ALL_CATEGORIES;

                BasePlayer player;
                PlayerLoot playerLoot;
                private int False;

                public PlayerData(BasePlayer player)
                {
                    this.player = player;
                    this.playerLoot = player.inventory.loot;
                }

                public bool IsLootingContainer(ItemContainer container)
                {
                    if (!playerLoot.IsLooting()) return false;
                    return playerLoot.containers.Contains(container);
                }

                public void Dispose()
                {
                    if (player.IsConnected)
                        CuiHelper.DestroyUi(player, "S&F");
                    Remove(player);
                }

                public void HandleUICommand(string[] args)
                {
                    switch (args[0])
                    {
                        case "filter":
                            int category;
                            if (int.TryParse(args[1], out category))
                            {
                                this.category = (ItemCategory)category;
                            }
                            playerLoot.MarkDirty();
                            break;
                        case "sort":
                            foreach (var container in playerLoot.containers)
                            {
                                container.itemList.Sort((a, b) => Instance.SortIndexes[(int)a.info.category].CompareTo(Instance.SortIndexes[(int)b.info.category]));
                                for (int i = 0; i < container.itemList.Count; i++)
                                    container.itemList[i].position = i;

                            }
                            playerLoot.MarkDirty();
                            break;
                    }
                    return;
                }


                public void RenderUI()
                {
                    if (player == null) return;
                    string textColor = "0.9058824 0.8745099 0.8392158 1";


                    var cui = new CUI();
                    var __16 = cui.AddContainer(
                        anchorMin: "0.5 0",
                        anchorMax: "0.5 0",
                        offsetMin: "572 112",
                        offsetMax: "636 667",
                        parent: "Hud.Menu",
                        name: "S&F");


                    int columns = 2;
                    int rows = Mathf.CeilToInt(Instance.SortButtons.Count / (float)columns);
                    if (!playerLoot.containers.Last()?.HasFlag(NoFiltering) == true)
                    {
                        for (int i = 0; i < Instance.SortButtons.Count; i++)
                        {
                            SortButtonPreset buttonPreset = Instance.SortButtons[i];
                            var sortButtonPanel = cui.AddButton(
                                  command: $"sortify.ui.cmd filter {buttonPreset.identificator}",
                                  color: "0.969 0.922 0.882 0.035",
                                  material: "assets/icons/greyout.mat",
                                  anchorMin: "0 0",
                                  anchorMax: "0 0",
                                  offsetMin: $"{(i % columns) * 35} {(35 * (rows - (i / columns) - 1))}",
                                  offsetMax: $"{((i % columns) + 1) * 35 - 5} {(35 * (rows - (i / columns))) - 5}",
                                  parent: __16);
                            if (buttonPreset.str != null)
                            {
                                if (i == 0)
                                {
                                    cui.AddHImage(
                                     content: buttonPreset.str,
                                     color: textColor,
                                     offsetMin: "4 4",
                                     offsetMax: "-4 -4",
                                     parent: sortButtonPanel);
                                }
                                else
                                {
                                    cui.AddImage(
                                      content: buttonPreset.str.StartsWith("http") ? Instance.ImageLibrary?.Call<string>("GetImage", new object[] { buttonPreset.str }) : buttonPreset.str,
                                      offsetMin: "2 2",
                                      offsetMax: "-2 -2",
                                      parent: sortButtonPanel);
                                }
                                
                            }
                            else if (buttonPreset.itemId != False)
                            {
                                cui.AddIcon(
                                    itemId: buttonPreset.itemId,
                                    offsetMin: "2 2",
                                    offsetMax: "-2 -2",
                                    parent: sortButtonPanel);
                            }
                        }
                    }

                    if (playerLoot.containers.Last()?.HasFlag(NoSorting) == false || playerLoot.containers.Last()?.HasFlag(NoFiltering) == false)
                    {
                        cui.AddColorPanel(
                            color: "1 1 1 0.03",
                            material: "assets/icons/greyout.mat",
                            anchorMin: "0 0",
                            anchorMax: "1 0",
                            offsetMin: $"0 {rows * 35}",
                            offsetMax: $"0 {rows * 35 + 2}",
                            parent: __16);
                    }

                    if (playerLoot.containers.Last()?.HasFlag(NoSorting) == false)
                    {
                        var sortInvButtonPanel = cui.AddButton(
                            command: $"sortify.ui.cmd sort",
                            color: "0.969 0.922 0.882 0.035",
                            material: "assets/icons/greyout.mat",
                            anchorMin: "0 0",
                            anchorMax: "1 0",
                            offsetMin: $"0 {rows * 35 + 6}",
                            offsetMax: $"0 {rows * 35 + 26}",
                            parent: __16);

                        cui.AddText(
                            text: "SORT",
                            color: textColor,
                            font: CUI.Font.RobotoCondensedRegular,
                            align: TextAnchor.MiddleCenter,
                            offsetMin: "2 2",
                            offsetMax: "-2 -2",
                            parent: sortInvButtonPanel);
                    }

                    cui.RenderWithDestroy(player);
                }
            }

            internal static Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
            public static PlayerData GetOrCreate(BasePlayer player, ItemContainer container)
            {
                PlayerData data = null;
                if (!Players.TryGetValue(player.userID, out data))
                    Players.Add(player.userID, data = new PlayerData(player));
                return data;
            }
            public static PlayerData Get(BasePlayer player)
            {
                if (player == null)
                    return null;
                PlayerData data = null;
                Players.TryGetValue(player.userID, out data);
                return data;
            }
            public static PlayerData Get(ItemContainer container)
            {
                if (container == null)
                    return null;
                foreach (var playerData in Players)
                {
                    if (playerData.Value.IsLootingContainer(container))
                        return playerData.Value;
                }
                return null;
            }

            public static void Remove(BasePlayer player)
            {
                Players.Remove(player.userID);
            }
        }


        static partial class Patches
        {
            internal static bool MoveToContainer(Item __instance, ItemContainer __0, ref int __1)
            {
                PlayersDatabase.PlayerData playerData = PlayersDatabase.Get(__0);
                if (playerData != null && playerData.category != ALL_CATEGORIES)
                {
                    if (__instance.parent == __0)
                        return false;
                    __1 = -1;
                }
                return true;
            }

            static void SomeThingsWithRPCs(ref NetWrite __0, SendInfo __1)
            {
                BasePlayer player = __1.connection?.player as BasePlayer;
                if (player == null) return;
                if (!Instance.permission.UserHasPermission(player.UserIDString, PERM_TO_USE)) return;

                uint? RPCID = __0?.Data?.ReadUnsafe<uint>(9);
                if (RPCID == 3394540410)
                {
                    uint num = __0.Data.ReadUnsafe<uint>(21);
                    if (num != 17) return;
                    if (player == null) return;
                    PlayerLoot playerLoot = player.inventory.loot;
                    if (playerLoot.containers.Count == 0) return;
                    PlayersDatabase.PlayerData playerData = PlayersDatabase.GetOrCreate(player, playerLoot.containers[0]);
                    playerData.RenderUI();
                    foreach (var container in playerLoot.containers)
                        container.MarkDirty();

                }
                else if (RPCID == 1748134015)
                {
                    PlayersDatabase.PlayerData playerData = PlayersDatabase.Get(player);
                    if (playerData == null) return;

                    NetWrite netWrite = Network.Net.sv.StartWrite();
                    netWrite.Write(__0.Data, 0, 21);
                    __0.Position = 21;
                    PlayerUpdateLoot updateLoot = PlayerUpdateLoot.Deserialize(__0);
                    __0.Dispose();
                    foreach (var protoContainer in updateLoot.containers)
                    {
                        bool hasPluginFlag = ((ItemContainer.Flag)protoContainer.flags & NoFiltering) == NoFiltering;
                        if (!hasPluginFlag)
                        {
                            if (playerData.category != ALL_CATEGORIES)
                            {
                                if ((int)playerData.category >= CUSTOM_CATEGORIES_START)
                                {
                                    Configuration.Category customCategory = config.Categories.ElementAt((int)playerData.category - CUSTOM_CATEGORIES_START);
                                    protoContainer.contents = protoContainer.contents.Where(item =>
                                    {
                                        ItemDefinition def = ItemManager.FindItemDefinition(item.itemid);
                                        foreach (var ccitem in customCategory.Items)
                                        {
                                            if (def.shortname == ccitem.Shortname)
                                            {
                                                if (ccitem.SkinId < 0)
                                                    return true;
                                                else if (item.skinid == (ulong)ccitem.SkinId)
                                                    return true;
                                            }
                                        }
                                        return false;
                                    }).ToList();
                                }
                                else if (playerData.category == SKINNED_CATEGORY)
                                    protoContainer.contents = protoContainer.contents.Where(item => item.skinid > 0).ToList();
                                else
                                    protoContainer.contents = protoContainer.contents.Where(item => ItemManager.FindItemDefinition(item.itemid).category == playerData.category).ToList();
                                for (int i = 0; i < protoContainer.contents.Count; i++)
                                    protoContainer.contents[i].slot = i;
                            }
                        }
                    }
                    netWrite.WriteObject(updateLoot);
                    __0 = netWrite;
                }
            }

            internal static bool ClientRPCSend(ref NetWrite __0, SendInfo __1)
            {
                SomeThingsWithRPCs(ref __0, __1);
                return true;
            }

        }
        #endregion


        #region Config
        static Configuration config;
        public class Configuration
        {

            public class Category
            {
                public class Item
                {
                    [JsonProperty(PropertyName = "Shortname")]
                    public string Shortname { get; set; } = string.Empty;
                    [JsonProperty(PropertyName = "SkinId (negative number - any)")]
                    public long SkinId { get; set; } = -1;

                }
                [JsonProperty(PropertyName = "Icon URL")]
                public string IconUrl { get; set; } = string.Empty;
                [JsonProperty(PropertyName = "Items")]
                public List<Item> Items { get; set; } = new List<Item>();
            }

            [JsonProperty(PropertyName = "Custom categories")]
            public IList<Category> Categories { get; set; } = new ReadOnlyCollection<Category>(new List<Category>
                    {
                        new Category
                        {
                            IconUrl = "https://i.imgur.com/VM0Qtvo.png",
                            Items = new List<Category.Item>
                            {
                                new Category.Item
                                {
                                    Shortname = "Here you specify the shortname of the item, you can also add multiple items to your category. To remove this category example leave the 'Custom categories' field at []",
                                    SkinId = -1
                                }
                            }
                        }
                    });

            public static Configuration DefaultConfig()
            {
                return new Configuration();
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
    }

    #region 0xF UI Library
    partial class Sortify
    {
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
                PermanentMarker
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
               float fadeIn = 0f,
               float fadeOut = 0f,
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
                             Align = align,
                             FadeIn = fadeIn
                         },
                         new  CuiRectTransformComponent()
                         {
                              AnchorMin = anchorMin,
                              AnchorMax = anchorMax,
                              OffsetMin = offsetMin,
                              OffsetMax =  offsetMax
                         },
                    },
                    FadeOut = fadeOut,
                    Parent = parent,
                    Name = name,

                };
                Add(element);
                return name;
            }
            public string AddOutlinedText(
               string text = "Text",
               string color = "1 1 1 1",
               Font font = Font.RobotoCondensedRegular,
               int fontSize = 14,
               TextAnchor align = TextAnchor.UpperLeft,
               VerticalWrapMode overflow = VerticalWrapMode.Overflow,
               string outlineColor = "0 0 0 1",
               float outlineWidth = 1,
               string anchorMin = "0 0",
               string anchorMax = "1 1",
               string offsetMin = "0 0",
               string offsetMax = "0 0",
               float fadeIn = 0f,
               float fadeOut = 0f,
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
                             Align = align,
                             FadeIn = fadeIn
                         },
                         new CuiOutlineComponent
                         {
                             Color = outlineColor,
                             Distance = $"{outlineWidth} {-outlineWidth}"
                         },
                         new  CuiRectTransformComponent()
                         {
                              AnchorMin = anchorMin,
                              AnchorMax = anchorMax,
                              OffsetMin = offsetMin,
                             OffsetMax =  offsetMax
                         },
                    },
                    FadeOut = fadeOut,
                    Parent = parent,
                    Name = name
                };
                Add(element);
                return name;
            }

            public string AddInputfield(
              string command,
              string text = "Enter text here...",
              string color = "1 1 1 1",
              Font font = Font.RobotoCondensedRegular,
              int fontSize = 14,
              TextAnchor align = TextAnchor.UpperLeft,
              string anchorMin = "0 0",
              string anchorMax = "1 1",
              string offsetMin = "0 0",
              string offsetMax = "0 0",
              bool needsKeyboard = true,
              bool autoFocus = false,
              bool isPassword = false,
              int charsLimit = 0,
              string parent = "Hud",
              string name = null)
            {
                if (name == null)
                    name = CuiHelper.GetGuid();
                CuiElement element = new CuiElement
                {
                    Components =
                    {
                         new CuiInputFieldComponent
                         {
                             Text = text,
                             Color = color,
                             Font = FontNames[(int)font],
                             FontSize = fontSize,
                             Align = align,
                             Autofocus = autoFocus,
                             Command = command,
                             IsPassword = isPassword,
                             CharsLimit = charsLimit,
                             NeedsKeyboard = needsKeyboard,
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
                Add(element);
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
               float fadeIn = 0f,
               float fadeOut = 0f,
               bool cursorEnabled = false,
               bool keyboardEnabled = false,
               string parent = "Hud",
               string name = null)
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
                    ImageType = imageType,
                    FadeIn = fadeIn,
                },
                    RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax,
                },
                    CursorEnabled = cursorEnabled,
                    KeyboardEnabled = keyboardEnabled,
                    FadeOut = fadeOut
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
                float fadeIn = 0f,
                float fadeOut = 0f,
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
                    ImageType = imageType,
                    FadeIn = fadeIn,
                },
                    RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax,
                },
                    FadeOut = fadeOut,
                };
                ElementContainer.Add(button, parent, name);
                return name;
            }


            public string AddImage(
                string content,
                string color = "1 1 1 1",
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0",
                float fadeIn = 0f,
                float fadeOut = 0f,
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
                             Sprite = "assets/content/textures/generic/fulltransparent.tga",
                             FadeIn = fadeIn
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
                    Name = name,
                    FadeOut = fadeOut
                };
                Add(element);
                return name;
            }
            public string AddHImage(string content,
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
                             Sprite = "assets/content/textures/generic/fulltransparent.tga",
                             Material = "assets/icons/iconmaterial.mat"
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
                Add(element);
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
                float fadeIn = 0f,
                float fadeOut = 0f,
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
                             FadeIn = fadeIn
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
                    Name = name,
                    FadeOut = fadeOut
                };
                Add(element);
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
                float fadeIn = 0f,
                float fadeOut = 0f,
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
                             FadeIn = fadeIn
                         },
                         new CuiOutlineComponent
                         {
                             Color = outlineColor,
                             Distance = $"{outlineWidth} {-outlineWidth}"
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
                    Name = name,
                    FadeOut = fadeOut
                };
                Add(element);
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
                    Name = name,
                };
                Add(element);
                return name;
            }

            public void Add(CuiElement element)
            {
                // element.Name = $"{element.Parent}/{element.Name}";
                ElementContainer.Add(element);
            }

            public void Render(BasePlayer player) => CuiHelper.AddUi(player, ElementContainer);
            public void RenderWithDestroy(BasePlayer player, int countElements = 1)
            {

                if (countElements == 0) return;
                if (ElementContainer.Count > 0)
                {
                    for (int i = 0; i < (countElements == -1 ? ElementContainer.Count : countElements); i++)
                    {
                        var element = ElementContainer.ElementAt(i);
                        if (element != null && element.Name != null && element.Name != string.Empty)
                            CuiHelper.DestroyUi(player, element.Name);
                    }

                }
                Render(player);
            }
            public void Update(BasePlayer player)
            {
                if (ElementContainer.Count > 0)
                {
                    for (int i = 0; i < ElementContainer.Count; i++)
                    {
                        var element = ElementContainer.ElementAt(i);
                        element.Update = true;
                    }
                }
                Render(player);
            }
            public void Destroy(BasePlayer player)
            {
                if (ElementContainer.Count > 0)
                {
                    for (int i = 0; i < ElementContainer.Count; i++)
                    {
                        var element = ElementContainer.ElementAt(i);
                        if (element != null && element.Name != null && element.Name != string.Empty)
                            CuiHelper.DestroyUi(player, element.Name);
                    }

                }
            }

        }
    }
    #endregion
}