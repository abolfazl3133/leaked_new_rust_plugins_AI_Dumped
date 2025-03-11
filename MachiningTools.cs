using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("MachiningTools", "Vlad-00003", "1.3.3", ResourceId = 89)]
    [Description("Creates tools that would gather refined materials from the resource nodes")]
    /*
     * Author info:
     *   E-mail: Vlad-00003@mail.ru
     *   Vk: vk.com/vlad_00003
     */
    class MachiningTools : RustPlugin
    {
        #region Vars‌‌﻿‌‍‌​
        private PluginConfig _config;
        private readonly Dictionary<ItemDefinition, ItemDefinition> _itemToCookable = new Dictionary<ItemDefinition, ItemDefinition>();
        #endregion

        #region Config‌‌﻿‌‍‌​
        private class Tool
        {
            [JsonProperty("Короткое имя предмета")]
            public string ShortName;
            [JsonProperty("ID скина предмета (Поддерживается Workshop)")]
            public ulong SkinId;
            [JsonProperty("Название предмета (Выводится в описании предмета в инвентаре)")]
            public string Name;
            [JsonProperty("Можно ли ремонтировать предмет")]
            public bool CanRepair;
            [JsonProperty("Можно ли перерабатывать предмет")]
            public bool CanRecycle;
            [JsonProperty("Настройки переработки")]
            public Transmutations Transmutation;
            [JsonProperty("Устанавливать символ огня в углу предмета")]
            public bool? ShoutSetFire;

            private ItemDefinition _info;

            public bool ItemExists()
            {
                _info = ItemManager.FindItemDefinition(ShortName);
                if (_info == null)
                    return false;

                var itemModEntity = _info.GetComponent<ItemModEntity>();
                if (itemModEntity == null)
                    return false;

                var baseMelee = GameManager.server.FindPrefab(itemModEntity.entityPrefab?.resourcePath)?.GetComponent<BaseMelee>();
                return baseMelee != null;
            }

            public Item Create(int amount = 1)
            {
                Item item = ItemManager.Create(_info,amount, SkinId);
                item.name = Name;
                return item;
            }

            public int GetCustomHash()
            {
                unchecked
                {
                    return (string.IsNullOrEmpty(ShortName) ? 0 : ShortName.GetHashCode() * 397) ^ SkinId.GetHashCode();
                }
            }

            public static int GetCustomHash(Item item)
            {
                unchecked
                {
                    if (item == null)
                        return 0;
                    return (string.IsNullOrEmpty(item.info.shortname) ? 0 : item.info.shortname.GetHashCode() * 397) ^ item.skin.GetHashCode();
                }
            }
        }
        private class Transmutations
        {
            [JsonProperty("Перерабатывать дерево в уголь")]
            private bool _wood;
            [JsonProperty("Перерабатывать руду МВК в металл")]
            private bool _hqm;
            [JsonProperty("Перерабатывать металлическую руду в фрагменты")]
            private bool _metal;
            [JsonProperty("Перерабатывать серную руду в серу")]
            private bool _sulfur;
            [JsonProperty("Перерабатывать мясо медведя в жаренное")]
            private bool _bear;
            [JsonProperty("Перерабатывать свинину в жаренную")]
            private bool _boar;
            [JsonProperty("Перерабатывать мясо курицы в жаренное")]
            private bool _chicken;
            [JsonProperty("Перерабатывать мясо лошади в жаренное")]
            private bool _horse;
            [JsonProperty("Перерабатывать мясо волка в жаренное")]
            private bool _wolf;
            [JsonProperty("Перерабатывать мясо оленя в жаренное")]
            private bool _deer;
            [JsonProperty("Перерабатывать человеческое мясо в жаренное")]
            private bool _human;

            #region Default Config‌‌﻿‌‍‌​

            public static Transmutations DefaultPick => new Transmutations
            {
                _wood = false,
                _hqm = true,
                _metal = true,
                _sulfur = true,
                _bear = false,
                _boar = false,
                _chicken = false,
                _wolf = false,
                _deer = false,
                _human = false,
                _horse = false
            };

            public static Transmutations DefaultAxe => new Transmutations
            {
                _wood = true,
                _hqm = false,
                _metal = false,
                _sulfur = false,
                _bear = true,
                _boar = true,
                _chicken = true,
                _wolf = true,
                _deer = true,
                _human = true,
                _horse = true
            };

            #endregion
            
            // ReSharper disable StringLiteralTypo
            public bool ShouldCook(Item item)
            {
                switch (item.info.shortname)
                {
                    case "humanmeat.raw":
                        return _human;
                    case "bearmeat":
                        return _bear;
                    case "chicken.raw":
                        return _chicken;
                    case "meat.boar":
                        return _boar;
                    case "deermeat.raw":
                        return _deer;
                    case "wolfmeat.raw":
                        return _wolf;
                    case "sulfur.ore":
                        return _sulfur;
                    case "metal.ore":
                        return _metal;
                    case "wood":
                        return _wood;
                    case "horsemeat.raw":
                        return _horse;
                    case "hq.metal.ore":
                        return _hqm;
                    default:
                        return false;
                }
            }
            // ReSharper restore StringLiteralTypo

        }
        private class PluginConfig
        {
            [JsonProperty("Привилегия для использования команд")]
            public string Permission;
            [JsonProperty("Команда(чат/консоль)")]
            public string Command;
            [JsonProperty("Список инструментов")]
            private Dictionary<string, Tool> _tools;

            [JsonIgnore] 
            private readonly Dictionary<int,KeyValuePair<string, Tool>> _toolsByHash = new Dictionary<int, KeyValuePair<string, Tool>>();
            [JsonIgnore]
            public readonly Dictionary<string, Tool> ToolsByKey = new Dictionary<string, Tool>();

            #region Default Config‌‌﻿‌‍‌​

            public static PluginConfig DefaultConfig => new PluginConfig
            {
                Permission = nameof(MachiningTools)+".use",
                Command = "GiveTool",
                _tools = new Dictionary<string, Tool>
                {
                    ["hatchet"] = new Tool
                    {
                        ShortName = "hatchet",
                        Name = "Магический топор",
                        CanRepair = true,
                        CanRecycle = true,
                        SkinId = 901876821,
                        Transmutation = Transmutations.DefaultAxe,
                        ShoutSetFire = false
                    },
                    ["pickaxe"] = new Tool
                    {
                        ShortName = "pickaxe",
                        Name = "Магическая кирка",
                        CanRepair = true,
                        CanRecycle = true,
                        SkinId = 902892485,
                        Transmutation = Transmutations.DefaultPick,
                        ShoutSetFire = false
                    },
                    ["icepick"] = new Tool
                    {
                        ShortName = "icepick.salvaged",
                        Name = "Магический ледоруб",
                        CanRepair = false,
                        CanRecycle = false,
                        SkinId = 804307574,
                        Transmutation = Transmutations.DefaultPick,
                        ShoutSetFire = false
                    },
                    ["axe"] = new Tool
                    {
                        ShortName = "axe.salvaged",
                        Name = "Магический топор",
                        CanRepair = false,
                        CanRecycle = false,
                        SkinId = 2057227617,
                        Transmutation = Transmutations.DefaultAxe,
                        ShoutSetFire = false
                    },
                    ["chainsaw"] = new Tool
                    {
                        ShortName = "chainsaw",
                        Name = "Магическая бензопила",
                        CanRepair = false,
                        CanRecycle = false,
                        SkinId = 2057228026,
                        Transmutation = Transmutations.DefaultAxe,
                        ShoutSetFire = false
                    },
                    ["jackhammer"] = new Tool
                    {
                        ShortName = "jackhammer",
                        Name = "Магический отбойный молоток",
                        CanRepair = false,
                        CanRecycle = false,
                        SkinId = 2057228546,
                        Transmutation = Transmutations.DefaultPick,
                        ShoutSetFire = false
                    }
                }

            };

            #endregion

            public string CheckConfig(RustPlugin plugin)
            {
                List<string> result = Pool.GetList<string>();
                var fireFlagOptionAdded = false;
                foreach (var pair in _tools)
                {
                    //version < 1.2.0
                    var canRecycle = plugin.Config["Список инструментов", pair.Key, "Можно ли перерабатывать пердмет"];
                    if (canRecycle != null)
                    {
                        pair.Value.CanRecycle = plugin.Config.ConvertValue<bool>(canRecycle);
                        result.Add($"Typo in the property of item {pair.Key} fixed.");
                    }

                    if (!pair.Value.ItemExists())
                    {
                        result.Add($"Item {pair.Key} not found in the game or not BaseMelee and would not be used.");
                        continue;
                    }

                    if (pair.Value.ShoutSetFire == null)
                    {
                        fireFlagOptionAdded = true;
                        pair.Value.ShoutSetFire = false;
                    }

                    KeyValuePair<string, Tool> defined;
                    var customHash = pair.Value.GetCustomHash();

                    if(TryGet(customHash, out defined))
                    {
                        result.Add($"Item {pair.Key} is using the same skin that was already defined by {defined.Key} and would not be used.");
                        continue;
                    }
                    ToolsByKey[pair.Key] = pair.Value;
                    _toolsByHash[customHash] = pair;
                }

                if (fireFlagOptionAdded)
                    result.Add("Option to add fire icon to items added to the config");
                
                var res = result.Count > 0 ? string.Join("\n", result) : null;
                Pool.FreeList(ref result);
                return res;
            }

            public bool TryGet(Item item, out Tool tool)
            {
                tool = null;
                var customItemHash = Tool.GetCustomHash(item);
                if (customItemHash == 0)
                    return false;
                KeyValuePair<string, Tool> toolData;
                if (!TryGet(customItemHash, out toolData))
                    return false;
                tool = toolData.Value;
                return true;
            }

            private bool TryGet(int hash, out KeyValuePair<string, Tool> pair) =>
                _toolsByHash.TryGetValue(hash, out pair);
        }
        #endregion

        #region Config handling‌‌﻿‌‍‌​

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за приобретение плагина на сайте RustPlugin.ru. Если вы приобрели этот плагин на другом ресурсе знайте - это лишает вас гарантированных обновлений!");
            _config = PluginConfig.DefaultConfig;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();

            var checkResult = _config.CheckConfig(this);
            if (checkResult == null) 
                return;
            PrintWarning(checkResult);
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion

        #region Init and quiting‌‌﻿‌‍‌​

        void Init()
        {
            AddCovalenceCommand(_config.Command, "GiveToolsCommand", _config.Permission);
        }

        void OnServerInitialized()
        {
            foreach (var itemDefinition in ItemManager.GetItemDefinitions())
            {
                var cookable = itemDefinition.GetComponent<ItemModCookable>();
                if (cookable)
                    _itemToCookable[itemDefinition] = cookable.becomeOnCooked;
            }
            _itemToCookable.Add(ItemManager.FindItemDefinition("wood"), ItemManager.FindItemDefinition("charcoal"));
        }
        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name is not ("RatesController" or "IQRates")) 
                return;
            Unsubscribe(nameof(OnDispenserBonus));
            Unsubscribe(nameof(OnDispenserGather));

            Subscribe(nameof(OnDispenserBonus));
            Subscribe(nameof(OnDispenserGather));
        }

        #endregion

        #region Oxide Hooks‌‌﻿‌‍‌​

        void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven.IsOn() == false)
                return;
            NextFrame(() =>
            {
                oven.inventory.itemList.ForEach(x => OnItemAddedToContainer(null, x));
            });
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            Tool tool;
            if (!_config.TryGet(item, out tool))
                return;
            if (tool.ShoutSetFire != true)
                return;
            item.SetFlag(global::Item.Flag.OnFire, true);
            item.MarkDirty();
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            return action != "refill" ? null : OnItemRepair(player, item);
        }
        object OnItemSkinChange(int skin, Item item, RepairBench bench, BasePlayer player)
        {
            if (!IsMachiningToolItem(item))
                return null;
            Reply(player, "Can't change skin");
            return false;
        }

        object OnItemRepair(BasePlayer player, Item item)
        {
            Tool tool;
            if (!_config.TryGet(item, out tool))
                return null;
            if (tool.CanRepair)
                return null;
            Reply(player, "Can't repair");
            return false;
        }

        object CanRecycle(Recycler recycler, Item item)
        {
            Tool tool;
            if (!_config.TryGet(item, out tool))
                return null;
            return tool.CanRecycle ? (object) null : false;
        }

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (!player)
                return;
            var activeItem = player?.GetActiveItem();
            if (activeItem == null)
                return;

            Tool tool;
            if (!_config.TryGet(activeItem, out tool))
                return;

            if (tool.Transmutation.ShouldCook(item)) 
                Transmute(item);
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            ulong playerid = 0;
            OnDispenserGather(dispenser, player, item);
        }

        #endregion

        #region SkinBox

        private object SB_CanReskinItem(BasePlayer player, Item item) => SB_CanAcceptItem(player, item);
        private object SB_CanAcceptItem(BasePlayer player, Item item) => IsMachiningToolItem(item) ? GetMsg("Can't change skin", player.UserIDString) : null;

        #endregion

        #region Localization‌‌﻿‌‍‌​

        private void Reply(IPlayer player, string langKey, params object[] args)
        {
            var format = GetMsg(langKey, player.Id);
            player.Reply(args.Length != 0 ? string.Format(format, args) : format);
        }

        private void Reply(BasePlayer player, string langKey, params object[] args)
        {
            var reply = 0;
            var format = GetMsg(langKey, player.UserIDString);
            player.ChatMessage(args.Length != 0 ? string.Format(format, args) : format);
        }

        private string GetMsg(string langKey, string userId = null) => lang.GetMessage(langKey, this, userId);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Syntax"] = "Incorrect syntax! Use: {0} <NameOrID> item [item2] [item3] ...",
                ["No items"] = "The following items don't exist in the config, none of the items were given to the player \"{0}\":\n{1}",
                ["No player"] = "Player \"{0}\" could not be found",
                ["Not on server"] = "Player \"{0}\" is not on the server",
                ["Multiply players"] = "Found multiply players:\n{0}",
                ["Successful"] = "Successfully gave player \"{0}\" tools:\n{1}",
                ["Can't repair"] = "You can not repair this tool!",
                ["Can't change skin"] = "You can not change skin of this tool!"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Syntax"] = "Неверный синтаксис! Используйте: {0} <ИмяИлиID> предмет [предмет2] [предмет3] ...",
                ["No items"] = "Следующие предметы из списка не найдены, ни один предмет не выдан игроку \"{0}\":\n{1}",
                ["No player"] = "Игрок \"{0}\" не найден",
                ["Not on server"] = "Игрок \"{0}\" не находится на сервере",
                ["Multiply players"] = "Найдено несколько игроков:\n{0}",
                ["Successful"] = "Успешно выдали игроку \"{0}\" предметы:\n{1}",
                ["Can't repair"] = "Данный предмет не подлежит ремонту!",
                ["Can't change skin"] = "Вы не можете изменить скин этого инструмента!"
            }, this, "ru");
        }
        #endregion

        #region Command‌‌﻿‌‍‌​
        private void GiveToolsCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                Reply(player, "Syntax", _config.Command);
                return;
            }
            var targets =  covalence.Players.FindPlayers(args[0]).ToList();
            if (targets.Count == 0)
            {
                Reply(player, "No player", args[0]);
                return;
            }
            if (targets.Count > 1)
            {
                Reply(player, "Multiply players", string.Join("\n", targets.Select(p => $"{p.Name} [{p.Id}]")));
                return;
            }

            var iTarget = targets[0];
            var target = iTarget.Object as BasePlayer;
            if (!target || !target.IsAlive())
            {
                Reply(player, "Not on server", $"{iTarget.Name} [{iTarget.Id}]");
                return;
            }

            var configNames = args.Skip(1).ToList();
            var wrongNames = string.Join(", ",configNames.Where(x => !_config.ToolsByKey.ContainsKey(x)));
            if (!string.IsNullOrEmpty(wrongNames))
            {
                Reply(player, "No items", target, wrongNames);
                return;
            }

            foreach (var configName in configNames)
            {
                Tool tool = _config.ToolsByKey[configName];
                target.GiveItem(tool.Create());
            }
            Reply(player, "Successful", target, string.Join(", ", configNames));
        }
        #endregion

        #region API‌‌﻿‌‍‌​

        [HookMethod("IsMachiningToolItem")]
        bool IsMachiningToolItem(Item item)
        {
            if (item == null)
                return false;
            Tool tool;
            return _config.TryGet(item, out tool);
        }

        #endregion

        #region Helpers‌‌﻿‌‍‌​
        private void Transmute(Item item)
        {
            if (!_itemToCookable.ContainsKey(item.info))
            {
                PrintWarning($"[MachiningTools.Transmute] Unhandled item - {item.info.displayName.english}!\nPlease contact developer: https://vk.com/vlad_00003");
                return;
            }
            item.info = _itemToCookable[item.info];
        }
        #endregion
    }
}
///////////////////////////////////////////////////////////
