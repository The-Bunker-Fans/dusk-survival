using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Fluids.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Directions;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Slippery;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Server.Fluids.EntitySystems
{
    [UsedImplicitly]
    public sealed class PuddleSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            _mapManager.TileChanged += HandleTileChanged;

            SubscribeLocalEvent<SpillableComponent, GetOtherVerbsEvent>(AddSpillVerb);
            SubscribeLocalEvent<PuddleComponent, ExaminedEvent>(HandlePuddleExamined);
            SubscribeLocalEvent<PuddleComponent, SolutionChangedEvent>(OnUpdate);
        }

        private void OnUpdate(EntityUid uid, PuddleComponent component, SolutionChangedEvent args)
        {
            UpdateSlip(uid, component);
            UpdateVisuals(uid, component);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _mapManager.TileChanged -= HandleTileChanged;
        }
        
        private void UpdateVisuals(EntityUid uid, PuddleComponent puddleComponent)
        {
            if (puddleComponent.Owner.Deleted || puddleComponent.EmptyHolder ||
                !EntityManager.TryGetComponent<SharedAppearanceComponent>(uid, out var appearanceComponent))
            {
                return;
            }

            // Opacity based on level of fullness to overflow
            // Hard-cap lower bound for visibility reasons
            var volumeScale = puddleComponent.CurrentVolume.Float() / puddleComponent.OverflowVolume.Float();
            var puddleSolution = _solutionContainerSystem.EnsureSolution(uid, puddleComponent.SolutionName);

            appearanceComponent.SetData(PuddleVisuals.VolumeScale, volumeScale);
            appearanceComponent.SetData(PuddleVisuals.SolutionColor, puddleSolution.Color);
        }
        
        private void UpdateSlip(EntityUid entityUid, PuddleComponent puddleComponent)
        {
            if ((puddleComponent.SlipThreshold == ReagentUnit.New(-1) ||
                 puddleComponent.CurrentVolume < puddleComponent.SlipThreshold) &&
                EntityManager.TryGetComponent(entityUid, out SlipperyComponent? oldSlippery))
            {
                oldSlippery.Slippery = false;
            }
            else if (puddleComponent.CurrentVolume >= puddleComponent.SlipThreshold)
            {
                var newSlippery =
                    EntityManager.EnsureComponent<SlipperyComponent>(EntityManager.GetEntity(entityUid));
                newSlippery.Slippery = true;
            }
        }

        private void AddSpillVerb(EntityUid uid, SpillableComponent component, GetOtherVerbsEvent args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            if (!_solutionContainerSystem.TryGetDrainableSolution(args.Target.Uid, out var solution))
                return;

            if (solution.DrainAvailable == ReagentUnit.Zero)
                return;

            Verb verb = new();
            verb.Text = Loc.GetString("spill-target-verb-get-data-text");
            // TODO VERB ICONS spill icon? pouring out a glass/beaker?
            verb.Act = () => _solutionContainerSystem.SplitSolution(args.Target.Uid,
                solution, solution.DrainAvailable).SpillAt(args.Target.Transform.Coordinates, "PuddleSmear");
            args.Verbs.Add(verb);
        }

        private void HandlePuddleExamined(EntityUid uid, PuddleComponent component, ExaminedEvent args)
        {
            if (EntityManager.TryGetComponent<SlipperyComponent>(uid, out var slippery) && slippery.Slippery)
            {
                args.PushText(Loc.GetString("puddle-component-examine-is-slipper-text"));
            }
        }

        //TODO: Replace all this with an Unanchored event that deletes the puddle
        private void HandleTileChanged(object? sender, TileChangedEventArgs eventArgs)
        {
            // If this gets hammered you could probably queue up all the tile changes every tick but I doubt that would ever happen.
            foreach (var puddle in EntityManager.EntityQuery<PuddleComponent>(true))
            {
                // If the tile becomes space then delete it (potentially change by design)
                var puddleTransform = puddle.Owner.Transform;
                if (!puddleTransform.Anchored)
                    continue;

                var grid = _mapManager.GetGrid(puddleTransform.GridID);
                if (eventArgs.NewTile.GridIndex == puddle.Owner.Transform.GridID &&
                    grid.TileIndicesFor(puddleTransform.Coordinates) == eventArgs.NewTile.GridIndices &&
                    eventArgs.NewTile.Tile.IsEmpty)
                {
                    puddle.Owner.QueueDelete();
                    break; // Currently it's one puddle per tile, if that changes remove this
                }
            }
        }

        /// <summary>
        ///     Whether adding this solution to this puddle would overflow.
        /// </summary>
        /// <param name="puddle">Puddle to which we are adding solution</param>
        /// <param name="solution">Solution we intend to add</param>
        /// <returns></returns>
        public bool WouldOverflow(PuddleComponent puddle, Solution solution)
        {
            return puddle.CurrentVolume + solution.TotalVolume > puddle.OverflowVolume;
        }

        public bool EmptyHolder(PuddleComponent puddleComponent)
        {
            return !_solutionContainerSystem.TryGetSolution(puddleComponent.Owner.Uid, puddleComponent.SolutionName,
                       out var solution)
                   || solution.Contents.Count == 0;
        }

        public ReagentUnit CurrentVolume(PuddleComponent puddleComponent)
        {
            return _solutionContainerSystem.TryGetSolution(puddleComponent.Owner.Uid, puddleComponent.SolutionName,
                out var solution)
                ? solution.CurrentVolume
                : ReagentUnit.Zero;
        }

        public bool TryAddSolution(PuddleComponent puddleComponent, Solution solution,
            bool sound = true,
            bool checkForOverflow = true)
        {
            if (solution.TotalVolume == 0 ||
                !_solutionContainerSystem.TryGetSolution(puddleComponent.Owner.Uid, puddleComponent.SolutionName,
                    out var puddleSolution))
            {
                return false;
            }


            var result = _solutionContainerSystem
                .TryAddSolution(puddleComponent.Owner.Uid, puddleSolution, solution);
            if (!result)
            {
                return false;
            }

            RaiseLocalEvent(puddleComponent.Owner.Uid, new SolutionChangedEvent());

            if (checkForOverflow)
            {
                CheckOverflow(puddleComponent);
            }

            if (!sound)
            {
                return true;
            }

            SoundSystem.Play(Filter.Pvs(puddleComponent.Owner), puddleComponent.SpillSound.GetSound(),
                puddleComponent.Owner);
            return true;
        }

        /// <summary>
        /// Will overflow this entity to neighboring entities if required
        /// </summary>
        private void CheckOverflow(PuddleComponent puddleComponent)
        {
            if (puddleComponent.CurrentVolume <= puddleComponent.OverflowVolume
                || puddleComponent.Overflown)
                return;

            var nextPuddles = new List<PuddleComponent>() { puddleComponent };
            var overflownPuddles = new List<PuddleComponent>();

            while (puddleComponent.OverflowLeft > ReagentUnit.Zero && nextPuddles.Count > 0)
            {
                foreach (var next in nextPuddles.ToArray())
                {
                    nextPuddles.Remove(next);

                    next.Overflown = true;
                    overflownPuddles.Add(next);

                    var adjacentPuddles = GetAllAdjacentOverflow(next).ToArray();
                    if (puddleComponent.OverflowLeft <= ReagentUnit.Epsilon * adjacentPuddles.Length)
                    {
                        break;
                    }

                    if (adjacentPuddles.Length == 0)
                    {
                        continue;
                    }

                    var numberOfAdjacent = ReagentUnit.New(adjacentPuddles.Length);
                    var overflowSplit = puddleComponent.OverflowLeft / numberOfAdjacent;
                    foreach (var adjacent in adjacentPuddles)
                    {
                        var adjacentPuddle = adjacent();
                        var quantity = ReagentUnit.Min(overflowSplit, adjacentPuddle.OverflowVolume);
                        var puddleSolution = _solutionContainerSystem.EnsureSolution(puddleComponent.Owner.Uid,
                            puddleComponent.SolutionName);
                        var spillAmount = _solutionContainerSystem.SplitSolution(puddleComponent.Owner.Uid,
                            puddleSolution, quantity);

                        TryAddSolution(adjacentPuddle, spillAmount, false, false);
                        nextPuddles.Add(adjacentPuddle);
                    }
                }
            }

            foreach (var puddle in overflownPuddles)
            {
                puddle.Overflown = false;
            }
        }

        /// <summary>
        /// Finds or creates adjacent puddles in random directions from this one
        /// </summary>
        /// <returns>Enumerable of the puddles found or to be created</returns>
        private IEnumerable<Func<PuddleComponent>> GetAllAdjacentOverflow(PuddleComponent puddleComponent)
        {
            foreach (var direction in SharedDirectionExtensions.RandomDirections())
            {
                if (TryGetAdjacentOverflow(puddleComponent, direction, out var puddle))
                {
                    yield return puddle;
                }
            }
        }

        /// <summary>
        /// Tries to get an adjacent coordinate to overflow to, unless it is blocked by a wall on the
        /// same tile or the tile is empty
        /// </summary>
        /// <param name="puddleComponent"></param>
        /// <param name="direction">The direction to get the puddle from, respective to this one</param>
        /// <param name="puddle">The puddle that was found or is to be created, or null if there
        /// is a wall in the way</param>
        /// <returns>true if a puddle was found or created, false otherwise</returns>
        private bool TryGetAdjacentOverflow(PuddleComponent puddleComponent, Direction direction,
            [NotNullWhen(true)] out Func<PuddleComponent>? puddle)
        {
            puddle = default;

            // We're most likely in space, do nothing.
            if (!puddleComponent.Owner.Transform.GridID.IsValid())
                return false;

            var mapGrid = _mapManager.GetGrid(puddleComponent.Owner.Transform.GridID);
            var coords = puddleComponent.Owner.Transform.Coordinates;

            if (!coords.Offset(direction).TryGetTileRef(out var tile))
            {
                return false;
            }

            // If space return early, let that spill go out into the void
            if (tile.Value.Tile.IsEmpty)
            {
                return false;
            }

            if (!puddleComponent.Owner.Transform.Anchored)
                return false;

            foreach (var entity in mapGrid.GetInDir(coords, direction))
            {
                if (EntityManager.TryGetComponent(entity, out IPhysBody? physics) &&
                    (physics.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                {
                    puddle = default;
                    return false;
                }

                if (EntityManager.TryGetComponent(entity, out PuddleComponent? existingPuddle))
                {
                    if (existingPuddle.Overflown)
                    {
                        return false;
                    }

                    puddle = () => existingPuddle;
                }
            }

            puddle ??= () =>
                puddleComponent.Owner.EntityManager.SpawnEntity(puddleComponent.Owner.Prototype?.ID,
                        mapGrid.DirectionToGrid(coords, direction))
                    .GetComponent<PuddleComponent>();

            return true;
        }
    }
}