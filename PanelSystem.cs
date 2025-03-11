// #define TESTING

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("Panel System", "Mevent", "1.1.10")]
	internal class PanelSystem : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			Notify = null,
			WipeBlock = null,
			UINotify = null;

		private static PanelSystem _instance;

		private bool _enabledImageLibrary;

#if CARBON
        private ImageDatabaseModule imageDatabase;
#endif

		private const string 
			Layer = "UI.PanelSystem",
			EventsLayer = "UI.PanelSystem.Events",
			SettingsLayer = "UI.PanelSystem.Settings";

		private Dictionary<string, bool> _eventsCache = new Dictionary<string, bool>
		{
			["CH47"] = false,
			["Bradley"] = false,
			["Helicopter"] = false,
			["Airdrop"] = false,
			["CargoShip"] = false,
			["Convoy"] = false,
			["ArmoredTrain"] = false,
			["Sputnik"] = false,
			["SpaceEvent"] = false,
			["AirEvent"] = false,
			["WipeBlock"] = false
		};

		private readonly List<PanelSettings> _totalSettings = new List<PanelSettings>();

		private enum PanelType
		{
			Online = 0,
			Time = 1,
			CH47 = 2,
			Helicopter = 3,
			Bradley = 4,
			Airdrop = 5,
			Economics = 6,
			Custom = 7,
			CargoShip = 8,
			Button = 9,
			Convoy = 10,
			ArmoredTrain = 11,
			WipeBlock = 12,
			Sputnik = 13,
			SpaceEvent = 14,
			Sleepers = 15,
			AirEvent = 16
		}

		private Dictionary<int, ButtonInfo> _buttonByID = new Dictionary<int, ButtonInfo>();

		private const string _chatSay = "chat.say";

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
			public string DisplayType = "Overlay";

			[JsonProperty(PropertyName = "Panel Anchor")] [JsonConverter(typeof(StringEnumConverter))]
			public PanelAnchor PanelAnchor = PanelAnchor.UpperRight;

			[JsonProperty(PropertyName = "Logotype")]
			public LogoSettings Logo = new LogoSettings
			{
				Enabled = true,
				Permission = string.Empty,
				Image = "https://i.ibb.co/VYtGJrY/image.png",
				Command = string.Empty,
				AnchorMin = "0 0", AnchorMax = "1 1",
				OffsetMin = "-100 -110", OffsetMax = "-10 -20"
			};

			[JsonProperty(PropertyName = "Players Settings")]
			public InfoPanel Players = new InfoPanel
			{
				Enabled = true,
				Permission = string.Empty,
				Image = "https://i.ibb.co/qNGK5N8/image.png",
				SettingsImage = "https://i.ibb.co/yV50kdm/image.pngg",
				Settings = new PanelSettings(true, string.Empty, true, false, true),
				Size = new InfoPanel.InfoPanelSizeSettings(0, new List<SizeData>(), new List<InfoPanel.InfoPanelSize>
				{
					new InfoPanel.InfoPanelSize("sizeBig", string.Empty, 35, 12, 16, 40, 145),
					new InfoPanel.InfoPanelSize("sizeMedium", string.Empty, 30, 10, 14, 40, 145),
					new InfoPanel.InfoPanelSize("sizeSmall", string.Empty, 25, 8, 12, 40, 145)
				})
			};

			[JsonProperty(PropertyName = "Sleepers Settings")]
			public InfoPanel Sleepers = new InfoPanel
			{
				Enabled = false,
				Permission = string.Empty,
				Image = "https://i.ibb.co/gWBfDdk/image.png",
				SettingsImage = "https://i.ibb.co/GsNbkxM/84eMeAq.png",
				Settings = new PanelSettings(true, string.Empty, true, false, true),
				Size = new InfoPanel.InfoPanelSizeSettings(0, new List<SizeData>(), new List<InfoPanel.InfoPanelSize>
				{
					new InfoPanel.InfoPanelSize("sizeBig", string.Empty, 35, 12, 16, 40, 145),
					new InfoPanel.InfoPanelSize("sizeMedium", string.Empty, 30, 10, 14, 40, 145),
					new InfoPanel.InfoPanelSize("sizeSmall", string.Empty, 25, 8, 12, 40, 145)
				})
			};

			[JsonProperty(PropertyName = "Time Settings")]
			public TimeSettings Time = new TimeSettings
			{
				Enabled = true,
				GameUpdate = false,
				Permission = string.Empty,
				Image = "https://i.ibb.co/yn5SRJQ/image.png",
				SettingsImage = "https://i.ibb.co/qFvCYt1/image.png",
				Settings = new PanelSettings(true, string.Empty, true, false, true),
				Size = new InfoPanel.InfoPanelSizeSettings(0, new List<SizeData>(), new List<InfoPanel.InfoPanelSize>
				{
					new InfoPanel.InfoPanelSize("sizeBig", string.Empty, 35, 12, 16, 50, 145),
					new InfoPanel.InfoPanelSize("sizeMedium", string.Empty, 30, 10, 14, 50, 145),
					new InfoPanel.InfoPanelSize("sizeSmall", string.Empty, 25, 8, 12, 50, 145)
				})
			};

			[JsonProperty(PropertyName = "Settings Button")]
			public SettingsInfo Settings = new SettingsInfo
			{
				Enabled = true,
				Permission = string.Empty,
				Image = "https://i.ibb.co/v1C2M8v/image.png",
				Color = new IColor("#4B68FF"),
				AnchorMin = "0 0", AnchorMax = "1 1",
				OffsetMin = "20 -60", OffsetMax = "55 -25"
			};

			[JsonProperty(PropertyName = "Buttons Setting")]
			public ButtonsSettings ButtonsSettings = new ButtonsSettings
			{
				Enabled = true,
				Permission = string.Empty,
				Commands = new[] {"panelsystem.buttons"},
				Buttons = new List<ButtonInfo>
				{
					new ButtonInfo
					{
						Enabled = true,
						Image = "https://i.ibb.co/K0JxnHR/image.png",
						Permission = string.Empty,
						LangKey = "BtnShop",
						Command = "chat.say /shop"
					},
					new ButtonInfo
					{
						Enabled = true,
						Image = "https://i.ibb.co/MPsG1db/image.png",
						Permission = string.Empty,
						LangKey = "BtnStats",
						Command = "chat.say /stats"
					},
					new ButtonInfo
					{
						Enabled = true,
						Image = "https://i.ibb.co/5BjZ3FC/image.png",
						Permission = string.Empty,
						LangKey = "BtnBank",
						Command = "chat.say /bank"
					}
				},
				SettingsImage = "https://i.ibb.co/g4s352m/image.png",
				Settings = new PanelSettings(true, string.Empty, true, false, true),
				Size = new ButtonsSizeSettings(0, new List<ButtonsSizeSettings.ButtonPanelSize>
				{
					new ButtonsSizeSettings.ButtonPanelSize("sizeBig", string.Empty, 35, 16, 205, 25, 5),
					new ButtonsSizeSettings.ButtonPanelSize("sizeMedium", string.Empty, 30, 14, 205, 25, 2.5f),
					new ButtonsSizeSettings.ButtonPanelSize("sizeSmall", string.Empty, 25, 12, 205, 25, 0)
				})
			};

			[JsonProperty(PropertyName = "Events", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<EventConf> Events = new List<EventConf>
			{
				new EventConf(true, "https://i.ibb.co/q5T7kqB/image.png", string.Empty, PanelType.CH47,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"CH47"),
				new EventConf(true, "https://i.ibb.co/V3cYD3B/image.png", string.Empty, PanelType.Bradley,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(2, 3),
					"Bradley"),
				new EventConf(true, "https://i.ibb.co/gtB5Cny/image.png", string.Empty, PanelType.Helicopter,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(3, 2),
					"Helicopter"),
				new EventConf(true, "https://i.ibb.co/TP6yc3g/image.png", string.Empty, PanelType.Airdrop,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"Airdrop"),
				new EventConf(false, "https://i.ibb.co/YLhCDR3/image.png", string.Empty, PanelType.CargoShip,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"CargoShip"),
				new EventConf(false, "https://i.ibb.co/Bczwjr8/image.png", string.Empty, PanelType.Convoy,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"Convoy"),
				new EventConf(false, "https://i.ibb.co/VNLXW5T/image.png", string.Empty, PanelType.ArmoredTrain,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"ArmoredTrain"),
				new EventConf(false, "https://i.ibb.co/3FPXDdv/image.png", string.Empty, PanelType.WipeBlock,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"WipeBlock"),
				new EventConf(false, "https://i.ibb.co/BBgsgfr/image.png", string.Empty, PanelType.Sputnik,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"Sputnik"),
				new EventConf(false, "https://i.ibb.co/f12gzx2/image.png", string.Empty, PanelType.SpaceEvent,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"SpaceEvent"),
				new EventConf(false, "https://i.ibb.co/hszCXVq/image.png", string.Empty, PanelType.AirEvent,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"AirEvent"),
				new EventConf(false, "https://i.ibb.co/zZB82J6/image.png", string.Empty, PanelType.Custom,
					new PanelSettings(true, string.Empty, true, true, true),
					new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
					{
						new EventSizeData("sizeBig", string.Empty, 25f),
						new EventSizeData("sizeMedium", string.Empty, 20f),
						new EventSizeData("sizeSmall", string.Empty, 15f)
					}),
					new ColorInfo(0, 2),
					"UserNotify")
			};

			[JsonProperty(PropertyName = "Economics", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<EconomyConf> Economics = new List<EconomyConf>
			{
				new EconomyConf(1, true, "https://i.ibb.co/x2Kfd20/image.png", "Economics", "Balance",
					new PanelSettings(true, string.Empty, true, true, true),
					new EconomyConf.EconomySizeSettings(0, new List<SizeData>(), new List<EconomyConf.EconomySize>
					{
						new EconomyConf.EconomySize("sizeBig", string.Empty, 20),
						new EconomyConf.EconomySize("sizeMedium", string.Empty, 18),
						new EconomyConf.EconomySize("sizeSmall", string.Empty, 16)
					}),
					6, string.Empty),
				new EconomyConf(2, true, "https://i.ibb.co/3yYQk8X/image.png", "ServerRewards", "CheckPoints",
					new PanelSettings(true, string.Empty, true, true, true),
					new EconomyConf.EconomySizeSettings(0, new List<SizeData>(), new List<EconomyConf.EconomySize>
					{
						new EconomyConf.EconomySize("sizeBig", string.Empty, 20),
						new EconomyConf.EconomySize("sizeMedium", string.Empty, 18),
						new EconomyConf.EconomySize("sizeSmall", string.Empty, 16)
					}),
					7, string.Empty)
			};

			[JsonProperty(PropertyName = "Settings Interface")]
			public SettingsInterface SettingsInterface = new SettingsInterface
			{
				IconsOnString = 4,
				Size = 50,
				Margin = 5
			};

			[JsonProperty(PropertyName = "Events Interface")]
			public EventsInterface EventsInterface = new EventsInterface
			{
				SideIndent = 56,
				UpIndent = 25,
				Size = 35
			};

			[JsonProperty(PropertyName = "Economics Interface")]
			public EconomicsInterface EconomicsInterface = new EconomicsInterface
			{
				SideIndent = 25,
				UpIndent = 110f,
				Margin = 5,
				Size = 5
			};

			[JsonProperty(PropertyName = "Hide Settings")]
			public HideSettings Hide = new HideSettings
			{
				Enabled = false,
				Command = "panel",
				ShowLogo = true
			};

			[JsonProperty(PropertyName = "Colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<IColor> Colors = new List<IColor>
			{
				new IColor("#FFFFFF"),
				new IColor("#000000"),
				new IColor("#FFFFFF", 60),
				new IColor("#FF6060"),
				new IColor("#F6003B"),
				new IColor("#0FF542"),
				new IColor("#DCDCDC"),
				new IColor("#4B68FF"),
				new IColor("#0FF542"),
				new IColor("#F68E00"),
				new IColor("#F68E00"),
				new IColor("#F6003B"),
				new IColor("#F68E00"),
				new IColor("#4B68FF"),
				new IColor("#F68E00"),
				new IColor("#0FF542"),
				new IColor("#F68E00"),
				new IColor("#0FF542"),
				new IColor("#F68E00"),
				new IColor("#F6003B"),
				new IColor("#F6003B"),
				new IColor("#F68E00"),
				new IColor("#F68E00"),
				new IColor("#0FF542"),
				new IColor("#4B68FF")
			};

			public VersionNumber Version;

			#region Utils

			public KeyValuePair<string, string> GetPanelPosition()
			{
				switch (PanelAnchor)
				{
					case PanelAnchor.UpperLeft:
						return new KeyValuePair<string, string>("0 1", "0 1");
					default: //UpperRight
						return new KeyValuePair<string, string>("1 1", "1 1");
				}
			}

			public string GetStartPanelValue()
			{
				switch (PanelAnchor)
				{
					case PanelAnchor.UpperLeft:
						return "";
					case PanelAnchor.UpperRight:
						return "-";
					default:
						return "";
				}
			}

			#endregion
		}

		private enum PanelAnchor
		{
			UpperLeft,
			UpperRight
		}

		private class HideSettings
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

			[JsonProperty(PropertyName = "Show Logo?")]
			public bool ShowLogo;
		}

		private class EconomicsInterface
		{
			[JsonProperty(PropertyName = "Side Indent")]
			public float SideIndent;

			[JsonProperty(PropertyName = "Up Indent")]
			public float UpIndent;

			[JsonProperty(PropertyName = "Margin")]
			public float Margin;

			[JsonProperty(PropertyName = "Size")] public float Size;
		}

		private class EventsInterface
		{
			[JsonProperty(PropertyName = "Side Indent")]
			public float SideIndent;

			[JsonProperty(PropertyName = "Up Indent")]
			public float UpIndent;

			[JsonProperty(PropertyName = "Size")] public float Size;
		}

		private class SettingsInterface
		{
			[JsonProperty(PropertyName = "Icons On String")]
			public int IconsOnString;

			[JsonProperty(PropertyName = "Size")] public float Size;

			[JsonProperty(PropertyName = "Margin")]
			public float Margin;
		}

		private abstract class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;
		}

		private class LogoSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

			public bool HasPermission(BasePlayer player)
			{
				return string.IsNullOrEmpty(Permission) || player.IPlayer.HasPermission(Permission);
			}
		}

		private class ButtonsSettings
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Commands for hiding/unhiding buttons",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands;

			[JsonProperty(PropertyName = "Buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ButtonInfo> Buttons = new List<ButtonInfo>();

			[JsonProperty(PropertyName = "Settings Image")]
			public string SettingsImage;

			[JsonProperty(PropertyName = "Panel Settngs")]
			public PanelSettings Settings;

			[JsonProperty(PropertyName = "Size Settings")]
			public ButtonsSizeSettings Size;

			public ButtonsSizeSettings.ButtonPanelSize GetPlayerSize(BasePlayer player, PanelType type)
			{
				var sizes = Size.Sizes;

				int sizeIndex;
				return PlayerData.GetOrAdd(player).Sizes.TryGetValue((int) type, out sizeIndex) &&
				       sizeIndex >= 0 &&
				       sizeIndex < sizes.Count
					? sizes[sizeIndex]
					: sizes[Size.DefaultSize];
			}

			public bool HasPermission(BasePlayer player)
			{
				return string.IsNullOrEmpty(Permission) || player.IPlayer.HasPermission(Permission);
			}
		}

		private class ButtonInfo
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Lang Key (oxide/lang/**/PanelSystem.json)")]
			public string LangKey;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

			[JsonIgnore] private int _id = -1;

			[JsonIgnore]
			public int ID
			{
				get
				{
					if (_id == -1)
					{
						_id = Random.Range(0, int.MaxValue);

						_instance._buttonByID[_id] = this;
					}

					return _id;
				}
			}

			public static ButtonInfo Get(int id)
			{
				ButtonInfo btn;
				return _instance._buttonByID.TryGetValue(id, out btn) ? btn : null;
			}
		}

		private class ButtonsSizeSettings : SizeSettings
		{
			public class ButtonPanelSize : SizeData
			{
				[JsonProperty(PropertyName = "Image Size")]
				public float ImageSize;

				[JsonProperty(PropertyName = "Font Size")]
				public int FontSize;

				[JsonProperty(PropertyName = "Up Indent")]
				public float UpIndent;

				[JsonProperty(PropertyName = "Side Indent")]
				public int SideIndent;

				[JsonProperty(PropertyName = "Margin")]
				public float Margin;

				public ButtonPanelSize(string langKey, string permission, float imageSize, int fontSize, float upIndent,
					int sideIndent, float margin) : base(langKey, permission)
				{
					ImageSize = imageSize;
					FontSize = fontSize;
					UpIndent = upIndent;
					SideIndent = sideIndent;
					Margin = margin;
				}
			}

			[JsonProperty(PropertyName = "Sizes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ButtonPanelSize> Sizes;

			[JsonConstructor]
			public ButtonsSizeSettings()
			{
			}

			public ButtonsSizeSettings(int defaultSize, List<ButtonPanelSize> sizes2) : base(
				defaultSize)
			{
				Sizes = sizes2;
			}
		}

		private class SettingsInfo : InterfacePosition
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Color")] public IColor Color;

			public bool HasPermission(BasePlayer player)
			{
				return string.IsNullOrEmpty(Permission) || player.IPlayer.HasPermission(Permission);
			}
		}

		private class TimeSettings : InfoPanel
		{
			[JsonProperty(PropertyName = "Updating the time every minute of game time (may cause performance issues)")]
			public bool GameUpdate;
		}

		private class InfoPanel
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Settings Image")]
			public string SettingsImage;

			[JsonProperty(PropertyName = "Panel Settngs")]
			public PanelSettings Settings;

			[JsonProperty(PropertyName = "Size Settings")]
			public InfoPanelSizeSettings Size;

			public InfoPanelSize GetPlayerSize(BasePlayer player, PanelType type)
			{
				var sizes = Size.Sizes;

				int sizeIndex;
				return PlayerData.GetOrAdd(player).Sizes.TryGetValue((int) type, out sizeIndex) &&
				       sizeIndex >= 0 &&
				       sizeIndex < sizes.Count
					? sizes[sizeIndex]
					: sizes[Size.DefaultSize];
			}

			public bool HasPermission(BasePlayer player)
			{
				return string.IsNullOrEmpty(Permission) || player.IPlayer.HasPermission(Permission);
			}

			public class InfoPanelSizeSettings : SizeSettings
			{
				[JsonProperty(PropertyName = "Sizes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<InfoPanelSize> Sizes;

				[JsonConstructor]
				public InfoPanelSizeSettings()
				{
				}

				public InfoPanelSizeSettings(int defaultSize, List<SizeData> sizes, List<InfoPanelSize> sizes2) : base(
					defaultSize)
				{
					Sizes = sizes2;
				}
			}

			public class InfoPanelSize : SizeData
			{
				[JsonProperty(PropertyName = "Image Size")]
				public float ImageSize;

				[JsonProperty(PropertyName = "Title Font Size")]
				public int TitleFontSize;

				[JsonProperty(PropertyName = "Value Font Size")]
				public int ValueFontSize;

				[JsonProperty(PropertyName = "Up Indent")]
				public int UpIndent;

				[JsonProperty(PropertyName = "Side Indent")]
				public int SideIndent;

				public InfoPanelSize(string langKey, string permission, float imageSize, int titleFontSize,
					int valueFontSize, int upIndent, int sideIndent) : base(langKey, permission)
				{
					ImageSize = imageSize;
					TitleFontSize = titleFontSize;
					ValueFontSize = valueFontSize;
					UpIndent = upIndent;
					SideIndent = sideIndent;
				}
			}
		}

		private class PanelSettings
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Switch")]
			public bool Switch;

			[JsonProperty(PropertyName = "Color")] public bool Color;

			[JsonProperty(PropertyName = "Size")] public bool Size;

			[JsonIgnore] public PanelType Type;

			[JsonIgnore] public int ID;

			[JsonIgnore] public string Image;

			[JsonIgnore] public string Key;

			[JsonIgnore] public List<SizeData> Sizes;

			public PanelSettings(bool enabled, string permission,
				bool @switch, bool color, bool size)
			{
				Enabled = enabled;
				Permission = permission;
				Switch = @switch;
				Color = color;
				Size = size;
			}

			public List<SizeData> GetSizes(BasePlayer player)
			{
				return Sizes.FindAll(x =>
					string.IsNullOrEmpty(x.Permission) ||
					_instance.permission.UserHasPermission(player.UserIDString, x.Permission));
			}
		}

		private class SizeSettings
		{
			[JsonProperty(PropertyName = "Default Size (index)")]
			public int DefaultSize;

			[JsonConstructor]
			protected SizeSettings()
			{
			}

			protected SizeSettings(int defaultSize)
			{
				DefaultSize = defaultSize;
			}
		}

		private class SizeData
		{
			[JsonProperty(PropertyName = "Lang Key")]
			public string LangKey;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			protected SizeData(string langKey, string permission)
			{
				LangKey = langKey;
				Permission = permission;
			}
		}

		private class EconomyConf
		{
			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Plugin name")]
			public string Plug;

			[JsonProperty(PropertyName = "Balance show hook")]
			public string BalanceHook;

			[JsonProperty(PropertyName = "Panel Settngs")]
			public PanelSettings Settings;

			[JsonProperty(PropertyName = "Size Settings")]
			public EconomySizeSettings Size;

			[JsonProperty(PropertyName = "Default Color ID")]
			public int ColorId;

			public string GetColor()
			{
				return (ColorId >= 0 && ColorId < _instance._config.Colors.Count
					? _instance._config.Colors[ColorId]
					: _instance._config.Colors[0]).Get;
			}

			public double ShowBalance(BasePlayer player)
			{
				var plugin = _instance?.plugins?.Find(Plug);
				if (plugin == null) return 0;

				return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.userID)), 2);
			}

			public bool HasPermission(BasePlayer player)
			{
				return string.IsNullOrEmpty(Permission) || player.IPlayer.HasPermission(Permission);
			}

			public EconomyConf(int id, bool enabled, string image, string plug, string balanceHook,
				PanelSettings settings, EconomySizeSettings size, int colorId, string permission)
			{
				ID = id;
				Enabled = enabled;
				Image = image;
				Plug = plug;
				BalanceHook = balanceHook;
				Settings = settings;
				Size = size;
				ColorId = colorId;
				Permission = permission;
			}

			public EconomySize GetPlayerSize(BasePlayer player)
			{
				var sizes = Size.Sizes;

				int sizeIndex;
				return PlayerData.GetOrAdd(player).EconomySizes.TryGetValue(ID, out sizeIndex) &&
				       sizeIndex >= 0 &&
				       sizeIndex < sizes.Count
					? sizes[sizeIndex]
					: sizes[Size.DefaultSize];
			}

			public class EconomySizeSettings : SizeSettings
			{
				[JsonProperty(PropertyName = "Sizes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<EconomySize> Sizes;

				[JsonConstructor]
				public EconomySizeSettings()
				{
				}

				public EconomySizeSettings(int defaultSize, List<SizeData> sizes, List<EconomySize> sizes2) : base(
					defaultSize)
				{
					Sizes = sizes2;
				}
			}

			public class EconomySize : SizeData
			{
				[JsonProperty(PropertyName = "Font Size")]
				public int FontSize;

				public EconomySize(string langKey, string permission, int fontSize) : base(langKey, permission)
				{
					FontSize = fontSize;
				}
			}
		}

		private class EventConf
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public PanelType Type;

			[JsonProperty(PropertyName = "Panel Settings")]
			public PanelSettings Settings;

			[JsonProperty(PropertyName = "Size Settings")]
			public EventSizesInfo Size;

			[JsonProperty(PropertyName = "Color Settings")]
			public ColorInfo Color;

			[JsonProperty(PropertyName = "Key (MUST BE UNIQUE)")]
			public string Key;

			public string GetColor(BasePlayer player, bool status)
			{
				var data = PlayerData.GetOrAdd(player).GetColorData(Type, 0, Key);
				var colorId = status ? data.ActiveId : data.InactiveId;

				return _instance._config.Colors[Mathf.Min(colorId, _instance._config.Colors.Count - 1)].Get;
			}

			public EventConf(bool enabled, string image, string permission, PanelType type,
				PanelSettings settings,
				EventSizesInfo size,
				ColorInfo color, string key)
			{
				Enabled = enabled;
				Image = image;
				Permission = permission;
				Type = type;
				Settings = settings;
				Size = size;
				Color = color;
				Key = key;
			}

			public EventSizeData GetPlayerSize(BasePlayer player)
			{
				var sizes = Size.Sizes;

				int sizeIndex;
				switch (Type)
				{
					case PanelType.Custom:
					{
						return PlayerData.GetOrAdd(player).CustomSizes.TryGetValue(Key, out sizeIndex) &&
						       sizeIndex >= 0 &&
						       sizeIndex < sizes.Count
							? sizes[sizeIndex]
							: sizes[Size.DefaultSize];
					}
					default:
					{
						return PlayerData.GetOrAdd(player).Sizes.TryGetValue((int) Type, out sizeIndex) &&
						       sizeIndex >= 0 &&
						       sizeIndex < sizes.Count
							? sizes[sizeIndex]
							: sizes[Size.DefaultSize];
					}
				}
			}

			public bool HasPermission(BasePlayer player)
			{
				return string.IsNullOrEmpty(Permission) || player.IPlayer.HasPermission(Permission);
			}
		}

		private class ColorInfo
		{
			[JsonProperty(PropertyName = "Default Active Color ID")]
			public int DefaultActiveColorId;

			[JsonProperty(PropertyName = "Default Inactive Color ID")]
			public int DefaultInactiveColorId;

			public ColorInfo(int defaultActiveColorId, int defaultInactiveColorId)
			{
				DefaultActiveColorId = defaultActiveColorId;
				DefaultInactiveColorId = defaultInactiveColorId;
			}
		}

		private class EventSizesInfo : SizeSettings
		{
			[JsonProperty(PropertyName = "Sizes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<EventSizeData> Sizes;

			[JsonConstructor]
			public EventSizesInfo()
			{
			}

			public EventSizesInfo(int defaultSize, List<SizeData> sizes, List<EventSizeData> sizes2) : base(defaultSize)
			{
				Sizes = sizes2;
			}
		}

		private class EventSizeData : SizeData
		{
			[JsonProperty(PropertyName = "Size")] public float Size;

			public EventSizeData(string langKey, string permission, float size) : base(langKey, permission)
			{
				Size = size;
			}
		}

		private class IColor
		{
			[JsonProperty(PropertyName = "HEX")] public string Hex;

			[JsonProperty(PropertyName = "Opacity (0 - 100)")]
			public readonly float Alpha;

			[JsonIgnore] private string _color;

			[JsonIgnore]
			public string Get
			{
				get
				{
					if (string.IsNullOrEmpty(_color))
						_color = GetColor();

					return _color;
				}
			}

			private string GetColor()
			{
				if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

				var str = Hex.Trim('#');
				if (str.Length != 6) throw new Exception(Hex);
				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
			}

			public IColor()
			{
			}

			public IColor(string hex, float alpha = 100)
			{
				Hex = hex;
				Alpha = alpha;
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config != null)
				{
					if (_config.Version < Version)
						UpdateConfigValues();

					SaveConfig();
				}
				else
				{
					throw new Exception();
				}
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

		private void UpdateConfigValues()
		{
			PrintWarning("Config update detected! Updating config values...");

			//var baseConfig = new Configuration();

			if (_config.Version != default(VersionNumber))
			{
				if (_config.Version < new VersionNumber(1, 1, 1))
				{
					_config.Events.Add(new EventConf(false, "https://i.imgur.com/CukDURt.png", string.Empty,
						PanelType.Sputnik,
						new PanelSettings(true, string.Empty, true, true, true),
						new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
						{
							new EventSizeData("sizeBig", string.Empty, 25f),
							new EventSizeData("sizeMedium", string.Empty, 20f),
							new EventSizeData("sizeSmall", string.Empty, 15f)
						}),
						new ColorInfo(0, 2),
						"Sputnik"));

					_config.Events.Add(new EventConf(false, "https://i.imgur.com/p4rxub9.png", string.Empty,
						PanelType.SpaceEvent,
						new PanelSettings(true, string.Empty, true, true, true),
						new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
						{
							new EventSizeData("sizeBig", string.Empty, 25f),
							new EventSizeData("sizeMedium", string.Empty, 20f),
							new EventSizeData("sizeSmall", string.Empty, 15f)
						}),
						new ColorInfo(0, 2),
						"SpaceEvent"));
				}

				if (_config.Version < new VersionNumber(1, 1, 3))
					_config.Events.Add(new EventConf(false, "https://i.imgur.com/mm8nhlg.png", string.Empty,
						PanelType.AirEvent,
						new PanelSettings(true, string.Empty, true, true, true),
						new EventSizesInfo(0, new List<SizeData>(), new List<EventSizeData>
						{
							new EventSizeData("sizeBig", string.Empty, 25f),
							new EventSizeData("sizeMedium", string.Empty, 20f),
							new EventSizeData("sizeSmall", string.Empty, 15f)
						}),
						new ColorInfo(0, 2),
						"AirEvent"));
			}

			_config.Version = Version;

			PrintWarning("Config update completed!");
		}

		#endregion

		#region Data

		private PluginData _data;

		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Main", _data);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Main");
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
			#region Mode

			[JsonProperty(PropertyName = "Hided")] public bool Hided;

			public bool IsHided()
			{
				return Hided;
			}

			public void ChangeHide(bool newHide)
			{
				Hided = newHide;
			}

			#endregion

			#region Sizes

			[JsonProperty(PropertyName = "Sizes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> Sizes = new Dictionary<int, int>(); //type - size index

			[JsonProperty(PropertyName = "Economy Sizes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> EconomySizes = new Dictionary<int, int>(); //type - size index

			[JsonProperty(PropertyName = "Custom Sizes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> CustomSizes = new Dictionary<string, int>(); //key - size index

			public void SetSize(PanelType type, int id, string key, int index)
			{
				switch (type)
				{
					case PanelType.Economics:
					{
						EconomySizes[id] = index;
						break;
					}
					case PanelType.Custom:
					{
						if (string.IsNullOrEmpty(key)) return;

						CustomSizes[key] = index;
						break;
					}
					default:
					{
						Sizes[(int) type] = index;
						break;
					}
				}
			}

			public SizeData GetSize(BasePlayer player, PanelType type, int id, string key)
			{
				switch (type)
				{
					case PanelType.Economics:
						return _instance._config.Economics.Find(x => x.ID == id)?.GetPlayerSize(player);

					case PanelType.Online:
						return _instance._config.Players.GetPlayerSize(player, type);

					case PanelType.Sleepers:
						return _instance._config.Sleepers.GetPlayerSize(player, type);

					case PanelType.Time:
						return _instance._config.Time.GetPlayerSize(player, type);

					case PanelType.Button:
						return _instance._config.ButtonsSettings.GetPlayerSize(player, type);

					case PanelType.Custom:
						return _instance._config.Events.Find(x => x.Type == type && x.Key == key)
							?.GetPlayerSize(player);

					default: //events
						return _instance._config.Events.Find(x => x.Type == type)?.GetPlayerSize(player);
				}
			}

			#endregion

			#region Colors

			[JsonProperty(PropertyName = "Events Colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, ColorData> EventsColors = new Dictionary<int, ColorData>();

			[JsonProperty(PropertyName = "Custom Colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, ColorData> CustomColors = new Dictionary<string, ColorData>();

			[JsonProperty(PropertyName = "Economics Colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, ColorData> EconomicsColors = new Dictionary<int, ColorData>();

			public ColorData GetColorData(PanelType type, int id, string key)
			{
				switch (type)
				{
					case PanelType.Custom:
					{
						ColorData colorData;
						if (!CustomColors.TryGetValue(key, out colorData))
							CustomColors.Add(key, colorData = new ColorData(type, key));
						return colorData;
					}
					case PanelType.Economics:
					{
						ColorData colorData;
						if (!EconomicsColors.TryGetValue(id, out colorData))
							EconomicsColors.Add(id, colorData = new ColorData(id));

						return colorData;
					}
					default:
					{
						ColorData colorData;
						if (!EventsColors.TryGetValue((int) type, out colorData))
							EventsColors.Add((int) type, colorData = new ColorData(type, key));
						return colorData;
					}
				}
			}

			public void SetEventColor(PanelType type, int id, string key, int colorPage, int index)
			{
				var colorData = GetColorData(type, id, key);

				switch (type)
				{
					case PanelType.Economics:
					{
						colorData.ActiveId = index;
						break;
					}
					default:
					{
						switch (colorPage)
						{
							case 0:
							{
								colorData.ActiveId = index;
								break;
							}
							case 1:
							{
								colorData.InactiveId = index;
								break;
							}
						}

						break;
					}
				}
			}

			#endregion

			#region Hidden Panels

			[JsonProperty(PropertyName = "Hidden Panel", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<int> HiddenPanel = new List<int>();

			[JsonProperty(PropertyName = "Hidden Economy", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<int> HiddenEconomy = new List<int>();

			public void SwitchHidden(PanelType type, int id)
			{
				switch (type)
				{
					case PanelType.Economics:
					{
						if (HiddenEconomy.Contains(id))
							HiddenEconomy.Remove(id);
						else
							HiddenEconomy.Add(id);
						break;
					}
					default:
					{
						var typeId = (int) type;

						if (HiddenPanel.Contains(typeId))
							HiddenPanel.Remove(typeId);
						else
							HiddenPanel.Add(typeId);
						break;
					}
				}
			}

			public bool IsHidden(PanelType type, int id = 0)
			{
				switch (type)
				{
					case PanelType.Economics:
						return HiddenEconomy.Contains(id);
					default:
						return HiddenPanel.Contains((int) type);
				}
			}

			#endregion

			public static PlayerData GetOrAdd(BasePlayer player)
			{
				_instance._data.Players.TryAdd(player.userID, new PlayerData());

				return _instance._data.Players[player.userID];
			}
		}

		private class ColorData
		{
			[JsonProperty(PropertyName = "Active ID")]
			public int ActiveId;

			[JsonProperty(PropertyName = "Inactive ID")]
			public int InactiveId;

			[JsonConstructor]
			public ColorData(int activeId, int inactiveId)
			{
				ActiveId = activeId;
				InactiveId = inactiveId;
			}

			public ColorData(PanelType type, string key)
			{
				var mainColor = _instance._config.Events.Find(x => x.Type == type && x.Key == key)?.Color;

				ActiveId = mainColor?.DefaultActiveColorId ?? 0;
				InactiveId = mainColor?.DefaultInactiveColorId ?? 0;
			}

			public ColorData(int id)
			{
				var mainColor = _instance._config.Economics.Find(x => x.ID == id)?.ColorId;

				ActiveId = mainColor ?? 0;
				InactiveId = mainColor ?? 0;
			}
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadData();

			RegisterPermissions();

			RegisterCommands();
		}

		private void OnServerInitialized()
		{
			LoadSettings();

			LoadImages();

			LoadEvents();

			foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);

			timer.Every(5, StartUpdate);

			if (_config.Time.Enabled && _config.Time.GameUpdate)
				GetTimeComponent();
		}

		private void OnServerSave()
		{
			timer.In(Random.Range(2f, 7f), SaveData);
		}

		private void Unload()
		{
			SaveData();

			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, EventsLayer);
				CuiHelper.DestroyUi(player, SettingsLayer);
			}

			if (_config.Time.Enabled && _config.Time.GameUpdate &&
			    timeComponent != null)
				timeComponent.OnMinute -= OnMinute;

			_instance = null;
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			MainUi(player, true);
			EventsUi(player, true);
		}

		#region Events

		#region Sputnik

		private void OnSputnikEventStart()
		{
			OnEventChangeStatus(PanelType.Sputnik, true);
		}

		private void OnSputnikEventStop()
		{
			OnEventChangeStatus(PanelType.Sputnik, false);
		}

		#endregion

		#region Space

		private void OnSpaceEventStart()
		{
			OnEventChangeStatus(PanelType.SpaceEvent, true);
		}

		private void OnSpaceEventStop()
		{
			OnEventChangeStatus(PanelType.SpaceEvent, false);
		}

		#endregion

		#region Convoy

		private void OnConvoyStart()
		{
			OnEventChangeStatus(PanelType.Convoy, true);
		}

		private void OnConvoyStop()
		{
			OnEventChangeStatus(PanelType.Convoy, false);
		}

		#endregion

		#region Armored Train

		private void OnArmoredTrainEventStart()
		{
			OnEventChangeStatus(PanelType.ArmoredTrain, true);
		}

		private void OnArmoredTrainEventStop()
		{
			OnEventChangeStatus(PanelType.ArmoredTrain, false);
		}

		#endregion

		#region WipeBlock

		private void OnWipeBlockEnded()
		{
			OnEventChangeStatus(PanelType.WipeBlock, false);
		}

		#endregion

		#region AirEvent

		private void OnAirEventStart(HashSet<BaseEntity> entities)
		{
			OnEventChangeStatus(PanelType.AirEvent, true);
		}

		private void OnAirEventEnd()
		{
			OnEventChangeStatus(PanelType.AirEvent, false);
		}

		#endregion

		private void OnEntitySpawned(BaseEntity entity)
		{
			OnEntityChangeStatus(entity, true);
		}

		private void OnEntityKill(BaseEntity entity)
		{
			OnEntityChangeStatus(entity, false);
		}

		#endregion

		#region Image Library

		private void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary") _enabledImageLibrary = true;
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary") _enabledImageLibrary = false;
		}

		#endregion

		#endregion

		#region Commands

		[ConsoleCommand("UI_PanelSystem")]
		private void CmdConsole(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "settings":
				{
					int panelId = -1, id = 0, page = 0, colorPage = -1;
					if (arg.HasArgs(2))
						int.TryParse(arg.Args[1], out panelId);

					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out id);

					if (arg.HasArgs(4)) int.TryParse(arg.Args[3], out page);

					if (arg.HasArgs(5))
						int.TryParse(arg.Args[4], out colorPage);

					var key = string.Empty;
					if (arg.HasArgs(6))
						key = arg.Args[5];

					SettingsUi(player, panelId, id, page, colorPage, key);
					break;
				}

				case "setsize":
				{
					PanelType type;
					int panelId, id, page, newIndex;
					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[1], out panelId) ||
					    !int.TryParse(arg.Args[2], out id) ||
					    !int.TryParse(arg.Args[3], out page) ||
					    !Enum.TryParse(arg.Args[4], out type) ||
					    !int.TryParse(arg.Args[5], out newIndex))
						return;

					var key = string.Empty;
					if (arg.HasArgs(7))
						key = arg.Args[6];

					PlayerData.GetOrAdd(player).SetSize(type, id, key, newIndex);

					MainUi(player);
					EventsUi(player);

					SettingsUi(player, panelId, id, page);
					break;
				}

				case "setcolor":
				{
					PanelType type;
					int panelId, id, page, colorPage, selIndex;
					if (!arg.HasArgs(7) ||
					    !int.TryParse(arg.Args[1], out panelId) ||
					    !int.TryParse(arg.Args[2], out id) ||
					    !int.TryParse(arg.Args[3], out page) ||
					    !int.TryParse(arg.Args[4], out colorPage) ||
					    !Enum.TryParse(arg.Args[5], out type) ||
					    !int.TryParse(arg.Args[6], out selIndex))
						return;

					var key = string.Empty;
					if (arg.HasArgs(8))
						key = arg.Args[7];

					PlayerData.GetOrAdd(player).SetEventColor(type, id, key, colorPage, selIndex);

					MainUi(player);
					EventsUi(player);

					switch ((PanelType) panelId)
					{
						case PanelType.Economics:
						{
							SettingsUi(player, panelId, id);
							break;
						}
						default:
						{
							SettingsUi(player, panelId, id, page);
							break;
						}
					}

					break;
				}

				case "switch":
				{
					PanelType type;
					int panelId, id;
					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out panelId) ||
					    !int.TryParse(arg.Args[2], out id) ||
					    !Enum.TryParse(arg.Args[3], out type))
						return;

					PlayerData.GetOrAdd(player).SwitchHidden(type, id);

					MainUi(player);
					EventsUi(player);

					SettingsUi(player, panelId, id);
					break;
				}
			}
		}

		[ConsoleCommand("panelsystem.sendcmd")]
		private void CmdSendCommand(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !arg.HasArgs()) return;

			player.SendConsoleCommand(
				arg.Args[0] == _chatSay
					? $"{arg.Args[0]}  \" {string.Join(" ", arg.Args.ToList().GetRange(1, arg.Args.Length - 1))}\" 0"
					: string.Join(" ", arg.Args));
		}

		private void CmdSwitchButtons(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			PlayerData.GetOrAdd(player).SwitchHidden(PanelType.Button, 0);

			MainUi(player);
		}

		private void CmdHide(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (args.Length < 1)
			{
				Reply(player, $"Error syntax! Use: /{command} hide/show");
				return;
			}

			var data = PlayerData.GetOrAdd(player);

			switch (args[0].ToLower())
			{
				case "hide":
				{
					data.ChangeHide(true);
					break;
				}

				case "show":
				{
					data.ChangeHide(false);
					break;
				}

				default:
				{
					Reply(player, $"Error syntax! Use: /{command} hide/show");
					return;
				}
			}

			MainUi(player, true);
			EventsUi(player, true);
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, bool first = false)
		{
			var data = PlayerData.GetOrAdd(player);

			var hided = data.IsHided();

			var container = new CuiElementContainer();

			var startValue = _config.GetStartPanelValue();

			#region Background

			if (first)
			{
				var pos = _config.GetPanelPosition();

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = pos.Key, AnchorMax = pos.Value,
						OffsetMin = "0 0", OffsetMax = "0 0"
					},
					Image = {Color = "0 0 0 0"}
				}, _config.DisplayType, Layer + ".Background", Layer + ".Background");
			}

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 0"
				},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Background", Layer, Layer);

			#endregion

			#region Main

			#region Logo

			if (_enabledImageLibrary && ((hided && _config.Hide.ShowLogo) || hided == false) && _config.Logo.Enabled &&
			    _config.Logo.HasPermission(player))
			{
				container.Add(new CuiElement
				{
					Name = Layer + ".Logotype",
					Parent = Layer,
					Components =
					{
						new CuiRawImageComponent {Png = GetImage(_config.Logo.Image)},
						new CuiRectTransformComponent
						{
							AnchorMin = _config.Logo.AnchorMin,
							AnchorMax = _config.Logo.AnchorMax,
							OffsetMin = _config.Logo.OffsetMin,
							OffsetMax = _config.Logo.OffsetMax
						}
					}
				});

				if (!string.IsNullOrEmpty(_config.Logo.Command))
					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Command = $"panelsystem.sendcmd {_config.Logo.Command}",
							Color = "0 0 0 0"
						}
					}, Layer + ".Logotype");
			}

			#endregion

			#region Panels

			var heightSwitch = 0f;

			#region Players

			if (hided == false && _config.Players.Enabled && !data.IsHidden(PanelType.Online) &&
			    _config.Players.HasPermission(player))
			{
				var panel = _config.Players;

				var size = panel.GetPlayerSize(player, PanelType.Online);

				heightSwitch -= size.UpIndent;

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = $"{startValue}{size.SideIndent} {heightSwitch}",
							OffsetMax = $"{startValue}{size.SideIndent} {heightSwitch}"
						},
						Image = {Color = "0 0 0 0"}
					}, Layer, Layer + $".Update.{PanelType.Online}");

				container.AddRange(UpdateUi(player, PanelType.Online, true));
			}

			#endregion

			#region Sleepers

			if (hided == false && _config.Sleepers.Enabled && !data.IsHidden(PanelType.Sleepers) &&
			    _config.Sleepers.HasPermission(player))
			{
				var panel = _config.Sleepers;

				var size = panel.GetPlayerSize(player, PanelType.Sleepers);

				heightSwitch -= size.UpIndent;

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = $"{startValue}{size.SideIndent} {heightSwitch}",
							OffsetMax = $"{startValue}{size.SideIndent} {heightSwitch}"
						},
						Image = {Color = "0 0 0 0"}
					}, Layer, Layer + $".Update.{PanelType.Sleepers}");

				container.AddRange(UpdateUi(player, PanelType.Sleepers, true));
			}

			#endregion

			#region Time

			if (hided == false && _config.Time.Enabled && !data.IsHidden(PanelType.Time) &&
			    _config.Time.HasPermission(player))
			{
				var panel = _config.Time;

				var size = panel.GetPlayerSize(player, PanelType.Time);

				heightSwitch -= size.UpIndent;

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = $"{startValue}{size.SideIndent} {heightSwitch}",
							OffsetMax = $"{startValue}{size.SideIndent} {heightSwitch}"
						},
						Image = {Color = "0 0 0 0"}
					}, Layer, Layer + $".Update.{PanelType.Time}");

				container.AddRange(UpdateUi(player, PanelType.Time, true));
			}

			#endregion

			#endregion

			#region Economics

			if (hided == false)
				container.AddRange(UpdateUi(player, PanelType.Economics, true));

			#endregion

			#region Buttons

			if (hided == false && _config.ButtonsSettings.Enabled && _config.ButtonsSettings.HasPermission(player))
			{
				var size = _config.ButtonsSettings.GetPlayerSize(player, PanelType.Button);

				var ySwitch = -size.UpIndent;
				var height = size.ImageSize;

				var buttons = GetAvailableButtons(player);
				buttons.ForEach(btn =>
				{
					if (PlayerData.GetOrAdd(player).IsHidden(PanelType.Button)) return;

					var title = Msg(player, btn.LangKey);
					var fontSize = size.FontSize;
					var length = (title.Length + 1) * fontSize * 0.5f;

					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "1 1", AnchorMax = "1 1",
								OffsetMin = $"{startValue}{size.SideIndent} {ySwitch - height}",
								OffsetMax = $"{startValue}{size.SideIndent} {ySwitch}"
							},
							Image =
							{
								Color = "0 0 0 0"
							}
						}, Layer, Layer + $".Buttons.{btn.ID}");

					if (_enabledImageLibrary && !string.IsNullOrEmpty(btn.Image))
						container.Add(new CuiElement
						{
							Parent = Layer + $".Buttons.{btn.ID}",
							Components =
							{
								new CuiRawImageComponent
								{
									Png = GetImage(btn.Image)
								},
								new CuiRectTransformComponent
								{
									AnchorMin = "1 0", AnchorMax = "1 1",
									OffsetMin = $"-{height} 0",
									OffsetMax = "0 0"
								}
							}
						});

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "1 0", AnchorMax = "1 1",
								OffsetMin = $"-{height + 5 + length + fontSize + 5} 0",
								OffsetMax = $"-{height + 5} 0"
							},
							Text =
							{
								Text = title,
								Align = TextAnchor.MiddleRight,
								Font = "robotocondensed-regular.ttf",
								FontSize = fontSize,
								Color = "1 1 1 1"
							}
						}, Layer + $".Buttons.{btn.ID}");

					container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = $"-{height + 10 + length + fontSize + 5} 0",
								OffsetMax = "0 0"
							},
							Text =
							{
								Text = ""
							},
							Button =
							{
								Command = $"panelsystem.sendcmd {btn.Command}",
								Color = "0 0 0 0"
							}
						}, Layer + $".Buttons.{btn.ID}");

					ySwitch = ySwitch - height - size.Margin;
				});
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void EventsUi(BasePlayer player, bool first = false)
		{
			var data = PlayerData.GetOrAdd(player);

			var hided = data.IsHided();

			var container = new CuiElementContainer();

			#region Background

			if (first)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "0 0", OffsetMax = "0 0"
					},
					Image = {Color = "0 0 0 0"}
				}, _config.DisplayType, EventsLayer + ".Background", EventsLayer + ".Background");
			}

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image = {Color = "0 0 0 0"}
			}, EventsLayer + ".Background", EventsLayer, EventsLayer);

			#endregion

			#region Main

			#region Settings

			if (hided == false && _config.Settings.Enabled && _config.Settings.HasPermission(player))
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = _config.Settings.AnchorMin,
						AnchorMax = _config.Settings.AnchorMax,
						OffsetMin = _config.Settings.OffsetMin,
						OffsetMax = _config.Settings.OffsetMax
					},
					Image =
					{
						Color = _config.Settings.Color.Get
					}
				}, EventsLayer, EventsLayer + ".Settings");

				if (_enabledImageLibrary)
					container.Add(new CuiElement
					{
						Parent = EventsLayer + ".Settings",
						Components =
						{
							new CuiRawImageComponent
								{Png = GetImage(_config.Settings.Image)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "5 5", OffsetMax = "-5 -5"
							}
						}
					});

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = "UI_PanelSystem settings"
					}
				}, EventsLayer + ".Settings");
			}

			#endregion

			#region Events

			if (hided == false)
			{
				var events = GetAvailableEvents(player);

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin =
							$"{_config.EventsInterface.SideIndent} -{_config.EventsInterface.UpIndent + _config.EventsInterface.Size}",
						OffsetMax =
							$"{_config.EventsInterface.SideIndent + events.Count * _config.EventsInterface.Size} -{_config.EventsInterface.UpIndent}"
					},
					Image =
					{
						Color = HexToCuiColor("#000000", 50)
					}
				}, EventsLayer, EventsLayer + ".Events.Panel");

				var xSwitch = 0f;
				events.ForEach(check =>
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch} 0",
								OffsetMax = $"{xSwitch + _config.EventsInterface.Size} 0"
							},
							Image =
							{
								Color = "0 0 0 0"
							}
						}, EventsLayer + ".Events.Panel", EventsLayer + $".{check.Type}");

					container.AddRange(UpdateUi(player, check.Type, true));

					xSwitch += _config.EventsInterface.Size;
				});
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private CuiElementContainer UpdateUi(BasePlayer player, PanelType type, bool first = false)
		{
			var container = new CuiElementContainer();

			if (PlayerData.GetOrAdd(player).IsHided()) return container;

			switch (type)
			{
				case PanelType.Online:
				{
					var panel = _config.Players;

					var size = panel.GetPlayerSize(player, type);
					
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							},
							Image = {Color = "0 0 0 0"}
						}, Layer + $".Update.{type}", Layer + $".Update.{type}.Update",
						Layer + $".Update.{type}.Update");

					var title = Msg(player, OnlineTitle);
					var fontSize = size.TitleFontSize;
					var length = (title.Length + 1) * fontSize * 0.5f;

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0",
								OffsetMax = $"{length} {size.ValueFontSize + 5}"
							},
							Text =
							{
								Text = title,
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = fontSize,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Update.{type}.Update");

					title = $"{BasePlayer.activePlayerList.Count}";
					fontSize = size.ValueFontSize;
					length = (title.Length + 1) * fontSize * 0.5f;

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = $"0 -{size.ValueFontSize + 5}",
								OffsetMax = $"{length} 0"
							},
							Text =
							{
								Text = title,
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = fontSize,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Update.{type}.Update");

					#region Icon

					if (_enabledImageLibrary)
						container.Add(new CuiElement
						{
							Parent = Layer + $".Update.{type}.Update",
							Components =
							{
								new CuiRawImageComponent {Png = GetImage(panel.Image)},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin =
										$"-{5 + size.ImageSize} -{size.ImageSize / 2}",
									OffsetMax = $"-5 {size.ImageSize / 2}"
								}
							}
						});

					#endregion

					break;
				}

				case PanelType.Sleepers:
				{
					var panel = _config.Sleepers;

					var size = panel.GetPlayerSize(player, type);
					
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							},
							Image = {Color = "0 0 0 0"}
						}, Layer + $".Update.{type}", Layer + $".Update.{type}.Update", Layer + $".Update.{type}.Update");

					var title = Msg(player, SleepersTitle);
					var fontSize = size.TitleFontSize;
					var length = (title.Length + 1) * fontSize * 0.5f;

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0",
								OffsetMax = $"{length} {size.ValueFontSize + 5}"
							},
							Text =
							{
								Text = title,
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = fontSize,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Update.{type}.Update");

					title = $"{BasePlayer.sleepingPlayerList.Count}";
					fontSize = size.ValueFontSize;
					length = (title.Length + 1) * fontSize * 0.5f;

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = $"0 -{size.ValueFontSize + 5}",
								OffsetMax = $"{length} 0"
							},
							Text =
							{
								Text = title,
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = fontSize,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Update.{type}.Update");

					#region Icon

					if (_enabledImageLibrary)
						container.Add(new CuiElement
						{
							Parent = Layer + $".Update.{type}.Update",
							Components =
							{
								new CuiRawImageComponent {Png = GetImage(panel.Image)},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin =
										$"-{5 + size.ImageSize} -{size.ImageSize / 2}",
									OffsetMax = $"-5 {size.ImageSize / 2}"
								}
							}
						});

					#endregion
					
					break;
				}

				case PanelType.Time:
				{
					var panel = _config.Time;

					var size = panel.GetPlayerSize(player, type);

					container.Add(new CuiPanel
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Image = {Color = "0 0 0 0"}
						}, Layer + $".Update.{type}", 
						Layer + $".Update.{type}.Update",
						Layer + $".Update.{type}.Update");

					var title = Msg(player, TimeTitle);
					var fontSize = size.TitleFontSize;
					var length = (title.Length + 1) * fontSize * 0.5f;

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0",
								OffsetMax = $"{length} {size.ValueFontSize + 5}"
							},
							Text =
							{
								Text = title,
								Align = TextAnchor.LowerLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = fontSize,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Update.{type}.Update");

					title = $"{TOD_Sky.Instance.Cycle.DateTime:HH:mm}";
					fontSize = size.ValueFontSize;
					length = (title.Length + 1) * fontSize * 0.5f;

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = $"0 -{size.ValueFontSize + 5}",
								OffsetMax = $"{length} 0"
							},
							Text =
							{
								Text = title,
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = fontSize,
								Color = "1 1 1 0.5"
							}
						}, Layer + $".Update.{type}.Update");

					#region Icon

					if (_enabledImageLibrary)
						container.Add(new CuiElement
						{
							Parent = Layer + $".Update.{type}.Update",
							Components =
							{
								new CuiRawImageComponent {Png = GetImage(panel.Image)},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin =
										$"-{5 + size.ImageSize} -{size.ImageSize / 2}",
									OffsetMax = $"-5 {size.ImageSize / 2}"
								}
							}
						});

					#endregion

					break;
				}

				case PanelType.Economics:
				{
					var ySwitch = -_config.EconomicsInterface.UpIndent;
					var index = 1;

					GetAvailableEconomics(player).ForEach(economy =>
					{
						if (PlayerData.GetOrAdd(player).IsHidden(PanelType.Economics, economy.ID)) return;

						var fontSize = economy.GetPlayerSize(player).FontSize;

						var balance = economy.ShowBalance(player).ToString(CultureInfo.InvariantCulture);
						var length = (balance.Length + 1) * fontSize * 0.5f;

						var nowColor = PlayerData.GetOrAdd(player)
							.GetColorData(PanelType.Economics, economy.ID, null);

						container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin =
										$"{-length - fontSize - _config.EconomicsInterface.SideIndent} {ySwitch - fontSize - _config.EconomicsInterface.Size}",
									OffsetMax = $"-{_config.EconomicsInterface.SideIndent} {ySwitch}"
								},
								Text =
								{
									Text = balance,
									Align = TextAnchor.MiddleRight,
									Font = "robotocondensed-bold.ttf",
									FontSize = fontSize,
									Color = "1 1 1 1"
								}
							}, Layer, Layer + $".Economics.{index}", Layer + $".Economics.{index}");

						#region Icon

						if (_enabledImageLibrary)
							container.Add(new CuiElement
							{
								Parent = Layer + $".Economics.{index}",
								Components =
								{
									new CuiRawImageComponent
									{
										Png = GetImage(economy.Image),
										Color = _config
											.Colors[nowColor.ActiveId].Get
									},
									new CuiRectTransformComponent
									{
										AnchorMin = "0 0.5", AnchorMax = "0 0.5",
										OffsetMin = $"-{fontSize - 5} -{fontSize / 2f}",
										OffsetMax = $"5 {fontSize / 2f}"
									}
								}
							});

						#endregion

						ySwitch = ySwitch - fontSize - _config.EconomicsInterface.Size -
						          _config.EconomicsInterface.Margin;
						index++;
					});
					break;
				}

				default: //events
				{
					var eventConf = _config.Events.Find(x => x.Type == type);
					if (eventConf == null)
					{
						return null;
					}

					if (_enabledImageLibrary)
					{
						var size = eventConf.GetPlayerSize(player).Size;

						bool value;
						_eventsCache.TryGetValue(eventConf.Key, out value);

						container.Add(new CuiElement
						{
							Name = EventsLayer + $".{eventConf.Type}.Icon",
							DestroyUi = EventsLayer + $".{eventConf.Type}.Icon",
							Parent = EventsLayer + $".{eventConf.Type}",
							Components =
							{
								new CuiRawImageComponent
								{
									Png = GetImage(eventConf.Image),
									Color = eventConf.GetColor(player, value)
								},
								new CuiRectTransformComponent
								{
									AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
									OffsetMin = $"-{size / 2} -{size / 2}",
									OffsetMax = $"{size / 2} {size / 2}"
								}
							}
						});
					}

					break;
				}
			}

			if (!first)
				CuiHelper.AddUi(player, container);

			return container;
		}

		private List<EconomyConf> GetAvailableEconomics(BasePlayer player)
		{
			return _config.Economics.FindAll(economy => economy.Enabled && economy.HasPermission(player));
		}

		private void SettingsUi(BasePlayer player, int panelId = -1,
			int id = 0, int page = 0, int colorPage = -1,
			string key = "")
		{
			var container = new CuiElementContainer();

			#region Background

			if (panelId == -1)
			{
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0.19 0.19 0.18 0.3",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
					},
					CursorEnabled = true
				}, "Overlay", SettingsLayer, SettingsLayer);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Close = SettingsLayer
					}
				}, SettingsLayer);
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5"},
				Image = {Color = "0 0 0 0"}
			}, SettingsLayer, SettingsLayer + ".Main", SettingsLayer + ".Main");

			#region Pages Handler

			switch (panelId)
			{
				case -1: //main
				{
					#region Header

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "-200 0", OffsetMax = "200 40"
						},
						Text =
						{
							Text = Msg(player, MainHelp),
							Align = TextAnchor.UpperCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 14,
							Color = "1 1 1 0.7"
						}
					}, SettingsLayer + ".Main");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "-200 40", OffsetMax = "200 80"
						},
						Text =
						{
							Text = Msg(player, MainSettings),
							Align = TextAnchor.LowerCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 24,
							Color = "1 1 1 1"
						}
					}, SettingsLayer + ".Main");

					#endregion

					#region Panels

					var constXSwitch = -(_config.SettingsInterface.IconsOnString * _config.SettingsInterface.Size +
					                     (_config.SettingsInterface.IconsOnString - 1) *
					                     _config.SettingsInterface.Margin) / 2f;
					var xSwitch = constXSwitch;
					var ySwitch = 0f;

					var settings = GetAvailableSettings(player);

					for (var i = 0; i < settings.Count; i++)
					{
						var setting = settings[i];

						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = $"{xSwitch} {ySwitch - _config.SettingsInterface.Size}",
									OffsetMax = $"{xSwitch + _config.SettingsInterface.Size} {ySwitch}"
								},
								Image =
								{
									Color = HexToCuiColor("#000000", 50)
								}
							}, SettingsLayer + ".Main", SettingsLayer + $".Param.{i}");

						if (_enabledImageLibrary)
							container.Add(new CuiElement
							{
								Parent = SettingsLayer + $".Param.{i}",
								Components =
								{
									new CuiRawImageComponent
									{
										Png = GetImage(setting.Image)
									},
									new CuiRectTransformComponent
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = "10 10", OffsetMax = "-10 -10"
									}
								}
							});

						container.Add(new CuiButton
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
								Text = {Text = ""},
								Button =
								{
									Color = "0 0 0 0",
									Command =
										$"UI_PanelSystem settings {(int) setting.Type} {setting.ID} 0 -1 {setting.Key}"
								}
							}, SettingsLayer + $".Param.{i}");

						if ((i + 1) % _config.SettingsInterface.IconsOnString == 0)
						{
							ySwitch = ySwitch - _config.SettingsInterface.Size - _config.SettingsInterface.Margin;
							xSwitch = constXSwitch;
						}
						else
						{
							xSwitch += _config.SettingsInterface.Size + _config.SettingsInterface.Margin;
						}
					}

					#endregion

					#region Exit

					if (settings.Count % _config.SettingsInterface.IconsOnString != 0)
						ySwitch = ySwitch - _config.SettingsInterface.Size - _config.SettingsInterface.Margin;

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = $"{constXSwitch} {ySwitch - _config.SettingsInterface.Size}",
							OffsetMax = $"{-constXSwitch} {ySwitch}"
						},
						Text =
						{
							Text = Msg(player, ExitBtn),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 16,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = HexToCuiColor("#000000", 50),
							Close = SettingsLayer
						}
					}, SettingsLayer + ".Main");

					#endregion

					break;
				}

				default:
				{
					var setting = _totalSettings.Find(x => x.Type == (PanelType) panelId && x.ID == id);
					if (setting == null) return;

					#region Header

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "-200 50", OffsetMax = "200 90"
						},
						Text =
						{
							Text = Msg(player, MainHelp),
							Align = TextAnchor.UpperCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 14,
							Color = "1 1 1 0.7"
						}
					}, SettingsLayer + ".Main");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "-200 90", OffsetMax = "200 130"
						},
						Text =
						{
							Text = Msg(player, MainSettings),
							Align = TextAnchor.LowerCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 24,
							Color = "1 1 1 1"
						}
					}, SettingsLayer + ".Main");

					#endregion

					switch (page)
					{
						case 0:
						{
							#region Buttons

							var ySwitch = 40f;
							var height = 50f;
							var margin = 5f;

							if (setting.Switch)
							{
								var active = !PlayerData.GetOrAdd(player).IsHidden(setting.Type, id);

								container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = $"-112.5 {ySwitch - height}",
										OffsetMax = $"112.5 {ySwitch}"
									},
									Text =
									{
										Text = Msg(player, active ? SwitchOff : SwitchOn),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 16,
										Color = "1 1 1 1"
									},
									Button =
									{
										Color = active ? HexToCuiColor("#4B68FF") : HexToCuiColor("#000000", 60),
										Command = $"UI_PanelSystem switch {panelId} {id} {setting.Type}"
									}
								}, SettingsLayer + ".Main");

								ySwitch = ySwitch - height - margin;
							}

							if (setting.Color)
							{
								container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = $"-112.5 {ySwitch - height}",
										OffsetMax = $"112.5 {ySwitch}"
									},
									Text =
									{
										Text = Msg(player, ChangeColor),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 16,
										Color = "1 1 1 1"
									},
									Button =
									{
										Color = HexToCuiColor("#000000", 50),
										Command = $"UI_PanelSystem settings {panelId} {id} 2"
									}
								}, SettingsLayer + ".Main");

								ySwitch = ySwitch - height - margin;
							}

							if (setting.Size)
							{
								container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = $"-112.5 {ySwitch - height}",
										OffsetMax = $"112.5 {ySwitch}"
									},
									Text =
									{
										Text = Msg(player, ChangeSize),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 16,
										Color = "1 1 1 1"
									},
									Button =
									{
										Color = HexToCuiColor("#000000", 50),
										Command = $"UI_PanelSystem settings {panelId} {id} 3"
									}
								}, SettingsLayer + ".Main");

								ySwitch = ySwitch - height - margin;
							}

							#endregion

							#region Back

							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = $"-112.5 {ySwitch - height}",
									OffsetMax = $"112.5 {ySwitch}"
								},
								Text =
								{
									Text = Msg(player, GoBack),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-bold.ttf",
									FontSize = 16,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = HexToCuiColor("#000000", 50),
									Command = "UI_PanelSystem settings"
								}
							}, SettingsLayer + ".Main");

							#endregion

							break;
						}

						case 2: // color
						{
							switch (setting.Type)
							{
								case PanelType.Economics:
								{
									colorPage = 3;
									break;
								}
							}

							switch (colorPage)
							{
								case -1: //select color type
								{
									#region Buttons

									var ySwitch = 40f;
									var height = 50f;
									var margin = 5f;

									container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "1 1",
											OffsetMin = $"-112.5 {ySwitch - height}",
											OffsetMax = $"112.5 {ySwitch}"
										},
										Text =
										{
											Text = Msg(player, ActiveColor),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 16,
											Color = "1 1 1 1"
										},
										Button =
										{
											Color = HexToCuiColor("#000000", 50),
											Command = $"UI_PanelSystem settings {panelId} {id} {page} 0"
										}
									}, SettingsLayer + ".Main");

									ySwitch = ySwitch - height - margin;

									container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "1 1",
											OffsetMin = $"-112.5 {ySwitch - height}",
											OffsetMax = $"112.5 {ySwitch}"
										},
										Text =
										{
											Text = Msg(player, InActiveColor),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 16,
											Color = "1 1 1 1"
										},
										Button =
										{
											Color = HexToCuiColor("#000000", 50),
											Command = $"UI_PanelSystem settings {panelId} {id} {page} 1"
										}
									}, SettingsLayer + ".Main");

									ySwitch = ySwitch - height - margin;

									#endregion

									#region Back

									container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "1 1",
											OffsetMin = $"-112.5 {ySwitch - height}",
											OffsetMax = $"112.5 {ySwitch}"
										},
										Text =
										{
											Text = Msg(player, GoBack),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 16,
											Color = "1 1 1 1"
										},
										Button =
										{
											Color = HexToCuiColor("#000000", 50),
											Command = $"UI_PanelSystem settings {panelId} {id}"
										}
									}, SettingsLayer + ".Main");

									#endregion

									break;
								}

								default: //select color
								{
									#region Colors

									var ySwitch = 40f;

									var amountOnString = 5;
									var margin = 10f;
									var size = (215 - (amountOnString - 1) * margin) / amountOnString;

									var constXSwitch = -(amountOnString * size + (amountOnString - 1) * margin) / 2f;
									var xSwitch = constXSwitch;

									var nowColor = PlayerData.GetOrAdd(player)
										.GetColorData(setting.Type, setting.ID, key);

									var changeColor = colorPage == 1 ? nowColor.InactiveId : nowColor.ActiveId;

									for (var i = 0; i < _config.Colors.Count; i++)
									{
										var color = _config.Colors[i];

										container.Add(new CuiButton
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "1 1",
												OffsetMin = $"{xSwitch} {ySwitch - size}",
												OffsetMax = $"{xSwitch + size} {ySwitch}"
											},
											Text =
											{
												Text = changeColor == i
													? Msg(player, SelectColor)
													: string.Empty,
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-regular.ttf",
												FontSize = 14,
												Color = "1 1 1 1"
											},
											Button =
											{
												Color = color.Get,
												Command =
													$"UI_PanelSystem setcolor {panelId} {id} {page} {colorPage} {setting.Type} {i} {key}"
											}
										}, SettingsLayer + ".Main");

										if ((i + 1) % amountOnString == 0)
										{
											ySwitch = ySwitch - size - margin;
											xSwitch = constXSwitch;
										}
										else
										{
											xSwitch += size + margin;
										}
									}

									#endregion

									#region Back

									container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "0 0", AnchorMax = "1 1",
											OffsetMin = $"-112.5 {ySwitch - 50}",
											OffsetMax = $"112.5 {ySwitch}"
										},
										Text =
										{
											Text = Msg(player, GoBack),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 16,
											Color = "1 1 1 1"
										},
										Button =
										{
											Color = HexToCuiColor("#000000", 50),
											Command = setting.Type == PanelType.Economics
												? $"UI_PanelSystem settings {panelId} {id} 0"
												: $"UI_PanelSystem settings {panelId} {id} {page} -1"
										}
									}, SettingsLayer + ".Main");

									#endregion

									break;
								}
							}

							break;
						}

						case 3: // size
						{
							#region Buttons

							var ySwitch = 40f;
							var height = 50f;
							var margin = 5f;

							var nowSize = PlayerData.GetOrAdd(player).GetSize(player, setting.Type, id, key);

							var sizesForPlayer = setting.GetSizes(player);
							var index = 0;
							sizesForPlayer.ForEach(size =>
							{
								container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = $"-112.5 {ySwitch - height}",
										OffsetMax = $"112.5 {ySwitch}"
									},
									Text =
									{
										Text = Msg(player, size.LangKey),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf",
										FontSize = 16,
										Color = "1 1 1 1"
									},
									Button =
									{
										Color = nowSize == size
											? HexToCuiColor("#4B68FF")
											: HexToCuiColor("#000000", 50),
										Command =
											$"UI_PanelSystem setsize {panelId} {id} {page} {setting.Type} {index} {key}"
									}
								}, SettingsLayer + ".Main");

								ySwitch = ySwitch - height - margin;
								index++;
							});

							#endregion

							#region Back

							container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = $"-112.5 {ySwitch - height}",
									OffsetMax = $"112.5 {ySwitch}"
								},
								Text =
								{
									Text = Msg(player, GoBack),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-bold.ttf",
									FontSize = 16,
									Color = "1 1 1 1"
								},
								Button =
								{
									Color = HexToCuiColor("#000000", 50),
									Command = $"UI_PanelSystem settings {panelId} {id}"
								}
							}, SettingsLayer + ".Main");

							#endregion

							break;
						}
					}

					break;
				}
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Update

		private void OnMinute()
		{
			foreach (var player in BasePlayer.activePlayerList)
				if (!PlayerData.GetOrAdd(player).IsHidden(PanelType.Time))
					UpdateUi(player, PanelType.Time);
		}

		private void StartUpdate()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				var data = PlayerData.GetOrAdd(player);

				if (_config.Players.Enabled && !data.IsHidden(PanelType.Online) &&
				    _config.Players.HasPermission(player))
					UpdateUi(player, PanelType.Online);

				if (_config.Sleepers.Enabled && !data.IsHidden(PanelType.Sleepers) &&
				    _config.Sleepers.HasPermission(player))
					UpdateUi(player, PanelType.Sleepers);

				if (_config.Time.Enabled && !_config.Time.GameUpdate && !data.IsHidden(PanelType.Time) &&
				    _config.Time.HasPermission(player))
					UpdateUi(player, PanelType.Time);

				if (_config.Economics.Count > 0 && !data.IsHidden(PanelType.Economics))
					UpdateUi(player, PanelType.Economics);
			}
		}

		private void OnEntityChangeStatus(BaseEntity entity, bool spawned, bool ignorePlayers = false)
		{
			if (entity == null) return;

#if TESTING
			Puts($"[OnEntityChangeStatus] entity type={entity.GetType()}");
#endif

			PanelType type;
			if (entity is BradleyAPC)
				type = PanelType.Bradley;
			else if (entity is CH47Helicopter)
				type = PanelType.CH47;
			else if (entity is PatrolHelicopter)
				type = PanelType.Helicopter;
			else if (entity is CargoPlane)
				type = PanelType.Airdrop;
			else if (entity is CargoShip)
				type = PanelType.CargoShip;
			else
				return;

			OnEventChangeStatus(type, spawned, ignorePlayers);
		}

		private void OnEventChangeStatus(PanelType type, bool spawned, bool ignorePlayers = false)
		{
			_eventsCache[type.ToString()] = spawned;

			if (!ignorePlayers)
				foreach (var player in BasePlayer.activePlayerList)
					UpdateUi(player, type);
		}

		#endregion

		#region Time Handler

		private uint componentSearchAttempts;

		private TOD_Time timeComponent;

		private void GetTimeComponent()
		{
			if (TOD_Sky.Instance == null)
			{
				++componentSearchAttempts;
				if (componentSearchAttempts < 50)
				{
					timer.In(3, GetTimeComponent);
					return;
				}
			}

			timeComponent = TOD_Sky.Instance.Components.Time;

			if (timeComponent == null)
			{
				RaiseError("Could not fetch time component. Plugin will not work without it.");
				return;
			}

			timeComponent.OnMinute += OnMinute;
		}

		#endregion

		#region Utils

		private List<ButtonInfo> GetAvailableButtons(BasePlayer player)
		{
			return _config.ButtonsSettings.Buttons.FindAll(btn => btn.Enabled &&
			                                                      (string.IsNullOrEmpty(btn.Permission) ||
			                                                       permission.UserHasPermission(player.UserIDString,
				                                                       btn.Permission)));
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.ButtonsSettings.Commands, nameof(CmdSwitchButtons));

			if (_config.Hide.Enabled)
				AddCovalenceCommand(_config.Hide.Command, nameof(CmdHide));
		}

		private void RegisterPermissions()
		{
			_config.Events.ForEach(check =>
			{
				if (!string.IsNullOrEmpty(check.Permission) &&
				    !permission.PermissionExists(check.Permission))
					permission.RegisterPermission(check.Permission, this);

				if (!string.IsNullOrEmpty(check.Settings.Permission) &&
				    !permission.PermissionExists(check.Settings.Permission))
					permission.RegisterPermission(check.Settings.Permission, this);

				check.Size.Sizes.ForEach(size =>
				{
					if (!string.IsNullOrEmpty(size.Permission) &&
					    !permission.PermissionExists(size.Permission))
						permission.RegisterPermission(size.Permission, this);
				});
			});

			_config.Economics.ForEach(check =>
			{
				if (!string.IsNullOrEmpty(check.Permission) &&
				    !permission.PermissionExists(check.Permission))
					permission.RegisterPermission(check.Permission, this);

				if (!string.IsNullOrEmpty(check.Settings.Permission) &&
				    !permission.PermissionExists(check.Settings.Permission))
					permission.RegisterPermission(check.Settings.Permission, this);

				check.Size.Sizes.ForEach(size =>
				{
					if (!string.IsNullOrEmpty(size.Permission) &&
					    !permission.PermissionExists(size.Permission))
						permission.RegisterPermission(size.Permission, this);
				});
			});

			if (_config.Logo.Enabled)
				if (!string.IsNullOrEmpty(_config.Logo.Permission) &&
				    !permission.PermissionExists(_config.Logo.Permission))
					permission.RegisterPermission(_config.Logo.Permission, this);

			if (_config.Settings.Enabled)
				if (!string.IsNullOrEmpty(_config.Settings.Permission) &&
				    !permission.PermissionExists(_config.Settings.Permission))
					permission.RegisterPermission(_config.Settings.Permission, this);

			if (_config.Players.Enabled)
			{
				if (!string.IsNullOrEmpty(_config.Players.Permission) &&
				    !permission.PermissionExists(_config.Players.Permission))
					permission.RegisterPermission(_config.Players.Permission, this);

				if (!string.IsNullOrEmpty(_config.Players.Settings.Permission) &&
				    !permission.PermissionExists(_config.Players.Settings.Permission))
					permission.RegisterPermission(_config.Players.Settings.Permission, this);

				_config.Players.Size.Sizes.ForEach(size =>
				{
					if (!string.IsNullOrEmpty(size.Permission) &&
					    !permission.PermissionExists(size.Permission))
						permission.RegisterPermission(size.Permission, this);
				});
			}

			if (_config.Time.Enabled)
			{
				if (!string.IsNullOrEmpty(_config.Time.Permission) &&
				    !permission.PermissionExists(_config.Time.Permission))
					permission.RegisterPermission(_config.Time.Permission, this);

				if (!string.IsNullOrEmpty(_config.Time.Settings.Permission) &&
				    !permission.PermissionExists(_config.Time.Settings.Permission))
					permission.RegisterPermission(_config.Time.Settings.Permission, this);

				_config.Time.Size.Sizes.ForEach(size =>
				{
					if (!string.IsNullOrEmpty(size.Permission) &&
					    !permission.PermissionExists(size.Permission))
						permission.RegisterPermission(size.Permission, this);
				});
			}

			if (_config.ButtonsSettings.Enabled)
			{
				if (!string.IsNullOrEmpty(_config.ButtonsSettings.Permission) &&
				    !permission.PermissionExists(_config.ButtonsSettings.Permission))
					permission.RegisterPermission(_config.ButtonsSettings.Permission, this);

				if (!string.IsNullOrEmpty(_config.ButtonsSettings.Settings.Permission) &&
				    !permission.PermissionExists(_config.ButtonsSettings.Settings.Permission))
					permission.RegisterPermission(_config.ButtonsSettings.Settings.Permission, this);

				_config.ButtonsSettings.Buttons.ForEach(btn =>
				{
					if (!string.IsNullOrEmpty(btn.Permission) &&
					    !permission.PermissionExists(btn.Permission))
						permission.RegisterPermission(btn.Permission, this);
				});

				_config.ButtonsSettings.Size.Sizes.ForEach(size =>
				{
					if (!string.IsNullOrEmpty(size.Permission) &&
					    !permission.PermissionExists(size.Permission))
						permission.RegisterPermission(size.Permission, this);
				});
			}
		}

		private void LoadSettings()
		{
			_config.Events.ForEach(x =>
			{
				if (!x.Enabled || !x.Settings.Enabled) return;

				var settings = x.Settings;
				settings.Type = x.Type;
				settings.Key = x.Key;
				settings.Image = x.Image;
				settings.Sizes = x.Size.Sizes.Cast<SizeData>().ToList();

				_totalSettings.Add(settings);
			});

			_config.Economics.ForEach(x =>
			{
				if (!x.Enabled || !x.Settings.Enabled) return;

				var settings = x.Settings;
				settings.Type = PanelType.Economics;
				settings.ID = x.ID;
				settings.Image = x.Image;
				settings.Sizes = x.Size.Sizes.Cast<SizeData>().ToList();

				_totalSettings.Add(settings);
			});

			//Players
			if (_config.Players.Enabled)
			{
				var players = _config.Players.Settings;
				players.Type = PanelType.Online;
				players.Image = _config.Players.SettingsImage;
				players.Sizes = _config.Players.Size.Sizes.Cast<SizeData>().ToList();
				_totalSettings.Add(players);
			}

			//Sleepers
			if (_config.Sleepers.Enabled)
			{
				var sleepers = _config.Sleepers.Settings;
				sleepers.Type = PanelType.Sleepers;
				sleepers.Image = _config.Sleepers.SettingsImage;
				sleepers.Sizes = _config.Sleepers.Size.Sizes.Cast<SizeData>().ToList();
				_totalSettings.Add(sleepers);
			}

			//Time
			if (_config.Time.Enabled)
			{
				var time = _config.Time.Settings;
				time.Type = PanelType.Time;
				time.Image = _config.Time.SettingsImage;
				time.Sizes = _config.Time.Size.Sizes.Cast<SizeData>().ToList();
				_totalSettings.Add(time);
			}

			//Buttons
			if (_config.ButtonsSettings.Enabled)
			{
				var buttons = _config.ButtonsSettings.Settings;
				buttons.Type = PanelType.Button;
				buttons.Image = _config.ButtonsSettings.SettingsImage;
				buttons.Sizes = _config.ButtonsSettings.Size.Sizes.Cast<SizeData>().ToList();
				_totalSettings.Add(buttons);
			}
		}

		private void LoadEvents()
		{
			foreach (var entity in BaseNetworkable.serverEntities.OfType<BaseEntity>())
				OnEntityChangeStatus(entity, true, true);

			if (WipeBlock != null && WipeBlock.IsLoaded && _totalSettings.Exists(x => x.Type == PanelType.WipeBlock))
			{
				var value = Convert.ToBoolean(WipeBlock?.Call("AnyBlocked"));

				OnEventChangeStatus(PanelType.WipeBlock, value);
			}
		}

		#region Working With Images

		private void AddImage(string url, string fileName, ulong imageId = 0)
		{
#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				[fileName] = url
			});
#else
			ImageLibrary?.Call("AddImage", url, fileName, imageId);
#endif
		}
		
		private string GetImage(string name)
		{
#if CARBON
			return imageDatabase.GetImageString(name);
#else
			return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
		}

		private void LoadImages()
		{
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
			
			_enabledImageLibrary = true;

			var imagesList = new Dictionary<string, string>();

			if (_config.Logo.Enabled)
				if (!string.IsNullOrEmpty(_config.Logo.Image))
					imagesList.TryAdd(_config.Logo.Image, _config.Logo.Image);

			if (_config.Players.Enabled)
			{
				if (!string.IsNullOrEmpty(_config.Players.Image))
					imagesList.TryAdd(_config.Players.Image, _config.Players.Image);

				if (!string.IsNullOrEmpty(_config.Players.SettingsImage))
					imagesList.TryAdd(_config.Players.SettingsImage, _config.Players.SettingsImage);
			}

			if (_config.Time.Enabled)
			{
				if (!string.IsNullOrEmpty(_config.Time.Image))
					imagesList.TryAdd(_config.Time.Image, _config.Time.Image);

				if (!string.IsNullOrEmpty(_config.Time.SettingsImage))
					imagesList.TryAdd(_config.Time.SettingsImage, _config.Time.SettingsImage);
			}

			if (_config.Sleepers.Enabled)
			{
				if (!string.IsNullOrEmpty(_config.Sleepers.Image))
					imagesList.TryAdd(_config.Sleepers.Image, _config.Sleepers.Image);

				if (!string.IsNullOrEmpty(_config.Sleepers.SettingsImage))
					imagesList.TryAdd(_config.Sleepers.SettingsImage, _config.Sleepers.SettingsImage);
			}

			if (_config.ButtonsSettings.Enabled)
			{
				if (!string.IsNullOrEmpty(_config.ButtonsSettings.SettingsImage))
					imagesList.TryAdd(_config.ButtonsSettings.SettingsImage, _config.ButtonsSettings.SettingsImage);

				_config.ButtonsSettings.Buttons.ForEach(btn =>
				{
					if (btn.Enabled && !string.IsNullOrEmpty(btn.Image))
						imagesList.TryAdd(btn.Image, btn.Image);
				});
			}

			if (!string.IsNullOrEmpty(_config.Settings.Image))
				imagesList.TryAdd(_config.Settings.Image, _config.Settings.Image);

			_config.Events.ForEach(x =>
			{
				if (x.Enabled && !string.IsNullOrEmpty(x.Image))
					imagesList.TryAdd(x.Image, x.Image);
			});

			_config.Economics.ForEach(x =>
			{
				if (x.Enabled && !string.IsNullOrEmpty(x.Image))
					imagesList.TryAdd(x.Image, x.Image);
			});
			
#if CARBON
            imageDatabase.Queue(false, imagesList);
#else
			timer.In(1f, () =>
			{
				if (ImageLibrary is not {IsLoaded: true})
				{
					_enabledImageLibrary = false;
					
					BroadcastILNotInstalled();
					return;
				}
				
				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			});
#endif		
		}

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		#endregion

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

		private List<PanelSettings> GetAvailableSettings(BasePlayer player)
		{
			var cov = player.IPlayer;
			return _totalSettings.FindAll(x =>
				string.IsNullOrEmpty(x.Permission) || cov.HasPermission(x.Permission));
		}

		private List<EventConf> GetAvailableEvents(BasePlayer player)
		{
			var cov = player.IPlayer;
			var data = PlayerData.GetOrAdd(player);
			return _config.Events.FindAll(x =>
				x.Enabled &&
				(string.IsNullOrEmpty(x.Settings.Permission) || cov.HasPermission(x.Settings.Permission)) &&
				!data.IsHidden(x.Type));
		}

		#endregion

		#region API

		private void OnEventChangeStatus(string key, bool spawned, bool ignorePlayers = false)
		{
			if (string.IsNullOrEmpty(key)) return;

			if (!_config.Events.Exists(x => x.Key == key))
			{
				PrintWarning("Key '{key}' not found!!!");
				return;
			}

			_eventsCache[key] = spawned;

			if (!ignorePlayers)
				foreach (var player in BasePlayer.activePlayerList)
					UpdateUi(player, PanelType.Custom);
		}

		#endregion

		#region Lang

		private const string
			SleepersTitle = "SleepersTitle",
			TimeTitle = "TimeTitle",
			SelectColor = "SelectColor",
			InActiveColor = "InActiveColor",
			ActiveColor = "ActiveColor",
			GoBack = "GoBack",
			ChangeSize = "ChangeSize",
			ChangeColor = "ChangeColor",
			SwitchOff = "SwitchOff",
			SwitchOn = "SwitchOn",
			ExitBtn = "ExitBtn",
			MainSettings = "MainSettings",
			MainHelp = "MainHelp",
			OnlineTitle = "OnlineTitle";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[OnlineTitle] = "ONLINE:",
				[MainHelp] = "To change the color or visibility of the indicator you need, click on it",
				[MainSettings] = "Display Settings",
				[ExitBtn] = "Exit",
				[SwitchOn] = "Enable showing",
				[SwitchOff] = "Disable showing",
				[ChangeColor] = "Change color",
				[ChangeSize] = "Change size",
				[GoBack] = "Go Back",
				[ActiveColor] = "Active Color",
				[InActiveColor] = "Inactive Color",
				[SelectColor] = "✔",
				[TimeTitle] = "TIME:",
				[SleepersTitle] = "SLEEP:",
				["sizeBig"] = "BIG",
				["sizeMedium"] = "MEDIUM",
				["sizeSmall"] = "SMALL",
				["BtnShop"] = "Shop",
				["BtnStats"] = "Stats",
				["BtnBank"] = "Bank"
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
			SendReply(player, Msg(key, player.UserIDString, obj));
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