﻿// -----------------------------------------------------------------------
// <copyright file="MenusPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Harmony;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Locations;
    using StardewValley.Menus;
    using StardewValley.Tools;

    /// <summary>
    ///     Encapsulates Harmony patches for Menus in the game.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class MenusPatcher
    {
        /// <summary>
        ///     The index of the tool to be selected on the toolbar update.
        /// </summary>
        private static int nextToolIndex = int.MinValue;

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(DayTimeMoneyBox), nameof(DayTimeMoneyBox.receiveLeftClick)),
                new HarmonyMethod(typeof(MenusPatcher), nameof(MenusPatcher.BeforeDayTimeMoneyBoxReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(NumberSelectionMenu), nameof(NumberSelectionMenu.receiveLeftClick)),
                transpiler: new HarmonyMethod(typeof(MenusPatcher), nameof(MenusPatcher.TranspileNumberSelectionMenuReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick)),
                new HarmonyMethod(typeof(MenusPatcher), nameof(MenusPatcher.BeforeShopMenuReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Toolbar), nameof(Toolbar.receiveLeftClick)),
                new HarmonyMethod(typeof(MenusPatcher), nameof(MenusPatcher.BeforeToolbarReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Toolbar), nameof(Toolbar.update)),
                postfix: new HarmonyMethod(typeof(MenusPatcher), nameof(MenusPatcher.AfterToolbarUpdate)));
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Toolbar.update"/> This method equips
        ///     the farmer with a tool previously chosen, when the farmer was using another tool.
        /// </summary>
        private static void AfterToolbarUpdate()
        {
            if (!Game1.player.UsingTool && MenusPatcher.nextToolIndex != int.MinValue)
            {
                Game1.player.CurrentToolIndex = MenusPatcher.nextToolIndex;
                MenusPatcher.nextToolIndex = int.MinValue;

                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClearAutoSelectTool();

                if (Game1.player.CurrentTool is MeleeWeapon weapon)
                {
                    ClickToMove.LastMeleeWeapon = weapon;
                }
            }
        }

        private static bool BeforeDayTimeMoneyBoxReceiveLeftClick(DayTimeMoneyBox __instance, int x, int y)
        {
            if (Game1.currentLocation is not null && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickHoldActive)
            {
                return false;
            }

            if (Game1.player.visibleQuestCount > 0 && __instance.questButton.containsPoint(x, y) && Game1.player.CanMove
                && !Game1.dialogueUp && !Game1.eventUp && Game1.farmEvent is null)
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
                ClickToMoveManager.OnScreenButtonClicked = true;
            }
            else if (Game1.options.zoomButtons && (__instance.zoomInButton.containsPoint(x, y) || __instance.zoomOutButton.containsPoint(x, y)))
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
                ClickToMoveManager.OnScreenButtonClicked = true;
            }

            return true;
        }

        private static void BeforeShopMenuReceiveLeftClick(ShopMenu __instance, int x, int y)
        {
            if (__instance.upperRightCloseButton.containsPoint(x, y))
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ResetRotatingFurniture();
            }
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Toolbar.receiveLeftClick"/> that
        ///     replaces it. This method allows the user to deselect an equipped object so that the
        ///     farmer doesn't have any equipped tool or active object. It also allows deferred
        ///     selection of items. If the farmer selects an item while using a tool, that item will
        ///     be later equipped when the toolbar updates.
        /// </summary>
        /// <returns>
        ///     Returns false, terminating prefixes and skipping the execution of the original
        ///     method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeToolbarReceiveLeftClick(int x, int y, List<ClickableComponent> ___buttons)
        {
            if (Game1.IsChatting || Game1.currentLocation is MermaidHouse || Game1.player.isEating || !Game1.displayFarmer || ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickHoldActive)
            {
                return false;
            }

            if (Game1.player.UsingTool)
            {
                foreach (ClickableComponent button in ___buttons)
                {
                    if (button.containsPoint(x, y))
                    {
                        ClickToMoveManager.OnScreenButtonClicked = true;

                        int toolIndex = Convert.ToInt32(button.name);

                        if (Game1.player.CurrentToolIndex == toolIndex)
                        {
                            MenusPatcher.nextToolIndex = -1;
                        }
                        else
                        {
                            MenusPatcher.nextToolIndex = toolIndex;
                        }

                        break;
                    }
                }

                return false;
            }

            foreach (ClickableComponent button in ___buttons)
            {
                if (button.containsPoint(x, y))
                {
                    ClickToMoveManager.OnScreenButtonClicked = true;

                    int toolIndex = Convert.ToInt32(button.name);

                    if (Game1.player.CurrentToolIndex == toolIndex)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                    else
                    {
                        Game1.player.CurrentToolIndex = toolIndex;

                        ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClearAutoSelectTool();

                        if (Game1.player.CurrentTool is MeleeWeapon weapon)
                        {
                            ClickToMove.LastMeleeWeapon = weapon;
                        }

                        if (Game1.player.ActiveObject != null)
                        {
                            Game1.player.showCarrying();
                            Game1.playSound("pickUpItem");
                        }
                        else
                        {
                            Game1.player.showNotCarrying();
                            Game1.playSound("stoneStep");
                        }
                    }

                    break;
                }
            }

            return false;
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="NumberSelectionMenu.receiveLeftClick"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileNumberSelectionMenuReceiveLeftClick(
            IEnumerable<CodeInstruction> instructions)
        {
            // Set Game1.player.canMove to true when exiting the menu by clicking the ok button.

            /*
             * Relevant CIL code:
             *      if (this.cancelButton.containsPoint(x, y))
             *          IL_0176: ldarg.0
             *          IL_0177: ldfld class StardewValley.Menus.ClickableTextureComponent StardewValley.Menus.NumberSelectionMenu::cancelButton
             *
             * Code to insert before:
             *      Game1.player.canMove = true;
             */

            FieldInfo canMove = AccessTools.Field(typeof(Farmer), nameof(Farmer.canMove));

            MethodInfo getPlayer =
                AccessTools.Property(typeof(Game1), nameof(Game1.player)).GetGetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Ldarg_0
                           && i + 1 < codeInstructions.Count
                           && codeInstructions[i + 1].opcode == OpCodes.Ldfld
                           && codeInstructions[i + 1].operand is FieldInfo { Name: "cancelButton" })
                {
                    yield return new CodeInstruction(OpCodes.Call, getPlayer);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Stfld, canMove);

                    found = true;
                }

                yield return codeInstructions[i];
            }

            if (!found)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(NumberSelectionMenu)}.{nameof(NumberSelectionMenu.receiveLeftClick)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }
    }
}
