// -----------------------------------------------------------------------
// <copyright file="GameLocationPatcher.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Harmony;

    using StardewModdingAPI;

    using StardewValley;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class GameLocationPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">
        ///     The Harmony patching API.
        /// </param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
                new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.BeforeanswerDialogueAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
                postfix: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.AfteranswerDialogueAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.cleanupBeforePlayerExit)),
                postfix: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.AftercleanupBeforePlayerExit)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.LowPriorityLeftClick)),
                new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.BeforeLowPriorityLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performTouchAction)),
                transpiler: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.Transpile_performTouchAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), "resetLocalState"),
                postfix: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.AfterresetLocalState)));
        }

        private static void AfteranswerDialogueAction(string questionAndAnswer)
        {
            if (questionAndAnswer == "Eat_Yes" || questionAndAnswer == "Eat_No")
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
            }
        }

        private static void AftercleanupBeforePlayerExit(GameLocation __instance)
        {
            ClickToMoveManager.GetOrCreate(__instance).Reset();
        }

        private static void AfterresetLocalState(GameLocation __instance)
        {
            if (ClickToMoveManager.GetOrCreate(__instance) is not null
                && ClickToMoveManager.GetOrCreate(__instance).Graph is not null)
            {
                ClickToMoveManager.GetOrCreate(__instance).Graph.RefreshBubbles();
            }
        }

        private static bool BeforeanswerDialogueAction(string questionAndAnswer, string[] questionParams)
        {
            if (questionAndAnswer is not null && questionParams is not null && questionParams.Length != 0
                && questionParams[0] == "Minecart")
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = false;
            }

            return true;
        }

        private static bool BeforeLowPriorityLeftClick(GameLocation __instance, ref bool __result)
        {
            if (ClickToMoveManager.GetOrCreate(__instance).ClickHoldActive
                && ClickToMoveManager.GetOrCreate(__instance).Furniture is null)
            {
                __result = false;
                return false;
            }

            return true;
        }

        /// <summary>A method called via Harmony to modify <see cref="GameLocation.performTouchAction" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> Transpile_performTouchAction(
            IEnumerable<CodeInstruction> instructions)
        {
            /* Reset the ClickToMove object associated with the current game location at specific points. */

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(ClickToMoveManager),
                nameof(ClickToMoveManager.GetOrCreate));
            MethodInfo reset = AccessTools.Method(typeof(ClickToMove), nameof(ClickToMove.Reset));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool error = false;

            while (true)
            {
                /*
                * Relevant CIL code:
                *     this.playSound("debuffHit");
                *         IL_05ad: ldarg.0
                *         IL_05ae: ldstr "debuffHit"
                *         IL_05b3: ldc.i4.0
                *         IL_05b4: call instance void StardewValley.GameLocation::playSound(string, valuetype StardewValley.Network.NetAudio / SoundContext)
                *
                * Code to include after:
                *     ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
                */

                int index = codeInstructions.FindIndex(
                    0,
                    ins => ins.opcode == OpCodes.Ldstr && ins.operand is string str && str == "debuffHit");

                if (index < 0 || index + 2 >= codeInstructions.Count
                              || !(codeInstructions[index + 2].opcode == OpCodes.Call
                                   && codeInstructions[index + 2].operand is MethodInfo { Name: "playSound" }))
                {
                    error = true;
                    break;
                }

                codeInstructions.Insert(index + 3, new CodeInstruction(OpCodes.Call, getCurrentLocation));
                codeInstructions.Insert(index + 4, new CodeInstruction(OpCodes.Call, getOrCreate));
                codeInstructions.Insert(index + 5, new CodeInstruction(OpCodes.Ldc_I4_1));
                codeInstructions.Insert(index + 6, new CodeInstruction(OpCodes.Callvirt, reset));

                /*
                * Relevant CIL code:
                *     if (!Game1.newDay && Game1.shouldTimePass() && Game1.player.hasMoved && !Game1.player.passedOut)
                *         ...
                *         IL_0c82: ldfld bool StardewValley.Farmer::passedOut
                *         IL_0c87: brtrue.s IL_0caf
                *
                * Code to include after:
                *     ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
                */

                index += 7;

                if (index >= codeInstructions.Count)
                {
                    error = true;
                    break;
                }

                index = codeInstructions.FindIndex(
                    index,
                    ins => ins.opcode == OpCodes.Ldfld && ins.operand is FieldInfo { Name: "passedOut" });

                if (index < 0 || index + 1 >= codeInstructions.Count)
                {
                    error = true;
                    break;
                }

                codeInstructions.Insert(index + 2, new CodeInstruction(OpCodes.Call, getCurrentLocation));
                codeInstructions.Insert(index + 3, new CodeInstruction(OpCodes.Call, getOrCreate));
                codeInstructions.Insert(index + 4, new CodeInstruction(OpCodes.Ldc_I4_1));
                codeInstructions.Insert(index + 5, new CodeInstruction(OpCodes.Callvirt, reset));

                break;
            }

            if (error)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(GameLocation)}.{nameof(GameLocation.performTouchAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }

            foreach (CodeInstruction instruction in codeInstructions)
            {
                yield return instruction;
            }
        }
    }
}