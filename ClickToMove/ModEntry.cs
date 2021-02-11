// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ModEntry.cs">
//   Copyright (c) 2021 Raquellcesar//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove
{
    using Harmony;

    using Raquellcesar.Stardew.ClickToMove.Framework;

    using StardewModdingAPI;
    using StardewModdingAPI.Events;

    using StardewValley;

    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        /// <summary>The mod configuration.</summary>
        private static ModConfig config;

        /// <summary>
        ///     The mod entry point, called after the mod is first loaded.
        /// </summary>
        /// <param name="helper">
        ///     Provides simplified APIs for writing mods.
        /// </param>
        public override void Entry(IModHelper helper)
        {
            // Read the configuration file.
            ModEntry.config = helper.ReadConfig<ModConfig>();

            // Initialization.
            ClickToMoveManager.Init(ModEntry.config, this.Monitor, helper, this.Helper.Reflection);

            HarmonyInstance.DEBUG = true;

            // Add patches.
            HarmonyInstance harmony = HarmonyInstance.Create(this.ModManifest.UniqueID);

            BuildingsPatcher.Hook(harmony);
            CharactersPatcher.Hook(harmony);
            EventPatcher.Hook(harmony);
            FarmAnimalPatcher.Hook(harmony);
            FarmerPatcher.Hook(harmony, this.Monitor);
            FarmerSpritePatcher.Hook(harmony);
            GamePatcher.Hook(harmony);
            GameLocationPatcher.Hook(harmony);
            LocationsPatcher.Hook(harmony);
            MenusPatcher.Hook(harmony);
            MinigamesPatcher.Hook(harmony);
            ObjectsPatcher.Hook(harmony);
            ShedPatcher.Hook(harmony);
            ToolsPatcher.Hook(harmony);
            UtilityPatcher.Hook(harmony);

            // Hook events.
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;

            // Log info
            this.Monitor.VerboseLog("Initialized.");
        }

        /// <summary>The event called after the game draws the world to the screen.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            // Ignore if player hasn't loaded a save yet.
            if (!Context.IsWorldReady)
            {
                return;
            }

            ClickToMoveManager.DrawClickToMoveTarget(Game1.spriteBatch);
        }
    }
}
