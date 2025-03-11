using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json; 
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FCleaner", "kamAp", "1.2.4")]
    public class FCleaner : RustPlugin
    {
        private Timer cleanTimer;
        private Configuration config;

        private Dictionary<string, int> playerItemDropCounts = new Dictionary<string, int>();
        private Dictionary<string, Timer> resetTimers = new Dictionary<string, Timer>();

        private List<VendingMachineMapMarker> activeMarkers = new List<VendingMachineMapMarker>();

        private Dictionary<string, int> droppedItemCountByType = new Dictionary<string, int>();

        #region Configuration

        public class Configuration
        {
            [JsonProperty("ПрефиксСообщений")]
            public string Prefix { get; set; } = "[FCleaner]";

            [JsonProperty("ЦветПрефикса")]
            public string PrefixColor { get; set; } = "#34eb4f";

            [JsonProperty("ИнтервалОчистки_в_секундах")]
            public float CleanInterval { get; set; } = 200f;

            [JsonProperty("Очищать_выброшенные_предметы")]
            public bool CleanDroppedItems { get; set; } = true;

            [JsonProperty("Очищать_трупы_игроков")]
            public bool CleanPlayerCorpses { get; set; } = true;

            [JsonProperty("Очищать_рюкзаки_NPC")]
            public bool CleanNPCBackpacks { get; set; } = true;

            [JsonProperty("Очищать_рюкзаки_из_предметов")]
            public bool CleanDropBackpacks { get; set; } = true;

            [JsonProperty("Очищать_трупы_с_лутом")]
            public bool CleanLootableCorpses { get; set; } = true;

            [JsonProperty("Очищать_неактивные_объекты")]
            public bool CleanInactiveEntities { get; set; } = true;

            [JsonProperty("Время_уведомления_перед_очисткой_сек")]
            public float CleanNotificationTime { get; set; } = 30f;

            [JsonProperty("Список_исключенных_предметов")]
            public List<string> ExcludedItems { get; set; } = new List<string>
            {
                "horsedung",
                "keycard_blue",
                "keycard_green",
                "keycard_red"
            };

            [JsonProperty("Включить_бан_за_спам_выбрасыванием")]
            public bool BanEnabled { get; set; } = true;

            [JsonProperty("Порог_предупреждения_при_выбросе")]
            public int ItemDropWarningThreshold { get; set; } = 30;

            [JsonProperty("Порог_бана_при_выбросе")]
            public int ItemDropBanThreshold { get; set; } = 50;

            [JsonProperty("Время_сброса_счетчика_сек")]
            public float ItemDropResetTime { get; set; } = 60f;

            [JsonProperty("Длительность_бана_в_минутах_(опционально)")]
            public int BanDuration { get; set; } = 60;

            [JsonProperty("Сообщение_предупреждения")]
            public string WarningMessage { get; set; } = "Внимание: Вы выбросили {0} предметов. Вас забанят, если вы продолжите.";

            [JsonProperty("Причина_бана")]
            public string BanReason { get; set; } = "Слишком много выброшенных предметов за короткий срок.";

            [JsonProperty("Разрешение_админа")]
            public string AdminPermission { get; set; } = "fcleaner.admin";

            [JsonProperty("Разрешение_на_уведомление")]
            public string NotifyPermission { get; set; } = "fcleaner.notify";

            [JsonProperty("Показывать_метки_на_карте")]
            public bool ShowMarkersOnMap { get; set; } = true;

            [JsonProperty("Включить_уведомления_в_чате")]
            public bool EnableChatNotifications { get; set; } = true;

            [JsonProperty("Отключить_коллизии_у_предметов")]
            public bool DisableItemCollisions { get; set; } = false;

            [JsonProperty("Включить_авто_очистку_по_типам")]
            public bool AutoCleanByTypeEnabled { get; set; } = false;

            [JsonProperty("Порог_авто_очистки_по_типам")]
            public int AutoCleanTypeThreshold { get; set; } = 100;

            [JsonProperty("Включить_пошаговую_очистку")]
            public bool EnablePartialCleanup { get; set; } = true;

            [JsonProperty("Размер_пакета_очистки")]
            public int CleanupChunkSize { get; set; } = 50;

            [JsonProperty("Задержка_между_пакетами_очистки_сек")]
            public float CleanupChunkDelay { get; set; } = 0.5f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception("Failed to load configuration");
            }
            catch
            {
                PrintWarning("Error loading configuration. Restoring defaults...");
                LoadDefaultConfig();
            }

            config.ExcludedItems = config.ExcludedItems.Distinct().ToList();

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            // Английский
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CleaningInterval"] = "Cleanup every {0} seconds.",
                ["TotalCleaned"] = "Removed objects: ({0}) total, ({1}) dropped items, ({2}) player corpses, ({3}) NPC backpacks, ({4}) item drop backpacks, ({5}) lootable corpses, ({6}) inactive entities.",
                ["CleaningNotification"] = "Cleanup will start in {0} seconds at marked locations.",
                ["DropWarning"] = "Warning: You have dropped {0} items. You will be banned if you continue.",
                ["DropBan"] = "Player {0} has been banned for: {1}",
                ["AdminNotify"] = "Player {0} ({1}) is close to being banned for item dropping.",
                ["MarkerAdded"] = "Cleanup marker added at {0}.",
                ["MarkersRemoved"] = "All cleanup markers have been removed.",
                ["ForceCleanTriggered"] = "Forced cleanup has been triggered!"
            }, this, "en");

            // Русский
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CleaningInterval"] = "Очистка каждые {0} секунд.",
                ["TotalCleaned"] = "Удалено объектов: ({0}) всего, ({1}) выброшенных предметов, ({2}) трупов игроков, ({3}) рюкзаков NPC, ({4}) рюкзаков с предметами, ({5}) добычных трупов, ({6}) неактивных сущностей.",
                ["CleaningNotification"] = "Очистка начнется через {0} секунд в отмеченных местах.",
                ["DropWarning"] = "Внимание: Вы выбросили {0} предметов. Вас забанят, если вы продолжите.",
                ["DropBan"] = "Игрок {0} был забанен по причине: {1}",
                ["AdminNotify"] = "Игрок {0} ({1}) близок к бану за выброс предметов.",
                ["MarkerAdded"] = "Метка для очистки добавлена в {0}.",
                ["MarkersRemoved"] = "Все метки очистки были удалены.",
                ["ForceCleanTriggered"] = "Принудительная очистка запущена!"
            }, this, "ru");
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
          
            permission.RegisterPermission(config.AdminPermission, this);
            permission.RegisterPermission(config.NotifyPermission, this);

            cleanTimer = timer.Repeat(config.CleanInterval, 0, PrepareForCleanup);

            string initMessage = string.Format(lang.GetMessage("CleaningInterval", this, null), config.CleanInterval);
            string formattedMessage = $"<color={config.PrefixColor}>{config.Prefix}</color> {initMessage}";

            if (config.EnableChatNotifications)
            {
                Server.Broadcast(formattedMessage);
            }
        }

        private void Unload()
        {
            cleanTimer?.Destroy();

            foreach (var t in resetTimers.Values)
                t.Destroy();
            resetTimers.Clear();

            RemoveAllMarkers();
        }

    
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is DroppedItem droppedItem)
            {
                if (config.DisableItemCollisions)
                {
                    var collider = droppedItem.GetComponent<Collider>();
                    if (collider != null) collider.enabled = false;
                }


                if (config.AutoCleanByTypeEnabled)
                {
                    var shortName = droppedItem.item?.info?.shortname;
                    if (shortName == null) return;


                    if (!config.ExcludedItems.Contains(shortName))
                    {
                        if (!droppedItemCountByType.ContainsKey(shortName))
                            droppedItemCountByType[shortName] = 0;

                        droppedItemCountByType[shortName]++;

                        if (droppedItemCountByType[shortName] >= config.AutoCleanTypeThreshold)
                        {
                            var allDropped = UnityEngine.Object.FindObjectsOfType<DroppedItem>()
                                .Where(d => d?.item?.info?.shortname == shortName)
                                .ToList();

                            foreach (var di in allDropped)
                                di?.Kill();

                            Puts($"Auto-clean removed {allDropped.Count} items of type '{shortName}' (threshold {config.AutoCleanTypeThreshold}).");

                            droppedItemCountByType[shortName] = 0;
                        }
                    }
                }
            }
        }


        private void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action != "drop") return;
            if (player == null) return;

            if (permission.UserHasPermission(player.UserIDString, config.AdminPermission)) return;
            if (!config.BanEnabled) return;

            string playerId = player.UserIDString;
            if (!playerItemDropCounts.ContainsKey(playerId))
            {
                playerItemDropCounts[playerId] = 1;
                resetTimers[playerId] = timer.Once(config.ItemDropResetTime, () => ResetDropCount(playerId));
            }
            else
            {
                playerItemDropCounts[playerId]++;
                int leftUntilBan = config.ItemDropBanThreshold - playerItemDropCounts[playerId];

                if (leftUntilBan <= 5 && leftUntilBan > 0)
                {
                    Puts($"Player {player.displayName} has dropped another item. Count = {playerItemDropCounts[playerId]}, left until ban = {leftUntilBan}.");
                }

                if (playerItemDropCounts[playerId] >= config.ItemDropWarningThreshold
                    && playerItemDropCounts[playerId] < config.ItemDropBanThreshold)
                {
                    if (config.EnableChatNotifications)
                    {
                        player.ChatMessage(string.Format(
                            lang.GetMessage("DropWarning", this, player.UserIDString),
                            playerItemDropCounts[playerId]));
                    }
                    NotifyAdmins(string.Format(lang.GetMessage("AdminNotify", this, player.UserIDString),
                        player.displayName, playerId));
                }
                else if (playerItemDropCounts[playerId] >= config.ItemDropBanThreshold)
                {
                    BanPlayer(player.IPlayer, config.BanReason);
                }
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand("fcleaner.forceclean")]
        private void ForceCleanCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;

            Puts(lang.GetMessage("ForceCleanTriggered", this, null));
            arg.ReplyWith(lang.GetMessage("ForceCleanTriggered", this, null));

            if (config.EnableChatNotifications)
            {
                Server.Broadcast($"<color={config.PrefixColor}>{config.Prefix}</color> {lang.GetMessage("ForceCleanTriggered", this, null)}");
            }

            CleanUpAll();
        }

        #endregion

        #region Cleanup Logic

        private void PrepareForCleanup()
        {
            if (config.AutoCleanByTypeEnabled)
            {
                AutoCleanByType();
            }

            if (config.ShowMarkersOnMap)
            {
                NotifyBeforeClean();
            }
            else
            {
                timer.Once(config.CleanNotificationTime, CleanUpAll);
            }
        }

        private void NotifyBeforeClean()
        {
            var positionsToClean = CollectTargetsPositions();

            foreach (var pos in positionsToClean)
            {
                AddMarker(pos);
            }

            if (config.EnableChatNotifications)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null) continue;
                    string notificationMessage = string.Format(lang.GetMessage("CleaningNotification", this, player.UserIDString), config.CleanNotificationTime);
                    player.ChatMessage($"<color={config.PrefixColor}>{config.Prefix}</color> {notificationMessage}");
                }
            }

            timer.Once(config.CleanNotificationTime, CleanUpAll);
        }

        private List<Vector3> CollectTargetsPositions()
        {
            var result = new List<Vector3>();

            if (config.CleanDroppedItems)
            {
                var dropped = UnityEngine.Object.FindObjectsOfType<DroppedItem>();
                foreach (var item in dropped)
                {
                    if (!config.ExcludedItems.Contains(item.item.info.shortname))
                    {
                        result.Add(item.transform.position);
                    }
                }
            }

            if (config.CleanPlayerCorpses)
            {
                var corpses = UnityEngine.Object.FindObjectsOfType<PlayerCorpse>();
                foreach (var corpse in corpses)
                {
                    result.Add(corpse.transform.position);
                }
            }

            if (config.CleanNPCBackpacks)
            {
                var npcCorpses = UnityEngine.Object.FindObjectsOfType<NPCPlayerCorpse>();
                foreach (var npc in npcCorpses)
                {
                    result.Add(npc.transform.position);
                }
            }

            if (config.CleanDropBackpacks)
            {
                var itemBackpacks = UnityEngine.Object.FindObjectsOfType<BaseNetworkable>()
                    .Where(n => n.ShortPrefabName.Equals("item_drop_backpack", StringComparison.OrdinalIgnoreCase));
                foreach (var ib in itemBackpacks)
                {
                    result.Add(ib.transform.position);
                }
            }

            if (config.CleanLootableCorpses)
            {
                var lootable = UnityEngine.Object.FindObjectsOfType<LootableCorpse>();
                foreach (var corpse in lootable)
                {
                    if (corpse.playerSteamID == 0)
                    {
                        result.Add(corpse.transform.position);
                    }
                }
            }

            if (config.CleanInactiveEntities)
            {
                var allEnts = BaseNetworkable.serverEntities.Cast<BaseNetworkable>();
                foreach (var ent in allEnts)
                {
                    if (ent == null || ent.IsDestroyed || !ent.gameObject.activeSelf)
                    {
                        result.Add(ent.transform.position);
                    }
                }
            }

            return result;
        }

        private void CleanUpAll()
        {
            var cleanedDropped = 0;
            var cleanedPlayerCorpses = 0;
            var cleanedNPCBackpacks = 0;
            var cleanedDropBackpacks = 0;
            var cleanedLootableCorpses = 0;
            var cleanedInactive = 0;
            var cleanedZeroPoint = 0; 

            RemoveAllMarkers();

            var droppedItems = new List<DroppedItem>();
            var playerCorpses = new List<PlayerCorpse>();
            var npcCorpses = new List<NPCPlayerCorpse>();
            var dropBackpacks = new List<BaseNetworkable>();
            var lootableCorpses = new List<LootableCorpse>();
            var inactiveEntities = new List<BaseNetworkable>();
            var zeroPointEntities = new List<BaseNetworkable>();

            if (config.CleanDroppedItems)
            {
                droppedItems = UnityEngine.Object.FindObjectsOfType<DroppedItem>()
                    .Where(i => !config.ExcludedItems.Contains(i.item.info.shortname))
                    .ToList();
            }
            if (config.CleanPlayerCorpses)
            {
                playerCorpses = UnityEngine.Object.FindObjectsOfType<PlayerCorpse>().ToList();
            }
            if (config.CleanNPCBackpacks)
            {
                npcCorpses = UnityEngine.Object.FindObjectsOfType<NPCPlayerCorpse>().ToList();
            }
            if (config.CleanDropBackpacks)
            {
                dropBackpacks = UnityEngine.Object.FindObjectsOfType<BaseNetworkable>()
                    .Where(n => n.ShortPrefabName.Equals("item_drop_backpack", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            if (config.CleanLootableCorpses)
            {
                lootableCorpses = UnityEngine.Object.FindObjectsOfType<LootableCorpse>()
                    .Where(c => c.playerSteamID == 0)
                    .ToList();
            }
            if (config.CleanInactiveEntities)
            {
                inactiveEntities = BaseNetworkable.serverEntities
                    .Where(e => e == null || e.IsDestroyed || !e.gameObject.activeSelf)
                    .ToList();
            }

            zeroPointEntities = UnityEngine.Object.FindObjectsOfType<BaseNetworkable>()
                .Where(e => e != null && !e.IsDestroyed &&
                            Vector3.Distance(e.transform.position, Vector3.zero) < 10f &&
                            (e.ShortPrefabName.Contains("boat") ||
                             e.ShortPrefabName.Contains("tugboat") ||
                             e.ShortPrefabName.Contains("rhib")))
                .ToList();

            if (config.EnablePartialCleanup)
            {
                CleanUpEntitiesInChunks(droppedItems, config.CleanupChunkSize, config.CleanupChunkDelay,
                    () => cleanedDropped += droppedItems.Count);

                CleanUpEntitiesInChunks(playerCorpses, config.CleanupChunkSize, config.CleanupChunkDelay,
                    () => cleanedPlayerCorpses += playerCorpses.Count);

                CleanUpEntitiesInChunks(npcCorpses, config.CleanupChunkSize, config.CleanupChunkDelay,
                    () => cleanedNPCBackpacks += npcCorpses.Count);

                CleanUpEntitiesInChunks(dropBackpacks, config.CleanupChunkSize, config.CleanupChunkDelay,
                    () => cleanedDropBackpacks += dropBackpacks.Count);

                CleanUpEntitiesInChunks(lootableCorpses, config.CleanupChunkSize, config.CleanupChunkDelay,
                    () => cleanedLootableCorpses += lootableCorpses.Count);

                CleanUpEntitiesInChunks(inactiveEntities, config.CleanupChunkSize, config.CleanupChunkDelay,
                    () => cleanedInactive += inactiveEntities.Count);

                CleanUpEntitiesInChunks(zeroPointEntities, config.CleanupChunkSize, config.CleanupChunkDelay,
                    () => cleanedZeroPoint += zeroPointEntities.Count);
            }
            else
            {
                foreach (var di in droppedItems) di?.Kill();
                cleanedDropped += droppedItems.Count;

                foreach (var pc in playerCorpses) pc?.Kill();
                cleanedPlayerCorpses += playerCorpses.Count;

                foreach (var nc in npcCorpses) nc?.Kill();
                cleanedNPCBackpacks += npcCorpses.Count;

                foreach (var db in dropBackpacks) db?.Kill();
                cleanedDropBackpacks += dropBackpacks.Count;

                foreach (var lc in lootableCorpses) lc?.Kill();
                cleanedLootableCorpses += lootableCorpses.Count;

                foreach (var ie in inactiveEntities) ie?.Kill();
                cleanedInactive += inactiveEntities.Count;

                foreach (var zp in zeroPointEntities) zp?.Kill();
                cleanedZeroPoint += zeroPointEntities.Count;
            }

            var totalCleaned = cleanedDropped + cleanedPlayerCorpses + cleanedNPCBackpacks +
                               cleanedDropBackpacks + cleanedLootableCorpses + cleanedInactive + cleanedZeroPoint;

            if (config.EnableChatNotifications)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null) continue;

                    string cleanedMessage = string.Format(
                        lang.GetMessage("TotalCleaned", this, player.UserIDString),
                        totalCleaned, cleanedDropped, cleanedPlayerCorpses, cleanedNPCBackpacks,
                        cleanedDropBackpacks, cleanedLootableCorpses, cleanedInactive);

                    player.ChatMessage($"<color={config.PrefixColor}>{config.Prefix}</color> {cleanedMessage}");
                }
            }
        }

        private void CleanUpEntitiesInChunks<T>(List<T> entities, int chunkSize, float delay, Action onComplete) where T : BaseNetworkable
        {
            if (entities == null || entities.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var localList = new List<T>(entities);

            void CleanChunk()
            {
                if (localList.Count == 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                int count = Mathf.Min(chunkSize, localList.Count);
                var chunk = localList.GetRange(0, count);

                foreach (var ent in chunk)
                {
                    ent?.Kill();
                }

                localList.RemoveRange(0, count);

                if (localList.Count > 0)
                {
                    timer.Once(delay, CleanChunk);
                }
                else
                {
                    onComplete?.Invoke();
                }
            }

            CleanChunk();
        }

        private void AutoCleanByType()
        {
            var items = UnityEngine.Object.FindObjectsOfType<DroppedItem>();
            var itemCount = new Dictionary<string, int>();

            foreach (var i in items)
            {
                var shortName = i.item.info.shortname;
                if (config.ExcludedItems.Contains(shortName)) continue;

                if (!itemCount.ContainsKey(shortName))
                    itemCount[shortName] = 0;

                itemCount[shortName]++;
            }

            foreach (var kvp in itemCount)
            {
                if (kvp.Value >= config.AutoCleanTypeThreshold)
                {
                    var shortName = kvp.Key;
                    var toRemove = items.Where(x => x.item.info.shortname.Equals(shortName)).ToList();
                    foreach (var rem in toRemove) rem?.Kill();

                    Puts($"Auto-clean removed {toRemove.Count} items of type '{shortName}' (threshold {config.AutoCleanTypeThreshold}).");
                }
            }
        }

        #endregion

        #region Markers

        private void AddMarker(Vector3 position)
        {
            if (!config.ShowMarkersOnMap) return;

            try
            {
                var marker = GameManager.server.CreateEntity(
                    "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab",
                    position) as VendingMachineMapMarker;
                if (marker != null)
                {
                    marker.markerShopName = "Cleanup zone";
                    marker.Spawn();
                    activeMarkers.Add(marker);
                }
            }
            catch (Exception ex)
            {
                Puts($"Error adding marker: {ex.Message}");
            }
        }

        private void RemoveAllMarkers()
        {
            foreach (var marker in activeMarkers)
            {
                if (marker != null && !marker.IsDestroyed)
                {
                    marker.Kill();
                }
            }
            activeMarkers.Clear();
            Puts(lang.GetMessage("MarkersRemoved", this, null));
        }

        #endregion

        #region Helpers (Ban, notify, etc.)

        private void ResetDropCount(string playerId)
        {
            playerItemDropCounts.Remove(playerId);
            if (resetTimers.ContainsKey(playerId))
            {
                resetTimers[playerId].Destroy();
                resetTimers.Remove(playerId);
            }
        }


        private void BanPlayer(IPlayer player, string reason)
        {
            if (!config.BanEnabled) return;
            if (player == null) return;

            if (config.BanDuration > 0)
            {

                player.Ban(reason);
            }
            else
            {
                player.Ban(reason);
            }

            Puts(string.Format(lang.GetMessage("DropBan", this, player.Id), player.Name, reason));
        }


        private void NotifyAdmins(string message)
        {
            bool notified = false;
            foreach (var bp in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(bp.UserIDString, config.NotifyPermission))
                {
                    if (config.EnableChatNotifications)
                    {
                        bp.ChatMessage(message);
                    }
                    notified = true;
                }
            }
            if (notified)
            {
                Puts(message);
            }
        }

        #endregion
    }
}
