// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ObjectsPatcher.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of __instance source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Harmony;

    using StardewValley;
    using StardewValley.Objects;

    internal static class ObjectsPatcher
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
                AccessTools.Method(typeof(Wallpaper), nameof(Wallpaper.placementAction)),
                new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.BeforePlacementAction)));
        }

        private static bool BeforePlacementAction(GameLocation location)
        {
            ClickToMoveManager.GetOrCreate(location).Reset();

            return true;
        }
    }
}