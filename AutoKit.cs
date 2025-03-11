using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins 
{
    [Info("AutoKit", "walkinrey", "1.0.3")]
    public class AutoKit : RustPlugin 
    {
        #region Конфигурация

        private Configuration _config;
        private StoredData _storedData;

        public class StoredData
        {
            public Dictionary<ulong, Dictionary<string, List<SavedLoadout>>> PlayerLoadouts = new Dictionary<ulong, Dictionary<string, List<SavedLoadout>>>();
        }

        public class SavedLoadout
        {
            public string Name { get; set; }
            public List<Configuration.Biome.ItemConfig> Items { get; set; }
        }

        public class Configuration 
        {
            [JsonProperty("Биомы")] 
            public Biomes biomes = new Biomes();

            public class Biomes
            {
                [JsonProperty("Тундра")] 
                public Biome tundra = new Biome();

                [JsonProperty("Пустыня")] 
                public Biome dust = new Biome();

                [JsonProperty("Зима")] 
                public Biome winter = new Biome();

                [JsonProperty("Умеренный")] 
                public Biome normal = new Biome();
            }

            public class Biome
            {
                [JsonProperty("Разрешение для использования")] 
                public string permission = "autokit.normal";

                [JsonProperty("Выдавать набор в этом биоме?")] 
                public bool recive = true;

                [JsonProperty("Очищать инвентарь перед выдачей?")]
                public bool strip = true;

                [JsonProperty("Предметы к выдаче")] 
                public List<ItemConfig> items = new List<ItemConfig>();

                public class ItemConfig
                {
                    [JsonProperty("Отображаемое название")] 
                    public string name = "Штаны";

                    [JsonProperty("Скин ID предмета")] 
                    public ulong id = 0;

                    [JsonProperty("Shortname предмета")] 
                    public string shortname = "pants";

                    [JsonProperty("Количество")] 
                    public int amount = 1;

                    [JsonProperty("В какой контейнер помещать? (wear, main, belt)")] 
                    public string container = "wear";

                    [JsonProperty("Позиция в инвентаре")]
                    public int position = -1;

                    [JsonProperty("Количество патронов")]
                    public int ammo = 0;

                    [JsonProperty("Тип патронов")]
                    public string ammoType = "";

                    [JsonProperty("Модификации оружия")]
                    public Dictionary<string, string> mods = new Dictionary<string, string>();

                    public ItemConfig(ulong sourceID = 0, string sourceShortname = "", int sourceAmount = 0, string sourceContainer = "", int sourcePosition = -1)
                    {
                        id = sourceID;
                        shortname = sourceShortname;
                        amount = sourceAmount;
                        container = sourceContainer;
                        position = sourcePosition;
                    }
                }
            
                public Biome(string defaultPermission = "", bool fillDefaultItem = false)
                {
                    if(fillDefaultItem) items.Add(new ItemConfig(0, "pants", 1, "wear"));
                    permission = defaultPermission;
                }
            }
        
            public static Configuration GetDefault()
            {
                Configuration _config = new Configuration();

                _config.biomes.tundra = new Biome("autokit.tundra", true);
                _config.biomes.dust = new Biome("autokit.dust", true);
                _config.biomes.winter = new Biome("autokit.winter", true);
                _config.biomes.normal = new Biome("autokit.normal", true);

                return _config;
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.GetDefault();
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        
        #endregion

        #region Data Management

        private void LoadData()
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("AutoKit") ?? new StoredData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("AutoKit", _storedData);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        void Unload()
        {
            SaveData();
        }

        #endregion

        #region Хуки

        private void Loaded()
        {
            permission.RegisterPermission(_config.biomes.tundra.permission, this);
            permission.RegisterPermission(_config.biomes.dust.permission, this);
            permission.RegisterPermission(_config.biomes.winter.permission, this);
            permission.RegisterPermission(_config.biomes.normal.permission, this);
            permission.RegisterPermission("autokit.admin", this);
            
            LoadData();
        }

        void Init()
        {
            LoadData();
        }

        private void OnDefaultItemsReceived(PlayerInventory inventory) 
        {
            var player = inventory.containerMain?.GetOwnerPlayer();

            if(player == null) return;

            switch(GetBiome(player.transform.position))
            {
                case "Dust":
                    ReciveItems(player, _config.biomes.dust);
                    break;

                case "Normal":
                    ReciveItems(player, _config.biomes.normal);
                    break;

                case "Tundra":
                    ReciveItems(player, _config.biomes.tundra);
                    break;

                case "Winter":
                    ReciveItems(player, _config.biomes.winter);
                    break;

                case "Undefined":
                    break;
            }
        }

        #endregion

        #region Методы

        [ChatCommand("autokit")]
        private void chatCmd(BasePlayer player, string command, string[] args) 
        {
            if(!permission.UserHasPermission(player.UserIDString, "autokit.admin")) return;

            if(args == null || args?.Length == 0) 
            {
                player.ChatMessage("Использование:\n/autokit [биом] - сохранить текущий набор для биома\n/autokit save [имя] - сохранить личный набор\n/autokit load [имя] - загрузить личный набор");
                return;
            }

            switch(args[0].ToLower())
            {
                case "save":
                    if(args.Length < 2)
                    {
                        player.ChatMessage("Укажите имя набора!");
                        return;
                    }
                    SavePlayerLoadout(player, args[1]);
                    return;

                case "load":
                    if(args.Length < 2)
                    {
                        player.ChatMessage("Укажите имя набора!");
                        return;
                    }
                    LoadPlayerLoadout(player, args[1]);
                    return;

                case "dust":
                    SetupNewItems(player, "dust");
                    return;

                case "tundra":
                    SetupNewItems(player, "tundra");
                    return;
                    
                case "winter":
                    SetupNewItems(player, "winter");
                    return;

                case "normal":
                    SetupNewItems(player, "normal");
                    return;
            }

            player.ChatMessage("Указан неизвестный аргумент!");
        }
        
        private void SavePlayerLoadout(BasePlayer player, string name)
        {
            var items = new List<Configuration.Biome.ItemConfig>();
            SaveItemsFromContainer(player, player.inventory.containerMain, "main", items);
            SaveItemsFromContainer(player, player.inventory.containerBelt, "belt", items);
            SaveItemsFromContainer(player, player.inventory.containerWear, "wear", items);

            var loadout = new SavedLoadout
            {
                Name = name,
                Items = items
            };

            if (!_storedData.PlayerLoadouts.ContainsKey(player.userID))
                _storedData.PlayerLoadouts[player.userID] = new Dictionary<string, List<SavedLoadout>>();

            if (!_storedData.PlayerLoadouts[player.userID].ContainsKey(name))
                _storedData.PlayerLoadouts[player.userID][name] = new List<SavedLoadout>();

            _storedData.PlayerLoadouts[player.userID][name].Add(loadout);
            SaveData();
            player.ChatMessage($"Набор {name} успешно сохранен!");
        }

        private void LoadPlayerLoadout(BasePlayer player, string name)
        {
            if (!_storedData.PlayerLoadouts.ContainsKey(player.userID) || 
                !_storedData.PlayerLoadouts[player.userID].ContainsKey(name) ||
                _storedData.PlayerLoadouts[player.userID][name].Count == 0)
            {
                player.ChatMessage($"Набор {name} не найден!");
                return;
            }

            var loadout = _storedData.PlayerLoadouts[player.userID][name].Last();
            player.inventory.Strip();

            foreach (var itemConfig in loadout.Items)
            {
                CreateAndGiveItem(player, itemConfig);
            }

            player.ChatMessage($"Набор {name} успешно загружен!");
        }
        
        private void SaveItemsFromContainer(BasePlayer player, ItemContainer container, string containerType, List<Configuration.Biome.ItemConfig> items)
        {
            foreach(var itemInventory in container.itemList) 
            {
                var item = new Configuration.Biome.ItemConfig(
                    itemInventory.skin, 
                    itemInventory.info.shortname, 
                    itemInventory.amount, 
                    containerType,
                    itemInventory.position
                );

                if(!string.IsNullOrEmpty(itemInventory.name)) 
                    item.name = itemInventory.name;

                // Сохраняем информацию о патронах
                var heldEntity = itemInventory.GetHeldEntity() as BaseProjectile;
                if (heldEntity != null)
                {
                    item.ammo = heldEntity.primaryMagazine.contents;
                    item.ammoType = heldEntity.primaryMagazine.ammoType.shortname;
                }

                // Сохраняем информацию о модификациях
                if (itemInventory.contents != null)
                {
                    foreach (var mod in itemInventory.contents.itemList)
                    {
                        item.mods[mod.position.ToString()] = mod.info.shortname;
                    }
                }

                items.Add(item);
            }
        }

        private void CreateAndGiveItem(BasePlayer player, Configuration.Biome.ItemConfig itemConfig)
        {
            Item item = ItemManager.CreateByName(itemConfig.shortname, itemConfig.amount, itemConfig.id);
            if (item == null) return;

            if (!string.IsNullOrEmpty(itemConfig.name))
                item.name = itemConfig.name;

            var container = GetContainer(player, itemConfig.container);
            if (container == null) return;

            // Устанавливаем позицию
            item.position = itemConfig.position;

            // Добавляем патроны если это оружие
            var heldEntity = item.GetHeldEntity() as BaseProjectile;
            if (heldEntity != null && !string.IsNullOrEmpty(itemConfig.ammoType))
            {
                heldEntity.primaryMagazine.contents = itemConfig.ammo;
                heldEntity.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemConfig.ammoType);
            }

            // Добавляем модификации
            if (itemConfig.mods.Count > 0)
            {
                foreach (var mod in itemConfig.mods)
                {
                    var modItem = ItemManager.CreateByName(mod.Value);
                    if (modItem != null)
                    {
                        modItem.position = int.Parse(mod.Key);
                        modItem.MoveToContainer(item.contents);
                    }
                }
            }

            item.MoveToContainer(container, itemConfig.position);
        }
        
        private void SetupNewItems(BasePlayer player, string caseStr) 
        {
            Configuration.Biome targetBiome = null;
            switch(caseStr)
            {
                case "dust":
                    targetBiome = _config.biomes.dust;
                    break;
                case "tundra":
                    targetBiome = _config.biomes.tundra;
                    break;
                case "winter":
                    targetBiome = _config.biomes.winter;
                    break;
                case "normal":
                    targetBiome = _config.biomes.normal;
                    break;
            }

            if (targetBiome == null) return;

            targetBiome.items = new List<Configuration.Biome.ItemConfig>();
            SaveItemsFromContainer(player, player.inventory.containerMain, "main", targetBiome.items);
            SaveItemsFromContainer(player, player.inventory.containerBelt, "belt", targetBiome.items);
            SaveItemsFromContainer(player, player.inventory.containerWear, "wear", targetBiome.items);

            NextTick(SaveConfig);
            player.ChatMessage("Автокит был успешно изменен!");
        }

        private void ReciveItems(BasePlayer player, Configuration.Biome biome) 
        {
            if(biome.recive && permission.UserHasPermission(player.UserIDString, biome.permission))
            {
                if(biome.strip) player.inventory.Strip();

                foreach(var itemConf in biome.items) 
                {
                    CreateAndGiveItem(player, itemConf);
                }
            }
        }
        
        private static ItemContainer GetContainer(BasePlayer player, string container) 
        {   
            switch(container) 
            {
                case "wear":
                    return player.inventory.containerWear;

                case "belt":
                    return player.inventory.containerBelt;

                case "main":
                    return player.inventory.containerMain;
            }

            return null;
        }

        private static string GetBiome(Vector3 pos)
        {
            if (TerrainMeta.BiomeMap.GetBiome(pos, 1) > 0.5f ) return "Dust";
            if (TerrainMeta.BiomeMap.GetBiome(pos, 2) > 0.5f ) return "Normal";
            if (TerrainMeta.BiomeMap.GetBiome(pos, 4) > 0.5f ) return "Tundra";
            if (TerrainMeta.BiomeMap.GetBiome(pos, 8) > 0.5f ) return "Winter";
            
            return "Undefined";
        }

        #endregion
    }
}