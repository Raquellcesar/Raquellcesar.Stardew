// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ClickToMovePatcher.cs">
//     Copyright (c) 2021 Raquellcesar
//
//     Use of this source code is governed by an MIT-style license
//     that can be found in the LICENSE file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.CompilerServices;

    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using Microsoft.Xna.Framework.Input;

    using Netcode;

    using Raquellcesar.Stardew.Common;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Characters;
    using StardewValley.Locations;
    using StardewValley.Menus;
    using StardewValley.Minigames;
    using StardewValley.Objects;
    using StardewValley.Tools;
    using StardewValley.Util;

    using xTile.Dimensions;

    using SObject = StardewValley.Object;

    /// <summary>
    ///     Applies Harmony patches to the game.
    /// </summary>
    internal static class ClickToMovePatcher
    {
        /// <summary>
        ///     Associates new properties to <see cref="Farmer"/> objects at runtime.
        /// </summary>
        private static readonly ConditionalWeakTable<Farmer, FarmerData> FarmersData =
            new ConditionalWeakTable<Farmer, FarmerData>();

        /// <summary>
        ///     Associates new properties to <see cref="Horse"/> objects at runtime.
        /// </summary>
        private static readonly ConditionalWeakTable<Horse, HorseData> HorsesData =
            new ConditionalWeakTable<Horse, HorseData>();

        /// <summary>
        ///     A reference to the private method <see cref="Game1.addHour"/>. Needed for the
        ///     reimplementation of <see cref="Game1.UpdateControlInput"/>.
        /// </summary>
        private static IReflectedMethod addHour;

        /// <summary>
        ///     A reference to the private method <see cref="Game1.addMinute"/>. Needed for the
        ///     reimplementation of <see cref="Game1.UpdateControlInput"/>.
        /// </summary>
        private static IReflectedMethod addMinute;

        /// <summary>
        ///     A reference to the private method <see cref="Game1.checkIfDialogueIsQuestion"/>. Needed for the
        ///     reimplementation of <see cref="Game1.pressActionButton"/>.
        /// </summary>
        private static IReflectedMethod checkIfDialogueIsQuestion;

        private static bool controlPadActionButtonPressed;

        /// <summary>
        ///     The public SMAPI APIs.
        /// </summary>
        private static IModHelper helper;

        /// <summary>
        ///     The last real down state of the mouse left button, ignoring eventual mod suppressions.
        /// </summary>
        private static bool lastMouseLeftButtonDown;

        /// <summary>
        ///     Encapsulates logging for the Harmony patch.
        /// </summary>
        private static IMonitor monitor;

        /// <summary>
        ///     The index of the tool to be selected on the toolbar update.
        /// </summary>
        private static int nextToolIndex = int.MinValue;

        /// <summary>
        ///     The manager of all PathFindingController objects.
        /// </summary>
        private static PathFindingManager pathFindingManager;

        /// <summary>
        ///     A reference to the private property <see cref="Game1.thumbstickToMouseModifier"/>.
        ///     Needed for the reimplementation of <see cref="Game1.UpdateControlInput"/>.
        /// </summary>
        private static IReflectedProperty<float> thumbstickToMouseModifier;

        private static bool aiming;

        private static bool usingSlingshot;

        public static bool LeftClickNextUpdateFishingGame { get; set; }

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        /// <param name="monitor">Encapsulates logging for the Harmony patch.</param>
        public static void Hook(HarmonyInstance harmony, IModHelper helper, IMonitor monitor, PathFindingManager pathFindingManager)
        {
            ClickToMovePatcher.helper = helper;
            ClickToMovePatcher.monitor = monitor;
            ClickToMovePatcher.pathFindingManager = pathFindingManager;

            ClickToMovePatcher.thumbstickToMouseModifier =
                ClickToMovePatcher.helper.Reflection.GetProperty<float>(typeof(Game1), "thumbstickToMouseModifier");

            ClickToMovePatcher.addHour = ClickToMovePatcher.helper.Reflection.GetMethod(typeof(Game1), "addHour");
            ClickToMovePatcher.addMinute = ClickToMovePatcher.helper.Reflection.GetMethod(typeof(Game1), "addMinute");
            ClickToMovePatcher.checkIfDialogueIsQuestion =
                ClickToMovePatcher.helper.Reflection.GetMethod(typeof(Game1), "checkIfDialogueIsQuestion");

            harmony.Patch(
                AccessTools.Method(typeof(Building), nameof(Building.dayUpdate)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterBuildingDayUpdate)));

            harmony.Patch(
                AccessTools.Method(typeof(BusStop), nameof(BusStop.answerDialogue)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeBusStopAnswerDialogue)));

            harmony.Patch(
                AccessTools.Method(typeof(BusStop), nameof(BusStop.checkAction)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeBusStopCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(BusStop), "playerReachedBusDoor"),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeBusStopPlayerReachedBusDoor)));

            harmony.Patch(
                AccessTools.Method(typeof(Child), nameof(Child.checkAction)),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileChildCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(CommunityCenter), "afterViewportGetsToJunimoNotePosition"),
                postfix: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.AfterCommunityCenterAfterViewportGetsToJunimoNotePosition)));

            harmony.Patch(
                AccessTools.Method(typeof(CommunityCenter), "resetLocalState"),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterCommunityCenterResetLocalState)));

            harmony.Patch(
                AccessTools.Method(typeof(DayTimeMoneyBox), nameof(DayTimeMoneyBox.receiveLeftClick)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeDayTimeMoneyBoxReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Event), "addSpecificTemporarySprite"),
                transpiler: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.TranspileEventAddSpecificTemporarySprite)));

            harmony.Patch(
                AccessTools.Method(typeof(Event), nameof(Event.checkAction)),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileEventCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Event), nameof(Event.checkForCollision)),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileEventCheckForCollision)));

            harmony.Patch(
                AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.draw), new[] { typeof(SpriteBatch) }),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeFarmAnimalDraw)));

            // Can't access the constructor using AccessTools, because it will originate an
            // AmbiguousMatchException, since there's a static constructor with the same signature
            // being implemented by the compiler under the hood.
            harmony.Patch(
                typeof(Farmer).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new Type[0],
                    new ParameterModifier[0]),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterFarmerConstructor)));

            harmony.Patch(
                AccessTools.Constructor(
                    typeof(Farmer),
                    new[] { typeof(FarmerSprite), typeof(Vector2), typeof(int), typeof(string), typeof(List<Item>), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterFarmerConstructor)));

            harmony.Patch(
                AccessTools.Property(typeof(Farmer), nameof(Farmer.ActiveObject)).GetGetMethod(),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeFarmerGetActiveObject)));

            harmony.Patch(
                AccessTools.Method(typeof(Farmer), nameof(Farmer.completelyStopAnimatingOrDoingAction)),
                postfix: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.AfterFarmerCompletelyStopAnimatingOrDoingAction)));

            harmony.Patch(
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentItem)).GetGetMethod(),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeFarmerGetCurrentItem)));

            harmony.Patch(
                AccessTools.Method(typeof(Farmer), nameof(Farmer.forceCanMove)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterFarmerForceCanMove)));

            harmony.Patch(
                AccessTools.Method(typeof(Farmer), "performSickAnimation"),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeParmerPerformSickAnimation)));

            harmony.Patch(
                AccessTools.Method(typeof(FarmerSprite), nameof(FarmerSprite.animateOnce), new[] { typeof(GameTime) }),
                transpiler: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.TranspileFarmerSpriteAnimateOnce)));

            harmony.Patch(
                AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.loadSpouseRoom)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterFarmHouseLoadSpouseRoom)));

            harmony.Patch(
                AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.setMapForUpgradeLevel)),
                postfix: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.AfterFarmHouseSetMapForUpgradeLevel)));

            harmony.Patch(
                AccessTools.Constructor(typeof(FishingGame)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterFishingGameConstructor)));

            harmony.Patch(
                AccessTools.Method(typeof(FishingRod), "doDoneFishing"),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterFishingRodDoDoneFishing)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), "_update"),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileGame1Update)));

            harmony.Patch(
                AccessTools.Property(typeof(Game1), nameof(Game1.currentMinigame)).GetSetMethod(),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileGame1SetCurrentMinigame)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.didPlayerJustLeftClick)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterGame1DidPlayerJustLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.didPlayerJustRightClick)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterGame1DidPlayerJustRightClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.drawObjectDialogue)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterGame1DrawObjectDialogue)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.exitActiveMenu)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterGame1ExitActiveMenu)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.pressActionButton)),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileGame1PressActionButton)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.pressUseToolButton)),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileGame1PressUseToolButton)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), "UpdateControlInput"),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeGame1UpdateControlInput)));

            harmony.Patch(
                AccessTools.Method(
                    typeof(Game1),
                    "warpFarmer",
                    new[] { typeof(LocationRequest), typeof(int), typeof(int), typeof(int) }),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeGame1WarpFarmer)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
                new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.BeforeGameLocationAnswerDialogueAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
                postfix: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.AfterGameLocationAnswerDialogueAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.cleanupBeforePlayerExit)),
                postfix: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.AfterGameLocationCleanupBeforePlayerExit)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.LowPriorityLeftClick)),
                new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.BeforeGameLocationLowPriorityLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performTouchAction)),
                transpiler: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.TranspileGameLocationPerformTouchAction)));

            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), "resetLocalState"),
                postfix: new HarmonyMethod(
                    typeof(ClickToMovePatcher),
                    nameof(ClickToMovePatcher.AfterGameLocationResetLocalState)));

            harmony.Patch(
                AccessTools.Method(typeof(Horse), nameof(Horse.checkAction)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeHorseCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(MineShaft), nameof(MineShaft.loadLevel)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterMineShaftLoadLevel)));

            harmony.Patch(
                AccessTools.Method(typeof(Mountain), nameof(Mountain.checkAction)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeMountainCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(MovieTheater), nameof(MovieTheater.performTouchAction)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeMovieTheaterPerformTouchAction)));

            harmony.Patch(
                AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeShopMenuReceiveLeftClick)));

            // Can't access the constructor using AccessTools, because it will originate an AmbiguousMatchException,
            // since there's a static constructor with the same signature being implemented by the compiler under the hood.
            harmony.Patch(
                typeof(TargetGame).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new Type[0],
                    new ParameterModifier[0]),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterTargetGameConstructor)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.receiveKeyPress)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeTargetGameReceiveKeyPress)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.receiveLeftClick)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeTargetGameReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.releaseLeftClick)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeTargetGameReleaseLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.releaseLeftClick)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterTargetGameReleaseLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(TargetGame), nameof(TargetGame.tick)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeTargetGameTick)));

            harmony.Patch(
                AccessTools.Method(typeof(Toolbar), nameof(Toolbar.receiveLeftClick)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeToolbarReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Toolbar), nameof(Toolbar.update)),
                postfix: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.AfterToolbarUpdate)));

            harmony.Patch(
                AccessTools.Method(typeof(Town), nameof(Town.checkAction)),
                new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.BeforeTownCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Utility), nameof(Utility.tryToPlaceItem)),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileTryToPlaceItem)));

            harmony.Patch(
                AccessTools.Method(typeof(Wand), nameof(Wand.DoFunction)),
                transpiler: new HarmonyMethod(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.TranspileWandDoFunction)));
        }

        /// <summary>A method called via Harmony to modify <see cref="GameLocation.performTouchAction" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileGameLocationPerformTouchAction(
            IEnumerable<CodeInstruction> instructions)
        {
            // Reset the PathFindingController object associated with the current game location at specific points.
            FieldInfo pathFindingManager = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.pathFindingManager));

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(PathFindingManager),
                nameof(PathFindingManager.GetOrCreate));
            MethodInfo reset = AccessTools.Method(typeof(PathFindingController), nameof(PathFindingController.Reset));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool error = false;

            while (true)
            {
                // Relevant CIL code:
                //     this.playSound("debuffHit");
                //         IL_05ad: ldarg.0
                //         IL_05ae: ldstr "debuffHit"
                //         IL_05b3: ldc.i4.0
                //         IL_05b4: call instance void StardewValley.GameLocation::playSound(string, valuetype StardewValley.Network.NetAudio / SoundContext)
                //
                // Code to include after:
                //     ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();

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

                codeInstructions.Insert(index + 3, new CodeInstruction(OpCodes.Ldsfld, pathFindingManager));
                codeInstructions.Insert(index + 4, new CodeInstruction(OpCodes.Call, getCurrentLocation));
                codeInstructions.Insert(index + 5, new CodeInstruction(OpCodes.Callvirt, getOrCreate));
                codeInstructions.Insert(index + 6, new CodeInstruction(OpCodes.Ldc_I4_1));
                codeInstructions.Insert(index + 7, new CodeInstruction(OpCodes.Callvirt, reset));

                // Relevant CIL code:
                //     if (!Game1.newDay && Game1.shouldTimePass() && Game1.player.hasMoved && !Game1.player.passedOut)
                //         ...
                //         IL_0c82: ldfld bool StardewValley.Farmer::passedOut
                //         IL_0c87: brtrue.s IL_0caf
                //
                // Code to include after:
                //     ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();

                index += 8;

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

                codeInstructions.Insert(index + 2, new CodeInstruction(OpCodes.Ldsfld, pathFindingManager));
                codeInstructions.Insert(index + 3, new CodeInstruction(OpCodes.Call, getCurrentLocation));
                codeInstructions.Insert(index + 4, new CodeInstruction(OpCodes.Callvirt, getOrCreate));
                codeInstructions.Insert(index + 5, new CodeInstruction(OpCodes.Ldc_I4_1));
                codeInstructions.Insert(index + 6, new CodeInstruction(OpCodes.Callvirt, reset));

                break;
            }

            if (error)
            {
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(GameLocation)}.{nameof(GameLocation.performTouchAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }

            foreach (CodeInstruction instruction in codeInstructions)
            {
                yield return instruction;
            }
        }

        private static bool BeforeDayTimeMoneyBoxReceiveLeftClick(DayTimeMoneyBox __instance, int x, int y)
        {
            if ((Game1.currentLocation is not null && ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickHoldActive)
                || Game1.activeClickableMenu is MuseumMenu || Game1.currentLocation is MermaidHouse)
            {
                return false;
            }

            if (Game1.player.visibleQuestCount > 0 && __instance.questButton.containsPoint(x, y) && Game1.player.CanMove
                && !Game1.dialogueUp && !Game1.eventUp && Game1.farmEvent is null)
            {
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
            }

            if (Game1.options.zoomButtons)
            {
                if (__instance.zoomInButton.containsPoint(x, y) && Game1.options.desiredBaseZoomLevel < 2f)
                {
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
                }
                else if (__instance.zoomOutButton.containsPoint(x, y) && Game1.options.desiredBaseZoomLevel > 0.75f)
                {
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
                }
            }

            return true;
        }

        private static void AfterGameLocationAnswerDialogueAction(string questionAndAnswer)
        {
            if (questionAndAnswer == "Eat_Yes" || questionAndAnswer == "Eat_No")
            {
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
            }
        }

        private static void AfterGameLocationCleanupBeforePlayerExit(GameLocation __instance)
        {
            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
        }

        private static void AfterGameLocationResetLocalState(GameLocation __instance)
        {
            ClickToMovePatcher.pathFindingManager[__instance].RefreshGraphBubbles();
        }

        private static void BeforeShopMenuReceiveLeftClick(ShopMenu __instance, int x, int y)
        {
            if (__instance.upperRightCloseButton.containsPoint(x, y))
            {
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ResetRotatingFurniture();
            }
        }

        private static bool BeforeGameLocationLowPriorityLeftClick(GameLocation __instance, ref bool __result)
        {
            if (__instance is DecoratableLocation && ClickToMovePatcher.pathFindingManager[__instance].ClickHoldActive
                && ClickToMovePatcher.pathFindingManager[__instance].Furniture is null)
            {
                __result = false;
                return false;
            }

            return true;
        }

        private static void AfterFishingGameConstructor()
        {
            ClickToMovePatcher.LeftClickNextUpdateFishingGame = false;
        }

        private static void AfterTargetGameReleaseLeftClick(GameLocation ___location)
        {
            ClickToMovePatcher.pathFindingManager[___location].Reset();
        }

        private static void AfterTargetGameConstructor()
        {
            ClickToMovePatcher.aiming = false;
            ClickToMovePatcher.usingSlingshot = false;
        }

        private static bool BeforeTargetGameReceiveKeyPress(
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

            if (!ClickToMovePatcher.aiming && Game1.player.movementDirections.Count < 2 && !Game1.player.UsingTool
                && ___timerToStart <= 0)
            {
                if (Game1.options.doesInputListContain(Game1.options.moveUpButton, k))
                {
                    Game1.player.setMoving(Farmer.up);
                }

                if (Game1.options.doesInputListContain(Game1.options.moveRightButton, k))
                {
                    Game1.player.setMoving(Farmer.right);
                }

                if (Game1.options.doesInputListContain(Game1.options.moveDownButton, k))
                {
                    Game1.player.setMoving(Farmer.down);
                }

                if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, k))
                {
                    Game1.player.setMoving(Farmer.left);
                }
            }

            if (Game1.options.doesInputListContain(Game1.options.runButton, k))
            {
                Game1.player.setRunning(true);
            }

            if (!Game1.player.usingTool && Game1.options.doesInputListContain(Game1.options.useToolButton, k))
            {
                ((Slingshot)Game1.player.CurrentTool).beginUsing(null, 0, 0, Game1.player);

                ClickToMovePatcher.aiming = true;
            }

            return false;
        }

        private static bool BeforeTargetGameReceiveLeftClick(int x, int y, ref int ___showResultsTimer)
        {
            if (___showResultsTimer < 0)
            {
                ClickToMovePatcher.usingSlingshot = false;

                if (Game1.currentMinigame is not null && Game1.currentMinigame is TargetGame && Vector2.Distance(
                        value2: new Vector2(
                            Game1.player.getStandingX() - Game1.viewport.X,
                            Game1.player.getStandingY() - Game1.viewport.Y),
                        value1: new Vector2(x, y)) < Game1.tileSize)
                {
                    ClickToMovePatcher.usingSlingshot = true;
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

        private static bool BeforeTargetGameReleaseLeftClick()
        {
            if (!ClickToMovePatcher.usingSlingshot)
            {
                return false;
            }

            ClickToMovePatcher.usingSlingshot = false;

            return true;
        }

        private static bool BeforeTargetGameTick()
        {
            if (ClickToMovePatcher.aiming)
            {
                if (!Game1.input.GetGamePadState().IsButtonDown(Buttons.X))
                {
                    ClickToMovePatcher.aiming = false;
                    ((Slingshot)Game1.player.CurrentTool).DoFunction(Game1.currentLocation, 0, 0, 1, Game1.player);
                    TargetGame.shotsFired++;
                }
                else
                {
                    Slingshot slingshot = (Slingshot)Game1.player.CurrentTool;

                    ClickToMovePatcher.helper.Reflection.GetMethod(slingshot, "updateAimPos").Invoke();
                }
            }

            return true;
        }

        private static void AfterCommunityCenterAfterViewportGetsToJunimoNotePosition(CommunityCenter __instance)
        {
            ClickToMovePatcher.pathFindingManager[__instance].Init();
        }

        private static void AfterMineShaftLoadLevel(MineShaft __instance)
        {
            ClickToMovePatcher.pathFindingManager[__instance] = new PathFindingController(__instance, ClickToMovePatcher.helper.Reflection);
        }

        private static void AfterFarmHouseLoadSpouseRoom(FarmHouse __instance)
        {
            ClickToMovePatcher.pathFindingManager[__instance].Init();
        }

        private static void AfterCommunityCenterResetLocalState(CommunityCenter __instance)
        {
            ClickToMovePatcher.pathFindingManager[__instance].Init();
        }

        private static void AfterFarmHouseSetMapForUpgradeLevel(FarmHouse __instance)
        {
            ClickToMovePatcher.pathFindingManager[__instance].Init();
        }

        private static bool BeforeMovieTheaterPerformTouchAction(MovieTheater __instance, string fullActionString)
        {
            if (fullActionString.Split(' ')[0] == "Theater_Exit")
            {
                ClickToMovePatcher.pathFindingManager[__instance].Reset();
            }

            return true;
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="FishingRod.doDoneFishing" />.
        ///     It resets the state of the <see cref="PathFindingController" /> object associated with the current game location.
        /// </summary>
        private static void AfterFishingRodDoDoneFishing()
        {
            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
        }

        /// <summary>A method called via Harmony to modify <see cref="Game1._update" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileGame1Update(IEnumerable<CodeInstruction> instructions)
        {
            // Add a call to the PathFindingController.UpdateMinigame method after the first call to GetMouseState.

            // Relevant CIL code:
            //    mouseState = Game1.input.GetMouseState();
            //        IL_06e1: ldsfld class StardewValley.InputState StardewValley.Game1::input
            //        IL_06e6: callvirt instance valuetype[Microsoft.Xna.Framework] Microsoft.Xna.Framework.Input.MouseState StardewValley.InputState::GetMouseState()
            //        IL_06eb: stloc.s 13
            //
            // Code to include after the variable mouseState is defined:
            //     ClickToMovePatcher.pathFindingManager.UpdateMinigame(mouseState);

            FieldInfo pathFindingManager = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.pathFindingManager));

            MethodInfo updateMinigame = AccessTools.Method(
                typeof(PathFindingManager),
                nameof(PathFindingManager.UpdateMinigame));

            List<CodeInstruction> codeInstructions = instructions.ToList();
            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Callvirt && codeInstructions[i].operand is MethodInfo { Name: "GetMouseState" }
                && i + 1 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Stloc_S)
                {
                    object mouseStateLocIndex = codeInstructions[i + 1].operand;

                    yield return codeInstructions[i];
                    i++;
                    yield return codeInstructions[i];
                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, mouseStateLocIndex);
                    yield return new CodeInstruction(OpCodes.Callvirt, updateMinigame);
                }
                else
                {
                    yield return codeInstructions[i];
                }
            }
        }

        /// <summary>A method called via Harmony to modify the setter for <see cref="Game1.currentMinigame" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileGame1SetCurrentMinigame(
            IEnumerable<CodeInstruction> instructions)
        {
            // Reset the PathFindingController object associated with the current game location
            // after the game checks that the current location is not null.

            // Relevant CIL code:
            //     if (Game1.currentLocation is not null)
            //         IL_0009: call class StardewValley.GameLocation StardewValley.Game1::get_currentLocation()
            //         IL_000e: brfalse.s IL_0024
            //
            // Code to include after:
            //     ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();

            FieldInfo pathFindingManager = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.pathFindingManager));

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(PathFindingManager),
                nameof(PathFindingManager.GetOrCreate));
            MethodInfo reset = AccessTools.Method(typeof(PathFindingController), nameof(PathFindingController.Reset));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Call && codeInstructions[i].operand is MethodInfo { Name: "get_currentLocation" }
                && i + 1 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Brfalse)
                {
                    yield return codeInstructions[i];
                    i++;
                    yield return codeInstructions[i];
                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Callvirt, reset);
                }
                else
                {
                    yield return codeInstructions[i];
                }
            }

            if (!found)
            {
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch the setter for {nameof(Game1)}.{nameof(Game1.currentMinigame)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        private static void BeforeGame1WarpFarmer()
        {
            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
        }

        private static void AfterGame1DrawObjectDialogue()
        {
            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();
        }

        private static void SwitchBackToLastTool(Farmer who)
        {
            if (who.IsMainPlayer && Game1.currentLocation is not null)
            {
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].SwitchBackToLastTool();
            }
        }

        /// <summary>A method called via Harmony to modify <see cref="FarmerSprite.animateOnce" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileFarmerSpriteAnimateOnce(IEnumerable<CodeInstruction> instructions)
        {
            // Switch back to last tool.

            // Relevant CIL code:
            //     if (base.currentAnimationIndex > this.currentAnimationFrames - 1)
            //         IL_0056: ldarg.0
            //         IL_0057: ldfld int32 StardewValley.AnimatedSprite::currentAnimationIndex
            //         IL_005c: ldarg.0
            //         IL_005d: ldfld int32 StardewValley.FarmerSprite::currentAnimationFrames
            //         IL_0062: ldc.i4.1
            //         IL_0063: sub
            //         IL_0064: ble IL_014c
            //.
            // Code to include after:
            //     FarmerSpritePatcher.SwitchBackToLastTool(this.owner);

            FieldInfo owner = AccessTools.Field(typeof(FarmerSprite), "owner");

            MethodInfo switchBackToLastTool =
                SymbolExtensions.GetMethodInfo(() => ClickToMovePatcher.SwitchBackToLastTool(null));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Ldfld && codeInstructions[i].operand is FieldInfo { Name: "currentAnimationFrames" }
                && i + 3 < codeInstructions.Count)
                {
                    for (int j = 0; j < 4; j++, i++)
                    {
                        yield return codeInstructions[i];
                    }

                    i--;

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
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(FarmerSprite)}.{nameof(FarmerSprite.animateOnce)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        private static bool BeforeFarmAnimalDraw(FarmAnimal __instance, SpriteBatch b)
        {
            if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].TargetFarmAnimal == __instance)
            {
                b.Draw(
                    Game1.mouseCursors,
                    Game1.GlobalToLocal(
                        Game1.viewport,
                        new Vector2(
                            (int)__instance.Position.X + (__instance.Sprite.getWidth() * 4 / 2) - 32,
                            (int)__instance.Position.Y + (__instance.Sprite.getHeight() * 4 / 2) - 24)),
                    new Microsoft.Xna.Framework.Rectangle(194, 388, 16, 16),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    4f,
                    SpriteEffects.None,
                    0.01f);
            }

            return true;
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Building.dayUpdate" />.
        ///     It reinitializes the <see cref="PathFindController" /> object associated to animal houses.
        /// </summary>
        /// <param name="__instance">The <see cref="Building" /> instance.</param>
        private static void AfterBuildingDayUpdate(Building __instance)
        {
            if (__instance.indoors.Value is AnimalHouse animalHouse)
            {
                ClickToMovePatcher.pathFindingManager[animalHouse].Init();
            }
        }

        /// <summary>
        ///     Gets if this farmer is being sick.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <returns>Returns true if the farmer is being sick; returns false otherwise.</returns>
        public static bool IsBeingSick(this Farmer farmer)
        {
            return ClickToMovePatcher.FarmersData.GetOrCreateValue(farmer).IsBeingSick;
        }

        /// <summary>
        ///     Gets if an horse should allow action checking.
        /// </summary>
        /// <param name="horse">The <see cref="Horse"/> instance.</param>
        /// <returns>
        ///     Returns true if action checking is enabled for this horse. Returns false otherwise.
        /// </returns>
        public static bool IsHorseCheckActionEnabled(this Horse horse)
        {
            return horse is not null && ClickToMovePatcher.HorsesData.GetOrCreateValue(horse).CheckActionEnabled;
        }

        /// <summary>
        ///     Sets whether an horse allows action checking.
        /// </summary>
        /// <param name="horse">The <see cref="Horse"/> instance.</param>
        /// <param name="value">Determines whether the horse can check action.</param>
        public static void SetHorseCheckActionEnabled(this Horse horse, bool value)
        {
            if (horse is not null)
            {
                ClickToMovePatcher.HorsesData.GetOrCreateValue(horse).CheckActionEnabled = value;
            }
        }

        /// <summary>
        ///     A method called via Harmony before the getter for <see cref="Farmer.CurrentItem"/>
        ///     that replaces it. This method checks if currentToolIndex is equal to -1 before
        ///     accessing items.
        /// </summary>
        /// <returns>
        ///     Returns false, terminating prefixes and skipping the execution of the original
        ///     method, effectively replacing the original method.
        /// </returns>
        internal static bool BeforeFarmerGetCurrentItem(
            Farmer __instance,
            NetInt ___currentToolIndex,
            bool ____itemStowed,
            ref Item __result)
        {
            if (__instance.TemporaryItem is not null)
            {
                __result = __instance.TemporaryItem;
            }
            else if (____itemStowed || ___currentToolIndex.Value == -1
                                    || ___currentToolIndex.Value >= __instance.items.Count)
            {
                __result = null;
            }
            else
            {
                __result = __instance.items[___currentToolIndex.Value];
            }

            return false;
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Farmer.completelyStopAnimatingOrDoingAction"/>.
        /// </summary>
        /// <param name="__instance">The <see cref="Farmer"/> instance.</param>
        private static void AfterFarmerCompletelyStopAnimatingOrDoingAction(Farmer __instance)
        {
            ClickToMovePatcher.FarmersData.GetOrCreateValue(__instance).IsBeingSick = false;
        }

        /// <summary>
        ///     A method called via Harmony after the constructor for <see cref="Farmer"/>.
        /// </summary>
        /// <param name="___currentToolIndex">
        ///     The private field currentToolIndex of the <see cref="Farmer"/> instance.
        /// </param>
        private static void AfterFarmerConstructor(NetInt ___currentToolIndex)
        {
            ___currentToolIndex.Set(-1);
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Farmer.forceCanMove"/>.
        /// </summary>
        /// <param name="__instance">The farmer instance.</param>
        private static void AfterFarmerForceCanMove(Farmer __instance)
        {
            ClickToMovePatcher.FarmersData.GetOrCreateValue(__instance).IsBeingSick = false;
        }

        /// <summary>
        /// A method called via Harmony after <see cref="Game1.didPlayerJustLeftClick" />.
        /// </summary>
        /// <param name="__result">A reference to the value returned by the original method.</param>
        private static void AfterGame1DidPlayerJustLeftClick(ref bool __result)
        {
            if (!__result)
            {
                if (Game1.currentLocation is not null)
                {
                    __result = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates
                        .UseToolButtonPressed;
                }
            }
        }

        /// <summary>
        /// A method called via Harmony after <see cref="Game1.didPlayerJustRightClick" />.
        /// </summary>
        /// <param name="__result">A reference to the value returned by the original method.</param>
        private static void AfterGame1DidPlayerJustRightClick(ref bool __result)
        {
            if (!__result)
            {
                if (Game1.currentLocation is not null)
                {
                    __result = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.ActionButtonPressed;
                }
            }
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Game1.exitActiveMenu"/>.
        /// </summary>
        private static void AfterGame1ExitActiveMenu()
        {
            PathFindingController pathFindingController = ClickToMovePatcher.pathFindingManager[Game1.currentLocation];

            if (pathFindingController is not null)
            {
                pathFindingController.OnCloseActiveMenu();

                if (Game1.input is not null)
                {
                    GamePadState gamePadState = Game1.input.GetGamePadState();
                    if (gamePadState.IsConnected
                        && !gamePadState.IsButtonDown(Buttons.DPadUp)
                        && !gamePadState.IsButtonDown(Buttons.DPadDown)
                        && !gamePadState.IsButtonDown(Buttons.DPadLeft)
                        && !gamePadState.IsButtonDown(Buttons.DPadRight)
                        && !gamePadState.IsButtonDown(Buttons.LeftThumbstickUp)
                        && !gamePadState.IsButtonDown(Buttons.LeftThumbstickDown)
                        && !gamePadState.IsButtonDown(Buttons.LeftThumbstickLeft)
                        && !gamePadState.IsButtonDown(Buttons.LeftThumbstickRight))
                    {
                        pathFindingController.KeyStates.Reset();
                    }
                }
            }
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Toolbar.update(GameTime)"/>.
        /// </summary>
        /// <param name="time">The current game tick.</param>
        private static void AfterToolbarUpdate(GameTime time)
        {
            if (!Game1.player.usingTool && ClickToMovePatcher.nextToolIndex != int.MinValue)
            {
                Game1.player.CurrentToolIndex = ClickToMovePatcher.nextToolIndex;
                ClickToMovePatcher.nextToolIndex = int.MinValue;

                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClearAutoSelectTool();

                if (Game1.player.CurrentTool is not null && Game1.player.CurrentTool is MeleeWeapon weapon)
                {
                    PathFindingController.MostRecentlyChosenMeleeWeapon = weapon;
                }
            }
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="BusStop.answerDialogueafterViewportGetsToJunimoNotePosition"/>.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="answer"></param>
        /// <returns></returns>
        private static bool BeforeBusStopAnswerDialogue(BusStop __instance, Response answer)
        {
            if (__instance.lastQuestionKey is not null && __instance.afterQuestion is null)
            {
                string[] words = __instance.lastQuestionKey.Split(' ');

                if (words[0] == "Minecart")
                {
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = false;
                }

                string questionAndAnswer = words[0] + "_" + answer.responseKey;

                if (questionAndAnswer == "Bus_Yes")
                {
                    NPC pam = Game1.getCharacterFromName("Pam");
                    if (!(Game1.player.Money >= (Game1.shippingTax ? 50 : 500) && __instance.characters.Contains(pam)
                                                                               && pam.getTileLocation().Equals(
                                                                                   new Vector2(11f, 10f))))
                    {
                        ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = false;
                    }
                }
                else if (questionAndAnswer == "Bus_No")
                {
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = false;
                }
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="tileLocation"></param>
        /// <returns></returns>
        private static bool BeforeBusStopCheckAction(BusStop __instance, Location tileLocation)
        {
            if (__instance.map.GetLayer("Buildings").Tiles[tileLocation] is not null)
            {
                switch (__instance.map.GetLayer("Buildings").Tiles[tileLocation].TileIndex)
                {
                    case 958:
                    case 1080:
                    case 1081:
                        if (Game1.MasterPlayer.mailReceived.Contains("ccBoilerRoom")
                            && (Game1.player.mount is null || !Game1.player.isRidingHorse()))
                        {
                            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = true;
                        }

                        break;
                    case 1057:
                        if (Game1.MasterPlayer.mailReceived.Contains("ccVault"))
                        {
                            if (Game1.player.mount is null || !Game1.player.isRidingHorse())
                            {
                                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = true;
                            }
                        }
                        else
                        {
                            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = false;
                        }

                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private static bool BeforeBusStopPlayerReachedBusDoor()
        {
            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = false;

            return true;
        }

        /// <summary>
        ///     A method called via Harmony before the getter for <see cref="Farmer.ActiveObject"/>
        ///     that replaces it. This method checks if currentToolIndex is equal to -1 before
        ///     accessing items.
        /// </summary>
        /// <returns>
        ///     Returns false, terminating prefixes and skipping the execution of the original
        ///     method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeFarmerGetActiveObject(
            Farmer __instance,
            NetInt ___currentToolIndex,
            bool ____itemStowed,
            ref SObject __result)
        {
            if (__instance.TemporaryItem is not null)
            {
                __result = __instance.TemporaryItem is SObject @object ? @object : null;
            }
            else if (___currentToolIndex.Value == -1 || ____itemStowed)
            {
                __result = null;
            }
            else
            {
                __result = ___currentToolIndex.Value < __instance.items.Count
                           && __instance.items[___currentToolIndex] is SObject object2
                               ? object2
                               : null;
            }

            return false;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Game1.UpdateControlInput"/>. It
        ///     replicates the game's control input processing with some changes so we can implement
        ///     the click to move functionality.
        /// </summary>
        /// <param name="__instance">The <see cref="Game1"/> instance.</param>
        /// <param name="time">The time passed since the last call to <see cref="Game1.Update"/>.</param>
        /// <returns>
        ///     Returns false, which terminates prefixes and skips the execution of the original
        ///     method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeGame1UpdateControlInput(
    Game1 __instance,
    GameTime time,
    int ____activatedTick,
    ref IInputSimulator ___inputSimulator,
    ref bool ____didInitiateItemStow,
    Multiplayer ___multiplayer)
        {
            KeyboardState currentKbState = Game1.GetKeyboardState();
            MouseState currentMouseState = Game1.input.GetMouseState();
            GamePadState currentPadState = Game1.input.GetGamePadState();

            if (Game1.ticks < ____activatedTick + 2
                && Game1.oldKBState.IsKeyDown(Keys.Tab) != currentKbState.IsKeyDown(Keys.Tab))
            {
                List<Keys> keys = Game1.oldKBState.GetPressedKeys().ToList();

                if (currentKbState.IsKeyDown(Keys.Tab))
                {
                    keys.Add(Keys.Tab);
                }
                else
                {
                    keys.Remove(Keys.Tab);
                }

                Game1.oldKBState = new KeyboardState(keys.ToArray());
            }

            if (Game1.options.gamepadControls)
            {
                bool noMouse = false;
                if (Math.Abs(currentPadState.ThumbSticks.Right.X) > 0
                    || Math.Abs(currentPadState.ThumbSticks.Right.Y) > 0)
                {
                    float thumbstickToMouseModifier = ClickToMovePatcher.thumbstickToMouseModifier.GetValue();
                    Game1.setMousePositionRaw(
                        (int)(currentMouseState.X + (currentPadState.ThumbSticks.Right.X * thumbstickToMouseModifier)),
                        (int)(currentMouseState.Y - (currentPadState.ThumbSticks.Right.Y * thumbstickToMouseModifier)));
                    noMouse = true;
                }

                if (Game1.IsChatting)
                {
                    noMouse = true;
                }

                if (((Game1.getMouseX() != Game1.getOldMouseX() || Game1.getMouseY() != Game1.getOldMouseY())
                     && Game1.getMouseX() != 0 && Game1.getMouseY() != 0) || noMouse)
                {
                    if (noMouse)
                    {
                        if (Game1.timerUntilMouseFade <= 0)
                        {
                            Game1.lastMousePositionBeforeFade = new Point(
                                __instance.localMultiplayerWindow.Width / 2,
                                __instance.localMultiplayerWindow.Height / 2);
                        }
                    }
                    else
                    {
                        Game1.lastCursorMotionWasMouse = true;
                    }

                    if (Game1.timerUntilMouseFade <= 0 && !Game1.lastCursorMotionWasMouse)
                    {
                        Game1.setMousePositionRaw(
                            Game1.lastMousePositionBeforeFade.X,
                            Game1.lastMousePositionBeforeFade.Y);
                    }

                    Game1.timerUntilMouseFade = 4000;
                }
            }
            else if (Game1.getMouseX() != Game1.getOldMouseX() || Game1.getMouseY() != Game1.getOldMouseY())
            {
                Game1.lastCursorMotionWasMouse = true;
            }

            ClickToMovePatcher.controlPadActionButtonPressed = false;
            bool actionButtonPressed = false;
            bool switchToolButtonPressed = false;
            bool useToolButtonPressed = false;
            bool useToolButtonReleased = false;
            bool useToolButtonHeld = false;
            bool cancelButtonPressed = false;
            bool moveUpPressed = false;
            bool moveRightPressed = false;
            bool moveLeftPressed = false;
            bool moveDownPressed = false;
            bool moveUpReleased = false;
            bool moveRightReleased = false;
            bool moveDownReleased = false;
            bool moveLeftReleased = false;
            bool moveUpHeld = false;
            bool moveRightHeld = false;
            bool moveDownHeld = false;
            bool moveLeftHeld = false;

            if ((Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.actionButton)
                 && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.actionButton))
                || (currentMouseState.RightButton == ButtonState.Pressed
                    && Game1.oldMouseState.RightButton == ButtonState.Released))
            {
                actionButtonPressed = true;
                Game1.rightClickPolling = 250;
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.actionButton)
                || currentMouseState.RightButton == ButtonState.Pressed)
            {
                Game1.rightClickPolling -= time.ElapsedGameTime.Milliseconds;
                if (Game1.rightClickPolling <= 0)
                {
                    Game1.rightClickPolling = 100;
                    actionButtonPressed = true;
                }
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.useToolButton))
            {
                useToolButtonHeld = true;

                if (Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.useToolButton))
                {
                    useToolButtonPressed = true;
                }
            }
            else if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton))
            {
                useToolButtonReleased = true;
            }

            if (currentMouseState.LeftButton == ButtonState.Pressed)
            {
                useToolButtonHeld = true;
            }

            if ((Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.toolSwapButton)
                 && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.toolSwapButton))
                || currentMouseState.ScrollWheelValue != Game1.oldMouseState.ScrollWheelValue)
            {
                switchToolButtonPressed = true;
            }

            if ((Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.cancelButton)
                 && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.cancelButton))
                || (currentMouseState.RightButton == ButtonState.Pressed
                    && Game1.oldMouseState.RightButton == ButtonState.Released))
            {
                cancelButtonPressed = true;
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.moveUpButton))
            {
                moveUpHeld = true;

                if (Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.moveUpButton))
                {
                    moveUpPressed = true;
                }
            }
            else if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveUpButton))
            {
                moveUpReleased = true;
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.moveRightButton))
            {
                moveRightHeld = true;

                if (Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.moveRightButton))
                {
                    moveRightPressed = true;
                }
            }
            else if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveRightButton))
            {
                moveRightReleased = true;
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.moveDownButton))
            {
                moveDownHeld = true;

                if (Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.moveDownButton))
                {
                    moveDownPressed = true;
                }
            }
            else if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveDownButton))
            {
                moveDownReleased = true;
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.moveLeftButton))
            {
                moveLeftHeld = true;

                if (Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.moveLeftButton))
                {
                    moveLeftPressed = true;
                }
            }
            else if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveLeftButton))
            {
                moveLeftReleased = true;
            }

            if (Game1.options.gamepadControls)
            {
                if (currentKbState.GetPressedKeys().Length != 0 || currentMouseState.LeftButton == ButtonState.Pressed
                                                                || currentMouseState.RightButton == ButtonState.Pressed)
                {
                    Game1.timerUntilMouseFade = 4000;
                }

                if (currentPadState.IsButtonDown(Buttons.A) && !Game1.oldPadState.IsButtonDown(Buttons.A))
                {
                    actionButtonPressed = true;
                    Game1.lastCursorMotionWasMouse = false;
                    Game1.rightClickPolling = 250;
                    ClickToMovePatcher.controlPadActionButtonPressed = true;
                }

                if (currentPadState.IsButtonDown(Buttons.A))
                {
                    Game1.rightClickPolling -= time.ElapsedGameTime.Milliseconds;
                    if (Game1.rightClickPolling <= 0)
                    {
                        Game1.rightClickPolling = 100;
                        actionButtonPressed = true;
                        ClickToMovePatcher.controlPadActionButtonPressed = true;
                    }
                }

                if (currentPadState.IsButtonDown(Buttons.X))
                {
                    useToolButtonHeld = true;

                    if (!Game1.oldPadState.IsButtonDown(Buttons.X))
                    {
                        useToolButtonPressed = true;
                        Game1.lastCursorMotionWasMouse = false;
                    }
                }
                else if (Game1.oldPadState.IsButtonDown(Buttons.X))
                {
                    useToolButtonReleased = true;
                }

                if (currentPadState.IsButtonDown(Buttons.RightTrigger)
                    && !Game1.oldPadState.IsButtonDown(Buttons.RightTrigger))
                {
                    switchToolButtonPressed = true;
                    Game1.triggerPolling = 300;
                }
                else if (currentPadState.IsButtonDown(Buttons.LeftTrigger)
                         && !Game1.oldPadState.IsButtonDown(Buttons.LeftTrigger))
                {
                    switchToolButtonPressed = true;
                    Game1.triggerPolling = 300;
                }

                if (currentPadState.IsButtonDown(Buttons.RightTrigger)
                    || currentPadState.IsButtonDown(Buttons.LeftTrigger))
                {
                    Game1.triggerPolling -= time.ElapsedGameTime.Milliseconds;
                    if (Game1.triggerPolling <= 0)
                    {
                        Game1.triggerPolling = 100;
                        switchToolButtonPressed = true;
                    }
                }

                if (currentPadState.IsButtonDown(Buttons.RightShoulder)
                    && !Game1.oldPadState.IsButtonDown(Buttons.RightShoulder))
                {
                    Game1.player.shiftToolbar(true);
                }

                if (currentPadState.IsButtonDown(Buttons.LeftShoulder)
                    && !Game1.oldPadState.IsButtonDown(Buttons.LeftShoulder))
                {
                    Game1.player.shiftToolbar(false);
                }

                if (currentPadState.IsButtonDown(Buttons.DPadUp))
                {
                    moveUpHeld = true;

                    if (!Game1.oldPadState.IsButtonDown(Buttons.DPadUp))
                    {
                        moveUpPressed = true;
                    }
                }
                else if (Game1.oldPadState.IsButtonDown(Buttons.DPadUp))
                {
                    moveUpReleased = true;
                }

                if (currentPadState.IsButtonDown(Buttons.DPadRight))
                {
                    moveRightHeld = true;

                    if (!Game1.oldPadState.IsButtonDown(Buttons.DPadRight))
                    {
                        moveRightPressed = true;
                    }
                }
                else if (Game1.oldPadState.IsButtonDown(Buttons.DPadRight))
                {
                    moveRightReleased = true;
                }

                if (currentPadState.IsButtonDown(Buttons.DPadDown))
                {
                    moveDownHeld = true;

                    if (!Game1.oldPadState.IsButtonDown(Buttons.DPadDown))
                    {
                        moveDownPressed = true;
                    }
                }
                else if (Game1.oldPadState.IsButtonDown(Buttons.DPadDown))
                {
                    moveDownReleased = true;
                }

                if (currentPadState.IsButtonDown(Buttons.DPadLeft))
                {
                    moveLeftHeld = true;

                    if (!Game1.oldPadState.IsButtonDown(Buttons.DPadLeft))
                    {
                        moveLeftPressed = true;
                    }
                }
                else if (Game1.oldPadState.IsButtonDown(Buttons.DPadLeft))
                {
                    moveLeftReleased = true;
                }

                if (currentPadState.ThumbSticks.Left.X < -0.2f)
                {
                    moveLeftPressed = true;
                    moveLeftHeld = true;
                }
                else if (currentPadState.ThumbSticks.Left.X > 0.2f)
                {
                    moveRightPressed = true;
                    moveRightHeld = true;
                }

                if (currentPadState.ThumbSticks.Left.Y < -0.2f)
                {
                    moveDownPressed = true;
                    moveDownHeld = true;
                }
                else if (currentPadState.ThumbSticks.Left.Y > 0.2f)
                {
                    moveUpPressed = true;
                    moveUpHeld = true;
                }

                if (Game1.oldPadState.ThumbSticks.Left.X < -0.2f && !moveLeftHeld)
                {
                    moveLeftReleased = true;
                }

                if (Game1.oldPadState.ThumbSticks.Left.X > 0.2f && !moveRightHeld)
                {
                    moveRightReleased = true;
                }

                if (Game1.oldPadState.ThumbSticks.Left.Y < -0.2f && !moveDownHeld)
                {
                    moveDownReleased = true;
                }

                if (Game1.oldPadState.ThumbSticks.Left.Y > 0.2f && !moveUpHeld)
                {
                    moveUpReleased = true;
                }

                if (__instance.controllerSlingshotSafeTime > 0)
                {
                    if (!currentPadState.IsButtonDown(Buttons.DPadUp) && !currentPadState.IsButtonDown(Buttons.DPadDown)
                                                                      && !currentPadState.IsButtonDown(Buttons.DPadLeft)
                                                                      && !currentPadState.IsButtonDown(Buttons.DPadRight)
                                                                      && Math.Abs(currentPadState.ThumbSticks.Left.X) < 0.04f
                                                                      && Math.Abs(currentPadState.ThumbSticks.Left.Y) < 0.04f)
                    {
                        __instance.controllerSlingshotSafeTime = 0;
                    }

                    if (__instance.controllerSlingshotSafeTime <= 0)
                    {
                        __instance.controllerSlingshotSafeTime = 0;
                    }
                    else
                    {
                        __instance.controllerSlingshotSafeTime -= (float)time.ElapsedGameTime.TotalSeconds;

                        moveUpPressed = false;
                        moveDownPressed = false;
                        moveLeftPressed = false;
                        moveRightPressed = false;
                        moveUpHeld = false;
                        moveDownHeld = false;
                        moveLeftHeld = false;
                        moveRightHeld = false;
                    }
                }
            }
            else
            {
                __instance.controllerSlingshotSafeTime = 0;
            }

            Game1.ResetFreeCursorDrag();

            SButtonState clickState = SButtonState.None;

            bool mouseLeftButtonDown = ClickToMovePatcher.helper.Input.IsDown(SButton.MouseLeft) || ClickToMovePatcher.helper.Input.IsSuppressed(SButton.MouseLeft);

            if (mouseLeftButtonDown)
            {
                clickState = lastMouseLeftButtonDown ? SButtonState.Held : SButtonState.Pressed;
            }
            else if (ClickToMovePatcher.lastMouseLeftButtonDown)
            {
                clickState = SButtonState.Released;
            }

            ClickToMovePatcher.lastMouseLeftButtonDown = mouseLeftButtonDown;

            ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Update();

            if (moveUpPressed || moveDownPressed || moveLeftPressed || moveRightPressed || moveUpReleased
                || moveDownReleased || moveLeftReleased || moveRightReleased || moveUpHeld || moveDownHeld
                || moveLeftHeld || moveRightHeld)
            {
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();

                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpPressed = moveUpPressed;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownPressed = moveDownPressed;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftPressed = moveLeftPressed;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightPressed = moveRightPressed;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpReleased = moveUpReleased;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownReleased = moveDownReleased;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftReleased = moveLeftReleased;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightReleased = moveRightReleased;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpHeld = moveUpHeld;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownHeld = moveDownHeld;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftHeld = moveLeftHeld;
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightHeld = moveRightHeld;
            }
            else if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Active)
            {
                moveUpPressed = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpPressed;
                moveDownPressed = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownPressed;
                moveLeftPressed = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftPressed;
                moveRightPressed = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightPressed;
                moveUpReleased = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpReleased;
                moveDownReleased = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownReleased;
                moveLeftReleased = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftReleased;
                moveRightReleased = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightReleased;
                moveUpHeld = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpHeld;
                moveDownHeld = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownHeld;
                moveLeftHeld = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftHeld;
                moveRightHeld = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightHeld;
            }

            if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.ActionButtonPressed)
            {
                actionButtonPressed = true;
            }

            if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.UseToolButtonPressed)
            {
                useToolButtonPressed = true;
            }

            if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.UseToolButtonHeld)
            {
                useToolButtonHeld = true;
            }

            if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.UseToolButtonReleased)
            {
                useToolButtonReleased = true;
            }

            if (useToolButtonHeld && !(Game1.player.ActiveObject is Furniture))
            {
                Game1.mouseClickPolling += time.ElapsedGameTime.Milliseconds;
            }
            else
            {
                Game1.mouseClickPolling = 0;
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.toolbarSwap) && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.toolbarSwap))
            {
                Game1.player.shiftToolbar(!currentKbState.IsKeyDown(Keys.LeftControl));
            }

            Game1.PushUIMode();

            foreach (IClickableMenu menu in Game1.onScreenMenus)
            {
                if ((Game1.displayHUD || menu == Game1.chatBox) && Game1.wasMouseVisibleThisFrame
                                                                && menu.isWithinBounds(
                                                                    Game1.getMouseX(),
                                                                    Game1.getMouseY()))
                {
                    menu.performHoverAction(Game1.getMouseX(), Game1.getMouseY());
                }
            }

            Game1.PopUIMode();

            if (Game1.chatBox is not null && Game1.chatBox.chatBox.Selected
                                      && Game1.oldMouseState.ScrollWheelValue != currentMouseState.ScrollWheelValue)
            {
                Game1.chatBox.receiveScrollWheelAction(
                    currentMouseState.ScrollWheelValue - Game1.oldMouseState.ScrollWheelValue);
            }

            if (Game1.panMode)
            {
                IReflectedMethod updatePanModeControls =
                    ClickToMovePatcher.helper.Reflection.GetMethod(__instance, "updatePanModeControls");
                updatePanModeControls.Invoke(currentMouseState, currentKbState);

                return false;
            }

            if (___inputSimulator is not null)
            {
                if (currentKbState.IsKeyDown(Keys.Escape))
                {
                    ___inputSimulator = null;
                }
                else
                {
                    bool addItemToInventoryButtonPressed = false;
                    ___inputSimulator.SimulateInput(
                        ref actionButtonPressed,
                        ref switchToolButtonPressed,
                        ref useToolButtonPressed,
                        ref useToolButtonReleased,
                        ref addItemToInventoryButtonPressed,
                        ref cancelButtonPressed,
                        ref moveUpPressed,
                        ref moveRightPressed,
                        ref moveLeftPressed,
                        ref moveDownPressed,
                        ref moveUpReleased,
                        ref moveRightReleased,
                        ref moveLeftReleased,
                        ref moveDownReleased,
                        ref moveUpHeld,
                        ref moveRightHeld,
                        ref moveLeftHeld,
                        ref moveDownHeld);
                }
            }

            if (useToolButtonReleased && Game1.player.CurrentTool is not null && Game1.CurrentEvent is null
                && Game1.pauseTime <= 0 && Game1.player.CurrentTool.onRelease(
                    Game1.currentLocation,
                    Game1.getMouseX(),
                    Game1.getMouseY(),
                    Game1.player))
            {
                Game1.oldKBState = currentKbState;
                Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                Game1.oldPadState = currentPadState;

                Game1.player.usingSlingshot = false;
                Game1.player.canReleaseTool = true;
                Game1.player.UsingTool = false;
                Game1.player.CanMove = true;

                return false;
            }

            if (currentMouseState.LeftButton == ButtonState.Pressed
                && Game1.oldMouseState.LeftButton == ButtonState.Released
                && Game1.CurrentEvent is not null)
            {
                Game1.CurrentEvent.receiveMouseClick(Game1.getMouseX(), Game1.getMouseY());
            }

            if (((currentMouseState.LeftButton == ButtonState.Pressed
                  && Game1.oldMouseState.LeftButton == ButtonState.Released)
                 || (useToolButtonPressed && !Game1.isAnyGamePadButtonBeingPressed())
                 || (actionButtonPressed && Game1.isAnyGamePadButtonBeingPressed())) && Game1.pauseTime <= 0f
                && Game1.wasMouseVisibleThisFrame)
            {
                Game1.PushUIMode();

                foreach (IClickableMenu menu in Game1.onScreenMenus)
                {
                    if (Game1.displayHUD || menu == Game1.chatBox)
                    {
                        if ((!Game1.IsChatting || menu == Game1.chatBox)
                            && (!(menu is LevelUpMenu) || (menu as LevelUpMenu).informationUp)
                            && menu.isWithinBounds(Game1.getMouseX(), Game1.getMouseY()))
                        {
                            menu.receiveLeftClick(Game1.getMouseX(), Game1.getMouseY());

                            Game1.PopUIMode();

                            Game1.oldKBState = currentKbState;
                            Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                            Game1.oldPadState = currentPadState;

                            return false;
                        }

                        if (menu == Game1.chatBox && Game1.options.gamepadControls && Game1.IsChatting)
                        {
                            Game1.PopUIMode();

                            Game1.oldKBState = currentKbState;
                            Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                            Game1.oldPadState = currentPadState;

                            return false;
                        }

                        menu.clickAway();
                    }
                }

                Game1.PopUIMode();
            }

            switch (clickState)
            {
                case SButtonState.Pressed:
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].OnClick(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                    break;
                case SButtonState.Held:
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].OnClickHeld(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                    break;
                case SButtonState.Released:
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].OnClickRelease(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                    break;
            }

            if (Game1.IsChatting || Game1.player.freezePause > 0)
            {
                if (Game1.IsChatting)
                {
                    ButtonCollection.ButtonEnumerator buttonEnumerator =
                        Utility.getPressedButtons(currentPadState, Game1.oldPadState).GetEnumerator();
                    while (buttonEnumerator.MoveNext())
                    {
                        Game1.chatBox.receiveGamePadButton(buttonEnumerator.Current);
                    }
                }

                Game1.oldKBState = currentKbState;
                Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                Game1.oldPadState = currentPadState;

                return false;
            }

            if (Game1.paused || Game1.HostPaused)
            {
                if (!Game1.HostPaused || !Game1.IsMasterGame
                                      || (!Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.menuButton)
                                          && !currentPadState.IsButtonDown(Buttons.B)
                                          && !currentPadState.IsButtonDown(Buttons.Back)))
                {
                    Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();

                    return false;
                }

                Game1.netWorldState.Value.IsPaused = false;

                if (Game1.chatBox is not null)
                {
                    Game1.chatBox.globalInfoMessage("Resumed");
                }
            }

            if (Game1.eventUp)
            {
                if (Game1.currentLocation.currentEvent is null && Game1.locationRequest is null)
                {
                    Game1.eventUp = false;
                }
                else if (actionButtonPressed || useToolButtonPressed)
                {
                    Game1.CurrentEvent?.receiveMouseClick(Game1.getMouseX(), Game1.getMouseY());
                }
            }

            bool eventUp = Game1.eventUp || Game1.farmEvent is not null;

            if (actionButtonPressed || (Game1.dialogueUp && useToolButtonPressed))
            {
                Game1.PushUIMode();

                foreach (IClickableMenu menu in Game1.onScreenMenus)
                {
                    if (Game1.wasMouseVisibleThisFrame && (Game1.displayHUD || menu == Game1.chatBox)
                                                       && menu.isWithinBounds(Game1.getMouseX(), Game1.getMouseY())
                                                       && (!(menu is LevelUpMenu)
                                                           || (menu as LevelUpMenu).informationUp))
                    {
                        menu.receiveRightClick(Game1.getMouseX(), Game1.getMouseY());

                        Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();

                        if (!Game1.isAnyGamePadButtonBeingPressed())
                        {
                            Game1.PopUIMode();

                            Game1.oldKBState = currentKbState;
                            Game1.oldPadState = currentPadState;

                            return false;
                        }
                    }
                }

                if (!Game1.pressActionButton(currentKbState, currentMouseState, currentPadState))
                {
                    Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                    Game1.oldKBState = currentKbState;
                    Game1.oldPadState = currentPadState;

                    return false;
                }
            }
            else
            {
                if (useToolButtonPressed && (!Game1.player.UsingTool || Game1.player.CurrentTool is MeleeWeapon)
                                         && !Game1.player.isEating && !Game1.pickingTool && !Game1.dialogueUp
                                         && !Game1.menuUp && Game1.farmEvent is null
                                         && (Game1.player.CanMove || Game1.player.CurrentTool is FishingRod
                                                                  || Game1.player.CurrentTool is MeleeWeapon))
                {
                    if (Game1.player.CurrentTool is not null)
                    {
                        Game1.player.FireTool();
                    }

                    Game1.pressUseToolButton();

                    if (Game1.mouseClickPolling < 100)
                    {
                        Game1.oldKBState = currentKbState;
                    }

                    Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();

                    Game1.oldPadState = currentPadState;

                    return false;
                }

                if (useToolButtonReleased && ____didInitiateItemStow)
                {
                    ____didInitiateItemStow = false;
                }

                if (useToolButtonReleased && Game1.player.canReleaseTool && Game1.player.UsingTool
                    && Game1.player.CurrentTool is not null)
                {
                    Game1.player.EndUsingTool();
                }
                else if (switchToolButtonPressed && !Game1.player.UsingTool && !Game1.dialogueUp
                         && (Game1.pickingTool || Game1.player.CanMove) && !Game1.player.areAllItemsNull() && !eventUp)
                {
                    Game1.pressSwitchToolButton();
                }
            }

            if (cancelButtonPressed)
            {
                if (Game1.numberOfSelectedItems != -1)
                {
                    Game1.numberOfSelectedItems = -1;
                    Game1.dialogueUp = false;
                    Game1.player.CanMove = true;
                }
                else if (Game1.nameSelectUp && NameSelect.cancel())
                {
                    Game1.nameSelectUp = false;
                    Game1.playSound("bigDeSelect");
                }
            }

            if (Game1.player.CurrentTool is not null && useToolButtonHeld && Game1.player.canReleaseTool && !eventUp
                && !Game1.dialogueUp && !Game1.menuUp && Game1.player.Stamina >= 1
                && Game1.player.CurrentTool is not FishingRod)
            {
                int reachingToolEnchantment = Game1.player.CurrentTool.hasEnchantmentOfType<ReachingToolEnchantment>() ? 1 : 0;

                if (Game1.player.toolHold <= 0 && Game1.player.CurrentTool.upgradeLevel.Value + reachingToolEnchantment
                    > Game1.player.toolPower)
                {
                    Game1.player.toolHold = (int)(Game1.toolHoldPerPowerupLevel * Game1.player.CurrentTool.AnimationSpeedModifier);
                }
                else if (Game1.player.CurrentTool.upgradeLevel.Value + reachingToolEnchantment > Game1.player.toolPower)
                {
                    Game1.player.toolHold -= time.ElapsedGameTime.Milliseconds;
                    if (Game1.player.toolHold <= 0)
                    {
                        Game1.player.toolPowerIncrease();
                    }
                }
            }

            if (Game1.upPolling >= Game1.keyPollingThreshold)
            {
                Game1.upPolling -= 100f;
            }
            else if (Game1.downPolling >= Game1.keyPollingThreshold)
            {
                Game1.downPolling -= 100f;
            }
            else if (Game1.rightPolling >= Game1.keyPollingThreshold)
            {
                Game1.rightPolling -= 100f;
            }
            else if (Game1.leftPolling >= Game1.keyPollingThreshold)
            {
                Game1.leftPolling -= 100f;
            }
            else if (!Game1.nameSelectUp && Game1.pauseTime <= 0 && Game1.locationRequest is null
                     && !Game1.player.UsingTool
                     && (!eventUp || (Game1.CurrentEvent is not null && Game1.CurrentEvent.playerControlSequence)))
            {
                if (Game1.player.movementDirections.Count < 2)
                {
                    int initialCount = Game1.player.movementDirections.Count;

                    if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpHeld)
                    {
                        Game1.player.setMoving(Farmer.up);
                    }

                    if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightHeld)
                    {
                        Game1.player.setMoving(Farmer.right);
                    }

                    if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownHeld)
                    {
                        Game1.player.setMoving(Farmer.down);
                    }

                    if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftHeld)
                    {
                        Game1.player.setMoving(Farmer.left);
                    }
                }

                if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Up.Value) && !ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.up);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Right.Value) && !ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.right);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Down.Value) && !ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.down);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Left.Value) && !ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.left);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if ((!ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpHeld
                     && !ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightHeld
                     && !ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownHeld
                     && !ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftHeld
                     && !Game1.player.UsingTool) || Game1.activeClickableMenu is not null)
                {
                    Game1.player.Halt();
                }
            }
            else if (Game1.isQuestion)
            {
                if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpPressed)
                {
                    Game1.currentQuestionChoice = Math.Max(Game1.currentQuestionChoice - 1, 0);

                    Game1.playSound("toolSwap");
                }
                else if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownPressed)
                {
                    Game1.currentQuestionChoice = Math.Min(
                        Game1.currentQuestionChoice + 1,
                        Game1.questionChoices.Count - 1);

                    Game1.playSound("toolSwap");
                }
            }
            else if (Game1.numberOfSelectedItems != -1 && !Game1.dialogueTyping)
            {
                int val = Game1.selectedItemsType switch
                {
                    "Animal Food" => 999 - Game1.player.Feed,
                    "calicoJackBet" => Math.Min(Game1.player.clubCoins, 999),
                    "flutePitch" => 26,
                    "drumTone" => 6,
                    "jukebox" => Game1.player.songsHeard.Count - 1,
                    "Fuel" => 100 - ((Lantern)Game1.player.getToolFromName("Lantern")).fuelLeft,
                    _ => 99
                };

                if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveRightPressed)
                {
                    Game1.numberOfSelectedItems = Math.Min(Game1.numberOfSelectedItems + 1, val);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveLeftPressed)
                {
                    Game1.numberOfSelectedItems = Math.Max(Game1.numberOfSelectedItems - 1, 0);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveUpPressed)
                {
                    Game1.numberOfSelectedItems = Math.Min(Game1.numberOfSelectedItems + 10, val);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].KeyStates.MoveDownPressed)
                {
                    Game1.numberOfSelectedItems = Math.Max(Game1.numberOfSelectedItems - 10, 0);
                    Game1.playItemNumberSelectSound();
                }
            }

            if (moveUpHeld && !Game1.player.CanMove)
            {
                Game1.upPolling += time.ElapsedGameTime.Milliseconds;
            }
            else if (moveDownHeld && !Game1.player.CanMove)
            {
                Game1.downPolling += time.ElapsedGameTime.Milliseconds;
            }
            else if (moveRightHeld && !Game1.player.CanMove)
            {
                Game1.rightPolling += time.ElapsedGameTime.Milliseconds;
            }
            else if (moveLeftHeld && !Game1.player.CanMove)
            {
                Game1.leftPolling += time.ElapsedGameTime.Milliseconds;
            }
            else if (moveUpReleased)
            {
                Game1.upPolling = 0;
            }
            else if (moveDownReleased)
            {
                Game1.downPolling = 0;
            }
            else if (moveRightReleased)
            {
                Game1.rightPolling = 0;
            }
            else if (moveLeftReleased)
            {
                Game1.leftPolling = 0;
            }

            if (Game1.debugMode)
            {
                if (currentKbState.IsKeyDown(Keys.Q))
                {
                    Game1.oldKBState.IsKeyDown(Keys.Q);
                }

                if (currentKbState.IsKeyDown(Keys.P) && !Game1.oldKBState.IsKeyDown(Keys.P))
                {
                    Game1.NewDay(0);
                }

                if (currentKbState.IsKeyDown(Keys.M) && !Game1.oldKBState.IsKeyDown(Keys.M))
                {
                    Game1.dayOfMonth = 28;
                    Game1.NewDay(0);
                }

                if (currentKbState.IsKeyDown(Keys.T) && !Game1.oldKBState.IsKeyDown(Keys.T))
                {
                    ClickToMovePatcher.addHour.Invoke();
                }

                if (currentKbState.IsKeyDown(Keys.Y) && !Game1.oldKBState.IsKeyDown(Keys.Y))
                {
                    ClickToMovePatcher.addMinute.Invoke();
                }

                if (currentKbState.IsKeyDown(Keys.D1) && !Game1.oldKBState.IsKeyDown(Keys.D1))
                {
                    Game1.warpFarmer("Mountain", 15, 35, false);
                }

                if (currentKbState.IsKeyDown(Keys.D2) && !Game1.oldKBState.IsKeyDown(Keys.D2))
                {
                    Game1.warpFarmer("Town", 35, 35, false);
                }

                if (currentKbState.IsKeyDown(Keys.D3) && !Game1.oldKBState.IsKeyDown(Keys.D3))
                {
                    Game1.warpFarmer("Farm", 64, 15, false);
                }

                if (currentKbState.IsKeyDown(Keys.D4) && !Game1.oldKBState.IsKeyDown(Keys.D4))
                {
                    Game1.warpFarmer("Forest", 34, 13, false);
                }

                if (currentKbState.IsKeyDown(Keys.D5) && !Game1.oldKBState.IsKeyDown(Keys.D4))
                {
                    Game1.warpFarmer("Beach", 34, 10, false);
                }

                if (currentKbState.IsKeyDown(Keys.D6) && !Game1.oldKBState.IsKeyDown(Keys.D6))
                {
                    Game1.warpFarmer("Mine", 18, 12, false);
                }

                if (currentKbState.IsKeyDown(Keys.D7) && !Game1.oldKBState.IsKeyDown(Keys.D7))
                {
                    Game1.warpFarmer("SandyHouse", 16, 3, false);
                }

                if (currentKbState.IsKeyDown(Keys.K) && !Game1.oldKBState.IsKeyDown(Keys.K))
                {
                    Game1.enterMine(Game1.mine.mineLevel + 1);
                }

                if (currentKbState.IsKeyDown(Keys.H) && !Game1.oldKBState.IsKeyDown(Keys.H))
                {
                    Game1.player.changeHat(Game1.random.Next(FarmerRenderer.hatsTexture.Height / 80 * 12));
                }

                if (currentKbState.IsKeyDown(Keys.I) && !Game1.oldKBState.IsKeyDown(Keys.I))
                {
                    Game1.player.changeHairStyle(Game1.random.Next(FarmerRenderer.hairStylesTexture.Height / 96 * 8));
                }

                if (currentKbState.IsKeyDown(Keys.J) && !Game1.oldKBState.IsKeyDown(Keys.J))
                {
                    Game1.player.changeShirt(Game1.random.Next(40));
                    Game1.player.changePants(
                        new Color(Game1.random.Next(255), Game1.random.Next(255), Game1.random.Next(255)));
                }

                if (currentKbState.IsKeyDown(Keys.L) && !Game1.oldKBState.IsKeyDown(Keys.L))
                {
                    Game1.player.changeShirt(Game1.random.Next(40));

                    Game1.player.changePants(
                        new Color(Game1.random.Next(255), Game1.random.Next(255), Game1.random.Next(255)));

                    Game1.player.changeHairStyle(Game1.random.Next(FarmerRenderer.hairStylesTexture.Height / 96 * 8));

                    if (Game1.random.NextDouble() < 0.5)
                    {
                        Game1.player.changeHat(Game1.random.Next(-1, FarmerRenderer.hatsTexture.Height / 80 * 12));
                    }
                    else
                    {
                        Game1.player.changeHat(-1);
                    }

                    Game1.player.changeHairColor(
                        new Color(Game1.random.Next(255), Game1.random.Next(255), Game1.random.Next(255)));

                    Game1.player.changeSkinColor(Game1.random.Next(16));
                }

                if (currentKbState.IsKeyDown(Keys.U) && !Game1.oldKBState.IsKeyDown(Keys.U))
                {
                    FarmHouse farmHouse = Game1.getLocationFromName("FarmHouse") as FarmHouse;
                    farmHouse.setWallpaper(Game1.random.Next(112), -1,
                        true);
                    farmHouse.setFloor(Game1.random.Next(40), -1, true);
                }

                if (currentKbState.IsKeyDown(Keys.F2))
                {
                    Game1.oldKBState.IsKeyDown(Keys.F2);
                }

                if (currentKbState.IsKeyDown(Keys.F5) && !Game1.oldKBState.IsKeyDown(Keys.F5))
                {
                    Game1.displayFarmer = !Game1.displayFarmer;
                }

                if (currentKbState.IsKeyDown(Keys.F6))
                {
                    Game1.oldKBState.IsKeyDown(Keys.F6);
                }

                if (currentKbState.IsKeyDown(Keys.F7) && !Game1.oldKBState.IsKeyDown(Keys.F7))
                {
                    Game1.drawGrid = !Game1.drawGrid;
                }

                if (currentKbState.IsKeyDown(Keys.B) && !Game1.oldKBState.IsKeyDown(Keys.B))
                {
                    Game1.player.shiftToolbar(false);
                }

                if (currentKbState.IsKeyDown(Keys.N) && !Game1.oldKBState.IsKeyDown(Keys.N))
                {
                    Game1.player.shiftToolbar(true);
                }

                if (currentKbState.IsKeyDown(Keys.F10) && !Game1.oldKBState.IsKeyDown(Keys.F10) && Game1.server is null)
                {
                    ___multiplayer.StartServer();
                }
            }
            else if (!Game1.player.UsingTool)
            {
                if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot1)
                    && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot1))
                {
                    Game1.player.CurrentToolIndex = 0;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot2)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot2))
                {
                    Game1.player.CurrentToolIndex = 1;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot3)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot3))
                {
                    Game1.player.CurrentToolIndex = 2;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot4)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot4))
                {
                    Game1.player.CurrentToolIndex = 3;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot5)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot5))
                {
                    Game1.player.CurrentToolIndex = 4;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot6)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot6))
                {
                    Game1.player.CurrentToolIndex = 5;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot7)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot7))
                {
                    Game1.player.CurrentToolIndex = 6;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot8)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot8))
                {
                    Game1.player.CurrentToolIndex = 7;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot9)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot9))
                {
                    Game1.player.CurrentToolIndex = 8;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot10)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot10))
                {
                    Game1.player.CurrentToolIndex = 9;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot11)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot11))
                {
                    Game1.player.CurrentToolIndex = 10;
                }
                else if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.inventorySlot12)
                         && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.inventorySlot12))
                {
                    Game1.player.CurrentToolIndex = 11;
                }
            }

            if (((Game1.options.gamepadControls && Game1.rightStickHoldTime >= Game1.emoteMenuShowTime
                                                && Game1.activeClickableMenu is null)
                 || (Game1.isOneOfTheseKeysDown(Game1.input.GetKeyboardState(), Game1.options.emoteButton)
                     && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.emoteButton))) && !Game1.debugMode
                && Game1.player.CanEmote())
            {
                if (Game1.player.CanMove)
                {
                    Game1.player.Halt();
                }

                Game1.emoteMenu = new EmoteMenu
                {
                    gamepadMode = Game1.options.gamepadControls && Game1.rightStickHoldTime >= Game1.emoteMenuShowTime,
                };

                Game1.timerUntilMouseFade = 0;
            }

            if (!Program.releaseBuild)
            {
                if (Game1.IsPressEvent(ref currentKbState, Keys.F3)
                    || Game1.IsPressEvent(ref currentPadState, Buttons.LeftStick))
                {
                    Game1.debugMode = !Game1.debugMode;
                    if (Game1.gameMode == Game1.errorLogMode)
                    {
                        Game1.gameMode = Game1.playingGameMode;
                    }
                }

                if (Game1.IsPressEvent(ref currentKbState, Keys.F8))
                {
                    __instance.requestDebugInput();
                }
            }

            if (currentKbState.IsKeyDown(Keys.F4) && !Game1.oldKBState.IsKeyDown(Keys.F4))
            {
                Game1.displayHUD = !Game1.displayHUD;
                Game1.playSound("smallSelect");
                if (!Game1.displayHUD)
                {
                    Game1.showGlobalMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3666"));
                }
            }

            bool menuButtonPressed = Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.menuButton)
                                     && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.menuButton);
            bool journalButtonPressed = Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.journalButton)
                                        && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.journalButton);
            bool mapButtonPressed = Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.mapButton)
                                    && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.mapButton);

            if (Game1.options.gamepadControls && !menuButtonPressed)
            {
                menuButtonPressed =
                    (currentPadState.IsButtonDown(Buttons.Start) && !Game1.oldPadState.IsButtonDown(Buttons.Start))
                    || (currentPadState.IsButtonDown(Buttons.B) && !Game1.oldPadState.IsButtonDown(Buttons.B));
            }

            if (Game1.options.gamepadControls && !journalButtonPressed)
            {
                journalButtonPressed = currentPadState.IsButtonDown(Buttons.Back)
                                       && !Game1.oldPadState.IsButtonDown(Buttons.Back);
            }

            if (Game1.options.gamepadControls && !mapButtonPressed)
            {
                mapButtonPressed = currentPadState.IsButtonDown(Buttons.Y)
                                   && !Game1.oldPadState.IsButtonDown(Buttons.Y);
            }

            if (menuButtonPressed && Game1.CanShowPauseMenu())
            {
                if (Game1.activeClickableMenu is null)
                {
                    Game1.PushUIMode();

                    Game1.activeClickableMenu = new GameMenu();

                    Game1.PopUIMode();
                }
                else if (Game1.activeClickableMenu.readyToClose())
                {
                    Game1.exitActiveMenu();
                }
            }

            if (Game1.dayOfMonth > 0 && Game1.player.CanMove && journalButtonPressed && !Game1.dialogueUp && !eventUp)
            {
                Game1.activeClickableMenu ??= new QuestLog();
            }
            else if (eventUp && Game1.CurrentEvent is not null && journalButtonPressed && !Game1.CurrentEvent.skipped
                     && Game1.CurrentEvent.skippable)
            {
                Game1.CurrentEvent.skipped = true;
                Game1.CurrentEvent.skipEvent();
                Game1.freezeControls = false;
            }

            if (Game1.options.gamepadControls && Game1.dayOfMonth > 0 && Game1.player.CanMove
                && Game1.isAnyGamePadButtonBeingPressed() && mapButtonPressed && !Game1.dialogueUp && !eventUp)
            {
                if (Game1.activeClickableMenu is null)
                {
                    Game1.PushUIMode();

                    Game1.activeClickableMenu = new GameMenu(4);

                    Game1.PopUIMode();
                }
            }
            else if (Game1.dayOfMonth > 0 && Game1.player.CanMove && mapButtonPressed && !Game1.dialogueUp && !eventUp
                     && Game1.activeClickableMenu is null)
            {
                Game1.PushUIMode();
                Game1.activeClickableMenu = new GameMenu(3);
                Game1.PopUIMode();
            }

            Game1.checkForRunButton(currentKbState);

            Game1.oldKBState = currentKbState;
            Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
            Game1.oldPadState = currentPadState;

            return false;
        }

        /// <summary>
        ///     A method called via Harmony before <see
        ///     cref="GameLocation.answerDialogueAction(string, string[])"/>.
        /// </summary>
        /// <param name="questionAndAnswer"></param>
        /// <param name="questionParams"></param>
        /// <returns></returns>
        private static bool BeforeGameLocationAnswerDialogueAction(string questionAndAnswer, string[] questionParams)
        {
            if (questionAndAnswer is not null && questionParams is not null && questionParams.Length != 0
                && questionParams[0] == "Minecart")
            {
                ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = false;
            }

            return true;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Horse.checkAction"/>.
        /// </summary>
        /// <returns>
        ///     Returns false, terminating prefixes and skipping the execution of the original
        ///     method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeHorseCheckAction(
            Horse __instance,
            Farmer who,
            GameLocation l,
            ref bool ___roomForHorseAtDismountTile,
            ref Vector2 ___dismountTile,
            ref bool __result)
        {
            HorseData horseData = ClickToMovePatcher.HorsesData.GetOrCreateValue(__instance);
            if (!horseData.CheckActionEnabled)
            {
                horseData.CheckActionEnabled = true;

                __result = false;
                return false;
            }

            if (who is not null && !who.canMove)
            {
                __result = false;
                return false;
            }

            if (__instance.rider is null)
            {
                __instance.mutex.RequestLock(
                    delegate
                    {
                        if (who.mount is not null || __instance.rider is not null
                                              || who.FarmerSprite.PauseForSingleAnimation)
                        {
                            __instance.mutex.ReleaseLock();
                        }
                        else if ((__instance.getOwner() == Game1.player
                                  || (__instance.getOwner() is null
                                      && (string.IsNullOrEmpty(Game1.player.horseName.Value)
                                          || Utility.findHorseForPlayer(Game1.player.UniqueMultiplayerID) is null)))
                                 && __instance.Name.Length <= 0)
                        {
                            foreach (Building building in (Game1.getLocationFromName("Farm") as Farm).buildings)
                            {
                                if (building.daysOfConstructionLeft.Value <= 0 && building is Stable stable)
                                {
                                    if (stable.getStableHorse() == __instance)
                                    {
                                        stable.owner.Value = who.UniqueMultiplayerID;
                                        stable.updateHorseOwnership();
                                    }
                                    else if (stable.owner.Value == who.UniqueMultiplayerID)
                                    {
                                        stable.owner.Value = 0;
                                        stable.updateHorseOwnership();
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(Game1.player.horseName.Value))
                            {
                                Game1.activeClickableMenu = new NamingMenu(
                                    __instance.nameHorse,
                                    Game1.content.LoadString("Strings\\Characters:NameYourHorse"),
                                    Game1.content.LoadString("Strings\\Characters:DefaultHorseName"));
                            }
                        }
                        else if (who.CurrentToolIndex >= 0 && who.items.Count > who.CurrentToolIndex && who.Items[who.CurrentToolIndex] is Hat hat)
                        {
                            if (__instance.hat.Value is not null)
                            {
                                Game1.createItemDebris(
                                    __instance.hat.Value,
                                    __instance.position.Value,
                                    __instance.facingDirection.Value);
                                __instance.hat.Value = null;
                            }
                            else
                            {
                                who.Items[who.CurrentToolIndex] = null;
                                __instance.hat.Value = hat;
                                Game1.playSound("dirtyHit");
                            }

                            __instance.mutex.ReleaseLock();
                        }
                        else if (!ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse)
                        {
                            __instance.rider = who;
                            __instance.rider.freezePause = 5000;
                            __instance.rider.synchronizedJump(6f);
                            __instance.rider.Halt();

                            if (__instance.rider.Position.X < __instance.Position.X)
                            {
                                __instance.rider.faceDirection(1);
                            }

                            l.playSound("dwop");
                            __instance.mounting.Value = true;
                            __instance.rider.isAnimatingMount = true;
                            __instance.rider.completelyStopAnimatingOrDoingAction();
                            __instance.rider.faceGeneralDirection(
                                Utility.PointToVector2(__instance.GetBoundingBox().Center),
                                0,
                                false,
                                false);
                        }
                    });

                __result = true;
                return false;
            }

            __instance.dismounting.Value = true;
            __instance.rider.isAnimatingMount = true;
            __instance.farmerPassesThrough = false;
            __instance.rider.TemporaryPassableTiles.Clear();

            Vector2 position = Utility.recursiveFindOpenTileForCharacter(
                __instance.rider,
                __instance.rider.currentLocation,
                __instance.rider.getTileLocation(),
                8);

            __instance.Position = new Vector2(
                (position.X * Game1.tileSize) + (Game1.tileSize / 2f) - (__instance.GetBoundingBox().Width / 2f),
                (position.Y * Game1.tileSize) + 4f);

            ___roomForHorseAtDismountTile = !__instance.currentLocation.isCollidingPosition(
                                                __instance.GetBoundingBox(),
                                                Game1.viewport,
                                                true,
                                                0,
                                                false,
                                                __instance);

            __instance.Position = __instance.rider.Position;
            __instance.dismounting.Value = false;
            __instance.rider.isAnimatingMount = false;
            __instance.Halt();

            if (!position.Equals(Vector2.Zero) && Vector2.Distance(position, __instance.rider.getTileLocation()) < 2f)
            {
                __instance.rider.synchronizedJump(6f);
                l.playSound("dwop");
                __instance.rider.freezePause = 5000;
                __instance.rider.Halt();
                __instance.rider.xOffset = 0f;
                __instance.dismounting.Value = true;
                __instance.rider.isAnimatingMount = true;

                ___dismountTile = position;

                Game1.debugOutput = "dismount tile: " + position;
            }
            else
            {
                __instance.dismount();
            }

            __result = true;
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="tileLocation"></param>
        /// <returns></returns>
        private static bool BeforeMountainCheckAction(Mountain __instance, Location tileLocation)
        {
            if (__instance.map.GetLayer("Buildings").Tiles[tileLocation] is not null)
            {
                int tileIndex = __instance.map.GetLayer("Buildings").Tiles[tileLocation].TileIndex;

                if ((tileIndex == 958 || tileIndex == 1080 || tileIndex == 1081)
                    && Game1.MasterPlayer.mailReceived.Contains("ccBoilerRoom") && Game1.player.mount is null
                    && !Game1.player.isRidingHorse())
                {
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = true;
                }
            }

            return true;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Farmer.performSickAnimation"/>. It
        ///     replaces the original method, so we can invoke a callback when the animation ends
        ///     (see <see cref="OnFinishSickAnim"/>).
        /// </summary>
        /// <param name="__instance">The farmer instance.</param>
        /// <returns>
        ///     Returns false, terminating prefixes and skipping the execution of the original
        ///     method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeParmerPerformSickAnimation(Farmer __instance)
        {
            if (__instance.isEmoteAnimating)
            {
                __instance.EndEmoteAnimation();
            }

            __instance.isEating = false;

            FarmerData farmerData = ClickToMovePatcher.FarmersData.GetOrCreateValue(__instance);
            farmerData.IsBeingSick = true;

            __instance.FarmerSprite.animateOnce(224, 350f, 4, ClickToMovePatcher.OnFinishSickAnim);
            __instance.doEmote(12);

            return false;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Toolbar.receiveLeftClick(int, int, bool)"/>
        ///     that replaces it. This method allows the user to deselect an equipped
        ///     object so that the farmer doesn't have any equipped tool or active object.
        /// </summary>
        /// <returns>
        ///     Returns false, terminating prefixes and skipping the execution of the original
        ///     method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeToolbarReceiveLeftClick(int x, int y, List<ClickableComponent> ___buttons)
        {
            if (Game1.IsChatting || Game1.currentLocation is MermaidHouse || Game1.player.isEating || !Game1.displayFarmer)
            {
                return false;
            }

            if (Game1.player.usingTool.Value)
            {
                foreach (ClickableComponent button in ___buttons)
                {
                    if (button.containsPoint(x, y))
                    {
                        int toolIndex = Convert.ToInt32(button.name);

                        if (Game1.player.CurrentToolIndex == toolIndex)
                        {
                            ClickToMovePatcher.nextToolIndex = -1;
                        }
                        else
                        {
                            ClickToMovePatcher.nextToolIndex = toolIndex;
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
                    int toolIndex = Convert.ToInt32(button.name);

                    if (Game1.player.CurrentToolIndex == toolIndex)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                    else
                    {
                        Game1.player.CurrentToolIndex = toolIndex;

                        ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClearAutoSelectTool();

                        if (Game1.player.CurrentTool is MeleeWeapon weapon)
                        {
                            PathFindingController.MostRecentlyChosenMeleeWeapon = weapon;
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

        private static bool BeforeTownCheckAction(
                    Town __instance,
                    Location tileLocation,
                    xTile.Dimensions.Rectangle viewport,
                    Farmer who)
        {
            if (__instance.map.GetLayer("Buildings").Tiles[tileLocation] is not null && who.mount is null)
            {
                int tileIndex = __instance.map.GetLayer("Buildings").Tiles[tileLocation].TileIndex;
                if ((tileIndex == 958 || tileIndex == 1080 || tileIndex == 1081) && Game1.player.mount is null
                    && (__instance.currentEvent is null || !__instance.currentEvent.isFestival
                                                        || !__instance.currentEvent.checkAction(
                                                            tileLocation,
                                                            viewport,
                                                            who)) && !(Game1.player.getTileX() <= 70
                                                                       && (Game1.CurrentEvent is null
                                                                           || Game1.CurrentEvent.FestivalName != "Egg Festival"))
                    && Game1.MasterPlayer.mailReceived.Contains("ccBoilerRoom"))
                {
                    ClickToMovePatcher.pathFindingManager[Game1.currentLocation].PreventMountingHorse = true;
                }
            }

            return true;
        }

        /// <summary>
        ///     Delegate to be called after the sick animation ends. (see <see cref="BeforeParmerPerformSickAnimation"/>).
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> that was animated.</param>
        private static void OnFinishSickAnim(Farmer farmer)
        {
            ClickToMovePatcher.FarmersData.GetOrCreateValue(farmer).IsBeingSick = false;
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Child.checkAction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileChildCheckAction(
            IEnumerable<CodeInstruction> instructions)
        {
            // Check if CurrentToolIndex is greater than zero before accessing items.

            // Relevant CIL code:
            //     if (base.Age >= 3 && who.items.Count > who.CurrentToolIndex &&
            //         who.items[who.CurrentToolIndex] != null && who.Items[who.CurrentToolIndex] is Hat)
            //         IL_0079: ldarg.0
            //         IL_007a: call instance int32 StardewValley.NPC::get_Age()
            //         IL_007f: ldc.i4.3
            //         IL_0080: blt IL_014f ...
            //
            // Replace with:
            //     if (base.Age >= 3 && who.CurrentToolIndex >= 0
            //         && who.items.Count > who.CurrentToolIndex
            //         && who.items[who.CurrentToolIndex] != null
            //         && who.Items[who.CurrentToolIndex] is Hat)

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
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(Child)}.{nameof(Child.checkAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Event.addSpecificTemporarySprite"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileEventAddSpecificTemporarySprite(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator ilGenerator)
        {
            // Check if this farmer's CurrentToolIndex is -1.

            // Relevant CIL code:
            //     this.drawTool = true;
            //         IL_7411: ldarg.0
            //         IL_7412: ldc.i4.1
            //         IL_7413: stfld bool StardewValley.Event::drawTool
            //
            // Code to include after:
            //     if (this.farmer.CurrentToolIndex == -1)
            //     {
            //         this.farmer.CurrentToolIndex = 0;
            //     }

            MethodInfo getFarmer = AccessTools.Property(typeof(Event), nameof(Event.farmer)).GetGetMethod();
            MethodInfo getCurrentToolIndex =
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentToolIndex)).GetGetMethod();
            MethodInfo setCurrentToolIndex =
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentToolIndex)).GetSetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            int jumpTo = -1;

            Label jumpIfNotEqual = ilGenerator.DefineLabel();

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (jumpTo == -1 && codeInstructions[i].opcode == OpCodes.Stfld
                           && codeInstructions[i].operand is FieldInfo { Name: "drawTool" }
                           && codeInstructions[i - 1].opcode == OpCodes.Ldc_I4_1
                           && i + 1 < codeInstructions.Count)
                {
                    yield return codeInstructions[i];

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, getFarmer);
                    yield return new CodeInstruction(OpCodes.Callvirt, getCurrentToolIndex);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
                    yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpIfNotEqual);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, getFarmer);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Callvirt, setCurrentToolIndex);

                    jumpTo = i + 1;
                }
                else
                {
                    if (i == jumpTo)
                    {
                        codeInstructions[i].labels.Add(jumpIfNotEqual);
                    }

                    yield return codeInstructions[i];
                }
            }

            if (jumpTo == -1)
            {
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(Event)}.addSpecificTemporarySprite.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>A method called via Harmony to modify <see cref="Event.checkAction" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileEventCheckAction(IEnumerable<CodeInstruction> instructions)
        {
            // Reset the PathFindingController object associated with the current game location
            // for the case "LuauSoup" if specialEventVariable2 is not defined.

            // Relevant CIL code:
            //     if (!this.specialEventVariable2)
            //         IL_0e41: ldarg.0
            //         IL_0e42: ldfld bool StardewValley.Event::specialEventVariable2
            //         IL_0e47: brtrue.s IL_0e87
            //
            // Code to include after:
            //     ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();

            FieldInfo pathFindingManager = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.pathFindingManager));

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(PathFindingManager),
                nameof(PathFindingManager.GetOrCreate));
            MethodInfo reset = AccessTools.Method(typeof(PathFindingController), nameof(PathFindingController.Reset));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Ldfld && codeInstructions[i].operand is FieldInfo { Name: "specialEventVariable2" }
                && i + 1 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Brtrue)
                {
                    yield return codeInstructions[i];
                    i++;
                    yield return codeInstructions[i];
                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Callvirt, reset);

                    found = true;
                }
                else
                {
                    yield return codeInstructions[i];
                }
            }

            if (!found)
            {
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch the setter for {nameof(Event)}.{nameof(Event.checkAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>A method called via Harmony to modify <see cref="Event.checkForCollision" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileEventCheckForCollision(
            IEnumerable<CodeInstruction> instructions)
        {
            // Reset the ClickToMove object associated with the current game location
            // after the test for isFestival.

            // Relevant CIL code:
            //     if (who.IsLocalPlayer && this.isFestival)
            //         IL_00e5: ldarg.2
            //         IL_00e6: callvirt instance bool StardewValley.Farmer::get_IsLocalPlayer()
            //         IL_00eb: brfalse IL_0182
            //         IL_00f0: ldarg.0
            //         IL_00f1: ldfld bool StardewValley.Event::isFestival
            //         IL_00f6: brfalse IL_0182
            //
            // Code to include:
            //     ClickToMovePatcher.pathFindingManager[Game1.currentLocation].Reset();

            FieldInfo pathFindingManager = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.pathFindingManager));

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(PathFindingManager),
                nameof(PathFindingManager.GetOrCreate));
            MethodInfo reset = AccessTools.Method(typeof(PathFindingController), nameof(PathFindingController.Reset));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Ldfld && codeInstructions[i].operand is MethodInfo { Name: "isFestival" }
                && i + 1 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Brfalse)
                {
                    yield return codeInstructions[i];
                    i++;
                    yield return codeInstructions[i];
                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Callvirt, reset);
                }
                else
                {
                    yield return codeInstructions[i];
                }
            }

            if (!found)
            {
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch the setter for {nameof(Event)}.{nameof(Event.checkForCollision)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Game1.pressActionButton"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileGame1PressActionButton(
            IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            // Initialize the grab tile with the current clicked tile from the path finding controller.

            // Relevant CIL code:
            //     Vector2 grabTile = new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y) / Game1.tileSize;
            //     Vector2 cursorTile = grabTile;
            //     if (!Game1.wasMouseVisibleThisFrame || Game1.mouseCursorTransparency == 0 || !Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            //     {
            //         grabTile = Game1.player.GetGrabTile();
            //     }
            //         IL_0590: call int32 StardewValley.Game1::getOldMouseX()
            //         ...
            //         IL_05f8: callvirt instance valuetype[Microsoft.Xna.Framework] Microsoft.Xna.Framework.Vector2 StardewValley.Character::GetGrabTile()
            //         IL_05fd: stloc.3
            //
            // Replace with:
            //     Vector2 grabTile;
            //     Vector2 cursorTile;
            //     if ((ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile.X == -1 && ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile.Y == -1) || ClickToMovePatcher.controlPadActionButtonPressed)
            //     {
            //         grabTile = new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y) / Game1.tileSize;
            //         cursorTile = grabTile;
            //
            //         if (!Game1.wasMouseVisibleThisFrame || Game1.mouseCursorTransparency == 0 || !Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            //         {
            //             grabTile = Game1.player.GetGrabTile();
            //         }
            //     }
            //     else
            //     {
            //         grabTile = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile;
            //         cursorTile = grabTile;
            //     }

            FieldInfo pathFindingManager = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.pathFindingManager));
            FieldInfo pointX = AccessTools.Field(typeof(Point), nameof(Point.X));
            FieldInfo pointY = AccessTools.Field(typeof(Point), nameof(Point.Y));
            FieldInfo controlPadActionButtonPressed = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.controlPadActionButtonPressed));

            MethodInfo currentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(PathFindingManager),
                nameof(PathFindingManager.GetOrCreate));
            MethodInfo clickedTile =
                AccessTools.Property(typeof(PathFindingController), nameof(PathFindingController.ClickedTile)).GetGetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;
            int count = 0;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (count < 2 && codeInstructions[i].opcode == OpCodes.Call && codeInstructions[i].operand is MethodInfo { Name: "getOldMouseX" })
                {
                    count++;

                    // Modify the code upon finding the second call to getOldMouseX.
                    if (count == 2)
                    {
                        Label jumpCondition = ilGenerator.DefineLabel();
                        Label jumpIfBlock = ilGenerator.DefineLabel();
                        Label jumpElseBlock = ilGenerator.DefineLabel();
                        Label jumpUnconditional = ilGenerator.DefineLabel();

                        // if ((ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile.X == -1 && ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile.Y == -1) || Game1.controlpadActionButtonPressed)
                        yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager) { labels = codeInstructions[i].labels };
                        yield return new CodeInstruction(OpCodes.Call, currentLocation);
                        yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                        yield return new CodeInstruction(OpCodes.Callvirt, clickedTile);
                        yield return new CodeInstruction(OpCodes.Ldfld, pointX);
                        yield return new CodeInstruction(OpCodes.Ldc_R4, -1);
                        yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpCondition);

                        yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                        yield return new CodeInstruction(OpCodes.Call, currentLocation);
                        yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                        yield return new CodeInstruction(OpCodes.Callvirt, clickedTile);
                        yield return new CodeInstruction(OpCodes.Ldfld, pointY);
                        yield return new CodeInstruction(OpCodes.Ldc_R4, -1);
                        yield return new CodeInstruction(OpCodes.Beq_S, jumpIfBlock);

                        yield return new CodeInstruction(OpCodes.Ldsfld, controlPadActionButtonPressed) { labels = new List<Label>() { jumpCondition } };
                        yield return new CodeInstruction(OpCodes.Brfalse_S, jumpElseBlock);

                        // If block.
                        codeInstructions[i].labels = new List<Label>() { jumpIfBlock };
                        yield return codeInstructions[i];
                        i++;
                        for (; i < codeInstructions.Count; i++)
                        {
                            yield return codeInstructions[i];

                            if (codeInstructions[i].opcode == OpCodes.Callvirt && codeInstructions[i].operand is MethodInfo { Name: "GetGrabTile" } && i + 1 < codeInstructions.Count
                                && codeInstructions[i + 1].opcode == OpCodes.Stloc_3)
                            {
                                i++;
                                yield return codeInstructions[i];
                                yield return new CodeInstruction(OpCodes.Br_S, jumpUnconditional);
                                break;
                            }
                        }

                        // Else block.
                        // grabTile = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile;
                        yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager) { labels = new List<Label>() { jumpElseBlock } };
                        yield return new CodeInstruction(OpCodes.Call, currentLocation);
                        yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                        yield return new CodeInstruction(OpCodes.Callvirt, clickedTile);
                        yield return new CodeInstruction(OpCodes.Stloc_3);

                        // cursorTile = grabTile;
                        yield return new CodeInstruction(OpCodes.Ldloc_3);
                        yield return new CodeInstruction(OpCodes.Stloc_S, 4);

                        i++;
                        if (i < codeInstructions.Count)
                        {
                            codeInstructions[i].labels.Add(jumpUnconditional);
                            yield return codeInstructions[i];
                            found = true;
                        }
                    }
                    else
                    {
                        yield return codeInstructions[i];
                    }
                }
                else
                {
                    yield return codeInstructions[i];
                }
            }

            if (!found)
            {
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(Game1)}.{nameof(Game1.pressActionButton)}.\nThe block of code to modify wasn't found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Game1.pressUseToolButton"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileGame1PressUseToolButton(
            IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            // Get clicked information from the path finding controller.
            FieldInfo pathFindingManager = AccessTools.Field(typeof(ClickToMovePatcher), nameof(ClickToMovePatcher.pathFindingManager));
            FieldInfo vector2X = AccessTools.Field(typeof(Vector2), nameof(Vector2.X));
            FieldInfo vector2Y = AccessTools.Field(typeof(Vector2), nameof(Vector2.Y));
            FieldInfo wasMouseVisibleThisFrame = AccessTools.Field(typeof(Game1), nameof(Game1.wasMouseVisibleThisFrame));

            ConstructorInfo vector2Constructor = AccessTools.Constructor(typeof(Vector2), new Type[] { typeof(float), typeof(float) });

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getPlayer =
                AccessTools.Property(typeof(Game1), nameof(Game1.player)).GetGetMethod();
            MethodInfo getToolLocation =
                AccessTools.Method(typeof(Farmer), nameof(Farmer.GetToolLocation), new Type[] { typeof(bool) });
            MethodInfo getPlacementGrabTile = AccessTools.Method(typeof(Game1), nameof(Game1.GetPlacementGrabTile));
            MethodInfo getZero = AccessTools.Property(typeof(Vector2), nameof(Vector2.Zero)).GetGetMethod();
            MethodInfo vector2Inequality = AccessTools.Method(typeof(Vector2), "op_Inequality");

            MethodInfo getOrCreate = AccessTools.Method(
                typeof(PathFindingManager),
                nameof(PathFindingManager.GetOrCreate));
            MethodInfo getClickPoint =
                AccessTools.Property(typeof(PathFindingController), nameof(PathFindingController.ClickPoint)).GetGetMethod();
            MethodInfo getClickedTile =
                AccessTools.Property(typeof(PathFindingController), nameof(PathFindingController.ClickedTile)).GetGetMethod();
            MethodInfo getGrabTile =
                AccessTools.Property(typeof(PathFindingController), nameof(PathFindingController.GrabTile)).GetGetMethod();
            MethodInfo setGrabTile =
                AccessTools.Property(typeof(PathFindingController), nameof(PathFindingController.GrabTile)).GetSetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                // Relevant CIL code:
                //     Vector2 position = ((!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y));
                //         ldsfld bool StardewValley.Game1::wasMouseVisibleThisFrame
                //         ...
                //         stloc.2
                //
                // Replace with:
                //     Vector2 position;
                //     if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.X == -1 && ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.Y == -1)
                //     {
                //         position = ((!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y));
                //     }
                //     else
                //     {
                //         position = ((!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint);
                //     }

                if (!found && codeInstructions[i].opcode == OpCodes.Ldsfld && codeInstructions[i].operand is FieldInfo { Name: "wasMouseVisibleThisFrame" })
                {
                    Label jumpFalse = ilGenerator.DefineLabel();
                    Label jumpEndIf = ilGenerator.DefineLabel();

                    // if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.X == -1 && ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.Y == -1)
                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager) { labels = codeInstructions[i].labels };
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                    yield return new CodeInstruction(OpCodes.Ldfld, vector2X);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, -1);
                    yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpFalse);

                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                    yield return new CodeInstruction(OpCodes.Ldfld, vector2Y);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, -1);
                    yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpFalse);

                    // If block.
                    // Replicate the original code.
                    codeInstructions[i].labels = new List<Label>();
                    yield return codeInstructions[i];
                    i++;
                    for (; i < codeInstructions.Count; i++)
                    {
                        yield return codeInstructions[i];

                        if (codeInstructions[i].opcode == OpCodes.Stloc_2)
                        {
                            yield return new CodeInstruction(OpCodes.Br_S, jumpEndIf);
                            break;
                        }
                    }

                    // Else block.
                    // position = ((!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint);
                    yield return new CodeInstruction(OpCodes.Ldsfld, wasMouseVisibleThisFrame) { labels = new List<Label>() { jumpFalse } };

                    jumpFalse = ilGenerator.DefineLabel();
                    Label jumpUnconditional = ilGenerator.DefineLabel();

                    yield return new CodeInstruction(OpCodes.Brfalse_S, jumpFalse);
                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                    yield return new CodeInstruction(OpCodes.Br_S, jumpUnconditional);
                    yield return new CodeInstruction(OpCodes.Call, getPlayer) { labels = new List<Label>() { jumpFalse } };
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Callvirt, getToolLocation);
                    yield return new CodeInstruction(OpCodes.Stloc_2) { labels = new List<Label>() { jumpUnconditional } };

                    // Next modification.
                    bool first = true;
                    i++;
                    for (; i < codeInstructions.Count; i++)
                    {
                        if (first)
                        {
                            codeInstructions[i].labels.Add(jumpEndIf);
                            first = false;
                        }

                        // Relevant CIL code:
                        //     Vector2 tile = new Vector2(position.X / Game1.tileSize, position.Y / Game1.tileSize);
                        //         IL_03e4: ldloca.s 7
                        //         ...
                        //         IL_03fe: call instance void [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Vector2::.ctor(float32, float32)
                        //
                        // Replace with:
                        //     Vector2 tile;
                        //     if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.X == -1 && ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.Y == -1)
                        //     {
                        //         tile = new Vector2(position.X / Game1.tileSize, position.Y / Game1.tileSize);
                        //     }
                        //     else
                        //     {
                        //         tile = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile;
                        //     }

                        if (!found && codeInstructions[i].opcode == OpCodes.Ldloca_S && (codeInstructions[i].operand as LocalBuilder).LocalIndex == 7)
                        {
                            jumpFalse = ilGenerator.DefineLabel();

                            // if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.X == -1 && ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickPoint.Y == -1)
                            yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager) { labels = codeInstructions[i].labels };
                            yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                            yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                            yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                            yield return new CodeInstruction(OpCodes.Ldfld, vector2X);
                            yield return new CodeInstruction(OpCodes.Ldc_R4, -1);
                            yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpFalse);

                            yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                            yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                            yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                            yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                            yield return new CodeInstruction(OpCodes.Ldfld, vector2Y);
                            yield return new CodeInstruction(OpCodes.Ldc_R4, -1);
                            yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpFalse);

                            // If block.
                            // Replicate original code.
                            codeInstructions[i].labels = new List<Label>();
                            yield return codeInstructions[i];
                            i++;
                            for (; i < codeInstructions.Count; i++)
                            {
                                yield return codeInstructions[i];

                                if (codeInstructions[i].opcode == OpCodes.Call && codeInstructions[i].operand is ConstructorInfo)
                                {
                                    jumpEndIf = ilGenerator.DefineLabel();
                                    yield return new CodeInstruction(OpCodes.Br_S, jumpEndIf);
                                    break;
                                }
                            }

                            // Else block.
                            // tile = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].ClickedTile;
                            yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager) { labels = new List<Label>() { jumpFalse } };
                            yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                            yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                            yield return new CodeInstruction(OpCodes.Callvirt, getClickedTile);
                            yield return new CodeInstruction(OpCodes.Stloc_S, 7);

                            // Next modification.
                            first = true;
                            i++;
                            for (; i < codeInstructions.Count; i++)
                            {
                                if (first)
                                {
                                    codeInstructions[i].labels.Add(jumpEndIf);
                                    first = false;
                                }

                                // Relevant CIL code:
                                //     Vector2 grabTile = Game1.GetPlacementGrabTile();
                                //         IL_053e: call valuetype [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Vector2 StardewValley.Game1::GetPlacementGrabTile()
                                //         IL_0543: stloc.s 8
                                //
                                // Replace with:
                                //     Vector2 grabTile;
                                //     if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].GrabTile != Vector2.Zero)
                                //     {
                                //         grabTile = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].GrabTile;
                                //         ClickToMovePatcher.pathFindingManager[Game1.currentLocation].GrabTile = Vector2.Zero;
                                //     }
                                //     else
                                //     {
                                //         grabTile = Game1.GetPlacementGrabTile();
                                //     }

                                if (!found && codeInstructions[i].opcode == OpCodes.Call && codeInstructions[i].operand is MethodInfo { Name: "GetPlacementGrabTile" }
                                && i + 2 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Stloc_S)
                                {
                                    jumpFalse = ilGenerator.DefineLabel();

                                    // if (ClickToMovePatcher.pathFindingManager[Game1.currentLocation].GrabTile != Vector2.Zero)
                                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager) { labels = codeInstructions[i].labels };
                                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                                    yield return new CodeInstruction(OpCodes.Callvirt, getGrabTile);
                                    yield return new CodeInstruction(OpCodes.Call, getZero);
                                    yield return new CodeInstruction(OpCodes.Call, vector2Inequality);
                                    yield return new CodeInstruction(OpCodes.Brtrue_S, jumpFalse);

                                    // If block.
                                    // grabTile = ClickToMovePatcher.pathFindingManager[Game1.currentLocation].GrabTile;
                                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                                    yield return new CodeInstruction(OpCodes.Callvirt, getGrabTile);
                                    yield return new CodeInstruction(OpCodes.Stloc_S, 8);

                                    // ClickToMovePatcher.pathFindingManager[Game1.currentLocation].GrabTile = Vector2.Zero;
                                    yield return new CodeInstruction(OpCodes.Ldsfld, pathFindingManager);
                                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                                    yield return new CodeInstruction(OpCodes.Callvirt, getOrCreate);
                                    yield return new CodeInstruction(OpCodes.Call, getZero);
                                    yield return new CodeInstruction(OpCodes.Callvirt, setGrabTile);

                                    jumpEndIf = ilGenerator.DefineLabel();
                                    yield return new CodeInstruction(OpCodes.Br_S, jumpEndIf);

                                    // Else block.
                                    // Return original code.
                                    codeInstructions[i].labels = new List<Label>() { jumpFalse };
                                    yield return codeInstructions[i];
                                    i++;
                                    yield return codeInstructions[i];
                                    i++;
                                    codeInstructions[i].labels.Add(jumpEndIf);
                                    yield return codeInstructions[i];
                                    found = true;
                                }
                                else
                                {
                                    yield return codeInstructions[i];
                                }
                            }
                        }
                        else
                        {
                            yield return codeInstructions[i];
                        }
                    }
                }
                else
                {
                    yield return codeInstructions[i];
                }
            }

            if (!found)
            {
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(Game1)}.{nameof(Game1.pressUseToolButton)}.\nSome block of code to modify wasn't found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Utility.tryToPlaceItem"/>. It
        ///     introduces a call to <see cref="TryToPlaceItemTranspiler"/> after the player places
        ///     an item.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileTryToPlaceItem(IEnumerable<CodeInstruction> instructions)
        {
            // Relevant CIL code:
            //     Game1.player.reduceActiveItemByOne();
            //         IL_0058: call class StardewValley.Farmer StardewValley.Game1::get_player()
            //         IL_005d: callvirt instance void StardewValley.Farmer::reduceActiveItemByOne()
            //
            // Insert code after:
            //     ClickToMovePatcher.TryToPlaceItemTranspiler();

            MethodInfo reduceActiveItemByOne = AccessTools.Method(typeof(Farmer), nameof(Farmer.reduceActiveItemByOne));
            MethodInfo tryToPlaceItemTranspiler =
                SymbolExtensions.GetMethodInfo(() => ClickToMovePatcher.TryToPlaceItemTranspiler());

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
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(Utility)}.{nameof(Utility.tryToPlaceItem)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Wand.DoFunction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileWandDoFunction(IEnumerable<CodeInstruction> instructions)
        {
            // Set the farmer CurrentToolIndex to -1.

            // Relevant CIL code:
            //     location.playSound("wand");
            //         IL_0102: ldarg.1
            //         IL_0103: ldstr "wand"
            //         IL_0108: ldc.i4.0
            //         IL_0109: callvirt instance void StardewValley.GameLocation::playSound(string, valuetype StardewValley.Network.NetAudio / SoundContext)
            //
            // Code to insert after:
            //     Game1.player.CurrentToolIndex = -1;

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
                ClickToMovePatcher.monitor.Log(
                    $"Failed to patch {nameof(Wand)}.{nameof(Wand.DoFunction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     Deselects the farmer's active object after they place a bomb.
        /// </summary>
        private static void TryToPlaceItemTranspiler()
        {
            if (Game1.player.ActiveObject != null && (Game1.player.ActiveObject.ParentSheetIndex == ObjectId.CherryBomb
                                                      || Game1.player.ActiveObject.ParentSheetIndex == ObjectId.Bomb
                                                      || Game1.player.ActiveObject.ParentSheetIndex == ObjectId.MegaBomb))
            {
                Game1.player.CurrentToolIndex = -1;
            }
        }

        /// <summary>
        ///     Keeps data about an <see cref="Horse"/> object.
        /// </summary>
        internal class HorseData
        {
            public bool CheckActionEnabled = true;
        }

        /// <summary>
        ///     Keeps data about a <see cref="Farmer"/> object.
        /// </summary>
        private class FarmerData
        {
            /// <summary>
            ///     Gets or sets a value indicating whether the <see cref="Farmer"/> is being sick.
            /// </summary>
            public bool IsBeingSick { get; set; }
        }
    }
}
