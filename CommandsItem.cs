using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Commands Item", "YaMang -w-", "2.2.71")]
    [Description("https://discord.gg/DTQuEE7neZ")]
    public class CommandsItem : RustPlugin
    {
        //2.1.7 로그 디테일 추가
        #region Field
        [PluginReference] Plugin ZoneManager, ImageLibrary;

        private string UsePermisson = "commandsitem.allow";

        private bool _d = false;

        private Dictionary<string, string> globalCooldown = new Dictionary<string, string>();
        private Dictionary<string, string> sharedCooldowns = new Dictionary<string, string>();

        #region Data
        private Dictionary<string, PlayerData> playerDatas = new Dictionary<string, PlayerData>();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, playerDatas);
        class PlayerData
        {
            public Dictionary<string, string> Cooldowns;
            public Dictionary<string, int> MaxUsed;
        }

        #endregion

        #endregion

        #region OxideHook

        void OnServerInitialized()
        {
            permission.RegisterPermission(UsePermisson, this);
            playerDatas = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerData>>(this.Name);
            cmd.AddChatCommand(_config.generalSettings.Commands, this, nameof(CommandsItemCMD));
            cmd.AddConsoleCommand(_config.generalSettings.Commands, this, nameof(CommandsItemConsole));
            ItemImageLoad();
            _d = _config.generalSettings.Debug;

            if (!_config.generalSettings.UseThrowCommands)
            {
                Unsubscribe(nameof(OnExplosiveThrown));
                Unsubscribe(nameof(OnExplosiveDropped));
            }

            if (!_config.generalSettings.UseHealingCommands) 
                Unsubscribe(nameof(OnHealingItemUse));

            if(!_config.generalSettings.UseActiveCommands)
                Unsubscribe(nameof(OnItemAction));
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null) return;
            if (string.IsNullOrEmpty(item.info.shortname)) return;

            var key = FindKey(item.info.shortname, item.skin);
            if (key == null) return;
            
            item.name = key;
        }
        void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon item) => OnExplosiveThrown(player, entity, item);
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon titem)
        {
            if (player == null || titem == null || entity == null) return;

            var item = titem.GetItem();

            if (item == null) return;
            if (string.IsNullOrEmpty(item.name)) return;
            if (!_config.itemSettings.ContainsKey(item.name)) return;
            

            var config = _config.itemSettings[item.name];
            if (item.skin != config.skin) return;

            #region Flags
            if (config.Flags != null)
            {
                if (config.Flags.Count != 0)
                {
                    bool find = false;
                    string flag = "";
                    for (int i = 0; i < config.Flags.Count; i++)
                    {
                        if (HasFlags(player, config.Flags[i]))
                        {
                            find = true;
                            flag = config.Flags[i];
                            break;
                        }
                    }

                    if (find)
                    {
                        if (_d) Puts($"Not Use - \n{player.displayName} {flag} [Explosive]");
                        API_TryGive(player, item.name);
                        entity.AdminKill();
                        LogWrite(player.UserIDString, $"{item.name} - {flag} [Explosive]");

                        return;
                    }
                }
            }

            #endregion

            #region MaxUsed
            if (config.MaxUsed != 0)
            {
                if (playerDatas.ContainsKey(player.UserIDString))
                {
                    if (playerDatas[player.UserIDString].MaxUsed.ContainsKey(item.name))
                    {
                        if (playerDatas[player.UserIDString].MaxUsed[item.name] >= config.MaxUsed)
                        {
                            Messages(player, Lang("MaxUsed", item.name, playerDatas[player.UserIDString].MaxUsed[item.name]));
                            var recovryItem = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                            player.GiveItem(recovryItem);
                            entity.AdminKill();
                            LogWrite(player.UserIDString, $"{item.name} - Maximum (Not Used) - {playerDatas[player.UserIDString].MaxUsed[item.name]} [Explosive]");

                            return;
                        }

                        playerDatas[player.UserIDString].MaxUsed[item.name]++;
                        if (_config.generalSettings.UseChatUsedMessageCount) Messages(player, Lang("Used", item.name, playerDatas[player.UserIDString].MaxUsed[item.name], config.MaxUsed));
                        LogWrite(player.UserIDString, $"{item.name} - Add Count (Used) - {playerDatas[player.UserIDString].MaxUsed[item.name]} [Explosive]");
                    }
                    else
                    {
                        playerDatas[player.UserIDString].MaxUsed.Add(item.name, 1);
                        LogWrite(player.UserIDString, $"{item.name} - Add Count (Used) - 1 [Explosive]");
                    }
                }
                else
                {
                    playerDatas.Add(player.UserIDString, new PlayerData()
                    {
                        MaxUsed = new Dictionary<string, int> { { item.name, 1 } },
                        Cooldowns = new Dictionary<string, string> { }

                    });
                    if (_config.generalSettings.UseChatUsedMessageCount) Messages(player, Lang("Used", item.name, 1, config.MaxUsed));
                    LogWrite(player.UserIDString, $"{item.name} - Add New Count (Used) - 1 [Explosive]");
                }
            }
            #endregion

            #region Cooldown
            var cooldown = Cooldowns(player, item.name, "Explosive");
            if (!cooldown)
            {
                var recovryItem = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                player.GiveItem(recovryItem);
                return;
            }
            #endregion

            LogWrite(player.UserIDString, $"THROW - {item.name} - Use [Explosive]");

            if (!string.IsNullOrEmpty(config.SoundEffect))
                SendEffect(player, config.SoundEffect);
            
            if (item.info.shortname == "supply.signal")
            {
                for (int i = 0; i < config.Commands.Count; i++)
                {
                    string cmds = config.Commands[i];

                    NextTick(() =>
                    {
                        if (cmds.StartsWith("pconsole"))
                        {
                            cmds = cmds.Replace("pconsole ", "");
                            player.SendConsoleCommand(cmds);
                            return;
                        }

                        ConsoleSystem.Run(ConsoleSystem.Option.Server, Format(player, cmds));
                    });
                }

                entity.Kill();
                
                
            }

            if (entity is TimedExplosive)
            {
                var et = entity as TimedExplosive;
                et.SetFuse(20f);

                var effect = new Effect(config.Effect, entity, 0, Vector3.zero, Vector3.forward);

                if (!string.IsNullOrEmpty(config.Effect))
                    EffectNetwork.Send(effect);


                timer.Once(3f, () =>
                {

                    for (int i = 0; i < config.Commands.Count; i++)
                    {
                        string cmds = config.Commands[i];
                        try
                        {
                            NextFrame(() =>
                            {
                                if(entity == null || entity.IsDestroyed)
                                {
                                    LogWrite(player.UserIDString, $"THROW - {item.name} - Not Used Location Error [Explosive]");
                                    PrintToChat(player, $"{item.name} is destroyed, the item is reissued.");
                                    var recovryItem = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                                    recovryItem.name = item.name;
                                    player.GiveItem(recovryItem);
                                    return;
                                }
                                ExecuteCommands(player, cmds, entity.transform.position);
                            });
                        }
                        catch
                        {
                            LogWrite(player.UserIDString, $"THROW - {item.name} - Not Used Location Error [Explosive]");
                            var recovryItem = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                            recovryItem.name = item.name;
                            player.GiveItem(recovryItem);
                            return;
                        }


                        timer.Once(2f, () => { effect.LeavePool(); et.Kill(); });
                    }

                    
                });

            }

            if (!string.IsNullOrEmpty(config.Message)) Messages(player, config.Message);

            SaveData();
        }
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action == "drop") return null;

            if (string.IsNullOrEmpty(item.name)) return null;
            if (!_config.itemSettings.ContainsKey(item.name)) return null;
            
            if (_config.itemSettings[item.name].shortname != item.info.shortname) return null;
            if (_config.itemSettings[item.name].skin != item.skin) return null;

            var config = _config.itemSettings[item.name] ?? null;
            if (config == null) return null;

            #region Flags
            if (config.Flags != null)
            {
                if (config.Flags.Count != 0)
                {
                    bool find = false;
                    string flag = "";
                    for (int i = 0; i < config.Flags.Count; i++)
                    {
                        if (HasFlags(player, config.Flags[i]))
                        {
                            find = true;
                            flag = config.Flags[i];
                            break;
                        }
                    }

                    if (find)
                    {
                        if (_d) Puts($"{player.displayName} {flag}");
                        LogWrite(player.UserIDString, $"{item.name} - {flag}  [Action]");
                        return true;
                    }
                }
            }

            #endregion

            #region MaxUsed
            if (config.MaxUsed != 0)
            {
                if (playerDatas.ContainsKey(player.UserIDString))
                {
                    if (playerDatas[player.UserIDString].MaxUsed.ContainsKey(item.name))
                    {
                        if (playerDatas[player.UserIDString].MaxUsed[item.name] >= config.MaxUsed)
                        {
                            Messages(player, Lang("MaxUsed", item.name, playerDatas[player.UserIDString].MaxUsed[item.name]));
                            var recovryItem = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                            player.GiveItem(recovryItem);
                            LogWrite(player.UserIDString, $"{item.name} - Maximum (Not Used) - {playerDatas[player.UserIDString].MaxUsed[item.name]} [Action]");

                            return true;
                        }

                        playerDatas[player.UserIDString].MaxUsed[item.name]++;
                        if (_config.generalSettings.UseChatUsedMessageCount) Messages(player, Lang("Used", item.name, playerDatas[player.UserIDString].MaxUsed[item.name], config.MaxUsed));
                        LogWrite(player.UserIDString, $"{item.name} - Add Count (Used) - {playerDatas[player.UserIDString].MaxUsed[item.name]} [Action]");
                    }
                    else
                    {
                        playerDatas[player.UserIDString].MaxUsed.Add(item.name, 1);
                        LogWrite(player.UserIDString, $"{item.name} - Add Count (Used) - 1 [Action]");
                    }
                }
                else
                {
                    playerDatas.Add(player.UserIDString, new PlayerData()
                    {
                        MaxUsed = new Dictionary<string, int> { { item.name, 1 } },
                        Cooldowns = new Dictionary<string, string> { }

                    });
                    if (_config.generalSettings.UseChatUsedMessageCount) Messages(player, Lang("Used", item.name, 1, config.MaxUsed));
                    LogWrite(player.UserIDString, $"{item.name} - Add New Count (Used) - 1 [Action]");
                }
            }
            #endregion

            #region Cooldown
            var cooldown = Cooldowns(player, item.name, "Action");
            if (!cooldown) return true;
            #endregion

            LogWrite(player.UserIDString, $"{item.name} - Use [Action]");

            if (!string.IsNullOrEmpty(config.SoundEffect))
                SendEffect(player, config.SoundEffect);
            if (!string.IsNullOrEmpty(config.Effect))
                SendEffect(player, config.Effect);

            for (int i = 0; i < config.Commands.Count; i++)
            {
                string cmds = config.Commands[i];

                NextTick(() =>
                {
                    ExecuteCommands(player, cmds);
                });
            }

            if (item.amount == 1)
            {
                item.Remove();
            }
            else
            {
                item.amount = item.amount - 1;
                player.SendNetworkUpdate();
            }


            if (!string.IsNullOrEmpty(config.Message)) Messages(player, config.Message);
            SaveData();
            return true;
        }
        private object OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            var item = tool.GetItem();
            if (string.IsNullOrEmpty(item.name)) return null;
            if (!_config.itemSettings.ContainsKey(item.name)) return null;

            if (_config.itemSettings[item.name].shortname != item.info.shortname) return null;
            if (_config.itemSettings[item.name].skin != item.skin) return null;

            var config = _config.itemSettings[item.name] ?? null;
            if (config == null) return null;

            #region Flags
            if (config.Flags != null)
            {
                if (config.Flags.Count != 0)
                {
                    bool find = false;
                    string flag = "";
                    for (int i = 0; i < config.Flags.Count; i++)
                    {
                        if (HasFlags(player, config.Flags[i]))
                        {
                            find = true;
                            flag = config.Flags[i];
                            break;
                        }
                    }

                    if (find)
                    {
                        if (_d) Puts($"{player.displayName} {flag}");
                        LogWrite(player.UserIDString, $"{item.name} - {flag}");
                        return true;
                    }
                }
            }

            #endregion

            #region MaxUsed
            if (config.MaxUsed != 0)
            {
                if (playerDatas.ContainsKey(player.UserIDString))
                {
                    if (playerDatas[player.UserIDString].MaxUsed.ContainsKey(item.name))
                    {
                        if (playerDatas[player.UserIDString].MaxUsed[item.name] >= config.MaxUsed)
                        {
                            Messages(player, Lang("MaxUsed", item.name, playerDatas[player.UserIDString].MaxUsed[item.name]));
                            var recovryItem = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                            player.GiveItem(recovryItem);
                            LogWrite(player.UserIDString, $"{item.name} - Maximum (Not Used) - {playerDatas[player.UserIDString].MaxUsed[item.name]}");

                            return true;
                        }

                        playerDatas[player.UserIDString].MaxUsed[item.name]++;
                        if (_config.generalSettings.UseChatUsedMessageCount) Messages(player, Lang("Used", item.name, playerDatas[player.UserIDString].MaxUsed[item.name], config.MaxUsed));
                        LogWrite(player.UserIDString, $"{item.name} - Add Count (Used) - {playerDatas[player.UserIDString].MaxUsed[item.name]}");
                    }
                    else
                    {
                        playerDatas[player.UserIDString].MaxUsed.Add(item.name, 1);
                        LogWrite(player.UserIDString, $"{item.name} - Add Count (Used) - 1");
                    }
                }
                else
                {
                    playerDatas.Add(player.UserIDString, new PlayerData()
                    {
                        MaxUsed = new Dictionary<string, int> { { item.name, 1 } },
                        Cooldowns = new Dictionary<string, string> { }

                    });
                    if (_config.generalSettings.UseChatUsedMessageCount) Messages(player, Lang("Used", item.name, 1, config.MaxUsed));
                    LogWrite(player.UserIDString, $"{item.name} - Add New Count (Used) - 1");
                }
            }
            #endregion

            #region Cooldown
            var cooldown = Cooldowns(player, item.name, "Healing");
            if (!cooldown) return true;
            #endregion

            LogWrite(player.UserIDString, $"{item.name} - Use [Healing]");

            if (!string.IsNullOrEmpty(config.SoundEffect))
                SendEffect(player, config.SoundEffect);
            if (!string.IsNullOrEmpty(config.Effect))
                SendEffect(player, config.Effect);

            for (int i = 0; i < config.Commands.Count; i++)
            {
                string cmds = config.Commands[i];

                NextTick(() =>
                {
                    ExecuteCommands(player, cmds);
                });
            }

            if (item.amount == 1)
            {
                item.Remove();
            }
            else
            {
                item.amount = item.amount - 1;
                player.SendNetworkUpdate();
            }


            if (!string.IsNullOrEmpty(config.Message)) Messages(player, config.Message);
            SaveData();
            return true;
        }
        #endregion
        

        #region Commands
        private void CommandsItemCMD(BasePlayer player, string Commands, string[] args)
        {
            if(!permission.UserHasPermission(player.UserIDString, UsePermisson))
            {
                Messages(player, Lang("NoPerm"));
                return;
            }

            if (args.Length == 0)
            {
                Messages(player, $"{Lang("UsageCI", _config.generalSettings.Commands)}");
                return;
            }


            if (args.Length == 1)
            {
                TryGive(player, args[0]);
                return;
            }

            TryGive(player, args[0], Convert.ToInt32(args[1]));
        }
        #region Scoll UI

        [ConsoleCommand("refreshplugininfo")]
        private void RefreshPluginInfo(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "Ci_CDLists");
            CommandsItemCooldownHud(player);
        }

        [ChatCommand("cicd")] //cicd
        private void CommandsItemCooldownHud(BasePlayer player)
        {
            if (!playerDatas.ContainsKey(player.UserIDString))
            {
                Messages(player, "No Cooldowns (no data)");
                return;
            }

            
            List<CommandsItems> ciList = new List<CommandsItems>();

            List<string> keyRemoved = new List<string>();

            foreach (var item in playerDatas[player.UserIDString].Cooldowns)
            {
                if (!_config.itemSettings.ContainsKey(item.Key))
                {
                    PrintError($"{item.Key} was not found item name config is problem ! (664)");
                    return;
                }

                if (_config.itemSettings[item.Key].GlobalCooldown)
                {
                    DateTime time = Convert.ToDateTime(globalCooldown[item.Key]);
                    DateTime now = Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                    TimeSpan remainingTime = time - now;

                    if (remainingTime.TotalSeconds >= 0)
                    {

                        ciList.Add(new CommandsItems
                        {
                            Title = $"{item.Key} - Global Cooldown: {remainingTime}",
                            shortname = _config.itemSettings[item.Key].shortname,
                            skinid = _config.itemSettings[item.Key].skin
                        });
                    }
                    else
                    {
                        globalCooldown.Remove(item.Key);
                    }
                }
                else
                {
                    DateTime time = Convert.ToDateTime(item.Value);
                    DateTime now = Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                    TimeSpan remainingTime = time - now;

                    if (remainingTime.TotalSeconds >= 0)
                    {
                        if (!_config.itemSettings.ContainsKey(item.Key))
                        {
                            PrintError($"{item.Key} was not found item name config is problem ! (699)");
                            return;
                        }

                        if (_config.itemSettings[item.Key].MaxUsed != 0)
                        {
                            ciList.Add(new CommandsItems
                            {
                                Title = $"{item.Key} - Private Cooldown {remainingTime}\nMaxUsed: {playerDatas[player.UserIDString].MaxUsed[item.Key]}/{_config.itemSettings[item.Key].MaxUsed}",
                                shortname = _config.itemSettings[item.Key].shortname,
                                skinid = _config.itemSettings[item.Key].skin
                            });
                        }
                        else
                        {
                            ciList.Add(new CommandsItems
                            {
                                Title = $"{item.Key} - Private Cooldown {remainingTime}\n",
                                shortname = _config.itemSettings[item.Key].shortname,
                                skinid = _config.itemSettings[item.Key].skin
                            });
                        }
                        
                    }
                    else
                    {
                        keyRemoved.Add(item.Key);
                    }
                }
                
                
            }

            if(keyRemoved.Count != 0)
            {
                for (int i = 0; i < keyRemoved.Count; i++)
                {
                    playerDatas[player.UserIDString].Cooldowns.Remove(keyRemoved[i]);
                }
            }

            if (playerDatas[player.UserIDString].Cooldowns.Count == 0)
            {
                Messages(player, "No Cooldowns");
                return;
            }

            int count = ciList.Count;
            int contentHeight = 70 * count;
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.1 0.1 0.1 1.0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Overlay", "Ci_CDLists");

            container.Add(new CuiElement
            {
                Name = "ScrollView",
                Parent = "Ci_CDLists",
                Components = {
                    new CuiScrollViewComponent {
                        MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                        Vertical = true,
                        Inertia = true,
                        Horizontal = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 24f,
                        ContentTransform = new CuiRectTransform { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 {-contentHeight}", OffsetMax = "0 0" },
                        VerticalScrollbar = new CuiScrollbar() { Size = 20f, AutoHide = false },
                    },
                    new CuiRawImageComponent
                    {
                        //Sprite = "assets/content/effects/crossbreed/fx gradient skewed.png",
                        Color = "0.05 0.05 0.05 0.5"
                    }
                }
            });

            for (int i = 0; i < count; i++)
            {
                CommandsItems plugin = ciList[i];
                int offset = 70 * i;

                container.Add(new CuiElement
                {
                    Name = "PluginPanel_" + i,
                    Parent = "ScrollView",
                    Components = {
                        new CuiRawImageComponent
                        {
                            Sprite = "",
                            Color = "0.2 0.2 0.2 1.0"
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 {-offset - 70}", OffsetMax = $"0 {-offset}" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "PluginImage_" + i,
                    Parent = "PluginPanel_" + i,
                    Components = {
                        new CuiRawImageComponent
                        {
                            Png = GetImage(plugin.shortname, plugin.skinid)
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.15 1" }
                    }
                });

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{plugin.Title}", FontSize = 14, Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = "0.17 0", AnchorMax = "1 1", OffsetMin = "-10 0", OffsetMax = "0 0" }
                }, "PluginPanel_" + i);
            }

            container.Add(new CuiButton
            {
                Button = { Color = "0.5 0.2 0.2 1.0", Close = "Ci_CDLists" },
                Text = { Text = "X", FontSize = 20, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.95 0.95", AnchorMax = "1.0 1.0" }
            }, "Ci_CDLists", "CloseButton");

            container.Add(new CuiButton
            {
                Button = { Color = "0.2 0.4 0.2 1.0", Command = $"refreshplugininfo" },
                Text = { Text = "Refresh", FontSize = 16, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.9 0.95", AnchorMax = "0.95 1.0" }
            }, "Ci_CDLists", "RefreshButton");

            CuiHelper.AddUi(player, container);
        }

        private class CommandsItems
        {
            public string Title { get; set; }
            public string shortname { get; set; }
            public ulong skinid { get; set; }
            
        }

        #endregion
        private void CommandsItemConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!arg.HasArgs()) return;
            if (player != null && !permission.UserHasPermission(player.UserIDString, UsePermisson))
            {
                SendReply(player, Lang("NoPerm"));
                return;
            }

            if (arg.Args.Length == 0)
            {
                SendReply(arg, "Syntax Error: player id ---> Ex]ci <player> <name> [amount]");
                return;
            }

            var target = FindPlayers(arg.Args[0]);

            if (target == null)
            {
                SendReply(arg, "Player is not found");
                return;
            }


            if (arg.Args.Length == 1)
            {
                SendReply(arg, $"Syntax Error: name ---> Ex]ci {arg.Args[1]} amount");
                return;
            }

            if (arg.Args.Length == 2)
            {
                TryGive(target, arg.Args[1]);
                return;
            }

            TryGive(target, arg.Args[1], Convert.ToInt32(arg.Args[2]));
        }
        #endregion

        #region Funtion
        private bool Cooldowns(BasePlayer player, string itemname, string hook)
        {
            if (!_config.itemSettings.ContainsKey(itemname))
            {
                PrintError($"'{itemname}' can't found !");
                return false;
            }

            var config = _config.itemSettings[itemname];
            if (string.IsNullOrEmpty(config.CooldownKey))
            {
                if (config.Cooldown != 0)
                {
                    if (_config.itemSettings[itemname].GlobalCooldown)
                    {
                        var expiringTime = DateTime.Now.AddSeconds(config.Cooldown).ToString("yyyy/MM/dd HH:mm:ss");
                        if (!globalCooldown.ContainsKey(itemname))
                        {
                            globalCooldown.Add(itemname, expiringTime);
                            LogWrite(player.UserIDString, $"{itemname} - Add Global Cooldown (Used) [{hook}]");
                            return true;
                        }
                        else
                        {
                            DateTime time = Convert.ToDateTime(globalCooldown[itemname]);
                            DateTime now = Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                            TimeSpan remainingTime = time - now;

                            if (remainingTime.TotalSeconds >= 0)
                            {
                                Messages(player, Lang("HasGlobalCooldown", itemname, remainingTime));
                                //var recovryItem = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                                //player.GiveItem(recovryItem);

                                LogWrite(player.UserIDString, $"{itemname} - Global Cooldown (Not Used) {remainingTime} [{hook}]");
                                return false;
                            }
                            else
                            {
                                globalCooldown.Remove(itemname);
                                globalCooldown.Add(itemname, expiringTime);
                                LogWrite(player.UserIDString, $"{itemname} - Add Global Cooldown (Used) - {expiringTime} [{hook}]");
                                return true;
                            }
                        }
                    }
                    else
                    {
                        var expiringTime = DateTime.Now.AddSeconds(config.Cooldown).ToString("yyyy/MM/dd HH:mm:ss");
                        if (playerDatas.ContainsKey(player.UserIDString))
                        {
                            if (playerDatas[player.UserIDString].Cooldowns.ContainsKey(itemname))
                            {
                                DateTime time = Convert.ToDateTime(playerDatas[player.UserIDString].Cooldowns[itemname]);
                                DateTime now = Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                                TimeSpan remainingTime = time - now;

                                if (remainingTime.TotalSeconds >= 0)
                                {
                                    Messages(player, Lang("HasCooldown", itemname, remainingTime));

                                    LogWrite(player.UserIDString, $"{itemname} - Cooldown (Not Used) {remainingTime}  [{hook}]");
                                    return false;
                                }
                                else
                                {
                                    playerDatas[player.UserIDString].Cooldowns.Remove(itemname);
                                    playerDatas[player.UserIDString].Cooldowns.Add(itemname, expiringTime);
                                    LogWrite(player.UserIDString, $"{itemname} - Add Cooldown (Used) - {expiringTime}  [{hook}]");
                                    return true;
                                }
                            }
                            else
                            {
                                playerDatas[player.UserIDString].Cooldowns.Add(itemname, expiringTime);
                                LogWrite(player.UserIDString, $"{itemname} - Add Cooldown (Used) - {expiringTime}  [{hook}]");
                                return true;
                            }

                        }
                        else
                        {
                            playerDatas.Add(player.UserIDString, new PlayerData()
                            {
                                Cooldowns = new Dictionary<string, string> { { itemname, expiringTime } },
                                MaxUsed = new Dictionary<string, int> { }

                            });
                            LogWrite(player.UserIDString, $"{itemname} - Add Cooldown (Used) - {expiringTime}  [{hook}]");
                            return true;
                        }
                    }
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if(_config.itemsShardCooldowns == null)
                {
                    PrintWarning($"'{itemname}' has shared config null");
                    return false;
                }

                if (!_config.itemsShardCooldowns.Cooldowns.ContainsKey(config.CooldownKey))
                {
                    PrintWarning($"'{itemname}' has a shared cooldown set, but '{config.CooldownKey}' cannot be found.  [{hook}]");
                    return true;
                }
                else
                {
                    var shared = _config.itemsShardCooldowns.Cooldowns[config.CooldownKey];

                    var expiringTime = DateTime.Now.AddSeconds(shared).ToString("yyyy/MM/dd HH:mm:ss");
                    if (!sharedCooldowns.ContainsKey(config.CooldownKey))
                    {
                        sharedCooldowns.Add(config.CooldownKey, expiringTime);
                        LogWrite(player.UserIDString, $"{itemname} - Add Shared Cooldown (Used) [{hook}]");
                        return true;
                    }
                    else
                    {
                        DateTime time = Convert.ToDateTime(sharedCooldowns[config.CooldownKey]);
                        DateTime now = Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                        TimeSpan remainingTime = time - now;

                        if (remainingTime.TotalSeconds >= 0)
                        {
                            Messages(player, Lang("HasSharedCooldown", itemname, remainingTime));
                            LogWrite(player.UserIDString, $"{itemname} - Shared Cooldown (Not Used) {remainingTime} [{hook}]");
                            return false;
                        }
                        else
                        {
                            sharedCooldowns.Remove(config.CooldownKey);
                            sharedCooldowns.Add(config.CooldownKey, expiringTime);
                            LogWrite(player.UserIDString, $"{itemname} - Add Shared Cooldown (Used) - {expiringTime} [{hook}]");
                            return true;
                        }
                    }

                }


            }
        }
        private void ExecuteCommands(BasePlayer player, string cmds, Vector3 pos = new Vector3())
        {
            if (cmds.StartsWith("pconsole"))
            {
                cmds = cmds.Replace("pconsole ", "");

                timer.Once(1f, () => {
                    NextTick(() =>
                    {
                        player.SendConsoleCommand(Format(player, cmds, pos));
                    });
                });
                return;
            }

            if (cmds.StartsWith("bpconsole"))
            {
                NextTick(() =>
                {
                    if (player.IsAdmin)
                    {
                        cmds = cmds.Replace("bpconsole ", "");
                        player.SendConsoleCommand(cmds);
                        return;
                    }
                    cmds = cmds.Replace("bpconsole ", "");
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendConsoleCommand(Format(player, cmds, pos));

                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                });
                return;
            }

            if (cmds.StartsWith("timer_"))
            {
                NextTick(() =>
                {
                    string numberString = Regex.Match(cmds, @"\d+").Value;
                    int number = int.Parse(numberString);

                    string result = Regex.Replace(cmds, @"timer_\d+\s*", "");

                    timer.Once(number, () =>
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, Format(player, result));
                    });
                });

            }
            else
            {
                NextTick(() =>
                {
                    if (pos.x == 0 && pos.y == 0 && pos.z == 0)
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, Format(player, cmds));
                    }
                    else
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, Format(player, cmds, pos));
                    }
                });
            }
        }
        private enum FlagType { IsBuildingBlock, IsSwimming, InSafeZone, IsBleeding, IsPlayerInZone, IsPlayerInGrid, HasTCAuth }
        private bool HasFlags(BasePlayer player, string flag)
        {
            if(ZoneManager != null)
            {
                //IsPlayerInZone
                if(flag.StartsWith("IsPlayerInZone"))
                {
                    string[] args = flag.Split('-');
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args.Length == 0)
                        {
                            if (ZoneManager == null)
                            {
                                PrintWarning("ZoneManager plugin could not be found. Removing flags.");
                                break;
                            }
                            else
                                continue;
                        }

                        if (IsPlayerInZone(args[i], player))
                        {
                            Messages(player, $"You cannot use this item in this Zone");
                            return true;
                        }

                    }
                }

                
            }
            
            if(flag.StartsWith("IsPlayerInGrid"))
            {
                string[] args = flag.Split('-');
                for (int i = 0; i < args.Length; i++)
                {
                    if (args.Length == 0) continue;

                    if (IsPlayerInGrid(player, args[i]))
                    {
                        Messages(player, $"<color=red>You cannot use this item in this {args[i]} Grid</color>");
                        return true;
                    }
                }
            }

            if(FlagType.IsBuildingBlock.ToString() == flag)
            {
                if (player.IsBuildingBlocked())
                {
                    Messages(player, "<color=red>Item cannot be used while you are Building Blocked.</color>");
                    return true;
                }
            }

            if(FlagType.HasTCAuth.ToString() == flag)
            {
                if (!player.IsBuildingAuthed())
                {
                    Messages(player, $"<color=red>Item can only be used while having Building Privilige</color>");
                    return true;
                }
            }

            if (FlagType.IsSwimming.ToString() == flag)
            {
                if (player.IsSwimming())
                {
                    Messages(player, "<color=red>Item cannot be used while you are are swimming.</color>");
                    return true;
                }
            }

            if (FlagType.InSafeZone.ToString() == flag)
            {
                if (player.InSafeZone())
                {
                    Messages(player, "<color=red>Item cannot be used while you are in a Safe Zone.</color>");
                    return true;
                }
            }

            if (FlagType.IsBleeding.ToString() == flag)
            {
                if (player.metabolism.bleeding.value > 0)
                {
                    Messages(player, "<color=red>Item cannot be used while you are bleeding.</color>");
                    return true;
                }
            }

            return false;
        }
        private bool IsPlayerInZone(string zoneID, BasePlayer player) => (bool)ZoneManager.Call("IsPlayerInZone", zoneID, player);
        private bool IsPlayerInGrid(BasePlayer player, string grid)
        {
            if(getGrid(player.transform.position) == grid)
                return true;
            else
                return false;
        }
        private string getGrid(Vector3 pos)
        {
            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(((int)letter) + x);
            return $"{letter}{z}";
        }
        private void ItemImageLoad()
        {
            List<KeyValuePair<string, ulong>> itemIcons = new List<KeyValuePair<string, ulong>>();
            var cfg = _config.itemSettings;

            foreach (var item in cfg)
            {
                itemIcons.Add(new KeyValuePair<string, ulong>(item.Value.shortname, item.Value.skin));
            }

            ImageLibrary.Call("LoadImageList", Title, itemIcons.Select(x => new KeyValuePair<string, ulong>(x.Key, x.Value)).ToList());
        }
        private string TryForImage(string shortname, ulong skin = 0)
        {
            if (!ImageLibrary) return "https://i.imgur.com/yxESUQJ.png";
            if (shortname.Contains("http") || shortname.Contains("www")) return shortname;
            return GetImage(shortname, skin);
        }
        private string GetImage(string shortname, ulong skin = 0, bool returnUrl = false) => (string)ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        private string FindKey(string shortname, ulong skin)
        {
            var find = _config.itemSettings.FirstOrDefault(x => x.Value.shortname == shortname && x.Value.skin == skin);
            if (string.IsNullOrEmpty(find.Key) || find.Key == null) return null;

            return find.Key;
        }
        private static BasePlayer FindPlayers(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.userID.ToString() == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            return null;
        }
        private void Messages(BasePlayer player, string text) => player.SendConsoleCommand("chat.add", 2, _config.generalSettings.SteamID, $"{_config.generalSettings.Prefix} {text}");
        private bool TryGive(BasePlayer player, string name, int amount = 1)
        {
            if (player.inventory.containerMain.IsFull())
            {
                Messages(player, Lang("InvFull"));
                LogWrite(player.UserIDString, Lang("InvFull"));
                return false;
            }

            var item = ItemManager.CreateByName(_config.itemSettings[name].shortname, amount, _config.itemSettings[name].skin);

            if(item == null)
            {
                PrintError($"The shortname could not be found for this {name}!");
                return false;
            }

            item.name = name;
            player.GiveItem(item);

            Messages(player, Lang("Received", name));
            LogWrite(player.UserIDString, $"{name} Received");

            return true;
        }
        private void SendEffect(BasePlayer player, string sound)
        {
            var effect = new Effect(sound, player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect, player.net.connection);
        }
        private string Format(BasePlayer player, string msg, Vector3 entity = new Vector3())
        {
            msg = msg
                .Replace("$player.id", player.UserIDString)
                .Replace("$player.name", player.displayName)
                .Replace("$player.x", player.transform.position.x.ToString())
                .Replace("$player.y", player.transform.position.y.ToString())
                .Replace("$player.z", player.transform.position.z.ToString())
                .Replace("$entity.x", entity.x.ToString())
                .Replace("$entity.y", entity.y.ToString())
                .Replace("$entity.z", entity.z.ToString())
                .Replace("$entity.vector3", entity.z.ToString());

            return msg;
        }
        private void LogWrite(string userid, string text) => LogToFile($"{userid}", $"[{DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss tt")}] - {text}", this, false);
        #endregion

        #region Config        
        private ConfigData _config;

        //Helping WhiteThunder Thanks
        private class CaseInsensitiveDictionary<TValue> : Dictionary<string, TValue>
        {
            public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase) {}
        }
        //

        private class ConfigData
        {
            [JsonProperty(PropertyName = "General Settings")] public GeneralSettings generalSettings { get; set; }
            [JsonProperty(PropertyName = "Item Settings (custom name)")] public CaseInsensitiveDictionary<ItemSettings> itemSettings { get; set; }
            [JsonProperty(PropertyName = "items Shared Cooldowns")] public ItemsSharedCooldowns itemsShardCooldowns { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        public class GeneralSettings
        {
            [JsonProperty(PropertyName = "Prefix", Order = 1)] public string Prefix { get; set; }
            [JsonProperty(PropertyName = "SteamID", Order = 2)] public ulong SteamID { get; set; }
            [JsonProperty(PropertyName = "Commands", Order = 3)] public string Commands { get; set; }
            [JsonProperty(PropertyName = "Use Active Commands", Order = 4)] public bool UseActiveCommands { get; set; }
            [JsonProperty(PropertyName = "Use Throw Commands", Order = 5)] public bool UseThrowCommands { get; set; }
            [JsonProperty(PropertyName = "Use Healing Commands", Order = 6)] public bool UseHealingCommands { get; set; }
            [JsonProperty(PropertyName = "Use Message on used count", Order = 10)] public bool UseChatUsedMessageCount { get; set; }
            [JsonProperty(PropertyName = "Use Map Wipe Data Clear", Order = 11)] public bool UseMapWipeDataCleaer { get; set; }
            [JsonProperty(PropertyName = "Debug", Order = 20)] public bool Debug { get; set; }
        }

        public class ItemSettings
        {
            [JsonProperty(PropertyName = "Item short name")] public string shortname { get; set; }
            [JsonProperty(PropertyName = "Item skin")] public ulong skin { get; set; }
            [JsonProperty(PropertyName = "Commands")] public List<string> Commands { get; set; }
            [JsonProperty(PropertyName = "Message on use (leave blank for no message)")] public string Message { get; set; }
            [JsonProperty(PropertyName = "Global Cooldown [true | false]")] public bool GlobalCooldown { get; set; }
            [JsonProperty(PropertyName = "Shared Cooldown")] public string CooldownKey { get; set; }
            [JsonProperty(PropertyName = "Cooldown (0 for disable - ※second※)")] public int Cooldown { get; set; }
            [JsonProperty(PropertyName = "MaxUsed (0 for unlimited)")] public int MaxUsed { get; set; }
            [JsonProperty(PropertyName = "Sound Effect (blink notting)")] public string SoundEffect { get; set; }
            [JsonProperty(PropertyName = "Effect (blink notting)")] public string Effect { get; set; }
            [JsonProperty(PropertyName = "Flags")] public List<string> Flags { get; set; }
        }

        public class ItemsSharedCooldowns
        {
            [JsonProperty(PropertyName = "Shard Name | Time (sec)")] public Dictionary<string, int> Cooldowns { get; set; }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) throw new Exception();

                if (_config.Version < Version)
                    UpdateConfigValues();

            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}_BackupError.json");
                PrintError("An error occurred in the config\nFind the CommandsItem_BackupError file.");
            }

            SaveConfig();
        }
        protected override void LoadDefaultConfig() => _config = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                generalSettings = new GeneralSettings
                {
                    Prefix = "<color=#5892bf>[Commands-Item]</color>\n",
                    SteamID = 0,
                    Commands = "ci",
                    UseMapWipeDataCleaer = false,
                    UseActiveCommands = true,
                    UseThrowCommands = false,
                    UseHealingCommands = false,
                    UseChatUsedMessageCount = false,
                    Debug = false
                },
                itemSettings = new CaseInsensitiveDictionary<ItemSettings>
                {
                    {
                        "Supply Space Fanny Drop", new ItemSettings
                        {
                            shortname = "grenade.smoke",
                            skin = 2867732572,
                            Commands = new List<string>
                            {
                                "ad.dropspace $player.id"
                            },
                            Message = "Warning Space Drop !!",
                            Cooldown = 0,
                            MaxUsed = 0,
                            Effect = "assets/bundled/prefabs/fx/smoke_signal_full.prefab",
                            SoundEffect = "",
                            Flags = new List<string>
                            {
                                ""
                            }
                        }
                    },
                    {
                        "Teleport Granade", new ItemSettings
                        {
                            shortname = "grenade.smoke",
                            skin = 2814909703,
                            Commands = new List<string>
                            {
                                "teleport.topos $player.id $entity.x $entity.y $entity.z"
                            },
                            Message = "Teleporting",
                            Cooldown = 0,
                            MaxUsed = 0,
                            Effect = "",
                            SoundEffect = "",
                            Flags = new List<string>
                            {
                                ""
                            }
                        }
                    },
                    {
                        "Unwarp Space Drop", new ItemSettings
                        {
                            shortname = "xmas.present.medium",
                            skin = 2814909703,
                            Commands = new List<string>
                            {
                                "ad.dropspace $player.id"
                            },
                            Message = "Warning Space Drop !!",
                            Cooldown = 0,
                            MaxUsed = 0,
                            Effect = "",
                            SoundEffect = "",
                            Flags = new List<string>
                            {
                                ""
                            }
                        }
                    }
                },
                itemsShardCooldowns = new ItemsSharedCooldowns
                {
                    Cooldowns = new Dictionary<string, int>()
                    {
                        {
                            "testshared",
                            60
                        }
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            //Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}_{_config.Version}.json");
            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {//야스
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NotExist", "<color=#d0d0d0>Item (<color=orange>{0}</color>) could not be found. Please use a valid SkinID!</color>\n<color=#5892bf>Usage: /{1} <skinid></color>" },
                { "UsageCI", "<color=red>Usage Sta: /{0} <Name> [Amount]</color>" },
                { "InvFull", "<color=#d0d0d0>Inventory is full. Clear some space and try again!</color>" },
                { "Received", "<color=yellow>[{0}] Was added to your inventory.</color>" },
                { "NoPerm", "<color=#d0d0d0>You dont have permission!</color>" },
                { "HasGlobalCooldown", "G <color=yellow>{0}</color> <color=red>can be used after {1}</color>" },
                { "HasSharedCooldown", "S <color=yellow>{0}</color> <color=red>can be used after {1}</color>" },
                { "HasCooldown", "C <color=yellow>{0}</color> <color=red>can be used after {1}</color>" },
                { "MaxUsed", "<color=yellow>{0}</color> <color=red>Max Used ({1})</color>" },
                { "Used", "<color=yellow>{0}</color> <color=red>You can use ({1}/{2})</color>" }

            }, this);
        }

        private string Lang(string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this), args);
        }

        #endregion

        #region API
        private bool API_IsCItem(string itemname)
        {
            if (_config.itemSettings.ContainsKey(itemname)) return true;
            else return false;
        }
        private bool API_IsItem(string shortname, ulong skin)
        {
            var find = FindKey(shortname, skin);
            if (find == null) return false;
            return true;
        }
        private bool API_IsCItem(Item item)
        {
            if (_config.itemSettings.ContainsKey(item.name)) return true;
            else return false;
        }
        private bool API_TryGive(string id, string name, int amount = 1)
        {
            var target = FindPlayers(id);
            if (target == null) return false;

            TryGive(target, name, amount);
            return true;
        }
        private bool API_TryGive(string id, string shortname, ulong skin, int amount = 1)
        {
            var target = FindPlayers(id);
            if (target == null) return false;

            var find = FindKey(shortname, skin);
            if (find == null) return false;
            TryGive(target, find, amount);
            return true;
        }
        private bool API_TryGive(BasePlayer player, string name, int amount = 1) => TryGive(player, name, amount);
        private bool API_TryGive(BasePlayer player, string shortname, ulong skin, int amount = 1)
        {
            var find = FindKey(shortname, skin);
            if (find == null) return false;
            TryGive(player, find, amount);
            return true;
        }
        #endregion
    }
}
