using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EFT.Animations;
using EFT.Settings.Game;
using HarmonyLib;

namespace FOVFix
{
    /// <summary>
    /// Finds the one game method this mod hooks that has no name it can safely spell on SPT 4.1.
    ///
    /// The first 4.1 port resolved three targets here by the shape of their bodies, because
    /// 4.1 deobfuscates the client and published no member mapping. Two of them now have real
    /// 4.1 names that upstream uses as well and that exist in the 4.1 Assembly-CSharp:
    /// method_19 -> AddHandRecoilRotateToCamera, method_23 -> OnAimOrPoseChanged. They are
    /// called and patched by those names (compile-time checked) in FovPatches.
    ///
    /// The FOV clamp stays here: it is a compiler-generated lambda on a cached display class,
    /// a name that shifts if BSG so much as adds a lambda to that file, so it is identified by
    /// something no rename can change - the shape of its body.
    /// </summary>
    internal static class ObfuscatedTargets
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static
                                            | BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.DeclaredOnly;

        // --------------------------------------------------------------- base FOV clamp

        /// <summary>
        /// 4.0 name: GClass1085.Class1841.method_0(int). The game builds its FieldOfView
        /// setting with a validator lambda that clamps to 50..75; the C# compiler puts that
        /// lambda on a cached display class nested in the settings group, and BSG's obfuscator
        /// renamed it. Patching it is what lets the base FOV leave the vanilla range.
        ///
        /// A compiler-generated name is fragile no matter what the deobfuscator does - it
        /// changes if BSG so much as adds a lambda to that file - so match on the clamp
        /// itself: the nested method taking and returning int whose body loads both
        /// MIN_FIELD_OF_VIEW and MAX_FIELD_OF_VIEW. The sibling lambda on the same class
        /// clamps 5..100, so the pair of constants separates them.
        /// </summary>
        internal static MethodBase BaseFovClamp()
        {
            var candidates = new List<MethodInfo>();

            foreach (Type nested in typeof(GameSettingsGroup).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (MethodInfo m in nested.GetMethods(Declared))
                {
                    if (m.IsAbstract || m.IsGenericMethodDefinition) continue;
                    if (m.ReturnType != typeof(int) || !HasParameters(m, typeof(int))) continue;
                    if (!BodyLoadsConstants(m, GameSettingsGroup.MIN_FIELD_OF_VIEW, GameSettingsGroup.MAX_FIELD_OF_VIEW)) continue;
                    candidates.Add(m);
                }
            }

            return Report(candidates, "base FOV clamp", typeof(GameSettingsGroup).Name);
        }

        // ------------------------------------------------------------------------ plumbing

        private static MethodInfo Report(List<MethodInfo> candidates, string what, string ownerName)
        {
            if (candidates.Count == 1)
            {
                // Log what each fingerprint actually landed on. On a client this mod has not
                // seen, these three lines are the difference between "it works" and knowing
                // why - and the names themselves say whether the match was sensible.
                MethodInfo hit = candidates[0];
                Utils.Logger.LogInfo($"FOVFix: {what} -> {hit.DeclaringType.Name}.{hit.Name}");
                return hit;
            }

            // Zero means the shape moved; more than one means the fingerprint stopped being
            // unique. Either way, picking something would patch the game at a point nobody
            // checked, so patch nothing and say so.
            Utils.Logger.LogError(
                $"FOVFix: expected exactly one {what} candidate on {ownerName}, found {candidates.Count}" +
                (candidates.Count > 1 ? $" ({string.Join(", ", candidates.Select(m => m.Name).ToArray())})" : "") +
                ". That feature is disabled.");
            return null;
        }

        private static bool HasParameters(MethodInfo m, params Type[] types)
        {
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != types.Length) return false;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].ParameterType != types[i]) return false;
            return true;
        }

        private static bool BodyLoadsConstants(MethodBase m, params int[] wanted)
        {
            var found = new HashSet<int>();
            foreach (var instruction in ReadBody(m))
            {
                int value;
                if (TryGetInt32Constant(instruction.Key, instruction.Value, out value)) found.Add(value);
            }

            foreach (int w in wanted)
                if (!found.Contains(w)) return false;
            return true;
        }

        private static IEnumerable<KeyValuePair<OpCode, object>> ReadBody(MethodBase m)
        {
            try { return PatchProcessor.ReadMethodBody(m); }
            catch { return new KeyValuePair<OpCode, object>[0]; }
        }

        private static bool TryGetInt32Constant(OpCode opcode, object operand, out int value)
        {
            // The short forms carry the value in the opcode itself and hand back a null operand.
            if (opcode == OpCodes.Ldc_I4_M1) { value = -1; return true; }
            if (opcode == OpCodes.Ldc_I4_0) { value = 0; return true; }
            if (opcode == OpCodes.Ldc_I4_1) { value = 1; return true; }
            if (opcode == OpCodes.Ldc_I4_2) { value = 2; return true; }
            if (opcode == OpCodes.Ldc_I4_3) { value = 3; return true; }
            if (opcode == OpCodes.Ldc_I4_4) { value = 4; return true; }
            if (opcode == OpCodes.Ldc_I4_5) { value = 5; return true; }
            if (opcode == OpCodes.Ldc_I4_6) { value = 6; return true; }
            if (opcode == OpCodes.Ldc_I4_7) { value = 7; return true; }
            if (opcode == OpCodes.Ldc_I4_8) { value = 8; return true; }

            if (opcode == OpCodes.Ldc_I4 || opcode == OpCodes.Ldc_I4_S)
            {
                // Ldc_I4_S hands back an sbyte, Ldc_I4 an int.
                if (operand is IConvertible convertible)
                {
                    value = Convert.ToInt32(convertible);
                    return true;
                }
            }

            value = 0;
            return false;
        }
    }
}
