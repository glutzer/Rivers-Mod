using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace Rivers;

public class LiquidTesselatorPatch
{
    public static float EnsureNonZeroSpeed(float speed) => speed == 0f ? 1f : speed;

    public static void TesselateFlow(float[] upFlowVectors, TCTCache vars)
    {
        TCTCacheTwo varsTwo = (TCTCacheTwo)vars;

        if (varsTwo.flowVectors != null) // Check if the chunk can even have a river.
        {
            float xFlow = varsTwo.flowVectors[(vars.posZ % 32 * 32) + (vars.posX % 32)] * varsTwo.riverSpeed; // These are normalized vectors multiplied by the speed?
            float zFlow = varsTwo.flowVectors[(vars.posZ % 32 * 32) + (vars.posX % 32) + 1024] * varsTwo.riverSpeed; // Z * 32 + X, 2d index.

            if (xFlow != 0f || zFlow != 0f)
            {
                upFlowVectors[0] = xFlow;
                upFlowVectors[1] = zFlow;
                upFlowVectors[2] = xFlow;
                upFlowVectors[3] = zFlow;
                upFlowVectors[4] = xFlow;
                upFlowVectors[5] = zFlow;
                upFlowVectors[6] = xFlow;
                upFlowVectors[7] = zFlow;
            }
        }
    }

    [HarmonyPatch(typeof(LiquidTesselator), "DrawLiquidBlockFace")]
    [HarmonyPatchCategory("flow")]
    public static class DrawLiquidBlockFaceTranspiler
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = [.. instructions];
            MethodInfo mathMinMethod = AccessTools.Method(typeof(Math), "Min", [typeof(float), typeof(float)]);

            for (int i = 0; i < code.Count - 1; i++)
            {
                if (code[i].Calls(mathMinMethod))
                {
                    CodeInstruction stloc = code[i + 1];
                    if (stloc.opcode == OpCodes.Stloc_S || stloc.opcode == OpCodes.Stloc)
                    {
                        int insertAt = i + 2;
                        OpCode ldlocOp = stloc.opcode == OpCodes.Stloc_S ? OpCodes.Ldloc_S : OpCodes.Ldloc;

                        CodeInstruction ldloc = new(ldlocOp, stloc.operand);
                        if (insertAt < code.Count)
                        {
                            ldloc.labels.AddRange(code[insertAt].labels);
                            code[insertAt].labels.Clear();
                        }

                        code.InsertRange(insertAt,
                        [
                            ldloc,
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LiquidTesselatorPatch), "EnsureNonZeroSpeed")),
                            new CodeInstruction(stloc.opcode, stloc.operand)
                        ]);
                        break;
                    }
                }
            }

            return code;
        }
    }

    [HarmonyPatch(typeof(LiquidTesselator))]
    [HarmonyPatch("Tesselate")]
    [HarmonyPatchCategory("flow")]
    public static class LiquidTesselateTranspiler
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            List<CodeInstruction> code = [.. instructions];
            int insertionIndex = -1;

            for (int i = 4; i < code.Count - 4; i++) // -1 since checking i + 1.
            {
                if (code[i].opcode == OpCodes.Ldloc_S && code[i + 1].opcode == OpCodes.Ldnull && code[i + 2].opcode == OpCodes.Call && (MethodBase)code[i + 2].operand == AccessTools.Method(typeof(Vec3i), "op_Inequality"))
                {
                    insertionIndex = i;
                    break;
                }
            }

            List<CodeInstruction> ins =
            [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(LiquidTesselator), "upFlowVectors")),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LiquidTesselatorPatch), "TesselateFlow"))
            ];

            if (insertionIndex != -1)
            {
                code.InsertRange(insertionIndex, ins);
            }

            return code;
        }
    }


}