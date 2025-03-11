// Reference: 0Harmony

/**
 * OxidationSmelting - Smelting controller
 * Copyright (C) 2021-2023 kasvoton [kasvoton@stinkfist.org]
 *
 * All Rights Reserved.
 * DO NOT DISTRIBUTE THIS SOFTWARE.
 *
 * You should have received a copy of the EULA along with this software.
 * If not, see <https://oxidation.stinkfist.org/license/eula.txt>.
 *
 *
 *                #################################
 *               ###  I AM AVAILABLE FOR HIRING  ###
 *                #################################
 *
 * IF YOU WANT A CUSTOM PLUGIN FOR YOUR SERVER GET INTO CONTACT WITH ME SO
 * WE CAN DISCUSS YOUR NEED IN DETAIL. I CAN BUILD PLUGINS FROM SCRATCH OR
 * MODIFY EXISTING ONES DEPENDING ON THE COMPLEXITY.
 *
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Facepunch.Extend;
using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core;

#pragma warning disable IDE0058

namespace Oxide.Plugins
{

	[Info("SmeltingController", "codefling.com/kasvoton", "1.5.15")]

	public class SmeltingController : RustPlugin
	{
		private Harmony _harmony;

		// --- EVENTS ------------------------------------------------------------------------------------------------------------
		internal void Loaded()
		{
			Harmony.DEBUG = ConVar.Global.developer > 0;
			_harmony = new Harmony($"{Name}_{Version.Major}_{Version.Minor}_{Version.Patch}");
		}

		internal void OnServerInitialized()
		{
			try
			{
				SaveConfig();
				ApplySettings();

				_ = _harmony.Patch(
					AccessTools.Method(typeof(ItemModCookable), "CycleCooking"),
					transpiler: new HarmonyMethod(typeof(CycleCookingPatch), "Transpiler"));

				_ = _harmony.Patch(
					AccessTools.Method(typeof(BaseOven), "ConsumeFuel"),
					transpiler: new HarmonyMethod(typeof(ConsumeFuelPatch), "Transpiler"));
			}
			catch
			{
				NextFrame(() =>
				{
					WriteError($"Issues were detected while parsing the config file, exiting..");
					Interface.Oxide.UnloadPlugin(Name);
				});
			}
		}

		internal void Unload()
		{
			Harmony.DEBUG = false;
			_harmony.UnpatchAll($"{Name}_{Version.Major}_{Version.Minor}_{Version.Patch}");
			RevertSettings();
		}

		// --- HARMONY -----------------------------------------------------------------------------------------------------------
		public class CycleCookingPatch
		{
			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				List<CodeInstruction> il = new(instructions);

				int index = il.FindIndex(i => i.opcode == OpCodes.Stloc_2);

				if (index > 0)
				{
					il.InsertRange(index + 1, new List<CodeInstruction>()
					{
						new(OpCodes.Ldloc_2),
						new (OpCodes.Call, AccessTools.Method(typeof(SmeltingController), nameof(GetMultiplication))),
						new (OpCodes.Ldloc_1),
						new (OpCodes.Mul),
						new (OpCodes.Stloc_1)
					});
				}

				return il;
			}
		}

		public class ConsumeFuelPatch
		{
			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				List<CodeInstruction> il = new(instructions);

				MethodInfo getFuelRate = AccessTools.Method(typeof(BaseOven), nameof(BaseOven.GetFuelRate));
				MethodInfo getCharcoalRate = AccessTools.Method(typeof(BaseOven), nameof(BaseOven.GetCharcoalRate));

				foreach (CodeInstruction instruction in il)
				{
					MethodInfo methodInfo = instruction.operand as MethodInfo;

					if (methodInfo == getFuelRate)
					{
						yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SmeltingController), nameof(GetMultiplication)));
					}
					else if (methodInfo == getCharcoalRate)
					{
						yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SmeltingController), nameof(GetCharcoalRate)));
					}
					else
					{
						yield return instruction;
					}
				}
			}
		}

		// --- UTILITY -----------------------------------------------------------------------------------------------------------
		protected static int GetMultiplication(BaseOven instance)
		{
			try
			{
				if (!Settings.Ovens.TryGetValue(instance.ShortPrefabName, out PluginSettings.OvenSettings oven))
				{
					return 1;
				}
				return oven.Multiplier;
			}
			catch (Exception)
			{
				return 1;
			}
		}

		protected static int GetCharcoalRate(BaseOven instance)
		{
			try
			{
				if (!Settings.Ovens.TryGetValue(instance.ShortPrefabName, out PluginSettings.OvenSettings oven))
				{
					return 1;
				}
				return oven.Charcoal;
			}
			catch (Exception)
			{
				return 1;
			}
		}

		// --- CONFIGURATION -----------------------------------------------------------------------------------------------------
		private static PluginSettings Settings;

		protected override void LoadConfig()
		{
			try
			{
				base.LoadConfig();
				Settings = Config.ReadObject<PluginSettings>();
			}
			catch (JsonReaderException)
			{
				// do nothing
			}
			catch (Exception)
			{
				LoadDefaultConfig();
				SaveConfig();
			}
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(Settings, true);
		}

		protected override void LoadDefaultConfig()
		{
			WriteWarning($"Created a new default config file");

			Settings = new PluginSettings
			{
				NoBurntMeat = false
			};

			foreach (string Oven in Ovens)
			{
				Settings.Ovens.Add(Oven, new PluginSettings.OvenSettings());
			}

			foreach (ItemDefinition Item in ItemManager.itemList)
			{
				foreach (ItemMod mod in Item.itemMods)
				{
					if (Item.shortname.Contains(".burned") || Item.shortname.Contains(".cooked"))
					{
						continue;
					}

					ItemModCookable Cookable = mod as ItemModCookable;

					if (Cookable != null)
					{
						Settings.Products.Add(Item.shortname, value: new PluginSettings.ProductSettings
						{
							Amount = Cookable.amountOfBecome,
							CookTime = Cookable.cookTime
						});
					}

					ItemModBurnable Burnable = mod as ItemModBurnable;

					if (Burnable != null && Burnable.byproductItem != null)
					{
						Settings.Fuel.Add(Item.shortname, value: new PluginSettings.FuelSettings
						{
							FuelAmount = Burnable.fuelAmount,
							ByProduct = Burnable.byproductItem.shortname,
							ByProductAmount = Burnable.byproductAmount,
							ByProductChance = 1 - Burnable.byproductChance,
						});
					}
				}
			}
		}

		protected void ApplySettings()
		{
			foreach (ItemDefinition Item in ItemManager.itemList)
			{
				foreach (ItemMod mod in Item.itemMods)
				{
					ItemModCookable cookable = mod as ItemModCookable;
					if (cookable != null)
					{
						PluginSettings.ProductSettings info;
						if (Settings.Products.TryGetValue(Item.shortname, out info))
						{
							if (!Backup.Products.ContainsKey(Item.shortname))
							{
								Backup.Products.Add(Item.shortname, value: new PluginSettings.ProductSettings
								{
									Amount = cookable.amountOfBecome,
									CookTime = cookable.cookTime,
									LowTemp = cookable.lowTemp,
									HighTemp = cookable.highTemp
								});
							}

							if (cookable.amountOfBecome != info.Amount)
							{
								WriteInfo($"Changed '{Item.shortname}' amountOfBecome from {cookable.amountOfBecome} to {info.Amount}");
								cookable.amountOfBecome = info.Amount;
							}

							if (cookable.cookTime != info.CookTime)
							{
								WriteInfo($"Changed '{Item.shortname}' cookTime from {cookable.cookTime} to {info.CookTime}");
								cookable.cookTime = info.CookTime;
							}
						}

						if (Settings.NoBurntMeat && Item.shortname.Contains(".cooked"))
						{
							if (!Backup.Products.ContainsKey(Item.shortname))
							{
								Backup.Products.Add(Item.shortname, value: new PluginSettings.ProductSettings
								{
									Amount = cookable.amountOfBecome,
									CookTime = cookable.cookTime,
									LowTemp = cookable.lowTemp,
									HighTemp = cookable.highTemp
								});
							}

							cookable.highTemp = -1;
							cookable.lowTemp = -1;
						}
					}

					ItemModBurnable Burnable = mod as ItemModBurnable;
					if (Burnable != null && Burnable.byproductItem != null)
					{
						PluginSettings.FuelSettings info;
						if (Settings.Fuel.TryGetValue(Item.shortname, out info))
						{
							Backup.Fuel.Add(Item.shortname, value: new PluginSettings.FuelSettings
							{
								FuelAmount = Burnable.fuelAmount,
								ByProductChance = Burnable.byproductChance,
								ByProductAmount = Burnable.byproductAmount
							});

							Burnable.fuelAmount = info.FuelAmount;
							Burnable.byproductAmount = info.ByProductAmount;
							Burnable.byproductChance = 1 - info.ByProductChance;
						}
					}
				}
			};
		}

		protected void RevertSettings()
		{
			foreach (KeyValuePair<string, PluginSettings.ProductSettings> kvp in Backup.Products)
			{
				if (!ItemManager.FindItemDefinition(kvp.Key).TryGetComponent(out ItemModCookable Cookable))
				{
					continue;
				}

				Cookable.amountOfBecome = kvp.Value.Amount;
				Cookable.cookTime = kvp.Value.CookTime;
				Cookable.highTemp = kvp.Value.HighTemp;
				Cookable.lowTemp = kvp.Value.LowTemp;
			}

			foreach (KeyValuePair<string, PluginSettings.FuelSettings> kvp in Backup.Fuel)
			{
				if (!ItemManager.FindItemDefinition(kvp.Key).TryGetComponent(out ItemModBurnable Burnable))
				{
					continue;
				}

				Burnable.fuelAmount = kvp.Value.FuelAmount;
				Burnable.byproductAmount = kvp.Value.ByProductAmount;
				Burnable.byproductChance = kvp.Value.ByProductChance;
			}
		}

		internal sealed class PluginSettings
		{
			public bool NoBurntMeat
			{ get; set; }

			public Dictionary<string, OvenSettings> Ovens
			{ get; set; } = new();

			public Dictionary<string, ProductSettings> Products
			{ get; set; } = new();

			public Dictionary<string, FuelSettings> Fuel
			{ get; set; } = new();

			internal sealed class OvenSettings
			{
				private int _speed = 1;
				private int _charcoalRate = 1;

				public int Multiplier
				{
					get { return _speed; }
					set { _speed = value.Clamp(1, 100); }
				}

				public int Charcoal
				{
					get { return _charcoalRate; }
					set { _charcoalRate = value.Clamp(1, 100); }
				}
			}

			internal sealed class ProductSettings
			{
				private int _amountValue = 1;
				private float _cookTimeValue = 10f;

				public float CookTime
				{
					get { return _cookTimeValue; }
					set { _cookTimeValue = value.Clamp(1f, 60f); }
				}

				public int Amount
				{
					get { return _amountValue; }
					set { _amountValue = value.Clamp(1, int.MaxValue); }
				}

				[JsonIgnore]
				public int LowTemp
				{ get; set; } = -1;

				[JsonIgnore]
				public int HighTemp
				{ get; set; } = -1;
			}

			internal sealed class FuelSettings
			{
				private float _fuelAmountValue = 1f;
				private int _byProductAmountValue = 1;
				private float _byProductChanceValue = 1f;

				public float FuelAmount
				{
					get { return _fuelAmountValue; }
					set { _fuelAmountValue = value.Clamp(1f, float.MaxValue); }
				}

				public string ByProduct
				{
					get; set;
				}

				public int ByProductAmount
				{
					get { return _byProductAmountValue; }
					set { _byProductAmountValue = value.Clamp(1, int.MaxValue); }
				}

				public float ByProductChance
				{
					get { return _byProductChanceValue; }
					set { _byProductChanceValue = value.Clamp(0f, 1f); }
				}
			}
		}

		// --- CACHE -------------------------------------------------------------------------------------------------------------
		private readonly Cache Backup = new();

		internal sealed class Cache
		{
			public Dictionary<string, PluginSettings.ProductSettings> Products
			{ get; set; } = new();

			public Dictionary<string, PluginSettings.FuelSettings> Fuel
			{ get; set; } = new();
		}

		private static readonly string[] Ovens =
		{
			"bbq.campermodule",
			"bbq.deployed",
			"campfire",
			"carvable.pumpkin",
			"chineselantern.deployed",
			"cursedcauldron.deployed",
			"electricfurnace.deployed",
			"fireplace.deployed",
			"furnace.large",
			"furnace",
			"hobobarrel.deployed",
			"jackolantern.angry",
			"jackolantern.happy",
			"lantern.deployed",
			"legacy_furnace",
			"refinery_small_deployed",
			"skull_fire_pit",
			"tunalight.deployed",
		};

		// --- LOGGING -----------------------------------------------------------------------------------------------------------
		internal void WriteInfo(string format, params object[] args)
		{
			Puts("[INFO] " + format, args);
		}

		internal void WriteWarning(string format, params object[] args)
		{
			PrintWarning("[WARNING] " + format, args);
		}

		internal void WriteError(string format, params object[] args)
		{
			PrintError("[ERROR] " + format, args);
		}
	}
}