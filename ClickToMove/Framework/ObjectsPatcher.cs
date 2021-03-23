// -----------------------------------------------------------------------
// <copyright file="ObjectsPatcher.cs" company="Raquellcesar">
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
    using StardewValley.Objects;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Object"/> classes.
    /// </summary>
    internal static class ObjectsPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(Wallpaper), nameof(Wallpaper.placementAction)),
                new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.BeforeWallpaperPlacementAction)));
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Wallpaper.placementAction"/>. It
        ///     resets the <see cref="ClickToMove"/> object associated to the current game location.
        /// </summary>
        /// <param name="location">The current <see cref="GameLocation"/>.</param>
        private static void BeforeWallpaperPlacementAction(GameLocation location)
        {
            ClickToMoveManager.GetOrCreate(location).Reset();
        }
    }
}
