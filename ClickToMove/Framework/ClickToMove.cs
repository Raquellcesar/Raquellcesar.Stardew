// ------------------------------------------------------------------------------------------------
// <copyright file="ClickToMove.cs" company="Raquellcesar">
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
    ///     This class encapsulates all the details needed to implement the click to move
    ///     functionality. Each instance will be associated to a single <see
    ///     cref="StardewValley.GameLocation"/> and will maintain data to optimize path finding in
    ///     that location.
    /// </summary>
    internal class ClickToMove
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
        ///     The time we need to wait before checking gor monsters to attack again measured in number of game ticks (aproximately 500 ms).
        /// </summary>
        private const int MinimumTicksBetweenMonsterChecks = 30;

        /// <summary>
        ///     The time the mouse left button must be pressed before we condider it held measured in number of game ticks (aproximately 350 ms).
        /// </summary>
        private const int TicksBeforeClickHoldKicksIn = 21;

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

        /// <summary>
        ///     The time of the last click, measured in game ticks.
        /// </summary>
        private static int startTime = int.MaxValue;

        /// <summary>
        ///     The queue of clicks to process.
        /// </summary>
        private readonly Queue<ClickQueueItem> clickQueue = new Queue<ClickQueueItem>();

        /// <summary>
        ///     A reference to the ignoreWarps private field in a <see cref="GameLocation"/>.
        /// </summary>
        private readonly IReflectedField<bool> ignoreWarps;

        /// <summary>
        ///     The list of the indexes of the last used tools.
        /// </summary>
        private readonly Stack<int> lastToolIndexList = new Stack<int>();

        /// <summary>
        ///     A reference to the oldMariner private field in a <see cref="Beach"/> game location.
        /// </summary>
        private readonly IReflectedField<NPC> oldMariner;

        private Building actionableBuilding;

        /// <summary>
        ///     Whether the player clicked the Cinema's door.
        /// </summary>
        private bool clickedCinemaDoor;

        /// <summary>
        ///     Whether the player clicked the Cinema's ticket office.
        /// </summary>
        private bool clickedCinemaTicketBooth;

        /// <summary>
        ///     Whether the player clicked Haley's bracelet during her event.
        /// </summary>
        private bool clickedHaleyBracelet;

        /// <summary>
        ///     The node associated with the tile clicked by the player.
        /// </summary>
        private AStarNode clickedNode;

        /// <summary>
        ///     The <see cref="Horse"/> clicked at the end of the path.
        /// </summary>
        private Horse clickedHorse;

        /// <summary>
        ///     The backing field for <see cref="ClickedTile"/>.
        /// </summary>
        private Point clickedTile = new Point(-1, -1);

        /// <summary>
        ///     The backing field for <see cref="ClickPoint"/>.
        /// </summary>
        private Point clickPoint = new Point(-1, -1);

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

        private bool useToolOnEndNode;

        private bool endTileIsActionable;

        private AStarNode finalNode;

        private SObject forageItem;

        private Fence gateClickedOn;

        private AStarNode gateNode;

        private InteractionType interactionAtCursor = InteractionType.None;

        private Point invalidTarget = new Point(-1, -1);

        private bool justUsedWeapon;

        private float lastDistance = float.MaxValue;

        /// <summary>
        ///     Contains the path last computed by the A* algorithm.
        /// </summary>
        private AStarPath path;

        private bool performActionFromNeighbourTile;

        private ClickToMovePhase phase;

        /// <summary>
        ///     Whether the player has picked a previously clicked furniture.
        /// </summary>
        private bool pickedFurniture = false;

        /// <summary>
        ///     Whether the click can be queued.
        /// </summary>
        private bool queueingClicks;

        private int reallyStuckCount;

        private AStarNode startNode;

        private int stuckCount;

        /// <summary>
        ///     The tool to select at the end of the path.
        /// </summary>
        private string toolToSelect;

        /// <summary>
        ///     Counts the number of times a path is (re)computed when walking to the target.
        /// </summary>
        private int tryCount;

        private bool waitingToFinishWatering;

        private bool warping;

        private bool waterSourceSelected;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClickToMove"/> class.
        /// </summary>
        /// <param name="gameLocation">
        ///     The <see cref="GameLocation"/> associated with this object.
        /// </param>
        public ClickToMove(GameLocation gameLocation)
        {
            this.GameLocation = gameLocation;

            this.ignoreWarps = ClickToMoveManager.Reflection.GetField<bool>(gameLocation, "ignoreWarps");

            if (gameLocation is Beach)
            {
                this.oldMariner = ClickToMoveManager.Reflection.GetField<NPC>(gameLocation, "oldMariner");
            }

            this.Graph = new AStarGraph(this);
        }

        /// <summary>
        ///     Gets or sets the last <see cref="MeleeWeapon"/> used.
        /// </summary>
        public static MeleeWeapon LastMeleeWeapon { get; set; }

        /// <summary>
        ///     Gets the tile clicked by the player.
        /// </summary>
        public Point ClickedTile => this.clickedTile;

        /// <summary>
        ///     Gets a value indicating whether the mouse left buton is being held.
        /// </summary>
        public bool ClickHoldActive { get; private set; }

        /// <summary>
        ///     Gets the simulated key states for this tick.
        /// </summary>
        public ClickToMoveKeyStates ClickKeyStates { get; } = new ClickToMoveKeyStates();

        /// <summary>
        ///     Gets the point clicked by the player. In absolute coordinates.
        /// </summary>
        public Point ClickPoint => this.clickPoint;

        /// <summary>
        ///     Gets the point clicked by the player. In absolute coordinates.
        /// </summary>
        public Vector2 ClickVector => new Vector2(this.clickPoint.X, this.clickPoint.Y);

        /// <summary>
        ///     Gets the bed the Farmer is in.
        /// </summary>
        public BedFurniture CurrentBed { get; private set; }

        /// <summary>
        ///     Gets or sets a value with the clicked absolute coordinates when the mouse left click
        ///     is postponed. This happens when <see cref="Furniture"/> is selected, since we need
        ///     to wait to see if the player will hold the click.
        /// </summary>
        public Point DeferredClick { get; set; } = new Point(-1, -1);

        /// <summary>
        ///     Gets the furniture that was clicked by the player.
        /// </summary>
        public Furniture Furniture { get; private set; }

        /// <summary>
        ///     Gets the <see cref="GameLocation"/> associated to this object.
        /// </summary>
        public GameLocation GameLocation { get; private set; }

        /// <summary>
        ///     Gets or sets the grab tile to use when the Farmer uses a tool.
        /// </summary>
        public Point GrabTile { get; set; } = Point.Zero;

        /// <summary>
        ///     Gets the graph used for path finding.
        /// </summary>
        public AStarGraph Graph { get; }

        /// <summary>
        ///     Gets a value indicating whether Warps should be ignored.
        /// </summary>
        public bool IgnoreWarps => this.ignoreWarps.GetValue();

        public Point InvalidTarget => this.invalidTarget;

        /// <summary>
        ///     Gets a value indicating whether this object is controlling the Farmer's current actions.
        /// </summary>
        public bool Moving => this.phase > ClickToMovePhase.None;

        /// <summary>
        ///     Gets the Old Mariner NPC.
        /// </summary>
        public NPC OldMariner => this.oldMariner?.GetValue();

        public bool PreventMountingHorse { get; set; }

        /// <summary>
        ///     Gets the bed clicked by the player.
        /// </summary>
        public BedFurniture TargetBed { get; private set; }

        /// <summary>
        ///     Gets the <see cref="FarmAnimal"/> that's at the current goal node, if any.
        /// </summary>
        public FarmAnimal TargetFarmAnimal { get; private set; }

        /// <summary>
        ///     Gets the <see cref="NPC"/> that's at the current goal node, if any.
        /// </summary>
        public NPC TargetNpc { get; private set; }

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
        ///     Called if the mouse left button is just pressed by the player.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        public void OnClick(int x, int y)
        {
            ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClick({x}, {y})");

            if (this.IgnoreClick())
            {
                return;
            }

            ClickToMove.startTime = Game1.ticks;

            if (this.GameLocation.GetFurniture(x, y) is Furniture furniture)
            {
                // We need to wait to see it the player will be holding the click.
                this.Furniture = furniture;
                this.pickedFurniture = false;
                this.DeferredClick = new Point(x, y);
                return;
            }

            this.Furniture = null;

            this.HandleClick(x, y);
        }

        /// <summary>
        ///     Called if the mouse left button is being held by the player.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        public void OnClickHeld(int x, int y)
        {
            ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld({x}, {y}) - Game1.ticks - ClickToMove.startTime = {Game1.ticks - ClickToMove.startTime}");

            if (ClickToMoveManager.IgnoreClick
                || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks()
                || Game1.currentMinigame is FishingGame
                || Game1.ticks - ClickToMove.startTime < ClickToMove.TicksBeforeClickHoldKicksIn)
            {
                ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld not running");
                return;
            }

            this.ClickHoldActive = true;

            if (this.ClickKeyStates.RealClickHeld)
            {
                ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - this.ClickKeyStates.RealClickHeld is true");

                if (this.GameLocation.IsChoppableOrMinable(this.clickedTile)
                    && (Game1.player.CurrentTool is Axe or Pickaxe)
                    && this.phase != ClickToMovePhase.FollowingPath
                    && this.phase != ClickToMovePhase.OnFinalTile
                    && this.phase != ClickToMovePhase.ReachedEndOfPath
                    && this.phase != ClickToMovePhase.Complete)
                {
                    if (Game1.player.UsingTool)
                    {
                        this.ClickKeyStates.StopMoving();
                        this.ClickKeyStates.SetUseTool(false);
                        this.phase = ClickToMovePhase.None;
                    }
                    else
                    {
                        this.phase = ClickToMovePhase.UseTool;
                    }

                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - Chopping or mining - phase is {this.phase}");

                    return;
                }
                else if (this.waterSourceSelected
                         && Game1.player.CurrentTool is FishingRod
                         && this.phase == ClickToMovePhase.Complete)
                {
                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - Fishing");

                    this.phase = ClickToMovePhase.UseTool;
                    return;
                }
            }

            if (this.Furniture is not null)
            {
                ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - Picking furniture");

                // Pick the furniture.
                if (!this.pickedFurniture)
                {
                    this.pickedFurniture = true;
                    this.phase = ClickToMovePhase.UseTool;
                }
            }
            else
            {
                if (!Game1.player.CanMove
                    || this.warping
                    || this.GameLocation.IsChoppableOrMinable(this.clickedTile))
                {
                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - not moving");
                    return;
                }

                ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - moving");

                if (this.phase != ClickToMovePhase.KeepMoving)
                {
                    if (this.phase != ClickToMovePhase.None)
                    {
                        this.Reset();
                    }

                    this.phase = ClickToMovePhase.KeepMoving;
                    this.invalidTarget.X = this.invalidTarget.Y = -1;
                }

                Vector2 mousePosition = new Vector2(x, y);

                if ((Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival) && !Game1.player.UsingTool
                    && !this.warping && !this.IgnoreWarps && this.GameLocation.WarpIfInRange(mousePosition))
                {
                    this.Reset();
                    this.warping = true;
                }

                Vector2 playerOffsetPosition = Game1.player.OffsetPositionOnMap();

                float distanceToMouse = Vector2.Distance(playerOffsetPosition, mousePosition);
                WalkDirection walkDirectionToMouse = WalkDirection.None;
                if (distanceToMouse > Game1.tileSize / 2)
                {
                    float angleDegrees = (float)Math.Atan2(
                                             mousePosition.Y - playerOffsetPosition.Y,
                                             mousePosition.X - playerOffsetPosition.X) / ((float)Math.PI * 2)
                                         * 360;

                    walkDirectionToMouse = WalkDirection.GetWalkDirectionForAngle(angleDegrees);
                }

                this.ClickKeyStates.SetMovement(walkDirectionToMouse);
            }
        }

        /// <summary>
        ///     Called if the mouse left button was just released by the player.
        /// </summary>
        public void OnClickRelease()
        {
            ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickRelease() - Game1.player.CurrentTool.UpgradeLevel: {Game1.player.CurrentTool?.UpgradeLevel}; Game1.player.canReleaseTool: {Game1.player.canReleaseTool}");

            this.ClickHoldActive = false;

            if (ClickToMoveManager.IgnoreClick)
            {
                ClickToMoveManager.IgnoreClick = false;
            }
            else if (!ClickToMoveHelper.InMiniGameWhereWeDontWantClicks())
            {
                if (Game1.player.CurrentTool is not FishingRod and not Slingshot)
                {
                    if (Game1.player.CanMove && Game1.player.UsingTool)
                    {
                        Farmer.canMoveNow(Game1.player);
                    }

                    this.ClickKeyStates.RealClickHeld = false;
                    this.ClickKeyStates.UseToolButtonReleased = true;
                }

                if (this.Furniture is not null && !this.pickedFurniture)
                {
                    // The furniture clicked was not picked. Check if the Farmer is placing
                    // something over it.
                    if (this.Furniture.heldObject.Value is null && Game1.player.ActiveObject is Furniture)
                    {
                        this.phase = ClickToMovePhase.UseTool;
                    }
                    else if (this.DeferredClick.X != -1)
                    {
                        this.HandleClick(this.DeferredClick.X, this.DeferredClick.Y);
                    }
                }
                else if (Game1.player.CurrentTool is not null
                         && Game1.player.CurrentTool is not FishingRod
                         && Game1.player.CurrentTool.UpgradeLevel > 0
                         && Game1.player.canReleaseTool
                         && (this.phase is ClickToMovePhase.None or ClickToMovePhase.PendingComplete || Game1.player.UsingTool))
                {
                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickRelease() 1 - phase = {this.phase}");
                    this.phase = ClickToMovePhase.UseTool;
                }
                else if (Game1.player.CurrentTool is Slingshot && Game1.player.usingSlingshot)
                {
                    this.phase = ClickToMovePhase.ReleaseTool;
                }
                else if (this.phase is ClickToMovePhase.PendingComplete or ClickToMovePhase.KeepMoving)
                {
                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickRelease() 2 - phase = {this.phase}");
                    this.Reset();
                    this.CheckForQueuedClicks();
                }
            }
        }

        /// <summary>
        ///     Clears the internal state of this instance.
        /// </summary>
        /// <param name="resetKeyStates">
        ///     Whether the simulated key states should also be reset or not.
        /// </param>
        public void Reset(bool resetKeyStates = true)
        {
            this.phase = ClickToMovePhase.None;

            this.clickPoint = new Point(-1, -1);
            this.clickedTile = new Point(-1, -1);

            if (this.clickedNode is not null)
            {
                this.clickedNode.FakeTileClear = false;
            }

            this.clickedNode = null;

            this.stuckCount = 0;
            this.reallyStuckCount = 0;
            this.lastDistance = float.MaxValue;
            this.distanceToTarget = DistanceToTarget.Unknown;

            this.clickedCinemaDoor = false;
            this.clickedCinemaTicketBooth = false;
            this.endNodeOccupied = false;
            this.useToolOnEndNode = false;
            this.endTileIsActionable = false;
            this.performActionFromNeighbourTile = false;
            this.warping = false;
            this.waterSourceSelected = false;

            this.actionableBuilding = null;
            this.clickedHorse = null;
            this.crabPot = null;
            this.forageItem = null;
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

        /// <summary>
        ///     Changes the farmer's equipped tool to the last used tool. This is used to get back
        ///     to the tool that was equipped before a different tool was autoselected.
        /// </summary>
        public void SwitchBackToLastTool()
        {
            if ((this.ClickKeyStates.RealClickHeld && this.GameLocation.IsChoppableOrMinable(this.clickedTile))
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
                    ClickToMove.startTime = Game1.ticks;
                }
            }
        }

        /// <summary>
        ///     Executes the action for this tick according to the current phase.
        /// </summary>
        public void Update()
        {
            ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.Update() - phase is {this.phase}");

            this.ClickKeyStates.ClearReleasedStates();

            if (Game1.eventUp && !Game1.player.CanMove && !Game1.dialogueUp && this.phase != ClickToMovePhase.None
                && (Game1.currentSeason != "winter" || Game1.dayOfMonth != 8)
                && Game1.currentMinigame is not FishingGame)
            {
                this.Reset();
            }
            else
            {
                switch (this.phase)
                {
                    case ClickToMovePhase.FollowingPath when Game1.player.CanMove:
                        this.FollowPath();
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
                        this.CheckForQueuedClicks();
                        break;
                    case ClickToMovePhase.DoAction:
                        this.ClickKeyStates.ActionButtonPressed = true;
                        this.phase = ClickToMovePhase.FinishAction;
                        break;
                    case ClickToMovePhase.FinishAction:
                        this.ClickKeyStates.ActionButtonPressed = false;
                        this.phase = ClickToMovePhase.None;
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

        /// <summary>
        ///     Adds a click to the clicks queue, if it's not already there.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the click wasn't already in the queue and was added to the queue;
        ///     returns <see langword="false"/> otherwise.
        /// </returns>
        private bool AddToClickQueue(int x, int y)
        {
            ClickQueueItem click = new ClickQueueItem(x, y);

            if (this.clickQueue.Contains(click))
            {
                return false;
            }

            this.clickQueue.Enqueue(click);
            return true;
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
            if (Game1.player.getToolFromName(toolName) is not null)
            {
                this.toolToSelect = toolName;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Checks if the Farmer is in the bed and/or the player clicks a bed. Beds in those
        ///     situations can be traversed by the path. If a bed was clicked, it also sets the
        ///     clicked tile to the farmer's bed spot tile.
        /// </summary>
        private void CheckBed()
        {
            this.CurrentBed = null;
            this.TargetBed = null;

            foreach (Furniture furniture in this.GameLocation.furniture)
            {
                if (this.CurrentBed is not null && this.TargetBed is not null)
                {
                    break;
                }

                if (furniture is BedFurniture bed)
                {
                    if (this.CurrentBed is null
                        && bed.getBoundingBox(bed.TileLocation).Intersects(Game1.player.GetBoundingBox()))
                    {
                        this.CurrentBed = bed;
                    }

                    if (this.TargetBed is null && bed.getBoundingBox(bed.TileLocation).Contains(this.clickPoint))
                    {
                        Point bedSpot = bed.GetBedSpot();
                        this.clickedTile.X = bedSpot.X;
                        this.clickedTile.Y = bedSpot.Y;

                        this.TargetBed = bed;
                    }
                }
            }
        }

        /// <summary>
        ///     Checks whether the player interacted with the Movie Theater.
        /// </summary>
        /// <param name="node">The node clicked.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the player clicked the Movie Theater's doors or
        ///     ticket office. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CheckCinemaInteraction(AStarNode node)
        {
            if (this.Graph.GameLocation is Town
                && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
            {
                // Node contains the cinema door.
                if ((node.X is 52 or 53) && (node.Y is 18 or 19))
                {
                    this.SelectDifferentEndNode(node.X, 19);

                    this.endTileIsActionable = true;
                    this.clickedCinemaDoor = true;

                    return true;
                }

                // Node contains the cinema ticket office.
                if (node.X >= 54 && node.X <= 56 && (node.Y is 19 or 20))
                {
                    this.SelectDifferentEndNode(node.Y, 20);

                    this.endTileIsActionable = true;
                    this.performActionFromNeighbourTile = true;
                    this.clickedCinemaTicketBooth = true;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks for queueable clicks, i.e. clicks that trigger actions that can be queued. An
        ///     example of this situation is when the player waters several tiles in succession.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the processing of the current click should stop
        ///     here. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CheckForQueueableClicks(int x, int y)
        {
            if (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
            {
                if (this.queueingClicks
                    && this.AddToClickQueue(x, y)
                    && this.phase == ClickToMovePhase.None)
                {
                    this.waitingToFinishWatering = true;
                }

                return true;
            }

            if (this.queueingClicks)
            {
                Vector2 tile = new Vector2(x / Game1.tileSize, y / Game1.tileSize);

                this.GameLocation.terrainFeatures.TryGetValue(tile, out TerrainFeature terrainFeature);

                if (terrainFeature is null)
                {
                    if (this.GameLocation.Objects.TryGetValue(tile, out SObject @object))
                    {
                        if (@object.readyForHarvest.Value
                            || (@object.Name.Contains("Table") && @object.heldObject.Value is not null)
                            || @object.IsSpawnedObject
                            || (@object is IndoorPot indoorPot && indoorPot.hoeDirt.Value.readyForHarvest()))
                        {
                            this.AddToClickQueue(x, y);
                            return true;
                        }
                    }
                }
                else if (terrainFeature is HoeDirt dirt)
                {
                    if (dirt.readyForHarvest())
                    {
                        this.AddToClickQueue(x, y);
                        return true;
                    }

                    if (Game1.player.ActiveObject is not null
                        && Game1.player.ActiveObject.Category == SObject.SeedsCategory)
                    {
                        this.AddToClickQueue(x, y);
                        return false;
                    }

                    if (dirt.state.Value != HoeDirt.watered && Game1.player.CurrentTool is WateringCan wateringCan)
                    {
                        if (wateringCan.WaterLeft > 0 || Game1.player.hasWateringCanEnchantment)
                        {
                            this.AddToClickQueue(x, y);
                            return true;
                        }
                        else
                        {
                            Game1.player.doEmote(4);
                            Game1.showRedMessage(
                                Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                        }
                    }
                }

                if (Utility.canGrabSomethingFromHere((int)tile.X, (int)tile.Y, Game1.player))
                {
                    this.AddToClickQueue(x, y);
                    return true;
                }

                this.queueingClicks = false;
                this.clickQueue.Clear();
            }

            return false;
        }

        private void CheckForQueuedClicks()
        {
            if (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
            {
                this.waitingToFinishWatering = true;
                this.queueingClicks = true;
                return;
            }

            this.queueingClicks = false;

            if (this.clickQueue.Count > 0)
            {
                /*if (Game1.player.CurrentTool is WateringCan { WaterLeft: <= 0 })
                {
                    Game1.player.doEmote(4);
                    Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    this.clickQueue.Clear();
                    return;
                }*/

                ClickQueueItem clickQueueItem = this.clickQueue.Dequeue();

                this.HandleClick(
                    clickQueueItem.ClickX,
                    clickQueueItem.ClickY);

                if (Game1.player.CurrentTool is WateringCan)
                {
                    this.OnClickRelease();
                }
            }
        }

        /// <summary>
        ///     Checks if there is a monster near the Farmer to attack.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there is a monster near the Farmer to attack.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CheckToAttackMonsters()
        {
            if (Game1.player.stamina <= 0)
            {
                return false;
            }

            if (!this.enableCheckToAttackMonsters)
            {
                if (Game1.ticks < ClickToMove.MinimumTicksBetweenMonsterChecks)
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

            if (this.phase != ClickToMovePhase.FollowingPath
                && this.phase != ClickToMovePhase.OnFinalTile
                && !Game1.player.UsingTool)
            {
                Rectangle boundingBox = Game1.player.GetBoundingBox();
                Point playerPosition = boundingBox.Center;
                boundingBox.Inflate(Game1.tileSize, Game1.tileSize);

                Monster targetMonster = null;
                float minimumDistance = float.MaxValue;
                foreach (NPC character in this.GameLocation.characters)
                {
                    // Ignore knocked down mummies. Ignore armored bugs if the Farmer isn't holding
                    // a weapon with the Bug Killer enchant.
                    if (character is Monster monster
                        && !(monster is Mummy mummy && mummy.reviveTimer > 0)
                        && !(monster is Bug bug && bug.isArmoredBug && !(Game1.player.CurrentTool is MeleeWeapon meleeWeapon && meleeWeapon.hasEnchantmentOfType<BugKillerEnchantment>())))
                    {
                        float distance = ClickToMoveHelper.Distance(monster.GetBoundingBox().Center, playerPosition);

                        if (distance < minimumDistance
                            && boundingBox.Intersects(monster.GetBoundingBox())
                            && !this.IsObjectBlockingMonster(monster))
                        {
                            minimumDistance = distance;
                            targetMonster = monster;
                        }
                    }
                }

                if (targetMonster is not null)
                {
                    Game1.player.faceDirection(WalkDirection.GetFacingDirection(
                        playerPosition,
                        targetMonster.GetBoundingBox().Center));

                    if (targetMonster is RockCrab rockCrab
                        && rockCrab.IsHidingInShell()
                        && Game1.player.CurrentTool is not Pickaxe)
                    {
                        Game1.player.SelectTool("Pickaxe");
                    }
                    else if (Game1.player.CurrentTool is not MeleeWeapon)
                    {
                        if (ClickToMove.LastMeleeWeapon is not null)
                        {
                            this.lastToolIndexList.Clear();
                            Game1.player.SelectTool(ClickToMove.LastMeleeWeapon.Name);
                        }
                        else if (!Game1.player.SelectMeleeWeapon() && !Game1.player.CurrentTool.isHeavyHitter())
                        {
                            Game1.player.SelectHeavyHitter();
                        }
                    }

                    this.justUsedWeapon = true;
                    this.ClickKeyStates.SetUseTool(true);
                    this.invalidTarget.X = this.invalidTarget.Y = -1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks whether the farmer can consume whatever they're holding.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the farmer can consume the item they're holding.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CheckToConsumeItem()
        {
            if (Game1.player.ActiveObject is not null
                && (Game1.player.ActiveObject.Edibility != SObject.inedible
                    || (Game1.player.ActiveObject.name.Length >= 11
                        && Game1.player.ActiveObject.name.Substring(0, 11) == "Secret Note")))
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
                if (fence is not null && fence.gatePosition.Value != Fence.gateOpenedPosition)
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
            if (this.TargetFarmAnimal is not null
                && this.clickedTile.X != -1
                && (this.clickedTile.X != this.TargetFarmAnimal.getTileX() || this.clickedTile.Y != this.TargetFarmAnimal.getTileY()))
            {
                this.HandleClick(
                    this.TargetFarmAnimal.getStandingX() + (Game1.tileSize / 2),
                    this.TargetFarmAnimal.getStandingY() + (Game1.tileSize / 2));
            }
        }

        /// <summary>
        ///     If the targeted <see cref="NPC"/> is no longer at the clicked position, recompute a
        ///     new path to it, if possible.
        /// </summary>
        private void CheckToRetargetNPC()
        {
            if (this.TargetNpc is not null && (this.clickedTile.X != -1 || this.clickedTile.Y != -1))
            {
                if (this.TargetNpc.currentLocation != this.GameLocation
                    || this.TargetNpc.AtWarpOrDoor(this.GameLocation))
                {
                    this.Reset();
                }
                else if (this.clickedTile.X != this.TargetNpc.getTileX()
                         || this.clickedTile.Y != this.TargetNpc.getTileY())
                {
                    this.HandleClick(
                        (this.TargetNpc.getTileX() * Game1.tileSize) + (Game1.tileSize / 2),
                        (this.TargetNpc.getTileY() * Game1.tileSize) + (Game1.tileSize / 2));
                }
            }
        }

        /// <summary>
        ///     Check if the Farmer has finished watering the current tile and proceed to the next.
        /// </summary>
        private void CheckToWaterNextTile()
        {
            if (this.waitingToFinishWatering && !Game1.player.UsingTool)
            {
                this.waitingToFinishWatering = false;
                this.CheckForQueuedClicks();
            }
        }

        /// <summary>
        ///     If the Farmer has the watering can or the fishing rod equipped, checks whether they
        ///     can use the tool on the clicked tile. If that's the case, the clicked tile is set to
        ///     a water source tile that neighbours a land tile.
        /// </summary>
        /// <param name="node">The node associated to the clicked tile.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the Farmer has the watering can or the fishing rod
        ///     equipped and they can use the tool on the clicked tile. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        private bool CheckWaterSource(AStarNode node)
        {
            if ((Game1.player.CurrentTool is WateringCan && this.GameLocation.CanRefillWateringCanOnTile(node.X, node.Y))
                || (Game1.player.CurrentTool is FishingRod && this.GameLocation.canFishHere() && this.GameLocation.isTileFishable(node.X, node.Y)))
            {
                if (this.Graph.GetNearestCoastNode(node) is AStarNode landNode)
                {
                    this.clickedNode = landNode;
                    this.clickedTile.X = this.clickedNode.X;
                    this.clickedTile.Y = this.clickedNode.Y;
                }

                this.useToolOnEndNode = true;
                this.performActionFromNeighbourTile = true;
                this.waterSourceSelected = true;

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Makes the farmer's face the clicked point.
        /// </summary>
        /// <param name="faceClickPoint">
        ///     Indicates whether to use the <see cref="clickPoint"/> or instead the <see
        ///     cref="ClickedTile"/> when computing the facing direction.
        /// </param>
        private void FaceTileClicked(bool faceClickPoint = false)
        {
            int facingDirection;

            if (faceClickPoint)
            {
                facingDirection = WalkDirection.GetFacingDirection(
                    Game1.player.Position,
                    Utility.PointToVector2(this.clickPoint));
            }
            else
            {
                facingDirection = WalkDirection.GetFacingDirection(
                    Game1.player.getTileLocation(),
                    Utility.PointToVector2(this.clickedTile));
            }

            if (facingDirection != Game1.player.FacingDirection)
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

        /// <summary>
        ///     Follows the path.
        /// </summary>
        private void FollowPath()
        {
            if (this.path.Count > 0)
            {
                AStarNode farmerNode = this.Graph.FarmerNode;

                if (farmerNode is null)
                {
                    this.Reset();
                    return;
                }

                // Next node reached.
                if (this.path[0] == farmerNode)
                {
                    this.path.RemoveFirst();

                    this.lastDistance = float.MaxValue;
                    this.stuckCount = 0;
                    this.reallyStuckCount = 0;
                }

                if (this.path.Count > 0)
                {
                    // An animal or an NPC is blocking the way, we need to recompute the path.
                    if (this.path[0].ContainsAnimal()
                        || (this.path[0].GetNpc() is NPC npc && npc is not Horse && !Game1.player.isRidingHorse()))
                    {
                        this.HandleClick(this.clickPoint.X, this.clickPoint.Y);

                        return;
                    }

                    Vector2 playerOffsetPositionOnMap = Game1.player.OffsetPositionOnMap();
                    Vector2 nextNodeCenter = this.path[0].NodeCenterOnMap;
                    WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                        playerOffsetPositionOnMap,
                        nextNodeCenter,
                        Game1.player.getMovementSpeed());

                    float distanceToNextNode = Vector2.Distance(playerOffsetPositionOnMap, nextNodeCenter);

                    // No progress since last attempt.
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
                            this.reallyStuckCount++;
                            if (this.reallyStuckCount == 8)
                            {
                                if (Game1.player.isRidingHorse())
                                {
                                    this.Reset();
                                }
                                else if (this.clickedHorse is not null)
                                {
                                    this.clickedHorse.checkAction(Game1.player, this.GameLocation);

                                    this.Reset();
                                }
                                else if (this.Graph.FarmerNode.GetNpc() is Horse horse)
                                {
                                    horse.checkAction(Game1.player, this.GameLocation);
                                }
                                else
                                {
                                    // Try again.
                                    this.HandleClick(
                                        this.clickPoint.X,
                                        this.clickPoint.Y,
                                        this.tryCount + 1);
                                }

                                return;
                            }

                            walkDirection = WalkDirection.OppositeWalkDirection(walkDirection);
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
                                    playerOffsetPositionOnMap,
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
                this.phase = ClickToMovePhase.OnFinalTile;
            }
        }

        private Rectangle GetHorseAlternativeBoundingBox(Horse horse)
        {
            if (horse.FacingDirection == WalkDirection.Up.Value || horse.FacingDirection == WalkDirection.Down.Value)
            {
                return new Rectangle((int)horse.Position.X, (int)horse.Position.Y - 128, 64, 192);
            }

            return new Rectangle((int)horse.Position.X - 32, (int)horse.Position.Y, 128, 64);
        }

        /// <summary>
        ///     The method that actually handles the left clicks.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        /// <param name="tryCount">The number of times we tried to follow the path.</param>
        private void HandleClick(int x, int y, int tryCount = 0)
        {
            while (true)
            {
                ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.HandleClick({x}, {y}, {tryCount})");

                this.interactionAtCursor = InteractionType.None;

                Point clickPoint = new Point(x, y);
                Point clickedTile = new Point(clickPoint.X / Game1.tileSize, clickPoint.Y / Game1.tileSize);
                AStarNode clickedNode = this.Graph.GetNode(clickedTile.X, clickedTile.Y);

                if (clickedNode is null)
                {
                    return;
                }

                this.SetInteractionAtCursor(clickedNode.X, clickedNode.Y);

                if (this.CheckForQueueableClicks(x, y))
                {
                    return;
                }

                if (!Game1.player.CanMove)
                {
                    if (Game1.player.UsingTool && Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter())
                    {
                        Game1.player.Halt();

                        this.ClickKeyStates.SetUseTool(false);
                        this.enableCheckToAttackMonsters = false;
                        this.justUsedWeapon = false;
                    }

                    if (Game1.eventUp)
                    {
                        if ((Game1.currentSeason == "winter" && Game1.dayOfMonth == 8) || Game1.currentMinigame is FishingGame)
                        {
                            this.phase = ClickToMovePhase.UseTool;
                        }

                        if (Game1.player.CurrentTool is not FishingRod)
                        {
                            return;
                        }
                    }

                    if (Game1.player.CurrentTool is FishingRod)
                    {
                        if (Game1.currentMinigame is FishingGame && !Game1.player.UsingTool)
                        {
                            Game1.player.CanMove = true;
                        }
                        else
                        {
                            this.phase = ClickToMovePhase.UseTool;
                            return;
                        }
                    }
                }

                if (Game1.player.ClickedOn(clickPoint.X, clickPoint.Y))
                {
                    if (Game1.player.CurrentTool is Slingshot && Game1.currentMinigame is not TargetGame)
                    {
                        this.ClickKeyStates.SetUseTool(true);
                        this.ClickKeyStates.RealClickHeld = true;

                        this.phase = ClickToMovePhase.UsingSlingshot;
                        return;
                    }

                    if (Game1.player.CurrentTool is Wand)
                    {
                        this.phase = ClickToMovePhase.UseTool;
                        return;
                    }

                    if (this.CheckToConsumeItem())
                    {
                        return;
                    }
                }

                if (tryCount >= ClickToMove.MaxTries)
                {
                    this.Reset();
                    Game1.player.Halt();
                    return;
                }

                this.Reset(false);
                this.ClickKeyStates.ResetLeftOrRightClickButtons();
                this.ClickKeyStates.RealClickHeld = true;

                this.clickPoint = clickPoint;
                this.clickedTile = clickedTile;
                this.invalidTarget.X = this.invalidTarget.Y = -1;

                this.tryCount = tryCount;

                // If the Farmer is holding some furniture, let the game handle the click.
                if (Game1.player.ActiveObject is Furniture)
                {
                    this.phase = ClickToMovePhase.UseTool;
                    return;
                }

                if (this.GameLocation is DecoratableLocation decoratableLocation
                    && Game1.player.ActiveObject is Wallpaper wallpaper
                    && wallpaper.CanBePlaced(decoratableLocation, this.clickedTile.X, this.clickedTile.Y))
                {
                    this.ClickKeyStates.ActionButtonPressed = true;
                    return;
                }

                if (Game1.player.isRidingHorse()
                    && (this.GetHorseAlternativeBoundingBox(Game1.player.mount).Contains(clickPoint.X, clickPoint.Y)
                        || Game1.player.ClickedOn(clickPoint.X, clickPoint.Y))
                    && Game1.player.mount.checkAction(Game1.player, this.GameLocation))
                {
                    this.Reset();
                    return;
                }

                if (this.GameLocation.doesTileHaveProperty(this.clickedTile.X, this.clickedTile.Y, "Action", "Buildings") is string action
                    && action.Contains("Message"))
                {
                    if (!ClickToMoveHelper.ClickedEggAtEggFestival(this.ClickPoint))
                    {
                        if (!this.GameLocation.checkAction(
                                new Location(this.clickedTile.X, this.clickedTile.Y),
                                Game1.viewport,
                                Game1.player))
                        {
                            this.GameLocation.checkAction(
                                new Location(this.clickedTile.X, this.clickedTile.Y + 1),
                                Game1.viewport,
                                Game1.player);
                        }

                        this.Reset();
                        Game1.player.Halt();
                        return;
                    }
                }
                else if (this.GameLocation is Town town
                         && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
                {
                    if (this.clickedTile.X >= 48
                        && this.clickedTile.X <= 51
                        && (this.clickedTile.Y is 18 or 19))
                    {
                        town.checkAction(new Location(this.clickedTile.X, 19), Game1.viewport, Game1.player);

                        this.Reset();
                        return;
                    }
                }
                else if (this.GameLocation is Beach beach
                         && !beach.bridgeFixed
                         && (this.clickedTile.X is 58 or 59)
                         && (this.clickedTile.Y is 11 or 12))
                {
                    beach.checkAction(new Location(58, 13), Game1.viewport, Game1.player);
                }
                else if (this.GameLocation is LibraryMuseum libraryMuseum
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
                        for (int deltaY = 0; deltaY < 3; deltaY++)
                        {
                            int tileY = this.clickedTile.Y + deltaY;

                            if (libraryMuseum.doesTileHaveProperty(
                                    this.clickedTile.X,
                                    tileY,
                                    "Action",
                                    "Buildings") is string actionProperty
                                && actionProperty.Contains("Notes")
                                && libraryMuseum.checkAction(
                                    new Location(this.clickedTile.X, tileY),
                                    Game1.viewport,
                                    Game1.player))
                            {
                                return;
                            }
                        }
                    }
                }

                this.CheckBed();

                this.clickedNode = this.Graph.GetNode(this.clickedTile.X, this.clickedTile.Y);

                if (this.clickedNode is null)
                {
                    this.Reset();
                    return;
                }

                this.startNode = this.finalNode = this.Graph.FarmerNode;

                if (this.startNode is null)
                {
                    return;
                }

                if (this.CheckEndNode(this.clickedNode))
                {
                    this.endNodeOccupied = true;
                    this.clickedNode.FakeTileClear = true;
                }
                else
                {
                    this.endNodeOccupied = false;
                }

                if (this.clickedHorse is not null && Game1.player.CurrentItem is Hat)
                {
                    this.clickedHorse.checkAction(Game1.player, this.GameLocation);

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

                if (!Game1.player.isRidingHorse() && Game1.player.mount is null && !this.performActionFromNeighbourTile && !this.useToolOnEndNode)
                {
                    foreach (NPC npc in this.GameLocation.characters)
                    {
                        if (npc is Horse horse
                            && ClickToMoveHelper.Distance(this.ClickPoint, horse.GetBoundingBox().Center) < 48
                            && (this.clickedTile.X != (int)horse.getTileLocation().X
                                || this.clickedTile.Y != (int)horse.getTileLocation().Y))
                        {
                            this.Reset();

                            this.HandleClick(
                                ((int)horse.getTileLocation().X * Game1.tileSize) + (Game1.tileSize / 2),
                                ((int)horse.getTileLocation().Y * Game1.tileSize) + (Game1.tileSize / 2));

                            return;
                        }
                    }
                }

                if (this.clickedNode is not null && this.endNodeOccupied && !this.useToolOnEndNode
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

                        if (!this.clickedNode.ContainsTree()
                            && this.actionableBuilding is null
                            && !(this.GameLocation is Farm
                                 && this.clickedNode.X == 21
                                 && this.clickedNode.Y == 25
                                 && Game1.whichFarm == Farm.mountains_layout))
                        {
                            this.Reset();
                            return;
                        }
                    }
                }

                if (this.clickedNode.ContainsCinema() && !this.clickedCinemaTicketBooth && !this.clickedCinemaDoor)
                {
                    this.invalidTarget = this.clickedTile;

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

                if (this.crabPot is not null && Vector2.Distance(
                        new Vector2(
                            (this.crabPot.TileLocation.X * Game1.tileSize) + (Game1.tileSize / 2),
                            (this.crabPot.TileLocation.Y * Game1.tileSize) + (Game1.tileSize / 2)),
                        Game1.player.Position) < Game1.tileSize * 2)
                {
                    this.PerformCrabPotAction();

                    return;
                }

                this.path = this.GameLocation is AnimalHouse
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
                            this.path.RemoveLast();
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
                    && (this.useToolOnEndNode || this.performActionFromNeighbourTile))
                {
                    AStarNode neighbour = this.startNode.GetNeighbourPassable();

                    if (neighbour is null)
                    {
                        this.Reset();

                        return;
                    }

                    if (this.queueingClicks)
                    {
                        this.phase = ClickToMovePhase.UseTool;
                        return;
                    }

                    if (this.waterSourceSelected)
                    {
                        this.FaceTileClicked(true);
                        this.phase = ClickToMovePhase.UseTool;
                        return;
                    }

                    this.path.Add(neighbour);
                    this.path.Add(this.startNode);

                    this.invalidTarget.X = this.invalidTarget.Y = -1;
                    this.finalNode = this.path[this.path.Count - 1];
                    this.phase = ClickToMovePhase.FollowingPath;

                    return;
                }

                if (this.startNode.IsSameNode(this.clickedNode))
                {
                    this.invalidTarget.X = this.invalidTarget.Y = -1;

                    Warp warp = this.clickedNode.GetWarp(this.IgnoreWarps);
                    if (warp is not null && (Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival))
                    {
                        this.GameLocation.WarpIfInRange(warp);
                    }

                    this.Reset();

                    return;
                }

                if (this.startNode is not null && Game1.player.ActiveObject is not null
                                               && Game1.player.ActiveObject.name == "Crab Pot")
                {
                    this.TryToFindAlternatePath(this.startNode);

                    if (this.path is not null && this.path.Count > 0)
                    {
                        this.finalNode = this.path[this.path.Count - 1];
                    }

                    return;
                }

                if (this.endTileIsActionable)
                {
                    y += Game1.tileSize;

                    this.invalidTarget = this.clickedTile;

                    if (tryCount > 0)
                    {
                        this.invalidTarget.Y -= 1;
                    }

                    tryCount++;

                    continue;
                }

                if (this.TargetNpc is not null && this.TargetNpc.Name == "Robin"
                                               && this.GameLocation is BuildableGameLocation)
                {
                    this.TargetNpc.checkAction(Game1.player, this.GameLocation);
                }

                this.invalidTarget = this.clickedTile;

                if (tryCount > 0)
                {
                    this.invalidTarget.Y -= 1;
                }

                this.Reset();

                break;
            }
        }

        /// <summary>
        ///     Checks whether the current click should be ignored.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the current click should be ignored. Returns <see
        ///     langword="false"/> if the click needs to be looked at.
        /// </returns>
        private bool IgnoreClick()
        {
            if (Game1.player.passedOut
                    || Game1.player.FarmerSprite.isPassingOut()
                    || Game1.player.isEating
                    || Game1.player.IsBeingSick()
                    || Game1.player.IsSitting()
                    || Game1.locationRequest is not null
                    || (Game1.CurrentEvent is not null && !Game1.CurrentEvent.playerControlSequence)
                    || Game1.dialogueUp
                    || (Game1.activeClickableMenu is not null and not AnimalQueryMenu and not CarpenterMenu and not PurchaseAnimalsMenu and not MuseumMenu)
                    || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks())
            {
                return true;
            }

            if (ClickToMoveManager.IgnoreClick)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Checks whether there's an object that can block the Farmer at the given tile coordinates.
        /// </summary>
        /// <param name="tileX">The x tile coordinate.</param>
        /// <param name="tileY">The y tile coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's an object that can block the Farmer at the
        ///     given tile coordinates. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool IsBlockingObject(int tileX, int tileY)
        {
            return (this.GameLocation.objects.TryGetValue(new Vector2(tileX, tileY), out SObject @object)
                    && @object is not null
                    && @object.ParentSheetIndex >= ObjectId.GlassShards
                    && @object.ParentSheetIndex <= ObjectId.GoldenRelic)
                   || (this.Graph.GetNode(tileX, tileY)?.ContainsStumpOrBoulder() ?? false);
        }

        /// <summary>
        ///     Checks whether there's any blocking object between the Farmer and the given monster.
        /// </summary>
        /// <param name="monster">The monster near the Farmer.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a blocking object between the Farmer and
        ///     the given monster. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool IsObjectBlockingMonster(Monster monster)
        {
            Point playerTile = Game1.player.getTileLocationPoint();
            Point monsterTile = monster.getTileLocationPoint();

            int distanceX = monsterTile.X - playerTile.X;
            int distanceY = monsterTile.Y - playerTile.Y;

            if (Math.Abs(distanceX) == 2 || Math.Abs(distanceY) == 2)
            {
                return this.IsBlockingObject(playerTile.X + Math.Sign(distanceX), playerTile.Y + Math.Sign(distanceY));
            }
            else if (Math.Abs(distanceX) == 1 && Math.Abs(distanceY) == 1)
            {
                return this.IsBlockingObject(playerTile.X + Math.Sign(distanceX), 0)
                    && this.IsBlockingObject(0, playerTile.Y + Math.Sign(distanceY));
            }

            return false;
        }

        /// <summary>
        ///     Called while the farmer is at the final tile on the path.
        /// </summary>
        private void MoveOnFinalTile()
        {
            Vector2 playerOffsetPositionOnMap = Game1.player.OffsetPositionOnMap();
            Vector2 clickedNodeCenterOnMap = this.clickedNode.NodeCenterOnMap;

            float distanceToGoal = Vector2.Distance(
                    playerOffsetPositionOnMap,
                    clickedNodeCenterOnMap);

            if (distanceToGoal == this.lastDistance)
            {
                this.stuckCount++;
            }

            this.lastDistance = distanceToGoal;

            if (this.performActionFromNeighbourTile)
            {
                float deltaX = Math.Abs(clickedNodeCenterOnMap.X - playerOffsetPositionOnMap.X)
                               - Game1.player.speed;
                float deltaY = Math.Abs(clickedNodeCenterOnMap.Y - playerOffsetPositionOnMap.Y)
                               - Game1.player.speed;

                if (this.distanceToTarget != DistanceToTarget.TooFar && this.crabPot is null && Game1.player.GetBoundingBox().Intersects(this.clickedNode.TileRectangle))
                {
                    this.distanceToTarget = DistanceToTarget.TooClose;

                    WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                        clickedNodeCenterOnMap,
                        playerOffsetPositionOnMap);

                    this.ClickKeyStates.SetMovement(walkDirection);
                }
                else if (this.distanceToTarget != DistanceToTarget.TooClose
                         && this.stuckCount < ClickToMove.MaxStuckCount && Math.Max(deltaX, deltaY) > Game1.tileSize)
                {
                    this.distanceToTarget = DistanceToTarget.TooFar;

                    WalkDirection walkDirection = WalkDirection.GetWalkDirectionForAngle(
                        (float)(Math.Atan2(
                                    clickedNodeCenterOnMap.Y - playerOffsetPositionOnMap.Y,
                                    clickedNodeCenterOnMap.X - playerOffsetPositionOnMap.X)
                                / Math.PI / 2 * 360));

                    this.ClickKeyStates.SetMovement(walkDirection);
                }
                else
                {
                    this.distanceToTarget = DistanceToTarget.Unknown;
                    this.OnReachEndOfPath();
                }
            }
            else
            {
                if (distanceToGoal < Game1.player.getMovementSpeed()
                    || this.stuckCount >= ClickToMove.MaxStuckCount
                    || (this.useToolOnEndNode && distanceToGoal < Game1.tileSize)
                    || (this.endNodeOccupied && distanceToGoal < Game1.tileSize + 2))
                {
                    this.OnReachEndOfPath();
                    return;
                }

                WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                    playerOffsetPositionOnMap,
                    this.finalNode.NodeCenterOnMap,
                    Game1.player.getMovementSpeed());

                this.ClickKeyStates.SetMovement(walkDirection);
            }
        }

        /// <summary>
        ///     Selects the tool to be used for the interaction with the given object at the end of the path.
        /// </summary>
        /// <param name="object">The object to interact with.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the tool to interact with the <paramref name="object"/> was chosen. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool AutoSelectToolForObject(SObject @object)
        {
            switch (@object.Name)
            {
                case "House Plant":
                    this.AutoSelectTool("Pickaxe");
                    this.useToolOnEndNode = true;
                    return true;
                case "Stone" or "Boulder":
                    this.AutoSelectTool("Pickaxe");
                    return true;
                case "Twig":
                    this.AutoSelectTool("Axe");
                    return true;
                case "Weeds":
                    this.AutoSelectTool("Scythe");
                    return true;
            }

            if (@object.ParentSheetIndex == ObjectId.ArtifactSpot)
            {
                this.AutoSelectTool("Hoe");
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Checks for the existence of something to chop or mine at the world location represented by the given node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there is something to chop or mine at the location represented by
        ///     the <paramref name="node"/>. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CheckForChoppableorMinable(AStarNode node)
        {
            switch (this.Graph.GameLocation)
            {
                case Woods woods:
                    if (woods.stumps.Any(t => t.occupiesTile(node.X, node.Y)))
                    {
                        this.AutoSelectTool("Axe");
                        return true;
                    }

                    break;
                case Forest forest:
                    if (forest.log is not null && forest.log.occupiesTile(node.X, node.Y))
                    {
                        this.AutoSelectTool("Axe");
                        return true;
                    }

                    break;
                default:
                    foreach (ResourceClump resourceClump in this.Graph.GameLocation.resourceClumps)
                    {
                        if (resourceClump.occupiesTile(node.X, node.Y))
                        {
                            if (resourceClump.parentSheetIndex.Value is ResourceClump.hollowLogIndex or ResourceClump.stumpIndex)
                            {
                                this.AutoSelectTool("Axe");
                            }

                            if (resourceClump is GiantCrop giantCrop)
                            {
                                if (giantCrop.tile.X + 1 == node.X
                                    && giantCrop.tile.Y + 1 == node.Y)
                                {
                                    Point point = this.Graph.FarmerNode.GetNearestNeighbour(node);
                                    this.SelectDifferentEndNode(point.X, point.Y);
                                }

                                this.AutoSelectTool("Axe");
                            }
                            else
                            {
                                this.AutoSelectTool("Pickaxe");
                            }

                            return true;
                        }
                    }

                    break;
            }

            return false;
        }

        /// <summary>
        ///     Checks if there's a building interaction available at the world location represented by the given node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there is a building at the location represented by
        ///     the <paramref name="node"/>. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CheckForBuildingInteraction(AStarNode node)
        {
            if (node.GetBuilding() is Building building)
            {
                if (building.buildingType.Value == "Shipping Bin")
                {
                    this.performActionFromNeighbourTile = true;
                    return true;
                }

                if (building.buildingType.Value == "Mill")
                {
                    if (Game1.player.ActiveObject is not null
                        && (Game1.player.ActiveObject.parentSheetIndex == ObjectId.Beet
                            || Game1.player.ActiveObject.ParentSheetIndex == ObjectId.Wheat))
                    {
                        this.useToolOnEndNode = true;
                    }

                    this.performActionFromNeighbourTile = true;
                    return true;
                }

                if (building is Barn barn)
                {
                    int animalDoorTileX = barn.tileX.Value + barn.animalDoor.X;
                    int animalDoorTileY = barn.tileY.Value + barn.animalDoor.Y;

                    if ((this.clickedNode.X == animalDoorTileX || this.clickedNode.X == animalDoorTileX + 1)
                        && (this.clickedNode.Y == animalDoorTileY || this.clickedNode.Y == animalDoorTileY - 1))
                    {
                        if (this.clickedNode.Y == animalDoorTileY - 1)
                        {
                            this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);
                        }

                        this.performActionFromNeighbourTile = true;
                        return true;
                    }
                }
                else if (building is FishPond fishPond
                         && Game1.player.CurrentTool is not FishingRod
                         && Game1.player.CurrentTool is not WateringCan)
                {
                    this.actionableBuilding = fishPond;

                    Point nearestTile = this.Graph.GetNearestTileNextToBuilding(fishPond);
                    this.SelectDifferentEndNode(nearestTile.X, nearestTile.Y);

                    this.performActionFromNeighbourTile = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks for available interactions at the path's end node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the node is blocked. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        private bool CheckEndNode(AStarNode node)
        {
            this.toolToSelect = null;

            if (this.GameLocation is Beach beach)
            {
                if (Game1.CurrentEvent is not null
                    && Game1.CurrentEvent.playerControlSequenceID == "haleyBeach"
                    && node.X == Game1.CurrentEvent.playerControlTargetTile.X
                    && node.Y == Game1.CurrentEvent.playerControlTargetTile.Y)
                {
                    this.clickedHaleyBracelet = true;
                    this.useToolOnEndNode = true;
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

                this.useToolOnEndNode = true;
                this.performActionFromNeighbourTile = !tileClear;

                return !tileClear;
            }

            if (this.CheckCinemaInteraction(node))
            {
                return true;
            }

            if (this.GameLocation is CommunityCenter && node.X == 14 && node.Y == 5)
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (this.GameLocation is FarmHouse { upgradeLevel: 2 } && this.clickedNode.X == 16 && this.clickedNode.Y == 4)
            {
                this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);
                this.performActionFromNeighbourTile = true;
                return true;
            }

            Vector2 tileVector = new Vector2(node.X, node.Y);

            this.GameLocation.terrainFeatures.TryGetValue(tileVector, out TerrainFeature terrainFeature);

            if (terrainFeature is null)
            {
                this.GameLocation.Objects.TryGetValue(tileVector, out SObject @object);

                if (@object is not null)
                {
                    if (@object.readyForHarvest.Value
                        || (@object.Name.Contains("Table") && @object.heldObject.Value is not null)
                        || @object.IsSpawnedObject
                        || (@object is IndoorPot indoorPot && indoorPot.hoeDirt.Value.readyForHarvest()))
                    {
                        this.queueingClicks = true;

                        this.forageItem = this.GameLocation.getObjectAt(
                            this.clickPoint.X,
                            this.clickPoint.Y);

                        this.performActionFromNeighbourTile = true;
                        return true;
                    }

                    if (@object.ParentSheetIndex is ObjectId.Torch or ObjectId.SpiritTorch
                        && Game1.player.CurrentTool is Pickaxe or Axe)
                    {
                        this.useToolOnEndNode = true;
                        return true;
                    }

                    if (@object.Category == SObject.BigCraftableCategory)
                    {
                        if (@object.ParentSheetIndex
                            is BigCraftableId.FeedHopper
                            or BigCraftableId.Incubator
                            or BigCraftableId.Cask
                            or BigCraftableId.MiniFridge
                            or BigCraftableId.Workbench)
                        {
                            if (Game1.player.CurrentTool is Axe or Pickaxe)
                            {
                                this.useToolOnEndNode = true;
                            }

                            this.performActionFromNeighbourTile = true;
                            return true;
                        }

                        if (@object.ParentSheetIndex is ObjectId.DrumBlock or ObjectId.FluteBlock)
                        {
                            if (Game1.player.CurrentTool is Axe or Pickaxe)
                            {
                                this.useToolOnEndNode = true;
                            }

                            return true;
                        }

                        if (@object.ParentSheetIndex is >= BigCraftableId.Barrel and <= BigCraftableId.Crate3)
                        {
                            if (Game1.player.CurrentTool is null || !Game1.player.CurrentTool.isHeavyHitter())
                            {
                                this.AutoSelectTool("Pickaxe");
                            }

                            this.useToolOnEndNode = true;
                            return true;
                        }

                        if (@object is Chest chest)
                        {
                            if (chest.isEmpty() && (Game1.player.CurrentTool is Axe or Pickaxe))
                            {
                                this.useToolOnEndNode = true;
                            }

                            this.performActionFromNeighbourTile = true;
                            return true;
                        }

                        // Generic case: just use the tool.
                        if (Game1.player.CurrentTool is not null
                            && Game1.player.CurrentTool.isHeavyHitter()
                            && Game1.player.CurrentTool is not MeleeWeapon)
                        {
                            this.useToolOnEndNode = true;
                        }

                        return true;
                    }

                    if (this.AutoSelectToolForObject(@object))
                    {
                        return true;
                    }
                }
                else
                {
                    if (this.CheckForChoppableorMinable(node))
                    {
                        return true;
                    }

                    if (this.CheckForBuildingInteraction(node))
                    {
                        return true;
                    }

                    AStarNode upNode = this.Graph.GetNode(this.clickedNode.X, this.clickedNode.Y - 1);
                    if (upNode?.GetFurnitureNoRug()?.ParentSheetIndex == FurnitureId.Calendar)
                    {
                        this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }
                }
            }
            else if (terrainFeature is HoeDirt dirt)
            {
                if (dirt.crop is Crop crop)
                {
                    if (crop.dead.Value)
                    {
                        if (Game1.player.CurrentTool is not Hoe)
                        {
                            this.AutoSelectTool("Scythe");
                        }

                        this.useToolOnEndNode = true;
                        return true;
                    }

                    if (crop.ReadyToHarvest())
                    {
                        this.queueingClicks = true;

                        this.forageItem = this.GameLocation.getObjectAt(
                            this.clickPoint.X,
                            this.clickPoint.Y);

                        this.performActionFromNeighbourTile = true;
                        return true;
                    }

                    if (Game1.player.CurrentTool is Pickaxe)
                    {
                        this.useToolOnEndNode = true;
                        this.performActionFromNeighbourTile = true;
                        return true;
                    }
                }
                else
                {
                    if (Game1.player.CurrentTool is Pickaxe)
                    {
                        this.useToolOnEndNode = true;
                        return true;
                    }

                    if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.Category == SObject.SeedsCategory)
                    {
                        this.queueingClicks = true;
                    }
                }

                if (dirt.state.Value != HoeDirt.watered && Game1.player.CurrentTool is WateringCan wateringCan)
                {
                    if (wateringCan.WaterLeft > 0 || Game1.player.hasWateringCanEnchantment)
                    {
                        this.queueingClicks = true;
                    }
                    else
                    {
                        Game1.player.doEmote(4);
                        Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    }
                }
            }
            else
            {
                if (terrainFeature is Tree or FruitTree)
                {
                    if (terrainFeature.isPassable())
                    {
                        // The Farmer is removing a tree seed from the ground.
                        if (Game1.player.CurrentTool is Hoe or Axe or Pickaxe)
                        {
                            this.useToolOnEndNode = true;
                            return true;
                        }
                    }
                    else
                    {
                        if (terrainFeature is Tree tree)
                        {
                            this.AutoSelectTool(tree.growthStage.Value <= 1 ? "Scythe" : "Axe");
                        }

                        return true;
                    }
                }
            }

            if (this.Furniture is not null)
            {
                if (this.Furniture.ParentSheetIndex is FurnitureId.Catalogue or FurnitureId.FurnitureCatalogue or FurnitureId.SamsBoombox || this.Furniture.furniture_type.Value is Furniture.fireplace or Furniture.torch || this.Furniture is StorageFurniture or TV || this.Furniture.GetSeatCapacity() > 0)
                {
                    this.performActionFromNeighbourTile = true;
                    return true;
                }

                if (this.Furniture.furniture_type.Value == Furniture.lamp)
                {
                    this.performActionFromNeighbourTile = true;
                    this.useToolOnEndNode = false;
                    return true;
                }

                if (this.Furniture.ParentSheetIndex == FurnitureId.SingingStone)
                {
                    this.Furniture.PlaySingingStone();

                    this.performActionFromNeighbourTile = true;
                    this.useToolOnEndNode = false;
                    this.clickedTile.X = this.clickedTile.Y = -1;
                    return true;
                }
            }

            if (Game1.player.CurrentTool is not null && Game1.player.CurrentTool.isHeavyHitter() && !(Game1.player.CurrentTool is MeleeWeapon))
            {
                if (node.ContainsFence())
                {
                    this.performActionFromNeighbourTile = true;
                    this.useToolOnEndNode = true;

                    return true;
                }
            }

            if (this.GameLocation.ContainsTravellingCart(this.clickPoint.X, this.clickPoint.Y))
            {
                if (this.clickedNode.Y != 11 || (this.clickedNode.X != 23 && this.clickedNode.X != 24))
                {
                    this.SelectDifferentEndNode(27, 11);
                }

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (this.GameLocation.ContainsTravellingDesertShop(this.clickPoint.X, this.clickPoint.Y)
                && (this.clickedNode.Y is 23 or 24))
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

            if (this.GameLocation.IsTreeLogAt(this.clickedNode.X, this.clickedNode.Y))
            {
                this.performActionFromNeighbourTile = true;
                this.useToolOnEndNode = true;

                this.AutoSelectTool("Axe");

                return true;
            }

            if (this.GameLocation is Farm farm
                && node.X == farm.petBowlPosition.X
                && node.Y == farm.petBowlPosition.Y)
            {
                this.AutoSelectTool("Watering Can");
                this.useToolOnEndNode = true;
                return true;
            }

            if (this.GameLocation is SlimeHutch && node.X == 16 && node.Y >= 6 && node.Y <= 9)
            {
                this.AutoSelectTool("Watering Can");
                this.useToolOnEndNode = true;
                return true;
            }

            NPC npc = node.GetNpc();
            if (npc is Horse horse)
            {
                this.clickedHorse = horse;

                if (Game1.player.CurrentItem is not Hat)
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (Utility.canGrabSomethingFromHere(
                    this.clickPoint.X,
                    this.clickPoint.Y,
                    Game1.player))
            {
                this.queueingClicks = true;

                this.forageItem = this.GameLocation.getObjectAt(
                    this.clickPoint.X,
                    this.clickPoint.Y);

                this.performActionFromNeighbourTile = true;

                return true;
            }

            if (this.GameLocation is FarmHouse farmHouse)
            {
                Point bedSpot = farmHouse.getBedSpot();

                if (bedSpot.X == node.X && bedSpot.Y == node.Y)
                {
                    this.useToolOnEndNode = false;
                    this.performActionFromNeighbourTile = false;

                    return false;
                }
            }

            npc = this.GameLocation.isCharacterAtTile(new Vector2(this.clickedTile.X, this.clickedTile.Y));
            if (npc is not null)
            {
                this.performActionFromNeighbourTile = true;

                this.TargetNpc = npc;

                if (npc is Horse horse2)
                {
                    this.clickedHorse = horse2;

                    if (Game1.player.CurrentItem is not Hat)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (this.GameLocation is MineShaft && Game1.player.CurrentTool is not null
                                                       && Game1.player.CurrentTool is Pickaxe)
                {
                    this.useToolOnEndNode = true;
                }

                return true;
            }

            npc = this.GameLocation.isCharacterAtTile(new Vector2(this.clickedTile.X, this.clickedTile.Y + 1));

            if (npc is not null && !(npc is Duggy) && !(npc is Grub) && !(npc is LavaCrab) && !(npc is MetalHead)
                && !(npc is RockCrab) && !(npc is GreenSlime))
            {
                this.SelectDifferentEndNode(this.clickedNode.X, this.clickedNode.Y + 1);

                this.performActionFromNeighbourTile = true;
                this.TargetNpc = npc;

                if (npc is Horse horse3)
                {
                    this.clickedHorse = horse3;

                    if (Game1.player.CurrentItem is not Hat)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (this.GameLocation is MineShaft && Game1.player.CurrentTool is not null
                                                       && Game1.player.CurrentTool is Pickaxe)
                {
                    this.useToolOnEndNode = true;
                }

                return true;
            }

            if (this.GameLocation is Farm && node.Y == 13 && (node.X is 71 or 72)
                && this.clickedHorse is null)
            {
                this.SelectDifferentEndNode(node.X, node.Y + 1);

                this.endTileIsActionable = true;

                return true;
            }

            this.TargetFarmAnimal = this.GameLocation.GetFarmAnimal(this.clickPoint.X, this.clickPoint.Y);

            if (this.TargetFarmAnimal is not null)
            {
                if (this.TargetFarmAnimal.getTileX() != this.clickedNode.X
                    || this.TargetFarmAnimal.getTileY() != this.clickedNode.Y)
                {
                    this.SelectDifferentEndNode(this.TargetFarmAnimal.getTileX(), this.TargetFarmAnimal.getTileY());
                }

                if (this.TargetFarmAnimal.wasPet
                    && this.TargetFarmAnimal.currentProduce > 0
                    && this.TargetFarmAnimal.age >= this.TargetFarmAnimal.ageWhenMature
                    && Game1.player.couldInventoryAcceptThisObject(this.TargetFarmAnimal.currentProduce, 1)
                    && this.AutoSelectTool(this.TargetFarmAnimal.toolUsedForHarvest?.Value))
                {
                    return true;
                }

                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.player.ActiveObject is not null
                && (Game1.player.ActiveObject is not Wallpaper || this.GameLocation is DecoratableLocation)
                && (Game1.player.ActiveObject.bigCraftable.Value
                    || ClickToMove.ActionableObjectIds.Contains(Game1.player.ActiveObject.ParentSheetIndex)
                    || (Game1.player.ActiveObject is Wallpaper
                        && Game1.player.ActiveObject.ParentSheetIndex <= 40)))
            {
                if (Game1.player.ActiveObject.ParentSheetIndex == ObjectId.MegaBomb)
                {
                    Building building = this.clickedNode.GetBuilding();

                    if (building is FishPond)
                    {
                        this.actionableBuilding = building;

                        Point nearestTile = this.Graph.GetNearestTileNextToBuilding(building);

                        this.SelectDifferentEndNode(nearestTile.X, nearestTile.Y);
                    }
                }

                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (this.GameLocation is Mountain && node.X == 29 && node.Y == 9)
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (this.clickedNode.GetCrabPot() is CrabPot crabPot)
            {
                this.crabPot = crabPot;
                this.performActionFromNeighbourTile = true;

                AStarNode neighbour = node.GetNearestLandNodeToCrabPot();

                if (node != neighbour)
                {
                    this.clickedNode = neighbour;

                    return false;
                }

                return true;
            }

            if (!node.TileClear)
            {
                if (node.ContainsBoulder())
                {
                    this.AutoSelectTool("Pickaxe");

                    return true;
                }

                if (this.GameLocation is Town && node.X == 108 && node.Y == 41)
                {
                    this.performActionFromNeighbourTile = true;
                    this.useToolOnEndNode = true;
                    this.endTileIsActionable = true;

                    return true;
                }

                if (this.GameLocation is Town && node.X == 100 && node.Y == 66)
                {
                    this.performActionFromNeighbourTile = true;
                    this.useToolOnEndNode = true;

                    return true;
                }

                Bush bush = node.GetBush();
                if (bush is not null)
                {
                    if (Game1.player.CurrentTool is Axe && bush.IsDestroyable(
                            this.GameLocation,
                            this.clickedTile))
                    {
                        this.useToolOnEndNode = true;
                        this.performActionFromNeighbourTile = true;

                        return true;
                    }

                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                if (this.GameLocation.IsOreAt(this.clickedTile) && this.AutoSelectTool("Copper Pan"))
                {
                    AStarNode nearestNode = this.Graph.GetCoastNodeNearestWaterSource(this.clickedNode);

                    if (nearestNode is not null)
                    {
                        this.clickedNode = nearestNode;
                        this.clickedTile = new Point(nearestNode.X, nearestNode.Y);

                        Vector2 nodeCenter = nearestNode.NodeCenterOnMap;
                        this.clickPoint = new Point((int)nodeCenter.X, (int)nodeCenter.Y);

                        this.useToolOnEndNode = true;

                        return true;
                    }
                }

                if (this.CheckWaterSource(node))
                {
                    return true;
                }

                if (this.GameLocation.IsWizardBuilding(
                    new Vector2(this.clickedTile.X, this.clickedTile.Y)))
                {
                    this.performActionFromNeighbourTile = true;

                    return true;
                }

                this.endTileIsActionable =
                    this.GameLocation.isActionableTile(this.clickedNode.X, this.clickedNode.Y, Game1.player)
                    || this.GameLocation.isActionableTile(this.clickedNode.X, this.clickedNode.Y + 1, Game1.player);

                if (!this.endTileIsActionable)
                {
                    Tile tile = this.GameLocation.map.GetLayer("Buildings").PickTile(
                        new Location(this.clickedTile.X * Game1.tileSize, this.clickedTile.Y * Game1.tileSize),
                        Game1.viewport.Size);

                    this.endTileIsActionable = tile is not null;
                }

                return true;
            }

            this.GameLocation.terrainFeatures.TryGetValue(
                new Vector2(node.X, node.Y),
                out TerrainFeature terrainFeature2);

            if (terrainFeature2 is not null)
            {
                if (terrainFeature2 is Grass && Game1.player.CurrentTool is not null
                                             && Game1.player.CurrentTool is MeleeWeapon meleeWeapon
                                             && meleeWeapon.type.Value != MeleeWeapon.club)
                {
                    this.useToolOnEndNode = true;

                    return true;
                }

                if (terrainFeature2 is Flooring && Game1.player.CurrentTool is not null
                                                && (Game1.player.CurrentTool is Pickaxe or Axe))
                {
                    this.useToolOnEndNode = true;

                    return true;
                }
            }

            if (Game1.player.CurrentTool is FishingRod && this.GameLocation is Town && this.clickedNode.X >= 50
                && this.clickedNode.X <= 53 && this.clickedNode.Y >= 103 && this.clickedNode.Y <= 105)
            {
                this.waterSourceSelected = true;

                this.clickedNode = this.Graph.GetNode(52, this.clickedNode.Y);
                this.clickedTile = new Point(this.clickedNode.X, this.clickedNode.Y);

                this.useToolOnEndNode = true;

                return true;
            }

            if (Game1.player.ActiveObject is not null
                && (Game1.player.ActiveObject.bigCraftable
                    || Game1.player.ActiveObject.ParentSheetIndex is 104 or ObjectId.WarpTotemFarm or ObjectId.WarpTotemMountains or ObjectId.WarpTotemBeach or ObjectId.RainTotem or ObjectId.WarpTotemDesert or 161 or 155 or 162 || Game1.player.ActiveObject.name.Contains("Sapling"))
                && Game1.player.ActiveObject.canBePlacedHere(
                    this.GameLocation,
                    new Vector2(this.clickedTile.X, this.clickedTile.Y)))
            {
                this.performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.player.mount is null)
            {
                this.useToolOnEndNode = ClickToMoveHelper.HoeSelectedAndTileHoeable(this.GameLocation, this.clickedTile);

                if (this.useToolOnEndNode)
                {
                    this.performActionFromNeighbourTile = true;
                }
            }

            if (!this.useToolOnEndNode)
            {
                this.useToolOnEndNode = this.WateringCanActionAtEndNode();
            }

            if (!this.useToolOnEndNode && Game1.player.ActiveObject is not null)
            {
                this.useToolOnEndNode = Game1.player.ActiveObject.isPlaceable()
                                           && Game1.player.ActiveObject.canBePlacedHere(
                                               this.GameLocation,
                                               new Vector2(this.clickedTile.X, this.clickedTile.Y));

                Crop crop = new Crop(Game1.player.ActiveObject.ParentSheetIndex, node.X, node.Y);
                if (crop is not null && (Game1.player.ActiveObject.parentSheetIndex == ObjectId.BasicFertilizer
                                         || Game1.player.ActiveObject.ParentSheetIndex == ObjectId.QualityFertilizer))
                {
                    this.useToolOnEndNode = true;
                }

                if (crop is not null && crop.raisedSeeds.Value)
                {
                    this.performActionFromNeighbourTile = true;
                }
            }

            if (this.GameLocation.isActionableTile(node.X, node.Y, Game1.player) || this.GameLocation.isActionableTile(node.X, node.Y + 1, Game1.player) || this.interactionAtCursor == InteractionType.Speech)
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
                    this.useToolOnEndNode = true;
                    this.performActionFromNeighbourTile = true;
                }
                else
                {
                    this.endTileIsActionable = true;
                }
            }

            if (node.GetWarp(this.IgnoreWarps) is not null)
            {
                this.useToolOnEndNode = false;

                return false;
            }

            if (!this.useToolOnEndNode)
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

            return this.useToolOnEndNode;
        }

        /// <summary>
        ///     Method called when the Farmer has completed the eventual interaction at the end of the path.
        /// </summary>
        private void OnClickToMoveComplete()
        {
            if ((Game1.CurrentEvent is null || !Game1.CurrentEvent.isFestival)
                && !Game1.player.UsingTool
                && this.clickedHorse is null
                && !this.warping
                && !this.IgnoreWarps
                && this.GameLocation.WarpIfInRange(this.ClickVector))
            {
                this.warping = true;
            }

            this.Reset();
            this.CheckForQueuedClicks();
        }

        /// <summary>
        ///     Method called after the Farmer reaches the end of the path.
        /// </summary>
        private void OnReachEndOfPath()
        {
            this.AutoSelectPendingTool();

            if (this.endNodeOccupied)
            {
                WalkDirection walkDirection;
                if (this.useToolOnEndNode)
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
                                || (Game1.player.CurrentTool is WateringCan && this.waterSourceSelected)))
                        {
                            Game1.player.faceDirection(
                                WalkDirection.GetFacingDirection(
                                    Game1.player.OffsetPositionOnMap(),
                                    this.ClickVector));
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
                            Game1.smallestTileSize);
                    }
                }

                if (walkDirection == WalkDirection.None)
                {
                    walkDirection = this.ClickKeyStates.LastWalkDirection;
                }

                this.ClickKeyStates.SetMovement(walkDirection);

                if (this.useToolOnEndNode || !this.PerformAction())
                {
                    if (Game1.player.CurrentTool is WateringCan)
                    {
                        this.FaceTileClicked(true);
                    }

                    if (Game1.player.CurrentTool is not FishingRod || this.waterSourceSelected)
                    {
                        this.GrabTile = this.clickedTile;
                        if (!this.GameLocation.IsChoppableOrMinable(this.clickedTile))
                        {
                            this.clickedTile.X = -1;
                            this.clickedTile.Y = -1;
                        }

                        if (Game1.CurrentEvent is not null && this.clickedHaleyBracelet)
                        {
                            Game1.CurrentEvent.receiveActionPress(Game1.CurrentEvent.playerControlTargetTile.X, Game1.CurrentEvent.playerControlTargetTile.Y);
                            this.clickedHaleyBracelet = false;
                        }
                        else
                        {
                            this.ClickKeyStates.SetUseTool(true);
                        }
                    }
                }
            }
            else if (this.useToolOnEndNode)
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
                this.GameLocation.checkAction(new Location(55, 20), Game1.viewport, Game1.player);
                return true;
            }

            if (this.clickedCinemaDoor)
            {
                this.clickedCinemaDoor = false;
                this.GameLocation.checkAction(new Location(53, 19), Game1.viewport, Game1.player);
                return true;
            }

            if ((this.endTileIsActionable || this.performActionFromNeighbourTile) && Game1.player.mount is not null
                && this.forageItem is not null)
            {
                this.GameLocation.checkAction(
                    new Location(this.clickedNode.X, this.clickedNode.Y),
                    Game1.viewport,
                    Game1.player);
                this.forageItem = null;
                return true;
            }

            if (this.clickedHorse is not null)
            {
                this.clickedHorse.checkAction(Game1.player, this.GameLocation);

                this.Reset();

                return false;
            }

            if (Game1.player.mount is not null && this.clickedHorse is null)
            {
                Game1.player.mount.SetCheckActionEnabled(false);
            }

            if (this.GameLocation.isActionableTile(this.clickedTile.X, this.clickedTile.Y, Game1.player)
                && !this.clickedNode.ContainsGate())
            {
                if (this.GameLocation.IsChoppableOrMinable(this.clickedTile))
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

            if (this.GameLocation is Farm && this.GameLocation.isActionableTile(
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
                this.TargetNpc.checkAction(Game1.player, this.GameLocation);

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
                else
                {
                    this.crabPot.checkForAction(Game1.player);
                }

                this.crabPot = null;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Selects a new end node for the current path computation, at the given tile coordinates.
        /// </summary>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        private void SelectDifferentEndNode(int tileX, int tileY)
        {
            AStarNode node = this.Graph.GetNode(tileX, tileY);
            if (node is not null)
            {
                this.clickedNode = node;
                this.clickedTile.X = tileX;
                this.clickedTile.Y = tileY;
                this.clickPoint.X = (tileX * Game1.tileSize) + (Game1.tileSize / 2);
                this.clickPoint.Y = (tileY * Game1.tileSize) + (Game1.tileSize / 2);
            }
        }

        /// <summary>
        ///     Determines the interaction available to the farmer at a clicked tile.
        /// </summary>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        private void SetInteractionAtCursor(int tileX, int tileY)
        {
            Vector2 tileVector = new Vector2(tileX, tileY);

            if (Game1.currentLocation.isCharacterAtTile(tileVector) is NPC character && !character.IsMonster && !character.IsInvisible)
            {
                if ((!Game1.eventUp || Game1.CurrentEvent is null || Game1.CurrentEvent.playerControlSequence)
                    && (Game1.currentLocation is MovieTheater
                        || (!(Game1.player.ActiveObject is not null
                              && Game1.player.ActiveObject.canBeGivenAsGift()
                              && !Game1.player.isRidingHorse()
                              && character.isVillager()
                              && ((Game1.player.friendshipData.ContainsKey(character.Name)
                                   && Game1.player.friendshipData[character.Name].GiftsToday != 1)
                                  || Game1.NPCGiftTastes.ContainsKey(character.Name))
                              && !Game1.eventUp)
                            && character.canTalk()
                            && ((character.CurrentDialogue != null && character.CurrentDialogue.Count > 0)
                                || (Game1.player.spouse is not null
                                    && character.Name == Game1.player.spouse
                                    && character.shouldSayMarriageDialogue.Value
                                    && character.currentMarriageDialogue is not null
                                    && character.currentMarriageDialogue.Count > 0)
                                || character.hasTemporaryMessageAvailable()
                                || (Game1.player.hasClubCard && character.Name.Equals("Bouncer") && Game1.player.IsLocalPlayer)
                                || (character.Name.Equals("Henchman")
                                    && character.currentLocation.Name.Equals("WitchSwamp")
                                    && !Game1.player.hasOrWillReceiveMail("henchmanGone")))
                            && !character.isOnSilentTemporaryMessage())))
                {
                    this.interactionAtCursor = InteractionType.Speech;
                }
                else if (this.GameLocation.currentEvent is not null)
                {
                    NPC festivalHost = ClickToMoveManager.Reflection.GetField<NPC>(this.GameLocation.currentEvent, "festivalHost").GetValue();

                    if (festivalHost is not null && festivalHost.getTileLocation().Equals(tileVector))
                    {
                        this.interactionAtCursor = InteractionType.Speech;
                    }
                }
            }
        }

        /// <summary>
        ///     Stops the player after reaching the end of the path and checks if they are engaged
        ///     in some ongoing activity (mining, chopping, watering, hoeing).
        /// </summary>
        private void StopMovingAfterReachingEndOfPath()
        {
            this.ClickKeyStates.SetMovement(WalkDirection.None);

            this.ClickKeyStates.ActionButtonPressed = false;

            if (this.ClickKeyStates.RealClickHeld
                && (((Game1.player.CurrentTool is Axe or Pickaxe)
                     && this.GameLocation.IsChoppableOrMinable(this.clickedTile))
                    || (Game1.player.UsingTool
                        && (Game1.player.CurrentTool is WateringCan or Hoe))))
            {
                this.ClickKeyStates.SetUseTool(true);

                this.phase = ClickToMovePhase.PendingComplete;

                return;
            }

            this.ClickKeyStates.SetUseTool(false);
            this.phase = ClickToMovePhase.Complete;
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
                    this.GameLocation.terrainFeatures.TryGetValue(
                        new Vector2(this.clickedNode.X, this.clickedNode.Y),
                        out TerrainFeature terrainFeature);

                    if (terrainFeature is HoeDirt dirt && dirt.state.Value != HoeDirt.watered)
                    {
                        return true;
                    }
                }

                if ((this.GameLocation is SlimeHutch
                     && this.clickedNode.X == 16 && this.clickedNode.Y >= 6
                     && this.clickedNode.Y <= 9)
                    || this.GameLocation.CanRefillWateringCanOnTile(this.clickedTile.X, this.clickedTile.Y))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
