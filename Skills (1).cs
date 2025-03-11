using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust.Ai;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Skills", "Hougan", "0.0.1")]
      //  Слив плагинов server-rust by Apolo YouGame
    public class Skills : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        
        #region Classes

        private class Skill
        {
            [JsonProperty("Название скилла")]
            public string Name;
            [JsonProperty("Описание скилла")]
            public string Description;
            [JsonProperty("Информация дл GUI")]
            public string ActionName;
            [JsonProperty("URL изображения")]
            public string URL;
            
            [JsonProperty("Необходимые уровни для прокачки")]
            public Dictionary<float, int> skillAction = new Dictionary<float,int>();
        }

        private class PlayerSkills
        {
            internal class SkillProgress
            {
                [JsonProperty("Текущий уровень очков")]
                public int CurrentPoints;
                [JsonProperty("Общее количество очков")]
                public int TotalPoints;
                
                public SkillProgress(int startPoints, int totalPoints)
                {
                    CurrentPoints = startPoints;
                    TotalPoints = totalPoints;
                }
            }

            [JsonProperty("Следующая смена")]
            public double NextChange;
            
            [JsonProperty("Имя активного скилла")]
            public string ActiveSkill;
            [JsonProperty("Активный уровень")] 
            public float ActiveLevel;
            [JsonProperty("Улучшаемый уровень")] 
            public float ProgressLevel;

            [JsonProperty("Уровень прокачки различных скиллов")]
            public Dictionary<string, Dictionary<float, SkillProgress>> skillLevels = new Dictionary<string, Dictionary<float, SkillProgress>>();

            public void Switch(BasePlayer player, string newSkill)
            {
                if (NextChange > LogTime())
                {
                    player.ChatMessage($"Следующая самена нвыка доступна через <color=#7B68EE>{(NextChange - LogTime()).ToString("F0")} секунд</color>!");
                    return;
                }

                ActiveSkill = newSkill;
                ActiveLevel = GetCurrentLevel(newSkill);
                ProgressLevel = GetProgressLevel(newSkill);
                NextChange = LogTime() + CONF_SwitchTime;
                
                player.ChatMessage($"Вы успешно изменили навык на <color=#7B68EE>{instance.publicSkills[ActiveSkill].Name}</color>\n" +
                                   $"Прогресс изучения навыка: <color=#7B68EE>{((float) skillLevels[ActiveSkill][ProgressLevel].CurrentPoints / skillLevels[ActiveSkill][ProgressLevel].TotalPoints * 100).ToString("F1")}%</color>");
            }
            
            /// <summary>
            /// Добавление определенного количество очков для пользователя
            /// </summary>
            /// <param name="player">Пользователь для которого мы добавляем очки</param>
            /// <param name="amount">Количество очков для прокачки</param>
            public void AddPoints(BasePlayer player, int amount)
            {
                Stopwatch x = new Stopwatch();
                x.Start();
                if (!skillLevels.ContainsKey(ActiveSkill))
                {
                    // Interface.Oxide.LogWarning($"Error #1 | Try add {amount} to {ActiveSkill}, player don't have it!");
                    return;
                }

                var currentProgress = skillLevels[ActiveSkill][ProgressLevel];
                
                // Добавляем новые очки, только если не хватает до конца уровня
                if (ActiveLevel != ProgressLevel)
                {
                    currentProgress.CurrentPoints += amount;
                    instance.UI_CurrentProgress(player, false, progressUpdate: true);
                }

                // Если переваливаем за конец уровня
                if (currentProgress.CurrentPoints >= currentProgress.TotalPoints)
                {
                    // И количесвто очков текущих != финальному (11 из 10 например)
                    if (ActiveLevel != ProgressLevel)
                    {
                        // Повышаем человеку уровень и приравниваем очки
                        currentProgress.CurrentPoints = currentProgress.TotalPoints;
                        ActiveLevel = GetCurrentLevel(ActiveSkill);
                        ProgressLevel = GetProgressLevel(ActiveSkill);
                        // Interface.Oxide.LogWarning($"DEBUG | Player {player.displayName} successful finished his level [A: {ActiveLevel} / P {ProgressLevel}]");
                        
                        player.ChatMessage($"Вы улучшили навык <color=#7B68EE>{instance.publicSkills[ActiveSkill].Name}</color> до <color=#7B68EE>{skillLevels[ActiveSkill].Keys.ToList().IndexOf(ActiveLevel) + 1}-ого</color> уровня!");
                        instance.UI_CurrentProgress(player);
                    }
                }
                else
                {
                    // Interface.Oxide.LogWarning($"DEBUG | Player {player.displayName} now have {currentProgress.CurrentPoints} / {currentProgress.TotalPoints} [{ProgressLevel}]");
                }
                x.Stop();
                // Interface.Oxide.LogWarning($"DEBUG | Player {player.displayName} successful got point [{x.Elapsed.TotalSeconds} ms]");
            }

            /// <summary>
            /// Получение текущего уровня прокачки для игрока
            /// </summary>
            /// <param name="name">Имя скилла</param>
            /// <returns>Получаем процентное значение</returns>
            public float GetProgressLevel(string name)
            {
                if (!skillLevels.ContainsKey(name))
                {
                    // Interface.Oxide.LogWarning($"Error #2 | Try get MaxLevel from {name}, player don't have it!");
                    return -1;
                }

                for (int i = 0; i < skillLevels[name].Count; i++)
                {
                    SkillProgress progress = skillLevels[name].ElementAt(i).Value;
                    
                    if (progress.CurrentPoints < progress.TotalPoints)
                        return skillLevels[name].ElementAt(i).Key;
                }

                return skillLevels[name].Last().Key;
            }

            /// <summary>
            /// Получение текущего активного уровня для игрока
            /// </summary>
            /// <param name="name">Имя скилла</param>
            /// <returns>Получаем процентное значение</returns>
            public float GetCurrentLevel(string name)
            {
                if (!skillLevels.ContainsKey(name))
                {
                    // Interface.Oxide.LogWarning($"Error #2 | Try get CurrentLevel from {name}, player don't have it!");
                    return -1;
                }

                for (int i = 0; i < skillLevels[name].Count; i++)
                {
                    SkillProgress progress = skillLevels[name].ElementAt(i).Value;
                    
                    if (progress.CurrentPoints < progress.TotalPoints)
                        return skillLevels[name].ElementAt(i-1).Key;
                }

                return skillLevels[name].Last().Key;
            }
        }

        #endregion

        #region Variables

        #region Configuration

        [JsonProperty("Интервал смены навыка")]
        public static float CONF_SwitchTime = 60;
        [JsonProperty("Увеличенный удар критом")]
        public static float CONF_KritMultiplier = 1.6f;
        
        [JsonProperty("Количество очков за убийство игрока")]
        public static int CONF_PlayerKill = 1;
        [JsonProperty("Количество очков за убийство животного")]
        public static int CONF_AnimalKill = 1;
        [JsonProperty("Количество очков за добычу ресурсов")]
        public static int CONF_Dispenser = 1;
        [JsonProperty("Количество очков за поднятие ресурса")]
        public static int CONF_GatherUp = 1;
        [JsonProperty("Количество очков за открытие ящика с лутом")]
        public static int CONF_LootEntry = 1;
        [JsonProperty("Количество очков за уничтожение бочки")]
        public static int CONF_BarDestroy = 1;
        [JsonProperty("Количество очков за урон по вертолёту")]
        public static int CONF_HeliDamage = 1;

        #endregion

        [JsonProperty("Слой для меню выбора скилла")]
        private string MenuLayer = "UI_InterfaceMenuLayer";
        [JsonProperty("Слой для отображения текущего скилла")]
        private string CurrentLayer = "UI_InterfaceCurrentLevel";
        
        [JsonProperty("Конкретный экземпляр")] 
        private static Skills instance;
        [JsonProperty("Игроки и их скиллы")]
        private Dictionary<ulong, PlayerSkills> playerSkills = new Dictionary<ulong, PlayerSkills>();
        [JsonProperty("Общая информация о скиллах")]
        private Dictionary<string, Skill> publicSkills = new Dictionary<string, Skill>
        {
            ["ProtectFromDamage"] = new Skill
            {
                Name = "Щит",
                URL = "https://i.imgur.com/RYeLsQu.png",
                ActionName = "защита",
                Description = "Увеличивает вашу защиту от любого получаемого урона",
                skillAction = new Dictionary<float, int>
                {
                    [6f] = -1,
                    [9f] = 5,
                    [12f] = 10,
                    [15f] = 15,
                    [18f] = 20
                }
            },
            ["AddMoreDamage"] = new Skill
            {
                Name = "Ярость",
                URL = "https://i.imgur.com/1Um5Xzy.png",
                ActionName = "урон",
                Description = "Увеличивает наносимый вами урон по игрокам и животным",
                skillAction = new Dictionary<float, int>
                {
                    [6f] = -1,
                    [9f] = 5,
                    [12f] = 10,
                    [15f] = 15,
                    [18f] = 20
                }
            },
            ["RegenerateHP"] = new Skill
            {
                Name = "Вампиризм",
                URL = "https://i.imgur.com/7PV5Qsh.png",
                ActionName = "реген",
                Description = "Восстанавливает HP в процентах от нанесенного вами урона по игрокам и животным",
                skillAction = new Dictionary<float, int>
                {
                    [7f] = -1,
                    [10.5f] = 5,
                    [14f] = 10,
                    [17.5f] = 15,
                    [21f] = 20
                }
            },
            ["YouCanDodgeAttack"] = new Skill
            {
                Name = "Провидец",
                URL = "https://i.imgur.com/kI1XmnD.png",
                ActionName = "шанс",
                Description = "Даёт вам шанс уклониться от каждой вражеской атаки",
                skillAction = new Dictionary<float, int>
                {
                    [5f] = -1,
                    [7.5f] = 5,
                    [10f] = 10,
                    [12.5f] = 15,
                    [15f] = 20
                }
            },
            ["AdditionalDamageFromKrit"] = new Skill
            {
                Name = "Криты",
                URL = "https://i.imgur.com/AMXPFKm.png",
                ActionName = "шанс",
                Description = "Даёт вам шанс при каждой атаке нанести x{0} урона игрокам и животным",
                skillAction = new Dictionary<float, int>
                {
                    [5f] = -1,
                    [7.5f] = 5,
                    [10f] = 10,
                    [12.5f] = 15,
                    [15f] = 20
                }
            },
            ["ReturnSomeDamage"] = new Skill
            {
                Name = "Шипы",
                URL = "https://i.imgur.com/MEWZtC2.png",
                ActionName = "возврат",
                Description = "Возвращает часть полученного вами урона обратно врагу",
                skillAction = new Dictionary<float, int>
                {
                    [9f] = -1,
                    [13.5f] = 5,
                    [18f] = 10,
                    [22.5f] = 15,
                    [27f] = 20
                }
            },
        };

        #endregion

        #region Initialization

        private void OnServerInitialized()
        {
            instance = this;
            
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Skills/SkillList"))
            {
                publicSkills = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Skill>>("Skills/SkillList");
                // PrintWarning($"Загружена информация о {publicSkills.Count} скиллах.");
            }
            else
            {
                Interface.Oxide.DataFileSystem.WriteObject("Skills/SkillList", publicSkills);
                // PrintWarning($"Загружены стандартные настройки скиллов");
            }

            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Skills/PlayerSkills"))
            {
                playerSkills = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerSkills>>("Skills/PlayerSkills");
                // PrintWarning($"Загружена информация о {playerSkills.Count} игроках.");
            }

            foreach (var check in publicSkills)
                ImageLibrary.Call("AddImage", check.Value.URL, check.Key);
            
            BasePlayer.activePlayerList.ForEach(OnPlayerInit);
        }

        private void OnNewSave(string filename) => Save();
        private void Unload() => Save();

        private void Save()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Skills/SkillList", publicSkills);
            Interface.Oxide.DataFileSystem.WriteObject("Skills/PlayerSkills", playerSkills);
            // PrintWarning($"Сохранена информация об успехах игроков");
        }

        #endregion

        #region Hooks

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() =>
                {
                    OnPlayerInit(player);
                    return;
                });
                return;
            }
            Stopwatch x = new Stopwatch();
            x.Start();
            if (!playerSkills.ContainsKey(player.userID))
            {
                PlayerSkills newPlayerSkill = new PlayerSkills();
                foreach (var skill in publicSkills)
                {
                    Dictionary<float, PlayerSkills.SkillProgress> skillPoints = new Dictionary<float, PlayerSkills.SkillProgress>();
                    foreach (var skillPoint in skill.Value.skillAction)
                        skillPoints.Add(skillPoint.Key, new PlayerSkills.SkillProgress(0, skillPoint.Value));
                    
                    newPlayerSkill.skillLevels.Add(skill.Key, skillPoints);
                }
                newPlayerSkill.ActiveSkill = newPlayerSkill.skillLevels.Keys.ToList().GetRandom();
                newPlayerSkill.ActiveLevel = newPlayerSkill.GetCurrentLevel(newPlayerSkill.ActiveSkill);
                newPlayerSkill.ProgressLevel = newPlayerSkill.GetProgressLevel(newPlayerSkill.ActiveSkill);
                newPlayerSkill.NextChange = LogTime();
                
                playerSkills.Add(player.userID, newPlayerSkill);
                player.ChatMessage($"Вам активирован случайный скилл <color=#7B68EE>{publicSkills[newPlayerSkill.ActiveSkill].Name}</color>\n" +
                                   $"Используйте <color=#7B68EE>/skill</color>, чтобы узнать подробности.");
            }
            x.Stop();
            // PrintWarning($"DEBUG | {player.displayName} successful get all Skills [{x.Elapsed.TotalSeconds} ms]");
            // PrintWarning($"DEBUG | Active skill {playerSkills[player.userID].ActiveSkill} [C: {playerSkills[player.userID].ActiveLevel} / P: {playerSkills[player.userID].ProgressLevel}]");
            
            CuiHelper.DestroyUi(player, CurrentLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.0105 0.038", AnchorMax = "0 0.038", OffsetMax = "188 27" },
                Image = { Color = "0.32 0.32 0.41 0.22" }
            }, "Hud", CurrentLayer);
            
            CuiHelper.AddUi(player, container);
            
            UI_CurrentProgress(player);
        }

        #region SkillRealization

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
      //  Слив плагинов server-rust by Apolo YouGame
        {
            try
            {
                if (info != null && entity is BaseHelicopter && info.Initiator is BasePlayer &&
                    info.InitiatorPlayer.GetComponent<NPCPlayer>() == null)
                {
                    playerSkills[info.InitiatorPlayer.userID].AddPoints(info.InitiatorPlayer, CONF_HeliDamage);
                    return;
                }

                // Начинаем отрабатывать если речь идёт об игроке и он не NPC
                if (entity is BasePlayer && entity.GetComponent<NPCPlayer>() == null && info != null)
                {
                    BasePlayer player = entity as BasePlayer;
                    BasePlayer target = null;

                    // Сразу берем и проверяем инициатора
                    if (info.Initiator is BasePlayer && info.Initiator.GetComponent<NPCPlayer>() == null)
                        target = info.InitiatorPlayer;

                    // Снижаем нагрузку за счёт не рассчитывания маленького урона
                    if (info.damageTypes.Total() < 5)
                        return;
                    
                    // Продолжаем работать, если у игрока есть скиллы
                    if (playerSkills.ContainsKey(player.userID))
                    {
                        // PrintWarning($"DEBUG | Start working on target skills [{player.displayName}]");
                        PlayerSkills playerSkill = this.playerSkills[player.userID];

                        switch (playerSkill.ActiveSkill.ToLower())
                        {
                            case "protectfromdamage":
                            {
                                // PrintWarning(
                                //    $"DEBUG | {player.displayName} should get {info.damageTypes.Total()} damage, but {playerSkill.ActiveSkill} active");
                                float protectMultiplier = 1 - playerSkill.ActiveLevel / 100;
                                info.damageTypes.ScaleAll(protectMultiplier);

                                // PrintWarning(
                                //    $"DEBUG | {player.displayName} will protect {playerSkill.ActiveLevel}% from his skill [{info.damageTypes.Total()}]");
                                break;
                            }
                            case "addmoredamage":
                            {
                                // NOTHING HERE, PLAYER GET DAMAGE, NO REASON TO COUNT HIS ADDITIONAL DAMAGE
                                break;
                            }
                            case "regeneratehp":
                            {
                                // NOTHING HERE, PLAYER GET DAMAGE, NO REASON TO COUNT HIS ADDITIONAL DAMAGE
                                break;
                            }
                            case "youcandodgeattack":
                            {
                                // PrintWarning(
                                //    $"DEBUG | {player.displayName} [{player.health} HP] should get {info.damageTypes.Total()} damage, but {playerSkill.ActiveSkill} active");

                                int generateNumber = Core.Random.Range(0, 100);
                                if (generateNumber <= playerSkill.ActiveLevel)
                                {
                                    player.ChatMessage(
                                        $"Навык <color=#7B68EE>{publicSkills[playerSkill.ActiveSkill].Name}</color> помог вам уклониться от атаки!");
                                    // PrintWarning(
                                    //    $"DEBUG | {player.displayName} won! {generateNumber} <= {playerSkill.ActiveLevel}! Damage is null!");
                                    info.damageTypes.ScaleAll(0);
                                    // PrintWarning(
                                    //    $"DEBUG | {player.displayName} won! {generateNumber} <= {playerSkill.ActiveLevel}! Damage is null! [{player.health} HP]");
                                }
                                else
                                {
                                    // PrintWarning(
                                    //    $"DEBUG | {player.displayName} lose! {generateNumber} > {playerSkill.ActiveLevel}! Damage is null!");
                                }

                                break;
                            }
                            case "additionaldamagefromkrit":
                            {
                                // NOTHING HERE, PLAYER GET DAMAGE, NO REASON TO COUNT HIS ADDITIONAL DAMAGE
                                break;
                            }
                            case "returnsomedamage":
                            {
                                // PrintWarning(
                                //    $"DEBUG | {player.displayName} will get {info.damageTypes.Total()} damage, but {playerSkill.ActiveSkill} active");
                                if (target != null)
                                {
                                    // PrintWarning(
                                    //    $"DEBUG | {target.displayName} will get {playerSkill.ActiveLevel}% from given damage!");
                                    target.Hurt(info.damageTypes.Total() * (playerSkill.ActiveLevel / 100));
                                }

                                break;
                            }
                            default:
                            {
                                // PrintWarning($"DEBUG | {player.displayName} have unknow skill");
                                break;
                            }
                        }

                        if (target != null && playerSkills.ContainsKey(target.userID))
                        {
                            // PrintWarning($"DEBUG | Start working on target skills [{target.displayName}]");
                            PlayerSkills targetSkill = this.playerSkills[target.userID];

                            switch (targetSkill.ActiveSkill.ToLower())
                            {
                                case "protectfromdamage":
                                {
                                    // NOTHING HERE, PLAYER GET DAMAGE, NO REASON TO COUNT HIS PROTECTION DAMAGE
                                    break;
                                }
                                case "addmoredamage":
                                {
                                    // PrintWarning(
                                    //    $"DEBUG | {player.displayName} should get {info.damageTypes.Total()} damage, but {target.displayName}'s {targetSkill.ActiveSkill} active");
                                    float damageMultiplier = 1 + targetSkill.ActiveLevel / 100;
                                    info.damageTypes.ScaleAll(damageMultiplier);

                                    // PrintWarning(
                                    //    $"DEBUG | {player.displayName} will get additional {targetSkill.ActiveLevel}% from {target.displayName}' skill [{info.damageTypes.Total()}]");
                                    break;
                                }
                                case "regeneratehp":
                                {
                                    // PrintWarning(
                                    //    $"DEBUG | {target.displayName} [{target.health} HP] will get {info.damageTypes.Total() * (targetSkill.ActiveLevel / 100)} HP, cause of {targetSkill.ActiveSkill} active");
                                    target.Heal(info.damageTypes.Total() * (targetSkill.ActiveLevel / 100));
                                    // PrintWarning($"DEBUG | {target.displayName} now have {target.health} HP");
                                    break;
                                }
                                case "youcandodgeattack":
                                {
                                    // NOTHING HERE, PLAYER GET DAMAGE, NO REASON TO COUNT HIS PROTECTION DAMAGE
                                    break;
                                }
                                case "additionaldamagefromkrit":
                                {
                                    // PrintWarning(
                                    //    $"DEBUG | {target.displayName} can give more {info.damageTypes.Total() * CONF_KritMultiplier} damage [{info.damageTypes.Total()} start], cause of {targetSkill.ActiveSkill} active");

                                    int randomAmount = Oxide.Core.Random.Range(0, 100);
                                    if (randomAmount <= targetSkill.ActiveLevel)
                                    {
                                        target.ChatMessage(
                                            $"Навык <color=#7B68EE>{publicSkills[targetSkill.ActiveSkill].Name}</color> помог вам нанести дополнительный урон!");
                                        // PrintWarning(
                                        //    $"DEBUG | {target.displayName} BINGO [{randomAmount}]! He will give additional damage {info.damageTypes.Total()} -> {info.damageTypes.Total() * (CONF_KritMultiplier)}");
                                        info.damageTypes.ScaleAll((CONF_KritMultiplier));
                                    }
                                    else
                                    {
                                        // PrintWarning(
                                        //    $"DEBUG | {target.displayName} LOSE [{randomAmount}]. No additional damage!");
                                    }

                                    break;
                                }
                                case "returnsomedamage":
                                {
                                    // NOTHING HERE, PLAYER GET DAMAGE, NO REASON TO COUNT HIS PROTECTION DAMAGE
                                    break;
                                }
                                default:
                                {
                                    // PrintWarning($"DEBUG | {player.displayName} have unknow skill");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // PrintWarning($"DEBUG | No additional player!");
                        }
                    }
                    else
                    {
                        // PrintWarning($"DEBUG | {player.displayName} try to use skill, but he dont have any skills!");
                    }
                }
            }
            catch (NullReferenceException)
            {
                
            }
        }

        #endregion

        #region Points Add

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            // Если это не игрок, или если это НПС - не делаем ничего
            if (entity is BasePlayer && entity.GetComponent<NPCPlayer>() == null)
            {
                BasePlayer player = entity as BasePlayer;
                
                if (playerSkills.ContainsKey(player.userID))
                {
                    // Добавляем игроку очки за разбитую бочку
                    playerSkills[player.userID].AddPoints(player, CONF_Dispenser);
                }
                else
                {
                    // PrintWarning($"DEBUG | {player.displayName} [{player.userID}] not in Player Skills!");
                    return;
                }
            }
            return;
        }
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
      //  Слив плагинов server-rust by Apolo YouGame
        {
            if (info != null && info.Initiator is BasePlayer && info.InitiatorPlayer.GetComponent<NPCPlayer>() != null)
            {
                BasePlayer attacker = info.InitiatorPlayer;

                if (!playerSkills.ContainsKey(attacker.userID))
                    return;

                PlayerSkills playerSkill = playerSkills[attacker.userID];

                if (entity is BasePlayer && entity.GetComponent<NPCPlayer>() == null)
                {
                    playerSkill.AddPoints(attacker, CONF_PlayerKill);
                    return;
                }

                if (entity.PrefabName.Contains("agent"))
                {
                    playerSkill.AddPoints(attacker, CONF_AnimalKill);
                    return;
                }

                if (entity.PrefabName.Contains("barrel"))
                {
                    playerSkill.AddPoints(attacker, CONF_BarDestroy);
                    return;
                }
            }
        }
        
        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (!playerSkills.ContainsKey(player.userID))
                return;

            PlayerSkills playerSkill = playerSkills[player.userID];
            playerSkill.AddPoints(player, CONF_GatherUp);
        }
        
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is LootContainer) || entity.OwnerID != 0)
                return;
            
            if (!playerSkills.ContainsKey(player.userID))
                return;

            PlayerSkills playerSkill = playerSkills[player.userID];
            playerSkill.AddPoints(player, CONF_LootEntry);
            entity.OwnerID = player.userID;
        }

        #endregion

        #endregion

        #region Commands

        [ConsoleCommand("UI_SkillHandler")]
        private void consoleCommandHandler(ConsoleSystem.Arg args)
        {
            if (args.Player() == null || !args.HasArgs(1))
                return;

            BasePlayer player = args.Player();

            switch (args.Args[0].ToLower())
            {
                case "switch":
                {
                    if (args.Args.Length != 2)
                    {
                        // PrintWarning($"DEBUG | {player.displayName} called Handler, but no args to switch!");
                        return;
                    }

                    string newSkill = args.Args[1];
                    if (!publicSkills.ContainsKey(newSkill))
                    {
                        // PrintWarning($"DEBUG | {player.displayName} called switch, but no skill with this name to switch!");
                        return;
                    }

                    CuiHelper.DestroyUi(player, MenuLayer);
                    playerSkills[player.userID].Switch(player, newSkill);
                    UI_ChooseSkill(player);
                    UI_CurrentProgress(player);
                    break;
                }
            }
        }

        [ChatCommand("skill")]
        private void cmdChatSkill(BasePlayer player) => UI_ChooseSkill(player);

        //[ChatCommand("testik")]
        private void cmdTest(BasePlayer player)
        {
            playerSkills[player.userID].AddPoints(player, 2);
        }

        #endregion

        #region GUI

        private void UI_CurrentProgress(BasePlayer player, bool avatarUpdate = true, bool levelUpdate = true, bool progressUpdate = true)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            if (progressUpdate)
            {
                PlayerSkills playerSkill = playerSkills[player.userID];
                int levelNumber = playerSkill.skillLevels[playerSkill.ActiveSkill].Keys.ToList()
                    .IndexOf(playerSkill.ActiveLevel) + 1;
                var progress = playerSkill.skillLevels[playerSkill.ActiveSkill][playerSkill.ProgressLevel];
                
                CuiHelper.DestroyUi(player, CurrentLayer + ".progressUpdate"); 
                container.Add(new CuiElement
                {
                    FadeOut = 0.2f,
                    Name = CurrentLayer + ".progressUpdate",
                    Parent = CurrentLayer,
                    Components =
                    {
                        new CuiImageComponent { FadeIn = 0.2f, Color = "0.48 0.41 0.93 1.00" },
                        new CuiRectTransformComponent { AnchorMin = "0.143 0.15", AnchorMax = $"{Math.Min(0.15 + 0.857 * ((float) progress.CurrentPoints / progress.TotalPoints), 1)} 0.849", OffsetMax = "0 0"}
                    }
                });
                
                CuiHelper.DestroyUi(player, CurrentLayer + ".levelUpdate");
                
                container.Add(new CuiElement
                {
                    Name = CurrentLayer + ".levelUpdate",
                    Parent = CurrentLayer,
                    Components =
                    {
                        new CuiTextComponent { Text = $"УРОВЕНЬ: {levelNumber}", FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft},
                        new CuiRectTransformComponent { AnchorMin = "0.21 0", AnchorMax = "1 1", OffsetMax = "0 0"}
                    }
                });
            }
            
            if (avatarUpdate)
            {
                CuiHelper.DestroyUi(player, CurrentLayer + ".avatarUpdate");
                container.Add(new CuiElement
                {
                    Name = CurrentLayer + ".avatarUpdate",
                    Parent = CurrentLayer,
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", playerSkills[player.userID].ActiveSkill) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "3.33333 3.33333", OffsetMax = "23.3333 23.3333"}
                    }
                });
            }
            
            CuiHelper.AddUi(player, container);
        }

        private void UI_ChooseSkill(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MenuLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                // Отрисовываем панель для ГУИ
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.09283856 0.2547454", AnchorMax = "0.09283856 0.2547454", OffsetMax = "1042 352" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", MenuLayer);
            
            container.Add(new CuiButton
            {
                // Отрисовываем кнопку для закрытия на заднем плане
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = MenuLayer },
                Text = { Text = "" }
            }, MenuLayer);

            container.Add(new CuiElement
            {
                // Отрисовываем полноценный задник
                Parent = MenuLayer,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0.35" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                }
            });

            for (int i = 0; i < publicSkills.Count; i++)
            {
                if (!playerSkills.ContainsKey(player.userID))
                    return;

                var currentSkill = publicSkills.ElementAt(i);
                PlayerSkills playerSkill = playerSkills[player.userID];
                
                container.Add(new CuiElement
                {
                    // Отрисовываем задник для интерфейса
                    Parent = MenuLayer,
                    Name = MenuLayer + $".{currentSkill.Key}.Handler",
                    Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0.5" },
                        new CuiRectTransformComponent { AnchorMin = $"{0.007197953 + i * 0.165352} 0.022438414", AnchorMax = $"{0.1621337 + i * 0.165352} 0.9775616", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    // Отрисовываем изображение для скилла
                    Parent = MenuLayer + $".{currentSkill.Key}.Handler",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", currentSkill.Key) },
                        new CuiRectTransformComponent { AnchorMin = "0.2168479 0.6943213", AnchorMax = "0.7831521 0.9544452", OffsetMax = "0 0" }
                    }
                });
                
                
                container.Add(new CuiElement
                {
                    // Отрисовываем название для скилла
                    Parent = MenuLayer + $".{currentSkill.Key}.Handler",
                    Components =
                    {
                        new CuiTextComponent { Text = $"<color=>{currentSkill.Value.Name}</color>", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter},
                        new CuiRectTransformComponent { AnchorMin = "0 0.6212121", AnchorMax = "1 0.6885524", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    // Отрисовываем полосу для уровня для скиллаа
                    Name = MenuLayer + $".{currentSkill.Key}.Handler.LevelHandler",
                    Parent = MenuLayer + $".{currentSkill.Key}.Handler",
                    Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0.6" },
                        new CuiRectTransformComponent { AnchorMin = $"{0.05789989} 0.5577442", AnchorMax = $"{0.9214915} 0.6112121", OffsetMax = "0 0" }
                    }
                });
                

                int levelNumber = currentSkill.Value.skillAction.Keys.ToList().IndexOf(playerSkill.GetCurrentLevel(currentSkill.Key));
                var progress = playerSkill.skillLevels[currentSkill.Key][playerSkill.GetProgressLevel(currentSkill.Key)];
                float levelLine = (float) progress.CurrentPoints / progress.TotalPoints;
                
                container.Add(new CuiElement
                {
                    // Отрисовываем полосу для уровня для скиллаа
                    Parent = MenuLayer + $".{currentSkill.Key}.Handler.LevelHandler",
                    Components =
                    {
                        new CuiImageComponent { Color = "0.48 0.41 0.93 1.00" },
                        new CuiRectTransformComponent { AnchorMin = $"{0} 0", AnchorMax = $"{Math.Min(levelLine, 1)} 1", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    // Отрисовываем полосу для уровня для скиллаа
                    Parent = MenuLayer + $".{currentSkill.Key}.Handler.LevelHandler",
                    Components =
                    {
                        new CuiTextComponent { Text = $"УРОВЕНЬ: {levelNumber + 1}", FontSize = 12, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = $"{0} 0", AnchorMax = $"1 1", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    // Отрисовываем полосу для уровня для скиллаа
                    Parent = MenuLayer + $".{currentSkill.Key}.Handler",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{currentSkill.Value.Description.Replace("{0}", CONF_KritMultiplier.ToString())}", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter },
                        new CuiRectTransformComponent { AnchorMin = $"0.05 0.30", AnchorMax = $"0.95 0.53", OffsetMax = "0 0" },
						new CuiOutlineComponent { Color = "0 0 0 0.9", Distance = "0.6 0.6" }
                    }
                });

                string text = playerSkill.ActiveSkill == currentSkill.Key ? "<color=red>Используется</color>" : "<color=white>Использовать</color>";
                string command = playerSkill.ActiveSkill == currentSkill.Key ? "" : $"UI_SkillHandler switch {currentSkill.Key}";
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.1 0.03", AnchorMax = "0.93 0.12", OffsetMax = "0 0" },
                    Text = { Text = text, FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                    Button = { Command = command, Color = "0.42 0.36 0.96 1" }
                }, MenuLayer + $".{currentSkill.Key}.Handler");

                for (int skillIndex = 0; skillIndex < currentSkill.Value.skillAction.Count; skillIndex++)
                {
                    var currentProgress = currentSkill.Value.skillAction.ElementAt(skillIndex);
                    
                    container.Add(new CuiElement
                    {
                        Parent = MenuLayer + $".{currentSkill.Key}.Handler",
                        Components =
                        {
                            new CuiTextComponent { Text = $"<color=#7B68EE>Ур. {skillIndex+1}:   {currentSkill.Value.ActionName} +{currentProgress.Key}% </color>", FontSize = 13, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft },
                            new CuiRectTransformComponent { AnchorMin = $"0.15 {0.145 + skillIndex * 0.04}", AnchorMax = $"1 {0.195 + skillIndex * 0.04}", OffsetMax = "0 0" },
							new CuiOutlineComponent { Color = "0 0 0 0.9", Distance = "0.6 0.6" }
                        }
                    });
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils
        
        private static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private static double LogTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        #endregion
    }
}
