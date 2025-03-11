using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Oxide.Core.Libraries.Covalence;
using System.Text;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("FCleaner", "BANTIK CHATGPTPASTER", "1.0.0")]
    [Description("Комплексное решение для очистки сервера от нежелательных объектов")]
    public class FCleaner : RustPlugin
    {
        private Dictionary<ulong, PlayerDropData> playerDrops = new Dictionary<ulong, PlayerDropData>();
        private Dictionary<string, int> itemTypeCount = new Dictionary<string, int>();
        private Queue<BaseEntity> cleanupQueue = new Queue<BaseEntity>();
        private Timer cleanupTimer;
        private Dictionary<ulong, int> playerDropCounts = new Dictionary<ulong, int>();
        private Dictionary<ulong, Timer> playerResetTimers = new Dictionary<ulong, Timer>();

        private class PlayerDropData
        {
            public int DropCount { get; set; }
            public Timer ResetTimer { get; set; }
        }

        #region Конфигурация
        private Configuration config;
        
        public class Configuration
        {
            [JsonProperty("Префикс")]
            public string Prefix = "[FCleaner]";
            
            [JsonProperty("Цвет префикса")]
            public string PrefixColor = "#34eb4f";
            
            [JsonProperty("URL аватарки для чата")]
            public string ChatAvatarUrl = "https://i.imgur.com/example.png";
            
            [JsonProperty("URL аватарки для Discord")]
            public string DiscordAvatarUrl = "https://i.imgur.com/example.png";
            
            [JsonProperty("Интервал очистки (секунды)")]
            public float CleanInterval = 200f;
            
            [JsonProperty("Включить пошаговую очистку")]
            public bool EnablePartialCleanup = true;
            
            [JsonProperty("Размер пакета очистки")]
            public int CleanupChunkSize = 50;
            
            [JsonProperty("Задержка между пакетами (секунды)")]
            public float CleanupChunkDelay = 1f;
            
            [JsonProperty("Включить автоочистку по типам")]
            public bool AutoCleanByTypeEnabled = true;
            
            [JsonProperty("Порог автоочистки по типам")]
            public int AutoCleanTypeThreshold = 100;
            
            [JsonProperty("Отключить коллизию предметов")]
            public bool DisableItemCollisions = true;
            
            [JsonProperty("Очищать выброшенные предметы")]
            public bool CleanDroppedItems = true;
            
            [JsonProperty("Очищать трупы игроков")]
            public bool CleanPlayerCorpses = true;
            
            [JsonProperty("Очищать рюкзаки NPC")]
            public bool CleanNPCBackpacks = true;
            
            [JsonProperty("Очищать рюкзаки с предметами")]
            public bool CleanDropBackpacks = true;
            
            [JsonProperty("Очищать добычные трупы")]
            public bool CleanLootableCorpses = true;
            
            [JsonProperty("Очищать неактивные сущности")]
            public bool CleanInactiveEntities = true;
            
            [JsonProperty("Время уведомления перед очисткой")]
            public float CleanNotificationTime = 30f;
            
            [JsonProperty("Исключенные предметы")]
            public List<string> ExcludedItems = new List<string>
            {
                "horsedung",
                "keycard_blue",
                "keycard_green",
                "keycard_red"
            };
            
            [JsonProperty("Порог предупреждения о выбросе предметов")]
            public int ItemDropWarningThreshold = 30;
            
            [JsonProperty("Порог бана за выброс предметов")]
            public int ItemDropBanThreshold = 50;
            
            [JsonProperty("Время сброса счетчика выброса (секунды)")]
            public float ItemDropResetTime = 60f;
            
            [JsonProperty("Длительность бана (минуты)")]
            public int BanDuration = 60;
            
            [JsonProperty("Включить бан за спам предметами")]
            public bool BanEnabled = true;
            
            [JsonProperty("URL Discord вебхука")]
            public string DiscordWebhookUrl = "";
            
            [JsonProperty("Включить уведомления в чате")]
            public bool EnableChatNotifications = true;
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
                PrintError("Ошибка загрузки конфигурации! Создаю новую конфигурацию.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Локализация
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CleanupStarting"] = "Очистка сервера начнется через {0} секунд",
                ["CleanupComplete"] = "Очистка завершена: удалено {0} предметов, {1} трупов, {2} рюкзаков",
                ["AutoCleanupByType"] = "Автоматически очищено {0} предметов типа {1}",
                ["DropWarning"] = "Внимание: Вы выбросили {0} предметов. При достижении {1} предметов вы будете заблокированы.",
                ["BanReason"] = "Превышен лимит выброшенных предметов ({0})",
                ["AdminNotification"] = "Игрок {0} выбросил {1} предметов за короткое время",
                ["CommandUsage"] = "Использование:\n/fcleaner force - Принудительная очистка\n/fcleaner status - Статус очистки",
                ["NoPermission"] = "У вас нет прав для использования этой команды",
                ["ForcedCleanup"] = "Запущена принудительная очистка",
                ["StatusInfo"] = "Статус FCleaner:\nВ очереди на очистку: {0}\nПредметов по типам:\n{1}"
            }, this);
        }

        private string GetMessage(string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this), args);
        }
        #endregion

        #region Оксид Хуки
        void Init()
        {
            permission.RegisterPermission("fcleaner.admin", this);
            permission.RegisterPermission("fcleaner.notify", this);
            
            cmd.AddChatCommand("fcleaner", this, "CmdFCleaner");
            
            if (string.IsNullOrEmpty(config.DiscordWebhookUrl))
            {
                PrintError("URL Discord вебхука не указан в конфигурации!");
            }
        }

        void OnServerInitialized()
        {
            if (string.IsNullOrEmpty(config.DiscordWebhookUrl))
            {
                PrintError("URL Discord вебхука не указан в конфигурации!");
            }
            
            timer.Every(config.CleanInterval, () => PerformCleanup());
        }

        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (config.DisableItemCollisions)
            {
                var droppedItem = entity as DroppedItem;
                if (droppedItem != null)
                {
                    var collider = droppedItem.GetComponent<Collider>();
                    if (collider != null)
                        collider.enabled = false;
                }
            }

            UpdateItemTypeCount(item);
            
            if (!config.BanEnabled) return;

            var player = item.GetOwnerPlayer();
            if (player == null) return;

            var steamId = player.userID;
            
            if (!playerDrops.ContainsKey(steamId))
            {
                playerDrops[steamId] = new PlayerDropData();
                playerDrops[steamId].ResetTimer = timer.Once(config.ItemDropResetTime, () => 
                {
                    if (playerDrops.ContainsKey(steamId))
                    {
                        playerDrops.Remove(steamId);
                    }
                });
            }
            else
            {
                playerDrops[steamId].ResetTimer.Destroy();
                playerDrops[steamId].ResetTimer = timer.Once(config.ItemDropResetTime, () => 
                {
                    if (playerDrops.ContainsKey(steamId))
                    {
                        playerDrops.Remove(steamId);
                    }
                });
            }

            playerDrops[steamId].DropCount++;
            
            if (playerDrops[steamId].DropCount >= config.ItemDropWarningThreshold)
            {
                if (playerDrops[steamId].DropCount >= config.ItemDropBanThreshold)
                {
                    if (config.BanEnabled)
                    {
                        BanPlayer(player);
                    }
                }
                else
                {
                    WarnPlayer(player);
                }
            }
        }

        void Unload()
        {
            foreach (var data in playerDrops.Values)
            {
                if (data.ResetTimer != null && !data.ResetTimer.Destroyed)
                {
                    data.ResetTimer.Destroy();
                }
            }
        }
        #endregion

        #region Основные методы
        void PerformCleanup()
        {
            if (config.EnableChatNotifications)
            {
                BroadcastToChat(GetMessage("CleanupStarting", config.CleanNotificationTime));
            }
            
            timer.Once(config.CleanNotificationTime, () =>
            {
                int itemsCount = 0;
                int corpsesCount = 0;
                int backpacksCount = 0;
                
                if (config.CleanDroppedItems)
                {
                    itemsCount = CleanDroppedItems();
                }
                
                if (config.CleanPlayerCorpses)
                {
                    corpsesCount = CleanPlayerCorpses();
                }
                
                if (config.CleanNPCBackpacks)
                {
                    backpacksCount = CleanNPCBackpacks();
                }
                
                if (config.CleanDropBackpacks)
                {
                    backpacksCount += CleanDroppedBackpacks();
                }
                
                if (config.CleanInactiveEntities)
                {
                    CleanInactiveEntities();
                }
                
                if (config.EnableChatNotifications)
                {
                    BroadcastToChat(GetMessage("CleanupComplete", itemsCount, corpsesCount, backpacksCount));
                }
                
                SendDiscordMessage("Очистка сервера завершена", itemsCount, corpsesCount, backpacksCount);
            });
        }

        int CleanDroppedItems()
        {
            int count = 0;
            var items = UnityEngine.Object.FindObjectsOfType<DroppedItem>();
            foreach (var item in items)
            {
                if (!config.ExcludedItems.Contains(item.ShortPrefabName))
                {
                    item.Kill();
                    count++;
                }
            }
            return count;
        }

        int CleanPlayerCorpses()
        {
            int count = 0;
            var corpses = UnityEngine.Object.FindObjectsOfType<BaseCorpse>();
            foreach (var corpse in corpses)
            {
                if (corpse is PlayerCorpse)
                {
                    corpse.Kill();
                    count++;
                }
            }
            return count;
        }

        int CleanNPCBackpacks()
        {
            int count = 0;
            var backpacks = UnityEngine.Object.FindObjectsOfType<DroppedItemContainer>()
                .Where(x => x.playerSteamID == 0);
            
            foreach (var backpack in backpacks)
            {
                backpack.Kill();
                count++;
            }
            return count;
        }

        int CleanDroppedBackpacks()
        {
            int count = 0;
            var backpacks = UnityEngine.Object.FindObjectsOfType<DroppedItemContainer>()
                .Where(x => x.playerSteamID != 0);
            
            foreach (var backpack in backpacks)
            {
                backpack.Kill();
                count++;
            }
            return count;
        }

        void CleanInactiveEntities()
        {
            var entities = BaseNetworkable.serverEntities.Where(x => 
                x is BaseEntity && 
                (x as BaseEntity).IsDestroyed);
            
            foreach (var entity in entities)
            {
                entity.Kill();
            }
        }

        void SendDiscordMessage(string title, int items = 0, int corpses = 0, int backpacks = 0)
        {
            if (string.IsNullOrEmpty(config.DiscordWebhookUrl)) return;
            
            var payload = new Dictionary<string, object>
            {
                ["username"] = "FCleaner",
                ["avatar_url"] = config.DiscordAvatarUrl,
                ["embeds"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["title"] = title,
                        ["color"] = 3586484,
                        ["fields"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["name"] = "Предметы",
                                ["value"] = items.ToString(),
                                ["inline"] = true
                            },
                            new Dictionary<string, object>
                            {
                                ["name"] = "Трупы",
                                ["value"] = corpses.ToString(),
                                ["inline"] = true
                            },
                            new Dictionary<string, object>
                            {
                                ["name"] = "Рюкзаки",
                                ["value"] = backpacks.ToString(),
                                ["inline"] = true
                            }
                        },
                        ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                }
            };
            
            webrequest.Enqueue(config.DiscordWebhookUrl, JsonConvert.SerializeObject(payload), 
                (code, response) => {}, this, RequestMethod.POST, 
                new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }

        void BroadcastToChat(string message, string color = null)
        {
            if (!config.EnableChatNotifications) return;

            color = color ?? config.PrefixColor;
            var formattedMessage = $"<color={color}>{config.Prefix}</color> {message}";

            if (!string.IsNullOrEmpty(config.ChatAvatarUrl))
            {
                Server.Broadcast(formattedMessage, config.ChatAvatarUrl);
            }
            else
            {
                Server.Broadcast(formattedMessage);
            }
        }

        void CmdFCleaner(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "fcleaner.admin"))
            {
                Player.Message(player, GetMessage("NoPermission"));
                return;
            }

            if (args.Length == 0)
            {
                Player.Message(player, GetMessage("CommandUsage"));
                return;
            }

            switch (args[0].ToLower())
            {
                case "force":
                    Player.Message(player, GetMessage("ForcedCleanup"));
                    PerformCleanup();
                    break;

                case "status":
                    var itemTypes = string.Join("\n", itemTypeCount
                        .OrderByDescending(x => x.Value)
                        .Take(5)
                        .Select(x => $"- {x.Key}: {x.Value}"));
                    
                    Player.Message(player, GetMessage("StatusInfo", 
                        cleanupQueue.Count,
                        string.IsNullOrEmpty(itemTypes) ? "Нет" : itemTypes));
                    break;

                default:
                    Player.Message(player, GetMessage("CommandUsage"));
                    break;
            }
        }

        void WarnPlayer(BasePlayer player)
        {
            var dropCount = playerDrops[player.userID].DropCount;
            var message = GetMessage("DropWarning", dropCount, config.ItemDropBanThreshold);
            Player.Message(player, $"<color={config.PrefixColor}>{config.Prefix}</color> {message}");

            var adminMessage = GetMessage("AdminNotification", player.displayName, dropCount);
            NotifyAdmins(adminMessage);
        }

        void BanPlayer(BasePlayer player)
        {
            var reason = GetMessage("BanReason", config.ItemDropBanThreshold);
            Server.Command($"ban {player.UserIDString} {config.BanDuration * 60} {reason}");
            
            SendDiscordMessage("Игрок заблокирован", 0, 0, 0);
        }

        private void ProcessCleanupQueue()
        {
            if (cleanupQueue.Count == 0)
            {
                cleanupTimer?.Destroy();
                cleanupTimer = null;
                return;
            }

            int processed = 0;
            while (cleanupQueue.Count > 0 && processed < config.CleanupChunkSize)
            {
                var entity = cleanupQueue.Dequeue();
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                    processed++;
                }
            }

            if (cleanupQueue.Count > 0)
            {
                cleanupTimer = timer.Once(config.CleanupChunkDelay, ProcessCleanupQueue);
            }
        }

        private void UpdateItemTypeCount(Item item)
        {
            if (!config.AutoCleanByTypeEnabled) return;

            string itemType = item.info.shortname;
            if (!itemTypeCount.ContainsKey(itemType))
                itemTypeCount[itemType] = 0;
            
            itemTypeCount[itemType]++;

            if (itemTypeCount[itemType] >= config.AutoCleanTypeThreshold)
            {
                CleanItemsByType(itemType);
                itemTypeCount[itemType] = 0;
            }
        }

        private void CleanItemsByType(string itemType)
        {
            var items = BaseNetworkable.serverEntities.OfType<DroppedItem>()
                .Where(item => item.item.info.shortname == itemType)
                .ToList();

            foreach (var item in items)
            {
                if (!item.IsDestroyed)
                    item.Kill();
            }

            if (config.EnableChatNotifications)
            {
                BroadcastToChat(GetMessage("AutoCleanupByType", items.Count, itemType));
            }

            SendDiscordMessage($"Автоочистка по типу: {itemType}", items.Count, 0, 0);
        }

        private void NotifyAdmins(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "fcleaner.notify"))
                {
                    Player.Message(player, $"<color={config.PrefixColor}>{config.Prefix}</color> {message}");
                }
            }
        }
        #endregion
    }
} 