// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="BuildingsPatcher.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Harmony;

    using StardewValley;
    using StardewValley.Buildings;

    internal class BuildingsPatcher
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
                AccessTools.Method(typeof(Building), nameof(Building.dayUpdate)),
                postfix: new HarmonyMethod(typeof(BuildingsPatcher), nameof(BuildingsPatcher.AfterDayUpdate)));
        }

        /// <summary>
        ///     A method called via Harmony after <see cref="Building.dayUpdate" />.
        ///     It reinitializes the <see cref="ClickToMove" /> object associated to animal houses.
        /// </summary>
        /// <param name="__instance">The <see cref="Building" /> instance.</param>
        private static void AfterDayUpdate(Building __instance)
        {
            if (__instance.indoors.Value is AnimalHouse animalHouse)
            {
                ClickToMoveManager.GetOrCreate(animalHouse).Init();
            }
        }
    }
}