using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using Rust;
using UnityEngine;
using System.Collections;
using System;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Random = UnityEngine.Random;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using ru = Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("RaidZone", "fermens", "0.1.51")]
    [Description("Рейблок по зонам")]
    public class RaidZone : RustPlugin
    {
        #region КОНФИГa
        private static PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private static Dictionary<string, string> _names = new Dictionary<string, string>
        {
            { "wall Stone", "вашу каменную стену"},
            { "wall.low Stone", "вашу каменную низкую стену"},
            { "wall.frame Stone", "ваш каменный настенный каркас"},
            { "foundation Stone", "ваш каменный фундамент"},
            { "roof Stone", "вашу каменную крышу"},
            { "wall.doorway Stone", "ваш каменный дверной проём"},
            { "foundation.steps Stone", "ваши каменные ступеньки"},
            { "block.stair.lshape Stone", "вашу каменную L-лестницу"},
            { "block.stair.ushape Stone", "вашу каменную U-лестницу"},
            { "foundation.triangle Stone", "ваш каменный треугольный фундамент"},
            { "wall.window Stone", "ваш каменное окно"},
            { "wall.half Stone", "вашу каменную полустену"},
            { "wall Metal", "вашу металлическую стену"},
            { "wall.low Metal", "вашу металлическую низкую стену"},
            { "wall.frame Metal", "ваш металлический настенный каркас"},
            { "foundation Metal", "ваш металлический фундамент"},
            { "roof Metal", "вашу металлическую крышу"},
            { "wall.doorway Metal", "ваш металлический дверной проём"},
            { "foundation.steps Metal", "ваши металлические ступеньки"},
            { "block.stair.lshape Metal", "вашу металлическую L-лестницу"},
            { "block.stair.ushape Metal", "вашу металлическую U-лестницу"},
            { "foundation.triangle Metal", "ваш металлический треугольный фундамент"},
            { "wall.window Metal", "ваше металлическое окно"},
            { "wall.half Metal", "вашу металлическую полустену"},
            { "wall TopTier", "вашу бронированную стену"},
            { "wall.low TopTier", "вашу бронированную низкую стену"},
            { "wall.frame TopTier", "ваш бронированный настенный каркас"},
            { "foundation TopTier", "ваш бронированный фундамент"},
            { "roof TopTier", "вашу бронированную крышу"},
            { "wall.doorway TopTier", "ваш бронированный дверной проём"},
            { "foundation.steps TopTier", "ваши бронированные ступеньки"},
            { "block.stair.lshape TopTier", "вашу бронированную L-лестницу"},
            { "block.stair.ushape TopTier", "вашу бронированную U-лестницу"},
            { "foundation.triangle TopTier", "ваш бронированный треугольный фундамент"},
            { "wall.window TopTier", "ваше бронированное окно"},
            { "wall.half TopTier", "вашу бронированную полустену"},
            { "wall Wood", "вашу деревянную стену"},
            { "wall.low Wood", "вашу деревянную низкую стену"},
            { "wall.frame Wood", "ваш деревянный настенный каркас"},
            { "foundation Wood", "ваш деревянный фундамент"},
            { "roof Wood", "вашу деревянную крышу"},
            { "wall.doorway Wood", "ваш деревянный дверной проём"},
            { "foundation.steps Wood", "ваши деревянные ступеньки"},
            { "block.stair.lshape Wood", "вашу деревянную L-лестницу"},
            { "block.stair.ushape Wood", "вашу деревянную U-лестницу"},
            { "foundation.triangle Wood", "ваш деревянный треугольный фундамент"},
            { "wall.window Wood", "ваше деревянное окно"},
            { "door.hinged.metal", "вашу металлическую дверь"},
            { "floor Wood", "ваш деревянный пол"},
            { "floor Metal", "ваш металлический пол"},
            { "door.hinged.wood", "вашу деревянную дверь"},
            { "floor Stone", "ваш каменный пол"},
            { "door.double.hinged.wood", "вашу двойную деревянную дверь"},
            { "door.double.hinged.metal", "вашу двойную металлическую дверь"},
            { "shutter.wood.a", "ваши деревянные ставни"},
            { "wall.frame.garagedoor", "вашу гаражную дверь"},
            { "wall.window.bars.wood", "вашу деревянную решетку"},
            { "floor.triangle Stone", "ваш каменный треугольный потолок"},
            { "wall.external.high.wood", "ваши высокие деревянные ворота"},
            { "door.double.hinged.toptier", "вашу двойную бронированную дверь"},
            { "floor.triangle Metal", "ваш металлический треугольный потолок"},
            { "wall.frame.netting", "вашу сетчатую стену"},
            { "door.hinged.toptier", "вашу бронированную дверь"},
            { "shutter.metal.embrasure.a", "ваши металлические ставни"},
            { "wall.external.high.stone", "вашу высокую каменную стену"},
            { "gates.external.high.stone", "ваши высокие каменные ворота"},
            { "floor.ladder.hatch", "ваш люк с лестнице"},
            { "floor.grill", "ваш решетчатый настил"},
            { "floor.triangle Wood", "ваш деревянный треугольный потолок"},
            { "floor.triangle TopTier", "ваш бронированный треугольный потолок"},
            { "gates.external.high.wood", "ваши высокие деревянные ворота"},
            { "wall.half Wood", "вашу деревянную полустену"},
            { "floor TopTier", "ваш треугольный бронированный потолок"},
            { "wall.frame.cell", "вашу тюремную стену"},
            { "wall.window.bars.metal", "вашу металлическую решетку"},
            { "wall.frame.fence", "ваш сетчатый забор"},
            { "shutter.metal.embrasure.b", "вашу металлическую бойницу"},
            { "wall.window.glass.reinforced", "ваше окно из укрепленного стекла"},
            { "wall.frame.fence.gate", "вашу сетчатую дверь"},
            { "floor.frame Stone", "ваш каменный пол"},
            { "wall.frame.cell.gate", "вашу тюремную решетку"},
            { "floor.frame Metal", "ваш металический пол"},
            { "floor.frame Wood", "ваш деревянный пол" }
        };

        private static string[] _spisok = new string[] { "wall.external.high", "wall.external.high.stone", "gates.external.high.wood", "gates.external.high.stone", "wall.window.bars.metal", "wall.window.bars.toptier", "wall.window.glass.reinforced", "wall.window.bars.wood" };

        class POSITION
        {
            [JsonProperty("Нулевая точка")]
            public string zero;

            [JsonProperty("offsetmax")]
            public string offsetmax;

            [JsonProperty("offsetmin")]
            public string offsetmin;
        }

        class GUI
        {
            [JsonProperty("Текст")]
            public string text;

            [JsonProperty("Цвет фона")]
            public string background;

            [JsonProperty("Цвет нижней полоски")]
            public string footline;

            [JsonProperty("Цвет текста")]
            public string colortext;

            [JsonProperty("Размер текста")]
            public string sizetext;

            [JsonProperty("Время капсом?")]
            public bool timeupper;

            [JsonProperty("Расположение")]
            public POSITION position;
        }

        class MARKER
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Цвет маркера")]
            public string color1;

            [JsonProperty("Цвет обводки")]
            public string color2;

            [JsonProperty("Прозрачность")]
            public float alfa;

            [JsonProperty("Отображать круг?")]
            public bool circle;

            [JsonProperty("Отображать маркером взрыва?")]
            public bool boom;
        }

        class BLOCK
        {
            [JsonProperty("Телепорт")]
            public bool tp;

            [JsonProperty("Киты")]
            public bool kits;

            [JsonProperty("Трейд")]
            public bool trade;

            [JsonProperty("Строительство")]
            public bool build;

            [JsonProperty("Ремонт/улучшение/ремув - не плагином")]
            public bool ingame;

            [JsonProperty("Команды")]
            public string[] commands;

            [JsonProperty("Сообщение о блоке")]
            public string text;

            [JsonProperty("Можно строить/устанавливать во время блокировки [prefabId]")]
            public uint[] whitelist;
        }

        class VK
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("API от группы")]
            public string api;

            [JsonProperty("Текст")]
            public string text;

            [JsonProperty("Кд на отправку")]
            public float cooldown;

            [JsonProperty("Сообщение при входе игрока на сервер, при условии, что он не присоеденил свой вк")]
            public string message;
        }

        class Discord
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Текст")]
            public string text;

            [JsonProperty("Кд на отправку")]
            public float cooldown;
        }

        class COMBATBLOCK
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Блокировать при попадании по игроку?")]
            public bool damageto;

            [JsonProperty("Блокировать при получении урона от игрока?")]
            public bool damagefrom;

            [JsonProperty("Блокировать команды")]
            public string[] blacklist;

            [JsonProperty("Текст")]
            public string text;

            [JsonProperty("Время блокировки")]
            public float blockseconds;

            [JsonProperty("Включить GUI?")]
            public bool enablegui;
        }

        class GAME
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Текст")]
            public string text;

            [JsonProperty("Кд на отправку")]
            public float cooldown;
        }

        enum MES { rnmain, rndelete, notallow, rnmainadded, rnaddcooldown, rnadd, rnconfirm, rncancel, rnnocode, rnnovk, rnnewvk, rnprivate, rnerror, rnblack, rnerror2 }

        private class PluginConfig
        {
            [JsonProperty("Время блокировки")]
            public int blockseconds;

            [JsonProperty("Название сервера - для оповещений")]
            public string servername;

            [JsonProperty("Радиус")]
            public float radius;

            [JsonProperty("Снимать блокировку если вышел из рейд-зоны?")]
            public bool blockremove;

            [JsonProperty("Сброс рейдблока при смерти?")]
            public bool removedeath;

            [JsonProperty("Рейдблок установливается даже если на территории нет шкафа?")]
            public bool cupboard;

            [JsonProperty("Настройка маркера на карте")]
            public MARKER marker;

            [JsonProperty("Настройка GUI")]
            public GUI gui;

            [JsonProperty("Настройка блокировки")]
            public BLOCK block;

            [JsonProperty("Команда")]
            public string command;

            [JsonProperty("Настройка комбатблока")]
            public COMBATBLOCK combatblock;

            [JsonProperty("Оповещение о рейде в игре")]
            public GAME GAME;

            [JsonProperty("Оповещание о рейде в ВК")]
            public VK vk;

            [JsonProperty("Оповещание о рейде в Дискорд")]
            public Discord discord;

            [JsonProperty("Сообщения")]
            public Dictionary<MES, string> messages;

            [JsonProperty("Названия - для оповещаний")]
            public Dictionary<string, string> names;

            [JsonProperty("Дополнительный список на что кидать РБ")]
            public string[] spisok;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    blockseconds = 120,
                    radius = 75f,
                    blockremove = true,
                    servername = "HaxLite X10",
                    cupboard = false,
                    removedeath = false,
                    gui = new GUI
                    {
                        background = "0.4842625 0.1774008 0.1774008 0.3960784",
                        colortext = "1 1 1 0.7",
                        sizetext = "18",
                        timeupper = true,
                        footline = "0.9442612 0.5032899 0.5032899 1",
                        text = "БЛОКИРОВКА НА {time}",
                        position = new POSITION
                        {
                            zero = "0.5 0",
                            offsetmin = "-200 85",
                            offsetmax = "180 107"
                        }
                    },
                    marker = new MARKER
                    {
                        alfa = 0.6f,
                        color1 = "#FF0000",
                        color2 = "#000000",
                        enable = true,
                        boom = true,
                        circle = true
                    },
                    block = new BLOCK
                    {
                        build = true,
                        kits = true,
                        trade = true,
                        ingame = true,
                        tp = true,
                        commands = new string[] { "oo", "duel" },
                        whitelist = new uint[] { 2335812770, 2057881102, 1206527181, 2089327217, 2150203378 },
                        text = "<color=yellow>Вы находитесь в зоне рейд-блока!</color>"
                    },
                    vk = new VK
                    {
                        api = "",
                        cooldown = 1200f,
                        enable = true,
                        text = "Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}\nconnect {ip}",
                        message = "Вы не добавили свой Вк для оповещений о рейде\nВы можете это сделать командой <color=yellow>/rn add vk.com/ID</color>"
                    },
                    discord = new Discord
                    {
                        cooldown = 1200f,
                        enable = true,
                        text = "```Внимание! Игрок {name} разрушил {destroy} в квадрате {quad}\nconnect {ip}```"
                    },
                    combatblock = new COMBATBLOCK
                    {
                        enable = true,
                        blockseconds = 30f,
                        damagefrom = true,
                        damageto = true,
                        blacklist = new string[] { "tpr", "home", "tpa", "oo" },
                        text = "<color=yellow>Вы недавно стрелялись с другим игроком!</color>\nВы сможете использовать эту команду через <color=yellow>{time}</color>",
                        enablegui = true
                    },
                    GAME = new GAME
                    {
                        enable = true,
                        cooldown = 300f,
                        text = "<color=yellow>ВНИМАНИЕ! ВАШ ДОМ РЕЙДИТ ИГРОК {name}! КВАДРАТ {quad}</color>"
                    }, messages = new Dictionary<MES, string>
                    {
                        { MES.notallow, "У вас нет доступа к этой команде!" },
                        { MES.rnmain, "Что бы добавить оповещание о рейде в <color=yellow>ВК</color>.\nНапишите в чат: <color=yellow>/rn add vk.com/ID</color>\nПример: <color=yellow>/rn add vk.com/fermenspwnz</color>"},
                        { MES.rnmainadded, "Ваш ВК указан как: <color=#c6ec79>vk.com/{value}</color>\n<color=yellow>/rn delete</color> - отвязать ВК"},
                        { MES.rnaddcooldown, "Отправить новый код вы сможете через {time}"},
                        { MES.rnadd, "Введите в игре /rn accept {num}, для подтверджения аккаунта." },
                        { MES.rnconfirm, "<color=#c6ec79>Отлично! Ваш VK подтвержден!</color>"},
                        { MES.rncancel, "<color=yellow>Не верный код!</color>"},
                        { MES.rnnocode, "<color=yellow>Вы не указали код!</color>"},
                        { MES.rndelete, "<color=#c6ec79>Ваш VK успешно отвязан от игрового аккаунта!</color>"},
                        { MES.rnnovk, "<color=yellow>У вас нет привязаного к игровому аккаунту ВК!</color>"},
                        { MES.rnnewvk, "Вы указали VK: <color=yellow>{id}</color>\nВам в VK отправлено сообщение с кодом.\n<color=yellow>/rn accept <код></color> - подтвердить авторизацию."},
                        { MES.rnprivate, "Ваши настройки приватности не позволяют отправить вам сообщение (<color=#a2d953>{id}</color>)"},
                        { MES.rnerror, "Невозможно отправить сообщение.\nПроверьте правильность ссылки (<color=#a2d953>{id}</color>) или повторите попытку позже."},
                        { MES.rnblack, "Невозможно отправить сообщение.\nВы добавили группу в черный список или не подписаны на нее, если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз."},
                        { MES.rnerror2, "Вы указали неверный VK ID (<color=#a2d953>{id}</color>), если это не так, то просто напишите в группу сервера любое сообщение и попробуйте еще раз."}
                    },
                    names = _names,
                    spisok = _spisok,
                    command = "rn"
                };
            }
        }
        #endregion

        #region БЛОККОМАНД
        private object OnServerCommand(ConsoleSystem.Arg arg) => blocker(arg.Player(), arg.cmd.FullName);
        
        private object OnUserCommand(IPlayer ipplayer, string com, string[] args)
        {
            com = com.TrimStart('/').Substring(com.IndexOf(".", StringComparison.Ordinal) + 1);
            BasePlayer player = BasePlayer.Find(ipplayer.Id);
            return blocker(player, com);
        }

        private object blocker(BasePlayer player, string command)
        {
            if (player == null) return null;

            if (IsBlock.ContainsKey(player.userID))
            {
                if (config.block.commands.Contains(command))
                {
                    player.ChatMessage(config.block.text);
                    return false;
                }
            }

            COMBATBK cOMBATBK;
            if (HasCombatBlock(player) && player.TryGetComponent<COMBATBK>(out cOMBATBK) && cOMBATBK.tick > 0)
            {
                if (config.combatblock.blacklist.Contains(command))
                {
                    player.ChatMessage(config.combatblock.text.Replace("{time}", FormatTime(TimeSpan.FromSeconds(cOMBATBK.tick)).ToLower()));
                    return false;
                }
            }

            return null;
        }
        #endregion

        #region КОМБАТБЛОК
        private static List<ulong> combatblock = new List<ulong>();
        private bool HasCombatBlock(BasePlayer player)
        {
            return combatblock.Contains(player.userID) ? true : false;
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info == null || info.HitEntity == null || IsNPC(attacker)) return;
            if (info.HitEntity is BasePlayer)
            {
                BasePlayer target = info.HitEntity.ToPlayer();
                if (target == null || IsNPC(target)) return;
                int block = (int)config.combatblock.blockseconds;
                if (config.combatblock.damageto)
                {
                    ADDCOMBATBLOCK(attacker, block);
                }
                if (config.combatblock.damagefrom)
                {
                    ADDCOMBATBLOCK(target, block);
                }
            }
        }

        private void ADDCOMBATBLOCK(BasePlayer player, int time, bool raidblock = false)
        {
            if (!raidblock && IsBlock.ContainsKey(player.userID)) return;
            COMBATBK cOMBATBK;
            if (!player.TryGetComponent<COMBATBK>(out cOMBATBK))
            {
                cOMBATBK = player.gameObject.AddComponent<COMBATBK>();
            }

            if(time > cOMBATBK.tick) cOMBATBK.tick = time;
            if (raidblock == true) cOMBATBK.ADDRAID();
        }

        class COMBATBK : MonoBehaviour
        {
            BasePlayer player;
            public int tick;
            public bool raidblock;
            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                if (player == null)
                {
                    Destroy(this);
                    return;
                }
                raidblock = false;
                if (!combatblock.Contains(player.userID)) combatblock.Add(player.userID);
                InvokeRepeating(nameof(TICK), 1f, 1f);
            }

            public void ADDRAID()
            {
                if (ins.IsBlock.ContainsKey(player.userID)) ins.IsBlock.Remove(player.userID);
                ins.IsBlock.Add(player.userID, null);
                raidblock = true;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "RAIDFONE");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", GUIJSON.Replace("{text}", config.gui.text.Replace("{time}", FormatTime(TimeSpan.FromSeconds(tick)))));
            }

            private void TICK()
            {
                tick--;
                GameObject gameObject; 
                if (tick <= 0 || !raidblock && ins.IsBlock.TryGetValue(player.userID, out gameObject) && gameObject != null)
                {
                    Destroy(this);
                    return;
                }
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "RAIDFONE");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", GUIJSON.Replace("{text}", config.gui.text.Replace("{time}", FormatTime(TimeSpan.FromSeconds(tick)))));
            }

            public void DoDestroy() => Destroy(this);

            private void OnDestroy()
            {
                if (IsInvoking(nameof(TICK))) CancelInvoke(nameof(TICK));
                if(combatblock.Contains(player.userID)) combatblock.Remove(player.userID);
                GameObject x;
                if (ins.IsBlock.TryGetValue(player.userID, out x))
                {
                    if (raidblock && x == null)
                    {
                        ins.IsBlock.Remove(player.userID);
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "RAIDFONE");
                    }
                }
                else
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "RAIDFONE");
                }
            }
        }


        /*
                private void REMOVECOMBATBLOCK(BasePlayer player)
                {
                    COMBATBLOCKER cOMBATBLOCKER;
                    if (!combatblock.TryGetValue(player, out cOMBATBLOCKER)) return;
                    if (!cOMBATBLOCKER.ttime.Destroyed) cOMBATBLOCKER.ttime.Destroy();
                    if (!IsBlock.Contains(player.userID)) CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "RAIDFONE");
                }*/
        #endregion

        #region ИГРОК ВЫШЕЛ ИЗ СЕРВЕРА

        Dictionary<BasePlayer, GameObject> disconnected = new Dictionary<BasePlayer, GameObject>();

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            COMBATBK cOMBATBK;
            if (player.TryGetComponent<COMBATBK>(out cOMBATBK)) cOMBATBK.DoDestroy();
            GameObject gameObject;
            if (!IsBlock.TryGetValue(player.userID, out gameObject) || gameObject == null) return;
            ZONE zONE;
            if (!gameObject.TryGetComponent<ZONE>(out zONE)) return;
            disconnected[player] = gameObject;
            zONE.RemovePlayer(player);
        }
        #endregion

        #region GRID
        private static Dictionary<string, Vector3> Grids = new Dictionary<string, Vector3>();
        private void CreateSpawnGrid()
        {
            Grids.Clear();
            var worldSize = (ConVar.Server.worldsize);
            float offset = worldSize / 2;
            var gridWidth = (0.0066666666666667f * worldSize);
            float step = worldSize / gridWidth;

            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Grids.Add($"{start}{letter}{number}", new Vector3(xx - 55f, 0, zz + 20f));
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
        }

        private string GetNameGrid(Vector3 pos)
        {
            return Grids.Where(x => x.Value.x < pos.x && x.Value.x + 150f > pos.x && x.Value.z > pos.z && x.Value.z - 150f < pos.z).FirstOrDefault().Key;
        }
        #endregion

        #region CONSOLECOMMAND
        [ConsoleCommand("vkintegra")]
        private void Cmdvkintegra(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            JObject vks = Interface.Oxide.DataFileSystem.ReadObject<JObject>("VKBotUsers");
            if (vks == null)
            {
                arg.ReplyWith("ДатаФайл VKBotUsers отсутсвует или пуст!");
            }
            int i = 0;
            foreach (var z in vks["VKUsersData"])
            {
                foreach (JObject obj in z)
                {
                    ulong userid = (ulong)obj["UserID"];
                    if (!VkPlayers.ContainsKey(userid))
                    {
                        VkPlayers.Add(userid, "id" + (string)obj["VkID"]);
                        i++;
                    }
                }
            }
            arg.ReplyWith($"Добавили {i} юзеров.");
            SaveVK();
        }
        #endregion
        private static RaidZone ins;
        private void Init()
        {
            ins = this;
            Unsubscribe(nameof(OnPlayerAttack));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(CanAffordUpgrade));
            Unsubscribe(nameof(OnStructureRepair));
            Unsubscribe(nameof(OnStructureDemolish));
        }

        private void OnServerInitialized()
        {
            if (config.spisok == null)
            {
                config.marker.circle = true;
                config.marker.boom = true;
                config.vk.message = "Вы не добавили свой Вк для оповещений о рейде\nВы можете это сделать командой <color=yellow>/rn add vk.com/ID</color>";
                config.spisok = _spisok;
                SaveConfig();
            }

            if (string.IsNullOrEmpty(config.command))
            {
                config.command = "rn";
                SaveConfig();
            }

            Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddChatCommand(config.command, this, "callcommandrn");

            #region НАСТРОЙКА КАПСА
            if (config.gui.timeupper)
            {
                m0 = m0.ToUpper();
                m1 = m1.ToUpper();
                m2 = m2.ToUpper();
                s0 = s0.ToUpper();
                s1 = s1.ToUpper();
                s2 = s2.ToUpper();
            }
            else
            {
                m0 = m0.ToLower();
                m1 = m1.ToLower();
                m2 = m2.ToLower();
                s0 = s0.ToLower();
                s1 = s1.ToLower();
                s2 = s2.ToLower();
            }
            #endregion

            #region НАСТРОЙКА GUI
            string raidtext = "{\"name\":\"RAIDTEXT\",\"parent\":\"RAIDFONE\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":{sizetext},\"align\":\"MiddleCenter\",\"color\":\"{colortext}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}".Replace("{sizetext}", config.gui.sizetext).Replace("{colortext}", config.gui.colortext);
            GUITEXT = "[" + raidtext + "]";
            GUIJSON = "[{\"name\":\"RAIDFONE\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{background}\"},{\"type\":\"RectTransform\",\"anchormin\":\"{zero}\",\"anchormax\":\"{zero}\",\"offsetmin\":\"{offsetmin}\",\"offsetmax\":\"{offsetmax}\"}]},{\"name\":\"BOTTOMSHIT\",\"parent\":\"RAIDFONE\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{footline}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0.05\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{raidtext}]".Replace("{background}", config.gui.background).Replace("{footline}", config.gui.footline).Replace("{zero}", config.gui.position.zero).Replace("{offsetmax}", config.gui.position.offsetmax).Replace("{offsetmin}", config.gui.position.offsetmin).Replace("{raidtext}", raidtext);
            #endregion

            #region НАСТРОЙКА МАРКЕРА
            if (!ColorUtility.TryParseHtmlString(config.marker.color1, out COLOR1))
            {
                Debug.LogError("ЦВЕТ МАРКЕРА НЕ В ФОРМАТЕ HEX!");
            }

            if (!ColorUtility.TryParseHtmlString(config.marker.color2, out COLOR2))
            {
                Debug.LogError("ЦВЕТ ОБВОДКИ МАРКЕРА НЕ В ФОРМАТЕ HEX!");
            }
            #endregion

            #region НАСТРОЙКА БЛОКИРОВКИ
            if (config.block.ingame)
            {
                Subscribe(nameof(CanAffordUpgrade));
                Subscribe(nameof(OnStructureRepair));
                Subscribe(nameof(OnStructureDemolish));
            }
            #endregion

            #region НАСТРОЙКА КОМБАТБЛОКА
            if (config.combatblock.enable) Subscribe(nameof(OnPlayerAttack));
            #endregion

            #region НАСТРОЙКА ОПОВЕЩЕНИЯ О РЕЙДЕ
            VkPlayers = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("raidzone_vk");
            permission.RegisterPermission(permvk, this);
            if (config.vk.enable && string.IsNullOrEmpty(config.vk.api))
            {
                Debug.LogError("ВЫ ВКЛЮЧИЛИ ОПОВЕЩЕНИЕ ДЛЯ ВК, НО ПРИ ЭТОМ НЕ УКАЗАЛИ API ОТ ГРУППЫ!");
            }
            #endregion

            if (config.removedeath) Subscribe(nameof(OnPlayerDeath));

            if (config.names == null)
            {
                config.names = _names;
                SaveConfig();
            }
            CreateSpawnGrid();
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            timer.Once(0.1f, () =>
            {
                if (!player.IsConnected) return;
                COMBATBK cOMBATBK;
                if (player.TryGetComponent<COMBATBK>(out cOMBATBK)) cOMBATBK.DoDestroy();
                GameObject gameObject;
                if (!IsBlock.TryGetValue(player.userID, out gameObject) || gameObject == null) return;
                ZONE zONE;
                if (!gameObject.TryGetComponent<ZONE>(out zONE)) return;
                zONE.RemovePlayer(player);
            });
        }

        #region ОПОВЕЩЕНИЕ О РЕЙДЕ
        [PluginReference] private Plugin DiscordCore, HaxBot;
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info, Item item)
        {
            if (info == null || entity == null) return;
            BasePlayer player = info.InitiatorPlayer;
            if (player == null) return;
            if (entity is BuildingBlock)
            {
                int tt = (int)(entity as BuildingBlock).grade;
                if (tt <= 0) return;
                ServerMgr.Instance.StartCoroutine(GORAID(entity, player, tt));
            }
            else if(entity is AnimatedBuildingBlock || entity is SamSite || entity is AutoTurret || entity is DecayEntity && config.spisok.Contains(entity.ShortPrefabName))
            {
                ServerMgr.Instance.StartCoroutine(GORAID(entity, player));
            }
        }

        private IEnumerator GORAID(BaseCombatEntity entity, BasePlayer player, int tt = 0)
        {
            Vector3 position = entity.transform.position;
            string dname = entity.ShortPrefabName;
            if (tt == 1) dname += " Wood";
            else if (tt == 2) dname += " Stone";
            else if (tt == 3) dname += " Metal";
            else if (tt == 4) dname += " TopTier";
            BuildingPrivlidge priv = entity.GetBuildingPrivilege(entity.WorldSpaceBounds());
            yield return new WaitForEndOfFrame();
            if (priv != null && !priv.authorizedPlayers.Any(x => x.userid == player.userID))
            {
                CreateTrigger(position, config.blockseconds);
                yield return new WaitForEndOfFrame();
                string name = player.displayName;
                string quad = GetNameGrid(position);
                string connect = ConVar.Server.ip + ":" + ConVar.Server.port;

                string destroy;
                if (!config.names.TryGetValue(dname, out destroy))
                {
                    config.names.Add(dname, dname);
                    destroy = dname;
                    SaveConfig();
                }

                foreach (var z in priv.authorizedPlayers)
                {
                    ALERTPLAYER(z.userid, name, quad, connect, destroy);
                    yield return new WaitForEndOfFrame();
                }
            }
            else if (priv == null && config.cupboard)
            {
                CreateTrigger(position, config.blockseconds);
            }
            yield break;
        }

        class ALERT
        {
            public DateTime gamecooldown;
            public DateTime discordcooldown;
            public DateTime vkcooldown;
            public DateTime vkcodecooldown;
        }

        private static Dictionary<ulong, ALERT> alerts = new Dictionary<ulong, ALERT>();
        private static Dictionary<ulong, string> VkPlayers = new Dictionary<ulong, string>();

        private void ALERTPLAYER(ulong ID, string name, string quad, string connect, string destroy)
        {
            ALERT alert;
            if(!alerts.TryGetValue(ID, out alert))
            {
                alerts.Add(ID, new ALERT());
                alert = alerts[ID];
            }

            #region ОПОВЕЩЕНИЕ В ИГРЕ
            if (config.GAME.enable && alert.gamecooldown < DateTime.Now)
            {
                BasePlayer player = BasePlayer.FindByID(ID);
                if (player != null && player.IsConnected)
                {
                    player.ChatMessage(config.GAME.text.Replace("{name}", name).Replace("{quad}", quad).Replace("{destroy}", destroy));
                    alert.gamecooldown = DateTime.Now.AddSeconds(config.GAME.cooldown);
                }
            }
            #endregion
            #region ОПОВЕЩЕНИЕ В ДИСКОРДЕ
            if (config.discord.enable && alert.discordcooldown < DateTime.Now)
            {
                if (HaxBot != null) HaxBot.Call("SENDMESSAGE", ID, config.discord.text.Replace("{ip}", connect).Replace("{destroy}", destroy).Replace("{name}", name).Replace("{quad}", quad).Replace("{servername}", config.servername));
                else if (DiscordCore != null) DiscordCore.Call("SendMessageToUser", ID.ToString(), config.discord.text.Replace("{ip}", connect).Replace("{destroy}", destroy).Replace("{name}", name).Replace("{quad}", quad).Replace("{servername}", config.servername));
                alert.discordcooldown = DateTime.Now.AddSeconds(config.discord.cooldown);
            }
            #endregion
            #region ОПОВЕЩЕНИЕ В ВК
            if (config.vk.enable && alert.vkcooldown < DateTime.Now)
            {
                string vkid;
                if (VkPlayers.TryGetValue(ID, out vkid))
                {
                    GetRequest(vkid, config.vk.text.Replace("{ip}", connect).Replace("{name}", name).Replace("{destroy}", destroy).Replace("{quad}", quad).Replace("{servername}", config.servername));
                    alert.vkcooldown = DateTime.Now.AddSeconds(config.vk.cooldown);
                }
            }
            #endregion
        }

        private object CanBuild(Planner plan, Construction prefab)
        {
            BasePlayer player = plan.GetOwnerPlayer();
           // Debug.Log(prefab.fullName + " - " + prefab.prefabID);
            if (player == null || !HasBlock(player.userID) || config.block.whitelist.Contains(prefab.prefabID) || prefab.fullName.Contains("assets/prefabs/building core/")) return null;
            player.ChatMessage(config.block.text);
            return false;
        }

        private void CreateTrigger(Vector3 position, int time)
        {
            ZONE oNE = GETZONE(position);
            if (oNE != null)
            {
                oNE.Refresh(config.blockseconds);
                return;
            }
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = position;
            TriggerBase trigger = sphere.GetComponent<TriggerBase>() ?? sphere.gameObject.AddComponent<TriggerBase>();
            trigger.interestLayers = LayerMask.GetMask("Player (Server)");
            trigger.enabled = true;
            ZONE zONE = sphere.AddComponent<ZONE>();
            zONE.START(config.radius, time);
        }

        #region ВК
        class CODE
        {
            public string id;
            public ulong gameid;
        }

        private static Dictionary<string, CODE> VKCODES = new Dictionary<string, CODE>();

        private void callcommandrn(BasePlayer player, string command, string[] arg)
        {
            if (!config.vk.enable) return;
            bool vkaccess = permission.UserHasPermission(player.UserIDString, permvk);

            if (!vkaccess)
            {
                player.ChatMessage(config.messages[MES.notallow]);
                return;
            }
            if(arg == null || arg.Length == 0)
            {
                string vkid;
                if (!VkPlayers.TryGetValue(player.userID, out vkid))
                {
                    player.ChatMessage(config.messages[MES.rnmain]);
                }
                else
                {
                    player.ChatMessage(config.messages[MES.rnmainadded].Replace("{value}", vkid));
                }
                return;
            }
            string command1 = arg[0].ToLower();
            if (command1 == "add")
            {
                if (arg.Length > 1)
                {
                    ALERT aLERT;
                    if (alerts.TryGetValue(player.userID, out aLERT) && aLERT.vkcodecooldown > DateTime.Now)
                    {
                        player.ChatMessage(config.messages[MES.rnaddcooldown].Replace("{time}", FormatTime(aLERT.vkcodecooldown - DateTime.Now).ToLower()));
                        return;
                    }

                    string vkid = arg[1].ToLower().Replace("vk.com/", "").Replace("https://", "").Replace("http://", "");
                    string num = RANDOMNUM();
                    GetRequest(vkid, config.messages[MES.rnadd].Replace("{num}", num), player, num);
                }
            }else if (command1 == "accept")
            {
                if (arg.Length > 1)
                {
                    CODE cODE;
                    if (VKCODES.TryGetValue(arg[1], out cODE) && cODE.gameid == player.userID)
                    {
                        string vkid;
                        if(VkPlayers.TryGetValue(player.userID, out vkid))
                        {
                            vkid = cODE.id;
                        }
                        else
                        {
                            VkPlayers.Add(player.userID, cODE.id);
                        }
                        VKCODES.Remove(arg[1]);
                        player.ChatMessage(config.messages[MES.rnconfirm]);
                        SaveVK();
                    }
                    else
                    {
                        player.ChatMessage(config.messages[MES.rncancel]);
                    }
                }
                else
                {
                    player.ChatMessage(config.messages[MES.rnnocode]);
                }
            }
            else if (command1 == "delete")
            {
                if (VkPlayers.ContainsKey(player.userID))
                {
                    VkPlayers.Remove(player.userID);
                    player.ChatMessage(config.messages[MES.rndelete]);
                }
                else
                {
                    player.ChatMessage(config.messages[MES.rnnovk]);
                }
            }
        }

        private void GetRequest(string reciverID, string msg, BasePlayer player = null, string num = null) => webrequest.Enqueue("https://api.vk.com/method/messages.send?domain=" + reciverID + "&message=" + msg.Replace("#", "%23") + "&v=5.80&access_token=" + config.vk.api, null, (code2, response2) => ServerMgr.Instance.StartCoroutine(GetCallback(code2, response2, reciverID, player, num)), this);
        
        private IEnumerator GetCallback(int code, string response, string id, BasePlayer player = null, string num = null)
        {
            if (player == null) yield break;
            if (response == null || code != 200)
            {
                ALERT alert;
                if (alerts.TryGetValue(player.userID, out alert)) alert.vkcooldown = DateTime.Now;
                Debug.Log("НЕ ПОЛУЧИЛОСЬ ОТПРАВИТЬ СООБЩЕНИЕ В ВК! => обнулили кд на отправку");
                yield break;
            }
            yield return new WaitForEndOfFrame();
            if (!response.Contains("error"))
            {
                ALERT aLERT;
                if (alerts.TryGetValue(player.userID, out aLERT))
                {
                    aLERT.vkcodecooldown = DateTime.Now.AddMinutes(10);
                }
                else
                {
                    alerts.Add(player.userID, new ALERT {vkcodecooldown = DateTime.Now.AddMinutes(10) });
                }
                if (VKCODES.ContainsKey(num)) VKCODES.Remove(num);
                VKCODES.Add(num, new CODE { gameid = player.userID, id = id });
                player.ChatMessage(config.messages[MES.rnnewvk].Replace("{id}", id));
            }
            else if (response.Contains("PrivateMessage"))
            {
                player.ChatMessage(config.messages[MES.rnprivate].Replace("{id}", id));
            }
            else if(response.Contains("ErrorSend"))
            {
                player.ChatMessage(config.messages[MES.rnerror].Replace("{id}", id));
            }
            else if(response.Contains("BlackList"))
            {
                player.ChatMessage(config.messages[MES.rnblack]);
            }
            else
            {
                player.ChatMessage(config.messages[MES.rnerror2].Replace("{id}", id));
            }
            yield break;
        }
        #endregion
        #endregion

        #region HEADER
        private const string permvk = "raidzone.vk";
        private const string genericPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string raidPrefab = "assets/prefabs/tools/map/explosionmarker.prefab";
        private static string GUIJSON = "";
        private static string GUITEXT = "";
        private static Color COLOR1;
       
        private static Color COLOR2;
        private Dictionary<ulong, GameObject> IsBlock = new Dictionary<ulong, GameObject>();
        private static List<MapMarkerGenericRadius> mapMarkerGenericRadii = new List<MapMarkerGenericRadius>();
        #endregion

        #region ZONE - КЛАСС
        class ZONE : MonoBehaviour
        {
            private MapMarkerGenericRadius generic;
            private MapMarkerExplosion explosion;
            private SphereCollider sphere;
            private List<Network.Connection> ZONEPLAYERS = new List<Network.Connection>();

            public int seconds;

            void Awake()
            {
                sphere = GetComponent<SphereCollider>();
                if (sphere == null)
                {
                    Destroy(this);
                    Debug.Log("sphere null");
                    return;
                }
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "RaidZone";
                sphere.radius = config.radius;
                sphere.isTrigger = true;
                sphere.enabled = true;
            }

            public void START(float radius, int time)
            {
                if (sphere == null)
                {
                    Destroy(this);
                    Debug.Log("sphere null");
                    return;
                }

                seconds = time;
                InvokeRepeating(nameof(OneSecond), 0f, 1f);

                if (config.marker.enable)
                {
                    if (config.marker.boom)
                    {
                        explosion = (MapMarkerExplosion)GameManager.server.CreateEntity(raidPrefab, sphere.transform.position);
                        explosion.SetDuration(time);
                        explosion.enableSaving = false;
                        explosion.Spawn();
                        explosion.SendNetworkUpdate();
                    }

                    if (config.marker.circle)
                    {
                        generic = (MapMarkerGenericRadius)GameManager.server.CreateEntity(genericPrefab, sphere.transform.position);
                        generic.color1 = COLOR1;
                        generic.color2 = COLOR2;
                        generic.radius = radius / 145f;
                        generic.alpha = config.marker.alfa;
                        generic.enableSaving = false;
                        generic.Spawn();
                        generic.SendUpdate();
                        mapMarkerGenericRadii.Add(generic);
                    }
                }
            }

            public void Refresh(int time)
            {
                seconds = time;
            }

            private void OneSecond()
            {
                if (seconds <= 0)
                {
                    DoDestroy();
                    return;
                }

                string GUI = GUITEXT.Replace("{text}", config.gui.text.Replace("{time}", FormatTime(TimeSpan.FromSeconds(seconds))));
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = ZONEPLAYERS }, null, "DestroyUI", "RAIDTEXT");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = ZONEPLAYERS }, null, "AddUI", GUI);
                seconds--;
            }

            public void AddPlayer(BasePlayer player)
            {
                if (!player.IsConnected || ZONEPLAYERS.Contains(player.net.connection)) return;

              /*  COMBATBK cOMBATBK;
                if (player.TryGetComponent<COMBATBK>(out cOMBATBK))
                {
                    cOMBATBK.DoDestroy();
                }*/

                GameObject x;
                if (ins.IsBlock.TryGetValue(player.userID, out x))
                {
                    if (!config.blockremove && x != null && x != gameObject)
                    {
                        ZONE zONE = x.GetComponent<ZONE>();
                        zONE.RemovePlayer(player, false);
                    }
                    else
                    {
                        ins.IsBlock.Remove(player.userID);
                    }
                }

                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "RAIDFONE");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", GUIJSON.Replace("{text}", config.gui.text.Replace("{time}", FormatTime(TimeSpan.FromSeconds(seconds)))));
                ZONEPLAYERS.Add(player.net.connection);
                ins.IsBlock.Add(player.userID, gameObject);
             //   Debug.Log(player.displayName + " добавили");
            }

            public void RemovePlayer(BasePlayer player, bool newzone = true)
            {
                if (!player.IsConnected || !ZONEPLAYERS.Contains(player.net.connection)) return;
                ZONEPLAYERS.Remove(player.net.connection);
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "RAIDFONE");
                if (ins.IsBlock.ContainsKey(player.userID)) ins.IsBlock.Remove(player.userID);
                // Debug.Log(player.displayName + " удалили");
                if(newzone) Invoke(nameof(CHECKNEWZONE), 0.1f);
            }

            private void CHECKNEWZONE(BasePlayer player)
            {
                if (player == null || player.IsDead() || !player.IsConnected) return;
                ZONE zONE = GETZONE(player.transform.position);
                if (zONE == null) return;
                COMBATBK cOMBATBK;
                if (player.TryGetComponent<COMBATBK>(out cOMBATBK))
                {
                    cOMBATBK.DoDestroy();
                }
                zONE.AddPlayer(player);
            }

            public void DoDestroy()
            {
                UnityEngine.GameObject.Destroy(gameObject);
                Destroy(this);
            }

            private void OnDestroy()
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = ZONEPLAYERS }, null, "DestroyUI", "RAIDFONE");
                foreach(Network.Connection connection in ZONEPLAYERS)
                {
                    if (ins.IsBlock.ContainsKey(connection.userid)) ins.IsBlock.Remove(connection.userid);
                }
                ZONEPLAYERS.Clear();
                if (config.marker.enable)
                {
                    if(config.marker.circle) RemoveGeneric(generic);
                    if (config.marker.boom && !explosion.IsDestroyed) explosion.Kill();
                }
            }
        }

        private void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        {
            if (trigger == null || trigger.name != "RaidZone") return;
            BasePlayer player = entity.ToPlayer();
            if (player == null || IsNPC(player)) return;
            ZONE zONE = trigger.GetComponent<ZONE>();
            if (zONE == null) return;
            COMBATBK cOMBATBK;
            if (player.TryGetComponent<COMBATBK>(out cOMBATBK))
            {
                cOMBATBK.DoDestroy();
            }
            zONE.AddPlayer(player);
        }

        private void OnEntityLeave(TriggerBase trigger, BaseEntity entity)
        {
            if (trigger == null || trigger.name != "RaidZone") return;
            BasePlayer player = entity.ToPlayer();
            if (player == null || IsNPC(player)) return;

            ZONE zONE = trigger.GetComponent<ZONE>();
            if (zONE == null) return;

            if (!config.blockremove) 
            {
                zONE.RemovePlayer(player, false);
                NextTick(() =>
                {
                    ADDCOMBATBLOCK(player, zONE.seconds, true);
                });
            }
            else
            {
                zONE.RemovePlayer(player);
            }
        }
        #endregion

        #region ПОРНО
        private bool HasBlock(ulong ID)
        {
            return IsBlock.ContainsKey(ID);
        }

        private bool HasBlockTera(Vector3 position)
        {
            return GETZONE(position) != null ? true : false;
        }

        private string CanTeleport(BasePlayer player)
        {
            if(!config.block.tp) return null;
            if (!HasBlock(player.userID)) return null;
            return config.block.text;
        }

        private string canTeleport(BasePlayer player)
        {
            if (!config.block.tp) return null;
            if (!HasBlock(player.userID)) return null;
            return config.block.text;
        }

        private int? CanBGrade(BasePlayer player, int grade, BuildingBlock block, Planner plan)
        {
            if (!HasBlock(player.userID)) return null;
            player.ChatMessage(config.block.text);
            return 0;
        }

        private string CanTrade(BasePlayer player)
        {
            if (!config.block.trade) return null;
            if (!HasBlock(player.userID)) return null;
            return config.block.text;
        }

        private string canRemove(BasePlayer player)
        {
            if (!HasBlock(player.userID)) return null;
            return config.block.text;
        }

        object canRedeemKit(BasePlayer player)
        {
            if (!config.block.kits) return null;
            if (!HasBlock(player.userID)) return null;
            return config.block.text;
        }

        private bool? CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (!HasBlock(player.userID)) return null;
            player.ChatMessage(config.block.text);
            return false;
        }

        private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (!HasBlock(player.userID)) return null;
            player.ChatMessage(config.block.text);
            return false;
        }

        private bool? OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (!HasBlock(player.userID)) return null;
            player.ChatMessage(config.block.text);
            return false;
        }

        object OnStructureDemolish(BaseCombatEntity entity, BasePlayer player)
        {   
            if(player == null || !HasBlock(player.userID)) return null;
            player.ChatMessage(config.block.text);
            return null;
        }
        #endregion

        #region ХЕЛПЕРЫ
        private static ZONE GETZONE(Vector3 position)
        {
            List<SphereCollider> sphereColliders = new List<SphereCollider>();
            Vis.Colliders(position, 0.1f, sphereColliders);
            if (sphereColliders.Count > 0)
            {
                foreach (var z in sphereColliders)
                {
                    ZONE oNE = z.gameObject.GetComponent<ZONE>();
                    if (oNE == null) continue;
                    return oNE;
                }
            }
            return null;
        }

        private static void RemoveGeneric(MapMarkerGenericRadius mapMarker)
        {
            if (mapMarkerGenericRadii.Contains(mapMarker)) mapMarkerGenericRadii.Remove(mapMarker);
            if (!mapMarker.IsDestroyed) mapMarker.Kill();
        }

        private string RANDOMNUM() => Random.Range(1000, 99999).ToString();
        #endregion

        #region DISMOUNT
        private void CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            NextTick(() =>
            {
                if (!player.IsConnected) return;
                ZONE zONE = GETZONE(player.transform.position);
                if (zONE == null) return;
                zONE.AddPlayer(player);
            });
        }
        #endregion

        private void SaveVK()
        {
            if (VkPlayers.Count > 0) Interface.Oxide.DataFileSystem.WriteObject("raidzone_vk", VkPlayers);
        }

        private void Unload()
        {
            SaveVK();
            foreach (var z in mapMarkerGenericRadii.ToList())
            {
                RemoveGeneric(z);
            }

            foreach(ZONE zONE in UnityEngine.Object.FindObjectsOfType<ZONE>().ToList())
            {
                zONE.DoDestroy();
            }

            foreach (COMBATBK zONE in UnityEngine.Object.FindObjectsOfType<COMBATBK>().ToList())
            {
                zONE.DoDestroy();
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "RAIDFONE");
            combatblock.Clear();
            IsBlock.Clear();
            VKCODES.Clear();
            alerts.Clear();
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            timer.Once(0.1f, () =>
            {
                if (!player.IsConnected) return;
                ZONE zONE = GETZONE(player.transform.position);
                if (zONE == null)
                {
                    if (config.blockremove)
                    {
                        GameObject x;
                        if (IsBlock.TryGetValue(player.userID, out x))
                        {
                            if (x == null) return;
                            ZONE zONE2 = x.GetComponent<ZONE>();
                            zONE2.RemovePlayer(player, false);
                        }
                    }
                    return;
                }
                zONE.AddPlayer(player);
            });
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player.IsConnected) return;

            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
            }
            if (config.vk.enable && !string.IsNullOrEmpty(config.vk.message) && permission.UserHasPermission(player.UserIDString, permvk) && !VkPlayers.ContainsKey(player.userID)) player.ChatMessage(config.vk.message);

            if (config.marker.enable) foreach (var z in mapMarkerGenericRadii) z.SendUpdate();

            GameObject gameObject;
            if(disconnected.TryGetValue(player, out gameObject) && gameObject != null)
            {
                ZONE zONE;
                if (!gameObject.TryGetComponent<ZONE>(out zONE)) return;
                zONE.AddPlayer(player);
                disconnected.Remove(player);
            }
        }

        private static bool IsNPC(BasePlayer player)
        {
            if (player is NPCPlayer) return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L)) return true;
            return false;
        }

        #region ВРЕМЯ
        private static string m0 = "МИНУТ";
        private static string m1 = "МИНУТЫ";
        private static string m2 = "МИНУТУ";

        private static string s0 = "СЕКУНД";
        private static string s1 = "СЕКУНДЫ";
        private static string s2 = "СЕКУНДУ";

        private static string FormatTime(TimeSpan time)
        => (time.Minutes == 0 ? string.Empty : FormatMinutes(time.Minutes)) + ((time.Seconds == 0) ? string.Empty : FormatSeconds(time.Seconds));

        private static string FormatMinutes(int minutes) => FormatUnits(minutes, m0, m1, m2);

        private static string FormatSeconds(int seconds) => FormatUnits(seconds, s0, s1, s2);

        private static string FormatUnits(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9 || tmp == 0)
                return $"{units} {form1} ";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2} ";

            return $"{units} {form3} ";
        }
        #endregion
    }
}
