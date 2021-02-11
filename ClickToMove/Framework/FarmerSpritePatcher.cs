// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="FarmerSpritePatcher.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of __instance source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

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
    using StardewValley.Tools;

    internal static class FarmerSpritePatcher
    {
        /// <summary>
        ///     Associates new properties to <see cref="FarmerSprite" /> objects at runtime.
        /// </summary>
        private static readonly ConditionalWeakTable<FarmerSprite, FarmerSpriteData> FarmerSpritesData =
            new ConditionalWeakTable<FarmerSprite, FarmerSpriteData>();

        /// <summary>
        ///     Gets a <see cref="FarmerSprite" /> next frame offset.
        /// </summary>
        /// <param name="farmerSprite">The <see cref="FarmerSprite" /> instance.</param>
        /// <returns></returns>
        public static int GetNextOffset(this FarmerSprite farmerSprite)
        {
            return FarmerSpritePatcher.FarmerSpritesData.GetOrCreateValue(farmerSprite).NextOffset;
        }

        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(FarmerSprite), nameof(FarmerSprite.animateOnce), new[] { typeof(GameTime) }),
                transpiler: new HarmonyMethod(
                    typeof(FarmerSpritePatcher),
                    nameof(FarmerSpritePatcher.TranspileAnimateOnce)));

            harmony.Patch(
                AccessTools.Method(
                    typeof(FarmerSprite),
                    nameof(FarmerSprite.setCurrentFrame),
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool) }),
                new HarmonyMethod(typeof(FarmerSpritePatcher), nameof(FarmerSpritePatcher.BeforeSetCurrentFrame)));

            harmony.Patch(
                AccessTools.Method(typeof(Tool), nameof(Tool.endUsing)),
                new HarmonyMethod(typeof(FarmerSpritePatcher), nameof(FarmerSpritePatcher.BeforeEndUsing)));
        }

        /// <summary>
        ///     Sets a <see cref="FarmerSprite" /> next frame offset.
        /// </summary>
        /// <param name="farmerSprite">The <see cref="FarmerSprite" /> instance.</param>
        /// <param name="offset">The new offset.</param>
        public static void SetNextOffset(this FarmerSprite farmerSprite, int offset)
        {
            FarmerSpritePatcher.FarmerSpritesData.GetOrCreateValue(farmerSprite).NextOffset = offset;
        }

        /// <summary>A method called via Harmony before <see cref="Tool.endUsing" />.</summary>
        /// <param name="__instance">The <see cref="Tool" /> __instance.</param>
        /// <param name="who">The <see cref="Farmer" /> using the <see cref="Tool" />.</param>
        private static bool BeforeEndUsing(Tool __instance, Farmer who)
        {
            who.stopJittering();

            who.canReleaseTool = false;

            int addedAnimationMultiplayer = who.Stamina > 0 ? 1 : 2;

            if (Game1.isAnyGamePadButtonBeingPressed() || !who.IsLocalPlayer)
            {
                who.lastClick = who.GetToolLocation();
            }

            if (__instance.Name.Equals("Seeds"))
            {
                switch (who.FacingDirection)
                {
                    case 2:
                        ((FarmerSprite)who.Sprite).animateOnce(200, 150f, 4);
                        break;
                    case 1:
                        ((FarmerSprite)who.Sprite).animateOnce(204, 150f, 4);
                        break;
                    case 0:
                        ((FarmerSprite)who.Sprite).animateOnce(208, 150f, 4);
                        break;
                    case 3:
                        ((FarmerSprite)who.Sprite).animateOnce(212, 150f, 4);
                        break;
                }
            }
            else if (__instance is WateringCan wateringCan)
            {
                if (wateringCan.WaterLeft > 0 && who.ShouldHandleAnimationSound())
                {
                    who.currentLocation.localSound("wateringCan");
                }

                switch (who.FacingDirection)
                {
                    case 2:
                        ((FarmerSprite)who.Sprite).animateOnce(164, 125f * addedAnimationMultiplayer, 3);
                        break;
                    case 1:
                        ((FarmerSprite)who.Sprite).animateOnce(172, 125f * addedAnimationMultiplayer, 3);
                        break;
                    case 0:
                        ((FarmerSprite)who.Sprite).animateOnce(180, 125f * addedAnimationMultiplayer, 3);
                        break;
                    case 3:
                        ((FarmerSprite)who.Sprite).animateOnce(188, 125f * addedAnimationMultiplayer, 3);
                        break;
                }
            }
            else if (__instance is FishingRod fishingRod && who.IsLocalPlayer && Game1.activeClickableMenu is null)
            {
                if (!fishingRod.hit)
                {
                    __instance.DoFunction(who.currentLocation, (int)who.lastClick.X, (int)who.lastClick.Y, 1, who);
                }
            }
            else if (!(__instance is MeleeWeapon) && !(__instance is Pan) && !(__instance is Shears)
                     && !(__instance is MilkPail) && !(__instance is Slingshot))
            {
                FarmerSpritePatcher.FarmerSpritesData.GetOrCreateValue(who.FarmerSprite).NextOffset = 0;

                switch (who.FacingDirection)
                {
                    case 0:
                        ((FarmerSprite)who.Sprite).animateOnce(176, 60f * addedAnimationMultiplayer, 8);
                        break;
                    case 1:
                        ((FarmerSprite)who.Sprite).animateOnce(168, 60f * addedAnimationMultiplayer, 8);
                        break;
                    case 2:
                        ((FarmerSprite)who.Sprite).animateOnce(160, 60f * addedAnimationMultiplayer, 8);
                        break;
                    case 3:
                        ((FarmerSprite)who.Sprite).animateOnce(184, 60f * addedAnimationMultiplayer, 8);
                        break;
                }
            }

            return false;
        }

        /// <summary>A method called via Harmony before <see cref="FarmerSprite.setCurrentFrame" />.</summary>
        /// <returns>
        ///     Returns <see langword="true"/> so that prefixes can continue to execute and the the original method can ecentually run..
        /// </returns>
        private static bool BeforeSetCurrentFrame(FarmerSprite __instance, ref int offset)
        {
            int nextOffset = FarmerSpritePatcher.FarmerSpritesData.GetOrCreateValue(__instance).NextOffset;
            if (nextOffset != 0)
            {
                offset = nextOffset;
                FarmerSpritePatcher.FarmerSpritesData.GetOrCreateValue(__instance).NextOffset = 0;
            }

            return true;
        }

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

            // Relevant CIL code:
            //     if (base.currentAnimationIndex > this.currentAnimationFrames - 1)
            //         IL_0056: ldarg.0
            //         IL_0057: ldfld int32 StardewValley.AnimatedSprite::currentAnimationIndex
            //         IL_005c: ldarg.0
            //         IL_005d: ldfld int32 StardewValley.FarmerSprite::currentAnimationFrames
            //         IL_0062: ldc.i4.1
            //         IL_0063: sub
            //         IL_0064: ble IL_014c
            //
            // Code to include after:
            //     FarmerSpritePatcher.SwitchBackToLastTool(this.owner);

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

        /// <summary>
        ///     Keeps data about a <see cref="FarmerSprite" /> object.
        /// </summary>
        internal class FarmerSpriteData
        {
            public int NextOffset;
        }
    }
}