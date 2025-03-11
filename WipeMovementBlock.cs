using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Network;

namespace Oxide.Plugins
{
    [Info("Wipe Movement Block", "Volk", "0.0.3")]
    [Description("Блокирует движение игроков на определенное время после вайпа")]
    public class WipeMovementBlock : RustPlugin
    {
        #region Конфигурация

        private Configuration? config;

        private class Configuration
        {
            [JsonProperty("Количество минут блокировки движения после вайпа")]
            public int BlockMinutes = 10;

            [JsonProperty("Разрешить администраторам двигаться во время блокировки")]
            public bool AdminsCanMove = true;
            
            [JsonProperty("Примечание")]
            public string Note = "Все сообщения находятся в локализационных файлах: oxide/data/lang/ru( или en )/WipeMovementBlock";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Конфигурация повреждена! Создание новой конфигурации...");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Локализация

        private void LoadMessages()
        {
            // Русские сообщения (Русский язык по умолчанию)
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BlockMovement"] = "Движение заблокировано на {0} минут после вайпа.",
                ["UnblockMovement"] = "Движение разблокировано! Удачной игры!",
                ["TimeRemaining"] = "Движение будет разблокировано через {0} минут и {1} секунд.",
                ["TestModeEnabled"] = "Тестовый режим активирован! Движение заблокировано на {0} секунд.",
                ["TestCommandUsage"] = "Использование: /testblock <секунды>",
                ["NoPermission"] = "У вас нет разрешения на использование этой команды.",
                ["BlockReset"] = "Блокировка движения была сброшена.",
                ["NoActiveBlock"] = "В данный момент нет активной блокировки движения.",
                ["MovementBlocked"] = "Ваше движение заблокировано!"
            }, this, "ru");
            
            // Английские сообщения (English)
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BlockMovement"] = "Movement is blocked for {0} minutes after wipe.",
                ["UnblockMovement"] = "Movement has been unblocked! Good luck!",
                ["TimeRemaining"] = "Movement will be unblocked in {0} minutes and {1} seconds.",
                ["TestModeEnabled"] = "Test mode activated! Movement is blocked for {0} seconds.",
                ["TestCommandUsage"] = "Usage: /testblock <seconds>",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["BlockReset"] = "Movement block has been reset.",
                ["NoActiveBlock"] = "There is no active movement block at the moment.",
                ["MovementBlocked"] = "Your movement is blocked!"
            }, this, "en");
        }

        #endregion

        #region Данные

        private bool isBlocked = false;
        private DateTime wipeTime;
        private DateTime unblockTime;
        private Timer? movementBlockTimer;
        private bool isTestMode = false;
        
        // Словарь для хранения начальных позиций игроков
        private Dictionary<ulong, Vector3> playerPositions = new Dictionary<ulong, Vector3>();
        // Интервал проверки движения для телепортации (в секундах)
        private const float MovementCheckInterval = 0.1f;

        #endregion

        #region Hooks

        private void Init()
        {
            LoadMessages();
            permission.RegisterPermission("wipemovementblock.bypass", this);
            permission.RegisterPermission("wipemovementblock.admin", this);
        }

        private void OnServerInitialized()
        {
            wipeTime = DateTime.Now;
            StartMovementBlock();
        }

        private void Unload()
        {
            movementBlockTimer?.Destroy();
        }
        
        // Блокировка передвижения через перехват инпута
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!isBlocked || config == null)
                return;

            // Проверка прав на обход блокировки
            if (config.AdminsCanMove && player.IsAdmin)
                return;

            if (permission.UserHasPermission(player.UserIDString, "wipemovementblock.bypass"))
                return;

            // Предотвращаем все действия игрока
            if (input.current != null)
            {
                input.current.buttons = 0; // Блокируем все кнопки
            }
            
            // Телепортируем игрока на сохраненную позицию
            if (playerPositions.TryGetValue(player.userID, out Vector3 savedPosition))
            {
                if (Vector3.Distance(savedPosition, player.transform.position) > 0.1f)
                {
                    player.Teleport(savedPosition);
                    
                    // Показываем редкое сообщение о блокировке
                    if (UnityEngine.Random.Range(0, 20) == 0)
                    {
                        player.ChatMessage(GetMsg("MovementBlocked", player.UserIDString));
                    }
                }
            }
            else
            {
                // Если позиция не сохранена, сохраняем текущую
                playerPositions[player.userID] = player.transform.position;
            }
        }
        
        // Отслеживание подключения игроков
        private void OnPlayerConnected(BasePlayer player)
        {
            // Если блокировка активна, запоминаем позицию
            if (isBlocked && player != null)
            {
                playerPositions[player.userID] = player.transform.position;
                
                // Показываем сообщение о блокировке, если она активна
                if (config != null)
                {
                    TimeSpan remainingTime = unblockTime - DateTime.Now;
                    if (remainingTime.TotalSeconds > 0)
                    {
                        int minutes = (int)remainingTime.TotalMinutes;
                        int seconds = remainingTime.Seconds;
                        player.ChatMessage(string.Format(GetMsg("TimeRemaining", player.UserIDString), minutes, seconds));
                    }
                }
            }
        }
        
        // Отслеживание отключения игроков
        private void OnPlayerDisconnected(BasePlayer player)
        {
            // Удаляем информацию о позиции при отключении
            if (playerPositions.ContainsKey(player.userID))
            {
                playerPositions.Remove(player.userID);
            }
        }
        
        // Предотвращение урона от падения во время блокировки
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            // Во время блокировки предотвращаем урон от падения
            if (!isBlocked) return;
            
            var player = entity as BasePlayer;
            if (player == null) return;
            
            // Проверка прав на обход блокировки
            if (config?.AdminsCanMove == true && player.IsAdmin)
                return;
                
            if (permission.UserHasPermission(player.UserIDString, "wipemovementblock.bypass"))
                return;
                
            // Если урон от падения - отменяем
            if (info?.damageTypes?.GetMajorityDamageType() == Rust.DamageType.Fall)
            {
                info.damageTypes.Scale(Rust.DamageType.Fall, 0f);
            }
        }

        #endregion

        #region Методы

        private void StartMovementBlock()
        {
            if (config == null) return;
            
            isBlocked = true;
            unblockTime = wipeTime.AddMinutes(config.BlockMinutes);
            
            // Вывод сообщения о блокировке
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage(string.Format(GetMsg("BlockMovement", player.UserIDString), config.BlockMinutes));
                
                // Сохраняем начальную позицию
                playerPositions[player.userID] = player.transform.position;
            }
            
            // Таймер разблокировки
            movementBlockTimer = timer.Once(config.BlockMinutes * 60f, () =>
            {
                isBlocked = false;
                
                // Вывод сообщения о разблокировке
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage(GetMsg("UnblockMovement", player.UserIDString));
                }
                
                // Очищаем сохраненные позиции
                playerPositions.Clear();
            });
            
            // Периодически информировать игроков о времени разблокировки
            timer.Every(60f, () =>
            {
                if (!isBlocked) return;
                
                TimeSpan remainingTime = unblockTime - DateTime.Now;
                if (remainingTime.TotalSeconds <= 0) return;
                
                int minutes = (int)remainingTime.TotalMinutes;
                int seconds = remainingTime.Seconds;
                
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage(string.Format(GetMsg("TimeRemaining", player.UserIDString), minutes, seconds));
                }
            });
        }
        
        private void StartTestBlock(int seconds)
        {
            if (config == null) return;
            
            // Останавливаем предыдущий таймер, если он существует
            movementBlockTimer?.Destroy();
            
            isBlocked = true;
            isTestMode = true;
            wipeTime = DateTime.Now;
            unblockTime = wipeTime.AddSeconds(seconds);
            
            // Вывод сообщения о тестовой блокировке
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage(string.Format(GetMsg("TestModeEnabled", player.UserIDString), seconds));
                
                // Сохраняем начальную позицию
                playerPositions[player.userID] = player.transform.position;
            }
            
            // Таймер разблокировки
            movementBlockTimer = timer.Once(seconds, () =>
            {
                isBlocked = false;
                isTestMode = false;
                
                // Вывод сообщения о разблокировке
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage(GetMsg("UnblockMovement", player.UserIDString));
                }
                
                // Очищаем сохраненные позиции
                playerPositions.Clear();
            });
        }
        
        private bool ResetMovementBlock()
        {
            if (!isBlocked)
                return false;
            
            isBlocked = false;
            isTestMode = false;
            movementBlockTimer?.Destroy();
            movementBlockTimer = null;
            
            // Вывод сообщения о разблокировке
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage(GetMsg("UnblockMovement", player.UserIDString));
            }
            
            // Очищаем сохраненные позиции
            playerPositions.Clear();
            
            return true;
        }

        private string GetMsg(string key, string? userId = null) => lang.GetMessage(key, this, userId);

        #endregion

        #region Команды

        [ChatCommand("blockstatus")]
        private void CmdBlockStatus(BasePlayer player, string command, string[] args)
        {
            if (!isBlocked)
            {
                player.ChatMessage(GetMsg("UnblockMovement", player.UserIDString));
                return;
            }
            
            TimeSpan remainingTime = unblockTime - DateTime.Now;
            if (remainingTime.TotalSeconds <= 0)
            {
                player.ChatMessage(GetMsg("UnblockMovement", player.UserIDString));
                return;
            }
            
            int minutes = (int)remainingTime.TotalMinutes;
            int seconds = remainingTime.Seconds;
            player.ChatMessage(string.Format(GetMsg("TimeRemaining", player.UserIDString), minutes, seconds));
        }
        
        [ChatCommand("testblock")]
        private void CmdTestBlock(BasePlayer player, string command, string[] args)
        {
            // Проверка на права администратора
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "wipemovementblock.admin"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }
            
            if (args.Length < 1)
            {
                player.ChatMessage(GetMsg("TestCommandUsage", player.UserIDString));
                return;
            }
            
            if (int.TryParse(args[0], out int seconds) && seconds > 0)
            {
                StartTestBlock(seconds);
                Puts($"Игрок {player.displayName} активировал тестовую блокировку движения на {seconds} секунд.");
            }
            else
            {
                player.ChatMessage(GetMsg("TestCommandUsage", player.UserIDString));
            }
        }
        
        [ConsoleCommand("wipemovementblock.test")]
        private void CmdConsoleTestBlock(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;
                
            if (arg.Args == null || arg.Args.Length < 1)
            {
                Puts("Использование: wipemovementblock.test <секунды>");
                return;
            }
            
            if (int.TryParse(arg.Args[0], out int seconds) && seconds > 0)
            {
                StartTestBlock(seconds);
                Puts($"Тестовая блокировка движения активирована на {seconds} секунд.");
            }
            else
            {
                Puts("Использование: wipemovementblock.test <секунды>");
            }
        }
        
        [ChatCommand("resetblock")]
        private void CmdResetBlock(BasePlayer player, string command, string[] args)
        {
            // Проверка на права администратора
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "wipemovementblock.admin"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }
            
            if (ResetMovementBlock())
            {
                player.ChatMessage(GetMsg("BlockReset", player.UserIDString));
                Puts($"Игрок {player.displayName} сбросил блокировку движения.");
            }
            else
            {
                player.ChatMessage(GetMsg("NoActiveBlock", player.UserIDString));
            }
        }
        
        [ConsoleCommand("wipemovementblock.reset")]
        private void CmdConsoleResetBlock(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;
                
            if (ResetMovementBlock())
            {
                Puts("Блокировка движения была сброшена.");
            }
            else
            {
                Puts("В данный момент нет активной блокировки движения.");
            }
        }
        
        #endregion
    }
} 