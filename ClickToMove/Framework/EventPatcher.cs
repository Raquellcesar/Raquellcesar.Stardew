// -----------------------------------------------------------------------
// <copyright file="EventPatcher.cs" company="Raquellcesar">
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

    using Microsoft.Xna.Framework;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Menus;

    /// <summary>
    ///     Applies Harmony patches to the <see cref="Event"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class EventPatcher
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
                AccessTools.Method(typeof(Event), "addSpecificTemporarySprite"),
                transpiler: new HarmonyMethod(
                    typeof(EventPatcher),
                    nameof(EventPatcher.TranspileAddSpecificTemporarySprite)));

            harmony.Patch(
                AccessTools.Method(typeof(Event), nameof(Event.checkAction)),
                transpiler: new HarmonyMethod(typeof(EventPatcher), nameof(EventPatcher.TranspileCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Event), nameof(Event.checkForCollision)),
                transpiler: new HarmonyMethod(typeof(EventPatcher), nameof(EventPatcher.TranspileCheckForCollision)));
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Event.addSpecificTemporarySprite"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        /// <param name="ilGenerator">Generates MSIL instructions.</param>
        private static IEnumerable<CodeInstruction> TranspileAddSpecificTemporarySprite(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator ilGenerator)
        {
            // Check if the farmer's CurrentToolIndex is -1.

            /*
            * Relevant CIL code:
            *     this.drawTool = true;
            *         IL_7411: ldarg.0
            *         IL_7412: ldc.i4.1
            *         IL_7413: stfld bool StardewValley.Event::drawTool
            *
            * Code to include after:
            *     if (this.farmer.CurrentToolIndex == -1)
            *     {
            *         this.farmer.CurrentToolIndex = 0;
            *     }
            */

            MethodInfo getFarmer = AccessTools.Property(typeof(Event), nameof(Event.farmer)).GetGetMethod();
            MethodInfo getCurrentToolIndex =
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentToolIndex)).GetGetMethod();
            MethodInfo setCurrentToolIndex =
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentToolIndex)).GetSetMethod();

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Stfld
                           && codeInstructions[i].operand is FieldInfo { Name: "drawTool" }
                           && codeInstructions[i - 1].opcode == OpCodes.Ldc_I4_1
                           && i + 1 < codeInstructions.Count)
                {
                    Label jumpIfNotEqual = ilGenerator.DefineLabel();

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

                    i++;
                    codeInstructions[i].labels.Add(jumpIfNotEqual);
                    yield return codeInstructions[i];

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
                    $"Failed to patch {nameof(Event)}.addSpecificTemporarySprite.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Event.checkAction"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileCheckAction(IEnumerable<CodeInstruction> instructions)
        {
            /*
            * Reset the ClickToMove object associated with the current game location
            * for the case "LuauSoup" if specialEventVariable2 is not defined.

            * Relevant CIL code:
            *     if (!this.specialEventVariable2)
            *         IL_0e41: ldarg.0
            *         IL_0e42: ldfld bool StardewValley.Event::specialEventVariable2
            *         IL_0e47: brtrue.s IL_0e87
            *
            * Code to include after:
            *     ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
            */

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
                if (!found && codeInstructions[i].opcode == OpCodes.Ldfld && codeInstructions[i].operand is FieldInfo { Name: "specialEventVariable2" }
                && i + 1 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Brtrue)
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
                    $"Failed to patch {nameof(Event)}.{nameof(Event.checkAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /*private static bool BeforeCheckForCollision(Event __instance, Rectangle position, Farmer who, GameLocation ___temporaryLocation)
        {
            foreach (NPC actor in __instance.actors)
            {
                if (actor.GetBoundingBox().Intersects(position) && !__instance.farmer.temporarilyInvincible && __instance.farmer.TemporaryPassableTiles.IsEmpty() && !actor.IsInvisible && !who.GetBoundingBox().Intersects(actor.GetBoundingBox()) && !actor.farmerPassesThrough)
                {
                    return true;
                }
            }

            int num = 32;
            int num2 = (this.isFestival ? (Math.Max(num, Game1.xEdge) + num) : num);
            int num3 = Game1.currentLocation.map.Layers[0].DisplayWidth - num;
            int num4 = num;
            int num5 = Game1.currentLocation.map.Layers[0].DisplayHeight - num;
            if ((Game1.currentMinigame == null || !(Game1.currentMinigame is FishingGame)) && (((position.X < num2 || position.X >= num3 || position.Y < num4 || position.Y >= num5) && who.IsLocalPlayer && this.isFestival) || position.X < 0 || position.Y < 0 || position.X >= Game1.currentLocation.map.Layers[0].DisplayWidth || position.Y >= Game1.currentLocation.map.Layers[0].DisplayHeight))
            {
                if (who.IsLocalPlayer && this.isFestival)
                {
                    who.Halt();
                    who.Position = who.lastPosition;

                    Game1.currentLocation.tapToMove.Reset();

                    if (!Game1.IsMultiplayer && Game1.activeClickableMenu == null)
                    {
                        Game1.activeClickableMenu = new ConfirmationDialog(Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1758", this.FestivalName), forceEndFestival);
                    }
                    else if (Game1.activeClickableMenu == null)
                    {
                        Game1.player.team.SetLocalReady("festivalEnd", ready: true);
                        Game1.activeClickableMenu = new ReadyCheckDialog("festivalEnd", allowCancel: true, forceEndFestival);
                    }
                }

                return true;
            }

            foreach (Object prop in __instance.props)
            {
                if (prop.getBoundingBox(prop.tileLocation).Intersects(position))
                {
                    return true;
                }
            }

            if (___temporaryLocation is not null)
            {
                foreach (Object value in ___temporaryLocation.objects.Values)
                {
                    if (value.getBoundingBox(value.tileLocation).Intersects(position))
                    {
                        return true;
                    }
                }
            }

            foreach (Prop festivalProp in __instance.festivalProps)
            {
                if (festivalProp.isColliding(position))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ForceEndFestival(this Event @event, Farmer who)
        {
            Game1.currentMinigame = null;
            Game1.exitActiveMenu();

            Game1.player.Halt();
            @event.endBehaviors(null, Game1.currentLocation);

            if (Game1.IsServer)
            {
                Game1.multiplayer.sendServerToClientsMessage("endFest");
            }

            Game1.changeMusicTrack("none");
        }*/

        /// <summary>
        ///     A method called via Harmony to modify <see cref="Event.checkForCollision"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileCheckForCollision(
            IEnumerable<CodeInstruction> instructions)
        {
            // Reset the ClickToMove object associated with the current game location after the test
            // for isFestival.

            /*
            * Relevant CIL code:
            *     if (who.IsLocalPlayer && this.isFestival)
            *         IL_00e5: ldarg.2
            *         IL_00e6: callvirt instance bool StardewValley.Farmer::get_IsLocalPlayer()
            *         IL_00eb: brfalse IL_0182
            *         IL_00f0: ldarg.0
            *         IL_00f1: ldfld bool StardewValley.Event::isFestival
            *         IL_00f6: brfalse IL_0182
            *
            * Code to include after:
            *     ClickToMoveManager.GetOrCreate(Game1.currentLocation).Reset();
            */

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
                if (!found && codeInstructions[i].opcode == OpCodes.Ldfld && codeInstructions[i].operand is FieldInfo { Name: "isFestival" }
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
                        $"Failed to patch {nameof(Event)}.{nameof(Event.checkForCollision)}.\nThe point of injection was not found.",
                        LogLevel.Error);
            }
        }
    }
}
