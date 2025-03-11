using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("FireGloves", "RustGPT", "1.0.0")]
    [Description("Огненные перчатки для переплавки ресурсов")]
    public class FireGloves : RustPlugin
    {
        #region Fields
        private const string GlovesPermissionPrefix = "firegloves";
        private const string LootPermission = "firegloves.glovesloot";
        private const int GlovesSlot = 6; // 7-й слот (индексация с 0)
        private bool LanguageEnglish = false;
        private Configuration config;
        private Dictionary<ulong, GlovesData> activeGloves = new Dictionary<ulong, GlovesData>();
        private DynamicConfigFile activeGlovesData;
        private Timer saveTimer;
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Общие настройки")]
            public GlobalSettings Settings { get; set; }

            [JsonProperty("Список огненных перчаток")]
            public Dictionary<string, GlovesConfig> GlovesList { get; set; }

            public class GlobalSettings
            {
                [JsonProperty("Использовать разрешение на шанс найти перчатки в ящиках: firegloves.glovesloot")]
                public bool UseGlovesLootPermission { get; set; }

                [JsonProperty("Отображать информацию в чате когда игрок надевает перчатки")]
                public bool ShowEquipInfo { get; set; }
            }

            public class GlovesConfig
            {
                [JsonProperty("Имя огненных перчаток")]
                public string Name { get; set; }

                [JsonProperty("Разрешение на использование перчаток")]
                public string Permission { get; set; }

                [JsonProperty("Рейты добываемых ресурсов в перчатках")]
                public float GatheringRate { get; set; }

                [JsonProperty("Рейты подбираемых ресурсов в перчатках")]
                public float PickupRate { get; set; }

                [JsonProperty("Кол-во радиации при подборе ресурсов")]
                public float PickupRadiation { get; set; }

                [JsonProperty("Кол-во радиации при бонусной добыче")]
                public float GatheringRadiation { get; set; }

                [JsonProperty("Кол-во юзов")]
                public int Uses { get; set; }

                [JsonProperty("Список ресурсов которые могут выплавлять перчатки")]
                public List<string> SmeltableItems { get; set; }

                [JsonProperty("Список ресурсов на которые не влияют рейты")]
                public List<string> NoRateItems { get; set; }

                [JsonProperty("Список кастомных предметов после переработки")]
                public List<RecycleItem> RecycleItems { get; set; }

                [JsonProperty("Включить удаление перчаток после N юзов - только при переплавке")]
                public bool EnableUsesOnSmelt { get; set; }

                [JsonProperty("Включить удаление перчаток после N юзов - при добыче/подборе любых ресурсов")]
                public bool EnableUsesOnAll { get; set; }

                [JsonProperty("Включить переплавку добываемых ресурсов")]
                public bool EnableGatheringSmelting { get; set; }

                [JsonProperty("Включить переплавку подбираемых ресурсов")]
                public bool EnablePickupSmelting { get; set; }

                [JsonProperty("Включить рейты добываемых ресурсов")]
                public bool EnableGatheringRates { get; set; }

                [JsonProperty("Включить рейты подбираемых ресурсов")]
                public bool EnablePickupRates { get; set; }

                [JsonProperty("Включить кастомные предметы после переработке огненных перчаток")]
                public bool EnableRecycleItems { get; set; }

                [JsonProperty("Включить выпадение перчаток из ящиков с определенным шансом")]
                public bool EnableLootDrops { get; set; }

                [JsonProperty("Включить накопление радиации при подборе ресурсов")]
                public bool EnablePickupRadiation { get; set; }

                [JsonProperty("Включить накопление радиации при бонусной добыче")]
                public bool EnableGatheringRadiation { get; set; }

                [JsonProperty("Настройка шанса выпадения из ящиков и бочек. Имя ящика/бочки | Шанс выпадения: 100.0 - 100%")]
                public Dictionary<string, float> LootContainers { get; set; }
            }

            public class RecycleItem
            {
                [JsonProperty("Шортнейм предмета")]
                public string Shortname { get; set; }

                [JsonProperty("Скин предмета")]
                public ulong SkinId { get; set; }

                [JsonProperty("Имя предмета")]
                public string Name { get; set; }

                [JsonProperty("Шанс выпадения [ 100.0 - 100% ]")]
                public float Chance { get; set; }

                [JsonProperty("Текст [ Если это записка ]")]
                public string NoteText { get; set; }

                [JsonProperty("Кол-во юзов [ Если это перчатки ]")]
                public int Uses { get; set; }

                [JsonProperty("Минимальное кол-во")]
                public int MinAmount { get; set; }

                [JsonProperty("Максимальное кол-во")]
                public int MaxAmount { get; set; }
            }
        }

        private class GlovesData
        {
            public string ConfigId { get; set; }
            public int RemainingUses { get; set; }
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
            catch
            {
                PrintError("Ошибка загрузки конфигурации! Создаю новую конфигурацию!");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            var defaultConfig = new Configuration
            {
                Settings = new Configuration.GlobalSettings
                {
                    UseGlovesLootPermission = true,
                    ShowEquipInfo = true
                },
                GlovesList = new Dictionary<string, Configuration.GlovesConfig>
                {
                    ["0"] = new Configuration.GlovesConfig
                    {
                        Name = "Огненные перчатки",
                        Permission = "firegloves.default",
                        GatheringRate = 2.0f,
                        PickupRate = 2.0f,
                        PickupRadiation = 2.0f,
                        GatheringRadiation = 1.0f,
                        Uses = 25,
                        SmeltableItems = new List<string>
                        {
                            "chicken.raw",
                            "bear.meat",
                            "deer.meat",
                            "pork.raw",
                            "wolf.meat",
                            "horse.meat",
                            "hq.metal.ore",
                            "metal.ore",
                            "sulfur.ore",
                            "wood"
                        },
                        NoRateItems = new List<string> { "diesel_barrel" },
                        RecycleItems = new List<Configuration.RecycleItem>
                        {
                            new Configuration.RecycleItem
                            {
                                Shortname = "wood",
                                SkinId = 0,
                                Name = "",
                                Chance = 50f,
                                NoteText = "",
                                Uses = 0,
                                MinAmount = 50,
                                MaxAmount = 100
                            }
                        },
                        EnableUsesOnSmelt = true,
                        EnableUsesOnAll = false,
                        EnableGatheringSmelting = true,
                        EnablePickupSmelting = true,
                        EnableGatheringRates = true,
                        EnablePickupRates = true,
                        EnableRecycleItems = true,
                        EnableLootDrops = true,
                        EnablePickupRadiation = true,
                        EnableGatheringRadiation = true,
                        LootContainers = new Dictionary<string, float>
                        {
                            ["crate_tools"] = 50.0f,
                            ["crate_normal_2"] = 50.0f
                        }
                    }
                }
            };

            Config.WriteObject(defaultConfig, true);
            config = defaultConfig;
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            LoadData();
            permission.RegisterPermission(LootPermission, this);
            foreach (var gloves in config.GlovesList)
            {
                permission.RegisterPermission(gloves.Value.Permission, this);
            }
            LoadDefaultMessages();

            // Запускаем таймер автосохранения каждые 5 минут
            saveTimer = timer.Every(300f, SaveData);
        }

        private void OnServerInitialized(bool initial)
        {
            cmd.AddChatCommand("gl_give", this, nameof(CommandGiveGloves));
            cmd.AddChatCommand("gloves", this, nameof(CommandGlovesSelf));
        }

        private void Unload()
        {
            saveTimer?.Destroy();
            SaveData();
            activeGloves.Clear();
        }

        private void OnServerShutdown()
        {
            saveTimer?.Destroy();
            SaveData();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            
            // Проверяем и удаляем данные отключившегося игрока
            if (activeGloves.ContainsKey(player.userID))
            {
                activeGloves.Remove(player.userID);
                SaveData();
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            // Проверяем наличие перчаток в слоте при подключении
            var equippedItem = player.inventory.containerWear.GetSlot(GlovesSlot);
            if (equippedItem != null && equippedItem.info.shortname == "burlap.gloves")
            {
                var skinId = equippedItem.skin.ToString();
                if (config.GlovesList.TryGetValue(skinId, out var glovesConfig))
                {
                    // Восстанавливаем данные о перчатках
                    if (!activeGloves.ContainsKey(player.userID))
                    {
                        activeGloves[player.userID] = new GlovesData
                        {
                            ConfigId = skinId,
                            RemainingUses = glovesConfig.Uses
                        };
                        SaveData();
                    }
                }
            }
        }

        private object OnItemPickup(Item item, BasePlayer player)
        {
            if (player == null || item == null) return null;

            var gloves = GetEquippedGloves(player);
            if (gloves == null) return null;

            var glovesConfig = config.GlovesList[gloves.ConfigId];
            if (!glovesConfig.EnablePickupSmelting) return null;

            if (glovesConfig.SmeltableItems.Contains(item.info.shortname))
            {
                // Применяем рейты и переплавку
                var amount = item.amount;
                if (glovesConfig.EnablePickupRates && !glovesConfig.NoRateItems.Contains(item.info.shortname))
                    amount = Mathf.FloorToInt(amount * glovesConfig.PickupRate);

                // Добавляем радиацию
                if (glovesConfig.EnablePickupRadiation && glovesConfig.PickupRadiation > 0)
                    player.metabolism.radiation_poison.Add(glovesConfig.PickupRadiation);

                // Уменьшаем количество использований
                if (glovesConfig.EnableUsesOnAll)
                    DecrementGlovesUses(player, gloves);

                // Создаем переплавленный предмет
                var smeltedItem = CreateSmeltedItem(item.info.shortname, amount);
                if (smeltedItem != null)
                {
                    player.GiveItem(smeltedItem);
                    return true; // Отменяем оригинальный подбор
                }
            }

            return null;
        }

        private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null) return null;

            var gloves = GetEquippedGloves(player);
            if (gloves == null) return null;

            var glovesConfig = config.GlovesList[gloves.ConfigId];
            if (!glovesConfig.EnableGatheringSmelting) return null;

            if (glovesConfig.SmeltableItems.Contains(item.info.shortname))
            {
                // Применяем рейты и переплавку
                var amount = item.amount;
                if (glovesConfig.EnableGatheringRates && !glovesConfig.NoRateItems.Contains(item.info.shortname))
                    amount = Mathf.FloorToInt(amount * glovesConfig.GatheringRate);

                // Добавляем радиацию
                if (glovesConfig.EnableGatheringRadiation && glovesConfig.GatheringRadiation > 0)
                    player.metabolism.radiation_poison.Add(glovesConfig.GatheringRadiation);

                // Уменьшаем количество использований
                if (glovesConfig.EnableUsesOnAll)
                    DecrementGlovesUses(player, gloves);

                // Создаем переплавленный предмет
                var smeltedItem = CreateSmeltedItem(item.info.shortname, amount);
                if (smeltedItem != null)
                {
                    player.GiveItem(smeltedItem);
                    return true; // Отменяем оригинальный сбор
                }
            }

            return null;
        }

        private object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            if (inventory == null || item == null) return null;
            var player = inventory.baseEntity;
            if (player == null) return null;

            // Проверяем, являются ли это перчатками
            if (item.info.shortname != "burlap.gloves") return null;

            // Проверяем слот
            if (targetSlot != GlovesSlot)
            {
                // Если перчатки надеты не в 7-й слот, перемещаем их обратно в инвентарь
                item.MoveToContainer(player.inventory.containerMain);
                PrintToChat(player, GetMsg("WrongSlot", player.UserIDString));
                return false; // Запрещаем надевать
            }

            var skinId = item.skin.ToString();
            if (!config.GlovesList.ContainsKey(skinId)) return null;

            var glovesConfig = config.GlovesList[skinId];
            
            // Проверяем разрешение
            if (!string.IsNullOrEmpty(glovesConfig.Permission) && !permission.UserHasPermission(player.UserIDString, glovesConfig.Permission))
            {
                item.MoveToContainer(player.inventory.containerMain);
                PrintToChat(player, GetMsg("NoPermission", player.UserIDString));
                return false; // Запрещаем надевать
            }

            // Регистрируем активные перчатки
            activeGloves[player.userID] = new GlovesData
            {
                ConfigId = skinId,
                RemainingUses = glovesConfig.Uses
            };

            if (config.Settings.ShowEquipInfo)
                PrintToChat(player, GetMsg("GlovesEquipped", player.UserIDString));

            return null;
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null) return;
            
            // Проверяем, является ли контейнер слотом экипировки
            if (container.playerOwner == null || container != container.playerOwner.inventory.containerWear) return;

            if (item.info.shortname != "burlap.gloves") return;

            var player = container.playerOwner;
            activeGloves.Remove(player.userID);
        }

        private object OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return null;

            // Проверяем включена ли функция выпадения перчаток для каждого типа
            foreach (var glovesEntry in config.GlovesList)
            {
                var glovesConfig = glovesEntry.Value;
                
                // Пропускаем если выпадение отключено
                if (!glovesConfig.EnableLootDrops) 
                    continue;

                // Проверяем требуется ли разрешение на лут
                if (config.Settings.UseGlovesLootPermission && !permission.UserHasPermission(container.OwnerID.ToString(), LootPermission))
                    continue;

                // Проверяем есть ли шанс для данного контейнера
                if (!glovesConfig.LootContainers.TryGetValue(container.ShortPrefabName, out float chance))
                    continue;

                // Проверяем шанс
                if (UnityEngine.Random.Range(0f, 100f) > chance)
                    continue;

                // Создаем перчатки
                var gloves = ItemManager.CreateByName("burlap.gloves", 1, ulong.Parse(glovesEntry.Key));
                if (gloves == null) continue;

                // Устанавливаем имя
                gloves.name = glovesConfig.Name;

                // Добавляем в контейнер
                if (!container.inventory.IsFull())
                {
                    container.inventory.Insert(gloves);
                    Puts($"Добавлены перчатки {glovesConfig.Name} в контейнер {container.ShortPrefabName}");
                }
            }

            return null;
        }

        private void OnServerSave() => SaveData();
        #endregion

        #region Commands
        [ChatCommand("gl_give")]
        private void CommandGiveGloves(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args.Length != 2)
            {
                PrintToChat(player, "InvalidCommand");
                return;
            }

            var targetId = args[0];
            var skinId = args[1];

            if (!config.GlovesList.ContainsKey(skinId))
            {
                PrintToChat(player, "InvalidGloves");
                return;
            }

            var target = BasePlayer.Find(targetId);
            if (target == null)
            {
                PrintToChat(player, "PlayerNotFound");
                return;
            }

            GiveGloves(target, skinId);
            PrintToChat(player, "GlovesGiven", target.displayName);
        }

        [ChatCommand("gloves")]
        private void CommandGlovesSelf(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                var availableGloves = new List<string>();
                foreach (var glove in config.GlovesList)
                {
                    if (permission.UserHasPermission(player.UserIDString, glove.Value.Permission))
                    {
                        availableGloves.Add($"{glove.Key} ({glove.Value.Name})");
                    }
                }

                if (availableGloves.Count == 0)
                {
                    PrintToChat(player, "NoGlovesAvailable");
                    return;
                }

                PrintToChat(player, "GlovesList", string.Join(", ", availableGloves));
                return;
            }

            var skinId = args[0];
            if (!config.GlovesList.ContainsKey(skinId))
            {
                PrintToChat(player, "InvalidGloves");
                return;
            }

            var glovesConfig = config.GlovesList[skinId];
            if (!string.IsNullOrEmpty(glovesConfig.Permission) && !permission.UserHasPermission(player.UserIDString, glovesConfig.Permission))
            {
                PrintToChat(player, "NoPermission");
                return;
            }

            GiveGloves(player, skinId);
        }
        #endregion

        #region Core Methods
        private void GiveGloves(BasePlayer player, string skinId)
        {
            if (!config.GlovesList.TryGetValue(skinId, out var glovesConfig))
                return;

            var gloves = ItemManager.CreateByName("burlap.gloves", 1, ulong.Parse(skinId));
            if (gloves == null)
                return;

            gloves.name = glovesConfig.Name;
            if (!player.inventory.GiveItem(gloves))
            {
                // Если инвентарь полон, бросаем перчатки на землю
                gloves.Drop(player.transform.position + new Vector3(0f, 1f, 0f), Vector3.zero);
            }
        }

        private string GetMsg(string key, string userId = null)
        {
            return lang.GetMessage(key, this, userId);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                // Russian messages
                ["InvalidCommand"] = "Использование: /gl_give <steamid> <skinid>",
                ["InvalidGloves"] = "Указанные перчатки не найдены",
                ["PlayerNotFound"] = "Игрок не найден",
                ["GlovesGiven"] = "Перчатки выданы игроку {0}",
                ["GlovesEquipped"] = "Вы надели огненные перчатки",
                ["GlovesBroken"] = "Ваши огненные перчатки сломались",
                ["WrongSlot"] = "Огненные перчатки можно надеть только в 7-й слот!",
                ["NoPermission"] = "У вас нет разрешения использовать эти перчатки!",
                ["GlovesList"] = "Доступные перчатки: {0}",
                ["NoGlovesAvailable"] = "У вас нет доступных перчаток"
            };

            lang.RegisterMessages(messages, this);

            if (LanguageEnglish)
            {
                messages = new Dictionary<string, string>
                {
                    ["InvalidCommand"] = "Usage: /gl_give <steamid> <skinid>",
                    ["InvalidGloves"] = "Specified gloves not found",
                    ["PlayerNotFound"] = "Player not found",
                    ["GlovesGiven"] = "Gloves given to player {0}",
                    ["GlovesEquipped"] = "You equipped fire gloves",
                    ["GlovesBroken"] = "Your fire gloves have broken",
                    ["WrongSlot"] = "Fire gloves can only be equipped in the 7th slot!",
                    ["NoPermission"] = "You don't have permission to use these gloves!",
                    ["GlovesList"] = "Available gloves: {0}",
                    ["NoGlovesAvailable"] = "You don't have any available gloves"
                };
            }

            // Ukrainian messages
            var ukrainianMessages = new Dictionary<string, string>
            {
                ["InvalidCommand"] = "Використання: /gl_give <steamid> <skinid>",
                ["InvalidGloves"] = "Вказані рукавиці не знайдені",
                ["PlayerNotFound"] = "Гравець не знайдений",
                ["GlovesGiven"] = "Рукавиці видані гравцю {0}",
                ["GlovesEquipped"] = "Ви надягли вогняні рукавиці",
                ["GlovesBroken"] = "Ваші вогняні рукавиці зламалися",
                ["WrongSlot"] = "Вогняні рукавиці можна надіти тільки в 7-й слот!",
                ["NoPermission"] = "У вас немає дозволу використовувати ці рукавиці!",
                ["GlovesList"] = "Доступні рукавиці: {0}",
                ["NoGlovesAvailable"] = "У вас немає доступних рукавиць"
            };
            lang.RegisterMessages(ukrainianMessages, this, "uk");

            // Spanish messages
            var spanishMessages = new Dictionary<string, string>
            {
                ["InvalidCommand"] = "Uso: /gl_give <steamid> <skinid>",
                ["InvalidGloves"] = "Guantes especificados no encontrados",
                ["PlayerNotFound"] = "Jugador no encontrado",
                ["GlovesGiven"] = "Guantes entregados al jugador {0}",
                ["GlovesEquipped"] = "Te has equipado los guantes de fuego",
                ["GlovesBroken"] = "Tus guantes de fuego se han roto",
                ["WrongSlot"] = "¡Los guantes de fuego solo se pueden equipar en la ranura 7!",
                ["NoPermission"] = "¡No tienes permiso para usar estos guantes!",
                ["GlovesList"] = "Guantes disponibles: {0}",
                ["NoGlovesAvailable"] = "No tienes guantes disponibles"
            };
            lang.RegisterMessages(spanishMessages, this, "es");
        }
        #endregion

        #region Helper Methods
        private GlovesData GetEquippedGloves(BasePlayer player)
        {
            if (player == null) return null;

            // Проверяем наличие перчаток в активном списке
            if (!activeGloves.TryGetValue(player.userID, out var gloves))
                return null;

            // Проверяем наличие перчаток в слоте
            var equippedItem = player.inventory.containerWear.GetSlot(GlovesSlot);
            if (equippedItem == null || equippedItem.info.shortname != "burlap.gloves")
            {
                activeGloves.Remove(player.userID);
                return null;
            }

            return gloves;
        }

        private void DecrementGlovesUses(BasePlayer player, GlovesData gloves)
        {
            if (gloves == null || player == null) return;

            gloves.RemainingUses--;
            if (gloves.RemainingUses <= 0)
            {
                // Получаем перчатки из слота
                var equippedGloves = player.inventory.containerWear.GetSlot(GlovesSlot);
                if (equippedGloves != null)
                {
                    // Удаляем перчатки
                    equippedGloves.Remove();
                    activeGloves.Remove(player.userID);

                    var glovesConfig = config.GlovesList[gloves.ConfigId];
                    PrintToChat(player, GetMsg("GlovesBroken", player.UserIDString));

                    // Выдаем предметы после переработки
                    if (glovesConfig.EnableRecycleItems && glovesConfig.RecycleItems != null && glovesConfig.RecycleItems.Count > 0)
                    {
                        foreach (var recycleItem in glovesConfig.RecycleItems)
                        {
                            if (UnityEngine.Random.Range(0f, 100f) <= recycleItem.Chance)
                            {
                                var amount = UnityEngine.Random.Range(recycleItem.MinAmount, recycleItem.MaxAmount + 1);
                                var item = ItemManager.CreateByName(recycleItem.Shortname, amount, recycleItem.SkinId);
                                if (item != null)
                                {
                                    if (!string.IsNullOrEmpty(recycleItem.Name))
                                        item.name = recycleItem.Name;
                                    
                                    if (!string.IsNullOrEmpty(recycleItem.NoteText))
                                        item.text = recycleItem.NoteText;

                                    player.GiveItem(item);
                                }
                            }
                        }
                    }
                }
            }
        }

        private Item CreateSmeltedItem(string shortname, int amount)
        {
            switch (shortname)
            {
                case "metal.ore":
                    return ItemManager.CreateByName("metal.fragments", amount);
                case "sulfur.ore":
                    return ItemManager.CreateByName("sulfur", amount);
                case "hq.metal.ore":
                    return ItemManager.CreateByName("metal.refined", Mathf.FloorToInt(amount * 0.5f));
                case "wood":
                    return ItemManager.CreateByName("wood", amount);
                case "chicken.raw":
                    return ItemManager.CreateByName("chicken.cooked", amount);
                case "bear.meat":
                    return ItemManager.CreateByName("bearmeat.cooked", amount);
                case "deer.meat":
                    return ItemManager.CreateByName("deermeat.cooked", amount);
                case "pork.raw":
                    return ItemManager.CreateByName("meat.pork.cooked", amount);
                case "wolf.meat":
                    return ItemManager.CreateByName("wolfmeat.cooked", amount);
                case "horse.meat":
                    return ItemManager.CreateByName("horsemeat.cooked", amount);
                default:
                    return null;
            }
        }

        private new void PrintToChat(BasePlayer player, string message, params object[] args)
        {
            if (player == null) return;
            Player.Message(player, string.Format(GetMsg(message, player.UserIDString), args));
        }
        #endregion

        private void SaveData()
        {
            if (activeGlovesData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject("XFireGloves_Data", activeGloves);
            }
        }

        private void LoadData()
        {
            activeGlovesData = Interface.Oxide.DataFileSystem.GetFile("XFireGloves_Data");
            try
            {
                activeGloves = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, GlovesData>>("XFireGloves_Data") ?? new Dictionary<ulong, GlovesData>();
            }
            catch
            {
                activeGloves = new Dictionary<ulong, GlovesData>();
            }
        }
    }
} 