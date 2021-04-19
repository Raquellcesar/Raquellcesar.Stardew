// -----------------------------------------------------------------------
// <copyright file="CharactersPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.CompilerServices;

    using Harmony;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Characters;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Character"/> classes.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class CharactersPatcher
    {
        /// <summary>
        ///     Associates new properties to <see cref="Horse"/> objects at runtime.
        /// </summary>
        private static readonly ConditionalWeakTable<Horse, HorseData> HorsesData =
            new ConditionalWeakTable<Horse, HorseData>();

        /// <summary>
        ///     The Harmony patching API.
        /// </summary>
        private static HarmonyInstance harmony;

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            CharactersPatcher.harmony = harmony;

            harmony.Patch(
                AccessTools.Method(typeof(Child), nameof(Child.checkAction)),
                transpiler: new HarmonyMethod(typeof(CharactersPatcher), nameof(CharactersPatcher.TranspileChildCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Horse), nameof(Horse.checkAction)),
                new HarmonyMethod(typeof(CharactersPatcher), nameof(CharactersPatcher.BeforeHorseCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Horse), nameof(Horse.checkAction)),
                transpiler: new HarmonyMethod(typeof(CharactersPatcher), nameof(CharactersPatcher.TranspileHorseCheckAction)));
        }

        /// <summary>
        ///     Gets if an horse should allow action checking.
        /// </summary>
        /// <param name="horse">The <see cref="Horse"/> instance.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this horse can check for action. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        public static bool IsCheckActionEnabled(this Horse horse)
        {
            return horse is not null && CharactersPatcher.HorsesData.GetOrCreateValue(horse).CheckActionEnabled;
        }

        /// <summary>
        ///     Sets whether an horse allows action checking.
        /// </summary>
        /// <param name="horse">The <see cref="Horse"/> instance.</param>
        /// <param name="value">Determines whether the horse can check action.</param>
        public static void SetCheckActionEnabled(this Horse horse, bool value)
        {
            if (horse is not null)
            {
                CharactersPatcher.HorsesData.GetOrCreateValue(horse).CheckActionEnabled = value;
            }
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Horse.checkAction"/>. It checks if the
        ///     horse has check action enabled.
        /// </summary>
        /// <param name="__instance">The <see cref="Horse"/> instance.</param>
        /// <param name="__result">A reference to the result of the original method.</param>
        /// <returns>
        ///     Returns <see langword="false"/> to terminate prefixes and skip the execution of the
        ///     original method, <see langword="true"/> otherwise.
        /// </returns>
        private static bool BeforeHorseCheckAction(
            Horse __instance,
            ref bool __result)
        {
            HorseData horseData = CharactersPatcher.HorsesData.GetOrCreateValue(__instance);
            if (!horseData.CheckActionEnabled)
            {
                horseData.CheckActionEnabled = true;

                __result = false;
                return false;
            }

            return true;
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Child.checkAction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileChildCheckAction(
            IEnumerable<CodeInstruction> instructions)
        {
            // Check if CurrentToolIndex is greater than zero before accessing items.

            /*
             * Relevant CIL code:
             *      if (base.Age >= 3 && who.items.Count > who.CurrentToolIndex && who.items[who.CurrentToolIndex] != null && who.Items[who.CurrentToolIndex] is Hat)
             *          IL_0079: ldarg.0
             *          IL_007a: call instance int32 StardewValley.NPC::get_Age()
             *          IL_007f: ldc.i4.3
             *          IL_0080: blt IL_014f
             *          ...
             *
             * Replace with:
             *      if (base.Age >= 3 && who.CurrentToolIndex >= 0 && who.items.Count > who.CurrentToolIndex && who.items[who.CurrentToolIndex] != null && who.Items[who.CurrentToolIndex] is Hat)
             */

            MethodInfo getCurrentToolIndex =
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentToolIndex)).GetGetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Blt
                           && codeInstructions[i - 1].opcode == OpCodes.Ldc_I4_3)
                {
                    yield return codeInstructions[i];

                    object jump = codeInstructions[i].operand;

                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Callvirt, getCurrentToolIndex);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Blt, jump);

                    found = true;
                }
                else
                {
                    yield return codeInstructions[i];
                }
            }

            if (!found)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(Child)}.{nameof(Child.checkAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Horse.checkAction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileHorseCheckAction(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codeInstructions = instructions.ToList();

            // Find the delegate method to transpile.
            int index = codeInstructions.FindIndex(
                    0,
                    ins => ins.opcode == OpCodes.Ldftn && ins.operand is MethodInfo);

            if (index < 0)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(Horse)}.{nameof(Horse.checkAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
            else
            {
                // Patch the delegate.
                CharactersPatcher.harmony.Patch(
                    (MethodInfo)codeInstructions[index].operand,
                    transpiler: new HarmonyMethod(typeof(CharactersPatcher), nameof(CharactersPatcher.TranspileHorseDelegateCheckAction)));
            }

            // Return the original method untouched.
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify the delegate in <see cref="Horse.checkAction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        /// <param name="ilGenerator">Generates MSIL instructions.</param>
        private static IEnumerable<CodeInstruction> TranspileHorseDelegateCheckAction(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator ilGenerator)
        {
            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found1 = false;
            bool found2 = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                /* Check if CurrentToolIndex is greater than zero before accessing items. */

                /*
                 * Relevant CIL code:
                 *      else if (this.who.items.Count > this.who.CurrentToolIndex && this.who.items[this.who.CurrentToolIndex] != null && this.who.Items[this.who.CurrentToolIndex] is Hat)
                 *          IL_01c8: ldarg.0
                 *          IL_01c9: ldfld class StardewValley.Farmer StardewValley.Characters.Horse/'<>c__DisplayClass31_0'::who
                 *          IL_01ce: ldfld class [Netcode]Netcode.NetObjectList`1<class StardewValley.Item> StardewValley.Farmer::items
                 *          IL_01d3: callvirt instance int32 class [Netcode]Netcode.NetList`2<class StardewValley.Item, class [Netcode]Netcode.NetRef`1<class StardewValley.Item>>::get_Count()
                 *          IL_01d8: ldarg.0
                 *          IL_01d9: ldfld class StardewValley.Farmer StardewValley.Characters.Horse/'<>c__DisplayClass31_0'::who
                 *          IL_01de: callvirt instance int32 StardewValley.Farmer::get_CurrentToolIndex()
                 *          IL_01e3: ble IL_02f3
                 *          ...
                 *
                 * Condition to add at the beginning of the test:
                 *      this.who.CurrentToolIndex >= 0
                 */

                if (!found1 && codeInstructions[i].opcode == OpCodes.Ldarg_0
                            && i + 7 < codeInstructions.Count
                            && codeInstructions[i + 2].opcode == OpCodes.Ldfld && codeInstructions[i + 2].operand is FieldInfo { Name: "items" }
                            && codeInstructions[i + 7].opcode == OpCodes.Ble)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, codeInstructions[i + 1].operand);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentToolIndex)).GetGetMethod());
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Blt, codeInstructions[i + 7].operand);

                    found1 = true;
                }

                // Check if farmer can mount horse on the last else.

                /*
                 * Relevant CIL code:
                 *      else
                 *      {
                 *          this.<>4__this.rider = this.who;
                 *              IL_02f3: ldarg.0
                 *              IL_02f4: ldfld class StardewValley.Characters.Horse StardewValley.Characters.Horse/'<>c__DisplayClass31_0'::'<>4__this'
                 *              IL_02f9: ldarg.0
                 *              IL_02fa: ldfld class StardewValley.Farmer StardewValley.Characters.Horse/'<>c__DisplayClass31_0'::who
                 *              IL_02ff: call instance void StardewValley.Characters.Horse::set_rider(class StardewValley.Farmer)
                 *          ...
                 *
                 * Add test:
                 *      else if (!ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse)
                 */

                if (found1 && !found2 && codeInstructions[i].opcode == OpCodes.Ldarg_0
                           && i + 4 < codeInstructions.Count
                           && codeInstructions[i + 4].opcode == OpCodes.Call && codeInstructions[i + 4].operand is MethodInfo { Name: "set_rider" })
                {
                    Label jump = ilGenerator.DefineLabel();

                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod());
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ClickToMoveManager), nameof(ClickToMoveManager.GetOrCreate)));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ClickToMove), nameof(ClickToMove.PreventMountingHorse)).GetGetMethod());
                    yield return new CodeInstruction(OpCodes.Brfalse_S, jump);
                    yield return new CodeInstruction(OpCodes.Ret);

                    codeInstructions[i].labels.Add(jump);

                    found2 = true;
                }

                yield return codeInstructions[i];
            }

            if (!found1 || !found2)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch the delegate for {nameof(Horse)}.{nameof(Horse.checkAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     Keeps data about an <see cref="Horse"/> object.
        /// </summary>
        internal class HorseData
        {
            /// <summary>
            ///     Gets or sets a value indicating whether check action is enabled.
            /// </summary>
            public bool CheckActionEnabled { get; set; } = true;
        }
    }
}
