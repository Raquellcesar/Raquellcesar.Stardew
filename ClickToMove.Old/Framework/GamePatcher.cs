// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="GamePatcher.cs">
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
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Input;

    using Raquellcesar.Stardew.Common;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Locations;
    using StardewValley.Menus;
    using StardewValley.Minigames;
    using StardewValley.Objects;
    using StardewValley.Quests;
    using StardewValley.Tools;
    using StardewValley.Util;

    using xTile.Dimensions;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    public static class GamePatcher
    {
        private static IReflectedProperty<Dictionary<Game1.MusicContext, KeyValuePair<string, bool>>>
            requestedMusicTracks;

        /// <summary>
        ///     A reference to the private property <see cref="Game1.thumbstickToMouseModifier"/>.
        ///     Needed for the reimplementation of <see cref="Game1.UpdateControlInput"/>.
        /// </summary>
        private static IReflectedProperty<float> thumbstickToMouseModifier;

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

        private static IReflectedMethod checkIfDialogueIsQuestion;
        private static bool lastMouseLeftButtonDown;

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">
        ///     The Harmony patching API.
        /// </param>
        public static void Hook(HarmonyInstance harmony)
        {
            GamePatcher.requestedMusicTracks =
                ClickToMoveManager.Reflection.GetProperty<Dictionary<Game1.MusicContext, KeyValuePair<string, bool>>>(
                    typeof(Game1),
                    "_requestedMusicTracks");
            GamePatcher.thumbstickToMouseModifier =
                ClickToMoveManager.Reflection.GetProperty<float>(typeof(Game1), "thumbstickToMouseModifier");

            GamePatcher.addHour = ClickToMoveManager.Reflection.GetMethod(typeof(Game1), "addHour");
            GamePatcher.addMinute = ClickToMoveManager.Reflection.GetMethod(typeof(Game1), "addMinute");
            GamePatcher.checkIfDialogueIsQuestion =
                ClickToMoveManager.Reflection.GetMethod(typeof(Game1), "checkIfDialogueIsQuestion");

            harmony.Patch(
                AccessTools.Method(typeof(Game1), "_update"),
                transpiler: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.TranspileUpdate)));

            harmony.Patch(
                AccessTools.Property(typeof(Game1), nameof(Game1.currentMinigame)).GetSetMethod(),
                transpiler: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.TranspileSetCurrentMinigame)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.didPlayerJustLeftClick)),
                postfix: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.AfterDidPlayerJustLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.didPlayerJustRightClick)),
                postfix: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.AfterDidPlayerJustRightClick)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.drawObjectDialogue)),
                postfix: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.AfterDrawObjectDialogue)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.exitActiveMenu)),
                postfix: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.AfterExitActiveMenu)));

            /*harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.pressActionButton)),
                new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.BeforePressActionButton)));*/

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.pressActionButton)),
                transpiler: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.TranspilePressActionButton)));

            /*harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.pressUseToolButton)),
                new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.BeforepressUseToolButton)));*/

            harmony.Patch(
                AccessTools.Method(typeof(Game1), nameof(Game1.pressUseToolButton)),
                transpiler: new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.TranspilePressUseToolButton)));

            harmony.Patch(
                AccessTools.Method(typeof(Game1), "UpdateControlInput"),
                new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.BeforeUpdateControlInput)));

            harmony.Patch(
                AccessTools.Method(
                    typeof(Game1),
                    "warpFarmer",
                    new[] { typeof(LocationRequest), typeof(int), typeof(int), typeof(int) }),
                new HarmonyMethod(typeof(GamePatcher), nameof(GamePatcher.BeforeWarpFarmer)));
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Game1.pressUseToolButton"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspilePressUseToolButton(
            IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            // Get clicked information from the path finding controller.
            
            FieldInfo pointX = AccessTools.Field(typeof(Point), nameof(Point.X));
            FieldInfo pointY = AccessTools.Field(typeof(Point), nameof(Point.Y));
            FieldInfo wasMouseVisibleThisFrame = AccessTools.Field(typeof(Game1), nameof(Game1.wasMouseVisibleThisFrame));

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getPlayer =
                AccessTools.Property(typeof(Game1), nameof(Game1.player)).GetGetMethod();
            MethodInfo getToolLocation =
                AccessTools.Method(typeof(Farmer), nameof(Farmer.GetToolLocation), new Type[] { typeof(bool) });
            MethodInfo getPlacementGrabTile = AccessTools.Method(typeof(Game1), nameof(Game1.GetPlacementGrabTile));
            MethodInfo getPointZero = AccessTools.Property(typeof(Point), nameof(Point.Zero)).GetGetMethod();
            MethodInfo pointInequality = AccessTools.Method(typeof(Point), "op_Inequality");
            MethodInfo pointToVector2 =
                AccessTools.Method(typeof(Utility), nameof(Utility.PointToVector2));

            MethodInfo getOrCreate = AccessTools.Method(
                typeof(ClickToMoveManager),
                nameof(ClickToMoveManager.GetOrCreate));
            MethodInfo getClickPoint =
                AccessTools.Property(typeof(ClickToMove), nameof(ClickToMove.ClickPoint)).GetGetMethod();
            MethodInfo getClickedTile =
                AccessTools.Property(typeof(ClickToMove), nameof(ClickToMove.ClickedTile)).GetGetMethod();
            MethodInfo getGrabTile =
                AccessTools.Property(typeof(ClickToMove), nameof(ClickToMove.GrabTile)).GetGetMethod();
            MethodInfo setGrabTile =
                AccessTools.Property(typeof(ClickToMove), nameof(ClickToMove.GrabTile)).GetSetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                // Relevant CIL code:
                //     Vector2 position = Vector2 position = (!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y);
                //         ldsfld bool StardewValley.Game1::wasMouseVisibleThisFrame
                //         ...
                //         stloc.2
                //
                // Replace with:
                //     Vector2 position;
                //     if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.X == -1 && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.Y == -1)
                //     {
                //         position = (!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y);
                //     }
                //     else
                //     {
                //         position = (!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : Utility.PointToVector2(ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint);
                //     }

                if (!found && codeInstructions[i].opcode == OpCodes.Ldsfld && codeInstructions[i].operand is FieldInfo { Name: "wasMouseVisibleThisFrame" })
                {
                    Label jumpFalse = ilGenerator.DefineLabel();
                    Label jumpEndIf = ilGenerator.DefineLabel();

                    // if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.X == -1 && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.Y == -1)
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation) { labels = codeInstructions[i].labels };
                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                    yield return new CodeInstruction(OpCodes.Ldfld, pointX);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
                    yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpFalse);

                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                    yield return new CodeInstruction(OpCodes.Ldfld, pointY);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
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
                    // position = (!Game1.wasMouseVisibleThisFrame) ? Game1.player.GetToolLocation() : Utility.PointToVector2(ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint);
                    yield return new CodeInstruction(OpCodes.Ldsfld, wasMouseVisibleThisFrame) { labels = new List<Label>() { jumpFalse } };

                    jumpFalse = ilGenerator.DefineLabel();
                    Label jumpUnconditional = ilGenerator.DefineLabel();

                    yield return new CodeInstruction(OpCodes.Brfalse_S, jumpFalse);
                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                    yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                    yield return new CodeInstruction(OpCodes.Call, pointToVector2);
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
                        //     if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.X == -1 && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.Y == -1)
                        //     {
                        //         tile = new Vector2(position.X / Game1.tileSize, position.Y / Game1.tileSize);
                        //     }
                        //     else
                        //     {
                        //         tile = Utility.PointToVector2(ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickedTile);
                        //     }

                        if (!found && codeInstructions[i].opcode == OpCodes.Ldloca_S && (codeInstructions[i].operand as LocalBuilder).LocalIndex == 7)
                        {
                            jumpFalse = ilGenerator.DefineLabel();

                            // if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.X == -1 && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.Y == -1)
                            yield return new CodeInstruction(OpCodes.Call, getCurrentLocation) { labels = codeInstructions[i].labels };
                            yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                            yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                            yield return new CodeInstruction(OpCodes.Ldfld, pointX);
                            yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
                            yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpFalse);

                            yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                            yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                            yield return new CodeInstruction(OpCodes.Callvirt, getClickPoint);
                            yield return new CodeInstruction(OpCodes.Ldfld, pointY);
                            yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
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
                            // tile = Utility.PointToVector2(ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickedTile);
                            yield return new CodeInstruction(OpCodes.Call, getCurrentLocation) { labels = new List<Label>() { jumpFalse } };
                            yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                            yield return new CodeInstruction(OpCodes.Callvirt, getClickedTile);
                            yield return new CodeInstruction(OpCodes.Call, pointToVector2);
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
                                //     if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile != Point.Zero)
                                //     {
                                //         grabTile = Utility.PointToVector2(ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile);
                                //         ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile = Point.Zero;
                                //     }
                                //     else
                                //     {
                                //         grabTile = Game1.GetPlacementGrabTile();
                                //     }

                                if (!found && codeInstructions[i].opcode == OpCodes.Call && codeInstructions[i].operand is MethodInfo { Name: "GetPlacementGrabTile" }
                                && i + 2 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Stloc_S)
                                {
                                    jumpFalse = ilGenerator.DefineLabel();

                                    // if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile != Point.Zero)
                                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation) { labels = codeInstructions[i].labels };
                                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                                    yield return new CodeInstruction(OpCodes.Callvirt, getGrabTile);
                                    yield return new CodeInstruction(OpCodes.Call, getPointZero);
                                    yield return new CodeInstruction(OpCodes.Call, pointInequality);
                                    yield return new CodeInstruction(OpCodes.Brfalse_S, jumpFalse);

                                    // If block.
                                    // grabTile = Utility.PointToVector2(ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile);
                                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                                    yield return new CodeInstruction(OpCodes.Callvirt, getGrabTile);
                                    yield return new CodeInstruction(OpCodes.Call, pointToVector2);
                                    yield return new CodeInstruction(OpCodes.Stloc_S, 8);

                                    // ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile = Point.Zero;
                                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                                    yield return new CodeInstruction(OpCodes.Call, getPointZero);
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
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(Game1)}.{nameof(Game1.pressUseToolButton)}.\nSome block of code to modify wasn't found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Game1.pressActionButton"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspilePressActionButton(
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
            //     if ((ClickToMoveManager.getOrCreate(Game1.currentLocation).ClickedTile.X == -1 && ClickToMoveManager.getOrCreate(Game1.currentLocation).ClickedTile.Y == -1))
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
            //         grabTile = Utility.PointToVector2(ClickToMoveManager.getOrCreate(Game1.currentLocation).ClickedTile);
            //         cursorTile = grabTile;
            //     }

            FieldInfo pointX = AccessTools.Field(typeof(Point), nameof(Point.X));
            FieldInfo pointY = AccessTools.Field(typeof(Point), nameof(Point.Y));

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(ClickToMoveManager),
                nameof(ClickToMoveManager.GetOrCreate));
            MethodInfo clickedTile =
                AccessTools.Property(typeof(ClickToMove), nameof(ClickToMove.ClickedTile)).GetGetMethod();
            MethodInfo pointToVector2 =
                AccessTools.Method(typeof(Utility), nameof(Utility.PointToVector2));

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
                        Label jumpElseBlock = ilGenerator.DefineLabel();
                        Label jumpUnconditional = ilGenerator.DefineLabel();

                        // if ((ClickToMoveManager.getOrCreate(Game1.currentLocation).ClickedTile.X == -1 && ClickToMoveManager.getOrCreate(Game1.currentLocation).ClickedTile.Y == -1) || Game1.controlpadActionButtonPressed)
                        yield return new CodeInstruction(OpCodes.Call, getCurrentLocation) { labels = codeInstructions[i].labels };
                        yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                        yield return new CodeInstruction(OpCodes.Callvirt, clickedTile);
                        yield return new CodeInstruction(OpCodes.Ldfld, pointX);
                        yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
                        yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpElseBlock);

                        yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                        yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                        yield return new CodeInstruction(OpCodes.Callvirt, clickedTile);
                        yield return new CodeInstruction(OpCodes.Ldfld, pointY);
                        yield return new CodeInstruction(OpCodes.Ldc_I4_M1);
                        yield return new CodeInstruction(OpCodes.Bne_Un_S, jumpElseBlock);

                        // If block.
                        codeInstructions[i].labels = new List<Label>();
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
                        // grabTile = Utility.PointToVector2(ClickToMoveManager.getOrCreate(Game1.currentLocation).ClickedTile);
                        yield return new CodeInstruction(OpCodes.Call, getCurrentLocation) { labels = new List<Label>() { jumpElseBlock } };
                        yield return new CodeInstruction(OpCodes.Call, getOrCreate);
                        yield return new CodeInstruction(OpCodes.Callvirt, clickedTile);
                        yield return new CodeInstruction(OpCodes.Call, pointToVector2);
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
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(Game1)}.{nameof(Game1.pressActionButton)}.\nThe block of code to modify wasn't found.",
                    LogLevel.Error);
            }
        }

        public static void UpdateClickToMove(MouseState currentMouseState)
        {
            GameLocation location = Game1.currentLocation;

            if (location is not null)
            {
                if (Game1.currentMinigame is FishingGame fishingGame)
                {
                    location = ClickToMoveManager.Reflection.GetField<GameLocation>(fishingGame, "location").GetValue();
                }
                else if (Game1.currentMinigame is TargetGame targetGame)
                {
                    location = ClickToMoveManager.Reflection.GetField<GameLocation>(targetGame, "location").GetValue();
                }

                if (currentMouseState.LeftButton == ButtonState.Pressed
                    && Game1.oldMouseState.LeftButton == ButtonState.Released)
                {
                    ClickToMoveManager.GetOrCreate(location).OnClick(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                }
                else if (currentMouseState.LeftButton == ButtonState.Pressed
                         && Game1.oldMouseState.LeftButton == ButtonState.Pressed)
                {
                    ClickToMoveManager.GetOrCreate(location).OnClickHeld(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                }
                else if (currentMouseState.LeftButton == ButtonState.Released
                         && Game1.oldMouseState.LeftButton == ButtonState.Pressed)
                {
                    ClickToMoveManager.GetOrCreate(location).OnClickRelease(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                }

                ClickToMoveManager.GetOrCreate(location).Update();

                if (Game1.currentMinigame is FishingGame fishingGame2)
                {
                    GamePatcher.FishingGameReceiveClickToMoveKeyStates(fishingGame2, location);
                }
                else if (Game1.currentMinigame is TargetGame targetGame)
                {
                    GamePatcher.TargetGameReceiveMobileKeyStates(targetGame, location);
                }
            }
        }

        private static void AfterDidPlayerJustLeftClick(ref bool __result)
        {
            if (!__result)
            {
                if (Game1.currentLocation is not null)
                {
                    __result = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates
                        .UseToolButtonPressed;
                }
            }
        }

        private static void AfterDidPlayerJustRightClick(ref bool __result)
        {
            if (!__result)
            {
                if (Game1.currentLocation is not null)
                {
                    __result = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.ActionButtonPressed;
                }
            }
        }

        private static void AfterDrawObjectDialogue()
        {
            ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
        }

        private static void AfterExitActiveMenu()
        {
            ClickToMoveManager.OnScreenMenuClicked = false;

            if (Game1.currentLocation is not null)
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).OnCloseActiveMenu();

                if (Game1.input is not null)
                {
                    GamePadState gamePadState = Game1.input.GetGamePadState();
                    if (gamePadState.IsConnected && !gamePadState.IsButtonDown(Buttons.DPadUp)
                                                 && !gamePadState.IsButtonDown(Buttons.DPadDown)
                                                 && !gamePadState.IsButtonDown(Buttons.DPadLeft)
                                                 && !gamePadState.IsButtonDown(Buttons.DPadRight)
                                                 && !gamePadState.IsButtonDown(Buttons.LeftThumbstickUp)
                                                 && !gamePadState.IsButtonDown(Buttons.LeftThumbstickDown)
                                                 && !gamePadState.IsButtonDown(Buttons.LeftThumbstickLeft)
                                                 && !gamePadState.IsButtonDown(Buttons.LeftThumbstickRight))
                    {
                        ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.Reset();
                    }
                }
            }
        }

        /// <summary>A method called via Harmony before <see cref="Game1.pressActionButton" />.</summary>
        /// <returns>
        ///     Returns a boolean that, if false, terminates prefixes and skips the execution of the original method,
        ///     effectively replacing the original method.
        /// </returns>
        private static bool BeforePressActionButton(
            KeyboardState currentKBState,
            GamePadState currentPadState,
            ref bool __result)
        {
            if (Game1.IsChatting)
            {
                currentKBState = default(KeyboardState);
            }

            if (Game1.dialogueTyping)
            {
                bool consume = true;
                Game1.dialogueTyping = false;
                if (Game1.currentSpeaker is not null)
                {
                    Game1.currentDialogueCharacterIndex =
                        Game1.currentSpeaker.CurrentDialogue.Peek().getCurrentDialogue().Length;
                }
                else if (Game1.currentObjectDialogue.Count > 0)
                {
                    Game1.currentDialogueCharacterIndex = Game1.currentObjectDialogue.Peek().Length;
                }
                else
                {
                    consume = false;
                }

                Game1.dialogueTypingInterval = 0;
                Game1.oldKBState = currentKBState;
                Game1.oldMouseState = Game1.input.GetMouseState();
                Game1.oldPadState = currentPadState;
                if (consume)
                {
                    Game1.playSound("dialogueCharacterClose");

                    __result = false;
                    return false;
                }
            }

            if (Game1.dialogueUp && Game1.numberOfSelectedItems == -1)
            {
                if (Game1.isQuestion)
                {
                    Game1.isQuestion = false;
                    if (Game1.currentSpeaker is not null)
                    {
                        if (Game1.currentSpeaker.CurrentDialogue.Peek()
                            .chooseResponse(Game1.questionChoices[Game1.currentQuestionChoice]))
                        {
                            Game1.currentDialogueCharacterIndex = 1;
                            Game1.dialogueTyping = true;
                            Game1.oldKBState = currentKBState;
                            Game1.oldMouseState = Game1.input.GetMouseState();
                            Game1.oldPadState = currentPadState;

                            __result = false;
                            return false;
                        }
                    }
                    else
                    {
                        Game1.dialogueUp = false;
                        if (Game1.eventUp && Game1.currentLocation.afterQuestion is null)
                        {
                            Game1.currentLocation.currentEvent.answerDialogue(
                                Game1.currentLocation.lastQuestionKey,
                                Game1.currentQuestionChoice);
                            Game1.currentQuestionChoice = 0;
                            Game1.oldKBState = currentKBState;
                            Game1.oldMouseState = Game1.input.GetMouseState();
                            Game1.oldPadState = currentPadState;
                        }
                        else if (Game1.currentLocation.answerDialogue(
                            Game1.questionChoices[Game1.currentQuestionChoice]))
                        {
                            Game1.currentQuestionChoice = 0;
                            Game1.oldKBState = currentKBState;
                            Game1.oldMouseState = Game1.input.GetMouseState();
                            Game1.oldPadState = currentPadState;

                            __result = false;
                            return false;
                        }

                        if (Game1.dialogueUp)
                        {
                            Game1.currentDialogueCharacterIndex = 1;
                            Game1.dialogueTyping = true;
                            Game1.oldKBState = currentKBState;
                            Game1.oldMouseState = Game1.input.GetMouseState();
                            Game1.oldPadState = currentPadState;

                            __result = false;
                            return false;
                        }
                    }

                    Game1.currentQuestionChoice = 0;
                }

                string exitDialogue = null;
                if (Game1.currentSpeaker is not null)
                {
                    if (Game1.currentSpeaker.immediateSpeak)
                    {
                        Game1.currentSpeaker.immediateSpeak = false;

                        __result = false;
                        return false;
                    }

                    exitDialogue = Game1.currentSpeaker.CurrentDialogue.Count > 0
                                       ? Game1.currentSpeaker.CurrentDialogue.Peek().exitCurrentDialogue()
                                       : null;
                }

                if (exitDialogue is null)
                {
                    if (Game1.currentSpeaker is not null && Game1.currentSpeaker.CurrentDialogue.Count > 0
                                                     && Game1.currentSpeaker.CurrentDialogue.Peek().isOnFinalDialogue()
                                                     && Game1.currentSpeaker.CurrentDialogue.Count > 0)
                    {
                        Game1.currentSpeaker.CurrentDialogue.Pop();
                    }

                    Game1.dialogueUp = false;

                    if (Game1.messagePause)
                    {
                        Game1.pauseTime = 500f;
                    }

                    if (Game1.currentObjectDialogue.Count > 0)
                    {
                        Game1.currentObjectDialogue.Dequeue();
                    }

                    Game1.currentDialogueCharacterIndex = 0;

                    if (Game1.currentObjectDialogue.Count > 0)
                    {
                        Game1.dialogueUp = true;
                        Game1.questionChoices.Clear();
                        Game1.oldKBState = currentKBState;
                        Game1.oldMouseState = Game1.input.GetMouseState();
                        Game1.oldPadState = currentPadState;
                        Game1.dialogueTyping = true;
                        __result = false;
                        return false;
                    }

                    Game1.tvStation = -1;

                    if (Game1.currentSpeaker is not null && !Game1.currentSpeaker.Name.Equals("Gunther") && !Game1.eventUp
                        && !Game1.currentSpeaker.doingEndOfRouteAnimation)
                    {
                        Game1.currentSpeaker.doneFacingPlayer(Game1.player);
                    }

                    Game1.currentSpeaker = null;

                    if (!Game1.eventUp)
                    {
                        Game1.player.CanMove = true;
                    }
                    else if (Game1.currentLocation.currentEvent.CurrentCommand > 0
                             || Game1.currentLocation.currentEvent.specialEventVariable1)
                    {
                        if (!Game1.isFestival() || !Game1.currentLocation.currentEvent.canMoveAfterDialogue())
                        {
                            Game1.currentLocation.currentEvent.CurrentCommand++;
                        }
                        else
                        {
                            Game1.player.CanMove = true;
                        }
                    }

                    Game1.questionChoices.Clear();
                    Game1.playSound("smallSelect");
                }
                else
                {
                    Game1.playSound("smallSelect");
                    Game1.currentDialogueCharacterIndex = 0;
                    Game1.dialogueTyping = true;
                    GamePatcher.checkIfDialogueIsQuestion.Invoke();
                }

                Game1.oldKBState = currentKBState;
                Game1.oldMouseState = Game1.input.GetMouseState();
                Game1.oldPadState = currentPadState;

                if (Game1.questOfTheDay is not null && (bool)Game1.questOfTheDay.accepted
                                                && Game1.questOfTheDay is SocializeQuest)
                {
                    ((SocializeQuest)Game1.questOfTheDay).checkIfComplete();
                }

                __result = false;
                return false;
            }

            if (Game1.currentBillboard != 0)
            {
                Game1.currentBillboard = 0;
                Game1.player.CanMove = true;
                Game1.oldKBState = currentKBState;
                Game1.oldMouseState = Game1.input.GetMouseState();
                Game1.oldPadState = currentPadState;

                __result = false;
                return false;
            }

            if (!Game1.player.UsingTool && !Game1.pickingTool && !Game1.menuUp
                && (!Game1.eventUp || (Game1.currentLocation.currentEvent is not null
                                       && Game1.currentLocation.currentEvent.playerControlSequence))
                && !Game1.nameSelectUp && Game1.numberOfSelectedItems == -1 && !Game1.fadeToBlack)
            {
                if (Game1.wasMouseVisibleThisFrame && Game1.currentLocation is IAnimalLocation animalLocation)
                {
                    Vector2 mousePosition = new Vector2(
                        Game1.getOldMouseX() + Game1.viewport.X,
                        Game1.getOldMouseY() + Game1.viewport.Y);
                    if (Utility.withinRadiusOfPlayer((int)mousePosition.X, (int)mousePosition.Y, 1, Game1.player))
                    {
                        if (animalLocation.CheckPetAnimal(mousePosition, Game1.player))
                        {
                            __result = true;
                            return false;
                        }

                        if (Game1.didPlayerJustRightClick(true)
                            && animalLocation.CheckInspectAnimal(mousePosition, Game1.player))
                        {
                            __result = true;
                            return false;
                        }
                    }
                }

                Vector2 grabTile;
                if ((ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickedTile.X == -1
                     && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickedTile.Y == -1))
                {
                    grabTile = new Vector2(
                                   Game1.getOldMouseX() + Game1.viewport.X,
                                   Game1.getOldMouseY() + Game1.viewport.Y) / Game1.tileSize;

                    if (Game1.mouseCursorTransparency == 0f || !Game1.wasMouseVisibleThisFrame
                                                            || (!Game1.lastCursorMotionWasMouse
                                                                && (Game1.player.ActiveObject is null
                                                                    || (!Game1.player.ActiveObject.isPlaceable()
                                                                        && Game1.player.ActiveObject.Category
                                                                        != SObject.SeedsCategory))))
                    {
                        grabTile = Game1.player.GetGrabTile();
                        if (grabTile.Equals(Game1.player.getTileLocation()))
                        {
                            grabTile = Utility.getTranslatedVector2(grabTile, Game1.player.FacingDirection, 1f);
                        }
                    }

                    if (!Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
                    {
                        grabTile = Game1.player.GetGrabTile();
                        if (grabTile.Equals(Game1.player.getTileLocation()) && Game1.isAnyGamePadButtonBeingPressed())
                        {
                            grabTile = Utility.getTranslatedVector2(grabTile, Game1.player.FacingDirection, 1f);
                        }
                    }
                }
                else
                {
                    grabTile = new Vector2(
                        ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickedTile.X,
                        ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickedTile.Y);
                }

                Vector2 cursorTile = grabTile;
                if (Game1.eventUp && !Game1.isFestival())
                {
                    if (Game1.CurrentEvent is not null)
                    {
                        Game1.CurrentEvent.receiveActionPress((int)grabTile.X, (int)grabTile.Y);
                    }

                    Game1.oldKBState = currentKBState;
                    Game1.oldMouseState = Game1.input.GetMouseState();
                    Game1.oldPadState = currentPadState;

                    __result = false;
                    return false;
                }

                if (Game1.tryToCheckAt(grabTile, Game1.player))
                {
                    __result = false;
                    return false;
                }

                if (Game1.player.isRidingHorse())
                {
                    Game1.player.mount.checkAction(Game1.player, Game1.player.currentLocation);

                    __result = false;
                    return false;
                }

                if (!Game1.player.canMove)
                {
                    __result = false;
                    return false;
                }

                bool isPlacingObject = false;
                if (Game1.player.ActiveObject is not null && !(Game1.player.ActiveObject is Furniture))
                {
                    if (Game1.player.ActiveObject.performUseAction(Game1.currentLocation))
                    {
                        Game1.player.reduceActiveItemByOne();
                        Game1.oldKBState = currentKBState;
                        Game1.oldMouseState = Game1.input.GetMouseState();
                        Game1.oldPadState = currentPadState;

                        __result = false;
                        return false;
                    }

                    int stack = Game1.player.ActiveObject.Stack;
                    Game1.isCheckingNonMousePlacement = !Game1.IsPerformingMousePlacement() || Game1.isOneOfTheseKeysDown(currentKBState, Game1.options.actionButton);

                    Vector2 validPosition = Utility.GetNearbyValidPlacementPosition(
                        Game1.player,
                        Game1.currentLocation,
                        Game1.player.ActiveObject,
                        ((int)grabTile.X * Game1.tileSize) + (Game1.tileSize / 2),
                        ((int)grabTile.Y * Game1.tileSize) + (Game1.tileSize / 2));
                    if (!Game1.isCheckingNonMousePlacement && Game1.player.ActiveObject is Wallpaper
                                                           && Utility.tryToPlaceItem(
                                                               Game1.currentLocation,
                                                               Game1.player.ActiveObject,
                                                               (int)cursorTile.X * Game1.tileSize,
                                                               (int)cursorTile.Y * Game1.tileSize))
                    {
                        Game1.isCheckingNonMousePlacement = false;

                        __result = true;
                        return false;
                    }

                    if (Utility.tryToPlaceItem(
                        Game1.currentLocation,
                        Game1.player.ActiveObject,
                        (int)validPosition.X,
                        (int)validPosition.Y))
                    {
                        Game1.isCheckingNonMousePlacement = false;

                        __result = true;
                        return false;
                    }

                    if (!Game1.eventUp && (Game1.player.ActiveObject is null || Game1.player.ActiveObject.Stack < stack
                                                                             || Game1.player.ActiveObject
                                                                                 .isPlaceable()))
                    {
                        isPlacingObject = true;
                    }

                    Game1.isCheckingNonMousePlacement = false;
                }

                if (!isPlacingObject)
                {
                    grabTile.Y += 1f;
                    if (Game1.player.FacingDirection >= 0 && Game1.player.FacingDirection <= 3)
                    {
                        Vector2 normalizedOffset2 = grabTile - Game1.player.getTileLocation();
                        if (normalizedOffset2.X > 0f || normalizedOffset2.Y > 0f)
                        {
                            normalizedOffset2.Normalize();
                        }

                        if (Vector2.Dot(Utility.DirectionsTileVectors[Game1.player.FacingDirection], normalizedOffset2)
                            >= 0f && Game1.tryToCheckAt(grabTile, Game1.player))
                        {
                            __result = false;
                            return false;
                        }
                    }

                    if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject is Furniture furniture)
                    {
                        furniture.rotate();
                        Game1.playSound("dwoop");
                        Game1.oldKBState = currentKBState;
                        Game1.oldMouseState = Game1.input.GetMouseState();
                        Game1.oldPadState = currentPadState;

                        __result = false;
                        return false;
                    }

                    grabTile = Game1.player.getTileLocation();
                    if (Game1.tryToCheckAt(grabTile, Game1.player))
                    {
                        __result = false;
                        return false;
                    }

                    if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject is Furniture)
                    {
                        (Game1.player.ActiveObject as Furniture).rotate();
                        Game1.playSound("dwoop");
                        Game1.oldKBState = currentKBState;
                        Game1.oldMouseState = Game1.input.GetMouseState();
                        Game1.oldPadState = currentPadState;

                        __result = false;
                        return false;
                    }
                }

                if (!Game1.player.isEating && Game1.player.ActiveObject is not null && !Game1.dialogueUp && !Game1.eventUp
                    && !Game1.player.canOnlyWalk && !Game1.player.FarmerSprite.PauseForSingleAnimation
                    && !Game1.fadeToBlack && Game1.player.ActiveObject.Edibility != -300
                    && Game1.didPlayerJustRightClick(true))
                {
                    if (Game1.player.team.SpecialOrderRuleActive("SC_NO_FOOD")
                        && Game1.player.currentLocation is MineShaft
                        && (Game1.player.currentLocation as MineShaft).getMineArea() == 121)
                    {
                        Game1.addHUDMessage(
                            new HUDMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.13053"), 3));

                        __result = false;
                        return false;
                    }

                    if (Game1.buffsDisplay.hasBuff(25) && Game1.player.ActiveObject is not null
                                                       && !Game1.player.ActiveObject.HasContextTag("ginger_item"))
                    {
                        Game1.addHUDMessage(
                            new HUDMessage(
                                Game1.content.LoadString("Strings\\StringsFromCSFiles:Nauseous_CantEat"),
                                3));

                        __result = false;
                        return false;
                    }

                    Game1.player.faceDirection(2);
                    Game1.player.itemToEat = Game1.player.ActiveObject;
                    Game1.player.FarmerSprite.setCurrentSingleAnimation(304);
                    Game1.currentLocation.createQuestionDialogue(
                        Game1.objectInformation[Game1.player.ActiveObject.parentSheetIndex].Split('/').Length > 6
                        && Game1.objectInformation[Game1.player.ActiveObject.parentSheetIndex].Split('/')[6]
                            .Equals("drink")
                            ? Game1.content.LoadString(
                                "Strings\\StringsFromCSFiles:Game1.cs.3159",
                                Game1.player.ActiveObject.DisplayName)
                            : Game1.content.LoadString(
                                "Strings\\StringsFromCSFiles:Game1.cs.3160",
                                Game1.player.ActiveObject.DisplayName),
                        Game1.currentLocation.createYesNoResponses(),
                        "Eat");
                    Game1.oldKBState = currentKBState;
                    Game1.oldMouseState = Game1.input.GetMouseState();
                    Game1.oldPadState = currentPadState;

                    __result = false;
                    return false;
                }
            }
            else if (Game1.numberOfSelectedItems != -1)
            {
                Game1.tryToBuySelectedItems();
                Game1.playSound("smallSelect");
                Game1.oldKBState = currentKBState;
                Game1.oldMouseState = Game1.input.GetMouseState();
                Game1.oldPadState = currentPadState;

                __result = false;
                return false;
            }

            if (Game1.player.CurrentTool is not null && Game1.player.CurrentTool is MeleeWeapon && Game1.player.CanMove
                && !Game1.player.canOnlyWalk && !Game1.eventUp && !Game1.player.onBridge
                && Game1.didPlayerJustRightClick(true))
            {
                ((MeleeWeapon)Game1.player.CurrentTool).animateSpecialMove(Game1.player);

                __result = false;
                return false;
            }

            __result = true;
            return false;
        }

        private static bool BeforepressUseToolButton(ref bool __result)
        {
            if (Game1.fadeToBlack)
            {
                __result = false;
                return false;
            }

            Game1.player.toolPower = 0;
            Game1.player.toolHold = 0;

            bool didAttemptObjectRemoval = false;

            if (Game1.player.CurrentTool is null && Game1.player.ActiveObject is null)
            {
                Vector2 key = Game1.player.GetToolLocation() / Game1.tileSize;
                key.X = (int)key.X;
                key.Y = (int)key.Y;

                if (Game1.currentLocation.Objects.ContainsKey(key))
                {
                    SObject @object = Game1.currentLocation.Objects[key];
                    if (!@object.readyForHarvest && @object.heldObject.Value is null && !(@object is Fence)
                        && !(@object is CrabPot) && @object.type is not null
                        && (@object.type.Value == "Crafting" || @object.type.Value == "interactive")
                        && @object.name != "Twig")
                    {
                        didAttemptObjectRemoval = true;
                        @object.setHealth(@object.getHealth() - 1);
                        @object.shakeTimer = 300;
                        Game1.currentLocation.playSound("hammer");

                        if (@object.getHealth() < 2)
                        {
                            Game1.currentLocation.playSound("hammer");

                            if (@object.getHealth() < 1)
                            {
                                Tool tool = new Pickaxe();
                                tool.DoFunction(Game1.currentLocation, -1, -1, 0, Game1.player);

                                if (@object.performToolAction(tool, Game1.currentLocation))
                                {
                                    @object.performRemoveAction(@object.tileLocation.Value, Game1.currentLocation);

                                    if (@object.type.Value.Equals("Crafting") && @object.fragility.Value != 2)
                                    {
                                        Game1.currentLocation.debris.Add(
                                            new Debris(
                                                @object.bigCraftable.Value
                                                    ? -@object.ParentSheetIndex
                                                    : @object.ParentSheetIndex,
                                                Game1.player.GetToolLocation(),
                                                new Vector2(
                                                    Game1.player.GetBoundingBox().Center.X,
                                                    Game1.player.GetBoundingBox().Center.Y)));
                                    }

                                    Game1.currentLocation.Objects.Remove(key);

                                    __result = true;
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            if (Game1.currentMinigame is null && !Game1.player.UsingTool && (Game1.player.IsSitting()
                                                                             || Game1.player.isRidingHorse()
                                                                             || Game1.player.onBridge.Value
                                                                             || Game1.dialogueUp
                                                                             || (Game1.eventUp
                                                                                         && !Game1.CurrentEvent
                                                                                             .canPlayerUseTool()
                                                                                         && (!Game1.currentLocation
                                                                                                         .currentEvent
                                                                                                         .playerControlSequence
                                                                                                     || (Game1
                                                                                                                     .activeClickableMenu
                                                                                                                 is null
                                                                                                                 && Game1
                                                                                                                     .currentMinigame
                                                                                                                 is null
                                                                                                         )))
                                                                             || (Game1.player.CurrentTool is not null
                                                                                         && Game1.currentLocation
                                                                                             .doesPositionCollideWithCharacter(
                                                                                                 Utility
                                                                                                     .getRectangleCenteredAt(
                                                                                                         Game1.player
                                                                                                             .GetToolLocation(),
                                                                                                         Game1
                                                                                                             .tileSize),
                                                                                                 true) is not null
                                                                                         && Game1.currentLocation
                                                                                             .doesPositionCollideWithCharacter(
                                                                                                 Utility
                                                                                                     .getRectangleCenteredAt(
                                                                                                         Game1.player
                                                                                                             .GetToolLocation(),
                                                                                                         Game1
                                                                                                             .tileSize),
                                                                                                 true).isVillager())))
            {
                Game1.pressActionButton(
                    Game1.GetKeyboardState(),
                    Game1.input.GetMouseState(),
                    Game1.input.GetGamePadState());

                __result = false;
                return false;
            }

            if (Game1.player.canOnlyWalk)
            {
                __result = true;
                return false;
            }

            Vector2 position;
            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.X == -1
                && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.Y == -1)
            {
                position = !Game1.wasMouseVisibleThisFrame
                               ? Game1.player.GetToolLocation()
                               : new Vector2(
                                   Game1.getOldMouseX() + Game1.viewport.X,
                                   Game1.getOldMouseY() + Game1.viewport.Y);

                if (Game1.isAnyGamePadButtonBeingPressed() || Game1.isAnyGamePadButtonBeingHeld())
                {
                    position = Game1.player.ActiveObject is not null
                                   ? GamePatcher.GetPointInFacingDirection(
                                       Game1.player,
                                       Game1.player.ActiveObject.boundingBox.Width)
                                   : GamePatcher.GetPointInFacingDirection(Game1.player);
                }
            }
            else
            {
                position = !Game1.wasMouseVisibleThisFrame
                               ? Game1.player.GetToolLocation()
                               : new Vector2(
                                   ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.X,
                                   ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.Y);
            }

            if (Utility.canGrabSomethingFromHere((int)position.X, (int)position.Y, Game1.player))
            {
                Vector2 tile;
                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.X == -1
                    && ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickPoint.Y == -1)
                {
                    tile = new Vector2(
                        (Game1.getOldMouseX() + Game1.viewport.X) / Game1.tileSize,
                        (Game1.getOldMouseY() + Game1.viewport.Y) / Game1.tileSize);
                }
                else
                {
                    tile = Utility.PointToVector2(ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickedTile);
                }

                if (Game1.currentLocation.checkAction(
                    new Location((int)tile.X, (int)tile.Y),
                    Game1.viewport,
                    Game1.player))
                {
                    Game1.updateCursorTileHint();

                    __result = true;
                    return false;
                }

                if (Game1.currentLocation.terrainFeatures.ContainsKey(tile))
                {
                    Game1.currentLocation.terrainFeatures[tile].performUseAction(tile, Game1.currentLocation);

                    __result = true;
                    return false;
                }

                __result = false;
                return false;
            }

            if (Game1.currentLocation.leftClick((int)position.X, (int)position.Y, Game1.player))
            {
                __result = true;
                return false;
            }

            Game1.isCheckingNonMousePlacement = !Game1.IsPerformingMousePlacement();

            if (Game1.player.ActiveObject is not null)
            {
                if (Game1.options.allowStowing)
                {
                    if (Game1.CanPlayerStowItem(Game1.GetPlacementGrabTile()))
                    {
                        if (Game1.didPlayerJustLeftClick())
                        {
                            Game1.playSound("stoneStep");

                            Game1.player.netItemStowed.Set(true);

                            __result = true;
                            return false;
                        }

                        __result = true;
                        return false;
                    }
                }

                if (Utility.withinRadiusOfPlayer((int)position.X, (int)position.Y, 1, Game1.player)
                    && Game1.currentLocation.checkAction(
                        new Location((int)position.X / Game1.tileSize, (int)position.Y / Game1.tileSize),
                        Game1.viewport,
                        Game1.player))
                {
                    __result = true;
                    return false;
                }

                Point grabTile;
                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile != Point.Zero)
                {
                    grabTile = ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile;
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).GrabTile = Point.Zero;
                }
                else
                {
                    grabTile = Utility.Vector2ToPoint(Game1.GetPlacementGrabTile());
                }

                Vector2 nearbyValidPlacementPosition = Utility.GetNearbyValidPlacementPosition(
                    Game1.player,
                    Game1.currentLocation,
                    Game1.player.ActiveObject,
                    grabTile.X * 64,
                    grabTile.Y * 64);
                if (Utility.tryToPlaceItem(
                    Game1.currentLocation,
                    Game1.player.ActiveObject,
                    (int)nearbyValidPlacementPosition.X,
                    (int)nearbyValidPlacementPosition.Y))
                {
                    Game1.isCheckingNonMousePlacement = false;

                    __result = true;
                    return false;
                }

                Game1.isCheckingNonMousePlacement = false;
            }

            if (Game1.currentLocation.LowPriorityLeftClick((int)position.X, (int)position.Y, Game1.player))
            {
                __result = true;
                return false;
            }

            if (Game1.options.allowStowing && Game1.player.netItemStowed.Value && !didAttemptObjectRemoval)
            {
                Game1.playSound("toolSwap");
                Game1.player.netItemStowed.Set(false);

                __result = true;
                return false;
            }

            if (Game1.player.UsingTool)
            {
                Game1.player.lastClick = new Vector2((int)position.X, (int)position.Y);
                Game1.player.CurrentTool.DoFunction(
                    Game1.player.currentLocation,
                    (int)Game1.player.lastClick.X,
                    (int)Game1.player.lastClick.Y,
                    1,
                    Game1.player);

                __result = true;
                return false;
            }

            if (Game1.player.ActiveObject is null && !Game1.player.isEating && Game1.player.CurrentTool is not null)
            {
                if (Game1.player.Stamina <= 20f && Game1.player.CurrentTool is not null
                                                && !(Game1.player.CurrentTool is MeleeWeapon))
                {
                    Game1.staminaShakeTimer = 1000;
                    for (int i = 0; i < 4; i++)
                    {
                        int num = Game1.random.Next(32) + Game1.uiViewport.Width - 56;
                        int num2 = Game1.uiViewport.Height - 224 - 16 - (int)((Game1.player.MaxStamina - 270) * 0.715);
                        Game1.uiOverlayTempSprites.Add(
                            new TemporaryAnimatedSprite(
                                "LooseSprites\\Cursors",
                                new Rectangle(366, 412, 5, 6),
                                new Vector2(num, num2),
                                false,
                                0.012f,
                                Color.SkyBlue)
                                {
                                    motion = new Vector2(-2f, -10f),
                                    acceleration = new Vector2(0f, 0.5f),
                                    local = true,
                                    scale = 4 + Game1.random.Next(-1, 0),
                                    delayBeforeAnimationStart = i * 30
                                });
                    }
                }

                if (Game1.player.CurrentTool is not null || Game1.didPlayerJustLeftClick(true))
                {
                    if (Utility.withinRadiusOfPlayer((int)position.X, (int)position.Y, 1, Game1.player))
                    {
                        if (Game1.player.CurrentTool is WateringCan
                            || Math.Abs(position.X - Game1.player.getStandingX()) >= 32f
                            || Math.Abs(position.Y - Game1.player.getStandingY()) >= 32f)
                        {
                            Game1.player.Halt();
                            if ((!(Game1.player.CurrentTool is MeleeWeapon)
                                 || Game1.player.CurrentTool.InitialParentTileIndex == 47)
                                && Game1.mouseCursorTransparency != 0f && !Game1.isAnyGamePadButtonBeingHeld())
                            {
                                Game1.player.faceGeneralDirection(new Vector2((int)position.X, (int)position.Y));
                            }

                            Game1.player.lastClick = new Vector2((int)position.X, (int)position.Y);
                        }
                    }

                    Game1.player.BeginUsingTool();
                }
            }

            __result = false;
            return false;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Game1.UpdateControlInput" />.
        ///     It replicates the game's control input processing with some changes so we can
        ///     implement the path finding functionality.
        /// </summary>
        /// <param name="__instance">The <see cref="Game1" /> instance.</param>
        /// <param name="time">The time passed since the last call to <see cref="Game1.Update" />.</param>
        /// <returns>
        ///     Returns false, which terminates prefixes and skips the execution of the original method,
        ///     effectively replacing the original method.
        /// </returns>
        /*private static bool BeforeUpdateControlInput(
            Game1 __instance,
            GameTime time,
            int ____activatedTick,
            ref IInputSimulator ___inputSimulator,
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
                bool mouseMoved = false;
                if (Math.Abs(currentPadState.ThumbSticks.Right.X) > 0
                    || Math.Abs(currentPadState.ThumbSticks.Right.Y) > 0)
                {
                    Game1.setMousePositionRaw(
                        (int)(currentMouseState.X + currentPadState.ThumbSticks.Right.X
                              * GamePatcher.thumbstickToMouseModifier.GetValue()),
                        (int)(currentMouseState.Y - currentPadState.ThumbSticks.Right.Y
                              * GamePatcher.thumbstickToMouseModifier.GetValue()));
                    mouseMoved = true;
                }

                if (Game1.IsChatting)
                {
                    mouseMoved = true;
                }

                if (((Game1.getMouseX() != Game1.getOldMouseX() || Game1.getMouseY() != Game1.getOldMouseY())
                     && Game1.getMouseX() != 0 && Game1.getMouseY() != 0) || mouseMoved)
                {
                    if (mouseMoved)
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

            bool actionButtonPressed = false;
            bool switchToolButtonPressed = false;
            bool useToolButtonPressed = false;
            bool useToolButtonReleased = false;
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
            bool useToolButtonHeld = false;

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

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.useToolButton)
                && Game1.areAllOfTheseKeysUp(Game1.oldKBState, Game1.options.useToolButton))
            {
                useToolButtonPressed = true;
            }

            if (Game1.isOneOfTheseKeysDown(currentKbState, Game1.options.useToolButton)
                || currentMouseState.LeftButton == ButtonState.Pressed)
            {
                useToolButtonHeld = true;
            }

            if (Game1.areAllOfTheseKeysUp(currentKbState, Game1.options.useToolButton)
                && Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton))
            {
                useToolButtonReleased = true;
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
                }

                if (currentPadState.IsButtonDown(Buttons.A))
                {
                    Game1.rightClickPolling -= time.ElapsedGameTime.Milliseconds;
                    if (Game1.rightClickPolling <= 0)
                    {
                        Game1.rightClickPolling = 100;
                        actionButtonPressed = true;
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

                if (__instance.controllerSlingshotSafeTime > 0f)
                {
                    if (!currentPadState.IsButtonDown(Buttons.DPadUp) && !currentPadState.IsButtonDown(Buttons.DPadDown)
                                                                      && !currentPadState.IsButtonDown(Buttons.DPadLeft)
                                                                      && !currentPadState.IsButtonDown(
                                                                          Buttons.DPadRight)
                                                                      && Math.Abs(currentPadState.ThumbSticks.Left.X)
                                                                      < 0.04 && Math.Abs(
                                                                          currentPadState.ThumbSticks.Left.Y) < 0.04)
                    {
                        __instance.controllerSlingshotSafeTime = 0f;
                    }

                    if (__instance.controllerSlingshotSafeTime <= 0f)
                    {
                        __instance.controllerSlingshotSafeTime = 0f;
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

            ClickState clickState = ClickState.None;

            if (currentMouseState.LeftButton == ButtonState.Pressed)
            {
                clickState = Game1.oldMouseState.LeftButton == ButtonState.Released ? ClickState.ClickDown : ClickState.ClickHeld;
            }
            else if (Game1.oldMouseState.LeftButton == ButtonState.Pressed)
            {
                clickState = ClickState.ClickReleased;
            }

            ClickToMoveManager.GetOrCreate(Game1.currentLocation).Update();

            if (moveUpPressed || moveDownPressed || moveLeftPressed || moveRightPressed || moveUpReleased
                || moveDownReleased || moveLeftReleased || moveRightReleased || moveUpHeld || moveDownHeld
                || moveLeftHeld || moveRightHeld)
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();

                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed = moveUpPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed = moveDownPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftPressed = moveLeftPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightPressed =
                    moveRightPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpReleased = moveUpReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownReleased =
                    moveDownReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftReleased =
                    moveLeftReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightReleased =
                    moveRightReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld = moveUpHeld;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld = moveDownHeld;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld = moveLeftHeld;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld = moveRightHeld;
            }
            else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).Moving)
            {
                moveUpPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed;
                moveDownPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed;
                moveLeftPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftPressed;
                moveRightPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates
                    .MoveRightPressed;
                moveUpReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpReleased;
                moveDownReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates
                    .MoveDownReleased;
                moveLeftReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates
                    .MoveLeftReleased;
                moveRightReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates
                    .MoveRightReleased;
                moveUpHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld;
                moveDownHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld;
                moveLeftHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld;
                moveRightHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.ActionButtonPressed)
            {
                actionButtonPressed = true;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.UseToolButtonPressed)
            {
                useToolButtonPressed = true;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.UseToolButtonHeld)
            {
                useToolButtonHeld = true;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.UseToolButtonReleased)
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
                    ClickToMoveManager.Reflection.GetMethod(__instance, "updatePanModeControls");
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
                Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                Game1.oldKBState = currentKbState;
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

                            Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                            Game1.oldKBState = currentKbState;
                            Game1.oldPadState = currentPadState;

                            return false;
                        }

                        if (menu == Game1.chatBox && Game1.options.gamepadControls && Game1.IsChatting)
                        {
                            Game1.PopUIMode();

                            Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                            Game1.oldKBState = currentKbState;
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
                case ClickState.ClickDown:
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).OnClick(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                    break;
                case ClickState.ClickHeld:
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).OnClickHeld(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                    break;
                case ClickState.ClickReleased:
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).OnClickRelease(
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

                Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
                Game1.oldKBState = currentKbState;
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

                    if (!Game1.pressUseToolButton() && Game1.player.canReleaseTool && Game1.player.UsingTool)
                    {
                        _ = Game1.player.CurrentTool;
                    }

                    Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();

                    if (Game1.mouseClickPolling < 100)
                    {
                        Game1.oldKBState = currentKbState;
                    }

                    Game1.oldPadState = currentPadState;

                    return false;
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
                && Game1.player.CurrentTool is not  FishingRod)
            {
                int enchantedLevel = Game1.player.CurrentTool.hasEnchantmentOfType<ReachingToolEnchantment>() ? 1 : 0;

                if (Game1.player.toolHold <= 0 && Game1.player.CurrentTool.upgradeLevel.Value + enchantedLevel
                    > Game1.player.toolPower)
                {
                    Game1.player.toolHold = (int)(600 * Game1.player.CurrentTool.AnimationSpeedModifier);
                }
                else if (Game1.player.CurrentTool.upgradeLevel.Value + enchantedLevel > Game1.player.toolPower)
                {
                    Game1.player.toolHold -= time.ElapsedGameTime.Milliseconds;
                    if (Game1.player.toolHold <= 0)
                    {
                        Game1.player.toolPowerIncrease();
                    }
                }
            }

            if (Game1.upPolling >= 650f)
            {
                Game1.upPolling -= 100f;
            }
            else if (Game1.downPolling >= 650f)
            {
                Game1.downPolling -= 100f;
            }
            else if (Game1.rightPolling >= 650f)
            {
                Game1.rightPolling -= 100f;
            }
            else if (Game1.leftPolling >= 650f)
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

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld)
                    {
                        Game1.player.setMoving(1);
                    }

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld)
                    {
                        Game1.player.setMoving(2);
                    }

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld)
                    {
                        Game1.player.setMoving(4);
                    }

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld)
                    {
                        Game1.player.setMoving(8);
                    }

                    if (initialCount == 0 && Game1.player.movementDirections.Count > 0 && Game1.player.running)
                    {
                        //Game1.player.FarmerSprite.SetNextOffset(1);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Up.Value) && !ClickToMoveManager
                            .GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld))
                {
                    Game1.player.setMoving(33);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(64);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Right.Value) && !ClickToMoveManager
                            .GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld))
                {
                    Game1.player.setMoving(34);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(64);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Down.Value) && !ClickToMoveManager
                            .GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld))
                {
                    Game1.player.setMoving(36);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(64);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Left.Value) && !ClickToMoveManager
                            .GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld))
                {
                    Game1.player.setMoving(40);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(64);
                    }
                }

                if ((!ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld
                     && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld
                     && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld
                     && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld
                     && !Game1.player.UsingTool) || Game1.activeClickableMenu is not null)
                {
                    Game1.player.Halt();
                }
            }
            else if (Game1.isQuestion)
            {
                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed)
                {
                    Game1.currentQuestionChoice = Math.Max(Game1.currentQuestionChoice - 1, 0);

                    Game1.playSound("toolSwap");
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed)
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

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightPressed)
                {
                    Game1.numberOfSelectedItems = Math.Min(Game1.numberOfSelectedItems + 1, val);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftPressed)
                {
                    Game1.numberOfSelectedItems = Math.Max(Game1.numberOfSelectedItems - 1, 0);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed)
                {
                    Game1.numberOfSelectedItems = Math.Min(Game1.numberOfSelectedItems + 10, val);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed)
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
                    Game1.NewDay(0f);
                }

                if (currentKbState.IsKeyDown(Keys.M) && !Game1.oldKBState.IsKeyDown(Keys.M))
                {
                    Game1.dayOfMonth = 28;
                    Game1.NewDay(0f);
                }

                if (currentKbState.IsKeyDown(Keys.T) && !Game1.oldKBState.IsKeyDown(Keys.T))
                {
                    GamePatcher.addHour.Invoke();
                }

                if (currentKbState.IsKeyDown(Keys.Y) && !Game1.oldKBState.IsKeyDown(Keys.Y))
                {
                    GamePatcher.addMinute.Invoke();
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
                    Game1.player.changeHat(Game1.random.Next(48));
                }

                if (currentKbState.IsKeyDown(Keys.I) && !Game1.oldKBState.IsKeyDown(Keys.I))
                {
                    Game1.player.changeHairStyle(Game1.random.Next(32));
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

                    Game1.player.changeHairStyle(Game1.random.Next(32));

                    if (Game1.random.NextDouble() < 0.5)
                    {
                        Game1.player.changeHat(Game1.random.Next(-1, 48));
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
                    (Game1.getLocationFromName("FarmHouse") as FarmHouse).setWallpaper(
                        Game1.random.Next(112),
                        -1,
                        true);
                    (Game1.getLocationFromName("FarmHouse") as FarmHouse).setFloor(Game1.random.Next(40), -1, true);
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
                                          gamepadMode = Game1.options.gamepadControls
                                                        && Game1.rightStickHoldTime >= Game1.emoteMenuShowTime
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

            Game1.oldMouseState = currentMouseState;//Game1.input.GetMouseState();
            Game1.oldKBState = currentKbState;
            Game1.oldPadState = currentPadState;

            return false;
        }*/
        private static bool BeforeUpdateControlInput(
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
                    float thumbstickToMouseModifier = GamePatcher.thumbstickToMouseModifier.GetValue();
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
                }

                if (currentPadState.IsButtonDown(Buttons.A))
                {
                    Game1.rightClickPolling -= time.ElapsedGameTime.Milliseconds;
                    if (Game1.rightClickPolling <= 0)
                    {
                        Game1.rightClickPolling = 100;
                        actionButtonPressed = true;
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

            bool mouseLeftButtonDown = ClickToMoveManager.Helper.Input.IsDown(SButton.MouseLeft) || ClickToMoveManager.Helper.Input.IsSuppressed(SButton.MouseLeft);

            if (mouseLeftButtonDown)
            {
                clickState = GamePatcher.lastMouseLeftButtonDown ? SButtonState.Held : SButtonState.Pressed;
            }
            else if (GamePatcher.lastMouseLeftButtonDown)
            {
                clickState = SButtonState.Released;
            }

            GamePatcher.lastMouseLeftButtonDown = mouseLeftButtonDown;

            ClickToMoveManager.GetOrCreate(Game1.currentLocation).Update();

            if (moveUpPressed || moveDownPressed || moveLeftPressed || moveRightPressed || moveUpReleased
                || moveDownReleased || moveLeftReleased || moveRightReleased || moveUpHeld || moveDownHeld
                || moveLeftHeld || moveRightHeld)
            {
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();

                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed = moveUpPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed = moveDownPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftPressed = moveLeftPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightPressed = moveRightPressed;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpReleased = moveUpReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownReleased = moveDownReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftReleased = moveLeftReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightReleased = moveRightReleased;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld = moveUpHeld;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld = moveDownHeld;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld = moveLeftHeld;
                ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld = moveRightHeld;
            }
            else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).Moving)
            {
                moveUpPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed;
                moveDownPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed;
                moveLeftPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftPressed;
                moveRightPressed = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightPressed;
                moveUpReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpReleased;
                moveDownReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownReleased;
                moveLeftReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftReleased;
                moveRightReleased = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightReleased;
                moveUpHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld;
                moveDownHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld;
                moveLeftHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld;
                moveRightHeld = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.ActionButtonPressed)
            {
                actionButtonPressed = true;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.UseToolButtonPressed)
            {
                useToolButtonPressed = true;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.UseToolButtonHeld)
            {
                useToolButtonHeld = true;
            }

            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.UseToolButtonReleased)
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
                    ClickToMoveManager.Helper.Reflection.GetMethod(__instance, "updatePanModeControls");
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
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).OnClick(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                    break;
                case SButtonState.Held:
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).OnClickHeld(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                    break;
                case SButtonState.Released:
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).OnClickRelease(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);

                    ClickToMoveManager.OnScreenMenuClicked = false;
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

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld)
                    {
                        Game1.player.setMoving(Farmer.up);
                    }

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld)
                    {
                        Game1.player.setMoving(Farmer.right);
                    }

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld)
                    {
                        Game1.player.setMoving(Farmer.down);
                    }

                    if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld)
                    {
                        Game1.player.setMoving(Farmer.left);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Up.Value) && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.up);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Right.Value) && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.right);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Down.Value) && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.down);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftReleased
                    || (Game1.player.movementDirections.Contains(WalkDirection.Left.Value) && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld))
                {
                    Game1.player.setMoving(Farmer.release + Farmer.left);

                    if (Game1.player.movementDirections.Count == 0)
                    {
                        Game1.player.setMoving(Farmer.halt);
                    }
                }

                if ((!ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpHeld
                     && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightHeld
                     && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownHeld
                     && !ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftHeld
                     && !Game1.player.UsingTool) || Game1.activeClickableMenu is not null)
                {
                    Game1.player.Halt();
                }
            }
            else if (Game1.isQuestion)
            {
                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed)
                {
                    Game1.currentQuestionChoice = Math.Max(Game1.currentQuestionChoice - 1, 0);

                    Game1.playSound("toolSwap");
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed)
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

                if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveRightPressed)
                {
                    Game1.numberOfSelectedItems = Math.Min(Game1.numberOfSelectedItems + 1, val);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveLeftPressed)
                {
                    Game1.numberOfSelectedItems = Math.Max(Game1.numberOfSelectedItems - 1, 0);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveUpPressed)
                {
                    Game1.numberOfSelectedItems = Math.Min(Game1.numberOfSelectedItems + 10, val);
                    Game1.playItemNumberSelectSound();
                }
                else if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates.MoveDownPressed)
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
                    GamePatcher.addHour.Invoke();
                }

                if (currentKbState.IsKeyDown(Keys.Y) && !Game1.oldKBState.IsKeyDown(Keys.Y))
                {
                    GamePatcher.addMinute.Invoke();
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
                    ClickToMoveManager.OnScreenMenuClicked = true;

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

        private static bool BeforeWarpFarmer()
        {
            ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();

            return true;
        }

        private static void FishingGameReceiveClickToMoveKeyStates(FishingGame fishingGame, GameLocation location)
        {
            ClickToMoveKeyStates clickKeyStates = ClickToMoveManager.GetOrCreate(location).ClickKeyStates;

            if (clickKeyStates.MoveUpReleased)
            {
                Game1.player.setMoving(33);
            }

            if (clickKeyStates.MoveDownReleased)
            {
                Game1.player.setMoving(36);
            }

            if (clickKeyStates.MoveLeftReleased)
            {
                Game1.player.setMoving(40);
            }

            if (clickKeyStates.MoveRightReleased)
            {
                Game1.player.setMoving(34);
            }

            int timerToStart = ClickToMoveManager.Reflection.GetField<int>(fishingGame, "timerToStart").GetValue();

            if (!fishingGame.gameDone && !Game1.player.UsingTool && timerToStart <= 0)
            {
                if ((clickKeyStates.MoveUpPressed && !clickKeyStates.MoveUpReleased) || clickKeyStates.MoveUpHeld)
                {
                    Game1.player.setMoving(1);
                }
                else if ((clickKeyStates.MoveDownPressed && !clickKeyStates.MoveDownReleased)
                         || clickKeyStates.MoveDownHeld)
                {
                    Game1.player.setMoving(4);
                }

                if ((clickKeyStates.MoveLeftPressed && !clickKeyStates.MoveLeftReleased) || clickKeyStates.MoveLeftHeld)
                {
                    Game1.player.setMoving(8);
                }
                else if ((clickKeyStates.MoveRightPressed && !clickKeyStates.MoveRightReleased)
                         || clickKeyStates.MoveRightHeld)
                {
                    Game1.player.setMoving(2);
                }
            }

            if (MinigamesPatcher.LeftClickNextUpdateFishingGame)
            {
                fishingGame.receiveLeftClick(0, 0);
                MinigamesPatcher.LeftClickNextUpdateFishingGame = false;
            }

            if (clickKeyStates.UseToolButtonPressed)
            {
                MinigamesPatcher.LeftClickNextUpdateFishingGame = true;
            }

            if (clickKeyStates.UseToolButtonReleased)
            {
                fishingGame.releaseLeftClick(
                    ClickToMoveManager.GetOrCreate(location).ClickPoint.X,
                    ClickToMoveManager.GetOrCreate(location).ClickPoint.Y);
            }

            if (clickKeyStates.ActionButtonPressed)
            {
                GamePatcher.OnRightClickFishingGame(fishingGame, timerToStart);
            }
        }

        private static Vector2 GetPointInFacingDirection(Farmer player, int offset = Game1.tileSize)
        {
            Rectangle boundingBox = player.GetBoundingBox();

            switch (player.FacingDirection)
            {
                case 0:
                    return new Vector2(boundingBox.X + boundingBox.Width / 2, boundingBox.Y - Game1.tileSize);
                case 2:
                    return new Vector2(
                        boundingBox.X + boundingBox.Width / 2,
                        boundingBox.Y + boundingBox.Height + Game1.tileSize);
                case 3:
                    return new Vector2(boundingBox.X - offset, boundingBox.Y + boundingBox.Height / 2);
                case 1:
                    return new Vector2(
                        boundingBox.X + boundingBox.Width + Game1.tileSize,
                        boundingBox.Y + boundingBox.Height / 2);
                default:
                    return new Vector2(player.getStandingX(), player.getStandingY());
            }
        }

        private static void OnRightClickFishingGame(FishingGame fishingGame, int timerToStart)
        {
            if (Game1.isAnyGamePadButtonBeingPressed())
            {
                IReflectedField<int> showResultsTimerField =
                    ClickToMoveManager.Reflection.GetField<int>(fishingGame, "showResultsTimer");
                int showResultsTimer = showResultsTimerField.GetValue();

                FishingRod fishingRod = Game1.player.CurrentTool as FishingRod;

                if (timerToStart <= 0 && showResultsTimer < 0 && !fishingGame.gameDone
                    && Game1.activeClickableMenu is null && !fishingRod.hit && !fishingRod.pullingOutOfWater
                    && !fishingRod.isCasting && !fishingRod.fishCaught)
                {
                    Game1.player.lastClick = Vector2.Zero;
                    Game1.player.Halt();
                    Game1.pressUseToolButton();
                }
                else if (showResultsTimer > 11000)
                {
                    showResultsTimerField.SetValue(11001);
                }
                else if (showResultsTimer > 9000)
                {
                    showResultsTimerField.SetValue(9001);
                }
                else if (showResultsTimer > 7000)
                {
                    showResultsTimerField.SetValue(7001);
                }
                else if (showResultsTimer > 5000)
                {
                    showResultsTimerField.SetValue(5001);
                }
                else if (showResultsTimer < 5000 && showResultsTimer > 1000)
                {
                    showResultsTimerField.SetValue(1500);
                    Game1.playSound("smallSelect");
                }
            }
        }

        private static void TargetGameReceiveMobileKeyStates(TargetGame targetGame, GameLocation location)
        {
            ClickToMoveKeyStates clickKeyStates = ClickToMoveManager.GetOrCreate(location).ClickKeyStates;

            if (ClickToMoveManager.Reflection.GetField<int>(targetGame, "showResultsTimer").GetValue() > 0
                || ClickToMoveManager.Reflection.GetField<int>(targetGame, "gameEndTimer").GetValue() < 0)
            {
                Game1.player.Halt();
                return;
            }

            if (Game1.player.movementDirections.Count < 2 && !Game1.player.UsingTool
                                                          && ClickToMoveManager.Reflection.GetField<int>(
                                                              targetGame,
                                                              "timerToStart").GetValue() <= 0)
            {
                if ((clickKeyStates.MoveUpPressed && !clickKeyStates.MoveUpReleased) || clickKeyStates.MoveUpHeld)
                {
                    Game1.player.setMoving(1);
                }
                else if ((clickKeyStates.MoveDownPressed && !clickKeyStates.MoveDownReleased)
                         || clickKeyStates.MoveDownHeld)
                {
                    Game1.player.setMoving(4);
                }

                if ((clickKeyStates.MoveLeftPressed && !clickKeyStates.MoveLeftReleased) || clickKeyStates.MoveLeftHeld)
                {
                    Game1.player.setMoving(8);
                }
                else if ((clickKeyStates.MoveRightPressed && !clickKeyStates.MoveRightReleased)
                         || clickKeyStates.MoveRightHeld)
                {
                    Game1.player.setMoving(2);
                }
            }

            if (clickKeyStates.MoveUpReleased)
            {
                Game1.player.setMoving(33);
            }

            if (clickKeyStates.MoveDownReleased)
            {
                Game1.player.setMoving(36);
            }

            if (clickKeyStates.MoveLeftReleased)
            {
                Game1.player.setMoving(40);
            }

            if (clickKeyStates.MoveRightReleased)
            {
                Game1.player.setMoving(34);
            }
        }

        /// <summary>A method called via Harmony to modify <see cref="Game1._update" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileUpdate(IEnumerable<CodeInstruction> instructions)
        {
            // Add a call to UpdateClickToMove method after the first call to GetMouseState.

            // Relevant CIL code:
            //    mouseState = Game1.input.GetMouseState();
            //        IL_06e1: ldsfld class StardewValley.InputState StardewValley.Game1::input
            //        IL_06e6: callvirt instance valuetype[Microsoft.Xna.Framework] Microsoft.Xna.Framework.Input.MouseState StardewValley.InputState::GetMouseState()
            //        IL_06eb: stloc.s 13
            //
            // Code to include after the variable mouseState is defined:
            //    GamePatcher.UpdateClickToMove(mouseState);
            //        IL_0937: ldloc.s 13
            //        IL_0939: call void Raquellcesar.Stardew.ClickToMove.Framework.GamePatcher::UpdateClickToMove(valuetype [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Input.MouseState)

            MethodInfo updateClickToMove = AccessTools.Method(
                typeof(GamePatcher),
                nameof(GamePatcher.UpdateClickToMove));

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

                    yield return new CodeInstruction(OpCodes.Ldloc_S, mouseStateLocIndex);
                    yield return new CodeInstruction(OpCodes.Call, updateClickToMove);

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
                        $"Failed to patch {nameof(Game1)}._update.\nThe point of injection was not found.",
                        LogLevel.Error);
            }
        }

        /// <summary>A method called via Harmony to modify the setter for <see cref="Game1.currentMinigame" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileSetCurrentMinigame(
            IEnumerable<CodeInstruction> instructions)
        {
            // Reset the ClickToMove object associated with the current game location
            // after the game checks that the current location is not null.

            // Relevant CIL code:
            //     if (Game1.currentLocation is not null)
            //         IL_0009: call class StardewValley.GameLocation StardewValley.Game1::get_currentLocation()
            //         IL_000e: brfalse.s IL_0024
            //
            // Code to include:
            //     ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();

            MethodInfo getCurrentLocation =
                AccessTools.Property(typeof(Game1), nameof(Game1.currentLocation)).GetGetMethod();
            MethodInfo getOrCreate = AccessTools.Method(
                typeof(ClickToMoveManager),
                nameof(ClickToMoveManager.GetOrCreate));
            MethodInfo reset = AccessTools.Method(typeof(ClickToMove), nameof(ClickToMove.Reset));

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

                    yield return new CodeInstruction(OpCodes.Call, getCurrentLocation);
                    yield return new CodeInstruction(OpCodes.Call, getOrCreate);
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
                ClickToMoveManager.Monitor.Log(
                        $"Failed to patch the setter for {nameof(Game1)}.{nameof(Game1.currentMinigame)}.\nThe point of injection was not found.",
                        LogLevel.Error);
            }
        }
    }
}