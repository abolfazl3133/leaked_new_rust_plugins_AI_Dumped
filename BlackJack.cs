//Requires: Casino
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BlackJack", "k1lly0u", "0.1.2")]
    [Description("A Black Jack card game used with the Casino plugin")]
    class BlackJack : RustPlugin
    {
        #region Fields
        public static BlackJack Instance { get; private set; }       
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;
            lang.RegisterMessages(Messages, this);
        }

        private void OnServerInitialized()
        {
            Casino.Instance.RegisterGame(Casino.GameType.BlackJack, InitializeGame);
        }

        private void Unload()
        {
            Casino.Instance?.UnregisterGame(Casino.GameType.BlackJack);
            Instance = null;
        }
        #endregion

        #region Functions
        private void InitializeGame(BaseEntity table, Casino.StoredData.GameData gameData)
        {
            BlackJackGame blackjackGame = table.gameObject.AddComponent<BlackJackGame>();
            blackjackGame.InitializeGame(gameData);
        }
        #endregion

        #region Game Manager
        private const string GAME_BG = "blackjack.background";

        private const string PLAYER_OVERLAY = "blackjack.players";

        private const string BALANCE_OVERLAY = "blackjack.balance";

        private const string BETTING_OVERLAY = "blackjack.betting";

        private const string MESSAGE_OVERLAY = "blackjack.message";

        private const string NOTIFICATION_OVERLAY = "blackjack.notification";

        private const string PLAY_OVERLAY = "blackjack.play";

        private const string STATUS_OVERLAY = "blackjack.status";

        private static string BLACK_COLOR = Casino.UI.Color("000000", 0.7f);

        public class BlackJackGame : Casino.CardGame
        {            
            private BlackJackAI DealerAI; 

            private Casino.Deck deck = new Casino.Deck();

            private CuiElementContainer backgroundContainer;

            private Dictionary<string, CuiElementContainer> containers = new Dictionary<string, CuiElementContainer>();

            private int playerIndex = -1;

            internal readonly Casino.UI4[] cardPositions = new Casino.UI4[]
            {
                new Casino.UI4(0.315f, 0.15f, 0.38f, 0.33f),
                new Casino.UI4(0.55f, 0.15f, 0.615f, 0.33f),
                new Casino.UI4(0.1f, 0.25f, 0.165f, 0.43f),
                new Casino.UI4(0.765f, 0.25f, 0.83f, 0.43f),
                new Casino.UI4(0.435f, 0.7f, 0.5f, 0.88f)
            };
            
            public override void OnPlayerEnter(BasePlayer player, int position)
            {
                BlackJackPlayer blackjackPlayer = player.gameObject.AddComponent<BlackJackPlayer>();
                cardPlayers[position] = blackjackPlayer;

                blackjackPlayer.Position = position;

                blackjackPlayer.AddUI(GAME_BG, backgroundContainer);
                                
                if (gameState == Casino.GameState.Waiting)
                {
                    CreatePlayerUI();

                    if (CurrentPlayerCount == 1)
                    {
                        CreateUIMessage(string.Format(msg("UI.Notification.StartsIn"), 10));

                        if (!InvokeHandler.IsInvoking(this, PlaceBets))
                            InvokeHandler.Invoke(this, PlaceBets, 10f);
                    }
                    else CreateUIMessage(msg("UI.Notification.IsStarting"));
                }
                else
                {
                    if (gameState == Casino.GameState.PlacingBets)
                    {
                        CreatePlayerUI();

                        CreateUIMessage(blackjackPlayer, msg("UI.Notification.UsersPlacingBets"));

                        for (int i = 0; i < cardPlayers.Length; i++)
                        {
                            Casino.CardPlayer cardPlayer = cardPlayers[i];
                            if (cardPlayer == null || !cardPlayer.IsPlaying)
                                continue;

                            PlaceBets(cardPlayer as BlackJackPlayer);
                        }
                    }
                    else if (gameState == Casino.GameState.Prestart)
                    {
                        CreatePlayerUI(true);

                        CreateUIMessage(blackjackPlayer, msg("UI.Notification.GameInProgress"));

                        foreach (KeyValuePair<string, CuiElementContainer> kvp in containers)
                            blackjackPlayer.AddUI(kvp.Key, kvp.Value);
                    }
                    else
                    {
                        CreateUIMessage(blackjackPlayer, msg("UI.Notification.GameInProgress"));

                        foreach (KeyValuePair<string, CuiElementContainer> kvp in containers)
                            blackjackPlayer.AddUI(kvp.Key, kvp.Value);

                        CreatePlayerUI(blackjackPlayer, true);
                    }
                }
            }

            public override void OnPlayerExit(BasePlayer player)
            {
                BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
                if (blackjackPlayer == null)
                    return;

                OnPlayerExit(blackjackPlayer);
            }

            private void OnPlayerExit(BlackJackPlayer blackjackPlayer)
            {                
                if (blackjackPlayer.Position < 0 || blackjackPlayer.Position > cardPlayers.Length)
                {
                    int index = System.Array.IndexOf(cardPlayers, blackjackPlayer);
                    if (index != -1)
                        cardPlayers[index] = null;
                }
                else cardPlayers[blackjackPlayer.Position] = null;

                CreateUIMessageOffset(string.Format(msg("UI.Notification.LeftGame"), blackjackPlayer.Player.displayName), 5f);

                Destroy(blackjackPlayer);

                if (CurrentPlayingCount == 0 && gameState != Casino.GameState.Waiting)
                {
                    ResetGame();
                    return;
                }

                if (gameState == Casino.GameState.Playing)
                {
                    CreatePlayerUI(true);

                    if (playerIndex == blackjackPlayer.Position)
                    {
                        timer.StopTimer();
                        InvokeHandler.Invoke(this, NextPlayerTurn, 2f);
                    }
                    else
                    {
                        Casino.CardPlayer currentPlayer = cardPlayers[playerIndex];

                        if (currentPlayer != null && currentPlayer.IsPlaying)
                            CreatePlayOverlay(currentPlayer as BlackJackPlayer);
                    }
                }
                else
                {
                    if (gameState == Casino.GameState.PlacingBets)
                    {
                        for (int i = 0; i < cardPlayers.Length; i++)
                        {
                            Casino.CardPlayer cardPlayer = cardPlayers[i];
                            if (cardPlayer == null || !cardPlayer.IsPlaying)
                                continue;

                            PlaceBets(cardPlayer as BlackJackPlayer);
                        }
                    }
                    else CreatePlayerUI(gameState == Casino.GameState.Prestart);
                }
            }

            public override void OnGameInitialized()
            {
                this.gameType = Casino.GameType.BlackJack;

                DealerAI = new GameObject("Blackjack_Dealer").AddComponent<BlackJackAI>();
                DealerAI.Position = 4;
                DealerAI.RegisterCardGame(this);

                base.OnGameInitialized();

                backgroundContainer = Casino.UI.ElementContainer(GAME_BG, Casino.UI4.FullScreen, true);
                Casino.UI.Image(ref backgroundContainer, GAME_BG, PlayingCards.GetBoardImage("blackjack"), Casino.UI4.FullScreen);                
            }

            private void ResetGame()
            {
                timer.StopTimer();

                DealerAI.ResetHand();

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer != null)
                        cardPlayer.ResetHand();
                }

                playerIndex = -1;

                InvokeHandler.CancelInvoke(this, PlaceBets);

                InvokeHandler.CancelInvoke(this, PreStartRound);

                InvokeHandler.CancelInvoke(this, StartRound);

                InvokeHandler.CancelInvoke(this, NextPlayerTurn);

                InvokeHandler.CancelInvoke(this, DealerAI.PlayTurn);

                InvokeHandler.CancelInvoke(this, FinalizeGame);

                InvokeHandler.CancelInvoke(this, ResetGame);                

                DestroyUIElements();
                
                gameState = Casino.GameState.Waiting;

                deck.Shuffle();

                if (CurrentPlayerCount > 0)
                {
                    CreatePlayerUI();

                    CreateUIMessage(string.Format(msg("UI.Notification.StartsIn"), 5));

                    InvokeHandler.Invoke(this, PlaceBets, 5f);
                }
            }

            private void CreatePlayerUI(bool betsLocked = false)
            {
                CuiElementContainer container = CreatePlayerUIElement(betsLocked);

                AddUIElement(PLAYER_OVERLAY, container, false);                
            }

            private void CreatePlayerUI(Casino.CardPlayer targetPlayer, bool betsLocked = false)
            {
                CuiElementContainer container = CreatePlayerUIElement(betsLocked);

                targetPlayer.AddUI(PLAYER_OVERLAY, container);
            }

            private CuiElementContainer CreatePlayerUIElement(bool betsLocked)
            {
                CuiElementContainer container = Casino.UI.ElementContainer(PLAYER_OVERLAY, Casino.UI4.FullScreen);
                Casino.UI4 cardPosition;
                Casino.UI4 position;

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayers[i] == null)
                        continue;

                    cardPosition = cardPositions[i];
                    position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.035f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.01f);
                    Casino.UI.Panel(ref container, PLAYER_OVERLAY, BLACK_COLOR, position);
                    Casino.UI.Label(ref container, PLAYER_OVERLAY, cardPlayer.Player.displayName, 12, position, TextAnchor.MiddleCenter);

                    if (betsLocked)
                    {
                        position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.065f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.04f);
                        Casino.UI.Panel(ref container, PLAYER_OVERLAY, BLACK_COLOR, position);
                        Casino.UI.Label(ref container, PLAYER_OVERLAY, cardPlayer.IsPlaying ? string.Format(msg("UI.Player.Bet"), cardPlayer.BetAmount) : msg("UI.Player.PlayingNext"), 12, position, TextAnchor.MiddleCenter);

                        if (Instance.configData.ShowBalance)
                        {
                            CuiElementContainer balance = Casino.UI.ElementContainer(BALANCE_OVERLAY, new Casino.UI4(cardPosition.xMin, cardPosition.yMax + 0.01f, cardPosition.xMin + 0.12f, cardPosition.yMax + 0.035f));
                            Casino.UI.Panel(ref container, BALANCE_OVERLAY, BLACK_COLOR, Casino.UI4.FullScreen);
                            Casino.UI.Label(ref container, BALANCE_OVERLAY, string.Format(msg("UI.Player.BalanceAmount"), cardPlayer.BankBalance, FormatBetString(cardPlayer.Player)), 12, Casino.UI4.FullScreen, TextAnchor.MiddleCenter);
                            cardPlayer.AddUI(BALANCE_OVERLAY, balance);
                        }
                    }
                }

                cardPosition = cardPositions[4];
                position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.035f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.01f);
                Casino.UI.Panel(ref container, PLAYER_OVERLAY, BLACK_COLOR, position);
                Casino.UI.Label(ref container, PLAYER_OVERLAY, msg("UI.Dealer"), 12, position);

                Casino.UI.Button(ref container, PLAYER_OVERLAY, BLACK_COLOR, msg("UI.LeaveTable"), 14, new Casino.UI4(0.9f, 0.95f, 0.99f, 0.98f), "blackjack.leavetable");

                return container;
            }

            private void PlaceBets()
            {
                if (CurrentPlayerCount == 0)
                    return;

                DestroyUIElement(MESSAGE_OVERLAY);                

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer == null)
                        continue;

                    BlackJackPlayer blackjackPlayer = cardPlayer as BlackJackPlayer;
                    if (blackjackPlayer.BankBalance < gameData.minimumBet)
                    {
                        OnPlayerExit(blackjackPlayer);
                        blackjackPlayer.Player.ChatMessage(msg("Chat.Kicked.NoScrap", blackjackPlayer.Player.UserIDString));                        
                    }
                }

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer == null)
                        continue;

                    BlackJackPlayer blackjackPlayer = cardPlayer as BlackJackPlayer;

                    blackjackPlayer.IsPlaying = true;

                    PlaceBets(blackjackPlayer);
                }

                gameState = Casino.GameState.PlacingBets;

                timer.StartTimer(30);

                InvokeHandler.Invoke(this, PreStartRound, 30f);
            }

            internal void PlaceBets(BlackJackPlayer blackjackPlayer)
            {
                blackjackPlayer.DestroyUI(MESSAGE_OVERLAY);

                CuiElementContainer container = Casino.UI.ElementContainer(BETTING_OVERLAY, Casino.UI4.FullScreen);

                if (!blackjackPlayer.BetLocked)
                {
                    Casino.UI.Label(ref container, BETTING_OVERLAY, msg("UI.Notification.PlaceBets"), 20, new Casino.UI4(0.3f, 0.4f, 0.7f, 0.6f));

                    Casino.UI4 chipsPosition = new Casino.UI4(0.385f, 0.36f, 0.615f, 0.44f);
                    Casino.UI.Image(ref container, BETTING_OVERLAY, PlayingCards.GetChipStackImage(), chipsPosition);

                    for (int i = 0; i < 5; i++)
                    {
                        Casino.UI.Button(ref container, BETTING_OVERLAY, new Casino.UI4(chipsPosition.xMin + (i * 0.046f), chipsPosition.yMin, chipsPosition.xMax + (i * 0.046f), chipsPosition.yMax), $"blackjack.bet {i}");
                    }

                    Casino.UI.Panel(ref container, BETTING_OVERLAY, BLACK_COLOR, new Casino.UI4(0.385f, 0.33f, 0.4975f, 0.355f));
                    Casino.UI.Panel(ref container, BETTING_OVERLAY, BLACK_COLOR, new Casino.UI4(0.5005f, 0.33f, 0.615f, 0.355f));
                    Casino.UI.Label(ref container, BETTING_OVERLAY, string.Format(msg("UI.Player.BalanceAmount"), blackjackPlayer.BankBalance, FormatBetString(blackjackPlayer.Player)), 12, new Casino.UI4(0.39f, 0.33f, 0.5f, 0.355f), TextAnchor.MiddleLeft);
                    Casino.UI.Label(ref container, BETTING_OVERLAY, string.Format(msg("UI.Player.BetAmount"), blackjackPlayer.BetAmount), 12, new Casino.UI4(0.505f, 0.33f, 0.615f, 0.355f), TextAnchor.MiddleLeft);
                    Casino.UI.Button(ref container, BETTING_OVERLAY, Casino.UI.Color("#ce422b", 0.7f), " - ", 12, new Casino.UI4(0.6f, 0.33f, 0.615f, 0.355f), "blackjack.deductbet");

                    Casino.UI.Button(ref container, BETTING_OVERLAY, BLACK_COLOR, msg("UI.Player.ResetBet"), 12, new Casino.UI4(0.385f, 0.3f, 0.4975f, 0.325f), "blackjack.bet -1");
                    Casino.UI.Button(ref container, BETTING_OVERLAY, BLACK_COLOR, msg("UI.Player.LockBet"), 12, new Casino.UI4(0.5005f, 0.3f, 0.615f, 0.325f), "blackjack.setbet");
                }
                else
                {
                    bool waitingForBets = false;
                    for (int i = 0; i < cardPlayers.Length; i++)
                    {
                        Casino.CardPlayer cardPlayer = cardPlayers[i];
                        if (cardPlayer == null || !cardPlayer.IsPlaying)
                            continue;

                        if (!cardPlayer.BetLocked)
                        {
                            waitingForBets = true;
                            break;
                        }
                    }

                    if (waitingForBets)
                    {
                        Casino.UI.Label(ref container, BETTING_OVERLAY, msg("UI.Notification.Waiting"), 20, new Casino.UI4(0.3f, 0.45f, 0.7f, 0.55f));
                    }
                    else
                    {
                        InvokeHandler.CancelInvoke(this, PreStartRound);

                        DestroyUIElement(BETTING_OVERLAY);

                        PreStartRound();
                    }
                }
                Casino.UI.Button(ref container, BETTING_OVERLAY, new Casino.UI4(0.9f, 0.95f, 0.99f, 0.98f), "blackjack.leavetable");

                blackjackPlayer.AddUI(BETTING_OVERLAY, container);
            }

            internal void CreateUIMessage(string message)
            {
                CuiElementContainer container = Casino.UI.ElementContainer(MESSAGE_OVERLAY, new Casino.UI4(0.2f, 0.45f, 0.8f, 0.55f));
                Casino.UI.Label(ref container, MESSAGE_OVERLAY, message, 20, Casino.UI4.FullScreen);

                AddUIElement(MESSAGE_OVERLAY, container);
            }

            internal void CreateUIMessage(BlackJackPlayer blackjackPlayer, string message)
            {
                CuiElementContainer container = Casino.UI.ElementContainer(MESSAGE_OVERLAY, new Casino.UI4(0.2f, 0.45f, 0.8f, 0.55f));
                Casino.UI.Label(ref container, MESSAGE_OVERLAY, message, 20, Casino.UI4.FullScreen);

                blackjackPlayer.AddUI(MESSAGE_OVERLAY, container);
            }

            internal void CreateUIMessageOffset(string message, float time)
            {
                CuiElementContainer container = Casino.UI.ElementContainer(NOTIFICATION_OVERLAY, new Casino.UI4(0.3f, 0.89f, 0.7f, 0.97f));
                Casino.UI.Label(ref container, NOTIFICATION_OVERLAY, message, 18, Casino.UI4.FullScreen);

                AddUIElement(NOTIFICATION_OVERLAY, container);

                if (InvokeHandler.IsInvoking(this, () => DestroyUIElement(NOTIFICATION_OVERLAY)))
                    InvokeHandler.CancelInvoke(this, () => DestroyUIElement(NOTIFICATION_OVERLAY));

                InvokeHandler.Invoke(this, () => DestroyUIElement(NOTIFICATION_OVERLAY), time);
            }

            private void PreStartRound()
            {
                timer.StopTimer();

                DestroyUIElement(BETTING_OVERLAY);

                if (gameState != Casino.GameState.PlacingBets || CurrentPlayerCount == 0)
                {
                    ResetGame();
                    return;
                }

                gameState = Casino.GameState.Prestart;

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer != null && cardPlayer.IsPlaying)
                    {
                        if (!cardPlayer.BetLocked)
                            cardPlayer.SetBet();
                    }
                }

                CreatePlayerUI(true);

                CreateUIMessage(msg("UI.Notification.BeginsIn"));

                InvokeHandler.Invoke(this, StartRound, 5f);
            }

            private void StartRound()
            {
                ServerMgr.Instance.StartCoroutine(DealCards());
            }
            
            private IEnumerator DealCards()
            {
                DestroyUIElement(MESSAGE_OVERLAY);

                gameState = Casino.GameState.Playing;

                for (int count = 0; count < 2; count++)
                {
                    for (int i = 0; i < cardPlayers.Length; i++)
                    {
                        Casino.CardPlayer cardPlayer = cardPlayers[i];
                        if (cardPlayer != null && cardPlayer.IsPlaying)
                        {
                            DealCard(cardPlayer as BlackJackPlayer);

                            yield return CoroutineEx.waitForSeconds(0.5f);
                        }
                    }

                    DealCard(DealerAI, count == 1);

                    yield return CoroutineEx.waitForSeconds(0.5f);
                }

                if (CurrentPlayingCount == 0)
                    ResetGame();
                else NextPlayerTurn();
            }

            internal void DealCard(BlackJackPlayer blackjackPlayer, bool hidden = false)
            {
                Casino.Card card = deck.DealCard();

                blackjackPlayer.Hit(card);

                int handCount = blackjackPlayer.Count;

                string panel = $"{blackjackPlayer.UID}.card.{handCount}";

                float offset = (float)(handCount - 1) * 0.015f;

                Casino.UI4 cardPosition = cardPositions[blackjackPlayer.Position];

                Casino.UI4 elementPosition = new Casino.UI4(cardPosition.xMin + offset, cardPosition.yMin, cardPosition.xMax + offset, cardPosition.yMax);
               
                CuiElementContainer container = Casino.UI.ElementContainer(panel, elementPosition);
                Casino.UI.Image(ref container, panel, hidden ? PlayingCards.GetCardBackground(Instance.configData.CardColor) : card.GetCardImage(), Casino.UI4.FullScreen);

                AddUIElement(panel, container, true);
            }

            private void NextPlayerTurn()
            {
                if (playerIndex >= 0)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[playerIndex];
                    if (cardPlayer != null && cardPlayer.IsPlaying)
                    {
                        cardPlayer.DestroyUI(PLAY_OVERLAY);
                    }
                }
               
                playerIndex++;
                UserPlayTurn();
            }

            private void UserPlayTurn()
            {
                timer.StopTimer();

                DestroyUIElement(MESSAGE_OVERLAY);

                if (playerIndex >= cardPlayers.Length)
                {
                    playerIndex = -1;

                    CreateUIMessage(string.Format(msg("UI.Notification.CurrentlyPlaying"), msg("UI.Dealer")));

                    InvokeHandler.Invoke(this, DealerAI.PlayTurn, 5f);                    
                    return;
                }

                Casino.CardPlayer cardPlayer = cardPlayers[playerIndex];
                if (cardPlayer == null || !cardPlayer.IsPlaying)
                {
                    NextPlayerTurn();
                    return;
                }

                if ((cardPlayer as BlackJackPlayer).HasBlackJack())
                {
                    CreateUIMessage(string.Format(msg("UI.Notification.HasBlackJack"), cardPlayer.Player.displayName));
                    InvokeHandler.Invoke(this, NextPlayerTurn, 5f);
                    return;
                }

                CreateUIMessage(string.Format(msg("UI.Notification.CurrentlyPlaying"), cardPlayer.Player.displayName));

                CreatePlayOverlay(cardPlayer as BlackJackPlayer);

                timer.StartTimer(30);

                if (InvokeHandler.IsInvoking(this, NextPlayerTurn))
                    InvokeHandler.CancelInvoke(this, NextPlayerTurn);

                InvokeHandler.Invoke(this, NextPlayerTurn, 30f);
            }

            private void CreatePlayOverlay(BlackJackPlayer blackjackPlayer)
            {
                Casino.UI4 cardPosition = cardPositions[blackjackPlayer.Position];
                Casino.UI4 position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.095f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.07f);

                CuiElementContainer container = Casino.UI.ElementContainer(PLAY_OVERLAY, position);

                Casino.UI.Button(ref container, PLAY_OVERLAY, BLACK_COLOR, msg("UI.Player.Hit"), 12, new Casino.UI4(0f, 0f, 0.495f, 1f), "blackjack.hit");
                Casino.UI.Button(ref container, PLAY_OVERLAY, BLACK_COLOR, msg("UI.Player.Stay"), 12, new Casino.UI4(0.505f, 0f, 1f, 1f), "blackjack.stay");

                blackjackPlayer.AddUI(PLAY_OVERLAY, container);
            }
            
            internal void Hit(BlackJackPlayer blackjackPlayer)
            {
                timer.StopTimer();

                DealCard(blackjackPlayer);

                int score = blackjackPlayer.GetScore();

                if (score > 21 || score == 21)                
                    Stay(blackjackPlayer);                
                else UserPlayTurn();
            }

            internal void Stay(BlackJackPlayer blackjackPlayer)
            {
                timer.StopTimer();

                blackjackPlayer.DestroyUI(PLAY_OVERLAY);

                if (blackjackPlayer.IsBusted)
                    CreateUIMessage(string.Format(msg("UI.Notification.HasBust"), blackjackPlayer.Player.displayName));
                else CreateUIMessage(string.Format(msg("UI.Notification.StayingOn"), blackjackPlayer.Player.displayName, blackjackPlayer.GetScore()));
                
                InvokeHandler.Invoke(this, NextPlayerTurn, 5f);
            }

            internal void FinalizeGame()
            {
                DestroyUIElement(MESSAGE_OVERLAY);

                CalculateScores();

                CreateUIMessage(string.Format(msg("UI.Notification.StartsIn"), 5));

                InvokeHandler.Invoke(this, ResetGame, 5f);                
            }

            private void CalculateScores()
            {
                int dealerScore = DealerAI.GetScore();

                CuiElementContainer container = Casino.UI.ElementContainer(STATUS_OVERLAY, Casino.UI4.FullScreen);

                Casino.UI4 cardPosition;
                Casino.UI4 position;

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer == null || !cardPlayer.IsPlaying)
                        continue;

                    cardPlayer.DestroyUI(BALANCE_OVERLAY);

                    BlackJackPlayer blackjackPlayer = cardPlayer as BlackJackPlayer;

                    cardPosition = cardPositions[blackjackPlayer.Position];
                    position = new Casino.UI4(cardPosition.xMin, cardPosition.yMax + 0.01f, cardPosition.xMin + 0.12f, cardPosition.yMax + 0.035f);

                    Casino.UI.Panel(ref container, STATUS_OVERLAY, BLACK_COLOR, position);

                    if (blackjackPlayer.IsBusted)    
                        Casino.UI.Label(ref container, STATUS_OVERLAY, msg("UI.Status.Bust"), 12, position);
                    else
                    {
                        int playerScore = blackjackPlayer.GetScore();
                        
                        if (blackjackPlayer.HasBlackJack() && !DealerAI.HasBlackJack())
                        {
                            if (Instance.configData.ShowWinnings)
                                Casino.UI.Label(ref container, STATUS_OVERLAY, string.Format(msg("UI.Status.BlackJack.Amount"), "+" + Mathf.CeilToInt((float)blackjackPlayer.BetAmount * 1.5f)), 12, position);
                            else Casino.UI.Label(ref container, STATUS_OVERLAY, msg("UI.Status.BlackJack"), 12, position);
                            blackjackPlayer.IssueWin(blackjackPlayer.BetAmount + Mathf.CeilToInt((float)blackjackPlayer.BetAmount * 1.5f));
                        }
                        else if (playerScore == dealerScore)
                        {
                            if (Instance.configData.ShowWinnings)
                                Casino.UI.Label(ref container, STATUS_OVERLAY, string.Format(msg("UI.Status.Tie.Amount"), 0), 12, position);
                            else Casino.UI.Label(ref container, STATUS_OVERLAY, msg("UI.Status.Tie"), 12, position);
                            blackjackPlayer.IssueWin(blackjackPlayer.BetAmount);
                        }
                        else if (playerScore > dealerScore || DealerAI.IsBusted)
                        {
                            if (Instance.configData.ShowWinnings)
                                Casino.UI.Label(ref container, STATUS_OVERLAY, string.Format(msg("UI.Status.Win.Amount"), "+" + blackjackPlayer.BetAmount), 12, position);
                            else Casino.UI.Label(ref container, STATUS_OVERLAY, msg("UI.Status.Win"), 12, position);
                            blackjackPlayer.IssueWin(blackjackPlayer.BetAmount * 2);
                        }
                        else if (playerScore < dealerScore)
                        {
                            if (Instance.configData.ShowWinnings)
                                Casino.UI.Label(ref container, STATUS_OVERLAY, string.Format(msg("UI.Status.Lost.Amount"), "-" + blackjackPlayer.BetAmount), 12, position);
                            else Casino.UI.Label(ref container, STATUS_OVERLAY, msg("UI.Status.Lost"), 12, position);
                        }
                    }
                }

                cardPosition = cardPositions[4];
                position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.065f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.04f);

                if (DealerAI.IsBusted)
                {
                    Casino.UI.Panel(ref container, STATUS_OVERLAY, BLACK_COLOR, position);
                    Casino.UI.Label(ref container, STATUS_OVERLAY, msg("UI.Status.Bust"), 12, position);
                }
                else if (DealerAI.HasBlackJack())
                {
                    Casino.UI.Panel(ref container, STATUS_OVERLAY, BLACK_COLOR, position);
                    Casino.UI.Label(ref container, STATUS_OVERLAY, msg("UI.Status.BlackJack"), 12, position);
                }

                Casino.UI.Button(ref container, STATUS_OVERLAY, new Casino.UI4(0.9f, 0.95f, 0.99f, 0.98f), "blackjack.leavetable");

                AddUIElement(STATUS_OVERLAY, container);
            }
            
            internal void DestroyUIElement(string str)
            {
                containers.Remove(str);

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.DestroyUI(str);
                }
            }

            internal void DestroyUIElements()
            {
                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer == null)
                        continue;

                    (cardPlayer as BlackJackPlayer).DestroyUIElements();
                }

                DestroyUIElement(STATUS_OVERLAY);

                containers.Clear();
            }

            internal void AddUIElement(string panel, CuiElementContainer container, bool addToList = false)
            {
                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.AddUI(panel, container);
                }

                if (addToList)
                    containers.Add(panel, container);
            }            
        }
        
        internal class BlackJackPlayer : Casino.CardPlayer
        {         
            public bool IsBusted
            {
                get
                {
                    return GetScore() > 21;
                }
            }            
            
            internal void Hit(Casino.Card dealtCard) => hand.Add(dealtCard);

            internal List<Casino.Card> Show() => hand;

            internal Casino.Card LastCard() => hand[hand.Count - 1];

            internal int Count => hand.Count;

            internal override void OnDestroy()
            {
                DestroyCards();
                base.OnDestroy();
            }

            internal override void ResetHand()
            {
                DestroyCards();
                base.ResetHand();
            }

            internal void DestroyCards()
            {
                for (int i = 0; i < hand.Count; i++)
                {
                    string panel = $"{UID}.card.{i + 1}";
                    (CardGame as BlackJackGame).DestroyUIElement(panel);
                }
            }

            internal void DestroyUIElements()
            {
                for (int i = 0; i < uiPanels.Count; i++)
                {
                    string panel = uiPanels[i];
                    if (panel == GAME_BG)
                        continue;

                    DestroyUI(panel);
                }
            }

            internal bool HasBlackJack()
            {
                return hand.Count == 2 && ((hand[0].Value == Casino.CardValue.Ace && (int)hand[1].Value >= 10) || (hand[1].Value == Casino.CardValue.Ace && (int)hand[0].Value >= 10));
            }

            internal int GetScore()
            {            
                if (HasBlackJack())
                    return 21;

                int score = 0;

                int aces = 0;

                for (int i = 0; i < hand.Count; i++)
                {
                    if (hand[i].Value == Casino.CardValue.Ace)
                        aces++;
                    else
                    {
                        if ((int)hand[i].Value > 10)
                            score += 10;
                        else score += (int)hand[i].Value;
                    }
                }

                for (int i = 0; i < aces; i++)
                {
                    if (score > 10)
                        score += 1;
                    else score += 11;
                }
                
                return score;
            }
        }

        private class BlackJackAI : BlackJackPlayer
        {
            private Coroutine playRoutine;

            internal override void Awake()
            {
                GenerateUID();
            }

            internal override void OnDestroy()
            {
                Destroy(this.gameObject);
            }

            internal override void ResetHand()
            {
                if (playRoutine != null)
                    ServerMgr.Instance.StopCoroutine(playRoutine);
                
                base.ResetHand();
            }

            internal void RegisterCardGame(Casino.CardGame cardGame)
            {
                this.CardGame = cardGame;
            }

            public void PlayTurn() => playRoutine = ServerMgr.Instance.StartCoroutine(RunAI());

            public IEnumerator RunAI()
            {
                RevealHiddenCard();

                yield return CoroutineEx.waitForSeconds(0.5f);

                if (!HasBlackJack())
                {
                    while (GetScore() < 17)
                    {
                        (CardGame as BlackJackGame).DealCard(this);
                        yield return CoroutineEx.waitForSeconds(0.5f);
                    }

                    if (IsBusted)
                        (CardGame as BlackJackGame).CreateUIMessage(string.Format(msg("UI.Notification.HasBust"), msg("Dealer")));
                    else (CardGame as BlackJackGame).CreateUIMessage(string.Format(msg("UI.Notification.StayingOn"), msg("Dealer"), GetScore()));
                }
                else (CardGame as BlackJackGame).CreateUIMessage(string.Format(msg("UI.Notification.HasBlackJack"), msg("Dealer")));

                InvokeHandler.Invoke(CardGame, (CardGame as BlackJackGame).FinalizeGame, 5f);
                
                playRoutine = null;
            }

            private void RevealHiddenCard()
            {
                if (hand.Count == 0)
                    return;

                Casino.Card card = hand[1];

                string panel = $"{UID}.card.2";

                (CardGame as BlackJackGame).DestroyUIElement(panel);

                Casino.UI4 cardPosition = (CardGame as BlackJackGame).cardPositions[4];
                Casino.UI4 elementPosition = new Casino.UI4(cardPosition.xMin + 0.015f, cardPosition.yMin, cardPosition.xMax + 0.015f, cardPosition.yMax);

                CuiElementContainer container = Casino.UI.ElementContainer(panel, elementPosition);
                Casino.UI.Image(ref container, panel, card.GetCardImage(), Casino.UI4.FullScreen);

                (CardGame as BlackJackGame).AddUIElement(panel, container, true);                
            }
        }    
        #endregion

        #region Commands
        [ConsoleCommand("blackjack.leavetable")]
        private void ccmdLeaveTable(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            Casino.CardGame cardGame = Casino.Instance.IsGamePlayer(player);
            if (cardGame == null)
                return;

            cardGame.OnPlayerExit(player);
        }

        [ConsoleCommand("blackjack.hit")]
        private void ccmdHit(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            (blackjackPlayer.CardGame as BlackJackGame).Hit(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.stay")]
        private void ccmdStay(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            (blackjackPlayer.CardGame as BlackJackGame).Stay(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.bet")]
        private void ccmdBet(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            int amount = int.Parse(arg.Args[0]);

            switch (amount)
            {
                case -1:
                    blackjackPlayer.CardGame.ResetBet(blackjackPlayer);
                    break;
                case 0:
                    blackjackPlayer.CardGame.AdjustBet(blackjackPlayer, 1);
                    break;
                case 1:
                    blackjackPlayer.CardGame.AdjustBet(blackjackPlayer, 10);
                    break;
                case 2:
                    blackjackPlayer.CardGame.AdjustBet(blackjackPlayer, 50);
                    break;
                case 3:
                    blackjackPlayer.CardGame.AdjustBet(blackjackPlayer, 100);
                    break;
                case 4:
                    blackjackPlayer.CardGame.AdjustBet(blackjackPlayer, 500);
                    break;                
            }

            (blackjackPlayer.CardGame as BlackJackGame).PlaceBets(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.deductbet")]
        private void ccmdDeductBet(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            blackjackPlayer.CardGame.AdjustBet(blackjackPlayer, -1);
            (blackjackPlayer.CardGame as BlackJackGame).PlaceBets(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.setbet")]
        private void ccmdSetBet(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            blackjackPlayer.SetBet();
            (blackjackPlayer.CardGame as BlackJackGame).PlaceBets(blackjackPlayer);
        }
        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty("Card Color (blue, gray, green, purple, red, yellow)")]
            public string CardColor { get; set; }

            [JsonProperty("Show Winnings")]
            public bool ShowWinnings { get; set; }

            [JsonProperty("Show Balance")]
            public bool ShowBalance { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                CardColor = "blue",
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["UI.Notification.StartsIn"] = "Next game starts in <color=#00E500>{0}</color> seconds!",
            ["UI.Notification.IsStarting"] = "Next game is starting!",
            ["UI.Notification.BeginsIn"] = "Bets are locked! Game starts in <color=#00E500>5</color> seconds...",
            ["UI.Notification.PlaceBets"] = "Place Your Bets!",
            ["UI.Notification.Waiting"] = "Waiting for players to place their bets!",
            ["UI.Notification.CurrentlyPlaying"] = "Currently Playing : <color=#00E500>{0}</color>",
            ["UI.Notification.HasBlackJack"] = "{0} has <color=#00E500>Black Jack!</color>",
            ["UI.Notification.HasBust"] = "{0} has <color=#ce422b>Bust!</color>",
            ["UI.Notification.StayingOn"] = "{0} is staying on <color=#00E500>{1}</color>",
            ["UI.Notification.UsersPlacingBets"] = "Players are placing bets. You will enter the next round!",
            ["UI.Notification.GameInProgress"] = "This round has started. You will enter the next round!",
            ["UI.Notification.LeftGame"] = "<color=#ce422b>{0}</color> has left the game!",

            ["UI.Player.Bet"] = "Bet : {0}",
            ["UI.Player.PlayingNext"] = "Playing Next Round",
            ["UI.Player.BalanceAmount"] = "Balance : {0} {1}",
            ["UI.Player.BetAmount"] = "Bet : {0}",
            ["UI.Player.ResetBet"] = "Set Minimum",
            ["UI.Player.LockBet"] = "Lock Bet",
            ["UI.Player.Hit"] = "Hit",
            ["UI.Player.Stay"] = "Stay",

            ["UI.Dealer"] = "Dealer",
            ["UI.LeaveTable"] = "Leave Table",

            ["UI.Status.Bust"] = "<color=#ce422b>Bust!</color>",
            ["UI.Status.BlackJack"] = "<color=#00E500>Black Jack!</color>",
            ["UI.Status.Tie"] = "Tie!",
            ["UI.Status.Win"] = "<color=#00E500>Win!</color>",
            ["UI.Status.Lost"] = "<color=#ce422b>Lost!</color>",
            ["UI.Status.BlackJack.Amount"] = "<color=#00E500>Black Jack!</color> ({0})",
            ["UI.Status.Tie.Amount"] = "Tie! ({0})",
            ["UI.Status.Win.Amount"] = "<color=#00E500>Win!</color> ({0})",
            ["UI.Status.Lost.Amount"] = "<color=#ce422b>Lost!</color> ({0})",

            ["Chat.Kicked.NoScrap"] = "<color=#D3D3D3>You have run out of scrap to bet with!</color>",
        };
        #endregion
    }
}
