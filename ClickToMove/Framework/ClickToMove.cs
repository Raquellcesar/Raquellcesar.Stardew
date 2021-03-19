// -----------------------------------------------------------------------
// <copyright file="ClickToMove.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be
//     found in the LICENSE file in the project root or at
//     https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Xna.Framework;

    using Raquellcesar.Stardew.ClickToMove.Framework.PathFinding;
    using Raquellcesar.Stardew.Common;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Characters;
    using StardewValley.Locations;
    using StardewValley.Menus;
    using StardewValley.Minigames;
    using StardewValley.Monsters;
    using StardewValley.Objects;
    using StardewValley.TerrainFeatures;
    using StardewValley.Tools;

    using xTile.Dimensions;
    using xTile.Tiles;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>
    ///     This class encapsulates all the details needed to implement the click to move functionality.
    ///     Each instance will be associated to a single <see cref="GameLocation" /> and will maintain data to optimize
    ///     path finding in that location.
    /// </summary>
    public class ClickToMove
    {
        private const int MaxReallyStuckCount = 2;

        /// <summary>
        ///     The maximum number of times we allow the farmer to get stuck in the path.
        /// </summary>
        private const int MaxStuckCount = 4;

        /// <summary>
        ///     Maximum number of allowed attempts at computing a path to a clicked destination.
        /// </summary>
        private const int MaxTries = 2;

        /// <summary>
        ///     The time the mouse left button must be pressed before we condider it held (350 ms).
        /// </summary>
        private const int TicksBeforeClickHoldKicksIn = 3500000;

        /// <summary>
        ///     All actionable objects.
        /// </summary>
        private static readonly List<int> ActionableObjectIds = new List<int>(
            new[]
                {
                    286, 287, 288, 298, 322, 323, 324, 325, 599, 621, 645, 405, 407, 409, 411, 415, 309, 310, 311, 313,
                    314, 315, 316, 317, 318, 319, 320, 321, 328, 329, 331, 401, 93, 94, 294, 295, 297, 461, 463, 464,
                    746, 326,
                });

        private static readonly string[] FestivalNames =
            {
                "eggFestival", "flowerFestival", "luau", "jellies", "fair", "iceFestival",
            };

        /// <summary>
        ///     The time of the last click.
        /// </summary>
        private static long startTime = long.MaxValue;

        private readonly Queue<ClickQueueItem> clickQueue = new Queue<ClickQueueItem>();

        /// <summary>
        ///     The <see cref="GameLocation" /> associated to this object.
        /// </summary>
        private readonly GameLocation gameLocation;

        private readonly IReflectedField<bool> ignoreWarps;

        /// <summary>
        ///     The list of the indexes of the last used tools.
        /// </summary>
        private readonly Stack<int> lastToolIndexList = new Stack<int>();

        private Building actionableBuilding;

        private bool clickedCinemaDoor;

        private bool clickedCinemaTicketBooth;

        private bool clickedHaleyBracelet;

        /// <summary>
        ///     The node associated with the tile clicked by the player.
        /// </summary>
        private AStarNode clickedNode;

        private bool clickedOnCrop;

        /// <summary>
        ///     The <see cref="Horse"/> clicked at the end of the path.
        /// </summary>
        private Horse clickedOnHorse;

        private Point clickedTile = new Point(-1, -1);

        private Point clickPoint = new Point(-1, -1);

        private bool clickPressed;

        private CrabPot crabPot;

        private DistanceToTarget distanceToTarget;

        /// <summary>
        ///     Whether checking for monsters to attack is enabled.
        /// </summary>
        private bool enableCheckToAttackMonsters = true;

        /// <summary>
        ///     Whether the node at the end of the path is occupied by something.
        /// </summary>
        private bool endNodeOccupied;

        private bool endNodeToBeActioned;

        private bool endTileIsActionable;

        private AStarNode finalNode;

        private SObject forageItem;

        private ResourceClump forestLog;

        private Fence gateClickedOn;

        private AStarNode gateNode;

        private bool justClosedActiveMenu;

        private bool justUsedWeapon;

        private float lastDistance;

        private Monster monsterTarget;

        private MouseCursor mouseCursor = MouseCursor.None;

        private int mouseX;

        private int mouseY;

        private Point noPathHere = new Point(-1, -1);

        /// <summary>
        ///     Contains the path last computed by the A* algorithm.
        /// </summary>
        private AStarPath path;

        private bool pendingFurnitureAction;

        private bool performActionFromNeighbourTile;

        private ClickToMovePhase phase;

        private int reallyStuckCount;

        private Furniture rotatingFurniture;

        private AStarNode startNode;

        private int stuckCount;

        private string toolToSelect;

        private int tryCount;

        private int viewportX;

        private int viewportY;

        private bool waitingToFinishWatering;

        private WalkDirection walkDirectionToMouse = WalkDirection.None;

        private bool warping;

        private bool waterSourceAndFishingRodSelected;

        public ClickToMove(GameLocation gameLocation)
        {
            this.gameLocation = gameLocation;

            this.ignoreWarps = ClickToMoveManager.Reflection.GetField<bool>(gameLocation, "ignoreWarps");

            this.Graph = new AStarGraph(this.gameLocation);
        }

        /// <summary>
        ///     The last <see cref="MeleeWeapon"/> used.
        /// </summary>
        public static MeleeWeapon LastMeleeWeapon { get; set; }

        public Point ClickedTile => this.clickedTile;

        public bool ClickHoldActive { get; private set; }

        public ClickToMoveKeyStates ClickKeyStates { get; } = new ClickToMoveKeyStates();

        public Point ClickPoint => this.clickPoint;

        public Vector2 ClickVector => new Vector2(this.clickPoint.X, this.clickPoint.Y);

        public Furniture Furniture { get; private set; }

        public Point GrabTile { get; set; } = Point.Zero;

        /// <summary>
        ///     Gets the graph used for path finding.
        /// </summary>
        public AStarGraph Graph { get; }

        public bool IgnoreWarps => this.ignoreWarps.GetValue();

        public bool Moving => this.phase > ClickToMovePhase.None;

        public Point NoPathHere => this.noPathHere;

        public bool PreventMountingHorse { get; set; }

        /// <summary>
        ///     Gets the <see cref="FarmAnimal" /> that's at the current goal node, if any.
        /// </summary>
        public FarmAnimal TargetFarmAnimal { get; private set; }

        /// <summary>
        ///     Gets the <see cref="NPC" /> that's at the current goal node, if any.
        /// </summary>
        public NPC TargetNpc { get; private set; }

        /// <summary>
        ///     The time we need to wait before checking gor monsters to attack again (500 ms).
        /// </summary>
        private const int MinimumTicksBetweenMonsterChecks = 5000000;

        /// <summary>
        ///     Clears all data relative to auto selection of tools.
        /// </summary>
        public void ClearAutoSelectTool()
        {
            this.lastToolIndexList.Clear();
            this.toolToSelect = null;
        }

        /// <summary>
        ///     (Re)Initializes the graph used by this instance.
        /// </summary>
        public void Init()
        {
            this.Graph.Init(); // = new AStarGraph(this.gameLocation);
        }

        /// <summary>
        ///     Called if the mouse left button is pressed by the player.
        /// </summary>
        /// <param name="mouseX">The mouse x coordinate.</param>
        /// <param name="mouseY">The mouse y coordinate.</param>
        /// <param name="viewportX">The viewport x coordinate.</param>
        /// <param name="viewportY">The viewport y coordinate.</param>
        /// <param name="tryCount">The number of times we tried to follow the path.</param>
        public void OnClick(int mouseX, int mouseY, int viewportX, int viewportY, int tryCount = 0)
        {
            while (true)
            {
                if (Game1.player.passedOut || Game1.player.FarmerSprite.isPassingOut() || Game1.player.isEating
                    || FarmerPatcher.IsFarmerBeingSick(Game1.player))
                {
                    return;
                }

                if (this.justClosedActiveMenu || ClickToMoveManager.OnScreenButtonClicked || Game1.locationRequest is not null)
                {
                    return;
                }

                this.clickPressed = true;

                ClickToMove.startTime = DateTime.Now.Ticks;

                Point clickPoint = new Point(mouseX + viewportX, mouseY + viewportY);
                Point clickedTile = new Point(clickPoint.X / Game1.tileSize, clickPoint.Y / Game1.tileSize);

                AStarNode clickedNode = this.Graph.GetNode(clickedTile.X, clickedTile.Y);

                this.SetMouseCursor(clickedNode);

                if (this.clickedOnCrop)
                {
                    if (this.ClickedOnAnotherQueueableCrop(clickedNode))
                    {
                        if (this.AddToClickQueue(mouseX, mouseY, viewportX, viewportY)
                            && Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool
                            && this.phase == ClickToMovePhase.None)
                        {
                            this.waitingToFinishWatering = true;
                        }

                        return;
                    }

                    if (this.ClickedOnHoeDirtAndHoldingSeed(clickedNode))
                    {
                        this.AddToClickQueue(mouseX, mouseY, viewportX, viewportY);
                    }
                    else
                    {
                        this.clickedOnCrop = false;
                        this.clickQueue.Clear();
                    }
                }

                if (Game1.CurrentEvent is not null
                    && ((Game1.CurrentEvent.id == 0 && Game1.CurrentEvent.FestivalName == string.Empty)
                        || !Game1.CurrentEvent.playerControlSequence))
                {
                    return;
                }

                if (!Game1.player.CanMove && Game1.player.UsingTool && Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter())
                {
                    Game1.player.Halt();

                    this.enableCheckToAttackMonsters = false;
                    this.justUsedWeapon = false;

                    this.ClickKeyStates.SetUseTool(false);
                }

                if (Game1.dialogueUp
                    || (Game1.activeClickableMenu is not null && !(Game1.activeClickableMenu is AnimalQueryMenu)
                                                              && !(Game1.activeClickableMenu is CarpenterMenu)
                                                              && !(Game1.activeClickableMenu is PurchaseAnimalsMenu)
                                                              && !(Game1.activeClickableMenu is MuseumMenu))
                    || (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
                    || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks()
                    || (Game1.player.ActiveObject is Furniture && this.gameLocation is DecoratableLocation))
                {
                    return;
                }

                if (Game1.currentMinigame is null && Game1.CurrentEvent is not null && Game1.CurrentEvent.isFestival
                    && Game1.CurrentEvent.FestivalName == "Stardew Valley Fair")
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                if (!Game1.player.CanMove && (Game1.eventUp || (this.gameLocation is FarmHouse && Game1.dialogueUp)))
                {
                    if (Game1.dialogueUp)
                    {
                        this.Reset();

                        this.phase = ClickToMovePhase.DoAction;
                    }
                    else if ((Game1.currentSeason == "winter" && Game1.dayOfMonth == 8)
                             || Game1.currentMinigame is FishingGame)
                    {
                        this.phase = ClickToMovePhase.UseTool;
                    }

                    if (!(Game1.player.CurrentTool is FishingRod))
                    {
                        return;
                    }
                }

                if (Game1.player.CurrentTool is Slingshot
                    && (Game1.currentMinigame is null || !(Game1.currentMinigame is TargetGame))
                    && Game1.player.ClickedOn(clickPoint.X, clickPoint.Y))
                {
                    this.ClickKeyStates.SetUseTool(true);

                    this.ClickKeyStates.RealClickHeld = true;

                    this.phase = ClickToMovePhase.ClickHeld;

                    return;
                }

                if (Game1.player.CurrentTool is Wand && Game1.player.ClickedOn(clickPoint.X, clickPoint.Y))
                {
                    this.phase = ClickToMovePhase.UseTool;

                    return;
                }

                if (Game1.currentMinigame is FishingGame && Game1.player.CurrentTool is FishingRod
                                                         && !Game1.player.CanMove && !Game1.player.UsingTool)
                {
                    Game1.player.CanMove = true;
                }

                if (!Game1.player.CanMove && Game1.player.CurrentTool is FishingRod)
                {
                    this.phase = ClickToMovePhase.UseTool;
                    return;
                }

                if (this.CheckToEatFood(clickPoint.X, clickPoint.Y))
                {
                    return;
                }

                if (tryCount >= ClickToMove.MaxTries)
                {
                    this.Reset();
                    Game1.player.Halt();
                    return;
                }

                this.Reset(false);

                this.ClickKeyStates.ResetLeftOrRightClickButtons();

                this.mouseX = mouseX;
                this.mouseY = mouseY;
                this.viewportX = viewportX;
                this.viewportY = viewportY;
                this.tryCount = tryCount;
                this.noPathHere.X = this.noPathHere.Y = -1;
                this.tryCount = tryCount;
                this.ClickKeyStates.RealClickHeld = true;
                this.clickPoint = clickPoint;
                this.clickedTile = clickedTile;

                if (this.gameLocation is FarmHouse farmHouse)
                {
                    Point bedSpot = farmHouse.getBedSpot();

                    if ((this.clickedTile.X == bedSpot.X
                         && (this.clickedTile.Y == bedSpot.Y - 1 || this.clickedTile.Y == bedSpot.Y + 1))
                        || (this.clickedTile.X == bedSpot.X - 1
                            && (this.clickedTile.Y == bedSpot.Y - 1 || this.clickedTile.Y == bedSpot.Y
                                                                    || this.clickedTile.Y == bedSpot.Y + 1)))
                    {
                        this.clickedTile.X = bedSpot.X;
                        this.clickedTile.Y = bedSpot.Y;
                    }
                }

                if (this.clickedTile.X == 37 && this.clickedTile.Y == 79 && this.gameLocation is Town
                    && Game1.CurrentEvent is not null && Game1.CurrentEvent.FestivalName == "Stardew Valley Fair")
                {
                    this.clickedTile.Y = 80;
                }

                if (Game1.player.isRidingHorse()
                    && (this.GetHorseAlternativeBoundingBox(Game1.player.mount).Contains(clickPoint.X, clickPoint.Y)
                        || Game1.player.ClickedOn(clickPoint.X, clickPoint.Y))
                    && Game1.player.mount.checkAction(Game1.player, this.gameLocation))
                {
                    this.Reset();
                    return;
                }

                if (this.HoldingWallpaperAndTileClickedIsWallOrFloor())
                {
                    this.ClickKeyStates.ActionButtonPressed = true;
                    return;
                }

                if (Game1.mailbox.Count > 0 && Game1.player.ActiveObject is null && this.gameLocation is Farm
                    && this.clickedTile.X == 68 && this.clickedTile.Y == 14)
                {
                    viewportY += Game1.tileSize;
                    continue;
                }

                if (this.mouseCursor == MouseCursor.MagnifyingGlass)
                {
                    if (!ClickToMoveHelper.ClickedEggAtEggFestival(this.ClickPoint))
                    {
                        if (!this.gameLocation.checkAction(
                                new Location(this.clickedTile.X, this.clickedTile.Y),
                                Game1.viewport,
                                Game1.player))
                        {
                            this.gameLocation.checkAction(
                                new Location(this.clickedTile.X, this.clickedTile.Y + 1),
                                Game1.viewport,
                                Game1.player);
                        }

                        this.Reset();
                        Game1.player.Halt();
                        return;
                    }
                }
                else if (this.gameLocation is Town town
                         && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
                {
                    if (this.clickedTile.X >= 48 && this.clickedTile.X <= 51
                                                 && (this.clickedTile.Y == 18 || this.clickedTile.Y == 19))
                    {
                        town.checkAction(new Location(this.clickedTile.X, 19), Game1.viewport, Game1.player);

                        this.Reset();
                        return;
                    }
                }
                else if (this.gameLocation is Beach beach && !beach.bridgeFixed
                                                          && (this.clickedTile.X == 58 || this.clickedTile.X == 59)
                                                          && (this.clickedTile.Y == 11 || this.clickedTile.Y == 12))
                {
                    beach.checkAction(new Location(58, 13), Game1.viewport, Game1.player);
                }
                else if (this.gameLocation is LibraryMuseum libraryMuseum
                         && (this.clickedTile.X != 3 || this.clickedTile.Y != 9))
                {
                    if (libraryMuseum.museumPieces.ContainsKey(new Vector2(this.clickedTile.X, this.clickedTile.Y)))
                    {
                        if (libraryMuseum.checkAction(
                            new Location(this.clickedTile.X, this.clickedTile.Y),
                            Game1.viewport,
                            Game1.player))
                        {
                            return;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (libraryMuseum.doesTileHaveProperty(
                                    this.clickedTile.X,
                                    this.clickedTile.Y + i,
                                    "Action",
                                    "Buildings") is not null
                                && libraryMuseum.doesTileHaveProperty(
                                    this.clickedTile.X,
                                    this.clickedTile.Y + i,
                                    "Action",
                                    "Buildings").Contains("Notes") && libraryMuseum.checkAction(
                                    new Location(this.clickedTile.X, this.clickedTile.Y + i),
                                    Game1.viewport,
                                    Game1.player))
                            {
                                return;
                            }
                        }
                    }
                }

                if (!this.Graph.IsTileOnMap(this.clickedTile.X, this.clickedTile.Y))
                {
                    this.Reset();
                    return;
                }

                this.startNode = this.finalNode = this.Graph.FarmerNodeOffset;

                this.clickedNode = this.Graph.GetNode(this.clickedTile.X, this.clickedTile.Y);

                if (this.clickedNode is null)
                {
                    this.Reset();
                    return;
                }

                if (this.gameLocation.IsWater(this.clickedTile) && this.mouseCursor != MouseCursor.Hand
                                                                && this.mouseCursor != MouseCursor.ReadyForHarvest
                                                                && !(Game1.player.CurrentTool is WateringCan)
                                                                && !(Game1.player.CurrentTool is FishingRod)
                                                                && (Game1.player.ActiveObject is null
                                                                    || Game1.player.ActiveObject.ParentSheetIndex
                                                                    != ObjectId.CrabPot)
                                                                && !this.clickedNode.IsTilePassable()
                                                                && !this.gameLocation.IsOreAt(this.clickedTile))
                {
                    AStarNode crabPotNode = this.clickedNode.CrabPotNeighbour();

                    if (crabPotNode is null)
                    {
                        this.Reset();
                        return;
                    }

                    this.crabPot = this.clickedNode.GetObject() as CrabPot;
                    this.clickedNode = crabPotNode;
                    this.clickedTile.X = this.clickedNode.X;
                    this.clickedTile.Y = this.clickedNode.Y;
                }

                if (this.startNode is null || this.clickedNode is null)
                {
                    return;
                }

                if (clickedNode.ContainsFurniture())
                {
                    Furniture furnitureClickedOn = this.gameLocation.GetFurniture(
                        this.clickPoint.X,
                        this.clickPoint.Y);

                    if (furnitureClickedOn is not null)
                    {
                        if (this.rotatingFurniture == furnitureClickedOn && furnitureClickedOn.rotations.Value > 1)
                        {
                            this.rotatingFurniture.rotate();

                            this.Reset();

                            return;
                        }

                        if (furnitureClickedOn.rotations.Value > 1)
                        {
                            this.rotatingFurniture = furnitureClickedOn;
                        }
                    }

                    this.Furniture = furnitureClickedOn;

                    if (Game1.player.CurrentTool is FishingRod)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }
                else
                {
                    this.Furniture = null;
                    this.rotatingFurniture = null;
                }

                if (this.clickedNode.ContainsSomeKindOfWarp() && Game1.player.CurrentTool is FishingRod)
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                if (this.NodeBlocked(this.clickedNode))
                {
                    this.endNodeOccupied = true;
                    this.clickedNode.FakeTileClear = true;
                }
                else
                {
                    this.endNodeOccupied = false;
                }

                if (this.clickedOnHorse is not null && Game1.player.CurrentItem is Hat)
                {
                    this.clickedOnHorse.checkAction(Game1.player, this.gameLocation);

                    this.Reset();
                    return;
                }

                if (this.TargetNpc is not null && Game1.CurrentEvent is not null
                                               && Game1.CurrentEvent.playerControlSequenceID is not null
                                               && Game1.CurrentEvent.festivalTimer > 0
                                               && Game1.CurrentEvent.playerControlSequenceID == "iceFishing")
                {
                    this.Reset();
                    return;
                }

                if (!Game1.player.isRidingHorse() && Game1.player.mount is null && !this.performActionFromNeighbourTile && !this.endNodeToBeActioned)
                {
                    foreach (NPC npc in this.gameLocation.characters)
                    {
                        if (npc is Horse horse
                            && ClickToMoveHelper.Distance(this.ClickPoint, horse.GetBoundingBox().Center) < 48f
                            && (this.clickedTile.X != (int)horse.getTileLocation().X
                                || this.clickedTile.Y != (int)horse.getTileLocation().Y))
                        {
                            this.Reset();

                            this.OnClick(
                                ((int)horse.getTileLocation().X * Game1.tileSize) + (Game1.tileSize / 2) - viewportX,
                                ((int)horse.getTileLocation().Y * Game1.tileSize) + (Game1.tileSize / 2) - viewportY,
                                viewportX,
                                viewportY);

                            return;
                        }
                    }
                }

                if (this.clickedNode is not null && this.endNodeOccupied && !this.endNodeToBeActioned
                    && !this.performActionFromNeighbourTile && !this.endTileIsActionable
                    && !this.clickedNode.ContainsSomeKindOfWarp() && this.clickedNode.ContainsBuilding())
                {
                    Building building = this.clickedNode.GetBuilding();
                    if (building is null || !building.buildingType.Value.Equals("Mill"))
                    {
                        if (building is not null && building.buildingType.Value.Equals("Silo"))
                        {
                            building.doAction(new Vector2(this.clickedNode.X, this.clickedNode.Y), Game1.player);

                            return;
                        }

                        if (!this.clickedNode.ContainsTree() && this.actionableBuilding is null
                                                             && (!(this.gameLocation is Farm)
                                                                 || this.clickedNode.X != 21 || this.clickedNode.Y != 25
                                                                 || Game1.whichFarm != 3))
                        {
                            this.Reset();

                            return;
                        }
                    }
                }

                if (this.clickedNode.ContainsCinema() && !this.clickedCinemaTicketBooth && !this.clickedCinemaDoor)
                {
                    this.noPathHere = this.clickedTile;

                    this.Reset();

                    return;
                }

                if (this.endNodeOccupied)
                {
                    if (this.startNode.IsVerticalOrHorizontalNeighbour(this.clickedNode)
                    || (this.startNode.IsDiagonalNeighbour(this.clickedNode) && (Game1.player.CurrentTool is WateringCan or Hoe or MeleeWeapon || this.performActionFromNeighbourTile)))
                    {
                        this.phase = ClickToMovePhase.OnFinalTile;

                        return;
                    }
                }

                this.path = this.gameLocation is AnimalHouse
                                ? this.Graph.FindPath(this.startNode, this.clickedNode)
                                : this.Graph.FindPathWithBubbleCheck(this.startNode, this.clickedNode);

                if ((this.path is null || this.path.Count == 0 || this.path[0] is null)
                    && this.endNodeOccupied && this.performActionFromNeighbourTile)
                {
                    this.path = this.Graph.FindPathToNeighbourDiagonalWithBubbleCheck(this.startNode, this.clickedNode);

                    if (this.path is not null && this.path.Count > 0)
                    {
                        this.clickedNode.FakeTileClear = false;
                        this.clickedNode = this.path[this.path.Count - 1];
                        this.endNodeOccupied = false;
                        this.performActionFromNeighbourTile = false;
                    }
                }

                if (this.path is not null && this.path.Count > 0)
                {
                    this.gateNode = this.path.ContainsGate();

                    if (this.endNodeOccupied)
                    {
                        this.path.SmoothRightAngles(2);
                    }
                    else
                    {
                        this.path.SmoothRightAngles();
                    }

                    if (this.clickedNode.FakeTileClear)
                    {
                        if (this.path.Count > 0)
                        {
                            this.path.RemoveLastNode();
                        }

                        this.clickedNode.FakeTileClear = false;
                    }

                    if (this.path.Count > 0)
                    {
                        this.finalNode = this.path[this.path.Count - 1];
                    }

                    this.phase = ClickToMovePhase.FollowingPath;

                    return;
                }

                if (this.startNode.IsSameNode(this.clickedNode)
                    && (this.endNodeToBeActioned || this.performActionFromNeighbourTile))
                {
                    AStarNode neighbour = this.startNode.GetNeighbourPassable();

                    if (neighbour is null)
                    {
                        this.Reset();

                        return;
                    }

                    if (this.clickedOnCrop)
                    {
                        this.phase = ClickToMovePhase.UseTool;
                        return;
                    }

                    if (this.waterSourceAndFishingRodSelected)
                    {
                        this.FaceTileClicked(true);

                        this.phase = ClickToMovePhase.UseTool;

                        return;
                    }

                    this.path.Add(neighbour);
                    this.path.Add(this.startNode);

                    this.noPathHere.X = this.noPathHere.Y = -1;
                    this.finalNode = this.path[this.path.Count - 1];
                    this.phase = ClickToMovePhase.FollowingPath;

                    return;
                }

                if (this.startNode.IsSameNode(this.clickedNode))
                {
                    this.noPathHere.X = this.noPathHere.Y = -1;

                    Warp warp = this.clickedNode.GetWarp(this.IgnoreWarps);
                    if (warp is not null && (Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival))
                    {
                        this.gameLocation.WarpIfInRange(warp);
                    }

                    this.Reset();

                    return;
                }

                if (this.startNode is not null && Game1.player.ActiveObject is not null
                                               && Game1.player.ActiveObject.name == "Crab Pot")
                {
                    this.TryToFindAlternatePath(this.startNode);

                    return;
                }

                if (this.crabPot is not null && Vector2.Distance(
                        new Vector2(
                            (this.crabPot.TileLocation.X * Game1.tileSize) + (Game1.tileSize / 2),
                            (this.crabPot.TileLocation.Y * Game1.tileSize) + (Game1.tileSize / 2)),
                        Game1.player.Position) < Game1.tileSize * 2)
                {
                    this.PerformCrabPotAction();

                    return;
                }

                if (this.endTileIsActionable)
                {
                    mouseY += Game1.tileSize;

                    this.noPathHere = this.clickedTile;

                    if (tryCount > 0)
                    {
                        this.noPathHere.Y -= 1;
                    }

                    tryCount++;

                    continue;
                }

                if (this.TargetNpc is not null && this.TargetNpc.Name == "Robin"
                                               && this.gameLocation is BuildableGameLocation)
                {
                    this.TargetNpc.checkAction(Game1.player, this.gameLocation);
                }

                this.noPathHere = this.clickedTile;

                if (tryCount > 0)
                {
                    this.noPathHere.Y -= 1;
                }

                this.Reset();

                break;
            }
        }

        /// <summary>
        ///     Called if the mouse left button is being held by the player.
        /// </summary>
        /// <param name="mouseX">The mouse x coordinate.</param>
        /// <param name="mouseY">The mouse y coordinate.</param>
        /// <param name="viewportX">The viewport x coordinate.</param>
        /// <param name="viewportY">The viewport y coordinate.</param>
        public void OnClickHeld(int mouseX, int mouseY, int viewportX, int viewportY)
        {
            if (this.justClosedActiveMenu || ClickToMoveManager.OnScreenButtonClicked
                || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks()
                || Game1.currentMinigame is FishingGame
                                          || DateTime.Now.Ticks - ClickToMove.startTime
                                          < ClickToMove.TicksBeforeClickHoldKicksIn)
            {
                return;
            }

            this.ClickHoldActive = true;

            if ((this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile)
                 || this.forestLog is not null) && this.ClickKeyStates.RealClickHeld
                                                && (Game1.player.CurrentTool is Axe
                                                    || Game1.player.CurrentTool is Pickaxe)
                                                && this.phase != ClickToMovePhase.FollowingPath
                                                && this.phase != ClickToMovePhase.OnFinalTile
                                                && this.phase != ClickToMovePhase.ReachedEndOfPath
                                                && this.phase != ClickToMovePhase.Complete)
            {
                if (Game1.player.UsingTool)
                {
                    this.phase = ClickToMovePhase.None;
                    this.ClickKeyStates.SetUseTool(false);
                    this.ClickKeyStates.StopMoving();
                }
                else
                {
                    this.phase = ClickToMovePhase.UseTool;
                }
            }
            else if (this.waterSourceAndFishingRodSelected && this.ClickKeyStates.RealClickHeld
                                                           && Game1.player.CurrentTool is FishingRod)
            {
                if (this.phase == ClickToMovePhase.Complete)
                {
                    this.phase = ClickToMovePhase.UseTool;
                }
            }
            else if ((Game1.player.CurrentItem is Furniture || Game1.player.ActiveObject is Furniture)
                     && this.gameLocation is DecoratableLocation)
            {
                this.ClickKeyStates.SetMovement(WalkDirection.None);
                this.phase = ClickToMovePhase.None;
            }
            else if (this.Furniture is not null
                     && DateTime.Now.Ticks - ClickToMove.startTime > ClickToMove.TicksBeforeClickHoldKicksIn)
            {
                this.phase = ClickToMovePhase.UseTool;
            }
            else
            {
                if (!Game1.player.canMove
                    || this.warping
                    || this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile)
                    || this.forestLog is not null)
                {
                    return;
                }

                if (this.phase != ClickToMovePhase.None && this.phase != ClickToMovePhase.UsingJoyStick)
                {
                    this.Reset();
                }

                if (this.phase != ClickToMovePhase.UsingJoyStick)
                {
                    this.phase = ClickToMovePhase.UsingJoyStick;
                    this.noPathHere.X = this.noPathHere.Y = -1;
                }

                Vector2 mousePosition = new Vector2(mouseX + viewportX, mouseY + viewportY);
                Vector2 playerOffsetPosition = Game1.player.OffsetPositionOnMap();

                float distanceToMouse = Vector2.Distance(playerOffsetPosition, mousePosition);
                if (distanceToMouse > Game1.smallestTileSize / Game1.options.zoomLevel)
                {
                    if (distanceToMouse > Game1.tileSize / 2)
                    {
                        float angleDegrees = (float)Math.Atan2(
                                                 mousePosition.Y - playerOffsetPosition.Y,
                                                 mousePosition.X - playerOffsetPosition.X) / ((float)Math.PI * 2)
                                             * 360;

                        this.walkDirectionToMouse = WalkDirection.GetWalkDirectionForAngle(angleDegrees);
                    }
                }
                else
                {
                    this.walkDirectionToMouse = WalkDirection.None;
                }

                this.ClickKeyStates.SetMovement(this.walkDirectionToMouse);

                if ((Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival) && !Game1.player.UsingTool
                    && !this.warping && !this.IgnoreWarps && this.gameLocation.WarpIfInRange(playerOffsetPosition))
                {
                    this.Reset();

                    this.warping = true;
                }
            }
        }

        public void OnClickRelease(int mouseX = 0, int mouseY = 0, int viewportX = 0, int viewportY = 0)
        {
            this.clickPressed = false;
            this.ClickHoldActive = false;

            if (this.justClosedActiveMenu)
            {
                this.justClosedActiveMenu = false;
            }
            else if (!ClickToMoveManager.OnScreenButtonClicked)
            {
                if (ClickToMoveHelper.InMiniGameWhereWeDontWantClicks())
                {
                    return;
                }

                if (!(Game1.player.CurrentTool is FishingRod || Game1.player.CurrentTool is Slingshot))
                {
                    if (Game1.player.CanMove && Game1.player.UsingTool)
                    {
                        Farmer.canMoveNow(Game1.player);
                    }

                    this.ClickKeyStates.RealClickHeld = false;
                    this.ClickKeyStates.ActionButtonPressed = false;
                    this.ClickKeyStates.UseToolButtonReleased = true;
                }

                if (Game1.player.ActiveObject is Furniture && this.gameLocation is DecoratableLocation)
                {
                    Furniture furnitureClickedOn = this.gameLocation.GetFurniture(
                        mouseX + viewportX,
                        mouseY + viewportY);

                    if (furnitureClickedOn is not null)
                    {
                        furnitureClickedOn.performObjectDropInAction(Game1.player.ActiveObject, false, Game1.player);
                    }
                    else
                    {
                        this.phase = ClickToMovePhase.UseTool;
                    }
                }
                else if (this.pendingFurnitureAction)
                {
                    this.pendingFurnitureAction = false;

                    if (this.Furniture is not null && (this.Furniture.parentSheetIndex.Value == FurnitureId.Catalogue
                                                       || this.Furniture.parentSheetIndex.Value
                                                       == FurnitureId.FurnitureCatalogue
                                                       || this.Furniture.parentSheetIndex.Value == FurnitureId.Calendar
                                                       || this.Furniture.furniture_type.Value
                                                       == (int)FurnitureType.Fireplace
                                                       || this.Furniture is StorageFurniture || this.Furniture is TV))
                    {
                        this.phase = ClickToMovePhase.DoAction;
                        return;
                    }

                    this.ClickKeyStates.ActionButtonPressed = true;
                    this.phase = ClickToMovePhase.Complete;
                }
                else if (Game1.player.CurrentTool is not null && Game1.player.CurrentTool.upgradeLevel.Value > 0
                                                              && Game1.player.canReleaseTool
                                                              && Game1.player.CurrentTool is not FishingRod
                                                              && (this.phase == ClickToMovePhase.None
                                                                  || this.phase == ClickToMovePhase.PendingComplete
                                                                  || Game1.player.UsingTool))
                {
                    this.phase = ClickToMovePhase.UseTool;
                }
                else if (Game1.player.CurrentTool is Slingshot && Game1.player.usingSlingshot)
                {
                    this.phase = ClickToMovePhase.ReleaseTool;
                }
                else if (this.phase == ClickToMovePhase.PendingComplete || this.phase == ClickToMovePhase.UsingJoyStick)
                {
                    this.Reset();

                    this.CheckForQueuedReadyToHarvestClicks();
                }
            }
        }

        /// <summary>
        ///     Called after the player exits a menu. It saves that information internally so we can
        ///     disregard the mouse left button release.
        /// </summary>
        public void OnCloseActiveMenu()
        {
            this.justClosedActiveMenu = true;
        }

        /// <summary>
        ///     Clears the internal state of this instance.
        /// </summary>
        public void Reset(bool resetKeyStates = true)
        {
            this.phase = ClickToMovePhase.None;

            this.mouseX = -1;
            this.mouseY = -1;
            this.viewportX = -1;
            this.viewportY = -1;

            this.clickPoint = new Point(-1, -1);
            this.clickedTile = new Point(-1, -1);

            if (this.clickedNode is not null)
            {
                this.clickedNode.FakeTileClear = false;
            }

            this.clickedNode = null;

            this.stuckCount = 0;
            this.reallyStuckCount = 0;
            this.lastDistance = 0;
            this.distanceToTarget = DistanceToTarget.InRange;

            this.clickedCinemaDoor = false;
            this.clickedCinemaTicketBooth = false;
            this.endNodeOccupied = false;
            this.endNodeToBeActioned = false;
            this.endTileIsActionable = false;
            this.performActionFromNeighbourTile = false;
            this.warping = false;
            this.waterSourceAndFishingRodSelected = false;

            this.actionableBuilding = null;
            this.clickedOnHorse = null;
            this.crabPot = null;
            this.forageItem = null;
            this.forestLog = null;
            this.gateClickedOn = null;
            this.gateNode = null;
            this.TargetFarmAnimal = null;
            this.TargetNpc = null;

            if (resetKeyStates)
            {
                this.ClickKeyStates.Reset();
            }

            if (Game1.player.mount is not null)
            {
                Game1.player.mount.SetCheckActionEnabled(true);
            }
        }

        public void ResetRotatingFurniture()
        {
            this.rotatingFurniture = null;
        }

        /// <summary>
        ///     Changes the farmer's equipped tool to the last used tool.
        ///     This is used to get back to the tool that was equipped before a different tool was autoselected.
        /// </summary>
        public void SwitchBackToLastTool()
        {
            if (((this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile)
                  || this.forestLog is not null) && this.ClickKeyStates.RealClickHeld)
                || this.lastToolIndexList.Count == 0)
            {
                return;
            }

            int lastToolIndex = this.lastToolIndexList.Pop();

            if (this.lastToolIndexList.Count == 0)
            {
                Game1.player.CurrentToolIndex = lastToolIndex;

                if (Game1.player.CurrentTool is FishingRod or Slingshot)
                {
                    this.Reset();

                    ClickToMove.startTime = DateTime.Now.Ticks;
                }
            }
        }

        /// <summary>
        ///     Executes the action for this tick according to the current phase.
        /// </summary>
        public void Update()
        {
            this.ClickKeyStates.ClearReleasedStates();

            if (Game1.eventUp && !Game1.player.CanMove && !Game1.dialogueUp && this.phase != ClickToMovePhase.None
                && (Game1.currentSeason != "winter" || Game1.dayOfMonth != 8)
                && !(Game1.currentMinigame is FishingGame))
            {
                this.Reset();
            }
            else
            {
                switch (this.phase)
                {
                    case ClickToMovePhase.FollowingPath when Game1.player.CanMove:
                        this.FollowPathToNextNode();
                        break;
                    case ClickToMovePhase.OnFinalTile when Game1.player.CanMove:
                        this.MoveOnFinalTile();
                        break;
                    case ClickToMovePhase.ReachedEndOfPath:
                        this.StopMovingAfterReachingEndOfPath();
                        break;
                    case ClickToMovePhase.Complete:
                        this.OnClickToMoveComplete();
                        break;
                    case ClickToMovePhase.UseTool:
                        this.ClickKeyStates.SetUseTool(true);
                        this.phase = ClickToMovePhase.ReleaseTool;
                        break;
                    case ClickToMovePhase.ReleaseTool:
                        this.ClickKeyStates.SetUseTool(false);
                        this.phase = ClickToMovePhase.CheckForMoreClicks;
                        break;
                    case ClickToMovePhase.CheckForMoreClicks:
                        this.Reset();
                        this.CheckForQueuedReadyToHarvestClicks();
                        break;
                    case ClickToMovePhase.DoAction:
                        this.ClickKeyStates.ActionButtonPressed = true;
                        this.phase = ClickToMovePhase.FinishAction;
                        break;
                    case ClickToMovePhase.FinishAction:
                        this.ClickKeyStates.ActionButtonPressed = false;
                        this.phase = ClickToMovePhase.None;
                        break;
                    case ClickToMovePhase.AttackInNewDirection:
                        this.AttackInNewDirectionUpdate();
                        break;
                }
            }

            if (!this.CheckToAttackMonsters())
            {
                this.CheckToRetargetNPC();
                this.CheckToRetargetFarmAnimal();
                this.CheckToOpenClosedGate();
                this.CheckToWaterNextTile();
            }
        }

        private bool AddToClickQueue(int mouseX, int mouseY, int viewportX, int viewportY)
        {
            int tileX = (mouseX + viewportX) / Game1.tileSize;
            int tileY = (mouseY + viewportY) / Game1.tileSize;

            ClickQueueItem item = new ClickQueueItem(mouseX, mouseY, viewportX, viewportY, tileX, tileY);

            if (this.clickQueue.Contains(item))
            {
                return false;
            }

            this.clickQueue.Enqueue(new ClickQueueItem(mouseX, mouseY, viewportX, viewportY, tileX, tileY));
            return true;
        }

        private void AttackInNewDirectionUpdate()
        {
            if (Game1.player.CanMove && !Game1.player.UsingTool && Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter())
            {
                this.ClickKeyStates.SetMovement(WalkDirection.None);

                Game1.player.faceDirection(0);

                this.justUsedWeapon = true;

                this.ClickKeyStates.SetUseTool(true);

                this.phase = ClickToMovePhase.None;
            }
        }

        /// <summary>
        ///     Equips the farmer with the appropriate tool for the interaction at the end of the path.
        /// </summary>
        private void AutoSelectPendingTool()
        {
            if (this.toolToSelect is not null)
            {
                this.lastToolIndexList.Push(Game1.player.CurrentToolIndex);

                Game1.player.SelectTool(this.toolToSelect);

                this.toolToSelect = null;
            }
        }

        /// <summary>
        ///     Selects the tool to be used at the end of the path.
        /// </summary>
        /// <param name="toolName">The name of the tool to select.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the tool was found in the farmer's inventory;
        ///     returns <see langword="false"/>, otherwise.
        /// </returns>
        private bool AutoSelectTool(string toolName)
        {
            if (Game1.player.HasTool(toolName))
            {
                this.toolToSelect = toolName;

                return true;
            }

            return false;
        }

        private bool CanWallpaperReallyBePlacedHere(
            Wallpaper wallpaper,
            DecoratableLocation location,
            Point tileLocation)
        {
            int x = tileLocation.X;
            int y = tileLocation.Y;

            if (wallpaper.isFloor.Value)
            {
                List<Rectangle> floors = location.getFloors();
                for (int i = 0; i < floors.Count; i++)
                {
                    if (floors[i].Contains(x, y))
                    {
                        return true;
                    }
                }
            }
            else
            {
                List<Rectangle> walls = location.getWalls();
                for (int j = 0; j < walls.Count; j++)
                {
                    if (walls[j].Contains(x, y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void CheckForQueuedReadyToHarvestClicks()
        {
            this.clickedOnCrop = false;

            if (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
            {
                this.waitingToFinishWatering = true;
                this.clickedOnCrop = true;
                return;
            }

            if (this.clickQueue.Count > 0)
            {
                if (Game1.player.CurrentTool is WateringCan { WaterLeft: <= 0 })
                {
                    Game1.player.doEmote(4);
                    Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    this.clickQueue.Clear();
                    return;
                }

                ClickQueueItem clickQueueItem = this.clickQueue.Dequeue();

                this.OnClick(
                    clickQueueItem.MouseX,
                    clickQueueItem.MouseY,
                    clickQueueItem.ViewportX,
                    clickQueueItem.ViewportY);

                if (Game1.player.CurrentTool is WateringCan)
                {
                    this.OnClickRelease();
                }
            }
        }

        private bool CheckToAttackMonsters()
        {
            if (Game1.player.stamina <= 0f)
            {
                return false;
            }

            if (!this.enableCheckToAttackMonsters)
            {
                if (DateTime.Now.Ticks < ClickToMove.MinimumTicksBetweenMonsterChecks)
                {
                    return false;
                }

                this.enableCheckToAttackMonsters = true;
            }

            if (this.justUsedWeapon)
            {
                this.justUsedWeapon = false;
                this.ClickKeyStates.Reset();

                return false;
            }

            if (this.phase != ClickToMovePhase.FollowingPath && this.phase != ClickToMovePhase.OnFinalTile
                                                             && !Game1.player.UsingTool
                                                             && Game1.player.CurrentTool is not null
                                                             && Game1.player.CurrentTool.isHeavyHitter())
            {
                Rectangle boundingBox = Game1.player.GetBoundingBox();
                boundingBox.Inflate(Game1.tileSize, Game1.tileSize);

                Point playerPosition = Game1.player.GetBoundingBox().Center;

                this.monsterTarget = null;
                float minimumDistance = float.MaxValue;
                foreach (NPC character in this.gameLocation.characters)
                {
                    if (character is Monster monster)
                    {
                        Point monsterPosition = monster.GetBoundingBox().Center;
                        float distance = ClickToMoveHelper.Distance(monsterPosition, playerPosition);

                        if (distance < minimumDistance && boundingBox.Intersects(monster.GetBoundingBox())
                                                       && !this.IsObjectBlockingMonster(monster))
                        {
                            minimumDistance = distance;
                            this.monsterTarget = monster;
                        }
                    }
                }

                if (this.monsterTarget is not null)
                {
                    Point nearestMonsterPosition = this.monsterTarget.GetBoundingBox().Center;
                    WalkDirection walkDirection = WalkDirection.GetFacingWalkDirection(
                        playerPosition,
                        nearestMonsterPosition);

                    if (Game1.player.FacingDirection != walkDirection.Value)
                    {
                        Game1.player.faceDirection(walkDirection.Value);
                    }

                    if (this.monsterTarget is RockCrab rockCrab && rockCrab.IsHidingInShell()
                                                                && !(Game1.player.CurrentTool is Pickaxe))
                    {
                        Game1.player.SelectTool("Pickaxe");
                    }
                    else if (ClickToMove.LastMeleeWeapon is not null
                             && ClickToMove.LastMeleeWeapon != Game1.player.CurrentTool)
                    {
                        this.lastToolIndexList.Clear();

                        Game1.player.SelectTool(ClickToMove.LastMeleeWeapon.Name);
                    }

                    this.justUsedWeapon = true;

                    this.ClickKeyStates.SetUseTool(true);

                    this.noPathHere.X = this.noPathHere.Y = -1;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks whether the farmer can consume whatever they're holding.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the farmer can eat (or consume) the item they're holding.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CheckToEatFood(int clickPointX, int clickPointY)
        {
            if (Game1.player.ActiveObject is not null
                && (Game1.player.ActiveObject.Edibility != -300
                    || (Game1.player.ActiveObject.name.Length >= 11
                        && Game1.player.ActiveObject.name.Substring(0, 11) == "Secret Note"))
                && Game1.player.ClickedOn(clickPointX, clickPointY))
            {
                this.phase = ClickToMovePhase.DoAction;
                return true;
            }

            return false;
        }

        private void CheckToOpenClosedGate()
        {
            if (this.gateNode is not null && Vector2.Distance(
                    Game1.player.OffsetPositionOnMap(),
                    new Vector2(this.gateNode.NodeCenterOnMap.X, this.gateNode.NodeCenterOnMap.Y)) < 83.2f)
            {
                Fence fence = this.gateNode.GetGate();

                // Is the gate closed?
                if (fence is not null && fence.gatePosition.Value != 88)
                {
                    fence.checkForAction(Game1.player);
                    this.gateNode = null;
                }
            }
        }

        /// <summary>
        ///     If the targeted <see cref="FarmAnimal"/> is no longer at the clicked position,
        ///     recompute a new path to it.
        /// </summary>
        private void CheckToRetargetFarmAnimal()
        {
            if (this.TargetFarmAnimal is not null && this.clickedTile.X != -1
                                                  && (this.clickedTile.X != this.TargetFarmAnimal.getTileX()
                                                      || this.clickedTile.Y != this.TargetFarmAnimal.getTileY()))
            {
                this.OnClick(
                    (this.TargetFarmAnimal.getTileX() * Game1.tileSize) - Game1.viewport.X + (Game1.tileSize / 2),
                    (this.TargetFarmAnimal.getTileY() * Game1.tileSize) - Game1.viewport.Y + (Game1.tileSize / 2),
                    Game1.viewport.X,
                    Game1.viewport.Y);
            }
        }

        /// <summary>
        ///     If the targeted <see cref="NPC"/> is no longer at the clicked position,
        ///     recompute a new path to it.
        /// </summary>
        private void CheckToRetargetNPC()
        {
            if (this.TargetNpc is not null && (this.clickedTile.X != -1 || this.clickedTile.Y != -1))
            {
                if (this.TargetNpc.currentLocation != this.gameLocation)
                {
                    this.Reset();
                }
                else if (this.TargetNpc.AtWarpOrDoor(this.gameLocation))
                {
                    this.Reset();
                }
                else if (this.clickedTile.X != this.TargetNpc.getTileX()
                         || this.clickedTile.Y != this.TargetNpc.getTileY())
                {
                    this.OnClick(
                        (this.TargetNpc.getTileX() * Game1.tileSize) - Game1.viewport.X + (Game1.tileSize / 2),
                        (this.TargetNpc.getTileY() * Game1.tileSize) - Game1.viewport.Y + (Game1.tileSize / 2),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                }
            }
        }

        private void CheckToWaterNextTile()
        {
            if (this.waitingToFinishWatering && !Game1.player.UsingTool)
            {
                this.waitingToFinishWatering = false;
                this.clickedOnCrop = false;
                this.CheckForQueuedReadyToHarvestClicks();
            }
        }

        private bool ClickedOnAnotherQueueableCrop(AStarNode clickedNode)
        {
            if (clickedNode is not null)
            {
                this.gameLocation.terrainFeatures.TryGetValue(
                    new Vector2(clickedNode.X, clickedNode.Y),
                    out TerrainFeature terrainFeature);

                if (terrainFeature is HoeDirt hoeDirt)
                {
                    if (hoeDirt.state.Value != HoeDirt.watered && Game1.player.CurrentTool is WateringCan wateringCan)
                    {
                        if (wateringCan.WaterLeft > 0)
                        {
                            return true;
                        }

                        Game1.player.doEmote(4);
                        Game1.showRedMessage(
                            Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    }

                    if (hoeDirt.crop is not null && (Game1.player.CurrentTool is WateringCan { WaterLeft: > 0 }
                                                     || hoeDirt.crop.fullyGrown.Value))
                    {
                        return true;
                    }
                }

                if (this.mouseCursor == MouseCursor.ReadyForHarvest
                    || Utility.canGrabSomethingFromHere(clickedNode.X, clickedNode.Y, Game1.player)
                    || (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ClickedOnHoeDirtAndHoldingSeed(AStarNode clickedNode)
        {
            if (clickedNode is not null)
            {
                this.gameLocation.terrainFeatures.TryGetValue(
                    new Vector2(clickedNode.X, clickedNode.Y),
                    out TerrainFeature terrainFeature);
                if (terrainFeature is HoeDirt { crop: null } && Game1.player.ActiveObject is not null
                                                             && Game1.player.ActiveObject.Category
                                                             == SObject.SeedsCategory)
                {
                    return true;
                }
            }

            return false;
        }

        private void FaceTileClicked(bool faceClickPoint = false)
        {
            int facingDirection;

            if (faceClickPoint)
            {
                facingDirection = WalkDirection.GetFacingDirection(
                    Game1.player.position.Value,
                    Utility.PointToVector2(this.clickPoint));
            }
            else
            {
                facingDirection = WalkDirection.GetFacingDirection(
                    new Vector2(Game1.player.position.X / Game1.tileSize, Game1.player.position.Y / Game1.tileSize),
                    Utility.PointToVector2(this.clickedTile));
            }

            if (facingDirection != Game1.player.facingDirection.Value)
            {
                Game1.player.Halt();
                Game1.player.faceDirection(facingDirection);
            }
        }

        private bool FindAlternatePath(AStarNode start, int x, int y)
        {
            if (start is not null)
            {
                AStarNode node = this.Graph.GetNode(x, y);

                if (node?.TileClear == true)
                {
                    this.path = this.Graph.FindPath(start, node);

                    if (this.path is not null)
                    {
                        this.path.SmoothRightAngles();
                        this.phase = ClickToMovePhase.FollowingPath;

                        return true;
                    }
                }
            }

            return false;
        }

        private void FollowPathToNextNode()
        {
            if (this.path.Count > 0)
            {
                AStarNode farmerNode = this.Graph.FarmerNodeOffset;

                if (farmerNode is null)
                {
                    this.Reset();

                    return;
                }

                if (this.path[0] == farmerNode)
                {
                    this.path.RemoveAt(0);

                    this.lastDistance = 0;
                    this.stuckCount = 0;
                    this.reallyStuckCount = 0;
                }

                if (this.path.Count > 0)
                {
                    if (this.path[0].ContainsAnimal()
                        || (this.path[0].ContainsNpc() && !Game1.player.isRidingHorse()
                                                       && !(this.path[0].GetNpc() is Horse)))
                    {
                        this.OnClick(this.mouseX, this.mouseY, this.viewportX, this.viewportY);

                        return;
                    }

                    Vector2 nextNodeCenter = this.path[0].NodeCenterOnMap;
                    WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                        Game1.player.OffsetPositionOnMap(),
                        nextNodeCenter,
                        Game1.player.getMovementSpeed());

                    float distanceToNextNode = Vector2.Distance(Game1.player.OffsetPositionOnMap(), nextNodeCenter);

                    if (distanceToNextNode >= this.lastDistance)
                    {
                        this.stuckCount++;
                    }

                    this.lastDistance = distanceToNextNode;

                    if (distanceToNextNode < Game1.player.getMovementSpeed()
                        || this.stuckCount >= ClickToMove.MaxStuckCount)
                    {
                        if (this.reallyStuckCount >= ClickToMove.MaxReallyStuckCount)
                        {
                            walkDirection = WalkDirection.OppositeWalkDirection(walkDirection);

                            this.reallyStuckCount++;

                            if (this.reallyStuckCount == 8)
                            {
                                if (Game1.player.isRidingHorse())
                                {
                                    this.Reset();
                                }
                                else if (this.clickedOnHorse is not null)
                                {
                                    this.clickedOnHorse.checkAction(Game1.player, this.gameLocation);

                                    this.Reset();
                                }
                                else if (this.Graph.FarmerNodeOffset.GetNpc() is Horse horse)
                                {
                                    horse.checkAction(Game1.player, this.gameLocation);
                                }
                                else
                                {
                                    this.OnClick(
                                        this.mouseX,
                                        this.mouseY,
                                        this.viewportX,
                                        this.viewportY,
                                        this.tryCount + 1);
                                }

                                return;
                            }
                        }
                        else
                        {
                            WalkDirection walkDirection2 = farmerNode.WalkDirectionTo(this.path[0]);

                            if (walkDirection2 != walkDirection)
                            {
                                this.reallyStuckCount++;
                                walkDirection = walkDirection2;
                            }
                            else
                            {
                                walkDirection2 = WalkDirection.GetWalkDirection(
                                    Game1.player.OffsetPositionOnMap(),
                                    nextNodeCenter);

                                if (walkDirection2 != walkDirection)
                                {
                                    this.reallyStuckCount++;
                                    walkDirection = walkDirection2;
                                }
                            }

                            this.stuckCount = 0;
                        }
                    }

                    this.ClickKeyStates.SetMovement(walkDirection);
                }
            }

            if (this.path.Count == 0)
            {
                this.path = null;
                this.stuckCount = 0;
                this.phase = ClickToMovePhase.OnFinalTile;
            }
        }

        private NPC GetFestivalHost()
        {
            Event festival = Game1.CurrentEvent;

            if (ClickToMove.FestivalNames.Contains(festival.FestivalName))
            {
                return festival.getActorByName("Lewis");
            }

            return null;
        }

        private Rectangle GetHorseAlternativeBoundingBox(Horse horse)
        {
            if (horse.FacingDirection == WalkDirection.Up.Value || horse.FacingDirection == WalkDirection.Down.Value)
            {
                return new Rectangle((int)horse.Position.X, (int)horse.Position.Y - 128, 64, 192);
            }

            return new Rectangle((int)horse.Position.X - 32, (int)horse.Position.Y, 128, 64);
        }

        private bool HoldingWallpaperAndTileClickedIsWallOrFloor()
        {
            if (Game1.player.CurrentItem is not null && Game1.player.CurrentItem is Wallpaper wallpaper
                                                     && this.gameLocation is DecoratableLocation location)
            {
                return this.CanWallpaperReallyBePlacedHere(wallpaper, location, this.clickedTile);
            }

            return false;
        }

        private bool IsObjectBlockingMonster(Monster monster)
        {
            int tileX = Game1.player.getTileX();
            if (Math.Abs(tileX - monster.getTileX()) == 2)
            {
                tileX = tileX < monster.getTileX() ? tileX + 1 : tileX - 1;
            }

            int tileY = Game1.player.getTileY();
            if (Math.Abs(tileY - monster.getTileY()) == 2)
            {
                tileY = tileY <= monster.getTileY() ? tileY + 1 : tileY - 1;
            }

            this.gameLocation.objects.TryGetValue(new Vector2(tileX, tileY), out SObject value);
            if (value is not null && ((value.parentSheetIndex.Value >= 118 && value.parentSheetIndex.Value <= 125)
                                      || value.Name == "Stone" || value.Name == "Boulder"))
            {
                return true;
            }

            return this.Graph.GetNode(tileX, tileY)?.ContainsStumpOrBoulder() ?? false;
        }

        private void MoveOnFinalTile()
        {
            if (this.performActionFromNeighbourTile)
            {
                float distanceToGoal = Vector2.Distance(
                    Game1.player.OffsetPositionOnMap(),
                    this.clickedNode.NodeCenterOnMap);
                float deltaX = Math.Abs(this.clickedNode.NodeCenterOnMap.X - Game1.player.OffsetPositionOnMap().X)
                               - Game1.player.speed;
                float deltaY = Math.Abs(this.clickedNode.NodeCenterOnMap.Y - Game1.player.OffsetPositionOnMap().Y)
                               - Game1.player.speed;

                if (distanceToGoal == this.lastDistance)
                {
                    this.stuckCount++;
                }

                this.lastDistance = distanceToGoal;

                if (Game1.player.GetBoundingBox().Intersects(this.clickedNode.BoundingBox)
                    && this.distanceToTarget != DistanceToTarget.TooFar && this.crabPot is null)
                {
                    this.distanceToTarget = DistanceToTarget.TooClose;

                    WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                        this.ClickVector,
                        Game1.player.OffsetPositionOnMap());

                    this.ClickKeyStates.SetMovement(walkDirection);
                }
                else if (this.distanceToTarget != DistanceToTarget.TooClose
                         && this.stuckCount < ClickToMove.MaxStuckCount && Math.Max(deltaX, deltaY) > Game1.tileSize)
                {
                    this.distanceToTarget = DistanceToTarget.TooFar;

                    WalkDirection walkDirection = WalkDirection.GetWalkDirectionForAngle(
                        (float)(Math.Atan2(
                                    this.clickedNode.NodeCenterOnMap.Y - Game1.player.OffsetPositionOnMap().Y,
                                    this.clickedNode.NodeCenterOnMap.X - Game1.player.OffsetPositionOnMap().X)
                                / Math.PI / 2 * 360));

                    this.ClickKeyStates.SetMovement(walkDirection);
                }
                else
                {
                    this.distanceToTarget = DistanceToTarget.InRange;
                    this.OnReachEndOfPath();
                }
            }
            else
            {
                float distance = Vector2.Distance(
                    Game1.player.OffsetPositionOnMap(),
                    this.clickedNode.NodeCenterOnMap);

                if (distance >= this.lastDistance)
                {
                    this.stuckCount++;
                }

                this.lastDistance = distance;

                if (distance < Game1.player.getMovementSpeed() || this.stuckCount >= ClickToMove.MaxStuckCount
                                                               || (this.endNodeToBeActioned && distance < Game1.tileSize)
                                                               || (this.endNodeOccupied && distance < 66f))
                {
                    this.OnReachEndOfPath();
                    return;
                }

                WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                    Game1.player.OffsetPositionOnMap(),
                    this.finalNode.NodeCenterOnMap,
                    Game1.player.getMovementSpeed());

                this.ClickKeyStates.SetMovement(walkDirection);
            }
        }

        /// <summary>
        ///     Checks if a node is occupied by something.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the node is occupied by something.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool NodeBlocked(AStarNode node)
        {
            this.toolToSelect = null;

            if (this.gameLocation is Beach beach)
            {
                if (node.X == 53 && node.Y == 8 && Game1.CurrentEvent is not null && Game1.CurrentEvent.id == 13)
                {
                    this.clickedHaleyBracelet = true;
                    this.endNodeToBeActioned = true;

                    return true;
                }

                if (node.X == 57 && node.Y == 13 && !beach.bridgeFixed)
                {
                    this.endTileIsActionable = true;

                    return false;
                }

                if (this.Graph.OldMariner is not null && node.X == this.Graph.OldMariner.getTileX()
                                                      && (node.Y == this.Graph.OldMariner.getTileY()
                                                          || node.Y == this.Graph.OldMariner.getTileY() - 1))
                {
                    if (node.Y == this.Graph.OldMariner.getTileY() - 1)
                    {
                        this.SelectDifferentEndNode(node.X, node.Y + 1);
                    }

                    this.performActionFromNeighbourTile = true;

                    return true;
                }
            }

            if (ClickToMoveHelper.ClickedEggAtEggFestival(this.clickPoint))
            {
                bool tileClear = node.TileClear;

                this.endNodeToBeActioned = true;
                this.performActionFromNeighbourTile = !tileClear;

                return !tileClear;
            }

            if (node.ContainsCinemaTicketOffice())
            {
                this.SelectDifferentEndNode(node.Y, 20);

                this.endTileIsActionable = true;
                this.performActionFromNeighbourTile = true;
                this.clickedCinemaTicketBooth = true;

                return true;
            }

            if (node.ContainsCinemaDoor())
            {
                this.SelectDifferentEndNode(node.X, 19);

                this.endTileIsActionable = true;
                this.clickedCinemaDoor = true;

                return true;
            }

            if (this.gameLocation is CommunityCenter && node.X == 14 && node.Y == 5)
            {
                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (this.gameLocation.terrainFeatures.TryGetValue(
                    new Vector2(node.X, node.Y),
                    out TerrainFeature terrainFeature) && terrainFeature is HoeDirt dirt)
            {
                if (Game1.player.CurrentTool is WateringCan { WaterLeft: <= 0 })
                {
                    Game1.player.doEmote(4);
                    Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                }

                if (dirt.state.Value != 1 && Game1.player.CurrentTool is WateringCan { WaterLeft: > 0 })
                {
                    this.clickedOnCrop = true;
                }

                Crop crop = dirt.crop;
                if (crop is not null)
                {
                    if (crop.dead.Value)
                    {
                        if (!(Game1.player.CurrentTool is Hoe))
                        {
                            this.AutoSelectTool("Scythe");
                        }

                        this.endNodeToBeActioned = true;

                        return true;
                    }

                    if ((dirt.state.Value != 1 && Game1.player.CurrentTool is WateringCan { WaterLeft: > 0 })
                        || crop.IsReadyToHarvest())
                    {
                        this.clickedOnCrop = true;
                    }
                    else if (Game1.player.CurrentTool is Pickaxe)
                    {
                        this.endNodeToBeActioned = true;
                        this.performActionFromNeighbourTile = true;

                        return true;
                    }
                }
                else
                {
                    if (Game1.player.CurrentTool is Pickaxe)
                    {
                        this.endNodeToBeActioned = true;

                        return true;
                    }

                    if (Game1.player.ActiveObject is not null
                        && Game1.player.ActiveObject.Category == SObject.SeedsCategory)
                    {
                        this.clickedOnCrop = true;
                    }
                }
            }

            if (this.gameLocation is FarmHouse { upgradeLevel: 2 } && this.clickedNode.X == 16 && this.clickedNode.Y == 4)
            {
                this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);

                this.performActionFromNeighbourTile = true;

                return true;
            }

            Furniture furniture = node.GetFurnitureIgnoreRugs();
            if (furniture is not null)
            {
                if (furniture.furniture_type.Value == (int)FurnitureType.Fireplace)
                {
                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                if (furniture.furniture_type.Value == (int)FurnitureType.Lamp)
                {
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = false;

                    return true;
                }

                if (furniture is TV)
                {
                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                if (furniture is StorageFurniture)
                {
                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                if (furniture.parentSheetIndex.Value == FurnitureId.FurnitureCatalogue
                    || furniture.parentSheetIndex.Value == FurnitureId.Catalogue)
                {
                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                if (furniture.parentSheetIndex.Value == FurnitureId.SingingStone)
                {
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = false;

                    furniture.PlaySingingStone();

                    this.clickedTile.X = this.clickedTile.Y = -1;

                    return true;
                }
            }

            if (Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter() && !(Game1.player.CurrentTool is MeleeWeapon))
            {
                if (node.ContainsFence())
                {
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = true;

                    return true;
                }

                Chest chest = node.GetChest();
                if (chest is not null)
                {
                    if (chest.CountNonNullItems() == 0)
                    {
                        this.performActionFromNeighbourTile = true;
                        this.endNodeToBeActioned = true;
                    }
                    else
                    {
                        this.performActionFromNeighbourTile = true;
                    }

                    return true;
                }
            }

            if (this.gameLocation.ContainsTravellingCart(this.clickPoint.X, this.clickPoint.Y))
            {
                if (this.clickedNode.Y != 11 || (this.clickedNode.X != 23 && this.clickedNode.X != 24))
                {
                    this.SelectDifferentEndNode(27, 11);
                }

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (this.gameLocation.ContainsTravellingDesertShop(this.clickPoint.X, this.clickPoint.Y)
                && (this.clickedNode.Y == 23 || this.clickedNode.Y == 24))
            {
                this.performActionFromNeighbourTile = true;

                switch (this.clickedNode.X)
                {
                    case >= 34 and <= 38:
                        this.SelectDifferentEndNode(this.clickedNode.X, 24);
                        break;
                    case 40:
                    case 41:
                        this.SelectDifferentEndNode(41, 24);
                        break;
                    case 42:
                    case 43:
                        this.SelectDifferentEndNode(42, 24);
                        break;
                }

                return true;
            }

            if (this.gameLocation is Forest forest)
            {
                if (forest.log is not null && forest.log.getBoundingBox(forest.log.tile).Contains(
                        this.clickedNode.X * Game1.tileSize,
                        this.clickedNode.Y * Game1.tileSize))
                {
                    this.forestLog = forest.log;
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = true;

                    this.AutoSelectTool("Axe");

                    return true;
                }
            }

            if (node.GetObjectParentSheetIndex() == ObjectId.ArtifactSpot)
            {
                this.AutoSelectTool("Hoe");

                return true;
            }

            if (this.gameLocation is Farm farm && node.X == farm.petBowlPosition.X
                                                   && node.Y == farm.petBowlPosition.Y)
            {
                this.AutoSelectTool("Watering Can");

                this.endNodeToBeActioned = true;

                return true;
            }

            if (this.gameLocation is SlimeHutch && node.X == 16 && node.Y >= 6 && node.Y <= 9)
            {
                this.AutoSelectTool("Watering Can");

                this.endNodeToBeActioned = true;

                return true;
            }

            NPC npc = node.GetNpc();
            if (npc is Horse horse)
            {
                this.clickedOnHorse = horse;

                if (!(Game1.player.CurrentItem is Hat))
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (this.mouseCursor == MouseCursor.ReadyForHarvest || Utility.canGrabSomethingFromHere(
                    this.mouseX + this.viewportX,
                    this.mouseY + this.viewportY,
                    Game1.player))
            {
                this.clickedOnCrop = true;

                this.forageItem = this.gameLocation.getObjectAt(
                    this.mouseX + this.viewportX,
                    this.mouseY + this.viewportY);

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (this.gameLocation is FarmHouse farmHouse)
            {
                Point bedSpot = farmHouse.getBedSpot();

                if (bedSpot.X == node.X && bedSpot.Y == node.Y)
                {
                    this.endNodeToBeActioned = false;
                    this.performActionFromNeighbourTile = false;

                    return false;
                }
            }

            npc = this.gameLocation.isCharacterAtTile(new Vector2(this.clickedTile.X, this.clickedTile.Y));
            if (npc is not null)
            {
                this.performActionFromNeighbourTile = true;

                this.TargetNpc = npc;

                if (npc is Horse horse2)
                {
                    this.clickedOnHorse = horse2;

                    if (!(Game1.player.CurrentItem is Hat))
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (this.gameLocation is MineShaft && Game1.player.CurrentTool is not null
                                                       && Game1.player.CurrentTool is Pickaxe)
                {
                    this.endNodeToBeActioned = true;
                }

                return true;
            }

            npc = this.gameLocation.isCharacterAtTile(new Vector2(this.clickedTile.X, this.clickedTile.Y + 1f));

            if (npc is not null && !(npc is Duggy) && !(npc is Grub) && !(npc is LavaCrab) && !(npc is MetalHead)
                && !(npc is RockCrab) && !(npc is GreenSlime))
            {
                this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);

                this.performActionFromNeighbourTile = true;
                this.TargetNpc = npc;

                if (npc is Horse horse3)
                {
                    this.clickedOnHorse = horse3;

                    if (!(Game1.player.CurrentItem is Hat))
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (this.gameLocation is MineShaft && Game1.player.CurrentTool is not null
                                                       && Game1.player.CurrentTool is Pickaxe)
                {
                    this.endNodeToBeActioned = true;
                }

                return true;
            }

            if (this.gameLocation is Farm && node.Y == 13 && (node.X == 71 || node.X == 72)
                && this.clickedOnHorse is null)
            {
                this.SelectDifferentEndNode(node.X, node.Y + 1);

                this.endTileIsActionable = true;

                return true;
            }

            this.TargetFarmAnimal = this.gameLocation.GetFarmAnimal(this.clickPoint.X, this.clickPoint.Y);

            if (this.TargetFarmAnimal is not null)
            {
                if (this.TargetFarmAnimal.getTileX() != this.clickedNode.X
                    || this.TargetFarmAnimal.getTileY() != this.clickedNode.Y)
                {
                    this.SelectDifferentEndNode(this.TargetFarmAnimal.getTileX(), this.TargetFarmAnimal.getTileY());
                }

                if ((this.TargetFarmAnimal.type.Value.Contains("Cow")
                     || this.TargetFarmAnimal.type.Value.Contains("Goat")) && Game1.player.CurrentTool is MilkPail)
                {
                    return true;
                }

                if (this.TargetFarmAnimal.type.Value.Contains("Sheep") && Game1.player.CurrentTool is Shears)
                {
                    return true;
                }

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (Game1.player.ActiveObject is not null
                && (!(Game1.player.ActiveObject is Wallpaper) || this.gameLocation is DecoratableLocation)
                && (Game1.player.ActiveObject.bigCraftable.Value
                    || ClickToMove.ActionableObjectIds.Contains(Game1.player.ActiveObject.parentSheetIndex.Value)
                    || (Game1.player.ActiveObject is Wallpaper
                        && Game1.player.ActiveObject.parentSheetIndex.Value <= 40)))
            {
                if (Game1.player.ActiveObject.ParentSheetIndex == ObjectId.MegaBomb)
                {
                    Building building = this.clickedNode.GetBuilding();

                    if (building is FishPond)
                    {
                        this.actionableBuilding = building;

                        Point nearestTile = this.Graph.GetNearestTileNextToBuilding(building);

                        this.SelectDifferentEndNode(nearestTile.X, nearestTile.Y);

                        this.performActionFromNeighbourTile = true;

                        return true;
                    }
                }

                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (this.gameLocation is Mountain && node.X == 29 && node.Y == 9)
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            CrabPot crabPot = this.clickedNode.GetCrabPot();

            if (crabPot is not null)
            {
                this.crabPot = crabPot;
                this.performActionFromNeighbourTile = true;

                AStarNode neighbour = node.GetNearestNodeToCrabPot();

                if (node != neighbour)
                {
                    this.clickedNode = neighbour;

                    return false;
                }

                return true;
            }

            if (!node.TileClear)
            {
                this.gameLocation.objects.TryGetValue(new Vector2(node.X, node.Y), out SObject nodeObject);

                if (nodeObject is not null)
                {
                    if (nodeObject.Category == SObject.BigCraftableCategory)
                    {
                        if (nodeObject.parentSheetIndex.Value == BigCraftableId.FeedHopper
                            || nodeObject.parentSheetIndex.Value == BigCraftableId.Incubator
                            || nodeObject.parentSheetIndex.Value == BigCraftableId.Cask)
                        {
                            if (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe)
                            {
                                this.endNodeToBeActioned = true;
                            }

                            this.performActionFromNeighbourTile = true;

                            return true;
                        }

                        if (nodeObject.parentSheetIndex.Value == BigCraftableId.Cask
                            || nodeObject.parentSheetIndex.Value == BigCraftableId.MiniFridge
                            || nodeObject.parentSheetIndex.Value == BigCraftableId.Workbench)
                        {
                            this.performActionFromNeighbourTile = true;

                            if (Game1.player.CurrentTool is not null
                                && Game1.player.CurrentTool.isHeavyHitter()
                                && !(Game1.player.CurrentTool is MeleeWeapon))
                            {
                                this.endNodeToBeActioned = true;
                            }

                            return true;
                        }

                        if (nodeObject.parentSheetIndex.Value >= BigCraftableId.Barrel
                            && nodeObject.parentSheetIndex.Value <= BigCraftableId.Crate3)
                        {
                            if (Game1.player.CurrentTool is null || !Game1.player.CurrentTool.isHeavyHitter())
                            {
                                this.AutoSelectTool("Pickaxe");
                            }

                            this.endNodeToBeActioned = true;

                            return true;
                        }

                        if (Game1.player.CurrentTool is not null
                            && (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe
                                                                || Game1.player.CurrentTool is Hoe))
                        {
                            this.endNodeToBeActioned = true;
                        }

                        if (nodeObject.Name.Contains("Chest"))
                        {
                            this.performActionFromNeighbourTile = true;
                        }

                        return true;
                    }

                    if (nodeObject.Name == "Stone" || nodeObject.Name == "Boulder")
                    {
                        this.AutoSelectTool("Pickaxe");

                        return true;
                    }

                    if (nodeObject.Name == "Weeds")
                    {
                        this.AutoSelectTool("Scythe");

                        return true;
                    }

                    if (nodeObject.Name == "Twig")
                    {
                        this.AutoSelectTool("Axe");

                        return true;
                    }

                    if (nodeObject.Name == "House Plant")
                    {
                        this.AutoSelectTool("Pickaxe");

                        this.endNodeToBeActioned = true;

                        return true;
                    }

                    if (nodeObject.parentSheetIndex.Value == ObjectId.DrumBlock
                        || nodeObject.parentSheetIndex.Value == ObjectId.FluteBlock)
                    {
                        if (Game1.player.CurrentTool is not null
                            && (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe
                                                                || Game1.player.CurrentTool is Hoe))
                        {
                            this.endNodeToBeActioned = true;
                        }

                        return true;
                    }
                }
                else
                {
                    if (node.ContainsStumpOrBoulder())
                    {
                        if (node.ContainsStumpOrHollowLog())
                        {
                            this.AutoSelectTool("Axe");
                        }
                        else
                        {
                            GiantCrop giantCrop = node.GetGiantCrop();

                            if (giantCrop is not null)
                            {
                                if (giantCrop.width.Value == 3 && giantCrop.height.Value == 3
                                                               && giantCrop.tile.X + 1 == node.X
                                                               && giantCrop.tile.Y + 1 == node.Y)
                                {
                                    Point point = ClickToMoveHelper.GetNextPointOut(
                                        this.Graph.FarmerNodeOffset.X,
                                        this.Graph.FarmerNodeOffset.Y,
                                        node.X,
                                        node.Y);

                                    this.SelectDifferentEndNode(point.X, point.Y);
                                }

                                this.AutoSelectTool("Axe");
                            }
                            else
                            {
                                this.AutoSelectTool("Pickaxe");
                            }
                        }

                        return true;
                    }

                    Building building = this.clickedNode.GetBuilding();

                    if (building is not null && building.buildingType.Value == "Shipping Bin")
                    {
                        this.performActionFromNeighbourTile = true;

                        return true;
                    }

                    if (building is not null && building.buildingType.Value == "Mill")
                    {
                        if (Game1.player.ActiveObject is not null
                            && (Game1.player.ActiveObject.parentSheetIndex == ObjectId.Beet
                                || Game1.player.ActiveObject.parentSheetIndex.Value == ObjectId.Wheat))
                        {
                            this.performActionFromNeighbourTile = true;
                            this.endNodeToBeActioned = true;

                            return true;
                        }

                        this.performActionFromNeighbourTile = true;

                        return true;
                    }

                    if (building is Barn barn)
                    {
                        int doorTileX = barn.tileX.Value + barn.animalDoor.X;
                        int doorTileY = barn.tileY.Value + barn.animalDoor.Y;

                        if ((this.clickedNode.X == doorTileX || this.clickedNode.X == doorTileX + 1)
                            && (this.clickedNode.Y == doorTileY || this.clickedNode.Y == doorTileY - 1))
                        {
                            if (this.clickedNode.Y == doorTileY - 1)
                            {
                                this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);
                            }

                            this.performActionFromNeighbourTile = true;

                            return true;
                        }
                    }
                    else if (building is FishPond fishPond
                             && !(Game1.player.CurrentTool is FishingRod || Game1.player.CurrentTool is WateringCan))
                    {
                        this.actionableBuilding = fishPond;

                        Point nearestTile = this.Graph.GetNearestTileNextToBuilding(fishPond);

                        this.SelectDifferentEndNode(nearestTile.X, nearestTile.Y);

                        this.performActionFromNeighbourTile = true;

                        return true;
                    }

                    AStarNode upNode = this.Graph.GetNode(this.clickedNode.X, this.clickedNode.Y - 1);
                    if (upNode?.GetFurnitureIgnoreRugs()?.parentSheetIndex.Value == FurnitureId.Calendar)
                    {
                        this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);

                        this.performActionFromNeighbourTile = true;

                        return true;
                    }
                }

                TerrainFeature someTree = node.GetTree();
                if (someTree is not null)
                {
                    if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.edibility.Value > -300)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }

                    if (someTree is Tree tree)
                    {
                        this.AutoSelectTool(tree.growthStage.Value <= 1 ? "Scythe" : "Axe");
                    }

                    return true;
                }

                if (node.ContainsStump())
                {
                    this.AutoSelectTool("Axe");

                    return true;
                }

                if (node.ContainsBoulder())
                {
                    this.AutoSelectTool("Pickaxe");

                    return true;
                }

                if (this.gameLocation is Town && node.X == 108 && node.Y == 41)
                {
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = true;
                    this.endTileIsActionable = true;

                    return true;
                }

                if (this.gameLocation is Town && node.X == 100 && node.Y == 66)
                {
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = true;

                    return true;
                }

                Bush bush = node.GetBush();
                if (bush is not null)
                {
                    if (Game1.player.CurrentTool is Axe && bush.IsDestroyable(
                            this.gameLocation,
                            this.clickedTile))
                    {
                        this.endNodeToBeActioned = true;
                        this.performActionFromNeighbourTile = true;

                        return true;
                    }

                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                if (this.gameLocation.IsOreAt(this.clickedTile) && this.AutoSelectTool("Copper Pan"))
                {
                    AStarNode nearestNode = this.Graph.GetNodeNearestWaterSource(this.clickedNode);

                    if (nearestNode is not null)
                    {
                        this.clickedNode = nearestNode;
                        this.clickedTile = new Point(nearestNode.X, nearestNode.Y);

                        Vector2 nodeCenter = nearestNode.NodeCenterOnMap;
                        this.clickPoint = new Point((int)nodeCenter.X, (int)nodeCenter.Y);

                        this.endNodeToBeActioned = true;

                        return true;
                    }
                }

                if (Game1.isActionAtCurrentCursorTile && Game1.player.CurrentTool is FishingRod)
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                if (this.gameLocation.IsWateringCanFillingSource(this.clickedTile)
                    && (Game1.player.CurrentTool is WateringCan || Game1.player.CurrentTool is FishingRod))
                {
                    if (Game1.player.CurrentTool is FishingRod && this.gameLocation is Town
                                                               && this.clickedNode.X >= 50 && this.clickedNode.X <= 53
                                                               && this.clickedNode.Y >= 103
                                                               && this.clickedNode.Y <= 105)
                    {
                        this.clickedNode = this.Graph.GetNode(52, this.clickedNode.Y);
                    }
                    else
                    {
                        AStarNode landNode = this.Graph.GetNearestLandNodePerpendicularToWaterSource(this.clickedNode);

                        float fishingDistance = 2.5f;

                        if (Game1.player.CurrentTool is FishingRod)
                        {
                            fishingDistance += Game1.player.GetFishingAddedDistance();
                        }

                        if (landNode is not null && Game1.player.CurrentTool is FishingRod && Vector2.Distance(
                                Game1.player.OffsetPositionOnMap(),
                                landNode.NodeCenterOnMap) < Game1.tileSize * fishingDistance)
                        {
                            this.FaceTileClicked();

                            this.clickedNode = this.startNode;
                        }
                        else if (landNode is not null)
                        {
                            this.clickedNode = landNode;
                        }
                    }

                    this.clickedTile = new Point(this.clickedNode.X, this.clickedNode.Y);

                    this.waterSourceAndFishingRodSelected = true;
                    this.endNodeToBeActioned = true;
                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                if (this.gameLocation.IsWizardBuilding(
                    new Vector2(this.clickedTile.X, this.clickedTile.Y)))
                {
                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                this.endTileIsActionable =
                    this.gameLocation.isActionableTile(this.clickedNode.X, this.clickedNode.Y, Game1.player)
                    || this.gameLocation.isActionableTile(this.clickedNode.X, this.clickedNode.Y + 1, Game1.player);

                if (!this.endTileIsActionable)
                {
                    Tile tile = this.gameLocation.map.GetLayer("Buildings").PickTile(
                        new Location(this.clickedTile.X * Game1.tileSize, this.clickedTile.Y * Game1.tileSize),
                        Game1.viewport.Size);

                    this.endTileIsActionable = tile is not null;
                }

                return true;
            }

            this.gameLocation.terrainFeatures.TryGetValue(
                new Vector2(node.X, node.Y),
                out TerrainFeature terrainFeature2);

            if (terrainFeature2 is not null)
            {
                if (terrainFeature2 is Grass && Game1.player.CurrentTool is not null
                                             && Game1.player.CurrentTool is MeleeWeapon meleeWeapon
                                             && meleeWeapon.type.Value != MeleeWeapon.club)
                {
                    this.endNodeToBeActioned = true;

                    return true;
                }

                if (terrainFeature2 is Flooring && Game1.player.CurrentTool is not null
                                                && (Game1.player.CurrentTool is Pickaxe
                                                    || Game1.player.CurrentTool is Axe))
                {
                    this.endNodeToBeActioned = true;

                    return true;
                }
            }

            this.gameLocation.objects.TryGetValue(new Vector2(node.X, node.Y), out SObject @object);

            if (@object is not null
                && (@object.parentSheetIndex.Value == ObjectId.Torch
                    || @object.parentSheetIndex.Value == ObjectId.SpiritTorch) && Game1.player.CurrentTool is not null
                && (Game1.player.CurrentTool is Pickaxe || Game1.player.CurrentTool is Axe))
            {
                this.endNodeToBeActioned = true;

                return true;
            }

            if (Game1.player.CurrentTool is FishingRod && this.gameLocation is Town && this.clickedNode.X >= 50
                && this.clickedNode.X <= 53 && this.clickedNode.Y >= 103 && this.clickedNode.Y <= 105)
            {
                this.waterSourceAndFishingRodSelected = true;

                this.clickedNode = this.Graph.GetNode(52, this.clickedNode.Y);
                this.clickedTile = new Point(this.clickedNode.X, this.clickedNode.Y);

                this.endNodeToBeActioned = true;

                return true;
            }

            if (Game1.player.ActiveObject is not null
                && Game1.player.ActiveObject.canBePlacedHere(
                    this.gameLocation,
                    new Vector2(this.clickedTile.X, this.clickedTile.Y))
                && (Game1.player.ActiveObject.bigCraftable || Game1.player.ActiveObject.parentSheetIndex.Value == 104
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == ObjectId.WarpTotemFarm
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == ObjectId.WarpTotemMountains
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == ObjectId.WarpTotemBeach
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == ObjectId.RainTotem
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == ObjectId.WarpTotemDesert
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == 161
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == 155
                                                           || Game1.player.ActiveObject.parentSheetIndex.Value == 162
                                                           || Game1.player.ActiveObject.name.Contains("Sapling")))
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.player.mount is null)
            {
                this.endNodeToBeActioned = ClickToMoveHelper.HoeSelectedAndTileHoeable(this.gameLocation, this.clickedTile);

                if (this.endNodeToBeActioned)
                {
                    this.performActionFromNeighbourTile = true;
                }
            }

            if (!this.endNodeToBeActioned)
            {
                this.endNodeToBeActioned = this.WateringCanActionAtEndNode();
            }

            if (!this.endNodeToBeActioned && Game1.player.ActiveObject is not null
                                          && Game1.player.ActiveObject is not null)
            {
                this.endNodeToBeActioned = Game1.player.ActiveObject.isPlaceable()
                                           && Game1.player.ActiveObject.canBePlacedHere(
                                               this.gameLocation,
                                               new Vector2(this.clickedTile.X, this.clickedTile.Y));

                Crop crop = new Crop(Game1.player.ActiveObject.parentSheetIndex.Value, node.X, node.Y);
                if (crop is not null && (Game1.player.ActiveObject.parentSheetIndex == ObjectId.BasicFertilizer
                                         || Game1.player.ActiveObject.parentSheetIndex.Value == ObjectId.QualityFertilizer))
                {
                    this.endNodeToBeActioned = true;
                }

                if (crop is not null && crop.raisedSeeds.Value)
                {
                    this.performActionFromNeighbourTile = true;
                }
            }

            if (node.ContainsTree() && (Game1.player.CurrentTool is Hoe || Game1.player.CurrentTool is Axe
                                                                        || Game1.player.CurrentTool is Pickaxe))
            {
                this.endNodeToBeActioned = true;
            }

            if (this.mouseCursor == MouseCursor.Hand || this.mouseCursor == MouseCursor.MagnifyingGlass
                                                     || this.mouseCursor == MouseCursor.SpeechBubble)
            {
                AStarNode gateNode = this.Graph.GetNode(this.clickedNode.X, this.clickedNode.Y + 1);

                Fence gate = this.clickedNode.GetGate();

                if (gate is not null)
                {
                    this.gateClickedOn = gate;

                    // Gate is open.
                    if (gate.gatePosition.Value == 88)
                    {
                        this.gateClickedOn = null;
                    }

                    this.performActionFromNeighbourTile = true;
                }
                else if (!this.clickedNode.ContainsScarecrow() && gateNode.ContainsScarecrow()
                                                               && Game1.player.CurrentTool is not null)
                {
                    this.endTileIsActionable = true;
                    this.endNodeToBeActioned = true;
                    this.performActionFromNeighbourTile = true;
                }
                else
                {
                    this.endTileIsActionable = true;
                }
            }

            if (node.GetWarp(this.IgnoreWarps) is not null)
            {
                this.endNodeToBeActioned = false;

                return false;
            }

            if (!this.endNodeToBeActioned)
            {
                AStarNode shippingBinNode = this.Graph.GetNode(node.X, node.Y + 1);

                Building building = shippingBinNode?.GetBuilding();

                if (building is not null && building.buildingType.Value == "Shipping Bin")
                {
                    this.SelectDifferentEndNode(node.X, node.Y + 1);

                    this.performActionFromNeighbourTile = true;

                    return true;
                }
            }

            return this.endNodeToBeActioned;
        }

        private void OnClickToMoveComplete()
        {
            if ((Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival)
                && !Game1.player.UsingTool
                && this.clickedOnHorse is null
                && !this.warping
                && !this.IgnoreWarps
                && this.gameLocation.WarpIfInRange(this.ClickVector))
            {
                this.warping = true;
            }

            this.Reset();

            this.CheckForQueuedReadyToHarvestClicks();
        }

        private void OnReachEndOfPath()
        {
            this.AutoSelectPendingTool();

            if (this.endNodeOccupied)
            {
                WalkDirection walkDirection;
                if (this.endNodeToBeActioned)
                {
                    if (Game1.currentMinigame is FishingGame)
                    {
                        walkDirection = WalkDirection.GetFacingWalkDirection(
                            Game1.player.OffsetPositionOnMap(),
                            new Vector2(this.clickPoint.X, this.ClickPoint.Y));
                        this.FaceTileClicked();
                    }
                    else
                    {
                        walkDirection = WalkDirection.GetWalkDirection(
                            Game1.player.OffsetPositionOnMap(),
                            this.ClickVector);

                        if (Game1.player.CurrentTool is not null
                            && (Game1.player.CurrentTool is FishingRod
                                || (Game1.player.CurrentTool is WateringCan && this.waterSourceAndFishingRodSelected)))
                        {
                            Game1.player.faceDirection(
                                WalkDirection.GetFacingDirection(
                                    new Vector2(this.clickPoint.X, this.ClickPoint.Y),
                                    Game1.player.OffsetPositionOnMap()));
                        }
                    }
                }
                else
                {
                    walkDirection = this.Graph.FarmerNode.WalkDirectionTo(this.clickedNode);
                    if (walkDirection == WalkDirection.None)
                    {
                        walkDirection = WalkDirection.GetWalkDirection(
                            Game1.player.OffsetPositionOnMap(),
                            this.clickedNode.NodeCenterOnMap,
                            Game1.tileSize / 4);
                    }
                }

                if (walkDirection == WalkDirection.None)
                {
                    walkDirection = this.ClickKeyStates.LastWalkDirection;
                }

                this.ClickKeyStates.SetMovement(walkDirection);

                if (this.endNodeToBeActioned || !this.PerformAction())
                {
                    if (Game1.player.CurrentTool is WateringCan)
                    {
                        this.FaceTileClicked(true);
                    }

                    if (!(Game1.player.CurrentTool is FishingRod) || this.waterSourceAndFishingRodSelected)
                    {
                        this.GrabTile = this.clickedTile;
                        if (!this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile)
                            && this.forestLog is null)
                        {
                            this.clickedTile.X = -1;
                            this.clickedTile.Y = -1;
                        }

                        if (this.clickedHaleyBracelet && Game1.CurrentEvent is not null)
                        {
                            Game1.CurrentEvent.receiveActionPress(53, 8);
                            this.clickedHaleyBracelet = false;
                        }
                        else
                        {
                            this.ClickKeyStates.SetUseTool(true);
                        }
                    }
                }
            }
            else if (this.endNodeToBeActioned)
            {
                this.ClickKeyStates.SetUseTool(true);
            }
            else if (!this.PerformAction())
            {
                this.ClickKeyStates.SetMovement(false, false, false, false);
            }

            this.phase = ClickToMovePhase.ReachedEndOfPath;
        }

        private bool PerformAction()
        {
            if (this.PerformCrabPotAction())
            {
                return true;
            }

            if (this.actionableBuilding is not null)
            {
                this.actionableBuilding.doAction(
                    new Vector2(this.actionableBuilding.tileX, this.actionableBuilding.tileY),
                    Game1.player);
                return true;
            }

            if (this.clickedCinemaTicketBooth)
            {
                this.clickedCinemaTicketBooth = false;
                this.gameLocation.checkAction(new Location(55, 20), Game1.viewport, Game1.player);
                return true;
            }

            if (this.clickedCinemaDoor)
            {
                this.clickedCinemaDoor = false;
                this.gameLocation.checkAction(new Location(53, 19), Game1.viewport, Game1.player);
                return true;
            }

            if ((this.endTileIsActionable || this.performActionFromNeighbourTile) && Game1.player.mount is not null
                && this.forageItem is not null)
            {
                this.gameLocation.checkAction(
                    new Location(this.clickedNode.X, this.clickedNode.Y),
                    Game1.viewport,
                    Game1.player);
                this.forageItem = null;
                return true;
            }

            if (this.clickedOnHorse is not null)
            {
                this.clickedOnHorse.checkAction(Game1.player, this.gameLocation);

                this.Reset();

                return false;
            }

            if (Game1.player.mount is not null && this.clickedOnHorse is null)
            {
                Game1.player.mount.SetCheckActionEnabled(false);
            }

            if (this.ClickKeyStates.RealClickHeld && this.Furniture is not null && this.forageItem is null)
            {
                this.pendingFurnitureAction = true;
                return true;
            }

            if (this.mouseCursor == MouseCursor.Hand && this.gameLocation.name.Value == "Blacksmith"
                                                     && this.clickedTile.X == 3
                                                     && (this.clickedTile.Y == 12 || this.clickedTile.Y == 13
                                                         || this.clickedTile.Y == 14))
            {
                this.gameLocation.performAction("Blacksmith", Game1.player, new Location(3, 14));

                Game1.player.Halt();

                return false;
            }

            if (this.gameLocation.isActionableTile(this.clickedTile.X, this.clickedTile.Y, Game1.player)
                && !this.clickedNode.ContainsGate())
            {
                if (this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile)
                    || this.forestLog is not null)
                {
                    if (this.ClickHoldActive)
                    {
                        return false;
                    }

                    this.SwitchBackToLastTool();
                }

                Game1.player.Halt();

                this.ClickKeyStates.ActionButtonPressed = true;

                return true;
            }

            if (this.endNodeOccupied && !this.endTileIsActionable && !this.performActionFromNeighbourTile)
            {
                return this.Furniture is not null;
            }

            if (this.gameLocation is Farm && this.gameLocation.isActionableTile(
                    this.clickedTile.X,
                    this.clickedTile.Y + 1,
                    Game1.player))
            {
                this.ClickKeyStates.SetMovement(WalkDirection.Down);
                this.ClickKeyStates.ActionButtonPressed = true;
                return true;
            }

            if (this.TargetNpc is Child)
            {
                this.TargetNpc.checkAction(Game1.player, this.gameLocation);

                this.Reset();
                return false;
            }

            if (this.endTileIsActionable || this.performActionFromNeighbourTile)
            {
                this.gateNode = null;
                if (this.gateClickedOn is not null)
                {
                    this.gateClickedOn = null;
                    return false;
                }

                this.FaceTileClicked();
                Game1.player.Halt();
                this.ClickKeyStates.ActionButtonPressed = true;
                return true;
            }

            Game1.player.Halt();
            return false;
        }

        private bool PerformCrabPotAction()
        {
            if (this.crabPot is not null)
            {
                if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.Category == SObject.baitCategory)
                {
                    if (this.crabPot.performObjectDropInAction(Game1.player.ActiveObject, false, Game1.player))
                    {
                        Game1.player.reduceActiveItemByOne();
                    }
                }
                else if (!this.crabPot.checkForAction(Game1.player))
                {
                    this.crabPot.performRemoveAction(Utility.PointToVector2(this.clickedTile), this.gameLocation);
                }

                this.crabPot = null;
                return true;
            }

            return false;
        }

        private void SelectDifferentEndNode(int x, int y)
        {
            AStarNode node = this.Graph.GetNode(x, y);
            if (node is not null)
            {
                this.clickedNode = node;
                this.clickedTile.X = x;
                this.clickedTile.Y = y;
                this.clickPoint.X = (x * Game1.tileSize) + (Game1.tileSize / 2);
                this.clickPoint.Y = (y * Game1.tileSize) + (Game1.tileSize / 2);
                this.mouseX = this.clickPoint.X - this.viewportX;
                this.mouseY = this.clickPoint.Y - this.viewportY;
            }
        }

        private void SetMouseCursor(AStarNode node)
        {
            this.mouseCursor = MouseCursor.None;

            if (node is null)
            {
                return;
            }

            if (this.gameLocation.isActionableTile(node.X, node.Y, Game1.player)
                || this.gameLocation.isActionableTile(node.X, node.Y + 1, Game1.player))
            {
                this.mouseCursor = MouseCursor.Hand;
            }

            string action = this.gameLocation.doesTileHaveProperty(node.X, node.Y, "Action", "Buildings");
            if (action is not null && action.Contains("Message"))
            {
                this.mouseCursor = MouseCursor.MagnifyingGlass;
            }

            Vector2 nodeTile = new Vector2(node.X, node.Y);

            NPC npc = this.gameLocation.isCharacterAtTile(nodeTile);
            if (npc is not null && !npc.IsMonster)
            {
                if (!Game1.eventUp && Game1.player.ActiveObject is not null
                                   && Game1.player.ActiveObject.canBeGivenAsGift()
                                   && Game1.player.friendshipData.ContainsKey(npc.Name)
                                   && Game1.player.friendshipData[npc.Name].GiftsToday != 1)
                {
                    this.mouseCursor = MouseCursor.Gift;
                }
                else if (npc.canTalk() && npc.CurrentDialogue is not null
                                       && (npc.CurrentDialogue.Count > 0 || npc.hasTemporaryMessageAvailable())
                                       && !npc.isOnSilentTemporaryMessage())
                {
                    this.mouseCursor = MouseCursor.SpeechBubble;
                }
            }

            if (Game1.CurrentEvent is not null && Game1.CurrentEvent.isFestival)
            {
                NPC festivalHost = this.GetFestivalHost();
                if (festivalHost is not null && festivalHost.getTileLocation().Equals(nodeTile))
                {
                    this.mouseCursor = MouseCursor.SpeechBubble;
                }
            }

            if (Game1.player.IsLocalPlayer)
            {
                if (this.gameLocation.Objects.ContainsKey(nodeTile))
                {
                    if (this.gameLocation.Objects[nodeTile].readyForHarvest.Value
                        || (this.gameLocation.Objects[nodeTile].Name.Contains("Table")
                            && this.gameLocation.Objects[nodeTile].heldObject.Value is not null)
                        || this.gameLocation.Objects[nodeTile].isSpawnedObject.Value
                        || (this.gameLocation.Objects[nodeTile] is IndoorPot indoorPot
                            && indoorPot.hoeDirt.Value.readyForHarvest()))
                    {
                        this.mouseCursor = MouseCursor.ReadyForHarvest;
                    }
                }
                else if (this.gameLocation.terrainFeatures.ContainsKey(nodeTile)
                         && this.gameLocation.terrainFeatures[nodeTile] is HoeDirt dirt && dirt.readyForHarvest())
                {
                    this.mouseCursor = MouseCursor.ReadyForHarvest;
                }
            }

            if (Game1.player.usingSlingshot)
            {
                this.mouseCursor = MouseCursor.UsingSlingshot;
            }
        }

        private void StopMovingAfterReachingEndOfPath()
        {
            this.ClickKeyStates.SetMovement(WalkDirection.None);

            this.ClickKeyStates.ActionButtonPressed = false;

            if ((this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile)
                 || this.forestLog is not null) && this.ClickKeyStates.RealClickHeld
                                                && (Game1.player.CurrentTool is Axe
                                                    || Game1.player.CurrentTool is Pickaxe))
            {
                this.ClickKeyStates.SetUseTool(true);

                this.phase = ClickToMovePhase.PendingComplete;

                if (!this.clickPressed)
                {
                    this.OnClickRelease();
                }
            }
            else if (Game1.player.UsingTool
                     && (Game1.player.CurrentTool is WateringCan || Game1.player.CurrentTool is Hoe)
                     && this.ClickKeyStates.RealClickHeld)
            {
                this.ClickKeyStates.SetUseTool(true);

                this.phase = ClickToMovePhase.PendingComplete;

                if (!this.clickPressed)
                {
                    this.OnClickRelease();
                }
            }
            else
            {
                this.ClickKeyStates.SetUseTool(false);
                this.phase = ClickToMovePhase.Complete;
            }
        }

        private void TryToFindAlternatePath(AStarNode startNode)
        {
            if (!this.endNodeOccupied
                || (!this.FindAlternatePath(startNode, this.clickedNode.X + 1, this.clickedNode.Y + 1)
                    && !this.FindAlternatePath(startNode, this.clickedNode.X - 1, this.clickedNode.Y + 1)
                    && !this.FindAlternatePath(startNode, this.clickedNode.X + 1, this.clickedNode.Y - 1)
                    && !this.FindAlternatePath(startNode, this.clickedNode.X - 1, this.clickedNode.Y - 1)))
            {
                this.Reset();
            }
        }

        private bool WateringCanActionAtEndNode()
        {
            if (Game1.player.CurrentTool is WateringCan wateringCan)
            {
                if (wateringCan.WaterLeft > 0)
                {
                    this.gameLocation.terrainFeatures.TryGetValue(
                        new Vector2(this.clickedNode.X, this.clickedNode.Y),
                        out TerrainFeature terrainFeature);

                    if (terrainFeature is HoeDirt dirt && dirt.state.Value != 1)
                    {
                        return true;
                    }
                }

                if (this.gameLocation is SlimeHutch && this.clickedNode.X == 16 && this.clickedNode.Y >= 6
                    && this.clickedNode.Y <= 9)
                {
                    return true;
                }

                if (this.gameLocation.IsWateringCanFillingSource(this.clickedTile))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
