// -----------------------------------------------------------------------
// <copyright file="ShedPatcher.cs" company="Raquellcesar">
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

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Shed"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class ShedPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(Shed), nameof(Shed.setUpgradeLevel)),
                postfix: new HarmonyMethod(typeof(ShedPatcher), nameof(ShedPatcher.AfterSetUpgradeLevel)));
        }

        /// <summary>
        ///     Method called via Harmony after <see cref="Shed.setUpgradeLevel"/>. It reinitializes
        ///     the graph used by this instance.
        /// </summary>
        /// <param name="__instance">The <see cref="Shed"/> instance.</param>
        private static void AfterSetUpgradeLevel(Shed __instance)
        {
            ClickToMoveManager.GetOrCreate(__instance).Init();
        }
    }
}
