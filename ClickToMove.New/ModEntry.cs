// --------------------------------------------------------------------------------------------------------------------
// <copyright company="RaquellCesar" file="ModEntry.cs">
//     Copyright (c) 2021 Raquellcesar
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove
{
    using Harmony;

    using Raquellcesar.Stardew.ClickToMove.Framework;

    using StardewModdingAPI;
    using StardewModdingAPI.Events;

    using StardewValley;

    using System.Linq.Expressions;

    /// <summary>
    ///     The mod entry point.
    /// </summary>
    public class ModEntry : Mod
    {
        /// <summary>
        ///     The manager of all <see cref="PathFindingController"/> objects.
        /// </summary>
        private PathFindingManager pathFindingManager;

        /// <summary>
        ///     The mod entry point, called after the mod is first loaded.
        /// </summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            ClickToMoveHelper.Init(this.Monitor, this.Helper.Reflection);

            this.pathFindingManager = new PathFindingManager(helper);

            // Hook events.
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;

            // Add patches.
            HarmonyInstance.DEBUG = true;
            HarmonyInstance harmony = HarmonyInstance.Create(this.ModManifest.UniqueID);
            ClickToMovePatcher.Hook(harmony, helper, this.Monitor, this.pathFindingManager);

            // Log info
            this.Monitor.VerboseLog("Initialized.");
        }

        /// <summary>
        ///     The event called after the game draws the world to the screen.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            // Ignore if player hasn't loaded a save yet.
            if (!Context.IsWorldReady)
            {
                return;
            }

            this.pathFindingManager.DrawPathTarget(Game1.spriteBatch);
        }
    }
}
