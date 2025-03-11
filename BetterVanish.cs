using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Rust;
using Rust.AI;
using Rust.Registry;
using UnityEngine;
using QueryGrid = BaseEntity.Query;
using Human_Npc = global::HumanNPC;

namespace Oxide.Plugins
{
    [Info("BetterVanish", "CosaNostra", "1.7.3")]
    [Description("Customized & Optimized \"Vanish\"")]
    public class BetterVanish : RustPlugin
    {
        // Hooks: OnVanishDisappear(BasePlayer), OnVanishReappear(BasePlayer)
        // API: _IsInvisible(BasePlayer), _IsInvisible(IPlayer), _Disappear(BasePlayer), _Reappear(BasePlayer)

        #region Constants

        private const string PermUse = "bettervanish.allowed";
        private const string PermUseOther = "bettervanish.allowedother";
        private const string PermPermanent = "bettervanish.perma";
        private const string PermUnvanish = "bettervanish.unvanish";
        private const string PermInvSpy = "bettervanish.invspy";
        private const string PermSkipLocks = "bettervanish.skiplocks";
        private const string CuiName = "VanishIcon";
        private const string PlayerPrefab = "assets/prefabs/player/player.prefab";
        private const string PlayerCorpsePrefab = "assets/prefabs/player/player_corpse.prefab";
        private const string EffectDisappear = "assets/bundled/prefabs/fx/gestures/cameratakescreenshot.prefab";
        private const string EffectAppear = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";
        private static readonly Effect EffectInstance = new Effect();
        private static readonly DamageTypeList EmptyDmgList = new DamageTypeList();
        private static readonly GameObjectRef EmptyObjRef = new GameObjectRef();
        private static readonly object FalseObj = false;
        private static readonly object TrueObj = true;
        //
        private enum VanishEvent { Disappear, Reappear, Disconnect, Unload }
        //
        private static readonly List<string> HooksLst = new List<string>
        {
            nameof(OnEntityMarkHostile), nameof(OnEntityTakeDamage), nameof(OnPlayerDisconnected), nameof(CanUseLockedEntity), nameof(OnPlayerViolation), 
            nameof(OnTeamInvite), nameof(OnPlayerColliderEnable)
        };

        #endregion

        #region Fields

        private static BetterVanish _instance;
        private static CuiElementContainer _cui;
        private GameObjectRef _fallDmgEff = EmptyObjRef, _drownEff = EmptyObjRef;
        private bool _isShutdown;
        private bool _lastHooksState = true;

        #endregion

        #region Configuration

        private const int CfgRev = 4;
        private static Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Show visual indicator (true/false)")]
            public bool ShowIndicator;

            [JsonProperty(PropertyName = "Visual indicator image address")]
            public string IndicatorAddress;

            [JsonProperty(PropertyName = "Visual indicator anchor min")]
            public string IndicatorAnchorMin;

            [JsonProperty(PropertyName = "Visual indicator anchor max")]
            public string IndicatorAnchorMax;

            [JsonProperty(PropertyName = "Visual indicator color")]
            public string IndicatorColor;

            [JsonProperty("Depth of an underground teleport (upon disconnection)")]
            public float UndergroundTeleportDepth;

            [JsonProperty(PropertyName = "Block all incoming damage while vanished (true/false)")]
            public bool BlockAllIncomingDamage;

            [JsonProperty(PropertyName = "Block all outgoing damage while vanished (true/false)")]
            public bool BlockAllOutgoingDamage;

            [JsonProperty(PropertyName = "Auto vanish on connect (true/false)")]
            public bool AutoVanish;

            [JsonProperty(PropertyName = "Auto noclip on connect (true/false)")]
            public bool AutoNoclip;
            
            [JsonProperty(PropertyName = "Auto noclip on vanish (true/false)")]
            public bool AutoNoclipOnVanish;
            
            [JsonProperty(PropertyName = "Turn off noclip on reappear (true/false)")]
            public bool TurnOffNoclipOnReappear;

            [JsonProperty(PropertyName = "Persist vanish (don't unhide upon leave & restore after restart)")]
            public bool PersistVanish;

            [JsonProperty(PropertyName = "Use sound effects (true/false)")]
            public bool SoundEffects;
            
            [JsonProperty(PropertyName = "Enable safepoints (true/false)")]
            public bool SafePoints;

            [JsonProperty(PropertyName = "Remove all safepoints after wipe (true/false)")]
            public bool SafePointsRemoval;

            [JsonProperty(PropertyName = "Config revision (do not edit)")]
            public int ConfigRev;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    ShowIndicator = true,
                    IndicatorAddress = "https://i.imgur.com/dOvX4uA.png",
                    IndicatorColor = "0.7 0 0 1",
                    IndicatorAnchorMin = "0.1 0.001",
                    IndicatorAnchorMax = "0.17 0.10",
                    UndergroundTeleportDepth = 15,
                    BlockAllIncomingDamage = true,
                    BlockAllOutgoingDamage = true,
                    AutoVanish = true,
                    AutoNoclip = true,
                    AutoNoclipOnVanish = true,
                    TurnOffNoclipOnReappear = true, 
                    PersistVanish = true,
                    SoundEffects = true,
                    SafePoints = true,
                    SafePointsRemoval = true,
                    ConfigRev = CfgRev
                };
            }
        }

        private void MigrateConfig()
        {
            switch (_config.ConfigRev)
            {
                case 1:
                case 2:
                case 3:
                    _config.ConfigRev = CfgRev;
                    break;
            }
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if(_config.ConfigRev != CfgRev)
                    MigrateConfig();
            }
            catch
            {
                PrintWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Data
        
        private readonly ListHashSet<ulong> _hiddenPlayers = new ListHashSet<ulong>();
        private readonly ListHashSet<ulong> _hiddenPlayersPersist = new ListHashSet<ulong>();
        private Dictionary<ulong, string> _safePoints;

        private void LoadSafePoints() =>
            _safePoints = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>($"{nameof(BetterVanish)}-SafePoints");

        private void SaveSafePoints() => Interface.Oxide.DataFileSystem.WriteObject($"{nameof(BetterVanish)}-SafePoints", _safePoints);

        private void LoadPersistPlayers()
        {
            var lst = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>($"{nameof(BetterVanish)}-PersistPlr");
            _hiddenPlayersPersist.Clear();
            _hiddenPlayersPersist.AddRange(lst);
        }

        private void SavePersistPlayers() =>
            Interface.Oxide.DataFileSystem.WriteObject($"{nameof(BetterVanish)}-PersistPlr", new List<ulong>(_hiddenPlayersPersist));

        private void LoadData()
        {
            LoadSafePoints();
            LoadPersistPlayers();
        }

        private void SaveData()
        {
            SaveSafePoints();
            SavePersistPlayers();
        }
        
        #endregion

        #region Classes
        
        private class VanishComponent : MonoBehaviour, IEntity
        {
            private BasePlayer _owner;
            private GameObject _dummyObj;

            private static readonly ListHashSet<Type> InterestedTriggers = new ListHashSet<Type>
            {
                typeof(TriggerWorkbench), typeof(TriggerLadder), typeof(TriggerParent), typeof(TriggerParentEnclosed), typeof(TriggerParentExclusion),
                typeof(TriggerMount)
            };

            private void Awake()
            {
                _owner = GetComponent<BasePlayer>();
                if(_owner.IsConnected)
                    StartNetworkGroupsUpdate();
                SetupDummyCollider();
            }

            private void UpdateNetworkGroups()
            {
                if (_owner.IsConnected && !_owner.IsSpectating())
                    _owner.net.UpdateGroups(_owner.transform.position);
            }

            public void StopNetworkGroupsUpdate() => CancelInvoke(nameof(UpdateNetworkGroups));
            
            public void StartNetworkGroupsUpdate() => InvokeRepeating(nameof(UpdateNetworkGroups), 3f, 3f);
            
            private void OnDestroy()
            {
                Entity.Unregister(_dummyObj);
                _owner.RemoveFromTriggers();
                Destroy(_dummyObj);
                _dummyObj = null;
                _owner = null;
            }

            #region Trigger Subsystem

            public bool IsDestroyed => _owner.IsDestroyed;

            private void SetupDummyCollider()
            {
                _dummyObj = _owner.gameObject.CreateChild();
                _dummyObj.name = "VanishTrig";
                _dummyObj.tag = "DeployVolumeIgnore";
                Entity.Register(_dummyObj, this);
                _dummyObj.layer = (int)Layer.Player_Server;
                _dummyObj.AddComponent<SphereCollider>().isTrigger = true;
            }

            private IEnumerable<TriggerBase> GetValidTriggers(Collider collider) => collider.GetComponentsInParent<TriggerBase>().
                Where(t=>t.InterestedInObject(_owner.gameObject) && InterestedTriggers.Contains(t.GetType()));

            private void OnTriggerEnter(Collider collider)
            {
				if(_owner.IsSpectating())
					return;
                foreach (var trigger in GetValidTriggers(collider)) 
                    trigger.OnTriggerEnter(_owner.playerCollider);
            }

            private void OnTriggerExit(Collider collider)
            {
				if(_owner.IsSpectating())
					return;
                foreach (var trigger in GetValidTriggers(collider)) 
                    trigger.OnTriggerExit(_owner.playerCollider);
            }

            #endregion
            
        }

        private class LootProxyController : FacepunchBehaviour
        {
            public BasePlayer lootsrc;
            public BasePlayer looter;
            public PlayerCorpse proxy;

            public void Init(BasePlayer lootSource, BasePlayer looterPlayer)
            {
                lootsrc = lootSource;
                looter = looterPlayer;
            }

            private void Awake()
            {
                proxy = GetComponent<PlayerCorpse>();
                InvokeRepeating(LifeCheck, 1f, .65f);
            }

            private void PlayerStoppedLooting(BasePlayer player) => Destroy(this);

            private void LifeCheck()
            {
                if (lootsrc && looter && looter.IsConnected)
                    return;
                Destroy(this);
            }

            private void OnDestroy()
            {
                proxy.containers = null;
                proxy.Kill();
            }
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandVanish"] = "vanish",
                ["CommandSetVanish"] = "setvanish",
                ["CommandUnvanishAll"] = "unvanishall",
                ["CommandInvSpy"] = "invspy",
                ["VanishDisabled"] = "<color=#FF686B>Vanish disabled</color>",
                ["VanishEnabled"] = "<color=#91D6FF>Vanish enabled</color>",
                ["VanishDisabledOther"] = "<color=#FF686B>You disabled vanish on {0}</color>",
                ["VanishEnabledOther"] = "<color=#91D6FF>You enabled vanish on {0}</color>",
                ["VanishPermanent"] = "<color=#FF686B>You have no rights to disable vanish!</color>",
                ["UnvanishedPlayers"] = "<color=#FF686B>You unvanished {0} players!</color>",
                ["SetVanishHelp"] = "Syntax: /setvanish PlayerName",
                ["SafePointNotSet"] = "You haven\'t set your <color=#F7B267>Safe Point</color>!\nType <color=#FF686B>/vanish safepoint</color> to save your current position.\nYou will be automatically teleported there upon disconnection.",
                ["SafePointSaved"] = "Your current position is saved as a Safe Point!",
                ["MultiplePlayers"] = "Multiple players found!\nRefine your search or use Steam ID.",
                ["NothingInSight"] = "No players in sight",
                ["NoSuchPlayer"] = "No such player found ({0})",
                ["InvSpyLooting"] = "Looting: {0} ({1}).",
            }, this);
        }

        #endregion

        #region Initialization

        private void Init()
        {
            _instance = this;
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermUseOther, this);
            permission.RegisterPermission(PermPermanent, this);
            permission.RegisterPermission(PermUnvanish, this);
            permission.RegisterPermission(PermInvSpy, this);
            permission.RegisterPermission(PermSkipLocks, this);
            AddCommandAliases("CommandVanish", nameof(VanishCommand));
            AddCommandAliases("CommandSetVanish", nameof(SetVanishCommand));
            AddCommandAliases("CommandUnvanishAll", nameof(UnvanishAllCommand));
            AddCommandAliases("CommandInvSpy", nameof(InvSpyCommand));
            if (!_config.AutoNoclip && !_config.AutoVanish && !_config.PersistVanish)
                Unsubscribe(nameof(OnPlayerConnected));
            LoadData();
            ManageHooks();
            _cui = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = CuiName,
                    Parent = "Hud.Menu",
                    Components =
                    {
                        new CuiRawImageComponent { Color = _config.IndicatorColor, Url = _config.IndicatorAddress, Sprite = "assets/icons/refresh.png" },
                        new CuiRectTransformComponent { AnchorMin = _config.IndicatorAnchorMin, AnchorMax = _config.IndicatorAnchorMax }
                    }
                }
            };
        }
        
        private void OnServerInitialized()
        {
            var pp = GameManager.server.FindPrefab(PlayerPrefab).GetComponent<BaseEntity>().ToPlayer();
            _drownEff = pp.drownEffect;
            _fallDmgEff = pp.fallDamageEffect;
            DisappearAll();
        }

        private void OnNewSave(string _)
        {
            if (_config.SafePointsRemoval) 
                _safePoints.Clear();
            _hiddenPlayersPersist.Clear();
            SaveData();
        }

        #endregion

        #region De-initialization

        private void OnServerShutdown() => _isShutdown = true;

        private void Unload()
        {
            foreach (var uid in _hiddenPlayers.ToArray())
            {
                var plr = FindPlayerById(uid);
                if(plr)
                    HandleEvent(plr, VanishEvent.Unload);
            }
            SaveData();
            if (!_isShutdown) 
                ReappearAll();
            _config = null;
            _cui = null;
            _instance = null;
        }

        #endregion

        #region Commands

        private void VanishCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player.IsAdmin && !HasPerm(player.UserIDString, PermUse))
                return;
            if (args.Length > 0 && args[0].Equals("safepoint") && _config.SafePoints)
            {
                _safePoints[player.userID] = $"{player.transform.position.x} {player.transform.position.y} {player.transform.position.z}";
                Player.Message(player, Lang("SafePointSaved", player.UserIDString));
                return;
            }
            var isVanished = IsPlayerVanished(player);
            if (isVanished && IsPlayerPermanent(player))
            {
                Player.Message(player, Lang("VanishPermanent", player.UserIDString));
                return;
            }
            HandleEvent(player, isVanished ? VanishEvent.Reappear : VanishEvent.Disappear);
        }

        private void SetVanishCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player.IsAdmin && !HasPerm(player.UserIDString, PermUseOther))
                return;
            if (args.Length > 0)
            {
                var playerName = string.Join(" ", args);
                var result = FindPlayer(playerName);
                var target = result as BasePlayer;
                if (target == null && result != null)
                {
                    Player.Message(player, Lang("MultiplePlayers", player.UserIDString));
                    return;
                }

                if (target == null)
                {
                    Player.Message(player, Lang("NoSuchPlayer", player.UserIDString, playerName));
                    return;
                }

                var isVanished = IsPlayerVanished(target);
                HandleEvent(target, isVanished ? VanishEvent.Reappear : VanishEvent.Disappear);
                Player.Message(player, Lang(isVanished ? "VanishDisabledOther" : "VanishEnabledOther", player.UserIDString, target.displayName));
            }
            else
                Player.Message(player, Lang("SetVanishHelp", player.UserIDString));
        }

        private void UnvanishAllCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player.IsAdmin && !HasPerm(player.UserIDString, PermUnvanish))
                return;
            var count = 0;
            ReappearAll(_=>count++);
            Player.Message(player, Lang("UnvanishedPlayers", player.UserIDString, count));
        }

        private void InvSpyCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player.IsAdmin && (!HasPerm(player.UserIDString, PermInvSpy)))
                return;
            timer.In(.15f, () => InvSpyShowLoot(player, args));
        }

        #endregion

        #region Vanishing Act

        private void Disappear(BasePlayer player, bool showGui = true)
        {
            if (IsPlayerVanished(player))
                return;
            if (player.IsConnected)
            {
                if (!_hiddenPlayers.Contains(player.userID))
                    _hiddenPlayers.Add(player.userID);
                _hiddenPlayersPersist.Remove(player.userID);
            }
            else
                player.SetServerFall(false);
            player.syncPosition = false;
            player.limitNetworking = true;
            player.fallDamageEffect = EmptyObjRef;
            player.drownEffect = EmptyObjRef;
            player.GetHeldEntity()?.SetHeld(false);
            player.DisablePlayerCollider();
            VanishComponent vc;
            if (player.gameObject.TryGetComponent(out vc))
                vc.StartNetworkGroupsUpdate();
            else
                player.gameObject.AddComponent<VanishComponent>();
            SimpleAIMemory.AddIgnorePlayer(player);
            QueryGrid.Server.RemovePlayer(player);
            RemoveFromTargets(player);
            if (player.IsConnected)
            {
                if (showGui)
                    VanishGui(player);
                Player.Message(player, Lang("VanishEnabled", player.UserIDString));
                if (_config.SoundEffects)
                    SendEffectTo(EffectDisappear, player);
            }
            if(_config.AutoNoclipOnVanish && !player.isMounted && !player.IsFlying)
                player.SendConsoleCommand("noclip");
            ManageHooks();
            plugins.CallHook("OnVanishDisappear", player);
        }

        #endregion

        #region Reappearing Act

        private void Reappear(BasePlayer player)
        {
            if (!IsPlayerVanished(player) && !IsPlayerPersisted(player))
                return;
            _hiddenPlayers.Remove(player.userID);
            _hiddenPlayersPersist.Remove(player.userID);
            player.syncPosition = true;
            player.limitNetworking = false;
            UnityEngine.Object.Destroy(player.GetComponent<VanishComponent>());
            player.UpdateNetworkGroup();
            player.SendNetworkUpdate();
            player.GetHeldEntity()?.UpdateVisibility_Hand();
            player.EnablePlayerCollider();
            QueryGrid.Server.AddPlayer(player);
            SimpleAIMemory.RemoveIgnorePlayer(player);
            player.drownEffect = _drownEff;
            player.fallDamageEffect = _fallDmgEff;
            player.ForceUpdateTriggers();
            if (player.IsConnected)
            {
                DestroyVanishGui(player);
                Player.Message(player, Lang("VanishDisabled", player.UserIDString));
                if (_config.SoundEffects)
                    SendEffectTo(EffectAppear, player);
            }
            if(_config.TurnOffNoclipOnReappear && !player.isMounted && player.IsFlying)
                player.SendConsoleCommand("noclip");
            ManageHooks();
            plugins.CallHook("OnVanishReappear", player);
        }

        #endregion

        #region Hooks

        private object OnEntityTakeDamage(BaseEntity victim, HitInfo info)
        {
            var attacker = info?.InitiatorPlayer;
            if (victim == null && attacker == null)
                return null;
            var victimInvis = IsPlayerVanished(victim.ToPlayer());
            var attackerInvis = IsPlayerVanished(attacker);
            if (!victimInvis && !attackerInvis)
                return null;
            if (victimInvis && !_config.BlockAllIncomingDamage)
                return null;
            if (attackerInvis && !victimInvis && !_config.BlockAllOutgoingDamage)
                return null;
            if (info == null)
                return this;
            info.damageTypes = EmptyDmgList;
            info.HitMaterial = 0;
            info.DoHitEffects = false;
            info.PointStart = Vector3.zero;
            info.HitEntity = null;
            return this;
        }

        private object OnEntityMarkHostile(BasePlayer player) => IsPlayerVanished(player) ? this : null;

        private object OnPlayerColliderEnable(BasePlayer player, CapsuleCollider _) => IsPlayerVanished(player) ? this : null;

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (!IsPlayerVanished(player))
                return null;
            if(player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermSkipLocks))
                return TrueObj;
            var cl = baseLock as CodeLock;
            if (cl != null)
                return cl.whitelistPlayers.Contains(player.userID) || cl.guestPlayers.Contains(player.userID) ? TrueObj : FalseObj;
            return null;
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount) => IsPlayerVanished(player) ? FalseObj : null;

        private object OnTeamInvite(BasePlayer inviter, BasePlayer target) => IsPlayerVanished(inviter) ? FalseObj : null;
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.IsConnected)
                return;
            if(!player.IsAdmin && !HasPerm(player.UserIDString, PermUse))
                return;
            if((_config.PersistVanish && IsPlayerPersisted(player)) || _config.AutoVanish || IsPlayerPermanent(player))
                HandleEvent(player, VanishEvent.Disappear);
            if (_config.AutoNoclip)
                player.SendConsoleCommand("noclip");
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (!_hiddenPlayers.Contains(player.userID))
                return;
            HandleEvent(player, VanishEvent.Disconnect);
        }

        #endregion

        #region GUI Indicator

        private static void VanishGui(BasePlayer player)
        {
            if (!_config.ShowIndicator)
                return;
            DestroyVanishGui(player);
            CuiHelper.AddUi(player, _cui);
        }

        private static void DestroyVanishGui(BasePlayer player)
        {
            if (!_config.ShowIndicator)
                return;
            CuiHelper.DestroyUi(player, CuiName);
        }

        #endregion

        #region Inventory Spy Logic

        private void InvSpyShowLoot(BasePlayer player, string[] args)
        {
            if (player.inventory.loot.IsLooting() || player.IsDead())
                return;
            BasePlayer target = null;
            var playerName = args != null ? string.Join(" ", args) : null;
            if (string.IsNullOrWhiteSpace(playerName))
            {
                var hits = Pool.GetList<RaycastHit>();
                GamePhysics.TraceAll(player.eyes.HeadRay(), .1f, hits, 5f, Layers.Server.Players);
                var hit = hits.FirstOrDefault(h => h.collider.gameObject != player.gameObject);
                if (hit.collider != null)
                    target = hit.transform.ToBaseEntity().ToPlayer();
                Pool.FreeList(ref hits);
            }
            else
            {
                var result = FindPlayer(playerName);
                target = result as BasePlayer;
                if (target == null && result != null)
                {
                    Player.Message(player, Lang("MultiplePlayers", player.UserIDString));
                    return;
                }
            }
            if (target == null)
            {
                Player.Message(player,
                    $"{(string.IsNullOrWhiteSpace(playerName) ? Lang("NothingInSight", player.UserIDString) : Lang("NoSuchPlayer", player.UserIDString, playerName))}.");
                return;
            }
            var proxyCorpse = InvSpyCreateProxy(player, target);
            player.inventory.loot.StartLootingEntity(proxyCorpse, false);
            player.inventory.loot.AddContainer(target.inventory.containerMain);
            player.inventory.loot.AddContainer(target.inventory.containerWear);
            player.inventory.loot.AddContainer(target.inventory.containerBelt);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "player_corpse");
            Player.Message(player, Lang("InvSpyLooting", player.UserIDString, GetPrettyPlayerName(target), target.UserIDString));
        }

        private static PlayerCorpse InvSpyCreateProxy(BasePlayer receiver, BasePlayer target)
        {
            var prx = (PlayerCorpse) GameManager.server.CreateEntity(PlayerCorpsePrefab, TerrainMeta.LowestPoint);
            prx.enableSaving = false;
            prx.syncPosition = false;
            prx.playerSteamID = receiver.userID;
            prx.playerName = GetPrettyPlayerName(target);
            prx.parentEnt = null;
            prx.Spawn();
            prx.InitializeHealth(float.MaxValue, float.MaxValue);
            prx.gameObject.AddComponent<LootProxyController>().Init(target, receiver);
            UnityEngine.Object.Destroy(prx.GetComponent<Buoyancy>());
            UnityEngine.Object.Destroy(prx.GetComponent<Rigidbody>());
            prx.CancelInvoke(prx.RemoveCorpse);
            prx.SetFlag(BaseEntity.Flags.Busy, true);
            SendEntitySnapshotEx(receiver, prx);
            return prx;
        }

        #endregion

        #region Logic

        private void HandleEvent(BasePlayer player, VanishEvent evt)
        {
            switch (evt)
            {
                case VanishEvent.Disappear:
                    Disappear(player);
                    if (_config.SafePoints && !HasSafePoint(player) && player.IsConnected && !player.IsSleeping()
                        && (player.IsAdmin || HasPerm(player.UserIDString, PermUse))) 
                        Player.Message(player, Lang("SafePointNotSet", player.UserIDString));
                    break;
                case VanishEvent.Reappear:
                    Reappear(player);
                    break;
                case VanishEvent.Disconnect:
                    NextTick(()=>player.SetServerFall(false));
                    if (((player.IsAdmin && _config.UndergroundTeleportDepth != 0f) || (_config.SafePoints && HasSafePoint(player)))) 
                        player.Teleport(GetSafePoint(player));
                    var vanishStay = false;
                    if (CanPersistPlayer(player))
                    {
                        AddPersistPlayer(player);
                        vanishStay = true;
                    }
                    if (IsPlayerPermanent(player))
                        vanishStay = true;
                    if (vanishStay)
                    {
                        _hiddenPlayers.Remove(player.userID);
                        player.GetComponent<VanishComponent>()?.StopNetworkGroupsUpdate();
                        NextTick(()=>player.DisablePlayerCollider());
                        ManageHooks();
                    }
                    else
                        Reappear(player);
                    break;
                case VanishEvent.Unload:
                    if (CanPersistPlayer(player)) 
                        AddPersistPlayer(player);
                    break;
            }
        }

        private static void RemoveFromTargets(BasePlayer player)
        {
            var hits = QueryGrid.Server.GetInSphere(player.GetNetworkPosition(), 64f, AIBrainSenses.queryResults,
                ent => ent is Human_Npc || ent is BaseAnimalNPC || ent is BaseFishNPC || ent is BradleyAPC);
            for (var i = 0; i < hits; i++)
            {
                var ent = AIBrainSenses.queryResults[i];
                if(ent is BradleyAPC)
                {
                    var apc = ((BradleyAPC)ent);
                    var ti = apc.targetList.FirstOrDefault(t => t.entity == player);
                    if (ti != null)
                    {
                        apc.targetList.Remove(ti);
                        Pool.Free(ref ti);
                        apc.UpdateTargetList();
                    }
                }
                else
                    RemoveFromTargets0(player, ent.GetComponent<BaseAIBrain>());
            }
        }

        private static void RemoveFromTargets0(BasePlayer player, BaseAIBrain brain)
        {
            if(brain == null)
                return;
            try
            {
                if(brain.Events != null && brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot) == player)
                    brain.Events.Memory.Entity.Remove(brain.Events.CurrentInputMemorySlot);
                brain.Senses.Memory.Players.Remove(player);
                brain.Senses.Memory.Targets.Remove(player);
                brain.Senses.Memory.Threats.Remove(player);
                brain.Senses.Memory.LOS.Remove(player);
                brain.Senses.Memory.All.RemoveAll(si => si.Entity == player);
            }
            catch (Exception e)
            {
                _instance.PrintError(
                    $"RemoveFromTargets failure ({e.GetType()})! Brain: {brain.GetType().FullName}. [E:{brain.Events != null};S:{brain.Senses != null};SM:{brain.Senses?.Memory != null}]. Please report to us!");
            }
        }

        private void ReappearAll(Action<BasePlayer> onReappear = null)
        {
            var results = _hiddenPlayers.AsEnumerable();
            if (_config.PersistVanish)
                results = results.Concat(_hiddenPlayersPersist);
            foreach (var hiddenPlayer in results.Distinct().Select(uid => FindPlayerById(uid)))
            {
                if (!hiddenPlayer)
                    continue;
                Reappear(hiddenPlayer);
                onReappear?.Invoke(hiddenPlayer);
            }
        }

        private void DisappearAll(Action<BasePlayer> onDisappear = null)
        {
            var results = BasePlayer.allPlayerList.Where(p => IsPlayerPermanent(p) && !IsPlayerVanished(p));
            if (_config.PersistVanish)
                results = results.Concat(_hiddenPlayersPersist.Select(uid=> FindPlayerById(uid)).Where(p=>p));
            foreach (var hiddenPlayer in results.Distinct())
            {
                if (!hiddenPlayer || (!hiddenPlayer.IsAdmin && !HasPerm(hiddenPlayer.UserIDString, PermUse)))
                    continue;
                Disappear(hiddenPlayer);
                onDisappear?.Invoke(hiddenPlayer);
            }
        }
        
        private void AddPersistPlayer(BasePlayer player)
        {
            if(!_hiddenPlayersPersist.Contains(player.userID))
                _hiddenPlayersPersist.Add(player.userID);
            _hiddenPlayers.Remove(player.userID);
            ManageHooks();
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key)))
                    AddCovalenceCommand(message.Value, command);
            }
        }

        private void SubscribeToHooks()
        {
            foreach (var hook in HooksLst)
                Subscribe(hook);
        }

        private void UnSubscribeFromHooks()
        {
            foreach (var hook in HooksLst)
                Unsubscribe(hook);
        }

        private static void SendEffectTo(string effect, BasePlayer player)
        {
            EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
            EffectInstance.pooledstringid = StringPool.Get(effect);
            var nw = Net.sv.StartWrite();
            nw.PacketID(Message.Type.Effect);
            EffectInstance.WriteToStream(nw);
            nw.Send(new SendInfo(player.net.connection));
            EffectInstance.Clear();
        }

        private bool HasSafePoint(BasePlayer player) => _config.SafePoints && _safePoints.ContainsKey(player.userID);

        private Vector3 GetSafePoint(BasePlayer player)
        {
            try
            {
                if (HasSafePoint(player))
                    return Vector3Ex.Parse(_safePoints[player.userID]);
            }
            catch { }
            return new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) - _config.UndergroundTeleportDepth, player.transform.position.z);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private bool IsPlayerVanished(BasePlayer player) => player != null && _hiddenPlayers.Contains(player.userID);
        
        private bool IsPlayerVanished(ulong playerId) => _hiddenPlayers.Contains(playerId);

        private bool IsPlayerPersisted(BasePlayer player) => player != null && _hiddenPlayersPersist.Contains(player.userID);

        private bool IsPlayerPermanent(BasePlayer player) => HasPerm(player.UserIDString, PermPermanent) && HasPerm(player.UserIDString, PermUse);
        
        private bool CanPersistPlayer(BasePlayer player) =>
            _config.PersistVanish && !IsPlayerPermanent(player) && (player.IsAdmin || HasPerm(player.UserIDString, PermUse));

        private static object FindPlayer(string name, bool sleeping = true)
        {
            var player = BasePlayer.Find(name) ?? BasePlayer.FindSleeping(name);
            if (player != null)
                return player;
            var list = BasePlayer.activePlayerList.ToArray();
            if (sleeping)
                list = list.Concat(BasePlayer.sleepingPlayerList.Values).ToArray();
            var result = list.Where(p =>
                p.displayName.StartsWith(name, StringComparison.InvariantCultureIgnoreCase) || p.displayName.Contains(name, CompareOptions.IgnoreCase));
            var players = result as BasePlayer[] ?? result.ToArray();
            if (players.Length == 0)
                return null;
            if (players.Length > 1)
                return players;
            return players[0];
        }
        
        private static BasePlayer FindPlayerById(ulong uid) => BasePlayer.FindByID(uid) ?? BasePlayer.FindSleeping(uid);

        private static void SendEntitySnapshotEx(BaseNetworkable receiver, BaseNetworkable ent)
        {
            if (ent == null || ent.net == null)
                return;
            ++receiver.net.connection.validate.entityUpdates;
            var saveInfo = new BaseNetworkable.SaveInfo
            {
                forConnection = receiver.net.connection,
                forDisk = false
            };
            var nw = Net.sv.StartWrite();
            nw.PacketID(Message.Type.Entities);
            nw.UInt32(receiver.net.connection.validate.entityUpdates);
            ent.ToStreamForNetwork(nw, saveInfo);
            nw.Send(new SendInfo(receiver.net.connection));
        }

        private static string GetPrettyPlayerName(BasePlayer player)
        {
            if (player.IsNpc)
                return $"*NPC* {player.ShortPrefabName}";
            else if (!player.IsConnected)
                return $"*Offline* {player.displayName}";
            else if (player.IsSleeping())
                return $"*Sleeping* {player.displayName}";
            return $"{player.displayName}";
        }
        
        private void ManageHooks()
        {
            var newState = _hiddenPlayers.Count > 0;
            if(newState == _lastHooksState)
                return;
            if(newState)
                SubscribeToHooks();
            else
                UnSubscribeFromHooks();
            _lastHooksState = newState;
        }
    
        #endregion

        #region API

        private bool _IsInvisible(BasePlayer player) => IsPlayerVanished(player);
        
        private bool _IsInvisible(IPlayer player) => IsPlayerVanished((BasePlayer)player.Object);

        private void _Disappear(BasePlayer player) => HandleEvent(player, VanishEvent.Disappear);

        private void _Reappear(BasePlayer player) => HandleEvent(player, VanishEvent.Reappear);

        #endregion

    }
}
