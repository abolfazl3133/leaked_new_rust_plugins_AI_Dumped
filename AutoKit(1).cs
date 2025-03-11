using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins 
{
    [Info("AutoKit", "BANTIK AKA CURSOR", "1.0.5")]
    public class AutoKit : RustPlugin 
    {
        #region Конфигурация

        private Configuration _config;

        public class Configuration 
        {
            [JsonProperty("Разрешение для использования")] 
            public string permission = "autokit.use";

            [JsonProperty("Выдавать набор при спавне?")] 
            public bool recive = true;

            [JsonProperty("Очищать инвентарь перед выдачей?")]
            public bool strip = true;

            [JsonProperty("Предметы к выдаче")] 
            public List<ItemConfig> items = new List<ItemConfig>();

            public class ItemConfig
            {
                [JsonProperty("Отображаемое название")] 
                public string name = "";

                [JsonProperty("Скин ID предмета")] 
                public ulong id = 0;

                [JsonProperty("Shortname предмета")] 
                public string shortname = "";

                [JsonProperty("Количество")] 
                public int amount = 1;

                [JsonProperty("В какой контейнер помещать? (wear, main, belt)")] 
                public string container = "main";

                public ItemConfig(ulong sourceID = 0, string sourceShortname = "", int sourceAmount = 0, string sourceContainer = "main")
                {
                    id = sourceID;
                    shortname = sourceShortname;
                    amount = sourceAmount;
                    container = sourceContainer;
                }
            }
        
            public static Configuration GetDefault()
            {
                return new Configuration();
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

        #region Хуки

        private void Loaded()
        {
            permission.RegisterPermission(_config.permission, this);
            permission.RegisterPermission("autokit.admin", this);
            
            cmd.AddChatCommand("autokit", this, nameof(cmdAutoKit));
        }

        private void OnDefaultItemsReceived(PlayerInventory inventory) 
        {
            var player = inventory.containerMain?.GetOwnerPlayer();
            if(player == null) return;
            
            ReciveItems(player);
        }

        #endregion

        #region Методы

        private void cmdAutoKit(BasePlayer player, string command, string[] args)
        {
            if(!permission.UserHasPermission(player.UserIDString, "autokit.admin"))
            {
                player.ChatMessage("У вас нет прав на использование этой команды!");
                return;
            }

            if(args == null || args.Length == 0)
            {
                ShowHelp(player);
                return;
            }

            switch(args[0].ToLower())
            {
                case "save":
                    SetupNewItems(player);
                    break;

                case "clear":
                    _config.items.Clear();
                    SaveConfig();
                    player.ChatMessage("Автокит очищен!");
                    break;

                case "list":
                    if(_config.items.Count == 0)
                    {
                        player.ChatMessage("Автокит пуст!");
                        return;
                    }

                    player.ChatMessage("Список предметов в автоките:");
                    for(int i = 0; i < _config.items.Count; i++)
                    {
                        var itemConf = _config.items[i];
                        player.ChatMessage($"{i + 1}. {itemConf.shortname} x{itemConf.amount} ({itemConf.container})" + 
                            (itemConf.id > 0 ? $" [Skin: {itemConf.id}]" : "") +
                            (!string.IsNullOrEmpty(itemConf.name) ? $" \"{itemConf.name}\"" : ""));
                    }
                    break;

                default:
                    ShowHelp(player);
                    break;
            }
        }

        private void ShowHelp(BasePlayer player)
        {
            player.ChatMessage("Автокит - Список команд:");
            player.ChatMessage("/autokit save - Сохранить текущий инвентарь как автокит");
            player.ChatMessage("/autokit list - Показать список предметов");
            player.ChatMessage("/autokit clear - Очистить автокит");
        }
        
        private void SetupNewItems(BasePlayer player) 
        {
            _config.items = new List<Configuration.ItemConfig>();
                
            foreach(var itemInventory in player.inventory.containerMain.itemList) 
            {
                Configuration.ItemConfig item = new Configuration.ItemConfig(itemInventory.skin, itemInventory.info.shortname, itemInventory.amount, "main");
                if(!string.IsNullOrEmpty(itemInventory.name)) item.name = itemInventory.name;
                _config.items.Add(item);
            }

            foreach(var itemInventory in player.inventory.containerBelt.itemList) 
            {
                Configuration.ItemConfig item = new Configuration.ItemConfig(itemInventory.skin, itemInventory.info.shortname, itemInventory.amount, "belt");
                if(!string.IsNullOrEmpty(itemInventory.name)) item.name = itemInventory.name;
                _config.items.Add(item);
            }

            foreach(var itemInventory in player.inventory.containerWear.itemList) 
            {
                Configuration.ItemConfig item = new Configuration.ItemConfig(itemInventory.skin, itemInventory.info.shortname, itemInventory.amount, "wear");
                if(!string.IsNullOrEmpty(itemInventory.name)) item.name = itemInventory.name;
                _config.items.Add(item);
            }

            SaveConfig();
            player.ChatMessage("Автокит был успешно изменен!");
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
        
        private void ReciveItems(BasePlayer player) 
        {
            if(_config.recive && permission.UserHasPermission(player.UserIDString, _config.permission))
            {
                if(_config.strip) player.inventory.Strip();

                foreach(var itemConf in _config.items) 
                {
                    Item item = ItemManager.CreateByName(itemConf.shortname, itemConf.amount, itemConf.id);
                    if(!string.IsNullOrEmpty(itemConf.name)) item.name = itemConf.name;
                    item.MoveToContainer(GetContainer(player, itemConf.container));
                }
            }
        }

        #endregion
    }
}