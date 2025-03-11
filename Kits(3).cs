using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Kits", "unknown", "1.0.0")] 
    public class Kits : RustPlugin
    {
        
        #region Variables

        [PluginReference] private Plugin ImageLibrary = null;


        private const string Layer = "LayerPanelKit";
        
        private const string LayerInventory = "LayerPanelInventory";
        
        public const bool    TranslationRU = false;
        
        private readonly Dictionary<String, Int32> _itemIds = new Dictionary<String, Int32>();

        #endregion
        
        #region Classes

        public class CategoryList
        {
            [JsonProperty(TranslationRU ? "Название категории"     : "Name Category")] 
            public string DisplayName;

            [JsonProperty(TranslationRU ? "Описание категории"     : "Description Category")] 
            public string Content;

            [JsonProperty(TranslationRU ? "Изображение категории"  : "Image Category")]
            public string Image;

            [JsonProperty(TranslationRU ? "Спрятать категорию?" : "Hide category?")]
            public bool Hide = false;

            [JsonProperty(TranslationRU ? "Лист китов"             : "List Kits")] 
            public List<KitList> KitList = new List<KitList>();

            public int Number;
        }


        public class KitList
        {
            [JsonProperty(TranslationRU ? "Название кита"                          : "Name Kits")] 
            public string Name;
            
            [JsonProperty(TranslationRU ? "Дисплейное название кита"               : "DisplayName Kits")] 
            public string DisplayName;

            [JsonProperty(TranslationRU ? "Время перезарядки использование кита"   : "Cooldown")]
            public double Cooldown;

            [JsonProperty(TranslationRU ? "Скрыть кит"                             : "Hide Kit")] 
            public bool Hide;
            
            [JsonProperty(TranslationRU ? "Привилегия"                             : "Permission")]
            public string Permission = "kitsdefault.use";

            [JsonProperty(TranslationRU ? "Предметы, выдываемые в ките"            : "List item")]
            public List<ItemList> _ItemLists = new List<ItemList>();


            public List<ItemList> GiveToItem()
            {
                var itemList = new List<ItemList>();


                if (_ItemLists == null || _ItemLists.Count == 0)
                    return itemList;


                foreach (var itemClass in _ItemLists)
                {
                    if (itemClass.Chance > 0 && UnityEngine.Random.Range(0, 100) < itemClass.Chance)
                    {
                        
                        itemList.Add(itemClass);
                            
                        continue;
                    }
                    
                    
                    itemList.Add(itemClass);
                }



                return itemList;
            }

            public Item BuildItem(string ShortName, int Amount, ulong SkinID, float Condition, int blueprintTarget, Weapon weapon, List<ItemContent> Content)
            {
                Item item = ItemManager.CreateByName(ShortName, Amount, SkinID);
                item.condition = Condition;
                if (blueprintTarget != 0)
                {
                    item.blueprintTarget = blueprintTarget;
                }
                if (weapon != null)
                {
                    var getheld = item.GetHeldEntity() as BaseProjectile;
                    if (getheld != null)
                    {
                        getheld.primaryMagazine.contents = weapon.ammoAmount;
                        getheld.primaryMagazine.ammoType = ItemManager.FindItemDefinition(weapon.ammoType);
                    }
                }
                if (Content != null)
                {
                    foreach (var cont in Content)
                    {
                        Item conts = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                        conts.condition = cont.Condition;
                        conts.MoveToContainer(item.contents);
                    }
                }
                return item;
            }
            
        }

        public class ItemList
        {
            public string             ShortName;
            public int                Amount;
            public ulong              SkinID;
            public int                Blueprint;
            public string             Container;
            public float              Condition;
            public int                Chance;
            public Weapon             Weapon;
            public List<ItemContent>  Content;

        }
        
        
        public class Weapon
        {
            public string ammoType;
            public int    ammoAmount;
        }
        
        public class ItemContent
        {
            public string ShortName;
            public float  Condition;
            public int    Amount;
        }


        public class CooldownKit
        {
			public int    Number;
            public string DisplayName;
            public double Cooldown;
        }


        #endregion

        #region Data
        

        public Dictionary<ulong, List<CooldownKit>> _playerData = new Dictionary<ulong, List<CooldownKit>>();

        public List<CategoryList> _KitList = new List<CategoryList>();


        #endregion

        #region Hooks

        /*private void OnPlayerRespawned(BasePlayer player)
        {
            player.inventory.Strip();
            timer.Once(0.1f, () => 
            {
                player.inventory.Strip();
                player.SendConsoleCommand("UIKitHandler autokit");
            });
            return;
        }*/

        void OnServerInitialized()
        {

            LoadData();
            
            LoadMessages();

            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();
                
                foreach (var listCategory in _KitList)
                {
                    var list = listCategory.KitList.Where(p => !string.IsNullOrEmpty(p.Permission));
                    
                    
                    foreach (var key in list)
                    {
                        if (!permission.PermissionExists(key.Permission, this))
                            permission.RegisterPermission(key.Permission, this);
                    }
                    
                    
                    if (!string.IsNullOrEmpty(listCategory.Image) && !imagesList.ContainsKey(listCategory.Image))
                    {
                        imagesList.Add(listCategory.Image, listCategory.Image);
                    }
                    
                    
                   
                    
                }

                ImageLibrary.Call("AddImage", "https://i.imgur.com/BQx3bap.png", "IconChance");
                
                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            
            
        }


        void OnServerSave() => timer.Once(UnityEngine.Random.Range(3, 5), SaveData);

        void Unload() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerCooldown", _playerData);


        void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
                _playerData.Add(player.userID, new List<CooldownKit>());
        }

        #endregion
        
        #region Functional
        

        public List<CategoryList> GetKitsForPlayer(BasePlayer player)
        {
            List<CategoryList> newList = new List<CategoryList>();


            foreach (var key in _KitList)
            {
                var find = key.KitList.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Permission) && p.Hide == false);
                if (find != null && !newList.Contains(key))
                    newList.Add(key);
            }


            return newList ?? new List<CategoryList>();

        }
        
        public string FormatShortTime(BasePlayer player, TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{GetLang("UI_Days", player.UserIDString, time.Days)} ";

            if (time.Hours != 0)
                result += $"{GetLang("UI_Hours", player.UserIDString, time.Hours)} ";

            if (time.Minutes != 0)
                result += $"{GetLang("UI_Minutes", player.UserIDString, time.Minutes)} ";

            if (time.Seconds != 0)
                result += $"{GetLang("UI_Seconds", player.UserIDString, time.Seconds)} ";

            return result;
        }
        

        #region GetItem

        
        private List<ItemList> GetItemPlayer(BasePlayer player)
        {
            var kititems = Pool.GetList<ItemList>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemAddToKit(item, "wear");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemAddToKit(item, "main");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemAddToKit(item, "belt");
                    kititems.Add(iteminfo);
                }
            }
            
            
            return kititems;
            
        }
        
        private ItemList ItemAddToKit(Item item, string container)
        {
            ItemList kitem = new ItemList();
            kitem.Amount = item.amount;
            kitem.Container = container;
            kitem.SkinID = item.skin;
            kitem.Blueprint = item.blueprintTarget;
            kitem.ShortName = item.info.shortname;
            kitem.Condition = item.condition;
            kitem.Weapon = null;
            kitem.Content = null;
            if (item.info.category == ItemCategory.Weapon)
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    kitem.Weapon = new Weapon();
                    kitem.Weapon.ammoType = weapon.primaryMagazine.ammoType.shortname;
                    kitem.Weapon.ammoAmount = weapon.primaryMagazine.contents;
                }
            }
            if (item.contents != null)
            {
                kitem.Content = new List<ItemContent>();
                foreach (var cont in item.contents.itemList)
                {
                    kitem.Content.Add(new ItemContent()
                        {
                            Amount = cont.amount,
                            Condition = cont.condition,
                            ShortName = cont.info.shortname
                        }
                    );
                }
            }
            return kitem;
        }
        

        #endregion
        
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }


        public void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerCooldown", _playerData);
            
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/KitList", _KitList);
        }

        public void LoadData()
        {
            try
            {
                _playerData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<CooldownKit>>>($"{Name}/PlayerCooldown");
                
                _KitList = Interface.GetMod().DataFileSystem.ReadObject<List<CategoryList>>($"{Name}/KitList");
            }
            catch
            {
                _playerData = new Dictionary<ulong, List<CooldownKit>>();

                _KitList = new List<CategoryList>();
            }
        }
        
		private Int32 FindItemID(String shortName)
		{
			Int32 val;
			if (_itemIds.TryGetValue(shortName, out val))
				return val;

			ItemDefinition definition = ItemManager.FindItemDefinition(shortName);
			if (definition == null) return 0;

			val = definition.itemid;
			_itemIds[shortName] = val;
			return val;
		}
        #endregion
        
        #region ChatCommand

        [ChatCommand("kit")]
        void KitFunctional(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                MainUI(player);
                return;
            }

            string param = args[0];

            switch (param)
            {
                case "create":
                {
                    if (!player.IsAdmin) return;

                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetLang("ErrorCreateCategory", player.UserIDString));
                        return;
                    }
                    

                    string paramName = string.Join(" ", args.Skip(1));;

                    if (string.IsNullOrEmpty(paramName))
                    {
                        player.ChatMessage(GetLang("ErrorCreateCategory", player.UserIDString));
                        return;
                    }


                    int Number = UnityEngine.Random.Range(0000, 9999);
                    
                    _KitList.Add(new CategoryList
                    {
                        DisplayName = paramName,
                        Content = TranslationRU ? "Описание категории" : "Description category",
                        KitList = new List<KitList>(),
                        Number = Number,
                        Image = "https://i.imgur.com/PVx4Ktj.png"
                    });
                    
                    player.ChatMessage(GetLang("CreateCategory", player.UserIDString, paramName, Number));
                    
                    
                    SaveData();

                    break;
                }
                case "add":
                {
                    if (!player.IsAdmin) return;
                    
                    if (args.Length < 3)
                    {
                        
                        player.ChatMessage(GetLang("ErrorCreateKit", player.UserIDString));
                        return;
                    }

                    int paramNumber = int.Parse(args[1]);

                    var find = _KitList.FirstOrDefault(p => p.Number == paramNumber);
                    if (find == null)
                    {
                        player.ChatMessage(GetLang("ErrorNotFoundCategory", player.UserIDString));
                        return;
                    }

                    if (find.KitList.Count >= 5)
                    {
                        player.ChatMessage(GetLang("MaxCountKitCategory", player.UserIDString));
                        return;
                    }
                    
                    
                    
                    string paramKit = string.Join(" ", args.Skip(2));
                    
                    
                    var listItem = GetItemPlayer(player);
                    
                    
                    find.KitList.Add(new KitList
                    {
                        Name = paramKit,
                        DisplayName = paramKit,
                        Hide = false,
                        _ItemLists = listItem,
                        Cooldown = 3600
                    });
                    

                    player.ChatMessage(GetLang("CreateKit", player.UserIDString, paramKit, paramNumber));
                    
                    SaveData();

                    break;
                }
                case "cremove":
                {
                    if (!player.IsAdmin) return;

                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetLang("ErrorRemoveCategory", player.UserIDString));
                        return;
                    }

                    int paramNumber = int.Parse(args[1]);

                    var find = _KitList.FirstOrDefault(p => p.Number == paramNumber);
                    if (find == null)
                    {
                        player.ChatMessage(GetLang("ErrorNotFoundCategory", player.UserIDString));
                        return;
                    }

                    _KitList.Remove(find);
                    
                    
                    player.ChatMessage(GetLang("RemoveCategory", player.UserIDString));
                    
                    SaveData();
                    
                    break;
                }

                case "kremove":
                {
                    if (!player.IsAdmin) return;
                    
                    if (args.Length < 3)
                    {
                        player.ChatMessage(GetLang("ErrorRemoveKit", player.UserIDString));
                        return;
                    }

                    int paramNumber = int.Parse(args[1]);;

                    string paramKit = string.Join(" ", args.Skip(2));
                    
                    var find = _KitList.FirstOrDefault(p => p.Number == paramNumber);
                    if (find == null)
                    {
                        player.ChatMessage(GetLang("ErrorNotFoundCategory", player.UserIDString));
                        return;
                    }

                    var findKit = find.KitList.FirstOrDefault(p => p.Name == paramKit);
                    if (findKit == null)
                    {
                        player.ChatMessage(GetLang("ErrorNotFoundKit", player.UserIDString));
                        return;
                    }

                    find.KitList.Remove(findKit);
                    
                    player.ChatMessage(GetLang("RemoveKit", player.UserIDString));

                    break;
                }
            }


        }
        

        #endregion

        #region ConsoleCommand
        
        
        [ConsoleCommand("UIKitHandler")]
        void CmdConsoleMain(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();


            if (args.HasArgs())
            {
                switch (args.Args[0])
                {
                    case "viewKits":
                    {
                        var findElement = _KitList.FirstOrDefault(p => p.Number == int.Parse(args.Args[1]));
                        if (findElement == null) return;

                        var findKit = findElement.KitList.FirstOrDefault(p => p.Name == args.Args[2]);
                        if (findKit == null) return;
                    
                        ViewKitInventory(player, findElement, findKit);
                        
                        break;
                    }

                    case "autokit":
                        var findAutoKit = _KitList.FirstOrDefault(p => p.KitList.FirstOrDefault(x => x.Name.Contains("autokit")) != null).KitList.FirstOrDefault(y => y.Name.Contains("autokit"));
                        if (findAutoKit == null) return;

                        int beltcount2 = findAutoKit._ItemLists.Where(i => i.Container == "belt").Count();

                        int wearcount2 = findAutoKit._ItemLists.Where(i => i.Container == "wear").Count();

                        int maincount2 = findAutoKit._ItemLists.Where(i => i.Container == "main").Count();



                        int totalcount2 = beltcount2 + wearcount2 + maincount2;
                        if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount2 || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount2 || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount2) if (totalcount2 > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                            {
                                return;
                            }


                        var itemList2 = Pool.GetList<ItemList>();

                        itemList2 = findAutoKit.GiveToItem();


                        var iventory2 = player.inventory;

                        foreach (var itemClass in itemList2)
                        {
                            var itemToGive = findAutoKit.BuildItem(
                                itemClass.ShortName,
                                itemClass.Amount,
                                itemClass.SkinID,
                                itemClass.Condition,
                                itemClass.Blueprint,
                                itemClass.Weapon,
                                itemClass.Content);


                            if (itemToGive == null) return;

                            var container = itemClass.Container == "belt" ? player.inventory.containerBelt : itemClass.Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain;


                            var moved = itemToGive.MoveToContainer(container) || itemToGive.MoveToContainer(iventory2.containerMain);
                            if (!moved)
                            {
                                if (container == iventory2.containerBelt) moved = itemToGive.MoveToContainer(iventory2.containerWear);
                                if (container == iventory2.containerWear) moved = itemToGive.MoveToContainer(iventory2.containerBelt);
                            }

                            if (!moved) itemToGive.Drop(player.GetCenter(), player.GetDropVelocity());
                        }

                        Pool.FreeList(ref itemList2);

                        break;

                    case "giveKit":
                    {
                        var findElement = _KitList.FirstOrDefault(p => p.Number == int.Parse(args.Args[1]));
                        if (findElement == null) return;

                        var findKit = findElement.KitList.FirstOrDefault(p => p.Name == args.Args[2]);
                        if (findKit == null) return;

                        var findCooldown = _playerData[player.userID].FirstOrDefault(p => p.DisplayName == findKit.Name && p.Number == findElement.Number);

                        
                        
                        
                        
                        if (findCooldown != null)
                        {
                            if (findCooldown.Cooldown - Facepunch.Math.Epoch.Current > 0)
                            {
                                Translate.Phrase Cooldown  = new Translate.Phrase("player.cooldown", GetLang("Cooldown", player.UserIDString));
                                
                                player.ShowToast(GameTip.Styles.Red_Normal, Cooldown);
                                return;
                            }

                            _playerData[player.userID].Remove(findCooldown);
                        }
                        



                        int beltcount = findKit._ItemLists.Where(i => i.Container == "belt").Count();
                        
                        int wearcount = findKit._ItemLists.Where(i => i.Container == "wear").Count();
                        
                        int maincount = findKit._ItemLists.Where(i => i.Container == "main").Count();
                        
                        
                        
                        int totalcount = beltcount + wearcount + maincount;
                        if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount) if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                        {
                            Translate.Phrase NoPlace   = new Translate.Phrase("player.inventory.noplace", GetLang("NoPlace", player.UserIDString));
                            
                            player.ShowToast(GameTip.Styles.Red_Normal, NoPlace);
                            return;
                        }


                        var itemList = Pool.GetList<ItemList>();
                        
                        itemList = findKit.GiveToItem();
                        
                        
                        var iventory = player.inventory;

                        foreach (var itemClass in itemList)
                        {
                            var itemToGive = findKit.BuildItem(
                                itemClass.ShortName,
                                itemClass.Amount,
                                itemClass.SkinID,
                                itemClass.Condition,
                                itemClass.Blueprint,
                                itemClass.Weapon,
                                itemClass.Content);
                            
                            
                            if (itemToGive == null) return;
                            
                            var container = itemClass.Container == "belt" ? player.inventory.containerBelt : itemClass.Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain;

                            
                            var moved = itemToGive.MoveToContainer(container) || itemToGive.MoveToContainer(iventory.containerMain);
                            if (!moved)
                            {
                                if (container == iventory.containerBelt) moved = itemToGive.MoveToContainer(iventory.containerWear);
                                if (container == iventory.containerWear) moved = itemToGive.MoveToContainer(iventory.containerBelt);
                            }

                            if (!moved) itemToGive.Drop(player.GetCenter(), player.GetDropVelocity());
                        }
                        
                        _playerData[player.userID].Add(new CooldownKit
                        {
                            DisplayName = findKit.Name,
                            Cooldown    = Facepunch.Math.Epoch.Current + findKit.Cooldown,
							Number      = findElement.Number
                        });
                        
                        CuiHelper.DestroyUi(player, Layer);

                        CuiHelper.DestroyUi(player, LayerInventory);
                        
                        
                        Translate.Phrase Accept = new Translate.Phrase("player.give.kit", GetLang("Accept", player.UserIDString));

						Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, new Vector3(), new Vector3());
						EffectNetwork.Send(effect, player.Connection);

						player.ShowToast(GameTip.Styles.Blue_Normal, Accept);
                        
                        Pool.FreeList(ref itemList);
                        
                        break;
                    }
                }
            }
        }

        #endregion
        
        #region Lang

        public static StringBuilder sb = new StringBuilder();
        
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    ["NoPlace"]               = "ВЫ НЕ МОЖЕТЕ ПОЛУЧИТЬ ЭТОТ КИТ!\n<size=12><color=#E4DAD138>НЕ ХВАТАЕТ МЕСТА</color></size>",
                    ["Cooldown"]              = "ВЫ НЕ МОЖЕТЕ ПОЛУЧИТЬ ЭТОТ КИТ!\n<size=12><color=#E4DAD138>У ВАС ПРОИСХОДИТ ОТКАТ КИТА</color></size>",
                    ["Accept"]                = "ВЫ УСПЕШНО ПОЛУЧИЛИ НАБОР",
                    ["ErrorCreateCategory"]   = "Ошибка при создании категории! Попробуйте: /kit create <categoryName>",
                    ["CreateCategory"]        = "Вы успешно создали категорию {0}.\nЗа данной категории закреплен уникальный ключ: {1}.\nПо данному ключу вы сможете добавлять наборы в эту категорию!",
                    ["ErrorCreateKit"]        = "Ошибка при добавлении набора в категорию! Попробуйте: /kit add <number> <kitName>",
                    ["ErrorNotFoundCategory"] = "Ошибка!\nДанный ключ не привязан ни к 1 категории!",
                    ["MaxCountKitCategory"]   = "В данной категории максимальное количество наборов!",
                    ["CreateKit"]             = "Вы успешно создали набор {0} в категории с ключом {1}!",
                    ["ErrorRemoveCategory"]   = "Ошибка при удалении категории! Попробуйте: /kit cremove <categoryNumber>\nПрошу так же учесть тот факт, что при удалении категории - удаляются наборы, которые в нем находятся!",
                    ["ErrorRemoveKit"]        = "Ошибка при удалении набора из категорию! Попробуйте: /kit kremove <categoryNumber> <kitName>",
                    ["RemoveCategory"]        = "Вы успешно удалили категорию!",
                    ["ErrorNotFoundKit"]      = "Ошибка! \nДанный набор не был найден в категории!",
                    ["RemoveKit"]             = "Вы успешно удалили кит с категории!",
                    ["UI_Close"]              = "Закрыть",
                    ["UI_Cooldown"]           = "откат",
                    ["UI_InfoChance"]         = "<b>Внимание!</b> Предметы с процентом выпадают только с определенным шансом",
                    ["UI_Days"]               = "{0}д",
                    ["UI_Hours"]              = "{0}ч",
                    ["UI_Minutes"]            = "{0}м",
                    ["UI_Seconds"]            = "{0}c"
                    
                }
                , this, "ru");
            
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    ["NoPlace"]               = "YOU CAN'T GET THIS WHALE!\n<size=12><color=#E4DAD138>NOT ENOUGH SPACE</color></size>",
                    ["Cooldown"]              = "YOU CAN'T GET THIS WHALE!\n<size=12><color=#E4DAD138>YOU HAVE A WHALE ROLLBACK</color></size>",
                    ["Accept"]                = "YOU HAVE SUCCESSFULLY RECEIVED A SET",
                    ["ErrorCreateCategory"]   = "Error when creating a category! Try: /kit create <CategoryName>",
                    ["CreateCategory"]        = "You have successfully created the category {0}.A unique key is assigned to this category: {1}.\n With this key, you will be able to add sets to this category!",
                    ["ErrorCreateKit"]        = "Error when adding a set to a category! Try: /kit add <number> <kitName>",
                    ["ErrorNotFoundCategory"] = "Error!\n he given key is not tied to any category!",
                    ["MaxCountKitCategory"]   = "There is a maximum number of sets in this category!",
                    ["CreateKit"]             = "You have successfully created a set {0} in the category with the key {1}!",
                    ["ErrorRemoveCategory"]   = "Error deleting the category! Try: /kit cremove <categoryNumber>\n I also ask you to take into account the fact that when deleting a category, the sets that are in it are deleted!",
                    ["ErrorRemoveKit"]        = "Error when deleting a set from a category! Try: /kit kremove <categoryNumber> <kitName>",
                    ["RemoveCategory"]        = "You have successfully deleted the category!",
                    ["ErrorNotFoundKit"]      = "Error! \n The given set was not found in the category!",
                    ["RemoveKit"]             = "You have successfully removed the whale from the category!",
                    ["UI_Close"]              = "Close",
                    ["UI_Cooldown"]           = "cooldown",
                    ["UI_InfoChance"]         = "<b>Attention!</b> Items with a percentage drop out only with a certain chance",
                    ["UI_Days"]               = "{0}d",
                    ["UI_Hours"]              = "{0}h",
                    ["UI_Minutes"]            = "{0}m",
                    ["UI_Seconds"]            = "{0}s"
                    
                }
                , this);
        }

        #endregion
        
        #region UI

        public void ViewKitInventory(BasePlayer player, CategoryList categoryList, KitList kitList)
        {
            CuiHelper.DestroyUi(player, LayerInventory);
            
            var container = new CuiElementContainer();
			
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image         = {Color     = "0 0 0 0.9"}
            }, "Overlay", LayerInventory);
            
			
            
            container.Add(new CuiButton
            {
                RectTransform  = {AnchorMin = "0 0", AnchorMax = "1 1" },
                Button         = {Color     = "0 0 0 0.23", Material  = "assets/content/ui/uibackgroundblur.mat"},
                Text           = {Text = ""}
            }, LayerInventory);
           
            
            container.Add(new CuiButton
            {
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button        = {Sprite  = "assets/content/ui/ui.background.transparent.radial.psd", Color     = HexToCuiColor("#000000", 100)},
                Text          = {Text = ""}
            }, LayerInventory);
            
            
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360"},
                Image         = {Color     = "0 0 0 0"}
            }, LayerInventory, LayerInventory + "MainPanel");
			
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button        = {Color     = "0 0 0 0",  Close = LayerInventory},
                Text = { Text = "" }
            },  LayerInventory + "MainPanel");
            
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.6088542 0.7879629", AnchorMax = $"0.6567701 0.8268518"},
                Button        = {Close     =  LayerInventory, Material = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#e0947a", 10),},
                Text          = {Text      = GetLang("UI_Close", player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#e0947a") }
            }, LayerInventory + "MainPanel");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.3432292 0.7879629", AnchorMax = $"0.6036459 0.8268518"},
                Button        = {Sprite    = "assets/content/ui/ui.background.tile.psd", Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                Text = { Text = "" }
            }, LayerInventory + "MainPanel");
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = $"{categoryList.DisplayName} / <b><color=#E4DAD1>{kitList.DisplayName}</color></b>", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E4DAD1", 50)},
                RectTransform = {AnchorMin = $"0.3505208 0.7879629", AnchorMax = $"0.4864584 0.8268518" },
            }, LayerInventory + "MainPanel");
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = $"{GetLang("UI_Cooldown", player.UserIDString)} {FormatShortTime(player, TimeSpan.FromSeconds(kitList.Cooldown))}", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#E4DAD1", 50)},
                RectTransform = {AnchorMin = $"0.4864584 0.7879629", AnchorMax = $"0.5979151 0.8268518" },
            }, LayerInventory + "MainPanel");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.3432292 0.1731481", AnchorMax = $"0.6567708 0.2120459"},
                Button        = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#bfa377", 10),},
                Text          = {Text      = GetLang("UI_InfoChance", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bfa377") }
            }, LayerInventory + "MainPanel");


            var allMain = kitList._ItemLists.FindAll(p => p.Container == "main");


            for (int i = 0; i < 24; i++)
            {
                
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{0.3432292 + i * 0.0529 - Math.Floor((double)i / 6) * 6 * 0.0529} {0.6935185 - Math.Floor((double) i / 6) * 0.094}", 
                                     AnchorMax = $"{0.3911458 + i * 0.0529 - Math.Floor((double)i / 6) * 6 * 0.0529} {0.7787051 - Math.Floor((double) i / 6) * 0.094}",},
                    Image         = {Color     = "0 0 0 0"},
                }, LayerInventory + "MainPanel", LayerInventory + $"{i}.Main");
                
                if (allMain.Count <= i)
                {

                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Main",
                        Components =
                        {
                            new CuiImageComponent         {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 5),},
                            new CuiRectTransformComponent {AnchorMin = $"0 0", AnchorMax = $"1 1"}
                        }
                    });   
                        
                    continue;
                }
                
                var element = allMain[i];

                if (element.Chance > 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Main", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", "IconChance"), Color = HexToCuiColor("#E4DAD1", 10), },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" },
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Main",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(element.ShortName), SkinId = element.SkinID },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });
                    /*container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Main", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", element.ShortName)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });*/
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = $"0 0.7499871", AnchorMax = $"0.4130608 1"},
                        Button        = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#bfa377", 10),},
                        Text          = {Text      = $"{element.Chance}%", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bfa377") }
                    }, LayerInventory + $"{i}.Main");
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 1"},
                        Image         = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                    }, LayerInventory + $"{i}.Main");
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Main",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(element.ShortName), SkinId = element.SkinID },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });
                    /*container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Main", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", element.ShortName)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });*/
                }
                
                container.Add(new CuiLabel
                {
                    Text          = {Text      = $"x{element.Amount}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#E4DAD1", 50)},
                    RectTransform = {AnchorMin = $"0 0.01086909", AnchorMax = $"0.9239309 0.2934731" },
                }, LayerInventory + $"{i}.Main");
            }
            
            
            var allWear = kitList._ItemLists.FindAll(p => p.Container == "wear");
            
            
            for (int i = 0; i < 7; i++)
            {
                
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{0.3166671 + i * 0.0529 - Math.Floor((double)i / 7) * 7 * 0.0529} 0.315746", 
                                     AnchorMax = $"{0.3645837 + i * 0.0529 - Math.Floor((double)i / 7) * 7 * 0.0529} 0.4009353",},
                    Image         = {Color     = "0 0 0 0"},
                }, LayerInventory + "MainPanel", LayerInventory + $"{i}.Wear");
                
                if (allWear.Count <= i)
                {

                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Wear",
                        Components =
                        {
                            new CuiImageComponent         {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 5),},
                            new CuiRectTransformComponent {AnchorMin = $"0 0", AnchorMax = $"1 1"}
                        }
                    });   
                        
                    continue;
                }
                
                
                var element = allWear[i];

                if (element.Chance > 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Wear", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", "IconChance"), Color = HexToCuiColor("#E4DAD1", 10), },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" },
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Wear",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(element.ShortName), SkinId = element.SkinID },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });
                    /*container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Wear", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", element.ShortName)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });*/
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = $"0 0.7499871", AnchorMax = $"0.4130608 1"},
                        Button        = {Close     =  LayerInventory, Material = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#bfa377", 10),},
                        Text          = {Text      = $"{element.Chance}%", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bfa377") }
                    }, LayerInventory + $"{i}.Wear");
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 1"},
                        Image         = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                    }, LayerInventory + $"{i}.Wear");
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Wear",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(element.ShortName), SkinId = element.SkinID },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });
                    /*container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Wear", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", element.ShortName + 512)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });*/
                }
                
                container.Add(new CuiLabel
                {
                    Text          = {Text      = $"x{element.Amount}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#E4DAD1", 50)},
                    RectTransform = {AnchorMin = $"0 0.01086909", AnchorMax = $"0.9239309 0.2934731" },
                }, LayerInventory + $"{i}.Wear");
            }
            
            var allBelt = kitList._ItemLists.FindAll(p => p.Container == "belt");
            
            for (int i = 0; i < 6; i++)
            {
                
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{0.3432292 + i * 0.0529 - Math.Floor((double)i / 7) * 7 * 0.0529} 0.2213013", 
                        AnchorMax =              $"{0.3911458 + i * 0.0529 - Math.Floor((double)i / 7) * 7 * 0.0529} 0.3064906",},
                    Image         = {Color     = "0 0 0 0"},
                }, LayerInventory + "MainPanel", LayerInventory + $"{i}.Belt");
                
                if (allBelt.Count <= i)
                {

                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Belt",
                        Components =
                        {
                            new CuiImageComponent         {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 5),},
                            new CuiRectTransformComponent {AnchorMin = $"0 0", AnchorMax = $"1 1"}
                        }
                    });   
                        
                    continue;
                }


                var element = allBelt[i];

                if (element.Chance > 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Belt", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", "IconChance"), Color = HexToCuiColor("#E4DAD1", 10), },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" },
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Belt",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(element.ShortName), SkinId = element.SkinID },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });
                    /*container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Belt", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", element.ShortName)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });*/
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = $"0 0.7499871", AnchorMax = $"0.4130608 1"},
                        Button        = {Close     =  LayerInventory, Material = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#bfa377", 10),},
                        Text          = {Text      = $"{element.Chance}%", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bfa377") }
                    }, LayerInventory + $"{i}.Belt");
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 1"},
                        Image         = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                    }, LayerInventory + $"{i}.Belt");
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Belt",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(element.ShortName), SkinId = element.SkinID },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });
                    /*container.Add(new CuiElement
                    {
                        Parent = LayerInventory + $"{i}.Belt", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", element.ShortName)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });*/
                }
                
                container.Add(new CuiLabel
                {
                    Text          = {Text      = $"x{element.Amount}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#E4DAD1", 50)},
                    RectTransform = {AnchorMin = $"0 0.01086909", AnchorMax = $"0.9239309 0.2934731" },
                }, LayerInventory + $"{i}.Belt");
            }

            CuiHelper.AddUi(player, container);
        }


        public void MainUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            
            var container = new CuiElementContainer();
			
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image         = {Color     = "0 0 0 0.9"}
            }, "Overlay", Layer);
            
			
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                Button         = {Color     = "0 0 0 0.23", Material  = "assets/content/ui/uibackgroundblur.mat"},
                Text = { Text = "" }
            }, Layer);
           
            
            container.Add(new CuiButton
            {
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button         = {Sprite  = "assets/content/ui/ui.background.transparent.radial.psd", Color     = HexToCuiColor("#363636", 100)},
                Text = { Text = "" }
            }, Layer);
			
			
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0 1", AnchorMax = $"0 1", OffsetMin = "7 -35", OffsetMax = "35 -7" },
                Button        = {Close = Layer, Material = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#e0947a", 10),},
                Text = { Text = "" }
            }, Layer, "PanelClose");
                        
                        
            container.Add(new CuiElement
            {
                Parent = $"PanelClose", 
                Components =
                { 
                new CuiImageComponent         {Sprite    = "assets/icons/close.png", Color = HexToCuiColor("#e0947a", 100)},
                new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "-6 -6", OffsetMin = "6 6" },
                }
            });

            
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360"},
                Image         = {Color     = "0 0 0 0"}
            }, Layer, Layer + "MainPanel");
			
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button        = {Color     = "0 0 0 0",  Close = Layer},
                Text = { Text = "" }
            },  Layer + "MainPanel");
			
			


            CuiHelper.AddUi(player, container);
            
            ViewKitList(player);
        }


        public void ViewKitList(BasePlayer player)
        {
            var firstContainer = new CuiElementContainer();


            var list = GetKitsForPlayer(player);
            
            for (int i = 0; i < _KitList.Count; i++)
            {
                CuiHelper.DestroyUi(player, Layer + i.ToString() + ".Kit");
            }
            


            float width      = 0.1709f;
            float height     = 0.204f;
            float margin     = 0.008f;
            float minHeight  = 0.4060f;
            
            int index        = 1;
            int String       = 1;
            
            int stringAmount = Mathf.CeilToInt(list.Count / 4f); 
            
            float position  = 0.506f - list.Skip((String - 1) * 3).Take(4).Count() / 2f * width - (list.Skip((String - 1) * 4).Take(4).Count()) / 2f * margin;
            
            if (stringAmount > 1)
            {
                minHeight += stringAmount / 2f * width - stringAmount / 2f * margin;   
            }


            for (int i = 0; i < list.Count; i++)
            {
                var element = list[i];
                
                firstContainer.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{position} {minHeight}", AnchorMax = $"{position + width} {minHeight + height}"},
                    Image         = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#F1ECE6", 0),},
                }, Layer + "MainPanel", Layer + i.ToString() + ".Kit");
                
                
                firstContainer.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"0 0.6448084", AnchorMax = $"1 1"},
                    Image         = {Sprite    = "assets/content/ui/ui.background.tile.psd", Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                }, Layer + i.ToString() + ".Kit");
                
                firstContainer.Add(new CuiLabel
                {
                    Text          = {Text      = element.DisplayName, Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E4DAD1")},
                    RectTransform = {AnchorMin = $"0.06060863 0.7999997", AnchorMax = $"0.5696962 0.9106383" },
                }, Layer + i.ToString() + ".Kit");
                
                firstContainer.Add(new CuiLabel
                {
                    Text          = {Text      = element.Content, Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E4DAD1", 50)},
                    RectTransform = {AnchorMin = $"0.06060863 0.6680849", AnchorMax = $"0.5818173 0.8085112" },
                }, Layer + i.ToString() + ".Kit");

                if (!string.IsNullOrEmpty(element.Image))
                {
                    firstContainer.Add(new CuiElement
                    {
                        Parent = Layer + i.ToString() + ".Kit", 
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", element.Image), Color = "1 1 1 0.7" },
                            new CuiRectTransformComponent {AnchorMin = "0.5484841 0.6448084", AnchorMax = "0.954542 1" },
                        }
                    }); 
                }


                var listElement = element.KitList.Where(p => p.Hide == false && permission.UserHasPermission(player.UserIDString, p.Permission) && p.DisplayName.ToLower() != "autokit").ToList();
                
                
                for (int j = 0; j < 5; j++)
                {
                    
                    firstContainer.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"0 {0.4229077 - Math.Floor((double) j / 1) * 0.222}", AnchorMax = $"0.995 {0.6211476 - Math.Floor((double) j / 1) * 0.222}"},
                        Image         = {Color     = "0 0 0 0"},
                    }, Layer + i.ToString() + ".Kit", Layer + $"{j}.U");
                    
                    if (listElement.Count <= j)
                    {

                        //firstContainer.Add(new CuiElement
                        //{
                        //    Parent = Layer + $"{j}.U",
                        //    Components =
                        //    {
                        //        new CuiImageComponent         {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 5),},
                        //        new CuiRectTransformComponent {AnchorMin = $"0 0", AnchorMax = $"1 1"}
                        //    }
                        //});   
                        
                        continue;
                    }

                    var elementKitList = listElement[j];

                    var findCooldown = _playerData[player.userID]
                        .FirstOrDefault(p => p.DisplayName == elementKitList.Name && p.Number == element.Number);

                    if (findCooldown != null)
                    {
                        if (findCooldown.Cooldown - Facepunch.Math.Epoch.Current <= 0)
                        {
                            _playerData[player.userID].Remove(findCooldown);
                            
                            findCooldown = null;
                        }
                    }

                    if (findCooldown != null)
                    {
                        firstContainer.Add(new CuiPanel
                        {
                            RectTransform = {AnchorMin = $"0 0", AnchorMax = $"0.8545457 1"},
                            Image         = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                        }, Layer + $"{j}.U");
                        
                        firstContainer.Add(new CuiLabel
                        {
                            Text          = {Text      = FormatShortTime(player, TimeSpan.FromSeconds(findCooldown.Cooldown - Facepunch.Math.Epoch.Current)), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#E4DAD1", 50)},
                            RectTransform = {AnchorMin = $"0.490909 0", AnchorMax = $"0.8545457 1" },
                        }, Layer + $"{j}.U");
                    }
                    else
                    {
                        firstContainer.Add(new CuiPanel
                        {
                            RectTransform = {AnchorMin = $"0 0", AnchorMax = $"0.7090911 1"},
                            Image         = {Material  = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                        }, Layer + $"{j}.U");
                    }


                    if (findCooldown == null)
                    {
                        firstContainer.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = $"0.7181818 0", AnchorMax = $"0.8545457 1"},
                            Button        = {Command = $"UIKitHandler giveKit {element.Number} {elementKitList.Name}", Material = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#ccd68a", 10),},
                            Text = { Text = "" }
                        }, Layer + $"{j}.U",  $"{j}.U1");
                        
                        
                        firstContainer.Add(new CuiElement
                        {
                            Parent = $"{j}.U1", 
                            Components =
                            {
                                new CuiImageComponent         {Sprite    = "assets/icons/check.png", Color = HexToCuiColor("#ccd68a", 100)},
                                new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "-6 -6", OffsetMin = "6 6" },
                            }
                        });
                    }
                    


                    firstContainer.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = $"0.8636362 0", AnchorMax = $"1 1"},
                        Button        = {Command = $"UIKitHandler viewKits {element.Number} {elementKitList.Name}", Sprite    = "assets/content/ui/ui.background.tile.psd", Material = "assets/content/ui/uibackgroundblur.mat", Color = HexToCuiColor("#E4DAD1", 10),},
                        Text = { Text = "" }
                    }, Layer + $"{j}.U",  $"{j}.U2");
                    
						
                    firstContainer.Add(new CuiElement
                    {
                        Parent = $"{j}.U2", 
                        Components =
                        {
                            new CuiImageComponent         {Sprite    = "assets/icons/info.png", Color = HexToCuiColor("#E4DAD1", 50)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "-6 -6", OffsetMin = "6 6" },
                        }
                    });
                        
                    firstContainer.Add(new CuiLabel
                    {
                        Text          = {Text      = elementKitList.DisplayName, Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E4DAD1")},
                        RectTransform = {AnchorMin = $"0.03333616 0", AnchorMax = $"0.6969679 1" },
                    }, Layer + $"{j}.U");
                }


                position += width + margin;

                if (index % 4 == 0)
                {
                    String++;
                    index      = 0;
                    minHeight -= 0.2225f;
                    position   = 0.506f - list.Skip((String - 1) * 4).Take(4).Count() / 2f * width - (list.Skip((String - 1) * 4).Take(4).Count()) / 2f * margin;
                }

                index++;
                
            }
			

            CuiHelper.AddUi(player, firstContainer);
        }
        
        
        #endregion
    }
}