// -----------------------------------------------------------------------
// <copyright file="UtilityPatcher.cs" company="Raquellcesar">
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

    using Raquellcesar.Stardew.Common;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Characters;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Utility"/> class.
    /// </summary>
    internal class UtilityPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(Utility), nameof(Utility.tryToPlaceItem)),
                transpiler: new HarmonyMethod(typeof(UtilityPatcher), nameof(UtilityPatcher.TranspileTryToPlaceItem)));
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Utility.tryToPlaceItem"/>. It
        ///     introduces a call to <see cref="TryToPlaceItemTranspiler"/> after the player places
        ///     an item.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileTryToPlaceItem(IEnumerable<CodeInstruction> instructions)
        {
            /*
            // Relevant CIL code: Game1.player.reduceActiveItemByOne();
            // IL_0058: call class StardewValley.Farmer StardewValley.Game1::get_player()
            // IL_005d: callvirt instance void StardewValley.Farmer::reduceActiveItemByOne()
            //
            // Insert code after: UtilityPatcher.TryToPlaceItemTranspiler();
            */

            MethodInfo reduceActiveItemByOne = AccessTools.Method(typeof(Farmer), nameof(Farmer.reduceActiveItemByOne));
            MethodInfo tryToPlaceItemTranspiler =
                SymbolExtensions.GetMethodInfo(() => UtilityPatcher.TryToPlaceItemTranspiler());

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Callvirt
                           && codeInstructions[i].operand is MethodInfo { Name: "reduceActiveItemByOne" })
                {
                    yield return codeInstructions[i];

                    yield return new CodeInstruction(OpCodes.Call, tryToPlaceItemTranspiler);

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
        ///     Deselects the farmer's active object after they place a bomb. To avoid placing the
        ///     bomb again by accident.
        /// </summary>
        private static void TryToPlaceItemTranspiler()
        {
            if (Game1.player.ActiveObject != null && (Game1.player.ActiveObject.ParentSheetIndex == ObjectId.CherryBomb
                                                      || Game1.player.ActiveObject.ParentSheetIndex == ObjectId.Bomb
                                                      || Game1.player.ActiveObject.ParentSheetIndex
                                                      == ObjectId.MegaBomb))
            {
                Game1.player.CurrentToolIndex = -1;
            }
        }
    }
}
