using Oxide.Core;
using Rust.Modular;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("Vehicle Fuel Manager", "bsdinis", "0.3.2")]
	class VehicleFuelManager : RustPlugin
	{
		void Init()
		{
			Unsubscribe(nameof(OnEntitySpawned));

			try
			{
				config = Config.ReadObject<ConfigData>();
				if (config == null)
				{
					throw new Exception();
				}
				else
				{
					SaveConfig();
				}
			}
			catch
			{
				PrintError("CONFIG FILE IS INVALID.\nCheck config file and reload VehicleFuelManager.");
				Interface.Oxide.UnloadPlugin(Name);
				return;
			}
		}

		void OnServerInitialized()
		{
			timer.Once(
				0.25f,
				() =>
				{
					foreach (BaseNetworkable entity in BaseNetworkable.serverEntities)
					{
						BaseVehicle vehicle = entity as BaseVehicle;
						if (vehicle != null)
						{
							FuelSettings fuelSettings;
							if (!config.Vehicles.TryGetValue(vehicle.ShortPrefabName, out fuelSettings))
							{
								continue;
							}
							SetConsumption(vehicle, fuelSettings);
							if (fuelSettings.LockFuelContainer)
							{
								AddFuel(vehicle.GetFuelSystem() as EntityFuelSystem, fuelSettings);
							}
							ModularCar car = vehicle as ModularCar;
							if (car != null)
							{
								for (int i = 0; i < car.AttachedModuleEntities.Count; i++)
								{
									VehicleModuleEngine moduleEngine = car.AttachedModuleEntities[i] as VehicleModuleEngine;
									if (moduleEngine == null)
									{
										continue;
									}
									EngineModuleSettings moduleSettings;
									if (!config.EngineModules.TryGetValue(moduleEngine.ShortPrefabName, out moduleSettings))
									{
										continue;
									}
									ModuleEngines(moduleEngine, moduleSettings);
									if (moduleSettings.PartsTier < 1 || !moduleSettings.LockContainer)
									{
										continue;
									}
									EngineStorage engineStorage = moduleEngine.GetContainer() as EngineStorage;
									if (engineStorage == null)
									{
										continue;
									}
									engineStorage.AdminAddParts((moduleSettings.PartsTier > 3 ? 3 : moduleSettings.PartsTier));
								}
							}
							continue;
						}
						HotAirBalloon hab = entity as HotAirBalloon;
						if (hab != null)
						{
							FuelSettings fuelSettings;
							if (!config.Vehicles.TryGetValue(hab.ShortPrefabName, out fuelSettings))
							{
								continue;
							}
							if (fuelSettings.FuelPerSecond != null)
							{
								hab.fuelPerSec = fuelSettings.FuelPerSecond.Value;
							}
							if (fuelSettings.LockFuelContainer)
							{
								AddFuel(hab.fuelSystem, fuelSettings);
							}
						}
					}

					Subscribe(nameof(OnEntitySpawned));
				}
			);
		}

		void RemoveFuelFromLockedContainer(EntityFuelSystem fuelSystem)
		{
			if (fuelSystem == null)
			{
				return;
			}
			StorageContainer fuelContainer = fuelSystem.fuelStorageInstance.Get(fuelSystem.isServer);
			if (fuelContainer == null || !fuelContainer.IsLocked())
			{
				return;
			}
			fuelContainer.SetFlag(BaseEntity.Flags.Locked, false);
			ItemContainer container = fuelContainer.inventory;
			if (container == null)
			{
				return;
			}
			Item fuelItem = container.GetSlot(0);
			if (fuelItem == null)
			{
				return;
			}
			fuelItem.Remove();
		}

		void Unload()
		{
			Unsubscribe(nameof(OnEntitySpawned));

			foreach (BaseNetworkable entity in BaseNetworkable.serverEntities)
			{
				BaseVehicle vehicle = entity as BaseVehicle;
				if (vehicle != null)
				{
					RemoveFuelFromLockedContainer(vehicle.GetFuelSystem() as EntityFuelSystem);
					PlayerHelicopter heli = vehicle as PlayerHelicopter;
					if (heli != null)
					{
						heli.fuelPerSec = 0.5f;
						continue;
					}
					MotorRowboat boat = vehicle as MotorRowboat;
					if (boat != null)
					{
						if (boat.ShortPrefabName == "rowboat")
						{
							boat.fuelPerSec = 0.1f;
						}
						else if (boat.ShortPrefabName == "rhib")
						{
							boat.fuelPerSec = 0.25f;
						}
						continue;
					}
					BaseSubmarine sub = vehicle as BaseSubmarine;
					if (sub != null)
					{
						if (sub.ShortPrefabName == "submarinesolo.entity")
						{
							sub.idleFuelPerSec = 0.025f;
							sub.maxFuelPerSec = 0.13f;
						}
						else if (sub.ShortPrefabName == "submarineduo.entity")
						{
							sub.idleFuelPerSec = 0.03f;
							sub.maxFuelPerSec = 0.15f;
						}
						continue;
					}
					Snowmobile snow = vehicle as Snowmobile;
					if (snow != null)
					{
						snow.idleFuelPerSec = 0.03f;
						snow.maxFuelPerSec = 0.15f;
						continue;
					}
					/*Bike bike = vehicle as Bike;
					if (bike != null)
					{
						bike.idleFuelPerSec = 0.03f;
						bike.maxFuelPerSec = 0.15f;
						continue;
					}*/
					ModularCar car = vehicle as ModularCar;
					if (car != null)
					{
						for (int i = 0; i < car.AttachedModuleEntities.Count; i++)
						{
							VehicleModuleEngine moduleEngine = car.AttachedModuleEntities[i] as VehicleModuleEngine;
							if (moduleEngine == null)
							{
								continue;
							}
							VehicleModuleEngine.Engine engine = moduleEngine.engine;
							if (engine != null)
							{
								if (moduleEngine.ShortPrefabName == "1module_cockpit_with_engine")
								{
									engine.idleFuelPerSec = 0.025f;
									engine.maxFuelPerSec = 0.08f;
								}
								else if (moduleEngine.ShortPrefabName == "1module_engine")
								{
									engine.idleFuelPerSec = 0.04f;
									engine.maxFuelPerSec = 0.11f;
								}
							}
							EngineStorage engineStorage = moduleEngine.GetContainer() as EngineStorage;
							if (engineStorage == null || !engineStorage.inventory.IsLocked())
							{
								continue;
							}
							engineStorage.inventory.Clear();
							engineStorage.inventory.SetLocked(false);
						}
						continue;
					}
					MagnetCrane crane = vehicle as MagnetCrane;
					if (crane != null)
					{
						crane.idleFuelPerSec = 0.06668f;
						crane.maxFuelPerSec = 0.3334f;
						continue;
					}
					TrainEngine train = vehicle as TrainEngine;
					if (train != null)
					{
						train.idleFuelPerSec = 0.025f;
						train.maxFuelPerSec = 0.075f;
						continue;
					}
					continue;
				}
				HotAirBalloon hab = entity as HotAirBalloon;
				if (hab != null)
				{
					RemoveFuelFromLockedContainer(hab.fuelSystem);
					hab.fuelPerSec = 0.25f;
				}
			}
		}

		protected override void SaveConfig() => Config.WriteObject(config, true);

		protected override void LoadDefaultConfig()
		{
			config = new ConfigData
			{
				Vehicles = new Dictionary<string, FuelSettings>
				{
					{ "attackhelicopter.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = 0.5f, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "minicopter.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = 0.5f, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "scraptransporthelicopter", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = 0.5f, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "rowboat", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = 0.1f, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "rhib", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = 0.25f, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "submarinesolo.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.025f, MaxFuelPerSecond = 0.13f } },
					{ "submarineduo.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.03f, MaxFuelPerSecond = 0.15f } },
					{ "hotairballoon", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = 0.25f, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "snowmobile", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.03f, MaxFuelPerSecond = 0.15f } },
					{ "tomahasnowmobile", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.03f, MaxFuelPerSecond = 0.15f } },
					{ "4module_car_spawned.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "3module_car_spawned.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "2module_car_spawned.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = null, MaxFuelPerSecond = null } },
					{ "motorbike", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.03f, MaxFuelPerSecond = 0.15f } },
					{ "motorbike_sidecar", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.03f, MaxFuelPerSecond = 0.15f } },
					{ "magnetcrane.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.06668f, MaxFuelPerSecond = 0.3334f } },
					{ "workcart.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.025f, MaxFuelPerSecond = 0.075f } },
					{ "workcart_aboveground.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.025f, MaxFuelPerSecond = 0.075f } },
					{ "workcart_aboveground2.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.025f, MaxFuelPerSecond = 0.075f } },
					{ "locomotive.entity", new FuelSettings { StartingFuel = 0, LockFuelContainer = false, FuelPerSecond = null, IdleFuelPerSecond = 0.035f, MaxFuelPerSecond = 0.1f } }
				},
				ModularCarsSpawnFullHealth = false,
				EngineModules = new Dictionary<string, EngineModuleSettings>
				{
					{ "1module_cockpit_with_engine", new EngineModuleSettings { PartsTier = 0, LockContainer = false, IdleFuelPerSecond = 0.025f, MaxFuelPerSecond = 0.08f } },
					{ "1module_engine", new EngineModuleSettings { PartsTier = 0, LockContainer = false, IdleFuelPerSecond = 0.04f, MaxFuelPerSecond = 0.11f } }
				}
			};
		}

		ConfigData config;
		class ConfigData
		{
			public Dictionary<string, FuelSettings> Vehicles;
			public bool ModularCarsSpawnFullHealth;
			public Dictionary<string, EngineModuleSettings> EngineModules;
		}

		class FuelSettings
		{
			public int StartingFuel;
			public bool LockFuelContainer;
			public float? FuelPerSecond;
			public float? IdleFuelPerSecond;
			public float? MaxFuelPerSecond;
		}

		class EngineModuleSettings
		{
			public int PartsTier;
			public bool LockContainer;
			public float IdleFuelPerSecond;
			public float MaxFuelPerSecond;
		}

		void SetConsumption(BaseVehicle vehicle, FuelSettings fuelSettings)
		{
			if (fuelSettings.FuelPerSecond != null)
			{
				PlayerHelicopter heli = vehicle as PlayerHelicopter;
				if (heli != null)
				{
					heli.fuelPerSec = fuelSettings.FuelPerSecond.Value;
					return;
				}
				MotorRowboat boat = vehicle as MotorRowboat;
				if (boat != null)
				{
					boat.fuelPerSec = fuelSettings.FuelPerSecond.Value;
					return;
				}
			}
			else if (fuelSettings.IdleFuelPerSecond != null && fuelSettings.MaxFuelPerSecond != null)
			{
				BaseSubmarine sub = vehicle as BaseSubmarine;
				if (sub != null)
				{
					sub.idleFuelPerSec = fuelSettings.IdleFuelPerSecond.Value;
					sub.maxFuelPerSec = fuelSettings.MaxFuelPerSecond.Value;
					return;
				}
				Snowmobile snow = vehicle as Snowmobile;
				if (snow != null)
				{
					snow.idleFuelPerSec = fuelSettings.IdleFuelPerSecond.Value;
					snow.maxFuelPerSec = fuelSettings.MaxFuelPerSecond.Value;
					return;
				}
				/*Bike bike = vehicle as Bike;
				if (bike != null)
				{
					bike.idleFuelPerSec = fuelSettings.IdleFuelPerSecond.Value;
					bike.maxFuelPerSec = fuelSettings.MaxFuelPerSecond.Value;
					return;
				}*/
				MagnetCrane crane = vehicle as MagnetCrane;
				if (crane != null)
				{
					crane.idleFuelPerSec = fuelSettings.IdleFuelPerSecond.Value;
					crane.maxFuelPerSec = fuelSettings.MaxFuelPerSecond.Value;
					return;
				}
				TrainEngine train = vehicle as TrainEngine;
				if (train != null)
				{
					train.idleFuelPerSec = fuelSettings.IdleFuelPerSecond.Value;
					train.maxFuelPerSec = fuelSettings.MaxFuelPerSecond.Value;
				}
			}
		}

		void OnEntitySpawned(BaseVehicle vehicle)
		{
			FuelSettings fuelSettings;
			if (!config.Vehicles.TryGetValue(vehicle.ShortPrefabName, out fuelSettings))
			{
				return;
			}
			SetConsumption(vehicle, fuelSettings);
			NextTick(
				() =>
				{
					if (vehicle == null)
					{
						return;
					}
					AddFuel(vehicle.GetFuelSystem() as EntityFuelSystem, fuelSettings);
					if (!config.ModularCarsSpawnFullHealth)
					{
						return;
					}
					ModularCar car = vehicle as ModularCar;
					if (car == null)
					{
						return;
					}
					for (int i = 0; i < car.AttachedModuleEntities.Count; i++)
					{
						car.AttachedModuleEntities[i].health = car.AttachedModuleEntities[i].MaxHealth();
					}
				}
			);
		}

		void OnEntitySpawned(HotAirBalloon hab)
		{
			FuelSettings fuelSettings;
			if (!config.Vehicles.TryGetValue(hab.ShortPrefabName, out fuelSettings))
			{
				return;
			}
			if (fuelSettings.FuelPerSecond != null)
			{
				hab.fuelPerSec = fuelSettings.FuelPerSecond.Value;
			}
			NextTick(
				() =>
				{
					if (hab != null)
					{
						AddFuel(hab.fuelSystem, fuelSettings);
					}
				}
			);
		}

		void AddFuel(EntityFuelSystem fuelSystem, FuelSettings fuelSettings)
		{
			if (fuelSystem == null)
			{
				return;
			}
			StorageContainer fuelContainer = fuelSystem.fuelStorageInstance.Get(fuelSystem.isServer);
			if (fuelContainer == null)
			{
				return;
			}
			if (fuelSettings.LockFuelContainer)
			{
				fuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
			}
			if (fuelSettings.StartingFuel < 1)
			{
				return;
			}
			ItemContainer container = fuelContainer.inventory;
			if (container == null)
			{
				return;
			}
			Item fuel = ItemManager.Create(fuelContainer.allowedItem, fuelSettings.StartingFuel);
			if (fuel.MoveToContainer(container))
			{
				return;
			}
			fuel.Remove();
		}

		void ModuleEngines(VehicleModuleEngine moduleEngine, EngineModuleSettings moduleSettings)
		{
			if (moduleEngine == null || moduleEngine.Vehicle == null)
			{
				return;
			}
			VehicleModuleEngine.Engine engine = moduleEngine.engine;
			if (engine != null)
			{
				engine.idleFuelPerSec = moduleSettings.IdleFuelPerSecond;
				engine.maxFuelPerSec = moduleSettings.MaxFuelPerSecond;
			}
			if (!moduleSettings.LockContainer)
			{
				return;
			}
			EngineStorage engineStorage = moduleEngine.GetContainer() as EngineStorage;
			if (engineStorage == null)
			{
				return;
			}
			engineStorage.internalDamageMultiplier = 0.0f;
			engineStorage.inventory.SetLocked(true);
		}

		void OnEntitySpawned(VehicleModuleEngine moduleEngine)
		{
			NextTick(
				() =>
				{
					if (moduleEngine == null)
					{
						return;
					}
					EngineModuleSettings moduleSettings;
					if (!config.EngineModules.TryGetValue(moduleEngine.ShortPrefabName, out moduleSettings))
					{
						return;
					}
					ModuleEngines(moduleEngine, moduleSettings);
					EngineStorage engineStorage = moduleEngine.GetContainer() as EngineStorage;
					if (engineStorage == null || moduleSettings.PartsTier < 1)
					{
						return;
					}
					engineStorage.AdminAddParts((moduleSettings.PartsTier > 3 ? 3 : moduleSettings.PartsTier));
				}
			);
		}

		object OnVehicleModuleMove(VehicleModuleEngine moduleEngine)
		{
			EngineStorage engineStorage = moduleEngine.GetContainer() as EngineStorage;
			if (engineStorage == null || !engineStorage.inventory.IsLocked())
			{
				return null;
			}
			return true;
		}

		object OnItemLock(Item item)
		{
			if (item.parent == null)
			{
				return null;
			}
			ModularCar car = item.parent.entityOwner as ModularCar;
			if (car == null)
			{
				return null;
			}
			VehicleModuleEngine moduleEngine = car.GetModuleForItem(item) as VehicleModuleEngine;
			if (moduleEngine == null)
			{
				return null;
			}
			EngineStorage engineStorage = moduleEngine.GetContainer() as EngineStorage;
			if (engineStorage == null || !engineStorage.inventory.IsLocked())
			{
				return null;
			}
			return false;
		}

		void OnEntityKill(VehicleModuleEngine moduleEngine)
		{
			EngineStorage engineStorage = moduleEngine.GetContainer() as EngineStorage;
			if (engineStorage == null || !engineStorage.inventory.IsLocked())
			{
				return;
			}
			engineStorage.Kill();
		}
	}
}