using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildProtection", "nn author, Fixed by North", "1.0.1")]
    internal class BuildProtection : RustPlugin
    {
        #region Static

        [PluginReference] private Plugin ImageLibrary;
        private readonly Dictionary<BasePlayer, BuildingPrivlidge> TCList = new Dictionary<BasePlayer, BuildingPrivlidge>();
        private Configuration _config;

        #endregion

        #region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "The list of privileges (The name of the permission and the maximum percentage of protection)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<string, int> PermList = new Dictionary<string, int>
            {
                ["buildprotection.default"] = 30,
                ["buildprotection.vip"] = 35,
                ["buildprotection.medium"] = 40,
                ["buildprotection.harm"] = 45,
                ["buildprotection.diamond"] = 100
            };

            [JsonProperty(PropertyName = "The cost of 100% home protection", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<ItemToProtect> PriceList = new List<ItemToProtect>
            {
                new ItemToProtect
                {
                    ShortName = "sulfur",
                    Amount = 45000,
                    skinID = 0,
                    displayName = "SULFUR",
                    Image = ""
                },
                new ItemToProtect
                {
                    ShortName = "metal.refined",
                    Amount = 3000,
                    skinID = 0,
                    displayName = "HQM",
                    Image = ""
                },
                new ItemToProtect
                {
                    ShortName = "metal.fragments",
                    Amount = 80000,
                    skinID = 0,
                    displayName = "METAL",
                    Image = ""
                },
                new ItemToProtect
                {
                    ShortName = "glue",
                    Amount = 250,
                    skinID = 0,
                    displayName = "DIAMOND",
                    Image = "https://i.imgur.com/2XQpFeh.png"
                }
            };

            [JsonProperty(PropertyName = "Setting the time (in hours) and multiplying the coefficients affecting the cost", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<int, float> TimeList = new Dictionary<int, float>
            {
                [5] = 1,
                [6] = 1.2f,
                [7] = 1.4f,
                [8] = 1.5f,
                [10] = 1.6f,
                [12] = 1.7f,
                [16] = 1.8f
            };

            internal class ItemToProtect
            {
                [JsonProperty("Amount")] public int Amount;
                [JsonProperty("displayName in UI")] public string displayName;

                [JsonProperty("Picture to display in the UI (leave blank if the standard item)")]
                public string Image;

                [JsonProperty("SHORTNAME")] public string ShortName;
                [JsonProperty("SkinID")] public ulong skinID;
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

        private int GetMaxProtection(string userID)
        {
            var protect = 0;
            foreach (var check in _config.PermList)
                if (permission.UserHasPermission(userID, check.Key))
                    protect = Math.Max(protect, check.Value);
            return protect;
        }

        private void OnServerInitialized()
        {
            LoadData();
            foreach (var check in _config.PermList) permission.RegisterPermission(check.Key, this);

            for (var index = 0; index < _config.PriceList.Count; index++)
            {
                var check = _config.PriceList[index];
                if (!string.IsNullOrEmpty(check.Image))
                    ImageLibrary.Call("AddImage", check.Image, check.Image);
            }

            PrintError("|-----------------------------------|");
            PrintWarning($"|  Plugin {Title} v{Version} is loaded  |");
            PrintWarning("|          fixed by North =)      |");
            PrintError("|-----------------------------------|");
        }

        private void Unload()
        {
            SaveData();
            foreach (var check in BasePlayer.activePlayerList) CuiHelper.DestroyUi(check, "Panel_5842");
        }

        #endregion

        #region Function

        private void OnEntityTakeDamage(BuildingBlock entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) return;
            if (!_data.ContainsKey(priv.net.ID.Value)) return;
            var data = _data[priv.net.ID.Value];
            if (!data.IsProtect) return;
            if ((data.FinishProtection - DateTime.Now).TotalSeconds < 0)
            {
                data.IsProtect = false;
                return;
            }

            var protect = 1 - (float)data.Protection / 100;
            info.damageTypes.ScaleAll(protect);
            if (info.InitiatorPlayer != null)
            {
            }
        }

        private void OnEntityTakeDamage(DecayEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) return;
            if (!_data.ContainsKey(priv.net.ID.Value)) return;
            var data = _data[priv.net.ID.Value];
            if (!data.IsProtect) return;
            if ((data.FinishProtection - DateTime.Now).TotalSeconds < 0)
            {
                data.IsProtect = false;
                return;
            }

            var protect = 1 - (float)data.Protection / 100;
            info.damageTypes.ScaleAll(protect);
            if (info.InitiatorPlayer != null)
            {
            }
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.HitEntity == null) return;
            var entity = info.HitEntity;
            if (entity as BuildingPrivlidge)
            {
                if (TCList.ContainsKey(player))
                    TCList[player] = entity as BuildingPrivlidge;
                else
                    TCList.Add(player, entity as BuildingPrivlidge);

                if (_data.ContainsKey(entity.net.ID.Value) && _data[entity.net.ID.Value].IsProtect)
                {
                    var data = _data[entity.net.ID.Value];
                    if ((data.FinishProtection - DateTime.Now).TotalSeconds < 0)
                    {
                        data.IsProtect = false;
                        ShowUI(player);
                        return;
                    }

                    ShowProtectUI(player, _data[entity.net.ID.Value]);
                }
                else
                {
                    ShowUI(player);
                }
            }
        }

        #endregion

        #region UI

        private void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
        {
            var num1 = 0;
            if (iAmount == 0) return;

            var list = Pool.GetList<Item>();

            foreach (var item in itemList)
            {
                if (item.info.shortname != shortname ||
                    (skinId != 0 && item.skin != skinId)) continue;

                var num2 = iAmount - num1;
                if (num2 <= 0) continue;
                if (item.amount > num2)
                {
                    item.MarkDirty();
                    item.amount -= num2;
                    num1 += num2;
                    break;
                }

                if (item.amount <= num2)
                {
                    num1 += item.amount;
                    list.Add(item);
                }

                if (num1 == iAmount)
                    break;
            }

            foreach (var obj in list)
                obj.RemoveFromContainer();

            Pool.FreeList(ref list);
        }

        [ConsoleCommand("UI_BUILDPROTECTION")]
        private void cmdConsoleUI_BUILDPROTECTION(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var procent = int.Parse(arg.Args[1]);
            var time = int.Parse(arg.Args[2]);
            switch (arg.Args[0])
            {
                case "PROTECT":
                    {
                        ShowUI(player, procent, time);
                        break;
                    }
                case "TIME":
                    {
                        ShowUI(player, procent, time);
                        break;
                    }
                case "SUCCESS":
                    {
                        var tc = TCList[player];

                        List<Item> allItems = new List<Item>();
                        allItems.AddRange(player.inventory.containerMain.itemList);
                        allItems.AddRange(player.inventory.containerBelt.itemList);
                        allItems.AddRange(player.inventory.containerWear.itemList);

                        foreach (var check in _config.PriceList)
                            Take(allItems, check.ShortName, check.skinID, (int)(check.Amount * procent / 100 * _config.TimeList[time]));

                        if (_data.ContainsKey(tc.net.ID.Value))
                        {
                            var data = _data[tc.net.ID.Value];
                            data.Protection = procent;
                            data.FinishProtection = DateTime.Now.AddHours(time);
                            data.IsProtect = true;
                        }
                        else
                        {
                            _data.Add(tc.net.ID.Value, new Data
                            {
                                Protection = procent,
                                FinishProtection = DateTime.Now.AddHours(time),
                                IsProtect = true
                            });
                        }

                        player.ChatMessage(GetMessage("MSG_SUCCEFULL", player.UserIDString, procent, time));
                        break;
                    }
            }
        }



        private void ShowProtectUI(BasePlayer player, Data data)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.282353 0.282353 0.282353 1" },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-205.77 -280.762",
                    OffsetMax = "204.835 271.887"
                }
            }, "Overlay", "Panel_5842");
            container.Add(new CuiElement
            {
                Name = "Label_1743",
                Parent = "Panel_5842",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMessage("UI_HOMEISPROTECT", player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 30,
                        Align = TextAnchor.MiddleCenter, Color = "0 0.5686275 1 1"
                    },
                    new CuiOutlineComponent { Color = "0 0 0 0.2117647", Distance = "1 -1" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-161.364 134.819",
                        OffsetMax = "161.364 203.181"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.4235294 0.4235294 0.4235294 1" },
                Text =
                {
                    Text = GetMessage("UI_STATUS_HEADER", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 28,
                    Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-206.407 219.233",
                    OffsetMax = "205.133 276.327"
                }
            }, "Panel_5842", "Button_9962");

            container.Add(new CuiButton
            {
                Button = { Color = "0.9254903 0.2901961 0.2901961 1", Close = "Panel_5842" },
                Text =
                {
                    Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 43, Align = TextAnchor.MiddleCenter,
                    Color = "0.5764706 0.1686275 0.1686275 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "148.77 -28.547",
                    OffsetMax = "205.77 28.453"
                }
            }, "Button_9962", "Button_8465");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0.5686275 1 1", Close = "Panel_5842" },
                Text =
                {
                    Text = GetMessage("UI_STATUS_COMPLETE", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 20,
                    Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-176.654 -265.209",
                    OffsetMax = "179.926 -223.938"
                }
            }, "Panel_5842", "Button_7334");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1960784 0.5686275 0.7372549 1" },
                Text =
                {
                    Text = $"{data.Protection}%", Font = "robotocondensed-bold.ttf", FontSize = 40, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-161.363 17.265",
                    OffsetMax = "-17.071 79.301"
                }
            }, "Panel_5842", "Button_4254");
            Outline(ref container, "Button_4254", "0 0 0 1", "1.5");

            container.Add(new CuiElement
            {
                Name = "Label_545",
                Parent = "Button_4254",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMessage("UI_PROTECTION_STATUS", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 16,
                        Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-72.144 34.509",
                        OffsetMax = "72.146 57.88"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.1960784 0.5686275 0.7372549 1" },
                Text =
                {
                    Text = $"{$"{(data.FinishProtection - DateTime.Now).TotalHours:F2}"}H", Font = "robotocondensed-bold.ttf", FontSize = 40, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "17.071 17.265",
                    OffsetMax = "161.363 79.301"
                }
            }, "Panel_5842", "Button_4254 (1)");

            container.Add(new CuiElement
            {
                Name = "Label_545",
                Parent = "Button_4254 (1)",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMessage("UI_TIME_STATUS", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 16,
                        Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-72.144 34.509",
                        OffsetMax = "72.146 57.88"
                    }
                }
            });
            Outline(ref container, "Button_4254 (1)", "0 0 0 1", "1.5");
            CuiHelper.DestroyUi(player, "Panel_5842");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUI(BasePlayer player, int procent = 0, int time = 0)
        {
            if (time < _config.TimeList.First().Key || time > _config.TimeList.Last().Key) time = _config.TimeList.First().Key;
            int nextKey = _config.TimeList.Keys.FirstOrDefault(k => k > time);
            int previousKey = _config.TimeList.Keys.LastOrDefault(k => k < time);

            var max = GetMaxProtection(player.UserIDString);
            if (procent <= 0 || procent > max) procent = max;
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.282353 0.282353 0.282353 1" },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-205.77 -280.762",
                    OffsetMax = "204.835 271.887"
                }
            }, "Overlay", "Panel_5842");

            container.Add(new CuiButton
            {
                Button = { Color = "0.4235294 0.4235294 0.4235294 1" },
                Text =
                {
                    Text = GetMessage("UI_SETUP_HEADER", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 28,
                    Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-206.407 219.233",
                    OffsetMax = "205.133 276.327"
                }
            }, "Panel_5842", "Button_9962");

            container.Add(new CuiButton
            {
                Button = { Color = "0.9254903 0.2901961 0.2901961 1", Close = "Panel_5842" },
                Text =
                {
                    Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 43, Align = TextAnchor.MiddleCenter,
                    Color = "0.5764706 0.1686275 0.1686275 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "148.77 -28.547",
                    OffsetMax = "205.77 28.453"
                }
            }, "Button_9962", "Button_8465");

            container.Add(new CuiButton
            {
                Button = { Color = "0.3803922 0.4039216 0.4862745 1" },
                Text =
                {
                    Text = $"{procent}%", Font = "robotocondensed-bold.ttf", FontSize = 28, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-178.026 140.498",
                    OffsetMax = "-48.174 190.703"
                }
            }, "Panel_5842", "protect");

            container.Add(new CuiElement
            {
                Name = "Label_3418",
                Parent = "protect",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMessage("UI_PROTECTION_STATUS", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 15,
                        Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.924 25.102",
                        OffsetMax = "64.926 49.999"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.2392157 0.3372549 0.6705883 1", Command = $"UI_BUILDPROTECTION PROTECT {procent - 5} {time}" },
                Text =
                {
                    Text = "◄", Font = "robotocondensed-bold.ttf", FontSize = 30, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                    { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.9 -19", OffsetMax = "-24.9 19" }
            }, "protect", "Button_1555");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1647059 0.5333334 0.7254902 1", Command = $"UI_BUILDPROTECTION PROTECT {procent + 5} {time}" },
                Text =
                {
                    Text = "►", Font = "robotocondensed-bold.ttf", FontSize = 30, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                    { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "24.9 -19", OffsetMax = "62.9 19" }
            }, "protect", "Button_1555 (1)");

            container.Add(new CuiButton
            {
                Button = { Color = "0.3803922 0.4039216 0.4862745 1" },
                Text =
                {
                    Text = $"{time}ч", Font = "robotocondensed-bold.ttf", FontSize = 28, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "50.074 140.497",
                    OffsetMax = "179.926 190.703"
                }
            }, "Panel_5842", "timer");

            container.Add(new CuiElement
            {
                Name = "Label_3418",
                Parent = "timer",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMessage("UI_TIME_STATUS", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 15,
                        Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.924 25.102",
                        OffsetMax = "64.926 49.999"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.2392157 0.3372549 0.6705883 1", Command = $"UI_BUILDPROTECTION TIME {procent} {previousKey}" },
                Text =
                {
                    Text = "◄", Font = "robotocondensed-bold.ttf", FontSize = 30, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                    { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.9 -19", OffsetMax = "-24.9 19" }
            }, "timer", "Button_1555");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1647059 0.5333334 0.7254902 1", Command = $"UI_BUILDPROTECTION TIME {procent} {nextKey}" },
                Text =
                {
                    Text = "►", Font = "robotocondensed-bold.ttf", FontSize = 30, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                    { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "24.9 -19", OffsetMax = "62.9 19" }
            }, "timer", "Button_1555 (1)");

            container.Add(new CuiElement
            {
                Name = "Label_7832",
                Parent = "Panel_5842",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMessage("UI_SETUP_PRICE", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14,
                        Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                    },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-132.191 97.783",
                        OffsetMax = "132.191 131.417"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_8774",
                Parent = "Panel_5842",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMessage("UI_SETUP_NEED", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 15,
                        Align = TextAnchor.MiddleCenter, Color = "0.572549 0.572549 0.572549 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "77.173 70.591",
                        OffsetMax = "179.928 97.782"
                    }
                }
            });
            var posy = 18.214;
            var height = 61.586 - posy;
            var i = 0;
            foreach (var check in _config.PriceList)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.4235294 0.4235294 0.4235294 1" },
                    Text =
                    {
                        Text = "  ", Font = "robotocondensed-regular.ttf", FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = "0 0 0 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-176.654 {posy}",
                        OffsetMax = $"179.926 {posy + height}"
                    }
                }, "Panel_5842", "Button_9555");
                List<Item> allItems = new List<Item>();
                allItems.AddRange(player.inventory.containerMain.itemList);
                allItems.AddRange(player.inventory.containerBelt.itemList);
                allItems.AddRange(player.inventory.containerWear.itemList);
                var amount = ItemCount(allItems, check.ShortName, check.skinID);


                var status = amount >= check.Amount * procent / 100 * _config.TimeList[time];
                if (status)
                    i++;
                var color = status ? "1 1 1 1" : "1 0 0 1";
                Outline(ref container, "Button_9555", color, "1.5");
                var image = string.IsNullOrEmpty(check.Image) ? check.ShortName : check.Image;
                container.Add(new CuiElement
                {
                    Name = "Image_9604",
                    Parent = "Button_9555",
                    Components =
                    {
                        new CuiRawImageComponent
                            { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", image) },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-180.683 -21.913",
                            OffsetMax = "-130.683 28.087"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_1531",
                    Parent = "Button_9555",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = check.displayName, Font = "robotocondensed-regular.ttf", FontSize = 30,
                            Align = TextAnchor.MiddleLeft, Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-129.4 -21.686",
                            OffsetMax = "82.054 21.688"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "Label_3548",
                    Parent = "Button_9555",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{amount} / {(int)(check.Amount * procent / 100 * _config.TimeList[time])} ", Font = "robotocondensed-bold.ttf", FontSize = 20,
                            Align = TextAnchor.MiddleRight, Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "38.29 -21.913",
                            OffsetMax = "178.29 21.688"
                        }
                    }
                });
                posy -= height + 5;
            }

            var cmd = i == _config.PriceList.Count ? $"UI_BUILDPROTECTION SUCCESS {procent} {time}" : "";
            // var cmd = $"UI_BUILDPROTECTION SUCCESS {procent} {time}";
            container.Add(new CuiButton
            {
                Button = { Color = "0 0.5686275 1 1", Command = cmd, Close = "Panel_5842" },
                Text =
                {
                    Text = GetMessage("UI_SETUP_FINISH", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 20,
                    Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-176.654 -265.209",
                    OffsetMax = "179.926 -223.938"
                }
            }, "Panel_5842", "Button_7334");

            CuiHelper.DestroyUi(player, "Panel_5842");
            CuiHelper.AddUi(player, container);
        }

        private static int ItemCount(IReadOnlyList<Item> items, string shortname, ulong skin)
        {
            var result = 0;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.info.shortname == shortname && (skin == 0 || item.skin == skin))
                    result += item.amount;
            }

            return result;
        }

        private void Outline(ref CuiElementContainer container, string parent, string color = "1 1 1 1",
            string size = "2.5")
        {
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = $"0 {size}" },
                Image = { Color = color }
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{size}", OffsetMax = "0 0" },
                Image = { Color = color }
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 {size}", OffsetMax = $"{size} -{size}" },
                Image = { Color = color }
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{size} {size}", OffsetMax = $"0 -{size}" },
                Image = { Color = color }
            }, parent);
        }

        #endregion

        #region LANG

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MSG_SUCCEFULL"] = "You have successfully installed {0}% home protection. It will be valid for {1} hours",
                ["UI_HOMEISPROTECT"] = "<size=25>YOUR HOME IS PROTECTED</size>",
                ["UI_STATUS_HEADER"] = "HOME PROTECTION STATUS       ",
                ["UI_STATUS_COMPLETE"] = "I SEE",
                ["UI_PROTECTION_STATUS"] = "PERCENTAGE OF PROTECTION",
                ["UI_TIME_STATUS"] = "PROTECTION CLOCK",
                ["UI_SETUP_HEADER"] = "<size=25>HOME SECURITY INSTALLATION </size>      ",
                ["UI_SETUP_PRICE"] = "RESOURCES TO PAY FOR PROTECTION",
                ["UI_SETUP_NEED"] = "NEED / HAVE",
                ["UI_SETUP_FINISH"] = "INSTALL PROTECTION"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MSG_SUCCEFULL"] = "Вы успешно установили {0}% защиту дома. Она будет действовать {1} часов",
                ["UI_HOMEISPROTECT"] = "ВАШ ДОМ ЗАЩИЩЕН",
                ["UI_STATUS_HEADER"] = "СОСТОЯНИЕ ЗАЩИТЫ ДОМА          ",
                ["UI_STATUS_COMPLETE"] = "I SEE",
                ["UI_PROTECTION_STATUS"] = "ПРОЦЕНТ ЗАЩИТЫ",
                ["UI_TIME_STATUS"] = "ЧАСЫ ЗАЩИТЫ",
                ["UI_SETUP_HEADER"] = "УСТАНОВКА ЗАЩИТЫ ДОМА         ",
                ["UI_SETUP_PRICE"] = "РЕСУРСЫ ДЛЯ ОПЛАТЫ ЗАЩИТЫ",
                ["UI_SETUP_NEED"] = "НУЖНО / ЕСТЬ",
                ["UI_SETUP_FINISH"] = "УСТАНОВИТЬ ЗАЩИТУ"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string steamID)
        {
            return lang.GetMessage(langKey, this, steamID);
        }

        private string GetMessage(string langKey, string steamID, params object[] args)
        {
            return args.Length == 0
                ? GetMessage(langKey, steamID)
                : string.Format(GetMessage(langKey, steamID), args);
        }

        #endregion

        #region Data

        private Dictionary<ulong, Data> _data;

        private class Data
        {
            public DateTime FinishProtection;
            public bool IsProtect;
            public int Protection;
        }

        private void LoadData()
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerData"))
                _data = new Dictionary<ulong, Data>();
            else
                _data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Data>>(
                    $"{Name}/PlayerData");
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", _data);

            if (_data == null)
                _data = new Dictionary<ulong, Data>();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", _data);
        }

        #endregion
    }
}