using System.Collections.Generic;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ItemsPerJump", "Bizlich", "1.0.0")]
    [Description("Выдаёт случайные предметы игроку при прыжке.")]
    public class ItemsPerJump : RustPlugin
    {
        private PlayerData playerData;
        private class PlayerData
        {
            public Dictionary<ulong, bool> JumpEnabled = new Dictionary<ulong, bool>();
        }
        private Dictionary<ulong, bool> playerJumped = new Dictionary<ulong, bool>();

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("SteamID для аватарки в чате")]
            public string ChatAvatar = "";
            [JsonProperty("Полный рандом")]
            public bool useGlobalRandom = true;
            [JsonProperty("Режим (1-Предметы; 2-Категории; 3-Предметы+Категории; 4-Рандом+Предметы; 5-Рандом+Категории; 6-Рандом+Предметы+Категории)")]
            public int mode = 1;
            [JsonProperty("Категории")]
            public Dictionary<string, CategoryConfig> categories = new Dictionary<string, CategoryConfig>();
            [JsonProperty("Предметы")]
            public Dictionary<string, ItemConfig> items = new Dictionary<string, ItemConfig>();
            internal class CategoryConfig
            {
                [JsonProperty("Минимум")]
                public int min = 1;

                [JsonProperty("Максимум")]
                public int max = 1;
            }
            internal class ItemConfig
            {
                [JsonProperty("Минимум")]
                public int min = 1;

                [JsonProperty("Максимум")]
                public int max = 1;
            }
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    ChatAvatar = "",
                    useGlobalRandom = true,
                    mode = 1,
                    categories = new Dictionary<string, CategoryConfig>
                    {
                        { "Weapon", new CategoryConfig { min = 1, max = 1 } },
                        { "Construction", new CategoryConfig { min = 1, max = 1 } },
                        { "Items", new CategoryConfig { min = 1, max = 1 } },
                        { "Resources", new CategoryConfig { min = 1, max = 1 } },
                        { "Attire", new CategoryConfig { min = 1, max = 1 } },
                        { "Tool", new CategoryConfig { min = 1, max = 1 } },
                        { "Medical", new CategoryConfig { min = 1, max = 1 } },
                        { "Food", new CategoryConfig { min = 1, max = 1 } },
                        { "Ammunition", new CategoryConfig { min = 1, max = 1 } },
                        { "Traps", new CategoryConfig { min = 1, max = 1 } },
                        { "Misc", new CategoryConfig { min = 1, max = 1 } },
                        { "Component", new CategoryConfig { min = 1, max = 1 } },
                        { "Electrical", new CategoryConfig { min = 1, max = 1 } },
                        { "Fun", new CategoryConfig { min = 1, max = 1 } },
                    },
                    items = new Dictionary<string, ItemConfig>
                    {
                        { "ammo.rifle", new ItemConfig { min = 10, max =  50} },
                        { "ammo.pistol", new ItemConfig { min = 100, max = 500 } }
                    }
                };
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка чтения конфигурации, создаём новую!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        private void LoadData()
        {
            playerData = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>("ItemsPerJump/Players") ?? new PlayerData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ItemsPerJump/Players", playerData);
        }

        private void OnServerInitialized()
        {   
            LoadData();
            SaveData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!playerData.JumpEnabled.ContainsKey(player.userID))
                playerData.JumpEnabled[player.userID] = true;

            SaveData();
        }

        [ChatCommand("jump")]
        private void ToggleJump(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 1)
            {
                string option = args[0].ToLower();
                switch (option)
                {
                    case "on":
                        if(!playerData.JumpEnabled[player.userID])
                        {
                            playerData.JumpEnabled[player.userID] = true;
                            SaveData();
                            SendChat(player, "Выдача предметов при прыжке <color=green>включена</color>.", config.ChatAvatar);
                            //SendReply(player, "Выдача предметов при прыжке <color=green>включена</color>.");
                        }
                        else
                            SendChat(player, "Выдача уже включена.", config.ChatAvatar);
                            //SendReply(player, "Выдача уже включена.");
                        break;
                    case "off":
                        if(playerData.JumpEnabled[player.userID])
                        {
                            playerData.JumpEnabled[player.userID] = false;
                            SaveData();
                            SendChat(player, "Выдача предметов при прыжке <color=red>выключена</color>.", config.ChatAvatar);
                            //SendReply(player, "Выдача предметов при прыжке <color=red>выключена</color>.");
                        }
                        else
                            SendChat(player, "Выдача уже выключена.", config.ChatAvatar);
                            //SendReply(player, "Выдача уже выключена.");
                        break;
                    default:
                        SendChat(player, "Использование: /jump on | off", config.ChatAvatar);
                        //SendReply(player, "Использование: /jump on | off");
                    break;
                }
            }
            else
                SendChat(player, "Использование: /jump on | off", config.ChatAvatar);
                //SendReply(player, "Использование: /jump on | off");
        }

        [ConsoleCommand("jumpon")]
        private void cmdJumpOn(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player == null)
            {
                arg.ReplyWith("Эту команду могут использовать только игроки.");
                return;
            }

            if (!playerData.JumpEnabled[player.userID])
            {
                playerData.JumpEnabled[player.userID] = true;
                SaveData();
                SendChat(player, "Выдача предметов при прыжке <color=yellow>включена</color>.", config.ChatAvatar);
                //SendReply(player, "Выдача предметов при прыжке <color=yellow>включена</color>.");
            }
            else
                SendChat(player, "Выдача уже включена.", config.ChatAvatar);
                //SendReply(player, "Выдача уже включена.");
        }

        [ConsoleCommand("jumpoff")]
        private void cmdJumpOff(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if (player == null)
            {
                arg.ReplyWith("Эту команду могут использовать только игроки.");
                return;
            }

            if(playerData.JumpEnabled[player.userID])
            {
                playerData.JumpEnabled[player.userID] = false;
                SaveData();
                SendChat(player, "Выдача предметов при прыжке <color=red>выключена</color>.", config.ChatAvatar);
                //SendReply(player, "Выдача предметов при прыжке <color=red>выключена</color>.");
            }
            else
                SendChat(player, "Выдача уже выключена.", config.ChatAvatar);
                //SendReply(player, "Выдача уже выключена.");
        }

        private void OnPlayerTick(BasePlayer player)
        {
            if (player == null || !player.IsConnected || player.IsDead())
                return;

            if (!playerData.JumpEnabled.ContainsKey(player.userID) || !playerData.JumpEnabled[player.userID])
                return;

            bool isGrounded = player.IsOnGround();
            bool wasJumping;

            if (!playerJumped.TryGetValue(player.userID, out wasJumping))
            {
                playerJumped[player.userID] = false;
                wasJumping = false;
            }

            if (!isGrounded && !wasJumping)
            {
                playerJumped[player.userID] = true;
                GiveItem(player);
            }
            else if (isGrounded && wasJumping)
            {       
                playerJumped[player.userID] = false;
            }
        }

        private void GiveItem(BasePlayer player)
        {
            if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
            {
                player.ChatMessage("<color=red>У вас полный инвентарь!</color>");
                return;
            }

            Item newItem = null;

            if (config.useGlobalRandom)
            {
                newItem = GetRandomItemFromAll();
            }
            else
            {
                switch (config.mode)
                {
                    case 1:
                        newItem = GetItemFromConfig();
                        break;
                    case 2:
                        newItem = GetItemFromCategory();
                        break;
                    case 3:
                        newItem = GetItemFromConfigAndCategory();
                        break;
                    case 4:
                        newItem = GetRandomItemFromAllWithOverrideByConfigItems();
                        break;
                    case 5:
                        newItem = GetRandomItemFromAllWithOverrideByConfigCategories();
                        break;
                    case 6:
                        newItem = GetRandomItemFromAllWithOverrideByConfigItemsAndCategories();
                        break;
                }
            }

            if (newItem != null && newItem.amount != 0)
                player.GiveItem(newItem);
        }

        // Полный рандом из всех предметов
        private Item GetRandomItemFromAll()
        {
            List<int> itemIDs = ItemManager.itemList.ConvertAll(i => i.itemid);
            int randomItemID = itemIDs[UnityEngine.Random.Range(0, itemIDs.Count)];
            return ItemManager.CreateByItemID(randomItemID, 1);
        }

        // 1. Рандом из того, что в предметах
        private Item GetItemFromConfig()
        {
            List<string> itemNames = new List<string>(config.items.Keys);
            string randomItemName = itemNames[UnityEngine.Random.Range(0, itemNames.Count)];
            var itemConfig = config.items[randomItemName];
            int amount = UnityEngine.Random.Range(itemConfig.min, itemConfig.max + 1);
            return ItemManager.CreateByName(randomItemName, amount);
        }

        // 2. Рандом из того, что в категориях
        private Item GetItemFromCategory()
        {
            List<ItemDefinition> possibleItems = new List<ItemDefinition>();

            foreach (var category in config.categories)
            {
                ItemCategory itemCategory = (ItemCategory)System.Enum.Parse(typeof(ItemCategory), category.Key);
                possibleItems.AddRange(ItemManager.itemList.FindAll(i => i.category == itemCategory));
            }

            if (possibleItems.Count > 0)
            {
                ItemDefinition randomItemDef = possibleItems[UnityEngine.Random.Range(0, possibleItems.Count)];
                var categoryConfig = config.categories[randomItemDef.category.ToString()];
                int amount = UnityEngine.Random.Range(categoryConfig.min, categoryConfig.max + 1);
                return ItemManager.Create(randomItemDef, amount);
            }

            return null;
        }

        // 3. Рандом из того, что в предметах и категориях
        private Item GetItemFromConfigAndCategory()
        {
            if (UnityEngine.Random.Range(0, 2) == 0)
                return GetItemFromConfig();
            else
                return GetItemFromCategory();
        }

        // 4. Рандом из всех предметов, но количество определяется из конфига предметов
        private Item GetRandomItemFromAllWithOverrideByConfigItems()
        {
            List<int> itemIDs = ItemManager.itemList.ConvertAll(i => i.itemid);
            int randomItemID = itemIDs[UnityEngine.Random.Range(0, itemIDs.Count)];
            Item item = ItemManager.CreateByItemID(randomItemID, 1);
            Configuration.ItemConfig itemConfig;

            if (config.items.TryGetValue(item.info.shortname, out itemConfig))
            {
                int amount = UnityEngine.Random.Range(itemConfig.min, itemConfig.max + 1);
                item.amount = amount;
            }

            return item;
        }

        // 5. Рандом из всех предметов, но количество определяется из конфига категорий
        private Item GetRandomItemFromAllWithOverrideByConfigCategories()
        {
            List<int> itemIDs = ItemManager.itemList.ConvertAll(i => i.itemid);
            int randomItemID = itemIDs[UnityEngine.Random.Range(0, itemIDs.Count)];
            Item item = ItemManager.CreateByItemID(randomItemID, 1);
            Configuration.CategoryConfig categoryConfig;

            if (config.categories.TryGetValue(item.info.category.ToString(), out categoryConfig))
            {
                int amount = UnityEngine.Random.Range(categoryConfig.min, categoryConfig.max + 1);
                item.amount = amount;
            }

            return item;
        }

        // 6. Рандом из всех предметов, но количество определяется из конфига предметов и категорий
        private Item GetRandomItemFromAllWithOverrideByConfigItemsAndCategories()
        {
            List<int> itemIDs = ItemManager.itemList.ConvertAll(i => i.itemid);
            int randomItemID = itemIDs[UnityEngine.Random.Range(0, itemIDs.Count)];
            Item item = ItemManager.CreateByItemID(randomItemID, 1);
            Configuration.ItemConfig itemConfig;
            Configuration.CategoryConfig categoryConfig;

            if (config.items.TryGetValue(item.info.shortname, out itemConfig))
            {
                int amount = UnityEngine.Random.Range(itemConfig.min, itemConfig.max + 1);
                item.amount = amount;
            }
            else if (config.categories.TryGetValue(item.info.category.ToString(), out categoryConfig))
            {
                int amount = UnityEngine.Random.Range(categoryConfig.min, categoryConfig.max + 1);
                item.amount = amount;
            }

            return item;
        }

        private void SendChat(BasePlayer player, string Message, string ChatAvatar = "")
        {
            player.SendConsoleCommand("chat.add", Chat.ChatChannel.Global, ChatAvatar, Message);
        }
    }
}
