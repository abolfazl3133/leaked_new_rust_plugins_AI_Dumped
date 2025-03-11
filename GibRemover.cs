// Reference: 0Harmony

using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Gib", "ViolationHandler", "0.0.4")]
    [Description("Removes gibs entirely.")]
    public class GibRemover : RustPlugin
    {
        private Harmony _harmonyInstance;
        
        private Configuration config;
        
        #region Configuration

        private class Configuration
        {
            [JsonProperty("Remove ALL gibs from destroying/removing everything. (Including decaying things)")]
            public bool everything = true;
            
            [JsonProperty("Remove gibs from exploded entities.")]
            public bool c4 = false;
            
            [JsonProperty("Remove gibs from ent killed entities.")]
            public bool entKill = false;
            
            [JsonProperty("Remove gibs from entities killed by cargo driving through base.")]
            public bool cargoDestroy = false;
            
            [JsonProperty("Remove gibs from modular car being killed.")]
            public bool modularCarKilled = false;
            
            [JsonProperty("Remove gibs from modular car being hurt.")]
            public bool modularCarHurt = false;
            
            [JsonProperty("Remove gibs from mountable things (chairs, couches, vehicles, slot machines, etc).")]
            public bool mountableEntities = false;
            
            [JsonProperty("Remove gibs from loot containers after being looted fully by a player.")]
            public bool lootContainerLooting = false;
            
            [JsonProperty("Remove gibs from loot containers killed by Patrol Heli/Bradley.")]
            public bool lootContainerRemoveMe = false;
            
            [JsonProperty("Remove gibs from entities when using a hammer to demolish them.")]
            public bool buildingDemolish = false;
            
            [JsonProperty("Remove gibs from entities when not enough Stability.")]
            public bool buildingStability = false;
            
            [JsonProperty("Remove gibs from tool-cupboard when placing in another building privilege zone.")]
            public bool buildingPrivilege = false;
            
            [JsonProperty("Remove gibs from entities when their ground is missing.")]
            public bool groundMissing = false;
            
            [JsonProperty("Remove gibs from traincars when they die.")]
            public bool trainCarDeath = false;
            
            [JsonProperty("Remove gibs from barricades blocking the train (EX: the blockades in tunnels underground).")]
            public bool trainBarricade = false;
            
            [JsonProperty("Remove gibs from boats when the pool they are in get destroyed.")]
            public bool boatPoolDestroyed = false;
            
            // [JsonProperty("Remove gibs from vehicles when they collide with something.")]
            // public bool vehicleCollisionGibs = false;
            
            private string ToJson() => JsonConvert.SerializeObject(this);
        
            public Dictionary<string, object> ToDictionary() =>
                JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }


        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    Puts("Configuration appears to be outdated; updating and saving.");
                    SaveConfig();
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }
        
        #endregion Configuration
        
        private void Loaded()
        {
            _harmonyInstance = new Harmony("com.ViolationHandler" + Name);
        }

        private void OnServerInitialized()
        {
            LoadConfig();
            if(config.everything)
            {
                // Everything.
                _harmonyInstance.Patch(AccessTools.Method(typeof(BaseNetworkable), nameof(BaseNetworkable.TerminateOnClient)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.TerminateOnClient)));
                
                // _harmonyInstance.Patch(AccessTools.Method(typeof(BaseVehicle), nameof(BaseVehicle.TryShowCollisionFX), new [] {typeof(Collision), typeof(GameObjectRef)}), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.TryShowCollisionFX)));
                // _harmonyInstance.Patch(AccessTools.Method(typeof(BaseVehicle), nameof(BaseVehicle.TryShowCollisionFX), new [] {typeof(Vector3), typeof(GameObjectRef)}), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.TryShowCollisionFX)));
                // _harmonyInstance.Patch(AccessTools.Method(typeof(BaseHelicopter), nameof(BaseHelicopter.ProcessCollision)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.ProcessCollision)));
            }
            else
            {
                // C4
                if(config.c4)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(BaseCombatEntity), nameof(BaseCombatEntity.OnKilled)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Ent Kill
                if(config.entKill)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(BaseNetworkable), nameof(BaseNetworkable.AdminKill)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Cargo Destroying Bases
                if(config.cargoDestroy)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(CargoShip), nameof(CargoShip.BuildingCheck)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Magnet Crane -- Recommend Against due to it being in VehicleFixedUpdate.
                // _harmonyInstance.Patch(AccessTools.Method(typeof(MagnetCrane), nameof(MagnetCrane.VehicleFixedUpdate)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Modular Car
                if(config.modularCarHurt)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(ModularCar), nameof(ModularCar.ModuleHurt)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));
                
                if(config.modularCarKilled)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(ModularCar), nameof(ModularCar.OnKilled)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // WaterInflatable
                if(config.mountableEntities)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(BaseMountable), nameof(BaseMountable.OnKilled)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.BaseMountableOnKilled)));

                // Loot Containers
                if(config.lootContainerLooting)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(LootContainer), nameof(LootContainer.PlayerStoppedLooting)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));
                if(config.lootContainerRemoveMe)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(LootContainer), nameof(LootContainer.RemoveMe)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Train Barricades
                if(config.trainBarricade)
                   _harmonyInstance.Patch(AccessTools.Method(typeof(HittableByTrains), "DestroyThisBarrier"), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Demolish
                if (config.buildingDemolish)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(StabilityEntity), nameof(StabilityEntity.DoDemolish)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Destroy on Ground Missing.
                if(config.groundMissing)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(DestroyOnGroundMissing), "OnGroundMissing"), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Stability.
                if(config.buildingStability)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(StabilityEntity), nameof(StabilityEntity.StabilityCheck)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Useless I think
                // _harmonyInstance.Patch(AccessTools.Method(typeof(BuildingBlock), nameof(BuildingBlock.DoImmediateDemolish)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));
                
                // Building priv stacked
                if(config.buildingPrivilege)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(BuildingPrivlidge), nameof(BuildingPrivlidge.EnsurePrimary)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Train Car
                if(config.trainCarDeath)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(TrainCar), nameof(TrainCar.ActualDeath)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // Boat
                if(config.boatPoolDestroyed)
                    _harmonyInstance.Patch(AccessTools.Method(typeof(BaseBoat), nameof(BaseBoat.OnPoolDestroyed)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // C4 entity itself?
                // _harmonyInstance.Patch(AccessTools.Method(typeof(TimedExplosive), nameof(TimedExplosive.Explode), new Type[] {typeof(Vector3)}), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.OnKilled)));

                // if (config.vehicleCollisionGibs)
                // {
                //     _harmonyInstance.Patch(AccessTools.Method(typeof(BaseVehicle), nameof(BaseVehicle.TryShowCollisionFX), new [] {typeof(Collision), typeof(GameObjectRef)}), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.TryShowCollisionFX)));
                //     _harmonyInstance.Patch(AccessTools.Method(typeof(BaseVehicle), nameof(BaseVehicle.TryShowCollisionFX), new [] {typeof(Vector3), typeof(GameObjectRef)}), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.TryShowCollisionFX)));
                //     _harmonyInstance.Patch(AccessTools.Method(typeof(BaseHelicopter), nameof(BaseHelicopter.ProcessCollision)), transpiler: new HarmonyMethod(typeof(GibFix), nameof(GibFix.ProcessCollision)));
                // }
            }
        }

        private void Unload()
        {
            _harmonyInstance?.UnpatchAll(_harmonyInstance.Id);
        }

        public class GibFix
        {
            internal static IEnumerable<CodeInstruction> OnKilled(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = instructions.ToList();

                int loadOne = list.FindIndex(i => (i.opcode == OpCodes.Call && i.operand.ToString().Contains("DestroyMode")) || (i.opcode == OpCodes.Callvirt && i.operand.ToString().Contains("DestroyMode")));

                if (loadOne == -1)
                {
                    Debug.LogWarning($"[GibRemover] Plugin failed to find proper location to transpile, contact developer.");
                    return list;
                }
                
                list[loadOne-1] = new CodeInstruction(OpCodes.Ldc_I4_0);
                
                int loadOneSecond = list.FindIndex(loadOne+1, i => i.opcode == OpCodes.Call && i.operand.ToString().Contains("DestroyMode"));

                if (loadOneSecond == -1)
                {
                    return list;
                }
                
                list[loadOneSecond-1] = new CodeInstruction(OpCodes.Ldc_I4_0);
                return list;
            }
            
            internal static IEnumerable<CodeInstruction> TerminateOnClient(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = instructions.ToList();
                
                int loadOne = list.FindIndex(i => i.opcode == OpCodes.Ldarg_1);
                
                if (loadOne == -1)
                {
                    Debug.LogWarning($"[GibRemover] Plugin failed to find proper location to transpile, contact developer.");
                    return list;
                }
                
                
                list[loadOne] = new CodeInstruction(OpCodes.Ldc_I4_0);
                
                int loadOneSecond = list.FindIndex(loadOne+1, i => i.opcode == OpCodes.Ldarg_1);

                if (loadOneSecond == -1)
                {
                    return list;
                }
                
                list[loadOneSecond] = new CodeInstruction(OpCodes.Ldc_I4_0);
                
                return list;
            }
            
            internal static IEnumerable<CodeInstruction> BaseMountableOnKilled(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = instructions.ToList();
                
                int loadOne = list.FindLastIndex(i => i.opcode == OpCodes.Ldarg_0);
                
                if (loadOne == -1)
                {
                    Debug.LogWarning($"[GibRemover] Plugin failed to find proper location to transpile, contact developer.");
                    return list;
                }
                
                list.RemoveRange(loadOne, 3);
                
                list.InsertRange(loadOne, new List<CodeInstruction>
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BaseNetworkable), nameof(BaseNetworkable.Kill))),
                });
                
                return list;
            }
            
            internal static IEnumerable<CodeInstruction> TryShowCollisionFX(IEnumerable<CodeInstruction> instructions)
            {
                return new List<CodeInstruction>();
            }
            
            internal static IEnumerable<CodeInstruction> ProcessCollision(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> list = instructions.ToList();
                
                int indexUp = list.FindIndex(i => i.opcode == OpCodes.Callvirt && i.operand.ToString().Contains("get_up"));
                // TODO: Update this IF statements location it jumps to go past the skipped portion of code dealing with effects being run. 
                
                if (indexUp == -1)
                {
                    Debug.LogWarning($"[GibRemover] Plugin failed to find proper location to transpile, contact developer.");
                    return list;
                }
                
                int indexBge = list.FindIndex(indexUp, i => i.opcode == OpCodes.Bge_Un_S);
                // TODO: Update this IF statements location it jumps to go past the skipped portion of code dealing with effects being run. 
                
                if (indexBge == -1)
                {
                    Debug.LogWarning($"[GibRemover] Plugin failed to find proper location to transpile, contact developer.");
                    return list;
                }

                int indexOne = list.FindIndex(i => i.opcode == OpCodes.Ldfld && i.operand.ToString().Contains("nextEffectTime"));
                
                if (indexOne == -1)
                {
                    Debug.LogWarning($"[GibRemover] Plugin failed to find proper location to transpile, contact developer.");
                    return list;
                }

                indexOne -= 2;
                
                int indexTwo = list.FindIndex(indexOne+1, i => i.opcode == OpCodes.Call && i.operand.ToString().Contains("Run"));

                if (indexTwo == -1)
                {
                    Debug.LogWarning($"[GibRemover] Plugin failed to find proper location to transpile, contact developer.");
                    return list;
                }

                indexTwo+= 1;

                List<Label> labels = list[indexOne].labels; // May not use labels and may just use a jump location instead of a label. 
                
                list.RemoveRange(indexOne, indexTwo-indexOne);

                list[indexBge].labels = labels; // check here.
                // Do here.
                return list;
            }
        }
    }
}