// -----------------------------------------------------------------------
// <copyright file="FarmerSpritePatcher.cs" company="Raquellcesar">
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

    using Microsoft.Xna.Framework;

    using StardewModdingAPI;

    using StardewValley;

    /// <summary>Encapsulates Harmony patches for the <see cref="FarmerSprite"/> class.</summary>
    internal static class FarmerSpritePatcher
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
                AccessTools.Method(typeof(FarmerSprite), nameof(FarmerSprite.animateOnce), new[] { typeof(GameTime) }),
                transpiler: new HarmonyMethod(
                    typeof(FarmerSpritePatcher),
                    nameof(FarmerSpritePatcher.TranspileAnimateOnce)));
        }

        /// <summary>
        ///     Changes the farmer's equipped tool to the last used tool.
        ///     This is used to get back to the tool used before auto selection of a different tool.
        /// </summary>
        /// <param name="who">The <see cref="Farmer"/> instance.</param>
        private static void SwitchBackToLastTool(Farmer who)
        {
            if (who.IsMainPlayer && Game1.currentLocation is not null)
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).SwitchBackToLastTool();
            }
        }

        /// <summary>A method called via Harmony to modify <see cref="FarmerSprite.animateOnce" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileAnimateOnce(IEnumerable<CodeInstruction> instructions)
        {
            // Switch back to last tool.

            /*
            * Relevant CIL code:
            *     if (base.currentAnimationIndex > this.currentAnimationFrames - 1)
            *         IL_0056: ldarg.0
            *         IL_0057: ldfld int32 StardewValley.AnimatedSprite::currentAnimationIndex
            *         IL_005c: ldarg.0
            *         IL_005d: ldfld int32 StardewValley.FarmerSprite::currentAnimationFrames
            *         IL_0062: ldc.i4.1
            *         IL_0063: sub
            *         IL_0064: ble IL_014c
            *
            * Code to include after:
            *     FarmerSpritePatcher.SwitchBackToLastTool(this.owner);
            */

            FieldInfo owner = AccessTools.Field(typeof(FarmerSprite), "owner");
            MethodInfo switchBackToLastTool =
                SymbolExtensions.GetMethodInfo(() => FarmerSpritePatcher.SwitchBackToLastTool(null));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Ldfld && codeInstructions[i].operand is FieldInfo { Name: "currentAnimationFrames" }
                && i + 3 < codeInstructions.Count)
                {
                    yield return codeInstructions[i];
                    i++;
                    yield return codeInstructions[i];
                    i++;
                    yield return codeInstructions[i];
                    i++;
                    yield return codeInstructions[i];

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, owner);
                    yield return new CodeInstruction(OpCodes.Call, switchBackToLastTool);

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
                        $"Failed to patch {nameof(FarmerSprite)}.{nameof(FarmerSprite.animateOnce)}.\nThe point of injection was not found.",
                        LogLevel.Error);
            }
        }
    }
}
