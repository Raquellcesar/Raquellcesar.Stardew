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
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Audio;
    using Microsoft.Xna.Framework.Input;

    using Netcode;

    using StardewValley;
    using StardewValley.Menus;
    using StardewValley.Objects;
    using StardewValley.Tools;

    using Object = StardewValley.Object;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Tool"/> classes.
    /// </summary>
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
               AccessTools.Method(typeof(MilkPail), "doFinish"),
               postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterMilkPailDoFinish)));

            harmony.Patch(
               AccessTools.Method(typeof(Shears), "doFinish"),
               postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterShearsDoFinish)));

            harmony.Patch(
                AccessTools.Method(typeof(Wand), nameof(Wand.DoFunction)),
                postfix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.AfterWandDoFunction)));

            harmony.Patch(
                AccessTools.Method(typeof(FishingRod), nameof(FishingRod.tickUpdate)),
                prefix: new HarmonyMethod(typeof(ToolsPatcher), nameof(ToolsPatcher.BeforeTickUpdate)));
        }

        private static bool BeforeTickUpdate(FishingRod __instance,
                                             GameTime time,
                                             Farmer who,
                                             Farmer ___lastUser,
                                             NetEvent0 ___beginReelingEvent,
                                             NetEvent0 ___putAwayEvent,
                                             NetEvent0 ___startCastingEvent,
                                             NetEventBinary ___pullFishFromWaterEvent,
                                             NetEvent1Field<bool, NetBool> ___doneFishingEvent,
                                             NetEvent0 ___castingEndEnableMovementEvent,
                                             int ___recastTimerMs,
                                             bool ____hasPlayerAdjustedBobber,
                                             bool ___usedGamePadToCast,
                                             string ___itemCategory,
                                             int ___whichFish,
                                             int ___fishQuality,
                                             NetVector2 ____totalMotion,
                                             Vector2[] ____totalMotionBuffer,
                                             int ____totalMotionBufferIndex,
                                             Vector2 ____lastAppliedMotion)
        {
            ___lastUser = who;
            ___beginReelingEvent.Poll();
            ___putAwayEvent.Poll();
            ___startCastingEvent.Poll();
            ___pullFishFromWaterEvent.Poll();
            ___doneFishingEvent.Poll();
            ___castingEndEnableMovementEvent.Poll();
            if (___recastTimerMs > 0 && who.IsLocalPlayer)
            {
                if (Game1.input.GetMouseState().LeftButton == ButtonState.Pressed || Game1.didPlayerJustClickAtAll() || Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton))
                {
                    ___recastTimerMs -= time.ElapsedGameTime.Milliseconds;
                    if (___recastTimerMs <= 0)
                    {
                        ___recastTimerMs = 0;
                        if (Game1.activeClickableMenu == null)
                        {
                            who.BeginUsingTool();
                        }
                    }
                }
                else
                {
                    ___recastTimerMs = 0;
                }
            }

            if (__instance.isFishing && !Game1.shouldTimePass() && Game1.activeClickableMenu != null && !(Game1.activeClickableMenu is BobberBar))
            {
                return false;
            }

            if (who.CurrentTool != null && who.CurrentTool.Equals(__instance) && who.UsingTool)
            {
                who.CanMove = false;
            }
            else if (Game1.currentMinigame == null && (who.CurrentTool == null || !(who.CurrentTool is FishingRod) || !who.UsingTool))
            {
                if (FishingRod.chargeSound != null && FishingRod.chargeSound.IsPlaying && who.IsLocalPlayer)
                {
                    FishingRod.chargeSound.Stop(AudioStopOptions.Immediate);
                    FishingRod.chargeSound = null;
                }

                return false;
            }

            for (int j = __instance.animations.Count - 1; j >= 0; j--)
            {
                if (__instance.animations[j].update(time))
                {
                    __instance.animations.RemoveAt(j);
                }
            }

            if (__instance.sparklingText != null && __instance.sparklingText.update(time))
            {
                __instance.sparklingText = null;
            }

            if (__instance.castingChosenCountdown > 0f)
            {
                __instance.castingChosenCountdown -= time.ElapsedGameTime.Milliseconds;
                if (__instance.castingChosenCountdown <= 0f && who.CurrentTool != null)
                {
                    switch (who.FacingDirection)
                    {
                        case 0:
                            who.FarmerSprite.animateOnce(295, 1f, 1);
                            who.CurrentTool.Update(0, 0, who);
                            break;
                        case 1:
                            who.FarmerSprite.animateOnce(296, 1f, 1);
                            who.CurrentTool.Update(1, 0, who);
                            break;
                        case 2:
                            who.FarmerSprite.animateOnce(297, 1f, 1);
                            who.CurrentTool.Update(2, 0, who);
                            break;
                        case 3:
                            who.FarmerSprite.animateOnce(298, 1f, 1);
                            who.CurrentTool.Update(3, 0, who);
                            break;
                    }

                    if (who.FacingDirection == 1 || who.FacingDirection == 3)
                    {
                        float distance2 = Math.Max(128f, __instance.castingPower * (float)(who.GetFishingAddedDistance() + 4) * 64f);
                        distance2 -= 8f;
                        float gravity2 = 0.005f;
                        float velocity2 = (float)((double)distance2 * Math.Sqrt(gravity2 / (2f * (distance2 + 96f))));
                        float t2 = 2f * (velocity2 / gravity2) + (float)((Math.Sqrt(velocity2 * velocity2 + 2f * gravity2 * 96f) - (double)velocity2) / (double)gravity2);
                        if (___lastUser.IsLocalPlayer)
                        {
                            __instance.bobber.Set(new Vector2((float)who.getStandingX() + (float)((who.FacingDirection != 3) ? 1 : (-1)) * distance2, who.getStandingY()));
                        }

                        __instance.animations.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(170, 1903, 7, 8), t2, 1, 0, who.Position + new Vector2(0f, -96f), flicker: false, flipped: false, (float)who.getStandingY() / 10000f, 0f, Color.White, 4f, 0f, 0f, (float)Game1.random.Next(-20, 20) / 100f)
                        {
                            motion = new Vector2(((who.FacingDirection != 3) ? 1 : (-1)) * velocity2, 0f - velocity2),
                            acceleration = new Vector2(0f, gravity2),
                            endFunction = __instance.castingEndFunction,
                            timeBasedMotion = true
                        });
                    }
                    else
                    {
                        float distance = 0f - Math.Max(128f, __instance.castingPower * (float)(who.GetFishingAddedDistance() + 3) * 64f);
                        float height = Math.Abs(distance - 64f);
                        if (___lastUser.FacingDirection == 0)
                        {
                            distance = 0f - distance;
                            height += 64f;
                        }

                        float gravity = 0.005f;
                        float velocity = (float)Math.Sqrt(2f * gravity * height);
                        float t = (float)(Math.Sqrt(2f * (height - distance) / gravity) + (double)(velocity / gravity));
                        t *= 1.05f;
                        if (___lastUser.FacingDirection == 0)
                        {
                            t *= 1.05f;
                        }

                        if (___lastUser.IsLocalPlayer)
                        {
                            __instance.bobber.Set(new Vector2(who.getStandingX(), (float)who.getStandingY() - distance));
                        }

                        __instance.animations.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(170, 1903, 7, 8), t, 1, 0, who.Position + new Vector2(24f, -96f), flicker: false, flipped: false, __instance.bobber.Y / 10000f, 0f, Color.White, 4f, 0f, 0f, (float)Game1.random.Next(-20, 20) / 100f)
                        {
                            alphaFade = 0.0001f,
                            motion = new Vector2(0f, 0f - velocity),
                            acceleration = new Vector2(0f, gravity),
                            endFunction = __instance.castingEndFunction,
                            timeBasedMotion = true
                        });
                    }

                    ____hasPlayerAdjustedBobber = false;
                    __instance.castedButBobberStillInAir = true;
                    __instance.isCasting = false;
                    if (who.IsLocalPlayer)
                    {
                        who.currentLocation.playSound("cast");
                    }

                    if (who.IsLocalPlayer && Game1.soundBank != null)
                    {
                        FishingRod.reelSound = Game1.soundBank.GetCue("slowReel");
                        FishingRod.reelSound.SetVariable("Pitch", 1600);
                        FishingRod.reelSound.Play();
                    }
                }
            }
            else if (!__instance.isTimingCast && __instance.castingChosenCountdown <= 0f)
            {
                who.jitterStrength = 0f;
            }

            if (__instance.isTimingCast)
            {
                if (FishingRod.chargeSound == null && Game1.soundBank != null)
                {
                    FishingRod.chargeSound = Game1.soundBank.GetCue("SinWave");
                }

                if (who.IsLocalPlayer && FishingRod.chargeSound != null && !FishingRod.chargeSound.IsPlaying)
                {
                    FishingRod.chargeSound.Play();
                }

                __instance.castingPower = Math.Max(0f, Math.Min(1f, __instance.castingPower + __instance.castingTimerSpeed * (float)time.ElapsedGameTime.Milliseconds));
                if (who.IsLocalPlayer && FishingRod.chargeSound != null)
                {
                    FishingRod.chargeSound.SetVariable("Pitch", 2400f * __instance.castingPower);
                }

                if (__instance.castingPower == 1f || __instance.castingPower == 0f)
                {
                    __instance.castingTimerSpeed = 0f - __instance.castingTimerSpeed;
                }

                who.armOffset.Y = 2f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
                who.jitterStrength = Math.Max(0f, __instance.castingPower - 0.5f);
                ClickToMoveKeyStates clickToMoveKeyStates = ClickToMoveManager.GetOrCreate(Game1.currentLocation).ClickKeyStates;
                ClickToMoveManager.Monitor.Log($"Ticks {Game1.ticks} -> FishingRod.tickUpdate - Left Mouse Button: {Game1.input.GetMouseState().LeftButton}; UseToolButtonHeld: {clickToMoveKeyStates.UseToolButtonHeld}; UseToolButtonPressed: {clickToMoveKeyStates.UseToolButtonPressed}; UseToolButtonReleased: {clickToMoveKeyStates.UseToolButtonReleased}");
                if (who.IsLocalPlayer && ((!___usedGamePadToCast && (Game1.input.GetMouseState().LeftButton == ButtonState.Released && !clickToMoveKeyStates.UseToolButtonPressed)) || (___usedGamePadToCast && Game1.options.gamepadControls && Game1.input.GetGamePadState().IsButtonUp(Buttons.X))) && Game1.areAllOfTheseKeysUp(Game1.GetKeyboardState(), Game1.options.useToolButton))
                {
                    ClickToMoveManager.Monitor.Log($"Ticks {Game1.ticks} -> FishingRod.tickUpdate - startCastingEvent fired");
                    ___startCastingEvent.Fire();
                }
                else
                {
                    ClickToMoveManager.Monitor.Log($"Ticks {Game1.ticks} -> FishingRod.tickUpdate - startCastingEvent not fired");
                }

                return false;
            }

            if (__instance.isReeling)
            {
                if (who.IsLocalPlayer && Game1.didPlayerJustClickAtAll())
                {
                    if (Game1.isAnyGamePadButtonBeingPressed())
                    {
                        Game1.lastCursorMotionWasMouse = false;
                    }

                    switch (who.FacingDirection)
                    {
                        case 0:
                            who.FarmerSprite.setCurrentSingleFrame(76, 32000);
                            break;
                        case 1:
                            who.FarmerSprite.setCurrentSingleFrame(72, 100);
                            break;
                        case 2:
                            who.FarmerSprite.setCurrentSingleFrame(75, 32000);
                            break;
                        case 3:
                            who.FarmerSprite.setCurrentSingleFrame(72, 100, secondaryArm: false, flip: true);
                            break;
                    }

                    who.armOffset.Y = (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
                    who.jitterStrength = 1f;
                }
                else
                {
                    switch (who.FacingDirection)
                    {
                        case 0:
                            who.FarmerSprite.setCurrentSingleFrame(36, 32000);
                            break;
                        case 1:
                            who.FarmerSprite.setCurrentSingleFrame(48, 100);
                            break;
                        case 2:
                            who.FarmerSprite.setCurrentSingleFrame(66, 32000);
                            break;
                        case 3:
                            who.FarmerSprite.setCurrentSingleFrame(48, 100, secondaryArm: false, flip: true);
                            break;
                    }

                    who.stopJittering();
                }

                who.armOffset = new Vector2((float)Game1.random.Next(-10, 11) / 10f, (float)Game1.random.Next(-10, 11) / 10f);
                __instance.bobberTimeAccumulator += time.ElapsedGameTime.Milliseconds;

                return false;
            }

            if (__instance.isFishing)
            {
                if (___lastUser.IsLocalPlayer)
                {
                    __instance.bobber.Y += (float)(0.10000000149011612 * Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0));
                }

                who.canReleaseTool = true;
                __instance.bobberTimeAccumulator += time.ElapsedGameTime.Milliseconds;
                switch (who.FacingDirection)
                {
                    case 0:
                        who.FarmerSprite.setCurrentFrame(44);
                        break;
                    case 1:
                        who.FarmerSprite.setCurrentFrame(89);
                        break;
                    case 2:
                        who.FarmerSprite.setCurrentFrame(70);
                        break;
                    case 3:
                        who.FarmerSprite.setCurrentFrame(89, 0, 10, 1, flip: true, secondaryArm: false);
                        break;
                }

                who.armOffset.Y = (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2) + (float)((who.FacingDirection == 1 || who.FacingDirection == 3) ? 1 : (-1));
                if (!who.IsLocalPlayer)
                {
                    return false;
                }

                if (__instance.timeUntilFishingBite != -1f)
                {
                    __instance.fishingBiteAccumulator += time.ElapsedGameTime.Milliseconds;
                    if (__instance.fishingBiteAccumulator > __instance.timeUntilFishingBite)
                    {
                        __instance.fishingBiteAccumulator = 0f;
                        __instance.timeUntilFishingBite = -1f;
                        __instance.isNibbling = true;
                        if (__instance.hasEnchantmentOfType<AutoHookEnchantment>())
                        {
                            __instance.timePerBobberBob = 1f;
                            __instance.timeUntilFishingNibbleDone = FishingRod.maxTimeToNibble;
                            __instance.DoFunction(who.currentLocation, (int)__instance.bobber.X, (int)__instance.bobber.Y, 1, who);
                            Rumble.rumble(0.95f, 200f);

                            return false;
                        }

                        who.PlayFishBiteChime();
                        Rumble.rumble(0.75f, 250f);
                        __instance.timeUntilFishingNibbleDone = FishingRod.maxTimeToNibble;
                        if (Game1.currentMinigame == null)
                        {
                            Game1.screenOverlayTempSprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(395, 497, 3, 8), new Vector2(___lastUser.getStandingX() - Game1.viewport.X, ___lastUser.getStandingY() - 128 - 8 - Game1.viewport.Y), flipped: false, 0.02f, Color.White)
                            {
                                scale = 5f,
                                scaleChange = -0.01f,
                                motion = new Vector2(0f, -0.5f),
                                shakeIntensityChange = -0.005f,
                                shakeIntensity = 1f
                            });
                        }

                        __instance.timePerBobberBob = 1f;
                    }
                }

                if (__instance.timeUntilFishingNibbleDone != -1f && !__instance.hit)
                {
                    __instance.fishingNibbleAccumulator += time.ElapsedGameTime.Milliseconds;
                    if (__instance.fishingNibbleAccumulator > __instance.timeUntilFishingNibbleDone)
                    {
                        __instance.fishingNibbleAccumulator = 0f;
                        __instance.timeUntilFishingNibbleDone = -1f;
                        __instance.isNibbling = false;
                        __instance.timeUntilFishingBite = ClickToMoveManager.Reflection.GetMethod(__instance, "calculateTimeUntilFishingBite").Invoke<float>(__instance.CalculateBobberTile(), false, who);
                    }
                }

                return false;
            }

            Vector2 motion;
            if (who.UsingTool && __instance.castedButBobberStillInAir)
            {
                motion = Vector2.Zero;
                if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveDownButton))
                {
                    goto IL_0d9f;
                }

                if (Game1.options.gamepadControls)
                {
                    _ = Game1.oldPadState;
                    if (Game1.oldPadState.IsButtonDown(Buttons.DPadDown) || Game1.input.GetGamePadState().ThumbSticks.Left.Y < 0f)
                    {
                        goto IL_0d9f;
                    }
                }

                goto IL_0dc7;
            }

            if (__instance.showingTreasure)
            {
                who.FarmerSprite.setCurrentSingleFrame(0, 32000);
            }
            else if (__instance.fishCaught)
            {
                if (!Game1.isFestival())
                {
                    who.faceDirection(2);
                    who.FarmerSprite.setCurrentFrame(84);
                }

                if (Game1.random.NextDouble() < 0.025)
                {
                    who.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(653, 858, 1, 1), 9999f, 1, 1, who.Position + new Vector2(Game1.random.Next(-3, 2) * 4, -32f), flicker: false, flipped: false, (float)who.getStandingY() / 10000f + 0.002f, 0.04f, Color.LightBlue, 5f, 0f, 0f, 0f)
                    {
                        acceleration = new Vector2(0f, 0.25f)
                    });
                }

                if (!who.IsLocalPlayer || (Game1.input.GetMouseState().LeftButton != ButtonState.Pressed && !Game1.didPlayerJustClickAtAll() && !Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton)))
                {
                    return false;
                }

                who.currentLocation.localSound("coin");
                if (!__instance.treasureCaught)
                {
                    ___recastTimerMs = 200;
                    Object item2 = null;
                    if (___itemCategory == "Object")
                    {
                        item2 = new Object(___whichFish, 1, isRecipe: false, -1, ___fishQuality);
                        if (___whichFish == GameLocation.CAROLINES_NECKLACE_ITEM)
                        {
                            item2.questItem.Value = true;
                        }

                        if (___whichFish == 79 || ___whichFish == 842)
                        {
                            item2 = who.currentLocation.tryToCreateUnseenSecretNote(___lastUser);
                            if (item2 == null)
                            {
                                return false;
                            }
                        }

                        if (__instance.caughtDoubleFish)
                        {
                            item2.Stack = 2;
                        }
                    }
                    else if (___itemCategory == "Furniture")
                    {
                        item2 = new Furniture(___whichFish, Vector2.Zero);
                    }

                    bool cachedFromFishPond = __instance.fromFishPond;
                    ___lastUser.completelyStopAnimatingOrDoingAction();
                    __instance.doneFishing(___lastUser, !cachedFromFishPond);
                    if (!Game1.isFestival() && !cachedFromFishPond && ___itemCategory == "Object" && Game1.player.team.specialOrders != null)
                    {
                        foreach (SpecialOrder order2 in Game1.player.team.specialOrders)
                        {
                            if (order2.onFishCaught != null)
                            {
                                order2.onFishCaught(Game1.player, item2);
                            }
                        }
                    }

                    if (!Game1.isFestival() && !___lastUser.addItemToInventoryBool(item2))
                    {
                        Game1.activeClickableMenu = new ItemGrabMenu(new List<Item> { item2 }, __instance).setEssential(essential: true);
                    }

                    return false;
                }

                __instance.fishCaught = false;
                __instance.showingTreasure = true;
                who.UsingTool = true;
                int stack = 1;
                if (__instance.caughtDoubleFish)
                {
                    stack = 2;
                }

                Object item = new Object(___whichFish, stack, isRecipe: false, -1, ___fishQuality);
                if (Game1.player.team.specialOrders != null)
                {
                    foreach (SpecialOrder order in Game1.player.team.specialOrders)
                    {
                        if (order.onFishCaught != null)
                        {
                            order.onFishCaught(Game1.player, item);
                        }
                    }
                }

                bool hadroomForfish = ___lastUser.addItemToInventoryBool(item);
                __instance.animations.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(64, 1920, 32, 32), 500f, 1, 0, ___lastUser.Position + new Vector2(-32f, -160f), flicker: false, flipped: false, (float)___lastUser.getStandingY() / 10000f + 0.001f, 0f, Color.White, 4f, 0f, 0f, 0f)
                {
                    motion = new Vector2(0f, -0.128f),
                    timeBasedMotion = true,
                    endFunction = __instance.openChestEndFunction,
                    extraInfoForEndBehavior = (!hadroomForfish) ? 1 : 0,
                    alpha = 0f,
                    alphaFade = -0.002f
                });
            }
            else if (who.UsingTool && __instance.castedButBobberStillInAir && __instance.doneWithAnimation)
            {
                switch (who.FacingDirection)
                {
                    case 0:
                        who.FarmerSprite.setCurrentFrame(39);
                        break;
                    case 1:
                        who.FarmerSprite.setCurrentFrame(89);
                        break;
                    case 2:
                        who.FarmerSprite.setCurrentFrame(28);
                        break;
                    case 3:
                        who.FarmerSprite.setCurrentFrame(89, 0, 10, 1, flip: true, secondaryArm: false);
                        break;
                }

                who.armOffset.Y = (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
            }
            else if (!__instance.castedButBobberStillInAir && ___whichFish != -1 && __instance.animations.Count > 0 && __instance.animations[0].timer > 500f && !Game1.eventUp)
            {
                ___lastUser.faceDirection(2);
                ___lastUser.FarmerSprite.setCurrentFrame(57);
            }

            return false;
            IL_0f58:
            if (!____hasPlayerAdjustedBobber)
            {
                Vector2 bobber_tile = __instance.CalculateBobberTile();
                if (!___lastUser.currentLocation.isTileFishable((int)bobber_tile.X, (int)bobber_tile.Y))
                {
                    if (___lastUser.FacingDirection == 3 || ___lastUser.FacingDirection == 1)
                    {
                        int offset2 = 1;
                        if (bobber_tile.Y % 1f < 0.5f)
                        {
                            offset2 = -1;
                        }

                        if (___lastUser.currentLocation.isTileFishable((int)bobber_tile.X, (int)bobber_tile.Y + offset2))
                        {
                            motion.Y += (float)offset2 * 4f;
                        }
                        else if (___lastUser.currentLocation.isTileFishable((int)bobber_tile.X, (int)bobber_tile.Y - offset2))
                        {
                            motion.Y -= (float)offset2 * 4f;
                        }
                    }

                    if (___lastUser.FacingDirection == 0 || ___lastUser.FacingDirection == 2)
                    {
                        int offset = 1;
                        if (bobber_tile.X % 1f < 0.5f)
                        {
                            offset = -1;
                        }

                        if (___lastUser.currentLocation.isTileFishable((int)bobber_tile.X + offset, (int)bobber_tile.Y))
                        {
                            motion.X += (float)offset * 4f;
                        }
                        else if (___lastUser.currentLocation.isTileFishable((int)bobber_tile.X - offset, (int)bobber_tile.Y))
                        {
                            motion.X -= (float)offset * 4f;
                        }
                    }
                }
            }

            if (who.IsLocalPlayer)
            {
                __instance.bobber.Set(__instance.bobber + motion);
                ____totalMotion.Set(____totalMotion.Value + motion);
            }

            if (__instance.animations.Count <= 0)
            {
                return false;
            }

            Vector2 applied_motion = Vector2.Zero;
            if (who.IsLocalPlayer)
            {
                applied_motion = ____totalMotion.Value;
            }
            else
            {
                ____totalMotionBuffer[____totalMotionBufferIndex] = ____totalMotion.Value;
                for (int i = 0; i < ____totalMotionBuffer.Length; i++)
                {
                    applied_motion += ____totalMotionBuffer[i];
                }

                applied_motion /= (float)____totalMotionBuffer.Length;
                ____totalMotionBufferIndex = (____totalMotionBufferIndex + 1) % ____totalMotionBuffer.Length;
            }

            __instance.animations[0].position -= ____lastAppliedMotion;
            ____lastAppliedMotion = applied_motion;
            __instance.animations[0].position += applied_motion;
            return false;
            IL_0e4d:
            if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveUpButton))
            {
                goto IL_0eaa;
            }

            if (Game1.options.gamepadControls)
            {
                _ = Game1.oldPadState;
                if (Game1.oldPadState.IsButtonDown(Buttons.DPadUp) || Game1.input.GetGamePadState().ThumbSticks.Left.Y > 0f)
                {
                    goto IL_0eaa;
                }
            }

            goto IL_0ed2;
            IL_0ed2:
            if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveLeftButton))
            {
                goto IL_0f2f;
            }

            if (Game1.options.gamepadControls)
            {
                _ = Game1.oldPadState;
                if (Game1.oldPadState.IsButtonDown(Buttons.DPadLeft) || Game1.input.GetGamePadState().ThumbSticks.Left.X < 0f)
                {
                    goto IL_0f2f;
                }
            }

            goto IL_0f58;
            IL_0e24:
            if (who.FacingDirection != 1 && who.FacingDirection != 3)
            {
                motion.X += 2f;
                ____hasPlayerAdjustedBobber = true;
            }

            goto IL_0e4d;
            IL_0dc7:
            if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveRightButton))
            {
                goto IL_0e24;
            }

            if (Game1.options.gamepadControls)
            {
                _ = Game1.oldPadState;
                if (Game1.oldPadState.IsButtonDown(Buttons.DPadRight) || Game1.input.GetGamePadState().ThumbSticks.Left.X > 0f)
                {
                    goto IL_0e24;
                }
            }

            goto IL_0e4d;
            IL_0eaa:
            if (who.FacingDirection != 0 && who.FacingDirection != 2)
            {
                motion.Y -= 4f;
                ____hasPlayerAdjustedBobber = true;
            }

            goto IL_0ed2;
            IL_0d9f:
            if (who.FacingDirection != 2 && who.FacingDirection != 0)
            {
                motion.Y += 4f;
                ____hasPlayerAdjustedBobber = true;
            }

            goto IL_0dc7;
            IL_0f2f:
            if (who.FacingDirection != 3 && who.FacingDirection != 1)
            {
                motion.X -= 2f;
                ____hasPlayerAdjustedBobber = true;
            }

            goto IL_0f58;
        }

        private static Vector2 CalculateBobberTile(this FishingRod rod)
        {
            Vector2 position = rod.bobber;
            position.X = rod.bobber.X / 64f;
            position.Y = rod.bobber.Y / 64f;
            return position;
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
