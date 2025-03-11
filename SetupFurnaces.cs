using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Plugins
{
    [Info("SetupFurnaces", "tofurahie", "2.2.11")]
    internal class SetupFurnaces : RustPlugin
    {
        #region Static 

        private const string PERM = "setupfurnaces.use";

        private Dictionary<string, int> AmountOfBecomeData = new();

        private Dictionary<ulong, SetupOven> OvenSetup = new();

        #endregion

        #region OxideHooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnOvenCook));
            Unsubscribe(nameof(OnOvenStart));
            Unsubscribe(nameof(OnFindBurnable));
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM, this);

            foreach (var check in ItemManager.GetItemDefinitions())
                if (check.ItemModCookable != null) 
                    AmountOfBecomeData.TryAdd(check.shortname, check.ItemModCookable.amountOfBecome);

            foreach (var check in _config.PlayersOvenSetup)
                    permission.RegisterPermission(check.Key, this);

            foreach (var item in BaseNetworkable.serverEntities.OfType<BaseOven>())
                OnEntitySpawned(item); 
            
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnOvenCook));
            Subscribe(nameof(OnOvenStart));
            Subscribe(nameof(OnFindBurnable));
        }

        private void Unload()
        {
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnOvenCook));  
            Subscribe(nameof(OnOvenStart));
            Subscribe(nameof(OnFindBurnable));
            
            foreach (var check in BaseNetworkable.serverEntities.OfType<BaseOven>())
            {
                if (check != null &&  check.IsOn())
                {
                    check.StopCooking(); 
                    check.StartCooking();
                }
            }
            
            foreach (var check in ItemManager.GetItemDefinitions())
                if (check.ItemModCookable != null)
                    check.ItemModCookable.amountOfBecome = AmountOfBecomeData[check.shortname];

            UI.DestroyToAll(".bg");
        }

        private void OnEntitySpawned(BaseOven oven)
        {
            if (oven == null)
                return;

            if (!_config.OvenPrefabs.ContainsKey(oven.ShortPrefabName) || oven.OwnerID == 0)
            {
                OvenSetup.TryAdd(oven.net.ID.Value, null);
                return;
            }

            SetupOven setupOven = null;
            foreach (var item in _config.PlayersOvenSetup)
            {
                if (!permission.UserHasPermission(oven.OwnerID.ToString(), item.Key))
                    continue;

                setupOven = item.Value[oven.ShortPrefabName];
                setupOven.OwnerID = oven.OwnerID;  
            }

            OvenSetup.TryAdd(oven.net.ID.Value, setupOven);

            if (!oven.IsOn())
                return;
            
            OnOvenStart(oven); 
        }

        private void OnEntityKill(BaseOven oven)
        {
            if (oven == null)
                return;

            OvenSetup.Remove(oven.net.ID.Value);
        }
        private Item OnFindBurnable(BaseOven oven)
        {
            if (oven == null)
                return null;

            OvenSetup.TryAdd(oven.net.ID.Value, null);
            var ovenModifiers = OvenSetup[oven.net.ID.Value];
            if (ovenModifiers == null)
                return null;

            var hasBurnable = false;
            foreach (var burnable in oven.inventory.itemList)
                if (oven.IsBurnableItem(burnable))
                    hasBurnable = true;

            if (!hasBurnable && ovenModifiers.EnableNoFuel)
                return ItemManager.CreateByName("wood");

            return null;
        }
         
        private void OnOvenCook(BaseOven oven, Item item)
        {
            if (oven == null || oven.inventory.itemList.Count == 0)
                return;

            OvenSetup.TryAdd(oven.net.ID.Value, null);
            var ovenModifiers = OvenSetup[oven.net.ID.Value];
            if (ovenModifiers == null)
            {
                foreach (var check in oven.inventory.itemList)
                {
                    if (check.info.ItemModCookable == null)
                        continue;

                    check.info.ItemModCookable.amountOfBecome = AmountOfBecomeData[check.info.shortname];
                }

                return;
            }

            foreach (var check in oven.inventory.itemList)
            {
                if (check.info.ItemModCookable == null)
                    continue;

                check.info.ItemModCookable.amountOfBecome = ovenModifiers.OutputMultiplier * AmountOfBecomeData[check.info.shortname];
            }
        }

        private void OnOvenToggle(BaseOven oven, BasePlayer player) =>
            NextTick(() =>
            {
                if (oven == null)
                    return;

                var ovenModifiers = OvenSetup[oven.net.ID.Value];
                if (ovenModifiers == null)
                    return;

                foreach (var burnable in oven.inventory.itemList)
                    if (oven.IsBurnableItem(burnable))
                        burnable.fuel = oven.IsOn() ? burnable.fuel * ovenModifiers.ConsumeMultiplier : 10;
            });

        private object OnOvenStart(BaseOven oven)
        {
            if (oven == null)
                return null;
            
            OvenSetup.TryAdd(oven.net.ID.Value, null);
            var ovenModifiers = OvenSetup[oven.net.ID.Value];
            if (ovenModifiers == null)
                return null;

            if (!ovenModifiers.EnableNoFuel && oven.FindBurnable() == null && !oven.CanRunWithNoFuel)
                return null;

            oven.inventory.temperature = oven.cookingTemperature;
            oven.UpdateAttachmentTemperature();
            oven.InvokeRepeating(oven.Cook, 0.5f, 0.5f / ovenModifiers.QuickSmeltMultiplier);
            oven.SetFlag(BaseEntity.Flags.On, true);
            Interface.CallHook("OnOvenStarted", this); 
            return true;
        }

        private void OnLootEntity(BasePlayer player, BaseOven oven)
        {
            if (player == null || !_config.OvenPrefabs.ContainsKey(oven?.ShortPrefabName ?? ""))
                return;

            if (oven.OwnerID == 0)
                oven.OwnerID = player.userID;

            OvenSetup.TryAdd(oven.net.ID.Value, null);
            var ovenModifiers = OvenSetup[oven.net.ID.Value];
            if (ovenModifiers == null)
                return;

            if (ovenModifiers.EnableReorganize)
                ShowUIReorganizeButton(player);
        }

        private void OnLootEntityEnd(BasePlayer player, BaseOven oven) => UI.Destroy(player, ".bg");

        private void OnUserPermissionGranted(string id, string permName)
        {
            if (_config.PlayersOvenSetup.ContainsKey(permName))
                OnPermChanged(ulong.Parse(id));
        }

        private void OnUserPermissionRevoked(string id, string permName)
        {
            if (_config.PlayersOvenSetup.ContainsKey(permName))
                OnPermChanged(ulong.Parse(id)); 
        }

        private void OnUserGroupAdded(string id, string groupName)
        {
            if (permission.GetGroupPermissions(groupName).Any(x => _config.PlayersOvenSetup.ContainsKey(x)))
                OnPermChanged(ulong.Parse(id));
        }

        private void OnUserGroupRemoved(string id, string groupName)
        {
            if (permission.GetGroupPermissions(groupName).Any(x => _config.PlayersOvenSetup.ContainsKey(x)))
                OnPermChanged(ulong.Parse(id));
        }

        #endregion

        #region Functions
        private void OnPermChanged(ulong userID)
        {
            foreach (var oven in BaseNetworkable.serverEntities.OfType<BaseOven>())
            {
                if (oven.OwnerID != userID)
                    continue;

                if (!_config.OvenPrefabs.ContainsKey(oven.ShortPrefabName))
                {
                    OvenSetup.TryAdd(oven.net.ID.Value, null);
                    continue;
                }
                
                SetupOven setupOven = null;
                foreach (var item in _config.PlayersOvenSetup)
                {
                    if (!permission.UserHasPermission(oven.OwnerID.ToString(), item.Key))
                        continue;

                    setupOven = item.Value[oven.ShortPrefabName];
                    setupOven.OwnerID = oven.OwnerID;
                }

                OvenSetup[oven.net.ID.Value] = setupOven;
            }
        }
        private void Reorganize(BaseOven oven, BasePlayer player)
        {
            var ovenInv = oven.inventory;
            var ovenItemList = ovenInv.itemList;
            var playerInv = player.inventory;
            var smeltingSpeed = 0.5 * oven.GetSmeltingSpeed();
            var ovenCookingTemperature = 10 / (0.5 * (oven.cookingTemperature / 200));

            var items = new List<Item>();
            playerInv.GetAllItems(items);
            foreach (var check in items)
                check.MoveToContainer(ovenInv);

            var woodAmount = ovenInv.GetAmount(-151838493, true);
            var woodNeed = 0;
            var inputSlotIndex = oven._inputSlotIndex;
            var outputSlotIndex = oven._outputSlotIndex;
            foreach (var check in ovenItemList)
            {
                if (check.position < inputSlotIndex || check.position >= outputSlotIndex) continue;
                woodNeed += (int)Math.Ceiling(ItemManager.CreateByItemID(check.info.itemid).cookTimeLeft * check.amount / smeltingSpeed / ovenCookingTemperature);
            }

            if (woodAmount > woodNeed)
            {
                ovenInv.Take(null, -151838493, woodAmount - woodNeed);
                player.inventory.GiveItem(ItemManager.CreateByItemID(-151838493, woodAmount - woodNeed));
            }

            foreach (var check in ovenItemList.ToArray())
                if (check.position >= outputSlotIndex)
                    check.MoveToContainer(check.CanMoveTo(player.inventory.containerMain) ? player.inventory.containerMain : player.inventory.containerBelt);
        }



        #endregion

        #region Commands

        [ChatCommand("fsetup")]
        private void cmdChatfsetup(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM) && !player.IsAdmin)
                return;

            ShowUISetup(player);
        }

        [ChatCommand("fadd")]
        private void cmdChatfadd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM) && !player.IsAdmin)
                return;

            RaycastHit info;
            Physics.Raycast(player.eyes.HeadRay(), out info, 10f);
            if (info.GetEntity() is not BaseOven)
            {
                SendReply(player, "Entity wasn't found");
                return;
            }

            var oven = info.GetEntity() as BaseOven;

            foreach (var check in _config.PlayersOvenSetup)
                check.Value.TryAdd(oven.ShortPrefabName, new SetupOven());

            if (!_config.OvenPrefabs.TryAdd(oven.ShortPrefabName, oven.pickup.itemTarget.displayName.english))
            {
                SendReply(player, "It was already in the list");
                return;
            }

            SendReply(player, "The entity was added successfully");
        }

        [ChatCommand("fremove")]
        private void cmdChatfremove(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM) && !player.IsAdmin)
                return;
 
            RaycastHit info;
            Physics.Raycast(player.eyes.HeadRay(), out info, 10f);
            if (info.GetEntity() is not BaseOven)
            {
                SendReply(player, "Entity wasn't found");
                return;
            }

            var oven = info.GetEntity();

            if (!_config.OvenPrefabs.ContainsKey(oven.ShortPrefabName))
            {
                SendReply(player, "The entity isn't in the list");
                return;
            }

            foreach (var check in _config.PlayersOvenSetup)
                check.Value.Remove(oven.ShortPrefabName);

            _config.OvenPrefabs.Remove(oven.ShortPrefabName);


            SendReply(player, "The entity was removed successfully");
        }


        [ConsoleCommand("UI_SF")]
        private void cmdConsoleUI_SF(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            switch (arg.GetString(0))
            {
                case "REORGANIZE":
                    if (!(player.inventory.loot.entitySource is BaseOven oven))
                        return;

                    Reorganize(oven, player);
                    break;
                case "BACKTOPREFABS":
                    ShowUIOvenPrefabs(player, arg.GetString(1));
                    break;
                case "BACKTOPERMS":
                    ShowUISetup(player);
                    break;

                case "SELECT":
                    switch (arg.GetString(1))
                    {
                        case "PERM":
                            ShowUIOvenPrefabs(player, arg.GetString(2));
                            break;
                        case "OVEN":
                            ShowUIOvenSetup(player, arg.GetString(2), arg.GetString(3));
                            break;
                    }
                    break;

                case "CHANGECONFIG":
                    switch (arg.GetString(1))
                    {
                        case "QUICKSMELT":
                            _config.PlayersOvenSetup[arg.GetString(2)][arg.GetString(3)].QuickSmeltMultiplier = arg.GetFloat(4);

                            SaveConfig();
                            break;
                        case "OUTPUTMULTIPLIER":
                            _config.PlayersOvenSetup[arg.GetString(2)][arg.GetString(3)].OutputMultiplier = arg.GetInt(4);

                            SaveConfig();
                            break;
                        case "CONSUMEMULTIPLIER":
                            _config.PlayersOvenSetup[arg.GetString(2)][arg.GetString(3)].ConsumeMultiplier = arg.GetFloat(4);

                            SaveConfig();
                            break;
                        case "REORGANIZER":
                            _config.PlayersOvenSetup[arg.GetString(2)][arg.GetString(3)].EnableReorganize = !_config.PlayersOvenSetup[arg.GetString(2)][arg.GetString(3)].EnableReorganize;
                            ShowUIOvenSetup(player, arg.GetString(2), arg.GetString(3));

                            SaveConfig();
                            break;
                        case "NOFUEL":
                            _config.PlayersOvenSetup[arg.GetString(2)][arg.GetString(3)].EnableNoFuel = !_config.PlayersOvenSetup[arg.GetString(2)][arg.GetString(3)].EnableNoFuel;
                            ShowUIOvenSetup(player, arg.GetString(2), arg.GetString(3));

                            SaveConfig();
                            break;
                    }

                    OnServerInitialized();
                    break;
            }
        }

        #endregion

        #region UI

        private void ShowUIReorganizeButton(BasePlayer player)
        {
            var container = new CuiElementContainer();

            UI.MainParent(ref container, null, "0.5 0", "0.5 0", true, false, false);

            UI.Button(ref container, ".bg", command: "UI_SF REORGANIZE", oMin: "270 109", oMax: "300 141", bgColor: "0.1829 0.3897 0.5338 1", sprite: "assets/icons/refresh.png");

            UI.Create(player, container);
        }

        private void ShowUIOvenSetup(BasePlayer player, string permName, string prefab)
        {
            var container = new CuiElementContainer();

            var setupOven = _config.PlayersOvenSetup[permName][prefab];

            UI.Panel(ref container, ".bg", ".setup.bg", ".setup.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: "-150 -79", oMax: "150 79", material: "assets/content/ui/binocular_overlay.mat");

            UI.Label(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -35", oMax: "-5 -5", text: "Quick smelt multiplier", align: TextAnchor.MiddleLeft);
            UI.PanelInput(ref container, ".setup.bg", ".search.quicksmelt.input", aMin: "0 1", aMax: "1 1", oMin: "155 -34", oMax: "-5 -6", command: $"UI_SF CHANGECONFIG QUICKSMELT {permName} {prefab}", text: setupOven.QuickSmeltMultiplier.ToString(), bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

            UI.Label(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -65", oMax: "-5 -35", text: "Output multiplier", align: TextAnchor.MiddleLeft);
            UI.PanelInput(ref container, ".setup.bg", ".search.outputmultiplier.input", aMin: "0 1", aMax: "1 1", oMin: "155 -64", oMax: "-5 -36", command: $"UI_SF CHANGECONFIG OUTPUTMULTIPLIER {permName} {prefab}", text: setupOven.OutputMultiplier.ToString(), bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

            UI.Label(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -95", oMax: "-5 -65", text: "Fuel consume multiplier", align: TextAnchor.MiddleLeft);
            UI.PanelInput(ref container, ".setup.bg", ".search.consumemultiplier.input", aMin: "0 1", aMax: "1 1", oMin: "155 -94", oMax: "-5 -66", command: $"UI_SF CHANGECONFIG CONSUMEMULTIPLIER {permName} {prefab}", text: setupOven.ConsumeMultiplier.ToString(), bgColor: "0.65 0.65 0.65 0.8", material: "assets/content/ui/uibackgroundblur-notice.mat");

            UI.Label(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -125", oMax: "-5 -95", text: "Add reorganize button", align: TextAnchor.MiddleLeft);
            UI.Button(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -125", oMax: "-5 -95", text: setupOven.EnableReorganize ? "<color=green>ON</color>" : "<color=red>OFF</color>", command: $"UI_SF CHANGECONFIG REORGANIZER {permName} {prefab}", align: TextAnchor.MiddleRight, font: "robotocondensed-bold.ttf");

            UI.Label(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -155", oMax: "-5 -125", text: "Work without fuel", align: TextAnchor.MiddleLeft);
            UI.Button(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: "5 -155", oMax: "-5 -125", text: setupOven.EnableNoFuel ? "<color=green>ON</color>" : "<color=red>OFF</color>", command: $"UI_SF CHANGECONFIG NOFUEL {permName} {prefab}", align: TextAnchor.MiddleRight, font: "robotocondensed-bold.ttf");

            UI.Button(ref container, ".setup.bg", command: $"UI_SF BACKTOPREFABS {permName}", aMin: "0 1", aMax: "0 1", oMin: "-30 -30", bgColor: "0.1829 0.3897 0.5338 1", sprite: "assets/icons/enter.png");

            UI.Create(player, container);
        }

        private void ShowUIOvenPrefabs(BasePlayer player, string permName)
        {
            var container = new CuiElementContainer();

            var ovens = _config.PlayersOvenSetup[permName];

            UI.Panel(ref container, ".bg", ".setup.bg", ".setup.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: $"-150 -{ovens.Count * 15}", oMax: $"150 {ovens.Count * 15}", material: "assets/content/ui/binocular_overlay.mat");

            var posY = -30;
            foreach (var check in ovens)
            {
                UI.Button(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: $"0 {posY}", oMax: $"0 {posY + 30}", text: _config.OvenPrefabs[check.Key], command: $"UI_SF SELECT OVEN {permName} {check.Key}");

                posY -= 30;
            }

            UI.Button(ref container, ".setup.bg", command: $"UI_SF BACKTOPERMS", aMin: "0 1", aMax: "0 1", oMin: "-30 -30", bgColor: "0.1829 0.3897 0.5338 1", sprite: "assets/icons/enter.png");

            UI.Create(player, container);
        }

        private void ShowUISetup(BasePlayer player)
        {
            var container = new CuiElementContainer();

            UI.MainParent(ref container, aMin: "0 0", aMax: "1 1");

            UI.Panel(ref container, ".bg", bgColor: "0 0 0 0.95", material: "assets/content/ui/uibackgroundblur-ingamemenu.mat");

            UI.Button(ref container, ".bg", ".bg");

            UI.Panel(ref container, ".bg", ".setup.bg", aMin: "0.5 0.5", aMax: "0.5 0.5", oMin: $"-150 -{_config.PlayersOvenSetup.Count * 15}", oMax: $"150 {_config.PlayersOvenSetup.Count * 15}", material: "assets/content/ui/binocular_overlay.mat");

            var posY = -30;
            foreach (var check in _config.PlayersOvenSetup)
            {
                UI.Button(ref container, ".setup.bg", aMin: "0 1", aMax: "1 1", oMin: $"0 {posY}", oMax: $"0 {posY + 30}", text: check.Key, command: $"UI_SF SELECT PERM {check.Key}");

                posY -= 30;
            }

            UI.Create(player, container);
        }

        #endregion  

        #region Language

        private void SendMessage(BasePlayer player, string msg, params object[] args) => Player.Message(player, GetMsg(player.UserIDString, msg, args));

        private string GetMsg(string player, string msg, params object[] args) => string.Format(lang.GetMessage(msg, this, player), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [""] = ""
            }, this);

        }

        #endregion

        #region Helpers



        #endregion

        #region Classes

        private class Configuration
        {
            [JsonProperty("Avaliable entities [shortprefabname - name]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> OvenPrefabs = new()
            {
                ["furnace"] = "Furnace",
                ["furnace.large"] = "Large Furnace",
                ["electricfurnace.deployed"] = "Electric Furnace",
                ["refinery_small_deployed"] = "Refinery",
                ["campfire"] = "Campfire",
                ["bbq.deployed"] = "Barbeque",
                ["hobobarrel.deployed"] = "Hobobarrel",
                ["fireplace.deployed"] = "Fireplace",
            };

            [JsonProperty(PropertyName = "Oven setup for players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, Dictionary<string, SetupOven>> PlayersOvenSetup = new()
            {
                ["setupfurnaces.default"] = new Dictionary<string, SetupOven>
                {
                    ["furnace"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["furnace.large"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["electricfurnace.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["refinery_small_deployed"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["campfire"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["bbq.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["hobobarrel.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["fireplace.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 1,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    }
                },
                ["setupfurnaces.vip"] = new Dictionary<string, SetupOven>
                {
                    ["furnace"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["furnace.large"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["electricfurnace.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["refinery_small_deployed"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["campfire"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["bbq.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["hobobarrel.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    },
                    ["fireplace.deployed"] = new()
                    {
                        QuickSmeltMultiplier = 4,
                        OutputMultiplier = 1,
                        ConsumeMultiplier = 1,
                        EnableReorganize = true,
                        EnableNoFuel = true,
                    }
                },
            };
        }


        public class SetupOven
        {
            [JsonIgnore] public ulong OwnerID = 0;

            [JsonProperty(PropertyName = "Quick smelt multiplier")]
            public float QuickSmeltMultiplier = 1;

            [JsonProperty(PropertyName = "Output multiplier")]
            public int OutputMultiplier = 1;

            [JsonProperty("Fuel consume multiplier")]
            public float ConsumeMultiplier = 1;

            [JsonProperty(PropertyName = "Reorganize items")]
            public bool EnableReorganize = false;

            [JsonProperty(PropertyName = "Cook without fuel")]
            public bool EnableNoFuel = false;
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

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();



        #endregion

        #region GUI

        private class UI
        {
            private const string Layer = "UI_SetupFurnaces";

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

                var padding = paddings.Split(" ");
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

                var padding = paddings.Split(" ");
                Icon(ref container,
                    name + ".bg", name,
                    oMin: $"{oMin.Split(" ")[0].ToInt() + padding[3].ToInt()} {oMin.Split(" ")[1].ToInt() + padding[2].ToInt()}",
                    oMax: $"{oMax.Split(" ")[0].ToInt() - padding[1].ToInt()} {oMax.Split(" ")[1].ToInt() + padding[0].ToInt()}",
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