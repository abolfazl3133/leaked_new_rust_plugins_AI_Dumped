using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

/// <UPDATES>
/// 2.0.0
/// - Добавлена многоязычность (eng, ru)
/// - Добавлены настройки очков в конфиг
/// 
/// 2.0.1
/// - Адаптация плагина Clans (https://oxide-russia.ru/resources/2849/)
/// 2.0.2
/// - Исправлен NRE в ChatTop -> StatsUI -> List<T>.ElementAt(index) (в конфиге не было значения для index элемента)
/// - Убран AddImage с получением иконок предметов из RUST
/// </UPDATES>

namespace Oxide.Plugins
{
    [Info("StatsSystem", "https://discord.gg/TrJ7jnS233", "2.0.2")]
    class StatsSystem : RustPlugin
    {
        #region Вар
        string Layer = "ui.StatsSystem.bg";

        [PluginReference] Plugin ImageLibrary, RustStore, Clans;

        Dictionary<ulong, DBSettings> _db = new Dictionary<ulong, DBSettings>();

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        private bool IsFriends(ulong user, ulong target)
        {
            if (Clans?.Call<bool>("HasFriend", user, target) == true)
                return true;
                
            return false;
        }
        #endregion

        #region Класс
        public class DBSettings
        {
            public string DisplayName;
            public int Points = 0;
            public int Farm = 0;
            public int Kill = 0;
            public int Death = 0;
            public bool IsConnected;
            public int Balance;
            public Dictionary<string, int> Settings = new Dictionary<string, int>()
            {
                ["Kill"] = 0,
                ["Death"] = 0,
                ["Farm"] = 0
            };
            public Dictionary<string, int> Res = new Dictionary<string, int>()
            {
                ["wood"] = 0,
                ["stones"] = 0,
                ["metal.ore"] = 0,
                ["sulfur.ore"] = 0,
                ["hq.metal.ore"] = 0,
                ["cloth"] = 0,
                ["leather"] = 0,
                ["fat.animal"] = 0,
                ["cratecostume"] = 0
            };
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration
        {
            [JsonProperty("ID магазина", Order = 0)] 
            public string ShopID = "";
            [JsonProperty("Secret ключ магазина", Order = 1)] 
            public string Secret = "";
            [JsonProperty("Настройки бонусов", Order = 2)] 
            public List<string> Bonus;
            [JsonProperty("Очков за убийство", Order = 3)]
            public int pointsForKill = 100;
            [JsonProperty("Очков за добычу руды", Order = 3)]
            public int pointsForFarm = 7;
            [JsonProperty("Очков за разрушение бочки", Order = 3)]
            public int pointsForBarrelDestroy = 2;
            [JsonProperty("Очков за сбитие вертолёта", Order = 3)]
            public int pointsForHeliDestroy = 1500;
            [JsonProperty("Очков за уничтожение танка", Order = 3)]
            public int pointsForAPCDestroy = 750;
            [JsonProperty("Очков за смерть", Order = 3)]
            public int pointsForDeath = -25;
            public static Configuration GetNewConfig()
            {
                return new Configuration
                {
                    Bonus = new List<string>()
                    {
                        "10000",
                        "9000",
                        "8000",
                        "7000",
                        "6000",
                        "5000",
                        "4000",
                        "3000",
                        "2000",
                        "1000"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.Bonus == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию.");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("StatsSystem/PlayerList"))
                _db = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DBSettings>>("StatsSystem/PlayerList");

            // foreach (var check in ResImage)
            //     ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{check}.png", check);

            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!_db.ContainsKey(player.userID))
                _db.Add(player.userID, new DBSettings());

            _db[player.userID].DisplayName = player.displayName;
            _db[player.userID].IsConnected = true;
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            _db[player.userID].IsConnected = false;
            SaveDataBase();
        }

        void Unload()
        {
            SaveDataBase();
        }

        void SaveDataBase()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("StatsSystem/PlayerList", _db);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            if (_db[player.userID].Res.ContainsKey(item.info.shortname))
            {
                _db[player.userID].Res[item.info.shortname] += item.amount;
                _db[player.userID].Settings["Farm"] += item.amount;
                _db[player.userID].Farm += config.pointsForFarm;
                return;
            }
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            if (_db[player.userID].Res.ContainsKey(item.info.shortname))
            {
                _db[player.userID].Res[item.info.shortname] += item.amount;
                _db[player.userID].Settings["Farm"] += item.amount;
                _db[player.userID].Farm += item.amount;
                _db[player.userID].Points += config.pointsForFarm;
                return;
            }
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (item == null || player == null) return;
            if (_db[player.userID].Res.ContainsKey(item.info.shortname))
            {
                _db[player.userID].Res[item.info.shortname] += item.amount;
                return;
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || player == null || player.IsNpc || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc) return;

            if (info.InitiatorPlayer != null)
            {
                var killer = info.InitiatorPlayer;
                if (IsFriends(player.userID, killer.userID))
                    return;

                if (killer != player)
                {
                    if (_db.ContainsKey(killer.userID))
                    {
                        _db[killer.userID].Settings["Kill"]++;
                        _db[player.userID].Kill++;
                        _db[killer.userID].Points += config.pointsForKill;
                    }
                }
                if (_db.ContainsKey(player.userID))
                {
                    _db[player.userID].Settings["Death"]++;
                    _db[player.userID].Death++;
                    _db[player.userID].Points += config.pointsForDeath;
                }
            }
        }

        public ulong lastDamageName;
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
            if (entity is BaseHelicopter && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            BasePlayer player = null;

            if (info.InitiatorPlayer != null)
                player = info.InitiatorPlayer;

            if (player == null) return;

            if (entity is BradleyAPC)
            {
                player = BasePlayer.FindByID(lastDamageName);
                _db[player.userID].Points += config.pointsForAPCDestroy;
            }

            if (entity is BaseHelicopter)
            {
                player = BasePlayer.FindByID(lastDamageName);
                _db[player.userID].Points += config.pointsForHeliDestroy;
            }

            if (entity.ShortPrefabName.Contains("barrel"))
            {
                _db[player.userID].Res["cratecostume"]++;
                _db[player.userID].Points += config.pointsForBarrelDestroy;
            }
        }

        void OnNewSave()
        {
            timer.In(60, () =>
            {
                PrintWarning("Обнаружен вайп, происходит выдача призов за топ и очистка даты!");

                foreach (var check in _db)
                {
                    check.Value.Points = 0;
                    check.Value.Farm = 0;
                    check.Value.Kill = 0;
                    check.Value.Death = 0;
                    check.Value.IsConnected = false;
                    check.Value.Settings = new Dictionary<string, int>()
                    {
                        ["Kill"] = 0,
                        ["Death"] = 0,
                        ["Farm"] = 0
                    };
                    check.Value.Res = new Dictionary<string, int>()
                    {
                        ["wood"] = 0,
                        ["stones"] = 0,
                        ["metal.ore"] = 0,
                        ["sulfur.ore"] = 0,
                        ["hq.metal.ore"] = 0,
                        ["cloth"] = 0,
                        ["leather"] = 0,
                        ["fat.animal"] = 0,
                        ["cratecostume"] = 0
                    };
                }
                int x = 0;
                foreach (var check in _db.Take(10))
                {
                    check.Value.Balance += int.Parse(config.Bonus.ElementAt(x));
                    x++;
                }

                SaveDataBase();
            });
        }
        #endregion

        #region Вывод коинов
        void ApiChangeGameStoresBalance(BasePlayer player, int amount)
        {
            // var player = BasePlayer.FindByID(userId);
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                { "action", "moneys" },
                { "type", "plus" },
                { "DisplayName", player.displayName.ToUpper() },
                { "steam_id", player.userID.ToString() },
                { "amount", amount.ToString() },
                { "mess", GetMsg("withdraw.thanksmessage", player.userID)}
            });
        }

        void APIChangeUserBalance(ulong steam, int balanceChange)
        {
            if (RustStore)
            {
                plugins.Find("RustStore").CallHook("APIChangeUserBalance", steam, balanceChange, new Action<string>((result) =>
                {
                    if (result == "SUCCESS")
                    {
                        LogToFile("LogMoscow", $"СтимID: {steam}\nУспешно получил {balanceChange} рублей на игровой счет!\n", this);
                        PrintWarning($"Игрок {steam} успешно получил {balanceChange} рублей");
                    }
                    else
                    {
                        PrintError($"Ошибка пополнения баланса для {steam}!");
                        PrintError($"Причина: {result}");
                        LogToFile("logError", $"Баланс игрока {steam} не был изменен, ошибка: {result}", this);
                    }
                }));
            }
        }

        void ExecuteApiRequest(Dictionary<string, string> args)
        {
            string url = $"https://gamestores.ru/api/?shop_id={config.ShopID}&secret={config.Secret}" + $"{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
            LogToFile("LogGS", $"Ник: {args["DisplayName"]}\nСтимID: {args["steam_id"]}\nУспешно получил {args["amount"]} рублей на игровой счет!\n", this);
            webrequest.EnqueueGet(url, (i, s) =>
            {
                if (i != 200)
                {
                    PrintError($"Ошибка соединения с сайтом!");
                }
                else
                {
                    JObject jObject = JObject.Parse(s);
                    if (jObject["result"].ToString() == "fail")
                    {
                        PrintError($"Ошибка пополнения баланса для {args["steam_id"]}!");
                        PrintError($"Причина: {jObject["message"].ToString()}");
                        LogToFile("logError", $"Баланс игрока {args["steam_id"]} не был изменен, ошибка: {jObject["message"].ToString()}", this);
                    }
                    else
                    {
                        PrintWarning($"Игрок {args["steam_id"]} успешно получил {args["amount"]} рублей");
                    }
                }
            }, this);
        }
        #endregion

        #region Картинки ресурсов
        #endregion

        #region Команды
        [ChatCommand("top")]
        void ChatTop(BasePlayer player)
        {
            StatsUI(player);
        }

        [ConsoleCommand("stats")]
        void ConsoleSkip(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "profile")
                {
                    ProfileUI(player, ulong.Parse(args.Args[1]), int.Parse(args.Args[2]));
                }
                if (args.Args[0] == "back")
                {
                    StatsUI(player);
                }
                if (args.Args[0] == "skip")
                {
                    StatsUI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "take")
                {
                    if (_db[player.userID].Balance == 0)
                    {
                        SendReply(player, GetMsg("withdraw.error.notenough", player.userID));
                        return;
                    }
                    if (string.IsNullOrEmpty(config.Secret)) APIChangeUserBalance(player.userID, _db[player.userID].Balance);
                    else ApiChangeGameStoresBalance(player, _db[player.userID].Balance);
                    SendReply(player, string.Format(GetMsg("withdraw.success", player.userID), _db[player.userID].Balance));
                    // SendReply(player, $"Вы успешно вывели {DB[player.userID].Balance} рублей, на игровой магазин!");
                    _db[player.userID].Balance -= _db[player.userID].Balance;
                    CuiHelper.DestroyUi(player, "MainStats");
                }
            }
        }
        #endregion

        #region Интерфейс
        void StatsUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, "MainStats");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.31 0.2", AnchorMax = "0.69 0.8", OffsetMax = "0 0" },
                Image = { Color = "0.2 0.2 0.2 1" }
            }, "MainStats", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.23", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Top");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.95", AnchorMax = $"1 1" },
                Image = { Color = "0.5 0.5 0.5 1" }
            }, "Top");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01 0.95", AnchorMax = $"1 1", OffsetMin = "2 1", OffsetMax = "-2 -1" },
                Image = { Color = "0.5 0.5 0.5 0" }
            }, "Top", "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.0 0", AnchorMax = $"0.08 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = GetMsg("main.place.text", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.115 0", AnchorMax = $"0.4 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = GetMsg("main.playername.text", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.4 0", AnchorMax = $"0.5 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = GetMsg("main.reward.text", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.6 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = GetMsg("main.farm.text", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.6 0", AnchorMax = $"0.7 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = GetMsg("main.points.text", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.7 0", AnchorMax = $"0.8 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = GetMsg("info.killsdeathrate", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            /*container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.0 0.94", AnchorMax = $"0.08 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = $"МЕСТО", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");*/

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.837 0.02", AnchorMax = $"0.987 0.11", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.00 0.00 0.7", Close = "MainStats" },
                Text = { Text = GetMsg("button.close", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.6 0.02", AnchorMax = $"0.83 0.11", OffsetMax = "0 0" },
                Button = { Color = "0.00 0.35 0.00 1", Command = $"stats profile {player.userID} 0" },
                Text = { Text = GetMsg("main.myprofile.text", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.523 0.02", AnchorMax = $"0.593 0.11", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.3", Command = _db.Count() > (page + 1) * 10 ? $"stats skip {page + 1}" : "" },
                Text = { Text = GetMsg("main.pagenext.text", player.userID), Color = _db.Count() > (page + 1) * 10 ? "1 1 1 1" : "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.446 0.02", AnchorMax = $"0.516 0.11", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.3", Command = "" },
                Text = { Text = $"{page + 1}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.37 0.02", AnchorMax = $"0.44 0.11", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.3", Command = page >= 1 ? $"stats skip {page - 1}" : "" },
                Text = { Text = GetMsg("main.pageprevious.text", player.userID), Color = page >= 1 ? "1 1 1 1" : "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 0.22", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "InfoTop");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.01 0.01", AnchorMax = $"0.99 0.99", OffsetMax = "0 0" },
                Text = { Text = string.Format(GetMsg("points.give.for", player.userID), config.pointsForKill, config.pointsForFarm, config.pointsForBarrelDestroy, config.pointsForHeliDestroy, config.pointsForAPCDestroy, config.pointsForKill), Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "InfoTop");

            float width = 0.98f, height = 0.063f, startxBox = 0.01f, startyBox = 0.95f - height, xmin = startxBox, ymin = startyBox, z = 0;
            var items = from item in _db orderby item.Value.Points descending select item;
            foreach (var check in items.Skip(page * 10).Take(10))
            {
                z++;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 1", OffsetMax = "-2 -1" },
                    Image = { Color = "0 0 0 0" }
                }, Layer, "PlayerTop");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"0.8 1", OffsetMax = "0 0" },
                    Image = { Color = "0.3 0.3 0.3 0.8" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.08 1", OffsetMax = "0 0" },
                    Text = { Text = $"{z + page * 10}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.115 0", AnchorMax = $"0.4 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.DisplayName}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.4 0", AnchorMax = $"0.575 1", OffsetMax = "0 0" },
                    Text = { Text = $"", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.6 0", AnchorMax = $"0.7 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.Points}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.6 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.Farm}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.7 0", AnchorMax = $"0.8 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.Kill}/{check.Value.Death}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.805 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Button = { Color = "0.3 0.3 0.3 0.80", Command = $"stats profile {check.Key} {z + page * 10}" },
                    Text = { Text = GetMsg("main.profile.text", player.userID), Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            float width1 = 0.785f, height1 = 0.063f, startxBox1 = 0.01f, startyBox1 = 0.95f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            if (page == 0)
            {
                for (int x = 0; x < _db.Take(10).Count(); x++)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "2 1", OffsetMax = "-2 -1" },
                        Image = { Color = "0 0 0 0" }
                    }, Layer, "PlayerTop");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.6 1", OffsetMax = "0 0" },
                        Text = { Text = $"{(config.Bonus.ElementAt(x) == null ? "0" : config.Bonus.ElementAt(x))}₽", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, "PlayerTop");

                    xmin1 += width1;
                    if (xmin1 + width1 >= 1)
                    {
                        xmin1 = startxBox1;
                        ymin1 -= height1;
                    }
                }
            }

            CuiHelper.AddUi(player, container);
        }

        void ProfileUI(BasePlayer player, ulong SteamID, int z)
        {
            CuiHelper.DestroyUi(player, "MainStats");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.31 0.28", AnchorMax = "0.69 0.72", OffsetMax = "0 0" },
                Image = { Color = "0.2 0.2 0.2 1" }
            }, "MainStats", Layer);

            var target = _db[SteamID];
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.01 0.8", AnchorMax = $"0.99 0.99", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=25>{string.Format(GetMsg("player.nickname", player.userID), target.DisplayName.ToUpper())}</size></b>\n{GetMsg("help.profile.desc", player.userID)}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.51 0.131", AnchorMax = $"0.518 0.777", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer);

            if (SteamID == player.userID)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.25 0.02", AnchorMax = $"0.518 0.11", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.1" }
                }, Layer, "Balance");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{GetMsg("info.balance", player.userID)}: {target.Balance}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Balance");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.525 0.02", AnchorMax = $"0.675 0.11", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1", Command = "stats take" },
                    Text = { Text = $"{GetMsg("button.withdraw", player.userID)}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, Layer);
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.682 0.02", AnchorMax = $"0.83 0.11", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.1", Command = "stats back" },
                Text = { Text = $"{GetMsg("button.back", player.userID)}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.837 0.02", AnchorMax = $"0.987 0.11", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.00 0.00 0.7", Close = "MainStats" },
                Text = { Text = $"{GetMsg("button.close", player.userID)}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.015 0.51", AnchorMax = $"0.21 0.777", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Avatar");

            container.Add(new CuiElement
            {
                Parent = "Avatar",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", SteamID.ToString()) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.22 0.7", AnchorMax = $"0.5 0.777", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Place");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"{GetMsg("info.topplace", player.userID)}: {z}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Place");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.22 0.603", AnchorMax = $"0.5 0.68", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Points");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"{GetMsg("info.points", player.userID)}: {target.Points}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Points");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.22 0.51", AnchorMax = $"0.5 0.587", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Status");

            var status = target.IsConnected == true ? GetMsg("info.status.online", player.userID) : GetMsg("info.status.offline", player.userID);
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"{GetMsg("info.status.text", player.userID)}: {status}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Status");

            float width1 = 0.494f, height1 = 0.0939f, startxBox1 = 0.01f, startyBox1 = 0.5f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in target.Settings)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Image = { Color = "1 1 1 0.1" }
                }, Layer, "Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Key.Replace("Kill", GetMsg("info.kills", player.userID)).Replace("Death", GetMsg("info.deaths", player.userID)).Replace("Farm", GetMsg("info.farm", player.userID))}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.97 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleRight, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "Count");

                xmin1 += width1;
                if (xmin1 + width1 >= 0)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1;
                }
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.014 0.133", AnchorMax = $"0.5 0.213", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "KD");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = GetMsg("info.killsdeathrate", player.userID), Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "KD");

            var kd = target.Settings["Death"] == 0 ? target.Settings["Kill"] : (float)Math.Round(((float)target.Settings["Kill"]) / target.Settings["Death"], 1);
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.97 1", OffsetMax = "0 0" },
                Text = { Text = $"{kd}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleRight, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, "KD");

            float width = 0.155f, height = 0.22f, startxBox = 0.523f, startyBox = 0.785f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in target.Res)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Image = { Color = "1 1 1 0.1" }
                }, Layer, "Images");

                container.Add(new CuiElement
                {
                    Parent = "Images",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", check.Key) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.97 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value}", Color = "1 1 1 0.8", Align = TextAnchor.LowerRight, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "Images");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Lang
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["info.killsdeathrate"] = "K/D",
                ["info.kills"] = "KILLS",
                ["info.deaths"] = "DEATHS",
                ["info.farm"] = "FARM",
                ["info.topplace"] = "Place in top",
                ["info.points"] = "Points",
                ["info.status.text"] = "Status",
                ["info.status.offline"] = "offline",
                ["info.status.online"] = "online",
                ["info.balance"] = "Your balance",
                ["button.close"] = "CLOSE",
                ["button.back"] = "BACK",
                ["button.withdraw"] = "WITHDRAW",
                ["main.place.text"] = "PLACE",
                ["main.playername.text"] = "PLAYER NAME",
                ["main.reward.text"] = "REWARD",
                ["main.farm.text"] = "FARM",
                ["main.points.text"] = "POINTS",
                ["main.myprofile.text"] = "MY PROFILE",
                ["main.pagenext.text"] = ">",
                ["main.pageprevious.text"] = "<",
                ["points.give.for"] = "Points are given for:\nKill +{0}, ore mining +{1}, destruction barrel +{2}, shooting down a helicopter +{3}, tank destruction +{4}\nPoints are taken away:\nDeath and suicide -{5}\nAwards are given after the wipe on the server!",
                ["help.profile.desc"] = "Here you can view player statistics.",
                ["player.nickname"] = "{0}",
                ["main.profile.text"] = "PROFILE",
                ["withdraw.success"] = "You have successfully withdrawn {0} rubles to the game store!",
                ["withdraw.error.notenough"] = "You don't have enough money!",
                ["withdraw.thanksmessage"] = "Thank you for playing on our project!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["info.killsdeathrate"] = "К/Д",
                ["info.kills"] = "УБИЙСТВ",
                ["info.deaths"] = "СМЕРТЕЙ",
                ["info.farm"] = "ФАРМ",
                ["info.topplace"] = "Место в топе",
                ["info.points"] = "Очков",
                ["info.status.text"] = "Статус",
                ["info.status.offline"] = "оффлайн",
                ["info.status.online"] = "онлайн",
                ["info.balance"] = "Ваш баланс",
                ["button.close"] = "ЗАКРЫТЬ",
                ["button.back"] = "НАЗАД",
                ["button.withdraw"] = "ВЫВЕСТИ",
                ["main.place.text"] = "МЕСТО",
                ["main.playername.text"] = "ИМЯ ИГРОКА",
                ["main.reward.text"] = "НАГРАДА",
                ["main.farm.text"] = "ФАРМ",
                ["main.points.text"] = "ОЧКИ",
                ["main.profile.text"] = "ПРОФИЛЬ",
                ["main.myprofile.text"] = "МОЙ ПРОФИЛЬ",
                ["main.pagenext.text"] = ">",
                ["main.pageprevious.text"] = "<",
                ["points.give.for"] = "Очки даются:\nУбийство +{0}, добыча руды +{1}, разрушение бочки +{2}, сбитие вертолета +{3}, уничтожение танка +{4}\nОчки отнимаются:\nСмерть и самоубийство -{5}\nНаграды выдаются после вайпа на сервере!",
                ["help.profile.desc"] = "Здесь вы можете посмотреть статистику игрока.",
                ["player.nickname"] = "{0}",
                ["withdraw.success"] = "Вы успешно вывели {0} рублей, на игровой магазин!",
                ["withdraw.error.notenough"] = "У вас не хватает денег!",
                ["withdraw.thanksmessage"] = "Спасибо что играете на нашем проекте!"
            }, this, "ru");

        }
        string GetMsg(string key, ulong id) => lang.GetMessage(key, this, id.ToString());
        #endregion
    }
}