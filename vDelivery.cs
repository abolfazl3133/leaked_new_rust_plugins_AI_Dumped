/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/vdelivery
*  Codefling license: https://codefling.com/plugins/vdelivery?tab=downloads_field_4
*
*  Copyright © 2023-2024 IIIaKa
*/

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("vDelivery", "IIIaKa", "0.1.3")]
    [Description("Allows you to add delivery drones to your vending machine through which you can order various items.")]
    class vDelivery : RustPlugin
    {
        [PluginReference]
        private Plugin ImageLibrary, CustomVendingSetup;
        
        #region ~Variables~
        private const string PERMISSION_ADMIN = "vDelivery.admin", UI_Name = "vDelivery_Popup", MarketplacePrefab = "assets/prefabs/misc/marketplace/marketplace.prefab", MarketplaceTerminalPrefab = "assets/prefabs/misc/marketplace/marketterminal.prefab", ItemDropPrefab = "assets/prefabs/misc/item drop/item_drop.prefab";
        private ulong _skinID = 3074297551uL;
        private Dictionary<ulong, Timer> _playerTimers = new Dictionary<ulong, Timer>();
        private Vector3 _terminalPos = Vector3.zero, _marketPos = new Vector3(0f, -2000f, 0f);
        private string _iconColor, _textColor, _text2Color;
        #endregion

        #region ~Configuration~
        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Chat command")]
            public string Command = "vdelivery";
            
            [JsonProperty(PropertyName = "Is it worth enabling GameTips for messages?")]
            public bool GameTips_Enabled = true;
            
            [JsonProperty(PropertyName = "Is it worth ignoring the inaccessibility of drones?")]
            public bool Ignore_Accessibility = false;
            
            [JsonProperty(PropertyName = "Display position - Forward")]
            public float Display_Pos_Forward = -0.35f;

            [JsonProperty(PropertyName = "Display position - Up")]
            public float Display_Pos_Up = 1.8f;

            [JsonProperty(PropertyName = "Display position - Right")]
            public float Display_Pos_Right = 0f;
            
            [JsonProperty(PropertyName = "Popup - Duration")]
            public float Popup_Duration = 6f;
            
            [JsonProperty(PropertyName = "Popup - Position AnchorMin")]
            public string Popup_AnchorMin = "0 0.9";

            [JsonProperty(PropertyName = "Popup - Position AnchorMax")]
            public string Popup_AnchorMax = "0.25 1";

            [JsonProperty(PropertyName = "Popup - Position OffsetMin")]
            public string Popup_OffsetMin = "20 0";

            [JsonProperty(PropertyName = "Popup - Position OffsetMax")]
            public string Popup_OffsetMax = "0 -30";
            
            [JsonProperty(PropertyName = "Popup - Icon Url")]
            public string Popup_Icon_Url = "https://i.imgur.com/4Adzkb8.png";
            
            [JsonProperty(PropertyName = "Popup - Icon Color")]
            public string Popup_Icon_Color = "#CCE699";
            
            [JsonProperty(PropertyName = "Popup - Icon Transparency")]
            public float Popup_Icon_Transparency = 0.8f;
            
            [JsonProperty(PropertyName = "Popup - Text Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
            public string Popup_Text_Font = "RobotoCondensed-Bold.ttf";
            
            [JsonProperty(PropertyName = "Popup - Text Font Size")]
            public int Popup_Text_Font_Size = 14;
            
            [JsonProperty(PropertyName = "Popup - Text Font Color")]
            public string Popup_Text_Font_Color = "#FFFFFF";
            
            [JsonProperty(PropertyName = "Popup - Description Font")]
            public string Popup_Desc_Font = "RobotoCondensed-Regular.ttf";
            
            [JsonProperty(PropertyName = "Popup - Description Font Size")]
            public int Popup_Desc_Font_Size = 12;

            [JsonProperty(PropertyName = "Popup - Description Font Color")]
            public string Popup_Desc_Font_Color = "#FFFFFF";
            
            [JsonProperty(PropertyName = "Popup - Text FadeIn")]
            public float Popup_Text_FadeIn = 1f;
            
            [JsonProperty(PropertyName = "Popup - Sound Prefab Name")]
            public string Popup_Sound_Prefab = "assets/bundled/prefabs/fx/invite_notice.prefab";
            
            [JsonProperty(PropertyName = "Settings of vending machines for each permission. Leave null or empty to recreate the default")]
            public Dictionary<string, PermissionLimit> PermissionsLimits = new Dictionary<string, PermissionLimit>();
            
            public Oxide.Core.VersionNumber Version;
        }
        
        public class PermissionLimit
        {
            [JsonProperty(PropertyName = "Max ammount")]
            public int Max_Amount;
            
            [JsonProperty(PropertyName = "Delivery fee item")]
            public string Fee_Item_Definition;
            
            [JsonProperty(PropertyName = "Delivery fee amount")]
            public int Fee_Item_Amount;
            
            public PermissionLimit(int limit = 1, string fee_Item_Definition = "scrap", int fee_Item_Amount = 20)
            {
                Max_Amount = limit;
                Fee_Item_Definition = fee_Item_Definition;
                Fee_Item_Amount = fee_Item_Amount;
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { _config = Config.ReadObject<Configuration>(); }
            catch (Exception ex) { PrintError($"{ex.Message}\n\n[{Title}] Your configuration file contains an error."); }
            if (_config == null || _config.Version == new VersionNumber())
            {
                PrintWarning("The configuration file is not found or contains errors. Creating a new one...");
                LoadDefaultConfig();
            }
            else if (_config.Version < Version)
            {
                PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
                _config.Version = Version;
                PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
            }

            _terminalPos = new Vector3(_config.Display_Pos_Right, _config.Display_Pos_Up, _config.Display_Pos_Forward);
            _iconColor = StringColorFromHex(_config.Popup_Icon_Color, _config.Popup_Icon_Transparency);
            _textColor = StringColorFromHex(_config.Popup_Text_Font_Color);
            _text2Color = StringColorFromHex(_config.Popup_Desc_Font_Color);
            
            if (_config.PermissionsLimits == null || !_config.PermissionsLimits.Any())
                _config.PermissionsLimits = new Dictionary<string, PermissionLimit>() { { "vDelivery.default", new PermissionLimit(1) }, { "vDelivery.vip", new PermissionLimit(3) }, { "realpve.vip", new PermissionLimit(5) } };
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
        #endregion
        
        #region ~DataFile~
        private static StoredData _storedData;

        private class StoredData
        {
            [JsonProperty(PropertyName = "Player's Data")]
            public Dictionary<ulong, PlayerData> PlayersData = new Dictionary<ulong, PlayerData>();
        }
        
        public class PlayerData
        {
            public bool AutoModify = true;
            public HashSet<ulong> Vendings = new HashSet<ulong>();
        }
        
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        #endregion
        
        #region ~Language~
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MsgNotAllowed"] = "You do not have permission to use this command!",
                ["MsgNotOwner"] = "You are not the owner of this vending machine!",
                ["MsgNotAccessible"] = "The vending machine is not accessible to drones!",
                ["MsgNotVending"] = "You need to look at the vending machine or provide correct net ID!",
                ["MsgNotVendingDelivery"] = "The vending machine does not have a terminal!",
                ["MsgLimitReached"] = "You cannot add a terminal as you have reached your limit of {0}!",
                ["MsgPopupText"] = "Add a terminal to the vending machine?",
                ["MsgPopupSubText"] = "Click on the notification to confirm",
                ["MsgMyAdded"] = "The terminal has been successfully added!",
                ["MsgMyRemoved"] = "The terminal has been successfully removed!",
                ["MsgMyAllRemoved"] = "All your terminals have been successfully removed!",
                ["MsgPlayerAllRemoved"] = "All {0}'s terminals have been successfully removed!",
                ["MsgAllRemoved"] = "All terminals have been successfully removed!",
                ["MsgTerminalsNotFound"] = "No terminals found!",
                ["MsgPlayerTerminalsNotFound"] = "{0}'s terminals not found!",
                ["MsgNoHaveCustomFee"] = "To pay the personal fee, you need to have :{0}:(x{1}). Using default fee settings!",
                ["MsgAutoModifyEntityEnabled"] = "Automatic entity modification is enabled!",
                ["MsgAutoModifyEntityDisabled"] = "Automatic entity modification is disabled!"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MsgNotAllowed"] = "У вас недостаточно прав для использования этой команды!",
                ["MsgNotOwner"] = "Вы не являетесь владельцем данного торгового автомата!",
                ["MsgNotAccessible"] = "Торговый автомат не доступен для дронов!",
                ["MsgNotVending"] = "Вам необходимо смотреть на торговый автомат или указать корректный net ID!",
                ["MsgNotVendingDelivery"] = "Торговый автомат не имеет терминала!",
                ["MsgLimitReached"] = "Вы не можете добавить терминал, так как вы превысили свой лимит в {0}!",
                ["MsgPopupText"] = "Добавить терминал к торговому автомату?",
                ["MsgPopupSubText"] = "Нажмите на уведомление для подтверждения",
                ["MsgMyAdded"] = "Терминал успешно добавлен!",
                ["MsgMyRemoved"] = "Терминал успешно удален!",
                ["MsgMyAllRemoved"] = "Все ваши терминалы успешно удалены!",
                ["MsgPlayerAllRemoved"] = "Все терминалы игрока {0} успешно удалены!",
                ["MsgAllRemoved"] = "Все терминалы успешно удалены!",
                ["MsgTerminalsNotFound"] = "Терминалы не найдены!",
                ["MsgPlayerTerminalsNotFound"] = "Терминалы игрока {0} не найдены!",
                ["MsgNoHaveCustomFee"] = "Для оплаты персональной комиссии вам необходимо иметь :{0}:(x{1}). Использование настроек комиссии по умолчанию!",
                ["MsgAutoModifyEntityEnabled"] = "Автоматическая модификация сущностей включена!",
                ["MsgAutoModifyEntityDisabled"] = "Автоматическая модификация сущностей выключена!"
            }, this, "ru");
        }
        #endregion

        #region ~Methods~
        private void LoadImage() => ImageLibrary?.Call("AddImage", _config.Popup_Icon_Url, UI_Name, 0uL);
        
        private bool CanModifyVending(VendingMachine vending, PlayerData playerData, IPlayer player = null)
        {
            string ownerID = vending.OwnerID.ToString();
            if (permission.UserHasPermission(ownerID, PERMISSION_ADMIN)) return true;
            bool result = false;
            string replyKey = string.Empty;
            string[] replyArgs = new string[5];
            
            int limit = GetHighestLimit(ownerID);
            if (limit == int.MinValue)
                replyKey = "MsgNotAllowed";
            else if (!_config.Ignore_Accessibility && !IsVendingAccessible(vending))
                replyKey = "MsgNotAccessible";
            else if (limit != -1 && !playerData.Vendings.Contains(vending.net.ID.Value) && playerData.Vendings.Count >= limit)
            {
                replyKey = "MsgLimitReached";
                replyArgs[0] = limit.ToString();
            }
            else
                result = true;
            
            if (!result)
            {
                if (player != null)
                    SendMessage(player, string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs), true);
                playerData.Vendings.Remove(vending.net.ID.Value);
            }
            return result;
        }
        
        private void ModifyVending(VendingMachine vending, bool sendReply = false)
        {
            RemoveFromVending(vending, true);
            if (!_storedData.PlayersData.TryGetValue(vending.OwnerID, out var playerData)) return;
            vending.skinID = _skinID;
            
            var marketEnt = GameManager.server.CreateEntity(MarketplacePrefab) as Marketplace;
            if (marketEnt != null)
            {
                marketEnt.skinID = _skinID;
                marketEnt.Spawn();
                marketEnt.SetParent(vending, true);
                marketEnt.transform.localPosition = _marketPos;
                marketEnt.transform.localRotation = Quaternion.identity;
                marketEnt.droneLaunchPoint.position = vending.transform.position + vending.transform.forward * 1f + vending.transform.up * 1.5f;
                foreach (var terminal in marketEnt.terminalEntities)
                    terminal.Get(true).Kill();
            }
            
            var terEnt = GameManager.server.CreateEntity(MarketplaceTerminalPrefab) as MarketTerminal;
            if (terEnt != null)
            {
                terEnt.skinID = _skinID;
                terEnt.Spawn();
                terEnt.SetParent(vending, true);
                terEnt.transform.localPosition = _terminalPos;
                terEnt.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                terEnt.Setup(marketEnt);
            }

            playerData.Vendings.Add(vending.net.ID.Value);
            if (sendReply && covalence.Players.FindPlayerById(vending.OwnerID.ToString()) is IPlayer player)
                SendMessage(player, lang.GetMessage("MsgMyAdded", this, player.Id));
        }
        
        private void RemoveFromVending(VendingMachine vending, bool isUnload = false)
        {
            if (vending.skinID == _skinID)
                vending.skinID = 0uL;
            if (IsVendingHasTerminal(vending))
            {
                var entitiesToRemove = Pool.Get<List<BaseEntity>>();
                foreach (var vendingChild in vending.children)
                {
                    if (!vendingChild.IsDestroyed && (vendingChild is MarketTerminal || vendingChild is Marketplace))
                        entitiesToRemove.Add(vendingChild);
                }
                foreach (var entToRemove in entitiesToRemove)
                {
                    if (entToRemove is MarketTerminal market)
                    {
                        var inventory = market.inventory;
                        if (inventory != null && inventory.itemList.Any())
                            inventory.Drop(ItemDropPrefab, vending.dropPosition, vending.transform.rotation, 0f);
                    }
                    entToRemove.Kill();
                }
                Pool.FreeUnmanaged(ref entitiesToRemove);
            }
            if (!isUnload && _storedData.PlayersData.TryGetValue(vending.OwnerID, out var playerData))
                playerData.Vendings.Remove(vending.net.ID.Value);
        }
        
        private void RemoveVendingOnRevoke(ulong userID, PlayerData playerData)
        {
            int counter = 0, limit = GetHighestLimit(userID.ToString());
            var removeList = Pool.Get<List<VendingMachine>>();
            foreach (var netID in playerData.Vendings)
            {
                var vending = BaseNetworkable.serverEntities.Find(new NetworkableId(netID)) as VendingMachine;
                if (vending != null)
                {
                    counter++;
                    if (counter > limit)
                        removeList.Add(vending);
                }
            }
            foreach (var vending in removeList)
                RemoveFromVending(vending);
            Pool.FreeUnmanaged(ref removeList);
        }
        
        private void ClearEntities(bool isUnload = false)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is VendingMachine vending)
                    RemoveFromVending(vending, isUnload);
            }
        }
        
        private void InitPlayer(BasePlayer player)
        {
            if (!_storedData.PlayersData.ContainsKey(player.userID))
                _storedData.PlayersData[player.userID] = new PlayerData();
        }
        
        private bool GetTerminalContainer(VendingMachine vending, out ItemContainer result)
        {
            result = null;
            if (vending.children != null)
            {
                foreach (var child in vending.children)
                {
                    if (child is MarketTerminal terminal)
                    {
                        result = terminal.inventory;
                        break;
                    }
                }
            }
            return result != null && result.itemList.Any();
        }
        
        private bool IsVendingHasTerminal(VendingMachine vending) => vending.children != null && vending.children.Any(e => e is MarketTerminal);
        
        private bool IsNPCVendingCustom(VendingMachine vendingMachine) => (bool)(CustomVendingSetup?.Call("API_IsCustomized", vendingMachine as NPCVendingMachine) ?? false);
        
        public bool IsVendingAccessible(VendingMachine vending, Vector3 offset = default, Vector3 halfExtents = default, float testHeight = 200f)
        {
            if (offset.Equals(default))
                offset = new Vector3(0f, 1f, 1f);
            if (halfExtents.Equals(default))
                halfExtents = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 vector = vending.transform.TransformPoint(offset);
            if (Physics.BoxCast(vector + Vector3.up * testHeight, halfExtents, Vector3.down, vending.transform.rotation, testHeight, 161546496))
                return false;

            return vending.IsVisibleAndCanSee(vector);
        }
        
        private bool GetVendingFromArgs(BasePlayer player, string[] args, out VendingMachine result)
        {
            result = null;
            if (args.Length > 1 && ulong.TryParse(args[1], out var entID))
            {
                var vendingByID = BaseNetworkable.serverEntities.Find(new NetworkableId(entID)) as VendingMachine;
                if (vendingByID != null)
                    result = vendingByID;
            }
            if (result == null && Physics.Raycast(player.eyes.HeadRay(), out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.GetEntity() is VendingMachine vending)
                result = vending;
            return result != null;
        }
        
        private int GetHighestLimit(string userID)
        {
            int result = int.MinValue, limit;
            foreach (var kvp in _config.PermissionsLimits)
            {
                limit = kvp.Value.Max_Amount;
                if (limit == -1)
                {
                    if (permission.UserHasPermission(userID, kvp.Key))
                        return limit;
                    continue;
                }
                if (result < limit && permission.UserHasPermission(userID, kvp.Key))
                    result = limit;
            }
            return result;
        }
        
        private bool GetDeliveryFee(string userID, out PermissionLimit result)
        {
            result = null;
            int highestAmount = int.MinValue, lowestFee = int.MaxValue;
            foreach (var kvp in _config.PermissionsLimits)
            {
                if (!permission.UserHasPermission(userID, kvp.Key)) continue;
                int maxAmount = kvp.Value.Max_Amount, feeAmount = kvp.Value.Fee_Item_Amount;
                if (maxAmount > highestAmount)
                {
                    highestAmount = maxAmount;
                    lowestFee = feeAmount;
                    result = kvp.Value;
                }
                else if (maxAmount == highestAmount && feeAmount < lowestFee)
                {
                    lowestFee = feeAmount;
                    result = kvp.Value;
                }
            }
            return result != null;
        }
        
        private static string StringColorFromHex(string hexColor, double transperent = 1d)
        {
            if (hexColor[0] != '#' || hexColor.Length < 7)
                return $"1 1 1 {transperent:F2}";

            int red = Convert.ToInt32(hexColor.Substring(1, 2), 16);
            int green = Convert.ToInt32(hexColor.Substring(3, 2), 16);
            int blue = Convert.ToInt32(hexColor.Substring(5, 2), 16);

            double redPercentage = (double)red / 255;
            double greenPercentage = (double)green / 255;
            double bluePercentage = (double)blue / 255;

            return $"{redPercentage:F2} {greenPercentage:F2} {bluePercentage:F2} {transperent:F2}";
        }
        
        private static void SendEffect(Vector3 position, Network.Connection connection, string prefabName = "")
        {
            if (prefabName.Length < 10)
                prefabName = "assets/bundled/prefabs/fx/invite_notice.prefab";
            var effect = new Effect();
            effect.Init(Effect.Type.Generic, position, Vector3.zero);
            effect.pooledString = prefabName;
            EffectNetwork.Send(effect, connection);
        }

        private static void SendMessage(IPlayer player, string message, bool isWarning = false)
        {
            if (_config.GameTips_Enabled)
                player.Command("gametip.showtoast", (int)(isWarning ? GameTip.Styles.Error : GameTip.Styles.Blue_Normal), message, string.Empty);
            else
                player.Reply(message);
        }
        
        private void DestroyPopup(BasePlayer player)
        {
            if (_playerTimers.TryGetValue(player.userID, out var timer))
            {
                CuiHelper.DestroyUi(player, UI_Name);
                if (timer != null)
                    timer.Destroy();
                _playerTimers.Remove(player.userID);
            }
        }
        #endregion

        #region ~Oxide Hooks~
        void OnPlayerConnected(BasePlayer player) => InitPlayer(player);
        
        void OnEntitySpawned(VendingMachine vending)
        {
            if (!vending.OwnerID.IsSteamId() || !_storedData.PlayersData.TryGetValue(vending.OwnerID, out var playerData) || !CanModifyVending(vending, playerData)) return;
            if (playerData.AutoModify)
                ModifyVending(vending, true);
            else
                ShowPopup(vending);
        }
        
        object OnEntityKill(VendingMachine vending)
        {
            if (vending.skinID == _skinID && GetTerminalContainer(vending, out var inventory))
                inventory.Drop(ItemDropPrefab, vending.dropPosition, vending.transform.rotation, 0f);
            if (vending.OwnerID.IsSteamId() && _storedData.PlayersData.TryGetValue(vending.OwnerID, out var playerData))
                playerData.Vendings.Remove(vending.net.ID.Value);
            return null;
        }
        
        object OnRotateVendingMachine(VendingMachine vending, BasePlayer player)
        {
            if (vending.skinID == _skinID && !IsVendingAccessible(vending, new Vector3(0f, 1f, -1.2f)))
            {
                SendMessage(player.IPlayer, lang.GetMessage("MsgNotAccessible", this, player.UserIDString), true);
                return false;
            }
            return null;
        }
        
        object OnVendingTransaction(VendingMachine shopVending, BasePlayer buyer, int sellOrderId, int numberOfTransactions, ItemContainer targetContainer)
        {
            var marketTerminal = targetContainer?.GetEntityOwner() as MarketTerminal;
            var vending = marketTerminal?.GetParentEntity() as VendingMachine;
            if (marketTerminal != null && vending != null && vending.skinID == _skinID && !IsVendingAccessible(vending))
            {
                marketTerminal.ClientRPC(RpcTarget.Player("Client_CloseMarketUI", buyer));
                SendMessage(buyer.IPlayer, lang.GetMessage("MsgNotAccessible", this, buyer.UserIDString), true);
                return false;
            }
            return null;
        }
        
        object CanPurchaseItem(BasePlayer buyer, Item item, Action<BasePlayer, Item> onItemPurchased, VendingMachine shopVending, ItemContainer targetContainer)
        {
            if (targetContainer != null && targetContainer.GetEntityOwner() is MarketTerminal terminal && terminal.skinID == _skinID && !IsNPCVendingCustom(shopVending) && GetDeliveryFee(buyer.UserIDString, out var feeValue))
            {
                int deliveryFeeAmount = feeValue.Fee_Item_Amount;
                var deliveryFeeCurrency = ItemManager.FindItemDefinition(feeValue.Fee_Item_Definition);

                if (deliveryFeeCurrency != null)
                {
                    if (feeValue.Fee_Item_Definition != "scrap" || feeValue.Fee_Item_Amount != 20)
                    {
                        int takenAmount = buyer.inventory.Take(null, deliveryFeeCurrency.itemid, deliveryFeeAmount);
                        if (takenAmount != deliveryFeeAmount)
                        {
                            if (takenAmount > 0)
                            {
                                var defaultFeeItem = ItemManager.CreateByItemID(deliveryFeeCurrency.itemid, takenAmount, 0uL);
                                if (!buyer.inventory.GiveItem(defaultFeeItem))
                                    defaultFeeItem.Drop(buyer.inventory.containerMain.dropPosition, buyer.inventory.containerMain.dropVelocity);
                            }
                            SendMessage(buyer.IPlayer, string.Format(lang.GetMessage("MsgNoHaveCustomFee", this, buyer.UserIDString), new string[] { feeValue.Fee_Item_Definition, deliveryFeeAmount.ToString() }), true);
                        }
                        else
                        {
                            var defaultFeeItem = ItemManager.CreateByItemID(terminal.deliveryFeeCurrency.itemid, terminal.deliveryFeeAmount, 0uL);
                            if (!buyer.inventory.GiveItem(defaultFeeItem))
                                defaultFeeItem.Drop(buyer.inventory.containerMain.dropPosition, buyer.inventory.containerMain.dropVelocity);
                        }

                        Facepunch.Rust.Analytics.Server.VendingMachineTransaction(null, item.info, item.amount);
                        if (!item.MoveToContainer(targetContainer))
                            item.Drop(targetContainer.dropPosition, targetContainer.dropVelocity);
                        onItemPurchased?.Invoke(buyer, item);
                        return true;
                    }
                }
                else
                    PrintWarning($"Could not find an item with the name {feeValue.Fee_Item_Definition} for user {buyer.displayName}. Using default fee settings.");
            }
            return null;
        }
        
        void OnUserPermissionRevoked(string id, string permName)
        {
            if (_config.PermissionsLimits.ContainsKey(permName) && ulong.TryParse(id, out ulong userID) && _storedData.PlayersData.TryGetValue(userID, out var playerData) && !permission.UserHasPermission(id, PERMISSION_ADMIN))
                RemoveVendingOnRevoke(userID, playerData);
        }
        
        void OnGroupPermissionRevoked(string groupName, string permName)
        {
            if (!_config.PermissionsLimits.ContainsKey(permName) || !permission.GroupExists(groupName)) return;
            foreach (var userStr in permission.GetUsersInGroup(groupName))
            {
                var userIDString = userStr.Substring(0, userStr.IndexOf('(')).Trim();
                if (ulong.TryParse(userIDString, out var userID) && _storedData.PlayersData.TryGetValue(userID, out var playerData) &&
                    permission.UserHasPermission(userIDString, PERMISSION_ADMIN) && !permission.UserHasPermission(userIDString, permName))
                {
                    RemoveVendingOnRevoke(userID, playerData);
                }
            }
        }
        
        void Init()
        {
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnRotateVendingMachine));
            Unsubscribe(nameof(OnVendingTransaction));
            Unsubscribe(nameof(CanPurchaseItem));
            Unsubscribe(nameof(OnUserPermissionRevoked));
            Unsubscribe(nameof(OnGroupPermissionRevoked));
            Unsubscribe(nameof(OnServerSave));
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            AddCovalenceCommand(_config.Command, nameof(vDelivery_Command));
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }
        
        void OnServerInitialized()
        {
            foreach (var perm in _config.PermissionsLimits.Keys)
            {
                if (perm.StartsWith("vDelivery"))
                    permission.RegisterPermission(perm, this);
            }
            LoadImage();
            
            var rList = Pool.Get<List<ulong>>();
            foreach (var kvp in _storedData.PlayersData)
            {
                var playerData = kvp.Value;
                int counter = 0, limit = GetHighestLimit(kvp.Key.ToString());
                foreach (var netID in playerData.Vendings)
                {
                    var vending = BaseNetworkable.serverEntities.Find(new NetworkableId(netID)) as VendingMachine;
                    if (vending != null && !vending.IsDestroyed)
                    {
                        counter++;
                        if (limit != -1 && counter > limit)
                        {
                            rList.Add(netID);
                            continue;
                        }
                        ModifyVending(vending);
                    }
                    else
                        rList.Add(netID);
                }
                foreach (var netID in rList)
                    playerData.Vendings.Remove(netID);
                rList.Clear();
            }
            Pool.FreeUnmanaged(ref rList);
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID.IsSteamId())
                    InitPlayer(player);
            }
            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityKill));
            if (!_config.Ignore_Accessibility)
            {
                Subscribe(nameof(OnRotateVendingMachine));
                Subscribe(nameof(OnVendingTransaction));
            }
            Subscribe(nameof(CanPurchaseItem));
            Subscribe(nameof(OnUserPermissionRevoked));
            Subscribe(nameof(OnGroupPermissionRevoked));
            Subscribe(nameof(OnServerSave));
        }
        
        void OnServerSave() => SaveData();
        #endregion

        #region ~Commands~
        private void vDelivery_Command(IPlayer player, string command, string[] args)
        {
            bool isAdmin = permission.UserHasPermission(player.Id, PERMISSION_ADMIN);
            string replyKey = string.Empty;
            string[] replyArgs = new string[5];
            bool isWarning = false;

            if (args != null && args.Length > 0)
            {
                if (player.IsServer && args[0] == "clear")
                {
                    ClearEntities();
                    player.Reply($"[{Title}] {lang.GetMessage("MsgAllRemoved", this, player.Id)}");
                    return;
                }
                if (player.Object is BasePlayer bPlayer && _storedData.PlayersData.TryGetValue(bPlayer.userID, out var playerData))
                {
                    GetVendingFromArgs(bPlayer, args, out var vending);
                    if (args[0] == "add")
                    {
                        if (vending == null)
                        {
                            replyKey = "MsgNotVending";
                            isWarning = true;
                        }
                        else if (!isAdmin && vending.OwnerID != bPlayer.userID)
                        {
                            replyKey = "MsgNotOwner";
                            isWarning = true;
                        }
                        else if (CanModifyVending(vending, playerData, player))
                            ModifyVending(vending, true);
                    }
                    else if (args[0] == "remove")
                    {
                        if (vending == null)
                        {
                            replyKey = "MsgNotVending";
                            isWarning = true;
                        }
                        else if (!IsVendingHasTerminal(vending))
                        {
                            replyKey = "MsgNotVendingDelivery";
                            isWarning = true;
                        }
                        else if (!isAdmin && vending.OwnerID != bPlayer.userID)
                        {
                            replyKey = "MsgNotOwner";
                            isWarning = true;
                        }
                        else
                        {
                            RemoveFromVending(vending);
                            replyKey = "MsgMyRemoved";
                        }
                    }
                    else if (args[0] == "clear")
                    {
                        if (isAdmin && args.Length > 1)
                        {
                            if (args[1] == "all")
                            {
                                ClearEntities();
                                replyKey = "MsgAllRemoved";
                            }
                            else if (ulong.TryParse(args[1], out ulong targetID))
                            {
                                foreach (var baseNet in BaseNetworkable.serverEntities)
                                {
                                    if (baseNet is VendingMachine tVending && tVending.OwnerID == targetID && (tVending.skinID == _skinID || IsVendingHasTerminal(tVending)))
                                        RemoveFromVending(tVending);
                                }
                                replyKey = "MsgPlayerAllRemoved";
                                replyArgs[0] = args[1];
                            }
                        }
                        else
                        {
                            foreach (var netID in playerData.Vendings.ToList())
                            {
                                if (BaseNetworkable.serverEntities.Find(new NetworkableId(netID)) is VendingMachine tVending)
                                    RemoveFromVending(tVending);
                                else
                                    playerData.Vendings.Remove(netID);
                            }
                            replyKey = "MsgMyAllRemoved";
                        }
                    }
                    else if (args[0] == "auto")
                    {
                        playerData.AutoModify = !playerData.AutoModify;
                        replyKey = playerData.AutoModify ? "MsgAutoModifyEntityEnabled" : "MsgAutoModifyEntityDisabled";
                        isWarning = playerData.AutoModify;
                    }
                }
            }
            
            if (!string.IsNullOrWhiteSpace(replyKey))
                SendMessage(player, string.Format(lang.GetMessage(replyKey, this, player.Id), replyArgs), isWarning);
        }
        #endregion

        #region ~UI~
        private void ShowPopup(VendingMachine vending)
        {
            if (BasePlayer.FindByID(vending.OwnerID) is not BasePlayer player) return;
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = _config.Popup_AnchorMin, AnchorMax = _config.Popup_AnchorMax, OffsetMin = _config.Popup_OffsetMin, OffsetMax = _config.Popup_OffsetMax },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", UI_Name);
            container.Add(new CuiElement
            {
                Parent = UI_Name,
                Components =
                {
                    new CuiImageComponent { Color = _iconColor, Png = (string)(ImageLibrary?.Call("GetImage", UI_Name) ?? string.Empty) },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "7 -17.5", OffsetMax = "42 17.5" }
                }
            });
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = lang.GetMessage("MsgPopupText", this, player.UserIDString),
                    Font = _config.Popup_Text_Font,
                    FontSize = _config.Popup_Text_Font_Size,
                    Color = _textColor,
                    Align = TextAnchor.UpperLeft,
                    FadeIn = _config.Popup_Text_FadeIn
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "50 7", OffsetMax = "-5 -7" }
            }, UI_Name);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = lang.GetMessage("MsgPopupSubText", this, player.UserIDString),
                    Font = _config.Popup_Text_Font,
                    FontSize = _config.Popup_Desc_Font_Size,
                    Color = _text2Color,
                    Align = TextAnchor.LowerLeft,
                    FadeIn = _config.Popup_Text_FadeIn
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "50 7", OffsetMax = "-5 -7" }
            }, UI_Name);
            container.Add(new CuiButton
            {
                Button =
                {
                    Close = UI_Name,
                    Command = $"{_config.Command} add {vending.net.ID.Value}",
                    Color = "0 0 0 0"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, UI_Name);

            DestroyPopup(player);
            CuiHelper.AddUi(player, container);
            SendEffect(player.transform.position, player.Connection, _config.Popup_Sound_Prefab);
            _playerTimers[player.userID] = timer.Once(_config.Popup_Duration, () => { DestroyPopup(player); });
        }
        #endregion

        #region ~Unload~
        void Unload()
        {
            Unsubscribe(nameof(OnServerSave));
            OnServerSave();
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPopup(player);
            ClearEntities(true);
            _storedData = null;
            _config = null;
        }
        #endregion
    }
}
