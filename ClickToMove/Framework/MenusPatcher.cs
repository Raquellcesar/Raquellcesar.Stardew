// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="MenusPatcher.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of __instance source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;

    using Harmony;

    using Microsoft.Xna.Framework;

    using StardewValley;
    using StardewValley.Locations;
    using StardewValley.Menus;
    using StardewValley.Tools;

    internal static class MenusPatcher
    {
        private static int nextToolIndex = int.MinValue;

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">
        ///     The Harmony patching API.
        /// </param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(DayTimeMoneyBox), nameof(DayTimeMoneyBox.receiveLeftClick)),
                new HarmonyMethod(typeof(MenusPatcher), nameof(MenusPatcher.BeforeDayTimeMoneyBoxReceiveLeftClick)));

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

        private static void AfterToolbarUpdate(GameTime time)
        {
            if (!Game1.player.usingTool && MenusPatcher.nextToolIndex != int.MinValue)
            {
                Game1.player.CurrentToolIndex = MenusPatcher.nextToolIndex;
                MenusPatcher.nextToolIndex = int.MinValue;

                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClearAutoSelectTool();

                if (Game1.player.CurrentTool is not null && Game1.player.CurrentTool is MeleeWeapon weapon)
                {
                    ClickToMove.MostRecentlyChosenMeleeWeapon = weapon;
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

        private static bool BeforeShopMenuReceiveLeftClick(ShopMenu __instance, int x, int y)
        {
            if (__instance.upperRightCloseButton.containsPoint(x, y))
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ResetRotatingFurniture();
            }

            return true;
        }

        private static bool BeforeToolbarReceiveLeftClick(int x, int y, List<ClickableComponent> ___buttons)
        {
            if (Game1.IsChatting || Game1.currentLocation is MermaidHouse || Game1.player.isEating || !Game1.displayFarmer || ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickHoldActive)
            {
                return false;
            }

            if (Game1.player.usingTool.Value)
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
                            ClickToMove.MostRecentlyChosenMeleeWeapon = weapon;
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
    }
}
