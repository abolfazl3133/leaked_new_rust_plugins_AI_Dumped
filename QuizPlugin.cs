using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Oxide.Game.Rust.Cui;
namespace Oxide.Plugins
{
    [Info("QuizPlugin", "sdapro", "1.0.5")]
    class QuizPlugin: RustPlugin
    {
        private Dictionary < ulong, QuizData > playerQuizzes = new Dictionary < ulong, QuizData > ();
        class QuizData
        {
            public string ImageUrl;
            public string CorrectAnswer;
            public bool Answered;
            public float StartTime;
        }
        void OnUserChat(IPlayer player, string message)
            {
                if (player == null || string.IsNullOrEmpty(message)) return;
                var basePlayer = player.Object as BasePlayer;
                if (basePlayer == null) return;
                foreach(var kvp in playerQuizzes)
                {
                    if (!kvp.Value.Answered && message.ToLower() == kvp.Value.CorrectAnswer.ToLower())
                    {
                        CheckAnswer(basePlayer, message, kvp.Key);
                    }
                }
            }
            [ChatCommand("vopros")]
        private void cmdVopros(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, "Использование: /vopros <ссылка на картинку> <правильный ответ>");
                return;
            }
            var imageUrl = args[0];
            var correctAnswer = args[1];
            var quizData = new QuizData
            {
                ImageUrl = imageUrl,
                    CorrectAnswer = correctAnswer,
                    Answered = false,
                    StartTime = Time.realtimeSinceStartup
            };
            playerQuizzes[player.userID] = quizData;
            foreach(BasePlayer current in BasePlayer.activePlayerList)
            {
                current.ChatMessage($ "<size=18><color=#0ef>|Угадай слово|</color></size> \n<size=14><color=#ffb700>(внимательно изучи картинку и отправь ответ в чат, у вас 1 минута!)</color></size>");
                current.ChatMessage($ "<color=#ffb700>Первая буква: {correctAnswer.Substring(0, 1)}</color>");
                current.ChatMessage($ "<color=#ffb700>Последняя буква: {correctAnswer.Substring(correctAnswer.Length - 1)}</color>");
                SendImageGUI(current, imageUrl);
            }
        }
        void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                EndQuiz(player);
            }
        }
        private void SendImageGUI(BasePlayer player, string imageUrl)
        {
            CuiElementContainer container = new CuiElementContainer();
            var imagePanel = container.Add(new CuiPanel
            {
                Image = {
                        Color = "0 0 0 0",
                        FadeIn = 1.0 f
                    },
                    RectTransform = {
                        AnchorMin = "0.75 0.6",
                        AnchorMax = "1 1"
                    },
                    CursorEnabled = false
            }, "Overlay", "panelImage");
            container.Add(new CuiElement
            {
                Parent = "panelImage",
                    Components = {
                        new CuiRawImageComponent
                        {
                            Url = imageUrl, Sprite = "assets/content/textures/generic/fulltransparent.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
            });
            CuiHelper.AddUi(player, container);
        }
        private void CheckAnswer(BasePlayer answeringPlayer, string answer, ulong quizOwnerId)
        {
            if (playerQuizzes.ContainsKey(quizOwnerId))
            {
                var quizData = playerQuizzes[quizOwnerId];
                if (!quizData.Answered && answer.ToLower() == quizData.CorrectAnswer.ToLower())
                {
                    quizData.Answered = true;
                    PrintToChat($ "<color=#f00>Победитель:</color> <size=17><color=#00ff95>{answeringPlayer.displayName}</color></size>\n <color=#f00>Правильный ответ:</color> <size=17><color=#00ff95>{quizData.CorrectAnswer}</color></size>");
                    answeringPlayer.GiveItem(ItemManager.CreateByName("explosive.timed", 1000));
                    if (answeringPlayer == null) return;
                    Vector3 playerPosition = answeringPlayer.transform.position;
                    for (int i = 0; i < 1; i++)
                    {
                        Vector3 offset = new Vector3(UnityEngine.Random.Range(-5 f, 5 f), 2 f, UnityEngine.Random.Range(-5 f, 5 f));
                        Vector3 grenadePosition = playerPosition + offset;
                        BaseEntity grenade = GameManager.server.CreateEntity("assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab", grenadePosition);
                        if (grenade != null)
                        {
                            grenade.Spawn();
                        }
                        timer.Once(6 f, () =>
                        {
                            if (grenade != null && !grenade.IsDestroyed)
                            {
                                grenade.Kill();
                            }
                        });
                    }
                }
                foreach(BasePlayer player in BasePlayer.activePlayerList)
                {
                    EndQuiz(player);
                }
            }
        }
        private void EndQuiz(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "panelImage");
            if (playerQuizzes.ContainsKey(player.userID))
            {
                playerQuizzes.Remove(player.userID);
            }
        }
    }
}