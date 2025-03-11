using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BlackjackPlugin", "YourName", "1.0.0")]
    [Description("Игра в блэкджек с настраиваемой колодой карт")]
    public class BlackjackPlugin : RustPlugin
    {
        #region Поля
        private Configuration config;
        private Dictionary<ulong, GameState> playerGames = new Dictionary<ulong, GameState>();
        private const string PERMISSION_USE = "blackjack.use";
        private Plugin economicsPlugin;
        private Plugin rewardsPlugin;
        #endregion

        #region Конфигурация
        private class Configuration
        {
            public BetSettings Betting { get; set; }
            public ChatSettings Chat { get; set; }
            public List<Card> CardDeck { get; set; }

            public class BetSettings
            {
                [JsonProperty("Use Economics")]
                public bool UseEconomics { get; set; }

                [JsonProperty("Use Server Rewards")]
                public bool UseServerRewards { get; set; }

                [JsonProperty("Use Item")]
                public bool UseItem { get; set; }

                [JsonProperty("Item Shortname")]
                public string ItemShortname { get; set; }

                [JsonProperty("Item Skin ID")]
                public ulong ItemSkinId { get; set; }

                [JsonProperty("Minimum Bet")]
                public int MinimumBet { get; set; }

                [JsonProperty("Maximum Bet")]
                public int MaximumBet { get; set; }
            }

            public class ChatSettings
            {
                [JsonProperty("Message Prefix")]
                public string Prefix { get; set; }

                [JsonProperty("Message Icon (Steam ID)")]
                public ulong Icon { get; set; }
            }

            public class Card
            {
                [JsonProperty("cardImage")]
                public string CardImage { get; set; }

                [JsonProperty("cardType")]
                public string CardType { get; set; }

                [JsonProperty("cardValue")]
                public int CardValue { get; set; }
            }
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
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                Betting = new Configuration.BetSettings
                {
                    UseEconomics = false,
                    UseServerRewards = false,
                    UseItem = true,
                    ItemShortname = "scrap",
                    ItemSkinId = 0,
                    MinimumBet = 500,
                    MaximumBet = 2000
                },
                Chat = new Configuration.ChatSettings
                {
                    Prefix = "<color=#FFD700>[Billys Blackjack]</color>",
                    Icon = 76561198194158447
                },
                CardDeck = new List<Configuration.Card>()
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            cmd.AddChatCommand("blackjack", this, "CmdBlackjack");
            cmd.AddChatCommand("bj", this, "CmdBlackjack");
        }

        private void OnServerInitialized(bool initial)
        {
            if (config.Betting.UseEconomics)
            {
                economicsPlugin = plugins.Find("Economics");
                if (economicsPlugin == null)
                {
                    PrintError("Economics plugin not found! Disabling Economics support.");
                    config.Betting.UseEconomics = false;
                }
            }

            if (config.Betting.UseServerRewards)
            {
                rewardsPlugin = plugins.Find("ServerRewards");
                if (rewardsPlugin == null)
                {
                    PrintError("ServerRewards plugin not found! Disabling ServerRewards support.");
                    config.Betting.UseServerRewards = false;
                }
            }
        }

        private void Unload()
        {
            foreach (var player in playerGames.Keys.ToList())
            {
                CloseUI(BasePlayer.FindByID(player));
            }
        }
        #endregion

        #region Команды
        [ChatCommand("blackjack")]
        private void CmdBlackjack(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                SendMessage(player.IPlayer, "У вас нет разрешения на использование этой команды");
                return;
            }

            ShowUI(player);
        }

        [ChatCommand("blackjack.bet")]
        private void CmdBet(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0 || !int.TryParse(args[0], out int betAmount))
            {
                SendMessage(player.IPlayer, "Пожалуйста, укажите сумму ставки");
                return;
            }

            if (betAmount < config.Betting.MinimumBet || betAmount > config.Betting.MaximumBet)
            {
                SendMessage(player.IPlayer, $"Ставка должна быть между {config.Betting.MinimumBet} и {config.Betting.MaximumBet}");
                return;
            }

            if (!TryPlaceBet(player, betAmount))
                return;

            StartNewGame(player, betAmount);
        }

        private bool TryPlaceBet(BasePlayer player, int amount)
        {
            if (config.Betting.UseEconomics && economicsPlugin != null)
            {
                double balance = (double)economicsPlugin.Call("GetBalance", player.UserIDString);
                if (balance < amount)
                {
                    SendMessage(player.IPlayer, "У вас недостаточно денег для этой ставки");
                    return false;
                }

                economicsPlugin.Call("Withdraw", player.UserIDString, (double)amount);
                return true;
            }

            if (config.Betting.UseServerRewards && rewardsPlugin != null)
            {
                int points = (int)rewardsPlugin.Call("CheckPoints", player.userID);
                if (points < amount)
                {
                    SendMessage(player.IPlayer, "У вас недостаточно очков для этой ставки");
                    return false;
                }

                rewardsPlugin.Call("TakePoints", player.userID, amount);
                return true;
            }

            if (config.Betting.UseItem)
            {
                var items = player.inventory.containerMain.itemList
                    .Where(item => item.info.shortname == config.Betting.ItemShortname)
                    .ToList();
                
                var totalAmount = items.Sum(item => item.amount);
                
                if (totalAmount < amount)
                {
                    SendMessage(player.IPlayer, $"У вас недостаточно {config.Betting.ItemShortname}");
                    return false;
                }

                var remainingBet = amount;
                foreach (var item in items)
                {
                    if (remainingBet <= 0) break;
                    
                    var amountToTake = System.Math.Min(remainingBet, item.amount);
                    if (amountToTake >= item.amount)
                    {
                        remainingBet -= item.amount;
                        item.RemoveFromContainer();
                        continue;
                    }

                    item.amount -= amountToTake;
                    remainingBet -= amountToTake;
                    item.MarkDirty();
                }

                return true;
            }

            return false;
        }

        [ChatCommand("blackjack.hit")]
        private void CmdHit(BasePlayer player, string command, string[] args)
        {
            if (!playerGames.TryGetValue(player.userID, out var game) || !game.IsPlaying)
            {
                SendMessage(player.IPlayer, "У вас нет активной игры");
                return;
            }

            game.PlayerHand.Add(DrawCard());
            game.CanDoubleDown = false;

            var playerValue = CalculateHandValue(game.PlayerHand);
            if (playerValue > 21)
            {
                EndGame(player, false);
                return;
            }

            UpdateUI(player);
        }

        [ChatCommand("blackjack.stand")]
        private void CmdStand(BasePlayer player, string command, string[] args)
        {
            if (!playerGames.TryGetValue(player.userID, out var game) || !game.IsPlaying)
            {
                SendMessage(player.IPlayer, "У вас нет активной игры");
                return;
            }

            var dealerValue = CalculateHandValue(game.DealerHand);
            while (dealerValue < 17)
            {
                game.DealerHand.Add(DrawCard());
                dealerValue = CalculateHandValue(game.DealerHand);
            }

            var playerValue = CalculateHandValue(game.PlayerHand);
            
            if (dealerValue > 21 || playerValue > dealerValue)
                EndGame(player, true);
            else if (dealerValue == playerValue)
                EndGame(player, null);
            else
                EndGame(player, false);
        }

        [ChatCommand("blackjack.doubledown")]
        private void CmdDoubleDown(BasePlayer player, string command, string[] args)
        {
            if (!playerGames.TryGetValue(player.userID, out var game) || !game.IsPlaying)
            {
                SendMessage(player.IPlayer, "У вас нет активной игры");
                return;
            }

            if (!game.CanDoubleDown)
            {
                SendMessage(player.IPlayer, "Вы не можете удвоить ставку в данный момент");
                return;
            }

            if (config.Betting.UseItem)
            {
                var items = player.inventory.containerMain.itemList
                    .Where(item => item.info.shortname == config.Betting.ItemShortname)
                    .ToList();
                
                var totalAmount = items.Sum(item => item.amount);
                
                if (totalAmount < game.CurrentBet)
                {
                    SendMessage(player.IPlayer, $"У вас недостаточно {config.Betting.ItemShortname} для удвоения ставки");
                    return;
                }

                var remainingBet = game.CurrentBet;
                foreach (var item in items)
                {
                    if (remainingBet <= 0) break;
                    
                    var amountToTake = System.Math.Min(remainingBet, item.amount);
                    if (amountToTake >= item.amount)
                    {
                        remainingBet -= item.amount;
                        item.RemoveFromContainer();
                        continue;
                    }

                    item.amount -= amountToTake;
                    remainingBet -= amountToTake;
                    item.MarkDirty();
                }
            }

            game.CurrentBet *= 2;
            game.PlayerHand.Add(DrawCard());
            
            var playerValue = CalculateHandValue(game.PlayerHand);
            if (playerValue > 21)
            {
                EndGame(player, false);
                return;
            }

            CmdStand(player, command, args);
        }

        private void EndGame(BasePlayer player, bool? playerWon)
        {
            if (!playerGames.TryGetValue(player.userID, out var game))
                return;

            var message = playerWon switch
            {
                true => GetWinMessage(game.CurrentBet * 2),
                false => GetLoseMessage(game.CurrentBet),
                null => GetDrawMessage(game.CurrentBet)
            };

            if (playerWon == true || playerWon == null)
            {
                var winAmount = playerWon == true ? game.CurrentBet * 2 : game.CurrentBet;
                GiveReward(player, winAmount);
            }

            SendMessage(player.IPlayer, message);
            game.IsPlaying = false;
            UpdateUI(player);
        }

        private string GetWinMessage(int amount)
        {
            if (config.Betting.UseEconomics)
                return $"Вы выиграли ${amount}!";
            if (config.Betting.UseServerRewards)
                return $"Вы выиграли {amount} очков!";
            return $"Вы выиграли {amount} {config.Betting.ItemShortname}!";
        }

        private string GetLoseMessage(int amount)
        {
            if (config.Betting.UseEconomics)
                return $"Вы проиграли ${amount}";
            if (config.Betting.UseServerRewards)
                return $"Вы проиграли {amount} очков";
            return $"Вы проиграли {amount} {config.Betting.ItemShortname}";
        }

        private string GetDrawMessage(int amount)
        {
            if (config.Betting.UseEconomics)
                return $"Ничья! Возврат ${amount}";
            if (config.Betting.UseServerRewards)
                return $"Ничья! Возврат {amount} очков";
            return $"Ничья! Возврат {amount} {config.Betting.ItemShortname}";
        }

        private void GiveReward(BasePlayer player, int amount)
        {
            if (config.Betting.UseEconomics && economicsPlugin != null)
            {
                economicsPlugin.Call("Deposit", player.UserIDString, (double)amount);
                return;
            }

            if (config.Betting.UseServerRewards && rewardsPlugin != null)
            {
                rewardsPlugin.Call("GivePoints", player.userID, amount);
                return;
            }

            if (config.Betting.UseItem)
            {
                var item = ItemManager.CreateByName(config.Betting.ItemShortname, amount, config.Betting.ItemSkinId);
                if (!player.inventory.GiveItem(item))
                    item.Drop(player.eyes.position, player.eyes.BodyForward() * 2f);
            }
        }
        #endregion

        #region Игровая логика
        private class GameState
        {
            public List<Configuration.Card> PlayerHand { get; set; } = new List<Configuration.Card>();
            public List<Configuration.Card> DealerHand { get; set; } = new List<Configuration.Card>();
            public int CurrentBet { get; set; }
            public bool IsPlaying { get; set; }
            public bool CanDoubleDown { get; set; }
        }

        private void StartNewGame(BasePlayer player, int bet)
        {
            if (!playerGames.ContainsKey(player.userID))
                playerGames[player.userID] = new GameState();

            var game = playerGames[player.userID];
            game.PlayerHand.Clear();
            game.DealerHand.Clear();
            game.CurrentBet = bet;
            game.IsPlaying = true;
            game.CanDoubleDown = true;

            DealInitialCards(game);
            UpdateUI(player);
        }

        private void DealInitialCards(GameState game)
        {
            for (int i = 0; i < 2; i++)
            {
                game.PlayerHand.Add(DrawCard());
                game.DealerHand.Add(DrawCard());
            }
        }

        private Configuration.Card DrawCard()
        {
            var deck = config.CardDeck;
            if (deck == null || deck.Count == 0) return null;
            
            var randomIndex = UnityEngine.Random.Range(0, deck.Count);
            return deck[randomIndex];
        }

        private int CalculateHandValue(List<Configuration.Card> hand)
        {
            int value = 0;
            int aceCount = 0;

            foreach (var card in hand)
            {
                if (card.CardType == "Ace")
                    aceCount++;
                else
                    value += card.CardValue;
            }

            for (int i = 0; i < aceCount; i++)
            {
                if (value + 11 <= 21)
                    value += 11;
                else
                    value += 1;
            }

            return value;
        }
        #endregion

        #region UI
        private void ShowUI(BasePlayer player)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.9" },
                RectTransform = { AnchorMin = "0.2 0.1", AnchorMax = "0.8 0.9" },
                CursorEnabled = true
            }, "Overlay", "blackjack_main");

            elements.Add(new CuiLabel
            {
                Text = { Text = "Blackjack", FontSize = 20, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" }
            }, "blackjack_main");

            if (!playerGames.ContainsKey(player.userID) || !playerGames[player.userID].IsPlaying)
            {
                AddBettingUI(elements, player);
            }
            else
            {
                AddGameUI(elements, player);
            }

            CuiHelper.DestroyUi(player, "blackjack_main");
            CuiHelper.AddUi(player, elements);
        }

        private void AddBettingUI(CuiElementContainer elements, BasePlayer player)
        {
            elements.Add(new CuiLabel
            {
                Text = { Text = $"Минимальная ставка: {config.Betting.MinimumBet}\nМаксимальная ставка: {config.Betting.MaximumBet}", FontSize = 14, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.1 0.7", AnchorMax = "0.9 0.8" }
            }, "blackjack_main");

            for (int i = 0; i < 4; i++)
            {
                var bet = config.Betting.MinimumBet * (i + 1);
                if (bet > config.Betting.MaximumBet) break;

                var xMin = 0.2f + (i * 0.15f);
                var xMax = xMin + 0.14f;

                elements.Add(new CuiButton
                {
                    Button = { Command = $"blackjack.bet {bet}", Color = "0.7 0.7 0.7 1" },
                    RectTransform = { AnchorMin = $"{xMin} 0.5", AnchorMax = $"{xMax} 0.6" },
                    Text = { Text = bet.ToString(), FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, "blackjack_main");
            }
        }

        private void AddGameUI(CuiElementContainer elements, BasePlayer player)
        {
            var game = playerGames[player.userID];

            elements.Add(new CuiLabel
            {
                Text = { Text = "Карты дилера:", FontSize = 14, Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = "0.1 0.7", AnchorMax = "0.9 0.8" }
            }, "blackjack_main");

            // Отображение карт дилера
            for (int i = 0; i < game.DealerHand.Count; i++)
            {
                var xMin = 0.1f + (i * 0.15f);
                var xMax = xMin + 0.14f;

                elements.Add(new CuiElement
                {
                    Parent = "blackjack_main",
                    Components =
                    {
                        new CuiRawImageComponent { Url = game.DealerHand[i].CardImage },
                        new CuiRectTransformComponent { AnchorMin = $"{xMin} 0.55", AnchorMax = $"{xMax} 0.7" }
                    }
                });
            }

            elements.Add(new CuiLabel
            {
                Text = { Text = $"Сумма карт дилера: {CalculateHandValue(game.DealerHand)}", FontSize = 14, Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = "0.1 0.5", AnchorMax = "0.9 0.55" }
            }, "blackjack_main");

            elements.Add(new CuiLabel
            {
                Text = { Text = "Ваши карты:", FontSize = 14, Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.45" }
            }, "blackjack_main");

            // Отображение карт игрока
            for (int i = 0; i < game.PlayerHand.Count; i++)
            {
                var xMin = 0.1f + (i * 0.15f);
                var xMax = xMin + 0.14f;

                elements.Add(new CuiElement
                {
                    Parent = "blackjack_main",
                    Components =
                    {
                        new CuiRawImageComponent { Url = game.PlayerHand[i].CardImage },
                        new CuiRectTransformComponent { AnchorMin = $"{xMin} 0.25", AnchorMax = $"{xMax} 0.4" }
                    }
                });
            }

            elements.Add(new CuiLabel
            {
                Text = { Text = $"Ваша сумма карт: {CalculateHandValue(game.PlayerHand)}", FontSize = 14, Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.9 0.25" }
            }, "blackjack_main");

            if (game.CanDoubleDown)
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "blackjack.doubledown", Color = "0.7 0.7 0.7 1" },
                    RectTransform = { AnchorMin = "0.1 0.1", AnchorMax = "0.3 0.15" },
                    Text = { Text = "Double Down", FontSize = 12, Align = TextAnchor.MiddleCenter }
                }, "blackjack_main");
            }

            elements.Add(new CuiButton
            {
                Button = { Command = "blackjack.hit", Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.4 0.1", AnchorMax = "0.6 0.15" },
                Text = { Text = "Hit", FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, "blackjack_main");

            elements.Add(new CuiButton
            {
                Button = { Command = "blackjack.stand", Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.7 0.1", AnchorMax = "0.9 0.15" },
                Text = { Text = "Stand", FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, "blackjack_main");

            elements.Add(new CuiLabel
            {
                Text = { Text = $"Текущая ставка: {game.CurrentBet}", FontSize = 14, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.2" }
            }, "blackjack_main");
        }

        private void CloseUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "blackjack_main");
        }

        private void UpdateUI(BasePlayer player)
        {
            CloseUI(player);
            ShowUI(player);
        }
        #endregion

        #region Вспомогательные методы
        private void SendMessage(IPlayer player, string message)
        {
            player.Reply($"{config.Chat.Prefix} {message}");
        }
        #endregion
    }
} 