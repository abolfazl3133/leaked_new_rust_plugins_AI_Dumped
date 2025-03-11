//#define DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Plugins 
{
    [Info("GatherLootMultiplier", "https://discord.gg/TrJ7jnS233", "1.3.7")]
    internal class GatherLootMultiplier : RustPlugin
    {
        #region Static
 
        #region DEBUG

#if DEBUG 
        private BasePlayer DEV;
#endif

        private List<ulong> LootedContainers = new List<ulong>();
        private Dictionary<ulong, string> KilledNPCs = new Dictionary<ulong, string>();
        private Dictionary<ulong, string> CustomRateMining = new Dictionary<ulong, string>();
        
        private enum GatherEntities
        {
            Querry,
            PumpJack,
            Chainsaw,
            Excavator,
            Jackhammer,
        }
        
        #endregion



        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
#if DEBUG
            DEV = BasePlayer.FindAwakeOrSleeping("76561198206621603"); 
#endif
 
            if (_config.AutoReload)
                ServerMgr.Instance.StartCoroutine(ConfigCheck()); 
            
            permission.RegisterPermission(_config.Permission, this);

            foreach (var check in _config.CustomRates) 
                permission.RegisterPermission(check.Permission, this);
             
            cmd.AddChatCommand(_config.Command, this, nameof(cmdChatSetupUI));
            
            ModifyExistingLoot(); 
        }
 

        private void Unload()
        {
            ServerMgr.Instance.StopCoroutine(ConfigCheck());
            
            UI.DestroyToAll(".bg"); 
        }
        
        private void OnLootSpawn(LootContainer container) =>
            NextTick(() =>
            {
                if (container == null || !LootedContainers.Contains(container.net.ID.Value)) 
                    return;

                LootedContainers.Remove(container.net.ID.Value);
            });

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (container == null || container.net == null || container.inventory == null || _config.IgnoreLootContainers.Contains(container.PrefabName))
                return;

            var customRate = GetPlayerCustomRate(player.UserIDString);
            if (customRate == null)
                return;

            if (LootedContainers.Contains(container.net.ID.Value))
                return;

            LootedContainers.Add(container.net.ID.Value);

            foreach (var check in container.inventory.itemList)
            {
                if (check == null)
                    continue;
 
                float modify;
                if (customRate.PersonalRates.TryGetValue(check.info.shortname, out modify))
                {
                    check.amount = (int)(check.amount * modify);
                    check.MarkDirty();
                    return;
                }

                check.amount = (int)(check.amount * customRate.CategoriesRates[check.info.category] * customRate.LootGlobalRate);
                check.MarkDirty();
            }        
        }

        private void OnEntityDeath(ScientistNPC entity, HitInfo info)
        {
            if (entity == null || info?.InitiatorPlayer == null)
                return;
            
            if (!KilledNPCs.TryAdd(entity.net.ID.Value, info.InitiatorPlayer.UserIDString))
                KilledNPCs[entity.net.ID.Value] = info.InitiatorPlayer.UserIDString;
        } 
        
        private void OnCorpsePopulate(ScientistNPC npcPlayer, NPCPlayerCorpse corpse)
        {
            if (npcPlayer == null || corpse == null || !KilledNPCs.ContainsKey(npcPlayer.net.ID.Value))
                return;

            if (corpse == null || _config.IgnoreLootContainers.Contains(corpse.PrefabName))
                return;
            
            var customRate = GetPlayerCustomRate(KilledNPCs[npcPlayer.net.ID.Value]);
            if (customRate == null)
                return;

            if (LootedContainers.Contains(corpse.net.ID.Value))
                return;

            LootedContainers.Add(corpse.net.ID.Value);

            NextTick(() =>
            {
                if (corpse == null)
                    return;
                
                foreach (var container in corpse.containers)
                {
                    foreach (var check in container.itemList)
                    {
                        if (check == null)
                            continue;

                        float modify;
                        if (customRate.PersonalRates.TryGetValue(check.info.shortname, out modify))
                        {
                            check.amount = (int)(check.amount * modify);
                            check.MarkDirty();
                            return;
                        }

                        check.amount = (int)(check.amount * customRate.CategoriesRates[check.info.category] * customRate.LootGlobalRate);
                        check.MarkDirty();
                    }
                }
            });
        }


        private void OnEntityDeath(ResourceDispenser entity, HitInfo info)
        {
            if (entity == null || info.InitiatorPlayer == null)
                return;
            
            var customRate = GetPlayerCustomRate(info.InitiatorPlayer.UserIDString);
            if (customRate == null)
                return;
            
            var name = info.InitiatorPlayer.GetActiveItem()?.info?.shortname;
            if (name == "jackhammer" && !customRate.IncreaseProduction[GatherEntities.Jackhammer])
                return;
            
            if (name == "chainsaw" && !customRate.IncreaseProduction[GatherEntities.Chainsaw])
                return;

            foreach (var item in entity.containedItems)
            {
                float modify;
                if (customRate.PersonalRates.TryGetValue(item.itemDef.shortname, out modify))
                { 
                    item.amount = (int) (item.amount * modify);
                    return;
                }
             
                item.amount = (int) (customRate.GatherGlobalRate * customRate.CategoriesRates[item.itemDef.category] * item.amount);
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null)
                return;
            
            var customRate = GetPlayerCustomRate(player.UserIDString);
            if (customRate == null)
                return;
            
            var name = player.GetActiveItem()?.info?.shortname;
            if (name == "jackhammer" && !customRate.IncreaseProduction[GatherEntities.Jackhammer])
                return;
            
            if (name == "chainsaw" && !customRate.IncreaseProduction[GatherEntities.Chainsaw])
                return;
 
            float modify;
            if (customRate.PersonalRates.TryGetValue(item.info.shortname, out modify))
            { 
                item.amount = (int) (item.amount * modify);
                item.MarkDirty();
                return;
            }
             
            item.amount = (int) (customRate.GatherGlobalRate * customRate.CategoriesRates[item.info.category] * item.amount);
            item.MarkDirty();
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) => OnDispenserGather(dispenser, player, item);

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null || collectible == null)
                return;
            
            var customRate = GetPlayerCustomRate(player.UserIDString);
            if (customRate == null)
                return;

            foreach (var check in collectible.itemList)
            {
                float modify;
                if (customRate.PersonalRates.TryGetValue(check.itemDef.shortname, out modify))
                {
                    check.amount = (int) (check.amount * modify);
                    continue;
                }

                check.amount = (int) (customRate.GatherGlobalRate * customRate.CategoriesRates[check.itemDef.category] * check.amount);
            }
        }

        private void OnEntityDeath(LootContainer container, HitInfo info)
        {
            if (container == null || info?.InitiatorPlayer == null || _config.IgnoreLootContainers.Contains(container.PrefabName))
                return;

            var customRate = GetPlayerCustomRate(info.InitiatorPlayer.UserIDString);
            if (customRate == null)
                return;

            if (LootedContainers.Contains(container.net.ID.Value))
                return;

            LootedContainers.Add(container.net.ID.Value);

            foreach (var check in container.inventory.itemList)
            {
                if (check == null)
                    continue;
 
                float modify;
                if (customRate.PersonalRates.TryGetValue(check.info.shortname, out modify))
                {
                    check.amount = (int)(check.amount * modify);
                    check.MarkDirty();
                    return;
                }
 
                check.amount = (int)(check.amount * customRate.CategoriesRates[check.info.category] * customRate.LootGlobalRate);
                check.MarkDirty();
            }  
        }
        
        private void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (player == null || item == null)
                return;

            var customRate = GetPlayerCustomRate(player.UserIDString);
            if (customRate == null)
                return;
            
            float modify;
            if (customRate.PersonalRates.TryGetValue(item.info.shortname, out modify))
            {
                item.amount = (int) (item.amount * modify);
                item.MarkDirty();
                return;
            }
            
            item.amount = (int)(customRate.GatherGlobalRate * customRate.CategoriesRates[item.info.category] * item.amount);
            item.MarkDirty();
        }
        
        private void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            if (quarry == null || player == null || !quarry.IsOn())
                return;

            if (!CustomRateMining.TryAdd(quarry.net.ID.Value, player.UserIDString))
                CustomRateMining[quarry.net.ID.Value] = player.UserIDString;
        }   
        
        private void OnExcavatorResourceSet(ExcavatorArm arm, string resourceName, BasePlayer player)
        {
            if (arm == null || player == null)
                return;

            if (!CustomRateMining.TryAdd(arm.net.ID.Value, player.UserIDString))
                CustomRateMining[arm.net.ID.Value] = player.UserIDString;
        }
        
        
        private void OnQuarryGather(MiningQuarry quarry, Item item) => GatherProduction(quarry?.net?.ID.Value ?? 0, item, GatherEntities.Querry);
        private void OnSurveyGather(SurveyCharge survey, Item item) => GatherProduction(survey?.net?.ID.Value ?? 0, item, GatherEntities.PumpJack);
        private void OnExcavatorGather(ExcavatorArm arm, Item item) => GatherProduction(arm?.net?.ID.Value ?? 0, item, GatherEntities.Excavator);

        #endregion

        #region Functions

        private CustomRates GetPlayerCustomRate(string userID)
        {
            CustomRates customRate = null;
            foreach (var check in _config.CustomRates)
            {
                if (permission.UserHasPermission(userID, check.Permission))
                    customRate = check;
            }
 
            return customRate;
        }
        private static void ModifyExistingLoot()
        {
            foreach (var check in Resources.FindObjectsOfTypeAll<LootContainer>())
                if (check?.inventory != null)
                    check.SpawnLoot();
        }
        
        private void GatherProduction(ulong id, Item item, GatherEntities type)
        {
            if (id == 0 || item == null || !CustomRateMining.ContainsKey(id))
                return;
            
            var customRate = GetPlayerCustomRate(CustomRateMining[id]);
            if (customRate == null || !customRate.IncreaseProduction[type])
               return;
             
            float modify;
            if (customRate.PersonalRates.TryGetValue(item.info.shortname, out modify))
            {
                item.amount = (int) (item.amount * modify);
                item.MarkDirty();
                return;
            }
            
            item.amount = (int) (customRate.GatherGlobalRate * customRate.CategoriesRates[item.info.category] * item.amount);
            item.MarkDirty();
        }
        
        #endregion

        #region Commands

        private void cmdChatSetupUI(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, _config.Permission))
                return;
            
            ShowUIBG(player);
        }
        
        [ConsoleCommand("UI_GLM")]
        private void cmdConsoleUI_GLM(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            switch (arg.GetString(0))
            {
                case "LOOTGLOBALRATE":
                    _config.CustomRates[arg.GetInt(1)].LootGlobalRate = arg.GetFloat(2, _config.CustomRates[arg.GetInt(1)].LootGlobalRate);
                    ModifyExistingLoot();
                    break;           
                case "GATHERGLOBALRATE":
                    _config.CustomRates[arg.GetInt(1)].GatherGlobalRate = arg.GetFloat(2, _config.CustomRates[arg.GetInt(1)].GatherGlobalRate);
                    break;   
                case "PRODUCTION":
                    _config.CustomRates[arg.GetInt(1)].IncreaseProduction[(GatherEntities) Enum.Parse(typeof(GatherEntities),arg.GetString(2))] = !_config.CustomRates[arg.GetInt(1)].IncreaseProduction[(GatherEntities) Enum.Parse(typeof(GatherEntities),arg.GetString(2))];
                    ShowUISettings(player, arg.GetInt(1));
                    break;  
                case "LOOTCATEGORY":
                    _config.CustomRates[arg.GetInt(1)].CategoriesRates[(ItemCategory) Enum.Parse(typeof(ItemCategory),arg.GetString(2))] = arg.GetFloat(3, _config.CustomRates[arg.GetInt(1)].CategoriesRates[(ItemCategory) Enum.Parse(typeof(ItemCategory),arg.GetString(2))]);
                    ShowUISettings(player, arg.GetInt(1));
                    break;        
                case "ADDPERSONALRATE":
                    ShowUISearch(player, arg.GetInt(1));
                    break;     
                case "REMOVEITEM":
                    _config.CustomRates[arg.GetInt(1)].PersonalRates.Remove(arg.GetString(3));
                    ShowUIPersonalRates(player, arg.GetInt(1), arg.GetInt(2) < (int) Math.Ceiling(_config.CustomRates[arg.GetInt(1)].PersonalRates.Count / 29f) - 1 ? arg.GetInt(2) : (int) Math.Ceiling(_config.CustomRates[arg.GetInt(1)].PersonalRates.Count / 29f) - 1);
                    break; 
                case "SETRATE":
                    _config.CustomRates[arg.GetInt(1)].PersonalRates[arg.GetString(2)] = arg.GetFloat(3, _config.CustomRates[arg.GetInt(1)].PersonalRates[arg.GetString(2)]);
                    break;
                case "PAGE":
                    ShowUIPersonalRates(player, arg.GetInt(1), arg.GetInt(2));
                    break;    
                case "CUSTOMRATEPAGE":
                    if (arg.GetInt(1) < 0 || arg.GetInt(1) > _config.CustomRates.Count - 1)
                        return;
                    
                    ShowUICurrentPermission(player, arg.GetInt(1));
                    ShowUISettings(player, arg.GetInt(1));
                    ShowUIPersonalRates(player, arg.GetInt(1));
                     
                    UI.Destroy(player, ".search.bg");
                    break;    
                case "SEARCH":
                    ShowUISearch(player, arg.GetInt(1), string.Join(" ", arg.Args.Skip(2)));
                    break;     
                case "ADDITEM":
                    if (_config.CustomRates[arg.GetInt(1)].PersonalRates.TryAdd(arg.GetString(2), 1f))
                        ShowUIPersonalRates(player, arg.GetInt(1),(int) Math.Ceiling(_config.CustomRates[arg.GetInt(1)].PersonalRates.Count / 29f) - 1);
                     
                    break; 
            }
            
            SaveConfig();
        }

        #endregion

        #region UI 

        private void ShowUISearch(BasePlayer player, int customRatePage = 0, string search = "")
        {
            var container = new CuiElementContainer();
             
            UI.Panel(ref container, ".bg", ".search.bg", ".search.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-610 -350", oMax: "-210 -200", bgColor: "0.15 0.15 0.15 0.95", material: "assets/content/ui/binocular_overlay.mat");

            UI.PanelInput(ref container, ".search.bg", ".search.input", aMin:"0.5 1", aMax:"0.5 1", oMin:"-70 -30", oMax:"70 -5", command:$"UI_GLM SEARCH {customRatePage}", text:string.IsNullOrEmpty(search) ? "Search" : search, bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

            var items = new List<ItemDefinition>();
            if (!string.IsNullOrEmpty(search))
                items.AddRange(ItemManager.GetItemDefinitions().Where(check => check.shortname.Contains(search) || check.displayName.english.Contains(search)));
            else
                items = ItemManager.GetItemDefinitions();
            
            var posY = -70;
            var posX = 5;
            foreach (var check in items.Take(30))
            {
                UI.PanelIcon(ref container, ".search.bg", ".item" + posY + posX, aMin:"0 1", aMax:"0 1", oMin:$"{posX} {posY}", oMax:$"{posX + 37} {posY + 37}", itemID:check.itemid);
                
                UI.Button(ref container, ".item" + posY + posX, command:$"UI_GLM ADDITEM {customRatePage} {check.shortname}");

                posX += 39;
                
                if (posX < 380)
                    continue;

                posY -= 39;
                posX = 5; 
            }
            
            UI.Create(player, container);  
        }

        private void ShowUIPersonalRates(BasePlayer player, int customRatePage = 0, int page = 0)
        {
            var container = new CuiElementContainer();
            
            UI.Panel(ref container, ".bg", ".personalRates.bg", ".personalRates.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-200 -350", oMax: "200 -200", bgColor: "0.15 0.15 0.15 0.95", material: "assets/content/ui/binocular_overlay.mat");

            var posY = -39;
            var posX = 5;

            var personalRates = _config.CustomRates[customRatePage].PersonalRates;
            foreach (var check in personalRates.Skip(29 * page).Take(29))
            {
                UI.PanelIcon(ref container, ".personalRates.bg", ".item" + posY + posX, aMin:"0 1", aMax:"0 1", oMin:$"{posX} {posY}", oMax:$"{posX + 37} {posY + 37}", itemID:ItemManager.FindItemDefinition(check.Key).itemid, bgColor:"0.25 0.25 0.25 0.95");

                UI.Button(ref container, ".item" + posY + posX, aMin:"1 1", aMax:"1 1", oMin:"-10 -10", oMax:"-2 -2", command:$"UI_GLM REMOVEITEM {customRatePage} {page} {check.Key}", sprite:"assets/icons/close.png", bgColor:"0.698 0.2039 0.0039 1");
                
                UI.PanelInput(ref container, ".item" + posY + posX, ".modifier" + posY + posX, aMin: "0 0", aMax: "1 0", oMax: "0 13", text: $"{check.Value}", fontSize:10, command: $"UI_GLM SETRATE {customRatePage} {check.Key}", limit: 7, color: "yellow", paddings: "0 0 0 0", bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

                posX += 39;
                
                if (posX < 380)
                    continue;

                posY -= 39;
                posX = 5;  
            }
            
            UI.Button(ref container, ".personalRates.bg", aMin:"0 1", aMax:"0 1", oMin:$"{posX + 5} {posY + 5}", oMax:$"{posX + 32} {posY + 32}", command:$"UI_GLM ADDPERSONALRATE {customRatePage}", sprite:"assets/icons/health.png", bgColor:"0.711 0.8676 0.4338 1");

            #region Navigation 

            UI.Button(ref container, ".personalRates.bg", aMin:"0.5 0", aMax:"0.5 0", oMin:"-35 0", oMax:"-15 20", text:"<", command:page > 0 ? $"UI_GLM PAGE {customRatePage} {page - 1}" : "", fontSize:14, font:"robotocondensed-bold.ttf");
            
            UI.Label(ref container, ".personalRates.bg", aMin:"0.5 0", aMax:"0.5 0", oMin:"-13 0", oMax:"13 20", text:$"{page + 1}/{(personalRates.Count / 29f > 0 ? Math.Ceiling(personalRates.Count / 29f) : 1)}", fontSize:12);
            
            UI.Button(ref container, ".personalRates.bg", aMin:"0.5 0", aMax:"0.5 0", oMin:"15 0", oMax:"35 20", text:">", command:_config.CustomRates[customRatePage].PersonalRates.Count() / 29f > page + 1 ? $"UI_GLM PAGE {customRatePage} {page + 1}" : "", fontSize:14, font:"robotocondensed-bold.ttf");
            
            #endregion
            
             
            UI.Create(player, container);
        }

        private void ShowUISettings(BasePlayer player, int customRatePage = 0)
        {
            var container = new CuiElementContainer();

            UI.Panel(ref container, ".bg", ".setup.bg", ".setup.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-200 -190", oMax: "200 300", bgColor: "0.15 0.15 0.15 0.95", material: "assets/content/ui/binocular_overlay.mat");

            #region Settigns

            var posY = -25;

            UI.Panel(ref container, ".setup.bg", ".attribute.bg" + posY, aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", bgColor: "0 0 0 0");

            UI.Label(ref container, ".attribute.bg" + posY, text: "[LOOT] Global rate:", align: TextAnchor.MiddleLeft);

            var customRate = _config.CustomRates[customRatePage];
            UI.PanelInput(ref container, ".attribute.bg" + posY, ".attribute.input", aMin: "1 0", aMax: "1 1", oMin: "-60 0", text: $"{customRate.LootGlobalRate}", command: $"UI_GLM LOOTGLOBALRATE {customRatePage}", limit: 7, color: "yellow", paddings: "0 0 0 0", bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

            posY -= 23;

            UI.Panel(ref container, ".setup.bg", ".attribute.bg" + posY, aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", bgColor: "0 0 0 0");

            UI.Label(ref container, ".attribute.bg" + posY, text: "[GATHER] Global rate:", align: TextAnchor.MiddleLeft);

            UI.PanelInput(ref container, ".attribute.bg" + posY, ".attribute.input", aMin: "1 0", aMax: "1 1", oMin: "-60 0", text: $"{customRate.GatherGlobalRate}", command: $"UI_GLM GATHERGLOBALRATE {customRatePage}", limit: 7, color: "yellow", paddings: "0 0 0 0", bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

            posY -= 23;

            foreach (var check in customRate.IncreaseProduction)
            {
                UI.Panel(ref container, ".setup.bg", ".attribute.bg" + posY, aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", bgColor: "0 0 0 0");

                UI.Label(ref container, ".attribute.bg" + posY, text: $"{check.Key}:", align: TextAnchor.MiddleLeft);

                UI.Button(ref container, ".attribute.bg" + posY, aMin: "1 0", aMax: "1 1", oMin: "-60 0", text: $"{(check.Value ? "ON" : "OFF")}", command: $"UI_GLM PRODUCTION {customRatePage} {check.Key}", color: check.Value ? "0.451 0.5529 0.2706 1" : "0.698 0.2039 0.0039 1", font: "robotocondensed-bold.ttf");

                posY -= 23;
            }
            
            foreach (var check in customRate.CategoriesRates)
            {
                UI.Panel(ref container, ".setup.bg", ".attribute.bg" + posY, aMin: "0 1", aMax: "1 1", oMin: $"5 {posY}", oMax: $"-5 {posY + 20}", bgColor: "0 0 0 0");

                UI.Label(ref container, ".attribute.bg" + posY, text: $"{check.Key}:", align: TextAnchor.MiddleLeft);

                UI.PanelInput(ref container, ".attribute.bg" + posY, ".attribute.input", aMin: "1 0", aMax: "1 1", oMin: "-60 0", text: $"{check.Value}", command: $"UI_GLM LOOTCATEGORY {customRatePage} {check.Key}", limit: 7, color: "yellow", paddings: "0 0 0 0", bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

                posY -= 23;
            }

            #endregion

            UI.Create(player, container);
        }

        private void ShowUIBG(BasePlayer player)
        {
            var container = new CuiElementContainer();
            
            UI.MainParent(ref container, aMin:"0 0", aMax:"1 1");
            
            UI.Panel(ref container, ".bg", bgColor:"0 0 0 0.85", material:"assets/content/ui/uibackgroundblur-ingamemenu.mat");
            
            UI.Button(ref container, ".bg", ".bg", aMin:"1 1", aMax:"1 1", oMin:"-45 -45", oMax:"-15 -15", sprite:"assets/icons/close.png", bgColor:"0.698 0.2039 0.0039 1");
            
            UI.Create(player, container);

            var perm = _config.CustomRates[0];

            ShowUICurrentPermission(player);
            ShowUISettings(player);
            ShowUIPersonalRates(player);
        }

        private void ShowUICurrentPermission(BasePlayer player, int customRatePage = 0)
        {
            var container = new CuiElementContainer(); 
            
            UI.Panel(ref container, ".bg", ".currentPermission.bg", ".currentPermission.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-200 310", oMax: "200 350", bgColor: "0.15 0.15 0.15 0.95", material: "assets/content/ui/binocular_overlay.mat");

            UI.PanelInput(ref container, ".currentPermission.bg", ".perm", aMin:"0.15 0.15", aMax:"0.85 0.85", text:_config.CustomRates[customRatePage].Permission, bgColor:"0.65 0.65 0.65 0.8");
            
            UI.Button(ref container, ".currentPermission.bg", aMin:"0 0", aMax:"0.15 1", text:"<", command:$"UI_GLM CUSTOMRATEPAGE {customRatePage - 1}");
            
            UI.Button(ref container, ".currentPermission.bg", aMin:"0.85 0", aMax:"1 1", text:">", command:$"UI_GLM CUSTOMRATEPAGE {customRatePage + 1}");
            
            UI.Create(player, container);
        }
        

        #endregion

        #region Classes

        private class Configuration
        {
            [JsonIgnore] public string JSON;

            [JsonProperty("Auto reload [If you change the config and save the file the plugin will reload itself]")]
            public bool AutoReload = true;
            
            [JsonProperty("Setup UI command")]
            public string Command = "rs";
            
            [JsonProperty("Premission for setup UI")]
            public string Permission = "gatherlootmultiplier.setup";

            [JsonProperty("Custom Rates", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CustomRates> CustomRates = new List<CustomRates>
            {
                new CustomRates 
                {
                    Permission = "gatherlootmultiplier.default",
                    LootGlobalRate = 2f,
                    GatherGlobalRate = 3f,
                    IncreaseProduction = new Dictionary<GatherEntities, bool>
                    {
                        [GatherEntities.Querry] = true,
                        [GatherEntities.PumpJack] = true,
                        [GatherEntities.Chainsaw] = true,
                        [GatherEntities.Excavator] = true,
                        [GatherEntities.Jackhammer] = true,
                    },
                    CategoriesRates = new Dictionary<ItemCategory, float>
                    {
                        [ItemCategory.Weapon] = 1,
                        [ItemCategory.Construction] = 1,
                        [ItemCategory.Items] = 1,
                        [ItemCategory.Resources] = 1,
                        [ItemCategory.Attire] = 1,
                        [ItemCategory.Tool] = 1,
                        [ItemCategory.Medical] = 1,
                        [ItemCategory.Food] = 1,
                        [ItemCategory.Ammunition] = 1,
                        [ItemCategory.Traps] = 1,
                        [ItemCategory.Misc] = 1,
                        [ItemCategory.Component] = 1,
                        [ItemCategory.Electrical] = 1,
                        [ItemCategory.Fun] = 1
                    },
                    PersonalRates = new Dictionary<string, float>()
                },
                new CustomRates
                {
                    Permission = "gatherlootmultiplier.vip",
                    LootGlobalRate = 3f,
                    GatherGlobalRate = 5f,
                    IncreaseProduction = new Dictionary<GatherEntities, bool>
                    {
                        [GatherEntities.Querry] = true,
                        [GatherEntities.PumpJack] = true,
                        [GatherEntities.Chainsaw] = true,
                        [GatherEntities.Excavator] = true,
                        [GatherEntities.Jackhammer] = true,
                    },
                    CategoriesRates = new Dictionary<ItemCategory, float>
                    {
                        [ItemCategory.Weapon] = 1,
                        [ItemCategory.Construction] = 1,
                        [ItemCategory.Items] = 1,
                        [ItemCategory.Resources] = 1,
                        [ItemCategory.Attire] = 1,
                        [ItemCategory.Tool] = 1,
                        [ItemCategory.Medical] = 1,
                        [ItemCategory.Food] = 1,
                        [ItemCategory.Ammunition] = 1,
                        [ItemCategory.Traps] = 1,
                        [ItemCategory.Misc] = 1,
                        [ItemCategory.Component] = 1,
                        [ItemCategory.Electrical] = 1,
                        [ItemCategory.Fun] = 1
                    },
                    PersonalRates = new Dictionary<string, float>()
                },
            };

            [JsonProperty("[LOOT] Ignore loot containers [prefabs]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> IgnoreLootContainers = new List<string>
            {
                "",
            };
        }

        private class CustomRates
        {
            [JsonProperty("Permission")]
            public string Permission;
            
            [JsonProperty("[LOOT] Gobal rate")]
            public float LootGlobalRate;

            [JsonProperty("[GATHER] Gobal rate")]
            public float GatherGlobalRate;

            [JsonProperty("[GATHER] Increase production", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<GatherEntities, bool> IncreaseProduction;

            [JsonProperty("[LOOT] Categories rates [global rate * category rate]")]
            public Dictionary<ItemCategory, float> CategoriesRates;

            [JsonProperty("Personal rates for items [shortname + modifier]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> PersonalRates;

        }

        #endregion

        #region Stuff

        #region Config

        private Configuration _config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            if (_config.AutoReload)
                _config.JSON = JsonConvert.SerializeObject(_config);

            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        private IEnumerator ConfigCheck()
        {
            var jError = string.Empty;

            while (true)
            {
                if (!IsLoaded)
                    yield break;

                try
                {
                    var checkConfig = Config.ReadObject<Configuration>();
                    if (checkConfig == null || JsonConvert.SerializeObject(Config.ReadObject<Configuration>()) == _config.JSON)
                        throw new Exception();

                    Interface.Oxide.ReloadPlugin(Name);

                    jError = string.Empty;
                }

                catch
                {
                    if (!string.IsNullOrEmpty(jError) && jError != File.ReadAllText(Path.Combine(Manager.ConfigPath, Name + ".json")))
                    {
                        jError = File.ReadAllText(Path.Combine(Manager.ConfigPath, Name + ".json"));

                        PrintError("Your configuration file contains an error. Using default configuration values.");
                    }
                }

                yield return new WaitForSeconds(2f);
            }
        }

        #endregion

        #region GUI

        private class UI
        {
            private const string Layer = "UI_GatherLootMultiplier";

            #region MainElements

            public static void MainParent(ref CuiElementContainer container, string name = null, string aMin = "0.5 0.5", string aMax = "0.5 0.5", bool overAll = true, bool keyboardEnabled = true, bool cursorEnabled = true) =>
                container.Add(new CuiPanel
                {
                    KeyboardEnabled = keyboardEnabled,
                    CursorEnabled = cursorEnabled,
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Image = { Color = "0 0 0 0" }
                }, overAll ? "Overlay" : "Hud", Layer + ".bg" + name, Layer + ".bg" + name);

            public static void Panel(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string bgColor = "0.33 0.33 0.33 1", string material = null) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiImageComponent { Color = HexToRustFormat(bgColor), Material = material },
                    },
                });

            public static void Icon(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", int itemID = 0, ulong skinID = 0, string sprite = null) =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiImageComponent { ItemId = itemID, SkinId = skinID, Sprite = sprite },
                    },
                });

            public static void Image(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string image = "", string color = "1 1 1 1") =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiRawImageComponent { Png = !image.StartsWith("http") && !image.StartsWith("www") ? image : null, Url = image.StartsWith("http") || image.StartsWith("www") ? image : null, Color = HexToRustFormat(color), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    },
                });

            public static void Label(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int fontSize = 16, string color = "1 1 1 1", TextAnchor align = TextAnchor.MiddleCenter, string outlineDistance = null, string outlineColor = "0 0 0 1", VerticalWrapMode wrapMode = VerticalWrapMode.Truncate, string font = "robotocondensed-regular.ttf") =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiTextComponent { Text = text, FontSize = fontSize, Color = HexToRustFormat(color), Align = align, Font = font, VerticalOverflow = wrapMode },
                        outlineDistance == null ? new CuiOutlineComponent { Distance = "0 0", Color = "0 0 0 0" } : new CuiOutlineComponent { Distance = outlineDistance, Color = HexToRustFormat(outlineColor) },
                    },
                });

            public static void Button(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int fontSize = 16, string color = "1 1 1 1", string command = null, string bgColor = "0 0 0 0", VerticalWrapMode wrapMode = VerticalWrapMode.Truncate, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string material = null, string sprite = null) =>
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                    Text = { Text = text, FontSize = fontSize, Color = HexToRustFormat(color), Align = align, Font = font, VerticalOverflow = wrapMode },
                    Button = { Command = command, Close = command == null ? Layer + name : null, Color = HexToRustFormat(bgColor), Material = material, Sprite = sprite }
                }, Layer + parent, command == null ? null : Layer + name, destroy == null ? null : Layer + destroy);

            public static void Input(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int limit = 40, int fontSize = 16, string color = "1 1 1 1", string command = null, TextAnchor align = TextAnchor.MiddleCenter, bool autoFocus = false, bool hudMenuInput = false, bool readOnly = false, bool isPassword = false, bool needsKeyboard = false, bool singleLine = true, string font = "robotocondensed-regular.ttf") =>
                container.Add(new CuiElement
                {
                    Parent = Layer + parent,
                    Name = Layer + name,
                    DestroyUi = destroy == null ? null : Layer + destroy,
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
                        new CuiInputFieldComponent { Text = text, Command = command, CharsLimit = limit, FontSize = fontSize, Color = HexToRustFormat(color), Align = align, Font = font, Autofocus = autoFocus, IsPassword = isPassword, ReadOnly = readOnly, HudMenuInput = hudMenuInput, NeedsKeyboard = needsKeyboard, LineType = singleLine ? InputField.LineType.SingleLine : InputField.LineType.MultiLineNewline },
                    }
                });


            #endregion

            #region CombineElements

            public static void Outline(ref CuiElementContainer container, string parent, string size = "1 1 1 1", string color = "0 0 0 1", bool external = false)
            {
                var borders = size.Split(' ');

                if (borders[0] != "0")
                    Panel(ref container, parent, aMin: "0 1", aMax: "1 1", oMin: $"-{borders[0]} {(external ? "0" : "-" + borders[0])}", oMax: $"{borders[0]} {(external ? borders[0] : "0")}", bgColor: color);
                if (borders[1] != "0")
                    Panel(ref container, parent, aMin: "1 0", aMax: "1 1", oMin: $"{(external ? "0" : "-" + borders[1])} -{borders[1]}", oMax: $"{(external ? borders[1] : "0")} {borders[1]}", bgColor: color);
                if (borders[2] != "0")
                    Panel(ref container, parent, aMin: "0 0", aMax: "1 0", oMin: $"-{borders[2]} {(external ? "-" + borders[2] : "0")}", oMax: $"{borders[2]} {(external ? "0" : borders[2])}", bgColor: color);
                if (borders[3] != "0")
                    Panel(ref container, parent, aMin: "0 0", aMax: "0 1", oMin: $"{(external ? "-" + borders[3] : "0")} -{borders[3]}", oMax: $"{(external ? "0" : borders[3])} {borders[3]}", bgColor: color);
            }

            public static void ImageButton(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string image = null, string color = "0 0 0 0", string command = null)
            {
                Image(ref container, parent, name + ".bg", destroy, aMin, aMax, oMin, oMax, image, color);

                Button(ref container,
                    name + ".bg",
                    name: name,
                    command: command);
            }

            public static void PanelInput(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", string text = null, int limit = 40, int fontSize = 16, string color = "1 1 1 1", string command = null, TextAnchor align = TextAnchor.MiddleCenter, bool autoFocus = false, bool hudMenuInput = false, bool readOnly = false, bool isPassword = false, bool needsKeyboard = false, bool singleLine = true, string font = "robotocondensed-regular.ttf", string sprite = null, string bgColor = "0 0 0 0", string paddings = "0 0 0 0", string material = null)
            {
                Panel(ref container, parent, name + ".bg", destroy, aMin, aMax, oMin, oMax, bgColor, material);

                var padding = paddings.Split(' ');
                Input(ref container,
                    name + ".bg",
                    oMin: $"{padding[3].ToInt()} {padding[2].ToInt()}",
                    oMax: $"{-padding[1].ToInt()} {-padding[0].ToInt()}",
                    text: text,
                    limit: limit,
                    fontSize: fontSize,
                    color: color,
                    command: command,
                    align: align,
                    autoFocus: autoFocus,
                    hudMenuInput: hudMenuInput,
                    needsKeyboard: needsKeyboard,
                    isPassword: isPassword,
                    readOnly: readOnly,
                    singleLine: singleLine,
                    font: font);
            }

            public static void PanelIcon(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", int itemID = 0, ulong skinID = 0, string sprite = null, string bgColor = "0 0 0 0", string paddings = "0 0 0 0", string material = null)
            {
                Panel(ref container, parent, name + ".bg", destroy, aMin, aMax, oMin, oMax, bgColor, material);

                var padding = paddings.Split(' ');
                Icon(ref container,
                    name + ".bg", name,
                    oMin: $"{padding[3].ToInt()} {padding[2].ToInt()}",
                    oMax: $"{-padding[1].ToInt()} {-padding[0].ToInt()}",
                    itemID: itemID,
                    skinID: skinID);
            }

            public static void ItemIcon(ref CuiElementContainer container, string parent, string name = null, string destroy = null, string aMin = "0 0", string aMax = "1 1", string oMin = "0 0", string oMax = "0 0", int itemID = 0, ulong skinID = 0, int amount = 1, int fontSize = 12, string color = "1 1 1 1", string bgColor = "0 0 0 0", string paddings = "0 0 0 0", string material = null)
            {
                PanelIcon(ref container, parent, name, destroy, aMin, aMax, oMin, oMax, itemID, skinID, null, bgColor, paddings, material);

                Label(ref container, name + ".bg", oMin: "0 2", oMax: "-4 0", text: $"x{amount}", align: TextAnchor.LowerRight, color: color, fontSize: fontSize);
            }

            #endregion

            #region Functions

            public static string HexToRustFormat(string hex)
            {
                if (string.IsNullOrEmpty(hex))
                    return hex;

                Color color;

                if (hex.Contains(":"))
                    return ColorUtility.TryParseHtmlString(hex.Substring(0, hex.IndexOf(":")), out color) ? $"{color.r:F2} {color.g:F2} {color.b:F2} {hex.Substring(hex.IndexOf(":") + 1, hex.Length - hex.IndexOf(":") - 1)}" : hex;

                return ColorUtility.TryParseHtmlString(hex, out color) ? $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}" : hex;
            }

            public static void Create(BasePlayer player, CuiElementContainer container)
            {
                CuiHelper.AddUi(player, container);
            }

            public static void CreateToAll(CuiElementContainer container, string layer)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    CuiHelper.AddUi(player, container);
            }

            public static void Destroy(BasePlayer player, string layer) => CuiHelper.DestroyUi(player, Layer + layer);

            public static void DestroyToAll(string layer)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    Destroy(player, layer);
            }

            #endregion
        }

        #endregion

        #endregion
    }
}