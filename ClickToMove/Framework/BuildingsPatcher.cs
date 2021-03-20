// -----------------------------------------------------------------------
// <copyright file="BuildingsPatcher.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Harmony;

    using StardewValley;
    using StardewValley.Buildings;

    /// <summary>Encapsulates Harmony patches for Buildings.</summary>
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
        private static void AfterDayUpdate(Building __instance)
        {
            if (__instance.indoors.Value is AnimalHouse animalHouse)
            {
                ClickToMoveManager.GetOrCreate(animalHouse).Init();
            }
        }
    }
}
