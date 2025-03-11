/*
                                   /\  /\  /\
     TTTTT  H   H  EEEE     K  K  |  \/  \/  |  NN   N   GGG
       T    H   H  E        K K   *----------*  N N  N  G
       T    HHHHH  EEE      KK     I  I  I  I   N  N N  G  GG
       T    H   H  E        K K    I  I  I  I   N   NN  G   G
       T    H   H  EEEE     K  K   I  I  I  I   N    N   GGG


This plugin (the software) is © copyright the_kiiiing.

You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without explicit consent from the_kiiiing.

DISCLAIMER:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using PluginComponents.HeliSpeed;
using PluginComponents.HeliSpeed.Core;
using PluginComponents.HeliSpeed.Extensions.BaseNetworkable;
using PluginComponents.HeliSpeed.Extensions.Reflection;
using PluginComponents.HeliSpeed.LoottableApi.Static;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VLB;

/*
 * CHANGELOG:
 * 
 *  V 1.1.0
 *  - added support for boats and modular cars
 * 
 *  V 1.1.1
 *  - fix modular cars not working
 *  - reset speed to vanilla on unload
 * 
 *  V 1.1.2
 *  - add support for tugboats
 *  - misc refactoring
 *  
 *  V 1.1.3
 *  - fix for Sept 07 rust update
 *  - add universal command for fuel
 *  - allow fuel command to target players
 *  - add support for attack helicopter
 *  
 *  V 1.1.4
 *  - add support for horses
 *  - add support for submarines
 * 
 *  V 1.1.5
 *  - fix for July 4th rust update
 *  
 *  V 1.2.0
 *  - misc refactoring
 *  - add support for motorcycles
 * 
 * V 1.2.1
 * - remove modular car debug message
 *
 * V 1.2.2
 * - add null checks
 * - update Loottable api
 *
 * V 1.2.3
 * - add support for skidoo
 * 
 */

namespace Oxide.Plugins
{
    [Info(nameof(HeliSpeed), "The_Kiiiing", "1.2.3")]
    internal class HeliSpeed : BasePlugin<HeliSpeed, HeliSpeed.Configuration>
    {
        #region Fields

        [Perm]
        private const string PERM_ADMIN = "helispeed.admin";

        private const int FUEL_ITEM_ID = -946369541;

        public enum VehicleType { MiniCopter, ScrapHeli, RowBoat, RHIB, ModularCar, Tugboat, AttackHelicopter, SubSolo, SubDuo, Bike, Skidoo }

        #endregion

        #region Config

        public class Configuration
        {
            [JsonProperty("Horse speed multiplier")]
            public float horseSpeedMultiplier = 1;

            [JsonProperty(PropertyName = "Fuel Configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<FuelConfig> fuelConfigs = new()
            {
                new FuelConfig { displayName = null, skinId = 0 }.SetValues(0.8f, -1),
                new FuelConfig { displayName = "Medium Grade Fuel", skinId = 2501207890 }.SetValues(1.5f, -1),
                new FuelConfig { displayName = "High Quality Fuel", skinId = 2664651800 }.SetValues(3f, -1)
            };

            public FuelConfig GetFuelConfig(int index)
            {
                return index < fuelConfigs.Count ? fuelConfigs[index] : null;
            }

            public FuelConfig GetFuelConfig(ulong skin)
            {
                return fuelConfigs.Find(x => x.skinId == skin);
            }
        }

        public class FuelConfig
        {
            [JsonProperty("Item name")]
            public string displayName;
            [JsonProperty("Fuel skin id")]
            public ulong skinId;
            [JsonProperty("Minicopter speed multiplier")]
            public float miniSpeedMultiplier;
            [JsonProperty("Minicopter fuel consumption per minute (-1 for default)")]
            public float miniFuelPerMinute;
            [JsonProperty("Scap heli speed multiplier")]
            public float scrapSpeedMultiplier;
            [JsonProperty("Scap heli consumption per minute (-1 for default)")]
            public float scrapFuelPerMinute;
            [JsonProperty("Row boat speed multiplier")]
            public float boatSpeedMultiplier;
            [JsonProperty("Row boat fuel consumption per minute (-1 for default)")]
            public float boatFuelPerMinute;
            [JsonProperty("RHIB speed multiplier")]
            public float rhibSpeedMultiplier;
            [JsonProperty("RHIB fuel consumption per minute (-1 for default)")]
            public float rhibFuelPerMinute;
            [JsonProperty("Modular car speed multiplier")]
            public float carSpeedMultiplier;
            [JsonProperty("Tugboat speed multiplier")]
            public float tugSpeedMultiplier = 1;
            [JsonProperty("Tugboat fuel consumption per minute (-1 for default)")]
            public float tugFuelPerMinute = -1;
            [JsonProperty("Attack helicopter speed multiplier")]
            public float attackSpeedMultiplier = 1;
            [JsonProperty("Attack helicopter fuel consumption per minute (-1 for default)")]
            public float attackFuelPerMinute = -1;
            [JsonProperty("Solo submarine speed multiplier")]
            public float soloSubSpeedMpl = 1;
            [JsonProperty("Solo submarine fuel consumption per minute (-1 for default)")]
            public float soloSubFuelPerMinute = -1;
            [JsonProperty("Duo submarine speed multiplier")]
            public float duoSubSpeedMpl = 1;
            [JsonProperty("Duo submarine fuel consumption per minute (-1 for default)")]
            public float duoSubFuelPerMinute = -1;
            [JsonProperty("Motor bike speed multiplier")]
            public float bikeSpeedMpl = 1;
            [JsonProperty("Motor bike fuel consumption per minute (-1 for default)")]
            public float bikeFuelPerMinute = -1;
            [JsonProperty("Diver propulsion speed multiplier")]
            public float skidooSpeedMpl = 1;
            [JsonProperty("Diver propulsion fuel consumption per minute (-1 for default)")]
            public float skidooFuelPerMinute = -1;

            public (float speed, float fuel) GetSpeedAndFuel(VehicleType vehicleType)
            {
                return vehicleType switch
                {
                    VehicleType.MiniCopter => (miniSpeedMultiplier, miniFuelPerMinute),
                    VehicleType.ScrapHeli => (scrapSpeedMultiplier, scrapFuelPerMinute),
                    VehicleType.RowBoat => (boatSpeedMultiplier, boatFuelPerMinute),
                    VehicleType.RHIB => (rhibSpeedMultiplier, rhibFuelPerMinute),
                    VehicleType.ModularCar => (carSpeedMultiplier, -1f),
                    VehicleType.Tugboat => (tugSpeedMultiplier, tugFuelPerMinute),
                    VehicleType.AttackHelicopter => (attackSpeedMultiplier, attackFuelPerMinute),
                    VehicleType.SubSolo => (soloSubSpeedMpl, soloSubFuelPerMinute),
                    VehicleType.SubDuo => (duoSubSpeedMpl, duoSubFuelPerMinute),
                    VehicleType.Bike => (bikeSpeedMpl, bikeFuelPerMinute),
                    VehicleType.Skidoo => (skidooSpeedMpl, skidooFuelPerMinute),
                    _ => (-1f, -1f),
                };
            }

            public FuelConfig SetValues(float speed, float fuel)
            {
                miniSpeedMultiplier = speed;
                scrapSpeedMultiplier = speed;
                boatSpeedMultiplier = speed;
                rhibSpeedMultiplier = speed;
                carSpeedMultiplier = speed;
                tugSpeedMultiplier = speed;
                attackSpeedMultiplier = speed;
                soloSubSpeedMpl = speed;
                duoSubSpeedMpl = speed;
                bikeSpeedMpl = speed;
                skidooSpeedMpl = speed;

                miniFuelPerMinute = fuel;
                scrapFuelPerMinute = fuel;
                boatFuelPerMinute = fuel;
                rhibFuelPerMinute = fuel;
                tugFuelPerMinute = fuel;
                attackFuelPerMinute = fuel;
                soloSubFuelPerMinute = fuel;
                duoSubFuelPerMinute = fuel;
                bikeFuelPerMinute = fuel;
                skidooFuelPerMinute = fuel;

                return this;
            }

        }

        #endregion

        #region Commands

        [UniversalCommand("fuel", Permission = PERM_ADMIN)]
        private void CmdFuel(IPlayer iPlayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(iPlayer.Id, PERM_ADMIN))
            {
                iPlayer.Reply("You don't have permission to do that");
                return;
            }

            if (args.Length < 1)
            {
                iPlayer.Reply("Invalid syntax. Usage: '/fuel <id> <amount> <player?>' or '/fuel list'");
                return;
            }

            if (args[0] == "list")
            {
                var sb = new StringBuilder("Fuel index:\n");
                for (int i = 0; i < Config.fuelConfigs.Count; i++)
                {
                    var config = Config.fuelConfigs[i];
                    sb.AppendLine($"{i:D2} {config.displayName ?? "default"}");
                }
                iPlayer.Reply(sb.ToString());
                return;
            }

            BasePlayer player = args.Length == 3 ? BasePlayer.Find(args[2]) : iPlayer.Object as BasePlayer;
            if (player == null)
            {
                iPlayer.Reply(args.Length == 3 ? $"No player found with name or id {args[2]}" : "Invalid player. Please specify a target player explicitly");
                return;
            }

            if (args.Length >= 2 && Int32.TryParse(args[0], out int idx) && Int32.TryParse(args[1], out int amt))
            {
                amt = Mathf.Clamp(amt, 1, 10000000);

                var config = Config.GetFuelConfig(idx);
                if (config == null)
                {
                    iPlayer.Reply("Invalid index. Use '/fuel list' to see view indexes");
                    return;
                }

                var fuel = CreateFuelItem(config, amt);
                fuel.MoveToContainer(player.inventory.containerMain);
                iPlayer.Reply($"{player.displayName} received {config.displayName} x{amt}");
            }
        }

        #endregion

        #region Hooks

        protected override void OnServerInitialized()
        {
            base.OnServerInitialized();

            AddAllSpeedControllers<PlayerHelicopter, HeliSpeedController>();
            AddAllSpeedControllers<MotorRowboat, BoatSpeedController>();
            AddAllSpeedControllers<ModularCar, CarSpeedController>();
            AddAllSpeedControllers<BaseSubmarine, SubmarineSpeedController>();
            AddAllSpeedControllers<RidableHorse, HorseSpeedController>();
            AddAllSpeedControllers<Bike, BikeSpeedController>();
            AddAllSpeedControllers<DiverPropulsionVehicle, DiverPropulsionSpeedController>();
        }

        protected override void OnServerInitializedDelayed()
        {
            base.OnServerInitializedDelayed();

            foreach (var fuel in Config.fuelConfigs)
            {
                LoottableApi.AddCustomItem(this, FUEL_ITEM_ID, fuel.skinId, fuel.displayName);
            }
        }

        protected override void Unload()
        {
            DestroyAllSpeedControllers<PlayerHelicopter, HeliSpeedController>();
            DestroyAllSpeedControllers<MotorRowboat, BoatSpeedController>();
            DestroyAllSpeedControllers<ModularCar, CarSpeedController>();
            DestroyAllSpeedControllers<BaseSubmarine, SubmarineSpeedController>();
            DestroyAllSpeedControllers<RidableHorse, HorseSpeedController>();
            DestroyAllSpeedControllers<Bike, BikeSpeedController>();
            DestroyAllSpeedControllers<DiverPropulsionVehicle, DiverPropulsionSpeedController>();

            base.Unload();
        }

        [Hook]
        void OnEntitySpawned(PlayerHelicopter heli)
        {
            AddSpeedController<HeliSpeedController>(heli);
        }

        [Hook]
        void OnEntitySpawned(MotorRowboat boat)
        {
            AddSpeedController<BoatSpeedController>(boat);
        }

        [Hook]
        void OnEntitySpawned(ModularCar car)
        {
            AddSpeedController<CarSpeedController>(car);
        }

        [Hook]
        void OnEntitySpawned(BaseSubmarine sub)
        {
            AddSpeedController<SubmarineSpeedController>(sub);
        }

        [Hook]
        void OnEntitySpawned(Bike bike)
        {
            if (bike != null && bike.poweredBy == Bike.PoweredBy.Fuel)
            {
                AddSpeedController<BikeSpeedController>(bike);
            }
        }
        
        [Hook]
        void OnEntitySpawned(DiverPropulsionVehicle skidoo)
        {
            AddSpeedController<DiverPropulsionSpeedController>(skidoo);
        }

        #endregion

        #region Methods

        private static Item CreateFuelItem(FuelConfig config, int amount)
        {
            var item = ItemManager.CreateByItemID(-946369541, amount, config.skinId);
            if (config.displayName != null)
            {
                item.name = config.displayName;
            }

            return item;
        }

        private static void DestroyAllSpeedControllers<TEntity, TSpeedController>() where TEntity : BaseEntity where TSpeedController : MonoBehaviour
        {
            foreach(var ent in BaseNetworkable.serverEntities.OfType<TEntity>())
            {
                if (ent.IsNullOrDestroyed())
                {
                    continue;
                }
                
                var controller = ent.GetComponent<TSpeedController>();
                if (controller != null)
                {
                    UnityEngine.Object.Destroy(controller);
                }
            }
        }

        private static void AddAllSpeedControllers<TEntity, TSpeedController>() where TEntity : BaseEntity where TSpeedController : MonoBehaviour
        {
            foreach (var ent in BaseNetworkable.serverEntities.OfType<TEntity>())
            {
                AddSpeedController<TSpeedController>(ent);
            }
        }

        private static void AddSpeedController<TSpeedController>(BaseEntity entity) where TSpeedController : MonoBehaviour
        {
            if (entity.IsNullOrDestroyed())
            {
                return;
            }
            
            var controller = entity.GetOrAddComponent<TSpeedController>();
            if (controller is BaseSpeedController sc && !sc.IsInvoking(sc.CheckConfig))
            {
                sc.CheckConfig();
            }
        }

        #endregion

        #region Mono Behavior

        private class DiverPropulsionSpeedController : BaseSpeedController<DiverPropulsionVehicle>
        {
            private float initialEngineKw;
            private float initialFuelPerSec;

            protected override void Awake()
            {
                base.Awake();
                
                vehicleType = VehicleType.Skidoo;

                initialFuelPerSec = entity.GetField<float>("maxFuelPerSec");
                initialEngineKw = entity.GetField<float>("engineKW");
            }

            protected override void AdjustFuelConsumption(float fuelPerMinute = -1)
            {
                float fps = fuelPerMinute / 60f;
                if (fps < 0)
                {
                    fps = initialFuelPerSec;
                }
                entity.SetField("maxFuelPerSec", fps);
            }

            protected override void AdjustSpeed(float m = 1)
            {
                entity.SetField("engineKW", initialEngineKw * m);
            }
            
            protected override IFuelSystem GetFuelSystem() => entity.GetFuelSystem();
        }

        private class BikeSpeedController : BaseSpeedController<Bike>
        {
            private int initialEngineKw;
            private float initialFuelPerSec;

            protected override void Awake()
            {
                base.Awake();

                vehicleType = VehicleType.Bike;

                initialFuelPerSec = entity.maxFuelPerSec;
                initialEngineKw = entity.engineKW;
            }

            protected override void AdjustFuelConsumption(float fuelPerMinute = -1)
            {
                float fps = fuelPerMinute / 60f;
                if (fps < 0)
                {
                    fps = initialFuelPerSec;
                }
                entity.maxFuelPerSec = fps;
            }

            protected override void AdjustSpeed(float m = 1)
            {
                entity.engineKW = Mathf.RoundToInt(initialEngineKw * m);
            }

            protected override IFuelSystem GetFuelSystem() => entity.GetFuelSystem();
        }

        private class HorseSpeedController : MonoBehaviour
        {
            private RidableHorse horse;

            private float initialMaxSpeed;
            private float initialRunSpeed;
            private float initialWalkSpeed;
            private float initialTrotSpeed;
            private float initialTurnSpeed;

            void Awake()
            {
                horse = GetComponent<RidableHorse>();

                initialMaxSpeed = horse.maxSpeed;
                initialRunSpeed = horse.runSpeed;
                initialWalkSpeed = horse.walkSpeed;
                initialTrotSpeed = horse.trotSpeed;
                initialTurnSpeed = horse.turnSpeed;

                AdjustSpeed(Config.horseSpeedMultiplier);
            }

            void OnDestroy()
            {
                AdjustSpeed(1);
            }

            public void AdjustSpeed(float m)
            {
                horse.maxSpeed = initialMaxSpeed * m;
                horse.runSpeed = initialRunSpeed * m;
                horse.walkSpeed = initialWalkSpeed * m;
                horse.trotSpeed = initialTrotSpeed * m;
                horse.turnSpeed = initialTurnSpeed * m;
            }
        }

        private class SubmarineSpeedController : BaseSpeedController<BaseSubmarine>
        {
            private float initialEngineKW;
            private float initialMaxFuelPerSec;

            protected override void Awake()
            {
                base.Awake();

                vehicleType = entity is SubmarineDuo ? VehicleType.SubDuo : VehicleType.SubSolo;

                initialEngineKW = entity.engineKW;
                initialMaxFuelPerSec = entity.maxFuelPerSec;
            }

            protected override void AdjustFuelConsumption(float fuelPerMinute = -1)
            {
                float fps = fuelPerMinute / 60f;
                if (fps >= 0)
                {
                    entity.maxFuelPerSec = fps;
                }
                else
                {
                    entity.maxFuelPerSec = initialMaxFuelPerSec;
                }
            }

            protected override void AdjustSpeed(float m = 1)
            {
                entity.engineKW = initialEngineKW * m;
            }

            protected override IFuelSystem GetFuelSystem() => entity.GetFuelSystem();
        }

        private class CarSpeedController : BaseSpeedController<ModularCar>
        {
            private Dictionary<NetworkableId, int> initialKw;

            protected override void Awake()
            {
                base.Awake();

                vehicleType = VehicleType.ModularCar;
                initialKw = new Dictionary<NetworkableId, int>();
            }

            protected override void AdjustFuelConsumption(float fuelPerMinute = -1f) { }

            protected override void AdjustSpeed(float m = 1)
            {
                foreach(var engine in entity.AttachedModuleEntities.OfType<VehicleModuleEngine>())
                {
                    int kw = GetInitialKw(engine);
                    int newKw = Mathf.RoundToInt(kw * m);

                    LogDebug($"adjust engine speed {m:N2} initial {kw}kw now {newKw}kw");
                    engine.engine.engineKW = newKw;
                }
            }

            protected override IFuelSystem GetFuelSystem() => entity.GetFuelSystem();

            private int GetInitialKw(VehicleModuleEngine engine)
            {
                if (!initialKw.TryGetValue(engine.ID, out var kw))
                {
                    kw = engine.engine.engineKW;
                    initialKw[engine.ID] = kw;
                }

                return kw;
            }
        }

        private class BoatSpeedController : BaseSpeedController<MotorRowboat>
        {
            private float engineThrustInitial;
            private float defaultFuelPerSec;

            protected override void Awake()
            {
                base.Awake();

                vehicleType = entity is RHIB ? VehicleType.RHIB : entity is Tugboat ? VehicleType.Tugboat : VehicleType.RowBoat;

                defaultFuelPerSec = entity.fuelPerSec;
                engineThrustInitial = entity.engineThrust;
            }

            protected override void AdjustFuelConsumption(float fuelPerMinute = -1)
            {
                var fps = fuelPerMinute / 60f;
                if (fps < 0) 
                { 
                    fps = defaultFuelPerSec; 
                }

                entity.fuelPerSec = fps;
            }

            protected override void AdjustSpeed(float m = 1)
            {
                entity.engineThrust = engineThrustInitial * m;
            }

            protected override IFuelSystem GetFuelSystem() => entity.GetFuelSystem();
        }

        private class HeliSpeedController : BaseSpeedController<PlayerHelicopter>
        {
            private float initialEngineThrustMax;
            private float initialFuelPerSec;

            protected override void Awake()
            {
                base.Awake();

                vehicleType = VehicleType.MiniCopter;

                if (entity is ScrapTransportHelicopter)
                {
                    vehicleType = VehicleType.ScrapHeli;
                }
                else if (entity is AttackHelicopter)
                {
                    vehicleType = VehicleType.AttackHelicopter;
                }

                initialEngineThrustMax = entity.engineThrustMax;
                initialFuelPerSec = entity.fuelPerSec;
            }

            protected override void AdjustFuelConsumption(float fuelPerMinute = -1f)
            {
                var fps = fuelPerMinute / 60f;
                if (fps < 0)
                {
                    fps = initialFuelPerSec;
                }

                entity.fuelPerSec = fps;
            }

            protected override void AdjustSpeed(float m = 1f)
            {
                if (!Mathf.Approximately(m, 1f))
                {
                    LogDebug($"adjust {vehicleType} speed to {m:N2}");
                }

                entity.engineThrustMax = initialEngineThrustMax * m;
            }

            protected override IFuelSystem GetFuelSystem() => entity.GetFuelSystem();

        }

        private abstract class BaseSpeedController<TEntity> : BaseSpeedController where TEntity : BaseEntity
        {
            protected TEntity entity;

            public VehicleType vehicleType;

            protected EntityFuelSystem fuelSystem;
            protected ulong? fuelSkin = null;

            protected virtual void Awake()
            {
                entity = GetComponent<TEntity>();

                if (entity == null || entity.IsDestroyed)
                {
                    Destroy(entity);
                    return;
                }

                InvokeRandomized(CheckConfig, 2f, 6f, 2f);
            }

            private void OnDestroy()
            {
                CancelInvoke(CheckConfig);

                try
                {
                    AdjustSpeed(1);
                    AdjustFuelConsumption(-1);
                }
                // Throws when entity is being destroyed
                catch (NullReferenceException) { }
            }

            public sealed override void CheckConfig()
            {
                fuelSystem ??= GetFuelSystem() as EntityFuelSystem;
                if (fuelSystem == null)
                {
                    LogDebug($"Failed to get fuel system for {entity?.ShortPrefabName}");
                    Destroy();
                    return;
                }

                var fuel = fuelSystem.GetFuelItem();
                if (fuel != null && fuel.skin != fuelSkin)
                {
                    fuelSkin = fuel.skin;
                    var cfg = Config.GetFuelConfig(fuelSkin.Value);
                    if (cfg != null)
                    {
                        var (speedMultiplier, fuelPerMinute) = cfg.GetSpeedAndFuel(vehicleType);

                        AdjustSpeed(speedMultiplier);
                        AdjustFuelConsumption(fuelPerMinute);
                    }
                    else
                    {
                        AdjustSpeed();
                        AdjustFuelConsumption();
                    }
                }
            }

            protected abstract void AdjustFuelConsumption(float fuelPerMinute = -1f);

            protected abstract void AdjustSpeed(float m = 1f);

            protected abstract IFuelSystem GetFuelSystem();
        }

        private abstract class BaseSpeedController : FacepunchBehaviour
        {
            public void Destroy() => Destroy(this);

            public abstract void CheckConfig();
        }

        #endregion
    }
}
namespace PluginComponents.HeliSpeed{using JetBrains.Annotations;using Oxide.Plugins;using System;[AttributeUsage(AttributeTargets.Field,AllowMultiple=false),MeansImplicitUse]public sealed class PermAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,AllowMultiple=false),MeansImplicitUse]public sealed class UniversalCommandAttribute:Attribute{public UniversalCommandAttribute(string name){Name=name;}public string Name{get;set;}public string Permission{get;set;}}[AttributeUsage(AttributeTargets.Method),MeansImplicitUse]public sealed class HookAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,Inherited=false)]public sealed class DebugAttribute:Attribute{}public class MinMaxInt{public int min;public int max;public MinMaxInt(){}public MinMaxInt(int value):this(value,value){}public MinMaxInt(int min,int max){this.min=min;this.max=max;}public int Random(){return UnityEngine.Random.Range(min,max+1);}}}namespace PluginComponents.HeliSpeed.Core{using Oxide.Core.Plugins;using Oxide.Core;using Oxide.Plugins;using Newtonsoft.Json;using System.IO;using UnityEngine;using System;using System.Diagnostics;using System.Collections.Generic;using System.Linq;using Facepunch.Extend;using System.Reflection;using PluginComponents.HeliSpeed;public abstract class BasePlugin<TPlugin,TConfig>:BasePlugin<TPlugin>where TConfig:class,new()where TPlugin:RustPlugin{protected new static TConfig Config{get;private set;}private string ConfigPath=>Path.Combine(Interface.Oxide.ConfigDirectory,$"{Name}.json");protected override void LoadConfig()=>ReadConfig();protected override void SaveConfig()=>WriteConfig();protected override void LoadDefaultConfig()=>Config=new TConfig();private void ReadConfig(){if(File.Exists(ConfigPath)){Config=JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(ConfigPath));if(Config==null){LogError("[CONFIG] Your configuration file contains an error. Using default configuration values.");LoadDefaultConfig();}}else{LoadDefaultConfig();}WriteConfig();}private void WriteConfig(){var directoryName=Utility.GetDirectoryName(ConfigPath);if(directoryName!=null&&!Directory.Exists(directoryName)){Directory.CreateDirectory(directoryName);}if(Config!=null){string text=JsonConvert.SerializeObject(Config,Formatting.Indented);File.WriteAllText(ConfigPath,text);}else{LogError("[CONFIG] Saving failed - config is null");}}}public abstract class BasePlugin<TPlugin>:BasePlugin where TPlugin:RustPlugin{public new static TPlugin Instance{get;private set;}protected static string DataFolder=>Path.Combine(Interface.Oxide.DataDirectory,typeof(TPlugin).Name);protected override void Init(){base.Init();Instance=this as TPlugin;}protected override void Unload(){Instance=null;base.Unload();}}public abstract class BasePlugin:RustPlugin{public const int OSI_DELAY=5;public const bool CARBONARA=
#if CARBON
true;
#else
false;
#endif
public const bool DEBUG=
#if DEBUG
true;
#else
false;
#endif
public static BasePlayer DebugPlayer=>DEBUG?BasePlayer.activePlayerList.FirstOrDefault(x=>!x.IsNpc):null;public static string PluginName=>Instance?.Name??"NULL";public static BasePlugin Instance{get;private set;}protected virtual UnityEngine.Color ChatColor=>default;protected virtual string ChatPrefix=>ChatColor!=default?$"<color=#{ColorUtility.ToHtmlStringRGB(ChatColor)}>[{Title}]</color>":$"[{Title}]";[HookMethod("Init")]protected virtual void Init(){Instance=this;foreach(var field in GetType().GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)){if(field.IsLiteral&&!field.IsInitOnly&&field.FieldType==typeof(string)&&field.HasAttribute(typeof(PermAttribute))){if(field.GetValue(null)is string perm){LogDebug($"Auto-registered permission '{perm}'");permission.RegisterPermission(perm,this);}}}foreach(var method in GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)){if(method.GetCustomAttributes(typeof(UniversalCommandAttribute),true).FirstOrDefault()is UniversalCommandAttribute attribute){var commandName=attribute.Name??method.Name.ToLower().Replace("cmd",string.Empty);if(attribute.Permission!=null){LogDebug($"Auto-registered command '{commandName}' with permission '{attribute.Permission??"<null>"}'");}else{LogDebug($"Auto-registered command '{commandName}'");}AddUniversalCommand(commandName,method.Name,attribute.Permission);}}}[HookMethod("Unload")]protected virtual void Unload(){Instance=null;}[HookMethod("OnServerInitialized")]protected virtual void OnServerInitialized(bool initial){if(!CARBONARA){OnServerInitialized();}timer.In(OSI_DELAY,OnServerInitializedDelayed);}
#if CARBON
[HookMethod("OnServerInitialized")]
#endif
protected virtual void OnServerInitialized(){}protected virtual void OnServerInitializedDelayed(){}public static void Log(string s){if(Instance!=null){Interface.Oxide.LogInfo($"[{Instance.Title}] {s}");}}[Conditional("DEBUG")]public static void LogDebug(string s){if(DEBUG&&Instance!=null){if(CARBONARA){LogWarning("[DEBUG] "+s);}else{Interface.Oxide.LogDebug($"[{Instance.Title}] {s}");}}}public static void LogWarning(string s){if(Instance!=null){Interface.Oxide.LogWarning($"[{Instance.Title}] {s}");}}public static void LogError(string s){if(Instance!=null){Interface.Oxide.LogError($"[{Instance.Title}] {s}");}}private Dictionary<string,CommandCallback>uiCallbacks;private string uiCommandBase;private void PrepareCommandHandler(){if(uiCallbacks==null){uiCallbacks=new();uiCommandBase=$"{Title.ToLower()}.uicmd";cmd.AddConsoleCommand(uiCommandBase,this,HandleCommand);}}private bool HandleCommand(ConsoleSystem.Arg arg){var cmd=arg.GetString(0);if(uiCallbacks.TryGetValue(cmd,out var callback)){var player=arg.Player();try{callback.ButtonCallback?.Invoke(player);callback.InputCallback?.Invoke(player,string.Join(' ',arg.Args?.Skip(1)??Enumerable.Empty<string>()));}catch(Exception ex){PrintError($"Failed to run UI command {cmd}: {ex}");}}return false;}public string CreateUiCommand(string guid,Action<BasePlayer>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}public string CreateUiCommand(string guid,Action<BasePlayer,string>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}private readonly struct CommandCallback{public readonly bool SingleUse;public readonly Action<BasePlayer>ButtonCallback;public readonly Action<BasePlayer,string>InputCallback;public CommandCallback(Action<BasePlayer>buttonCallback,bool singleUse){ButtonCallback=buttonCallback;InputCallback=null;SingleUse=singleUse;}public CommandCallback(Action<BasePlayer,string>inputCallback,bool singleUse){ButtonCallback=null;InputCallback=inputCallback;SingleUse=singleUse;}}public void ChatMessage(BasePlayer player,string message){if(player){player.SendConsoleCommand("chat.add",2,0,$"{ChatPrefix} {message}");}}}}namespace PluginComponents.HeliSpeed.Extensions.BaseNetworkable{using PluginComponents.HeliSpeed;using PluginComponents.HeliSpeed.Extensions;public static class BaseNetworkableEx{public static bool IsNullOrDestroyed(this global::BaseNetworkable baseNetworkable){return!baseNetworkable||baseNetworkable.IsDestroyed;}}}namespace PluginComponents.HeliSpeed.Extensions.Reflection{using Oxide.Core;using System;using System.Linq;using System.Reflection;using System.Text.RegularExpressions;using PluginComponents.HeliSpeed;using PluginComponents.HeliSpeed.Extensions;public static class ReflectionExtensions{public static T GetField<T>(this object obj,string fieldName){var field=obj.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){return(T)field.GetValue(obj);}else{Interface.Oxide.LogError($"Failed to get value of field {obj.GetType().Name}.{fieldName}");return default;}}public static void SetField(this object obj,string fieldName,object value){var field=obj.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){field.SetValue(obj,value);}else{Interface.Oxide.LogError($"Failed to set value of field {obj.GetType().Name}.{fieldName}");}}public static void SetField<T>(this T obj,string fieldName,object value){var field=typeof(T).GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){field.SetValue(obj,value);}else{Interface.Oxide.LogError($"Failed to set value of field {obj.GetType().Name}.{fieldName}");}}public static void CallMethod(this object obj,string methodName,params object[]args){var method=obj.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(method!=null){method.Invoke(obj,args);}else{Interface.Oxide.LogError($"Failed to invoke method {obj.GetType().Name}.{methodName} with {args.Length} args");}}public static T CallMethod<T>(this object obj,string methodName,params object[]args){var method=obj.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(method!=null){return(T)method.Invoke(obj,args);}else{Interface.Oxide.LogError($"Failed to invoke method {obj.GetType().Name}.{methodName} with {args.Length} args");return default;}}public static MethodInfo FindLocalMethod(this Type type,string surroundingName,string localName,bool capturesVariables){var methods=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Public|(capturesVariables?BindingFlags.Instance:BindingFlags.Static));return methods.FirstOrDefault(m=>m.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>()!=null&&Regex.IsMatch(m.Name,$@"^<{surroundingName}>g__{localName}\|\d+(_\d+)?"));}}}namespace PluginComponents.HeliSpeed.LoottableApi.Static{using Oxide.Core.Plugins;using PluginComponents.HeliSpeed;using PluginComponents.HeliSpeed.LoottableApi;public static class LoottableApi{private static Plugin Loottable=>Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugin("Loottable");public static void ClearPresets(Plugin plugin){Loottable?.Call("ClearPresets",plugin);}public static void CreatePresetCategory(Plugin plugin,string displayName){Loottable?.Call("AddCategory",plugin,displayName);}public static void CreatePreset(Plugin plugin,string displayName,string iconOrUrl){CreatePreset(plugin,false,displayName,displayName,iconOrUrl);}public static void CreatePreset(Plugin plugin,bool isNpc,string displayName,string iconOrUrl){CreatePreset(plugin,isNpc,displayName,displayName,iconOrUrl);}public static void CreatePreset(Plugin plugin,string key,string displayName,string iconOrUrl){CreatePreset(plugin,false,key,displayName,iconOrUrl);}public static void CreatePreset(Plugin plugin,bool isNpc,string key,string displayName,string iconOrUrl){Loottable?.Call("AddPreset",plugin,isNpc,key,displayName,iconOrUrl);}public static bool AssignPreset(Plugin plugin,ScientistNPC npc,string key){return Loottable?.Call("AssignPreset",plugin,key,npc)!=null;}public static bool AssignPreset(Plugin plugin,StorageContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public static bool AssignPreset(Plugin plugin,ItemContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public static void ClearCustomItems(Plugin plugin){Loottable?.Call("ClearCustomItems",plugin);}public static void AddCustomItem(Plugin plugin,int itemId,ulong skinId){Loottable?.Call("AddCustomItem",plugin,itemId,skinId);}public static void AddCustomItem(Plugin plugin,int itemId,ulong skinId,string customName){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName);}public static void AddCustomItem(Plugin plugin,int itemId,ulong skinId,string customName,bool persistent){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName,persistent);}}}namespace PluginComponents.HeliSpeed.LoottableApi{using Oxide.Core.Plugins;using PluginComponents.HeliSpeed;public class LoottableApi{private static Plugin Loottable=>Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugin("Loottable");private readonly Plugin plugin;public LoottableApi(Plugin plugin){this.plugin=plugin;}public void ClearPresets(){Loottable?.Call("ClearPresets",plugin);}public void CreatePresetCategory(string displayName){Loottable?.Call("AddCategory",plugin,displayName);}public void CreatePreset(string displayName,string iconOrUrl){CreatePreset(false,displayName,displayName,iconOrUrl);}public void CreatePreset(bool isNpc,string displayName,string iconOrUrl){CreatePreset(isNpc,displayName,displayName,iconOrUrl);}public void CreatePreset(string key,string displayName,string iconOrUrl){CreatePreset(false,key,displayName,iconOrUrl);}public void CreatePreset(bool isNpc,string key,string displayName,string iconOrUrl){Loottable?.Call("AddPreset",plugin,isNpc,key,displayName,iconOrUrl);}public bool AssignPreset(ScientistNPC npc,string key){return Loottable?.Call("AssignPreset",plugin,key,npc)!=null;}public bool AssignPreset(StorageContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public bool AssignPreset(ItemContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public void ClearCustomItems(){Loottable?.Call("ClearCustomItems",plugin);}public void AddCustomItem(int itemId,ulong skinId){Loottable?.Call("AddCustomItem",plugin,itemId,skinId);}public void AddCustomItem(int itemId,ulong skinId,string customName){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName);}public void AddCustomItem(int itemId,ulong skinId,string customName,bool persistent){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName,persistent);}}}