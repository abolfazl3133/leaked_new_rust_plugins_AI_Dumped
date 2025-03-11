using Oxide.Core.Plugins;
using Oxide.Core;
using Newtonsoft.Json;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins 
{
    [Info("Traffic Drivers", "walkinrey", "1.4.2")]
    public class TrafficDrivers : RustPlugin
    {
        public static TrafficDrivers Instance;

        [PluginReference] private Plugin SpawnModularCar, Convoy, MarkerManager, CarTurrets;

        private SpawnManager _spawner;
        private List<Vector3[]> _roadPathes = new List<Vector3[]>();

        private Dictionary<ulong, TrafficCompanion> _driverCompanions = new Dictionary<ulong, TrafficCompanion>();
        private Dictionary<ulong, TrafficDriver> _carsDrivers = new Dictionary<ulong, TrafficDriver>();
        private Dictionary<ulong, List<BaseNetworkable>> _debugMarkers = new Dictionary<ulong, List<BaseNetworkable>>();

        private Dictionary<ulong, PlayerPathRecorder> _recorders = new Dictionary<ulong, PlayerPathRecorder>();

        private TrafficCompanion GetCompanion(ulong playerNetID) 
        {
            if(_driverCompanions.ContainsKey(playerNetID))
            {
                var value = _driverCompanions[playerNetID];
                if(value == null) return null;

                return value;
            }

            return null;
        }

        private TrafficDriver GetDriver(ulong playerNetID)
        {
            if(_carsDrivers.ContainsKey(playerNetID))
            {
                var value = _carsDrivers[playerNetID];
                if(value == null) return null;

                return value;
            }

            return null;
        }

        #region Конфиг

        private Configuration _config;

        public class Configuration 
        {
            [JsonProperty("Enable path recorder?")]
            public bool enableRecording = false;

            [JsonProperty("Cooldown between points in secodns (when using path recorder)")]
            public float cooldownSecondsRecorder = 0.25f;

            [JsonProperty("Vehicle presets")]
            public Dictionary<string, CarPreset> carPresets = new Dictionary<string, CarPreset>();

            [JsonProperty("Driver presets")]
            public Dictionary<string, DriverPreset> driverPresets = new Dictionary<string, DriverPreset>();

            [JsonProperty("Limits, spawn and interaction setup")]
            public Limits limits = new Limits();

            public class Limits 
            {
                [JsonProperty("Maximum amount of cars")]
                public MinMax maxCars = new MinMax();

                [JsonProperty("Minimum road length")]
                public int minRoadLength = 100; 

                [JsonProperty("After how many seconds destroy stuck car?")]
                public float stuckDespawnTime = 60f;

                [JsonProperty("Delay between spawn next car")]
                public int spawnCooldown = 5;

                [JsonProperty("Prevent bots from attacking drivers and companions?")]
                public bool blockTrafficTarget = true;

                [JsonProperty("Despawn vehicle if it collides with next prefabs")]
                public string[] despawnPrefabs = new string[] {
                    "train_wagon",
                    "trainwagon"
                };

                [JsonProperty("Fully despawn vehicle (no detonator) if it collides with next prefabs")]
                public string[] fullDespawnPrefabs = new string[] {
                    "bradleyapc"
                };

                [JsonProperty("Destroy prefab when despawning a car?")]
                public bool destroyPrefab = false;

                [JsonProperty("Convoy plugin support")]
                public ConvoySupport convoy = new ConvoySupport();

                public class ConvoySupport
                {
                    [JsonProperty("Enable the Convoy plugin support?")]
                    public bool enableConvoySupport = false;

                    [JsonProperty("Destroy traffic vehicles at the start of the Convoy plugin event?")]
                    public bool destroyCarsOnEvent = false;

                    [JsonProperty("Destroy traffic vehicles when colliding with Convoy plugin event vehicles?")]
                    public bool destroyCarsOnHit = false;
                }
            }

            public class DriverPreset 
            {
                [JsonProperty("Display driver name")]
                public string name = "Водитель Ярик";

                [JsonProperty("Spawn as scientist npc?")]
                public bool spawnAsScientist = false;

                [JsonProperty("Bot skin (0 - random, not work if scientist)")]
                public ulong skin = 0;

                [JsonProperty("Bot health")]
                public MinMax health = new MinMax();

                [JsonProperty("Bot loot (not work if scientist)")]
                public List<LootInfo> loot = new List<LootInfo>();

                [JsonProperty("Driver will be moving with default speed (1) or will be increase max. speed for a while (2) when attacked?")]
                public int attackBehaviour = 1;

                [JsonProperty("Damage receive rate")]
                public float damageReceiveRate = 0.5f;

                [JsonProperty("Spawn bag with items instead of corpse on death?")]
                public bool spawnBagOnDeath = false;

                [JsonProperty("Clothes")]
                public List<ClothInfo> clothes = new List<ClothInfo>();

                [JsonProperty("Companion")]
                public CompanionSetup companion = new CompanionSetup();

                [JsonProperty("Lock player inventory containers (not work if scientist)")]
                public ContainerLock containerLock = new ContainerLock();

                public class ContainerLock 
                {
                    [JsonProperty("Lock player main inventory container?")]
                    public bool lockMain = false;

                    [JsonProperty("Lock player wear inventory container?")]
                    public bool lockWear = false;

                    [JsonProperty("Lock player belt inventory container?")]
                    public bool lockBelt = false;
                }

                public class CompanionSetup 
                {
                    [JsonProperty("Spawn companion for driver? (he will shoot and protect him)")]
                    public bool enableCompanion = false;

                    [JsonProperty("Display companion name")]
                    public string name = "Компаньон-защитник";

                    [JsonProperty("Prevent companion from attacking first?")]
                    public bool preventAttackFirst = true;

                    [JsonProperty("Spawn bag with items instead of corpse on death?")]
                    public bool spawnBagOnDeath = false;

                    [JsonProperty("Companion health")]
                    public MinMax health = new MinMax();

                    [JsonProperty("Clothes")]
                    public List<ClothInfo> clothes = new List<ClothInfo>();

                    [JsonProperty("Weapons")]
                    public List<ClothInfo> weapons = new List<ClothInfo>();
                    
                    [JsonProperty("Damage receive rate")]
                    public float damageReceiveRate = 0.5f;

                    [JsonProperty("Damage rate")]
                    public float damageHurtRate = 2f;
                }

                public class LootInfo : ClothInfo
                {
                    [JsonProperty("Item name", Order = -1)]
                    public string name;

                    [JsonProperty("Spawn chance", Order = 3)]
                    public float chance;

                    [JsonProperty("Item amount", Order = 4)]
                    public MinMax amount;

                    [JsonProperty("Target container (main, belt, wear)", Order = 5)]
                    public string container;
                }

                public class ClothInfo 
                {
                    [JsonProperty("Item shortname")]
                    public string shortname;

                    [JsonProperty("Item skin")]
                    public ulong skin;
                }
            }

            public class CarPreset 
            {
                [JsonProperty("Random modules count (2-4, 0 for random count)")]
                public int modulesCount;

                [JsonProperty("Modules (if you enter modules here the random modules option will not work)")]
                public string[] modules = new string[] {};

                [JsonProperty("Add codelock?")]
                public bool addCodeLock = false;

                [JsonProperty("Add door lock?")]
                public bool addDoorLock = false;

                [JsonProperty("Engine parts tier (0-3, 0 to spawn without parts)")]
                public int enginePartsTier = 3;

                [JsonProperty("Fuel amount (-1 for max amount)")]
                public int fuelAmount = -1;

                [JsonProperty("Water amount for fuel tank (not necessary, -1 for max amount)")]
                public int waterAmount = 0;

                [JsonProperty("Max speed")]
                public float maxSpeed = 15;

                [JsonProperty("Enable infinite fuel")]
                public bool infiniteFuel = true;

                [JsonProperty("Driver preset name (leave blank to spawn random driver)")]
                public string driverPreset = "";

                [JsonProperty("Destroy car after driver death?")]
                public bool destroyCarAfterDeath = false;

                [JsonProperty("Block access to engine parts?")]
                public bool blockEngineLooting = true;

                [JsonProperty("Destroy engine parts after driver death?")]
                public bool destroyEngineParts = true;

                [JsonProperty("Destroy engine parts after full car despawn?")]
                public bool destroyEnginePartsAtFullDespawn = true;

                [JsonProperty("Add flashing lights to car driver module?")]
                public bool enableBlueLights = false;

                [JsonProperty("Vehicle and passenger immortal time after spawn")]
                public float immortalTime =  5f;

                [JsonProperty("Make vehicle immortal?")]
                public bool enableImmortal = false;

                [JsonProperty("Full destroy vehicle (no detonation) when driver is killed by Bradley APC?")]
                public bool fullDestroyBradley = true;

                [JsonProperty("Offset from the road edge")]
                public float roadEdgeOffset = 4f;

                [JsonProperty("Detonator (for vehicle despawn)")]
                public Detonator detonator = new Detonator();

                [JsonProperty("Loot in Storage Module")]
                public StorageLoot storage = new StorageLoot();

                [JsonProperty("Passengers")]
                public List<Passenger> passengers = new List<Passenger>();

                [JsonProperty("Passengers as Companions")]
                public List<PassengerCompanion> passengerCompanions = new List<PassengerCompanion>();

                [JsonProperty("Car Headlights")]
                public CarHeadlights carHeadlights = new CarHeadlights();

                [JsonProperty("Hackable Crate")]
                public CarHackableCrate carHackableCrate = new CarHackableCrate();

                [JsonProperty("Car Turret")]
                public CarTurret turret = new CarTurret();

                public class CarTurret 
                {
                    [JsonProperty("Deploy auto turret on car? (requires CarTurrets plugin)")]
                    public bool enableDeployTurret = false;

                    [JsonProperty("Default weapon shortname")]
                    public string defaultWeapon = "rifle.ak";

                    [JsonProperty("Ammo amount")]
                    public int ammoAmount = 999;
                }

                public class CarHackableCrate
                {
                    [JsonProperty("Spawn hackable crate on car destroy?")]
                    public bool spawnCrate = false;

                    [JsonProperty("Position offset")]
                    public Vector3 posOffset = new Vector3(0, 1f, 0);

                    [JsonProperty("Use custom loot?")]
                    public bool enableCustomLoot = false;

                    [JsonProperty("Custom loot")]
                    public List<DriverPreset.LootInfo> loot = new List<DriverPreset.LootInfo>();
                }

                public class CarHeadlights
                {
                    [JsonProperty("Enable car headlights?")]
                    public bool enableHeadlights = true;

                    [JsonProperty("Enable headlights only at night?")]
                    public bool onlyAtNightMode = true;
                }

                public class PassengerCompanion 
                {
                    [JsonProperty("Spawn chance")]
                    public float chance = 50f;

                    [JsonProperty("Companion setup")]
                    public DriverPreset.CompanionSetup companionSetup;
                }

                public class Passenger 
                {
                    [JsonProperty("Spawn chance")]
                    public float chance = 50f;

                    [JsonProperty("Passenger display name")]
                    public string displayName = "Passenger";

                    [JsonProperty("Passenger skin (0 for random)")]
                    public ulong id = 0;

                    [JsonProperty("Spawn bag with items instead of corpse on death?")]
                    public bool spawnBagOnDeath = false;

                    [JsonProperty("Passenger health")]
                    public MinMax health = new MinMax();

                    [JsonProperty("Passenger loot")]
                    public List<DriverPreset.LootInfo> loot = new List<DriverPreset.LootInfo>();

                    [JsonProperty("Passenger lock inventory containers")]
                    public DriverPreset.ContainerLock lockInventory = new DriverPreset.ContainerLock();
                }

                public class Detonator 
                {
                    [JsonProperty("Add a detonator to the car after the death of the driver (useful to despawn cars)")]
                    public bool enableDetonator = true;

                    [JsonProperty("In how many seconds detonator will be blow up")]
                    public float timer = 300f;

                    [JsonProperty("Detonator position offset on the car")]
                    public Vector3 offset = new Vector3(0, 0.5f, 0);
                }

                public class StorageLoot 
                {
                    [JsonProperty("Add loot to Storage Module")]
                    public bool enableLoot = true;

                    [JsonProperty("Loot")]
                    public List<ItemInfo> lootInfo = new List<ItemInfo>();

                    public struct ItemInfo 
                    {
                        [JsonProperty("Item shortname")]
                        public string shortname;

                        [JsonProperty("Item skin")]
                        public ulong skin;

                        [JsonProperty("Item name (not necessary)")]
                        public string name;

                        [JsonProperty("Spawn chance")]
                        public float chance;

                        [JsonProperty("Item amount")]
                        public MinMax amount;
                    }
                }
            
                public Dictionary<string, object> ConvertForAPI()
                {
                    var dict = new Dictionary<string, object>();

                    dict.Add("Modules", modules);

                    dict.Add("CodeLock", addCodeLock);
                    dict.Add("KeyLock", addDoorLock);

                    dict.Add("EnginePartsTier", enginePartsTier);
                    dict.Add("FuelAmount", fuelAmount);
                    dict.Add("FreshWaterAmount", waterAmount);

                    return dict;
                }
            }

            public class MinMax 
            {
                [JsonProperty("Minimum")]
                public float min = 1;

                [JsonProperty("Maximum")]
                public float max = 5;

                public MinMax(float minimum = 1, float maximum = 5)
                {
                    min = minimum; 
                    max = maximum;
                }

                public float Randomize() => UnityEngine.Random.Range(min, max);
            }
        }

        protected override void LoadDefaultConfig() 
        {
            _config = new Configuration();

            _config.carPresets.Add("3 module vehicle", new Configuration.CarPreset
            {
                modules = new string[]
                {
                    "vehicle.1mod.engine",
                    "vehicle.1mod.storage",
                    "vehicle.1mod.cockpit.with.engine"
                },

                maxSpeed = 10,
                storage = new Configuration.CarPreset.StorageLoot
                {
                    enableLoot = true,
                    lootInfo = new List<Configuration.CarPreset.StorageLoot.ItemInfo>
                    {
                        new Configuration.CarPreset.StorageLoot.ItemInfo 
                        {
                            shortname = "wood",
                            chance = 100,
                            amount = new Configuration.MinMax
                            {
                                min = 1000,
                                max = 10000,
                            }
                        },
                        new Configuration.CarPreset.StorageLoot.ItemInfo
                        {
                            shortname = "stones",
                            chance = 100,
                            amount = new Configuration.MinMax
                            {
                                min = 5000,
                                max = 50000
                            }
                        }
                    }
                },
                passengers = new List<Configuration.CarPreset.Passenger>
                {
                    new Configuration.CarPreset.Passenger
                    {
                        health = new Configuration.MinMax(100, 150),
                        loot = new List<Configuration.DriverPreset.LootInfo>
                        {
                            new Configuration.DriverPreset.LootInfo
                            {
                                shortname = "wood",
                                chance = 100,
                                amount = new Configuration.MinMax
                                {
                                    min = 1000,
                                    max = 10000,
                                }, 
                                container = "main"
                            },
                            new Configuration.DriverPreset.LootInfo
                            {
                                shortname = "stones",
                                chance = 100,
                                amount = new Configuration.MinMax
                                {
                                    min = 5000,
                                    max = 50000
                                },
                                container = "main"
                            }
                        }
                    }
                },
                passengerCompanions = new List<Configuration.CarPreset.PassengerCompanion>
                {
                    new Configuration.CarPreset.PassengerCompanion
                    {
                        chance = 0,
                        companionSetup = new Configuration.DriverPreset.CompanionSetup
                        {
                            enableCompanion = true,
                            health = new Configuration.MinMax(100, 150),
                            clothes = new List<Configuration.DriverPreset.ClothInfo>
                            {
                                new Configuration.DriverPreset.ClothInfo
                                {
                                    shortname = "attire.banditguard"
                                }
                            },
                            weapons = new List<Configuration.DriverPreset.ClothInfo>
                            {
                                new Configuration.DriverPreset.ClothInfo
                                {
                                    shortname = "rifle.ak"
                                }
                            }
                        }
                    }
                },
                carHackableCrate = new Configuration.CarPreset.CarHackableCrate
                {
                    loot = new List<Configuration.DriverPreset.LootInfo>
                    {
                        new Configuration.DriverPreset.LootInfo
                        {
                            shortname = "rifle.ak",
                            amount = new Configuration.MinMax(1, 1),
                            chance = 100,
                            container = "belt"
                        },

                        new Configuration.DriverPreset.LootInfo
                        {
                            shortname = "hatchet",
                            amount = new Configuration.MinMax(1, 1),
                            chance = 100,
                            container = "main"
                        }
                    },
                }
            });

            _config.driverPresets.Add("Yarik Driver", new Configuration.DriverPreset
            {
                attackBehaviour = 2,
                health = new Configuration.MinMax(100, 150),
                loot = new List<Configuration.DriverPreset.LootInfo>
                {
                    new Configuration.DriverPreset.LootInfo
                    {
                        shortname = "rifle.ak",
                        amount = new Configuration.MinMax(1, 1),
                        chance = 100,
                        container = "belt"
                    },

                    new Configuration.DriverPreset.LootInfo
                    {
                        shortname = "hatchet",
                        amount = new Configuration.MinMax(1, 1),
                        chance = 100,
                        container = "main"
                    }
                },
                clothes = new List<Configuration.DriverPreset.ClothInfo>
                {
                    new Configuration.DriverPreset.ClothInfo
                    {
                        shortname = "hazmatsuit"
                    }
                },
                companion = new Configuration.DriverPreset.CompanionSetup
                {
                    enableCompanion = true,
                    health = new Configuration.MinMax(100, 150),
                    clothes = new List<Configuration.DriverPreset.ClothInfo>
                    {
                        new Configuration.DriverPreset.ClothInfo
                        {
                            shortname = "attire.banditguard"
                        }
                    },
                    weapons = new List<Configuration.DriverPreset.ClothInfo>
                    {
                        new Configuration.DriverPreset.ClothInfo
                        {
                            shortname = "rifle.ak"
                        }
                    }
                }
            });
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new System.Exception();

                SaveConfig();
            }
            catch (System.Exception ex)
            {
                PrintError("{0}", ex);
                LoadDefaultConfig();
            }
        }

        #endregion

        #region Дата

        private DataFile _data;

        private class DataFile 
        {
            [JsonProperty("Recorded pathes | Записанные пути")]
            public Dictionary<int, List<Vector3>> pathes = new Dictionary<int, List<Vector3>>();
        }

        private void LoadData() => _data = Interface.Oxide.DataFileSystem.ReadObject<DataFile>("TrafficDrivers_Pathes");
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("TrafficDrivers_Pathes", _data);

        #endregion

        #region Хуки

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if(player == null || player?.net == null || info == null) return null;

            // if(info.Initiator == null) return null;
            // if(!(info.Initiator is BradleyAPC)) return null;

            var driver = GetDriver(player.net.ID.Value);
            if(driver == null) return null;

            driver.Destroy();
            return null;
        }

        private void OnTrafficDestroyed() => _spawner.OnTrafficDestroyed();

        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            foreach(var pair in _debugMarkers)
            {
                if(pair.Value.Contains(entity))
                {
                    if(!_debugMarkers.ContainsKey(target.userID)) return false;
                    else 
                    {
                        var marker = _debugMarkers[target.userID];
                        if(!marker.Contains(entity)) return false;
                    }
                }
            }

            return null;
        }

        private void Loaded()
        {
            Instance = this;

            NextTick(() =>
            {
                if(SpawnModularCar == null)
                {
                    PrintError("SpawnModularCar plugin is not installed");
                    NextTick(() => Interface.Oxide.UnloadPlugin(Title));

                    return;
                }

                if(!_config.enableRecording) Unsubscribe("OnPlayerInput");
                else Subscribe("OnPlayerInput");

                if(_config.limits.convoy.enableConvoySupport && _config.limits.convoy.destroyCarsOnEvent)
                {
                    Subscribe("OnConvoyStart");
                    Subscribe("OnConvoyStop");
                }
                else 
                {
                    Unsubscribe("OnConvoyStart");
                    Unsubscribe("OnConvoyStop");
                }

                LoadData();
            });
        }

        private void OnConvoyStart()
        {
            if(_spawner) _spawner.Pause();

            foreach(var driver in _carsDrivers.Values)
            {
                if(driver == null) continue;

                var bot = driver.Driver;
                if(bot == null) continue;

                var mountable = bot?.GetMountedVehicle();

                mountable.transform.position = new Vector3(0, -9999f, 0);

                this.NextTick(() =>
                {
                    if(mountable != null) mountable.Kill();
                    if(bot != null) bot.Kill();
                });
            }
        }

        private void OnConvoyStop()
        {
            if(_spawner) _spawner.Resume(true);
        }

        private void Unload()
        {
            foreach(var driver in _carsDrivers.Values)
            {
                if(driver == null) continue;
                driver.FullDestroy();
            }

            foreach(var recorder in _recorders.Values)
            {
                if(recorder != null) UnityEngine.Object.Destroy(recorder);
            }

            if(_spawner)
            {
                _spawner.Pause();
                UnityEngine.Object.DestroyImmediate(_spawner);
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if(!input.WasJustPressed(BUTTON.RELOAD)) return;
            if(!_recorders.ContainsKey(player.userID)) return;

            var recorder = _recorders[player.userID];
            if(recorder == null) return;

            recorder.Input();
        }

        private object OnCorpsePopulate(BasePlayer npcPlayer, BaseCorpse corpse)
        {
            TrafficCompanion companion;

            if(npcPlayer.TryGetComponent<TrafficCompanion>(out companion))
            {
                if(!companion.ShouldDeleteCorpse) return false;

                var backpack = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", npcPlayer.transform.position, Quaternion.identity) as DroppedItemContainer;

                List<LootSpawn> loots = new List<LootSpawn>();

                foreach (LootContainer.LootSpawnSlot lootSpawnSlot in companion.NPC.LootSpawnSlots)
                {
                    for (int index = 0; index < lootSpawnSlot.numberToSpawn; ++index)
                    {
                        if (UnityEngine.Random.Range(0.0f, 1f) <= lootSpawnSlot.probability)
                            loots.Add(lootSpawnSlot.definition);
                    }
                }

                backpack.inventory = new ItemContainer();
                backpack.inventory.ServerInitialize((Item) null, loots.Count);
                backpack.inventory.GiveUID();
                backpack.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

                foreach(var bagItem in loots) bagItem.SpawnIntoContainer(backpack.inventory);

                backpack.ResetRemovalTime();
                backpack.Spawn();

                NextTick(() =>
                {
                    corpse.ServerPosition = new Vector3(0, -1000, 0);
                    corpse.Kill();
                });

                return corpse;
            }

            return null;
        }

        private object OnPlayerCorpseSpawn(BasePlayer npcPlayer)
        {
            var driver = GetDriver(npcPlayer.net.ID.Value);

            if(driver != null)
            {
                if(!driver.ShouldDropBag) return null;

                var backpack = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", npcPlayer.transform.position, Quaternion.identity) as DroppedItemContainer;

                backpack.TakeFrom(new ItemContainer[] {
                    npcPlayer.inventory.containerMain, npcPlayer.inventory.containerWear, npcPlayer.inventory.containerBelt
                });
                backpack.playerName = npcPlayer.displayName;
                backpack.playerSteamID = npcPlayer.userID;

                backpack.ResetRemovalTime();
                backpack.Spawn();

                return false;
            }

            TrafficPassenger passenger;

            if(npcPlayer.TryGetComponent<TrafficPassenger>(out passenger))
            {
                var backpack = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", npcPlayer.transform.position, Quaternion.identity) as DroppedItemContainer;

                backpack.TakeFrom(new ItemContainer[] {
                    npcPlayer.inventory.containerMain, npcPlayer.inventory.containerWear, npcPlayer.inventory.containerBelt
                });
                backpack.playerName = npcPlayer.displayName;
                backpack.playerSteamID = npcPlayer.userID;

                backpack.ResetRemovalTime();
                backpack.Spawn();

                return false;
            }

            return null;
        }

        private object OnEntityTakeDamage(BaseVehicleModule module, HitInfo info)
        {
            if(module == null || info == null) return null;

            var car = module.Vehicle;

            if(car == null) return null;

            var driver = car.GetDriver();
            
            if(driver != null)
            {
                if(driver.net == null) return null;

                var traffic = GetDriver(driver.net.ID.Value);

                if(traffic != null)
                {
                    if(traffic.IsCarImmortal) 
                    {
                        info.damageTypes = new Rust.DamageTypeList();
                        info.DidHit = false;
                        info.DoHitEffects = false;

                        return true;
                    }
                }
            }
            
            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
        {
            if(item == null) return null;
            if(item.parent == null) return null;
            if(item.parent.IsLocked()) return false;
            
            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainerId, int targetSlotIndex, int splitAmount)
        {
            if(item == null) return null;
            if(item.parent == null) return null;
            if(item.parent.IsLocked()) return false;
            
            return null;
        }

        private object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if(player == null || info == null) return null;
            if(info.InitiatorPlayer == null) return null;

            if(!player.userID.IsSteamId() && player.net != null)
            {
                var driver = GetDriver(player.net.ID.Value);

                if(driver != null)
                {
                    if(!info.InitiatorPlayer.userID.IsSteamId() && info.InitiatorPlayer.net != null)
                    {
                        var companion = GetCompanion(info.InitiatorPlayer.net.ID.Value);

                        if(companion != null)
                        {
                            return false;
                        }
                    }
                }
            }

            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if(entity == null || info == null || info?.damageTypes == null) return null;

            if(entity is BaseVehicleModule) OnEntityTakeDamage(entity as BaseVehicleModule, info);

            if(entity is BasePlayer)
            {
                var player = entity.ToPlayer();

                if(player != null)
                {
                    if(!player.userID.IsSteamId() && player.net != null)
                    {
                        var driver = GetDriver(player.net.ID.Value);

                        if(driver != null) 
                        {
                            if(driver.IsImmortal) return true;
                            info.damageTypes.ScaleAll(driver.GetDamageReceiveRate());
                        }
                        else 
                        {
                            var companion = GetCompanion(player.net.ID.Value);

                            if(companion != null) 
                            {
                                var companionDriver = GetDriver(companion.GetDriverID());

                                if(companionDriver != null)
                                {
                                    if(companionDriver.IsImmortal) return true;
                                }

                                info.damageTypes.ScaleAll(companion.GetDamageReceiveRate());
                            }
                        }
                    }
                }
            }
            else 
            {
                if(info.Initiator != null)
                {
                    if(info.Initiator is ModularCar)
                    {    
                        if(_config.limits.convoy.enableConvoySupport)
                        {
                            if(_config.limits.convoy.destroyCarsOnHit)
                            {
                                if(Convoy != null)
                                {
                                    var driverMounted = ((ModularCar)info.Initiator)?.GetDriver();

                                    if(driverMounted != null)
                                    {
                                        if(driverMounted.net != null)
                                        {
                                            var driver = GetDriver(driverMounted.net.ID.Value);

                                            if(driver != null)
                                            {
                                                var callResult = Convoy.Call("IsConvoyVehicle", entity);

                                                if(callResult != null)
                                                {
                                                    if(callResult is bool)
                                                    {
                                                        if(((bool)callResult))
                                                        {
                                                            UnityEngine.Object.Destroy(driver);
                                                            return null;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if(_config.limits.despawnPrefabs.Length != 0 || _config.limits.fullDespawnPrefabs.Length != 0)
                        {
                            var find = Physics.OverlapSphere(entity.transform.position, 1f, -1);

                            if(find != null && find?.Length != 0)
                            {
                                foreach(var collider in find)
                                {
                                    if(collider == null) continue;

                                    var ent = collider.ToBaseEntity();

                                    if(ent != null)
                                    {
                                        if(_config.limits.despawnPrefabs.Contains(ent.ShortPrefabName) || _config.limits.fullDespawnPrefabs.Contains(ent.ShortPrefabName))
                                        {
                                            var driverMounted = ((ModularCar)info.Initiator).GetDriver();

                                            if(driverMounted != null)
                                            {
                                                var driver = GetDriver(driverMounted.net.ID.Value);

                                                if(driver != null)
                                                {
                                                    if(_config.limits.destroyPrefab) ent.Kill();

                                                    if(_config.limits.despawnPrefabs.Contains(ent.ShortPrefabName)) 
                                                    {
                                                        if(driver.Driver != null) driver.Driver.Kill();
                                                    }
                                                    else driver.FullDestroy();
                                                    
                                                    return null;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                BaseModularVehicle vehicle = null;

                if(entity is BaseVehicleModule)
                {
                    var module = (BaseVehicleModule)entity;
                    if(module.Vehicle != null) vehicle = module.Vehicle;
                }

                if(entity is BaseModularVehicle) vehicle = (BaseModularVehicle)entity;

                if(vehicle != null)
                {
                    var driverMounted = vehicle.GetDriver();
                    if(driverMounted == null || driverMounted?.net == null) return null;

                    var driver = GetDriver(driverMounted.net.ID.Value);
                    if(driver == null) return null; 

                    if(driver.IsImmortal) return true;

                    if(info.InitiatorPlayer != null) driver.OnAttacked(info.InitiatorPlayer);
                    else if(info.Initiator != null)
                    {
                        if(info.Initiator is BaseVehicle && info.Initiator != entity)
                        {
                            var attackerVehicle = (BaseVehicle)info.Initiator;

                            if(attackerVehicle != null)
                            {
                                if(attackerVehicle.HasDriver())
                                {
                                    driver.OnAttacked(attackerVehicle.GetDriver());
                                    return null;
                                }
                            }
                        }

                        if(info.damageTypes.Total() > 2f) driver.OnPhysicAttacked();
                    }
                }
            }

            if(info.InitiatorPlayer != null)
            {
                if(info.InitiatorPlayer.net != null)
                {
                    var initiatorCompanion = GetCompanion(info.InitiatorPlayer.net.ID.Value);

                    if(initiatorCompanion != null)
                    {
                        if(entity.net != null)
                        {
                            if(initiatorCompanion.GetExcludeList().Contains(entity.net.ID.Value)) return true;
                        }
                    }
                }
            }

            return null;
        }

        private void OnServerInitialized()
        {
            if(_data == null) LoadData();

            if(_data.pathes.Count == 0)
            {
                Puts("Recorded paths not found, load standard Rust roads");

                foreach(var path in TerrainMeta.Path.Roads)
                {
                    if(path.Path.Points.Length < _config.limits.minRoadLength || path.Splat == 1) continue;

                    _roadPathes.Add(path.Path.Points);
                }

                if(_roadPathes.Count == 0)
                {
                    PrintError("No suitable roads found! Decrease the minimum road length for traffic in the config.");
                    NextTick(() => Interface.Oxide.UnloadPlugin(Title));

                    return;
                }
            }
            else 
            {
                Puts("Recorded paths found in data file, load them");

                foreach(var path in _data.pathes)
                {
                    _roadPathes.Add(path.Value.ToArray());
                }
            }

            _spawner = new GameObject("Traffic SpawnManager", typeof(SpawnManager)).GetComponent<SpawnManager>();
            _spawner.Init(_config.limits, this, _roadPathes);

            Puts($"Found suitable roads: {_roadPathes.Count}");
        }

        private object OnNpcTarget(BaseEntity npc, BaseEntity entity)
        {
            if(npc == null || entity == null) return null;
            if(npc.net == null || entity.net == null) return null;

            var companion = GetCompanion(npc.net.ID.Value);

            if(companion != null)
            {
                if(!companion.CanTargetEntity(entity)) return true;
            }

            var driver = GetDriver(entity.net.ID.Value);
            if(driver != null && _config.limits.blockTrafficTarget) return true;

            var companionBlock = GetCompanion(entity.net.ID.Value);
            if(companionBlock != null && _config.limits.blockTrafficTarget) return true;

            return null;
        }

        private void OnPlayerCorpseSpawned(BasePlayer player, PlayerCorpse corpse)
        {
            if(player.inventory.containerMain.IsLocked()) corpse.containers[0].SetLocked(true);
            if(player.inventory.containerBelt.IsLocked()) corpse.containers[1].SetLocked(true);
            if(player.inventory.containerWear.IsLocked()) corpse.containers[2].SetLocked(true);
        }

        #endregion

        #region Методы

        [ConsoleCommand("trafficdrivers.debug")]
        private void showDebugInfo(ConsoleSystem.Arg arg)
        {
            var drivers = _spawner.GetDrivers();
            string msg = $"Traffic cars amount: {drivers.Count}";

            if(arg.Player() != null) 
            {
                var player = arg.Player();
                if(!player.IsAdmin) return;

                foreach(var driver in drivers)
                {
                    if(driver == null) continue;
                    msg += $"\n{driver.CarPresetName}: {PhoneController.PositionToGridCoord(driver.Position)}";

                    DebugMarker marker = new GameObject("Debug Marker", typeof(DebugMarker)).GetComponent<DebugMarker>();

                    marker.displayName = driver.CarPresetName;
                    marker.radius = 0.2f;
                    marker.alpha = 0.5f;
                    marker.refreshRate = 3f;
                    marker.position = driver.Position;
                    marker.duration = 15;
                    marker.playerID = player.userID;

                    ColorUtility.TryParseHtmlString($"#00FFFF", out marker.color1);
                    ColorUtility.TryParseHtmlString($"#00FFFFFF", out marker.color2);

                    var instance = TrafficDrivers.Instance;
                    marker.instance = instance;

                    if(instance._debugMarkers.ContainsKey(player.userID))
                    {
                        var found = instance._debugMarkers[player.userID];
                        
                        if(found != null) 
                        {
                            foreach(var mrkr in found)
                            {
                                if(mrkr != null) UnityEngine.Object.Destroy(mrkr);
                            }
                        }

                        instance._debugMarkers.Remove(player.userID);
                    }
                }

                player.ChatMessage(msg);

                return;
            }

            foreach(var driver in drivers)
            {
                if(driver == null) continue;
                msg += $"\n{driver.CarPresetName}: {PhoneController.PositionToGridCoord(driver.Position)}";
            }

            Puts(msg);
        }

        [ChatCommand("trafficdrivers")]
        private void chatAdminCommand(BasePlayer player, string command, string[] args)
        {
            if(!player.IsAdmin) return;
            if(args?.Length == 0) return;

            if(args[0] == "recorder")
            {
                if(!_config.enableRecording)
                {
                    player.ChatMessage("Recorder disabled in config!");
                    return;
                }

                PlayerPathRecorder recorder;
                
                if(_recorders.ContainsKey(player.userID))
                {
                    recorder = _recorders[player.userID];
                    if(recorder != null) UnityEngine.Object.Destroy(recorder);

                    _recorders.Remove(player.userID);
                }

                recorder = player.gameObject.AddComponent<PlayerPathRecorder>();
                recorder.Init(this);

                _recorders.Add(player.userID, recorder);

                switch(lang.GetLanguage(player.UserIDString))
                {
                    case "ru":
                        player.ChatMessage("Рекордер добавлен. Чтобы начать/закончить запись пути, нажмите кнопку R");
                        break;

                    default:
                        player.ChatMessage("Recorder added. To start/stop recording a path, press R button");
                        break;
                }

                return;
            }

            if(args[0] == "markers")
            {
                if(player.IsAdmin)
                {
                    if(MarkerManager)
                    {
                        foreach(var driver in _carsDrivers.Values)
                        {
                            if(driver != null)
                            {
                                MarkerManager.Call("API_CreateMarker", driver.transform.position, driver.CarPresetName, 5, 3f, 0.4f, driver.CarPresetName, "34495e", "7f8c8d");
                            }
                        }

                        player.ChatMessage("Car drivers now visible on the in-game map for everyone for 5 seconds");
                    }
                    else player.ChatMessage("MarkerManager plugin isn't installed");
                }
            }
        }

        private TrafficDriver SpawnTrafficCarParams(Vector3[] path, Vector3 position, TrafficDriver.DriveSide side)
        {
            var presetsList = new List<Configuration.CarPreset>(_config.carPresets.Values);
            var driversList = new List<Configuration.DriverPreset>(_config.driverPresets.Values);

            var carPreset = presetsList[UnityEngine.Random.Range(0, presetsList.Count)]; 
            var driverPreset = !string.IsNullOrEmpty(carPreset.driverPreset) ? _config.driverPresets[carPreset.driverPreset] : driversList[UnityEngine.Random.Range(0, driversList.Count)];

            BasePlayer bot = GameManager.server.CreateEntity(driverPreset.spawnAsScientist ? "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab" : "assets/prefabs/player/player.prefab", position) as BasePlayer;
            
            if(!driverPreset.spawnAsScientist)
            {
                bot.displayName = driverPreset.name;

                bot.userID = driverPreset.skin == 0 ? (ulong)UnityEngine.Random.Range(1, 99999) : driverPreset.skin;
                bot.UserIDString = bot.userID.ToString();
            }

            bot.enableSaving = false;

            bot.Spawn();
            if(!driverPreset.spawnAsScientist) bot.InitializeHealth(driverPreset.health.min, driverPreset.health.max);

            if(driverPreset.spawnAsScientist)
            {
                var navigator = bot.GetComponent<BaseNavigator>();
                var npc = bot.GetComponent<ScientistNPC>();

                navigator.CanUseNavMesh = false;
                navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = 0f;
                navigator.DefaultArea = "Not Walkable";    

                bot.displayName = driverPreset.name;
                bot._health = UnityEngine.Random.Range(driverPreset.health.min, driverPreset.health.max);

                bot.inventory.Strip();
                npc.CancelInvoke(npc.EquipTest);
            }

            if(driverPreset.containerLock.lockWear) bot.inventory.containerWear.SetLocked(true);
            if(driverPreset.containerLock.lockBelt) bot.inventory.containerBelt.SetLocked(true);
            if(driverPreset.containerLock.lockMain) bot.inventory.containerMain.SetLocked(true);

            foreach(var cloth in driverPreset.clothes) bot.inventory.containerWear.Insert(ItemManager.CreateByName(cloth.shortname, 1, cloth.skin));

            foreach(var loot in driverPreset.loot)
            {
                if(UnityEngine.Random.Range(0, 100) < loot.chance)
                {
                    Item item = ItemManager.CreateByName(loot.shortname, (int)loot.amount.Randomize(), loot.skin);

                    if(item == null) continue;
                    if(!string.IsNullOrEmpty(loot.name)) item.name = loot.name;

                    if(loot.container == "main") bot.inventory.containerMain.Insert(item);
                    if(loot.container == "belt") bot.inventory.containerBelt.Insert(item);
                    if(loot.container == "wear") bot.inventory.containerWear.Insert(item);
                }
            }

            bool isFilledWithRandom = false;

            if(carPreset.modules == null || carPreset.modules?.Length == 0)
            {
                List<string> modules = new List<string>();

                string prefab;

                if(carPreset.modulesCount == 0) prefab = $"assets/content/vehicles/modularcar/{UnityEngine.Random.Range(3, 5)}module_car_spawned.entity.prefab";
                else prefab = $"assets/content/vehicles/modularcar/{carPreset.modulesCount}module_car_spawned.entity.prefab";

                ModularCar vehicle = GameManager.server.CreateEntity(prefab) as ModularCar;
                vehicle.Spawn();

                foreach (KeyValuePair<BaseVehicleModule, System.Action> entry in (new List<KeyValuePair<BaseVehicleModule, System.Action>>(vehicle.moduleAddActions)))
                {
                    modules.Add(entry.Key.AssociatedItemDef.shortname);
                }

                if(modules.Count < 2) modules.Add("vehicle.1mod.storage");

                isFilledWithRandom = true;

                vehicle.Kill();
                carPreset.modules = modules.ToArray();
            }

            ModularCar car = SpawnModularCar.Call<ModularCar>("API_SpawnPresetCar", bot, carPreset.ConvertForAPI());
            car.enableSaving = false;

            if(isFilledWithRandom) carPreset.modules = new string[] {};

            if(carPreset.infiniteFuel)
            {
                var container = car.GetFuelSystem().fuelStorageInstance.Get(true);
                
                if (container != null)
                {
                    var item = ItemManager.CreateByName("lowgradefuel", 200, 12345);

                    item.OnDirty += delegate(Item item1)
                    {
                        item1.amount = 200;
                    };

                    container.inventory.Insert(item);
                    item.SetFlag(global::Item.Flag.IsLocked, true);
                    
                    container.dropsLoot = false;
                    container.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                    container.SetFlag(BaseEntity.Flags.Locked, true);
                }
            }
            
            VehicleModuleStorage storage = null;
            BaseMountable driverPlace = null, companionPlace = null;

            BaseVehicleModule driverPlaceModule = null;

            List<VehicleModuleSeating> seatingModules = new List<VehicleModuleSeating>();

            foreach(var module in car.AttachedModuleEntities)
            {
                module.SetHealth(module.MaxHealth());

                if(carPreset.storage.enableLoot && storage == null)
                {
                    if(module is VehicleModuleStorage && module.ShortPrefabName == "1module_storage")
                    {
                        storage = module as VehicleModuleStorage;
                        var container = storage.GetContainer();

                        if(container.inventory.entityOwner is StorageContainer) 
                        {
                            ((StorageContainer)container.inventory.entityOwner).dropsLoot = false;
                        }
                    
                        foreach(var item in carPreset.storage.lootInfo)
                        {
                            if(UnityEngine.Random.Range(0, 100) > item.chance) continue;

                            Item loot = ItemManager.CreateByName(item.shortname, (int)item.amount.Randomize(), item.skin);
                            if(!string.IsNullOrEmpty(item.name)) loot.name = item.name;

                            container.inventory.Insert(loot);
                        }
                    }
                }

                if(carPreset.blockEngineLooting)
                {
                    if(module is VehicleModuleEngine) 
                    {
                        var container = ((VehicleModuleEngine)module).GetContainer();
                        container.inventory.SetLocked(true);

                        if(container.inventory.entityOwner is StorageContainer) ((StorageContainer)container.inventory.entityOwner).dropsLoot = false;
                    }
                }

                if(module is VehicleModuleSeating)
                {
                    var seating = module as VehicleModuleSeating;

                    seatingModules.Add(seating);

                    foreach(var info in seating.mountPoints)
                    {
                        if(driverPlace != null && companionPlace != null) break;

                        if(info.isDriver && driverPlace == null) 
                        {
                            driverPlace = info.mountable;
                            driverPlaceModule = module;

                            if(carPreset.enableBlueLights) AddFlashingLights(seating);
                        }
                        else if(!info.isDriver && companionPlace == null)
                        {
                            companionPlace = info.mountable;
                        }
                    }
                }
            }

            if(carPreset.turret.enableDeployTurret && CarTurrets != null)
            {
                var deployTurret = CarTurrets.Call<AutoTurret>("API_DeployAutoTurret", driverPlaceModule, bot);

                if(deployTurret != null)
                {
                    if(!string.IsNullOrEmpty(carPreset.turret.defaultWeapon))
                    {
                        var def = ItemManager.FindItemDefinition(carPreset.turret.defaultWeapon);
                        deployTurret.inventory.AddItem(def, 1);

                        deployTurret.UpdateAttachedWeapon();

                        if(carPreset.turret.ammoAmount != 0)
                        {
                            var weapon = deployTurret.GetAttachedWeapon();
                            
                            if(weapon != null)
                            {
                                deployTurret.inventory.Insert(ItemManager.Create(weapon.primaryMagazine.ammoType, carPreset.turret.ammoAmount));
                                deployTurret.UpdateTotalAmmo();
                            }
                        }
                    }

                    deployTurret.InitiateStartup();
                }
            }

            driverPlace.MountPlayer(bot);

            TrafficCompanion trafficCompanion = null;
            BasePlayer trafficCompanionPlayer = null;

            List<BasePlayer> passengers = new List<BasePlayer>();

            if(driverPreset.companion.enableCompanion)
            {
                var companion = GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab", position, Quaternion.identity) as ScientistNPC;
                companion.Spawn();

                trafficCompanionPlayer = companion;

                companion.enableSaving = false;

                trafficCompanion = companion.gameObject.AddComponent<TrafficCompanion>();
                trafficCompanion.Init(driverPreset.companion);

                trafficCompanion.AddExclude(bot.net.ID.Value);
                trafficCompanion.AddExclude(car.net.ID.Value);

                companionPlace.MountPlayer(companion);
                _driverCompanions.Add(companion.net.ID.Value, trafficCompanion);

                passengers.Add(companion);
            }

            foreach(var passenger in carPreset.passengers)
            {
                if(UnityEngine.Random.Range(0f, 100f) < passenger.chance)
                {
                    BasePlayer passengerPlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", bot.transform.position) as BasePlayer;
                    passengerPlayer.displayName = passenger.displayName;

                    passengerPlayer.userID = passenger.id == 0 ? (ulong)UnityEngine.Random.Range(1, 99999) : passenger.id;
                    passengerPlayer.UserIDString = passengerPlayer.userID.ToString();

                    passengerPlayer.Spawn();
                    passengerPlayer.enableSaving = false;

                    passengerPlayer.InitializeHealth(passenger.health.min, passenger.health.max);

                    if(passenger.lockInventory.lockWear) passengerPlayer.inventory.containerWear.SetLocked(true);
                    if(passenger.lockInventory.lockBelt) passengerPlayer.inventory.containerBelt.SetLocked(true);
                    if(passenger.lockInventory.lockMain) passengerPlayer.inventory.containerMain.SetLocked(true);

                    passengerPlayer.inventory.Strip();

                    foreach(var loot in passenger.loot)
                    {
                        if(UnityEngine.Random.Range(0, 100) < loot.chance)
                        {
                            Item item = ItemManager.CreateByName(loot.shortname, (int)loot.amount.Randomize(), loot.skin);

                            if(item == null) continue;
                            if(!string.IsNullOrEmpty(loot.name)) item.name = loot.name;

                            if(loot.container == "main") passengerPlayer.inventory.containerMain.Insert(item);
                            if(loot.container == "belt") passengerPlayer.inventory.containerBelt.Insert(item);
                            if(loot.container == "wear") passengerPlayer.inventory.containerWear.Insert(item);
                        }
                    }
                    
                    foreach(var seating in seatingModules)
                    {
                        var seat = seating.mountPoints.Find(x => x.mountable != driverPlace && x.mountable != companionPlace && x.isDriver == false && !x.mountable.IsBusy());

                        if(seat != null)
                        {
                            seat.mountable.MountPlayer(passengerPlayer);
                            break;
                        }
                    }

                    if(!passengerPlayer.isMounted)
                    {
                        passengerPlayer.Teleport(new Vector3(0, -1000, 0));
                        passengerPlayer.Kill();
                    }
                    else 
                    {
                        if(trafficCompanion) trafficCompanion.AddExclude(passengerPlayer.net.ID.Value);
                        if(passenger.spawnBagOnDeath) passengerPlayer.gameObject.AddComponent<TrafficPassenger>().ShouldDropBag = true;

                        passengers.Add(passengerPlayer);
                    }
                }
            }

            foreach(var passengerCompanion in carPreset.passengerCompanions)
            {
                if(UnityEngine.Random.Range(0f, 100f) < passengerCompanion.chance)
                {
                    var companion = GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab", position, Quaternion.identity) as ScientistNPC;
                    companion.Spawn();

                    companion.enableSaving = false;

                    var trafficCompanionPassenger = companion.gameObject.AddComponent<TrafficCompanion>();
                    trafficCompanionPassenger.Init(driverPreset.companion);

                    trafficCompanionPassenger.AddExclude(bot.net.ID.Value);
                    trafficCompanionPassenger.AddExclude(car.net.ID.Value);

                    foreach(var seating in seatingModules)
                    {
                        var seat = seating.mountPoints.Find(x => x.mountable != driverPlace && x.mountable != companionPlace && x.isDriver == false && !x.mountable.IsBusy());

                        if(seat != null)
                        {
                            seat.mountable.MountPlayer(companion);
                            break;
                        }
                    }
                    
                    if(!companion.isMounted)
                    {
                        companion.Teleport(new Vector3(0, -1000, 0));
                        companion.Kill();
                    }
                    else 
                    {
                        if(trafficCompanion) trafficCompanion.AddExclude(companion.net.ID.Value);
                        if(trafficCompanionPlayer) trafficCompanionPassenger.AddExclude(trafficCompanionPlayer.net.ID.Value);

                        foreach(var passenger in passengers) 
                        {
                            if(passenger == null) continue;
                            trafficCompanionPassenger.AddExclude(passenger.net.ID.Value);
                        }

                        passengers.Add(companion);
                        _driverCompanions.Add(companion.net.ID.Value, trafficCompanionPassenger);
                    }
                }
            }

            foreach(var passenger in passengers)
            {
                TrafficCompanion trafficCompanion1;

                if(passenger.TryGetComponent<TrafficCompanion>(out trafficCompanion1))
                {
                    foreach(var passengerExclude in passengers)
                    {
                        trafficCompanion1.AddExclude(passengerExclude.net.ID.Value);
                    }
                }
            }

            var driver = _spawner.gameObject.AddComponent<TrafficDriver>();  

            driver.Driver = bot;
            driver.AssignCar(car, path, side, driverPreset, carPreset, GetKeyFromValue(_config.carPresets, carPreset), _config.limits.stuckDespawnTime, trafficCompanion, passengers);

            _carsDrivers.Add(bot.net.ID.Value, driver);

            Puts($"Traffic car was sucessfully spawned!");

            return driver;
        }

        private void AddFlashingLights(VehicleModuleSeating module)
        {
            BaseEntity light1 = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", module.transform.position), light2 = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", module.transform.position);
            
            DestroyOnGroundMissing groundMissing;
            if(light1.TryGetComponent<DestroyOnGroundMissing>(out groundMissing)) UnityEngine.Object.Destroy(groundMissing);
            if(light2.TryGetComponent<DestroyOnGroundMissing>(out groundMissing)) UnityEngine.Object.Destroy(groundMissing);

            GroundWatch groundWatch;
            if(light1.TryGetComponent<GroundWatch>(out groundWatch)) UnityEngine.Object.Destroy(groundWatch);
            if(light2.TryGetComponent<GroundWatch>(out groundWatch)) UnityEngine.Object.Destroy(groundWatch);

            StabilityEntity stabilityEntity;
            if(light1.TryGetComponent<StabilityEntity>(out stabilityEntity)) UnityEngine.Object.Destroy(stabilityEntity);
            if(light2.TryGetComponent<StabilityEntity>(out stabilityEntity)) UnityEngine.Object.Destroy(stabilityEntity);

            light1.Spawn();
            light2.Spawn();

            light1.SetParent(module, true);
            light2.SetParent(module, true);

            module.AddChild(light1);
            module.AddChild(light2);

            light1.transform.localPosition += new Vector3(-0.5f, 1.35f, -0.4f);
            light2.transform.localPosition += new Vector3(0.5f, 1.35f, -0.4f);

            light1.SetFlag(BaseEntity.Flags.Reserved8, true);
            light2.SetFlag(BaseEntity.Flags.Reserved8, true);

            light1.SendNetworkUpdateImmediate(true);
            light2.SendNetworkUpdateImmediate(true);

            module.SendNetworkUpdateImmediate(true);
        }

        private TrafficDriver SpawnTrafficCar()
        {
            var road = _roadPathes[UnityEngine.Random.Range(0, _roadPathes.Count)];

            var side = UnityEngine.Random.Range(-5, 5) > 0 ? TrafficDriver.DriveSide.Right : TrafficDriver.DriveSide.Left;
            var position = road[side == TrafficDriver.DriveSide.Right ? 0 : road.Length - 1];

            return SpawnTrafficCarParams(road, position, side);
        }

        public static string GetKeyFromValue(Dictionary<string, Configuration.CarPreset> dictionary, Configuration.CarPreset value)
        {
            foreach (string keyVar in dictionary.Keys)
            {
                if (dictionary[keyVar] == value) return keyVar;
            }

            return null;
        }

        #endregion

        #region Запись пути

        private class PlayerPathRecorder : FacepunchBehaviour
        {
            private TrafficDrivers _pluginInstance;

            private BasePlayer _player;
            private List<Vector3> _path = new List<Vector3>();

            private bool _isRecording = false;

            private void Start() => _player = GetComponent<BasePlayer>();
            
            public virtual void Init(TrafficDrivers plugin) => _pluginInstance = plugin;

            public void Input()
            {
                if(_isRecording)
                {
                    StopRecording();
                    _path.Add(_player.transform.position);

                    _pluginInstance._data.pathes.Add(_pluginInstance._data.pathes.Keys.Count, _path);

                    switch(_pluginInstance.lang.GetLanguage(_player.UserIDString))
                    {
                        case "ru":
                            _player.ChatMessage($"Путь записан и сохранен в дата-файл. Индекс: {_pluginInstance._data.pathes.Keys.Count - 1}");
                            break;
                        default:
                            _player.ChatMessage($"Path recorded and saved to data. Index: {_pluginInstance._data.pathes.Keys.Count - 1}");
                            break;
                    }

                    _pluginInstance.SaveData();
                    Destroy(this);
                }
                else 
                {
                    StartRecording();

                    switch(_pluginInstance.lang.GetLanguage(_player.UserIDString))
                    {
                        case "ru":
                            _player.ChatMessage("Запись пути начата! Нажмите R чтобы остановить запись.");
                            break;
                        default:
                            _player.ChatMessage("Path recording started! Press R to stop recording.");
                            break;
                    }
                }
            }

            public void StartRecording()
            {
                _isRecording = true;
                InvokeRepeating(() => Record(), 0.1f, _pluginInstance._config.cooldownSecondsRecorder);
            }

            public void StopRecording() => _isRecording = false;

            public virtual void Record()
            {
                if(!_isRecording) return;

                _path.Add(_player.transform.position);

                switch(_pluginInstance.lang.GetLanguage(_player.UserIDString))
                {
                    case "ru":
                        _player.ChatMessage($"Добавлена точка {_player.transform.position}, индекс {_path.Count - 1}");
                        break;
                    default:
                        _player.ChatMessage($"Added point {_player.transform.position}, index {_path.Count - 1}");
                        break;
                }
            }
        }

        #endregion

        #region Поведение

        public class DebugMarker : MonoBehaviour
        {
            private const string genericPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
            private const string vendingPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        
            private VendingMachineMapMarker vending;
            private MapMarkerGenericRadius generic;

            public float radius, alpha, refreshRate;
            public Color color1, color2;
            public string displayName;
            public Vector3 position;
            public int duration;
            public TrafficDrivers instance;

            public ulong playerID;

            private void Start()
            {
                transform.position = position;

                vending = GameManager.server.CreateEntity(vendingPrefab, position).GetComponent<VendingMachineMapMarker>();
                vending.markerShopName = displayName;
                vending.enableSaving = false;
                vending.Spawn();

                generic = GameManager.server.CreateEntity(genericPrefab).GetComponent<MapMarkerGenericRadius>();
                generic.color1 = color1;
                generic.color2 = color2;
                generic.radius = radius;
                generic.alpha = alpha;
                generic.enableSaving = false;
                generic.SetParent(vending);
                generic.Spawn();

                if (duration != 0) Invoke(nameof(DestroyMakers), duration);
                if (refreshRate > 0f) InvokeRepeating(nameof(UpdateMarkers), refreshRate, refreshRate);

                UpdateMarkers();

                if(instance._debugMarkers.ContainsKey(playerID))
                {
                    var list = instance._debugMarkers[playerID];
                    list.Add(generic);

                    instance._debugMarkers[playerID] = list;
                }
                else instance._debugMarkers.Add(playerID, new List<BaseNetworkable> {generic});
            }

            public void UpdateMarkers()
            {
                vending.SendNetworkUpdate();
                generic.SendUpdate();
            }

            private void DestroyMakers()
            {
                if (vending.IsValid()) vending.Kill();
                if (generic.IsValid()) generic.Kill();

                Destroy(gameObject);
            }

            private void OnDestroy() 
            {
                if (vending.IsValid()) vending.Kill();
                if (generic.IsValid()) generic.Kill();
            }
        }

        public class TrafficCompanion : FacepunchBehaviour
        {
            private ScientistNPC _npc;
            private Configuration.DriverPreset.CompanionSetup _setup;
            private List<ulong> _attackExclude = new List<ulong>();

            private List<ulong> _attackers = new List<ulong>();

            public virtual ScientistNPC NPC => _npc;
            public bool ShouldDeleteCorpse => _setup.spawnBagOnDeath;

            public virtual bool CanTargetEntity(BaseEntity ent) => _setup.preventAttackFirst ? (!_attackExclude.Contains(ent.net.ID.Value) && _attackers.Contains(ent.net.ID.Value)) : (!_attackExclude.Contains(ent.net.ID.Value));
            public virtual float GetDamageReceiveRate() => _setup.damageReceiveRate * (_npc.isMounted ? 10f : 1f);

            public List<ulong> GetExcludeList() => _attackExclude; 

            public virtual void Kill() => _npc.Kill();

            public virtual ulong GetDriverID() => _attackExclude[0];

            public virtual void AddAttacker(ulong id) 
            {
                if(_attackers.Contains(id)) return;
                _attackers.Add(id);
            }
 
            public virtual void Init(Configuration.DriverPreset.CompanionSetup companionSetup)
            {
                _setup = companionSetup;

                _npc = GetComponent<ScientistNPC>();

                var navigator = GetComponent<BaseNavigator>();

                navigator.CanUseNavMesh = false;
                navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = 0f;
                navigator.DefaultArea = "Not Walkable";     

                _npc.displayName = _setup.name;
                _npc._health = companionSetup.health.Randomize();
            
                _npc.inventory.containerWear.Clear();
                _npc.Brain = _npc.GetComponent<ScientistBrain>();

                _npc.CancelInvoke(_npc.EquipTest);

                foreach(var item in _npc.inventory.containerBelt.itemList) item.Remove();
                foreach(var weapon in _setup.weapons) ItemManager.CreateByName(weapon.shortname, 1, weapon.skin).MoveToContainer(_npc.inventory.containerBelt);

                foreach(var item in _npc.inventory.containerBelt.itemList)
                {
                    if(item.info.category == ItemCategory.Weapon)
                    {
                        var held = item.GetHeldEntity();
                        
                        if(held != null)
                        {
                            if(held is BaseProjectile)
                            {
                                var projectile = held as BaseProjectile;
                                projectile.damageScale += _setup.damageHurtRate;
                            }
                        }
                    }
                }

                foreach(var cloth in _setup.clothes) ItemManager.CreateByName(cloth.shortname, 1, cloth.skin).MoveToContainer(_npc.inventory.containerWear);

                _npc.EquipWeapon();
            }

            public virtual void AddExclude(ulong exclude) => _attackExclude.Add(exclude);
        }

        public class TrafficDriver : FacepunchBehaviour
        {
            public enum Movement 
            {
                Idle = 0, Forward = 2, Backward = 4, Left = 8, Right = 16
            }

            public enum DriveSide 
            {
                Left = -1, Right = 1
            }

            private ModularCar _car;

            private TrafficCompanion _companion;

            private DriveSide _side;
            private Vector3[] _path;
            
            private Configuration.DriverPreset _driverPreset;
            private Configuration.CarPreset _carPreset;
            
            private List<BasePlayer> _passengers = new List<BasePlayer>();

            private float _lastObstacleTime;
            private int _currentPathIndex = 0;

            private float _lastIncreaseSpeedTime, _stuckTime, _lastDestinationTime, _spawnTime, _lastTimeBackwards;
            private bool _isDestroyed = false;

            public bool IsImmortal => _spawnTime + _carPreset.immortalTime > Time.realtimeSinceStartup;

            public bool IsCarImmortal => _carPreset.enableImmortal;
            public bool ShouldDropBag => _driverPreset.spawnBagOnDeath;
            public string CarPresetName;

            public Vector3 Position => _car != null ? _car.transform.position : Vector3.zero;
            public BasePlayer Driver, CompanionPlayer;

            public virtual float GetDamageReceiveRate() => _driverPreset.damageReceiveRate;

            public virtual void Start() 
            {
                _spawnTime = Time.realtimeSinceStartup;
                _lastDestinationTime = Time.realtimeSinceStartup;

                if(_carPreset.carHeadlights.enableHeadlights)
                {
                    if(_carPreset.carHeadlights.onlyAtNightMode)
                    {
                        InvokeRepeating(() => 
                        {
                            var instance = TOD_Sky.Instance;

                            if(instance)
                            {
                                if(_car)
                                {
                                    _car.SetFlag(BaseEntity.Flags.Reserved5, (instance.Cycle.Hour < 8 && instance.Cycle.Hour > 0));
                                }
                            }
                        }, 5f, 5f);
                    }
                    else _car.SetFlag(BaseEntity.Flags.Reserved5, true);
                }
            }

            public virtual void AssignCar(ModularCar car, Vector3[] path, DriveSide side, Configuration.DriverPreset driverPreset, Configuration.CarPreset carPreset, string carPresetName, float stuckTime, TrafficCompanion companion, List<BasePlayer> passengers) 
            {
                CarPresetName = carPresetName;
                _companion = companion;

                _stuckTime = stuckTime;
                _car = car;

                _passengers = passengers;

                _side = side;
                _path = path;

                _carPreset = carPreset;
                _driverPreset = driverPreset;
                
                _currentPathIndex = ((int)_side) > 0 ? 0 : _path.Length - 1;
            }

            public void Destroy()
            {
                if(_isDestroyed) return;

                Interface.CallHook("OnTrafficDestroyed");
                _isDestroyed = true;

                if(_car != null)
                {
                    var fuelSystem = _car.GetFuelSystem();

                    if(fuelSystem != null)
                    {
                        var fuelStorage = fuelSystem.fuelStorageInstance.Get(true);

                        if(fuelStorage != null)
                        {
                            if(fuelStorage.inventory != null) fuelStorage.inventory.Clear();

                            fuelStorage.dropsLoot = true;
                            fuelStorage.SetFlag(BaseEntity.Flags.Locked, false);
                            if(fuelStorage.inventory != null) fuelStorage.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                        }
                    }

                    if(_car.AttachedModuleEntities != null && _carPreset != null)
                    {
                        foreach(var module in _car.AttachedModuleEntities)
                        {
                            if(module is VehicleModuleEngine)
                            {
                                var engine = module as VehicleModuleEngine;

                                if(engine != null)
                                {
                                    var container = engine.GetContainer();

                                    if(container != null)
                                    {
                                        if(container.inventory != null)
                                        {
                                            if(_carPreset.blockEngineLooting) container.inventory.SetLocked(false);
                                            if(_carPreset.destroyEngineParts && container.inventory.itemList != null) 
                                            {
                                                for (int i = container.inventory.itemList.Count - 1; i >= 0; i--) 
                                                {
                                                    container.inventory.Remove(container.inventory.itemList[i]);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if(_carPreset != null)
                    {
                        if(_carPreset.destroyCarAfterDeath)
                        {
                            _car.Kill();

                            return;
                        }
                        else 
                        {
                            if(_carPreset.carHackableCrate.spawnCrate && !_car.IsDestroyed)
                            {
                                HackableLockedCrate crate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", _car.transform.position + _carPreset.carHackableCrate.posOffset) as HackableLockedCrate;
                                crate.Spawn();

                                if(_carPreset.carHackableCrate.enableCustomLoot)
                                {
                                    crate.inventory.Clear();

                                    foreach(var loot in _carPreset.carHackableCrate.loot)
                                    {
                                        if(UnityEngine.Random.Range(0, 100) < loot.chance)
                                        {
                                            Item item = ItemManager.CreateByName(loot.shortname, (int)loot.amount.Randomize(), loot.skin);

                                            if(item == null) continue;
                                            if(!string.IsNullOrEmpty(loot.name)) item.name = loot.name;

                                            crate.inventory.Insert(item);
                                        }
                                    }
                                }
                            }

                            if(_carPreset.detonator.enableDetonator && !_car.IsDestroyed)
                            {
                                TimedExplosive detonator = GameManager.server.CreateEntity("assets/prefabs/tools/c4/explosive.timed.deployed.prefab", _car.transform.position, Quaternion.identity) as TimedExplosive;
                            
                                if(detonator != null)
                                {
                                    detonator.Spawn();
                                    detonator.DoStick(_car.transform.position + _carPreset.detonator.offset, Vector3.zero, _car, null);
                                    
                                    detonator.SetFuse(_carPreset.detonator.timer);
                                    detonator.Invoke(new System.Action(() =>
                                    {
                                        if(_car != null)
                                        {
                                            if(!_car.IsDestroyed)
                                            {
                                                FullDestroy();
                                            }
                                        }
                                    }), _carPreset.detonator.timer);

                                    detonator.InvokeRepeating(new System.Action(() =>
                                    {
                                        if(_car == null)
                                        {
                                            FullDestroy();
                                            detonator.Kill();
                                        }
                                        else if(_car.IsDestroyed)
                                        {
                                            FullDestroy();
                                            detonator.Kill();
                                        }
                                    }), 1f, 1f);
                                }
                            }
                        }
                    }

                    _car.RemoveLock();
                }
            }

            public virtual void OnAttacked(BaseEntity attacker)
            {
                if(_companion) _companion.AddAttacker(attacker.net.ID.Value);
                if(_driverPreset.attackBehaviour == 3) _lastIncreaseSpeedTime = UnityEngine.Time.realtimeSinceStartup + 10;
            }

            public virtual void OnPhysicAttacked() => _lastObstacleTime = UnityEngine.Time.realtimeSinceStartup + 3;

            public virtual void Update()
            {
                if(_car == null || Driver == null)
                {
                    Destroy();

                    return;
                }
                else if(!Driver.isMounted || _car.IsDestroyed)
                {
                    Destroy();

                    return;
                }

                if(_lastDestinationTime + _stuckTime < Time.realtimeSinceStartup)
                {
                    foreach(var module in _car.AttachedModuleEntities)
                    {
                        if(module is VehicleModuleStorage)
                        {
                            var storage = module as VehicleModuleStorage;
                            var container = storage.GetContainer();

                            for (int i = container.inventory.itemList.Count - 1; i >= 0; i--)
                            {
                                var item = container.inventory.itemList[i];
                                container.inventory.Remove(item);
                            }
                        }
                    }

                    FullDestroy();

                    return;
                }
                else if(_lastDestinationTime + (_stuckTime / 3) < Time.realtimeSinceStartup && _lastObstacleTime < Time.realtimeSinceStartup && _lastTimeBackwards < Time.realtimeSinceStartup)
                {
                    _lastTimeBackwards = Time.realtimeSinceStartup + 10;
                    OnPhysicAttacked();
                }

                Vector3 destination = Vector3.zero;

                if(_side == DriveSide.Left) destination = _path[_currentPathIndex] + new Vector3(_carPreset.roadEdgeOffset, 1, -4.5f * (int)TrafficDriver.DriveSide.Left - 2);
                else destination = _path[_currentPathIndex] + new Vector3(_carPreset.roadEdgeOffset * -1f, 1, -3 * (int)TrafficDriver.DriveSide.Right + 2);

                if(Vector3.Distance(_car.transform.position, destination) < 10f) 
                {
                    _lastDestinationTime = Time.realtimeSinceStartup;
                    _currentPathIndex += (int)_side;

                    if(_side == DriveSide.Left)
                    {
                        if(_currentPathIndex < 0) 
                        {
                            _currentPathIndex = 0;
                            _side = DriveSide.Right;
                        }
                    }
                    else 
                    {
                        if(_currentPathIndex > _path.Length - 1) 
                        {
                            _currentPathIndex = _path.Length - 1;
                            _side = DriveSide.Left;
                        }
                    }
                }

                Movement verticalMovement = Movement.Idle, horizontalMovement = Movement.Idle;  

                Vector3 lhs = BradleyAPC.Direction2D(destination, _car.transform.position);
                float dotRight = Vector3.Dot(lhs, _car.transform.right);

                var turning =  Vector3.Dot(lhs, -_car.transform.forward) <= dotRight ? Mathf.Clamp(dotRight * 3f, -1f, 1f) : (dotRight < Vector3.Dot(lhs, -_car.transform.right) ? -1f : 1f);
                var throttle = (0.1f + Mathf.InverseLerp(0f, 20f, Vector3.Distance(_car.transform.position, destination)) * 1) * (1f - Mathf.InverseLerp(0f, 0.3f, Mathf.Abs(turning))) + Mathf.InverseLerp(0.1f, 0.4f, Vector3.Dot(_car.transform.forward, Vector3.up));
                
                if(_lastObstacleTime > UnityEngine.Time.realtimeSinceStartup) verticalMovement = Movement.Backward;
                else if(GetSpeed() < _carPreset.maxSpeed) verticalMovement = Movement.Forward;

                if(verticalMovement != Movement.Backward)
                {
                    if(turning < -0.6f) horizontalMovement = Movement.Left;
                    else if(turning > 0.6f) horizontalMovement = Movement.Right;
                }

                _car.PlayerServerInput(new InputState
                {
                    current = new InputMessage
                    {
                        buttons = (int)(verticalMovement | horizontalMovement)
                    }
                }, Driver);
            }

            private float GetSpeed() => _car.GetSpeed() - ((_lastIncreaseSpeedTime > UnityEngine.Time.realtimeSinceStartup) ? 5 : 0);

            public void FullDestroy()
            {
                Interface.CallHook("OnTrafficDestroyed");

                if(Driver != null) Driver.Kill();

                if(_car != null)
                {
                    var fuelSystem = _car.GetFuelSystem();

                    if(fuelSystem != null)
                    {
                        var fuelStorage = fuelSystem.fuelStorageInstance.Get(true);

                        if(fuelStorage != null)
                        {
                            if(fuelStorage.inventory != null) fuelStorage.inventory.Clear();

                            fuelStorage.dropsLoot = true;
                            fuelStorage.SetFlag(BaseEntity.Flags.Locked, false);
                            if(fuelStorage.inventory != null) fuelStorage.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                        }
                    }

                    if(_car.mountPoints?.Count != 0)
                    {
                        foreach(var mountPoint in _car.mountPoints)
                        {
                            if(mountPoint.mountable != null)
                            {
                                var mount = mountPoint.mountable;

                                if(mount.AnyMounted())
                                {
                                    var mounted = mount.GetMounted();

                                    if(mounted != null)
                                    {
                                        mounted.Kill();
                                    }
                                }
                            }
                        }
                    }

                    if(_car.AttachedModuleEntities != null && _carPreset != null)
                    {
                        foreach(var module in _car.AttachedModuleEntities)
                        {
                            if(module is VehicleModuleEngine)
                            {
                                var engine = module as VehicleModuleEngine;

                                if(engine != null)
                                {
                                    var container = engine.GetContainer();

                                    if(container != null)
                                    {
                                        if(container.inventory != null)
                                        {
                                            if(_carPreset.blockEngineLooting) container.inventory.SetLocked(false);
                                            if(_carPreset.destroyEnginePartsAtFullDespawn && container.inventory.itemList != null) 
                                            {
                                                for (int i = container.inventory.itemList.Count - 1; i >= 0; i--) 
                                                {
                                                    container.inventory.Remove(container.inventory.itemList[i]);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    _car.Kill();
                }

                if(_companion) _companion.Kill();
                
                if(_passengers?.Count != 0)
                {
                    foreach(var passenger in _passengers) 
                    {
                        if(passenger != null) passenger.Kill();
                    }
                }

                Destroy(this);
            }
        }

        public class TrafficPassenger : MonoBehaviour
        {
            public bool ShouldDropBag;
        }

        #endregion

        #region Спавнер

        public class SpawnManager : FacepunchBehaviour
        {
            private Configuration.Limits _limits;
            private TrafficDrivers _pluginInstance;

            private List<Vector3[]> _roads;
            private List<TrafficDriver> _currentDrivers = new List<TrafficDriver>();

            private bool _isPaused = false;

            public virtual List<TrafficDriver> GetDrivers() 
            {
                for(int i = _currentDrivers.Count - 1; i >= 0; i--)
                {
                    if(_currentDrivers[i] == null) _currentDrivers.RemoveAt(i);
                }

                return _currentDrivers;
            }

            public virtual void Init(Configuration.Limits limits, TrafficDrivers plugin, List<Vector3[]> roads)
            {
                _roads = roads;
                _limits = limits;
                _pluginInstance = plugin;

                int cooldown = 0;

                if(plugin.SpawnModularCar == null) return;

                for(int i = 0; i < (int)_limits.maxCars.Randomize(); i++)
                {
                    if(cooldown != 0)
                    {
                        Invoke("DelayedSpawn", cooldown);

                        cooldown += limits.spawnCooldown;
                    }
                    else 
                    {
                        TrafficDriver driver = _pluginInstance.Call<TrafficDriver>("SpawnTrafficCar");
                        _currentDrivers.Add(driver);

                        cooldown += limits.spawnCooldown;
                    }
                }

                Debug.Log($"[Traffic Drivers] Cars in spawn queue: {cooldown / limits.spawnCooldown}");
            }

            private void DelayedSpawn() 
            {
                if(_isPaused) return;

                _currentDrivers.Add(_pluginInstance.Call<TrafficDriver>("SpawnTrafficCar"));
            }

            public virtual void OnTrafficDestroyed()
            {
                if(_isPaused) return;

                Invoke(() =>
                {                    
                    for(int i = 0; i < (_roads.Count == 1 ? 1 : 50); i++)
                    {
                        var road = _roads[UnityEngine.Random.Range(0, _roads.Count)];
                        
                        Vector3 startPos = road[(UnityEngine.Random.Range(0, 50) > 50 ? 0 : road.Length - 1)];

                        List<BasePlayer> vis = new List<BasePlayer>();
                        Vis.Entities(startPos, 20f, vis, LayerMask.GetMask("Player (Server)"));

                        if(vis?.Count != 0)
                        {
                            if(HasConnectedPlayers(vis))
                            {
                                startPos = road[road.Length - 1];
                                Vis.Entities(startPos, 20f, vis, LayerMask.GetMask("Player (Server)"));

                                if(HasConnectedPlayers(vis)) continue;
                            }
                        }

                        _currentDrivers.Add(_pluginInstance.Call<TrafficDriver>("SpawnTrafficCarParams", road, startPos, road[0] == startPos ? TrafficDriver.DriveSide.Right : TrafficDriver.DriveSide.Left));

                        break;
                    }
                }, _limits.spawnCooldown);
            }

            public bool HasConnectedPlayers(List<BasePlayer> list)
            {
                foreach(var player in list)
                {
                    if(player == null) continue;
                    if(player.IsConnected && !player.IsSleeping()) return true;
                }

                return false;
            }

            public virtual void Resume(bool callInit = true)
            {
                _isPaused = false;

                Init(_limits, _pluginInstance, _roads);
            }

            public virtual void Pause()
            {
                _isPaused = true;
            }
        }

        #endregion

        #region API

        private bool IsPlayerTrafficDriver(BasePlayer player)
        {
            if(player == null || player?.net == null) return false;

            var driver = GetDriver(player.net.ID.Value);
            return driver != null;
        }

        private bool IsPlayerTrafficCompanion(BasePlayer player)
        {
            if(player == null || player?.net == null) return false;

            var companion = GetCompanion(player.net.ID.Value);
            return companion?.NPC != null;
        }

        private bool IsTrafficVehicle(BaseModularVehicle vehicle)
        {
            if(vehicle == null) return false;

            var driver = vehicle.GetDriver();
            if(driver == null) return false;

            return IsPlayerTrafficDriver(driver);
        }

        #endregion
    }
}