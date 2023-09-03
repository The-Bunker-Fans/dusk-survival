using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Ghost.Components;
using Content.Server.Mind;
using Content.Server.Mind.Components;
using Content.Server.Roles.Jobs;
using Content.Server.Visible;
using Content.Server.Warps;
using Content.Shared.Actions;
using Content.Shared.Administration;
using Content.Shared.Examine;
using Content.Shared.Follower;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Storage.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Ghost
{
    public sealed partial class GhostSystem : SharedGhostSystem
    {
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private GameTicker _ticker = default!;
        [Dependency] private MindSystem _mindSystem = default!;
        [Dependency] private SharedActionsSystem _actions = default!;
        [Dependency] private VisibilitySystem _visibilitySystem = default!;
        [Dependency] private EntityLookupSystem _lookup = default!;
        [Dependency] private FollowerSystem _followerSystem = default!;
        [Dependency] private MobStateSystem _mobState = default!;
        [Dependency] private SharedPhysicsSystem _physics = default!;
        [Dependency] private MindSystem _minds = default!;
        [Dependency] private JobSystem _jobs = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GhostComponent, ComponentStartup>(OnGhostStartup);
            SubscribeLocalEvent<GhostComponent, ComponentShutdown>(OnGhostShutdown);

            SubscribeLocalEvent<GhostComponent, ExaminedEvent>(OnGhostExamine);

            SubscribeLocalEvent<GhostComponent, MindRemovedMessage>(OnMindRemovedMessage);
            SubscribeLocalEvent<GhostComponent, MindUnvisitedMessage>(OnMindUnvisitedMessage);
            SubscribeLocalEvent<GhostComponent, PlayerDetachedEvent>(OnPlayerDetached);

            SubscribeLocalEvent<GhostOnMoveComponent, MoveInputEvent>(OnRelayMoveInput);

            SubscribeNetworkEvent<GhostWarpsRequestEvent>(OnGhostWarpsRequest);
            SubscribeNetworkEvent<GhostReturnToBodyRequest>(OnGhostReturnToBodyRequest);
            SubscribeNetworkEvent<GhostWarpToTargetRequestEvent>(OnGhostWarpToTargetRequest);

            SubscribeLocalEvent<GhostComponent, BooActionEvent>(OnActionPerform);
            SubscribeLocalEvent<GhostComponent, InsertIntoEntityStorageAttemptEvent>(OnEntityStorageInsertAttempt);

            SubscribeLocalEvent<RoundEndTextAppendEvent>(_ => MakeVisible(true));
        }

        private void OnActionPerform(EntityUid uid, GhostComponent component, BooActionEvent args)
        {
            if (args.Handled)
                return;

            var ents = _lookup.GetEntitiesInRange(args.Performer, component.BooRadius);

            var booCounter = 0;
            foreach (var ent in ents)
            {
                var handled = DoGhostBooEvent(ent);

                if (handled)
                    booCounter++;

                if (booCounter >= component.BooMaxTargets)
                    break;
            }

            args.Handled = true;
        }

        private void OnRelayMoveInput(EntityUid uid, GhostOnMoveComponent component, ref MoveInputEvent args)
        {
            // Let's not ghost if our mind is visiting...
            if (EntityManager.HasComponent<VisitingMindComponent>(uid))
                return;

            if (!_minds.TryGetMind(uid, out var mindId, out var mind) || mind.IsVisitingEntity)
                return;

            if (component.MustBeDead && (_mobState.IsAlive(uid) || _mobState.IsCritical(uid)))
                return;

            _ticker.OnGhostAttempt(mindId, component.CanReturn, mind: mind);
        }

        private void OnGhostStartup(EntityUid uid, GhostComponent component, ComponentStartup args)
        {
            // Allow this entity to be seen by other ghosts.
            var visibility = EntityManager.EnsureComponent<VisibilityComponent>(component.Owner);

            if (_ticker.RunLevel != GameRunLevel.PostRound)
            {
                _visibilitySystem.AddLayer(visibility, (int) VisibilityFlags.Ghost, false);
                _visibilitySystem.RemoveLayer(visibility, (int) VisibilityFlags.Normal, false);
                _visibilitySystem.RefreshVisibility(visibility);
            }

            if (EntityManager.TryGetComponent(component.Owner, out EyeComponent? eye))
            {
                eye.VisibilityMask |= (uint) VisibilityFlags.Ghost;
            }

            var time = _gameTiming.CurTime;
            component.TimeOfDeath = time;

            // TODO ghost: remove once ghosts are persistent and aren't deleted when returning to body
            if (component.Action.UseDelay != null)
                component.Action.Cooldown = (time, time + component.Action.UseDelay.Value);
            _actions.AddAction(uid, component.Action, null);
        }

        private void OnGhostShutdown(EntityUid uid, GhostComponent component, ComponentShutdown args)
        {
            // Perf: If the entity is deleting itself, no reason to change these back.
            if (!Terminating(uid))
            {
                // Entity can't be seen by ghosts anymore.
                if (EntityManager.TryGetComponent(component.Owner, out VisibilityComponent? visibility))
                {
                    _visibilitySystem.RemoveLayer(visibility, (int) VisibilityFlags.Ghost, false);
                    _visibilitySystem.AddLayer(visibility, (int) VisibilityFlags.Normal, false);
                    _visibilitySystem.RefreshVisibility(visibility);
                }

                // Entity can't see ghosts anymore.
                if (EntityManager.TryGetComponent(component.Owner, out EyeComponent? eye))
                {
                    eye.VisibilityMask &= ~(uint) VisibilityFlags.Ghost;
                }

                _actions.RemoveAction(uid, component.Action);
            }
        }

        private void OnGhostExamine(EntityUid uid, GhostComponent component, ExaminedEvent args)
        {
            var timeSinceDeath = _gameTiming.RealTime.Subtract(component.TimeOfDeath);
            var deathTimeInfo = timeSinceDeath.Minutes > 0
                ? Loc.GetString("comp-ghost-examine-time-minutes", ("minutes", timeSinceDeath.Minutes))
                : Loc.GetString("comp-ghost-examine-time-seconds", ("seconds", timeSinceDeath.Seconds));

            args.PushMarkup(deathTimeInfo);
        }

        private void OnMindRemovedMessage(EntityUid uid, GhostComponent component, MindRemovedMessage args)
        {
            DeleteEntity(uid);
        }

        private void OnMindUnvisitedMessage(EntityUid uid, GhostComponent component, MindUnvisitedMessage args)
        {
            DeleteEntity(uid);
        }

        private void OnPlayerDetached(EntityUid uid, GhostComponent component, PlayerDetachedEvent args)
        {
            DeleteEntity(uid);
        }

        private void OnGhostWarpsRequest(GhostWarpsRequestEvent msg, EntitySessionEventArgs args)
        {
            if (args.SenderSession.AttachedEntity is not {Valid: true} entity ||
                !EntityManager.HasComponent<GhostComponent>(entity))
            {
                Logger.Warning($"User {args.SenderSession.Name} sent a {nameof(GhostWarpsRequestEvent)} without being a ghost.");
                return;
            }

            var response = new GhostWarpsResponseEvent(GetPlayerWarps(entity).Concat(GetLocationWarps()).ToList());
            RaiseNetworkEvent(response, args.SenderSession.ConnectedClient);
        }

        private void OnGhostReturnToBodyRequest(GhostReturnToBodyRequest msg, EntitySessionEventArgs args)
        {
            if (args.SenderSession.AttachedEntity is not {Valid: true} attached ||
                !EntityManager.TryGetComponent(attached, out GhostComponent? ghost) ||
                !ghost.CanReturnToBody ||
                !EntityManager.TryGetComponent(attached, out ActorComponent? actor))
            {
                Logger.Warning($"User {args.SenderSession.Name} sent an invalid {nameof(GhostReturnToBodyRequest)}");
                return;
            }

            _mindSystem.UnVisit(actor.PlayerSession);
        }

        private void OnGhostWarpToTargetRequest(GhostWarpToTargetRequestEvent msg, EntitySessionEventArgs args)
        {
            if (args.SenderSession.AttachedEntity is not {Valid: true} attached ||
                !EntityManager.TryGetComponent(attached, out GhostComponent? ghost))
            {
                Logger.Warning($"User {args.SenderSession.Name} tried to warp to {msg.Target} without being a ghost.");
                return;
            }

            if (!EntityManager.EntityExists(msg.Target))
            {
                Logger.Warning($"User {args.SenderSession.Name} tried to warp to an invalid entity id: {msg.Target}");
                return;
            }

            if (TryComp(msg.Target, out WarpPointComponent? warp) && warp.Follow
                || HasComp<MobStateComponent>(msg.Target))
            {
                 _followerSystem.StartFollowingEntity(ghost.Owner, msg.Target);
                 return;
            }

            var xform = Transform(ghost.Owner);
            xform.Coordinates = Transform(msg.Target).Coordinates;
            xform.AttachToGridOrMap();
            if (TryComp(attached, out PhysicsComponent? physics))
                _physics.SetLinearVelocity(attached, Vector2.Zero, body: physics);
        }

        private void DeleteEntity(EntityUid uid)
        {
            if (Deleted(uid) || Terminating(uid))
                return;

            QueueDel(uid);
        }

        private IEnumerable<GhostWarp> GetLocationWarps()
        {
            foreach (var warp in EntityManager.EntityQuery<WarpPointComponent>(true))
            {
                if (warp.Location != null)
                {
                    yield return new GhostWarp(warp.Owner, warp.Location, true);
                }
            }
        }

        private IEnumerable<GhostWarp> GetPlayerWarps(EntityUid except)
        {
            foreach (var player in _playerManager.Sessions)
            {
                if (player.AttachedEntity is {Valid: true} attached)
                {
                    if (attached == except) continue;

                    TryComp<MindContainerComponent>(attached, out var mind);

                    var jobName = _jobs.MindTryGetJobName(mind?.Mind);
                    var playerInfo = $"{EntityManager.GetComponent<MetaDataComponent>(attached).EntityName} ({jobName})";

                    if (_mobState.IsAlive(attached) || _mobState.IsCritical(attached))
                        yield return new GhostWarp(attached, playerInfo, false);
                }
            }
        }

        private void OnEntityStorageInsertAttempt(EntityUid uid, GhostComponent comp, ref InsertIntoEntityStorageAttemptEvent args)
        {
            args.Cancelled = true;
        }

        /// <summary>
        /// When the round ends, make all players able to see ghosts.
        /// </summary>
        public void MakeVisible(bool visible)
        {
            foreach (var (_, vis) in EntityQuery<GhostComponent, VisibilityComponent>())
            {
                if (visible)
                {
                    _visibilitySystem.AddLayer(vis, (int) VisibilityFlags.Normal, false);
                    _visibilitySystem.RemoveLayer(vis, (int) VisibilityFlags.Ghost, false);
                }
                else
                {
                    _visibilitySystem.AddLayer(vis, (int) VisibilityFlags.Ghost, false);
                    _visibilitySystem.RemoveLayer(vis, (int) VisibilityFlags.Normal, false);
                }
                _visibilitySystem.RefreshVisibility(vis);
            }
        }

        public bool DoGhostBooEvent(EntityUid target)
        {
            var ghostBoo = new GhostBooEvent();
            RaiseLocalEvent(target, ghostBoo, true);

            return ghostBoo.Handled;
        }
    }

    [AnyCommand]
    public sealed class ToggleGhostVisibility : IConsoleCommand
    {
        public string Command => "toggleghosts";
        public string Description => "Toggles ghost visibility";
        public string Help => $"{Command}";
        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player == null)
                shell.WriteLine("You can only toggle ghost visibility on a client.");

            var entityManager = IoCManager.Resolve<IEntityManager>();

            var uid = shell.Player?.AttachedEntity;
            if (uid == null
                || !entityManager.HasComponent<GhostComponent>(uid)
                || !entityManager.TryGetComponent<EyeComponent>(uid, out var eyeComponent))
                return;

            eyeComponent.VisibilityMask ^= (uint) VisibilityFlags.Ghost;
        }
    }
}
