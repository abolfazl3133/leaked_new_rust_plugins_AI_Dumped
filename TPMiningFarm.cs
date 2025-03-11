using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("TPMiningFarm", "https://topplugin.ru", "11.0.1")]
    class TPMiningFarm : RustPlugin
    {   
        [PluginReference]
        private Plugin ImageLibrary, TPEconomic;

        /*
            сделать текст парящий
        */
        public static TPMiningFarm _;

        public class ElectricBatteryHelper : FacepunchBehaviour
        {
            public ElectricBattery battery;
            public Mailbox mailbox;
            public IOEntity split;

            private float checkInterval = 1f; // Интервал проверки в секундах
            private float timer = 0f; // Таймер для отслеживания интервала

            public void Awake()
            {
                battery = GetComponent<ElectricBattery>();  
            }
            
            public void AddChildMailBox(Mailbox box)
            {
                mailbox = box;
            }

            public void AddChildSplit(IOEntity a)
            {
                split = a;
            }
            
            public void Destroy()
            {
                if(mailbox != null)
                {
                    StorageContainer[] containers = mailbox.GetComponentsInChildren<StorageContainer>();
                    foreach (StorageContainer container in containers) {
                        container.DropItems();
                    }
                    _.NextTick(() => { 

                        battery.RemoveChild(mailbox);
                        mailbox.Kill(); 
                        battery.SendNetworkUpdateImmediate(); 
                    });

                     
                }

              /*  ElectricBatteryHelper batteryHelper = battery.gameObject.GetComponent<ElectricBatteryHelper>();
                if (batteryHelper != null) {
              
                        DestroyImmediate(batteryHelper); 
                        battery.SendNetworkUpdateImmediate();
                
                    
                }*//*
                foreach (var a in battery.children) {
                        _.PrintWarning($"{a.ShortPrefabName} 12-");
                    }*/
            }

            public void Update()
            {   
                timer += Time.deltaTime;

                if (timer >= checkInterval)
                {
                    timer = 0f;

                    var components = _.GetComponents(split);
               

                    if(components == null && mailbox != null)
                    {
                        Destroy();
                    }

                    if(components!=null && mailbox !=null)
                        CheckBatteryEnergy();
                }

            }

            public void CheckBatteryEnergy()
            {
                if (battery != null)
                {
                    float energy = battery.rustWattSeconds;

                    if(energy >= configData.Fermsettings.watt) {
                        battery.rustWattSeconds =0;
                        DropMoney();
                    }
                }
            }

            private void DropMoney()
            {
                var config = configData.Fermsettings;
                var itemContainer = mailbox.inventory;

                int amm=0;
                int much=config.amount;

                foreach(var a in itemContainer.itemList)
                    if(a != null) amm = a.amount;
                
                if(amm >= config.maxCoins)
                    return;

                if(amm+config.amount > config.maxCoins)
                    much = config.maxCoins-amm;

                Item item = ItemManager.CreateByItemID(config.id, much, config.SkinID);
                item.name = $"{config.Name}";

                mailbox.inventory.GiveItem(item);
                mailbox.SendNetworkUpdate();
            }
            
        }

        void OnItemUpgrade(Item item, Item upgraded, BasePlayer player)
        {
            if(item.info.itemid == configData.Fermsettings.id && item.skin == configData.Fermsettings.SkinID)
            {
                TPEconomic.Call("API_PUT_BALANCE_PLUS", player.userID, (float) 10*configData.Mon);

                upgraded.Remove();
            }
        }

      /*  object OnItemAction(Item item, string action, BasePlayer player)
        {
            if(action == "unwrap" && item.info.shortname == "xmas.present.small" && item.skin == configData.Fermsettings.SkinID)
            {
                return false;
            }
            return null;
        }*/

     /*   void OnItemUpgrade(Item item, Item upgraded, BasePlayer player)
        {
            if(item.info.shortname == "xmas.present.small" && item.skin == configData.Fermsettings.SkinID)
            {
                Item item1 = ItemManager.CreateByItemID(configData.Fermsettings.id, item.amount, configData.Fermsettings.SkinID);
                item1.name = $"{configData.Fermsettings.Name}";
                player.inventory.GiveItem(item1);
                upgraded.Remove();
            }
        }*/

      /*  bool playerGiveMoneyItem(BasePlayer player, Item item)
        {
            bool a = CheckDataPlayer(player.userID);
            if(!a)
            {
                int ammo = item.amount;
                int much = ammo;
                int balance = dataPlayer[player.userID].balance;
                PrintWarning($"{balance} | ${ammo}");
                if(balance >= configData.playerMoneyLimit) return false;
                if(balance+ammo > configData.playerMoneyLimit) 
                {
                    int ost = (balance+ammo)-configData.playerMoneyLimit;
                    much = ammo-ost;

                    item.Remove();
                    Item item1 = ItemManager.CreateByItemID(configData.Fermsettings.id, ost, configData.Fermsettings.SkinID);
                    item1.name = $"{configData.Fermsettings.Name}";
                    player.inventory.GiveItem(item1);

                }
                dataPlayer[player.userID].balance += much;
            }
            return false;
        }*/

      /*  bool playerGiveMoney(ulong userID, int much)
        {
            bool a = CheckDataPlayer(userID);
            if(!a)
            {
                dataPlayer[userID].balance -= much;
                return true;
            }
            return false;
        }

        bool playerRemoveMoney(ulong userID, int much)
        {
            bool a = CheckDataPlayer(userID);
            if(!a)
            {
                dataPlayer[userID].balance -= much;
                return true;
            }
            return false;
        }*/

        void Unload()
        {
            foreach(var a in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(a, "Main.");
            }

            ElectricBattery[] batteries = GameObject.FindObjectsOfType<ElectricBattery>();

            data.batt.Clear();
            foreach (var batt in batteries){
                if(batt != null && CheckMailBox(batt))
                {
                    ElectricBatteryHelper batteryHelpers = batt.GetComponent<ElectricBatteryHelper>();
                    if(batteryHelpers != null)
                    {
                        if(!data.batt.ContainsKey(batt.OwnerID))
                            data.batt.Add(batt.OwnerID, new List<int>());
                        data.batt[batt.OwnerID].Add(batt.GetInstanceID());
                        batteryHelpers.Destroy();
                    }
                }
            }
            SaveData();
        }

        bool CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            ElectricBattery batt = entity.GetComponent<ElectricBattery>();
            if(batt != null && CheckMailBox(batt))
            {
                ElectricBatteryHelper batteryHelpers = batt.GetComponent<ElectricBatteryHelper>();
                if(batteryHelpers != null)
                {
                    data.batt[batt.OwnerID].Remove(batt.GetInstanceID());
                }
            }
            return true;
        }

        void OnServerInitialized()
        {   
            ItemDefinition item = ItemManager.FindItemDefinition(configData.Fermsettings.id);
            item.stackable = 1000;

            foreach(var a in configData.LimitPermission)
                permission.RegisterPermission(a.Key, this);

            ImageLibrary?.Call("AddImage", "https://gspics.org/images/2024/07/02/0zC0Nx.png", "background");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/NJlZl9e.png", "info");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/ZR8Jx1U.png", "placeholder");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/lD5VZQn.png", "placeholder-");
            ImageLibrary?.Call("AddImage", "https://i.ibb.co/bg0QbfG/Po-up-3.png", "window");
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/7cMZNtg.png", "btn");

            /*foreach(var a in BasePlayer.activePlayerList)
                OnPlayerConnected(a);*/
        }

       /* void OnPlayerConnected(BasePlayer player)
        {
            CheckDataPlayer(player.userID);
        }

        bool CheckDataPlayer(ulong userID)
        {
            if(userID == null) return false;
            if(!dataPlayer.ContainsKey(userID))
            {
                dataPlayer.Add(userID, new DATA { balance=0 });
                return true;
            }
            return false;
        }*/

        void Loaded()
        {
            ElectricBattery[] batteryHelpers = GameObject.FindObjectsOfType<ElectricBattery>();
            foreach (var batt in batteryHelpers){
                if (data.batt.ContainsKey(batt.OwnerID))
                {
                    if(data.batt[batt.OwnerID].Contains(batt.GetInstanceID()))
                    {
                        SpawnMailBox(batt, batt.inputs[0].connectedTo.Get(true));
                    }
                }
            }
        }

        int GetLimitFarm(string userID)
        {
            int limit = 0;
            foreach (var num in configData.LimitPermission)
            {
                if (permission.UserHasPermission(userID, num.Key))
                    if (num.Value > limit) limit = num.Value;
            }
            return limit;
        }

        static bool AreListsEqual<T>(List<T> list1, List<T> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            list1.Sort();
            list2.Sort();

            return list1.SequenceEqual(list2);
        }

        List<string> shortnameComp = new List<string>{"electric.flasherlight.deployed", "smallrechargablebattery.deployed", "rfbroadcaster"};
        public Dictionary<string, IOEntity> GetComponents(IOEntity entity)
        {
            Dictionary<string, IOEntity> connectedComponents = new Dictionary<string, IOEntity>();
            List<string> connectedComponentsShortPrefabName = new List<string>();

            foreach (var slot in entity.outputs)
            {

                if (slot.connectedTo != null)
                {
                    var connectedObject = slot.connectedTo.Get(true);

                    if (connectedObject != null)
                    {
                        connectedComponents.Add($"{connectedObject.ShortPrefabName}", connectedObject);
                        connectedComponentsShortPrefabName.Add(connectedObject.ShortPrefabName);
                    }
                }
            }
          
            if(!AreListsEqual(shortnameComp, connectedComponentsShortPrefabName))
                return null;

            return connectedComponents;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if(entity.ShortPrefabName == "smallrechargablebattery.deployed")
            {
                if(data.batt.ContainsKey(entity.OwnerID))
                {
                    var ent = entity.GetComponent<BaseEntity>();
                    if(CheckMailBox(entity.GetComponent<IOEntity>()))
                    {
                        if(data.batt[ent.OwnerID].Contains(entity.GetInstanceID()))
                        {
                            data.batt[ent.OwnerID].Remove(entity.GetInstanceID());
                            SaveData();
                        }
                    }
                }
            }
        }

        void DestroyMeshCollider(BaseEntity ent) {
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>()) {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        void DestroyGroundComp(BaseEntity ent) {
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
        }

        List<ulong> playerNotifed = new List<ulong>();
        private void SpawnMailBox(IOEntity entity, IOEntity split)
        {

            if(CheckMailBox(entity)) return;
            Vector3 worldHandlePosition = entity.transform.TransformPoint(entity.outputs[0].handlePosition);
            Mailbox mailbox = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", entity.transform.position, entity.transform.rotation * Quaternion.Euler(0f, -90f, 0f)) as Mailbox;
            if(mailbox == null) return;

            var ownerID = entity.GetComponent<BaseEntity>().OwnerID;
            BasePlayer player = BasePlayer.FindByID(ownerID);

            int limits = GetLimitFarm((player.userID).ToString());

            if (data.batt.ContainsKey(ownerID))
            {
                if(data.batt[ownerID].Count() >= limits && !data.batt[ownerID].Contains(entity.GetInstanceID()))
                {
                    player.ChatMessage(lang.GetMessage("FARM_LIMIT", this, player.UserIDString).Replace("{0}", $"{limits}"));
                    return;
                }

            }

            if(!data.batt.ContainsKey(ownerID))
                data.batt.Add(ownerID, new List<int>());

            if(!data.batt[ownerID].Contains(entity.GetInstanceID()))
            {
                data.batt[ownerID].Add(entity.GetInstanceID());
                player.ChatMessage(lang.GetMessage("FARM_CREATED", this, player.UserIDString).Replace("{0}", $"{data.batt[ownerID].Count()}").Replace("{1}", $"{limits}"));
            }
            else
            {
                if(!playerNotifed.Contains(ownerID))
                {
                    playerNotifed.Add(ownerID);
                    player.ChatMessage(lang.GetMessage("FARM_ACTIVED", this, player.UserIDString).Replace("{0}", $"{data.batt[ownerID].Count()}").Replace("{1}", $"{limits}"));
                }
            }
            DestroyMeshCollider(mailbox);
            DestroyGroundComp(mailbox);
            mailbox.allowedItems= new ItemDefinition[0];
            mailbox.allowedItems.Append(ItemManager.FindItemDefinition(configData.Fermsettings.id));
            entity.AddChild(mailbox);
            mailbox.skinID = 2436472234;
            mailbox.Spawn();
            mailbox.SendNetworkUpdateImmediate();

            var electricBatteryHelper = entity.gameObject.AddComponent<ElectricBatteryHelper>();
            electricBatteryHelper.AddChildSplit(split);
            electricBatteryHelper.AddChildMailBox(mailbox);

            entity.SendNetworkUpdateImmediate();
        }

        private bool CheckMailBox(IOEntity entity)
        {
            if(entity.HasChild(entity))
            {
                var child = entity.children;
                foreach(var a in child)
                {
                    if(a.ShortPrefabName == "mailbox.deployed")
                        return true;
                } 
            }
            return false;
        }

        object OnOutputUpdate(IOEntity entity)
        {
            var ownerID = entity.GetComponent<BaseEntity>().OwnerID;
            BasePlayer player = BasePlayer.FindByID(ownerID);
            var RFBroad = entity.GetComponent<RFBroadcaster>();

            if (player == null || ownerID == 0 || entity.ShortPrefabName != "rfbroadcaster") return null;
            if (RFBroad.frequency != configData.Fermsettings.hzn || entity.GetConnectedInputCount() < 1) return null;
            if (entity.inputs[0].connectedTo.Get(true).ShortPrefabName != "splitter") return null;
            var split = RFBroad.inputs[0].connectedTo.Get(true);
            
            var components = GetComponents(split);
            if(components == null) return null;

            SpawnMailBox(components["smallrechargablebattery.deployed"], split);

            return null;
        }

       
        void Init()
        {
            _ = this;
            LoadVariables();
            LoadData();
        }

       /* public void OpenWindow(BasePlayer player, int rev = -1)
        {
            if(rev==-1) rev = configData.conclusion.why;
            CuiHelper.DestroyUi(player, MenuContent+".Window");

            var container = new CuiElementContainer();  
           if(dataPlayer[player.userID].balance > 0)
                GameStoresBalanceSet(player.userID, dataPlayer[player.userID].balance, 5);
            container.Add(new CuiElement
            {
                Name = MenuContent + ".Window",
                Parent = MenuContent,
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "pcikgksd"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-180 -65", OffsetMax = "180 107" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9409847 0.8806619", AnchorMax = "0.987 0.9759417"},
                Button = { Close=MenuContent+".Window", Color = "0 0 0 0" },
            }, MenuContent + ".Window");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.023 0.886953", AnchorMax = "0.8299285 0.9721064" },
                Text = { Text = $"Обмен монеток", Color = "1 1 1 0.65",FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, MenuContent + ".Window");
            string em ="";
            container.Add(new CuiElement
            {
                Parent = MenuContent + ".Window",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = em,
                        CharsLimit = 5,
                        Color = "1 1 1 0.8",
                        Command = $"TPMiningFarm_h349ugpresbnj3 test {em}",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.423 0.886953", AnchorMax = "0.8299285 0.9721064"
                    }
                }
            });
           container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.423 0.686953", AnchorMax = "0.8299285 0.7721064"},
                Button = { Command=$"TPMiningFarm_h349ugpresbnj3 test {em}", Color = "1 1 1 1" },
            }, MenuContent + ".Window");
            CuiHelper.AddUi(player, container);
        }

        void Submit(BasePlayer player, int mon)
        {
            CuiHelper.DestroyUi(player, MenuContent+".Window");

            var container = new CuiElementContainer();  

            container.Add(new CuiElement
            {
                Name = MenuContent + ".Window",
                Parent = MenuContent,
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "pcikgksd"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-180 -65", OffsetMax = "180 107" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9409847 0.8806619", AnchorMax = "0.987 0.9759417"},
                Button = { Close=MenuContent+".Window", Color = "0 0 0 0" },
            }, MenuContent + ".Window");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.023 0.886953", AnchorMax = "0.8299285 0.9721064" },
                Text = { Text = $"Подтверждение", Color = "1 1 1 0.65",FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, MenuContent + ".Window");

            container.Add(new CuiElement
            {
                Name = MenuContent + ".Window",
                Parent = MenuContent,
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "btn"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-180 -65", OffsetMax = "180 107" },
                }
            });

            CuiHelper.AddUi(player, container);
        }*/

   /*     [ConsoleCommand("MiningFarm_h349ugpresbnj3")]
        void cmdCommandWindow(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                switch (args.Args[0])
                {
                    case "openWindow":
                    {
                        OpenWindow(player);
                        break;
                    }
                    case "test":
                    {
                        Submit(player, int.Parse(args.Args[1]));
                        break;
                    }
                }
            }
        }*/

        public string MenuContent = "TPMiningFarm.Content";

        //[ChatCommand("mf")]
        private void chatMiningFarm(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MenuContent);
            var container = new CuiElementContainer();  

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, ".Mains", MenuContent);

            container.Add(new CuiElement
            {
                Name = MenuContent+"lay",
                Parent = MenuContent,
                Components ={
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage", "background"),},
                    new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-592 -333", OffsetMax = "581 335"}}
            });

            container.Add(new CuiButton
            {
                Button ={
                    Close = "Menu_UI",
                    Color = "1 1 1 0",
                },
                RectTransform ={
                    AnchorMin = "0.801 0.805",
                    AnchorMax = "0.817 0.832",
                }
            }, MenuContent+"lay");

         /*   container.Add(new CuiButton{
                Button ={
                    Command = "TPMiningFarm_h349ugpresbnj3 openWindow",
                    Color = "0 0 0 0"
                },
                Text ={
                    Text = lang.GetMessage("INFO_BRIGHTOUT", this, player.UserIDString),
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.9",
                    Font = "robotocondensed-regular.ttf"
                },
                RectTransform ={
                    AnchorMin = "0.39 0.563",
                    AnchorMax = "0.459 0.597",
                }
            }, MenuContent+"lay");*/

            container.Add(new CuiButton{
                Button ={
                    Command = $"playerVideoTPMiningFarm {configData.urlviedo}",
                    Color = "1 1 1 0"
                },
                Text ={
                    Text = lang.GetMessage("INFO_VIDEOMANUAL", this, player.UserIDString),
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.9",
                    Font = "robotocondensed-regular.ttf"
                },
                RectTransform ={
                    AnchorMin = "0.292 0.563",
                    AnchorMax = "0.384 0.597",
                }
            }, MenuContent+"lay");

            /*container.Add(new CuiElement{
                Parent = MenuContent+"lay",
                Components ={
                    new CuiTextComponent{Text = lang.GetMessage("INFO_LABEL", this, player.UserIDString),FontSize = 12,Align = TextAnchor.MiddleCenter,Color = "1 1 1 0.8",Font = "robotocondensed-regular.ttf"},
                    new CuiRectTransformComponent{AnchorMin = "0.239 0.65",AnchorMax = "0.456 0.675",},
                }
            });*/

            container.Add(new CuiElement{
                Parent = MenuContent+"lay",
                Components ={
                    new CuiTextComponent{
                        Text = lang.GetMessage("INFO_LEFT", this, player.UserIDString),
                        FontSize = 12,
                        Align = TextAnchor.UpperCenter,
                        Color = "1 1 1 0.9",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent{
                        AnchorMin = "0.215 0.32",
                        AnchorMax = "0.456 0.545",
                    },
                }
            });

            container.Add(new CuiElement{
                Parent = MenuContent+"lay",
                Components ={
                    new CuiTextComponent{
                        Text = lang.GetMessage("INFO_RIGHT_UP", this, player.UserIDString),
                        FontSize = 11,
                        Align = TextAnchor.UpperCenter,
                        Color = "1 1 1 0.9",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent{
                        AnchorMin = "0.48 0.67",
                        AnchorMax = "0.8 0.77",
                    },
                }
            });

            container.Add(new CuiElement{
                Parent = MenuContent+"lay",
                Components ={
                    new CuiTextComponent{
                        Text =  lang.GetMessage("INFO_RIGHT_DOWN", this, player.UserIDString),
                        FontSize = 11,
                        Align = TextAnchor.UpperCenter,
                        Color = "1 1 1 0.9",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent{
                        AnchorMin = "0.48 0.19",
                        AnchorMax = "0.8 0.35",
                    },
                }
            });

            /*int placeAm = dataPlayer[player.userID].balance;
            
            container.Add(new CuiPanel{
                Image ={
                    Color = "0 0 0 0",
                },
                RectTransform ={
                    AnchorMin = "0.239 0.606",
                    AnchorMax = "0.456 0.644",
                },
            }, MenuContent+"lay", MenuContent+"lay"+"placeholder");

           
            float aMax = 0.998f;
            container.Add(new CuiPanel{
                Image ={
                    Color = "0.255 0.143 0.162",
                },
                RectTransform ={
                    AnchorMin = "0.015 0.2",
                    AnchorMax = $"{(aMax/configData.playerMoneyLimit)*placeAm} 0.8",
                },
            }, MenuContent+"lay"+"placeholder", "plc");

            container.Add(new CuiElement{
                Parent = "plc",
                Components ={
                    new CuiRawImageComponent{
                        Png = (string) ImageLibrary.Call("GetImage", "placeholder-"),
                    },
                    new CuiRectTransformComponent{
                        AnchorMin = "0.974 0",
                        AnchorMax = "1 0.97",
                    }
                }
            });

            container.Add(new CuiElement{
                Parent = MenuContent+"lay"+"placeholder",
                Components ={
                    new CuiRawImageComponent{
                        Png = (string) ImageLibrary.Call("GetImage", "placeholder"),
                    },
                    new CuiRectTransformComponent{
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });

            container.Add(new CuiElement{
                Parent = MenuContent+"lay"+"placeholder",
                Components ={
                    new CuiTextComponent{
                        Text = $"{placeAm}",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 0.7",
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent{
                        AnchorMin = "0.05 0",
                        AnchorMax = "1 1",
                    },
                }
            });*/

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("playerVideoTPMiningFarm")]
        void paltdsag(ConsoleSystem.Arg arg)
        {
            BasePlayer baseplayer = arg.Player();
            if(baseplayer == null) return;
            string @string = arg.GetString(0, "");
            baseplayer.Command("client.playvideo", new object[]
            {
            @string
            });
        }

        #region Configuration

        private static ConfigData configData;

        private class ConfigData
        {
            [JsonProperty("Настройка Фермы")]
            public FermSet Fermsettings;

            [JsonProperty("Ограничение по пермишенсу")]
            public Dictionary<string, int> LimitPermission = new Dictionary<string, int>();

            [JsonProperty("Ссылка на видеоинструкцию")]
            public string urlviedo;

            [JsonProperty("Сколько стоит одна монетка в TPEconomic")]
            public int Mon;
           /* [JsonProperty("Максимальное кол-во монеток у игрока на балансе")]
            public int playerMoneyLimit;*/

           /* [JsonProperty("Метод обмена монеток")]
            public Сonclusion conclusion;*/

          /*  public class Сonclusion
            {
                [JsonProperty("Куда выводить монетки (гейстор - 0, экономика - 1, два варианта сразу - 2)")]
                public int why;

                [JsonProperty("Настройки гейстора")]
                public GameStore gamestores;

                [JsonProperty("Настройки экономики")]
                public Economics economics;

                public class GameStore
                {
                    [JsonProperty("Сколько рублей стоит одна монетка?")]
                    public int how;
                    [JsonProperty("API Магазина(GameStores)")]
                    public string GameStoresAPIStore;
                    [JsonProperty("ID Магазина(GameStores)")]
                    public string GameStoresIDStore;
                    [JsonProperty("Сообщение в магазин при выдаче баланса(GameStores)")]
                    public string GameStoresMessage;
                }

                public class Economics
                {
                    [JsonProperty("Сколько экономики стоит одна монетка?")]
                    public int how;
                }
            }*/

            public class FermSet
            {
                [JsonProperty("id предмета который будет использоваться для монетки")]
                public int id;
                [JsonProperty("Частота которую игроки должны вписать")]
                public int hzn;
                [JsonProperty("Название монетки")]
                public string Name;
                [JsonProperty("SkinID монетки")]
                public ulong SkinID;
                [JsonProperty("Падающее кол-во")]
                public int amount;
                [JsonProperty("Значение заряда аккумулятора для выдачи монетки")]
                public int watt;
                [JsonProperty("Максимальное кол-во монеток, которое может вместиться в одну ферму")]
                public int maxCoins;
            }
        }

        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
               /* conclusion = new ConfigData.Сonclusion()
                {
                    why = 2,
                    gamestores = new ConfigData.Сonclusion.GameStore()
                    {
                        GameStoresAPIStore = "",
                        GameStoresIDStore = "",
                        GameStoresMessage = "Успешный обмен",
                        how = 10,
                    },
                    economics = new ConfigData.Сonclusion.Economics()
                    {
                        how = 10,
                    },
                },*/
                Fermsettings = new ConfigData.FermSet()
                {
                    id = -126305173,
                    hzn = 202,
                    Name = "123",
                    SkinID = 642482233,
                    amount = 1,
                    watt = 100,
                    maxCoins =1000
                },

                LimitPermission = new Dictionary<string, int>()
                {
                    {"miningfarm.default",1},
                    {"miningfarm.vip",2}
                },

                urlviedo="https://github.com/pirojok95/tp/raw/main/RUST_2024.02.24_-_02.50.01.02%20(1).mp4",
                //playerMoneyLimit=100,
            };
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        #endregion

        #region Data
        
        private static MiningData data = new MiningData();
        //private static Dictionary<ulong, DATA> dataPlayer;
        
        private class MiningData
        {
            public Dictionary<ulong, List<int>> batt = new Dictionary<ulong, List<int>>();     
        }

       
        private class DATA
        {
            [JsonProperty("баланс")]
            public int balance;
        }
        
        private void LoadData() 
        {
            data = Interface.GetMod().DataFileSystem.ReadObject<MiningData>("MiningData");

            //dataPlayer = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, DATA>>("MiningDataPlayer");
            
            if (data == null)
                data = new MiningData();

            //if (dataPlayer == null)
               //dataPlayer = new Dictionary<ulong, DATA>();
        }
        
        private void SaveData(){ 
            Interface.GetMod().DataFileSystem.WriteObject("MiningData", data);   
            //Interface.GetMod().DataFileSystem.WriteObject("MiningDataPlayer", dataPlayer);   
        }    
        private void OnServerSave() => SaveData();
        #endregion      

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FARM_CREATED"] = "Поздравляем! Ферма успешно собрана.\nОжидайте монетки в почтовом ящике\nАктивных ферм {0}/{1}",
                ["FARM_ACTIVED"] = "Фермы успешно загружены.\nОжидайте монетки в почтовом ящике\nАктивных ферм {0}/{1}",
                ["FARM_LIMIT"] = "Вы достигли лимита ферм - {0} шт.",

                ["MENU_HEADER"] = "Майнинг ферма",
                ["MENU_UP"] = "Для сборки Фермы необходимо: устройство для выработки энергии (Малый генератор, Солнечная панель или Ветрогенератор)\nРазветвитель, Мигалка, Аккумулятор, Радиопередатчик. (Схему подключения смотрите на картинке).",
                ["MENU_DOWN"] = "Если электро цепь была собрана правильно, сразу после подачи питания на Аккумулятор,\nвозле него появится Почтовый ящик, в который и будут майниться ваши Гранды.\nСуперигра Как только заряд в Аккумуляторе накопит энергии 150 т (полный заряд), в Почтовом ящике появится 1 Гранд в обмен на\nэнергию в Аккумуляторе, и так по кругу.\n«6 минут заряда аккумулятора = 1 Гранду.\nБлок Гранды с инвентаря можно перевести на Электронный кошелёк, накопив 10 Грандов и нажав на них кнопку Улучшить.",

                //["INFO_BRIGHTOUT"] = "Вывести",
                ["INFO_VIDEOMANUAL"] = "Видеоинструкция",

                //["INFO_LABEL"] = "Баланс",
                ["INFO_LEFT"] = "\n\nМайните монеты - покупайте предметы",
                ["INFO_RIGHT_UP"] = "Ну тут короче майнинг ферма и тд\n\nДля сборки Фермы необходимо: устройство для выработки энергии (Малый генератор, Солнечная панель или Ветрогенератор)\nРазветвитель, Мигалка, Аккумулятор, Радиопередатчик. (Схему подключения смотрите на картинке).\nПосле того как всё было собрано и подключено, задайте на Радиопередатчике частоту 1001 и нажмите Применить.",
                ["INFO_RIGHT_DOWN"] = "После того как всё было собрано и подключено, задайте на Радиопередатчике частоту 1001 и нажмите Применить.\nЕсли электро цепь была собрана правильно, сразу после подачи питания на Аккумулятор,\nвозле него появится Почтовый ящик, в который и будут майниться ваши Гранды.\nСуперигра Как только заряд в Аккумуляторе накопит энергии 150 т (полный заряд), в Почтовом ящике появится 1 Гранд в обмен на\nэнергию в Аккумуляторе, и так по кругу.\n«6 минут заряда аккумулятора = 1 Гранду.\nБлок Гранды с инвентаря можно перевести на Электронный кошелёк, накопив 10 Грандов и нажав на них кнопку Улучшить.",
                            
               /* ["CREDITED"] = "Монеты успешно зачислены на баланс",
                ["NO_CREDITED"] = "Достигнут лимит баланса",*/

               /* ["CHAT_STORE_SUCCESS"] = "Вы успешно обменяли валюту : {0} монет(/ы) и получили {1} рублей на баланс магазина",
                ["CHAT_NO_AUTH_STORE"] = "Вы не аваторизованы в магазине",*/
/*                ["FARM_TEXT_HEADER"] = "ФЕРМА ГРАНДОВ",
                ["FARM_TEXT_LABEL"] = "Ожидайте в этом ящике ваши гранды.",*/
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FARM_CREATED"] = "Поздравляем! Ферма успешно собрана.\nОжидайте монетки в почтовом ящике\nАктивных ферм {0}/{1}",
                ["FARM_ACTIVED"] = "Фермы успешно загружены.\nОжидайте монетки в почтовом ящике\nАктивных ферм {0}/{1}",
                ["FARM_LIMIT"] = "Вы достигли лимита ферм - {0} шт.",

                ["MENU_HEADER"] = "Майнинг ферма",
                ["MENU_UP"] = "Для сборки Фермы необходимо: устройство для выработки энергии (Малый генератор, Солнечная панель или Ветрогенератор)\nРазветвитель, Мигалка, Аккумулятор, Радиопередатчик. (Схему подключения смотрите на картинке).",
                ["MENU_DOWN"] = "Если электроцепь была собрана правильно, сразу после подачи питания на Аккумулятор,\nвозле него появится Почтовый ящик, в который и будут майниться ваши Гранды.\nСуперигра Как только заряд в Аккумуляторе накопит энергии 150 т (полный заряд), в Почтовом ящике появится 1 Гранд в обмен на\nэнергию в Аккумуляторе, и так по кругу.\n«6 минут заряда аккумулятора = 1 Гранду.\nБлок Гранды с инвентаря можно перевести на Электронный кошелёк, накопив 10 Грандов и нажав на них кнопку Улучшить.",

                //["INFO_BRIGHTOUT"] = "Вывести",
                ["INFO_VIDEOMANUAL"] = "Video instruction",

                //["INFO_LABEL"] = "Balance",
                ["INFO_LEFT"] = "\n\nМайните монеты - покупайте предметы",
                ["INFO_RIGHT_UP"] = "Ну тут короче майнинг ферма и тд\n\nДля сборки Фермы необходимо: устройство для выработки энергии (Малый генератор, Солнечная панель или Ветрогенератор)\nРазветвитель, Мигалка, Аккумулятор, Радиопередатчик. (Схему подключения смотрите на картинке).\nПосле того как всё было собрано и подключено, задайте на Радиопередатчике частоту 1001 и нажмите Применить.",
                ["INFO_RIGHT_DOWN"] = "После того как всё было собрано и подключено, задайте на Радиопередатчике частоту 1001 и нажмите Применить.\nЕсли электроцепь была собрана правильно, сразу после подачи питания на Аккумулятор,\nвозле него появится Почтовый ящик, в который и будут майниться ваши Гранды.\nСуперигра Как только заряд в Аккумуляторе накопит энергии 150 т (полный заряд), в Почтовом ящике появится 1 Гранд в обмен на\nэнергию в Аккумуляторе, и так по кругу.\n«6 минут заряда аккумулятора = 1 Гранду.\nБлок Гранды с инвентаря можно перевести на Электронный кошелёк, накопив 10 Грандов и нажав на них кнопку Улучшить.",
                
                /*["CREDITED"] = "Coins successfully credited to your balance",
                ["NO_CREDITED"] = "Balance limit reached",*/

                /*["CHAT_STORE_SUCCESS"] = "You succes transfers : {0} coins and received {1} balance in the shop",
                ["CHAT_NO_AUTH_STORE"] = "You no auth stores",*/
            }, this, "en");
            PrintWarning("Языковой файл загружен успешно");
        }
      /*  public class GameStoresConfiguration
        {

            [JsonProperty("Настройки API плагина")]
            public API APISettings;
            public class API
            {

                [JsonProperty("Секретный ключ (не распространяйте его)")]
                public string SecretKey;
                [JsonProperty("ИД магазина в сервисе")]
                public string ShopID;
            }
        }*/
        /*public void GameStoresBalanceSet(ulong userID, double Balance, int MoneyTake = 0)
        {
            var GameStores = configData.conclusion.gamestores;
            if (string.IsNullOrEmpty(GameStores.GameStoresAPIStore) || string.IsNullOrEmpty(GameStores.GameStoresIDStore))
            {
                if (Config.Exists(Interface.Oxide.ConfigDirectory + "/GameStoresRUST.json"))
                {
                    try
                    {
                        GameStoresConfiguration gameStoresConfiguration = Config.ReadObject<GameStoresConfiguration>(Interface.Oxide.ConfigDirectory + "/GameStoresRUST.json");

                        GameStores.GameStoresAPIStore = gameStoresConfiguration.APISettings.SecretKey;
                        GameStores.GameStoresIDStore = gameStoresConfiguration.APISettings.ShopID;

                        SaveConfig();
                    }
                    catch
                    {
                        PrintError("Error reading GameStoresRUST config!");
                    }
                }

                if (string.IsNullOrEmpty(GameStores.GameStoresAPIStore) || string.IsNullOrEmpty(GameStores.GameStoresIDStore))
                {
                    PrintWarning("Магазин GameStores не настроен! Невозможно выдать баланс пользователю");
                    return;
                }
            }

            if (MoneyTake > 0)
                playerRemoveMoney(userID, MoneyTake);

            webrequest.Enqueue($"https://gamestores.ru/api?shop_id={GameStores.GameStoresIDStore}&secret={GameStores.GameStoresAPIStore}&action=moneys&type=plus&steam_id={userID}&amount={Balance}&mess={GameStores.GameStoresMessage}", null, (i, s) =>
            {
                BasePlayer player = BasePlayer.FindByID(userID);
                if (i != 200) { }
                if (s.Contains("success"))
                {                                                                                    
                    if (player != null)
                    {
                        player.ChatMessage(lang.GetMessage("CHAT_STORE_SUCCESS", this, player.UserIDString).Replace("{0}", $"{MoneyTake}").Replace("{1}",$"{MoneyTake*configData.conclusion.gamestores.how}"));
                                                                                                                       
                        //if (MoneyTake > 0)
                            //Interface_Changer(player);
                    }

                    return;
                }

                if (s.Contains("fail"))
                {
                    Puts($"Пользователь {userID} не авторизован в магазине");
                    if (player != null)
                    {
                        player.ChatMessage(lang.GetMessage("CHAT_NO_AUTH_STORE", this, player.UserIDString));
                        playerGiveMoney(player.userID, MoneyTake);
                    }
                }
            }, this);
        }*/
    }
}