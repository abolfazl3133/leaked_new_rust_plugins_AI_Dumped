using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oxide.Plugins
{
    [Info("AuleManagement", "RAREGUN▲", "1.0.2e")]
	// 17D 06M 2019Y
	// AuleManagement
	//    ___    ___    ___    ____  _____  __  __   _  __
	//   / _ \  / _ |  / _ \  / __/ / ___/ / / / /  / |/ /
	//  / , _/ / __ | / , _/ / _/  / (_ / / /_/ /  /    /
	// /_/|_| /_/ |_|/_/|_| /___/  \___/  \____/  /_/|_/  ^

    public class AuleManagement : RustPlugin
    {
#region VARS

		private DynamicConfigFile DataFile = Interface.Oxide.DataFileSystem.GetFile("AuleManagement");
		private Dictionary<ulong, DataCell> DataLocal;
		
		private class DataCell
		{
			public Vector3 lastPos;
			public Dictionary<string, bool> states;

			public bool GetSetting(string k) => states[k];

			public DataCell(Vector3 _lastPos)
			{
				lastPos = _lastPos;
				states = new Dictionary<string, bool>
				{
					{ "typeResources", true },
					{ "typePicklock", true },
					{ "typeAntiAttack", true },
					{ "typeGodMode", true },
					{ "typeAuleHammer", true },
					{ "typeAntiKick", true },
					{ "typeInfAmmo", true }
				};
			}
		}
		
		private Dictionary<string, string> blockList = new Dictionary<string, string>
		{
			{"stairs.u/block.stair.ushape", "Лестницы U-образной"},
			{"stairs.l/block.stair.lshape", "Лестницы L-образной"},
			{"wall.low/wall.low", "Низкой стены"},
			{"wall.half/wall.half", "Половины стены"},
			{"roof/roof", "Крышы"},
			{"floor.frame/floor.frame", "Каркаса"},
			{"wall.frame/wall.frame", "Каркаса"},
			{"floor.triangle/floor.triangle", "Треуг. потолка"},
			{"floor/floor", "Потолка"},
			{"wall.window/wall.window", "Окна"},
			{"wall.doorway/wall.doorway", "Дверного проема"},
			{"wall/wall", "Стены"},
			{"foundation/foundation", "Фундамента"},
			{"foundation.triangle/foundation.triangle", "Треуг. фундамента"},
			{"foundation.steps/foundation.steps", "Ступенек"}
		};

		private bool GetPlayerSetting(BasePlayer d, string s) => CellExist(d) && DataLocal[d.userID].GetSetting(s);

#endregion

#region INIT

        private void OnServerInitialized()
        {
            LoadData();

			foreach (BasePlayer d in BasePlayer.activePlayerList.Where(IsOurTarget).ToList())
			{
				CheckCell(d);
				if (GetPlayerSetting(d, "typeResources")) AddAntiResources(d);

				actualUsers++;
			}
			
			CheckPluginActivity();
        }

        private void OnServerSave() => ServerMgr.Instance.StartCoroutine(SaveData(false));

        private void Unload()
		{
			ServerMgr.Instance.StartCoroutine(SaveData(true));
			BasePlayer.activePlayerList.Where(IsOurTarget).ToList().ForEach(RemoveAntiResources);
		}

		private void LoadData()
		{
			DataLocal = DataFile.ReadObject<Dictionary<ulong, DataCell>>() ?? new Dictionary<ulong, DataCell>();
		}

        private IEnumerator SaveData(bool u)
		{
			DataFile.WriteObject(DataLocal ?? new Dictionary<ulong, DataCell>());
			yield return new WaitForEndOfFrame();
		}

#endregion

#region TOGGLER

		private void EnablePlugin()
		{
			Subscribe(nameof(OnPlayerInput));
			Subscribe(nameof(OnPlayerRespawned));
			Subscribe(nameof(OnPlayerViolation));
			Subscribe(nameof(OnRunPlayerMetabolism));
			Subscribe(nameof(CanBeWounded));
			Subscribe(nameof(OnEntityTakeDamage));
			Subscribe(nameof(CanUseLockedEntity));
			Subscribe(nameof(OnTurretTarget));
			Subscribe(nameof(CanBradleyApcTarget));
			Subscribe(nameof(CanHelicopterTarget));
			Subscribe(nameof(OnTrapTrigger));
			Subscribe(nameof(OnPlayerLand));
			Subscribe(nameof(CanBeTargeted));
			Subscribe(nameof(OnServerMessage));
			Subscribe(nameof(OnWeaponFired));
			Subscribe(nameof(OnRocketLaunched));
		}
		
		private void DisablePlugin()
		{
			Unsubscribe(nameof(OnPlayerInput));
			Unsubscribe(nameof(OnPlayerRespawned));
			Unsubscribe(nameof(OnPlayerViolation));
			Unsubscribe(nameof(OnRunPlayerMetabolism));
			Unsubscribe(nameof(CanBeWounded));
			Unsubscribe(nameof(OnEntityTakeDamage));
			Unsubscribe(nameof(CanUseLockedEntity));
			Unsubscribe(nameof(OnTurretTarget));
			Unsubscribe(nameof(CanBradleyApcTarget));
			Unsubscribe(nameof(CanHelicopterTarget));
			Unsubscribe(nameof(OnTrapTrigger));
			Unsubscribe(nameof(OnPlayerLand));
			Unsubscribe(nameof(CanBeTargeted));
			Unsubscribe(nameof(OnServerMessage));
			Unsubscribe(nameof(OnWeaponFired));
			Unsubscribe(nameof(OnRocketLaunched));
		}

		private void CheckPluginActivity()
		{
			if (actualUsers == 0 && state)
			{
				DisablePlugin();
				state = false;
			}
			else if (actualUsers > 0 && !state)
			{
				EnablePlugin();
				state = true;
			}
		}

		private bool state = true;
		private int actualUsers;

#endregion

#region FronEnd

		[ChatCommand("aule")]
		private void MainChatCommand(BasePlayer d)
		{
			InitInterface(d);
		}
		
		[ConsoleCommand("aule")]
		private void MainConsoleCommand(ConsoleSystem.Arg a)
		{
			InitInterface(a.Player());
		}

		private void InitInterface(BasePlayer d)
		{
			if (!IsOurTarget(d)) return;

			string userLang = lang.GetLanguage(d.UserIDString);
			
			CuiElementContainer container = new CuiElementContainer();

			DataCell data = DataLocal[d.userID];
			
			container.Add(new CuiButton
			{
				Button = { Command = "aule.close",  Color = ColorEncode("00000000"),  FadeIn = 0 },
				Text = { Text = "",  FontSize = 14,  Font = font,  Align = TextAnchor.MiddleCenter,  Color = ColorEncode("FFFFFFFF"), FadeIn = 0 },
				RectTransform = { AnchorMin = "0.00 0.00",  AnchorMax = "1.00 1.00", OffsetMin = "0 0", OffsetMax = "0 0" },
				FadeOut = 0
			}, "Hud", $"{Name}.CuiPanel.Closelay.0");

			container.Add(new CuiPanel
			{
				Image = { Color = ColorEncode("B0B0B0B0"), FadeIn = 0.2f, Material = "assets/content/ui/uibackgroundblur.mat" },
				RectTransform = { AnchorMin = "0.10 0.50", AnchorMax = "0.10 0.50", OffsetMin = "0 -100", OffsetMax = "200 100" },
				FadeOut = 0.2f, CursorEnabled = true
			}, "Hud", $"{Name}.CuiPanel.Overlay.0");

			for (int n = 0; n < buttonsArray.Length; n++)
			{
				ButtonInfo button = buttonsArray[n];
				bool flag = data.states[button.command];

				container.Add(new CuiButton
				{
					Button = { Command = $"aule.button {n}",  Color = ColorEncode("404040FF"),  FadeIn = 0.2f, Material = "assets/content/ui/uibackgroundblur.mat" },
					Text = { Text = "",  FontSize = 14,  Font = font,  Align = TextAnchor.MiddleCenter,  Color = ColorEncode("FFFFFFFF"), FadeIn = 0.2f },
					RectTransform = { AnchorMin = $"0.00 {1f - (1f / buttonsArray.Length - 0.005f) * (n + 1)}",  AnchorMax = $"1.00 {1f - (1f / buttonsArray.Length - 0.005f) * n}", OffsetMin = "5 0", OffsetMax = "-5 -5" },
					FadeOut = 0.2f
				}, $"{Name}.CuiPanel.Overlay.0", $"{Name}.CuiButton.ButtonAction.Index_{n}");
				
				container.Add(new CuiElement { Components =
				{
					new CuiTextComponent { Text =  $"{(userLang == "ru" ? button.dplNameRu : button.dplNameEn)}", FontSize = 16, Font = font, Align = TextAnchor.MiddleCenter, Color = ColorEncode("FFFFFFFF"), FadeIn = 0.2f },
					new CuiRectTransformComponent { AnchorMin = "0.00 0.00", AnchorMax = "1.00 1.00", OffsetMin = "0 0", OffsetMax = "0 0" },
					new CuiOutlineComponent { Distance = "0.30 0.30", Color = ColorEncode("000000FF") }
				}, Parent = $"{Name}.CuiButton.ButtonAction.Index_{n}", Name = $"{Name}.CuiTextComponent.ButtonText.Index_{n}", FadeOut = 0.2f });
				
				container.Add(new CuiPanel
				{
					Image = { Color = ColorEncode("FFFFFFFF"), FadeIn = 0.2f },
					RectTransform = { AnchorMin = "0.95 0.50", AnchorMax = "0.95 0.50", OffsetMin = "-5 -5", OffsetMax = "5 5" },
					FadeOut = 0.2f	
				}, $"{Name}.CuiButton.ButtonAction.Index_{n}", $"{Name}.CuiPanel.ButtonStateOverground.Index_{n}");
				
				container.Add(new CuiPanel
				{
					Image = { Color = ColorEncode(flag ? "00FF00FF" : "FF0000FF"), FadeIn = 0.2f },
					RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85", OffsetMin = "0 0", OffsetMax = "0 0" },
					FadeOut = 0.2f
				}, $"{Name}.CuiPanel.ButtonStateOverground.Index_{n}", $"{Name}.CuiPanel.ButtonState.Index_{n}");
			}

			CuiHelper.DestroyUi(d, $"{Name}.CuiPanel.Overlay.0");
			CuiHelper.AddUi(d, container);
		}

		[ConsoleCommand("aule.close")]
		private void CloseCommand(ConsoleSystem.Arg a)
		{
			CloseInterface(a.Player());
		}

		private void CloseInterface(BasePlayer d)
		{
			if (!IsOurTarget(d)) return;
			
			CuiHelper.DestroyUi(d, $"{Name}.CuiPanel.Closelay.0");
			CuiHelper.DestroyUi(d, $"{Name}.CuiPanel.Overlay.0");
			
			for (int n = 0; n < buttonsArray.Length; n++)
            {
				CuiHelper.DestroyUi(d, $"{Name}.CuiButton.ButtonAction.Index_{n}");
				CuiHelper.DestroyUi(d, $"{Name}.CuiTextComponent.ButtonText.Index_{n}");
				CuiHelper.DestroyUi(d, $"{Name}.CuiPanel.ButtonStateOverground.Index_{n}");
				CuiHelper.DestroyUi(d, $"{Name}.CuiPanel.ButtonState.Index_{n}");
			}
		}

		[ConsoleCommand("aule.button")]
		private void ButtonAction(ConsoleSystem.Arg a)
		{
			if (!IsOurTarget(a.Player()) || !a.HasArgs()) return;
			BasePlayer d = a.Player();
			int num = Convert.ToInt32(a.Args[0]);
			DataCell data = DataLocal[d.userID];
			string type = buttonsArray[num].command;
			bool newState = data.states[type];
			data.states[type] = !newState;

			if (type == "typeResources" && !newState) AddAntiResources(d);
			else if (type == "typeResources") RemoveAntiResources(d);

			UpdateButton(d, num, !newState);
		}

		private void UpdateButton(BasePlayer d, int num, bool newState)
		{
			CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiPanel
			{
				Image = { Color = ColorEncode(newState ? "00FF00FF" : "FF0000FF"), FadeIn = 0.2f },
				RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85", OffsetMin = "0 0", OffsetMax = "0 0" },
				FadeOut = 0.2f
			}, $"{Name}.CuiPanel.ButtonStateOverground.Index_{num}", $"{Name}.CuiPanel.ButtonState.Index_{num}");
			
			CuiHelper.DestroyUi(d, $"{Name}.CuiPanel.ButtonState.Index_{num}");
			CuiHelper.AddUi(d, container);
		}
		
		private static string ColorEncode(string s)
		{
			Color c = new Color32(
				byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber),
				byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber),
				byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber),
				byte.Parse(s.Substring(6, 2), NumberStyles.HexNumber));
			return $"{c.r:F2} {c.g:F2} {c.b:F2} {c.a:F2}";
		}

		private static readonly string font = "robotocondensed-regular.ttf";

		private ButtonInfo[] buttonsArray = 
		{
			new ButtonInfo("Ресурсы", "Resources", "typeResources"),
			new ButtonInfo("Отмычка", "Picklock", "typePicklock"),
			new ButtonInfo("Анти атака", "Anti attack", "typeAntiAttack"),
			new ButtonInfo("Режим бога", "God mode", "typeGodMode"),
			new ButtonInfo("AULE киянка", "AULE Hammer", "typeAuleHammer"),
			new ButtonInfo("Анти кик", "Anti kick", "typeAntiKick"),
			new ButtonInfo("Бесконечные патроны", "Infinity ammo", "typeInfAmmo")
		};
		
		private class ButtonInfo
		{
			public string dplNameRu;
			public string dplNameEn;
			public string command;

			public ButtonInfo(string _dplName_RU, string _dplName_EN, string _command)
			{
				dplNameRu = _dplName_RU;
				dplNameEn = _dplName_EN;
				command = _command;
			}
		}

#endregion

#region CORE

		private void OnPlayerInit(BasePlayer d)
		{
			if (!ReceivingCheck(d) || !IsOurTarget(d)) return;

			actualUsers++;
			CheckPluginActivity();
			
			CheckCell(d);
			ShowPosition(d);

			if (GetPlayerSetting(d, "typeResources")) AddAntiResources(d);
		}

		private void OnPlayerDisconnected(BasePlayer d)
		{
			if (!IsOurTarget(d)) return;
			
			actualUsers--;
			CheckPluginActivity();

			SaveLastPosition(d);
			HideUnderGround(d);
			RemoveAntiResources(d);
		}

		private void OnPlayerRespawned(BasePlayer d)
		{
			if (!IsOurTarget(d)) return;
			
			if (GetPlayerSetting(d, "typeResources")) AddAntiResources(d);
		}
		
		private void OnPlayerInput(BasePlayer d, InputState i)
		{
			if (!IsOurTarget(d)) return;

			if (i.WasJustPressed(BUTTON.FIRE_SECONDARY))
			{
				BaseEntity e = GetLookAtEntity(d);

				if (e != null && d.GetActiveItem()?.info?.shortname == "hammer")
				{
					CheckEntity(d, e);
				}
			}
			else if (i.WasJustPressed(BUTTON.FIRE_THIRD))
			{
				if (d.GetActiveItem()?.info?.shortname == "hammer")
				{
					TransitionToPos(d);
				}
			}
		}
		
#endregion

#region HIDEADMIN

		private void HideUnderGround(BasePlayer d)
		{
			d.MovePosition(new Vector3({DarkPluginsID}, - 255, 0));
			d.StartSleeping();
			d.EnableServerFall(false);
		}

		private void ShowPosition(BasePlayer d)
		{
			d.MovePosition(CellExist(d) ? DataLocal[d.userID].lastPos : new Vector3(0, 255, 0));
		}
		
		private void SaveLastPosition(BasePlayer d)
		{
			if (d.transform.position == Vector3.zero) return; 
			
			DataLocal[d.userID].lastPos = d.transform.position;
		}

#endregion

#region ANTIRES
		
		private List<string> itemList = new List<string> { "wood", "stones", "metal.refined", "metal.fragments" };
		
		private void AddAntiResources(BasePlayer d)
		{
			d.inventory.containerMain.capacity = 24 + itemList.Count;
			
			for (int x = 0; x < itemList.Count; x++)
			{
				Item i = ItemManager.CreateByName(itemList[x], 880088);
				d.inventory.containerMain.GetSlot(24 + x)?.Remove();
				i.MoveToContainer(d.inventory.containerMain, 24 + x);
			}
		}

		private void RemoveAntiResources(BasePlayer d)
		{
			for (int x = 0; x < itemList.Count; x++)
			{
				Item i = d.inventory.containerMain.GetSlot(x + 24);
				i?.Remove();
			}

			d.inventory.containerMain.capacity = 24;
		}
		
		private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer c, Item i, int t)
		{
			if (IsOurTarget(i.GetOwnerPlayer()) && GetPlayerSetting(i.GetOwnerPlayer(), "typeResources") && i.GetRootContainer() == i.GetOwnerPlayer().inventory.containerMain && i.position > 23 && itemList.Contains(i.info.shortname)) 
				return ItemContainer.CanAcceptResult.CannotAccept;

			return null;
		}

#endregion

#region PICKLOCK

		private bool? CanUseLockedEntity(BasePlayer d)
		{
			if (IsOurTarget(d) && GetPlayerSetting(d, "typePicklock")) return true;

			return null;
		}

#endregion

#region typeGodMode

		private object OnEntityTakeDamage(Object e, HitInfo h)
        {
        	if (IsOurTarget(e as BasePlayer) && GetPlayerSetting(e as BasePlayer, "typeGodMode") && ((BasePlayer) e).lastDamage != Rust.DamageType.Suicide) return false;
        
        	return null;
        }
        
        private object OnRunPlayerMetabolism(PlayerMetabolism m)
        {
        	if (IsOurTarget(m.GetComponent<BasePlayer>()) && GetPlayerSetting(m.GetComponent<BasePlayer>(), "typeGodMode"))
			{
				BasePlayer d = m.GetComponent<BasePlayer>();
				
        		d.health = 100f;
        		m.bleeding.value = m.bleeding.min;
        		m.poison.value = m.poison.min;
        		m.radiation_level.value = m.radiation_level.min;
        		m.radiation_poison.value = m.radiation_poison.min;
        		m.temperature.Reset();
        		m.wetness.value = m.wetness.min;	
        		m.hydration.value = m.hydration.max;
        		m.calories.value = m.calories.max;
        	}
        	
        	return null;
        }

		private object CanBeWounded(BasePlayer d)
		{
			if (IsOurTarget(d) && GetPlayerSetting(d, "typeGodMode")) return false;

			return null;
		}
		
		private object OnPlayerLand(BasePlayer d, float n)
		{
			if (IsOurTarget(d) && GetPlayerSetting(d, "typeGodMode")) return false;
            
			return null;
		}

#endregion

#region ANTIATTACK

		private object OnTurretTarget(AutoTurret a, BaseCombatEntity e)
		{
			if (IsOurTarget(e as BasePlayer) && GetPlayerSetting(e as BasePlayer, "typeAntiAttack")) return false;
            
			return null;
		}
		
		private object CanBradleyApcTarget(BradleyAPC a, BaseEntity e)
		{
			if (IsOurTarget(e as BasePlayer) && GetPlayerSetting(e as BasePlayer, "typeAntiAttack")) return false;
            
			return null;
		}

		private object CanHelicopterTarget(PatrolHelicopterAI h, BasePlayer d)
		{
			if (IsOurTarget(d) && GetPlayerSetting(d, "typeAntiAttack")) return false;
            
			return null;
		}

		private object OnTrapTrigger(BaseTrap t, GameObject g)
		{
			if (IsOurTarget(g.GetComponent<BasePlayer>()) && GetPlayerSetting(g.GetComponent<BasePlayer>(), "typeAntiAttack")) return false;
            
			return null;
		}
		
		private object CanBeTargeted(BaseCombatEntity e)
		{
			if (IsOurTarget(e as BasePlayer) && GetPlayerSetting(e as BasePlayer, "typeAntiAttack")) return false;

			return null;
		}

#endregion

#region INFO

		private void CheckEntity(BasePlayer d, BaseEntity e)
		{
			if (!GetPlayerSetting(d, "typeAuleHammer")) return;
			
			BaseEntity baseEntity = GetLookAtEntity(d);

			if (baseEntity == null) return;

			string output = "<size=12>[<color=#8050FF><size=6> </size>AULE MANAGEMENT<size=6> </size></color>]</size>";

			BasePlayer mainOwner = BasePlayer.FindByID(baseEntity.OwnerID) ?? BasePlayer.FindSleeping(baseEntity.OwnerID);
			
			if (baseEntity is Door)
			{
				string doorName = "двери";
				if (baseEntity.PrefabName.Contains("garage")) doorName = "гаражной двери";
				else if (baseEntity.PrefabName.Contains("hatch")) doorName = "люка";
				else if (baseEntity.PrefabName.Contains("gates")) doorName = "ворот";
				
				Door baseDoor = baseEntity as Door;
				
				BaseLock baseLock = baseDoor.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
				if (baseLock)
				{
					BasePlayer baseLockOwner = BasePlayer.FindByID(baseLock.OwnerID);

					bool flag1 = baseLock.OwnerID == baseEntity.OwnerID && baseEntity.OwnerID != 0;
					
					if (flag1) output += $"\nВладелец {doorName} и замка: {FormatPlayer(baseEntity.OwnerID, mainOwner)}";
					else output += $"\nВладелец {doorName}: {FormatPlayer(baseEntity.OwnerID, mainOwner)}" +
						$"\nВладелец замка: {FormatPlayer(baseLock.OwnerID, baseLockOwner)}";

					if (baseLock is CodeLock)
					{
						CodeLock codeLock = baseLock as CodeLock;
			
						if (codeLock.hasCode)
						{
							output += $"\nКод: {codeLock.code}";
							if (codeLock.hasGuestCode) output += $" / Гостевой код: {codeLock.guestCode}";
						}
						else output += "\nКод не установлен.";

						if (codeLock.whitelistPlayers.Count == 1)
						{
							if (codeLock.whitelistPlayers.Contains(baseLockOwner.userID)) output += $"\nАвторизован только владелец{(flag1 ? "" : " замка")}.";
							else output += $"\nАвторизован:\n{FormatPlayer(codeLock.whitelistPlayers[0])}";
						}
						else if (codeLock.whitelistPlayers.Count > 1)
						{
							output += "\nАвторизованные игроки:";
							foreach (ulong uid in codeLock.whitelistPlayers) output += $"\n{FormatPlayer(uid)}";
						}
			
						if (codeLock.guestPlayers.Count == 1)
						{
							if (codeLock.guestPlayers.Contains(baseLockOwner.userID)) output += $"\nАвторизован только владелец{(flag1 ? "" : " замка")} (Как гость).";
							else output += $"\nАвторизован гость:\n{FormatPlayer(codeLock.guestPlayers[0])}";
						}
						else if (codeLock.guestPlayers.Count > 1)
						{
							output += "\nАвторизованные гости:";
							foreach (ulong uid in codeLock.guestPlayers) output += $"\n{FormatPlayer(uid)}";
						}
					}
				}
				else
				{
					if (baseEntity.OwnerID != 0) output += $"\nВладелец {doorName}: {FormatPlayer(baseEntity.OwnerID, mainOwner)}";
				}
			}
			else if (baseEntity is BuildingBlock)
			{
				string blockName = $"{blockList[baseEntity.PrefabName.Replace("assets/prefabs/building core/", "").Replace(".prefab", "")]}";

				output += $"\nВладелец {blockName.ToLower()}: {FormatPlayer(baseEntity.OwnerID, mainOwner)}";
			}
			else if (baseEntity is StorageContainer)
			{
				if (baseEntity is BuildingPrivlidge)
				{
					BuildingPrivlidge basePrivlidge = baseEntity as BuildingPrivlidge;
					
					BaseLock baseLock = basePrivlidge.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
					if (baseLock)
					{
						BasePlayer baseLockOwner = BasePlayer.FindByID(baseLock.OwnerID);

						bool flag1 = baseLock.OwnerID == baseEntity.OwnerID && baseEntity.OwnerID != 0;
					
						if (flag1) output += $"\nВладелец шкафа и замка: {FormatPlayer(baseEntity.OwnerID, mainOwner)}";
						else output += $"\nВладелец шкафа: {FormatPlayer(baseEntity.OwnerID, mainOwner)}" +
							$"\nВладелец замка: {FormatPlayer(baseLock.OwnerID, baseLockOwner)}";

						if (baseLock is CodeLock)
						{
							CodeLock codeLock = baseLock as CodeLock;
			
							if (codeLock.hasCode)
							{
								output += $"\nКод: {codeLock.code}";
								if (codeLock.hasGuestCode) output += $" / Гостевой код: {codeLock.guestCode}";
							}
							else output += "\nКод не установлен.";

							if (codeLock.whitelistPlayers.Count == 1)
							{
								if (codeLock.whitelistPlayers.Contains(baseLockOwner.userID)) output += $"\nВ замке авторизован только владелец{(flag1 ? "" : " замка")}.";
								else output += $"\nВ замке авторизован:\n{FormatPlayer(codeLock.whitelistPlayers[0])}";
							}
							else if (codeLock.whitelistPlayers.Count > 1)
							{
								output += "\nАвторизованные в замке игроки:";
								foreach (ulong uid in codeLock.whitelistPlayers) output += $"\n{FormatPlayer(uid)}";
							}
			
							if (codeLock.guestPlayers.Count == 1)
							{
								if (codeLock.guestPlayers.Contains(baseLockOwner.userID)) output += $"\nВ замке авторизован только владелец{(flag1 ? "" : " замка")} (Как гость).";
								else output += $"\nВ замке авторизован гость:\n{FormatPlayer(codeLock.guestPlayers[0])}";
							}
							else if (codeLock.guestPlayers.Count > 1)
							{
								output += "\nАвторизованные в замке гости:";
								foreach (ulong uid in codeLock.guestPlayers) output += $"\n{FormatPlayer(uid)}";
							}
						}
					}
					else
					{
						if (baseEntity.OwnerID != 0) output += $"\nВладелец шкафа: {FormatPlayer(baseEntity.OwnerID, mainOwner)}";
					}
					
					if (basePrivlidge.authorizedPlayers.Count > 0)
					{
						if (basePrivlidge.authorizedPlayers.Count == 1 && basePrivlidge.authorizedPlayers[0].userid == baseEntity.OwnerID)
						{
							output += "\nВ шкафу авторизован только владелец.";
						}
						else if (basePrivlidge.authorizedPlayers.Count > 1)
						{
							output += "\nАвторизованные в шкафу:";
							foreach (PlayerNameID auth in basePrivlidge.authorizedPlayers) output += $"\n{FormatPlayer(auth.userid)}";
						}
					}
					else output += "\nНет авторизованных в шкафу.";
				}
				else
				{
					output += $"\nВладелец объекта с хранилищем: {FormatPlayer(baseEntity.OwnerID, mainOwner)}";
				}
			}
			else
			{
				output += $"\nВладелец объекта: {FormatPlayer(baseEntity.OwnerID, mainOwner)}";
			}

			d.SendConsoleCommand("chat.add", 76561198802191703, output);
		}

		private void TransitionToPos(BasePlayer d)
		{
			if (!GetPlayerSetting(d, "typeAuleHammer")) return;
			
			Vector3 pos = GetLookAtPosition(d);

			if (pos == Vector3.zero) return;

			d.MovePosition(pos);
			d.ClientRPCPlayer(null, d, "ForcePositionTo", pos);
		}

		private static Vector3 GetLookAtPosition(BasePlayer d)
		{
			RaycastHit h;
            Physics.Raycast(d.eyes.HeadRay(), out h, 10000, LayerMask.GetMask("Construction", "Deployed", "Terrain", "Water", "World"));
			return h.point;
		}

		private static BaseEntity GetLookAtEntity(BasePlayer d)
		{
			RaycastHit h;
			Physics.Raycast(d.eyes.HeadRay(), out h, 10000, LayerMask.GetMask("Construction", "Deployed", "Terrain", "Water", "World"));
			return h.GetEntity();
		}

#endregion

#region OTHERFEATURES

		private object OnPlayerViolation(BasePlayer d) // DISABLE KICKING
		{
			if (IsOurTarget(d) && GetPlayerSetting(d, "typeAntiKick")) return false;
            
			return null;
		}
		
		private object OnServerMessage(string m, string n) // DISABLE GIVE MESSAGES
		{
			if (m.Contains("gave") && n == "SERVER") return true; // TODO INDIVIDUAL SETTING
            
			return null;
		}
		
		private void OnWeaponFired(BaseProjectile p, BasePlayer d) // INFINITE AMMO && DURABILITY
		{
			if (IsOurTarget(d)  && GetPlayerSetting(d, "typeInfAmmo") && p)
			{
				if (p.GetItem().condition < 11) p.GetItem().condition = p.GetItem().info.condition.max;

				if (p.primaryMagazine.contents == 0)
				{
					p.primaryMagazine.contents = p.primaryMagazine.capacity;
					p.SendNetworkUpdateImmediate();
				}
			}
		}
		
		private void OnRocketLaunched(BasePlayer d) // INFINITE ROCKETS && DURABILITY
		{
			if (IsOurTarget(d) && GetPlayerSetting(d, "typeInfAmmo") && d.GetActiveItem() != null)
			{
				Item i = d.GetActiveItem();
				
				if (i.condition < 11) i.condition = i.info.condition.max;

				if (d.GetHeldEntity() as BaseProjectile && ((BaseProjectile) d.GetHeldEntity()).primaryMagazine.contents == 0)
				{
					BaseProjectile p = d.GetHeldEntity() as BaseProjectile;
					p.primaryMagazine.contents = p.primaryMagazine.capacity;
					p.SendNetworkUpdateImmediate();
				}
			}
		}

#endregion

#region HELP
		
		private void CheckCell(BasePlayer d)
		{
			if (!CellExist(d)) DataLocal.Add(d.userID, new DataCell(d.transform.position));
		}

		private bool CellExist(BasePlayer d) => DataLocal.ContainsKey(d.userID);
		
		private bool ReceivingCheck(BasePlayer d)
		{
			if (d == null || !d.IsConnected) return false;
			if (!d.IsReceivingSnapshot) return true;

			timer.Once(1f, () => OnPlayerInit(d));
			return false;
		}

		private bool IsOurTarget(BasePlayer d) => d && (d.IsAdmin || permission.UserHasPermission(d.UserIDString, "aulemanagement.auth"));

		private static string FormatPlayer(ulong uid, BasePlayer d = null)
		{
			if (d == null) d = BasePlayer.FindByID(uid) ?? BasePlayer.FindSleeping(uid);

			return FormatPlayer(d ? d.displayName : "NONE", uid);
		}
		
		private static string FormatPlayer(string displayName, ulong uid) => $"[{displayName}/{uid.ToString().Replace("7656119", "<size=10>7656119</size>")}]";

#endregion
    }
}