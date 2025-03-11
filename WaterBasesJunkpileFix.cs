// Reference: 0Harmony

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WaterBasesJunkpileFix", "Nikedemos", "1.0.0")]
    [Description("Prevents JunkPileWater entities from spawning inside anything player-deployed, built or parked")]
    public class WaterBasesJunkpileFix : RustPlugin
    {
        public Harmony WaterBasesJunkPileFixHarmony;
        public const int LAYERMASK_VEHICLE_LARGE_CONSTRUCTION_DEPLOYED_DEFAULT = 1 << (int)Rust.Layer.Vehicle_Large | 1 << (int)Rust.Layer.Construction | 1 << (int)Rust.Layer.Deployed | 1 << (int)Rust.Layer.Default;

        #region HOOK SUBSCRIPTIONS
        void Init()
        {
            WaterBasesJunkPileFixHarmony = null;

            JunkpileWaterSpawn_patch.JunkpileWaterSpawnMethodInfo = AccessTools.Method(typeof(JunkPileWater), nameof(JunkPileWater.Spawn));
            JunkpileWaterSpawn_patch.ReplaceOnStackMethodInfo = AccessTools.Method(typeof(JunkpileWaterSpawn_patch), nameof(JunkpileWaterSpawn_patch.ReplaceOnStack));

            bool goAhead = true;

            if (JunkpileWaterSpawn_patch.JunkpileWaterSpawnMethodInfo == null)
            {
                PrintError($"FATAL ERROR: {nameof(JunkpileWaterSpawn_patch.JunkpileWaterSpawnMethodInfo)} is null, the patch cannot proceed! Please let Nikedemos know!");
                goAhead = false;

            }

            if (JunkpileWaterSpawn_patch.ReplaceOnStackMethodInfo == null)
            {
                PrintError($"FATAL ERROR: {nameof(JunkpileWaterSpawn_patch.ReplaceOnStackMethodInfo)} is null, the patch cannot proceed! Please let Nikedemos know!");
                goAhead = false;
            }

            if (!goAhead)
            {
                return;
            }

            WaterBasesJunkPileFixHarmony = new Harmony(nameof(WaterBasesJunkPileFixHarmony));

            try
            {
                WaterBasesJunkPileFixHarmony.CreateClassProcessor(typeof(JunkpileWaterSpawn_patch)).Patch();

                PrintWarning("OK: Successfully applied the patch");
            }
            catch (Exception e)
            {
                PrintError($"FATAL `{e.GetType()}` while attempting to initialize the patch, please let Nikedemos know: {e.Message}\n{e.StackTrace}");
            }
        }

        void Unload()
        {
            WaterBasesJunkPileFixHarmony?.UnpatchAll(WaterBasesJunkPileFixHarmony.Id);
            WaterBasesJunkPileFixHarmony = null;

            JunkpileWaterSpawn_patch.JunkpileWaterSpawnMethodInfo = null;
            JunkpileWaterSpawn_patch.ReplaceOnStackMethodInfo = null;
        }

        #endregion
        #region HARMONY
        [HarmonyPatch]
        public static class JunkpileWaterSpawn_patch
        {
            public static MethodInfo JunkpileWaterSpawnMethodInfo;
            public static MethodInfo ReplaceOnStackMethodInfo;

            public static int ReplaceOnStack(int discard)
            {
                return LAYERMASK_VEHICLE_LARGE_CONSTRUCTION_DEPLOYED_DEFAULT;
            }

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
            {
                var codes = new List<CodeInstruction>(instructions);

                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode != OpCodes.Call)
                    {
                        continue;
                    }

                    if (!(codes[i].operand is MethodInfo usedMethod))
                    {
                        continue;
                    }

                    if (!usedMethod.DeclaringType.Equals(typeof(Physics)))
                    {
                        continue;
                    }

                    if (!usedMethod.Name.Equals(nameof(Physics.CheckSphere)))
                    {
                        continue;
                    }

                    codes.Insert(i, new CodeInstruction(OpCodes.Call, ReplaceOnStackMethodInfo));
                    break;
                }

                return codes;
            }
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return new List<MethodBase> { JunkpileWaterSpawnMethodInfo };
            }

        }

        #endregion
    }
}
