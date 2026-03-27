using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
            List<CodeInstruction> code = [.. instructions];

            MethodInfo setVectorsMethod = typeof(BlockLayersPatches).GetMethod(nameof(SetVectors))!;
            MethodInfo getRiverPowerMethod = typeof(BlockLayersPatches).GetMethod(nameof(GetRiverPower))!;
            MethodInfo mathMaxFloat = typeof(Math).GetMethod("Max", [typeof(float), typeof(float)])!;

            // Scan for ldloc; ldc.i4.s 32; blt patterns to find loop variables.
            // Scanning forward: first match = inner loop (j/z), second = outer loop (i/x).
            List<CodeInstruction> loopVarLoads = [];
            for (int k = 2; k < code.Count; k++)
            {
                if ((code[k].opcode == OpCodes.Blt || code[k].opcode == OpCodes.Blt_S)
                    && ((code[k - 1].opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(code[k - 1].operand) == 32)
                        || (code[k - 1].opcode == OpCodes.Ldc_I4 && (int)code[k - 1].operand == 32))
                    && code[k - 2].IsLdloc())
                {
                    loopVarLoads.Add(code[k - 2]);
                }
            }

            // Patch 1: Insert SetVectors(chunks) after chunks local is stored at the start.
            for (int k = 0; k < code.Count - 1; k++)
            {
                if (code[k].opcode == OpCodes.Callvirt
                    && code[k].operand is MethodInfo chunksMi && chunksMi.Name == "get_Chunks"
                    && code[k + 1].IsStloc())
                {
                    CodeInstruction loadChunks = StlocToLdloc(code[k + 1]);
                    code.Insert(k + 2, loadChunks);
                    code.Insert(k + 3, new CodeInstruction(OpCodes.Call, setVectorsMethod));
                    break;
                }
            }

            // Patch 2: Multiply raise by GetRiverPower(i, j) after Math.Max(0f, (0.5f - rainRel) * 40f).
            if (loopVarLoads.Count >= 2)
            {
                // loopVarLoads[0] = j/z (inner loop), loopVarLoads[1] = i/x (outer loop)
                CodeInstruction loadI = new(loopVarLoads[1].opcode, loopVarLoads[1].operand);
                CodeInstruction loadJ = new(loopVarLoads[0].opcode, loopVarLoads[0].operand);

                for (int k = 0; k < code.Count; k++)
                {
                    if (code[k].opcode == OpCodes.Call
                        && code[k].operand is MethodInfo maxMi && maxMi == mathMaxFloat)
                    {
                        code.Insert(k + 1, loadI);
                        code.Insert(k + 2, loadJ);
                        code.Insert(k + 3, new CodeInstruction(OpCodes.Call, getRiverPowerMethod));
                        code.Insert(k + 4, new CodeInstruction(OpCodes.Mul));
                        break;
                    }
                }
            }

            return code;
        }

        private static CodeInstruction StlocToLdloc(CodeInstruction stloc)
        {
            return stloc.opcode == OpCodes.Stloc_0
                ? new CodeInstruction(OpCodes.Ldloc_0)
                : stloc.opcode == OpCodes.Stloc_1
                ? new CodeInstruction(OpCodes.Ldloc_1)
                : stloc.opcode == OpCodes.Stloc_2
                ? new CodeInstruction(OpCodes.Ldloc_2)
                : stloc.opcode == OpCodes.Stloc_3
                ? new CodeInstruction(OpCodes.Ldloc_3)
                : stloc.opcode == OpCodes.Stloc_S
                ? new CodeInstruction(OpCodes.Ldloc_S, stloc.operand)
                : new CodeInstruction(OpCodes.Ldloc, stloc.operand);
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

    /// <summary>
    /// Call this at the beginning of OnChunkColumnGeneration in GenBlockLayers.
    /// </summary>
    public static void SetVectors(IServerChunk[] chunks)
    {
        if (chunks == null) return;

        // One deserialization per column, yeah could cache that too.
        Distances = chunks[0]?.MapChunk.GetModdata<ushort[]>("riverDistance");
    }

    /// <summary>
    /// 0 power at the river bank, 1 far enough away that dry areas are normally boosted.
    /// </summary>
    public static float GetRiverPower(int localX, int localZ)
    {
        if (Distances == null) return 1f;

        ushort distance = Distances[(localZ * 32) + localX];

        return distance == 0 ? 0 : distance > 10 ? 1 : (float)RiverMath.InverseLerp(distance, 0, 10);
    }
}