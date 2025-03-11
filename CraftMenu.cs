using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Network;
using VLB;

namespace Oxide.Plugins
{
    [Info("CraftMenu", "rustmods.ru", "1.1.6")]
    [Description("Simple craft menu for uncraftable items.")]
    public class CraftMenu : RustPlugin
    {
        int _bpsTotal = 0;
        int _bpsDefault = 0;
        string click = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";
        string pageChange = "assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab";
        string research = "assets/prefabs/deployable/research table/effects/research-success.prefab";
        string craft = "assets/bundled/prefabs/fx/notice/loot.start.fx.prefab";
        string[] db;
        string[] f;
        string[] ca;
        string[] ba;
        string[] ia = {
            "0.22 0.17", "0.78 0.83",
            "0.22 0.17", "0.78 0.83",
            "0.24 0.14", "0.76 0.82",
            "0.24 0.14", "0.76 0.82",
            "0.24 0.14", "0.76 0.82"
        };



        private void OnServerInitialized()
        {
            LoadConfig();
            LoadData();
            LoadPlayerData();
            LoadNamesData();
            DownloadImages();
            ImageQueCheck();

            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "server.uselegacyworkbenchinteraction true"); 

            if (config.main.perms == false)
            {
                config.main.perms = false;
                SaveConfig();
            }

            if (config.ct == null)
            {
                Puts($"\n*******************************************************************\nConfig update is required. Make backup of your old one, delete it from config folder and reload plugin again.\n*******************************************************************");
                Interface.Oxide.UnloadPlugin("CraftMenu");
                return;
            }

            RegisterPerms();

            permission.RegisterPermission($"craftmenu.use", this);

            _bpsTotal = bps.Count();
            foreach (string bp in bps.Keys)
            {
                if (bps[bp].ResearchCost == 0)
                    _bpsDefault++;
            }

            AddMonoComponent();

        }

        void Unload()
        {
            foreach (var _player in BasePlayer.activePlayerList)
                DestroyCui(_player);

            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "server.uselegacyworkbenchinteraction false"); 

            DestroyMonoComponent();
        }

        void OnNewSave()
        {
            if (config.main.wipe)
            {
                PrintWarning("New save detected, Wiping Player Blueprints....");

                playerBps = new Dictionary<ulong, PlayerBps>();

                PrintWarning("Player Blueprints Wiped.");
            }
            SavePlayerData();
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (!config.ct.craftQue) return;
            var run = player.GetComponent<CraftingQue>();
            if (run == null)
                return;
            else
                run.CancelAll(player);

            UnityEngine.Object.Destroy(run);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.ct.craftQue)
                player.gameObject.GetOrAddComponent<CraftingQue>();
        }


        void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            if (!config.ct.craftQue) return;

            var run = player.GetComponent<CraftingQue>();
            if (run == null)
                return;
            else
                run.CancelAll(player);
        }

        private void OnLootEntity(BasePlayer player, Workbench bench)
        {

            if (!permission.UserHasPermission(player.UserIDString, "craftmenu.use")) return;

            if (!config.ct.craftQue)
                player.gameObject.GetOrAddComponent<CraftingQue>();

            if (!playerBps.ContainsKey(player.userID))
            {
                playerBps.Add(player.userID, new PlayerBps());
                SavePlayerData();
            }
            if (bench.PrefabName.Contains("1"))
            {
                CreateBaseCui(player, 1);
                ShowBps(player, 1, "all");
            }
            if (bench.PrefabName.Contains("2"))
            {
                CreateBaseCui(player, 2);
                ShowBps(player, 2, "all");
            }
            if (bench.PrefabName.Contains("3"))
            {
                CreateBaseCui(player, 3);
                ShowBps(player, 3, "all");
            }
        }

        private void OnLootEntityEnd(BasePlayer player, Workbench bench)
        {
            DestroyCui(player);
        }



        private void GetDefaultImages()
        {
            db = "{\"file\":{\"file_id\":\"525\",\"file_name\":\"s\",\"file_image\":{\"name\":\"'5'0'2'93'4'250'573'7'8'0'620'1'0.6'92'0'4'1'39'0'49'2'59'0'8'1'19'7'9'4'1'3'82'0'59'2'4'7'.gif\",\"url\":\"https:\\/\\/codefling.com\\/uploads\\/monthly_2022_02\\/1528951962_50293425057378062010.69204139049259081197941382059247.gif.cafa79ad4e60c4e0ff86b58a358b3fa4.gif\",\"size\":\"2211771\"},\"file_version\":\"3.0.2\",\"file_author\":\"s\",\"file_price\":\"$s\"}}".Split('\'');

            ca = new string[] { $"{db[23]}.{db[19]}{db[12]} {db[10]}", $"{db[19]}.{db[19]}{db[27]}5 {db[17]}", $"{db[33]}.{db[2]}{db[28]} {db[33]}", $"{db[19]}.{db[12]}9 {db[17]}", $"{db[15]}.{db[12]}{db[28]} {db[2]}", $"{db[15]}.{db[3]}{db[28]} {db[30]}", $"{db[19]}.{db[21]}9 {db[2]}", $"{db[23]}.{db[18]} {db[25]}", $"{db[2]}.{db[18]} {db[19]}", $"{db[2]}.{db[20]} {db[25]}", $"{db[23]}.{db[20]} {db[19]}", $"{db[33]}.{db[22]} {db[17]}", $"{db[15]}.{db[22]} {db[2]}", $"{db[13]}{db[28]} {db[30]}" };
            ba = new string[] { $"{db[24]}.{db[27]} {db[18]}", $"{db[15]}.{db[23]} {db[2]}.{db[9]}{db[1]}", $"{db[12]} {db[12]}", $"{db[23]}.{db[2]} {db[13]}{db[28]}", $"{db[30]} {db[2]}.{db[24]}{db[29]}", $"{db[33]}.{db[23]} {db[15]}.{db[1]}{db[31]}", $"{db[12]} {db[13]}{db[9]}", $"{db[15]}.{db[2]} {db[33]}.{db[31]}6", $"{db[17]} {db[2]}.{db[1]}{db[3]}", $"{db[23]}.{db[33]} {db[2]}.{db[3]}", $"{db[30]} {db[2]}.{db[31]}{db[1]}", $"{db[2]}.{db[23]} {db[2]}.{db[2]}{db[5]}", $"{db[17]} {db[23]}.{db[17]}{db[28]}" };
            f = new string[] { "1 1 1 0.6", "1 1 1 0.4" };
        }


        private void RegisterPerms()
        {
            if (config.main.perms)
            {
                foreach (string item in config.main.cat.Keys)
                    permission.RegisterPermission($"craftmenu.{item}", this);
            }
        }

        [ConsoleCommand("craftmenu_admin")]
        private void craftmenu_admin(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (arg.Player() != null)
            {
                if (!player.IsAdmin)
                    return;
            }

            if (args[0] == "wipe")
            {
                if (args.Length > 1)
                {
                    if (!playerBps.ContainsKey(Convert.ToUInt64(args[1])))
                    {
                        Puts($"Player {args[1]} have no blueprints to wipe.");
                        return;
                    }

                    playerBps.Remove(Convert.ToUInt64(args[1]));
                    Puts($"BPs for player {args[1]} wiped.");
                    return;
                }

                Puts($"Blueprints has been wiped.");
                playerBps.Clear();
                SavePlayerData();
                return;
            }
        }

        [ConsoleCommand("craftmenu_cmd")]
        private void craftmenu_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (arg.Player() == null) return;
            if (args == null) return;

            if (args[0] == "select")
            {
                CreateBp(player, Convert.ToInt32(args[1]), args[2], Convert.ToInt32(args[3]), true);
                PlayFx(player, click);
                return;
            }
            if (args[0] == "category")
            {
                ShowBps(player, Convert.ToInt32(args[2]), args[1]);
                PlayFx(player, pageChange);
                return;
            }
            if (args[0] == "pageup")
            {
                if (Convert.ToInt32(args[3]) < 0) return;

                ShowBps(player, Convert.ToInt32(args[2]), args[1], Convert.ToInt32(args[3]));
                PlayFx(player, pageChange);
                return;
            }
            if (args[0] == "pagedown")
            {
                ShowBps(player, Convert.ToInt32(args[2]), args[1], Convert.ToInt32(args[3]));
                PlayFx(player, pageChange);
                return;
            }
            if (args[0] == "craft")
            {
                CraftItem(player, Convert.ToInt32(args[1]), args[2], Convert.ToInt32(args[3]));
                return;
            }

            if (args[0] == "research")
            {
                ResearchItem(player, Convert.ToInt32(args[1]), args[2], Convert.ToInt32(args[3]));
                return;
            }
        }

        private List<string> CreateBpOrder(BasePlayer player, int tier, string category)
        {
            List<string> bpOrder = new List<string>();
            List<string> lockedBps = new List<string>();

            foreach (string item in bps.Keys)
            {
                if (category == "all")
                {
                    if (playerBps[player.userID].bp.Contains(item))
                    {
                        if (bps[item].Tier <= tier)
                        {
                            bpOrder.Insert(0, item);
                        }
                        else
                        {
                            bpOrder.Add(item);
                        }
                        continue;
                    }
                    else
                    {
                        lockedBps.Add(item);
                        continue;
                    }
                }
                if (bps[item].Category == category)
                {
                    if (playerBps[player.userID].bp.Contains(item))
                    {
                        if (bps[item].Tier <= tier)
                        {
                            bpOrder.Insert(0, item);
                        }
                        else
                        {
                            bpOrder.Add(item);
                        }
                    }
                    else
                    {
                        lockedBps.Add(item);
                        continue;
                    }
                }
            }

            foreach (string item in lockedBps)
            {
                bpOrder.Add(item);
            }

            bpOrder.Insert(0, "null");

            return bpOrder;
        }

        private bool CheckInv(BasePlayer player, string shortName, int amount)
        {
            var itemDef = ItemManager.FindItemDefinition(shortName);
            if (itemDef == null) return false;

            int invAmount = player.inventory.GetAmount(itemDef.itemid);
            if (invAmount < amount) return false;

            return true;
        }


        bool IsSkinID(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        private int ItemAmount_Skin(ItemContainer container, ulong? skin)
        {
            var items = Facepunch.Pool.Get<List<Item>>();
            items.AddRange(container.itemList.Where(x => x.skin == skin.Value));
            var currentAmount = items.Sum(x => x.amount);

            Facepunch.Pool.FreeUnmanaged(ref items);
            return currentAmount;
        }

        private void TakeItems_Skin(int amount, ItemContainer container, ulong? skin)
        {
            if (amount == 0 || skin == 0) return;

            var items = Facepunch.Pool.Get<List<Item>>();
            items.AddRange(container.itemList.Where(x => x.skin == skin.Value));

            var amountLeft = amount;
            foreach (var item in items)
            {
                if (amountLeft <= 0) break;

                var oldItemAmount = item.amount;

                if (amountLeft >= item.amount) item.Remove();
                else item.amount -= amountLeft;

                amountLeft -= oldItemAmount;
            }
            Facepunch.Pool.FreeUnmanaged(ref items);
        }

        private bool _CanCraft(BasePlayer player, string shortName)
        {
            foreach (string item in bps[shortName].Resources.Keys)
            {
                if (IsSkinID(item))
                {

                    if (_GetAmount(player, Convert.ToUInt64(item)) < bps[shortName].Resources[item])
                        return false;

                    /* if (ItemAmount_Skin(player.inventory.containerBelt, Convert.ToUInt64(item)) +
                    ItemAmount_Skin(player.inventory.containerMain, Convert.ToUInt64(item)) < bps[shortName].Resources[item]) 
                        return false; */
                }
                else
                {
                    var itemDef = ItemManager.FindItemDefinition(item);
                    int invAmount = player.inventory.GetAmount(itemDef.itemid);
                    if (invAmount < bps[shortName].Resources[item])
                        return false;
                }
            }
            return true;
        }

        private void CraftItem(BasePlayer player, int tier, string shortName, int index)
        {
            string _shortname = shortName;
            if (shortName.Contains("{"))
            {
                int charsToRemove = shortName.Length - shortName.IndexOf("{");
                _shortname = shortName.Remove(shortName.Length - charsToRemove);
            }
            if (shortName.Contains("militaryflamethrower"))
            {
                _shortname = shortName.Replace("militaryflamethrower", "military flamethrower"); 
            }

            if (!_CanCraft(player, shortName))
            {

                CreateBp(player, tier, shortName, index, true);
                PlayFx(player, click);
                return;
            }
            else
            {
                foreach (string _item in bps[shortName].Resources.Keys)
                {
                    if (IsSkinID(_item))
                    {
                        /* ulong skin = Convert.ToUInt64(_item);
                        int amount = bps[shortName].Resources[_item]; */
                        /* int belt = ItemAmount_Skin(player.inventory.containerBelt, skin);
                        int main = ItemAmount_Skin(player.inventory.containerMain, skin); */

                        if (!TakeFromInventory(player, bps[shortName].Resources[_item], Convert.ToUInt64(_item)))
                            return;

                        /* if (belt + main >= amount)
                        {
                            if (main >= amount)
                            {
                                main = amount;
                                TakeItems_Skin(main, player.inventory.containerMain, skin);
                                return;
                            }

                            TakeItems_Skin(main, player.inventory.containerMain, skin);
                            amount -= main;
                            TakeItems_Skin(amount, player.inventory.containerBelt, skin);
                        } */
                    }
                    else
                    {
                        var itemDef = ItemManager.FindItemDefinition(_item);
                        if (itemDef == null)
                        {
                            SendReply(player, $" <color=#C2291D>!</color> '{_item}' <color=#C2291D>is not correct shortname.</color>");
                            return;
                        }
                        player.inventory.Take(null, itemDef.itemid, bps[shortName].Resources[_item]);
                    }
                }

                var item = ItemManager.CreateByName(_shortname, 1, bps[shortName].SkinID);
                if (item != null)
                {

                    if (config.ct.craftQue)
                    {
                        var run = player.GetComponent<CraftingQue>();
                        if (run != null)
                            run.AddToQue(player, shortName);
                        else
                            RefundItem(player, shortName);

                        PlayFx(player, craft);
                    }
                    else
                    {
                        item.name = bps[shortName].Name;
                        player.GiveItem(item);
                        CreateBp(player, tier, shortName, index, true);
                        PlayFx(player, craft);
                        return;
                    }
                }
                else
                {
                    SendReply(player, $" <color=#C2291D>!</color> '{item}' <color=#C2291D>is not correct shortname.</color>");
                    return;
                }
            }
        }

        private void ResearchItem(BasePlayer player, int tier, string shortName, int index)
        {
            if (!CheckInv(player, "scrap", bps[shortName].ResearchCost))
            {
                CreateBp(player, tier, shortName, index, true);
                PlayFx(player, click);
                return;
            }
            else
            {
                var itemDef = ItemManager.FindItemDefinition("scrap");
                if (itemDef == null)
                {
                    SendReply(player, $" <color=#C2291D>!</color> Error, please contact developer.</color>");
                    return;
                }
                player.inventory.Take(null, itemDef.itemid, bps[shortName].ResearchCost);
                playerBps[player.userID].bp.Add(shortName);
                SavePlayerData();
                CreateBp(player, tier, shortName, index, true);
                PlayFx(player, research);
            }

        }

        private void RefundItem(BasePlayer player, string shortName)
        {
            foreach (string item in bps[shortName].Resources.Keys)
            {
                var _item = ItemManager.CreateByName($"{item}", bps[shortName].Resources[item]);
                player.GiveItem(_item);
            }
        }

        private void CreateItemMono(BasePlayer player, string shortName)
        {
            string _shortname = shortName;
            if (shortName.Contains("{"))
            {
                int charsToRemove = shortName.Length - shortName.IndexOf("{");
                _shortname = shortName.Remove(shortName.Length - charsToRemove);
            }

            var item = ItemManager.CreateByName(_shortname, 1, bps[shortName].SkinID);
            if (item != null)
            {
                item.name = bps[shortName].Name;
                player.GiveItem(item);
            }

        }

        private bool TakeFromInventory(BasePlayer player, int amount, ulong skinid)
        {
            if (player == null)
                return false;

            if (_GetAmount(player, skinid) < amount)
                return false;

            if (RemoveItem(player, amount, skinid))
                return true;

            return false;
        }

        private int _GetAmount(BasePlayer player, ulong skin)
        {
            var items = Facepunch.Pool.Get<List<Item>>();

            items.AddRange(player.inventory.containerBelt.itemList
                .Where(item => item.skin == skin));
            items.AddRange(player.inventory.containerMain.itemList
                .Where(item => item.skin == skin));

            var currentAmount = items.Sum(x => x.amount);

            Facepunch.Pool.FreeUnmanaged(ref items);
            return currentAmount;
        }

        private bool RemoveItem(BasePlayer player, int amount, ulong? skin)
        {
            if (amount == 0) return false;

            var items = Facepunch.Pool.Get<List<Item>>();
            try
            {

                items.AddRange(player.inventory.containerBelt.itemList
                    .Where(item => item.skin == skin));

                items.AddRange(player.inventory.containerMain.itemList
                    .Where(item => item.skin == skin));

                var amountLeft = amount;
                foreach (var item in items)
                {
                    if (amountLeft <= 0) break;

                    item.MarkDirty();
                    var oldItemAmount = item.amount;

                    if (amountLeft >= item.amount)
                    {
                        item.MarkDirty();
                        item.Remove();
                    }
                    else item.amount -= amountLeft;

                    amountLeft -= oldItemAmount;
                }
            }
            catch
            {
                SendReply(player, "Something failed while taking items, please let server admin know about it.");
                Puts($"Something failed while taking items from {player} -> (shortname: null/amount:{amount}/skin:{skin})");
                return false;
            }

            Facepunch.Pool.FreeUnmanaged(ref items);
            return true;
        }

        [ConsoleCommand("craftmenu_cancel")]
        private void craftmenu_cancel(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (arg.Player() == null) return;
            var args = arg.Args;
            if (args[0] == null) return;
            int index = Convert.ToInt32(args[0]);

            var run = player.GetComponent<CraftingQue>();
            if (run == null) player.gameObject.GetOrAddComponent<CraftingQue>();
            if (run != null)
            {
                run.CancelCraft(player, index);
            }
        }

        [ConsoleCommand("craftmenu_addtoque")]
        private void craftmenu_addtoque(ConsoleSystem.Arg arg)
        {

            var player = arg?.Player();
            if (arg.Player() == null) return;
            if (!player.IsAdmin) return;
            var args = arg.Args;
            if (args[0] == null) return;

            var run = player.GetComponent<CraftingQue>();
            if (run == null) player.gameObject.GetOrAddComponent<CraftingQue>();
            if (run != null) run.AddToQue(player, args[0]);

        }


        [ConsoleCommand("craftmenu_openquepanel")]
        private void craftmenu_openquepanel(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (arg.Player() == null) return;

            var run = player.GetComponent<CraftingQue>();
            if (run == null) player.gameObject.GetOrAddComponent<CraftingQue>();
            if (run != null) run.OpenQuePanel(player);
        }

        private void PlayFx(BasePlayer player, string fx)
        {
            if (!config.main.fx) return;
            if (player == null) return;
            var EffectInstance = new Effect();
            EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
            EffectInstance.pooledstringid = StringPool.Get(fx);
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.Effect);
            EffectInstance.WriteToStream(netWrite);
            netWrite.Send(new SendInfo(player.net.connection));
            EffectInstance.Clear();
        }


        static CraftMenu plugin;

        private void Init() => plugin = this;

        private void AddMonoComponent()
        {
            foreach (var _player in BasePlayer.activePlayerList)
                _player.gameObject.GetOrAddComponent<CraftingQue>();
        }

        private void DestroyMonoComponent()
        {
            foreach (var _player in BasePlayer.activePlayerList)
            {
                var run = _player.GetComponent<CraftingQue>();
                if (run == null)
                    return;
                else
                    run.CancelAll(_player);


                UnityEngine.Object.Destroy(run);
            }
        }

        private class CraftingQue : MonoBehaviour
        {
            BasePlayer player;
            List<string> craftOrder = new List<string>();
            bool craftPanelOpen = false;
            int progress;

            void Awake() => player = GetComponent<BasePlayer>();

            public void CancelAll(BasePlayer player)
            {
                if (craftOrder.Count != 0)
                {
                    foreach (string item in craftOrder)
                    {
                        plugin.RefundItem(player, item);
                    }
                    CuiHelper.DestroyUi(player, "ql_base");
                    CancelInvoke(nameof(CraftProgress));
                }
                craftOrder.Clear();
            }

            public void OpenQuePanel(BasePlayer player)
            {
                if (!craftPanelOpen)
                {
                    plugin.CreateQuePanel(player, craftOrder);
                    craftPanelOpen = true;
                }
                else
                {
                    CuiHelper.DestroyUi(player, "qPanel_panel");
                    craftPanelOpen = false;
                }
            }

            public void CancelCraft(BasePlayer player, int index)
            {
                plugin.RefundItem(player, craftOrder[index]);
                craftOrder.RemoveAt(index);
                plugin.CreateQueButton(player, craftOrder.Count);
                if (craftOrder.Count <= 0)
                {
                    CuiHelper.DestroyUi(player, "ql_base");
                    CancelInvoke(nameof(CraftProgress));
                }
                else
                {
                    if (craftPanelOpen)
                        plugin.CreateQuePanel(player, craftOrder);

                    if (craftOrder.Count <= 1)
                    {
                        CuiHelper.DestroyUi(player, "qPanel_panel");
                    }


                    if (index == 0)
                    {
                        CancelInvoke(nameof(CraftProgress));

                        if (plugin.config.ct.excp.ContainsKey(craftOrder[0]))
                            progress = plugin.config.ct.excp[craftOrder[0]];
                        else
                            progress = plugin.config.ct.defaultTime;

                        InvokeRepeating(nameof(CraftProgress), 1f, 1f);
                    }
                }

            }

            void CraftProgress()
            {

                progress -= 1;
                plugin.CreateTimer(player, craftOrder[0], progress);

                if (progress <= 0)
                {
                    plugin.CreateItemMono(player, craftOrder[0]);
                    craftOrder.RemoveAt(0);
                    int itemsLeft = craftOrder.Count();

                    if (itemsLeft <= 1)
                    {
                        CuiHelper.DestroyUi(player, "ql_base_quetext_btn");
                        CuiHelper.DestroyUi(player, "qPanel_panel");
                    }
                    else
                    {
                        plugin.CreateQueButton(player, itemsLeft);
                        if (craftPanelOpen)
                            plugin.CreateQuePanel(player, craftOrder);
                    }

                    if (itemsLeft <= 0)
                    {
                        CancelInvoke(nameof(CraftProgress));
                        CuiHelper.DestroyUi(player, "ql_base");
                    }
                    else
                    {
                        CuiHelper.DestroyUi(player, "qTimer");
                        CuiHelper.DestroyUi(player, "qTimerName");
                        if (plugin.config.ct.excp.ContainsKey(craftOrder[0]))
                            progress = plugin.config.ct.excp[craftOrder[0]];
                        else
                            progress = plugin.config.ct.defaultTime;
                    }

                }
            }

            public void AddToQue(BasePlayer player, string itemName)
            {
                if (player == null) return;


                if (craftOrder == null)
                {
                    //s
                }
                craftOrder.Add(itemName);

                if (IsInvoking(nameof(CraftProgress)) == false)
                {
                    if (plugin.config.ct.excp.ContainsKey(craftOrder[0]))
                        progress = plugin.config.ct.excp[craftOrder[0]];
                    else
                        progress = plugin.config.ct.defaultTime;
                    InvokeRepeating(nameof(CraftProgress), 0.1f, 1f);


                    plugin.CreateCraftQueLayout(player);
                }
                if (craftOrder.Count > 1)
                {
                    plugin.CreateQueButton(player, craftOrder.Count);

                    if (craftPanelOpen)
                        plugin.CreateQuePanel(player, craftOrder);
                }

            }

        }


        private void CreateBaseCui(BasePlayer player, int tier = 1)
        {
            var _baseCraftCui = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat

            CUIClass.CreatePanel(ref _baseCraftCui, "baseCraft_main", "Overlay", "0 0 0 0", $"{db[2]}.{db[1]} 0.{db[23]}", $"{db[10]}.{db[1]} 0.{db[10]}", false, 0.1f, 0f, "assets/icons/iconmaterial.mat", $"{db[12]}{db[14]} {db[6]}", $"{db[7]} {db[11]}");

            CUIClass.CreateText(ref _baseCraftCui, "baseCraft_title_text", "baseCraft_main", f[0], $"<size=21><b>BLUEPRINTS</b></size>", 12, "0.00 1", "1 1.2", TextAnchor.LowerLeft, $"robotocondensed-regular.ttf", 0.1f);
            CUIClass.CreateText(ref _baseCraftCui, "baseCraft_title_count", "baseCraft_main", f[0], $"UNLOCKED {playerBps[player.userID].bp.Count() + _bpsDefault}/{_bpsTotal}", 12, "0.00 1.01", "0.99 1.2", TextAnchor.LowerRight, $"robotocondensed-regular.ttf", 0.1f);

            CUIClass.CreatePanel(ref _baseCraftCui, "baseCraft_category_panel", "baseCraft_main", "0.70 0.67 0.65 0.17", "0.0 0.93", "1 1", true, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");

            CUIClass.CreateButton(ref _baseCraftCui, "baseCraft_category_btnAll", "baseCraft_category_panel", "0.70 0.67 0.65 0.0", "", 11, ca[0], ca[1], $"craftmenu_cmd category all {tier}", "", "1 1 1 0.7", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
            CUIClass.PullFromAssets(ref _baseCraftCui, "bc_ci_all", "baseCraft_category_btnAll", "1 1 1 0.5", "assets/icons/community_servers.png", 0.1f, 0f, "0.1 0.15", "0.96 0.83");

            int index = 1;
            foreach (string category in config.main.cat.Keys)
            {
                int a1 = 0 + index;
                int a2 = 1 + index;
                if (index == 1) { a1 = 2; a2 = 3; }
                if (index == 2) { a1 = 4; a2 = 5; }
                if (index == 3) { a1 = 6; a2 = 7; }
                if (index == 4) { a1 = 8; a2 = 9; }
                if (index == 5) { a1 = 10; a2 = 11; }
                if (index == 6) { a1 = 12; a2 = 13; }

                CUIClass.CreateButton(ref _baseCraftCui, $"baseCraft_category_btn{index}", "baseCraft_category_panel", "0.70 0.67 0.65 0.0", "", 11, ca[a1], ca[a2], $"craftmenu_cmd category {category} {tier}", "", "1 1 1 0.7", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

                if (config.main.cat[category].StartsWith("assets"))
                {
                    int b1 = 0; int b2 = 1;
                    if (config.main.cat[category] == "assets/icons/construction.png") { b1 = 2; b2 = 3; }
                    if (config.main.cat[category] == "assets/icons/clothing.png") { b1 = 4; b2 = 5; }
                    if (config.main.cat[category] == "assets/icons/bullet.png") { b1 = 6; b2 = 7; }
                    if (config.main.cat[category] == "assets/icons/medical.png") { b1 = 8; b2 = 9; }

                    CUIClass.PullFromAssets(ref _baseCraftCui, $"category_btn_asset{index}", $"baseCraft_category_btn{index}", f[1], config.main.cat[category], 0.1f, 0f, ia[b1], ia[b2]);
                }
                else
                    CUIClass.CreateImage(ref _baseCraftCui, $"category_btn_asset{index}", $"baseCraft_category_btn{index}", Img($"{config.main.cat[category]}"), "0 0", "1 1", 0.1f);

                index++;
            }

            CUIClass.CreatePanel(ref _baseCraftCui, "baseCraft_blueprints_panel", "baseCraft_main", "0.70 0.67 0.65 0.0", "0.0 0.0", "1 0.92", false, 0.1f, 0f, "assets/icons/iconmaterial.mat");

            DestroyCui(player);
            CuiHelper.AddUi(player, _baseCraftCui);
            CuiHelper.DestroyUi(player, "empty");
        }

        private void DestroyCui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "empty");
            CuiHelper.DestroyUi(player, "baseCraft_main");
        }

        private void CreatePageBtns(BasePlayer player, string category, int tier, int currentPage)
        {
            var _pageBtns = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            int pageup = currentPage - 1;
            int pagedown = 1 + currentPage;

            CUIClass.CreateButton(ref _pageBtns, "craft_page_up", "baseCraft_category_panel", "0.80 0.25 0.16 0.0", "▲", 11, "0.86 0.16", $"0.89 0.84", $"craftmenu_cmd pageup {category} {tier} {pageup}", "", f[1], 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
            CUIClass.CreateButton(ref _pageBtns, "craft_page_down", "baseCraft_category_panel", "0.80 0.25 0.16 0.0", "▼", 11, "0.89 0.16", $"0.94 0.84", $"craftmenu_cmd pageup {category} {tier} {pagedown}", "", f[1], 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
            CUIClass.CreateText(ref _pageBtns, "bp_page_count", "baseCraft_category_panel", f[1], $"{currentPage + 1}", 11, "0.94 0.0", "0.98 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", 0.1f);

            CuiHelper.DestroyUi(player, "craft_page_up");
            CuiHelper.DestroyUi(player, "craft_page_down");
            CuiHelper.DestroyUi(player, "bp_page_count");
            CuiHelper.AddUi(player, _pageBtns);
            CuiHelper.DestroyUi(player, "empty");
        }

        private void ShowBps(BasePlayer player, int tier, string category, int page = 0)
        {
            List<string> bpOrder = CreateBpOrder(player, tier, category);
            int index = 6 * page;

            for (int i = 1; i < 7; i++)
                CuiHelper.DestroyUi(player, $"baseCraft_bp_{i}");

            int totalItems = bpOrder.Count() - 1;

            for (int i = 1; i < 7; i++)
            {
                if (totalItems < index + i)
                    break;

                CreateBp(player, tier, bpOrder[index + i], i);
            }

            if (bpOrder.Count() > 7)
                CreatePageBtns(player, category, tier, page);
            else
            {
                CuiHelper.DestroyUi(player, "craft_page_up");
                CuiHelper.DestroyUi(player, "craft_page_down");
                CuiHelper.DestroyUi(player, "bp_page_count");
            }
        }

        private void CreateBp(BasePlayer player, int tier, string shortName, int index, bool selected = false)
        {
            string bpUi = $"baseCraft_bp_{index}";
            string anchorMin = ba[1];
            string anchorMax = ba[2];
            if (index == 2) { anchorMin = ba[3]; anchorMax = ba[4]; }
            if (index == 3) { anchorMin = ba[5]; anchorMax = ba[6]; }
            if (index == 4) { anchorMin = ba[7]; anchorMax = ba[8]; }
            if (index == 5) { anchorMin = ba[9]; anchorMax = ba[10]; }
            if (index == 6) { anchorMin = ba[11]; anchorMax = ba[12]; }

            string img = $"{bps[shortName].Image}";
            if (!img.StartsWith("http"))
                img = "https://rustexplore.com/images/130/" + img;

            string resource = "";
            foreach (string item in bps[shortName].Resources.Keys)
            {

                string itemDisplayName = "";
                var itemDef = ItemManager.FindItemDefinition(item);
                if (itemDef != null)
                {
                    itemDisplayName = itemDef.displayName.translated;
                }

                if (nameReplace.ContainsKey(item))
                    itemDisplayName = nameReplace[item];


                if (IsSkinID(item))
                {
                    if (ItemAmount_Skin(player.inventory.containerBelt, Convert.ToUInt64(item)) +
                    ItemAmount_Skin(player.inventory.containerMain, Convert.ToUInt64(item)) < bps[shortName].Resources[item])
                        resource = resource + $"<color=#d0b255>{bps[shortName].Resources[item]} {itemDisplayName}</color>, ";
                    else
                        resource = resource + $"{bps[shortName].Resources[item]} {itemDisplayName}, ";
                }
                else
                {
                    if (CheckInv(player, item, bps[shortName].Resources[item]))
                        resource = resource + $"{bps[shortName].Resources[item]} {itemDisplayName}, ";
                    else
                        resource = resource + $"<color=#d0b255>{bps[shortName].Resources[item]} {itemDisplayName}</color>, ";
                }
            }
            resource = resource.Remove(resource.Length - 2);

            var _createBps = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            if (selected)
            {
                if (bps[shortName].Tier > tier)
                {
                    if (config.main.perms && !permission.UserHasPermission(player.UserIDString, $"craftmenu.{bps[shortName].Category}"))
                    {
                        CUIClass.CreateText(ref _createBps, "selected_btn", bpUi, "1 1 1 0.25", $"<size=10>YOU CAN'T CRAFT THIS ITEM</size>", 12, "0.8 0.19", $"0.98 0.83", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", 0.1f);
                    }
                    else
                    {
                        CUIClass.CreateText(ref _createBps, "selected_btn", bpUi, "1 1 1 0.25", $"<b><size=15>TIER {bps[shortName].Tier}</size></b>\nREQUIRED", 12, "0.8 0.19", $"0.98 0.83", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", 0.1f);
                    }
                }
                else
                {


                    if (bps[shortName].ResearchCost != 0)
                    {
                        if (playerBps[player.userID].bp.Contains(shortName))
                        {
                            if (_CanCraft(player, shortName))
                            {

                                CUIClass.CreateButton(ref _createBps, "selected_btn", bpUi, "0.38 0.51 0.16 0.85", "     CRAFT", 11, "0.8 0.19", $"0.98 0.83", $"craftmenu_cmd craft {tier} {shortName} {index}", "", f[0], 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                                CUIClass.PullFromAssets(ref _createBps, "bp_craftBtn_icon", "selected_btn", "1 1 1 0.65", "assets/icons/tools.png", 0.1f, 0f, "0.14 0.34", "0.32 0.67");
                            }
                            else
                            {
                                CUIClass.CreateButton(ref _createBps, "selected_btn", bpUi, "0.70 0.67 0.65 0.17", "NOT ENOUGH\nRESOURCES", 11, "0.8 0.19", $"0.98 0.83", $"", "", f[1], 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                            }
                        }
                        else
                        {
                            if (config.main.perms && !permission.UserHasPermission(player.UserIDString, $"craftmenu.{bps[shortName].Category}"))
                            {
                                CUIClass.CreateText(ref _createBps, "selected_btn", bpUi, "1 1 1 0.25", $"<size=10>YOU CAN'T CRAFT THIS ITEM</size>", 12, "0.8 0.19", $"0.98 0.83", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", 0.1f);
                            }
                            else
                            {
                                CUIClass.CreateButton(ref _createBps, "selected_btn", bpUi, "0.70 0.67 0.65 0.17", "RESEARCH\n", 11, "0.8 0.19", $"0.98 0.83", $"craftmenu_cmd research {tier} {shortName} {index}", "", f[0], 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/content/ui/uibackgroundblur.mat");
                                CUIClass.CreateImage(ref _createBps, "btn_research_scrapImg", "selected_btn", Img("https://rustexplore.com/images/130/scrap.png"), "0.16 0.12", "0.43 0.48", 0.1f);
                                CUIClass.CreateText(ref _createBps, "btn_research_cost", "selected_btn", f[1], $"{bps[shortName].ResearchCost}", 12, "0.3 0", "0.80 0.5", TextAnchor.UpperRight, $"robotocondensed-bold.ttf", 0.1f);
                            }
                        }
                    }
                    else
                    {
                        if (_CanCraft(player, shortName))
                        {

                            CUIClass.CreateButton(ref _createBps, "selected_btn", bpUi, "0.38 0.51 0.16 0.85", "     CRAFT", 11, "0.8 0.19", $"0.98 0.83", $"craftmenu_cmd craft {tier} {shortName} {index}", "", f[0], 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                            CUIClass.PullFromAssets(ref _createBps, "bp_craftBtn_icon", "selected_btn", "1 1 1 0.65", "assets/icons/tools.png", 0.1f, 0f, "0.14 0.34", "0.32 0.67");
                        }
                        else
                        {
                            CUIClass.CreateButton(ref _createBps, "selected_btn", bpUi, "0.70 0.67 0.65 0.17", "NOT ENOUGH\nRESOURCES", 11, "0.8 0.19", $"0.98 0.83", $"", "", f[1], 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                        }
                    }
                }
            }
            else
            {//BASE
                CUIClass.CreatePanel(ref _createBps, bpUi, "baseCraft_blueprints_panel", $"0.{db[27]}0 0.6{db[8]} {db[13]}5 {db[23]}.{db[2]}{db[8]}", anchorMin, anchorMax, false, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
                //data
                CUIClass.CreateImage(ref _createBps, "bp_image", bpUi, Img($"{img}"), "0.02 0.12", "0.14 0.88", 0.1f);
                CUIClass.CreateText(ref _createBps, "bp_name", bpUi, f[1], $"{bps[shortName].Name}", 17, $"0.{db[30]}7 0.{db[16]}{db[1]}", "0.80 0.88", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.1f);
                CUIClass.CreateText(ref _createBps, "bp_resource", bpUi, f[1], resource, 11, $"{db[15]}.{db[25]}{db[27]} -{db[2]}.{db[17]}4", "0.80 0.46", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", 0.1f);

                if (bps[shortName].ResearchCost != 0)
                {//if needs to be reseached
                    if (playerBps[player.userID].bp.Contains(shortName))
                    {//bp available
                        CUIClass.PullFromAssets(ref _createBps, "bp_available", bpUi, "1 1 1 0.18", "assets/icons/check.png", 0.1f, 0f, "0.85 0.22", "0.945 0.87");
                    }
                    else
                    {//locked
                        CUIClass.PullFromAssets(ref _createBps, "bp_lock", bpUi, "1 1 1 0.18", "assets/icons/bp-lock.png", 0.1f, 0f, "0.85 0.22", "0.945 0.87");
                    }
                }
                else
                {//bp available
                    CUIClass.PullFromAssets(ref _createBps, "bp_available", bpUi, "1 1 1 0.18", "assets/icons/check.png", 0.1f, 0f, "0.85 0.22", "0.945 0.87");
                }

                //select btn
                CUIClass.CreateButton(ref _createBps, "select_btn", bpUi, "0 0 0 0", "", 11, "0 0", $"1 1", $"craftmenu_cmd select {tier} {shortName} {index}", "", f[1], 1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
            }

            if (!selected)
                CuiHelper.DestroyUi(player, bpUi);
            else
                CuiHelper.DestroyUi(player, "selected_btn");

            CuiHelper.AddUi(player, _createBps);
            CuiHelper.DestroyUi(player, "empty");
        }

        private void DestroyBps(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "empty");
            CuiHelper.DestroyUi(player, "baseCraft_main");
        }

        string[] anchors = {
            "index",
            "0.86 0.0-0.98 1",
            "0.72 0.0-0.84 1",
            "0.58 0.0-0.70 1",
            "0.44 0.0-0.56 1",
            "0.30 0.0-0.42 1",
            "0.16 0.0-0.28 1",
            "0.02 0.0-0.14 1",

        };

        private void CreateCraftQueLayout(BasePlayer player, string itemName = "default")
        {

            var qL = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat

            CUIClass.CreatePanel(ref qL, "ql_base", "Overlay", "0.10 0.40 0.60 1", "1 0.0", "1 0.0", false, 0.3f, 0f, "assets/content/ui/uibackgroundblur.mat", "-398 16", "-220 43");
            CUIClass.PullFromAssets(ref qL, "ql_base_gearicon", "ql_base", "0.20 0.6 0.8 1", "assets/icons/gear.png", 0.3f, 0f, "0.02 0.25", "0.11 0.75");

            CUIClass.CreateButton(ref qL, "ql_base_btn_cancel_current", "ql_base", "0.70 0.67 0.65 0.17", "", 11, "-0.15 0.02", $"-0.01 0.98", $"craftmenu_cancel 0", "", "1 1 1 0.7", 1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
            CUIClass.PullFromAssets(ref qL, "ql_base_crossicon1", "ql_base_btn_cancel_current", "1 1 1 0.45", "assets/icons/vote_down.png", 0.1f, 0f, "0.0 0", "1 1");

            CuiHelper.DestroyUi(player, "empty");
            CuiHelper.DestroyUi(player, "ql_base");
            CuiHelper.AddUi(player, qL);
            CuiHelper.DestroyUi(player, "empty");
        }

        private void CreateTimer(BasePlayer player, string itemName, int seconds)
        {
            string displayName = bps[itemName].Name;
            if (displayName.Length > 16)
            {
                displayName = displayName.Remove(16);
                displayName += ".";
            }
            string s = "king";
            string result = s.Remove(s.Length - 1);
            var qTimer = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            CUIClass.CreateText(ref qTimer, "qTimer", "ql_base", f[0], $"{seconds}s", 13, "0.13 0.00", "0.96 1", TextAnchor.MiddleRight, $"robotocondensed-regular.ttf", 0.0f);
            CUIClass.CreateText(ref qTimer, "qTimerName", "ql_base", "1 1 1 0.8", $"{displayName.ToUpper()}", 13, "0.13 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.0f);

            CuiHelper.DestroyUi(player, "qTimer");
            CuiHelper.DestroyUi(player, "qTimerName");
            CuiHelper.AddUi(player, qTimer);
            CuiHelper.DestroyUi(player, "empty");
        }

        private void CreateQueButton(BasePlayer player, int count)
        {
            var qBtn = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            if (count > 1)
                CUIClass.CreateButton(ref qBtn, "ql_base_quetext_btn", "ql_base", "0 0 0 0", $"  {count - 1} more items in queue, click to cancel", 9, "0 -0.4", "1.3 3.0", $"craftmenu_openquepanel", "", "1 1 1 0.9", 0.2f, TextAnchor.LowerLeft, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");

            CuiHelper.DestroyUi(player, "ql_base_quetext_btn");
            CuiHelper.AddUi(player, qBtn);
            CuiHelper.DestroyUi(player, "empty");
        }

        private void CreateQuePanel(BasePlayer player, List<string> craftingQue)
        {
            var qPanel = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            if (craftingQue.Count > 1)
            {
                int forLenght = craftingQue.Count;
                if (forLenght > 8)
                    forLenght = 8;

                CUIClass.CreatePanel(ref qPanel, "qPanel_panel", "ql_base", "0.70 0.67 0.65 0.17", "-0.155 1.1", "1 2.2", false, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
                for (var i = 1; i < forLenght; i++)
                {
                    string img = $"{bps[craftingQue[i]].Image}";
                    if (!img.StartsWith("http"))
                        img = "https://rustexplore.com/images/130/" + img;



                    string[] splitA = anchors[i].Split('-');
                    CUIClass.CreateButton(ref qPanel, $"ql_quebtn{i}", "qPanel_panel", "0 0 0 0", "", 11, splitA[0], splitA[1], $"craftmenu_cancel {i}", "", "1 1 1 0.7", 0.2f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
                    CUIClass.CreateImage(ref qPanel, $"ql_base_que_img{i}", $"ql_quebtn{i}", $"{Img(img)}", "0 0.1", "1 0.9", 0.2f);

                }
            }

            CuiHelper.DestroyUi(player, "qPanel_panel");
            CuiHelper.AddUi(player, qPanel);
            CuiHelper.DestroyUi(player, "empty");
        }


        public class CUIClass
        {
            public static CuiElementContainer CreateOverlay(string _name, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fade = 0f, string _mat = "")
            {
                var _element = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = _color, Material = _mat, FadeIn = _fade},
                            RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                            CursorEnabled = _cursorOn
                        },
                        new CuiElement().Parent = "Overlay",
                        _name
                    }
                };
                return _element;
            }

            public static void CreatePanel(ref CuiElementContainer _container, string _name, string _parent, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fadeIn = 0f, float _fadeOut = 0f, string _mat2 = "", string _OffsetMin = "", string _OffsetMax = "")
            {
                _container.Add(new CuiPanel
                {
                    Image = { Color = _color, Material = _mat2, FadeIn = _fadeIn },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax },
                    FadeOut = _fadeOut,
                    CursorEnabled = _cursorOn
                },
                _parent,
                _name);
            }

            public static void CreateImage(ref CuiElementContainer _container, string _name, string _parent, string _image, string _anchorMin, string _anchorMax, float _fadeIn = 0f, float _fadeOut = 0f, string _OffsetMin = "", string _OffsetMax = "")
            {
                if (_image.StartsWith("http") || _image.StartsWith("www"))
                {
                    _container.Add(new CuiElement
                    {
                        Name = _name,
                        Parent = _parent,
                        FadeOut = _fadeOut,
                        Components =
                        {
                            new CuiRawImageComponent { Url = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fadeIn},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax }
                        }

                    });
                }
                else
                {
                    _container.Add(new CuiElement
                    {
                        Parent = _parent,
                        Components =
                        {
                            new CuiRawImageComponent { Png = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fadeIn},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax }
                        }
                    });
                }
            }

            public static void PullFromAssets(ref CuiElementContainer _container, string _name, string _parent, string _color, string _sprite, float _fadeIn = 0f, float _fadeOut = 0f, string _anchorMin = "0 0", string _anchorMax = "1 1", string _material = "assets/icons/iconmaterial.mat")
            {

                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                            {
                                new CuiImageComponent { Material = _material, Sprite = _sprite, Color = _color, FadeIn = _fadeIn},
                                new CuiRectTransformComponent {AnchorMin = _anchorMin, AnchorMax = _anchorMax}
                            },
                    FadeOut = _fadeOut
                });
            }

            public static void CreateInput(ref CuiElementContainer _container, string _name, string _parent, string _color, int _size, string _anchorMin, string _anchorMax, string _font = "permanentmarker.ttf", string _command = "command.processinput", TextAnchor _align = TextAnchor.MiddleCenter)
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,

                    Components =
                    {
                        new CuiInputFieldComponent
                        {

                            Text = "0",
                            CharsLimit = 250,
                            Color = _color,
                            IsPassword = false,
                            Command = _command,
                            Font = _font,
                            FontSize = _size,
                            Align = _align
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = _anchorMin,
                            AnchorMax = _anchorMax

                        }

                    },
                });
            }

            public static void CreateText(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "robotocondensed-bold.ttf", float _fadeIn = 0f, float _fadeOut = 0f, string _outlineColor = "0 0 0 0", string _outlineScale = "0 0")
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _text,
                            FontSize = _size,
                            Font = _font,
                            Align = _align,
                            Color = _color,
                            FadeIn = _fadeIn,
                        },

                        new CuiOutlineComponent
                        {

                            Color = _outlineColor,
                            Distance = _outlineScale

                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = _anchorMin,
                             AnchorMax = _anchorMax
                        }
                    },
                    FadeOut = _fadeOut
                });
            }

            public static void CreateButton(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, string _command = "", string _close = "", string _textColor = "0.843 0.816 0.78 1", float _fade = 1f, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "", string _material = "assets/content/ui/uibackgroundblur-ingamemenu.mat")
            {

                _container.Add(new CuiButton
                {
                    Button = { Close = _close, Command = _command, Color = _color, Material = _material, FadeIn = _fade },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                    Text = { Text = _text, FontSize = _size, Align = _align, Color = _textColor, Font = _font, FadeIn = _fade }
                },
                _parent,
                _name);
            }
        }


        private void SaveData()
        {
            if (bps != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Blueprints", bps);
        }

        private Dictionary<string, Bps> bps;

        private class Bps
        {
            public string Name;
            public string Image;
            public ulong SkinID;
            public string Category;
            public int Tier;
            public int ResearchCost;
            public Dictionary<string, int> Resources = new Dictionary<string, int> { };
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Blueprints"))
            {
                bps = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Bps>>($"{Name}/Blueprints");
            }
            else
            {
                bps = new Dictionary<string, Bps>();

                CreateExamples();
                SaveData();
            }
        }

        private void CreateExamples()
        {
            bps.Add("multiplegrenadelauncher", new Bps());
            bps["multiplegrenadelauncher"].Name = "Grenade Launcher";
            bps["multiplegrenadelauncher"].Image = "multiplegrenadelauncher.png";
            bps["multiplegrenadelauncher"].SkinID = 0;
            bps["multiplegrenadelauncher"].Category = "weapons";
            bps["multiplegrenadelauncher"].Tier = 3;
            bps["multiplegrenadelauncher"].Resources.Add("metal.fragments", 750);
            bps["multiplegrenadelauncher"].Resources.Add("metalpipe", 6);
            bps["multiplegrenadelauncher"].Resources.Add("metal.refined", 150);

            SaveData();
        }


        private void SavePlayerData()
        {
            if (playerBps != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerBlueprints", playerBps);
        }

        private Dictionary<ulong, PlayerBps> playerBps;

        private class PlayerBps
        {
            public List<string> bp = new List<string> { };
        }

        private void LoadPlayerData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerBlueprints"))
            {
                playerBps = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerBps>>($"{Name}/PlayerBlueprints");
            }
            else
            {
                playerBps = new Dictionary<ulong, PlayerBps>();

                CreatePlayerExamples();
                SavePlayerData();
            }
        }

        private void CreatePlayerExamples()
        {
            playerBps.Add(76561198207548749, new PlayerBps());
            playerBps[76561198207548749].bp.Add("rifle.lr300");
            playerBps[76561198207548749].bp.Add("fun.boomboxportable");
            SavePlayerData();
        }


        private void SaveNamesData()
        {
            if (nameReplace != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/CustomNames_Resources", nameReplace);
        }

        private Dictionary<string, string> nameReplace;



        private void LoadNamesData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/CustomNames_Resources"))
            {
                nameReplace = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>($"{Name}/CustomNames_Resources");
            }
            else
            {
                nameReplace = new Dictionary<string, string>();

                CreateNameReplacements();
                SaveNamesData();
            }
        }

        private void CreateNameReplacements()
        {
            nameReplace.Add("metal.refined", "HQM");
            nameReplace.Add("propanetank", "Propane Tank");
            nameReplace.Add("metalpipe", "Pipes");
            nameReplace.Add("wiretool", "Wires");
        }


        [PluginReference] Plugin ImageLibrary;

        //list for load order
        private List<string> imgList = new List<string>();

        private void DownloadImages()
        {
            if (ImageLibrary == null)
            { Puts($"(! MISSING) ImageLibrary not found, image load speed will be significantly slower."); return; }

            //add to load order
            imgList.Add("https://rustexplore.com/images/130/rifle.lr300.png");
            imgList.Add("https://rustplugins.net/products/craftmenu/blueprint.png");
            imgList.Add("https://rustplugins.net/products/craftmenu/mini.png");
            ImageLibrary.Call("AddImage", "https://rustplugins.net/products/craftmenu/blueprint.png", "https://rustplugins.net/products/craftmenu/blueprint.png");
            ImageLibrary.Call("AddImage", "https://rustexplore.com/images/130/rifle.lr300.png", "https://rustexplore.com/images/130/rifle.lr300.png");

            string prefix = "https://rustexplore.com/images/130/";
            //add item images
            foreach (string item in bps.Keys)
            {
                if (!bps[item].Image.StartsWith("http"))
                {
                    ImageLibrary.Call("AddImage", prefix + bps[item].Image, prefix + bps[item].Image);
                    if (!imgList.Contains(bps[item].Image))
                        imgList.Add(prefix + bps[item].Image);
                }
                else
                {
                    ImageLibrary.Call("AddImage", bps[item].Image, bps[item].Image);
                    if (!imgList.Contains(bps[item].Image))
                        imgList.Add(bps[item].Image);
                }
            }
            //add category images
            foreach (string category in config.main.cat.Keys)
            {
                if (!config.main.cat[category].StartsWith("assets"))
                {
                    ImageLibrary.Call("AddImage", config.main.cat[category], config.main.cat[category]);
                    imgList.Add(config.main.cat[category]);
                }
            }
            //call load order
            ImageLibrary.Call("ImportImageList", "CraftMenu", imgList);
        }

        private void ImageQueCheck()
        {
            int imgCount = imgList.Count();
            int downloaded = 0;
            foreach (string img in imgList)
            {
                if ((bool)ImageLibrary.Call("HasImage", img))
                    downloaded++;
            }

            if (imgCount > downloaded)
                Puts($"(!) Stored Images ({downloaded}/{imgCount}). Reload ImageLibrary and then CraftMenu plugin to start download order.");

            if (imgCount == downloaded)
                Puts($"Stored Images ({downloaded}). All images has been successfully stored in image library.");
        }

        private string Img(string url)
        {   //img url been used as image names
            if (ImageLibrary != null)
            {
                if (!(bool)ImageLibrary.Call("HasImage", url))
                    return url;
                else
                    return (string)ImageLibrary?.Call("GetImage", url);
            }
            else
                return url;
        }



        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
            GetDefaultImages();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty(PropertyName = "Main Settings")]
            public MainSet main { get; set; }

            public class MainSet
            {
                [JsonProperty("Wipe Blueprints at Map Wipe")]
                public bool wipe { get; set; }

                [JsonProperty("Sound Effects")]
                public bool fx { get; set; }

                [JsonProperty("Categories (Max 6)")]
                public Dictionary<string, string> cat { get; set; }

                [JsonProperty("Permissions required for each category")]
                public bool perms { get; set; }
            }

            [JsonProperty(PropertyName = "Crafting Time")]
            public CT ct { get; set; }

            public class CT
            {
                [JsonProperty("Enabled")]
                public bool craftQue { get; set; }

                [JsonProperty("Default Craft Time for all items (in seconds)")]
                public int defaultTime { get; set; }

                [JsonProperty("Specific Craft Time (in seconds)")]
                public Dictionary<string, int> excp { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    main = new CraftMenu.Configuration.MainSet
                    {
                        wipe = false,
                        fx = true,
                        cat = new Dictionary<string, string>
                        {
                            { "construction", "assets/icons/construction.png" },
                            { "weapons", "assets/icons/bullet.png" },
                            { "clothing", "assets/icons/clothing.png" },
                            { "electrical", "assets/icons/electric.png" },
                            { "vehicles", "assets/icons/horse_ride.png" },
                            { "dlc", "assets/icons/download.png" },
                        },
                        perms = false,
                    },

                    ct = new CraftMenu.Configuration.CT
                    {
                        craftQue = false,
                        defaultTime = 5,
                        excp = new Dictionary<string, int>
                        {
                            { "rifle.m39", 60 },
                            { "pistol.m92", 45 },
                            { "rifle.lr300", 80 },
                            { "rifle.l96", 120 },
                            { "lmg.m249", 200 },
                            { "multiplegrenadelauncher", 200 },
                        }
                    },
                };
            }
        }
    }
}