// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="PathFindingController.cs">
//     Copyright (c) 2021 Raquellcesar
//
//     Use of this source code is governed by an MIT-style license
//     that can be found in the LICENSE file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

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
    ///     This classes encapsulates all the details needed to implement the click to move
    ///     functionality. Each instance will be associated to a single <see cref="GameLocation"/>
    ///     and will maintain data to optimize path finding in that location.
    /// </summary>
    internal class PathFindingController
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
        private const int MinimumTicksBeforeClickHold = 3500000;

        /// <summary>
        ///     The time we need to wait before checking gor monsters to attack again (500 ms).
        /// </summary>
        private const int MinimumTicksBetweenMonsterChecks = 5000000;

        /// <summary>
        ///     All actionable objects.
        /// </summary>
        private static readonly List<int> ActionableObjectIDs = new List<int>(new int[42]
                                                {
            286, 287, 288, 298, 322, 323, 324, 325, 599, 621,
            645, 405, 407, 409, 411, 415, 309, 310, 311, 313,
            314, 315, 316, 317, 318, 319, 320, 321, 328, 329,
            331, 401, 93, 94, 294, 295, 297, 461, 463, 464,
            746, 326
                                                });

        /// <summary>
        ///     The time of the last click.
        /// </summary>
        private static long startTime = long.MaxValue;

        private readonly Queue<ClickQueueItem> clickQueue = new Queue<ClickQueueItem>();

        /// <summary>
        ///     The <see cref="GameLocation"/> associated to this object.
        /// </summary>
        private readonly GameLocation gameLocation;

        /// <summary>
        ///     The graph used for path finding.
        /// </summary>
        private readonly AStarGraph graph;

        private readonly IReflectedField<bool> ignoreWarps;

        /// <summary>
        ///     The list of the indexes of the last used tools.
        /// </summary>
        private readonly List<int> lastToolIndexList = new List<int>();

        /// <summary>
        ///     Simplifies access to private game code.
        /// </summary>
        private readonly IReflectionHelper reflection;

        private Building actionableBuilding;

        private bool clickedCinemaDoor;

        private bool clickedCinemaTicketBooth;

        private bool clickedHaleyBracelet;

        /// <summary>
        ///     The <see cref="Horse"/> clicked at the end of the path.
        /// </summary>
        private Horse clickedHorse;

        private bool clickedOnCrop;

        private Vector2 clickedTile = new Vector2(-1, -1);

        public bool ClickHoldActive { get; private set; }

        public void ResetRotatingFurniture()
        {
            this.rotatingFurniture = null;
        }

        /// <summary>
        ///
        /// </summary>
        private Vector2 clickPoint = new Vector2(-1, -1);

        private bool clickPressed;

        private CrabPot crabPot;

        private DistanceToTarget distanceToTarget;

        /// <summary>
        ///     If true, the farmer will check for monsters to attack.
        /// </summary>
        private bool enableCheckToAttackMonsters = true;

        private AStarNode endNodeOccupied;

        /// <summary>
        ///     Whether the end node in the path is to be actioned when reached.
        /// </summary>
        private bool endNodeToBeActioned;

        private bool endTileIsActionable;

        private AStarNode finalNode;

        private SObject forageItem;

        private ResourceClump forestLog;

        private Furniture furniture;

        public Furniture Furniture => this.furniture;

        private Fence gateClickedOn;

        private AStarNode gateNode;

        /// <summary>
        ///     Whether the player has just closed a menu.
        /// </summary>
        private bool justClosedActiveMenu;

        /// <summary>
        ///     Whether the farmer has used a weapon in the last tick.
        /// </summary>
        private bool justUsedWeapon;

        /// <summary>
        ///     The last distance to the target.
        /// </summary>
        private float lastDistance = float.MaxValue;

        private Monster monsterTarget;

        private MouseCursor mouseCursor = MouseCursor.None;

        /// <summary>
        ///     The clicked point x coordinate.
        /// </summary>
        private int mouseX;

        /// <summary>
        ///     The clicked point y coordinate.
        /// </summary>
        private int mouseY;

        private Vector2 noPathHere = new Vector2(-1, -1);

        /// <summary>
        ///     Contains the path last computed by the A* algorithm.
        /// </summary>
        private AStarPath path;

        private bool pendingFurnitureAction;

        private bool performActionFromNeighbourTile;

        /// <summary>
        ///     The current phase of this <see cref="PathFindingController"/>.
        /// </summary>
        private PathFindingPhase phase;

        private int reallyStuckCount;

        private Furniture rotatingFurniture;

        private AStarNode startNode;

        /// <summary>
        ///     Number of times the farmer couldn't progress in the path.
        /// </summary>
        private int stuckCount;

        /// <summary>
        ///     The name of the tool to select at the end of the path.
        /// </summary>
        private string toolToSelect;

        /// <summary>
        ///     The number of attempts at computing a path to a clicked destination.
        /// </summary>
        private int tryCount;

        /// <summary>
        ///     The viewport x coordinate on the last player interaction.
        /// </summary>
        private int viewportX;

        /// <summary>
        ///     The viewport y coordinate on the last player interaction.
        /// </summary>
        private int viewportY;

        private bool waitingToFinishWatering;

        private WalkDirection walkDirectionFarmerToMouse = WalkDirection.None;

        private bool warping;

        private bool waterSourceAndFishingRodSelected;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PathFindingController"/> class.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> associated to this instance.</param>
        /// <param name="reflection">Simplifies access to private game code.</param>
        public PathFindingController(GameLocation gameLocation, IReflectionHelper reflection)
        {
            this.gameLocation = gameLocation;
            this.reflection = reflection;

            this.ignoreWarps = this.reflection.GetField<bool>(gameLocation, "ignoreWarps");

            this.graph = new AStarGraph(this.gameLocation, this.reflection);
        }

        /// <summary>
        ///     Gets or sets the last melee weapon equipped by the player.
        /// </summary>
        public static MeleeWeapon MostRecentlyChosenMeleeWeapon { get; set; }

        /// <summary>
        ///     Gets a value indicating whether this <see cref="PathFindingController"/> is active.
        /// </summary>
        public bool Active => this.phase != PathFindingPhase.None;

        /// <summary>
        ///     Gets the node associated with the tile clicked by the player.
        /// </summary>
        public AStarNode ClickedNode { get; private set; }

        public Vector2 ClickedTile => this.clickedTile;

        public Vector2 ClickPoint => this.clickPoint;

        public Vector2 GrabTile { get; set; } = Vector2.Zero;

        public bool IgnoreWarps => this.ignoreWarps.GetValue();

        /// <summary>
        ///     Gets the relevant key states for this thick.
        /// </summary>
        public ClickToMoveKeyStates KeyStates { get; private set; } = new ClickToMoveKeyStates();

        public Vector2 NoPathHere => this.noPathHere;

        /// <summary>
        ///     Gets or sets a value indicating whether the farmer should be prevented from mounting
        ///     an horse.
        /// </summary>
        public bool PreventMountingHorse { get; set; }

        /// <summary>
        ///     Gets the <see cref="FarmAnimal"/> that's at the current goal node, if any.
        /// </summary>
        public FarmAnimal TargetFarmAnimal { get; private set; }

        /// <summary>
        ///     Gets the <see cref="NPC"/> that's at the current goal node, if any.
        /// </summary>
        public NPC TargetNpc { get; private set; }

        public static Point GetNextPointOut(int startX, int startY, int endX, int endY)
        {
            Point nextPoint = new Point(endX, endY);

            if (startX < endX)
            {
                nextPoint.X--;
            }
            else if (startX > endX)
            {
                nextPoint.X++;
            }

            if (startY < endY)
            {
                nextPoint.Y--;
            }
            else if (startY > endY)
            {
                nextPoint.Y++;
            }

            return nextPoint;
        }

        /// <summary>
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
            this.graph.Init();
        }

        /// <summary>
        ///     Called if the mouse left button is pressed by the player.
        /// </summary>
        /// <param name="mouseX">The mouse x coordinate.</param>
        /// <param name="mouseY">The mouse y coordinate.</param>
        /// <param name="viewportX">The viewport x coordinate.</param>
        /// <param name="viewportY">The viewport y coordinate.</param>
        /// <param name="tryCount">The number of attempts at computing a path until now.</param>
        public void OnClick(int mouseX, int mouseY, int viewportX, int viewportY, int tryCount = 0)
        {
            if (Game1.player.passedOut || Game1.player.FarmerSprite.isPassingOut() || Game1.player.isEating || Game1.player.IsBeingSick())
            {
                return;
            }

            if (Game1.locationRequest is not null)
            {
                return;
            }

            this.clickPressed = true;

            PathFindingController.startTime = DateTime.Now.Ticks;

            Vector2 point = new Vector2(mouseX + viewportX, mouseY + viewportY);
            AStarNode clickedNode = this.graph.GetNode((int)(point.X / Game1.tileSize), (int)(point.Y / Game1.tileSize));

            this.SetMouseCursor(clickedNode);

            if (this.clickedOnCrop)
            {
                if (this.TappedOnAnotherQueableCrop((int)point.X, (int)point.Y))
                {
                    if (this.AddToClickQueue(mouseX, mouseY, viewportX, viewportY) && Game1.player.CurrentTool is WateringCan && (bool)Game1.player.usingTool && this.phase == PathFindingPhase.None)
                    {
                        this.waitingToFinishWatering = true;
                    }

                    return;
                }

                if (this.TappedOnHoeDirtAndHoldingSeed((int)point.X, (int)point.Y))
                {
                    this.AddToClickQueue(mouseX, mouseY, viewportX, viewportY);
                }
                else
                {
                    this.clickedOnCrop = false;
                    this.clickQueue.Clear();
                }
            }

            if (Game1.CurrentEvent is not null && ((Game1.CurrentEvent.id == 0 && Game1.CurrentEvent.FestivalName == string.Empty) || !Game1.CurrentEvent.playerControlSequence))
            {
                return;
            }

            if (!Game1.player.CanMove && Game1.player.UsingTool && Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter())
            {
                Game1.player.Halt();

                this.enableCheckToAttackMonsters = false;
                this.justUsedWeapon = false;
                this.KeyStates.SetUseTool(false);
            }

            if (Game1.dialogueUp
                || (Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not AnimalQueryMenu and not CarpenterMenu and not PurchaseAnimalsMenu and not MuseumMenu)
                || (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
                || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks
                || (Game1.player.ActiveObject is not null && Game1.player.ActiveObject is Furniture && Game1.currentLocation is DecoratableLocation))
            {
                return;
            }

            if (Game1.currentMinigame is null && Game1.CurrentEvent is not null && Game1.CurrentEvent.isFestival && Game1.CurrentEvent.FestivalName == "Stardew Valley Fair")
            {
                Game1.player.CurrentToolIndex = -1;
            }

            if (!Game1.player.CanMove && (Game1.eventUp || (Game1.currentLocation is FarmHouse && Game1.dialogueUp)))
            {
                if (Game1.dialogueUp)
                {
                    this.Reset();
                    this.phase = PathFindingPhase.DoAction;
                }
                else if ((Game1.currentSeason == "winter" && Game1.dayOfMonth == 8) || Game1.currentMinigame is FishingGame)
                {
                    this.phase = PathFindingPhase.UseTool;
                }

                if (!(Game1.player.CurrentTool is FishingRod))
                {
                    return;
                }
            }

            if (this.ClickedOnFarmer(point))
            {
                if (Game1.player.CurrentTool is Slingshot && (Game1.currentMinigame is null || !(Game1.currentMinigame is TargetGame)))
                {
                    this.KeyStates.SetUseTool(true);
                    this.KeyStates.RealClickHeld = true;
                    this.phase = PathFindingPhase.ClickHeld;
                    return;
                }
                else if (Game1.player.CurrentTool is Wand)
                {
                    this.phase = PathFindingPhase.UseTool;
                    return;
                }
                else if (this.CheckToEatFood())
                {
                    return;
                }
            }

            if (!Game1.player.CanMove && Game1.player.CurrentTool is FishingRod)
            {
                if (Game1.currentMinigame is FishingGame && !Game1.player.UsingTool)
                {
                    Game1.player.CanMove = true;
                }

                this.phase = PathFindingPhase.UseTool;
                return;
            }

            if (tryCount >= 2)
            {
                this.Reset();
                Game1.player.Halt();
                return;
            }

            this.Reset(resetKeyStates: false);
            this.noPathHere.X = this.noPathHere.Y = -1;

            this.KeyStates.ClearClickButtons();

            this.mouseX = mouseX;
            this.mouseY = mouseY;
            this.viewportX = viewportX;
            this.viewportY = viewportY;
            this.tryCount = tryCount;

            this.KeyStates.RealClickHeld = true;
            this.clickPoint = new Vector2(point.X, point.Y);
            this.clickedTile = new Vector2(point.X / Game1.tileSize, point.Y / Game1.tileSize);

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

            if (this.clickedTile.X == 37 && this.clickedTile.Y == 79 && this.gameLocation is Town && Game1.CurrentEvent is not null && Game1.CurrentEvent.FestivalName == "Stardew Valley Fair")
            {
                this.clickedTile.Y = 80;
            }

            if (Game1.player.isRidingHorse() && (Game1.player.mount.GetAlternativeBoundingBox().Contains((int)point.X, (int)point.Y) || this.ClickedOnFarmer(point)) && Game1.player.mount.checkAction(Game1.player, this.gameLocation))
            {
                this.Reset();
                return;
            }

            if (this.HoldingWallpaperAndTileClickedIsWallOrFloor())
            {
                this.KeyStates.ActionButtonPressed = true;
                return;
            }

            if (Game1.mailbox.Count > 0 && Game1.player.ActiveObject is null && Game1.currentLocation is Farm && this.clickedTile.X == 68f && this.clickedTile.Y == 14f)
            {
                this.OnClick(mouseX, mouseY, viewportX, viewportY + Game1.tileSize, tryCount);
                return;
            }

            if (this.mouseCursor == MouseCursor.MagnifyingGlass)
            {
                if (!ClickToMoveHelper.ClickedEggAtEggFestival(this.clickPoint))
                {
                    if (!Game1.currentLocation.checkAction(new Location((int)this.clickedTile.X, (int)this.clickedTile.Y), Game1.viewport, Game1.player))
                    {
                        Game1.currentLocation.checkAction(new Location((int)this.clickedTile.X, (int)this.clickedTile.Y + 1), Game1.viewport, Game1.player);
                    }

                    this.Reset();
                    Game1.player.Halt();
                    return;
                }
            }
            else if (Game1.currentLocation is Town && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
            {
                if (this.clickedTile.X >= 48f && this.clickedTile.X <= 51f && (this.clickedTile.Y == 18f || this.clickedTile.Y == 19f))
                {
                    Game1.currentLocation.checkAction(new Location((int)this.clickedTile.X, 19), Game1.viewport, Game1.player);
                    this.Reset();
                    return;
                }
            }
            else if (Game1.currentLocation is Beach && !((Beach)Game1.currentLocation).bridgeFixed && (this.clickedTile.X == 58 || this.clickedTile.X == 59) && (this.clickedTile.Y == 11 || this.clickedTile.Y == 12))
            {
                Game1.currentLocation.checkAction(new Location(58, 13), Game1.viewport, Game1.player);
            }
            else if (Game1.currentLocation is LibraryMuseum && ((int)this.clickedTile.X != 3 || (int)this.clickedTile.Y != 9))
            {
                if (((LibraryMuseum)Game1.currentLocation).museumPieces.ContainsKey(new Vector2((int)this.clickedTile.X, (int)this.clickedTile.Y)))
                {
                    if (Game1.currentLocation.checkAction(new Location((int)this.clickedTile.X, (int)this.clickedTile.Y), Game1.viewport, Game1.player))
                    {
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (((LibraryMuseum)Game1.currentLocation).doesTileHaveProperty((int)this.clickedTile.X, (int)this.clickedTile.Y + i, "Action", "Buildings") is not null && ((LibraryMuseum)Game1.currentLocation).doesTileHaveProperty((int)this.clickedTile.X, (int)this.clickedTile.Y + i, "Action", "Buildings").Contains("Notes") && Game1.currentLocation.checkAction(new Location((int)this.clickedTile.X, (int)this.clickedTile.Y + i), Game1.viewport, Game1.player))
                        {
                            return;
                        }
                    }
                }
            }

            if (!this.gameLocation.isTileOnMap((int)this.clickedTile.X, (int)this.clickedTile.Y))
            {
                this.Reset();
                return;
            }

            this.startNode = this.finalNode = this.graph.FarmerNodeOffset;
            this.ClickedNode = this.graph.GetNode((int)this.clickedTile.X, (int)this.clickedTile.Y);
            if (this.ClickedNode is null)
            {
                this.Reset();
                return;
            }

            if (this.gameLocation.IsWater((int)this.clickedTile.X, (int)this.clickedTile.Y)
                && this.mouseCursor != MouseCursor.Hand
                && this.mouseCursor != MouseCursor.ReadyForHarvest
                && !(Game1.player.CurrentTool is WateringCan)
                && !(Game1.player.CurrentTool is FishingRod)
                && (Game1.player.ActiveObject is null || Game1.player.ActiveObject.ParentSheetIndex != 710)
                && !this.ClickedNode.IsTilePassable()
                && !this.gameLocation.IsOreAt(this.clickedTile))
            {
                AStarNode aStarNode = this.ClickedNode.CrabPotNeighbour();
                if (aStarNode is null)
                {
                    this.Reset();
                    return;
                }

                this.crabPot = this.ClickedNode.GetObject() as CrabPot;
                this.ClickedNode = aStarNode;
                this.clickedTile.X = this.ClickedNode.X;
                this.clickedTile.Y = this.ClickedNode.Y;
            }

            if (this.startNode is null || this.ClickedNode is null)
            {
                return;
            }

            if (this.ClickedNode.ContainsFurniture())
            {
                Furniture furnitureClickedOn = this.gameLocation.GetFurnitureClickedOn((int)this.clickPoint.X, (int)this.clickPoint.Y);
                if (furnitureClickedOn is not null)
                {
                    if (this.rotatingFurniture == furnitureClickedOn && furnitureClickedOn.rotations.Value > 1)
                    {
                        this.rotatingFurniture.rotate();

                        this.Reset();
                        return;
                    }

                    if ((int)furnitureClickedOn.rotations > 1)
                    {
                        this.rotatingFurniture = furnitureClickedOn;
                    }
                }

                this.furniture = furnitureClickedOn;
                if (Game1.player.CurrentTool is FishingRod)
                {
                    Game1.player.CurrentToolIndex = -1;
                }
            }
            else
            {
                this.furniture = null;
                this.rotatingFurniture = null;
            }

            if (this.ClickedNode.ContainsSomeKindOfWarp() && Game1.player.CurrentTool is FishingRod)
            {
                Game1.player.CurrentToolIndex = -1;
            }

            if (this.EndNodeBlocked(this.ClickedNode))
            {
                this.endNodeOccupied = this.ClickedNode;
                this.ClickedNode.FakeTileClear = true;
            }
            else
            {
                this.endNodeOccupied = null;
            }

            if (this.clickedHorse is not null && Game1.player.CurrentItem is Hat)
            {
                this.clickedHorse.checkAction(Game1.player, Game1.currentLocation);

                this.Reset();

                return;
            }

            if (this.TargetNpc is not null && Game1.CurrentEvent is not null && Game1.CurrentEvent.playerControlSequenceID is not null && Game1.CurrentEvent.festivalTimer > 0 && Game1.CurrentEvent.playerControlSequenceID == "iceFishing")
            {
                this.Reset();

                return;
            }

            if (!Game1.player.isRidingHorse() && Game1.player.mount is null && !this.performActionFromNeighbourTile && !this.endNodeToBeActioned)
            {
                for (int j = 0; j < this.gameLocation.characters.Count; j++)
                {
                    if (this.gameLocation.characters[j] is Horse)
                    {
                        Horse horse = (Horse)this.gameLocation.characters[j];
                        if (Vector2.Distance(this.clickPoint, Utility.PointToVector2(horse.GetBoundingBox().Center)) < 48f && (this.clickedTile.X != horse.getTileLocation().X || this.clickedTile.Y != horse.getTileLocation().Y))
                        {
                            this.Reset();

                            this.OnClick(
                                (int)((horse.getTileLocation().X * Game1.tileSize) + (Game1.tileSize / 2) - viewportX),
                                (int)((horse.getTileLocation().Y * Game1.tileSize) + (Game1.tileSize / 2) - viewportY),
                                viewportX,
                                viewportY);

                            return;
                        }
                    }
                }
            }

            Building building;
            if (this.ClickedNode is not null
                && this.endNodeOccupied is not null
                && !this.endNodeToBeActioned
                && !this.performActionFromNeighbourTile
                && !this.endTileIsActionable
                && !this.ClickedNode.ContainsSomeKindOfWarp()
                && (building = this.ClickedNode.GetBuilding()) is not null)
            {
                if (building.buildingType.Value != "Mill")
                {
                    if (building is not null && building.buildingType.Value == "Silo")
                    {
                        building.doAction(new Vector2(this.ClickedNode.X, this.ClickedNode.Y), Game1.player);
                        return;
                    }

                    if (!this.ClickedNode.ContainsTree()
                        && this.actionableBuilding is null
                        && (!(Game1.currentLocation is Farm) || this.ClickedNode.X != 21 || this.ClickedNode.Y != 25 || Game1.whichFarm != 3))
                    {
                        this.Reset();
                        return;
                    }
                }
            }

            if (this.ClickedNode.ContainsCinema() && !this.clickedCinemaTicketBooth && !this.clickedCinemaDoor)
            {
                this.noPathHere = this.clickedTile;
                this.Reset();
                return;
            }

            if ((this.startNode.IsNeighbourNoDiagonals(this.ClickedNode) && this.endNodeOccupied is not null)
                || (this.startNode.IsNeighbour(this.ClickedNode)
                && this.endNodeOccupied is not null
                && Game1.player.CurrentTool is not null
                && (Game1.player.CurrentTool is WateringCan || Game1.player.CurrentTool is Hoe || Game1.player.CurrentTool is MeleeWeapon)))
            {
                this.phase = PathFindingPhase.OnFinalTile;
                return;
            }

            if (this.startNode.IsNeighbourNoDiagonals(this.ClickedNode) && this.endNodeOccupied is not null && this.performActionFromNeighbourTile)
            {
                this.phase = PathFindingPhase.OnFinalTile;
                return;
            }

            if (this.graph.GameLocation is AnimalHouse)
            {
                this.path = this.graph.FindPath(this.startNode, this.ClickedNode);
            }
            else
            {
                this.path = this.graph.FindPathWithBubbleCheck(this.startNode, this.ClickedNode);
            }

            if ((this.path is null || this.path.Count == 0) && this.endNodeOccupied is not null && this.performActionFromNeighbourTile)
            {
                this.path = this.graph.FindPathToNeighbourDiagonalWithBubbleCheck(this.startNode, this.ClickedNode);
                if (this.path is not null && this.path.Count > 0)
                {
                    this.ClickedNode.FakeTileClear = false;
                    this.ClickedNode = this.path.GetLast();
                    this.endNodeOccupied = null;
                    this.performActionFromNeighbourTile = false;
                }
            }

            if (this.path is not null && this.path.Count > 0)
            {
                this.gateNode = this.path.ContainsGate();
                if (this.endNodeOccupied is not null)
                {
                    this.path.SmoothRightAngles(2);
                }
                else
                {
                    this.path.SmoothRightAngles();
                }

                if (this.ClickedNode.FakeTileClear)
                {
                    if (this.path.Count > 0)
                    {
                        this.path.RemoveLast();
                    }

                    this.ClickedNode.FakeTileClear = false;
                }

                if (this.path.Count > 0)
                {
                    this.finalNode = this.path.GetLast();
                }

                this.phase = PathFindingPhase.FollowingPath;

                return;
            }

            if (this.startNode.IsSameNode(this.ClickedNode))
            {
                if (this.endNodeToBeActioned || this.performActionFromNeighbourTile)
                {
                    AStarNode aStarNode2 = this.startNode.GetNeighbourPassable();

                    if (aStarNode2 is null)
                    {
                        this.Reset();
                        return;
                    }

                    if (this.clickedOnCrop)
                    {
                        this.phase = PathFindingPhase.UseTool;
                        return;
                    }

                    if (this.waterSourceAndFishingRodSelected)
                    {
                        this.FaceTileClicked(faceClickPoint: true);
                        this.phase = PathFindingPhase.UseTool;
                        return;
                    }

                    this.path.Add(aStarNode2);
                    this.path.Add(this.startNode);

                    this.noPathHere.X = this.noPathHere.Y = -1;

                    this.finalNode = this.path.GetLast();

                    this.phase = PathFindingPhase.FollowingPath;

                    return;
                }

                this.noPathHere.X = this.noPathHere.Y = -1;
                if (this.ClickedNode.IsWarp() && (Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival))
                {
                    Game1.player.WarpIfInRange(this.gameLocation, this.clickPoint);
                }

                this.Reset();
                return;
            }

            if (this.startNode is not null && Game1.player.ActiveObject is not null && Game1.player.ActiveObject.name == "Crab Pot")
            {
                this.TryTofindAlternatePath(this.startNode);
                return;
            }

            if (this.crabPot is not null
                && Vector2.Distance(new Vector2((this.crabPot.TileLocation.X * Game1.tileSize) + (Game1.tileSize / 2), (this.crabPot.TileLocation.Y * Game1.tileSize) + (Game1.tileSize / 2)), Game1.player.Position) < 128f)
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

                this.OnClick(mouseX, mouseY, viewportX, viewportY, tryCount);

                return;
            }

            if (this.TargetNpc is not null && this.TargetNpc.Name == "Robin" && this.gameLocation is BuildableGameLocation)
            {
                this.TargetNpc.checkAction(Game1.player, Game1.currentLocation);
            }

            this.noPathHere = this.clickedTile;

            if (tryCount > 0)
            {
                this.noPathHere.Y -= 1;
            }

            this.Reset();
        }

        internal void RefreshGraphBubbles()
        {
            this.graph.RefreshBubbles();
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
            if (this.justClosedActiveMenu
                || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks
                || Game1.currentMinigame is FishingGame
                || DateTime.Now.Ticks - PathFindingController.startTime < PathFindingController.MinimumTicksBeforeClickHold)
            {
                return;
            }

            this.ClickHoldActive = true;

            if ((this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile) || this.forestLog is not null)
                && this.KeyStates.RealClickHeld
                && (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe)
                && this.phase != PathFindingPhase.FollowingPath
                && this.phase != PathFindingPhase.OnFinalTile
                && this.phase != PathFindingPhase.ReachedEndOfPath
                && this.phase != PathFindingPhase.Complete)
            {
                if (Game1.player.usingTool)
                {
                    this.phase = PathFindingPhase.None;
                    this.KeyStates.SetUseTool(false);
                    this.KeyStates.StopMoving();
                }
                else
                {
                    this.phase = PathFindingPhase.UseTool;
                }
            }
            else if (this.waterSourceAndFishingRodSelected && this.KeyStates.RealClickHeld && Game1.player.CurrentTool is FishingRod)
            {
                if (this.phase == PathFindingPhase.Complete)
                {
                    this.phase = PathFindingPhase.UseTool;
                }
            }
            else if ((Game1.player.CurrentItem is Furniture || Game1.player.ActiveObject is Furniture) && Game1.currentLocation is DecoratableLocation)
            {
                this.KeyStates.SetMovePressed(WalkDirection.None);
                this.phase = PathFindingPhase.None;
            }
            else if (this.furniture is not null && DateTime.Now.Ticks - PathFindingController.startTime > PathFindingController.MinimumTicksBeforeClickHold)
            {
                this.phase = PathFindingPhase.UseTool;
            }
            else
            {
                if (!Game1.player.canMove || this.warping || this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile) || this.forestLog is not null)
                {
                    return;
                }

                if (this.phase != 0 && this.phase != PathFindingPhase.UsingJoyStick)
                {
                    this.Reset();
                }

                if (this.phase != PathFindingPhase.UsingJoyStick)
                {
                    this.phase = PathFindingPhase.UsingJoyStick;
                    this.noPathHere.X = this.noPathHere.Y = -1;
                }

                Vector2 mousePosition = new Vector2(mouseX + viewportX, mouseY + viewportY);
                Vector2 playerOffsetPositionOnMap = Game1.player.OffsetPositionOnMap();

                float distanceToMouse = Vector2.Distance(playerOffsetPositionOnMap, mousePosition);
                float num3 = 16f / Game1.options.zoomLevel;
                if (distanceToMouse > num3)
                {
                    if (distanceToMouse > Game1.tileSize / 2)
                    {
                        float angleDegrees = (float)Math.Atan2(
                            mousePosition.Y - playerOffsetPositionOnMap.Y,
                            mousePosition.X - playerOffsetPositionOnMap.X) / ((float)Math.PI * 2) * 360;
                        this.walkDirectionFarmerToMouse = WalkDirection.GetWalkDirectionForAngle(angleDegrees);
                    }
                }
                else
                {
                    this.walkDirectionFarmerToMouse = WalkDirection.None;
                }

                this.KeyStates.SetMovePressed(this.walkDirectionFarmerToMouse);

                if ((Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival) && !Game1.player.usingTool && !this.warping && !this.IgnoreWarps
                    && Game1.player.WarpIfInRange(this.gameLocation, mousePosition))
                {
                    this.Reset();

                    this.warping = true;
                }
            }
        }

        /// <summary>
        ///     Called if the mouse left button is just released by the player.
        /// </summary>
        /// <param name="mouseX">The mouse x coordinate.</param>
        /// <param name="mouseY">The mouse y coordinate.</param>
        /// <param name="viewportX">The viewport x coordinate.</param>
        /// <param name="viewportY">The viewport y coordinate.</param>
        public void OnClickRelease(int mouseX = 0, int mouseY = 0, int viewportX = 0, int viewportY = 0)
        {
            this.clickPressed = false;
            this.ClickHoldActive = false;

            if (this.justClosedActiveMenu)
            {
                this.justClosedActiveMenu = false;
                return;
            }

            if (ClickToMoveHelper.InMiniGameWhereWeDontWantClicks)
            {
                return;
            }

            if (Game1.player.CurrentTool is not FishingRod and not Slingshot)
            {
                if (Game1.player.CanMove && Game1.player.UsingTool)
                {
                    Farmer.canMoveNow(Game1.player);
                }

                this.KeyStates.RealClickHeld = false;
                this.KeyStates.ActionButtonPressed = false;
                this.KeyStates.UseToolButtonReleased = true;
            }

            if (Game1.player.ActiveObject is Furniture && Game1.currentLocation is DecoratableLocation)
            {
                Furniture furnitureClickedOn = this.gameLocation.GetFurnitureClickedOn(mouseX + viewportX, mouseY + viewportY);

                if (furnitureClickedOn is not null)
                {
                    furnitureClickedOn.performObjectDropInAction(Game1.player.ActiveObject, probe: false, Game1.player);
                }
                else
                {
                    this.phase = PathFindingPhase.UseTool;
                }
            }
            else if (this.pendingFurnitureAction)
            {
                this.pendingFurnitureAction = false;
                if (this.furniture is not null && ((int)this.furniture.parentSheetIndex == 1308 || (int)this.furniture.parentSheetIndex == 1226 || (int)this.furniture.parentSheetIndex == 1402 || (int)this.furniture.furniture_type == 14 || this.furniture is StorageFurniture || this.furniture is TV))
                {
                    this.phase = PathFindingPhase.DoAction;
                    return;
                }

                this.KeyStates.ActionButtonPressed = true;

                this.phase = PathFindingPhase.Complete;
            }
            else if (Game1.player.CurrentTool is not null && (int)Game1.player.CurrentTool.upgradeLevel > 0 && Game1.player.canReleaseTool && !(Game1.player.CurrentTool is FishingRod) && (this.phase == PathFindingPhase.None || this.phase == PathFindingPhase.PendingComplete || (bool)Game1.player.usingTool))
            {
                this.phase = PathFindingPhase.UseTool;
            }
            else if (Game1.player.CurrentTool is Slingshot && Game1.player.usingSlingshot)
            {
                this.phase = PathFindingPhase.ReleaseTool;
            }
            else if (this.phase == PathFindingPhase.PendingComplete || this.phase == PathFindingPhase.UsingJoyStick)
            {
                this.Reset();
                this.CheckForQueuedReadyToHarvestTaps();
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
        ///     Resets the internal state of this instance.
        /// </summary>
        public void Reset(bool resetKeyStates = true)
        {
            this.mouseX = -1;
            this.mouseY = -1;
            this.viewportX = -1;
            this.viewportY = -1;

            this.justClosedActiveMenu = false;

            this.phase = PathFindingPhase.None;

            this.clickPoint = new Vector2(-1, -1);
            this.clickedTile = new Vector2(-1, -1);

            if (this.ClickedNode is not null)
            {
                this.ClickedNode.FakeTileClear = false;
            }

            this.ClickedNode = null;

            this.clickedCinemaDoor = false;
            this.clickedCinemaTicketBooth = false;

            this.endNodeToBeActioned = false;
            this.endTileIsActionable = false;
            this.performActionFromNeighbourTile = false;
            this.waterSourceAndFishingRodSelected = false;
            this.warping = false;

            this.actionableBuilding = null;
            this.clickedHorse = null;
            this.crabPot = null;
            this.endNodeOccupied = null;
            this.forageItem = null;
            this.forestLog = null;
            this.gateClickedOn = null;
            this.gateNode = null;
            this.TargetFarmAnimal = null;
            this.TargetNpc = null;

            this.stuckCount = 0;
            this.reallyStuckCount = 0;
            this.lastDistance = float.MaxValue;

            this.distanceToTarget = DistanceToTarget.InRange;

            if (Game1.player.mount is not null)
            {
                ClickToMovePatcher.SetHorseCheckActionEnabled(Game1.player.mount, true);
            }

            if (resetKeyStates)
            {
                this.KeyStates.Reset();
            }
        }

        public void SwitchBackToLastTool()
        {
            if (((this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile)
                  || this.forestLog is not null) && this.KeyStates.RealClickHeld)
                || this.lastToolIndexList.Count == 0)
            {
                return;
            }

            int lastToolIndex = this.lastToolIndexList[this.lastToolIndexList.Count - 1];
            this.lastToolIndexList.RemoveAt(this.lastToolIndexList.Count - 1);

            if (this.lastToolIndexList.Count == 0)
            {
                Game1.player.CurrentToolIndex = lastToolIndex;

                if (Game1.player.CurrentTool is FishingRod || Game1.player.CurrentTool is Slingshot)
                {
                    this.Reset();

                    PathFindingController.startTime = DateTime.Now.Ticks;
                }
            }
        }

        /// <summary>
        ///     Executes the action for this tick according to the current phase.
        /// </summary>
        public void Update()
        {
            this.KeyStates.ClearReleasedStates();

            if (Game1.eventUp && !Game1.player.CanMove && !Game1.dialogueUp && this.phase != 0 && !(Game1.currentSeason == "winter" && Game1.dayOfMonth == 8) && Game1.currentMinigame is not FishingGame)
            {
                this.Reset();
            }
            else if (this.phase == PathFindingPhase.FollowingPath && Game1.player.CanMove)
            {
                this.FollowPath();
            }
            else if (this.phase == PathFindingPhase.OnFinalTile && Game1.player.CanMove)
            {
                this.MoveOnFinalTile();
            }
            else if (this.phase == PathFindingPhase.ReachedEndOfPath)
            {
                this.StopMovingAfterReachingEndOfPath();
            }
            else if (this.phase == PathFindingPhase.Complete)
            {
                this.OnClickToMoveComplete();
            }
            else if (this.phase == PathFindingPhase.UseTool)
            {
                this.KeyStates.SetUseTool(useTool: true);
                this.phase = PathFindingPhase.FinishAction;
            }
            else if (this.phase == PathFindingPhase.ReleaseTool)
            {
                this.KeyStates.SetUseTool(useTool: false);
                this.phase = PathFindingPhase.CheckForQueuedClicks;
            }
            else if (this.phase == PathFindingPhase.CheckForQueuedClicks)
            {
                this.Reset();
                this.CheckForQueuedReadyToHarvestTaps();
            }
            else if (this.phase == PathFindingPhase.DoAction)
            {
                this.KeyStates.ActionButtonPressed = true;
                this.phase = PathFindingPhase.FinishAction;
            }
            else if (this.phase == PathFindingPhase.FinishAction)
            {
                this.KeyStates.ActionButtonPressed = false;
                this.phase = PathFindingPhase.None;
            }

            if (!this.CheckToAttackMonsters())
            {
                this.CheckToReTargetNpc();
                this.CheckToReTargetFarmAnimal();
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

        private void AutoSelectPendingTool()
        {
            if (this.toolToSelect != null)
            {
                this.lastToolIndexList.Add(Game1.player.CurrentToolIndex);

                Game1.player.SelectTool(this.toolToSelect);
                this.toolToSelect = null;
            }
        }

        private bool AutoSelectTool(string toolName)
        {
            if (Game1.player.HasTool(toolName))
            {
                this.toolToSelect = toolName;
                return true;
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

        private bool CheckForQueuedReadyToHarvestTaps()
        {
            this.clickedOnCrop = false;
            if (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
            {
                this.waitingToFinishWatering = true;
                this.clickedOnCrop = true;
                return false;
            }

            if (this.clickQueue.Count > 0)
            {
                if (Game1.player.CurrentTool is WateringCan && ((WateringCan)Game1.player.CurrentTool).WaterLeft <= 0)
                {
                    Game1.player.doEmote(4);
                    Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    this.clickQueue.Clear();
                    return false;
                }

                ClickQueueItem clickQueueItem = this.clickQueue.Dequeue();

                this.OnClick(clickQueueItem.MouseX, clickQueueItem.MouseY, clickQueueItem.ViewportX, clickQueueItem.ViewportY);

                if (Game1.player.CurrentTool is WateringCan)
                {
                    this.OnClickRelease();
                }

                return true;
            }

            return false;
        }

        private bool CheckToAttackMonsters()
        {
            if (Game1.player.stamina <= 0f)
            {
                return false;
            }

            if (!this.enableCheckToAttackMonsters)
            {
                if (DateTime.Now.Ticks < PathFindingController.MinimumTicksBetweenMonsterChecks)
                {
                    return false;
                }

                this.enableCheckToAttackMonsters = true;
            }

            if (this.justUsedWeapon)
            {
                this.justUsedWeapon = false;
                this.KeyStates.Reset();

                return false;
            }

            if (this.phase != PathFindingPhase.FollowingPath && this.phase != PathFindingPhase.OnFinalTile
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
                        nearestMonsterPosition,
                        playerPosition);

                    if (Game1.player.FacingDirection != walkDirection.Value)
                    {
                        Game1.player.faceDirection(walkDirection.Value);
                    }

                    if (this.monsterTarget is RockCrab rockCrab && rockCrab.IsHidingInShell()
                                                                && !(Game1.player.CurrentTool is Pickaxe))
                    {
                        Game1.player.SelectTool("Pickaxe");
                    }
                    else if (PathFindingController.MostRecentlyChosenMeleeWeapon is not null
                             && PathFindingController.MostRecentlyChosenMeleeWeapon != Game1.player.CurrentTool)
                    {
                        this.lastToolIndexList.Clear();

                        Game1.player.SelectTool(PathFindingController.MostRecentlyChosenMeleeWeapon.Name);
                    }

                    this.justUsedWeapon = true;

                    this.KeyStates.SetUseTool(true);

                    this.noPathHere.X = this.noPathHere.Y = -1;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks whether the farmer can eat whatever they're holding.
        /// </summary>
        /// <returns>
        ///     Returns true if the farmer can eat the item they're holding. Returns false otherwise.
        /// </returns>
        private bool CheckToEatFood()
        {
            if (Game1.player.ActiveObject is not null && (Game1.player.ActiveObject.Edibility != SObject.inedible || (Game1.player.ActiveObject.name.Length >= 11 && Game1.player.ActiveObject.name.Substring(0, 11) == "Secret Note")))
            {
                this.phase = PathFindingPhase.DoAction;
                return true;
            }

            return false;
        }

        private void CheckToOpenClosedGate()
        {
            if (this.gateNode is not null && Vector2.Distance(Game1.player.OffsetPositionOnMap(), this.gateNode.CenterOnMap) < 83.2f)
            {
                Fence fence = this.gateNode.GetGate();
                if (fence is not null && fence.gatePosition != Fence.gateOpenedPosition)
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
        private void CheckToReTargetFarmAnimal()
        {
            if (this.TargetFarmAnimal is not null && this.clickedTile.X != -1 && (this.clickedTile.X != this.TargetFarmAnimal.getTileX()
                                                                                  || this.clickedTile.Y != this.TargetFarmAnimal.getTileY()))
            {
                this.OnClick(
                    (this.TargetFarmAnimal.getTileX() * Game1.tileSize) - Game1.viewport.X + (Game1.tileSize / 2),
                    (this.TargetFarmAnimal.getTileY() * Game1.tileSize) - Game1.viewport.Y + (Game1.tileSize / 2),
                    Game1.viewport.X,
                    Game1.viewport.Y);
            }
        }

        private void CheckToReTargetNpc()
        {
            if (this.TargetNpc is not null && (this.clickedTile.X != -1 || this.clickedTile.Y != -1))
            {
                if (this.TargetNpc.currentLocation != this.gameLocation)
                {
                    this.Reset();
                }
                else if (ClickToMoveHelper.NpcAtWarpOrDoor(this.TargetNpc, this.gameLocation))
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
                this.CheckForQueuedReadyToHarvestTaps();
            }
        }

        /// <summary>
        ///     Checks whether the player was clicked.
        /// </summary>
        /// <param name="clickPoint">The location clicked.</param>
        /// <returns>returns true if the player was clicked, false otherwise.</returns>
        private bool ClickedOnFarmer(Vector2 clickPoint)
        {
            return new Rectangle((int)Game1.player.position.X, (int)Game1.player.position.Y - 85, Game1.tileSize, 125)
                .Contains((int)clickPoint.X, (int)clickPoint.Y);
        }

        private bool EndNodeBlocked(AStarNode endNode)
        {
            this.toolToSelect = null;
            if (this.gameLocation is Beach beach)
            {
                if (endNode.X == 53 && endNode.Y == 8 && Game1.CurrentEvent is not null && Game1.CurrentEvent.id == 13)
                {
                    this.clickedHaleyBracelet = true;
                    this.endNodeToBeActioned = true;
                    return true;
                }

                if (endNode.X == 57 && endNode.Y == 13 && !beach.bridgeFixed)
                {
                    this.endTileIsActionable = true;
                    return false;
                }

                if (this.graph.OldMariner is not null && endNode.X == this.graph.OldMariner.getTileX() && (endNode.Y == this.graph.OldMariner.getTileY() || endNode.Y == this.graph.OldMariner.getTileY() - 1))
                {
                    if (endNode.Y == this.graph.OldMariner.getTileY() - 1)
                    {
                        this.SelectDifferentEndNode(endNode.X, endNode.Y + 1);
                    }

                    this.performActionFromNeighbourTile = true;
                    return true;
                }
            }

            if (ClickToMoveHelper.ClickedEggAtEggFestival(this.clickPoint))
            {
                this.endNodeToBeActioned = true;
                this.performActionFromNeighbourTile = !endNode.TileClear;
                return !endNode.TileClear;
            }

            if (endNode.ContainsCinemaTicketOffice())
            {
                this.SelectDifferentEndNode(endNode.X, 20);
                this.endTileIsActionable = true;
                this.performActionFromNeighbourTile = true;
                this.clickedCinemaTicketBooth = true;
                return true;
            }

            if (endNode.ContainsCinemaDoor())
            {
                this.SelectDifferentEndNode(endNode.X, 19);
                this.endTileIsActionable = true;
                this.clickedCinemaDoor = true;
                return true;
            }

            if (this.gameLocation is CommunityCenter && endNode.X == 14 && endNode.Y == 5)
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            Game1.currentLocation.terrainFeatures.TryGetValue(new Vector2(endNode.X, endNode.Y), out var value);
            if (value is not null && value is HoeDirt)
            {
                if (Game1.player.CurrentTool is WateringCan && ((WateringCan)Game1.player.CurrentTool).WaterLeft <= 0)
                {
                    Game1.player.doEmote(4);
                    Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                }

                if ((int)((HoeDirt)value).state != 1 && Game1.player.CurrentTool is WateringCan && ((WateringCan)Game1.player.CurrentTool).WaterLeft > 0)
                {
                    this.clickedOnCrop = true;
                }

                Crop crop = ((HoeDirt)value).crop;
                if (crop != null)
                {
                    if ((bool)crop.dead)
                    {
                        if (!(Game1.player.CurrentTool is Hoe))
                        {
                            this.AutoSelectTool("Scythe");
                        }

                        this.endNodeToBeActioned = true;
                        return true;
                    }

                    if (((int)((HoeDirt)value).state != 1 && Game1.player.CurrentTool is WateringCan && ((WateringCan)Game1.player.CurrentTool).WaterLeft > 0) || crop.ReadyToHarvest())
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

                    if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.Category == -74)
                    {
                        this.clickedOnCrop = true;
                    }
                }
            }

            if (this.graph.GameLocation is FarmHouse farmHouse && farmHouse.upgradeLevel == 2 && this.ClickedNode.X == 16 && this.ClickedNode.Y == 4)
            {
                this.SelectDifferentEndNode(this.ClickedNode.X, this.ClickedNode.Y + 1);
                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (endNode.ContainsFurniture())
            {
                Furniture furniture = endNode.GetFurniture();
                if (furniture != null)
                {
                    if ((int)furniture.furniture_type == 14)
                    {
                        this.performActionFromNeighbourTile = true;
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

                    if ((int)furniture.furniture_type == 7)
                    {
                        this.performActionFromNeighbourTile = true;
                        this.endNodeToBeActioned = false;
                        return true;
                    }

                    if ((int)furniture.parentSheetIndex == 1226 || (int)furniture.parentSheetIndex == 1308)
                    {
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }

                    if ((int)furniture.parentSheetIndex == 1300)
                    {
                        this.performActionFromNeighbourTile = true;
                        this.endNodeToBeActioned = false;
                        furniture.PlaySingingStone();
                        this.clickedTile.X = this.clickedTile.Y = -1;
                        return true;
                    }
                }
            }

            if (endNode.ContainsFence() && Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter() && !(Game1.player.CurrentTool is MeleeWeapon))
            {
                this.performActionFromNeighbourTile = true;
                this.endNodeToBeActioned = true;
                return true;
            }

            Chest chest = endNode.GetChest();
            if (chest is not null && Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter() && !(Game1.player.CurrentTool is MeleeWeapon))
            {
                if (chest.items.Count(item => item is not null) == 0)
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

            if (this.gameLocation.ContainsTravellingCart((int)this.clickPoint.X, (int)this.clickPoint.Y))
            {
                if (this.ClickedNode.Y != 11 || (this.ClickedNode.X != 23 && this.ClickedNode.X != 24))
                {
                    this.SelectDifferentEndNode(27, 11);
                }

                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (this.gameLocation.ContainsTravellingDesertShop((int)this.clickPoint.X, (int)this.clickPoint.Y) && (this.ClickedNode.Y == 23 || this.ClickedNode.Y == 24))
            {
                this.performActionFromNeighbourTile = true;

                switch (this.ClickedNode.X)
                {
                    case >= 34 and <= 38:
                        this.SelectDifferentEndNode(this.ClickedNode.X, 24);
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

            if (Game1.currentLocation is Forest forest)
            {
                if (forest.log is not null && forest.log.getBoundingBox(forest.log.tile).Contains(this.ClickedNode.X * Game1.tileSize, this.ClickedNode.Y * Game1.tileSize))
                {
                    this.forestLog = forest.log;
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = true;
                    this.AutoSelectTool("Axe");
                    return true;
                }
            }

            if (endNode.GetObjectParentSheetIndex() == 590)
            {
                this.AutoSelectTool("Hoe");
                return true;
            }

            if (Game1.currentLocation is Farm && endNode.X == ((Farm)Game1.currentLocation).petBowlPosition.X && endNode.Y == ((Farm)Game1.currentLocation).petBowlPosition.Y)
            {
                this.AutoSelectTool("Watering Can");
                this.endNodeToBeActioned = true;
                return true;
            }

            if (Game1.currentLocation is SlimeHutch && endNode.X == 16 && endNode.Y >= 6 && endNode.Y <= 9)
            {
                this.AutoSelectTool("Watering Can");
                this.endNodeToBeActioned = true;
                return true;
            }

            NPC npc = endNode.GetNpc();
            if (npc is Horse horse)
            {
                this.clickedHorse = horse;

                if (!(Game1.player.CurrentItem is Hat))
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (this.mouseCursor == MouseCursor.ReadyForHarvest || Utility.canGrabSomethingFromHere(this.mouseX + this.viewportX, this.mouseY + this.viewportY, Game1.player))
            {
                this.clickedOnCrop = true;
                this.forageItem = Game1.currentLocation.getObjectAt(this.mouseX + this.viewportX, this.mouseY + this.viewportY);
                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.currentLocation is FarmHouse)
            {
                Point bedSpot = ((FarmHouse)Game1.currentLocation).getBedSpot();
                if (bedSpot.X == endNode.X && bedSpot.Y == endNode.Y)
                {
                    this.endNodeToBeActioned = false;
                    this.performActionFromNeighbourTile = false;
                    return false;
                }
            }

            NPC npc2 = Game1.currentLocation.isCharacterAtTile(this.clickedTile);
            if (npc2 != null)
            {
                this.performActionFromNeighbourTile = true;
                this.TargetNpc = npc2;
                if (npc2 is Horse)
                {
                    this.clickedHorse = (Horse)npc2;
                    if (!(Game1.player.CurrentItem is Hat))
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (Game1.currentLocation is MineShaft && Game1.player.CurrentTool is not null && Game1.player.CurrentTool is Pickaxe)
                {
                    this.endNodeToBeActioned = true;
                }

                return true;
            }

            npc2 = Game1.currentLocation.isCharacterAtTile(new Vector2(this.clickedTile.X, this.clickedTile.Y + 1f));
            if (npc2 is not null && !(npc2 is Duggy) && !(npc2 is Grub) && !(npc2 is LavaCrab) && !(npc2 is MetalHead) && !(npc2 is RockCrab) && !(npc2 is GreenSlime))
            {
                this.SelectDifferentEndNode(this.ClickedNode.X, this.ClickedNode.Y + 1);
                this.performActionFromNeighbourTile = true;
                this.TargetNpc = npc2;
                if (npc2 is Horse)
                {
                    this.clickedHorse = (Horse)npc2;
                    if (!(Game1.player.CurrentItem is Hat))
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (Game1.currentLocation is MineShaft && Game1.player.CurrentTool is not null && Game1.player.CurrentTool is Pickaxe)
                {
                    this.endNodeToBeActioned = true;
                }

                return true;
            }

            if (Game1.currentLocation is Farm && endNode.Y == 13 && (endNode.X == 71 || endNode.X == 72) && this.clickedHorse == null)
            {
                this.SelectDifferentEndNode(endNode.X, endNode.Y + 1);
                this.endTileIsActionable = true;
                return true;
            }

            this.TargetFarmAnimal = ClickToMoveHelper.GetFarmAnimal(this.graph.GameLocation, (int)this.clickPoint.X, (int)this.clickPoint.Y);
            if (this.TargetFarmAnimal != null)
            {
                if (this.TargetFarmAnimal.getTileX() != this.ClickedNode.X || this.TargetFarmAnimal.getTileY() != this.ClickedNode.Y)
                {
                    this.SelectDifferentEndNode(this.TargetFarmAnimal.getTileX(), this.TargetFarmAnimal.getTileY());
                }

                if (((string)this.TargetFarmAnimal.type == "White Cow" || (string)this.TargetFarmAnimal.type == "Brown Cow" || (string)this.TargetFarmAnimal.type == "Goat") && Game1.player.CurrentTool is MilkPail)
                {
                    return true;
                }

                if ((string)this.TargetFarmAnimal.type == "Sheep" && Game1.player.CurrentTool is Shears)
                {
                    return true;
                }

                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.player.ActiveObject is not null && ((bool)Game1.player.ActiveObject.bigCraftable || PathFindingController.ActionableObjectIDs.Contains(Game1.player.ActiveObject.parentSheetIndex) || (Game1.player.ActiveObject is Wallpaper && (int)Game1.player.ActiveObject.parentSheetIndex <= 40)) && (!(Game1.player.ActiveObject is Wallpaper) || Game1.currentLocation is DecoratableLocation))
            {
                if (Game1.player.ActiveObject.ParentSheetIndex == 288)
                {
                    Building building = this.ClickedNode.GetBuilding();
                    if (building is FishPond)
                    {
                        this.actionableBuilding = building;
                        Point tileNextToBuildingNearestFarmer = this.graph.GetNearestTileNextToBuilding(building);
                        this.SelectDifferentEndNode(tileNextToBuildingNearestFarmer.X, tileNextToBuildingNearestFarmer.Y);
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }
                }

                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.currentLocation is Mountain && endNode.X == 29 && endNode.Y == 9)
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            CrabPot crabPot = this.ClickedNode.GetCrabPot();
            if (crabPot != null)
            {
                this.crabPot = crabPot;
                this.performActionFromNeighbourTile = true;
                AStarNode aStarNode = endNode.GetNearestNodeToCrabPot();
                if (aStarNode is not null && endNode != aStarNode)
                {
                    this.ClickedNode = aStarNode;
                    return false;
                }

                return true;
            }

            if (!endNode.TileClear)
            {
                Game1.currentLocation.objects.TryGetValue(new Vector2(endNode.X, endNode.Y), out var value2);
                if (value2 != null)
                {
                    if (value2.Category == -9)
                    {
                        if ((int)value2.parentSheetIndex == 99 || (int)value2.parentSheetIndex == 101 || (int)value2.parentSheetIndex == 163)
                        {
                            if (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe)
                            {
                                this.endNodeToBeActioned = true;
                            }

                            this.performActionFromNeighbourTile = true;
                            return true;
                        }

                        if ((int)value2.parentSheetIndex == 163 || (int)value2.parentSheetIndex == 216 || (int)value2.parentSheetIndex == 208)
                        {
                            this.performActionFromNeighbourTile = true;
                            if (Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter() && !(Game1.player.CurrentTool is MeleeWeapon))
                            {
                                this.endNodeToBeActioned = true;
                            }

                            return true;
                        }

                        if ((int)value2.parentSheetIndex >= 118 && (int)value2.parentSheetIndex <= 125)
                        {
                            if (Game1.player.CurrentTool == null || !Game1.player.CurrentTool.isHeavyHitter())
                            {
                                this.AutoSelectTool("Pickaxe");
                            }

                            this.endNodeToBeActioned = true;
                            return true;
                        }

                        if (Game1.player.CurrentTool is not null && (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe || Game1.player.CurrentTool is Hoe))
                        {
                            this.endNodeToBeActioned = true;
                        }

                        if (value2.Name.Contains("Chest"))
                        {
                            this.performActionFromNeighbourTile = true;
                        }

                        return true;
                    }

                    if (value2.Name == "Stone" || value2.Name == "Boulder")
                    {
                        this.AutoSelectTool("Pickaxe");
                        return true;
                    }

                    if (value2.Name == "Weeds")
                    {
                        this.AutoSelectTool("Scythe");
                        return true;
                    }

                    if (value2.Name == "Twig")
                    {
                        this.AutoSelectTool("Axe");
                        return true;
                    }

                    if (value2.Name == "House Plant")
                    {
                        this.AutoSelectTool("Pickaxe");
                        this.endNodeToBeActioned = true;
                        return true;
                    }

                    if (value2.parentSheetIndex.Value == ObjectId.DrumBlock || value2.parentSheetIndex.Value == ObjectId.FluteBlock)
                    {
                        if (Game1.player.CurrentTool is not null && (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe || Game1.player.CurrentTool is Hoe))
                        {
                            this.endNodeToBeActioned = true;
                        }

                        return true;
                    }
                }
                else
                {
                    if (endNode.ContainsStumpOrBoulder())
                    {
                        if (endNode.ContainsStumpOrHollowLog())
                        {
                            this.AutoSelectTool("Axe");
                        }
                        else
                        {
                            GiantCrop giantCrop = endNode.GetGiantCrop();

                            if (giantCrop is not null)
                            {
                                if (giantCrop.width.Value == 3 && giantCrop.height.Value == 3 && giantCrop.tile.X + 1 == endNode.X && giantCrop.tile.Y + 1 == endNode.Y)
                                {
                                    Point point = PathFindingController.GetNextPointOut(this.graph.FarmerNodeOffset.X, this.graph.FarmerNodeOffset.Y, endNode.X, endNode.Y);

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

                    Building building2 = this.ClickedNode.GetBuilding();
                    if (building2 is not null && building2.buildingType.Equals("Shipping Bin"))
                    {
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }

                    if (building2 is not null && building2.buildingType.Equals("Mill"))
                    {
                        if (Game1.player.ActiveObject is not null && ((int)Game1.player.ActiveObject.parentSheetIndex == 284 || (int)Game1.player.ActiveObject.parentSheetIndex == 262))
                        {
                            this.performActionFromNeighbourTile = true;
                            this.endNodeToBeActioned = true;
                            return true;
                        }

                        this.performActionFromNeighbourTile = true;
                        return true;
                    }

                    if (building2 is not null && building2 is Barn)
                    {
                        Barn barn = building2 as Barn;
                        int num = (int)barn.tileX + barn.animalDoor.X;
                        int num2 = (int)barn.tileY + barn.animalDoor.Y;
                        if ((this.ClickedNode.X == num || this.ClickedNode.X == num + 1) && (this.ClickedNode.Y == num2 || this.ClickedNode.Y == num2 - 1))
                        {
                            if (this.ClickedNode.Y == num2 - 1)
                            {
                                this.SelectDifferentEndNode(this.ClickedNode.X, this.ClickedNode.Y + 1);
                            }

                            this.performActionFromNeighbourTile = true;
                            return true;
                        }
                    }
                    else if (building2 is not null && building2 is FishPond && !(Game1.player.CurrentTool is FishingRod) && !(Game1.player.CurrentTool is WateringCan))
                    {
                        this.actionableBuilding = building2;
                        Point tileNextToBuildingNearestFarmer2 = this.graph.GetNearestTileNextToBuilding(building2);
                        this.SelectDifferentEndNode(tileNextToBuildingNearestFarmer2.X, tileNextToBuildingNearestFarmer2.Y);
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }

                    AStarNode upNode = this.graph.GetNode(this.ClickedNode.X, this.ClickedNode.Y - 1);
                    if (upNode is not null && upNode.ContainsFurnitureIgnoreRugs() && upNode.GetFurnitureIgnoreRugs().parentSheetIndex.Value == 1402)
                    {
                        this.SelectDifferentEndNode(this.ClickedNode.X, this.ClickedNode.Y + 1);
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }
                }

                TerrainFeature terrainFeature = endNode.GetTree();
                if (terrainFeature is not null)
                {
                    if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.edibility.Value > -300)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }

                    if (terrainFeature is Tree tree)
                    {
                        if (tree.growthStage.Value <= 1)
                        {
                            this.AutoSelectTool("Scythe");
                        }
                        else
                        {
                            this.AutoSelectTool("Axe");
                        }
                    }

                    return true;
                }

                if (endNode.ContainsStump())
                {
                    this.AutoSelectTool("Axe");
                    return true;
                }

                if (endNode.ContainsMinable())
                {
                    this.AutoSelectTool("Pickaxe");
                    return true;
                }

                if (Game1.currentLocation is Town && endNode.X == 108 && endNode.Y == 41)
                {
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = true;
                    this.endTileIsActionable = true;
                    return true;
                }

                if (Game1.currentLocation is Town && endNode.X == 100 && endNode.Y == 66)
                {
                    this.performActionFromNeighbourTile = true;
                    this.endNodeToBeActioned = true;
                    return true;
                }

                Bush bush = endNode.GetBush();
                if (bush is not null)
                {
                    if (Game1.player.CurrentTool is Axe && bush.isDestroyable(Game1.currentLocation, this.clickedTile))
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
                    AStarNode aStarNode3 = this.graph.GetNodeNearestWaterSource(this.ClickedNode);
                    if (aStarNode3 != null)
                    {
                        this.ClickedNode = aStarNode3;
                        this.clickedTile = new Vector2(this.ClickedNode.X, this.ClickedNode.Y);
                        this.clickPoint = new Vector2((int)this.ClickedNode.CenterOnMap.X, (int)this.ClickedNode.CenterOnMap.Y);
                        this.endNodeToBeActioned = true;
                        return true;
                    }
                }

                if (Game1.isActionAtCurrentCursorTile && Game1.player.CurrentTool is FishingRod)
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                if (this.gameLocation.IsWateringCanFillingSource((int)this.clickedTile.X, (int)this.clickedTile.Y) && (Game1.player.CurrentTool is WateringCan || Game1.player.CurrentTool is FishingRod))
                {
                    if (Game1.player.CurrentTool is FishingRod
                        && Game1.currentLocation is Town
                        && this.ClickedNode.X >= 50
                        && this.ClickedNode.X <= 53
                        && this.ClickedNode.Y >= 103
                        && this.ClickedNode.Y <= 105)
                    {
                        this.ClickedNode = this.graph.GetNode(52, this.ClickedNode.Y);
                    }
                    else
                    {
                        AStarNode nodeNextToWater = this.graph.GetNearestLandNodePerpendicularToWaterSource(this.ClickedNode);

                        float distanceMultiplier = 2.5f;
                        if (Game1.player.CurrentTool is FishingRod)
                        {
                            distanceMultiplier += Game1.player.GetFishingAddedDistance();
                        }

                        if (nodeNextToWater is not null
                            && Game1.player.CurrentTool is FishingRod
                            && Vector2.Distance(Game1.player.OffsetPositionOnMap(), nodeNextToWater.CenterOnMap) < Game1.tileSize * distanceMultiplier)
                        {
                            this.FaceTileClicked();
                            this.ClickedNode = this.startNode;
                        }
                        else if (nodeNextToWater != null)
                        {
                            this.ClickedNode = nodeNextToWater;
                        }
                    }

                    this.waterSourceAndFishingRodSelected = true;
                    this.endNodeToBeActioned = true;
                    this.clickedTile = new Vector2(this.ClickedNode.X, this.ClickedNode.Y);
                    this.performActionFromNeighbourTile = true;
                    return true;
                }

                if (this.gameLocation.IsWizardBuilding(this.clickedTile))
                {
                    this.performActionFromNeighbourTile = true;
                    return true;
                }

                this.endTileIsActionable = Game1.currentLocation.isActionableTile(this.ClickedNode.X, this.ClickedNode.Y, Game1.player) || Game1.currentLocation.isActionableTile(this.ClickedNode.X, this.ClickedNode.Y + 1, Game1.player);

                if (!this.endTileIsActionable)
                {
                    Tile tile = Game1.currentLocation.map.GetLayer("Buildings").PickTile(new Location((int)this.clickedTile.X * Game1.tileSize, (int)this.clickedTile.Y * Game1.tileSize), Game1.viewport.Size);
                    this.endTileIsActionable = tile != null;
                }

                return true;
            }

            Game1.currentLocation.terrainFeatures.TryGetValue(new Vector2(endNode.X, endNode.Y), out var value3);
            if (value3 != null)
            {
                if (value3 is Grass && Game1.player.CurrentTool is not null && Game1.player.CurrentTool is MeleeWeapon && (int)((MeleeWeapon)Game1.player.CurrentTool).type != 2)
                {
                    this.endNodeToBeActioned = true;
                    return true;
                }

                if (value3 is Flooring && Game1.player.CurrentTool is not null && (Game1.player.CurrentTool is Pickaxe || Game1.player.CurrentTool is Axe))
                {
                    this.endNodeToBeActioned = true;
                    return true;
                }
            }

            Game1.currentLocation.objects.TryGetValue(new Vector2(endNode.X, endNode.Y), out var value4);
            if (value4 is not null && ((int)value4.parentSheetIndex == 93 || (int)value4.parentSheetIndex == 94) && Game1.player.CurrentTool is not null && (Game1.player.CurrentTool is Pickaxe || Game1.player.CurrentTool is Axe))
            {
                this.endNodeToBeActioned = true;
                return true;
            }

            if (Game1.player.CurrentTool is FishingRod && Game1.currentLocation is Town && this.ClickedNode.X >= 50 && this.ClickedNode.X <= 53 && this.ClickedNode.Y >= 103 && this.ClickedNode.Y <= 105)
            {
                this.waterSourceAndFishingRodSelected = true;
                this.ClickedNode = this.graph.GetNode(52, this.ClickedNode.Y);
                this.clickedTile = new Vector2(this.ClickedNode.X, this.ClickedNode.Y);
                this.endNodeToBeActioned = true;
                return true;
            }

            if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.canBePlacedHere(Game1.currentLocation, this.clickedTile) && (Game1.player.ActiveObject.bigCraftable || (int)Game1.player.ActiveObject.parentSheetIndex == 104 || (int)Game1.player.ActiveObject.parentSheetIndex == 688 || (int)Game1.player.ActiveObject.parentSheetIndex == 689 || Game1.player.ActiveObject.parentSheetIndex.Value == 690 || Game1.player.ActiveObject.parentSheetIndex.Value == 681 || (int)Game1.player.ActiveObject.parentSheetIndex == 261 || (int)Game1.player.ActiveObject.parentSheetIndex == 161 || (int)Game1.player.ActiveObject.parentSheetIndex == 155 || (int)Game1.player.ActiveObject.parentSheetIndex == 162 || Game1.player.ActiveObject.name.Contains("Sapling")))
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.player.mount == null)
            {
                this.endNodeToBeActioned = Game1.player.CurrentTool is Hoe && this.gameLocation.IsTileHoeable(this.clickedTile);
                if (this.endNodeToBeActioned)
                {
                    this.performActionFromNeighbourTile = true;
                }
            }

            if (!this.endNodeToBeActioned)
            {
                this.endNodeToBeActioned = this.WateringCanActionAtEndNode();
                _ = this.endNodeToBeActioned;
            }

            if (!this.endNodeToBeActioned && Game1.player.ActiveObject is not null && Game1.player.ActiveObject is not null)
            {
                this.endNodeToBeActioned = Game1.player.ActiveObject.isPlaceable() && Game1.player.ActiveObject.canBePlacedHere(Game1.currentLocation, this.clickedTile);
                Crop crop2 = new Crop(Game1.player.ActiveObject.parentSheetIndex, endNode.X, endNode.Y);
                if (crop2 is not null && ((int)Game1.player.ActiveObject.parentSheetIndex == 368 || (int)Game1.player.ActiveObject.parentSheetIndex == 369))
                {
                    this.endNodeToBeActioned = true;
                }

                if (crop2 is not null && (bool)crop2.raisedSeeds)
                {
                    this.performActionFromNeighbourTile = true;
                }
            }

            if (endNode.ContainsTree() && (Game1.player.CurrentTool is Hoe || Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe))
            {
                this.endNodeToBeActioned = true;
                _ = this.endNodeToBeActioned;
            }

            if (this.mouseCursor == MouseCursor.Hand || this.mouseCursor == MouseCursor.MagnifyingGlass || this.mouseCursor == MouseCursor.SpeechBubble)
            {
                AStarNode downNode = this.graph.GetNode(this.ClickedNode.X, this.ClickedNode.Y + 1);
                if (this.ClickedNode.ContainsGate())
                {
                    this.gateClickedOn = this.ClickedNode.GetGate();
                    if (this.gateClickedOn.gatePosition == Fence.gateOpenedPosition)
                    {
                        this.gateClickedOn = null;
                    }

                    this.performActionFromNeighbourTile = true;
                }
                else if (!this.ClickedNode.ContainsScarecrow() && downNode.ContainsScarecrow() && Game1.player.CurrentTool is not null)
                {
                    this.endTileIsActionable = true;
                    this.endNodeToBeActioned = true;
                    this.performActionFromNeighbourTile = true;
                }
                else
                {
                    this.endTileIsActionable = true;
                    _ = this.endTileIsActionable;
                }
            }

            if (endNode.GetWarp(this.IgnoreWarps) is not null)
            {
                this.endNodeToBeActioned = false;
                return false;
            }

            if (!this.endNodeToBeActioned)
            {
                AStarNode aStarNode6 = this.graph.GetNode(endNode.X, endNode.Y + 1);
                if (aStarNode6 != null)
                {
                    Building building3 = aStarNode6.GetBuilding();
                    if (building3 is not null && building3.buildingType.Equals("Shipping Bin"))
                    {
                        this.SelectDifferentEndNode(endNode.X, endNode.Y + 1);
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }
                }
            }

            return this.endNodeToBeActioned;
        }

        private void FaceTileClicked(bool faceClickPoint = false)
        {
            int facingDirection = Game1.player.facingDirection.Value;
            int direction = (!faceClickPoint) ? WalkDirection.GetFacingDirection(this.clickedTile, new Vector2(Game1.player.position.X / Game1.tileSize, Game1.player.position.Y / Game1.tileSize)) : WalkDirection.GetFacingDirection(this.clickPoint, Game1.player.position);
            if (direction != facingDirection)
            {
                Game1.player.Halt();
                Game1.player.faceDirection(direction);
            }
        }

        private bool FindAlternatePath(AStarNode start, int x, int y)
        {
            if (start is not null)
            {
                AStarNode node = this.graph.GetNode(x, y);

                if (node?.TileClear == true)
                {
                    this.path = this.graph.FindPath(start, node);

                    if (this.path is not null)
                    {
                        this.path.SmoothRightAngles();
                        this.phase = PathFindingPhase.FollowingPath;

                        return true;
                    }
                }
            }

            return false;
        }

        private void FollowPath()
        {
            if (this.path.Count > 0)
            {
                AStarNode farmerNode = this.graph.FarmerNodeOffset;

                if (farmerNode is null)
                {
                    this.Reset();
                    return;
                }

                AStarNode nextNode = this.path.GetFirst();

                if (nextNode == farmerNode)
                {
                    this.path.RemoveFirst();

                    nextNode = this.path.GetFirst();

                    this.lastDistance = float.MaxValue;
                    this.stuckCount = 0;
                    this.reallyStuckCount = 0;
                }

                if (nextNode is not null)
                {
                    if (nextNode.ContainsAnimal()
                        || (nextNode.ContainsNpc() && !Game1.player.isRidingHorse() && nextNode.GetNpc() is not Horse))
                    {
                        this.OnClick(this.mouseX, this.mouseY, this.viewportX, this.viewportY);

                        return;
                    }

                    Vector2 nextNodeCenter = nextNode.CenterOnMap;
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
                        || this.stuckCount >= PathFindingController.MaxStuckCount)
                    {
                        if (this.reallyStuckCount >= PathFindingController.MaxReallyStuckCount)
                        {
                            walkDirection = WalkDirection.OppositeWalkDirection(walkDirection);

                            this.reallyStuckCount++;

                            if (this.reallyStuckCount == 8)
                            {
                                if (Game1.player.isRidingHorse())
                                {
                                    this.Reset();
                                }
                                else if (this.clickedHorse is not null)
                                {
                                    this.clickedHorse.checkAction(Game1.player, this.gameLocation);

                                    this.Reset();
                                }
                                else if (this.graph.FarmerNodeOffset.GetNpc() is Horse horse)
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
                            WalkDirection newWalkDirection = farmerNode.WalkDirectionToNeighbour(nextNode);

                            if (newWalkDirection != walkDirection)
                            {
                                this.reallyStuckCount++;
                                walkDirection = newWalkDirection;
                            }
                            else
                            {
                                newWalkDirection = WalkDirection.GetWalkDirection(
                                    Game1.player.OffsetPositionOnMap(),
                                    nextNodeCenter);

                                if (newWalkDirection != walkDirection)
                                {
                                    this.reallyStuckCount++;
                                    walkDirection = newWalkDirection;
                                }
                            }

                            this.stuckCount = 0;
                        }
                    }

                    this.KeyStates.SetMovePressed(walkDirection);
                }
            }

            if (this.path.Count == 0)
            {
                this.path = null;
                this.stuckCount = 0;
                this.phase = PathFindingPhase.OnFinalTile;
            }
        }

        private bool HoldingWallpaperAndTileClickedIsWallOrFloor()
        {
            if (Game1.player.CurrentItem is not null && Game1.player.CurrentItem is Wallpaper wallpaper
                                                     && this.gameLocation is DecoratableLocation decoratableLocation)
            {
                return wallpaper.CanReallyBePlacedHere(decoratableLocation, this.clickedTile);
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

            return this.graph.GetNode(tileX, tileY)?.ContainsStumpOrBoulder() ?? false;
        }

        private void MoveOnFinalTile()
        {
            if (this.performActionFromNeighbourTile)
            {
                float num = Vector2.Distance(Game1.player.OffsetPositionOnMap(), this.ClickedNode.CenterOnMap);
                float val = Math.Abs(this.ClickedNode.CenterOnMap.X - Game1.player.OffsetPositionOnMap().X) - (float)Game1.player.speed;
                float val2 = Math.Abs(this.ClickedNode.CenterOnMap.Y - Game1.player.OffsetPositionOnMap().Y) - (float)Game1.player.speed;
                if (num == this.lastDistance)
                {
                    this.stuckCount++;
                }

                this.lastDistance = num;
                if (Game1.player.GetBoundingBox().Intersects(this.ClickedNode.BoundingBox) && this.distanceToTarget != DistanceToTarget.TooFar && this.crabPot == null)
                {
                    this.distanceToTarget = DistanceToTarget.TooClose;
                    WalkDirection movePressed = WalkDirection.GetWalkDirection(this.clickPoint, Game1.player.OffsetPositionOnMap());
                    this.KeyStates.SetMovePressed(movePressed);
                }
                else if (this.distanceToTarget != DistanceToTarget.TooClose && this.stuckCount < 4 && Math.Max(val, val2) > Game1.tileSize)
                {
                    this.distanceToTarget = DistanceToTarget.TooFar;
                    WalkDirection movePressed2 = WalkDirection.GetWalkDirectionForAngle((float)Math.Atan2(this.ClickedNode.CenterOnMap.Y - Game1.player.OffsetPositionOnMap().Y, this.ClickedNode.CenterOnMap.X - Game1.player.OffsetPositionOnMap().X) / ((float)Math.PI * 2f) * 360f);
                    this.KeyStates.SetMovePressed(movePressed2);
                }
                else
                {
                    this.distanceToTarget = DistanceToTarget.InRange;
                    this.OnReachEndOfPath();
                }
            }
            else
            {
                float num2 = Vector2.Distance(Game1.player.OffsetPositionOnMap(), this.ClickedNode.CenterOnMap);
                if (num2 == this.lastDistance || num2 > this.lastDistance)
                {
                    this.stuckCount++;
                }

                this.lastDistance = num2;
                if (num2 < Game1.player.getMovementSpeed() || this.stuckCount >= PathFindingController.MaxStuckCount || (this.endNodeToBeActioned && num2 < Game1.tileSize) || (this.endNodeOccupied is not null && num2 < 66f))
                {
                    this.OnReachEndOfPath();
                    return;
                }

                WalkDirection movePressed3 = WalkDirection.GetWalkDirection(Game1.player.OffsetPositionOnMap(), this.finalNode.CenterOnMap, Game1.player.getMovementSpeed());

                this.KeyStates.SetMovePressed(movePressed3);
            }
        }

        private void OnClickToMoveComplete()
        {
            if ((Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival) && !Game1.player.usingTool.Value
                                                                               && this.clickedHorse is null
                                                                               && !this.warping && !this.IgnoreWarps
                                                                               && Game1.player.WarpIfInRange(
                                                                                   this.gameLocation,
                                                                                   new Vector2(this.clickPoint.X, this.clickPoint.Y)))
            {
                this.Reset();

                this.warping = true;
            }
            else
            {
                this.Reset();
            }

            this.CheckForQueuedReadyToHarvestClicks();
        }

        private void OnReachEndOfPath()
        {
            this.AutoSelectPendingTool();
            if (this.endNodeOccupied != null)
            {
                WalkDirection walkDirection;
                if (this.endNodeToBeActioned)
                {
                    if (Game1.currentMinigame is FishingGame)
                    {
                        walkDirection = WalkDirection.GetFacingWalkDirection(this.clickPoint, Game1.player.OffsetPositionOnMap());

                        this.FaceTileClicked();
                    }
                    else
                    {
                        walkDirection = WalkDirection.GetWalkDirection(Game1.player.OffsetPositionOnMap(), this.clickPoint);

                        if (Game1.player.CurrentTool is not null && (Game1.player.CurrentTool is FishingRod || (Game1.player.CurrentTool is WateringCan && this.waterSourceAndFishingRodSelected)))
                        {
                            Game1.player.faceDirection(WalkDirection.GetFacingDirection(this.clickPoint, Game1.player.OffsetPositionOnMap()));
                        }
                    }
                }
                else
                {
                    walkDirection = this.graph.FarmerNode.WalkDirectionToNeighbour(this.endNodeOccupied);
                    if (walkDirection == WalkDirection.None)
                    {
                        walkDirection = WalkDirection.GetWalkDirection(Game1.player.OffsetPositionOnMap(), this.endNodeOccupied.CenterOnMap, 16f);
                    }
                }

                if (walkDirection == WalkDirection.None)
                {
                    walkDirection = this.KeyStates.LastWalkDirection;
                }

                this.KeyStates.SetMovePressed(walkDirection);

                if (this.endNodeToBeActioned || !this.PerformAction())
                {
                    if (Game1.player.CurrentTool is WateringCan)
                    {
                        this.FaceTileClicked(faceClickPoint: true);
                    }

                    if (!(Game1.player.CurrentTool is FishingRod) || this.waterSourceAndFishingRodSelected)
                    {
                        this.GrabTile = this.clickedTile;
                        if (!this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile) && this.forestLog == null)
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
                            this.KeyStates.SetUseTool(useTool: true);
                        }
                    }
                }
            }
            else if (this.endNodeToBeActioned)
            {
                this.KeyStates.SetUseTool(useTool: true);
            }
            else if (!this.PerformAction())
            {
                this.KeyStates.SetPressed(up: false, down: false, left: false, right: false);
            }

            this.phase = PathFindingPhase.ReachedEndOfPath;
        }

        private bool PerformAction()
        {
            if (this.PerformCrabPotAction())
            {
                return true;
            }

            if (this.actionableBuilding != null)
            {
                this.actionableBuilding.doAction(new Vector2((int)this.actionableBuilding.tileX, (int)this.actionableBuilding.tileY), Game1.player);
                return true;
            }

            if (this.clickedCinemaTicketBooth)
            {
                this.clickedCinemaTicketBooth = false;
                Game1.currentLocation.checkAction(new Location(55, 20), Game1.viewport, Game1.player);
                return true;
            }

            if (this.clickedCinemaDoor)
            {
                this.clickedCinemaDoor = false;
                Game1.currentLocation.checkAction(new Location(53, 19), Game1.viewport, Game1.player);
                return true;
            }

            if ((this.endTileIsActionable || this.performActionFromNeighbourTile) && Game1.player.mount is not null && this.forageItem is not null)
            {
                Game1.currentLocation.checkAction(new Location(this.ClickedNode.X, this.ClickedNode.Y), Game1.viewport, Game1.player);
                this.forageItem = null;
                return true;
            }

            if (this.clickedHorse is not null)
            {
                this.clickedHorse.checkAction(Game1.player, Game1.currentLocation);

                this.Reset();

                return false;
            }

            if (Game1.player.mount is not null && this.clickedHorse == null)
            {
                ClickToMovePatcher.SetHorseCheckActionEnabled(Game1.player.mount, false);
            }

            if (this.KeyStates.RealClickHeld && this.furniture is not null && this.forageItem == null)
            {
                this.pendingFurnitureAction = true;
                return true;
            }

            if (this.mouseCursor == MouseCursor.Hand && (string)Game1.currentLocation.name == "Blacksmith" && this.clickedTile.X == 3f && (this.clickedTile.Y == 12f || this.clickedTile.Y == 13f || this.clickedTile.Y == 14f))
            {
                Game1.currentLocation.performAction("Blacksmith", Game1.player, new Location(3, 14));
                Game1.player.Halt();
                return false;
            }

            if (Game1.currentLocation.isActionableTile((int)this.clickedTile.X, (int)this.clickedTile.Y, Game1.player) && !this.ClickedNode.ContainsGate())
            {
                if (this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile) || this.forestLog != null)
                {
                    if (this.ClickHoldActive)
                    {
                        return false;
                    }

                    this.SwitchBackToLastTool();
                }

                Game1.player.Halt();
                this.KeyStates.ActionButtonPressed = true;
                return true;
            }

            if (this.endNodeOccupied is not null && !this.endTileIsActionable && !this.performActionFromNeighbourTile)
            {
                if (this.furniture != null)
                {
                    return true;
                }

                return false;
            }

            if (Game1.currentLocation is Farm && Game1.currentLocation.isActionableTile((int)this.clickedTile.X, (int)this.clickedTile.Y + 1, Game1.player))
            {
                this.KeyStates.SetMovePressed(WalkDirection.Down);
                this.KeyStates.ActionButtonPressed = true;
                return true;
            }

            if (this.TargetNpc is Child)
            {
                this.TargetNpc.checkAction(Game1.player, Game1.currentLocation);

                this.Reset();

                return false;
            }

            if (this.endTileIsActionable || this.performActionFromNeighbourTile)
            {
                this.gateNode = null;
                if (this.gateClickedOn != null)
                {
                    this.gateClickedOn = null;
                    return false;
                }

                this.FaceTileClicked();

                Game1.player.Halt();

                this.KeyStates.ActionButtonPressed = true;

                return true;
            }

            Game1.player.Halt();

            return false;
        }

        private bool PerformCrabPotAction()
        {
            if (this.crabPot != null)
            {
                if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.Category == -21)
                {
                    if (this.crabPot.performObjectDropInAction(Game1.player.ActiveObject, probe: false, Game1.player))
                    {
                        Game1.player.reduceActiveItemByOne();
                    }
                }
                else if (!this.crabPot.checkForAction(Game1.player))
                {
                    this.crabPot.performRemoveAction(this.clickedTile, Game1.currentLocation);
                }

                this.crabPot = null;

                return true;
            }

            return false;
        }

        private void SelectDifferentEndNode(int x, int y)
        {
            AStarNode aStarNode = this.graph.GetNode(x, y);
            if (aStarNode != null)
            {
                this.ClickedNode = aStarNode;
                this.clickedTile.X = x;
                this.clickedTile.Y = y;
                this.clickPoint.X = (x * Game1.tileSize) + (Game1.tileSize / 2);
                this.clickPoint.Y = (y * Game1.tileSize) + (Game1.tileSize / 2);
                this.mouseX = (int)this.clickPoint.X - this.viewportX;
                this.mouseY = (int)this.clickPoint.Y - this.viewportY;
            }
        }

        private void SetMouseCursor(AStarNode endNode)
        {
            this.mouseCursor = MouseCursor.None;

            if (endNode == null)
            {
                return;
            }

            if (this.gameLocation.isActionableTile(endNode.X, endNode.Y, Game1.player) || this.gameLocation.isActionableTile(endNode.X, endNode.Y + 1, Game1.player))
            {
                this.mouseCursor = MouseCursor.Hand;
            }

            if (this.gameLocation.doesTileHaveProperty(endNode.X, endNode.Y, "Action", "Buildings") is not null
                && this.gameLocation.doesTileHaveProperty(endNode.X, endNode.Y, "Action", "Buildings").Contains("Message"))
            {
                this.mouseCursor = MouseCursor.MagnifyingGlass;
            }

            Vector2 tile = new Vector2(endNode.X, endNode.Y);

            NPC npc = Game1.currentLocation.isCharacterAtTile(tile);
            if (npc is not null && !npc.IsMonster)
            {
                if (!Game1.eventUp && Game1.player.ActiveObject is not null && Game1.player.ActiveObject.canBeGivenAsGift() && Game1.player.friendshipData.ContainsKey(npc.Name) && Game1.player.friendshipData[npc.Name].GiftsToday != 1)
                {
                    this.mouseCursor = MouseCursor.Gift;
                }
                else if (npc.canTalk() && npc.CurrentDialogue is not null && (npc.CurrentDialogue.Count > 0 || npc.hasTemporaryMessageAvailable()) && !npc.isOnSilentTemporaryMessage())
                {
                    this.mouseCursor = MouseCursor.SpeechBubble;
                }
            }

            if (Game1.CurrentEvent is not null && Game1.CurrentEvent.isFestival)
            {
                NPC festivalHost = Game1.CurrentEvent.GetFestivalHost();
                if (festivalHost is not null && festivalHost.getTileLocation().Equals(tile))
                {
                    this.mouseCursor = MouseCursor.SpeechBubble;
                }
            }

            if (Game1.player.IsLocalPlayer)
            {
                if (Game1.currentLocation.Objects.ContainsKey(tile))
                {
                    if (Game1.currentLocation.Objects[tile].readyForHarvest
                        || (Game1.currentLocation.Objects[tile].Name.Contains("Table") && Game1.currentLocation.Objects[tile].heldObject.Value is not null)
                        || Game1.currentLocation.Objects[tile].isSpawnedObject
                        || (Game1.currentLocation.Objects[tile] is IndoorPot indoorPot && indoorPot.hoeDirt.Value.readyForHarvest()))
                    {
                        this.mouseCursor = MouseCursor.ReadyForHarvest;
                    }
                }
                else if (Game1.currentLocation.terrainFeatures.ContainsKey(tile) && Game1.currentLocation.terrainFeatures[tile] is HoeDirt dirt && dirt.readyForHarvest())
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
            this.KeyStates.SetMovePressed(WalkDirection.None);
            this.KeyStates.ActionButtonPressed = false;

            if ((this.gameLocation.IsMatureTreeStumpOrBoulderAt(this.clickedTile) || this.forestLog is not null) && this.KeyStates.RealClickHeld && (Game1.player.CurrentTool is Axe || Game1.player.CurrentTool is Pickaxe))
            {
                this.KeyStates.SetUseTool(true);
                this.phase = PathFindingPhase.PendingComplete;

                if (!this.clickPressed)
                {
                    this.OnClickRelease();
                }
            }
            else if (Game1.player.usingTool && (Game1.player.CurrentTool is WateringCan || Game1.player.CurrentTool is Hoe) && this.KeyStates.RealClickHeld)
            {
                this.KeyStates.SetUseTool(true);
                this.phase = PathFindingPhase.PendingComplete;

                if (!this.clickPressed)
                {
                    this.OnClickRelease();
                }
            }
            else
            {
                this.KeyStates.SetUseTool(false);
                this.phase = PathFindingPhase.Complete;
            }
        }

        private bool TappedOnAnotherQueableCrop(int clickPointX, int clickPointY)
        {
            AStarNode aStarNode = this.graph.GetNode(clickPointX / Game1.tileSize, clickPointY / Game1.tileSize);
            if (aStarNode != null)
            {
                Game1.currentLocation.terrainFeatures.TryGetValue(new Vector2(aStarNode.X, aStarNode.Y), out var value);
                if (value is not null && value is HoeDirt)
                {
                    HoeDirt hoeDirt = (HoeDirt)value;
                    if ((int)hoeDirt.state != 1 && Game1.player.CurrentTool is WateringCan && ((WateringCan)Game1.player.CurrentTool).WaterLeft > 0)
                    {
                        return true;
                    }

                    if ((int)hoeDirt.state != 1 && Game1.player.CurrentTool is WateringCan && ((WateringCan)Game1.player.CurrentTool).WaterLeft <= 0)
                    {
                        Game1.player.doEmote(4);
                        Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    }

                    if (hoeDirt.crop is not null && ((Game1.player.CurrentTool is WateringCan && ((WateringCan)Game1.player.CurrentTool).WaterLeft > 0) || (bool)hoeDirt.crop.fullyGrown))
                    {
                        return true;
                    }
                }

                if (this.mouseCursor == MouseCursor.ReadyForHarvest || Utility.canGrabSomethingFromHere(clickPointX, clickPointY, Game1.player))
                {
                    return true;
                }

                if (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TappedOnHoeDirtAndHoldingSeed(int clickPointX, int clickPointY)
        {
            AStarNode aStarNode = this.graph.GetNode(clickPointX / Game1.tileSize, clickPointY / Game1.tileSize);
            if (aStarNode != null)
            {
                Game1.currentLocation.terrainFeatures.TryGetValue(new Vector2(aStarNode.X, aStarNode.Y), out var value);
                if (value is not null && value is HoeDirt && ((HoeDirt)value).crop == null && Game1.player.ActiveObject is not null && Game1.player.ActiveObject.Category == -74)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryTofindAlternatePath(AStarNode startNode)
        {
            if (this.endNodeOccupied == null || (!this.FindAlternatePath(startNode, this.ClickedNode.X + 1, this.ClickedNode.Y + 1) && !this.FindAlternatePath(startNode, this.ClickedNode.X - 1, this.ClickedNode.Y + 1) && !this.FindAlternatePath(startNode, this.ClickedNode.X + 1, this.ClickedNode.Y - 1) && !this.FindAlternatePath(startNode, this.ClickedNode.X - 1, this.ClickedNode.Y - 1)))
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
                    Game1.currentLocation.terrainFeatures.TryGetValue(new Vector2(this.ClickedNode.X, this.ClickedNode.Y), out TerrainFeature terrainFeature);
                    if (terrainFeature is HoeDirt dirt && dirt.state.Value != 1)
                    {
                        return true;
                    }
                }

                if (Game1.currentLocation is SlimeHutch && this.ClickedNode.X == 16 && this.ClickedNode.Y >= 6 && this.ClickedNode.Y <= 9)
                {
                    return true;
                }

                if (this.gameLocation.IsWateringCanFillingSource((int)this.clickedTile.X, (int)this.clickedTile.Y))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
