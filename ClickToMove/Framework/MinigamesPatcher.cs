// -----------------------------------------------------------------------
// <copyright file="MinigamesPatcher.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Reflection;

    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Input;

    using StardewValley;
    using StardewValley.Minigames;
    using StardewValley.Tools;

    internal static class MinigamesPatcher
    {
        private static bool aiming;

        private static bool usingSlingshot;

        public static bool LeftClickNextUpdateFishingGame { get; set; }

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">
        ///     The Harmony patching API.
        /// </param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Constructor(typeof(FishingGame)),
                postfix: new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.AfterFishingGameConstructor)));

            // Can't access the constructor using AccessTools, as was done with FishingGame,
            // because it will originate an AmbiguousMatchException, since there's a static
            // constructor with the same signature being implemented by the compiler under the hood.
            harmony.Patch(
                typeof(TargetGame).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new Type[0],
                    new ParameterModifier[0]),
                postfix: new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.AfterTargetGameConstructor)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.receiveKeyPress)),
                new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.BeforeReceiveKeyPress)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.receiveLeftClick)),
                new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.BeforeReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.releaseLeftClick)),
                new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.BeforereleaseLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.releaseLeftClick)),
                postfix: new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.AfterReleaseLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.tick)),
                new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.BeforeTick)));
        }

        private static void AfterFishingGameConstructor()
        {
            MinigamesPatcher.LeftClickNextUpdateFishingGame = false;
        }

        private static void AfterReleaseLeftClick(GameLocation ___location)
        {
            ClickToMoveManager.GetOrCreate(___location).Reset();
        }

        private static void AfterTargetGameConstructor()
        {
            MinigamesPatcher.aiming = false;
            MinigamesPatcher.usingSlingshot = false;
        }

        private static bool BeforeReceiveKeyPress(
            ref int ___showResultsTimer,
            int ___gameEndTimer,
            int ___timerToStart,
            Keys k)
        {
            if (Game1.options.doesInputListContain(Game1.options.menuButton, k))
            {
                Game1.playSound("fishEscape");

                ___showResultsTimer = 1;
            }

            if (___showResultsTimer > 0 || ___gameEndTimer < 0)
            {
                Game1.player.Halt();

                return false;
            }

            if (!MinigamesPatcher.aiming && Game1.player.movementDirections.Count < 2 && !Game1.player.UsingTool
                && ___timerToStart <= 0)
            {
                if (Game1.options.doesInputListContain(Game1.options.moveUpButton, k))
                {
                    Game1.player.setMoving(1);
                }

                if (Game1.options.doesInputListContain(Game1.options.moveRightButton, k))
                {
                    Game1.player.setMoving(2);
                }

                if (Game1.options.doesInputListContain(Game1.options.moveDownButton, k))
                {
                    Game1.player.setMoving(4);
                }

                if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, k))
                {
                    Game1.player.setMoving(8);
                }
            }

            if (Game1.options.doesInputListContain(Game1.options.runButton, k))
            {
                Game1.player.setRunning(true);
            }

            if (!Game1.player.usingTool && Game1.options.doesInputListContain(Game1.options.useToolButton, k))
            {
                ((Slingshot)Game1.player.CurrentTool).beginUsing(null, 0, 0, Game1.player);

                MinigamesPatcher.aiming = true;
            }

            return false;
        }

        private static bool BeforeReceiveLeftClick(int x, int y, ref int ___showResultsTimer)
        {
            if (___showResultsTimer < 0)
            {
                MinigamesPatcher.usingSlingshot = false;

                if (Game1.currentMinigame is not null && Game1.currentMinigame is TargetGame && Vector2.Distance(
                        value2: new Vector2(
                            Game1.player.getStandingX() - Game1.viewport.X,
                            Game1.player.getStandingY() - Game1.viewport.Y),
                        value1: new Vector2(x, y)) < Game1.tileSize)
                {
                    MinigamesPatcher.usingSlingshot = true;
                    Game1.pressUseToolButton();
                }
            }
            else if (___showResultsTimer > 16000)
            {
                ___showResultsTimer = 16001;
            }
            else if (___showResultsTimer > 14000)
            {
                ___showResultsTimer = 14001;
            }
            else if (___showResultsTimer > 11000)
            {
                ___showResultsTimer = 11001;
            }
            else if (___showResultsTimer > 9000)
            {
                ___showResultsTimer = 9001;
            }
            else if (___showResultsTimer < 9000 && ___showResultsTimer > 1000)
            {
                ___showResultsTimer = 1500;
                Game1.player.freezePause = 1500;
                Game1.playSound("smallSelect");
            }

            return false;
        }

        private static bool BeforereleaseLeftClick()
        {
            if (!MinigamesPatcher.usingSlingshot)
            {
                return false;
            }

            MinigamesPatcher.usingSlingshot = false;

            return true;
        }

        private static bool BeforeTick()
        {
            if (MinigamesPatcher.aiming)
            {
                if (!Game1.input.GetGamePadState().IsButtonDown(Buttons.X))
                {
                    MinigamesPatcher.aiming = false;
                    ((Slingshot)Game1.player.CurrentTool).DoFunction(Game1.currentLocation, 0, 0, 1, Game1.player);
                    TargetGame.shotsFired++;
                }
                else
                {
                    Slingshot slingshot = (Slingshot)Game1.player.CurrentTool;

                    ClickToMoveManager.Reflection.GetMethod(slingshot, "updateAimPos").Invoke();
                }
            }

            return true;
        }
    }
}