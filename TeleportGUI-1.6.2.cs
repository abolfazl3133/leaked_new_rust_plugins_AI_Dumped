using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    /*
        This plugin was written solely by PsychoTea.
        Please do not, under any circumstances, redistribute this code.
        If you find any bugs, please report them either directly to me at bensparkes8@gmail.com,
        or on the chaoscode.io page.
        The same applies for feature requests.
        For any enquiries please also email me at bensparkes8@gmail.com
        I do write private plugins.
    */

    [Info("TeleportGUI", "PsychoTea", "1.6.2")]

    class TeleportGUI : RustPlugin
    {
        #region Fields

        private const string permUse = "teleportgui.use";
        private const string permCancel = "teleportgui.tpcancel";
        private const string permBack = "teleportgui.tpback";
        private const string permHere = "teleportgui.tphere";
        private const string permSleepers = "teleportgui.sleepers";

        private static TeleportGUI Instance;

        private Dictionary<BasePlayer, bool> GUIOpen = new Dictionary<BasePlayer, bool>();
        private Dictionary<BasePlayer, Vector3> LastTeleport = new Dictionary<BasePlayer, Vector3>();
        private List<GameObject> GameObjects = new List<GameObject>();

        private bool DebuggingMode = false;

        [PluginReference] private Plugin Economics, ServerRewards, NoEscape, ZoneManager;

        #endregion

        #region Classes 

        class GUIManager
        {
            public static Dictionary<BasePlayer, GUIManager> Players = new Dictionary<BasePlayer, GUIManager>();

            public int Page = 1;
            public bool TPHere = false;
            public bool Sleepers = false;

            public static GUIManager Get(BasePlayer player)
            {
                if (Players.ContainsKey(player)) return Players[player];
                Players.Add(player, new GUIManager());
                return Players[player];
            }
        }

        class TeleportRequest : MonoBehaviour
        {
            private BasePlayer _from;
            private BasePlayer _to;

            private bool _tpHere;
            private double _time;
            private bool _playerIsPaying;

            public static void Create(BasePlayer from, BasePlayer to, double timeoutTime, bool tpHere, bool playerIsPaying)
            {
                var gameObject = new GameObject();

                var tpReq = gameObject.AddComponent<TeleportRequest>();
                tpReq._from = from;
                tpReq._to = to;
                tpReq._tpHere = tpHere;
                tpReq._time = timeoutTime;
                tpReq._playerIsPaying = playerIsPaying;

                Instance.GameObjects.Add(gameObject);
            }

            void Start()
            {
                if (_tpHere)
                {
                    Instance.SendReply(_from, Instance.GetMessage("HereRequestSent").Replace("{0}", _to.displayName));
                    Instance.SendReply(_to, Instance.GetMessage("HereRequestRecieved").Replace("{0}", _from.displayName));
                }
                else
                {
                    Instance.SendReply(_from, Instance.GetMessage("RequestSent").Replace("{0}", _to.displayName));
                    Instance.SendReply(_to, Instance.GetMessage("RequestRecieved").Replace("{0}", _from.displayName));
                }

                PendingRequest pr = _to.gameObject.AddComponent<PendingRequest>();
                pr.From = _from;
                pr.TeleportRequest = this;
                InvokeRepeating("TimerTick", 0, 1.0f);
            }

            void TimerTick()
            {
                if (_time == 0) RequestTimeOut();
                _time--;
            }

            public void RequestAccepted()
            {
                int timeUntilTP = Instance.ConfigFile.DefaultTimeUntilTeleport;

                var lowestTimeUntilTP = Instance.ConfigFile.TimeUntilTeleport
                    .Where(x => Instance.permission.UserHasPermission(_from.UserIDString, x.Key) &&
                                x.Value < timeUntilTP)
                    .OrderBy(x => x.Value)
                    .FirstOrDefault();
                if (!lowestTimeUntilTP.Equals(default(KeyValuePair<string, int>)))
                {
                    timeUntilTP = lowestTimeUntilTP.Value;
                }

                Instance.SendReply(_from, Instance.GetMessage("RequestToAccepted").Replace("{0}", _to.displayName).Replace("{1}", timeUntilTP.ToString("N1")));
                Instance.SendReply(_to, Instance.GetMessage("RequestFromAccepted").Replace("{0}", _from.displayName).Replace("{1}", timeUntilTP.ToString("N1")));

                Teleporter teleporter = _from.gameObject.AddComponent<Teleporter>();
                teleporter.Create(_from, _to, timeUntilTP);

                PendingRequest pr = _to.gameObject.GetComponent<PendingRequest>();
                if (pr != null) GameObject.Destroy(pr);

                CancelInvoke();

                GameObject.Destroy(this.gameObject);
            }

            public void RequestDeclined()
            {
                if (_from != null)
                {
                    if (_tpHere)
                    {
                        Instance.SendReply(_from, Instance.GetMessage("HereRequestToDenied").Replace("{0}", _to.displayName));
                    }
                    else
                    {
                        Instance.SendReply(_from, Instance.GetMessage("RequestToDenied").Replace("{0}", _to.displayName));
                    }

                    if (Instance.EconomicsInstalled() &&
                        Instance.ConfigFile.UseEconomicsPlugin &&
                        _playerIsPaying)
                    {
                        Instance.RefundPlayerEconomics(_from);
                    }

                    if (Instance.ServerRewardsInstalled() &&
                        Instance.ConfigFile.UseServerRewardsPlugin &&
                        _playerIsPaying)
                    {
                        Instance.RefundServerRewards(_from);
                    }
                }

                if (_to != null)
                {
                    if (_tpHere)
                    {
                        Instance.SendReply(_to, Instance.GetMessage("HereRequestFromDenied").Replace("{0}", _from.displayName));
                    }
                    else
                    {
                        Instance.SendReply(_to, Instance.GetMessage("RequestFromDenied").Replace("{0}", _from.displayName));
                    }
                }

                PendingRequest pendingReq = _to?.gameObject?.GetComponent<PendingRequest>();
                if (pendingReq != null) GameObject.Destroy(pendingReq);

                GameObject.Destroy(this.gameObject);
            }

            public void RequestCancelled()
            {
                if (_from != null)
                {
                    Instance.SendReply(_from, Instance.GetMessage("TeleportRequestToCancelled").Replace("{0}", _to.displayName));

                    if (Instance.EconomicsInstalled() &&
                        Instance.ConfigFile.UseEconomicsPlugin &&
                        _playerIsPaying)
                    {
                        Instance.RefundPlayerEconomics(_from);
                    }

                    if (Instance.ServerRewardsInstalled() &&
                        Instance.ConfigFile.UseServerRewardsPlugin &&
                        _playerIsPaying)
                    {
                        Instance.RefundServerRewards(_from);
                    }
                }

                if (_to != null)
                {
                    Instance.SendReply(_to, Instance.GetMessage("TeleportRequestFromCancelled").Replace("{0}", _from.displayName));
                }

                PendingRequest pr = _to?.gameObject?.GetComponent<PendingRequest>();
                if (pr != null) GameObject.Destroy(pr);

                GameObject.Destroy(this.gameObject);
            }

            void RequestTimeOut()
            {
                if (_from != null)
                {
                    if (_tpHere)
                    {
                        Instance.SendReply(_from, Instance.GetMessage("HereRequestToTimedOut").Replace("{0}", _to.displayName));
                    }
                    else
                    {
                        Instance.SendReply(_from, Instance.GetMessage("RequestToTimedOut").Replace("{0}", _to.displayName));
                    }

                    if (Instance.EconomicsInstalled() && Instance.ConfigFile.UseEconomicsPlugin)
                        Instance.RefundPlayerEconomics(_from);
                    if (Instance.ServerRewardsInstalled() && Instance.ConfigFile.UseServerRewardsPlugin)
                        Instance.RefundServerRewards(_from);
                }

                if (_to != null)
                {
                    if (_tpHere)
                    {
                        Instance.SendReply(_to, Instance.GetMessage("HereRequestFromTimedOut").Replace("{0}", _from.displayName));
                    }
                    else
                    {
                        Instance.SendReply(_to, Instance.GetMessage("RequestFromTimedOut").Replace("{0}", _from.displayName));
                    }
                }

                PendingRequest pr = _to?.gameObject?.GetComponent<PendingRequest>();
                if (pr != null) GameObject.Destroy(pr);
                GameObject.Destroy(this.gameObject);
            }

            void CancelRequest()
            {
                Instance.SendReply(_from, Instance.GetMessage("BlockTPTakeDamage"));

                if (Instance.EconomicsInstalled() && Instance.ConfigFile.UseEconomicsPlugin)
                    Instance.RefundPlayerEconomics(_from);

                if (Instance.ServerRewardsInstalled() && Instance.ConfigFile.UseServerRewardsPlugin)
                    Instance.RefundServerRewards(_from);

                PendingRequest pr = _to.gameObject.GetComponent<PendingRequest>();
                if (pr != null) GameObject.Destroy(pr);

                GameObject.Destroy(this.gameObject);
            }

            void OnDestroy()
            {
                CancelInvoke();
                Instance.GameObjects.Remove(this.gameObject);
            }
        }

        class PendingRequest : MonoBehaviour
        {
            public BasePlayer From;
            public TeleportRequest TeleportRequest;
        }

        class Teleporter : MonoBehaviour
        {
            private GameObject _gameObject;
            private BasePlayer _from;
            private BasePlayer _to;
            private int _timeUtilTeleport;

            public void Create(BasePlayer from, BasePlayer to, int timeUntilTeleport)
            {
                this._gameObject = new GameObject();
                this._timeUtilTeleport = timeUntilTeleport;
                this._from = from;
                this._to = to;

                Instance.GameObjects.Add(_gameObject);
            }

            void Start() => InvokeRepeating("TimerTick", 0, 1.0f);

            void TimerTick()
            {
                if (_timeUtilTeleport == 0) Teleport();
                _timeUtilTeleport--;
            }

            void Teleport()
            {
                Vector3 currentPos = _from.transform.position;
                Instance.RecordLastTP(_from, currentPos);

                Instance.Teleport(_from, _to);

                Instance.SendReply(_from, Instance.GetMessage("YouTeleportedTo").Replace("{0}", _to.displayName));
                Instance.SendReply(_to, Instance.GetMessage("TeleportedToYou").Replace("{0}", _from.displayName));

                double cooldown = Instance.ConfigFile.DefaultCooldown;

                var lowestCooldown = Instance.ConfigFile.Cooldowns
                    .Where(x => Instance.permission.UserHasPermission(_from.UserIDString, x.Key) &&
                                x.Value < cooldown)
                    .OrderBy(x => x.Value)
                    .FirstOrDefault();
                if (!lowestCooldown.Equals(default(KeyValuePair<string, double>)))
                {
                    cooldown = lowestCooldown.Value;
                }

                Instance.storedData.Cooldowns.Add(_from.userID, cooldown);

                if (Instance.ConfigFile.DefaultDailyLimit > 0 &&
                    !Instance.HasReachedDailyLimit(_from))
                {
                    int usesRemaining = Instance.IncrementUses(_from);
                    Instance.SendReply(_from, Instance.GetMessage("TeleportsRemaining").Replace("{0}", usesRemaining.ToString()));
                }

                GameObject.Destroy(this.gameObject.GetComponent<Teleporter>());
            }

            public void CancelTeleport()
            {
                Instance.SendReply(_from, Instance.GetMessage("TeleportToCancelled").Replace("{0}", _to.displayName));
                Instance.SendReply(_to, Instance.GetMessage("TeleportFromCancelled").Replace("{0}", _from.displayName));

                if (Instance.EconomicsInstalled() && Instance.ConfigFile.UseEconomicsPlugin)
                    Instance.RefundPlayerEconomics(_from);

                if (Instance.ServerRewardsInstalled() && Instance.ConfigFile.UseServerRewardsPlugin)
                    Instance.RefundServerRewards(_from);

                GameObject.Destroy(this.gameObject.GetComponent<Teleporter>());
            }

            void StartSleeping(BasePlayer player)
            {
                if (player.IsSleeping())
                    return;
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                if (!BasePlayer.sleepingPlayerList.Contains(player))
                    BasePlayer.sleepingPlayerList.Add(player);
                player.CancelInvoke("InventoryUpdate");
            }

            void OnDestory()
            {
                CancelInvoke();
                Instance.GameObjects.Remove(_gameObject);
            }
        }

        class CooldownManager : MonoBehaviour
        {
            GameObject GameObject;

            public static void Create()
            {
                CooldownManager cm = new CooldownManager();
                cm.GameObject = new GameObject();
                cm.GameObject.AddComponent<CooldownManager>();
                Instance.GameObjects.Add(cm.GameObject);
            }

            void Start()
            {
                InvokeRepeating("TimerTick", 0, 1.0f);
            }

            void TimerTick()
            {
                if (Instance?.storedData?.Cooldowns == null) return;

                foreach (KeyValuePair<ulong, double> kvp in new Dictionary<ulong, double>(Instance.storedData.Cooldowns))
                {
                    Instance.storedData.Cooldowns[kvp.Key]--;

                    if (kvp.Value == 0)
                    {
                        Instance.storedData.Cooldowns.Remove(kvp.Key);
                    }
                }
            }

            void OnDestroy()
            {
                Instance?.SaveData();
                CancelInvoke();
                Instance.GameObjects.Remove(GameObject);
            }
        }

        class StoredData
        {
            public Dictionary<ulong, double> Cooldowns = new Dictionary<ulong, double>();
            public Dictionary<ulong, int> UsesToday = new Dictionary<ulong, int>();
        }
        StoredData storedData;

        #endregion

        #region Oxide Hooks

        void Init()
        {
            // Debugging mode should be enabled?
            if (ConVar.Server.hostname == "PsychoTea's Testing Server")
            {
                DebuggingMode = true;
                Puts("Debugging mode enabled.");
            }

            // Register regular permissions
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permCancel, this);
            permission.RegisterPermission(permBack, this);
            permission.RegisterPermission(permHere, this);
            permission.RegisterPermission(permSleepers, this);

            // Register config permissions
            foreach (string perm in ConfigFile.Cooldowns.Keys)
                if (!permission.PermissionExists(perm, this))
                    permission.RegisterPermission(perm, this);

            foreach (string perm in ConfigFile.DailyLimit.Keys)
                if (!permission.PermissionExists(perm, this))
                    permission.RegisterPermission(perm, this);

            foreach (string perm in ConfigFile.TimeUntilTeleport.Keys)
                if (!permission.PermissionExists(perm, this))
                    permission.RegisterPermission(perm, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You do not have permission to use this command." },
                { "TeleportTitle", "Teleport" },
                { "RequestSent", "Teleport request sent to {0}." },
                { "HereRequestSent", "Teleport here request sent to {0}." },
                { "RequestRecieved", "Teleport request from {0}. Open the teleport GUI (/tp) to accept." },
                { "HereRequestRecieved", "Teleport here request from {0}. Open the teleport GUI (/tp) to accept." },
                { "RequestToTimedOut", "Your teleport request to {0} timed out." },
                { "HereRequestToTimedOut", "Your teleport here request to {0} timed out." },
                { "RequestFromTimedOut", "The teleport request from {0} timed out." },
                { "HereRequestFromTimedOut", "The teleport here request from {0} timed out." },
                { "HasPendingRequest", "{0} already has a pending teleport request." },
                { "RequestFrom", "Request from {0}" },
                { "RequestToAccepted", "Your teleport request to {0} was accepted. Teleporting in {1} seconds." },
                { "RequestFromAccepted", "Teleport request from {0} accepted. Telporting in {1} seconds." },
                { "RequestToDenied", "Your teleport request to {0} was denied." },
                { "HereRequestToDenied", "Your teleport here request to {0} was denied." },
                { "RequestFromDenied", "Teleport request from {0} denied." },
                { "HereRequestFromDenied", "Teleport here request from {0} denied." },
                { "YouTeleportedTo", "You teleported to {0}." },
                { "TeleportedToYou", "{0} teleported to you." },
                { "OnCooldown", "Your teleport is on cooldown for {0} seconds." },
                { "NoPendingRequests", "You don't have any pending requests." },
                { "SyntaxTPR", "Incorrect usage! /tpr {name}" },
                { "PlayerNotFound", "The player \"{0}\" was not found." },
                { "PlayerIDNotFound", "A player with the user ID {0} was not found." },
                { "MultiplePlayersFound", "Multiple players were found with the name {0}." },
                { "CantTeleportToSelf", "You can't teleport to yourself, silly!" },
                { "PlayerIsBuildBlocked", "You may not use teleport whilst building blocked!" },
                { "TargetIsBuildBlocked", "The person you're trying to teleport to is building blocked." },
                { "LocationIsBuildBlocked", "You are building blocked in the location you're trying to teleport to." },
                { "PlayerIsBleeding", "You may not use teleport whilst bleeding." },
                { "CantAffordEconomics", "You can't afford this! Price: ${0}" },
                { "EconomicsYouSpent", "You spent ${0} on this teleport." },
                { "EconomicsRefunded", "You were refunded ${0}." },
                { "CantAffordServerRewards", "You can't afford this! Price: {0}RP" },
                { "ServerRewardsYouSpent", "You spent {0}RP on this teleport." },
                { "ServerRewardsRefunded", "You were refunded {0}RP." },
                { "BlockTPCrafting", "You may not use teleport whilst crafting." },
                { "MaxTeleportsReached", "You have reached your max teleports for today." },
                { "TeleportsRemaining", "{0} teleports remaining today." },
                { "TeleportRequestFromCancelled", "Teleport request from {0} cancelled." },
                { "TeleportRequestToCancelled", "Teleport request to {0} cancelled." },
                { "TeleportRequestCancelled", "Teleport request cancelled." },
                { "TeleportToCancelled", "Teleport to {0} cancelled." },
                { "TeleportFromCancelled", "Teleport from {0} cancelled." },
                { "NoBackLocation", "You have no previous location to return to." },
                { "TeleportedBack", "Teleported back to your previous location." },
                { "SyntaxTPHere", "Incorrect usage! /tphere {name}" },
                { "TPPos-InvalidSyntax", "Incorrect usage! /tp {x} {y} {z}" },
                { "TPToPos", "Teleported to {x}, {y}, {z}" },
                { "NothingToCancel", "You have no teleports to cancel." },
                { "CantTeleportFromZone", "You may not teleport out of a ZoneManager zone." },
                { "CantTeleportToZone", "You may not teleport into a ZoneManager zone." },
                { "CantTPWhilstWounded", "You may not TP whilst wounded." },
                { "IsEscapeBlocked", "You are currently escape blocked and may not teleport." }
            }, this, "en");

            ReadData();

            foreach (string cmdAlias in ConfigFile.TPCommandAliases)
                cmd.AddChatCommand(cmdAlias, this, "tpCommand");

            timer.Once(TimeUntilMidnight(), () => ResetDailyUses());

            if (DebuggingMode) BasePlayer.activePlayerList.ForEach(x => ShowTeleportUI(x));
        }

        void OnServerInitialized()
        {
            Instance = this;

            CooldownManager.Create();

            if (Economics == null && ConfigFile.UseEconomicsPlugin)
            {
                Debug.LogError("[TeleportGUI] Error! Economics is enabled in the config but is not installed! Please install Economics or disable 'UseEconomicsPlugin' in the config!");
            }

            if (ServerRewards == null && ConfigFile.UseServerRewardsPlugin)
            {
                Debug.LogError("[TeleportGUI] Error! ServerRewards is enabled in the config but is not installed! Please install ServerRewards or disable 'UseServerRewardsPlugin' in the config!");
            }

            if (!ConfigFile.UseEconomicsPlugin &&
                !ConfigFile.UseServerRewardsPlugin &&
                ConfigFile.PayAfterUsingDailyLimits)
            {
                Debug.LogError("[TeleportGUI] Error! PayAfterUsingDailyLimits is set in the config, but neither UseEconomicsPlugin or UseServerRewardsPlugin is set! Please fix this error before loading TeleportGUI again. Unloading...");
                Interface.Oxide.UnloadPlugin(this.Title);
                return;
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer)) return;
            BasePlayer player = entity as BasePlayer;
            if (player == null) return;

            if (!HasComponent<Teleporter>(player)) return;
            if (!ConfigFile.CancelTeleportOnDamage) return;

            var teleporter = player.gameObject.GetComponent<Teleporter>();
            teleporter.CancelTeleport();
        }

        void Unload()
        {
            foreach (GameObject go in GameObjects)
            {
                if (go == null) continue;
                if (HasComponent<Teleporter>(go) ||
                    HasComponent<PendingRequest>(go) ||
                    HasComponent<CooldownManager>(go))
                    GameObject.Destroy(go);
            }
            GameObjects.Clear();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CloseUI(player);

            SaveData();
        }

        #endregion

        #region Config

        ConfigData ConfigFile;

        class ConfigData
        {
            public bool PrefixEnabled = true;
            public string PrefixText = "<color=orange>TP: </color>";
            public int DefaultTimeUntilTeleport = 15;
            public Dictionary<string, int> TimeUntilTeleport = new Dictionary<string, int>()
            {
                { "teleportgui.vip", 10 },
                { "teleportgui.elite", 5 },
                { "teleportgui.god", 3 },
                { "teleportgui.none", 0 }
            };
            public List<string> TPCommandAliases = new List<string>() { };
            public double RequestTimeoutTime = 30;
            public double DefaultCooldown = 180;
            public Dictionary<string, double> Cooldowns = new Dictionary<string, double>()
            {
                { "teleportgui.vip", 60 },
                { "teleportgui.elite", 30 },
                { "teleportgui.god", 15 },
                { "teleportgui.none", 0 }
            };
            public int DefaultDailyLimit = 3;
            public Dictionary<string, int> DailyLimit = new Dictionary<string, int>()
            {
                { "teleportgui.vip", 5 },
                { "teleportgui.elite", 8 },
                { "teleportgui.god", 15 },
                { "teleportgui.none", 9999 }
            };
            public bool AdminTPSilent = false;
            public bool AdminTPEnabled = false;
            public bool AllowTeleportWhilstBleeding = false;
            public bool AllowTeleportToBuildBlockedPlayer = false;
            public bool AllowTeleportFromBuildBlock = false;
            public bool UseEconomicsPlugin = false;
            public double EconomicsPrice = 100;
            public bool UseServerRewardsPlugin = false;
            public int ServerRewardsPrice = 10;
            public bool PayAfterUsingDailyLimits = false;
            public bool BlockTPCrafting = true;
            public bool AllowSpecialCharacters = false;
            public bool CancelTeleportOnDamage = true;
            public bool CanTeleportIntoZone = true;
            public bool CanTeleportFromZone = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            ConfigFile = Config.ReadObject<ConfigData>();
            Config.WriteObject(ConfigFile, true);
        }

        protected override void LoadDefaultConfig() => ConfigFile = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(ConfigFile, true);

        #endregion

        #region Commands

        [ChatCommand("tp")]
        void TPCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            if (player.IsAdmin && args.Length > 0)
            {
                if (args.Length < 3)
                {
                    SendReply(player, GetMessage("TPPos-InvalidSyntax"));
                    return;
                }

                float x, y, z;
                if (!float.TryParse(args[0], out x) || !float.TryParse(args[1], out y) || !float.TryParse(args[2], out z))
                {
                    SendReply(player, GetMessage("TPPos-InvalidSyntax"));
                    return;
                }

                Teleport(player, new Vector3(x, y, z));
                SendReply(player, GetMessage("TPToPos")
                                    .Replace("{x}", x.ToString("N1"))
                                    .Replace("{y}", y.ToString("N1"))
                                    .Replace("{z}", z.ToString("N1")));
                return;
            }

            ShowTeleportUI(player);
        }

        [ChatCommand("tpr")]
        void TPRCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, GetMessage("SyntaxTPR"));
                return;
            }

            string name = args[0];
            for (int i = 1; i < args.Length; i++)
                name += " " + args[i];

            List<BasePlayer> matches = FindByNameMulti(name);
            if (matches.Count() == 0)
            {
                SendReply(player, GetMessage("PlayerNotFound").Replace("{0}", name));
                return;
            }
            else if (matches.Count() > 1)
            {
                SendReply(player, GetMessage("MultiplePlayersFound").Replace("{0}", name));
                return;
            }
            BasePlayer targetPlayer = matches.First();

            if (targetPlayer == player && !DebuggingMode)
            {
                SendReply(player, GetMessage("CantTeleportToSelf"));
                return;
            }

            TPR(player, targetPlayer, false);
            return;
        }

        [ChatCommand("tpa")]
        void TPACommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            TeleportRequest tr = player.gameObject.GetComponent<PendingRequest>()?.TeleportRequest;

            if (tr == null)
            {
                SendReply(player, GetMessage("NoPendingRequests"));
                return;
            }

            tr.RequestAccepted();
            CloseUI(player);
        }

        [ChatCommand("tpd")]
        void TPDCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            TeleportRequest tr = player.gameObject.GetComponent<PendingRequest>()?.TeleportRequest;

            if (tr == null)
            {
                SendReply(player, GetMessage("NoPendingRequests"));
                return;
            }

            tr.RequestDeclined();
            CloseUI(player);
        }

        [ChatCommand("tpc")]
        void TPCCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player, permCancel))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            TPC(player);
        }

        [ChatCommand("tpb")]
        void TPBCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player, permBack))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            TPB(player);
        }

        [ChatCommand("tpahere")]
        void TPAHereCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player, permHere))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, GetMessage("SyntaxTPHere"));
                return;
            }

            string targetName = string.Join(" ", args);

            List<BasePlayer> matches = FindByNameMulti(targetName);
            if (matches.Count() == 0)
            {
                SendReply(player, GetMessage("PlayerNotFound").Replace("{0}", targetName));
                return;
            }
            if (matches.Count > 1)
            {
                SendReply(player, GetMessage("MultiplePlayersFound").Replace("{0}", targetName));
                return;
            }
            BasePlayer targetPlayer = matches.First();

            TPHere(player, targetPlayer.userID);
        }

        [ConsoleCommand("tpgui")]
        void TPGuiCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            BasePlayer player = arg.Player();

            string[] args = arg.Args ?? new string[] { };

            #region Check Perm
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }
            #endregion

            #region Open GUI
            if (args.Length == 0)
            {
                ShowTeleportUI(player);
                return;
            }

            if (args[0] == "True") //Because apparently bind b tpgui runs "tpgui True" -.-
            {
                ShowTeleportUI(player);
                return;
            }
            #endregion

            #region Close
            if (args[0] == "close")
            {
                CloseUI(player);
                return;
            }
            #endregion

            #region To
            if (args[0] == "to")
            {
                if (args.Length < 2) return;

                CloseUI(player);

                string userIDString = args[1];
                ulong userID;
                if (!ulong.TryParse(userIDString, out userID)) return;

                var guiSettings = GUIManager.Get(player);

                if (guiSettings.Sleepers)
                {
                    var target = BasePlayer.sleepingPlayerList.FirstOrDefault(x => x.userID == userID);
                    if (target == null)
                    {
                        SendReply(player, GetMessage("PlayerIDNotFound").Replace("{0}", userIDString));
                        return;
                    }

                    SendReply(player, GetMessage("YouTeleportedTo").Replace("{0}", target.displayName));
                    RecordLastTP(player, player.transform.position);
                    Teleport(player, target);
                    return;
                }

                BasePlayer targetPlayer = BasePlayer.FindByID(userID);

                if (targetPlayer == null)
                {
                    SendReply(player, GetMessage("PlayerIDNotFound").Replace("{0}", userIDString));
                    return;
                }

                TPR(player, targetPlayer, false);
                return;
            }
            #endregion

            #region Accept
            if (args[0] == "accept")
            {
                TeleportRequest tr = player.gameObject.GetComponent<PendingRequest>()?.TeleportRequest;
                if (tr == null)
                {
                    SendReply(player, GetMessage("NoPendingRequests"));
                    return;
                }

                tr.RequestAccepted();
                CloseUI(player);
                return;
            }
            #endregion

            #region Decline
            if (args[0] == "decline")
            {
                TeleportRequest tr = player.gameObject.GetComponent<PendingRequest>()?.TeleportRequest;
                if (tr == null)
                {
                    SendReply(player, GetMessage("NoPendingRequests"));
                    return;
                }

                tr.RequestDeclined();
                CloseUI(player);
                return;
            }
            #endregion

            #region Back
            if (args[0] == "back")
            {
                if (!HasPerm(player, permBack))
                {
                    SendReply(player, GetMessage("NoPermission"));
                    return;
                }

                TPB(player);
                CloseUI(player);
                return;
            }
            #endregion

            #region Cancel
            if (args[0] == "cancel")
            {
                if (!HasPerm(player, permCancel))
                {
                    SendReply(player, GetMessage("NoPermission"));
                    return;
                }

                TPC(player);
                CloseUI(player);
                return;
            }
            #endregion

            #region Here TP
            if (args[0] == "heretp")
            {
                if (!HasPerm(player, permHere))
                {
                    SendReply(player, GetMessage("NoPermission"));
                    return;
                }

                if (args.Length < 2) return;

                string userIDString = args[1];
                ulong userID;
                if (!ulong.TryParse(userIDString, out userID)) return;

                TPHere(player, userID);
                return;
            }
            #endregion

            #region Set
            if (args[0] == "set")
            {
                if (args.Length < 3) return;

                switch (args[1])
                {
                    case "page":
                        int page;
                        if (!Int32.TryParse(args[2], out page)) return;
                        GUIManager.Get(player).Page = page;
                        UIChooser(player);
                        break;

                    case "tphere":
                        if (!HasPerm(player, permHere))
                        {
                            SendReply(player, GetMessage("NoPermission"));
                            return;
                        }
                        bool here = args[2] == bool.TrueString;
                        GUIManager.Get(player).TPHere = here;
                        UIChooser(player);
                        break;

                    case "sleepers":
                        if (!HasPerm(player, permSleepers))
                        {
                            SendReply(player, GetMessage("NoPermission"));
                            return;
                        }
                        bool sleepers = args[2] == bool.TrueString;
                        GUIManager.Get(player).Sleepers = sleepers;
                        UIChooser(player);
                        break;
                }
                return;
            }
            #endregion
        }

        [ConsoleCommand("tpgui.resetdatafile")]
        void ResetAllCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;

            if (!DebuggingMode)
            {
                Debug.LogError("[TeleportGUI] You may not use this command. Warning: It is highly untested and unsafe. Please to not bypass this warning.");
                return;
            }

            storedData.Cooldowns.Clear();
            storedData.UsesToday.Clear();
            SaveData();
            Puts("Cleared data file and saved.");
        }

        #endregion

        #region GUIs

        void ShowTeleportUI(BasePlayer player)
        {
            if (!GUIOpen.ContainsKey(player))
                GUIOpen.Add(player, false);
            if (GUIOpen[player])
            {
                CloseUI(player);
                GUIOpen[player] = false;
                return;
            }
            GUIOpen[player] = true;

            UIChooser(player);
        }

        void UIChooser(BasePlayer player)
        {
            var guiSettings = GUIManager.Get(player);

            if (BasePlayer.activePlayerList.Count > 25 || guiSettings.Sleepers)
            {
                BigUI(player);
                return;
            }

            SmallUI(player);
        }

        void CloseUI(BasePlayer player)
        {
            if (!GUIOpen.ContainsKey(player))
                GUIOpen.Add(player, false);
            GUIOpen[player] = false;
            CuiHelper.DestroyUi(player, "smallTeleportGUI");
            CuiHelper.DestroyUi(player, "bigTeleportGUI");
        }

        void SmallUI(BasePlayer player)
        {
            var GUIElement = new CuiElementContainer();

            var guiSettings = GUIManager.Get(player);

            List<BasePlayer> players = !guiSettings.Sleepers ?
                                       BasePlayer.activePlayerList :
                                       BasePlayer.sleepingPlayerList;
            players = new List<BasePlayer>(players);

            if (!DebuggingMode && players.Contains(player))
            {
                players.Remove(player);
            }

            //if (debuggingMode) players.AddRange(SpareNames());

            players = players.OrderBy(x => x.displayName).ToList();

            #region Whole Panel

            var smallTeleportGUI = GUIElement.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.75"
                },
                RectTransform =
                {
                    AnchorMin = "0.3 0.3", //Left Bottom
                    AnchorMax = "0.7 0.75" // Right Top
                },
                CursorEnabled = true
            }, "Hud", "smallTeleportGUI");

            #endregion

            #region Title Bar

            var titleBar = GUIElement.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.75"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.9", //Left Bottom
                    AnchorMax = "0.998 0.999" // Right Top
                }
            }, smallTeleportGUI);

            #region Pending Request Buttons

            bool pendingRequest = (player.gameObject.GetComponent<PendingRequest>() != null);
            string requestFrom = player.gameObject.GetComponent<PendingRequest>()?.From.displayName;

            if (pendingRequest)
            {
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0.12 0.96"
                    },
                    Button =
                    {
                        Command = "tpgui accept",
                        Color = "0 1 0 1",
                    },
                    Text =
                    {
                        Text = "Accept",
                        FontSize = 18,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    }
                }, titleBar);

                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.122 0",
                        AnchorMax = "0.24 0.96"
                    },
                    Button =
                    {
                        Command = "tpgui decline",
                        Color = "1 0 0 1",
                    },
                    Text =
                    {
                        Text = "Decline",
                        FontSize = 18,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    }
                }, titleBar);

                GUIElement.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("RequestFrom", this).Replace("{0}", requestFrom),
                        FontSize = 16,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.26 0",
                        AnchorMax = "1 1"
                    }
                }, titleBar);
            }

            #endregion

            #region Title

            if (!pendingRequest)
            {
                GUIElement.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("TeleportTitle", this),
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }, titleBar);
            }

            #endregion

            #region Sleepers
            if (HasPerm(player, permSleepers))
            {
                var sleepers = GUIManager.Get(player).Sleepers;
                string colour = (sleepers) ? "1 0.2 0.2 1" : "0.5 0.5 0.5 1";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.61 0",
                        AnchorMax = "0.71 0.97"
                    },
                    Text =
                    {
                        Text = "Sleepers",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = $"tpgui set sleepers {!sleepers}",
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region TPHere
            if (HasPerm(player, permHere))
            {
                var tpHere = GUIManager.Get(player).TPHere;
                string colour = (tpHere) ? "0 1 0 1" : "0.5 0.5 0.5 1";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.71 0",
                        AnchorMax = "0.78 0.97"
                    },
                    Text =
                    {
                        Text = "Here",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = $"tpgui set tphere {!tpHere}",
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region TPBack
            if (HasPerm(player, permBack))
            {
                var colour = (LastTeleport.ContainsKey(player)) ? "0.15 0.15 1 1" : "0.5 0.5 0.5 1";
                var command = (LastTeleport.ContainsKey(player)) ? "tpgui back" : "";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.78 0",
                        AnchorMax = "0.85 0.97"
                    },
                    Text =
                    {
                        Text = "Back",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = command,
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region TPCancel
            if (HasPerm(player, permCancel))
            {
                var colour = (HasPendingTeleport(player)) ? "1 0.5 0 1" : "0.5 0.5 0.5 1";
                var command = (HasPendingTeleport(player)) ? "tpgui cancel" : "";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.85 0",
                        AnchorMax = "0.934 0.97"
                    },
                    Text =
                    {
                        Text = "Cancel",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = command,
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region Close Button

            GUIElement.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.935 0",
                    AnchorMax = "0.998 0.97"
                },
                Button =
                {
                    Command = "tpgui close",
                    Color = "1 0 0 1"
                },
                Text =
                {
                    Text = "X",
                    FontSize = 20,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, titleBar);

            #endregion

            #endregion

            #region Player List

            var playerList = GUIElement.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.9"
                }
            }, smallTeleportGUI);

            const float columnWidth = 0.2f;
            const float rowWidth = 0.2f;

            int playerCount = 0;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    if (players.Count <= playerCount) continue;

                    BasePlayer target = players[playerCount];

                    var panel = GUIElement.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = (columnWidth * j).ToString() + " " + (1f - (rowWidth * i) - rowWidth).ToString(),
                            AnchorMax = ((columnWidth * j) + columnWidth).ToString() + " " + (1f - (rowWidth * i)).ToString()
                        },
                        Image =
                        {
                            Color = "0 0 0 0"
                        }
                    }, playerList);

                    GUIElement.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        Text =
                        {
                            Text = CleanText(target.displayName),
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 18,
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf"
                        },
                        Button =
                        {
                            Command = $"tpgui to {target.userID}",
                            Color = "0 0 0 0"
                        }
                    }, panel);

                    if (GUIManager.Get(player).TPHere)
                    {
                        GUIElement.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.7 0.005",
                                AnchorMax = "0.97 0.2"
                            },
                            Text =
                            {
                                Text = "Here",
                                Color = "1 1 1 1",
                                Align = TextAnchor.MiddleCenter,
                                FontSize = 12
                            },
                            Button =
                            {
                                Command = $"tpgui heretp {target.userID}",
                                Color = "0 1 0 0.5"
                            }
                        }, panel);
                    }

                    playerCount++;
                }
            }

            #endregion

            #region Empty List

            if (players.Count == 0)
            {
                GUIElement.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Looks like you're a lone survivor!",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.1",
                        AnchorMax = "1 1"
                    }
                }, playerList);
            }

            #endregion

            CuiHelper.DestroyUi(player, "smallTeleportGUI");
            CuiHelper.DestroyUi(player, "bigTeleportGUI");
            CuiHelper.AddUi(player, GUIElement);
        }

        void BigUI(BasePlayer player)
        {
            var GUIElement = new CuiElementContainer();

            var guiSettings = GUIManager.Get(player);

            List<BasePlayer> players = !guiSettings.Sleepers ?
                                       BasePlayer.activePlayerList :
                                       BasePlayer.sleepingPlayerList;
            players = new List<BasePlayer>(players);

            if (!DebuggingMode && players.Contains(player))
            {
                players.Remove(player);
            }

            //if (debuggingMode) players.AddRange(SpareNames());

            players = players.OrderBy(x => x.displayName).ToList();

            int maxPages = CalculatePages(players.Count);

            #region Whole Panel

            var bigTeleportGUI = GUIElement.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.75"
                },
                RectTransform =
                {
                    AnchorMin = "0.2 0.125", //Left Bottom
                    AnchorMax = "0.8 0.9" // Right Top
                },
                CursorEnabled = true
            }, "Hud", "bigTeleportGUI");

            #endregion

            #region Title Bar

            var titleBar = GUIElement.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.75"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.935", //Left Bottom
                    AnchorMax = "0.997 1.0" // Right Top
                }
            }, bigTeleportGUI);

            #region Pending Request Buttons

            bool pendingRequest = (player.gameObject.GetComponent<PendingRequest>() != null);
            string requestFrom = player.gameObject.GetComponent<PendingRequest>()?.From.displayName;

            if (pendingRequest)
            {
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0.01",
                        AnchorMax = "0.16 0.98"
                    },
                    Button =
                    {
                        Command = "tpgui accept",
                        Color = "0 1 0 1",
                    },
                    Text =
                    {
                        Text = "Accept",
                        FontSize = 18,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    }
                }, titleBar);

                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.1615 0.01",
                        AnchorMax = "0.32 0.98"
                    },
                    Button =
                    {
                        Command = "tpgui decline",
                        Color = "1 0 0 1",
                    },
                    Text =
                    {
                        Text = "Decline",
                        FontSize = 18,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    }
                }, titleBar);

                GUIElement.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("RequestFrom", this).Replace("{0}", requestFrom),
                        FontSize = 18,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.34 0",
                        AnchorMax = "1 1"
                    }
                }, titleBar);
            }

            #endregion

            #region Title

            if (!pendingRequest)
            {
                string pageNum = (maxPages > 1) ? $" - {GUIManager.Get(player).Page}" : "";
                GUIElement.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("TeleportTitle", this) + pageNum,
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }, titleBar);
            }

            #endregion

            #region Sleepers
            if (HasPerm(player, permSleepers))
            {
                var sleepers = GUIManager.Get(player).Sleepers;
                string colour = (sleepers) ? "1 0.2 0.2 1" : "0.5 0.5 0.5 1";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.62 0",
                        AnchorMax = "0.70 0.97"
                    },
                    Text =
                    {
                        Text = "Sleepers",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = $"tpgui set sleepers {!sleepers}",
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region TPHere
            if (HasPerm(player, permHere))
            {
                var tpHere = GUIManager.Get(player).TPHere;
                string colour = (tpHere) ? "0 1 0 1" : "0.5 0.5 0.5 1";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.70 0",
                        AnchorMax = "0.78 0.97"
                    },
                    Text =
                    {
                        Text = "Here",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = $"tpgui set tphere {!tpHere}",
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region TPBack
            if (HasPerm(player, permBack))
            {
                var colour = (LastTeleport.ContainsKey(player)) ? "0.15 0.15 1 1" : "0.5 0.5 0.5 1";
                var command = (LastTeleport.ContainsKey(player)) ? "tpgui back" : "";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.78 0",
                        AnchorMax = "0.86 0.97"
                    },
                    Text =
                    {
                        Text = "Back",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    Button =
                    {
                        Command = command,
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region TPCancel
            if (HasPerm(player, permCancel))
            {
                var colour = (HasPendingTeleport(player)) ? "1 0.5 0 1" : "0.5 0.5 0.5 1";
                var command = (HasPendingTeleport(player)) ? "tpgui cancel" : "";
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.86 0",
                        AnchorMax = "0.9385 0.98"
                    },
                    Text =
                    {
                        Text = "Cancel",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 16
                    },
                    Button =
                    {
                        Command = command,
                        Color = colour
                    }
                }, titleBar);
            }
            #endregion

            #region Close Button

            GUIElement.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.94 0.01",
                    AnchorMax = "1.0 0.98"
                },
                Button =
                {
                    Command = "tpgui close",
                    Color = "1 0 0 1"
                },
                Text =
                {
                    Text = "X",
                    FontSize = 20,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, titleBar);

            #endregion

            #endregion

            #region Player List

            var playerList = GUIElement.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.9"
                }
            }, bigTeleportGUI);

            var page = GUIManager.Get(player).Page;
            int playerCount = (page * 100) - 100;
            for (int j = 0; j < 20; j++)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (players.Count <= playerCount) continue;

                    BasePlayer target = players[playerCount];

                    var panel = GUIElement.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = (0.2f * i).ToString() + " " + (1f - (0.05f * j) - 0.05f).ToString(),
                            AnchorMax = ((0.2f * i) + 0.2f).ToString() + " " + (1f - (0.05f * j)).ToString()
                        },
                        Image =
                        {
                            Color = "0 1 0 0"
                        }
                    }, playerList);

                    GUIElement.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        Text =
                        {
                            Text = CleanText(target.displayName),
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 18,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Command = $"tpgui to {target.userID}",
                            Color = "0 0 0 0"
                        }
                    }, panel);

                    if (GUIManager.Get(player).TPHere)
                    {
                        GUIElement.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.85 0",
                                AnchorMax = "0.98 0.5"
                            },
                            Text =
                            {
                                Text = "Here",
                                FontSize = 8,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            },
                            Button =
                            {
                                Command = $"tpgui heretp {target.userID}",
                                Color = "0 1 0 1"
                            }
                        }, panel);
                    }

                    playerCount++;
                }
            }

            #endregion

            #region Empty List

            if (players.Count == 0)
            {
                GUIElement.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Looks like you're a lone survivor!",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.1",
                        AnchorMax = "1 1"
                    }
                }, playerList);
            }

            #endregion

            #region Page Buttons

            if (page < maxPages)
            {
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1.025 0.575",
                        AnchorMax = "1.1 0.675"
                    },
                    Text =
                    {
                        Text = "Up",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 16,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Command = $"tpgui set page {(page + 1).ToString()}",
                        Color = "0 0 0 0.75"
                    }
                }, bigTeleportGUI);
            }

            if (page > 1)
            {
                GUIElement.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1.025 0.45",
                        AnchorMax = "1.1 0.55"
                    },
                    Text =
                    {
                        Text = "Down",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 16,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Command = $"tpgui set page {(page - 1).ToString()}",
                        Color = "0 0 0 0.75"
                    }
                }, bigTeleportGUI);
            }

            #endregion

            CuiHelper.DestroyUi(player, "smallTeleportGUI");
            CuiHelper.DestroyUi(player, "bigTeleportGUI");
            CuiHelper.AddUi(player, GUIElement);
        }

        #endregion

        #region TP Functions

        void TPR(BasePlayer player, BasePlayer targetPlayer, bool tpHere)
        {
            bool playerIsPaying = false;

            if (player.IsAdmin && ConfigFile.AdminTPEnabled)
            {
                if (!ConfigFile.AdminTPSilent)
                {
                    SendReply(player, GetMessage("TeleportedToYou").Replace("{0}", player.displayName));
                }

                SendReply(player, GetMessage("YouTeleportedTo").Replace("{0}", targetPlayer.displayName));

                Vector3 currentPos = player.transform.position;
                RecordLastTP(player, currentPos);

                Teleport(player, targetPlayer);
                return;
            }

            if (storedData.Cooldowns.ContainsKey(player.userID))
            {
                SendReply(player, GetMessage("OnCooldown").Replace("{0}", storedData.Cooldowns[player.userID].ToString()));
                return;
            }

            if (player.IsWounded())
            {
                SendReply(player, GetMessage("CantTPWhilstWounded"));
                return;
            }

            if (!ConfigFile.AllowTeleportWhilstBleeding)
            {
                if (player.metabolism.bleeding.value > 0f)
                {
                    SendReply(player, GetMessage("PlayerIsBleeding"));
                    return;
                }
            }

            if (!ConfigFile.AllowTeleportFromBuildBlock)
            {
                if (!player.CanBuild())
                {
                    SendReply(player, GetMessage("PlayerIsBuildBlocked"));
                    return;
                }
            }

            if (!ConfigFile.AllowTeleportToBuildBlockedPlayer)
            {
                if (!targetPlayer.CanBuild())
                {
                    SendReply(player, GetMessage("TargetIsBuildBlocked"));
                    return;
                }
            }

            if (IsCrafting(player))
            {
                SendReply(player, GetMessage("BlockTPCrafting"));
                return;
            }

            if (HasComponent<PendingRequest>(targetPlayer))
            {
                SendReply(player, GetMessage("HasPendingRequest").Replace("{0}", targetPlayer.displayName));
                return;
            }

            if (ConfigFile.UseEconomicsPlugin &&
                !ConfigFile.PayAfterUsingDailyLimits)
            {
                if (EconomicsInstalled())
                {
                    if (!PayEconomics(player))
                    {
                        SendReply(player, GetMessage("CantAffordEconomics").Replace("{0}", ConfigFile.EconomicsPrice.ToString("N2")));
                        return;
                    }

                    SendReply(player, GetMessage("EconomicsYouSpent").Replace("{0}", ConfigFile.EconomicsPrice.ToString("N2")));
                    playerIsPaying = true;
                }
            }

            if (ConfigFile.UseServerRewardsPlugin &&
                !ConfigFile.PayAfterUsingDailyLimits)
            {
                if (ServerRewardsInstalled())
                {
                    if (!PayServerRewards(player))
                    {
                        SendReply(player, GetMessage("CantAffordServerRewards").Replace("{0}", ConfigFile.ServerRewardsPrice.ToString("N1")));
                        return;
                    }

                    SendReply(player, GetMessage("ServerRewardsYouSpent").Replace("{0}", ConfigFile.ServerRewardsPrice.ToString("N1")));
                    playerIsPaying = true;
                }
            }

            if (HasReachedDailyLimit(player))
            {
                if (!ConfigFile.PayAfterUsingDailyLimits)
                {
                    SendReply(player, GetMessage("MaxTeleportsReached"));
                    return;
                }

                if (ConfigFile.UseEconomicsPlugin)
                {
                    if (!PayEconomics(player))
                    {
                        SendReply(player, GetMessage("CantAffordEconomics").Replace("{0}", ConfigFile.EconomicsPrice.ToString("N1")));
                        return;
                    }

                    SendReply(player, GetMessage("EconomicsYouSpent").Replace("{0}", ConfigFile.EconomicsPrice.ToString("N1")));
                    playerIsPaying = true;
                }

                if (ConfigFile.UseServerRewardsPlugin)
                {
                    if (!PayServerRewards(player))
                    {
                        SendReply(player, GetMessage("CantAffordServerRewards").Replace("{0}", ConfigFile.ServerRewardsPrice.ToString("N1")));
                        return;
                    }

                    SendReply(player, GetMessage("ServerRewardsYouSpent").Replace("{0}", ConfigFile.ServerRewardsPrice.ToString("N1")));
                    playerIsPaying = true;
                }
            }

            string canTeleport = Interface.Oxide.CallHook("CanTeleport", player) as string;
            if (canTeleport != null)
            {
                SendReply(player, canTeleport);
                return;
            }

            bool escapeBlocked = NoEscape?.Call<bool>("IsBlocked", player) ?? false;
            if (escapeBlocked)
            {
                SendReply(player, GetMessage("IsEscapeBlocked"));
                return;
            }

            if (ZoneManager != null)
            {
                if (!ConfigFile.CanTeleportFromZone)
                {
                    var call = ZoneManager.Call("EntityHasFlag", player, "notp");
                    if (call is bool && (bool)call)
                    {
                        SendReply(player, GetMessage("CantTeleportFromZone"));
                        return;
                    }
                }

                if (!ConfigFile.CanTeleportIntoZone)
                {
                    var call = ZoneManager.Call("EntityHasFlag", targetPlayer, "notp");
                    if (call is bool && (bool)call)
                    {
                        SendReply(player, GetMessage("CantTeleportToZone"));
                        return;
                    }
                }
            }

            TeleportRequest.Create(
                player,
                targetPlayer,
                ConfigFile.RequestTimeoutTime,
                tpHere,
                playerIsPaying);
        }

        void TPC(BasePlayer player)
        {
            if (HasComponent<Teleporter>(player))
            {
                var teleporter = player.GetComponent<Teleporter>();
                teleporter.CancelTeleport();
                return;
            }

            var call = Interface.Oxide.CallHook("CancelAllTeleports", player);
            if (call is string)
            {
                SendReply(player, call as string);
                return;
            }

            SendReply(player, GetMessage("NothingToCancel"));
            return;
        }

        void TPB(BasePlayer player)
        {
            if (!LastTeleport.ContainsKey(player))
            {
                SendReply(player, GetMessage("NoBackLocation"));
                return;
            }

            Teleport(player, LastTeleport[player]);
            SendReply(player, GetMessage("TeleportedBack"));
        }

        void TPHere(BasePlayer player, ulong targetID)
        {
            if (!HasPerm(player, permHere))
            {
                SendReply(player, GetMessage("NoPermission"));
                return;
            }

            BasePlayer target = BasePlayer.FindByID(targetID) ??
                                BasePlayer.sleepingPlayerList.FirstOrDefault(x => x.userID == targetID);

            if (target == null)
            {
                SendReply(player, GetMessage("PlayerIDNotFound").Replace("{0}", targetID.ToString()));
                return;
            }

            if (!DebuggingMode && player == target)
            {
                SendReply(player, GetMessage("CantTeleportToSelf"));
                return;
            }

            TPR(target, player, true);
        }

        void Teleport(BasePlayer player, BasePlayer target) => Teleport(player, target.transform.position);

        void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(position);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }

        void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping()) return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        #endregion

        #region Functions

        void RecordLastTP(BasePlayer player, Vector3 oldPos)
        {
            if (!LastTeleport.ContainsKey(player))
                LastTeleport.Add(player, Vector3.zero);
            LastTeleport[player] = oldPos;
        }

        bool HasPendingTeleport(BasePlayer player) => HasComponent<PendingRequest>(player) || HasComponent<Teleporter>(player);

        bool HasReachedDailyLimit(BasePlayer player)
        {
            int maxTeleports = ConfigFile.DefaultDailyLimit;

            if (maxTeleports <= 0) return false;

            var highestMaxTeleports = ConfigFile.DailyLimit
                .Where(x => permission.UserHasPermission(player.UserIDString, x.Key) &&
                            x.Value > maxTeleports)
                .OrderByDescending(x => x.Value)
                .FirstOrDefault();

            if (!highestMaxTeleports.Equals(default(KeyValuePair<string, int>)))
            {
                maxTeleports = highestMaxTeleports.Value;
            }

            if (!storedData.UsesToday.ContainsKey(player.userID))
            {
                storedData.UsesToday.Add(player.userID, 0);
            }

            return storedData.UsesToday[player.userID] >= maxTeleports;
        }

        int IncrementUses(BasePlayer player)
        {
            if (!storedData.UsesToday.ContainsKey(player.userID))
            {
                storedData.UsesToday.Add(player.userID, 0);
            }

            storedData.UsesToday[player.userID]++;
            SaveData();

            int maxTeleports = ConfigFile.DefaultDailyLimit;

            var highestMaxTeleports = ConfigFile.DailyLimit
                .Where(x => permission.UserHasPermission(player.UserIDString, x.Key) &&
                            x.Value > maxTeleports)
                .OrderByDescending(x => x.Value)
                .FirstOrDefault();

            if (!highestMaxTeleports.Equals(default(KeyValuePair<string, int>)))
            {
                maxTeleports = highestMaxTeleports.Value;
            }

            return maxTeleports - storedData.UsesToday[player.userID];
        }

        void ResetDailyUses()
        {
            storedData.UsesToday.Clear();
            SaveData();
            timer.Once(TimeUntilMidnight(), () => ResetDailyUses());
        }

        bool IsCrafting(BasePlayer player) => (player.inventory.crafting.queue.Count() > 0);

        int CalculatePages(int value) => (int)Math.Ceiling(value / 100d);

        BasePlayer FindByName(string name, bool sleepers = false)
        {
            List<BasePlayer> players = new List<BasePlayer>();
            if (sleepers) players = new List<BasePlayer>(BasePlayer.sleepingPlayerList);
            else players = new List<BasePlayer>(BasePlayer.activePlayerList);
            return players.Where(x => x.displayName.ToLower().Replace(" ", "")
                                       .Contains(name.ToLower().Replace(" ", "")))
                                       .FirstOrDefault();
        }

        List<BasePlayer> FindByNameMulti(string name, bool sleepers = false)
        {
            List<BasePlayer> players = new List<BasePlayer>();
            if (sleepers) players = new List<BasePlayer>(BasePlayer.sleepingPlayerList);
            else players = new List<BasePlayer>(BasePlayer.activePlayerList);
            return players.Where(x => x.displayName.ToLower().Replace(" ", "")
                                       .Contains(name.ToLower().Replace(" ", "")))
                                       .ToList();
        }

        #region Economics/ServerRewards

        bool EconomicsInstalled() => Economics != null;

        bool ServerRewardsInstalled() => ServerRewards != null;

        bool PayEconomics(BasePlayer player)
        {
            double price = ConfigFile.EconomicsPrice;
            double playerMoney = (double)Economics.Call("Balance", player.userID);

            if (playerMoney - price >= 0)
            {
                Economics?.Call("Withdraw", player.userID, price);
                return true;
            }
            return false;
        }

        bool PayServerRewards(BasePlayer player)
        {
            int price = ConfigFile.ServerRewardsPrice;
            int currentPoints;
            var call = ServerRewards?.Call("CheckPoints", player.userID);
            if (call == null) currentPoints = 0;
            else currentPoints = (int)call;

            if (currentPoints - price >= 0)
            {
                ServerRewards.Call("TakePoints", player.userID, price);
                return true;
            }
            return false;
        }

        void RefundPlayerEconomics(BasePlayer player)
        {
            double price = ConfigFile.EconomicsPrice;
            Economics?.Call("Deposit", player.userID, price);
            SendReply(player, GetMessage("EconomicsRefunded").Replace("{0}", price.ToString("N2")));
        }

        void RefundServerRewards(BasePlayer player)
        {
            int price = ConfigFile.ServerRewardsPrice;
            ServerRewards.Call("AddPoints", player.userID, price);
            SendReply(player, GetMessage("ServerRewardsRefunded").Replace("{0}", price.ToString("N1")));
        }

        #endregion

        #endregion

        #region Helpers

        bool HasPerm(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permUse) || player.IsAdmin);
        bool HasPerm(BasePlayer player, string perm) => (permission.UserHasPermission(player.UserIDString, perm) || player.IsAdmin);

        string CleanText(string text) => ConfigFile.AllowSpecialCharacters ? text : new Regex(@"[^A-Za-z0-9\/:*?<>|!@#$%^&()\[\] ]+").Replace(text, " ");

        int TimeUntilMidnight() => ((59 - DateTime.Now.Second) + ((59 - DateTime.Now.Minute) * 60) + ((23 - DateTime.Now.Hour) * 3600));

        string GetMessage(string key) => (ConfigFile.PrefixEnabled ? ConfigFile.PrefixText : string.Empty) + lang.GetMessage(key, this);

        bool HasComponent<T>(GameObject go) => (go.GetComponent<T>() != null);
        bool HasComponent<T>(BasePlayer player) => (player.GetComponent<T>() != null);

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, storedData);
        void ReadData() => storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Title);

        #endregion

        #region Spare Names (For Testing)

        List<BasePlayer> SpareNames() => nameList.Select(x => new BasePlayer() { displayName = x, userID = 1 }).ToList();

        static string names = @"AaronLongjohnsonson John Thomas George James Henry Charles Joseph Frederick Robert Alfred Edward Arthur Richard Samuel Walter David Harry Albert Edwin Francis Frank Benjamin Herbert Daniel Tom Isaac Fred Peter Ernest Michael Stephen Patrick Matthew Edmund Frederic Alexander Philip Mark Evan Andrew Abraham Hugh Christopher Sidney Lewis Jonathan Jesse Ralph Joshua Sam Martin Owen Josiah Jacob Reuben Joe Leonard Edgar Eli Enoch Job Oliver Anthony Amos Horace Elijah Timothy Cornelius Moses Jeremiah Sydney Louis Nicholas Aaron Percy Ebenezer Willie Luke Dennis Jabez Levi Augustus Adam Nathaniel Harold Allen Griffith Bernard Rowland Ben Ellis Rees Archibald Ambrose Lawrence Morgan Noah Simon Ephraim Caleb Elias Reginald Roger Isaiah Phillip Jonas Nathan Clement Solomon Morris Charley Emanuel Gilbert Paul Hubert Maurice Simeon Abel Wilfred Dan Emmanuel Jim Ezra Squire Theodore Seth Horatio Wright Theophilus Vincent Wilson Alan Stanley Sampson Miles Wallace Israel Smith Humphrey Hiram Howard Cecil Felix Allan Oswald Silas Austin Nelson Douglas Hedley Enos Eugene Percival Spencer Edmond Septimus Robinson Luther Joel Adolphus Cuthbert Donald Bartholomew Elisha Uriah Laurence Johnson Lionel Clarence Llewellyn Oscar Norman Dick Charlie Godfrey Herman Colin Harvey Walker Denis Claude Zachariah Hezekiah Roland Llewelyn Harrison Julius Duncan Victor Jasper Jackson Lancelot Giles Jenkin Hartley Gerald Valentine Clifford Thompson Charlie Godfrey Herman Colin Harvey Walker Denis Claude Zachariah Hezekiah Roland Llewelyn Harrison Julius Duncan Victor Jasper Jackson Lancelot Giles Jenkin Hartley Gerald Valentine Clifford Thompson";

        string[] nameList = names.Split(' ');

        #endregion
    }
}