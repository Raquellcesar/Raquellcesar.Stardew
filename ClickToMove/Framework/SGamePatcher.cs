// ------------------------------------------------------------------------------------------------
// <copyright file="SGamePatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    using StardewModdingAPI;
    using StardewModdingAPI.Framework;

    using StardewValley;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="NPC"/> class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class SGamePatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            Assembly smapiAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName.StartsWith("StardewModdingAPI"));
            Type sGameType = smapiAssembly.GetType("StardewModdingAPI.Framework.SGame");

            harmony.Patch(
                AccessTools.Method(sGameType, "DrawImpl"),
                transpiler: new HarmonyMethod(typeof(SGamePatcher), nameof(SGamePatcher.TranspileDrawImpl)));
        }

        /// <summary>
        ///     Draws a green square below the current path's NPC target.
        /// </summary>
        private static void DrawTargetNpc()
        {
            if (Game1.currentLocation is not null && ClickToMoveManager.GetOrCreate(Game1.currentLocation).TargetNpc is NPC npc)
            {
                Game1.spriteBatch.Draw(
                    Game1.mouseCursors,
                    Game1.GlobalToLocal(
                        Game1.viewport,
                        new Vector2(
                            (int)npc.Position.X + (npc.Sprite.SpriteWidth * 4 / 2) - (Game1.tileSize / 2),
                            (int)npc.Position.Y + npc.GetBoundingBox().Height + (npc.IsMonster ? 0 : 12) - (Game1.tileSize / 2))),
                    new Rectangle(194, 388, 16, 16),
                    Color.White,
                    0,
                    Vector2.Zero,
                    4,
                    SpriteEffects.None,
                    0.58f);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="SGame"/>.DrawImpl. Signals the
        ///     current path's NPC target, if there is one.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileDrawImpl(
            IEnumerable<CodeInstruction> instructions)
        {
            /*
             * Relevant CIL code:
             *      Layer building_layer = Game1.currentLocation.Map.GetLayer("Buildings");
             *      building_layer.Draw(Game1.mapDisplayDevice, Game1.viewport, Location.Origin, wrapAround: false, 4);
             *      Game1.mapDisplayDevice.EndScene();
             *          ...
             *          ldstr "Buildings"
             *          IL_1108: callvirt instance class [xTile]xTile.Layers.Layer [xTile]xTile.Map::GetLayer(string)
             *          ...
             *          IL_112c: callvirt instance void [xTile]xTile.Display.IDisplayDevice::EndScene()
             *
             * Code to include after:
             *      SGamePatcher.DrawTargetNpc();
             */

            MethodInfo drawTargerNpc =
                AccessTools.Method(typeof(SGamePatcher), nameof(SGamePatcher.DrawTargetNpc));

            bool found1 = false;
            bool found2 = false;

            foreach (CodeInstruction instruction in instructions)
            {
                if (!found1
                    && instruction.opcode == OpCodes.Ldstr
                    && instruction.operand is string text && text == "Buildings")
                {
                    yield return instruction;
                    found1 = true;
                    continue;
                }

                if (found1
                    && !found2
                    && instruction.opcode == OpCodes.Callvirt
                    && instruction.operand is MethodInfo { Name: "EndScene" })
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, drawTargerNpc);

                    found2 = true;
                    continue;
                }

                yield return instruction;
            }

            if (!found1 || !found2)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch SGame.DrawImpl.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }
    }
}
