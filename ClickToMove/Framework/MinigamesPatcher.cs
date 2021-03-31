// ------------------------------------------------------------------------------------------------
// <copyright file="MinigamesPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Harmony;

    using StardewModdingAPI;

    using StardewValley.Minigames;

    /// <summary>
    ///     Encapsulates Harmony patches for Menigames in the game.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class MinigamesPatcher
    {
        /// <summary>
        ///     A reference to the private field <see cref="FishingGame"/>.showResultsTimer. To be
        ///     used when updating the <see cref="FishingGame"/> state (see <see cref="ClickToMoveManager.ReceiveClickToMoveKeyStates"/>).
        /// </summary>
        private static IReflectedField<int> fishingGameShowResultsTimerField;

        /// <summary>
        ///     A reference to the private field <see cref="FishingGame"/>.timerToStart. To be
        ///     used when updating the <see cref="FishingGame"/> state (see <see
        ///     cref="ClickToMoveManager.OnLeftClick"/> and <see cref="ClickToMoveManager.OnLeftClickRelease"/>).
        /// </summary>
        private static IReflectedField<int> fishingGameTimerToStartField;

        /// <summary>
        ///     Gets or sets a value indicating whether the <see cref="FishingGame"/> should receive
        ///     a left click on the next update.
        /// </summary>
        public static bool LeftClickNextUpdateFishingGame { get; set; }

        /// <summary>
        ///     Gets or sets the private field <see cref="FishingGame"/>.showResultsTimer.
        /// </summary>
        public static int FishingGameShowResultsTimer { get => fishingGameShowResultsTimerField.GetValue(); set => fishingGameShowResultsTimerField.SetValue(value); }

        /// <summary>
        ///     Gets or sets the private field <see cref="FishingGame"/>.timerToStart.
        /// </summary>
        public static int FishingGameTimerToStart { get => fishingGameTimerToStartField.GetValue(); set => fishingGameTimerToStartField.SetValue(value); }

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Constructor(typeof(FishingGame)),
                postfix: new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.AfterFishingGameConstructor)));

            harmony.Patch(
                AccessTools.Method(typeof(FishingGame), nameof(FishingGame.receiveLeftClick)),
                new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.BeforeReceiveLeftClick)));

            harmony.Patch(
                AccessTools.Method(typeof(FishingGame), nameof(FishingGame.releaseLeftClick)),
                new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.BeforeReleaseLeftClick)));
        }

        /// <summary>
        ///     A method called via Harmony after the <see cref="FishingGame"/> constructor. It
        ///     initializes the private field references.
        /// </summary>
        /// <param name="__instance">The <see cref="FishingGame"/> instance.</param>
        private static void AfterFishingGameConstructor(FishingGame __instance)
        {
            MinigamesPatcher.fishingGameShowResultsTimerField = ClickToMoveManager.Reflection.GetField<int>(__instance, "showResultsTimer");

            MinigamesPatcher.fishingGameTimerToStartField = ClickToMoveManager.Reflection.GetField<int>(__instance, "timerToStart");

            MinigamesPatcher.LeftClickNextUpdateFishingGame = false;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="FishingGame.receiveLeftClick"/> that
        ///     replaces it. It does nothing as all input is dealt with by <see cref="ClickToMoveManager.UpdateMinigameInput"/>.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="false"/>, terminating prefixes and skipping the execution of
        ///     the original method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeReceiveLeftClick()
        {
            return false;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="FishingGame.releaseLeftClick"/> that
        ///     replaces it. It does nothing as all input is dealt with by <see cref="ClickToMoveManager.UpdateMinigameInput"/>.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="false"/>, terminating prefixes and skipping the execution of
        ///     the original method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeReleaseLeftClick()
        {
            return false;
        }
    }
}
