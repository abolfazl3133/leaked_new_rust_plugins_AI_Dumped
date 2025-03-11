//  ######  
//  #     # 
//  #     # 
//  ######  
//  #   #   
//  #    #  
//  #     # 
//
//  #     # 
//  #     # 
//  #     # 
//  #     # 
//  #     # 
//  #     # 
//   #####  
//
//   #####  
//  #     # 
//  #       
//   #####  
//        # 
//  #     # 
//   #####  
//
//  #######
//     #    
//     #    
//     #    
//     #    
//     #    
//     #   
// 
//   ####  #####    ##   #####  ######   ####  
//  #      #    #  #  #  #    # #    #  #    # 
//   ####  #    # #    # #    # #    #  #    # 
//       # #    # ###### #####  ######  #    # 
//  #    # #    # #    # #      #   #   #    #  Creator
//   ####  #####  #    # #      #    #   #### 
//  Плагин создан специально для RustWin 2.0 x1000000 Упрощает работу модератору! не детектит стим игроков!
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("SpeedHackDetector", "sdapro", "0.0.0")]
    public class SpeedHackDetector : RustPlugin
    {
        private const float MAX_LEFT_SPEED = 4.7f;
        private const float MAX_RIGHT_SPEED = 4.7f;
        private const float MAX_BACK_SPEED = 4.7f;
        private const float MAX_BACK_LEFT_SPEED = 4.7f;
        private const float MAX_BACK_RIGHT_SPEED = 4.7f;
        private const float CHECK_INTERVAL = 0.1f;
        private const string PERMISSION_ADMIN = "speedhackdetector.admin"; // Это право дает показ модеру или админу что тип ебаный флеш
        private const string STEAM_API_KEY = "API Ключик из стима";
        private const int MIN_STEAM_LEVEL = 0;
        private const int MIN_STEAM_DAYS = 10;
        
        [PluginReference]
        private Plugin MultiFighting;
        [PluginReference]
        private Plugin NTeleportation;

        private class PlayerData
        {
            public Vector3 LastPosition;
            public Vector3 LastSafePosition;
            public float LastCheck;
            public bool WasViolated;
            public float JoinTime;
            public bool IsChecked;
            public float TotalPlayTime { get; set; }
            public float LastSessionUpdate { get; set; }
            public float LastYRotation;
            public float FreezeEndTime;
            public bool IsFrozen;
            public float LastDismountTime;
            public float LastRespawnTime;
        }
        
        private Dictionary<ulong, PlayerData> _playerData = new Dictionary<ulong, PlayerData>();

        void Init()
        {
            permission.RegisterPermission(PERMISSION_ADMIN, this);
        }

        private bool IsSteam(string id)
        {
            if (MultiFighting == null) return true;
            
            BasePlayer player = BasePlayer.Find(id);
            if (player == null) return false;
        
            object obj = MultiFighting.CallHook("IsSteam", player.Connection);
            return obj is bool ? (bool)obj : false;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
            {
                _playerData[player.userID] = new PlayerData 
                { 
                    LastPosition = player.transform.position,
                    LastSafePosition = player.transform.position,
                    LastCheck = Time.time,
                    WasViolated = false,
                    JoinTime = Time.time,
                    IsChecked = false,
                    TotalPlayTime = 0f,
                    LastSessionUpdate = Time.time,
                    LastYRotation = player.eyes.rotation.eulerAngles.y,
                    FreezeEndTime = 0f,
                    IsFrozen = false,
                    LastDismountTime = 0f,
                    LastRespawnTime = 0f
                };
                timer.Once(2f, () => CheckAccount(player));
            }
        }

        void OnServerInitialized()
        {
            timer.Once(5f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (!IsSteam(player.UserIDString))
                    {
                        CheckAccount(player);
                    }
                }
            });
        }

        private void CheckAccount(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            
            if (IsSteam(player.UserIDString)) return;

            webrequest.Enqueue(
                $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={STEAM_API_KEY}&steamids={player.UserIDString}",
                null,
                (code, response) =>
                {
                    if (code != 200) return;
                    
                    var json = JsonConvert.DeserializeObject<JObject>(response);
                    var players = json["response"]["players"].ToList();
                    
                    if (players.Count == 0) return;
                    
                    var profile = players[0];
                    var createdTime = profile["timecreated"]?.Value<long>() ?? 0;
                    var communityVisibilityState = profile["communityvisibilitystate"]?.Value<int>() ?? 1;
                    bool isPrivate = communityVisibilityState != 3;

                    webrequest.Enqueue(
                        $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={STEAM_API_KEY}&steamid={player.UserIDString}",
                        null,
                        (levelCode, levelResponse) =>
                        {
                            if (levelCode != 200) return;

                            var levelJson = JsonConvert.DeserializeObject<JObject>(levelResponse);
                            var steamLevel = levelJson["response"]["player_level"]?.Value<int>() ?? 0;

                            if (createdTime > 0)
                            {
                                var creationDate = DateTimeOffset.FromUnixTimeSeconds(createdTime).UtcDateTime;
                                var accountAge = DateTime.UtcNow - creationDate;
                                var daysOld = (int)accountAge.TotalDays;

                                string message = $"\n<color=#ff0000>Обнаружен пиратский аккаунт!</color>\n" +
                                               $"Имя: <color=#00ffff>{player.displayName}</color>\n" +
                                               $"SteamID: <color=#00ffff>{player.UserIDString}</color>\n" +
                                               $"Профиль: {(isPrivate ? "<color=#ff0000>Приватный</color>" : "<color=#00ff00>Публичный</color>")}\n" +
                                               $"Уровень Steam: <color=#00ffff>{steamLevel}</color>\n" +
                                               $"Возраст аккаунта: <color=#00ffff>{daysOld} дней</color>";

                                if (isPrivate)
                                {
                                    message += "\n<color=#ff0000>Игрок будет кикнут за приватный профиль</color>";
                                    timer.Once(0.5f, () => player.Kick("Ваш профиль Steam не может быть приватным на этом сервере"));
                                }
                                else if (steamLevel < MIN_STEAM_LEVEL)
                                {
                                    message += "\n<color=#ff0000>Игрок будет кикнут за низкий уровень Steam</color>";
                                    timer.Once(0.5f, () => player.Kick($"Требуется минимум {MIN_STEAM_LEVEL} уровень Steam"));
                                }
                                else if (daysOld < MIN_STEAM_DAYS)
                                {
                                    message += "\n<color=#ff0000>Игрок будет кикнут за новый аккаунт</color>";
                                    timer.Once(0.5f, () => player.Kick($"Ваш аккаунт слишком новый. Требуется аккаунт старше {MIN_STEAM_DAYS} дней"));
                                }

                                foreach (var admin in BasePlayer.activePlayerList.Where(p => 
                                    permission.UserHasPermission(p.UserIDString, PERMISSION_ADMIN)))
                                {
                                    admin.ChatMessage(message);
                                    admin.Command("note.play", "assets/prefabs/misc/summer_dlc/boom_box/effects/boombox-deploy.prefab");
                                }
                            }
                        },
                        this
                    );
                },
                this
            );
        }

        void OnPlayerTick(BasePlayer player)
        {
            if (player == null || !player.IsConnected || player.IsSleeping() || player.IsSpectating() ||  player.isMounted || !player.IsOnGround() || player.IsFlying ||  player.IsSwimming() || player.modelState.onLadder) return;

            if (IsSteam(player.UserIDString)) return;

            if (NTeleportation != null)
            {
                object isTeleporting = NTeleportation.Call("IsTeleporting", player);
                if (isTeleporting is bool && (bool)isTeleporting)
                {
                    return;
                }
            }

            if (_playerData.TryGetValue(player.userID, out var playerData) && Time.time - playerData.LastRespawnTime < 5f)
            {
                playerData.LastPosition = player.transform.position;
                playerData.LastCheck = Time.time;
                return;
            }

            bool isOnMovingPlatform = false;
            RaycastHit hit;
            if (Physics.Raycast(player.transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 0.3f))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity != null)
                {
                    BaseEntity currentEntity = entity;
                    while (currentEntity != null)
                    {
                        if (currentEntity.ShortPrefabName.Contains("cargo") || currentEntity.ShortPrefabName.Contains("heli") || currentEntity.ShortPrefabName.Contains("car") || currentEntity.ShortPrefabName.Contains("boat") || currentEntity is HotAirBalloon) 
                        {isOnMovingPlatform = true; break;}
                        currentEntity = currentEntity.GetParentEntity();
                    }
                }
            }

            if (!_playerData.ContainsKey(player.userID))
            {
                _playerData[player.userID] = new PlayerData
                {
                    LastPosition = player.transform.position,
                    LastSafePosition = player.transform.position,
                    LastCheck = Time.time,
                    WasViolated = false,
                    JoinTime = Time.time,
                    IsChecked = false,
                    TotalPlayTime = 0f,
                    LastSessionUpdate = Time.time,
                    LastYRotation = player.eyes.rotation.eulerAngles.y,
                    FreezeEndTime = 0f,
                    IsFrozen = false,
                    LastDismountTime = 0f,
                    LastRespawnTime = 0f
                };
                return;
            }

            var data = _playerData[player.userID];
            float deltaTime = Time.time - data.LastCheck;
            
            if (isOnMovingPlatform)
            {
                data.LastPosition = player.transform.position;
                data.LastCheck = Time.time;
                return;
            }

            bool isOnPrefab = false;
            if (Physics.Raycast(player.transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 0.3f, LayerMask.GetMask("Construction", "Deployable")))
            {
                isOnPrefab = true;
            }

            if (Time.time - data.LastDismountTime < 3f)
            {
                data.LastPosition = player.transform.position;
                data.LastCheck = Time.time;
                return;
            }

            if (deltaTime < CHECK_INTERVAL) return;

            Vector3 movement = (player.transform.position - data.LastPosition) / deltaTime;
            movement.y = 0;
            
            Vector3 forward = player.eyes.BodyForward();
            forward.y = 0;
            forward.Normalize();
            
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            
            float forwardSpeed = Vector3.Dot(movement, forward);
            float rightSpeed = Vector3.Dot(movement, right);

            float currentYRotation = player.eyes.rotation.eulerAngles.y;
            float rotationSpeed = Mathf.Abs(Mathf.DeltaAngle(currentYRotation, data.LastYRotation)) / deltaTime;
            data.LastYRotation = currentYRotation;

            float rotationSpeedBonus = rotationSpeed > 100f ? 3f : 0f;
            float prefabSpeedBonus = isOnPrefab ? 2f : 0f;
            float adjustedMaxSpeed = MAX_LEFT_SPEED + rotationSpeedBonus + prefabSpeedBonus;
            float adjustedMaxBackSpeed = MAX_BACK_SPEED + rotationSpeedBonus + prefabSpeedBonus;
            
            bool isViolating = false;
            string violationType = "";
            float violationSpeed = 0f;
            float maxAllowedSpeed = 0f;

            if (forwardSpeed > 0)
            {
                data.LastPosition = player.transform.position;
                data.LastCheck = Time.time;
                return;
            }

            if (forwardSpeed < 0)
            {
                float backSpeed = Mathf.Abs(forwardSpeed);
                if (backSpeed > adjustedMaxBackSpeed)
                {
                    isViolating = true;
                    violationType = "назад";
                    violationSpeed = backSpeed;
                    maxAllowedSpeed = adjustedMaxBackSpeed;
                }
            }

            if (!isViolating && Mathf.Abs(rightSpeed) > adjustedMaxSpeed)
            {
                isViolating = true;
                violationType = rightSpeed > 0 ? "вправо" : "влево";
                violationSpeed = Mathf.Abs(rightSpeed);
                maxAllowedSpeed = adjustedMaxSpeed;
            }

            if (data.WasViolated)
            {
                data.WasViolated = false;
                data.LastPosition = player.transform.position;
                data.LastCheck = Time.time;
                return;
            }

            if (isViolating && !data.IsFrozen)
            {
                UpdateSessionTime(player, data);
                
                TimeSpan sessionTime = TimeSpan.FromSeconds(player.Connection.GetSecondsConnected());
                string accountType = IsSteam(player.UserIDString) ? "<color=#00ff00>STEAM</color>" : "<color=#ff0000>ПИРАТ</color>";

                string message = $"\n<color=#ff0000>Обнаружен SpeedHack!</color>\n" +
                               $"Игрок: <color=#00ffff>{player.displayName}</color>\n" +
                               $"Тип аккаунта: {accountType}\n" +
                               $"Текущая сессия: {sessionTime.Hours}ч {sessionTime.Minutes}м {sessionTime.Seconds}с\n" +
                               $"Всего на сервере: {FormatTimeSpan(data.TotalPlayTime)}\n" +
                               $"Тип движения: <color=#ff0000>{violationType}</color>\n" +
                               $"Скорость: <color=#ff0000>{violationSpeed:F1} м/с</color>\n" +
                               $"Макс. допустимая: <color=#00ff00>{maxAllowedSpeed:F1} м/с</color>";

                foreach (var admin in BasePlayer.activePlayerList.Where(p => 
                    permission.UserHasPermission(p.UserIDString, PERMISSION_ADMIN)))
                {
                    admin.ChatMessage(message);
                    admin.Command("note.play", "assets/prefabs/misc/summer_dlc/boom_box/effects/boombox-deploy.prefab");
                }

                // //Фризить игрока!
                //data.IsFrozen = true;
               // data.FreezeEndTime = Time.time + 1f;
                data.WasViolated = true;
                
                //player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
                //player.SendNetworkUpdate(); 
            }
	//Фризить игрока!
           // else if (data.IsFrozen)
           // {
            //    if (Time.time >= data.FreezeEndTime)
            //    {
            //        data.IsFrozen = false;
              //      data.WasViolated = false;
              //  }
             //   else
             //   {
                    // Продолжаем удерживать игрока на месте
             //       player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
             //   }
           // }

            if (!data.IsFrozen)
            {
                data.LastSafePosition = player.transform.position;
                data.LastPosition = player.transform.position;
            }
            
            data.LastCheck = Time.time;
        }

        [ChatCommand("checkspeed")]
        private void CmdCheckSpeed(BasePlayer player, string command, string[] args)
        {
            if (!_playerData.ContainsKey(player.userID)) return;
            
            var data = _playerData[player.userID];
            float deltaTime = Time.time - data.LastCheck;
            
            Vector3 movement = (player.transform.position - data.LastPosition) / deltaTime;
            movement.y = 0;
            
            Vector3 forward = player.eyes.BodyForward();
            forward.y = 0;
            forward.Normalize();
            
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            
            float forwardSpeed = Vector3.Dot(movement, forward);
            float rightSpeed = Vector3.Dot(movement, right);
            
            string direction;
            if (forwardSpeed > 0.1f)
                direction = "вперед";
            else if (forwardSpeed < -0.1f)
                direction = "назад";
            else if (Mathf.Abs(rightSpeed) > 0.1f)
                direction = rightSpeed > 0 ? "вправо" : "влево";
            else
                direction = "стоит";
            
            player.ChatMessage($"Скорость:\n" +
                              $"Вперед/Назад: {forwardSpeed:F1} м/с\n" +
                              $"Вправо/Влево: {rightSpeed:F1} м/с\n" +
                              $"Направление: {direction}\n" +
                              $"Макс. скорость назад: {MAX_BACK_SPEED:F1} м/с\n" +
                              $"Макс. скорость в стороны: {MAX_LEFT_SPEED:F1} м/с");
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (_playerData.ContainsKey(player.userID))
            {
                var data = _playerData[player.userID];
                data.TotalPlayTime += (float)TimeSpan.FromSeconds(player.Connection.GetSecondsConnected()).TotalSeconds;
                _playerData.Remove(player.userID);
            }
        }

        private string FormatTimeSpan(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            if (time.Days > 0)
                return $"{time.Days}д {time.Hours}ч {time.Minutes}м";
            if (time.Hours > 0)
                return $"{time.Hours}ч {time.Minutes}м";
            return $"{time.Minutes}м {time.Seconds}с";
        }

        private void UpdateSessionTime(BasePlayer player, PlayerData data)
        {
            if (player == null || !player.IsConnected) return;
            
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - data.LastSessionUpdate < 60f) return;
            
            data.TotalPlayTime += (float)TimeSpan.FromSeconds(player.Connection.GetSecondsConnected()).TotalSeconds;
            data.LastSessionUpdate = currentTime;
        }

        void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            if (player == null || !_playerData.ContainsKey(player.userID)) return;
            
            var data = _playerData[player.userID];
            data.LastDismountTime = Time.time;
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !_playerData.ContainsKey(player.userID)) return;
            
            var data = _playerData[player.userID];
            data.LastRespawnTime = Time.time;
            data.LastPosition = player.transform.position;
            data.LastSafePosition = player.transform.position;
        }
    }
}