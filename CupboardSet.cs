using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CupboardSet", "LAGZYA", "1.0.4")]
    public class CupboardSet : RustPlugin
    {
        #region lang
        protected override void LoadDefaultMessages()
        {
            var ru = new Dictionary<string, string>();
            foreach (var rus in new Dictionary<string, string>()
            {
                ["CS_LABLE"] = "СПИСОК ИГРОКОВ",
                ["CS_REMOVE"] = "удалить",
            }) ru.Add(rus.Key, rus.Value);
            var en = new Dictionary<string, string>();
            foreach (var ens in new Dictionary<string, string>()
            {
                ["CS_LABLE"] = "LIST PLAYERS",
                ["CS_REMOVE"] = "remove"
            }) en.Add(ens.Key, ens.Value);
            lang.RegisterMessages(ru, this, "ru");
            lang.RegisterMessages(en, this, "en");
        }
        #endregion
        private string API_KEY = "CupboardSet-213213123123saafvqasd";
        private void OnServerInitialized()
        {

            AddImage($"https://rustapi.top/cartinki/givecart.php?token={API_KEY}&image=remove_cupb.png", $"remove_cupboard");
            AddImage($"https://rustapi.top/cartinki/givecart.php?token={API_KEY}&image=button_cup.png", $"button_cupboard");
        }

        void OnLootEntity(BasePlayer player, BuildingPrivlidge entity)
        {
            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = "0 0 0 0",
                },
                RectTransform =
                {
                    AnchorMin = "0.5 1",
                    AnchorMax = "0.5 1",
                    OffsetMin = "-100 -360",
                    OffsetMax = "100 -210"
                }
            }, "Overlay", "CupboardSet");
            cont.Add(new CuiElement()
            {
                Name = "CupboardSet" + "Global",
                Parent = "CupboardSet",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Png = GetImage("button_cupboard"),
                        Color = HexToRustFormat("#ba422a")
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"-75 {208}",
                        OffsetMax = $"75 {230}"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = "CupboardSet" + "Global",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"{lang.GetMessage("CS_LABLE", this, player.UserIDString)}".ToUpper(),
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });
            int i = 0;
            foreach (var entityAuthorizedPlayer in entity.authorizedPlayers)
            {
                var min = i % 2 == 0 ? -155 : 0;
                var max = i % 2 == 0 ? -15 : 150;
                cont.Add(new CuiElement()
                {
                    Name = "CupboardSet" + "Players" + i,
                    Parent = "CupboardSet",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Png = GetImage("button_cupboard"),
                            Color = HexToRustFormat("#292f3df0")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = $"{min} {176 - i/2 * 31}",
                            OffsetMax = $"{max} {204 - i/2 * 31}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = "CupboardSet" + "Players" + i,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"{entityAuthorizedPlayer.username}".ToUpper(),
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0.8 1",
                        }
                    }
                });
                if (entity.OwnerID == entityAuthorizedPlayer.userid)
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = "CupboardSet" + "Players" + i,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Sprite = "assets/icons/favourite_servers.png",
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0.05 0.25",
                                AnchorMax = "0.15 0.75",
                            }
                        }
                    });
                }
                cont.Add(new CuiElement()
                {
                    Parent = "CupboardSet" + "Players" + i,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage("remove_cupboard")
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.7 0.11",
                            AnchorMax = "0.98 0.89"
                        }
                    }
                });
                cont.Add(new CuiButton()
                {
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"ui_cupboardset remove {entityAuthorizedPlayer.userid} {entity.net.ID}",
                    },
                    Text =
                    {
                        Text = $"{lang.GetMessage("CS_REMOVE", this, player.UserIDString)}",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 8,
                        Font = "robotocondensed-regular.ttf"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.7 0.11",
                        AnchorMax = "0.98 0.89"
                    }
                }, "CupboardSet" + "Players" + i);
                i++;
            }

            CuiHelper.DestroyUi(player, "CupboardSet");
            CuiHelper.AddUi(player, cont);
        }
        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (!(inventory.entitySource is BuildingPrivlidge)) return;
            CuiHelper.DestroyUi(inventory._baseEntity, "CupboardSet");
        }
        #region Help

        [PluginReference] private Plugin ImageLibrary;

        [ConsoleCommand("ui_cupboardset")]
        void UICommands(ConsoleSystem.Arg arg)
        {
            switch (arg.Args[0])
            {
                case "remove":
                    var ent = BaseNetworkable.serverEntities.Find(new NetworkableId (uint.Parse(arg.Args[2]))) as BuildingPrivlidge;
                    if (ent.authorizedPlayers.Exists(p => p.userid == arg.Player().userID))
                    {
                        if (!ent.authorizedPlayers.Exists(p => p.userid == ulong.Parse(arg.Args[1]))) return;
                        var friend = ent.authorizedPlayers.Find(p => p.userid == ulong.Parse(arg.Args[1]));
                        ent.authorizedPlayers.Remove(friend);
                        ent.UpdateMaxAuthCapacity();
                        ent.SendNetworkUpdate();
                        OnLootEntity(arg.Player(), ent);
                        var target = BasePlayer.FindByID(ulong.Parse(arg.Args[1]));
                        if (target != null)
                            target.SendNetworkUpdate();
                        if (friend.userid == arg.Player().userID)
                        {
                            arg.Player().EndLooting();
                            CuiHelper.DestroyUi(arg.Player(), "CupboardSet");
                        }
                    }
                    break;
            }
        }

        public string GetImage(string shortname, ulong skin = 0) =>
            (string)ImageLibrary.Call("GetImage", shortname, skin);

        public bool AddImage(string url, string shortname, ulong skin = 0) =>
            (bool)ImageLibrary.Call("AddImage", url, shortname, skin);

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}