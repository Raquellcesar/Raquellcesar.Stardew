// -----------------------------------------------------------------------
// <copyright file="FarmAnimalPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    using StardewValley;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="FarmAnimal"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class FarmAnimalPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.draw), new[] { typeof(SpriteBatch) }),
                new HarmonyMethod(typeof(FarmAnimalPatcher), nameof(FarmAnimalPatcher.BeforeDraw)));
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="FarmAnimal.draw"/>. Signals the
        ///     current path's animal target, if any.
        /// </summary>
        /// <param name="__instance">The <see cref="FarmAnimal"/> instance.</param>
        /// <param name="b">The <see cref="SpriteBatch"/> to draw to.</param>
        private static void BeforeDraw(FarmAnimal __instance, SpriteBatch b)
        {
            if (ClickToMoveManager.GetOrCreate(Game1.currentLocation).TargetFarmAnimal == __instance)
            {
                b.Draw(
                    Game1.mouseCursors,
                    Game1.GlobalToLocal(
                        Game1.viewport,
                        new Vector2(
                            (int)__instance.Position.X + (__instance.Sprite.getWidth() * 4 / 2) - 32,
                            (int)__instance.Position.Y + (__instance.Sprite.getHeight() * 4 / 2) - 24)),
                    new Rectangle(194, 388, 16, 16),
                    Color.White,
                    0,
                    Vector2.Zero,
                    4,
                    SpriteEffects.None,
                    0.01f);
            }
        }
    }
}
