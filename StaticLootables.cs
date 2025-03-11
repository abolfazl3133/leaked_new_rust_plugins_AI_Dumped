using Facepunch;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("StaticLootables", "Raul-Sorin Sorban", "2.9.8")]
	[Description("Loot containers that aren't normally supposed to be looted.")]
	public class StaticLootables : RustPlugin
	{
		public static StaticLootables Instance { get; private set; }

		public System.Random Randomizer { get; set; } = new System.Random();

		public System.Random GetSeededRandomizer(int seed)
		{
			return new System.Random(seed);
		}

		public Dictionary<string, string> PanelTypes { get; set; } = new Dictionary<string, string>
		{
			["generic"] = "Generic",
			["genericsmall"] = "Generic - Small",
			["generic_large"] = "Generic - Large",
			["generic_resizable"] = "Generic - Resizable",
			["locker"] = "Locker",
			["lantern"] = "Lantern",
			["mailboxcontents"] = "Mailbox Contents",
			["photoframe"] = "Photo Frame",
			["reclaim"] = "Reclaim",
			["skulltrophy"] = "Skull Trophy",
			["toolcupboard"] = "Tool Cupboard"
		};
		public List<string> PanelTypesList { get; set; } = new List<string>();

		#region Permissions

		public const string AdminPerm = "staticlootables.admin";
		public const string EditorPerm = "staticlootables.editor";

		public void RegisterPermissions()
		{
			permission.RegisterPermission(AdminPerm, this);
			permission.RegisterPermission(EditorPerm, this);
		}

		#endregion

		#region Plugins

		[PluginReference] Plugin ImageLibrary;

		[PluginReference] Plugin QuickSort;

		[PluginReference] Plugin ZoneManager;

		[PluginReference] Plugin LootablesExt;

		public void RefreshPlugins()
		{
			if (ImageLibrary == null || !ImageLibrary.IsLoaded) { ImageLibrary = plugins.Find("ImageLibrary"); ImageLibrary?.Load(); }
			if (QuickSort == null || !QuickSort.IsLoaded) { QuickSort = plugins.Find("QuickSort"); QuickSort?.Load(); }
			if (ZoneManager == null || !ZoneManager.IsLoaded) { ZoneManager = plugins.Find("ZoneManager"); ZoneManager?.Load(); }
			if (LootablesExt == null || !LootablesExt.IsLoaded) { LootablesExt = plugins.Find("LootablesExt"); LootablesExt?.Load(); }
		}

		#endregion

		#region CUI

		public const string CUIInteractionName = "stlootable";
		public const string CUIEditorInteractionName = "stlootableeditorinteraction";
		public const string CUIEditorName = "stlootableeditor";
		public const string CUICodeLockInputName = "stlootableclin";
		public const string CUIPinAppendCommand = "staticlootables.pinappend";
		public const string CUIPinApplyCommand = "staticlootables.pinapply";
		public const string CUIPinUndoCommand = "staticlootables.pinundo";
		public const string DamageColor = "1 0.3 0.3 0.8";
		public const string HackingColor = "0.5 0.8 0.3 0.8";

		public Dictionary<BasePlayer, PlayerInteraction?> CUIPlayers { get; set; } = new Dictionary<BasePlayer, PlayerInteraction?>(800);
		public Dictionary<BasePlayer, TimeSince> TimedPlayers { get; set; } = new Dictionary<BasePlayer, TimeSince>(800);
		public Dictionary<BasePlayer, Vector3> PositionCache { get; set; } = new Dictionary<BasePlayer, Vector3>(800);
		public Dictionary<BasePlayer, string> PinCache { get; set; } = new Dictionary<BasePlayer, string>(800);
		public Dictionary<BasePlayer, RootData.Lootable> LootingPlayers { get; set; } = new Dictionary<BasePlayer, RootData.Lootable>(800);
		public List<BasePlayer> DrawnUI { get; set; } = new List<BasePlayer>();

		public const int ProcessorCount = 1;

		public struct PlayerInteraction
		{
			public RootConfig.Interaction Interaction { get; set; }
			public RootLock Lock { get; set; }
			public RootHack Hack { get; set; }
			public RootData.Lootable Lootable { get; set; }

			public bool IsProcessingCommand { get; set; }
			public bool IsLoadingTimer { get; set; }
			public DateTime Timer { get; set; }

			public float TimerTime => (float)(DateTime.Now - Timer).TotalSeconds;
		}
		public void DrawInteraction(BasePlayer player, RootConfig.Interaction interaction, RootConfig.LootableDefinition definition, RootData.Lootable lootable, bool transition = true, bool enableDot = true, bool reset = true)
		{
			// UndrawInteraction(player, reset);

			var container = new CuiElementContainer();
			var background = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0.36 0.36", AnchorMax = "0.64 0.738" }, CursorEnabled = false }, "Hud", CUIInteractionName, CUIInteractionName);
			var offset = "0 -10";
			var fade = transition ? 0.3f : 0f;
			var @lock = lootable != null && lootable.Lock != null ? lootable.Lock : null;
			var hack = lootable != null && lootable.Hack != null ? lootable.Hack : null;
			var i = default(PlayerInteraction?);

			if (!CUIPlayers.ContainsKey(player))
				CUIPlayers.Add(player, i = new PlayerInteraction { Interaction = interaction, Lock = @lock, Lootable = lootable });
			else i = CUIPlayers[player];

			if (i == null) return;

			if (enableDot) container.Add(new CuiElement { Parent = background, FadeOut = fade, Components = { Death.GetRawImage(Config.Urls.DotUrl, fade: fade), new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = offset, OffsetMax = offset } } });
			if (Config.ShadowOpacity != 0f) container.Add(new CuiElement { Parent = background, FadeOut = fade, Components = { Death.GetRawImage(Config.Urls.ShadowUrl, color: $"1 1 1 {Config.ShadowOpacity}", fade: fade), new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = offset, OffsetMax = offset } } });
			container.Add(new CuiElement { Parent = background, FadeOut = fade, Components = { Death.GetRawImage(i.Value.IsProcessingCommand ? Config.Urls.LoadingUrl : lootable != null && lootable.IsBusy(player) ? Config.Urls.BusyUrl : (@lock == null ? (hack == null ? interaction.Icon : hack.IsHacking ? Config.Urls.HackingUrl : Config.Urls.HackUrl) : Config.Urls.LockUrl), fade: fade), new CuiRectTransformComponent { AnchorMin = "0.46 0.53", AnchorMax = "0.54 0.63", OffsetMin = offset, OffsetMax = offset } } });
			container.Add(new CuiLabel { Text = { Text = $"{Config.InteractionPrefix}<b>{(i.Value.IsProcessingCommand ? Config.CommandName : lootable != null && lootable.IsBusy(player) ? $"{Config.BusyName}" : (@lock == null ? hack == null ? interaction.Text : hack.IsHacking ? Config.HackingName : Config.HackName : Config.LockName))}{(hack == null || @lock != null || (hack?.IsHacking).GetValueOrDefault() ? string.Empty : $"  {Config.HackingPrefix}")}</b>".ToUpper(), FadeIn = fade, FontSize = 13, Font = "robotocondensed-regular.ttf", Align = TextAnchor.LowerCenter }, RectTransform = { AnchorMin = $"0 0.4275", AnchorMax = $"1 1" } }, background);

			if (lootable != null)
			{
				if (lootable.IsLocked() && definition != null && definition.Lock != null)
				{
					var value = lootable.Lock.Health;
					var total = definition.Lock.Health * Config.LootableHealthMultiplier;
					var count = lootable.Contents.Count;

					DrawInteractionBar(background, container, value, total, fade, count, definition, lootable);
				}
				else if (lootable.Hack != null && definition != null && definition.Hack != null)
				{
					var time = lootable.GetHackTime();
					var totalTime = lootable.GetTotalHackingTime();

					if (lootable.IsHacking())
					{
						DrawInteractionBar(background, container, totalTime - time, totalTime, fade, -1, definition, textOne: $"{totalTime - time:0}s", textTwo: $"Please wait...", hackMode: true);
					}
				}
			}

			if (i != null && i.Value.IsLoadingTimer)
			{
				var span = DateTime.Now - i.Value.Timer;
				var value = definition.Timer - (float)span.TotalSeconds;
				var total = definition.Timer;

				DrawInteractionBar(background, container, value, total, fade, 0, definition, textOne: $"{value:0}s", textTwo: "", hackMode: true);
			}

			CuiHelper.AddUi(player, container);
			DrawnUI.Add(player);
		}
		public void UndrawInteraction(BasePlayer player, bool reset = true)
		{
			if (reset && !CUIPlayers.ContainsKey(player)) return;

			if (DrawnUI.Contains(player))
			{
				for (int i = 0; i < ProcessorCount; i++)
				{
					CuiHelper.DestroyUi(player, CUICodeLockInputName);
				}
				for (int i = 0; i < ProcessorCount; i++)
				{
					CuiHelper.DestroyUi(player, CUIInteractionName);
				}
				for (int i = 0; i < ProcessorCount; i++)
				{
					CuiHelper.DestroyUi(player, CUIEditorInteractionName);
				}
				for (int i = 0; i < ProcessorCount; i++)
				{
					CuiHelper.DestroyUi(player, CUIEditorName);
				}

				DrawnUI.Remove(player);
			}

			if (reset) CUIPlayers.Remove(player);
		}

		public void DrawInteractionBar(string background, CuiElementContainer container, float value, float total, float fade, int count = -1, RootConfig.LootableDefinition definition = null, RootData.Lootable lootable = null, string textOne = null, string textTwo = null, bool hackMode = false)
		{
			container.Add(new CuiLabel { Text = { Text = textOne == null ? $"<b>{value:n0} / {total:n0}</b>".ToUpper() : textOne, FadeIn = fade, FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperRight }, RectTransform = { AnchorMin = "0.3 0.9", AnchorMax = "0.7 1" } }, background);

			var bar = container.Add(new CuiPanel { Image = { Color = hackMode ? HackingColor : DamageColor, FadeIn = fade }, RectTransform = { AnchorMin = "0.3 0.925", AnchorMax = "0.7 0.95" }, CursorEnabled = false }, background);
			container.Add(new CuiPanel { Image = { Color = $"1 1 1 1", FadeIn = fade }, RectTransform = { AnchorMin = "0 0", AnchorMax = $"{Scale(value, 0f, total, 0f, 1f)} 1" }, CursorEnabled = false }, bar);

			if ((definition != null && Instance.Config.ShowLockedLootableApproximateCount) || textTwo != null)
			{
				var randomizer = GetSeededRandomizer(lootable == null ? definition.InteractionIndex : (int)(lootable.Position.x + lootable.Position.y + lootable.Position.z + definition.InteractionIndex));
				container.Add(new CuiLabel { Text = { Text = textTwo == null ? $"~{randomizer.Next(count - 2, count + 2):n0} items appx.".ToUpper() : textTwo, FadeIn = fade, FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft }, RectTransform = { AnchorMin = "0.3 0.9", AnchorMax = "0.7 1" } }, background);
				randomizer = null;
			}
		}
		private void DrawCodeLockInput(BasePlayer player)
		{
			if (!PinCache.ContainsKey(player)) PinCache.Add(player, string.Empty);

			for (int i = 0; i < 5; i++)
			{
				CuiHelper.DestroyUi(player, CUICodeLockInputName);
			}

			var container = new CuiElementContainer();

			var background = container.Add(new CuiPanel { Image = { Color = $"0.3 0.3 0.3 0.9", Material = "assets/content/ui/uibackgroundblur.mat" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }, CursorEnabled = true }, "Hud", CUICodeLockInputName, CUICodeLockInputName);
			container.Add(new CuiButton { Button = { Command = $"{CUIPinApplyCommand}", Color = "0 0 0 0" }, Text = { Text = string.Empty }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, background);

			var panel = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0.425 0.35", AnchorMax = "0.575 0.7" }, CursorEnabled = false }, background);

			container.Add(new CuiLabel { Text = { Text = $"<b>{Config.CodeInputTitle}</b>".ToUpper(), FontSize = 18, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter }, RectTransform = { AnchorMin = $"0 0.75", AnchorMax = $"1 0.985" } }, panel);

			var pinText = "";
			foreach (var number in PinCache[player]) pinText += $"{number}  ";
			var pin = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0.5" }, RectTransform = { AnchorMin = "0.25 0.78", AnchorMax = "0.75 0.875" }, CursorEnabled = false }, panel);
			container.Add(new CuiLabel { Text = { Text = $"<b>{pinText.Trim()}</b>".ToUpper(), Color = "1 1 1 0.7", FontSize = 18, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" } }, pin);

			#region Buttons

			var offset = 0f;
			var horizontalOffset = 12f;
			var spacing = 37.5f;
			var count = 9;

			for (int i = 0; i < 3; i++)
			{
				container.Add(new CuiButton { Button = { Command = $"{CUIPinAppendCommand} {count - 2}", Color = "0.8 0.8 0.8 0.35" }, Text = { Text = $"<b>{count - 2}</b>", Align = TextAnchor.MiddleCenter, Color = "0.15 0.15 0.15 0.7", Font = "robotocondensed-regular.ttf", FontSize = 19 }, RectTransform = { AnchorMin = "0.2 0.62", AnchorMax = "0.35 0.75", OffsetMin = $"{horizontalOffset} {offset}", OffsetMax = $"{horizontalOffset} {offset}" } }, panel);
				container.Add(new CuiButton { Button = { Command = $"{CUIPinAppendCommand} {count - 1}", Color = "0.8 0.8 0.8 0.35" }, Text = { Text = $"<b>{count - 1}</b>", Align = TextAnchor.MiddleCenter, Color = "0.15 0.15 0.15 0.7", Font = "robotocondensed-regular.ttf", FontSize = 19 }, RectTransform = { AnchorMin = "0.2 0.62", AnchorMax = "0.35 0.75", OffsetMin = $"{horizontalOffset + 32} {offset}", OffsetMax = $"{horizontalOffset + 32} {offset}" } }, panel);
				container.Add(new CuiButton { Button = { Command = $"{CUIPinAppendCommand} {count}", Color = "0.8 0.8 0.8 0.35" }, Text = { Text = $"<b>{count}</b>", Align = TextAnchor.MiddleCenter, Color = "0.15 0.15 0.15 0.7", Font = "robotocondensed-regular.ttf", FontSize = 19 }, RectTransform = { AnchorMin = "0.2 0.62", AnchorMax = "0.35 0.75", OffsetMin = $"{horizontalOffset + 64} {offset}", OffsetMax = $"{horizontalOffset + 64} {offset}" } }, panel);

				offset -= spacing;
				count -= 3;
			}

			container.Add(new CuiButton { Button = { Command = $"{CUIPinAppendCommand} 0", Color = "0.8 0.8 0.8 0.35" }, Text = { Text = $"<b>0</b>", Align = TextAnchor.MiddleCenter, Color = "0.15 0.15 0.15 0.7", Font = "robotocondensed-regular.ttf", FontSize = 19 }, RectTransform = { AnchorMin = "0.2 0.62", AnchorMax = "0.515 0.75", OffsetMin = $"{horizontalOffset} {offset}", OffsetMax = $"{horizontalOffset} {offset}" } }, panel);
			var acceptButton = container.Add(new CuiButton { Button = { Command = $"{CUIPinApplyCommand}", Color = "0.2 0.85 0.2 0.35" }, Text = { Text = string.Empty, Align = TextAnchor.MiddleCenter, Color = "0.15 0.15 0.15 0.7", Font = "robotocondensed-regular.ttf", FontSize = 19 }, RectTransform = { AnchorMin = "0.535 0.62", AnchorMax = "0.68 0.75", OffsetMin = $"{horizontalOffset} {offset}", OffsetMax = $"{horizontalOffset} {offset}" } }, panel);
			container.Add(new CuiElement { Parent = acceptButton, Components = { Death.GetRawImage(Config.Urls.CheckUrl, color: "0.15 0.15 0.15 0.7"), new CuiRectTransformComponent { AnchorMin = "0.2 0.22", AnchorMax = "0.8 0.78" } } });

			offset -= spacing;
			container.Add(new CuiButton { Button = { Command = $"{CUIPinUndoCommand}", Color = "0.5 0.5 0.5 0.35" }, Text = { Text = $"<b>C</b>", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.4", Font = "robotocondensed-regular.ttf", FontSize = 19 }, RectTransform = { AnchorMin = "0.2 0.62", AnchorMax = "0.68 0.75", OffsetMin = $"{horizontalOffset} {offset}", OffsetMax = $"{horizontalOffset} {offset}" } }, panel);

			#endregion

			CuiHelper.AddUi(player, container);

			if (!CUIPlayers.ContainsKey(player))
				CUIPlayers.Add(player, new PlayerInteraction());
		}
		public bool IsLootingLootable(BasePlayer player, RootData.Lootable lootable)
		{
			if (!CUIPlayers.ContainsKey(player)) return false;

			return CUIPlayers[player]?.Lootable == lootable;
		}

		[ConsoleCommand(CUIPinAppendCommand)]
		private void PinAppendCommand(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			if (PinCache[player].Length >= 4) PinCache[player] = string.Empty;

			PinCache[player] += arg.Args[0];
			DrawCodeLockInput(player);
		}
		[ConsoleCommand(CUIPinApplyCommand)]
		private void PinApplyCommand(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			var interaction = CUIPlayers[player];

			if (string.IsNullOrEmpty(PinCache[player]) || PinCache[player].Length < 4)
			{
				UndrawInteraction(player);
				PinCache[player] = string.Empty;
				return;
			}

			if (PinCache[player] == interaction?.Lootable.Hack.Code)
			{
				var definition = interaction?.Lootable.GetDefinition();
				var interactionDefinition = Config.Interactions[definition.InteractionIndex];

				PlayerLootContainer(player, definition, interaction?.Lootable, interactionDefinition, definition.Panel);
			}
			else
			{
				player.ChatMessage($"Wrong code for that static lootable container.");
			}

			PinCache[player] = string.Empty;

			UndrawInteraction(player);
		}
		[ConsoleCommand(CUIPinUndoCommand)]
		private void PinUndoCommand(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			if (!string.IsNullOrEmpty(PinCache[player])) PinCache[player] = PinCache[player].Substring(0, PinCache[player].Length - 1);
			DrawCodeLockInput(player);
		}

		#endregion

		#region Override

		public class Death
		{
			public static string GetImage(string url, bool isPng = true, ulong imageId = 0)
			{
				try
				{
					var name = GetStringChecksum(url);

					if (string.IsNullOrEmpty(url)) return string.Empty;

					if ((bool)Instance.ImageLibrary.Call("HasImage", name, imageId))
					{
						var success = Instance.ImageLibrary.Call("GetImage", name, imageId);
						return !isPng ? null : (string)success;
					}
					else
					{
						Instance.ImageLibrary.Call("AddImage", url, name, imageId);
						return !isPng ? url : null;
					}
				}
				catch { Instance.RefreshPlugins(); }

				return string.Empty;
			}

			public static CuiRawImageComponent GetRawImage(string url, ulong skin = 0, string color = "", float fade = 0.3f)
			{
				var _url = GetImage(url, isPng: false, imageId: skin);
				var _png = GetImage(url, imageId: skin);

				return new CuiRawImageComponent
				{
					Url = _url,
					Png = _png,
					Color = color,
					FadeIn = fade
				};
			}
		}

		public static string GetStringChecksum(string value)
		{
			if (value.Length <= 3) return null;

			using (var md5 = MD5.Create())
			{
				var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
				return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
			}
		}

		private void OnServerInitialized()
		{
			Instance = this;

			RegisterPermissions();

			timer.Every(20, () =>
			{
				ServerMgr.Instance.StartCoroutine(DoRefillingCalculations());
			});

			LoadContainer();

			Log($"Initialized {Config.Definitions.Count:n0} lootables and {Data.Container.ItemContainer.itemList.Count:n0} persistent container(s). Poggers.");

			CreateBehaviour();

			foreach (var panel in PanelTypes)
			{
				PanelTypesList.Add(panel.Key);
			}
		}
		private void Loaded()
		{
			RefreshPlugins();

			if (ConfigFile == null) ConfigFile = new Core.Configuration.DynamicConfigFile($"{Manager.ConfigPath}/{Name}.json");
			if (DataFile == null) DataFile = Interface.Oxide.DataFileSystem.GetFile($"{Name}_data");

			if (!ConfigFile.Exists()) { ConfigFile.WriteObject(Config ?? (Config = new RootConfig())); }
			else { Config = ConfigFile.ReadObject<RootConfig>(); }

			if (DataFile.Exists()) Data = DataFile.ReadObject<RootData>(); else Data = new RootData();

			if (Instance == null) Instance = this;
		}
		private void Unload()
		{
			foreach (var player in CUIPlayers.ToArray())
			{
				UndrawInteraction(player.Key);
				player.Key.inventory.loot.Clear();
				player.Key.inventory.loot.SendImmediate();
			}

			foreach (var player in Editors)
			{
				UndrawEditor(player.Key, false);
				UndrawEditorInteraction(player.Key);
			}

			foreach (var lootable in Data.Lootables)
			{
				lootable.ClearContainer();
			}

			Data.Lootables.Clear();
			Data.Container.ItemContainer?.Kill();

			ClearBehaviour();
			Instance = null;
		}
		private void OnServerSave()
		{
			SaveContainer();

			ConfigFile.WriteObject(Config ?? (Config = new RootConfig()));
			DataFile.WriteObject(Data ?? (Data = new RootData()));
		}
		public void DoPlayerInput(BasePlayer player, InputState input)
		{
			if (Config == null || Instance == null) return;

			try
			{
				if (Config.AsyncMode)
				{
					if (!CoroutineBuffer.ContainsKey(player.userID)) CoroutineBuffer.Add(player.userID, true);
					else if (CoroutineBuffer[player.userID] && !input.WasJustPressed(BUTTON.USE) && !input.WasJustPressed(BUTTON.RELOAD)) return;

					CoroutineBuffer[player.userID] = true;

					player.StartCoroutine(DoPlayerInputCoroutine(player, input));
				}
				else
				{
					var loot = (RootData.Lootable)null;
					if (LootingPlayers.TryGetValue(player, out loot) && loot.TimeSinceDistanceCheck >= 0.5f)
					{
						if (Vector3.Distance(player.eyes.position, PositionCache[player]) > Config.Distance)
						{
							player.inventory.loot.Clear();
							player.inventory.loot.SendImmediate();
						}

						loot.TimeSinceDistanceCheck = 0;
					}

					var didTrace = GamePhysics.Trace(player.eyes.HeadRay(), 0.0f, out var hitInfo, Config.Distance, ~0);

					if (!didTrace)
					{
						if (CUIPlayers.ContainsKey(player))
						{
							var i = CUIPlayers[player].GetValueOrDefault();
							i.Timer = DateTime.Now;
							i.IsLoadingTimer = false;
							i.Lootable?.CancelHack();

							CUIPlayers[player] = i;
						}

						UndrawEditorInteraction(player);
						UndrawInteraction(player);
						return;
					}

					var obj = hitInfo.collider?.gameObject;

					if (obj == null) return;

					var name = obj.transform.GetRecursiveName().ToLower();

					if (IsInEditMode(player))
					{
						if (input.WasJustPressed(BUTTON.RELOAD))
						{
							input.SwallowButton(BUTTON.RELOAD);

							var editor = GetOrCreateEditor(player);
							if (!editor.IsEditing) DrawEditor(player, EditModes.Settings);
						}

						if (!EditorInteractionPlayers.ContainsKey(player) || (EditorInteractionPlayers.ContainsKey(player) && EditorInteractionPlayers[player] != obj.transform))
						{
							if (!EditorInteractionPlayers.ContainsKey(player)) EditorInteractionPlayers.Add(player, obj.transform);
							else EditorInteractionPlayers[player] = obj.transform;

							DrawEditorInteraction(player, obj.transform);
						}

						if (input.WasJustPressed(BUTTON.USE))
						{
							input.SwallowButton(BUTTON.USE);

							var editor = GetOrCreateEditor(player);
							var definition = GetDefinition(obj.transform);
							Editor.StartEdit(definition ?? new RootConfig.LootableDefinition(obj.transform.name.Split(' ')[0].ToLower(), 2, new List<RootLootableItemDefinition>()), editor.EditingDefinition);
							editor.Transform = obj.transform;

							DrawEditor(player);
						}
					}
					else
					{
						if (Config.MiddleMouseInfo && input.WasJustPressed(BUTTON.FIRE_THIRD) && permission.UserHasPermission(player.UserIDString, AdminPerm) && !player.IsHoldingEntity<BaseEntity>())
						{
							input.SwallowButton(BUTTON.FIRE_THIRD);

							var ids = ZoneManager?.Call<string[]>("GetPlayerZoneIDs", player) ?? new string[0];
							player.ChatMessage(
								$"<size=10>Looking at:</size>\n<color=orange>{name}</color>\n" +
								$"<size=10>Zone(s) [{ids.Length:n0}]:</size>\n<color=orange>{(ids.Length == 0 ? "-" : string.Join(", ", ids))}</color>");
							return;
						}

						var farAway = Vector3.Distance(hitInfo.point, player.transform.position) > Config.Distance;
						var definition = GetDefinition(obj);
						var interaction = definition == null ? null : Config.Interactions[definition.InteractionIndex];
						var lootable = GetLootable(player, obj, definition, false);
						var playerInteraction = !CUIPlayers.ContainsKey(player) ? null : CUIPlayers[player];

						if (definition != null && definition.HasTimer() && lootable == null) lootable = GetLootable(player, obj, definition, true);

						if (lootable != null && !lootable.IsLocked() && definition.HasTimer())
						{
							if (playerInteraction == null && !player.inventory.loot.IsLooting())
							{
								DrawInteraction(player, interaction, definition, lootable, true, reset: false);
							}

							var val = playerInteraction.GetValueOrDefault();

							if (input.IsDown(BUTTON.USE))
							{
								if (!PositionCache.ContainsKey(player)) PositionCache.Add(player, hitInfo.point); else PositionCache[player] = hitInfo.point;

								if (input.WasJustPressed(BUTTON.USE))
								{
									val.Timer = DateTime.Now;
									val.IsLoadingTimer = true;
									CUIPlayers[player] = val;
									DrawInteraction(player, interaction, definition, lootable, true, reset: false);
								}
								else if (val.IsLoadingTimer && val.TimerTime >= definition.Timer)
								{
									input.SwallowButton(BUTTON.USE);

									val.Timer = DateTime.Now;
									val.IsLoadingTimer = false;
									PlayerLootContainer(player, definition, lootable, interaction, definition.Panel);
									CUIPlayers[player] = val;
									UndrawInteraction(player, true);
									return;
								}
							}
							else
							{
								val.IsLoadingTimer = false;
								CUIPlayers[player] = val;

								if (input.WasDown(BUTTON.USE) && !player.inventory.loot.IsLooting())
								{
									DrawInteraction(player, interaction, definition, lootable, false, reset: false);
								}
							}

							CUIPlayers[player] = val;

							if (!TimedPlayers.ContainsKey(player) || (TimedPlayers.ContainsKey(player) && TimedPlayers[player] >= Config.DamageCUIRefreshRate / 2))
							{
								if (input.IsDown(BUTTON.USE) && !player.inventory.loot.IsLooting()) DrawInteraction(player, interaction, definition, lootable, false, reset: false);
								if (TimedPlayers.ContainsKey(player)) TimedPlayers[player] = 0f; else TimedPlayers.Add(player, 0f);
							}

							return;
						}
						else
						{
							var val = playerInteraction.GetValueOrDefault();
							val.Timer = DateTime.Now;
							val.IsLoadingTimer = false;
							CUIPlayers[player] = val;
						}

						if (definition != null && !definition.Rule.IsValid(name, player)) return;

						if (interaction == null && playerInteraction?.Interaction == interaction || playerInteraction?.Lock != lootable?.Lock ||
							playerInteraction?.Interaction != interaction)
						{
							if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }

							UndrawInteraction(player);
						}

						if (!farAway && definition != null && !player.inventory.loot.IsLooting() && !Config.IsFilteredOut(name) && Config.ZoneCheck(player))
						{
							if (definition.Lock != null || definition.Hack != null) lootable = GetLootable(player, obj, definition, true);

							if (!CUIPlayers.ContainsKey(player) && !player.inventory.loot.IsLooting())
							{
								DrawInteraction(player, interaction, definition, lootable, true);
							}
						}
						else
						{
							if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }
							if (CUIPlayers.ContainsKey(player)) UndrawInteraction(player);
						}

						if (definition != null && !farAway)
						{
							if (input.WasJustPressed(BUTTON.USE))
							{
								if (lootable == null) lootable = GetLootable(player, obj, definition, true);
								if (lootable == null) return;

								if (!PositionCache.ContainsKey(player)) PositionCache.Add(player, hitInfo.point); else PositionCache[player] = hitInfo.point;

								if (lootable.IsHackable() && !lootable.IsLocked() && !lootable.IsBusy(player))
								{
									lootable.StartHack(player);
									DrawInteraction(player, interaction, definition, lootable, true, false);

									lootable.Hack.HackingTimer = timer.Every(Config.DamageCUIRefreshRate, () =>
									{
										if (!CUIPlayers.ContainsKey(player) || Vector3.Distance(PositionCache[player], player.transform.position) > Config.Distance)
										{
											if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }
											UndrawInteraction(player);
											return;
										}

										if (lootable.HackHacked())
										{
											var hackNote = ItemManager.CreateByName("note");
											hackNote.text = $"Successfully hacked! The code is: {lootable.Hack.Code}\n\nPress [RELOAD] to open the code input panel.";

											player.GiveItem(hackNote);
											lootable.Hack.HackedTimes++;
											if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }
										}

										var players = CUIPlayers?.ToArray();
										for (int i = 0; i < players.Length; i++)
										{
											if (players[i].Value?.Lootable == lootable)
											{
												DrawInteraction(players[i].Key, interaction, definition, lootable, false, false);
											}
										}
										players = null;
									});

									return;
								}
								else if (lootable.IsHacking()) return;

								if (!lootable.IsLocked() && CUIPlayers[player].Value.TimerTime >= definition.Timer)
								{
									PlayerLootContainer(player, definition, lootable, interaction, definition.Panel);
								}
							}
							else if (input.WasJustPressed(BUTTON.RELOAD) && !lootable.IsLocked() && lootable.IsHackable())
							{
								DrawCodeLockInput(player);
							}
						}
					}
				}
			}
			catch { }
		}
		private void OnPlayerAttack(BasePlayer player, HitInfo info)
		{
			if (Config == null || Instance == null) return;

			var didTrace = GamePhysics.Trace(player.eyes.HeadRay(), 0.0f, out var hitInfo, Config.Distance, ~0);

			if(!didTrace) return;

			var obj = hitInfo.collider?.gameObject;

			if (obj == null) return;

			var lootable = GetLootable(player, obj);
			if (lootable == null) return;
			var definition = lootable.GetDefinition();

			if (!lootable.IsLocked()) return;

			var multiplier = info.damageProperties.fallback.GetMultiplier(HitArea.Head);
			var damageAmount = GetRandomNumber(Config.DamageMultiplierMinimum, Config.DamageMultiplierMaximum) * multiplier;
			lootable.Lock.Health -= damageAmount;

			if (lootable.Lock.Health <= 0)
			{
				lootable.Lock = null;
			}

			if (!TimedPlayers.ContainsKey(player) || (TimedPlayers.ContainsKey(player) && TimedPlayers[player] >= Config.DamageCUIRefreshRate) || !lootable.IsLocked())
			{
				ServerMgr.Instance.Invoke(() =>
				{
					var players = CUIPlayers.ToArray();
					foreach (var cuiPlayer in players)
					{
						if (cuiPlayer.Value?.Lootable == lootable)
						{
							DrawInteraction(cuiPlayer.Key, Config.Interactions[definition.InteractionIndex], definition, lootable, false);
						}
					}
					players = null;
				}, 0.2f);
				TimedPlayers[player] = 0;
			}

			player?.SendConsoleCommand("gametip.hidegametip");
		}
		private void OnPlayerLootEnd(PlayerLoot inventory)
		{
			var player = inventory.baseEntity;
			if (player == null) return;

			UndrawInteraction(player, reset: true);
			if (LootingPlayers.ContainsKey(player)) LootingPlayers.Remove(player);
			if (CUIPlayers.ContainsKey(player)) CUIPlayers[player] = null;
		}
		private void OnPluginLoaded(Plugin name)
		{
			RefreshPlugins();
		}
		private void OnPluginUnloaded(Plugin name)
		{
			RefreshPlugins();
		}
		private void OnNewSave(string filename)
		{
			DataFile.WriteObject(Data = new RootData());
		}
		private void OnEntityKill(TimedExplosive entity)
		{
			var colliders = Facepunch.Pool.GetList<Collider>();
			Vis.Colliders(entity.transform.position, entity.explosionRadius, colliders);

			foreach (var collider in colliders)
			{
				var lootable = GetLootable(null, collider.gameObject);

				if (lootable != null && lootable.IsLocked())
				{
					lootable.Lock.Health -= entity.damageTypes.Sum(x => x.amount);
					if (lootable.Lock.Health <= 0) lootable.Lock = null;
				}
			}

			Facepunch.Pool.FreeList(ref colliders);
		}

		private readonly static string[] CommandSplit = new string[] { " " };
		private readonly static string[] EmptyArgs = new string[0];

		public static void PlayerLootContainer(BasePlayer player, RootConfig.LootableDefinition definition, RootData.Lootable lootable, RootConfig.Interaction interaction, string lootPanel)
		{
			player.serverInput.SwallowButton(BUTTON.USE);

			var cui = Instance.CUIPlayers[player].GetValueOrDefault();

			if (cui.IsProcessingCommand) return;

			if (!string.IsNullOrEmpty(interaction.OpenEffect)) SendEffectTo(interaction.OpenEffect, player);

			if (!string.IsNullOrEmpty(definition.Command))
			{
				cui.IsProcessingCommand = true;
				Instance.CUIPlayers[player] = cui;

				Instance.DrawInteraction(player, interaction, definition, lootable, false, false, reset: false);
				Instance.timer.In(0.5f, () =>
				{
					var credential = ConsoleSystem.Option.Unrestricted.FromConnection(player.net.connection);
					var split = definition.Command.Split(CommandSplit, StringSplitOptions.RemoveEmptyEntries);
					var command = split.Length > 1 ? split[0] : definition.Command;
					ConsoleSystem.Run(credential, command, split.Length <= 1 ? EmptyArgs : split.Skip(1).ToArray());

					cui.IsProcessingCommand = false;
					Instance.CUIPlayers[player] = cui;
					Instance.DrawInteraction(player, interaction, definition, lootable, false, reset: false);
				});
				return;
			}

			player.inventory.loot.Clear();
			player.inventory.loot.PositionChecks = false;
			player.inventory.loot.entitySource = RelationshipManager.ServerInstance;
			player.inventory.loot.itemSource = null;
			player.inventory.loot.AddContainer(lootable.GetContainer(definition.ContainerSize, definition.Liquid, definition.AllowStack));
			player.inventory.loot.MarkDirty();
			player.inventory.loot.SendImmediate();

			player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", lootPanel);

			Instance.QuickSort?.CallHook("OnLootPlayer", player);

			Instance.UndrawInteraction(player, true);

			if (!Instance.LootingPlayers.ContainsKey(player)) Instance.LootingPlayers.Add(player, lootable);
		}
		public static ItemContainer CreateContainer(int capacity = 1, bool liquid = false)
		{
			var container = new ItemContainer
			{
				isServer = true,
				allowedContents = liquid ? ItemContainer.ContentsType.Liquid : ItemContainer.ContentsType.Generic,
				capacity = capacity
			};

			container.GiveUID();

			return container;
		}

		public Dictionary<ulong, bool> CoroutineBuffer { get; private set; } = new();
		public IEnumerator DoPlayerInputCoroutine(BasePlayer player, InputState input)
		{
			var loot = (RootData.Lootable)null;
			if (LootingPlayers.TryGetValue(player, out loot) && loot?.TimeSinceDistanceCheck >= 0.5f)
			{
				if (Vector3.Distance(player.eyes.position, PositionCache[player]) > Config.Distance)
				{
					player.inventory.loot.Clear();
					player.inventory.loot.SendImmediate();
				}

				loot.TimeSinceDistanceCheck = 0;
			}

			if (!GamePhysics.Trace(player.eyes.HeadRay(), 0.0f, out var hitInfo, Config.Distance, ~0))
			{
				if (CUIPlayers.ContainsKey(player))
				{
					var i = CUIPlayers[player].GetValueOrDefault();
					i.Timer = DateTime.Now;
					i.IsLoadingTimer = false;
					i.Lootable?.CancelHack();

					CUIPlayers[player] = i;
				}

				UndrawEditorInteraction(player);
				UndrawInteraction(player);

				CoroutineBuffer[player.userID] = false;
				yield break;
			}

			var obj = hitInfo.collider?.gameObject;

			if (obj== null)
			{
				CoroutineBuffer[player.userID] = false;
				yield break;
			}

			var name = obj.transform.GetRecursiveName().ToLower();

			try
			{
				if (IsInEditMode(player))
				{
					if (input.WasJustPressed(BUTTON.RELOAD))
					{
						input.SwallowButton(BUTTON.RELOAD);

						var editor = GetOrCreateEditor(player);
						if (!editor.IsEditing) DrawEditor(player, EditModes.Settings);
					}

					if (obj != null)
					{
						if (!EditorInteractionPlayers.ContainsKey(player) || (EditorInteractionPlayers.ContainsKey(player) && EditorInteractionPlayers[player] != obj.transform))
						{
							if (!EditorInteractionPlayers.ContainsKey(player)) EditorInteractionPlayers.Add(player, obj.transform);
							else EditorInteractionPlayers[player] = obj.transform;

							DrawEditorInteraction(player, obj.transform);
						}
					}

					if (input.WasJustPressed(BUTTON.USE))
					{
						input.SwallowButton(BUTTON.USE);

						var editor = GetOrCreateEditor(player);
						var definition = GetDefinition(obj.transform);
						Editor.StartEdit(definition ?? new RootConfig.LootableDefinition(obj.transform.name.Split(' ')[0].ToLower(), 2, new List<RootLootableItemDefinition>()), editor.EditingDefinition);
						editor.Transform = obj.transform;

						DrawEditor(player);
					}
				}
				else
				{
					if (Config.MiddleMouseInfo && input.WasJustPressed(BUTTON.FIRE_THIRD) && permission.UserHasPermission(player.UserIDString, AdminPerm) && !player.IsHoldingEntity<BaseEntity>())
					{
						input.SwallowButton(BUTTON.FIRE_THIRD);

						var ids = ZoneManager?.Call<string[]>("GetPlayerZoneIDs", player) ?? new string[0];
						player.ChatMessage(
							$"<size=10>Looking at:</size>\n<color=orange>{name}</color>\n" +
							$"<size=10>Zone(s) [{ids.Length:n0}]:</size>\n<color=orange>{(ids.Length == 0 ? "-" : string.Join(", ", ids))}</color>");

						CoroutineBuffer[player.userID] = false;
						yield break;
					}

					var farAway = Vector3.Distance(hitInfo.point, player.transform.position) > Config.Distance;
					var definition = GetDefinition(obj);
					var interaction = definition == null ? null : Config.Interactions[definition.InteractionIndex];
					var lootable = GetLootable(player, obj, definition, false);
					var playerInteraction = !CUIPlayers.ContainsKey(player) ? null : CUIPlayers[player];

					if (definition != null && !definition.Rule.IsValid(name, player))
					{
						CoroutineBuffer[player.userID] = false;
						yield break;
					}

					if (definition != null && definition.HasTimer() && lootable == null) lootable = GetLootable(player, obj, definition, true);

					if (lootable != null && !lootable.IsLocked() && definition.HasTimer())
					{
						if (playerInteraction == null && !player.inventory.loot.IsLooting())
						{
							DrawInteraction(player, interaction, definition, lootable, true, reset: false);
						}

						var val = playerInteraction.GetValueOrDefault();

						if (input.IsDown(BUTTON.USE))
						{
							if (!PositionCache.ContainsKey(player)) PositionCache.Add(player, hitInfo.point); else PositionCache[player] = hitInfo.point;

							if (input.WasJustPressed(BUTTON.USE) && !player.inventory.loot.IsLooting())
							{
								input.SwallowButton(BUTTON.RELOAD);

								val.Timer = DateTime.Now;
								val.IsLoadingTimer = true;
								CUIPlayers[player] = val;
								DrawInteraction(player, interaction, definition, lootable, true, reset: false);
							}
							else if (val.IsLoadingTimer && val.TimerTime >= definition.Timer)
							{
								val.Timer = DateTime.Now;
								val.IsLoadingTimer = false;
								PlayerLootContainer(player, definition, lootable, interaction, definition.Panel);
								if (!string.IsNullOrEmpty(interaction.OpenEffect)) SendEffectTo(interaction.OpenEffect, player);
								CUIPlayers[player] = null;
								UndrawInteraction(player);
								CoroutineBuffer[player.userID] = false;
								yield break;
							}
						}
						else
						{
							val.IsLoadingTimer = false;
							CUIPlayers[player] = val;

							if (input.WasDown(BUTTON.USE) && !player.inventory.loot.IsLooting())
							{
								DrawInteraction(player, interaction, definition, lootable, false, reset: false);
							}
						}

						if (!TimedPlayers.ContainsKey(player) ||
							(TimedPlayers.ContainsKey(player) &&
							TimedPlayers[player] >= Config.DamageCUIRefreshRate / 2))
						{
							if (input.IsDown(BUTTON.USE) && !player.inventory.loot.IsLooting()) DrawInteraction(player, interaction, definition, lootable, false, reset: false);
							if (TimedPlayers.ContainsKey(player)) TimedPlayers[player] = 0f; else TimedPlayers.Add(player, 0f);
						}

						CoroutineBuffer[player.userID] = false;
						yield break;
					}
					else
					{
						var val = playerInteraction.GetValueOrDefault();
						val.Timer = DateTime.Now;
						val.IsLoadingTimer = false;
						CUIPlayers[player] = val;
					}

					if (interaction == null && playerInteraction?.Interaction == interaction || playerInteraction?.Lock != lootable?.Lock ||
						playerInteraction?.Interaction != interaction)
					{
						if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }
						UndrawInteraction(player);
					}

					if (!farAway && definition != null && !player.inventory.loot.IsLooting() && !Config.IsFilteredOut(name) && Config.ZoneCheck(player))
					{
						if (definition.Lock != null || definition.Hack != null) lootable = GetLootable(player, obj, definition, true);

						if (!CUIPlayers.ContainsKey(player) && !player.inventory.loot.IsLooting()) DrawInteraction(player, interaction, definition, lootable, true);
					}
					else
					{
						if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }
					}

					if (definition != null && !farAway)
					{
						if (input.WasJustPressed(BUTTON.USE))
						{
							if (lootable == null) lootable = GetLootable(player, obj, definition, true);
							if (lootable == null)
							{
								CoroutineBuffer[player.userID] = false;
								yield break;
							}

							PositionCache[player] = hitInfo.point;

							if (lootable.IsHackable() && !lootable.IsLocked() && !lootable.IsBusy(player))
							{
								lootable.StartHack(player);
								DrawInteraction(player, interaction, definition, lootable, true, false);

								lootable.Hack.HackingTimer = timer.Every(Config.DamageCUIRefreshRate, () =>
								{
									if (!CUIPlayers.ContainsKey(player) || Vector3.Distance(PositionCache[player], player.transform.position) > Config.Distance)
									{
										if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }
										UndrawInteraction(player);
										return;
									}

									if (lootable.HackHacked())
									{
										var hackNote = ItemManager.CreateByName("note");
										hackNote.text = $"Successfully hacked! The code is: {lootable.Hack.Code}\n\nPress [RELOAD] to open the code input panel.";

										player.GiveItem(hackNote);
										lootable.Hack.HackedTimes++;
										if (lootable != null && lootable.HackingPlayer == player) { lootable.CancelHack(); }
									}

									var players = CUIPlayers?.ToArray();
									for (int i = 0; i < players.Length; i++)
									{
										if (players[i].Value?.Lootable == lootable)
										{
											DrawInteraction(players[i].Key, interaction, definition, lootable, false, false);
										}
									}
									players = null;
								});

								CoroutineBuffer[player.userID] = false;
								yield break;
							}
							else if (lootable.IsHacking())
							{
								CoroutineBuffer[player.userID] = false;
								yield break;
							}

							if (!lootable.IsLocked() && !player.inventory.loot.IsLooting() && CUIPlayers[player].Value.TimerTime >= definition.Timer)
							{
								PlayerLootContainer(player, definition, lootable, interaction, definition.Panel);
								if (!string.IsNullOrEmpty(interaction.OpenEffect)) SendEffectTo(interaction.OpenEffect, player);
							}
						}
						else if (input.WasJustPressed(BUTTON.RELOAD) && !lootable.IsLocked() && lootable.IsHackable())
						{
							DrawCodeLockInput(player);
						}
					}
				}
			}
			catch (Exception ex) { Puts($"Error: {ex}"); }
			CoroutineBuffer[player.userID] = false;
		}
		public IEnumerator DoRefillingCalculations()
		{
			var lootables = Facepunch.Pool.GetList<RootData.Lootable>();
			lootables.AddRange(Data.Lootables);
			var players = CUIPlayers.ToArray();

			foreach (var lootable in lootables)
			{
				var definition = lootable.GetDefinition();
				if (definition.Persistent) continue;

				var now = DateTime.Now;

				if (definition.Hack != null && (now - new DateTime(lootable.LastHackWipeTick)).TotalMinutes > definition.Hack.CodeResetRate)
				{
					lootable.ApplyHack(definition, true);

					foreach (var player in players)
					{
						if (player.Value?.Lootable == lootable) UndrawInteraction(player.Key);
					}
				}

				if ((now - new DateTime(lootable.LastRefillTick)).TotalMinutes > definition.Rule.RefillRate)
				{
					lootable.ClearContainer();

					if (definition.Hack != null)
					{
						lootable.CancelHack();

						lootable.ApplyLock(definition);
						lootable.RandomizeContents(definition);
						lootable.LastRefillTick = DateTime.Now.Ticks;
					}
					else Data.Lootables.Remove(lootable);

					foreach (var player in players)
					{
						if (player.Value?.Lootable == lootable) UndrawInteraction(player.Key);
					}
				}

				yield return null;
			}

			Facepunch.Pool.FreeList(ref lootables);
			players = null;

			yield return null;
		}

		#endregion

		#region API

		private JObject[] GetLootables()
		{
			var list = new List<JObject>();

			foreach (var lootable in Config.Definitions)
			{
				list.Add(ParseLootable(lootable));
			}

			var result = list.ToArray();
			list.Clear();
			return result;
		}
		private bool CreateOrEditLootable(JObject definition, bool saveNew = false)
		{
			try
			{
				var prefabFilter = definition["PrefabFilter"].ToObject<string>();
				var uniqueId = definition["uniqueId"] == null ? string.Empty : definition["uniqueId"].ToObject<string>();
				var position = Vector3.zero;
				if (!string.IsNullOrEmpty(uniqueId))
				{
					var split = uniqueId.Split('_');

					if (split.Length == 3) position = new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));

					Array.Clear(split, 0, split.Length);
				}
				var existentDefinition = GetDefinition(prefabFilter, position, !string.IsNullOrEmpty(uniqueId));
				var lootable = saveNew ? new RootConfig.LootableDefinition() : existentDefinition ?? new RootConfig.LootableDefinition();
				ApplyLootable(definition, lootable);

				if (existentDefinition == null) saveNew = true;

				if (saveNew)
				{
					Config.Definitions.Add(lootable);
				}

				return true;
			}
			catch (Exception exception)
			{
				Puts($"CreateOrEditLootable failed: {exception}");
				return false;
			}
		}
		private bool DeleteLootable(string prefabFilter, string uniqueId = null)
		{
			var position = Vector3.zero;
			if (!string.IsNullOrEmpty(uniqueId))
			{
				var split = uniqueId.Split('_');

				if (split.Length == 3) position = new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));

				Array.Clear(split, 0, split.Length);
			}

			var existentDefinition = GetDefinition(prefabFilter, position, !string.IsNullOrEmpty(uniqueId));

			if (existentDefinition != null)
			{
				Config.Definitions.Remove(existentDefinition);
				return true;
			}

			return false;
		}

		private void EmulateAttack(BasePlayer player, GameObject gameObject, float damageMin, float damageMax)
		{
			var lootable = GetLootable(player, gameObject);
			if (lootable == null) return;
			var definition = lootable.GetDefinition();

			if (!lootable.IsLocked()) return;

			var damageAmount = GetRandomNumber(damageMin, damageMax);
			lootable.Lock.Health -= damageAmount;

			if (lootable.Lock.Health <= 0)
			{
				lootable.Lock = null;
			}

			if (!TimedPlayers.ContainsKey(player) || (TimedPlayers.ContainsKey(player) && TimedPlayers[player] >= Config.DamageCUIRefreshRate) || !lootable.IsLocked())
			{
				ServerMgr.Instance.Invoke(() =>
				{
					var players = CUIPlayers.ToArray();
					foreach (var cuiPlayer in players)
					{
						if (cuiPlayer.Value?.Lootable == lootable)
						{
							DrawInteraction(cuiPlayer.Key, Config.Interactions[definition.InteractionIndex], definition, lootable, false);
						}
					}
					players = null;
				}, 0.2f);
				TimedPlayers[player] = 0;
			}
		}

		internal JObject ParseLootable(RootConfig.LootableDefinition definition)
		{
			return JObject.FromObject(definition);
		}
		internal void ApplyLootable(JObject jObject, RootConfig.LootableDefinition definition)
		{
			var uniqueId = jObject[nameof(definition.UniqueId)];
			var prefabFilter = jObject[nameof(definition.PrefabFilter)];
			var interactionIndex = jObject[nameof(definition.InteractionIndex)];
			var containerSize = jObject[nameof(definition.ContainerSize)];
			var allowStack = jObject[nameof(definition.AllowStack)];
			var liquid = jObject[nameof(definition.Liquid)];
			var persistent = jObject[nameof(definition.Persistent)];
			var timer = jObject[nameof(definition.Timer)];
			var @lock = jObject[nameof(definition.Lock)];
			var hack = jObject[nameof(definition.Hack)];
			var rule = jObject[nameof(definition.Rule)];
			var contents = jObject[nameof(definition.Contents)];

			if (uniqueId != null) definition.UniqueId = uniqueId.ToObject<string>();
			if (prefabFilter != null) definition.PrefabFilter = prefabFilter.ToObject<string>();
			if (interactionIndex != null) definition.InteractionIndex = interactionIndex.ToObject<int>();
			if (containerSize != null) definition.ContainerSize = containerSize.ToObject<int>();
			if (allowStack != null) definition.AllowStack = allowStack.ToObject<bool>();
			if (liquid != null) definition.Liquid = liquid.ToObject<bool>();
			if (persistent != null) definition.Persistent = persistent.ToObject<bool>();
			if (timer != null) definition.Timer = timer.ToObject<float>();
			if (@lock != null) definition.Lock = @lock.ToObject<RootLock>();
			if (hack != null) definition.Hack = hack.ToObject<RootHack>();
			if (rule != null) definition.Rule = rule.ToObject<RootConfig.Rule>();
			if (contents != null) definition.Contents = contents.ToObject<List<RootLootableItemDefinition>>();
		}

		#endregion

		#region Helpers

		public RootConfig.LootableDefinition GetDefinition(Transform reference)
		{
			if (reference == null) return null;

			return reference == null ? null : GetDefinition(reference.GetRecursiveName().ToLower(), reference.position);
		}
		public RootConfig.LootableDefinition GetDefinition(GameObject reference)
		{
			return GetDefinition(reference.transform);
		}
		public RootConfig.LootableDefinition GetDefinition(string reference, Vector3 position, bool isUnique = false)
		{
			foreach (var definition in Config.Definitions)
			{
				if (string.IsNullOrEmpty(definition.UniqueId)) continue;

				var split = definition.UniqueId.Split('_');
				if (reference.Contains(definition.PrefabFilter) && AreVectorsEqual(position, new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]))))
				{
					Array.Clear(split, 0, split.Length);
					return definition;
				}
				Array.Clear(split, 0, split.Length);
			}

			if (!isUnique)
			{
				foreach (var definition in Config.Definitions)
				{
					if (!definition.IsUnique() && reference.Contains(definition.PrefabFilter)) return definition;
				}
			}

			return null;
		}

		internal bool AreVectorsEqual(Vector3 source, Vector3 target)
		{
			if ($"{source.x:0.0}" == $"{target.x:0.0}" &&
				 $"{source.y:0.0}" == $"{target.y:0.0}" &&
				 $"{source.z:0.0}" == $"{target.z:0.0}") return true;

			return false;
		}

		public RootData.Lootable GetLootable(BasePlayer player, GameObject gameObject, RootConfig.LootableDefinition definition = null, bool createOnLocked = true)
		{
			var name = gameObject.transform?.GetRecursiveName().ToLower();
			if (Config.IsFilteredOut(name) || !Config.ZoneCheck(player)) return null;

			var lootable = GetLootable(name, gameObject.transform.position, gameObject);
			if (lootable == null)
			{
				if (definition == null) definition = GetDefinition(gameObject);
				if (definition == null) return null;

				if (definition.Rule != null && !definition.Rule.IsValid(name, player))
					return null;

				if (!createOnLocked) return null;

				Data.Lootables.Add(lootable = new RootData.Lootable()
				{
					Name = name,
					Position = gameObject.transform.position,
					LastRefillTick = DateTime.Now.Ticks,
					Object = gameObject
				});

				if (definition.Persistent)
				{
					lootable.Container = GetContainer(gameObject.transform.position);

					if (lootable.Container == null)
					{
						lootable.RandomizeContents(definition);
						SetContainer(gameObject.transform.position, lootable.GetContainer());
					}
				}
				else
				{
					lootable.RandomizeContents(definition);
				}

				lootable.ApplyLock(definition);
				lootable.ApplyHack(definition);
			}

			if (gameObject != null && definition != null && definition.Rule != null && (!definition.Rule.IsValid(gameObject.transform.GetRecursiveName(), player)))
				return null;

			return lootable;
		}
		public RootData.Lootable GetLootable(string name, Vector3 position, GameObject reference = null)
		{
			foreach (var lootable in Data.Lootables)
			{
				if (reference != null && lootable.Object == reference) { return lootable; }

				switch (string.IsNullOrEmpty(name))
				{
					case true:
						if (lootable.Position == position) return lootable; break;
					case false:
						if (lootable.Name == name && lootable.Position == position) return lootable; break;
				}
			}

			return null;
		}

		public void Print(object message, BasePlayer player = null)
		{
			if (player == null) PrintToChat($"<color=orange>{Name}</color>: {message}");
			else PrintToChat(player, $"<color=orange>{Name}</color> (OY): {message}");
		}
		public void Log(object message, BasePlayer player = null)
		{
			Puts($"{(player == null ? "" : $"({player.displayName}) ")}{message}");
		}

		public string Join(string[] array, string separator, string lastSeparator = null)
		{
			if (string.IsNullOrEmpty(lastSeparator))
			{
				lastSeparator = separator;
			}

			if (array.Length == 0)
			{
				return string.Empty;
			}

			if (array.Length == 1)
			{
				return array[0];
			}

			var list = Facepunch.Pool.GetList<string>();
			for (int i = 0; i < array.Length - 1; i++)
			{
				list.Add(array[i]);
			}

			var result = list.ToArray();
			Facepunch.Pool.FreeList(ref list);

			return string.Join(separator, result) + $"{lastSeparator}{array[array.Length - 1]}";
		}

		public float GetRandomNumber(float minimum, float maximum)
		{
			return (float)Randomizer.NextDouble() * (maximum - minimum) + minimum;
		}
		public float Scale(float oldValue, float oldMin, float oldMax, float newMin, float newMax)
		{
			var oldRange = (oldMax - oldMin);
			var newRange = (newMax - newMin);
			var newValue = (((oldValue - oldMin) * newRange) / oldRange) + newMin;

			return newValue;
		}
		public float Percentage(int value, int total, float percent = 100f)
		{
			return (float)Math.Round((double)percent * value) / total;
		}
		public int Clamp(int value, int min, int max)
		{
			if (value < min)
			{
				value = min;
			}
			else if (value > max)
			{
				value = max;
			}

			return value;
		}
		public float Clamp(float value, float min, float max)
		{
			if (value < min)
			{
				value = min;
			}
			else if (value > max)
			{
				value = max;
			}

			return value;
		}

		public static void SendEffectTo(string effect, BasePlayer player)
		{
			if (player == null) return;

			var effectInstance = new Effect();
			effectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
			effectInstance.pooledstringid = StringPool.Get(effect);
			NetWrite netWrite = Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.Effect);
			effectInstance.WriteToStream(netWrite);
			netWrite.Send(new SendInfo(player.net.connection));
			effectInstance.Clear();
		}

		#endregion

		#region Config

		public Core.Configuration.DynamicConfigFile ConfigFile { get; set; }
		public Core.Configuration.DynamicConfigFile DataFile { get; set; }

		public new RootConfig Config { get; set; } = new RootConfig();
		public RootData Data { get; set; } = new RootData();

		public class RootLootableItemDefinition
		{
			public string ShortName { get; set; }
			public string CustomName { get; set; }
			public ulong SkinId { get; set; }

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public string Text { get; set; }
			public bool UseRandomSkins { get; set; }
			public ulong[] RandomSkins { get; set; } = new ulong[0];
			public int MinimumAmount { get; set; } = 1;
			public int MaximumAmount { get; set; } = 1;
			public float ConditionMinimumAmount { get; set; } = 1;
			public float ConditionMaximumAmount { get; set; } = 1;
			public int SpawnChanceTimes { get; set; } = 2;
			public int SpawnChanceScale { get; set; } = 5;
			public int BlueprintChanceTimes { get; set; } = 0;
			public int BlueprintChanceScale { get; set; } = 0;
			public List<RootLootableItemDefinition> Contents { get; set; } = new List<RootLootableItemDefinition>();
		}
		public class RootLootableItem
		{
			public string ShortName { get; set; }
			public string CustomName { get; set; }
			public int Amount { get; set; }
			public float Condition { get; set; }
			public ulong SkinId { get; set; }
			public string Text { get; set; }

			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public bool IsBlueprint { get; set; }

			public List<RootLootableItem> Contents { get; set; } = new List<RootLootableItem>();
		}
		public class RootLock
		{
			public float Health { get; set; } = 250f;
		}
		public class RootHack
		{
			[JsonProperty("Wait Time (in Seconds)")]
			public float WaitTime { get; set; } = 10f;

			[JsonProperty("Code Resetting Rate (in Minutes)")]
			public float CodeResetRate { get; set; } = 300f;

			[JsonIgnore] public string Code { get; set; } = "0000";
			[JsonIgnore] public bool IsHacking { get; set; } = false;
			[JsonIgnore] public long HackStartTick { get; set; }
			[JsonIgnore] public Timer HackingTimer { get; set; }
			[JsonIgnore] public int HackedTimes { get; set; } = 1;
		}

		public class RootConfig
		{
			public bool AsyncMode { get; set; } = false;
			public bool MiddleMouseInfo { get; set; } = false;
			public float Distance { get; set; } = 2.5f;
			public float ItemAmountMultiplier { get; set; } = 1f;
			public float LootableHealthMultiplier { get; set; } = 1f;
			public float ShadowOpacity { get; set; } = 0.075f;
			public string LockName { get; set; } = "Locked";
			public string HackName { get; set; } = "Hack";
			public string HackingName { get; set; } = "Hacking...";
			public string CommandName { get; set; } = "Executing...";
			public string CodeInputTitle { get; set; } = "Lootable Code";
			public string BusyName { get; set; } = "Busy";
			public string InteractionPrefix { get; set; } = "<color=#ff8115><size=16><b>[E] </b></size></color>";
			public string HackingPrefix { get; set; } = "<color=#ff8115><size=16><b>[R] </b></size></color>ENTER CODE";
			public UrlDefinitions Urls { get; set; } = new UrlDefinitions();
			public bool ShowLockedLootableApproximateCount { get; set; } = true;
			public float DamageMultiplierMinimum { get; set; } = 6.75f;
			public float DamageMultiplierMaximum { get; set; } = 9.25f;
			public float DamageCUIRefreshRate { get; set; } = 0.35f;
			public float WaitingCUIRefreshRate { get; set; } = 0.35f;
			public string[] EnforcedFilters { get; set; } = new string[0];
			public string[] EnforcedInZone { get; set; } = new string[0];
			public string[] EnforcedNotInZone { get; set; } = new string[0];
			public List<Interaction> Interactions { get; set; } = new List<Interaction>();
			public List<LootableDefinition> Definitions { get; set; } = new List<LootableDefinition>();

			public bool IsFilteredOut(string objectName)
			{
				if (string.IsNullOrEmpty(objectName)) return false;

				if (EnforcedFilters != null && EnforcedFilters.Length > 0)
				{
					foreach (var item in EnforcedFilters)
					{
						if (objectName.Contains(item)) return true;
					}
				}

				return false;
			}
			public bool ZoneCheck(BasePlayer player)
			{
				if (player == null) return true;

				if (Instance.ZoneManager != null)
				{
					if (EnforcedInZone != null && EnforcedInZone.Length > 0)
					{
						foreach (var zone in EnforcedInZone)
						{
							if (!Instance.ZoneManager.Call<bool>("IsPlayerInZone", zone, player)) return false;
						}
					}

					if (EnforcedNotInZone != null && EnforcedNotInZone.Length > 0)
					{
						foreach (var zone in EnforcedNotInZone)
						{
							if (Instance.ZoneManager.Call<bool>("IsPlayerInZone", zone, player)) return false;
						}
					}
				}

				return true;
			}

			public class LootableDefinition
			{
				[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
				public string UniqueId { get; set; }
				public string PrefabFilter { get; set; }

				[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
				public string Command { get; set; }

				[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
				public string Panel { get; set; } = "genericsmall";

				public int InteractionIndex { get; set; } = 0;
				public int ContainerSize { get; set; } = 4;
				public bool AllowStack { get; set; } = false;
				public bool Liquid { get; set; } = false;
				public bool Persistent { get; set; } = false;
				public float Timer { get; set; } = 0f;
				public RootLock Lock { get; set; } = null;
				public RootHack Hack { get; set; } = null;
				public Rule Rule { get; set; } = new Rule();
				public List<RootLootableItemDefinition> Contents { get; set; } = new List<RootLootableItemDefinition>();

				public bool HasTimer()
				{
					return Timer > 0f;
				}
				public bool IsUnique()
				{
					return !string.IsNullOrEmpty(UniqueId);
				}

				public LootableDefinition() { }
				public LootableDefinition(
					string prefabFilter,
					int containerSize,
					List<RootLootableItemDefinition> lootableItems)
				{
					PrefabFilter = prefabFilter;
					ContainerSize = containerSize;
					Contents = lootableItems;
				}
			}
			public class Interaction
			{
				public string Icon { get; set; } = "https://raulssorban.tv/wp-content/uploads/plugins/sl_open.png";
				public string Text { get; set; } = "Open";
				public string OpenEffect { get; set; }
			}
			public class Rule
			{
				public float RefillRate { get; set; } = 40f;
				public List<string> OnlyIfParentFilter { get; set; } = new List<string>();
				public List<string> OnlyIfNotParentFilter { get; set; } = new List<string>();
				public List<string> OnlyIfInZone { get; set; } = new List<string>();
				public List<string> OnlyIfNotInZone { get; set; } = new List<string>();

				public bool IsValid(string objectName, BasePlayer player)
				{
					if (player == null) return true;

					if (Instance.ZoneManager != null)
					{
						if (OnlyIfInZone != null && OnlyIfInZone.Count > 0)
						{
							foreach (var zone in OnlyIfInZone)
							{
								if (!Instance.ZoneManager.Call<bool>("IsPlayerInZone", zone, player)) return false;
							}
						}

						if (OnlyIfNotInZone != null && OnlyIfNotInZone.Count > 0)
						{
							foreach (var zone in OnlyIfNotInZone)
							{
								if (Instance.ZoneManager.Call<bool>("IsPlayerInZone", zone, player)) return false;
							}
						}
					}

					if (string.IsNullOrEmpty(objectName)) return true;

					if (OnlyIfParentFilter != null && OnlyIfParentFilter.Count > 0)
					{
						foreach (var item in OnlyIfParentFilter)
						{
							if (!objectName.Contains(item)) return false;
						}
					}

					if (OnlyIfNotParentFilter != null && OnlyIfNotParentFilter.Count > 0)
					{
						foreach (var item in OnlyIfNotParentFilter)
						{
							if (objectName.Contains(item)) return false;
						}
					}

					return true;
				}
			}
			public class UrlDefinitions
			{
				public string DotUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_dot.png";
				public string ShadowUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_fuzz.png";
				public string LockUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_lock.png";
				public string HackUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_hack.png";
				public string HackingUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_hacking.png";
				public string CheckUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_check.png";
				public string BusyUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_busy.png";
				public string LoadingUrl { get; set; } = @"https://raulssorban.tv/wp-content/uploads/plugins/sl_loading.png";
			}
		}
		public class RootData
		{
			public byte[] ContainerData { get; set; }

			[JsonIgnore] public Container Container { get; set; }
			[JsonIgnore] public List<Lootable> Lootables { get; set; } = new List<Lootable>();

			public bool IsLootingLootable(BasePlayer player)
			{
				if (!player.inventory.loot.IsLooting() || !Instance.PositionCache.ContainsKey(player)) return false;

				var lootable = (Lootable)null;
				foreach (var dataLootable in Instance.Data.Lootables)
				{
					if (dataLootable.ContainerExists() && dataLootable.GetContainer().uid == player.inventory.loot.containers[0].uid) { lootable = dataLootable; break; }
				}

				if (lootable == null) return false;

				return Vector3.Distance(Instance.PositionCache[player], player.transform.position) <= Instance.Config.Distance;
			}

			public class Lootable
			{
				private static System.Random Random { get; } = new System.Random();

				public string Name { get; set; }
				public Vector3 Position { get; set; }
				public List<RootLootableItem> Contents { get; set; } = new List<RootLootableItem>();
				public RootLock Lock { get; set; }
				public RootHack Hack { get; set; }
				public long LastRefillTick { get; set; }
				public long LastHackWipeTick { get; set; }
				public BasePlayer HackingPlayer { get; set; }

				[JsonIgnore] public TimeSince TimeSinceDistanceCheck { get; set; }
				[JsonIgnore] public GameObject Object { get; set; }

				public ItemContainer Container { get; set; }
				public ItemContainer GetContainer(int capacity = 6, bool liquid = false, bool allowStack = true, bool preview = false)
				{
					if (!preview && Container != null)
					{
						Container.SetFlag(ItemContainer.Flag.IsLocked, false);

						if (!IsPersistent)
						{
							Container.SetFlag(ItemContainer.Flag.NoItemInput, true);
							Container.canAcceptItem = new System.Func<Item, int, bool>((item, slot) => false);
						}
						else
						{
							Container.SetFlag(ItemContainer.Flag.NoItemInput, false);
							Container.canAcceptItem = null;
						}

						return Container;
					}

					Container = Container ?? (Container = CreateContainer(capacity, liquid));
					if (preview)
					{
						Container.Clear();
						Container.SetFlag(ItemContainer.Flag.NoItemInput, false);
						Container.canAcceptItem = null;
					}

					foreach (var content in Contents)
					{
						if (content.Amount <= 0 || ItemManager.FindItemDefinition(content.ShortName) == null) continue;
						if (Container.itemList.Count == Container.capacity) break;

						var item = ItemManager.CreateByName(content.IsBlueprint ? "blueprintbase" : content.ShortName, content.IsBlueprint ? 1 : content.Amount, content.IsBlueprint ? 0UL : content.SkinId);

						if (content.IsBlueprint)
						{
							item.blueprintTarget = ItemManager.FindItemDefinition(content.ShortName).itemid;
						}
						else
						{
							if (!string.IsNullOrEmpty(content.CustomName)) item.name = content.CustomName;
							if (!string.IsNullOrEmpty(content.Text)) item.text = content.Text;
							if (item.hasCondition) item.condition = Instance.Scale(content.Condition, 0f, 1f, 0f, item.maxCondition);
						}

						item.MoveToContainer(Container, allowStack: allowStack);

						MoveRecursiveSubContents(item, content.Contents, content.IsBlueprint ? false : allowStack);
					}

					if (!IsPersistent)
					{
						Container.SetFlag(ItemContainer.Flag.NoItemInput, true);
						Container.canAcceptItem = new System.Func<Item, int, bool>((item, slot) => false);
					}

					if (preview) Container.SetFlag(ItemContainer.Flag.IsLocked, true);

					return Container;
				}
				public bool IsContainer(ItemContainer container)
				{
					if (Container == null) return false;
					return Container.uid == container.uid;
				}
				public bool IsPersistent
				{
					get
					{
						var definition = GetDefinition();
						if (definition == null) return false;

						return definition.Persistent;
					}
				}
				public void MoveRecursiveSubContents(Item target, List<RootLootableItem> contents, bool allowStack)
				{
					if (target.contents == null) return;

					foreach (var content in contents)
					{
						if (ItemManager.FindItemDefinition(content.ShortName) == null) continue;
						if (target.contents.itemList.Count == target.contents.capacity) break;

						var item = ItemManager.CreateByName(content.ShortName, content.Amount, content.SkinId);
						if (!string.IsNullOrEmpty(content.CustomName)) item.name = content.CustomName;
						if (!string.IsNullOrEmpty(content.Text)) item.text = content.Text;
						if (item.hasCondition) item.condition = Instance.Scale(content.Condition, 0f, 1f, 0f, item.maxCondition);

						item.MoveToContainer(target.contents, allowStack: allowStack);

						MoveRecursiveSubContents(item, content.Contents, allowStack);
					}
				}

				public void RandomizeContents(RootConfig.LootableDefinition definition)
				{
					Contents.Clear();

					foreach (var contentsItem in Shuffle(definition.Contents))
						if (Instance.Randomizer.Next(0, contentsItem.SpawnChanceScale) <= contentsItem.SpawnChanceTimes - 1)
						{
							var lootable = new RootLootableItem
							{
								ShortName = contentsItem.ShortName,
								CustomName = contentsItem.CustomName,
								Amount = (int)(Random.Next(contentsItem.MinimumAmount == 0 ? Random.Next(0, 2) : contentsItem.MinimumAmount, contentsItem.MaximumAmount) * Instance.Config.ItemAmountMultiplier),
								Condition = Random.Next((int)(contentsItem.ConditionMinimumAmount * 100f), (int)(contentsItem.ConditionMaximumAmount * 100f)) / 100f,
								SkinId = contentsItem.UseRandomSkins ? contentsItem.RandomSkins[Random.Next(0, contentsItem.RandomSkins.Length - 1)] : contentsItem.SkinId,
								Text = contentsItem.Text,
								IsBlueprint = contentsItem.BlueprintChanceScale == 0 && contentsItem.BlueprintChanceTimes == 0 ? false : Instance.Randomizer.Next(0, contentsItem.BlueprintChanceScale) <= contentsItem.BlueprintChanceTimes - 1
							};

							Contents.Add(lootable);

							RecursiveRandomizeContents(contentsItem, lootable);

							if (Contents.Count > definition.ContainerSize) break;
						}
				}
				public void RecursiveRandomizeContents(RootLootableItemDefinition definition, RootLootableItem target)
				{
					foreach (var contentsItem in Shuffle(definition.Contents))
						if (Instance.Randomizer.Next(0, contentsItem.SpawnChanceScale) <= contentsItem.SpawnChanceTimes - 1)
						{
							var lootable = new RootLootableItem
							{
								ShortName = contentsItem.ShortName,
								CustomName = contentsItem.CustomName,
								Amount = (int)(Random.Next(contentsItem.MinimumAmount == 0 ? Random.Next(0, 2) : contentsItem.MinimumAmount, contentsItem.MaximumAmount) * Instance.Config.ItemAmountMultiplier),
								Condition = Random.Next((int)(contentsItem.ConditionMinimumAmount * 100f), (int)(contentsItem.ConditionMaximumAmount * 100f)) / 100f,
								SkinId = contentsItem.UseRandomSkins ? contentsItem.RandomSkins[Random.Next(0, contentsItem.RandomSkins.Length - 1)] : contentsItem.SkinId,
								Text = contentsItem.Text
							};

							target.Contents.Add(lootable);

							RecursiveRandomizeContents(contentsItem, lootable);
						}
				}

				public T[] Shuffle<T>(IList<T> list)
				{
					var array = new T[list.Count];
					for (int i = 0; i < list.Count; i++) array[i] = list[i];

					int n = array.Length;
					while (n > 1)
					{
						n--;
						int k = Instance.Randomizer.Next(n + 1);
						T value = array[k];
						array[k] = array[n];
						array[n] = value;
					}

					return array;
				}

				public RootConfig.LootableDefinition GetDefinition()
				{
					return Instance.GetDefinition(Name, Position);
				}

				public void ApplyLock(RootConfig.LootableDefinition definition)
				{
					if (definition.Lock == null) Lock = null;
					else
					{
						if (Lock == null) Lock = new RootLock
						{
							Health = definition.Lock.Health * Instance.Config.LootableHealthMultiplier
						};
						else
						{
							Lock.Health = definition.Lock.Health * Instance.Config.LootableHealthMultiplier;
						};
					}
				}
				public void ApplyHack(RootConfig.LootableDefinition definition, bool resetCode = false)
				{
					if (definition.Hack == null) Hack = null;
					else
					{
						var code = GetRandomCode();

						if (Hack == null) Hack = new RootHack
						{
							Code = code,
							CodeResetRate = definition.Hack.CodeResetRate,
							WaitTime = definition.Hack.WaitTime,
							HackedTimes = 1
						};
						else
						{
							if (resetCode) Hack.Code = code;
							Hack.CodeResetRate = definition.Hack.CodeResetRate;
							Hack.WaitTime = definition.Hack.WaitTime;

							Hack.HackedTimes--;
							if (Hack.HackedTimes <= 0) Hack.HackedTimes = 1;
						};
					}

					LastHackWipeTick = DateTime.Now.Ticks;
				}
				public string GetRandomCode()
				{
					return $"{Random.Next(0, 9999):0000}";
				}
				public bool IsLocked()
				{
					return Lock != null && Lock.Health > 0f;
				}
				public bool IsHackable()
				{
					return Hack != null && !Hack.IsHacking;
				}
				public bool IsHacking()
				{
					return Hack != null && Hack.IsHacking;
				}
				public void StartHack(BasePlayer player)
				{
					if (Hack == null) return;

					Hack.IsHacking = true;
					Hack.HackStartTick = DateTime.Now.Ticks;
					HackingPlayer = player;
				}
				public void CancelHack()
				{
					if (Hack == null) return;

					Hack.IsHacking = false;
					Hack.HackStartTick = 0L;

					Hack.HackingTimer?.Destroy();
					Hack.HackingTimer = null;
					HackingPlayer = null;
				}
				public float GetHackTime()
				{
					if (Hack == null) return 0f;

					return (float)(DateTime.Now - new DateTime(Hack.HackStartTick)).TotalSeconds;
				}
				public float GetTotalHackingTime()
				{
					if (Hack == null) return 0f;

					return Hack.WaitTime * Hack.HackedTimes;
				}
				public bool HackHacked()
				{
					if (Hack == null) return false;

					return GetHackTime() >= GetTotalHackingTime();
				}
				public bool IsBusy(BasePlayer observer)
				{
					return IsHacking() && HackingPlayer != observer;
				}

				public void ClearContainer()
				{
					if (Container == null) return;

					Container.Kill();
					Container = null;
				}
				public bool ContainerExists()
				{
					return Container != null;
				}
			}
		}

		[ProtoBuf.ProtoContract]
		public class Container
		{
			[ProtoMember(1)]
			public byte[] ContainerBuffer { get; set; }

			[ProtoMember(5)]
			public List<byte[]> EntityBuffer { get; set; } = new List<byte[]>();

			public ItemContainer ItemContainer { get; set; }
		}

		public void LoadContainer()
		{
			Data.Container = Data.ContainerData == null ? new Container() : DeserializeMemory<Container>(Data.ContainerData);

			var load = new BaseNetworkable.LoadInfo { fromDisk = true };

			foreach (var buffer in Data.Container.EntityBuffer)
			{
				var entity = Entity.Deserialize(buffer);
				var baseEntity = GameManager.server.CreateEntity(
					strPrefab: StringPool.Get(entity.baseNetworkable.prefabID),
					pos: entity.baseEntity.pos,
					rot: Quaternion.Euler(entity.baseEntity.rot),
					startActive: true);

				if (baseEntity != null)
				{
					load.msg = entity;
					baseEntity.InitLoad(entity.baseNetworkable.uid);
					baseEntity.Spawn();
					baseEntity.Load(load);
					baseEntity.PostServerLoad();
				}
			}

			Data.Container.ItemContainer = new ItemContainer();
			Data.Container.ItemContainer.ServerInitialize(null, int.MaxValue);
			Data.Container.ItemContainer.GiveUID();
			try { Data.Container.ItemContainer.Load(ProtoBuf.ItemContainer.Deserialize(Data.Container.ContainerBuffer)); } catch { }
		}
		public void SaveContainer()
		{
			if (Data.Container == null) Data.Container = new Container();

			Data.Container.ContainerBuffer = ProtoBuf.ItemContainer.SerializeToBytes(Data.Container.ItemContainer.Save());

			Data.Container.EntityBuffer.Clear();
			CollectEntities(Data.Container.ItemContainer);

			Data.ContainerData = SerializeMemory(Data.Container);
		}

		public void CollectEntities(ItemContainer container)
		{
			if (container == null) return;

			var save = new BaseNetworkable.SaveInfo { forDisk = true };

			foreach (var item in container.itemList)
			{
				try
				{
					var entity = item.GetHeldEntity();
					if (entity != null)
					{
						var saveEntity = Facepunch.Pool.Get<Entity>();
						save.msg = saveEntity;

						entity.Save(save);
						Data.Container.EntityBuffer.Add(Entity.SerializeToBytes(saveEntity));

						Facepunch.Pool.Free(ref saveEntity);
					}
					else if (item.instanceData != null && item.instanceData.subEntity.IsValid)
					{
						var saveEntity = Facepunch.Pool.Get<Entity>();
						save.msg = saveEntity;

						var subEntity = BaseNetworkable.serverEntities.Find(item.instanceData.subEntity);
						subEntity.Save(save);
						Data.Container.EntityBuffer.Add(Entity.SerializeToBytes(saveEntity));

						Facepunch.Pool.Free(ref saveEntity);
					}

					CollectEntities(item.contents);
				}
				catch { }
			}
		}

		public ItemContainer GetContainer(Vector3 position)
		{
			foreach (var item in Data.Container.ItemContainer.itemList)
			{
				if (item.text.ToString() == position.ToString())
				{
					return item.contents;
				}
			}

			return null;
		}
		public void SetContainer(Vector3 position, ItemContainer container)
		{
			var item = ItemManager.CreateByName("note");
			item.text = position.ToString();
			item.contents = container;
			item.MoveToContainer(Data.Container.ItemContainer);
		}

		public static byte[] SerializeMemory<T>(T @object)
		{
			using (var stream = new MemoryStream())
			{
				Serializer.Serialize(stream, @object);

				stream.Close();
				return stream.ToArray();
			}
		}
		public static T DeserializeMemory<T>(byte[] bytes)
		{
			using (var stream = new MemoryStream(bytes))
			{
				var data = Serializer.Deserialize<T>(stream);

				stream.Close();
				return data;
			}
		}

		#endregion

		#region Editor

		internal string[] _emptyStringArray = new string[0];

		[ChatCommand("sledit")]
		private void Edit(BasePlayer player, string command, string[] args)
		{
			if (player != null && !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			UndrawInteraction(player);
			UndrawEditorInteraction(player);

			ToggleEditMode(player);
			Print($"You've {(IsInEditMode(player) ? "enabled" : "disabled")} edit mode.", player);
		}

		[ChatCommand("slwipe")]
		private void Wipe(BasePlayer player, string command, string[] args)
		{
			if (player != null && !permission.UserHasPermission(player.UserIDString, AdminPerm)) return;

			if (CUIPlayers.ContainsKey(player))
			{
				var interaction = CUIPlayers[player];
				var lootable = interaction?.Lootable;
				var definition = lootable.GetDefinition();
				lootable.ClearContainer();
				lootable.CancelHack();
				lootable.ApplyLock(definition);
				lootable.RandomizeContents(definition);
				lootable.LastRefillTick = DateTime.Now.Ticks;

				UndrawInteraction(player);
				UndrawEditorInteraction(player);

				Print($"You've wiped that container.", player);
			}
			else Print($"You're not looking at a container.", player);
		}

		public Dictionary<BasePlayer, Transform> EditorInteractionPlayers { get; set; } = new Dictionary<BasePlayer, Transform>(800);
		public Dictionary<BasePlayer, Editor> Editors { get; set; } = new Dictionary<BasePlayer, Editor>();

		public bool IsInEditMode(BasePlayer player)
		{
			foreach (var editor in Editors)
			{
				if (editor.Key == player) return true;
			}

			return false;
		}
		public void ToggleEditMode(BasePlayer player)
		{
			if (IsInEditMode(player)) Editors.Remove(player);
			else GetOrCreateEditor(player);
		}
		public Editor GetOrCreateEditor(BasePlayer player)
		{
			if (Editors.ContainsKey(player)) return Editors[player];

			var editor = new Editor();
			Editors.Add(player, editor);

			return editor;
		}

		public void DrawEditorInteraction(BasePlayer player, Transform transform)
		{
			UndrawEditorInteraction(player);

			if (IsInEditMode(player) && GetOrCreateEditor(player).IsEditing) return;

			var container = new CuiElementContainer();
			var background = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0.36 0.36", AnchorMax = "0.64 0.738" }, CursorEnabled = false }, "Hud", CUIEditorInteractionName, CUIEditorInteractionName );
			var offset = "0 -10";
			var fade = 0.3f;

			if (!EditorInteractionPlayers.ContainsKey(player))
				EditorInteractionPlayers.Add(player, transform);

			var definition = GetDefinition(transform);
			var ids = ZoneManager?.Call<string[]>("GetPlayerZoneIDs", player) ?? _emptyStringArray;

			container.Add(new CuiElement { Parent = background, FadeOut = fade, Components = { Death.GetRawImage(Config.Urls.DotUrl, fade: fade), new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = offset, OffsetMax = offset } } });
			container.Add(new CuiElement { Parent = background, FadeOut = fade, Components = { Death.GetRawImage(Config.Urls.ShadowUrl, color: $"1 1 1 {Config.ShadowOpacity}", fade: fade), new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = offset, OffsetMax = offset } } });
			container.Add(new CuiLabel { Text = { Text = $"<b>{transform.name}</b>\n<color=#c2c2c2><size=10>Press <b>[USE]</b> to {(definition != null ? "edit" : "create")} lootable\nPress <b>[RELOAD]</b> to edit settings</size></color>".ToUpper(), FadeIn = fade, FontSize = 13, Font = "robotocondensed-regular.ttf", Align = TextAnchor.LowerCenter }, RectTransform = { AnchorMin = $"0 0.435", AnchorMax = $"1 1" } }, background);
			container.Add(new CuiLabel { Text = { Text = $"<b>Path:</b> <size=10>{transform.GetRecursiveName().ToLower()}\n<b>Zone:</b> {(ids.Length == 0 ? "N/A" : string.Join(", ", ids))}</size>", Color = "1 1 1 0.6", FadeIn = fade * 2f, FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.LowerCenter }, RectTransform = { AnchorMin = $"0 0.385", AnchorMax = $"1 1", OffsetMin = "-400 -50", OffsetMax = "400 -50" } }, background);

			CuiHelper.AddUi(player, container);

		}
		public void UndrawEditorInteraction(BasePlayer player)
		{
			if (!EditorInteractionPlayers.ContainsKey(player)) return;

			for (int i = 0; i < 2; i++)
			{
				CuiHelper.DestroyUi(player, CUIEditorInteractionName);
			}

			EditorInteractionPlayers.Remove(player);
		}

		public const int HeightOffset = 1000;
		public const string Editor_Cmd = "sleditor.";

		public enum EditModes
		{
			Main,
			Contents,
			AddItem,
			Rules,
			OnlyIfParentFilter,
			OnlyIfNotParentFilter,
			OnlyIfInZone,
			OnlyIfNotInZone,

			Settings
		}
		public enum SettingModes
		{
			Main,
			Interactions,
			AddInteraction
		}

		public void DrawEditor(BasePlayer player, EditModes mode = EditModes.Main, string message = null, string messageTitle = "UH-OH!")
		{
			try
			{
				var editor = GetOrCreateEditor(player);
				var definition = editor.EditingDefinition;
				var setting = editor.Setting;

				editor.Mode = mode;

				UndrawEditor(player, true);

				var back = (mode != EditModes.Main && mode != EditModes.Settings) || setting == SettingModes.Interactions || setting == SettingModes.AddInteraction;
				var container = new CuiElementContainer();
				var background = container.Add(new CuiPanel { KeyboardEnabled = true, Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 -{HeightOffset}", OffsetMax = $"0 -{HeightOffset}" }, CursorEnabled = true, FadeOut = 0.001f}, "Hud", CUIEditorName, CUIEditorName);
				var panel = container.Add(new CuiPanel { CursorEnabled = false, KeyboardEnabled = true, Image = { Color = $"0.15 0.15 0.15 0.6", Material = "assets/content/ui/uibackgroundblur.mat" }, RectTransform = { AnchorMin = "0.28 0.35", AnchorMax = "0.72 0.75", OffsetMin = $"0 {HeightOffset}", OffsetMax = $"1 {HeightOffset}" } }, background);
				container.Add(new CuiElement() { Parent = panel, Components = { new CuiNeedsKeyboardComponent() } });

				if (!string.IsNullOrEmpty(message))
				{
					var messagePanel = container.Add(new CuiPanel { Image = { Color = $"0.15 0.15 0.15 0.6", Material = "assets/content/ui/uibackgroundblur.mat" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.135", OffsetMin = "0 -40", OffsetMax = "0 -40" } }, panel);
					container.Add(new CuiLabel { Text = { Text = $"<color=red><size=11><b>{messageTitle}</b></size></color>\n{message}", FontSize = 9, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.02 0", AnchorMax = $"0.98 1" } }, messagePanel);
				}

				panel = container.Add(new CuiPanel { KeyboardEnabled = true, Image = { Color = $"0.15 0.15 0.15 0.6" }, RectTransform = { AnchorMin = "0.01 0.025", AnchorMax = "0.9875 0.975" } }, panel);

				var showStupidShit = Randomizer.Next(0, 10) <= 1;
				container.Add(new CuiElement { Parent = panel, Components = { Death.GetRawImage("https://raulssorban.tv/wp-content/uploads/plugins/sl_pep.png", color: "1 1 1 0.02"), new CuiRectTransformComponent { AnchorMin = $"0.9 0", AnchorMax = "1 0.15" } } });

				if (back) container.Add(new CuiButton { Button = { Command = Editor_Cmd + "update_back", Color = "0.2 0.2 0.2 0.8" }, RectTransform = { AnchorMin = $"0.015 0.9", AnchorMax = "0.1 0.975" }, Text = { Text = $"BACK", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, panel);

				var title = "";
				switch (mode)
				{
					case EditModes.Main:
						title = $"{(GetDefinition(editor.Transform) == null ? "Create" : "Edit")} Lootable";
						break;

					case EditModes.Contents:
						title = $"Contents for '{editor.EditingDefinition.PrefabFilter}'";
						break;

					case EditModes.AddItem:
						title = $"Add Item to Contents";
						break;

					case EditModes.Rules:
						title = $"Rule Settings for '{editor.EditingDefinition.PrefabFilter}'";
						break;

					case EditModes.OnlyIfParentFilter:
						title = $"Only If Parent — Filter — Rule Settings for '{editor.EditingDefinition.PrefabFilter}'";
						break;

					case EditModes.OnlyIfNotParentFilter:
						title = $"Only If Not Parent — Filter — Rule Settings for '{editor.EditingDefinition.PrefabFilter}'";
						break;

					case EditModes.OnlyIfInZone:
						title = $"Only If In Zone — Rule Settings for '{editor.EditingDefinition.PrefabFilter}'";
						break;

					case EditModes.OnlyIfNotInZone:
						title = $"Only If Not In Zone — Rule Settings for '{editor.EditingDefinition.PrefabFilter}'";
						break;

					case EditModes.Settings:
						title = $"Settings";
						break;
				}

				container.Add(new CuiLabel { Text = { Text = title.ToUpper(), FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = $"{(back ? 0.11 : 0.015)} 0", AnchorMax = $"1 0.965" } }, panel);

				if (mode == EditModes.Main || mode == EditModes.Settings)
				{
					container.Add(new CuiButton { Button = { Command = Editor_Cmd + "cancel", Color = "0.9 0.3 0.2 0.8" }, RectTransform = { AnchorMin = $"0.9 0.9", AnchorMax = "0.9875 0.975" }, Text = { Text = $"<b>CLOSE</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, panel);

					if (mode == EditModes.Main)
					{
						container.Add(new CuiButton { Button = { Command = Editor_Cmd + "save", Color = "0.5 0.8 0.3 0.8" }, RectTransform = { AnchorMin = $"0.81 0.9", AnchorMax = "0.895 0.975" }, Text = { Text = $"<b>SAVE</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, panel);
						container.Add(new CuiButton { Button = { Command = Editor_Cmd + "delete", Color = "0.2 0.2 0.2 0.8" }, RectTransform = { AnchorMin = $"0.715 0.9", AnchorMax = "0.805 0.975" }, Text = { Text = $"<b>DELETE</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, panel);
					}
				}

				var spacing = 24.5f;
				var panel1 = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0.01 0.03", AnchorMax = "0.495 0.875" } }, panel);
				{
					var index = spacing;

					switch (mode)
					{
						case EditModes.Main:
							{
								var prefabFilter = DrawEditorOption(container, panel1, "Prefab Filter", index -= spacing, 0.5f, "Recommended <b>not</b> to manually modify if you're not sure how it works. <b>P</b> stands for 'Path', which switches between the short and long path of a prefab. <b>U</b> stands for 'Unique', which marks current lootable as unique, creating a perfect duplicate. Whenever edits are done and saved, only this world prefab uses the definition.");
								{
									DrawInputField(container, prefabFilter, Editor_Cmd + "update_prefabfilter", definition.PrefabFilter, max: 0.9f);
									DrawButton(container, prefabFilter, Editor_Cmd + "path", "P", xMin: 0.9f, xMax: 0.95f);
									DrawButton(container, prefabFilter, Editor_Cmd + "unique", "U", xMin: 0.95f, xMax: 1f, transparent: definition.IsUnique());
								}
								var interaction = DrawEditorOption(container, panel1, $"{(Config.Interactions.Count == 0 ? "<color=red>NO INTERACTIONS CONFIGURED</color>" : "Interaction")}", index -= spacing, 0.5f, "Visual representation of the lootable.");
								{
									if (Config.Interactions.Count > 0) DrawEnum(container, interaction, Editor_Cmd + "update_interaction -1", Editor_Cmd + "update_interaction 1", definition.InteractionIndex, Config.Interactions.Select(x => x.Text).ToArray(), Config.Interactions.Select(x => x.Icon).ToArray());
								}
								var _panel = DrawEditorOption(container, panel1, $"Panel", index -= spacing, 0.5f, "The panel used for the lootable when opened.");
								{
									DrawEnum(container, _panel, Editor_Cmd + "update_panel -1", Editor_Cmd + "update_panel 1", PanelTypesList.IndexOf(definition.Panel), PanelTypes.Select(x => x.Value).ToArray(), null);
								}
								var containerSize = DrawEditorOption(container, panel1, "Container Size", index -= spacing, 0.5f, "The amount of slots in the container. This limits the random amount of defined Contents that are gonna get spawned.");
								{
									DrawInputField(container, containerSize, Editor_Cmd + "update_containersize", $"{definition.ContainerSize}");
								}
								var allowStack = DrawEditorOption(container, panel1, "Allow Stack", index -= spacing, 0.5f, "Allows stacking of same-definition but multiple items defined in the Content stacking. Eg. you have 2+ definitions of Scrap, if true, they'll be merged. False, they'll be added separately.");
								{
									DrawToggle(container, allowStack, Editor_Cmd + "update_allowstack", definition.AllowStack);
								}
								var liquid = DrawEditorOption(container, panel1, "Liquid", index -= spacing, 0.5f, "When enabled, only items like Water, Salt Water and Blood defined in Contents will be displayed.");
								{
									DrawToggle(container, liquid, Editor_Cmd + "update_liquid", definition.Liquid);
								}
								var persistent = DrawEditorOption(container, panel1, "Persistent", index -= spacing, 0.5f, "Allows players to add in items in containers when this setting is enabled. The containers spawn in Contents items on the first player interaction, and get wiped on server wipe. These global containers persist on server restarts.");
								{
									DrawToggle(container, persistent, Editor_Cmd + "update_persistent", definition.Persistent);
								}
								var rules = DrawEditorOption(container, panel1, "Rules", index -= spacing, 0.5f);
								{
									DrawButton(container, rules, Editor_Cmd + "update_rules", "Edit");
								}
								var timer = DrawEditorOption(container, panel1, "Holding Timer [USE]", index -= spacing, 0.5f);
								{
									DrawInputField(container, timer, Editor_Cmd + "update_timer", $"{definition.Timer}");
								}

								if (LootablesExt != null && editor.Transform.name.ToLower().Contains("slprefab"))
								{
									var extDelete = DrawEditorOption(container, panel1, "Delete <color=orange>Lootables.Ext</color> Prefab", index -= spacing, 0.5f);
									{
										DrawButton(container, extDelete, Editor_Cmd + "ext_delete", "Delete");
									}
								}
							}
							break;

						case EditModes.Contents:
							{
								var contentsPerPage = 9;
								var contents = editor.EditingContents;
								var pageId = editor.EditingLootableItem?.Contents == editor.EditingContents ? 2 : 1;
								var page = editor.GetPage(pageId);
								page.TotalPages = (int)Math.Ceiling((double)contents.Count / contentsPerPage - 1);
								page.Check();

								var pageContents = contents.Skip(contentsPerPage * page.CurrentPage).Take(contentsPerPage);
								var count = pageContents.Count();
								var totalCount = contents.Count;

								var add = DrawEditorOption(container, panel1, string.Empty, index -= spacing, 0.5f);
								{
									container.Add(new CuiButton { Button = { Command = Editor_Cmd + "update_content_add", Color = "0.4 0.8 0.3 0.8" }, RectTransform = { AnchorMin = $"0.01 0.125", AnchorMax = "0.7 0.875" }, Text = { Text = $"<b>ADD</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, add);

									container.Add(new CuiLabel { Text = { Text = $"{page.CurrentPage + 1:n0} / {page.TotalPages + 1:n0}", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = $"0.7 0", AnchorMax = $"1 1" } }, add);
									container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"page_back {pageId}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.71 0.125", AnchorMax = "0.79 0.875" }, Text = { Text = $"<b>◀</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, add);
									container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"page_forward {pageId}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.92 0.125", AnchorMax = "0.99 0.875" }, Text = { Text = $"<b>▶</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, add);
								}

								for (int i = 0; i < count; i++)
								{
									var actualI = i + (page.CurrentPage * contentsPerPage);
									var item = contents.ElementAt(actualI);
									var itemDefinition = ItemManager.FindItemDefinition(item.ShortName);

									var content = DrawEditorOption(container, panel1, $"          {(itemDefinition == null ? $"Invalid: {item.ShortName}" : itemDefinition?.displayName?.english)}{(string.IsNullOrEmpty(item.CustomName) ? "" : $" ({item.CustomName})")}", index -= spacing, 0.5f);
									{
										if (item == editor.EditingLootableItem) container.Add(new CuiPanel { Image = { Color = $"0.2 0.3 0.8 0.8" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, content);

										container.Add(new CuiElement { Parent = content, Components = { new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(item.ShortName).itemid, SkinId = item.SkinId }, new CuiRectTransformComponent { AnchorMin = $"0.02 0.1", AnchorMax = "0.1 0.9" } } });
										container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_content_edit {actualI} {(editor.EditingLootableItem2 != null ? "1" : "0")}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = "1 1" }, Text = { Text = "", Color = "0 0 0 0" } }, content);

										container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_content_delete {actualI}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875" }, Text = { Text = $"<b>X</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 9 } }, content);
										if (actualI > 0) container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_content_move {actualI} -1", Color = "0.2 0.2 0.2 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875", OffsetMin = "-36 0", OffsetMax = "-36 1" }, Text = { Text = $"<b>▲</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
										if (actualI < totalCount - 1) container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_content_move {actualI} 1", Color = "0.2 0.2 0.2 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875", OffsetMin = "-18 0", OffsetMax = "-18 1" }, Text = { Text = $"<b>▼</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
									}
								}
							}
							break;

						case EditModes.AddItem:
							{
								var filteredItems = Facepunch.Pool.GetList<ItemDefinition>();
								foreach (var item in ItemManager.GetItemDefinitions())
								{
									if (string.IsNullOrEmpty(item.displayName.english) || item.hidden) continue;

									try
									{
										if (editor.ItemSearchCategory != -1 && item.category == GetCategory(editor.ItemSearchCategory) && string.IsNullOrEmpty(editor.ItemSearchFilter))
										{
											filteredItems.Add(item);
											continue;
										}

										if ((!item.hidden && item.displayName.english.ToLower().Trim().Contains(editor.ItemSearchFilter.ToLower()) || item.shortname == editor.ItemSearchFilter.ToLower()))
										{
											if (editor.ItemSearchCategory != -1 && item.category != GetCategory(editor.ItemSearchCategory)) continue;

											filteredItems.Add(item);
										}
									}
									catch { }
								}
								var contentsPerPage = 8;
								var pageId = 4;
								var page = editor.GetPage(pageId);
								page.TotalPages = (int)Math.Ceiling((double)filteredItems.Count / contentsPerPage - 1);
								page.Check();

								var pageContents = filteredItems.Skip(contentsPerPage * page.CurrentPage).Take(contentsPerPage);
								var count = pageContents.Count();
								var totalCount = filteredItems.Count;

								var categories = DrawEditorOption(container, panel1, "", index -= spacing, 0f);
								{
									var offset = 0.077f;
									var start = 0f;
									var indice = 0;

									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice}", "assets/icons/construction.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/extinguish.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/blunt.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/clothing.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/tools.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/pills.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/weapon.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/ammunition.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/electric.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/lick.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/demolish.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/fork_and_spoon.png", start, start += offset, editor.ItemSearchCategory == indice);
									DrawButtonImage(container, categories, Editor_Cmd + $"update_additem_category {indice += 1}", "assets/icons/open.png", start, start += offset, editor.ItemSearchCategory == indice);
								}

								var search = DrawEditorOption(container, panel1, "", index -= spacing, 0f);
								{
									DrawInputField(container, search, Editor_Cmd + "update_additem_filter", editor.ItemSearchFilter, 0f, 0.55f, TextAnchor.MiddleCenter);
									DrawButton(container, search, Editor_Cmd + "update_additem_filter ", "CLEAR", xMin: 0.56f, xMax: 0.7f, yMin: 0f, yMax: 1f, transparent: string.IsNullOrEmpty(editor.ItemSearchFilter));

									container.Add(new CuiLabel { Text = { Text = $"{page.CurrentPage + 1:n0} / {page.TotalPages + 1:n0}", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = $"0.7 0", AnchorMax = $"1 1" } }, search);
									container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"page_back {pageId}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.71 0", AnchorMax = "0.79 1" }, Text = { Text = $"<b>◀</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, search);
									container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"page_forward {pageId}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.92 0", AnchorMax = "0.99 1" }, Text = { Text = $"<b>▶</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, search);
								}

								foreach (var item in pageContents)
								{
									var content = DrawEditorOption(container, panel1, $"          {item.displayName?.english}", index -= spacing, 0.5f);
									{
										container.Add(new CuiElement { Parent = content, Components = { new CuiImageComponent { ItemId = item.itemid }, new CuiRectTransformComponent { AnchorMin = $"0.02 0.1", AnchorMax = "0.1 0.9" } } });
										container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_content_add {item.shortname}", Color = "0.4 0.8 0.3 0.8" }, RectTransform = { AnchorMin = $"0.9 0.125", AnchorMax = "0.99 0.875" }, Text = { Text = $"Add", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
									}
								}

								if (filteredItems.Count == 0)
								{
									DrawEditorTitle(container, panel1, "No items found with this filter.", index -= spacing, 0f);
								}

								Facepunch.Pool.FreeList(ref filteredItems);
							}
							break;

						case EditModes.Rules:
							{
								var refillRate = DrawEditorOption(container, panel1, "Refill Rate (in minutes)", index -= spacing, 0.5f);
								{
									DrawInputField(container, refillRate, Editor_Cmd + "update_rules_refillrate", definition.Rule.RefillRate.ToString("0.0"));
								}
								var onlyIfParentFilter = DrawEditorOption(container, panel1, "Only If Parent — Filter", index -= spacing, 0.5f);
								{
									DrawButton(container, onlyIfParentFilter, Editor_Cmd + "update_rules_onlyifparentfilter", "Edit");
								}
								var onlyIfNotParentFilter = DrawEditorOption(container, panel1, "Only If Not Parent — Filter", index -= spacing, 0.5f);
								{
									DrawButton(container, onlyIfNotParentFilter, Editor_Cmd + "update_rules_onlyifnotparentfilter", "Edit");
								}
								var onlyIfInZone = DrawEditorOption(container, panel1, "Only If In Zone", index -= spacing, 0.5f);
								{
									DrawButton(container, onlyIfInZone, Editor_Cmd + "update_rules_onlyifinzone", "Edit");
								}
								var onlyIfNotInZone = DrawEditorOption(container, panel1, "Only If Not In Zone", index -= spacing, 0.5f);
								{
									DrawButton(container, onlyIfNotInZone, Editor_Cmd + "update_rules_onlyifnotinzone", "Edit");
								}
							}
							break;

						case EditModes.OnlyIfParentFilter:
						case EditModes.OnlyIfNotParentFilter:
						case EditModes.OnlyIfInZone:
						case EditModes.OnlyIfNotInZone:
							{
								var list = editor.GetRuleList();

								for (int i = 0; i < list.Count; i++)
								{
									var item = list[i];
									var content = DrawEditorOption(container, panel1, $"{item}", index -= spacing, 0.5f);
									{
										container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_rules_delete {i}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875" }, Text = { Text = $"<b>X</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
										if (i > 0) container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_rules_move {i} -1", Color = "0.2 0.2 0.2 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875", OffsetMin = "-18 0", OffsetMax = "-18 1" }, Text = { Text = $"<b>▲</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
										if (i < list.Count - 1) container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"update_rules_move {i} 1", Color = "0.2 0.2 0.2 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875", OffsetMin = "-36 0", OffsetMax = "-36 1" }, Text = { Text = $"<b>▼</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
									}
								}

								var add = DrawEditorOption(container, panel1, "", index -= spacing, 0f);
								{
									DrawInputField(container, add, Editor_Cmd + "update_rules_add", "Enter filter here...", 0f, 1f);
								}
							}
							break;

						case EditModes.Settings:
							{
								switch (editor.Setting)
								{
									case SettingModes.Main:
										{
											var asyncMode = DrawEditorOption(container, panel1, "Async Mode", index -= spacing, 0.5f, "Enabling this will make sure that if your servers has any kind of lag, it'll wait for the server to 'breathe' first, at the expense of your players noticing slight delay when interacting with lootables, most of the time not noticeable. Keeping this disabled offers the most accuracy - highly recommended.");
											{
												DrawToggle(container, asyncMode, Editor_Cmd + "settings_asyncmode", Config.AsyncMode);
											}
											var distance = DrawEditorOption(container, panel1, "Distance", index -= spacing, 0.5f);
											{
												DrawInputField(container, distance, Editor_Cmd + "settings_distance", $"{Config.Distance}");
											}

											var lockName = DrawEditorOption(container, panel1, "Lock Name", index -= spacing, 0.5f);
											{
												DrawInputField(container, lockName, Editor_Cmd + "settings_lockname", Config.LockName);
											}
											var busy = DrawEditorOption(container, panel1, "Busy", index -= spacing, 0.5f);
											{
												DrawInputField(container, busy, Editor_Cmd + "settings_busy", Config.BusyName);
											}
											var hackName = DrawEditorOption(container, panel1, "Hack Name", index -= spacing, 0.5f);
											{
												DrawInputField(container, hackName, Editor_Cmd + "settings_hackname", Config.HackName);
											}
											var hackingName = DrawEditorOption(container, panel1, "Hacking Name", index -= spacing, 0.5f);
											{
												DrawInputField(container, hackingName, Editor_Cmd + "settings_hackingname", Config.HackingName);
											}
											var codeInputTitle = DrawEditorOption(container, panel1, "Code Input Title", index -= spacing, 0.5f);
											{
												DrawInputField(container, codeInputTitle, Editor_Cmd + "settings_codeinputtitle", Config.CodeInputTitle);
											}
											var interactions = DrawEditorOption(container, panel1, "Interactions", index -= spacing, 0.5f, "At least one interaction must be configured for the plugin to correctly run.");
											{
												DrawButton(container, interactions, Editor_Cmd + "settings_interactions", "Edit");
											}
											var itemAmountMultiplier = DrawEditorOption(container, panel1, "Item Amount Multiplier", index -= spacing, 0.5f);
											{
												DrawInputField(container, itemAmountMultiplier, Editor_Cmd + "update_settings_itemamountmultiplier", $"{Config.ItemAmountMultiplier}");
											}
											var lootableHealthMultiplier = DrawEditorOption(container, panel1, "Lootable Health Multiplier", index -= spacing, 0.5f);
											{
												DrawInputField(container, lootableHealthMultiplier, Editor_Cmd + "update_settings_lootablehealthmultiplier", $"{Config.LootableHealthMultiplier}");
											}
										}
										break;

									case SettingModes.Interactions:
									case SettingModes.AddInteraction:
										{
											var interactions = Config.Interactions;
											var contentsPerPage = 9;
											var pageId = 3;
											var page = editor.GetPage(pageId);
											page.TotalPages = (int)Math.Ceiling((double)interactions.Count / contentsPerPage - 1);
											page.Check();

											var pageContents = interactions.Skip(contentsPerPage * page.CurrentPage).Take(contentsPerPage);
											var count = pageContents.Count();
											var totalCount = interactions.Count;

											var add = DrawEditorOption(container, panel1, string.Empty, index -= spacing, 0.5f);
											{
												container.Add(new CuiButton { Button = { Command = Editor_Cmd + "update_settings_addinteraction", Color = editor.Setting == SettingModes.AddInteraction ? "0.4 0.3 0.8 0.8" : "0.4 0.8 0.3 0.8" }, RectTransform = { AnchorMin = $"0.01 0.125", AnchorMax = "0.7 0.875" }, Text = { Text = editor.Setting == SettingModes.AddInteraction ? "<b>SAVE</b>" : $"<b>ADD</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, add);

												container.Add(new CuiLabel { Text = { Text = $"{page.CurrentPage + 1:n0} / {page.TotalPages + 1:n0}", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = $"0.7 0", AnchorMax = $"1 1" } }, add);
												container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"page_back {pageId}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.71 0.125", AnchorMax = "0.79 0.875" }, Text = { Text = $"<b>◀</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, add);
												container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"page_forward {pageId}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.92 0.125", AnchorMax = "0.99 0.875" }, Text = { Text = $"<b>▶</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, add);
											}

											for (int i = 0; i < count; i++)
											{
												var actualI = i + (page.CurrentPage * contentsPerPage);
												var item = interactions.ElementAt(actualI);

												var content = DrawEditorOption(container, panel1, $"          {item.Text}", index -= spacing, 0.5f);
												{
													if (item == editor.EditingInteraction) container.Add(new CuiPanel { Image = { Color = $"0.2 0.3 0.8 0.8" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, content);

													container.Add(new CuiElement { Parent = content, Components = { new CuiRawImageComponent { Url = item.Icon }, new CuiRectTransformComponent { AnchorMin = $"0.03 0.1", AnchorMax = "0.1 0.9" } } });
													container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"settings_interactions_edit {actualI} {(editor.EditingLootableItem2 != null ? "1" : "0")}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = "1 1" }, Text = { Text = "", Color = "0 0 0 0" } }, content);
													container.Add(new CuiButton { Button = { Command = Editor_Cmd + $"settings_interactions_delete {actualI}", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875" }, Text = { Text = $"<b>X</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
												}
											}
										}
										break;
								}
							}
							break;
					}
				}

				var panel2 = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0.505 0.03", AnchorMax = "0.99 0.875" } }, panel);
				{
					var index = spacing;

					switch (mode)
					{
						case EditModes.Main:
							{
								var contents = DrawEditorOption(container, panel2, $"<b>Contents</b> {definition.Contents.Count:n0}", index -= spacing, 0.5f);
								{
									DrawButton(container, contents, Editor_Cmd + "update_contents 0", "Edit");
								}

								var lockOption = DrawEditorOption(container, panel2, "<b>Lock</b>", index -= spacing, 0.5f);
								{
									DrawButton(container, lockOption, Editor_Cmd + "update_lock", definition.Lock == null ? "Create" : "Remove");
								}
								if (definition.Lock != null)
								{
									var lockHealth = DrawEditorOption(container, panel2, "Health", index -= spacing, 0.25f);
									{
										DrawInputField(container, lockHealth, Editor_Cmd + "update_lockhealth", $"{definition.Lock.Health}");
									}
								}
								else
								{
									var content = DrawEditorOption(container, panel2, "", index -= spacing, 0f);
									{
										container.Add(new CuiLabel { Text = { Text = "There's no lock set on this lootable.", FontSize = 9, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" } }, content);
									}
								}

								var hackOption = DrawEditorOption(container, panel2, "<b>Hack</b>", index -= spacing, 0.5f);
								{
									DrawButton(container, hackOption, Editor_Cmd + "update_hack", definition.Hack == null ? "Create" : "Remove");
								}
								if (definition.Hack != null)
								{
									var hackWaitTime = DrawEditorOption(container, panel2, "Wait Time (in seconds)", index -= spacing, 0.25f);
									{
										DrawInputField(container, hackWaitTime, Editor_Cmd + "update_hackwaittime", $"{definition.Hack.WaitTime}");
									}
									var hackCodeResetRate = DrawEditorOption(container, panel2, "Code Reset Rate (in minutes)", index -= spacing, 0.25f);
									{
										DrawInputField(container, hackCodeResetRate, Editor_Cmd + "update_hackcoderesetrate", $"{definition.Hack.CodeResetRate}");
									}
								}
								else
								{
									var content = DrawEditorOption(container, panel2, "", index -= spacing, 0f);
									{
										container.Add(new CuiLabel { Text = { Text = "There's no hack set on this lootable.", FontSize = 9, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" } }, content);
									}
								}

								var commandOption = DrawEditorOption(container, panel2, "<b>Command</b>", index -= spacing, 0.5f, "Command that's going to be called under player's credentials (permissions on the server) instead of looting the regular <color=orange>Contents</color> loot.");
								{
									DrawInputField(container, commandOption, Editor_Cmd + "update_command", definition.Command);
								}

								var resetLootable = DrawEditorOption(container, panel2, "<b>Reset</b>", index -= spacing, 0.5f, "This will reset the contents of this specific lootable.");
								{
									DrawButton(container, resetLootable, Editor_Cmd + "resetcontents", "Reset", transparent: true);
								}
								var resetAllLootable = DrawEditorOption(container, panel2, "<b><color=red>Reset All</color></b>", index -= spacing, 0.5f, "This will reset ALL contents across the map of this specific lootable.");
								{
									DrawButton(container, resetAllLootable, Editor_Cmd + "resetallcontents", "Reset All", transparent: true);
								}
							}
							break;

						case EditModes.Contents:
							{
								var lootableItem = editor.EditingLootableItem2 ?? (editor.EditingLootableItem?.Contents == editor.EditingContents ? editor.EditingLootableItem2 : editor.EditingLootableItem);

								if (lootableItem == null)
								{
									container.Add(new CuiLabel { Text = { Text = "No item selected.", FontSize = 9, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.2" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" } }, panel2);
								}
								else
								{
									var itemDefinition = ItemManager.FindItemDefinition(lootableItem.ShortName);

									if (itemDefinition == null)
									{
										DrawEditorOption(container, panel2, "Invalid item definition", index -= spacing, 0.5f);
									}
									else
									{
										var customName = DrawEditorOption(container, panel2, "Custom Name", index -= spacing, 0.5f, "The in-game name of the item whenever it gets created.");
										{
											DrawInputField(container, customName, Editor_Cmd + "update_contents_customname", lootableItem.CustomName);
										}
										var skinId = DrawEditorOption(container, panel2, "Skin ID", index -= spacing, 0.5f);
										{
											DrawInputField(container, skinId, Editor_Cmd + "update_contents_skinid", lootableItem.SkinId.ToString());
											container.Add(new CuiButton { Button = { Command = Editor_Cmd + "update_contents_skinid 0", Color = "0.8 0.4 0.3 0.8" }, RectTransform = { AnchorMin = $"0.925 0.125", AnchorMax = "0.99 0.875" }, Text = { Text = $"<b>R</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, skinId);
										}
										var amount = DrawEditorOption(container, panel2, "Amount", index -= spacing, 0.5f, "The stack amount range of the item which is used whenever the item's being created.");
										{
											container.Add(new CuiLabel { Text = { Text = "min.", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.55 0", AnchorMax = $"1 1" } }, amount);
											container.Add(new CuiLabel { Text = { Text = "max.", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.77 0", AnchorMax = $"1 1" } }, amount);

											DrawInputField(container, amount, Editor_Cmd + "update_contents_minamount", lootableItem.MinimumAmount.ToString("n0"), 0.62f, 0.75f, TextAnchor.MiddleCenter);
											DrawInputField(container, amount, Editor_Cmd + "update_contents_maxamount", lootableItem.MaximumAmount.ToString("n0"), 0.85f, 1f, TextAnchor.MiddleCenter);
										}
										if (itemDefinition.condition.enabled)
										{
											var conditionAmount = DrawEditorOption(container, panel2, "Condition", index -= spacing, 0.5f, "The condition scaled automatically based on the maximum condition of the item. 0.5 is the exact half, no matter the actual condition value.");
											{
												container.Add(new CuiLabel { Text = { Text = "min.", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.55 0", AnchorMax = $"1 1" } }, conditionAmount);
												container.Add(new CuiLabel { Text = { Text = "max.", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.77 0", AnchorMax = $"1 1" } }, conditionAmount);

												DrawInputField(container, conditionAmount, Editor_Cmd + "update_contents_conditionminamount", lootableItem.ConditionMinimumAmount.ToString("0.0"), 0.62f, 0.75f, TextAnchor.MiddleCenter);
												DrawInputField(container, conditionAmount, Editor_Cmd + "update_contents_conditionmaxamount", lootableItem.ConditionMaximumAmount.ToString("0.0"), 0.85f, 1f, TextAnchor.MiddleCenter);
											}
										}
										var spawnChance = DrawEditorOption(container, panel2, "Spawn Chance", index -= spacing, 0.5f, "The odds of the item actually being spawned in a container.");
										{
											container.Add(new CuiLabel { Text = { Text = $"times in", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.64 0", AnchorMax = $"1 1" } }, spawnChance);
											container.Add(new CuiLabel { Text = { Text = $"=  <b>{Percentage(lootableItem.SpawnChanceTimes, lootableItem.SpawnChanceScale):0}%</b>", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.8725 0", AnchorMax = $"1 1" } }, spawnChance);

											DrawInputField(container, spawnChance, Editor_Cmd + "update_contents_spawnchancetimes", lootableItem.SpawnChanceTimes.ToString("n0"), 0.55f, 0.625f, TextAnchor.MiddleCenter);
											DrawInputField(container, spawnChance, Editor_Cmd + "update_contents_spawnchancescale", lootableItem.SpawnChanceScale.ToString("n0"), 0.775f, 0.85f, TextAnchor.MiddleCenter);
										}
										if (itemDefinition.Blueprint != null && itemDefinition.Blueprint.userCraftable)
										{
											var blueprint = DrawEditorOption(container, panel2, "Blueprint Chance", index -= spacing, 0.5f, "The odds of the item being spawned as a blueprint. When the item's spawned as a blueprint, condition, amount, custom name and skin ID aren't taken into account.");
											{
												container.Add(new CuiLabel { Text = { Text = $"times in", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.64 0", AnchorMax = $"1 1" } }, blueprint);
												container.Add(new CuiLabel { Text = { Text = $"=  <b>{(lootableItem.BlueprintChanceTimes == 0 && lootableItem.BlueprintChanceScale == 0 ? $"0" : $"{Percentage(lootableItem.BlueprintChanceTimes, lootableItem.BlueprintChanceScale):0}")}%</b>", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0.8725 0", AnchorMax = $"1 1" } }, blueprint);

												DrawInputField(container, blueprint, Editor_Cmd + "update_contents_blueprintchancetimes", lootableItem.BlueprintChanceTimes.ToString("n0"), 0.55f, 0.625f, TextAnchor.MiddleCenter);
												DrawInputField(container, blueprint, Editor_Cmd + "update_contents_blueprintchancescale", lootableItem.BlueprintChanceScale.ToString("n0"), 0.775f, 0.85f, TextAnchor.MiddleCenter);
											}
										}

										if (itemDefinition.shortname.Contains("note") ||
											 itemDefinition.shortname.Contains("car.key") ||
											 itemDefinition.shortname.Contains("door.key"))
										{
											var note = DrawEditorOption(container, panel2, "Note", index -= spacing, 0.5f, "The text value of this note item.");
											{
												DrawInputField(container, note, Editor_Cmd + "update_contents_text", lootableItem.Text);
											}
										}

										if (itemDefinition.itemMods.Any(x => x is ItemModContainer))
										{
											var contents = DrawEditorOption(container, panel2, $"Contents {lootableItem.Contents.Count:n0}", index -= spacing, 0.5f, "Sub-container of the item. Mainly used so you can add attachments on weapons or liquids in items like Bota Bag or Water Jug.");
											{
												DrawButton(container, contents, Editor_Cmd + "update_contents 1", "Edit");
											}
										}

										DrawEditorOption(container, panel2, $"{lootableItem.ShortName}[{itemDefinition.itemid}]", index -= spacing, 0.5f);
									}
								}
							}
							break;

						case EditModes.AddItem:
							{

							}
							break;

						case EditModes.Settings:
							{
								switch (editor.Setting)
								{
									case SettingModes.Interactions:
									case SettingModes.AddInteraction:
										{
											var interaction = editor.EditingInteraction;

											if (interaction == null)
											{
												container.Add(new CuiLabel { Text = { Text = $"No interaction selected.", FontSize = 8, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.75" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" } }, panel2);
											}
											else
											{
												var text = DrawEditorOption(container, panel2, "Text", index -= spacing, 0.5f);
												{
													DrawInputField(container, text, Editor_Cmd + "update_settings_updatetext", interaction.Text, min: 0.25f);
												}
												var icon = DrawEditorOption(container, panel2, "Icon", index -= spacing, 0.5f);
												{
													DrawInputField(container, icon, Editor_Cmd + "update_settings_updateicon", interaction.Icon, min: 0.25f);
												}
												var openEffect = DrawEditorOption(container, panel2, "Open Effect", index -= spacing, 0.5f);
												{
													DrawInputField(container, openEffect, Editor_Cmd + "update_settings_updateopeneffect", interaction.OpenEffect, min: 0.25f);
												}
											}
										}
										break;

									case SettingModes.Main:
										{
											var shadowOpacity = DrawEditorOption(container, panel2, "Shadow Opacity", index -= spacing, 0.5f);
											{
												DrawInputField(container, shadowOpacity, Editor_Cmd + "update_settings_shadowopacity", $"{Config.ShadowOpacity}");
											}
											var showAppxCount = DrawEditorOption(container, panel2, "Show Approximate Count", index -= spacing, 0.5f);
											{
												DrawToggle(container, showAppxCount, Editor_Cmd + "update_settings_showlockedlootableapproximatecount", Config.ShowLockedLootableApproximateCount);
											}
											var damageMultiplierMin = DrawEditorOption(container, panel2, "Damage Multiplier Min.", index -= spacing, 0.5f);
											{
												DrawInputField(container, damageMultiplierMin, Editor_Cmd + "update_settings_damagemultipliermin", $"{Config.DamageMultiplierMinimum}");
											}
											var damageMultiplierMax = DrawEditorOption(container, panel2, "Damage Multiplier Max.", index -= spacing, 0.5f);
											{
												DrawInputField(container, damageMultiplierMax, Editor_Cmd + "update_settings_damagemultipliermax", $"{Config.DamageMultiplierMaximum}");
											}
											var damageCUIRefreshRate = DrawEditorOption(container, panel2, "Damage CUI Refresh Rate", index -= spacing, 0.5f);
											{
												DrawInputField(container, damageCUIRefreshRate, Editor_Cmd + "update_settings_damagecuirefreshrate", $"{Config.DamageCUIRefreshRate}");
											}
											var waitingCUIRefreshRate = DrawEditorOption(container, panel2, "Waiting CUI Refresh Rate", index -= spacing, 0.5f);
											{
												DrawInputField(container, waitingCUIRefreshRate, Editor_Cmd + "update_settings_waitingcuirefreshrate", $"{Config.WaitingCUIRefreshRate}");
											}
											var middleMouseInfo = DrawEditorOption(container, panel2, "Middle Mouse Info", index -= spacing, 0.5f, info: "Once enabled, whenever you're clicking [MMB] and you're not selecting any hotbar slots, it'll privately print to chat the full prefab name and zone of the prefab you're looking at.");
											{
												DrawToggle(container, middleMouseInfo, Editor_Cmd + "settings_middlemouseinfo", Config.MiddleMouseInfo);
											}

											if (plugins.Find("LootablesExt"))
											{
												var ext = DrawEditorOption(container, panel2, "          Lootables.Ext", index -= spacing, 0.5f);
												{
													container.Add(new CuiButton { Button = { Command = string.Empty, Color = $"0.9 0.2 0.2 0.9" }, RectTransform = { AnchorMin = $"0.01 0.2", AnchorMax = "0.1 0.8" }, Text = { Text = $"PRO", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 7 } }, ext);
												}

												var create = DrawEditorOption(container, panel2, "Create Prefab", index -= spacing, 0.5f, "Use the input field to set a custom ID for the prefab. Leave blank for it to be unique each time you create one.");
												{
													DrawInputField(container, create, Editor_Cmd + "update_ext_prefabname", editor.ExtPrefabName, max: 0.8f);
													DrawButton(container, create, Editor_Cmd + "ext_create", "Start");
												}
												var ping = DrawEditorOption(container, panel2, "Ping Nearby Prefabs", index -= spacing, 0.5f);
												{
													DrawButton(container, ping, Editor_Cmd + "ext_ping", "Ping");
												}
											}
										}
										break;
								}
							}
							break;
					}
				}

				if (showStupidShit)
				{
					container.Add(new CuiElement
					{
						Parent = panel,
						Components =
					{
						new CuiInputFieldComponent { Text = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0", CharsLimit = 300 },
						new CuiRectTransformComponent { AnchorMin = "0.9 0", AnchorMax = "1 0.1" }
					}
					});
				}

				CuiHelper.AddUi(player, container);

				editor.IsEditing = true;
			}
			catch { }
		}
		public void UndrawEditor(BasePlayer player, bool refresh)
		{
			var editor = GetOrCreateEditor(player);
			editor.IsEditing = false;

			for (int i = 0; i < 2; i++)
			{
				CuiHelper.DestroyUi(player, CUIEditorName);
			}
		}

		internal ItemCategory GetCategory(int indece)
		{
			switch (indece)
			{
				case 0: return ItemCategory.Construction;
				case 1: return ItemCategory.Items;
				case 2: return ItemCategory.Resources;
				case 3: return ItemCategory.Attire;
				case 4: return ItemCategory.Tool;
				case 5: return ItemCategory.Medical;
				case 6: return ItemCategory.Weapon;
				case 7: return ItemCategory.Ammunition;
				case 8: return ItemCategory.Electrical;
				case 9: return ItemCategory.Fun;
				case 10: return ItemCategory.Misc;
				case 11: return ItemCategory.Food;
				case 12: return ItemCategory.Component;
			}

			return ItemCategory.All;
		}

		public string DrawEditorOption(CuiElementContainer container, string parent, string title, float yOffset, float opacity, string info = null)
		{
			var hasInfo = !string.IsNullOrEmpty(info);
			var content = container.Add(new CuiPanel { Image = { Color = $"0 0 0 {opacity}" }, RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1", OffsetMin = $"0 {yOffset}", OffsetMax = $"0 {yOffset}" } }, parent);

			if (hasInfo) DrawButton(container, content, Editor_Cmd + $"info {title} Info|{info}", "?", xMin: 0f, xMax: 0.05f);

			container.Add(new CuiLabel { Text = { Text = title, FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.9" }, RectTransform = { AnchorMin = hasInfo ? "0.075 0" : $"0.02 0", AnchorMax = $"1 1" } }, content);

			return content;
		}
		public string DrawEditorTitle(CuiElementContainer container, string parent, string title, float yOffset, float opacity)
		{
			var content = container.Add(new CuiPanel { Image = { Color = $"0 0 0 {opacity}" }, RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1", OffsetMin = $"0 {yOffset}", OffsetMax = $"0 {yOffset}" } }, parent);
			container.Add(new CuiLabel { Text = { Text = title, FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" } }, content);

			return content;
		}

		public void DrawInputField(CuiElementContainer container, string parent, string command, string placeholder, float min = 0.55f, float max = 1f, TextAnchor anchor = TextAnchor.MiddleLeft)
		{
			var content = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0.5" }, RectTransform = { AnchorMin = $"{min} 0", AnchorMax = $"{max} 1" } }, parent);
			container.Add(new CuiElement
			{
				Parent = content,
				Components =
				{
					new CuiInputFieldComponent { Text = placeholder == null ? string.Empty : placeholder, FontSize = 10, Command = command, Align = anchor, Color = "1 1 1 1", CharsLimit = 300 },
					new CuiRectTransformComponent { AnchorMin = "0.05 0", AnchorMax = "1 1" }
				}
			});
		}
		public void DrawEnum(CuiElementContainer container, string parent, string leftCommand, string rightCommand, int index, string[] names, string[] icons)
		{
			var content = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0.5" }, RectTransform = { AnchorMin = $"0.55 0", AnchorMax = "1 1" } }, parent);

			if (icons != null)
			{
				container.Add(new CuiLabel { Text = { Text = names[index], FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.75" }, RectTransform = { AnchorMin = $"0.3 0", AnchorMax = $"1 1" } }, content);
				container.Add(new CuiElement { Parent = content, Components = { Death.GetRawImage(icons[index]), new CuiRectTransformComponent { AnchorMin = $"0.15 0.2", AnchorMax = "0.26 0.8" } } });
			}
			else
			{
				container.Add(new CuiLabel { Text = { Text = names[index], FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.75" }, RectTransform = { AnchorMin = $"0.031 0", AnchorMax = $"1 1" } }, content);
			}
			container.Add(new CuiButton { Button = { Command = leftCommand, Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = "0.15 1" }, Text = { Text = $"<b><</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
			container.Add(new CuiButton { Button = { Command = rightCommand, Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0.85 0", AnchorMax = "1 1" }, Text = { Text = $"<b>></b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
		}
		public void DrawToggle(CuiElementContainer container, string parent, string command, bool enabled)
		{
			var content = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0.5" }, RectTransform = { AnchorMin = $"0.55 0", AnchorMax = "1 1" } }, parent);
			container.Add(new CuiButton { Button = { Command = command, Color = enabled ? "0.5 0.8 0.3 0.8" : "0.7 0.3 0.3 0.8" }, RectTransform = { AnchorMin = $"0.01 0.05", AnchorMax = "0.98 0.85" }, Text = { Text = $"<b>{(enabled ? "Enabled" : "Disabled")}</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
		}
		public void DrawButton(CuiElementContainer container, string parent, string command, string text, float xMin = 0.8f, float xMax = 0.985f, float yMin = 0.05f, float yMax = 0.87f, bool transparent = false)
		{
			var content = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0.5" }, RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" } }, parent);
			container.Add(new CuiButton { Button = { Command = command, Color = $"0.3 0.4 0.8 {(transparent ? 0.4f : 0.8f)}" }, RectTransform = { AnchorMin = $"0.01 0", AnchorMax = "0.985 1" }, Text = { Text = $"<b>{text}</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8 } }, content);
		}
		public void DrawButtonImage(CuiElementContainer container, string parent, string command, string image, float min = 0.8f, float max = 0.985f, bool selected = false)
		{
			var content = container.Add(new CuiPanel { Image = { Color = $"0 0 0 0.5" }, RectTransform = { AnchorMin = $"{min} 0", AnchorMax = $"{max} 1" } }, parent);
			var button = container.Add(new CuiButton { Button = { Command = command, Color = !selected ? "0.3 0.3 0.3 0.4" : $"0.3 0.4 0.8 {(selected ? 1f : 0.8f)}" }, RectTransform = { AnchorMin = $"0.01 0.025", AnchorMax = "0.985 0.975" }, Text = { Text = string.Empty, Color = "0 0 0 0" } }, content);
			container.Add(new CuiElement { Parent = button, Components = { new CuiImageComponent { Sprite = image, Color = !selected ? "0.7 0.7 0.7 0.7" : "0.5 0.7 0.9" }, new CuiRectTransformComponent { AnchorMin = $"0.2 0.2", AnchorMax = "0.8 0.8" } } });
		}

		#region Commands

		[ConsoleCommand(Editor_Cmd + "save")]
		private void EditorSave(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			if (Config.Interactions.Count > 0)
			{
				var lootable = GetDefinition(editor.EditingDefinition.PrefabFilter, editor.Transform.position, editor.EditingDefinition.IsUnique());

				if (lootable == null)
				{
					lootable = new RootConfig.LootableDefinition();
					Config.Definitions.Add(lootable);
				}

				Editor.ApplyEdit(editor.EditingDefinition, lootable);
				editor.IsEditing = false;
				UndrawEditor(player, false);
				timer.In(1f, () => OnServerSave());
			}
			else
			{
				DrawEditor(player, message: "You may not add any lootables yet, please <b>Close</b> this panel and press <b>[RELOAD]</b> when the prefab name shows up to configre at least one Interaction setting.");
			}
		}
		[ConsoleCommand(Editor_Cmd + "cancel")]
		private void EditorCancel(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			editor.IsEditing = false;
			editor.EditingInteraction = null;
			editor.Setting = SettingModes.Main;

			UndrawEditor(player, false);
		}
		[ConsoleCommand(Editor_Cmd + "path")]
		private void EditorPath(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			editor.EditingDefinition.PrefabFilter = editor.EditingDefinition.PrefabFilter.Contains("/") ? editor.Transform.name.Split(' ')[0] : editor.Transform.GetRecursiveName().ToLower();

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "unique")]
		private void EditorUnique(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var definition = GetDefinition(editor.Transform);

			if (!editor.EditingDefinition.IsUnique())
			{
				var position = editor.Transform.position;
				if (definition != null) editor.EditingDefinition = new RootConfig.LootableDefinition();

				Editor.StartEdit(definition ?? new RootConfig.LootableDefinition(editor.EditingDefinition.PrefabFilter, editor.EditingDefinition.ContainerSize, new List<RootLootableItemDefinition>()), editor.EditingDefinition);

				editor.EditingDefinition.UniqueId = $"{position.x}_{position.y}_{position.z}";
			}
			else
			{
				Editor.StartEdit(definition ?? new RootConfig.LootableDefinition(editor.Transform.name.Split(' ')[0].ToLower(), 2, new List<RootLootableItemDefinition>()), editor.EditingDefinition);
			}

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "info")]
		private void EditorInfo(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var split = arg.FullString.Split('|');
			DrawEditor(player, editor.Mode, message: split[1], messageTitle: split[0]);
		}
		[ConsoleCommand(Editor_Cmd + "delete")]
		private void EditorDelete(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var definition = GetDefinition(editor.Transform);

			if (definition != null)
			{
				Data.Lootables.RemoveAll(x => x.GetDefinition() == definition);
				Config.Definitions.Remove(definition);
			}

			UndrawEditor(player, false);

			OnServerSave();
		}
		[ConsoleCommand(Editor_Cmd + "update_prefabfilter")]
		private void EditorPrefabFilter(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var content = Join(arg.Args, " ");

			if (!string.IsNullOrEmpty(content)) try { editor.EditingDefinition.PrefabFilter = content; } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_interaction")]
		private void EditorInteraction(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.InteractionIndex += int.Parse(arg.Args[0]); } catch { }

			if (editor.EditingDefinition.InteractionIndex < 0) editor.EditingDefinition.InteractionIndex = Config.Interactions.Count - 1;
			else if (editor.EditingDefinition.InteractionIndex > Config.Interactions.Count - 1) editor.EditingDefinition.InteractionIndex = 0;

			var effect = Config.Interactions.Count > 0 ? Config.Interactions[editor.EditingDefinition.InteractionIndex].OpenEffect : null;
			if (!string.IsNullOrEmpty(effect))
			{
				SendEffectTo(effect, player);
			}

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_panel")]
		private void EditorPanel(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var index = 0;

			try { index = PanelTypesList.IndexOf(editor.EditingDefinition.Panel) + int.Parse(arg.Args[0]); } catch { }

			if (index < 0) index = PanelTypesList.Count - 1;
			else if (index > PanelTypesList.Count - 1) index = 0;

			editor.EditingDefinition.Panel = PanelTypesList[index];

			DrawEditor(player);
		}

		[ConsoleCommand(Editor_Cmd + "update_containersize")]
		private void EditorContainerSize(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.ContainerSize = int.Parse(arg.Args[0]); } catch { }

			editor.EditingDefinition.ContainerSize = Clamp(editor.EditingDefinition.ContainerSize, 0, 32);

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_allowstack")]
		private void EditorAllowStack(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.AllowStack = !editor.EditingDefinition.AllowStack; } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_liquid")]
		private void EditorLiquid(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.Liquid = !editor.EditingDefinition.Liquid; } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_persistent")]
		private void EditorPresistent(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.Persistent = !editor.EditingDefinition.Persistent; } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_timer")]
		private void EditorTimer(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var content = Join(arg.Args, " ");

			if (!string.IsNullOrEmpty(content)) try { editor.EditingDefinition.Timer = float.Parse(content); } catch { }
			editor.EditingDefinition.Timer = Mathf.Clamp(editor.EditingDefinition.Timer, 0f, 30f);

			if (editor.EditingDefinition.Timer > 0 && editor.EditingDefinition.Hack != null)
			{
				editor.EditingDefinition.Hack = null;
				DrawEditor(player, message: "Since you've set a <b>Holding Timer [USE]</b> for this lootable, you may not be having a <b>Hack</b> on the same lootable due to conflicts.");
			}
			else
			{
				DrawEditor(player);
			}
		}
		[ConsoleCommand(Editor_Cmd + "update_lock")]
		private void EditorLock(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.Lock = editor.EditingDefinition.Lock == null ? new RootLock() : null; } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_hack")]
		private void EditorHack(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.Hack = editor.EditingDefinition.Hack == null ? new RootHack() : null; } catch { }

			if (editor.EditingDefinition.Hack != null && editor.EditingDefinition.Timer > 0)
			{
				editor.EditingDefinition.Timer = 0f;
				DrawEditor(player, message: "Since you've set a <b>Hack</b> to this lootable, you may not be having a <b>Holding Timer [USE]</b> on the same lootable due to conflicts.");
			}
			else
			{
				DrawEditor(player);
			}
		}
		[ConsoleCommand(Editor_Cmd + "update_command")]
		private void EditorCommand(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var content = Join(arg.Args, " ");
			editor.EditingDefinition.Command = content;

			DrawEditor(player, message: string.IsNullOrEmpty(editor.EditingDefinition.Command) ? null : "You've set this lootable preset as command-only. Container Size, Contents, Persistent and other container-generating properties aren't used while there's a Command.", messageTitle: "Command Applied");
		}
		[ConsoleCommand(Editor_Cmd + "update_lockhealth")]
		private void EditorLockHealth(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.Lock.Health = int.Parse(arg.Args[0]); } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_hackwaittime")]
		private void EditorHackWaitTime(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.Hack.WaitTime = float.Parse(arg.Args[0]); } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_hackcoderesetrate")]
		private void EditorHackCodeResetRate(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingDefinition.Hack.CodeResetRate = float.Parse(arg.Args[0]); } catch { }

			DrawEditor(player);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules")]
		private void EditorRules(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			GetOrCreateEditor(player);

			DrawEditor(player, EditModes.Rules);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents")]
		private void EditorContents(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);

			var type = int.Parse(arg.Args[0]);
			switch (type)
			{
				case 0:
					editor.EditingContents = editor.EditingDefinition.Contents;
					break;

				case 1:
					editor.EditingContents = editor.EditingLootableItem.Contents;
					break;
			}

			DrawEditor(player, EditModes.Contents);
		}

		[ConsoleCommand(Editor_Cmd + "update_rules_onlyifparentfilter")]
		private void EditorRulesOnlyIfParentFilter(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			GetOrCreateEditor(player);

			DrawEditor(player, EditModes.OnlyIfParentFilter);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules_onlyifnotparentfilter")]
		private void EditorRulesOnlyIfNotParentFilter(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			GetOrCreateEditor(player);

			DrawEditor(player, EditModes.OnlyIfNotParentFilter);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules_onlyifinzone")]
		private void EditorRulesOnlyIfInZone(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			GetOrCreateEditor(player);

			DrawEditor(player, EditModes.OnlyIfInZone);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules_onlyifnotinzone")]
		private void EditorRulesOnlyIfNotInZone(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			GetOrCreateEditor(player);

			DrawEditor(player, EditModes.OnlyIfNotInZone);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules_add")]
		private void EditorRulesAdd(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var list = editor.GetRuleList();
			var value = Join(arg.Args, " ");
			if (!string.IsNullOrEmpty(value))
				list.Add(Join(arg.Args, " "));

			DrawEditor(player, editor.Mode);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules_delete")]
		private void EditorRulesDelete(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var list = editor.GetRuleList();
			list.RemoveAt(int.Parse(arg.Args[0]));

			DrawEditor(player, editor.Mode);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules_move")]
		private void EditorRulesMove(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var list = editor.GetRuleList();
			try
			{
				var value = list[int.Parse(arg.Args[0])];
				var index = list.IndexOf(value);
				list.Remove(value);

				if (index + int.Parse(arg.Args[1]) <= list.Count && index + int.Parse(arg.Args[1]) >= 0)
					list.Insert(index + int.Parse(arg.Args[1]), value);
			}
			catch { }

			DrawEditor(player, editor.Mode);
		}
		[ConsoleCommand(Editor_Cmd + "update_rules_refillrate")]
		private void EditorRulesRefillRate(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			editor.EditingDefinition.Rule.RefillRate = float.Parse(arg.Args[0]);
			editor.EditingDefinition.Rule.RefillRate = Clamp(editor.EditingDefinition.Rule.RefillRate, 1f, 500f);

			DrawEditor(player, editor.Mode);
		}

		[ConsoleCommand(Editor_Cmd + "update_back")]
		private void EditorBack(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			if (editor.EditingLootableItem2 == null) editor.EditingLootableItem = null; else editor.EditingLootableItem2 = null;

			if (editor.Setting == SettingModes.AddInteraction)
			{
				editor.EditingInteraction = null;
				editor.Setting = SettingModes.Interactions;
				DrawEditor(player, EditModes.Settings);
				return;
			}

			editor.Setting = SettingModes.Main;
			editor.EditingInteraction = null;

			var mode = editor.Mode;
			DrawEditor(player, mode == EditModes.Settings ? EditModes.Settings : mode == EditModes.OnlyIfParentFilter ||
				mode == EditModes.OnlyIfNotParentFilter ||
				mode == EditModes.OnlyIfInZone ||
				mode == EditModes.OnlyIfNotInZone ? EditModes.Rules : mode == EditModes.AddItem ?
					EditModes.Contents :
					EditModes.Main);
		}

		[ConsoleCommand(Editor_Cmd + "resetcontents")]
		[Permission(EditorPerm)]
		private void ResetContents(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var lootable = GetLootable(player, editor.Transform.gameObject);

			if (lootable != null)
			{
				lootable.ClearContainer();

				var definition = lootable.GetDefinition();
				lootable.ApplyLock(definition);
				lootable.RandomizeContents(definition);

				DrawEditor(player, message: "You've successfully reset the lootable container.", messageTitle: "Success!");
			}
			else DrawEditor(player, message: "There's no container for this lootable yet. Interact with it first?");
		}
		[ConsoleCommand(Editor_Cmd + "resetallcontents")]
		private void ResetAllContents(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var counter = 0;

			foreach (var lootable in Data.Lootables)
			{
				if (editor.Transform.name.Contains(lootable.Name)) continue;

				lootable.ClearContainer();

				var definition = lootable.GetDefinition();
				lootable.ApplyLock(definition);
				lootable.RandomizeContents(definition);
				counter++;
			}

			if (counter > 0) DrawEditor(player, message: $"You've successfully reset {counter:n0} lootable containers.", messageTitle: "Success!");
			else DrawEditor(player, message: "There's no container for this lootable yet. Interact with any first?");
		}

		[ConsoleCommand(Editor_Cmd + "update_content_edit")]
		private void EditorContentEdit(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);

			if (arg.Args[1] == "1")
			{
				editor.EditingLootableItem2 = editor.EditingContents[int.Parse(arg.Args[0])];
			}
			else
			{
				editor.EditingLootableItem = editor.EditingContents[int.Parse(arg.Args[0])];
			}

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_content_delete")]
		private void EditorContentDelete(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try
			{
				editor.EditingContents.RemoveAt(int.Parse(arg.Args[0]));
				editor.EditingLootableItem = null;
				editor.EditingLootableItem2 = null;
			}
			catch { }

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_content_move")]
		private void EditorContentMove(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try
			{
				var value = editor.EditingContents[int.Parse(arg.Args[0])];
				var index = editor.EditingContents.IndexOf(value);
				editor.EditingContents.Remove(value);

				if (index + int.Parse(arg.Args[1]) <= editor.EditingContents.Count && index + int.Parse(arg.Args[1]) >= 0)
					editor.EditingContents.Insert(index + int.Parse(arg.Args[1]), value);
			}
			catch { }

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_content_add")]
		private void EditorContentAdd(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			if (arg.Args == null || arg.Args.Length == 0)
			{
				DrawEditor(player, EditModes.AddItem);
			}
			else
			{
				editor.EditingContents.Add(new RootLootableItemDefinition
				{
					ShortName = arg.Args[0]
				});

				DrawEditor(player, EditModes.Contents);
			}
		}

		[ConsoleCommand(Editor_Cmd + "update_contents_customname")]
		private void EditorContentCustomName(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			editor.EditingLootableItem.CustomName = Join(arg.Args, " ");

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_skinid")]
		private void EditorContentSkinId(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.SkinId = ulong.Parse(Join(arg.Args, " ")); } catch { }

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_text")]
		private void EditorContentText(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.Text = Join(arg.Args, " "); } catch { }

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_minamount")]
		private void EditorContentMinAmount(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.MinimumAmount = int.Parse(Join(arg.Args, " ")); } catch { }

			editor.EditingLootableItem.MinimumAmount = Clamp(editor.EditingLootableItem.MinimumAmount, 0, editor.EditingLootableItem.MaximumAmount);

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_maxamount")]
		private void EditorContentMaxAmount(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.MaximumAmount = int.Parse(Join(arg.Args, " ")); } catch { }

			var item = ItemManager.FindItemDefinition(editor.EditingLootableItem.ShortName);
			editor.EditingLootableItem.MaximumAmount = Clamp(editor.EditingLootableItem.MaximumAmount, editor.EditingLootableItem.MinimumAmount, item.stackable);

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_conditionminamount")]
		private void EditorContentConditionMinAmount(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			Puts(Join(arg.Args, " "));

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.ConditionMinimumAmount = float.Parse(Join(arg.Args, " ")); } catch { }

			editor.EditingLootableItem.ConditionMinimumAmount = Clamp(editor.EditingLootableItem.ConditionMinimumAmount, 0, editor.EditingLootableItem.ConditionMaximumAmount);

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_conditionmaxamount")]
		private void EditorContentConditionMaxAmount(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.ConditionMaximumAmount = float.Parse(Join(arg.Args, " ")); } catch { }

			editor.EditingLootableItem.ConditionMaximumAmount = Clamp(editor.EditingLootableItem.ConditionMaximumAmount, editor.EditingLootableItem.ConditionMinimumAmount, 1f);

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_spawnchancetimes")]
		private void EditorContentSpawnChanceTimes(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.SpawnChanceTimes = int.Parse(Join(arg.Args, " ")); } catch { }

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_spawnchancescale")]
		private void EditorContentSpawnChanceScale(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.SpawnChanceScale = int.Parse(Join(arg.Args, " ")); } catch { }

			DrawEditor(player, EditModes.Contents);
		}

		[ConsoleCommand(Editor_Cmd + "update_contents_blueprintchancetimes")]
		private void EditorContentBlueprintChanceTimes(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.BlueprintChanceTimes = int.Parse(Join(arg.Args, " ")); } catch { }

			DrawEditor(player, EditModes.Contents);
		}
		[ConsoleCommand(Editor_Cmd + "update_contents_blueprintchancescale")]
		private void EditorContentBlueprintChanceScale(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.EditingLootableItem.BlueprintChanceScale = int.Parse(Join(arg.Args, " ")); } catch { }

			DrawEditor(player, EditModes.Contents);
		}

		[ConsoleCommand(Editor_Cmd + "update_additem_filter")]
		private void EditorAddItemFilter(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			try { editor.ItemSearchFilter = Join(arg.Args, " ").Trim(); } catch { }

			DrawEditor(player, EditModes.AddItem);
		}
		[ConsoleCommand(Editor_Cmd + "update_additem_category")]
		private void EditorAddItemCategory(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var page = editor.GetPage(4);
			page.CurrentPage = 0;

			if (editor.ItemSearchCategory == int.Parse(arg.Args[0])) editor.ItemSearchCategory = -1;
			else
			{
				try { editor.ItemSearchCategory = int.Parse(arg.Args[0]); } catch { }
			}

			DrawEditor(player, EditModes.AddItem);
		}

		[ConsoleCommand(Editor_Cmd + "ext_create")]
		private void ExtCreate(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			LootablesExt?.Call("SLExt_Start", player, editor.ExtPrefabName);

			UndrawEditor(player, false);
		}
		[ConsoleCommand(Editor_Cmd + "ext_delete")]
		private void ExtDelete(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			LootablesExt?.Call("SLExt_Delete", editor.Transform);

			UndrawEditor(player, false);
		}
		[ConsoleCommand(Editor_Cmd + "ext_ping")]
		private void ExtPing(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			LootablesExt?.Call("SLExt_Ping", player, 10f, 20f);

			UndrawEditor(player, false);
		}

		[ConsoleCommand(Editor_Cmd + "settings_asyncmode")]
		private void SettingsAsyncMode(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			Config.AsyncMode = !Config.AsyncMode;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_middlemouseinfo")]
		private void SettingsMiddleMouseInfo(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			Config.MiddleMouseInfo = !Config.MiddleMouseInfo;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_distance")]
		private void SettingsDistance(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.Distance = int.Parse(arg.Args[0]); } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_lockname")]
		private void SettingsLockName(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var value = Join(arg.Args, " ");

			try { if (!string.IsNullOrEmpty(value)) Config.LockName = value; } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_busy")]
		private void SettingsBusy(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var value = Join(arg.Args, " ");

			try { if (!string.IsNullOrEmpty(value)) Config.BusyName = value; } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_hackname")]
		private void SettingsHackName(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var value = Join(arg.Args, " ");

			try { if (!string.IsNullOrEmpty(value)) Config.HackName = value; } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_hackingname")]
		private void SettingsHackingName(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var value = Join(arg.Args, " ");

			try { if (!string.IsNullOrEmpty(value)) Config.HackingName = value; } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_codeinputtitle")]
		private void SettingsCodeInputTitle(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var value = Join(arg.Args, " ");

			try { if (!string.IsNullOrEmpty(value)) Config.CodeInputTitle = Join(arg.Args, " "); } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_interactions")]
		private void SettingsInteractions(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			editor.Setting = SettingModes.Interactions;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_interactions_delete")]
		private void SettingsInteractionsDelete(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			Config.Interactions.RemoveAt(int.Parse(arg.Args[0]));

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "settings_interactions_edit")]
		private void SettingsInteractionsEdit(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			editor.Setting = SettingModes.Interactions;
			editor.EditingInteraction = Config.Interactions[int.Parse(arg.Args[0])];

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_updatetext")]
		private void EditorSettingsUpdateText(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var interaction = editor.EditingInteraction;
			var value = Join(arg.Args, " ");
			if (!string.IsNullOrEmpty(value)) interaction.Text = value;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_updateicon")]
		private void EditorSettingsUpdateIcon(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var interaction = editor.EditingInteraction;
			var value = Join(arg.Args, " ");
			if (!string.IsNullOrEmpty(value)) interaction.Icon = value;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_updateopeneffect")]
		private void EditorSettingsUpdateOpenEffect(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var interaction = editor.EditingInteraction;
			var value = Join(arg.Args, " ");
			if (!string.IsNullOrEmpty(value)) interaction.OpenEffect = value;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_addinteraction")]
		private void EditorSettingsAddInteraction(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			switch (editor.Setting)
			{
				case SettingModes.AddInteraction:
					{
						Config.Interactions.Add(editor.EditingInteraction);
						editor.EditingInteraction = null;
						editor.Setting = SettingModes.Interactions;
					}
					break;

				default:
					{
						editor.EditingInteraction = new RootConfig.Interaction
						{
							Text = "New Open",
							Icon = "https://raulssorban.tv/wp-content/uploads/plugins/sl_open.png",
							OpenEffect = "assets/prefabs/deployable/locker/effects/locker-deploy.prefab"
						};
						editor.Setting = SettingModes.AddInteraction;
					}
					break;
			}
			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_itemamountmultiplier")]
		private void SettingsItemAmountMultiplier(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.ItemAmountMultiplier = float.Parse(arg.Args[0]); } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_lootablehealthmultiplier")]
		private void SettingsLootableHealthMultiplier(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.LootableHealthMultiplier = float.Parse(arg.Args[0]); } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_shadowopacity")]
		private void SettingsShadowOpacity(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.ShadowOpacity = float.Parse(arg.Args[0]); } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_showlockedlootableapproximatecount")]
		private void SettingsShowLockedLootableApproximateCount(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			Config.ShowLockedLootableApproximateCount = !Config.ShowLockedLootableApproximateCount;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_damagemultipliermin")]
		private void SettingsDamageMultiplierMinimum(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.DamageMultiplierMinimum = float.Parse(arg.Args[0]); } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_damagemultipliermax")]
		private void SettingsDamageMultiplierMaximum(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.DamageMultiplierMaximum = float.Parse(arg.Args[0]); } catch { }

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_damagecuirefreshrate")]
		private void SettingsDamageCUIRefreshRate(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.DamageCUIRefreshRate = float.Parse(arg.Args[0]); } catch { }

			if (Config.DamageCUIRefreshRate < 0) Config.DamageCUIRefreshRate = 0;
			else if (Config.DamageCUIRefreshRate > 1f) Config.DamageCUIRefreshRate = 1f;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_settings_waitingcuirefreshrate")]
		private void SettingsWaitingCUIRefreshRate(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			try { Config.WaitingCUIRefreshRate = float.Parse(arg.Args[0]); } catch { }

			if (Config.WaitingCUIRefreshRate < 0) Config.WaitingCUIRefreshRate = 0;
			else if (Config.WaitingCUIRefreshRate > 1f) Config.WaitingCUIRefreshRate = 1f;

			DrawEditor(player, EditModes.Settings);
		}
		[ConsoleCommand(Editor_Cmd + "update_ext_prefabname")]
		private void SettingsExtPrefabName(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = Instance.GetOrCreateEditor(player);
			editor.ExtPrefabName = string.Join(" ", arg.Args);

			DrawEditor(player, EditModes.Settings);
		}

		[ConsoleCommand(Editor_Cmd + "page_back")]
		private void EditorPageBack(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var page = editor.GetPage(int.Parse(arg.Args[0]));
			page.CurrentPage--;
			page.Check();

			DrawEditor(player, editor.Mode);
		}
		[ConsoleCommand(Editor_Cmd + "page_forward")]
		private void EditorPageForward(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !permission.UserHasPermission(player.UserIDString, EditorPerm)) return;

			var editor = GetOrCreateEditor(player);
			var page = editor.GetPage(int.Parse(arg.Args[0]));
			page.CurrentPage++;
			page.Check();

			DrawEditor(player, editor.Mode);
		}
		#endregion

		public class Editor
		{
			public bool IsEditing { get; set; } = false;
			public EditModes Mode { get; set; } = EditModes.Main;
			public SettingModes Setting { get; set; } = SettingModes.Main;
			public string ExtPrefabName { get; set; }
			public RootConfig.LootableDefinition EditingDefinition { get; set; } = new RootConfig.LootableDefinition();
			public RootLootableItemDefinition EditingLootableItem { get; set; }
			public List<RootLootableItemDefinition> EditingContents { get; set; }
			public RootLootableItemDefinition EditingLootableItem2 { get; set; }
			public Dictionary<int, Page> Pages { get; set; } = new Dictionary<int, Page>();
			public RootConfig.Interaction EditingInteraction { get; set; }

			public Page GetPage(int id)
			{
				if (Pages.ContainsKey(id)) return Pages[id];

				var page = new Page();
				Pages.Add(id, page);

				return page;
			}

			public int ItemSearchCategory { get; set; } = -1;
			public string ItemSearchFilter { get; set; } = string.Empty;
			public Transform Transform { get; set; }

			public static void ApplyEdit(RootConfig.LootableDefinition source, RootConfig.LootableDefinition target)
			{
				target.UniqueId = source.UniqueId;
				target.Command = source.Command;
				target.PrefabFilter = source.PrefabFilter;
				target.InteractionIndex = source.InteractionIndex;
				target.Panel = source.Panel;
				target.ContainerSize = source.ContainerSize;
				target.AllowStack = source.AllowStack;
				target.Liquid = source.Liquid;
				target.Lock = source.Lock;
				target.Persistent = source.Persistent;
				target.Timer = source.Timer;
				target.Hack = source.Hack;
				target.Rule = source.Rule;
				target.Contents.Clear();

				foreach (var content in source.Contents)
				{
					var definition = new RootLootableItemDefinition
					{
						ShortName = content.ShortName,
						CustomName = content.CustomName,
						SkinId = content.SkinId,
						Text = content.Text,
						UseRandomSkins = content.UseRandomSkins,
						RandomSkins = content.RandomSkins,
						MinimumAmount = content.MinimumAmount,
						MaximumAmount = content.MaximumAmount,
						ConditionMinimumAmount = content.ConditionMinimumAmount,
						ConditionMaximumAmount = content.ConditionMaximumAmount,
						SpawnChanceTimes = content.SpawnChanceTimes,
						SpawnChanceScale = content.SpawnChanceScale,
						BlueprintChanceTimes = content.BlueprintChanceTimes,
						BlueprintChanceScale = content.BlueprintChanceScale
					};

					target.Contents.Add(definition);

					CopyRecursiveList(content, definition);
				}
			}
			public static void StartEdit(RootConfig.LootableDefinition source, RootConfig.LootableDefinition target)
			{
				target.UniqueId = source.UniqueId;
				target.Command = source.Command;
				target.PrefabFilter = source.PrefabFilter;
				target.InteractionIndex = source.InteractionIndex;
				target.Panel = source.Panel;
				target.ContainerSize = source.ContainerSize;
				target.AllowStack = source.AllowStack;
				target.Liquid = source.Liquid;
				target.Persistent = source.Persistent;
				target.Timer = source.Timer;
				target.Lock = source.Lock == null ? null : new RootLock { Health = source.Lock.Health };
				target.Hack = source.Hack == null ? null : new RootHack { WaitTime = source.Hack.WaitTime, CodeResetRate = source.Hack.CodeResetRate };
				target.Rule = new RootConfig.Rule { RefillRate = source.Rule.RefillRate };
				foreach (var item in source.Rule.OnlyIfParentFilter) target.Rule.OnlyIfParentFilter.Add(item);
				foreach (var item in source.Rule.OnlyIfNotParentFilter) target.Rule.OnlyIfNotParentFilter.Add(item);
				foreach (var item in source.Rule.OnlyIfInZone) target.Rule.OnlyIfInZone.Add(item);
				foreach (var item in source.Rule.OnlyIfNotInZone) target.Rule.OnlyIfNotInZone.Add(item);

				target.Contents = new List<RootLootableItemDefinition>();

				for (int i = 0; i < source.Contents.Count; i++)
				{
					var item = source.Contents[i];
					var definition = new RootLootableItemDefinition
					{
						ShortName = item.ShortName,
						CustomName = item.CustomName,
						SkinId = item.SkinId,
						Text = item.Text,
						UseRandomSkins = item.UseRandomSkins,
						RandomSkins = item.RandomSkins,
						MinimumAmount = item.MinimumAmount,
						MaximumAmount = item.MaximumAmount,
						ConditionMinimumAmount = item.ConditionMinimumAmount,
						ConditionMaximumAmount = item.ConditionMaximumAmount,
						SpawnChanceTimes = item.SpawnChanceTimes,
						SpawnChanceScale = item.SpawnChanceScale,
						BlueprintChanceTimes = item.BlueprintChanceTimes,
						BlueprintChanceScale = item.BlueprintChanceScale
					};

					target.Contents.Add(definition);

					CopyRecursiveList(item, definition);
				}
			}

			public static void CopyRecursiveList(RootLootableItemDefinition definition, RootLootableItemDefinition target)
			{
				foreach (var content in definition.Contents)
				{
					var subDefinition = new RootLootableItemDefinition
					{
						ShortName = content.ShortName,
						CustomName = content.CustomName,
						SkinId = content.SkinId,
						Text = content.Text,
						UseRandomSkins = content.UseRandomSkins,
						RandomSkins = content.RandomSkins,
						MinimumAmount = content.MinimumAmount,
						MaximumAmount = content.MaximumAmount,
						ConditionMinimumAmount = content.ConditionMinimumAmount,
						ConditionMaximumAmount = content.ConditionMaximumAmount,
						SpawnChanceTimes = content.SpawnChanceTimes,
						SpawnChanceScale = content.SpawnChanceScale,
						BlueprintChanceTimes = content.BlueprintChanceTimes,
						BlueprintChanceScale = content.BlueprintChanceScale
					};

					target.Contents.Add(subDefinition);

					CopyRecursiveList(content, subDefinition);
				}
			}

			public List<string> GetRuleList()
			{
				var list = Mode == EditModes.OnlyIfParentFilter ? EditingDefinition.Rule.OnlyIfParentFilter :
					Mode == EditModes.OnlyIfNotParentFilter ? EditingDefinition.Rule.OnlyIfNotParentFilter :
					Mode == EditModes.OnlyIfInZone ? EditingDefinition.Rule.OnlyIfInZone :
					Mode == EditModes.OnlyIfNotInZone ? EditingDefinition.Rule.OnlyIfNotInZone : EditingDefinition.Rule.OnlyIfInZone;

				return list;
			}

			public class Page
			{
				public int CurrentPage { get; set; }
				public int TotalPages { get; set; }

				public void Check()
				{
					if (CurrentPage < 0) CurrentPage = TotalPages;
					else if (CurrentPage > TotalPages) CurrentPage = 0;
				}
			}
		}

		#endregion

		#region Mono

		public LootablesMono LootablesManagerBehaviour { get; private set; }
		private void CreateBehaviour()
		{
			ClearBehaviour();

			var gameObject = new GameObject("LootablesMono");
			LootablesManagerBehaviour = gameObject.AddComponent<LootablesMono>();
		}
		private void ClearBehaviour()
		{
			if (LootablesManagerBehaviour != null)
			{
				UnityEngine.Object.Destroy(LootablesManagerBehaviour.gameObject);
			}
		}

		public class LootablesMono : FacepunchBehaviour
		{
			public void Update()
			{
				foreach (var player in BasePlayer.activePlayerList)
				{
					Instance.DoPlayerInput(player, player.serverInput);
				}
			}
		}

		#endregion
	}
}
