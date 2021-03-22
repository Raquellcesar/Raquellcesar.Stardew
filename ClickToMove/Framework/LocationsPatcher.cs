// -----------------------------------------------------------------------
// <copyright file="LocationsPatcher.cs" company="Raquellcesar">
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

    using Microsoft.Xna.Framework;

    using StardewValley;
    using StardewValley.Locations;

    using xTile.Dimensions;

    using Rectangle = xTile.Dimensions.Rectangle;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class LocationsPatcher
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
                AccessTools.Method(typeof(BusStop), nameof(BusStop.checkAction)),
                new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.BeforeBusStopCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(BusStop), "playerReachedBusDoor"),
                new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.BeforePlayerReachedBusDoor)));

            harmony.Patch(
                AccessTools.Method(typeof(BusStop), nameof(BusStop.answerDialogue)),
                new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.BeforeAnswerDialogue)));

            harmony.Patch(
                AccessTools.Method(typeof(CommunityCenter), "afterViewportGetsToJunimoNotePosition"),
                postfix: new HarmonyMethod(
                    typeof(LocationsPatcher),
                    nameof(LocationsPatcher.AfterAfterViewportGetsToJunimoNotePosition)));

            harmony.Patch(
                AccessTools.Method(typeof(CommunityCenter), "resetLocalState"),
                postfix: new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.AfterResetLocalState)));

            harmony.Patch(
                AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.loadSpouseRoom)),
                postfix: new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.AfterLoadSpouseRoom)));

            harmony.Patch(
                AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.setMapForUpgradeLevel)),
                postfix: new HarmonyMethod(
                    typeof(LocationsPatcher),
                    nameof(LocationsPatcher.AfterSetMapForUpgradeLevel)));

            harmony.Patch(
                AccessTools.Method(typeof(MineShaft), nameof(MineShaft.loadLevel)),
                postfix: new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.AfterLoadLevel)));

            harmony.Patch(
                AccessTools.Method(typeof(Mountain), nameof(Mountain.checkAction)),
                new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.BeforeMountainCheckAction)));

            harmony.Patch(
                AccessTools.Method(typeof(MovieTheater), nameof(MovieTheater.performTouchAction)),
                new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.BeforePerformTouchAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Town), nameof(Town.checkAction)),
                new HarmonyMethod(typeof(LocationsPatcher), nameof(LocationsPatcher.BeforeTownCheckAction)));
        }

        private static void AfterAfterViewportGetsToJunimoNotePosition(CommunityCenter __instance)
        {
            ClickToMoveManager.GetOrCreate(__instance).Init();
        }

        private static void AfterLoadLevel(MineShaft __instance)
        {
            ClickToMoveManager.AddOrUpdate(__instance, new ClickToMove(__instance));
        }

        private static void AfterLoadSpouseRoom(FarmHouse __instance)
        {
            ClickToMoveManager.GetOrCreate(__instance).Graph.Init();
        }

        private static void AfterResetLocalState(CommunityCenter __instance)
        {
            ClickToMoveManager.GetOrCreate(__instance).Init();
        }

        private static void AfterSetMapForUpgradeLevel(FarmHouse __instance)
        {
            ClickToMoveManager.GetOrCreate(__instance).Init();
        }

        private static bool BeforeAnswerDialogue(BusStop __instance, Response answer)
        {
            if (__instance.lastQuestionKey is not null && __instance.afterQuestion is null)
            {
                string[] words = __instance.lastQuestionKey.Split(' ');

                if (words[0] == "Minecart")
                {
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = false;
                }

                string questionAndAnswer = words[0] + "_" + answer.responseKey;

                if (questionAndAnswer == "Bus_Yes")
                {
                    NPC pam = Game1.getCharacterFromName("Pam");
                    if (!(Game1.player.Money >= (Game1.shippingTax ? 50 : 500) && __instance.characters.Contains(pam)
                                                                               && pam.getTileLocation().Equals(
                                                                                   new Vector2(11f, 10f))))
                    {
                        ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = false;
                    }
                }
                else if (questionAndAnswer == "Bus_No")
                {
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = false;
                }
            }

            return true;
        }

        private static bool BeforeBusStopCheckAction(BusStop __instance, Location tileLocation)
        {
            if (__instance.map.GetLayer("Buildings").Tiles[tileLocation] is not null)
            {
                switch (__instance.map.GetLayer("Buildings").Tiles[tileLocation].TileIndex)
                {
                    case 958:
                    case 1080:
                    case 1081:
                        if (Game1.MasterPlayer.mailReceived.Contains("ccBoilerRoom")
                            && (Game1.player.mount is null || !Game1.player.isRidingHorse()))
                        {
                            ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = true;
                        }

                        break;
                    case 1057:
                        if (Game1.MasterPlayer.mailReceived.Contains("ccVault"))
                        {
                            if (Game1.player.mount is null || !Game1.player.isRidingHorse())
                            {
                                ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = true;
                            }
                        }
                        else
                        {
                            ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = false;
                        }

                        break;
                }
            }

            return true;
        }

        private static bool BeforeMountainCheckAction(Mountain __instance, Location tileLocation)
        {
            if (__instance.map.GetLayer("Buildings").Tiles[tileLocation] is not null)
            {
                int tileIndex = __instance.map.GetLayer("Buildings").Tiles[tileLocation].TileIndex;

                if ((tileIndex == 958 || tileIndex == 1080 || tileIndex == 1081)
                    && Game1.MasterPlayer.mailReceived.Contains("ccBoilerRoom") && Game1.player.mount is null
                    && !Game1.player.isRidingHorse())
                {
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = true;
                }
            }

            return true;
        }

        private static bool BeforePerformTouchAction(MovieTheater __instance, string fullActionString)
        {
            if (fullActionString.Split(' ')[0] == "Theater_Exit")
            {
                ClickToMoveManager.GetOrCreate(__instance).Reset();
            }

            return true;
        }

        private static bool BeforePlayerReachedBusDoor()
        {
            ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = false;

            return true;
        }

        private static bool BeforeTownCheckAction(
            Town __instance,
            Location tileLocation,
            Rectangle viewport,
            Farmer who)
        {
            if (__instance.map.GetLayer("Buildings").Tiles[tileLocation] is not null && who.mount is null)
            {
                int tileIndex = __instance.map.GetLayer("Buildings").Tiles[tileLocation].TileIndex;
                if ((tileIndex == 958 || tileIndex == 1080 || tileIndex == 1081) && Game1.player.mount is null
                    && (__instance.currentEvent is null || !__instance.currentEvent.isFestival
                                                        || !__instance.currentEvent.checkAction(
                                                            tileLocation,
                                                            viewport,
                                                            who)) && !(Game1.player.getTileX() <= 70
                                                                       && (Game1.CurrentEvent is null
                                                                           || Game1.CurrentEvent.FestivalName != "Egg Festival"))
                    && Game1.MasterPlayer.mailReceived.Contains("ccBoilerRoom"))
                {
                    ClickToMoveManager.GetOrCreate(Game1.currentLocation).PreventMountingHorse = true;
                }
            }

            return true;
        }
    }
}