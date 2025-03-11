using Network;
using Network.Visibility;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("AntiHack", "https://discord.gg/dNGbxafuJn", "1.0.0")]
    public class AntiHack : RustPlugin
    {
		private static Dictionary<ulong, HashSet<ulong>> playersHidenEntities = new Dictionary<ulong, HashSet<ulong>>();
        private static int radius = 35;
        private Dictionary<BasePlayer, AntiHack.CurrentLog> currentAdminsLog = new Dictionary<BasePlayer, AntiHack.CurrentLog>();
        private Dictionary<ulong, float> lastSpeedAttackAttackTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastShootingThroughWallTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, int> speedAttackDetections = new Dictionary<ulong, int>();
        private Dictionary<ulong, int> shootingThroughWallDetections = new Dictionary<ulong, int>();
        private static bool isSaving = false;
        private static bool debug = false;
        private static float minPlayersWallHackDistanceCheck = 0.0f;
        private static float minObjectsWallHackDistanceCheck = 50.0f;
        private static float maxPlayersWallHackDistanceCheck = 250f;
        private static float maxObjectsWallHackDistanceCheck = 150f;
        private static float tickRate = 0.1f;
        private static int globalMask = LayerMask.GetMask("Construction", "Deployed", "World", "Default");
        private static int cM = LayerMask.GetMask("Construction");
        private static int playerWallHackMask = LayerMask.GetMask("Construction", "World", "Default", "Deployed");
        private static int entityMask = LayerMask.GetMask("Deployed");
        private static int constructionAndDeployed = LayerMask.GetMask("Construction", "Deployed");
        private static Dictionary<ulong, int> playersKicks = new Dictionary<ulong, int>();
        private static Dictionary<ulong, AntiHack.HackHandler> playersHandlers = new Dictionary<ulong, AntiHack.HackHandler>();
        private static Dictionary<int, Dictionary<int, AntiHack.Chunk>> chunks = new Dictionary<int, Dictionary<int, AntiHack.Chunk>>();
        private static HashSet<AntiHack.Chunk> chunksList = new HashSet<AntiHack.Chunk>();
        private List<string> neededEntities = new List<string>()
        {
          "cupboard.tool.deployed",
          "sleepingbag_leather_deployed",
          "bed_deployed"
        };
		public bool IsConnected(BasePlayer player) => BasePlayer.activePlayerList.Contains(player);
        public void Kick(BasePlayer player, string reason = "") => player.Kick(reason);
        public bool IsBanned(ulong id) => ServerUsers.Is(id, ServerUsers.UserGroup.Banned);
        private static AntiHack instance;
        private bool isLoaded;
        private static bool wallHackPlayersEnabled;
        private static bool wallHackObjectsEnabled;
        private static bool enableFlyHackLog;
        private static bool enableFlyHackCar;
        private static bool enableSpeedHackLog;
        private static bool enableTextureHackLog;
        private static bool enableSpeedAttackLog;
        private static bool enableWallHackAttackLog;
        private static bool needKick;
        private static bool needKickEndKill;
        private static bool needBan;
        private bool configChanged;
        private const int intervalBetweenTextureHackMessages = 50;
        private const int maxFalseFlyDetects = 5;
        private const int maxFalseSpeedDetects = 5;
        private static int maxFlyWarnings;
        private static int maxSpeedWarnings;
        private static AntiHack.StoredData db;
		
        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            Dictionary<string, object> dictionary = this.Config[menu] as Dictionary<string, object>;
            if (dictionary == null)
            {
                dictionary = new Dictionary<string, object>();
                this.Config[menu] = (object)dictionary;
                this.configChanged = true;
            }
            object obj;
            if (!dictionary.TryGetValue(datavalue, out obj))
            {
                obj = defaultValue;
                dictionary[datavalue] = obj;
                this.configChanged = true;
            }
            return obj;
        }
		
        protected override void LoadDefaultConfig()
        {
            this.Config.Clear();
            this.LoadVariables();
        }

        private void LoadVariables()
        {
            maxFlyWarnings = Convert.ToInt32(this.GetConfig("Основное", "Количество детектов FlyHack для наказания:", (object)10));
            Plugins.AntiHack.maxSpeedWarnings = Convert.ToInt32(this.GetConfig("Основное", "Количество детектов SpeedHack для наказания:", (object)10));
            Plugins.AntiHack.needKick = Convert.ToBoolean(this.GetConfig("Основное", "Наказать киком:", (object)false));
            Plugins.AntiHack.needKickEndKill = Convert.ToBoolean(this.GetConfig("Основное", "TextureHack - Наказать киком и убить игрока:", (object)false));
            Plugins.AntiHack.needBan = Convert.ToBoolean(this.GetConfig("Основное", "Наказать баном:", (object)false));
            Plugins.AntiHack.enableFlyHackLog = Convert.ToBoolean(this.GetConfig("Основное", "Логировать детекты FlyHack:", (object)true));
            Plugins.AntiHack.enableFlyHackCar = Convert.ToBoolean(this.GetConfig("Основное", "Не логировать детекты в машине?", (object)true));
            Plugins.AntiHack.enableSpeedHackLog = Convert.ToBoolean(this.GetConfig("Основное", "Логировать детекты SpeedHack:", (object)true));
            Plugins.AntiHack.enableTextureHackLog = Convert.ToBoolean(this.GetConfig("Основное", "Логировать детекты TextureHack:", (object)false));
            Plugins.AntiHack.enableSpeedAttackLog = Convert.ToBoolean(this.GetConfig("Основное", "Логировать детекты на быстрое добывание:", (object)true));
            Plugins.AntiHack.enableWallHackAttackLog = Convert.ToBoolean(this.GetConfig("Основное", "Логировать детекты на WallHackAttack:", (object)true));
            Plugins.AntiHack.wallHackObjectsEnabled = Convert.ToBoolean(this.GetConfig("Экспериментальное", "Включить AntiESP на объекты (Внимание! Может нагружать сервер!)", (object)false));
            Plugins.AntiHack.wallHackPlayersEnabled = Convert.ToBoolean(this.GetConfig("Экспериментальное", "Включить AntiESP на людей (Внимание! Может сильно нагружать сервер!)", (object)false));
            if (!this.configChanged)
                return;
            this.SaveConfig();
            this.configChanged = false;
        }

        public static void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject<AntiHack.StoredData>("AntiHack_Detects", Plugins.AntiHack.db, false);
        }

        public void ShowDetects(BasePlayer player, string[] args)
        {
            string s = (string)null;
            if (args.Length == 2)
            {
                if (args[1] == "0")
                {
                    player.ChatMessage("Очищаем номер детекта для телепорта...");
                    this.currentAdminsLog.Remove(player);
                }
                else
                    s = args[1];
            }
            string user = args[0];
            AntiHack.Log log;
            if (user.Contains("765"))
            {
                ulong id;
                ulong.TryParse(args[0], out id);
                log = Plugins.AntiHack.db.logs.Find((Predicate<Log>)(x => (long)x.steamID == (long)id));
            }
            else
                log = Plugins.AntiHack.db.logs.Find((Predicate<Log>)(x => x.name.Contains(user, CompareOptions.IgnoreCase)));
            if (log == null)
            {
                player.ChatMessage("Ошибка. В логах нет такого игрока");
            }
            else
            {
                AntiHack.CurrentLog currentLog;
                if (!this.currentAdminsLog.TryGetValue(player, out currentLog))
                {
                    this.currentAdminsLog[player] = new AntiHack.CurrentLog()
                    {
                        detect = 1,
                        steamID = log.steamID
                    };
                    player.ChatMessage(string.Format("Игрок {0}\nКоличество детектов: {1}", (object)log.name, (object)log.detectsAmount));
                }
                else if ((long)currentLog.steamID != (long)log.steamID)
                {
                    this.currentAdminsLog[player] = new AntiHack.CurrentLog()
                    {
                        detect = 1,
                        steamID = log.steamID
                    };
                    player.ChatMessage(string.Format("Игрок {0}\nКоличество детектов: {1}", (object)log.name, (object)log.detectsAmount));
                }
                else if (s == null)
                {
                    if (log.detectsAmount >= currentLog.detect + 1)
                    {
                        ++currentLog.detect;
                    }
                    else
                    {
                        player.ChatMessage(string.Format("Больше детектов у игрока {0} нет", (object)log.name));
                        this.currentAdminsLog.Remove(player);
                        return;
                    }
                }
                int result = 0;
                int.TryParse(s, out result);
                bool flag = false;
                for (int index = 0; index < log.detects.Count; ++index)
                {
                    AntiHack.Detect detect = log.detects[index];
                    if (result == 0)
                    {
                        if (this.currentAdminsLog[player].detect == index + 1)
                        {
                            foreach (AntiHack.Coordinates coordinate in detect.coordinates)
                            {
                                Vector3 vector3_1 = coordinate.startPos.ToVector3();
                                Vector3 vector3_2 = coordinate.endPos.ToVector3();
                                player.SendConsoleCommand("ddraw.arrow", (object)20f, (object)Color.white, (object)vector3_1, (object)vector3_2, (object)0.2f);
                                if (!flag)
                                {
                                    player.Teleport(vector3_1);
                                    flag = true;
                                    player.ChatMessage(string.Format("Телепорт на детект {0} игрока {1}", (object)this.currentAdminsLog[player].detect, (object)log.name));
                                }
                            }
                        }
                    }
                    else if (result == index + 1)
                    {
                        foreach (AntiHack.Coordinates coordinate in detect.coordinates)
                        {
                            Vector3 vector3_1 = coordinate.startPos.ToVector3();
                            Vector3 vector3_2 = coordinate.endPos.ToVector3();
                            player.SendConsoleCommand("ddraw.arrow", (object)20f, (object)Color.white, (object)vector3_1, (object)vector3_2, (object)0.2f);
                            if (!flag)
                            {
                                player.Teleport(vector3_1);
                                flag = true;
                                player.ChatMessage(string.Format("Телепорт на детект {0} игрока {1}", (object)result, (object)log.name));
                                this.currentAdminsLog[player].detect = result;
                            }
                        }
                    }
                }
            }
        }
		void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity == null || !(entity is BasePlayer) || item == null || dispenser == null) return;
            if (entity.ToPlayer() is BasePlayer);
                
        }
        public static void LogHandler(BasePlayer player, Vector3 lastGroundPos, AntiHack.TemporaryCoordinates temp, bool isSpeedHack = false)
        {
            AntiHack.Log log1 = Plugins.AntiHack.db.logs.Find((Predicate<Log>)(x => x.steamID == player.userID.Get()));
            Vector3 position = player.transform.position;
            if (isSpeedHack)
            {
                position.y += 0.7f;
                lastGroundPos.y += 0.7f;
            }
            AntiHack.Coordinates coordinates;
            coordinates.startPos = lastGroundPos.ToString();
            coordinates.endPos = position.ToString();
            AntiHack.Detect detect = new AntiHack.Detect();
            if (log1 == null)
            {
                AntiHack.Log log2 = new AntiHack.Log();
                log2.detectsAmount = 1;
                if (temp.coordinates.Count > 0)
                {
                    detect.coordinates.AddRange((IEnumerable<AntiHack.Coordinates>)temp.coordinates);
                    log2.detects.Add(detect);
                }
                else if (isSpeedHack)
                {
                    detect.coordinates.Add(coordinates);
                    log2.detects.Add(detect);
                }
                log2.name = player.displayName;
                log2.steamID = player.userID;
                Plugins.AntiHack.db.logs.Add(log2);
            }
            else
            {
                ++log1.detectsAmount;
                if (temp.coordinates.Count > 0)
                {
                    detect.coordinates.AddRange((IEnumerable<AntiHack.Coordinates>)temp.coordinates);
                    log1.detects.Add(detect);
                }
                else if (isSpeedHack)
                {
                    detect.coordinates.Add(coordinates);
                    log1.detects.Add(detect);
                }
                log1.name = player.displayName;
            }
        }

        [HookMethod("ShowLog")]
        private void ShowLog(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                    return;
            if (!Plugins.AntiHack.CanGetReport(player))
                return;
            if (args.Length == 0)
            {
                if (!Plugins.AntiHack.debug)
                {
                    AntiHack.HackHandler component = player.GetComponent<AntiHack.HackHandler>();
                    component.lastGroundPosition = player.transform.position;
                    component.playerPreviousPosition = player.transform.position;
                    Plugins.AntiHack.debug = true;
                    player.ChatMessage("Админ дебаг включен. Вас детектит.");
                }
                else
                {
                    Plugins.AntiHack.debug = false;
                    player.ChatMessage("Админ дебаг выключен. Вас не детектит.");
                }
            }
            else
                this.ShowDetects(player, args);
        }

        private static HashSet<BaseEntity> GetEntitiesFromAllChunks()
        {
            HashSet<BaseEntity> baseEntitySet = new HashSet<BaseEntity>();
            foreach (AntiHack.Chunk chunks in Plugins.AntiHack.chunksList)
            {
                foreach (BaseEntity entity in chunks.entities)
                {
                    if (!((UnityEngine.Object)entity == (UnityEngine.Object)null) && !entity.IsDestroyed)
                        baseEntitySet.Add(entity);
                }
            }
            return baseEntitySet;
        }

        private static HashSet<BaseEntity> GetEntitiesFromChunksNearPointOptimized(Vector3 point)
        {
            AntiHack.Chunk chunkFromPoint = Plugins.AntiHack.GetChunkFromPoint(point);
            HashSet<BaseEntity> baseEntitySet = new HashSet<BaseEntity>();
            if (chunkFromPoint == null)
                return baseEntitySet;
            foreach (BaseEntity entity in chunkFromPoint.entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x + 1][chunkFromPoint.z + 1].entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x - 1][chunkFromPoint.z - 1].entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x][chunkFromPoint.z + 1].entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x + 1][chunkFromPoint.z].entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x - 1][chunkFromPoint.z].entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x][chunkFromPoint.z - 1].entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x - 1][chunkFromPoint.z + 1].entities)
                baseEntitySet.Add(entity);
            foreach (BaseEntity entity in Plugins.AntiHack.chunks[chunkFromPoint.x + 1][chunkFromPoint.z - 1].entities)
                baseEntitySet.Add(entity);
            return baseEntitySet;
        }

        private static AntiHack.Chunk GetChunkFromPoint(Vector3 point)
        {
            Dictionary<int, AntiHack.Chunk> dictionary;
            AntiHack.Chunk chunk;
            if (Plugins.AntiHack.chunks.TryGetValue((int)((double)point.x / (double)Plugins.AntiHack.radius), out dictionary) && dictionary.TryGetValue((int)((double)point.z / (double)Plugins.AntiHack.radius), out chunk))
                return chunk;
            return (AntiHack.Chunk)null;
        }

        private void SetPlayer(BasePlayer player)
        {
            AntiHack.HackHandler hackHandler = player.gameObject.AddComponent<AntiHack.HackHandler>() ?? player.GetComponent<AntiHack.HackHandler>();
            Plugins.AntiHack.playersHandlers[player.userID] = hackHandler;
            this.lastSpeedAttackAttackTime[player.userID] = UnityEngine.Time.realtimeSinceStartup;
            this.speedAttackDetections[player.userID] = 0;
            this.shootingThroughWallDetections[player.userID] = 0;
            this.lastShootingThroughWallTime[player.userID] = UnityEngine.Time.realtimeSinceStartup;
        }

        public static bool CanGetReport(BasePlayer player)
        {
            return Interface.Oxide.GetLibrary<Permission>((string)null).UserHasPermission(player.userID.ToString(), "antihack.logs") || player.IsAdmin;
        }

        private static void SendReportToOnlineModerators(string report)
        {
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (Plugins.AntiHack.CanGetReport(activePlayer))
                    activePlayer.ChatMessage(string.Format("[AntiHack] {0}", (object)report));
            }
        }

        public static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp || activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                Networkable net = activePlayer.net;
                if ((net != null ? net.connection : (Network.Connection)null) != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrIdOrIp || sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return (BasePlayer)null;
        }

        private void ShootingThroughWallHanlder(BasePlayer attacker, HitInfo info, float timeNow)
        {
            BaseEntity hitEntity = info.HitEntity;
            if ((UnityEngine.Object)hitEntity == (UnityEngine.Object)null || (UnityEngine.Object)(hitEntity as BasePlayer) == (UnityEngine.Object)null)
                return;
            Vector3 hitPositionWorld = info.HitPositionWorld;
            Vector3 pointStart = info.PointStart;
            if (!UnityEngine.Physics.Linecast(pointStart, hitPositionWorld, Plugins.AntiHack.cM))
            {
                if (this.shootingThroughWallDetections[attacker.userID] == 0 || (double)timeNow - (double)this.lastShootingThroughWallTime[attacker.userID] <= 10.0)
                    return;
                this.shootingThroughWallDetections[attacker.userID] = 0;
            }
            else
            {
                this.lastShootingThroughWallTime[attacker.userID] = timeNow;
                Dictionary<ulong, int> throughWallDetections = this.shootingThroughWallDetections;
                ulong userId = attacker.userID;
                long num1 = (long)userId;
                int num2 = throughWallDetections[(ulong)num1];
                long num3 = (long)userId;
                int num4 = num2 + 1;
                throughWallDetections[(ulong)num3] = num4;
                if (this.shootingThroughWallDetections[attacker.userID] <= 5)
                    return;
                int averagePing = Network.Net.sv.GetAveragePing(attacker.net.connection);
                string str = string.Format("WallHackAttack Detected\n{0} [{1}]\n{2} -> {3}\nПинг: {4} мс.\nПредупреждений: {5}", (object)attacker.displayName, (object)attacker.userID, (object)pointStart, (object)hitPositionWorld, (object)averagePing, (object)this.shootingThroughWallDetections[attacker.userID]);
                string strMessage = string.Format("WallHackAttack | {0} [{1}] | {2} -> {3} | {4} мс. | Предупреждений: {5}", (object)attacker.displayName, (object)attacker.userID, (object)pointStart, (object)hitPositionWorld, (object)averagePing, (object)this.shootingThroughWallDetections[attacker.userID]);
                instance.LogToFile("Log", strMessage, instance);
                Plugins.AntiHack.SendReportToOnlineModerators(str);
                Interface.Oxide.LogError(str);
            }
        }

        private void SpeedAttackHandler(BasePlayer attacker, HitInfo info, BaseMelee melee, float timeNow)
        {
            if (attacker.IsAdmin)
                return;
            if ((double)timeNow - (double)this.lastSpeedAttackAttackTime[attacker.userID] < (double)melee.repeatDelay - 0.25)
            {
                info.damageTypes = new DamageTypeList();
                info.HitEntity = (BaseEntity)null;
                Dictionary<ulong, int> attackDetections = this.speedAttackDetections;
                ulong userId = attacker.userID;
                long num1 = (long)userId;
                int num2 = attackDetections[(ulong)num1];
                long num3 = (long)userId;
                int num4 = num2 + 1;
                attackDetections[(ulong)num3] = num4;
                if (this.speedAttackDetections[attacker.userID] > 5)
                {
                    int averagePing = Network.Net.sv.GetAveragePing(attacker.net.connection);
                    string str = string.Format("SpeedGather Detected\n{0} [{1}]\nПозиция: {2}\nПинг: {3} мс.\nПредупреждений: {4}", (object)attacker.displayName, (object)attacker.userID, (object)attacker.transform.position, (object)averagePing, (object)this.speedAttackDetections[attacker.userID]);
                    string strMessage = string.Format("SpeedGather | {0} [{1}] | {2} | {3} мс. | Предупреждений: {4}", (object)attacker.displayName, (object)attacker.userID, (object)attacker.transform.position, (object)averagePing, (object)this.speedAttackDetections[attacker.userID]);
                    instance.LogToFile("Log", strMessage, instance);
                    Plugins.AntiHack.SendReportToOnlineModerators(str);
                    Interface.Oxide.LogError(str);
                }
            }
            else
                this.speedAttackDetections[attacker.userID] = 0;
            this.lastSpeedAttackAttackTime[attacker.userID] = timeNow;
        }

        [HookMethod("CanNetworkTo")]
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            if (!Plugins.AntiHack.wallHackPlayersEnabled || !this.isLoaded || (!Plugins.AntiHack.playersHidenEntities.ContainsKey(target.userID) || !Plugins.AntiHack.playersHidenEntities[target.userID].Contains(entity.net.ID.Value)))
                return (object)null;
            return (object)false;
        }

        [HookMethod("OnEntitySpawned")]
        private void OnEntitySpawned(BaseEntity ent, GameObject gameObject)
        {
            if (!Plugins.AntiHack.wallHackObjectsEnabled || (UnityEngine.Object)(ent as BasePlayer) != (UnityEngine.Object)null || (UnityEngine.Object)ent.GetComponent<LootContainer>() != (UnityEngine.Object)null || (UnityEngine.Object)ent.GetComponent<StorageContainer>() == (UnityEngine.Object)null && !this.neededEntities.Contains(ent.ShortPrefabName))
                return;
            AntiHack.Chunk chunkFromPoint = Plugins.AntiHack.GetChunkFromPoint(ent.transform.position);
            if (chunkFromPoint == null)
                return;
            chunkFromPoint.entities.Add(ent);
        }

        [HookMethod("OnEntityKill")]
        private void OnEntityKill(BaseNetworkable ent)
        {
            if (!Plugins.AntiHack.wallHackObjectsEnabled || !this.isLoaded || (UnityEngine.Object)(ent as BasePlayer) != (UnityEngine.Object)null || (UnityEngine.Object)ent.GetComponent<LootContainer>() != (UnityEngine.Object)null || (UnityEngine.Object)ent.GetComponent<StorageContainer>() == (UnityEngine.Object)null && !this.neededEntities.Contains(ent.ShortPrefabName))
                return;
            AntiHack.Chunk chunkFromPoint = Plugins.AntiHack.GetChunkFromPoint(ent.transform.position);
            if (chunkFromPoint == null || !chunkFromPoint.entities.Contains(ent as BaseEntity))
                return;
            chunkFromPoint.entities.Remove(ent as BaseEntity);
        }

        [HookMethod("Init")]
        private void Init()
        {
            this.LoadVariables();
            if (Plugins.AntiHack.wallHackObjectsEnabled)
            {
                Plugins.AntiHack.radius = ConVar.Server.worldsize / 100;
                int num = 100;
                for (int index1 = num * -1; index1 < num; ++index1)
                {
                    Plugins.AntiHack.chunks[index1] = new Dictionary<int, Chunk>();
                    for (int index2 = num * -1; index2 < num; ++index2)
                    {
                        AntiHack.Chunk chunk = new AntiHack.Chunk()
                        {
                            x = index1,
                            z = index2
                        };
                        Plugins.AntiHack.chunks[index1][index2] = chunk;
                        Plugins.AntiHack.chunksList.Add(chunk);
                    }
                }
                if (Plugins.AntiHack.debug)
                    Puts(string.Format("[Debug WallHackHandler] Chunks: {0} Radius: {1} Map size: {2}", (object)Plugins.AntiHack.chunks.Count, (object)Plugins.AntiHack.radius, (object)ConVar.Server.worldsize));
            }
            Plugins.AntiHack.db = Interface.GetMod().DataFileSystem.ReadObject<AntiHack.StoredData>("AntiHack_Detects");
            Plugins.AntiHack.instance = this;
            Interface.Oxide.GetLibrary<Permission>((string)null).RegisterPermission("antihack.logs", (Plugin)this);
            Interface.Oxide.GetLibrary<Oxide.Game.Rust.Libraries.Command>((string)null).AddChatCommand("ah", (Plugin)this, "ShowLog");
            Interface.Oxide.GetLibrary<Oxide.Game.Rust.Libraries.Command>((string)null).AddConsoleCommand("antihack", (Plugin)this, "AntiHackCmd");
        }

        [HookMethod("OnServerInitialized")]
        private void OnServerInitialized()
        {
            if (Plugins.AntiHack.wallHackObjectsEnabled)
            {
                int num = 0;
                foreach (BaseNetworkable serverEntity in BaseNetworkable.serverEntities)
                {
                    if (!((UnityEngine.Object)(serverEntity as BasePlayer) != (UnityEngine.Object)null) && ((!((UnityEngine.Object)serverEntity.GetComponent<StorageContainer>() == (UnityEngine.Object)null) || this.neededEntities.Contains(serverEntity.ShortPrefabName)) && !((UnityEngine.Object)serverEntity.GetComponent<LootContainer>() != (UnityEngine.Object)null)))
                    {
                        AntiHack.Chunk chunkFromPoint = Plugins.AntiHack.GetChunkFromPoint(serverEntity.transform.position);
                        if (chunkFromPoint != null)
                            chunkFromPoint.entities.Add(serverEntity as BaseEntity);
                        ++num;
                    }
                }
                if (Plugins.AntiHack.debug)
                    Interface.Oxide.LogInfo(string.Format("[Debug WallHackHandler] Added new {0} entities ({1} all)", (object)num, (object)Plugins.AntiHack.GetEntitiesFromAllChunks().Count));
            }
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                this.SetPlayer(activePlayer);
            this.isLoaded = true;
        }

        [HookMethod("OnPlayerDisconnected")]
        private void OnPlayerDisconnected(BasePlayer player)
        {
            AntiHack.HackHandler component = player.GetComponent<AntiHack.HackHandler>();
            if (component == null)
                return;
            component.Disconnect();
        }

        [HookMethod("OnServerSave")]
        private void OnServerSave()
        {
            Plugins.AntiHack.isSaving = true;
            Interface.Oxide.NextTick((Action)(() => Plugins.AntiHack.isSaving = false));
        }

        [HookMethod("OnPlayerInit")]
        private void OnPlayerInit(BasePlayer player)
        {
            this.SetPlayer(player);
        }

        [HookMethod("OnServerShutdown")]
        private void OnServerShutdown()
        {
            Plugins.AntiHack.SaveData();
        }
        
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                Plugins.AntiHack.playersHandlers.Remove(player.userID);
                AntiHack.HackHandler component = player.GetComponent<AntiHack.HackHandler>();
                if (component != null)
                {
                    component.Disconnect();
                }
            }
            Plugins.AntiHack.SaveData();
            DestroyAll<AntiHack.HackHandler>();
        }

        void DestroyAll<T>()
        {
            UnityEngine.Object[] objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (UnityEngine.Object gameObj in objects)
                    GameObject.Destroy(gameObj);
            
        }

        [HookMethod("OnPlayerAttack")]
        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (Plugins.AntiHack.isSaving || (UnityEngine.Object)attacker == (UnityEngine.Object)null || (attacker.IsAdmin || (UnityEngine.Object)info.Weapon == (UnityEngine.Object)null))
                return;
            float realtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup;
            BaseMelee component = info.Weapon.GetComponent<BaseMelee>();
            if ((UnityEngine.Object)component == (UnityEngine.Object)null && Plugins.AntiHack.enableWallHackAttackLog)
            {
                this.ShootingThroughWallHanlder(attacker, info, realtimeSinceStartup);
            }
            else
            {
                if (!Plugins.AntiHack.enableSpeedAttackLog)
                    return;
                this.SpeedAttackHandler(attacker, info, component, realtimeSinceStartup);
            }
        }

        private bool IsPlayerGotImmunity(ulong id)
        {
            object obj = Interface.Oxide.CallHook("AntiHackIsPlayerGotImmunity", (object)id);
            return obj != null && (bool)obj;
        }

        private class Chunk
        {
            public HashSet<BaseEntity> entities = new HashSet<BaseEntity>();

            public int x { get; set; }

            public int z { get; set; }
        }

        private class Log
        {
            public List<AntiHack.Detect> detects = new List<AntiHack.Detect>();
            public int detectsAmount;
            public ulong steamID;
            public string name;
        }

        public struct Coordinates
        {
            public string startPos;
            public string endPos;
        }

        public class Detect
        {
            public List<AntiHack.Coordinates> coordinates = new List<AntiHack.Coordinates>();
        }

        public class TemporaryCoordinates
        {
            public List<AntiHack.Coordinates> coordinates = new List<AntiHack.Coordinates>();
        }

        private class StoredData
        {
            public List<AntiHack.Log> logs = new List<AntiHack.Log>();
        }

        private class CurrentLog
        {
            public ulong steamID;
            public int detect;
        }

        private class HackHandler : MonoBehaviour
        {
            private int flyWarnings = 0;
            private int textureWarnings = 0;
            private int speedWarnings = 0;
            private float ownTickRate = 0.1f;
            private bool IsFlying = false;
            private int falseFlyDetects = 0;
            private int falseSpeedDetects = 0;
            private int ping = 0;
            private AntiHack.TemporaryCoordinates temp = new AntiHack.TemporaryCoordinates();
            public HashSet<BaseEntity> hidedEntities = new HashSet<BaseEntity>();
            public Dictionary<ulong, HashSet<BaseEntity>> hidedPlayersEntities = new Dictionary<ulong, HashSet<BaseEntity>>();
            public HashSet<BaseEntity> seenObjects = new HashSet<BaseEntity>();
            public Vector3 lastPosition = new Vector3();
            private bool IsHided = false;
            private bool isShowedAll = false;
            public BasePlayer player;
            private float lastTick;
            private float deltaTime;
            private float flyTime;
            private float flyTimeStart;
            public Vector3 lastGroundPosition;
            public Vector3 playerPreviousPosition;
            private Network.Connection connection;

            private void Awake()
            {
                this.player = this.GetComponent<BasePlayer>();
                this.ownTickRate = UnityEngine.Random.Range(0.09f, 0.11f);
                Plugins.AntiHack.playersHidenEntities[this.player.userID] = new HashSet<ulong>();
                this.connection = this.player.net.connection;
                this.lastGroundPosition = this.player.transform.position;
                this.playerPreviousPosition = this.player.transform.position;
                if (!Plugins.AntiHack.wallHackObjectsEnabled)
                    return;
                if (this.player.IsReceivingSnapshot || this.player.IsSleeping())
                    this.CheckSnapshot();
                else
                    this.HideAll();
            }

            private void Update()
            {
                if ((double)UnityEngine.Time.realtimeSinceStartup - (double)this.lastTick < (double)this.ownTickRate)
                    return;
                if (Plugins.AntiHack.instance.IsPlayerGotImmunity(this.player.userID))
                {
                    this.ShowAllEntities();
                    this.lastPosition = this.player.GetNetworkPosition();
                    this.playerPreviousPosition = this.player.transform.position;
                    this.lastGroundPosition = this.player.transform.position;
                }
                else
                {
                    this.isShowedAll = false;
                    if (this.player.IsAdmin && !Plugins.AntiHack.debug)
                    {
                        this.WallHackHandler();
                        this.lastPosition = this.player.GetNetworkPosition();
                        this.playerPreviousPosition = this.player.transform.position;
                    }
                    else
                    {
                        this.CorrectValues();
                        this.lastPosition = this.player.GetNetworkPosition();
                        this.FlyHackHandler();
                        this.PlayerWallHackHandler();
                        this.PlayerWallHackHandlerSleepers();
                        this.SpeedHackHandler();
                        this.WallHackHandler();
                        if (this.player.IsSleeping() || this.player.IsDead())
                        {
                            this.playerPreviousPosition = this.player.transform.position;
                            this.lastGroundPosition = this.player.transform.position;
                        }
                        else
                        {
                            this.TextureHackHandler();
                            this.playerPreviousPosition = this.player.transform.position;
                        }
                    }
                }
            }

            private void SpeedHackHandler()
            {
                if (this.player.IsOnGround())
                {
                    if (Plugins.AntiHack.enableFlyHackCar && player.GetMounted() != null) return;
                    Vector3 position = this.player.transform.position;
                    if ((double)this.playerPreviousPosition.y - (double)position.y < 0.5)
                    {
                        float num = Vector3Ex.Distance2D(this.playerPreviousPosition, position);
                        float maxSpeed = (float)(((double)this.player.GetMaxSpeed() + 1.0) * (double)Plugins.AntiHack.tickRate * (double)this.deltaTime * 1.54999995231628);
                        if ((double)num > (double)maxSpeed)
                        {
                            this.falseSpeedDetects = this.falseSpeedDetects + 1;
                            if (this.falseSpeedDetects <= 5)
                                return;
                            this.falseSpeedDetects = 0;
                            this.speedWarnings = this.speedWarnings + 1;
                            if (Plugins.AntiHack.enableSpeedHackLog)
                            {
                                this.CreateLogSpeedHack(position, maxSpeed);
                                Plugins.AntiHack.LogHandler(this.player, this.playerPreviousPosition, this.temp, true);
                            }
                            this.ReturnBack(this.playerPreviousPosition);
                            if (this.speedWarnings >= Plugins.AntiHack.maxSpeedWarnings)
                                this.CrimeHandler("SpeedHack");
                            return;
                        }
                    }
                }
                this.falseSpeedDetects = 0;
            }
			public List<Blacklist> list = new List<Blacklist>();
			public class Blacklist
			{
				public Blacklist(string Player, string SteamId)
				{
					this.Player = Player;
					this.SteamId = SteamId;
				}
				public string Player { get; set; }
				public string SteamId { get; set; }


			}
            private void FlyHackHandler()
            {
                Vector3 position = this.player.transform.position;
                if (this.player.IsOnGround() || (double)this.player.WaterFactor() > 0.0)
                {
                    this.lastGroundPosition = position;
                    this.Reset();
                    this.falseFlyDetects = 0;
                }
                else
                {
                    if (!this.IsFlying)
                    {
                        this.flyTimeStart = UnityEngine.Time.realtimeSinceStartup;
                        this.IsFlying = true;
                    }
                    this.flyTime = this.lastTick - this.flyTimeStart;
                    this.AddTemp();
                    if ((double)this.flyTime < 0.600000023841858 && (double)position.y - (double)this.lastGroundPosition.y < 3.0)
                        return;
                    float num1 = Vector3.Distance(position, this.lastGroundPosition);
                    float num2 = Vector3Ex.Distance2D(position, this.lastGroundPosition);
                    if ((double)num1 > 1.20000004768372 * (double)this.deltaTime && ((double)position.y - (double)this.lastGroundPosition.y > 1.20000004768372 || (double)num2 > 15.0) && (((double)this.playerPreviousPosition.y < (double)position.y || (double)num2 > 15.0) && (double)num1 > (double)Vector3.Distance(this.playerPreviousPosition, this.lastGroundPosition) && !UnityEngine.Physics.Raycast(position, Vector3.down, 1.2f)))
                    {
                        this.falseFlyDetects = this.falseFlyDetects + 1;
                        if (this.falseFlyDetects <= 5)
                            return;
                        this.falseFlyDetects = 0;
                        this.flyWarnings = this.flyWarnings + 1;
                        if (Plugins.AntiHack.enableFlyHackLog)
                        {
                            Plugins.AntiHack.LogHandler(this.player, this.lastGroundPosition, this.temp, false);
                            this.CreateLogFlyHack(position);
                        }
                        this.ReturnBack(this.lastGroundPosition);
                        if (this.flyWarnings >= maxFlyWarnings)
                            this.CrimeHandler("FlyHack");
                    }
                    else
                        this.falseFlyDetects = 0;
                }
            }

            private void TextureHackHandler()
            {
                Vector3 position = this.player.transform.position;
                foreach (RaycastHit hit in UnityEngine.Physics.RaycastAll(new Ray(position + Vector3.up * 10f, Vector3.down), 50f, Plugins.AntiHack.globalMask))
                {
                    if (!((UnityEngine.Object)hit.collider == (UnityEngine.Object)null))
                    {
                        if ((UnityEngine.Object)hit.GetEntity() != (UnityEngine.Object)null)
                        {
                            BaseEntity entity = hit.GetEntity();
                            if (this.IsInsideFoundation(entity))
                            {
                                this.textureWarnings = this.textureWarnings + 1;
                                if (Plugins.AntiHack.enableTextureHackLog && this.textureWarnings % 50 == 0)
                                {
                                    this.CreateLogTextureHack(position, entity.ShortPrefabName);
                                }
                                
                                this.ReturnBack(this.playerPreviousPosition);
                                break;
                            }
                        }
                        if ((!(hit.collider.name != "Mesh") || hit.collider.name.Contains("rock_small") || hit.collider.name.Contains("ores")) && this.IsInsideCave(hit.collider))
                        {
                            string objectName = hit.collider.name;
                            if (objectName == "Mesh")
                                objectName = "Rock";
                            this.textureWarnings = this.textureWarnings + 1;
                            if (Plugins.AntiHack.enableTextureHackLog && this.textureWarnings % 20 == 0)
                            {
                                if (objectName.Contains("assets") && objectName.Length > 23)
                                    objectName = objectName.Remove(0, 23);
                                this.CreateLogTextureHack(position, objectName);
                            }
                            this.ReturnBack(this.playerPreviousPosition);
                            break;
                        }
                    }
                }
            }

            private bool IsInsideFoundation(BaseEntity block)
            {
                BuildingBlock buildingBlock = block as BuildingBlock;
                if ((UnityEngine.Object)buildingBlock != (UnityEngine.Object)null)
                {
                    if (!buildingBlock.PrefabName.Contains("foundation") || buildingBlock.PrefabName.Contains("foundation.steps") && buildingBlock.grade != BuildingGrade.Enum.TopTier || (buildingBlock.grade == BuildingGrade.Enum.Twigs || buildingBlock.grade == BuildingGrade.Enum.Wood))
                        return false;
                }
                else if (!block.PrefabName.Contains("wall.external"))
                    return false;
                OBB obb = block.WorldSpaceBounds();
                Vector3 center1 = obb.ToBounds().center;
                obb = this.player.WorldSpaceBounds();
                Vector3 center2 = obb.ToBounds().center;
                center2.y -= 0.7f;
                Vector3 direction = center1 - center2;
                RaycastHit hitInfo;
                return !UnityEngine.Physics.Raycast(new Ray(center2, direction), out hitInfo, direction.magnitude + 1f, Plugins.AntiHack.cM);
            }

            private bool IsInsideCave(Collider collider)
            {
                Vector3 center1 = collider.bounds.center;
                Vector3 center2 = this.player.WorldSpaceBounds().ToBounds().center;
                Vector3 direction = center1 - center2;
                Ray ray = new Ray(center2, direction);
                RaycastHit hitInfo;
                return !collider.Raycast(ray, out hitInfo, direction.magnitude + 1f);
            }

            private void CheckSnapshot()
            {
                if (this.player.IsReceivingSnapshot || this.player.IsSleeping())
                    this.Invoke("CheckSnapshot", 0.1f);
                else
                    this.HideAll();
            }

            public void HideAll()
            {
                if (this.IsHided)
                    return;
                this.IsHided = true;
                foreach (BaseEntity entitiesFromAllChunk in Plugins.AntiHack.GetEntitiesFromAllChunks())
                    this.Hide(entitiesFromAllChunk);
            }

            public void Hide(BaseEntity entity)
            {
                if (this.seenObjects.Contains(entity) || this.hidedEntities.Contains(entity))
                    return;
                NetWrite nw = Net.sv.StartWrite();

                {
                    nw.PacketID(Network.Message.Type.EntityDestroy);
                    nw.EntityID(entity.net.ID);
                    nw.UInt8((byte)0);
                    nw.Send(new SendInfo(this.connection));
                }
                this.hidedEntities.Add(entity);
            }

            private void Show(BaseEntity entity, bool needRemove = true)
            {
                this.seenObjects.Add(entity);
                if (!this.hidedEntities.Contains(entity))
                    return;
                if (needRemove)
                    this.hidedEntities.Remove(entity);
                this.player.QueueUpdate(BasePlayer.NetworkQueue.Update, (BaseNetworkable)entity);
            }

            private void ShowLines(Vector3 start, Vector3 target, bool isVisible)
            {
                if (!this.player.IsAdmin)
                    return;
                if (isVisible)
                    this.player.SendConsoleCommand("ddraw.arrow", (object)0.1f, (object)Color.blue, (object)start, (object)target, (object)0.1);
                else
                    this.player.SendConsoleCommand("ddraw.arrow", (object)0.1f, (object)Color.red, (object)start, (object)target, (object)0.1);
            }

            private bool TryLineCast(Vector3 start, Vector3 target, float plusTarget = 0.0f, float plusPlayer = 1.5f)
            {
                target.y += plusTarget;
                start.y += plusPlayer;
                return !UnityEngine.Physics.Linecast(start, target, Plugins.AntiHack.cM);
            }

            private bool IsObjectVisible(Vector3 start, Vector3 target)
            {
                return this.TryLineCast(start, target, 0.0f, 1.5f) || (double)Vector3.Distance(start, target) <= 25.0 && (this.TryLineCast(start, target, 0.0f, 0.5f) || this.TryLineCast(start, target, 0.5f, 0.5f) || this.TryLineCast(start, target, 0.5f, 1f));
            }

            private void WallHackHandler()
            {
                if (!Plugins.AntiHack.wallHackObjectsEnabled)
                    return;
                Vector3 position = this.player.transform.position;
                if ((double)Vector3.Distance(this.player.transform.position, this.playerPreviousPosition) < 1.0 / 1000.0)
                    return;
                foreach (BaseEntity entity in Plugins.AntiHack.GetEntitiesFromChunksNearPointOptimized(position))
                {
                    if (!((UnityEngine.Object)entity == (UnityEngine.Object)null) && !this.seenObjects.Contains(entity))
                    {
                        entity.WorldSpaceBounds().ToBounds();
                        if (this.IsObjectVisible(position, entity.WorldSpaceBounds().ToBounds().center))
                            this.Show(entity, true);
                    }
                }
            }

            private void PlayerWallHackHandlerSleepers()
            {
                if (!Plugins.AntiHack.wallHackPlayersEnabled || (double)Vector3.Distance(this.player.transform.position, this.playerPreviousPosition) < 1.0 / 1000.0)
                    return;
                foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
                {
                    if (!((UnityEngine.Object)sleepingPlayer == (UnityEngine.Object)this.player) && ((double)this.player.Distance((BaseEntity)sleepingPlayer) >= (double)Plugins.AntiHack.minPlayersWallHackDistanceCheck && (double)this.player.Distance((BaseEntity)sleepingPlayer) <= (double)Plugins.AntiHack.maxPlayersWallHackDistanceCheck && !this.seenObjects.Contains((BaseEntity)sleepingPlayer)))
                    {
                        if (!this.IsVisible((BaseEntity)sleepingPlayer, true))
                            this.HidePlayer(sleepingPlayer, true);
                        else
                            this.ShowPlayer(sleepingPlayer, true, true);
                    }
                }
            }

            private void PlayerWallHackHandler()
            {
                if (!Plugins.AntiHack.wallHackPlayersEnabled)
                    return;
                foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                {
                    if (!((UnityEngine.Object)activePlayer == (UnityEngine.Object)this.player) && ((double)this.player.Distance((BaseEntity)activePlayer) >= (double)Plugins.AntiHack.minPlayersWallHackDistanceCheck && (double)this.player.Distance((BaseEntity)activePlayer) <= (double)Plugins.AntiHack.maxPlayersWallHackDistanceCheck && !activePlayer.net.connection.ipaddress.StartsWith("127.0")) && ((double)Vector3.Distance(Plugins.AntiHack.playersHandlers[activePlayer.userID].lastPosition, activePlayer.GetNetworkPosition()) < 1.0 / 1000.0 || (double)this.player.Distance((BaseEntity)activePlayer) > 50.0 || activePlayer.IsDucked()))
                    {
                        if (!this.IsVisible((BaseEntity)activePlayer, false))
                            this.HidePlayer(activePlayer, false);
                        else
                            this.ShowPlayer(activePlayer, true, false);
                    }
                }
            }

            private bool DoLine(Vector3 start, Vector3 target, float plusTarget = 0.0f, float plusPlayer = 1.5f)
            {
                target.y += plusTarget;
                start.y += plusPlayer;
                return !UnityEngine.Physics.Linecast(start, target, Plugins.AntiHack.cM);
            }

            private bool IsBehindStairs(Vector3 start, Vector3 target)
            {
                RaycastHit hitInfo;
                if (UnityEngine.Physics.Linecast(start, target, out hitInfo, Plugins.AntiHack.cM))
                {
                    BaseEntity entity1 = hitInfo.GetEntity();
                    if ((UnityEngine.Object)entity1 != (UnityEngine.Object)null && ((entity1.ShortPrefabName == "block.stair.lshape" || entity1.ShortPrefabName == "block.stair.ushape") && UnityEngine.Physics.Linecast(target, start, out hitInfo, Plugins.AntiHack.cM)))
                    {
                        BaseEntity entity2 = hitInfo.GetEntity();
                        if ((UnityEngine.Object)entity2 != (UnityEngine.Object)null && (entity2.ShortPrefabName == "block.stair.lshape" || entity2.ShortPrefabName == "block.stair.ushape"))
                            return true;
                    }
                }
                return false;
            }

            private bool IsVisible(BaseEntity target, bool isSleeper = false)
            {
                Vector3 position1 = this.player.transform.position;
                Vector3 position2 = target.transform.position;
                if (isSleeper)
                    return this.DoLine(position1, position2, 0.0f, 1.5f) || this.IsBehindStairs(new Vector3(position1.x, position1.y + 1.2f, position1.z), new Vector3(position2.x, position2.y + 1.2f, position2.z));
                if ((target as BasePlayer).IsDucked())
                    position2.y -= 0.5f;
                float num = this.player.Distance(target);
                if (this.DoLine(position1, position2, 1.5f, 1.5f) || this.IsBehindStairs(new Vector3(position1.x, position1.y + 1.2f, position1.z), new Vector3(position2.x, position2.y + 1.2f, position2.z)))
                    return true;
                if ((double)num > 120.0)
                    return false;
                if (this.DoLine(position1, position2, 0.0f, 1.5f) || this.DoLine(position1, position2, 1.2f, 1.5f) || (this.DoLine(position1, position2, 0.9f, 1.5f) || this.DoLine(position1, position2, 0.5f, 1.5f)) || (this.DoLine(position1, position2, 1.9f, 1.5f) || this.DoLine(position1, position2, 1.5f, 0.0f)))
                    return true;
                if ((double)num > 75.0)
                    return false;
                bool flag1 = !UnityEngine.Physics.Linecast(position1, Quaternion.Euler(this.player.GetNetworkRotation()) * Vector3.left + position1, Plugins.AntiHack.cM);
                bool flag2 = !UnityEngine.Physics.Linecast(position1, Quaternion.Euler(this.player.GetNetworkRotation()) * Vector3.right + position1, Plugins.AntiHack.cM);
                return flag1 && this.DoLine(Quaternion.Euler(this.player.GetNetworkRotation()) * Vector3.left + position1, position2, 1.1f, 1.5f) || flag2 && this.DoLine(Quaternion.Euler(this.player.GetNetworkRotation()) * Vector3.right + position1, position2, 1.1f, 1.5f) || (flag1 && this.DoLine(Quaternion.Euler(this.player.GetNetworkRotation()) * Vector3.left + position1, position2, 1.1f, 1.1f) || flag2 && this.DoLine(Quaternion.Euler(this.player.GetNetworkRotation()) * Vector3.right + position1, position2, 1.1f, 1.1f));
            }

            private void ShowPlayer(BasePlayer target, bool needRemove = true, bool isSleeper = false)
            {
                if (isSleeper)
                    this.seenObjects.Add((BaseEntity)target);
                if (!this.hidedPlayersEntities.ContainsKey(target.userID))
                    return;
                this.player.QueueUpdate(BasePlayer.NetworkQueue.Update, (BaseNetworkable)target);
                this.player.QueueUpdate(BasePlayer.NetworkQueue.Update, target != null ? (BaseNetworkable)target.GetHeldEntity() : (BaseNetworkable)null);
                Plugins.AntiHack.playersHidenEntities[this.player.userID].Remove(target.net.ID.Value);
                foreach (BaseEntity baseEntity in this.hidedPlayersEntities[target.userID])
                {
                    if (!((UnityEngine.Object)baseEntity == (UnityEngine.Object)null) && !baseEntity.IsDestroyed)
                    {
                        this.player.QueueUpdate(BasePlayer.NetworkQueue.Update, (BaseNetworkable)baseEntity);
                        Plugins.AntiHack.playersHidenEntities[this.player.userID].Remove(baseEntity.net.ID.Value);
                    }
                }
                if (needRemove)
                    this.hidedPlayersEntities.Remove(target.userID);
            }

            private void HidePlayer(BasePlayer target, bool isSleeper = false)
            {
                if (isSleeper)
                {
                    if (this.seenObjects.Contains((BaseEntity)target))
                        return;
                }
                else if (this.seenObjects.Contains((BaseEntity)target))
                    this.seenObjects.Remove((BaseEntity)target);
                if (this.hidedPlayersEntities.ContainsKey(target.userID))
                    return;
                this.hidedPlayersEntities[target.userID] = new HashSet<BaseEntity>();
                NetWrite nw = Net.sv.StartWrite();

                {
                    nw.PacketID(Network.Message.Type.EntityDestroy);
                    nw.EntityID(target.net.ID);
                    nw.UInt8((byte)0);
                    nw.Send(new SendInfo(this.connection));
                }
                Item activeItem = target.GetActiveItem();
                if ((activeItem != null ? (UnityEngine.Object)activeItem.GetHeldEntity() : (UnityEngine.Object)null) != (UnityEngine.Object)null && nw.Start())
                {
                    nw.PacketID(Network.Message.Type.EntityDestroy);
                    nw.EntityID(activeItem.GetHeldEntity().net.ID);
                    nw.UInt8((byte)0);
                    nw.Send(new SendInfo(this.connection));
                    this.hidedPlayersEntities[target.userID].Add(activeItem.GetHeldEntity());
                    Plugins.AntiHack.playersHidenEntities[this.player.userID].Add(activeItem.GetHeldEntity().net.ID.Value);
                }
                this.HidePlayersHostile(target);
                this.hidedPlayersEntities[target.userID].Add((BaseEntity)target);
                Plugins.AntiHack.playersHidenEntities[this.player.userID].Add(target.net.ID.Value);
            }

            private void HidePlayersHostile(BasePlayer target)
            {
                foreach (Item obj in target.inventory.containerBelt.itemList)
                {
                    if (target.IsHostileItem(obj))
                    {
                        if (!((obj != null ? (UnityEngine.Object)obj.GetHeldEntity() : (UnityEngine.Object)null) == (UnityEngine.Object)null))
                        {
                            NetWrite nw = Net.sv.StartWrite();

                            {
                                nw.PacketID(Network.Message.Type.EntityDestroy);
                                nw.EntityID(obj.GetHeldEntity().net.ID);
                                nw.UInt8((byte)0);
                                nw.Send(new SendInfo(this.connection));
                                this.hidedPlayersEntities[target.userID].Add(obj.GetHeldEntity());
                                Plugins.AntiHack.playersHidenEntities[this.player.userID].Add(obj.GetHeldEntity().net.ID.Value);
                            }
                        }
                        else
                            break;
                    }
                }
                foreach (Item obj in target.inventory.containerMain.itemList)
                {
                    if (target.IsHostileItem(obj))
                    {
                        if ((obj != null ? (UnityEngine.Object)obj.GetHeldEntity() : (UnityEngine.Object)null) == (UnityEngine.Object)null)
                            break;
                        NetWrite nw = Net.sv.StartWrite();

                        {
                            nw.PacketID(Network.Message.Type.EntityDestroy);
                            nw.EntityID(obj.GetHeldEntity().net.ID);
                            nw.UInt8((byte)0);
                            nw.Send(new SendInfo(this.connection));
                            this.hidedPlayersEntities[target.userID].Add(obj.GetHeldEntity());
                            Plugins.AntiHack.playersHidenEntities[this.player.userID].Add(obj.GetHeldEntity().net.ID.Value);
                        }
                    }
                }
            }

            private void ShowAllEntities()
            {
                if (this.isShowedAll)
                    return;
                this.isShowedAll = true;
                Plugins.AntiHack.playersHidenEntities[this.player.userID] = new HashSet<ulong>();
                HashSet<BaseEntity> hidedEntities = this.hidedEntities;
                foreach (KeyValuePair<ulong, HashSet<BaseEntity>> hidedPlayersEntity in this.hidedPlayersEntities)
                    hidedEntities.UnionWith((IEnumerable<BaseEntity>)hidedPlayersEntity.Value);
                foreach (BaseEntity baseEntity in hidedEntities)
                {
                    if (!((UnityEngine.Object)baseEntity == (UnityEngine.Object)null) && !baseEntity.IsDestroyed)
                        this.player.QueueUpdate(BasePlayer.NetworkQueue.Update, (BaseNetworkable)baseEntity);
                }
            }

            private void CorrectValues()
            {
                this.ping = Network.Net.sv.GetAveragePing(this.player.net.connection);
                if (this.ping == 0)
                    this.ping = 1;
                int frameRate = Performance.current.frameRate;
                float num = 1f;
                if (frameRate < 100)
                    num = 4f;
                if (frameRate < 50)
                    num = 6f;
                this.deltaTime = (float)(1.0 + (double)this.ping * 0.00400000018998981 + (((double)UnityEngine.Time.realtimeSinceStartup - (double)this.lastTick) * (double)num - (double)Plugins.AntiHack.tickRate));
                this.lastTick = UnityEngine.Time.realtimeSinceStartup;
            }

            private void ReturnBack(Vector3 pos)
            {
                this.player.MovePosition(pos);
                Networkable net1 = this.player.net;
                if ((net1 != null ? net1.connection : (Network.Connection)null) != null)
                    this.player.ClientRPCPlayer(null, this.player, "ForcePositionTo", pos);
                Networkable net2 = this.player.net;
                if ((net2 != null ? net2.connection : (Network.Connection)null) == null)
                    return;
                try
                {
                    this.player.ClearEntityQueue((Group)null);
                }
                catch
                {
                }
            }
            private void CreateLogFlyHack(Vector3 playerPosition)
            {
                string str = string.Format("FlyHack detected\n{0} [{1}]\nНачальная позиция: {2}\nКонечная позиция: {3}\nВремя в полете: {4} сек.\nДистанция: {5} м.\nПинг: {6} мс.\nПредупреждений: {7}", (object)this.player.displayName, (object)this.player.userID, (object)this.lastGroundPosition, (object)playerPosition, (object)string.Format("{0:0.##}", (object)this.flyTime), (object)string.Format("{0:0.##}", (object)Vector3.Distance(playerPosition, this.lastGroundPosition)), (object)this.ping, (object)this.flyWarnings);
                string strMessage = string.Format("FlyHack | {0} [{1}] | {2} -> {3} | Время: {4} сек. | Дистанция: {5} м. | {6} мс. | Предупреждений: {7}", (object)this.player.displayName, (object)this.player.userID, (object)this.lastGroundPosition, (object)playerPosition, (object)string.Format("{0:0.##}", (object)this.flyTime), (object)string.Format("{0:0.##}", (object)Vector3.Distance(playerPosition, this.lastGroundPosition)), (object)this.ping, (object)this.flyWarnings);
                Interface.Oxide.LogError(str);
                instance.LogToFile("Log", strMessage, instance);
                Plugins.AntiHack.SendReportToOnlineModerators(str);
                var reply = 79;
            }

            private void CreateLogSpeedHack(Vector3 playerPosition, float maxSpeed)
            {
                string str = string.Format("SpeedHack detected\n{0} [{1}]\nНачальная позиция: {2}\nКонечная позиция: {3}\nСкорость: {4} м/c (Максимально допустимая: {5} м/c).\nПинг: {6} мс.\nПредупреждений: {7}", (object)this.player.displayName, (object)this.player.userID, (object)this.playerPreviousPosition, (object)playerPosition, (object)string.Format("{0:0.##}", (object)(float)((double)Vector3.Distance(playerPosition, this.playerPreviousPosition) * 5.0)), (object)string.Format("{0:0.##}", (object)(float)((double)maxSpeed * 5.0)), (object)this.ping, (object)this.speedWarnings);
                string strMessage = string.Format("SpeedHack | {0} [{1}] | {2} -> {3} | Скорость: {4} м/c (Макс: {5} м/c).| {6} мс. | Предупреждений: {7}", (object)this.player.displayName, (object)this.player.userID, (object)this.playerPreviousPosition, (object)playerPosition, (object)string.Format("{0:0.##}", (object)(float)((double)Vector3.Distance(playerPosition, this.playerPreviousPosition) * 5.0)), (object)string.Format("{0:0.##}", (object)(float)((double)maxSpeed * 5.0)), (object)this.ping, (object)this.speedWarnings);
                Interface.Oxide.LogError(str);
                instance.LogToFile("Log", strMessage, instance);
                Plugins.AntiHack.SendReportToOnlineModerators(str);
            }

            private void CreateLogTextureHack(Vector3 playerPosition, string objectName)
            {
                string str = string.Format("TextureHack detected\n{0} [{1}]\nПозиция: {2}\nОбъект: {3}\nПинг: {4} мс.\nПопыток: {5}", (object)this.player.displayName, (object)this.player.userID, (object)playerPosition, (object)objectName, (object)this.ping, (object)this.textureWarnings);
                string strMessage = string.Format("TextureHack | {0} [{1}] | {2} | Объект: {3} | {4} мс. | Попыток: {5}", (object)this.player.displayName, (object)this.player.userID, (object)playerPosition, (object)objectName, (object)this.ping, (object)this.textureWarnings);
                string reason = string.Format("AntiHack: TextureHack");
                Interface.Oxide.LogError(str);
                instance.LogToFile("Log", strMessage, instance);
                if (Plugins.AntiHack.needKickEndKill)
                { 
                    instance.Kick(player, $"{reason}");
                    player.KillMessage();
                }
                Plugins.AntiHack.SendReportToOnlineModerators(str);
            }

            private void AddTemp()
            {
                AntiHack.Coordinates coordinates;
                coordinates.startPos = this.playerPreviousPosition.ToString();
                coordinates.endPos = this.player.transform.position.ToString();
                this.temp.coordinates.Add(coordinates);
            }

            private void Reset()
            {
                this.temp.coordinates.Clear();
                this.IsFlying = false;
            }

            private void CrimeHandler(string reason)
            {
                if (Plugins.AntiHack.needBan)
                    ConsoleSystem.Run(ConsoleSystem.Option.Unrestricted, string.Format("ban {0} {1}", (object)this.player.userID, (object)reason));
                if (!Plugins.AntiHack.needKick)
                    return;
                ConsoleSystem.Run(ConsoleSystem.Option.Unrestricted, string.Format("kick {0} {1}", (object)this.player.userID, (object)reason));
            }

            public void Disconnect()
            {
                if (playersHandlers.ContainsKey(this.player.userID))
                    playersHandlers.Remove(this.player.userID);
                Destroy(this);
            }
            public void ShowAllPlayers()
            {
                foreach (HashSet<BaseEntity> baseEntitySet in this.hidedPlayersEntities.Values)
                {
                    foreach (BaseNetworkable ent in baseEntitySet)
                        this.player.QueueUpdate(BasePlayer.NetworkQueue.Update, ent);
                }
            }

            public void Destroy()
            {
                foreach (BaseEntity hidedEntity in this.hidedEntities)
                {
                    if ((UnityEngine.Object)(hidedEntity as BasePlayer) != (UnityEngine.Object)null)
                        this.ShowPlayer(hidedEntity as BasePlayer, false, false);
                    else
                        this.Show(hidedEntity, false);
                }
                Plugins.AntiHack.playersHandlers.Remove(this.player.userID);
                UnityEngine.Object.Destroy((UnityEngine.Object)this);
            }
        }
    }
}
                    
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */