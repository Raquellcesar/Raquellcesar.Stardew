// ------------------------------------------------------------------------------------------------
// <copyright file="GameLocationPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Harmony;

    using Microsoft.Xna.Framework;

    using Netcode;

    using StardewModdingAPI;

    using StardewValley;

    /// <summary>
    ///     Applies Harmony patches to the <see cref="GameLocation"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class GameLocationPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
                new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.BeforeAnswerDialogueAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
                postfix: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.AfterAnswerDialogueAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.cleanupBeforePlayerExit)),
                postfix: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.AfterCleanupBeforePlayerExit)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isTileOccupiedForPlacement)),
                transpiler: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.TranspileIsTileOccupiedForPlacement)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performTouchAction)),
                transpiler: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.TranspilePerformTouchAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), "resetLocalState"),
                postfix: new HarmonyMethod(
                    typeof(GameLocationPatcher),
                    nameof(GameLocationPatcher.AfterResetLocalState)));
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="GameLocation.answerDialogueAction"/>.
        ///     It resets the <see cref="ClickToMove"/> object associated to the current game location.
        /// </summary>
        /// <param name="questionAndAnswer">The string identifying a dialogue answer.</param>
        private static void AfterAnswerDialogueAction(string questionAndAnswer)
        {
            if (questionAndAnswer == "Eat_Yes" || questionAndAnswer == "Eat_No")
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
            }
        }

        /// <summary>
        ///     A method called via Harmony after <see
        ///     cref="GameLocation.cleanupBeforePlayerExit"/>. It resets the <see
        ///     cref="ClickToMove"/> object associated to this <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="__instance">The <see cref="GameLocation"/> instance.</param>
        private static void AfterCleanupBeforePlayerExit(GameLocation __instance)
        {
            ClickToMoveManager.GetOrCreate(__instance).Reset();
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="GameLocation.resetLocalState"/>. It
        ///     resets the stored map information in the search graph associated to this <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="__instance">The <see cref="GameLocation"/> instance.</param>
        private static void AfterResetLocalState(GameLocation __instance)
        {
            if (ClickToMoveManager.GetOrCreate(__instance) is not null
                && ClickToMoveManager.GetOrCreate(__instance).Graph is not null)
            {
                ClickToMoveManager.GetOrCreate(__instance).Graph.RefreshBubbles();
            }
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="GameLocation.answerDialogueAction"/>.
        ///     It sets <see cref="ClickToMove.PreventMountingHorse"/> to false.
        /// </summary>
        /// <param name="questionAndAnswer">The string identifying a dialogue answer.</param>
        /// <param name="questionParams">The question parameters.</param>
        private static void BeforeAnswerDialogueAction(string questionAndAnswer, string[] questionParams)
        {
            if (questionAndAnswer is not null && questionParams is not null && questionParams.Length != 0
                && questionParams[0] == "Minecart")
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = false;
            }
        }

        /// <summary>
        ///     Method to be called by <see
        ///     cref="GameLocationPatcher.TranspileIsTileOccupiedForPlacement(IEnumerable{CodeInstruction})"/>.
        ///     For some reason the patch generates invalid code when I attempt to insert directly
        ///     the code wraped in this method, causing either one of these two exceptions:
        ///     "System.InvalidProgramException: JIT Compiler encountered an internal limitation."
        ///     or a "System.InvalidProgramException: Common Language Runtime detected an invalid program.".
        /// </summary>
        /// <param name="gameLocation">
        ///     The <see cref="GameLocation"/> where the object is being placed.
        /// </param>
        /// <param name="tileLocation">The tile coordinates of the placement position.</param>
        /// <param name="toPlace">The <see cref="Object"/> to place.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the tile is occupied by a <see cref="Farmer"/> and
        ///     there is no object to place or it's furniture. Returns <see langword="false"/> otherwise.
        /// </returns>
        private static bool IsTileOccupiedForPlacementTranspiler(GameLocation gameLocation, Vector2 tileLocation, Object toPlace)
        {
            return gameLocation.isTileOccupiedByFarmer(tileLocation) is not null
                && (toPlace is null || (!toPlace.isPassable() && (toPlace.Category == Object.furnitureCategory || toPlace.Category == 0)));
        }

        /// <summary>
        ///     A method called via Harmony to modify <see
        ///     cref="GameLocation.isTileOccupiedForPlacement"/>. It restricts the check for the
        ///     Farmer occupying the placement position to the situation where the object has type
        ///     furniture or has no defined type.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileIsTileOccupiedForPlacement(
            IEnumerable<CodeInstruction> instructions)
        {
            /*
             * Relevant CIL code:
             *     if (this.isTileOccupiedByFarmer(tileLocation) != null && (toPlace == null || !toPlace.isPassable()))
             *         IL_00c1: ldarg.0
             *         IL_00c2: ldarg.1
             *         IL_00c3: call instance class StardewValley.Farmer StardewValley.GameLocation::isTileOccupiedByFarmer(valuetype [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Vector2)
             *         IL_00c8: brfalse.s IL_00d7
             *         IL_00ca: ldarg.2
             *         IL_00cb: brfalse.s IL_00d5
             *         IL_00cd: ldarg.2
             *         IL_00ce: callvirt instance bool StardewValley.Object::isPassable()
             *         IL_00d3: brtrue.s IL_00d7
             *
             * Replace with:
             *     if (this.isTileOccupiedByFarmer(tileLocation) != null && (toPlace == null || (!toPlace.isPassable() && (toPlace.Category == Object.furnitureCategory || toPlace.Category == 0))))
             */

            MethodInfo isTileOccupiedForPlacementTranspiler = AccessTools.Method(typeof(GameLocationPatcher), nameof(GameLocationPatcher.IsTileOccupiedForPlacementTranspiler));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found
                    && codeInstructions[i].opcode == OpCodes.Ldarg_0
                    && i + 9 < codeInstructions.Count
                    && codeInstructions[i + 8].opcode == OpCodes.Brtrue)
                {
                    object jumpEndIf = codeInstructions[i + 8].operand;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Call, isTileOccupiedForPlacementTranspiler);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, jumpEndIf);

                    i += 9;

                    found = true;
                }

                yield return codeInstructions[i];
            }

            if (!found)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(GameLocation)}.{nameof(GameLocation.isTileOccupiedForPlacement)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="GameLocation.performTouchAction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspilePerformTouchAction(
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
