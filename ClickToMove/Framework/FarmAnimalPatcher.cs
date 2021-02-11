// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="FarmAnimalPatcher.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of __instance source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    using StardewValley;

    internal static class FarmAnimalPatcher
    {
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.draw), new[] { typeof(SpriteBatch) }),
                new HarmonyMethod(typeof(FarmAnimalPatcher), nameof(FarmAnimalPatcher.BeforeDraw)));
        }

        private static bool BeforeDraw(FarmAnimal __instance, SpriteBatch b)
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
                    0f,
                    Vector2.Zero,
                    4f,
                    SpriteEffects.None,
                    0.01f);
            }

            return true;
        }
    }
}