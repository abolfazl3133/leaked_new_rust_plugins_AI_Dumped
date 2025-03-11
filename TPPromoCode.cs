using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TPPromoCode", "N1KTO", "1.0.0")]
    [Description("Система промо-кодов с наградами и интерфейсом")]
    public class TPPromoCode : RustPlugin
    {
        #region Fields
        private Configuration config;
        private const string PermissionUse = "tppromocode.use";
        private const string PermissionAdmin = "tppromocode.admin";
        private Dictionary<ulong, HashSet<string>> usedCodes = new Dictionary<ulong, HashSet<string>>();
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Включить оповещения в чат")]
            public bool EnablePromoCodeBroadcast = true;

            [JsonProperty("Префикс сообщений")]
            public string MessagePrefix = "<color=#0057b8>[PromoCode]</color>";

            [JsonProperty("Промо-коды")]
            public Dictionary<string, PromoCodeData> PromoCodes = new Dictionary<string, PromoCodeData>
            {
                ["BONUS"] = new PromoCodeData
                {
                    Reward = "give {steamid} stones 500",
                    MaxActivations = 100,
                    ExpirationDate = "2024-12-31"
                },
                ["CONGRATULATIONS"] = new PromoCodeData
                {
                    Reward = "give {steamid} wood 1000",
                    MaxActivations = 50,
                    ExpirationDate = "2024-12-31"
                }
            };
        }

        public class PromoCodeData
        {
            [JsonProperty("Команда награды")]
            public string Reward { get; set; }

            [JsonProperty("Максимум активаций")]
            public int MaxActivations { get; set; }

            [JsonProperty("Дата окончания (YYYY-MM-DD)")]
            public string ExpirationDate { get; set; }

            [JsonProperty("Текущее количество активаций")]
            public int CurrentActivations { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("Ошибка чтения конфигурации! Создаю новую...");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
            LoadData();
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();
        #endregion

        #region Data Management
        private void LoadData()
        {
            usedCodes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, HashSet<string>>>("TPPromoCode_UsedCodes") 
                       ?? new Dictionary<ulong, HashSet<string>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("TPPromoCode_UsedCodes", usedCodes);
        }
        #endregion

        #region Commands
        [ChatCommand("promo")]
        private void PromoCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                SendMessage(player, "NoPermission");
                return;
            }

            if (args.Length == 0)
            {
                ShowPromoUI(player);
                return;
            }

            string code = args[0].ToUpper();
            ActivatePromoCode(player, code);
        }

        [ConsoleCommand("promocode.activate")]
        private void PromoCodeActivateCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (arg.Args == null || arg.Args.Length == 0) return;

            string code = arg.Args[0].ToUpper();
            ActivatePromoCode(player, code);
        }
        #endregion

        #region Core Methods
        private void ActivatePromoCode(BasePlayer player, string code)
        {
            if (!config.PromoCodes.ContainsKey(code))
            {
                SendMessage(player, "InvalidCode");
                return;
            }

            var promoData = config.PromoCodes[code];

            if (!usedCodes.ContainsKey(player.userID))
                usedCodes[player.userID] = new HashSet<string>();

            if (usedCodes[player.userID].Contains(code))
            {
                SendMessage(player, "AlreadyUsed");
                return;
            }

            if (promoData.CurrentActivations >= promoData.MaxActivations)
            {
                SendMessage(player, "MaxActivationsReached");
                return;
            }

            if (DateTime.TryParse(promoData.ExpirationDate, out DateTime expDate) && DateTime.Now > expDate)
            {
                SendMessage(player, "CodeExpired");
                return;
            }

            string reward = promoData.Reward.Replace("{steamid}", player.UserIDString);
            Server.Command(reward);

            usedCodes[player.userID].Add(code);
            promoData.CurrentActivations++;

            if (config.EnablePromoCodeBroadcast)
            {
                string message = $"{config.MessagePrefix} {player.displayName} активировал промо-код {code}!";
                Server.Broadcast(message);
            }

            SendMessage(player, "Success");
            SaveData();
        }
        #endregion

        #region UI Methods
        private void ShowPromoUI(BasePlayer player)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
                Image = { Color = "0 0 0 0.9" }
            }, "Overlay", "PromoCodeUI");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 0.95" },
                Text = { Text = "Активация промо-кода", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "PromoCodeUI");

            elements.Add(new CuiElement
            {
                Parent = "PromoCodeUI",
                Components =
                {
                    new CuiInputFieldComponent { Command = "promocode.activate", FontSize = 14, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.5" }
                }
            });

            elements.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3 0.2", AnchorMax = "0.7 0.3" },
                Button = { Command = "cui.cancel", Color = "0.7 0.7 0.7 1" },
                Text = { Text = "Закрыть", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "PromoCodeUI");

            CuiHelper.DestroyUi(player, "PromoCodeUI");
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет прав на использование этой команды!",
                ["InvalidCode"] = "Неверный промо-код!",
                ["AlreadyUsed"] = "Вы уже использовали этот промо-код!",
                ["MaxActivationsReached"] = "Этот промо-код больше не действителен (достигнут лимит активаций)!",
                ["CodeExpired"] = "Срок действия промо-кода истек!",
                ["Success"] = "Промо-код успешно активирован!"
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command!",
                ["InvalidCode"] = "Invalid promo code!",
                ["AlreadyUsed"] = "You have already used this promo code!",
                ["MaxActivationsReached"] = "This promo code is no longer valid (activation limit reached)!",
                ["CodeExpired"] = "This promo code has expired!",
                ["Success"] = "Promo code successfully activated!"
            }, this);
        }

        private void SendMessage(BasePlayer player, string key)
        {
            if (player == null) return;
            Player.Message(player, lang.GetMessage(key, this, player.UserIDString), config.MessagePrefix);
        }
        #endregion
    }
} 