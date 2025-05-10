using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Rivers;

public class BlockLayersPatches
{
    public static ushort[]? Distances { get; set; }

    /// <summary>
    /// Before the XZ loop, retrieve arrays.
    /// In XZ loop, check if either of the arrays != 0. If this is the case raise is 0.
    /// Nullify in postfix.
    /// </summary>
    [HarmonyPatch(typeof(GenBlockLayers))]
    [HarmonyPatch("OnChunkColumnGeneration")]
    [HarmonyPatchCategory("core")]
    public static class OnChunkColumnGenerationTranspiler
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            List<CodeInstruction> code = new(instructions);

            int insertionIndex = -1;
            object xOperand = null!;
            object zOperand = null!;

            for (int i = 4; i < code.Count - 4; i++)
            {
                if (code[i].opcode == OpCodes.Newobj && code[i].operand == typeof(BlockPos).GetConstructor(Array.Empty<Type>()) && code[i + 1].opcode == OpCodes.Stloc_S && code[i + 2].opcode == OpCodes.Ldc_I4_0)
                {
                    insertionIndex = i;
                    xOperand = code[i + 3].operand;
                    zOperand = code[i + 6].operand;
                    break;
                }
            }

            List<CodeInstruction> ins = new()
            {
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockLayersPatches), "SetVectors"))
            };

            if (insertionIndex != -1)
            {
                code.InsertRange(insertionIndex, ins);
            }

            //Second part

            insertionIndex = -1;
            object seaLevelOperand = null!;

            for (int i = 4; i < code.Count - 4; i++)
            {
                if (code[i].opcode == OpCodes.Stloc_S && code[i - 1].opcode == OpCodes.Conv_I4 && code[i - 2].opcode == OpCodes.Call && code[i - 2].operand == AccessTools.Method(typeof(Math), "Min", new Type[] { typeof(float), typeof(float) }))
                {
                    seaLevelOperand = code[i].operand;
                    insertionIndex = i + 1;
                    break;
                }
            }

            ins = new()
            {
                new CodeInstruction(OpCodes.Ldloc_S, xOperand),
                new CodeInstruction(OpCodes.Ldloc_S, zOperand),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BlockLayersPatches), "IsRiver")),
                new CodeInstruction(OpCodes.Ldloc_S, seaLevelOperand),
                new CodeInstruction(OpCodes.Conv_R4),
                new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Conv_I4),
                new CodeInstruction(OpCodes.Stloc_S, seaLevelOperand)
            };

            if (insertionIndex != -1)
            {
                code.InsertRange(insertionIndex, ins);
            }

            return code;
        }
    }

    [HarmonyPatch(typeof(GenBlockLayers))]
    [HarmonyPatch("OnChunkColumnGeneration")]
    [HarmonyPatchCategory("core")]
    public static class OnChunkColumnGenerationPostfix
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Distances = null;
        }
    }

    public static void SetVectors(IServerChunk[] chunks)
    {
        Distances = chunks[0].MapChunk.GetModdata<ushort[]>("riverDistance");
    }

    // Disable Y level boost in dry areas.
    public static float IsRiver(int localX, int localZ)
    {
        if (Distances == null) return 1;

        ushort distance = Distances[(localZ * 32) + localX];

        if (distance == 0) return 0;
        return distance > 10 ? 1 : (float)RiverMath.InverseLerp(distance, 0, 10);
    }
}