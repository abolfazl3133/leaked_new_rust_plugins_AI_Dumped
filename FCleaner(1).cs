using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Oxide.Core.Libraries.Covalence;
using System.Text;
using Oxide.Core.Libraries;
using System.Diagnostics;

namespace Oxide.Plugins
{
    [Info("FCleaner", "BY BANTIK", "1.0.0")]
    [Description("Комплексное решение для очистки сервера от нежелательных объектов")]
    public class FCleaner : RustPlugin
    {
        private Dictionary<ulong, PlayerDropData> playerDrops = new Dictionary<ulong, PlayerDropData>();
        private Dictionary<string, int> itemTypeCount = new Dictionary<string, int>();
        private Queue<BaseEntity> cleanupQueue = new Queue<BaseEntity>();
        private Timer cleanupTimer;
        private Dictionary<ulong, int> playerDropCounts = new Dictionary<ulong, int>();
        private Dictionary<ulong, Timer> playerResetTimers = new Dictionary<ulong, Timer>();
        private Dictionary<string, Timer> resetTimers = new Dictionary<string, Timer>();
        private Dictionary<string, int> droppedItemCountByType = new Dictionary<string, int>();

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
            
            [JsonProperty("Очищать внешние стены")]
            public bool CleanExternalWalls = true;
            
            [JsonProperty("Очищать миникоптеры")]
            public bool CleanMinicopters = true;
            
            [JsonProperty("Очищать спальные мешки")]
            public bool CleanSleepingBags = true;
            
            [JsonProperty("Процент урона при очистке стен")]
            public float WallDecayPercentage = 10.0f;
            
            [JsonProperty("Список префабов стен для очистки")]
            public List<string> WallPrefabs = new List<string>
            {
                "assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab",
                "assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab",
                "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab",
                "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab",
                "assets/prefabs/deployable/barricades/barricade.concrete.prefab",
                "assets/prefabs/deployable/barricades/barricade.metal.prefab",
                "assets/prefabs/deployable/barricades/barricade.sandbags.prefab",
                "assets/prefabs/deployable/barricades/barricade.stone.prefab",
                "assets/prefabs/deployable/barricades/barricade.wood.prefab",
                "assets/prefabs/deployable/barricades/barricade.woodwire.prefab"
            };
            
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
                ["CleanupComplete"] = "Очистка завершена: удалено {0} предметов, {1} трупов, {2} рюкзаков, {3} стен, {4} миникоптеров, {5} спальных мешков",
                ["AutoCleanupByType"] = "Автоматически очищено {0} предметов типа {1}",
                ["DropWarning"] = "Внимание: Вы выбросили {0} предметов. При достижении {1} предметов вы будете заблокированы.",
                ["BanReason"] = "Превышен лимит выброшенных предметов ({0})",
                ["AdminNotification"] = "Игрок {0} выбросил {1} предметов за короткое время",
                ["CommandUsage"] = "Использование:\n/fcleaner force - Принудительная очистка\n/fcleaner status - Статус очистки",
                ["NoPermission"] = "У вас нет прав для использования этой команды",
                ["ForcedCleanup"] = "Запущена принудительная очистка",
                ["StatusInfo"] = "Статус FCleaner:\nВ очереди на очистку: {0}\nПредметов по типам:\n{1}",
                ["WallsCleanupInfo"] = "Очищено стен: {0} (повреждено: {1}, уничтожено: {2})",
                ["WallsCleanupDiscord"] = "Очистка стен завершена",
                ["MinicopterCleanupInfo"] = "Очищено миникоптеров: {0}",
                ["SleepingBagCleanupInfo"] = "Очищено спальных мешков: {0}"
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
            
            // Периодическая очистка словарей
            timer.Every(300f, () =>
            {
                droppedItemCountByType.Clear();
                
                // Очистка устаревших таймеров
                var expiredTimers = resetTimers
                    .Where(x => x.Value == null || x.Value.Destroyed)
                    .Select(x => x.Key)
                    .ToList();
                    
                foreach (var key in expiredTimers)
                {
                    resetTimers.Remove(key);
                }
            });
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
            
            foreach (var timer in resetTimers.Values)
            {
                if (timer != null && !timer.Destroyed)
                {
                    timer.Destroy();
                }
            }
            
            resetTimers.Clear();
            droppedItemCountByType.Clear();
        }
        #endregion

        #region Основные методы
        private void PerformCleanup()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            if (config.EnableChatNotifications)
            {
                BroadcastToChat(GetMessage("CleanupStarting", config.CleanNotificationTime));
            }
            
            timer.Once(config.CleanNotificationTime, () =>
            {
                int itemsCount = 0;
                int corpsesCount = 0;
                int backpacksCount = 0;
                int wallsCount = 0;
                int minicopterCount = 0;
                int sleepingBagsCount = 0;
                
                if (config.CleanDroppedItems)
                {
                    itemsCount = CleanDroppedItems();
                    LogCleanup("DroppedItems", itemsCount);
                }
                
                if (config.CleanPlayerCorpses)
                {
                    corpsesCount = CleanPlayerCorpses();
                    LogCleanup("PlayerCorpses", corpsesCount);
                }
                
                if (config.CleanNPCBackpacks)
                {
                    backpacksCount = CleanNPCBackpacks();
                    LogCleanup("NPCBackpacks", backpacksCount);
                }
                
                if (config.CleanDropBackpacks)
                {
                    var dropBackpackCount = CleanDroppedBackpacks();
                    backpacksCount += dropBackpackCount;
                    LogCleanup("DropBackpacks", dropBackpackCount);
                }
                
                if (config.CleanExternalWalls)
                {
                    wallsCount = CleanExternalWalls();
                    LogCleanup("ExternalWalls", wallsCount);
                }
                
                if (config.CleanMinicopters)
                {
                    minicopterCount = CleanMinicopters();
                    LogCleanup("Minicopters", minicopterCount);
                }
                
                if (config.CleanSleepingBags)
                {
                    sleepingBagsCount = CleanSleepingBags();
                    LogCleanup("SleepingBags", sleepingBagsCount);
                }
                
                if (config.CleanInactiveEntities)
                {
                    CleanInactiveEntities();
                }
                
                sw.Stop();
                Puts($"Cleanup completed in {sw.ElapsedMilliseconds}ms");
                
                if (config.EnableChatNotifications)
                {
                    BroadcastToChat(GetMessage("CleanupComplete", itemsCount, corpsesCount, backpacksCount, wallsCount, minicopterCount, sleepingBagsCount));
                }
                
                SendDiscordMessage("Очистка сервера завершена", itemsCount, corpsesCount, backpacksCount, wallsCount, minicopterCount, sleepingBagsCount);
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

        void SendDiscordMessage(string title, int items = 0, int corpses = 0, int backpacks = 0, int walls = 0, int minicopters = 0, int sleepingBags = 0)
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
                            },
                            new Dictionary<string, object>
                            {
                                ["name"] = "Стены",
                                ["value"] = walls.ToString(),
                                ["inline"] = true
                            },
                            new Dictionary<string, object>
                            {
                                ["name"] = "Миникоптеры",
                                ["value"] = minicopters.ToString(),
                                ["inline"] = true
                            },
                            new Dictionary<string, object>
                            {
                                ["name"] = "Спальные мешки",
                                ["value"] = sleepingBags.ToString(),
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
            
            SendDiscordMessage("Игрок заблокирован", 0, 0, 0, 0, 0, 0);
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

            SendDiscordMessage($"Автоочистка по типу: {itemType}", items.Count, 0, 0, 0, 0, 0);
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

        private void LogCleanup(string type, int count)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Cleaned {count} {type}";
            LogToFile("cleanup_log", logEntry, this);
        }

        [ChatCommand("fcleanstats")]
        private void CmdCleanStats(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "fcleaner.admin"))
                return;

            var stats = new Dictionary<string, int>
            {
                ["DroppedItems"] = itemTypeCount.Sum(x => x.Value),
                ["QueuedItems"] = cleanupQueue.Count,
                ["WarnedPlayers"] = playerDrops.Count
            };

            Player.Message(player, $"FCleaner Statistics:\n" + 
                          string.Join("\n", stats.Select(x => $"{x.Key}: {x.Value}")));
        }

        private void SafeKillEntity(BaseNetworkable entity)
        {
            try
            {
                if (entity != null && !entity.IsDestroyed)
                    entity.Kill();
            }
            catch (Exception ex)
            {
                Puts($"Error killing entity: {ex.Message}");
            }
        }

        private int CleanExternalWalls()
        {
            int reduced = 0;
            int destroyed = 0;

            var walls = BaseNetworkable.serverEntities.Where(e => 
            {
                var entity = e as BaseEntity;
                if (entity == null || entity.IsDestroyed) return false;

                string prefabName = entity.PrefabName?.ToLower() ?? "";
                return prefabName.Contains("wall.external") || 
                       prefabName.Contains("gates.external") ||
                       prefabName.Contains("barricade");
            }).ToList();

            foreach (var entity in walls)
            {
                var wall = entity as BaseEntity;
                if (wall == null) continue;

                var entityRadius = Physics.OverlapSphere(wall.transform.position, 5f, LayerMask.GetMask("Trigger", "Construction", "Deployed"));
                bool hasCupboard = false;
                
                foreach (var collider in entityRadius)
                {
                    var priv = collider.GetComponentInParent<BuildingPrivlidge>();
                    if (priv != null && priv.IsAuthed(wall.OwnerID))
                    {
                        hasCupboard = true;
                        break;
                    }
                }

                if (!hasCupboard)
                {
                    var combatEntity = wall as BaseCombatEntity;
                    if (combatEntity != null)
                    {
                        // Увеличим урон для гарантированного уничтожения
                        float damageAmount = combatEntity.MaxHealth() * 2f;
                        combatEntity.Hurt(damageAmount);
                        destroyed++;
                        
                        // Принудительно уничтожаем
                        if (!wall.IsDestroyed)
                        {
                            wall.Kill();
                        }
                    }
                    else
                    {
                        // Если это не BaseCombatEntity, просто уничтожаем
                        wall.Kill();
                        destroyed++;
                    }
                }
            }

            if (config.EnableChatNotifications)
            {
                BroadcastToChat(GetMessage("WallsCleanupInfo", destroyed, reduced, destroyed));
            }

            return destroyed;
        }

        private int CleanMinicopters()
        {
            int count = 0;
            var vehicles = BaseNetworkable.serverEntities.Where(e => 
            {
                var entity = e as BaseEntity;
                if (entity == null || entity.IsDestroyed) return false;
                
                return entity.ShortPrefabName.Contains("minicopter") || 
                       entity.ShortPrefabName.Contains("mini") ||
                       entity.prefabID == 3459945130 || // MiniCopter.Entity
                       entity.prefabID == 1696706575;   // MINI Copter
            });

            foreach (var entity in vehicles)
            {
                try
                {
                    if (!entity.IsDestroyed)
                    {
                        entity.Kill();
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    Puts($"Error killing minicopter: {ex.Message}");
                }
            }

            if (config.EnableChatNotifications && count > 0)
            {
                BroadcastToChat(GetMessage("MinicopterCleanupInfo", count));
            }
            
            return count;
        }

        private int CleanSleepingBags()
        {
            int count = 0;
            var sleepingBags = UnityEngine.Object.FindObjectsOfType<BaseEntity>()
                .Where(e => e != null && !e.IsDestroyed && 
                    (e.prefabID == 1588298435 || // Sleeping Bag
                     e.prefabID == 2662124780 || // Bed
                     e.prefabID == 3872955268 || // Beach Towel
                     e.ShortPrefabName.Contains("sleepingbag") ||
                     e.ShortPrefabName.Contains("bed") ||
                     e.ShortPrefabName.Contains("beachtowel")));

            foreach (var bag in sleepingBags)
            {
                if (bag != null && !bag.IsDestroyed)
                {
                    bag.Kill();
                    count++;
                }
            }

            if (config.EnableChatNotifications)
            {
                BroadcastToChat(GetMessage("SleepingBagCleanupInfo", count));
            }
            
            return count;
        }
        #endregion
    }
} 