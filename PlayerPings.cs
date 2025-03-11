using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("PlayerPings", "Lore", "1.0.0")]
    [Description("Система маркеров для улучшения коммуникации между игроками")]
    public class PlayerPings : RustPlugin
    {
        #region Поля

        private Configuration config;
        private const string UsePermission = "playerpings.use";
        private readonly Dictionary<ulong, PlayerPingData> playerPings = new Dictionary<ulong, PlayerPingData>();
        private readonly Dictionary<ulong, string> activeUIs = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, Timer> pingTimers = new Dictionary<ulong, Timer>();
        private readonly HashSet<BasePlayer> playersInRange = new HashSet<BasePlayer>();
        private readonly Queue<Effect> effectPool = new Queue<Effect>();
        private const int MaxEffectsInPool = 50;

        // Кэшированные слои для физики
        private static readonly int DefaultMask = LayerMask.GetMask("Default", "Deployed", "Construction", "Resource", "World", "Terrain");
        
        // Кэшированные цвета маркеров
        private static readonly string[] markerColors = {
            "1 0.92 0.016 1",      // Желтый
            "0 0 1 1",             // Синий
            "0 1 0 1",             // Зеленый
            "1 0 0 1",             // Красный
            "0.5 0 0.5 1",         // Фиолетовый
            "0 1 1 1"              // Голубой
        };

        // Кэшированные компоненты UI
        private static readonly CuiPanel MainPanel = new CuiPanel
        {
            Image = { Color = "0 0 0 0.9" },
            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -100", OffsetMax = "150 100" },
            CursorEnabled = true
        };

        private static readonly CuiLabel TitleLabel = new CuiLabel
        {
            Text = { FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
            RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 1" }
        };

        private static readonly CuiButton AutoDetectCheckbox = new CuiButton
        {
            Button = { Color = "0.7 0.7 0.7 1", Command = "pingtoggleauto" },
            RectTransform = { AnchorMin = "0.05 0.3", AnchorMax = "0.15 0.4" },
            Text = { Text = "✓", FontSize = 20, Align = TextAnchor.MiddleCenter }
        };

        private static readonly CuiLabel AutoDetectLabel = new CuiLabel
        {
            Text = { FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
            RectTransform = { AnchorMin = "0.2 0.3", AnchorMax = "0.95 0.4" }
        };

        private static readonly CuiLabel BindKeyLabel = new CuiLabel
        {
            Text = { FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.7 1" },
            RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 0.2" }
        };

        // Кэшированные параметры для эффектов
        private static readonly string MarkerPrefab = "assets/prefabs/misc/map/genericmarker.prefab";
        private static readonly Vector3 ZeroVector = Vector3.zero;

        #endregion

        #region Локализация

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command!",
                ["MarkerSettings"] = "MARKER SETTINGS",
                ["AutoDetect"] = "Automatically define the type of object",
                ["BindKey"] = "bind key {0}",
                ["ColorChanged"] = "Marker color changed!",
                ["AutoDetectToggle"] = "Auto-detection of object type: {0}",
                ["AutoDetectEnabled"] = "enabled",
                ["AutoDetectDisabled"] = "disabled",
                ["TooManyPings"] = "Please wait before placing another ping",
                ["OutOfRange"] = "Target is out of range"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет разрешения на использование этой команды!",
                ["MarkerSettings"] = "НАСТРОЙКИ МАРКЕРА",
                ["AutoDetect"] = "Автоматически определять тип объекта",
                ["BindKey"] = "привязка клавиши {0}",
                ["ColorChanged"] = "Цвет маркера изменен!",
                ["AutoDetectToggle"] = "Автоопределение типа объекта: {0}",
                ["AutoDetectEnabled"] = "включено",
                ["AutoDetectDisabled"] = "выключено",
                ["TooManyPings"] = "Подождите перед установкой следующего маркера",
                ["OutOfRange"] = "Цель вне зоны досягаемости"
            }, this, "ru");
        }

        private string GetMessage(string key, string userId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, userId), args);
        }

        #endregion

        #region Конфигурация
        
        private class Configuration
        {
            [JsonProperty("Базовые настройки")]
            public BasicSettings Basic = new BasicSettings();

            public class BasicSettings
            {
                [JsonProperty("Цвет(0-желтый,1-синий,2-зеленый,3-красный,4-фиолетовый,5-голубой)")]
                public int DefaultColor = 3;

                [JsonProperty("Максимальное количество пингов на игрока")]
                public int MaxPingsPerPlayer = 5;

                [JsonProperty("Максимальная дистанция")]
                public float MaxDistance = 300f;

                [JsonProperty("Время отображения пинга")]
                public float PingDisplayTime = 3f;

                [JsonProperty("Включить метод пинга Прицеливание + клавиша использования (E)")]
                public bool EnableAimingPing = true;

                [JsonProperty("Задержка между пингами (секунды): 0.3 секунды стандартно")]
                public float PingDelay = 0.3f;

                [JsonProperty("Команда для установки пинга")]
                public string PingCommand = "playerping.set";
            }
        }

        private class PlayerPingData
        {
            public int Color { get; set; }
            public bool AutoDetect { get; set; }
            public float LastPingTime { get; set; }
            public List<Vector3> ActivePings { get; set; }

            public PlayerPingData()
            {
                Color = 3;
                AutoDetect = true;
                LastPingTime = 0f;
                ActivePings = new List<Vector3>();
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
                PrintError("Ошибка чтения конфигурации! Создание новой конфигурации.");
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
            permission.RegisterPermission(UsePermission, this);
            cmd.AddChatCommand("pingmenu", this, "CmdPingMenu");
            cmd.AddConsoleCommand(config.Basic.PingCommand, this, "ConsolePing");
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }

            // Очистка таймеров и пулов
            foreach (var timer in pingTimers.Values)
            {
                timer?.Destroy();
            }
            pingTimers.Clear();
            effectPool.Clear();
            playersInRange.Clear();
        }

        private void OnPlayerDisconnected(BasePlayer,string)
        {
            if (player == null) return;

            DestroyUI(player);
            CleanupPlayerData(player.userID);
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!config.Basic.EnableAimingPing || 
                player == null || 
                !permission.UserHasPermission(player.UserIDString, UsePermission) ||
                !input.WasJustPressed(BUTTON.USE)) return;

            CreatePing(player);
        }

        private void OnServerInitialized(bool)
        {
            // Предварительное заполнение пула эффектов
            for (int i = 0; i < MaxEffectsInPool; i++)
            {
                effectPool.Enqueue(new Effect(MarkerPrefab, ZeroVector, ZeroVector));
            }
        }

        #endregion

        #region Команды

        private void CmdPingMenu(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                player.ChatMessage(GetMessage("NoPermission", player.UserIDString));
                return;
            }

            ShowPingMenu(player);
        }

        private void ConsolePing(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                player.ChatMessage(GetMessage("NoPermission", player.UserIDString));
                return;
            }

            CreatePing(player);
        }

        #endregion

        #region UI Methods

        private void ShowPingMenu(BasePlayer player)
        {
            DestroyUI(player);

            var elements = new CuiElementContainer();
            
            // Используем кэшированные компоненты
            elements.Add(MainPanel, "Overlay", "PingMenu");

            var title = TitleLabel;
            title.Text.Text = GetMessage("MarkerSettings", player.UserIDString);
            elements.Add(title, "PingMenu");

            // Цветные кнопки
            float buttonWidth = 0.18f;
            float spacing = 0.025f;
            float startX = spacing;

            for (int i = 0; i < markerColors.Length; i++)
            {
                elements.Add(new CuiButton
                {
                    Button = { Color = markerColors[i], Command = $"pingcolor {i}" },
                    RectTransform = { AnchorMin = $"{startX} 0.5", AnchorMax = $"{startX + buttonWidth} 0.7" },
                    Text = { Text = "" }
                }, "PingMenu");

                startX += buttonWidth + spacing;
            }

            elements.Add(AutoDetectCheckbox, "PingMenu");

            var autoDetectText = AutoDetectLabel;
            autoDetectText.Text.Text = GetMessage("AutoDetect", player.UserIDString);
            elements.Add(autoDetectText, "PingMenu");

            var bindKeyText = BindKeyLabel;
            bindKeyText.Text.Text = GetMessage("BindKey", player.UserIDString, config.Basic.PingCommand);
            elements.Add(bindKeyText, "PingMenu");

            string uiName = CuiHelper.GetGuid();
            activeUIs[player.userID] = uiName;
            CuiHelper.AddUi(player, elements);
        }

        private void DestroyUI(BasePlayer player)
        {
            if (activeUIs.ContainsKey(player.userID))
            {
                CuiHelper.DestroyUi(player, activeUIs[player.userID]);
                activeUIs.Remove(player.userID);
            }
        }

        #endregion

        #region Data Management

        private void SaveData()
        {
            if (playerPings.Count > 0)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, playerPings);
            }
        }

        private void LoadData()
        {
            try
            {
                playerPings.Clear();
                var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerPingData>>(Name);
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        playerPings[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error loading data: {ex.Message}");
            }
        }

        private void CleanupPlayerData(ulong userId)
        {
            playerPings.Remove(userId);
            activeUIs.Remove(userId);
            
            if (pingTimers.TryGetValue(userId, out Timer timer))
            {
                timer?.Destroy();
                pingTimers.Remove(userId);
            }
        }

        #endregion

        #region Ping Methods

        private void CreatePing(BasePlayer player)
        {
            if (!playerPings.TryGetValue(player.userID, out PlayerPingData pingData))
            {
                pingData = new PlayerPingData();
                playerPings[player.userID] = pingData;
            }

            if (Time.time - pingData.LastPingTime < config.Basic.PingDelay)
            {
                player.ChatMessage(GetMessage("TooManyPings", player.UserIDString));
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, config.Basic.MaxDistance, DefaultMask))
            {
                player.ChatMessage(GetMessage("OutOfRange", player.UserIDString));
                return;
            }

            Vector3 pingPosition = hit.point;
            
            // Очистка старых пингов
            if (pingData.ActivePings.Count >= config.Basic.MaxPingsPerPlayer)
            {
                pingData.ActivePings.RemoveAt(0);
            }

            pingData.ActivePings.Add(pingPosition);
            pingData.LastPingTime = Time.time;

            // Оптимизированная проверка игроков в радиусе
            playersInRange.Clear();
            Vis.Entities(pingPosition, config.Basic.MaxDistance, BasePlayer.activePlayerList);
            
            foreach (var targetPlayer in playersInRange)
            {
                if (targetPlayer != null && targetPlayer.IsConnected)
                {
                    ShowPingEffect(targetPlayer, pingPosition, pingData.Color, DetectObjectType(hit.collider));
                }
            }

            // Создаем или обновляем таймер для автоудаления
            if (pingTimers.TryGetValue(player.userID, out Timer existingTimer))
            {
                existingTimer?.Destroy();
            }

            pingTimers[player.userID] = timer.Once(config.Basic.PingDisplayTime, () =>
            {
                if (pingData.ActivePings.Contains(pingPosition))
                {
                    pingData.ActivePings.Remove(pingPosition);
                }
                pingTimers.Remove(player.userID);
            });
        }

        private string DetectObjectType(Collider hitCollider)
        {
            if (hitCollider == null) return "default";

            var entity = hitCollider.GetComponent<BaseEntity>();
            if (entity == null) return "default";

            // Используем switch вместо множественных if для оптимизации
            switch (entity)
            {
                case BuildingBlock _: return "building";
                case ResourceEntity _: return "resource";
                case BasePlayer _: return "player";
                case LootContainer _: return "loot";
                default: return "default";
            }
        }

        private Effect GetEffectFromPool()
        {
            if (effectPool.Count > 0)
                return effectPool.Dequeue();

            return new Effect(MarkerPrefab, ZeroVector, ZeroVector);
        }

        private void ReturnEffectToPool(Effect effect)
        {
            if (effectPool.Count < MaxEffectsInPool)
                effectPool.Enqueue(effect);
        }

        private void ShowPingEffect(BasePlayer player, Vector3 position, int colorIndex, string type)
        {
            if (player == null || !player.IsConnected) return;

            string color = markerColors[colorIndex];
            
            // Оптимизированное создание маркера
            player.SendConsoleCommand("ddraw.marker", config.Basic.PingDisplayTime, color, position);
            
            // Используем пул эффектов
            var effect = GetEffectFromPool();
            effect.worldPos = position;
            EffectNetwork.Send(effect, player.Connection);
            
            // Возвращаем эффект в пул после использования
            timer.Once(0.1f, () => ReturnEffectToPool(effect));
        }

        #endregion

        #region Chat Commands

        [ChatCommand("pingcolor")]
        private void CmdPingColor(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, UsePermission)) return;
            if (args.Length == 0) return;

            int colorIndex;
            if (!int.TryParse(args[0], out colorIndex) || colorIndex < 0 || colorIndex >= markerColors.Length)
                return;

            if (!playerPings.ContainsKey(player.userID))
                playerPings[player.userID] = new PlayerPingData();

            playerPings[player.userID].Color = colorIndex;
            player.ChatMessage(GetMessage("ColorChanged", player.UserIDString));
        }

        [ChatCommand("pingtoggleauto")]
        private void CmdToggleAuto(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, UsePermission)) return;

            if (!playerPings.ContainsKey(player.userID))
                playerPings[player.userID] = new PlayerPingData();

            var data = playerPings[player.userID];
            data.AutoDetect = !data.AutoDetect;
            
            string status = data.AutoDetect ? 
                GetMessage("AutoDetectEnabled", player.UserIDString) : 
                GetMessage("AutoDetectDisabled", player.UserIDString);
                
            player.ChatMessage(GetMessage("AutoDetectToggle", player.UserIDString, status));
        }

        #endregion
    }
} 