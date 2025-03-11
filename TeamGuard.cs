using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Oxide.Core;
using UnityEngine;
using Formatter = Oxide.Core.Libraries.Covalence.Formatter;

namespace Oxide.Plugins
{
    [Info("TeamGuard", "https://discord.gg/TrJ7jnS233", "2.1.61")]
    [Description("Plugin allow admins to easier control player groups size.")]
    /*
     * Author info:
     * E-mail: Vlad-00003@mail.ru
     * Vk: vk.com/vlad_00003
     */
    internal class TeamGuard : RustPlugin
    {
        #region Checker

        private class TGChecker : TriggerBase
        {
            private readonly HashSet<BasePlayer> _players = new HashSet<BasePlayer>();
            private SphereCollider _collider = new SphereCollider();
            private Info _info;
            private bool _mainCuiCreated;
            private BasePlayer _player;

            private ChatMessage Message => _info.ShouldShock
                ? new ChatMessage("TextWhileDmg", _config.RadiusCheck.MaxPlayers, _config.RadiusCheck.DamagePerTime,
                    _config.RadiusCheck.DamageFrequencyString)
                : new ChatMessage("TextBeforeDmg", _config.RadiusCheck.MaxPlayers, _info.TimeBeforeShock);

            private void Awake()
            {
                _collider = GetComponent<SphereCollider>();
                _collider.radius = _config.RadiusCheck.Radius;
                _collider.isTrigger = true;
                interestLayers = 131072; // 17: Player (Server)
                if (_config.RadiusCheck.DamagePerTime > 0)
                    InvokeRepeating(DamageSequence, _config.RadiusCheck.DamageFrequency);
                if (_config.RadiusCheck.UseChat)
                    InvokeRepeating(ChatSequence, _config.RadiusCheck.ChatUpdateFrequency);
                if (_config.RadiusCheck.UseCui)
                    InvokeRepeating(CuiSequence, _config.RadiusCheck.CuiUpdateFrequency);
            }

            private TGChecker SetPlayer(BasePlayer player)
            {
                _player = player;
                return this;
            }

            private void InvokeRepeating(Action action, float time) => InvokeRepeating(action, time, time);

            private bool ShouldCount(BasePlayer player)
            {
				if (Interface.CallHook("OnShouldCount", player) != null)
					return false;
                return player.IsConnected && !InWhitelistedZone(player) && !InSafeZone(player) &&
                       !IsDuelPlayer(player) &&
                       !IsEventPlayer(player) && !HasAuth(player) &&
                       player.CanInteract() && CanSee(player, _player);
            }

            private IEnumerator DoShock()
            {
                _player.Hurt(_config.RadiusCheck.DamagePerTime, DamageType.ElectricShock, _player, false);
                foreach (var prefab in _config.RadiusCheck.Effects)
                {
                    EffectNetwork.Send(new Effect(prefab, _player, 0u, Vector3.zero, Vector3.forward),
                        _player.net.connection);
                    yield return CoroutineEx.waitForSeconds(0.3f);
                }
            }

            public void Kill()
            {
                DestroyCui();
                Destroy(gameObject);
            }

            private void FixedUpdate()
            {
                if (!ShouldCount(_player))
                {
                    _info.Clear();
                    return;
                }

                var playersInArea = _players.Count(ShouldCount);
                if (playersInArea < _config.RadiusCheck.MaxPlayers)
                {
                    _info.Remove(Time.deltaTime);
                    return;
                }

                _info.Add(Time.deltaTime);
            }

            private void Update()
            {
                if (_player == null) return;
                transform.rotation = _player.transform.rotation;
                transform.position = _player.transform.position;
            }

            private void CuiSequence()
            {
                if (!_info.OverLimit)
                {
                    DestroyCui();
                    return;
                }

                ShowCui(Message.Read(_player.UserIDString));
            }

            private void ChatSequence()
            {
                if (_info.OverLimit)
                    Message.SendToChat(_player);
            }

            private void DamageSequence()
            {
                if (_info.ShouldShock)
                    StartCoroutine(DoShock());
            }

            public static TGChecker Create(BasePlayer player)
            {
                var go = new GameObject($"TGChecker for {player.userID}", typeof(SphereCollider)) { layer = 18 };
                //go.transform.SetParent(player.transform, false);
                return go.AddComponent<TGChecker>().SetPlayer(player);
            }

            private struct Info
            {
                public float Time;
                public bool OverLimit;

                public void Add(float deltaTime)
                {
                    Time += deltaTime;
                    OverLimit = true;
                }

                public void Remove(float deltaTime)
                {
                    Time = Mathf.MoveTowards(Time, 0, deltaTime);
                    OverLimit = false;
                }

                public void Clear()
                {
                    if (Time <= 0)
                        return;
                    Time = 0;
                    OverLimit = false;
                }

                public bool ShouldShock => OverLimit && Time >= _config.RadiusCheck.DelayBeforeShock;
                public string TimeBeforeShock => (_config.RadiusCheck.DelayBeforeShock - Time).ToString("0");
            }

            #region Cui

            private void ShowCui(string text)
            {
                if (!_mainCuiCreated)
                {
                    _config.Cui.AddMain(_player);
                    _mainCuiCreated = true;
                }

                _config.Cui.UpdateText(_player, text);
            }

            private void DestroyCui()
            {
                if (!_mainCuiCreated)
                    return;
                _config.Cui.Destroy(_player);
                _mainCuiCreated = false;
            }

            #endregion

            #region Trigger

            public override GameObject InterestedInObject(GameObject obj)
            {
                obj = base.InterestedInObject(obj);
                if (obj == null)
                    return null;

                var baseEntity = obj.ToBaseEntity();
                if (baseEntity == null)
                    return null;

                if (baseEntity.isClient)
                    return null;

                var basePlayer = baseEntity as BasePlayer;
                if (!basePlayer || basePlayer.IsDead() || basePlayer == _player || IsNpc(basePlayer))
                    return null;

                return baseEntity.gameObject;
            }

            public override void OnEntityEnter(BaseEntity ent)
            {
                var basePlayer = ent as BasePlayer;
                if (!basePlayer)
                    return;
                _players.Add(basePlayer);
            }

            public override void OnEntityLeave(BaseEntity ent)
            {
                var basePlayer = ent as BasePlayer;
                if (!basePlayer)
                    return;
                _players.Remove(basePlayer);
            }

            #endregion
        }

        #endregion

        #region Vars

        private static PluginConfig _config;
        private static TeamGuard _inst;

        [PluginReference] Plugin Duel, EventManager, ZoneManager;

        private readonly Dictionary<BasePlayer, uint> _lastUsed = new Dictionary<BasePlayer, uint>();
        private readonly Dictionary<BasePlayer, TGChecker> _checkers = new Dictionary<BasePlayer, TGChecker>();

        #endregion

        #region Localization

        private class ChatMessage
        {
            public static readonly ChatMessage CodeLockName = new ChatMessage("CodeLock");
            public static readonly ChatMessage CupboardName = new ChatMessage("Cupboard");
            public static readonly ChatMessage TurretName = new ChatMessage("Turret");
            public static readonly ChatMessage ClearMode = new ChatMessage("Clear");
            public static readonly ChatMessage ReplaceMode = new ChatMessage("Replace");

            private readonly object[] _args;
            private readonly string _langKey;

            public ChatMessage(string langKey)
            {
                _langKey = langKey;
                _args = new object[0];
            }

            public ChatMessage(string langKey, params object[] args)
            {
                _langKey = langKey;
                _args = args;
            }

            public string Read(string userId = null)
            {
                var reply = 5688;
                var format = GetMsg(_langKey, userId);
                if (_args.Length == 0)
                    return format;
                var args = new List<object>(_args.Length);
                foreach (var arg in _args)
                {
                    var message = arg as ChatMessage;
                    if (message == null)
                    {
                        args.Add(arg);
                        continue;
                    }

                    args.Add(message.Read(userId));
                }

                return string.Format(format, args.ToArray());
            }

            public void SendToChat(BasePlayer player)
            {
                player.ChatMessage(string.Format(_config.Generic.ChatFormat, Read(player.UserIDString)));
            }

            public void SendToConsole(BasePlayer player)
            {
                player.ConsoleMessage(string.Format(_config.Generic.ChatFormat, Read(player.UserIDString)));
            }
        }

        private static string GetMsg(string langKey, string userid = null)
        {
            return _inst.lang.GetMessage(langKey, _inst, userid);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TextBeforeDmg"] =
                    "You have more players around then allowed (Allowed - {0}).\nYou have {1} seconds before you will start to get damage.",
                ["TextWhileDmg"] =
                    "You have more players around then allowed (Allowed - {0}).\nAll players in the area would get {1} damage every {2} seconds " +
                    "until redundant player will leave the area.",
                ["LogLimit"] =
                    "Player \"{0}\" attempted to auth in the {1} of the player \"{2}\"\nPlayer position: {3} | Target position: {4}",
                ["LogCleared"] =
                    "Player \"{0}\" has authorized and cleared auth list of {1} of the player \"{2}\"\nPlayer position: {3} | Target position: {4}",
                ["LogReplaced"] =
                    "Player \"{0}\" has replaced player \"{1}\" in the {2} of the player  \"{3}\"\nPlayer position: {4} | Target position: {5}",
                ["Replace"] = "replace first authorized player",
                ["Clear"] = "authorize and clear previously authorized players list",
                ["ChatLimit"] = "This {0} has it's auth limit. (Max - {1}).\nRetry to {2}.",
                ["CodeLock"] = "code lock",
                ["Cupboard"] = "cupboard",
                ["Turret"] = "turret"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TextBeforeDmg"] =
                    "В зоне вокруг вас находится больше игроков, чем разрешено (Разрешено {0}).\nЧерез {1}с. все игроки в зоне начнут получать урон.",
                ["TextWhileDmg"] =
                    "В зоне вокруг вас находится больше игроков, чем разрешено (Разрешено {0}).\nВсем игрокам в зоне будет наносится урон " +
                    "в размере {1} каждые {2}с. до тех пор, пока лишние игроки не покинут зону проверки.",
                ["LogLimit"] =
                    "Игрок \"{0}\" попытался авторизоваться в {1} игрока \"{2}\"\nПозиция игрока: {3} | Позиция цели: {4}",
                ["LogCleared"] =
                    "Игрок \"{0}\" очистил список и авторизовался в {1} игрока \"{2}\"\nПозиция игрока: {3} | Позиция цели: {4}",
                ["LogReplaced"] =
                    "Игрок \"{0}\" заменил игрока \"{1}\" в списке авторизованных в {2} игрока \"{3}\"\nПозиция игрока: {4} | Позиция цели: {5}",
                ["Replace"] = "заменить первого авторизованно игрока",
                ["Clear"] = "авторизоваться и очистить список ранее авторизованных игроков",
                ["ChatLimit"] =
                    "Достигнут лимит авторизованных в {0}. (Максимально - {1})\nПовторите попытку для того, чтобы {2}.",
                ["CodeLock"] = "замке",
                ["Cupboard"] = "шкафу",
                ["Turret"] = "турели"
            }, this, "ru");
        }

        #endregion

        #region Config

        private class RadiusCheckConfig
        {
            [JsonProperty("Частота сообщений в чате")]
            public float ChatUpdateFrequency;

            [JsonProperty("Частота обновления UI")]
            public float CuiUpdateFrequency;

            [JsonProperty("Частота нанесения урона")]
            public float DamageFrequency;

            [JsonProperty("Наносимый урон за раз")]
            public float DamagePerTime;

            [JsonProperty("Разрешённое время нахождения рядом")]
            public float DelayBeforeShock;

            [JsonProperty("Отключить проверку по радиусу в безопасных зонах")]
            public bool DisableInSaveZone;

            [JsonProperty("Список эффектов, запускающихся при получении урона")]
            public List<string> Effects;

            [JsonProperty("Максимальное количество игроков в радиусе")]
            public int MaxPlayers;

            [JsonProperty("Радиус зоны проверки")]
            public float Radius;

            [JsonProperty("Использовать ли проверку по радиусу")]
            public bool Use;

            [JsonProperty("Выводить ли сообщения о нанесении урона в чат?")]
            public bool UseChat;

            [JsonProperty("Использовать ли графическую панель?")]
            public bool UseCui;

            [JsonIgnore]
            public string DamageFrequencyString;

            #region Default Config

            public static RadiusCheckConfig DefaultConfig => new RadiusCheckConfig
            {
                Use = true,
                DisableInSaveZone = true,
                MaxPlayers = 4,
                Radius = 10,
                DelayBeforeShock = 20,
                DamageFrequency = 5,
                DamagePerTime = 5,
                UseCui = true,
                CuiUpdateFrequency = 1,
                UseChat = false,
                ChatUpdateFrequency = 5,
                Effects = new List<string>
                {
                    "assets/prefabs/npc/autoturret/effects/targetacquired.prefab",
                    "assets/prefabs/weapons/hatchet/effects/strike_screenshake.prefab"
                }
            };

            #endregion

            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                DamageFrequencyString = DamageFrequency.ToString("0");
            }
        }

        private class GenericConfig
        {
            [JsonProperty("Формат сообщений в чате")]
            private string _chatFormat;

            [JsonIgnore]
            public string ChatFormat;

            [JsonProperty("Привилегия для просмотра логов")]
            public string LogPermission;

            [JsonProperty("Выводить лог в чат (false - в консоль)")]
            public bool LogToChat;

            #region Default Config

            public static GenericConfig DefaultConfig => new GenericConfig
            {
                LogPermission = "teamguard.log",
                LogToChat = true,
                _chatFormat = "[#f46600][TeamGuard][/#] {0}"
            };

            #endregion

            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                ChatFormat = Formatter.ToUnity(_chatFormat);
            }
        }

        private class CuiConfig
        {
            [JsonProperty("Цвет фона")]
            private string _color;

            [JsonProperty("Размер шрифта")]
            private int _fontSize;

            [JsonProperty("Максимальный отступ")]
            private string _max;

            [JsonProperty("Минимальный отступ")]
            private string _min;

            [JsonIgnore]
            private readonly string _textPanelName = CuiHelper.GetGuid();

            [JsonIgnore]
            private readonly string _mainPanelName = CuiHelper.GetGuid();

            #region Default Config

            public static CuiConfig DefaultConfig => new CuiConfig
            {
                _min = "0 0.355",
                _max = "1 0.655",
                _color = "0.30 0.01 0.01 0.80",
                _fontSize = 16
            };

            #endregion

            #region CUI

            private static readonly CuiElementContainer ReusableContainer = new CuiElementContainer();

            #region Elements

            [JsonIgnore]
            private CuiElement MainPanel => new CuiElement
            {
                Parent = "Overlay",
                Name = _mainPanelName,
                Components =
                {
                    new CuiImageComponent {Color = ToRustColor(_color)},
                    new CuiRectTransformComponent {AnchorMin = _min, AnchorMax = _max}
                }
            };

            private CuiElement GetTextPanel(string format, params object[] args)
            {
                var text = args.Length != 0 ? string.Format(format, args) : format;
                return new CuiElement
                {
                    Parent = _mainPanelName,
                    Name = _textPanelName,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text, FontSize = _fontSize, Font = "robotocondensed-regular.ttf",
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                };
            }

            #endregion

            public void AddMain(BasePlayer player)
            {
                if (!player || !player.IsConnected)
                    return;
                ReusableContainer.Clear();
                ReusableContainer.Add(MainPanel);
                CuiHelper.DestroyUi(player, _mainPanelName);
                CuiHelper.AddUi(player, ReusableContainer);
            }

            public void UpdateText(BasePlayer player, string format, params object[] args)
            {
                if (!player || !player.IsConnected)
                    return;
                ReusableContainer.Clear();
                ReusableContainer.Add(GetTextPanel(format, args));
                CuiHelper.DestroyUi(player, _textPanelName);
                CuiHelper.AddUi(player, ReusableContainer);
            }

            public void Destroy(BasePlayer player)
            {
                if (player?.IsConnected == true)
                    CuiHelper.DestroyUi(player, _mainPanelName);
            }

            #endregion
        }

        private class AdminsConfig
        {
            [JsonProperty("Необходимый уровень AuthLevel для игнорирования")]
            public int AuthLevel;

            [JsonProperty("Привилегия для игнорирования при проверке")]
            public string IgnorePermission;

            [JsonProperty("Игнорировать администраторов при проверке?")]
            public bool IgnoreAdmins;

            #region Default Config

            public static AdminsConfig DefaultConfig => new AdminsConfig
            {
                IgnoreAdmins = false,
                IgnorePermission = nameof(TeamGuard) + ".ignore",
                AuthLevel = 2
            };

            #endregion
        }

        private class EntityCheckConfig
        {
            [JsonProperty("Очищать список при превышении лимита")]
            public bool ClearAll;

            [JsonProperty("Максимум авторизаций")]
            public int PlayersLimit;

            [JsonProperty("Проверять авторизации")]
            public bool Use;

            #region Default Config

            public static EntityCheckConfig DefaultConfig => new EntityCheckConfig
            {
                Use = true,
                PlayersLimit = 4,
                ClearAll = false
            };

            #endregion

            public ChatMessage GetLimitMessage(ChatMessage entityName)
            {
                return new ChatMessage("ChatLimit", entityName, PlayersLimit,
                    ClearAll ? ChatMessage.ClearMode : ChatMessage.ReplaceMode);
            }
        }

        private class PluginConfig
        {
            [JsonProperty("Общие Настройки")]
            public GenericConfig Generic;

            [JsonProperty("Проверка администраторов")]
            public AdminsConfig Admins;

            [JsonProperty("Проверка игроков по радиусу")]
            public RadiusCheckConfig RadiusCheck;

            [JsonProperty("Настройки GUI")]
            public CuiConfig Cui;

            [JsonProperty("Авторизации в кодовых замках")]
            public EntityCheckConfig CodeLock;

            [JsonProperty("Авторизации в шкафах")]
            public EntityCheckConfig Cupboard;

            [JsonProperty("Авторизации в турелях")]
            public EntityCheckConfig Turret;

            [JsonProperty("Список зон ZoneManager, в которых не нужно вести проверку")]
            private List<string> _zonesList;

            #region Default Config

            public static PluginConfig DefaultConfig => new PluginConfig
            {
                Generic = GenericConfig.DefaultConfig,
                RadiusCheck = RadiusCheckConfig.DefaultConfig,
                Cui = CuiConfig.DefaultConfig,
                Admins = AdminsConfig.DefaultConfig,
                Cupboard = EntityCheckConfig.DefaultConfig,
                Turret = EntityCheckConfig.DefaultConfig,
                CodeLock = EntityCheckConfig.DefaultConfig,
                _zonesList = new List<string> { "zone1", "warzone", "safehouse" }
            };

            #endregion

            public bool InZone(Plugin zoneManager, BasePlayer player)
            {
                return zoneManager != null &&
                       _zonesList.Any(zoneId => (bool)zoneManager.Call("isPlayerInZone", zoneId, player));
            }

            public void Register(TeamGuard plugin)
            {
                plugin.permission.RegisterPermission(Generic.LogPermission, plugin);
                plugin.permission.RegisterPermission(Admins.IgnorePermission, plugin);
            }
        }

        #endregion

        #region Config initialization

        protected override void LoadDefaultConfig()
        {
            PrintWarning(
                "Благодарим за приобретение плагина на сайте RustPlugin.ru. Если вы приобрели этот плагин на другом ресурсе знайте - это лишает вас гарантированных обновлений!");
            _config = PluginConfig.DefaultConfig;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();

            if (ShouldUpdateConfig())
                SaveConfig();
            _config.Register(this);
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private bool ShouldUpdateConfig()
        {
            var res = false;
            //version < 2.1.0
            if (!ConfigExists<string>("Проверка администраторов", "Привилегия для игнорирования при проверке"))
            {
                res = true;
                PrintWarning("New option added to the config file: Admin ignore permission");
                _config.Admins.IgnorePermission = nameof(TeamGuard) + ".ignore";
            }
            return res;
        }
        private bool ConfigExists<T>(params string[] path)
        {
            var value = Config.Get(path);
            return value is T;
        }

        #endregion

        #region Initialization and quiting

        private void Init()
        {
            _inst = this;
            if (!_config.RadiusCheck.Use)
            {
                Unsubscribe("OnPlayerConnected");
                Unsubscribe("OnPlayerDisconnected");
                Unsubscribe("OnServerInitialized");
                return;
            }
            if (!_config.CodeLock.Use)
                Unsubscribe("OnCodeEntered");
            if (!_config.Turret.Use)
                Unsubscribe("OnTurretAuthorize");
            if (!_config.Cupboard.Use)
                Unsubscribe("OnCupboardAuthorize");
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            foreach (var checker in _checkers)
            {
                checker.Value.Kill();
            }
            _checkers.Clear();
            _config = null;
            _inst = null;
        }

        #endregion

        #region Oxide hooks (Player initialization)

        private void OnPlayerConnected(BasePlayer player)
        {
            TGChecker checker;
            if (_checkers.TryGetValue(player, out checker))
                checker.Kill();
            _checkers[player] = TGChecker.Create(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            TGChecker checker;
            if (!_checkers.TryGetValue(player, out checker))
                return;
            checker.Kill();
            _checkers.Remove(player);
        }

        #endregion

        #region Oxide Hooks (Authorization)

        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (HasAuth(player))
                return null;

            var isMainCode = code == codeLock.code;
            var isGuestCode = code == codeLock.guestCode;
            var canOpen = isMainCode || isGuestCode;
            if (!canOpen)
                return null;

            var parent = codeLock.GetParentEntity();
            var owner = GetName(parent.OwnerID);

            var whitelistPlayers = codeLock.whitelistPlayers.Where(x => !HasAuth(x)).ToList();
            var guestPlayers = codeLock.guestPlayers.Where(x => !HasAuth(x)).ToList();

            var count = guestPlayers.Count + whitelistPlayers.Count;
            if (count < _config.CodeLock.PlayersLimit)
                return null;

            uint lastUsedId;
            if (!_lastUsed.TryGetValue(player, out lastUsedId))
            {
                _config.CodeLock.GetLimitMessage(ChatMessage.CodeLockName).SendToChat(player);
                Log("LogLimit", player.displayName, ChatMessage.CodeLockName, owner, player.ServerPosition,
                    parent.ServerPosition);
                _lastUsed[player] = (uint)codeLock.net.ID.Value;
                return false;
            }

            if (lastUsedId != codeLock.net.ID.Value)
            {
                _lastUsed.Remove(player);
                return false;
            }

            if (_config.CodeLock.ClearAll)
            {
                Log("LogCleared", player.displayName, ChatMessage.CodeLockName, owner, player.ServerPosition,
                    parent.ServerPosition);
                if (isGuestCode)
                    codeLock.guestPlayers.Clear();
                else
                    codeLock.whitelistPlayers.Clear();
                codeLock.SendNetworkUpdate();
                return null;
            }

            ulong replaced;
            if (isGuestCode)
            {
                if (guestPlayers.Count == 0)
                {
                    _lastUsed.Remove(player);
                    return false;
                }

                replaced = codeLock.guestPlayers.FirstOrDefault(x => !HasAuth(x));

                if (replaced == 0)
                    return null;

                codeLock.guestPlayers.Remove(replaced);
            }
            else
            {
                if (whitelistPlayers.Count == 0)
                {
                    _lastUsed.Remove(player);
                    return false;
                }

                replaced = codeLock.whitelistPlayers.FirstOrDefault(x => !HasAuth(x));
                if (replaced == 0)
                    return null;

                codeLock.whitelistPlayers.Remove(replaced);
            }

            Log("LogReplaced", player.displayName, GetName(replaced), ChatMessage.CodeLockName, owner,
                player.ServerPosition, parent.ServerPosition);
            _lastUsed.Remove(player);
            return null;
        }

        private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (HasAuth(player))
                return null;
            var authorizedPlayers = privilege.authorizedPlayers.Where(x => !HasAuth(x.userid)).ToList();
            if (authorizedPlayers.Count < _config.Cupboard.PlayersLimit)
                return null;

            var owner = GetName(privilege.OwnerID);
            uint lastUsedId;
            if (!_lastUsed.TryGetValue(player, out lastUsedId))
            {
                _config.Cupboard.GetLimitMessage(ChatMessage.CupboardName).SendToChat(player);
                Log("LogLimit", player.displayName, ChatMessage.CupboardName, owner, player.ServerPosition,
                    privilege.ServerPosition);
                _lastUsed[player] = (uint)privilege.net.ID.Value;
                return false;
            }

            if (_config.Cupboard.ClearAll)
            {
                Log("LogCleared", player.displayName, ChatMessage.CupboardName, owner, player.ServerPosition,
                    privilege.ServerPosition);
                privilege.authorizedPlayers.Clear();
                privilege.SendNetworkUpdate();
                return null;
            }

            var replaced = privilege.authorizedPlayers.FirstOrDefault(x => !HasAuth(x.userid));

            if (replaced == null)
                return null;

            privilege.authorizedPlayers.RemoveAt(0);
            Log("LogReplaced", player.displayName, GetName(replaced.userid), ChatMessage.CupboardName, owner,
                player.ServerPosition, privilege.ServerPosition);
            _lastUsed.Remove(player);
            return null;
        }

        private object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (HasAuth(player))
                return null;
            var authorizedPlayers = turret.authorizedPlayers.Where(x => !HasAuth(x.userid)).ToList();

            if (authorizedPlayers.Count < _config.Turret.PlayersLimit)
                return null;

            var owner = GetName(turret.OwnerID);
            uint lastUsedId;
            if (!_lastUsed.TryGetValue(player, out lastUsedId))
            {
                _config.Turret.GetLimitMessage(ChatMessage.TurretName).SendToChat(player);
                Log("LogLimit", player.displayName, ChatMessage.TurretName, owner, player.ServerPosition,
                    turret.ServerPosition);
                _lastUsed[player] = (uint)turret.net.ID.Value;
                return false;
            }

            if (_config.Turret.ClearAll)
            {
                Log("LogCleared", player.displayName, ChatMessage.TurretName, owner, player.ServerPosition,
                    turret.ServerPosition);
                turret.authorizedPlayers.Clear();
                turret.SendNetworkUpdate();
                return null;
            }

            var replaced = turret.authorizedPlayers.FirstOrDefault(x => !HasAuth(x.userid));

            if (replaced == null)
                return null;
            turret.authorizedPlayers.Remove(replaced);
            Log("LogReplaced", player.displayName, GetName(replaced.userid), ChatMessage.TurretName, owner,
                player.ServerPosition, turret.ServerPosition);
            _lastUsed.Remove(player);
            return null;
        }

        #endregion

        #region Checks

        private static bool InSafeZone(BasePlayer player, ulong playerid = 5824512)
        {
            return _config.RadiusCheck.DisableInSaveZone && player.InSafeZone();
        }

        private static bool IsNpc(BasePlayer player)
        {
            //BotSpawn
            if (player is NPCPlayer)
                return true;
            return player.userID < 76560000000000000L;
        }

        private static bool HasAuth(BasePlayer player)
        {
            return _config.Admins.IgnoreAdmins && player?.net?.connection?.authLevel >= _config.Admins.AuthLevel;
        }

        private bool HasAuth(ulong playerid = 5824512)
        {
            if (!_config.Admins.IgnoreAdmins)
                return false;
            if (permission.UserHasPermission(playerid.ToString(), _config.Admins.IgnorePermission))
                return true;
            if (ServerUsers.Is(playerid, ServerUsers.UserGroup.Moderator))
            {
                return _config.Admins.AuthLevel >= 1;
            }
            if (ServerUsers.Is(playerid, ServerUsers.UserGroup.Owner))
            {
                return _config.Admins.AuthLevel >= 2;
            }
            if (DeveloperList.Contains(playerid))
            {
                return _config.Admins.AuthLevel >= 3;
            }

            return false;
        }

        private static bool IsDuelPlayer(BasePlayer player)
        {
            var dueler = _inst.Duel?.Call("IsPlayerOnActiveDuel", player);
            if (dueler is bool)
                return (bool)dueler;
            return false;
        }

        private static bool InWhitelistedZone(BasePlayer player)
        {
            return _config.InZone(_inst.ZoneManager, player);
        }

        private static bool IsEventPlayer(BasePlayer player)
        {
            var check = _inst.EventManager?.Call("isPlaying", player);
            if (check is bool)
                return (bool)check;
            return false;
        }

        private static bool CanSee(BasePlayer player, BasePlayer target)
        {
            return GamePhysics.LineOfSight(player.eyes.center, player.eyes.position, 2162688) &&
                   (target.IsVisible(player.eyes.HeadRay(),1218519041,float.PositiveInfinity) || target.IsVisible(player.eyes.position));
        }
        
        #endregion

        #region Helpers

        private string GetName(ulong id)
        {
            var player = covalence.Players.FindPlayerById(id.ToString());
            return player == null ? id.ToString() : player.Name;
        }

        private void Log(string langKey, params object[] args)
        {
            var message = new ChatMessage(langKey, args);
            LogToFile("Log", $"({DateTime.Now.ToShortTimeString()}) {message.Read()}", this);

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!permission.UserHasPermission(player.UserIDString, _config.Generic.LogPermission))
                    continue;

                if (_config.Generic.LogToChat)
                    message.SendToChat(player);
                else
                    message.SendToConsole(player);
            }
        }

        private static string ToRustColor(string input)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(input, out color))
                return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";

            var split = input.Split(' ');
            for (var i = 0; i < 4; i++)
            {
                float num;
                if (!float.TryParse(split[i], out num)) return null;
                color[i] = num;
            }

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}