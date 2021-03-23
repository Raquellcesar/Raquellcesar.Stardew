// -----------------------------------------------------------------------
// <copyright file="FarmerPatcher.cs" company="Raquellcesar">
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
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using Harmony;

    using Microsoft.Xna.Framework;

    using Netcode;

    using StardewModdingAPI;

    using StardewValley;

    using SObject = StardewValley.Object;

    /// <summary>
    ///     Applies Harmony patches to the <see cref="Farmer"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class FarmerPatcher
    {
        /// <summary>
        ///     Associates new properties to <see cref="Farmer"/> objects at runtime.
        /// </summary>
        private static readonly ConditionalWeakTable<Farmer, FarmerData> FarmersData =
            new ConditionalWeakTable<Farmer, FarmerData>();

        /// <summary>
        ///     Encapsulates logging for the Harmony patch.
        /// </summary>
        private static IMonitor monitor;

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        /// <param name="monitor">Encapsulates logging for the Harmony patch.</param>
        public static void Hook(HarmonyInstance harmony, IMonitor monitor)
        {
            FarmerPatcher.monitor = monitor;

            // Can't access the constructor using AccessTools, because it will originate an
            // AmbiguousMatchException, since there's a static constructor with the same signature
            // being implemented by the compiler under the hood.
            harmony.Patch(
                typeof(Farmer).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new Type[0],
                    new ParameterModifier[0]),
                postfix: new HarmonyMethod(typeof(FarmerPatcher), nameof(FarmerPatcher.AfterConstructor)));

            harmony.Patch(
                AccessTools.Constructor(
                    typeof(Farmer),
                    new[] { typeof(FarmerSprite), typeof(Vector2), typeof(int), typeof(string), typeof(List<Item>), typeof(bool), }),
                postfix: new HarmonyMethod(typeof(FarmerPatcher), nameof(FarmerPatcher.AfterConstructor)));

            harmony.Patch(
                AccessTools.Property(typeof(Farmer), nameof(Farmer.ActiveObject)).GetGetMethod(),
                new HarmonyMethod(typeof(FarmerPatcher), nameof(FarmerPatcher.BeforeGetActiveObject)));

            harmony.Patch(
                AccessTools.Method(typeof(Farmer), nameof(Farmer.completelyStopAnimatingOrDoingAction)),
                postfix: new HarmonyMethod(
                    typeof(FarmerPatcher),
                    nameof(FarmerPatcher.AfterCompletelyStopAnimatingOrDoingAction)));

            harmony.Patch(
                AccessTools.Property(typeof(Farmer), nameof(Farmer.CurrentItem)).GetGetMethod(),
                new HarmonyMethod(typeof(FarmerPatcher), nameof(FarmerPatcher.BeforeGetCurrentItem)));

            harmony.Patch(
                AccessTools.Method(typeof(Farmer), nameof(Farmer.forceCanMove)),
                postfix: new HarmonyMethod(typeof(FarmerPatcher), nameof(FarmerPatcher.AfterForceCanMove)));

            harmony.Patch(
                AccessTools.Method(typeof(Farmer), "performSickAnimation"),
                new HarmonyMethod(typeof(FarmerPatcher), nameof(FarmerPatcher.BeforePerformSickAnimation)));
        }

        /// <summary>
        ///     A method called via Harmony before the getter for <see cref="Farmer.CurrentItem"/>
        ///     that replaces it. This method checks if currentToolIndex is equal to -1 before
        ///     accessing items.
        /// </summary>
        /// <param name="__instance">The <see cref="Farmer"/> instance.</param>
        /// <param name="___currentToolIndex">The private field <see cref="Farmer.currentToolIndex"/>.</param>
        /// <param name="____itemStowed">The private field <see cref="Farmer._itemStowed"/>.</param>
        /// <param name="__result">A reference to the result of the original method.</param>
        /// <returns>
        ///     Returns <see langword="false"/>, terminating prefixes and skipping the execution of
        ///     the original method, effectively replacing the original method.
        /// </returns>
        internal static bool BeforeGetCurrentItem(
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
        ///     Gets if this farmer is being sick, i.e. is performing the sick animation.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this farmer is being sick, returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        internal static bool IsFarmerBeingSick(this Farmer farmer)
        {
            return farmer is not null && FarmerPatcher.FarmersData.GetOrCreateValue(farmer).IsBeingSick;
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Farmer.completelyStopAnimatingOrDoingAction"/>.
        /// </summary>
        /// <param name="__instance">The <see cref="Farmer"/> instance.</param>
        private static void AfterCompletelyStopAnimatingOrDoingAction(Farmer __instance)
        {
            FarmerData farmerData = FarmerPatcher.FarmersData.GetOrCreateValue(__instance);
            farmerData.IsBeingSick = false;
        }

        /// <summary>
        ///     A method called via Harmony after the constructor for <see cref="Farmer"/>.
        /// </summary>
        /// <param name="___currentToolIndex">
        ///     The private field currentToolIndex of the <see cref="Farmer"/> instance.
        /// </param>
        private static void AfterConstructor(NetInt ___currentToolIndex)
        {
            ___currentToolIndex.Set(-1);
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Farmer.forceCanMove"/>.
        /// </summary>
        /// <param name="__instance">The farmer instance.</param>
        private static void AfterForceCanMove(Farmer __instance)
        {
            FarmerData farmerData = FarmerPatcher.FarmersData.GetOrCreateValue(__instance);
            farmerData.IsBeingSick = false;
        }

        /// <summary>
        ///     A method called via Harmony before the getter for <see cref="Farmer.ActiveObject"/>
        ///     that replaces it. This method checks if currentToolIndex is equal to -1 before
        ///     accessing items.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="false"/>, terminating prefixes and skipping the execution of
        ///     the original method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeGetActiveObject(
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
        ///     A method called via Harmony before <see cref="Farmer.performSickAnimation"/>. It
        ///     replaces the original method, so we can register the beginning of the animation and
        ///     invoke a callback when the animation ends (see <see cref="OnFinishSickAnim"/>).
        /// </summary>
        /// <param name="__instance">The farmer instance.</param>
        /// <returns>
        ///     Returns <see langword="false"/> so this method replaces the original method.
        /// </returns>
        private static bool BeforePerformSickAnimation(Farmer __instance)
        {
            if (__instance.isEmoteAnimating)
            {
                __instance.EndEmoteAnimation();
            }

            __instance.isEating = false;

            FarmerPatcher.FarmersData.GetOrCreateValue(__instance).IsBeingSick = true;

            __instance.FarmerSprite.animateOnce(224, 350, 4, FarmerPatcher.OnFinishSickAnim);
            __instance.doEmote(12);

            return false;
        }

        /// <summary>
        ///     Delegate to be called after the sick animation ends. <see
        ///     cref="BeforePerformSickAnimation"/> Used to detect when the farmer stops being sick.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> that was animated.</param>
        private static void OnFinishSickAnim(Farmer farmer)
        {
            FarmerData farmerData = FarmerPatcher.FarmersData.GetOrCreateValue(farmer);
            farmerData.IsBeingSick = false;
        }

        /// <summary>
        ///     Keeps data about a <see cref="Farmer"/> object.
        /// </summary>
        private class FarmerData
        {
            /// <summary>
            ///     Gets or sets a value indicating whether a farmer is being sick.
            /// </summary>
            public bool IsBeingSick { get; set; }
        }
    }
}
