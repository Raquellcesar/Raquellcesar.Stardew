// -----------------------------------------------------------------------
// <copyright file="CharactersPatcher.cs" company="Raquellcesar">
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
    using System.Runtime.CompilerServices;

    using Harmony;

    using Microsoft.Xna.Framework;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Characters;
    using StardewValley.Menus;
    using StardewValley.Objects;

    internal static class CharactersPatcher
    {
        /// <summary>
        ///     Associates new properties to <see cref="Horse" /> objects at runtime.
        /// </summary>
        private static readonly ConditionalWeakTable<Horse, HorseData> HorsesData =
            new ConditionalWeakTable<Horse, HorseData>();

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">
        ///     The Harmony patching API.
        /// </param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(Child), nameof(Child.checkAction)),
                transpiler: new HarmonyMethod(typeof(CharactersPatcher), nameof(CharactersPatcher.TranspileChildCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Horse), nameof(Horse.checkAction)),
                new HarmonyMethod(typeof(CharactersPatcher), nameof(CharactersPatcher.BeforeHorseCheckAction)));
        }

        /// <summary>
        ///     Gets if an horse should allow action checking.
        /// </summary>
        /// <param name="horse">The <see cref="Horse" /> instance.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this horse can check for action.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsCheckActionEnabled(this Horse horse)
        {
            return horse is not null && CharactersPatcher.HorsesData.GetOrCreateValue(horse).CheckActionEnabled;
        }

        /// <summary>
        ///     Sets whether an horse allows action checking.
        /// </summary>
        /// <param name="horse">The <see cref="Horse" /> instance.</param>
        /// <param name="value">Determines whether the horse can check action.</param>
        public static void SetCheckActionEnabled(this Horse horse, bool value)
        {
            if (horse is not null)
            {
                CharactersPatcher.HorsesData.GetOrCreateValue(horse).CheckActionEnabled = value;
            }
        }

        /// <summary>A method called via Harmony before <see cref="Horse.checkAction" />.</summary>
        /// <returns>
        ///     Returns <see langword="false"/>, terminating prefixes and skipping the execution of the original method,
        ///     effectively replacing the original method.
        /// </returns>
        private static bool BeforeHorseCheckAction(
            Horse __instance,
            Farmer who,
            GameLocation l,
            ref bool ___roomForHorseAtDismountTile,
            ref Vector2 ___dismountTile,
            ref bool __result)
        {
            HorseData horseData = CharactersPatcher.HorsesData.GetOrCreateValue(__instance);
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
                            else if (!ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse)
                            {
                                __instance.rider = who;
                                __instance.rider.freezePause = 5000;
                                __instance.rider.synchronizedJump(6);
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
                (position.X * Game1.tileSize) + (Game1.tileSize / 2) - (__instance.GetBoundingBox().Width / 2),
                (position.Y * Game1.tileSize) + 4);

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

            if (!position.Equals(Vector2.Zero) && Vector2.Distance(position, __instance.rider.getTileLocation()) < 2)
            {
                __instance.rider.synchronizedJump(6);
                l.playSound("dwop");
                __instance.rider.freezePause = 5000;
                __instance.rider.Halt();
                __instance.rider.xOffset = 0;
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

        /// <summary>A method called via Harmony to modify <see cref="Child.checkAction" />.</summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileChildCheckAction(
            IEnumerable<CodeInstruction> instructions)
        {
            // Check if CurrentToolIndex is greater than zero before accessing items.

            /*
             * Relevant CIL code:
             *      if (base.Age >= 3 && who.items.Count > who.CurrentToolIndex && who.items[who.CurrentToolIndex] != null && who.Items[who.CurrentToolIndex] is Hat)
             *          IL_0079: ldarg.0
             *          IL_007a: call instance int32 StardewValley.NPC::get_Age()
             *          IL_007f: ldc.i4.3
             *          IL_0080: blt IL_014f
             *          ...
             *
             * Replace with:
             *      if (base.Age >= 3 && who.CurrentToolIndex >= 0 && who.items.Count > who.CurrentToolIndex && who.items[who.CurrentToolIndex] != null && who.Items[who.CurrentToolIndex] is Hat)
             */

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
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(Child)}.{nameof(Child.checkAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     Keeps data about an <see cref="Horse" /> object.
        /// </summary>
        internal class HorseData
        {
            public bool CheckActionEnabled = true;
        }
    }
}
