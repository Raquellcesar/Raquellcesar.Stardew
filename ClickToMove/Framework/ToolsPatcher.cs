// ------------------------------------------------------------------------------------------------
// <copyright file="ToolsPatcher.cs" company="Raquellcesar">
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

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Tools;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Tool"/> classes.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
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
                postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterFishingRodDoDoneFishing)));

            harmony.Patch(
                AccessTools.Method(typeof(FishingRod), nameof(FishingRod.tickUpdate)),
                transpiler: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.TranspileTickUpdate)));

            harmony.Patch(
               AccessTools.Method(typeof(MilkPail), "doFinish"),
               postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterMilkPailDoFinish)));

            harmony.Patch(
               AccessTools.Method(typeof(Shears), "doFinish"),
               postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterShearsDoFinish)));

            harmony.Patch(
                AccessTools.Method(typeof(Wand), nameof(Wand.DoFunction)),
                postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterWandDoFunction)));
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="FishingRod.tickUpdate"/>. It
        ///     prevents the method considering the mouse left button released when the simulated button is pressed.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileTickUpdate(
            IEnumerable<CodeInstruction> instructions)
        {
            /*
             * Relevant CIL code:
             *     if (who.IsLocalPlayer && ((!this.usedGamePadToCast && Game1.input.GetMouseState().LeftButton == ButtonState.Released) || (this.usedGamePadToCast && Game1.options.gamepadControls && Game1.input.GetGamePadState().IsButtonUp(Buttons.X))) && Game1.areAllOfTheseKeysUp(Game1.GetKeyboardState(), Game1.options.useToolButton))
             *         ...
             *         IL_07d6: ldfld bool StardewValley.Tools.FishingRod::usedGamePadToCast
             *         IL_07db: brtrue.s IL_07f1
             *         IL_07dd: ldsfld class StardewValley.InputState StardewValley.Game1::input
             *         IL_07e2: callvirt instance valuetype [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Input.MouseState StardewValley.InputState::GetMouseState()
             *         IL_07e7: stloc.0
             *         IL_07e8: ldloca.s 0
             *         IL_07ea: call instance valuetype [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Input.ButtonState [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Input.MouseState::get_LeftButton()
             *         IL_07ef: brfalse.s IL_0828
             *         ...
             *
             * Replace with:
             *     if (who.IsLocalPlayer && ((!this.usedGamePadToCast && Game1.input.GetMouseState().LeftButton == ButtonState.Released && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.UseToolButtonPressed) || (this.usedGamePadToCast && Game1.options.gamepadControls && Game1.input.GetGamePadState().IsButtonUp(Buttons.X))) && Game1.areAllOfTheseKeysUp(Game1.GetKeyboardState(), Game1.options.useToolButton))
             */

            MethodInfo getCurrentLocation = AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(typeof(ClickToMoveManager), nameof(ClickToMoveManager.GetOrCreate));
            MethodInfo getClickKeyStates = AccessTools.Property(typeof(ClickToMove), nameof(ClickToMove.ClickKeyStates)).GetGetMethod();
            MethodInfo getUseToolButtonPressed = AccessTools.Property(typeof(ClickToMoveKeyStates), nameof(ClickToMoveKeyStates.UseToolButtonPressed)).GetGetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found
                    && codeInstructions[i].opcode == OpCodes.Ldfld
                    && i + 1 < codeInstructions.Count
                    && codeInstructions[i + 1].opcode == OpCodes.Brtrue
                    && codeInstructions[i + 6].operand is MethodInfo { Name: "get_LeftButton" }
                    && codeInstructions[i + 7].opcode == OpCodes.Brfalse)
                {
                    object jumpNextOrCondition = codeInstructions[i + 1].operand;
                    object jumpNextAndCondition = codeInstructions[i + 7].operand;

                    for (int j = 0; j < 7; j++)
                    {
                        yield return codeInstructions[i++];
                    }

                    yield return new CodeInstruction(OpCodes.Brtrue_S, jumpNextOrCondition);
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Callvirt, getClickKeyStates);
                    yield return new CodeInstruction(OpCodes.Callvirt, getUseToolButtonPressed);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, jumpNextAndCondition);

                    i++;

                    found = true;
                }

                yield return codeInstructions[i];
            }

            if (!found)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(FishingRod)}.{nameof(FishingRod.tickUpdate)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="FishingRod"/>.doDoneFishing. It resets
        ///     the state of the <see cref="ClickToMove"/> object associated with the current game location.
        /// </summary>
        private static void AfterFishingRodDoDoneFishing(FishingRod __instance)
        {
            ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="MilkPail"/>.doFinish. It switchs the
        ///     Farmer's equipped tool to the one that they had equipped before this one was autoselected.
        /// </summary>
        private static void AfterMilkPailDoFinish()
        {
            ClickToMoveManager.GetOrCreate(Game1.currentLocation).SwitchBackToLastTool();
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Shears"/>.doFinish. It switchs the
        ///     Farmer's equipped tool to the one that they had equipped before this one was autoselected.
        /// </summary>
        private static void AfterShearsDoFinish()
        {
            ClickToMoveManager.GetOrCreate(Game1.currentLocation).SwitchBackToLastTool();
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Wand.DoFunction"/>. It unequips the
        ///     <see cref="Wand"/>.
        /// </summary>
        private static void AfterWandDoFunction()
        {
            Game1.player.CurrentToolIndex = -1;
        }
    }
}
