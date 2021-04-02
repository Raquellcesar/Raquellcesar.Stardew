// -----------------------------------------------------------------------
// <copyright file="ToolsPatcher.cs" company="Raquellcesar">
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

    using Harmony;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Tools;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Tool"/> classes.
    /// </summary>
    internal static class ToolsPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(FishingRod), "doDoneFishing"),
                postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterDoDoneFishing)));

            harmony.Patch(
                AccessTools.Method(typeof(Wand), nameof(Wand.DoFunction)),
                postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterWandDoFunction)));

            /*harmony.Patch(
                AccessTools.Method(typeof(Wand), nameof(Wand.DoFunction)),
                transpiler: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.TranspileWandDoFunction)));*/
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="FishingRod.doDoneFishing"/>. It resets
        ///     the state of the <see cref="ClickToMove"/> object associated with the current game location.
        /// </summary>
        private static void AfterDoDoneFishing()
        {
            ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Wand.DoFunction"/>. It unequips the
        ///     <see cref="Wand"/>.
        /// </summary>
        private static void AfterWandDoFunction()
        {
            Game1.player.CurrentToolIndex = -1;
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Wand.DoFunction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileWandDoFunction(IEnumerable<CodeInstruction> instructions)
        {
            // Set the farmer CurrentToolIndex to -1.

            /*
            * Relevant CIL code:
            *     location.playSound("wand");
            *         IL_0102: ldarg.1
            *         IL_0103: ldstr "wand"
            *         IL_0108: ldc.i4.0
            *         IL_0109: callvirt instance void StardewValley.GameLocation::playSound(string, valuetype StardewValley.Network.NetAudio / SoundContext)
            *
            * Code to insert after:
            *     Game1.player.CurrentToolIndex = -1;
            */

            MethodInfo getPlayer = AccessTools.Property(typeof(Game1), nameof(Game1.player)).GetGetMethod();
            MethodInfo setCurrentToolIndex =
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentToolIndex)).GetSetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Callvirt
                           && codeInstructions[i].operand is MethodInfo { Name: "playSound" })
                {
                    yield return codeInstructions[i];

                    object jump = codeInstructions[i].operand;

                    yield return new CodeInstruction(OpCodes.Call, getPlayer);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
                    yield return new CodeInstruction(OpCodes.Callvirt, setCurrentToolIndex);

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
                    $"Failed to patch {nameof(Wand)}.{nameof(Wand.DoFunction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }
    }
}
