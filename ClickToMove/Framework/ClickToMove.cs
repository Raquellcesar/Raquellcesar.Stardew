// ------------------------------------------------------------------------------------------------
// <copyright file="ClickToMove.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

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

using Object = StardewValley.Object;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using SObject = Object;

    /// <summary>
    ///     This class encapsulates all the details needed to implement the click to move
    ///     functionality. Each instance will be associated to a single
    ///     <see
    ///         cref="StardewValley.GameLocation" />
    ///     and will maintain data to optimize path finding in
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
        ///     The time the mouse left button must be pressed before we consider it held measured in number of game ticks
        ///     (approximately 350 ms).
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
                746, 326
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
        ///     A reference to the ignoreWarps private field in a <see cref="GameLocation" />.
        /// </summary>
        private readonly IReflectedField<bool> ignoreWarps;

        /// <summary>
        ///     The list of the indexes of the last used tools.
        /// </summary>
        private readonly Stack<int> lastToolIndexList = new Stack<int>();

        /// <summary>
        ///     A reference to the oldMariner private field in a <see cref="Beach" /> game location.
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
        ///     The <see cref="Horse" /> clicked at the end of the path.
        /// </summary>
        private Horse clickedHorse;

        /// <summary>
        ///     The backing field for <see cref="ClickedTile" />.
        /// </summary>
        private Vector2 clickedTile = new Vector2(-1, -1);

        /// <summary>
        ///     The backing field for <see cref="ClickPoint" />.
        /// </summary>
        private Vector2 clickPoint = new Vector2(-1, -1);

        private CrabPot crabPot;

        private DistanceToTarget distanceToTarget;

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

        private Vector2 invalidTarget = new Vector2(-1, -1);

        private bool justUsedWeapon;

        private float lastDistance = float.MaxValue;

        /// <summary>
        ///     Contains the path last computed by the A* algorithm.
        /// </summary>
        private AStarPath path;

        private bool performActionFromNeighbourTile;

        private ClickToMovePhase phase;

        /// <summary>
        ///     Whether the player is picking up furniture. This means the player is holding the left mouse button over
        ///     the furniture.
        /// </summary>
        private bool pickedFurniture;

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

        private bool waterSourceSelected;

        /// <summary>
        ///     The furniture that was clicked by the player.
        /// </summary>
        private Furniture furniture;

        /// <summary>
        ///     Set to true when the player has clicked some furniture that can be picked. It signals that we're waiting to see if
        ///     the player will be holding the click.
        /// </summary>
        private bool waitingToPickFurniture;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClickToMove" /> class.
        /// </summary>
        /// <param name="gameLocation">
        ///     The <see cref="GameLocation" /> associated with this object.
        /// </param>
        public ClickToMove(GameLocation gameLocation)
        {
            GameLocation = gameLocation;

            ignoreWarps = ClickToMoveManager.Reflection.GetField<bool>(gameLocation, "ignoreWarps");

            if (gameLocation is Beach)
            {
                oldMariner = ClickToMoveManager.Reflection.GetField<NPC>(gameLocation, "oldMariner");
            }

            Graph = new AStarGraph(this);
        }

        /// <summary>
        ///     Gets or sets the last <see cref="MeleeWeapon" /> used.
        /// </summary>
        public static MeleeWeapon LastMeleeWeapon { get; set; }

        /// <summary>
        ///     Gets the tile clicked by the player.
        /// </summary>
        public Vector2 ClickedTile => clickedTile;

        /// <summary>
        ///     Gets a value indicating whether the mouse left button is being held.
        /// </summary>
        public bool ClickHoldActive { get; private set; }

        /// <summary>
        ///     Gets the simulated key states for this tick.
        /// </summary>
        public ClickToMoveKeyStates ClickKeyStates { get; } = new ClickToMoveKeyStates();

        /// <summary>
        ///     Gets the point clicked by the player. In absolute coordinates.
        /// </summary>
        public Vector2 ClickPoint => clickPoint;

        /// <summary>
        ///     Gets the bed the Farmer is in.
        /// </summary>
        public BedFurniture CurrentBed { get; private set; }

        /// <summary>
        ///     Gets or sets a value with the clicked absolute coordinates when the mouse left click
        ///     is postponed. This happens when <see cref="Furniture" /> is selected, since we need
        ///     to wait to see if the player will hold the click.
        /// </summary>
        public Vector2 DeferredClick { get; set; } = new Vector2(-1, -1);

        /// <summary>
        ///     Gets the <see cref="GameLocation" /> associated to this object.
        /// </summary>
        public GameLocation GameLocation { get; }

        /// <summary>
        ///     Gets or sets the grab tile to use when the Farmer uses a tool.
        /// </summary>
        public Vector2 GrabTile { get; set; } = Vector2.Zero;

        /// <summary>
        ///     Gets the graph used for path finding.
        /// </summary>
        public AStarGraph Graph { get; }

        public Vector2 InvalidTarget => invalidTarget;

        /// <summary>
        ///     Gets a value indicating whether this object is controlling the Farmer's current actions.
        /// </summary>
        public bool Moving => phase > ClickToMovePhase.None;

        /// <summary>
        ///     Gets the Old Mariner NPC.
        /// </summary>
        public NPC OldMariner => oldMariner?.GetValue();

        public bool PreventMountingHorse { get; set; }

        /// <summary>
        ///     Gets the bed clicked by the player.
        /// </summary>
        public BedFurniture TargetBed { get; private set; }

        /// <summary>
        ///     Gets the <see cref="FarmAnimal" /> that's at the current goal node, if any.
        /// </summary>
        public FarmAnimal TargetFarmAnimal { get; private set; }

        /// <summary>
        ///     Gets the <see cref="NPC" /> that's at the current goal node, if any.
        /// </summary>
        public NPC TargetNpc { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether Warps should be ignored.
        /// </summary>
        private bool IgnoreWarps => ignoreWarps.GetValue();

        /// <summary>
        ///     Clears all data relative to auto selection of tools.
        /// </summary>
        public void ClearAutoSelectTool()
        {
            lastToolIndexList.Clear();
            toolToSelect = null;
        }

        /// <summary>
        ///     (Re)Initializes the graph used by this instance.
        /// </summary>
        public void Init()
        {
            Graph.Init(); // = new AStarGraph(this.gameLocation);
        }

        /// <summary>
        ///     Called if the mouse left button is just pressed by the player.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        public void OnClick(int x, int y)
        {
            if (IgnoreClick())
            {
                return;
            }

            ClickToMove.startTime = Game1.ticks;

            furniture = GameLocation.GetFurniture(x, y);
            ClickToMoveManager.Monitor.Log(
                $"Tick {Game1.ticks} -> ClickToMove.OnClick({x}, {y}) - this.Furniture = {furniture}");

            if (furniture is not null
                && (GameLocation.CanFreePlaceFurniture() || furniture.IsCloseEnoughToFarmer(Game1.player)))
            {
                ClickToMoveManager.Monitor.Log(
                    $"Tick {Game1.ticks} -> ClickToMove.OnClick({x}, {y}) - Waiting to pick furniture");

                // We need to wait to see it the player will be holding the click.
                waitingToPickFurniture = true;
                pickedFurniture = false;
                DeferredClick = new Vector2(x, y);
                return;
            }

            HandleClick(x, y);
        }

        /// <summary>
        ///     Called if the mouse left button is being held by the player.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        public void OnClickHeld(int x, int y)
        {
            ClickToMoveManager.Monitor.Log(
                $"Tick {Game1.ticks} -> ClickToMove.OnClickHeld({x}, {y}) - Game1.ticks - ClickToMove.startTime = {Game1.ticks - ClickToMove.startTime}");

            if (ClickToMoveManager.IgnoreClick
                || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks()
                || Game1.currentMinigame is FishingGame
                || Game1.ticks - ClickToMove.startTime < ClickToMove.TicksBeforeClickHoldKicksIn)
            {
                ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld not running");
                return;
            }

            ClickHoldActive = true;

            if (ClickKeyStates.RealClickHeld)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - this.ClickKeyStates.RealClickHeld is true");

                if (GameLocation.IsChoppableOrMinable(clickedTile)
                    && Game1.player.CurrentTool is Axe or Pickaxe
                    && phase != ClickToMovePhase.FollowingPath
                    && phase != ClickToMovePhase.OnFinalTile
                    && phase != ClickToMovePhase.ReachedEndOfPath
                    && phase != ClickToMovePhase.Complete)
                {
                    if (Game1.player.UsingTool)
                    {
                        ClickKeyStates.StopMoving();
                        ClickKeyStates.SetUseTool(false);
                        phase = ClickToMovePhase.None;
                    }
                    else
                    {
                        phase = ClickToMovePhase.UseTool;
                    }

                    ClickToMoveManager.Monitor.Log(
                        $"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - Chopping or mining - phase is {phase}");

                    return;
                }

                if (waterSourceSelected
                    && Game1.player.CurrentTool is FishingRod
                    && phase == ClickToMovePhase.Complete)
                {
                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - Fishing");

                    phase = ClickToMovePhase.UseTool;
                    return;
                }
            }

            if (waitingToPickFurniture)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - Waiting to pick furniture");
                if (!pickedFurniture)
                {
                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - Picked furniture");
                    clickPoint = DeferredClick;
                    clickedTile = clickPoint / Game1.tileSize;
                    pickedFurniture = true;
                    phase = ClickToMovePhase.UseTool;
                }
            }
            else
            {
                if (!Game1.player.CanMove
                    || GameLocation.IsChoppableOrMinable(clickedTile))
                {
                    ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - not moving");
                    return;
                }

                ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.OnClickHeld - moving");

                if (phase != ClickToMovePhase.KeepMoving)
                {
                    if (phase != ClickToMovePhase.None)
                    {
                        Reset();
                    }

                    phase = ClickToMovePhase.KeepMoving;
                    invalidTarget.X = invalidTarget.Y = -1;
                }

                Vector2 mousePosition = new Vector2(x, y);
                Vector2 playerOffsetPosition = Game1.player.OffsetPositionOnMap();

                float distanceToMouse = Vector2.Distance(playerOffsetPosition, mousePosition);
                WalkDirection walkDirectionToMouse = WalkDirection.None;
                if (distanceToMouse > Game1.tileSize / 2f)
                {
                    float angleDegrees = (float) Math.Atan2(
                                             mousePosition.Y - playerOffsetPosition.Y,
                                             mousePosition.X - playerOffsetPosition.X)
                                         / ((float) Math.PI * 2)
                                         * 360;

                    walkDirectionToMouse = WalkDirection.GetWalkDirectionForAngle(angleDegrees);
                }

                ClickKeyStates.SetMovement(walkDirectionToMouse);
            }
        }

        /// <summary>
        ///     Called if the mouse left button was just released by the player.
        /// </summary>
        public void OnClickRelease()
        {
            ClickToMoveManager.Monitor.Log(
                $"Tick {Game1.ticks} -> ClickToMove.OnClickRelease() - Game1.player.CurrentTool.UpgradeLevel: {Game1.player.CurrentTool?.UpgradeLevel}; Game1.player.canReleaseTool: {Game1.player.canReleaseTool}");

            ClickHoldActive = false;

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

                    ClickKeyStates.RealClickHeld = false;
                    ClickKeyStates.UseToolButtonReleased = true;
                }

                if (waitingToPickFurniture)
                {
                    waitingToPickFurniture = false;

                    if (!pickedFurniture)
                    {
                        // The furniture clicked was not picked. Check if the player is placing
                        // something over it.
                        if (Game1.player.ActiveObject is Furniture && furniture.heldObject.Value is null)
                        {
                            ClickToMoveManager.Monitor.Log(
                                $"Tick {Game1.ticks} -> ClickToMove.OnClickRelease - Placing furniture over furniture, set phase to UseTool.");
                            phase = ClickToMovePhase.UseTool;
                        }
                        else if (DeferredClick.X != -1)
                        {
                            HandleClick((int) DeferredClick.X, (int) DeferredClick.Y);
                        }
                    }
                }
                else if (Game1.player.CurrentTool is not null
                         && Game1.player.CurrentTool is not FishingRod
                         && Game1.player.CurrentTool.UpgradeLevel > 0
                         && Game1.player.canReleaseTool
                         && (phase is ClickToMovePhase.None or ClickToMovePhase.PendingComplete
                             || Game1.player.UsingTool))
                {
                    ClickToMoveManager.Monitor.Log(
                        $"Tick {Game1.ticks} -> ClickToMove.OnClickRelease() 1 - phase = {phase}");
                    phase = ClickToMovePhase.UseTool;
                }
                else if (Game1.player.CurrentTool is Slingshot && Game1.player.usingSlingshot)
                {
                    phase = ClickToMovePhase.ReleaseTool;
                }
                else if (phase is ClickToMovePhase.PendingComplete or ClickToMovePhase.KeepMoving)
                {
                    ClickToMoveManager.Monitor.Log(
                        $"Tick {Game1.ticks} -> ClickToMove.OnClickRelease() 2 - phase = {phase}");
                    Reset();
                    CheckForQueuedClicks();
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
            phase = ClickToMovePhase.None;

            clickPoint = new Vector2(-1, -1);
            clickedTile = new Vector2(-1, -1);

            if (clickedNode is not null)
            {
                clickedNode.FakeTileClear = false;
            }

            clickedNode = null;

            stuckCount = 0;
            reallyStuckCount = 0;
            lastDistance = float.MaxValue;
            distanceToTarget = DistanceToTarget.Unknown;

            clickedCinemaDoor = false;
            clickedCinemaTicketBooth = false;
            endNodeOccupied = false;
            useToolOnEndNode = false;
            endTileIsActionable = false;
            performActionFromNeighbourTile = false;
            waterSourceSelected = false;

            actionableBuilding = null;
            clickedHorse = null;
            crabPot = null;
            forageItem = null;
            gateClickedOn = null;
            gateNode = null;
            TargetFarmAnimal = null;
            TargetNpc = null;

            if (resetKeyStates)
            {
                ClickKeyStates.Reset();
            }

            if (Game1.player.mount is not null)
            {
                Game1.player.mount.SetCheckActionEnabled(true);
            }
        }

        /// <summary>
        ///     Changes the farmer's equipped tool to the last used tool. This is used to get back
        ///     to the tool that was equipped before a different tool was auto-selected.
        /// </summary>
        public void SwitchBackToLastTool()
        {
            if ((ClickKeyStates.RealClickHeld && GameLocation.IsChoppableOrMinable(clickedTile))
                || lastToolIndexList.Count == 0)
            {
                return;
            }

            int lastToolIndex = lastToolIndexList.Pop();

            if (lastToolIndexList.Count == 0)
            {
                Game1.player.CurrentToolIndex = lastToolIndex;

                if (Game1.player.CurrentTool is FishingRod or Slingshot)
                {
                    Reset();
                    ClickToMove.startTime = Game1.ticks;
                }
            }
        }

        /// <summary>
        ///     Executes the action for this tick according to the current phase.
        /// </summary>
        public void Update()
        {
            ClickToMoveManager.Monitor.Log($"Tick {Game1.ticks} -> ClickToMove.Update() - phase is {phase}");

            ClickKeyStates.ClearReleasedStates();

            if (Game1.eventUp
                && !Game1.player.CanMove
                && !Game1.dialogueUp
                && phase != ClickToMovePhase.None
                && (Game1.currentSeason != "winter" || Game1.dayOfMonth != 8)
                && Game1.currentMinigame is not FishingGame)
            {
                Reset();
            }
            else
            {
                switch (phase)
                {
                    case ClickToMovePhase.FollowingPath when Game1.player.CanMove:
                        FollowPath();
                        break;
                    case ClickToMovePhase.OnFinalTile when Game1.player.CanMove:
                        MoveOnFinalTile();
                        break;
                    case ClickToMovePhase.ReachedEndOfPath:
                        StopMovingAfterReachingEndOfPath();
                        break;
                    case ClickToMovePhase.Complete:
                        OnClickToMoveComplete();
                        break;
                    case ClickToMovePhase.UseTool:
                        ClickKeyStates.SetUseTool(true);
                        phase = ClickToMovePhase.ReleaseTool;
                        break;
                    case ClickToMovePhase.ReleaseTool:
                        ClickKeyStates.SetUseTool(false);
                        phase = ClickToMovePhase.CheckForMoreClicks;
                        break;
                    case ClickToMovePhase.CheckForMoreClicks:
                        Reset();
                        CheckForQueuedClicks();
                        break;
                    case ClickToMovePhase.DoAction:
                        ClickKeyStates.ActionButtonPressed = true;
                        phase = ClickToMovePhase.FinishAction;
                        break;
                    case ClickToMovePhase.FinishAction:
                        ClickKeyStates.ActionButtonPressed = false;
                        phase = ClickToMovePhase.None;
                        break;
                }
            }

            if (!CheckToAttackMonsters())
            {
                CheckToRetargetNpc();
                CheckToRetargetFarmAnimal();
                CheckToOpenClosedGate();
                CheckToWaterNextTile();
            }
        }

        /// <summary>
        ///     Adds a click to the clicks queue, if it's not already there.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if the click wasn't already in the queue and was added to the queue;
        ///     returns <see langword="false" /> otherwise.
        /// </returns>
        private bool AddToClickQueue(int x, int y)
        {
            ClickQueueItem click = new ClickQueueItem(x, y);

            if (clickQueue.Contains(click))
            {
                return false;
            }

            clickQueue.Enqueue(click);
            return true;
        }

        /// <summary>
        ///     Equips the farmer with the appropriate tool for the interaction at the end of the path.
        /// </summary>
        private void AutoSelectPendingTool()
        {
            if (toolToSelect is not null)
            {
                lastToolIndexList.Push(Game1.player.CurrentToolIndex);

                Game1.player.SelectTool(toolToSelect);

                toolToSelect = null;
            }
        }

        /// <summary>
        ///     Selects the tool to be used at the end of the path.
        /// </summary>
        /// <param name="toolName">The name of the tool to select.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if the tool was found in the farmer's inventory;
        ///     returns <see langword="false" />, otherwise.
        /// </returns>
        private bool AutoSelectTool(string toolName)
        {
            if (Game1.player.getToolFromName(toolName) is not null)
            {
                toolToSelect = toolName;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Selects the tool to be used for the interaction with the given object at the end of the path.
        /// </summary>
        /// <param name="object">The object to interact with.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if the tool to interact with the <paramref name="object" /> was chosen. Returns
        ///     <see langword="false" /> otherwise.
        /// </returns>
        private bool AutoSelectToolForObject(SObject @object)
        {
            switch (@object.Name)
            {
                case "House Plant":
                    AutoSelectTool("Pickaxe");
                    useToolOnEndNode = true;
                    return true;
                case "Stone" or "Boulder":
                    AutoSelectTool("Pickaxe");
                    return true;
                case "Twig":
                    AutoSelectTool("Axe");
                    return true;
                case "Weeds":
                    AutoSelectTool("Scythe");
                    return true;
            }

            if (@object.ParentSheetIndex == ObjectId.ArtifactSpot)
            {
                AutoSelectTool("Hoe");
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
            CurrentBed = null;
            TargetBed = null;

            foreach (Furniture furniture in GameLocation.furniture)
            {
                if (CurrentBed is not null && TargetBed is not null)
                {
                    break;
                }

                if (furniture is BedFurniture bed)
                {
                    if (CurrentBed is null
                        && bed.getBoundingBox(bed.TileLocation).Intersects(Game1.player.GetBoundingBox()))
                    {
                        CurrentBed = bed;
                    }

                    if (TargetBed is null
                        && bed.getBoundingBox(bed.TileLocation).Contains((int) clickPoint.X, (int) clickPoint.Y))
                    {
                        Point bedSpot = bed.GetBedSpot();
                        SelectEndNode(bedSpot.X, bedSpot.Y);

                        TargetBed = bed;
                    }
                }
            }
        }

        /// <summary>
        ///     Checks whether the player interacted with the Movie Theater.
        /// </summary>
        /// <param name="node">The node clicked.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if the player clicked the Movie Theater's doors or
        ///     ticket office. Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool CheckCinemaInteraction(AStarNode node)
        {
            if (Graph.GameLocation is Town
                && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
            {
                // Node contains the cinema door.
                if (node.X is 52 or 53 && node.Y is 18 or 19)
                {
                    SelectEndNode(node.X, 19);

                    endTileIsActionable = true;
                    clickedCinemaDoor = true;

                    return true;
                }

                // Node contains the cinema ticket office.
                if (node.X is >= 54 and <= 56 && node.Y is 19 or 20)
                {
                    SelectEndNode(node.Y, 20);

                    endTileIsActionable = true;
                    performActionFromNeighbourTile = true;
                    clickedCinemaTicketBooth = true;

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
        ///     Returns <see langword="true" /> if the node is blocked. Returns
        ///     <see
        ///         langword="false" />
        ///     otherwise.
        /// </returns>
        private bool CheckEndNode(AStarNode node)
        {
            toolToSelect = null;

            if (GameLocation is Beach beach)
            {
                if (Game1.CurrentEvent is not null
                    && Game1.CurrentEvent.playerControlSequenceID == "haleyBeach"
                    && node.X == Game1.CurrentEvent.playerControlTargetTile.X
                    && node.Y == Game1.CurrentEvent.playerControlTargetTile.Y)
                {
                    clickedHaleyBracelet = true;
                    useToolOnEndNode = true;
                    return true;
                }

                if (node.X == 57 && node.Y == 13 && !beach.bridgeFixed)
                {
                    endTileIsActionable = true;

                    return false;
                }

                if (Graph.OldMariner is not null
                    && node.X == Graph.OldMariner.getTileX())
                {
                    int oldMarinerY = Graph.OldMariner.getTileY();
                    if (node.Y == oldMarinerY || node.Y == oldMarinerY - 1)
                    {
                        if (node.Y == oldMarinerY - 1)
                        {
                            SelectEndNode(node.X, oldMarinerY);
                        }

                        performActionFromNeighbourTile = true;
                        return true;
                    }
                }
            }

            if (ClickToMoveHelper.ClickedEggAtEggFestival(clickPoint))
            {
                bool tileClear = node.TileClear;

                useToolOnEndNode = true;
                performActionFromNeighbourTile = !tileClear;

                return !tileClear;
            }

            if (CheckCinemaInteraction(node))
            {
                return true;
            }

            if (GameLocation is CommunityCenter && node.X == 14 && node.Y == 5)
            {
                performActionFromNeighbourTile = true;
                return true;
            }

            if (GameLocation is FarmHouse {upgradeLevel: 2} && clickedNode.X == 16 && clickedNode.Y == 4)
            {
                SelectEndNode(clickedNode.X, clickedNode.Y + 1);
                performActionFromNeighbourTile = true;
                return true;
            }

            Vector2 tileVector = new Vector2(node.X, node.Y);

            GameLocation.terrainFeatures.TryGetValue(tileVector, out TerrainFeature terrainFeature);

            if (terrainFeature is null)
            {
                GameLocation.Objects.TryGetValue(tileVector, out SObject @object);

                if (@object is not null)
                {
                    if (@object.readyForHarvest.Value
                        || (@object.Name.Contains("Table") && @object.heldObject.Value is not null)
                        || @object.IsSpawnedObject
                        || (@object is IndoorPot indoorPot && indoorPot.hoeDirt.Value.readyForHarvest()))
                    {
                        queueingClicks = true;
                        forageItem = @object;
                        performActionFromNeighbourTile = true;
                        return true;
                    }

                    if (@object.ParentSheetIndex is ObjectId.Torch or ObjectId.SpiritTorch
                        && Game1.player.CurrentTool is Pickaxe or Axe)
                    {
                        useToolOnEndNode = true;
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
                                useToolOnEndNode = true;
                            }

                            performActionFromNeighbourTile = true;
                            return true;
                        }

                        if (@object.ParentSheetIndex is ObjectId.DrumBlock or ObjectId.FluteBlock)
                        {
                            if (Game1.player.CurrentTool is Axe or Pickaxe)
                            {
                                useToolOnEndNode = true;
                            }

                            return true;
                        }

                        if (@object.ParentSheetIndex is >= BigCraftableId.Barrel and <= BigCraftableId.Crate3)
                        {
                            if (Game1.player.CurrentTool is null || !Game1.player.CurrentTool.isHeavyHitter())
                            {
                                AutoSelectTool("Pickaxe");
                            }

                            useToolOnEndNode = true;
                            return true;
                        }

                        if (@object is Chest chest)
                        {
                            if (chest.isEmpty() && Game1.player.CurrentTool is Axe or Pickaxe)
                            {
                                useToolOnEndNode = true;
                            }

                            performActionFromNeighbourTile = true;
                            return true;
                        }

                        // Generic case: just use the tool.
                        if (Game1.player.CurrentTool is not null
                            && Game1.player.CurrentTool.isHeavyHitter()
                            && Game1.player.CurrentTool is not MeleeWeapon)
                        {
                            useToolOnEndNode = true;
                        }

                        return true;
                    }

                    if (AutoSelectToolForObject(@object))
                    {
                        return true;
                    }
                }
                else
                {
                    if (CheckForChoppableOrMinable(node))
                    {
                        return true;
                    }

                    if (CheckForBuildingInteraction(node))
                    {
                        return true;
                    }

                    AStarNode upNode = Graph.GetNode(clickedNode.X, clickedNode.Y - 1);
                    if (upNode?.GetFurnitureNoRug()?.ParentSheetIndex == FurnitureId.Calendar)
                    {
                        SelectEndNode(clickedNode.X, clickedNode.Y + 1);
                        performActionFromNeighbourTile = true;
                        return true;
                    }
                }
            }
            else if (terrainFeature is HoeDirt dirt)
            {
                if (dirt.crop is { } crop)
                {
                    if (crop.dead.Value)
                    {
                        if (Game1.player.CurrentTool is not Hoe)
                        {
                            AutoSelectTool("Scythe");
                        }

                        useToolOnEndNode = true;
                        return true;
                    }

                    if (crop.IsReadyToHarvestAndNotDead())
                    {
                        queueingClicks = true;
                        performActionFromNeighbourTile = true;
                        return true;
                    }

                    if (Game1.player.CurrentTool is Pickaxe)
                    {
                        useToolOnEndNode = true;
                        performActionFromNeighbourTile = true;
                        return true;
                    }
                }
                else
                {
                    if (Game1.player.CurrentTool is Pickaxe)
                    {
                        useToolOnEndNode = true;
                        return true;
                    }

                    if (Game1.player.ActiveObject is not null
                        && Game1.player.ActiveObject.Category == SObject.SeedsCategory)
                    {
                        queueingClicks = true;
                    }
                }

                if (dirt.state.Value != HoeDirt.watered && Game1.player.CurrentTool is WateringCan wateringCan)
                {
                    if (wateringCan.WaterLeft > 0 || Game1.player.hasWateringCanEnchantment)
                    {
                        queueingClicks = true;
                    }
                    else
                    {
                        Game1.player.doEmote(4);
                        Game1.showRedMessage(
                            Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
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
                            useToolOnEndNode = true;
                            return true;
                        }
                    }
                    else
                    {
                        if (terrainFeature is Tree tree)
                        {
                            AutoSelectTool(tree.growthStage.Value <= 1 ? "Scythe" : "Axe");
                        }

                        return true;
                    }
                }
            }

            if (furniture is not null)
            {
                if (furniture.ParentSheetIndex is FurnitureId.Catalogue or FurnitureId.FurnitureCatalogue or FurnitureId
                        .SamsBoombox
                    || furniture.furniture_type.Value is Furniture.fireplace or Furniture.lamp or Furniture.torch
                    || furniture is StorageFurniture or TV
                    || furniture.GetSeatCapacity() > 0)
                {
                    performActionFromNeighbourTile = true;
                    return true;
                }

                if (furniture.ParentSheetIndex == FurnitureId.SingingStone)
                {
                    furniture.PlaySingingStone();

                    performActionFromNeighbourTile = true;
                    return true;
                }
            }

            if (Game1.player.CurrentTool is not null
                && Game1.player.CurrentTool.isHeavyHitter()
                && !(Game1.player.CurrentTool is MeleeWeapon))
            {
                if (node.ContainsFence())
                {
                    performActionFromNeighbourTile = true;
                    useToolOnEndNode = true;

                    return true;
                }
            }

            if (GameLocation.ContainsTravellingCart((int) clickPoint.X, (int) clickPoint.Y))
            {
                if (clickedNode.Y != 11 || (clickedNode.X != 23 && clickedNode.X != 24))
                {
                    SelectEndNode(27, 11);
                }

                performActionFromNeighbourTile = true;

                return true;
            }

            if (GameLocation.ContainsTravellingDesertShop((int) clickPoint.X, (int) clickPoint.Y)
                && clickedNode.Y is 23 or 24)
            {
                performActionFromNeighbourTile = true;

                switch (clickedNode.X)
                {
                    case >= 34 and <= 38:
                        SelectEndNode(clickedNode.X, 24);
                        break;
                    case 40:
                    case 41:
                        SelectEndNode(41, 24);
                        break;
                    case 42:
                    case 43:
                        SelectEndNode(42, 24);
                        break;
                }

                return true;
            }

            if (GameLocation.IsTreeLogAt(clickedNode.X, clickedNode.Y))
            {
                performActionFromNeighbourTile = true;
                useToolOnEndNode = true;

                AutoSelectTool("Axe");

                return true;
            }

            if (GameLocation is Farm farm
                && node.X == farm.petBowlPosition.X
                && node.Y == farm.petBowlPosition.Y)
            {
                AutoSelectTool("Watering Can");
                useToolOnEndNode = true;
                return true;
            }

            if (GameLocation is SlimeHutch && node.X == 16 && node.Y is >= 6 and <= 9)
            {
                AutoSelectTool("Watering Can");
                useToolOnEndNode = true;
                return true;
            }

            NPC npc = node.GetNpc();
            if (npc is Horse horse)
            {
                clickedHorse = horse;

                if (Game1.player.CurrentItem is not Hat)
                {
                    Game1.player.CurrentToolIndex = -1;
                }

                performActionFromNeighbourTile = true;

                return true;
            }

            if (Utility.canGrabSomethingFromHere(
                (int) clickPoint.X,
                (int) clickPoint.Y,
                Game1.player))
            {
                queueingClicks = true;

                forageItem = GameLocation.getObjectAt(
                    (int) clickPoint.X,
                    (int) clickPoint.Y);

                performActionFromNeighbourTile = true;

                return true;
            }

            if (GameLocation is FarmHouse farmHouse)
            {
                Point bedSpot = farmHouse.getBedSpot();

                if (bedSpot.X == node.X && bedSpot.Y == node.Y)
                {
                    useToolOnEndNode = false;
                    performActionFromNeighbourTile = false;

                    return false;
                }
            }

            npc = GameLocation.isCharacterAtTile(new Vector2(clickedTile.X, clickedTile.Y));
            if (npc is not null)
            {
                performActionFromNeighbourTile = true;

                TargetNpc = npc;

                if (npc is Horse horse2)
                {
                    clickedHorse = horse2;

                    if (Game1.player.CurrentItem is not Hat)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (GameLocation is MineShaft
                    && Game1.player.CurrentTool is not null
                    && Game1.player.CurrentTool is Pickaxe)
                {
                    useToolOnEndNode = true;
                }

                return true;
            }

            npc = GameLocation.isCharacterAtTile(new Vector2(clickedTile.X, clickedTile.Y + 1));

            if (npc is not null
                && !(npc is Duggy)
                && !(npc is Grub)
                && !(npc is LavaCrab)
                && !(npc is MetalHead)
                && !(npc is RockCrab)
                && !(npc is GreenSlime))
            {
                SelectEndNode(clickedNode.X, clickedNode.Y + 1);

                performActionFromNeighbourTile = true;
                TargetNpc = npc;

                if (npc is Horse horse3)
                {
                    clickedHorse = horse3;

                    if (Game1.player.CurrentItem is not Hat)
                    {
                        Game1.player.CurrentToolIndex = -1;
                    }
                }

                if (GameLocation is MineShaft
                    && Game1.player.CurrentTool is not null
                    && Game1.player.CurrentTool is Pickaxe)
                {
                    useToolOnEndNode = true;
                }

                return true;
            }

            if (GameLocation is Farm
                && node.Y == 13
                && node.X is 71 or 72
                && clickedHorse is null)
            {
                SelectEndNode(node.X, node.Y + 1);

                endTileIsActionable = true;

                return true;
            }

            TargetFarmAnimal = GameLocation.GetFarmAnimal((int) clickPoint.X, (int) clickPoint.Y);

            if (TargetFarmAnimal is not null)
            {
                if (TargetFarmAnimal.getTileX() != clickedNode.X
                    || TargetFarmAnimal.getTileY() != clickedNode.Y)
                {
                    SelectEndNode(TargetFarmAnimal.getTileX(), TargetFarmAnimal.getTileY());
                }

                if (TargetFarmAnimal.wasPet
                    && TargetFarmAnimal.currentProduce > 0
                    && TargetFarmAnimal.age >= TargetFarmAnimal.ageWhenMature
                    && Game1.player.couldInventoryAcceptThisObject(TargetFarmAnimal.currentProduce, 1)
                    && AutoSelectTool(TargetFarmAnimal.toolUsedForHarvest?.Value))
                {
                    return true;
                }

                performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.player.ActiveObject is not null
                && (Game1.player.ActiveObject is not Wallpaper || GameLocation is DecoratableLocation)
                && (Game1.player.ActiveObject.bigCraftable.Value
                    || ClickToMove.ActionableObjectIds.Contains(Game1.player.ActiveObject.ParentSheetIndex)
                    || (Game1.player.ActiveObject is Wallpaper
                        && Game1.player.ActiveObject.ParentSheetIndex <= 40)))
            {
                if (Game1.player.ActiveObject.ParentSheetIndex == ObjectId.MegaBomb)
                {
                    Building building = clickedNode.GetBuilding();

                    if (building is FishPond)
                    {
                        actionableBuilding = building;

                        Point nearestTile = Graph.GetNearestTileNextToBuilding(building);

                        SelectEndNode(nearestTile.X, nearestTile.Y);
                    }
                }

                performActionFromNeighbourTile = true;
                return true;
            }

            if (GameLocation is Mountain && node.X == 29 && node.Y == 9)
            {
                performActionFromNeighbourTile = true;
                return true;
            }

            if (clickedNode.GetCrabPot() is { } crabPot)
            {
                this.crabPot = crabPot;
                performActionFromNeighbourTile = true;

                AStarNode neighbour = node.GetNearestLandNodeToCrabPot();

                if (node != neighbour)
                {
                    clickedNode = neighbour;

                    return false;
                }

                return true;
            }

            if (!node.TileClear)
            {
                if (node.ContainsBoulder())
                {
                    AutoSelectTool("Pickaxe");

                    return true;
                }

                if (GameLocation is Town && node.X == 108 && node.Y == 41)
                {
                    performActionFromNeighbourTile = true;
                    useToolOnEndNode = true;
                    endTileIsActionable = true;

                    return true;
                }

                if (GameLocation is Town && node.X == 100 && node.Y == 66)
                {
                    performActionFromNeighbourTile = true;
                    useToolOnEndNode = true;

                    return true;
                }

                Bush bush = node.GetBush();
                if (bush is not null)
                {
                    if (Game1.player.CurrentTool is Axe
                        && bush.IsDestroyable(
                            GameLocation,
                            clickedTile))
                    {
                        useToolOnEndNode = true;
                        performActionFromNeighbourTile = true;

                        return true;
                    }

                    performActionFromNeighbourTile = true;

                    return true;
                }

                if (GameLocation.IsOreAt(clickedTile)
                    && AutoSelectTool("Copper Pan")
                    && SelectEndNode(Graph.GetCoastNodeNearestWaterSource(clickedNode)))
                {
                    useToolOnEndNode = true;
                    return true;
                }

                if (CheckWaterSource(node))
                {
                    return true;
                }

                if (GameLocation.IsWizardBuilding(
                    new Vector2(clickedTile.X, clickedTile.Y)))
                {
                    performActionFromNeighbourTile = true;

                    return true;
                }

                endTileIsActionable =
                    GameLocation.isActionableTile(clickedNode.X, clickedNode.Y, Game1.player)
                    || GameLocation.isActionableTile(clickedNode.X, clickedNode.Y + 1, Game1.player);

                if (!endTileIsActionable)
                {
                    Tile tile = GameLocation.map.GetLayer("Buildings").PickTile(
                        new Location((int) (clickedTile.X * Game1.tileSize), (int) (clickedTile.Y * Game1.tileSize)),
                        Game1.viewport.Size);

                    endTileIsActionable = tile is not null;
                }

                return true;
            }

            GameLocation.terrainFeatures.TryGetValue(
                new Vector2(node.X, node.Y),
                out TerrainFeature terrainFeature2);

            if (terrainFeature2 is not null)
            {
                if (terrainFeature2 is Grass
                    && Game1.player.CurrentTool is not null
                    && Game1.player.CurrentTool is MeleeWeapon meleeWeapon
                    && meleeWeapon.type.Value != MeleeWeapon.club)
                {
                    useToolOnEndNode = true;

                    return true;
                }

                if (terrainFeature2 is Flooring
                    && Game1.player.CurrentTool is not null
                    && Game1.player.CurrentTool is Pickaxe or Axe)
                {
                    useToolOnEndNode = true;

                    return true;
                }
            }

            if (Game1.player.CurrentTool is FishingRod
                && GameLocation is Town
                && clickedNode.X is >= 50 and <= 53
                && clickedNode.Y is >= 103 and <= 105)
            {
                SelectEndNode(52, clickedNode.Y);

                waterSourceSelected = true;
                useToolOnEndNode = true;
                return true;
            }

            if (Game1.player.ActiveObject is not null
                && (Game1.player.ActiveObject.bigCraftable
                    || Game1.player.ActiveObject.ParentSheetIndex is 104 or ObjectId.WarpTotemFarm or
                        ObjectId.WarpTotemMountains or ObjectId.WarpTotemBeach or ObjectId.RainTotem or
                        ObjectId.WarpTotemDesert or 161 or 155 or 162
                    || Game1.player.ActiveObject.name.Contains("Sapling"))
                && Game1.player.ActiveObject.canBePlacedHere(
                    GameLocation,
                    new Vector2(clickedTile.X, clickedTile.Y)))
            {
                performActionFromNeighbourTile = true;
                return true;
            }

            if (Game1.player.mount is null)
            {
                useToolOnEndNode = ClickToMoveHelper.HoeSelectedAndTileHoeable(GameLocation, clickedTile);

                if (useToolOnEndNode)
                {
                    performActionFromNeighbourTile = true;
                }
            }

            if (!useToolOnEndNode)
            {
                useToolOnEndNode = WateringCanActionAtEndNode();
            }

            if (!useToolOnEndNode && Game1.player.ActiveObject is not null)
            {
                useToolOnEndNode = Game1.player.ActiveObject.isPlaceable()
                                   && Game1.player.ActiveObject.canBePlacedHere(
                                       GameLocation,
                                       new Vector2(clickedTile.X, clickedTile.Y));

                Crop crop = new Crop(Game1.player.ActiveObject.ParentSheetIndex, node.X, node.Y);
                if (crop is not null
                    && (Game1.player.ActiveObject.parentSheetIndex == ObjectId.BasicFertilizer
                        || Game1.player.ActiveObject.ParentSheetIndex == ObjectId.QualityFertilizer))
                {
                    useToolOnEndNode = true;
                }

                if (crop is not null && crop.raisedSeeds.Value)
                {
                    performActionFromNeighbourTile = true;
                }
            }

            if (GameLocation.isActionableTile(node.X, node.Y, Game1.player)
                || GameLocation.isActionableTile(node.X, node.Y + 1, Game1.player)
                || interactionAtCursor == InteractionType.Speech)
            {
                AStarNode gateNode = Graph.GetNode(clickedNode.X, clickedNode.Y + 1);

                Fence gate = clickedNode.GetGate();

                if (gate is not null)
                {
                    gateClickedOn = gate;

                    // Gate is open.
                    if (gate.gatePosition.Value == 88)
                    {
                        gateClickedOn = null;
                    }

                    performActionFromNeighbourTile = true;
                }
                else if (!clickedNode.ContainsScarecrow()
                         && gateNode.ContainsScarecrow()
                         && Game1.player.CurrentTool is not null)
                {
                    endTileIsActionable = true;
                    useToolOnEndNode = true;
                    performActionFromNeighbourTile = true;
                }
                else
                {
                    endTileIsActionable = true;
                }
            }

            if (node.GetWarp(IgnoreWarps) is not null)
            {
                useToolOnEndNode = false;

                return false;
            }

            if (!useToolOnEndNode)
            {
                AStarNode shippingBinNode = Graph.GetNode(node.X, node.Y + 1);

                Building building = shippingBinNode?.GetBuilding();

                if (building is not null && building.buildingType.Value == "Shipping Bin")
                {
                    SelectEndNode(node.X, node.Y + 1);

                    performActionFromNeighbourTile = true;

                    return true;
                }
            }

            return useToolOnEndNode;
        }

        /// <summary>
        ///     Checks if there's a building interaction available at the world location represented by the given node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if there is a building at the location represented by
        ///     the <paramref name="node" />. Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool CheckForBuildingInteraction(AStarNode node)
        {
            if (node.GetBuilding() is { } building)
            {
                if (building.buildingType.Value == "Shipping Bin")
                {
                    performActionFromNeighbourTile = true;
                    return true;
                }

                if (building.buildingType.Value == "Mill")
                {
                    if (Game1.player.ActiveObject is not null
                        && (Game1.player.ActiveObject.parentSheetIndex == ObjectId.Beet
                            || Game1.player.ActiveObject.ParentSheetIndex == ObjectId.Wheat))
                    {
                        useToolOnEndNode = true;
                    }

                    performActionFromNeighbourTile = true;
                    return true;
                }

                if (building is Barn barn)
                {
                    int animalDoorTileX = barn.tileX.Value + barn.animalDoor.X;
                    int animalDoorTileY = barn.tileY.Value + barn.animalDoor.Y;

                    if ((clickedNode.X == animalDoorTileX || clickedNode.X == animalDoorTileX + 1)
                        && (clickedNode.Y == animalDoorTileY || clickedNode.Y == animalDoorTileY - 1))
                    {
                        if (clickedNode.Y == animalDoorTileY - 1)
                        {
                            SelectEndNode(clickedNode.X, clickedNode.Y + 1);
                        }

                        performActionFromNeighbourTile = true;
                        return true;
                    }
                }
                else if (building is FishPond fishPond
                         && Game1.player.CurrentTool is not FishingRod
                         && Game1.player.CurrentTool is not WateringCan)
                {
                    actionableBuilding = fishPond;

                    Point nearestTile = Graph.GetNearestTileNextToBuilding(fishPond);
                    SelectEndNode(nearestTile.X, nearestTile.Y);

                    performActionFromNeighbourTile = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks for the existence of something to chop or mine at the world location represented by the given node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if there is something to chop or mine at the location represented by
        ///     the <paramref name="node" />. Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool CheckForChoppableOrMinable(AStarNode node)
        {
            switch (Graph.GameLocation)
            {
                case Woods woods:
                    if (woods.stumps.Any(t => t.occupiesTile(node.X, node.Y)))
                    {
                        AutoSelectTool("Axe");
                        return true;
                    }

                    break;
                case Forest forest:
                    if (forest.log is not null && forest.log.occupiesTile(node.X, node.Y))
                    {
                        AutoSelectTool("Axe");
                        return true;
                    }

                    break;
                default:
                    foreach (ResourceClump resourceClump in Graph.GameLocation.resourceClumps)
                    {
                        if (resourceClump.occupiesTile(node.X, node.Y))
                        {
                            if (resourceClump.parentSheetIndex.Value is ResourceClump.hollowLogIndex or ResourceClump
                                .stumpIndex)
                            {
                                AutoSelectTool("Axe");
                            }
                            else if (resourceClump is GiantCrop giantCrop)
                            {
                                if (giantCrop.tile.X + 1 == node.X
                                    && giantCrop.tile.Y + 1 == node.Y)
                                {
                                    Point point = Graph.FarmerNode.GetNearestNeighbour(node);
                                    SelectEndNode(point.X, point.Y);
                                }

                                AutoSelectTool("Axe");
                            }
                            else
                            {
                                AutoSelectTool("Pickaxe");
                            }

                            return true;
                        }
                    }

                    break;
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
        ///     Returns <see langword="true" /> if the processing of the current click should stop
        ///     here. Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool CheckForQueueableClicks(int x, int y)
        {
            if (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
            {
                if (queueingClicks
                    && AddToClickQueue(x, y)
                    && phase == ClickToMovePhase.None)
                {
                    waitingToFinishWatering = true;
                }

                return true;
            }

            if (queueingClicks)
            {
                Vector2 tile = new Vector2(x / (float) Game1.tileSize, y / (float) Game1.tileSize);

                GameLocation.terrainFeatures.TryGetValue(tile, out TerrainFeature terrainFeature);

                if (terrainFeature is null)
                {
                    if (GameLocation.Objects.TryGetValue(tile, out SObject @object))
                    {
                        if (@object.readyForHarvest.Value
                            || (@object.Name.Contains("Table") && @object.heldObject.Value is not null)
                            || @object.IsSpawnedObject
                            || (@object is IndoorPot indoorPot && indoorPot.hoeDirt.Value.readyForHarvest()))
                        {
                            AddToClickQueue(x, y);
                            return true;
                        }
                    }
                }
                else if (terrainFeature is HoeDirt dirt)
                {
                    if (dirt.readyForHarvest())
                    {
                        AddToClickQueue(x, y);
                        return true;
                    }

                    if (Game1.player.ActiveObject is not null
                        && Game1.player.ActiveObject.Category == SObject.SeedsCategory)
                    {
                        AddToClickQueue(x, y);
                        return false;
                    }

                    if (dirt.state.Value != HoeDirt.watered && Game1.player.CurrentTool is WateringCan wateringCan)
                    {
                        if (wateringCan.WaterLeft > 0 || Game1.player.hasWateringCanEnchantment)
                        {
                            AddToClickQueue(x, y);
                            return true;
                        }

                        Game1.player.doEmote(4);
                        Game1.showRedMessage(
                            Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    }
                }

                if (Utility.canGrabSomethingFromHere((int) tile.X, (int) tile.Y, Game1.player))
                {
                    AddToClickQueue(x, y);
                    return true;
                }

                queueingClicks = false;
                clickQueue.Clear();
            }

            return false;
        }

        private void CheckForQueuedClicks()
        {
            if (Game1.player.CurrentTool is WateringCan && Game1.player.UsingTool)
            {
                waitingToFinishWatering = true;
                queueingClicks = true;
                return;
            }

            queueingClicks = false;

            if (clickQueue.Count > 0)
            {
                /*if (Game1.player.CurrentTool is WateringCan { WaterLeft: <= 0 })
                {
                    Game1.player.doEmote(4);
                    Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:WateringCan.cs.14335"));
                    this.clickQueue.Clear();
                    return;
                }*/

                ClickQueueItem clickQueueItem = clickQueue.Dequeue();

                HandleClick(
                    clickQueueItem.ClickX,
                    clickQueueItem.ClickY);

                if (Game1.player.CurrentTool is WateringCan)
                {
                    OnClickRelease();
                }
            }
        }

        /// <summary>
        ///     Checks if there is a monster near the Farmer to attack.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true" /> if there is a monster near the Farmer to attack.
        ///     Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool CheckToAttackMonsters()
        {
            if (Game1.player.stamina <= 0)
            {
                return false;
            }

            if (justUsedWeapon)
            {
                justUsedWeapon = false;
                ClickKeyStates.Reset();
                return false;
            }

            if (phase != ClickToMovePhase.FollowingPath
                && phase != ClickToMovePhase.OnFinalTile
                && !Game1.player.UsingTool)
            {
                Rectangle boundingBox = Game1.player.GetBoundingBox();
                boundingBox.Inflate(Game1.tileSize, Game1.tileSize);
                Point playerPosition = boundingBox.Center;

                Monster targetMonster = null;
                int minimumDistance = int.MaxValue;
                foreach (NPC character in GameLocation.characters)
                {
                    // Ignore knocked down mummies. Ignore armored bugs if the Farmer isn't holding
                    // a weapon with the Bug Killer enchant.
                    if (character is Monster monster
                        && !(monster is Mummy mummy && mummy.reviveTimer > 0)
                        && !(monster is Bug bug
                             && bug.isArmoredBug
                             && !(Game1.player.CurrentTool is MeleeWeapon meleeWeapon
                                  && meleeWeapon.hasEnchantmentOfType<BugKillerEnchantment>()))
                        && boundingBox.Intersects(monster.GetBoundingBox())
                        && !IsObjectBlockingMonster(monster))
                    {
                        int distance = ClickToMoveHelper.DistanceSquared(
                            playerPosition,
                            monster.GetBoundingBox().Center);

                        if (distance < minimumDistance)
                        {
                            minimumDistance = distance;
                            targetMonster = monster;
                        }
                    }
                }

                if (targetMonster is not null)
                {
                    Game1.player.faceDirection(
                        WalkDirection.GetFacingDirection(
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
                            lastToolIndexList.Clear();
                            Game1.player.SelectTool(ClickToMove.LastMeleeWeapon.Name);
                        }
                        else if (!Game1.player.SelectMeleeWeapon() && !Game1.player.CurrentTool.isHeavyHitter())
                        {
                            Game1.player.SelectHeavyHitter();
                        }
                    }

                    justUsedWeapon = true;
                    ClickKeyStates.SetUseTool(true);
                    invalidTarget.X = invalidTarget.Y = -1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks whether the farmer can consume whatever they're holding.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true" /> if the farmer can consume the item they're holding.
        ///     Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool CheckToConsumeItem()
        {
            if (Game1.player.ActiveObject is not null
                && (Game1.player.ActiveObject.Edibility != SObject.inedible
                    || (Game1.player.ActiveObject.name.Length >= 11
                        && Game1.player.ActiveObject.name.Substring(0, 11) == "Secret Note")))
            {
                phase = ClickToMovePhase.DoAction;
                return true;
            }

            return false;
        }

        private void CheckToOpenClosedGate()
        {
            if (gateNode is not null
                && Vector2.Distance(
                    Game1.player.OffsetPositionOnMap(),
                    new Vector2(gateNode.NodeCenterOnMap.X, gateNode.NodeCenterOnMap.Y))
                < 83.2f)
            {
                Fence fence = gateNode.GetGate();

                // Is the gate closed?
                if (fence is not null && fence.gatePosition.Value != Fence.gateOpenedPosition)
                {
                    fence.checkForAction(Game1.player);
                    gateNode = null;
                }
            }
        }

        /// <summary>
        ///     If the targeted <see cref="FarmAnimal" /> is no longer at the clicked position,
        ///     recompute a new path to it.
        /// </summary>
        private void CheckToRetargetFarmAnimal()
        {
            if (TargetFarmAnimal is not null
                && clickedTile.X != -1
                && (clickedTile.X != TargetFarmAnimal.getTileX() || clickedTile.Y != TargetFarmAnimal.getTileY()))
            {
                HandleClick(
                    TargetFarmAnimal.getStandingX() + (Game1.tileSize / 2),
                    TargetFarmAnimal.getStandingY() + (Game1.tileSize / 2));
            }
        }

        /// <summary>
        ///     If the targeted <see cref="NPC" /> is no longer at the clicked position, recompute a
        ///     new path to it, if possible.
        /// </summary>
        private void CheckToRetargetNpc()
        {
            if (TargetNpc is not null && (clickedTile.X != -1 || clickedTile.Y != -1))
            {
                if (TargetNpc.currentLocation != GameLocation
                    || TargetNpc.AtWarpOrDoor(GameLocation))
                {
                    Reset();
                }
                else if (clickedTile.X != TargetNpc.getTileX()
                         || clickedTile.Y != TargetNpc.getTileY())
                {
                    HandleClick(
                        (TargetNpc.getTileX() * Game1.tileSize) + (Game1.tileSize / 2),
                        (TargetNpc.getTileY() * Game1.tileSize) + (Game1.tileSize / 2));
                }
            }
        }

        /// <summary>
        ///     Check if the Farmer has finished watering the current tile and proceed to the next.
        /// </summary>
        private void CheckToWaterNextTile()
        {
            if (waitingToFinishWatering && !Game1.player.UsingTool)
            {
                waitingToFinishWatering = false;
                CheckForQueuedClicks();
            }
        }

        /// <summary>
        ///     If the Farmer has the watering can or the fishing rod equipped, checks whether they
        ///     can use the tool on the clicked tile. If that's the case, the clicked tile is set to
        ///     a water source tile that neighbours a land tile.
        /// </summary>
        /// <param name="node">The node associated to the clicked tile.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if the Farmer has the watering can or the fishing rod
        ///     equipped and they can use the tool on the clicked tile. Returns
        ///     <see
        ///         langword="false" />
        ///     otherwise.
        /// </returns>
        private bool CheckWaterSource(AStarNode node)
        {
            if ((Game1.player.CurrentTool is WateringCan && GameLocation.CanRefillWateringCanOnTile(node.X, node.Y))
                || (Game1.player.CurrentTool is FishingRod
                    && GameLocation.canFishHere()
                    && GameLocation.isTileFishable(node.X, node.Y)))
            {
                SelectEndNode(Graph.GetNearestCoastNode(node));

                useToolOnEndNode = true;
                performActionFromNeighbourTile = true;
                waterSourceSelected = true;

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Makes the farmer's face the clicked point.
        /// </summary>
        /// <param name="faceClickPoint">
        ///     Indicates whether to use the <see cref="clickPoint" /> or instead the
        ///     <see
        ///         cref="ClickedTile" />
        ///     when computing the facing direction.
        /// </param>
        private void FaceTileClicked(bool faceClickPoint = false)
        {
            int facingDirection;

            if (faceClickPoint)
            {
                facingDirection = WalkDirection.GetFacingDirection(
                    Game1.player.Position,
                    clickPoint);
            }
            else
            {
                facingDirection = WalkDirection.GetFacingDirection(
                    Game1.player.getTileLocation(),
                    clickedTile);
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
                AStarNode node = Graph.GetNode(x, y);

                if (node?.TileClear == true)
                {
                    path = Graph.FindPath(start, node);

                    if (path is not null)
                    {
                        path.SmoothRightAngles();
                        phase = ClickToMovePhase.FollowingPath;

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
            if (path.Count > 0)
            {
                AStarNode farmerNode = Graph.FarmerNode;

                if (farmerNode is null)
                {
                    Reset();
                    return;
                }

                // Next node reached.
                if (path[0] == farmerNode)
                {
                    path.RemoveFirst();

                    lastDistance = float.MaxValue;
                    stuckCount = 0;
                    reallyStuckCount = 0;
                }

                if (path.Count > 0)
                {
                    // An animal or an NPC is blocking the way, we need to recompute the path.
                    if (path[0].ContainsAnimal()
                        || (path[0].GetNpc() is { } and not Horse && !Game1.player.isRidingHorse()))
                    {
                        HandleClick((int) clickPoint.X, (int) clickPoint.Y);
                        return;
                    }

                    Vector2 playerOffsetPositionOnMap = Game1.player.OffsetPositionOnMap();
                    Vector2 nextNodeCenter = path[0].NodeCenterOnMap;
                    WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                        playerOffsetPositionOnMap,
                        nextNodeCenter,
                        Game1.player.getMovementSpeed());

                    float distanceToNextNode = Vector2.Distance(playerOffsetPositionOnMap, nextNodeCenter);

                    // No progress since last attempt.
                    if (distanceToNextNode >= lastDistance)
                    {
                        stuckCount++;
                    }

                    lastDistance = distanceToNextNode;

                    if (distanceToNextNode < Game1.player.getMovementSpeed()
                        || stuckCount >= ClickToMove.MaxStuckCount)
                    {
                        if (reallyStuckCount >= ClickToMove.MaxReallyStuckCount)
                        {
                            reallyStuckCount++;
                            if (reallyStuckCount == 8)
                            {
                                if (Game1.player.isRidingHorse())
                                {
                                    Reset();
                                }
                                else if (clickedHorse is not null)
                                {
                                    clickedHorse.checkAction(Game1.player, GameLocation);

                                    Reset();
                                }
                                else if (Graph.FarmerNode.GetNpc() is Horse horse)
                                {
                                    horse.checkAction(Game1.player, GameLocation);
                                }
                                else
                                {
                                    // Try again.
                                    HandleClick((int) clickPoint.X, (int) clickPoint.Y, tryCount + 1);
                                }

                                return;
                            }

                            walkDirection = WalkDirection.OppositeWalkDirection(walkDirection);
                        }
                        else
                        {
                            WalkDirection walkDirection2 = farmerNode.WalkDirectionTo(path[0]);

                            if (walkDirection2 != walkDirection)
                            {
                                reallyStuckCount++;
                                walkDirection = walkDirection2;
                            }
                            else
                            {
                                walkDirection2 = WalkDirection.GetWalkDirection(
                                    playerOffsetPositionOnMap,
                                    nextNodeCenter);

                                if (walkDirection2 != walkDirection)
                                {
                                    reallyStuckCount++;
                                    walkDirection = walkDirection2;
                                }
                            }

                            stuckCount = 0;
                        }
                    }

                    ClickKeyStates.SetMovement(walkDirection);
                }
            }

            if (path.Count == 0)
            {
                path = null;
                phase = ClickToMovePhase.OnFinalTile;
            }
        }

        private Rectangle GetHorseAlternativeBoundingBox(Horse horse)
        {
            if (horse.FacingDirection == WalkDirection.Up.Value || horse.FacingDirection == WalkDirection.Down.Value)
            {
                return new Rectangle((int) horse.Position.X, (int) horse.Position.Y - 128, 64, 192);
            }

            return new Rectangle((int) horse.Position.X - 32, (int) horse.Position.Y, 128, 64);
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

                interactionAtCursor = InteractionType.None;

                Vector2 clickPoint = new Vector2(x, y);
                Vector2 clickedTile = new Vector2(clickPoint.X / Game1.tileSize, clickPoint.Y / Game1.tileSize);
                AStarNode clickedNode = Graph.GetNode((int) clickedTile.X, (int) clickedTile.Y);

                if (clickedNode is null)
                {
                    return;
                }

                SetInteractionAtCursor(clickedNode.X, clickedNode.Y);

                if (CheckForQueueableClicks(x, y))
                {
                    return;
                }

                if (!Game1.player.CanMove)
                {
                    if (Game1.player.UsingTool
                        && Game1.player.CurrentTool is not null
                        && Game1.player.CurrentTool.isHeavyHitter())
                    {
                        Game1.player.Halt();

                        ClickKeyStates.SetUseTool(false);
                        justUsedWeapon = false;
                    }

                    if (Game1.eventUp)
                    {
                        if ((Game1.currentSeason == "winter" && Game1.dayOfMonth == 8)
                            || Game1.currentMinigame is FishingGame)
                        {
                            phase = ClickToMovePhase.UseTool;
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
                            phase = ClickToMovePhase.UseTool;
                            return;
                        }
                    }
                }

                if (Game1.player.ClickedOn((int) clickPoint.X, (int) clickPoint.Y))
                {
                    if (Game1.player.CurrentTool is Slingshot && Game1.currentMinigame is not TargetGame)
                    {
                        ClickKeyStates.SetUseTool(true);
                        ClickKeyStates.RealClickHeld = true;

                        phase = ClickToMovePhase.UsingSlingshot;
                        return;
                    }

                    if (Game1.player.CurrentTool is Wand)
                    {
                        phase = ClickToMovePhase.UseTool;
                        return;
                    }

                    if (CheckToConsumeItem())
                    {
                        return;
                    }
                }

                if (tryCount >= ClickToMove.MaxTries)
                {
                    Reset();
                    Game1.player.Halt();
                    return;
                }

                Reset(false);
                ClickKeyStates.ResetLeftOrRightClickButtons();
                ClickKeyStates.RealClickHeld = true;

                this.clickPoint = clickPoint;
                this.clickedTile = clickedTile;
                invalidTarget.X = invalidTarget.Y = -1;

                this.tryCount = tryCount;

                if (Game1.player.ActiveObject is Furniture furniture
                    && (GameLocation.CanFreePlaceFurniture()
                        || furniture.IsCloseEnoughToFarmer(
                            Game1.player,
                            (int) this.clickedTile.X,
                            (int) this.clickedTile.Y)))
                {
                    ClickToMoveManager.Monitor.Log(
                        $"Tick {Game1.ticks} -> ClickToMove.HandleClick({x}, {y}) - Holding furniture, set phase to UseTool.");
                    phase = ClickToMovePhase.UseTool;
                    return;
                }

                if (GameLocation is DecoratableLocation decoratableLocation
                    && Game1.player.ActiveObject is Wallpaper wallpaper
                    && wallpaper.CanBePlaced(decoratableLocation, (int) this.clickedTile.X, (int) this.clickedTile.Y))
                {
                    ClickKeyStates.ActionButtonPressed = true;
                    return;
                }

                if (Game1.player.isRidingHorse()
                    && (GetHorseAlternativeBoundingBox(Game1.player.mount)
                            .Contains((int) clickPoint.X, (int) clickPoint.Y)
                        || Game1.player.ClickedOn((int) clickPoint.X, (int) clickPoint.Y))
                    && Game1.player.mount.checkAction(Game1.player, GameLocation))
                {
                    Reset();
                    return;
                }

                if (GameLocation.doesTileHaveProperty(
                        (int) this.clickedTile.X,
                        (int) this.clickedTile.Y,
                        "Action",
                        "Buildings") is string action
                    && action.Contains("Message"))
                {
                    if (!ClickToMoveHelper.ClickedEggAtEggFestival(ClickPoint))
                    {
                        if (!GameLocation.checkAction(
                            new Location((int) this.clickedTile.X, (int) this.clickedTile.Y),
                            Game1.viewport,
                            Game1.player))
                        {
                            GameLocation.checkAction(
                                new Location((int) this.clickedTile.X, (int) this.clickedTile.Y + 1),
                                Game1.viewport,
                                Game1.player);
                        }

                        Reset();
                        Game1.player.Halt();
                        return;
                    }
                }
                else if (GameLocation is Town town
                         && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
                {
                    if (this.clickedTile.X is >= 48 and <= 51 && this.clickedTile.Y is 18 or 19)
                    {
                        town.checkAction(new Location((int) this.clickedTile.X, 19), Game1.viewport, Game1.player);

                        Reset();
                        return;
                    }
                }
                else if (GameLocation is Beach beach
                         && !beach.bridgeFixed
                         && this.clickedTile.X is 58 or 59
                         && this.clickedTile.Y is 11 or 12)
                {
                    beach.checkAction(new Location(58, 13), Game1.viewport, Game1.player);
                }
                else if (GameLocation is LibraryMuseum libraryMuseum
                         && (this.clickedTile.X != 3 || this.clickedTile.Y != 9))
                {
                    if (libraryMuseum.museumPieces.ContainsKey(new Vector2(this.clickedTile.X, this.clickedTile.Y)))
                    {
                        if (libraryMuseum.checkAction(
                            new Location((int) this.clickedTile.X, (int) this.clickedTile.Y),
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
                            int tileY = (int) this.clickedTile.Y + deltaY;

                            if (libraryMuseum.doesTileHaveProperty(
                                    (int) this.clickedTile.X,
                                    tileY,
                                    "Action",
                                    "Buildings") is { } actionProperty
                                && actionProperty.Contains("Notes")
                                && libraryMuseum.checkAction(
                                    new Location((int) this.clickedTile.X, tileY),
                                    Game1.viewport,
                                    Game1.player))
                            {
                                return;
                            }
                        }
                    }
                }

                CheckBed();

                this.clickedNode = Graph.GetNode((int) this.clickedTile.X, (int) this.clickedTile.Y);

                if (this.clickedNode is null)
                {
                    Reset();
                    return;
                }

                startNode = finalNode = Graph.FarmerNode;

                if (startNode is null)
                {
                    return;
                }

                if (CheckEndNode(this.clickedNode))
                {
                    endNodeOccupied = true;
                    this.clickedNode.FakeTileClear = true;
                }
                else
                {
                    endNodeOccupied = false;
                }

                if (clickedHorse is not null && Game1.player.CurrentItem is Hat)
                {
                    clickedHorse.checkAction(Game1.player, GameLocation);

                    Reset();
                    return;
                }

                if (TargetNpc is not null
                    && Game1.CurrentEvent is not null
                    && Game1.CurrentEvent.playerControlSequenceID is not null
                    && Game1.CurrentEvent.festivalTimer > 0
                    && Game1.CurrentEvent.playerControlSequenceID == "iceFishing")
                {
                    Reset();
                    return;
                }

                if (!Game1.player.isRidingHorse()
                    && Game1.player.mount is null
                    && !performActionFromNeighbourTile
                    && !useToolOnEndNode)
                {
                    foreach (NPC npc in GameLocation.characters)
                    {
                        if (npc is Horse horse
                            && Vector2.Distance(ClickPoint, Utility.PointToVector2(horse.GetBoundingBox().Center)) < 48
                            && this.clickedTile != horse.getTileLocation())
                        {
                            Reset();

                            HandleClick(
                                (int) ((horse.getTileLocation().X * Game1.tileSize) + (Game1.tileSize / 2f)),
                                (int) ((horse.getTileLocation().Y * Game1.tileSize) + (Game1.tileSize / 2f)));

                            return;
                        }
                    }
                }

                if (this.clickedNode is not null
                    && endNodeOccupied
                    && !useToolOnEndNode
                    && !performActionFromNeighbourTile
                    && !endTileIsActionable
                    && !this.clickedNode.ContainsSomeKindOfWarp()
                    && this.clickedNode.ContainsBuilding())
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
                            && actionableBuilding is null
                            && !(GameLocation is Farm
                                 && this.clickedNode.X == 21
                                 && this.clickedNode.Y == 25
                                 && Game1.whichFarm == Farm.mountains_layout))
                        {
                            Reset();
                            return;
                        }
                    }
                }

                if (this.clickedNode.ContainsCinema() && !clickedCinemaTicketBooth && !clickedCinemaDoor)
                {
                    invalidTarget = this.clickedTile;

                    Reset();

                    return;
                }

                if (endNodeOccupied)
                {
                    if (startNode.IsVerticalOrHorizontalNeighbour(this.clickedNode)
                        || (startNode.IsDiagonalNeighbour(this.clickedNode)
                            && (Game1.player.CurrentTool is WateringCan or Hoe or MeleeWeapon
                                || performActionFromNeighbourTile)))
                    {
                        phase = ClickToMovePhase.OnFinalTile;

                        return;
                    }
                }

                if (crabPot is not null
                    && Vector2.Distance(
                        new Vector2(
                            (crabPot.TileLocation.X * Game1.tileSize) + (Game1.tileSize / 2f),
                            (crabPot.TileLocation.Y * Game1.tileSize) + (Game1.tileSize / 2f)),
                        Game1.player.Position)
                    < Game1.tileSize * 2)
                {
                    PerformCrabPotAction();

                    return;
                }

                path = GameLocation is AnimalHouse
                    ? Graph.FindPath(startNode, this.clickedNode)
                    : Graph.FindPathWithBubbleCheck(startNode, this.clickedNode);

                if ((path is null || path.Count == 0 || path[0] is null)
                    && endNodeOccupied
                    && performActionFromNeighbourTile)
                {
                    path = Graph.FindPathToNeighbourDiagonalWithBubbleCheck(startNode, this.clickedNode);

                    if (path is not null && path.Count > 0)
                    {
                        this.clickedNode.FakeTileClear = false;
                        this.clickedNode = path[path.Count - 1];
                        endNodeOccupied = false;
                        performActionFromNeighbourTile = false;
                    }
                }

                if (path is not null && path.Count > 0)
                {
                    gateNode = path.ContainsGate();

                    if (endNodeOccupied)
                    {
                        path.SmoothRightAngles(2);
                    }
                    else
                    {
                        path.SmoothRightAngles();
                    }

                    if (this.clickedNode.FakeTileClear)
                    {
                        if (path.Count > 0)
                        {
                            path.RemoveLast();
                        }

                        this.clickedNode.FakeTileClear = false;
                    }

                    if (path.Count > 0)
                    {
                        finalNode = path[path.Count - 1];
                    }

                    phase = ClickToMovePhase.FollowingPath;

                    return;
                }

                if (startNode.IsSameNode(this.clickedNode)
                    && (useToolOnEndNode || performActionFromNeighbourTile))
                {
                    AStarNode neighbour = startNode.GetNeighbourPassable();

                    if (neighbour is null)
                    {
                        Reset();

                        return;
                    }

                    if (queueingClicks)
                    {
                        phase = ClickToMovePhase.UseTool;
                        return;
                    }

                    if (waterSourceSelected)
                    {
                        FaceTileClicked(true);
                        phase = ClickToMovePhase.UseTool;
                        return;
                    }

                    path.Add(neighbour);
                    path.Add(startNode);

                    invalidTarget.X = invalidTarget.Y = -1;
                    finalNode = path[path.Count - 1];
                    phase = ClickToMovePhase.FollowingPath;

                    return;
                }

                if (startNode.IsSameNode(this.clickedNode))
                {
                    if (!Game1.isFestival() && this.clickedNode.GetWarp(IgnoreWarps) is { } warp)
                    {
                        Game1.player.warpFarmer(warp);
                    }

                    Reset();
                    invalidTarget.X = invalidTarget.Y = -1;
                    return;
                }

                if (startNode is not null
                    && Game1.player.ActiveObject is not null
                    && Game1.player.ActiveObject.name == "Crab Pot")
                {
                    TryToFindAlternatePath(startNode);

                    if (path is not null && path.Count > 0)
                    {
                        finalNode = path[path.Count - 1];
                    }

                    return;
                }

                if (endTileIsActionable)
                {
                    y += Game1.tileSize;

                    invalidTarget = this.clickedTile;

                    if (tryCount > 0)
                    {
                        invalidTarget.Y -= 1;
                    }

                    tryCount++;

                    continue;
                }

                if (TargetNpc is not null
                    && TargetNpc.Name == "Robin"
                    && GameLocation is BuildableGameLocation)
                {
                    TargetNpc.checkAction(Game1.player, GameLocation);
                }

                invalidTarget = this.clickedTile;

                if (tryCount > 0)
                {
                    invalidTarget.Y -= 1;
                }

                Reset();

                break;
            }
        }

        /// <summary>
        ///     Checks whether the current click should be ignored.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true" /> if the current click should be ignored. Returns
        ///     <see
        ///         langword="false" />
        ///     if the click needs to be looked at.
        /// </returns>
        private bool IgnoreClick()
        {
            return Game1.player.passedOut
                   || Game1.player.FarmerSprite.isPassingOut()
                   || Game1.player.isEating
                   || Game1.player.IsBeingSick()
                   || Game1.locationRequest is not null
                   || (Game1.CurrentEvent is not null && !Game1.CurrentEvent.playerControlSequence)
                   || Game1.dialogueUp
                   || Game1.activeClickableMenu is not null
                       and not AnimalQueryMenu
                       and not CarpenterMenu
                       and not PurchaseAnimalsMenu
                       and not MuseumMenu
                   || ClickToMoveHelper.InMiniGameWhereWeDontWantClicks()
                   || ClickToMoveManager.IgnoreClick;
        }

        /// <summary>
        ///     Checks whether there's an object that can block the Farmer at the given tile coordinates.
        /// </summary>
        /// <param name="tileX">The x tile coordinate.</param>
        /// <param name="tileY">The y tile coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if there's an object that can block the Farmer at the
        ///     given tile coordinates. Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool IsBlockingObject(int tileX, int tileY)
        {
            return (GameLocation.objects.TryGetValue(new Vector2(tileX, tileY), out SObject @object)
                    && @object?.ParentSheetIndex is >= ObjectId.GlassShards and <= ObjectId.GoldenRelic)
                   || (Graph.GetNode(tileX, tileY)?.ContainsStumpOrBoulder() ?? false);
        }

        /// <summary>
        ///     Checks whether there's any blocking object between the Farmer and the given monster.
        /// </summary>
        /// <param name="monster">The monster near the Farmer.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if there's a blocking object between the Farmer and
        ///     the given monster. Returns <see langword="false" /> otherwise.
        /// </returns>
        private bool IsObjectBlockingMonster(Monster monster)
        {
            Point playerTile = Game1.player.getTileLocationPoint();
            Point monsterTile = monster.getTileLocationPoint();

            int distanceX = monsterTile.X - playerTile.X;
            int distanceY = monsterTile.Y - playerTile.Y;

            if (Math.Abs(distanceX) == 2 || Math.Abs(distanceY) == 2)
            {
                return IsBlockingObject(playerTile.X + Math.Sign(distanceX), playerTile.Y + Math.Sign(distanceY));
            }

            if (Math.Abs(distanceX) == 1 && Math.Abs(distanceY) == 1)
            {
                return IsBlockingObject(playerTile.X + Math.Sign(distanceX), 0)
                       && IsBlockingObject(0, playerTile.Y + Math.Sign(distanceY));
            }

            return false;
        }

        /// <summary>
        ///     Called while the farmer is at the final tile on the path.
        /// </summary>
        private void MoveOnFinalTile()
        {
            Vector2 playerOffsetPositionOnMap = Game1.player.OffsetPositionOnMap();
            Vector2 clickedNodeCenterOnMap = clickedNode.NodeCenterOnMap;

            float distanceToGoal = Vector2.Distance(
                playerOffsetPositionOnMap,
                clickedNodeCenterOnMap);

            if (distanceToGoal == lastDistance)
            {
                stuckCount++;
            }

            lastDistance = distanceToGoal;

            if (performActionFromNeighbourTile)
            {
                float deltaX = Math.Abs(clickedNodeCenterOnMap.X - playerOffsetPositionOnMap.X)
                               - Game1.player.speed;
                float deltaY = Math.Abs(clickedNodeCenterOnMap.Y - playerOffsetPositionOnMap.Y)
                               - Game1.player.speed;

                if (distanceToTarget != DistanceToTarget.TooFar
                    && crabPot is null
                    && Game1.player.GetBoundingBox().Intersects(clickedNode.TileRectangle))
                {
                    distanceToTarget = DistanceToTarget.TooClose;

                    WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                        clickedNodeCenterOnMap,
                        playerOffsetPositionOnMap);

                    ClickKeyStates.SetMovement(walkDirection);
                }
                else if (distanceToTarget != DistanceToTarget.TooClose
                         && stuckCount < ClickToMove.MaxStuckCount
                         && Math.Max(deltaX, deltaY) > Game1.tileSize)
                {
                    distanceToTarget = DistanceToTarget.TooFar;

                    WalkDirection walkDirection = WalkDirection.GetWalkDirectionForAngle(
                        (float) (Math.Atan2(
                                     clickedNodeCenterOnMap.Y - playerOffsetPositionOnMap.Y,
                                     clickedNodeCenterOnMap.X - playerOffsetPositionOnMap.X)
                                 / Math.PI
                                 / 2
                                 * 360));

                    ClickKeyStates.SetMovement(walkDirection);
                }
                else
                {
                    distanceToTarget = DistanceToTarget.Unknown;
                    OnReachEndOfPath();
                }
            }
            else
            {
                if (distanceToGoal < Game1.player.getMovementSpeed()
                    || stuckCount >= ClickToMove.MaxStuckCount
                    || (useToolOnEndNode && distanceToGoal < Game1.tileSize)
                    || (endNodeOccupied && distanceToGoal < Game1.tileSize + 2))
                {
                    OnReachEndOfPath();
                    return;
                }

                WalkDirection walkDirection = WalkDirection.GetWalkDirection(
                    playerOffsetPositionOnMap,
                    finalNode.NodeCenterOnMap,
                    Game1.player.getMovementSpeed());

                ClickKeyStates.SetMovement(walkDirection);
            }
        }

        /// <summary>
        ///     Method called when the Farmer has completed the eventual interaction at the end of the path.
        /// </summary>
        private void OnClickToMoveComplete()
        {
            if (!Game1.isFestival()
                && !Game1.player.UsingTool
                && clickedHorse is null
                && !IgnoreWarps)
            {
                GameLocation.WarpIfInRange(clickPoint);
            }

            Reset();
            CheckForQueuedClicks();
        }

        /// <summary>
        ///     Method called after the Farmer reaches the end of the path.
        /// </summary>
        private void OnReachEndOfPath()
        {
            AutoSelectPendingTool();

            if (endNodeOccupied)
            {
                WalkDirection walkDirection;
                if (useToolOnEndNode)
                {
                    if (Game1.currentMinigame is FishingGame)
                    {
                        walkDirection = WalkDirection.GetFacingWalkDirection(
                            Game1.player.OffsetPositionOnMap(),
                            clickPoint);

                        FaceTileClicked();
                    }
                    else
                    {
                        walkDirection = WalkDirection.GetWalkDirection(
                            Game1.player.OffsetPositionOnMap(),
                            clickPoint);

                        if (Game1.player.CurrentTool is not null
                            && (Game1.player.CurrentTool is FishingRod
                                || (Game1.player.CurrentTool is WateringCan && waterSourceSelected)))
                        {
                            Game1.player.faceDirection(
                                WalkDirection.GetFacingDirection(
                                    Game1.player.OffsetPositionOnMap(),
                                    clickPoint));
                        }
                    }
                }
                else
                {
                    walkDirection = Graph.FarmerNode.WalkDirectionTo(clickedNode);
                    if (walkDirection == WalkDirection.None)
                    {
                        walkDirection = WalkDirection.GetWalkDirection(
                            Game1.player.OffsetPositionOnMap(),
                            clickedNode.NodeCenterOnMap,
                            Game1.smallestTileSize);
                    }
                }

                if (walkDirection == WalkDirection.None)
                {
                    walkDirection = ClickKeyStates.LastWalkDirection;
                }

                ClickKeyStates.SetMovement(walkDirection);

                if (useToolOnEndNode || !PerformAction())
                {
                    if (Game1.player.CurrentTool is WateringCan)
                    {
                        FaceTileClicked(true);
                    }

                    if (Game1.player.CurrentTool is not FishingRod || waterSourceSelected)
                    {
                        GrabTile = new Vector2(clickPoint.X / Game1.tileSize, clickPoint.Y / Game1.tileSize);
                        if (!GameLocation.IsChoppableOrMinable(clickedTile))
                        {
                            clickedTile.X = -1;
                            clickedTile.Y = -1;
                        }

                        if (Game1.CurrentEvent is not null && clickedHaleyBracelet)
                        {
                            Game1.CurrentEvent.receiveActionPress(
                                Game1.CurrentEvent.playerControlTargetTile.X,
                                Game1.CurrentEvent.playerControlTargetTile.Y);
                            clickedHaleyBracelet = false;
                        }
                        else
                        {
                            ClickKeyStates.SetUseTool(true);
                        }
                    }
                }
            }
            else if (useToolOnEndNode)
            {
                ClickKeyStates.SetUseTool(true);
            }
            else if (!PerformAction())
            {
                ClickKeyStates.SetMovement(false, false, false, false);
            }

            phase = ClickToMovePhase.ReachedEndOfPath;
        }

        private bool PerformAction()
        {
            if (PerformCrabPotAction())
            {
                return true;
            }

            if (actionableBuilding is not null)
            {
                actionableBuilding.doAction(
                    new Vector2(actionableBuilding.tileX, actionableBuilding.tileY),
                    Game1.player);
                return true;
            }

            if (clickedCinemaTicketBooth)
            {
                clickedCinemaTicketBooth = false;
                GameLocation.checkAction(new Location(55, 20), Game1.viewport, Game1.player);
                return true;
            }

            if (clickedCinemaDoor)
            {
                clickedCinemaDoor = false;
                GameLocation.checkAction(new Location(53, 19), Game1.viewport, Game1.player);
                return true;
            }

            if ((endTileIsActionable || performActionFromNeighbourTile)
                && Game1.player.mount is not null
                && forageItem is not null)
            {
                GameLocation.checkAction(
                    new Location(clickedNode.X, clickedNode.Y),
                    Game1.viewport,
                    Game1.player);
                forageItem = null;
                return true;
            }

            if (clickedHorse is not null)
            {
                clickedHorse.checkAction(Game1.player, GameLocation);

                Reset();

                return false;
            }

            if (Game1.player.mount is not null && clickedHorse is null)
            {
                Game1.player.mount.SetCheckActionEnabled(false);
            }

            if (GameLocation.isActionableTile((int) clickedTile.X, (int) clickedTile.Y, Game1.player)
                && !clickedNode.ContainsGate())
            {
                if (GameLocation.IsChoppableOrMinable(clickedTile))
                {
                    if (ClickHoldActive)
                    {
                        return false;
                    }

                    SwitchBackToLastTool();
                }

                Game1.player.Halt();
                ClickKeyStates.ActionButtonPressed = true;
                return true;
            }

            if (endNodeOccupied && !endTileIsActionable && !performActionFromNeighbourTile)
            {
                return furniture is not null;
            }

            if (GameLocation is Farm
                && GameLocation.isActionableTile(
                    (int) clickedTile.X,
                    (int) clickedTile.Y + 1,
                    Game1.player))
            {
                ClickKeyStates.SetMovement(WalkDirection.Down);
                ClickKeyStates.ActionButtonPressed = true;
                return true;
            }

            if (TargetNpc is Child)
            {
                TargetNpc.checkAction(Game1.player, GameLocation);

                Reset();
                return false;
            }

            if (endTileIsActionable || performActionFromNeighbourTile)
            {
                gateNode = null;
                if (gateClickedOn is not null)
                {
                    gateClickedOn = null;
                    return false;
                }

                FaceTileClicked();
                Game1.player.Halt();
                ClickKeyStates.ActionButtonPressed = true;
                return true;
            }

            Game1.player.Halt();
            return false;
        }

        private bool PerformCrabPotAction()
        {
            if (crabPot is not null)
            {
                if (Game1.player.ActiveObject is not null && Game1.player.ActiveObject.Category == SObject.baitCategory)
                {
                    if (crabPot.performObjectDropInAction(Game1.player.ActiveObject, false, Game1.player))
                    {
                        Game1.player.reduceActiveItemByOne();
                    }
                }
                else
                {
                    crabPot.checkForAction(Game1.player);
                }

                crabPot = null;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Selects a new end node for the current path computation, at the given tile coordinates.
        /// </summary>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        private void SelectEndNode(int tileX, int tileY)
        {
            AStarNode node = Graph.GetNode(tileX, tileY);
            if (node is not null)
            {
                clickedNode = node;
                clickedTile.X = tileX;
                clickedTile.Y = tileY;
                clickPoint.X = (tileX * Game1.tileSize) + (Game1.tileSize / 2);
                clickPoint.Y = (tileY * Game1.tileSize) + (Game1.tileSize / 2);
            }
        }

        /// <summary>
        ///     Selects a new end node for the current path computation.
        /// </summary>
        /// <param name="node">The end node to select.</param>
        /// <returns>
        ///     Returns <see langword="true" /> if the <paramref name="node" /> is not null. Returns <see langword="false" />
        ///     otherwise.
        /// </returns>
        private bool SelectEndNode(AStarNode node)
        {
            if (node is not null)
            {
                clickedNode = node;
                clickedTile.X = node.X;
                clickedTile.Y = node.Y;
                clickPoint.X = (node.X * Game1.tileSize) + (Game1.tileSize / 2);
                clickPoint.Y = (node.Y * Game1.tileSize) + (Game1.tileSize / 2);

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Determines the interaction available to the farmer at a clicked tile.
        /// </summary>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        private void SetInteractionAtCursor(int tileX, int tileY)
        {
            Vector2 tileVector = new Vector2(tileX, tileY);

            if (Game1.currentLocation.isCharacterAtTile(tileVector) is {IsMonster: false, IsInvisible: false} character)
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
                            && (character.CurrentDialogue is {Count: > 0}
                                || (Game1.player.spouse is not null
                                    && character.Name == Game1.player.spouse
                                    && character.shouldSayMarriageDialogue.Value
                                    && character.currentMarriageDialogue is not null
                                    && character.currentMarriageDialogue.Count > 0)
                                || character.hasTemporaryMessageAvailable()
                                || (Game1.player.hasClubCard
                                    && character.Name.Equals("Bouncer")
                                    && Game1.player.IsLocalPlayer)
                                || (character.Name.Equals("Henchman")
                                    && character.currentLocation.Name.Equals("WitchSwamp")
                                    && !Game1.player.hasOrWillReceiveMail("henchmanGone")))
                            && !character.isOnSilentTemporaryMessage())))
                {
                    interactionAtCursor = InteractionType.Speech;
                }
                else if (GameLocation.currentEvent is not null)
                {
                    NPC festivalHost = ClickToMoveManager.Reflection
                        .GetField<NPC>(GameLocation.currentEvent, "festivalHost").GetValue();

                    if (festivalHost is not null && festivalHost.getTileLocation().Equals(tileVector))
                    {
                        interactionAtCursor = InteractionType.Speech;
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
            ClickKeyStates.SetMovement(WalkDirection.None);
            ClickKeyStates.ActionButtonPressed = false;

            if (ClickKeyStates.RealClickHeld
                && ((Game1.player.CurrentTool is Axe or Pickaxe
                     && GameLocation.IsChoppableOrMinable(clickedTile))
                    || (Game1.player.UsingTool
                        && Game1.player.CurrentTool is WateringCan or Hoe)))
            {
                ClickKeyStates.SetUseTool(true);
                phase = ClickToMovePhase.PendingComplete;
                return;
            }

            ClickKeyStates.SetUseTool(false);
            phase = ClickToMovePhase.Complete;
        }

        private void TryToFindAlternatePath(AStarNode startNode)
        {
            if (!endNodeOccupied
                || (!FindAlternatePath(startNode, clickedNode.X + 1, clickedNode.Y + 1)
                    && !FindAlternatePath(startNode, clickedNode.X - 1, clickedNode.Y + 1)
                    && !FindAlternatePath(startNode, clickedNode.X + 1, clickedNode.Y - 1)
                    && !FindAlternatePath(startNode, clickedNode.X - 1, clickedNode.Y - 1)))
            {
                Reset();
            }
        }

        private bool WateringCanActionAtEndNode()
        {
            if (Game1.player.CurrentTool is WateringCan wateringCan)
            {
                if (wateringCan.WaterLeft > 0)
                {
                    GameLocation.terrainFeatures.TryGetValue(
                        new Vector2(clickedNode.X, clickedNode.Y),
                        out TerrainFeature terrainFeature);

                    if (terrainFeature is HoeDirt dirt && dirt.state.Value != HoeDirt.watered)
                    {
                        return true;
                    }
                }

                if ((GameLocation is SlimeHutch
                     && clickedNode.X == 16
                     && clickedNode.Y is >= 6 and <= 9)
                    || GameLocation.CanRefillWateringCanOnTile((int) clickedTile.X, (int) clickedTile.Y))
                {
                    return true;
                }
            }

            return false;
        }
    }
}