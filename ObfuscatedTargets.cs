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
    /// Finds the three game methods this mod hooks that have no name it can spell on SPT 4.1.
    ///
    /// 4.1 deobfuscates the client, and "method" is one of the prefixes the SPT assembly tool
    /// rewrites. Types got a published old-to-new mapping table; members did not, because the
    /// tool derives member names by matching signatures against a real-named reference
    /// assembly while it builds and keeps no table of the result. Worse, that match is a
    /// heuristic: it renames some members and leaves others alone, and which is which changes
    /// per EFT build. So a hardcoded "method_23" is not merely wrong-if-renamed, it is
    /// wrong-in-a-way-that-still-compiles-and-silently-patches-the-wrong-method if a later
    /// build renumbers.
    ///
    /// Each target below is instead identified by something the deobfuscator cannot change:
    /// the shape of its body.
    /// </summary>
    internal static class ObfuscatedTargets
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static
                                            | BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.DeclaredOnly;

        // ------------------------------------------------------------------ camera recoil

        private static Action<ProceduralWeaponAnimation, float> _cameraRecoil;
        private static bool _cameraRecoilResolved;

        /// <summary>
        /// 4.0 name: ProceduralWeaponAnimation.method_19(float). LerpCamera calls it to apply
        /// the per-frame camera recoil rotation, and this mod's LerpCamera replacement has to
        /// call it too or recoil stops moving the camera.
        ///
        /// It is one of eleven `void (float)` methods on the type, but the only one that is
        /// mostly quaternion work - it declares twelve Quaternion locals where the next
        /// highest declares one. Resolved once and held as an open delegate, because this
        /// runs every frame and reflection per frame is not free.
        /// </summary>
        internal static void ApplyCameraRecoil(ProceduralWeaponAnimation pwa, float deltaTime)
        {
            if (!_cameraRecoilResolved)
            {
                _cameraRecoilResolved = true;

                MethodInfo target = Single(
                    typeof(ProceduralWeaponAnimation),
                    m => !m.IsStatic
                      && m.ReturnType == typeof(void)
                      && HasParameters(m, typeof(float))
                      && CountLocalsOfType(m, "Quaternion") >= 4,
                    "camera recoil");

                if (target != null)
                {
                    _cameraRecoil = (Action<ProceduralWeaponAnimation, float>)Delegate.CreateDelegate(
                        typeof(Action<ProceduralWeaponAnimation, float>), target);
                    Utils.Logger.LogInfo($"FOVFix: camera recoil -> ProceduralWeaponAnimation.{target.Name}");
                }
                else
                {
                    Utils.Logger.LogError(
                        "FOVFix: could not find ProceduralWeaponAnimation's camera recoil method. " +
                        "Camera recoil will not be applied while aiming.");
                }
            }

            _cameraRecoil?.Invoke(pwa, deltaTime);
        }

        // ------------------------------------------------------- weapon params / FOV update

        /// <summary>
        /// 4.0 name: ProceduralWeaponAnimation.method_23(bool forced = false). Runs when the
        /// weapon's animation parameters are refreshed, which is where the game sets the main
        /// camera FOV - so it is where this mod learns the current weapon and re-applies its
        /// own FOV.
        ///
        /// Of the `void (bool)` methods on the type it is the only non-accessor whose body
        /// calls SetFov; the other two SetFov callers are the Sprint setter (an accessor) and
        /// InitTransforms (two parameters).
        /// </summary>
        internal static MethodBase WeaponParamsUpdate()
        {
            return Single(
                typeof(ProceduralWeaponAnimation),
                m => !m.IsStatic
                  && m.ReturnType == typeof(void)
                  && HasParameters(m, typeof(bool))
                  && BodyCallsMethodNamed(m, "SetFov"),
                "weapon params update");
        }

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

        private static MethodInfo Single(Type owner, Func<MethodInfo, bool> matches, string what)
        {
            var candidates = owner.GetMethods(Declared)
                .Where(m => !m.IsAbstract && !m.IsGenericMethodDefinition && !m.IsSpecialName)
                .Where(m => { try { return matches(m); } catch { return false; } })
                .ToList();

            return Report(candidates, what, owner.Name);
        }

        private static MethodInfo Report(List<MethodInfo> candidates, string what, string ownerName)
        {
            if (candidates.Count == 1) return candidates[0];

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

        private static int CountLocalsOfType(MethodBase m, string typeName)
        {
            MethodBody body;
            try { body = m.GetMethodBody(); } catch { return 0; }
            if (body == null) return 0;

            int count = 0;
            foreach (LocalVariableInfo local in body.LocalVariables)
                if (local.LocalType != null && local.LocalType.Name == typeName) count++;
            return count;
        }

        private static bool BodyCallsMethodNamed(MethodBase m, string calleeName)
        {
            foreach (var instruction in ReadBody(m))
                if (instruction.Value is MethodBase callee && callee.Name == calleeName) return true;
            return false;
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
