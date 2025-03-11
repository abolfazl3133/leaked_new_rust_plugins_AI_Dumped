using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Network;
using System.Diagnostics;

namespace Oxide.Plugins
{
    [Info("ATMSystem", "David", "2.0.3")]
    public class ATMSystem : RustPlugin
    {
        #region Variables

        List<CardReader> readers = new List<CardReader>();
        List<VendingMachine> machines = new List<VendingMachine>();
        Dictionary<BasePlayer, string> stored_input = new Dictionary<BasePlayer, string>();

        #endregion

        #region Hooks

        [PluginReference]
        private Plugin Economics;

        void OnServerInitialized(bool initial)
        {
            Puts("Looking for possible ATMs...");
            int count = 0;
            foreach (VendingMachine machine in GameObject.FindObjectsOfType(typeof(VendingMachine)))
            {
                if (machine.skinID == config.atm.skinID)
                {
                    count++;
                    ApplyATMPreset(machine);

                if (config.atm.card)
                    AttachCardReader(machine);
                }
            }
            Puts($"Created {count} ATMs.");
            
            if (initial)
            {
                timer.Once(90f, () => { 
                    Puts("Looking for possible ATMs again...");
                    foreach (VendingMachine machine in GameObject.FindObjectsOfType(typeof(VendingMachine)))
                    {
                        if (machine.skinID == config.atm.skinID)
                        {
                            count++;
                            ApplyATMPreset(machine);

                        if (config.atm.card)
                            AttachCardReader(machine);
                        }
                    }
                    Puts($"Created {count} ATMs.");
                }); 
            }
        }

        void OnEntityTakeDamage(VendingMachine entity, HitInfo info)
        {
            if (entity.skinID == config.atm.skinID)
                info.damageTypes.ScaleAll(0);
        }

        object OnRotateVendingMachine(VendingMachine entity, BasePlayer player)
        {
            if (entity.skinID == config.atm.skinID)
                return false;

            return null;
        }

        object CanAdministerVending(BasePlayer player, VendingMachine entity)
        {
            if (entity.skinID == config.atm.skinID)
                return false;

            return null;
        }

        private object CanUseVending(BasePlayer player, VendingMachine entity)
        {
            if (entity.skinID == config.atm.skinID)
            {
                if (config.atm.card)
                {
                    SendReply(player, gl("need_card"));
                    return false;
                }

                OpenInterface(player);
                return false;
            }

            return null;
        }

        object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {      
            if (card.skinID == config.card.skinID)
            {       
                if (cardReader.GetParentEntity() != null && cardReader.GetParentEntity().skinID == config.atm.skinID)
                {
                    OpenInterface(player);
                    return false;
                }

                var readerAsEnt = cardReader as BaseEntity;

                if (readerAsEnt != null && readerAsEnt.OwnerID != 0 )
                {
                    OpenInterface(player);
                    return false;
                }
                
                return false;
            }

            return null;
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (config.card.onSpawn && config.atm.card)
            {
                var item = ItemManager.CreateByName(config.card.shortname, 1, config.card.skinID);
                if (item != null)
                {
                    item.name = config.card.displayName;
                    player.GiveItem(item);
                }
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (config.card.takeOnDeath && config.atm.card)
            {
                TakeFromInventory(player, config.card.shortname, 1, config.card.skinID);
            }
        }

        void Unload()
        {
            /* foreach (var reader in readers)
            {
                try
                {
                    reader.Kill();
                }
                catch
                {
                    //
                }
            } */

            foreach (var machine in machines)
            {
                try
                {
                    //machine.skinID = 0;
                    machine.shopName = "";
                    machine.SetFlag(BaseEntity.Flags.Reserved4, false, false);
                    machine.SendNetworkUpdate();
                    machine.UpdateMapMarker();
                }
                catch
                {
                    //
                }
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "atm_bg");
            }
        }


        #endregion

        #region Method & Functions

        void ApplyATMPreset(VendingMachine entity)
        {
            entity.skinID = config.atm.skinID;
            entity.shopName = config.atm.name;

            if (config.atm.broadcast)
                entity.SetFlag(BaseEntity.Flags.Reserved4, true, true);

            entity.SendNetworkUpdate();
            entity.UpdateMapMarker();

            if (!machines.Contains(entity))
                machines.Add(entity);
        }

        void AttachCardReader(VendingMachine entity)
        {
            var reader = GameManager.server.CreateEntity("assets/prefabs/io/electric/switches/cardreader.prefab",
            new Vector3(-0.22f, -0.21f, 0.38f), Quaternion.Euler(-0f, -0f, 0f)) as CardReader;
            reader.accessLevel = 2;
            reader.SetFlag(IOEntity.Flag_HasPower, true);
            reader.SetParent(entity);
            reader.Spawn();

            if (!readers.Contains(reader))
                readers.Add(reader);
        }

        bool TakeFromInventory(BasePlayer player, string shortname, int amount, ulong skinid = 0)
        {
            if (player == null)
                return false;

            var item = ItemManager.FindItemDefinition(shortname);

            if (item == null)
            {
                SendReply(player, "Plugin configuration error, please let server admin know about it.");
                Puts($"{shortname} is not valid shortname");
                return false;
            }

            if (GetAmount(player, shortname, skinid) < amount)
                return false;

            if (RemoveItem(player, amount, shortname, skinid))
                return true;

            return false;
        }

        

        [ConsoleCommand("testg")]
        private void testg(ConsoleSystem.Arg arg)
        {   
            Stopwatch stopwatch = new Stopwatch();
            var player = arg?.Player();


            stopwatch.Start();
            Puts($"[{stopwatch.ElapsedMilliseconds}ms] Player has {GetAmount(player, "paper", 2570661100)} of paper");
            RemoveItem(player, 10, "paper", 2570661100);
            Puts($"[{stopwatch.ElapsedMilliseconds}ms] Removing 10 paper from player's inventory");
            
            Puts($"[{stopwatch.ElapsedMilliseconds}ms] Amount left now:{GetAmount(player, "paper", 2570661100)} paper");

            timer.Every(0.1f, () => { 
                if (stopwatch.ElapsedMilliseconds > 2000)
                {
                    return;
                }
                Puts($"[{stopwatch.ElapsedMilliseconds}ms] Amount left now:{GetAmount(player, "paper", 2570661100)} paper");
            });

        }

        int GetAmount(BasePlayer player, string shortname, ulong? skin)
        {
            var items = Facepunch.Pool.GetList<Item>();

            items.AddRange(player.inventory.containerBelt.itemList
                .Where(item => item.skin == skin && item.info.shortname == shortname));
            items.AddRange(player.inventory.containerMain.itemList
                .Where(item => item.skin == skin && item.info.shortname == shortname));

            var currentAmount = items.Sum(x => x.amount);

            Facepunch.Pool.FreeList(ref items);
            return currentAmount;
        }

        bool RemoveItem(BasePlayer player, int amount, string shortname, ulong? skin)
        {
            if (amount == 0) return false;

            var items = Facepunch.Pool.GetList<Item>();
            try
            {

                items.AddRange(player.inventory.containerBelt.itemList
                    .Where(item => item.skin == skin && item.info.shortname == shortname));

                items.AddRange(player.inventory.containerMain.itemList
                    .Where(item => item.skin == skin && item.info.shortname == shortname));

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
                Puts($"Something failed while taking items from {player} -> (shortname:{shortname}/amount:{amount}/skin:{skin})");
                return false;
            }

            Facepunch.Pool.FreeList(ref items);
            return true;
        }

        void UpdateBalancesUI(BasePlayer player, bool forceZero = false)
        {   
            if (stored_input.ContainsKey(player))
                stored_input[player] = "0";
            else
                stored_input.Add(player, "0");

            var c = new CuiElementContainer();
            
            CuiHelper.DestroyUi(player, "atm_input_field");
            CuiHelper.DestroyUi(player, "atm_acc_cash_content");
            CuiHelper.DestroyUi(player, "atm_acc_balance_content");
            Create.Input(ref c, "atm_input_field", "atm_input_b", "1 1 1 0.7", 13, "0.1 -0.02", "1 1", "0", "robotocondensed-bold.ttf", "atm_input", TextAnchor.MiddleLeft, 9);
            
            if (forceZero)
                Create.Text(ref c, "atm_acc_cash_content", "atm_acc_cash", "1 1 1 0.75", $"$ 0", 13, "0.05 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);
            else
                Create.Text(ref c, "atm_acc_cash_content", "atm_acc_cash", "1 1 1 0.75", $"$ {GetAmount(player, config.currency.shortname, config.currency.skinID)}", 13, "0.05 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);
            
            Create.Text(ref c, "atm_acc_balance_content", "atm_acc_balance", "1 1 1 0.75", $"$ {Economics.Call<double>("Balance", player.UserIDString)}", 13, "0.05 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);
            CuiHelper.AddUi(player, c);

        }

        void PlayFx(BasePlayer player, string fx)
        {
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

        bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        #endregion

        #region Commands 

        [ConsoleCommand("atm_input")]
        private void consolecmd_atminput(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (player == null) return;
            if (args.Length != 1)
            {
                Notification(player, false, "INTERNAL PLUGIN ERROR");
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            }

            if (!IsDigitsOnly(args[0]))
            {
                Notification(player, false, gl("ui_input_only_digits"));
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            }

            if (stored_input.ContainsKey(player))
                stored_input[player] = args[0];
            else
                stored_input.Add(player, args[0]);
        }

        [ConsoleCommand("atm_btn")]
        private void consolecmd_atmbtn(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (player == null) return;
            if (args.Length != 1)
            {
                Notification(player, false, "INTERNAL PLUGIN ERROR");
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            }

            int amount = 0;
            try
            {
                amount = int.Parse(stored_input[player]);
            }
            catch
            {
                Notification(player, false, "INTERNAL PLUGIN ERROR");
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            }


            if (!stored_input.ContainsKey(player))
            {
                Notification(player, false, "INTERNAL PLUGIN ERROR");
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            }

            if (amount <= 0) return;

            if (args[0] == "deposit")
            {
                bool forceZero = GetAmount(player, config.currency.shortname, config.currency.skinID) == amount;
                if (TakeFromInventory(player, config.currency.shortname, amount, config.currency.skinID))
                {
                    Economics.Call("Deposit", player.UserIDString, (double)amount);
                    UpdateBalancesUI(player, forceZero);
                    Notification(player, true, gl("ui_deposit_success").Replace("{value}", amount.ToString()));
                    PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab");
                    return;
                }
                else
                {
                    Notification(player, false, gl("ui_deposit_fail_inv"));
                    PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                }

            }

            if (args[0] == "withdraw")
            {
                if (Economics.Call<double>("Balance", player.UserIDString) < (double)amount)
                {
                    Notification(player, false, gl("ui_withdraw_fail"));
                    PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                    return;
                }

                var item = ItemManager.CreateByName(config.currency.shortname, amount, config.currency.skinID);
                if (item != null)
                {
                    Economics.Call("Withdraw", player.UserIDString, (double)amount);

                    item.name = config.currency.displayName;
                    player.GiveItem(item);

                    UpdateBalancesUI(player);

                    Notification(player, true, gl("ui_withdraw_success").Replace("{value}", amount.ToString()));
                    PlayFx(player, "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab");
                }
                else
                {
                    Notification(player, false, "INTERNAL PLUGIN ERROR");
                    PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                    Puts($"Something went wrong while giving {player} money -> (shortname:{config.currency.shortname}/amount:{amount}/skin:{config.currency.skinID})");
                }

            }
        }

        [ConsoleCommand("atm_closenotif")]
        private void consolecmd_atmclosenotif(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "atm_notif_bg");
        }


        [ChatCommand("setatm")]
        private void chatcmd_setatm(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            Vector3 ViewAdjust = new Vector3(0f, 1.5f, 0f);
            Vector3 position = player.transform.position + ViewAdjust;
            Vector3 rotation = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            RaycastHit hit;
            Physics.Raycast(position, rotation, out hit, 10);
            BaseEntity entity = hit.collider.GetComponentInParent<BaseEntity>();

            if (!(entity is VendingMachine))
            {
                SendReply(player, "Entity you looking at is not vending machine.");
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            }

            var vending = entity as VendingMachine;
            ApplyATMPreset(vending);

            if (config.atm.card)
                AttachCardReader(vending);

            SendReply(player, $"Vending machine({vending.net.ID.Value}) is set to ATM.");
            PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab");
        }

        [ChatCommand("setreader")]
        private void chatcmd_setreader(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            Vector3 ViewAdjust = new Vector3(0f, 1.5f, 0f);
            Vector3 position = player.transform.position + ViewAdjust;
            Vector3 rotation = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            RaycastHit hit;
            Physics.Raycast(position, rotation, out hit, 10);
            BaseEntity entity = hit.collider.GetComponentInParent<BaseEntity>();

            if (!(entity is CardReader))
            {
                SendReply(player, "Entity you looking at is not card reader");
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            }

            var reader = entity as BaseEntity;
            reader.OwnerID = 1337;

            var reader_ = entity as CardReader;
            reader_.accessLevel = 2;
            reader_.SetFlag(IOEntity.Flag_HasPower, true);
            reader_.SendNetworkUpdateImmediate();
            
            SendReply(player, $"Card reader({reader.net.ID.Value}) is set to ATM.");
            PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab");
        }

        [ChatCommand("givepower")]
        private void chatcmd_givepower(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            Vector3 ViewAdjust = new Vector3(0f, 1.5f, 0f);
            Vector3 position = player.transform.position + ViewAdjust;
            Vector3 rotation = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            RaycastHit hit;
            Physics.Raycast(position, rotation, out hit, 10);
            BaseEntity entity = hit.collider.GetComponentInParent<BaseEntity>();

            /* if (!(entity is CardReader))
            {
                SendReply(player, "Entity you looking at is not card reader");
                PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                return;
            } */

           /*  var reader = entity as BaseEntity;
            reader.OwnerID = 1337; */

            /* var reader_ = entity as CardReader;
            reader_.accessLevel = 2; */
            entity.SetFlag(IOEntity.Flag_HasPower, true);
            entity.SendNetworkUpdateImmediate();
            
            PlayFx(player, "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab");
        }

        #endregion

        #region User Interface

        void OpenInterface(BasePlayer player)
        {
            var c = new CuiElementContainer();

            // set input back to 0
            if (stored_input.ContainsKey(player))
                stored_input[player] = "0";
            else
                stored_input.Add(player, "0");

            var cc = config.colors;

            // background
            Create.Panel(ref c, "atm_bg", "Overlay", cc.bg, "0 0", "1 1", true, 0.08f, 0.0f, "assets/content/ui/uibackgroundblur.mat");
            Create.Panel(ref c, "atm_offset", "atm_bg", "0 0 0 0", "0.5 0.5", "0.5 0.5", false, 0.0f, 0.0f, "assets/icons/iconmaterial.mat", "-680 -360", "680 360", true);

            // title 
            Create.Text(ref c, "atm_title", "atm_offset", "1 1 1 1", gl("ui_title"), 12, "0.3485 0.690", "0.7 0.9", TextAnchor.LowerLeft, "robotocondensed-bold.ttf", 0.08f);

            // main panel
            Create.Panel(ref c, "atm_base", "atm_offset", cc.bg2, "0.35 0.30", "0.64 0.7", false, 0.08f, 0.0f, "assets/content/ui/uibackgroundblur.mat");

            // info panel assets/icons/info.png
            Create.Panel(ref c, "atm_info_t", "atm_offset", cc.pTitle, "0.352 0.665", "0.638 0.695", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Panel(ref c, "atm_info_b", "atm_offset", cc.pCon, "0.352 0.58", "0.638 0.665", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Text(ref c, "atm_info_title", "atm_info_t", "1 1 1 0.55", gl("ui_info_title"), 12, "0.06 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Asset(ref c, "atm_icon_info", "atm_info_t", "1 1 1 0.55", "assets/icons/info.png", 0.08f, 0f, "0.01 0.2", "0.045 0.8");
            Create.Text(ref c, "atm_info_text", "atm_info_b", "1 1 1 0.8", gl("ui_info"), 11, "0.015 0", "0.985 0.92", TextAnchor.UpperLeft, "robotocondensed-regular.ttf", 0.08f);

            // account panel
            Create.Panel(ref c, "atm_acc_t", "atm_offset", cc.pTitle, "0.352 0.54", "0.499 0.575", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Panel(ref c, "atm_acc_b", "atm_offset", cc.pCon, "0.352 0.305", "0.499 0.54", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Text(ref c, "atm_acc_title", "atm_acc_t", "1 1 1 0.55", gl("ui_account_title"), 12, "0.12 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Asset(ref c, "atm_icon_account", "atm_acc_t", "1 1 1 0.55", "assets/icons/bp-lock.png", 0.08f, 0f, "0.02 0.25", "0.1 0.8");

            Create.Panel(ref c, "atm_acc_name", "atm_offset", "0 0 0 0.5", "0.362 0.46", "0.489 0.50", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Text(ref c, "atm_acc_name_title", "atm_acc_name", "1 1 1 0.35", gl("ui_account_holder"), 10, "0.01 1.05", "1 2", TextAnchor.LowerLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Text(ref c, "atm_acc_name_content", "atm_acc_name", "1 1 1 0.55", $"{player.displayName}", 13, "0.05 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);

            Create.Panel(ref c, "atm_acc_balance", "atm_offset", "0 0 0 0.5", "0.362 0.39", "0.489 0.43", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Text(ref c, "atm_acc_balance_title", "atm_acc_balance", "1 1 1 0.35", gl("ui_balance"), 10, "0.01 1.05", "1 2", TextAnchor.LowerLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Text(ref c, "atm_acc_balance_content", "atm_acc_balance", "1 1 1 0.75", $"$ {Economics.Call<double>("Balance", player.UserIDString)}", 13, "0.05 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);

            Create.Panel(ref c, "atm_acc_cash", "atm_offset", "0 0 0 0.5", "0.362 0.32", "0.489 0.36", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Text(ref c, "atm_acc_cash_title", "atm_acc_cash", "1 1 1 0.35", gl("ui_cash"), 10, "0.01 1.05", "1 2", TextAnchor.LowerLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Text(ref c, "atm_acc_cash_content", "atm_acc_cash", "1 1 1 0.75", $"$ {GetAmount(player, config.currency.shortname, config.currency.skinID)}", 13, "0.05 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);


            // controls panel with input
            Create.Panel(ref c, "atm_controls_t", "atm_offset", cc.pTitle, "0.5015 0.54", "0.638 0.575", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Panel(ref c, "atm_controls_b", "atm_offset", cc.pCon, "0.5015 0.305", "0.638 0.54", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Text(ref c, "atm_controls_title", "atm_controls_t", "1 1 1 0.55", gl("ui_controls_title"), 12, "0.13 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Asset(ref c, "atm_icon_controls", "atm_controls_t", "1 1 1 0.55", "assets/icons/player_loot.png", 0.08f, 0f, "0.02 0.25", "0.1 0.8");

            Create.Panel(ref c, "atm_input_b", "atm_offset", "0 0 0 0.5", "0.51 0.46", "0.629 0.50", false, 0.08f, 0.0f, "assets/icons/iconmaterial.mat");
            Create.Text(ref c, "atm_input_title", "atm_input_b", "1 1 1 0.35", gl("ui_enter_amount"), 10, "0.01 1.1", "1 2", TextAnchor.LowerLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Text(ref c, "atm_input_symbol", "atm_input_b", "1 1 1 0.35", "$", 12, "0.05 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.08f);
            Create.Input(ref c, "atm_input_field", "atm_input_b", "1 1 1 0.7", 13, "0.1 -0.02", "1 1", "0", "robotocondensed-bold.ttf", "atm_input", TextAnchor.MiddleLeft, 9);
            Create.Button(ref c, "atm_withdraw_btn", "atm_offset", cc.wBtn, gl("ui_btn_withdraw"), 12, "0.51 0.39", "0.629 0.435", "atm_btn withdraw", "", "1 1 1 1", 0.08f);
            Create.Button(ref c, "atm_withdraw_btn", "atm_offset", cc.dBtn, gl("ui_btn_deposit"), 12, "0.51 0.325", "0.629 0.37", "atm_btn deposit", "", "1 1 1 1", 0.08f);

            // close button
            Create.Button(ref c, "atm_close_btn", "atm_info_t", "0.631 0.282 0.22 1", "", 12, "0.945 0.1", "0.995 0.86", "", "atm_bg", "1 1 1 1", 0.08f);
            Create.Asset(ref c, "atm_icon_cross", "atm_close_btn", "1 1 1 0.8", "assets/icons/vote_down.png", 0.08f);


            CuiHelper.DestroyUi(player, "atm_bg");
            CuiHelper.AddUi(player, c);

        }

        void Notification(BasePlayer player, bool success, string text)
        {
            var c = new CuiElementContainer();

            string color = success ? config.colors.nSuccess : config.colors.nFail;
            string icon = success ? "assets/icons/vote_up.png" : "assets/icons/vote_down.png";

            Create.Panel(ref c, "atm_notif_bg", "atm_base", config.colors.bg2, "0 -0.15", "1 0", false, 0.3f, 0f);
            Create.Panel(ref c, "atm_notif_color", "atm_notif_bg", color, "0.008 0.1", "0.992 1", false, 0.3f, 0f);
            Create.Asset(ref c, "atm_icon_voteup", "atm_notif_bg", "1 1 1 0.65", icon, 0.3f, 0f, "0.015 0.2", "0.125 0.90");
            Create.Text(ref c, "atm_notif_text", "atm_notif_bg", "1 1 1 0.55", text, 12, "0.13 0.1", "1 1", TextAnchor.MiddleLeft, "robotocondensed-bold.ttf", 0.3f);

            c.Add(new CuiElement
            {
                Parent = "atm_notif_bg",
                Name = "atm_notif",
                Components = {
                    new CuiTextComponent{ Text = "", FontSize = 0, Align = TextAnchor.MiddleLeft, Color = "0 0 0 0", Font = "robotocondensed-regular.ttf",},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0 0" },
                    new CuiCountdownComponent { EndTime = 2, StartTime = 0, Command = "atm_closenotif"}
                },
                FadeOut = 0f
            });


            CuiHelper.DestroyUi(player, "atm_notif_bg");
            CuiHelper.AddUi(player, c);

        }

        public class Create
        {
            public static void Panel(ref CuiElementContainer container, string name, string parent, string color, string anchorMinx, string anchorMax, bool cursorOn = false, float fade = 0f, float fadeOut = 0f, string material = "", string offsetMin = "", string offsetMax = "", bool keyboard = false)
            {
                container.Add(new CuiPanel
                {

                    Image = { Color = color, Material = material, FadeIn = fade },
                    RectTransform = { AnchorMin = anchorMinx, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax },
                    FadeOut = 0f,
                    CursorEnabled = cursorOn,
                    KeyboardEnabled = keyboard,

                },
                parent,
                name);
            }

            public static void Image(ref CuiElementContainer container, string name, string parent, string image, string anchorMinx, string anchorMax, float fade = 0f, float fadeOut = 0f, string offsetMin = "", string offsetMax = "")
            {
                if (image.StartsWith("http") || image.StartsWith("www"))
                {
                    container.Add(new CuiElement
                    {
                        Name = name,
                        Parent = parent,
                        FadeOut = 0f,
                        Components =
                        {
                            new CuiRawImageComponent { Url = image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = fade},
                            new CuiRectTransformComponent { AnchorMin = anchorMinx, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = parent,
                        Components =
                        {
                            new CuiRawImageComponent { Png = image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = fade},
                            new CuiRectTransformComponent { AnchorMin = anchorMinx, AnchorMax = anchorMax }
                        }
                    });
                }
            }

            public static void Text(ref CuiElementContainer container, string name, string parent, string color, string text, int size, string anchorMinx, string anchorMax, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", float fade = 0f, float fadeOut = 0f, string _outlineColor = "0 0 0 0", string _outlineScale = "0 0")
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Font = font,
                            Align = align,
                            Color = color,
                            FadeIn = fade,
                        },

                        new CuiOutlineComponent
                        {

                            Color = _outlineColor,
                            Distance = _outlineScale

                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = anchorMinx,
                             AnchorMax = anchorMax
                        }
                    },
                    FadeOut = 0f
                });
            }

            public static void Button(ref CuiElementContainer container, string name, string parent, string color, string text, int size, string anchorMinx, string anchorMax, string command = "", string _close = "", string textColor = "0.843 0.816 0.78 1", float fade = 1f, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-bold.ttf", string material = "assets/content/ui/uibackgroundblur-ingamemenu.mat")
            {
                container.Add(new CuiButton
                {
                    Button = { Close = _close, Command = command, Color = color, Material = material, FadeIn = fade },
                    RectTransform = { AnchorMin = anchorMinx, AnchorMax = anchorMax },
                    Text = { Text = text, FontSize = size, Align = align, Color = textColor, Font = font, FadeIn = fade }
                },
                parent,
                name);
            }

            public static void Asset(ref CuiElementContainer container, string name, string parent, string color, string sprite, float fadeIn = 0f, float fadeOut = 0f, string anchorMin = "0 0", string anchorMax = "1 1", string material = "assets/icons/iconmaterial.mat")
            {
                //assets/content/textures/generic/fulltransparent.tga MAT
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
                            {
                                new CuiImageComponent { Material = material, Sprite = sprite, Color = color, FadeIn = fadeIn},
                                new CuiRectTransformComponent {AnchorMin = anchorMin, AnchorMax = anchorMax}
                            },
                    FadeOut = fadeOut
                });
            }

            public static void Input(ref CuiElementContainer container, string name, string parent, string color, int size, string anchorMin, string anchorMax, string defaultText, string font = "permanentmarker.ttf", string command = "command.processinput", TextAnchor align = TextAnchor.MiddleCenter, int charsLimit = 200)
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,

                    Components =
                    {
                        new CuiInputFieldComponent
                        {

                            Text = defaultText,
                            CharsLimit = charsLimit,
                            Color = color,
                            IsPassword = false,
                            Command = command,
                            Font = font,
                            FontSize = size,
                            Align = align,
                            Autofocus = false,
                            LineType = UnityEngine.UI.InputField.LineType.SingleLine
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchorMin,
                            AnchorMax = anchorMax

                        }

                    },
                });
            }
        }

        #endregion

        #region Configuration 

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty(PropertyName = "ATM")]
            public ATM atm { get; set; }

            public class ATM
            {
                [JsonProperty("Skin ID")]
                public ulong skinID { get; set; }

                [JsonProperty("Use Card Reader")]
                public bool card { get; set; }

                [JsonProperty("Marker Name")]
                public string name { get; set; }

                [JsonProperty("Broadcast")]
                public bool broadcast { get; set; }
            }


            [JsonProperty(PropertyName = "Currency")]
            public Currency currency { get; set; }

            public class Currency
            {
                [JsonProperty("Shortname")]
                public string shortname { get; set; }

                [JsonProperty("Display name")]
                public string displayName { get; set; }

                [JsonProperty("Skin ID")]
                public ulong skinID { get; set; }
            }

            [JsonProperty(PropertyName = "Card")]
            public Card card { get; set; }

            public class Card
            {
                [JsonProperty("Skin ID")]
                public ulong skinID { get; set; }

                [JsonProperty("Display Name")]
                public string displayName { get; set; }

                [JsonProperty("Shortname")]
                public string shortname { get; set; }

                [JsonProperty("Receive card on spawn")]
                public bool onSpawn { get; set; }

                [JsonProperty("Destroy card on death")]
                public bool takeOnDeath { get; set; }
            }

            [JsonProperty(PropertyName = "UI Colors")]
            public Colors colors { get; set; }

            public class Colors
            {
                [JsonProperty("Background")]
                public string bg { get; set; }

                [JsonProperty("Main Panel Background")]
                public string bg2 { get; set; }

                [JsonProperty("Content Panel")]
                public string pCon { get; set; }

                [JsonProperty("Title Panel")]
                public string pTitle { get; set; }

                [JsonProperty("Withdraw Button")]
                public string wBtn { get; set; }

                [JsonProperty("Deposit Button")]
                public string dBtn { get; set; }

                [JsonProperty("Notification Success")]
                public string nSuccess { get; set; }

                [JsonProperty("Notification Fail")]
                public string nFail { get; set; }
            }


            public static Configuration CreateConfig()
            {
                return new Configuration
                {

                    atm = new ATMSystem.Configuration.ATM
                    {
                        skinID = 3042408530,
                        card = false,
                        name = "ATM",
                        broadcast = true
                    },

                    currency = new ATMSystem.Configuration.Currency
                    {
                        shortname = "paper",
                        displayName = "Cash",
                        skinID = 2570661100,
                    },

                    card = new ATMSystem.Configuration.Card
                    {
                        skinID = 2410672337,
                        displayName = "Credit Card",
                        shortname = "keycard_blue",
                        onSpawn = false,
                        takeOnDeath = false,

                    },

                    colors = new ATMSystem.Configuration.Colors
                    {
                        bg = "0 0 0 0.79",
                        bg2 = "0.086 0.082 0.078 1",
                        pCon = "0.357 0.357 0.357 0.25",
                        pTitle = "0.357 0.357 0.357 0.40",
                        wBtn = "0.337 0.424 0.196 1",
                        dBtn = "0.082 0.369 0.482 1",
                        nSuccess = "0.337 0.424 0.196 1",
                        nFail = "0.631 0.282 0.22 1"
                    }
                };
            }
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["need_card"] = "You need card to access ATM.",
                ["no_perms"] = "You don't have permissions to use ATM.",
                ["ui_title"] = "<size=70><color=#ce422b>RUST</color> ATM</size>",
                ["ui_info_title"] = "INFORMATION",
                ["ui_info"] = "This field is used to explain how economy works with ATMs on your server, where money can be found etc...",
                ["ui_account_title"] = "ACCOUNT",
                ["ui_account_holder"] = "ACCOUNT HOLDER",
                ["ui_balance"] = "BALANCE",
                ["ui_cash"] = "CASH ON HAND",
                ["ui_controls_title"] = "CONTROLS",
                ["ui_enter_amount"] = "ENTER AMOUNT",
                ["ui_btn_withdraw"] = "<color=#bad56aff>WITHDRAW</color>",
                ["ui_btn_deposit"] = "<color=#66b4d3ff>DEPOSIT</color>",
                ["ui_withdraw_success"] = "You have withdrawn {value}$ from your account.",
                ["ui_withdraw_fail"] = "You don't have enough money in your account.",
                ["ui_deposit_success"] = "You have deposited {value}$ to your account.",
                ["ui_deposit_fail_inv"] = "You don't have enough money in your inventory.",
                ["ui_deposit_fail_inv_fail"] = "Something went wrong while giving you money.",
                ["ui_input_only_digits"] = "Invalid characters inside input field."

            }, this);
        }

        string gl(string _message) => lang.GetMessage(_message, this);

        #endregion
    }
}