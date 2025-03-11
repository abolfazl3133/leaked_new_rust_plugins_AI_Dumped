using System;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using System.Globalization;
using Oxide.Core.Plugins;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HitAdvance", "Hougan, Ryamkk", "1.1.2")]
    public class HitAdvance : RustPlugin
    {
		float LineFadeOut = 0.3f;
		
		float TextFadeOut = 0.5f;
		string TextFont = "robotocondensed-bold.ttf";
		string TextColor = "#FFFFFFFF";
		
		private void LoadDefaultConfig()
        {
            GetConfig("Настройки GUI линии дамага", "Время отображения линии дамага (0.3 секунды)", ref LineFadeOut);
            GetConfig("Настройки GUI текста дамага", "Время отображения текста дамага (0.5 секунды)", ref TextFadeOut);
            GetConfig("Настройки GUI текста дамага", "Шрифт текста", ref TextFont);
			GetConfig("Настройки GUI текста дамага", "Цвет текста при попадании в тело", ref TextColor);
            SaveConfig();
        }

        [JsonProperty("Настройки игроков")]
        private Dictionary<ulong, int> playerMarkers = new Dictionary<ulong,int>();
		
        [JsonProperty("Настройка маркеров и их привлегий")]
        private Dictionary<string, string> markerPermissions = new Dictionary<string, string>
        {
            ["ОТКЛЮЧЕНО"] = "",
            ["ПОЛОСА"] = "HitAdvance.Line",
            ["ТЕКСТ"] = "HitAdvance.Text",
			["ОБА"] = "HitAdvance.All",
        };        
        
        private string Layer = "UI_HitMarker";
		
		Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"HitMarkerON", "Вы <color=#81b67a>успешно</color> изменили хит-маркер!"},
			{"HitMarkerPermission", "У вас <color=#81B67a>недостаточно прав</color> для включения этого хит-маркера!"},
        };
		
        private void OnServerInitialized()
        {
			if (Interface.Oxide.DataFileSystem.ExistsDatafile("HitAdvance/Player"))
            {
                playerMarkers = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>("HitAdvance/Player");
            }
			
			if (Interface.Oxide.DataFileSystem.ExistsDatafile("HitAdvance/Permissions"))
            {
                markerPermissions = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>("HitAdvance/Permissions");
            } 
            else
            {
                Interface.Oxide.DataFileSystem.WriteObject("HitAdvance/Permissions", markerPermissions);
                OnServerInitialized();
                return;
            }
            
            foreach (var check in markerPermissions.Where(p => p.Value != ""))
                permission.RegisterPermission(check.Value, this);
            
            BasePlayer.activePlayerList.ForEach(OnPlayerInit);
			lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (!playerMarkers.ContainsKey(player.userID))
                playerMarkers.Add(player.userID, 1);
        }
        
        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info != null && attacker is BasePlayer && info.HitEntity is BasePlayer)
            {
                if (playerMarkers[attacker.userID] == 1)
                    DrawLine(attacker, info.HitEntity as BasePlayer);
                else if (playerMarkers[attacker.userID] == 2)
                {
                    NextTick(() =>
                    {
                        DrawText(attacker, info.HitEntity as BasePlayer, info);
                    });
                }
				else if (playerMarkers[attacker.userID] == 3)
				{
					NextTick(() =>
                    {
						DrawLine(attacker, info.HitEntity as BasePlayer);
                        DrawText(attacker, info.HitEntity as BasePlayer, info);
                    });
				}
            }
        }

		[ChatCommand("marker")]
        private void cmdMarker(BasePlayer player)
		{
            DrawChangeMenu(player);
		}
		
        [ConsoleCommand("changemarker")]
        private void consoleCmdChange(ConsoleSystem.Arg args)
        {
            if (args.Player() == null)
                return;

            BasePlayer player = args.Player();
            if (!args.HasArgs(1))
                DrawChangeMenu(player);
            else
            {
                int newId;
                if (int.TryParse(args.Args[0], out newId))
                {
                    if (markerPermissions.ElementAt(newId).Value == "" || permission.UserHasPermission(player.UserIDString, markerPermissions.ElementAt(newId).Value))
                    {
						SendReply(player, Messages["HitMarkerON"]);
                        playerMarkers[player.userID] = newId;
                        DrawChangeMenu(player);
                    }
                    else
                    {
						SendReply(player, Messages["HitMarkerPermission"]);
                    }
                }
            }
        }

        private void DrawLine(BasePlayer player, BasePlayer target)
        {
            var id = CuiHelper.GetGuid();
            CuiElementContainer container = new CuiElementContainer();
            
            string lineColor = GetGradientColor((int)(target.MaxHealth() - target.health), (int)target.MaxHealth());
            Vector2 linePosition = GetLinePosition(target.health / target.MaxHealth());
            
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = $"{linePosition[0]} 0.1101852", AnchorMax = $"{linePosition[1]} 0.1157407" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", Layer);
            container.Add(new CuiElement
            {
                FadeOut = LineFadeOut,
                Parent = Layer,
                Name = Layer + ".Showed",
                Components =
                {
                    new CuiImageComponent { Color = lineColor },
                    new CuiRectTransformComponent { AnchorMin = "0 0",AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            CuiHelper.AddUi(player, container);
            timer.Once(0.1f, () => { CuiHelper.DestroyUi(player, Layer + ".Showed"); });
        }

        private void DrawText(BasePlayer player, BasePlayer target, HitInfo info)
        {
            var id = CuiHelper.GetGuid();
            CuiElementContainer container = new CuiElementContainer();
            
            float divisionDmg = info.damageTypes.Total() / target.MaxHealth();
            float divisionHP = (target.health  / target.MaxHealth());
            float avgDivision = (target.IsDead() ? 1 : 1 - divisionHP);
            
            var position = GetRandomTextPosition(divisionDmg, divisionHP);

            string textDamage = info.damageTypes.Total().ToString("F0");
            if (info.isHeadshot)
                textDamage = "<color=#DC143C>ГОЛОВА</color>";
            if (target.IsWounded())
            {
                textDamage = "<color=#DC143C>УПАЛ</color>";
                if (info.isHeadshot)
                    textDamage += " <color=#DC143C>ГОЛОВА</color>";
            }
            else if (target.IsDead())
            {
                textDamage = "<color=#DC143C>УМЕР</color>";
                if (info.isHeadshot)
                {
                    textDamage += " <color=#DC143C>ГОЛОВА</color>";
                }
            }
            
            container.Add(new CuiElement()
            {
                Name = id,
                Parent = "Hud",
                FadeOut = TextFadeOut,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"<b>{textDamage}</b>",
                        Color = HexToCuiColor(TextColor),
                        Font = TextFont,
                        FontSize = (int)Mathf.Lerp(15, 30, avgDivision),
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiOutlineComponent() { Color = "0 0 0 1", Distance = "0.155 0.155"},
                    new CuiRectTransformComponent() {AnchorMin = $"{position.x} {position.y}", AnchorMax = $"{position.x} {position.y}", OffsetMin = "-100 -100", OffsetMax = "100 100"}
            
                }
            });

            CuiHelper.AddUi(player, container);
            timer.Once(0.1f, () => { CuiHelper.DestroyUi(player, id); });
        }

        private void DrawChangeMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.3546875 0.3733796", AnchorMax = "0.3546875 0.3733796", OffsetMax = "371 182" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100" },
                Button = { Close = Layer, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".BG",
                Components =
                {
                    new CuiImageComponent { Color = HexToCuiColor("#0000003C") },
                    new CuiRectTransformComponent { AnchorMin = "0 0.4232166", AnchorMax = "1.327 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Header",
                Components =
                {
                    new CuiImageComponent { Color = HexToCuiColor("#81B67AFF") },
                    new CuiRectTransformComponent { AnchorMin = "0 0.8145424", AnchorMax = "1.327 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Header",
                Components =
                {
                    new CuiTextComponent { Text = "ВЫБОР ХИТ-МАРКЕРА", Color = HexToCuiColor("#476443FF"), FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1.014 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent { Text = "Здесь вы можете выбрать вид хит-маркера, либо полностью отключить его.", Color = HexToCuiColor("#FFFFFFFF"), FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0 0.410668", AnchorMax = "1.332 1" }
                }
            });
            
            int i = 0;
            foreach (var check in markerPermissions)
            {
                string color = playerMarkers[player.userID] == i ? HexToCuiColor("#518eefFF") :
                    permission.UserHasPermission(player.UserIDString, check.Value) || check.Value == "" ? HexToCuiColor("#81B67AFF") :  HexToCuiColor("#C44B4BFF");
                
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Name = Layer + $".{i}",
                    Components =
                    {
                        new CuiImageComponent { Color = color },
                        new CuiRectTransformComponent { AnchorMin = $"{0.01254448 + i * 0.332} 0.4561242", AnchorMax = $"{0.3243728 + i * 0.332} 0.5950636", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{i}",
                    Components =
                    {
                        new CuiTextComponent { Text = check.Key, FontSize = 14, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"changemarker {i}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer + $".{i}");
                
                i++;
            }

            CuiHelper.AddUi(player, container);
        }
		
		private static string HexToCuiColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }
 
            var str = hex.Trim('#');
 
            if (str.Length == 6)
                str += "FF";
 
            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
 
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
 
            Color color = new Color32(r, g, b, a);
 
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        private Vector2 GetLinePosition(float divisionDmg)
        {
            float centerX = 0.4932292f;
            float xMax = 0.6411458f;
            float diff = xMax - centerX;
            float lenght05 = diff * divisionDmg;
            
            float xLeft = centerX - lenght05;
            float xRight = centerX + lenght05;
            
            return new Vector2(xLeft, xRight);
        }
        
        Vector2 GetRandomTextPosition(float divisionDmg, float divisionHP)
        {
            float x = (float) Oxide.Core.Random.Range(45, 55) / 100;
            float y = (float) Oxide.Core.Random.Range(45, 55) / 100;
            
            return new Vector2(x, y);
        }
        
        public string GetGradientColor(int count, int max)
        {
            if (count > max)
                count = max;
            float n = max > 0 ? (float)ColorsGradientDB.Length / max : 0;
            var index = (int) (count * n);
            if (index > 0) index--;
            return ColorsGradientDB[ index ];
        }
        
        private string[] ColorsGradientDB = new string[100]
        {
            "0.2000 0.8000 0.2000 1.0000",
            "0.2471 0.7922 0.1961 1.0000",
            "0.2824 0.7843 0.1922 1.0000",
            "0.3176 0.7725 0.1843 1.0000",
            "0.3451 0.7647 0.1804 1.0000",
            "0.3686 0.7569 0.1765 1.0000",
            "0.3922 0.7490 0.1725 1.0000",
            "0.4118 0.7412 0.1686 1.0000",
            "0.4314 0.7333 0.1647 1.0000",
            "0.4471 0.7216 0.1608 1.0000",
            "0.4667 0.7137 0.1569 1.0000",
            "0.4784 0.7059 0.1529 1.0000",
            "0.4941 0.6980 0.1490 1.0000",
            "0.5098 0.6902 0.1412 1.0000",
            "0.5216 0.6824 0.1373 1.0000",
            "0.5333 0.6706 0.1333 1.0000",
            "0.5451 0.6627 0.1294 1.0000",
            "0.5569 0.6549 0.1255 1.0000",
            "0.5647 0.6471 0.1216 1.0000",
            "0.5765 0.6392 0.1176 1.0000",
            "0.5843 0.6314 0.1137 1.0000",
            "0.5922 0.6235 0.1137 1.0000",
            "0.6039 0.6118 0.1098 1.0000",
            "0.6118 0.6039 0.1059 1.0000",
            "0.6196 0.5961 0.1020 1.0000",
            "0.6275 0.5882 0.0980 1.0000",
            "0.6314 0.5804 0.0941 1.0000",
            "0.6392 0.5725 0.0902 1.0000",
            "0.6471 0.5647 0.0863 1.0000",
            "0.6510 0.5569 0.0824 1.0000",
            "0.6588 0.5451 0.0784 1.0000",
            "0.6627 0.5373 0.0784 1.0000",
            "0.6667 0.5294 0.0745 1.0000",
            "0.6745 0.5216 0.0706 1.0000",
            "0.6784 0.5137 0.0667 1.0000",
            "0.6824 0.5059 0.0627 1.0000",
            "0.6863 0.4980 0.0588 1.0000",
            "0.6902 0.4902 0.0588 1.0000",
            "0.6941 0.4824 0.0549 1.0000",
            "0.6980 0.4745 0.0510 1.0000",
            "0.7020 0.4667 0.0471 1.0000",
            "0.7020 0.4588 0.0471 1.0000",
            "0.7059 0.4471 0.0431 1.0000",
            "0.7098 0.4392 0.0392 1.0000",
            "0.7098 0.4314 0.0392 1.0000",
            "0.7137 0.4235 0.0353 1.0000",
            "0.7176 0.4157 0.0314 1.0000",
            "0.7176 0.4078 0.0314 1.0000",
            "0.7216 0.4000 0.0275 1.0000",
            "0.7216 0.3922 0.0275 1.0000",
            "0.7216 0.3843 0.0235 1.0000",
            "0.7255 0.3765 0.0235 1.0000",
            "0.7255 0.3686 0.0196 1.0000",
            "0.7255 0.3608 0.0196 1.0000",
            "0.7255 0.3529 0.0196 1.0000",
            "0.7294 0.3451 0.0157 1.0000",
            "0.7294 0.3373 0.0157 1.0000",
            "0.7294 0.3294 0.0157 1.0000",
            "0.7294 0.3216 0.0118 1.0000",
            "0.7294 0.3137 0.0118 1.0000",
            "0.7294 0.3059 0.0118 1.0000",
            "0.7294 0.2980 0.0118 1.0000",
            "0.7294 0.2902 0.0078 1.0000",
            "0.7255 0.2824 0.0078 1.0000",
            "0.7255 0.2745 0.0078 1.0000",
            "0.7255 0.2667 0.0078 1.0000",
            "0.7255 0.2588 0.0078 1.0000",
            "0.7255 0.2510 0.0078 1.0000",
            "0.7216 0.2431 0.0078 1.0000",
            "0.7216 0.2353 0.0039 1.0000",
            "0.7176 0.2275 0.0039 1.0000",
            "0.7176 0.2196 0.0039 1.0000",
            "0.7176 0.2118 0.0039 1.0000",
            "0.7137 0.2039 0.0039 1.0000",
            "0.7137 0.1961 0.0039 1.0000",
            "0.7098 0.1882 0.0039 1.0000",
            "0.7098 0.1804 0.0039 1.0000",
            "0.7059 0.1725 0.0039 1.0000",
            "0.7020 0.1647 0.0039 1.0000",
            "0.7020 0.1569 0.0039 1.0000",
            "0.6980 0.1490 0.0039 1.0000",
            "0.6941 0.1412 0.0039 1.0000",
            "0.6941 0.1333 0.0039 1.0000",
            "0.6902 0.1255 0.0039 1.0000",
            "0.6863 0.1176 0.0039 1.0000",
            "0.6824 0.1098 0.0039 1.0000",
            "0.6784 0.1020 0.0039 1.0000",
            "0.6784 0.0941 0.0039 1.0000",
            "0.6745 0.0863 0.0039 1.0000",
            "0.6706 0.0784 0.0039 1.0000",
            "0.6667 0.0706 0.0039 1.0000",
            "0.6627 0.0627 0.0039 1.0000",
            "0.6588 0.0549 0.0039 1.0000",
            "0.6549 0.0431 0.0039 1.0000",
            "0.6510 0.0353 0.0000 1.0000",
            "0.6471 0.0275 0.0000 1.0000",
            "0.6392 0.0196 0.0000 1.0000",
            "0.6353 0.0118 0.0000 1.0000",
            "0.6314 0.0039 0.0000 1.0000",
            "0.6275 0.0000 0.0000 1.0000",
        };
		
		private void GetConfig<T>(string menu, string Key, ref T var)
        {
            if (Config[menu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[menu, Key], typeof(T));
            }

            Config[menu, Key] = var;
        }
    }
}