// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="CommonHelper.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common
{
    using System;

    using StardewModdingAPI;

    using StardewValley;

    /// <summary>Provides common utility methods for interacting with the game code shared by various mods.</summary>
    internal static class CommonHelper
    {
        /// <summary>Log an error and warn the user.</summary>
        /// <param name="monitor">Encapsulates monitoring and logging.</param>
        /// <param name="ex">The exception to handle.</param>
        /// <param name="verb">
        ///     The verb describing where the error occurred (e.g. "looking that up"). This is displayed on the
        ///     screen, so it should be simple and avoid characters that might not be available in the sprite font.
        /// </param>
        /// <param name="detailedVerb">
        ///     A more detailed form of verb, if applicable. This is displayed in the log, so
        ///     it can be more technical and isn't constrained by the sprite font.
        /// </param>
        public static void InterceptError(this IMonitor monitor, Exception ex, string verb, string detailedVerb = null)
        {
            detailedVerb ??= verb;
            monitor.Log($"Something went wrong {detailedVerb}:\n{ex}", LogLevel.Error);
            CommonHelper.ShowErrorMessage(
                $"Huh. Something went wrong {verb}. The error log has the technical details.");
        }

        /// <summary>Intercept errors thrown by the action.</summary>
        /// <param name="monitor">Encapsulates monitoring and logging.</param>
        /// <param name="verb">
        ///     The verb describing where the error occurred (e.g. "looking that up"). This is displayed on the
        ///     screen, so it should be simple and avoid characters that might not be available in the sprite font.
        /// </param>
        /// <param name="action">The action to invoke.</param>
        /// <param name="onError">A callback invoked if an error is intercepted.</param>
        public static void InterceptErrors(
            this IMonitor monitor,
            string verb,
            Action action,
            Action<Exception> onError = null)
        {
            monitor.InterceptErrors(verb, null, action, onError);
        }

        /// <summary>Intercept errors thrown by the action.</summary>
        /// <param name="monitor">Encapsulates monitoring and logging.</param>
        /// <param name="verb">
        ///     The verb describing where the error occurred (e.g. "looking that up"). This is displayed on the
        ///     screen, so it should be simple and avoid characters that might not be available in the sprite font.
        /// </param>
        /// <param name="detailedVerb">
        ///     A more detailed form of <see cref="verb" /> if applicable. This is displayed in the log, so
        ///     it can be more technical and isn't constrained by the sprite font.
        /// </param>
        /// <param name="action">The action to invoke.</param>
        /// <param name="onError">A callback invoked if an error is intercepted.</param>
        public static void InterceptErrors(
            this IMonitor monitor,
            string verb,
            string detailedVerb,
            Action action,
            Action<Exception> onError = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                monitor.InterceptError(ex, verb, detailedVerb);
                onError?.Invoke(ex);
            }
        }

        /// <summary>Show an error message to the player.</summary>
        /// <param name="message">The message to show.</param>
        public static void ShowErrorMessage(string message)
        {
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
        }
    }
}