using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Plugins.TPExtensionMethods;

namespace Oxide.Plugins
{
    [Info("TPBattle Pass", "rustmods.ru", "2.0.7")]
    internal class  TPBattlePass : RustPlugin
    {
        #region Static

        [PluginReference] private Plugin ImageLibrary, TPApi;
        private Configuration _config;
        private const string Layer = "UI_BATTLEPASS_MAINLAYER";
        private const string perm = "tpbattlepass.vip";
        private const string perm1 = "tpbattlepass.exp";

        #endregion

        #region Config

        private class Configuration
        {
            [JsonProperty("Настройка очков")] public readonly PointSettings Point = new PointSettings();

            [JsonProperty("Настройка уровней")]
            public readonly LevelSettings LevelDefault = new LevelSettings();

            [JsonProperty("Настройка для донатеров")]
            public readonly LevelSettings LevelDonate = new LevelSettings();

            internal class LevelSettings
            {

                [JsonProperty(PropertyName = "Список уровней", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public readonly List<Settings> LevelList = new List<Settings>
                {
                    new Settings
                    {
                        Level = 1,
                        Exp = 1000,
                        DisplayName = "Хай",
                        Image = "https://i.ibb.co/ZHPr63j/3455435436.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 2,
                        Exp = 1000,
                        DisplayName = "Калашик x1",
                        Image = "https://i.ibb.co/8mk7g2s/4-2.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 3,
                        Exp = 1000,
                        DisplayName = "Звезда",
                        Image = "https://i.ibb.co/4dw6gpg/4-4.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 4,
                        Exp = 1000,
                        DisplayName = "Тест",
                        Image = "https://i.ibb.co/3WBB1Y6/4.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 5,
                        Exp = 1000,
                        DisplayName = "Бомба x5",
                        Image = "https://i.ibb.co/25gZ9W0/4.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 6,
                        Exp = 1000,
                        DisplayName = "Хай x2",
                        Image = "https://i.ibb.co/ZHPr63j/3455435436.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 7,
                        Exp = 1000,
                        DisplayName = "тенис",
                        Image = "https://i.ibb.co/ZHPr63j/3455435436.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 8,
                        Exp = 1000,
                        DisplayName = "Точка",
                        Image = "https://i.ibb.co/8mk7g2s/4-2.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 9,
                        Exp = 1000,
                        DisplayName = "Дорого",
                        Image = "https://i.ibb.co/4dw6gpg/4-4.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 10,
                        Exp = 1000,
                        DisplayName = "дина",
                        Image = "https://i.ibb.co/3WBB1Y6/4.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 11,
                        Exp = 1000,
                        DisplayName = "x5",
                        Image = "https://i.ibb.co/25gZ9W0/4.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    },
                    new Settings
                    {
                        Level = 12,
                        Exp = 1000,
                        DisplayName = "Мужик",
                        Image = "https://i.ibb.co/25gZ9W0/4.png",
                        Reward = new Settings.RewardSettings
                        {
                            ShortName = "rifle.ak",
                            Amount = 1,
                            SkinID = 0,
                            command = new List<string>()
                            {
                                "givecoin %STEAMID% 10",
                                "o.grant user %STEAMID% kits.vip"
                            }

                        }
                    }
                };

                internal class Settings
                {
                    [JsonProperty("Level Number")] public int Level;

                    [JsonProperty("Number Of Exp To Get This Level")]
                    public int Exp;

                    [JsonProperty("DisplayName")] public string DisplayName;

                    [JsonProperty("Award Display Image")]
                    public string Image;

                    [JsonProperty("Level Award")] public RewardSettings Reward;

                    internal class RewardSettings
                    {
                        [JsonProperty("ShortName")] public string ShortName;
                        [JsonProperty("Amount")] public int Amount;
                        [JsonProperty("SkinID")] public ulong SkinID;

                        [JsonProperty("Commands To Be Executed")]
                        public List<string> command;
                    }
                }
            }

            internal class PointSettings
            {
                [JsonProperty("Donator Point Multiplier")]
                public readonly int DonateAmount = 1;

                [JsonProperty("Количество Очков За Убийство Игрока")]
                public readonly int killPlayer = 1;

                [JsonProperty("Количество Очков За Убийство Животных")]
                public readonly int killHuman = 1;

                [JsonProperty("Количество Очков За Уничтожение вертолета")]
                public readonly int killHeli = 1;

                [JsonProperty("Количество Очков за Убийство NPC")]
                public readonly int killNPC = 1;

                [JsonProperty("Количество Очков за Уничтожение Танка")]
                public readonly int killBredly = 1;

                [JsonProperty("Количество Очков, Вычитаемых За Смерть")]
                public readonly int deathPlayer = 1;

                [JsonProperty("Настройки фарма")] public readonly GatherSettings Gather = new GatherSettings();
            }

            internal class GatherSettings
            {
                [JsonProperty(PropertyName = "Настройка фарма(Краткое название/Количество Очков)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public readonly Dictionary<string, int> GatherList = new Dictionary<string, int>
                {
                    ["wood"] = 2
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region OxideHooks

        void OnServerInitialized()
        {
            PrintWarning("PLUGINS========Forum - rustmods.ru");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/s17Cxv7/11.png", "4oktSYU");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/MMsDR8T/VLVR47D.png", "VLVR47D");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/xXBn6tp/NcpsEhm.png", "NcpsEhm");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/3N0hLC9/mbdM6pM.png", "mbdM6pM");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/hf6yBFC/HNOe293.png", "HNOe293");
            LoadData();
            permission.RegisterPermission(perm, this);
            permission.RegisterPermission(perm1, this);
            foreach (var check in _config.LevelDefault.LevelList)
                ImageLibrary?.Call("AddImage", check.Image, check.Image);
            foreach (var check in _config.LevelDonate.LevelList)
                ImageLibrary?.Call("AddImage", check.Image, check.Image);
            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (!_data.ContainsKey(player.userID))
                _data.Add(player.userID, new Data());
        }

        private void Unload()
        {
            SaveData();
        }

        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (item == null || player == null) return null;
            if (!_config.Point.Gather.GatherList.ContainsKey(item.info.shortname)) return null;
            GivePoint(player.userID, _config.Point.Gather.GatherList[item.info.shortname]);
            return null;
        }

      

        private readonly Dictionary<NetworkableId, ulong> LastHeliHit = new Dictionary<NetworkableId, ulong>();

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            var player = info.InitiatorPlayer;
            if (player == null) return null;
            if (entity is BaseHelicopter && info.Initiator is BasePlayer)
            {
                if (!LastHeliHit.ContainsKey(entity.net.ID))
                    LastHeliHit.Add(entity.net.ID, info.InitiatorPlayer.userID);
                LastHeliHit[entity.net.ID] = info.InitiatorPlayer.userID;
            }

            return null;
        }
        private void OnEntityDeath(PatrolHelicopter entity, HitInfo info)
        {
            if (entity is null || info is null) return;
            
            BasePlayer player = info.InitiatorPlayer ? info.InitiatorPlayer : entity.myAI?._targetList?.TPLast()?.ply;

            if (player is not null && !player.IsNpc && entity.ToPlayer() != player)
            {
                GivePoint(player.userID, _config.Point.killHeli);
                return;
            }
        }
        private void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null) return;
            var player = info.InitiatorPlayer;
            if (entity as BaseAnimalNPC)
            {
                GivePoint(player.userID, _config.Point.killHuman);
                return;
            }

            if (entity as BaseHelicopter)
            {
                if (!LastHeliHit.ContainsKey(entity.net.ID)) return;
                GivePoint(LastHeliHit[entity.net.ID], _config.Point.killHeli);
                return;
            }

            if (entity as BradleyAPC)
            {
                GivePoint(player.userID, _config.Point.killBredly);
                return;
            }

            if (entity as NPCPlayer || entity as ScientistNPC || entity.IsNpc)
            {
                GivePoint(player.userID, _config.Point.killNPC);
                return;
            }

            if (entity.ToPlayer() == null) return;
            if (entity.ToPlayer().userID != player.userID)
            {
                GivePoint(player.userID, _config.Point.killPlayer);
            }

            if (!_data.ContainsKey(entity.ToPlayer().userID)) return;
            if (info.InitiatorPlayer == null) return;
            if (info.InitiatorPlayer.IsNpc) return;
            _data[entity.ToPlayer().userID].Score -= _config.Point.deathPlayer;
        }

        #endregion

        #region Data

        private Dictionary<ulong, Data> _data;

        private class Data
        {
            public int Level;
            public int Score;
            public readonly List<int> DefaultRewardID = new List<int>();
            public readonly List<int> DonateRewardID = new List<int>();
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerData"))
                _data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Data>>(
                    $"{Name}/PlayerData");
            else _data = new Dictionary<ulong, Data>();
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", _data);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void SaveData()
        {
            if (_data != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", _data);
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_BATTLEPASS_GETREWARDDEFULT")]
        private void cmdChatUI_BATTLEPASS_GETREWARD(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var id = int.Parse(arg.Args[0]);
            var page = int.Parse(arg.Args[1]);
            var settings = _config.LevelDefault.LevelList.FirstOrDefault(p => p.Level == id);
            var reward = settings.Reward;
            _data[player.userID].DefaultRewardID.Add(id);
            ShowUIMain(player, page);

            if (!string.IsNullOrEmpty(reward.ShortName))
            {
                var item = ItemManager.CreateByName(reward.ShortName, reward.Amount, reward.SkinID);
                player.GiveItem(item);
            }

            if (reward.command.Count <= 0) return;
            for (var index = 0; index < reward.command.Count; index++)
            {
                var check = reward.command[index];
                rust.RunServerCommand(check.Replace("%STEAMID%", player.UserIDString));
            }

        }

        [ChatCommand("pass")]
        private void cmdChatpass(BasePlayer player, string command, string[] args)
        {
            ShowUIMain(player, 0);
        }

        [ChatCommand("lvl")]
        private void cmdChatpa22ss(BasePlayer player, string command, string[] args)
        {
            ShowUIMain(player, 0);
        }

        [ChatCommand("level")]
        private void cmdChatpas2s(BasePlayer player, string command, string[] args)
        {
            ShowUIMain(player, 0);
        }

        [ConsoleCommand("UI_BATTLEPASS_GETREWARDDONATE")]
        private void cmdChatUI_BATTLEPASS_DDONATE(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var id = int.Parse(arg.Args[0]);
            var page = int.Parse(arg.Args[1]);
            var settings = _config.LevelDonate.LevelList.FirstOrDefault(p => p.Level == id);
            var reward = settings.Reward;
            _data[player.userID].DonateRewardID.Add(id);
            ShowUIMain(player, page);
            if (!string.IsNullOrEmpty(reward.ShortName))
            {
                var item = ItemManager.CreateByName(reward.ShortName, reward.Amount, reward.SkinID);
                player.GiveItem(item);
            }

            if (reward.command.Count <= 0) return;
            for (var index = 0; index < reward.command.Count; index++)
            {
                var check = reward.command[index];
                rust.RunServerCommand(check.Replace("%STEAMID%", player.UserIDString));
            }
        }

        [ConsoleCommand("givepoint")]
        private void cmdChatgivepoint(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args?.Length != 2)
            {
                PrintError(
                    "You are not using the command correctly. Example: givepoint STEAMID Quantity");
                return;
            }

            var player = arg.Player();
            if (arg.Connection != null)
                if (!player.IsAdmin)
                    return;
            var userID = ulong.Parse(arg.Args[0]);
            var amount = int.Parse(arg.Args[1]);
            GivePoint(userID, amount);

        }

        [ConsoleCommand("UI_BATTLEPASS_CHANGEPAGE")]
        private void cmdChatUI_BATTLEPASS_CHANGEPAGE(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (arg == null || arg.Args.Length == 0)
            {
                player.ChatMessage("Wrong number");
                return;
            }

            var page = int.Parse(arg.Args[0]);
            ShowUIMain(player, page);
        }

        #endregion

        #region Function

        private void GivePoint(ulong userid, int amount)
        {
            if (!_data.ContainsKey(userid)) return;
            if (permission.UserHasPermission(userid.ToString(), perm1))
                amount *= _config.Point.DonateAmount;
            var data = _data[userid];
            var settings = _config.LevelDefault.LevelList.FirstOrDefault(p => p.Level == data.Level + 1);
            if (settings == null) return;

            data.Score += amount;
            if (data.Score < settings.Exp) return;
            data.Level++;
            data.Score -= settings.Exp;
            GivePoint(userid, 0);
            TPApi.Call("ShowGameTipForPlayer", BasePlayer.FindByID(userid), 1, "Вы получили новый уровень");
        }

        #endregion
        
        void ShowUIMain(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            var d = _data[player.userID];
            
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiElement
            {
                Name = Layer + ".Main",
                Parent = Layer,
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "4oktSYU"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                }
            }); 

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.193 0.455", AnchorMax = "0.21 0.59", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".Main", "standart");

            container.Add(new CuiElement
            {
                Parent = "standart",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "NcpsEhm"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                }
            }); 

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.193 0.275", AnchorMax = "0.21 0.41", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".Main", "prem");

            container.Add(new CuiElement
            {
                Parent = "prem",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", permission.UserHasPermission(player.UserIDString, perm) ? "mbdM6pM" : "HNOe293"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                }
            }); 

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832", OffsetMax = "0 0" },
                Button = { Close = Layer, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".Main");

            #region body

            var levels = _config.LevelDefault.LevelList.Skip(page * 8).Take(8);
            var donate = _config.LevelDonate.LevelList.Skip(page * 8).Take(8);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.155 0.221", AnchorMax = "0.848 0.597",/*AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "198 -561", OffsetMax = "1087 -290"*/},
                Image = {Color = "2 2 2 0"}
            }, Layer, Layer + ".rewards");

            float width = 0.1038f, height = 0.35f, startxBox = 0.09f, startyBox = 0.98f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in levels)
            {
                var lvl = check.Level;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 1", OffsetMax = "-2 0" },
                    Image = { Color = "0 0 0 0" },
                }, Layer + ".rewards", Layer + ".rewards" + lvl);
                xmin += width + 0.003f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }

                var color = d.DefaultRewardID.Contains(check.Level) ? "1 1 1 0.3" : "1 1 1 1";
                container.Add(new CuiElement
                {
                    Parent = Layer + ".rewards" + lvl,
                    Components =
                    {
                        new CuiRawImageComponent {Png = (String) ImageLibrary.Call("GetImage", check.Image), Color = color, FadeIn = 0.5f},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"}
                    }
                });

                var text = !d.DefaultRewardID.Contains(check.Level) && check.Level <= d.Level ? "Получить" : check.DisplayName;
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0.03", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Text =
                    {
                        Text = text, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerCenter,
                        Color = "1 1 1 1", FadeIn = 0.5f
                    }
                }, Layer + ".rewards" + lvl);

                if (!d.DefaultRewardID.Contains(check.Level) && check.Level <= d.Level)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".rewards" + lvl,
                        Components =
                        {
                            new CuiRawImageComponent {Png = (String) ImageLibrary.Call("GetImage", "VLVR47D"), FadeIn = 0.5f},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"}
                        }
                    });
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                        Button = {Color = "0 0 0 0", Command = $"UI_BATTLEPASS_GETREWARDDEFULT {check.Level} {page}", FadeIn = 0.5f},
                        Text = {Text = ""}
                    }, Layer + ".rewards" + lvl);
                }
            }

            float width1 = 0.1038f, height1 = 0.35f, startxBox1 = 0.09f, startyBox1 = 0.5f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in donate)
            {
                var lvl = check.Level;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "2 2", OffsetMax = "-2 0" },
                    Image = { Color = "0 0 0 0" },
                }, Layer + ".rewards", Layer + ".rewards" + lvl);
                xmin1 += width1 + 0.003f;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1;
                }

                var color = d.DonateRewardID.Contains(check.Level) ? "1 1 1 0.3" : permission.UserHasPermission(player.UserIDString, perm) ? "1 1 1 1" : "1 1 1 0.3";
                container.Add(new CuiElement
                {
                    Parent = Layer + ".rewards" + lvl,
                    Components =
                    {
                        new CuiRawImageComponent {Png = (String) ImageLibrary.Call("GetImage", check.Image), Color = color, FadeIn = 0.5f},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"}
                    }
                });

                var text = !d.DonateRewardID.Contains(check.Level) && check.Level <= d.Level && permission.UserHasPermission(player.UserIDString, perm) ? "Получить" : check.DisplayName;
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0.03", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Text =
                    {
                        Text = text, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerCenter,
                        Color = "1 1 1 1", FadeIn = 0.5f
                    }
                }, Layer + ".rewards" + lvl);

                if (!d.DonateRewardID.Contains(check.Level) && check.Level <= d.Level && permission.UserHasPermission(player.UserIDString, perm))
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".rewards" + lvl,
                        Components =
                        {
                            new CuiRawImageComponent {Png = (String) ImageLibrary.Call("GetImage", "VLVR47D"), FadeIn = 0.5f},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"}
                        }
                    });
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                        Button = {Color = "0 0 0 0", Command = $"UI_BATTLEPASS_GETREWARDDONATE {check.Level} {page}", FadeIn = 0.5f},
                        Text = {Text = ""}
                    }, Layer + ".rewards" + lvl);
                }
            }

            float width2 = 0.02f, height2 = 0.035f, startxBox2 = 0.181f, startyBox2 = 0.125f - height2, xmin2 = startxBox2, ymin2 = startyBox2;
            foreach (var check in levels)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin2} {ymin2}", AnchorMax = $"{xmin2 + width2} {ymin2 + height2 * 1}", OffsetMin = "2 0", OffsetMax = "-2 0" },
                    Image = { Color = "0 0 0 0" },
                }, Layer + ".rewards", Layer + ".rewards" + check.Level);
                xmin2 += width2 + 0.087f;

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Level}", Color = "1 1 1 0.4", Align = TextAnchor.MiddleCenter, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                }, Layer + ".rewards" + check.Level);
            }

            #endregion
            string text1 = "";
            if(d.Level < _config.LevelDefault.LevelList.Last().Level)
                text1 = $"{d.Score}/{_config.LevelDefault.LevelList[d.Level].Exp}Exp";
            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{text1}",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1",
                    },

                    new CuiRectTransformComponent
                    {
                       AnchorMin = "0.68 0.608", AnchorMax = "0.755 0.637"
                    },
                }
            });
            if (page > 0)
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0.7627 0.601", AnchorMax = "0.785 0.644", OffsetMax = "0 0"},
                    Button = {Color = "0 0 0 0", Command = $"UI_BATTLEPASS_CHANGEPAGE {page - 1}"},
                    Text = {Text = ""}
                }, Layer + ".Main");
            
            if (_config.LevelDefault.LevelList.Count - 8 * (page + 1) > 0)
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0.787 0.601", AnchorMax = "0.8101 0.644", OffsetMax = "0 0"},
                    Button = {Color = "0 0 0 0", Command = $"UI_BATTLEPASS_CHANGEPAGE {page + 1}"},
                    Text = {Text = ""}
                }, Layer + ".Main");

            CuiHelper.AddUi(player, container);
        }
    }
}

namespace Oxide.Plugins.TPExtensionMethods
{
    public static class ExtensionMethods
    {
        public static TSource TPLast<TSource>(this IList<TSource> source) => source[source.Count - 1];
    }
}
