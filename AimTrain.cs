using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections;
using Oxide.Game.Rust.Cui;
using VLB;
using UI = Oxide.Plugins.AimTrainUI.UIMethods;
using Anchor = Oxide.Plugins.AimTrainUI.Anchor;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("AimTrain", "https://discord.gg/dNGbxafuJn", "1.3.2")]
    public class AimTrain : RustPlugin
    {
        #region Declaration

        [PluginReference]
        // ReSharper disable InconsistentNaming
        private Plugin Kits,
            NoEscape;
        // ReSharper restore InconsistentNaming

        private Configuration _config;
        private StoredData _storedData;
        private Dictionary<string, Arena> _arenasCache = new Dictionary<string, Arena>();
        private Dictionary<ulong, PlayerData> _playersCache = new Dictionary<ulong, PlayerData>();
        private Dictionary<ulong, string> _editArena = new Dictionary<ulong, string>();
        private List<ulong> _noAmmo = new List<ulong>();
        private static AimTrain _instance;
        private string _mainContainer = "Main.Container";
        private string _statsContainer = "Stats.Container";
        private CuiElementContainer _cachedContainer;
        private string _cachedContainerJson;
        private object _falseObject = false;

        private HashSet<Bot> _spawnedBots = new HashSet<Bot>();
        private HashSet<NetworkableId> _spawnedBotsNetIds = new HashSet<NetworkableId>();

        #endregion

        #region Hooks

        private void Init()
        {
            DeleteAll<Bot>();
            _instance = this;
            LoadConfig();

            if( _config.EnableUI )
            {
                ConstructUi();
            }

            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>( Name );
            _arenasCache = _storedData.Arenas;
            permission.RegisterPermission( "aimtrain.join", this );
            permission.RegisterPermission( "aimtrain.admin", this );
            if( _config.ArenaPermission )
            {
                foreach( string arena in _arenasCache.Keys )
                {
                    permission.RegisterPermission( $"aimtrain.{arena}", this );
                    Puts( "Added extra permission: " + $"aimtrain.{arena}" );
                }
            }

            if( _config.EnableUI )
            {
                timer.Repeat( 2f, 0, () =>
                {
                    foreach( var player in BasePlayer.activePlayerList )
                    {
                        if( _playersCache.ContainsKey( player.userID ) )
                        {
                            UpdateTimer( player );
                        }
                    }
                } );
            }
        }

        private object CanTrade( BasePlayer player )
        {
            return _playersCache.ContainsKey( player.userID ) ? Lang( "CantWhileAimTrain" ) : null;
        }

        private object CanOpenBackpack( BasePlayer player, ulong backpackOwnerID )
        {
            if( _playersCache.ContainsKey( player.userID ) || _playersCache.ContainsKey( backpackOwnerID ) )
            {
                return Lang( "CantWhileAimTrain" );
            }

            return null;
        }

        private object CanTeleport( BasePlayer player )
        {
            return _playersCache.ContainsKey( player.userID ) ? Lang( "CantWhileAimTrain" ) : null;
        }

        private object CanBank( BasePlayer player )
        {
            return _playersCache.ContainsKey( player.userID ) ? Lang( "CantWhileAimTrain" ) : null;
        }

        private void OnPlayerAttack( BasePlayer attacker, HitInfo info )
        {
            if( _playersCache.ContainsKey( attacker.userID ) && info.HitEntity is BasePlayer )
            {
                _playersCache[attacker.userID].Hits++;
                if( info.isHeadshot )
                {
                    _playersCache[attacker.userID].Headshots++;
                }
            }
        }

        private void OnWeaponFired( BaseProjectile projectile, BasePlayer player )
        {
            bool inCache = false;
            if( _playersCache.ContainsKey( player.userID ) && _config.EnableUI )
            {
                inCache = true;
                _playersCache[player.userID].Bullets++;
            }

            if( !inCache || _noAmmo.Contains( player.userID ) )
            {
                return;
            }

            projectile.GetItem().condition = projectile.GetItem().info.condition.max;
            projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
            projectile.SendNetworkUpdateImmediate();
        }

        private void OnItemDropped( Item item, BaseEntity entity )
        {
            var player = item?.GetOwnerPlayer();
            if( player == null )
            {
                return;
            }

            if( _playersCache.ContainsKey( player.userID ) )
            {
                item.Remove();
            }
        }

        private object OnPlayerCommand( BasePlayer player, string command, string[] args )
        {
            if( !_playersCache.ContainsKey( player.userID ) )
            {
                return null;
            }

            string cmdLower = command.ToLower();
            foreach( var blacklistedCommand in _config.BlacklistedCommands )
            {
                if( cmdLower == blacklistedCommand )
                {
                    SendReply( player, Lang( "CantWhileAimTrain" ) );
                    return _falseObject;
                }

                if( cmdLower.StartsWith( blacklistedCommand ) )
                {
                    SendReply( player, Lang( "CantWhileAimTrain" ) );
                    return _falseObject;
                }
            }

            return null;
        }

        private void OnPlayerDisconnected( BasePlayer player, string reason )
        {
            if( _playersCache.ContainsKey( player.userID ) )
            {
                LeaveAimTrain( player );
            }
        }

        private object OnPlayerDeath( BasePlayer player, HitInfo info )
        {
            if( _playersCache.ContainsKey( player.userID ) )
            {
                LeaveAimTrain( player );
            }

            if( !_spawnedBotsNetIds.Contains( player.net.ID ) )
            {
                return null;
            }

            player.health = 100;
            var botPlayer = player.gameObject.GetComponent<Bot>();

            var arena = _arenasCache[botPlayer.CurrentArena];
            if( arena.BotMoving )
            {
                int spawnPosition = UnityEngine.Random.Range( 1, _arenasCache[botPlayer.CurrentArena].SpawnsBot.Keys.Count );
                botPlayer.IsLerping = false;
                StripPlayer( player );
                player.Teleport( _arenasCache[botPlayer.CurrentArena].SpawnsBot[spawnPosition].ToVector3() );
                Kits?.Call( "GiveKit", player, _arenasCache[botPlayer.CurrentArena].Kits.GetRandom() );

                int random = UnityEngine.Random.Range( 0, 2 );
                if( random.Equals( 1 ) )
                {
                    botPlayer.minSpeed = 5.5f;
                    botPlayer.maxSpeed = 5.5f;
                }
                else
                {
                    botPlayer.minSpeed = 2.4f;
                    botPlayer.maxSpeed = 2.4f;
                }
            }

            return _falseObject;
        }

        private object CanBeWounded( BasePlayer player, HitInfo info )
        {
            return _spawnedBotsNetIds.Contains( player.net.ID ) ? _falseObject : null;
        }

        private void OnServerSave()
        {
            SaveCacheData();
        }

        private void Unload()
        {
            foreach( var player in BasePlayer.activePlayerList )
            {
                if( _playersCache.ContainsKey( player.userID ) )
                {
                    LeaveAimTrain( player );
                    if( _config.EnableUI )
                    {
                        CuiHelper.DestroyUi( player, _mainContainer );
                        CuiHelper.DestroyUi( player, _statsContainer );
                    }
                }
            }

            foreach( var arena in _arenasCache )
            {
                arena.Value.Players = 0;
                ClearBots( arena.Key );
            }

            ServerMgr.Instance.StopAllCoroutines();
            SaveCacheData();
            DeleteAll<Bot>();
        }

        #endregion

        #region Functions

        private void ClearBots( string arenaName )
        {
            foreach( var bot in _spawnedBots )
            {
                if( bot.CurrentArena == arenaName )
                {
                    if( !bot.Player.IsDestroyed )
                    {
                        bot.Player.Kill();
                    }
                }
            }

            _spawnedBots.Clear();
            _spawnedBotsNetIds.Clear();
        }

        private void DeleteAll<T>() where T : MonoBehaviour
        {
            foreach( var type in UnityEngine.Object.FindObjectsOfType<T>() )
            {
                UnityEngine.Object.Destroy( type );
            }
        }

        private void SaveCacheData()
        {
            _storedData.Arenas = _arenasCache;
            Interface.Oxide.DataFileSystem.WriteObject( Name, _storedData );
        }

        private IEnumerator ChangeBotAmount( int bots, string arenaName )
        {
            for( var i = 0; i < bots; i++ )
            {
                if( _arenasCache[arenaName].Players == 0 )
                {
                    yield break;
                }

                CreateBot( arenaName );
                yield return CoroutineEx.waitForSeconds( 0.5f );
            }
        }

        private void MovePlayer( BasePlayer player, Vector3 pos )
        {
            player.EnsureDismounted();
            player.SetPlayerFlag( BasePlayer.PlayerFlags.ReceivingSnapshot, true );
            player.Teleport( pos );
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate( false );
            player.ClientRPCPlayer( null, player, "StartLoading" );
            player.SendFullSnapshot();
        }

        private void ChangeBotCount( int amount, string arenaName )
        {
            ClearBots( arenaName );
            if( amount == 0 )
            {
                return;
            }

            _arenasCache[arenaName].RunningCoroutine = ServerMgr.Instance.StartCoroutine( ChangeBotAmount( amount, arenaName ) );
        }

        private void CreateBot( string arenaName )
        {
            var arena = _arenasCache[arenaName];

            var spawn = Vector3.zero;

            if( !arena.BotMoving )
            {
                spawn = arena.GetSpawn().ToVector3();
            }
            else
            {
                int spawnPosition = UnityEngine.Random.Range( 1, arena.SpawnsBot.Count );
                spawn = arena.SpawnsBot[spawnPosition].ToVector3();
            }

            var newBot = GameManager.server.CreateEntity( "assets/prefabs/player/player.prefab", spawn, Quaternion.identity );
            newBot.enableSaving = false;
            newBot.Spawn();

            var botMover = newBot.GetOrAddComponent<Bot>();
            _spawnedBots.Add( botMover );
            _spawnedBotsNetIds.Add( newBot.net.ID );
            botMover.CurrentArena = arenaName;

            int random = UnityEngine.Random.Range( 0, 2 );
            if( random.Equals( 1 ) )
            {
                botMover.minSpeed = 5.5f;
                botMover.maxSpeed = 5.5f;
            }
            else
            {
                botMover.minSpeed = 2.4f;
                botMover.maxSpeed = 2.4f;
            }

            Kits?.Call( "GiveKit", newBot, arena.Kits.GetRandom() );
        }

        private string Lang( string key, string id = null, params object[] args )
        {
            return string.Format( lang.GetMessage( key, this, id ), args );
        }

        private void EnterAimTrain( BasePlayer player, string arenaName )
        {
            if( Interface.CallHook( "CanJoinAimTrain", player ) != null )
            {
                return;
            }

            if( _arenasCache[arenaName].SpawnsPlayer.Count == 0 )
            {
                PrintWarning( "No player spawn points set" );
                SendReply( player, Lang( "ErrorSpawns" ) );
                return;
            }

            _arenasCache[arenaName].Players++;
            int randomSpawn = UnityEngine.Random.Range( 1, _arenasCache[arenaName].SpawnsPlayer.Count );
            _playersCache.Add( player.userID, new PlayerData() );

            if( _arenasCache[arenaName].Players == 1 )
            {
                ChangeBotCount( _arenasCache[arenaName].BotCount, arenaName );
            }

            _playersCache[player.userID].Arena = arenaName;
            _playersCache[player.userID].Pos = new Position( player.transform.position.x, player.transform.position.y, player.transform.position.z );
            MovePlayer( player, _arenasCache[arenaName].SpawnsPlayer[randomSpawn].ToVector3() );
            if( _config.EnableUI )
            {
                CuiHelper.AddUi( player, _cachedContainerJson );
                UpdateTimer( player );
            }

            if( !_config.IgnoreInv )
            {
                StripPlayer( player );
            }

            player.limitNetworking = true;
            player.SendNetworkUpdateImmediate();

            SendReply( player, Lang( "JoinAT" ) );
            Kits?.Call( "GiveKit", player, _arenasCache[arenaName].PlayerKit );
            Interface.CallHook( "JoinedAimTrain", player );
        }

        private void LeaveAimTrain( BasePlayer player, Vector3 position = default( Vector3 ) )
        {
            string arenaName = _playersCache[player.userID].Arena;
            StripPlayer( player );

            if( position != default( Vector3 ) )
            {
                MovePlayer( player, position );
            }
            else if( _config.TpPosLeftAimTrain != null )
            {
                MovePlayer( player, _config.TpPosLeftAimTrain.ToVector3() );
            }
            else
            {
                MovePlayer( player, _playersCache[player.userID].Pos.ToVector3() );
            }

            player.limitNetworking = false;
            player.SendNetworkUpdateImmediate();

            _arenasCache[arenaName].Players--;
            _playersCache.Remove( player.userID );

            if( _arenasCache[arenaName].Players == 0 )
            {
                ServerMgr.Instance.StopCoroutine( _arenasCache[arenaName].RunningCoroutine );
                ClearBots( arenaName );
            }

            if( _config.EnableUI )
            {
                CuiHelper.DestroyUi( player, _mainContainer );
                CuiHelper.DestroyUi( player, _statsContainer );
                if( _noAmmo.Contains( player.userID ) )
                {
                    _noAmmo.Remove( player.userID );
                }
            }

            SendReply( player, Lang( "LeaveAT" ) );
            Interface.CallHook( "LeftAimTrain", player );
        }

        private void StripPlayer( BasePlayer player )
        {
            StripContainer( player.inventory.containerBelt );
            StripContainer( player.inventory.containerMain );
            StripContainer( player.inventory.containerWear );
        }

        private void StripContainer( ItemContainer container )
        {
            for( int i = container.itemList.Count - 1; i >= 0; i-- )
            {
                container.itemList[i].Remove();
            }

            ItemManager.DoRemoves();
        }

        private string AmmoStatus( ulong playerId )
        {
            var ammoStatus = "";
            if( _noAmmo.Contains( playerId ) )
            {
                ammoStatus = "Unlimited Ammo: OFF";
            }
            else
            {
                ammoStatus = "Unlimited Ammo: ON";
            }

            return ammoStatus;
        }

        private bool IsAimTraining( ulong playerId )
        {
            return _playersCache.ContainsKey( playerId );
        }

        #endregion

        #region Commands

        [ChatCommand( "at" )]
        private void CmdAimTrain( BasePlayer player, string command, string[] args )
        {
            if( !permission.UserHasPermission( player.UserIDString, "aimtrain.join" ) || !_config.EnableAimTrain )
            {
                return;
            }

            if( _config.UseNoEscape )
            {
                if( plugins.Exists( "NoEscape" ) && ( (bool) NoEscape.Call( "IsCombatBlocked", player ) || (bool) NoEscape.Call( "IsRaidBlocked", player ) ) )
                {
                    return;
                }
            }

            if( _playersCache.ContainsKey( player.userID ) )
            {
                LeaveAimTrain( player );
                return;
            }

            if( Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())).Count() != 0 && _config.IgnoreInv )
            {
                SendReply( player, Lang( "ClearInv" ) );
                return;
            }

            if( _arenasCache.Keys.Count == 1 )
            {
                string arena = _arenasCache.Keys.First();
                if( !_arenasCache[arena].SpawnsPlayer.Count.Equals( 0 ) && !_arenasCache[arena].SpawnsBot.Count.Equals( 0 ) )
                {
                    if( _config.ArenaPermission )
                    {
                        if( permission.UserHasPermission( player.UserIDString, "aimtrain." + arena ) )
                        {
                            EnterAimTrain( player, arena );
                            return;
                        }

                        SendReply( player, Lang( "NoPerm" ) );
                        return;
                    }

                    EnterAimTrain( player, arena );
                }

                return;
            }

            if( args.Length < 1 )
            {
                return;
            }

            string arenaName = args[0];
            if( arenaName == null || !_arenasCache.ContainsKey( arenaName ) )
            {
                SendReply( player, Lang( "ArenaNotExisting" ) );
                return;
            }

            if( _arenasCache.ContainsKey( arenaName ) && _arenasCache[arenaName].SpawnsBot.Count != 0 && _arenasCache[arenaName].SpawnsPlayer.Count != 0 )
            {
                if( _config.ArenaPermission )
                {
                    if( permission.UserHasPermission( player.UserIDString, "aimtrain." + arenaName ) )
                    {
                        EnterAimTrain( player, arenaName );
                    }
                    else
                    {
                        SendReply( player, Lang( "NoPerm" ) );
                    }
                }
                else
                {
                    EnterAimTrain( player, arenaName );
                }
            }
        }

        [ChatCommand( "at_edit" )]
        private void CmdAimTrainEdit( BasePlayer player, string command, string[] args )
        {
            if( !permission.UserHasPermission( player.UserIDString, "aimtrain.admin" ) || args.Length < 1 || args[0] == null )
            {
                return;
            }

            string arenaName = args[0];
            if( _arenasCache.ContainsKey( arenaName ) )
            {
                if( _editArena.ContainsKey( player.userID ) )
                {
                    _editArena[player.userID] = arenaName;
                    SendReply( player, Lang( "EditArena", null, arenaName ) );
                }
                else
                {
                    _editArena.Add( player.userID, arenaName );
                    SendReply( player, Lang( "EditArena", null, arenaName ) );
                }
            }
            else
            {
                SendReply( player, Lang( "ArenaNotExisting" ) );
            }
        }

        [ChatCommand( "aimtrain" )]
        private void CmdAdminAimTrain( BasePlayer player, string command, string[] args )
        {
            if( !permission.UserHasPermission( player.UserIDString, "aimtrain.admin" ) )
            {
                return;
            }

            if( args.Length < 1 )
            {
                SendReply( player, Lang( "ATAdmin" ) );
                return;
            }

            switch( args[0] )
            {
                case "add":
                {
                    if( args.Length < 2 )
                    {
                        SendReply( player, Lang( "InvalidName" ) );
                        return;
                    }

                    string arenaName = args[1];
                    if( _arenasCache.ContainsKey( arenaName ) )
                    {
                        SendReply( player, Lang( "ArenaExist" ) );
                        return;
                    }

                    _arenasCache.Add( arenaName, new Arena() );
                    if( _config.ArenaPermission )
                    {
                        permission.RegisterPermission( $"aimtrain.{arenaName}", this );
                    }

                    SendReply( player, Lang( "ArenaCreated", null, arenaName ) );
                    break;
                }
                case "delete":
                {
                    if( args.Length < 2 )
                    {
                        SendReply( player, Lang( "InvalidName" ) );
                        return;
                    }

                    string arenaName = args[1];
                    if( !_arenasCache.ContainsKey( arenaName ) )
                    {
                        SendReply( player, Lang( "ArenaNotExisting" ) );
                        return;
                    }

                    foreach( ulong playerId in _playersCache.Keys.ToList() )
                    {
                        if( _playersCache[playerId].Arena == arenaName )
                        {
                            LeaveAimTrain( BasePlayer.FindByID( playerId ) );
                        }
                    }

                    SendReply( player, Lang( "ArenaDeleted", null, arenaName ) );
                    _arenasCache.Remove( arenaName );
                    break;
                }
                case "leavepos":
                {
                    _config.TpPosLeftAimTrain = new Position( player.transform.position );
                    SaveConfig();
                    SendReply( player, Lang( "SetLeavePosition", null ) );
                    break;
                }
                case "info":
                {
                    if( args.Length < 2 )
                    {
                        SendReply( player, Lang( "InvalidName" ) );
                        return;
                    }

                    string arenaName = args[1];
                    if( !_arenasCache.ContainsKey( arenaName ) )
                    {
                        SendReply( player, Lang( "ArenaNotExisting" ) );
                        return;
                    }

                    SendReply( player, string.Join( "\n", new[]
                    {
                        $"<size=16><color=#4286f4>AimTrain</color></size> Arena: <i>{arenaName}</i>",
                        "Bot Kits: " + string.Join( ", ", _arenasCache[arenaName].Kits.ToArray() ),
                        $"Player Kit: {_arenasCache[arenaName].PlayerKit}",
                        $"Bot Spawns: {_arenasCache[arenaName].SpawnsBot.Count.ToString()}",
                        $"Player Spawns: {_arenasCache[arenaName].SpawnsPlayer.Count.ToString()}",
                        $"Enabled: {_arenasCache[arenaName].Enabled}",
                        $"Movement: {_arenasCache[arenaName].BotMoving}"
                    } ) );
                    break;
                }
                case "list":
                {
                    var arenas = new List<string>();
                    foreach( var arena in _arenasCache )
                    {
                        arenas.Add( arena.Key );
                    }

                    SendReply( player, "<size=16><color=#4286f4>AimTrain</color></size> Arenas:\n" + string.Join( "\n", arenas.ToArray() ) );
                    break;
                }
                case "botkit":
                {
                    if( args.Length < 2 )
                    {
                        SendReply( player, Lang( "InvalidName" ) );
                        return;
                    }

                    if( !_editArena.ContainsKey( player.userID ) )
                    {
                        SendReply( player, Lang( "NotEditingArena" ) );
                        return;
                    }

                    string arenaEdit = _editArena[player.userID];
                    string kitName = args[1];
                    if( kitName == "clear" )
                    {
                        _arenasCache[arenaEdit].Kits.Clear();
                        SendReply( player, Lang( "ClearBotKit", null, arenaEdit ) );
                        return;
                    }

                    if( !_arenasCache[arenaEdit].Kits.Contains( kitName ) )
                    {
                        SendReply( player, Lang( "AddedBotKit", null, kitName ) );
                        _arenasCache[arenaEdit].Kits.Add( kitName );
                    }

                    break;
                }
                case "playerkit":
                {
                    if( !_editArena.ContainsKey( player.userID ) )
                    {
                        SendReply( player, Lang( "NotEditingArena" ) );
                        return;
                    }

                    string arenaEdit = _editArena[player.userID];
                    if( args.Length < 2 )
                    {
                        SendReply( player, Lang( "InvalidName" ) );
                        return;
                    }

                    string kitName = args[1];
                    SendReply( player, Lang( "AddedPlayerKit", null, kitName ) );
                    _arenasCache[arenaEdit].PlayerKit = kitName;
                    break;
                }
                case "sbot":
                {
                    if( !_editArena.ContainsKey( player.userID ) )
                    {
                        SendReply( player, Lang( "NotEditingArena" ) );
                        return;
                    }

                    string arenaEdit = _editArena[player.userID];
                    if( args.Length < 2 )
                    {
                        _arenasCache[arenaEdit].SpawnsBot[_arenasCache[arenaEdit].SpawnsBot.Count + 1] = new Position( player.transform.position.x, player.transform.position.y, player.transform.position.z );
                        SendReply( player, Lang( "SpawnBot", null, _arenasCache[arenaEdit].SpawnsBot.Count.ToString() ) );
                    }
                    else if( args[1] == "clear" )
                    {
                        _arenasCache[arenaEdit].SpawnsBot.Clear();
                        SendReply( player, Lang( "ClearBotSpawns", null, arenaEdit ) );
                        return;
                    }

                    break;
                }
                case "splayer":
                {
                    if( !_editArena.ContainsKey( player.userID ) )
                    {
                        SendReply( player, Lang( "NotEditingArena" ) );
                        return;
                    }

                    string arenaEdit = _editArena[player.userID];
                    if( args.Length < 2 )
                    {
                        _arenasCache[arenaEdit].SpawnsPlayer[_arenasCache[arenaEdit].SpawnsPlayer.Count + 1] = new Position( player.transform.position.x, player.transform.position.y, player.transform.position.z );
                        SendReply( player, Lang( "SpawnPlayer", null, _arenasCache[arenaEdit].SpawnsPlayer.Count.ToString() ) );
                    }
                    else if( args[1] == "clear" )
                    {
                        _arenasCache[arenaEdit].SpawnsPlayer.Clear();
                        SendReply( player, Lang( "ClearPlayerSpawns", null, arenaEdit ) );
                    }

                    break;
                }
                case "movement":
                {
                    if( !_editArena.ContainsKey( player.userID ) )
                    {
                        SendReply( player, Lang( "NotEditingArena" ) );
                        return;
                    }

                    string arenaEdit = _editArena[player.userID];
                    if( _arenasCache[arenaEdit].BotMoving )
                    {
                        _arenasCache[arenaEdit].BotMoving = false;
                    }
                    else
                    {
                        _arenasCache[arenaEdit].BotMoving = true;
                    }

                    SendReply( player, $"Movement: {_arenasCache[arenaEdit].BotMoving}" );
                    break;
                }
                case "enable":
                {
                    if( !_editArena.ContainsKey( player.userID ) )
                    {
                        SendReply( player, Lang( "NotEditingArena" ) );
                        return;
                    }

                    string arenaEdit = _editArena[player.userID];
                    if( _arenasCache[arenaEdit].Enabled )
                    {
                        _arenasCache[arenaEdit].Enabled = false;
                    }
                    else
                    {
                        _arenasCache[arenaEdit].Enabled = true;
                    }

                    SendReply( player, $"Enabled: {_arenasCache[arenaEdit].Enabled}" );
                    break;
                }
                case "botcount":
                {
                    if( args.Length < 2 )
                    {
                        SendReply( player, "Not a valid number." );
                        return;
                    }

                    if( !_editArena.ContainsKey( player.userID ) )
                    {
                        SendReply( player, Lang( "NotEditingArena" ) );
                        return;
                    }

                    string arenaEdit = _editArena[player.userID];
                    int amount;
                    if( !int.TryParse( args[1], out amount ) )
                    {
                        SendReply( player, "Not a valid number." );
                        return;
                    }

                    ChangeBotCount( amount, arenaEdit );
                    _arenasCache[arenaEdit].BotCount = amount;
                    SendReply( player, $"Changed Bot amount in Arena: {arenaEdit} to {amount}" );
                    break;
                }
            }
        }

        [ConsoleCommand( "LeaveAT" )]
        private void CmdUiLeaveAimTrain( ConsoleSystem.Arg arg )
        {
            LeaveAimTrain( arg.Player() );
        }

        [ConsoleCommand( "AmmoAT" )]
        private void CmdUiToggleAimTrain( ConsoleSystem.Arg arg )
        {
            var player = arg.Player();
            if( _noAmmo.Contains( player.userID ) )
            {
                _noAmmo.Remove( player.userID );
            }
            else
            {
                _noAmmo.Add( player.userID );
            }

            UpdateTimer( player );
        }

        [ConsoleCommand( "ResetAT" )]
        private void CmdUiResetStats( ConsoleSystem.Arg arg )
        {
            var player = arg.Player();
            _playersCache[player.userID].Hits = 0;
            _playersCache[player.userID].Headshots = 0;
            _playersCache[player.userID].Bullets = 0;
            UpdateTimer( player );
        }

        #endregion

        #region Bot Class

        public class Bot : MonoBehaviour
        {
            public BasePlayer Player;
            public bool IsLerping;
            private Vector3 _startPos;
            private Vector3 _endPos;
            private float _timeTakenDuringLerp;
            public float minSpeed;
            public float maxSpeed;
            private float _lastDelta;
            public string CurrentArena;

            private void SetViewAngle( Quaternion viewAngles )
            {
                if( viewAngles.eulerAngles == default( Vector3 ) )
                {
                    return;
                }

                Player.OverrideViewAngles( viewAngles.eulerAngles );
                Player.SendNetworkUpdateImmediate();
            }

            private void Start()
            {
                Player = GetComponent<BasePlayer>();
                Player.InitializeHealth( 100, 100 );
                Player.displayName = _instance._config.BotNames.GetRandom();
                StartLerping();
            }

            private void StartLerping()
            {
                if( _instance._arenasCache[CurrentArena].SpawnsBot.Count <= 1 )
                {
                    IsLerping = false;
                    return;
                }

                if( _instance._arenasCache[CurrentArena].SpawnsBot.Keys.Count > 1 )
                {
                    var spawnPoint = _instance._arenasCache[CurrentArena].SpawnsBot.ElementAt( UnityEngine.Random.Range( 1, _instance._arenasCache[CurrentArena].SpawnsBot.Keys.Count ) );
                    _endPos = new Vector3( spawnPoint.Value.PosX, spawnPoint.Value.PosY, spawnPoint.Value.PosZ );
                    _startPos = transform.position;
                    if( _endPos != Player.transform.position )
                    {
                        SetViewAngle( Quaternion.LookRotation( _endPos - Player.transform.position ) );
                    }

                    float distanceToDestination = Vector3.Distance( _startPos, _endPos );
                    _timeTakenDuringLerp = distanceToDestination / UnityEngine.Random.Range( minSpeed, maxSpeed );
                    _lastDelta = 0.0f;
                    IsLerping = true;
                }
            }

            private static float GetGroundY( Vector3 position )
            {
                RaycastHit hitinfo;
                if( Physics.Raycast( position + Vector3.up, new Vector3( 0f, -1f, 0f ), out hitinfo, 30f, LayerMask.GetMask( "Construction", "Clutter", "World" ) ) )
                {
                    float posY = Math.Max( hitinfo.point.y, TerrainMeta.HeightMap.GetHeight( position ) );
                    return posY;
                }

                float height = TerrainMeta.HeightMap.GetHeight( position );
                var pos = new Vector3( position.x, height, position.z );
                return pos.y;
            }

            private void FixedUpdate()
            {
                if( !_instance._arenasCache[CurrentArena].BotMoving )
                {
                    return;
                }

                if( IsLerping )
                {
                    _lastDelta += Time.deltaTime;
                    float pct = _lastDelta / _timeTakenDuringLerp;
                    var nextPos = Vector3.Lerp( _startPos, _endPos, pct );
                    nextPos.y = GetGroundY( nextPos );
                    Player.MovePosition( nextPos );
                    Player.EnablePlayerCollider();
                    if( pct >= 1.0f )
                    {
                        IsLerping = false;
                        StartLerping();
                    }
                }
                else
                {
                    if( _instance._arenasCache[CurrentArena].SpawnsBot.Keys.Count > 1 )
                    {
                        StartLerping();
                    }
                }
            }
        }

        #endregion

        #region Config

        public class Configuration
        {
            [JsonProperty( PropertyName = "Enable AimTrain" )]
            public bool EnableAimTrain = true;

            [JsonProperty( PropertyName = "Use permissons for Arena" )]
            public bool ArenaPermission = false;

            [JsonProperty( PropertyName = "Needs empty inventory to join" )]
            public bool IgnoreInv = true;

            [JsonProperty( PropertyName = "Enable UI" )]
            public bool EnableUI = true;

            [JsonProperty( PropertyName = "Use NoEscape Raid/Combatblock" )]
            public bool UseNoEscape = false;

            [JsonProperty( PropertyName = "Bot Names" )]
            public List<string> BotNames;

            [JsonProperty( PropertyName = "Commands that cant be used during AimTrain" )]
            public List<string> BlacklistedCommands;

            [JsonProperty( PropertyName = "Leave Position" )]
            public Position TpPosLeftAimTrain;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    BotNames = new List<string>
                    {
                        "Bot1",
                        "Bot2",
                        "Bot3"
                    },
                    BlacklistedCommands = new List<string>
                    {
                        "tp",
                        "home"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if( _config == null )
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning( $"Creating new config file." );
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = Configuration.DefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject( _config );
        }

        #endregion

        #region Data

        public class Position
        {
            public float PosX;
            public float PosY;
            public float PosZ;

            public Position() { }

            public Position( float x, float y, float z )
            {
                PosX = x;
                PosY = y;
                PosZ = z;
            }

            public Position( Vector3 vector )
            {
                PosX = vector.x;
                PosY = vector.y;
                PosZ = vector.z;
            }

            public Vector3 ToVector3()
            {
                return new Vector3( PosX, PosY, PosZ );
            }
        }

        private class Arena
        {
            public bool Enabled = true;
            public string PlayerKit = "Player Kit";
            public List<string> Kits = new List<string>();
            public int BotCount = 5;
            public bool BotMoving = true;
            public Dictionary<int, Position> SpawnsBot = new Dictionary<int, Position>();
            public Dictionary<int, Position> SpawnsPlayer = new Dictionary<int, Position>();

            [JsonIgnore]
            public int Players;

            [JsonIgnore]
            private int _spawnIndex = 1;

            [JsonIgnore]
            public Coroutine RunningCoroutine;

            public Position GetSpawn()
            {
                var spawn = SpawnsBot[_spawnIndex];
                _spawnIndex++;

                if( _spawnIndex >= SpawnsBot.Count )
                {
                    _spawnIndex = 1;
                }

                return spawn;
            }
        }

        private class PlayerData
        {
            public string Arena;
            public Position Pos;
            public int Hits;
            public int Bullets;
            public int Headshots;
        }

        private class StoredData
        {
            public Dictionary<string, Arena> Arenas = new Dictionary<string, Arena>();
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages( new Dictionary<string, string>
            {
                ["ClearInv"] = "Please clear your inventory before you join AimTrain.",
                ["JoinAT"] = "You've joined AimTrain! Use /at to leave.",
                ["LeaveAT"] = "You've left AimTrain.",
                ["SpawnBot"] = "Created a new Bot spawn point({0}).",
                ["SpawnPlayer"] = "Created the spawn point for players({0}).",
                ["ATAdmin"] = string.Join( "\n", new[]
                {
                    "<size=16><color=#4286f4>AimTrain</color></size>",
                    "/aimtrain add <name> - <i>Create a Arena</i>",
                    "/at_edit <name> - <i>to edit a Arena</i>",
                    "/aimtrain delete <name> - <i>Delete a Arena</i>",
                    "/aimtrain list - <i>See all Arenas</i>",
                    "/aimtrain info <name> - <i>See the settings for your Arena</i>",
                    "/aimtrain sbot - <i>Set spawn points for bots</i>",
                    "/aimtrain splayer - <i>Set spawn points for players)</i>",
                    "/aimtrain botkit <name> - <i>Add a kit for the Bots</i>",
                    "/aimtrain playerkit <name> - <i>Add a kit for the players</i>",
                    "/aimtrain movement - <i>Enable bot moving</i>",
                    "/aimtrain enable - <i>Enable AimTrain</i>",
                    "/aimtrain botcount <amount> - <i>Change the amount of bots in the Arena</i>"
                } ),
                ["EnableAimTrain"] = "AimTrain enabled: {0}",
                ["ErrorSpawns"] = "No spawn points for players set.",
                ["ErrorSpawnsbot"] = "You don't have spawn points for bots set {0} / 2.",
                ["CantWhileAimTrain"] = "You cant perform this action while you are in AimTrain.",
                ["NoPerm"] = "You don't have permissions to join this Arena!",
                ["ArenaExist"] = "This Arena already exists!",
                ["ArenaNotExisting"] = "This Arena doesn't exist, use /aimtrain add to create a Arena.",
                ["EditArena"] = "You are now editing Arena: {0}.",
                ["NotEditingArena"] = "You aren't editing a Arena, use /at_edit <name> in order to do so",
                ["ArenaCreated"] = "You created a new Arena called: {0}.",
                ["ArenaDeleted"] = "You deleted the Arena: {0}.",
                ["InvalidName"] = "Not a valid Arena Name.",
                ["ClearBotKit"] = "You cleared all Bot Kits.",
                ["ClearBotSpawns"] = "You cleared all spawn points for the Bots!",
                ["ClearPlayerSpawns"] = "You cleared all spawn points for the Players!",
                ["AddedBotKit"] = "You added the Kit <i>{0}</i> to the Bot Kits.",
                ["AddedPlayerKit"] = "You changed the Player Kit to <i>{0}</i>.",
                ["SetLeavePosition"] = "You changed the current default leave TP position to your current location."
            }, this );
        }

        #endregion

        #region GUI

        private void UpdateTimer( BasePlayer player )
        {
            var container = DrawTimer( player );
            CuiHelper.DestroyUi( player, _statsContainer );
            CuiHelper.AddUi( player, container );
        }

        private void NewBorder( Anchor min, Anchor max )
        {
            UI.Border( min, max, ref _cachedContainer, 0.001f, "1 1 1 1", _mainContainer );
        }

        private CuiElementContainer DrawTimer( BasePlayer player )
        {
            var percentComplete = 0;
            if( _playersCache[player.userID].Bullets != 0 && _playersCache[player.userID].Hits != 0 )
            {
                percentComplete = (int) Math.Round( (double) ( 100 * _playersCache[player.userID].Hits ) / _playersCache[player.userID].Bullets );
            }

            var container = UI.Container( _statsContainer, "0 0 0 0", new Anchor( 0f, 0.35f ), new Anchor( 0.1f, 0.6f ), "Hud.Menu" );
            UI.Text( "", _statsContainer, ref container, TextAnchor.MiddleRight, "0 0 0 1", 13, _playersCache[player.userID].Headshots.ToString() + " ", new Anchor( 0f, 0.8f ), new Anchor( 0.99f, 0.9f ) );
            UI.Text( "", _statsContainer, ref container, TextAnchor.MiddleRight, "0 0 0 1", 13, _playersCache[player.userID].Bullets.ToString() + " ", new Anchor( 0f, 0.7f ), new Anchor( 0.99f, 0.8f ) );
            UI.Text( "", _statsContainer, ref container, TextAnchor.MiddleRight, "0 0 0 1", 13, _playersCache[player.userID].Hits.ToString() + " ", new Anchor( 0f, 0.6f ), new Anchor( 0.99f, 0.7f ) );
            UI.Text( "", _statsContainer, ref container, TextAnchor.MiddleRight, "0 0 0 1", 13, percentComplete.ToString() + "%" + " ", new Anchor( 0f, 0.5f ), new Anchor( 0.99f, 0.6f ) );
            UI.Text( "", _statsContainer, ref container, TextAnchor.MiddleLeft, "0 0 0 1", 13, "  " + AmmoStatus( player.userID ), new Anchor( 0f, 0.4f ), new Anchor( 0.99f, 0.5f ) );
            return container;
        }

        private void ConstructUi()
        {
            _cachedContainer = UI.Container( _mainContainer, "0 0 0 0", new Anchor( 0f, 0.35f ), new Anchor( 0.1f, 0.6f ), "Overlay" );
            UI.Text( "Main.Name", _mainContainer, ref _cachedContainer, TextAnchor.MiddleCenter, "0 0 0 1", 15, "AimTrain", new Anchor( 0f, 0.9f ), new Anchor( 1f, 1f ) );
            UI.Text( "", _mainContainer, ref _cachedContainer, TextAnchor.MiddleLeft, "0 0 0 1", 13, "  Headshots: ", new Anchor( 0f, 0.8f ), new Anchor( 0.99f, 0.9f ) );
            UI.Text( "", _mainContainer, ref _cachedContainer, TextAnchor.MiddleLeft, "0 0 0 1", 13, "  Bullets fired: ", new Anchor( 0f, 0.7f ), new Anchor( 0.99f, 0.8f ) );
            UI.Text( "", _mainContainer, ref _cachedContainer, TextAnchor.MiddleLeft, "0 0 0 1", 13, "  Hits: ", new Anchor( 0f, 0.6f ), new Anchor( 0.99f, 0.7f ) );
            UI.Text( "", _mainContainer, ref _cachedContainer, TextAnchor.MiddleLeft, "0 0 0 1", 13, "  Accuracy: ", new Anchor( 0f, 0.5f ), new Anchor( 0.99f, 0.6f ) );
            UI.Button( "Button.Reset", _mainContainer, ref _cachedContainer, new Anchor( 0f, 0.27f ), new Anchor( 0.99f, 0.4f ), $"global.ResetAT", "Reset", "0 0 0", 12, "0 0 0 0" );
            UI.Button( "Button.Ammo", _mainContainer, ref _cachedContainer, new Anchor( 0f, 0.14f ), new Anchor( 0.99f, 0.27f ), $"global.AmmoAT", "Toggle Ammo", "0 0 0", 12, "0 0 0 0" );
            UI.Button( "Button.Leave", _mainContainer, ref _cachedContainer, new Anchor( 0f, 0f ), new Anchor( 0.99f, 0.14f ), $"global.LeaveAT", "Leave", "0 0 0", 12, "0 0 0 0" );
            NewBorder( new Anchor( 0f, 0.27f ), new Anchor( 0.99f, 0.4f ) );
            NewBorder( new Anchor( 0f, 0.14f ), new Anchor( 0.99f, 0.27f ) );
            NewBorder( new Anchor( 0f, 0f ), new Anchor( 1f, 1f ) );
            UI.Element( "", _mainContainer, ref _cachedContainer, new Anchor( 0.993f, 0f ), new Anchor( 0.99f, 1f ), "1 1 1 1" );

            _cachedContainerJson = _cachedContainer.ToJson();
        }

        #endregion
    }
}

namespace Oxide.Plugins.AimTrainUI
{
    public class UIMethods
    {
        public static CuiElementContainer Container( string name, string bgColor, Anchor Min, Anchor Max,
            string parent = "Overlay", float fadeOut = 0f, float fadeIn = 0f )
        {
            var newElement = new CuiElementContainer()
            {
                new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = fadeOut,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = bgColor,
                            FadeIn = fadeIn
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{Min.X} {Min.Y}",
                            AnchorMax = $"{Max.X} {Max.Y}"
                        }
                    }
                }
            };
            return newElement;
        }

        public static void Panel( string name, string parent, ref CuiElementContainer container, string bgColor,
            Anchor Min, Anchor Max, bool cursor = false )
        {
            container.Add( new CuiPanel()
            {
                Image =
                {
                    Color = bgColor
                },
                CursorEnabled = cursor,
                RectTransform =
                {
                    AnchorMin = $"{Min.X} {Min.Y}",
                    AnchorMax = $"{Max.X} {Max.Y}"
                }
            }, parent, name );
        }

        public static void Label( string name, string parent, ref CuiElementContainer container, Anchor Min, Anchor Max,
            string text, string color = "1 1 1 1", int fontSize = 15, TextAnchor textAnchor = TextAnchor.MiddleCenter,
            string font = "robotocondensed-bold.ttf" )
        {
            container.Add( new CuiLabel()
            {
                Text =
                {
                    Align = textAnchor,
                    Color = color,
                    Font = font,
                    FontSize = fontSize
                },
                RectTransform =
                {
                    AnchorMin = $"{Min.X} {Min.Y}",
                    AnchorMax = $"{Max.X} {Max.Y}"
                }
            }, parent, name );
        }

        public static void Button( string name, string parent, ref CuiElementContainer container, Anchor Min,
            Anchor Max, string command, string text, string textColor,
            int fontSize, string color = "1 1 1 1", TextAnchor anchor = TextAnchor.MiddleCenter, float fadeOut = 0f,
            float fadeIn = 0f, string font = "robotocondensed-bold.ttf" )
        {
            container.Add( new CuiButton()
            {
                FadeOut = fadeOut,
                Button =
                {
                    Color = color,
                    Command = command
                },
                RectTransform =
                {
                    AnchorMin = $"{Min.X} {Min.Y}",
                    AnchorMax = $"{Max.X} {Max.Y}"
                },
                Text =
                {
                    Text = text,
                    Color = textColor,
                    Align = anchor,
                    Font = font,
                    FontSize = fontSize,
                    FadeIn = fadeIn
                }
            }, parent, name );
        }

        public static void Text( string name, string parent, ref CuiElementContainer container, TextAnchor anchor,
            string color, int fontSize, string text,
            Anchor Min, Anchor Max, string font = "robotocondensed-bold.ttf", float fadeOut = 0f,
            float fadeIn = 0f )
        {
            container.Add( new CuiElement()
            {
                Name = name,
                Parent = parent,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = text,
                        Align = anchor,
                        FontSize = fontSize,
                        Font = font,
                        FadeIn = fadeIn,
                        Color = color
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = $"{Min.X} {Min.Y}",
                        AnchorMax = $"{Max.X} {Max.Y}"
                    }
                }
            } );
        }

        public static void Element( string name, string parent, ref CuiElementContainer container, Anchor Min, Anchor Max,
            string bgColor, string material = "", float fadeOut = 0f, float fadeIn = 0f )
        {
            container.Add( new CuiElement()
            {
                Name = name,
                Parent = parent,
                FadeOut = fadeOut,
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = bgColor,
                        FadeIn = fadeIn
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = $"{Min.X} {Min.Y}",
                        AnchorMax = $"{Max.X} {Max.Y}"
                    }
                }
            } );
        }

        public static void Border( Anchor posMin, Anchor posMax, ref CuiElementContainer container, float borderSize = 0.001f, string color = "1 1 1 1", string parent = "Overlay" )
        {
            Element( "", parent, ref container, posMin, new Anchor( posMax.X, posMin.Y + borderSize * 2 ), "1 1 1 1" );
            Element( "", parent, ref container, new Anchor( posMin.X, posMax.Y - borderSize * 2 ), posMax, "1 1 1 1" );
            Element( "", parent, ref container, posMin, new Anchor( posMin.X + borderSize, posMax.Y ), "1 1 1 1" );
            Element( "", parent, ref container, new Anchor( posMax.X, posMin.Y ), new Anchor( posMax.X + borderSize, posMax.Y ), "1 1 1 1" );
        }
    }

    public class Anchor
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Anchor() { }

        public Anchor( float x, float y )
        {
            X = x;
            Y = y;
        }

        public static Anchor operator +( Anchor first, Anchor second )
        {
            return new Anchor( first.X + second.X, first.Y + second.Y );
        }

        public static Anchor operator -( Anchor first, Anchor second )
        {
            return new Anchor( first.X - second.X, first.Y - second.Y );
        }
    }

    public class Rgba
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }
        public float A { get; set; }

        public Rgba() { }

        public Rgba( float r, float g, float b, float a )
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public string Format()
        {
            return $"{R / 255} {G / 255} {B / 255} {A}";
        }
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */