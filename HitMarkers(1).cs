using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Hit Markers", "https://blackplugin.ru", "1.2.2")]
	public class HitMarkers : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			UINotify = null,
			Notify = null;

		private const string Layer = "UI.HitMarkers";

		private const string HitLayer = "UI.HitMarkers.Hit";

		private const string HealthLineLayer = "UI.HitMarkers.HealthLine";

		private static HitMarkers _instance;

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"marker", "hits"};

			[JsonProperty(PropertyName = "Permission (ex: hitmarkers.use)")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Fonts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, FontConf> Fonts = new Dictionary<int, FontConf>
			{
				[0] = new FontConf
				{
					Font = "robotocondensed-bold.ttf",
					Permission = string.Empty
				},
				[1] = new FontConf
				{
					Font = "robotocondensed-regular.ttf",
					Permission = string.Empty
				},
				[2] = new FontConf
				{
					Font = "permanentmarker.ttf",
					Permission = string.Empty
				},
				[3] = new FontConf
				{
					Font = "droidsansmono.ttf",
					Permission = string.Empty
				}
			};

			[JsonProperty(PropertyName = "Min Font Size")]
			public int MinFontSize = 8;

			[JsonProperty(PropertyName = "Max Font Size")]
			public int MaxFontSize = 18;

			[JsonProperty(PropertyName = "Buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<BtnConf> Buttons = new List<BtnConf>
			{
				new BtnConf
				{
					Enabled = true,
					Title = "Text",
					Type = BtnType.Text,
					Description = "<b>Damage numbers</b> will pop up in the center of the screen!",
					Permission = string.Empty
				},
				new BtnConf
				{
					Enabled = true,
					Title = "Icon",
					Type = BtnType.Icon,
					Description = "The familiar hit icon changes color after a <b>headshot!</b>",
					Permission = string.Empty
				},
				new BtnConf
				{
					Enabled = true,
					Title = "Health Line",
					Type = BtnType.HealthLine,
					Description = "A bar appears above the slots, showing the <b>remaining</b> health of the enemy",
					Permission = string.Empty
				},
				new BtnConf
				{
					Enabled = true,
					Title = "Buildings",
					Type = BtnType.Buildings,
					Description = "Displaying damage by buildings",
					Permission = string.Empty
				}
			};

			[JsonProperty(PropertyName = "Info Icon")]
			public string InfoIcon = "https://i.imgur.com/YIRjnIT.png";

			[JsonProperty(PropertyName = "Hit Icon")]
			public string HitIcon = "assets/icons/close.png";

			[JsonProperty(PropertyName = "Fall Icon")]
			public string FallIcon = "assets/icons/fall.png";

			[JsonProperty(PropertyName = "Show damage to NPC")]
			public bool ShowNpcDamage = true;

			[JsonProperty(PropertyName = "Show damage to animals")]
			public bool ShowAnimalDamage = false;

			[JsonProperty(PropertyName = "Marker removal time")]
			public float DestroyTime = 0.25f;

			[JsonProperty(PropertyName = "Default Values")]
			public DefaultValues DefaultValues = new DefaultValues
			{
				FontSize = 14,
				Buildings = false,
				FontId = 0,
				HealthLine = false,
				Icon = false,
				Text = true
			};

			[JsonProperty(PropertyName = "Line Settings")]
			public LineSettings Line = new LineSettings
			{
				Show = true,
				Text = false
			};
		}

		private class LineSettings
		{
			[JsonProperty(PropertyName = "Show Line?")]
			public bool Show;

			[JsonProperty(PropertyName = "Show Text?")]
			public bool Text;
		}

		private class DefaultValues
		{
			[JsonProperty(PropertyName = "Font ID")]
			public int FontId;

			[JsonProperty(PropertyName = "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = "Text")] public bool Text;

			[JsonProperty(PropertyName = "Icon")] public bool Icon;

			[JsonProperty(PropertyName = "Health Line")]
			public bool HealthLine;

			[JsonProperty(PropertyName = "Buildings")]
			public bool Buildings;
		}

		private class BtnConf
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public BtnType Type;

			[JsonProperty(PropertyName = "Description")]
			public string Description;

			[JsonProperty(PropertyName = "Permission (ex: hitmarkers.text)")]
			public string Permission;
		}

		private enum BtnType
		{
			Text,
			Icon,
			HealthLine,
			Buildings
		}

		private class FontConf
		{
			[JsonProperty(PropertyName = "Font")] public string Font;

			[JsonProperty(PropertyName = "Permission (ex: hitmarkers.font)")]
			public string Permission;
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
			catch (Exception ex)
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
				Debug.LogException(ex);
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

		#region Data

		private static PluginData _data;

		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
		}

		private class PluginData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
		}

		private class PlayerData
		{
			[JsonProperty(PropertyName = "Font ID")]
			public int FontId;

			[JsonProperty(PropertyName = "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = "Text")] public bool Text;

			[JsonProperty(PropertyName = "Icon")] public bool Icon;

			[JsonProperty(PropertyName = "Health Line")]
			public bool HealthLine;

			[JsonProperty(PropertyName = "Buildings")]
			public bool Buildings;

			public static PlayerData GetOrAdd(BasePlayer player)
			{
				return GetOrAdd(player.userID);
			}

			public static PlayerData GetOrAdd(ulong userId)
			{
				_data.Players.TryAdd(userId, new PlayerData
				{
					FontSize = _config.DefaultValues.FontSize,
					FontId = _config.DefaultValues.FontId,
					Text = _config.DefaultValues.Text,
					Buildings = _config.DefaultValues.Buildings,
					Icon = _config.DefaultValues.Icon,
					HealthLine = _config.DefaultValues.HealthLine
				});

				return _data.Players[userId];
			}

			public bool GetValue(BtnType type)
			{
				switch (type)
				{
					case BtnType.Text:
						return Text;
					case BtnType.Icon:
						return Icon;
					case BtnType.HealthLine:
						return HealthLine;
					case BtnType.Buildings:
						return Buildings;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			public void SetValue(BtnType type, bool newValue)
			{
				switch (type)
				{
					case BtnType.Text:
						Text = newValue;
						break;
					case BtnType.Icon:
						Icon = newValue;
						break;
					case BtnType.HealthLine:
						HealthLine = newValue;
						break;
					case BtnType.Buildings:
						Buildings = newValue;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadData();

			RegisterPermissions();
		}

		private void OnServerInitialized()
		{
			LoadImages();

			AddCovalenceCommand(_config.Commands, nameof(CmdOpenMarkers));
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

			_markerByPlayer.Values.ToList().ForEach(marker =>
			{
				if (marker != null)
					marker.Kill();
			});

			SaveData();

			_instance = null;
			_data = null;
			_config = null;
		}


		private void OnEntityTakeDamage(BuildingBlock block, HitInfo info)
		{
			if (block == null || info == null || info.damageTypes.Total() < 1) return;

			var player = info.InitiatorPlayer;
			if (player == null || HasPermission(player) == false) return;

			var marker = GetMarker(player);
			if (marker == null) return;

			if (PlayerData.GetOrAdd(player).Buildings)
				NextTick(() =>
				{
					if (block != null && !block.IsDestroyed)
						marker.ShowHit(block, info);
				});
		}

		private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
		{
			if (attacker == null || attacker.IsNpc || info == null || HasPermission(attacker) == false) return;

			var target = info.HitEntity as BaseCombatEntity;
			if (target == null || target is BaseCorpse || target is BuildingBlock ||
			    (!_config.ShowAnimalDamage && target is BaseAnimalNPC) ||
			    (!_config.ShowNpcDamage &&
			     (target is BaseNpc || (target is BasePlayer && (target as BasePlayer).IsNpc)))) return;

			NextTick(() =>
			{
				if (target != null && !target.IsDestroyed)
					GetOrAddMarker(attacker).ShowHit(target, info);
			});
		}

		#endregion

		#region Commands

		private void CmdOpenMarkers(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (HasPermission(player) == false)
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			MainUi(player, true);
		}

		[ConsoleCommand("UI_Markers")]
		private void CmdConsoleMarkers(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "settype":
				{
					BtnType type;
					if (!arg.HasArgs(2) || !Enum.TryParse(arg.Args[1], out type)) return;

					var data = PlayerData.GetOrAdd(player);
					if (data == null) return;

					var perm = _config.Buttons.Find(x => x.Type == type);
					if (perm != null && HasPermission(player) == false)
					{
						SendNotify(player, NoPermission, 1);
						return;
					}

					data.SetValue(type, !data.GetValue(type));

					MainUi(player);
					break;
				}

				case "setsize":
				{
					int fontSize;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out fontSize)) return;

					var data = PlayerData.GetOrAdd(player);
					if (data == null) return;

					data.FontSize = fontSize;

					MainUi(player);
					break;
				}

				case "setfont":
				{
					int fontId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out fontId)) return;

					var data = PlayerData.GetOrAdd(player);
					if (data == null) return;

					data.FontId = fontId;

					MainUi(player);
					break;
				}

				case "info":
				{
					int index;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out index) || index < 0 ||
					    _config.Buttons.Count <= index) return;

					InfoUi(player, _config.Buttons[index].Description);
					break;
				}
			}
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, bool first = false)
		{
			var data = PlayerData.GetOrAdd(player);
			if (data == null) return;

			float xSwitch;
			float ySwitch;
			float width;
			float height;
			float margin;

			var container = new CuiElementContainer();

			#region Background

			if (first)
			{
				CuiHelper.DestroyUi(player, Layer);

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0 0 0 0.9",
						Material = "assets/content/ui/uibackgroundblur.mat"
					},
					CursorEnabled = true
				}, "Overlay", Layer);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Close = Layer
					}
				}, Layer);
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-300 -190",
					OffsetMax = "300 190"
				},
				Image =
				{
					Color = HexToCuiColor("#0E0E10")
				}
			}, Layer, Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, Layer + ".Main", Layer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, TitleMenu),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, Layer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-50 -37.5",
					OffsetMax = "-25 -12.5"
				},
				Text =
				{
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Close = Layer,
					Color = HexToCuiColor("#4B68FF")
				}
			}, Layer + ".Header");

			#endregion

			#region Preview

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "95 -255",
					OffsetMax = "265 -85"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".Preview");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 0", OffsetMax = "0 18"
				},
				Text =
				{
					Text = Msg(player, LooksNow),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 0.4"
				}
			}, Layer + ".Preview");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = Msg(player, PreviewTitle),
					Align = TextAnchor.MiddleCenter,
					Font = $"{_config.Fonts[data.FontId].Font}",
					FontSize = data.FontSize,
					Color = "1 1 1 0.7"
				}
			}, Layer + ".Preview");

			#endregion

			#region Fonts

			xSwitch = -265f;
			ySwitch = -85f;
			margin = 10f;
			width = 80f;
			height = 80f;

			var i = 1;
			foreach (var fontConf in _config.Fonts)
			{
				var selected = fontConf.Key == data.FontId;

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - height}",
						OffsetMax = $"{xSwitch + width} {ySwitch}"
					},
					Image =
					{
						Color = selected ? HexToCuiColor("#4B68FF") : HexToCuiColor("#161617")
					}
				}, Layer + ".Main", Layer + $".Font.{fontConf.Key}");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "1 1",
						OffsetMin = "0 2", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, TextTitle),
						Align = TextAnchor.LowerCenter,
						Font = fontConf.Value.Font,
						FontSize = 14,
						Color = selected ? HexToCuiColor("#FFFFFF") : HexToCuiColor("#4B68FF")
					}
				}, Layer + $".Font.{fontConf.Key}");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0.5",
						OffsetMin = "0", OffsetMax = "0 -2"
					},
					Text =
					{
						Text = Msg(player, FontTitle, i),
						Align = TextAnchor.UpperCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, Layer + $".Font.{fontConf.Key}");

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Markers setfont {fontConf.Key}"
					}
				}, Layer + $".Font.{fontConf.Key}");

				xSwitch += margin + width;
				i++;
			}

			#endregion

			#region Font Size

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-265 -255",
					OffsetMax = "85 -175"
				},
				Image =
				{
					Color = HexToCuiColor("#161617")
				}
			}, Layer + ".Main", Layer + ".FontSize");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 1",
					OffsetMin = "15 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, FontIncreaseTitle),
					Align = TextAnchor.LowerLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, Layer + ".FontSize");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 0.5",
					OffsetMin = "280 -25",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, FontSizeFormat, data.FontSize),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				}
			}, Layer + ".FontSize");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "0 0.5",
					OffsetMin = "15 -15",
					OffsetMax = "265 -10"
				},
				Image =
				{
					Color = HexToCuiColor("#C4C4C4", 20)
				}
			}, Layer + ".FontSize", Layer + ".FontSize.Line");

			width = 250;

			var steps = _config.MaxFontSize - _config.MinFontSize;

			var progress = (float) (data.FontSize - _config.MinFontSize) / steps;

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progress} 0.95"},
				Image =
				{
					Color = HexToCuiColor("#4B68FF")
				}
			}, Layer + ".FontSize.Line", Layer + ".FontSize.Line.Finish");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "1 0.5", AnchorMax = "1 0.5",
					OffsetMin = "-5 -5", OffsetMax = "5 5"
				},
				Image =
				{
					Color = "1 1 1 1"
				}
			}, Layer + ".FontSize.Line.Finish");

			var size = width / steps;

			xSwitch = 0;
			for (var j = _config.MinFontSize; j <= _config.MaxFontSize; j++)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = $"{xSwitch} 0",
						OffsetMax = $"{xSwitch + size} 0"
					},
					Text =
					{
						Text = ""
					},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Markers setsize {j}"
					}
				}, Layer + ".FontSize.Line");

				xSwitch += size;
			}

			#endregion

			#region Buttons

			width = 530;

			var buttons = _config.Buttons.FindAll(x => x.Enabled);

			margin = 10f;

			size = (width - (buttons.Count - 1) * margin) / buttons.Count;

			xSwitch = -265f;

			for (i = 0; i < buttons.Count; i++)
			{
				var btn = buttons[i];

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} -325",
						OffsetMax = $"{xSwitch + size} -265"
					},
					Image =
					{
						Color = HexToCuiColor("#161617")
					}
				}, Layer + ".Main", Layer + $".Btn.{i}");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{btn.Title}",
						Align = TextAnchor.LowerCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + $".Btn.{i}");

				SwitchUi(ref container, Layer + $".Btn.{i}", data.GetValue(btn.Type), $"UI_Markers settype {btn.Type}");

				#region Info

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-40 16",
						OffsetMax = "0 32"
					},
					Text =
					{
						Text = Msg(player, InfoTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 0.4"
					}
				}, Layer + $".Btn.{i}");

				if (ImageLibrary)
					container.Add(new CuiElement
					{
						Parent = Layer + $".Btn.{i}",
						Components =
						{
							new CuiRawImageComponent {Png = GetImage(_config.InfoIcon)},
							new CuiRectTransformComponent
							{
								AnchorMin = "1 0", AnchorMax = "1 0",
								OffsetMin = "-52 18", OffsetMax = "-40 30"
							}
						}
					});

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-52 18", OffsetMax = "0 30"
					},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Markers info {_config.Buttons.IndexOf(btn)}"
					}
				}, Layer + $".Btn.{i}");

				#endregion

				xSwitch += margin + size;
			}

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void SwitchUi(ref CuiElementContainer container, string parent, bool value, string command)
		{
			var guid = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 0",
					OffsetMin = "18 16",
					OffsetMax = "56 28"
				},
				Image =
				{
					Color = HexToCuiColor("#C4C4C4", 20)
				}
			}, parent, guid);

			if (value)
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0.5 0", AnchorMax = "1 1"},
					Image =
					{
						Color = HexToCuiColor("#4B68FF")
					}
				}, guid);
			else
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0.5 1"},
					Image =
					{
						Color = HexToCuiColor("#FFFFFF", 40)
					}
				}, guid);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{command}"
				}
			}, guid);
		}

		private void InfoUi(BasePlayer player, string text)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-200 0", OffsetMax = "200 130"
				},
				Text =
				{
					Text = $"{text}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = "1 1 1 0.5"
				}
			}, Layer, Layer + ".Info");

			CuiHelper.DestroyUi(player, Layer + ".Info");
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

		private void LoadImages()
		{
			if (ImageLibrary == null || !ImageLibrary.IsLoaded)
			{
				PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
			}
			else
			{
				var imagesList = new Dictionary<string, string>();

				if (_config.InfoIcon.Contains("assets/") == false)
					AddImage(ref imagesList, _config.InfoIcon);

				if (_config.HitIcon.Contains("assets/") == false)
					AddImage(ref imagesList, _config.HitIcon);

				if (_config.FallIcon.Contains("assets/") == false)
					AddImage(ref imagesList, _config.FallIcon);

				if (imagesList.Count > 0)
					ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		private void AddImage(ref Dictionary<string, string> imagesList, string image)
		{
			if (!string.IsNullOrEmpty(image))
				imagesList.TryAdd(image, image);
		}

		private string GetImage(string image, ulong skin = 0)
		{
			return ImageLibrary.Call<string>("GetImage", image, skin);
		}

		private bool HasPermission(BasePlayer player)
		{
			return string.IsNullOrEmpty(_config.Permission) ||
			       permission.UserHasPermission(player.UserIDString, _config.Permission);
		}

		private void RegisterPermissions()
		{
			foreach (var font in _config.Fonts.Values.Where(check =>
				         !string.IsNullOrEmpty(check.Permission) && !permission.PermissionExists(check.Permission)))
				permission.RegisterPermission(font.Permission, this);

			_config.Buttons.ForEach(btn =>
			{
				if (!string.IsNullOrEmpty(btn.Permission) && !permission.PermissionExists(btn.Permission))
					permission.RegisterPermission(btn.Permission, this);
			});

			if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
				permission.RegisterPermission(_config.Permission, this);
		}

		private static string HexToCuiColor(string hex, float alpha = 100)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

			var str = hex.Trim('#');
			if (str.Length != 6) throw new Exception(hex);
			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
		}

		private static bool IsTeammates(ulong player, ulong friend)
		{
			return RelationshipManager.ServerInstance.FindPlayersTeam(player)?.members?.Contains(friend) == true;
		}

		private static Vector2 GetRandomTextPosition()
		{
			var x = (float) Random.Range(45, 55) / 100;
			var y = (float) Random.Range(40, 60) / 100;

			return new Vector2(x, y);
		}

		private static string GetGradientColor(int count, int max)
		{
			if (count > max)
				count = max;
			var n = max > 0 ? (float) ColorsGradientDB.Length / max : 0;
			var index = (int) (count * n);
			if (index > 0) index--;
			return ColorsGradientDB[index];
		}

		private static readonly string[] ColorsGradientDB =
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
			"0.6275 0.0000 0.0000 1.0000"
		};

		#endregion

		#region Component

		private readonly Dictionary<BasePlayer, MarkerComponent> _markerByPlayer =
			new Dictionary<BasePlayer, MarkerComponent>();

		private MarkerComponent GetMarker(BasePlayer player)
		{
			MarkerComponent marker;
			return _markerByPlayer.TryGetValue(player, out marker) && marker != null ? marker : null;
		}

		private MarkerComponent GetOrAddMarker(BasePlayer player)
		{
			MarkerComponent marker;
			if (_markerByPlayer.TryGetValue(player, out marker) && marker != null) return marker;

			return player.gameObject.AddComponent<MarkerComponent>();
		}

		private class MarkerComponent : FacepunchBehaviour
		{
			private BasePlayer _player;

			private PlayerData _playerData;

			private void Awake()
			{
				_player = GetComponent<BasePlayer>();

				_playerData = PlayerData.GetOrAdd(_player);

				_instance._markerByPlayer[_player] = this;
			}

			public void ShowHit(BaseCombatEntity target, HitInfo info)
			{
				if (_playerData.HealthLine) ShowLine(BtnType.HealthLine, target, info);
				if (_playerData.Text) ShowLine(BtnType.Text, target, info);
				if (_playerData.Icon) ShowLine(BtnType.Icon, target, info);
			}

			private void ShowLine(BtnType type, BaseCombatEntity target, HitInfo info)
			{
				var container = new CuiElementContainer();

				switch (type)
				{
					case BtnType.Text:
					{
						var pos = GetRandomTextPosition();
						var textDamage = info.damageTypes.Total().ToString("F0");

						if (Mathf.FloorToInt(info.damageTypes.Total()) == 0)
							return;

						var targetPlayer = target as BasePlayer;
						if (targetPlayer != null)
						{
							if (info.isHeadshot)
								textDamage = _instance.Msg(_player, FormatHeadshotTitle, textDamage);

							if (targetPlayer.IsWounded())
							{
								textDamage = _instance.Msg(_player, FormatFellTitle);
								if (info.isHeadshot)
									textDamage += _instance.Msg(_player, FormatFellHeadshotTitle);
							}

							if (IsTeammates(_player.userID, targetPlayer.userID))
								textDamage = _instance.Msg(_player, FormatFriendTitle);
						}

						var hitId = CuiHelper.GetGuid();
						container.Add(new CuiElement
						{
							Name = hitId,
							Parent = "Hud",
							FadeOut = 0.5f,
							Components =
							{
								new CuiTextComponent
								{
									Text = $"{textDamage}",
									Color = "1 1 1 1",
									Font = _config.Fonts[_playerData.FontId].Font,
									FontSize = _playerData.FontSize,
									Align = TextAnchor.MiddleCenter
								},
								new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.15500772 0.1550507712"},
								new CuiRectTransformComponent
								{
									AnchorMin = $"{pos.x} {pos.y}", AnchorMax = $"{pos.x} {pos.y}",
									OffsetMin = "-100 -100", OffsetMax = "100 100"
								}
							}
						});

						CuiHelper.AddUi(_player, container);
						StartCoroutine(DestroyHit(hitId));
						break;
					}

					case BtnType.Icon:
					{
						var hitId = CuiHelper.GetGuid();

						var color = "1 1 1 0.5";
						var image =
							string.IsNullOrEmpty(_config.HitIcon) ? "assets/icons/close.png" : _config.HitIcon;
						float margin = 10;

						var targetPlayer = target as BasePlayer;
						if (targetPlayer != null)
						{
							if (targetPlayer.IsWounded())
							{
								margin = 20;
								image =
									string.IsNullOrEmpty(_config.FallIcon)
										? "assets/icons/fall.png"
										: _config.FallIcon;
							}

							if (targetPlayer.IsWounded() || targetPlayer.IsDead())
								color = "1 0.207745 0.20771 0.5";
						}

						if (info.isHeadshot) color = "1 0.2 0.2 0.5";

						if (image.Contains("assets/"))
							container.Add(new CuiButton
							{
								FadeOut = 0.3f,
								RectTransform =
								{
									AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{margin} -{margin}",
									OffsetMax = $"{margin} {margin}"
								},
								Button =
								{
									Color = color,
									Sprite = image
								},
								Text = {Text = ""}
							}, "Hud", hitId);
						else
							container.Add(new CuiElement
							{
								Name = hitId,
								Parent = "Hud",
								FadeOut = 0.3f,
								Components =
								{
									new CuiRawImageComponent
									{
										Color = color,
										Png = _instance.GetImage(image)
									},
									new CuiRectTransformComponent
									{
										AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
										OffsetMin = $"-{margin} -{margin}",
										OffsetMax = $"{margin} {margin}"
									}
								}
							});

						CuiHelper.AddUi(_player, container);
						CuiHelper.DestroyUi(_player, hitId);
						break;
					}

					case BtnType.HealthLine:
					{
						var curHealth = target.health;
						var maxHealth = target._maxHealth;

						var block = target as BuildingBlock;
						if (block != null)
						{
							var curGrade = block.currentGrade;
							if (curGrade == null) return;

							maxHealth = curGrade.maxHealth;
						}

						var color = _config.Line.Show ? GetGradientColor((int) curHealth, (int) maxHealth) : "0 0 0 0";
						var decreaseLength = (180.5f + 199.5f) / 2f * (curHealth / maxHealth);

						container.Add(new CuiPanel
						{
							FadeOut = 0.5f,
							RectTransform =
							{
								AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"{-10 - decreaseLength} 80",
								OffsetMax = $"{-9 + decreaseLength} 85"
							},
							Image = {Color = color}
						}, "Hud", HealthLineLayer);

						if (_config.Line.Text)
							container.Add(new CuiElement
							{
								Parent = HealthLineLayer,
								FadeOut = 0.5f,
								Components =
								{
									new CuiTextComponent
									{
										Text = $"{maxHealth - curHealth:F0}",
										Color = "1 1 1 1",
										Font = _config.Fonts[_playerData.FontId].Font,
										FontSize = _playerData.FontSize,
										Align = TextAnchor.LowerCenter
									},
									new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.15500772 0.1550507712"},
									new CuiRectTransformComponent
									{
										AnchorMin = "0 1", AnchorMax = "1 1",
										OffsetMin = "0 0", OffsetMax = "0 30"
									}
								}
							});


						DestroyHealthLine();
						CuiHelper.AddUi(_player, container);

						Invoke(DestroyHealthLine, _config.DestroyTime);
						break;
					}
				}
			}

			#region Destroy Hit

			public void DestroyHit()
			{
				CancelInvoke(DestroyHit);

				CuiHelper.DestroyUi(_player, HitLayer);
			}

			public void DestroyHealthLine()
			{
				CancelInvoke(DestroyHealthLine);

				CuiHelper.DestroyUi(_player, HealthLineLayer);
			}

			public IEnumerator DestroyHit(string id, float delay = 0.5f)
			{
				yield return CoroutineEx.waitForSeconds(delay);

				CuiHelper.DestroyUi(_player, id);
			}

			#endregion

			private void OnDestroy()
			{
				_instance?._markerByPlayer.Remove(_player);
			}

			public void Kill()
			{
				Destroy(this);
			}
		}

		#endregion

		#region Lang

		private const string
			InfoTitle = "InfoTitle",
			FontSizeFormat = "FontSizeFormat",
			FontIncreaseTitle = "FontIncreaseTitle",
			FontTitle = "FontTitle",
			TextTitle = "TextTitle",
			PreviewTitle = "PreviewTitle",
			LooksNow = "LooksNow",
			FormatFriendTitle = "FormatFriendTitle",
			FormatFellHeadshotTitle = "FormatFellHeadshotTitle",
			FormatFellTitle = "FormatFellTitle",
			FormatHeadshotTitle = "FormatHeadshotTitle",
			NoPermission = "NoPermission",
			CloseButton = "CloseButton",
			TitleMenu = "TitleMenu";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoPermission] = "You don't have permission to use this command!",
				[CloseButton] = "✕",
				[TitleMenu] = "Hit Markers",
				[FormatHeadshotTitle] = "<color=#DC143C>{0}</color>",
				[FormatFellTitle] = "<color=#DC143C>FELL</color>",
				[FormatFellHeadshotTitle] = " <color=#DC143C>HEADSHOT</color>",
				[FormatFriendTitle] = "<color=#32915a>FRIEND</color>",
				[LooksNow] = "What it looks like now",
				[PreviewTitle] = "-90",
				[TextTitle] = "TEXT",
				[FontTitle] = "Font #{0}",
				[FontIncreaseTitle] = "Increase the font size",
				[FontSizeFormat] = "{0}px",
				[InfoTitle] = "Info"
			}, this);
		}

		private string Msg(string key, string userid = null, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, userid), obj);
		}

		private string Msg(BasePlayer player, string key, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			SendReply(player, Msg(player, key, obj));
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion
	}
}