using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using ru = Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("ArenaTournament", "discord: fermens#8767", "1.1.22")]
    [Description("Турнир на выбывание | Knockout tournament")]
    public class ArenaTournament : RustPlugin
    {
        const bool fermensEN = false;
        const bool debug = false;

        #region Config
        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
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

        class IITEM
        {
            [JsonProperty(PropertyName = fermensEN ? "Name" : "Название")]
            public string name;

            [JsonProperty(PropertyName = "Shortname")]
            public string shortname;

            [JsonProperty(PropertyName = fermensEN ? "SkinID" : "СкинID")]
            public ulong skin;

            [JsonProperty(PropertyName = fermensEN ? "Amount" : "Количество")]
            public int amount;

            [JsonProperty(PropertyName = fermensEN ? "Blueprint [true/false]" : "Рецепт [true/false]")]
            public bool blueprintTarget;
        }

        class SkinTeam
        {
            [JsonProperty(PropertyName = fermensEN ? "SkinId for team #1" : "Скин для команды #1")]
            public ulong skinteam1;

            [JsonProperty(PropertyName = fermensEN ? "SkinId for team #2" : "Скин для команды #2")]
            public ulong skinteam2;
        }

        class GUNITEM
        {
            [JsonProperty(PropertyName = fermensEN ? "Shortname <weapon>" : "Shortname <оружия>")]
            public string shortname;

            [JsonProperty(PropertyName = fermensEN ? "SkinID <weapon>" : "СкинID <оружия>")]
            public ulong skin;

            [JsonProperty(PropertyName = fermensEN ? "Wear [shortname|skinID]" : "Одежда [shortname|cкинID]")]
            public Dictionary<string, SkinTeam> wear = new Dictionary<string, SkinTeam>();

            [JsonProperty(PropertyName = fermensEN ? "Modules for weapons [shortname]" : "Модули на оружия [shortname]")]
            public List<string> moduls = new List<string>();

            [JsonProperty(PropertyName = fermensEN ? "Additional Items [shortname|amount]" : "Дополнительные предметы [shortname|количество]")]
            public Dictionary<string, int> additional = new Dictionary<string, int>();
        }

        class Reward
        {
            [JsonProperty(PropertyName = fermensEN ? "Command sent | message to player" : "Отправляемая команда | сообщение игроку")]
            public Dictionary<string, string> commands = new Dictionary<string, string>();

            [JsonProperty(PropertyName = fermensEN ? "Items" : "Предметы")]
            public List<IITEM> iTEMs = new List<IITEM>();
        }

        class SPEC
        {
            [JsonProperty(PropertyName = fermensEN ? "Wear [shortname|cкинID]" : "Одежда [shortname|скин]")]
            public Dictionary<string, ulong> wears = new Dictionary<string, ulong>();

            [JsonProperty(PropertyName = fermensEN ? "Additional Items [shortname|amount]" : "Дополнительные предметы [shortname|количество]")]
            public Dictionary<string, int> additional = new Dictionary<string, int>();
        }

        private class CurrentEvent
        {
            [JsonProperty(PropertyName = fermensEN ? "Position [coordinates]" : "Позиция [координаты]")]
            public Vector3 position;

            [JsonProperty(PropertyName = fermensEN ? "Arena radius" : "Радиус арены")]
            public float radius;

            [JsonProperty(PropertyName = fermensEN ? "Weapons" : "Оружия")]
            public List<GUNITEM> weapons = new List<GUNITEM>();

            [JsonProperty(PropertyName = fermensEN ? "Spectators" : "Зрители")]
            public SPEC sPEC;

            [JsonProperty(PropertyName = fermensEN ? "Awards [place|award]" : "Награды [место|награда]")]
            public Dictionary<int, Reward> reward;

            [JsonProperty(PropertyName = fermensEN ? "<xVSx> Tournament Modes" : "Режимы турниров <xVSx>")]
            public List<int> vs = new List<int>();

            //[JsonProperty(PropertyName = "")]
            //public bool wound;
        }

        private class ArenaSetting
        {
            public List<Vector3> spectators = new List<Vector3>();
            public List<Vector3> duelers1 = new List<Vector3>();
            public List<Vector3> duelers2 = new List<Vector3>();
        }

        class UIRegistration
        {
            public string anchormin { get; set; } = "0.5 0";
            public string anchormax { get; set; } = "0.5 0";
            public string offsetmin { get; set; } = "-440 48";
            public string offsetmax { get; set; } = "-267 130";
            public string background_color { get; set; } = "0.2313726 0.2313726 0.2313726 0.3882353";
            public string button_color { get; set; } = "0 0 0 0.7058824";
            public string text_color { get; set; } = "0 0 0 0.7058824";
        }

        private class PluginConfig
        {

            #region ARENAS
            [JsonProperty(fermensEN ? "Tournament arenas" : "Турнирные арены")]
            public Dictionary<string, CurrentEvent> currentEvent = new Dictionary<string, CurrentEvent>();
            #endregion

            [JsonProperty(PropertyName = fermensEN ? "Run events every <x> minutes" : "Запуск ивентов через каждый <x> минут")]
            public float eventstart { get; set; } = 60f;

            [JsonProperty(PropertyName = fermensEN ? "How much time to register for the tournament? [seconds]" : "Сколько давать времени на регистрацию в турнире? [секунд]")]
            public int regatime { get; set; } = 120;

            [JsonProperty(PropertyName = fermensEN ? "Available commands in the arena [lower case]" : "Доступные команды на арене [нижний регистр]")]
            public string[] allowcommands { get; set; } = new string[] { "report", "mreport", "ban", "mute", "pm", "r", "qq.arena", "report", "setinfo", "sleep", "kill", "respawn", "recover", "qg", "qw", "inventory.endloot", "inventory.lighttoggle", "ff" };

            [JsonProperty(PropertyName = fermensEN ? "Chat command to enter/exit the arena" : "Чат команда для входа/выхода с арены")]
            public string chatcommand { get; set; } = "qq";

            [JsonProperty(PropertyName = fermensEN ? "UI - Tournament registration" : "UI - Регистрация на турнир")]
            public UIRegistration UIRegistration { get; set; } = new UIRegistration();

            [JsonProperty(PropertyName = fermensEN ? "Prevent players from using sound chat" : "Запретить игрокам использовать звуковой чат")]
            public bool blockvoice { get; set; }

            [JsonProperty(PropertyName = fermensEN ? "Do not give a prize to a player if he has won more than one tournament in a row" : "Не давать приз игроку если он выиграл больше одного турнира в ряд")]
            public bool noprizeinarow { get; set; }

            [JsonProperty(PropertyName = fermensEN ? "Disable notifications in Notify plugin if you have it" : "Отключить уведомления в плагин Notify, если он у вас имеется")]
            public bool disablenotify { get; set; }

            [JsonProperty(PropertyName = fermensEN ? "How many victories do the teams duel to get to the next stage?" : "До скольки побед дуэлятся между собой команды, что бы пройти в следующий этап?")]
            public int needwinds { get; set; } = 2;
        }
        #endregion

        #region Oxide - Hooks
        static ArenaTournament fermens;
        [PluginReference] Plugin CopyPaste, AimTrain, Notify;

        private void Init()
        {
            Unsubscribe(nameof(OnPlayerVoice));
            fermens = this;
        }

        private string token = "090620220734fermens";
        private string namer = "ArenaTournament";
        private void OnServerInitialized()
        {
            if (config.currentEvent.Count == 0)
            {
                config.currentEvent = new Dictionary<string, CurrentEvent>
                {
                    {
                        "ater",
                        new CurrentEvent
                        {
                            weapons = new List<GUNITEM>
                            {
                                { new GUNITEM { shortname = "rifle.ak", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.ak.ice", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "lmg.m249", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.semiauto", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "rifle.bolt", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.small.scope", 1 }, { "weapon.mod.8x.scope", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.l96", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.small.scope", 1 }, { "weapon.mod.8x.scope", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.m39", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.small.scope", 1 }, { "weapon.mod.8x.scope", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "rifle.lr300", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "smg.mp5", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzleboost", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "pistol.m92", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "shotgun.spas12", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "smg.thompson", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzleboost", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "shotgun.waterpipe", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "grenade.f1", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "pistol.semiauto", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "pistol.python", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "smg.2", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzleboost", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "bow.compound", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "bow.hunting", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "longsword", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "grenade.f1", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "crossbow", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.lasersight", 1 }, { "grenade.f1", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } }

                            },
                            sPEC = new SPEC
                            {
                                wears = new Dictionary<string, ulong> { { "sunglasses02red", 0 }, { "pants.shorts", 0 }, { "hoodie", 1368417352 } },
                                additional = new Dictionary<string, int> { { "tool.camera", 1 }, { "flashlight.held", 1 }, { "tool.binoculars", 1 } }
                            },
                            vs = new List<int> { 4, 3, 2, 1 },
                            position = new Vector3(-777f, 666f, -777f),
                            // wound = true,
                            radius = 50f,
                            reward = new Dictionary<int, Reward>()
                            {
                                {
                                    1,
                                    new Reward
                                    {
                                        commands = new Dictionary<string, string>
                                        {
                                            { "addgroup {steamid} vip 1h", fermensEN ? "VIP for 1 hour" : "VIP на 1 час" },
                                            { "create.key {steamid} arena", fermensEN ? "Duelist Case Key" : "Ключ от кейса Дуэлянт" }
                                        },
                                        iTEMs = new List<IITEM>
                                        {
                                            { new IITEM { amount = 2, blueprintTarget = false, shortname = "supply.signal", name = null } }
                                        }
                                    }
                                },
                                {
                                    2,
                                    new Reward
                                    {
                                        commands = new Dictionary<string, string>
                                        {
                                        },
                                        iTEMs = new List<IITEM>
                                        {
                                            { new IITEM { amount = 2, blueprintTarget = false, shortname = "supply.signal", name = null } }
                                        }
                                    }
                                },
                                {
                                    3,
                                    new Reward
                                    {
                                        commands = new Dictionary<string, string>
                                        {
                                        },
                                        iTEMs = new List<IITEM>
                                        {
                                            { new IITEM { amount = 1, blueprintTarget = false, shortname = "supply.signal", name = null } }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    {
                        "onevsone",
                        new CurrentEvent
                        {
                            weapons = new List<GUNITEM>
                            {
                                { new GUNITEM { shortname = "rifle.ak", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.ak.ice", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "lmg.m249", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.semiauto", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzlebrake", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "rifle.bolt", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.small.scope", 1 }, { "weapon.mod.8x.scope", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.l96", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.small.scope", 1 }, { "weapon.mod.8x.scope", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "rifle.m39", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.small.scope", 1 }, { "weapon.mod.8x.scope", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "rifle.lr300", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "smg.mp5", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzleboost", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "pistol.m92", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "shotgun.spas12", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "smg.thompson", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzleboost", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "shotgun.waterpipe", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "grenade.f1", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "pistol.semiauto", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "pistol.python", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.silencer", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "smg.2", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.muzzleboost", 1 }, { "weapon.mod.lasersight", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "bow.compound", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "bow.hunting", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },
                                { new GUNITEM { shortname = "longsword", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "grenade.f1", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } },

                                { new GUNITEM { shortname = "crossbow", moduls = new List<string> { }, skin = 0, additional = new Dictionary<string, int> { { "syringe.medical", 5 }, { "weapon.mod.holosight", 1 }, { "weapon.mod.simplesight", 1 }, { "weapon.mod.lasersight", 1 }, { "grenade.f1", 1 } }, wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } } } }

                            },
                            sPEC = new SPEC
                            {
                                wears = new Dictionary<string, ulong> { { "sunglasses02red", 0 }, { "pants.shorts", 0 }, { "hoodie", 1368417352 } },
                                additional = new Dictionary<string, int> { { "tool.camera", 1 }, { "flashlight.held", 1 }, { "tool.binoculars", 1 } }
                            },
                            vs = new List<int> { 1, 2 },
                            position = new Vector3(-777f, 666f, 777f),
                            // wound = true,
                            radius = 50f,
                            reward = new Dictionary<int, Reward>()
                            {
                                {
                                    1,
                                    new Reward
                                    {
                                        commands = new Dictionary<string, string>
                                        {
                                            { "addgroup {steamid} vip 1h", fermensEN ? "<color=#ccff33>VIP</color> for 1 hour" : "<color=#ccff33>VIP</color> на 1 час" },
                                            { "create.key {steamid} arena", fermensEN ? "<color=#ccff33>Duelist Case Key</color>" : "Ключ от кейса <color=#ccff33>Дуэлянт</color>" }
                                        },
                                        iTEMs = new List<IITEM>
                                        {
                                            { new IITEM { amount = 2, blueprintTarget = false, shortname = "supply.signal", name = null } }
                                        }
                                    }
                                },
                                {
                                    2,
                                    new Reward
                                    {
                                        commands = new Dictionary<string, string>
                                        {
                                        },
                                        iTEMs = new List<IITEM>
                                        {
                                            { new IITEM { amount = 2, blueprintTarget = false, shortname = "supply.signal", name = null } }
                                        }
                                    }
                                },
                                {
                                    3,
                                    new Reward
                                    {
                                        commands = new Dictionary<string, string>
                                        {
                                        },
                                        iTEMs = new List<IITEM>
                                        {
                                            { new IITEM { amount = 1, blueprintTarget = false, shortname = "supply.signal", name = null } }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
            }

            SaveConfig();
            OnEvent.Clear();
            ServerMgr.Instance.StartCoroutine(Start());
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (item == null || container == null) return;
            if (container.playerOwner != null && IsOnTournament(container.playerOwner.userID.Get()) || container.parent != null && container.parent.GetOwnerPlayer() != null && IsOnTournament(container.parent.GetOwnerPlayer().userID.Get()))
            {
                if (item.GetWorldEntity() != null)
                {
                    BaseEntity heldEntity = item.GetHeldEntity();
                    if (heldEntity != null) item.Remove(0f);
                    item.DoRemove();
                }
            }
        }

        #region CASH
        private string GUI = "";
        private string tt = "";
        private string td = "";
        private string tms = "";
        private string n1 = "";
        private string n2 = "";
        private string regaGUI = "";
        private string regarr = "";
        private string regabb = "";
        private string uiline = "";
        #endregion

        #region BTC PRICE 999,999 $

        Dictionary<string, string> messagesRU = new Dictionary<string, string>
        {
            { "tournament", "Турнир" },
            { "close", "закрыть"},
            { "regNoActiveTournament", "Нет активных регистраций на какой либо турнир!" },
            { "regIsDead", "Вы мертвы!" },
            { "regIsWounded", "Вы смертельно ранены!" },
            { "regIsMounted", "Покиньте транспортное средство или слезьте с кресла!" },
            { "regInSafeZone", "Выйдите с безопасной зоны!" },
            { "regNotIsOnGround", "Ваши ноги не чувствуют земли =(" },
            { "regIsAimTraining", "Выйдите с арены AimTrain!" },
            { "regIsArenaNull", "Арена некорректно настроена!" },
            { "globalRegistration", "<color=#ceff7a>[ArenaTournament]</color> Регистрация на турнир открыта!" },
            { "uiRegRegim", "Режим, {vs}vs{vs}" },
            { "uiRegWeapon", "Оружие, {name}" },
            { "uiRegPlayers", "Участников, {count}" },
            { "uiRegTimer", "Начало через, {time}" },
            { "uiPanelRound", "Раунд {round}" },
            { "uiPanelRoundFinal", "ГРАНД-ФИНАЛ" },
            { "uiPanelAlives", "Осталось игроков {count}" },
            { "chatRoundStart", "<color=#ceff7a>[ArenaTournament]</color> <<color=#ffff75>{round}</color>>\n<color=#f07575>{team1_players}</color> <color=#ffc675>{team1_count}</color>vs<color=#ffc675>{team2_count}</color> <color=#47c2eb>{team2_players}</color>" },
            { "uiMessageWinDuel", "Поздравляем, вы прошли в следующий раунд." },
            { "globalWinner", "Поздравляем <color=#ffff75>{name}</color> с <color=#ffc675>первым местом</color> в турнире." },
            { "uiButtonReg", "УЧАСТВОВАТЬ" },
            { "uiButtonCancelReg", "ОТМЕНИТЬ УЧАСТИЕ" },
            { "chatReward", "<color=#ff6666>Поздравляем, ваша награда за {place} место в турнире,</color>\n" },
            { "chatRewardRecipt", "Чертеж <color=#ccff33>{name}</color> - {amount} шт.\n" },
            { "chatRewardItem", "<color=#ccff33>{name}</color> - {amount} шт.\n" },
            { "chatBlockCommand", "Запрещено использовать эту команду на Арене!" },
            { "btnexit", "Покинуть турнир" },
            { "vtext", "К сожалению вы проиграли :(" },
            { "vtext2", "Наблюдать" },
            { "rifle.ak", "АК-47" },
            { "rifle.ak.ice", "АК-47 - ЛЁД" },
            { "rifle.bolt", "Болтовка" },
            { "rifle.l96", "L96" },
            { "rifle.lr300", "LR-300" },
            { "rifle.m39", "M39" },
            { "rifle.semiauto", "Берданка" },
            { "bone.club", "Костяная дубина" },
            { "knife.bone", "Костяной нож" },
            { "knife.butcher", "Нож мясника" },
            { "candycaneclub", "Леденец-дубинка" },
            { "knife.combat", "Боевой нож" },
            { "bow.compound", "Блочный лук" },
            { "crossbow", "Арбалет" },
            { "smg.2", "СМГ" },
            { "shotgun.double", "Двустволка" },
            { "pistol.eoka", "Еока" },
            { "bow.hunting", "Лук" },
            { "longsword", "Длинный меч" },
            { "pistol.m92", "М92 Беретта" },
            { "smg.mp5", "MP5A4" },
            { "mace", "Булава" },
            { "machete", "Мачете" },
            { "multiplegrenadelauncher", "Гранатомёт" },
            { "pistol.nailgun", "Гвоздомёт" },
            { "paddle", "Весло" },
            { "pitchfork", "Вилы" },
            { "shotgun.pump", "Помповый дробовик" },
            { "pistol.pyhon", "Питон" },
            { "pistol.revolver", "Револьвер" },
            { "rocket.launcher", "Ракетница" },
            { "salvaged.cleaver", "Тесак" },
            { "salvaged.sword", "Меч" },
            { "pistol.semiauto", "P250" },
            { "snowballgun", "Снежкомёт" },
            { "shotgun.spas12", "Spas-12" },
            { "speargun", "Подводное ружьё" },
            { "spear.stone", "Копье" },
            { "spear.wooden", "Копье" },
            { "smg.thompson", "Томпсон" },
            { "shotgun.waterpipe", "Пайпа" },
            { "lmg.m249", "Пулемёт М249" }
        };

        Dictionary<string, string> messagesEN = new Dictionary<string, string>
        {
            { "tournament", "Tournament" },
            { "close", "сlose"},
            { "regNoActiveTournament", "There are no active registrations for any tournament!" },
            { "regIsDead", "You are dead!" },
            { "regIsWounded", "You are wounded!" },
            { "regIsMounted", "Get out of the vehicle or get off the chair!" },
            { "regInSafeZone", "Get out of the safe zone!" },
            { "regNotIsOnGround", "Your feet don't feel the ground =(" },
            { "regIsAimTraining", "Get out of the AimTrain arena!" },
            { "regIsArenaNull", "Arena set up incorrectly!" },
            { "globalRegistration", "<color=#ceff7a>[ArenaTournament]</color> Registration for the tournament is open!" },
            { "uiRegRegim", "Mode, {vs}vs{vs}" },
            { "uiRegWeapon", "Weapon, {name}" },
            { "uiRegPlayers", "Participants, {count}" },
            { "uiRegTimer", "Starting in, {time}" },
            { "uiPanelRound", "Round {round}" },
            { "uiPanelRoundFinal", "GRAND FINAL" },
            { "uiPanelAlives", "Players left {count}" },
            { "chatRoundStart", "<color=#ceff7a>[ArenaTournament]</color> <<color=#ffff75>{round}</color>>\n<color=#f07575>{team1_players}</color> <color=#ffc675>{team1_count}</color>vs<color=#ffc675>{team2_count}</color> <color=#47c2eb>{team2_players}</color>" },
            { "uiMessageWinDuel", "Congratulations, you have advanced to the next round." },
            { "globalWinner", "Congratulations to <color=#ffff75>{name}</color> on <color=#ffc675>first place</color> in the tournament." },
            { "uiButtonReg", "PARTICIPATE" },
            { "uiButtonCancelReg", "CANCEL PARTICIPATION" },
            { "chatReward", "<color=#ff6666>Congratulations, your reward for placing {place} in the tournament is,</color>\n" },
            { "chatRewardRecipt", "Blueprint <color=#ccff33>{name}</color> - {amount} шт.\n" },
            { "chatRewardItem", "<color=#ccff33>{name}</color> - {amount} шт.\n" },
            { "chatBlockCommand", "It is forbidden to use this command in the Arena!" },
            { "rifle.ak", "AK-47" },
            { "rifle.ak.ice", "AK-47 - ICE" },
            { "btnexit", "Leave the tournament" },
            { "vtext", "GOOD LUCK NEXT TIME" },
            { "vtext2", "Spectate" },
            { "rifle.bolt", "Bolt" },
            { "rifle.l96", "L96 Rifle" },
            { "rifle.lr300", "LR-300" },
            { "rifle.m39", "M39 Rifle" },
            { "rifle.semiauto", "SA - Rifle" },
            { "bone.club", "Bone Club" },
            { "knife.bone", "Bone Knife" },
            { "knife.butcher", "Butcher Knife" },
            { "candycaneclub", "Candy Cane Club" },
            { "knife.combat", "Combat Knife" },
            { "bow.compound", "Compound Bow" },
            { "crossbow", "Crossbow" },
            { "smg.2", "Custom SMG" },
            { "shotgun.double", "Double Shotgun" },
            { "pistol.eoka", "Eoka Pistol" },
            { "bow.hunting", "Hunting Bow" },
            { "longsword", "Longsword" },
            { "pistol.m92", "M92 Pistol" },
            { "smg.mp5", "MP5A4" },
            { "mace", "Mace" },
            { "machete", "Machete" },
            { "multiplegrenadelauncher", "MGL" },
            { "pistol.nailgun", "Nailgun" },
            { "paddle", "Paddle" },
            { "pitchfork", "Pitchfork" },
            { "shotgun.pump", "Pump Shotgun" },
            { "pistol.pyhon", "Python Revolver" },
            { "pistol.revolver", "Revolver" },
            { "rocket.launcher", "Rocket Launcher" },
            { "salvaged.cleaver", "Salvaged Cleaver" },
            { "salvaged.sword", "Salvaged Sword" },
            { "pistol.semiauto", "SA - Pistol" },
            { "snowballgun", "Snowball Gun" },
            { "shotgun.spas12", "Spas-12" },
            { "speargun", "Speargun" },
            { "spear.stone", "Stone Spear" },
            { "spear.wooden", "Wooden Spear" },
            { "smg.thompson", "Thompson" },
            { "lmg.m249", "M249" },
            { "shotgun.waterpipe", "Waterpipe Shotgun" }
        };

        BasePlayer lastwinner;

        string RRQ = "[{\"name\":\"RRQ\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.06145881 0.06115152 0.06115152 0.7878113\"},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"aaa\",\"parent\":\"RRQ\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.3010596 0.1053711 0.1053711 0.5781319\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-200 0\",\"offsetmax\":\"200 50\"}]},{\"name\":\"ttt\",\"parent\":\"aaa\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"К сожалению вы проиграли :(\",\"fontSize\":20,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"yyy\",\"parent\":\"RRQ\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{button1}\",\"color\":\"0.04051316 0.04071674 0.04057312 0.6273077\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-200 -25\",\"offsetmax\":\"-5 -5\"}]},{\"name\":\"uuu\",\"parent\":\"yyy\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"yyy2\",\"parent\":\"RRQ\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{button2}\",\"color\":\"0.04051316 0.04071674 0.04057312 0.6273077\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"5 -25\",\"offsetmax\":\"200 -5\"}]},{\"name\":\"uuu\",\"parent\":\"yyy2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        string p1 = "[{\"name\":\"ww\",\"parent\":\"eeee\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{pl1}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        string p2 = "[{\"name\":\"tt2\",\"parent\":\"rrrr\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{pl2}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        string viever = "[{\"name\":\"Viever\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.9432197\"},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"ee\",\"parent\":\"Viever\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0.9186267 0.9186267 0.3921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.5\",\"anchormax\":\"0.5 0.5\",\"offsetmin\":\"-200 0\",\"offsetmax\":\"200 50\"}]},{\"name\":\"uu\",\"parent\":\"ee\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":20,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"yy\",\"parent\":\"ee\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"at.quit\",\"color\":\"1 0.3686275 0.3686275 0.3921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"0 -25\",\"offsetmax\":\"197.5 -5\"}]},{\"name\":\"text\",\"parent\":\"yy\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text1}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"oo\",\"parent\":\"ee\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"Viever\",\"color\":\"0.372549 1 0.6392111 0.3921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"-197.5 -25\",\"offsetmax\":\"0 -5\"}]},{\"name\":\"text\",\"parent\":\"oo\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text2}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
        IEnumerator Start()
        {
            Debug.Log("[ArenaTournament] Initialization...");
            yield return CoroutineEx.waitForSeconds(2f);

            if (!CopyPaste)
            {
                Debug.LogError(fermensEN ? "[ArenaTournament] Install the CopyPaste plugin! (https://umod.org/plugins/copy-paste)" : "[ArenaTournament] Установите плагин CopyPaste! (https://umod.org/plugins/copy-paste)");
                Interface.Oxide.UnloadPlugin(Name);
                yield break;
            }

            GUI = "[{\"name\":\"arenamain\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0.5529412 0.5529412 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 1\",\"anchormax\":\"0.5 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"score1\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0.5529412 0.5529412 0.8392157\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-85 -60\",\"offsetmax\":\"-45 -20\"}]},{\"name\":\"t1\",\"parent\":\"score1\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{n1}\",\"fontSize\":20,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"score2\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.5529412 0.7176471 1 0.8392157\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"45 -60\",\"offsetmax\":\"85 -20\"}]},{\"name\":\"t2\",\"parent\":\"score2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{n2}\",\"fontSize\":20,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"teamas\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.3960784\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-105 -20\",\"offsetmax\":\"105 -5\"}]},{\"name\":\"tms\",\"parent\":\"teamas\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":10,\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"qqq\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.5921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-45 -80\",\"offsetmax\":\"45 -60\"}]},{\"name\":\"CuiElwww\",\"parent\":\"qqq\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"BO{n}\",\"fontSize\":12,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"eeee\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.7874001 0.4590079 0.4590079 0.8392157\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-105 -40\",\"offsetmax\":\"-85 -20\"}]},{\"name\":\"ww\",\"parent\":\"eeee\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{pl1}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"rrrr\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.4471784 0.5598956 0.7602817 0.8392157\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"85 -40\",\"offsetmax\":\"105 -20\"}]},{\"name\":\"tt2\",\"parent\":\"rrrr\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{pl2}\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"tatt\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.5921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"-45 -60\",\"offsetmax\":\"45 -20\"}]},{\"name\":\"tt\",\"parent\":\"tatt\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{timer}\",\"fontSize\":20,\"align\":\"UpperCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 -1\"}]},{\"name\":\"td\",\"parent\":\"tatt\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{teams}\",\"fontSize\":12,\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"LowerCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"btnss\",\"parent\":\"arenamain\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"at.quit\",\"color\":\"1 0.512997 0.512997 0.397651\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"120 -25\",\"offsetmax\":\"260 -5\"}]},{\"name\":\"22343\",\"parent\":\"btnss\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{exit}\",\"color\":\"1 1 1 0.5\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
            tt = "[{\"name\":\"tt\",\"parent\":\"tatt\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{timer}\",\"fontSize\":20,\"align\":\"UpperCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 -1\"}]}]";
            td = "[{\"name\":\"td\",\"parent\":\"tatt\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{teams}\",\"fontSize\":12,\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"LowerCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 1\",\"offsetmax\":\"0 0\"}]}]";
            tms = "[{\"name\":\"tms\",\"parent\":\"teamas\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":10,\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";
            n1 = "[{\"name\":\"t1\",\"parent\":\"score1\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{n1}\",\"fontSize\":20,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
            n2 = "[{\"name\":\"t2\",\"parent\":\"score2\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{n2}\",\"fontSize\":20,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
            regaGUI = "[{\"name\":\"ElemR\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"{anchormin}\",\"anchormax\":\"{anchormax}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"PanR\",\"parent\":\"ElemR\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"assets/content/ui/ui.background.transparent.radial.psd\",\"color\":\"{background_color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0 0\",\"offsetmin\":\"{offsetmin}\",\"offsetmax\":\"{offsetmax}\"}]},{\"name\":\"StarR\",\"parent\":\"PanR\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{regim}\n{gun}\n{join}\n{time}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.7843137\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"15 0\",\"offsetmax\":\"-10 0\"}]},{\"name\":\"regaR\",\"parent\":\"PanR\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"color\":\"{button_color}\", \"command\": \"chat.say /{coma}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"0.5 0\",\"offsetmin\":\"-87 -30\",\"offsetmax\":\"87 0\"}]},{\"name\":\"LR\",\"parent\":\"regaR\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{line_color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 1\"}]},{\"name\":\"RR\",\"parent\":\"regaR\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{line_color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"TextR\",\"parent\":\"regaR\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{rtext}\",\"fontSize\":16,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"RR\",\"parent\":\"PanR\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"{line_color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 1\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 -1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"herka\",\"parent\":\"PanR\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{tournament}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"{text_color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 1\",\"anchormax\":\"0.5 1\",\"offsetmin\":\"-85 0\",\"offsetmax\":\"85 40\"}]},{\"name\":\"xex\",\"parent\":\"PanR\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"color\":\"1 1 1 0\",\"command\":\"{command2}\"},{\"type\":\"RectTransform\",\"anchormin\":\"1 0\",\"anchormax\":\"1 0\",\"offsetmin\":\"-100 -45\",\"offsetmax\":\"0 -30\"}]},{\"name\":\"xexx\",\"parent\":\"xex\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{close}\",\"fontSize\":10,\"align\":\"MiddleRight\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]".Replace("{anchormax}", config.UIRegistration.anchormax).Replace("{anchormin}", config.UIRegistration.anchormin).Replace("{offsetmax}", config.UIRegistration.offsetmax).Replace("{offsetmin}", config.UIRegistration.offsetmin).Replace("{offsetmax}", config.UIRegistration.offsetmax).Replace("{button_color}", config.UIRegistration.button_color).Replace("{background_color}", config.UIRegistration.background_color).Replace("{command2}", "at.close");
            regarr = "[{\"name\":\"StarR\",\"parent\":\"PanR\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{regim}\n{gun}\n{join}\n{time}\",\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleLeft\",\"color\":\"1 1 1 0.7843137\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"15 0\",\"offsetmax\":\"-10 0\"}]}]";
            regabb = "[{\"name\":\"TextR\",\"parent\":\"regaR\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{rtext}\",\"fontSize\":16,\"align\":\"MiddleCenter\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]}]";
            uiline = "[{\"name\":\"arenaline\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0.2314585 0.2314585 0.2314585 0.5461518\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0.55\",\"anchormax\":\"1 0.65\",\"offsetmax\":\"0 0\"}]},{\"name\":\"arenaline_text\",\"parent\":\"arenaline\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":30,\"font\":\"RobotoCondensed-Regular.ttf\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7878122\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";

            lang.RegisterMessages(messagesEN, this, "en");
            lang.RegisterMessages(messagesRU, this, "ru");

            if (config.blockvoice) Subscribe(nameof(OnPlayerVoice));

            permission.RegisterPermission(Name + ".admin", this);

            LoadDate();

            timer.Every(config.eventstart * 60f, () => RandomEvent(config.regatime));

            Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddChatCommand(config.chatcommand, this, "CMDTEST");

            string[] arenas = new string[] { "ater", "onevsone" };

            foreach (var x in arenas)
            {
                string copyStorage = "copypaste/" + x;
                if (!Interface.Oxide.DataFileSystem.ExistsDatafile(copyStorage))
                {
                    Debug.Log($"[ArenaTournament] Downloading copypaste for arena {x.ToUpper()}");
                    webrequest.Enqueue($"https://fermens.foxplugins.ru/CopyPaste/{x}.json", "", (code2, response2) =>
                    {
                        if (code2 == 200)
                        {
                            if (!string.IsNullOrEmpty(response2))
                            {
                                Debug.Log($"[ArenaTournament] Saving copypaste for arena {x.ToUpper()}");
                                Interface.Oxide.DataFileSystem.WriteObject(copyStorage, JsonConvert.DeserializeObject(response2));
                                return;
                            }
                        }
                        Debug.Log($"[ArenaTournament] Copypaste for arena {x.ToUpper()} not found!");
                    }, this, Core.Libraries.RequestMethod.GET);
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            foreach (var currentevent in config.currentEvent)
            {
                if (currentevent.Value.vs == null || currentevent.Value.vs.Count == 0)
                {
                    currentevent.Value.vs.Add(1);
                    currentevent.Value.vs.Add(2);
                    if (currentevent.Key == "ater")
                    {
                        currentevent.Value.vs.Add(3);
                        currentevent.Value.vs.Add(4);
                    }
                    SaveConfig();
                }

                foreach (var x in currentevent.Value.weapons)
                {
                    if (x.wear == null)
                    {
                        x.wear = new Dictionary<string, SkinTeam> { { "metal.facemask", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "hoodie", new SkinTeam { skinteam1 = 954947279, skinteam2 = 971807764 } }, { "roadsign.gloves", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "pants", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "metal.plate.torso", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } }, { "shoes.boots", new SkinTeam { skinteam1 = 0, skinteam2 = 0 } } };
                        SaveConfig();
                    }
                }

                Paste(currentevent.Key, currentevent.Value);
                yield return CoroutineEx.waitForSeconds(5f);
            }

            yield return CoroutineEx.waitForEndOfFrame;

            Debug.Log("[ArenaTournament] Initialization successful >>fermens#8767<<");

            yield break;
        }
        #endregion

        private CurrentEvent GetConfigEvent(string name)
        {
            CurrentEvent currentEvent;
            if (config.currentEvent.TryGetValue(name, out currentEvent)) return currentEvent;
            return null;
        }

        private void SaveArena(string name, ArenaSetting arenaSetting)
        {
            Interface.Oxide.DataFileSystem.WriteObject("fermens/Arenas/" + name, arenaSetting);
        }

        private ArenaSetting LoadArena(string name)
        {
            ArenaSetting arenaSetting = Interface.Oxide.DataFileSystem.ReadObject<ArenaSetting>("fermens/Arenas/" + name);
            return arenaSetting;
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            Arena arena = GetAEvent(player);
            if (arena != null)
            {
                if (arena.IsDueling(player))
                {
                    arena.Killed(player);
                    return false;
                }
                return false;
            }
            return null;
        }

        private object OnPlayerWound(BasePlayer player, HitInfo info)
        {
            Arena arena = GetAEvent(player);
            if (arena != null)
            {
                if (arena.IsDueling(player))
                {
                    arena.Killed(player);
                }
                return false;
                /*
                if (!arena.IsDuelingLaster(player) && arena.wound)
                {
                    return null;
                }
                if (debug) Debug.Log("[ArenaTournament] OnPlayerWound");
                arena.Killed(player);
                return false;*/
            }
            return null;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || entity == null) return;

            float damage = hitInfo.damageTypes.Total();
            if (damage < 1f) return;
            if (entity is BasePlayer)
            {
                BasePlayer player = entity as BasePlayer;
                Arena arena = GetAEvent(player);
                if (arena != null)
                {
                    if (hitInfo.Initiator is BaseHelicopter)
                    {
                        hitInfo.Initiator.AdminKill();
                        ClearDamage(hitInfo);
                        return;
                    }

                    if (hitInfo.damageTypes.GetMajorityDamageType() == DamageType.Fall || !arena.IsDueling(player) || hitInfo.InitiatorPlayer != null && arena.IsTeammate(player, hitInfo.InitiatorPlayer))
                    {
                        ClearDamage(hitInfo);
                        return;
                    }
                }
            }
            else if (entity is DecayEntity || entity is IOEntity)
            {
                if (entity.OwnerID == 8767) ClearDamage(hitInfo);
            }
        }

        object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity is BasePlayer && hitInfo.InitiatorPlayer != null)
            {
                BasePlayer player = entity as BasePlayer;
                Arena arena = GetAEvent(player);
                if (arena != null)
                {
                    if (arena.IsDueling(player) && !arena.IsTeammate(player, hitInfo.InitiatorPlayer)) return true;
                }
            }
            return null;
        }

        private object isEventPlayer(BasePlayer player)
        {
            if (IsOnTournament(player.userID.Get())) return (object)true;
            return null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_currentevent != null) GetArena(_currentevent).UIRega(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            Delete(player);
        }

        private void ClearDamage(HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitEntity = null;
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
            if (debug) Debug.Log("[ArenaTournament] Clear Damage");
        }

        private void Delete(BasePlayer player)
        {
            Arena arena = GetAEvent(player);
            if (arena != null) arena.DeletePlayer(player);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            timer.Once(1f, () =>
            {
                if (!player.IsConnected || player.IsDead()) return;
                Arena arena = GetAEvent(player);
                if (arena == null || arena != null && !arena.IsWList(player))
                {
                    Restore(player);
                }
            });
        }

        #region Another
        private string GetMessage(string key, string userId)
        {
            return lang.GetMessage(key, this, userId);
        }

        private void LoadDate()
        {
            if (!wipe) savedatatp = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, SaveDataTP>>($"fermens/SaveTournament");
            else wipe = false;

            load = true;
        }

        private IEnumerator SaveData()
        {
            if (load) Interface.Oxide.DataFileSystem.WriteObject($"fermens/SaveTournament", savedatatp);
            yield break;
        }

        private bool load;
        private bool wipe;
        private void OnNewSave(string filename) => wipe = true;

        #endregion

        private void Unload()
        {
            var list = events.ToList();
            foreach (var x in list)
            {
                if (x.Value == null) continue;
                UnityEngine.Object.Destroy(x.Value);
            }

            timer.Once(1f, () => fermens = null);
        }
        #endregion

        #region FUNCS

        #endregion

        #region TESTING
        GameObject _currentevent;
        private void CMDTEST(BasePlayer player, string cmd, string[] args)
        {
            Add2Event(player, _currentevent);
        }

        [ChatCommand("qb")]
        private void CMDTEST1(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Name + ".admin")) return;
            int ax = 0;
            while (ax < 1)
            {
                BasePlayer npc = (BasePlayer)GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab", player.transform.position, new Quaternion(), false);
                if (npc == null) return;
                npc.enableSaving = false;
                npc.gameObject.AwakeFromInstantiate();
                npc.Spawn();
                Add2Event(npc, _currentevent);
                ax++;
            }
        }

        [ChatCommand("o")]
        private void CMDTEST2(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, Name + ".admin")) return;

            int rtime;
            if (args == null || args.Length == 0 || !int.TryParse(args[0], out rtime)) rtime = config.regatime;

            RandomEvent(rtime);
        }

        [ConsoleCommand("at.start")]
        private void atstart(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            if (!arg.HasArgs()) RandomEvent(config.regatime);
            else
            {
                if (!arg.HasArgs(5))
                {
                    Debug.LogError("at.start {arena_name} {weapon_name} {number_of_players_in_a_team} {check-in_time} {till_how_many_victories}");
                    return;
                }

                if (events.Count == 0)
                {
                    Debug.LogError(fermensEN ? "[ArenaTournament] DID NOT FIND A READY ARENA!" : "[ArenaTournament] НЕ НАШЛИ ГОТОВУЮ АРЕНУ!");
                    return;
                }

                if (_currentevent != null)
                {
                    Debug.Log(fermensEN ? "[ArenaTournament] There is already an active registration for the tournament!" : "[ArenaTournament] Уже есть активная регистрация на турнир!");
                    return;
                }

                string arenaname = arg.Args[0];
                string riflename = arg.Args[1];

                int vsmode;
                if (!int.TryParse(arg.Args[2], out vsmode))
                {
                    Debug.Log("error number_of_players_in_a_team!");
                    return;
                }

                int starttime;
                if (!int.TryParse(arg.Args[3], out starttime))
                {
                    Debug.Log("error check-in_time!");
                    return;
                }

                int needwins;
                if (!int.TryParse(arg.Args[4], out needwins))
                {
                    Debug.Log("error till_how_many_victories!");
                    return;
                }

                GameObject gameObject;
                if (!events.TryGetValue(arenaname, out gameObject))
                {
                    Debug.LogError(fermensEN ? $"[ArenaTournament] DID NOT FIND A READY ARENA {arenaname}!" : $"[ArenaTournament] НЕ НАШЛИ АРЕНУ С НАЗВАНИЕМ {arenaname}!");
                    return;
                }

                Arena arena = GetArena(gameObject);
                if (arena.round == 0)
                {
                    ServerMgr.Instance.StartCoroutine(arena.StartRegistration(starttime, riflename, vsmode, needwins));
                }
            }
        }

        [ConsoleCommand("at.test")]
        private void atstest(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            arg.Player().Hurt(1000f, DamageType.Explosion, arg.Player(), false);
        }

        [ConsoleCommand("at.copy")]
        private void atcopy(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            if (!arg.HasArgs(2))
            {
                arg.ReplyWith(fermensEN ? "at.copy existing_name new_name" : "at.copy название_существующей название_новой");
                return;
            }

            string name1 = arg.Args[0];
            string name2 = arg.Args[1];

            CurrentEvent currentEvent;
            if (!config.currentEvent.TryGetValue(name1, out currentEvent))
            {
                arg.ReplyWith(fermensEN ? $"You don't have the {name1} arena!" : $"У вас нет арены {name1}!");
                return;
            }

            if (config.currentEvent.ContainsKey(name2))
            {
                arg.ReplyWith(fermensEN ? $"You already own the {name2} arena!" : $"У вас уже есть арена {name2}!");
                return;
            }

            config.currentEvent.Add(name2, currentEvent);
            arg.ReplyWith(fermensEN ? $"Settings for arena {name2} successfully created." : $"Настройки для арены {name2} успешно созданы.");
            SaveConfig();
        }

        private void RandomEvent(int time = 60)
        {
            if (events.Count == 0)
            {
                Debug.LogError(fermensEN ? "[ArenaTournament] DID NOT FIND A READY ARENA!" : "[ArenaTournament] НЕ НАШЛИ ГОТОВУЮ АРЕНУ!");
                return;
            }

            if (_currentevent != null)
            {
                Debug.Log(fermensEN ? "[ArenaTournament] There is already an active registration for the tournament!" : "[ArenaTournament] Уже есть активная регистрация на турнир!");
                return;
            }

            Arena arena = GetArena(events.ElementAtOrDefault(Rand(events.Count)).Value);
            if (arena.round == 0) ServerMgr.Instance.StartCoroutine(arena.StartRegistration(time));
            else
            {
                foreach (var x in events.Values)
                {
                    arena = GetArena(x);
                    if (arena != null && arena.round == 0)
                    {
                        ServerMgr.Instance.StartCoroutine(arena.StartRegistration(time));
                        break;
                    }
                }

                if (_currentevent == null) Debug.LogError(fermensEN ? "[ArenaTournament] All arenas are busy!" : "[ArenaTournament] Все арены заняты!");
            }

        }

        private void God(BasePlayer player)
        {
            player._maxHealth = float.MaxValue;
            player.health = float.MaxValue;
            player.metabolism.bleeding.max = 0;
            player.metabolism.bleeding.value = 0;
            player.metabolism.calories.min = 500;
            player.metabolism.calories.value = 500;
            player.metabolism.dirtyness.max = 0;
            player.metabolism.dirtyness.value = 0;
            player.metabolism.heartrate.min = 0.5f;
            player.metabolism.heartrate.max = 0.5f;
            player.metabolism.heartrate.value = 0.5f;
            player.metabolism.hydration.min = 250;
            player.metabolism.hydration.value = 250;
            player.metabolism.oxygen.min = 1;
            player.metabolism.oxygen.value = 1;
            player.metabolism.poison.max = 0;
            player.metabolism.poison.value = 0;
            player.metabolism.radiation_level.max = 0;
            player.metabolism.radiation_level.value = 0;
            player.metabolism.radiation_poison.max = 0;
            player.metabolism.radiation_poison.value = 0;
            player.metabolism.temperature.min = 32;
            player.metabolism.temperature.max = 32;
            player.metabolism.temperature.value = 32;
            player.metabolism.wetness.max = 0;
            player.metabolism.wetness.value = 0;
            player.metabolism.SendChangesToClient();
        }

        #endregion

        #region Behavior - Event  

        #region FUNC
        private Dictionary<string, GameObject> events = new Dictionary<string, GameObject>();

        //private CurrentEvent currentEvent;

        private void Add2Event(BasePlayer player, GameObject gameObject)
        {
            if (gameObject == null)
            {
                player.ChatMessage(GetMessage("regNoActiveTournament", player.UserIDString));
                return;
            }

            if (player.IsDead())
            {
                player.ChatMessage(GetMessage("regIsDead", player.UserIDString));
                return;
            }

            if (player.IsWounded())
            {
                player.ChatMessage(GetMessage("regIsWounded", player.UserIDString));
                return;
            }

            if (player.isMounted)
            {
                player.ChatMessage(GetMessage("regIsMounted", player.UserIDString));
                return;
            }

            if (player.InSafeZone())
            {
                player.ChatMessage(GetMessage("regInSafeZone", player.UserIDString));
                return;
            }

            if (!player.IsOnGround() || player.GetComponentInParent<Tugboat>())
            {
                player.ChatMessage(GetMessage("regNotIsOnGround", player.UserIDString));
                return;
            }

            if (AimTrain != null && AimTrain.Call<bool>("IsAimTraining", player.userID.Get()))
            {
                player.ChatMessage(GetMessage("regIsAimTraining", player.UserIDString));
                return;
            }
            player.inventory.crafting.CancelAll(true);
            if (player.inventory.loot?.entitySource != null) player.EndLooting();

            Arena arena = GetArena(gameObject);
            if (arena == null)
            {
                player.ChatMessage(GetMessage("regIsArenaNull", player.UserIDString));
                return;
            }
            if (!arena.IsWList(player))
            {
                arena.AddPlayer(player);
            }
            else
            {
                arena.DeletePlayer(player);
            }
        }

        [ConsoleCommand("at.quit")]
        private void CMDTESTQUIT(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            Delete(player);
        }

        [ConsoleCommand("at.close")]
        private void CMDCLOSEUI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            if (closedUIrega.Contains(player)) return;
            closedUIrega.Add(player);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ElemR");
        }

        private int Rand(int count)
        {
            if (count == 1) return 0;
            return Random.Range(0, count);
        }
        #endregion

        #region TRIGER
        private void CreateTrigger(string name, CurrentEvent @event)
        {
            ArenaSetting arenaSetting = LoadArena(name);
            if (arenaSetting == null)
            {
                Debug.Log("arenaSetting == null");
                return;
            }

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = @event.position;
            SphereCollider sphereCollider = sphere.GetComponent<SphereCollider>();
            sphereCollider.radius = @event.radius;
            TriggerBase trigger = sphere.GetComponent<TriggerBase>() ?? sphere.gameObject.AddComponent<TriggerBase>();
            trigger.interestLayers = LayerMask.GetMask("Player (Server)");
            trigger.enabled = true;

            Arena zONE = sphere.AddComponent<Arena>();
            zONE._vs = @event.vs;
            zONE.spectators = arenaSetting.spectators;
            zONE.duelers1 = arenaSetting.duelers1;
            zONE.duelers2 = arenaSetting.duelers2;
            zONE.weapons = @event.weapons;
            zONE.gUNITEM = @event.weapons[0];
            zONE.wears = @event.sPEC.wears;
            zONE.additional = @event.sPEC.additional;
            zONE.reward = @event.reward;
            //zONE.wound = @event.wound;

            Debug.Log($"[ArenaTournament] {name.ToUpper()} arena ready.");

            events.Add(name, sphere);
        }

        private Arena GetArena(GameObject gameObject)
        {
            return gameObject.GetComponent<Arena>();
        }

        private List<BasePlayer> closedUIrega = new List<BasePlayer>();

        private class Arena : MonoBehaviour
        {
            private SphereCollider sphere;

            private Dictionary<BasePlayer, int> playerswhitelist = new Dictionary<BasePlayer, int>();
            private List<BasePlayer> viewers = new List<BasePlayer>();

            public List<int> _vs = new List<int>();

            private List<BasePlayer> team1 = new List<BasePlayer>();
            private List<BasePlayer> team2 = new List<BasePlayer>();

            private int team1_score;
            private int team2_score;

            private List<BasePlayer> team1_players_alive = new List<BasePlayer>();
            private List<BasePlayer> team2_players_alive = new List<BasePlayer>();

            public bool wound;
            public int vs;

            public int needwins;
            public string bo;

            public GUNITEM gUNITEM;

            private List<Item> items = new List<Item>();
            private List<Item> items_duelers = new List<Item>();

            #region -
            public List<Vector3> spectators;
            public List<Vector3> duelers1;
            public List<Vector3> duelers2;

            public List<GUNITEM> weapons = new List<GUNITEM>();

            public Dictionary<string, ulong> wears = new Dictionary<string, ulong>();
            public Dictionary<string, int> additional = new Dictionary<string, int>();

            public Dictionary<int, Reward> reward;
            #endregion

            private int seconds = 120;

            public int round = 0;
            public int duel = 0;

            public int secondsreg = 60;

            private void Awake()
            {
                sphere = GetComponent<SphereCollider>();
                if (sphere == null)
                {
                    Destroy(this);
                    Debug.Log("sphere null");
                    return;
                }

                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "EventArena";
                sphere.radius = 150f;
                sphere.isTrigger = true;
                sphere.enabled = true;
            }

            private void RefreshTimer()
            {
                seconds = 120;
            }

            public IEnumerator StartRegistration(int rega = 60, string _weapon = "", int _vsmode = 0, int mw = 0)
            {
                if (BasePlayer.activePlayerList.Count == 0) yield break;
                if (weapons.Count == 0)
                {
                    Debug.LogError("[ArenaTournament] weapons = 0 !");
                    yield break;
                }

                if (_vs.Count == 0)
                {
                    Debug.LogError("[ArenaTournament] _vs = 0 !");
                    yield break;
                }

                fermens._currentevent = gameObject;
                round = 0;
                team1_score = 0;
                team2_score = 0;

                if (mw == 0) needwins = fermens.config.needwinds;
                else needwins = mw;

                bo = (needwins * 2 - 1).ToString();
                RefreshTimer();
                secondsreg = rega;

                if (_vsmode <= 0) vs = _vs[fermens.Rand(_vs.Count)];
                else vs = _vsmode;

                gUNITEM = weapons.Find(x => x.shortname == _weapon);

                if (string.IsNullOrEmpty(_weapon) || gUNITEM == null) gUNITEM = weapons[fermens.Rand(weapons.Count)];

                playerswhitelist.Clear();
                fermens.closedUIrega.Clear();

                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    string message = fermens.GetMessage("globalRegistration", player.UserIDString);
                    SendMessage(player, message);
                }

                UIRega();

                Debug.Log("[ArenaTournament] Open registration for the tournament");
                while (secondsreg > 0)
                {
                    if (!fermens.IsLoaded) yield break;
                    var ts = TimeSpan.FromSeconds(secondsreg);
                    string text = $"{ts.Minutes}:{ts.Seconds.ToString("00")}";
                    string svs = vs.ToString();
                    string ccount = playerswhitelist.Count.ToString();

                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        if (fermens.closedUIrega.Contains(player)) continue;
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "StarR");
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.regarr.Replace("{regim}", fermens.GetMessage("uiRegRegim", player.UserIDString).Replace("{vs}", svs)).Replace("{gun}", fermens.GetMessage("uiRegWeapon", player.UserIDString).Replace("{name}", fermens.GetMessage(gUNITEM.shortname, player.UserIDString))).Replace("{join}", fermens.GetMessage("uiRegPlayers", player.UserIDString).Replace("{count}", ccount)).Replace("{time}", fermens.GetMessage("uiRegTimer", player.UserIDString).Replace("{time}", text)));
                    }

                    secondsreg--;
                    yield return CoroutineEx.waitForSeconds(1f);
                }


                fermens._currentevent = null;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "ElemR");
                StartEvent();
                yield break;
            }

            public void UIRega(BasePlayer pl = null)
            {
                var ts = TimeSpan.FromSeconds(secondsreg);
                string text = $"{ts.Minutes}:{ts.Seconds.ToString("00")}";
                string svs = vs.ToString();
                string ccount = playerswhitelist.Count.ToString();

                if (pl == null)
                {
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        if (fermens.closedUIrega.Contains(player)) continue;
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ElemR");
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.regaGUI.Replace("{close}", fermens.GetMessage("close", player.UserIDString)).Replace("{tournament}", fermens.GetMessage("tournament", player.UserIDString)).Replace("{coma}", fermens.config.chatcommand).Replace("{rtext}", fermens.GetMessage("uiButtonReg", player.UserIDString)).Replace("{regim}", fermens.GetMessage("uiRegRegim", player.UserIDString).Replace("{vs}", svs)).Replace("{gun}", fermens.GetMessage("uiRegWeapon", player.UserIDString).Replace("{name}", fermens.GetMessage(gUNITEM.shortname, player.UserIDString))).Replace("{join}", fermens.GetMessage("uiRegPlayers", player.UserIDString).Replace("{count}", ccount)).Replace("{time}", fermens.GetMessage("uiRegTimer", player.UserIDString).Replace("{time}", text)));
                    }
                }
                else
                {
                    if (fermens.closedUIrega.Contains(pl)) return;
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = pl.net.connection }, null, "DestroyUI", "ElemR");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = pl.net.connection }, null, "AddUI", fermens.regaGUI.Replace("{tournament}", fermens.GetMessage("tournament", pl.UserIDString)).Replace("{coma}", fermens.config.chatcommand).Replace("{rtext}", fermens.GetMessage("uiButtonReg", pl.UserIDString)).Replace("{regim}", fermens.GetMessage("uiRegRegim", pl.UserIDString).Replace("{vs}", svs)).Replace("{gun}", fermens.GetMessage("uiRegWeapon", pl.UserIDString).Replace("{name}", fermens.GetMessage(gUNITEM.shortname, pl.UserIDString))).Replace("{join}", fermens.GetMessage("uiRegPlayers", pl.UserIDString).Replace("{count}", ccount)).Replace("{time}", fermens.GetMessage("uiRegTimer", pl.UserIDString).Replace("{time}", text)));
                }
            }

            public void StartEvent()
            {
                Debug.Log("[ArenaTournament] Tournament has started");
                fermens._currentevent = null;
                round = 1;
                GeneratePair();
            }

            public IEnumerator CreateUILine(BasePlayer player, string text)
            {
                if (!player.IsConnected) yield break;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "arenaline");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.uiline.Replace("{text}", text));
                yield return CoroutineEx.waitForSeconds(3f);
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "arenaline");
                yield break;
            }

            private void GeneratePair()
            {

                team1.Clear();
                team2.Clear();
                team1_players_alive.Clear();
                team2_players_alive.Clear();
                int count = playerswhitelist.Count;
                if (debug) Debug.Log($"[ArenaTournament] Участников {count} [{gUNITEM.shortname} | {vs}vs{vs}]");

                if (count > 1)
                {
                    int crat = vs;
                    if (vs * 2 > count)
                    {
                        int num = count;
                        crat = (count - (num %= 2)) / 2;
                    }

                    List<BasePlayer> basePlayers = playerswhitelist.OrderBy(x => x.Value).Select(x => x.Key).ToList();
                    List<Vector3> pos1 = duelers1.ToList();
                    List<Vector3> pos2 = duelers2.ToList();


                    GenerateTeam(crat, ref basePlayers, ref team1, ref team1_players_alive, ref pos1, 1);
                    GenerateTeam(crat, ref basePlayers, ref team2, ref team2_players_alive, ref pos2, 2);

                    StartDuel();
                }
                else GG();
            }

            private void GenerateTeam(int crat, ref List<BasePlayer> basePlayers, ref List<BasePlayer> players, ref List<BasePlayer> alive, ref List<Vector3> poslist, int skinteam)
            {
                while (players.Count < crat)
                {
                    BasePlayer player = basePlayers.FirstOrDefault();
                    basePlayers.Remove(player);
                    Vector3 pos = GetDuelersPosition(poslist);
                    poslist.Remove(pos);
                    players.Add(player);
                    alive.Add(player);
                    ServerMgr.Instance.StartCoroutine(ToDuel(player, pos, skinteam));
                }
            }

            private void RefreshTeams()
            {
                CancelInvoke(nameof(OneSecond));
                RefreshTimer();

                team1_players_alive.Clear();
                team2_players_alive.Clear();

                List<Vector3> pos1 = duelers1.ToList();
                List<Vector3> pos2 = duelers2.ToList();

                foreach (BasePlayer player in team1)
                {
                    if (!playerswhitelist.ContainsKey(player)) continue;
                    Vector3 pos = GetDuelersPosition(pos1);
                    pos1.Remove(pos);
                    team1_players_alive.Add(player);
                    ServerMgr.Instance.StartCoroutine(ToDuel(player, pos, 1));
                }

                foreach (BasePlayer player in team2)
                {
                    if (!playerswhitelist.ContainsKey(player)) continue;
                    Vector3 pos = GetDuelersPosition(pos2);
                    pos2.Remove(pos);
                    team2_players_alive.Add(player);
                    ServerMgr.Instance.StartCoroutine(ToDuel(player, pos, 2));
                }

                StartDuel();
            }

            private IEnumerator ToDuel(BasePlayer player, Vector3 position, int skinteam)
            {
                TP(player, position);
                DuelerItems(player, skinteam);
                yield break;
            }

            private void WhoWINNN()
            {
                float health1 = team1_players_alive.Sum(x => x.health);
                float health2 = team2_players_alive.Sum(x => x.health);

                if (health1 > health2)
                {
                    foreach (var x in team2) Killed(x);
                }
                else if (health1 < health2)
                {
                    foreach (var x in team2) Killed(x);
                }
                else
                {
                    float Rand = Random.Range(0, 1f);
                    if (0.5f > Rand)
                    {
                        foreach (var x in team1) Killed(x);
                    }
                    else
                    {
                        foreach (var x in team2) Killed(x);
                    }
                }
            }

            #region Round
            private void StartDuel()
            {
                var ts = TimeSpan.FromSeconds(seconds);
                string text2 = $"{ts.Minutes}:{ts.Seconds.ToString("00")}";

                foreach (BasePlayer player in team1)
                {
                    if (!playerswhitelist.ContainsKey(player)) continue;
                    if (playerswhitelist[player] > round) round = playerswhitelist[player];
                }
                foreach (BasePlayer player in team2)
                {
                    if (!playerswhitelist.ContainsKey(player)) continue;
                    if (playerswhitelist[player] > round) round = playerswhitelist[player];
                }

                foreach (BasePlayer player in playerswhitelist.Keys)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "arenamain");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "exitR");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.GUI.Replace("{n}", bo).Replace("{exit}", fermens.GetMessage("btnexit", player.UserIDString)).Replace("{timer}", text2));
                }

                InvokeRepeating(nameof(OneSecond), 1f, 1f);

                UIRound();
            }

            private void UIRound()
            {
                bool final = team1_players_alive.Count + team2_players_alive.Count == playerswhitelist.Count;
                string text = (1 + team1_score + team2_score).ToString();
                string alive = playerswhitelist.Count.ToString();

                string nc1 = team1_players_alive.Count.ToString();
                string nc2 = team2_players_alive.Count.ToString();

                foreach (BasePlayer player in playerswhitelist.Keys)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "td");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.td.Replace("{teams}", final ? fermens.GetMessage("uiPanelRoundFinal", player.UserIDString) : fermens.GetMessage("uiPanelRound", player.UserIDString).Replace("{round}", text)));
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "tms");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.tms.Replace("{text}", fermens.GetMessage("uiPanelAlives", player.UserIDString).Replace("{count}", alive)));
                    UIt1(player, team1_score.ToString());
                    UIt2(player, team2_score.ToString());
                    UIp1(player, nc1);
                    UIp2(player, nc2);
                    string t = fermens.GetMessage("chatRoundStart", player.UserIDString).Replace("{round}", final ? fermens.GetMessage("uiPanelRoundFinal", player.UserIDString) : fermens.GetMessage("uiPanelRound", player.UserIDString).Replace("{round}", text)).Replace("{team1_players}", string.Join(", ", team1.Where(x => playerswhitelist.ContainsKey(x)).Select(p => p.displayName).ToArray())).Replace("{team2_players}", string.Join(", ", team2.Where(x => playerswhitelist.ContainsKey(x)).Select(p => p.displayName).ToArray())).Replace("{team1_count}", nc1).Replace("{team2_count}", nc2);
                    SendMessage(player, t);
                }

                Debug.Log($"[ArenaTournament] <R{text}> [{string.Join(", ", team1.Where(x => playerswhitelist.ContainsKey(x)).Select(p => p.displayName).ToArray())}] {team1.Count(x => playerswhitelist.ContainsKey(x))}vs{team2.Count(x => playerswhitelist.ContainsKey(x))} {string.Join(", ", team2.Where(x => playerswhitelist.ContainsKey(x)).Select(p => p.displayName).ToArray())}");
            }


            private void SendMessage(BasePlayer player, string text)
            {
                if (!fermens.config.disablenotify && fermens.Notify != null) fermens.Notify.Call("SendNotify", player.userID.Get(), 0, text);
                else player.SendConsoleCommand("chat.add", 2, 1, text);

                player.SendConsoleCommand("echo " + text);
            }

            private void UIt1(BasePlayer player, string text)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "t1");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.n1.Replace("{n1}", text));
            }

            private void UIt2(BasePlayer player, string text)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "t2");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.n2.Replace("{n2}", text));
            }

            private void UIp1(BasePlayer player, string text)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "ww");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.p1.Replace("{pl1}", text));
            }

            private void UIp2(BasePlayer player, string text)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "tt2");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.p2.Replace("{pl2}", text));
            }
            #endregion

            #region SecondsUI
            private void UISeconds()
            {
                var ts = TimeSpan.FromSeconds(seconds);
                string text = $"{ts.Minutes}:{ts.Seconds.ToString("00")}";
                foreach (BasePlayer player in playerswhitelist.Keys)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "tt");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.tt.Replace("{timer}", text));
                }
            }

            private void OneSecond()
            {
                seconds--;
                UISeconds();
                if (seconds <= 0)
                {
                    CancelInvoke(nameof(OneSecond));
                    WhoWINNN();
                }
            }
            #endregion

            private RelationshipManager.PlayerTeam GetTeam(ulong ID) => RelationshipManager.ServerInstance.FindTeam(ID);

            public bool Killed(BasePlayer player)
            {
                if (team1_players_alive.Contains(player))
                {
                    KilledPlayer(player, ref team1_players_alive, 1);
                    return true;
                }

                if (team2_players_alive.Contains(player))
                {
                    KilledPlayer(player, ref team2_players_alive, 2);
                    return true;
                }

                return false;
            }

            private void KilledPlayer(BasePlayer player, ref List<BasePlayer> players_alive, int tnum)
            {
                players_alive.Remove(player);
                TP(player, GetSpectatorPosition());
                SpectatorItems(player);
                if (players_alive.Count == 0)
                {
                    if (tnum == 1)
                    {
                        team2_score += 1;
                        if (team2_score >= needwins || team1.Count(x => playerswhitelist.ContainsKey(x)) == 0)
                        {
                            ServerMgr.Instance.StartCoroutine(EndDuel(team2, team1, team2_players_alive));
                            return;
                        }
                    }
                    else
                    {
                        team1_score += 1;
                        if (team1_score >= needwins || team2.Count(x => playerswhitelist.ContainsKey(x)) == 0)
                        {
                            ServerMgr.Instance.StartCoroutine(EndDuel(team1, team2, team1_players_alive));
                            return;
                        }
                    }

                    RefreshTeams();
                }

                if (tnum == 1) foreach (BasePlayer pl in playerswhitelist.Keys) UIp1(pl, players_alive.Count.ToString());
                else foreach (BasePlayer pl in playerswhitelist.Keys) UIp2(pl, players_alive.Count.ToString());
            }

            private Item GiveItem(ItemContainer itemContainer, ref List<Item> itms, string name, ulong skin = 0UL, int amount = 1)
            {
                if (amount == 0) return null;
                global::Item item = ItemManager.CreateByName(name, amount, skin);
                if (item != null)
                {
                    item.MoveToContainer(itemContainer, allowStack: false, ignoreStackLimit: true);
                    itms.Add(item);
                }
                return item;
            }

            private Item GiveItem(ItemContainer itemContainer, ref List<Item> itms, ItemDefinition itemDefinition, ulong skin = 0UL, int amount = 1)
            {
                global::Item item = ItemManager.Create(itemDefinition, amount, skin);
                if (item != null)
                {
                    item.MoveToContainer(itemContainer, allowStack: false, ignoreStackLimit: true);
                    itms.Add(item);
                }
                return item;
            }

            public void SpectatorItems(BasePlayer player)
            {
                if (player.IsWounded()) player.StopWounded();
                player.inventory.Strip();
                var wear = player.inventory.containerWear;
                foreach (var item in wears)
                {
                    GiveItem(wear, ref items, item.Key, item.Value);
                }
                var belt = player.inventory.containerBelt;
                foreach (var item in additional) GiveItem(belt, ref items, item.Key, amount: item.Value);
            }

            private void DuelerItems(BasePlayer player, int skinteam)
            {
                if (player.IsWounded()) player.StopWounded();
                player.inventory.Strip();
                var wear = player.inventory.containerWear;
                foreach (var item in gUNITEM.wear)
                {
                    GiveItem(wear, ref items_duelers, item.Key, skinteam == 1 ? item.Value.skinteam1 : item.Value.skinteam2);
                }
                var belt = player.inventory.containerBelt;
                Item item1 = GiveItem(belt, ref items_duelers, gUNITEM.shortname, gUNITEM.skin);

                BaseProjectile weapon = item1.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (weapon.primaryMagazine != null)
                    {
                        weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                        GiveItem(player.inventory.containerMain, ref items_duelers, weapon.primaryMagazine.ammoType, amount: 666666);
                    }
                }

                ItemContainer contents = item1.contents;
                if (contents != null) foreach (var item in gUNITEM.moduls) GiveItem(contents, ref items_duelers, item);
                foreach (var item in gUNITEM.additional) GiveItem(belt, ref items_duelers, item.Key, amount: item.Value);
                player.SendNetworkUpdateImmediate(false);
            }

            public IEnumerator EndDuel(List<BasePlayer> win, List<BasePlayer> lose, List<BasePlayer> alive)
            {
                CancelInvoke(nameof(OneSecond));
                RefreshTimer();

                LoseDuel(lose);
                WinDuel(win);

                foreach (BasePlayer player in alive)
                {
                    if (debug) Debug.Log($"alive {player.displayName}");
                    TP(player, GetSpectatorPosition());
                    SpectatorItems(player);
                }

                foreach (var item in items_duelers)
                {
                    if (item == null) continue;
                    BaseEntity heldEntity = item.GetHeldEntity();
                    if (heldEntity != null) item.Remove(0f);
                    if (item != null) item.DoRemove();
                }

                yield return CoroutineEx.waitForSeconds(1f);
                team1_score = 0;
                team2_score = 0;
                GeneratePair();

                yield break;
            }


            public void LoseDuel(List<BasePlayer> team)
            {
                int place = playerswhitelist.Count;

                Reward rew;
                if (!reward.TryGetValue(place, out rew)) rew = null;

                foreach (BasePlayer player in team)
                {
                    if (!playerswhitelist.ContainsKey(player)) continue;
                    if (rew != null) fermens.AddReward(player, rew, place);
                    if (place > 2)
                    {
                        SpectateQuote(player);
                    }
                    else
                    {
                        DeletePlayer(player);
                    }
                }
            }

            private void SpectateQuote(BasePlayer player)
            {
                if (playerswhitelist.ContainsKey(player)) playerswhitelist.Remove(player);
                if (!IsViever(player)) viewers.Add(player);
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "Viever");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.viever.Replace("{text}", fermens.GetMessage("vtext", player.UserIDString)).Replace("{text2}", fermens.GetMessage("vtext2", player.UserIDString)).Replace("{text1}", fermens.GetMessage("btnexit", player.UserIDString)));

            }

            public void WinDuel(List<BasePlayer> team)
            {
                int count = playerswhitelist.Count;

                foreach (BasePlayer player in team)
                {
                    if (!playerswhitelist.ContainsKey(player)) continue;
                    if (count != 1)
                    {
                        ServerMgr.Instance.StartCoroutine(CreateUILine(player, fermens.GetMessage("uiMessageWinDuel", player.UserIDString)));
                    }
                    else
                    {
                        if (!fermens.config.noprizeinarow || fermens.lastwinner != player)
                        {
                            Reward rew;
                            if (reward.TryGetValue(1, out rew))
                            {
                                fermens.AddReward(player, rew, 1);
                            }
                        }

                        fermens.lastwinner = player;

                        foreach (BasePlayer pl in BasePlayer.activePlayerList)
                        {
                            string text = fermens.GetMessage("globalWinner", pl.UserIDString).Replace("{name}", player.displayName);
                            SendMessage(pl, text);
                        }
                    }

                    if (playerswhitelist.ContainsKey(player))
                    {
                        playerswhitelist[player] += 1;
                    }
                }
            }

            private BasePlayer FIND(ulong userid)
            {
                BasePlayer player = BasePlayer.FindByID(userid);
                if (player == null) player = BasePlayer.FindBot(userid);
                if (player == null /*|| !player.IsConnected*/) return null;
                return player;
            }
            private void UIBut(BasePlayer player, string text)
            {
                if (fermens.closedUIrega.Contains(player)) return;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "TextR");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", fermens.regabb.Replace("{rtext}", text));
            }

            public void AddPlayer(BasePlayer player)
            {
                if (!playerswhitelist.ContainsKey(player)) playerswhitelist.Add(player, 1);
                UIBut(player, fermens.GetMessage("uiButtonCancelReg", player.UserIDString));

                BasePlayer saveman = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", new Vector3(1999f, 600f, 1999f)) as BasePlayer;
                if (saveman != null)
                {
                    saveman.enableSaving = true;
                    saveman.Spawn();
                    fermens.God(saveman);

                    saveman.inventory.Strip();

                    foreach (Item item in player.inventory.containerBelt.itemList.ToList()) item.MoveToContainer(saveman.inventory.containerBelt, item.position, false, true);
                    foreach (Item item in player.inventory.containerMain.itemList.ToList()) item.MoveToContainer(saveman.inventory.containerMain, item.position, false, true);
                    foreach (Item item in player.inventory.containerWear.itemList.ToList()) item.MoveToContainer(saveman.inventory.containerWear, item.position, false, true);

                    saveman.SendNetworkUpdateImmediate();
                }

                PlayerMetabolism metabolism = player.metabolism;
                fermens.savedatatp[player.userID.Get()] = new SaveDataTP
                {
                    position = player.transform.position,
                    saveMetabolism = new SaveMetabolism
                    {
                        bleeding_value = metabolism.bleeding.value,
                        calories_value = metabolism.calories.value,
                        calories_min = metabolism.calories.min,
                        dirtyness_value = metabolism.dirtyness.value,
                        health = player.health,
                        heartrate_value = metabolism.heartrate.value,
                        hydration_value = metabolism.hydration.value,
                        hydration_min = metabolism.hydration.min,
                        oxygen_value = metabolism.oxygen.value,
                        poison_value = metabolism.poison.value,
                        radiation_level_value = metabolism.radiation_level.value,
                        radiation_poison_value = metabolism.poison.value,
                        temperature_max = metabolism.temperature.max,
                        temperature_min = metabolism.temperature.min,
                        temperature_value = metabolism.temperature.value,
                        wetness_value = metabolism.wetness.value
                    },
                    saveman = saveman.userID
                };
                ServerMgr.Instance.StartCoroutine(fermens.SaveData());
                SpectatorItems(player);
                fermens.OnEvent[player.userID.Get()] = this;
                fermens.Teleport(player, GetSpectatorPosition());
                SendHealth(player);
            }

            public void SendHealth(BasePlayer player)
            {
                player.SetHealth(100f);
                player.metabolism.bleeding.value = 0;
                player.metabolism.calories.min = 0;
                player.metabolism.calories.value = 500;
                player.metabolism.hydration.min = 0;
                player.metabolism.hydration.value = 250;
                player.metabolism.oxygen.value = 1;
                player.metabolism.poison.value = 0;
                player.metabolism.radiation_level.value = 0;
                player.metabolism.radiation_poison.value = 0;
                player.metabolism.wetness.value = 0;
                player.metabolism.temperature.min = 32;
                player.metabolism.temperature.max = 32;
                player.metabolism.temperature.value = 32;
                player.metabolism.SendChangesToClient();
            }

            private void TP(BasePlayer player, Vector3 vector3)
            {
                player.Teleport(fermens.GetSafePosition(vector3));
                SendHealth(player);
            }

            public void DeletePlayer(BasePlayer player)
            {
                if (playerswhitelist.ContainsKey(player)) playerswhitelist.Remove(player);
                if (IsViever(player)) viewers.Remove(player);
                //Debug.Log("DeletePlayer");
                fermens.RemoveAEvent(player);
                Killed(player);
                if (secondsreg > 0) UIBut(player, fermens.GetMessage("uiButtonReg", player.UserIDString));
                if (!player.IsDead() && !player.IsWounded() || player.IsNpc)
                {
                    fermens.Restore(player);
                    if (player.IsConnected)
                    {
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "arenamain");
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "exitR");
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "Viever");
                    }
                }
            }

            public bool IsDueling(BasePlayer player)
            {
                if (team1_players_alive.Contains(player)) return true;
                if (team2_players_alive.Contains(player)) return true;
                return false;
            }

            public bool IsViever(BasePlayer player)
            {
                return viewers.Contains(player);
            }

            public bool IsDuelingLaster(BasePlayer player)
            {
                if (team1_players_alive.Contains(player) && team1_players_alive.Count == 1) return true;
                if (team2_players_alive.Contains(player) && team2_players_alive.Count == 1) return true;
                return false;
            }

            public Vector3 GetSpectatorPosition() => spectators[fermens.Rand(spectators.Count)];

            public Vector3 GetDuelersPosition(List<Vector3> pos) => pos[fermens.Rand(pos.Count)];

            public bool IsWList(BasePlayer player)
            {
                if (playerswhitelist.ContainsKey(player)) return true;
                if (IsViever(player)) return true;
                return false;
            }

            public bool IsTeammate(BasePlayer player1, BasePlayer player2)
            {
                if (team1_players_alive.Contains(player1) && team1_players_alive.Contains(player2)) return true;
                if (team2_players_alive.Contains(player1) && team2_players_alive.Contains(player2)) return true;
                return false;
            }

            private void OnTriggerExit(Collider collider)
            {
                if (collider == null) return;
                BasePlayer player = collider.GetComponent<BasePlayer>();
                if (player == null) return;
                if (debug) Debug.Log("exit " + player.displayName);

                if (IsWList(player))
                {
                    TP(player, GetSpectatorPosition());
                    return;
                }
            }

            private void OnTriggerEnter(Collider collider)
            {
                if (collider == null) return;
                BasePlayer player = collider.GetComponent<BasePlayer>();
                if (player == null) return;
                if (debug) Debug.Log("enter " + player.displayName);
                if (!IsWList(player))
                {
                    player.Hurt(1000f, DamageType.Explosion, player, false);
                    return;
                }
            }

            public void End()
            {
                Destroy(this);
            }

            public void GG()
            {
                if (BasePlayer.activePlayerList.Count > 0)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "arenamain");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "exitR");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "ElemR");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "Viever");
                }
                foreach (BasePlayer player in playerswhitelist.Keys.ToList()) DeletePlayer(player);
                foreach (BasePlayer player in viewers.ToList()) DeletePlayer(player);


                team2_players_alive.Clear();
                team1_players_alive.Clear();
                team2.Clear();
                team1.Clear();
                round = 0;
                playerswhitelist.Clear();
                foreach (var item in items)
                {
                    if (item == null) continue;
                    BaseEntity heldEntity = item.GetHeldEntity();
                    if (heldEntity != null) item.Remove(0f);
                    if (item != null) item.DoRemove();
                }
            }

            private void OnDestroy()
            {
                GG();
            }
        }

        #endregion

        #region SaveDataTP
        private Dictionary<ulong, SaveDataTP> savedatatp = new Dictionary<ulong, SaveDataTP>();

        class SaveMetabolism
        {
            public float health;
            public float bleeding_value;
            public float calories_min;
            public float calories_value;
            public float dirtyness_value;
            public float heartrate_value;
            public float hydration_value;
            public float hydration_min;
            public float oxygen_value;
            public float poison_value;
            public float radiation_level_value;
            public float radiation_poison_value;
            public float temperature_min;
            public float temperature_max;
            public float temperature_value;
            public float wetness_value;
        }

        private class SaveDataTP
        {
            public Vector3 position;

            public SaveMetabolism saveMetabolism;

            public ulong saveman;

            public int place;

            public Reward rewards;
        }
        #endregion

        #region Behavior - fermens#8767

        #region API
        private bool IsDueling(BasePlayer player)
        {
            Arena arena = GetAEvent(player);
            if (arena != null && arena.IsDueling(player)) return true;
            return false;
        }

        private bool IsOnTournament(ulong userid)
        {
            return OnEvent.ContainsKey(userid);
        }
        #endregion

        private Arena GetAEvent(BasePlayer player)
        {
            Arena gameObject;
            if (OnEvent.TryGetValue(player.userID.Get(), out gameObject))
            {
                return gameObject;
            }
            return null;
        }
        private void RemoveAEvent(BasePlayer player)
        {
            if (OnEvent.ContainsKey(player.userID.Get())) OnEvent.Remove(player.userID.Get());
        }

        private readonly int buildingLayer = LayerMask.GetMask("Terrain", "World", "Construction", "Deployed");

        private Vector3 GetSafePosition(Vector3 vector3)
        {
            RaycastHit hitInfo;
            if (!Physics.Raycast(vector3 + 2f * Vector3.up, Vector3.down, out hitInfo, 20f, buildingLayer) || hitInfo.collider == null)
            {
                Debug.Log("GetSafePosition NULL");
                return vector3;
            }
            return hitInfo.point;
        }

        private void AddReward(BasePlayer player, Reward reward, int place)
        {
            SaveDataTP saveDataTP;
            if (savedatatp.TryGetValue(player.userID.Get(), out saveDataTP))
            {
                saveDataTP.rewards = reward;
                saveDataTP.place = place;
            }
        }

        private void Restore(BasePlayer player)
        {
            SaveDataTP saveDataTP;
            if (savedatatp.TryGetValue(player.userID.Get(), out saveDataTP))
            {
                Teleport(player, saveDataTP.position);

                if (saveDataTP.saveman != 0UL)
                {
                    BasePlayer saveman = BasePlayer.FindBot(saveDataTP.saveman);
                    if (saveman != null)
                    {
                        if (debug) Debug.Log("saveman != null");
                        player.inventory.Strip();
                        foreach (Item item in saveman.inventory.containerBelt.itemList.ToList()) item.MoveToContainer(player.inventory.containerBelt, item.position, false, true);
                        foreach (Item item in saveman.inventory.containerMain.itemList.ToList()) item.MoveToContainer(player.inventory.containerMain, item.position, false, true);
                        foreach (Item item in saveman.inventory.containerWear.itemList.ToList()) item.MoveToContainer(player.inventory.containerWear, item.position, false, true);
                        saveman.AdminKill();
                    }
                }

                if (saveDataTP.rewards != null)
                {
                    string text = fermens.GetMessage("chatReward", player.UserIDString).Replace("{place}", saveDataTP.place.ToString());
                    if (saveDataTP.rewards.commands != null)
                    {
                        foreach (var x in saveDataTP.rewards.commands)
                        {
                            text += x.Value + "\n";
                            Server.Command(x.Key.Replace("{steamid}", player.UserIDString));
                        }
                    }

                    if (saveDataTP.rewards.iTEMs != null)
                    {
                        foreach (var x in saveDataTP.rewards.iTEMs)
                        {
                            if (x.amount == 0) continue;
                            if (x.blueprintTarget)
                            {
                                Item item = ItemManager.CreateByName("blueprintbase", x.amount, x.skin);
                                if (!string.IsNullOrEmpty(x.name)) item.name = x.name;
                                var def = ItemManager.FindItemDefinition(x.shortname);
                                if (def != null)
                                {
                                    item.blueprintTarget = def.itemid;
                                    player.GiveItem(item);

                                    text += GetMessage("chatRewardRecipt", player.UserIDString).Replace("{name}", def.displayName.translated).Replace("{amount}", x.amount.ToString());
                                }
                            }
                            else
                            {
                                Item item = ItemManager.CreateByName(x.shortname, x.amount, x.skin);
                                if (item != null)
                                {
                                    if (!string.IsNullOrEmpty(x.name)) item.name = x.name;
                                    player.GiveItem(item);
                                    text += GetMessage("chatRewardItem", player.UserIDString).Replace("{name}", item.info.displayName.translated).Replace("{amount}", x.amount.ToString());
                                }
                            }
                        }
                    }
                    player.ChatMessage(text);
                }

                player.SetHealth(saveDataTP.saveMetabolism.health);
                player.metabolism.hydration.SetValue(saveDataTP.saveMetabolism.hydration_value);
                player.metabolism.bleeding.SetValue(saveDataTP.saveMetabolism.bleeding_value);
                player.metabolism.calories.SetValue(saveDataTP.saveMetabolism.calories_value);
                player.metabolism.calories.min = saveDataTP.saveMetabolism.calories_min;
                player.metabolism.dirtyness.SetValue(saveDataTP.saveMetabolism.dirtyness_value);
                player.metabolism.heartrate.SetValue(saveDataTP.saveMetabolism.heartrate_value);
                player.metabolism.hydration.min = saveDataTP.saveMetabolism.hydration_min;
                player.metabolism.hydration.SetValue(saveDataTP.saveMetabolism.hydration_value);
                player.metabolism.oxygen.SetValue(saveDataTP.saveMetabolism.oxygen_value);
                player.metabolism.poison.SetValue(saveDataTP.saveMetabolism.poison_value);
                player.metabolism.radiation_level.SetValue(saveDataTP.saveMetabolism.radiation_level_value);
                player.metabolism.radiation_poison.SetValue(saveDataTP.saveMetabolism.radiation_poison_value);
                player.metabolism.temperature.min = saveDataTP.saveMetabolism.temperature_min;
                player.metabolism.temperature.max = saveDataTP.saveMetabolism.temperature_max;
                player.metabolism.temperature.value = saveDataTP.saveMetabolism.temperature_value;
                player.metabolism.wetness.SetValue(saveDataTP.saveMetabolism.wetness_value);
                player.metabolism.SendChangesToClient();

                savedatatp.Remove(player.userID.Get());
                RemoveAEvent(player);
                ServerMgr.Instance.StartCoroutine(fermens.SaveData());
            }
        }

        Dictionary<ulong, Arena> OnEvent = new Dictionary<ulong, Arena>();
        #endregion

        #endregion

        #region SleepTP
        private void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.userID.Get() < 99999999f)
            {
                player.Teleport(position);
                return;
            }
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

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }
        #endregion

        #region COPYPASTE
        private void OnPasteFinished(List<BaseEntity> pastedEntities, string filename)
        {
            CurrentEvent currentEvent = GetConfigEvent(filename);
            if (currentEvent == null || Vector3.Distance(pastedEntities[0].transform.position, currentEvent.position) > 200f) return;

            ArenaSetting arenaSetting = arenaSetting = new ArenaSetting();

            foreach (var entity in pastedEntities)
            {
                //DS: fermens#8767
                entity.OwnerID = 8767;
                if (entity is SirenLight)
                {
                    arenaSetting.spectators.Add(entity.transform.position);
                    entity.AdminKill();
                }
                if (entity is BaseChair)
                {
                    if (entity.ShortPrefabName == "secretlabchair.deployed")
                    {
                        arenaSetting.duelers1.Add(entity.transform.position);
                        entity.AdminKill();
                    }
                    else if (entity.ShortPrefabName == "chair.deployed")
                    {
                        arenaSetting.duelers2.Add(entity.transform.position);
                        entity.AdminKill();
                    }
                }
            }

            if (arenaSetting.spectators.Count == 0)
            {
                Debug.LogError($"[ArenaTournament] На арене {filename} не указаны респавн места для зрителей!");
            }

            if (arenaSetting.duelers1.Count == 0)
            {
                Debug.LogError($"[ArenaTournament] На арене {filename} не указаны респавн места для команды #1!");
            }

            if (arenaSetting.duelers2.Count == 0)
            {
                Debug.LogError($"[ArenaTournament] На арене {filename} не указаны респавн места для команды #2!");
            }

            SaveArena(filename, arenaSetting);
            CreateTrigger(filename, currentEvent);
        }

        private void Paste(string name, CurrentEvent currentEvent)
        {
            List<DecayEntity> list = Pool.GetList<DecayEntity>();
            Vis.Entities<DecayEntity>(currentEvent.position, 25f, list);
            if (list.Count == 0)
            {
                if (debug) Debug.Log("A");
                var options = new List<string> { "Stability", "false", "Inventories", "true", "height", currentEvent.position.y.ToString() };
                var successPaste = CopyPaste.Call("TryPasteFromVector3", currentEvent.position, 0f, name, options.ToArray());
                if (successPaste is string)
                {
                    PrintError(successPaste.ToString());
                }
                Debug.Log($"[ArenaTournament] Building an arena {name}...");
            }
            else
            {
                var larena = LoadArena(name);
                if (larena != null && larena.duelers1.Count > 0 && larena.duelers2.Count > 0 && larena.spectators.Count > 0)
                {
                    CreateTrigger(name, currentEvent);
                }
                else
                {
                    Debug.LogError(fermensEN ? $"[ArenaTournament] Specify other coordinates for {name} because another arena has already been built on the specified ones or respawn points are not specified!" : $"[ArenaTournament] Укажите другие координаты для {name} так как на указаных уже построена другая арена или не указаны точки респавна!");
                }
            }
        }
        #endregion

        #region BlockCommand
        private object OnUserCommand(IPlayer player, string com, string[] args)
        {
            com = com.TrimStart('/').Substring(com.IndexOf(".", StringComparison.Ordinal) + 1).ToLower();
            return blocker(BasePlayer.Find(player.Id), com);
        }

        private object CanTeleport(BasePlayer player, Vector3 to)
        {
            if (IsOnTournament(player.userID.Get())) return "(ツ)";
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            string com = arg.cmd.FullName.ToLower();
            if (com.Equals("chat.teamsay")) return null;
            return blocker(arg.Player(), com);
        }

        private object blocker(BasePlayer player, string com)
        {
            if (player == null) return null;
            if (com.Equals(config.chatcommand) || com.Equals("at.quit") || com.Equals("at.close") || com.Equals("at.test") || com.Equals("at.spectate") || config.allowcommands.Any(x => x == com)) return null;
            Arena arena = GetAEvent(player);
            if (arena != null)
            {
                player.ChatMessage(fermens.GetMessage("chatBlockCommand", player.UserIDString));
                if (debug) Debug.Log("block " + com);
                return true;
            }
            return null;
        }

        object OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, Name + ".admin")) return null;
            Arena arena = GetAEvent(player);
            if (arena != null) return true;
            return null;
        }
        #endregion

        #region StackModifier
        private object OnIgnoreStackSize(BasePlayer player, Item item)
        {
            if (IsOnTournament(player.userID.Get())) return (object)true;
            return null;
        }
        #endregion

        #region
        private object OnEntityMarkHostile(BaseCombatEntity entity, float duration)
        {
            if (!(entity is BasePlayer)) return null;
            if (IsOnTournament((entity as BasePlayer).userID.Get())) return false;
            return null;
        }
        #endregion
    }
}
