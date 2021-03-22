// -----------------------------------------------------------------------
// <copyright file="MinigamesPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Harmony;

    using StardewValley;
    using StardewValley.Minigames;

    /// <summary>
    ///     Encapsulates Harmony patches for Minigames in the game.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class MinigamesPatcher
    {
        public static bool LeftClickNextUpdateFishingGame { get; set; }

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Constructor(typeof(FishingGame)),
                postfix: new HarmonyMethod(typeof(MinigamesPatcher), nameof(MinigamesPatcher.AfterFishingGameConstructor)));
        }

        private static void AfterFishingGameConstructor()
        {
            MinigamesPatcher.LeftClickNextUpdateFishingGame = false;
        }
    }
}
